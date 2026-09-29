# JX3 绝境战场 — gap register (what's known / missing / how to get it)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Legend:**
- **DONE** — recovered, in docs/evidence
- **STATIC** — recoverable with our existing tooling (xref disasm, PakV4 extraction, Lua bytecode dump)
- **RUNTIME** — only exists in live client/server traffic; needs a capture bridge
- **SERVER** — lives only on the server, not observable even with a client bridge (must infer empirically from results)

---

## 1. Content & data

| Item | Status | Evidence / notes |
|---|---|---|
| Maps (5 BR maps) | DONE | `MapList.tab` |
| map→tier/config mapping | PARTIAL / MED | inferred from constant clustering in `CheckTreasureBattleFieldMap.lua` bytecode (`proof/netcode/mode_juejing/CheckTreasureBattleFieldMap.dump.txt`); not a decoded table |
| Loot template set (423 containers, timings, drop refs) | DONE | `DoodadTemplate.tab` (MapName=沙漠风暴) |
| Drop-table **names** (97) | DONE | `mode_doodad_stats.txt` |
| Drop-table **contents/rates** | SERVER | not in PakV4 / logical files / launcher cache |
| Item pool (133 道具 scripts + consumables/specials) | DONE | `mode_item_scripts.txt`, `mode_item_catalog.txt` |
| Skill kit (182 绝境 skills), cooldowns | DONE | `skills.tab`, `CoolDownList.tab` |
| SkillMove phys table | DONE | `SkillMove.tab` (other agent) |
| Ability ranges (836 scripts) | DONE | other agent's commit `01c406f` |
| Quest/Doodad/NPC tables | PARTIAL | extracted; mode-relevant rows not fully catalogued |

## 2. Loot chain (details in `JX3_MODE_LOOT_SYSTEM.md` §8)

| Step | Status |
|---|---|
| B1 `OnSyncNewDoodad` / `OnSyncDoodadState` layouts | **DONE** (`JX3_LOOT_PROTOCOL_LAYOUTS.md`) |
| E1 `OnSyncLootList` rolled contents layout | **DONE** |
| C1 `DoApplyLootList` 0x4D / `DoLootMoney` 0x51 | **DONE** |
| Decoder + heatmap/histogram tooling | **DONE** (`tools/netcode/loot/`) |
| A1 spawn anchor positions | RUNTIME (decoder ready) |
| A3 spawner weights / per-phase bias | SERVER |
| D2 roll algorithm inside drop tables | SERVER |
| G2 per-anchor respawn/refresh | RUNTIME (doodad respawn observed) + SERVER rules |
| F2 inventory grant messages | STATIC (`OnAddItemNotify`/`OnSyncItemData`) |

## 3. Mode session / HUD (all statically recoverable next)

| Handler (string exists) | Missing today |
|---|---|
| `OnCreateBattlefieldRoomRespond` | room create payload (room id, map, rules) |
| `OnForceStartBattleFieldChaosFightRespond` | force-start payload |
| `OnSetBattleFieldSide` | side/camp assignment payload |
| `DoApplyBFPlayerTeamGroupIDInfo` / `ApplyBFPlayerTeamGroupID` | team grouping payload |
| `OnSyncBaseInfoFromBattlefieldCompetitorList` | competitor base record (name/class/side/team) |
| `OnSyncVariableInfoFromBattlefieldCompetitorList` | per-competitor variable record |
| `OnSyncBattlefieldCompetitorCDState` | competitor cooldown sync |
| `OnSyncBattlefieldCompetitorBuffList` | competitor buff list |
| `OnSyncBattlefieldStatistics` (live variant) | scoreboard stats |
| `DoSyncBattlefieldCompetitorsListRequest` | request payload |
| `GetBattleFieldObjective` / `GetBattleFieldPQInfo` | client-side objective/queue data model |
| `BattleFieldIncomePunish` | economy rule values |

**Recovered since this register was written** (see `JX3_MODE_LOAD_FLOW.md` §8):
`DoSyncBattlefieldCompetitorsListRequest` = protocol **0x164** (pull on staleness);
`OnSyncBFRoleData` fields (`+0xF` player id, `+0x13`, array `+0x17`, count `+0x6F`);
sectioned initial sync = protocol **4** acks + `OnSyncRoleDataOver` terminator.
Still missing from this table: competitor base/variable/CD/buff/stat record layouts,
room create/force-start/side payloads.

## 4. Round / phase logic

| Item | Status |
|---|---|
| Countdown UI asset (`UI_黑山绝境_倒计时通用底框.pss`) | DONE (asset only) |
| Phase durations, zone/shrink schedule, scoring, revive rules, win condition | SERVER (no client constants found; runtime observable only as UI/buffs) |
| `ReviveInSitu=0`, `RevieCycle=160`, `DynamicReviveMinTime` | DONE (flags only) |
| Per-map revive table (`MapReviveList.tab`, `DoodadReviveList.tab`) | SERVER/absent locally |

