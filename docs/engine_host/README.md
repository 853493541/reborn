# docs/engine_host - engine host index

MovieEditor engine-hosting research and the M1 milestone docs.

| Doc | Title |
|---|---|
| `ENGINE_HOST_PLAN.md` | Engine Host Plan 鈥?JX3 MovieEditor playback core |
| `CLIENT_STACK_PIVOT.md` | Client Stack Pivot 鈥?host the game client's own engine (2026-09-30) |
| `SFX_WIRING_PLAN.md` | SFX Wiring Plan 鈥?retire the free-standing dummy effect (2026-09-30) |
| `M1_ACTOR_ON_MAP.md` | M1.2 鈥?animated actor on a real map (PASSED) |
| `M1_STATUS.md` | M1 status 鈥?one player on the real map |
| `CLIENT_PROVENANCE.md` | Client code provenance audit 鈥?original vs game-derived (2026-09-29) |
| `MINI_SANDBOX_CLIENT.md` | Mini Sandbox 鈥?cropped loose map for feature work (verified) |
| `CLIENT_CHARACTER_PLAN.md` | Client character plan 鈥?visible player model on the real client engine (active, 2026-10-04) |
| `NEXT_AGENT_HANDOFF.md` | Next-agent handoff - Gate 1 RL table chain state, current blocker (lua file layer) and next probes (2026-10-06) |

## Tools

| Tool | Purpose |
|---|---|
| `tools/sandbox/build_sandbox.py` | Build a cropped loose mini-map from the real pak (read-only extraction; world coords preserved) |
| `tools/sandbox/run_sandbox.cmd` | Launch the feature client on the 1脳1 sandbox map (title `sandbox-mini`) |
| `tools/engine_host/client_sfx_probe.cpp` + `build_client_sfx_probe.cmd` | Client-stack recon probe: boots the client engine (window hook + game file layer), creates a real `.Sfx`, maps the SFX play path (see `SFX_WIRING_PLAN.md`) |

