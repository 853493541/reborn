# UI out-of-window (oob) fix plan

**Status:** plan for review (2026-10-07). Owner area: `docs/ui/`; viewer `ui-process-app/`.
**Problem:** the reviewer sees UI components outside the window frame. `--audit` reports
**oob=7,690** flagged elements, but the number mixes real misplacements with engine-faithful
overhang and viewer-only false positives, so it cannot be acted on as-is. Goal: make the report
classify every flagged element, fix the genuine viewer bugs, and leave the engine-faithful cases
labelled rather than "fixed" by invention.

## 1. Measured baseline (2026-10-07)

**P0 is DONE** — `--audit` now writes every flagged element with its class
(`oob[class] name (x,y wxh)`) and a `TOTAL oob classes:` line (no more 40/window cap).
The uncapped split of **oob=7,690**:

| class | count | meaning |
|---|---|---|
| `overhang` | 3,516 | expected placement outside the frame; the engine draws it (C) |
| `clipped` | 2,168 | under a `WndScroll` viewport / `$Clip` — the viewer clips it (D) |
| `parked` | 1,633 | authored negative Left/Top on the parent chain (B candidate) |
| `edge-pos` | 371 | edge-anchored PosType 3/4/5/9-12 (A candidate) |
| `clone` | 2 | runtime item clone (`__lt_*`) |

The earlier capped analysis (first 40/window, 6,254 elements) gave scroll 1,194 · parked 1,347 ·
negative-pos 457 · edge 149 · other 2,903, and the post-filter container 1,548 / leaf 1,204 /
decor 708 — consistent with the uncapped totals.
- Frame sizing: 1,123 root `WndFrame`s carry **no** `AutoSize` key, 95 carry `=1`, 26 `=0`.
  The engine's INI decoder (`UI::KUiComponentsDecoder::DecodeItem`, KGUIX64 0x1800b83f0; AutoSize
  xref at 0x1800b86df) writes `Width`/`Height` with the auto-size field cleared and `AutoSize`
  with the parsed value — so absent = explicit size (MED, confirm in P1). The engine does not
  grow frames to content by default.
- 66 / 1,202 replayed windows have a script-sized root (`SetSize`/`SetH`/`SetW` on the stem).
- No script-facing clip API exists in KGUI strings (only animation clips / clipboard). The viewer
  clips `WndScroll` viewports plus one inventory `$Clip` entry.
- Sampled causes (verified against the INIs):
  - **Authored overhang** (engine draws it too, no clip): CompassPanel `Handle_Images` 255×255 in
    a 236×259 frame; QuestTraceList `Image_Bg` 300×600 at (0,28) in a 300×550 frame; MailPanel
    `PageSet_Total` 535 tall in a 512 frame with `CheckBox_Receive` at y=505.
  - **Script/page-driven**: QuestTraceList's replay sizes `Page_Achi` 300×600 but not the root.
  - **Parked-but-visible**: 1,347 elements with negative authored coordinates still rendered.

## 2. Classification rules — what "fixed" means

| class | definition | disposition |
|---|---|---|
| **A. viewer placement bug** | rendered rect ≠ engine-expected rect (INI + runtime mutations + PosType/anchor math) | fix in the viewer, engine-derived only |
| **B. hidden-but-shown** | section hidden by the script/runtime state (Show(false)/Hide/Clear) or an authored list prototype, yet rendered | fix the state application (systemic) |
| **C. authored/runtime overhang** | rendered rect == expected rect, but outside the frame | keep (engine draws it; no clip API) — report separately |
| **D. clip false positive** | element under a clipping control (WndScroll, `$Clip`) | exclude from the actionable list |

Only A and B are bugs. C is a data property of the shipped INI/script; D is measurement noise. A
blanket clip-to-frame fix is **not** in this plan: the engine's own scripts park dropdowns/tooltips
outside frames on purpose, so clipping everything would invent behavior.

**P0 finding — `parked` is mostly script-parked (C), not hidden-but-shown (B).** Selfie's side bars
are authored on-screen (`Wnd_LeftBottom Top=852` in a 970-tall frame) but the replay itself moves
them off-screen: `Wnd_LeftBottom SetRelPos 0 -100`, `Wnd_RightTopFrame SetRelPos -290 0`,
`Wnd_RightBottom SetRelPos -790 -100` (Selfie.tsv) — the script slides them in/out. The viewer
applies those positions correctly, so the panels render where the script put them; in the game the
window is full-screen and y=-100 is off-screen (invisible), while the viewer's **overhang canvas
expansion** (`MainWindow.xaml.cs:1622` `ComputeOverhang`) pulls them into view. So the parked class
is C, and the visible "component outside the window" is partly the viewer's expansion affordance.

**Concrete A detector (for P3):** for each oob element, compare its rendered position with the
script's own last recorded `SetRelPos`/`SetAbsPos`/`SetPoint` for that section (and its parent
chain). Match → C (script/author-faithful); mismatch → A (viewer placement bug). This is
independent of the viewer's layout math and needs no GT.

## 3. Phases

**P0 — honest measurement — DONE (2026-10-07).**
- `--audit` no longer caps at 40/window; every flagged element is written as
  `oob[<class>] <name> (<x>,<y> <w>x<h>)`, the per-window header carries `[class=n ...]`, and the
  file ends with `TOTAL oob classes:`.
