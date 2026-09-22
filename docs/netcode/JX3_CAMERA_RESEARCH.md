# JX3 camera system — static research notes

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Primary binary:** `JX3RepresentX64.dll` (SO3Represent layer, image base `0x180000000`)
**Method:** static only — full string extract + targeted disassembly (see `proof/netcode/disasm/camera_*.txt`).

Question driving this: *how does the game camera work, and how does it follow the player?*

---

## 1. Architecture: ECS camera entities + mode controllers

The camera is an entity-component-system feature in `SO3Represent`, not a single
hard-coded camera:

```
RLScene
 ├─ camera entity          CameraComponent            (the 3D camera object)
 ├─ camera controller      "camera controller"        (movement/follow logic)
 │    ├─ default character follow   (pMovePitchCameraController, pObjectController)
 │    ├─ KRLCameraMovementControllerComponent
 │    ├─ UKRLTrackCameraComponent            (cinematic camera tracks)
 │    ├─ UKRLAirCombatCameraControllerComponent
 │    ├─ UKRLSkillMoveCameraControllerComponent
 │    └─ UKRLNPCDialogCameraControllerComponent
 └─ pCameraControllerEntity / pCameraModeEntity   (active controller + mode)
```

Evidence (string offsets in `JX3RepresentX64_all_strings.txt` / dumps):

| Symbol | Offset | Meaning |
|---|---|---|
| `CameraComponent::OnPositionChanged`, `CameraComponent::LuaGetPosition` | `0x00C9C588`, `0x00C9C898` | camera component |
| `.?AUKRLCameraMovementControllerComponent@@` | `0x00EC33E8` | default movement controller |
| `.?AUKRLTrackCameraComponent@@` | `0x00EC3430` | track/cinematic camera |
| `.?AUKRLAirCombatCameraControllerComponent@@` | `0x00EC3520` | air-combat mode |
| `.?AUKRLNPCDialogCameraControllerComponent@@` | `0x00EC3640` | NPC dialog mode |
| `.?AUKRLSkillMoveCameraControllerComponent@@` | `0x00EC36D0` | skill-move mode |
| `pCameraControllerEntity`, `pCameraModeEntity` | `0x00D09B98`, `0x00D08CA8` | active controller/mode |
| `SetCharacterCameraPosition` | `0x00D0D2B8` | main follow placement fn |
| `AdjustCharacterCameraPosition` | `0x00D08A28` | follow adjustment fn |
| `TrackCameraFrameMove` | `0x00D0D648` | per-frame cinematic update |
| `pMovePitchCameraController` | `0x00D0D318` | default (move/pitch) controller |
| `SetGodCameraPosition`, `SetHomelandCameraPosition` | `0x00D0D388`, `0x00D0D3C0` | special modes |
| `GetObjectPositionOffset`, `UpdateObjectPositionOffset`, `SetForceObjectPositionOffset` | `0x00D0D160`, `0x00D0D1A0`, `0x00D08D78` | per-object camera offsets |

The client looks controllers up by **component name string** at runtime —
disassembly shows `strncmp(component->name, "camera", 0x18)` and
`strncmp(..., "camera controller", 0x18)` (`SetCharacterCameraPosition`,
`0x180B0E975`, `0x180B0EA50`).

### Mode switching (events, all string-verified)

| Event / API | Offset |
|---|---|
| `SwitchCameraMode`, `SetCameraFollowMode` | `0x00C80260`, `0x00C801E0` |
| `OnSetLocalCameraMode`, `OnSetRemoteCameraMode`, `OnSetGodCameraMode`, `OnSetHomelandCameraMode`, `OnResetCameraMode` | `0x00C83EF0`, `0x00C83DE8`, `0x00C83FC8`, `0x00C84050`, `0x00CC4F10` |
| `OnEnableAirCombatCamera` / `EnterAirCombat` / `SmoothToAirCombatCamera` / `GetAirCombatCameraOriginalParams` | `0x00C84968`, `0x00D08BC0`, `0x00D08BD8`, `0x00D08C30` |
| `OnDisableSkillMoveCamera` / `LeaveSkillMoveCamera` / `pSkillMoveCamera` | `0x00C84A50`, `0x00D08CF0`, `0x00D08D28` |
| `EnterCarrier`, `pCarrierCameraController`, `m_pCarrierCameraFile` | `0x180ACFD5D`, `0x180B0E9A3`, `0x00CD1B90` |

