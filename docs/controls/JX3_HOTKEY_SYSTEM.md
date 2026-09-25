# JX3 hotkey system (input layer)

**Evidence:** `docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md` §2,
`docs/CAMERA_INPUT_CONTROLS.md` §1–2,
`proof/movement/extracted/ui_hotkey_default.txt`,
`proof/movement/extracted/ui_hotkey_bindings.ini`,
`proof/movement/disasm/kgui_hotkey*.txt`.

---

## 1. Architecture

```
keyboard / mouse
  └─ UI::KHotkeyMgr (KGUIX64.dll)          binding table + key states + repeat
       └─ Lua command (bindings.ini down=/up=)   MoveForwardStart(), Jump(), ...
            └─ engine commit (JX3RepresentX64.dll KGameWorldHandler::CommitInput)
                 └─ character controller → logic character (server-authoritative)
```

Manager API: `Init/Load/Save/GetBinding/SearchBinding/RefreshKeyDownData/LoadDefault/LoadBinding`.
UI Lua accessors: `LuaGetHotKey`, `Lua_Get/Set/IsDown/IsKeyDown/IsUsed`,
`Lua_AddBinding`, `Lua_SetCapture`, `Lua_EnableKeyDownLoop`; repeat via
`GetKeyTimeInterval`, `Hotkey_EnableKeyDownLoop`, `Hotkey_EnableNewMode`.

## 2. Files

| Logical name | File | Content |
|---|---|---|
| default set | `ui/hotkey/default.txt` | `name \t context \t key1 \t key2` |
| command defs | `ui/hotkey/bindings.ini` | `[COMMAND] desc= down= up= runOnUp=` |
| UI file map | `ui/filepath.txt` → `HotkeyBindingSetPath`, `HotkeyBindingsFile` | paths |
| per-role overrides | `userdata/<account>/<region>/<server>/<role>/hotkey*.txt` | TSV `name index key`, empty = default **[MED]** |

## 3. Key encoding (verified from `LoadAsOldAdd`)

- integer per key: **low 16 bits = virtual-key code**, **high 16 bits = modifier word**
- modifier bits: `0x1` Ctrl, `0x2` Shift, `0x4` Alt
- mouse codes: `1` LMB, `2` RMB, `256` wheel up, `257` wheel down
- all other values are Windows `VK_*` (the engine ships the full VK name table)

Examples: `ACTIONBAR2_BUTTON1 = 262193 = 0x40031` → Alt+1;
`TOGGLE_UI = 65621 = 0x10055` → Ctrl+U;
`TOGGLE_NPC = 196686 = 0x3004E` → Ctrl+Shift+N;
`SKILL_CAST_FORWARD = 0x40057` → Alt+W.

## 4. Contexts

Same key can act differently per context (`context` column):

| Context | Use |
|---|---|
| *(empty)* | normal gameplay |
| `ExtentDynamicAction` | dynamic action bar 1 (vehicles/pets/loot) |
| `NpcAssistedDynamicAction` | NPC-assisted bar |
| `RougeDynamicAction` | rogue-like mode bar |
| `DynamicBattleRoyale` | 绝境/BR bar |
| `minigame` | minigames |
| `MobileSkillActionBar` | mobile/VK action bar |

## 5. Command inventory (428 sections, accurate counts)

| Group | Count | Examples |
|---|---|---|
| Action bars / dynamic bars | **288** | `ACTIONBAR1_BUTTON1..16`, `ACTIONBAR2` (Alt+1..), pages, dynamic 32, BR 14, vehicle/NPC/rogue |
| Panel / UI toggles | **66** | `TOGGLE_EQUIP_PANEL (C)`, `TOGGLE_QUEST_PANEL (L)`, `TOGGLE_MIDDLEMAP (M)`, `TOGGLE_UI (Ctrl+U)`, `TOGGLE_UI_CUSTOM_MODE`, `OPENORCLOSEALLBAGS (B)` |
| Other | **26** | `OPENCHAT`, `SCREENSHOT`, `KINESCOPE`, `SHORTCUT_REPLY`, `MINIGAME_*`, `VKACTIONBAR_*` |
| Skill / talent / stance | **18** | `SKILL_CAST_FORWARD/BACK/LEFT/RIGHT`, `SKILL_YYX_*`, `TALENT_SET1..5`, `KUNG_FU_*`, `SUIT_INDEX1..4` |
| Targeting / selection | **11** | `SEARCH_ENEMY (Tab)`, `SELECT_PREV_TARGET (Ctrl+Tab)`, `SELECT_SELF (F1)`, `SELECT_TEAMMATE1..4 (F2–F5)`, `SELECT_TARGETTARGET`, `SELECT_ONLYPLAYER`, `ATTACKTARGET` (unbound) |
| Movement / character | ~15 | `MOVEFORWARD (W/↑)`, `MOVEBACKWARD (S/↓)`, `TURNLEFT/RIGHT (←/→)`, `STRAFELEFT/RIGHT (A/D)`, `JUMP (Space)`, `TOGGLERUN (Num/)`, `TOGGLEAUTORUN (G)`, `RIDEHORSE (T)`, `TOGGLESHEATH (Z)`, `TOGGLESITDOWN (V/X)`, `FOLLOWTARGET (Ctrl+G)`, `AUTOINTERACT (F)` |
| Camera | **9** | `CAMERAORSELECTORMOVE (LMB)`, `CAMERAORSELECTORMOVESTICKY (RMB)`, `CAMERAZOOMIN/OUT (wheel)`, `CAMERARESET (F11)`, `CAMERA_SET_VIEW_1/2 (Home/End)`, `CAMERAUP/DOWN` (unbound) |

## 6. Rebind / capture / repeat

- capture mode for the settings UI: `Lua_SetCapture`
- add/read/clear bindings: `Lua_AddBinding`, `Lua_GetBinding`
- key-down loop (hold repeat): `Hotkey_EnableKeyDownLoop`, `GetKeyTimeInterval`
- `runOnUp=1` commands fire their `down` on release (`STRAFERIGHT` ships it)
- per-role save: TSV with `name index key`; empty key = fall back to default **[MED]**

## 7. Our client today

Hardcoded `KeyDown/KeyUp` if-chain in `client/RebornClient.cs` — no table, no
contexts, no modifiers, no rebinding, no per-role file. Target design:
`controls/REBORN_CONTROLS_SPEC.md` §Input.

## 8. Open items

1. Exact per-role override TSV grammar (index = key slot? context?).
2. Conflict resolution when two commands bind the same key.
3. `context` switching rules (which UI state activates each context).
4. Rebind UI panel layout (packed Lua).
