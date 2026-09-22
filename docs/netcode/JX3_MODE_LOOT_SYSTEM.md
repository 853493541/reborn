# 绝境战场 / 沙漠风暴 — ground loot system (PUBG mode)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Sources:** `settings\DoodadTemplate.tab` (11,011 rows), `settings\DoodadClass.tab`,
`settings\MapList.tab`, client binaries (loot protocol symbols), editor prop tables.
**Evidence:** `proof/netcode/mode_juejing/mode_doodads.txt`, `mode_doodad_stats.txt`,
`doodad_tables_parse.txt`, `loot_symbols.txt`, `drop_paths.txt`.

The BR mode's internal system name is **沙漠风暴** ("desert storm"); 绝境 maps run it.
Loot is not hard-placed in the client map data — it is **doodad templates + server-side
spawn points + drop tables**. Everything below is what the client ships.

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
| `MapName=沙漠风暴` | the BR template set (423 rows incl. 寻宝模式 variants) |
| `Kind=7` | loot container (349 rows); other kinds: 8 (42), 0 (24), 3, 2, 14 |
| `ClassID` | links to `DoodadClass.tab` class drops (corpse/mining/herb classes) |
| `DropN` + `CountN` | up to 10 **drop-table** references with stack counts |
| `CanOperateEach=1` | each container lootable once (per player) |
| `OpenPrepareFrame` | interaction wind-up frames before the loot opens (8 default; 48/96 for gatherables) |
| `OverLootFrame=320` | loot window/ownership window (1440 for gatherables) |
| `ReviveDelay` | container respawn delay (180 frames ≈ 6 s on 4 templates) |
| `RemoveDelay=2880` | node despawn (砂石/灌木/瓦罐/枯树 ≈ 96 s at 30 fps) |
| `MoneyMin/Max/DropRate` | coin drops (`MoneyDropRate=1048576` = 100%) |
| `CanPick` / `BarText` | interaction verb: 拾取 (254), 打开, 操作, 逃出生天, 打破侠客内力封锁 |
| `Script` | client-side doodad script hook (not shipped in the client pak) |
| `IndependentDrop*` | independent per-player rolls (all 0 for BR rows — randomness is in the drop tables/spawner) |

## 2. What spawns (423 沙漠风暴 templates)

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

## 4. Drop tables (server-side, referenced by name)

97 distinct `DropN` tables are referenced, e.g.:

```
chengwu.tab (9)  horse3.tab (7)  equipment3_bl.tab (5)  chengzhuang.tab (3)
equipment3_lw.tab (3)  jewelry4_skill.tab (3)  zuzhoubox_purple/bl/yellow (4)
equipment3_sea.tab (2)  horse3_sea.tab (2)  huilandaoju_sea (2)  labazhou,
hide.tab, fanyinyan.tab, heilianhua.tab, tuoling.tab, feiting.tab, ...
```

All content paths were probed across 15 VFS prefixes (`settings\`, `沙漠风暴\`,
`Doodad\`, `Drop\`, ...) — **0 extracted**: drop tables, like mode scripts, are
server-only. What the client knows is *which table* each container uses, not its rows.

## 5. Client interaction + protocol (visible)

```
CanLoot / CanLootBoxItem / DoPickPrepare / OnBreakPickPrepare / OnBreakPicking
PickUpItem / OverLootFrame / PlayerBeginPickAnimationID / PlayerEndPickAnimationID
OnOpenLootList / OnSyncLootList / OnCloseLootWindow / OnSyncDropItem
MaxLootRange (MapList 绝境 rows = 5) / nLootIndex / nLootItemIndex / LootMode
```

Flow: client asks to pick (`DoPickPrepare`) → server validates range/ownership/loot mode
→ returns loot list (`OnSyncLootList`) → client opens the loot window (`OverLootFrame`),
player takes items (`PickUpItem`), server syncs inventory (`OnAddItemNotify` etc.).

## 6. Summary of the randomness model

| Layer | Where | Client-visible? |
|---|---|---|
| which spawn anchors exist on the map | server map logic (`CustomObject` / anchor points) | no |
| which template lands on an anchor | server mode script (spawner rolls) | no |
| which item comes out of a container | drop table `沙漠风暴/<tier>.tab` | only table name |
| identification/decoy behaviour | container template + client script hook names | names only (未知/已知/伪传) |
| interaction, timing, respawn | `DoodadTemplate` fields (`OpenPrepareFrame`, `OverLootFrame`, `ReviveDelay`, `RemoveDelay`) | yes |
| pickup rules | `MapList` (`MaxLootRange=5`, item/skill ban masks) + loot protocol | yes |

To reconstruct actual spawn density/positions and drop rates, the remaining options are
runtime: capture `OnSyncNewDoodad`/`OnSyncDoodadState` while playing the mode, or read
the server's map logic if it is ever available. Static client mining is exhausted here.

## 7. Reproduce

```powershell
python tools\netcode\extract_pak_paths.py --list proof\netcode\mode_juejing\pak_candidates6.txt --out-dir proof\netcode\mode_juejing\pak_out6
# then parse pak_out6\DoodadTemplate.tab (GBK TSV, MapName=沙漠风暴)
```

Evidence: `mode_doodads.txt` (435 mode rows), `mode_doodad_stats.txt`
(kind/bar/prepare/frame stats + 97 drop tables + 25 script hooks),
`doodad_tables_parse.txt` (DoodadClass + samples), `loot_symbols.txt` (loot protocol),
`drop_paths.txt` / `drop_ctx.txt` (drop-file references).
