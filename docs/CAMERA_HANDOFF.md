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
- Committed tip: **`45f8ae5`**. The `camara-fix` branch adds **41 commits over
  `main`** (`a8471b4`..`45f8ae5`: drag model + aim closed loop, sphere offset,
  wall obstruction, engine rays, FOV, modes).
- **Uncommitted (M0+M1)**: `docs/CAMERA_HOST_DEVIATIONS.md`,
  `RC_CAM_CLEARANCE` override, `betweendbg` telemetry, signed pull in
  `CameraObstruction`, final-camera wall gate, smoke test rename. Build passes,
  smoke 25/25, live run clean.
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
  X=cur+S*dt`, 0.05 rad guard (`0x180674314`). All in `docs/CAMERA_DRAG_MODEL.md`,
  `docs/CAMERA_CLOSE_RANGE_RESEARCH.md`, `proof/netcode/disasm/*`.

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

## 5. What to do next (corrected order - classify before baking)

**Step B - classify at a drawn wall (one run per rung, do this FIRST).**
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

**Step C - native bridge (stays deferred).** Only after A/B are exhausted.
Install VS Build Tools (MSVC + Windows SDK, ~2-4 GB), then write a single
version-checked `camera_shim.dll` exposing `SetLookAt`, near-plane get/set
(view `vt+0xA8`), and the `FilterCamera`/mask `0x301` scene query. That deletes
the aim band-aids (A1-A7) and gives exact wall clipping. It is brittle and
needs the toolchain; do not start before B (and A only if B proves a gap).

## 6. Deviations register / docs

- `docs/CAMERA_HOST_DEVIATIONS.md` - all 31 host-vs-game items with exit
  criteria (M0, uncommitted).
- `docs/CAMERA_RECONCILIATION_STATUS.md` - fix status per audit item.
- `docs/CAMERA_DRAG_MODEL.md`, `docs/CAMERA_CLOSE_RANGE_RESEARCH.md`,
  `docs/CAMERA_WALL_OBSTRUCTION.md` - proven game behaviour + evidence.
- `docs/CAMERA_FIX_SPEC.md`, `docs/CAMERA_COMPLETION_PLAN.md` - the original
  fix spec/plan.

## 7. Useful runtime knobs and logs

- `RC_CAM_DEBUG=1` -> `camdbg` (yaw/pitch/dist/r/obst/hit/len/eff/clamp),
  `obstprobe` (bake + engine ray hits), `betweendbg` (camera->anchor invariant),
  `postdbg` (whether the engine moved our placed camera).
- `RC_CAM_CLEARANCE=<u>` (obstruction clearance), `RC_CAM_MODE=<row>`,
  `RC_CAM_9RAY=1`, `RC_MOVE_PITCH=1`, `RC_VIEW_ANGLE=<factor>`,
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
  `CAMERA_CLOSE_RANGE_RESEARCH.md` §3 and the host section of
  `CAMERA_WALL_OBSTRUCTION.md` predate the current tip; treat this file and
  `CAMERA_HOST_DEVIATIONS.md` as current.
