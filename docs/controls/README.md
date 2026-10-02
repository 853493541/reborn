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
| `controls/JX3_UI_CUSTOMIZATION.md` | counts and mechanics of user customization |
| `controls/REBORN_CONTROLS_SPEC.md` | our target architecture (design only) + phase plan |
| `controls/OPERATION_MODES_PLAN.md` | CLASSICAL/JOYSTICK operation modes — game truth, routing matrix, switch key, P0–P4 |
| `controls/CONTROLS_GAP_REGISTER.md` | every open gap with ID, status, dependency |
| `controls/CLASSIC_CONTROLS_AUDIT.md` | classic-mode control surface audit: shipped default bindings vs client handlers (2026-10-01) |
| `controls/CONTROL_MODES_TRACEABILITY.md` | **live matrix for the full CLASSICAL+JOYSTICK decode (incl. animation)** — layers, rows, gaps G1–G10 / A1–A20, verification plan (2026-10-02) |
| `controls/CONTROL_MODES_LUA_ANNEX.md` | **P1 Lua annex** — decoded control handler bodies: mode wrapper, `ResponseWASDKey` joystick vector, mode apply/toggle/persistence (2026-10-02) |
| `controls/CONTROL_MODES_P3_STATIC.md` | **P3 static** — thunk table, script-API wrappers, input apply chain; dynamic probe plan (2026-10-02) |
| controls/CONTROL_MODES_P5_ANIM.md | **P5 animation** — locomotion selection algorithm (speed-tier thresholds, transition clip sets, two slots; param lookup next) (2026-10-02) |

## The one-paragraph summary

JX3's controls are **data-driven**: a 428-command hotkey table (2 keys + context +
`down`/`up` Lua per command, Ctrl/Shift/Alt modifiers, per-role override files)
feeds a server-authoritative movement/combat model; the camera is a mouse-drag
orbit around the character with per-user settings; roughly 340 action-bar slots,
~60 panel toggles and 202 persisted window-state keys are user-customizable.
Our client now decodes the real binding table and dispatches the movement
command set from it (turn-in-place, autorun, jump, strafe; 2026-09-30) and has a
working camera drag (JX3-exact sphere offset + per-frame aim sync), but **no wall
obstruction**, no rebinding UI, no action bars/targeting/cast model, and no UI
customization. Research is ~85–90% complete; implementation ~35–40%.

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

## Tools

| Tool | What |
|---|---|
| `tools/controls/hotkey_parse.py` | decode `ui/hotkey/default.txt` + `bindings.ini` into the proof annexes; `--movement-check` prints and asserts the decoded movement key map (offline gate for C1/C2) |
| `tools/controls/registry_summary.py` | summarise the generated hotkey command registry (context groups/categories) |
| `tools/controls/lua51_probe.py` | instruction-level Lua 5.1 bytecode probe (packed UI scripts) |
| `tools/controls/lua_index.py` | batch proto/name/global index for a directory of packed UI scripts (P1 substrate; writes `proof/controls/lua_index_*.txt`) |

## Rules for implementing later

1. Load the real JX3 files where possible (`default.txt`, `bindings.ini`,
   `custom.dat`, `config.ini`) — never invent values that evidence already pins.
2. Mirror JX3 scopes: per-role settings live in `custom.dat`, per-install
   settings (广角/FOV) in `config.ini`; read-only until the settings UI is built.
3. Server owns combat/movement truth; the client sends intents and predicts
   animation/UI only.
4. Every fix gets a note here + an acceptance test before code.
