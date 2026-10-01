# JX3 KGUI font & color code system (`FontScheme=#43`, `FontColor=<name>`)

**Branch:** `agent/battle-floating-ui` (worktree `../reborn-iso-battle-floating-ui`)
**Date:** 2026-09-30
**Method:** decode of the shipped UI scheme tables (tracked copies under
`proof/ui/evidence/scheme/`), a usage census over the locally extracted INI corpora,
and xref/disasm probes on `KGUIX64.dll` (client `bin64`, read-only).
**Evidence roots:** `proof/ui/evidence/scheme/*` (tracked);
main-checkout local corpus `proof/minimap/ui/Config/**` + worktree extraction
`proof/ui/battle_hud/pakv4/*.ini` (160 INIs total); `ui-process-app/Data/text/ui/Scheme/Case/string.txt`
(tracked); `proof/movement/extracted/ui_filepath.txt` (path table); client binaries
cited by symbol/RVA (2026-09-30 build).

Confidence: **HIGH** = shipped file/RVA cited · **MED** = data-consistent inference,
engine not decoded · **LOW** = plausible, unproven.

---

## 0. TL;DR — the "code" is a scheme id

A layout never names a font. It writes a **scheme id** (the user-visible "font#43")
and optionally a **color name**:

```ini
[Text_Title]
._WndType=Text
FontScheme=43          ; -> font.ini [43] = 方正黑体20黑
FontColor=yellow2      ; optional fill override -> color.txt yellow2 #F0F000
```

The chain is:

```
FontScheme=<id 0..420> ─► font.ini [id]                        (421 schemes)
                           Name, FontID, Color, Size,
                           BorderColor/BorderSize,
                           ProjectionColor/ProjectionSize
                              │ FontID 0..35
                              ▼
                          fontlist.ini [FontID]                 (36 slots)
                           File → ui/Font/*.ttf, base Size, Vertical, Dpi, Chat
                              │ File
                              ▼
                          fontpathlist.ini families ─► <game>\ui\Font\<file>.ttf (5 files)

FontColor=<name> ─► color.txt (106 rows / 105 names) → RGB
```

There are **421 font schemes**, not ~100; a corpus-wide census found **125 distinct
schemes actually referenced** (~4,600 refs). Everything below is shipped data.

## 1. The tables

Path-table keys (`proof/movement/extracted/ui_filepath.txt`) and their logical paths:

| key | logical path | tracked copy |
|---|---|---|
| `SchemeElemFont` | `\UI\Scheme\Elem\font.ini` | `proof/ui/evidence/scheme/font.ini` |
| `FontList` | `\UI\Scheme\Elem\fontlist.ini` | `proof/ui/evidence/scheme/fontlist.ini` |
| `FontPathList` | `\UI\Scheme\Elem\fontpathlist.ini` | `proof/ui/evidence/scheme/fontpathlist.ini` |
| `SchemeElemColor` | `\UI\Scheme\Elem\color.txt` | `proof/ui/evidence/scheme/color.txt` |
| `SchemeElemNumber` | `\UI\Scheme\Elem\number.txt` | `proof/ui/evidence/scheme/number.txt` |
| `SchemeElemCodePage` | `\UI\Scheme\Elem\codepage.txt` | `proof/ui/evidence/scheme/codepage.txt` |

`KGUIX64.dll` carries the key strings (`SchemeElemFont` `0x5E1D68`, `SchemeElemColor`
`0x5E1BB0`, `FontList` `0x5E1CF8`) and opens the tables through them — the literal
filenames `font.ini`/`color.txt` are **not** in the binary (probe, 2026-09-30), which
is why the path table is the right lookup for re-extraction.

### 1.1 `font.ini` — 421 schemes (`[0]..[420]`)

Fields per scheme: `Name`, `FontID`, `Color`, `Size`, `BorderColor`, `BorderSize`,
`ProjectionColor`, `ProjectionSize` (all color fields are `color.txt` names).
Data facts (2026-09-30, `tools/ui_scheme_lookup.py --list`):

