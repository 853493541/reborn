# UI interaction replay — the missing system

**Status:** design + proven core (2026-10-04). **Why:** the static render cannot exercise tabs,
checkboxes, list rows or window chains, so the reviewer can never reach the states that make the UI
"the real thing". The window scripts already contain the handlers (`OnCheckBoxCheck`, `OnLButtonClick`,
`OnItemLButtonClick`, `OnMouseEnter`, ...); we can dispatch them into the replayed module and feed
the resulting state deltas back into the render.

## 1. Architecture

```
viewer (WPF)                    replay_server.lua (lua32.exe, one per window)
  select window  ──spawn──▶     load script + INI tree + UI shim, run OnFrameCreate
  click canvas   ──EVENT──▶     set this/arg1..N, call module handler, pcall
  apply delta    ◀──MUT/END──   mutation delta (section/method/args TSV)
  re-render
```

- **Server:** `tools/ui/replay_server.lua` (same shim as the batch harness). Commands:
  `EVENT <section> <handler> [arg1] [arg2]`, `STATE`, `QUIT`. On load it prints
  `READY handlers=OnCheckBoxCheck,...` (the module's exported event handlers), so the viewer knows
  which sections are interactive.
- **Hit-testing:** the render already computes every section's rect (`build.Elements` +
  `TransformToAncestor`; `--dump` shows them). A click maps to the topmost section under the cursor;
  the handler is picked by type: `CheckBox_*` → `OnCheckBoxCheck`/`OnCheckBoxUncheck`, `Btn_*` →
  `OnLButtonClick`, item clones → `OnItemLButtonClick`, etc.
- **State overlay:** the delta is applied in memory on top of `Data/runtime_state/<stem>.tsv`
  (same method table as `ApplyRuntimeState`: SetSize/SetRelPos/SetShow/Hide/SetFrame/SetText/...),
  then the layout cache entry for that window is invalidated and the render repeats.

## 2. Proven core (2026-10-04)

`EVENT CheckBox_Compact OnCheckBoxCheck` against BigBagPanel returns `RESULT OK` and a delta:
`CheckBox_Compact Check false` · `Handle_Bag_Compact Hide` · `Handle_Bag_Compact SetSize 514 452` ·
`BigBagPanel SetSize 594 624` · `FormatAllItemPos` · `SetSizeByAllItemSize` — i.e. the client's own
checkbox handler toggles the compact/normal bag exactly as in the game. `READY` lists 29 handlers
for the bag (OnLButtonClick, OnItemLButtonClick, OnMouseEnter, OnEvent, OnFrameBreathe, ...).

## 3. Scope

- **In:** UI-state interactions driven by the scripts — tab/page switches, checkboxes, expand/
  collapse, sort buttons, item selection visuals, window open/close chains (`OpenWindow`/
  `CloseWindow`), hover/checked frames.
- **Out (stubs):** anything needing live server/player data (inventory contents, mail bodies) —
  the handlers run with the same deterministic stubs as the batch replay; data-dependent branches
  may no-op.
- **Out of the viewer by design:** animations/SFX playback, 3D scenes, web windows — the product
  runs the real engine for those.

## 4. Viewer wiring (done 2026-10-04)

- `MainWindow` starts one `replay_server.lua` process per selected window (only when the replay is
  completed per `replay_summary.tsv`); it reads the `READY handlers=…` line into the handler set and
  stops the server on window/stage change.
- A left click on the render hit-tests the built element tree (`VisualTreeHelper.HitTest` → walk up
  to a section element), picks the handler by section type (`CheckBox_*` → `OnCheckBoxCheck`,
  `Box_*` → `OnItemLButtonClick`, else `OnLButtonClick`), sends `EVENT <section> <handler>`, reads
  the mutation delta until `END`, appends it to a per-window overlay, clears the layout cache and
  re-renders. `ApplyRuntimeMutations` (shared with the on-disk state) applies the overlay.
- The server sets `this` to the **control that fired the event** (the scripts branch on
  `this:GetName()`; e.g. the bag checkbox handler compares it to `"CheckBox_Compact"`), and toggles
  the checkbox state before firing `OnCheckBoxCheck` (the engine does).
- Headless check: `UiProcessApp.exe --click <windowId> <section> [handler] [--out file.png]` spawns
  the server, dispatches the event, applies the delta on top of the runtime state and renders —
  verified: `--click bigbagpanel CheckBox_Compact OnCheckBoxCheck` → 8-step delta (`SetButtonMaxState`
  on Btn_Max, compact/normal lookups) → 145 sections rendered.
- Verified end-to-end at process level: the viewer spawns the server for the default window
  (bigbagpanel). A human click is the remaining live check.

## 5. Next steps

1. ~~Hover pass (`OnMouseEnter`/`OnMouseLeave`)~~ done: mouse-move hit-tests the section under the
   cursor and dispatches leave/enter when it changes.
2. **Window chains (recorded + followed):** the shim records all forms —
   `Wnd.OpenWindow`/`Station.OpenWindow`/bare `OpenWindow`, plus engine helpers (`OpenBankPanel`,
   `CloseXxx`) — as `opens=N` in the batch RESULT / `WINDOW <name>` lines in the server. The viewer
   appends `opens=…` to the AssetNote and, for **clicks**, follows the chain: a matching catalog
   window is selected (history push), `Close*` pops the history. Hover never navigates. Verified:
   `--click bigbagpanel Btn_Bank OnLButtonClick` → `opens=OpenBankPanel`.
   Limitation: cross-module calls (e.g. `AuctionPanel.Open()`) need that module loaded; single-module
   replays only see the globals they define plus the engine helpers.
3. Item-level clicks with the row index (the list templates give the geometry).
4. Show the interactive handler count in the AssetNote.

**Lesson (2026-10-04):** distinguishing "PascalCase method" from "PascalCase property" via verb
prefixes broke the batch (property proxies caused `number < table` errors — Lua 5.1 order
comparisons between different types error regardless of metatables — and one script hung).
Reverted to the known-good shim: unknown lowercase fields → permissive proxies, PascalCase → callable
proxies, UI numeric properties → 0. Keep mixed-type comparisons out of the stubs.

## Reproduce

```powershell
# interactive session against the bag (type commands on stdin)
lua32.exe tools\ui\replay_server.lua ui-process-app\assets\ui\Config\Default\BigBagPanel.lua auto ui-process-app\assets\ui\Config\Default\BigBagPanel.ini
EVENT CheckBox_Compact OnCheckBoxCheck
STATE
QUIT
```

**Confidence:** dispatch core HIGH (handler runs; delta recorded); viewer wiring not built yet (MED
design); handler-signature mapping MED (validated on the bag's checkbox).

Last verified: 2026-10-04 (`EVENT CheckBox_Compact OnCheckBoxCheck` → RESULT OK + 7-step delta;
READY lists 29 handlers).