## 2. Follow model (parameter vocabulary, all table-driven)

Every mode loads a parameter row from a game table. `LoadCarrierParams`
(`0x180ACFB70`, `proof/netcode/disasm/camera_mouse_config.txt`) shows the full
key vocabulary, read via `GetFloat(key, default)` / `GetBool(key)`:

| Key | Field | Meaning |
|---|---|---|
| `CameraHeight` | `+0x24` | camera height above anchor |
| `CameraMaxDeltaYaw` | `+0x2c` | per-frame mouse yaw clamp |
| `CameraMaxDeltaPitch` | `+0x30` | per-frame mouse pitch clamp |
| `MaxDragSpeed` | `+0x28` | camera drag speed (must be > 0) |
| `RotationSpeed` | `+0x34` | follow rotation speed (must be > 0) |
| `ForbidStrafe` | `+0x38` (bool) | disable strafe |
| `ForbidRotation` | `+0x3c` (bool) | disable rotation |
| `TargetDistance` | `+0x40` | follow distance (default 1.0) |
| `SmoothTime` | `+0x44` | smoothing time (must be > 0) |
| `TurnCameraYawToObjectYaw` | bool | align camera yaw to object yaw |
| `InitCameraPitch` / `InitCameraAngle` / `InitCameraDistance` | `+0x48` etc. | initial state |

### Movement-reactive follow (default character camera)

| Key | Offset | Behaviour |
|---|---|---|
| `CameraMovePitchApplyAngle` | `0x00C8C4B8` | pitch applied when moving |
| `CameraMovePitchSmoothTime` | `0x00C8C4D8` | smoothing for that pitch |
| `CameraMovePitchAdjustPitch` | `0x00C8C4F8` | base adjust pitch |
| `CameraMovePitchAdjustMaxPitch` | `0x00C8C518` | cap for adjust |
| `CameraMovePitchApplyMaxPitch` | `0x00C8C540` | cap for applied pitch |
| `CameraMovePitchApplyTimeInterval` | `0x00C8C5C0` | min interval between applies |
| `CameraAdjustYawWhenMoveTurn` | `0x00C8C568` | yaw follows when turning while moving |
| `CameraAdjustYawWhenMoveTurnDisableAngle` | `0x00C8C590` | dead-zone angle for that |

The loader for these is `0x180338C10` (`proof/netcode/disasm/camera_config.txt`):
it iterates **10 camera rows** (stride `0x24`) reading
`ZoomLength`, `CameraMovePitchApplyAngle`, `CameraMovePitchSmoothTime`,
`CameraMovePitchAdjustPitch`, `CameraMovePitchAdjustMaxPitch`,
`CameraMovePitchApplyMaxPitch`, `CameraMovePitchApplyTimeInterval`,
`CameraAdjustYawWhenMoveTurn`, `CameraAdjustYawWhenMoveTurnDisableAngle` —
i.e. zoom-level-specific follow behaviour. A second sorted table at
`[global+0x262F0]` is used for value lookup by threshold.

### Sprint / mount / special modes

