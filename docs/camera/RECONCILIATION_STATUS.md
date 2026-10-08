# Camera reconciliation status (2026-09-24)

Disposition of the other agent's "Camera notes vs client" report. Branch
`camara-fix`; client smoke 24/24 (`camera_smoke.exe`).

| Report item | Status | Where |
|---|---|---|
| A W1 wheel = `Camera_Zoom(0.9/1.1)`, Step is not the wheel | fixed | `docs/camera/REAL_VALUES.md` §5, `docs/camera/ADOPTION_FOR_ONLINE_CLIENT.md` §2, `REBORN_CAMERA_SPEC.md` |
| A W2 `NearByWallDistance` has no reader | fixed | `docs/camera/REAL_VALUES.md` §3/§8, `CAMERA_ADOPTION...` §4 |
| A W3 fixed-`camY` placement claim | fixed (sphere offset) | `CAMERA_ADOPTION...` §3 |
| A W4 "never write `camSys.Yaw` from the measured view" | fixed (reversed) | `docs/camera/COMPLETION_PLAN.md` §1.2 C |
| A W5 15 fps speed basis | fixed (16 Hz -> 96/320 u/s) | `docs/camera/REAL_VALUES.md` §7 #4, `CAMERA_ADOPTION...` §1 |
| B S2 `EyeScale` missing from the aim math | done | `RebornClient.cs geometricAimPitch`/`aimPitchOf` use `Distance * EyeScale` (`52cca8f`) |
| B S1 aim re-pin after zoom/sprint/EyeScale | done | `aimDirty` on distance change (`52cca8f`) |
| B S3 ground-clamp aim guard | done (over-claim corrected) | `aimPitchOverride` aims at the anchor from the clamped point **and marks `aimDirty`** so the aim sync consumes it (`52cca8f` + this batch) |
| B S4 pitch correction deadband | done | 1 px deadband in the orbit block (`52cca8f`) |
| B S5 one distance clamp | done (over-claim corrected) | `ClampDistanceUnits` reads the caps from the **character row** (only that row is written from custom.dat); ZoomBy + F11 + sprint max go through it |
| B obstruction 100 u obstructed-side hysteresis | done | release only 100 u past desired or fully clear (`6e1d1b2`) |
| B S6 RMB instant body turn | done (over-claim corrected) | `RotationSpeed` rows are engine int speeds (0.00314); values < 1 rad/s fall back to the engine key-rotation default `fChaseRate` = pi rad/s (~180 deg/s) |
| B S7 LMB click vs drag | done | 4 px dead zone; cursor locks only once the drag starts (`6e1d1b2`) |
| B move-reactive camera (`CameraMovePitch*`, `CameraAdjustYawWhenMoveTurn`) | mechanism done, opt-in | `CameraSystem.AdjustPitch` + `FollowYaw`, fed to the engine; `RC_MOVE_PITCH=1` because the current build's real table is 0.0 (`6e1d1b2`) |
| B remaining settings (reset speeds, `nCameraMode`, smoothing/curve/eye-follow) | read + joystick mode applied | `CameraSettings.cs`; reset-speed/curve/eye-follow semantics remain read-only (`11b2168`) |
| B verification (S1/S2/S5 smoke, per-run logs, camdbg fields) | partial, stated honestly | only the **S5 clamp helper** is smoke-covered; S1/S2 (EyeScale/re-pin) are client-side and have no unit test; per-run logs + `camdbg eff=/clamp=` exist (`11b2168`) |
| B pitch limit + sprint trigger | done, trigger removed | orbit pitch clamp is the engine's pi/2 - 0.0157 (was -0.05); the sprint camera mode followed `wSprint` (double-tap W) — the WW trigger was **removed 2026-09-30** (sprint row reachable via `RC_CAM_MODE` only); placement smoothing now uses the shared CharacterCameraSmoothTime in every mode (camera-wwdrag fix) |

## Still open (data-gated or native-interface work)

| Item | Why blocked | Path |
|---|---|---|
| Per-mode `.krl` rows (CameraCommon/AirCombat/NpcDialog/Carrier/Glider/DynamicFollow), `CameraLockTargetConfig`, `player_rush_camera.txt` | not in the local install (CDN mini-update) | `docs/camera/REAL_VALUES.md` §7 #1 |
| Engine `[Camera]` ini (`bObstructdAvert`, `fChaseRate`, flex, `fFovy`) | ini not shipped; constants hardcoded from the constructor defaults | §7 #2 |
| 广角/FOV path (config.ini `[UIVideoSetting]`, skill-move FOV, nameplate compensation) | binding not decoded; engine caps unprobed | §7 #7, §6 |
| Engine caps `fMinCameraDistance`/`fMinCameraAngle`/`fMaxCameraAngle` | compiled per option level | live caps probe from the host (§6) |
| `bObscatleCamera` per-mesh gate, alternate 9-ray mode | extraction doesn't store the mesh property; 9-ray trigger field unknown | `docs/camera/WALL_OBSTRUCTION.md` (remaining unknowns) |
| Real anchor head/socket (`Bip01 Head`, `s_face`, carrier sockets) | managed host exposes no bone/socket transform | native camera/actor interface recon (Phase 0) |
| Mounts, gliding, air combat, dialogs, spectate, skill-move/dynamic-follow cameras | per-mode rows + activation triggers missing | completion plan Phase 4-6; §7 #1 |
| `CameraShake`, `SetFollowAction`, `TrackCamera` triggers | no gameplay hook in the host (model classes exist) | completion plan Phase 6 |
| `IKG3D_Camera` / `KG3DCameraProxy` (look-at, FOV, engine obstruction) | never reconned | `docs/camera/STATUS.md` next step / Phase 0 |
| `MinCameraDistance = 100 u` | engine cap unprobed (placeholder) | live caps probe |
| Character visibility when the camera is inside (park-below-map hack) | no visibility/alpha in the managed API | **mitigated**: hide/show now keyed on the real post-clamp camera->anchor distance (90/150 u) and the model re-attaches on every handle change; inside-character views are gone in testing (user-confirmed). Still a host approximation of the game's near-plane clipping. |

