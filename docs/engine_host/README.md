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

## Tools

| Tool | Purpose |
|---|---|
| `tools/sandbox/build_sandbox.py` | Build a cropped loose mini-map from the real pak (read-only extraction; world coords preserved) |
| `tools/sandbox/run_sandbox.cmd` | Launch the feature client on the 1×1 sandbox map (title `sandbox-mini`) |
| `tools/proof/capture_window.ps1` | External window capture (DPI-aware; optional posted key) — needed for overlay proofs the engine screenshot path misses |
| `tools/proof/run_solo5min.ps1` | M1.7 five-minute solo-run driver (posted I/W/Space/1 keys + captures every 30 s) |
