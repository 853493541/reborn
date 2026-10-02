# Control modes full decode — traceability matrix (CLASSICAL + JOYSTICK, incl. animation)

**Goal:** one client-derived model of both operation modes where every behavior
traces **binding → Lua handler → UI C binding → exe forwarding → engine consumer
→ move state → animation clip/selection → data value → scripted verification**.
Host observations are verification only, never evidence.

**Client:** `C:\SeasunGame\Game\JX3\bin\zhcn_hd` (1.5.0.9975, `GameInfo.dat`).
**Host-loaded engine:** `C:\SeasunGame\MovieEditor` (build-diff every probe function).
**Status legend:** `DONE` (client-cited) · `PART` (some layers verified) · `OPEN(Pn)`
(owner phase) · `N/A` (not applicable to the mode) · `SERVER` (server-owned).

Owner phases follow the full plan: P1 Lua · P2 UI C bindings · P3 exe boundary ·
P4 engine semantics · P5 animation resources/state map · P6 selection/blend ·
P7 data · P8 verification · P9 publish.

---

## 1. Layer ledger (modules, anchors already decoded)

| Layer | Module | Known anchors (HIGH unless marked) |
|---|---|---|
| Bindings | paks `ui/hotkey/default.txt` + `bindings.ini` | 286 bound command rows; `proof/movement/extracted/ui_hotkey_default.txt` |
| Lua handlers | packed UI scripts (Lua 5.1, `LuaQ`) | `hotkeys.lua`, `control.lua`, `mainscene.lua`, `Scene.lua`, `OperationModeBase.lua`, `CameraCommon.lua`, `OBDungeon.lua`; probe `tools/controls/lua51_probe.py` |
| UI C bindings | `JX3UIX64.dll` (`KRepresentScriptTable::Lua*`) | `Camera_EnableControl 0x1800AC1F0`, `Camera_BeginDrag 0x1800ABFD0`, `Camera_LockControl 0x1800ACEE0`, `MouseControlMoveEnable 0x1800ACF90`, `Camera_SetResetSpeed` (RVA pending) |
| Exe forwarding | `JX3ClientX64.exe` `KEventCommonMgr` | world vtable `0x180CC21E8`: `BeginDragCamera +0x3D0`, `EndDragCamera +0x3D8`, `SetCameraDragParams +0x3E8`, `ForceResetCamera +0x428`, `EnableControlCamera +0x450`; `+0x7D0` → thunk `0x180004070` → `HandleRLAction 0x1802F54B0` → action table `0x180E96C00` (6 = control enable `0x1802F5E50`, 7 = drag state `0x1802F5400`); property store `[SO3+0x25F08]` |
| Engine: input/character | `JX3RepresentX64.dll` | `AjustCtrlInput 0x1805E1ED0` → controller `0x1805DF350` → applier `0x1805DF7E0` (8 event types → `+0x7C..+0x98`); `GetMoveInfo 0x1805DFE90` (`+0x50` fwd, `+0x4C` strafe, `+0x3C` rot); setters Move `0x1805E0010`, Jump `0x1805DFFF0`, clear `0x1805E0030`; `CommitInput 0x1805E3270` |
| Engine: camera | `JX3RepresentX64.dll` | `MouseMove 0x180B21300`, `ApplyMouse 0x180B1F520`, `ClampMouse 0x180B1FE70`, `ApplyRotation 0x180B1FA30`, `UpdateRotation 0x180B22740`, alt applier `0x180B211D0`; manager `+0x20` char, `+0x5C` state, `+0x90..0x9C` accumulators, `+0x1A8`, `+0x1AC`, `+0x1B0`, `+0x25C`, `+0x2D0/+0x2F0` controllers; per-mode node fields `+0x6C..+0x98`; rows from `[0x180EDDFE0+0x262F0]` (`CameraAdjustYawWhenMoveTurn +0x14`, `DisableAngle +0x18`, …) |
| Engine: animation | `JX3RepresentX64.dll` | `UpdateDirection 0x180533D40` (`KeepTurningFrame +0x40`, `TurningEpsilon +0x50`); `UpdateMoveAnimation 0x1804C0700` (thresholds `[param+0x50]/[param+0x68]` → `+0x30/+0x34`, slots `+0x1B8/+0x1C0`); face yaw `[KRLLocalCharacter+0x30]` setter `0x180530F00` (thunk `0x18001F05F`), `UpdateFaceFootDirection` (assert `0x180534699`); move-state vocabulary table `0x00CBCF08` |
| Data | paks / krl | `player_animation_f1.txt` (KindID map), `number.krl` (walk `+0x48`, run `+0x4C`, yawTurn `+0x54`), camera rows, `config.ini`, `custom.dat` |

