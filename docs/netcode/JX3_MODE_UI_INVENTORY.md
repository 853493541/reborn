# JX3 绝境战场 — UI inventory (what appears, stage by stage)

**Branch:** main
**Date:** 2026-09-24
**Method:** official PakV4 UI assets only — extracted `ui/Config/Default/*` windows
(ini + Lua bytecode/decompiled), the official string tables
(`ui/Scheme/Case/string.txt`, `string_PVPAcount.txt`), and code/protocol evidence.
The `interface\` addon tree is excluded (see `JX3_MODE_MATCH_LIFECYCLE.md` §10).

Legend: **PROVEN** = window/label located in shipped data · **PARTIAL** = labels or
assets located but the renderer/consumer is missing · **OPEN** = expected, not found yet.

**Interactive viewer:** `ui-process-app` (WPF, same stack as `map-ui-app`) renders every
window below from its real KGUI INI with the shared `UiLayout` engine —
`dotnet run --project ui-process-app`; headless check
`UiProcessApp.exe --selftest` (currently 15/15 windows rendered).

---

## 0. Flow at a glance

```
[queue panel] -> [match-found / auto-enter] -> [loading window]
   -> [staging: countdown + safe zone] -> [in-match HUD]
   -> [death / revive] -> [settlement PVPShowFinal] -> [leave / back to world]
