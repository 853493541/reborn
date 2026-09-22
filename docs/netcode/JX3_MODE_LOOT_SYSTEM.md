# 绝境战场 / 沙漠风暴 — ground loot system (PUBG-style, per user description)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Sources:** `settings\DoodadTemplate.tab` (11,011 rows), `settings\DoodadClass.tab`,
`settings\MapList.tab`, client binaries (loot protocol symbols), editor prop tables.
**Evidence:** `proof/netcode/mode_juejing/mode_doodads.txt`, `mode_doodad_stats.txt`,
`doodad_tables_parse.txt`, `loot_symbols.txt`, `drop_paths.txt`.

The name the **game data** uses for the BR systems is **沙漠风暴** (this is an evidence-backed
label: the `DoodadTemplate` sets, doodad script paths and cooldown notes use it — not a claim
about UI naming, where the modes appear as 龙门绝境/绝境 etc.). Maps: `龙门寻宝` (DisplayName
龙门绝境), 白龙绝境, 天原绝境, plus 海岛/洱海/林海 which are out of scope here.

---

## 0. Answer: do we know where loot spawns?

**No — not from client files.** Nothing local contains the spawn positions/density; evidence
indicates they are authored server-side (exporter structures + absence from every local store).
What we know and do not know, precisely:

| Question | Answer | Confidence |
|---|---|---|
| Which loot can spawn? | 423 container rows across the two `沙漠风暴` sets (255 + 168) | HIGH |
| How does a container behave? | interaction frames, loot window, despawn, respawn, pick-up rules | HIGH (fields), frame→seconds unverified |
| Which drop table does each container use? | 97 named tables (`沙漠风暴/equipment1.tab`, ...) | HIGH |
| What is inside a drop table (item + rate)? | unknown — tables not present in client pak | — |
| **Where do containers appear on the map?** | **unknown — spawn anchors are in server map logic** | — |
| How many per anchor, respawn timing per anchor? | unknown — server mode script | — |

Exhausted static avenues (all 0 hits):
- per-map logical files: `CustomObject.tab`, `DoodadRelive\DoodadReviveList.tab`,
  `AnchorPoint\AnchorPointList.tab`, `NpcRelive\MapReviveList.tab`, `.land` / `.pland`,
  `logicalTrigger.json`, `LandObject.tab`
- 97 drop-table paths across 15 VFS prefixes
- all doodad scripts (`scripts/Map/沙漠风暴/doodad/*.lua`, 25 distinct hooks)
- map bundles extracted from PakV4 contain no doodad/spawn records
- launcher/editor cache (15 GB, path-hash PakV5 reader): for the four BR maps only the
  `.jsonmap` descriptors are cached (龙门寻宝, 天原绝境); every logical/spawn file misses
  (`cache_map_probe.txt`, hits=2/32)

How to actually get spawn positions (runtime only):
1. **Capture `OnSyncNewDoodad` / `OnSyncSimpleObject` / `OnSyncDoodadState`** while
   playing: each spawned container carries doodad id + position. Log over several
   matches per map, cluster positions → anchor set; histogram template ids per anchor →
   spawn weights.
2. Read the server's map logic (`CustomObject` / `AnchorPoint` / mode script) if it ever
   becomes available.
3. Client-side visual markers do not reveal anchors (containers only exist after the
   server spawns them; empty anchors render nothing).

---

## 1. Loot container schema (`settings\DoodadTemplate.tab`, 51 columns)

```
ID | Name | MapName | Kind | Level | ClassID | CraftID | CanOperateEach | IsSelectable |
RemoveDelay | ReviveDelay | OpenPrepareFrame | Drop1..Drop10 | Count1..Count10 |
RepresentID | SprintRepresentID | MoneyMin | MoneyMax | MoneyDropRate | CanPick |
BarText | Script | OverLootFrame | DynamicObstacle | ProgressID |
IndependentDrop | IndependentDropCount | IndependentDropType | AreaBroadcast |
IdentityVisiableID | ShowName | SceneFilter | DisableNavObstacle
```

