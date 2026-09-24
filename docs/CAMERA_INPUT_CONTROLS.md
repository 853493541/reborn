# JX3 camera input controls — real client data (2026-09-23)

How the **real client** maps the mouse/keyboard to the camera, recovered from
the shipped UI scripts and the represent DLL. This answers "what happens on
left-drag / right-drag" with client data instead of guessing.

Raw evidence: `proof/netcode/camera_ui_controls.txt`.

---

## 1. Real default bindings

Source: `ui/hotkey/default.txt` (extracted from the client pak; UI file map
`ui/module_info.xml` → `HotkeyBindingSetPath = \UI\Hotkey\default.txt`).
Key codes: `1` = LMB, `2` = RMB, `256` = wheel up, `257` = wheel down.

| Command | Default key | Lua handler (`ui/hotkey/bindings.ini`) | Meaning (in-file desc) |
|---|---|---|---|
| `CAMERAORSELECTORMOVE` | **LMB (1)** | `CameraOrSelectOrMoveStart(0)` / `Stop(0)` | 镜头旋转 — rotate the camera (or select) |
| `CAMERAORSELECTORMOVESTICKY` | **RMB (2)** | `CameraOrSelectOrMoveStart(1)` / `Stop(1)` | 镜头和人物旋转 — rotate camera **and character** |
| `CAMERAZOOMIN` | wheel up (256) | `CameraZoomIn()` | 推进镜头 — zoom in |
| `CAMERAZOOMOUT` | wheel down (257) | `CameraZoomOut()` | 拉远镜头 — zoom out |
| `CAMERARESET` | F11 (122) | `CameraReset()` | 重置镜头 — reset |
| `CAMERAUP` / `CAMERADOWN` | *(unbound)* | `MoveUpStart()/Stop()`, `MoveDownStart()/Stop()` | 镜头向上/向下旋转 — camera pitch by key |
| `CAMERA_SET_VIEW_1` / `_2` | Home (36) / End (35) | `CameraSetView(0)` / `CameraSetView(180)` | 镜头切换到脑(后)/前 — view presets |

So: **both mouse buttons rotate the camera while dragging**; LMB is the
"camera-or-selector" variant (a click selects what is under the cursor, a drag
rotates the view), RMB is the "sticky" variant that also turns the character.

## 2. UI-layer implementation (decompiled)

`ui/script/hotkeys.lua` (Lua 5.1 bytecode; decompiled with unluac,
see `proof/netcode/camera_ui_controls.txt`):

```lua
function CameraZoomIn()                 -- wheel up
  if IsCameraZoomEnabled then Camera_Zoom(0.9) end      -- distance * 0.9
end
function CameraZoomOut()                -- wheel down
  if IsCameraZoomEnabled then Camera_Zoom(1.1) end      -- distance * 1.1
end

function CameraOrSelectOrMoveStart(mode) -- LMB mode=0, RMB mode=1
  Ctrl_CameraOrSelectOrMoveStart(mode)
end
function CameraOrSelectOrMoveStop(mode)
  Ctrl_CameraOrSelectOrMoveStop(mode)
end

function CameraReset()                  -- F11
  local p = GetClientPlayer()
  if not p then return end
  local d = p.nFaceDirection - 64
  if d < 0 then d = d + 256 end
  local yaw = (256 - d) / 256 * math.pi * 2
  Camera_SetForceReset(yaw, math.pi / -12, 1)   -- camera behind the character, pitch -15 deg
end
```

- `ui/script/control.lua` defines the control ids and **empty stubs** for
  `Ctrl_CameraOrSelectOrMoveStart/Stop` — the actual per-frame rotation is not
  in Lua; the engine/represent owns it (see §3).
- Control ids: `CONTROL_CAMERA = 6`, `CONTROL_OBJECT_STICK_CAMERA = 7`
  (mode 0 -> camera, mode 1 -> camera+character), `CONTROL_WALK = 8`, `JUMP = 9`,
  `AUTO_RUN = 10`, `FOLLOW = 11`, `UP = 12`, `DOWN = 13`.
- `ui/script/operationmodebase.lua` — operation modes `CLASSICAL_MODE` (经典)
  vs `JOYSTICK_MODE` (摇杆); it toggles `Camera_EnableControl`,
  `Scene_LockMouseRotation`, `Camera_SetResetSpeed`, `UseFullAngle` per mode.
- `ui/script/cameracommon.lua` — spectator/watch camera (SetView/StartWatch,
  god camera), not the player drag.

## 3. Represent-side mouse → camera (the actual drag)

`JX3RepresentX64.dll` contains the per-frame mouse handling (string-verified):

