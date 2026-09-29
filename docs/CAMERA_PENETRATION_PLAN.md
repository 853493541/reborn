# Camera penetration plan (accurate, 2026-09-28)

Goal: close the remaining wall see-through by making the camera obstruction set,
per-mesh gate and probe geometry match the game - no invented heuristics.

This version corrects the earlier draft against verified facts (host engine
split, the landed engine look-at path, the foliage bin contents, and the
mesh-property data availability).

## Verified baseline (this worktree)

- Engine-faithful camera set path is landed and default-on (uncommitted):
  `KGSceneCLR.m_pScene` read by reflection (`IKG3DSceneProxy*`, vtable in
  `KG_EngineEditorX64.dll`), camera via `scene->vt[+0x50]`, then the managed
  IL's own calls `cam->vt[+0x50](pos,0)` (absolute Y) and
  `cam->vt[+0x58](anchor,0)` (look-at), SEH-guarded in `camera_shim.dll`.
  Evidence: 2026-09-28 runs `140358` (stationary), `140659` (drag sweep),
  `140838` (movement): `engineSet direct rc=0 native=True`, `vyaw` tracks the
  model aim through the crossing, `postdbg moved=0.0`, `DONE`, exit 0.
- The dominant reported defect class - view keeps its direction when the
  signed pull crosses the anchor - is fixed by that path. Re-measure before
  assuming the old % .
- Camera probes currently call
  `col.Raycast(..., structuresOnly: true, frontFacesOnly: true)`
  (`client/RebornClient.cs:1645`): baked **foliage solids are excluded**.
- The foliage bin holds only solid patterns 4/5/6/7 (deadwood/cactus/rocks,
  394 instances, verified by parsing `龙门寻宝_foliage_collision.bin`);
  grass patterns 2/3 were never baked (no mesh). Game content ships those
  solids with `bObscatleCamera=1`.
- Mesh-property data (`bObscatleCamera`) is **not on this machine**: no
  `_HttpFileForDebug_`, no `[Display]` ini in `cache-extraction`, and the flag
  string is not inside the extracted `.mesh` assets (byte-scan). Per-mesh
  gating needs a pak/CDN source or stays blocked.
- Host engine split: camera/proxy in `KG_EngineEditorX64.dll`;
  `KG3DEngineDX11EX64.dll` has no `FilterCamera`/`bObscatleCamera`. The game's
  filter semantics must be replicated offline; the game-DLL probe-basis fields
  may not map to the host camera.

## Metric (user definition)

Stand near a wall and adjust the angle; count frames where a drawn surface
between camera and character is see-through (or the camera sits inside
geometry). Report penetration frames / total per spot/route, measured on
screenshots.

## Test set (coordinates)

| # | Spot | Spawn | Why |
|---|---|---|---|
| T1 | rock cavity (user spot) | `18985,682,24515` | crossing + look-at regression |
| T2 | drawn wall inst 262 | `18097,500,22171` | pull exactness (hit 206 -> len 188) |
| T3 | spawn / cliff route | default `23334,761,24224` + walk | general route |
| T4 | solid foliage | `19183,864,31720` (pattern-4 deadwood; nearest to the play area) | foliage gate |
| T5 | decorative prop (`bObscatleCamera=0`) | pending P3 | false-pull check |

Per spot: `RC_CAM_DEMO=1 RC_CAM_DEBUG=1 RC_SHOTS=2000,3000,...,12000`
(yaw sweep 2-6 s, pitch 6-12 s), screenshots + `camdbg`/`betweendbg`/`obstprobe`;
count penetration frames by eye.

## Workstreams

**P0 - baseline re-measure (no code).** Run T1-T4 on the current default.
Classify each penetration frame: view direction (should be 0 now), near-plane
clip, coverage gap (`betweendbg -1` behind a drawn wall), foliage, footprint,
other. Decision gate: which classes remain and their share.