Loot-relevant fields:

| Field | Meaning |
|---|---|
| `MapName=沙漠风暴` | the BR template set: **255 rows** (plus 168 rows in `沙漠风暴_寻宝模式`, 12 in `沙漠公益活动`; total 435) |
| `Kind=7` | loot container (349 rows of the 423 in the two 沙漠风暴 sets); other kinds: 8 (42), 0 (24), 3, 2, 14 |
| `ClassID` | links to `DoodadClass.tab` class drops (corpse/mining/herb classes) |
| `DropN` + `CountN` | up to 10 **drop-table** references with stack counts |
| `CanOperateEach=1` | each container lootable once (per player) |
| `OpenPrepareFrame` | interaction wind-up frames before the loot opens (8 default; 48/96 for gatherables) |
| `OverLootFrame=320` | loot window/ownership window (1440 for gatherables) |
| `ReviveDelay` | container respawn delay (180 frames on 4 templates; **frame→seconds not verified**) |
| `RemoveDelay=2880` | node despawn (砂石/灌木/瓦罐/枯树; **frame rate not verified** — would be 96 s at 30 fps) |
| `MoneyMin/Max/DropRate` | coin drops (`MoneyDropRate=1048576` = 100%, engine fixed-point) |
| `CanPick` / `BarText` | interaction verb: 拾取 (254), 打开, 操作, 逃出生天, 打破侠客内力封锁 |
| `Script` | doodad script hook path (files not present in the client pak — probed) |
| `IndependentDrop*` | independent per-player roll columns (all 0 for BR rows; randomness is in the drop tables/spawner) |

## 2. What spawns (423 rows in the two 沙漠风暴 sets)

**Tiered gear boxes** (the main ground loot): 一阶/二阶/三阶/天阶 × 装备/武器
(`equipment1..3`, `weapon1..3`, `chengzhuang`, `chengwu`), each with per-map variants:
`_sea` (海岛), `_bl` (白龙), `_lw` (林海), `_skill`, plus `weaponZ4`, `equipment4_lw`,
`jewelry4_skill` — see §4 for the full drop-table list.

**Consumables**: 金疮药 (jinchuangyao), 止血草 (zhixuecao), 麻布绷带 (mabubengdai),
鬼黄藤 (guihuangteng), 千里镜 (qianlijing), 行气散 (xingqisan), 定安散, 回蓝道具
(huilandaoju), 天赐·鸡肉腊八粥 (labazhou).

**Specials / objective items**: 匿踪宝盒 (hide.tab, stealth box), 觅踪窥影烟 (fanyinyan),
流萤魂返丹 (heilianhua), 驼铃 (mount summon, tuoling), 飞艇 (airship, feiting),
楼兰神兵匣 (zuzhoubox_purple/yellow), 马匪的货箱 (MultiDrop: chengwu+chengzhuang+
gaojidaoju+zizhuang+horse3), 叹息风碑, 钥匙开的宝箱, 吃鸡铁血宝箱 ("blood chest"),
拾取吃鸡技能 (skill pickups).

**Gatherables** (Kind 8/0): 砂石, 灌木, 瓦罐, 枯树 — no drop table, money drop rate 100%,
`RemoveDelay=2880`, long loot window (1440).

**Player-death bags**: 丢弃的装备/武器·一/二/三/天, 丢弃的金疮药/麻布绷带/砂石/灌木/瓦罐/
鬼黄藤/行气散/定安散 — dynamic loot from the dead player's inventory
(synced via `OnSyncDropItem`, not drop-table driven).

**Random/decoy variants** (client script names prove the "half random" flavour):
`未知的宝箱` (unknown chest) vs `已知类型的宝箱` (known-type chest) vs
`伪传的秘密` (false secret, 95 templates) — the client rolls/implements identification
when the container is opened.

## 3. Spawn placement (the "certain places, half random" part)

