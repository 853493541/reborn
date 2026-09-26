# System tables (scheme / fonts / modes / animation / states)

**Confidence:** VERIFIED values from PakV4 extraction with
`tools/netcode/extract_pak_paths.py` (official `PakV4SfxExtract.exe`).
Extracted copies: `../scheme/` (raw, GBK for most) and
`../scheme_utf8/*.utf8.txt` (UTF-8 conversions).

## Path table — the authoritative list

`proof/movement/extracted/ui_filepath.txt` (game file `ui/filepath.txt`) names
every system table. The important ones:

| key | game path | state |
|---|---|---|
| `ModuleInfo` | `\ui\module_info.xml` | have |
| `UIConfig` | `\UI\Scheme\Elem\uiconfig.ini` | extracted (identical to the repo copy) |
| `FontList` | `\UI\Scheme\Elem\fontlist.ini` | extracted |
| `SchemeElemFont` | `\UI\Scheme\Elem\font.ini` | extracted (4,203 lines, 421 schemes) |
| `FontPathList` | `\UI\Scheme\Elem\fontpathlist.ini` | extracted |
| `SchemeElemColor` | `\UI\Scheme\Elem\color.txt` | extracted (106 named colors) |
| `SchemeElemNumber` | `\UI\Scheme\Elem\number.txt` | extracted (Chinese numeral words) |
| `SchemeElemCodePage` | `\UI\Scheme\Elem\codepage.txt` | extracted (936=zh_CN, 950, 1258, 65001) |
| `DrawMgr` | `\UI\Scheme\Elem\drawmgr.ini` | extracted (layer order) |
| `UIAnimation` | `\UI\Scheme\Elem\uianimation.txt` | extracted (42 ids → XML) |
| `ShowModeInfos` | `\UI\Scheme\Case\showmode.txt` | extracted (mode enum 0–38) |
| `IconImageTable` | `\UI\Scheme\Case\icon.txt` | extracted (28,202 icons) |
| `CursorList` | `\UI\Config\jx3_cursor.ini` | extracted |
| `PickDropBoxSetting` | `\UI\Config\jx3_box.xml` | extracted (Box state art) |
| `GlobalStringValuable` | `\UI\Scheme\Case\String.txt` | have (`proof/minimap/ui/Scheme/Case/string.txt`) |
| `FrameList` | `\UI\Config\framellist.ini` | **MISS in this build** |
| `FrameSetting` | `\UI\Config\framesetting.txt` | **MISS in this build** |
| `MouseHover` | `\UI\Config\jx3_mousehover.ini` | **MISS in this build** |
| `PopupMenu` | `\UI\Config\jx3_popupmenu.ini` | **MISS in this build** |
| `PickDropBox` | `\UI\Config\jx3_pickdropbox.ini` | **MISS in this build** |
| `DefaultShapTexture` | `\UI\Image\Minimap\Sharp.dds` | referenced default |

Extraction rules that matter (discovered the hard way): PakV4 lookup is
case-insensitive but **must not have a leading backslash**; a failed path is a
silent MISS. `ui/**` is PakV4-only (the CDN index has no `ui/*` records).

## `showmode.txt` — `ShowModeID` enum (VERIFIED, complete)

0 Default, 1 DesertStormOB, 2 ZombieFightFinal, 3 PlotDialoguePanel,
4 CampOB, 5 MobaOB, 6 Cloud, 7 Selfie, 8 Chapters, 9 VideoSettingPanel,
10 UIMovie, 11 CoinShop, 12 HomelandBuilding, 13 HomelandPortalPanel,
14 Partner, 15 HeroMorph, 16 Interlude, 17 GuildLeagueOB,
18 PendantCollection, 19 PersonalCard, 20 Instrument, 21 ShareStation,
22 NewYear, 23 ExcellentCard, 24 FullScreenShop, 25 MovieRecord,
26 OperationCenter, 27 StoryMode, 28 DungeonOB, 29 SamplingScreenShot,
30 MonoPolyMain, 31 MonoPolyClearObstacles, 32 MonoPolyGames,
33 MonoPolyFinal, 34 FilterMask, 35 ArenaFinal, 36 GooseDuckKill,
37 GooseDuckKillMeeting, 38 GooseDuckKillFinal.

(Chinese descriptions exist in the file; IDs/names are authoritative.)

## Fonts (VERIFIED)

