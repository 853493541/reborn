# UI out-of-window (oob) fix plan

**Status:** plan for review (2026-10-07). Owner area: `docs/ui/`; viewer `ui-process-app/`.
**Problem:** the reviewer sees UI components outside the window frame. `--audit` reports
**oob=7,690** flagged elements, but the number mixes real misplacements with engine-faithful
overhang and viewer-only false positives, so it cannot be acted on as-is. Goal: make the report
classify every flagged element, fix the genuine viewer bugs, and leave the engine-faithful cases
labelled rather than "fixed" by invention.

## 1. Measured baseline (2026-10-07)

- `--audit` detail is capped at 40 items/window (`.Take(40)`, `App.xaml.cs:590`); the analysis
  below parses the first 40 of every window: **6,254 of 7,690** elements.
- Raw classes (first 40/window): scroll 1,194 · parked (negative Left/Top on the parent chain)
  1,347 · negative-pos 457 · edge-PosType 149 · not-in-ini 204 · other 2,903.
- Post-filter (excluding scroll/parked/runtime clones): **container-overhang 1,548 ·
  leaf-content 1,204 · decor-art 708**.
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

Only A and B are bugs. C is a data property of the shipped INI; D is measurement noise. A blanket
clip-to-frame fix is **not** in this plan: the engine's own scripts park dropdowns/tooltips outside
frames on purpose, so clipping everything would invent behavior.

## 3. Phases

**P0 — honest measurement (0.5-1 session).**
1. Raise/remove the 40/window audit cap; keep the report readable with a per-class summary.
2. Classify every flagged element into A/B/C/D:
   - D: walk the parent chain for a clipping control (WndScroll/WndNewScrollBar, `$Clip`).
   - B: element is runtime-hidden or a list prototype (the runtime TSV's `Clear`/`AppendItemFromIni`
     receivers), or its `LockShowAndHide`/`show` state says hidden.
   - A vs C: compare the rendered rect with the plan's expected rect (the layout pass already
     computes the intended position/size; carry it through the build, or recompute from the plan).
3. Output: `actionable=A+B` separate from `overhang=C` and `clipped=D`, plus the A/B per-window
   lists. This turns the current 7,690 into a work queue.
4. Tool placement: extend `App.xaml.cs --audit` (viewer math is there) or add
   `tools/ui/oob_report.py` for offline classification; register in `docs/ui/README.md`.
5. Acceptance: the audit prints the four classes; the A/B lists are stable across runs.

**P1 — engine clip/size truth (0.5 session, alongside P0).**
1. Determine which control classes clip in the engine (WndScroll is confirmed by prior evidence;
   check WndPage/WndPageSet/WndFrame in the renderer path) and whether the page-set sizes to the
   frame or vice versa (QuestTraceList/MailPanel cases). Read-only disassembly, RVAs cited.
2. Confirm the `AutoSize` default for frames/pagesets in `DecodeItem` (struct default) so C's
   "expected" rect uses the engine's real sizing.
3. Deliverable: a short evidence note (this doc + `UI_RUNTIME_REPLAY.md` if it changes the shim).

**P2 — fix class B, hidden-but-shown (1-2 sessions).**
1. Group the parked/prototype hits by window and cause:
   - windows whose replay is NOENTRY/ERR (no state to apply) — check whether the authored parked
     popups are engine-visible at rest or script-hidden; document data-blocked ones;
   - runtime popups the script hides via `Show(false)`/`Hide` — verify the guard applies them;
   - authored list prototypes with runtime clones — verify `Clear`/deferred-clear removed them.
2. Fix the state application systemically (the guards in `LayoutPlanBuilder.ApplyRuntimeMutations`
   and `MainWindow`'s hide/collapse logic), never per window.
3. Acceptance: parked-visible count falls to the set the scripts never hide; each remaining hit
   carries a class-B reason in the audit.

**P3 — fix class A, viewer placement bugs (1-2 sessions).**
1. Group the A mismatches by cause: PosType edge cases (3/4/5/8-12), anchors (`AnchorDst`/`SetPoint`),
   flex (`WndFlexContainer`/`FlexHandle`), item flow (`FirstItemPosType`), runtime mutation
   application order.
2. Fix each cause against the engine's jump tables/RVAs; re-run P0's report per cause.
3. Acceptance: A list empty for the corpus; every fixed cause has a gate + a fingerprint sample.

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
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --audit      # ph/unresolved/oob
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
# class breakdown used for section 1 (first 40/window):
#   parse ui_process_audit.txt, join the INIs + Data/runtime_state/*.tsv,
#   walk parent chains (scroll/parked), then split the rest by element type/name
.venv\Scripts\python.exe tools\ui\runtime_gap_report.py                 # dropped calls stay 1
```

**Confidence:** baseline counts HIGH (tool output 2026-10-07); class split MED (first 40/window
sample; P0 removes the cap); engine AutoSize default MED (DecodeItem 0x1800b86df; P1 confirms);
clip classes MED (WndScroll HIGH from prior evidence, others to confirm).

Last verified: 2026-10-07 (`--audit` oob=7,690; 6,254 parsed; post-filter container 1,548 /
leaf 1,204 / decor 708; 1,123 root frames without AutoSize; 66 script-sized roots).
