# JX3 hotkey / input system — full specification

**Branch:** `control-system-notes` · **Date:** 2026-09-25
**Status:** research complete (notes only — no implementation).
**Why this topic:** every user control in the game is dispatched through this
system — movement, camera, combat, action bars, targeting and UI. 428 commands,
two key slots each, seven runtime contexts, per-role rebinding with save
history. Recreating the control system starts and ends here.

**Annexes (generated, complete):**

| File | Content |
|---|---|
| `proof/controls/hotkey_default.md` | all **286** default rows, decoded keys |
| `proof/controls/hotkey_default_decoded.tsv` | same, machine-readable |
| `proof/controls/hotkey_registry_by_context.md` | all **428** commands: context, keys, down/up, description |
| `proof/controls/hotkey_command_registry.tsv` | same, machine-readable |
| `proof/controls/registry_summary.txt` | counts, context groups, handler samples |
| `proof/controls/HotkeyPanel.ini` | rebind-panel window layout (72 sections) |
| `proof/controls/userdata_hotkey/*` | real per-role binding files + manifest |

---

## 1. Architecture

```
keyboard / mouse
  │
  ├─ win32 input ──► UI::KHotkeyMgr (KGUIX64.dll)
  │                     • binding table (command, context, key1, key2)
  │                     • key states, capture mode, repeat timer
  │                     • default load / per-role load / save
  │
  └─ Lua command dispatch (ui/hotkey/bindings.ini `down=` / `up=`)
        │
        ├─ UI scripts (ui/script/hotkeys.lua, panels, target.lua, …)
        │     e.g. MoveForwardStart(), ActionButtonDown(1,1), CastSkillByKeyDown("…")
        │
        └─ engine bridge (JX3RepresentX64.dll KGameWorldHandler::CommitInput)
              • movement → character controller (server-authoritative)
              • camera   → camera controller (ApplyMouse/ClampMouse)
              • combat   → cast intent packets
```

The command layer is deliberately thin: bindings.ini maps a key to a Lua
function call; all state and authority live in the engine/server.

## 2. File inventory and formats

### 2.1 Shipped tables (pak)

| File | Content |
|---|---|
| `ui/hotkey/default.txt` | default bindings; header `name \t context \t key1 \t key2`; **286 rows** |
| `ui/hotkey/bindings.ini` | command definitions; **428 `[COMMAND]` sections** with `desc`, `contextgroup`, `down`, `up`, `runOnUp` |
| `ui/filepath.txt` | maps `HotkeyBindingSetPath` / `HotkeyBindingsFile` to the two files above |
| `ui/Config/Default/HotkeyPanel.ini` | rebind window layout (72 sections; `[HotkeyPanel]`, `[Handle_*]`, `[Handle_Binding]`, `[Handle_Key1/2]`, `[Text_*]`) — the command list itself is read at runtime via `Hotkey.GetBinding`, not from this ini |
| `ui/Config/Default/HotkeyPanel.lua` | panel driver (decompiled in `RESEARCH_RESOLVED_GAPS.md` §2) |

### 2.2 Per-role files (`userdata/<account>/<region>/<server>/<role>/`)

| File | Format | Meaning |
|---|---|---|
| `hotkey_newlast.txt` | `name \t context \t index \t key` | **current overrides** (new format) |
| `hotkey_newback<N>.txt` | same | rotating dated backups |
| `hotkey_last.txt` | `name \t index \t key` | old format (pre-context) |
| `hotkey_back<N>.txt` | same | old-format backups |
| `hotkey.data` | CNDK container + Lua manifest | save index / backup list |

Only **user-modified** commands are stored; absent rows fall back to
`default.txt`. `index` = key slot (1 or 2); an empty `key` means the user
explicitly unbound that slot. Addon commands (e.g. `JX_SelectMark1`) live in the
same file.

Real example (`hotkey_newlast.txt`, 2025 build):

```
name                     context   index   key
STRAFELEFT                         1
STRAFERIGHT                        1
TOGGLESHEATH                       1
TOGGLESITDOWN                      2
FOLLOWTARGET                       2       98
SELECT_TEAMMATE1                   1       97
SEARCH_ENEMY                       2       79
ACTIONBAR1_BUTTON5                 1       262225
ACTIONBAR2_BUTTON1                 1
ACTIONBAR2_BUTTON2                 1       81
ACTIONBAR2_BUTTON9                 1
ACTIONBAR2_BUTTON12                1       256
JX_SelectMark1                     2       112
```

Old example (`hotkey_back2.txt`):

```
name      index   key
STRAFELEFT        1
CAMERAZOOMIN      1
ACTIONBAR1_BUTTON14     1       256
ACTIONBAR1_BUTTON15     1       257
```

