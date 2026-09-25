# Camera plan #2 — first fix, then full completion

Supersedes the "Next step" section of `docs/CAMERA_STATUS.md` and extends
`docs/CAMERA_CLIENT_AUDIT.md`. Target repo: `reborn-merge` (branch `merge`),
client code in `client/`, host spike in `engine_host_spike/`.

Goal: **(1)** fix the drag/pitch placement bug now, **(2)** finish the camera
system: native-faithful follow, obstruction, modes, settings and extras, using
the recovered reference in `reborn-camera/docs` and
`reborn-camera/proof/netcode/`.

---

# Part 1 — First fix (P0, do this before anything else)

## 1.1 The bug (one paragraph)

The live placement treats the row distance as a *horizontal* radius and adds a
`tan(pitch) * distance` vertical term, so the camera-to-anchor length becomes
`distance / cos(pitch)` and re-scales while dragging. The real
`SetCharacterCameraPosition` (`camera_set.txt 0x180B0F1EE`,
`JX3_CAMERA_RESEARCH.md` §6) rotates a **constant-length** offset
`(A,B,C)` by yaw/pitch and adds `C` (height) as a separate constant. Drag must
only change yaw/pitch; the position is always `anchor + rotate(yaw,pitch) * (distance) + height`.

## 1.2 Exact changes

**A. `client/RebornClient.cs`, placement block (~lines 914-955).**

Before:

```csharp
double pitchOffset = Math.Tan(camSys.Pitch);
double camX = ax2 - vx * dist;
double camY = ay2 - pitchOffset * dist;
double camZ = az2 - vz * dist;
...
// obstruction retract:
camX = ax2 - vx * d2;
camY = ay2 - pitchOffset * d2;
camZ = az2 - vz * d2;
```

After (constant-length rotation; height as its own term):

```csharp
double camHeight = camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter;
double cp = Math.Cos(camSys.Pitch), sp = Math.Sin(camSys.Pitch);
double offX = -vx * cp * dist;
double offY =  sp * dist + camHeight;   // sign to verify with measureView
double offZ = -vz * cp * dist;
// keep the existing per-axis 60 ms dead-zone smoothing on (offX,offY,offZ)
...
// obstruction: scale the offset along anchor->camera, never re-tan
double s = d2 / Math.Max(1e-6, dist);
camX = ax2 + camOffset[0] * s;
camY = ay2 + camOffset[1] * s;
camZ = az2 + camOffset[2] * s;
```

**B. Remove the height-in-pitch calibration.** Delete/neutralise
`alignPitch = -Math.Atan2(alignHeight, distance)` in the startup block
(`RebornClient.cs:588-604`): start from the saved/row pitch (`-0.35`) and keep
`CameraHeight` additive. The alignment nudge may stay only as an *aim*
correction (compare `measureView` pitch with `camSys.Pitch`; if the host's aim
is off, nudge the engine orbit, then re-measure) — never as the source of the
camera height.

**C. Drag = angle only.**

- Keep `orbitQueue -> ExecAction(30/1, ...)` purely as the host's aim driver
  (the managed `SetCameraPos` cannot set look-at — `CAMERA_STATUS.md` blocker 1).
- Delete the yaw re-derivation from `measureView` while dragging; at most keep
  an idle-only diagnostic (`RC_CAM_DEBUG`). Do **not** write `camSys.Yaw`
  from measured camera positions in the normal loop
  (`RebornClient.cs:722-730`) — that is position -> angle feedback and makes
  drags shift the position.

**D. Same fix in `engine_host_spike/MapSpike.cs`** (its placement uses a fixed
`camHeight` without the `cos(pitch)` horizontal factor; align it with the same
formula so both hosts agree).

## 1.3 Acceptance (Part 1)

1. New `CameraSmoke` case: with fixed distance `d`, sweep pitch over
   `{-1.2, -0.8, -0.35, 0, +0.8}` and assert
   `|anchor + (0,height,0) -> camera| == d` within `1e-3` u.
2. Drag simulation: apply `(dx,dy)` deltas; assert `dist`/`height` unchanged
   and only yaw/pitch changed.
3. Run `camera_smoke.exe` (13/13 must stay green) and one `RC_SHOTS` capture:
   camera stays behind the character at every tested pitch, no slide/fly-away.

---

# Part 2 — Full completion plan

Ordering: Phase 0 unblocks the rest; Phases 1-2 are the core; 3-5 finish the
feature surface; Phase 6 keeps it honest with tests and the missing data.

## Phase 0 — Host camera interface (unblocker)

The managed wrapper only exposes position + editor actions
(`CAMERA_STATUS.md` blockers 1-3). The recovered engine facts show the camera
object (`KG3DTrackCamera`) has position/look-at/FOV vtable methods and its own
obstruction+flex path.

