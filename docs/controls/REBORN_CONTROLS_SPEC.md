# reborn full user-control system — design spec (no implementation)

**Status:** design only. This branch contains notes; it must not change code.
**Base client:** `client/RebornClient.cs` (hardcoded input) +
`client/CameraSystem.cs` / `CameraSettings.cs` (camera subset).
**Goal:** reproduce the JX3 control surface: data-driven hotkeys, movement,
camera, combat and UI customization.

---

## 1. Principles

1. **Load the real JX3 data** where possible (`default.txt`, `bindings.ini`,
   per-role `hotkey*.txt`, `custom.dat`, `config.ini`); invent only where
   evidence is missing, and mark it.
2. **Mirror JX3 scopes:** per-role settings in `custom.dat`, per-install in
   `config.ini`; read-only until the settings UI exists.
3. **Server authority:** the client sends intents (move/cast), predicts
   animation/UI only; no local damage/buff state.
4. **One state, one owner:** the camera/aim state has a single writer (the
   per-frame aim sync pattern proven in `docs/CAMERA_DRAG_MODEL.md`).
5. Every behaviour is testable without the engine where possible (pure model +
   smoke tests), with the engine host as the integration layer.

## 2. Components (proposed files under `client/`)

| Component | Responsibility | JX3 counterpart |
|---|---|---|
| `InputCode` | VK + modifier + mouse/wheel encoding, parse/serialize | KGUI key encoding |
| `HotkeyTable` | load `default.txt` + `bindings.ini`; command, context, keys, down/up Lua names | `KHotkeyMgr::Load` |
| `HotkeyManager` | per-frame key state, press/release, repeat (`EnableKeyDownLoop`), capture mode, context switching, conflict resolution | `KHotkeyMgr` |
| `CommandRegistry` | command name → handler(delegate); the down/up dispatch table | UI Lua commands |
| `BindingStore` | per-role override read/write (TSV `name index key`) | `userdata/.../hotkey*.txt` |
| `ActionBars` | bars/pages/dynamic contexts, slot → skill assignment, lock | `ACTIONBAR*` commands |
| `Targeting` | current target, Tab/Ctrl+Tab cycle, F1–F5, click/mouseover, filters | client-side target state |
| `CastController` | cast intent (skill, target, aim), directional cast (`Alt+WASD`), cast/channel bar, interrupt on move, GCD display | skill input + predict |
| `ControlSettings` | read `custom.dat` (`tCameraStatic`, runtime view), `config.ini` (`CammeraAngle`, `[UIVideoSetting]`) | camera/video panels |
| `CameraControl` | existing `CameraSystem` + aim loop + obstruction (see `docs/CAMERA_FIX_SUGGESTIONS.md`) | camera controller |

The camera work continues in `CameraSystem`/`RebornClient`; the input/combat
work is new. All intents should funnel through plain structs so M2 netcode can
reuse them:

```
MoveIntent  { input_seq, keys, facing, client_tick }     // OP_MOVE_INPUT
CastIntent  { skill_id, target_id, aim[3], flags }       // OP_CAST_INTENT
TargetIntent{ target_id }                                // OP_TARGET_INTENT
```

## 3. Data model

```text
KeyCode      = (vk:u16, mods:u8)          mods Ctrl=1 Shift=2 Alt=4
MouseCode    = LMB=1 RMB=2 WheelUp=256 WheelDown=257
Binding      = command:string, context:string, key1:KeyCode?, key2:KeyCode?
CommandDef   = name, down:string?, up:string?, runOnUp:bool, desc:string
ActionBar    = id, page, slots[16] -> {kind: skill|item|macro, id}
```

## 4. Phase plan (each phase ships docs + tests first)

### Phase A — input core (no gameplay change)
- `InputCode`, `HotkeyTable`, `CommandRegistry`, `HotkeyManager`,
  `BindingStore`; load the real files; expose a rebinding API; HUD debug listing.
- Acceptance: every currently hardcoded key works through the table; a rebind
  to a different key works; per-role overrides round-trip; conflicts reported.
- Open from notes: override TSV grammar, conflict policy, context switching.

### Phase B — movement controls
- Route movement through `CommandRegistry`; add turn keys, autorun, walk/run
  toggle, sit/mount/sheath, click-to-move, follow/interact; turn-rate model
  (facing interpolation + >112.5° penalty); port the integer jump/gravity model
  (`docs/REBORN_JUMP_FALL_SPEC.md`) and later the 轻功 chain.
- Acceptance: real defaults from `default.txt` drive movement; demo script
  reproduces documented speeds; smoke tests for the turn model.

### Phase C — camera completion
- Fix S1/S2/S3 (aim re-pin, `EyeScale`, ground-clamp guard), wall obstruction
  S9, then 广角/FOV + caps (`CAMERA_DISTANCE_FOV_SPEC.md`), then modes/move-pitch.
- Acceptance: see `CAMERA_CONFORMANCE_CHECKS.md`.

### Phase D — combat controls
- `Targeting` + `ActionBars` + `CastController`; cast intents to the future
  server; animation/UI prediction only; directional cast; GCD/cooldown rendering.
- Acceptance: 4 bars × 16 + pages + dynamic bar; Tab/F1–F5 selection;
  a cast appears locally and sends `CastIntent`; server reject paths handled.

### Phase E — UI customization
- Rebind panel, action-bar assignment UI, camera/video settings panels
  (read-only write path decision), window layout persistence (`custom.dat`).
- Acceptance: settings match the real file scopes and clamps.

### Phase F — netcode hookup (M2)
- `MoveIntent`/`CastIntent`/`TargetIntent` carried on the reborn protocol;
  prediction/reconciliation per `docs/netcode/REBORN_SERVER_SPEC.md`.

## 5. Testing strategy

- **Pure model smoke tests** (no engine): key encode/decode, binding load,
  repeat timing, context switch, bar/page math, turn model, distance clamp,
  aim math with `EyeScale`, obstruction clearance/hysteresis.
- **Scripted runs**: `RC_DEMO`-style key sequences; per-run logs
  (`reborn_<timestamp>.log`); screenshot baselines.
- **Conformance matrix**: `CONTROLS_GAP_REGISTER.md` is the live checklist.

## 6. Explicit non-goals in this branch

No code edits, no new engine features, no settings write path, no UI panels.
This file is the plan those changes will follow.
