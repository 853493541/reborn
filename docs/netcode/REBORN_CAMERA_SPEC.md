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

1. **Per-mode row values** — the krl tables are not shipped in the current
   build (downloader verified the install is complete). **Verified defaults**
   that the current build actually uses (see
   `docs/netcode/JX3_CAMERA_RESEARCH.md` §8.1, `proof/netcode/camera_defaults_verified.txt`):
   `CameraMaxDeltaYaw = 2π`, `CameraMaxDeltaPitch = 1.56`,
   `TargetDistance = 1.0`, `SmoothTime = 1.0`,
   `CameraAdjustYawWhenMoveTurnDisableAngle = 0.26 rad (15°)`,
   `InitCameraPitch = π` (sentinel), everything else 0/false.
   Ship these as the defaults in `camera.json`.
2. **Obstruction** — implemented (reference `obstruction` hook): raycast
   anchor→camera, clamp to hit−0.2, smooth return; JX3 does the same inside the
   engine camera (`bObstructdAvert` in `KG3DEngineX64.dll`).
3. **Camera shake** — implemented (`CameraShake` class): burst = cos over
   period, amplitude × decay per cycle, ends after max cycles; idle = rand
   jitter within ± amplitude. Matches the recovered updater (`0x180B10A70`).
4. Mouse sensitivity defaults (`userdata\custom.dat`, binary; user setting).
5. **Resolved 2026-09-23** — per-user camera settings and the real zoom step:
   - `userdata/<account>/<region>/<server>/<role>/custom.dat` (Lua-ish text)
     stores the camera panel state:
     `VideoSettingPanel.tCameraStatic = {fDragSpeed, fMaxCameraDistance,
     fSpringResetSpeed, fCameraResetSpeed, nCameraMode, fDragPitchSpeed}`
     plus `bCameraSmoothing`, `bCurveCamera`, `bEyeFollow`,
     `UISetting_Comprehensive.nCameraModeInClassicMode` and the saved runtime
     camera `g_Scene_tCameraRuntime = {fYaw, fPitch, fCameraToObjectEyeScale}`.
     Real defaults across 92 roles: `fDragSpeed = fDragPitchSpeed =
     fSpringResetSpeed = fCameraResetSpeed = 1`, **`fMaxCameraDistance = 2000`**
     (68 roles untouched; user-adjusted roles use 760/1125/1245),
     **`nCameraMode = 0`** (some roles 1), smoothing/curve = true,
     `fCameraToObjectEyeScale = 1` (0.9 on some roles).
     Saved pitch: **-0.35 rad (-20°) default**, observed range -0.17..-0.78 rad.
   - Engine caps (from `Lua3DEngine_Get3DEngineOptionCaps`, `JX3UIX64.dll`):
     `fMinCameraDistance`, `fMaxCameraDistance`, `fMinCameraAngle`,
     `fMaxCameraAngle`; the values come from the engine caps object (per
     graphics option level) and are not in any accessible config — only the
     `fMaxCameraDistance = 2000` default is confirmed (DLL const blob
     `0x180d2af90`, next to `-1.56298 / 1.0 / 0`; string block also contains
     `AdjustEyeScale`, `InitCameraEyeScaleEnable`, `InitEyeScale`).
   - Wheel zoom (real, `ZoomCharacterCamera_Step`, `JX3RepresentX64.dll`
     `0x180b3ce40`): `step = clamp(current / (0.2 * fMaxCameraDistance) * 120,
     10, 120)` world units; sign by direction. Pitch is asserted
     `|pitch| < π/2`; the smoothing setter clamps its value to [0.001, 1.0].
   - Camera row tables (`tabCamera`, `tabCarrierCamera`, `tabAirCombatCamera`,
     `tabNpcDialogCamera`) are still absent from this install and from the CDN
     resource index, so the per-mode row values (default follow distance,
     min distance, angle caps) remain unknown.
   - **Units: 1 engine unit = 1 cm (1 m = 100 u)** — mesh-verified
     (`docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md`: adult male 181.64 u =
     1.816 m); the earlier `UnitsPerMeter = 192` was wrong (it would make the
     adult 0.95 m). `fMaxCameraDistance = 2000` is therefore 20 m.

## 8. Product implementation (engine host)

Implemented in `engine_host_spike/CameraSystem.cs` (C# port of the Python
reference, byte-for-byte parity: `CameraSmoke.cs` ports the 13 reference
checks and prints ALL PASS).

- Modes/rows exactly as the spec; verified defaults shipped in
  `engine_host_spike/camera.json` (copied to `bin64\camera.json`; the host
  loads it at startup, overriding row values with no code change).
- Follow math: anchor + (cos(yaw)cos(pitch)d, sin(pitch)d + h, sin(yaw)cos(pitch)d),
  exponential smoothing `offset += delta*dt/SmoothTime` with dead-zone snap.
- Movement-reactive pitch (pi/3000 rad/ms = 60 deg/s), yaw-follow while turning
  (dead zone 0.26 rad), sprint pull-back (SprintCameraMaxDistance), follow-action
  lock target, camera shake and cinematic spring (TrackCamera) ported as well.
- Obstruction: ray-march from the character's chest toward the camera against
  the real terrain sampler (14 steps, 20 u margin); on hit the camera is pulled
  to hit-0.2 m. Plus a final clamp above terrain+30 u.
- Integration in `MapSpike.cs` (player follow mode, **follow camera only** —
  the F free-cam toggle was removed 2026-09-23):
  right-drag = engine-native rotation (the engine camera owns the look
  direction; the host re-measures it via the nudge probe every 200 ms),
  wheel = real JX3 zoom step (see §7.5) with limits `MinCameraDistance = 100 u`
  (placeholder for the unknown engine cap) .. `MaxCameraDistance = 2000 u`
  (real setting default, 20 m), Shift+move = sprint mode pull-back,
  movement input holds its world direction while the key set is unchanged.
- Units: rows are meters; the host scales them by `UnitsPerMeter`
  (default **100**, `MAP_CAMERA_SCALE`). Character 6 m distance -> 600 u,
  height 2 m -> 200 u, real pitch -0.35 rad.
- Verified in game: camera settles at camDistXZ 600 u (= 6 m * 100 * cos20°)
  with the terrain clamp active.

Build (from the repo root):

```
csc /platform:x64 /target:exe /out:map_spike_host.exe ^
  /r:MovieEngineCLR.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  engine_host_spike\MapSpike.cs engine_host_spike\FoliageCollision.cs ^
  engine_host_spike\CameraSystem.cs
csc /platform:x64 /target:exe /out:camera_smoke.exe ^
  engine_host_spike\CameraSystem.cs engine_host_spike\CameraSmoke.cs
```
