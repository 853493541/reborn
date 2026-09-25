# Main client camera audit (`reborn-merge`, branch `merge`)

Scope: `client/RebornClient.cs`, `client/CameraSystem.cs`,
`client/CameraSettings.cs`, `client/camera.json` as of 2026-09-24 (worktree
carries the pitch-align/tracking correction diff). Compared against the
recovered reference in `reborn-camera` (`CAMERA_WALL_OBSTRUCTION.md`,
`CAMERA_CONFIG_FILES.md`, `CAMERA_REAL_VALUES.md`,
`CAMERA_INPUT_CONTROLS.md`) and the UI bindings.

**Verdict:** the client implements the follow camera's user-facing subset —
distance/zoom/drag/reset/init per-user settings plus a terrain-only
obstruction. It does **not** implement the native collision system, the
multi-mode behaviour, the movement-reactive camera, shake/follow-action,
cinematic tracks, or the engine FOV path.

## Implemented (matches the recovered system)

| Feature | Where | Notes |
|---|---|---|
| Unit scale 1 u = 1 cm | `CameraSystem.cs:90` | `UnitsPerMeter = 100`, `RC_CAMERA_SCALE` override (`RebornClient.cs:186`) |
| Per-mode parameter rows | `CameraSystem.cs:94-214`, `camera.json` | character/sprint/carrier/air_combat/npc_dialog/god; verified defaults (`CameraMaxDeltaYaw=2pi`, `CameraMaxDeltaPitch=1.56`, `SmoothTime`, `TargetDistance`) |
| Per-map init yaw/pitch | `CameraSettings.cs:78-103` | from `scene_init_param.txt` (`CameraInitYaw/Pitch`) |
| Saved runtime view | `CameraSettings.cs:126-149` | `g_Scene_tCameraRuntime` `fYaw`/`fPitch`/`fCameraToObjectEyeScale` |
| User camera settings (read) | `CameraSettings.cs:126-149` | `fMaxCameraDistance`, `fDragSpeed`, `fDragPitchSpeed` from `VideoSettingPanel.tCameraStatic` |
| LMB = camera drag, RMB = camera + character turn, cursor lock/recenter | `RebornClient.cs:446-470`, `795-798` | matches `ui/hotkey/default.txt` |
| Per-frame drag clamps | `RebornClient.cs:619-646` | row `CameraMaxDeltaYaw/Pitch` scaled by the measured engine orbit sensitivities (0.0018 / 0.00121 rad/px) |
| Wheel zoom `x0.9 / x1.1` with limits | `CameraSystem.cs:304-313`, `RebornClient.cs:471-475` | matches `Camera_Zoom(0.9/1.1)`; clamped to `[MinCameraDistance, MaxCameraDistance]` |
| F11 reset (behind, -15 deg) and Home/End presets | `RebornClient.cs:497-512` | matches `hotkeys.lua` `Camera_SetForceReset(..., -pi/12)` / `CameraSetView(0/180)` |
| Follow placement | `RebornClient.cs:911-929` | anchor + 90 u; **wrong pitch model — see the correctness bug below** |
| Offset smoothing 60 ms | `RebornClient.cs:938-948` (worktree diff) | the `SetCharacterCameraPosition` dead-zone/exponential model |
| Pitch tracking + drift yaw correction | `RebornClient.cs:637-644`, `722-730` | pitch from orbit deltas; yaw re-measured from the engine view when the mouse is idle |
| Sprint mode switch + pull-back | `RebornClient.cs:900-911`, `CameraSystem.cs:431-452` | Shift+move; `SprintCameraSmoothTime` smoothing |
| Obstruction: terrain raymarch | `RebornClient.cs:931-954` | 14 samples, 20 u margin, retract `max(150, t*len-40)` u, ground clamp +30 u |
| Camera-error fallbacks | `RebornClient.cs:892-958` | try/catch, fixed-cam override `RC_FIXED_CAM` |

## Partial

| Area | What exists | What is missing |
|---|---|---|
| Sprint camera | mode + distance pull-back | `SprintCameraOffset`, `OffsetSmoothTime`, `SpringTime`, track-back speed/slope, pitch/angle unused; the `sprintSpeed` argument of `UpdateDistance` is ignored |
| Settings | reads 3 custom.dat values | read-only (no save path); `fSpringResetSpeed`, `fCameraResetSpeed`, `nCameraMode` not read; `MinCameraDistance` is a 100 u guess (engine cap never probed) |
| Modes | rows exist for carrier/air_combat/npc_dialog/god | no activation triggers (mount / air combat / dialog / spectate); glider and dynamic-follow modes absent entirely |
| FOV | editor `Get/SetViewAngleFactor` probe | real `fCameraAngle`/`fFovy` not used; no `config.ini [UIVideoSetting]` persistence |
| Anchor | fixed chest +90 u | no head/socket resolution (`s_face`, carrier sockets), no per-object offsets |
| Drag speeds | `fDragSpeed`/`fDragPitchSpeed` scale pixel deltas | row `MaxDragSpeed`/`RotationSpeed` not applied (engine orbit owns the response) |

## Missing (no implementation in the client)

1. **Native obstruction**: scene raycast via `FilterCamera`, the per-mesh
   `bObscatleCamera` gate, 5/9 footprint probes, nearest-hit clamping, the
   18-u clearance, the 50/100-u hysteresis and the flex return
   (`fObstructdAvert`/`fFlexCoefficient`/`fDampCoefficient`). The client only
   samples the terrain heightfield.
2. **Movement-reactive camera**: `CameraMovePitch*` rows (auto pitch while
   moving / returning to adjust pitch) and `CameraAdjustYawWhenMoveTurn`
   (yaw follows the move turn) exist in `CameraSystem.Update` but the client
   never calls it.