Manifest `hotkey.data` (fully decoded):

```
return {dwSaveID=1, tSaveDate={
  {szFile="hotkey_newback1.txt", szDate="2025_8_25"},
  {szDate="2023_9_1",   szFile="hotkey_newback2.txt"},
  {szFile="hotkey_newback3.txt", szDate="2024_10_15"},
  {szDate="2024_10_28", szFile="hotkey_newback4.txt"},
  [0]={szFile="hotkey_newback0.txt", szDate="2025_5_30"}}}
```

The container is `CNDK` + 12 header bytes (two `0x11E` = 286 dwords) + the Lua

> **Correction (2026-10-06):** CNDK header = magic + uint32 crc32(payload) + uint32 size + uint32 size + payload; `0x11E` is the byte size repeated (crc verified on 17k `.jx3dat` + userdata). See `docs/character/3_4_FACIAL.md`.
text above.

## 3. Key encoding (exact)

```
key_value = (modifiers << 16) | virtual_key      ; 32-bit integer
modifiers: 0x1 Ctrl, 0x2 Shift, 0x4 Alt          ; any combination
mouse: 1 = LMB, 2 = RMB, 256 = wheel up, 257 = wheel down
```

| Value | Decoded |
|---|---|
| `87` | W |
| `38` | Up |
| `32` | Space |
| `111` | Num/ |
| `65607` = 0x10047 | Ctrl+G |
| `65621` = 0x10055 | Ctrl+U |
| `131081` = 0x20009 | Shift+Tab |
| `196686` = 0x3004E | Ctrl+Shift+N |
| `262193` = 0x40031 | Alt+1 |
| `262231` = 0x40057 | Alt+W |
| `262225` = 0x40051 | Alt+Q (real user binding) |

Decoder: `vk = n & 0xFFFF`, `mods = n >> 16`, style `Alt+Shift+Ctrl+VK`.
The full decoded default table is in the annex (`hotkey_default.md`).

## 4. Command registry (428 commands)

| Category (by prefix) | Count |
|---|---|
| Action bars / dynamic / vehicle / rogue / BR / mobile | **292** |
| Panel & UI toggles | **66** |
| Skill / talent / stance | **30** |
| Targeting / selection | **11** |
| Movement / character | **10** |
| Camera | **9** |
| Other (chat, screenshot, mail, trade, minigame, …) | **10** |
| **Total** | **428** |

Contexts used by `default.txt` rows:
`(normal)`, `ExtentDynamicAction`, `DynamicBattleRoyale`, `NpcAssistedDynamicAction`,
`RougeDynamicAction`, `MobileSkillActionBar`, `minigame`.

UI context groups from `bindings.ini` `contextgroup=` (rebind-panel tabs):

| Group | Example command |
|---|---|
| *(none — main list)* | 421 commands |
| 战场技能栏 | `BATTLEACTIONBAR_BUTTON1` |
| 特殊动态快捷栏 | `DYNAMICACTIONBAR1_BUTTON1` |
| 红尘侠影共鸣 | `DYNAMICACTIONBAR2_BUTTON1` |
| 小游戏模式 | `MINIGAME_JUMP` |
| 八荒衡鉴快捷栏 | `ROUGEACTIONBAR_BUTTON1` |
| 大唐世界 | `TOGGLEMOVECONTROL` |
| 无界心法技能栏 | `VKACTIONBAR_BUTTON_AUTO` |

## 5. Handler semantics

- `down=` fires on key press; `up=` on release. **307 / 428** commands have an
  `up` handler; the rest are pure toggles/one-shots.
- `runOnUp=1` (only `STRAFERIGHT`) fires the `down` handler on release instead.
- Hold-repeat (`Hotkey_EnableKeyDownLoop` + `GetKeyTimeInterval`) is used by UI
  list scrolling, not by movement/combat.
- Key slots 1 and 2 are independent; both can fire.
- **Double-press bindings:** a slot can store `nDoubleKey` with `nKey=0` and
  `bDoubleKeyDown=true` (UI shows `STR_DOUBLE_CLICK` + the key); the engine
  provides `IsKeyDoubleDown()` to consumers.
- `bUnchangeable` commands cannot be rebound (tooltip
  `STR_HOTKEY_UNCHANGEDABLE`).

Representative handlers (all 428 in the annex):

