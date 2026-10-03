# JX3 Client UI System — Reproduction Report

**Scope:** how the JX3 PC client declares, loads, lays out and renders its
interface, and exactly which files/resources define any given UI, so that a
window can be rebuilt from the original data.
**Out of scope (by decision):** UI event/network wiring, live-client hooks,
rendering implementations.
**Evidence roots:** local JX3 install `C:\SeasunGame\Game\JX3\bin\zhcn_hd\`,
the extracted UI tree `proof/minimap/ui/`, the module manifest
`proof/netcode/ui_lua_probe/out/ui/module_info.xml`, and the evidence produced
for this report under `proof/ui/`.
**Companion notes:** `proof/ui/notes/manifest.md`, `corpus-census.md`,
`system-tables.md`, `resource-sources.md`, `re-xrefs.md`, `gaps.md`.

Confidence labels (used on every claim):

| label | meaning |
|---|---|
| **VERIFIED** | bytes/file/disasm/table on disk, cited by path or address |
| **INFERRED** | derived from names/structure/patterns; not fully decoded |
| **UNKNOWN** | not decoded yet (see `gaps.md`) |
| **MISSING** | declared by the client but absent from this build |

---

## 1. System overview

The UI is a data-driven XGUI system (KGUI) with a Lua 5.1 behavior layer:

```
ui/module_info.xml                 module graph (frame/lib, layer, file list)
        │
        ├─ ui/Script/...           engine shell + shared libs (compiled Lua 5.1)
        ├─ ui/Config/Default/X.lua window driver  ── ScriptFile ──┐
        └─ ui/Config/Default/X.ini window layout ─────────────────┤
                                                                  ▼