Client tables do **not** contain the placement:
- map bundles (`白龙绝境`, `天原绝境`, `海岛绝境`, `龙门寻宝`) carry only
  landscape/environment descriptors; no doodad/spawn records (scan: 0 hits).
- per-map logical files (`CustomObject.tab`, `DoodadRelive\DoodadReviveList.tab`,
  `AnchorPointList.tab`, `MapReviveList.tab`, `NpcTeamList.tab`) are referenced by the
  logic module but **are not extractable from the client pak**.

So the spawner distribution is server-side: the mode script picks spawn points (fixed
anchor sets per map) and rolls which template lands on each. The client just receives
`OnSyncNewDoodad` / `OnSyncDoodadState` / `OnSyncSimpleObject` and renders them.

## 4. Drop tables (referenced by name; rows not present locally)

97 distinct `DropN` tables are referenced, e.g.:

```
chengwu.tab (9)  horse3.tab (7)  equipment3_bl.tab (5)  chengzhuang.tab (3)
equipment3_lw.tab (3)  jewelry4_skill.tab (3)  zuzhoubox_purple/bl/yellow (4)
equipment3_sea.tab (2)  horse3_sea.tab (2)  huilandaoju_sea (2)  labazhou,
hide.tab, fanyinyan.tab, heilianhua.tab, tuoling.tab, feiting.tab, ...
```