| Key | Offset |
|---|---|
| `SprintCameraMinTrackBackSpeed`, `SprintCameraMaxTrackBackSpeed`, `SprintCameraTrackBackSpeedSlope` | `0x00CCE448`, `0x00CCE470`, `0x00CCE498` |
| `SprintCameraMaxDistance`, `SprintCameraSmoothTime` | `0x00CCE0C8`, `0x00CCE0E8` |
| `CarrierCameraMaxDistance`, `CarrierCameraDeltaHeight`, `CarrierCameraSmoothTime`(via `CharacterCameraSmoothTime`) | `0x00CCE1A8`, `0x00CCE1C8`, `0x00CCE848` |
| `AirCombatToSkillMoveCameraDis`, `SkillMoveCamera`, `PlayerRushCamera` | `0x00CCE7D0`, `0x00CCF760`, `0x00CD0A48` |
| `CameraFollowCharacterAction_MinDis/_MaxDis/_Speed` | `0x00CCEAD0`, `0x00CCEB00`, `0x00CCEB30` |
| `UpdateAnimationCameraDistance`, `AnimationCameraModel`, `CameraFollowCharacterAction` | `0x00CCE888`, `0x00CD0E98`, `0x00CCDE68` |

Interpretation (MED): the follow camera is a **spring/damped follower around an
anchor** (usually the character head/bone): yaw/pitch from mouse (clamped by
`CameraMaxDelta*`), distance/height from the active row, smoothing by
`SmoothTime`; moving auto-applies pitch (`CameraMovePitch*`); turning while
moving drags yaw (`CameraAdjustYawWhenMoveTurn*`); sprint pulls the camera back
with a speed-dependent offset (`SprintCamera*`); mounts/vehicles use their own
row (`CarrierCamera*`).

## 3. Runtime control from game logic (Lua-facing)

`KGameWorldHandler` exposes camera setters called from logic/Lua
(all string-verified in `JX3RepresentX64_all_strings.txt`):

```
SetCameraDragSpeed, SetCameraMaxDistance, SetCameraSpringResetSpeed,
SetCameraResetSpeed, SetCameraFollowMode, SwitchCameraMode,
OnSetRemoteCameraMode, OnSetGodCameraMode, OnResetCameraMode
```

Plus the Lua binding layer in `proof/netcode/disasm/camera_player_params.txt`
(`0x1802EB220`): `SetCameraMaxDistance`, `SetCameraParams`, `SetCameraPitch`,
`SetCameraDragSpeed`, `SetCameraSpringResetSpeed`, `SetCameraResetSpeed`,
`SetCameraFollowMode`, `SwitchCameraMode` — each parses a Lua argument (bool /
int / float / double) and forwards it.

So gameplay code (Lua) can change follow distance, drag speed and mode at
runtime; the Representation layer owns the actual motion.

## 4. Camera config files (located, not yet extracted)

`KTableList::LoadCameraConfigFile` (`0x180832DD0`,
`proof/netcode/disasm/camera_configfile.txt`) does:

1. resolve path for logical name **`CameraConfig`** → `g_OpenIniFile(path)`
2. read `[?] OBJECT_POSITION_OFFSET_FILE_NAME / Count` — asserted to be **7**
3. read `Item_%d` (0..6) into `g_szCameraObjectPositionOffsetFiles[7]` (64-byte names)
4. load **`CameraLockTargetConfig`**; on failure logs
   `[rl] load CameraLockTargetConfig failed, file:%s`

The path root comes from a runtime global (`[0x180EDDFE0]+0x120`), so the
logical path could not be resolved statically. PakV4 probes for
`CameraConfig.*` / `CameraLockTargetConfig.*` under 12 likely roots and 8
extensions all missed (`proof/netcode/camera_probe/`) — the file likely lives
under a root we haven't guessed or is resolved from the client working dir at
runtime.

`CameraLockTargetConfig` is a separate lock-on camera config; per-object camera
position offsets are in 7 separate files listed inside `CameraConfig`.

## 5. Cinematic / track camera (for completeness)

`TrackCameraFrameMove` (`0x180B17090`, `proof/netcode/disasm/camera_track.txt`)
implements the camera-track player:

- `RunSpringSystem` with `Param.fDistance = fOffsetZ - fHeight - fPrev` and
  assert `Param.fDistance >= 0.0f` (`0x180B17151`)
- spring integrates toward a target, clamps, then final camera pos =
  base offset `[rbx+0x60..0x68]` + track delta `[rbx+0x38..0x40]`
