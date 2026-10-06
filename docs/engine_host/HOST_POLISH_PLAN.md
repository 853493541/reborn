# Host UX / perf polish — plan (workstream D, items 1.1/1.2/1.8/1.10)

**Date:** 2026-10-05 · **Branch:** `agent/host-polish` (worktree `Desktop\reborn-iso-host-polish`, off main `8e0352a`)
**Scope:** the remaining host-polish items of system 1: clean shutdown, device/window
settings, loading screen, native option read-back, and the two 1.10 pose probes.
**Out of scope (parked):** audio (1.6), weather semantics (1.9), TrueSky/vol-cloud,
server-owned state, packaging.

## Method

- Feature build `reborn_client_hostpolish.exe` (title `sandbox-hostpolish`, own memory
  namespace); all probe runs with `RC_STARTUP=nodb`; screenshots fingerprinted with
  `tools/proof/image_stats.py`; proof under `proof/host/`; one focused commit per task.
- Boundaries (engine/install won't do it) are registered with re-open criteria — no
  guessed overrides.

## Tasks (ordered)

### D1 — Clean shutdown (1.1) — small
- Wire on `FormClosing`: `engine.UnInit3DEngine()`, `sound.UnInit()`, `baselib.UninitLog()`
  (`UnInitMemory`) in order, each guarded + logged; skip on crash paths.
- Verify: close window → process exits with no hang; log shows the shutdown lines; no
  dump; re-launch works.

### D2 — Native per-key option read-back (1.8) — probe, then tool
- Reflection probe of `KGEngineOptionProxyCLR` (public members) and the pair
  `GetEngineOptionFromConfigFile(string, out proxy)` / `GetEngineOption(ref proxy)`
  (signatures already recovered in the metadata dump).
- If a readable surface exists: `RC_OPT_DUMP=<file>` writes active key=value pairs to
  `bin64\reborn_out\active_options.ini` (documented output dir).
- Else: fall back to the adapter's config save fn (RVA `0x67B10`, render-options §2) or
  register the boundary.
- Verify: dump after `RC_QUALITY=1` vs `RC_QUALITY=9` differs in the tier keys
  (`nEngineGraphicsLevel`, shadow/fog/LOD keys) and matches `config_*_*.ini` values.

### D3 — Device/window settings (1.1) — probe, then boundary/knob
- Probe A: `RC_OPT_CanvasWidth/CanvasHeight/FullScreen` through
  `SetEngineOptionFromConfigFile` — does the engine apply `[Main]` keys at runtime?
  (Check via `image_stats` screenshot size.)
- Probe B: write `bin64\reborn_out\host_init.ini` (copied `config.default`/config_9 +
  `[Main]` overrides) and pass its path as the `Init3DEngine` config argument via a new
  `RC_INIT_CFG` env — does the window/render target change?
- Outcome: `RC_RES=WxH` / `RC_FULLSCREEN` knob if the engine honors either path; else a
  documented boundary (device size fixed from the install config at engine init) plus a
  README note. Verify with screenshots at two sizes.

### D4 — Loading screen (1.2) — implement
- New `client/LoadingOverlay.cs` (same layered top-level pattern as `HudOverlay`, shown
  before `Init3DEngine`): phase texts `Initializing engine…` → `Loading map…` →
  `Preparing scene…`, refreshed with `Application.DoEvents` between the blocking calls
  (no threads/engine calls off the UI thread). `RC_NOLOADING=1` disables it.
- Full-load path: when `RC_FULLLOAD=1`, poll `scene.GetLoadingProgress()` and show a
  percentage.
- Verify: external window captures (`tools/proof/capture_window.ps1`) during init/load
  show the overlay; phase text in the log; overlay gone after spawn (fingerprint
  compare); default runs unchanged apart from the overlay.

### D5 — Foliage-density re-check (1.10)
- Re-probe `nFoliageDensity` 0 / 100 / 999 vs baseline at the densest `.foliage` cell
  `(173747,98442)` and one more dense cell, 8×8 fingerprints; confirm the clamp
  (100 vs 999 identical) and whether density applies before map load in this host.
- Close as verified or as "engine ignores in ME host" with the two-pose evidence.

### D6 — Close-up LOD pose (1.10)
- Spawn next to a large structure/tree, zoom in (scripted `RC_CAM_ZOOMSEQ`), and probe
  `fModelLodRadius=100,100,100,100` / `nMinimumModelLod=3` with 8×8 fingerprints.
- Close as verified or as "model-LOD keys inert in ME host" (adds the close-up pose to
  the existing vista evidence in `LOD_CULL_MATRIX.md`).

### D7 — HUD perf readout (optional, small)
- Show `hitch` max (predraw metric) alongside fps in the info panel.

## Verification / definition of done

- Each task: deterministic repro command; before/after evidence (log lines + numeric
  fingerprints); regression gates: feature build exit 0, `camera_smoke` ALL PASS,
  collision selftest 36/36, gravity/`jx3_model`/loot PASS.
- Docs: this plan + `docs/engine_host/README.md` index + `docs/EXPERIENCES.md` entry;
  boundaries carry re-open criteria.

## Risks

- Resolution may be install-config-only (read-only) → boundary, not a workaround.
- Loading overlay z-order vs the engine child window: solved by the `HudOverlay`
  pattern (WS_EX_LAYERED top-level), external capture needed for proof.
- `KGEngineOptionProxyCLR` may expose no readable values (render-options note) → D2
  falls back to the adapter save fn or the boundary.

## Base note

Forked off `main` (`8e0352a`) per the team's parallel-agent convention. The
consolidated `agent/item1-completion` work (1.5-E, 1.6, 1.7, 1.8-C, 1.9, 1.10) is not in
this base; where this workstream needs it (the 1.10 matrix doc), it re-derives the
focused probe instead of depending on the unmerged branch.
