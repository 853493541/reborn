# Camera work - handoff report (for a fresh session)

## 0. Rules the user set

- **Never commit without the user saying so.** Several changes below are
  deliberately uncommitted; commit only on request.
- **Always copy the real game/client mechanism**, never invent a fix because it
  "works". When the host cannot do what the game does, write it in the
  deviations register with an exit criterion instead of silently approximating.
- Test everything: `camera_smoke.exe` + live probes + screenshots.

## 1. Where we are

- Repo `C:\Users\Zhibin Ren\Desktop\reborn` (main, clean, pushed), worktree
  `C:\Users\Zhibin Ren\Desktop\reborn-camara-fix`, branch **`camara-fix`**.
- Committed tip: the batch commit on top of **`1b7d23e`** (M0+M1 set + corrected
  docs; `camara-fix` adds 41 commits over `main`, `a8471b4`..). The M0+M1 set
  (`RC_CAM_CLEARANCE`, `betweendbg`, signed pull, wall gate, smoke rename) is
  committed in `1b7d23e`.
- This batch (big-batch commit): `setdbg` read-back probe,
  `RC_PLAYER_HIDE=0` knob, `Mouse` delta-clamp fix, dead `MinDistance` removal,
  Step B results + near-plane measurement in the docs/register. Build passes,
  smoke 25/25.
- Build: `client\build_client.cmd` (uses `csc` v4.0.30319) -> writes
  `C:\SeasunGame\MovieEditor\bin64\reborn_client.exe` and `camera_smoke.exe`.
- No C++ toolchain on the machine (no cl/g++/clang, no VS, no Windows SDK);
  only .NET `csc`. A native shim would need VS Build Tools installed first.

## 2. Architecture (what exists in the client)

- `client/CameraSystem.cs` - JX3 model: `DesiredOffset` (sphere offset),
  distance dynamics, `CameraObstruction` (18 u clearance, 50/100 window, flex
  1.5/2.828, signed pull), shake/track/shake classes.
- `client/RebornClient.cs` - live loop: aim emulation, obstruction probes,
  displacement, input, modes, harnesses, telemetry.
- `client/CameraSettings.cs` - `custom.dat` (per-role settings, now loaded by
  default; `RC_CUSTOM_DAT` override, `RC_LOAD_CUSTOM_DAT=0` opt-out) and
  `scene_init_param.txt`.
- `client/VideoSettings.cs` - 广角: custom.dat `VideoSetting_WidAngle` else
  config.ini `CammeraAngle` else panel max 60 deg -> `SetViewAngleFactor(f)`.
- `client/EngineRay.cs` - native scene rays (below).
- `client/FoliageCollision.cs` - baked structures/foliage collision + camera
  raycast (`structuresOnly` flag used by the camera).
- `client/TerrainSampler.cs` - PhysX heightfield sampler (legacy).

Host API limits (`MovieEngineCLR`): `KGSceneCLR.SetCameraPos/GetCameraPos/
ExecAction/SetViewAngleFactor`; `KGModelCLR` has no visibility/bone/alpha.
**No look-at API** - that is the root of the aim band-aids.

The host runs the **editor engine `KG3DEngineDX11EX64.dll`**, not the game's
`KG3DEngineX64.dll` (verified via the process module list).

## 3. Verified facts (addresses are from the local DLLs)

Input (game `JX3RepresentX64.dll`):
- `ApplyMouse 0x180B36E20`: mouse deltas x2pi (`0x180CB9F34`); X -> controller
  yaw `+0x20`, Y -> pitch `+0x24`; setter `0x180AE29D0`; pitch clamp
  `+/-1.5550884`. Drag down raises the camera (aim keeps the character centred).
- Wheel = `Camera_Zoom(0.9/1.1)` (`ui/script/hotkeys.lua`), NOT the Step path.
- `SetCharacterCameraPosition 0x180B0E820`: camera = anchor + rotated offset
  `(cos(yaw)cos(pitch)d, sin(pitch)d + h, sin(yaw)cos(pitch)d)`; anchor =
  head/socket (Bip01 Head, s_face); 5/9-ray probes; 18 u clearance
  (`0x1806ED3F0`); 50/100 u window; flex `E=cur-ref, S+=(-1.5E-2.828S)dt,
  X=cur+S*dt`, 0.05 rad guard (`0x180674314`). All in `docs/camera/DRAG_MODEL.md`,
  `docs/camera/CLOSE_RANGE_RESEARCH.md`, `proof/netcode/disasm/*`.