**P1 - include baked foliage solids (small, game-faithful data).**
Change the camera probe call to `structuresOnly: false` (keep
`frontFacesOnly: true`). The bin contains only cacti/rocks/deadwood - the
classes the content ships as camera blockers - so no grass/decor is added.
A/B at T4: before, the camera can sit inside the tree; after, it pulls with the
18 u rule. Regression: T2 unchanged, T1 view still aims at the anchor.

**P2 - real probe footprint (verified recon on the host camera).**
Use the shim on the live camera object (already reachable) and the SceneView
(`+0x44C` FOV factor known) to find the projected corner-offset fields
(game camera used `+0x12c..+0x134` / `+0x144..+0x14c`). `Snap/SnapDiff` while
toggling `SetViewAngleFactor` locates the fields that scale with FOV/aspect.
If found: replace `foot = 22` with the recovered formula and re-run T1-T3.
If the host has no such fields: keep 22 u and register C2 as blocked.

**P3 - per-mesh `bObscatleCamera` (data).**
Find the mesh-property inis: search the paks (and the SeasunDownloader CDN
cache) for the `[Display]` ini set, using the existing pak tooling
(`tools/netcode/extract_pak_paths.py`, `extract_hpkg_member.py`,
`pss_assets.run_pakv4`); if absent, try the CDN mini-update route. If found,
extend `tools/export_structure_collision.py` to FCOL v3 with a per-instance
flag and gate the camera ray. If not found: keep the class gate from P1
(evidence-based for cactus/rock/deadwood) and register the decorative-prop
over-block as blocked; no heuristic replacement.

**P4 - missing drawn classes (only if P0 proves a coverage gap).**
Recipe already documented: descriptor `proof/map_spike/龙门寻宝_landscapeinfo.json`
(RegionSize 512, 8x8, UnitScale 100 - no mesh list); enumerate/extract
landscape/bd/subscene members with `extract_pak_paths.py` /
`extract_hpkg_member.py`; parse with repo-root `mesh.py`; write a **camera-only**
FCOL bin loaded beside the structure bin (never appended to the player bin).
Larger bin is accepted (+50-150 MB); consider per-region streaming only if load
time regresses.

**P5 - remove obsolete band-aids (after A/B parity).**
Delete `RC_CAM_LOOKPACK` (default off; superseded by the engine look-at),
`RC_CAM_SNAPGUARD` (B7), the B5 wall gate, and the vertical window if P4
replaces it. Keep the engine set path, the signed pull/window/flex (game
rules), the engine terrain/space/entity rays, and the heightfield march as the
registered terrain substitute (host vertical backend returns E_FAIL).

## Acceptance

- Per spot: penetration frames = 0 by eye after P1/P2/P4; T2 pull 206 -> 188
  unchanged; T1 `postdbg 0`, view aims at the anchor; T4 cacti/rocks/deadwood
  block, grass none (not baked); T5 once flags exist: `=0` props do not pull.
- `camera_smoke` ALL PASS; default env; clean `DONE`; no crash events.
- Register updated per exit (C2 closed or blocked, D2 closed or blocked,
  B5/B7 deleted, C7 unchanged).

## Risks / unknowns

- Property inis may genuinely absent from local paks -> D2 stays blocked (no
  heuristic will be substituted).
- Host camera may lack basis fields -> foot=22 stays registered.
- Landscape/subscene format may not parse with `mesh.py` -> separate parser
  task (size estimate first).
- Structure-prop over-blocking remains until flags exist (decorative
  `bObscatleCamera=0` objects still pull the camera).

## Progress log

### 2026-09-28 - P0 baseline (no code) + P1 foliage class gate (landed)

P0 runs: T1-T4, `RC_CAM_DEMO=1 RC_CAM_DEBUG=1 RC_SHOTS=2000..12000`,
screenshots + logs copied to `bin64\reborn_out\pen_T*` (contact sheets).

