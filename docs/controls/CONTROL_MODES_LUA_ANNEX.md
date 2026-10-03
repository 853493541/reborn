# CLASSICAL/JOYSTICK Lua annex — decoded handler bodies (P1)

**Scope:** instruction-level decode of the shipped packed UI scripts
(Lua 5.1 `LuaQ`, `size_t=4`). Part of the full control-modes decode
(`CONTROL_MODES_TRACEABILITY.md`, phase P1). Every entry cites the proto path
and the committed disassembly dump.

**Source scripts:** extracted from the game client paks
(`C:\SeasunGame\Game\JX3\bin\zhcn_hd`, 1.5.0.9975) with the official
`PakV4SfxExtract.exe` via `pss_assets.run_pakv4` into
`%TEMP%\opencode\ui_ex\` (read-only research; raw scripts stay in temp).

**Tools:** `tools/controls/lua_index.py` (batch proto/name/global index),
`tools/controls/lua51_probe.py` (instruction-level body dump).

**Branch convention (re-derived 2026-10-02, used throughout):** for
`TEST/TESTSET A … C` and `EQ/LT/LE A …`, the following `JMP` is taken iff the
condition equals `A` (`TEST C=0`: jump when the value is falsy; `EQ A=0`: jump
when not equal); for `TEST C=1`: jump when truthy. Settled against proto 0/63,
`0/31` and `0/76` where the source intent is known.

**Dumps (committed):** `proof/controls/lua_dump/*.txt`;
indexes `proof/controls/lua_index_control.txt` (+ `_all.txt`, all 1549 scripts).

---

## A1. Control routing wrapper — `hotkeys.lua` proto `0/61` (L679-685) HIGH

```lua
local function SetControl(control, flag)        -- name assigned by caller
    if GetOperationMode() == CLASSICAL_MODE then
        Camera_EnableControl(control, flag)
    else
        Scene_EnableFreeMoveControl(control, flag)
    end
end
```

No other mode logic. Confirms §7e of `OPERATION_MODES_PLAN.md`: CLASSICAL
routes to the camera control store, JOYSTICK to the free-move control store.

## A2. `ResponseWASDKey(key, down, isDouble)` — `hotkeys.lua` proto `0/46` (L488-565) HIGH

The joystick movement-vector builder. Called by the joystick branch of the
movement handlers with the raw control name (`'StrafeLeft'`, `'StrafeRight'`,
`'TurnLeft'`, `'TurnRight'`, `'Forward'`, `'Backward'`), the key state and the
double-tap flag.

```lua
-- upvalue keyState = { [name] = pressed counter }
keyState[key] = math.max(keyState[key] + (down and 1.0 or -1.0), 0.0)

local p = GetClientPlayer()
local onTower = p and p.bOnTowerFlag          -- (tower/bird/horse guards)
local handled = false

-- displacement (轻功) hotkeys get the key name with Turn/Strafe stripped:
local dirName = string.upper(string.gsub(string.gsub(key, 'Turn', ''), 'Strafe', ''))
if ResponseDisplacementHotkey(dirName, down, isDouble) then handled = true end

-- analog vector from ALL held keys (A/D and arrows share the axes):
local rx = (keyState.TurnRight + keyState.StrafeRight)
         - (keyState.TurnLeft  + keyState.StrafeLeft)
local ry = keyState.Forward - keyState.Backward

local move = 'MOVE_STOP'
if     rx == 0 and ry ==  0 then move = 'MOVE_STOP'
elseif rx == 0 and ry ==  1 then move = 'MOVE_FORWARD'
elseif rx == 1 and ry ==  1 then move = 'MOVE_RIGHTFORWARD'
elseif rx == 1 and ry ==  0 then move = 'MOVE_RIGHT'
elseif rx == 1 and ry == -1 then move = 'MOVE_RIGHTBACKWARD'
elseif rx == 0 and ry == -1 then move = 'MOVE_BACKWARD'
elseif rx == -1 and ry == -1 then move = 'MOVE_LEFTBACKWARD'
elseif rx == -1 and ry ==  0 then move = 'MOVE_LEFT'
elseif rx == -1 and ry ==  1 then move = 'MOVE_LEFTFORWARD'
end

if ResponseDisplacementHotkey(move, down, isDouble) then handled = true end

-- double-tap sprint (key-down + isDouble required):
if not down then return handled end
if isDouble then
    -- on tower: only Forward may sprint; off tower: any movement key
    if onTower and key ~= 'Forward' then return handled end
    if p and (p.bBirdMove or p.bHoldHorse) then return handled end
    StartSprint()
end

return handled
```

(Symbolic execution of pc131-147; `onTower = p and p.bOnTowerFlag`.)

Decoded facts:

1. **Joystick axes combine turn and strafe keys** — arrows and A/D both feed
   the same lateral axis, exactly the 8-direction `MOVE_*` vocabulary the
   engine's animation system uses (`hotkeys.ResponseWASDKey` builds
   `MOVE_FORWARD/RIGHTFORWARD/...`). Pressing both left and right cancels to 0.
2. **Overflow = stop**: any combination that sums beyond −1/0/+1 per axis
   (e.g. `TurnRight + StrafeRight`) matches none of the branches and stays
   `MOVE_STOP`.
3. **Displacement routing**: the bare direction (`LEFT`, `RIGHT`, ...) and the
   `MOVE_*` state are both offered to `ResponseDisplacementHotkey` (轻功/位移
   hotkeys), which owns any direction-bound skill.
4. **Double-tap = sprint** (`StartSprint`) on key-down with `isDouble`, blocked
   by bird/hold-horse; on tower only the `Forward` key may sprint, off tower any
   of the six movement keys may.
5. `handled` (true when a displacement hotkey consumed the event) is returned
   to the movement handler (see the pending 76/78 verification).

## A3. Operation-mode apply — `OperationModeBase.lua` proto `0/5` (L185-209) HIGH (one open)

```lua
-- upvalue: current mode
local function ApplyOperationMode(mode)
    if mode == CLASSICAL_MODE and IsMobileKungfu() then return end
    ClearMoveState()
    currentMode = mode
    UploadOperationMode(mode)
    if StorageServer:GetData('CurrentOperationMode') ~= mode then
        UploadOperationModeData(mode, 1.0)
    end
    if mode == CLASSICAL_MODE then
        Camera_SetResetSpeed(1.0)
        UseFullAngle(false)
        StorageServer:SetData('CurrentOperationMode', CLASSICAL_MODE)
        Scene_LockMouseRotation(false)
    else -- JOYSTICK_MODE
        UseFullAngle(true)
        Scene_LockMouseRotation(true)
        StorageServer:SetData('CurrentOperationMode', JOYSTICK_MODE)
    end
    FireUIEvent('CHANGE_OPERATION_MODE', currentMode)
end
```

Decoded facts:

- **Persisted mode key found:** `StorageServer` data key
  `'CurrentOperationMode'` (value = the mode constant) — resolves the
  "persisted operation-mode key unrecovered" item (G8, storage-server side;
  `custom.dat` mapping still to confirm).
- **CLASSICAL**: `Camera_SetResetSpeed(1.0)` (single arg — different from the
  3.5/3.75 pair documented at the drag-release site), `UseFullAngle(false)`,
  `Scene_LockMouseRotation(false)`.
- **JOYSTICK**: `UseFullAngle(true)`, `Scene_LockMouseRotation(true)`; no reset
  speed call here.
- **Gate (corrected by re-deriving EQ/TEST):** `if mode == CLASSICAL_MODE and
  IsMobileKungfu() then return end` — joystick applies unconditionally; classical
  is refused on mobile-kungfu clients (which A4 forces to JOYSTICK), desktop
  classical applies. What `IsMobileKungfu` is on desktop builds stays open
  (implementation pending; do not assume "mobile-only" yet).

## A8. Mode constants, getter, and the control-disabled flag (P2) HIGH

**Constants are Lua, not C.** `OperationModeBase.lua` chunk (proto 0) pc0-3:

```lua
CLASSICAL_MODE = 0.0
JOYSTICK_MODE  = 1.0
```

`GetOperationMode` is a closure over the shared current-mode upvalue
(`CLOSURE proto 0/0; SETGLOBAL 'GetOperationMode'`, chunk pc97-102); the initial
value is CLASSICAL (0), and `SetOperationMode` (`0/5`) writes the same upvalue
(`SETUPVAL`). So all mode checks are plain Lua number comparisons.

Consequences (with the VM rule confirmed — jump iff comparison == A):

| Site | CLASSICAL (0) | JOYSTICK (1) |
|---|---|---|
| `hotkeys 0/61` wrapper (`EQ A=0`) | `Camera_EnableControl(control,flag)` | `Scene_EnableFreeMoveControl(control,flag)` |
| `hotkeys 0/76/78` strafe handlers (`EQ A=1`) | `ResponseWASDKey('StrafeLeft/Right',…)` + `Camera_EnableControl(CONTROL_STRAFE_*)` fallback | OB wrapper + free-view `TurnLeftStart/RightStart` |
| `Scene 0/25` both-buttons autorun (`EQ A=1`) | returns | `FreeMoveControl(CONTROL_FORWARD, true/false)` |

**Control-disabled flag:** `mainscene 0/2 Camera_IsClientControlDisabled` is a
getter over a flag slot; `CameraStatus_Set` (`0/3`) writes it at pc404-409:
`flag = (params.dis_ctrl == 1)` and then calls its `CameraStatus_Animation`
upvalue with `mode ~= 'god camera'` (the free-view flag read by
`Camera_IsInFreeView`, `0/1`). `hotkeys 0/62 ClientControlEnabled()` = that
flag, gating the double-tap branch in `ResponseWASDKey` call sites.

`CameraStatus_Set` camera-param table keys (for P4 camera work): `mode`
('local camera' | 'remote camera' | 'god camera' | 'delay camera'),
`dis_ctrl`, `localdis_ctrl`, `maxheight` (default 5000), `movespeed` (15),
`Limit`, `limitx/y/z`, `lock`, `height`, `fix_camera`, `lock_zoom`,
`shortpath`, `x/y/z`, `scale`, `yaw`, `pitch` (clamped ±π), `offsetx/y/z`,
`offsetangle`, `tick`, `remoteid`; drives `rlcmd` commands
(`ob -camera params %f %f %f %f`, `set local camera mode %d %d`,
`set remote camera mode …`, `set god camera mode …`, `enable fix camera %d`,
`disable camera zoom %d`).

## A9. Joystick free-move vector (`OperationModeBase.lua` 0/9, 0/10, 0/16) HIGH

The joystick movement is a **discrete direction vector** maintained in Lua:

- `AddDirectionVector(dx, dy)` (0/9): `vector.nX += dx; vector.nY += dy`.
- `FreeMoveControl(control, down)` (0/16, the free-move control handler):

```lua
if control == CONTROL_FORWARD      then edge: AddDirectionVector(0, +1) end
if control == CONTROL_BACKWARD     then edge: AddDirectionVector(0, -1) end
if control == CONTROL_TURN_LEFT    then edge: AddDirectionVector(-1, 0) end
if control == CONTROL_TURN_RIGHT   then edge: AddDirectionVector(+1, 0) end
-- (each latched once per press; release adds the inverse)
hasVector = (vector.nX ~= 0 or vector.nY ~= 0)
Camera_EnableControl(freeMoveControlId, hasVector)
```

- `SetPlayerRotation` (0/10): `Camera_GetRTParams` -> `ConvertYawToDirection` -> byte-angle
  arithmetic (<=255, +128 when `bReverseMove`, -1 when the base is 128) -> `TurnTo(dir)`.
- `RotatePlayer` (0/14), `SetDirectionVector` (0/15), `FastArcTan` (0/13, the
  precomputed atan table): consume the vector for the auto-face/travel.

**Consequence (corrected model):** in JOYSTICK the **turn controls are the
lateral axis**: A/D (STRAFE-bound -> strafe handler -> `TurnLeftStart` ->
wrapper -> `Scene_EnableFreeMoveControl(CONTROL_TURN_*)`) and the arrow keys
(TURN-bound) both add to `nX`; W/S add to `nY`. There is **no in-place
keyboard rotation** - the body auto-faces the travel (`TurnTo`), and the
camera is mouse-only. So joystick A/D = lateral run with auto-face, not a
turn-in-place (this corrects the earlier A6.2 reading of the free-view
`TurnLeftStart` branch).

Host mapping applied: joystick turn controls fold into the strafe axis
(`strafeL/R = ... || pTurnL/R`), `rotAxis = 0`, and the turn block never runs
in joystick (mouse owns the camera).

## A10. Sprint input (`hotkeys.lua` 0/36, 0/164, 0/165, 0/166) HIGH

- `IsKeyDoubleDown` (0/36): `Hotkey.IsKeyDoubleDown() and
  Hotkey.GetKeyTimeInterval() < 250.0` — **the double-tap window is 250 ms**.
- `StartSprint` (0/164): if the player exists and not `bIgnoreGravity`:
  set the sprint flag; if `dwJumpType ~= SCHOOL_TYPE.GAI_BANG` cast
  `OnUseSkill(6754, 6754*5)`; `player:Sprint(true)`.
- `CheckEndSprint(force)` (0/165): if the flag -> `EndSprint(force)`; clear it.
- `EndSprint(force)` (0/166): `SprintGetSummitID()` -> `SetSprintTopPoint(x,y,z)`;
  `player:Sprint(false)`.
- Trigger site: `ResponseWASDKey` (A2) on key-down + `isDouble`, off-tower any
  of the six movement keys, on-tower only `Forward`; blocked while
  `bBirdMove`/`bHoldHorse`.

Host status (J3): the input side is ported exactly — per-command fresh-press
double-tap detection over the six movement commands with the decoded 250 ms
window, `StartSprint`/`EndSprint` logged, `sprint=` telemetry. The engine
`Sprint(true)` state and skill 6754 cast are **not modeled (open)**; no speed
was invented. Scripted check: `RC_SPRINT_TEST=1` (down 12000 / up 12100 /
down 12150 / up 12650) logs the expected Start/End pair.

## A11. `RotatePlayer` (0/14) — joystick turn + camera follow speed HIGH

`RotatePlayer` runs per frame in JOYSTICK (registered from the mode switch):

```lua
if not active then return end
vector.nX += (target.nX - vector.nX) * up_smooth      -- per-axis smoothing
vector.nY += (target.nY - vector.nY) * up_smooth
dir = 64                                              -- default = 90 deg
ax, ay = abs(nX), abs(nY)
if ay ~= 0 then dir = FastArcTan(ax / ay) end         -- 0/13 atan lookup
scale = player.bSprintFlag and up_sprintScale or 1.0
if up_fwdThresh < nY then
    Camera_SetResetSpeed(dir * scale * up_k)          -- <<< camera reset speed
else
    d2 = (nY < 0) and (128 - dir) or dir
    Camera_SetResetSpeed(d2 * scale * up_k)
end
-- quadrant fixups into a 0..255 byte heading:
if nX <= 0 and nY < 0 then dir = 128 - dir
elseif nX < 0 and nY < 0 then dir = 128 + dir
elseif nX < 0 and nY >= 0 then dir = 256 - dir end
if dir == 256 then dir = 0 end
SetPlayerRotation(dir)                               -- 0/10 -> TurnTo(dir)
```

Decoded consequences:

- **`Camera_SetResetSpeed` is the joystick camera-follow rate, written every
  frame** as a function of the movement direction (`dir * scale * k`), not a
  drag-release spring setting. This resolves the G5 ambiguity: the per-mode
  value applied at switch is the base; in joystick the Lua layer overrides it
  per frame (sprint flag scales it).
- The player heading is a **byte angle** (`FastArcTan` table, quadrilateral
  fixups) passed to `SetPlayerRotation`/`TurnTo`.
- The vector is **smoothed** toward the target with `up_smooth` (value from
  the chunk locals; still to extract) - this is the joystick input feel.

**Constants extracted from the chunk locals (pc0-28) — HIGH:**
| upvalue | value | role |
|---|---|---|
| `up_smooth` (R9) | **0.3** | per-call vector smoothing factor |
| `up_sprintScale` (R12) | **3.0** | reset-speed scale while `bSprintFlag` |
| `up_fwdThresh` (R10) | **10.0** | forward/reverse branch threshold on `nY` |
| `up_k` (R11) | **0.00125** | reset-speed factor (`dir * scale * 0.00125`) |
| vector/target | tables `{nX,nY}` | current vs target vector (0/16 writes target) |

Remaining: port the math to the host (facing byte-angle from `FastArcTan` +
the per-frame camera follow rate `Camera_SetResetSpeed(dir*scale*0.00125)`).

## A12. Camera follow mode (CAMERA_MODE) — full pipe + host gating HIGH

**Enum (game client `enum_ui.lua` chunk pc2977-2987):**
```lua
CAMERA_MODE = { NEVER_FOLLOW = 0, AUTO_FOLLOW = 1, ALWAYS_FOLLOW = 2 }
```

**Pipe:** `UISetting_Comprehensive:SetCameraMode(nCameraMode, announce)` (0/53)
stores `nCameraMode`, updates the settings checkboxes, then calls the native
`UI_Camera_SetParams_S(fDragSpeed, fMaxCameraDistance, ?, ?, nCameraMode)`.
The native camera Lua bindings live in the game client `JX3RepresentX64.dll`
(assert strings, `docs/controls/CONTROL_MODES_P4_PROBE.md`):
`SetCameraFollowMode` wrapper `0x1802EB1C0` (L179, nArgs==1) -> thunk
`0x18000D909` -> **node setter `0x180ACE3F0`** (clamp 0..3, field `+0x80`
classic / `+0x98` joystick per `[node+0x34]`). `SetCameraMaxDistance`
`0x1802EB220`, `SetCameraParams` `0x1802EB300`, `SetCameraPitch`
`0x1802EB3C0`, `SetCameraRTParams` `0x1802EB4E0` are the sibling bindings.

**Host gating (implemented):** per-mode values already come from
`custom.dat` (`nCameraModeInClassicMode/InJoystickMode`, applied on switch).
The client now gates the camera follow by the mode:
- `NEVER_FOLLOW (0)` - mouse-only (no follow).
- `AUTO_FOLLOW (1)` - follow on the move+turn row (classical); joystick
  follows the travel while moving (the decoded `RotatePlayer` per-frame
  `Camera_SetResetSpeed` behavior).
- `ALWAYS_FOLLOW (2)` - also follows a plain move.
Joystick follows the **travel direction**; classical follows the control frame.
`RC_FOLLOW_MODE=0|1|2` overrides for scripted tests.

Host interpretation register: AUTO/ALWAYS semantics come from the client's own
enum names plus the decoded joystick rate behavior; the exact native per-frame
consumer of the node field was not located (no direct reads). Re-open when the
consumer is traced.

Evidence runs: `reborn_20261002_173504.log` (joystick ALWAYS: A-alone camera
follows `dcam≈1.06`, TURNRIGHT `camd=1.57`, W+D `dcam=-1.05`);
`reborn_20261002_173637.log` (classical default `followMode=0`, all control
fingerprints unchanged).

## A4. Mode toggle — `OperationModeBase.lua` proto `0/19` (L463-488) MED/HIGH

```lua
if IsMobileKungfu() then
    ClearMoveState(); UseFullAngle(true); Scene_LockMouseRotation(true)
    FireUIEvent('CHANGE_OPERATION_MODE', JOYSTICK_MODE)
    pendingMode = JOYSTICK_MODE
    UISetting_Comprehensive:SetCameraMode(CAMERA_MODE.NEVER_FOLLOW, true)
    return
end
if pendingMode then
    SetOperationMode(pendingMode)          -- deferred apply
    pendingMode = nil
    local cm = (mode == CLASSICAL_MODE) and UISetting_Comprehensive.nCameraModeInClassicMode
                                           or UISetting_Comprehensive.nCameraModeInJoystickMode
    UISetting_Comprehensive:SetCameraMode(cm, true)
elseif param then
    SetOperationMode(pendingMode)
end
```

Confirms `SetCameraMode(nCameraModeIn<Mode>)` on switch (matches §7c plumbing)
and adds the mobile-kungfu shortcut that forces JOYSTICK + `NEVER_FOLLOW`.

## A5. Settings panel setter — `UISetting_Operation_Switch.lua` proto `0/12` (L144-151) HIGH

```lua
local function set(value)
    if value then
        SetOperationMode(CLASSICAL_MODE)
        UISetting_Comprehensive:SetCameraMode(UISetting_Comprehensive.nCameraModeInClassicMode)
    end
end
```

(The joystick counterpart setter lives in another proto of the same file —
next P1 item.)

## A6. Movement handlers — `hotkeys.lua` (HIGH)

Handler roster (proto index → guessed name from the chunk's `SETGLOBAL` pairs):
`0/65 MoveForwardStart`, `0/66 MoveForwardStop`, `0/67` (forward/back composite),
`0/71 TurnLeftStart`, `0/72 TurnLeftStop`, `0/73 TurnRightStart`①,
`0/74 TurnRightStop`②, `0/76 StrafeLeftStart`, `0/77 StrafeLeftStop`,
`0/78 StrafeRightStart`, `0/79 StrafeRightStop`.
① `SETGLOBAL 41 = TurnLeftStart` at pc538-542 actually binds proto 73; the
chunk interleaves captures — names per the index/dump pairs.

### A6.1 OB wrapper — proto `0/63` (L691-696) HIGH

```lua
local function SetControlInOB(control, flag)
    if IsPlayerInOBDungeon() then
        Camera_EnableControl(control, flag)
        return true
    end
    -- nil in normal play
end
```

### A6.2 `StrafeLeftStart` — proto `0/76` (L825-854) HIGH

```lua
-- upvalues RESOLVED from the chunk CLOSURE pseudo-instructions (pc547-551):
--   [0]=OB wrapper (0/63), [1]=false CONSTANT, [2]=closure 0/75 (CanStrafeMove)
function StrafeLeftStart()
    if GetOperationMode() == CLASSICAL_MODE then
        -- CLASSICAL block (pc19-62), see the mapping caveat below:
        if upval1 then                                  -- false in the shipped build:
            OnUseSkill(3801, 3801 * ((3801 % 10) + 1))  -- DEAD branch (flag off)
            return
        end
        if CanStrafeMove() then return end              -- 0/75 gate (A6.4)
        if IsKeyDoubleDown() then
            if not ResponseWASDKey('StrafeLeft', true, true) then
                Camera_EnableControl(CONTROL_STRAFE_LEFT, true)        -- fallback
            end
        else
            if not ResponseWASDKey('StrafeLeft', true, false) then
                Camera_EnableControl(CONTROL_STRAFE_LEFT, true)
            end
        end
    else
        -- JOYSTICK block (pc5-18):
        if SetControlInOB(CONTROL_STRAFE_LEFT, true) then return end   -- OB: set + done
        if Camera_IsInFreeView() then TurnLeftStart() end              -- else: turn
    end
end
```

`0/78 StrafeRightStart` mirrors it (`CONTROL_STRAFE_RIGHT`, `TurnRightStart`);
its upvalues are identical in kind (`[1]=false` constant, `[2]=0/75`).

**RESOLVED (P2, 2026-10-02) — see A8 for the constants.** With
`CLASSICAL_MODE = 0`, `JOYSTICK_MODE = 1` and the confirmed VM rule, the
mapping is: **CLASSICAL** (A/D are STRAFE-bound in `default.txt`) →
`ResponseWASDKey` + `Camera_EnableControl(CONTROL_STRAFE_*)` fallback (the
strafe habit); **JOYSTICK** → OB-wrapper + free-view `TurnLeftStart` (A/D
turn to face movement). The earlier docs' "classical = turn" belonged to the
joystick mode; the host now defaults to the CLASSICAL strafe behavior
(`RC_ADHABIT=turn` opts into the turn habit).

**Correction (2026-10-02):** the earlier doc correction that this free-view
`TurnLeftStart/RightStart` branch is OB-only is **inverted**. Direct bytecode:
the OB wrapper returns `true` only inside OB (short-circuit: set STRAFE, no
turn); in normal play it returns nil, so the code continues to
`Camera_IsInFreeView()` (true in normal play per `mainscene.lua` proto 0/1) and
calls `TurnLeftStart/RightStart`. Normal classical A/D = turn + STRAFE control
set; OB/spectator = strafe only. Supersedes the "Correction (later 2026-10-01)"
text in `CLASSIC_CONTROLS_AUDIT.md` §1 and `OPERATION_MODES_PLAN.md` §7b.

### A6.3 `MoveForwardStart` — proto `0/65` (L703-719) HIGH

```lua
-- upvalues RESOLVED (pc506-510): [0]=false CONSTANT, [1]=wrapper 0/61,
-- [2]=closure 0/62 (ClientControlEnabled)
function MoveForwardStart()
    local p = GetClientPlayer()
    if p then p.HoldW(1.0) end
    if upval0 then                                   -- false in the shipped build:
        OnUseSkill(3799, 3799 * ((3799 % 10) + 1))   -- DEAD branch (feature flag off)
    else
        upval1(CONTROL_FORWARD, true)                -- mode wrapper (0/61): both modes
        if ClientControlEnabled() then               -- 0/62: not Camera_IsClientControlDisabled()
            if IsKeyDoubleDown() then
                ResponseWASDKey('Forward', true, true)   -- double-tap (A2)
            else
                ResponseWASDKey('Forward', true, false)
            end
        else
            ResponseWASDKey('Forward', true, false)  -- no double-tap while control-disabled
        end
    end
end
```

Notes: `p.HoldW(1.0)` on the client player; the wrapper runs in **both** modes,
then the joystick vector builder runs too (its double-tap branch is gated by
`0/62`). Stop handlers call the wrapper with `false` and
`ResponseWASDKey(..., false, ...)`.

## A6.4 Gate closures (resolved upvalues)

```lua
-- 0/62 (upvalue=true captured)
function ClientControlEnabled()
    if upval then return true end              -- const true in this build
    return not Camera_IsClientControlDisabled()
end

-- 0/75 (no upvalues)
function CanStrafeMove()
    local p = GetClientPlayer()
    if not p then return false end
    if p.nMoveState == MOVE_STATE.ON_FLY_JUMP then return false end
    if p.bSprintFlag then return false end
    if p.bOnHorse then return false end
    return true
end
```

### A6.5 Handler roster (P1 complete) HIGH

All movement handlers share one pattern: **enable flag → OB wrapper → dead
skill flag (const false) → double-tap-aware `ResponseWASDKey` → mode wrapper
fallback**. Only the strafe pair branches on the operation mode (A6.2).

| Handler | Protos | Decoded body |
|---|---|---|
| `MoveForwardStart` | 0/65 | `p.HoldW(1.0)`; dead skill flag; `wrapper(CONTROL_FORWARD,true)`; `ResponseWASDKey('Forward',true,isDouble)` gated by `0/62` |
| `MoveForwardStop` | 0/66 | `p.HoldW(0.0)`; `ResponseWASDKey('Forward',false,false)`; if not handled → `CheckEndSprint()`; `wrapper(CONTROL_FORWARD,false)` |
| `MoveBackwardStart` | 0/67 | enable flag (true); `SetControlInOB(CONTROL_BACKWARD,true)` (OB short-circuit); dead skill flag; `ResponseWASDKey('Backward',true,isDouble)`; if not handled `wrapper(CONTROL_BACKWARD,true)`; `CheckEndSprint()` on the single-tap path |
| `MoveBackwardStop` | 0/68 | `wrapper(CONTROL_BACKWARD,false)` + the ResponseWASDKey/stop pattern |
| `TurnLeftStart/Stop` | 0/71/0/72 | enable flag (true); `ResponseWASDKey('TurnLeft',down,isDouble)`; if not handled `wrapper(CONTROL_TURN_LEFT,flag)` — **no mode branch** (wrapper routes) |
| `TurnRightStart/Stop` | 0/73/0/74 | mirror of TurnLeft |
| `StrafeLeftStart/Stop` | 0/76/0/77 | mode-branched (A6.2): CLASSICAL → `ResponseWASDKey('StrafeLeft',…)`/`Camera_EnableControl`; JOYSTICK → OB wrapper + free-view `TurnLeftStart/Stop` |
| `StrafeRightStart/Stop` | 0/78/0/79 | mirror of StrafeLeft |

Captured constants: `Hotkey_EnableTurnLeft/Right`, `Hotkey_EnableMoveBack`,
`Hotkey_EnableSprint` closures all capture `true`; the skill branches capture
`false` (disabled in this build). Wrapper `0/61` used by forward/turn/backward
handlers; OB wrapper `0/63` by strafe/backward.

## A7. Scene.lua drag / control handlers (G2 script side)

### A7.1 `Scene` update (L614-622) — proto `0/27` HIGH

```lua
if scene.bLDown and Hotkey_IsLMouseEnabled() then Camera_BeginDrag(1.0) end
if not scene.bLDown and Scene_IsInMorphCamera() then Camera_BeginDrag(1.0) end
if scene.bRDown and Hotkey_IsRMouseEnabled() then Camera_BeginDrag(2.0) end
```

(Order per bytecode: L block falls through to the morph check; R block is
independent.)

### A7.2 Control enable — proto `0/31` (L652-665) HIGH

```lua
Camera_EnableControl(CONTROL_CAMERA,
    scene.bLDown and Hotkey_IsLMouseEnabled() or Scene_IsInMorphCamera())
Camera_EnableControl(CONTROL_OBJECT_STICK_CAMERA,
    scene.bRDown and Hotkey_IsRMouseEnabled())
```

**G2 script answer:** LMB → `CONTROL_CAMERA` (with a morph-camera bypass),
RMB → `CONTROL_OBJECT_STICK_CAMERA`; no other control ids are set by the drag
path. Engine flag mapping (`+0x1AC`/`+0x1B0`) stays P3.

### A7.3 EndDrag — proto `0/36` (L741-747) HIGH

```lua
Camera_EndDrag(x, y, right and 1.0 or 2.0)   -- 1.0 = LMB drag, 2.0 = RMB drag
```

### A7.4 `Scene_LockMouseRotation` — proto `0/61` (L924-930) HIGH

```lua
if lock then rlcmd('lock input control mouse object rotation 1')
else        rlcmd('lock input control mouse object rotation 0') end
```

Joystick mode locks the mouse-object rotation through the engine console
command (called from `ApplyOperationMode`, annex A3).

### A7.5 Both-buttons autorun — proto `0/25` (L585-599) MED/HIGH

Per A8 the mode test means **JOYSTICK runs this** (classical returns early):
enables `FreeMoveControl(CONTROL_FORWARD, true)` when both mouse buttons are
down (plus an extra `upval0[CONTROL_FORWARD]` condition on the L-only path)
and clears it via the `bMoveStart` latch otherwise. Upvalue identity of that
extra condition stays open (low impact).

### A7.6 Autorun clear + persistence + stick camera HIGH

- `0/24` (per-frame): if `not bLDown and not bRDown and not Hotkey_IsAutoRun()`
  → `Camera_EnableControl(CONTROL_AUTO_RUN, false)`.
- `0/26`: control-setter helper; `CONTROL_FORWARD` is routed to the per-frame
  both-buttons function (upval1), all other controls to `FreeMoveControl`.
- `0/6 IsInStickCamera() = Hotkey_IsRMouseEnabled() and scene.bRDown`.
- `0/94/0/95 MoveControlStart/Stop` = `Scene_SetMoveControl(true/false)`.
- `0/50 SetMouseMove(v)`: sets `g_Scene_bMouseMove` and persists
  `StorageServer('SceneMouseMove', v)`; `0/51 SetMouseMoveData()` restores it
  when the storage server is available, else applies the current global.

---

## Next P1 items (queued)

1. `hotkeys.lua` stop handlers + `0/67`/`0/71`/`0/73` bodies; upvalue identity
   for `0/65`/`0/76` (`upval0/1/2`), incl. the `OnUseSkill(3799/3801...)`
   displacement branch and Scene `0/25` `upval0`.
2. `Scene.lua` remaining: `Scene_EnableFreeMoveControl`, `MoveForwardStart/Stop`
   wrappers (0/66), `MoveControlStart/Stop`, `IsInStickCamera`, `SetMouseMove`.
3. `IsMobileKungfu` implementation + desktop behavior (C binding, P2/P4).
3. Callers of `ResponseWASDKey` and consumer of `StartSprint`
   (`SprintBase.lua`?).
4. `IsMobileKungfu` implementation + desktop behavior.
5. `StorageServer` ↔ `custom.dat` mapping for `CurrentOperationMode` (P7).

## Evidence / reproduce

| Dump | Function |
|---|---|
| `proof/controls/lua_dump/hotkeys_0_61_wrapper.txt` | A1 |
| `proof/controls/lua_dump/hotkeys_0_46_ResponseWASDKey.txt` | A2 |
| `proof/controls/lua_dump/opmodebase_0_5.txt` | A3 |
| `proof/controls/lua_dump/opmodebase_0_19.txt` | A4 |
| `proof/controls/lua_dump/uisetting_switch_0_12_set.txt` | A5 |
| `proof/controls/lua_dump/hotkeys_0_63.txt` … `hotkeys_0_79.txt` | A6 (0/63,0/65,0/67,0/71-0/74,0/77,0/79) |
| `proof/controls/lua_dump/scene_0_24.txt` … `scene_0_61_LockMouse.txt` | A7 (0/24-0/27,0/31,0/36,0/61) |

Reproduce:

```
.venv\Scripts\python.exe tools/controls/lua_index.py %TEMP%\opencode\ui_ex\core --out %TEMP%\opencode\lua_index_all.txt
.venv\Scripts\python.exe tools/controls/lua51_probe.py <script>.lua --index 0/46 --out proof/controls/lua_dump/hotkeys_0_46_ResponseWASDKey.txt
```

The full 1549-file index (11 MB) is regenerated on demand and not tracked
(`.gitignore`); the committed control-set index is
`proof/controls/lua_index_control.txt`.

Last verified: 2026-10-02.
