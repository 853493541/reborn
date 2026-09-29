# Open items / unknown register

Labels: **VERIFIED** (bytes/disasm/extracted table), **INFERRED** (reasonable
from evidence), **UNKNOWN** (not decoded), **MISSING** (declared but not
shipped in this build).

## Formats and tables

| item | state | where to close it |
|---|---|---|
| `.UITex` header/frames/dice-groups | VERIFIED (parser + native asserts); dice-group algorithm exact math still INFERRED | `KImageInfoMgr::LoadUITexFile` / `BuildDicedImage` disasm |
| DDS decoding variants (`StormLine*`, `BMap_149`) | UNKNOWN locally (not decoded in the map app) | DXT1/DXT5 decode + mip handling |
| `string.txt` rich markup (fonts/colors/inline `<KEY>`) | PARTIAL (TSV + `<text text= font=>` parsed; color tags not enumerated) | scan corpus + `KItemText` markup parser |
| `state_x` tables, `TextLength/TextMaxNum` | UNKNOWN | DecodeItem + KItemText dump |
| `framellist.ini` / `framesetting.txt` content | MISSING from build (paths declared) | another client build / runtime dump |
| `jx3_mousehover.ini`, `jx3_popupmenu.ini`, `jx3_pickdropbox.ini` | MISSING from build | another build; search loose tree / other paks |
| `uianimation.txt` id usage (`AniID` binding) | INFERRED (ids 0–4 observed in `AniID`; table has 42) | xref `uianimation` reader in KGUIX64 |
| `*_AniBind.ini` schema | UNKNOWN (name seen in TweenNew) | extract examples from PakV4 |
| `custom.dat` record format | KNOWN as Lua table with `CNDK` header (from map research) | existing docs |

## Rendering semantics

| item | state | where to close it |
|---|---|---|
| `ImageType` full enum (0,1,2,3,4,5,6,7,8,9,10,11,12,13,15,16,20) | PARTIAL: 8 = mirror, 10 = nine-slice (map-app); 11 is 2nd most used, semantics UNKNOWN | KItemImage draw path / `SetExtentImageType`, dump `../re/kgui_imagetype.txt` |
| `PosType` 0 (default) and 1,6,7,8,9,11,12 | PARTIAL: map-app implements 1/6/7/8/9/11/12; 0 = plain Left/Top; 2/3/10 UNKNOWN | `0x1800b8824` decode + layout caller (find switch on the parsed field) |
| `HandleType` layout math (0,1,2,3,5,6) | UNKNOWN | `KItemHandle::FormatAllItemPosByAutoNewLine` etc. |
| `FirstItemPosType` (0,1,2,4,7) | UNKNOWN | same |
| Draw order / clipping / z within a parent | PARTIAL: section order + `drawmgr` layer model; explicit ordering keys UNKNOWN | KGUI item list renderer |
| Button/checkbox group → frame selection | VERIFIED pattern (group tables in `.UITex`); exact `MouseDown/Over/Disable` selection order INFERRED | `GetGroupFrame` + state machine disasm |
| `State_0_*` on Animate/image strips | UNKNOWN | `KAnimateImageMgr::SetAnimateType/GetFrameIndex` |
| `ImagePercent`, `TimeStartAngle`, `Pivot*`, `Rotate*` math | PARTIAL (fields located; formulas not decoded) | KItemImage draw code |
| Rich text measurement/wrapping (`RichText`, `Wrap`) | UNKNOWN | KItemText |
| UI scale math on non-1280×960 screens (`UIScale.MaxScale=2`) | UNKNOWN | `KFontSchemeMgr::SetFontScale` + canvas code |
| Balloon/tooltip layout | PARTIAL (`uiconfig.ini [Balloon]`) | Balloon.lua + native tooltip |

## Coverage

| item | state |
|---|---|
| Window→files mapping | VERIFIED for the whole manifest (1,679 entries), local files 167 |
| System tables | 13 extracted; 5 declared-but-MISSING |
| Resources for map windows | VERIFIED resolved; full-UI resources not extracted (PakV4 program needed) |
| Lifecycle scripts | 22 decompiled (20 clean); `common.lua`/`RemoteCommand.lua`/`GlobalEventHandler.lua` left as bytecode |
| UI event / network wiring | **out of scope for this report** (layout + resources only) |

## Next decode steps (ordered, with anchors)

1. Disassemble the layout switch after `0x1800b8824` (`PosType`) and the
   parent attach at `0x1800bb063` → pin PosType 2/3/10 and anchor math.
2. Disassemble `KItemImage` draw path (find via vtable from the
   `KItemImageData` factory) → full `ImageType` enum + nine-slice borders.
3. `KItemHandle::FormatAllItemPosByAutoNewLine` → `HandleType`/
   `FirstItemPosType` layout math (lists, rows, spacing).
4. `KAnimateImageMgr` → atlas frame animation groups and `Animate` items.
5. `KFontSchemeMgr::LoadScheme` → scheme table load + `Size/Border/Projection`
   composition rules (font.ini fields are known; draw composition is not).
6. Extract and diff a second client build for the 5 MISSING system tables.
