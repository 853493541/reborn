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

**Sprint clamp mechanism — VERIFIED** (`ClampMouse` @ `0x180B1FE70`, sprint
branch `0x180B20140–0x180B20200`; `ClampMouseForSprintCamera` assert):
- int speeds from sprint controller `[+0x1D8]` / `[+0x1DC]` (getters
  `0x180611BB0` / `0x180611B90`) × **π/128 = 0.024543693** (`0x180D0CF64`) → radians;
- slew-rate limiter `0x180B13B60`: `clamp(angle, −dt·[rcx+0x5C], +dt·[rcx+0x5C])`;
- final mouse pitch clamp:
  `clamp(speed_angle, max(min_angle, base + slew_result)) − base`.
So sprint limits how far the camera can pitch back as a function of speed,
rate-limited over time. `MouseMoveCamera` (`0x180B240D0`) is the *homeland*
housing camera (screen-edge drag via X3DEngine `IView`), separate from this.

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
   plus height and per-axis deltas `[r14+0xD0]` etc. **Machine-verified form**
   (π/2 constant @ `0x180C8DAC8`, `xmm8 = yaw − π/2`):

   ```
   offset = ( cos(yaw)·sin(pol)·A + sin(yaw)·B,
              cos(pol)·A + C,
              sin(yaw)·sin(pol)·A − cos(yaw)·B )
   ```

   `yaw` = first param (`yaw=0` → +X, positive → +Z), `pol` = second param
   (angle from vertical-down, i.e. `pitch_from_horizontal = π/2 − pol`),
   `A/B/C` = the controller's `CameraPositionOffset` fields
   (`[r14+0xC8/0xCC/0xD0]` = distance / lateral / height).

   **Move-pitch adjustment path (partially decoded):**
   `AdjustCharacterCameraPosition` (`0x180AC8F10`, sole caller `0x180B1006E`)
   composes a rotation from two angles in its params struct
   (`[rsi+0x20]`, `[rsi+0x24]`, scale `[rsi+0x28]`) and writes the result into
   `[r14+0x8C]` — the vertical offset consumed here as `C` — plus the adjusted
   position into `[r14+0x38]`. The easing/timing consumption of
   `CameraMovePitchSmoothTime` / `CameraMovePitchApplyTimeInterval` is still open.

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

## 7. Where the actual values live (resolved, third pass)

`KTableList::LoadCameraConfigFile` resolves `CameraConfig` through
`KFilePath` → **`Represent\filepath.ini`**, a logical-name → file mapping
loaded via `SemanticX64.dll!CreateRLFile` (`KFilePath::LoadFilePath`,
`0x1803F0920`). The recovered mapping (Aug-build extraction, `represent-out\represent\filepath.ini`):

| Key | File |
|---|---|
| `CameraConfig` | `Represent/camera/config.ini` |
| `CameraSegmentConfig` | `Represent/camera/CameraSegmentConfig.json` |
| `CameraLockTargetConfig` | `Represent/camera/CameraLockTargetConfig.txt` |
| `CameraCommon` | `Represent/camera/camera_common.krl.txt` |
| `SkillMoveCamera` | `Represent/camera/skill_move_camera.txt` |
| `AirCombatCamera` / `NpcDialogCamera` / `CarrierCamera` / `GliderCamera` / `DynamicFollowCamera` | `Represent/camera/*.krl.txt` |
| `PlayerRushCamera` | `Represent/player/player_rush_camera.txt` |
| `CameraShake` / `CameraAni` / `SceneAnimationCamera` / `AnimationCameraModel` | `Represent/...` |
| `PlayerSkillMoveAnimationTable` | `Represent/player/player_skill_move_animation.txt` |

