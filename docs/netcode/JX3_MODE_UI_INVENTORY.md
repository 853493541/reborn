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
backgrounds); progress handshake `ComfirmSyncMapProgress`; per-map loading art extracted
for 296/297/410/512/532/645/709 (`proof/netcode/mode_ui/loading/*`, `JX3_MODE_UI_FLOW.md` §4).

## 5. Staging (safe zone + countdown)

| item | evidence | status |
|---|---|---|
| countdown frame art `UI_黑山绝境_倒计时通用底框.pss` | editor asset (`editor_assets_juejing.txt:72-77`) | PROVEN asset |
| countdown value source | `OnSyncBFPQInfo` 0x11B → absolute end time → `BattleFieldMap.UpdateTime` h:m:s | PROVEN data path |
| countdown labels | `STR_PQ_PROTIME` 阶段倒计时： and `STR_PQ_TIMETITLE` 挑战倒计时： (PQ module; mode use inferred), `STR_MAP_QUEUE` 排队中 | PARTIAL (generic PQ labels) |
| safe-zone state | `STR_SAFEZONE` 安全区, `STR_HATREDPANEL_SAFEZONE` 安全区域 | PROVEN labels |
| staging pose | `龙门绝境_站姿01..05` animation set | PROVEN asset |
| mode HUD activation | minimap + main bar + BR skill bar appear after load | PARTIAL (activation trigger not proven) |

## 6. In-match HUD

| UI | evidence | status |
|---|---|---|
| BR dynamic skill bar | `DynamicBattleRoyale.{ini,lua}` (default + dynamic boxes, hotkeys) | PROVEN |
| Minimap + BattleField button | `Minimap.lua`, `Btn_BattleField`, `Minimap.ini` | PROVEN |
| storm circle on minimap | `STR_SETTING210` 小地图增加风暴圈范围提示; `STR_SETTING262` 小地图安全区外警告闪烁及距离提示 | PROVEN labels |
| battlefield map panel (M) | `BattleField/BattleFieldMap.{ini,lua}`: `Handle_StormLine`, `MapCircle`, `SFX_CircleNew`, heat map, mark/gain/sfx notify events | PROVEN renderer |
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
