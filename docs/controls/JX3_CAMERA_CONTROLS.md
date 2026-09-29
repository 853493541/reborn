# JX3 camera controls

**Evidence:** `docs/CAMERA_INPUT_CONTROLS.md`, `docs/CAMERA_REAL_VALUES.md`,
`docs/CAMERA_CONFIG_FILES.md`, `docs/CAMERA_DRAG_MODEL.md`,
`docs/CAMERA_WALL_OBSTRUCTION.md`,
`proof/netcode/camera_settings_real.txt`,
`proof/movement/extracted/ui_hotkey_default.txt`.

---

## 1. Bindings (real defaults)

| Input | Command | Lua | Effect |
|---|---|---|---|
| LMB drag | `CAMERAORSELECTORMOVE` (code 1) | `CameraOrSelectOrMoveStart(0)/Stop(0)` | rotate camera; click without drag selects |
| RMB drag | `CAMERAORSELECTORMOVESTICKY` (2) | `CameraOrSelectOrMoveStart(1)/Stop(1)` | rotate camera **and turn the character** |
| wheel up | `CAMERAZOOMIN` (256) | `CameraZoomIn()` → `Camera_Zoom(0.9)` | `distance × 0.9` |
| wheel down | `CAMERAZOOMOUT` (257) | `CameraZoomOut()` → `Camera_Zoom(1.1)` | `distance × 1.1` |
| F11 | `CAMERARESET` (122) | `CameraReset()` → `Camera_SetForceReset(charYaw, −π/12, 1)` | behind, pitch −15°, distance 1× |
| Home / End | `CAMERA_SET_VIEW_1/2` (36/35) | `CameraSetView(0)/(180)` | yaw preset behind / front |
| `CAMERAUP/DOWN` | unbound | `MoveUpStart/Stop`, `MoveDownStart/Stop` | pitch by key |

Control ids: `CONTROL_CAMERA = 6`, `CONTROL_OBJECT_STICK_CAMERA = 7`
(mode 0 → camera, mode 1 → camera + character).

## 2. Drag pipeline (represent layer, proven)

```
mouse button + delta
  → ComputeMouseMoveDelta
  → ApplyMouse: delta × 2π (const 0x180CB9F34 = 6.2831855)
      X → controller yaw (+0x20), Y → controller pitch (+0x24)
  → ClampMouse: row drag/rotation speeds (+0x20/+0x24) + per-mode variants
      (camera / sprint / carrier / glider / npc dialog camera controller)
  → pitch setter clamps |pitch| < π/2 − 0.0157 (0x180AE29D0)
  → SetCharacterCameraPosition:
       offset = ( A·cos(pitch)·sin(yaw) + B·…,
                  A·sin(pitch) + C,
                  A·cos(pitch)·cos(yaw) ± B·… )
       camera = anchor + smoothed(offset)      // A = distance, C = CameraHeight
```

- Constant-length orbit: **no `tan`**, `C` is a separate additive term.
- Per-axis dead-zone smoothing (`SmoothTime`).
- Cursor lock/hide while dragging (`GetRLCursorPos`/`SetRLCursorPos`).
- Mouse never writes a camera position; it only changes controller yaw/pitch.
- Anchor = character head/socket (`Bip01 Head`), look-at stays on it.

Verified loader defaults: `CameraMaxDeltaYaw = 2π`, `CameraMaxDeltaPitch = 1.56`,
`MaxDragSpeed = RotationSpeed = 0.00314`, `TargetDistance = 1.0`,
`SmoothTime = 1.0`, `InitCameraPitch = π` (sentinel),
`CameraAdjustYawWhenMoveTurnDisableAngle = 0.26 rad (15°)`.

## 3. Operation modes

`operationmodebase.lua`: **CLASSICAL_MODE** (rotate only while holding LMB/RMB)
vs **JOYSTICK_MODE** (mouse always rotates). Toggles `Camera_EnableControl`,
`Scene_LockMouseRotation`, `Camera_UseFullAngle`,
`Camera_SetResetSpeed(3.5, 3.75)`.

## 4. User settings

Per-role `custom.dat` (`VideoSettingPanel.tCameraStatic`):