## 5. Map logical data

| Item | Status |
|---|---|
| `CustomObject.tab`, `AnchorPointList.tab`, `DoodadReviveList.tab`, `MapReviveList.tab`, `.land`/`.pland`, `logicalTrigger.json` | SERVER/absent (PakV4 + launcher cache probed, 0 hits) |
| Map render bundles (landscape/env/camera) | DONE (map spike renders 3 BR maps) |

## 6. Netcode framework

| Item | Status |
|---|---|
| Frame layout, reliability (serial/ack/retransmit), handshake/resume, ping/timeout | DONE (`JX3_PROTOCOL_SPEC.md`) |
| Reference server+client implementing the model | DONE (`tools/netcode/reference/`, 10/10 smoke) |
| Fixed-size table `m_nProtocolSize` + S2C opcode IDs | **DONE (2026-09-24)**: KPlayerClient registration function parsed (handler base `this+0x16460`, size base `this+0x17F28`, anchor `0x22A`); 810 handlers with IDs+sizes in `proof/netcode/protocol_table_s2c_annotated.tsv` |
| ~900 protocol payload layouts | STATIC (per-handler disasm, only ~15 recovered so far) |
| Crypto/compression order on live stream | UNKNOWN (runtime/RE) |
| Self skill/cast (`OnSkillPrepare/Cast/EffectResult`), move sync (`OnSyncMoveState/MoveCtrl/MoveParam`), buffs | STATIC — same method as loot |

## 7. UI / text

| Item | Status |
|---|---|
| Mode HUD labels, item tooltips, queue UI text | STATIC (PakV4 `ui/...` extraction — not yet enumerated) |
| Loading backgrounds (`ui\Loading\Loading.ini`, 18 entries) | DONE — window RE + 18 class-art backgrounds in `JX3_MODE_UI_FLOW.md` §4; per-map `[loading]` art extracted (`proof/netcode/mode_ui/loading/`) but has **no reader in the PC client binaries** (scan `proof/netcode/mode_ui/consumer_scan_bin64.txt`) |

---

## 8. Recommended next static wins (ordered)

1. **Competitor/room/side sync layouts** — 10 handlers with known strings; same xref method that
   produced `JX3_LOOT_PROTOCOL_LAYOUTS.md`. Gives the full mode HUD/scoreboard data model.
2. **Self combat sync layouts** — `OnSkillPrepare/Cast/EffectResult`, `OnSyncMoveState/MoveCtrl/MoveParam`,
   `OnSyncBuffList`; feeds the reborn server spec directly.
3. **Protocol size/dispatch table** — unlocks S2C opcode IDs, making captures decodable without `--detect`
   and giving the complete opcode map.
4. **PakV4 `ui/` enumeration** — extract mode UI data/text (needs path discovery; launcher cache/reader can resolve by name).
5. **More shipped Lua bytecode** (`scripts\Map\<map>\include\*.lua`) — round/spawn config that ships client-side.

## 9. Runtime-only (needs the bridge)

- spawn positions (A1) + respawn observations (G2)
- rolled loot contents per container (empirical drop tables)
- zone/phase timing observations, actual player counts per match
- S2C opcode IDs if the dispatch table can't be cracked (observed directly)

> **Local dead end (2026-09-21):** the spawn/refresh/drop-table values were searched in every
> local store (client PakV4, both launcher caches, editor tree, filesystem, AppData) — confirmed
> absent. Do not re-probe. See `JX3_MODE_SPAWN_RULES_SEARCH.md` §0. Re-open only with a server
> map bundle (then write the file parser from the known schema) or a capture (decoder already
> ready in `tools/netcode/loot/`).

## 10. Server-only (never in any local file)

- spawner distribution/weights (A3)
- drop-table rows/rates (D2)
- mode scripts (server side), matchmaking, scoring rules

Linked docs: `JX3_MODE_LOOT_SYSTEM.md`, `JX3_LOOT_PROTOCOL_LAYOUTS.md`,
`JX3_MODE_LOAD_FLOW.md`, `JX3_MODE_JUEJING_LOGIC.md`, `JX3_PROTOCOL_SPEC.md`.

---

## 11. Update 2026-09-24 — UI corpus + lifecycle consolidation

- **Mode UI is present and partly readable**: 144 PakV4 `ui/Config/Default/*.lua` bytecode
  files + JX addon packs extracted (`proof/minimap/ui/`, `proof/minimap/addons/JX/`);
  17 `*.decompiled.lua` files already exist (9 base-UI panels incl. `NewBattleFieldQueue`,
  `MapQueue`, `DynamicBattleRoyale` (mode skill bar), `BattleField/BattleFieldMap`,
  `Minimap`, `MiddleMap`, `WorldMap`; 8 addon builds incl. `JX_LootPlus`).
