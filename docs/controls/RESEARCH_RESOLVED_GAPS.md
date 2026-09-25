# Control-system gaps — resolved by the 2026-09-25 research pass

**Branch:** `control-system-notes` · **Date:** 2026-09-25
**Method:** bytecode dump + instruction-level probe of the extracted packed UI
scripts (`ui/Config/Default/*.lua`, `ui/script/hotkeys.lua`, `ui/script/control.lua`)
and the real per-role `userdata` files. Tool: `tools/controls/lua51_probe.py`
(new, instruction-level constants); evidence under `proof/controls/`.
**No code was changed; findings only.**

---

## 1. Per-role hotkey override files — FORMAT RESOLVED

Real files found in `userdata/13595372824/.../<role>/`:

| File | Format | Meaning |
|---|---|---|
| `hotkey_newlast.txt` | `name \t context \t index \t key` | **current** user overrides |
| `hotkey_newback<N>.txt` | same | rotating backups |
| `hotkey_last.txt` | `name \t index \t key` | **old** format (pre-context) |
| `hotkey_back<N>.txt` | same | old-format backups |
| `hotkey.data` | binary container: `CNDK` + `return {dwSaveID=1,tSaveDate={{szFile="hotkey_ne...` | save manifest (version, save id, file list) |

Decoded semantics:
- `index` = key slot (1 or 2) — the two bindable keys per command.
- `key` = the encoded key (empty = explicitly unbound). Encoding is the same as
  `default.txt`: VK + modifier high word, mouse 1/2/256/257.
- `context` = the binding context (empty = normal; `MobileSkillActionBar`, …).
- Only *user-modified* commands appear; missing rows fall back to `default.txt`.
- Addon bindings appear in the same file (e.g. `JX_SelectMark1`).
- Sample: `ACTIONBAR2_BUTTON2 1 81` (Q), `ACTIONBAR2_BUTTON9 1 262225` (Alt+Q),
  `ACTIONBAR2_BUTTON12 1 256` (wheel up), `SEARCH_ENEMY 2 79` (O).

Evidence: `proof/controls/userdata_hotkey/*` (copied), read-only.

## 2. Hotkey panel (`HotkeyPanel.lua`) — REBIND UI RESOLVED

Custom data / state:
- `HotkeyPanel.nVersion`, `dwSaveID`, `tSaveDate`, `bSaveToServer = true`.
- Layout from `UI/Config/default/HotkeyPanel.ini`; groups come from the
  `contextgroup` / `szContext` entries; visible tabs include `MobileSkillActionBar`.
- Row frames: `nNormalFrame 29`, `nMouseOverFrame 29`, `nDownFrame 30`,
  `nDisableFrame 31`, `nAutoModifiedFrame 29`, `nSelfModifiedFrame 31`,
  `nSelFrame 30`, `nUnchangeableFrame 31`.
  → rows visually distinguish **self-modified** vs **auto-modified** (addon
  defaults) vs **unchangeable** commands.

Binding row fields (per command): `szCommand`, `szDesc`, `szTip`, two slots
`Hotkey1` / `Hotkey2`, each with `nKey`, `bShift`, `bCtrl`, `bAlt`,
`bUnchangeable`, `bDoubleKeyDown`, `nDoubleKey`; label from
`GetKeyShow(nKey, bShift, bCtrl, bAlt)`; a double-press binding shows
`STR_DOUBLE_CLICK` + `GetKeyShow(nDoubleKey,…)`.

Flow:
1. Click a key row → `Hotkey.SetCapture(true)` (engine captures the next key).
2. Click again / cancel → `HotkeyPanel_CancelSetHotkey` → `SetCapture(false)`.
3. Refresh: `HotkeyPanel_SetHotkey` reads `Hotkey.Get(command, 1|2)`; if a slot
   has `nKey == 0` and `nDoubleKey > 0` it marks `bDoubleKeyDown` (double-press).
4. Engine owns conflict resolution; the Lua only displays state.
5. Save → `Hotkey.<Tab>` writes `hotkey_newlast.txt`, rotates a dated
   `hotkey_newback<date>.txt`, then `SaveLUAData('\hotkey.data', …)`.