- T1 cavity (`18985,682,24515`): 11/11 frames no see-through. During the jam
  `vyaw = yaw +- pi` and `vptich` mirrored (the engine look-at is active), the
  frames show the close wood/rock - not the world through it. Hits 11/40/8/6/18,
  crossings `len` -4..-15.
- T2 wall 262: 11/11 clean; `hit=206 len=188` exact at the sweep start.
- T3 spawn route: 11/11 clean.
- T4 deadwood (`19183,864,31720`): ~2-4 frames with the camera at/inside the
  trunk, the character occluded by a sliced surface - the only remaining class
  in this sample; foliage was excluded from the camera rays.

P1 landed (small, content-faithful):
- `client/RebornClient.cs`: the 5/9-probe raycast and the final-camera gate now
  call `col.Raycast(..., structuresOnly: false, frontFacesOnly: true)`; the
  foliage bin holds solids only (patterns 4/5/6/7), so no grass/decor is added.
- T4p1: foliage pulls fire (`hit/len` 186/168, 16/4, 9/-9, 206/53, 10/-9,
  16/-2); the trunk frames show solid bark, no sliced surface
  (`pen_T4p1\contact.png`).
- T2p1 regression: `hit=206 len=188` unchanged; T1p1 regression: clean `DONE`,
  exit 0, crossings keep the engine look-at. `camera_smoke` ALL PASS.

P3 lead (property inis): `KG3DMeshFileDataLoader::_LoadMeshProperty` (local
`KG3DEngineX64.dll`, 2026-09-27 build) loads `[Display] bObscatleCamera` from a
`piIniFile`; `KG3DMesh::SavePropertyToIni` writes the same keys
(`bAutoProduceObstacle`, `bObscatleCamera`, `bHeightTest`, `bOccluder`).
The ini path is built by the caller `KG3DMeshManager::LoadMesh` - disassemble
that caller next, then extract the inis via the pak tools
(`tools/netcode/extract_pak_paths.py` + `pss_assets.run_pakv4`).

Not yet done: T5 (needs flags), P2 (footprint recon), P3 extraction, P4
(missing classes), P5 (band-aid removal).

### 2026-09-28 - P3 completed for the baked set (per-mesh gate live)

Property path convention recovered from the local game build
(`KG3DEngineX64.dll` 2026-09-27): `KG3DMeshFileDataLoader::Load` (0x180264540)
derives `bin`/`bsp`/`ini` siblings by extension swap (0x180264592 ->
0x180524270) and `_LoadMeshProperty` (0x180267360) opens the `.ini` and reads
`[Display] bObscatleCamera` (+ bAutoProduceObstacle/bSelectable/...).
`KG3DMesh` ctor defaults the whole block to 1, so a missing ini blocks.

