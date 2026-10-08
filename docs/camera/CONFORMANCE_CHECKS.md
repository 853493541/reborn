# Camera conformance checks — notes vs code

**Code audited:** `camara-fix` @ `00f1237` (per-frame aim sync) · **Date:** 2026-09-25
**Purpose:** turn every camera note into a check with a status, a code
reference and an acceptance test. Companion to `docs/camera/FIX_SUGGESTIONS.md`
(what to fix) and `controls/CONTROLS_GAP_REGISTER.md` (live register).

Status: `PASS` · `PARTIAL` · `FAIL` · `N/A`

---

## 2026-09-30 re-audit — status summary

Code: `main` after the camera merges (`d450ba6` camera-wall-clip, `18885a1` cam-wwdrag).
The tables below are the 2026-09-25 baseline; this section records what changed and the
current counts. Live per-item detail: `HOST_DEVIATIONS.md`.

**Landed since the baseline (now implemented):**

- Drag/aim A2–A9: per-frame sync, engine look-at (`D3` closed), `postdbg moved=0.0`.
- S1–S6/S8/S9: zoom/sprint re-pin, `EyeScale` in the aim math, max clamp on every
  writer, rate-limited RMB turn (S6), 广角 read (custom.dat `WidAngle` else config.ini,
  panel 30–60), full obstruction (bake + engine terrain/scene rays + 18 u clearance +
  50/100 hysteresis + flex return).
- E3 (rate-limited turn), E12 (CLASSICAL/JOYSTICK operation modes, F7 + gating + smoke),
  E19 (wall/structure obstruction with the per-mesh `bObscatleCamera` gate from extracted
  content), E20/E24 (move-pitch + shake models exist; opt-in / not triggered).
- B6/D3 closed: engine position setter `cam->vt[+0x50](pos,0)` + look-at
  `cam->vt[+0x58](anchor,0)`, absolute Y; B14 double-sided probes; B15 `rSm` uses the
  character-row `SmoothTime` (60 ms) in every mode.
- D6 root cause (lazy `RCPI_Scene` hash) + `RC_D6Seed` fix default-on; VEH diagnostic,
  minidump reader, input driver.
- custom.dat reader: 13 keys (yaw/pitch/max/drag×2/EyeScale/WidAngle/spring/reset/curve/
  follow/smoothing/eye-follow) + saved-runtime view preference (E6).

**Counts** (over the E1–E29 list plus the A/S/C groups):

| Group | Implemented | Partial | Missing |
|---|---|---|---|
| Core follow camera (orbit, drag, smoothing, clamps, obstruction, look-at, engine set) | 12 | 2 | 0 |
| Data-fidelity parameters (anchor, footprint, caps, mode rows, FOV default, near plane, flex, per-mode caps, initial distance) | 3 | 6 | 5 |
| Extra camera families & controls (E1–E29) | 5 | 3 | 21 |

**Quality assessment:**

- **Core follow camera: engine-faithful.** Position + look-at go through the engine's own
  camera object; the model comes from recovered IL/data; invariants verified (smoke ALL
  PASS; T2 `206/188`; T4 `186/168`; T1 sweep 0 flips; route aim 125/125; sprint drag
  constant-orbit 1.15%).
- **Data layer: the weak part.** C1 anchor (chest+90 vs head/socket), C2 footprint
  (invented 22 u), C3 min distance (100 u guess), C4 per-mode rows (placeholders), C6 FOV
  default (60 = panel max, game panel default 50), C7 near plane (unread), C8 flex
  hardcoded, C9 per-mode caps not re-clamped on switch, C10 initial distance unproven
  (start-at-max is a user decision).
- **Feature breadth: low-medium.** The extra camera families (carrier/glider/air-combat/
  NPC-dialog/dynamic-follow), skill-move camera, cinematic/track camera, spectator/god,
  follow-action look-at, free view, shake triggers are not implemented (most need
  data/triggers that are not in the host yet).
- **Registered host deviations still shipped:** B1 (park-below hide), B8 (crossing guard),
  B9 (degenerate-hit guard), B12 (scene near floor), B13 (flag-0 structures block), B15
  (host fix), plus user-decision bindings A12 (zoom on `+/-`) and C10 (start at max).
  B5/B7/B10 and the A1–A9 aim emulation are obsolete/default-off deletion candidates.

---

## A. Drag / aim (the proven model)

