# Resource sources, paths and extraction

**Confidence:** VERIFIED by extraction experiments against the local client.

## Where each resource class lives

| class | source | path form | notes |
|---|---|---|---|
| Layout INIs, UI Lua, UI images/atlases | **PakV4** (`bin64/PakV4SfxExtract.exe`) | `ui\...` | `ui/**` does **not** appear in the 3.16M-record CDN index (0 hits) |
| UI particle effects | **CDN `.hpkg`** | `data\source\other\...\Pss\*.pss` | 107 refs from INIs; use `tools/netcode/extract_hpkg_member.py` |
| Fonts | **loose install tree** | `<game>\ui\Font\*.ttf` | 5 files (fzht_GBK, fzxk, fzjz, FangZhengKaiTi-GBK, MSJH) |
| Loading screen | **loose install tree** | `<game>\ui\Loading\*` | `loading.ini` + BMPs (documented in `JX3_MODE_UI_FLOW.md`) |
| Map art (minimap/middlemap) | CDN `.hpkg` (also `_mb` PNG variants) | `data\source\maps\...` | see `MAP_MINIMAP_RESEARCH.md` |

## PakV4 extraction rules (learned)

1. Exact logical path, e.g. `ui\scheme\elem\font.ini`.
2. **No leading backslash** — `\UI\Scheme\Elem\font.ini` is a silent MISS,
   `UI\Scheme\Elem\font.ini` and `ui/scheme/elem/font.ini` both HIT.
3. Lookup is case-insensitive.
4. Batch with `tools/netcode/extract_pak_paths.py --list <file> --out-dir <dir>`
   (per-path resilient, prints HIT/MISS; candidate list read as UTF-8 and
   passed to the tool as GBK, so Chinese names work).
5. Extension swaps apply on the engine side (e.g. `.tga` references stored as
   `.dds`); for UI atlases the `.UITex` descriptor names its texture and a
   `.tga`/`.dds` sibling normally exists.

## INI resource references — corpus facts

From all `proof/minimap/ui/Config/**/*.ini` (see `../manifest/resource_refs.tsv`):

- 653 unique referenced resources across keys
  `Image, ShapTexture, ShapTextureTop/Bottom, sharptexture, defaulttexture,
  SFXFile, Background, BackGround, IconImage, TitleImage`.
- 542 are `ui/...` (PakV4), 110 are `data/...` (hpkg, of which 107 are `.pss`),
  1 other.
- 147 resolve against the locally extracted `proof/minimap/ui` tree; the rest
  need PakV4 extraction (the local tree covers the map windows mostly).
- Extension mix of the references: `.UITex` 427, `.Tga` 113, `.pss` 107,
  `.sfx` 3, `.jpg`/`.dds` 1 each, 1 bare value.
- All 248 locally extracted `.UITex` files parse and their textures resolve
  (validates the atlas reader).

## Extracted for this research (committed as text evidence)

| dir | content | count |
|---|---|---|
| `../scheme/` + `../scheme_utf8/` | system tables | 13 |
| `../animation/` | named UI animation XMLs | 41 |
| `../scripts/` | UI Lua bytecode (lifecycle subset) | 33 |
| `../decompiled/` | unluac output for the lifecycle subset | 22 (20 clean, `control.lua`/`tweenlite.lua` partial) |
| `../re/` | disassembly dumps (KGUIX64/JX3UIX64) | 8 |
| `../manifest/` | TSV tables + candidate lists | 5 |

## Reproduction recipes

```powershell
# 1) system tables / a window's files (exact paths, no leading backslash)
python tools\netcode\extract_pak_paths.py --list <candidates.txt> --out-dir <dir>

# 2) UI-referenced particle effects from the CDN hpkg cache
python tools\netcode\extract_hpkg_member.py <pkg>.hpkg --match <name> --out-dir <dir>

# 3) GBK UI text -> UTF-8 working copies
python tools\prepare_ui_text.py

# 4) Lua bytecode -> readable source (unluac; stripped chunks warn but decompile)
java -jar unluac.jar <script.lua> > <script.decompiled.lua>
```

The candidate lists used here are in `../manifest/`
(`system_tables_candidates.txt`, `lifecycle_scripts_candidates.txt`,
`animation_candidates.txt`).

## Encoding

Layout INIs, `string.txt`, `color.txt`, `showmode.txt`, `font*.ini`,
`icon.txt` and in-INI Chinese `.pss` paths are **GB18030/GBK**. UI Lua
bytecode stores strings as raw GBK bytes; unluac output keeps those bytes, so
decompiled files must be read as GBK when Chinese matters.

## What is still missing locally

`framellist.ini`, `framesetting.txt`, `jx3_mousehover.ini`,
`jx3_popupmenu.ini`, `jx3_pickdropbox.ini` (declared in the path table, MISS in
this build), the 1,5xx `ui/**` manifest files, subfolders
(`CoinShop/`, `ChatPanel/`, `OperationActivity/`, ...), and all referenced
images outside the map subset.