JX3UIX64.dll (scheme/Lua host) ──► KGUIX64.dll / KGUICocosX64.dll (controls)
        │                                   │
        │      ui/Scheme/Elem/*.ini|txt     │  ui/Image/**.UITex + .Tga/.Dds
        │      ui/Scheme/Case/string.txt    │  ui/Font/*.ttf
        └──────────────┬────────────────────┘
                       ▼
            canvas 1280×960 (ui/scheme/elem/uiconfig.ini), screen-scaled
```

- Window layouts are INI sections under `[SectionName]`; every section is one
  control (`._WndType`), attached to a parent section (`._Parent`).
- Images are atlas references: `Image=ui\Image\...\X.UITex` + `Frame=<index>`
  (or a frame-group id for buttons).
- Text is a string key (`$Text=STR_...`) resolved through
  `ui/Scheme/Case/string.txt`, rendered with a named font scheme (`FontScheme`)
  built from `font.ini` + `fontlist.ini` + `color.txt`.
- Visibility is gated by `ShowModeID` values defined in `showmode.txt`.
- Persisted per-window state lives in `custom.dat` (Lua table) under keys
  registered with `RegisterCustomData`.

Client build used for RE: `KGUIX64.dll` 1.0.0.6122, `JX3UIX64.dll` 1.0.0.6116
(**VERIFIED**).

---

## 2. Finding any UI: the discovery chain

### 2.1 The manifest

`ui/module_info.xml` (already extracted) has **1,783 `<file>` entries**, of
which **1,679 are `ui/**`**: 1,402 `ui/Config/**`, 231 `ui/Script/**`,
22 `ui/String/**`, 24 other. Modules are tagged `type="frame"` (a window),
`type="lib"` (shared), or neither (startup scripts), and load in `layer` 1/2
(**VERIFIED**; `proof/ui/evidence/manifest/ui_manifest.tsv`).

Only 167 of the 1,679 exist locally today; the rest are in PakV4 and are
extractable by exact path (section 10).

### 2.2 Lookup procedure for a requested UI

1. Find the window name in `module_info.xml` (module name usually equals the
   window name).
2. Extract the module's Lua: `ui/Config/Default/<Name>.lua` (or the path in
   the manifest; subfolders exist: `BattleField/`, `CoinShop/`, `ChatPanel/`,
   `OperationActivity/`, ...).
3. Extract the sibling layout `ui/Config/Default/<Name>.ini`. A root section
   may name its driver explicitly: `ScriptFile=UI\Config\Default\<Name>.lua`.
4. Read the INI: root section → child sections via `._Parent`.
5. Resolve the system tables and assets (sections 3, 7, 10).
6. If the window has dynamic state, its keys are registered with
   `RegisterCustomData` in the Lua and stored in `custom.dat`.

### 2.3 The path table

`ui/filepath.txt` (extracted at `proof/movement/extracted/ui_filepath.txt`)
is the authoritative list of system files: `ModuleInfo`, `UIConfig`,
`FontList`, `SchemeElemFont`, `SchemeElemColor`, `SchemeElemNumber`,
`SchemeElemCodePage`, `SchemeElemCodePage`, `DrawMgr`, `UIAnimation`,
`ShowModeInfos`, `IconImageTable`, `GlobalStringValuable`, `CursorList`,
`MouseHover`, `PopupMenu`, `PickDropBox`, `FrameList`, `FrameSetting`,
`DefaultShapTexture` (full table in `notes/system-tables.md`) (**VERIFIED**).

---

## 3. File formats

### 3.1 Layout INI (VERIFIED)

- GBK text, `[SectionName]` blocks, `Key=Value` lines, `;`/`#` comments.
- Section names are unique per file; runtime duplicates use a `!` suffix
  (e.g. `Image_Bg1_0!`, from instantiated templates).
- Values are ints, floats, bools (`0/1`), strings, comma lists
  (`ShowModeID=1,4,5,17`), or resource paths (`Image=ui\Image\...`).
- 22,054 sections / 337 distinct keys / 28 control types in the locally
  extracted subset (**VERIFIED** census in `notes/corpus-census.md`).

### 3.2 `.UITex` atlas descriptor (VERIFIED)

92-byte header: magic `UI`, `texWidth@4`, `texHeight@8`, `frameCount@12`,
64-byte texture name at `@24`; then `frameCount` × 20-byte records
(`x, y, w, h, flag`); then frame-group records: u32 `frameCount` (0 = empty
group, 4 bytes), otherwise `frameCount` × 8-byte `(frameIndex, intervalMs)`
entries — the group's start frame is the first entry's index. (An earlier
reading as `(count, startFrame, intervalMs)` + `(count−1)` bare u32 indices
mis-parsed multi-frame groups and desynced the whole group table, making
buttons fall back to their authored `Frame` — fixed 2026-09-30, see
`docs/EXPERIENCES.md`.) The texture
is the sibling file named in the header (case-insensitive; `.tga`/`.dds`
swap accepted).

Native confirmation: `UI::KImageInfoMgr::LoadUITexFile` asserts
`sizeof(UITEXFILEHEADER)`, `sizeof(UITEXFRAMEDATASTRUCTURE)`,
`sizeof(UITEXDICEDBLOK)` (**VERIFIED**, `notes/re-xrefs.md`).

Parser validation: all 248 locally extracted `.UITex` files parse and their
textures resolve (**VERIFIED**).

### 3.3 Textures (VERIFIED/PARTIAL)

- `.Tga` — uncompressed 32-bit BGRA (`desc=8`), read directly.
- `.Dds` — present (e.g. storm lines, `BMap_149`, `Minimap/Sharp.dds`);
  DXT variants are **UNKNOWN** decodes locally.
- Engine may swap extensions at load (`*.tga` references stored as `.dds`).

### 3.4 Strings (VERIFIED format)

`ui/Scheme/Case/string.txt` TSV:
`ID<TAB>Length<TAB>String`, where String contains
`<text text="..." font=N>` runs and inline macros like `<KEY TOGGLE_MAP>`.
`$Text`, `$Tip`, `$AppendString`, `$Placeholder` values in INIs are keys into
this table (plus per-window string tables). Inline color/rich-text markup is
**PARTIAL** (not fully enumerated; `RichText=1` occurs 14×).

### 3.5 Fonts (VERIFIED)

- `fontpathlist.ini` → families (`fzht_GBK.ttf`, `fzxk.ttf`, `fzjz.ttf`, ...);
  files are loose at `<game>\ui\Font\`.
- `fontlist.ini` → FontID → `IsMipmap, IsAntiAlias, Size, Border, Vertical,
  Projection, Dpi, Name, File` (36 entries; `Chat=1` marks a chat-only face).
- `font.ini` → **421 named FontSchemes**:
  `Name, FontID, Color, Size, BorderColor, BorderSize, ProjectionColor,
  ProjectionSize`. The dominant UI scheme is `[212]` = FontID 3, white,
  Size 14, border gray6 (0), projection gray7 (1) (**VERIFIED**).
- `color.txt` → 106 named RGB colors used by the schemes.

### 3.6 UI animation XMLs (VERIFIED schema)

`uianimation.txt` maps 42 ids to `\ui\Animation\*.xml`; all 41 non-empty XMLs
were extracted. Schema (from `line.xml`):
`<Animation name totaltime>` → `<Layer totaltime offsettime>` →
`<Image file frame w h onlyeditor>`, `<KeyFrames/>`,
`<State type totaltime>` → `<Point type="Pos|Line" inherit x y
velocity accelertion>` + `<PosPercent perx pery>`.
Layout-level tweens also exist inline (`PosFromX/PosToX`, `ScaleFrom*`,
`AlphaFrom/To`, curves) and in `ui/Animation/{Middlemap,WorldMap,
DaTangJiaYuan}_Ani.ini` (`StepCount` + `StepN` blocks). TweenNew additionally
references `<Name>_AniBind.ini` binding files (**INFERRED** usage, file
present in the format).

### 3.7 `module_info.xml` (VERIFIED)

`<module name load layer [type=frame|lib]>` with `<file>` children. See §2.

### 3.8 `uiconfig.ini` (VERIFIED)

Game path `ui/scheme/elem/uiconfig.ini` (the repo's
`proof/minimap/ui/Config/uiconfig.ini` is byte-identical).
`[Standard] CanvasWidth=1280 CanvasHeight=960`,
`[UIScale] MaxScale=2`, `[Animation] Enable=1`, `[Balloon] ...`,
`[IniLoad] NewIniLoad=1`, `[WndContainer] MaxAppendFromIni=3000`.

### 3.9 `custom.dat` (INFERRED/known)

Per-character persisted UI state, a Lua table behind a 16-byte `CNDK` header,
written for keys registered with `RegisterCustomData("Window.Key")`.
Documented in `MAP_MINIMAP_RESEARCH.md` §2b; format not re-derived here.

---

## 4. Layout model

### 4.1 Canvas and scaling

Logical canvas **1280×960** (`uiconfig.ini [Standard]`), max user scale 2
(`[UIScale]`). Screen-fit math is **UNKNOWN** (see `gaps.md`).

### 4.2 Hierarchy

- `._Parent` names another section in the same file, or a virtual root layer
  from `drawmgr.ini`: `Lowest, Lowest1, Lowest2, Normal, Normal1, Normal2,
  Topmost, Topmost1, Topmost2` (all enabled; in this build the only explicit
  layer→module mapping is `Lowest → ActionBar1..4`) (**VERIFIED**).
- Unknown/missing parents in the extracted subset are exactly the map windows'
  virtual parents (`Normal`, `Lowest`) and **MISSING** subfolder files.
- Parent section order in the file is the implicit child order; explicit
  ordering keys exist (`Level`, `Index`) but their semantics are **UNKNOWN**.

### 4.3 Geometry keys (VERIFIED presence)

`Left, Top, Width, Height, MinWidth/MinHeight, MaxWidth/MaxHeight,
AutoSize (2583×), DisableScale, ImageWidth/ImageHeight, IconLeft/IconTop/
IconWidth/IconHeight, OffsetX/OffsetY, CanvasWidth/CanvasHeight`.

### 4.4 `PosType` (PARTIAL)

Corpus distribution: `0(15458), 8(1100), 7(306), 9(163), 10(83), 1(45),
2(28), 11(26), 3(11), 12(4), 6(1)`. `PosType=0` = plain `Left`/`Top`.
The map-window reproducer (`map-ui-app/Engine/UiLayout.cs:209-252`) implements,
from observation, these cases (**INFERRED**, verified for map windows):

| PosType | behavior |
|---|---|
| 1 | bottom-aligned in the root (keeps own Left) |
| 6 | centered on the given point (size/2 subtracted) |
| 7 | right-aligned in parent (when Left=0) |
| 8 | right-aligned in root (keeps own Top) |
| 9 | flows right after previous sibling |
| 11 | bottom-right in root |
| 12 | horizontally centered, bottom-aligned in parent |

`PosType` 2, 3, 10 are **UNKNOWN**; the decoder write site is `0x1800b8824`
(see `notes/re-xrefs.md`).

### 4.5 `AnchorArgs` (VERIFIED presence, PARTIAL semantics)

Geometry anchors appear on windows: `AnchorDst=client`,
`AnchorArgs=BOTTOMCENTER,BOTTOMCENTER,0,-195`, `AnchorDefault=1`,
`StretchAnchorArgs`, `StretchAnchorDst`. Grammar is
`<selfPoint>,<dstPoint>[,dx,dy]`; exact point enum and interaction with
`PosType` are **UNKNOWN** (decoder xrefs `0x1800b9094`, `0x1800ccf1f`;
`AnchorDefault=1` occurs 23×).

### 4.6 Containers

- `Handle` — the generic layout container; always carries `HandleType` and
  `FirstItemPosType`; `RowSpacing`, `AlignContent`. `HandleType` values
  `0(4387), 3(523), 5(10), 2(6), 6(5), 1(4)`; `FirstItemPosType`
  `0(4444), 2(25), 4(3), 1(2), 7(1)`. Layout math is **UNKNOWN**
  (native: `KItemHandle::FormatAllItemPosByAutoNewLine`, `SetItemNewIndex`,
  `LoadShapeTexture`) (**VERIFIED symbols**).
- `WndFlexContainer` — flexbox keys `FlexDirection, JustifyContent,
  AlignItems, FlexGrow/Shrink, FlexPadding*, FlexMargin*, AutoLength,
  PreviewCount, Wrap` (**VERIFIED presence**; math **UNKNOWN**, native
  `KItemFlexHandle::FormatAllItemPos`).
- `WndScroll`/`WndNewScrollBar` — `ScrollHandle, VerScrollBar, HorScrollBar,
  ScrollButtonUp/Down/Left/Right, ScrollContainer, PageStepCount, StepCount,
  SlideBtn, AutoHideSlideButton, WheelScrollSpeed, PixelScroll`.
- `WndPageSet`/`WndPage` — `PageCount`, `Page_0..Page_N`, `CheckBox_0/1`;
  `ExportToLua` exposes the set to Lua.
- `WndList`/`WndListNode`, `WndTreeList`/`TreeNode`/`TreeLeaf` — list/tree
  containers with `DataBinding`, `NormalFrame/MouseOverFrame/MouseDownFrame/
  SelectedFrame`, `Indent/IndentWidth`, `Collapse/ExpandIconFrame`,
  `LineColor`, `TitleImage` (semantics partly **UNKNOWN**).
- `WndContainer` — `ContainerType`, `DrawStyle`, `DummyWnd`, `AlphaShap`,
  `AppendContentFromIni` via XGUI.

### 4.7 Visibility and state

`Visible`, `Alpha` (default 255; value 0 exists and is meaningful),
`LockShowAndHide`, `ShowWhenHideUI`, `RenderEvent`, `ViewMutex`/`ViewMutexKey`/
`ViewLevel`, `BreatheWhenHide`, `MousePenetrable`, `Moveable`,
`DisableBringToTop`.

---

## 5. Control catalog (`._WndType`)

28 types observed. Runtime classes and per-type data structs are exposed by
templated factory symbols `UI::DecodeFun<KItem*Data, KItem*>` in KGUIX64
(**VERIFIED**, `notes/re-xrefs.md`).

Base component properties are decoded by
`UI::KUiComponentsDecoder::DecodeItem` (`0x1800b83f0`); 77 of them were
captured with their reader type from the disassembly
(`proof/ui/evidence/manifest/decoder_properties.tsv`; int reader `+0x50`,
string reader `+0x48`, float reader `+0x70`) (**VERIFIED mapping**).

| `._WndType` | role | key notes |
|---|---|---|
| `WndFrame` | top-level window | drag area, `IsCustomDragable`, `ShowModeID`, `AnchorArgs`, `ScriptFile` |
| `WndWindow` | sub-window | `DummyWnd`, `FollowMove/FollowSize`, `Moveable` |
| `WndContainer` | content host | `ContainerType`, `DrawStyle`, appends from INI (XGUI) |
| `WndFlexContainer` | flex layout | `Flex*`, `Wrap`, `PreviewCount` |
| `Handle` / `FlexHandle` | layout/flow containers | `HandleType`, `FirstItemPosType`, `RowSpacing` |
| `WndScroll` / `WndNewScrollBar` | scrolling | scroll buttons, page/step counts, slide button |
| `WndPageSet` / `WndPage` | tab pages | `PageCount`, `Page_N`, `CheckBox_0/1` |
| `WndList` / `WndListNode` | list | `DataBinding`, per-state frames |
| `WndTreeList` / `WndTreeNode` / `WndTreeLeaf` | tree | expand/collapse icons, indent, line color |
| `Image` | atlas image | `Image`, `Frame`, `ImageType`, `Alpha`, `AutoSize` |
| `Text` | label | `$Text`, `FontScheme`, `FontSpacing`, `RowSpacing`, `HAlign/VAlign`, `Wrap`, `RichText` |
| `WndButton` | button | `NormalGroup, MouseOverGroup, MouseDownGroup, DisableGroup` |
| `WndCheckBox` | toggle | `CheckAndEnable/Disable`, `UnCheckAndEnable/Disable`, `*WhenMouseOver`, `Checking/UnChecking`, `RadioButton` |
| `WndEdit` | text input | `FontScheme`, `MaxLen`, `Password`, `FocusBgColor*`, caret/placeholder schemes |
| `Box` | item/object cell | `IconID` (→ `icon.txt`), `EventID`, `Index`; state art from `jx3_box.xml` |
| `Null` | layout spacer | geometry only |
| `Animate` | atlas frame animation | `Image`, `Group`, `LoopCount`, `AnimateType`, `AniStartX/Y`, `AniID` |
| `SFX` / `WndSFX` | particle/effect node | `SFXFile` (`.pss`), `Scale`, `Loop` |
| `Shadow` | drop shadow | `ShadowColor`, `Alpha` |
| `WndScene` | embedded 3D scene | `EnableFrameMove`, `DisableRenderSkyBox/Terrain` |
| `WndMinimap` | native minimap lens | `defaulttexture`, `sharptexture`, `MinimapType`, `FollowSize/FollowMove` |

Note `WndButon` appears as a typo key in some assets; treat case-insensitively
(**INFERRED**).

---

## 6. Image / atlas rendering

1. `Image`/`Group` refer to a `.UITex`; `Frame` is an index into the frame
   table. Negative/`-1` appears 403× (`Frame=-1`) meaning "use default"
   (**INFERRED**; semantics to confirm).
2. Buttons/checkboxes use **group ids** (`NormalGroup`...), resolved through
   the group table that follows the frame table in the `.UITex`:
   `startFrame` = the first frame of the group (normal state artwork).
3. `ImageType` (17 values observed) is the render mode. Verified for map
   windows: `8` = horizontal mirror, `10` = nine-slice ("diced", native
   `BuildDicedImage`/`SetFrameDiecedInfo`/`GetFrameDiecedSize`). Most common
   values: `0(1209), 10(1046), 11(351), 1(257), 8(138)`. **`ImageType=11` =
   horizontal three-slice** (the caps keep their pixel size, only the middle
   stretches): the KGUI draw dispatch (`KGUIX64.dll` `0x180117D7C`) groups
   10/11/12 and 17/18/19 on one diced path, and the queue panel's reward
   plaque (PVPUI22 frame 11, 48x20 authored 68x20) renders with native caps
   in the live capture — a plain stretch flattened them so the right cap read
   as open (verified 2026-10-02; the viewer detects the cap widths from the
   frame's alpha/color profile, since this atlas ships no diced blocks). The
   rest of the enum is UNKNOWN (`notes/gaps.md`).
4. Related keys: `Alpha`, `DisableScale`, `ImagePercent`, `TimeStartAngle`,
   `PivotRotate/PivotScaleX/Y/PivotX/Y`, `Rotate*`, `RenderSampling`,
   `Blur`, `GrayColor`, `ReverseMask`, `R/G/B`, `ShapTexture*`.
5. The item-cell state art (chosen for `Box`) is a **system table**, not the
   INI: `jx3_box.xml` maps `CoolDown/Sparking/InUse/Staring/Selected/
   UnEquipable/Disable/NoObjectMouseOver/MouseOver/Pressed` →
   `image + frame + animation` for both square cells and circular cells
   (**VERIFIED**).
6. Icons: `icon.txt` maps numeric `IconID/Index` → `.UITex` + frame (+ large
   and mobile variants); 28,202 rows (**VERIFIED format**).

---

## 7. Text rendering

Composition chain (**VERIFIED data, PARTIAL draw rules**):

`Text.FontScheme` → `font.ini [scheme]` → `FontID` → `fontlist.ini [FontID]`
(face file, size, vertical, projection, DPI) + `color.txt` names for
`Color`, `BorderColor`, `ProjectionColor`, plus `Size`, `BorderSize`,
`ProjectionSize`. `FontColor` on the control can override the color.

Defaults: 4,244 `Text` sections use a `FontScheme`; scheme `212` is the
common default (Size 14 white with gray projection). Fonts are loose TTFs
under `<game>\ui\Font\` and loaded by `UI::KFontSchemeMgr` (**VERIFIED
symbols**). Alignment via `HAlign (0/1/2 = 855/1848/428)` and `VAlign`;
spacing via `FontSpacing` and `RowSpacing`; multi-line via `MultiLine`/`Wrap`;
rich text via `RichText`. Exact wrap/measure and projection offsets are
**UNKNOWN** (`GetFontOffset/GetFontScale/GetFontBoder/GetFontProjection`).

Strings resolve `$Text`/`$Tip`/`$AppendString`/`$Placeholder` through
`string.txt`; the `<text text= font=>` runs can change font per run and embed
`<KEY ...>` hotkey macros (**VERIFIED**). `number.txt` provides Chinese
numerals for counters.

---

## 8. States and animation

- **Button states**: `NormalGroup, MouseOverGroup, MouseDownGroup,
  DisableGroup` (+ optional `Image_OnAttack`/`Image_Die` runtime overlays).
- **Checkbox states**: `CheckAndEnable, CheckAndDisable, UnCheckAndEnable,
  UnCheckAndDisable`, each with a `*WhenMouseOver` variant, plus
  `Checking/UnChecking` transitional art and `Checking`/`RadioButton` flags.
- **Generic keyed states**: `State_0_0..State_2_2`, `State_N_Count/Type`,
  `StateCount`, `nDefaultState` (observed on animated cells).
- **Atlas animation**: `Animate` items and groups
  (`KAnimateImageMgr::SetAnimateType/GetFrameIndex/GetGroup`), `LoopCount`,
  `MultiFrame`; separate `Animate_New` sections appear in layouts.
- **Layout tweens**: inline keys (`Pos/Scale/Alpha/Rotate/Size/Distance`
  `From/To/Time/Delay/Curve/Enable`) and `_Ani.ini` step files
  (`Middlemap_Ani.ini` etc.), plus the named vector animations in
  `ui/Animation/*.xml` (`uianimation.txt` id table) and `*_AniBind.ini`
  binding files. Selection/timing rules are **PARTIAL**.

---

## 9. Show modes and scaling

`ShowModeID` values are defined in `ui/Scheme/Case/showmode.txt` (0–38, full
list in `notes/system-tables.md`): e.g. `6` Cloud (mobile-stream mode),
`12` Homeland building, `1` DesertStorm OB, `27` StoryMode, `28` DungeonOB,
`36–38` GooseDuckKill. Windows are shown when the current mode id is in their
list (`ShowModeID=1,4,5,17` etc.); `IsShowModeIdLegal` exists natively
(**VERIFIED**). Canvas is 1280×960 with `MaxScale=2`; the exact screen-fit
formula is **UNKNOWN**.

---

## 10. Getting the resources

Source matrix (**VERIFIED** by experiment):

| resource | source | rule |
|---|---|---|
| `ui/**` (INI, Lua, images, atlases, scheme tables) | PakV4 (`bin64\PakV4SfxExtract.exe`) | exact path, no leading `\`, case-insensitive |
| `data/source/other/**/Pss/*.pss` (UI effects) | CDN `.hpkg` | `tools/netcode/extract_hpkg_member.py` |
| fonts | loose `<game>\ui\Font\*.ttf` | copy directly |
| loading art/config | loose `<game>\ui\Loading\` | documented in `JX3_MODE_UI_FLOW.md` |

Facts and caveats:

- The 3.16M-record CDN index contains **zero `ui/*`** records → UI is
  PakV4-only.
- A leading backslash silently MISSes; `ui\...` and `UI/...` both HIT.
- Bulk extraction recipe and candidate lists:
  `tools/netcode/extract_pak_paths.py --list <file> --out-dir <dir>`;
  examples in `proof/ui/evidence/manifest/`.
- Corpus reference coverage: **653 unique refs** (542 PakV4-ui, 110 hpkg-data
  of which 107 `.pss`, 1 other), **147 resolved locally**; extension mix
  `.UITex 427 / .Tga 113 / .pss 107 / .sfx 3 / .jpg 1 / .dds 1`.
- `string.txt`, INIs, `icon.txt`, `font*.ini`, `color.txt`, `showmode.txt`
  and Chinese `.pss` paths are **GBK/GB18030**.

---

## 11. Lifecycle (how it happens)

Evidence: `proof/ui/evidence/decompiled/` (unluac), `module_info.xml`,
native symbols.

1. **Startup**: `ui/module_info.xml` loads layer-1 libs and shell scripts
   (`base.lua`, `common.lua`, `customdata.lua`, `module.lua`, `XGUI.lua`,
   `uishell.lua`, ...).
2. **Shell**: `uishell.lua` owns the frame-loading queue —
   `GetFrameList`, `LoadFrameList`, `AdjustFrameListPosition`,
   `UnloadFrameList`, `GetFrameLoadingQueueCount`, `GetDelayOpenFrames`,
   and fires `ON_UI_SHELL_LOAD_END` through `FireUIEvent` after
   `RenderCall("OnUIShellLoading", ...)` (**VERIFIED from decompile**).
   Loading-order helpers (`LoadFrameList`, `LoadModule`) are globals invoked
   from the shell.
3. **Window modules**: each `type="frame"` module's Lua registers events
   (`RegisterEvent`) and persisted state (`RegisterCustomData("Window.Key")`)
   and exposes `Open/Close` functions. Example (`module.lua`): `MessageBox`
   templates at `ui/Config/Default/MessageBox/%s.ini`, `RegisterCustomData`
   for `ArenaOpponent.*`, `Dismantle.*`, `FBTimeRank.*` (**VERIFIED**).
4. **Instantiation**: the native side reads the layout INI with
   `UI::KUiComponentsDecoder::DecodeItem` (`0x1800b83f0`), creates the item
   per `._WndType` through the `DecodeFun<KItem*Data,KItem*>` factories, and
   attaches children to `._Parent` (parent xrefs `0x1800bb063...`).
5. **Persistent state**: `custom.dat` (CNDK-headed Lua table) stores
   `Window.Key` values such as `Minimap.bOpen`, `MiddleMap.nAlpha`,
   `BattleFieldMap.Anchor`.
6. **Visibility**: `LOADING_END`/mode events open HUD windows subject to
   `ShowModeID`; a window's own Lua drives `Show/Hide`.

UI event names and server calls visible in decompiled scripts
(`RegisterEvent`, `RemoteCallToServer`, ...) are intentionally **not**
analyzed in this report.

---

## 12. Reproduction procedure (step-by-step)

1. **Locate** the window in `module_info.xml`;
   extract `ui/Config/Default/<Name>.lua` + `.ini`.
2. **Extract system tables** (section 3.5/3.6/3.8, exact paths):
   `uiconfig.ini`, `font.ini`, `fontlist.ini`, `fontpathlist.ini`,
   `color.txt`, `number.txt`, `codepage.txt`, `showmode.txt`, `icon.txt`,
   `drawmgr.ini`, `uianimation.txt`, `jx3_box.xml`; copy `ui/Font/*`.
3. **Parse the INI** into sections; build the tree from `._Parent`; resolve
   `PosType`/anchors; note `ShowModeID`.
4. **Resolve art**: `Image`/`Group` → `.UITex` frame/group; buttons via
   `NormalGroup...`; extract every referenced `.UITex` and its texture
   (extension swap); `Box` state art from `jx3_box.xml`; `IconID` from
   `icon.txt`; `SFXFile` `.pss` from hpkg.
5. **Resolve text**: `$Text` → `string.txt`; `FontScheme` → `font.ini` →
   `fontlist.ini` + `color.txt`.
6. **Render**: canvas 1280×960; draw in parent/child order; apply the
   `ImageType` mode (nine-slice/mirror known), alpha, states; play
   `Animate`/tween per `uianimation`/`_Ani.ini`.
7. **Validate** against a real screenshot or the client at the same mode.

### Worked example — MiniMap (M-window lens)

| item | value | source |
|---|---|---|
| module | `DefaultScript17` (`ui/Config/Default/Minimap.lua`) | `module_info.xml` |
| layout | `ui/Config/Default/MiniMap.ini` (33,501 B extracted) | PakV4 |
| root | `[Minimap]` `WndFrame`, `._Parent=Normal`, `AnchorArgs=TOPRIGHT,TOPRIGHT,0,0`, `ShowModeID=17,36` | INI |
| lens control | `[Minimap_Map]` `WndMinimap`, `Image=UI\Image\Minimap\Minimap.UITex`, `defaulttexture=...defualtminimap.jpg`, `sharptexture=ui\Image\UItimate\Minimap\MinimapSharp.tga`, `MinimapType=1` | INI |
| chrome atlases | `ui/Image/Minimap/{Minimap,Minimap2,Minimap3,MapMark,BattleMinimap*,BMap_149}.{UITex,Tga,Dds}` | extracted |
| icons | `MapMark.UITex` frames, `icon.txt` for item cells | evidence |
| text | `STR_SYSTEM_MAP`, `STR_MINIMAP_*`, `STR_BATTLEFIELD_*` | `string.txt` |
| map art | per-map `<map>minimap` tiles/`middlemap` + `config.ini`/`area.tab` | `MAP_MINIMAP_RESEARCH.md` |
| state | `Minimap.bOpen`, `RadarType`, `RadarParam`, `nVersion` | `custom.dat` |
| map marks | `KMapMark` record (0x60 B) + `SyncMidMapMark` | `MAP_MINIMAP_RESEARCH.md` §10 |

The existing `map-ui-app` (WPF) already reproduces these three map windows
from the same data and is the reference for the verified subset
(`PosType` cases, `.UITex` group logic, TGA reading, nine-slice).

---

## 13. Coverage and still-open items

- **Exact today**: discovery (manifest/path table), file formats (INI, UITex
  structure, string.txt, font tables, animation XML, box states, icon table),
  font/color/mode schemes, resource sourcing rules, map-window layout
  semantics.
- **Partial**: `PosType` 2/3/10, `ImageType` enum beyond 8/10, `HandleType`/
  `FirstItemPosType` layout math, states/tween selection rules, text
  measurement, screen scaling.
- **Missing data**: `framellist.ini`, `framesetting.txt`,
  `jx3_mousehover.ini`, `jx3_popupmenu.ini`, `jx3_pickdropbox.ini`, the
  ~1,500 unextracted `ui/**` files, DDS decode.

The address-level decode plan for each open item is in
`proof/ui/notes/gaps.md` (each entry names the function/address to attack).

---

## 14. Evidence index

| path | content |
|---|---|
| `proof/ui/notes/manifest.md` | manifest census + window→files rule |
| `proof/ui/notes/corpus-census.md` | INI/property/type statistics |
| `proof/ui/notes/system-tables.md` | scheme/font/mode/animation/state tables |
| `proof/ui/notes/resource-sources.md` | source matrix + extraction recipes |
| `proof/ui/notes/re-xrefs.md` | native symbols, addresses, decoder fields |
| `proof/ui/notes/gaps.md` | unknown register + decode plan |
| `proof/ui/evidence/manifest/ui_manifest.tsv` | 1,783 manifest entries + presence |
| `proof/ui/evidence/manifest/resource_refs.tsv` | 653 INI resource refs + class/resolution |
| `proof/ui/evidence/manifest/decoder_properties.tsv` | 77 base properties + reader slot |
| `proof/ui/evidence/scheme/` (+`_utf8/`) | extracted system tables |
| `proof/ui/evidence/animation/` | 41 named UI animation XMLs |
| `proof/ui/evidence/scripts/`, `decompiled/` | UI Lua bytecode + unluac output |
| `proof/ui/evidence/re/` | KGUIX64/JX3UIX64 xref disassembly dumps |
| `docs/ui/MAP_MINIMAP_RESEARCH.md` (main worktree) | map/minimap runtime background |
| `proof/netcode/ui_lua_probe/out/ui/module_info.xml` | the UI manifest |
| `proof/movement/extracted/ui_filepath.txt` | the system path table |
