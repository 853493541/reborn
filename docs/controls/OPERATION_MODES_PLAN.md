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
| RMB drag | camera orbit: `Camera_BeginDrag(2.0)` + `CONTROL_OBJECT_STICK_CAMERA` (script-verified; **no script-level body turn**) | camera (character faces movement) |
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

## 5b. Status (2026-09-29, `agent/camera-wall-clip`)

- **P0 done** — `CameraOperationMode` (`CameraSystem.cs`), `CameraSettings.OperationMode`
  + `RC_MODE` + per-mode fields, `F7` toggle, `op=` in `camdbg`, `OPMODE=` in the
  build fingerprint.
- **P1 done** — the always-rotate/cursor-lock branch moved off the follow-mode
  setting (`RebornClient.cs`); joystick keeps the cursor locked, classical
  unlocks on release.
- **P2 partial** — body already faces the movement heading; RMB body-turn is
  disabled in joystick. The recovered turn-rate model (`>112.5°` penalty) **landed
  2026-09-29** (S6, `CONTROLS_GAP_REGISTER.md`); the remaining P2 gap is the
  mode-specific movement routing (classical strafe/free-view vs joystick
  turn-to-heading), tracked as M3 in §7.
- **P3 partial** — `nCameraModeInClassicMode`/`InJoystickMode` parsed + logged;
  applying them needs the follow-mode [0..3] semantics (`+0x80`) that are not
  decoded yet. Reset-speed application still open.
- **P4 not started** (UI panel).
- Verified: `camera_smoke` ALL PASS (29 checks incl. mode gating + parse);
  `RC_MODE=joystick` run `reborn_20260929_175413.log` (`op=joystick`, DONE,
  exit 0); test client `reborn_client_campen_v3.exe` (merged tree + ports)
  runs with `OPMODE=classical`, F7 live.

## 6. Open / risks

1. Persisted operation-mode key (custom.dat) unrecovered — session-only until
   found; no invented key.
2. `Camera_UseFullAngle` consumer unknown (BLOCKED).
3. Free-view strafe→turn-in-place needs a `Camera_IsInFreeView` equivalent;
   undefined in the host (open).
4. `nCameraMode` [0..3] follow-mode semantics per mode not fully decoded.
5. `Camera_SetResetSpeed(3.5, 3.75)` argument order per mode is [MED].

## 7. Implementation plan — both modes end-to-end (2026-09-30, `agent/move-controls`)

**Subject:** make CLASSICAL (normal) and JOYSTICK both fully playable and
selectable at runtime, faithful to the decoded behavior. Build for mode runs:
`reborn_client_control_modes.exe` (title `sandbox-control_modes`) in the same
worktree as the C1/C2 input core this routes through.

**What the player gets**

- Normal (CLASSICAL): hold LMB/RMB to look; RMB also turns the character; A/D
  side-step (strafe), S back-pedal while facing stays camera-forward; free view
  turns A/D into turn-in-place; cursor stays free.
- Joystick: mouse always looks (cursor locked); W/S/A/D all turn the character
  to the travel heading; RMB rotates the camera only; camera follow per the
  role's `nCameraModeInJoystickMode`.

**Already landed**

- P0/P1 (§5b) plus, since then (`agent/move-controls`): real hotkey table
  (C1/C2), TURNLEFT/TURNRIGHT turn-in-place, autorun, jump takeoff XY, landing
  branch — all of which the mode routing below builds on.

**Remaining work (phases, each = code + verify command)**

- **M0 mode routing audit + telemetry** (small). HUD shows the mode name (log
  `op=` exists); confirm the matrix in §3 against live code. Verify:
  `camera_smoke` ALL PASS, run logs `op=classical|joystick`.
- **M1 per-mode follow mode (P3)**. Research `SetCameraMode(nCameraModeInXMode)`
  -> `tCameraStatic.nCameraMode` (`+0x80`) `[0..3]` consumers in Represent and
  decode each value; then apply the role's per-mode value on switch and log the
  applied row. If `[0..3]` stays undecoded, keep the current follow behavior and
  mark open (no invented modes).