Values: max distance cap 2000 u (20 m; users ship 760/1125/1245); min cap
unknown (100 u placeholder); 广角 panel 30-60 deg default 50, install
config.ini 0.837757 rad (48 deg); 16 Hz movement (96/320 u/s).

Engine rays (host `KG3DEngineDX11EX64.dll`, in `client/EngineRay.cs`):
- engine = `KG3D_GetEngine2()` (returns g_pEngine); window =
  `?GetActiveWindow2@...`; scene = `?Get3DScene2@...`.
- terrain object = `[scene+0x950]`, call `vt[+0xB8](pos,dir,maxDist,float*)`
  (engine 0x1809763C6). Works: downward hit 91 u.
- space manager = `[[scene+0x910]+8]`, call `0x180A5E4C0` (0x1809760EC). Works:
  hit 1865 u.
- entity list = `[scene+0xAD8]` (count +0x600, data +0x5F8), element 0 through
  `0x18053CB80` (0x180976144), entCount=10. Resolves; unverified hits.
- vertical = `0x180A5E1D0` (this,posXZ/pos,range,float* pRetHeight). **Returns
  no hits in the host scene.**
- guards: `cmp [global], tls+0x89B0; jg skip`; globals `0x2D58298`,
  `0x2D5829C`, `0x2D58B2C`; TLS cell reached via TEB
  (`NtQueryInformationThread`) and raised in `SatisfyGuard`.
- the full guarded `KG3D_Scene::RayIntersection` (`0x975EE0`) still returns
  `E_FAIL` - it needs the engine's `FilterCamera`/mask `0x301` (dispatcher
  `0x18032EA40`), which we do not construct.
- near-plane getter (`0x1433E0(&global 0x86ECC0)` then `vt+0xA8`) **deadlocks
  the engine even from a worker thread**; the probe was deleted.

## 4. The main open problem: see-through walls (hypotheses)

**Status: not measured yet.** No controlled wall test has been run. The only
`betweendbg` sample (spawn) was taken in open terrain, where all `-1` means
"nothing there", not "geometry missing". Two competing hypotheses:

- **H1 - coverage gap**: the camera ray never hits the drawn geometry, so the
  camera ends up beyond it (`betweendbg` all `-1` behind a wall that is visibly
  in front of the camera).
- **H2 - near-plane clip**: the ray hits and the camera pulls to `hit - 18`,
  but the host engine's view near plane is larger than 18 u, so the wall is
  clipped at pull distance (wall visibly disappears when close; `betweendbg`
  shows a hit).

Measured offline (this session, correcting the old text):

- The world-object bake is complete (4963/4965; FCOL v2: 695 meshes / 4957
  instances). The nearest baked AABB at the spawn is **1425 u away = 14 m**,
  consistent with the big wall on the right of `rc_03_30000ms.png` (instance
  i0013, AABB x 24759..27155, z 22198..24605); the engine space ray hits the
  same building at 1865 u (`obstprobe sceneE=1865`). Nine instances sit within
  3000 u. "1425 u away" does **not** mean the visible buildings are missing.
- The engine terrain ray is literally the call the engine's own
  `RayIntersectionTerrain` makes (`[scene+0x950]->vt[+0xB8]`,
  `proof/netcode/disasm/host_ray_terrain_fn.txt`) - the game's terrain backend
  is wired, not a host approximation.
- Still genuinely unproven: whether every *drawn* surface (landscape render
  mesh / bd / subscene `.SRScene` meshes) is reached by the current rays.

Run Step B before any bake work.

### Step B first pass - result (2026-09-26, current tip + setdbg probe)

Controlled wall: inst 262's north face, spawn `(18097, 500, 22171)`, wall
z~21971, default camera line crosses it at ~200 u.

| Clearance | bake hit | pull len | postdbg move | frames |
|---|---|---|---|---|
| 18 | 206 u | 188 | 0.0 | clean, no holes (`stepb_clr18`) |
| 40 | 206 u | 166 | 0.0 | clean (`stepb_clr40`) |
| 80 | 206 u | 126 | 0.0 | clean (`stepb_clr80`) |

