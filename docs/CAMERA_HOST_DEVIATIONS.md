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
| B1 | view near-plane clipping | park dummy 100000 u below at 90/150 u (threshold roughly matches the engine's own model fade near ~90 u) | B | near-plane setter (P0 measurement) |
| B2 | `C'' = C' + normalize(A-C')*18`, length may go behind the anchor (signed) | signed pull landed (`max(0.001,hit)-18`, `1b7d23e`); the dead `MinDistance` field is removed | P | done (T1.4); no exit needed |
| B3 | `|C' - ref|^2` vs 50/100 thresholds | scalar `|hit - desired| < 50/100` | B | exact candidate test |
| B4 | engine terrain ray | extra 14-sample heightfield march + 20 u margin + 30 u ground clamp | B | covered by EngineRay; remove after T1.5 |
| B5 | obstruction shortens the camera; no extra post-pass | final-camera wall gate: re-ray camera->anchor, retract if a hit is < full length - 30 u, stop 25 u short (M1) | B | Step B first pass met this at inst 262's wall (hit 206 -> pull 188/166/126, `postdbg` 0.0); keep until 2-3 more wall spots confirm, then delete |
| B6 | the game camera owns its exact Y (anchor + rotated offset); no ground clamp | `SetCameraPos(fX,fY,fZ,bKeepY)` cannot set an absolute Y: `false` clamps Y up to the render surface at the camera xz (cliff pit: intent 727 -> 1463 while the PhysX terrain object reports 622; `setdbg` proved set-time), `true` keeps the engine's current Y (6405) and ignores ours | N | native camera path (Step C) |
| B7 | the game camera line never runs under the drawn surface | experimental retraction when `SetCameraPos` lifts the camera (`RC_CAM_SNAPGUARD=1`, off by default); it cannot escape a pit where the surface is above the whole local line and is therefore not shipped as default | B | delete; B6 exit (native camera path) |

## C. Placeholders (data/API missing)

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| C1 | anchor = head/socket (Bip01 Head, s_face) | anchor = chest + 90 u | P | actor/bone API |
| C2 | probe footprint from camera basis/projection | invented 22 u circle (unchanged; the true basis offsets are not recoverable from this host - the game-DLL fields/RVAs do not map). **Correction 2026-09-27:** the earlier "phantom hit" reading was wrong. `obstdbg`: `probe1 off=(-22,0,0) bake=11.4 inst=897(combined)=503(foliage offset 394, mesh 349, tri 23648)`; the offline replay of the same ray on the bin finds the same triangle, and a parity test puts the anchor *inside* mesh 349 (the rock formation). The 11.4 hit is therefore real cavity geometry - the camera pull is the native signed-pull rule doing its job. Removing it (front-face filter kept) does not remove the visible see-through; only D3 does. The center ray's 524 hit is back-facing from inside the mesh and is now filtered by the front-face rule (render rays see front faces) | P | recover the basis offsets (C2); not the user-visible defect |
| C3 | `fMinCameraDistance` engine cap | 100 u placeholder | P | caps provider (blocked: JX3UIX64 not loaded) |
| C4 | carrier/air/npc/god rows from CDN .krl | host placeholders | P | CDN data |
| C5 | sprint camera extras (`Offset`, `SpringTime`, track-back, sprintSpeed arg) | unused; `SprintCameraMaxDistance=60` unit unresolved | P | row data + unit |
| C6 | panel `VideoSetting_WidAngle` (30-60, default 50), raw<30 `+fMinCameraAngle` | reads custom.dat WidAngle else config.ini; no raw<30 rule; defaults to 60 (max) - **user-requested deviation** (`1d5368e`); game panel default is 50 | P-partial | caps + rule |
| C7 | view near plane | never read (getter deadlocks). The earlier N ~= 60-70 u estimate came from the *character model* fading at camLen 66-96; the `stepb_clr18/40/80` wall frames show sand within 60 u in front of the camera is never clipped, so that fade is engine-side model culling, not the view near plane. A wall-distance test is still pending (the `RC_FIXED_CAM` harness only sets the camera once and the engine overrides it) | P | native near-plane setter (Step C), if the wall test shows a problem |
| C8 | streamed `[Camera]` ini values | flex 1.5/2.828 and 18 u hardcoded (ctor defaults) | P | streamed ini data |
| C9 | per-mode caps + `fCameraToObjectEyeScale` inside the clamp | `SwitchMode(...,false)` does not re-clamp `Distance` to the new mode's caps; `ClampDistanceUnits` reads only the character row; EyeScale is applied after the clamp, so the effective distance can exceed `MaxCameraDistance` | P | per-mode rows + caps provider |

## D. Native bypasses of the game filter path

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| D1 | `FilterCamera`, mask 0x301, dispatcher 0x18032EA40 | raw backends: terrain 0x976260, space 0xA5E4C0, entity 0x53CB80, vertical 0xA5E1D0, guard raised via TEB | N | pass the real filter object |
| D2 | per-mesh `bObscatleCamera` gate | no gate; **all foliage is excluded** (structures-only) although content ships `bObscatleCamera=1` on cacti/rocks (those never block the camera now), while decorative props with `bObscatleCamera=0` still block | N-partial | mesh properties in the bake |
| D3 | look-at | **Engine contract recovered 2026-09-27** (`proof/netcode/engine_camera_contract.txt`): `camera = scene->vt[+0x50]()`, `camera->vt[+0x50](pos,0)` set position, `camera->vt[+0x58](target,0)` set look-at; the managed API only *translates* the target by the position delta, which is why the view direction is preserved across the crossing. The native `m_pScene` behind the managed wrapper is not reachable from outside (3 routes tried, evidence in the proof file; one AVs the engine) - next: hook the managed call site or a C++/CLI helper. `SetCameraPos` position-only. Measured 2026-09-27: the engine view does not follow the camera position, so when the signed pull crosses the anchor the render points *away* from it (user repro: camLen -6.6 -> camera 6.6 u in front of the head, view unchanged; the character sits behind the camera and nothing occludes the view -> the reported see-through). This is the confirmed visible defect at the user spot (the signed pull there is *legitimate*: the anchor sits inside the rock formation mesh, the cavity wall is only ~11 u behind the head - see C2 correction). Orbit-flip experiment (`RC_CAM_LOOKPACK=1`, **default OFF**): rotates the engine orbit 180 deg when crossed (yaw + mirrored pitch), closed-loop converges to 0.005 rad of pi, reverse flip stable. It fixes the stationary repro but crashed the host engine when it fired while moving (D6), so it is not shipped default-on and, when enabled, only engages while `!movingNow` | N-partial (experiment only) | native look-at / view-matrix write (Step C item 2); orbit path dead for moving (D6) |
| D4 | engine filter object + self-hit/watchdog filtering | raw backend calls every frame with hardcoded RVAs; no own-body filter, no watchdog | N | filter object / native bridge |
| D5 | mask `0x301` vertical probe (`RayIntersectionVertical`) | `0xA5E1D0` returns no hits in the host scene (hr=E_FAIL), so the vertical ladder contributes nothing | N-partial | filter object / native bridge |
| D6 | host engine content loading (engine bug, not a game rule) | **Refined again 2026-09-27 late (T3 result): rate limiting does not make the moving flip safe.** Evidence: (a) instant 600 px orbit event AVs the shader parser (`+0xA6C75A`, `"] = stage_input."`) even at open spawn; (b) a rate-limited turn (<=1.5 rad/s, <=20 px/event, aim frozen) rotated ~17 rad over 10 s clean (T1); (c) with the rate-limited flip *while walking*, 11 flips into a walking demo AVed at the D6 material-parameter DataStore offset (`+0x11D03B6`, null map deref, `"noinfo"` store - the editor install lacks the build-machine DataStores) - the view changing into unloaded content while the scene streams is the trigger, not the rotation rate. One more fault code seen: function-prologue push (stack exhaustion?) at `+0x9DD289` during a shim scan+rotation run. Mitigation: the look-at flip (D3) stays stationary-only and OFF by default | H | load/preconvert the missing materials, or fix the editor install's DataStores path; a moving look-at cannot be made safe in this host build without that |

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
measurement; C1/C2 confirmed in code; C7 corrected (the old N ~= 60-70 u came
from the model fade; the 2026-09-27 clearance ladder at 2-18 u in front shows
no clip, N is still unmeasured). Stale vs the
`ea6352b` audit: C6 now reads custom.dat and defaults to the panel max;
custom.dat loads by default; foliage is excluded from camera rays; the
vertical backend was tested and returns no hits in the host scene.

**Step B first pass (2026-09-26).** At inst 262's north wall spawn
`(18097, 500, 22171)`: bake hit 206 u, engine space ray 204 u; the signed pull
is exact at clearance 18/40/80 (len 188/166/126); `postdbg` 0.0; frames clean.
No coverage gap and no near-plane hole at that wall - the pulling wall sits
behind the camera. **Corrected 2026-09-27:** the old "N ~= 60-70 u" from the
`stepb_np*` ladder was the engine's own model culling, not the near plane; the
`RC_CAM_CLR_SEQ` run at this wall (clearances 18..2 -> wall 18..2 u behind the
camera, proof/nearplane_ladder.png) shows the forward view is intact at every
rung, so nothing in front within 18 u is clipped. The cliff pit spawned the
character inside the inst 503 rock formation and exposed B6
(`SetCameraPos` Y clamp).

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
- `RC_CAM_CLR_SEQ=ms:C,ms:C,...` (2026-09-27) applies a timed clearance ladder
  in one run; with `RC_SHOTS` at the same times it brackets a known
  camera->wall distance. At inst 262 (hit 206) clearances 18/12/9/6/4/2 put
  the wall 18..2 u *behind* the camera (pull lands on the far side of the
  clearance), so the six frames show the unobstructed view - nothing in front
  is clipped; no near-plane hole appears in this direction. Full $PATH
  evidence: `reborn_out` shots rc_00_3600..rc_05_9600 (14:40 run),
  `proof/nearplane_ladder.png`.
- `RC_CAM_OBSTDBG=1` (2026-09-27) logs every obstruction probe's backend hits
  below 80 u (`obstdbg probe<k> off=(..) bake=.. terr=.. scene=..`) and the
  winning source (`obstdbg min=.. src=[..]`); used to find the C2 artifact.
- `RC_CAM_LOOKPACK` (2026-09-27, experiment, **default OFF** since the D6
  crash) rotates the engine view 180 deg while the resolved pull is negative
  (D3), delivered as orbit pixels **rate-limited to ~1.5 rad/s (<=20 px per
  event, the engine's fMaxAngelVel)** plus a spaced `lookpack verify` closed
  loop (converges to 0.005 rad of pi; full flip ~3.5 s). It only engages while
  `!movingNow`; the moving view keeps the old direction. Enable with
  `RC_CAM_LOOKPACK=1` for stationary repros only.
- `RC_CAM_YAWSPEED=<rad/s>` (2026-09-27) rotates the engine view at a
  controlled rate with small orbit steps (crash-safety characterisation for
  D6: <=1.5 rad/s clean for 20 s, instant 600 px AVs at `+0xA6C75A`).
- `RC_CAM_YAWFDIFF=1` (2026-09-27) snapshots the SceneView, rotates the view
  by a known orbit delta and logs `SnapDiff` - groundwork for locating the
  engine's view-angle fields for the P2 route-1 engine-side view set (the
  instant delta used in the first attempt crashed at `+0xA6C75A`; retry with
  rate-limited steps).
- Cost: each frame casts 5 (or 9) probe rays against the bake + terrain +
  space/entity engine rays, a 14-step heightfield march, a 14-step vertical
  ladder, and the B5 gate's 3 rays; measured 150-300 fps at the spawn with one
  animated character. No watchdog on the native calls (D4).
