# Classic controls — completeness audit

**Date:** 2026-10-01 · **Client:** `reborn_client_control_modes.exe` (`agent/move-controls`)
**Method:** every command bound in the shipped `ui/hotkey/default.txt` (286 rows)
compared against the client's dispatcher (`client/HotkeyTable.cs` + the host key
handlers in `client/RebornClient.cs`). 176 bound commands have no handler today;
most belong to the targeting / action-bar / UI subjects and are listed here only
as a count.

**Status legend:** `DONE` · `PARTIAL` · `TODO <subject>` · `UNBOUND` (no default
key, no action needed) · `BLOCKED-RE` (mechanism undecoded).

## 1. Movement (classic) — all DONE

**The full classic matrix (2026-10-01, decoded from the shipped UI scripts +
the user-observed game behaviour):**

| Input | Behaviour | Speed |
|---|---|---|
| W | run forward **along the facing** (classical; joystick keeps camera-relative) | run 320 u/s (walk 96 when `/` toggled) |
| S | **back-pedal, facing kept** (`后退01` clip) | **walk 96 u/s (slower than forward)** |
| A/D · ←/→ | **decoded + implemented**: A/D/←/→ turn the character (`CONTROL_TURN_*`); the camera is never written by the keyboard. Free-view strafe→turn is OB-dungeon-only (hotkeys proto 63). **No RMB gate exists anywhere** — RMB only starts the camera drag (see §2) | local `RotationSpeed` 0.00314 rad/ms = π rad/s |
| W+A / W+D | run while turning (a curve) — the view and the body rotate together (RMB does not change this) | run 320 u/s, curve radius v/ω |
| S+A / S+D | back-pedal while turning (facing kept) | walk 96 u/s |
| G autorun | forward run | run |

**Decoded gate (2026-10-01, full script + binding decode):** `mainscene.lua`
defines `Camera_IsInFreeView()` as a getter for the flag stored by
`CameraStatus_Animation` (proto 0/1 returns upval = the flag; proto 0/0 stores
it). `CameraStatus_Set` ends with `CameraStatus_Animation(mode ~= 'god camera')`,
so `Camera_IsInFreeView()` is **true in normal play** (`'local camera'`) and
false only in the god camera. With the flag true, `CameraStatus_Animation`
**restores** the real `TurnLeftStart/Stop` + `TurnRightStart/Stop` globals
(false swaps in `Strafe*` for the god camera). **Correction 2026-10-01 (later):**
the strafe handler's `TurnLeftStart/RightStart` call is **not** the normal
classical turn path — its guard (hotkeys proto 63) is
`if IsPlayerInOBDungeon() then Camera_EnableControl(control, flag); return true
end` (nil otherwise), so the free-view strafe+turn branch fires **only in OB
(spectator) dungeons**. Normal classical WASD is consumed by the **engine**
input layer; the Lua handlers are overrides for joystick free-move, OB camera
and displacement skills. Host `RC_FREEVIEW` stays as the host knob for the
god-camera/OB strafe branch.
**Binding map (JX3UIX64.dll, `KRepresentScriptTable::Lua*`, HIGH):**
`Camera_EnableControl(id,bool)` is a pure state setter (returns nothing; ids
6/7 additionally call a camera vtable `+0x110`); control ids 0..13 are
Lua-defined (`control.lua`: FORWARD 0 … OBJECT_STICK_CAMERA 7 … DOWN 13).
`hotkeys`' unnamed wrapper (proto 61) routes CLASSICAL →
`Camera_EnableControl`, JOYSTICK → `Scene_EnableFreeMoveControl`;
`MoveControlStart/Stop` = `Scene_SetMoveControl(true/false)`;
`Camera_BeginDrag` bridges vtable `+0x118` and returns two numbers;
`Camera_LockControl(sec)` = 轻功 camera lock (`IsCharacterMoving(BACKWARD)` +
skill 9007).

