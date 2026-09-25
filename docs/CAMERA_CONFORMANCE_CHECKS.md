# Camera conformance checks — notes vs code

**Code audited:** `camara-fix` @ `00f1237` (per-frame aim sync) · **Date:** 2026-09-25
**Purpose:** turn every camera note into a check with a status, a code
reference and an acceptance test. Companion to `CAMERA_FIX_SUGGESTIONS.md`
(what to fix) and `controls/CONTROLS_GAP_REGISTER.md` (live register).

Status: `PASS` · `PARTIAL` · `FAIL` · `N/A`

---

## A. Drag / aim (the proven model)

| # | Requirement | Status | Code / evidence | Acceptance |
|---|---|---|---|---|
| A1 | Camera position is the constant-length JX3 sphere offset; height is an additive term; no `tan` | **PASS** | `CameraSystem.DesiredOffset`; placement `RebornClient.cs:1053`; `docs/CAMERA_DRAG_MODEL.md` | `camera_smoke.exe` radius-invariant case |
| A2 | Mouse X → yaw, Y → pitch (`pitch += dy`), engine orbit as actuator | **PASS** | `RebornClient.cs:728`, `:735`; proven in `docs/CAMERA_DRAG_MODEL.md` | drag probe: model and measured yaw track |
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
| S8 | 广角/FOV + engine caps | **FAIL** | only `RC_VIEW_ANGLE`; `MinCameraDistance=100` guess | see `CAMERA_DISTANCE_FOV_SPEC.md` |
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

## Change log

| Date | Change |
|---|---|
| 2026-09-24 | `a8471b4` sphere offset + drag model; `733b131` corrections spread; `436345e` gentler timing; `00f1237` per-frame sync (A8 PASS) |
| 2026-09-25 | this audit: S1–S9 open as above |