3. **Camera shake** (`CameraShake`) and **follow-action look-at**
   (`SetFollowAction`) — implemented in the model, never triggered.
4. **Cinematic/track camera** (`TrackCamera`, `KRLCameraAni` equivalent) —
   class exists, not wired.
5. **Engine `[Camera]` settings** (`bObstructdAvert`, `fChaseRate`,
   distances/angles, `fFovy`) and the **engine caps**
   (`fMinCameraDistance`, angle limits) — not read or probed.
6. **Operation modes**: classic/joystick switching, `Camera_EnableControl`,
   `Camera_UseFullAngle`, `Scene_LockMouseRotation`,
   `Camera_SetResetSpeed(3.5, 3.75)`.
7. **Mode-specific behaviour**: carrier pitch/yaw/delta-height/`ForbidStrafe`,
   air-combat entry parameters, NPC dialog camera (post-render/eye scale),
   glider, dynamic follow, skill-move camera (FOV/edge FX).
8. **UI settings panel** (video/camera sliders, `custom.dat` write path),
   `bCameraSmoothing`/`bCurveCamera`/`bEyeFollow` toggles.
9. **God/spectate cameras** (`CameraCommon` watch modes), view presets beyond
   Home/End, unbounded pitch keys.

## Model-only code (in `CameraSystem.cs`, unused by the client)

`Update` (rotated offset + move-pitch + yaw-follow + dead-zone smoothing +
obstruction callback), `Mouse`, `FollowYaw`, `SetFollowAction`, `SprintSpeed`,
`CameraShake`, `TrackCamera`, the `god` row and the `MODE_*` rows other than
character/sprint. These are covered by `CameraSmoke.cs` but are not part of
the running client.

## Correctness bug (2026-09-24): dragging changes the camera's distance

Symptom: dragging (especially vertically) moves the camera closer/farther and
slides its position, instead of only rotating it around the character.

**Real behaviour** (`SetCharacterCameraPosition`, recovered in
`JX3_CAMERA_RESEARCH.md` §6 and `camera_set.txt` `0x180B0F1EE`): the camera
position is `anchor + offset`, where `offset` is the controller's
`CameraPositionOffset` vector **rotated by yaw/pitch at constant length**:

```
offset = ( cos(yaw)·sin(pol)·A + sin(yaw)·B,
           cos(pol)·A + C,
           sin(yaw)·sin(pol)·A − cos(yaw)·B )
pol    = angle from vertical; pitch_from_horizontal = pi/2 − pol
A/B/C  = distance / lateral / height ([r14+0xC8/0xCC/0xD0])
```

`A` multiplies only sines/cosines, so pitching rotates the camera on a sphere
of radius `A`; `C` (height) is a **separate constant term**. Mouse drag only
accumulates yaw/pitch (per-frame deltas); nothing writes a position.

**Our code** (`RebornClient.cs:925-929`, and the obstruction retract):

```csharp
double pitchOffset = Math.Tan(camSys.Pitch);
double camX = ax2 - vx * dist;              // horizontal radius stays dist
double camY = ay2 - pitchOffset * dist;     // vertical = tan(pitch) * dist
double camZ = az2 - vz * dist;
```

The offset length is now `dist / cos(pitch)` — it **grows as the camera
pitches**. Vertical drag therefore re-scales the orbit (the camera's distance
from the anchor changes), and the horizontal offset never shortens with
pitch. The startup pitch `-atan2(CameraHeight, distance)` masked this by
folding the row height into `tan`: at that pitch the camera happened to sit at
anchor + CameraHeight. As soon as the user drags, the height term is lost and
the radius changes.

Note `CameraSystem.Update` (`CameraSystem.cs:372-375`) already contains the
correct rotation (`cos(yaw)·cos(pitch)·d`, `sin(pitch)·d + height`,
`sin(yaw)·cos(pitch)·d`) — but the live client never calls it; the placement
is a separate, incorrect reimplementation.

**Secondary contributors**

- Drag is executed through the engine orbit (`scene.ExecAction(30/1, ...)`),
  which moves the engine's own camera position around the engine pivot. The
  real client's drag only mutates the controller's yaw/pitch state.
- The idle "drift correction" (`RebornClient.cs:722-730`) re-derives yaw from
  the measured engine camera **position** (`measureView`), feeding position
  back into angle — after a drag, the engine's moved position can swing the
  yaw and therefore our placed position.

**Fix direction**

1. Use the constant-length rotation in the live placement (same math as
   `CameraSystem.Update`): horizontal `cos(pitch)·dist`, vertical
   `sin(pitch)·dist + CameraHeight` (separate term, no `tan`).
2. Do not derive yaw from camera positions while dragging; integrate the drag
   deltas into yaw/pitch only (re-measure only when idle, and only if needed).
3. Keep the engine orbit strictly as an aim-alignment step for the host's
   screenshot path; never let it define the camera position.

## Practical gaps with the most visible effect

1. Dragging re-scales the camera distance (wrong pitch model, above) — the
   worst current camera defect.
2. Walls/buildings/trees never push the camera (terrain only) — the native
   18-u wall clearance and model filter are absent.
3. No auto pitch when running (the real client dips the camera while moving).
4. Mounts, gliding, air combat and dialogs do not change the camera.
5. The camera never shakes and never eases back with the native flex curve.

Sources: `client/*.cs` line references above; the recovered reference tables
in `reborn-camera/docs/CAMERA_CONFIG_FILES.md` §7 and
`reborn-camera/docs/CAMERA_WALL_OBSTRUCTION.md`.