| Function | Role |
|---|---|
| `ComputeMouseMoveDelta` | computes the mouse delta (cursor pos based) |
| `ApplyMouse` | applies the delta: **delta × 2π** (const `0x180cb9f34` = `6.2831855`) then clamps |
| `MouseMove`, `MouseMoveCamera` | per-frame application to the active camera controller |
| `ClampMouse` | clamp by controller (row params `+0x20` / `+0x24` = drag/rotation speeds) |
| `ClampMouseForSprintCamera`, `ClampMouseForCarrierCamera`, `ClampMouseForGliderCamera` | per-mode clamp variants |
| `IsMouseLeftButtonPressed`, `IsMouseRightButtonPressed` | mouse-button state exposed to Lua/UI |
| `MouseControlMoveEnable`, `EnableMouseControlMove`, `HandleMouseMoveEnable` | mouse-move-turns-character coupling |
| `GetRLCursorPos` / `SetRLCursorPos` / `GetRLCursorScenePos` | cursor lock/restore used while dragging |

Controller lookup strings used by `ApplyMouse`/`ClampMouse`:
`"camera"`, `"camera controller"`, `"sprint camera controller"`,
`"carrier camera controller"`, `"glider camera controller"`,
`"npc dialog camera controller"`.

The drag thus flows: engine input (mouse button + delta) → represent camera
controller (`ComputeMouseMoveDelta` → `ApplyMouse` → `ClampMouse`) → camera
yaw/pitch, with the row's drag/rotation speeds and per-mode clamps. The UI Lua
only decides **which mode** is active (`CameraOrSelectOrMoveStart(0/1)`), zooms
(`Camera_Zoom(0.9/1.1)`), resets (`Camera_SetForceReset`) and gates controls
per operation mode.

## 4. What our client should implement

Current `client/RebornClient.cs` rotates the camera only when the engine orbit
actions are fed; it has no LMB/RMB split and no character coupling. To match:

1. **RMB drag** (`CameraOrSelectOrMoveStart(1)`): lock/hide the cursor, rotate
   the camera from the mouse delta **and turn the character** to the camera
   direction (the "镜头和人物旋转" behaviour).
2. **LMB drag** (mode 0): rotate the camera only; a click without drag selects
   (keep the existing left-click select).
3. **Wheel**: `distance *= 0.9` (up) / `*= 1.1` (down), clamped to
   `[fMinCameraDistance, fMaxCameraDistance]` (see `CAMERA_REAL_VALUES.md`).
   This supersedes the invented ±0.5 m step.
4. **F11**: reset the camera behind the character with pitch **-15 deg**
   (`Camera_SetForceReset(charYaw, -pi/12)`), not the current ad-hoc reset.
5. **Home / End**: view presets (camera yaw offset 0 / 180 relative to the
   character facing).
6. **Camera pitch keys**: `MoveUpStart/Stop`, `MoveDownStart/Stop` (unbound by
   default, but the commands exist).
7. Sensitivity: keep the user setting path (`fDragSpeed` / `fDragPitchSpeed`
   from `custom.dat`) and the row limits (`MaxDragSpeed`, `RotationSpeed`,
   `CameraMaxDeltaYaw/Pitch`); the represent applies `delta × 2π` before
   clamping.
8. Operation modes: classic mode = hold LMB/RMB to rotate; joystick/action
   mode = mouse always rotates (`Scene_LockMouseRotation`). Our client can
   start with the classic scheme.

## 5. Evidence and how to reproduce

- Extract UI files (works with the official pak tool):
  `ui/module_info.xml`, `ui/hotkey/default.txt`, `ui/hotkey/bindings.ini`,
  `ui/script/hotkeys.lua`, `ui/script/control.lua`,
  `ui/script/operationmodebase.lua` (script:
  `tools/netcode/extract_pak_paths.py`, or the one-off probe used here).
- Dump bytecode: `tools/netcode/lua51_dump.py FILE --code`.
- Decompile: `java -jar unluac.jar FILE > out.lua` (unluac 2023_12_24).
- Represent strings/disasm: `proof/gravity/JX3RepresentX64_strings.txt`,
  `tools/dump_va.py JX3RepresentX64.dll 0x180b36e80 0x180b37060` (ApplyMouse),
  `0x180b37770` (ClampMouse).

## 6. Open items

1. Exact `ApplyMouse` math for the **character** camera controller (the
   `+0x20`/`+0x24` row fields and the 2π factor) — disasm pass in progress;
   `ClampMouse` variants already located.
2. `Ctrl_CameraOrSelectOrMoveStart/Stop` real implementation — the `control.lua`
   stubs are empty; check whether another UI package (`ui/Traits/...`,
   `ui/Config/Default/...`) overrides them, or whether the engine consumes the
   mouse directly (represent functions in §3 suggest the latter).
3. Cursor lock details while dragging (`GetRLCursorPos/SetRLCursorPos` usage)
   and the "sticky" flag meaning for mode 1.
4. Per-mode clamps (sprint/carrier/glider) constants.