---

## 2. Movement & character inputs

| # | Item | Client chain (evidence) | Animation | Status |
|---|---|---|---|---|
| M1 | Binding table loaded (286 rows) | C1: `HotkeyTable.cs`; `default.txt` + `bindings.ini` | — | DONE (C1/C2) |
| M2 | MOVEFORWARD (W/Up) | **handlers decoded** (A6.5): `HoldW` + wrapper + `ResponseWASDKey('Forward',…)` (double-tap gated by `0/62`); stop → `CheckEndSprint` | run/walk clip (§6) | PART: engine direction application OPEN(P4, G6) |
| M3 | MOVEBACKWARD (S/Down) | **handlers decoded** (A6.5): OB wrapper short-circuit; dead skill flag; double-tap; `ResponseWASDKey('Backward',…)`; wrapper fallback; `CheckEndSprint` | kind 57/58 | PART: Lua DONE, cancel-set engine OPEN(P4) |
| M4 | STRAFELEFT/RIGHT (A/D) | **decoded (annex A6/A8)**: CLASSICAL(0) → `ResponseWASDKey` + `Camera_EnableControl(CONTROL_STRAFE_*)` fallback (strafe); JOYSTICK(1) → OB wrapper 0/63 + free-view `TurnLeftStart` (turn); dead skill branch (const false) | turn = model rotate (no clip) | Lua DONE; engine application OPEN(P4) |
| M5 | TURNLEFT/TURNRIGHT (arrows) | **handlers decoded** (annex A6.5): enable flag + `ResponseWASDKey('TurnLeft/Right',down,isDouble)` + mode wrapper `0/61`; no mode branch | no ground turn clip (model rotate) | Lua DONE; engine consumer OPEN(P4) |
| M6 | JUMP (Space) | +0x50 jump; 二段跳; takeoff XY; `JumpParam` | kinds 16-19 | PART (jump/land anim transitions OPEN(P5)) |
| M7 | Q/E strafe | official help "Q/E 左右平行移动"; binding rows | as M4 | OPEN(P1) |
| M8 | AUTORUN (G/NumLock; both mouse buttons) | `ControlId.AutoRun`; cancel set (backward/strafe) | stays run | PART |
| M9 | TOGGLERUN (Numpad /) | walk/run tier (`number.krl` +0x48/+0x4C) | walk 56 / run 5 | DONE |
| M10 | CAMERAUP/DOWN (MoveUp/Down) | unbound default; `MoveUpStart/Stop` exists | fly/swim tiers | OPEN(P1) |
| M11 | double-tap W sprint/dash | **decoded**: `ResponseWASDKey` (hotkeys 0/46) calls `StartSprint()` on `Forward` + down + `isDouble`, blocked on tower/bird/horse; classical path ? | SPRINT_* states | PART: Lua DONE(annex A2), classical + engine consumer OPEN(P4) |
| M12 | movement tick model | character 15 Hz, combat 16 fps; `CommitInput` per frame | — | PART (G11) |
| M13 | turn rate + >112.5° penalty | `WalkTo/RunTo` `+0x44/+0x48` mask `0x1000011E` | — | DONE (model); consumer OPEN(P4) |
| M14 | movement-direction application (facing vs camera-relative) | — | — | OPEN(P4, G6) |

## 3. Camera inputs

