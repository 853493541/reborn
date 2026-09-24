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
| `client/RebornClient.cs:380` | `pGravity=-1289, pJumpV=703, pSpeed=200, pRun=667` | `-2475, 1350, 90, 300` | the table values in world units: gravity 11 u/frame^2 and jump 90 u/frame at 15 fps; walk/run 6/20 u/frame (pending the `.krl` speed-unit check). Current values are the 192-derived metric numbers, i.e. 1.92x off in the world |

Effect of the unit fix: follow distance 6 m becomes 600 u (was 1152 u),
`CameraHeight` 2 m becomes 200 u (was 384 u) — the current camera sits ~1.92x
too far back.

## 2. Wheel zoom — replace the invented step

`client/RebornClient.cs:341-349` does `TargetDistance ± 0.5 m`, clamped
`2..30 m`. The real client (`ZoomCharacterCamera_Step`,
`JX3RepresentX64.dll 0x180b3ce40`):

```
step = clamp(current / (0.2 * fMaxCameraDistance) * 120, 10, 120)   ; units
```

Limits: `fMaxCameraDistance` = **2000 u (20 m)** (real per-user default +
DLL const blob `0x180d2af90`), `fMinCameraDistance` = engine cap (unknown;
placeholder 100 u). Our `CameraSystem.ZoomStep` / `ZoomBy` implement exactly
this — copy them (plus the two row keys `MaxCameraDistance=2000`,
`MinCameraDistance=100`) and call `camSys.ZoomBy(e.Delta > 0 ? -1 : +1)`.

## 3. Camera placement — they still have the idle-jitter version

`client/RebornClient.cs:700-709` places the camera on the **full 3D** measured
view direction (`viewY` included). The nudge probe's vertical component
jitters ~5x/s, which is the idle shake fixed in `1d3f7c9`. Use the fixed
version:

- only the **horizontal** part of the measured direction for the offset,
- `camY = anchor.y + CameraHeight * UnitsPerMeter` (row height),
- keep the terrain ray-march + `terrain+30 u` clamp.

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
| `NearByWallDistance` | **800 u** (obstruction trigger) | ray-march only |
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
  caps vtable +0x48 / +0x50 / +0x58 / +0x60
    = fMinCameraDistance / fMaxCameraDistance / fMinCameraAngle / fMaxCameraAngle
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
