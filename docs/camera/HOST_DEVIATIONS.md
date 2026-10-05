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
| A12 | `CAMERAZOOMIN`/`CAMERAZOOMOUT` bound to the wheel (`ui/script/hotkeys.lua`; `INPUT_CONTROLS.md` §2) | **the wheel is inert; zoom is on the `+`/`-` keys (2026-09-30, user decision)**: `Oemplus`/numpad `Add` = CameraZoomIn (x0.9), `OemMinus`/numpad `Subtract` = CameraZoomOut (x1.1); same `CameraSystem.ZoomBy` rule and clamps | H (host binding) | real hotkey table + input contexts |
| A13 | decoded: the keyboard never writes the camera (`CONTROL_TURN_*` feeds the character only; the camera is mouse-owned, the move+turn row is the only follow) | CLASSICAL turn keys rotate the **view** directly (user request 2026-10-02: turn keys turn the camera; kept for arrows and the `RC_ADHABIT=turn` option) and the character follows the camera. The decoded row follow is applied on top for AUTO/ALWAYS modes (target = travel heading, fixed 2026-10-02). JOYSTICK turn controls never write the camera (client-true). | H (host/user decision) | revert when the engine `CONTROL_TURN_*` consumer is modeled, or on user preference |

## B. Visibility/placement band-aids

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| B1 | view near-plane clipping + engine model fade (measured at camLen ~36..96 u, HANDOFF §4) | park dummy 100000 u below, thresholds **105/250 u** (2026-09-29; was 90/150, then 105/150): the 90 u hide threshold equaled the head offset (~90 u above the chest anchor), so a camera inside the head hovered at camDist ~90 and never triggered the hide ("I see the inside of the character"). The show threshold was raised 150 -> **250** after a crash dump (`…30400.dmp`) showed hide/show **thrashing** (104.9 -> 150.1 -> 105.0 -> 150.5 in 5 s while dragging): every `show` re-adds the dummy model and churns engine content on the same streamed thread as D6. Transitions log `hideNear hide/show camDist=`. **2026-09-29 faithful-mechanism recon:** `KGEngineCLR.SetMainPlayerType` is callable (`RC_MAINPLAYER`) but a T1 A/B (camera pulled to len=0, park-below off) shows the engine culls the character identically with and without it, so it is **not** the character-hide mechanism; a crash delta was intermittent D6 noise (3 runs each, all clean). The managed wrapper exposes **no** near-plane API; the engine exports the camera-property schema (`NearPlane/FarPlane/AutoComputeClipPanes`, `KG3D_CAMERA_PROPERTY`) used by `KG3D_Engine::CreateCamera`, but no near-plane setter export - the remaining faithful route is the camera/view vtable projection block (RE) | B | near-plane setter (P0 measurement) |
| B2 | `C'' = C' + normalize(A-C')*18`, length may go behind the anchor (signed) | signed pull landed (`max(0.001,hit)-18`, `1b7d23e`); the dead `MinDistance` field is removed | P | done (T1.4); no exit needed |
| B3 | `|C' - ref|^2` vs 50/100 thresholds | scalar `|hit - desired| < 50/100` | B | exact candidate test |
| B4 | engine terrain ray | extra 14-sample heightfield march + 20 u margin + 30 u ground clamp | B | covered by EngineRay; remove after T1.5 |
| B5 | obstruction shortens the camera; no extra post-pass | final-camera wall gate: re-ray camera->anchor, retract if a hit is < full length - 30 u, stop 25 u short (M1) | B | Step B first pass met this at inst 262's wall (hit 206 -> pull 188/166/126, `postdbg` 0.0); keep until 2-3 more wall spots confirm, then delete |
| B6 | the game camera owns its exact Y (anchor + rotated offset); no ground clamp | **CLOSED 2026-09-28**: the engine-faithful path writes absolute Y through the camera object (`cam->vt[+0x50](pos,0)`), bypassing the managed `SetCameraPos` Y clamp; verified `postdbg moved=0.0` with the intended Y on the engine path (logs 140358/140659/140838; kinematic proof `camGet rc=0 pos=(0,500,-800) managed=(0,500,-800)`). Historical: `SetCameraPos(...,false)` clamped Y up, `true` ignored it. `RC_CAM_SNAPGUARD`/B7 remains as an opt-in leftover to delete in P5 | N (closed) | delete B7 knob in the P5 cleanup |
| B7 | the game camera line never runs under the drawn surface | experimental retraction when `SetCameraPos` lifts the camera (`RC_CAM_SNAPGUARD=1`, off by default); it cannot escape a pit where the surface is above the whole local line and is therefore not shipped as default | B | delete; B6 exit (native camera path) |
| B8 | native signed pull may place the camera behind the anchor (hit < 18); the engine look-at then flips the view 180 deg | **crossing guard shipped (2026-09-28, on by default, `RC_CAM_CROSS=1` restores the native rule)**: the resolved pull floors at 0, so the camera reaches the anchor and never crosses. Reason: with the host bake the hit chatters in tight cavities (T1 sweep: 5 sign flips / 2 s measured with `RC_CAM_SHAKEDBG=1`), each flip spinning the look-at view - nausea. After the guard: T1 sweep 0 flips, `len=0`, vyaw tracks the aim; T2 `hit=206 len=188`, T4 `186->168`, walk route 0 events, smoke PASS | B (host stabilizer) | stable render-entity hit set (P4) then allow the native crossing again |
| B9 | the game camera query hits the wall's front surface from the anchor (self excluded by FilterCamera) | **degenerate-hit guard shipped (2026-09-28, on by default, `RC_CAM_HITMIN=0` disables)**: probe hits within 3 u of the probe origin are ignored. Cause: the raw scene backend (D1/D4, no self/own-body filter) returns 0.1-2.3 u self/exit/grazing faces from the 22 u footprint offsets at wall edges (measured in the T1 sweep: `probe1 ... bake=-1 terr=-1 scene=0.1`), which won the min-hit and yanked the camera 10+ u per frame. A real wall that close to the head floors the pull at the anchor anyway, so ignoring it only removes the false teleport. After: 21 degenerate hits ignored in the sweep, winning hits real (`bake=11`, `bake=70`), 0 shake events, T2/T4/walk unchanged | B (host stabilizer) | real FilterCamera / self-hit filter (D1/D4, P4) |
| B10 | the resolved pull is immediate (game rule) | B5 final-camera wall gate disabled by default (`RC_CAM_WALLGATE=1` restores): it can fire at wall edges and adds a jump line of its own; the pull + B8/B9 guards keep the camera on the near side (T2/T4/walk re-verified) | B | delete once P4 lands |
| B14 | the engine smooth-follows the camera's character target (`JX3RepresentX64` `DynamicFollowSmoothObjectPosition`; `CharacterCameraSmoothTime=60 ms` in `Represent/common/number.krl.txt`) | **anchor-Y step smoother shipped (2026-09-29, on by default, `RC_CAM_YFOLLOW=0` disables)**: the host feeds raw physics `py + 90` as the camera anchor, so a grounded step snap (house rug 924 ↔ floor 969, stairs; up to the 64 u ground tolerance) teleported the camera 42–64 u in a single frame (measured with `RC_CAM_YDBG=1`). A one-frame delta > 5 u while grounded starts an exponential follow (SmoothTime = camera-row `SmoothTime`, 60 ms) with a 5 ms frame clamp and a catch-up rate cap (`RC_CAM_YRATE`, default 1200 u/s); continuous slope motion and airborne frames pass through, so jumps/falls are untouched. Field route (19600,36000 walking north): max grounded camera-Y step 63.9 → 6.0 u, median 0.6 u; the 10 Hz stair train (+7/+10 u per frame) becomes 0.1–0.8 u | B (host stabilizer) | represent-layer interpolation / native `DynamicFollowSmoothObjectPosition` port (P4) |