Data extracted via the official PakV4 tool + `tools/export_camera_flags.py`:
- 692 mesh paths, 592 flags from sibling inis, **316 zero-flag meshes**;
- rock foliage patterns 6/7 ship `bObscatleCamera=0` (the game ignores them:
  P1's class gate was over-blocking); deadwood/cactus (4/5) have no ini ->
  default 1 (block); buildings/walls 1; wood boxes / tomb decor / canopies 0.

Implementation:
- `tools/export_structure_collision.py` writes `<bin>.cflags`
  (magic 'CFLG', per-mesh bytes; 317/695 zero) and copies it beside the bin;
- `client/FoliageCollision.cs`: loads the sidecar (MeshData.blocksCamera),
  gates camera rays on it (`Raycast(..., cameraGate: true)`), and applies the
  data-derived foliage pattern table (6/7 do not block);
- client camera probes + final gate now pass `cameraGate: true`.

Verified (gate live): T2 wall `hit=206 len=188`; T4 deadwood `hit=186
len=168` (rocks no longer pull); T1 cavity unchanged (`hit=11 len=-7`, engine
look-at active); `camera_smoke` ALL PASS. Note: the T4 demo sweep hit the
known D6 engine AV (`+0x11D03B6`) once - intermittent content AV, separate
from the gate (idle run DONE exit 0).

Remaining: P2 (footprint), P4 (drawn classes outside the bake), P5
(band-aid removal). Per-map .cflags export for other maps.

### 2026-09-28 - gate verification + audit notes

- Gate verified live against content data:
  - flag-1 wall (T2): `hit=206 len=188` exact;
  - flag-1-by-default deadwood (T4): `hit=186 len=168`;
  - flag-0 foliage rocks: hardcoded pattern gate (patterns 6/7) and the
    extracted `石头\wj_石头006_00{5,7}_hd.ini` both say 0.
- T5 live A/B attempt (nearest isolated rock at 181571,107008): inconclusive -
  the rock is a small ground-level stone (scale 0.64-0.9) and head-height /
  camera rays pass above it (`obstprobe` all -1), so no pull occurs either way.
  Needs a suitable flag-0 blocker (structural decorative prop) for a visual
  A/B; the mechanism is data-verified.
- Per-map flags: only `龙门寻宝` has an entity dump under
  `C:\jx3tmp\ent_all\...`; the other map bins in `collision_data` have no
  `.cflags` and keep the pre-gate behaviour (all meshes block) until their
  sceneinfo dumps are extracted.
- P2 footprint: see `proof/netcode/engine_camera_contract.txt` (ctor basis =
  identity x 500 @ 0x68064C; per-frame projection not located; foot=22 stays
  registered).

### 2026-09-28 - P0 D6 pacing + P1 per-map flags (both landed)

P0 (D6 view pacing):
- `scene.GetLoadingProgress()` measured at 1.000 for the whole session after the
  synchronous map load, so the streamed content the AV hits is invisible to it;
  HasLoadingTask needs a handle we do not hold.
- First mitigation attempt: orbit/view-rate cap at 1.5 rad/s (default on,
  `RC_CAM_NORATE=1`). LATER REVERTED TO OPT-IN (`RC_CAM_RATECAP=1`): it bends
  drag feel (the user noticed the controls as broken) and does not prevent the
  crash. The client also gained a dormant loading-state quarter-rate
  pacing (`RC_CAM_LOADPACE=0` disables). Both registered as host pacing in D6.
- T2 regression with the cap: `hit=206 len=188`; 4/4 T4 demo-sweep stress runs
  clean (crash is intermittent - risk reduction, not proof); smoke ALL PASS.

P1 (per-map flags): all four local map dumps exported and rebuilt
(`tools/export_camera_flags.py` + `export_structure_collision.py --flags`):
- 龙门寻宝 (55476992 B): 317/695 zero
- 龙门寻宝_夜晚 (55935816 B): 331/709 zero - live verified (`camflag0=331`,
  cavity pull `hit=11 len=-7`, engine look-at, exit 0)
- 天原绝境 (21926208 B): 256/535 zero - live verified (`camflag0=256`,
  `hit=139 len=121`, exit 0)
- 海岛绝境 (43575836 B): 25/245 zero
- 白龙绝境 (75888976 B): 394/822 zero
  All bins byte-match the previously shipped sizes; `.cflags` copied beside
  each bin; missing sidecar still means default-block.

Register/doc hygiene: D3 and B6 rows now read CLOSED with the engine-path
evidence; the stale `RC_CAM_LOOKPACK default ON` comment is corrected.

Still queued: P2 (structural flag-0 prop A/B - mechanism data-verified, spot
pending), P3 (delete lookpack/snapguard/B5/vertical-window after the broad
audit), P4 (broad audit + F9 captures at the spots where see-through remains).

### 2026-09-28 - D6 crash guard (engine null-deref trampoline)

- Reproduced D6 in normal play: 17:41:51 crash `KG3DEngineDX11EX64+0x11D03B6`
  while running/sprinting (no experiments), and again 1/3 in the stress campaign.
- Tested `SetSceneFullLoading(true)`: loading becomes observable
  (`progress=0.250`) but the crash still happened after it reached 1.000 -> not
  a fix; stays `RC_FULLLOAD=1` opt-in.
- Shipped `RC_PatchD6()` in `camera_shim.dll` (default on, `RC_PATCH_D6=0` kill
  switch): at `+0x11D03B6` the null case returns E_FAIL through the epilogue
  (`+0x11D0840`) instead of dereferencing null; the non-null path runs the
  original two instructions. 8-byte original-signature + PE-stamp guarded.
  Registered host bypass; the bail leaks that call's string locals.
- Evidence: `patchD6 rc=0 site=... byte0=E9`; 4/4 T4 demo runs and 3/3 55 s
  walking runs at the crash spot clean; T2 exact `hit=206 len=188`; smoke PASS.

### 2026-09-28 late - BEX64 regression + multi-instance guard

- The D6 trampoline was left default-on and a session with it produced a NEW
  failure mode: BEX64 jump-to-data (`fault offset 0x7FFE622B0000`, module
  unknown, P7 `PCH_84_FROM_unknown+0x0`), i.e. an indirect call landing on data.
  It never occurred before the patch; skipped cleanup/destructors in the bail
  path may corrupt state later.
- Root context: **three overlapping client instances** were running
  (logs 181949/182557/183000, 18:19-18:30) - concurrent clients share the
  engine/GPU and destabilize each other. The long single session (578 s) did
  not crash.
- Actions: `RC_PATCH_D6` default is now 0 (opt-in only, BEX64 caveat recorded);
  the client refuses a second instance (dialog; `RC_ALLOW_MULTI=1` overrides).
- D6 remains open; the only safe mitigation in the default build is the 1.5
  rad/s view-rate cap. Do not re-enable the trampoline without A/B evidence.

### 2026-09-29 C0 - freeze & baseline (camFP build)

Protection against concurrent workstreams:
- The exe is now **reborn_camfp.exe** (client/build_client.cmd); the fork's
  builds (reborn_client.exe, ability_sandbox.exe - rebuilt 19:24) can no longer
  overwrite it.
- Every run logs a first-line fingerprint:
  `build=reborn_camfp.exe <mtime> git=<hash> dirty=<n> camFP=True flags=(...)`.
  Camera logs are the **camFP=True** set; logs containing `feizhua` belong to
  the fork and are ignored.
- The single-instance guard blocks any of reborn_camfp / reborn_client /
  ability_sandbox / asset_sandbox (`RC_ALLOW_MULTI=1` overrides).
- Fixed a FormatException in the fingerprint (unescaped braces) that crashed
  the first four runs.

Baseline build: `reborn_camfp.exe` 2026-09-28 19:47:55, git 35dead9 dirty=18,
flags (ENGINESET=1, LOOKPACK=0, RATECAP=0, LOADPACE=1, FULLLOAD=0, PATCH_D6=0,
PITCH_ALIGN=1, PLAYER_HIDE=1, SNAPGUARD=0).

Baseline runs (RC_CAM_DEBUG=1, 12 s each unless noted; exit code 0, 0 crashes):

| Pose | camdbg | fps |
|---|---|---|
| spawn free-look (23334,761,24224) | hit=-1 len=599 | 281 |
| T2 wall (18097,500,22171) | hit=206 len=188 | 290 |
| T1 cavity (18985,682,24515) | hit=11 len=-7, vyaw = yaw + pi (look-at) | 153 |
| T4 deadwood (19183,864,31720) | hit=186 len=168 | 253 |
| walk route (DEMO_COLLIDE+TELEPORT, 62 s) | 123 camdbg samples, 0 view/aim mismatches | 275 |

Screenshots: `reborn_out\rc_0{0..3}_*.png` (4 from the walk) + per-pose sets.

### 2026-09-29 C1 - control feel A/B (camFP build)

Automated drag sweep (`RC_CAM_DEMO=1`, yaw 2-6 s, pitch 6-12 s) at spawn/T2/T1,
both modes, telemetry comparison:
- Unobstructed (spawn/T2): `vyaw == yaw` within 0.01 rad in BOTH modes; same
  yaw rate (~3 rad/s scripted). No measurable difference in tracking.
- T1 cavity: ES=1 shows `vyaw = yaw + pi` (engine look-at across the crossing,
  intended); ES=0 keeps `vyaw == yaw` (legacy managed path).

Conclusion: the scripted responses are identical, so the felt difference is not
input math. The engine path placed the offset **raw every frame**; the client
lost the game's per-axis dead-zone + SmoothTime (`SetCharacterCameraPosition`
@ 0x180B0F2BA.., spec CAMERA_FIX_SPEC.md, reference camera_model.py).