- `fontpathlist.ini` — 3 font families:
  `fzht_GBK.ttf`, `fzxk.ttf`, `fzjz.ttf` (plus `FangZhengKaiTi-GBK.ttf`,
  `MSJH.TTF` used by `fontlist.ini`, all loose in `<game>\ui\Font\`).
- `fontlist.ini` — per-FontID render parameters:
  `IsMipmap, IsAntiAlias, Size, Border, Vertical, Projection, Dpi, Name,
  File` (+ `Chat=` for the chat-only face). E.g. `[0]` = size 16, Dpi 86,
  fzht_GBK; `[3]` = size 14; `[5]` = chat font.
- `font.ini` — 421 named FontSchemes. Fields:
  `Name, FontID, Color, Size, BorderColor, BorderSize, ProjectionColor,
  ProjectionSize`. The dominant UI scheme is `[212]`:
  FontID=3, Color=white, Size=14, BorderColor=gray6, BorderSize=0,
  ProjectionColor=gray7, ProjectionSize=1.
- `color.txt` — 106 named colors `name r g b` referenced by
  `Color/BorderColor/ProjectionColor` (e.g. `white 240 240 240`,
  `gray6 30 30 30`, `yellow2 240 240 0`).
- Text colors can also come from `FontColor` on the control.

## Layers / draw order (VERIFIED structure)

`drawmgr.ini` defines the layer system:
`Lowest, Lowest1, Lowest2, Normal, Normal1, Normal2, Topmost, Topmost1,
Topmost2` with `[LayerEnable]=1` and per-layer `ModuleCount/ModuleN` lists.
In this build only `[Lowest] → ActionBar` is mapped (`Frame0..3` =
`ActionBar1..4`); the layer names are what `._Parent=Normal`/`Lowest`
reference in layouts.

## UI animations (VERIFIED table + schema)

- `uianimation.txt` maps 42 ids to `\ui\Animation\*.xml`
  (id 0 is empty; e.g. 1=line, 2=bcurve, 3=shake, 8=panelopen,
  14=zoombsb, 41=PVP set).
- All 41 XMLs extracted to `../animation/`. Schema (from `line.xml`):
  ```xml
  <Animation name totaltime>
    <Layer totaltime offsettime>
      <Image file frame w h onlyeditor/>
      <KeyFrames/>
      <State type totaltime>
        <Point type="Pos|Line" inherit x y [velocity accelertion]/>
        <PosPercent perx pery/>
      </State>
    </Layer>
  </Animation>
  ```
  `State type 0` = start, `type 2` = end for a line motion; `KeyFrames`
  enables keyframe curves. `onlyeditor` images are placeholders.
- TweenNew shows the binding convention: a tween file `<Name>_AniBind.ini`
  next to the tween (`TweenNew.decompiled.lua`), i.e. `*_AniBind.ini` is a
  resource class to look for.
- Layout tween properties exist both inline (`PosFromX/PosToX`,
  `ScaleFrom*/To*`, `AlphaFrom/To`, curves) and in
  `ui/Animation/{Middlemap,WorldMap,DaTangJiaYuan}_Ani.ini` (StepCount/StepN
  blocks; see `proof/minimap/ui/Animation/`).

## Item-box state art (VERIFIED)

`jx3_box.xml` maps logical states to atlas art for item cells, both normal
`<box>` and `<CicleBox>`:
`CoolDown, Sparking, InUse, Staring, Selected, UnEquipable, Disable,
NoObjectMouseOver, MouseOver, Pressed` → `image + frame + animation`.
Example: `box/MouseOver` = `UI\Image\Common\Box.UITex` frame 3;
`box/Selected` frame 7; `CicleBox/CoolDown` frame 99.

## Icon table (VERIFIED format)

`icon.txt` TSV header:
`ID FileName Frame Kind SubKind Tag1 Tag2 FileName_Large MobileFileName MobileBigImg`.
28,202 rows; `ID` is what `Box.IconID` and icon APIs resolve. Example row:
`0  System\quest\QuestPuzzle10.UITex  0  系统  ...`.

## Misc

- `number.txt` — Chinese numeral words (0..23) used by number formatting.
- `codepage.txt` — locale list (936 zh_CN default).
- `jx3_cursor.ini` — cursor definitions (used by `CursorList`).
- `uiconfig.ini` — `[Standard] CanvasWidth=1280, CanvasHeight=960`,
  `[UIScale] MaxScale=2`, `[Balloon]`, `[Animation] Enable=1`,
  `[IniLoad] NewIniLoad=1`, `[WndContainer] MaxAppendFromIni=3000`.