- Engine space ray agrees with the bake (`obstprobe sceneN=204`).
- `betweendbg` clear while unobstructed; no host Y move; no near-plane hole.
- => this wall is **fully covered** and the 18 u rule is exact. No H1 at a
  drawn building wall, no H2 at 18 u. Do not bake on this evidence.

Found instead (new, measured): `SetCameraPos` (`MovieEngineCLR.KGSceneCLR`,
4th parameter is `bKeepY`, not "aim") **cannot set an absolute Y**:

- `bKeepY=false` (current call) clamps Y **up to the render surface at the
  camera xz**. At the cliff pit `(18284, 500, 26095)` (spawn inside the
  inst503 rock formation) the intent 727 became 1463 while the PhysX terrain
  object reports 622 - the clamp is scene geometry, not the heightfield.
  `setdbg moved=735.6` proves it happens at set time, not in FrameMove.
- `bKeepY=true` keeps the engine's current Y (6405 at that spawn) and ignores
  ours.
- The clamped camera leaves the anchor->camera line and can sit inside the
  rock (inside-geometry frames, `setdbg`/`postdbg` telemetry). Registered as
  host deviations B6; exit = native camera path (Step C).

### Step B near-plane ladder - result (2026-09-26)

Same wall (hit = 206 u), `RC_CAM_DEBUG=1 RC_PLAYER_HIDE=0`, camLen = hit - C:

| Clearance | camLen | character in frame |
|---|---|---|
| 18 | 188 | full |
| 60 | 146 | full |
| 90 | 116 | full |
| 110 | 96 | full (back fills the frame) |
| 125 | 81 | back cut open, interior/front visible |
| 140 | 66 | mostly gone (skirt fragment only) |
| 170 | 36 | gone |
| 190 | 16 | gone |

- ~~Host view near plane N ~= 60-70 u~~ **corrected 2026-09-27**: the model
  fade motions above are the **engine's own model culling/fade** when the
  camera is inside/very close to the character (game anti-clip behaviour), not
  the view near plane. The `stepb_clr18/40/80` wall frames show sand within
  60 u in front of the camera is never clipped, so the near plane is NOT large.
- **H2 as framed is therefore unproven/disproven**; no wall-clip holes were
  observed at 18 u. A definitive wall-distance test still needs a working
  fixed-camera harness (`RC_FIXED_CAM` only sets once and the engine overrides
  it).
- The character fade threshold (~90 u) roughly matches the park-below hack, so
  B1's host approximation stays for now. Evidence: `reborn_out\stepb_np*`,
  `stepb_np2_*`.

## 5. What to do next (corrected order - classify before baking)

**Step B - classification (first pass DONE 2026-09-26: result in §4; the recipe
below is reusable for other spots).**
At a wall that is visibly drawn (e.g. the spawn-east building i0013, or a
楼兰三间房 wall), run with `RC_CAM_DEBUG=1` and a `RC_CAM_CLEARANCE` ladder
(18 -> 40 -> 80 u) plus a screenshot per rung:

- `betweendbg` hit while the wall is invisible -> **H2 near-plane clip**:
  bracket the clearance where the wall becomes visible (that is the host near
  plane); fix by native near-plane set, or a documented clearance deviation.
- `betweendbg` all `-1` while the camera is past a drawn wall -> **H1 coverage
  gap**: only then do Step A.
- If nothing disappears up to 100 u, clipping is not the cause.

Keep the `betweendbg` invariant (camera->anchor must be clear while
unobstructed) in every live test. Do not start a bake before this run.

