# JX3 drops research — session record (audited)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Scope:** everything learned this session about the BR mode's loot drops — containers, drop
tables, spawn/pickup protocol, spawn placement — with an explicit accuracy audit.

**Companion detail docs:** `JX3_MODE_LOOT_SYSTEM.md` (tables/chain),
`JX3_LOOT_PROTOCOL_LAYOUTS.md` (wire formats), `JX3_MODE_SPAWN_RULES_SEARCH.md` (hunt log),
`JX3_MODE_LOAD_FLOW.md` §8 (post-enter flow), `JX3_MODE_GAP_REGISTER.md` (status).

---

## 1. Verified findings (evidence-backed, nothing assumed)

| # | Finding | Evidence | Confidence |
|---|---|---|---|
| D1 | Loot containers are doodad templates in `settings\DoodadTemplate.tab`, sets `MapName=沙漠风暴` (255 rows) and `沙漠风暴_寻宝模式` (168) | extracted table (`proof/netcode/mode_juejing/mode_doodads.txt`) | HIGH |
| D2 | Container behavior fields exist and are populated: `Kind`, `CanOperateEach`, `OpenPrepareFrame`, `OverLootFrame`, `RemoveDelay`, `ReviveDelay`, `BarText`, `Drop1..10`+`Count1..10`, `MoneyMin/Max/DropRate`, `Script` | same table (51 columns) | HIGH |
| D3 | 97 distinct drop-table names are referenced by the containers (`沙漠风暴/equipment1.tab`, `weapon1..3`, `chengwu/chengzhuang`, `hide`, `horse1..3`, `zuzhoubox_*`, …) with per-map variants (`_sea` 18 refs, `_bl` 15, `_lw` 7) | `mode_doodad_stats.txt` | HIGH |
| D4 | The **runtime wire formats** for spawn + loot are fully decoded: `OnSyncNewDoodad` (template id `+7`, packed X/Z/Y in `+0x39`, flags, NPC-template branch), `OnSyncDoodadState`, `OnSyncLootList` (looter array + variable item records `u8 type, u32 index, u8 count, u8 flag, u8 extraLen, extra[]`) | `proof/netcode/disasm/*`, `JX3_LOOT_PROTOCOL_LAYOUTS.md` | HIGH |
| D5 | Take requests: `DoApplyLootList` = protocol **0x4D**, `DoLootMoney` = protocol **0x51**; both 15-byte frames (11-byte base header + u32 param) | `0x18019DA0F`, `0x180175020` | HIGH |
| D6 | Frame base header = **11 bytes** (`id u16, flags u8, seq u16, ack u16, field7 u32`); `u32 param @+0xB` optional (15-byte packets) | proto 2 @ `0x180170E60` (11 B), proto 0x164 @ `0x1801A9503` (11 B), ping/0x4D/0x51 (15 B) | HIGH |
| D7 | Decoder implemented and tested for D4/D5 shapes; synthetic end-to-end run decodes spawns, heatmap, rolled loot, takes | `tools/netcode/loot/` (selftest 8/8) | HIGH |
| D8 | Exported rules **shape** for spawn placement: `KLogicExporter::ExchangeAnchorPointSetting` (`AnchorID, X, Y, Z, Dir, RollDir, Pitch`), `ExchangeDoodadReliveSetting` (`IsRandom, ReviveID, Min, Max`), counters `NumDoodadRefreshSet/CustomObject/Doodad` | strings @ `0x8F5E5C`–`0x8F68A8` in `JX3LogicEditOperationX64.dll` | HIGH (strings), MED (that these are the complete rule set) |
| D9 | Placement values live in per-map logical files: `map\<map>\AnchorPoint\AnchorPointList.tab`, `DoodadRelive\DoodadReviveList.tab`, `NpcRelive\MapReviveList.tab`, `CustomObject.tab`, `maps\<map>\<map>.land`/`.pland` | exporter path format strings @ `0x8F5F5E`–`0x8F6008` | HIGH (paths exist as strings), MED (runtime client reads them — no read found) |
| D10 | Those files are absent from every local store probed | `JX3_MODE_SPAWN_RULES_SEARCH.md` §2 (PakV4, both launcher caches, editor tree, filesystem, AppData) | HIGH (negative result, listed probes) |
| D11 | Mode entry/state flow: queue confirm 0x116, ready proto 2, enter scene proto 3, role-data chunks proto 4 + `OnSyncRoleDataOver`, competitor list pull 0x164, sectioned/periodic sync | `JX3_MODE_LOAD_FLOW.md` §8 + disasm files | HIGH (protocol facts), MED (semantics annotations) |
| D12 | Item pool: 133 `scripts/skill/沙漠风暴/道具_*.lua` item scripts + 163 `绝境战场/绝境_*.lua` skill scripts (source text), catalogued with effect summaries | `mode_item_scripts.txt`, `mode_item_catalog.txt` | HIGH (names/counts), MED (effects not individually verified) |
| D13 | `scripts\Map\沙漠风暴\doodad\*` (25 hook names from `DoodadTemplate`) are **not extractable** from the local pak; `scripts\Map\龙门寻宝\include\CheckTreasureBattleFieldMap.lua` IS extractable (Lua 5.1 bytecode) | `pak_extract_log7.txt`, `CheckTreasureBattleFieldMap.dump.txt` | HIGH |