```

---

## 1. Queue panel — `NewBattleFieldQueue`

**PROVEN** (`ui/Config/Default/NewBattleFieldQueue.{ini,lua}`, 1,129 ini sections;
decompiled Lua `proof/minimap/ui/Config/Default/decompiled/NewBattleFieldQueue.decompiled.lua`).

Mode tabs (`PageSet_Total`): `CheckBox_DesertStorm` (绝境/沙漠风暴),
`CheckBox_DesertStorm_Single` (单人), `CheckBox_DesertStorm_Skill` (技能向),
`CheckBox_DesertStorm_Takov` (塔科夫向/林海), plus `CheckBox_Battlefield`,
`CheckBox_Moba`, `CheckBox_Zombie`, `CheckBox_Bomb`, `CheckBox_CustomizeBattlefield`.

`Page_DesertStorm` contents (`NewBattleFieldQueue.ini` sections):

| element | role |
|---|---|
| `Handle_FEISHA`, `Image_FSbg`, `Text_AllNum{T}`, `Text_WeekNum{T}`, `Text_MyScore`, `Text_PersonalScore` | 飞沙 (currency) totals/weekly caps/personal score |
| `Btn_SingleQueue` / `Btn_TeamQueue` / `Btn_RoomQueue` (+ `Handle_Mathing_*`, `Text_Matching_*`, `Text_Time_*`, `SFX_*`) | solo / team / custom-room queue with matching state + elapsed time |
| `Image_S/T/R WeiWang|ZhanJie|ZHF`, `Text_*` | weekly reward preview (威望/战阶/战魂) |
| `Btn_LeaveSingleQueue` / `Btn_LeaveTeamQueue` / `Btn_LeaveRoomQueue` | leave queue |
| `WndContainer_Buff`, `Handle_Buff` | queue buffs (includes the queue countdown buff) |
| `Text_TIP_DSTime`, `Text_TIP_DS`, `Image_Rule_4`, `Text_TIPWARNING1` | mode rules/tips |
| `Text_BattleName`, `Wnd_ListMaps`, `Handle_BattlefieldList`, `Btn_MapDown` | map list rows |

Room UI (custom rooms): `Bool_CreateRoom/JoinRoom/StartGame/DisbandRoom/ExitRoom`
confirm dialogs, room member list, OB slot (`NewBattleFieldQueue.decompiled.lua:5604-5882`).

## 2. Queue tracking — `MapQueue`

**PROVEN** (`MapQueue.{ini,lua}`): per-map queue rows, cancel menu 点击取消排队
(`MAP_QUEUE_CANCEL`), elapsed time (`STR_MAP_QUEUE` 排队中), and the auto-enter
checkbox `STR_MAP_QUEUE_AUTOSURE` = 自动进入 (`MapQueue.ini` consumer). Queue position
updates via `OnQueuePosUpdate` / `OnSyncMapQueueInfo`.

## 3. Match found / accept

- **PROVEN (flow):** queue pop → confirm dialog, or silent entry when 自动进入 is
  checked → `DoComfirmEnterQueueMap` (C→S 0x116) (`JX3_MODE_UI_FLOW.md` §3).
- **PROVEN (renderer):** the prompt is the engine's generic KGUI MessageBox
  (`ui/Config/Default/MessageBox/MessageBox.ini`, 35 sections), opened as
  `MB_entermap` by the shell `ui/script/module.lua` `ComfirmEnterQueuedMap`:
  body `FormatString(STR_SWITCHMAP_GFZ_TIP, map)` = 你要传送到"<map>"地图吗？,
  option 1 `STR_HOTKEY_SURE` (确定) with `nCountDownTime=30` through
  `MSG_BRACKET` `<D0>(<D1>)` = 确定(30), option 2 `STR_HOTKEY_CANCEL` (取消);
  the script appends the body to `Handle_Message` (`AppendItemFromString`,
  font 18), fills `Text_Option1/2`, hides `CheckBox_Msg` (no `tCheckBoxConfig`)
  and `fnAutoClose` fires when the 30 s run out. The strings live in the global
  `g_tStrings` lib `ui/String/string.lua` (bound by `ui/module_info.xml`, not in
  `ui/Scheme/Case/string.txt`); `tools/ui/extract_lua_string_table.py` decodes
  its Lua-5.1 `SETTABLE` constants into the committed TSV
  `ui-process-app/Data/text/ui/String/string.txt` (8,242 ids). The viewer
  renders the 龙门绝境 sample (MapList 296/297) at the first countdown frame:
  `ui-process-app` `ready-confirm` (inventory `appends` + `texts`); render
  `proof/ui/evidence/ready_confirm_render.png` (2026-09-29).
- `STR_PVP_Ready` = 准备好了 is a separate label (no extracted consumer);
- `STR_SETTING244` = 无需倒计时直接进入 (skip countdown and enter) — a user option
  that affects this transition.

## 4. Loading window

**PROVEN:** `NSX3DEngine::KWindowsLoadingWnd` (X3DEngine.dll) — GDI loading window with
art + progress bar + spinner; config `<game>/ui/loading/loading.ini` (18 random
backgrounds, `ui/Loading/background{1..18}.bmp` 600x333 + `progress.bmp` 441x3 +
`sprite.bmp` 28x28); progress handshake `ComfirmSyncMapProgress`; per-map loading art
extracted for 296/297/410/512/532/645/709 (`proof/netcode/mode_ui/loading/*`,
`JX3_MODE_UI_FLOW.md` §4).

**PROVEN (琉璃 KGUI overlay):** `ui/Config/Default/LoadingPanel.ini` (39 sections) +
`LoadingPanel.lua` (35 KB bytecode): the full-screen loading overlay. `GetLoadingImage`
reads the destination map's minimap `config.ini` `[loading] image=` (e.g. 龙门寻宝minimap
→ `loadinglmxb.dds`, 1920x1080; the CDN `_mb` PNG variant is the same art) and
`CorrectShow` fits it to the canvas; `ShowProgress` (proto /26) picks
`Handle_Traffic`+`Image_ProgressT` while `IsTrafficState()` (download) or
`Handle_Nor`+`Image_Progress` for a normal map load, and the traffic state adds the
`Handle_Nodes` markers with `Text_PointName`. Progress chrome: `Carriage.UITex`
frames 20-24 (track) and `CharButton.UITex` frames 18/19 (fill). The viewer replays
  the normal state as one `loading-window` entry with a page per map
  (296/297/410/512/532; 645 洱海绝境 / 709 林海绝境 dropped for now):
  `Handle_Traffic` and `Handle_PakV4Msg` hidden (the traffic bar and the PakV4
  download tips only appear while the client streams resources), `Image_Bg`
  full-bleed (cw+4 x ch+2 at -3,-1) on a 1280x720 canvas, progress bar at
  (10% w, 89.4% h) = (128,644) with a 912px fill, message handles at (128,646),
  tip panel at (655,115) — all from `CorrectShow`; renders
  `proof/ui/evidence/loading_panel_296_render.png` and
  `loading_panel_532_render.png` (2026-09-29). The CDN art is pulled with
  `tools/netcode/extract_hpkg_member.py` (packages `105/…`, `103/…`, `117/…`,
  `51/…`, `108/…`, `111/…`, `100/…`); the viewer's `TextureLoader` decodes
  PNG/BMP (WPF) in addition to the custom TGA/DDS decoders.

## 5. Staging (safe zone + countdown)

| item | evidence | status |
|---|---|---|
| countdown frame art `UI_黑山绝境_倒计时通用底框.pss` | editor asset (`editor_assets_juejing.txt:72-77`); no extracted INI references it (corpus scan) | PROVEN asset, editor/runtime effect only |
| countdown value source | `OnSyncBFPQInfo` 0x11B → absolute end time (`+0x1b490`) → `GetBattleFieldPQInfo()` 4th return | PROVEN data path |
| countdown display | the only KGUI consumer is `BattleFieldMap.UpdateTime` → `Wnd_Title/Text_Time` (`STR_BUFF_H_LEFT_TIME_MSG`, decompiled:1578-1627), and `BattleFieldMap.Init` hides `Text_Time` when `IsInTreasureBattleFieldMap()` (decompiled:1352-1359) — true for every 绝境 map (296/297/410/512/532/645/676/677/709/715, `CheckTreasureBattleFieldMap.dump.txt`) | **OPEN — no KGUI renderer in this mode**; engine/PSS or system message |
| countdown announcements | server system messages observed at T-30/20/10 s (`interface MY#DATA team_mon` countdown entries; interface-addon) | LOW |
| countdown labels | `STR_PQ_PROTIME` 阶段倒计时： and `STR_PQ_TIMETITLE` 挑战倒计时： (PQ module; mode use inferred), `STR_MAP_QUEUE` 排队中 | PARTIAL (no mode consumer in the extracted corpus) |
| safe-zone boundary | `BattleFieldMap.InitMapData` shows `Handle_CircleNew` + `Handle_StormLine` and registers `MapCircle` with `SFX_CircleNew` (`C_自己圈范围_677.pss`) for treasure maps (decompiled:5317-5341); engine-drawn, no KGUI image. `STR_SAFEZONE` 安全区 has no extracted consumer; `PROTECT_ZONE`/`ENTER_PROTECT_ZONE` (即将开启安全保护) is account security, not the BR zone | OPEN |
| staging pose | `龙门绝境_站姿01..05` animation set | PROVEN asset |
| mode HUD activation | minimap + main bar + BR skill bar appear after load | PARTIAL (activation trigger not proven) |

**Viewer state (2026-09-29):** `staging-countdown` stays a research entry (no render):
the countdown is not a KGUI window in this mode and the safe-zone circle is an
engine/PSS effect. Re-open with a client GT screenshot of the staging moment or a
PSS renderer.

## 6. In-match HUD

| UI | evidence | status |
|---|---|---|
| BR dynamic skill bar | `DynamicBattleRoyale.{ini,lua}` (default + dynamic boxes, hotkeys) | PROVEN — research only; removed from the viewer catalog 2026-09-30 (its window is not part of the product's UI list) |
| Minimap + BattleField button | `Minimap.lua`, `Btn_BattleField`, `Minimap.ini` | PROVEN |
| storm circle on minimap | `STR_SETTING210` 小地图增加风暴圈范围提示; `STR_SETTING262` 小地图安全区外警告闪烁及距离提示 | PROVEN labels |
| battlefield map panel (M) | `BattleField/BattleFieldMap.{ini,lua}`: five viewer pages 296/297/410/512/532, each with that map's minimap-pack art; the INI's map-suffixed team/line elements exist only for 512 and 709 (`Handle_Team_<currentMapID>_n` :3324-3358, `Image_CLine_` :3653) so the four other pages hide `*_512_*` + `*709*` and page 512 hides `*709*`; the window hide drops the faction/camp set (浩气盟/恶人谷 tabs, camp commander/OB markers, PK + area counts), the top-bar mode controls (`Wnd_Route` line tabs + `CheckBox_Follow` — the 绝境 line-choose phase; `CheckBox_ShowNum` + `Btn_Refresh` — `CanShowHeatMap` (map.lua:347-371) is true only for 同阵营战场/command mode, never a treasure map), the heat-map grid and the runtime item prototypes (data/map-icon/mark/arrow/event/draw-line/player/teammate templates); the title bar keeps `Btn_Setting` (PopupMenu) + `CheckBox_Minimize` (ExpandFrame); the skin filter ignores `StormLine` art — without that, hiding the heat-map items made `Handle_Map` look like old-skin chrome and the map layer was dropped as a duplicate of `Image_Bg`; `Handle_StormLine`, `MapCircle`, `SFX_CircleNew`, heat map, mark/gain/sfx notify events; only map 512 ships line data (the `Image_CLine_512_*` choice-line ring — shown; the runtime grays/normalizes one line, `ShowLootMode` :3636-3679), the other maps have none so no storm line is drawn; the 圈 is the `SFX_CircleNew` particle (no bitmap) | PROVEN renderer |
| storm countdown HUD | `STR_TIMEDESERT` 风暴倒计时： | **PARTIAL — renderer not found** (see §8) |
| remaining players | `STR_LEFTPEELE` 剩余人数： | PARTIAL — same |
| storm state | `STR_STORM` 风暴, `STR_STORM2` 风暴状态 | PROVEN labels |
| loot window / pickup | `LootList.lua`, `CanPick` bar text 拾取/打开/操作, `MaxLootRange=5` | PROVEN |
| smart loot / auto-pick | `STR_SETTING195/196/197/199/202` + `STR_SETTING201` (filter rules re-read every entry) | PROVEN settings |
| buffs/debuffs | `BuffList.lua`, `DebuffList.lua`, `TargetBuff.lua` | PROVEN files |
| team frames | `Teammate.lua`, `TeamBuff.lua`, `RaidPanel.lua` | PROVEN files |
| damage/heal/kill stats panel | `PVPShowPanel.lua`: `GetBattleFieldStatistics` + `PQ_STATISTICS_INDEX` (`INJURY`/`HARM_OUTPUT`/`TREAT_OUTPUT`) | PROVEN |
| kill feed / messages | `MainMessageLine.lua` (SYS_MSG) | PROVEN file, mode use inferred |
| streamer-mode banner | `STR_STORM3` 本场有主播玩家参与… | PROVEN label |
| observer button | `Btn_Observer` + `UpdateObserverButton` (`Minimap.ini`, `Minimap.decompiled.lua:635,3654,7452`) | OPEN (BR gating unknown) |
| mount/skill buttons from items | dynamic bar handles item skills (133 道具 scripts) | PARTIAL |

**Viewer state (2026-09-29):** every HUD window renders with its shipped art after
extracting the missing atlases from PakV4 (`RougeLike/NewRougeSkillBar`,
`PVPUI1/2/3/4/5/16`, `PVPWatch`, `SystemButton`, `Box`, `BlackMarket1`, `JYUi_06`,
`TeachingPanel7`, `TargetBg`, `Player`, `AssistNewbie`, `DesertStorm3`,
`RevivePanel`, `Target`, `TopMenu`, `RaidTotal`, `RoomPanel`, `RaidRelated`,
`Voice1`). The per-window `StringTable=` files the labels need were missing from
the local extraction and are now committed as UTF-8 copies under
`ui-process-app/Data/text/ui/Scheme/Case/`: `String_Comman.txt` (MiddleMap),
`string_PVP.txt` (PVPShowPanel), `string_Novice.txt` (RevivePanel/Teammate),
`String_RougeLike.txt` (DynamicBattleRoyale), `string_TeachingPanel.txt`.
State/layout replays: the minimap lens is parked at `Left=-200` in the INI and
`Minimap.UpdateAnchorCorner` (Minimap.decompiled.lua:1542-1770) puts the TOPRIGHT
layout at `Wnd_Corner (27,0)` / `Wnd_Minimap (0,32)` / `CheckBox_Switch (205,-2)`
(inventory `adjust`). The MiddleMap viewer state is a **composite**: the WorldMap
window is drawn *behind* it — the client calls
`WorldMap_ShowBehindMiddleMap(false, true)` (MiddleMap.decompiled.lua:22298,
WorldMap.decompiled.lua:8314-8356), which hides the WorldMap map scroll/list and
keeps its chrome — so the render takes the WorldMap top band (overlay `show`),
`Text_Title` 地图, the `Handle_TipsTitle` zone legend, `CheckBox_Footprint`
神行足迹 and `Wnd_SearchASetting` (`$Placeholder` 城镇或秘境; `WndEdit`
placeholders now render). On top, MiddleMap replays the translucent
`MapWindow3/4` bg tiles + `Handle_Map/Image_Map` with the **real map art** (the
map area is masked by `Handle_Border`'s `ShapTexture`
`ui/Image/UItimate/UIMask/MapMask.tga` with `AlphaShap=1` — a soft 100x80
rounded-rect alpha stretched over the 936x764 border, applied by the viewer as a
container `OpacityMask` so the art feathers into the glass like the capture): the
map window is the **battlefield map's M-map** (inventory `pages`): `MapList.tab`
rows 296/297/410/512/532 are the five `BATTLE_FIELD` maps — 龙门绝境,
龙门绝境·夜, 沧溟绝境, 白龙绝境, 天原绝境 — and each page takes its art from
that map's minimap pack (`龙门寻宝minimap_mb`, `龙门寻宝_夜晚minimap_mb`,
`海岛绝境minimap_mb`, `白龙绝境minimap_mb`, `天原绝境minimap_mb`, the same packs
the loading pages use) and its row label from
`Table_GetMiddleMap(id).MiddleMap0`. Each pack carries `config.ini`
(`[middlemap0] image=middlemap.png`, 1024x896, scale/startx/starty),
`middlemap.png` (2048x1792) and `area.tab`; `MiddleMap.UpdateMapPos`
(:9263-9320) fits the art preserving the config aspect into `Handle_Map`
(928x812 = 928/2048 = 0.453; the inset art matches the packs at corr 0.98+).
The left list is 世界 = `STRING_TITLE_WORLDMAP` plus the map row; the region row
stays hidden (every battlefield row has Region=0, `UpateRegionBtn` :11989-11995
returns before showing `Wnd_Region` — the 5.1 capture's 陇右 row is the
world-map state); `Wnd_SmallMaps` is adjusted to `(left 0, top 0)` — with the
region row hidden the runtime list stacks at the container top, right under the
世界 row (authored `(53,161)` is the parked prototype; left 6 left the map
crest/text ~6 px right of the row above; the engine's `FormatAllContentPos`
re-format itself is not reproduced, so the viewer pins the stacked position). `Image_MapLogo` is adjusted to top 4: the authored top
-6 draws the crest ~10 px above the capture, which centers it on
`Text_SmallMap` (box correlation +11 px before, +0 after; capture
`proof/minimap/screenshots/5.1 Example.png`, 2026-09-30 measurement — the
viewer's static item layout does not reproduce the runtime row placement). The
selected 龙门荒漠 row shows `Image_BgSelect` (`MapWindow6` frame 7 — its ink
RGB (93,131,103) matches the capture's bar (91,118,100), not the normal frame
33's (50,95,88)), so the viewer hides `Image_BgNormal`/`Image_BgOn` and places
`Image_BgSelect` at top 5 (authored -5 puts the bar's bottom edge 10 px above
the capture's — same runtime row-placement fit as the crest); the row highlight
bar now aligns at +3/+1 px like the rest of the list (strip corr 0.68→0.94). The battlefield packs ship no NPC filter data: all five
`minimap/npc.tab` are empty (0 bytes) and there is no `doodad.tab`, so
`UpdateNpcDoodad` (:5978-6043, `g_tMapNpcTitle`/`g_tMapDoodad` in
table_defs_dynamic.lua) appends nothing — the viewer replays no NPC rows (the
5.1 capture's ten rows 地图内交通 … 碑铭 and its 跨地图交通/其他商人 checks are
the *world* map 龙门荒漠's 33-row `npc.tab` and the player's saved StorageServer
`MiddleMap_SelectNpc` filter — world state, not the battlefield; the same
`Image_NpcOption` frame 0 checked / 4 unchecked mapping applies when a page
ever carries rows, :501-504). The trunk rows
(`Handle_NpcTrunk`/`Handle_CraftTrunk`, cloned from `Handle_Mode` by
`UpdateAreaOrNpcList` :5660-5705) are hidden on the battlefield pages — an empty
list shows no title, and the packs carry no rows (the 5.1 capture's world state
shows them because its 龙门荒漠 pack has rows). When a page carries rows the
trunk art is the `Image_ListBg1`/`Bg2` eye (`MapWindow6` frame 32 open / 35
closed; `UpdateAreaOrNpcTruckState` :4501-4592 swaps them with the expand
state), with the authored-visible `Image_ListCover` (frame 30 magnifier) and
`Image_Minimize` hidden (list-template `hide`). The NPC list's scrollbar
(`Scroll_List`) is hidden as well — the capture shows no scrollbar (its list
fits; ours is empty). `Image_Search` is rendered diced (`adjust imageType 10`): the
client draws the 20x20 `Common` frame 0 box art with 1 px borders although the
INI omits `ImageType`; the stretched render showed a 13 px left/right band (the
"extra dusted area"), the capture 1 px. Also replayed: the 标记设置
button and the scale/alpha sliders, plus the window placement and tab state the
capture shows: the MiddleMap window sits 27 px below the WorldMap band
(inventory `offsetY`; the WorldMap stays at the client top — the capture's 地图
tab underline is at y=113, the list/bottom bar/art all ~+27 vs the authored
positions, and a high-pass alignment against the capture now leaves ≤2 px
residuals on the list/tabs/search/title/rows near the window origin), the NPC
title row is 43 px tall
at runtime (inventory `adjust Handle_Mode`; the authored 54 is the editor
height — the capture's panel rows land at y=228.6/266.9, ours 229/267),
`CheckBox_MapPage` is checked (`images.checked`; the capture paints
the selected frame and the script calls `Check(true)` :20817/20942) and
`CheckBox_ExplorePage` is hidden (the script hides it when
`GetMapExploreInfo(mapID)` is empty :3920-3958; the capture shows no 探索 tab).
Measurement note (2026-09-30): the capture's MiddleMap content is ~0.64 % (x) /
0.72 % (y) larger than the design-size render — a least-squares fit over 16
feature patches gives `dx = 0.0064·x + 2.4`, `dy = 0.0072·y − 0.7` (the
screenshot's UI scale is ≈1.8145, not the 1.8021 used in earlier passes), so
residuals grow with x/y (≈ +3 px at the left list, +10 px at the right panel);
per-element checks must correct for the scale before calling an offset real.
`WndContainer_GFInfo` (GF counters + heat
toolbar) is hidden: `UpdateHeatMapState` hides it when `CanShowHeatMap()` is
false (:25045-25081) and the reference capture shows no toolbar. Only the parked
data-marker layers (person/teammate, born/treasure/event marks, draw/storm lines,
area name, traffic, quest/NPC marks), the command-mode/quest-filter subtrees and
the script-locked widgets (`Wnd_CommandMap`, `CheckBox_QuestPage`, `Btn_Close`,
`WndContainer_HeatMapDetail`) stay hidden (2026-09-29). The viewer supports
table-driven labels/lists (`TextOverride.Table*`, `ListTemplate.RowSources` over
the UTF-8 table copies in `ui-process-app/Data/table/`). Render sheet:
`proof/ui/evidence/hud_windows_render.png`. Audit after the pass: 15
placeholders — all authored `Image no Image` elements filled at runtime from
player/engine data (`Image_Map`, `Image_School`, `Image_NPCMark`,
`Image_innerPower`, `Image_inSchool*`) — and 0 unresolved ids. Open: the
Teammate slot stack (five `PosType=10` frames at one spot; the stacking rule is
not decoded yet).

## 7. Death / revive / settlement / exit

| UI | evidence | status |
|---|---|---|
| 濒危 / 受重伤 | `STR_NEAR_DEATH` 濒危 (`string_PVPAcount.txt`), `STR_BF_DEAD` 受重伤 | PROVEN labels |
| revive | `STR_REVIVE_NO` 复活点复活 (`NewSkillPanel.ini` — generic skill panel consumer); C→S `DoPlayerReviveRequest` 0xB9; 冰封复活 31507 | PARTIAL |
| settlement panel | `PVPShowFinal.{ini,lua}` + `PVPShowFinalL/R.ini`: registers `BATTLE_FIELD_SYNC_STATISTICS`, rows Name/Kill/Damage/Health/NearDeath, opens with `ApplyBattleFieldStatistics` | PROVEN |
| settlement labels | `STR_JIESUAN_TITLE` 结算, `STR_SHOW2` 个人评分结算, `STR_BF_*` (击伤/协助击伤/伤害量/治疗量/威望/奖励金钱/经验), `STR_FBLIST_DUIYUANMINGZI` | PROVEN |
| settlement art | `UI_黑山结算_折戟沉沙.pss` | PROVEN asset |
| leave | `STR_BATTLEFIELD_MENU_LEAVE`, `STR_BATTLEFIELD_LEAVE_QUEUE`, `LEAVE_BATTLE_FIELD` events | PROVEN |
| rewards / rank recap | `STR_BF_REWARD/MONEY/EXP/PRESTIGE`, `MultipleRankPoint*`, `DoGetBFRankRequest` | PROVEN labels/handlers |

## 8. Missing renderers (the hunt list)

1. **Mode HUD window** (storm countdown + remaining players + storm state). Not in the
   144-file dictionary corpus; not in `JX3UIX64.dll` string scan; no extracted Lua
   references `STR_TIMEDESERT`/`STR_LEFTPEELE`. Candidates: a PakV4 `ui/Config/...` Lua
   panel whose path was never extracted, or a native/Compose window.
2. **Ready prompt** — solved: the queue pop uses the generic MessageBox
   (`MB_entermap`, §3); `STR_PVP_Ready` (准备好了) is a distinct label with no
   extracted consumer yet.
3. **Observer/spectate UI** (BR-specific or arena-only).
4. **Death/revive overlay** for the BR mode.
5. **Airdrop / kill-feed specifics** (`KMapMark` types exist; the labels are not located).

## 9. Research plan to close §8

1. **Enumerate PakV4 `ui/`** — the dictionary extraction (custom.dat window names)
   missed at least the mode HUD. Options: (a) brute-force candidate paths via
   `tools/netcode/extract_pak_paths.py` (name patterns: `DesertStorm*`, `BattleRoyale*`,
   `BattleField*`, `FightingNum/Statistic`, `Storm*`, `Treasure*`, plus
   `ui/Config/BattleField/*`); (b) restore the file-list export
   (`KG_PAKFS_CollectAllFileNames`) in the engine host; (c) use the CDN `.hpkg` index.
2. **Decompile the remaining mode-relevant panels** once a working unluac exists
   (`DynamicBattleRoyale` variants, `FightingStatistic`, `FightingNum`, `LootList`,
   `MainMessageLine`, `BuffList`, `NewSkillPanel`).
3. **Cross-check native windows**: scan `JX3UIX64.dll` / `KGUICocosX64.dll` string
   tables for `STR_*` ids (first pass on `JX3UIX64.dll` was negative).
4. **Verify activation triggers** via a capture: the phase clock (`0x11B`) and objective
   updates (`0x11A`) can be aligned with HUD visibility/state changes; `0x0C/0x10/0x82`
   with the loot window; `0x330` with the stats panel.
5. **Label the objective array** (which of the two 8-slot arrays drives the storm
   countdown/center/radius) — one captured match.

Linked: `JX3_MODE_UI_FLOW.md`, `JX3_MODE_MATCH_LIFECYCLE.md` §7/§8,
`MAP_MINIMAP_RESEARCH.md` (UI corpus inventory), `JX3_MODE_EDGE_SYSTEMS.md`.


## 10. KGUI renderer semantics (ui-process-app findings, 2026-09)

These were validated against the shipped data while building the WPF renderer:

1. **No suffix collapsing.** Every INI section name is unique and is a real
   element. The client Lua looks elements up by exact name plus a page suffix
   (e.g. `"Btn_SingleQueue" .. szSuffix` with `""`/`_B`/`_K`/`_T` for
   DesertStorm/Battlefield/Skill/Takov). Numeric suffixes (`_0`, `_0_0`) are
   per-slot variants (e.g. three separate leave buttons), **not** re-issues.
   Collapsing them drops real elements (storm lines, team slots, leave labels).
2. **Page filter = ancestor chain.** An element belongs to the page of the
   nearest `Page_*` ancestor; elements without one (window chrome, mode tabs)
   are shared. Render one page by keeping the selected page's ancestor chain
   plus page-less sections.
3. **Text overflow.** The engine draws glyphs past the authored `Width`
   (centered labels like `Text_SingleT_0`, Width=46, text 4 chars). A fixed
   WPF `Width` clips them, so overflowing `HAlign=1` labels are hosted in a
   sized canvas; tips/marquees keep their box.
4. **Rich-text tokens.** String values carry inline markup: leading `<D0>`
   style tags, inline icon ids (`本周还可获得<1010>`), `<image>` wrappers.
   Strip `<...>` before display.
5. **Encoding.** PakV4 text assets are GBK: `PVPShowFinal.ini`,
   `PVPShowFinalL/R.ini`, `string_PVPAcount.txt` (settlement) are not UTF-8.
   `Engine/TextFile.cs` decodes UTF-8 strictly and falls back to GBK (cp936).
6. **Nine-slice (ImageType=10)** needs all four detected borders > 0 with
   `l+r < frameW`, `t+b < frameH`; otherwise fall back to a plain stretched
   image (tiny frames crashed `CroppedBitmap`).
7. **Queue panel static state.** `Page_DesertStorm` shows three crest buttons
   (`Btn_SingleQueue` / `Btn_TeamQueue` / `Btn_RoomQueue`) with mode labels
   `Text_SingleT_0` / `Text_TeamT_0` / `Text_Room`. The idle state (panel just
   opened, not queued) hides the in-queue overlays: matching state
   (`Handle_Mathing_*`), 中止匹配 buttons (`Btn_Leave*Queue`, shown only while
   `IsInBattleFieldQueue` reports that queue), weekly reward rows
   (`*WeiWang*`, `*ZhanJie*`, `*ZHF*`), the 飞沙令 bottom bar (`Handle_FEISHA`),
   the tips (`Text_TIPWARNING1`, `Text_Escape`) and the 战场规则 label
   (`Handle_Rull__DS`) — all `LockShowAndHide=1` state/widget sections that the
   engine hides by default and the script shows as needed (see §10.15; the
   old hand-curated `hide` globs were replaced by that rule).
8. **One mode at a time (default page).** The client keeps one selected
   battlefield (`NewBattleFieldQueue.nSelID`) and looks every per-mode element
   up as `name .. szSuffix` (`""`/`_B`/`_Z`/`_Bomb`/`_M`/`_PleasantGoat`/`_S`/
   `_K`/`_T`; see `m_WndBattleField` in the decompiled Lua). A render without a
   page must therefore pick the window's default page (inventory `page`,
   `Page_DesertStorm` for the queue) instead of stacking all pages.
   The mode tab strip is not authored as a strip either: `ShowModeTabs`
   (lua:4374-4451) hides every tab, shows only the battlefield ids bound to the
   entry point, and pins them with `SetRelX(18 + index * 100)`. The entry point
   is an NPC template (`tOpenQueueNpcID`, lua:54-73) — the main queue NPC 59149
   binds 296/645/676/709/9999, the minimap button passes 5097 which falls back
   to `{52}` (`GetBattleFieldMapID`), and `InitWndBattleField` (lua:4713-4744)
   removes activity-gated modes when `IsActivityOn(nActivityID)` is false.
   The inventory `tabs` field encodes the chosen entry point's list + geometry.
9. **List item prototypes are cloned from other INIs.** A `HandleType=3`
   container never draws its authored children: `AppendItemFromIni(iniPath,
   "Handle_Player", ...)` clones the prototype into the list at runtime
   (`PVPShowFinal.UpdateOneSideList`). The parked prototypes sit at
   `PosType=8` off the window edge, so a static render must hide them
   (inventory `hide: Handle_Player*`) and may inject the authored row layout
   via inventory `lists` (container + prototype INI + item name).
10. **Shared windows render one scene's composition.** `MiddleMap` holds
    `Wnd_NormalMap` (world map: quest/NPC search/region list), `Wnd_CommandMap`
    (war-sandbox: mark panel, painting board, command buttons), explore panels
    and the battlefield layers (storm line, camp counts, `WndContainer_GFInfo`
    + `WndContainer_HeatMapDetail`, gated by `CanShowHeatMap()`). The 绝境战场
    default keeps only the battlefield composition. For the settlement,
    `PVPShowFinal.InitPanel` hides `Image_ScoreL/R` unconditionally and shows
    the arena title (`Image_Title_Master`) only for its own `nMode`; the
    battlefield state hides `Handle_Title` + score art.
11. **Two skins live in the same INI (琉璃·境).** The 2024-12-23 flagship UI
    update "更换界面皮肤而不改变现有布局": the INIs now carry the old chrome
    (`ui/Image/UICommon`, `ui/Image/UITga`) and the 琉璃·境 skin
    (`ui/Image/UItimate`, `ImageType=16` glass masks) side by side
    (e.g. `Handle_Bg` vs `Handle_Bg_960x624`, `Image_BgPopUp` vs
    `Image_BgPopUp_Glassmorphism`, `Image_Bg1` vs `Image_BgPopUp`). The live
    client shows the UItimate skin; the renderer does the same by default
    (inventory `skin`, default `uitimate`): `ApplySkin` drops old-art chrome
    whose box is covered by a same-area UItimate element, guarded so runtime
    marker/event layers are never matched. `ImageType=16` frames are glass
    masks (PanelBg frame 6 = solid fill, frame 4 = soft-edged popup); WPF has
    no backdrop blur offscreen, so they render as the 琉璃 panel tone
    (`#2E3B49`) masked by the frame's alpha.
12. **Handles lay their items out at runtime (`FormatAllItemPos`).** The Lua
    calls `handle:FormatAllItemPos()` after filling texts; the engine's
    `KItemHandle::FormatAllItemPosByAutoNewLine` (KGUIX64 `0x180107e40`)
    flows items left-to-right and wraps at the handle width; the code checks
    `HandleType == 6`, and `HandleType == 3` lists behave the same. The
    renderer flows both: items in INI order, wrap at the container width, then
    HAlign (0/1/2) aligns each row and VAlign the item in its row; `Alpha=0`
    alternatives are skipped. Item sizes come from the authored W/H, else the
    frame's pixel size (images) or the measured text (labels) — never the
    parent's size, or rows wrap wrongly (MapQueue's `[浩气盟]太原(3568)`
    fragments, the queue's 飞沙令 number).
13. **PosType 7/9 anchor to the previous sibling, 8/11 to the window.**
    Validated on the 飞沙令 row and the MiddleMap bottom bar: PosType 7 places
    the element at the previous sibling's right edge (same Top) — e.g.
    `Text_WeekNum` after `Text_WeekNumT`, `Text_ScalePer` after `Text_Scale`
    (484,773) — falling back to the parent's right edge when there is no
    sibling; PosType 9 does the same for flow items (`Image_Rule_4` after the
    score). PosType 8/11 are window-relative and only apply when no Left/Top
    is authored (the settlement prototypes park at `windowWidth - width`);
    when authored they keep their offset (`Text_PersonalScore` at Left=65).
    `Text_AllNum_0` is a duplicate of `Text_AllNum` for another layout and is
    hidden, not the real value.
14. **Runtime anchors (`SetPoint`) come from the Lua.** The engine anchor API
    `handle:SetPoint(selfSide, sx, sy, parentSide, x, y)` aligns a point on the
    element to a point on the parent plus an offset. The Lua calls it after
    creation (e.g. `PVPShowPanel.UpdatePos`, lua:155-183, anchors
    `Wnd_HPListL` → LEFTCENTER y+10, `Wnd_HPListR` → RIGHTCENTER y+10,
    `Wnd_PlayerInfo` → BOTTOMCENTER; also DynamicBattleRoyale/NewBattleFieldQueue
    `UpdatePos` center the window on UI_SCALED). The renderer replays them via
    the inventory `anchors` field, which writes the same `AnchorArgs` the INI
    uses (`parentSide,selfSide,dx,dy`). Without it, `Wnd_PlayerInfo` (authored
    Top=843 in an 884-tall window) and its texts rendered below the window.
    Runtime-resized panels (BattleFieldMap `SetSize`/`SetRelPos` per row,
    PVPShowPanel list rows) remain approximations.
15. **`LockShowAndHide` decides the default visibility.** The KGUI decoder
    (`UI::DecodeFun<KItemHandleData,KItemHandle>::CreateItem`, KGUIX64
    `0x1800cc700`) treats sections with `LockShowAndHide=1` specially: it
    clears the item's visible flag at creation and runs the show/hide
    bookkeeping, so the page logic never auto-shows them — only the window
    script does (`ShowModeTabs` for the tab checkboxes, lua:4374-4451;
    `UpdateAnniversaryTabIcon` for the activity badges, lua:6506-6553;
    `UpdateButtonState`/`UpdateFeiSha...` for the queue-state widgets).
    The live client screenshot of the idle queue window
    (`proof/minimap/screenshots/Screenshot-given-1.png`) confirms it: every
    `LockShowAndHide=1` section is hidden except the script-shown ones (tabs,
    badges), while `LockShowAndHide=0` sections are visible (随机地图 block,
    个人评分 + its tip, the three crest buttons, 飞沙令, 技能平衡, 单场奖励,
    bottom buttons). The renderer implements this as `ApplyLockedVisibility`
    (drops LSH=1 sections, keeps the tab list + the inventory `show` list);
    the old hand-curated `hide` globs for those widgets are gone.
16. **UITex frame groups (buttons/checkboxes).** Atlas layout: 92-byte header
    (magic `UI`, texW, texH, frameCount, **groupCount at offset 16**, texture
    name[64] at 24), then `frameCount` 20-byte frame records, then `groupCount`
    group records: `u32 frameCount` (0 = empty group, 4 bytes only), else
    `u32 startFrame`, `u32 intervalMs` and, for multi-frame groups,
    `frameCount-1` extra frame indices. Buttons/checkboxes pick their frame by
    *group id* (`NormalGroup`/`MouseOverGroup`/`MouseDownGroup`/`DisableGroup`;
    checkboxes use `UnCheckAndEnable`/`CheckAndEnable`/`Checking`/...), and the
    ids are local to the atlas: `Button.UITex` group 96 = frame 92 (the "?"
    help icon), group 97/98/99 = frames 93/94/95 (over/down/disable). An
    earlier parser mis-read the table (it broke on the first empty group and
    then guessed `group + firstStart`), which drew the wrong frame for every
    button whose group fell outside the broken table — e.g. the 战场规则 `?`
    button (group 96) rendered as a solid teal fill. The `Frame` key is only
    the fallback used when the group is empty/out of range (engine
    `0x18011f0f0` bails and leaves the item's frame alone). The old
    `CheckBox_MapPage` frame override hack is gone with the fix
    (`MapWindow6.UITex` group 34/33/87 = frames 39/4/38 = unchecked/checked/
    hover tab art).
17. **Script-set labels come from the window's own string table.** The mode
    tab handlers set `Handle_Total/Text_SkillTitle` per mode (lua:4453-4573;
    五人模式 → `STR_DESERTSTORM_TITLE`) and `UpdateOpenTime` sets
    `Text_TIP_DSTime` (lua:4350-4372; `STR_DSOPEN_TIME`/`STR_DSOPEN_WEEKTIME`).
    Those ids live in the window's declared `StringTable=
    ui\Scheme\Case\string_ArenaCorpsPanel.txt` (NewBattleFieldQueue.ini), which
    the local extraction lacked — extracting it from PakV4 resolves 绝境战场,
    每日12:00至次日凌晨1:00开放, 个人评分, 单场奖励, 随机地图, 技能平衡,
    绝境殊影/排名信息/快捷组队 etc. The app loads it through the committed
    UTF-8 copy `ui-process-app/Data/text/ui/Scheme/Case/string_ArenaCorpsPanel.txt`
    (`Engine/Paths.cs` merges `Data/text/ui/Scheme/Case/*.txt` after the local
    extraction, first id wins). `Engine/Strings.cs` keeps its aliases only as a
    fallback for snapshots without that table. The inventory `texts` field
    replays the Lua's `SetText` calls onto the static render.
18. **Live reference screenshot.** `proof/minimap/screenshots/Screenshot-given-1.png`
    is the live client's 五人模式 idle queue window. Besides the visibility
    rules above it pins: the 4-tab strip (乱武模式 676 dropped by
    `InitWndBattleField`'s `IsActivityOn` gate), the 赛季 badges on
    五人模式/单人模式/寻宝模式 (`Image_AnniversaryIcon1/2/3`, shown by
    `UpdateAnniversaryTabIcon` while the activity benefit is on), the
    个人评分 sub-line `Text_ZombieScoreTips_0` (评分越高，奖励越多), the `?`
    help button at the 随机地图 row (`Btn_Rull_DS`; its label `Handle_Rull__DS`
    is LSH=1 and hidden), `WndContainer_Buff` (技能平衡) visible only because
    the player has the treasure-balance buff (`UpdateBalanceBuff`, lua:4746),
    and the translucent 琉璃 glass background over the world (the offscreen
    renderer draws the same tone opaquely).
19. **Window geometry is script-driven in places.** `MapQueue.UpdateListSize`
    (lua:667-745) resizes the list handle/background/checkbox from the row
    count and then `Collapse` (lua:330-376) sets the window to
    `32 + rows*48 + 32` (112 for one row) and shows `Wnd_List`; the inventory
    `adjust` list replays those `SetSize`/`SetRelPos` calls for the state the
    static render shows (MapQueue is 240x112 with the row and 自动进入 checkbox
    inside). Elsewhere the authored sizes overhang the window on purpose: the
    client does not clip window children, so `RevivePanel`'s kneeling icon
    (Top=-24), `PVPShowPanel`'s title (Top=-12) and the settlement logos
    (Top=-2) are visible in game. The renderer grows the canvas by the
    overhang of visible art/text elements (clamped to 48px so parked
    off-window elements stay excluded) and offsets the root accordingly.
20. **Missing live assets can be re-extracted from PakV4.** Several atlases
    referenced by the INIs were absent from the extraction: `PVPSetting2` and
    `StormLine3` ship as single-mip DXT5 `.dds` (`Engine/Dds.cs` decodes
    BC1/BC3; `TextureLoader` dispatches TGA/DDS), `PVPShowFinal.UITex` +
    `.Tga`, `LinkLine.tga`/`LinkLine200.tga` (an `Image` pointing straight at
    a texture file loads it as frame 0) and the per-window string tables
    (e.g. `ui/Scheme/Case/string_LoadingPanel.txt` for STR_PAKV4_TIP1/2).
    `UiProcessApp --audit` renders every inventory window and reports what is
    still missing: currently 17 placeholders, 0 unresolved ids. The remaining
    placeholders are legitimate gaps — `Image_Map` is painted by the engine's
    map renderer (no UI atlas), `Image_School`/`Image_NPCMark`/
    `Image_innerPower` are filled at runtime from player data, and Minimap's
    slide-out panels are authored parked at negative X until the module
    animates them in.
21. **State-driven art keys the renderer honours.** `CheckedWhenCreate=1` makes a
    checkbox paint its `CheckAndEnable` frame group instead of the unchecked one:
    in `Button4.UITex` group 14 (unchecked) is a 30x30 *empty* frame while group
    20 (checked) is the 124x30 tab plate. The live queue window shows exactly
    that — 五人模式/单人模式/寻宝模式 are created checked (plates), 自定义模式 is
    not — and the reference screenshot confirms it. The renderer also checks the
    tab of the rendered page when it was authored unchecked (radio tabs check on
    click), and activity-gated tabs (`tabs.gated`, e.g. 乱武模式 676) only get a
    slot in the strip when the rendered page *is* that tab — which is why the
    五人模式 render keeps four tabs while the 乱武模式 render shows five with the
    寻宝/自定义 tabs shifted to slots 3/4 (ShowModeTabs advances the index only
    for shown tabs). `FontColor=<color.txt name>` overrides the font scheme's
    fill (the engine decoder reads it as a string); `Title_EveryWin_Reward_1` is
    white in scheme 18 but `yellow2` in the INI — the live client's 单场奖励
    label is yellow. `ImagePercent` scales the element's opacity (the
    随机地图/单场奖励 flourishes are 0.35, old-skin chrome 1.0).
22. **技能预览 (DesertWeaponSkill) is not in the local client's pak index.**
    `Btn_DesertStormSkills` opens the `DesertWeaponSkill` module (title
    `STR_SKILLSINTRODYCTION` 绝境武学), but neither `ui/Config/Default/
    DesertWeaponSkill.ini|.lua` nor any plausible path resolves through
    PakV4SfxExtract on this install (the surrounding modules and string tables
    do). Rendering that panel needs its INI + Lua from a full client (or a
    downloaded r2d bundle).
23. **Labels use the shipped fonts, and VAlign centres the font's line box.**
    `fontlist.ini` maps each scheme's `FontID` to a font file
    (`\UI\Font\fzht_GBK.ttf` 方正黑体, `fzxk.ttf` 行楷, `fzjz.ttf` 剪纸,
    `FangZhengKaiTi-GBK.ttf` 楷体; the files ship loose in the client's
    `ui/font`). The renderer now loads them per scheme (WPF `FontFamily` from
    the file with the font's internal family name, e.g. `FZHei-B01`) instead of
    Microsoft YaHei UI — that fixes both the letterforms and the ~1.5-2px
    vertical offset every label had. The text decoder stores `VAlign` at
    item+0x5c4 and the engine centres the font's ascent+descent line box in the
    item, so CJK ink lands slightly *above* the box centre (the descent hangs
    below); pinning WPF's `LineHeight` to the box height instead drags the ink
    ~3px down. The renderer now positions the measured block in a canvas host
    (`offsetY=(boxH-textH)/2`, HAlign offsets from the measured width). Verified
    against the live screenshot: title, tabs, 个人评分, 随机地图, 飞沙令 and
    单场奖励 all land within ~1.5px; the bottom button labels differ only by the
    screenshot's window-size/scale offset (the live window is ~975x624, not the
    authored 960x624, so its absolute pixel positions are ~1.5% larger).
24. **Lua-formatted labels are replayed through the inventory `texts`.** The
    飞沙令 row's second line is `FormatString(STR_GAME_GUIDE_WEEK_REMAIN, n)`
    (NewBattleFieldQueue lua), which reads `(本周还可获得：N)` in the live
    client; the INI's authored `STR_WEIMINGDIAN_GET` renders as
    `本周还可获得<1010>` (inline icon) before the script runs. The inventory
    overrides `Text_FeiShaLingAvailable[_K/_S/_T]` with the formatted sample so
    the static render matches the reference; it also overrides the
    player-data labels `Text_PersonalScore` (1873) and `Text_FeiShaLing`
    (10000/10000, `UpdatePersonalScore`/`UpdateFeiShaWandNumber` read live role
    data) with the reference screenshot's values.
25. **Runtime image swaps are replayed through the inventory `images`.** The tab
    badges (`Image_AnniversaryIcon1/2/3`) are `LockShowAndHide=1` and
    `UpdateAnniversaryTabIcon` (lua:6506-6553) sets their atlas+frame from
    `ActivityBenefitMgr` data; the INI default is frame 21 of
    `PartnerTeam.UITex` (周年), while the reference screenshot shows the 赛季
    art (frame 23, verified by dumping the atlas frames). The queue inventory
    shows the three ids and overrides their frame to 23 (`ApplyImages`), which
    is what the reference state pins.
26. **Runtime message bodies are appended, and anchors honour `AnchorDst`.**
    The MessageBox module never draws its body from the INI: it calls
    `handleMsg:AppendItemFromString(text, 18)` and fills only the option labels.
    The inventory `appends` field injects the authored text into the list handle
    (`ApplyAppends`, optional `top` spacer because list items ignore authored
    offsets), and the option texts come from `texts`. `AnchorArgs` sections that
    also carry `AnchorDst` (e.g. the MessageBox ornaments →
    `../Image_Bg`, `Btn_Close` → `root`) align against that target's rect, not
    the direct parent's; the renderer resolves the named section when its
    absolute rect is already known and otherwise keeps the parent fallback.
27. **The MessageBox sizes to its content.** `MessageBox.lua` (lua:829-842)
    sets `handleMsg` width to the body text extent, the option row to
    `max(option button width * n + 40 + (n-1)*10, body)`, Wnd_All to the flex
    content (+20 height via `StretchAnchorArgs`) and Image_Bg to the flex content
    +56 (the 28px overhang on each side of the window). For the 2-option queue
    prompt (`确定(30)` / `取消`, body 你要传送到"龙门绝境"地图吗？ = 205px):
    content 226x83 → window 226x103, panel 282x103, buttons at x=20/118 (10px
    gap), `Btn_Close` and `CheckBox_Msg` hidden (`bShowClose`/`tCheckBoxConfig`
    unset). The inventory replays those values in `adjust`
    (`ready-confirm`); the corrected render is
    `proof/ui/evidence/ready_confirm_render.png` (2026-09-29).