**Step A - only if Step B proves a coverage gap.** The old premise is shaky:
the extracted `landscape/` folders contain only the 539 B `landscapeinfo.json`
descriptor (RegionSize 512, 8x8 regions, UnitScale 100 - no mesh list), the
member naming is not discovered, and the drawn building may be a bd/subscene
(`.SRScene`) asset rather than `landscape`. We already have the heightfield
and the exact terrain ray. If a bake is required:
1. Read the descriptor copy that does exist:
   `proof/map_spike/龙门寻宝_landscapeinfo.json` (repo) or
   `C:\jx3tmp\landscape\out\data\source\maps\龙门寻宝\landscape\` (also
   `C:\jx3tmp\proc\...`). The
   `C:\jx3tmp\inspack\out\...` path in the old text does not exist.
2. Recon which member class draws the gap first (landscape / bd / subscene
   `.SRScene`); then extract candidates with
   `tools/netcode/extract_pak_paths.py --list <candidates>` and
   `tools/netcode/extract_hpkg_member.py` (pak files live under the JX3
   install / SeasunDownloader cache; earlier `ent_all` extraction used the
   same tools).
3. Parse with the repo-root **`mesh.py`** (there is no `tools/mesh.py`) and
   write a **camera-only bin** with the FCOL v2 writer from
   `tools/export_structure_collision.py`, loaded beside the structure bin by
   `FoliageCollision`. Do **not** append camera geometry into
   `<map>_structure_collision.bin` - that would add it to player collision
   (`Resolve`/`SupportHeight`).
4. Verify: nearest baked instance at the wall ~0; `betweendbg` hits; the camera
   pulls with the 18 u rule.

**Step C - native bridge (deferred, requirements now measured).** Install VS
Build Tools (MSVC + Windows SDK, ~2-4 GB), then write one version-checked
`camera_shim.dll`. It must cover, in priority order:

1. **View near plane get/set** (`view vt+0xA8`): the host N is ~60-70 u
   (measured), the game's 18 u clearance can never work until N is small
   (C7/B1/B6 exit).
2. **Absolute camera Y / look-at**: `KGSceneCLR.SetCameraPos` cannot set Y
   (`bKeepY=false` clamps to the render surface, `true` keeps the engine's
   stale Y) and has no look-at; the shim sets pos + look-at directly on the
   engine camera (A1-A9, B6, D3 exit).
3. **`FilterCamera` / mask `0x301` scene query** with the real filter object
   (D1/D4/D5 exit; removes the raw-RVA backends and the vertical no-hits).

Acceptance: camera Y round-trips exactly; near plane visible in A/B
screenshots; the guarded scene ray returns hits. Brittle: version-check the
engine module before any vtable/RVA use.

### Step C status (2026-09-27, uncommitted)

- Toolchain installed: VS 2022 Build Tools 17.14.41, MSVC 14.44.35207,
  Windows SDK 10.0.26100. Build: `native\build_shim.cmd` -> `camera_shim.dll`
  in bin64 (version-guarded on the engine PE timestamp/size).
- `client/CameraShim.cs` loads/self-tests it with a managed fallback;
  `RC_CAM_IL`, `RC_CAM_SCAN`, `RC_CAM_POKE`, `RC_CAM_NATIVE` are the probe
  switches (all off by default; default behaviour is unchanged).
- Recon results: host classes + vtable RVAs - `KG3D_Camera` 0x21EF6E0 /
  0x21EF6F0, `KG3D_SceneView` 0x21D4970 / 0x21D4DE8, `KG3D_Window` 0x21E1970,
  `KG3D_Scene` 0x21C5598; SceneView ctor at 0x1809CBB50; live instances found
  by `RC_Probe_Deep` (SceneView at `scene+0x18C8`, camera records in the
  object at `scene+0x910`*); the active camera holds the live position at
  +0x28/+0x30 with target at +0x10 after.
- **Blocker:** writes to those camera/snapshot fields do not drive the render
  (the render reads a snapshot the managed path writes through an untraced
  route). Managed entry-point thunks were dumped with `RC_CAM_IL`; the next
  step is to follow the thunk target and map `SetCameraPos/GetCameraPos` to
  the engine camera call, then re-test the absolute-Y write.
- Differential scans (`RC_CAM_DIFF`, `RC_CAM_DIFF2`):
  - the managed `SetCameraPos` X/Z input lands at `[engine+0x1F58]+0x630`
    (transient - consumed within a frame);
  - after a frame the position/target/direction records live at
    `[scene+0x910]+0xF68/0x11A0/0x1988/0x1998/0x1BC0/0x1BCC` and
    `[scene+0x918]+0x98/0x3F0` (position + target + direction);
  - writing the model's exact placement into those records between
    `FrameMove` and `Render` did **not** move the render (evidence
    `reborn_out\pre_idx{0,1,7}`) - the renderer appears to consume a
    view/projection matrix rebuilt during `FrameMove`.
  - Next: locate that matrix (or the function that builds it) and write the
    exact camera there, or hook the matrix build.
- Near-plane setter: deferred pending the wall test (see §4 correction).
  Filtered ray: query wrapper `0x180446920` still to be disassembled.
- **2026-09-27 late findings.** `RC_CAM_OBSTDBG=1`: at the user spot the
  winning hit is `probe1 off=(-22,0,0) bake=11.4` - the west footprint corner
  starts inside the post (face 21 u west) and returns its exit distance
  (C2 artifact) -> pull -7 -> camera 6.6 u in front of the head; the
  between-segment is clear and the character is engine-culled (proved with
  `RC_PLAYER_HIDE=0`, frame identical). `RC_CAM_OBSTDBG` also shows the
  vertical ladder false-firing at 85.6 u from a roof 9045 u overhead.
  `RC_CAM_CLR_SEQ` ladder (inst 262, clearances 18..2) is clean - the wall is
  behind the camera at every rung, no forward clip (`proof/nearplane_ladder.png`).
  `RC_CAM_LOOKPACK=1` turns the view (partial, ~2.43 rad) and was validated on
  the user spot (`proof/lookpack_ab.png`). Default behaviour byte-identical to
  the user repro (mean 0.48/255). No shippable fix yet; the look-at and the
  footprint basis are the two measured roots.

### Step C plan (re-prioritized 2026-09-27)

**Recon vs fix line.** Calls into engine functions (even undocumented) =
allowed. Writing engine memory/records/matrices as the shipped fix = host
bypass; register it in the deviations list; never default-on. Default client
behaviour stays unchanged while the probes are env-gated.

**1. Near plane first (the visible cliff cut / close-surface clipping).**
- Status: SceneView `+0x88`/`+0x98` (values ~20.67) were poked safely to 5.0
  one at a time (`RC_CAM_POKE`); the frame did not change, so they are not the
  near plane. `+0x444` (0.1, next to the FOV factor at `+0x44C`) was poked to
  0.02 (per-frame) at the user spot - **no pixel change** (no visible geometry
  lies between 2 cm and the old value there); `0.005` crashes `Render`, so the
  field is live but remains unidentified. The `RC_CAM_CLR_SEQ` ladder at inst
  262 (wall 18..2 u in front of the camera, `proof/nearplane_ladder.png`)
  shows the forward view intact at every rung - no clip in front within 18 u.
- Conclusion so far (2026-09-27): the user's repro frame is **not** a
  near-plane cut. The camera is 6.6 u in *front* of the head (signed pull -7
  from a corner probe that starts inside the wooden post, C2) and the engine
  view still points the old way (no look-at, D3), so the render shows the
  scene from inside the character (see item 2).
- Next: if a near plane is still needed, the projection block must be found in
  the host engine (the game-DLL RVA `0x180446920`/`0x180473...` family does
  not map to `KG3DEngineDX11EX64.dll`, which lacks `FilterCamera` and
  `bObscatleCamera` entirely).

**2. Camera transport (absolute Y / look-at).**
- Tested and refuted first per the plan: the engine input field
  `[engine+0x1F58]` does not carry Y - after `SetCameraPos(x, 12345, z)` no
  Y value appears in the dump, so the clamp is downstream. Writing the input
  field cannot set an absolute Y.
- Post-process records (`[scene+0x910]+...`, `[scene+0x918]+0x98`) written
  between `FrameMove` and `Render` did not move the render.
- **Look-at (2026-09-27, experiment only, default OFF).** The host view
  direction never follows the camera position, so a signed pull that crosses
  the anchor makes the render look *away* from it (D3) - that is the user's
  see-through, not clipping (clearance ladder 2-18 u in front: clean, and the
  signed pull is legitimate: the anchor sits inside the rock mesh, the cavity
  wall is ~11 u behind the head - C2 correction). `RC_CAM_LOOKPACK=1` turns
  the engine orbit 180 deg when crossed (mirrored pitch) via
  `ExecAction(1,...)` in <=200 px steps with a spaced `measureView` closed
  loop (host clamps a burst; converges to 0.005 rad). It fixes the stationary
  repro (`proof/lookpack_ab.png`) but the host engine AVed when it fired while
  moving (D6, 3 logs), so the flip is **off by default** and, when enabled,
  only engages while `!movingNow`. The C2 "phantom hit" that was expected to
  make the crossing disappear does not exist (real geometry); the crossing
  persists while moving by design of the native signed pull.
- P1 hygiene landed with this batch: camera probe rays are front-face only
  (`Raycast(..., frontFacesOnly: true)`; render rays see front faces) and the
  vertical ladder is windowed (a 9045 u roof overhead no longer pulls the
  camera to ~68 u). Controlled wall still exact: hit 206 -> len 188. The C2
  premise correction (11.4 hit is real rock-cavity geometry, anchor inside the
  rock mesh) is in the deviations register.
- P2 status: the orbit route is rate-sensitive, not movement-sensitive. A
  single 600 px (1.08 rad) orbit event AVs the host in the shader parser
  (`+0xA6C75A`) even at an open spawn; a rate-limited turn (<=1.5 rad/s,
  <=20 px/event, the engine's fMaxAngelVel) ran 20 s clean, and the flip now
  uses that rate (full 180 deg ~3.5 s) and converges (measured 1.575 vs
  1.571). It stays an experiment, default OFF, stationary-only.
- P2/T3 result (2026-09-27 late): **the moving flip cannot be made safe.**
  T1 confirmed rate safety in isolation (aim frozen, <=20 px/event, ~17 rad
  over 10 s clean), but the T3 test - rate-limited flip enabled while walking
  (11 flips, clearance-200 crossing injection) - AVed at the D6 DataStore
  offset `+0x11D03B6`. The moving view therefore keeps the old direction by
  design; the stationary flip stays an experiment (default OFF). The yaw-diff
  probe (`RC_CAM_YAWFDIFF`, rate-limited) remains for the route-1 recon; its
  first run also hit an engine AV (`+0x9DD289`, prologue push, possible stack
  exhaustion under shim scan + rotation).
- P3 acceptance (default-env walking run, no crash, clean F9 A/B) is **not
  met**: by default the moving view still crosses with no look-at, and the
  moving look-at is blocked by D6. This is a host-engine limitation, not a
  remaining camera-rule gap: with the installed editor engine, any moving
  view change into unloaded content can AV (missing build-machine DataStores).
- **Crash found (2026-09-27, D6).** With the flip default-on the app AVed
  three times (`KG3DEngineDX11EX64+0x11D03B6`, material DataStore null deref),
  each within ~0.2 s of a flip firing **while moving**; stationary flips never
  crashed (minutes of runs) and no-flip roaming did not crash (~8 runs).
  Mitigation shipped: the flip engages/keeps only while `!movingNow`, so the
  stationary repro (the user's F9 case) still flips and the moving case keeps
  the old (unflipped) view. 3x60 s moving demo runs after the guard: clean
  (0 flips, no AV). Root is a host-engine hole, tracked as D6.
- Next (exit for D3): native look-at / view-matrix write via the shim and
  delete the orbit-flip approximation; for D6, isolate the content whose
  material store is missing (crash dump / first-render trace).

### Engine camera API contract - Phase 1 done (2026-09-27 late)

`proof/netcode/engine_camera_contract.txt` has the full evidence. The managed
`KGSceneCLR::SetCameraPos` IL (MovieEngineCLR.dll, RVA 0x2728AC, decoded with
dncil) shows the engine's own contract:

```
camera = scene->vt[+0x50](scene)             ; scene = this.m_pScene
camera->vt[+0x48](&pos)  camera->vt[+0x50](pos, 0)
camera->vt[+0x60](&tgt)  camera->vt[+0x58](tgt, 0)   ; look-at setter
scene->vt[+0x70](&pos, &clampedY)            ; then if (clampedY > pos.y)
                                               pos.y = clampedY  <- the B6 clamp
