# JX3 camera — real values found (2026-09-23)

Supplement to `docs/netcode/REBORN_CAMERA_SPEC.md` and
`docs/netcode/JX3_CAMERA_RESEARCH.md`. Those docs describe the camera
*structure*; this file records the real **values** and the **unit system**,
found while researching the follow camera. Nothing existing is replaced —
this only adds what was missing.

Raw extraction evidence: `proof/netcode/camera_number_krl_block.txt`.

Worktree: `reborn-camera`, branch `camara-imp`.

---

## 1. Units — 1 engine unit = 1 cm (1 m = 100 u)

Every camera distance below is in **engine units (cm)**. This matters: the
earlier `UnitsPerMeter = 192` assumption in the host was wrong by 1.92x.

| Fact | Value | Evidence |
|---|---|---|
| engine unit | 1 cm | adult male HD mesh 181.64 u = 1.816 m (`proof/netcode/character_size/mesh_census.json`) |
| 1 m | 100 u | same |
| 1 尺 (player-visible) | 64 u = 0.64 m | 4 skill-script sites (`/64`, `4*64*4*64`, `15*15*64*64`) |
| FBX export unit | **cm** | `UnitScaleFactor = 1.0`, `OriginalUnitScaleFactor = 1.0` in `MovieEditor\source\fbx\*\*.fbx` (FBX convention: 1.0 = cm) |
| pelvis height (adult male m2) | 98.59 u = ~1.0 m | `number.krl.txt` `m2PelvisHeight` |
| ragdoll limbs | radius 5–8 u, length 10–12 u | `proof/gravity/physic_character_param.krl.txt` (cm-scale body parts) |
| nameplate offset | 200 u = 2 m | `number.krl.txt` `nNpcAdjustOffsetY` |
| m -> 尺 factor | 1.5625 | `number.krl.txt` `CameraSpeedRatio = 1.5625 = 100/64` |

Canonical doc: `docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md` (this is the
calibrated system; the netcode index was revised to it in commit `cf93d9b`).

**Stale docs:** `docs/JX3_GRAVITY_RESEARCH.md` §3.1/§9,
`proof/gravity/verification.txt` and `docs/REBORN_JUMP_FALL_SPEC.md` still say
`1 m = 192 units` (they assumed the real-world 尺 = 1/3 m). Do not use those
for camera distances. The table values in them are fine — only their metric
labels are wrong (world-unit values are 1.92x the printed metres).

Camera consequences (100 u/m):

| Setting | Units | Metres |
|---|---|---|
| `fMaxCameraDistance = 2000` (default) | 2000 u | 20 m |
| `CameraMaxDistance = 1245` (`number.krl`) | 1245 u | 12.45 m |
| host follow `TargetDistance = 6` | 600 u | 6 m |
| host `CameraHeight = 2` | 200 u | 2 m |

---

## 2. Per-user camera settings (the real settings panel)

Path: `C:\SeasunGame\Game\JX3\bin\zhcn_hd\userdata\<account>\<region>\<server>\<role>\custom.dat`
(plain Lua-ish text: `{k="...",v=...}`). 92 role files carry the camera keys.

`VideoSettingPanel.tCameraStatic` (camera panel):

```
{fDragSpeed=1, fMaxCameraDistance=2000, fSpringResetSpeed=1,
 fCameraResetSpeed=1, nCameraMode=0, fDragPitchSpeed=1}   <- 68/92 roles, untouched
```

User-modified roles use `fMaxCameraDistance` = 760 / 1125 / 1245 and
`nCameraMode` = 1. Other keys (all roles):

| Key | Values seen | Note |
|---|---|---|
| `VideoSettingPanel.bCameraSmoothing` | true x92 | camera smoothing on |
| `VideoSettingPanel.bCurveCamera` | true x92 | curve camera on |
| `VideoSettingPanel.bEyeFollow` | true x66 / false x26 | |
| `UISetting_Comprehensive.nCameraModeInClassicMode` | 1 x47 / 0 x20 | |
| `UISetting_Comprehensive.nCameraModeInJoystickMode` | 0 x67 | |
| `g_Scene_tCameraStatic` | `{}` x92 | static camera, unused |
| `g_Scene_tCameraRuntime` | `{fYaw, fPitch, fCameraToObjectEyeScale}` | saved per role |

