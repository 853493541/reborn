# JX3 camera config files — inventory and where the values live

Static inventory recovered 2026-09-23. This is a new file; no existing research
file was changed. Companion to `CAMERA_WALL_OBSTRUCTION.md` (engine defaults)
and `CAMERA_REAL_VALUES.md` (real values found).

## 1. The definitive logical-name map

`JX3RepresentX64.dll` resolves camera files through `KFilePath`
(`Represent/filepath.ini`, loaded by `SemanticX64.dll!CreateRLFile`;
`KFilePath::LoadFilePath` `0x1803F0920`). The recovered `filepath.ini`
(Aug-build extraction, `pakv4-probe\represent-out\represent\filepath.ini`)
maps:

| Logical name | File |
|---|---|
| `CameraDirectory` | `Represent/camera` |
| `CameraConfig` | `Represent/camera/config.ini` |
| `CameraSegmentConfig` | `Represent/camera/CameraSegmentConfig.json` |
| `CameraLockTargetConfig` | `Represent/camera/CameraLockTargetConfig.txt` |
| `CameraPostRender` | `Represent/camera/CameraPostRender.krl.txt` |
| `CameraCommon` | `represent/camera/camera_common.krl.txt` |
| `AirCombatCamera` | `Represent/camera/AirCombatCamera.krl.txt` |
| `NpcDialogCamera` | `Represent/camera/NpcDialogCamera.krl.txt` |
| `CarrierCamera` | `represent/camera/CarrierCamera.krl.txt` |
| `GliderCamera` | `represent/camera/GliderCamera.krl.txt` |
| `DynamicFollowCamera` | `Represent/camera/DynamicFollowCamera.krl.txt` |
| `CameraShake` | `represent/camera/camera_shake.krl.txt` |
| `AnimationCameraModel` | `represent/camera/animation_camera_model.txt` |
| `PlayerAnimationEnableCameraFollow` | `represent/camera/camera_follow_animation.txt` |
| `SceneAnimationCamera` | `Represent/camera/SceneCameraAni.tab` |
| `NpcDialogSceneCameraAni` | `Represent/camera/npc_dialog_scene_camera_ani.txt` |
| `SkillMoveCamera` | `Represent/camera/skill_move_camera.txt` |
| `PlayerRushCamera` | `Represent/player/player_rush_camera.txt` |
| `CommonNumber` | `Represent/common/number.krl.txt` |

`KTableList::LoadCameraConfigFile` (`0x180832DD0`) additionally reads
`OBJECT_POSITION_OFFSET_FILE_NAME` with `Count` asserted == 7 and
`Item_0..Item_6` (64-byte names, `g_szCameraObjectPositionOffsetFiles`), and
then loads `CameraLockTargetConfig`.

## 2. Engine `[Camera]` ini — not on disk

`KG3DEngineX64.dll` loader `0x1804c1660` reads the `[Camera]` section
(`bObstructdAvert`, `fChaseRate`, `fMaxDistance`, `fFovy`, ...) through
`g_OpenIniFile`, but no ini file on the local disk contains those keys:

- A recursive `Select-String` over every `*.ini` under
  `C:\SeasunGame\Game\JX3` matched **only `KG3DEngineX64.dll` itself**
  (the embedded key strings). It matched `bObstructdAvert|fChaseRate`.
- So all engine `[Camera]` behaviour in the current install runs on the
  compiled constructor defaults (see `CAMERA_WALL_OBSTRUCTION.md` §Settings);
  the streamed override has not been observed.

### Engine `[Camera]` key usage in this build

