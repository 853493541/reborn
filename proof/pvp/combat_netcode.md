# JX3 combat netcode / protocol — PvP static research

**Branch:** `research/jx3-pvp-battle`
**Date:** 2026-09-24
**Method:** read-only static analysis. Strings from the committed dumps
(`proof/netcode/*`) + targeted disassembly of `JX3LogicEditOperationX64.dll`
(image base `0x180000000`) with capstone 5.0.7 (`tools/pvp/dump_fn_disasm.py`).
No live capture, no hooking, no client modification.
**Raw evidence:** `proof/pvp/netcode/` (string hits, disasm, struct runs),
`proof/pvp/combat_opcodes.tsv`, `proof/pvp/combat_netcode.md` (this file).

Confidence labels:

| Label | Meaning |
|---|---|
| **HIGH** | immediate read from disassembly/string with cited address (offset in code is exact) |
| **MED** | offset exact, *semantics* inferred from naming/context/call target |
| **LOW** | heuristic catalog row, naming only, or single-string lead |

Address conventions: disasm addresses are VAs in `JX3LogicEditOperationX64.dll`
(image base `0x180000000`); addresses in the `*_net_strings.txt` dumps are **file
offsets** (verified: file off `0x771400` → VA `0x180772800`). Both are cited.

---

## 0. Headline findings

1. **Combat is fully server-arbitrated.** The client has no "cast accepted" logic of
   its own: `OnSkillPrepare/OnSkillCast/OnSkillChannel/OnSkillEffectResult` are
   `KPlayerClient` S2C handlers. Client-side cast requests are only
   `DoCastProfessionSkill` (0x49), `DoCharacterSkill` (0x1B), `DoCastHoardSkill`
   (0x104), `DoStartHoardSkill` (0x103) — all now pinned to exact IDs/sizes by
   disassembly (HIGH). See §1.
2. **Damage/heal numbers arrive in one S2C message**: `OnSkillEffectResult` is
   variable-length, `0x23 + 9*n` bytes, with a signed byte `cResultCount` at `+0x22`
   and `n` records of exactly 9 bytes (`KSKILL_RESULT`) beginning `+0x23`
   (HIGH; assert `nSize == sizeof(S2C_SKILL_EFFECT_RESULT) + sizeof(...KSKILL_RESULT) *
   pSkillResult->cResultCount`, exe file off `0x007C04C0`). See §2/§3.
3. **Cooldowns are server-owned clocks, not client timers.** The server sends
   reset (`OnResetCooldown`), pause (`OnPauseCDTimer`), accelerate
   (`OnAccelerateCDTimer`) and overdraft-charge (`OnCoolDownOverDraftNotify`)
   messages; the client stores server time bases (`entity+0xB924` etc.) and renders.
   `CoolDownList.tab` (3,510 rows) controls which CDs can be paused/reset
   (`CanBackup`), accelerated (`CanAccelerate`) and shared with competitors
   (`NeedSyncOB`). See §5.
4. **Arena/battlefield competitor sync is a pull-on-demand overlay**: the client
   asks for a competitor list (`DoSyncBattlefieldCompetitorsListRequest` 0x164) or a
   competitor's CD state (`DoSyncBattlefieldCompetitorSkillCDStateRequest` 0x161,
   cancel 0x162), and receives base info / variable info / buff list / CD state /
   statistics pushes (`OnSync*FromBattlefieldCompetitorList`,
   `OnSyncBattlefieldCompetitorCDState/BuffList/Statistics`). See §8.
5. **No network rollback/replay netcode exists in the client.** All
   "rollback/replay" strings belong to an unrelated util library (`RollbackImpl`),
   SQLite, or the representation-side *video replayer* (`KVideoReplayer`,
   `RLReplaySystem`, `RLReplaySkillEffect`). Combat prediction is presentational:
   UI progress events + animation (`KRLCharacter::PrepareCastSkill/CastSkill`,
   `KVideoReplayer::OnUIDataSkillPrepareProgress/SkillChannelProgress/HoardProgress`).
   See §4.3.

---

## 1. C2S combat opcodes (verified by disassembly)

All rows were read from the request builder function (proto id immediate + packet
size passed to `KPlayerClient::SendPacket` @ `0x1801ACDA0`). Raw disasm:
`proof/pvp/netcode/disasm/`. Full TSV: `proof/pvp/combat_opcodes.tsv`.

