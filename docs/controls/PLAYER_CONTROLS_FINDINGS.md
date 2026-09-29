# JX3 player controls — consolidated research findings

**Branch:** `camara-fix` · **Date:** 2026-09-24
**Scope:** every user input in the real client — hotkeys, movement, camera, jump,
combat, UI — plus the current state of our client.
**Method:** static extraction from the local install (PakV4 UI files,
`userdata/.../custom.dat`, `config.ini`) and targeted disassembly, already
recorded in `docs/` and `proof/`. This file consolidates the control surface;
it adds no new reverse engineering.
**Confidence:** **[HIGH]** direct evidence cited · **[MED]** strongly implied ·
**[GAP]** open item.

---

## 0. How much is user-customizable (the short answer)

| Surface | Count / scope | Where it is stored |
|---|---|---|
| Hotkey commands | **428** command sections in `ui/hotkey/bindings.ini`; 286 rows in `ui/hotkey/default.txt` (2 keys + context each; `down`/`up`/`runOnUp`) | shipped pak; per-role overrides in `userdata/<account>/<region>/<server>/<role>/hotkey*.txt` |
| Action-bar bindable slots | bars 1–11 + 20 + dynamic (2 × up to 32, hang 32) + rogue 6 + BR 14 + NPC/vehicle 12 + school pose ~9 → **~340 slots** | per-role action-bar layout (skill assignment), `custom.dat` family **[MED]** |
| Action pages | 5 (`Shift+1..5`, `PgUp`/`PgDn`) | runtime |
| Panel/UI toggles | ~60 commands (`TOGGLE_*`, bags, maps) | hotkeys |
| UI window layout | **202 window state keys** in `custom.dat` (anchors, alpha, radar, flags) | `userdata/.../custom.dat` |
| Camera panel | 6 numeric + 3 boolean + 2 operation-mode keys | per-role `custom.dat` → `VideoSettingPanel.tCameraStatic` etc. |
| Video panel (incl. 广角) | `[UIVideoSetting]` + engine option `fCameraAngle` | per-install `config.ini` (all characters) |
| Operation mode | classic / joystick | per-role `custom.dat` |

Sources: `proof/movement/extracted/ui_hotkey_default.txt`,
`ui_hotkey_bindings.ini`, `docs/ui/MAP_MINIMAP_RESEARCH.md` §2b,
`docs/camera/CONFIG_FILES.md` §7, `docs/camera/REAL_VALUES.md` §2.

---

## 1. Input layer (the hotkey system)

| Item | Finding |
|---|---|
| Manager | `UI::KHotkeyMgr` (`KGUIX64.dll`): `Init/Load/Save/GetBinding/SearchBinding/RefreshKeyDownData/LoadDefault/LoadBinding` |
| Default bindings | `ui/hotkey/default.txt`, tab columns `name context key1 key2` |
| Command definitions | `ui/hotkey/bindings.ini` — `[COMMAND]` with `desc`, `down=`, `up=`, optional `runOnUp` |
| Key encoding | low 16 bits = virtual-key; high 16 bits = modifier word: `0x1` Ctrl, `0x2` Shift, `0x4` Alt. Mouse: `1` LMB, `2` RMB, `256` wheel up, `257` wheel down |
| Contexts | same key can do different things per context: normal, `ExtentDynamicAction`, `NpcAssistedDynamicAction`, `RougeDynamicAction`, `DynamicBattleRoyale`, `minigame`, `MobileSkillActionBar` |
| Repeat | `Hotkey_EnableKeyDownLoop`, `GetKeyTimeInterval`, `Hotkey_EnableNewMode` |
| Rebind UI | `Lua_SetCapture` / `Lua_AddBinding` / `Lua_GetBinding` + the option panel (`TOGGLE_OPTION_PANEL`, Esc) |
| Save | per-role override file `userdata/.../hotkey*.txt` (TSV `name index key`, empty = default) **[MED]** |

**Examples decoded:** `ACTIONBAR2_BUTTON1 = 262193 = 0x40031` → Alt+1;
`TOGGLE_UI = 65621 = 0x10055` → Ctrl+U; `SKILL_CAST_FORWARD = 0x40057` → Alt+W.

Evidence: `docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md` §2,
`docs/camera/INPUT_CONTROLS.md` §1–2.

---

## 2. Movement controls