| Command | Default key | Status | Note |
|---|---|---|---|
| `MOVEFORWARD` | W / Up | DONE | camera-relative, RunTo turn model |
| `MOVEBACKWARD` | S / Down | DONE | back-pedal at walk pace, facing kept (`后退01`) |
| `STRAFELEFT` / `STRAFERIGHT` | A / D | DONE | free view: turn the view + body; `RC_FREEVIEW=0`: side-step branch (挪步 clips) |
| `TURNLEFT` / `TURNRIGHT` | Left / Right | DONE | same keyboard turn (view + body) |
| `JUMP` | Space | DONE | + 二段跳, jump XY, landing branch |
| `TOGGLERUN` | Numpad / | DONE | main `/` host convenience |
| `TOGGLEAUTORUN` | G / NumLock | DONE | cancel set open (backward/strafe) |
| `STRAFERIGHT` `runOnUp=1` quirk | — | BLOCKED-RE | shipped data quirk (down handler on release) not modeled |

## 2. Camera (classic)

| Command | Default key | Status | Note |
|---|---|---|---|
| `CAMERAORSELECTORMOVE` | LMB | PARTIAL | drag = camera only, DONE; **click select/move missing** (`docs/controls/CONTROLS_GAP_REGISTER.md` S7, targeting subject) |
| `CAMERAORSELECTORMOVESTICKY` | RMB | DONE | **script-verified**: `Scene_OnSceneRButtonDown` → `Camera_BeginDrag(2.0)` + `Camera_EnableControl(CONTROL_OBJECT_STICK_CAMERA, true)` (gated by `Hotkey_IsRMouseEnabled`); no script-level body turn. Camera orbit in-classic is the engine's consumer of `CONTROL_OBJECT_STICK_CAMERA` (host coupling) |
| `CAMERAZOOMIN` / `CAMERAZOOMOUT` | wheel up/down | **DONE (restored 2026-10-01)** | real default `Camera_Zoom(0.9/1.1)`; `+/-` keys kept as host extra, wheel-inert host deviation A12 superseded |
| `CAMERARESET` | F11 | DONE | behind, game pitch −15° |
| `CAMERA_SET_VIEW_1/2` | Home / End | DONE | behind / front presets |
| `CAMERAUP` / `CAMERADOWN` | (unbound) | UNBOUND | `MoveUpStart/Stop`, `MoveDownStart/Stop` exist, no default key |
| camera obstruction | — | TODO camera (S9) | camera clips through walls/structures (terrain-only probe) |
| 广角/FOV | — | TODO camera (S8) | panel value → `Set3DEngineOption(fCameraAngle)` not applied |
| follow mode `nCameraModeInClassicMode` | — | BLOCKED-RE | per-mode value read/clamped/applied at switch, per-frame consumer `[0..3]` undecoded |
| spring / camera reset speeds | — | BLOCKED-RE | values read/clamped/applied at switch, reset path not decoded |

## 3. Base character actions

| Command | Default key | Status | Needs / note |
|---|---|---|---|
| `TOGGLESITDOWN` | V / X | **DONE (2026-10-01)** | decoded 0/98: sit = `OnUseSkill(17 打坐)`, stand = `Stand()`; host plays the catalog's looping `F1b02dj打坐a.tani` and stands on any movement/jump intent |
| `TOGGLESHEATH` | Z | **DONE (2026-10-01)** | decoded 0/97: toggles `SetSheath`; gates sit/death/fight/bird/horse/tower/buff (only the sit gate is modellable in the host); draw = `F1b02ty拔剑01_start01.ani` → `…st01_持续.tani` stance, sheathe back to idle (no 收剑 clip ships for b02) |
| `RIDEHORSE` / `DownHorse` | T | TODO | horse actor + mounted state/speeds |
| `FOLLOWTARGET` | Ctrl+G | TODO | target system |
| `AUTOINTERACT` | F | TODO | NPC/interactable lookup |
| `SCREENSHOT` | PrintScreen | PARTIAL | host has F9 repro shot only |
| `KINESCOPE` | Ctrl+Shift+C | TODO | video capture (host) |

## 4. Separate subjects (no handler today, by design)

