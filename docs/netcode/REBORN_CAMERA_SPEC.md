# Reborn camera spec (modeled on JX3)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Input:** `docs/netcode/JX3_CAMERA_RESEARCH.md` (static findings from `JX3RepresentX64.dll`).
**Goal:** recreate the JX3 camera behaviour — ECS camera entities with per-mode
controllers, table-driven follow parameters, smoothing formula, and script hooks.

Everything below is our own implementation; only the *model* is copied.

---

## 1. Architecture (from JX3)

- A scene holds a **camera entity** (position/look) and one active **camera
  controller** (a component looked up by name `"camera controller"`).
- Controllers are **modes**: `character` (default follow), `skill_move`,
  `air_combat`, `npc_dialog`, `carrier` (mount), `sprint`, `track` (cinematic),
  `god`, `homeland`, `lock_target`, `focus_face`, `dynamic_follow`.
- Switching is event-driven; entering a mode may **smooth** from the current
  camera (`SmoothToAirCombatCamera`, `SmoothToNpcDialogCamera`).

```python
class CameraController:  # one per mode
    def update(self, dt, ctx) -> CameraState: ...
    def enter(self, ctx): ...   # snapshot current state for smoothing
    def leave(self, ctx): ...
```

## 2. Parameter schema (per-mode row, table-driven)

Rows come from a data table; keys are exactly the ones JX3 uses:

| Key | Type | Meaning |
|---|---|---|
| `CameraHeight` | float | height above anchor |
| `TargetDistance` | float | follow distance (>= 0, default 1.0) |
| `SmoothTime` | float | smoothing time, seconds (> 0) |
| `MaxDragSpeed` | float | camera drag speed cap (> 0) |
| `RotationSpeed` | float | follow rotation speed (> 0) |
| `CameraMaxDeltaYaw` / `CameraMaxDeltaPitch` | float | per-frame mouse delta clamps |
| `ForbidStrafe` / `ForbidRotation` | bool | lock strafe / rotation (mounts) |
| `TurnCameraYawToObjectYaw` | bool | align yaw to object yaw |
| `InitCameraPitch` / `InitCameraAngle` / `InitCameraDistance` | float | initial state |

Zoom-level follow behaviour (10 rows, stride 0x24 — JX3 evidence):

| Key | Meaning |
|---|---|
| `ZoomLength` | zoom distance for this row |
| `CameraMovePitchApplyAngle` | pitch applied while moving |
| `CameraMovePitchSmoothTime` | smoothing for applied pitch |
| `CameraMovePitchAdjustPitch` | base adjust |
| `CameraMovePitchAdjustMaxPitch` | adjust cap |
| `CameraMovePitchApplyMaxPitch` | apply cap |
| `CameraMovePitchApplyTimeInterval` | min time between applies |
| `CameraAdjustYawWhenMoveTurn` | yaw-follow amount on move turn |
| `CameraAdjustYawWhenMoveTurnDisableAngle` | dead zone |

Specialisations: `SprintCameraMin/MaxTrackBackSpeed`,
`SprintCameraTrackBackSpeedSlope`, `SprintCameraMaxDistance`,
`SprintCameraSmoothTime`, `CarrierCameraMaxDistance`,
`CarrierCameraDeltaHeight`, `CameraFollowCharacterAction_MinDis/_MaxDis/_Speed`.

## 3. Follow algorithm (recovered from `SetCharacterCameraPosition`)

Given anchor `A` (character position; head/socket or mount point), yaw `Y`,
pitch `P`, distance `d`, height `h`:

```
desired_offset = (
    cos(P) * sin(Y) * d,     # horizontal x
    sin(P) * d + h,          # vertical
    cos(P) * cos(Y) * d,     # horizontal z
)
```

Then **exponential smoothing** with dead zone (exact JX3 form, disassembly
`0x180B0F2BA`–`0x180B0F3A6`):

```
dt       = now - last_frame_time
delta    = desired - current
if |delta| > epsilon and |delta| > |delta| * dt / SmoothTime:
    current += delta * dt / SmoothTime        # time-based lerp
else:
    current = desired                           # snap (avoids jitter)
camera_pos = anchor + current
```

`camera_look = anchor (+ face target if `s_face` / lock-target mode)`. Obstruction
handling happens in the final set-camera call (engine-side in JX3; in our
implementation: ray from anchor to camera, clamp `d` on hit, smooth back).

Mouse input: `delta_yaw = clamp(mouse_dx * sens, CameraMaxDeltaYaw)`,
`delta_pitch = clamp(mouse_dy * sens, CameraMaxDeltaPitch)`.

## 4. Movement-reactive rules

- **Moving**: every `CameraMovePitchApplyTimeInterval`, apply pitch toward
  `CameraMovePitchApplyAngle` (clamped by `CameraMovePitchApplyMaxPitch`),
  smoothed over `CameraMovePitchSmoothTime`; return to `AdjustPitch` when idle.
- **Turning while moving**: if turn angle > `CameraAdjustYawWhenMoveTurnDisableAngle`,
  drag camera yaw toward the character yaw by `CameraAdjustYawWhenMoveTurn`.
- **Sprint**: pull distance toward `SprintCameraMaxDistance` at a speed scaled
  from `SprintCameraMin/MaxTrackBackSpeed` + slope; restore on stop
  (`SprintCameraSmoothTime`).

## 5. Script hooks (parity with the Lua API we found)

```python
camera.set_max_distance(v); camera.set_drag_speed(v)
camera.set_follow_mode(mode); camera.switch_mode(mode)
camera.set_pitch(pitch); camera.set_params(params)
camera.set_spring_reset_speed(v); camera.set_reset_speed(v)
camera.set_remote_mode(mode)      # replay/observer
camera.set_god_mode(on)           # free fly
camera.on_reconnect()             # reset after KREPRESENT_EVENT_RECONNECTED
```

## 6. Cinematic (track) camera

`TrackCameraFrameMove` = spring system: `distance = offset_z - height - prev`,
spring integrate with clamp, then `pos = base + track_delta`
(`docs/netcode/JX3_CAMERA_RESEARCH.md` §5). Implement as a damped spring.

## 7. Known gaps (values, not structure)

1. Actual numeric values of the parameter tables (`CameraConfig` ini lives
   behind a runtime path root; extraction needs VFS enumeration).
2. Camera obstruction/collision details (engine-side in JX3).
3. Mouse sensitivity defaults (`userdata\custom.dat`, binary).

Until extracted, ship JX3's key names with tunable defaults and a single
`camera.json` to override them.