| # | Requirement | Status | Code / evidence | Acceptance |
|---|---|---|---|---|
| A1 | Camera position is the constant-length JX3 sphere offset; height is an additive term; no `tan` | **PASS** | `CameraSystem.DesiredOffset`; placement `RebornClient.cs:1053`; `docs/camera/DRAG_MODEL.md` | `camera_smoke.exe` radius-invariant case |
| A2 | Mouse X → yaw, Y → pitch (`pitch += dy`), engine orbit as actuator | **PASS** | `RebornClient.cs:728`, `:735`; proven in `docs/camera/DRAG_MODEL.md` | drag probe: model and measured yaw track |
| A3 | Per-frame aim sync while dragging (no interval lag) | **PASS** | `if (dragging \|\| orbitApplied)` `RebornClient.cs:675` (commit `00f1237`) | fast 1.5 rad/s sweep: yaw err ≤ 0.002 rad (commit probe) |
| A4 | Yaw correction smoothed, not snapped | **PASS** | τ=10 ms low-pass `:699` | no visible snap; yaw err ≤ 0.002 rad |
| A5 | Pitch aim closed loop (residual to the engine) | **PASS** | `pitchAimErrPx` `:691`, applied `:745-752` (±400 px clamp `:753`) | pitch aim ≤ 0.013 rad |
| A6 | Engine does not override the placed camera | **PASS** | run log `postdbg intended==actual moved=0.0` | postdbg moved = 0 |
| A7 | Slow drags stay centred | **PASS** | run captures `rc_00..rc_02` identical, centred | visual |
| A8 | Fast drags stay centred | **PASS** (post-`00f1237`) | per-frame sync removes the interval lag | user confirmed; probe ≤0.002 rad |
| A9 | Drag does not change distance | **PASS** | log invariant `dist=600` constant | zoom/drag invariant |
| A10 | Pitch clamp matches JX3 `π/2 − 0.0157` | **PARTIAL** | our clamp `π/2 − 0.05` `:736-738` | comparison test |

## B. Remaining defects (S-list)

| # | Item | Status | Detail | Acceptance |
|---|---|---|---|---|
| S1 | Aim re-pin after zoom/sprint | **FAIL** | sync only while dragging/after orbit; idle block refreshes only `yawCorr`, not `pitchAimErrPx` | wheel 10 steps + 5 s sprint with no mouse → residual < 0.01 rad |
| S2 | `EyeScale` in the aim math | **FAIL** | placement ×`EyeScale` (`:1043`), helpers use `camSys.Distance` | smoke: `aimPitchOf` with `EyeScale=0.9` equals the placed offset geometry |
| S3 | Ground-clamp guard | **FAIL** | `camY = camGround` `:1080` moves the camera off the ray; residual still unclamped geometry | against a slope: stable, no hunting, ≤0.03 rad off-centre |
| S4 | One pitch controller + deadband | **PARTIAL** | residual fractional + τ=10 ms; feed-forward separate; no deadband; ±400 px clamp | no ringing; residual ≤2 px at rest |
| S5 | Max-distance clamp on every writer | **FAIL** | only `ZoomBy` clamps; F11 `:558` and sprint `CameraSystem.cs:450` bypass | user max 760: zoom-out stops, F11/sprint obey |
| S6 | RMB turn via turn model | **FAIL** | snap `:929` | smooth rate-limited turn, no snap |
| S7 | LMB click vs drag | **FAIL** | press always locks `:483-491` | click selects without capture; drag rotates |
| S8 | 广角/FOV + engine caps | **FAIL** | only `RC_VIEW_ANGLE`; `MinCameraDistance=100` guess | see `docs/camera/DISTANCE_FOV_SPEC.md` |
| S9 | Wall/structure obstruction | **FAIL** | terrain-only `:1058-1080`; `FoliageCollision` unused for camera | camera stops at hit−18 u, hysteresis 50/100 u, flex return |

## C. Settings / distance / FOV

| # | Requirement | Status | Detail |
|---|---|---|---|
| C1 | Read `custom.dat` camera values | **PARTIAL** | 3 of 8 keys (`fMaxCameraDistance`, `fDragSpeed`, `fDragPitchSpeed`) |
| C2 | Read all `tCameraStatic` + saved runtime view | **FAIL** | spring/reset/follow mode not read |
| C3 | Apply clamps [1,2000] / [0.01,10] / [0,3] | **PARTIAL** | distance clamp only in `ZoomBy` |
| C4 | Read `config.ini` 广角 (`CammeraAngle=0.837757`) | **FAIL** | `config.ini` never opened |
| C5 | Engine caps probe (`+0x50/+0x58/+0x60/+0x68`) | **FAIL** | not probed |
| C6 | Read-only persistence (per-role vs per-install scopes) | **FAIL** | not wired (decision: read-only) |

## D. Verification hygiene

| # | Requirement | Status |
|---|---|---|
| V1 | Per-run log files | **FAIL** (log overwritten in testing) |
| V2 | Smoke cases for S1/S2/S5/S9 pure math | **FAIL** (16 checks exist) |
| V3 | Wall test run near known collision | **FAIL** |
| V4 | camdbg includes `effDist`, residual, `clamped` | **FAIL** |

---

## How to run the checks

```powershell
# pure-model smoke (no engine)
C:\SeasunGame\MovieEditor\bin64\camera_smoke.exe

# instrumented run (camera telemetry every 500 ms)
set RC_CAM_DEBUG=1
reborn_client.exe

# use the real per-role camera settings
set RC_LOAD_CUSTOM_DAT=1
```

