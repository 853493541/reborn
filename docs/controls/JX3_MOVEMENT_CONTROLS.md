# JX3 movement controls

**Evidence:** `docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md`,
`docs/movement/REBORN_JUMP_FALL_SPEC.md`,
`proof/movement/extracted/ui_hotkey_default.txt`.

---

## 1. Key map (real defaults)

| Command | Key(s) | Lua handler | Notes |
|---|---|---|---|
| `MOVEFORWARD` | W / ↑ | `MoveForwardStart(); MoveForwardStop();` | camera-relative forward |
| `MOVEBACKWARD` | S / ↓ | `MoveBackwardStart/Stop` | |
| `STRAFELEFT` / `STRAFERIGHT` | A / D | `StrafeLeftStart/Stop`, `StrafeRightStart/Stop` | `STRAFERIGHT` has `runOnUp=1` |
| `TURNLEFT` / `TURNRIGHT` | ← / → | `TurnLeftStart/Stop`, `TurnRightStart/Stop` | turn in place |
| `JUMP` | Space | `Jump(); EndJump();` | hold/air press → 轻功 chain |
| `TOGGLERUN` | Numpad `/` | `ToggleRun();` | walk ⇄ run **toggle**, not hold |
| `TOGGLEAUTORUN` | G / NumLock | `ToggleAutoRun();` | forward without holding W |
| `RIDEHORSE` | T | `RideHorse(); DownHorse();` | ride walk/run speeds |
| `TOGGLESHEATH` | Z | `ToggleSheath();` | weapon drawn/sheathed |
| `TOGGLESITDOWN` | V / X | `ToggleSitDown();` | 打坐 |
| `FOLLOWTARGET` | Ctrl+G | `FollowTarget();` | auto-follow |
| `AUTOINTERACT` | F | `AutoInteract();` | interact nearest |
| `TOGGLEMOVECONTROL` | — | `MoveControlStart/Stop` | control lock used by UI states |
| *(click)* | LMB on ground | `AutoMoveToPoint` / `AutoMoveToTarget` | click-to-move |
| *(host extra)* | double-tap W | — | sprint; real sprint is a 轻功/skill state [MED] — **removed 2026-09-30** (WW trigger dropped per user; the sprint row stays reachable via `RC_CAM_MODE` only) |

## 2. Movement model (logic side, server-authoritative + client prediction)

Fields (verified from disassembly):

| Field | Meaning |
|---|---|
| `+0x1F4` move state | 2 walk, 3 run, 4 jump, 7 swim, 8 ?, `0x17` sprint dash, `0x1C` ?, `0x21` fly-jump end |
| `+0x10/+0x14/+0x18` | world position x/y/z (cm) |
| `+0x26C` | motion heading (`atan2(dx,dy)`, byte angle) |
| `+0x44` | facing angle (0..255 = 0..360°) |
| `+0x48` | per-frame turn step (server sync byte) |
| `+0x2F8` | XY speed (units/frame, clamp 0..127) |
| `+0x270` | Z speed (clamp ±2048) |
| `+0x320` | gravity (units/frame², clamp 0..31 on jump) |
| `+0xF9C` | move sequence counter (client player) |

Per-frame turn rule (RunTo `0x14031B7A9`): `delta = heading − facing`;
if `|delta| > 0x50` (112.5°) the movement speed is halved that frame and the
turn step is halved.

States accepted by ground locomotion: `{1, 2, 3, 4, 8, 0x1C}` (mask
`0x1000011E`).

## 3. Speeds / units (table values)

| Constant | Value | Source |
|---|---|---|
| logic tick | **character logic 15 Hz (66.7 ms)**; combat tables 16 fps (`GAME_FPS=16`) | `JX3_COLLISION_SYSTEM.md` §"ticks" (`:190-191`); gravity research |
| length | 1 cm; 1 尺 = 64 u = 0.64 m | mesh calibration |
| walk / run | 6 / 20 u per frame (96 / 320 u/s at 16 fps) | `number.krl` |
| ride walk / run | 8 / 40 尺/s | `number.krl` |
| swim | `CharacterSwimSpeed` | `number.krl` |
| yaw turn | `CharacterYawTurnSpeed = 0.007465`, reset `0.0023` | `number.krl` |

**Tick note (2026-09-30):** the character logic (movement integration,
`DoMoveCtrl`) runs at **15 Hz**; `GAME_FPS=16` is the combat data-table time
(`docs/movement/JX3_COLLISION_SYSTEM.md:190-191`). The host still integrates
continuous dt for walk/run (96/320 u/s) and converts jump tables at 15 Hz; the
exact integer 15 Hz port remains open (C11). The `CharacterYawTurnSpeed`
consumer is still not located; the host uses the registered π rad/s fallback
(S6).

