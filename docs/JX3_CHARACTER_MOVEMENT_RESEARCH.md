# JX3 character movement & turning — research

**Branch:** `feature/real-map-collision`
**Date:** 2026-09-22
**Primary binaries:** `JX3ClientX64.exe` (logic), `JX3RepresentX64.dll` (display),
`KGUIX64.dll` (UI / hotkeys), `KG3DEngineX64.dll` (key names)
**Method:** string extraction + targeted disassembly (`tools/netcode/xref_string.py`,
`tools/movement/find_xrefs.py`) + official PakV4 extraction of the UI hotkey tables
(`tools/movement/extract_ui_filepath.py`).

Question driving this: *how does character movement work in the real client — what
are the inputs, what happens when the character turns, and how is it replicated?*

**Answer (short):** movement is a **server-authoritative, client-predicted**
system. Input is a hotkey table (`\UI\Hotkey\default.txt`) that maps keys to Lua
commands (`MoveForwardStart()`, `TurnLeftStart()`, …). The logic character keeps a
**move state** (`+0x1F4`: 2=walk, 3=run, 4=jump, 7=swim, …), a **motion heading**
(`+0x26C` = `atan2(dx,dy)`), a **facing angle** (`+0x44`, byte 0..255 = 0..360°)
and a **turn rate** (`+0x48`), and every frame interpolates facing toward heading.
If the required turn exceeds **0x50/0x100 (112.5°)**, movement speed is **halved**
for that frame. The display layer interpolates the model yaw with
`KeepTurningFrame`/`TurningEpsilon` from `tabCGAni`, and dedicated turn clips exist
for fly/suspend/rush (`TurnLeftAnimation`/`TurnRightAnimation`, `Lturn`/`Rturn`),
but no dedicated ground turn clip was found. Movement is replicated with
`DoMoveCtrl` (C→S type 7) and `DoSyncDirection` (C→S type 0x13), and applied from
`OnMoveCharacter` / `OnSyncMoveState` / `OnSyncMoveCtrl` / `OnSyncMoveParam`.

---

## 1. Architecture layers

```
keyboard / mouse
  └─ UI::KHotkeyMgr (KGUIX64.dll)          binding table, key states
       └─ Lua command (bindings.ini)       MoveForwardStart(), TurnLeftStart(), ...
            └─ KGameWorldHandler::CommitInput          (JX3RepresentX64.dll 0x1805E3270)
                 └─ character controller commit        0x18001AB90(controller, input, 1)
                      └─ KCharacter logic (JX3ClientX64.exe)  move states, heading, velocity
                           ├─ KCharacter::WalkTo / RunTo     ground locomotion
                           ├─ KCharacter::MoveTo             position/destination set
                           └─ KCharacter::TurnTo             facing set
                                └─ KPlayerClient::DoMoveCtrl / DoSyncDirection   (C→S)
                                     └─ server broadcasts OnMoveCharacter /
                                        OnSyncMoveState / OnSyncMoveCtrl / OnSyncMoveParam
                                          └─ KCharacter::MoveTo + field writes (S2C apply)
display:
  KRLLocalCharacter::UpdateDirection (Represent)   yaw interpolation + turn clips
  KRLRushState::UpdateMoveAnimation               walk/run/strafe/back anim selection
  KRLRemoteCharacter::UpdateDirection             remote heading interpolation
```

Evidence: `proof/movement/disasm/client_movement_symbols.txt`,
`proof/movement/disasm/represent_movement_symbols.txt`,
`proof/movement/disasm/kgui_hotkey*.txt`.

---

## 2. Inputs — real client defaults (decoded)

### 2.1 Hotkey system

| Item | Value | Evidence |
|---|---|---|
| Hotkey manager | `UI::KHotkeyMgr::Init/Load/Save/GetBinding/SearchBinding/RefreshKeyDownData/LoadDefault/LoadBinding` | KGUIX64 strings `0x005AF360…` |
| Path table | `\ui\filepath.txt` (tab) read by `UI::KFilePathMgr::Init` (`0x1800E1780`) | disasm `kgui_filepath.txt` |
| Default binding set | `HotkeyBindingSetPath = \UI\Hotkey\default.txt` | extracted `proof/movement/extracted/ui_filepath.txt` |
| Command definitions | `HotkeyBindingsFile = \UI\Hotkey\bindings.ini` | same |
| Tab columns | `name, context, key1, key2, index` | KGUI strings / `LoadBinding` disasm |
| Lua accessors | `UI::LuaGetHotKey`, `Lua_Get/Set/IsDown/IsKeyDown/IsUsed`, `Lua_AddBinding`, `Lua_SetCapture`, `Lua_EnableKeyDownLoop` | KGUIX64 strings |
| Repeat | `GetKeyTimeInterval`, `Hotkey_EnableKeyDownLoop`, `Hotkey_EnableNewMode` | KGUIX64 strings |

