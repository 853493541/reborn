# Host deviations register (camera)

One row per behaviour where the host does something other than the game.
Baseline: `camara-fix` @ `45f8ae5`. Type: B=band-aid, P=placeholder,
N=native bypass, H=harness. Status updated as items land.

## A. Aim/control band-aids (no look-at API in the managed host)

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| A1 | mouse writes yaw/pitch; position and look-at derived in the same call | `ExecAction(30/1)` orbit + `measureView` nudge probe | B | native look-at API (WS2) |
| A2 | no separate aim tracker | closed-loop yaw read-back + 10 ms low-pass `yawCorr` | B | A1 |
| A3 | not needed | 1 Hz idle drift correction | B | A1 |
| A4 | not needed | `pitchAimErrPx` + feed-forward `oyFF` + deadband | B | A1 |
| A5 | distance changes do not disturb aim | `aimDirty`/`lastAimDist` re-pin | B | A1 |
| A6 | no freeze state | `aimFrozen` while obstructed & idle | B | A1 (or engine aim API) |
| A7 | ray preserved, no terrain clamp | `camY = camGround` clamp + `aimPitchOverride` | B | native camera/obstruction path |
| A8 | `delta x 2pi` then row clamp (ApplyMouse) | pixel clamps from measured 0.0018/0.00121 rad/px | B | host aim reads game rates |
| A9 | same as A8 | drag integrates `Yaw -= ox*0.0018`, `Pitch += oy*0.00121` | B | A8 |
| A10 | `CharacterYawTurnSpeed` (consumer unknown) | RMB body-turn fallback pi rad/s | P | recover the consumer |
| A11 | mouse deltas are clamped per frame | `CameraSystem.Mouse` now clamps the per-call delta with `CameraMaxDeltaPitch` (was a no-op absolute clamp); still test-path only, the live drag clamps in `RebornClient` | B | wire the model call into the live path |

## B. Visibility/placement band-aids

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| B1 | view near-plane clipping | park dummy 100000 u below at 90/150 u | B | near-plane setter (P0 measurement: N ~= 60-70 u, see note below) |
| B2 | `C'' = C' + normalize(A-C')*18`, length may go behind the anchor (signed) | signed pull landed (`max(0.001,hit)-18`, `1b7d23e`); the dead `MinDistance` field is removed | P | done (T1.4); no exit needed |
| B3 | `|C' - ref|^2` vs 50/100 thresholds | scalar `|hit - desired| < 50/100` | B | exact candidate test |
| B4 | engine terrain ray | extra 14-sample heightfield march + 20 u margin + 30 u ground clamp | B | covered by EngineRay; remove after T1.5 |
| B5 | obstruction shortens the camera; no extra post-pass | final-camera wall gate: re-ray camera->anchor, retract if a hit is < full length - 30 u, stop 25 u short (M1) | B | Step B first pass met this at inst 262's wall (hit 206 -> pull 188/166/126, `postdbg` 0.0); keep until 2-3 more wall spots confirm, then delete |
| B6 | the game camera owns its exact Y (anchor + rotated offset); no ground clamp | `SetCameraPos(fX,fY,fZ,bKeepY)` cannot set an absolute Y: `false` clamps Y up to the render surface at the camera xz (cliff pit: intent 727 -> 1463 while the PhysX terrain object reports 622; `setdbg` proved set-time), `true` keeps the engine's current Y (6405) and ignores ours | N | native camera path (Step C) |

## C. Placeholders (data/API missing)

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| C1 | anchor = head/socket (Bip01 Head, s_face) | anchor = chest + 90 u | P | actor/bone API |
| C2 | probe footprint from camera basis/projection | invented 22 u circle | P | recover the basis offsets |
| C3 | `fMinCameraDistance` engine cap | 100 u placeholder | P | caps provider (blocked: JX3UIX64 not loaded) |
| C4 | carrier/air/npc/god rows from CDN .krl | host placeholders | P | CDN data |
| C5 | sprint camera extras (`Offset`, `SpringTime`, track-back, sprintSpeed arg) | unused; `SprintCameraMaxDistance=60` unit unresolved | P | row data + unit |
| C6 | panel `VideoSetting_WidAngle` (30-60, default 50), raw<30 `+fMinCameraAngle` | reads custom.dat WidAngle else config.ini; no raw<30 rule; defaults to 60 (max) - **user-requested deviation** (`1d5368e`); game panel default is 50 | P-partial | caps + rule |
| C7 | view near plane | never read (getter deadlocks); **measured live** with `RC_PLAYER_HIDE=0`: no clipping at camLen 96, back of the model cut at 81, mostly gone at 66, gone <=36 - host N ~= 60-70 u (0.6-0.7 m), >3x the game's 18 u clearance | P | native near-plane setter (Step C) |
| C8 | streamed `[Camera]` ini values | flex 1.5/2.828 and 18 u hardcoded (ctor defaults) | P | streamed ini data |
| C9 | per-mode caps + `fCameraToObjectEyeScale` inside the clamp | `SwitchMode(...,false)` does not re-clamp `Distance` to the new mode's caps; `ClampDistanceUnits` reads only the character row; EyeScale is applied after the clamp, so the effective distance can exceed `MaxCameraDistance` | P | per-mode rows + caps provider |