| Key | Default | Clamp | Applied to |
|---|---|---|---|
| `fMaxCameraDistance` | 2000 u | [1, 2000] | camera node classic `+0x74` / joystick `+0x8C` |
| `fDragSpeed` / `fDragPitchSpeed` | 1 / 1 | [0.01, 10] | `+0x6C/+0x70` |
| `fSpringResetSpeed` | 1 | [0.01, 10] | `+0x78` |
| `fCameraResetSpeed` | 1 | [0.01, 10] | `+0x7C` |
| `nCameraMode` | 0 | [0, 3] | follow mode `+0x80` (int) |
| `bCameraSmoothing` / `bCurveCamera` / `bEyeFollow` | true | bool | engine/runtime |
| `g_Scene_tCameraRuntime.fYaw/fPitch` | map init / −0.35 | — | saved view |
| `fCameraToObjectEyeScale` | 1 (0.9 some roles) | — | distance scale |

Per-install `config.ini` (video panel, incl. **广角**):
`[KG3DENGINE] CammeraAngle = 0.837757` rad ≈ 48°, `CammeraDistance = 80000`
(engine far plane, unrelated), `[UIVideoSetting]` toggles.

Per-map init from `scene_init_param.txt` (`MapID CameraYaw CameraPitch CameraRoll`;
maps 0/1 yaw 0.5022619 pitch −0.17, map 653 yaw 2.11075783).

## 5. Zoom / distance

- Wheel: Lua `Camera_Zoom(0.9 / 1.1)`, clamped `[fMinCameraDistance, fMaxCameraDistance]`.
- Non-wheel path (`ZoomCharacterCamera_Step` `0x180b3ce40`):
  `step = clamp(current / (0.2 · fMaxCameraDistance) · 120, 10, 120)` u.
- Engine caps (per graphics option level): `fMinCameraDistance` `+0x50`,
  `fMaxCameraDistance` `+0x58`, `fMinCameraAngle` `+0x60`, `fMaxCameraAngle`
  `+0x68` of the caps object (`[[manager]+0xB0] → vt+0x238`).
- 镜头最大距离 details and our implementation plan:
  `CAMERA_DISTANCE_FOV_SPEC.md`.
- **广角 slider mapping (resolved 2026-09-25):** the video panel stores
  `VideoSetting_WidAngle`; the applier clamps **30°–60°**, defaults to **50°**
  (`50·π/180`) when unset, adds `caps.fMinCameraAngle` (converted to degrees)
  when the value is below 30, and calls
  `KG3DEngine.Set3DEngineOption({fCameraAngle = value·π/180})`. Panel defaults:
  `MouseSpeed=1.2`, `MaxDistance=2000`. Full detail:
  `controls/RESEARCH_RESOLVED_GAPS.md` §3.

## 6. Wall obstruction (native)

- engine `[Camera] bObstructdAvert` compiled default **on**
- default **5-ray** probe set (center + 4 corners), alternate **9-ray** mode
- nearest hit along anchor→camera; `C' = A + u·clamp(hit, 0.001, …)`
- caller clearance: `C'' = C' + normalize(A − C')·18 u`
- hysteresis 50 u (clear) / 100 u (obstructed)
- flex return: `S += (−1.5·E − 2.828·S)·dt`, `X = desired + S·dt`, accept ≤ 0.05 rad
- per-mesh `bObscatleCamera` gate (default 1; some decorative props 0)
- `NearByWallDistance = 800` is **not** a wall rule (loaded, no reader found)

Our client: **terrain-only** ray-march, no structures — S9 in
`CAMERA_FIX_SUGGESTIONS.md`.

## 7. Other camera systems

- move-reactive: `CameraMovePitchApplyAngle/SmoothTime/AdjustPitch/…`,
  `CameraAdjustYawWhenMoveTurn` + dead zone
- sprint camera: pull-back (`SprintCameraMaxDistance`, track-back 10→90 deg/s,
  smooth/spring 500 ms, offset 40/max 100 u)
- mode cameras: carrier, glider, air-combat, NPC dialog, dynamic follow,
  skill-move per-skill FOV (`skill_move_camera.txt`)
- shake (`CameraShake` table keys), cinematic track (`KRLCameraAni`),
  follow-action look-at (`s_face`)

## 8. Our client status (camara-fix @ 00f1237)

Working: LMB/RMB drag, wheel ×0.9/×1.1, F11, Home/End, cursor lock,
JX3 sphere offset, per-frame aim closed loop (yaw + pitch),
`custom.dat` read (3 keys).
Missing: wall obstruction, 广角/FOV, engine caps probe, joystick mode,
`CAMERAUP/DOWN`, spring/reset speeds, move-reactive pitch/yaw-follow, mode
cameras, shake/cinematic, full `custom.dat` keys, settings write path.
