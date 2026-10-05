# docs/engine_host - engine host index

MovieEditor engine-hosting research and the M1 milestone docs.

| Doc | Title |
|---|---|
| `ENGINE_HOST_PLAN.md` | Engine Host Plan — JX3 MovieEditor playback core |
| `M1_ACTOR_ON_MAP.md` | M1.2 — animated actor on a real map (PASSED) |
| `M1_STATUS.md` | M1 status — one player on the real map |
| `M1_SOLO5_PROOF.md` | M1.7 — HUD overlay + five-minute solo run proof (2026-10-02) |
| `CLIENT_PROVENANCE.md` | Client code provenance audit — original vs game-derived (2026-09-29) |
| `MINI_SANDBOX_CLIENT.md` | Mini Sandbox — cropped loose map for feature work (verified) |
| `FAST_STARTUP.md` | Fast startup — the 22 s shader-DB TCP stall + `RC_STARTUP=nodb` override (deviation D7, verified) |
| `RENDERING_OPTIONS_PLAN.md` | Rendering / LOD / weather options (areas 1.8–1.10) — corpus census + improvement plan (2026-10-04) |
| `RENDERING_OPTIONS.md` | Rendering/LOD/weather option reference — P0 census + read chain (adapter/UI RVAs, config.ini), open items (2026-10-04) |
| `MAP_QUALITY_TIERS.md` | Map quality tiers — what the 5 BR maps actually ship (bd+low only; 2026-10-04) |

## Tools

| Tool | Purpose |
|---|---|
| `tools/sandbox/build_sandbox.py` | Build a cropped loose mini-map from the real pak (read-only extraction; world coords preserved) |
| `tools/probe_map_quality.py` | Probe which declared map quality tiers actually ship (bd/bddnc/mb/low) across the 5 BR maps |
| `tools/sandbox/run_sandbox.cmd` | Launch the feature client on the 1×1 sandbox map (title `sandbox-mini`) |
| `native/build_startup.cmd` | Build `bin64\startup_shim.dll` (RC_STARTUP=nodb startup override, deviation D7) |
| `tools/proof/capture_window.ps1` | External window capture (DPI-aware; optional posted key) — needed for overlay proofs the engine screenshot path misses |
| `tools/proof/run_solo5min.ps1` | M1.7 five-minute solo-run driver (posted I/W/Space/1 keys + captures every 30 s) |
| `tools/render/preset_census.py` | Parse the 15 shipped graphics presets + editor config → `proof/render/option_matrix.tsv` / `varying.tsv`; deterministic census |
