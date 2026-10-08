# UI out-of-window (oob) fix plan

**Status: COMPLETE (2026-10-07).** P0/P1/P2/P3/P4 done, P5 gates green; all measured viewer
placement bugs fixed (`placed-wrong=0`, oob 7,690 -> 6,897). The remaining oob is engine-faithful
(clipped WndScroll content, script/authored overhang) and the edge-pos review queue; overhang
policy = GUI clips to the window frame (audit still measures off-window content). Owner area:
`docs/ui/`; viewer `ui-process-app/`.
**Problem:** the reviewer sees UI components outside the window frame. `--audit` reports
**oob=7,690** flagged elements, but the number mixes real misplacements with engine-faithful
overhang and viewer-only false positives, so it cannot be acted on as-is. Goal: make the report
classify every flagged element, fix the genuine viewer bugs, and leave the engine-faithful cases
labelled rather than "fixed" by invention.

## 1. Measured baseline (2026-10-07)

**P0 is DONE** — `--audit` now writes every flagged element with its class
(`oob[class] name (x,y wxh)`) and a `TOTAL oob classes:` line (no more 40/window cap), and the
`placed-wrong` detector (rendered vs the script's own `SetRelPos`/`SetAbsPos`) separates real
viewer bugs from script-faithful overhang. After the P3 fixes (below), **oob=6,897** and
**placed-wrong=0**:

| class | count | meaning |
|---|---|---|
| `overhang` | 3,241 | expected placement outside the frame; the engine draws it (C) |
| `clipped` | 1,683 | under a `WndScroll` viewport / `$Clip` — the viewer clips it (D) |
| `parked` | 1,597 | authored/script off-window (C after the Selfie finding) |
| `edge-pos` | 374 | edge-anchored PosType 3/4/5/9-12 (review) |
| `placed-wrong` | **0** | rendered ≠ the script's own position — real viewer bugs (A), all fixed |
| `clone` | 2 | runtime item clone (`__lt_*`) |

**`edge-pos` review queue (374).** None of these carry a script `SetRelPos`/`SetAbsPos` (checked:
0/374), so they are authored/anchor-driven placements with no independent verifier — the placement
detector deliberately skips PosType 3/4/5/9-12 (their final position is the engine's anchor math).
Reviewing them would require reimplementing that math (circular) or a GT capture, so they stay a
documented review queue, not a fix step.

**P3 first fix (2026-10-07): `FormatAllItemPos` no-runtime-items guard.** `UiLayout` flowed a
`$FormatItems` container's *authored* children when no runtime items were appended (the engine's
list is empty, so the call is a no-op). BigBagPanel's `Handle_Bg` decoration images were stacked
into a row at x≈1927 and the bag's right-side controls (Btn_Drag/Scroll_List/…) were dragged to
y=0 — outside the window. Guarding the flow removed **813 oob elements** (7,690 → 6,877) and 8
`placed-wrong`; Btn_Drag is back at its scripted (580,610). Remaining `placed-wrong` (6) and the
observed pattern:
- **SetAbsPos with an offset parent — FIXED (2026-10-07).** The engine's `LuaWindow_SetAbsPos`
  (KGUIX64 0x1801c36a0) subtracts the **window root** origin before setting (`subss xmm7,[rbx+0x24]`),
  so `SetAbsPos` is window-root-relative, while `LuaWindow_SetRelPos` (0x1801c3490) calls the
  parent-relative core directly. The viewer treated both as parent-relative, so Collection
  `Image_BottomBg` (`SetAbsPos 0 -40`, parent `Handle_BottomBg` at y≈1027) rendered at y=987 and
  CreditsPanel `Image_CreditsPanelBg` (`SetAbsPos 0 0`, parent `Handle_All` at -300,-28) at
  (-300,-28). Fix: `LayoutPlan` stores `$AbsPos=x,y`; `UiLayout.Attach` resolves it against the
  window root. 59 `SetAbsPos` calls across 43 windows. `placed-wrong` 6 → 4.
- **SetRelPos with one axis mismatched — FIXED (2026-10-07).** The plan held the right value
  (`Wnd_Thumb plan=-399.5,-110`) but the render showed 0 on the fractional axis: `UiLayout.Attach`
  read `Left`/`Top` with `GetInt`, whose `int.TryParse` fails on `-399.5` and returns 0. All 4 cases
  had one fractional coordinate (`-399.5`, `-385.5`, `-157.5`, `-248.5`); 12 fractional position
  calls exist corpus-wide. Fix: position reads (`Left`/`Top`/`ImageRelX`/`ImageRelY`, incl. the
  list first-item origin and tab strip) now use `GetDouble`. `Wnd_Thumb` renders at (-400,-110).

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

**P1 — engine clip/size truth — DONE (2026-10-07).**
- **No general clip property exists.** No `Clip` INI key in the 1,240 shipped INIs; no control
  `Clip`/`SetClip` in the KGUI strings (only animation clips and clipboard). The only masking keys
  are `ShapTexture` (1,073), `AlphaShap` (697), `ShapTextureTop/Bottom` (486/486) and `ReverseMask`
  (129) — alpha masks, which the viewer already applies (MiddleMap's map mask), not hard clips.
- Therefore **WndScroll is the only hard clipping control** (viewport clip, prior evidence). Frames,
  pages and page-sets do **not** clip: the remaining `overhang`/`parked` classes are what the engine
  itself draws (class C). This is why a blanket clip-to-frame fix is rejected — it would hide content
  the engine shows.
- `AutoSize` default: the INI decoder (`KUiComponentsDecoder::DecodeItem` 0x1800b86df) writes the
  parsed value into the auto-size field, and `Width`/`Height` clear it — absent = explicit size
  (MED). 1,123 root frames carry no `AutoSize`.

**P2 — class B + overhang policy — DONE (2026-10-07).**
1. **No true B was found beyond the script's own state.** A section the script hides would not be
   flagged (hidden elements are skipped), and the `parked` queue re-classed as script/author
   faithful (C) once compared with the script's recorded positions (the Selfie sample). The residual
   no-state parked items are in NOENTRY/ERR windows (no replay to apply) — documented, not a viewer
   bug.
2. **Overhang policy: clip the GUI render to the window frame (revised 2026-10-07).** The first
   decision was keep-as-is, but the reviewer still saw "many components outside the window": the
   viewer's overhang canvas expansion was pulling the scripts' parked popups/slide bars and the
   authored overhang into view. The GUI now sizes the canvas to the window rect and sets
   `ClipToBounds`, so the window renders cleanly; `--audit` still measures and classes the
   off-window content. (Headless `--render`/`--click` keep the overhang expansion for inspection.)
3. Acceptance met: every remaining hit is labelled clipped (D), script/author overhang (C), or
   edge-pos review; the policy is decided and recorded.

**P3 — fix class A, viewer placement bugs (1-2 sessions).**
1. Implement the A detector from section 2: rendered position vs the script's own last
   `SetRelPos`/`SetAbsPos`/`SetPoint` per section. Emit `oob[placed-wrong]` where they disagree;
   the rest of `parked`/`overhang` is C.
2. Group the A mismatches by cause: PosType edge cases (3/4/5/9-12), anchors
   (`AnchorDst`/`SetPoint`), flex (`WndFlexContainer`/`FlexHandle`), item flow
   (`FirstItemPosType`), runtime mutation application order.
3. Fix each cause against the engine's jump tables/RVAs; re-run the report per cause.
4. Acceptance: A list empty for the corpus; every fixed cause has a gate + a fingerprint sample.

**P4 — script/page-driven sizing — DONE (2026-10-07).**
- The root-size consumer works: of the windows whose script sets the root size, every parseable
  value is applied (BigBagPanel 594x624, etc.). The precise root-`SetSize` set is 10 windows:
  **6 render exactly at the scripted size**, 4 carry degenerate stub values (Bullet/Teaching
  0,0; EditBox width -4; Navigator -200,-230) where the viewer keeps the authored/fallback size —
  a stub-session artifact (the script computed the size from absent session data), not a viewer bug.
- Page-level `SetSize` (QuestTraceList's `Page_Achi 300x600`) is page content, not a window resize:
  the window keeps its authored 300x550 and `Image_Bg`'s 300x600 is authored overhang (C).
- Acceptance met: captured sizes apply; degenerate/absent data is documented, not faked.

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

Last verified: 2026-10-07 (`--audit` oob=6,897 → overhang=3,241 clipped=1,683 parked=1,597
edge-pos=374 clone=2, **placed-wrong=0**; the flow-guard fix removed 813, then the SetAbsPos and
fractional-position fixes cleared all 14 A bugs; 1,123 root frames without AutoSize; 66
script-sized roots; `--selftest` 1240/0/0).