Movement replication: `DoMoveCtrl` (C→S type 7, 49 B) and `DoSyncDirection`
(type 0x13, 46 B) carry predicted state + sequence; the server broadcasts
`OnMoveCharacter` / `OnSyncMoveState/Ctrl/Param`, which write the exact fields.

## 4. Jump / fall / 轻功

- takeoff triple per jump index from `JumpParam.tab`; per-frame curves from
  `JumpFrameParam.tab` (`TotalFrame` + 128 records)
- clamps: XY 0..127, Z −2048..2047, gravity 0..31; `vxy_fixed = vxy << 4`
- landing: roll if height diff > 500 u; water variants; fall death server-side
- full model and animation mapping: `docs/movement/REBORN_JUMP_FALL_SPEC.md`

## 5. Display side (facing/animation)

- `KRLLocalCharacter::UpdateDirection`: yaw interpolation with
  `KeepTurningFrame=30`, `TurningEpsilon=0.02`; snap states `{1, 28, 32}`
- locomotion clip chosen by speed thresholds: idle 1 / combat idle 2,
  walk 56, run 5, strafe 6, backpedal 57/58, jumps 16–19
- no dedicated ground turn clip; fly/suspend/rush have turn clips

## 5b. Lua bridge — resolved 2026-09-25 (detail: `RESEARCH_RESOLVED_GAPS.md` §5)

`ui/script/hotkeys.lua` implements the command wrappers; `ui/script/control.lua`
defines the control ids (0 forward … 13 down, `CONTROL_CAMERA=6`,
`CONTROL_OBJECT_STICK_CAMERA=7`, `CONTROL_JUMP=9`, `CONTROL_AUTO_RUN=10`).
The forward chain is:

```
MoveForwardStart() → player.HoldW = 1
                   → SetControl(CONTROL_FORWARD, true)
                   → ResponseWASDKey('Forward', true, doubleTap)   // engine C
MoveForwardStop()  → player.HoldW = 0; CheckEndSprint() if no other key
                   → SetControl(CONTROL_FORWARD, false)
```

- `IsKeyDoubleDown()` supplies the double-tap flag (sprint family).
- Strafe handlers branch on `GetOperationMode() == CLASSICAL_MODE` and
  `Camera_IsInFreeView` and use `Camera_EnableControl` (classic) vs
  `Scene_EnableFreeMoveControl` (joystick).
- Directional skills use `OnUseSkill(3799 forward, 3801 strafe-left,
  3802 strafe-right, …)` with a `%10 + 1` variant index.
- `ResponseWASDKey`, `IsKeyDoubleDown`, `Camera_EnableControl`,
  `Scene_EnableFreeMoveControl`, `GetOperationMode` are engine globals
  (not defined in the extracted UI Lua).

## 6. Our client today

`client/RebornClient.cs` (feature build `reborn_client_move_controls.exe`,
2026-09-30 `agent/move-controls`):

- **Input core (C1/C2)**: `client/HotkeyTable.cs` decodes
  `ui/hotkey/default.txt` + `bindings.ini` at startup (embedded snapshot of
  `proof/movement/extracted/`; `RC_HOTKEY_DIR` overrides with live files) and
  dispatches the movement command set; the full table (286 rows / 430 commands)
  is loaded and logged, other commands count as unhandled. Shift is ignored for
  the movement set only (host debug ×10); Ctrl/Alt match exactly (Alt+W is a
  skill command, not forward).
- WASD camera-relative **recomputed per frame** (rotating the camera steers the
  run), heading/facing turn model with the >112.5° speed/turn-step penalty.
- **TURNLEFT/TURNRIGHT (←/→)** turn in place at the char turn rate; the camera
  drags behind through the `cameraYawBehind` reflection with the row's 15°
  `CameraAdjustYawWhenMoveTurnDisableAngle` dead zone; **TOGGLEAUTORUN
  (G/NumLock)** runs forward hands-free (cancel: backward/strafe);
  **TOGGLERUN (Numpad /)**, debug main-`/` kept.