**Why the current (2026-09) local paks don't yield these files:** the client
pak system stores hash-named paks (`Package.UseFileName=0`, manifest
`Trunk.Dir`, `Package.RootDir=../../../PakV4` from
`bin64\KGPK4_StreamDownloader.zhcn_hd.conf`), and the `Represent` tree ships via
**CDN mini-updates** (`jx3v4*-miniupdate.xoyocdn.com/jx3hd_v4_mini/…`) rather
than the base install paks. `PakV4SfxExtract` against the local install finds
`data\source\...` but not `represent\...`. The Aug-build extraction (cached in
`jx3-web-map-viewer\cache-extraction\pakv4-probe\`) has the raw files; getting
the current set needs either the CDN stream downloader or engine-side VFS
enumeration after update.

### Real values recovered from the cached raw tables

`Represent/camera/skill_move_camera.txt` (columns: `SkillID`, `bAniTag`,
进入时间 ms, 退出时间(缓出) ms, 视场角 rad (0~2π) / 固定角度 deg (≥30), 持续时间 ms,
屏幕效果编号, 边缘色温 (0~10), 色温饱和度 (0~1)):

| SkillID | bAniTag | enter ms | exit ms | fov/angle | duration ms | fx | edge temp | sat |
|---|---|---|---|---|---|---|---|---|
| 3119 | | 800 | 800 | 0.3 | 120000 | | | |
| 20788 | | 300 | 800 | 0.7 | 100000 | 1 | 2 | 0 |
| 21000 | | 300 | 300 | 40 (deg) | 100000 | 1 | 0.2 | 0 |
| 25252 | | 300 | 300 | 0.3 | 100000 | 1 | 0.2 | 0 |
| 100182 / 124841 / 200415 | 1 | 560/1800/800 | 200 | 0.3 | 0 | 1 | 0.2 | 0 |

`Represent/skill/skill_dash.txt` maps **SkillID → AnimationID** for dash skills
(e.g. 228→347, 424→478, 1577→700, 1578→702, 21292→700, 38011→-1 …) — combine
with the `tools/netcode/measure_skill_motion.py` pipeline (skill → tani → ani →
root motion) to get per-dash distances.

`Represent/player/player_skill_move_animation.txt` maps
**SkillMoveID → AnimationID + bAllowSkillMoveDst** (drives `KRLRushState` /
skill move animation).

Files kept locally (raw game data, git-ignored): `proof/netcode/camera_files/`.

## 8. Verified constants + shake + obstruction (fourth pass)

### 8.1 Machine-verified default constants (current build's effective values)

`tools/netcode/verify_camera_defaults.py` pairs each `GetFloat(key, default)`
with the actual constant the loader passes, following register chains.
Output: `proof/netcode/camera_defaults_verified.txt`.

| Key (loader) | Default | Evidence |
|---|---|---|
| `CameraHeight` (carrier) | **0.0** | xorps |
| `CameraMaxDeltaYaw` | **6.2800002 (2π)** | const @ `0x180D09F48` |
| `CameraMaxDeltaPitch` | **1.56** | const @ `0x180D09F44` |
| `MaxDragSpeed` | 0.00314 (placeholder) | const @ `0x180D09F40` |
| `RotationSpeed` | 0.00314 (placeholder) | same const |
| `ForbidStrafe` / `ForbidRotation` / `TurnCameraYawToObjectYaw` | bool, no default (false) | GetBool `+0x70` |
| `TargetDistance` | **1.0** (also clamped to 1.0 when table ≤ 0) | const @ `0x180C82240` |
| `SmoothTime` | **1.0** | same const via xmm6 chain |
| `InitCameraPitch` | **π (3.1415927)** — sentinel: caller-provided value used when table returns π | const @ `0x180C8D19C` |
| `InitCameraAngle` | 0.0 | xorps |
| 10-row move-pitch table: `ZoomLength`, `CameraMovePitchApplyAngle/SmoothTime/AdjustPitch/AdjustMaxPitch/ApplyMaxPitch/ApplyTimeInterval`, `CameraAdjustYawWhenMoveTurn` | all **0.0** | xorps |
| `CameraAdjustYawWhenMoveTurnDisableAngle` | **0.26 rad ≈ 15°** | const @ `0x180C8D3E8` |

### 8.2 Camera shake (recovered, `0x180B10A70`)

`proof/netcode/disasm/camera_setcore.txt` — the post-placement updater is the
**shake controller**:

- state `[+0x1EC]`: 0 = off/idle, 1 = burst active
- burst: phase = cos((t mod period)/period · 2π); position jitter and rotation
  scaled by amplitude; each cycle amplitude ×= decay (`[+0x208]`); after
  `max cycles` (`[+0x1F4]`) the shake disables itself
- idle branch: 3× `rand()` offsets within ± amplitude applied directly
- rotation weights `[+0x20C]/[+0x210]`, rotation scale `[+0x1FC]`

### 8.3 Obstruction: engine-side flag

No collision code in SO3Represent — obstruction is an engine camera feature:
`KG3DEngineX64.dll` contains the parameter **`bObstructdAvert`**
("obstructed avert") — the engine camera pulls forward when the view is
obstructed. Reproduction: raycast anchor→camera, clamp distance to hit−ε,
smooth return when clear (implemented in the reference).

## 9. Remaining open items

1. `Represent/camera/config.ini` (per-mode row values + 7 object-position-offset
   files) and the `*.krl.txt` rows — **not shipped in the current build**
   (downloader confirmed 0 missing files; Sep build moved configs server-side).
   Current-build effective values are the verified defaults above; the krl file
   format is a simple key-value float table (`sLoadNumberFromFile`,
   `0x180857DF0`), so old-build files parse trivially if ever obtained.
2. Mouse sensitivity defaults (`userdata\custom.dat`, binary; user setting).

## 10. Fifth pass — air-combat, shake, follow-action, skill-move, camera-ani

All machine-checked where numbers are given (`proof/netcode/disasm/`,
`camera_defaults_verified.txt`).

**Air-combat params (VERIFIED defaults, `LoadAirCombatParams` @ `0x180AC9C00`):**

| Key | Default |
|---|---|
| `InitCameraDistance` | 2000 |
| `AdjustEyeScale` | 0.2 |
| `YawRange` | 70 |
| `EnterYawAngleSpeed` | 500 |
| `EnterPitchAngleSpeed` | 500 |
| `FinalYawAngle` | 30 |
| `ScreenWidthLimit` | 0.2 |
| `ScreenHeightLimit` | 0.25 |

**Camera shake (VERIFIED model):** logic → `OnSetCameraShake(int)` (event
adaptor) → row from the `CameraShake` table via
`KTableList::GetCameraShakeConfig` with keys `ShakeType`, `ShakeIntensity`,
`ShakeTotalTime`, `ShakeCycleCount`, `ShakeOffsetX`, `ShakeOffsetY`,
`ShakeDecayRate`, `ShakePeriodTime` → updater `0x180B10A70` applies the
cos/decay/jitter curve (fields `+0x1EC` mode, `+0x1F4` amplitude/cycles,
`+0x200` period, `+0x208` decay).

**Follow-action (VERIFIED mechanism):** `SetCameraFollowCharacterAction`
(`0x180ACE370`) writes an enum at camera object `+0x27C`; when nonzero,
`SetCharacterCameraPosition` resolves the `s_face` target and uses it as the
camera look-at (`0x180B0F152–0x180B0F187`). Min/MaxDis/Speed keys load via krl;
distance blend still open.

**Skill-move camera (partial):** `ApplySkillMoveCameraTag` (`0x1802F8D20`) uses
`pcSkillMoveCameraConfig`; config field `+0x22C` = duration → expiry timestamp
`now + duration` stored at `[obj+0x8308]` (`0x180542440`), gated by character
flag bit 30. FOV interpolation curve still open.

**Camera animation (VERIFIED):** `KRLCameraAni::SyncCamera` (`0x1805AE2xx`)
writes track position to camera `[+0x44/0x48/0x4C]` and look-target to
`[+0x50/0x54/0x58]` per frame, then engine setters via vtable
(`+0x30`, `+0xA0`, `+0x50`, `+0xA8`).

**Sprint camera (VERIFIED structure, §2):** int speeds `[+0x1D8]`/`[+0x1DC]`
× **π/128**, slew limiter `0x180B13B60`, final clamp
`clamp(speed_angle, max(min_angle, base+slew)) − base`.

## 11. Seventh pass — move-pitch, height state machine, carrier, skill-move apply

**Move-pitch / slope (`AdjustCameraForSlope`, blend `0x180B0FF45–0x180B1001E`):**
- `[rsi+0x54]` is the row's apply-angle; if > ε:
  `angle[i] += slopeOffset[i] · weight · [rsi+0x54]` (`xmm11` weight).
- slope offsets live in `[r14+0x14C/0x150/0x154]` and refresh only when
  `now − [r14+0x158] >` **1000.0 ms** (double const `0x180C8E3D0`).
- adjusted angles go to `AdjustCharacterCameraPosition` (`0x180B1006E`) →
  `[r14+0x38/0x3C/0x40]`.

**Height state machine (`0x180B100F0–0x180B10283`, state `[r14+0xC0]`):**
- 3→1 and 4→2 resets; transitions gated by `[r14+0xB4]`, `[r14+0xB8]` vs
  `[r14+0xBC]`, `[r14+0xE4]`;
- state 2: instant `[r14+0x8C] = target`;
- state 1: rate-limited `target = [rdi+0x24] − dt·(π/3000)` — const
  `0x180D0E180 = 0.0010471976` rad/ms ≈ **60°/s** — never below current, flips
  to state 2 on reach.

**Carrier smoothing (`SmoothToCarrierCamera` @ `0x180AD0230`):** per-axis
exponential step, `delta / timeFactor([rcx+0x44])`, ratio clamped `[0,1]`
(consts `1.0` @ `0x180C82240`, abs-mask @ `0x180C8D240`), state
`[rdi+0x60..0x6C]`, outputs `pfRetYaw` / `pfRetPitch` / `pfRetAngle`.

**Skill-move apply (`0x1804FBAE0`):** params → `[obj+0x7458/0x7460/0x7464]`,
rotated via `0x180024A78` → `[obj+0x746C/0x7470/0x7474]`, `[obj+0x7448]`, then
engine camera vtable `+0xA48`.

**Reference implementation updated** to the verified rate-limited pitch motion
(`PITCH_RATE` = π/3000 rad/ms ≈ 60°/s); all 13 smoke checks pass.

## 12. Eighth pass — engine chase camera + real engine configs

**Engine chase camera** (`KG3DEngineX64.dll`, loader `0x1804C1660`): reads ini
section **`[Camera]`** via `g_OpenIniFile`: `nChaseType`, **`bObstructdAvert`**,
`fChaseRate`, `fMaxDistance`, `fMinDistance`, `fMaxAngelVel`, `fMinAngelVel`,
`fAngelRateHor`, `fAngelRateVel`, `fDisZoomRate`, `fFlexCoefficient`,
`fDampCoefficient`, `bUseFlexibilitySys`, `fFlexRate`, `fDistance`, `fAngleHor`,
`fAngleVel`, `fLootAtOffsetY`, `bLockedCamera`, `fFovy`.

Struct offsets (`rdi`): `+0x10` bObstructdAvert, `+0x18` nChaseType,
`+0x1C` fChaseRate, `+0x20/24` fMax/fMinDistance, `+0x28/2C` fMax/fMinAngelVel,
`+0x30/34` fAngelRateHor/Vel, `+0x38` fDisZoomRate, `+0x3C/40`
fFlex/fDampCoefficient, `+0x44` bUseFlexibilitySys, `+0x48` fFlexRate,
`+0x4C` fDistance, `+0x50/54` fAngleHor/Vel, `+0x58` fLootAtOffsetY,
`+0x5C` bLockedCamera, `+0x60` fFovy.

So the engine's third-person chase camera IS a configurable spring/flex system
(`fFlexCoefficient`, `fDampCoefficient`, `fFlexRate`) with distance/angle limits
and obstruction avert — values live in the missing `[Camera]` ini.

**Real engine configs found** in
`SeasunDownloaderV2.4\seasun\client\_HttpFileForDebug_\local\data\public\`
(engine data/public is streamed, not in client paks):
`lookatconfig.ini` (head/neck look-at: Spine1→Spine2 `30 / 0.4 / 90 / 20 / 2.5`,
Neck→Head `20 / 0.9 / 30 / 75 / 4`, `FaceSocket=s_face`),
`flexconfig.ini` (`fDamping = 0.675 / 0.2`), `3denginesettings.ini`,
`environmentdefault.ini`, `physics.ini`, and more.