### 2.2 Key encoding (verified from `LoadAsOldAdd`)

The tab stores an integer per key: **low 16 bits = virtual-key code, high 16 bits =
modifier word** (`shr eax,0x10` → `nKey`/mods split; `kgui_hotkey_bits.txt`).
Modifier bits (verified against `bCtrl`/`bShift`/`bAlt` Lua field population):

| bit (high word) | modifier |
|---|---|
| `0x0001` | **Ctrl** |
| `0x0002` | **Shift** |
| `0x0004` | **Alt** |

Examples: `ACTIONBAR2_BUTTON1 = 262193 = 0x40031` → Alt+1;
`TOGGLE_UI = 65621 = 0x10055` → Ctrl+U;
`SKILL_CAST_FORWARD = 262231 = 0x40057` → Alt+W;
`TOGGLE_NPC = 196686 = 0x3004E` → Ctrl+Shift+N.

Special mouse codes: `1` = LMB, `2` = RMB, `256` = wheel up, `257` = wheel down.
All other values are standard Windows `VK_*` codes (`KG3DEngineX64_strings.txt`
contains the full `VK_*` name table used for display).

### 2.3 Movement & camera defaults (`ui/hotkey/default.txt`)

| Command | key1 | key2 | Lua (bindings.ini) | Meaning |
|---|---|---|---|---|
| `MOVEFORWARD` | 87 (`W`) | 38 (`Up`) | `MoveForwardStart(); MoveForwardStop();` | 前进 |
| `MOVEBACKWARD` | 83 (`S`) | 40 (`Down`) | `MoveBackwardStart(); MoveBackwardStop();` | 后退 |
| `TURNLEFT` | — | 37 (`Left`) | `TurnLeftStart(); TurnLeftStop();` | 左转 |
| `TURNRIGHT` | — | 39 (`Right`) | `TurnRightStart(); TurnRightStop();` | 右转 |
| `STRAFELEFT` | 65 (`A`) | — | `StrafeLeftStart(); StrafeLeftStop();` | 左平移 |
| `STRAFERIGHT` | 68 (`D`) | — | `StrafeRightStart(); StrafeRightStop();` | 右平移 |
| `JUMP` | 32 (`Space`) | — | `Jump(); EndJump();` | 跳跃 |
| `TOGGLERUN` | 111 (`Numpad /`) | — | `ToggleRun();` | 切换走路/跑步 |
| `TOGGLEAUTORUN` | 71 (`G`) | 144 (`NumLock`) | `ToggleAutoRun();` | 切换自动前进 |
| `RIDEHORSE` | 84 (`T`) | — | — | 上马 |
| `TOGGLESHEATH` | 90 (`Z`) | — | — | 收/拔武器 |
| `TOGGLESITDOWN` | 86 (`V`) | 88 (`X`) | — | 坐下/起身 |
| `FOLLOWTARGET` | Ctrl+G | — | `FollowTarget();` | 跟随目标 |
| `CAMERAORSELECTORMOVE` | LMB (1) | — | — | 鼠标移动/选择 |
| `CAMERAORSELECTORMOVESTICKY` | RMB (2) | — | — | 粘性鼠标移动 |
| `CAMERAZOOMIN` | wheel up (256) | — | — | 镜头拉近 |
| `CAMERAZOOMOUT` | wheel down (257) | — | — | 镜头拉远 |
| `CAMERARESET` | F11 (122) | — | — | 镜头复位 |
| `SKILL_CAST_FORWARD` | Alt+W | — | `CastSkillByKeyDown(...)` | 招式方向·前 |
| `SKILL_CAST_BACK` | Alt+S | — | `CastSkillByKeyDown(...)` | 招式方向·后 |
| `SKILL_CAST_LEFT` | Alt+A | — | `CastSkillByKeyDown(...)` | 招式方向·左 |
| `SKILL_CAST_RIGHT` | Alt+D | — | `CastSkillByKeyDown(...)` | 招式方向·右 |

Interpretation: **W/S move along the camera forward axis, A/D strafe, ←/→ turn in
place, Space jumps, Numpad `/` toggles walk/run** (it is a toggle, not a hold
modifier). There is no dedicated "run" key; `TOGGLERUN` flips the walk/run state.
`TOGGLEAUTORUN` (G) makes the character keep moving forward without holding W.

> Note: this table is the *default* set shipped in the client pak. Per-character
> overrides are saved in `userdata/<account>/<server>/<role>/hotkey*.txt`
> (`name index key` TSV, empty = default). The MovieEditor has a separate
> `HotKeyConfig.txt` and is not the game mapping.

### 2.4 Input → logic bridge

