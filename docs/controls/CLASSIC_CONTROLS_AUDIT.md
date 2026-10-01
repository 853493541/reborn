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
| W | run forward (camera-relative), facing follows | run 320 u/s (walk 96 when `/` toggled) |
| S | **back-pedal, facing kept** (`后退01` clip) | **walk 96 u/s (slower than forward)** |
| A/D · ←/→ | **turn: the view (camera) rotates at the char turn rate; standing still the body turns with it, while moving the body follows the rotating camera-relative heading** | yaw rate |
| W+A / W+D | **run while turning (a curve)** — the view and the body rotate together | run 320 u/s, curve radius v/ω |
| S+A / S+D | back-pedal while turning (facing kept) | walk 96 u/s |
| G autorun | forward run | run |

**Decoded gate:** `mainscene.lua`'s `CameraStatus_Set` calls
`CameraStatus_Animation(mode ~= 'god camera')`; `CameraCommon.lua` enters
`'local camera'` for the player, so `Camera_IsInFreeView()` is **true in normal
play**. In free view the strafe handler (proto 0/76) calls
`TurnLeftStart/RightStart`, i.e. **A/D turn**; the non-free branch
(`SetControl` side-step) only applies to the god camera and stays reachable
with `RC_FREEVIEW=0` for tests.

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
| `CAMERAORSELECTORMOVESTICKY` | RMB | DONE | camera + character turn (classical) |
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

## 6. Recommended order

1. Camera obstruction S9 + FOV S8 (classic camera feel; camera subject).
2. Mount (T) — horse actor + mounted movement (the last base action).
3. Targeting (Tab/F1-F5) — unlocks click-select, follow and interact.
4. Follow-mode `[0..3]` / reset-speed decode (live debug on the real client).