## Progress after this doc (latest commits)

| Item | State |
|---|---|
| Engine terrain + entity rays (the game's camera backends) | **done** - `EngineRay` calls `terrain->vt[+0xB8]` and the space-manager ray `0x180A5E4C0` directly (guard bypass); live `terrD=91`, `sceneE=1865`; wired into the 5 obstruction probes |
| Close-character view | **fixed** - real camera->anchor distance hide (90/150) + per-frame re-attach |
| Distance caps | **done** - `SetTargetDistance` + `ClampDistanceUnits` on the character-row caps for wheel/F11/sprint (min and max); game max default 2000 u |
| 广角 / FOV | **done** - `VideoSettings` reads `config.ini` CammeraAngle (48 deg here) and applies the game panel semantics (30-60 deg, default 50) as `SetViewAngleFactor`; projection-only |
| Pitch limit / sprint trigger / body-turn rate / S3-S5 over-claims | **done** - engine limit pi/2-0.0157, `wSprint`, pi rad/s fallback, caps row + aimDirty |
| Engine caps probe (`fMinCameraDistance`, `fMinCameraAngle`, `fMaxCameraAngle`) | **blocked by design** - the caps provider `JX3UIX64.dll` is present in `bin64` but **not loaded** by the host, so no in-process caps object exists; the 100 u min stays a placeholder and the documented defaults are used |
| Host near-plane read | **blocked** - the engine view-manager singleton getter (`0x1801433E0`) blocks on the client thread (froze the app); needs an engine-context callback with a thread guard; near-plane setting API likewise unexposed |
| Item 2 - 9-ray probe set | **done (opt-in)** - centre + 8 perimeter at 45 deg matches the game's alternate mode; the `+0x15c` trigger is not recovered, so `RC_CAM_9RAY=1` (default remains the 5-ray set) |
| Item 2 - `bObscatleCamera` per-mesh gate | **blocked (data)** - the mesh-property inis are not in the extracted collision bake |
| Item 3 - mode cameras | **harness done** - `RC_CAM_MODE=<row>` activates carrier/air_combat/npc_dialog/god rows; the gameplay triggers (mount, dialog, air combat, spectate) do not exist in the host |
| Item 3 - shake / follow-action / track | **shake done** (fires on the skill cast, host amplitude, per-skill rows data-gated); follow-action and track have no trigger in the host |
| Item 4 - operation-mode engine coupling (`Camera_EnableControl`, `Scene_LockMouseRotation`, `Camera_SetResetSpeed`, `Camera_UseFullAngle`) | **blocked (API)** - these live in the represent/UI layer; the managed host wrapper exposes no equivalent |
| Item 4 - settings write path | **blocked (no UI)** - nothing in the host changes the settings; the read path (custom.dat) is done |
| Item 4 - controls C1-C13 | **blocked (data)** - the gap-register contents live in the notes worktree (`control-system-notes`) not in this repo |
| Obstruction apply exact rule | **done** - shortening applies immediately; 50/100 window only for receding candidates; outside it clears (engine `0x1804C1208`) |

## Audit follow-up (latest; corrects the over-claims above)

Fixed: `RC_CAM_MODE` is no longer overridden by the automatic character/sprint
switch; `UpdateDistance` uses the active mode row's `TargetDistance`/`SmoothTime`
(carrier verified live at 700 u); `EngineRay.ProbeNearPlane` and the view-manager
delegates are deleted (the getter deadlocks the engine even on a worker thread);
the move-pitch yaw double-apply is fixed (engine feed uses `oxSend`, model
integration uses the drag `ox`); the startup `camSys.Distance` is clamped; the
obstruction apply rule above is accurate.

Still open, stated plainly: no self-hit (own-body) filtering or watchdog on the
native rays (raw calls per frame, hardcoded RVAs); `CameraSystem.Mouse` pitch
clamp is unused/no-op; 广角 does not read the panel `VideoSetting_WidAngle`, the
raw<30 `+fMinCameraAngle` rule is not applied and `SetViewAngleFactor` <-> FOV is
unverified; the 9-ray perimeter is a 22 u circle, not the engine's FOV/aspect
basis; `bObscatleCamera` and the engine caps stay blocked (data/provider); the
operation-mode engine coupling and controls C1-C13 are **not implemented here**
(not a data blocker); the `control-system-notes` camera pages are still the
pre-correction versions.