- The `down=` Lua commands (`MoveForwardStart()` etc.) are defined in the packed
  UI Lua packages (not in the loose `interface/*.lua` files); the engine exposes
  the binding entry points.
- The display-side commit is `KGameWorldHandler::CommitInput` (`0x1805E3270`):
  resolves the character controller (`0x180014E2F`), gets the input payload
  (`0x18000AA3D`) and commits it (`0x18001AB90(controller, payload, 1)`).
- Related Represent APIs (all in `JX3RepresentX64.dll`):
  `MouseControlMoveEnable`, `LockControl`, `IsCharacterMoving`,
  `TurnToFaceDirection` (`0x1805FD2C0`), `AutoMoveToPoint` (`0x1805E20E0`),
  `AutoMoveToTarget`, `IsAutoMoving` — the last three implement **click-to-move**.
- Ride turn input: `GetTurnInputYaw` / `KRLRide::LuaGetTurnInputYaw`
  (`0x180469630`).

**Gap:** the exact Lua definition of `MoveForwardStart` etc. lives in the packed
interface packages (compiled). The engine-side commit function is located, which
is sufficient for a host reimplementation.

---

## 3. Logic-side movement model (`KCharacter`, `JX3ClientX64.exe`)

### 3.1 Character fields (verified from WalkTo/RunTo/MoveTo/sync handlers)

| Field | Meaning | Notes |
|---|---|---|
| `+0x08` | character ID | bit 30 = player (`bt edx,0x1E`) |
| `+0x10/+0x14/+0x18` | world position x, y, z (int units) | `MoveTo` writes |
| `+0x2A0/+0x2A4/+0x2A8` | move destination x, y, z | written by WalkTo/RunTo/`OnSync*` |
| `+0x268` | VelocityXY ×16 fixed point | clamp `[0, 0x7FF]` |
| `+0x2F8` | VelocityXY, units/frame | clamp `[0, 127]` |
| `+0x270` | VelocityZ, units/frame | clamp `[-2048, 2047]` |
| `+0x26C` | **motion heading** (byte angle) | `atan2(dx, dy)` toward destination/velocity |
| `+0x44` | **facing angle** (0..255, 1 unit = 360/256°) | set by `TurnTo`, server sync |
| `+0x48` | **turn rate** (per-frame step) | set from server sync byte |
| `+0x1F4` | **move state** | 2=walk, 3=run, 4=jump, 7=swim, 0x17=sprint dash, … |
| `+0x2FC` | character height | waterline + default speed base |
| `+0x320` | gravity, units/frame² | see gravity research |
| `+0x340` | synced movement param | included in move packets |
| `+0xC08` | scripted-move frame counter | jump/parkour/skill moves |
| `+0x174/+0x204/+0x210/+0xC04` | movement flags | cleared by WalkTo/RunTo |
| `+0xF9C` | move sequence counter (client player) | incremented per sent packet |
| `+0x200/+0x208/+0x22C/+0x230/+0x2CC/+0x2F4` | state flag bits | from `OnSyncMoveParam` |
| `+0x378/+0x37C/+0x380/+0x384` | turn-range params | from `OnSetTurnRange` |

### 3.2 `KCharacter::WalkTo` (`0x140320F30`) — state 2

Args: `(char, nX, nY, ? , speed, ?)`; `nX/nY` are world coordinates.

1. **State gate:** `state <= 0x1C` and bit set in mask `0x1000011E`
   (states **1, 2, 3, 4, 8, 0x1C**). Otherwise reject.
2. Destination validity via shared path check `0x1403105A0`.
3. **Default speed** when `speed == 0`:
   `6 × height / 10` for NPCs, `6 × height / 7` for players (`+0x2FC`; same
   scaling as `GetWaterline` — 0.6·h NPC / 0.857·h player).
4. **Destination == current position** and state ∈ {2, 3}: turn in place —
   `heading = current motion heading`, call `0x14031BFE0(char, 1)`.
5. **State 4 (airborne):** `heading = atan2(dx, dy)`, `Vxy = waterline << 4`.
6. **State 0x1C:** set `state = 4` (jump) and return.
7. **Otherwise (ground):** `heading = atan2(dx, dy)`; if the cell has the
   road/path flag, adjust for the packed cell slope angle
   (`(cell >> 1) & 7 << 8 >> 3`, same encoding as the gravity research) and run
   the **turn interpolation** (see §3.5).
8. Writes: `+0x174=0`, `+0x210=0`, `+0xC04=0`, **`+0x1F4 = 2`**,
   `+0x204=1`, destination `+0x2A0/2A4/2A8`, `Vz = +0x270`, `Vxy = +0x2F8`,
   and `+0x26C = heading` / `+0x268 = Vxy_fixed` when not already aligned.

### 3.3 `KCharacter::RunTo` (`0x14031B540`) — state 3

