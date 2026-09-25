# Camera reconciliation status (2026-09-24)

Disposition of the other agent's "Camera notes vs client" report. Branch
`camara-fix`; client smoke 24/24 (`camera_smoke.exe`).

| Report item | Status | Where |
|---|---|---|
| A W1 wheel = `Camera_Zoom(0.9/1.1)`, Step is not the wheel | fixed | `CAMERA_REAL_VALUES.md` §5, `CAMERA_ADOPTION_FOR_ONLINE_CLIENT.md` §2, `REBORN_CAMERA_SPEC.md` |
| A W2 `NearByWallDistance` has no reader | fixed | `CAMERA_REAL_VALUES.md` §3/§8, `CAMERA_ADOPTION...` §4 |
| A W3 fixed-`camY` placement claim | fixed (sphere offset) | `CAMERA_ADOPTION...` §3 |
| A W4 "never write `camSys.Yaw` from the measured view" | fixed (reversed) | `CAMERA_COMPLETION_PLAN.md` §1.2 C |
| A W5 15 fps speed basis | fixed (16 Hz -> 96/320 u/s) | `CAMERA_REAL_VALUES.md` §7 #4, `CAMERA_ADOPTION...` §1 |
| B S2 `EyeScale` missing from the aim math | done | `RebornClient.cs geometricAimPitch`/`aimPitchOf` use `Distance * EyeScale` (`52cca8f`) |
| B S1 aim re-pin after zoom/sprint/EyeScale | done | `aimDirty` on distance change (`52cca8f`) |
| B S3 ground-clamp aim guard | done | `aimPitchOverride` aims at the anchor from the clamped point (`52cca8f`) |
| B S4 pitch correction deadband | done | 1 px deadband in the orbit block (`52cca8f`) |
| B S5 one distance clamp | done | `CameraSystem.ClampDistanceUnits` (ZoomBy, sprint upper bound, F11 reset) (`52cca8f`) |
| B obstruction 100 u obstructed-side hysteresis | done | release only 100 u past desired or fully clear (`6e1d1b2`) |
| B S6 RMB instant body turn | done | rate-limited turn (`RotationSpeed` row, 10 rad/s default) (`6e1d1b2`) |
| B S7 LMB click vs drag | done | 4 px dead zone; cursor locks only once the drag starts (`6e1d1b2`) |
| B move-reactive camera (`CameraMovePitch*`, `CameraAdjustYawWhenMoveTurn`) | mechanism done, opt-in | `CameraSystem.AdjustPitch` + `FollowYaw`, fed to the engine; `RC_MOVE_PITCH=1` because the current build's real table is 0.0 (`6e1d1b2`) |
| B remaining settings (reset speeds, `nCameraMode`, smoothing/curve/eye-follow) | read + joystick mode applied | `CameraSettings.cs`; reset-speed/curve/eye-follow semantics remain read-only (`11b2168`) |
| B verification (S1/S2/S5 smoke, per-run logs, camdbg fields) | done | clamp smoke check; `reborn_<timestamp>.log`; `camdbg ... eff= clamp=` (`11b2168`) |

## Still open (data-gated or native-interface work)

| Item | Why blocked | Path |
|---|---|---|
| Per-mode `.krl` rows (CameraCommon/AirCombat/NpcDialog/Carrier/Glider/DynamicFollow), `CameraLockTargetConfig`, `player_rush_camera.txt` | not in the local install (CDN mini-update) | `CAMERA_REAL_VALUES.md` §7 #1 |
| Engine `[Camera]` ini (`bObstructdAvert`, `fChaseRate`, flex, `fFovy`) | ini not shipped; constants hardcoded from the constructor defaults | §7 #2 |
| 广角/FOV path (config.ini `[UIVideoSetting]`, skill-move FOV, nameplate compensation) | binding not decoded; engine caps unprobed | §7 #7, §6 |
| Engine caps `fMinCameraDistance`/`fMinCameraAngle`/`fMaxCameraAngle` | compiled per option level | live caps probe from the host (§6) |
| `bObscatleCamera` per-mesh gate, alternate 9-ray mode | extraction doesn't store the mesh property; 9-ray trigger field unknown | `CAMERA_WALL_OBSTRUCTION.md` (remaining unknowns) |
| Real anchor head/socket (`Bip01 Head`, `s_face`, carrier sockets) | managed host exposes no bone/socket transform | native camera/actor interface recon (Phase 0) |
| Mounts, gliding, air combat, dialogs, spectate, skill-move/dynamic-follow cameras | per-mode rows + activation triggers missing | completion plan Phase 4-6; §7 #1 |
| `CameraShake`, `SetFollowAction`, `TrackCamera` triggers | no gameplay hook in the host (model classes exist) | completion plan Phase 6 |
| `IKG3D_Camera` / `KG3DCameraProxy` (look-at, FOV, engine obstruction) | never reconned | `CAMERA_STATUS.md` next step / Phase 0 |
| `MinCameraDistance = 100 u` | engine cap unprobed (placeholder) | live caps probe |
| Character visibility when the camera is inside (park-below-map hack) | no visibility/alpha in the managed API | native camera interface; per-represent `KRLCharacter::SetPlayerControlVisibleState` (`0x1804E4150`) exists in the DLL |
