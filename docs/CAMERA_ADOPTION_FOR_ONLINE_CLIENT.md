# Camera: what the online client (`reborn-online`) should take from the camera work

Analysis date 2026-09-23. Compared against the real values documented in
`docs/CAMERA_REAL_VALUES.md` (same worktree, branch `camara-imp`).

Target code: `C:\Users\Zhibin Ren\Desktop\reborn-online\client\`
(`RebornClient.cs`, `CameraSystem.cs`, `camera.json`).
Reference code: `C:\Users\Zhibin Ren\Desktop\reborn-camera\engine_host_spike\`
(same files, commits `37bb969` + `1d3f7c9` + `5dc8ed5`).

The online client's `CameraSystem.cs` is the **pre-`37bb969` copy** of the
camera model (only the unit change, the real zoom, and the pitch differ), and
its `CameraSmoke.cs` is byte-identical to ours (so the smoke test is safe).

---

## 1. Units — the single most important fix

| Where | Now | Use | Why |
|---|---|---|---|
| `client/CameraSystem.cs:89` | `UnitsPerMeter = 192.0` | **`100.0`** | 1 engine unit = 1 cm (mesh 181.64 u = 1.816 m; official FBX `UnitScaleFactor=1.0` cm; pelvis 98.59 u; ragdoll limbs in cm). `docs/CAMERA_REAL_VALUES.md` §1 |
| `client/RebornClient.cs:698` | `pRun / 192.0` | `pRun / camSys.UnitsPerMeter` | same |
| `client/RebornClient.cs:380` | `pGravity=-1289, pJumpV=703, pSpeed=200, pRun=667` | `-2475, 1350, 96, 320` | table values in world units at **16 game frames/s** (1 frame = 1/16 s): walk/run 6/20 u/frame -> 96/320 u/s (`PLAYER_CONTROLS_FINDINGS.md` §3; the old 15-fps note was wrong); gravity/jump from `JX3_GRAVITY_RESEARCH.md`. Current values are the 192-derived metric numbers, i.e. 1.92x off in the world |

Effect of the unit fix: follow distance 6 m becomes 600 u (was 1152 u),
`CameraHeight` 2 m becomes 200 u (was 384 u) — the current camera sits ~1.92x
too far back.

## 2. Wheel zoom — `Camera_Zoom(0.9 / 1.1)`

**Corrected 2026-09-24:** the wheel binding is `CAMERAZOOMIN/OUT` ->
`CameraZoomIn/Out()` -> `Camera_Zoom(0.9 / 1.1)` (`CAMERA_INPUT_CONTROLS.md`
§2): multiply the target distance by 0.9 / 1.1 and clamp to
`[fMinCameraDistance, fMaxCameraDistance]` (`CAMERA_REAL_VALUES.md` §5).
`ZoomCharacterCamera_Step` (`0x180b3ce40`) is **not** the wheel path - it is a
separate step-zoom used by other camera code. Our `CameraSystem.ZoomBy` serves
the wheel with the proven factors (row keys `MaxCameraDistance=2000`,
`MinCameraDistance=100`); the Step formula is unused by the wheel.

## 3. Camera placement — the sphere offset (supersedes the fixed-camY note)

**Corrected 2026-09-24:** the real camera is the constant-length sphere
offset, `camera = anchor + (cos(yaw)cos(pitch)d, sin(pitch)d + height,
sin(yaw)cos(pitch)d)`, smoothed per axis (`CAMERA_FIX_SPEC.md`,
`CAMERA_DRAG_MODEL.md`). The earlier "horizontal direction + fixed
`camY = anchor.y + CameraHeight`, no pitch" note is wrong and would freeze the
pitch geometry (drag up/down must orbit the camera vertically). Keep the
proven offset plus the native obstruction rule.

## 4. Real defaults to adopt (from `number.krl.txt` + `custom.dat`)

| Key | Real value | Their value |
|---|---|---|
| `CharacterCameraSmoothTime` | **60 ms (0.06 s)** | row `SmoothTime` 0.08 |
| `CameraInitPitch` (map/scene init) | **-0.17 rad** | `-20 deg` rows + `RC_CAM_PITCH_FIX` synthetic pitch |
| default / sprint pitch | **-0.35 rad** (saved per-role default; `SprintCameraPitch`) | `-20 deg` |
| `SprintCameraSmoothTime` / `SpringTime` | **500 ms / 500 ms** | 0.3 s / - |
| `SprintCameraAngle` | 0.3 | - |
| sprint track-back | **10 -> 90 deg/s, slope 1.0** | `MinTrackBackSpeed 4 / Max 14 / Slope 0.5` |
| `SprintCameraMaxDragSpeed` | 0.0025 | - |
| `SprintCameraOffset` / `MaxOffset` | 40 u / 100 u | - |
| `SprintCameraMaxDistance` | 60 (unit TBC — **not** an absolute target) | 9 m as absolute target |
| `CarrierCameraPitch/Yaw/DeltaHeight` | -0.17 / 0 / 50 u | -0.35 / 0 / 1 m |
| `NearByWallDistance` | 800 u **loaded, no reader** — NOT an obstruction trigger | terrain ray-march; wall rule is 18 u clearance + 50/100 u hysteresis (`CAMERA_WALL_OBSTRUCTION.md`) |
| per-map init | `scene_init_param.txt`: maps 0/1 yaw 0.5022619 pitch -0.17; map 653 yaw 2.11075783 | measured at startup |

## 5. Read the user's real camera settings at startup

`client/RebornClient.cs` currently hard-codes its rows. The real per-user
settings are readable text:

`C:\SeasunGame\Game\JX3\bin\zhcn_hd\userdata\<account>\<region>\<server>\<role>\custom.dat`

- `VideoSettingPanel.tCameraStatic` -> `fMaxCameraDistance` (default 2000),
  `fDragSpeed` / `fDragPitchSpeed` (1), `fSpringResetSpeed` /
  `fCameraResetSpeed` (1), `nCameraMode` (0);
- `g_Scene_tCameraRuntime` -> saved `fYaw`, `fPitch` (-0.35 default),
  `fCameraToObjectEyeScale` (1) for the initial camera state.

Wiring the panel values into the model makes the client match the user's own
camera settings instead of fixed constants.

## 6. Stop guessing the engine caps — probe them live

`fMinCameraDistance`, `fMinCameraAngle`, `fMaxCameraAngle` are compiled per
graphics option level, but the host loads the engine, so they can be read at
runtime:

```
JX3RepresentX64.dll EnterCarrier path:
  manager [[0x180f06a50] + 0xB0] -> vtable [vt + 0x238]  => caps object
  caps vtable +0x50 / +0x58 / +0x60 / +0x68
    = fMinCameraDistance / fMaxCameraDistance / fMinCameraAngle / fMaxCameraAngle
  (corrected 2026-09-24; the earlier +0x48..+0x60 list was one slot early)
```

## 7. Still missing (do not invent)

Per-mode rows (`Represent/camera/config.ini` + `*.krl.txt`) and the engine
`[Camera]` ini (`fChaseRate`, `fFovy`, `fMaxDistance`, ...) are not in the
local paks; they need the CDN mini-update / one client update run. Until
then, the per-mode `TargetDistance` / `CameraHeight` / move-pitch / drag
speeds stay host placeholders. Details: `docs/CAMERA_REAL_VALUES.md` §7.

## 8. Fastest port

1. Copy `engine_host_spike/CameraSystem.cs` + `camera.json` from
   `reborn-camera` (branch `camara-imp`) over `client/` (keeps
   `CameraSmoke.cs` unchanged; run `camera_smoke.exe` -> 13/13).
2. Apply `RebornClient.cs` changes: `pRun / camSys.UnitsPerMeter` (line 698),
   the placement fix (§3), the wheel call (§2), the sim constants (§1).
3. Optional: settings loader (§5) and caps probe (§6).
