# JX3 UI customization — how much the user can change

**Evidence:** `docs/MAP_MINIMAP_RESEARCH.md` §2b,
`docs/CAMERA_CONFIG_FILES.md` §7, `docs/CAMERA_REAL_VALUES.md` §2,
`proof/movement/extracted/ui_hotkey_default.txt`,
`proof/minimap/recon/ui_config_inventory.txt`.

---

## 1. Counts

| Surface | Count | Storage |
|---|---|---|
| Hotkey commands | 428 sections (2 keys + context each) | `ui/hotkey/*` + per-role `hotkey*.txt` |
| Action-bar slots | ~340 (bars 1–11 + 20, dynamic 2×32 + 32 hang, rogue 6, BR 14, NPC/vehicle 12, school pose ~9) | per-role layout **[MED]** |
| Action pages | 5 | runtime |
| Panel/UI toggles | 66 commands | hotkeys |
| UI window states | 202 keys | `custom.dat` |
| Camera panel | 6 numeric + 3 booleans + 2 operation-mode keys | per-role `custom.dat` |
| Video panel | `[UIVideoSetting]` + `fCameraAngle` (广角) | per-install `config.ini` |
| Bags | 5 bag toggles + open/close all | hotkeys |

## 2. Hotkey rebinding UI

- `Esc` opens the option panel (`TOGGLE_OPTION_PANEL`).
- Lua API: `Lua_SetCapture` (capture next key), `Lua_AddBinding`,
  `Lua_GetBinding`, `Lua_GetHotKey`.
- Key format: VK + modifier word (Ctrl 1 / Shift 2 / Alt 4); mouse 1/2/256/257.
- Per-role overrides saved under `userdata/<account>/<region>/<server>/<role>/`.
- **Resolved 2026-09-25** (detail `controls/RESEARCH_RESOLVED_GAPS.md` §1–§2):
  files `hotkey_newlast.txt` (`name context index key`), `hotkey_newbackN.txt`
  backups, `hotkey.data` manifest; two slots per command; double-press bindings;
  `bUnchangeable` commands; self/auto-modified row frames; save also to server
  (`bSaveToServer=true`).

## 3. Action bars

- Drag skill from skill panel onto a slot; pages switch the whole bar set.
- Dynamic bars are contextual (vehicles, pets, rogue, BR loot).
- Bar lock toggle; slot keys bindable like any command.
- **Resolved 2026-09-25** (detail `controls/RESEARCH_RESOLVED_GAPS.md` §6):
  layout/anchor custom data `ActionBar.{nPage,bLock,aShowBg,Anchor,ExtendAnchor,
  AnchorTop,Size,Line}` with default anchors `BOTTOMCENTER` on
  `Lowest1/MainBarPanel`, `x=26`, `y=-10/-58/-118/-166/-213`;
  slot contents persist via `StorageServer.SetData('ActionBar',
  GetActionBarKey(...), …)`, page from `GetMainActionBarPage()`, `_LockPage`;
  cast entry `ActionBar_ButtonDown/Up` → `ActionBar_Cast`.

## 4. Window layout (`custom.dat`)

- 202 window state keys: position/alpha/visible/flags per window.
- Examples: `Minimap.bOpen`, `RadarType`, `MiddleMap.nAlpha`,
  `BattleFieldMap.Anchor = {x,y,s,r}` (the only draggable map),
  `WorldMap.bFirstOpen`.
- `TOGGLE_UI` (Ctrl+U) hides/shows the HUD; `TOGGLE_UI_CUSTOM_MODE`
  enables the move/scale layout mode.
- Window definitions live in `ui/Config/Default/*.ini` + compiled `.lua`
  (253 files found via the `custom.dat` window-name dictionary).
- **Resolved 2026-09-25** (detail `controls/RESEARCH_RESOLVED_GAPS.md` §7):
  custom mode = `UICustomModePanel` with anchor custom data
  `UICustomModePanel.Anchor={s,r,x,y}`; entering fires `ON_ENTER_CUSTOM_UI_MODE`
  and gives every registered window `EnableDrag`/`SetDragArea`/
  `SetMousePenetrable` (previous state stored in
  `__bIsDragableBeforeEnterUICustomMode`); `Btn_Default` fires
  `CUSTOM_UI_MODE_SET_DEFAULT`. `UpdateCustomModeWindow` is the per-window hook.

## 5. Settings panels and scopes

| Panel | Keys | File | Scope |
|---|---|---|---|
| Camera/mouse | drag speeds, max distance, spring/reset speeds, follow mode | `custom.dat` | per role |
| Operation mode | classic/joystick camera mode | `custom.dat` | per role |
| Video | `[UIVideoSetting]`, `fCameraAngle` (广角) | `config.ini` | per install |
| Graphics presets | `config_1_zuijian … config_9_chenjin`, `_bd_*` | `config/*.ini` | per install |

## 6. Not user-adjustable (compiled/inaccessible)

- engine `[Camera]` ini (`bObstructdAvert`, `fChaseRate`, `fFovy`, …) not on disk
- per-mode camera row tables (`camera_common.krl.txt`, `AirCombatCamera`, …)
- engine caps `fMinCameraDistance`, `fMinCameraAngle`, `fMaxCameraAngle`
  (compiled per graphics option level)

## 7. Our client today

HUD label + `I` toggle only. No panels, no rebinding, no layout persistence,
no settings write path. Target design: `controls/REBORN_CONTROLS_SPEC.md`
§UI and `CAMERA_DISTANCE_FOV_SPEC.md` for camera/video settings.
