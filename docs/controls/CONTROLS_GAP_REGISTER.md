# Controls gap register (live checklist)

**Branch:** `control-system-notes` · **Code audited:** `camara-fix` @ `00f1237`
**Status legend:** `OPEN` · `PARTIAL` · `DONE` · `BLOCKED` (needs RE/data) ·
`SERVER` (server-side, out of client scope)

This is the single live checklist for the control system. Update status as work
lands, keep the evidence column pointing at real file:line.

---

## Camera

| ID | Item | Status | Evidence / note | Dependency |
|---|---|---|---|---|
| S1 | Aim re-pin after wheel zoom / sprint / mode switch | **OPEN** | sync only `if (dragging \|\| orbitApplied)`; idle block refreshes yaw only | none — small |
| S2 | `EyeScale` in the aim math | **OPEN** | placement ×`EyeScale` (`RebornClient.cs:1043`); helpers use `camSys.Distance` | none — small |
| S3 | Terrain ground-clamp guard for the aim loop | **OPEN** | `camY = camGround` breaks the orbit ray | none — small |
| S4 | Unify pitch controllers / deadband | **PARTIAL** | residual now fractional + 10 ms low-pass; feed-forward still separate, no deadband | S1–S3 |
| S5 | Max-distance clamp on every distance writer | **OPEN** | only `ZoomBy` clamps; F11/sprint bypass; `SetMaxDistance` misnamed | none |
| S6 | RMB character turn uses the turn-rate model | **DONE** (2026-09-29) | movement direction now recomputed camera-relative per frame (no held world dir) + heading/facing turn model with the >112.5° speed/turn-step penalty (`client/RebornClient.cs`; run `proof/controls/steering_run.txt`). Server `+0x48` turn step still undecoded — host fallback π rad/s | — |
| S7 | LMB click vs drag (select on click) | **OPEN** | press always locks; no threshold/timestamp | targeting |
| S8 | 广角/FOV + engine caps | **OPEN (mapping DONE)** | panel = 30–60°, default 50° → `Set3DEngineOption(fCameraAngle)`; caps unprobed (`RESEARCH_RESOLVED_GAPS.md` §3) | `docs/camera/DISTANCE_FOV_SPEC.md` |
| S9 | Wall/structure obstruction (camera clips through walls) | **OPEN** | terrain-only march `RebornClient.cs:1058-1080`; native rules in `docs/camera/WALL_OBSTRUCTION.md` | baked bins; native interface optional |

## Input / hotkeys

Status note: `OPEN (research DONE)` = the real behaviour is fully decoded in
`RESEARCH_RESOLVED_GAPS.md`; only the client implementation remains.
The whole input layer is fully specified in `HOTKEY_SYSTEM_FULL.md`
(architecture, formats, encoding, 428-command registry, rebind flow,
persistence, bridges) with complete decoded annexes.

| ID | Item | Status | Evidence / note | Dependency |
|---|---|---|---|---|
| C1 | Hotkey table (`default.txt` + `bindings.ini`) loaded at runtime | **OPEN** | hardcoded `KeyDown/KeyUp` | none |
| C2 | Key encoding (VK + Ctrl/Shift/Alt + mouse/wheel) | **OPEN (research DONE)** | `JX3_HOTKEY_SYSTEM.md` §3 | C1 |
| C3 | Rebinding UI + `SetCapture` semantics | **OPEN (research DONE)** | `HotkeyPanel` flow decoded (`RESEARCH_RESOLVED_GAPS.md` §2) | C1 |
| C4 | Per-role override save/load (`hotkey_newlast.txt` + backups + `hotkey.data`) | **OPEN (research DONE)** | grammar + real files decoded (§1) | C1 |
| C5 | Contexts (dynamic/BR/rogue/mobile) | **OPEN (research DONE)** | `context` column + `contextgroup` tabs (§1/§2) | C1 |
| C6 | Key repeat (`EnableKeyDownLoop`) | **OPEN** | API recovered | C1 |
| C7 | Action bars/pages/dynamic bars + skill assignment | **OPEN (research DONE)** | custom data + `StorageServer('ActionBar')` + anchors (§6) | C1, targeting |
| C8 | Targeting (Tab/Ctrl+Tab/F1–F5/target-of-target/filters) | **OPEN (partially researched)** | bodies located in `b03/target.lua`, not decoded | C1 |
| C9 | Cast input: keydown, `Alt+WASD` direction, ground aim | **OPEN (partially researched)** | `CastSkillByKeyDown` defined in `hotkeys.lua`; body pending | C7/C8 |
| C10 | Movement fidelity: turn keys, autorun, sit/mount/sheath, click-to-move, follow/interact | **OPEN (research DONE)** | command bodies decoded (§5); engine `ResponseWASDKey` is C-side | C1 |
| C11 | Integer 15/16 Hz movement model + turn-rate + penalty | **OPEN** | spec complete (`REBORN_JUMP_FALL_SPEC.md`) | C10 |
| C12 | Operation modes (classic/joystick, `CAMERAUP/DOWN`) | **OPEN (research DONE)** | `SetOperationMode` + `Camera_EnableControl`/`Scene_EnableFreeMoveControl` (§4) | C1 |
| C13 | UI customization (panels, layout `custom.dat`, settings panels) | **OPEN (research DONE)** | `UICustomModePanel` + window anchors (§7) | C1 |

## Combat / netcode (server-owned)

| ID | Item | Status |
|---|---|---|
| N1 | Cast intent → server (`OP_CAST_INTENT`) + result handling | **SERVER** (M2) |
| N2 | Cooldowns/GCD/resources display from server clocks | **SERVER** (M2) |
| N3 | Buffs/CC/DR display | **SERVER** (M2) |
| N4 | Death/revive/fall-damage | **SERVER** (M2) |

## Verification / hygiene

| ID | Item | Status | Note |
|---|---|---|---|
| V1 | Per-run log files (`reborn_<timestamp>.log`) | **OPEN** | logs were overwritten during testing |
| V2 | Smoke cases for S1/S2/S5/S9 math | **OPEN** | `CameraSmoke.cs` has 16 checks, none of these |
| V3 | Wall/structure test run near known collision | **OPEN** | use `C` teleport debug / baked bins |
| V4 | Camera telemetry fields (`effDist`, residual, clamped) | **OPEN** | camdbg lacks `EyeScale`/clamp flags |

---

## Suggested order

1. S1 + S2 + S3 (small, same aim loop) → V2 partial
2. S9 wall ray (managed, `FoliageCollision.RaycastSegment`) → V3
3. S5, then S8 (`docs/camera/DISTANCE_FOV_SPEC.md`)
4. C1–C6 input core (biggest "full control" step), then C7–C9
5. C10–C11 movement, C12 modes, C13 UI
6. M2 netcode (N1–N4)
