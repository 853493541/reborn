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

-- double-tap sprint: only Forward, only on key-down with isDouble,
-- blocked while on tower / bird / horse:
if down and isDouble and not onTower and key ~= 'Forward' then
    return handled
end
if p and (p.bBirdMove or p.bHoldHorse) then return handled end
StartSprint()

return handled
```

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
4. **Double-tap forward = sprint** (`StartSprint`) with tower/bird/horse guards;
   other keys ignore `isDouble`.
5. `handled` (true when a displacement hotkey consumed the event) is returned
   to the movement handler (see the pending 76/78 verification).

## A3. Operation-mode apply — `OperationModeBase.lua` proto `0/5` (L185-209) HIGH (one open)

```lua
-- upvalue: current mode
local function ApplyOperationMode(mode)
    if mode ~= CLASSICAL_MODE and not IsMobileKungfu() then return end
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
- **Open:** the `IsMobileKungfu()` gate for non-classical modes — determine
  what this flag actually is on desktop builds (implementation pending; do not
  assume "mobile-only" yet).

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
-- upvalues: [0]=OB wrapper (0/63), [1]=?, [2]=? (mode/state predicates)
function StrafeLeftStart()
    if GetOperationMode() == CLASSICAL_MODE then
        if SetControlInOB(CONTROL_STRAFE_LEFT, true) then return end   -- OB: set + done
        if Camera_IsInFreeView() then TurnLeftStart() end              -- normal play: turn
        return
    end
    if not upval1 then
        OnUseSkill(3801, 3801 * ((3801 % 10) + 1))                     -- 3801, 37990
        return
    end
    if not upval2() then return end
    if IsKeyDoubleDown() then
        if not ResponseWASDKey('StrafeLeft', true, true) then
            Camera_EnableControl(CONTROL_STRAFE_LEFT, true)            -- fallback
        end
    else
        if not ResponseWASDKey('StrafeLeft', true, false) then
            Camera_EnableControl(CONTROL_STRAFE_LEFT, true)
        end
    end
end
```

`0/78 StrafeRightStart` mirrors it (`CONTROL_STRAFE_RIGHT`, `TurnRightStart`,
`'StrafeRight'`, skill 3802 family).

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
function MoveForwardStart()
    local p = GetClientPlayer()
    if p then p.HoldW(1.0) end
    if upval0 then
        OnUseSkill(3799, 3799 * ((3799 % 10) + 1))   -- 3799, 37990
    else
        upval1(CONTROL_FORWARD, true)                -- mode wrapper (0/61)
        if upval2() or not IsKeyDoubleDown() then
            ResponseWASDKey('Forward', true, false)
        else
            ResponseWASDKey('Forward', true, true)   -- double-tap -> sprint (A2)
        end
    end
end
```

`p.HoldW` (client-player hold state) + displacement skill in the `upval0`
branch; the wrapper + joystick vector path otherwise. Stop handlers call the
wrapper with `false` and `ResponseWASDKey(..., false, ...)`.

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

### A7.5 Classical both-buttons autorun — proto `0/25` (L585-599) MED

Joystick returns immediately; classical path enables
`FreeMoveControl(CONTROL_FORWARD, true)` when both mouse buttons are down
(with an extra `upval0[CONTROL_FORWARD]` condition) and clears it via the
`bMoveStart` latch otherwise. Exact upvalue identity open (P1 next).

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