| Command | Down | Up |
|---|---|---|
| `MOVEFORWARD` | `MoveForwardStart();` | `MoveForwardStop();` |
| `TURNLEFT` | `TurnLeftStart();` | `TurnLeftStop();` |
| `JUMP` | `Jump();` | `EndJump();` |
| `RIDEHORSE` | `RideHorse();` | `DownHorse();` |
| `TOGGLERUN` | `ToggleRun();` | — |
| `TOGGLEAUTORUN` | `ToggleAutoRun();` | — |
| `AUTOINTERACT` | `AutoInteract();` | `AutoInteract_keyup();` |
| `SEARCH_ENEMY` | `SearchNextTarget();` | — |
| `SELECT_SELF` | `SelectPlayer()` | — |
| `SELECT_TEAMMATE1` | `SelectTeammate(1)` | — |
| `SELECT_TARGETTARGET` | `SelectTargetTarget()` | — |
| `SELECT_ONLYPLAYER` | `SearchTarget_SwitchOnlyPlayer()` | — |
| `CAMERAORSELECTORMOVE` | `CameraOrSelectOrMoveStart(0);` | `CameraOrSelectOrMoveStop(0);` |
| `CAMERAORSELECTORMOVESTICKY` | `CameraOrSelectOrMoveStart(1);` | `CameraOrSelectOrMoveStop(1);` |
| `CAMERARESET` | `CameraReset();` | — |
| `CAMERA_SET_VIEW_1/2` | `CameraSetView(0)` / `(180)` | — |
| `ACTIONBAR1_BUTTON1` | `ActionButtonDown(1, 1);` | `ActionButtonUp(1, 1);` |
| `SKILL_CAST_FORWARD` | `CastSkillByKeyDown("SKILL_CAST_FORWARD");` | — |
| `TALENT_SET1` | `NewSkillPanel_ChangeQixueSet(0)` | — |

## 6. Runtime APIs

**Engine (KGUI Lua accessors):**
`LuaGetHotKey`, `Lua_Get`, `Lua_Set`, `Lua_IsDown`, `Lua_IsKeyDown`,
`Lua_IsUsed`, `Lua_AddBinding`, `Lua_SetCapture`, `Lua_EnableKeyDownLoop`;
manager side: `KHotkeyMgr::Init/Load/Save/GetBinding/SearchBinding/
RefreshKeyDownData/LoadDefault/LoadBinding`, `GetKeyTimeInterval`,
`Hotkey_EnableNewMode`.

**UI (`Hotkey.*`, used by the rebind panel):**
`Hotkey.Get(command, slot)` → `{nKey, nDoubleKey, bShift, bCtrl, bAlt,
bUnchangeable, bDoubleKeyDown}`; `Hotkey.GetBinding`, `Hotkey.GetKeyShow`,
`Hotkey.SetCapture(bool)`, `Hotkey.SaveAsAdd`, `Hotkey.ConvertFile`,
`Hotkey.Tab.Open/Close/Save`.

**Panel globals:** `OpenHotkeyPanel`, `CloseHotkeyPanel`, `IsHotkeyPanelOpened`,
`HotkeyPanel_SetHotkey`, `HotkeyPanel_CancelSetHotkey`, `Hotkey_MsgBox`.

## 7. Rebind UI flow (HotkeyPanel)

1. `OpenHotkeyPanel` → `HotkeyPanel:Init` → `LoadConfig` builds `aGroup` from
   `Hotkey.GetBinding` + `HotkeyPanel.ini`; each command gets a group row and a
   binding row with two key slots (`Hotkey1`, `Hotkey2`).
2. Slot label = `GetKeyShow(nKey, bShift, bCtrl, bAlt)`; double-press shows
   `STR_DOUBLE_CLICK` + `GetKeyShow(nDoubleKey)`; `bUnchangeable` slots use the
   unchangeable frame and ignore clicks.
3. Click (LButtonDown) on a changeable slot:
   `HotkeyPanel.CancelSetHotkey(...)` → `bSel=true` → `UpdateBtnState` →
   `Hotkey.SetCapture(true)` → UI sound.
4. The engine consumes the next key/chord and updates the binding; the panel
   refreshes (`HotkeyPanel_SetHotkey`).
5. Click again / right-click / panel close → `SetCapture(false)`.
6. Modified rows are marked self-modified (`nSelfModifiedFrame`) vs
   auto-modified addon defaults (`nAutoModifiedFrame`).

## 8. Persistence lifecycle

```
save:   write hotkey_newlast.txt
        copy/rotate a dated hotkey_newback<date>.txt
        SaveLUAData('\hotkey.data', {dwSaveID, tSaveDate={…}})
        optional upload when HotkeyPanel.bSaveToServer = true

restore: GetFileList(GetUserDataPath()) → files matching 'hotkey_newback'/'hotkey_back'
         menu: STR_RESTORE_HK_DFT (defaults) / STR_RESTORE_HOTKEY (backup)

convert: Hotkey.ConvertFile('hotkey_last.txt' → 'hotkey_newlast.txt')
         (old name/index/key format → new name/context/index/key)
```

## 9. Command → engine bridge (decoded)