| # | Item | Client chain | Status |
|---|---|---|---|
| C1 | LMB drag (`CAMERAORSELECTORMOVE`) | **script DONE** (annex A7): `Camera_BeginDrag(1.0)` gated by `scene.bLDown` + `Hotkey_IsLMouseEnabled` (+ morph-camera bypass); `CONTROL_CAMERA` set/cleared in 0/31 | click-select S7 OPEN (targeting) |
| C2 | RMB drag (`...STICKY`) | **script DONE** (annex A7): `Camera_BeginDrag(2.0)` gated by `scene.bRDown` + `Hotkey_IsRMouseEnabled`; `CONTROL_OBJECT_STICK_CAMERA` set/cleared; EndDrag 1.0/2.0 | engine flag mapping G2 OPEN(P3); body-carry conditions OPEN(P4) |
| C3 | mouse move (joystick always-rotate) | `Scene_LockMouseRotation`; ApplyMouse gated by `+0x25C`/controller; controller index `+0x2D0` | OPEN(P4, G9) |
| C4 | wheel zoom in/out | `Camera_Zoom(0.9/1.1)` (wheel 256/257) | DONE |
| C5 | F11 reset / Home/End presets | `ForceResetCamera +0x428`; behind/front | DONE (host), spring path OPEN(P4, G5) |
| C6 | per-mode drag speeds/clamps | node `+0x6C..+0x98` (classic/joystick pairs) | DONE (fields); consumer OPEN(P4) |
| C7 | follow mode `[0..3]` per mode | `nCameraModeInClassicMode/JoystickMode` → node `+0x80/+0x98` | OPEN(P4, G4) |
| C8 | reset speeds | `Camera_SetResetSpeed(3.5, 3.75)` → `+0x78/+0x7C` (classic), `+0x90/+0x94` (joystick) | OPEN(P4, G5) |
| C9 | `[mgr+0x5C]` state enum 1..7 | gated face-yaw writes (ApplyRotation/alt applier) | OPEN(P3/P4, G3) |
| C10 | dynamic-follow flag `+0x1AC` / sticky `+0x1B0` writers | runtime-built vtable; watchpoints | OPEN(P3, G2) |
| C11 | `Camera_UseFullAngle` | no consumer found | OPEN(P4, G8) |
| C12 | camera obstruction | native rules `docs/camera/WALL_OBSTRUCTION.md` | PART (host terrain-only) |
| C13 | FOV 广角 | panel 30-60° → `Set3DEngineOption(fCameraAngle)` | PART (S8) |

## 4. Character / base actions

| # | Item | Client chain | Animation | Status |
|---|---|---|---|---|
| B1 | TOGGLESITDOWN (V/X) | proto 0/98: sit = skill 17 打坐, stand = `Stand()` | `F1b02dj打坐a.tani` loop | DONE |
| B2 | TOGGLESHEATH (Z) | proto 0/97 `SetSheath`; sit/death/fight/horse gates | `F1b02ty拔剑01_start01.ani` → `_持续.tani` | DONE (draw/sheathe b02); other bodies OPEN |
| B3 | RIDEHORSE (T) | horse actor + mounted state; `CharacterVehicleYawTurnSpeed` | horse clips | OPEN(P1/P5) |
| B4 | FOLLOWTARGET (Ctrl+G) | follows target | follow run | OPEN(targeting) |
| B5 | AUTOINTERACT (F) | interact lookup/channel | interact clips | OPEN(targeting) |
| B6 | SCREENSHOT (PrintScreen) | — | — | PART (host F9) |
| B7 | KINESCOPE (Ctrl+Shift+C) | video capture | — | OPEN(host) |
| B8 | 轻功 camera lock | `Camera_LockControl` skill 9007 | fly turn clips `F1bqg…bR/bL.tani` | PART |

## 5. Mode routing (classical vs joystick)

| # | Aspect | CLASSICAL | JOYSTICK | Status |
|---|---|---|---|---|
| R1 | camera rotation | LMB/RMB only | mouse always (`Scene_LockMouseRotation`) | PART: joystick path OPEN(P4, G9) |
| R2 | LMB / RMB semantics | camera / camera+body (scripts: `BeginDrag` only) | camera | G2/P4 |
| R3 | movement keys | WASD camera-relative; A/D habit (default.txt STRAFE; turn habit observed) | WASD + auto-face heading | G6/G10/P4 |
| R4 | control API | `Camera_EnableControl` | `Scene_EnableFreeMoveControl` | **DONE** (annex A1: wrapper proto 0/61 branches on `GetOperationMode()==CLASSICAL_MODE`) |
| R5 | free view | `Camera_IsInFreeView` (Lua getter) | n/a | DONE |
| R6 | persisted follow mode | `nCameraModeInClassicMode` | `nCameraModeInJoystickMode` | **DONE** script-side (annex A4: `SetCameraMode(cm,true)` on switch); consumer G4 |
| R7 | switch | UI-only `SetOperationMode`; no default key | same + host F7 | DONE (host key documented) |
| R8 | persisted mode key | `StorageServer` key **`CurrentOperationMode`** (annex A3) | same | DONE (storage side); `custom.dat` mapping OPEN(P7) |

