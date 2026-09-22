# JX3 mode "绝境战场" — client-side data map

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Method:** static client mining only — GBK/UTF-16 grep on binaries and editor tables, MapList parse, ASCII symbol scans.
**Evidence:** `proof/netcode/mode_juejing/*.txt` (see §7).
**Tool:** `tools/netcode/gbk_grep.py` (searches GBK + UTF-16LE Chinese terms with context).

Confidence: **HIGH** = literal string with file/offset; **MED** = inferred from naming/API shape.

---

## 1. Finding method

```
gbk_grep.py 绝境 → MovieEditor\ResourcePack\** (34 tables hit)
                  client bin64 (JX3ClientX64.exe/.dlls): 0 Chinese hits
MapList.tab parse → map ids and logical paths
exe/dll ASCII scan → Battlefield* API and protocol names
RepresentModelInfo\*.tab → mode content: stances, gear skins, doodads
```

Chinese mode names are **not in the client binaries** (UI text ships in PakV4 / server-
synced), but the mode's assets and protocol surface are fully visible locally.

---

## 2. The 绝境 map series (HIGH)

`MapList.tab` (622 rows) contains six 绝境 rows → five unique maps:

| Map id | Name | Logical path |
|---|---|---|
| 410 | 海岛绝境 | `data\source\maps\海岛绝境\海岛绝境.jsonmap` |
| 512 | 白龙绝境 | `data\source\maps\白龙绝境\白龙绝境.jsonmap` |
| 532 | 天原绝境 | `data\source\maps\天原绝境\天原绝境.jsonmap` |
| 645 | 洱海绝境 | `data\source\maps\洱海绝境\洱海绝境.jsonmap` |
| 709 / 715 | 林海绝境 (duplicate id) | `data\source\maps\林海绝境\林海绝境.jsonmap` |

Three of these already render in our engine host spike: `proof/map_spike/`
(海岛绝境 id410, 白龙绝境 id512, 天原绝境 id532 — tour PNGs + extracted bundles).

## 3. Mode-specific content visible in editor tables (HIGH)

### 3.1 Dedicated player stances
`Ani.rt` (78 unique matches) has a stance set for all four body models:

```
F1/F2/M1/M2b02ty龙门绝境_站姿01..05.ani
```

A mode-only idle/pose set ⇒ the mode displays characters in a special lobby/staging pose.

### 3.2 Loot / interactive props (林海绝境 examples)
`Mesh.rt` (154 matches) lists mode props such as:

```
WJ_lhjj九幽凝香珀  WJ_lhjj劫藏谶骨  WJ_lhjj太乙金丝  WJ_lhjj天星
WJ_lhjj射箭靶  WJ_lhjj毒蘑菇  WJ_lhjj活动珍珠  WJ_lhjj焚劫
WJ_lhjj玉琀  WJ_lhjj红宝石  WJ_lhjj蓝宝石  WJ_lhjj血蛊巢穴  WJ_lhjj恐龙
```

(`lhjj` = 林海绝境) — collectible/treasure objects and interactive props, same idea as
the tier loot in §3.4.

### 3.3 Mode UI / countdown effects
`Pss.rt` (9 matches):

```
UI_黑山绝境_倒计时通用底框.pss     ← countdown frame (phase/timer UI)
ui_龙门绝境01.pss
Y_烟花_十二周年_闯龙门绝境.pss
```

`倒计时` (countdown) frame ⇒ timed mode phases (e.g. shrink/round timer) confirmed by assets.

### 3.4 Related survival-loot mode tables
`ResourcePack\RepresentModelInfo\Doodad\`:

| Table | Content |
|---|---|
| `沙漠风暴.tab` (256 rows) | loot props: 一阶/二阶/三阶 装备 + 武器 (tier 1-3 gear and weapons as world props) |
| `沙漠风暴_寻宝模式.tab` (169 rows) | 伪装蘑菇 / 伪装药罐 / 伪装食人花 / 伪装遗骸 (disguises), 匿踪宝盒 (stealth box) |
| `寻宝系统.tab` | 九州祈福灯, 宝箱 |

Same battle-royale item-progression family; useful as a template when rebuilding loot.

### 3.5 Mode gear set
Equipment skin tables (`Armor\Bangle|Chest|Hat|Pants|Waist|Weapon*.tab`, all body models)
contain a 6-variant set per slot, ids ≈ 8248+:

```
绝境逢生·若倾 绝境逢生·惊梦 绝境逢生·寄海 绝境逢生·幽桓 绝境逢生·暮稂 绝境逢生·辞归
```

## 4. Client-side machinery (HIGH unless noted)

### 4.1 Queue / entry
```
JoinBattleFieldQueue  LeaveBattleFieldQueue  AcceptJoinBattleField  LeaveBattleField
JoinTongBattleFieldQueue  LeaveTongBattleFieldQueue  AcceptJoinTongBattleField
BF_QUEUE_COUNTDOWN_BUFF  DoComfirmEnterQueueMap  DoQueryMapQueueInfo  DoLeaveMapQueue
```
(exe strings) — solo and guild (Tong) queues, countdown buff, map-queue confirmation.

### 4.2 Rooms / match start
```
KPlayerClient::OnCreateBattlefieldRoomRespond     (S2C_BATTLE_FIELD_CREATE_ROOM_RESPOND)
KPlayerClient::OnForceStartBattleFieldChaosFightRespond
    (S2C_FORCE_START_BATTLE_FIELD_CHAOS_FIGHT_RESPOND)
