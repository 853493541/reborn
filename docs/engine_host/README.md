# docs/engine_host - engine host index

MovieEditor engine-hosting research and the M1 milestone docs.

| Doc | Title |
|---|---|
| `ENGINE_HOST_PLAN.md` | Engine Host Plan — JX3 MovieEditor playback core |
| `CLIENT_STACK_PIVOT.md` | Client Stack Pivot — host the game client's own engine (2026-09-30) |
| `M1_ACTOR_ON_MAP.md` | M1.2 — animated actor on a real map (PASSED) |
| `M1_STATUS.md` | M1 status — one player on the real map |
| `CLIENT_PROVENANCE.md` | Client code provenance audit — original vs game-derived (2026-09-29) |
| `MINI_SANDBOX_CLIENT.md` | Mini Sandbox — cropped loose map for feature work (verified) |

## Tools

| Tool | Purpose |
|---|---|
| `tools/sandbox/build_sandbox.py` | Build a cropped loose mini-map from the real pak (read-only extraction; world coords preserved) |
| `tools/sandbox/run_sandbox.cmd` | Launch the feature client on the 1×1 sandbox map (title `sandbox-mini`) |