Landed (game rule, no tuning): per-axis `current += delta*dt/SmoothTime` with
dead-zone snap, SmoothTime from the character row (0.06 s, CommonNumber 60 ms),
honoring `bCameraSmoothing` from custom.dat. Applies in both modes (shared
placement). Invariants re-verified: T2 hit=206 len=188; T1 hit=11 len=-7 with
`vyaw = yaw + pi`; T4 hit=186 len=168; probe exit 0; smoke ALL PASS.

Pending: user feel judgment on this build (`reborn_camfp.exe`).

### 2026-09-29 C1b - crossing guard (nausea fix)

Reproduced the user's nausea with the shake detector (`RC_CAM_SHAKEDBG=1`):
T1 cavity + yaw sweep produced **5 signed-pull sign flips / 2 s** (`camLen`
0.2 / -11.3 / -15.8 / 69.6 ...). Each flip moves the camera through the anchor
and the engine look-at spins the view 180 deg - at 2-3 Hz that is nauseating.
Root: the host bake hit chatters in tight cavities (hit 117 / 11 / 22 / -1 as
the yaw sweeps), so `hit - 18` oscillates around 0.

Fix (registered host stabilizer, default on): `CameraObstruction.NoCross` floors
the pull at 0 - the camera reaches the anchor and never passes it; the view
never flips. Kill switch `RC_CAM_CROSS=1` restores the native crossing.
`camera_smoke` still passes (the class default keeps testing the native rule).

