# JX3 movement controls

**Evidence:** `docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md`,
`docs/REBORN_JUMP_FALL_SPEC.md`,
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
| *(host extra)* | double-tap W | — | sprint; real sprint is a 轻功/skill state [MED] |

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
| logic tick | 1/16 s (`GAME_FPS=16`); some systems 15 Hz | PvP research §0 / gravity |
| length | 1 cm; 1 尺 = 64 u = 0.64 m | mesh calibration |
| walk / run | 6 / 20 u per frame (96 / 320 u/s at 16 fps) | `number.krl` |
| ride walk / run | 8 / 40 尺/s | `number.krl` |
| swim | `CharacterSwimSpeed` | `number.krl` |
| yaw turn | `CharacterYawTurnSpeed = 0.007465`, reset `0.0023` | `number.krl` |

Movement replication: `DoMoveCtrl` (C→S type 7, 49 B) and `DoSyncDirection`
(type 0x13, 46 B) carry predicted state + sequence; the server broadcasts
`OnMoveCharacter` / `OnSyncMoveState/Ctrl/Param`, which write the exact fields.

## 4. Jump / fall / 轻功

- takeoff triple per jump index from `JumpParam.tab`; per-frame curves from
  `JumpFrameParam.tab` (`TotalFrame` + 128 records)
- clamps: XY 0..127, Z −2048..2047, gravity 0..31; `vxy_fixed = vxy << 4`
- landing: roll if height diff > 500 u; water variants; fall death server-side
- full model and animation mapping: `docs/REBORN_JUMP_FALL_SPEC.md`

## 5. Display side (facing/animation)

- `KRLLocalCharacter::UpdateDirection`: yaw interpolation with
  `KeepTurningFrame=30`, `TurningEpsilon=0.02`; snap states `{1, 28, 32}`
- locomotion clip chosen by speed thresholds: idle 1 / combat idle 2,
  walk 56, run 5, strafe 6, backpedal 57/58, jumps 16–19
- no dedicated ground turn clip; fly/suspend/rush have turn clips

## 6. Our client today

`client/RebornClient.cs`: WASD camera-relative with held world direction,
Shift ×10 debug, Space jump, `/` walk-run toggle, W-double-tap sprint,
continuous gravity/jump approximation. Missing: turn-in-place keys, autorun,
sit/mount/sheath, click-to-move, follow/interact, exact 15/16 Hz integer model,
turn-rate interpolation + >112.5° penalty, jump chain.

## 7. Open items

1. Ground turn-in-place clip (挪步 vs pure model rotation) unresolved.
2. Exact yaw-turn-speed consumer (`CommonNumber+0x58`) not located.
3. `OnAdjustPlayerMove` / `OnSyncRunSpeedLimit` layouts not decoded.
4. Buff modifiers (`MoveSpeedPercent`, jump chain costs) application.