```

The managed API then translates the target by the same delta as the position,
which is exactly why the view keeps its direction when the pull crosses the
anchor; the `vt+0x58` setter is the game's look-at path (fixes D3 and the B6
clamp at the same time).

**Phase 2 blocked (no fix landed).** The native `m_pScene` behind the managed
wrapper is not reachable from outside: (a) the `Get3DScene2` scene's vt+0x50 is
not get-camera (returns 0); (b) the wrapper's fields (address via `__makeref`
and via weak-GCHandle deref, same value) expose no engine-vtable pointers;
(c) the object scanner's `KG3D_Camera` has vt+0x50/0x58 pointing into
mid-function code - calling them AVs the engine (observed). The shim exports
`RC_CamUseScene`/`RC_CamUseClr`/`RC_CamUseObj`/`RC_CamSetVt2`/`RC_DumpClr`
exist but are **not called by the client** (the call path was removed to keep
the default build safe). Next routes: hook the managed SetCameraPos call site,
or a C++/CLI helper with the real headers. The default app is unchanged
(P0/P1 state, `camera_smoke` 25/25, default spot run clean).

**3. Repro protocol.** Use `F9` (USERREPRO: `rc_user_<ms>.png` + full
camera/ray dump) at the reported spot; every fix is validated against that
frame, not against invented walls.

**4. Stop-list.** `RC_CAM_SNAPGUARD`, proximity/wall gates stay registered
band-aids (B5/B7) until a game-path replacement lands; no new default-on
approximations.

## 6. Deviations register / docs

- `docs/camera/HOST_DEVIATIONS.md` - all 37 host-vs-game items with exit
  criteria (A1-A11, B1-B6, C1-C9, D1-D5, E1-E7), current as of the Step B pass.
- `docs/camera/RECONCILIATION_STATUS.md` - fix status per audit item.
- `docs/camera/DRAG_MODEL.md`, `docs/camera/CLOSE_RANGE_RESEARCH.md`,
  `docs/camera/WALL_OBSTRUCTION.md` - proven game behaviour + evidence.
- `docs/camera/FIX_SPEC.md`, `docs/camera/COMPLETION_PLAN.md` - the original
  fix spec/plan.

## 7. Useful runtime knobs and logs

- `RC_CAM_DEBUG=1` -> `camdbg` (yaw/pitch/dist/r/obst/hit/len/eff/clamp),
  `obstprobe` (bake + engine ray hits), `betweendbg` (camera->anchor invariant),
  `setdbg` (host moved the camera after placement; B6), `postdbg` (whether the
  engine moved our placed camera after FrameMove).
- `RC_CAM_CLEARANCE=<u>` (obstruction clearance), `RC_CAM_MODE=<row>`,
  `RC_CAM_9RAY=1`, `RC_MOVE_PITCH=1`, `RC_VIEW_ANGLE=<factor>`,
  `RC_PLAYER_HIDE=0` (disable the park-below hack for near-plane ladders),
  `RC_CUSTOM_DAT=<path>`, `RC_LOAD_CUSTOM_DAT=0`, `RC_ORBIT_TEST=1`,
  `RC_DEMO=1`, `RC_DEMO_COLLIDE=1`+`RC_COL_TELEPORT=1`, `RC_SHOTS=...`.
- Logs: `bin64\reborn_out\reborn.log` and per-run `reborn_<timestamp>.log`.

## 8. Gotchas

- Starting a build while the client runs fails (exe locked): kill
  `reborn_client` first, and relaunch it after.
- The client takes ~25 s to load the map; live probes need ~50-70 s runs.
- `apply` note: the map data is streamed from the **paks**; no loose map files
  under the MovieEditor tree.
- Keep `img` screenshots: `rc_00..rc_05` in `reborn_out`.
- The 18:19 `rc_00..rc_02` PNGs are thumbnails; `rc_03_30000ms.png` (17:52)
  is the full-size spawn frame.
- `main` is not perfectly clean: `perf_config.ini` is untracked there
  (unrelated to the camera work; leave it alone).
- Doc staleness: the `control-system-notes` camera pages,
  `docs/camera/CLOSE_RANGE_RESEARCH.md` §3 and the host section of
  `docs/camera/WALL_OBSTRUCTION.md` predate the current tip; treat this file and
  `docs/camera/HOST_DEVIATIONS.md` as current.

## 2026-09-28: engine-faithful camera path landed (D3/B6 closed)

Recovered and wired: the managed `KGSceneCLR` holds the engine scene proxy in
`m_pScene` (`IKG3DSceneProxy*`); it is reachable from C# by reflection
(`FieldInfo` + `Pointer.Unbox`) - the proxy/camera vtables live in
`KG_EngineEditorX64.dll`, which is why the engine-module-only guards rejected
every previous scan. The shim calls the managed IL's own setters
(`+0x50` position, `+0x58` look-at) with SEH guards; absolute Y and
look-at-at-the-anchor are live. Evidence: 2026-09-28 logs
`reborn_20260928_140358` (stationary), `_140659` (drag sweep),
`_140838` (movement demo): `engineSet direct rc=0`, view tracks the anchor,
`postdbg 0.0`, `DONE`, exit 0. Default on; `RC_CAM_ENGINESET=0` opts out.
The orbit-flip/aim-emulation band-aids are obsolete (register updated).