Same structure as `WalkTo`; differences:

- sets **`+0x1F4 = 3`** (run);
- default speed when arg 0 = `height` (`+0x2FC`, later clamped);
- the turn-angle speed penalty is explicit (§3.5).

### 3.4 `KCharacter::MoveTo` (`0x140314230`) — position/destination set

- Validates `nX >= 0`, `nY >= 0`, scene/region pointers.
- Converts world → region/cell coordinates (`>>5`, `>>6`; local offsets masked
  `0x7FF`), resolves the destination cell (`0x1401806B0`), runs the path/valid
  check (`0x1403D65C0`), switches region/cell if needed, then writes
  `+0x10/+0x14` (x/y), `+0x18` (z = cell height<<6 + offset),
  `+0x2C/+0x30` (local cell coords).
- This is the function every sync handler calls after applying a server packet
  (and what `KGSO3WorldClientInterface::MoveCharacter` wraps).

### 3.5 Turning math — what happens when the character turns

RunTo (`0x14031B780`), applied every frame while moving:

```asm
delta = heading - facing            ; +0x26C - +0x44, 0..255 circle
if delta >  0x80: delta = 0x100 - delta
if delta < -0x80: delta += 0x100
if |delta| > 0x50:                  ; > 80/256 = 112.5°
    speed      >>= 1                ; halve movement speed this frame
    turn_step  >>= 1
```

So the client model is:

- **heading** (`+0x26C`) = direction of travel, recomputed as `atan2(dx,dy)` on
  each new destination and from the velocity vector in
  `ProcessAcceleration` (see `JX3_GRAVITY_RESEARCH.md` §3.2);
- **facing** (`+0x44`) = body yaw, interpolated toward heading with the per-frame
  step `+0x48`;
- a large turn (> 112.5°) costs half the movement speed for that frame
  (turn-in-motion penalty);
- WalkTo uses the same machinery with a `0x40` (90°) reference in its slope
  branch.

`KCharacter::LuaTurnTo` (`0x1403E0F60`) reads a Lua angle and calls
`0x14031E7C0(char, angle, 1)`, then updates the display (`0x140380FB0`) and marks
a dirty flag (`g+0x1B5CC = 1`).

`KCharacter::LuaTurnToCharacter` (`0x1403E1080`) computes
`atan2(targetX - x, targetY - y)`, normalizes to `0..255`, writes it to
`+0x44` **directly** (snap facing), and when `state == 8` (swim) also zeroes
velocity and the destination and calls `0x14031BFE0(char, 1)`.

`KPlayerClient::OnSetTurnRange` (`0x14014A860`) stores four dwords from the
packet into `+0x384`, `+0x380`, `+0x378`, `+0x37C` — the server-configured
turn range (used by skill auto-facing).

---

## 4. Display-side turning & animation (`JX3RepresentX64.dll`)

### 4.1 `KRLLocalCharacter::UpdateDirection` (`0x180533D40`)

1. Reads **`KeepTurningFrame`** (int) and **`TurningEpsilon`** (float) from the
   `tabCGAni` resource param (`0x1800110A9` lookup, getters `+0x40` / `+0x50`).
2. Branches on the move state (`0x18001D8D1`):
   - state `0x21`: special path via `0x18000A420`;
   - otherwise, if the character is turning, calls
     `0x18000CEC8(char, targetYaw, targetPitch, KeepTurningFrame, TurningEpsilon)`;
   - states `{1, 28, 32}` (mask `0x1100000002`) use the **immediate** path
     `0x180016617` (snap, no interpolation).
3. Writes the resulting yaw to `+0x6C` and applies it (`0x180020BB7`).

### 4.2 Immediate / rush direction

- `KRLRushState::UpdateImmediateDirection` (`0x1804BF340`): reads the direction
  from `m_pFrameData` (`+0xA0`), converts it, builds a vector/quaternion and
  applies it to `m_prlActor` (`+0x80`) — used to snap the model for scripted
  moves.
- `KRLRemoteCharacter::UpdateDirection` (`0x180543CF0`): interpolates the
  server-synced heading for other players.
- `KRLRushState::GetFaceDirection` (`0x1804B4220`) / `UpdateImmediateFootDirection`
  (`0x1804BF4A0`) handle the body/foot split.

### 4.3 Locomotion animation selection

`KRLRushState::UpdateMoveAnimation` (`0x1804C0700`) chooses the clip and speed
from a resource-param struct by comparing the character speed (`+0xD0`) against
per-clip thresholds (`[param+0x50]`, `[param+0x68]`), writing the animation ID
to `+0x30` and speed to `+0x34`, and feeds two animation slots (`+0x1B8`,
`+0x1C0`).

F1 animation kinds (`samples/player/catalog/player_animation_f1.txt`):