After: T1 sweep 0 flips, `len=0`, `vyaw` tracks the aim; T2 `hit=206 len=188`;
T4 `hit=186 len=168`; 62 s walk route 0 events, fps 273; exit 0.
Exit criterion: stable render-entity hit set (P4), then re-allow crossing.

### 2026-09-29 C1c - edge/teleport fixes (from the 204545 log review)

Reproduced with `RC_CAM_OBSTDBG=1` at T1 + yaw sweep: the degenerate near hits
come from the **raw scene backend** at the offset probe origins
(`probe1 ... bake=-1 terr=-1 scene=0.1`, `probe3 ... scene=2.3`), i.e. the D4
no-self-filter backend returning exit/grazing faces within 3 u of the origin.
Those won the min and moved the camera 10+ u in one frame (hit 11 -> hit 1 ->
len -17 in 204545).

Landed:
1. `RC_CAM_HITMIN` (default 3.0 u, 0 disables): probe hits closer than the
   threshold to the probe origin are ignored (registered B9). Sweep result: 21
   degenerate hits ignored, winning hits real (`bake=11`, `bake=70`), camLen
   steps 0.0 -> 20.5, 0 shake events.
2. `RC_CAM_WALLGATE` (default 0): B5 final-camera gate off by default
   (registered B10) - it was another jump source at edges.
3. B8 crossing guard + offset SmoothTime (previous cycles) remain.

Acceptance: T2 `hit=206 len=188`; T4 `hit=186 len=168`; T1 `hit=11 len=0` (no
crossing/flip); 62 s walk route 0 shake events @252 fps; smoke ALL PASS.
Exit: real FilterCamera/self-filter + footprint basis (P2/P4) then remove
B8/B9/B10.