- **Toolchain caveat**: `tools/netcode/bin/unluac.jar` is a 9-byte `Not Found` placeholder;
  the committed `*.decompiled.lua` came from a locally built unluac
  (`MAP_MINIMAP_RESEARCH.md`). Restore it before decompiling more panels;
  `lua51_dump.py` constants remain the fallback.
- **New docs**: `JX3_MODE_MATCH_LIFECYCLE.md` (stages 0–8.5, per-map chapters, protocol
  appendix, unknowns register, full-pass plan) and `JX3_MODE_EDGE_SYSTEMS.md`
  (death/observer/AFK/reconnect/guild league/rooms/rewards). Screen-level flow is a separate
  companion already on main: `JX3_MODE_UI_FLOW.md` + `proof/netcode/mode_ui/**`
  (loading art/progress xrefs; merged in `f82f15a`).
- **New symbol leads** for previously-unlisted systems: observer (`Btn_Observer`,
  `UpdateObserverButton`, `OnSetObserverParam`), ghost/death (`ApplyGenModelGhost`,
  灵魂出窍02 animation), AFK report (C2S **0x1C9**), revive request (C2S **0xB9**),
  `MaxSwitchMapMoveDistance`, glider camera/carrier wiring, storm art
  (`Image/MiddleMap/StormLine/*.DDS`).
- **Top open proof item**: the storm **data path** is now recovered (objective/PQ cache
  writers `0x180193FD0`/`0x180194020`, stats builder `0x1801940A0`; lifecycle Stage 7/8),
  but the **schedule values and index→meaning labels remain server/capture-only** — no
  official local file carries shrink timing (verified: 0 hits for 风暴/缩圈/沙暴/安全区 in
  Buff/TopBuff/Activity/CoolDown; the CheckTreasure ids 22495+ are skills).
- UI corpus was dictionary-extracted (window names) → PakV4 `ui/` enumeration (item 4
  above) is still recommended before claiming any UI stage complete.
- **UI inventory (2026-09-24)**: `JX3_MODE_UI_INVENTORY.md` maps every stage to its
  official windows/labels (queue panel sections, MapQueue auto-enter, loading window,
  DynamicBattleRoyale, BattleFieldMap storm lines, PVPShowPanel stats, PVPShowFinal
  settlement). Open renderers: mode storm HUD (`STR_TIMEDESERT`/`STR_LEFTPEELE`), ready
  prompt (`STR_PVP_Ready`), observer/death overlays — not in the extracted PakV4 corpus or
  `JX3UIX64.dll` strings; hunt plan in the doc §9.
- **Capture decoders ready (2026-09-24)**: `tools/netcode/mode/capture.py` decodes the
  recovered mode opcodes (switch map placement, objective increments `0x11A`, phase clock
  `0x11B`, statistics `0x119`, side `0xAA`, stat flag `0x330`, BR role data `0x245`);
  self-test 7/7, synthetic end-to-end decode verified. Loot stays in
  `tools/netcode/loot/capture.py`. A single live capture labels the objective-array
  indices and yields the actual zone/phase schedule values.
- **Settlement panel located (2026-09-24)**: `PVPShowFinal.lua` + `PVPShowFinalL/R.ini`
  extracted from PakV4 (`proof/netcode/ui/`); registers `BATTLE_FIELD_SYNC_STATISTICS`,
  consumes `GetBattleFieldStatistics` + `PQ_STATISTICS_INDEX`; settlement labels live in
  `string_PVPAcount.txt`.
- **S2C opcode IDs recovered (2026-09-24)**: the KPlayerClient protocol registration
  function (`0x180163540`–`0x180168280`) was parsed with
  `tools/netcode/parse_protocol_registration.py`; calibration anchor
  `s2c_sync_arena_competitior_cd_state = 0x22A`. Mode-relevant IDs: `0x11A` objective
  array update (16 B), `0x11B` phase clock (23 B), `0x119` statistics (264 B), competitor
  family `0x25F/0x260/0x261/0x262`, side `0xAA`, BR role data `0x245`, stat flag `0x330`,
  room `0x323/0x324`, plus loot/movement (`0x0C/0x10/0x82/0x19`). Full table:
  `proof/netcode/protocol_table_s2c_annotated.tsv`.
- **Source audit (2026-09-24)**: match-start/storm timings previously read from a
  team-monitor addon userdata (`interface\MY#DATA\...\userdata\team_mon\remote\*.jx3dat`)
  were **excluded as evidence** (player-side config, not official) and the extracted proof
  file was deleted. Official replacement: the panel clock comes from
  `GetBattleFieldPQInfo` (engine getter `0x180356a90`, 4 ints at client cache
  `+0x1b480..0x1b490`, 4th = absolute end time; see
  `proof/netcode/disasm/LuaGetBattleFieldPQInfo.txt`), and phase data is the 16-int
  `LuaGetBattleFieldObjective` array (`+0x1b498..0x1b4d8`). Evidence rule: client
  binaries, PakV4-shipped assets, and protocol only; `interface\` is out of scope.
