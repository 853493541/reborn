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
- Corpus: 133 extracted scripts, 132 decompiled with unluac 1.2.3 into
  `proof/ui/basic_ui/decompiled/` (git-ignored).
- VM: the client's bundled LuaJIT (`SeasunDownloaderV2.4/.../lua51.dll`, loaded read-only via
  ctypes) runs Lua 5.1 source; verified (`print`, arithmetic, `luaL_loadfile`).
- Shim surface (from the decompiled corpus): ~120 UI methods (`Lookup`, `Show`, `Hide`, `SetText`,
  `SetSize`, `SetRelPos`, `SetPoint`, `SetFrame`, `Check`, `SetAlpha`, `SetFontScheme`, `Enable`,
  `AppendItemFromIni/String`, `FormatAllItemPos`, `Clear`, `GetSize`, `GetAbsPos`, `UpdateAnchor`, …)
  plus engine globals (`GetClientPlayer`, `GetFormatText`, `OutputString`, `RegisterEvent`,
  `FireUIEvent`, `INVENTORY_INDEX`, `g_tStrings`, per-module tables). Data APIs get deterministic
  placeholder values (marked as samples, same policy as the HUD sample texts).
- **Blocker found 2026-10-04:** unluac 1.2.3 emits `goto lbl_N` whose label is nested in a sibling
  block; LuaJIT rejects it (`undefined label`). 148/186 decompiled files compile, **38 fail** —
  including CharacterPanel, BigBagPanel, NewSkillPanel, GuildMainPanel, KungFuPanel, WorldMap,
  Target, ActionBar, Minimap, QuestTraceList. Options: newer unluac (1.3.x), a label-repair pass, or
  a different decompiler. Until fixed, Layer B replays only the compiling scripts.

**Layer C — conformance gate + status dashboard.** The existing `--status` scan plus a construct
census; a window render is "conformant" when it uses no unimplemented construct and its runtime
state is either replayed (script) or flagged (`runtime-hosts=N`).

## 3. Work order

1. ~~ImageType 12/17/18/19 on the diced path~~ (done, engine dispatch cited).
2. ~~AnchorDst=client basis~~ (done; client ≡ window rect for standalone renders).
3. Script decompiler fix (newer unluac or label-repair) → replay harness → generated
   `Data/runtime_state/<window>.tsv` consumed by the viewer (no hand edits).
4. KGUI conformance pass in census order: page sets, list/tree controls, PosType 3/4/5,
   FirstItemPosType variants, scene/web surfaces.
5. Census + `--status` as the gate; contact sheets for review.

## Reproduce

```powershell
# census (session tool): scan assets/ui/Config/Default for WndType/PosType/ImageType/AnchorDst
# decompile corpus
.venv\Scripts\python.exe <decompile_all.py>          # unluac over assets/ui/Config/Default/*.lua
# compile-audit the corpus (LuaJIT lua51.dll via ctypes)
.venv\Scripts\python.exe <lua_compile_all.py>        # 148/186 OK, 38 label-bug failures
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
```

**Confidence:** census HIGH (parsed from the shipped INIs); engine dispatch HIGH (disassembled at
`KGUIX64.dll` RVA 0x117D7C); decompiler blocker HIGH (compile-audited); shim design MED.

Last verified: 2026-10-04 (`--selftest` 1240/0/0; census over 1,240 INIs; 132 scripts decompiled;
148/186 compile).