| KindID | Animation | Notes |
|---|---|---|
| 1 | 普通待机 | idle |
| 2 | 战斗待机 | combat idle |
| 5 | 奔跑 (`F1b02yd奔跑`) | run |
| 6 | 挪步 (`F1b02yd挪步左/右`) | strafe/shuffle step |
| 56 | 行走 (`F1b02yd行走`) | walk |
| 57 | 后退01 (`F1b02yd后退01`) | backpedal |
| 58 | 后退02 | backpedal variant |
| 16/17/18 | 小跳a/b/c | jump 1 |
| 19 | 二段跳a | double jump |

Animation table columns also control facing/head behaviour:
`是否禁止自动转头` (disable auto head-turn), `IsLookAtCamera` (0 face forward,
1 follow focus, 2 ignore), `PoseState`, `锁定朝向` (lock facing).

### 4.4 Turn clips

| Context | Clip source | Evidence |
|---|---|---|
| Fly / 轻功 | `TurnLeftAnimation` / `TurnRightAnimation` per body (F1 `F1bqg…bR/bL.tani`), `TurningEpsilon=0.02`, `KeepTurningFrame=30` | `proof/gravity/player_flyjump.krl.txt` |
| Skill rush | `player_rush_skill.txt` rows 478/479 = `Lturn`/`Rturn` per body | extracted table |
| Air turn | `F1bqg空中转身衔接01.tani` | F1 animation table |
| Normal ground | **no dedicated turn clip found** — model yaw rotates while the walk/run/strafe/back clip plays | negative result over KindID map + clip list |

Represent move-state vocabulary (`0x00CBCF08…`, names as state identifiers):
`STAND, TURN_LEFT, TURN_RIGHT, WALK, SWIM, SWIM_JUMP, SWIM_DOUBLE_JUMP, FLOAT,
FLY_FLOAT, FLY_JUMP, FLY_JUMP_TURN_LEFT/RIGHT, JUMP, DOUBLE_JUMP, SIT_DOWN,
DEATH, FALL_DEATH, DASH, KNOCK_DOWN/BACK/OFF, SPRINT_BREAK, HALT, FREEZE,
ENTRAP, AUTOFLY, PULL, REPULSED, RISE, SKID, WALL_JUMP, SPRINT_DASH/KICK/FLASH,
SKILL_MOVE_SRC/DST/DEATH, SUSPEND(+TURN_LEFT/RIGHT), SUMMIT, BIRD_FLY/FLOAT/JUMP,
POSE_STATE, MANNED_SPACE, RUSH, HAND_LINK, SLOT_LINK, TERRAIN, WATER, …`.

---

## 5. Movement state machine (ground subset)

```
STAND/IDLE (1?) --(MoveForwardStart/W)--> WALK (2) --(ToggleRun/run speed)--> RUN (3)
WALK/RUN --(destination reached)--> turn-in-place (0x14031BFE0) --> STAND
WALK/RUN --(Jump)--> JUMP (4) --(land)--> STAND
WALK/RUN --(water)--> SWIM (7)
any --(sprint dash skill)--> SPRINT_DASH (0x17)
any --(server sync state)--> KNOCK_*/FREEZE/ENTRAP/...
```

- Ground move states accepted by WalkTo/RunTo: **{1, 2, 3, 4, 8, 0x1C}**
  (mask `0x1000011E`).
- `2` = walk, `3` = run (verified by the `+0x1F4` writes).
- `4` = jump, `7` = swim (from the gravity research), `0x17` = sprint dash.
- `0x1A…0x1D` have a distinct `OnSyncMoveState` payload branch (state flags).
- `0x21` = fly-jump end state (gravity research §3.8).

---

## 6. Netcode — movement replication

Protocol header is 7 bytes (`[0]` word type, then size/id fields); handlers read
fields at `+7` onward (`KPlayerClient::On*` signature `(this, packet)`).

### 6.1 Client → server

**`KPlayerClient::DoMoveCtrl` (`0x140130130`)** — packet type **7**, size **0x31
(49 B)** sent via `0x140169840`:

| offset | size | source |
|---|---|---|
| +0x00 | word | `7` (type) |
| +0x0B | dword | `[arg+8]` |
| +0x0F | byte | arg flag (`r14d != 0`) |
| +0x10 | byte | arg flag (`r15d != 0`) |
| +0x11 | byte | arg 5 |
| +0x12 | byte | arg 6 |
| +0x13 | byte | arg 7 |
| +0x14 | dword | `[clientPlayer+0xF9C]` sequence (pre-incremented) |
| +0x18 | byte | move state `[char+0x1F4]` |
| +0x19 | dword | height `[char+0x2FC]` |
| +0x1D | dword | gravity `[char+0x320]` |
| +0x21 | dword | `[char+0x340]` |
| +0x25 | dword | x `[char+0x10]` |
| +0x29 | dword | y `[char+0x14]` |
| +0x2D | dword | z `[char+0x18]` |