Saved runtime camera (the strongest default evidence):

- **fPitch = -0.35 rad (-20.05 deg)** on untouched roles -> real default pitch.
- observed fPitch range: -0.17 .. -0.78 rad (users tilt the camera).
- `fCameraToObjectEyeScale` = 1 (0.9 on some roles) — distance scale.
- `fYaw` matches the map init yaw (see §4).

Open: the exact wiring of the panel values (multipliers on the `number.krl`
base rates; `nCameraMode` 0/1 meaning; `bCurveCamera` / `bEyeFollow` behaviour)
is not decoded yet — see §6.

---

## 3. Real camera defaults — `Represent/common/number.krl.txt`

The earlier camera research looked for `Represent/camera/*` (missing) but
`Represent/common/number.krl.txt` **is** available and contains a real camera
block (extracted: `proof/gravity/number.krl.txt`; loader
`sLoadNumberFromFile` `JX3RepresentX64.dll 0x180857DF0`; logical name
`CommonNumber` from `Represent/filepath.ini`).

| Key | Value | Unit |
|---|---|---|
| `CameraInitYaw` | 0.5022619 | rad |
| `CameraInitPitch` | -0.17 | rad (-9.7 deg) |
| `CameraInitRoll` | 0 | rad |
| `CameraFollowMode` | 1 | flag |
| `CameraMaxDistance` | 1245 | u (12.45 m) |
| `CameraDragSpeed` | 1 | multiplier |
| `CameraResetSpeed` | 1 | multiplier |
| `CameraPitchSpeed` | 0.001 | rad/ms |
| `CameraSpeedRatio` | 1.5625 | m->尺 (100/64) |
| `CharacterCameraSmoothTime` | 60 | ms (0.06 s) |
| `SprintCameraAngle` | 0.3 | rad |
| `SprintCameraPitch` | -0.35 | rad (-20.05 deg) |
| `SprintCameraMaxDistance` | 60 | u? (see §6) |
| `SprintCameraSmoothTime` | 500 | ms |
| `SprintCameraSpringTime` | 500 | ms |
| `SprintCameraMaxDragSpeed` | 0.0025 | |
| `SprintCameraMinTrackBackSpeed` | 10 | deg/s (in-file comment 角度/秒) |
| `SprintCameraMaxTrackBackSpeed` | 90 | deg/s |
| `SprintCameraTrackBackSpeedSlope` | 1.0 | |
| `SprintCameraOffset` | 40 | u |
| `SprintCameraMaxOffset` | 100 | u |
| `CarrierCameraPitch` | -0.17 | rad |
| `CarrierCameraYaw` | 0 | rad |
| `CarrierCameraMaxDistance` | 1 | u? (see §6) |
| `CarrierCameraDeltaHeight` | 50 | u |
| `NearByWallDistance` | 800 | u (obstruction range) |
| `AirCombatToSkillMoveCameraDis` | 2000.0 | u |
| `DiveCameraRollRange` / `DiveCameraRollTime` | 12 / 400 | deg? / ms |
| `TitleAdjustFovMin` / `Max` / `Value` | 0.7 / 1.4 / 40 | nameplate FOV scale |

Note: the `SprintCameraPitch = -0.35` matches the untouched saved `fPitch`;
`CameraInitPitch = -0.17` matches `scene_init_param.txt` (§4).

---

## 4. Per-map camera init — `scene_init_param.txt`

`proof/gravity/scene_init_param.txt` (`MapID CameraYaw CameraPitch CameraRoll`):

```
0    0.5022619   -0.17  0
1    0.5022619   -0.17  0
653  2.11075783  -0.17  0
```

The saved per-role `fYaw` matches these values (e.g. a role with
`fYaw = 2.1107578277588` = map 653). So a host can initialise the follow
camera from the map's real init yaw/pitch instead of guessing.

---

## 5. Wheel zoom — real behaviour and limits

`ZoomCharacterCamera_Step` (`JX3RepresentX64.dll 0x180b3ce40`):

