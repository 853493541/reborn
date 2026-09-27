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
| A11 | mouse deltas are clamped per frame | `CameraSystem.Mouse` misuses `CameraMaxDeltaPitch` as an absolute clamp and is dead code in the live path (the live drag clamps in `RebornClient`) | B | delete it or wire the real per-frame delta clamp |

## B. Visibility/placement band-aids

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| B1 | view near-plane clipping | park dummy 100000 u below at 90/150 u | B | near-plane setter (P0 measurement) |
| B2 | `C'' = C' + normalize(A-C')*18`, length may go behind the anchor (signed) | floor `MinDistance = 0.1` | B | T1.4 signed pull |
| B3 | `|C' - ref|^2` vs 50/100 thresholds | scalar `|hit - desired| < 50/100` | B | exact candidate test |
| B4 | engine terrain ray | extra 14-sample heightfield march + 20 u margin + 30 u ground clamp | B | covered by EngineRay; remove after T1.5 |
| B5 | obstruction shortens the camera; no extra post-pass | final-camera wall gate: re-ray camera->anchor, retract if a hit is < full length - 30 u, stop 25 u short (M1, uncommitted) | B | delete once Step B proves the obstruction path is complete at walls (or native filter) |

## C. Placeholders (data/API missing)

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| C1 | anchor = head/socket (Bip01 Head, s_face) | anchor = chest + 90 u | P | actor/bone API |
| C2 | probe footprint from camera basis/projection | invented 22 u circle | P | recover the basis offsets |
| C3 | `fMinCameraDistance` engine cap | 100 u placeholder | P | caps provider (blocked: JX3UIX64 not loaded) |
| C4 | carrier/air/npc/god rows from CDN .krl | host placeholders | P | CDN data |
| C5 | sprint camera extras (`Offset`, `SpringTime`, track-back, sprintSpeed arg) | unused; `SprintCameraMaxDistance=60` unit unresolved | P | row data + unit |
| C6 | panel `VideoSetting_WidAngle` (30-60, default 50), raw<30 `+fMinCameraAngle` | reads custom.dat WidAngle else config.ini; no raw<30 rule; defaults to 60 (max) - **user-requested deviation** (`1d5368e`); game panel default is 50 | P-partial | caps + rule |
| C7 | view near plane | never read (getter deadlocks) | P | P0 measurement / native |
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

Verified at this tip: B1/B2/B3/B4, C1/C2 confirmed in code. Stale vs the
`ea6352b` audit: C6 now reads custom.dat and defaults to the panel max;
custom.dat loads by default; foliage is excluded from camera rays; the
vertical backend was tested and returns no hits in the host scene.

**M1 live verification is spawn-only.** The signed pull (B2/T1.4), the
final-camera wall gate (B5) and `betweendbg` have not been exercised at an
actual wall in a live run; the spawn `betweendbg` sample was open terrain
(all `-1`). Step B (clearance ladder at a drawn wall) is the first live test
of this set.

## Instrumentation (M0)

- `RC_CAM_CLEARANCE=<u>` overrides `CameraObstruction.Clearance` (near-plane ladder).
- `RC_CAM_DEBUG=1` logs `betweendbg cam->anchor bake/terr/scene`: a hit close to
  the camera on the camera->anchor line means the camera is on the wrong side
  of a wall (invariant: no such hit while the camera is unobstructed).
- Cost: each frame casts 5 (or 9) probe rays against the bake + terrain +
  space/entity engine rays, a 14-step heightfield march, a 14-step vertical
  ladder, and the B5 gate's 3 rays; measured 150-300 fps at the spawn with one
  animated character. No watchdog on the native calls (D4).