- **358/421** schemes have `Size=0` → fall back to the fontlist base size (see §2.1).
- **83** schemes enable a border (`BorderSize>0`), **149** a projection
  (`ProjectionSize>0`); the rest are plain fill.
- Every scheme color resolves except one case variant: `font.ini` has a scheme with
  `Color=Yellow2` while `color.txt` only defines `yellow2` → lookup is
  case-insensitive (our renderer already is).
- Names encode the intent, e.g. `#0 方正黑体16白`, `#18 方正黑体15白-阴影灰7`,
  `#43 方正黑体20黑`, `#212 最小字体测试`, `#162 【弃用】方正黑体15白-阴影灰7`
  ("deprecated" — still referenced 210× in the local corpus; do not drop it).

### 1.2 `fontlist.ini` — 36 font slots (`[0]..[35]`)

Fields: `IsMipmap`, `IsAntiAlias`, `Size` (base), `Border`, `Vertical`, `Projection`,
`Dpi`, `Name`, `File`, `Chat=1` (chat-only slot). Facts:

- base `Size` spans **12–60**; `Vertical=1` slots: 2, 6, 11, 16, 19, 20, 23, 26, 34, 35;
  slot 5 is the chat face.
- all 36 slots use only **4 files**: `\UI\Font\fzht_GBK.ttf`, `fzxk.ttf`, `fzjz.ttf`,
  `FangZhengKaiTi-GBK.ttf` (plus the unreferenced `MSJH.TTF` fallback in the install).

### 1.3 `fontpathlist.ini` — 3 families

`方正黑体 → \UI\Font\fzht_GBK.ttf`, `行楷 → fzxk.ttf`, `剪纸 → fzjz.ttf`
(方正楷体 is referenced directly by `fontlist.ini`, not listed here).

### 1.4 `color.txt` — 106 rows / 105 unique names

TSV `name<TAB>r<TAB>g<TAB>b` (0–255). Quirks:

- **`red6` is defined twice** (`255,27,27` then `239,55,12`) — last-wins in our
  renderer; engine side not decoded (**MED**).
- 21 names mix case (`lightGrayYellow1`, `Yellow14`, `Brown6`, …) — lookup is
  case-insensitive.

### 1.5 `number.txt` and `codepage.txt`

- `number.txt`: 25 rows `Index/Text` = Chinese numerals（零一二三四五六…）, for
  counters/number displays.
- `codepage.txt`: 7 rows `CodePage/Locale` (932 zh_JP, 936 zh_CN, 949 zh_KR,
  950 zh_HK / zh_TW, 1258 vi_VN) consumed by `KFontSchemeMgr::UpdateCodePage`.

## 2. Resolution rules

### 2.1 Size: scheme override, else fontlist base (**MED**)

`Size>0` on the scheme wins; `Size=0` (358 schemes) falls back to the `FontID`'s base
`Size` in `fontlist.ini`. Evidence: names agree with the base size for 346/358
schemes (the 9 disagreements are stale names — e.g. `#215 最小字体测试4` base 14,
`#339 方正黑体8白` base 15 — and 3 have no number), and `ui-process-app/Engine/Fonts.cs`
implements exactly that. Engine confirmation is still open:
`UI::KFontSchemeMgr::LoadScheme` fn `0x1801F5C30` (KGUIX64) is the next probe.

### 2.2 Border and projection

- `BorderSize>0` + `BorderColor` = 勾边 (glyph outline); the engine accessor symbol is
  `GetFontBoder` (sic), plus `GetFontScale`, `GetFontOffset`, `GetFontProjection`
  (`proof/ui/notes/re-xrefs.md`).
- `ProjectionSize>0` + `ProjectionColor` = 阴影 (drop shadow).

### 2.3 FontColor / per-state codes

`FontColor=<name>` is a **fill override** on top of the scheme (the decoder reads it as
a string color name; e.g. a label whose scheme is white renders yellow).
Component state fonts are separate keys (KGUIX64 strings, same id space):