```
xmm7 = fMaxCameraDistance                      ; assert > 0
step = clamp(current / (0.2 * fMaxCameraDistance) * 120, 10, 120)   ; units
if (flag) step = -step                          ; sign by wheel direction
```

Constants: 0.2 @ `0x180cb9e9c`, 120 @ `0x180cd8ab4`, 10 @ `0x180cad554`,
sign mask @ `0x180cad610`.

Limits: `fMaxCameraDistance` (user setting default 2000; engine cap default
2000; `number.krl CameraMaxDistance = 1245`) and `fMinCameraDistance` (engine
cap, value unknown — see §6).

Pitch limits: hard assert `|pitch| < pi/2` (`0x180cade98`); the smoothing
setter clamps its value to `[0.001 (0x180ca1918), 1.0 (0x180ca1280)]`.

---

## 6. Engine camera caps (per graphics option level)

`Lua3DEngine_Get3DEngineOptionCaps` (`JX3UIX64.dll 0x18000682e`) exposes:

| Cap | vtable slot |
|---|---|
| `fMinCameraDistance` | +0x48 |
| `fMaxCameraDistance` | +0x50 |
| `fMinCameraAngle` | +0x58 |
| `fMaxCameraAngle` | +0x60 |

Only `fMaxCameraDistance = 2000` is confirmed (`JX3RepresentX64.dll` const
blob `0x180d2af90 = 44fa0000`, next to `-1.56298 / 1.0 / 0`; and the settings
default). The other three are compiled per option level.

**How to get them without new data:** query the live caps object from the
engine host — the engine is already loaded there. Path used by `EnterCarrier`
(`JX3RepresentX64.dll`): manager `[[0x180f06a50] + 0xB0]` -> vtable call
`[vt + 0x238]` returns the caps object; the caps' own vtable holds the four
getters at +0x48/+0x50/+0x58/+0x60.

---

## 7. What is still missing, and how to get it