| Group | Bound commands | Subject |
|---|---|---|
| Targeting | `SEARCH_ENEMY` (Tab), `SELECT_SELF` (F1), `SELECT_TEAMMATE1-4`, `ATTACKTARGET` | targeting (C7-C9) |
| Action bar | `ACTIONBAR1_BUTTON1-12`, page keys, extra bars | action bar (C7) |
| UI panels | ~60 `TOGGLE_*` commands | UI system (C13) |
| Bags/inventory | `OPENORCLOSEALLBAGS` etc. | inventory |

## 5. Fixed in this pass

- **Wheel zoom restored** to the real default (`CAMERAZOOMIN/OUT` on wheel
  256/257 → `Camera_Zoom(0.9/1.1)`), keeping the `+/-` host keys. The
  2026-09-30 wheel-inert decision is superseded; say the word to revert.
- **`TOGGLESITDOWN` (V/X)** implemented from the decoded body (sit = skill 17
  打坐, stand = `Stand()`), with the looping 打坐 clip and stand-on-move.
- **`TOGGLESHEATH` (Z)** implemented from the decoded body (SetSheath toggle,
  sit gate), with the b02 draw transition + drawn-stance loop.
- **Turn rate is local data**: derived from the camera-controller row
  `RotationSpeed` (loader default `0.00314` rad/ms = π rad/s); no server byte is
  involved in the keyboard turn. Fallback π only if the row is absent.
- **RMB-held keyboard turn gate — REVERTED 2026-10-01** (commit `bb91c08`).
  The shipped scripts contain **no RMB-conditional turn/strafe logic**
  (checked: `Scene.lua`, `OperationModeBase.lua`, `hotkeys.lua`,
  `mainscene.lua`, `CameraCommon.lua`, `control.lua` — all extracted from the
  game client's own paks): RMB only starts camera drag (`Camera_BeginDrag(2.0)`
  + `CONTROL_OBJECT_STICK_CAMERA`). **Binding layer checked too** (2026-10-01):
  `Camera_EnableControl` (`JX3UIX64.dll` `0x1800AC1F0`) is a plain control-state
  setter (ids 6/7 also call a camera vtable `+0x110`; no turn interaction, no
  return value); `Camera_BeginDrag` (`0x1800ABFD0`) only bridges vtable `+0x118`
  (drag) and returns two numbers; `Camera_LockControl` is the 轻功 timed lock
  (skill 9007). The engine's own WASD/classical handling (where an RMB
  interaction could still live) is in the represent/input layer, not the UI
  bindings: the input-state applier `0x1805df7e0` (8 event types → controller
  fields `+0x7C..+0x98`, vtable `[obj+0xA8]` slots `+0x88/+0x98/+0xA0…`) is the
  next trace target. Host has **no provisional rule** on this path; A/D behave
  the same with and without RMB.
- **Host model — IMPLEMENTED 2026-10-01 (final form)**: classical movement
  runs along the **character facing** (`KRLLocalCharacter+0x30` semantics),
  spawn-synced to the initial camera view. A/D + arrows turn the character
  **and the camera at the same rate (1:1)** - the client's historic coupling
  (the old `ApplyRotation` wrote the identical yaw delta to the camera
  controller; the rate is the local `RotationSpeed` row, 0.00314 rad/ms =
  π rad/s). While **RMB holds the camera drag**, A/D turn the character but
  the camera is not keyboard-turned (the observed classic rule). The camera
  additionally follows a moving forward character through the cached
  `CameraAdjustYawWhenMoveTurn` row (15° dead zone, rate-limited, skipped
  while a drag is active). Evidence: run `reborn_20261001_220719.log` -
  A-alone: yaw +3.14, cam -3.14, `dpos=(0,0)`; TURNRIGHT 0.6 s: +1.88/-1.88;
  W+A/W+D: ±2.51 on both; back-pedal: camera kept, dist 96. Gates: smoke
  ALL PASS; jx3_model 10x PASS; verify_model exit 0; loot selftest PASS.

## 6. Recommended order

1. Camera obstruction S9 + FOV S8 (classic camera feel; camera subject).
2. Mount (T) — horse actor + mounted movement (the last base action).
3. Targeting (Tab/F1-F5) — unlocks click-select, follow and interact.
4. Follow-mode `[0..3]` / reset-speed decode (live debug on the real client).
