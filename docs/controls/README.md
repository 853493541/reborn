# Control system notes — index

**Branch:** `control-system-notes` · **Base:** `camara-fix` @ `00f1237` · **Date:** 2026-09-25
**Purpose:** write down everything found about the JX3 user-control system and the
state of our own client, so the full control system can be implemented later.
**This branch contains documentation only — no code changes.**

---

## Documents

| File | Content |
|---|---|
| `controls/PLAYER_CONTROLS_FINDINGS.md` | consolidated findings: every user input (hotkeys, movement, camera, combat, UI) + our client status |
| `docs/camera/FIX_SUGGESTIONS.md` (repo `docs/`) | post-fix camera analysis: S1–S9 remaining defects with suggested fixes |
| `docs/camera/CONFORMANCE_CHECKS.md` (repo `docs/`) | notes-vs-code checklist: every item with status, evidence, acceptance |
| `docs/camera/DISTANCE_FOV_SPEC.md` (repo `docs/`) | implementation spec for 镜头最大距离 + 广角 (read-only settings) |
| `controls/RESEARCH_RESOLVED_GAPS.md` | 2026-09-25 pass: hotkey override files, rebind UI flow, 广角 mapping, operation modes, movement Lua bridge, action-bar/UI-custom storage |
| `controls/HOTKEY_SYSTEM_FULL.md` | **full specification of the hotkey/input system** (the top topic), with complete decoded annexes |
| `GAME_SYSTEMS_RESEARCH_MAP.md` (repo `docs/`) | all game systems + research backlog and priorities |
| `controls/JX3_HOTKEY_SYSTEM.md` | the hotkey manager, file formats, key encoding, 428 commands, rebinding |
| `controls/JX3_MOVEMENT_CONTROLS.md` | movement keys and the movement/turn model |
| `controls/JX3_CAMERA_CONTROLS.md` | camera inputs, drag pipeline, modes, settings, obstruction |
| `controls/JX3_COMBAT_CONTROLS.md` | targeting, action bars, cast input, server authority |
| `controls/JX3_TARGET_SELECTION.md` | **target selection mechanics**: 3 cone zones (radius/angle/SelLevel), `KPlayer::LuaSearchForEnemy` disasm, filter/sort order, Tab cycle, click pick, reborn implementation plan |
| `controls/JX3_UI_CUSTOMIZATION.md` | counts and mechanics of user customization |
| `controls/REBORN_CONTROLS_SPEC.md` | our target architecture (design only) + phase plan |
| `controls/OPERATION_MODES_PLAN.md` | CLASSICAL/JOYSTICK operation modes — game truth, routing matrix, switch key, P0–P4 |
| `controls/CONTROLS_GAP_REGISTER.md` | every open gap with ID, status, dependency |

## The one-paragraph summary

JX3's controls are **data-driven**: a 428-command hotkey table (2 keys + context +
`down`/`up` Lua per command, Ctrl/Shift/Alt modifiers, per-role override files)
feeds a server-authoritative movement/combat model; the camera is a mouse-drag
orbit around the character with per-user settings; roughly 340 action-bar slots,
~60 panel toggles and 202 persisted window-state keys are user-customizable.
Our client currently has hardcoded keys, a working camera drag (JX3-exact
sphere offset + per-frame aim sync) but **no wall obstruction**, no hotkey
table/rebinding, no action bars/targeting/cast model, and no UI customization.
Research is ~85–90% complete; implementation ~30–35%.

## Evidence base (already in the repo)

| Path | Content |
|---|---|
| `proof/movement/extracted/ui_hotkey_default.txt` | real default bindings |
| `proof/movement/extracted/ui_hotkey_bindings.ini` | 428 command definitions |
| `docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md` | movement, turning, key encoding |
| `docs/camera/INPUT_CONTROLS.md` | camera bindings and drag pipeline |
| `docs/camera/DRAG_MODEL.md` | proven mouse→yaw/pitch mapping |
| `docs/camera/WALL_OBSTRUCTION.md` | native wall obstruction rules |
| `docs/camera/REAL_VALUES.md`, `docs/camera/CONFIG_FILES.md` | camera values/settings/files |
| `docs/movement/REBORN_JUMP_FALL_SPEC.md` | jump/fall/轻功 model |
| `docs/pvp/JX3_PVP_BATTLE_RESEARCH.md` | combat/targeting/cooldowns |
| `docs/ui/MAP_MINIMAP_RESEARCH.md` §2b | `custom.dat` window state |

## Rules for implementing later

1. Load the real JX3 files where possible (`default.txt`, `bindings.ini`,
   `custom.dat`, `config.ini`) — never invent values that evidence already pins.
2. Mirror JX3 scopes: per-role settings live in `custom.dat`, per-install
   settings (广角/FOV) in `config.ini`; read-only until the settings UI is built.
3. Server owns combat/movement truth; the client sends intents and predicts
   animation/UI only.
4. Every fix gets a note here + an acceptance test before code.