| # | Missing | Why | Path to get it | Breakthrough? |
|---|---|---|---|---|
| 1 | `Represent/camera/config.ini` + `Represent/camera/*.krl.txt` — the per-mode rows (CameraCommon, AirCombatCamera, NpcDialogCamera, CarrierCamera, GliderCamera, DynamicFollowCamera), plus `CameraLockTargetConfig`, `CameraSegmentConfig.json`, `Represent/player/player_rush_camera.txt` | Not in local paks; ship via CDN mini-updates. Aug-build cache has only `skill_move_camera.txt` + `filepath.ini` (`SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\pakv4-probe\`) | (a) CDN mini-update fetch using the game's own downloader config (`bin64\KGPK4_StreamDownloader.zhcn_hd.conf`, `jx3hd_v4_mini` URLs); (b) run the real client once to let it update, then read the local files | **yes** (data access) |
| 2 | Engine `[Camera]` ini: `nChaseType`, `bObstructdAvert`, `fChaseRate`, `fMaxDistance`, `fMinDistance`, `fMax/MinAngelVel`, `fAngelRateHor/Vel`, `fDisZoomRate`, `fFlexCoefficient`, `fDampCoefficient`, `bUseFlexibilitySys`, `fFlexRate`, `fDistance`, `fAngleHor/Vel`, `fLootAtOffsetY`, `bLockedCamera`, **`fFovy`** | Loader `KG3DEngineX64.dll 0x1804C1660` reads an ini that is not in the install | same CDN/client route as #1; or engine-binary defaults (RE) | **yes** |
| 3 | Engine caps `fMinCameraDistance`, `fMinCameraAngle`, `fMaxCameraAngle` | compiled per option level | live caps probe from the host (§6) | no |
| 4 | `.krl` speed unit (u/frame vs 尺/s) — drives the sprint camera trigger and the host player sim | `number.krl` `CharacterWalkSpeed=6`, `Run=20`, `RideRun=40` | measure the walk clip stride (`samples/actor_presets/f1_hualuo/mapviewer_clips_ascii/walk.fbx`, 1.4 s cycle) or read the consumer of the loaded field (`JX3RepresentX64.dll` config struct `+0x4c` walk, `+0x50` run) | no |
| 5 | Units of `SprintCameraMaxDistance=60`, `CarrierCameraMaxDistance=1`, `SprintCameraMaxOffset=100` | ambiguous (u vs offset vs ratio) | read their consumers in the camera controller (`JX3RepresentX64.dll`) | no |
| 6 | Settings -> engine wiring: `nCameraMode` 0/1, `bCurveCamera`, `bEyeFollow`, `fSpringResetSpeed`/`fCameraResetSpeed`, and whether the panel `fMaxCameraDistance` (2000) feeds the engine cap or the follow distance (vs `number.krl` 1245) | UI panel is C++/pak; binding not decoded | RE of the panel binding / engine settings apply path | no (RE work) |
| 7 | 广角 / FOV — no per-user key found | candidates: `fCameraViewAngleFactor = 1.0` (`config.default.ini`), `fFovy` (missing ini #2), `TitleAdjustFov*` (nameplates) | #2 | partial |
| 8 | Real-client validation (camera feel vs actual game) | needs the game running/logged in | optional | optional |

---

## 8. Adopt-now checklist for the host camera

Already applied on `camara-imp` (commit `37bb969`):

- `UnitsPerMeter = 100` (was 192)
- wheel zoom = real step formula, range 100 u .. 2000 u
- default pitch -0.35 rad, free camera removed (follow only)

Still to adopt from this file (values are real; safe ones first):

1. `CharacterCameraSmoothTime = 60 ms` (host currently 0.08 s).
2. Sprint camera: `SprintCameraAngle = 0.3`, `SprintCameraSmoothTime/SpringTime
   = 500 ms`, track-back `10 -> 90 deg/s`, `Slope = 1.0`,
   `SprintCameraMaxDragSpeed = 0.0025`, `Offset = 40 u`, `MaxOffset = 100 u`
   (verify #5 for the distance/offset units).
3. Carrier camera: `Pitch = -0.17`, `Yaw = 0`, `DeltaHeight = 50 u`.
4. Obstruction: `NearByWallDistance = 800 u`.
5. Initialise the camera per map from `scene_init_param.txt`
   (`CameraInitYaw/Pitch`), and keep `-0.35` as the character/sprint pitch.
6. Keep the per-mode rows (`TargetDistance`, `CameraHeight`, `MaxDragSpeed`,
   `RotationSpeed`, move-pitch) marked as **host placeholders** until #1 is
   obtained — they are currently invented in `engine_host_spike/camera.json`.

Units side-effect worth knowing for the camera: the host player sim in
`MapSpike.cs` is 1.92x off because it was derived at 192 u/m (gravity
-1289 u/s^2 and jump 703 u/s vs the table's -2475 u/s^2 and 1350 u/s; walk/run
200/667 u/s vs 6/20 per frame — 90/300 u/s if the `.krl` speeds are u/frame
at 15 fps, which the walk-clip stride suggests, pending #4 above). The sprint
camera trigger (`Shift` + move) is unaffected, but movement/jump scale is —
fix together with the unit correction if the player sim is touched.

---

## 9. Sources

- `userdata/<account>/<region>/<server>/<role>/custom.dat` — per-user settings.
- `proof/gravity/number.krl.txt` — `Represent/common/number.krl.txt`
  (camera block; loader `sLoadNumberFromFile` `JX3RepresentX64.dll 0x180857DF0`).
- `proof/gravity/scene_init_param.txt` — per-map init camera.
- `JX3RepresentX64.dll` — `ZoomCharacterCamera_Step` `0x180b3ce40`, constants
  above; `LoadCarrierParams`/`MovePitch10RowLoader` row loaders;
  `EnterCarrier` caps path `[[0x180f06a50]+0xB0]` -> `[vt+0x238]`.
- `JX3UIX64.dll` — `Lua3DEngine_Get3DEngineOptionCaps` `0x18000682e`.
- `MovieEditor\source\fbx\*\*.fbx` — `UnitScaleFactor = 1.0` (cm).
- `docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md`,
  `proof/netcode/character_size/mesh_census.json` — unit calibration.
- `proof/netcode/camera_number_krl_block.txt` — raw values for this file.
