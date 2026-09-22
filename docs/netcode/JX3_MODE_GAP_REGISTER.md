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
| Maps (5 BR maps), map→tier table | DONE | `MapList.tab`, `CheckTreasureBattleFieldMap.lua` dump |
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
| Fixed-size table `m_nProtocolSize` + S2C opcode IDs | STATIC (hard): find dispatch table / size table |
| ~900 protocol payload layouts | STATIC (per-handler disasm, only ~15 recovered so far) |
| Crypto/compression order on live stream | UNKNOWN (runtime/RE) |
| Self skill/cast (`OnSkillPrepare/Cast/EffectResult`), move sync (`OnSyncMoveState/MoveCtrl/MoveParam`), buffs | STATIC — same method as loot |

## 7. UI / text

| Item | Status |
|---|---|
| Mode HUD labels, item tooltips, queue UI text | STATIC (PakV4 `ui/...` extraction — not yet enumerated) |
| Loading backgrounds (`ui\Loading\Loading.ini`, 18 entries) | DONE |

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