## 6. Animation matrix (locomotion + base actions)

| A# | Item | Evidence | Criteria/values | Status |
|---|---|---|---|---|
| A1 | F1 KindID map | `player_animation_f1.txt` | idle 1, combat idle 2, run 5, 挪步 6, walk 56, 后退 57/58, jumps 16-19 | DONE |
| A2 | Move-state vocabulary | `0x00CBCF08` name table | ~80 states (STAND…RUSH…) | DONE (names) |
| A3 | Ground locomotion selection | `UpdateMoveAnimation 0x1804C0700` | speed vs per-clip thresholds `[param+0x50]/[param+0x68]` | PART: exact thresholds OPEN(P6) |
| A4 | Direction/turn interpolation | `UpdateDirection 0x180533D40` | `KeepTurningFrame 0x40`, `TurningEpsilon 0x50`; immediate states {1,28,32} | PART: per-state values OPEN(P5/P6) |
| A5 | Strafe/back/turn clip choice | host uses 45°/135° | engine criteria unknown | OPEN(P6, G7) |
| A6 | Turn clips (ground) | none found | negative result; confirm 挪步 vs pure rotate | OPEN(P6) |
| A7 | Turn clips (fly/rush/air) | `player_rush_skill.txt` 478/479; `F1bqg空中转身衔接01` | — | DONE (sources), wiring OPEN(P5) |
| A8 | Jump/double/fall/land transitions | kinds 16-19; gravity model | takeoff/air/land phases | OPEN(P5) |
| A9 | Swim/fly/float states | REP state names; jump/fall spec §3–§9 | per-state clips | OPEN(P5) |
| A10 | blend params `pnForward/pnStrafeRight/pnRotationRight` | table ~0xCD8800 | semantics/weights | OPEN(P5/P6) |
| A11 | two animation slots `+0x1B8/+0x1C0` | `UpdateMoveAnimation` | base vs overlay semantics | OPEN(P6) |
| A12 | playback rate/speed sync | `+0x30/+0x34` writes | rate vs move speed | OPEN(P6) |
| A13 | start/stop transitions | — | idle↔walk↔run edges | OPEN(P6) |
| A14 | face/foot split | `UpdateFaceFootDirection` (assert `0x180534699`) | face yaw `+0x30` | PART |
| A15 | auto head-turn | table cols `是否禁止自动转头`, `IsLookAtCamera` | application unknown | OPEN(P6) |
| A16 | per-mode animation timing | classical keep-facing vs joystick pivot | 挪步/后退 while facing catches up | PART (host), engine criteria OPEN(P6) |
| A17 | sit/sheath anims | B1/B2 catalog clips | loops/transitions | DONE (b02) |
| A18 | mount/vehicle anims | `GetTurnInputYaw`, horse states | — | OPEN(P5) |
| A19 | animation resource resolution | body/weapon variants (`F1b02…` naming) | catalog resolution rules | OPEN(P5) |
| A20 | `tabCGAni` params | `KeepTurningFrame +0x40`, `TurningEpsilon +0x50` | per-anim values | PART |

## 7. Data ledger

| # | Data | Location | Status |
|---|---|---|---|
| D1 | movement speeds | `number.krl` walk `+0x48`, run `+0x4C`, yaw `+0x54/+0x58` | DONE |
| D2 | per-mode camera node fields | Represent camera node `+0x34` selector, `+0x6C..+0x98` | DONE (fields), consumers OPEN(P4) |
| D3 | camera rows (`CameraAdjustYawWhenMoveTurn` …) | `[0x180EDDFE0+0x262F0]`, 10 × 0x24 | PART (offsets), values OPEN(P7) |
| D4 | F1 animation catalog | `samples/player/catalog/player_animation_f1.txt` | DONE (KindID map) |
| D5 | `tabCGAni` params | client data | OPEN(P5) |
| D6 | `JumpParam` | gravity research | PART |
| D7 | persisted mode key | `custom.dat` | OPEN(P7, G8) |
| D8 | default bindings | `default.txt`/`bindings.ini` | DONE |

## 8. Open gaps (owner phase)