| Method | Proto | Size | Payload (after 11-B header) | Evidence | Conf. |
|---|---|---|---|---|---|
| `DoCharacterSkill` | `0x1B` | `0x1D` | u32@+0xB, u8@+0xF, u8@+0x10(status), u32@+0x11, u32@+0x15, u8@+0x19 | `disasm/KPlayerClient__DoCharacterSkill.txt` fn `0x180170B30`; `mov eax,0x1b` @`0x180170B52`; `mov r8d,0x1d` @`0x180170C04` | HIGH id/size |
| `DoCastProfessionSkill` | `0x49` | `0x20` | u32@+0xB, u32@+0xF, u8@+0x13, u32@+0x14, u32@+0x18, u32@+0x1C | `disasm/KPlayerClient__DoCastProfessionSkill.txt` fn `0x180170270`; `mov eax,0x49` @`0x1801702CB`; `r8d=0x20` @`0x1801703B4` | HIGH id/size |
| `DoCastHoardSkill` | `0x104` | `0x1F` | 5 × u32 @ +0xB,+0xF,+0x13,+0x17,+0x1B | `disasm/KPlayerClient__DoCastHoardSkill.txt` fn `0x1801701C0`; `0x1801701D3`, `0x1801701FD` | HIGH id/size |
| `DoStartHoardSkill` | `0x103` | `0x21` | u32@+0xB, u8@+0xF, u32@+0x10, u8@+0x14, u32@+0x15, u32@+0x19, u8@+0x1D | `disasm/KPlayerClient__DoStartHoardSkill.txt` fn `0x18017A6C0`; `0x18017A6E2`, `0x18017A79C` | HIGH id/size |
| `DoPlayerReviveRequest` | `0xB9` | `0xF` | u32@+0xB | `disasm_deep2/KPlayerClient__DoPlayerReviveRequest.txt` fn `0x180176A90`; `0x180176AA3`, `0x180176AB6` | HIGH |
| `DoApplyCharacterBuffList` | `0x20` | `0xF` | u32@+0xB (character id) | `disasm/KPlayerClient__DoApplyCharacterBuffList.txt` fn `0x18016B6F0`; `0x18016B736`, `lea r8d,[rax-0x11]` @`0x18016B749` | HIGH |
| `DoSyncBattlefieldCompetitorSkillCDStateRequest` | `0x161` | `0xF` | u32@+0xB = `[request+8]` (target) | `disasm/KPlayerClient__DoSyncBattlefieldCompetitorSkillCDStateRequest.txt` fn `0x18017AAE0` | HIGH |
| `DoCancelSyncBattlefieldCompetitorSkillCDStateRequest` | `0x162` | `0xB` | none | `disasm/KPlayerClient__DoCancelSyncBattlefieldCompetitorSkillCDStateRequest.txt` fn `0x1801700A0` | HIGH |
| `DoSyncDungeonCompetitorSkillCDStateRequest` | `0x1E6` | `0xF` | u32@+0xB = `[request+8]` | `disasm/KPlayerClient__DoSyncDungeonCompetitorSkillCDStateRequest.txt` fn `0x18017AE00`; `0x18017AE57`, `0x18017AE5C` | HIGH id/size |
| `DoCancelSyncDungeonCompetitorSkillCDStateRequest` | `0x1E7` | `0xB` | none | `disasm/KPlayerClient__DoCancelSyncDungeonCompetitorSkillCDStateRequest.txt` fn `0x180170130`; `0x180170143`, `0x18017014D` | HIGH |
| `DoSyncBattlefieldCompetitorsListRequest` | `0x164` | `0xB` | none; input fields `[req+9]`, `[req+0xD]` | `JX3_PROTOCOL_SPEC.md:35`; `disasm/KPlayerClient__DoSyncBattlefieldCompetitorsListRequest.txt` fn `0x1801A9470` | HIGH |
| `DoSyncOBCompetitorSkillList` | `0x71` | `0x3B` | u32@+0xB,+0xF,+0x13,+0x17; 32-B blob @+0x1B; u8@+0x3A | `disasm/KPlayerClient__DoSyncOBCompetitorSkillList.txt` xref1 fn `0x18017B0F0`; `0x18017B110`, `0x18017B161`, 0x20-B copy @`0x18017B156` | HIGH id/size |
| `DoApplyBFRoleData` | `0x153` | `0x17` | n/a (catalog + auto-summary) | `JX3LogicEditOperation_net_strings` row, catalog `c2s_protocol_catalog.tsv:29` | MED |
| `DoApplyBFPlayerTeamGroupIDInfo` | `0x160` | `0xB` | none | catalog `:28`, auto-summary | MED |
| `DoSyncSubSkillPosition` | `0x71`/`0x14F`/`0x1A8` | `0x3B`/`0xF`/`0x1F` | attribution collides with `DoSyncOBCompetitorSkillList` | catalog `:371-373` | LOW |

Notes:
- `DoCharacterSkill`/`DoStartHoardSkill` share the shape
  `status byte @+0x14` set to `2` when the target resolves through
  `0x1801DBA60`, else `3/4/5` from the arg checks (HIGH offsets, MED meaning).
  This looks like an *action kind / OT-action* discriminator.
- The `0x49` builder passes 4 payload dwords plus a byte and is the most likely
  "player pressed skill" intent (skill id, target/aim, mode).
- There is **no `DoSelectTarget`** in the client protocol. Only client-side
  selection helpers exist (`KSkill::AutoSelectTarget`, `ProcessDisableSelectTarget`,
  `pDstCharacter->m_nDisableSelectTargetCounter` — exe file offsets
  `0x0080B450`, `0x00841A88`, `0x00841AA8`). Target choice is not a C→S message in
  the extracted surface.

## 2. S2C combat handlers (payload shapes read from the handler code)

S2C protocol **IDs are not recoverable from strings** — dispatch uses a compiled
`m_nProtocolSize` table (`JX3_PROTOCOL_SPEC.md` §6) whose contents were not dumped.
All handler addresses below are HIGH; field offsets are HIGH reads of the packet
pointer; meaning is MED unless the client's own assert names the struct.