```
⇒ custom rooms and a "chaos fight" force-start mode.

### 4.3 Teams / sides
```
DoApplyBFPlayerTeamGroupIDInfo  ApplyBFPlayerTeamGroupID  GetAllBFPlayerTeamGroupID
KPlayerClient::OnSetBattleFieldSide
KCharacter::SetBattleFieldSide   (INVALID_BATTLE_SIDE < side < MAX_BATTLE_SIDE)
nameplate struct field: nBattleFieldSide
```
⇒ players grouped into battlefield teams and assigned a side shown on nameplates.

### 4.4 Competitor state sync (the core of the mode)
```
DoSyncBattlefieldCompetitorsListRequest / OnSyncBaseInfoFromBattlefieldCompetitorList
                                        OnSyncVariableInfoFromBattlefieldCompetitorList
DoSyncBattlefieldCompetitorSkillCDStateRequest / cancel
    OnSyncBattlefieldCompetitorCDState   OnSyncBattlefieldCompetitorBuffList
OnSyncBattlefieldStatistics     (also KVideoReplayer::OnSyncBattlefieldStatistics)
KBattlefieldCache::AddNextSyncTime          ← periodic client cache refresh
GetBattlefieldPlayersPosInfo                ← competitor positions (Lua-facing)
m_pScene->m_BattlefieldCompetitorInfoMap    ← client-side map of competitors
S2C_SYNC_BATTLEFIELD_COMPETITOR_BUFF_LIST   ← size-validated fixed record
```
⇒ the client tracks every competitor's **skill cooldowns, buffs, stats and positions**
for the mode HUD (scoreboard/overlay), refreshed on a timer.

### 4.5 Representation / HUD
```
BFSPosition                       HandleGetBFPlayerKungfuID  HandleGetBFPlayerParam
KREPRESENT_EVENT_GET_ALLBFPLAYER_KUNGFUID / _PARAM
UpdateBFPlayerHeadTopParam        KRLCharacter::UpdateCJBattleFieldHeadTop
set local battlefield side        ("set local battlefield side" console command)
```
⇒ head-top overlays show class/params for battlefield players.

### 4.6 Economy
```
BattleFieldIncomePunish           (next to DungeonIncomePunish, CHEAT_PUNISH)
```
⇒ battlefield earnings are subject to a separate income-penalty rule.

## 5. What the client does *not* give statically

| Missing | Why / next step |
|---|---|
| player count, phase durations, shrink/zone rules, revive rules | server-authoritative or in PakV4-packed UI; not literal strings in client binaries |
| mode UI layout/labels | UI ships inside PakV4 — enumerate with `KG_PAKFS_CollectAllFileNames` from an engine host, or inspect live |
| exact loot tables per map | server-side; client only has prop models (§3.2, §3.4) |
| runtime numbers for competitor sync (rate) | field of `KBattlefieldCache::AddNextSyncTime` — disassemble for the interval |

## 6. Practical next experiments

1. **Enumerate PakV4** with `KG_PAKFS_CollectAllFileNames` (export exists in
   `KGPK4_FileSystemX64.dll`) to find mode UI/config files, then parse.
2. **Disassemble `KBattlefieldCache`** / `GetBattlefieldPlayersPosInfo` for sync rate and fields.
3. **Render the remaining maps** (洱海绝境 id645, 林海绝境 id709) with the existing map host.
4. **Extract mode props** (loot meshes) with `PakV4SfxExtract` to build a loot catalog.

## 7. Evidence & reproduce

```powershell
# in worktree C:\Users\Zhibin Ren\Desktop\reborn-netcode
python tools\netcode\gbk_grep.py 绝境 --dir "C:\SeasunGame\MovieEditor\ResourcePack" --ext .rt,.tab,.txt,.xml,.json --out proof\netcode\mode_juejing\gbk_juejing_editor.txt
python tools\netcode\gbk_grep.py 绝境 --dir "C:\SeasunGame\Game\JX3\bin\zhcn_hd\interface" --ext .lua,.jx3dat --out proof\netcode\mode_juejing\gbk_juejing_interface.txt
```

Evidence files:
- `proof/netcode/mode_juejing/gbk_juejing_editor.txt` (240 hits)
- `proof/netcode/mode_juejing/editor_assets_juejing.txt` (34 tables, unique matches)
- `proof/netcode/mode_juejing/exe_mode_keywords.txt` (Battlefield/ChaosFight offsets)
- `proof/netcode/mode_juejing/battlefield_api_symbols.txt` (logic/exe/represent API sets)
- `proof/netcode/mode_juejing/doodad_tables.txt` (沙漠风暴 / 寻宝模式 / 寻宝系统)
- `proof/netcode/mode_juejing/tani_juejing.txt` (Tani.rt: 0 hits — mode has no tanis; stances live in Ani.rt)
- `proof/netcode/mode_juejing/map_bundles.txt` (绝境 map jsonmap/Setting/environment keys)