## D. Native bypasses of the game filter path

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| D1 | `FilterCamera`, mask 0x301, dispatcher 0x18032EA40 | raw backends: terrain 0x976260, space 0xA5E4C0, entity 0x53CB80, vertical 0xA5E1D0, guard raised via TEB | N | pass the real filter object |
| D2 | per-mesh `bObscatleCamera` gate | no gate; **all foliage is excluded** (structures-only) although content ships `bObscatleCamera=1` on cacti/rocks (those never block the camera now), while decorative props with `bObscatleCamera=0` still block | N-partial | mesh properties in the bake |
| D3 | look-at | `SetCameraPos` position-only | N | same as A1 |
| D4 | engine filter object + self-hit/watchdog filtering | raw backend calls every frame with hardcoded RVAs; no own-body filter, no watchdog | N | filter object / native bridge |
| D5 | mask `0x301` vertical probe (`RayIntersectionVertical`) | `0xA5E1D0` returns no hits in the host scene (hr=E_FAIL), so the vertical ladder contributes nothing | N-partial | filter object / native bridge |

## E. Test scaffolding in the runtime

| ID | Type | Note |
|---|---|---|
| E1 `RC_CAM_MODE` | H | no real mount/dialog/air/spectate triggers |
| E2 `RC_MOVE_PITCH` | H | real move-pitch table is 0.0 |
| E3 `RC_CAM_9RAY` | H | trigger `+0x15c` unknown |
| E4 `RC_VIEW_ANGLE` | H | test override of FOV |
| E5 skill-cast shake | H | host amplitude 2.0/0.5/0.8/3, rotation unused |
| E6 `FindLatestCustomDat` | H | newest custom.dat globally, not the active role |
| E7 `RC_PLAYER_HIDE=0` | H | disables the park-below character hack so the engine near-plane can be bracketed with the clearance ladder |

Verified at this tip: B1/B2/B3/B4/B5 confirmed in code; B6 found by
measurement; C1/C2 confirmed in code; C7 measured (N ~= 60-70 u). Stale vs the
`ea6352b` audit: C6 now reads custom.dat and defaults to the panel max;
custom.dat loads by default; foliage is excluded from camera rays; the
vertical backend was tested and returns no hits in the host scene.

**Step B first pass (2026-09-26).** At inst 262's north wall spawn
`(18097, 500, 22171)`: bake hit 206 u, engine space ray 204 u; the signed pull
is exact at clearance 18/40/80 (len 188/166/126); `postdbg` 0.0; frames clean.
No coverage gap and no near-plane hole at that wall - the pulling wall sits
behind the camera. The near plane is real but only bites geometry *in front*
within N ~= 60-70 u (ladder with `RC_PLAYER_HIDE=0`, evidence
`reborn_out\stepb_np*`). The cliff pit spawned the character inside the inst
503 rock formation and exposed B6 (`SetCameraPos` Y clamp).

## Instrumentation (M0)

- `RC_CAM_CLEARANCE=<u>` overrides `CameraObstruction.Clearance` (near-plane ladder).
- `RC_CAM_DEBUG=1` logs `betweendbg cam->anchor bake/terr/scene`: a hit close to
  the camera on the camera->anchor line means the camera is on the wrong side
  of a wall (invariant: no such hit while the camera is unobstructed).
- `RC_CAM_DEBUG=1` logs `setdbg moved=... intended=... actual=...` when the
  host moves the camera between `SetCameraPos` and the immediate read-back
  (B6 clamp; measured at the cliff pit). `postdbg` is the total move after
  `FrameMove`.
- `RC_PLAYER_HIDE=0` disables the park-below character hack (E7); with the
  clearance ladder it measures the engine near plane (C7, B1).
- Cost: each frame casts 5 (or 9) probe rays against the bake + terrain +
  space/entity engine rays, a 14-step heightfield march, a 14-step vertical
  ladder, and the B5 gate's 3 rays; measured 150-300 fps at the spawn with one
  animated character. No watchdog on the native calls (D4).