- **Operation modes** (F7 / `RC_MODE`): the packed `hotkeys.lua` bytecode shows
  STRAFE is the only mode-branched movement handler — CLASSICAL:
  `SetControl(CONTROL_STRAFE_*)` and, when `Camera_IsInFreeView()`,
  `TurnLeftStart/TurnRightStart`; JOYSTICK: `ResponseWASDKey('StrafeLeft/Right')`
  free-move (no free-view branch). Forward/back/turn handlers are
  mode-independent. Host: classical A/D **turn in place** by default
  (`RC_FREEVIEW=1`, the observed game behaviour; the camera drags behind with
  the 15° dead zone) — `RC_FREEVIEW=0` restores the decoded side-step branch
  with the authored `F1b02yd挪步左/右.tani` clips. Classical S back-pedals
  (`F1b02yd后退01.tani`); joystick A/D/S turn the body to the travel heading
  (run clip). Per-mode follow mode / reset speeds are applied at switch; the
  free-view state itself and the follow-mode `[0..3]` consumer stay open
  (`OPERATION_MODES_PLAN.md` §7c); proof `proof/controls/control_modes_run.txt`.
- **Base actions**: `TOGGLESITDOWN` (V/X — decoded 0/98: `OnUseSkill(17 打坐)` /
  `Stand()`; the host plays the looping `F1b02dj打坐a.tani` pose and stands on
  any movement/jump intent) and `TOGGLESHEATH` (Z — decoded 0/97: `SetSheath`
  toggle with the sit/death/fight/bird/horse/tower/buff gates; b02 draw =
  `F1b02ty拔剑01_start01` → `…st01_持续` stance, sheathe back to idle). Mount /
  follow / interact await the mount and targeting subjects
  (`CLASSIC_CONTROLS_AUDIT.md`).
- **Mode-matched locomotion clips**: travel direction vs facing selects the
  clip in both modes (>45° = `挪步左/右` kind 6, >135° = `后退01` kind 57, else
  walk/run). So the joystick pivot plays `挪步`/`后退` while the body turns to
  the heading, then the run clip; classical side-step (`RC_FREEVIEW=0`) and S
  use the same clips. Classical A/D turn-in-place has no authored F1 turn clip
  — the model rotates (catalog-verified).
- **Jump carries horizontal takeoff velocity** (`JumpSpeedXY` of the row,
  clamp 0..127, 15 Hz, `RC_JUMP_SCALE`) along the input direction when moving;
  a standing jump is ballistic-vertical (no move intent -> no horizontal
  velocity). Airborne integration is ballistic (no WASD air steering -
  `ProcessAcceleration` has no horizontal input term). Landing stop resets the
  horizontal velocity.
- **Landing branch**: drop > `FallDownHeightFloor` (500 u) plays the authored
  F1 `FallFloorAnimation` (`f1b02yd握拳小跳c.ani`, `RC_CLIP_LAND`); else the
  normal resume.
- Space jump + 二段跳 (J0-profile flip, `RC_JUMP_SCALE`), continuous
  gravity, ground clamp/ledge/step collision, RMB body turn at π rad/s
  (server `+0x48` step undecoded).
- Scripted proof: `proof/controls/movement_controls_run.txt` (autorun → forward
  jump → turn key → 600 u drop → roll).

Missing: sit/mount/sheath, click-to-move, follow/interact, exact 15 Hz integer
model, jump-chain phase, swim/sprint/parkour kit.

## 7. Open items

1. Ground turn-in-place clip (挪步 vs pure model rotation) unresolved.
2. Exact yaw-turn-speed consumer (`CommonNumber+0x58`) not located; host
   fallback π rad/s (registered, S6).
3. `OnAdjustPlayerMove` / `OnSyncRunSpeedLimit` layouts not decoded.
4. Buff modifiers (`MoveSpeedPercent`, jump chain costs) application.
5. **Air steering**: no horizontal input term found in the decoded per-frame
   integrator; click-to-move while airborne (`WalkTo` state-4 branch,
   `JX3_CHARACTER_MOVEMENT_RESEARCH.md` §3.2) not ported (host has no
   click-to-move). `KCharacter::Jump` writes `Vxy` unconditionally; whether the
   air commit / `EndJump` stop path clears it for a standing jump is the next
   probe - the host keeps standing jumps vertical until then (not invented).
6. **Autorun cancel rules**: backward/strafe cancel per host reading of the
   `MoveAction_StopAll` handler; exact per-key cancel set (jump/turn/forward)
   still to be decoded from the packed hotkey Lua.
7. **Integer tick port (C11)**: character logic 15 Hz vs host continuous dt -
   reconcile walk/run conversion when the port lands (G-14 in
   `JX3_COLLISION_SYSTEM.md`).
