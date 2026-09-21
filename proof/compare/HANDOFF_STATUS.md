# HANDOFF STATUS (summary)

**Full doc:** [`../../HANDOFF.md`](../../HANDOFF.md) → `/workspace/jx3-ani-player/HANDOFF.md`  
**Written:** 2026-09-20 ~03:57 PT (box), from `STOP_SUMMARY.md` (~03:56 PT).  
**Lead:** stopped for usage/context.  
**Andy:** synced `C:\Users\Zhibin Ren\jx3-ani-player\HANDOFF.md` + `proof\compare\HANDOFF_STATUS.md` (machineId `3bd32f7d-8a1e-43b8-b5fd-2e413ab6ffde`).

## Done vs in-flight (short)

| Area | Status |
|------|--------|
| Materials / peach face / cyan sleeve hide | **DONE** |
| Catalog 花萝 / 风来吴山 + resolve_playable | **DONE** |
| Transport + pose_drive / PlaybackClock | **DONE** |
| Bone name map 182/182 | **DONE** (keep) |
| local_matrices contract + chrome prefer local | **DONE** (smoke green; Andy chrome landed) |
| **P0 mid-clip 花萝 pose (风来吴山·蓄力)** | **OPEN — still explodes**; map-viewer Animation Player = GT |
| Junk/bad proofs (ignore / do not SOURCES) | `flws_f0{00,25,50}`, `*_local_f*`, `*_retarget_f*`, `aniplayer_flws_pose_f000` |
| P1 bind-pose hands float | **OPEN** |
| P1 live Tk chrome merge (`todo 6`) | **OPEN** |
| P2 Dev5 SOURCES after good mid-clip | **HOLD** |
| P2 JS copies synced | keep `web/` ≡ `viewport_fbx/` |

## P0 (exact from STOP_SUMMARY)

> Mid-clip 风来吴山·蓄力 still **explodes** even after local-matrix switch. Map-viewer Animation Player shows this action **correctly** — match that path. **Exit:** mid-clip PNG of a recognizable charge pose (humanoid, no spikes).

## Run (shortest)

```bash
cd /workspace/jx3-ani-player
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
.venv/bin/python smoke_pose_drive.py
.venv/bin/python player_fbx_viewport.py --serve
(cd viewport_fbx && python3 -m http.server 8765)
.venv/bin/python player.py
# Andy: Start JX3 Ani Player.bat  or  .venv\Scripts\python.exe player.py
```