| Key | Consumed as |
|---|---|
| `bObstructdAvert` | obstruction gate (parent `+0x204`); see `CAMERA_WALL_OBSTRUCTION.md` |
| `nChaseType` | camera-key input mode (nested `+0x18` = parent `+0x2b0`, compared to 0 / 2) |
| `fChaseRate` | camera-key rotation rate (nested `+0x1c` = `+0x2b4`, × dt, default π) |
| `fMaxDistance` / `fMinDistance` | read via `+0x2b8` / `+0x2bc` |
| `fFlexCoefficient` / `fDampCoefficient` / `bUseFlexibilitySys` | flex integrator (`+0x1fc`/`+0x200`/`+0x208`) |
| `fFovy` | part of the FOV block `+0x48..+0x54`, applied via camera vtable `+0x68` |
| `fFlexRate` | no reader found in the track-camera code (unused in this build) |

## 3. What *is* available locally

| File | Where | Content |
|---|---|---|
| `Represent/camera/skill_move_camera.txt` | `SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\pakv4-probe\skill-tables-out\` | Full per-skill camera table (SkillID, bAniTag, enter/exit ms, FOV/angle, duration, screen FX, edge temp/sat). Rows: 3119, 20788, 21000, 25252, 100182, 124841, 200415. |
| `Represent/player/player_rush_camera.txt` | `SeasunDownloaderV2.4\jx3-web-map-viewer\tmp\nieyun-rush-out\represent\player\` | `CameraID / File / Speed` table (4 rows: `\data\movie\camera\16.mani`, `17.mani`, speed 1). |
| `Represent/player/player_skill_move_animation.txt` | `reborn-netcode\proof\netcode\camera_files\` | SkillMoveID -> AnimationID + bAllowSkillMoveDst. |
| `Represent/skill/skill_dash.txt` | same | SkillID -> AnimationID for dash skills. |
| `Represent/common/number.krl.txt` (CommonNumber) | `proof/gravity/number.krl.txt` | Camera defaults incl. `NearByWallDistance=800`, `CameraMaxDistance=1245`, `CharacterCameraSmoothTime=60`, sprint/carrier params. |
| mesh property inis | `_HttpFileForDebug_\local\data\source\...` | `bObscatleCamera` (and the other `[Display]` bools) per map/npc/player/effect mesh. |

Missing locally: `config.ini`, `camera_common.krl.txt`, `AirCombatCamera`,
`NpcDialogCamera`, `CarrierCamera`, `GliderCamera`, `DynamicFollowCamera`,
`camera_shake.krl.txt`, `CameraSegmentConfig.json`, `CameraLockTargetConfig.txt`,
the 7 object-position-offset files, and the engine `[Camera]` ini. Per prior
research these ship via the CDN mini-update set (or are resolved from the
client working dir at runtime), not the base install paks.

## 4. Represent `CommonNumber` location

`number.krl.txt` fills a struct embedded in the RL singleton:

- accessor `JX3RepresentX64.dll 0x180338bf0`:
  `mov rax,[0x180eddfe0]; add rax,0x24c14; ret`
- loader `0x180836510` -> `0x180836571`: `add rcx,0x24c14`, then
  `sLoadNumberFromFile` (`0x180857df0`).
- `NearByWallDistance` is the field at `CommonNumber+0x104` = `RL+0x24d18`.
  A direct reader of that field was not found in the represent
  camera-controller region; the consumer remains unidentified.

## 5. Content usage of `bObscatleCamera`

Across extracted mesh property inis:

- `bObscatleCamera=1` (default; blocks the camera): walls, stones, rubble,
  cacti, bricks, NPC bodies, player body.
- `bObscatleCamera=0` (excluded from camera obstruction): selected
  small-wood/plant pieces, altar/tomb decorations and similar.

The `KG3DMesh` constructor defaults the whole `[Display]` block to 1
(`bAutoProduceObstacle`, `bObscatleCamera`, `bSelectable`, `bHeightTest`,
`bOccluder`). See `CAMERA_WALL_OBSTRUCTION.md` and
`proof/netcode/camera_wall_obstruction.txt` §8.

## 6. Recovered parameter vocabularies (from the loaders)

The missing `.krl.txt` files' **key names** are recoverable from the code that
reads them:

### GliderCamera — `LoadGliderParams` (`JX3RepresentX64.dll 0x180afa3b0`)

Floats: `MaxDragSpeed`, `PitchSpeed`, `BeginTurnAngle`, `PitchMaxSpeed`,
`RollMaxSpeed`, `RotationSpeed`, `CameraPitchSpeed`,
`MaxObjectCamarePitchDifference`, `RollTurnOverAcceleration`,
`RollTurnBackAcceleration`, `PitchAcceleration`, `InitPitch`.
Intervals (min/max/step): `YawRange`, `PitchRange`, `ADRollRange`,
`QERollRange`, `TrackBlurSampleStrength`, `TrackBlurSampleDist`.
Also `SmoothTime`, `SprintCameraAngle`.

`EnterGlider` (`0x180af95a0`) reads the row: `TurnCameraYawToObjectYaw`,
`InitCameraAngleEnable`/`InitCameraAngle`,
`InitCameraDistanceEnable`/`InitCameraDistance`, `FallingRollRange`,
`FallingRollTime`, `ControlType` (`Aircraft` / `Aircraft2` / `Falling`).

### NpcDialogCamera — `EnterNpcDialog` (`~0x180b1a900`)

`InitCameraDistanceEnable`, `InitCameraDistance`, `InitCameraAngleEnable`,
`InitCameraAngle`, `InitCameraEyeScaleEnable`, `InitEyeScale`,
`InitPostRenderEnable`, `InitGatherBlurSize`, `InitFocus`, `InitNear`,
`InitDofDegree`, `PlaySceneCameraAni`. The post-render block also checks the
engine quality level (`RL+0x24f38` must be >= 4/5).

### DynamicFollowCamera — `EnterDynamicFollow` (`0x180ae8000`)

Table (`m_tabDynamicFollowCamera`, `RL+0x300`) with two sub-blocks:
`Rotate` (`Enable`, `CanBreak`, `SmoothType`, `InterpolateType`,
`SmoothSpeed` for type 2, `SmoothTime` for type 1, plus an angle limit) and
`Translate` (`MaxDistanceLimit`, `CanBreak`, same smooth fields).
`SmoothType` 1 = time-based, 2 = speed-based.

### CameraCommon — `m_tabCameraCommonConfig` (`RL+0x2f0`, loaded `0x18083438a`)

A table config consumed by the follow-camera code (the file
`camera_common.krl.txt` is not on disk).

### Already-documented mode vocabularies

- AirCombat: `docs/netcode/JX3_CAMERA_RESEARCH.md` §10 (`LoadAirCombatParams`
  `0x180AC9C00`).
- Carrier + Sprint + CommonNumber: `docs/CAMERA_REAL_VALUES.md`.
- CameraShake: `ShakeType`, `ShakeIntensity`, `ShakeTotalTime`,
  `ShakeCycleCount`, `ShakeOffsetX/Y`, `ShakeDecayRate`, `ShakePeriodTime`
  (same section).
- CameraPostRender: `KRLCameraPostRender` / `LoadPostRenderParam`
  (`JX3RepresentX64.dll`), DOF/gather-blur keys listed under NpcDialog above.

## 7. How players adjust camera settings (recovered)

Two different panels, two different persistence files.

### 7.1 Camera/mouse panel -> `custom.dat` (per role)

`userdata\<account>\<region>\<server>\<role>\custom.dat` holds
`VideoSettingPanel.tCameraStatic` (default `{fDragSpeed=1,
fMaxCameraDistance=2000, fSpringResetSpeed=1, fCameraResetSpeed=1,
nCameraMode=0, fDragPitchSpeed=1}`) plus the booleans `bCameraSmoothing`,
`bCurveCamera`, `bEyeFollow`, the operation-mode camera modes
(`UISetting_Comprehensive.nCameraModeInClassicMode/InJoystickMode`) and the
saved runtime view `g_Scene_tCameraRuntime` (`fYaw`, `fPitch` default -0.35,
`fCameraToObjectEyeScale`).

The panel applies values through represent Lua bindings; each writes the
camera controller's node of type `0xD` (reached via `this->+0x20` chain).
`[node+0x34]` selects the classic (`+0x6C..+0x80`) or joystick
(`+0x84..+0x98`) field pair:

| UI value | Binding | Classic / joystick field | Clamp |
|---|---|---|---|
| `fMaxCameraDistance` | `SetCameraMaxDistance` | `+0x74` / `+0x8C` | [1, 2000] |
| `fDragSpeed` + `fDragPitchSpeed` | `SetCameraDragSpeed(x, y)` | `+0x6C/+0x70` / `+0x84/+0x88` | [0.01, 10] |
| `fSpringResetSpeed` | `SetCameraSpringResetSpeed` | `+0x78` / `+0x90` | [0.01, 10] |
| `fCameraResetSpeed` | `SetCameraResetSpeed` | `+0x7C` / `+0x94` | [0.01, 10] |
| `nCameraMode` | `SetCameraFollowMode` | `+0x80` / `+0x98` (int) | [0, 3] |
| mode switch | `SwitchCameraMode` | child node named `"camera mode"` | - |

Implementations: drag `0x180ace2e0`, max distance `0x180ace520`, spring
`0x180aced60`, reset `0x180ace900`, follow mode `0x180ace3f0`, switch
`0x180acf6f0`; the Lua bindings are at `0x1802eb0a0`, `0x1802eb220`,
`0x1802eb7f0`, `0x1802eb5f0`, `0x1802eb1c0`, `0x1802eb9f0`.

### 7.2 Video panel -> `config.ini` (per install)

`video_base.lua` (UI) writes `[UIVideoSetting]` integers into `config.ini` and
calls `KG3DEngine.Set3DEngineOption("fCameraAngle", ...)` for the camera-angle
(FOV) slider; the script clamps it with `math.min(..., pi)` and a `0.3333333`
constant. `config.default.ini [KG3DENGINE]` holds the defaults:
`CammeraAngle=0.837757` (in-engine spelling), `CammeraDistance=80000`,
`CameraShake=0`, `bCameraShake=0`.

### 7.3 Operation modes and hotkeys

`operationmodebase.lua` switches `CLASSICAL_MODE` / `JOYSTICK_MODE`
(`SetCameraMode`) and toggles `Camera_EnableControl`, `Camera_UseFullAngle`,
`Scene_LockMouseRotation`, and calls `Camera_SetResetSpeed(3.5, 3.75)` (the
two literal floats in the bytecode).

Hotkeys (unchanged from `CAMERA_INPUT_CONTROLS.md`): LMB/RMB drag, wheel
`Camera_Zoom(0.9/1.1)`, F11 `Camera_SetForceReset(charYaw, -pi/12, 1)`,
Home/End view presets.

### 7.4 Not user-adjustable in this build

The engine `[Camera]` ini (obstruction, probes, flex, FOV fallback) and the
per-mode tables (`CameraCommon`, `AirCombatCamera`, ...) are not on disk, so
they run on compiled defaults; the UI has no controls for them. They would
arrive through a streamed update.

## 8. Evidence pointers

- `proof/netcode/disasm/camera_configfile.txt` — `LoadCameraConfigFile`.
- `proof/netcode/disasm/camera_player_config.txt` — `sLoadNumberFromFile`
  camera block (CarrierCamera*, `NearByWallDistance`, SprintCameraMaxOffset).
- `...\pakv4-probe\represent-out\represent\filepath.ini` — the mapping above.
- `proof/gravity/number.krl.txt` — CommonNumber values.
- `proof/netcode/camera_wall_obstruction.txt` — selected disassembly.