- Classifier `App.xaml.cs ClassifyOutOfBounds` (walk the `._Parent` chain):
  `clipped` (WndScroll / `$Clip`), `parked` (negative Left/Top), `clone` (`__lt_*`),
  `edge-pos` (PosType 3/4/5/9-12), else `overhang`.
- Honest caveat on A vs C: the viewer's rendered rect *is* its layout output, so an automated
  rendered-vs-expected diff would be circular. `edge-pos` (371) is the automated A-review queue;
  the remaining `overhang` (3,516) is C unless a specific section is shown to be mis-placed in P3
  against the engine's PosType/anchor rules (or a GT capture).
- Acceptance met: stable per-class totals; the file is now a work queue.

**P1 — engine clip/size truth (0.5 session, alongside P0).**
1. Determine which control classes clip in the engine (WndScroll is confirmed by prior evidence;
   check WndPage/WndPageSet/WndFrame in the renderer path) and whether the page-set sizes to the
   frame or vice versa (QuestTraceList/MailPanel cases). Read-only disassembly, RVAs cited.
2. Confirm the `AutoSize` default for frames/pagesets in `DecodeItem` (struct default) so C's
   "expected" rect uses the engine's real sizing.
3. Deliverable: a short evidence note (this doc + `UI_RUNTIME_REPLAY.md` if it changes the shim).

**P2 — fix class B + decide the overhang policy (1 session).**
1. True B is small: a section the script hides (`Show(false)`/`Hide`/`Clear`) that the viewer still
   renders would not be flagged at all (hidden elements are skipped), so re-class the `parked`
   queue by the script's own recorded position (`SetRelPos`/`SetAbsPos`): match → C, mismatch → A.
   The residual B candidates are: NOENTRY/ERR windows (283 parked items, no state to apply) and
   authored list prototypes with runtime clones.
2. **Viewer overhang policy (the user-visible complaint):** the canvas expansion reveals
   engine-off-screen content. Options: (a) keep expansion but add a "clip to frame" toggle;
   (b) expand only for positive (right/bottom) overhang and clip negative (off-screen in the game);
   (c) keep as-is and rely on the classified audit. Recommendation: (a) — review-friendly default,
   one key to see the engine's clipping. Decide with the reviewer before implementing.
3. Fix the state application systemically (the guards in `LayoutPlanBuilder.ApplyRuntimeMutations`
   and `MainWindow`'s hide/collapse logic), never per window.
4. Acceptance: each remaining parked hit is labelled script-parked/authored (C) or no-state; the
   overhang policy is chosen and documented.

**P3 — fix class A, viewer placement bugs (1-2 sessions).**
1. Implement the A detector from section 2: rendered position vs the script's own last
   `SetRelPos`/`SetAbsPos`/`SetPoint` per section. Emit `oob[placed-wrong]` where they disagree;
   the rest of `parked`/`overhang` is C.
2. Group the A mismatches by cause: PosType edge cases (3/4/5/9-12), anchors
   (`AnchorDst`/`SetPoint`), flex (`WndFlexContainer`/`FlexHandle`), item flow
   (`FirstItemPosType`), runtime mutation application order.
3. Fix each cause against the engine's jump tables/RVAs; re-run the report per cause.
4. Acceptance: A list empty for the corpus; every fixed cause has a gate + a fingerprint sample.

**P4 — script/page-driven sizing (1 session).**
1. For the 66 root-sized windows and page-sized windows (QuestTraceList), verify the replay
   captured the sizing and the viewer applied it. Where the script branch is data-dependent and the
   stub session cannot reach it, record it as runtime-data-blocked (not a viewer bug).
2. If a captured `SetSize` is dropped, fix the mutation consumer.
3. Acceptance: windows whose size the script sets render at that size where captured.

**P5 — gates + proof (0.5 session).**
- Gates: `--selftest` 1240/0/0; `--audit` class totals; replay 1201 OK/1 ERR/9 NOENTRY; census 0;
  gap drops unchanged.
- Proof: per-class before/after counts in this doc (or `proof/ui/oob_*`); one
  `tools/proof/image_stats.py` fingerprint per fixed class.
- Docs: `docs/EXPERIENCES.md` entry; `docs/ui/README.md` index; update `UI_RENDER_FIDELITY_PLAN.md`
  P3/P4 status.

## 4. Priority order

1. **P0** (measurement) — everything else needs the A/B/C/D split.
2. **P2** (class B) — the most visible wrongness (parked popups/prototypes).
3. **P3** (class A) — genuine misplacements.
4. **P4** (script sizing) — overlaps P3.
5. **P1** runs alongside P0; its result can reclassify C.

## 5. Reproduce

```powershell
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --audit      # ph/unresolved/oob + class totals
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
# the audit report now ends with e.g.
#   TOTAL oob classes: overhang=3516 clipped=2168 parked=1633 edge-pos=371 clone=2
.venv\Scripts\python.exe tools\ui\runtime_gap_report.py                 # dropped calls stay 1
```

**Confidence:** class totals HIGH (uncapped `--audit` 2026-10-07); classifier rules HIGH (direct
parent-chain walk, WndScroll clip HIGH from prior evidence); engine AutoSize default MED
(DecodeItem 0x1800b86df; P1 confirms); A-vs-C split MED (needs P3 engine-rules review or GT).

Last verified: 2026-10-07 (`--audit` oob=7,690 → overhang=3,516 clipped=2,168 parked=1,633
edge-pos=371 clone=2; 1,123 root frames without AutoSize; 66 script-sized roots; `--selftest`
1240/0/0).
