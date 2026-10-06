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

### D1 — Clean shutdown (1.1) — DONE (with a registered boundary)
- Implemented at the end of the run loop (covers window-close and `RC_AUTORUN` exits):
  `sound.UnInit()` + `baselib.UninitLog()` + `baselib.UnInitMemory()`, each guarded and
  logged; default `RC_SHUTDOWN=safe` (`0` disables, `all` adds engine, `engine` is the
  reproduction mode).
- A/B (`proof/host/d1_shutdown_ab.txt`, one run per mode): `0`/`sound`/`log`/`mem` exit
  **0**; `engine.UnInit3DEngine()` exits **-1073741819 (0xC0000005)** after `DONE` — engine
  teardown is not safe at this lifecycle point, so it is **not** called by default.
- Boundary: re-open when the editor's own close sequence (`MovieEditorHD` teardown / IL)
  or an engine fix provides safe engine uninit; then re-run the matrix.

### D2 — Native per-key option read-back (1.8) — PARTIAL (schema recovered; values = defaults)
- `RC_OPT_PROBE=1` (reflection): `KGEngineOptionProxyCLR` exposes **49 public fields**
  (`nScaleUpMode`, `fScreenSizeLimitedRate`, `nClientMdlLimit/SFXLimit`,
  `fArrModelLodRadius`, `nShadowType`, water/tex-LOD limits, foliage+SpeedTree density,
  `bEnableSubFoliage*Render`, all cull distances, ...). `RC_OPT_DUMP=<file>` writes them
  as key=value.
- A/B: `RC_QUALITY=1` vs `9` dumps are **identical and equal the defaults**, not the
  applied preset (`nShadowType=0` vs config_9 `3`; `fSpeedTreeCullDist=80000` vs
  `560000`) — the proxy is the panel/UI option object, not an authoritative engine-state
  read.
- Boundary/next probe: native DX11 `GetOption` export or the adapter save round-trip
  (render-options §2) for active values; the 49-field schema is kept (panel option
  catalog). Evidence: `proof/host/active_t1.ini`, `active_t9.ini`.

### D3 — Device/window settings (1.1) — DONE (host-side sizing; engine config arg inert)
- `RC_WIDTH/RC_HEIGHT` set the form client size, which drives the engine render target:
  the engine's own screenshot goes **1280x720 → 1600x900** (`image_stats size=`), and the
  startup line records `window: client=WxH`.
- `RC_FULLSCREEN=1` = borderless maximized: external `GetWindowRect` = **1920x1080**
  (screen), clean exit.
- `RC_INIT_CFG=<path>`: passing a **nonexistent** path still yields
  `Init3DEngine=1 ms=3141` — the old `configHttpFile.ini` argument is not read by the
  engine in this build (confirms the render-options C2 closure); resolution is
  host-window-driven, not config-driven.
- Evidence: `proof/host/size_{base,1600}.png`; logs `reborn_20261005_2147*`.

### D4 — Loading screen (1.2) — DONE
- `client/LoadingOverlay.cs`: borderless top-most **420x84** window
  (`WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW`, `ShowWithoutActivation`) with phase text; shown
  before engine init, phases `Initializing engine...` → `Loading map...` →
  `Preparing scene...` (plus a percent line under `RC_FULLLOAD`), closed before the first
  frame; `RC_NOLOADING=1` disables it.
- Verified on the **shipped 24 s init path** by window enumeration: t=8 s →
  overlay `rect=420x84 ex=0x08010088` + main window `sandbox-hostpolish`; t=33 s (after
  spawn) → overlay **gone**, main only. (The external `capture_window.ps1` attempt hung
  and produced no artifacts; the enumeration is the numeric proof.)

### D5 — Foliage-density re-check (1.10) — DONE
- At the densest `.foliage` cell `(173747,98442)`, 8×8 fingerprints (tier 9 base):
  `nFoliageDensity=0` changes **2/64** cells (foliage removed); `=100` is identical to
  the base; `=999` is identical to `=100` → the **clamp at 100** is confirmed in-host.
  Density applies pre-map-load (VideoOptions) — the earlier "no-op" was pose sensitivity.
- Evidence: `proof/host/foliage8/{base,den0,den100,den999}.png`; logs `reborn_20261005_2151*`.

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