- **M2 per-mode reset speeds (P3)**. `Camera_SetResetSpeed(3.5, 3.75)` (order
  [MED]) maps to the drag-release spring/return rate; `fSpringResetSpeed` /
  `fCameraResetSpeed` are loaded but unused. Decode which arg belongs to which
  mode first, then wire the release-reset path. Verify: scripted drag-release
  reset-rate change per mode.
- **M3 mode-specific movement routing** (the meat). The engine globals
  `ResponseWASDKey`, `Camera_IsInFreeView`, `Camera_EnableControl`,
  `Scene_EnableFreeMoveControl` are **not yet located in our string dumps** —
  first scan the game-client binaries with `tools/` scanners for these names and
  decode:
  - how `ResponseWASDKey('StrafeLeft/Right'/'Forward')` behaves per
    `GetOperationMode()` (classical side-step vs joystick turn-to-heading);
  - what sets `Camera_IsInFreeView` (classic free view -> A/D call
    `TurnLeftStart/Right`);
  - what `Camera_EnableControl` vs `Scene_EnableFreeMoveControl` toggle.
  Implement exactly what is decoded: classical strafe keeps the camera-forward
  facing (strafe clip 6 / back-pedal 57-58 selection by travel angle vs facing);
  joystick keeps the current turn-to-heading turn model; free view A/D turn in
  place. Verify: scripted runs per mode, numeric fingerprints (facing yaw vs
  travel heading, clip names, turn-key deltas), classical vs joystick compared.
- **M4 switch UX + persistence**. F7 host key + `RC_MODE` already exist; HUD
  label via M0. Persisted mode key is unrecovered -> session-only (open item;
  no invented custom.dat key). P4 UI panel stays out of this subject.

**Verification / gates**

