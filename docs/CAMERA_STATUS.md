# Camera status — JX3 follow model (port of the camara-imp branch)

> **Update 2026-09-23:** the `camara-imp` branch camera (`engine_host_spike/CameraSystem.cs`)
> has been ported into the client (`client/CameraSystem.cs`, `client/camera.json`) and
> supersedes the experimental model below. See `docs/netcode/REBORN_CAMERA_SPEC.md` §8
> for the final integration. `client/GameCamera.cs` was removed.

**Date:** 2026-09-23
**Code:** `client/GameCamera.cs` + camera block in `client/RebornClient.cs`
**Default right now:** legacy map-host camera (`RC_CAM_LEGACY=1`); the JX3 model is
`RC_CAM_LEGACY=0` and is still experimental.

## What is implemented

| Piece | State |
|---|---|
| `GameCamera`: yaw/pitch/dist state, spherical offset, time-based smoothing with dead-zone snap, verified defaults (SmoothTime 1.0, MaxDeltaYaw 2π, MaxDeltaPitch 1.56, dead zone 0.26) | done |
| Free-look input: click to lock cursor, mouse deltas → engine `ROTATE_CAMERA` + our state, Esc unlock, wheel zoom | done |
| Engine orbit calibration (`RC_ORBIT_TEST`): yaw ≈ 0.0018 rad/px, pitch ≈ 0.00121 rad/px | measured |
| View-direction tracking (`measureView` nudge) to correct drift, 100 ms + after every orbit | done |
| Terrain pull-in + height clamp for the camera | done (legacy proven; new model conservative) |
| Movement-reactive pitch/yaw, sprint pull-back | parameter hooks only — this build's verified defaults are 0/disabled |

## What was learned (blockers)

1. `SetCameraPos(x,y,z,bool)` sets **position only**. The 4th flag ("aim") does not
   make the engine track the player after an orbit — the player leaves the frame.
2. The **engine chase camera** (`KG3DEngineX64.dll`, `[Camera]` ini) acts on the
   camera each frame: when our position lands inside terrain it pulls the camera
   along the **view direction**, sometimes past the player (camera flips to the
   front). Its config (`fChaseRate`, `bObstructdAvert`, `fMaxDistance`, …) is not
   shipped in this build, so it runs on compiled defaults.
3. `ROTATE_CAMERA` is an **orbit around the engine's own target**, so the
   pixel→radian rate can depend on the target distance; pure integration drifts,
   hence the `measureView` closed loop.
4. Per-mode camera krl rows are absent from this client build — only the verified
   defaults are real data.

## Next step (recommended)

Recon the **native camera interface** — `IKG3D_Camera` / `IKG3DCameraProxy` /
`KG3D_CAMERA_OPTION_PROXY` are present in `MovieEngineCLR.dll` RTTI. The managed
wrapper only exposes position + editor actions; the game camera needs the engine's
own SetPos/SetLookAt/pitch and its obstruction path. Same method as the actor recon
(`tools/dump_il_actor.fsx` style): exports/vtable scan + a small probe that walks
from `KGSceneCLR.m_pScene` to the camera object.

Alternative (less faithful): keep fighting the managed path with a stronger
closed loop and our own obstruction raycast — but that is reimplementation, not the
original engine camera.

## Evidence

- `RC_ORBIT_TEST=1` log (calibration + pitch support).
- `RC_CAM_DEBUG=1` log (camera/player/measured-direction angles showing the flip).
- `reborn_out/rc_0{0,1,2}_*.png` (legacy vs new model runs).
