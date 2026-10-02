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

TSV `name<TAB>r<TAB>g<TAB>b` (0–255). The loader is `UI::KColorSchemeMgr::Init`
(KGUIX64 fn `0x1801F4300`): it opens the file through the `SchemeElemColor` path key,
reads it as a **4-column tab (`siii` row format)** and keeps an **array** of schemes;
lookups are a linear first-match scan (the `LoadScheme` color loop at
`0x1801F5E79..0x1801F5EA8` breaks on the first hit). Quirks:

- **`red6` is defined twice** (`255,27,27` then `239,55,12`) → the engine resolves
  `red6` = **255,27,27 (first row wins)**. Scheme 208 uses it and is referenced by one
  shipped layout, so this is a real (if tiny) rendering difference;
  `tools/ui_scheme_lookup.py` follows the engine (first-wins), while
  `ui-process-app` still resolves duplicates last-wins — a **known, unfixed deviation**
  recorded here (research only; no code changes were made).
- 21 names mix case (`lightGrayYellow1`, `Yellow14`, `Brown6`, …) — lookup is
  case-insensitive (a scheme uses `Yellow2` while the table defines `yellow2`).

### 1.5 `number.txt` and `codepage.txt`

- `number.txt`: 25 rows `Index/Text` = Chinese numerals（零一二三四五六…）, for
  counters/number displays.
- `codepage.txt`: 7 rows `CodePage/Locale` (932 zh_JP, 936 zh_CN, 949 zh_KR,
  950 zh_HK / zh_TW, 1258 vi_VN) consumed by `KFontSchemeMgr::UpdateCodePage`.

## 2. Resolution rules

### 2.1 Size: scheme override, else fontlist base (**HIGH for all shipped usage**)

**Final answer (2026-09-30, HIGH for both renderers): the scheme record's `Size`
field is not consumed; the effective rendered size is the font slot's base size
(× global/item scale).** `LoadScheme` reads `Size` with default `12` and stores it
raw in the 0x40-byte record (`+4`), but nothing reads `+4`:

- **KGUIX64 (legacy):** two exhaustive scans found no reader — (a) every
  `shl/imul reg, ×0x40` followed by a `[reg+4]` read across the image (only
  unrelated 0x40-stride tables matched); (b) every writer of the item font-size
  float `+0x2F4` (only the scale adjuster `0x18011EFE0` and the draw-struct builder
  `0x180122160`, which copies `+0x2F4` → struct `+0x94`).
- **KGUICocosX64 (live):** the style/decoration converter `0x1802CC070` reads the
  record's `FontID (+0)`, `BorderSize (+8)`, `ProjectionSize (+0xC)`, colors
  (`+0x10/+0x14/+0x18`) and `FontScale (+0x3C)` — **never `+4`**; its style size is
  a constant default `0x10` (16) fallback.
- Both `KFontSchemeMgr::LoadFont` ports build the 36 slot fonts at
  `size = (slot.Size + mgr+0x4C) * mgr+0x48` — the **fontlist slot base size**
  (× global scale) is what gets rendered.
- Data corroborates: the 58 `Size>0` schemes that differ from their slot base are
  unused `____________` placeholders and two chat schemes; every referenced scheme
  resolves to its slot base under any rule. `Size` is editor metadata (the scheme
  names encode the intended size).
- Renderer note: `Size>0 ? Size : base` coincides with the slot base for every
  referenced scheme, so no renderer change is required. Residual caveat: a
  whole-record `movups` copy followed by a local-buffer `+4` read could in
  principle hide a reader — nothing in the traced item/draw paths suggests one.

### 2.2 Border and projection

- `BorderSize>0` + `BorderColor` = 勾边 (glyph outline); `ProjectionSize>0` +
  `ProjectionColor` = 阴影 (drop shadow). Accessors: `GetFontBoder` (sic),
  `GetFontScale`, `GetFontOffset`, `GetFontProjection` (`proof/ui/notes/re-xrefs.md`;
  Lua wrappers `LuaItemText_GetFontBoder/GetFontProjection`).