**`KPlayerClient::DoSyncDirection` (`0x140135FC0`)** — packet type **0x13 (19)**,
size **0x2E (46 B)**:

| offset | size | source |
|---|---|---|
| +0x00 | word | `0x13` (type) |
| +0x0B | dword | `[arg+8]` |
| +0x0F | byte | arg |
| +0x10 | byte | arg |
| +0x11 | dword | sequence `[clientPlayer+0xF9C]` |
| +0x15 | byte | move state `[char+0x1F4]` |
| +0x16 | dword | height `[char+0x2FC]` |
| +0x1A | dword | gravity `[char+0x320]` |
| +0x1E | dword | `[char+0x340]` |
| +0x22 | dword | x |
| +0x26 | dword | y |
| +0x2A | dword | z |

Both are built from the **predicted** character state (client prediction) and
carry the sequence counter used by the server for reconciliation.

### 6.2 Server → client (apply functions)

**`KPlayerClient::OnMoveCharacter` (`0x1401460F0`)** — full state snapshot:

| packet | → character | meaning |
|---|---|---|
| +0x07 dword | id | resolve character (bit 30 = player) |
| +0x0B byte | `+0x44` | facing angle |
| +0x0C/0x10/0x14 dword | `MoveTo(x,y,z)` | position |
| +0x18 byte | `+0x26C` | motion heading |
| +0x19 word | `+0x268` | VelocityXY ×16 |
| +0x1B word (signed) | `+0x270` | VelocityZ |
| +0x1D word | `+0x2F8` | VelocityXY |
| +0x1F byte | `0x14031BFE0(char, byte)` | direction/turn flag |
| +0x20 byte | MoveTo arg | move flag |

**`KPlayerClient::OnSyncMoveState` (`0x140158C50`)**:

| packet | → character | meaning |
|---|---|---|
| +0x07 dword | id | character |
| +0x0B byte | `+0x44` | facing |
| +0x0C byte | `+0x48` | **turn rate** |
| +0x0D byte | `+0xC08` | frame counter |
| +0x0E dword | state-dependent (`+0x3BC` / `+0x2B0`,`+0x2AC`,`+0x2CC`) | state payload |
| +0x12 qword bits 58–63 | move state | 6-bit state |
| +0x2B qword bits 0–17 / 18–35 / 36–59 | `+0x2A0/2A4/2A8` | destination x/y/z |
| +0x33 byte | `+0x2B4` | flag |
| (tail) | `MoveTo` | position apply |

**`KPlayerClient::OnSyncMoveCtrl` (`0x140158870`)**:

| packet | → character |
|---|---|
| +0x07 dword | id |
| +0x0B byte | `+0xC08` frame counter |
| +0x0C byte | `+0x44` facing |
| +0x0D byte | `+0x48` turn rate |
| +0x0E / +0x16 / +0x1F | vectors (`0x14016AAA0` / `0x14016AD70` / `0x14016ABE0`) |
| (tail) | `MoveTo` |

**`KPlayerClient::OnSyncMoveParam` (`0x140158A30`)**:

| packet | → character |
|---|---|
| +0x07 dword | id |
| +0x0B byte | `+0x44` facing |
| +0x0C byte | `+0x48` turn rate |
| +0x0D byte | `+0xC08` counter |
| +0x0E / +0x17 | vectors |
| +0x27 u16 bit0..6 | `+0x1F8`, `+0x208`, `+0x230`, `+0x200`, `+0x174`, `+0x2CC`, `+0x2F4` |
| +0x27 bits 7–9 | `+0x22C` (3-bit value) |
| +0x29 qword bits 0–17/18–35/36–59 | destination x/y/z |
| (tail) | `MoveTo` |

**`KPlayerClient::OnSetTurnRange` (`0x14014A860`)**:
`+0x07 → +0x384`, `+0x0B → +0x380`, `+0x0F → +0x378`, `+0x13 → +0x37C`.

Other located handlers: `OnAdjustPlayerMove` (`0x14013AA40`),
`OnSyncRunSpeedLimit` (`0x14015FE30`) — not decoded field-by-field yet.

### 6.3 Authority model

- Movement is **server-authoritative**: the client predicts locally
  (`DoMoveCtrl`/`DoSyncDirection` with a sequence counter) and the server
  broadcasts `OnMoveCharacter`/`OnSync*` which call `KCharacter::MoveTo` and set
  the exact fields.
- `MoveCharacter`/`MoveCtrl` (`KGSO3WorldClientInterface::MoveCharacter`
  `0x14018BBC0`, `MoveCtrl` `0x14018BE00`) are the world-level wrappers around
  `KCharacter::MoveTo`; `TurnTo` (`0x14018C5F0`) wraps the facing set.