| Command | Default | Effect |
|---|---|---|
| `MOVEFORWARD` | `W` / `↑` | forward along the camera axis |
| `MOVEBACKWARD` | `S` / `↓` | back |
| `STRAFELEFT` / `STRAFERIGHT` | `A` / `D` | strafe |
| `TURNLEFT` / `TURNRIGHT` | `←` / `→` | turn in place |
| `JUMP` | `Space` | jump; held/pressed in air → 轻功 multi-jump chain |
| `TOGGLERUN` | Numpad `/` | walk ⇄ run toggle (there is no hold-to-run key) |
| `TOGGLEAUTORUN` | `G` / NumLock | keep moving forward without holding W |
| `RIDEHORSE` | `T` | mount / dismount |
| `TOGGLESHEATH` | `Z` | sheathe / draw weapon |
| `TOGGLESITDOWN` | `V` / `X` | sit / stand |
| `FOLLOWTARGET` | Ctrl+G | auto-follow target |
| `AUTOINTERACT` | `F` | interact with nearest object |
| — | double-tap `W` | sprint (host addition; real client sprint is a 轻功/skill state) [MED] |
| — | mouse click on ground | click-to-move (`AutoMoveToPoint` / `AutoMoveToTarget`) |

Model behind the keys: server-authoritative, client-predicted. The logic
character keeps a move state (`2` walk, `3` run, `4` jump, `7` swim, `0x17`
sprint dash), heading (`atan2`), facing (byte 0..255) and turn rate; a turn
> 112.5° in one frame halves speed. Walk 6 / run 20 world-units per logic frame
(1/16 s); 1 unit = 1 cm.

Jump: Space takeoff triple from `JumpParam.tab`, gravity per frame, landing
thresholds (500 u roll / water variants), fall death server-side. Full spec:
`docs/movement/REBORN_JUMP_FALL_SPEC.md`.

Evidence: `docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md` §2.3–§3,
`docs/movement/REBORN_JUMP_FALL_SPEC.md`.

---

## 3. Camera controls

| Input | Command | Effect |
|---|---|---|
| LMB drag | `CAMERAORSELECTORMOVE` | rotate camera only; a **click without drag selects** |
| RMB drag | `CAMERAORSELECTORMOVESTICKY` | rotate camera **and turn the character** |
| Wheel up / down | `CAMERAZOOMIN/OUT` | distance × 0.9 / × 1.1, clamped |
| F11 | `CAMERARESET` | behind character, pitch −15°, distance scale 1 |
| Home / End | `CAMERA_SET_VIEW_1/2` | view preset behind / front |
| (unbound) | `CAMERAUP/DOWN` | pitch by key |

Drag pipeline (real client): `ComputeMouseMoveDelta` → `ApplyMouse`
(delta × 2π) → `ClampMouse` (controller row drag/rotation speeds, per-mode
variants) → controller yaw `+0x20` / pitch `+0x24`; pitch clamped to
`|pitch| < π/2 − 0.0157`. The UI Lua only chooses the mode, zoom and reset.
Camera position = anchor + constant-length offset rotated by yaw/pitch + row
height (no `tan`).

Settings: per-role `custom.dat` (`fMaxCameraDistance` default 2000 u = 20 m,
`fDragSpeed`, `fDragPitchSpeed`, `fSpringResetSpeed`, `fCameraResetSpeed`,
`nCameraMode`; saved view `fYaw`/`fPitch`/`fCameraToObjectEyeScale`);
per-install `config.ini` `[KG3DENGINE] CammeraAngle = 0.837757` (广角/FOV,
video panel). Operation modes: classic (hold button) vs joystick (mouse always
rotates, `Scene_LockMouseRotation`). Per-map init from `scene_init_param.txt`.

**镜头最大距离** caps wheel zoom-out and scales the DLL zoom step;
**广角** is projection-only and is overridden per skill/mode by the
skill-move camera tables. Details: `docs/camera/REAL_VALUES.md`,
`docs/camera/CONFIG_FILES.md`, `docs/camera/INPUT_CONTROLS.md`,
`docs/camera/DRAG_MODEL.md`.

---

## 4. Combat controls

| Group | Finding |
|---|---|
| Targeting | client-side only (no opcode): click / mouseover, `Tab` cycle enemy, `Ctrl+Tab` previous, `F1` self, `F2–F5` teammates, target-of-target, only-player filter |
| Action bars | 4 main bars × 16 (`1..0,-,=`, `Alt+1..`), 5 pages; extra bars 5–11; dynamic bars for vehicles/pets/rogue; BR bar (`DynamicBattleRoyale`, 14); talents sets `TALENT_SET1..5` |
| Casting | keydown cast (`CastSkillByKeyDown`) or bar button; `Alt+W/A/S/D` = skill direction; instant vs prepared vs channel from `skills.tab`; GCD = cooldown row 16 (1.5 s) |
| Facing | auto-face target (`LuaTurnToCharacter`), server turn range, skill turning speeds in `number.krl` |
| Authority | cast legality, cooldowns/charges/overdraft, resources, damage/heal, CC/DR, death — server; client predicts animation/UI only |

Evidence: `docs/pvp/JX3_PVP_BATTLE_RESEARCH.md`,
`docs/pvp/REBORN_PVP_BATTLE_SPEC.md`, `proof/movement/extracted/ui_hotkey_default.txt`.

---

## 5. UI toggles and customization