All content paths were probed across 15 VFS prefixes (`settings\`, `沙漠风暴\`,
`Doodad\`, `Drop\`, ...) — **0 extracted**. Conclusion (evidence-scoped): the tables are
**not present in the client PakV4 or any other local store**; they are most likely
server-side. What the client ships is *which table* each container uses, not its rows.

## 5. Client interaction + protocol (verified names only)

Verified strings (in `JX3ClientX64.exe` / `JX3LogicEditOperationX64.dll`):

```
CanLoot / CanLootBoxItem / KPlayer::LuaCanLootBoxItem   (UI gating)
OnOpenLootList / OnSyncLootList / OnCloseLootWindow / OnSyncDropItem*  (*coin-shop handler, not loot)
DoApplyLootList (proto 0x4D) / DoLootMoney (proto 0x51) (C→S take)
OnAddItemNotify / OnSyncItemData                        (inventory sync)
MaxLootRange (MapList 绝境 rows = 5) / nLootIndex / nLootItemIndex / LootMode
```

`DoPickPrepare` / `OnBreakPickPrepare` / `PickUpItem` are **not present as strings in those
two binaries** (scope-limited search — not proof they don't exist elsewhere). Verified flow:
client side plays the `OpenPrepareFrame` wind-up, sends `DoApplyLootList` (0x4D) for the
container, server answers `OnSyncLootList`, taking an entry is what fills the loot window;
inventory updates come via `OnAddItemNotify`/`OnSyncItemData`.

## 6. Summary of the randomness model

| Layer | Where | Client-visible? | Confidence |
|---|---|---|---|
| which spawn anchors exist on the map | server map logic (`CustomObject` / anchor points) | no | MED — exporter writes such files; no runtime read found |
| which template lands on an anchor | server mode script (spawner rolls) | no | INFERRED — not directly observed |
| which item comes out of a container | drop table `沙漠风暴/<tier>.tab` | only table name | HIGH for the reference; rows not local |
| identification/decoy behaviour | container template + script hook names | names only (未知/已知/伪传) | MED |
| interaction, timing, respawn | `DoodadTemplate` fields (`OpenPrepareFrame`, `OverLootFrame`, `ReviveDelay`, `RemoveDelay`) | yes | HIGH (fields), frame→seconds unverified |
| pickup rules | `MapList` (`MaxLootRange=5`, item/skill ban masks) + loot protocol | yes | HIGH |

To reconstruct actual spawn density/positions and drop rates, the remaining options are
runtime: capture `OnSyncNewDoodad`/`OnSyncDoodadState` while playing the mode, or read
the server's map logic if it is ever available. Static client mining is exhausted here.

## 7. Roll tables — the possible item pool (2026-09-21 update)

**Question asked:** can we know the random roll table per loot container?

| Question | Answer | Evidence |
|---|---|---|
| which drop table does a container use? | yes — 97 named tables (`沙漠风暴/equipment1.tab`, ...) | `mode_doodad_stats.txt` |
| what rows/rates are inside a drop table? | **no** — not in the client | see below |
| what items can appear in the mode? | **mostly yes** — 133 mode item scripts + consumables/specials | `mode_item_scripts.txt`, `mode_item_catalog.txt` |
| exact per-container roll odds | **no** — server-side; reconstruct empirically | §5 protocol |

Where the drop tables are not:

- client PakV4: all 97 names × 15 VFS prefixes → 0 extracted
- per-map logical files (`CustomObject.tab`, `AnchorPointList.tab`, `DoodadReviveList.tab`,
  `MapReviveList.tab`, `.land`/`.pland`, `logicalTrigger.json`) → 0 extracted
- launcher/editor caches: resolved with a path-hash PakV5 reader
  (`jx3-web-map-viewer/tools/jx3-cache-reader.js`) — asset roots (`data/source/...`)
  hit, but `settings/...` and `scripts/...` are **not present** in those caches

What we *can* enumerate — the item pool:

- **133 `scripts/skill/沙漠风暴/道具_*.lua`** item scripts (source, from the earlier
  map-viewer extraction cache): 临时飞爪, 孤风飒踏冲刺, 乘黄之威, 九转归一, 云栖松,
  人剑合一, 任驰骋, 伪九霄风雷, 傍花随柳, 凌然天风, 剑破虚空, 剑转流云, 化蝶, 十方玄机,
  听风吹雪, 唐门车装填, 啸如虎, 夺命蛊, 如意法, 孤影化双, 守如山, 平沙落雁, 幻蛊,
  应天授命, 徐如林, 心诤(4), 怖畏暗刑, 惊鸿游龙, 抢珠式, 振翅图南, 捉影式, 撼地, 散流霞, …
  (full list in `mode_item_scripts.txt`; effect summaries in `mode_item_catalog.txt`)
- 163 `绝境战场/绝境_*.lua` skill scripts (the mode's weapon/starter kits)
- direct-pickup templates in `DoodadTemplate` (consumables, 匿踪宝盒, 觅踪窥影烟,
  流萤魂返丹, 驼铃, 飞艇, 楼兰神兵匣, 马匪的货物, 钥匙开的宝藏, …)

Note on script shipping: the current client pak stores some scripts as Lua 5.1 bytecode
(`\x1bLuaQ`, e.g. `CheckTreasureBattleFieldMap.lua`), while the earlier extracted cache
holds readable GBK source — both are useful, the cache gives the readable form.

To recover **exact roll contents/rates**, capture the loot messages while playing:
`OnOpenLootList` / `OnSyncLootList` carry the rolled result for each opened container,
and `OnSyncDropItem` / `OnSyncNewDoodad` carry dynamic/dropped items. Logging container id
(`DoodadTemplate` ID) + rolled items over many opens reconstructs the tables empirically.

## 8. The spawn→pickup action chain: what we have vs what's missing

```
[round start / server]
  A1 spawn anchor set per map .............. MISSING  (server map logic: CustomObject/AnchorPoint)
  A2 per-map tier/config selection ......... PARTIAL  (recovered from a shipped Lua bytecode)
  A3 spawner rolls (count, template, weight) MISSING  (server mode script)
        |
        v
[S2C replication]
  B1 doodad create/state to client ......... RECOVERED (layouts in JX3_LOOT_PROTOCOL_LAYOUTS.md)
  B2 client caches + renders container ..... KNOWN   (DoodadTemplate fields, interaction rules)
        |
        v
[C2S interaction]
  C1 pick request ......................... RECOVERED (DoApplyLootList proto 0x4D, DoLootMoney 0x51)
  C2 server validation (range/owner/once) .. INFERRED (MaxLootRange=5, CanOperateEach)
        |
        v
[server roll]
  D1 container -> drop table lookup ........ KNOWN   (97 named tables)
  D2 table contents + roll algorithm ....... MISSING  (server-side; not in client pak)
  D3 decoy/identification/special chains ... NAMES ONLY (未知的宝藏/已知类型的宝藏/伪传的秘籍/
                                                       䖳装备触发铁血宝箱; scripts not shipped)
        |
        v
[S2C result]
  E1 rolled loot list ...................... RECOVERED (OnSyncLootList: looters + item records)
  E2 dropped/dynamic items ................. same channel (new doodad / loot list); no separate
                                                       client handler found (OnSyncDropItem is coin shop)
        |
        v
[C2S take]
  F1 take item ............................. RECOVERED (DoApplyLootList 0x4D)
  F2 inventory grant ....................... NAME ONLY (OnAddItemNotify/OnSyncItemData)
        |
        v
[lifecycle]
  G1 per-template timings .................. KNOWN   (Prepare/OverLoot/Remove/Revive frames)
  G2 per-anchor respawn/refresh ............ MISSING  (server DoodadReviveList/MapReviveList)
```

**Recoverable statically right now** (we have the tooling and the binaries):

1. **B1, C1, E1, E2, F1, F2 payload layouts** — disassemble the handlers in
   `JX3LogicEditOperationX64.dll` (same method as `OnSwitchMap`/`DoClientConfirmReady`);
   gives exact fields for doodad sync, loot list, and take-item messages.
2. **A2 + parts of D3** — more shipped Lua bytecode: the current pak stores scripts as
   Lua 5.1 bytecode; `tools/netcode/lua51_dump.py` extracts constants/globals safely.
   First result: `CheckTreasureBattleFieldMap.lua` contains the **map→tier/config table**
   (IDs 296/297, 410, 512, 532, 645, 676/677, 709/715 → tiers 1..5), a buff refresh
   (`AddBuff(dwID=26524, level 1800, custom value)`), a desert-horse list, the
   `Tool_GetChickenMapItem` hook, and an item-grant routine
   (`GetItem`/`INVENTORY_INDEX`/`EQUIP`, box slots, `Type_Index` item ids like `7_100446`,
   `6_42592`, `8_44286`).
3. **More bytecode sources** — probe/extract other shipped map scripts
   (`scripts\Map\<map>\include\*.lua`) and dump them for spawn/round logic.

**Only obtainable at runtime** (not in any local file):

- A1 anchor positions, A3 spawner weights, D2 drop-table rows/rates, G2 per-anchor respawn.
  Capture `OnSyncNewDoodad`/`OnSyncDoodadState` (spawns + positions) and
  `OnSyncLootList`/`OnSyncDropItem` (rolled contents) across matches, then reconstruct.

Note: earlier inline PakV4 probes used a relative work path and silently returned 0
(fixed in `extract_pak_paths.py`). All drop-table/doodad conclusions above were
re-verified with the fixed tool (`pak_candidates8.txt`: 97 tables × prefixes + doodad
scripts × map roots → 0 files).

## 9. Reproduce

```powershell
python tools\netcode\extract_pak_paths.py --list proof\netcode\mode_juejing\pak_candidates6.txt --out-dir proof\netcode\mode_juejing\pak_out6
# then parse pak_out6\DoodadTemplate.tab (GBK TSV, MapName=沙漠风暴)
python tools\netcode\mine_item_scripts.py    # item pool catalog from the extraction cache
```

Evidence: `mode_doodads.txt` (435 rows incl. `沙漠公益活动`; 423 in the two `沙漠风暴` sets),
`mode_doodad_stats.txt` (kind/bar/prepare/frame stats + 97 drop tables + 25 script hooks),
`doodad_tables_parse.txt` (DoodadClass + samples), `loot_symbols.txt` (loot protocol),
`drop_paths.txt` / `drop_ctx.txt` (drop-file references),
`mode_item_scripts.txt` + `mode_item_catalog.txt` (item pool),
`cache_files.txt` / `cacheout2/` probe notes (launcher-cache hash reader results).