- [ ] Recon `IKG3D_Camera` / `IKG3DCameraProxy` / `KG3D_CAMERA_OPTION_PROXY`
  (`MovieEngineCLR.dll` RTTI): walk `KGSceneCLR.m_pScene` -> camera object;
  map vtable slots used by the engine (`+0x18` direction, `+0x20` set-position,
  `+0x28` set-position variant, `+0x48..+0x54` FOV block, `+0x60/+0x68` FOV
  get/set, `+0xC8` 9-ray mode; see `CAMERA_WALL_OBSTRUCTION.md` and
  `proof/netcode/camera_wall_obstruction.txt` §12).
- [ ] Extend the host wrapper with: `GetCameraPos`, `SetCameraPos`,
  `SetCameraLookAt`, `GetCameraLookAt`, `Get/SetFov`, `GetEngineCameraState`
  (pos, dir, fov, obstructed flag).
- [ ] Keep `ExecAction(ROTATE_CAMERA)` only as fallback.
- Acceptance: a probe prints/sets pos+look-at deterministically on a test map;
  screenshots still work.

## Phase 1 — Follow model completion (character mode)

- [ ] Replace the ad-hoc placement with the full model already present in
  `CameraSystem.Update` (rotation + dead-zone smoothing + move-pitch + yaw
  follow) and call it from the client loop.
- [ ] Anchor selection: character head/entity position with chest fallback
  (mount socket when mounted), matching the recovered priority order.
- [ ] Keep `CameraHeight` (`C`), `TargetDistance` (`A`) and a `B` lateral term
  per row; verify signs with `measureView` at several pitches.
- [ ] Movement-reactive camera: `CameraMovePitch*` state machine (rate
  pi/3000 rad/ms = 60 deg/s), `CameraAdjustYawWhenMoveTurn` + dead zone 0.26.
- [ ] `ForbidStrafe`, `ForbidRotation`, `TurnCameraYawToObjectYaw` flags.
- Acceptance: smoke cases for each behaviour; drag invariant from Part 1 stays
  green.

## Phase 2 — Obstruction (native-faithful)

Prefer native: once Phase 0 exposes the engine camera, its own path already
does `bObstructdAvert` (5/9 probes, nearest hit, 18 u clearance, 50/100 u
hysteresis, flex return) — then this phase is mostly *verification*.
If the host must keep placing positions manually, implement the same rule:

- [ ] Probe set: center + 4 corners (default) / 8 perimeter at pi/4 (alternate).
- [ ] Ray origin = anchor, direction to proposed camera point, max = segment
  length; take nearest hit.
- [ ] Final position = `hit + 18 u * normalize(anchor - hit)`.
- [ ] Hysteresis: start-block within 50 u, keep-block within 100 u, state flag.
- [ ] Flex return: state `S += (-fFlex*E - fDamp*S)*dt`, `X = desired + S*dt`,
  0.05 rad guard (`fFlex` 1.5, `fDamp` 2.828).
- [ ] Geometry: structures/trees/props from `FoliageCollision` (host has real
  structure meshes); skip meshes whose data says `bObscatleCamera = 0`
  (map that flag when loading).
- Acceptance: near a wall the camera shortens by the 18 u rule, holds with
  hysteresis, returns with the flex curve; no clipping; screenshots.

## Phase 3 — Modes

Parameter vocabularies recovered in `CAMERA_CONFIG_FILES.md` §6.

- [ ] Sprint: pull-back distance, `SprintCameraOffset`/`MaxOffset`,
  `SpringTime`, track-back `10 -> 90 deg/s` slope 1.0, `SprintCameraAngle`,
  `SprintCameraPitch`.
- [ ] Carrier (mount): `CarrierCameraPitch/Yaw/MaxDistance/DeltaHeight`,
  `ForbidStrafe`, `TurnCameraYawToObjectYaw`; trigger from mount state.
- [ ] NPC dialog: `InitCameraDistance/Angle/EyeScale`, `InitPostRenderEnable`,
  DOF keys (`InitFocus/InitNear/InitDofDegree`, `InitGatherBlurSize`);
  trigger from dialog events.
- [ ] Air combat: `LoadAirCombatParams` defaults (`InitCameraDistance 2000`,
  `AdjustEyeScale 0.2`, `YawRange 70`, enter speeds 500, `FinalYawAngle 30`,
  screen limits 0.2/0.25).
- [ ] Glider (`LoadGliderParams` full key list), Dynamic follow
  (`Rotate`/`Translate` blocks), Skill-move camera (per-skill FOV/edge FX
  table; `skill_move_camera.txt` already extracted).
- Acceptance: each mode has a switch test and a smoke row check.

## Phase 4 — Settings, input and operation modes

