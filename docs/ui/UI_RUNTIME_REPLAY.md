# UI runtime replay + KGUI conformance — the correct system

**Status:** active direction (2026-10-04). **Rule:** no per-window fixes. The viewer must derive
every window's runtime state from the client's own inputs (INI + engine semantics + window scripts),
with an automated conformance gate. Hand-written `adjust`/`hide`/`texts` entries stay only as the
historical record for the 绝境战场 windows that were matched to live captures.

## 1. Why the current renders are wrong (systemic diagnosis)

The INI is the authored prototype; the runtime state comes from the engine + the window scripts.
A full census of the 1,240 shipped INIs (`assets/ui/Config/Default`) against the viewer's coverage:

| construct | corpus usage | viewer coverage | consequence |
|---|---|---|---|
| `ImageType` 10/11/12/17/18/19 | 6,257 + 1,680 + **289** + 0 | engine dispatch puts them on ONE diced path (client `KGUIX64.dll` 0x180117D7C: `cmp eax,0xa/0xb/0xc/0x11/0x12/0x13 -> je 0x180117CE7`); viewer handled 10/11 only | **289 sections (ImageType 12) were plain-stretched** — fixed 2026-10-04 |
| `WndPage` / `WndPageSet` | 372 + 141 (130+ files) | page chain filter + partial tab flow | page sets/tab strips need the engine's page-set semantics |
| `WndList` / `WndTreeNode` / `TreeLeaf` / `WndListNode` / `WndTreeList` | 13+16+107+10+6 | not modeled | list/tree windows lose their item layout |
| `WndScene` / `Scene` / `WndMovie` / `WndWebCef` | 33+32+11+10 | placeholders | 3D/web windows are runtime surfaces |
| `PosType` 3/4/5 | 63+5+2 | not implemented | local misplacement |
| `FirstItemPosType` 1/2/3/4/7/8/9 | 98 | treated as boolean | item-flow placement variants |
| `AnchorDst=client` | 847 sections / 548 files (mostly each window's root frame) | non-root fell back to the parent basis | **fixed 2026-10-04**: client = window/client rect for a standalone render |
| window scripts (same-name `.lua`) | 121 INIs (38/53 liked, 21/48 recommended) | replayed by hand for a handful | geometry/visibility/texts/frames/list content stay at authored prototype state |

## 2. The correct system

**Layer A — KGUI conformance (engine-derived, no per-window patches).** Implement every construct
the corpus uses, decoded from `KGUIX64.dll`/`JX3ClientX64.exe` IL, until the census reports zero
unimplemented constructs. Gate: a `--census` check over all 1,240 INIs fails if any used construct
is unhandled (the census script lives in the session notes; move it to `tools/ui/`).

**Layer B — script runtime-state replay.** Run each window's own Lua with a UI API shim and emit
the runtime mutations as data the viewer consumes:
- Corpus: 133 extracted scripts. **They are standard Lua 5.1 bytecode** (`\x1bLuaQ` = ESC "Lua" +
  version 0x51; header `int=4 size_t=4 instr=4 number=8` → a 32-bit PUC 5.1 build). LuaJIT refuses
  PUC bytecode and the client's own VM (`Engine_Lua5X64.dll`) does not export the Lua API, so
  **PUC Lua 5.1.5 was built from source** (lua.org tarball, MSVC x86 via vcvars32 — the 64-bit build
  mismatches the 4-byte `size_t` header). It runs the ORIGINAL compiled scripts directly; the
  unluac decompiler (and its label-scope bugs) is no longer needed.
- Harness: `tools/ui/replay_harness.lua` (run by the built `lua32.exe`). It parses the window INI
  into a section tree, exposes UI proxies (`Lookup`, `Show`/`Hide`, `SetSize`/`SetRelPos`/`SetPoint`,
  `SetText`/`SetFrame`/`Check`, `FormatAllItemPos`, `GetSize`/`IsVisible`/... — every call is
  recorded per section), sets `_G.this` to the root (the engine sets the `this` global before
  dispatching events), runs the module chunk, and calls the module's `OnFrameCreate`. Output:
  `section TAB method TAB args` TSV. Data APIs get deterministic stubs (Hungarian prefixes:
  `b*` → false, `s*` → "", `t*`/`h*`/`p*` → permissive sub-objects, other camelCase → 0; ALL_CAPS
  names → permissive constant tables; PascalCase → callable proxies; `Is*`/`Has*`/`Can*` → false).
- **`module()` finding (fixed 2026-10-05):** every script starts with `module("Name", ExportExternalLib)`
  (Lua 5.1 `loadlib`'s module system). Lua 5.1's `module()` re-setfenv's **its caller** — with the
  harness calling it through a wrapper, that was the wrapper, so the script chunk kept `_G` and every
  module global leaked into `_G` while the module table stayed empty; scripts reading their own module
  table (`ArenaOpponent.Anchor`, `Craft`, `BattleField`, `LiveShowBuff`, `BrightMarkTitle`) then hit
  nil/functions. The wrapper now takes `getfenv(1)` (its own env = the module table), chains
  `__index = _G`, and **`setfenv(2, env)` on the script chunk**. 81 → 85 OK.
- **Handle-property rule (2026-10-05):** in the section proxy, unauthored `hXxx`/`tXxx`/`pXxx`
  properties resolve to the child control section (`hBtnProperty` → `BtnProperty`, case-folding the
  first letter) or a permissive proxy — the engine exposes control handles/tables there, and returning
  `0` aborted scripts that index them (`self.hBtnProperty:...`).
- **PascalCase globals are callable proxies (2026-10-05):** unknown PascalCase names on a permissive
  proxy return a callable+indexable proxy instead of a plain function, because some are module tables
  the scripts index (`Craft.Foo`) and some are functions (`Craft.Foo()`); a plain function broke the
  first form.
- **Scalar getters (2026-10-05):** permissive-proxy/global `Get*Count`/`*Num`/`*ID`/`*Id`/`*Index`/
  `*Level`/`*Score`/`*Screen`/`*Rate`/`*Percent` return `0` — they feed numeric loop bounds and
  comparisons (`for i = 1, GetArenaPlayerCount() do`), and a proxy operand errors. Two exclusions
  are load-bearing: `Get*Size` stays a proxy (`GetBoxSize` results flow through bag arithmetic;
  returning 0 costs BigBagPanel ~200 mutations, verified 793 vs 595) and `Get*Time`/`Get*Frame`
  stay proxies (`GetTodayTime` returns a month/day table, `GetMgFrame`/`GetGameFrame` return frame
  objects — returning 0 broke EditBox and LuckyMeeting).
- **Anchor getters (2026-10-05):** section `GetDefaultAnchor`/`GetFrameAnchor` return an anchor-table
  proxy (the engine's anchor has `s/r/x/y`); the generic `Get*`→0 fallback stored a number and
  `UpdateAnchor` died indexing it (ComboPanel, PetPanel, PetActionBar, PuppetActionBar, EnterAreaTip,
  ProgressBar).
- **More stub rules (2026-10-05, second pass):**
  - `is_*` fields on API tables are boolean functions (`sns_sina.is_bind`), not Hungarian ints.
  - `CAN_*` globals are numeric thresholds (`CAN_HOT_POINT_SHOW`); `_MENU`/`_LIST`/`_TAGS`/`t*` keys
    of `g_tStrings` are authored data tables (everything else is a string).
  - Engine list getters return several lists (multi-value `__call` returns 4 proxies) so
    `table.sort` on the 2nd/3rd list works.
  - Numeric/other-key indexing into a proxy returns an element proxy (scripts `pairs(t[n])`).
  - Section `GetActivePage` resolves the INI's authored `page=`; `GetFirstChild` returns a proxy and
    `GetNext` nil (child walks terminate; a first-child method call works).
  - String helpers: `StringReplaceW` returns its input, `StringFindW` nil (ends the search loop);
    `DateToTime` returns a string, `GetCurrentTime` a number; `string.sub/find/gmatch/gsub/len/byte`
    coerce non-string first args.
- **Permissive-compare build patch (2026-10-05):** the temp PUC Lua build (`lua32.exe`) is patched in
  `lvm.c` so **mixed-type `<`/`<=` return false** instead of erroring and **tables coerce to 0 in
  `luaV_tonumber`** (loop bounds). Lua 5.1's mixed-type comparisons cannot use metamethods; the stub
  environment wants the permissive branch. This is a test-rig patch only (documented here), not
  product behavior.
- **Batch:** `tools/ui/replay_all.py` replays every same-stem `.lua`/`.ini` pair and writes
  `ui-process-app/Data/runtime_state/<stem>.tsv` + `replay_summary.tsv`. Entry chain:
  `OnFrameCreate` → `OnLoad` → `OnCreate` → `Init` → `OnOpen`.
  Verified 2026-10-05 (rechecked): **113/122 scripts replay OK**, 9 partial (most with recorded
  mutations; 3,931 mutations total). Top recordings:
  BigBagPanel 793, Player 178, TopMenu 105, EmotionPanel 105, MiniMap 101, MailPanel 101,
  SoundSettingPanel 93, SocialPanel 91. The remaining partials hinge on module-local tables the
  engine populates at runtime (AccelerateBall, CraftPanel, FBlist, PLActionBar, RaidPanel,
  ReputationPanel) or have no standard init hook (Balloon, TradingSure); stubbing those would mean
  inventing data.
- **Stub rules (2026-10-05 refinement):** unknown camelCase fields return permissive proxies (not 0)
  so container fields the scripts index keep working. Note: the `debug.setmetatable(0, …)` number
  metatable cannot rescue mixed number/table **comparisons** in stock Lua 5.1 (mixed types error
  before metamethods) — hence the build patch above.

## Completion recheck (2026-10-04)

| layer | state | evidence |
|---|---|---|
| A — KGUI conformance | **not started beyond the two engine fixes** | census: 14 unhandled variants (PosType 3/4/5=70, HandleType 1/2/4/5=137, FirstItemPosType 1-9=98) + approximate page-set/list/tree/scene types |
| B — script replay | **input 113/122 full + 9 partial (3,931 mutations); viewer consumption DONE** | `replay_summary.tsv`; `LayoutPlanBuilder.ApplyRuntimeState` loads `Data/runtime_state/<stem>.tsv` and applies SetSize/SetRelPos/SetAbsPos/SetRelX/Y/SetW/H/SetFrame/SetText/SetFontScheme/SetAlpha/Show/Hide/SetVisible before the inventory overrides (root Hide ignored — the engine shows the window after init). BigBagPanel render: root 594x624, `runtime=284`, all six bag rows laid out; `--selftest` 1240/0/0 |
| C — gates | **working** | `--selftest` 1240/0/0; `--status`; `--contact-sheet`; `ini_construct_census.py` |

**Verdict:** the replay now drives the viewer for every window with a recorded TSV; windows without
one (or with partial state) fall back to the authored INI. The bag's now-superseded hand `adjust`
values (440-wide root and backgrounds) were removed — the script's 594x624 is the truth there.
Remaining: stub-tuning the 43 partial scripts and the Layer A conformance pass.

**Layer C — conformance gate + status dashboard.** The existing `--status` scan plus a construct
census; a window render is "conformant" when it uses no unimplemented construct and its runtime
state is either replayed (script) or flagged (`runtime-hosts=N`).

## 3. Work order

1. ~~ImageType 12/17/18/19 on the diced path~~ (done, engine dispatch cited).
2. ~~AnchorDst=client basis~~ (done; client ≡ window rect for standalone renders).
3. ~~Script execution path~~ (done: PUC Lua 5.1.5 32-bit built from source runs the original
   bytecode; `tools/ui/replay_harness.lua` replays `OnFrameCreate` and records mutations).
4. ~~Batch replay~~ (done: `tools/ui/replay_all.py`, 79/122 OK + 43 partial) and ~~viewer
   consumption~~ (done: `LayoutPlanBuilder.ApplyRuntimeState`). Next: keep stub-tuning the remaining
   partial scripts and start the Layer A conformance pass.
5. KGUI conformance pass in census order: page sets, list/tree controls, PosType 3/4/5,
   FirstItemPosType variants, scene/web surfaces.
6. Census + `--status` as the gate; contact sheets for review.

## Reproduce

```powershell
# build the PUC Lua 5.1 interpreter the bytecode needs (32-bit: size_t=4 in the header)
#   lua.org lua-5.1.5.tar.gz -> vcvars32 -> cl src\*.c (except luac.c/print.c) /Fe:lua32.exe
# replay one window (writes the mutation TSV)
lua32.exe tools\ui\replay_harness.lua <window.lua> <ModuleName|auto> <window.ini> <out.tsv>
# replay every scripted window + summary
.venv\Scripts\python.exe tools\ui\replay_all.py [--only <stem>]
# census + viewer gates
.venv\Scripts\python.exe tools\ui\ini_construct_census.py
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
```

**Confidence:** census HIGH (parsed from the shipped INIs); engine dispatch HIGH (disassembled at
`KGUIX64.dll` RVA 0x117D7C); script replay HIGH (original bytecode runs; 78/122 scripts replay OK,
44 partial); shim completeness MED (stub tuning in progress).

Last verified: 2026-10-04 (`--selftest` 1240/0/0; census over 1,240 INIs — 14 unhandled variants;
batch replay rechecked 79 OK / 43 partial of 122 scripted windows, 3,265 mutations).