| Domain | Bridge |
|---|---|
| Movement | `MoveForwardStart` → `player.HoldW=1`, `SetControl(CONTROL_FORWARD,true)`, `ResponseWASDKey('Forward',down,doubleTap)`; stop → `SetControl(...,false)` + `CheckEndSprint()`; strafes branch on `GetOperationMode()==CLASSICAL_MODE` / `Camera_IsInFreeView` and use `Camera_EnableControl` vs `Scene_EnableFreeMoveControl`; directional skills `OnUseSkill(3799/3801/3802)` |
| Camera | `CameraOrSelectOrMoveStart/Stop(0|1)` → `Ctrl_CameraOrSelectOrMoveStart/Stop` (empty Lua stubs) → the represent camera controller consumes the mouse (`ApplyMouse`/`ClampMouse`); `CameraZoomIn/Out` → `Camera_Zoom(0.9/1.1)`; `CameraReset` → `Camera_SetForceReset`; `CameraSetView(0/180)`; `MoveUpStart/Stop`, `MoveDownStart/Stop` |
| Action bars | `ActionButtonDown(bar, slot)` / `ActionButtonUp` → `ActionBar_ButtonDown/Up` → `ActionBar_Cast`; dynamic bars use `DynamicActionBar_ButtonDown(slot)`; page commands `ActionBar_PageUp/PageDown`, `ChangeActionBarPage` |
| Cast | `CastSkillByKeyDown("SKILL_CAST_FORWARD")` etc. — casts the current skill in the forced direction; plain bar casts go through `ActionBar_Cast` |
| Targeting | `SearchNextTarget`, `SelectPrevTarget`, `SelectPlayer`, `SelectTeammate(n)`, `SelectTargetTarget`, `SearchTarget_SwitchOnlyPlayer`, `AttackTarget`, `FollowTarget`, `AutoInteract` (bodies in `target.lua` / panel scripts) |

Control ids (`ui/script/control.lua`):
`0 FORWARD, 1 BACKWARD, 2 TURN_LEFT, 3 TURN_RIGHT, 4 STRAFE_LEFT,
5 STRAFE_RIGHT, 6 CAMERA, 7 OBJECT_STICK_CAMERA, 8 WALK, 9 JUMP,
10 AUTO_RUN, 11 FOLLOW, 12 UP, 13 DOWN`.

## 10. Implementation plan for reborn (design only, no code)

1. `InputCode` — the exact encoding of §3 (parse/format).
2. `HotkeyTable` — load `default.txt` + `bindings.ini`; expose command,
   context, keys, `down`/`up` handler names.
3. `CommandRegistry` — map handler names to C# methods in the same domains
   (movement/camera/action/target/cast), mirroring the bridge table of §9.
4. `HotkeyManager` — press/release, key slots 1–2, contexts, double-press,
   `bUnchangeable`, capture mode, repeat.
5. `BindingStore` — read/write the per-role format of §2.2 (new format first;
   parse the old format for migration), manifest + rotating backups.
6. Rebind UI — port the §7 flow on top of WinForms; reuse `HotkeyPanel.ini`
   grouping.
7. Keep JX3 scopes: bindings are per role; never invent a different file.

Acceptance: every currently hardcoded key works through the table; rebinding a
movement + an action-bar key works and survives restart; a backup restores;
old-format import works; contexts switch with the UI state.

## 11. Open items

1. Engine-side conflict resolution when two commands share a key (C-side, not
   in Lua).
2. `ResponseWASDKey`, `Scene_EnableFreeMoveControl`, `IsKeyDoubleDown` bodies
   (engine globals).
3. Default binding of `ATTACKTARGET` and the remaining empty rows (no default).
4. Repeat timing constants (`GetKeyTimeInterval`) for UI list scrolling.
5. `bSaveToServer` upload protocol/endpoint.

## 12. Evidence index

| Path | Content |
|---|---|
| `proof/movement/extracted/ui_hotkey_default.txt` | raw default table |
| `proof/movement/extracted/ui_hotkey_bindings.ini` | raw command registry |
| `proof/controls/userdata_hotkey/*` | real per-role overrides + manifest |
| `proof/controls/HotkeyPanel.ini` / `.lua` (bytecode) | rebind panel |
| `proof/controls/hotkey_default.md` / `.tsv` | decoded default annex |
| `proof/controls/hotkey_registry_by_context.md` / `.tsv` | decoded registry annex |
| `proof/controls/registry_summary.txt` | counts/groups/handler samples |
| `tools/controls/hotkey_parse.py`, `registry_summary.py` | generators |
| `docs/controls/JX3_HOTKEY_SYSTEM.md` | short-form companion |
| `docs/controls/RESEARCH_RESOLVED_GAPS.md` §1–§2 | rebind/persistence detail |
