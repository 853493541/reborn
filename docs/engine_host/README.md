# docs/engine_host - engine host index

MovieEditor engine-hosting research and the M1 milestone docs.

| Doc | Title |
|---|---|
| `ENGINE_HOST_PLAN.md` | Engine Host Plan — JX3 MovieEditor playback core |
| `CLIENT_STACK_PIVOT.md` | Client Stack Pivot — host the game client's own engine (2026-09-30) |
| `SFX_WIRING_PLAN.md` | SFX Wiring Plan — retire the free-standing dummy effect (2026-09-30) |
| `M1_ACTOR_ON_MAP.md` | M1.2 — animated actor on a real map (PASSED) |
| `M1_STATUS.md` | M1 status — one player on the real map |
| `M1_SOLO5_PROOF.md` | M1.7 — HUD overlay + five-minute solo run proof (2026-10-02) |
| `CLIENT_PROVENANCE.md` | Client code provenance audit — original vs game-derived (2026-09-29) |
| `MINI_SANDBOX_CLIENT.md` | Mini Sandbox — cropped loose map for feature work (verified) |
| `CLIENT_CHARACTER_PLAN.md` | Client character plan — visible player model on the real client engine (active, 2026-10-04) |
| `FAST_STARTUP.md` | Fast startup — the 22 s shader-DB TCP stall + `RC_STARTUP=nodb` override (deviation D7, verified) |
| `RENDERING_OPTIONS_PLAN.md` | Rendering / LOD / weather options (areas 1.8–1.10) — corpus census + improvement plan (2026-10-04) |
| `RENDERING_OPTIONS.md` | Rendering/LOD/weather option reference — P0 census + read chain (adapter/UI RVAs, config.ini), open items (2026-10-04) |
| `MAP_QUALITY_TIERS.md` | Map quality tiers — what the 5 BR maps actually ship (bd+low only; 2026-10-04) |
| `LOD_CULL_MATRIX.md` | LOD / culling per-option caps matrix (1.10) — one key per run, house+vista fingerprints (2026-10-05) |

## Tools

| Tool | Purpose |
|---|---|
| `tools/sandbox/build_sandbox.py` | Build a cropped loose mini-map from the real pak (read-only extraction; world coords preserved) |
| `tools/probe_map_quality.py` | Probe which declared map quality tiers actually ship (bd/bddnc/mb/low) across the 5 BR maps |
| `tools/sandbox/run_sandbox.cmd` | Launch the feature client on the 1×1 sandbox map (title `sandbox-mini`) |
| `native/build_startup.cmd` | Build `bin64\startup_shim.dll` (RC_STARTUP=nodb startup override, deviation D7) |
| `tools/engine_host/client_sfx_probe.cpp` + `build_client_sfx_probe.cmd` | Client-stack recon probe: boots the client engine (window hook + game file layer), creates a real `.Sfx`, maps the SFX play path (see `SFX_WIRING_PLAN.md`) |
| `tools/proof/capture_window.ps1` | External window capture (DPI-aware; optional posted key) — needed for overlay proofs the engine screenshot path misses |
| `tools/proof/run_solo5min.ps1` | M1.7 five-minute solo-run driver (posted I/W/Space/1 keys + captures every 30 s) |
| `tools/render/preset_census.py` | Parse the 15 shipped graphics presets + editor config → `proof/render/option_matrix.tsv` / `varying.tsv`; deterministic census |
| `tools/render/key_offsets.py` | Extract config-key → engine-struct offset candidates from the annotated disasm captures → `proof/render/key_offsets.tsv` (290 keys, self-checked) |