- For reborn we do not need JX3's exact bytes/IDs; the field map above is enough
  to recreate the model on our own opcode plan
  (see `reborn-netcode/docs/netcode/REBORN_SERVER_SPEC.md`).

---

## 7. Camera coupling

The camera follows turning while moving (from
`reborn-netcode/docs/netcode/REBORN_CAMERA_SPEC.md`):

| Parameter | Behaviour |
|---|---|
| `CameraAdjustYawWhenMoveTurn` | yaw-follow amount when the character turns while moving |
| `CameraAdjustYawWhenMoveTurnDisableAngle` | dead zone, default **0.26 rad (15°)** |
| `CameraFollowMode` | `1` (follow) |
| `CameraSpeedRatio=1.5625`, `CameraResetSpeed=1`, `CameraPitchSpeed=0.001` | camera motion (`number.krl.txt`) |

Turn while moving: if the turn angle exceeds the 15° dead zone, camera yaw is
dragged toward the character yaw; otherwise the camera stays put and the
character turns within the dead zone.

---

## 8. Constants and units

| Constant | Value | Source |
|---|---|---|
| Logic tick | 66.7 ms (15 Hz) | gravity research |
| Length unit | 1 m = 192 units | gravity research |
| Walk speed | `CharacterWalkSpeed=6` 尺/s ≈ 2.0 m/s | `number.krl.txt:20` |
| Run speed | `CharacterRunSpeed=20` 尺/s ≈ 6.67 m/s | `number.krl.txt:21` |
| Ride walk/run | 8 / 40 尺/s | `number.krl.txt:25-26` |
| **Yaw turn speed** | `CharacterYawTurnSpeed=0.007465` | `number.krl.txt:23` |
| Yaw turn reset | `CharacterYawTurnResetSpeed=0.0023` | `number.krl.txt:24` |
| Ride yaw turn / reset | `0.003465` / `0.0023` | `number.krl.txt:27-28` |
| Vehicle yaw turn | `0.006283` | `number.krl.txt:29` |
| Flight roll reset | `0.003` | `number.krl.txt:86` |
| Skill turning | `SkillTurningToTargetSpeed=0.01`, `SkillTurningToLogicSpeed=0.01`, `SkillTurningTime=1500` | `number.krl.txt:142-144` |
| Turning epsilon / keep frames | `TurningEpsilon=0.02`, `KeepTurningFrame=30` | `player_flyjump.krl.txt:1-3` |
| Turn-angle speed penalty | > `0x50`/`0x100` = 112.5° → speed halved | RunTo `0x14031B7A9` |
| Turn range (server) | 4 dwords | `OnSetTurnRange` |

CommonNumber struct layout (from the `sLoadNumberFromFile` loader
`0x180857DF0`; loaded at `[g+0x24C14]`):

| offset | key |
|---|---|
| +0x4C | `CharacterWalkSpeed` |
| +0x50 | `CharacterRunSpeed` |
| +0x54 | `CharacterSwimSpeed` |
| **+0x58** | **`CharacterYawTurnSpeed`** |
| +0x5C | `CharacterYawTurnResetSpeed` |
| +0x60/+0x64 | `CharacterRideWalkSpeed` / `CharacterRideRunSpeed` |
| +0x68/+0x6C | `CharacterRideYawTurnSpeed` / `CharacterRideYawResetSpeed` |
| +0x70 | `CharacterVehicleYawTurnSpeed` |

**Open:** the code consumer of `+0x58` was not located (the struct is reachable
only through the runtime singleton pointer at `0x180EDDFE0 + 0x24C14`). The
logic-side turn step (`+0x48`) is delivered by the server sync byte; the display
side uses `tabCGAni` `KeepTurningFrame`/`TurningEpsilon`. The `number.krl` yaw
speeds are most likely the default/server-independent turn rate source and are
documented here for the host.

---

## 9. Host implementation status & gaps

`engine_host_spike/MapSpike.cs` player mode currently has:

- WASD camera-relative movement, Shift run, Space jump
  (`MapSpike.cs:225-290`, `:901-1012`);
- gravity/jump tuned to the real values (`pGravity=-1289`, `pJumpV=703`);
- terrain height sampling, ledge detection, foliage collision, follow camera.

Missing vs the real client:

1. **No facing/turn model** — the character model does not rotate toward the
   movement direction; there is no yaw interpolation, no turn-rate penalty.
2. **No walk/run/strafe/back animation selection** (kind map in §4.3).
3. **No turn clips** (fly/suspend/rush) and no `KeepTurningFrame`/`TurningEpsilon`
   blend.