Regression captures: `bin64\reborn_out\rc_00..rc_03_*.png`. Add a scripted
zoom/sprint variant so S1 is covered by a run, not a manual test.

## E. Camera controls missing in our client (complete list)

Input commands (real → ours):
| # | Real control | Ours | Note |
|---|---|---|---|
| E1 | `CAMERAUP` / `CAMERADOWN` (`MoveUpStart/Stop`, `MoveDownStart/Stop`) | missing | commands exist, unbound by default |
| E2 | LMB click-without-drag = select (camera-or-selector mode 0) | missing | S7; drag works, click does nothing |
| E3 | RMB character turn via the turn-rate model | snap | S6; real turns at `CharacterYawTurnSpeed` with turn range |
| E4 | Non-wheel zoom path (`ZoomCharacterCamera_Step`, `clamp(current/(0.2·max)·120,10,120)`) | missing | we only use Lua ×0.9/×1.1 |
| E5 | `FlipCameraYaw` | missing | flips camera yaw (hotkeys.lua) |
| E6 | `ResetView` / spring reset speed (`fSpringResetSpeed`) | missing | F11 is fixed-behaviour |
| E7 | `CameraReset` reset speed (`fCameraResetSpeed`) | missing | user setting not applied |
| E8 | `EnableCameraZoom` toggle | missing | gates wheel zoom at runtime |
| E9 | `Camera_IsInFreeView` + free-view strafe/turn behaviour | missing | classic-mode strafe uses `TurnLeftStart/Stop` |
| E10 | Row drag speeds `MaxDragSpeed` / `RotationSpeed` (0.00314) | not applied | engine orbit owns response |
| E11 | Per-frame drag clamps `CameraMaxDeltaYaw/Pitch` (radians) | pixel approximation | 2π/1.56 rows effectively non-binding |
| E12 | Classic vs joystick camera mode (`Scene_LockMouseRotation`, `Camera_EnableControl`, `Camera_UseFullAngle`, `SetCameraResetSpeed(3.5/3.75)`) | missing | S8/C12 |

Settings / engine options (real → ours):
| # | Real control | Ours | Note |
|---|---|---|---|
| E13 | 广角 `WidAngle` slider (30–60°, default 50°) | missing | engine option `fCameraAngle`; S8 |
| E14 | Video engine options `MouseSpeed` (1.2), `MaxDistance` (2000) | missing | `Set3DEngineOption` not called |
| E15 | Spring/reset/follow-mode settings (`fSpringResetSpeed`, `fCameraResetSpeed`, `nCameraMode`) | read-not-applied | only 3 custom.dat keys read |
| E16 | `bCameraSmoothing`, `bCurveCamera`, `bEyeFollow`, `bCloseGJInJump`, `bWinkAnimation` | missing | video-panel toggles incl. `rlcmd` calls |
| E17 | Engine caps (`fMinCameraDistance`, `fMinCameraAngle`, `fMaxCameraAngle`) | unprobed | min distance still the 100 u guess |
| E18 | Saved runtime view write-back (`fYaw`, `fPitch`, `fCameraToObjectEyeScale`) | read-only | write-back is a design decision (currently: no) |

Behaviour / systems:
| # | Real control | Ours | Note |
|---|---|---|---|
| E19 | Wall/structure obstruction (5/9 probes, 18 u, 50/100 u hysteresis, flex) | terrain-only | S9 |
| E20 | Movement-reactive pitch (`CameraMovePitch*`) and yaw-follow (15° dead zone) | model exists, not called | S3/S4 family |
| E21 | Sprint camera offset/spring/track-back/pitch/angle | distance pull-back only | `Offset`, `SpringTime`, 10→90 deg/s unused |
| E22 | Carrier / glider / air-combat / NPC-dialog / dynamic-follow cameras | missing | mode rows exist in `camera.json` |
| E23 | Skill-move camera (per-skill FOV/edge FX, `skill_move_camera.txt`) | missing | table already extracted |
| E24 | Camera shake triggers (skill/impact) | model exists, not triggered | `CameraShake` table |
| E25 | Cinematic/track camera (`KRLCameraAni`, `.mani`) | missing | — |
| E26 | Follow-action look-at (`s_face`, lock target) | missing | camera looks at face target |
| E27 | Spectator/god cameras (`CameraCommon`) | missing | watch/OB modes |
| E28 | Aim hardening: re-pin after zoom/sprint, `EyeScale` in aim math, ground-clamp guard, unified pitch controller, distance cap | missing/partial | S1–S5 |
| E29 | Head/socket anchor (real: `Bip01 Head`) | chest + 90 u | affects framing/obstruction origin |

## Change log

| Date | Change |
|---|---|
| 2026-09-24 | `a8471b4` sphere offset + drag model; `733b131` corrections spread; `436345e` gentler timing; `00f1237` per-frame sync (A8 PASS) |
| 2026-09-25 | this audit: S1–S9 open as above |
| 2026-09-25 | section E added: complete missing-camera-controls list (E1–E29) |
