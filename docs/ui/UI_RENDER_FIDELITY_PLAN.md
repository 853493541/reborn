# UI render fidelity — retrospective + plan

**Status:** plan for review (2026-10-04). Owner area: `docs/ui/`; viewer code: `ui-process-app/`.
**Problem it answers:** the reviewer's loop is too slow (1,240 windows, one by one), some renders
look "unreasonable" so need-vs-not cannot be judged, and there is no defined depth at which
fidelity is "enough". This doc records what made early renders "out of place", what was changed,
and the proposed plan + fidelity contract.

## 1. Retrospective — what was missing at first

The KGUI INI is the **authored/initial prototype state**, not the runtime state. Early renders looked
"out of place" because three layers were absent:

| layer | what was missing | what fixed it (examples) |
|---|---|---|
| **Engine semantics** | PosType 8 right-align vs explicit Left; PosType 9/10/11/12; `AnchorDst`/`SetPoint` anchors; item-flow (`FirstItemPosType`) and list handles (HandleType 1/2/3/6 share the engine's row/list path — dispatcher `0x1801064f5`, post pass `0x180106cc0`, evidence `proof/ui/evidence/re/kgui_handle_layout.txt`; 4/5 distinct, unhandled); nine-slice (`ImageType 10`) and horizontal three-slice (`ImageType 11`); `ShapTexture` masks; scroll-viewport clipping | per-case PosType handling + `AnchorArgs` (`ApplyAnchors`); item-flow/list layout in `UiLayout`; `NineSliceImage`/`HorizontalSliceImage`; `clip` adjust; WndScroll viewport clip; fixes: `1d44d2b`, `9a10650`, `b338aa1`, `790a5c1`, `98b619f` |
| **Runtime state** | all `Page_*` variants rendering at once; `LockShowAndHide=1` sections hidden at load (script shows them); runtime popups authored visible (dropdowns/tooltips/edit boxes); scripted `SetSize`/`SetRelPos`/`SetText`/tab state; runtime rows/slots created by Lua; unclipped window overhang | page chain filter + per-window `page`; `ApplyLockedVisibility` + `show` lists; `hide` lists; inventory overrides `adjust`/`anchors`/`texts`/`tabs`/`lists`/`appends`/`images`/`overlay`; overhang canvas growth; fixes: `0ba42a3`, `8a86e20`, `6327f18`, `790a5c1` |
| **Resources** | missing textures → placeholder; `.UITex` atlas frame-group parser bug; unresolved `$Text` keys; GBK text; fonts | referenced-atlas + `.UITex` sibling-texture extraction (2,479 files, 1.5 GB); parser fix `cb2ae74`; per-window StringTables + global `g_tStrings`; shipped fonts; fixes: art pass + `6327f18`, `9c00de5`, `8f6594e` |

Root cause in one line: **"out of place" was never one bug** — it was the authored prototype being
rendered without (a) the engine's placement/slicing rules, (b) the script's state mutations, and
(c) the referenced art/text. The viewer now replays (a) and (b) via a per-window override layer
(`adjust`/`anchors`/`texts`/`tabs`/`hide`/`show`/`lists`/`appends`/`images`/`page`) and (c) via the
extracted assets.

## 2. Fidelity contract — how deep it needs to go

| tier | scope | requirement | acceptance |
|---|---|---|---|
| **T-A core** | the default interface a player uses daily (current `liked` + `推荐`; ~80-120 windows) | authored layout + scripted state replay (geometry/visibility/text/tabs/pages) + real art/text; runtime rows may be replayed as one sample row | recognizable at a glance; no misleading overlap/empty/out-of-place; user can answer "needed?" |
| **T-B functional** | the rest of the default interface (功能面板/菜单与交互 remainder) | authored layout + real art/text + explicit "runtime-filled" marker where content is runtime | chrome + function readable; marker prevents false rejection |
| **T-C catalog** | mode/minigame/one-off windows (其他界面/其他模式与入口) | catalog facts + thumbnail only | reviewed by description, not render; never blocks the review |

**Stop rule:** review is done when every T-A/T-B window is judged (liked or 不需要); T-C needs no
render review. Fidelity work is prioritized only for T-A, then T-B.

## 3. Plan

**P1 — status scan + badges (attacks "unreasonable → can't judge").**
Headless scan (`--status`) writing `Data/render_status.tsv` per window: sections; placeholder/
unresolved counts (from `--audit`); root 0x0/parked; `LockShowAndHide` count; pages without a
default; runtime-shell heuristics (no visible leaf art, or a single runtime host); content ratio.
Viewer shows the badge in the status line and as a tooltip in the tree, so the reviewer knows what
they are looking at ("authored shell — runtime content", "placeholders n", "pages n default X").

**P2 — contact sheet (attacks "at this rate it will take forever").**
`--contact-sheet <stage> [--out sheet.png] [--cols N]`: render every window of a stage as a
thumbnail grid with `X.Y` number + name. Triage 20-40 windows per sheet, open only the plausible
ones in the viewer. Native WPF (no web), reuses the existing render path.

**P3 — runtime replay for T-A (attacks "some are unreasonable").**
- One sample row per T-A runtime list (extends `lists`; sample data marked placeholder). **TODO** —
  per-panel template research; the status scan now quantifies the gap (615 windows carry runtime hosts).
- Multi-INI append (TargetCommon) so the target frame is complete. **DONE** — inventory `appendIni`
  + `LayoutPlanBuilder.ApplyAppendIni`; `target-frame` renders TargetPlayer10 + TargetCommon's
  `Handle_TM` subtree under `Handle_Energy` (sample kungfu 唐门; other class handles selectable the
  same way).
- Generalize scroll clipping (`clip`) to registered scroll content. **TODO** — per-window `clip` is in
  place (bag); auto-detection needs the Lua scroll registrations.
- Runtime-shell windows (raid/chat/social lists) get the "runtime-filled" marker instead of a fake row.
  **DONE** — `--status` flags (`shell`, `runtime-hosts=N`, `pages=n no-default`) shown in the viewer.

**P4 — render sanity checks.** *(not started)*
- Off-window parked-element detector (authored position far outside the frame and not script-placed).
- Top-level sibling overlap detector (rendered bbox intersections).
- Empty-render detector (content ratio below threshold) → auto-suggest "runtime-filled" marker.
These feed P1's status and give the reviewer an objective "this render is suspicious" signal.

**P5 — depth dashboard.** *(partially delivered by P1's stage totals)*
Stage-level counters (T-A recognized / T-A blocked / T-B marked / T-C) from the status file, printed
by `--status`, so "how deep do we need to go" has a number.

## Completion check (2026-10-04, P1+P2+P3 attempted)

- `--status` → `ui-process-app/Data/render_status.tsv`, 1,240 windows: **placed=1240, shell=134,
  runtime-hosts=615**. Per stage: recommended 48 (shell 5 / hosts 31), liked 53 (7/35), ui 11 (1/6),
  hud-ui 45 (5/13), panels-ui 64 (5/25), menus-ui 24 (1/11), other-ui 780 (86/392), modes-ui 211
  (24/100), spare 4 (0/2). Viewer shows `ph=/str=/oob=` + flags in the AssetNote line and the tree
  tooltip; the status bar reports `status=<rows>`.
- `--contact-sheet recommended` → 14 windows (34 reviewer-rejected hidden), numbering matches the
  viewer (`1.1`-`1.14`), each cell labelled `size / sections / ph / str`.
- `target-frame` render: 36 → 46 sections (TargetCommon `Handle_TM` appended), energy bar visible.
- `--selftest` 1240/0/0. Reviewer rejections are respected (side file; `originalStageId` refreshed
  for windows the catalog later moved to `liked`).
- Remaining for a follow-up: P3 sample-row replay per T-A list (615 runtime-host windows),
  generalized scroll clipping, P4 detectors, P5 tier dashboard.

## Reproduce

```powershell
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest      # must stay 1240/0/0
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --audit         # placeholder/unresolved/outOfBounds
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --status        # Data/render_status.tsv + stage totals
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --contact-sheet recommended --out sheet.png
```

**Confidence:** retrospective HIGH (commit history + `docs/EXPERIENCES.md` entries cited);
tier scopes MED (to be confirmed by the reviewer); P1/P2 DONE, P3 PARTIAL (see above).

Last verified: 2026-10-04 (`--selftest` 1240/0/0; `--status` placed=1240 shell=134 runtime-hosts=615;
`--contact-sheet recommended` 14 windows; target-frame 46 sections).