## 2. Inferences explicitly NOT presented as fact

| Statement | Status | What would confirm it |
|---|---|---|
| map ID → tier mapping (296/297, 410, 512, 532, 645, 676/677, 709/715 → 1..5) | INFERRED from constant clustering in Lua bytecode | full decompile of `CheckTreasureBattleFieldMap.lua` |
| "spawner rolls which template lands on each anchor" | INFERRED from system shape | server script or capture |
| "terrain region" meaning of X/Z bits 11..17 | INFERRED (field proven, label not) | engine/scene docs or capture correlation |
| remaining `OnSyncNewDoodad` field roles (`+0x18/+0x1C/+0x29/+0x31`, `+0x2D`, `+0x35`, `+0x43`) | UNKNOWN (offsets proven, roles deliberately not claimed) | more handler context |
| frame counts → seconds (`RemoveDelay=2880`) | NOT CONVERTED (client frame rate unverified) | tick-rate evidence |
| `DoLootMoney` param meaning | UNKNOWN | UI flow or capture |
| "no prepare message" | SCOPE-LIMITED to two binaries scanned | scan remaining modules |
| "drop tables are server-side" | LIKELY (absent locally) | server bundle availability |
| community sources for spawn data | searched 4 queries, nothing coordinate-level | targeted community docs (images/video) |

## 3. Dead ends and re-open criteria

- **Local dead end:** spawn anchors / refresh rules / drop-table rows are not present in any
  local store. Do not re-probe. Full probe list: `JX3_MODE_SPAWN_RULES_SEARCH.md` §2.
- **Re-open only with:** (a) a server map bundle / private-server package → we know the file
  schema and can write parsers (needs one sample + loader disasm); or (b) a runtime capture →
  `tools/netcode/loot/capture.py` already decodes positions and rolled contents.

## 4. Artifacts produced this session

| Kind | Path |
|---|---|
| Docs | `JX3_MODE_LOOT_SYSTEM.md`, `JX3_LOOT_PROTOCOL_LAYOUTS.md`, `JX3_MODE_SPAWN_RULES_SEARCH.md`, `JX3_MODE_LOAD_FLOW.md` §8, this file |
| Tools | `tools/netcode/loot/capture.py` (+ README), `tools/netcode/mine_item_scripts.py`, `tools/netcode/extract_pak_paths.py`, `tools/netcode/lua51_dump.py`, `tools/netcode/search_tree.py`, `tools/netcode/gbk_grep.py` |
| Evidence | `proof/netcode/disasm/{OnSyncNewDoodad,OnSyncDoodadState,OnSyncLootList,OnOpenLootList,DoApplyLootList,DoLootMoney,apply_loot_build,OnSyncRoleDataOver,OnSyncRoleDataSectionCheck,DoSyncBattlefieldCompetitorsListRequest,OnSyncBFRoleData}.txt`, `proof/netcode/mode_juejing/*`, `proof/netcode/loot_demo*` |
| Key commits | `3b23719`, `1dcf26a`, `ed5b5f9`, `2d9ded7`, `7930978`, `a18e06d`, `57d644d`, `383b8fb`, `81c8583`, `a13dab3`, `1ec28e5`, `2f1cf14`, `24dbb5f`, `31d01f3`, `19f2034`, `fb26bca`, `7191210` |

## 5. Audit note

This file was written after a full re-read of the drops docs (2026-09-21). Corrections made in
the same pass: 11-byte base header (was "15-byte prefix"), 423-vs-435 row counts clarified,
frame→seconds conversions removed/marked unverified, unverified protocol names
(`DoPickPrepare`/`PickUpItem`) scoped out, "server-only" softened to "not present locally,
likely server-side", and per-row confidence added where an inference could be mistaken for
evidence.