- used by `KRLCameraAni::SyncCamera` / `KRLReferenceCameraAni::SyncCamera`
  (`0x00CBBE30`, `0x00CA3740`) for cutscenes/replays

## 6. Follow formula recovered (second disasm pass)

From `SetCharacterCameraPosition` (`0x180B0E820`, `proof/netcode/disasm/camera_set.txt`):

1. **Anchor selection** (`0x180B0EE17`–`0x180B0EFDC`): the follow target position is
   gathered in priority order — mount socket (`[pRLCharacter+0x3A08]`), custom
   offsets (`+0xCF8`, `+0x3CA8 == 2` branch), character head/entity position
   (`+0x3B9C`, `+0x38`), else the raw character position — into a 3-float anchor.
2. **Desired offset** (`0x180B0F1EE`–`0x180B0F290`): the offset is the distance
   vector rotated by yaw/pitch via sinf/cosf (`0x180BB83B5` / `0x180BB83BB`),
   plus height and per-axis deltas `[r14+0xD0]` etc. Form:

   ```
   offset = (cos(P)*sin(Y)*d,   sin(P)*d + h,   cos(P)*cos(Y)*d)
   ```

3. **Exponential smoothing with dead zone** (`0x180B0F2BA`–`0x180B0F3A6`):
   `dt = now - last` (global timestamps `[g+0x80] - [g+0x88]`), and per axis:

   ```
   delta = desired - current
   if |delta| > eps and |delta| > |delta| * dt / SmoothTime:
       current += delta * dt / SmoothTime
   else:
       current = desired
   ```

   Smoothed state persists in `[r14+0x1B8/0x1BC/0x1C0]`; final camera pos =
   anchor + offset (`0x180B0F3AF`–`0x180B0F435`), then the engine camera is set
   with look-at params (`call 0x180B10A70`, plus distance smoothing fields
   `[r14+0x214..0x24C]` via `0x180B0E6E0`).

Reference implementation of this exact model (all 9 smoke checks pass):
`tools/netcode/reference/camera_model.py`, spec: `docs/netcode/REBORN_CAMERA_SPEC.md`.

## 7. Evidence index

| File | Content |
|---|---|
| `proof/netcode/JX3RepresentX64_camera_strings.txt` | filtered camera string vocabulary with offsets |
| `proof/netcode/disasm/camera_player_params.txt` | Lua-facing camera API (`SetCameraMaxDistance` etc.) |
| `proof/netcode/disasm/camera_mouse_config.txt` | `LoadCarrierParams` — full per-mode key vocabulary |
| `proof/netcode/disasm/camera_config.txt` | 10-row move-pitch/yaw follow parameter loader |
| `proof/netcode/disasm/camera_set.txt` | `SetCharacterCameraPosition` — component lookup + placement |
| `proof/netcode/disasm/camera_adjust.txt` | `AdjustCharacterCameraPosition` — follow adjustment math |
| `proof/netcode/disasm/camera_configfile.txt` | `LoadCameraConfigFile` — ini + 7 offset files + lock-target config |
| `proof/netcode/disasm/camera_track.txt` | `TrackCameraFrameMove` — cinematic spring camera |

## 7. Open questions / next steps

1. **Extract the actual camera values.** Either resolve the `CameraConfig` ini
   through the client VFS (needs `KG_PAKFS_CollectAllFileNames` from an
   engine-host process) or read the parameter rows via the table system.
2. **Exact follow math.** `AdjustCharacterCameraPosition` is partially decoded
   (anchor → sin/cos rotated offset → smoothing); a focused read of
   `SetCharacterCameraPosition` (`0x180B0E820`) will pin the final formula.
3. **Collision.** Camera obstruction handling was not found yet; likely
   engine-side via `CameraComponent::OnPositionChanged` + object position
   offsets (`GetObjectPositionOffset`).
4. **Mouse sensitivity.** `MouseMoveCamera` + `CameraMaxDeltaYaw/Pitch` clamps;
   user settings live in `userdata\custom.dat` (binary, not yet decoded).
