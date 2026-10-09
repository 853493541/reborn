# Ability follow-up plan — what is still imprecise (and the next probe for each)

**Status:** active. Area: pvp / netcode / client. Created: 2026-10-09.
**Purpose:** an honest inventory of every ability-system item that is NOT yet precise, with its
current confidence, the exact unknown, and the concrete next probe/tool to close it.
Complements `ABILITY_CORRECTNESS_PLAN.md` (FWD1–FWD8) and `ENGINE_RE_BREAKTHROUGH_PLAN.md` (W1–W5).

Confidence: **HIGH** = verified from data/code; **MED** = partial evidence; **LOW** = assumption.

## A. Engine-RE items (root cause located, exact detail open)

| Item | Precise now (HIGH/MED) | Still imprecise | Next probe |
|---|---|---|---|
| W1 per-tani AV (39 abilities) | fault = `memcpy` size `0xFFFFFFFA` via `jemallocX64.dll` from engine anim code (`KG3DEngineDX11EX64.dll+0xE2E842` region); dump captured | exact engine caller fn; the corrupt map key/binding (which anim/bone id) | debugger (x64dbg/`cdb`) attach, or forced full-memory dump → `.pdata` unwind; then disasm the caller |
| W2 be-hit animation **DONE (2026-10-09)** | kind map (`BeHittedByNpc` @ `0x1804cfb50`: kind 1→`[rsi+0xa4]`, 2→`[rsi+0x94]`, 6→`[rsi+0x8c]`) + default NPC rule (`BeHittedByPlayer` @ `0x1804d0220`: literal `bat01.ani`; sibling `0x1804d8700` = `data/source/npc_source/%s/动作/%s_%s`) | — | implemented: target's `_bat01` sibling of its idle anim, played + restored; `npc_animation.txt` is runtime-generated (not shipped) |
| W3 hit sound | ids are **authored, not hashed** (adjacent index events get consecutive ids; FLWS name→FNV ≠ id); client `ProcessSkillEffectSound` @`0x18059fdc0` hands `pcHitTargetSoundModel->szEvent` (name) to the Wwise manager; `sound_probe` already hooks the `PostEvent(const char*)` overload | the authored hit-target names (`TianCe_Body_L01`, `l_LongYaBeiJi`, `f2sgb11BangFaGongJi07`) are absent from all 230 indexed banks, from `resource_sfx`/`custom_sfx`/`WwiseSound`/**`Sound.txt`**, and from the raw `.bnk`/bank-**`.txt`** files (game client `jx3ac` + the 93 cache banks) | **blocked:** the mapping needs a bank set we cannot find locally, or a runtime hook of the real client (not allowed). Re-open only if such a bank/table surfaces |
| W4 landing `.Sfx` | engine `.Sfx` spawn faults (`rc=7`, `0xC0000005`); `.pss` works | the correct engine SFX-factory owner chain | disasm the engine tag-spawn caller (shim comment cites `0x76E51A`) |
| W5 be-hit shake (optional) | `behit_shake.txt` has only 3 rows: RepresentID 0/1/2 = none / 2osc-10u-100ms / 5osc-50u-100ms | the skill→shake-type (0/1/2) selector (no shake column in `skill_caster_*`/`skills.tab`) | low value given the trivial data; keep optional |

## B. Ability mechanics still imprecise

- **Multi-hit / channel hit schedule. CHANNEL DONE (2026-10-09).** Channel skills now apply the
  mechanic every `channelInterval` during the channel (27906 -> 10 applies over 4 s, was 1).
  Still open: non-channel multi-hit skills' authored hit count/timing. Next: parse the skill
  script hit count + `skill_chain.txt`.
- **Missiles / projectiles (scoped feature).** `skill_missile.txt`, `missile.txt`,
  `skill_bullet.krl.txt` exist. Per-skill projectile assignment `skill_caster_<body>` `MissileID`
  (col 3) = 2 roster skills (28046→231, 65059→25), `ImmediatelyMissileID` = 0. But the bullet
  **visual** is broader: joining roster→`skills.tab` col-57 `ScriptFile` (keyed by col-1 SkillID) →
  script shows **17 roster skills** set `skill.nSkillBulletType = 1` (all the same type). The type→
  model map is `skill_bullet.krl.txt` (groups like `CYShield`/`PureShadow`, each `{ CommonFlyAni,
  CommonModel, SingleTargetAni, OutTime }`). `SKILL_BULLET_TYPE` is registered as a **Lua global in
  `JX3ClientX64.exe` @`0x140085714`** (a name/value-pair registration block; member names live in the
  enum string table around `0x1407F05C8`), and `JX3RepresentX64.dll` has a `KSKILL_BULLET_TYPE`
  enum (assert `!"KSKILL_BULLET_TYPE INVALID"` @`0xCA8340`). We resolve projectiles instantly at the
  point. Next: decode the `SKILL_BULLET_TYPE` member→value table (all 17 roster skills use value 1,
  so one group) → its `CommonModel`/`CommonFlyAni`, then spawn at the caster, travel, trigger on
  arrival.
- **Buff durations. DONE (2026-10-09).** Client truth: `KScriptFuncList::LuaGetBuffTime`
  @`0x1401c0730` (`JX3ClientX64.exe`) returns a buff's `(Count×Interval, Count, Interval)`; `Count`
  = Buff.tab col 13, `Interval` col 14; frame base 16 fps (cast frames confirm it). New
  `ability_picker/tools/build_buff_times.py` → `ability_picker/data/buff_times_f1.tsv` (123 buffs /
  346 rows) for the roster's applied buffs (apply_f1 + mechanics `CALL_BUFF`); the client expires
  self/target buffs at `Count×Interval/16` s (0 = permanent). Verified driven: 27892 → 20354
  +3.004 s / 20359 +3.128 s; 65028 → 51402 +5.007 s; 65149 → 28998 (target) +9.992 s. Proof
  `proof/netcode/skillv6_buff_duration_20261009.txt`.
  Still open: CC buffs carry a separate control duration in `Intensity` (col 10) that disagrees
  with `Count×Interval` (51402 Silence: 96→6 s vs 80→5 s); the client's CC remains the uniform
  functionType 2 s. The `AddBuff` 5th script arg is an override of unclear unit (authored comments
  are contradictory) — not applied.
- **Target CC duration — server-authoritative (client has no source).** `skill_caster_<body>.txt`
  `LockControlTime` (col 4) is empty/0 for every roster CC skill (65116/65149/65153/65156/65159);
  it is the *caster's* control lock, not the target's CC time. `skills.tab` cols 63/64 are
  `SelfMoveStateMask`/`TargetMoveStateMask` (bitmasks, no time); Buff.tab has `MoveStateMask` but
  no CC-time column; only 65156 applies a CC-typed buff (682 Stun, Count×Interval=1 s). So the
  client's uniform 2 s CC is a stand-in for the absent server (matches `ABILITY_MECHANICS_PLAN.md`
  P7: CC application is server-side). No client-side duration to adopt; keep the documented 2 s
  deviation until a server/replay supplies the value.
- **Per-skill cooldown / GCD values. DONE (opt-in, 2026-10-09).** `RC_AUTHORED_CD=1` applies the
  authored per-skill cooldown (`cooldowns_f1` via the mechanics `normalCd` row) + authored GCD
  (`gcdRow`); default stays the locked uniform 3 s / 1.19 s. Verified: 65029 -> gcdMs=1500,
  cdMs=10000 (authored) vs 1190/3000 (default).
- **Skill marks/tags/chain/shadow — characterized (2026-10-09): not combat mechanics.**
  `skill_tag.txt` = SkillID→AnimationID anim binding (already handled via `skill_caster_*`);
  `skill_chain.txt` = chain-shaped **visual** effect (链状特效 beam between two chars, `.Sfx`) — not a
  combo chain, and blocked by the engine `.Sfx` path (W4); `skill_shadow.txt` is represent/shadow
  config. No combat wiring needed.
- **Hit-stiff — nothing to apply (2026-10-09).** `skills.tab` cols 112–116 (`HitStiffDelayFrame`,
  `HitStiffSkillMoveID`, `HitStiffVelocityXY`, `HitStiffAccelerateXY`) are **empty/0 for every one
  of the 154 roster skills**, so no target hit-stiff move is authored to apply.
- **Damage scaling (blocked).** `nWeaponDamagePercent` + adaptive coefficient not applied — no
  player weapon/attack-power stats. Unknown: whether a base-attribute table exists locally. Next:
  search the client settings for a character base-attribute / attack-power table.
- **AoE shape. DONE (2026-10-09) — radius only; no sector/rect in client data.** The scripts carry
  only `nMinRadius/nMaxRadius/nAreaRadius/nHeight` (no angle/width/length/shape tokens), and
  `skills.tab` has only `LongRange`/`RangePutOpti`/`IgnoreRangeBlock` — so the AoE is circular
  (annulus via `nMinRadius`) + height, **not** sector/rect. Fixed a real bug: a skill with
  `nAreaRadius` unset hit *every* entity; `build_mechanics.py` now falls back
  `areaRadius = nMaxRadius` when unset, and the client only hits within a positive radius
  (30262: 3→1 target; 65149 regression 3). `nMinRadius>0` in only 1 roster skill (65120, not
  applied). Proof `proof/netcode/skillv6_aoe_shape_20261009.txt`.

## C. Scripts

- **46 `EXECUTE_SCRIPT`-only abilities (LOW).** Logic not statically extractable → the Lua 5.1 VM
  residual. Next: build the VM (`tools/pvp/lua51_disasm.py` already parses bytecode) + engine-API shim.
- **32 `-` abilities (HIGH).** Confirmed no cast-time function; mostly template passives.
- **Complex scripts (loops/local helpers) (LOW).** The static parser may miss them. Next: the VM.

## D. Tooling gaps

- **No debugger** (`x64dbg`/`cdb`) in the environment → W1 blocked.
- **`minidump_exc.py` has no `.pdata` unwind**, and the WER dumps omit caller code pages.
- **No `.bnk` (Wwise bank) reader** → W3 blocked.
- **No missile/particle model loader** beyond `AddDummyModel` (`.pss` only).

## E. Verification / review

- 115 castables individually verified; **"correct" is ultimately the user's judgement** via the
  panel review (F1 已完成 / F2 测试中 / F3 需要修复). The fastest closing loop is the user's
  `需要修复` tags → I fix each. Currently no unaddressed `需要修复` tags beyond the defaults.

## Suggested order (highest leverage first)

1. **W1** (39 abilities) — needs a debugger; highest count.
2. **B: per-skill cooldown/GCD + multi-hit schedule** — code-only, no engine RE; affects many skills.
3. **W3 hit sound** — a bounded `.bnk` reader.
4. **B: missiles** — model projectile skills.
5. **W4** — deeper engine RE (`.Sfx` factory owner chain); W2 done.
6. **C: Lua VM** — for the 46 script-only abilities.