6. Restore → scans `hotkey_newback*.txt` / `hotkey_back*.txt` (`GetFileList`,
   `string.find`), menu entries `STR_RESTORE_HK_DFT` (defaults) and
   `STR_RESTORE_HOTKEY` (backup).
7. Old format → `Hotkey.ConvertFile('hotkey_last.txt' → 'hotkey_newlast.txt')`.

Additional confirmed APIs: `Hotkey.Get`, `Hotkey.GetBinding`, `Hotkey.GetKeyShow`,
`Hotkey.SetCapture`, `Hotkey.Tab.Open/Close/Save`, `Hotkey.ConvertFile`.
Reference: `proof/controls/ui_lua/HotkeyPanel.setter.txt` / `.save.txt` / `.dump.txt`.

## 3. 广角 (WidAngle) — EXACT MAPPING RESOLVED

`VideoSettingPanel.lua` defines engine-option custom data:
`VideoSetting_MaxDistance`, `VideoSetting_WidAngle`, `VideoSetting_MouseSpeed`,
`VideoSetting_Version`, plus the camera panel table `VideoSettingPanel.tCameraStatic`.

The engine-option applier (proto `0/63`, `proof/controls/ui_lua/VideoSettingPanel.widangle.txt`):

```
-- MouseSpeed: if engine value == 0 then 1.2
-- MaxDistance: if engine value == 0 then 2000.0
-- WidAngle:
if value == 0 (unset):
    fCameraAngle = 50.0 * math.pi / 180.0          -- default 50°
    KG3DEngine.Set3DEngineOption({fCameraAngle = …})
else:
    if value < 30.0:
        value = value + caps.fMinCameraAngle * 180.0 / math.pi
    value = math.max(value, 30.0)
    value = math.min(value, 60.0)                  -- slider clamp 30°..60°
    fCameraAngle = value * math.pi / 180.0
    KG3DEngine.Set3DEngineOption({fCameraAngle = …})
```

So: **广角 slider = 30–60°, default 50°, stored in degrees, applied in radians
via `Set3DEngineOption("fCameraAngle")`**, with the engine caps
(`VideoBase.Get3DEngineOptionCaps().fMinCameraAngle`) used as a floor when the
value is below 30. `config.ini CammeraAngle = 0.837757` (48°) sits inside the range.

Camera panel values (same file, proto `0/62`/`0/63`) mirror into
`tCameraStatic`: `fDragSpeed` (= MouseSpeed), `fMaxCameraDistance`
(= MaxDistance), `fSpringResetSpeed`, `fCameraResetSpeed`, `nCameraMode`,
`fDragPitchSpeed`; server sync via `UI_Camera_SetParams_S` when
`StorageServer:IsReady()`; getters `Camera_GetParams` → `SetCameraParam` /
`SaveCameraParam`. Restore defaults (`tCameraSetInfo`): `nReCameraMode=1`,
`fReDragSpeed=10`, `fReCameraDistance=41`, `fReCameraAngle=18`.

## 4. Operation modes — RESOLVED

`UISetting_Operation_Switch.lua` (proto `0/14`):
```
SetOperationMode('JOYSTICK_MODE')
UISetting_Comprehensive:SetCameraMode(nCameraModeInJoystickMode)
-- mirror branch:
SetOperationMode('CLASSICAL_MODE')
UISetting_Comprehensive:SetCameraMode(nCameraModeInClassicMode)
```
Plus `Scene_LockTarget` in the same panel family.

Movement coupling (`hotkeys.lua`): with `GetOperationMode() == CLASSICAL_MODE`
the look/strafe handlers call `Camera_EnableControl(flag, …)`; in joystick mode
they call `Scene_EnableFreeMoveControl(flag, …)`. Strafe-left/right additionally
branch on `Camera_IsInFreeView` and call `TurnLeftStart/TurnRightStart` when in
classic free view, then `ResponseWASDKey('StrafeLeft/Right', down, double)`.

## 5. Movement command implementations — RESOLVED

