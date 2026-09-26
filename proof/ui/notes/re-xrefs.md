# Static RE evidence for the UI runtime

**Confidence:** VERIFIED addresses/strings from the local client binaries.
Binaries: `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\`
`KGUIX64.dll` v1.0.0.6122 (7,056,304 B), `JX3UIX64.dll` v1.0.0.6116
(5,137,848 B), `KGUICocosX64.dll` v1.0.0.6122, `Engine_Lua5X64.dll`.
Dumps: `../re/*.txt` (produced with `tools/netcode/xref_string.py`).

## The layout decoder (the core of "how an INI becomes controls")

| symbol / item | address | role |
|---|---|---|
| `UI::KUiComponentsDecoder::DecodeItem` | `0x1800b83f0` | master decoder for common component properties (`pszName` assert at line 0x6c4) |
| `UI::KItemImageData` decoder | `0x1800b9ae0` | `Image` → `[obj+0x5cc]`, `Frame` → `+0x5b8`, `ImageType` → `+0x5bc`, `ImagePercent` → `+0x5c4`, `TimeStartAngle` → `+0x5c8`, `CommonAlpha` → `+0x5c0` |
| `._Parent` | xrefs `0x1800bb063 0x1800bb380 0x1800bb677 0x1800bc496 0x1800d3dce 0x1800d4332 0x18014c389` | parent name → tree attach |
| `NormalGroup` | `0x1800ce596` | button state group decode |
| `ShowModeID` | `0x1800cde77`, `0x18020e1cd` | mode gating decode (`IsShowModeIdLegal` symbol exists) |
| `HandleType` | `0x1800ba2c2` | container type decode |
| `AnchorArgs` | `0x1800b9094`, `0x1800ccf1f` | anchor string decode |
| `PosType` | `0x1800b8824` | position mode decode (reader stub at `0x1800b83d0`) |
| `ImageType` (second site) | `0x1800d3260` | another item class that carries an image type |

`decoder_properties.tsv` lists 77 base properties captured from the
`DecodeItem` dumps with the reader vtable slot used for each:
`+0x50` = integer reader (36), `+0x48` = string reader (19),
`+0x70` = float reader (8). Example keys: `._WndType`, `._Parent`, `Alpha`,
`AnchorArgs`, `AnchorDst`, `AniID`, `AniSPercentX/Y`, `AutoSize`, `EventID`,
`FontSpacing`, `HAlign`, `Height`, `Left`, `Link`, `MousePenetrable`,
`PivotRotate`, ... .

## Atlas / texture symbols (KGUIX64)

`UI::KImageInfoMgr::LoadUITexFile` with size asserts:
`dwReadIn == sizeof(UITEXFILEHEADER)`,
`... sizeof(UITEXFRAMEDATASTRUCTURE)`, `... sizeof(UITEXDICEDBLOK)`;
`UI::KImageInfoMgr::BuildDicedImage`,
`UI::KImageInfo::SetFrameDiecedInfo` / `GetFrameDiecedInfo` /
`GetFrameDiecedSize` (nine-slice = "diced"), `UI::KAnimateImageMgr::GetGroup`
/ `SetGroup` / `GetFrameIndex` / `SetAnimateType` / `ToGray`,
`UI::KTextureMgr::ReadConfigFile`, `[KGUI] KImageInfo::LoadUITexFile(%s)`.

## Font symbols (KGUIX64)

`UI::KFontSchemeMgr::Init/LoadFont/LoadDefaultFontList/LoadFontList/LoadScheme/
SetFontScale/SetFont/UpdateCodePage/ReloadFont`,
`[KGUI] KFontSchemeMgr::LoadFont(%u, %s)`,
`UI::KItemText::SetFontScheme`, `KWndEdit` placeholder/caret schemes,
`GetFontScale/GetFontOffset/GetFontBoder/GetFontProjection` accessors.
Load path table: `ui/scheme/elem/uiconfig.ini` string in `JX3UIX64.dll`
(`0x18039c490`); `JX3UIX64` dump `../re/jx3ui_uiconfig.txt`.

## Item classes (from KGUIX64 symbol table)

Templated per-control factories exist for every `_WndType` data struct:
`UI::DecodeFun<KItemNullData,KItemNull>`, `KItemTextData/KItemText`,
`KItemImageData/KItemImage`, `KItemShadowData`, `KItemAnimateData`,
`KItemSceneData`, `KItemBoxData`, `KItemHandleData`, `KItemTreeLeafData`,
`KItemSFXData`, `KItemFlexHandleData`, plus `KItemHandle::InsertItem/
AppendItem/AppendFromString/FormatAllItemPosByAutoNewLine/SetItemNewIndex/
LoadShapeTexture` and `KItemFlexHandle::FormatAllItemPos/OnFormatChildPos`.
This is the canonical mapping between `._WndType` and the runtime item class.

## Decompiler

`unluac` release `v2023.03.22` (scratchminer) run on JDK 21 decompiles the
PakV4 Lua 5.1 (`\x1bLuaQ`, 32-bit `size_t`) scripts. Chunks are stripped, so
unluac warns and local/upvalue names are lost; control flow and string/int
constants are recovered (sufficient for API and event inventories).

## Notes / cautions

- `proof/minimap/recon/` in the main worktree contains earlier targeted dumps
  (`xref_kgui_minimap.txt`, `kgui_tile_loader.txt`, `kgui_load_default_image.txt`,
  ...); this pass adds the component-decoder dumps under `../re/`.
- Addresses are image-base relative (`0x180000000`) for the current build.