4. **No input mapping fidelity** — the real defaults are W/S move, A/D strafe,
   ←/→ turn, Numpad `/` run toggle, G auto-run (§2.3).
5. **No netcode** — no `DoMoveCtrl`/`DoSyncDirection` sending or `OnMoveCharacter`
   apply (single-player host does not need it, but the model is documented).

Implementation-ready pseudocode (ground):

```python
# per logic frame (15 Hz), after input is committed
move = camera_relative(W, S, A, D, left, right)   # forward/strafe/turn axes
if turning_left/right: facing += turn_step        # ←/→ turn in place
if move.any():
    heading = atan2(move.x, move.z)               # +0x26C
    speed   = run_speed if run_toggle else walk_speed
    delta   = wrap255(heading - facing)           # +0x44
    if abs(delta) > 0x50: speed >>= 1             # turn penalty
    facing  = approach(facing, heading, turn_step)# +0x48
    velocity = dir(heading) * speed               # +0x2F8 / +0x268
else:
    velocity = 0
# animation: choose KindID by speed
#   0      -> idle(1) / combat idle(2)
#   walk   -> 56
#   run    -> 5
#   strafe -> 6
#   back   -> 57/58
```

---

## 10. Open items

1. **Yaw-turn-speed consumer** (`CommonNumber +0x58`) — not located; likely read
   through the runtime singleton. Low risk for the host (we can use the table
   value directly).
2. **Packed UI Lua** definition of `MoveForwardStart`/`TurnLeftStart` etc. —
   located only as binding names; the engine commit function
   (`CommitInput`) is known.
3. `OnAdjustPlayerMove` / `OnSyncRunSpeedLimit` field layouts not decoded.
4. Ground **turn-in-place clip** — no dedicated clip found; confirm whether the
   client plays `挪步` or just rotates the model for small turns.
5. Head look-at (`是否禁止自动转头`, `IsLookAtCamera`) application not decoded.
6. `CharacterVehicleYawTurnSpeed` / ride turn input (`GetTurnInputYaw`) not
   traced end-to-end.
7. Server fall-damage / knockback remain server-side (see jump/fall spec).

---

## 11. Evidence index

| Path | Content |
|---|---|
| `proof/movement/disasm/client_movement_symbols.txt` | WalkTo/RunTo/MoveTo/TurnTo + all move sync handlers |
| `proof/movement/disasm/represent_movement_symbols.txt` | UpdateDirection/UpdateMoveAnimation/CommitInput/… |
| `proof/movement/disasm/kgui_hotkey.txt` | `UI::KHotkeyMgr` loaders/defaults |
| `proof/movement/disasm/kgui_hotkey_bits.txt` | key modifier bit decoding (bCtrl/bShift/bAlt) |
| `proof/movement/disasm/kgui_filepath.txt` | `UI::KFilePathMgr::Init` reading `\ui\filepath.txt` |
| `proof/movement/extracted/ui_hotkey_default.txt` | **real default keybindings** |
| `proof/movement/extracted/ui_hotkey_bindings.ini` | command → Lua down/up definitions |
| `proof/movement/extracted/ui_filepath.txt` | hotkey file path config |
| `proof/movement/KGUIX64_strings.txt`, `KG3DEngineX64_strings.txt`, `KBaseX64_strings.txt` | symbol/string dumps |
| `proof/gravity/number.krl.txt` | speeds + yaw turn constants |
| `proof/gravity/player_flyjump.krl.txt` | `TurningEpsilon`, `KeepTurningFrame`, turn clips |
| `proof/gravity/disasm/process_acceleration.txt` | heading recompute (`atan2`), velocity clamps |
| `docs/JX3_GRAVITY_RESEARCH.md` | gravity/jump/fall model |
| `docs/REBORN_JUMP_FALL_SPEC.md` | implementation spec for jump/fall |
| `samples/player/catalog/player_animation_f1.txt` | F1 animation table (KindID map) |
| `reborn-netcode/docs/netcode/REBORN_CAMERA_SPEC.md` | camera turn coupling |
| `tools/movement/extract_ui_filepath.py` | extracts hotkey tables from PakV4 |
| `tools/movement/find_xrefs.py` | call/field xref helper |
| `tools/movement/verify_input_map.py` | numeric check of the decoded key map + constants |
| `tools/netcode/xref_string.py` | multi-needle string xref + disasm |

### Numeric verification

`tools/movement/verify_input_map.py` re-decodes `ui_hotkey_default.txt` and
prints the movement key map (W/Up, S/Down, Left, Right, A, D, Space,
Numpad/, G/NumLock, Ctrl+G, Ctrl+U, F11, Alt+W/A/S/D) plus the state mask bits
`{1,2,3,4,8,28}` and the 112.5° turn threshold — all matching §2.3, §3.2, §3.5.
