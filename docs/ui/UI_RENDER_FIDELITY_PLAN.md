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
| **Engine semantics** | PosType 8 right-align vs explicit Left; PosType 9/10/11/12; `AnchorDst`/`SetPoint` anchors; item-flow (`FirstItemPosType`) and list handles (HandleType 3/6) laying their own items; nine-slice (`ImageType 10`) and horizontal three-slice (`ImageType 11`); `ShapTexture` masks; scroll-viewport clipping | per-case PosType handling + `AnchorArgs` (`ApplyAnchors`); item-flow/list layout in `UiLayout`; `NineSliceImage`/`HorizontalSliceImage`; `clip` adjust; fixes: `1d44d2b`, `9a10650`, `b338aa1`, `790a5c1` |
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
- One sample row per T-A runtime list (extends `lists`; sample data marked placeholder).
- Multi-INI append (TargetCommon) so the target frame is complete.
- Generalize scroll clipping (`clip`) to registered scroll content.
- Runtime-shell windows (raid/chat/social lists) get the "runtime-filled" marker instead of a fake row.

**P4 — render sanity checks.**
- Off-window parked-element detector (authored position far outside the frame and not script-placed).
- Top-level sibling overlap detector (rendered bbox intersections).
- Empty-render detector (content ratio below threshold) → auto-suggest "runtime-filled" marker.
These feed P1's status and give the reviewer an objective "this render is suspicious" signal.

**P5 — depth dashboard.**
Stage-level counters (T-A recognized / T-A blocked / T-B marked / T-C) from the status file, printed
by `--status`, so "how deep do we need to go" has a number.

## Reproduce

```powershell
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest      # must stay 1240/0/0
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --audit         # placeholder/unresolved/outOfBounds
```

**Confidence:** retrospective HIGH (commit history + `docs/EXPERIENCES.md` entries cited);
tier scopes MED (to be confirmed by the reviewer); P1-P5 are proposals.

Last verified: 2026-10-04 (`--selftest` 1240/0/0; `--audit` placeholders=899 unresolved=3262
outOfBounds=6533).
