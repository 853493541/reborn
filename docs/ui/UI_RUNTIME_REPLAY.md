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
  `section TAB method TAB args` TSV. Data APIs get deterministic stubs (camelCase fields → 0,
  ALL_CAPS names → permissive constant tables, PascalCase → callable proxies).
- Verified 2026-10-04: `BigBagPanel` → **234 mutations** (`BigBagPanel SetSize 594 624`,
  `Handle_Bag_Compact …`, `Handle_Bag_Normal SetSize`, `Image_Glassmorphism SetSize`, `Hide`/`Show`/
  `SetRelPos`/`Check`/`FormatAllItemPos`/`SetSizeByAllItemSize` …); the module table is the global
  `_G.BigBagPanel` (41 functions; fields `nFrameW=440`, `nFrameH=410`, `nExtendFrameW=594`,
  `nExtendFrameH=624`, `bCompact=false`, `nCount=6`, `aOpen`) — matching the hand-extracted facts.
- Remaining: stub tuning per script (data-dependent branches; e.g. the first bag run took the
  extended 594x624 path because a stubbed branch made `bExtendPackage` truthy), then the viewer
  consumes the generated mutation TSV instead of hand overrides.

**Layer C — conformance gate + status dashboard.** The existing `--status` scan plus a construct
census; a window render is "conformant" when it uses no unimplemented construct and its runtime
state is either replayed (script) or flagged (`runtime-hosts=N`).

## 3. Work order

1. ~~ImageType 12/17/18/19 on the diced path~~ (done, engine dispatch cited).
2. ~~AnchorDst=client basis~~ (done; client ≡ window rect for standalone renders).
3. ~~Script execution path~~ (done: PUC Lua 5.1.5 32-bit built from source runs the original
   bytecode; `tools/ui/replay_harness.lua` replays `OnFrameCreate` and records mutations).
4. Stub-tune the harness per script (data branches), emit `Data/runtime_state/<window>.tsv`, and
   make the viewer consume it (no hand edits).
5. KGUI conformance pass in census order: page sets, list/tree controls, PosType 3/4/5,
   FirstItemPosType variants, scene/web surfaces.
6. Census + `--status` as the gate; contact sheets for review.

## Reproduce

```powershell
# build the PUC Lua 5.1 interpreter the bytecode needs (32-bit: size_t=4 in the header)
#   lua.org lua-5.1.5.tar.gz -> vcvars32 -> cl src\*.c (except luac.c/print.c) /Fe:lua32.exe
# replay one window (writes the mutation TSV)
lua32.exe tools\ui\replay_harness.lua <window.lua> <ModuleName> <window.ini> <out.tsv>
# census + viewer gates
.venv\Scripts\python.exe tools\ui\ini_construct_census.py
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
```

**Confidence:** census HIGH (parsed from the shipped INIs); engine dispatch HIGH (disassembled at
`KGUIX64.dll` RVA 0x117D7C); script replay HIGH (original bytecode runs; BigBagPanel 234 recorded
mutations); shim completeness MED (stub tuning in progress).

Last verified: 2026-10-04 (`--selftest` 1240/0/0; census over 1,240 INIs; BigBagPanel replay 234
mutations).