| Handler | Packet shape (offsets from frame start, header = 11 B) | Handler addr / evidence | Conf. |
|---|---|---|---|
| `OnSkillPrepare` | `[+7]` caster (bit30 = NPC), `[+0xB]` skill id, `[+0xF]` level, `[+0x10]` cast time, `[+0x14]` kind (2 vs 3/4), `[+0x15/+0x19/+0x1D]` extras for kind 2 | fn region `0x180190340`, xref `0x180190408` (`disasm_deep2/...OnSkillPrepare.txt`) | HIGH offsets / MED meaning |
| `OnSkillCast` | `[+7]` caster, `[+0xB]` skill, `[+0xF]` level, `[+0x10]` val (cast time), `[+0x14]` has-aim flag, `[+0x15]`, `[+0x16]` kind, `[+0x17/+0x1B/+0x1F/+0x23]` | fn `0x18018F220`, xref `0x18018F2D5` | HIGH offsets / MED |
| `OnSkillChannel` | `[+7]` caster, `[+0xB]` skill, `[+0xF]` level, `[+0x10/+0x14]`, `[+0x18]` kind, `[+0x19/+0x1D/+0x21]` | fn `0x18018F760`, xref `0x18018F828` | HIGH offsets / MED |
| `OnSkillEffectResult` | min 35 B; `[+9]`,`[+0xD]` ids; `[+0x15]` u8; `[+0x16]` u32; `[+0x1A]` u8; `[+0x1B]` flags (bits 0,1,2,3,6 read); `[+0x1C]` flags (bits 0,2,6,7 read); `[+0x22]` s8 result count; 9-B records @`+0x23` | fn `0x18018F950`, xref `0x18018F9BC`; size checks @`0x18018F9A9` and `0x18018FA8B` | HIGH shape |
| `OnSkillBeatBack` | `[+7]` entity, `[+0xB]` u32, `[+0xF]` u32 flag; dispatch on `entity+0xC70` (switch 0..9) | fn `0x18018EEC0`, xref `0x18018F095` | HIGH offsets |
| `OnSkillRayEffect` | `[+0xB]` skill, `[+0xF]` caster, `[+0x13]` entity, `[+0x14]` val | xref `0x1801905D6` (same region as prepare) | MED |
| `OnSkillChainEffect` | `[+9]` skill, `[+0xD]` val, `[+0x11]` caster (bit30), `[+0x15]` u32 count, array after | fn `0x18018F4A0`, xref `0x18018F731` | HIGH offsets |
| `OnPointChainSkillEffect` | sized `0x29 + 4*n`; count `[+0x25]`; fields `[+0xD,+0x11,+0x15,+0x19,+0x1D,+0x21]`; DWORD array @`+0x29` | fn `0x18018C160`; `lea rax,[rcx*4+0x29]` @`0x18018C1A3` | HIGH shape |
| `OnStartHoardSkill` | `[+7]` caster, `[+0xB]` skill, `[+0xF]` level, `[+0x10]` kind, `[+0x11/+0x15/+0x19]` | fn `0x180190790`, xref `0x180190854` | HIGH offsets |
| `OnCharacterDeath` | `[+7]` dead id, `[+0xB]` killer id, `[+0xF/+0x13/+0x17]` u32s, `[+0x1B]` u32 → `entity+0x44` | fn `0x1801833A0`, xref `0x18018341B` | HIGH offsets |
| `OnSyncBuffList` | sized: fixed `0x17` + u16 `wDataSize` @`+0x15`; entity @`+9`; `[+0xD]`,`[+0x11]`; blob @`+0x17` | fn `0x180195010`; assert `nSize == sizeof(S2C_SYNC_BUFF_LIST) + pSyncBuff->wDataSize` (exe off `0x007C0640`) | HIGH shape |
| `OnSyncBuffSingle` | not extracted | string only: logic off `0x007717A0` | LOW |
| `OnSyncPlayerStateInfo` | 30+ scalar reads `+7..+0x58` (u8/u16/u32 mix; full map in TSV/disasm) | fn `0x1801A1E60`, xref `0x1801A1E8C` | HIGH offsets / LOW meaning |
| `OnSyncMoveState` | `+0xB/+0xC/+0xD` u8; vector-ish @`+0x12`; blob @`+0x1B`; packed u64 @`+0x2B` (X: bits0-17, Y: 18-35, Z: 36-61, nibble 58-61, flag 62); `+0xE` bitfield; `+0x33` u8 | fn `0x18019CF90`, xref `0x18019D301` | HIGH offsets / MED bitfields |
| `OnSyncStealthCharacter` | `[+7]` entity then a binary blob serialised through `0x1800D4BB0` | fn `0x1801A7310`, xref `0x1801A7359`; neighbour writes `[+0xB]→entity+0x4AC`, `[+0xC]→entity+0x4C4` | MED |
| `OnSetCamp` / `OnSetForce` / `OnSetBattleFieldSide` | not extracted | logic off `0x00774B68` / `0x007730D8` / `0x007730F8` | MED existence |
| `OnSyncCampInfo` | `sizeof(S2C_SYNC_CAMP_INFO) + byCampNpcCount*sizeof(KSYNC_CAMP_NPC_INFO)` | exe off `0x007C2CC0` (assert) | HIGH shape |
| `OnSyncBattleStatFlag`, `OnPVEDamageStatNotify`, `OnSyncTargetOutputDamage` | not extracted | logic off `0x0077A418`, `0x0077A3B8`, `0x0077B168` | MED existence |