- `KItemText::SetFontScheme` resolves both and calls the setters with
  `(u16 size, ARGB color)`; the record stores them as 16-bit fields at `+8`
  (border) and `+0xC` (projection). Setters (decoded 2026-09-30, dumps in
  `proof/ui/evidence/battle_hud/re/kgui_font/setters/`):
  - border setter `0x180121FC0`: **clamps size to ≤4** (`cmp dx,4; cmovbe`), stores
    color at `item+0x304`, size byte at `item+0x310`; if the item is dirty it pushes
    per-glyph-part (`item+0x358..0x360`, stride 0xB0) `part+0x84` = border color and
    `part+0xA8` = border size (color alpha attenuated by an item scaling factor
    `(item+0x14 × item+0x18) / 65025`).
  - projection setter `0x180122090`: **clamps size to ≤255**, stores color at
    `item+0x308`, size byte at `item+0x311`; per part `part+0x88`/`part+0xA9`.
  - draw-struct builder `0x180122160`: copies `FontID (+0x2FC)`, fill/border/
    projection ARGB into `+0x80/+0x84/+0x88`, sizes into `+0xA8/+0xA9`, the
    **resolved font-size float `+0x2F4` into `+0x94`** (after `addss` + a
    `0x1804DB63D` rounding call) and `FontScale (+0x314)` into `+0x90`.