## C. Placeholders (data/API missing)

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| C1 | anchor = head/socket (Bip01 Head, s_face) | anchor = chest + 90 u | P | actor/bone API |
| C2 | probe footprint from camera basis/projection | invented 22 u circle. **P2 recon 2026-09-28:** the game base-camera ctor (`KG3DEngineX64 0x180447770`) initializes the probe-basis block (+0x12c/+0x138/+0x144) to identity axes scaled by **500.0** (`0x18068064C`), +0x15c zeroed (5-ray mode); the located writers of those offsets are struct copies, not the per-frame projection, so the exact footprint formula is not recovered - `foot=22` stays registered (evidence in `proof/netcode/engine_camera_contract.txt`). **Correction 2026-09-27:** the earlier "phantom hit" reading was wrong. `obstdbg`: `probe1 off=(-22,0,0) bake=11.4 inst=897(combined)=503(foliage offset 394, mesh 349, tri 23648)`; the offline replay of the same ray on the bin finds the same triangle, and a parity test puts the anchor *inside* mesh 349 (the rock formation). The 11.4 hit is therefore real cavity geometry - the camera pull is the native signed-pull rule doing its job. Removing it (front-face filter kept) does not remove the visible see-through; only D3 does. The center ray's 524 hit is back-facing from inside the mesh and is now filtered by the front-face rule (render rays see front faces). **2026-09-29 correction:** the front-only rule is itself a defect (B14) - it strands the camera outside surfaces once the desired ray clears; camera probes are now double-sided by default and the T2/T4/T1/userspot invariants are unchanged | P | recover the basis offsets (C2); not the user-visible defect |
| C3 | `fMinCameraDistance` engine cap | 100 u placeholder | P | caps provider (blocked: JX3UIX64 not loaded) |
| C4 | carrier/air/npc/god rows from CDN .krl | host placeholders | P | CDN data |
| C5 | sprint camera extras (`Offset`, `SpringTime`, track-back, sprintSpeed arg) | unused; `SprintCameraMaxDistance=60` unit unresolved | P | row data + unit |
| C6 | panel `VideoSetting_WidAngle` (30-60, default 50), raw<30 `+fMinCameraAngle` | reads custom.dat WidAngle else config.ini; no raw<30 rule; defaults to 60 (**user decision 2026-09-29: default = client-truth max = the client's own slider max**, log source `client-max-default`; the install config.ini 48 deg is informational only); game panel default is 50 | P-partial | caps + rule |
| C7 | view near plane | never read (getter deadlocks). The earlier N ~= 60-70 u estimate came from the *character model* fading at camLen 66-96; the `stepb_clr18/40/80` wall frames show sand within 60 u in front of the camera is never clipped, so that fade is engine-side model culling, not the view near plane. A wall-distance test is still pending (the `RC_FIXED_CAM` harness only sets the camera once and the engine overrides it) | P | native near-plane setter (Step C), if the wall test shows a problem |
| C8 | streamed `[Camera]` ini values | flex 1.5/2.828 and 18 u hardcoded (ctor defaults) | P | streamed ini data |
| C9 | per-mode caps + `fCameraToObjectEyeScale` inside the clamp | `SwitchMode(...,false)` does not re-clamp `Distance` to the new mode's caps; `ClampDistanceUnits` reads only the character row; EyeScale is applied after the clamp, so the effective distance can exceed `MaxCameraDistance` | P | per-mode rows + caps provider |
| C10 | the real per-mode follow distance rows (`Represent/camera/config.ini`, CDN-only) | **character/sprint default follow distance = 1245 u (12.45 m), the client's shipped `number.krl CameraMaxDistance`** (2026-09-29; `camera.json` + `CameraSystem` fallbacks). Corrects the earlier "default = 2000 u max" call: 2000 is only the per-role **zoom-out cap** (`fMaxCameraDistance` -> `SetCameraMaxDistance` writes the camera node cap `+0x74`/`+0x8C`; it never sets the current distance), and the character camera reads **no** `InitCameraDistance` (that key is only read by the air-combat/carrier loaders). Caveat: no reader of `CommonNumber+0x98` (where `CameraMaxDistance` lands) was located in this build, so the exact initial distance stays unproven; the per-mode row would settle it (C4). The 6 m value was an invented placeholder. Other mode rows stay placeholders. **2026-09-30 (user decision, start from max):** the host starts both follow rows at the max range (`fMaxCameraDistance`, 2000 u on this install) - target + init distance = max, so the follow camera holds at max; 1245 stays the client-number value and the F11 base is now max too (log `reborn_20260930_164645`: `CameraSystem ready: ... dist=2000u`) | P-partial | CDN per-mode rows (C4) |

## D. Native bypasses of the game filter path

| ID | Game rule | Host behaviour | Type | Exit criterion |
|---|---|---|---|---|
| D1 | `FilterCamera`, mask 0x301, dispatcher 0x18032EA40 | raw backends: terrain 0x976260, space 0xA5E4C0, entity 0x53CB80, vertical 0xA5E1D0, guard raised via TEB | N | pass the real filter object |
| D2 | per-mesh `bObscatleCamera` gate | **2026-09-28: gate implemented from extracted content data.** Property source resolved: `KG3DMeshFileDataLoader::Load` opens `path` + the `bin`/`bsp`/`ini` extension swap (`0x180264592`, KG3DEngineX64.dll) and `_LoadMeshProperty` (`0x180267360`) reads `[Display] bObscatleCamera` (ctor default 1). `tools/export_camera_flags.py` extracted **592 sibling inis** for the map (692 mesh paths; **316 zero-flag meshes**); `tools/export_structure_collision.py` now writes a `<bin>.cflags` sidecar aligned to the mesh order (317/695 zero) and `FoliageCollision` gates camera rays on it; foliage rock patterns 6/7 are `=0` (excluded), deadwood/cactus have no ini -> default 1 (block). Verified: T2 wall `hit=206 len=188` exact; T4 deadwood pulls 186/168; T1 cavity unchanged with the engine look-at; `camera_smoke` ALL PASS. Remaining: meshes outside the bake (missing classes, P4) and other maps' bins need their own flag export | N-closed (baked set) | re-export per map; P4 for missing classes |
| D3 | look-at | **CLOSED 2026-09-28 (default on)**: `KGSceneCLR.m_pScene` read by reflection, `camera = scene->vt[+0x50]()`, then `cam->vt[+0x50](pos,0)` + `cam->vt[+0x58](anchor,0)` in `camera_shim.dll` (SEH-guarded; `RC_SceneCamRead`/`RC_CamGetVt`/`RC_CamSetVt3`). The view now tracks the model aim through crossings (`vyaw` = aim ±0.06 rad over 125/125 samples in the route logs), `postdbg moved=0.0`, aim emulation disabled while active. The old orbit-flip approximation stays default-off (`RC_CAM_LOOKPACK`, A/B only). Historical note follows. **Engine contract recovered 2026-09-27** (`proof/netcode/engine_camera_contract.txt`): `camera = scene->vt[+0x50]()`, `camera->vt[+0x50](pos,0)` set position, `camera->vt[+0x58](target,0)` set look-at; the managed API only *translates* the target by the position delta, which is why the view direction is preserved across the crossing. `SetCameraPos` position-only. Measured 2026-09-27: the engine view does not follow the camera position, so when the signed pull crosses the anchor the render points *away* from it (user repro: camLen -6.6 -> camera 6.6 u in front of the head, view unchanged; the character sits behind the camera and nothing occludes the view -> the reported see-through). This is the confirmed visible defect at the user spot (the signed pull there is *legitimate*: the anchor sits inside the rock formation mesh, the cavity wall is only ~11 u behind the head - see C2 correction). Orbit-flip experiment (`RC_CAM_LOOKPACK=1`, **default OFF**): rotates the engine orbit 180 deg when crossed (yaw + mirrored pitch), closed-loop converges to 0.005 rad of pi, reverse flip stable. It fixes the stationary repro but crashed the host engine when it fired while moving (D6), so it is not shipped default-on and, when enabled, only engages while `!movingNow` | N-partial (experiment only) | native look-at / view-matrix write (Step C item 2); orbit path dead for moving (D6) |
| D4 | engine filter object + self-hit/watchdog filtering | raw backend calls every frame with hardcoded RVAs; no own-body filter, no watchdog | N | filter object / native bridge |
| D5 | mask `0x301` vertical probe (`RayIntersectionVertical`) | `0xA5E1D0` returns no hits in the host scene (hr=E_FAIL), so the vertical ladder contributes nothing | N-partial | filter object / native bridge |
| D6 | host engine content loading (engine bug, not a game rule) | **Still open, now mitigated by view pacing (2026-09-28).** It is reachable in normal play, not only by the old orbit flip: crash 15:48:52 on the *engine set path* with a rapid yaw change (T4 demo sweep, no experiments active, `+0x11D03B6` DataStore null deref). `scene.GetLoadingProgress()` measured **1.000 throughout** after the synchronous map load, so loading-state gating cannot see the streamed content the AV hits; `KGSceneCLR.HasLoadingTask(handle)` needs a handle we do not hold. **Crash guard (2026-09-28, registered host bypass, now OPT-IN `RC_PATCH_D6=1`):** the shim patches the engine's null material-store deref with a trampoline (`RC_PatchD6`): at `+0x11D03B6` the original `mov rbx,[rsi]; mov rcx,[rsp+0x78]` runs unchanged when `rsi != 0`; when `rsi == 0` the stub returns E_FAIL through the function epilogue (`+0x11D0840`) instead of AVing. Version-guarded by the original 8-byte signature and the PE stamp; the bail leaks that call's string locals. **Not default-on**: a 18:28:14 session (three overlapping client instances running!) crashed with a new BEX64 jump-to-data (`fault offset 0x7FFE622B0000`, module unknown) - the skipped cleanup may corrupt state later, and concurrent instances share the engine/GPU. The client now refuses a second instance (`RC_ALLOW_MULTI=1` overrides) with a dialog. Evidence: `patchD6 rc=0 site=... byte0=E9`, 3/3 55 s walking stress + 4/4 T4 demo runs clean, T2 exact (`hit=206 len=188`), smoke ALL PASS. Also `SetSceneFullLoading(true)` (`RC_FULLLOAD=1`) was tested: loading becomes observable (progress 0.250) but the crash still occurred after progress hit 1.000, so it is not a fix and stays default-off. Rate cap 1.5 rad/s is **opt-in** (`RC_CAM_RATECAP=1`) - it bends drag feel and did not prevent the crash; dormant loading-rate pacing (`RC_CAM_LOADPACE=0` disables) engages only if progress reports <1 (with `RC_FULLLOAD=1`). Historical evidence: (a) instant 600 px orbit event AVs the shader parser (`+0xA6C75A`); (b) rate-limited turn ~17 rad/10 s clean; (c) 11 rate-limited flips while walking AVed at `+0x11D03B6`; one fault at `+0x9DD289` (prologue push). **2026-09-29: the trampoline itself is unsafe** - a single-instance `RC_PATCH_D6=1` run crashed BEX64-style (`module unknown`, fault `0x7FFE70EE0000`, patch cave `0x170EE0000`), matching the documented bail-path corruption; the guard stays off by default and must not be re-enabled without a fixed bail path. **2026-09-29 root cause pinned (dump `...30272.dmp`, user crash after spawning 24212,649,25583, running ~4 s, stopping with the camera <105 u; reproduced again in dump `...31804.dmp` while only dragging the camera at spawn):** the fault function is `KG3DEngineDX11EX64+0x11CE760` (worker/streaming path); it lazily FNV-hashes the named context **`RCPI_Scene`** (`.rdata` RVA `0x21CB2A8`), looks it up in a global registry (`call 0x18105CF50` with `[[rbp-68h]]+0x58`), and on lookup failure sets `rsi=0` then **dereferences it without a null check** (`mov rbx,[rsi]` at `+0x11D03B6`). The registry entry is absent in the editor install (the documented missing content/DataStores). `RCPI_Scene` is an engine RTTI class (`.?AVRCPI_Scene@@`, present only in this DLL) looked up in a reflection registry reached four dereferences from `r12` (`[[[[r12]]]]`, `0x1811CFC1F..0x1811CFC2C`); the registry lookup is `0x18105CF50`. Fix options: (a) find/trigger the registrar that inserts `RCPI_Scene` (proper), or (b) a patch that runs the function's own failure cleanup (frees the small allocations + string at `0x1811D0778` lands too late to jump to directly) - the old trampoline skipped cleanup and caused BEX64. Evidence: `proof/netcode/disasm/d6_rcpi_scene.txt`. **2026-09-29 FIX (default on, `RC_D6SEED=0` opts out):** the VEH capture (`RC_D6DBG=1`, `%TEMP%\d6dbg.txt`) showed `[rbp-70h]=0` at the crash - the engine's per-function lazy FNV-1 hash of `RCPI_Scene` (slot RVA `0x2D5BBD0`) was never computed because the guard gate (`cmp [guard], tls; jg init`, `0x1811D0335`) let worker threads through before the init ran; the lookup then used key 0 and dereferenced the miss. `camera_shim.dll` now writes the exact hash the engine would compute (`0x392E0BFA0428F080`, engine's own FNV-1 loop) into the slot at load (`RC_D6Seed`); it does not patch code. A/B on a driven interactive repro (`tools/camera/drive_client.ps1`: camera drags + WASD, same spawn): baseline **2/2 crashes** (`+0x11D03B6`, key 0), seeded **2/2 alive**. Exit: remove when the engine's own init runs (verified build/update), or if the registry itself turns out to lack the entry | N-partial (host fix) | load/preconvert the missing materials, or fix the editor install's DataStores path; then delete the trampoline |
| D7 | host engine init (editor shader-build upload, not a game rule) | **2026-10-04, OPT-IN `RC_STARTUP=nodb`, verified**: `KG3D_MaterialShaderManager::_initShaderUpload` (`KG3D_MaterialSystemX64.dll`) blocks ~21 s in a blocking `connect()` to the editor shader-compile DB `10.11.10.102:1433` (Seasun LAN, unreachable; observed as `SynSent`). `startup_shim.dll` (DLL-load notification) scans the module's readable sections for the exact standalone literal `10.11.10.102` and rewrites it to `0.0.0.0` in memory, so connect fails immediately (`WSAEADDRNOTAVAIL`) and the engine takes its own existing "server not ready" path. No hardcoded RVA/PE gate: the byte-exact literal is the guard, so engine updates that keep the address need no action (PE stamp/size are only reported as `build=ok|mismatch`; if the literal is gone the shim refuses with `site-not-found`). Data-only, no code patch; unset = shipped, DLL never loaded. Measured `Init3DEngine` 24.2-24.6 s → 3.1-3.2 s (engine `const time` 24.375 → 2.844 s), full map 3.1 s + LoadMap 1.56 s; spawn/terrain/fingerprints unchanged. Evidence: `docs/engine_host/FAST_STARTUP.md`, `proof/engine_host/startup_nodb_2026_10_04/`. Historical: a forced pre-draw 4-thread / skip A/B (RVAs `0x8CFF3F/47/64`) had no effect on init - the earlier predraw attribution was wrong. | N | engine update (guards refuse; re-derive constants) or a supported config that disables the shader-list upload |

## E. Test scaffolding in the runtime

| ID | Type | Note |
|---|---|---|
| E1 `RC_CAM_MODE` | H | no real mount/dialog/air/spectate triggers |
| E2 `RC_MOVE_PITCH` | H | real move-pitch table is 0.0 |
| E3 `RC_CAM_9RAY` | H | trigger `+0x15c` unknown |
| E4 `RC_VIEW_ANGLE` | H | test override of FOV |
| E5 skill-cast shake | H | host amplitude 2.0/0.5/0.8/3, rotation unused |
| E6 `FindLatestCustomDat` | H | newest custom.dat globally, not the active role. **2026-09-30:** prefers the newest file that actually carries `g_Scene_tCameraRuntime` (the account/global files have no saved view), so the real per-role view is restored; the active-role ambiguity remains (no login context) |
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

## Engine-faithful camera closure (2026-09-28)

**D3 look-at - CLOSED (engine call).** The camera object is reached by reading
`KGSceneCLR.m_pScene` (private `IKG3DSceneProxy*`, vtable in
`KG_EngineEditorX64.dll`) with reflection + `Pointer.Unbox` (the byte-scan
of the wrapper and the engine-module-only guards were the blockers; the proxy
lives in the editor module). The shim (`RC_SceneCamRead`, `RC_CamGetVt`,
`RC_CamSetVt3`) then makes the managed IL's own calls:
`cam->vt[+0x50](pos,0)` set position, `cam->vt[+0x58](anchor,0)` set
look-at, all SEH-guarded. Live evidence 2026-09-28:
`clr camGet rc=0 pos=(0,500,-800) managed=(0,500,-800)` (getter identity);
`engineSet direct rc=0 native=True` every frame at the user spot; camdbg
`vyaw` equals the model aim through a full yaw sweep (`yaw +- pi` when the
signed pull crosses the anchor, `yaw == vyaw` unobstructed after walking
away); `postdbg moved=0.0`; clean `DONE`, exit 0, no crash. Default-on
(`RC_CAM_ENGINESET=0` opts out; managed fallback when the shim is absent).

**B6 absolute Y - CLOSED** by the same path (direct camera position setter, no
managed scene Y clamp; `setdbg` silent).

**B5 wall gate, B7 snapguard, `RC_CAM_LOOKPACK` - OBSOLETE, delete.**
The engine set path owns position + look-at while moving; the orbit flip and
the retraction guards are no longer needed. The aim emulation (sync, drift,
`alignAim`, orbit sends) is disabled when the engine path is active and is a
deletion candidate once the engine path has been stable across the test maps.

**Still open:** C1 anchor (chest+90), C3 caps, C4 per-mode rows, C6 FOV
default (60 max vs game 50), C7 near plane (not the user-visible cause),
D1/D2/D4/D5 filter path, B1 park-below hide, B3 scalar window, B4 terrain march.

| B11 | the pull follows the raw min-hit exactly (game rule) | **hit stabilization is opt-in on main (`RC_CAM_HITWIN`/`RC_CAM_HITWINDOW`, default 0), correcting the C1d claim of a shipped 0.25 s default (the `3fd31b4` WIP checkpoint disabled it)**: the pull uses the min-hit over the last N s. At the user spot (18755,657,24539) the raw min flickered 52<->3 u as the idle animation moved the head across a bake triangle edge -> pull 34<->0 several times per second (measured: raw jumps + dive/crawl). A past-min can only hold the camera at the closest contact longer, never ignore a wall. **2026-09-29:** with B14 (double-sided probes) the userspot idle and the T1 sweep show 0 `jumpdbg`/0 events at HITWIN=0, so the default stays off; enable for A/B only. Implemented in `CameraObstruction.Stabilize`, host-called (smoke ALL PASS). Exit: stable render-entity hit set + footprint basis (P2/P4) |
| B12 | the game's camera mask 0x301 includes the raw scene backend | **scene near-hit floor shipped (2026-09-28, default 80 u, `RC_CAM_SCENEMIN`; `RC_CAM_SCENERAY=0` removes the backend)**: the raw backend has no FilterCamera (D4) - it hits the player's own model (57 u behind the head at the user spot -> camera slammed to 39 u) and exit/grazing faces (0.1-3 u). Near walls are the bake's job (map geometry, no self); scene hits < 80 u are dropped. Far scene hits still block (T2 wall 281 -> pull 189, C0 acceptance 188). Exit: real scene self/own-body filter (D1/D4) |
| B13 | the game hides `bObscatleCamera=0` meshes (fade) instead of blocking the camera | **flag=0 structures still block the camera (host deviation, default)**: the host has no camera fade, so honoring the flag would see through a visible wall (T2 inst 262 is flag=0). The camera gate now only drops flag=0 *foliage* (rock/grass cards); structures always block. Exit: camera fade workstream (P5) |
| B15 | the obstruction query tests the candidate (desired) camera line; the follow smoothing is a separate layer | **probes + obstruction now use the raw desired offset, not the per-axis smoothed one (2026-09-29)**: per-axis smoothing a fast-rotating offset shortens the vector through its chord (measured with `RC_CAM_SHAKEDBG`: `offLen` 2050 -> 1520 u in ~60 ms during a ~7+ rad/s camera flick, log `reborn_20260929_163131`); the obstruction machine treated the drop as an immediate "shortening" (the 18 u pull-in has no threshold) and the flex eased back over ~3 s - the reported "camera zooms in while turning, returns when I stop". The game's per-axis SmoothTime still applies once, to the resolved offset (`rSm`). Verified: smoke ALL PASS, `hit=-1` flick burst gone (no `jumpdbg`). **2026-09-30 (camera-wwdrag):** the `rSm` constant must be the character row's `SmoothTime` (CharacterCameraSmoothTime 60 ms, shared placement) in every mode - reading the active row made sprint mode use the sprint row's 0.5 s (SprintCameraSmoothTime) and collapsed the orbit radius while dragging (live repro `mode=sprint dist=1305 obst=0 hit=-1`: r 1305 -> 443, mean r/len 0.77 over 95 samples; after: mean 0.99 over 136 samples, only the synthetic >30 rad/s flick troughs dip; smoke `sprint drag keeps constant-length orbit` 47.6% -> 1.15%) | B (host fix; matches the engine querying the desired candidate) | stable render-entity hit set + footprint basis (P2/P4) |
| B14 | the native camera query's winding rule is not proven (`FilterCamera`, D1) | **double-sided camera probes, default on (`RC_CAM_BACKFACE=0` restores the old front-only rule, 2026-09-29)**: front-only could strand the resolved camera outside a surface once the desired ray cleared - measured at T1 (18985,682,24515) with `RC_CAM_PENDBG`: 18 event lines / 192 event-frames, reverse bake hit 1-2 u from the camera (rock shell inst 897) and reverse scene hits 6-11 u with the bake clear; forward probes were blind because the surfaces' front faces point at the camera. A/B: front-only 192 frames, front-only+HITWIN(0.25) 118, double-sided **0**. Regression exact: T2 `hit=206 len=188`, T4 `hit=186 len=168`, T1 `hit=11 len=0`, userspot idle pull 155 (hit=173) 0 jumps, 45 s collide route 0 events/jumps, smoke ALL PASS | B (host fix; likely native-faithful, winding not yet proven) | pass the real `FilterCamera` (D1) or recover its winding/backface semantics |