- `camera_smoke` extended with the mode->input matrix (pure, no engine).
- Scripted engine runs per mode (`RC_MODE=` + `RC_DEMO_MOVE`, internal
  env-driven test modes only — never drive the user's mouse/keyboard):
  proof `proof/controls/control_modes_run.txt`.
- Must-stay-green gates (§12 root AGENTS) + build exit 0.

**Blocked / open (do not approximate)** — `Camera_UseFullAngle` consumer
unknown; `nCameraMode [0..3]` semantics if undecoded; persisted mode key;
`Camera_IsInFreeView` equivalent if not locatable.

**Branch decision:** continue on `agent/move-controls` (recommended — modes
route through this branch's input/movement code; both subjects merge together),
or merge that subject to main first and branch `agent/control-modes`.

### 7b. Status (2026-09-30, `agent/move-controls`, commit 6833334)

- **M0 done** — HUD shows `op <classical|joystick>`; periodic log carries
  `gait=` and `mode=`; `RC_MODE` + `OPMODE=` fingerprint already live.
- **M3 done (decoded routing)** — **corrected 2026-10-01 by re-decoding the
  packed bytecode** (`StrafeLeftStart` proto 0/76, `StrafeRightStart` 0/78):
  STRAFE is the **only** mode-branched movement handler. CLASSICAL calls
  `SetControl(CONTROL_STRAFE_*)` (upval `SetControl`; falsy -> return) and calls
  `TurnLeftStart/TurnRightStart` when `Camera_IsInFreeView()`; JOYSTICK calls
  `ResponseWASDKey('StrafeLeft/Right', true, double)` (double-tap aware), with
  `Camera_EnableControl` only as the failure fallback. Forward/back/turn
  handlers (protos 0/65-0/74) have no mode branch. Free view is now **decoded**
  (`Camera_IsInFreeView` = `CameraStatus_Animation` flag, true except the god
  camera); in normal play the global `TurnLeftStart/RightStart` handlers are
  the real turn controls, so the default A/D keys (bound to STRAFE) run
  strafe + `TurnLeftStart/RightStart`. The host maps this to `RC_FREEVIEW`:
  **1 (default) = normal play (A/D turn + strafe)** (camera drags behind
  through the 15 deg dead zone), 0 = the god-camera side-step branch with the
  authored `挪步左/右` clips. **No RMB gate exists** (reverted `bb91c08`).
  Classical S back-pedals (`后退01` clip); joystick A/D/S turn to the travel
  heading. Proof: `proof/controls/control_modes_run.txt`.
- **Mode-matched locomotion animation (2026-10-01)** — the locomotion clip is
  selected by travel direction vs facing in **both** modes: >45 deg plays the
  authored side-step (`挪步左/右`, kind 6), >135 deg the back-pedal
  (`后退01`, kind 57), else walk/run. This covers classical side-step
  (`RC_FREEVIEW=0`), classical S, and the joystick pivot (the body plays
  `挪步`/`后退` while the facing catches up with the heading, then the run clip).
  Classical A/D turn-in-place uses model rotation: the F1 catalog has **no
  dedicated ground turn clip** (kinds 5 run / 6 挪步 / 56 walk / 57-58 后退 /
  16-19 jumps), so no clip is invented for it. Thresholds are host values
  pending the engine's clip-selection criteria; proof:
  `proof/controls/control_modes_run.txt`.
- **Turn-key camera coupling fixed** — the heading<->camera-yaw relation is the
  `cameraYawBehind` reflection (`Forward(yaw)=(-cos,-sin)`), so the drag now
  rotates the camera behind the character (was rotating the wrong way after the
  first turn keys landed). Uses the row's
  `CameraAdjustYawWhenMoveTurnDisableAngle` (15 deg) dead zone; the pull rate is
  the char turn step (host interpretation of the documented drag — re-open when
  the `CameraAdjustYawWhenMoveTurn` row values/consumer are decoded).
- **M1 open** — `nCameraMode` `[0..3]` follow-mode semantics still undecoded;
  no application, no invented modes.
- **M2 open** — the drag-release reset path (`Camera_SetResetSpeed(3.5, 3.75)`)
  is not implemented in the host yet; values loaded but unused.
- **Free view DECODED (2026-10-01)** — `Camera_IsInFreeView` is a **Lua**
  getter in `mainscene.lua` (proto 0/1: returns the flag stored by
  `CameraStatus_Animation`, proto 0/0), not a C binding; `CameraStatus_Set`
  sets it to `mode ~= 'god camera'`. True in normal play, false in the god
  camera. When true, `CameraStatus_Animation` restores the real
  `TurnLeftStart/Stop`+`TurnRightStart/Stop` globals; when false it swaps in
  `Strafe*`. **Correction (later 2026-10-01):** the strafe handler's
  free-view branch that calls `TurnLeftStart/RightStart` is guarded by
  `IsPlayerInOBDungeon()` (hotkeys proto 63, true only in OB/spectator
  dungeons), so it is NOT the normal classical A/D path: normal classical
  WASD is handled by the engine input layer and the Lua handlers are overrides
  (joystick free-move, OB camera, displacement). Host `RC_FREEVIEW` maps
  1 = normal play, 0 = god-camera/OB strafe-only. **RMB gate reverted**
  (`bb91c08`): no RMB interaction with turn/strafe exists in the scripts or
  the UI C bindings (`Camera_EnableControl` 0x1800AC1F0 is a plain state
  setter; `Camera_BeginDrag` 0x1800ABFD0 only bridges the drag vtable +0x118).
  Host model gap recorded in `CLASSIC_CONTROLS_AUDIT.md` §5: the run frame
  should be decoupled from the camera and follow per
  `CameraAdjustYawWhenMoveTurn` (moving-only, 15 deg dead zone).
- Gates: camera_smoke (incl. mode gating) ALL PASS; jx3_model 10x PASS;
  verify_model exit 0; capture selftest PASS.

### 7c. Per-mode settings — decode pass + plumbing (2026-09-30)

**Verified freshly from `JX3RepresentX64.dll` disasm (HIGH):** the camera node
(type 0xD, reached via the `[scene+0x20]` chain) keeps two field pairs, selected
by `[node+0x34]` (0 = joystick, nonzero = classic):

| field | classic | joystick | setter (RVA) | clamp |
|---|---|---|---|---|
| drag / drag-pitch speed | +0x6C / +0x70 | +0x84 / +0x88 | `0x180ace2e0` | [0.01, 10] |
| max distance | +0x74 | +0x8C | `0x180ace520` | [1, 2000] |
| spring reset speed | +0x78 | +0x90 | `0x180aced60` | [0.01, 10] |
| camera reset speed | +0x7C | +0x94 | `0x180ace900` | [0.01, 10] |
| follow mode | +0x80 | +0x98 | `0x180ace3f0` | [0, 3] |

`SetCameraFollowCharacterAction` (`0x180ace370`) stores its flag at
`[obj+0x27C]`, which gates the `UpdateCameraFollowAction` path (`0x180b0e820`,
`s_face` lookup) — the follow-action branch of the camera update.

**Applied in the host:** `CameraSettings.ApplyOperationMode()` clamps and applies
the role's per-mode follow mode + spring/camera reset speeds on every switch
(F7 / `RC_MODE` / scripted `RC_MODE_SWITCH_AT`), logged as
`opmode applied: op=... followMode=... springReset=... cameraReset=...`
(proof: `proof/controls/control_modes_run.txt`).

**Still open (no invention):** the per-frame *consumer* of follow mode [0..3]
(what each value changes) and of the reset speeds. **Correction
(2026-10-01):** `Camera_IsInFreeView` is **not** a hashed C registration — it
is plain Lua in `mainscene.lua` (proto 0/1, set by `CameraStatus_Animation`).
`Camera_EnableControl` is the C binding (found in `JX3UIX64.dll`, string
verified). Next probe: the engine consumer of `CONTROL_TURN_*` /
`CONTROL_OBJECT_STICK_CAMERA` (`JX3RepresentX64.dll`: commit 0x1805df660 →
applier 0x1805df7e0, 8-type input switch) to settle the RMB-drag interaction.

### 7d. Engine binding + camera-manager decode (2026-10-01, game-client build)

**Command chain (HIGH, `JX3UIX64.dll` + `JX3ClientX64.exe` + `JX3RepresentX64.dll`):**
Lua bindings live in `JX3UIX64.dll` (`KRepresentScriptTable::Lua*`): 
`Camera_EnableControl` `0x1800AC1F0` is a pure control-state setter (ids 6/7
additionally call camera vtable `+0x110`; returns no values), `Camera_BeginDrag`
`0x1800ABFD0` bridges the represent vtable `+0x118` (returns tx/ty numbers),
`Camera_LockControl` `0x1800ACEE0` is the timed 轻功 lock (skill 9007),
`MouseControlMoveEnable` `0x1800ACF90` stores `{id,flag}` into a type-`0x54`
object and notifies (cmd `0x10`). `MoveControlStart/Stop` (hotkeys 0/94-95) =
`Scene_SetMoveControl(true/false)`. The exe's `KEventCommonMgr` forwards to the
engine world interface by vtable slot: `BeginDragCamera +0x3D0`,
`EndDragCamera +0x3D8`, `SetCameraDragParams +0x3E8`, `ForceResetCamera +0x428`,
`EnableControlCamera +0x450` (`KEventCommonMgr::*` 0x1400EFD60..0x1400F000B).

**Control interpreter (HIGH):** `KGameWorldHandler::AjustCtrlInput`
(`0x1805E1ED0`) forwards to the character controller `0x1805DF350`, which
applies the queued input commands (`0x1805DF7E0`, 8 event types) into controller
fields `+0x7C..+0x98` and calls vtable `[obj+0xA8]` slots
`+0x88/+0x98/+0xA0/+0xA8/+0xB0/+0xB8`. This queue carries move/turn commands;
no mouse-button condition exists at this level.

**Camera manager, game-client build (HIGH):** `MouseMove` `0x180B21300` +
`ApplyMouse` `0x180B1F520` (assert strings; game-client RVAs, not the
MovieEditor build): the manager keeps a per-mode controller array
(`[this+0x2F0]`, stride `0x20`, active index `[this+0x2D0]`), movement
accumulators `+0x98/+0x9C` with clamps `+0x90/+0x94`, a "camera moved" flag
`+0x1A8`, and two source-select flags `+0x1AC/+0x1B0` that choose which
yaw/pitch pair ApplyMouse writes. Mouse deltas are divided by the frame delta,
clamped, then scaled by the **per-mode drag speeds** `[ctx+0x6C]`/`[ctx+0x70]`
(classic) or `[ctx+0x84]`/`[ctx+0x88]` (joystick/other) before ApplyMouse —
matching the camera-node fields from §7c. The path is gated by two global words
`[0x180EDDFE0+0x25CD0]`/`+0x25CF0` and by `[this+0x25C]` + controller `[+8]`.
After ApplyMouse both follow controllers update (`0x18001C035`, `0x18000B7DA`).

**Camera rows (HIGH):** the 10 per-mode camera rows are cached in a
`10 x 0x24` array reached from `[0x180EDDFE0+0x262F0]`; loader `0x180338C10`
fetches each by name from the value source (`[base+0x490]` vtable `+0x98/+0x50`):
`ZoomLength +0x0`, `CameraMovePitchApplyAngle +0x4`, `SmoothTime +0x8`,
`AdjustPitch +0xC`, `ApplyTimeInterval +0x10`,
**`CameraAdjustYawWhenMoveTurn +0x14`**, **`DisableAngle +0x18`**,
`AdjustMaxPitch +0x1C`, `ApplyMaxPitch +0x20`. This confirms the host row
semantics (`FollowYaw` dead zone 0.26) come from real cached per-mode data.

**Implication for the host model (decoded direction):** the only camera-yaw
writer found on the mouse path is ApplyMouse (mouse deltas, gated by the manager
flags); `CONTROL_TURN_*` feeds the character controller queue. No TURN→camera
path was found in the game-client build. Therefore A/D should drive the
character (and the run frame) and must not rotate the camera; the camera is
owned by the mouse/manager drag path. The host's 1:1 `camSys.Yaw -= dturn`
coupling is the deviation (documented in `CLASSIC_CONTROLS_AUDIT.md` §5); the
camera follow should use the cached `CameraAdjustYawWhenMoveTurn` row while
moving. Next probe if needed: identify the writers of the manager flags
`+0x1AC/+0x1B0` and the gate words `+0x25CD0/+0x25CF0` (dynamic scan; static
xref shows only constructors).

Evidence dumps: `%TEMP%\opencode\modes-re\gc_mousemove.txt`,
`gc_applymouse_fn.txt`, `gc_camrow_loader.txt`, `gc_ctrl_apply.txt`,
`exe_eventcommon_camera.txt`, `ui_enablecontrol.txt`, `ui_begindrag.txt`,
`ui_lockcontrol.txt`, `hk_proto61.txt`, `hk_proto63.txt`, `control_full.txt`.

**7d addendum (same dig, later): the mouse/rotation pipeline in full.**
`MouseMove 0x180B21300` → `ApplyMouse 0x180B1F520` → two appliers:
- `ClampMouse 0x180B1FE70`: resolves five named controllers
  (`pCarrierCameraController`, `"camera"`, `pSprintCameraController` kind 0x1B,
  `pGliderCameraController`, `pNPCDialogCameraController`) and clamps/applies
  the deltas to the active one;
- `ApplyRotation 0x180B1FA30` (assert string; called from 0x180B22922):
  dynamic-follow path; checks `[this+0x28C]`, `[this+0x1B0]`, reads state
  `[ctrl+0x17C]`, fetches `pDynamicFollowCameraController`, and — past a
  dead-zone test — writes the **character** yaw: `[character+0x20]` +
  `0x18001F05F` = `mov [rcx+0x30], xmm1` (`0x180530F00`).
- a third applier `0x180B211D0` (alternate branch, entered when
  `[mgr+0x1AC]`/`[mgr+0x1B0]` are set) also writes `[character+0x30]` under
  states `[mgr+0x5C] == 5..7`.
Character-yaw writers (`0x18001F05F` thunk xrefs, HIGH): `0x180B1FCC1`
(ApplyRotation), `0x180B21282` (alternate applier), `0x180B10115`
(state machine step `[obj+0xC0]==3`), `0x180B24EDE` (state transition → 0).
So the engine **does** turn the character from the camera path — conditioned
on the dynamic-follow/stick state, not on keyboard A/D.
Manager fields consolidated: `+0x20` character, `+0x5C` camera state,
`+0x90..0x9C` accumulators, `+0xA4/+0xA8` pitch clamps, `+0x1A8` moved flag,
`+0x1AC` dynamic-follow, `+0x1B0` checked by both ApplyRotation and ApplyMouse,
`+0x25C` enable, `+0x2D0`/`+0x2F0` controller array, `+0x318` flag.
Remaining precise unknowns (3): (1) which of `CONTROL_CAMERA`/`OBJECT_STICK_CAMERA`
sets `[mgr+0x1B0]` vs `[mgr+0x1AC]` (writer not yet located; the vtable slot is
runtime-built), (2) the `[mgr+0x5C]` state enum values 1..7, (3) the character
controller's movement-direction application (facing vs camera-relative).

**7d addendum 2.** `ResetCharacterCamera` (assert string; body at 0x180B090A0+)
bulk-zeroes the per-character camera: state `+0x5C`, `+0x68`, `+0x88`,
`+0xA4/+0xA8` (later pitch clamps), `+0x1A8` (moved), `+0x1AC` (dynamic
follow), `+0x1B0`, `+0x1B4`, `+0xAC`, `+0x1C4..`, and reinitialises
`+0x70/+0x158/+0x1A0` — so `+0x1B0` is a camera-state field, not a control.
A full-binary dword/qword-store scan shows `+0x1B0` is only ever *zeroed*
here: in normal play the ApplyMouse alternate branch is driven by `+0x1AC`
(dynamic follow) alone. `[character+0x30]` (written by 0x180530F00 via the
four sites above) is the engine's character-facing/camera-yaw field; the
write is gated by the follow/state machine, not by A/D.

**7d addendum 3 (final pieces).** The per-frame rotation pipeline is
`UpdateRotation` (assert string; region 0x180B22740+): it resolves
`pCarrierCameraController` / `pGliderCameraController` /
`pTelescopeCameraController`, applies the carrier/glider speeds
(`fRotationSpeed`, asserted > 0), then calls the base applier
`0x180B20C30` and `ApplyRotation 0x180B1FA30` with the frame context from
`[0x180EDDFE0+0x25C10]`. Character-class facts:
- `[controller+0x20]` = `KRLLocalCharacter` (render character);
- `KRLLocalCharacter +0x30` = **face yaw** (float): written by
  `0x180530F00` via `0x18001F05F`, read by
  `KRLLocalCharacter::UpdateFaceFootDirection` (assert string at 0x180534699)
  and passed to `0x1800236D7` as the facing input;
- `[controller+0x5C]` = **move/object state** (set by `UpdateObjectState`,
  assert at 0x180B24F0A; compared against 8, 0x10, and the 5..7 range);
  entering/leaving states 5..7 resets the face yaw, and the alternate
  applier's camera→face write is gated to those states.
So the complete engine rule is: keyboard A/D drive `CONTROL_TURN_*` into the
character command queue (never the camera); the camera is moved only by the
mouse pipeline (`MouseMove`/`ApplyMouse`/`ClampMouse`/`UpdateRotation`) and
may carry the character's face yaw only under the follow/water-air state
conditions above. Host consequence: A/D must not rotate the camera; body
carry belongs to RMB/follow states.
Evidence: `%TEMP%\opencode\modes-re\gc_applyrotation.txt`,
`gc_applyalt.txt`, `gc_clampmouse.txt`, `gc_charyaw_thunk.txt`,
`gc_setcontrolother_thunk.txt`, `exe_createso3.txt`.