### 2026-09-29 C1d - user-spot shake, root causes and shipped fix

User report: at (18755,657,24539), camera shaking at some angles after a while.

Measured with `RC_CAM_OBSTDBG=1 RC_CAM_SHAKEDBG=1` (jumpdbg = frame-to-frame
resolved-length jumps > 5 u):
1. Raw min-hit flicker from the idle head motion across bake triangle edges:
   probe3 bake 52 <-> 3 u -> pull 34 <-> 0 several times per second
   (dive/crawl cycle, 9-35 raw jumps per run).
2. Scene backend self-hits: the raw scene ray hit the player's own model 57 u
   behind the head -> pull 39 (unfiltered backend, D4).
3. Structure gate: T2's wall (inst 262) is flag=0, which the game handles by
   fading; the gate made the camera ignore a visible wall.

Shipped (all registered):
- hit stabilization B11 (min over 0.25 s, `RC_CAM_HITWINDOW`);
- scene near-hit floor B12 (80 u, `RC_CAM_SCENEMIN`), scene backend restored
  default-on;
- flag=0 structures block again B13 (gate only drops flag=0 foliage);
- resolved-offset smoothing (plan step 3): the post-pull offset follows the
  same per-axis dead-zone + SmoothTime (60 ms) as the orbit, so single-frame
  hit changes slide instead of teleport; dt clamped to 50 ms so streaming
  hitches cannot snap it.

Acceptance (shipped `reborn_camfp.exe`, fingerprint camFP=True):
- USERSPOT idle 14 s: 0 jumps, 0 shake, stable pull 155 (hit=173);
- T1 cavity: hit=11, len=0, 0 jumps;
- T2 wall (demo sweep): hit=287, len=189, 0 shake (raw follows smooth);
- T4 deadwood: hit=186, len=168, 0 jumps;
- 42 s demo route: 0 jumps, 0 shake, 0 aim mismatches @278 fps;
- camera_smoke ALL PASS (stabilizer kept out of the class contract).

### 2026-09-29 Step 1 - penetration recorder shipped (RC_CAM_PENDBG=1)

Implemented per the catch-classify plan (diagnostics only, default off):
- probe context ring: the last ~150 frames of per-probe hits
  (`p<n> off=(..) bake=<h>(i=<inst>,t=<tri>,blk=<cflags>,fol=<fromFoliage>) terr=.. scene=.. h=..`);
- per frame after Render: reverse cast actual camera (scene.GetCameraPos) ->
  placement anchor with four casts: bake gated, bake ungated, RayTerrain,
  RayScene; event = a hit strictly between the two (end margin 2 u, scene body
  margin 40 u for the player's own model);
- event line: map, player, yaw/pitch, actual vs intended camera, anchor, dcam,
  resolved len/hit/src, gated/ungated inst/tri/cflags/foliage, terrain, scene,
  rayPost, sceneLevel (hr,hit) + the ring dump; rate limit 1 event / 500 ms,
  `pendbg summary events=N` every 10 s.

Controls (test exe, T-spots):
- POSITIVE T2 with `RC_CAM_HITMIN=999` (pull disabled): 23 events / 2477
  event-frames, attribution `gated=381(i=656,t=664,blk=1,fol=0) ungated=381
  scene=384` - a real wall, flag=1, missed because the pull was off.
- NEGATIVE T2 default: 0 events, dcam=188 (camera in front of the wall),
  fps 247-296 with the recorder on.
- Note: T1's pull comes from a CORNER probe (bake 11.4 at off=(-22,0,0)); the
  center camera->anchor line is clear, so T1 is not a center-line penetration
  spot (the F9 shot agrees: wall on the near side).
- Recorder reverse casts use frontFacesOnly=false: the forward probe sees the
  front face, the reverse cast meets its back - front-only skipped every wall.