- So border thickness is a 0–4 px outline and projection is a 0–255 parameter.
- **Renderer owner (2026-09-30):** the live Cocos UI draws text through cocos2d-x
  label effects (`ccui.RichText:setAnchorTextOutline/Color/Size`,
  `setAnchorTextShadow/Offset/BlurRadius`, `getAnchorTextOutlineSize` in
  `KGUICocosX64`'s binding strings) on the **Imgui backend**
  (`KGUICocosX64` imports `KG3D_ImguiX64.dll`, 2.88 MB, stock ImGui). The exact
  outline/shadow rasterization is therefore **third-party cocos/Imgui code, not a
  JX3-authored formula**; the legacy KGUI draw path continues through virtual
  font-renderer calls (`0x1800FBE70`, vtable `+0x138/+0x148/+0x158`).
  `ui-process-app` keeps approximating the outline with a `DropShadowEffect` and
  does not render projection (§5) — a fidelity gap, not a missing mechanism.

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
| `GrayFontColor` | config file | disabled text |

Per-state consumption is in the component decoders, e.g. the button decoder
(`KGUIX64` fn `0x1800CE400`) reads `NormalFont`/`MouseOverFont`/`MouseDownFont`/
`DisableFont` next to `NormalGroup`/`MouseOverGroup`/`MouseDownGroup`/`DisableGroup`
plus `SfxNormal/SfxMouseOver/SfxMouseDown/SfxDisable`; the `WndEdit` decoder
(fn `0x1800D0100`) reads `FontScheme`, `SelFontScheme`, `CaretFontScheme` and
`KWndEdit::SetPlaceholderFontScheme/Color` exists (placeholder branch, `+0x310`/`+0x311`
alpha bytes on items).
`GrayFontColor` is **not** a per-scheme value: it lives in `ui/scheme/elem/uiconfig.ini`
as `[GrayFontColor] R=207 G=207 B=207` (read by `UI::KConfig::Init`; tracked copy
`proof/ui/evidence/scheme/uiconfig.ini`).

### 2.4 Inline text runs (**MED**, corrected)

`ui/Scheme/Case/string.txt` embeds `<text text="…" font=N>` runs. The `N` values found
are `{4,18,27,32,65,106,162,163,164,172,177}` — above the FontID range (0–35) and
inside the scheme range, so `font=` names a **scheme id** (parser not disassembled).
Correction (2026-09-30): the ASCII `font-size` string in `KGUIX64.dll` (`0x5B20A8`)
belongs to the **SVG/HTML renderer** (`style`/`display`/`fill`/`stroke`/`opacity`
parsers at `0x18010DA50..`), not to the label rich-text path.

**Second control/text layer (found 2026-09-30): `KGUICocosX64.dll`.** Besides
`KGUIX64.dll`, the client ships a Cocos-based control layer that implements
`ccui.KGUIText` (Lua binding table at `0x1803A4490`: `SetText`, `SetString`,
`SetFontColor/Size`, `SetFontScheme/GetFontScheme`, `SetRichText`, `SetAutoEtc`,
`SetMultiLine`, `SetVAlign/HAlign`, `AutoSize`, `Clear`, `GetLayoutInfo`, …) plus a
rich-text API (`TipRichText`, `RichText`, `GetRichText`,
`OnRichTextOpenUrl`, `RichTextImageRenderer`, `OnRichTextSpriteAnim`). Its text
layout exposes per-**fragment-run** fields (`text`, `rowTop`, `relX/relY`,
`absX/absY`, `width`, `height`, `visible`, `alpha`, `isTextFragmentRun`) — the
markup built by the represent layer is parsed here. It also decodes the same INI
keys in lowercase (`richtext`, `multiline`, `halign`, `showall`, `reversemask`,
`shaptexture`, …), i.e. the runtime decoder is case-insensitive. Evidence:
`proof/ui/evidence/battle_hud/re/cocos_richtext/`.

**Which control DLL is live (final, 2026-09-30): the Cocos layer.**
The Cocos UI is a gray-release feature `KGUIUseCocos` in `JX3ClientX64.exe`
(`KLoadGrayFeatureConfig` `0x140099820`: `[KGUIUseCocos] Percent` default 0 clamped
0-100, `Override` from user settings, `IntraNet`; Lua bindings `IsUseCocos` /
`GetCocosGrayInfo` registered via the config binding table at `0x140A3E210`), and
the **client module loader uses `%s%s.dll` with module name `KGUICocos`**
(module list next to `JX3UI`, `JX3Represent`, `KG3DEngineAdapter`).

Live install evidence (read 2026-09-30):
- `config/cocos_config.ini` → `[Main] KGUIUseCocos=1` (explicit override),
- `config/gray_config.ini` → `[KGUIUseCocos] Percent=5` (rewritten by the client
  2026-09-27; the earlier "empty section" read was stale),
- `config/gray_usersettings.ini` → `[Environment] IntraNet=0`,
- the bound `IsUseCocos` implementation (`0x1400A5750`) returns a **constant 1**,
- `ui/Script/base.lua` sets `USE_COCOS = IsUseCocos()` (→ true) and branches on it.

⇒ the running control/text renderer is **KGUICocosX64**; `KGUIX64` is the legacy
renderer still shipped. **`KGUICocosX64` carries its own 1:1 port of the font
scheme manager** (`UI::KFontSchemeMgr::LoadScheme/LoadFont/SetFontScale/...`,
same `SchemeElemFont` keys, 36 slots, `size=(slot+mgr+0x4C)*mgr+0x48`,
`GetLocaleFontListPath`; note its instance offsets differ, e.g. slot array at
`mgr+0x57B8` vs KGUI's `+0x5AA8`) — the KGUIX64 RVAs in §3 document the shared
semantics; Cocos dumps: `proof/ui/evidence/battle_hud/re/cocos_font/`. Dumps for
the gray gate: `re/cocos_gray/`.

**Live text element API** (`ccui.KGUIText` binding table parsed 2026-09-30 from
`KGUICocosX64 0x1803A4490`; dumps `re/cocos_richtext/`): `SetText` `0x1803A19C0`,
`SetString` `0x1803A1D00`, `SetFontScheme` `0x1803A30A0` / `GetFontScheme`
`0x1803A3120`, `SetFontSize` `0x1803A2130`, `SetFontID` `0x1803A4100`,
`SetFontBorder` `0x1803A3670`, `SetFontShadow`/`SetFontProjection` `0x1803A37E0`,
`SetRichText` `0x1803A3160` / `IsRichText` `0x1803A31F0`,
`GetFragmentRuns`/`GetFragmentLabels` `0x1803A3240`, `AutoSize` `0x1803A2200`,
`SetAutoEtc` `0x1803A2580`, `SetFontSpacing/RowSpacing`, `SetNumber`
`0x1803A3D30`, `SprintfText` `0x1803A3E90`, `FormatTextForDraw` `0x1803810B0`.
The fragment runs expose `text/rowTop/relX/relY/absX/absY/width/height/visible/
alpha/isTextFragmentRun` (`0x1803A3240` body).

The producer of the markup is the represent layer:
`OnReloadTable` (`JX3RepresentX64.dll 0x18031FE40`) builds
`<text> text="…" font=10 r=255 g=165 b=0 </text>` (template at `0xC839C8`); the
`font=N` value there is the represent font index, while the string-table variant
(`font=` only, values ≤177) is the scheme id (MED).

**Static-analysis boundary (2026-09-30):** the remaining two unknowns (the exact
markup parser body and the native `CreateProgressBar` producer) are not in any
readable binary. `JX3ClientX64Base.dll` (the client's protected module) shows
`.tp6d` at **entropy 8.00** and a 20.8 MB `.tvm0` VM section, with **zero**
plaintext occurrences of `CreateProgressBar`, `REPRESENT_CALL`, `</text>`,
`font=`, `text="`, `<text`, `KGUICocos` or `IsUseCocos` — both are attributed to
that protected module (or to a literal-free char parser that exhaustive scans did
not isolate). Settling them needs a live debugger session (injection is forbidden
by the repo rules), so they stay documented open items with runtime probes.

KGUI rich-text flag (decoded 2026-09-30): `KItemText::SetRichText` /
`LuaItemText_SetRichText` (`KGUIX64 0x1801978F0`) only toggles **flag bit 23**
(`0x800000`) on `item+0x10`; `IsRichText` reads it. Six text-processing functions
test that bit (`0x180106CBF`, `0x180107A2F`, `0x18011F8FF`, `0x18012006F`,
`0x1801207AF`, `0x180120FBF`) — the markup parser runs inside that path when the
flag is on; the exact parser body is still not isolated (**next probe**: breakpoint
a known markup label in the live client, e.g. a string containing `<1010>`).
Dumps: `re/kgui_richtext/`.

**Markup grammar (decoded via the pure-text extractor):**
`UI::KSchemeScriptTable::LuaGetPureText` (`0x1801AA260`, Lua-exposed) decodes the
string, then `0x1800BC260` scans the **UTF-16** text for the attribute name
`text` (literal `u"text"` at `0x1805AAFE8`), requires `="` (checks `=` then `"`),
copies the quoted value until the next `"`, and unescapes backslash sequences
(`\n`/`\t`/`\"`/`\\` handling at `0x1800BC32E..0x1800BC375`). So the markup carries
its content in the `text="…"` **attribute**, not element text —
`<text text="…" font=N r=R g=G b=B </text>` (represent producer) is the canonical
form; everything else is stripped for pure-text consumers. Renderer-side handling
of `font=`/`r/g/b` attributes remains part of the un-isolated parser.
Dumps: `re/kgui_puretext/`. The `<Dn>` tags (15×) remain undecoded (**unknown**).

## 3. Engine side (KGUIX64.dll; KGUICocosX64 is a parallel port)

Symbols and RVAs (committed annotated dumps in
`proof/ui/evidence/battle_hud/re/kgui_font/`; xref re-run 2026-09-30).
The live Cocos layer ships the same manager (dumps `re/cocos_font/`); the table
below is the legacy KGUIX64 instance of the shared code:

| symbol | RVA / offset | role |
|---|---|---|
| `UI::KFontSchemeMgr::Init` | string `0x5E1BC0` | startup |
| `...::LoadFontList` | fn `0x1801F5790` (assert `0x5E1C50`) | reads `fontlist.ini` (FontList key) |
| `...::LoadDefaultFontList` | string `0x5E1C28` | fallback list |
| `...::LoadScheme` | fn `0x1801F5C30` | reads `font.ini` (SchemeElemFont) |
| `...::LoadFontPathList` | fn `0x1801F63D0` | reads `fontpathlist.ini`; takes `pcszLocale` |
| `...::LoadFont` | fn `0x1801F5350` | loads the 36 slot fonts (scaled) |
| `...::SetFont` | string `0x5E1E48` | per-item font select |
| `...::SetFontScale` | fn `0x180159BA0` | global UI-scale path |
| `...::IsFontVertical` | fn `0x18012FFB0` | vertical flag (used by `KWndEdit`) |
| `...::ReloadFont` | fn `0x1801AA260` | Lua `LuaFont_ReloadFont` binding |
| `...::UpdateCodePage` | fn `0x1801F63D0` | per-locale font path list reload |
| `UI::KColorSchemeMgr::Init` | fn `0x1801F4300` | loads `color.txt` (`SchemeElemColor`, `siii` 4-col tab) |
| `UI::KItemText::SetFontScheme` | fn `0x18011F0F0` | applies a scheme to a text item |
| `UI::KSchemeScriptTable::LuaFont_LoadFontList` | string `0x5D1B88` | Lua reload binding |
| `UI::GetLocaleFontListPath` | string `0x5E1CC0` | locale-specific fontlist |

Decoded behavior:

- **`LoadScheme`** opens the file via `KFilePathMgr::GetFilePath("SchemeElemFont")`, then
  per section `[0..N]` reads `Name` (buffer 32), `Color`, `BorderColor`,
  `ProjectionColor` (resolved against the color array, index stored, `-1` = missing),
  `FontID` (default 0), `Size` (default 12), `BorderSize` (default 0),
  `ProjectionSize` (default 0), `FontScale` (float). Record is **0x40 bytes**:
  `FontID@0 · Size@4 · BorderSize@8(u16) · ProjectionSize@0xC(u16) · Color@0x10 ·
  BorderColor@0x14 · ProjectionColor@0x18 · Name[32]@0x1C · FontScale@0x3C`; the vector
  lives at mgr `+0x5A80/+0x5A88`.
- **`SetFontScheme`** (item method) bounds-checks the id against the vector and stores
  the scheme id at `item+0x2F8`; resolves the record's `FontID` against the 0x138-stride
  slot array (`mgr+0x5AA8`, slot valid byte `+0x134`) into `item+0x2FC`; resolves the
  three color indices to ARGB at color entry `+0x40`; calls the border setter
  (`0x180121FC0`, `(u16 BorderSize, ARGB)`) and projection setter (`0x180122090`,
  `(u16 ProjectionSize, ARGB)`); stores fill ARGB at `item+0x300` and
  `max(0, FontScale)` at `item+0x314`. Missing/black fill in one engine mode is
  substituted by `0xFFFF7E7E`.
- **`LoadFont`** loops exactly **36** slots (`RS2_MAX_FONT_ITEM_NUM`) and creates each
  base font with `size = (slot.Size + mgr+0x4C) * mgr+0x48` (integer size plus an offset,
  times the global font scale float) — the per-scheme `Size` is not part of this call.
- **`SetFontScale`** is the UI-scale entry (`KWndStation::SetUIScale`): computes the
  delta against the old scale (`this+0x6448`), writes the new scale into `mgr+0x48`,
  re-runs `LoadFont`, rescales 9 layer objects, calls `ResizeUI` and fires `UI_SCALED`.
- **`UpdateCodePage(locale)`** re-resolves the font path list for a locale
  (`codepage.txt` 932/936/949/950/1258) and reloads it.

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

Implemented as shipped (`Engine/Fonts.cs`, `Engine/UiLayout.cs` — unchanged by this
research):
scheme id → `FontID` → file + base size; scheme `Color`/`Size`/`BorderColor`/`BorderSize`;
`FontColor` override; fallback scheme **212**; border approximated by a
`DropShadowEffect` (blur 1, depth 0). Deviations from the decoded engine behavior are
recorded, not fixed: duplicate color names resolve **last-wins** (engine: first-wins,
see §1.4) and projection is not drawn.

Not implemented (gaps; data now decoded where noted):
- `Projection*` (阴影) rendering — 149 schemes enable it (the most-used scheme #18 is
  one). Setter/field locations decoded (§2.2); pixel math still open.
- Exact 勾边 thickness (`BorderSize` px) and the engine border math.
- Per-state fonts for buttons/checkboxes (`MouseOverFont` etc.) and `WndEdit`
  caret/placeholder schemes — keys decoded (§2.3).
- Vertical text (`Vertical=1` slots), `SetFontScale`, `codepage.txt` mapping — engine
  paths decoded (§3); renderer side open.
- Rich-text inline runs (`<text font=N>`, `<Dn>` tags) inside string values — label
  parser still open (the `font-size` string is the SVG renderer; see §2.4).

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
# annotated disasm dumps (committed; dump_fn_disasm now resolves RIP-relative strings)
.venv\Scripts\python.exe tools\pvp\dump_fn_disasm.py C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KGUIX64.dll ^
  --names proof\ui\evidence\battle_hud\re\names_font_scheme.txt ^
  --out-dir proof\ui\evidence\battle_hud\re\kgui_font --limit 500
# session scans (temporary probes, local): scheme_size_scan.py -> scheme_size_scan.report.txt,
# scheme_size_readers.txt, scheme_size_resolved.txt under proof/ui/battle_hud/
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
| `proof/ui/evidence/scheme/uiconfig.ini` | `[GrayFontColor] 207/207/207` + Balloon/Option keys (tracked) |
| `proof/ui/evidence/battle_hud/re/names_font_scheme.txt` | symbol list for the RE dumps (tracked) |
| `proof/ui/evidence/battle_hud/re/kgui_font/*.txt` | annotated disasm: LoadScheme/LoadFont/SetFontScheme/SetFontScale/UpdateCodePage/ColorSchemeMgr/per-state decoders (tracked) |
| `proof/ui/evidence/battle_hud/re/kgui_font/setters/*.txt` | border/projection setters + draw-struct builder (`@addr` dumps) (tracked) |
| `proof/ui/evidence/battle_hud/re/cocos_richtext/*` | KGUICocosX64 text/rich-text bindings + fragment-run layout (`ccui.KGUIText`) (tracked) |
| `tools/ui_scheme_lookup.py` | resolver + census tool (committed 2026-09-30) |
| `tools/pvp/dump_fn_disasm.py` | added RIP-relative string annotation (2026-09-30) |

**Verified (2026-09-30):** `tools/ui_scheme_lookup.py 43` → `方正黑体20黑 / fzht_GBK.ttf`;
`--census` over the battle-HUD extraction → `schemes=421 fontlistSlots=36`,
`0 unknown ids`, `0 unresolved FontColor`; combined 160-INI census → 4,595 refs /
125 schemes; xrefs → `LoadScheme 0x1801F5C30`, `LoadFontList 0x1801F5790`,
`LoadFontPathList 0x1801F63D0`, `LoadFont 0x1801F5350`,
`KColorSchemeMgr::Init 0x1801F4300`; annotated dumps show the `siii` color tab load
and the first-match lookup; override-usage scan → 58/58 differing `Size>0` schemes
unused by shipped layouts.

**Game-design check:** Does this follow the client's own truth — no invented fixes or
band-aids? **Yes** — decoded from the shipped tables and engine disassembly; the one
still-open engine detail (the exact glyph-size consumer for `Size>0` overrides) is
marked with its next probe and shown to be unused by every shipped layout. No code
fixes were applied or kept: the app-side changes made mid-session were reverted to
`main` on 2026-09-30 (scope correction — research only), and the `red6`/case-twin
deviations are recorded as findings for a future, explicitly requested fix.
