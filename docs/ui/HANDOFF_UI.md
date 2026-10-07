# UI session handoff — JX3 Reborn viewer (2026-10-07)

**For the next agent.** Read root `AGENTS.md` first, then this. Worktree/branch:
`C:\Users\Zhibin Ren\Desktop\reborn-iso-queue-ui-compare` on `agent/queue-ui-compare`.
All work is committed; `item_checks.tsv`/`rejected.tsv` are gitignored side files.

## 1. What this work is

The WPF viewer (`ui-process-app/`) renders the real client's KGUI UI: the shipped INIs
(authored layout) + the extracted assets + the client's **own Lua bytecode replayed** in a
pinned PUC Lua 5.1 (`tools/ui/replay_harness.lua` / `replay_server.lua`), with the recorded
mutation state consumed by `LayoutPlanBuilder.ApplyRuntimeMutations`. Goal: review every
window's real content and placement; fix rendering gaps from the engine's own code/data.

## 2. Current measured state (all green)

| gate | command | value |
|---|---|---|
| render | `UiProcessApp.exe --selftest` | **1240/0/0** |
| audit | `UiProcessApp.exe --audit` | placeholders=482, unresolved=49, oob=7610 |
| replay | `.venv\Scripts\python.exe tools\ui\replay_all.py` | **OK=1201 / ERR=1 / NOENTRY=9** of 1211 scripted |
| census | `.venv\Scripts\python.exe tools\ui\ini_construct_census.py` | **0 unhandled constructs** |
| runtime gap | `.venv\Scripts\python.exe tools\ui\runtime_gap_report.py` | visual drops **1** (FromIconID, data-blocked) |

A/B (runtime vs authored/static, `Data/render_status.tsv` vs a static baseline): 31 windows
fuller, 113 fewer (state-driven hides), no collapse.

## 3. What was done in this session (newest last)

- UITex v1/v2 atlas layout from the engine loader; art pair extraction; text libs.
- **All manifest scripts extracted** (1,742/1,783; 41 probe MISS at exact paths — not shipped).
- **Replay 1148 → 1201 OK**: stub classes (is<Upper> predicates, number metatable
  `__index/__newindex/__len`, pairs/ipairs/sort tolerance, `table.insert` tolerance,
  `JsonDecode` passthrough, class-style entry via `<Stem>_Base:new()`, `Table_Find*/Table_Get*`
  proxy rows, session getters) + **permissive-access VM patches** (see §5).
- **HandleType 4/5 decoded/implemented**; **all 13 PosTypes** (jump table 0x180108998);
  **flex/Yoga** layout for `WndFlexContainer`/`FlexHandle`/`WndList`; **page-set semantics**
  (`Page_i`/`CheckBox_i` + `CheckedWhenCreate` default, nested sets show their own default).
- **Interaction**: click/hover, wheel scroll (ScrollHandle translate), WndEdit TextBox →
  `OnEditChanged`, window drag (root background, SetDragArea bounds).
- **Placement fixes** (the 超出窗口 class): `Show(false)` now hides; deferred `Clear` empties
  the list incl. the authored prototype; collapse guard refined (restore only fully-hidden
  page-sets' default page; honor hides when parked sections are hidden).
- **Checklist feature** (`清单` panel beside the canvas): per rendered item, display-based
  defaults (checked unless ⚠ 缺图/文案缺失/超出窗口), overrides in `Data/item_checks.tsv`.
- **Issue-check fixes**: atlas texture-name decoded as **GBK**; size-aware texture pick
  (stale small tga vs real dds); intentional `TextureName=no` no longer flagged.

## 4. Open items (priority order)

1. **WulinShenghuiDuizhen** — the only ERR: `assert` on absent server data. A tolerant assert
   was tried and **rejected** (it changes pcall-guarded branches; broke EmotionPanel). Re-open
   only with the real data source or a caller-aware approach.
2. **FromIconID** (Player) — needs the server icon table (data-blocked); the last visual drop.
3. **Art tail** (482 placeholders): 259 intentional `no`, ~195 files absent from the pak
   (probed MISS), ~24 authored frames beyond the atlas. Only fix if a new source appears.
4. **Text tail** (49 ids): all in tables not shipped (probed MISS) — dev/unreleased.
5. **Interaction extras**: item drag (`OnDragButton`/`OnItemLButtonDown/Move/Up`), scrollbar
   thumb dragging, window chains to popups.
6. **Fidelity (Phase 7)**: unmeasured; user declined a screenshot set — one screenshot only
   when a specific window looks wrong (`tools/proof/image_stats.py`; proof under `proof/ui/`).
7. 9 NOENTRY popups: verified-authored (no init to replay).

## 5. Provisional items (registered, re-open criteria)

The pinned Lua build (`%TEMP%\opencode\lua-5.1.5\...\build32\lua32.exe`) is patched — test-rig
only, documented in `docs/ui/UI_RUNTIME_REPLAY.md`:
`lvm.c` (mixed `<`/`<=` false; tables→0 in `luaV_tonumber`; `#nil`→0; nil arith→0; nil concat→"";
GETTABLE of nil/function/boolean → nil; SETTABLE of nil ignored), `ldo.c` (calling a non-callable
stub is a no-op), `ltablib.c` (concat coerces non-strings), `ldebug.c` (TYPEERROR pcidx/opcode
debug print — diagnostic only). Rebuild recipe: `cl /c ..\src\*.c` then link without
`luac.obj`/`print.obj` (vcvars32; see the doc). Drop each when the underlying stub is fixed.

## 6. Critical gotchas

- **The app reads its inventory from `bin\Release\net5.0-windows\Data\ui_inventory.json`**
  (a build copy). After editing `ui-process-app\Data\ui_inventory.json`, copy it there or rebuild,
  or the viewer shows the old catalog.
- **Concurrent runs**: this is not `#iso`; the canonical exe is fine, but kill stale
  `UiProcessApp`/`lua32` before rebuilds. `lua32` children hold the temp build; relink fails if
  a process is running.
- **Checklist state**: `Data/item_checks.tsv` (windowId TAB section TAB 1/0); rejections
  `Data/rejected.tsv`; both gitignored. Runtime state: `Data/runtime_state/*.tsv`.
- **Replay completion rule**: only replays marked OK in `replay_summary.tsv` have their state
  applied (`ReplayCompleted`). A partial state is NOT applied (a deliberate guard).
- **Pair rule for art**: never copy a `.UITex` without its `.Tga`/`.dds` sibling (or vice versa);
  the loader now also picks the candidate that covers the atlas frame extent.
- **Encoding**: INI/Lua/atlas-name fields are GBK; decode with `TextFile.DecodeGameText` /
  the `TextFile` auto-detect (repo files are UTF-8).
- **Engine ops**: `--selftest`/`--audit`/`--status` are headless; `--render <stem> --out x.png`
  for one window; the GUI supports X (reject), checklist, wheel/edit/drag.

## 7. Commands

```powershell
dotnet build ui-process-app -c Release
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --audit      # ph/unresolved/oob
.venv\Scripts\python.exe tools\ui\replay_all.py                         # OK/ERR/NOENTRY
.venv\Scripts\python.exe tools\ui\ini_construct_census.py               # unhandled constructs
.venv\Scripts\python.exe tools\ui\runtime_gap_report.py                 # visual drops
```

Docs index: `docs/ui/README.md` (`UI_WIREUP_PLAN.md` has the phase ledger; `UI_RUNTIME_REPLAY.md`
the VM/replay contract; `UI_RUNTIME_GAP.md` the measured drops; `EXPERIENCES.md` the log).