- [ ] Read all `VideoSettingPanel.tCameraStatic` keys: `fMaxCameraDistance`,
  `fDragSpeed`, `fDragPitchSpeed`, `fSpringResetSpeed`, `fCameraResetSpeed`,
  `nCameraMode`; plus `bCameraSmoothing`, `bCurveCamera`, `bEyeFollow`,
  `nCameraModeInClassic/JoystickMode`, `g_Scene_tCameraRuntime`.
- [ ] Apply through the same clamps as the real bindings: distance [1, 2000],
  drag/spring/reset speeds [0.01, 10], follow mode [0, 3].
- [ ] Optional write path: save the panel table back to `custom.dat`.
- [ ] Operation modes: classic/joystick switching, `Camera_SetResetSpeed`
  (3.5 / 3.75), `Scene_LockMouseRotation`, `Camera_EnableControl`,
  `Camera_UseFullAngle`.
- [ ] Engine caps: probe/import `fMinCameraDistance`, `fMinCameraAngle`,
  `fMaxCameraAngle` (caps slots `+0x50/+0x58/+0x60/+0x68`) instead of the
  100 u guess.
- Acceptance: settings round-trip; clamps enforced; zoom respects caps.

## Phase 5 — Extras

- [ ] Camera shake: wire `CameraShake` to skill/impact triggers using the
  recovered curve (`ShakeType/Intensity/TotalTime/CycleCount/OffsetXY/Decay/
  Period`).
- [ ] Follow-action look-at (`SetCameraFollowCharacterAction`, `s_face`).
- [ ] Cinematic/track camera: spring from `TrackCameraFrameMove`
  (`fDistance = fOffsetZ - fHeight - fPrev`), camera animations
  (`KRLCameraAni`).
- [ ] FOV: drive the engine FOV (Phase 0 get/set) from the video-panel
  `fCameraAngle` semantics (default 0.837757, saved in `config.ini`).
- [ ] Spectate/watch (`CameraCommon`) if needed for the product.
- Acceptance: shake matches the model; FOV slider visibly changes the view.

## Phase 6 — Verification, data, and source of truth

- [ ] Golden A/B: with Phase 0, drive the native camera and our model on the
  same map/pose and compare position/look-at per frame (tolerance table).
- [ ] Keep `camera_smoke` growing with every phase (rotation invariance,
  obstruction clearance/hysteresis, mode rows, settings clamps).
- [ ] Fetch the missing data when possible (CDN mini-update): engine `[Camera]`
  ini and `Represent/camera/*.krl.txt`; replace host placeholders
  (`TargetDistance`, `CameraHeight`, move-pitch rows, `MinCameraDistance`).
- [ ] Decide the long-term source of truth: native camera for aim/obstruction/
  FOV + our layer for JX3 follow/modes/settings (recommended), or full
  reimplementation in the client.

## Milestones

| # | Milestone | Phase | Exit criteria |
|---|---|---|---|
| 1 | Drag fix | Part 1 | radius-invariant test + smoke green |
| 2 | Camera interface | 0 | probe sets pos/look-at/FOV |
| 3 | Follow model | 1 | `Update` wired, move-pitch/yaw-follow live |
| 4 | Obstruction | 2 | wall test: 18 u + hysteresis + flex |
| 5 | Modes | 3 | sprint/carrier/dialog/air/glider/dynamic |
| 6 | Settings/input | 4 | full custom.dat keys + caps |
| 7 | Extras | 5 | shake, follow-action, track, FOV |
| 8 | Verified complete | 6 | native A/B + rows fetched |

## Risks / decisions

- **Native vs reimplementation**: the engine's own chase camera acts every
  frame (`CAMERA_STATUS.md` blocker 2) and can fight manual placement. Once
  Phase 0 lands, prefer handing aim/obstruction/FOV to the native camera and
  keep only the JX3 behaviour layer in the client.
- **Missing rows**: placeholders must stay clearly marked until the CDN data
  is obtained; the Part 1 fix changes the default look (height is no longer
  folded into pitch), so expect a one-time visual retune.
- **Anchor mismatch**: chest +90 u vs the real head/socket anchor affects the
  apparent height; Phase 1 fixes it.

## References

- `docs/CAMERA_CLIENT_AUDIT.md` — what exists / what is missing.
- `docs/CAMERA_STATUS.md` — host constraints (position-only API, orbit rate).
- `reborn-camera/docs/CAMERA_WALL_OBSTRUCTION.md` — obstruction rule.
- `reborn-camera/docs/CAMERA_CONFIG_FILES.md` §6/§7 — mode vocabularies and
  user settings.
- `reborn-camera/docs/CAMERA_REAL_VALUES.md`, `docs/netcode/REBORN_CAMERA_SPEC.md`
  — constants and the reference model.
- `reborn-camera/proof/netcode/disasm/camera_set.txt` — placement disassembly.