| key | corpus refs (files) | applies to |
|---|---|---|
| `NormalFont` | 308 (52) | buttons |
| `MouseOverFont` / `MouseDownFont` / `DisableFont` | 904 / 899 / 899 (63) | buttons |
| `CheckFont` / `UncheckFont` | 597 / 597 (42) | checkboxes |
| `SelFontScheme` / `CaretFontScheme` | 74 / 74 (27) | `WndEdit` selection/caret |
| `PlaceholderFontScheme` / `PlaceholderFontColor` | 35 (17) / present | `WndEdit` placeholder |
| `GrayFontColor` | string only | disabled text |

### 2.4 Inline text runs (**MED**)

`ui/Scheme/Case/string.txt` embeds `<text text="…" font=N>` runs. The `N` values found
are `{4,18,27,32,65,106,162,163,164,172,177}` — above the FontID range (0–35) and
inside the scheme range, so `font=` names a **scheme id** (parser not disassembled).
`<Dn>` style tags (15×) are not decoded (**unknown**).

## 3. Engine side (KGUIX64.dll)

| symbol | RVA / offset | role |
|---|---|---|
| `UI::KFontSchemeMgr::Init` | string `0x5E1BC0` | startup |
| `...::LoadFontList` | fn `0x1801F5790` (assert `0x5E1C50`) | reads `fontlist.ini` (FontList key) |
| `...::LoadDefaultFontList` | string `0x5E1C28` | fallback list |
| `...::LoadScheme` | fn `0x1801F5C30` | reads `font.ini` (SchemeElemFont) |
| `...::LoadFontPathList` | fn `0x1801F63D0` | reads `fontpathlist.ini` |
| `...::LoadFont` | fn `0x1801F5350` | loads a TTF (debug `[KGUI] KFontSchemeMgr::LoadFont(%u, %s)`) |
| `...::SetFont` / `SetFontScale` / `IsFontVertical` / `ReloadFont` / `UpdateCodePage` | strings `0x5E1E48/`… | runtime switching |
| `UI::KItemText::SetFontScheme` | string `0x5B37F8` | text consumer |
| `UI::KSchemeScriptTable::LuaFont_LoadFontList` | string `0x5D1B88` | Lua reload binding |
| `UI::GetLocaleFontListPath` | string `0x5E1CC0` | locale-specific fontlist |

Decoder keys live in the component decoder: `FontScheme`, `FontColor` (plus the
per-state keys above; `proof/ui/evidence/manifest/decoder_properties.tsv`).

## 4. Usage census (2026-09-30)

160 INIs (`proof/minimap/ui/Config/**` + `proof/ui/battle_hud/pakv4/*.ini`):
**4,595 `FontScheme` refs, 125 distinct schemes, 0 unknown ids**. Top:

| id | refs | name | example windows |
|---|---|---|---|
| 18 | 2,397 | 方正黑体15白-阴影灰7 | CastingPanel, EndOfBattle, FightingStatistic, LootList, MainMessageLine |
| 162 | 210 | 【弃用】方正黑体15白-阴影灰7 | CastingPanel, MainMessageLine |
| 3 | 201 | 方正黑体16白-阴影灰7 | BuffList, KillMessage, MainBarPanel, Player |
| 27 | 149 | 方正黑体15黄2-阴影灰7 | BuffList, Playerbar, TargetBuff |
| 160 | 119 | 方正黑体15黑 | — |
| 228 | 94 | 方正黑体14白 | TeamBuff |
| 212 | 90 | 最小字体测试 (the renderer's fallback id) | EndOfBattle, TimeBuff, WhoSeeMe |
| 2 | 86 | 方正黑体16白-勾边灰5 | CastingPanel, LootList, TeamBuff |
| 4 / 9 | 84 / 72 | 15号6 / 垂直15号-勾边灰6 | — |

`FontColor`: 32 distinct names in the corpora, **all resolvable** (case-insensitive).
Per-state keys per §2.3. Inline `font=` per §2.4.

## 5. Renderer status (`ui-process-app`)

Implemented (`Engine/Fonts.cs`, `Engine/UiLayout.cs`):
scheme id → `FontID` → file + base size; scheme `Color`/`Size`/`BorderColor`/`BorderSize`;
`FontColor` override; fallback scheme **212**; border approximated by a
`DropShadowEffect` (blur 1, depth 0).

Not implemented (gaps, all data available):
- `Projection*` (阴影) rendering — 149 schemes enable it (the most-used scheme #18 is one).
- Exact 勾边 thickness (`BorderSize` px) and the engine's border math.
- Per-state fonts for buttons/checkboxes (`MouseOverFont` etc.) and `WndEdit`
  caret/placeholder schemes.
- Vertical text (`Vertical=1` slots), `SetFontScale`, `codepage.txt` mapping.
- Rich-text inline runs (`<text font=N>`, `<Dn>` tags) inside string values.

## 6. Look any code up

```powershell
.venv\Scripts\python.exe tools\ui_scheme_lookup.py 43
# #43  方正黑体20黑
#      FontID=4 -> fzht_GBK.ttf (base size 20, vertical=0, dpi=86)
#      Size=20 (fontlist base)  Color=black #000000
#      Border: size=0 color=black #000000 | Projection: size=0 color=gray7 #262626

.venv\Scripts\python.exe tools\ui_scheme_lookup.py --color yellow2   # #F0F000
.venv\Scripts\python.exe tools\ui_scheme_lookup.py --list            # all 421
.venv\Scripts\python.exe tools\ui_scheme_lookup.py --census --scan proof\ui\battle_hud\pakv4
.venv\Scripts\python.exe tools\ui_scheme_lookup.py 18 --scan <ui\Config dir>
```

## 7. Reproduce

```powershell
# tables (already tracked): proof/ui/evidence/scheme/{font.ini,fontlist.ini,fontpathlist.ini,color.txt}
# usage census + lookup
.venv\Scripts\python.exe tools\ui_scheme_lookup.py --census --scan proof\ui\battle_hud\pakv4
# engine symbols (read-only on the install)
.venv\Scripts\python.exe tools\netcode\xref_string.py C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KGUIX64.dll "UI::KFontSchemeMgr::LoadScheme"
.venv\Scripts\python.exe tools\netcode\xref_string.py C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KGUIX64.dll "UI::KFontSchemeMgr::LoadFontList"
.venv\Scripts\python.exe tools\netcode\xref_string.py C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KGUIX64.dll "UI::KFontSchemeMgr::LoadFontPathList"
```

## 8. Evidence index

| path | content |
|---|---|
| `proof/ui/evidence/scheme/font.ini` | 421 schemes (tracked) |
| `proof/ui/evidence/scheme/fontlist.ini` | 36 font slots (tracked) |
| `proof/ui/evidence/scheme/fontpathlist.ini` | 3 families (tracked) |
| `proof/ui/evidence/scheme/color.txt` | 106 rows / 105 names (tracked) |
| `proof/ui/evidence/scheme/number.txt`, `codepage.txt` | numerals + codepages (tracked) |
| `proof/movement/extracted/ui_filepath.txt` | SchemeElem*/FontList path keys (tracked) |
| `proof/movement/KGUIX64_strings.txt` | KFontSchemeMgr strings/offsets (tracked) |
| `proof/ui/notes/re-xrefs.md` | font symbols + accessors (tracked) |
| `proof/ui/evidence/manifest/decoder_properties.tsv` | `FontScheme`/`FontColor` decoder fields (tracked) |
| `tools/ui_scheme_lookup.py` | resolver + census tool (committed 2026-09-30) |

**Verified (2026-09-30):** `ui_scheme_lookup.py 43` → `方正黑体20黑 / fzht_GBK.ttf`;
`--census` over the battle-HUD extraction → `schemes=421 fontlistSlots=36`,
`0 unknown ids`, `0 unresolved FontColor`; combined 160-INI census → 4,595 refs /
125 schemes; xrefs → `LoadScheme 0x1801F5C30`, `LoadFontList 0x1801F5790`,
`LoadFontPathList 0x1801F63D0`, `LoadFont 0x1801F5350`.

**Game-design check:** Does this follow the client's own truth — no invented fixes or
band-aids? **Yes** — the system is decoded from the shipped scheme tables and engine
symbols; the one unresolved engine detail (`Size=0` fallback math) is marked MED with
its exact next probe instead of being guessed.