- ~60 panel commands: bags (`B` and `TOGGLEBAG1..5`), character (`C`), skills
  (`P`), quest (`L`), map (`M`), world map, friends (`O`), guild (`J`), craft,
  achievements, arena, etc.
- `Ctrl+U` toggle UI, `Shift+U` UI custom mode (move/scale windows),
  `TOGGLEACTIONBARLOCK`, action pages.
- Per-window state (position/alpha/visibility) in `custom.dat`; the
  battlefield map is the draggable one (`BattleFieldMap.Anchor`).
- `Escape` opens the option panel, which hosts rebinding and the camera/video
  sliders.

Evidence: `proof/movement/extracted/ui_hotkey_default.txt`,
`docs/ui/MAP_MINIMAP_RESEARCH.md` §2b, `docs/camera/CONFIG_FILES.md` §7.

---

## 6. Our client today (`client/RebornClient.cs`)

| Area | State |
|---|---|
| Input | hardcoded `KeyDown/KeyUp` if-chain — **no hotkey table, no rebinding, no contexts** |
| Movement | WASD camera-relative, Shift debug ×10, `Space` jump, `/` walk-run, W-double-tap sprint, `1` skill, `C` debug teleport; world-dir held while keys unchanged |
| Turn | body turns instantly to the move direction / RMB camera direction (**no turn-rate model**) |
| Camera | LMB/RMB drag, wheel ×0.9/×1.1, F11, Home/End; JX3 sphere offset; per-frame aim sync (`00f1237`); **no wall obstruction**, no FOV/modes/shake; settings read from `custom.dat` (3 keys) |
| Jump | continuous approximation tuned to the table numbers (integer 15 Hz model **not ported**) |
| Combat | one skill key (`1`), no targeting, no action bars, no cast model |
| UI | HUD label + `I` toggle only; no panels, no customization |
| Settings | read-only `custom.dat`; `config.ini`/广角 ignored; engine caps not probed |

### Coverage

| Subsystem | Research | Implemented |
|---|---|---|
| Camera drag/aim | ~95% | ~85% (walls/FOV/caps/modes missing) |
| Camera settings | ~80% | ~40% |
| Movement | ~90% | ~50% |
| Input/hotkeys | ~95% | ~10% |
| Combat controls | ~85% | ~5% |
| UI customization | ~70% | ~5% |
| Jump/轻功 | ~90% | ~40% |
| Netcode | ~85% | 0% |

Open gaps for the full control system are consolidated in
`docs/controls/CONTROLS_GAP_REGISTER.md`; the camera-specific status is in
`docs/camera/CONFORMANCE_CHECKS.md`.

---

## 7. Evidence index

| Path | Content |
|---|---|
| `proof/movement/extracted/ui_hotkey_default.txt` | real default bindings |
| `proof/movement/extracted/ui_hotkey_bindings.ini` | 428 command definitions |
| `docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md` | movement + turning + hotkey encoding |
| `docs/camera/INPUT_CONTROLS.md` | camera bindings and drag pipeline |
| `docs/camera/REAL_VALUES.md`, `docs/camera/CONFIG_FILES.md` | camera values, settings, files |
| `docs/camera/DRAG_MODEL.md` | proven mouse → yaw/pitch mapping |
| `docs/movement/REBORN_JUMP_FALL_SPEC.md` | jump/fall/轻功 model |
| `docs/pvp/JX3_PVP_BATTLE_RESEARCH.md` | combat, targeting, cooldowns |
| `proof/netcode/camera_settings_real.txt` | per-user settings + zoom step |
| `docs/ui/MAP_MINIMAP_RESEARCH.md` §2b | per-window `custom.dat` state |

---

## 8. The notes set (branch `control-system-notes`)

| File | Content |
|---|---|
| `docs/controls/README.md` | index + summary |
| `docs/controls/JX3_HOTKEY_SYSTEM.md` | hotkey manager, files, 428 commands, rebinding |
| `docs/controls/JX3_MOVEMENT_CONTROLS.md` | movement keys + movement/turn model |
| `docs/controls/JX3_CAMERA_CONTROLS.md` | camera inputs, drag pipeline, modes, obstruction |
| `docs/controls/JX3_COMBAT_CONTROLS.md` | targeting, action bars, cast input, authority |
| `docs/controls/JX3_UI_CUSTOMIZATION.md` | customization counts and mechanics |
| `docs/controls/REBORN_CONTROLS_SPEC.md` | target architecture (design only) + phases |
| `docs/controls/CONTROLS_GAP_REGISTER.md` | master live checklist |
| `docs/camera/FIX_SUGGESTIONS.md` | camera defects S1–S9 + suggested fixes |
| `docs/camera/CONFORMANCE_CHECKS.md` | notes-vs-code matrix with acceptance |
| `docs/camera/DISTANCE_FOV_SPEC.md` | 镜头最大距离 + 广角 spec |
