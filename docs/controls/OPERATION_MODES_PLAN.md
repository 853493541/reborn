# Operation modes (CLASSICAL / JOYSTICK) — implementation plan

**Goal:** implement both JX3 operation modes in the client with a runtime
switch key, faithful to the recovered game behavior. Design + phases only;
each phase lands code + a verify command.

**Evidence:** `controls/JX3_CAMERA_CONTROLS.md` §3 (operationmodebase.lua),
`controls/RESEARCH_RESOLVED_GAPS.md` §4-5 (decoded `UISetting_Operation_Switch.lua`
and `hotkeys.lua`), `proof/controls/ui_lua/OperationSwitch.joystick.txt`,
`proof/movement/extracted/hotkeys_script.dump.txt` (CLASSICAL/JOYSTICK globals),
`docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md` (turn model).

## 1. Game truth

| Aspect | CLASSICAL_MODE | JOYSTICK_MODE |
|---|---|---|
| Camera rotation | only while holding LMB/RMB | **mouse always rotates** (`Scene_LockMouseRotation`) |
| LMB drag | camera only | camera (+ selection semantics) |
| RMB drag | camera + character turn | camera (character faces movement) |
| Movement keys | camera-relative WASD; A/D strafe; in free view A/D = turn-in-place (`TurnLeftStart/TurnRightStart`) | camera-relative WASD; character turns to face the movement heading |
| Control API | `Camera_EnableControl(flag, …)` | `Scene_EnableFreeMoveControl(flag, …)` |
| Reset speeds | `Camera_SetResetSpeed(3.5, 3.75)` (classic, joystick — order per call, [MED]) | same call |
| Free view | exists (`Camera_IsInFreeView`) | n/a |
| Persisted follow-mode | `UISetting_Comprehensive.nCameraModeInClassicMode` | `nCameraModeInJoystickMode` |

Switching in the real client is **UI-only** (`UISetting_Operation_Switch.lua`
proto 0/14 calls `SetOperationMode('CLASSICAL_MODE'|'JOYSTICK_MODE')` then
`SetCameraMode(<per-mode nCameraMode>)`); the default hotkey table has **no**
operation-mode command, so a host switch key is our addition (mark host-H).

## 2. Current client state (`client/RebornClient.cs`)

- LMB = camera drag, RMB = camera + character turn (`curYaw`), wheel zoom,
  F11 reset, Home/End presets: classic-like only (`:778-850`).
- **Partial joystick behavior already exists but is keyed off the wrong
  setting**: `cameraSettings.CameraMode == 1` (that is `tCameraStatic.nCameraMode`,
  the *follow mode* `+0x80`, not the operation mode) enables always-rotate and
  keeps the cursor locked (`:806-823`). Must be refactored onto an explicit
  operation-mode state.
- Per-mode camera settings (`nCameraModeInClassicMode/JoystickMode`) are parsed
  nowhere; `CameraSettings.Load` reads `nCameraMode` only.
- No mode HUD/telemetry; no switch key.

## 3. Design

**State:** `CameraSettings.OperationMode` (`0=classical, 1=joystick`) +
`OperationModeSource`. Default classical to preserve current behavior.
Persistence: the real custom.dat key for the mode itself is **unrecovered**
(open item) — session-only until found; do not invent a write path (read-only
policy).

**Switch key:** `F7` toggles; log `opmode=` in the per-frame telemetry and the
build fingerprint. `RC_MODE=classic|joystick` sets it at startup for scripted
tests. Document as host key (real client switches via Its UI panel).

**Input routing matrix (implementation):**

| Input | Classical (keep, `:790-837`) | Joystick (new/shared) |
|---|---|---|
| mouse move, no button | ignored | rotates camera (always), cursor locked |
| LMB drag | orbit camera | orbit camera |
| RMB drag | orbit + body yaw (`curYaw`) | orbit camera only (body follows movement) |
| A/D | strafe (camera-relative) | strafe (camera-relative) |
| W/S | forward/back (camera-relative) | forward/back + body yaw = heading |
| movement step | unchanged | add turn model: heading vs facing, >112.5° halves speed + turn step (`JX3_MOVEMENT_CONTROLS.md` §2) |
| F11 / Home / End | behind/facing presets (current) | behind = camera yaw; keep camera yaw on presets |
| escape | unlock | unlock (cursor re-locks on next move) |

**Per-mode params:** apply `nCameraModeInClassicMode`/`InJoystickMode` to the
active follow mode on switch; use `SpringResetSpeed`/`CameraResetSpeed` for the
reset path (values already read). `Camera_UseFullAngle` semantics remain
**BLOCKED** (no consumer recovered) — do not approximate.

## 4. Phases

**P0 — state + switch + telemetry (no behavior change).**
`OperationMode` field, `F7` + `RC_MODE`, HUD label, log field. Verify:
`camera_smoke` unchanged; a run logs `opmode=classical`; F7 flips it.
Files: `client/RebornClient.cs`, `client/CameraSettings.cs`.

**P1 — joystick camera rotation.**
Move the `CameraMode == 1` always-rotate branch onto `OperationMode`, so the
follow-mode setting no longer doubles as the operation mode. Verify: joystick
run rotates on mouse move without buttons (`camdbg` yaw tracks the deltas);
classical unchanged; cursor lock/unlock as before.

**P2 — joystick movement/facing.**
Body yaw follows the movement heading with the recovered turn model; disable
the RMB body-turn in joystick. Verify: scripted `RC_DEMO` + `RC_MODE=joystick`:
character faces heading, camera stays where the mouse put it; classic run
unchanged (numeric yaw/pos fingerprints).

**P3 — per-mode settings.**
Parse `nCameraModeInClassicMode`/`InJoystickMode` from custom.dat and apply on
switch; map `Camera_SetResetSpeed(3.5/3.75)` to the reset path; log the applied
row. Verify: role with non-zero per-mode values switches follow mode on F7.

**P4 — real switch UI (later).**
Mirror the `UISetting_Operation_Switch` panel (per-mode setting + switch);
persist only when the mode's custom.dat key is recovered.

## 5. Testing strategy

- Pure smoke: a small `OperationMode` gating model (which inputs are active per
  mode) + reset-speed mapping; no engine.
- Scripted: `RC_MODE=classic|joystick` with `RC_CAM_DEBUG=1`; assert
  `opmode=`, yaw/pitch deltas, cursor-lock events, body-facing samples; numeric
  fingerprints, not screenshots-as-proof.
- Manual checklist per mode (documented in this file's phase PR notes).

## 6. Open / risks

1. Persisted operation-mode key (custom.dat) unrecovered — session-only until
   found; no invented key.
2. `Camera_UseFullAngle` consumer unknown (BLOCKED).
3. Free-view strafe→turn-in-place needs a `Camera_IsInFreeView` equivalent;
   undefined in the host (open).
4. `nCameraMode` [0..3] follow-mode semantics per mode not fully decoded.
5. `Camera_SetResetSpeed(3.5, 3.75)` argument order per mode is [MED].