`ui/script/hotkeys.lua` defines 39 command globals, including:
`MoveForwardStart/Stop`, `MoveBackwardStart/Stop`, `TurnLeftStart/Stop`,
`TurnRightStart/Stop` (via root closures), `Hotkey_EnableSprint/MoveBack/
ToggleRun/Sheath/TurnLeft/TurnRight`, `Jump/EndJump` (`EndJump` global),
`CameraOrSelectOrMoveStart/Stop`, `CameraReset`, `CameraSetView`, `CameraZoomIn/Out`,
`EnableCameraZoom`, `FlipCameraYaw`, `AttackTarget`, `AutoInteract`,
`CastSkillByKeyDown`, `FollowTarget`, `DownHorse`, `EndSprint`,
`CheckEndSprint`, `CanUseLeftRightSprint`, `ActionButtonDown/Up`,
`ActionBar_PageUp/PageDown`, `ActionBar_YaoZongPlant`, `ChangeActionBarPage`,
`ChangePlayerDisplayMode`, `EnterOrLeaveCarrier`, `EquipKongfu`,
`CloseAllConflictPanels`, `CloseIndePentShowPanels`, `GetKeyName/GetKeyShow/GetKeyValue`,
`GVOpen/CloseMicphone`, `GetDisplacementSkillSetting`.

Decoded bodies:
```
MoveForwardStart:
  if GetClientPlayer() then player.HoldW = 1 end
  if NOT <upval flag> then
      SetControl(CONTROL_FORWARD, true)
      if <upval check>() and IsKeyDoubleDown() then
          ResponseWASDKey('Forward', true, true)
      else ResponseWASDKey('Forward', true, false)
      end
  else
      -- skill-directed movement: OnUseSkill(3799, …) with 3799 % 10 + 1
  end

MoveForwardStop:
  player.HoldW = 0
  if not ResponseWASDKey('Forward', false, false) then CheckEndSprint() end
  SetControl(CONTROL_FORWARD, false)

TurnLeftStop: ResponseWASDKey('TurnLeft', false, false); SetControl(CONTROL_TURN_LEFT, false)
Hotkey_EnableSprint(a,b):
  if GetOperationMode() == CLASSICAL_MODE then Camera_EnableControl(a,b)
  else Scene_EnableFreeMoveControl(a,b) end
```
Strafe handlers use skill ids 3801 (left) / 3802 (right) for the
skill-direction path.

`ui/script/control.lua` control ids (numeric → name):
`0 CONTROL_FORWARD, 1 CONTROL_BACKWARD, 2 CONTROL_TURN_LEFT, 3 CONTROL_TURN_RIGHT,
4 CONTROL_STRAFE_LEFT, 5 CONTROL_STRAFE_RIGHT, 6 CONTROL_CAMERA,
7 CONTROL_OBJECT_STICK_CAMERA, 8 CONTROL_WALK, 9 CONTROL_JUMP,
10 CONTROL_AUTO_RUN, 11 CONTROL_FOLLOW, 12 CONTROL_UP, 13 CONTROL_DOWN`.
`Ctrl_CameraOrSelectOrMoveStart/Stop` are **empty stubs** (confirmed again).

`ResponseWASDKey(direction, down, isDouble)` is the engine-side actuator (global,
not defined in the extracted Lua); `IsKeyDoubleDown` drives double-tap.
Sprint state checks use `MOVE_STATE.ON_FLY_JUMP`, `bSprintFlag`, `bOnHorse`.

## 6. Action bar — STORAGE AND LAYOUT RESOLVED

`ActionBar.lua` custom data (`RegisterCustomData`):
`ActionBar.nPage`, `bLock`, `aShowBg`, `DefaultAnchor`, `Anchor`, `ExtendAnchor`,
`AnchorTop`, `Size`, `Line`, `tSkillIDToBox`, `tSkillIDToShowNum`.

- Default anchors: `s/r = BOTTOMCENTER`, `rw = Lowest1/MainBarPanel`, `x = 26`,
  `y = -10, -58, -118, -166, -213` per bar group (indices 9..13).
- Slot contents persist via `StorageServer.SetData(data_key, GetActionBarKey(...), …)`
  with `data_key = 'ActionBar'`; page from `GetMainActionBarPage()`, lock page
  `_LockPage`.
- Events handled: `ON_SELECT_MAIN_ACTIONBAR_PAGE`, `ON_SET_ACTIONBAR_COUNT`,
  `ON_SET_ACTIONBAR_LINE`, `ON_ACTIONBAR_BG_SHOW`, `HOT_KEY_RELOADED`,
  `HAND_PICK_OBJECT`, `HAND_CLEAR_OBJECT`, `OPEN_SKILL_PANEL`, bag/skill updates.
