# UI interaction replay — the missing system

**Status:** design + proven core (2026-10-04); drag/scroll/popup wiring (2026-10-07, §6).
**Why:** the static render cannot exercise tabs,
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
   `--click bigbagpanel Btn_Bank OnLButtonClick` → `opens=OpenBankPanel`. Opener globals are
   resolved to catalog windows through `Data/ui_window_aliases.tsv` (the scripts' own `SETGLOBAL`
   definitions, §6); cross-module `SomePanel.OpenWindow(...)` proxy calls are recorded too, so
   single-module replays no longer lose those chains.
3. Item-level clicks with the row index (the list templates give the geometry).
4. Show the interactive handler count in the AssetNote.

**Lesson (2026-10-04):** distinguishing "PascalCase method" from "PascalCase property" via verb
prefixes broke the batch (property proxies caused `number < table` errors — Lua 5.1 order
comparisons between different types error regardless of metatables — and one script hung).
Reverted to the known-good shim: unknown lowercase fields → permissive proxies, PascalCase → callable
proxies, UI numeric properties → 0. Keep mixed-type comparisons out of the stubs.

## 6. Engine event contract + drag/scroll/popup wiring (2026-10-07)

**Engine contract (KGUIX64.dll, read-only recon).** `KItemEventMgr` drives the item family: a press
iterates the hit items and fires `OnItemLButtonDown` (0x180153590, call at 0x1801536a3; `this` = the
item, no Lua args). Mouse-up fires `OnItemLButtonUp` (0x180153850, item flag bit 2) then
`OnItemLButtonDragEnd` (item drag flag 0x80000, after the engine's `ReportClickData`), and
`OnItemLButtonClick` for clickable items (flag 0x10) when no drag flag is set (0x180153bbe).
`OnItemLButtonDrag` fires on mouse-move once the cursor passed the ~3px threshold (0x180158920:
distance² > 0xa) with `this` = the pressed item. The frame drag family fires on the control the
script registered with `RegisterLButtonDrag` (recorded as `$DragRegistered`, e.g. BigBagPanel's
`Btn_Drag`): `OnDragButtonBegin`/`OnDragButton`/`OnDragButtonEnd` with `this` = that control; the
handlers read `Station.GetMessagePos()` and resize/move the frame. Event payload arrives as the
globals `arg0..argN` (KGUIX64 0x1801b2690 stores `arg0`; 0x5A7760 is the `arg0..arg8` name array)
with `this` = the sender control.

**Shim (replay_server.lua).**
- `this` = the event's section; `arg0`/`arg1` = the EVENT payload tokens (numeric tokens become
  numbers). The old `arg1 = target` convention is gone.
- `MOUSE x y` / `CLIENT w h` commands feed `Station.GetMessagePos` / `Station.GetClientSize` (the
  scripts clamp and delta drags with them).
- Section proxies are memoized per section: engine controls are stable objects, so state a handler
  stores on `this`/child handles (`OnDragButtonBegin`'s `fDragX/fDragY/fFrameW`, `bOnDraging`)
  survives to the next event. Without this the resize deltas were 0.
- `RegisterScrollControl` (scroll.lua) is recorded in the mutation log; the viewer stores the
  binding's content handle as `$ScrollTarget` and maps the wheel/thumb offset onto it.
- Cross-module `SomePanel.OpenWindow(...)` calls on unresolved global proxies are recorded as window
  intents (single-segment `_G` names only).

**Viewer (MainWindow.xaml.cs).**
- A press arms the sequence by section role: scrollbar thumb (a press on the **bar** —
  `WndNewScrollBar` or a viewport's `SlideBtn` — and only when its content is known, i.e. a
  `RegisterScrollControl` binding or an authored `ScrollHandle`; a `WndScroll` is the viewport
  and its children are content, so a press there stays an item/click — 1026 shipped scroll
  controls carry no resolvable handle and are left alone), drag handle
  (`$DragRegistered`/`$DragEnabled` → `OnDragButtonBegin`), item (`Box_*`/`__lt_*` →
  `OnItemLButtonDown`). Release finishes it: item drag → `OnItemLButtonUp` then
  `OnItemLButtonDragEnd`; no drag → the click handler (`OnItemLButtonClick` /
  `OnCheckBoxCheck` / `OnLButtonClick`), which now fires on release like the engine. Drag dispatch
  is throttled to ~40 ms (the engine fires per move; the viewer's round trip + re-render is heavier).
- Scrollbar thumb dragging shares `_scrollOffsets` with the wheel; the wheel now also works where the
  binding comes from `RegisterScrollControl` (BigBagPanel's `Scroll_List` → `Handle_Bag_Normal`),
  not just a `WndScroll` ancestor.
- Popup chains: `Data/ui_window_aliases.tsv` (scanned from the window scripts' own `SETGLOBAL`
  openers by `tools/ui/scan_window_aliases.py`) resolves `OpenBankPanel` → `BigBankPanel`, so
  `NavigateAfterEvent` follows it.

**Headless checks.** `UiProcessApp.exe --drag <windowId> <section>` sends the engine's sequence
(Down/Drag/Up/DragEnd, or Begin/Drag/End for a `$DragRegistered` section) with `MOUSE`/`CLIENT`
lines and renders the result; `--click` is unchanged. Verified: `--drag bigbagpanel Btn_Drag` → 205
mutations (the script's own resize: `BigBagPanel SetSize 654 634`, `SetDragArea 0 0 654 40`, full
relayout); `--drag bigbagpanel Box_1` → item sequence dispatches; `--click bigbagpanel Btn_Bank
OnLButtonClick` → `opens=OpenBankPanel`.

## Reproduce

```powershell
# interactive session against the bag (type commands on stdin)
lua32.exe tools\ui\replay_server.lua ui-process-app\assets\ui\Config\Default\BigBagPanel.lua auto ui-process-app\assets\ui\Config\Default\BigBagPanel.ini
CLIENT 1920 1080
MOUSE 500 300
EVENT Btn_Drag OnDragButtonBegin
MOUSE 560 310
EVENT Btn_Drag OnDragButton
EVENT Btn_Drag OnDragButtonEnd
EVENT CheckBox_Compact OnCheckBoxCheck
STATE
QUIT

# headless drag / click through the viewer
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --drag bigbagpanel Btn_Drag --out drag.png
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --click bigbagpanel Btn_Bank OnLButtonClick --out click.png

# rebuild the opener alias index
.venv\Scripts\python.exe tools\ui\scan_window_aliases.py
```

**Confidence:** dispatch core HIGH (handler runs; delta recorded); drag contract HIGH (disassembled
call sites above); viewer pointer wiring MED (headless sequence verified; a human click is the live
check); opener aliases HIGH (the scripts' own SETGLOBAL names, 541 entries).

Last verified: 2026-10-07 (`--drag bigbagpanel Btn_Drag` → 205 mutations, `SetSize 654 634`;
`--click bigbagpanel Btn_Bank` → `opens=OpenBankPanel`; replay 1201 OK / 1 ERR / 9 NOENTRY).
