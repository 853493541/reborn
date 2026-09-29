# Layout INI corpus census

**Confidence:** VERIFIED (counts are machine-parsed from the extracted files).

Scope: all `*.ini` under `proof/minimap/ui/Config/**` in the main worktree
(the locally extracted base-UI subset; the full tree is in PakV4).

## Totals

| fact | value |
|---|---|
| INI files | 125 |
| sections (controls) | 22,054 |
| distinct property keys | 337 |
| distinct `._WndType` values | 28 |

## Control types and their dominant keys

`._WndType` → the keys that occur on nearly every section of that type
(counts in parentheses over the whole corpus):

| type | characteristic keys |
|---|---|
| `WndFrame` | DragAreaLeft/Top/Width/Height, Width/Height, Left/Top, IsCustomDragable |
| `WndWindow` | Width/Height, Left/Top, DummyWnd, FollowMove, FollowSize, Moveable |
| `WndContainer` | Width/Height, ContainerType, DrawStyle, DummyWnd, AlphaShap |
| `WndFlexContainer` | Width/Height, Left/Top, AutoLength, JustifyContent, AlignItems |
| `Handle` | HandleType(4908), FirstItemPosType(4450), Left/Top, Width/Height, RowSpacing |
| `FlexHandle` | Wrap, JustifyContent, PreviewCount, AutoLength |
| `WndScroll` | ScrollHandle, VerScrollBar, ScrollButtonUp/Down, ScrollContainer |
| `WndNewScrollBar` | PageStepCount(208), StepCount(207), SlideBtn, Type, AutoHideSlideButton |
| `WndPageSet` | PageCount, Page_0..Page_N, CheckBox_0/1 |
| `WndPage` | Frame, DummyWnd, FollowSize, FollowMove, Moveable |
| `WndList` | ContainerType, DataBinding, ExportToLua, FlexPadding* |
| `WndListNode` | NormalFrame, MouseOverFrame, MouseDownFrame, SelectedFrame, Image |
| `WndTreeList` | DataBinding, DrawStyle, ContainerType, Wrap, SingleExpand |
| `WndTreeNode` | MouseDownFrame, MouseOverFrame, TitleImage, Selected |
| `WndTreeLeaf` | HandleType, Indent/IndentWidth, CollapseIconFrame, ExpandIconFrame, LineColor, IconImage |
| `Image` | Image(7684), Frame(7639), Alpha(7412), Left/Top, Width/Height, ImageType |
| `WndButton` | NormalGroup/MouseOverGroup/MouseDownGroup/DisableGroup (1375 each) |
| `WndCheckBox` | CheckAndEnable/CheckAndDisable/UnCheckAndEnable/UnCheckAndDisable, *WhenMouseOver, Checking, UnChecking |
| `Text` | FontScheme(4244), `$Text`(4234), FontSpacing(4157), RowSpacing(4104), HAlign/VAlign |
| `WndEdit` | FontScheme, FocusBgColor/FocusBgColorAlpha, MaxLen, Password, Caret/Placeholder schemes |
| `Box` | IconID(473), EventID(424), Index(373) |
| `Null` | Width/Height, PosType (layout placeholder) |
| `Animate` | Image, Group(295), LoopCount(293) |
| `SFX` / `WndSFX` | SFXFile, Scale, Loop |
| `Shadow` | ShadowColor, Alpha |
| `WndScene` | EnableFrameMove, DisableRenderSkyBox/Terrain |
| `WndMinimap` | defaulttexture, sharptexture, selfframe, FollowSize/FollowMove, MinimapType |

## Value distributions (whole corpus)

| key | values (count) |
|---|---|
| `PosType` | 0 (15458), 8 (1100), 7 (306), 9 (163), 10 (83), 1 (45), 2 (28), 11 (26), 3 (11), 12 (4), 6 (1) |
| `ImageType` | 0 (1209), 10 (1046), 11 (351), 1 (257), 8 (138), 7 (87), 16 (82), 12 (73), 6 (22), 2 (8), 4 (7), 20 (5), 13/15 (3/4), 9 (1), 3 (1), 5 (1) |
| `HandleType` | 0 (4387), 3 (523), 5 (10), 2 (6), 6 (5), 1 (4) |
| `FirstItemPosType` | 0 (4444), 2 (25), 4 (3), 1 (2), 7 (1) |
| `AutoSize` | 1 (2583), 0 (1777) |
| `DisableScale` | 1 (8) |
| `RichText` | 0 (22), 1 (14) |
| `MultiFrame` | 1 (1) |
| `Alpha` | 255 (12813), 0 (240), 220 (156), 150 (119), 200 (118), 180 (76), 100 (49), ... |
| `HAlign` | 1 (1848), 0 (855), 2 (428) |
| `VAlign` | 1 (3615), 0 (117), 2 (51) |
| `Wrap` | 1 (5) |
| `Moveable` | 0 (1963), 1 (20) |
| `ShowModeID` | single values 27/28/6/12/7/15; lists `28,27,5,15`; `1,4,5,17`; `1,4,5,16,17,36`; `38,37,36` |
| `AniID` | only 4 uses in this subset (0/3/4) |

## Notes for reproducers

- `PosType` 0 is the overwhelming default (plain `Left`/`Top`).
- `ImageType` 0/10/11/1/8 dominate; 8 = horizontal mirror and 10 = nine-slice
  were verified for map windows by `map-ui-app`; the full enum is still open
  (see `gaps.md`).
- Buttons and checkboxes never carry frames directly: they carry frame-group
  ids resolved through `.UITex` group tables (`NormalGroup` etc.).
- `PosType` 6 appears once in this subset but was used by map-window markers
  (center-on-point).
- `$Text` values are string keys resolved through `ui/Scheme/Case/string.txt`.

Raw TSV/evidence: `../manifest/ui_manifest.tsv`,
`../manifest/resource_refs.tsv`, `../manifest/decoder_properties.tsv`.