- Drag/drop: `Hand_Pick`, `Hand_IsEmpty`, `OnChangeHandAndBoxItem`,
  `DropHandItem`, with errors `SRT_ERROR_LOCK_ACTIONBAR_WHEN_DRAG`,
  `SRT_ERROR_DROP_HAND_OBJ_WHEN_DRAG`, `SRT_ERROR_CANCEL_CURSOR_STATE`.
- Auto-cast helpers: `ActionBar_AddAutoDownKey`, `ActionBar_IsAutoDownEnable`,
  `ActionBar_IsKeyAutoPressing`, `ActionBar_IsAutoWhenTruce`,
  `ActionBar_CheckAutoPress`, `ActionBar_BreatheAutoDownKey`.
- Cast entry points: `ActionBar_ButtonDown/Up`, `ActionBar_Cast`.

## 7. UI custom mode — RESOLVED

`UICustomModePanel.lua`:
- custom data `UICustomModePanel.Anchor = {s, r, x, y}`, flag `bCustomMode`;
  events `CUSTOM_UI_MODE_ANCHOR_LOADED`, `CUSTOM_DATA_LOADED`,
  `ON_ENTER_CUSTOM_UI_MODE`, `ON_LEAVE_CUSTOM_UI_MODE`,
  `CUSTOM_UI_MODE_SET_DEFAULT`.
- `OpenUICustomModePanel` / `CloseUICustomModePanel` /
  `IsUICustomModePanelOpened` / `IsInUICustomMode` / `UpdateCustomModeWindow`.
- Entering: opens the panel frame, fires the event; every registered window gets
  `EnableDrag`, `SetDragArea`, `SetMousePenetrable` and keeps its previous
  dragability in `__bIsDragableBeforeEnterUICustomMode`; leaving restores it.
- Buttons: `Btn_Sure` (close/apply anchor) and `Btn_Default`
  (fires `CUSTOM_UI_MODE_SET_DEFAULT`).

## 8. Still open after this pass

| Item | Status |
|---|---|
| Engine conflict resolution for duplicate bindings | **BLOCKED** (engine C-side; Lua only displays) |
| `ResponseWASDKey` / `Scene_EnableFreeMoveControl` / `IsKeyDoubleDown` bodies | **BLOCKED** (engine globals, not in UI Lua) |
| `SearchNextTarget` / `SelectPlayer` / `AutoMoveToPoint` bodies | located (`b03/target.lua`, `b01/helper.lua`, `b02/player.lua`) — not decoded yet |
| Rating→% damage formula | **BLOCKED** (needs `KPlayer::GetAttributeValue` decompilation) |
| DR per-application ladder | **BLOCKED** (server-side) |
| `OnAdjustPlayerMove` / `OnSyncRunSpeedLimit` layouts | open (client exe disasm) |
| `CharacterYawTurnSpeed` consumer | open (client exe disasm) |
| Engine caps values (`fMinCameraAngle`, …) | open (runtime probe) |
| Native camera look-at interface | open (host API work) |

## 9. Evidence index

| Path | Content |
|---|---|
| `proof/controls/userdata_hotkey/` | real `hotkey_newlast.txt`, `hotkey_last.txt`, `hotkey_back2.txt`, `hotkey.data`, `custom.dat` |
| `proof/controls/ui_lua/HotkeyPanel.*` | dump + setter/capture/save disassembly |
| `proof/controls/ui_lua/VideoSettingPanel.*` | dump + WidAngle/option disassembly |
| `proof/controls/ui_lua/UICustomModePanel.dump.txt` | custom-mode functions |
| `proof/controls/ui_lua/OperationSwitch.joystick.txt` | operation-mode switch |
| `proof/controls/ui_lua/ActionBar.*` | storage/anchors/events |
| `proof/controls/ui_lua/hotkeys_script.lua` + `.dump.txt` + `.proto*.txt` | movement/camera command bodies |
| `proof/controls/ui_lua/control_script.lua` | control ids |
| `tools/controls/lua51_probe.py` | new instruction-level bytecode probe |