| Gap | Owner | Closure |
|---|---|---|
| G1 exe per-frame input loop (runtime interface boundary) | P3 | **static chain mapped** (`CONTROL_MODES_P3_STATIC.md`): KEventCommonMgr → AjustCtrlInput → controller apply → tick-ordered queue drain → appliers; CommitInput caller + vtable wiring dynamic-probe step remains |
| G2 `CONTROL_CAMERA`/`OBJECT_STICK` → `+0x1AC`/`+0x1B0` | P3/P4 | **static DONE**: controls drive the dynamic-follow state machine; `+0x1AC` written by `0x180B1A6ED/0x180B1B3CB/0x180B1D31F/0x180B1D3EB`, `+0x1B0` zero-only (`0x180B09132`); mouse pipeline reads both (P4 doc). Per-state behavioral mapping via probe (open) |
| G3 `[mgr+0x5C]` state enum 1..7 | P3/P4 | transition table |
| G4 follow mode `[0..3]` consumer | P4 | per-value behavior |
| G5 reset-speed consumers/order | P4 | release path behavior |
| G6 movement-direction application per mode | P4 | intent→facing/heading rule |
| G7 animation selection criteria | P6 | thresholds/curves from params |
| G8 `Camera_UseFullAngle`; persisted mode key | P4/P7 | consumer open; mode key = `StorageServer('CurrentOperationMode')` DONE, custom.dat mapping open |
| G9 joystick internals (`Scene_EnableFreeMoveControl`, `ResponseWASDKey`, always-rotate) | P1/P4 | **Lua side DONE** (annex A1/A2/A3): wrapper, axis/8-way MOVE_* builder, mode apply; engine consumer open (P4) |
| G10 free-view/OB vs normal A/D routing truth | P4 | **script mapping DONE** (A8 constants: classical=strafe, joystick=turn); engine application P4 |
| G11 Lua binding registration (no plaintext binding names in any binary: `GetOperationMode` etc. are Lua globals; C-bound names like `Scene_EnableFreeMoveControl` resolve through a registry) | P2 | locate the registry/hash mechanism; enumerate bindings used by hotkeys.lua |
| A1..A20 | P5/P6 | per section 6 |

## 9. Verification plan

- Offline: extend `camera_smoke` with pure mode/animation-selection checks (table
  driven from this matrix).
- Engine: `RC_MODE=classical|joystick` + scripted input sequences; per-frame
  telemetry of intent fields `+0x3C/+0x4C/+0x50`, face yaw, camera manager
  fields, anim id/kind/rate/slot values; numeric fingerprints.
- Proof: `proof/controls/modes_full_<date>.txt`; every DONE row names its run.

## 10. Changelog

- 2026-10-02: created (P0) — baseline audit from the controls docs; G1–G10 and
  A1–A20 registered; animation matrix added per user request.
- 2026-10-02: P1 start — batch Lua index (`tools/controls/lua_index.py`),
  committed indexes + body dumps; annex `CONTROL_MODES_LUA_ANNEX.md`:
  wrapper proto 0/61 (R4), `ResponseWASDKey` 0/46 (R3/M11 joystick), mode apply
  0/5 + toggle 0/19 + settings setter (R6/R8; mode key
  `StorageServer('CurrentOperationMode')`), `Camera_SetResetSpeed(1.0)`
  classical, `UseFullAngle`/`LockMouseRotation` per mode.
- 2026-10-02: P1 movement+drag pass — A6 (`0/63` OB wrapper, `0/65`
  MoveForwardStart, `0/76` StrafeLeftStart) and A7 (Scene `0/27`/`0/31`/`0/36`
  drag + control ids, `0/61` rlcmd lock, `0/25` both-buttons autorun); the
  older "OB-only free-view turn" correction was **inverted** and is superseded;
  M4/C1/C2 updated.
- 2026-10-02: **P2 mode constants resolved** (annex A8): CLASSICAL_MODE=0,
  JOYSTICK_MODE=1, `GetOperationMode` = Lua closure; classical A/D = strafe
  (ResponseWASDKey+CONTROL_STRAFE), joystick A/D = turn (free-view
  TurnLeftStart); `Camera_IsClientControlDisabled` = flag written by
  `CameraStatus_Set` (`dis_ctrl == 1`); binding-name registry gap G11 opened.
- 2026-10-02: **P4 static** (`CONTROL_MODES_P4_PROBE.md`): host-build diff
  table (probe uses host RVAs), camera-manager `+0x1AC` writers = dynamic-follow
  state machine, `+0x1B0` zero-only, readers = mouse pipeline; probe plan
  (shim module/read exports + `RC_PROBE_CONTROL` telemetry + scripted runs).