Note on `OnSyncBuffList` entity offset: the handler reads a dword at `+9`, i.e.
bytes 9..0xC (the common header's u32 field7 upper half plus body bytes), unlike
the skill handlers that read the field7 dword at `+7`. Treat the exact id byte
range as an implementation detail to verify with a capture; the **size prefix is
the reliable part** (`+0x15` u16 + data `+0x17`).

## 3. Struct field runs (reconstructed wire lists)

Raw runs: `proof/pvp/netcode/struct_member_runs_logic.txt`. The client ships its
own assert text, which is the strongest struct evidence:

- `nSize == sizeof(S2C_SKILL_EFFECT_RESULT) + sizeof(S2C_SKILL_EFFECT_RESULT::KSKILL_RESULT) * pSkillResult->cResultCount`
  (exe off `0x007C04C0`) and `pSkillResult->cResultCount < MAX_UI_SKILL_RESULT_TYPE_COUNT`
  (logic off `0x0077163D` run) → **S2C_SKILL_EFFECT_RESULT** fixed part 0x23 B,
  **KSKILL_RESULT** = 9 B each (proved independently by `lea rcx,[rax*8+0x23]` +
  `add rcx, rax` = 9·n at `0x18018FA8B`).
- `nSize == sizeof(S2C_SYNC_BUFF_LIST) + pSyncBuff->wDataSize` (exe `0x007C0640`)
- `nSize == sizeof(S2C_SYNC_BATTLEFIELD_COMPETITOR_BUFF_LIST) + pSync->wDataSize`
  (exe `0x007C8020`) → competitor buff list shares the buff-list wire shape.
- `nSize == sizeof(S2C_POINT_CHAIN_SKILL_EFFECT) + sizeof(DWORD) * pPak->nCount`
  (exe `0x007C05D0`) → count-prefixed DWORD list (matches §2).
- `sizeof(S2C_SYNC_ARENA_COMPETITIOR_CD_STATE) == nSize` (logic off `0x007B2A4D`
  region, `KVideoReplayer::OnSyncArenaCompetitorCDState`) → fixed-size 4-dword
  competitor CD state (matches §8).
- `KVideoReplayer::OnPauseCDTimer` asserts `pPause->byProtocolID == s2c_pause_cd_timer`
  and `nSize == sizeof(S2C_PAUSE_CD_TIMER)`; same pattern for
  `s2c_accelerate_cd_timer` / `S2C_RESET_COOLDOWN` / `S2C_COOLDOWN_OVER_DRAFT_NOTIFY`
  (logic off `0x007B2A4D`..`0x007B2BB8`) → confirms protocol type names.
- Arena records: `sizeof(KV2C_SYNC_ARENA_RECORD_INFO_LIST_RESPOND) + byCount *
  sizeof(KARENA_PLAYER_RECORD_INFO)` (logic off `0x007972F0`);
  `sizeof(S2C_GET_ARENA_RECOMMEND_LIST_RESPOND) + uListCount *
  sizeof(KARENA_RECOMMEND_COMPETITION_NODE)`, `uListCount <= KARENA_RECOMMEND_LIST_SIZE`
  (logic off `0x00778284` region).

Field-name runs (member arrays) around the skill/buff loaders are contiguous in
`.rdata` and were extracted (raw file above). Most relevant quotes (file offsets,
`JX3LogicEditOperationX64.dll`):

- `0x00784316` run (skill/buff recipe row): `...dwLearnSkillID, dwLearnSkillLevel,
  dwSelfMoveStateMaskLow, dwSelfMoveStateMaskHigh, dwTargetMoveStateMaskLow,
  dwTargetMoveStateMaskHigh, KBuffManager::Init, TopBuff.tab, skill/Buff.tab,
  skill/BuffRecipe.tab...` — move-state masks are **per-skill data**, matching
  finding #9 in `JX3_NETCODE_RESEARCH.md` (server rejects casts by move state).
- `0x007823CD` / `0x0078321C` runs: `KSkillManager::LoadSkillRealizationTable`,
  `dwRecipeID`, `MirrorSkillID`, `LoadSkillRecipeBaseInfo`, `LoadSkillRecipeMirrorInfo`
  — the recipe/skill-id tables (not the wire).
- `0x0084C2A1`..`0x0084C3E9` runs (attribute/result struct): `nPoisonHitValue,
  nPoisonHitBaseRate, nPoisonCriticalStrikeBaseRate, nPoisonCriticalDamagePowerBaseKiloNumRate,
  nPoisonCriticalDamagePower, nPoisonOvercome, nPoisonMagicShield, ... nTherapyPower,
  nSkillTherapy, nBeTherapyCoefficient, nOverflowTh...` — this is the **damage
  pipeline vocabulary** the result records summarise (`hit value`, `critical`,
  `overcome`, `shield`, `therapy`). It is a stat struct, not proven to be the wire.
- `0x0084CA85`: `nCurrentKillPoint, bHuntingFlag, bHunterQualification,
  bOnlyReviveInSitu, bCannotDialogWithNPC, bRedName, dwKillCount, dwBestAssistKil...`
  → PvP player-state flags (kill points / red name / revive-in-situ).
- `0x00802CF8`/`0x00803391`: NPC template `ReviveTime, DynamicReviveMinTime,
  bCanSeeLifeBar, bCanSeeLifeNum, bCanSeeName, nKnockedBackRate...` → visibility +
  knock-back rules are **content data**, server-side.
- `0x007F67A8`/`0x00838FF8`/`0x0084D059`: combat stat names (`...CriticalStrike,
  ...AttackPowerBase, ...OvercomeBase, ...MagicShieldBase, ...RestoreCount`).

No run containing `dwCasterID`, `nTargetID`, `nHitType`, `SkillInfo`, `BuffInfo`,
`HitResult`, `DamageResult` was found in the scanned DLL — those exact names do
not exist as literals; the client uses `KSKILL_RESULT` + the `S2C_*` structs above.

## 4. Skill event model

### 4.1 Observable sequence (server → client, all authoritative)

```
C→S DoCastProfessionSkill (0x49) / DoCharacterSkill (0x1B) / Do(Start|Cast)HoardSkill (0x103/0x104)
        │  (server validates: alive, move-state/control, range/angle, LOS, resource, CD, silence/immunity,
        │   target relation/camp, stealth)
        ▼
S2C OnSkillPrepare   {caster@+7, skill@+0xB, level@+0xF, cast_time@+0x10, kind@+0x14, …}
S2C OnSkillCast      {caster@+7, skill@+0xB, level@+0xF, val@+0x10, has_aim@+0x14, kind@+0x16, …}
   (OnStartHoardSkill {…, kind@+0x10} for charge/hoard skills)
S2C OnSkillChannel   {caster@+7, skill@+0xB, level@+0xF, kind@+0x18, ticks…}
S2C OnSkillEffectResult {ids@+9/+0xD, flags@+0x1B/+0x1C, cResultCount@+0x22, KSKILL_RESULT[9*n]@+0x23}
   ── damage/heal actually happens here (the only result message)
S2C OnSkillBeatBack / OnSkillRayEffect / OnSkillChainEffect / OnPointChainSkillEffect
   ── derived motion/visual events (knock-back, beams, chained targets)
```

`OnSkillPrepare/OnSkillCast/OnSkillChannel/OnStartHoardSkill` all resolve the
caster with the same pattern: id at `+7`, `bt id,0x1e` selects player
(`0x18012B610`) vs NPC (`0x18012B5B0`). The "kind" byte (`2` vs `3/4`) selects
either a 3-dword form (`0x1801DC020`) or a 1-dword form (`0x1801DC210`); this is
the same code family the client uses for prepare/cast/channel/hord variants.

### 4.2 Client-side handling (representation)

- Local character: `KRLLocalCharacter::PrepareCastSkill` / `CastSkill`;
  remote: `KRLRemoteCharacter::PrepareCastSkill` / `CastSkill`;
  common: `KRLCharacter::BreakPrepareCastSkill`, `PlaySkillCasterEffect`,
  `PlaySkillEffectResultAnimation`, `PlayUnBindSkillEffectResultSFX`,
  `KRLSkillEffectResult::KSkillEffectResultData::InvokeResult`.
  (`JX3RepresentX64_net_strings.txt` @ `0x00CB2CB0..0x00CB2CE0`,
  `0x00CB4800..0x00CB4830`, `0x00CAC5B0..0x00CADCC0`, `0x00CBA950`.)
- Adaptor: `krlEventAdaptor::HandlePrepareCastSkill` / `HandleCastSkill` /
  `HandleSkillEffectResult`; `KGameWorldHandler::OnCharacterCastSkill` /
  `OnCharacterSkillEffectResult`; `KWorldRepresentAgent::OnSkillEffectResult`.
- UI progress is local-only: `KVideoReplayer::OnUIDataSkillPrepareProgress`,
  `OnUIDataSKillChannelProgress`, `OnUIDataSkillHoardProgress`, `OnUIDataSkillCastLog`
  (`JX3LogicEditOperation_net_strings.txt` @ `0x007B2EF8..0x007B2FD0`).

### 4.3 Prediction / rollback / replay search result

- **No network rollback or resim** strings: `rollback`/`RollBack` hits are
  `util::RollbackImpl` (scope-guard template), SQLite ("cannot rollback - no
  transaction is active"), and an SSL string. There is no `resim`/`re-sim` at all.
- **Interpolation is a view concern**: `KRLLocalCharacterFrameData::Interpolate`,
  `KRLRemoteCharacterFrameData::Interpolate` (`JX3RepresentX64_net_strings.txt`
  @ `0x00CCCBE8`, `0x00CCCC40`) and the runtime toggle
  `disable remote character interpolate` (@ `0x00C86208`) — unchanged from the
  earlier netcode research.
- **"Replay" is video/effect replay**: `RLReplaySystem::PushSkillEffectResult`,
  `RLReplaySkillEffect`, `KPROTOBUF_CONVERTOR::__KPB_*_RLReplaySkillEffect`; the
  arena replay records the same protocol structs (`KVideoReplayer::On*`).
- Conclusion (HIGH confidence for *absence in static strings*, MED for behaviour):
  combat has **no client-side rollback**; the only prediction is animation/UI, and
  the client reconciles by trusting the next server message.

## 5. Cooldown sync semantics

### 5.1 S2C handlers (offsets HIGH, meaning MED)

| Handler | Fields | Effect in client |
|---|---|---|
| `OnResetCooldown` fn `0x18018D4D0` | entity `+7` (player only); cd id `+0xB`; s32 `+0xF`; u32 `+0x13` | `+0xF == -1` → `0x180314050(mgr, cd_id)` (reset/all); else `0x1803143F0(mgr, cd_id, u32@+0x13, mgr[0xB924] + s32@+0xF)` — i.e. **set remaining time from server base + delta** |
| `OnPauseCDTimer` fn `0x18018B9A0` | entity `+7`; u8 `+0xB` | stores `entity+0xB920 = byte` (pause flag) |
| `OnAccelerateCDTimer` fn `0x18017DB80` | entity `+7`; u8 `+0xB`; u32 `+0x10` | stores `entity+0xB928 = byte`, `entity+0xB92C = u32`; calls flush `0x180314520` before/after |
| `OnCoolDownOverDraftNotify` fn `0x180184170` | entity `+7`; u32 `+0xB`; u32 `+0xF` | `+0xF == -1` → `0x180314130(mgr)`; else `0x180314460(mgr, u32@+0xB, u32@+0xF)` |

The client's cooldown manager is the object at `entity+0xB930`; `+0xB920`,
`+0xB924`, `+0xB928`, `+0xB92C` are its pause/base/accelerate fields.

### 5.2 `CoolDownList.tab` (3,510 rows, 11 columns)

Parsed copy: `proof/pvp/netcode/cool_down_list_analysis.txt`. Columns
(ID, Duration, MinDuration, note, Usage, MaxCount, MaxDuration, CanBackup,
MaxOverDraftCount, CanAccelerate, NeedSyncOB); durations are floats in **seconds**
(many fractional: 0.0625, 0.5; max observed 99999999). The client loader
(`0x1802E5440`, xref of the literal `NeedSyncOB` @ `0x1802E577C`) reads the columns
in order `MinDuration, MaxDuration, MaxCount, CanBackup, MaxOverDraftCount,
CanAccelerate, NeedSyncOB`, multiplies float columns by a scale (likely ×1000 for
ms), and writes a 0x40-byte row.

Statistics (whole table):

| Column | Non-zero | Meaning (evidence) |
|---|---|---|
| `CanBackup` | 2328 / 3510 | cooldown can be backed up / preserved (client keeps the CD through its own backup path); 560 rows forbid it |
| `CanAccelerate` | 2041 / 3510 | server may accelerate this CD (`OnAccelerateCDTimer`); getter string `GetCanAccelerateCooldownFlag %u Faild!` @ logic `0x007AA8A8` |
| `NeedSyncOB` | 837 / 3510 | CD is exposed to OB/competitor sync (`OnSyncBattlefieldCompetitorCDState`, `DoSyncOBCompetitorSkillList`); `bOBFlag` lives in the `KPlayer` member run @ logic `0x007DD2B8` |
| `MaxOverDraftCount` | 31 / 3510 | charge/overdraft stack limit; observed values 2, 3, 4, 6 — `GetMaxCoolDownOverDraftCount %u Faild!` @ `0x007AA880` |
| `MaxCount` | 3223 rows = 1; 142 = 2; 62 = 3; … | max stacks (assert `nMaxStackCount >= 1` sits immediately before `CanBackup`) |

`GetCoolDownValue/GetMinCoolDownValue/GetMaxCoolDownValue/GetMaxCoolDownStackCount/
GetCoolDownInfo` getters are all in the same literal run (`0x007AA7F8..0x007AA8D0`),
so the runtime reads every column from this table. `Usage` is a 53-value enum
(likely CD keying/class); we did not decode it.

## 6. Server authority for PvP — what a server must enforce

Evidence vocabulary (all strings in `JX3LogicEditOperationX64.dll` /
`JX3ClientX64.exe` net dumps; these are client-visible rejection/result enums):

| Gate | Evidence | What the server must validate |
|---|---|---|
| alive / dead | `OnCharacterDeath` fn `0x1801833A0`; `bOnlyReviveInSitu` @ logic `0x0084CA85`; `DoPlayerReviveRequest` 0xB9 | no cast while dead; revive legality (point/type/in-situ) |
| move-state / control | `MOVE_STATE_ERROR`, `MOVE_STATE_INVALID`, `YOU_MOVE_STATE_WRONG`, `TARGET_MOVE_STATE_WRONG`, `DST_MOVE_STATE_ERROR` (logic `0x007D4510`..`0x007ED298`); skill rows carry `dwSelfMoveStateMaskLow/High`, `dwTargetMoveStateMaskLow/High` @ `0x00784316`; `OnSyncMoveCtrl` | caster/target move-state mask per skill; server can lock movement (`OnSyncMoveCtrl`) |
| cooldown / charges | `OnResetCooldown`, `OnPauseCDTimer`, `OnAccelerateCDTimer`, `OnCoolDownOverDraftNotify`; `IN_COOLDOWN`, `QUEUE_COOLDOWN`, `PLAY_COOLDOWN_LIMIT` | server-owned CD/charge state; client only renders |
| resource | `OnSyncSelfCurrentST` (logic `0x00772FE0`), `OnSyncSelfCurrentSprintPower` (`0x007718A0`), `OnSyncSelfCurrentLMRS` (`0x00771878`) | stamina/mana/sprint checks before apply |
| silence / control / immunity | `KSkill::LuaCheckSilence` (exe `0x00869B40`); `dwSkillImmunity` (exe `0x007FAF90`), `IgnoreImmunityCast` (`0x0080CF18`), `bIgnoreImmunityCastID` (`0x0080FEE8`); `OnChangeImmunityCastIDNotify`, `OnChangeMultiImmunityCastIDNotify` | silence/control/immunity must be applied server-side; immunity cast-id changes are broadcast |
| target relation / camp | `OnSetCamp`, `OnSetForce`, `OnSetBattleFieldSide`, `OnSyncCampInfo` (`0x007C2CC0`), `OnSyncSceneCampTypeToPlayer`, `OnSyncRelationAllEnemyExceptTeam` (`0x007C7DA0`), `KSYNC_CAMP_NPC_INFO` | friendly/enemy resolution; battlefield side; first-attack rules (`FirstAttackOnCampFoe` exe `0x00808680`) |
| stealth | `OnSyncStealthCharacter` fn `0x1801A7310`; `bCounterStealth`, `nCounterStealthRange` (logic `0x007669FE` run) | caster invisibility / target counter-stealth decides whether the attack lands |
| range / angle / LOS | no client-side validation strings found; target position helpers are client-only (`GetCastSkillTargetPosition`, `CanCastOnTower`); `CAST_SKILL_NO_TARGET` @ logic `0x007F81F8` | range/angle/obstacle/LOS are **server-side only** in JX3; the client only gets the result (`OnSkillEffectResult` or nothing) |
| channel interruption | `OnSkillChannel`, `OnSkillChannelOTActionEnd` (`0x007A5328`), `OnSkillBeatBack`, `IMMUNE_SKILL_MOVED` (`0x007D6360`) | server ends channels (interrupt/beat-back/OT-action end) and broadcasts |
| death / revive | `OnCharacterDeath`, `DoPlayerReviveRequest`, `ReviveTime/DynamicReviveMinTime` (NPC data `0x00802CF8`) | server decides death, kill credit (`dwKillCount`, `bRedName`, `nCurrentKillPoint`) and revive timing |

Server must broadcast (owner + observers): prepare/cast/channel/effect-result,
beat-back/ray/chain, hoard start/cast, cd set/reset/pause/accelerate/overdraft,
buff full/incremental, control/immunity changes, stealth state, death/revive,
camp/force/side, player-state and move-state, and (PvP modes) competitor
base/variable/buff/CD/statistics.

## 7. PROPOSAL — reborn PvP opcode/event set (ours, not JX3's)

> **This section is PROPOSAL**, extending `REBORN_SERVER_SPEC.md` §7. IDs are ours;
> no JX3 opcode or byte is reused. Payloads use little-endian fixed fields
> (`KNetBuffer`-style) and the 15-byte frame from `REBORN_SERVER_SPEC.md` §2.

### 7.1 C→S

| ID | Name | Payload |
|---|---|---|
| `0x0040` | `OP_CAST_INTENT` | `u32 seq`, `u32 skill_id`, `u64 target_id`, `f32 aim[3]`, `u8 flags` (auto-target, queue), `u32 client_tick` |
| `0x0051` | `OP_CD_QUERY` | `u32 cd_id` (client UI reconciliation) |
| `0x0052` | `OP_COMPETITOR_SUBSCRIBE` | `u64 player_id`, `u8 want` (0=cancel,1=buff list,2=CD state) |
| `0x0053` | `OP_REVIVE_REQUEST` | `u8 revive_kind`, `u32 point_id` |
| `0x0054` | `OP_TARGET_INTENT` | `u64 target_id` (optional, for ground-targeted skills) |

### 7.2 S→C

| ID | Name | Payload |
|---|---|---|
| `0x0041` | `OP_CAST_RESULT` | `u32 seq`, `u8 accepted`, `u8 reason` (0 ok, 1 dead, 2 move_state, 3 range, 4 angle, 5 los, 6 resource, 7 cooldown, 8 silent, 9 control, 10 immune, 11 target_relation, 12 stealth, 13 busy) |
| `0x0042` | `OP_SKILL_PREPARE` | `u64 caster`, `u32 skill`, `u8 level`, `u32 cast_ms`, `u8 kind`, `u32 seq` |
| `0x0043` | `OP_SKILL_CAST` | `u64 caster`, `u32 skill`, `u8 level`, `u32 seq`, `u8 has_aim`, `f32 aim[3]` (when has_aim) |
| `0x0044` | `OP_SKILL_CHANNEL` | `u64 caster`, `u32 skill`, `u8 level`, `u16 tick_index`, `u16 tick_ms`, `u32 total_ms` |
| `0x0045` | `OP_SKILL_EFFECT` | `u64 caster`, `u32 skill`, `u8 result_count`, then per result: `u64 target`, `u8 hit_type`, `u8 crit`, `u8 result` (hit/dodge/parry/block/immune/absorb/resist), `u32 damage` (signed as i32 for heal), `u32 absorbed`, `u32 shield_left`, `u8 flags` |
| `0x0046` | `OP_SKILL_MOTION` | `u64 caster`, `u32 skill`, `u8 motion` (1 beat_back, 2 ray, 3 chain, 4 point_chain), `u16 node_count`, `u32 nodes[node_count]` (entity/target ids) |
| `0x0047` | `OP_SKILL_HOARD` | `u64 caster`, `u32 skill`, `u8 phase` (start/tick/cast/end), `u32 value`, `u32 max_value` |
| `0x0048` | `OP_SKILL_INTERRUPT` | `u64 caster`, `u32 skill`, `u8 reason` (beat_back, control, death, range_lost, caster_moved) |
| `0x0050` | `OP_COOLDOWN` | `u32 cd_id`, `u32 remaining_ms`, `u8 kind` (0 set, 1 reset_all, 2 pause, 3 resume, 4 accelerate, 5 overdraft, 6 charge), `u32 value` (accelerate %, overdraft stack) |
| `0x0055` | `OP_BUFF_SYNC` | `u8 mode` (0 full, 1 add, 2 remove, 3 stack), `u64 entity`, `u16 count`, then records: `u32 buff_id`, `u16 level`, `u32 remainder_ms`, `u32 caster`, `u32 stacks`, `u32 flags` |
| `0x0056` | `OP_CONTROL` | `u64 entity`, `u32 control_id`, `u8 kind` (apply/immune/break), `u8 school`, `u32 duration_ms` |
| `0x0057` | `OP_DEATH` | `u64 victim`, `u64 killer`, `u8 death_kind`, `u32 respawn_ms` |
| `0x0058` | `OP_REVIVE` | `u64 player`, `f32 pos[3]`, `u32 hp`, `u8 kind` (in-situ / point / npc) |
| `0x0059` | `OP_COMPETITOR_SYNC` | `u8 kind` (base / variable / buffs / cd / stats), `u64 player`, `u16 count`, count records (see §8) |

Rules (server-side, extend `REBORN_SERVER_SPEC.md` §5.3):
- cooldowns/charges/overdraft are server-authoritative; the client may render a
  local estimate for UI only and MUST correct on `OP_COOLDOWN`.
- `OP_SKILL_EFFECT` is the only event allowed to change HP; all other skill
  events are presentational.
- channels are ended by `OP_SKILL_INTERRUPT`; the server re-checks range/LOS on
  every channel tick.
- casts carry `seq` so `OP_CAST_RESULT` and the subsequent
  `OP_SKILL_PREPARE/CAST` can be correlated and deduped after reconnect.

## 8. Arena / battlefield competitor sync

Evidence is anchored in `JX3_MODE_JUEJING.md` §4, the symbol table
`reborn-netcode/proof/netcode/mode_juejing/battlefield_api_symbols.txt`, and the
handler disassembly summarised in §2 plus the table below.

**Per-competitor client state** (client tracks for the HUD/scoreboard):

| Piece | Handler / accessor | Wire shape |
|---|---|---|
| list / base info | `OnSyncBaseInfoFromBattlefieldCompetitorList`, `OnSyncArenaCompetitorList`, `OnSyncArenaNewCompetitor` | not extracted (list push) |
| variable info (HP/class/score) | `OnSyncVariableInfoFromBattlefieldCompetitorList` | not extracted |
| buffs | `OnSyncBattlefieldCompetitorBuffList` fn `0x180194390` | `wDataSize+0x17` (same as `OnSyncBuffList`) |
| cooldown state | `OnSyncBattlefieldCompetitorCDState` fn `0x180194490` | 4 × u32: `+0xB→entity+0xB920`, `+0xF→+0xB924`, `+0x13→+0x9F40`, `+0x17→+0x9F44` |
| cooldown state (arena) | `OnSyncArenaCompetitorCDState` fn `0x180192EE0` | 4 × u32, `+0xB`/`+0xF` swapped vs battlefield; fixed-size struct assert |
| statistics | `OnSyncBattlefieldStatistics`, `OnSyncArenaStatistics`, `KPlayerBattleStat::AddData/ReportBattleStat` | not extracted |
| positions | `GetBattlefieldPlayersPosInfo`, `BFSPosition`, `m_BattlefieldCompetitorInfoMap` (accessor asserts at logic `0x007E9D40`), `m_ArenaPlayerInfoVector`, `m_DungeonCompetitorInfoMap` | server-driven; client caches |
| refresh | `KBattlefieldCache::AddNextSyncTime` (timer); pull requests `0x164`/`0x161`/`0x162`; OB variant `DoSyncOBCompetitorSkillList` (0x71) | pull on demand |
| head-top overlay | `UpdateBFPlayerHeadTopParam`, `KREPRESENT_EVENT_GET_ALLBFPLAYER_KUNGFUID/_PARAM`, `HandleGetBFPlayerParam` | client-only |

**Implications for a PvP server** (evidence-scoped):
1. Competitor data is **pull-driven** (`DoSyncBattlefieldCompetitorsListRequest` /
   `...SkillCDStateRequest`), so the server can rate-limit the overlay without
   touching the main combat stream; `KBattlefieldCache::AddNextSyncTime` is the
   client-side refresh clamp.
2. `NeedSyncOB` (837/3510 `CoolDownList` rows) means **only a subset of cooldowns
   is exposable**; a server should gate competitor CD broadcasts by that flag.
3. `OnSyncBattlefieldCompetitorCDState` overwrites the same CD-clock fields the
   owner uses (`+0xB920/+0xB924`), so competitor state must never be applied to
   the local player's own manager — the client keys it by entity (`[+7]`).
4. Arena has richer competitor records than battlefield
   (`OnSyncArenaCompetitorTalentData`, `...EquipBoxInfo`, `...BaseData`,
   `KARENA_PLAYER_RECORD_INFO`, `KARENA_RECOMMEND_COMPETITION_NODE`), i.e. the
   arena server also exposes talent/gear snapshots for the scoreboard/observer.
5. Observer mode (`OB`) is a separate skill-list subscription (0x71) and a
   `KPlayer::bOBFlag`; a server must implement visibility rules + AOI even for
   spectators (see `REBORN_SERVER_SPEC.md` §6).

## 9. Open items / unknowns

| Item | Why static can't answer |
|---|---|
| S2C protocol IDs | dispatch via compiled `m_nProtocolSize` table; IDs not in strings |
| exact semantics of `OnSkillEffectResult` fields (`+9/+0xD`, flag bits at `+0x1B/+0x1C`) | requires capture or deeper call-graph RE |
| `KSKILL_RESULT` 9-byte field split (target/hit-type/crit/damage) | 9 B proven, fields not decoded |
| `DoCastProfessionSkill` arg order (skill/target/aim?) | builder writes are clear, call-site args not traced |
| cast validation logic | it lives on the server; client only carries result enums |
| arena/battlefield sync interval | `KBattlefieldCache::AddNextSyncTime` not disassembled |
| whether `DoSyncSubSkillPosition` really uses 0x71/0x14F/0x1A8 | catalog attribution is heuristic; shares builder with `DoSyncOBCompetitorSkillList` |
| `Usage` enum of `CoolDownList.tab` | 53 values, not decoded |
| `Engine_*`/KBase encryption of the game stream | code paths, not strings (unchanged from `JX3_PROTOCOL_SPEC.md` §5) |

## 10. Reproduce

```powershell
# combat string/symbol sweep (1032 hits)
python tools\pvp\combat_scan.py -o proof\pvp\netcode\combat_string_hits.tsv --symbols proof\pvp\netcode\combat_symbol_names.tsv
# proto type names (S2C_*/C2S_* literals)
python tools\pvp\protocol_names.py
# member-name runs around combat anchors
python tools\pvp\member_runs.py "<bin64>\JX3LogicEditOperationX64.dll" dwCasterID nSkillID SkillResult KSKILL_RESULT CDState -o proof\pvp\netcode\struct_member_runs_logic.txt
# disassemble builder/handler functions (needs pefile+capstone, venv in reborn worktree)
& "<venv>\python.exe" tools\pvp\dump_fn_disasm.py "<bin64>\JX3LogicEditOperationX64.dll" --names proof\pvp\netcode\names\combat_logic_names.txt --out-dir proof\pvp\netcode\disasm
& "<venv>\python.exe" tools\pvp\dump_fn_disasm.py "<bin64>\JX3LogicEditOperationX64.dll" --names proof\pvp\netcode\names\combat_deep_names.txt --out-dir proof\pvp\netcode\disasm_deep2 --limit 400
# cooldown table stats
python tools\pvp\cooldown_table.py
```

### Evidence inventory written this session

| File | Contents |
|---|---|
| `proof/pvp/combat_netcode.md` | this report |
| `proof/pvp/combat_opcodes.tsv` | 41-row combat opcode/handler catalog |
| `proof/pvp/netcode/combat_string_hits.tsv` | 1032 combat string hits across net dumps |
| `proof/pvp/netcode/combat_symbol_names.tsv` | 281 symbol rows |
| `proof/pvp/netcode/protocol_type_names.tsv` | 104 protocol struct-name literals |
| `proof/pvp/netcode/combat_asserts_raw.txt` | 236 curated assert/protocol/struct strings |
| `proof/pvp/netcode/struct_member_runs_logic.txt` | member-name runs (32 KB) |
| `proof/pvp/netcode/cool_down_list_analysis.txt` | CoolDownList.tab statistics + samples |
| `proof/pvp/netcode/disasm/*.txt` | 51 builder/handler function disassemblies |
| `proof/pvp/netcode/disasm_deep2/*.txt` | 22 deep handler disassemblies (no early ret stop) |
| `tools/pvp/*.py` | extraction/disasm helpers (read-only research) |

### Audit note

All game-install and `reborn-netcode` inputs were read-only; every artifact
written by this session lives under `C:\Users\Zhibin Ren\Desktop\reborn-pvp`
(no commits made). No live client was run.
