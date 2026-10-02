# Experiences log

Append-only record of the development process: what we tried, what broke, how we
solved it, and what is still open. **Newest at the bottom.**

## How to use this file

- Append a **compact entry** after every work-bearing response/session (investigation,
  code change, decision). Pure Q&A does not need one.
- Use the **full template** for notable lessons, dead ends, non-obvious fixes, or
  locked decisions.
- Never edit or delete past entries; a correction is a new entry.
- Every entry links its evidence (paths, commands, commits) and carries a confidence
  when the outcome is uncertain.
- A dead end must include date, scope, and re-open criteria — never "we tried stuff".

### Compact entry

```markdown
### YYYY-MM-DD — <area> — <title>
- Did: <what changed or was investigated>
- Evidence: <files, commands, commit hashes>
- Outcome: solved | partial | dead end
- Re-open: <only when not solved>
```

### Full template

```markdown
### YYYY-MM-DD — <area> — <title>
**Problem:** <what we were trying to do and why it was hard>
**Tried:** <what was attempted, including dead ends>
**Outcome:** solved | partial | dead end
**Why:** <root cause / what actually worked, with evidence>
**Re-open criteria:** <what would justify trying again>
**Links:** <docs, proof artifacts, commits>
```

## Legacy experience records

- `engine_host_spike/EXPERIENCE_MAP_SPIKE.md` — Spike B: hosting the MovieEditor map
  display (2026-09-21). Lessons still valid: IL recon first, adopt the editor's exact
  order/values, numeric fingerprints for visual checks, engine-run costs.
- `_port_from_mapviewer/EXPERIENCES.md` — removed in cleanup (commit `f92139d`).
  Recoverable from git history if needed; its theory is banned anyway (AGENTS.md §7).

## Entries

### 2026-09-29 — repo — Agent rules + experience log setup
- Did: wrote root `AGENTS.md` (merged with the existing `#iso` workflow) covering
  lookup protocol, `interface\` rule, no-invented-fixes, map-viewer disposition,
  locked constraints, legacy table, area map, stack, gates, and hard rules; created
  this experience log; appended recall pointers in `SFX_GROUND_RULES.md`; rewrote
  `README.md` as the human entry point; added nested `AGENTS.md` files under
  `engine_host_spike/`, `client/`, `tools/netcode/`, `ui-process-app/`, `native/`.
- Evidence: `AGENTS.md`, this file, commits on `cleanup/repo-tidy`.
- Outcome: solved

### 2026-09-29 — repo — Consolidation + cleanup (merge, naming, launcher, Desktop)
- Did: merged all 8 branches into `main` (union-resolved the anim-picker/ability-sandbox
  split; pushed `30d9178`). On `cleanup/repo-tidy`: removed the web viewer and probes
  (`map-ui-explorer`, `web/`, Playwright/three.js leftovers), `_port_from_mapviewer`/
  `_ref_chrome`, unused `ability_sandbox/cam`, legacy `engine_host_spike` hosts and the
  actor-map spike; retired `map-ui-app` (text assets → `ui-process-app/Data/text`);
  renamed `reborn_camfp.exe` → `reborn_client.exe` and rebuilt; repurposed `app/`
  launcher to start `reborn_client.exe` (+`RC_MAP` picker) and fixed its Desktop
  shortcut; removed dead JX3 Ani Player leftovers (`player.py` gone since `17385ae`);
  untracked generated per-map structure bins; dropped the redundant 90 MB samples zip;
  consolidated the Desktop to one folder (`_backup/` moved inside, ignored); untracked
  local runtime state (`perf_config.ini`, `launch_*.txt`) per AGENTS.md §9.
- Evidence: commits `30d9178`, `4816d46`..`12d1a81`; `git status` clean; rebuilt
  `bin64\reborn_client.exe` (build_info git id); `_backup\reborn-all-refs.bundle`.
- Outcome: solved. `cleanup/repo-tidy` is ahead of `main`, not merged yet.
- Re-open: merge `cleanup/repo-tidy` → `main` and push; delete `_backup/` after.

### 2026-09-29 — docs — Note inventory + netcode index registration
- Did: inventoried tracked notes (**143 `.md`**: `docs/` 69, `proof/` 30, root 28
  (24 player-era), `engine_host_spike/` 5, tools/apps 10); audited the area indexes
  (`docs/controls` 9/9, `docs/pvp` 2/2, `docs/netcode` 17/21) and registered the four
  missing 绝境 mode notes (`JX3_MODE_GAP_REGISTER`, `JX3_MODE_JUEJING`,
  `JX3_MODE_JUEJING_LOGIC`, `JX3_MODE_LOAD_FLOW`) in `docs/netcode/README.md` (§13).
- Evidence: `git ls-files "*.md"`; index audit command; this commit.
- Outcome: solved. `docs/netcode` is now 21/21 registered.

### 2026-09-29 — docs — Reorganize docs into area folders + indexes
- Did: moved the 32 remaining `docs/` root notes into `docs/camera/` (18, `CAMERA_`
  prefix dropped), `docs/movement/` (7), `docs/engine_host/` (3), `docs/ui/` (2),
  `docs/audio/` (1) and `docs/controls/` (+`PLAYER_CONTROLS_FINDINGS.md`); swept all
  old-path references (50+52 files, incl. backslash variants and the `docs/CAMERA_*`
  glob); generated area `README.md` indexes from each doc's H1 (camera 18/18,
  movement 7/7, engine_host 3/3, ui 2/2, audio 1/1; netcode 21/21, controls 10/10,
  pvp 2/2); added `docs/README.md` master index; updated `AGENTS.md` §3/§10 paths.
- Evidence: stale-path sweep → 0; registration audit output; this commit.
- Outcome: solved. `docs/` root is exactly `README.md`,
  `GAME_SYSTEMS_RESEARCH_MAP.md`, `EXPERIENCES.md`.

### 2026-09-29 — repo — Finalize: merge the cleanup branch into main
- Did: merged `cleanup/repo-tidy` into `main` (`085daa7`, 21 commits) and pushed.
  The branch carried: repo consolidation (web/legacy removal, union anim-picker
  merge), client naming (`reborn_client.exe`), launcher repurpose, Desktop
  consolidation, agent-rules alignment, note inventory, and the docs area
  reorganization.
- Evidence: merge commit `085daa7`; `origin/main` updated in the same session.
- Outcome: solved.

### 2026-09-29 — client — Parallel feature clients: per-build engine namespace
- Did: replaced the name-list single-instance guard with namespace-based isolation.
  Feature builds derive `reborn_client_<slug>.memory` from the exe name (canonical
  keeps `MovieEditor.memory`; `RC_MEM_NS` overrides); the guard blocks only namespace
  peers — the canonical build excludes `asset_sandbox`/`ability_picker` (they still
  hardcode the shared namespace), a feature build excludes only a second instance of
  itself. `client\build_client.cmd` accepts `RC_CLIENT_EXE`/`RC_SMOKE_EXE` and, for
  feature builds, skips the shared bin64 config copies and writes
  `build_info_<exe>.txt`. Rule added: `AGENTS.md` §2 "Parallel feature work on the
  client (isolated builds)" + `client/AGENTS.md`.
- Evidence: live 2-client run 12:27 — `reborn_client_alpha.exe` + `reborn_client_beta.exe`
  concurrently; logs show `ns=reborn_client_alpha.memory` / `ns=reborn_client_beta.memory`,
  both loaded terrain, no crash; `build exit=0` for all three builds.
- Outcome: solved. Shared engine-root writes (ShaderListUpload/dxvk) remain the known
  caveat; root isolation broke init (`d8268d2`).

### 2026-09-29 — client — Collision improvement pass (holes, capsule, slope, substeps)
- Did: gap audit + fixes on the main client (`docs/movement/CLIENT_COLLISION_IMPROVEMENT_PLAN.md`).
  (1) Terrain holes: `TerrainSampler` now calls the loader's `LoadHoleRegion` (vt[4]) and
  exposes `SampleGround` (no ground over a hole); the client falls through caves instead of
  standing on them. (2) `FoliageCollision.InstanceContact` replaced 6-point axis sampling
  with exact segment/triangle closest pairs (a thin rail at 15 u between former samples now
  blocks). (3) Slope rule uses a fixed 40 u look-ahead (speed/framerate independent).
  (4) Long horizontal moves are substepped (≤20 u) so they cannot tunnel thin colliders.
  (5) Offline gate `collision_selftest.exe` (9 checks) wired into `client\build_client.cmd`.
- Evidence: selftest 9/9 PASS; engine A/B on 海岛绝境 region 0,0 — engine mask == decoded
  `.hlb` after the **row-flip in Z** (`tools/collision/check_hole_mask.py`); live fall
  (`py=-4434`, `vy=-4952`, `grounded=False` at t=2 s); 龙门寻宝 cactus regression blocked at
  z=53288 (264 events). Proof: `proof/collision/client_holes/`. Commits `4d46810`, `ec72490`,
  `a7b653c`, `e59234b`, `9ca8549`, `7334772`, `c6d0af1` (push blocked: credential manager
  hung; local only at session end).
- Outcome: solved (C-1..C-5). Known limitation: no cave meshes under holes → a fall is
  bottomless until geometry is baked beneath (no invented floor).
- Re-open: cave-bake availability; engine A/B for holes beyond region 0,0 once other maps
  with holes are baked.

### 2026-09-29 — client — Building door block + inside stutter (camera rays, not collision)
- Did: reproduced the user's report from their run log (57 blocked events at
  (18758,652,24591), FPS 268→130 sustained at the wall). The blocker is the
  visual mesh `jz_xb玉门关建筑001_004_hd.mesh` (34,786 tris) — closed gates block
  per the engine's own `.mesh` rule, and the jump entry exploits the mesh not
  being a solid volume, so both are engine-faithful. The stutter is the camera
  obstruction block: `RC_COL_PROF` split `colms=0.19-0.44` vs `camms=3.16`
  (`nat=1.19` + `vert=1.90` native rays; up to 10.8 ms stalls). Fixed with a
  20 Hz cap on the camera query set (`RC_CAM_OBSTHZ`, 0 = old every-frame) and
  skipping the host vertical ladder when the horizontal probes hit.
- Evidence: user log `reborn_20260929_141108.log`; before/after runs
  `145720` (122 fps) vs `150433` (228-243 fps) at the same spot; camera still
  pulls at the gate (`rc_00_7500ms.png`); plan doc §7. Commits `6a4d8b3`,
  `5ff8705`.
- Outcome: solved (stutter). Blocking is engine-faithful, not a defect.
- Re-open: engine camera query cadence / a cheaper native ray path (the 20 Hz
  cap is a provisional host policy).

### 2026-09-29 — movement — Step forgiveness research (no invented threshold)
- Did: researched the "low objects don't block" feel. Recovered the shipped engine's
  PhysX capsule-controller defaults from `PhysicsEngineX64.dll` (`PxControllerDesc`
  ctor RVA `0x18000e910`): `stepOffset=0.5` m, `slopeLimit=0.7071` (45°),
  `contactOffset=0.1`, in the metric scene (gravity −9.81). PhysX CCT semantics climb
  obstacles up to the step offset and block above. Found no gameplay step constant in
  configs/tables/strings; `fPathingHeight` (unit template) is the only height-like key
  left and has no recovered consumer; movement is server-authoritative and nav data
  lives outside the client install.
- Evidence: `proof/collision/disasm/pxcontrollerdesc_ctor.txt`,
  `docs/movement/JX3_STEP_FORGIVENESS_RESEARCH.md`; G-13 updated.
- Outcome: partial (engine defaults HIGH; gameplay applicability unproven). No host
  threshold changed - deciding between the recovered 50 u default, the calibrated
  70 u rule, and a live-measured value is pending a choice.
- Re-open: a live observation of a walked-over object's height, or decoder work on
  `KCharacter::AdjustPosZ` / server nav semantics.

### 2026-09-29 — movement — Adopt recovered engine step budget (0.5 m)
- Did: applied the real engine value to the host per user decision: object step budget
  50 u (recovered PxControllerDesc default 0.5 m, metric scene), env `RC_STEP_HEIGHT`;
  `FoliageCollision.Resolve`/`MoveResolved` take the budget as a parameter. Terrain
  slope rule left calibrated (different subsystem: ProcessDropSpeed, not CCT).
- Evidence: `docs/movement/JX3_STEP_FORGIVENESS_RESEARCH.md` §8; selftest 11/11
  (`step_up_50u_budget`, `step_blocks_over_budget`).
- Outcome: solved (adopted).
- Re-open: a live-measured gameplay step height or server/nav decoding could replace
  the CCT default with the online value.
### 2026-09-29 — client — Object collidability vs live game (玉门关 building)
- Did: user reports walking through the 玉门关 wall in the live game while the host
  blocks it (instance 897 = `jz_xb玉门关建筑001_004_hd.mesh`). Checked the bake rule:
  H1 admits the model (not file_black; folder `maps_source` white) and H1 is still
  A/B-pending (G-35). Engine `.mesh` rule collides visual triangles; live collidability
  is server/streamed state absent from the client install. Units/step are not the
  issue. Added debug controls `RC_COL_OFF` and `RC_COL_SKIP_BOX=x0,z0,x1,z1` (HUD
  `[COL OFF]`), launched the client with the building box skipped so the user can
  verify the rest of the world.
- Evidence: `docs/movement/CLIENT_COLLISION_IMPROVEMENT_PLAN.md` §8;
  `proof/collision/physic/audit_lists.txt`; EngineStaticConfig.ini `[KG3DENGINE]`.
- Outcome: partial (control shipped; rule open). Next: settle whether the live game
  blocks any part of that building (whole-object vs doorway vs height) before changing
  the bake rule; H1 A/B is the candidate fix.
- Re-open: user observation or server/nav data.
### 2026-09-29 — client — Root cause: physic lists are GB18030, read as UTF-8
- Did: the "carpet/props block walking" reports traced to `_read_list` in
  `tools/export_structure_collision.py` decoding the engine's `Represent/physic/*`
  lists as UTF-8 with errors=replace. Every Chinese stem became mojibake, so the
  black-list filter silently skipped them (8 rejects instead of 60). Decode u8-sig ->
  GB18030; re-baked 龙门寻宝 (60 rejects: 29 file_black + 23 folder_black + 8
  no_whitelist; instances 4,949 -> 4,897). Removed the RC_COL_OFF/RC_COL_SKIP_BOX
  band-aids entirely.
- Evidence: `docs/movement/CLIENT_COLLISION_IMPROVEMENT_PLAN.md` §8.2;
  `wj_dcy地毯001_001_hd` present in `proof/collision/physic/physic_file_black.txt`
  (GBK); bake log with 60 rejects; cactus regression still blocks (z=53288).
- Outcome: solved (list filter now real). Gate passability remains server/doodad
  state; other four maps need the same re-bake.
- Re-open: none for the encoding bug; door state needs server data.
### 2026-09-29 — movement — Local prediction ground rules (engine) replace host rise/ledge rules
- Did: decoded `KCharacter::ProcessVerticalMove` (y = min(y, ground), 64 u landing
  tolerance) and `ProcessDropSpeed` (slope projection/air-stop); replaced the host's
  70/50 u rise-budget and 150 u ledge rule with the engine behavior (snap up, snap
  down within 64, else fall). Named-blocker logging added earlier identifies the
  merged interior mesh (`jz_xb玉门关建筑001_003sw_hd`) as the carpet/furniture
  blocker; per-part blocking inside merged meshes is server state.
- Evidence: `docs/movement/CLIENT_COLLISION_IMPROVEMENT_PLAN.md` §8.3;
  `docs/movement/JX3_GRAVITY_RESEARCH.md` §3.2; disasm transcripts; selftest 11/11.
- Outcome: solved for terrain ground forgiveness; structure blocking remains the
  host's server proxy.
- Re-open: server-side obstacle data (client files cannot express doors/carpets).
### 2026-09-29 — movement — CCT top rule: low obstacles never block
- Did: the remaining "wood on the ground / carpet blocks" cases are merged-mesh
  features with no up-facing support face (verified: at the gate contact the mesh
  has 0 up-facing and 10 down-facing local triangles). Implemented the engine's
  CCT rule directly: when a horizontal contact occurs, measure the obstacle top
  from the geometry at the contact point (`LocalTop`, world-XZ-tight probe) and
  climb (no block) whenever top <= feet + stepOffset; block only above.
- Evidence: `client/FoliageCollision.cs` (LocalTop + Resolve), selftest 14/14
  (`thin_low_plate_passes`, `thin_tall_plate_blocks`, `step_onto_low_edge`);
  commit to follow; docs/movement/CLIENT_COLLISION_IMPROVEMENT_PLAN.md §8.3.
- Outcome: solved for low geometry; tall walls/gates still block.
- Re-open: live spot with a still-blocking low object - the named blocker log
  gives the model, then the local contact triangle can be inspected.
### 2026-09-29 — movement — Low touching face wins the CCT step test (plank at wall passes)
- Did: at the gate the deepest contact was a tall face (top 860.8 vs feet 636.9) while a low
  face also touched the capsule, so the step test blocked. `InstanceContact` now tracks
  `lowTop` (lowest top among all horizontal faces touching the capsule) and `Resolve` climbs
  when `lowTop` is within the step budget. Live test at the gate: the first low contact now
  passes (blocked=False, player climbed and continued); the next block is a genuine 1.03 m
  face (top 741.7, feet 638) beyond the recovered 0.5 m step budget.
- Evidence: `client/FoliageCollision.cs`; selftest 15/15 (`low_plank_at_wall_passes`);
  run log `reborn_20260929_175736.log`.
- Outcome: solved for low geometry at wall bases; >0.5 m still blocks (engine default).
### 2026-09-29 — movement — Faces at/below the feet must never block (user-log case)
- Did: the user's copied log showed the stuck contact as
  `blocked by inst=487 mesh=364 top=929.9 feet=933.9 ...001_003sw_hd.mesh` - the
  contacting face's top is BELOW the player's feet, yet the resolve pushed out.
  `Resolve` now treats any horizontal contact whose triangle top is `<= feet` as a
  non-obstacle (no push-out, keep moving), in addition to the existing within-budget
  climb. Verified in-engine at the same interior spot: 0 blocked events while
  running through (was 400+ and stuck).
- Evidence: `client/FoliageCollision.cs`; selftest 16/16 (`face_below_feet_passes`);
  run `reborn_20260929_181709.log`.
- Outcome: solved.
### 2026-09-29 — movement — Capsule bottom artifact: feet are the capsule bottom
- Did: the "below feet skip" hack caused wall penetration and carpets still blocking.
  Root cause: `InstanceContact` used the axis py..py+height, extending the capsule a
  full radius BELOW the feet, so floor edges/thresholds/carpet borders touched it and
  were treated as obstacles. Fixed the capsule to the engine convention: axis from
  `py + radius` to `py + height - radius` (bottom exactly at the feet), and removed
  the skip hack; the CCT top rule remains (below feet cannot touch anymore).
- Evidence: `client/FoliageCollision.cs`; selftest 16/16; interior run
  `reborn_20260929_183202.log` (0 blocked, was 400+); gate run
  `reborn_20260929_183257.log` (blocked by top=860.8 vs feet=701, a real >0.5 m wall).
- Outcome: solved; this makes the host capsule match the engine controller convention.
### 2026-09-29 — movement — Full collision status audit
- Did: rechecked the collision docs against the branch and wrote
  `docs/movement/COLLISION_SYSTEM_STATUS.md` (per-subsystem implemented/missing/wrong).
  Top issues: terrain slope over-permissive (no ProcessDropSpeed projection);
  15 Hz integer movement not ported (G-14); H1 list rule A/B pending (G-35);
  host-generated tree canopy columns; 20 Hz camera query cap; bottomless holes;
  host-chosen capsule/step values; merged interiors remain server state.
- Evidence: the audit doc; this branch's commits and run logs.
- Outcome: documented; implementation priorities listed in the doc §8.
### 2026-09-29 — camera — Re-enable hit stabilizer (door-crossing shake)
- Did: `RC_CAM_HITWIN` defaulted to "0" (stabilizer disabled) although the class
  documents 0.25 s as the registered B11 default; raw min-hit flicker at triangle
  edges made the camera jump while crossing a doorway. Default restored to 0.25 s.
- Carpet check: `wj_erg地毯001_hd` at (19690,920,36271) crossed with 0 blocked
  events; the next block 340 u later is a real 1.05 m wall (top 1007.8, feet 903).
- Evidence: `client/RebornClient.cs`; run `reborn_20260929_203756.log`.
- Outcome: solved.
### 2026-09-29 — movement — Step budget 64 u (1 尺 ground tolerance), field case
- Did: the user's run showed the stuck spot (19756,971,...) bouncing at the house floor
  edge: floor ~51 u above the outside ground, 1 u over the 50 u CCT default. The
  engine's ground/landing tolerance constant is 64 u = 1 尺 (`ProcessVerticalMove`
  0x14031A25E), so the step budget is now 64 u (`RC_STEP_HEIGHT` override). New
  selftest `step_51u_with_64_budget` (17/17).
- Evidence: run `reborn_20260929_203902.log` (vy/fall-clip loop at the floor edge);
  `client/RebornClient.cs`.
- Outcome: solved (field case covered by an engine constant).
### 2026-09-29 — movement — Steps only while grounded; camera stabilizer 0.4 s
- Did: the CCT climb applied while airborne, so a jump let the player climb/penetrate
  walls whose top was below the jump height. `Resolve` now climbs only when
  `grounded`; airborne moves push out. Camera hit stabilizer window 0.25 -> 0.4 s
  (20 Hz query sampling needs the longer hold to stop doorway shake).
- Evidence: selftest 17/17 (step tests now start grounded); jump-against-gate run
  `reborn_20260929_204919.log` (x stays 18768, blocked, no penetration).
- Outcome: solved.
### 2026-09-29 — movement — Floor query ignores triangle winding (house rug case)
- Did: the house "carpet" (`wj_erg地毯001_hd`, 龙门寻宝 inst 485 / mesh 75) is an
  ~800×800 u plate at y 921.8–924.2 authored with **inverted winding** (both
  triangles ny=−1.00; verified offline from the baked bin). `SupportHeight`
  (the floor query) required up-facing normals, so the rug was never ground:
  the capsule surface contact pushed the player up, the 64 u drop-tolerance
  snapped him back — py oscillated 921↔924 every frame, `hits=0`, camera bob.
  Fix: `Math.Abs(wny)/nl >= 0.5` in the floor query (winding is a render
  property; the engine floor test is orientation-agnostic).
- Verified: selftest 19/19 (`inverted_floor_support`, `inverted_floor_stand`);
  in-game by the agent at the user's stuck coordinate: `reborn_20260929_212757.log`
  (spawn 19295,887,36435 → t=2s pos (19935,**924**,36435) grounded `hits=0`
  `colCalls=0`) and `reborn_20260929_212423.log` (spawn 19240,886,36435).
  The next blocker east is inst 773, the same mesh placed vertically (top
  1115.9, feet 924.6) — a real obstacle.
- Outcome: solved.
### 2026-09-29 — camera — Anchor-Y smooth-follow (step-snap shake, B14)
- Did: the user's repeated walking in the house area produced 42–64 u camera
  jumps in single frames: the camera anchor is raw physics `py + 90`, so the
  grounded step snap (rug 924 ↔ floor 969, stairs) teleported the camera, plus
  a ~10 Hz +7/+10 u stair train. Added an anchor-Y smooth-follow: one-frame
  grounded delta > 5 u starts an exponential follow (SmoothTime = camera-row
  0.06 s = `CharacterCameraSmoothTime`) with a 5 ms frame clamp and a catch-up
  rate cap (`RC_CAM_YRATE`, 1200 u/s); slopes and airborne frames stay raw.
  Kill switch `RC_CAM_YFOLLOW=0`; debug `RC_CAM_YDBG=1` (rawstep vs smoothed).
- Verified: A/B on the field route (spawn 19600,36000, walk north): raw max
  grounded camera-Y step 63.9 u → shipped 6.0 u (median 0.6, p90 1.5); stair
  train 7–10 u → 0.1–0.8 u/frame. Logs `reborn_20260929_215235.log` (raw) /
  `..._220246.log` (fix); selftest 19/19; registered as B14 in
  `docs/camera/HOST_DEVIATIONS.md`.
- Outcome: solved (host stabilizer; re-open with the represent-layer
  interpolation / `DynamicFollowSmoothObjectPosition` port).
### 2026-09-29 — collision — Tagged the door/carpet fix
- Did: the user asked to record the rug fix commit as the door/carpet fix:
  annotated tag `door-carpet-fix` → `62f796a` (floor query ignores winding).
- Evidence: `git show door-carpet-fix`.
- Outcome: recorded.
### 2026-09-29 — camera — Tagged the step-shake build
- Did: annotated tag `camera-step-fix` → `6b03864` (anchor-Y smooth-follow B14).
- Outcome: recorded.
### 2026-09-29 — collision — "walked into the cabinet": engine step rule, not a bug
- Did: the user's end-of-run log showed them inside the open-front cabinet
  `wj_erg柜子002_hd` (龙门寻宝 idx 648, AABB 18728-19351/922-1329/36721-36821)
  at feet 974-1044, asking whether the step forgiveness let them in.
  Reproduced with a grounded walk (spawn 19040,36650, walk north): the cabinet
  front BLOCKS at z=36714 (`blocked by inst=1042 top=1017.2 feet=928`), so a
  plain walk-in is not possible. Route from the log + geometry: a 51 u ledge
  south of the cabinet (within the 64 u = 1 尺 tolerance) put the feet at ~974;
  the cabinet's closed-door front top (1007.8) was then only ~34 u above the
  feet, inside the engine's own `stepOffset` (0.5 m = 50 u), so the step rule
  climbed over the doors; the following 小跳 landed on the interior shelf
  (1043.7 / 1052). Same outcome would occur under the real PhysX CCT for the
  same collidable mesh.
- Open question (not invented): whether the real game collides with this prop
  at all — the bake admits it under the unresolved folder-white H1 rule
  (`G-35`); real per-object physics is server/nav state we cannot derive from
  the client files. Indoor props' interiors are therefore **[PART/H1]**.
- Evidence: repro run `reborn_20260929_222021.log`; geometry scan of the
  cabinet mesh (front panels y 922-1008 + sides; open band y 1008-1224).
- Outcome: explained + documented; no host rule added (engine-faithful).
### 2026-09-29 — collision — Prop shells: ejection experiment reverted (inverted winding)
- Did: the user went fully inside the open-front cabinet (latest position
  (19168,921,36779), inside its footprint) and asked for a fix. Diagnostics:
  the prop mesh is a hollow shell (front panels y 922-1008, open band
  y 1008-1224, no bottom face, interior shelves at 1017/1076/1085), admitted
  by the unresolved H1 folder-white rule (top folder `maps_source`, G-35).
  A winding-based "stay on the visible side" ejection was implemented and
  benched: it never fired on this data because the game's meshes are authored
  with **inverted winding** (the rug's triangles ny=-1; the cabinet's front
  panel normal points +z INTO the cabinet) - winding carries no reliable
  visible side. An earlier variant also oscillated between the cabinet and the
  house shell (exit into a wall). Live game keeps characters out of props via
  server/nav movement authority, which is not in the client install.
- Evidence: offline geometry scans; runs `reborn_20260929_222232.log` (user
  inside), `..._223458/224136/224248.log` (ejection experiment: oscillation /
  no fire). Reverted; selftest 19/19; build clean.
- Outcome: documented; no host rule shipped. Options (solid-prop volume
  approximation vs server/nav research) in `COLLISION_SYSTEM_STATUS.md` §8
  item 11.
### 2026-09-29 — collision — Solid volumetric props (cabinet entry fixed, host proxy)
- Did: recovered the shipped per-mesh obstacle flags — every mesh `.mesh.ini`
  carries `bAutoProduceObstacle` (`[Display]`) and `bLogicObstacle` /
  `bCollisionOnly` per LOD submesh; consumers in `KG3DEngineDX11EX64.dll` /
  `KG3D_LoaderNoRenderX64.dll`. Our bake used only `bObscatleCamera` and
  ignored them. The per-unit passability values (`bUnitWalkable` /
  `bUnitCanPass`) are **not** in the shipped files (G-21).
  Implemented the host proxy: volumetric furniture (柜/箱/桌/桶/缸/坛 mesh
  classes) is solid — when the capsule overlaps the prop's world AABB it is
  pushed out along the minimum-translation axis (a wall-like contact with
  2–10 u per-frame pushes), so it cannot be entered and there is no ejection
  bounce/shake. Near-geometry gate (80 u) keeps the empty AABB air of thin
  sheets (rugs/banners/bones) non-solid; buildings keep mesh-shell collision.
- Verified: selftest 22/22 (`prop_solid_eject`, `prop_solid_building_kept`,
  `prop_solid_free_exit`); in-game pressing into the cabinet front
  (`reborn_20260929_234229.log`): held at z=36704 (face − radius) with
  2–9 u per-frame pushes — wall-like, no bounce; running into its east side
  (`..._234128.log`) the same. Earlier ejection build teleported 19–27 u per
  frame and made the camera shake (user report 23:38), which is why the
  contact-push form replaced it.
- Outcome: solved as a registered host proxy; re-open with the real
  `bUnitWalkable` data (G-21) or the server/nav obstacle stream.
### 2026-09-29 — docs — Full host-vs-game collision comparison + solid-prop deviation recorded
- Did: created `docs/movement/COLLISION_SYSTEM_COMPARISON.md`: the game's 16
  collision domains vs the host, a per-object/per-unit flag inventory
  (`bAutoProduceObstacle`/`bLogicObstacle`/`bCollisionOnly` ship in every
  `.mesh.ini` with located consumers; `bUnitWalkable`/`bUnitCanPass` values are
  NOT in the shipped client files, G-21), data-have / data-do-not-have lists,
  the `G-0..G-35` rollup mapped to host status, and the registered host
  deviations including the solid-prop proxy with its exact "partly" scope
  (name taxonomy + AABB shape + missing unit values). Registered in
  `docs/movement/README.md`.
- Evidence: the doc and the sources cited inside it.
- Outcome: recorded; no code change.

### 2026-09-30 — collision — P0: shipped obstacle flags wired (`bAutoProduceObstacle`)
- Did: `tools/export_obstacle_flags.py` fetches each baked mesh's sibling
  `.mesh.ini` and writes `<bin>.oflags` (bit0 `bAutoProduceObstacle`, bit1/2
  any/all LOD0 `bLogicObstacle`, bit3 `bCollisionOnly`, bit4 missing-ini);
  `FoliageCollision` skips instances whose mesh has auto=0
  (`RC_OBST_FLAGS=0` disables). On 龙门寻宝 17 meshes (flags, lanterns, drying
  racks, straw mats, farm props, one hut) carry auto=0 and **none** has an
  authored `CollisionMesh` sibling — the shipped data says the engine does not
  auto-produce an obstacle for them.
- Verified: selftest 24/24 (`obstacle_flag_off_walkthrough`,
  `obstacle_flag_on_blocks`); in-game `FoliageCollision: instances=5235 ...
  noObstacle=56` (was 5291), FPS 216-267 (`reborn_20260930_135518.log`).
- Outcome: P0 done for the loaded map; other 4 baked maps need their
  `.meshes.txt` sidecars (re-run `export_structure_collision.py`) before the
  same oflags export.

### 2026-09-30 — collision — P1 probe (negative) + P2 sizing (integer model)
- P1: `KG3D_LoaderNoRenderX64.dll` is **not** the auto-obstacle producer - no
  client module references it, its exports are `Get/Init/UninitLoaderNoRender`
  plus SpeedTree, and it imports no PhysX/cooking APIs; it merely parses the
  same ini keys while loading meshes offline. The producer is the client
  `PhysicsEngineX64` actor-from-mesh path reading the `KG3DMesh` struct fields
  (+0x194 = `bAutoProduceObstacle`, filled by `KG3DMeshFileDataLoader`); next
  probe = locate `_CreatePxActorFromMeshFile` there and trace the field reads.
- P2: `KCharacter::ProcessDropSpeed` (`0x140316BE0`) is Q5/Q8 fixed-point over
  two 3-bit terrain-cell slope fields (`(cell>>1)&7`, `(cell>>4)&7`; slope 0-1
  skips the projection path). A faithful port needs the full integer model
  (G-14), not a scalar patch; sized as its own work item.
- Evidence: loader exports/imports dump; `proof/gravity/disasm/process_drop_speed.txt`.
- Outcome: plan rows P1/P2 updated in `COLLISION_SYSTEM_COMPARISON.md` §8.2.

### 2026-09-30 — collision — P1 resolved (negative): no generated obstacle shape client-side
- Evidence: client+MovieEditor `PhysicsEngineX64.dll` import the same PhysX surface
  (`PxCreateCooking`, box/sphere/capsule/convex/triangle/heightfield geometry
  registrations, `PxCreateControllerManager`); its symbol blob names
  `_GetCollisionGeometryFilesFromGroupFile` (`.CollisionMesh`/`.proxymesh`/`.mesh`),
  `_CreateCollisionDataFromFile`, `_CreatePxActorFromMeshFile` (isolated literals,
  no code xrefs; scanner validated on `KGLOG_PROCESS_ERROR`). The baked props
  have no sibling collision file -> fallback to the render `.mesh` cooked as a
  triangle mesh. Conclusion: our baked triangle geometry equals the game's
  obstacle geometry; statics have no AABB/generated-box path; prop-interior
  solidity is server/nav and the AABB push remains a labeled proxy.
- Next: P2 task breakdown (T1 15 Hz tick, T2 slope fixed-point, T3 render glue)
  added to `COLLISION_SYSTEM_COMPARISON.md` §8.3.
- Outcome: P1 closed; plan continues at P2.

### 2026-09-30 — movement — P2-T1/T3: fixed 15 Hz logic tick + render interpolation
- Did: the movement/collision/ground/jump/gravity block now runs only in whole
  1/15 s ticks (accumulator, hitch cap 4; `pdt` = 1/15), positions are quantized
  to integer cm after every tick (engine integer model), and the model
  placement + camera anchor use the interpolated render position (`rpx/rpy/rpz`,
  alpha = remaining tick fraction) so 15 Hz does not stutter at 200+ fps.
  Constants already match the integer model exactly (jump 1350 u/s = 90 u/f,
  gravity -2475 u/s2 = -11 u/f2).
- Verified: build ok, selftest 24/24; in-game demo `reborn_20260930_143156.log`:
  integer positions (23334,719,24875...), jump air ~1.0 s (小跳b/c clips),
  FPS 235-306, ground/walk/run unchanged.
- Remaining P2: T2 slope/air-stop fixed-point port (`ProcessDropSpeed`, Q12,
  64-entry trig); T4 walk/run/sprint per-frame integer alignment (96/320/563
  u/s are not the table's u/f values).

### 2026-09-30 — movement — P2: JumpParam confirms 15 Hz; integer per-tick step
- Evidence: `proof/gravity/JumpParam.tab` school 0 jump 0 = (XY 40, Z 90,
  G 11) integers -> 90 u/f * 15 = 1350 u/s and 11 u/f2 * 225 = 2475 u/s2,
  exactly the client's jump constants: the integer model runs at 15 Hz. The
  run UI value (5 尺/s at 1 尺 = 64 u = 320 u/s) is 4.69 rounded at 20 u/f
  (300 u/s). The locomotion table is not in the extracted set (`Sprint.tab`
  is dive caps 10..120 / 8..900 u/f) - T4 = extract the walk/run table.
- Did: per-tick displacement is now integral (`step = round(sp * pdt)`), so
  each logic frame moves an integer number of units like the engine.
- Verified: build ok, selftest 24/24; run `reborn_20260930_144921.log`:
  integer positions, rug crossing normal, hits=0.

### 2026-09-30 — movement — T2 mapped: ProcessDropSpeed is a steep-slope slide, not a climb limit
- Read the full function (`0x140316BE0..0x1403171FD`): Vz-gated; terrain cell slope
  fields `(cell>>1)&7` / `(cell>>4)&7`; `slope2<=1` (or states 0x1A..0x1D,
  `[+0xC20]` 0x1A/0x1B) -> `Vz=0`; steeper -> Q12 trig (64-entry table
  `0x14020EE10`, angle `slope2*8`) projects the velocity onto the slope, `sqrtf`
  magnitude vs per-state threshold; below threshold -> `Vz=0`; else clamp
  `Vxy_fixed` 0..0x7FF / `Vz` -0x800..0x7FF and switch move state.
- Conclusion: this is the airborne slide model on steep terrain; the walking
  path (WalkTo/RunTo) has no client-side slope limit (server validates). T2 is
  therefore re-scoped: needs packed-cell exposure in TerrainSampler + the trig
  table dump + per-state thresholds; deprioritized behind P3/P4 unless slides
  are needed.
- Evidence: `proof/gravity/disasm/process_drop_speed.txt`; plan doc §8.3.

### 2026-09-30 — movement — T4 done: walk/run from the shipped CommonNumber table
- Found `CharacterWalkSpeed=6`, `CharacterRunSpeed=20`, `CharacterSwimSpeed=20`,
  Ride 8/40 in `proof/gravity/number.krl.txt` (units per 15 Hz logic frame).
  Client constants updated: walk 90 u/s, run 300 u/s - exact 6/20 u per tick.
- Verified: build ok, selftest 24/24; in-game `spd=300u/s(RUN)` then
  `90u/s(WALK)` (`reborn_20260930_152403.log`), integer positions.
- Remaining: sprint hold value (host 8.8 尺/s), `CharacterYawTurnSpeed`
  (camera A10).

### 2026-09-30 — movement — T2 disposition (scene/server data) + P4 probe negative
- The `0x14020EE10` helper behind ProcessDropSpeed's slope math is a 65-entry
  **Q12 sine table** (0..4096 for 0..90 deg in 1/64 steps): dumped to
  `proof/gravity/heading_table_65x32.txt` (VA 0x140A0DEA0).
- The slide inputs are the in-memory terrain **cell word** (slope fields +
  road/path flag). Shipped BCH is normalized floats; cells/road flags are
  engine-scene/nav-derived (server) -> T2 is not client-derivable; normal
  terrain (packed slope <=1) forces `Vz=0` anyway. T2 parked behind P5/server
  data, not required for current-map parity.
- P4 probe: the scene objects' `templateFile: "npc.json"` is an editor
  template name; 12 candidate paks paths do not exist. `bUnitWalkable`/
  `bUnitCanPass` values still have no located shipped source (G-21); next
  candidates: scene-response binary decode or a `filepath.ini` index search.
- Evidence: `proof/gravity/disasm/process_drop_speed.txt`,
  `heading_table_65x32.txt`; plan doc §8.3.

### 2026-09-30 — collision — P0 refined (authored siblings keep physics) + all 5 maps re-baked
- Re-running the bake for the other four maps (白龙绝境, 天原绝境, 海岛绝境,
  龙门寻宝_夜晚) produced their `.meshes.txt` sidecars + fresh cflags; oflags
  generated for all five. New finding from the sibling probe: several `auto=0`
  meshes on the other maps DO have authored `.CollisionMesh` siblings, which
  the engine's file-selection chain uses - so the skip rule is now
  `auto=0 AND no sibling` (bit5 in the oflags marks the exception).
- Verified: selftest 25/25 (`obstacle_flag_off_with_sibling_blocks`);
  龙门寻宝 unchanged `noObstacle=56` (`reborn_20260930_153559.log`).
- New plan item P0b: when a sibling collision file exists the BAKE should use
  it instead of the render mesh (recorded in the comparison doc §8.3).

### 2026-09-30 — collision — P0b done: authored collision siblings baked
- `tools/export_structure_collision.py` now resolves each object's obstacle
  geometry through the engine's file-selection chain
  (`<base>_proxymesh.mesh` / `.proxymesh` / `.CollisionMesh` / render `.mesh`)
  and picks the first that exists in the pak; the camera-flag lookup falls back
  to the render model.
- Re-baked all five maps (structure + sidecars + oflags). Non-tree sibling
  substitutions: 白龙绝境 17, 天原绝境 133, 海岛绝境 8, 龙门寻宝_夜晚 15,
  龙门寻宝 0.
- Verified: client load unchanged on 龙门寻宝 (5235 instances, noObstacle=56),
  rug crossing py=924 hits=0, fps 238-248 (`reborn_20260930_154559.log`).

### 2026-09-30 — collision — P3 closed as runtime boundary (capsule K/V not shipped)
- `PxWorld::GetRigidParam` / `ShapeData` consume the shape/rigid tables for
  dynamic actors; the gameplay movement capsule is the SIMWorld scenario K/V
  (`capsules radius/length`) set from the character unit at runtime. No shipped
  value exists (ragdoll file = bone capsules r6/l10-12; shape id 6 = dynamic
  actor capsule). Host keeps 17/116 (scaled from the 花萝 115.58 u bind pose),
  registered as a host value with this re-open criterion.
- Plan status after this: P0/P0b/P1/P2-T1-T3/T4 done; T2/P3/P4 closed as
  scene/server/runtime boundaries; P5 (host the game physics stack) and P6
  (server dynamics) gated on a milestone decision.

### 2026-09-30 — collision — re-baked maps smoke-tested
- 白龙绝境 (RC_MAP smoke): loads the re-baked bins cleanly -
  `instances=5156 meshes=817 camflag0=112 noObstacle=172`
  (`reborn_20260930_154956.log`).
- 天原绝境 (the 133-substitution map): loads with
  `instances=5842 meshes=501 noObstacle=100` (shared engine root; line captured
  from the concurrent-session log `reborn_20260930_155044.log`).
- No crashes, terrain/spawn fine on both; 龙门寻宝 unchanged.

### 2026-09-30 — collision — P5 feasibility confirmed: game physics stack runs in-host
- Added `client/PhysicsProbe.cs` (`RC_PHYS_PROBE=1`) and the standalone
  `tools/p5_physics_probe/PhysicsProbe.cs` (csc, no engine needed).
- In-client results (`reborn_20260930_161323.log`): `GetPhysicsManager hr=0`,
  `CreatePhysXTerrain hr=0`, `LoadTerrain(vt5) ok=1` (region table 8x8,
  regionMgr created), `UpdateTerrain x40` streaming, and
  `CreatePhysicsSceneDynamicLoader hr=0` (`StaticPhysicsSceneManager`,
  vtable rva 0xFCCA8) - the whole static pipeline comes up inside our client.
- Standalone: PhysX core initializes in a fresh process (`Init hr=0`) after
  preloading `KGCommonX64`/`SemanticX64`/`Engine_Lua5X64` (`SetWorkingDir
  hr=0`); the map loader needs the engine FS, so the integration point is the
  client host (as the legacy probe was).
- Next: drive the static dyn loader (`recon_dyn_methods.txt`) to add object
  actors, then query the real scene (`PhysicsScene` rva 0xFA7B8, `SweepEx`
  vt[16]) - P5 continues.

### 2026-09-30 — collision — P5 static-object path is server-owned (LoadFromFile ok=0)
- Extended `RC_PHYS_PROBE`: dyn vt[4] = `StaticPhysicsSceneManager::LoadFromFile`
  (0x2BB80), vt[2] = UpdateScene (0x2BA90). `LoadFromFile(sceneDir, mapName)`
  returns **ok=0**.
- Cause (pak check): `data/source/maps/<map>/entities/sceneinfo/%03u_%03u.json`
  does **not exist** in the shipped client - only `sceneinfo_full/%03u_%03u.json`
  (editor data, 1448 objects in 002_002). The engine's runtime static-object
  loader has no local source; that set is server-streamed (P1/P4 boundary).
- P5 disposition: engine stack + terrain + scene manager run in-host
  (feasibility proven); its remaining value is the PhysX query API on
  terrain/dynamic actors, not static collision. Scan diagnostic fixed
  (private-memory filter) after the crash.

### 2026-09-30 — collision — All five maps smoke-verified; sprint disposition
- 海岛绝境: `instances=3683 meshes=223 noObstacle=0`
  (`reborn_20260930_170531.log`); 龙门寻宝_夜晚: `instances=5100 meshes=690
  noObstacle=177` (`reborn_20260930_170643.log`); 白龙绝境/天原绝境 already
  verified. All five maps load the re-baked bins + sidecars cleanly.
- Sprint: the shipped rush tables (`player_rush_skill.txt`,
  `skill_rush_state.txt`) are skill moves (轻功) with no hold-to-sprint
  constant; the host 8.8 尺/s stays a registered test convenience.
- Note: back-to-back client starts hit the single-instance guard if the
  previous process is still shutting down (no log written, fast exit) - add a
  3 s gap between smoke runs.

### 2026-09-30 — collision — Remaining items closed (H1 live-game A/B, P5 query API)
- H1 (G-35): offline rule + audit exact (60 rejects); the in-game A/B needs
  live-game movement observation, banned by the locked constraints (no
  real-client hijack) - closed with that boundary.
- P5 query API: `SweepEx` anchor located (assert/name at 0x18001C7FB, scene
  vtable RVA 0xFA7B8, vt[16] = 0x1C5B0); call-site ABI unrecovered, and a blind
  PhysX call would violate the no-guesswork rule. Terrain queries already
  covered by TerrainSampler + EngineRay. Optional, not required for parity.
- Plan execution is complete: everything client-derivable is implemented and
  gate-verified; all remaining items are closed as proven boundaries.

### 2026-09-30 — collision — Full check + manual test list
- Full check ran: gates (jx3_model 10x, verify_model, loot, collision 25/25)
  and the three standard routes (rug y=924 hits=0; cabinet held z=36704;
  demo walk/run 90/300 + jump). Manual test checklist written to
  `docs/movement/COLLISION_TEST_LIST.md` (registered in the movement README).

### 2026-09-30 — collision — Walked-up wood pile fixed (remove host climb shortcuts)
- User report: "walked up a thing higher than me" - reproduced from the log:
  the climb target is inst 624 `wj_木堆001_hd` (wood pile), AABB y 787..1057
  (270 u tall) at x 19858..20097 / z 30743..31026; the player reached y=1048
  (its top) at (19951,30869).
- Cause (host inventions predating the plan): (1) the 42/67/92 u look-ahead
  support probes and (2) the caller-side `SupportHeight(px,pz,+64)` raise -
  both lifted `ground` onto any surface within +64 u, every 15 Hz tick, which
  chained up the pile's stepped logs.
- Fix: both removed; movement now climbs only via faces the capsule actually
  contacts (`Resolve` step branch, CCT semantics). Selftest 25/25.
- Verified: tall pile crossing (spawn 20450,30800 west): bump/fall, stays at
  ground (874 peak, `173214`); mid pile (20450,31000): no climb (was 974,
  `173345`); rug unchanged py=924 `hits=0` (`173620`); house floor edge still
  steps up to ~969 (`173445`); buildings still block (inst 563 top 1061 >
  feet 888).

### 2026-09-30 — collision — Pile walk-up fix PROVEN by controlled A/B
- Built the pre-fix revision as a separate binary
  (`bin64\reborn_client_pileold.exe`, source = `85e086b^`) and ran the same
  spawn/direction on both.
- z=31000 crossing (spawn 20450,0,31000, dir -1,0, held W):
  - OLD: t=2s `(19870, 974, 31000) grounded=True` - walked up onto the pile.
  - NEW: t=2s `(19850, 874, 31000) grounded=False vy=-660` - bump/fall, stays
    at ground (`reborn_20260930_182507.log` vs `..._182553.log`).
- Two other routes (z=30800 straight, NW segment toward the pile top) do not
  reproduce a walk-up on either build - the user's 1048 was reached with the
  jump (their log shows the 小跳c clips immediately before), which is the
  engine table jump (v0=90 u/f, g=11 u/f2 -> ~368 u apex), not a walk-up.
- The fix removal is therefore validated on the reproducible route; the A/B
  binaries are kept for the user.

### 2026-09-30 — collision — Solid props collide as AABB boxes (pile no longer penetrable)
- User follow-up: after contact-only steps the wood pile became **walk-through**
  (its render mesh is stacked logs with air gaps: `hits=0` through the
  footprint), and the tiered log faces let a slow contact-step climb back up.
- Fix: `propSolid` instances (registered proxy family 柜/箱/桌/桶/缸/坛 + 堆)
  now collide as their **world AABB box** in `InstanceContact` (`AabbContact`):
  smallest-overlap push, surface height = the box top, so the CCT step branch
  can only step onto the top within the step budget (a low box still steps,
  the 270 u pile blocks). No winding, no tiers, no gaps.
- Verified: selftest 27/27 (`solid_prop_blocks_tall`, `solid_prop_step_low`);
  in-game pile mid (20450,31000) and tall (20450,30800) held at x=20114 with
  y constant and hits climbing - no climb, no pass-through
  (`reborn_20260930_184525/184610.log`); rug unchanged (924, hits=0).

### 2026-09-30 — collision — Vertical rise is resolved (jump-through-roof class)
- User diagnosis confirmed: vertical motion was never resolved (only horizontal
  substeps were), so a jump could carry the capsule through a thin slab, and the
  degenerate normal could flip past the capsule midpoint and eject it upward.
- Fix: `Resolve(..., vMotion)`: while RISING, horizontal faces oppose the motion
  (`ny = -sign(vMotion)*|ny|`), so a face hit on the way up pushes down; the
  client calls it for `vy > 0` only. A first version resolved falling too and
  created a fall->push-up ratchet that launched the player to y=1274 at
  (18850,36700) - caught by the in-game check and restricted to rises.
- Verified: selftest 29/29 (`vertical_rise_slab_blocks` py 40 -> -11;
  `vertical_fall_slab_supports` py -> 64); in-game spot (18850,36700) stable at
  y=959 with hits climbing (no launch, `reborn_20260930_203353.log`); a
  pre-vertical-fix binary kept as `bin64\reborn_client_prevert.exe`.

### 2026-09-30 - collision - Wall-ledge ratchet fixed (CCT up-sweep) + standability gate
- Field report (user, 玉门关 building backside x 18899-19026): running into the
  back wall climbed the player +96 u (y 951<->1047 airborne cycles, capsule
  embedded in the face) - "walked up because the wall had a step, then half way
  into the building". Bin data: the wall profile (mesh 364, inst 93) carries
  ledge/molding faces at y ~975/990/1030/1040 - a ladder within the step budget.
- Root cause: the step branch claimed ANY touched face top within the budget
  (`lowTop`) without requiring the raise to clear the blocker, and skipped the
  push-out while stepping (embedding); the caller support raise then lifted
  `ground` to overhang undersides under the capsule centre every tick.
- Fix: CCT up-sweep contract - a step is accepted only when the raised capsule
  is clear (`CapsuleBlocked` at top+0.1); the support raise only to a surface
  the capsule can stand on (`CapsuleBlockedDown`: reject downward contacts
  only; horizontal side overlaps are left to the solid-prop push). A first
  strict fit test bounced the player airborne on the prop edge at the rug
  (19690,36270) - caught with `RC_SUPDBG=1` (sh=924.2 rejected by a 0.17 u
  horizontal AABB contact) and fixed by the down-only filter.
- Also fixed: the log fingerprint read the shared `build_info.txt` (every
  feature run printed the canonical build's git hash, 2026-09-30 incident);
  it now prefers `build_info_<exe>.txt`.
- Verified: selftest 31/31 (`wall_ledge_step_rejected` blocked/ground 0;
  `capsule_fit_probe`; `low_plank_open_top_passes`); in-game A/B at
  (18915,36850): old build oscillates 951<->1047 airborne, new build stable
  952-994 blocked (`reborn_20260930_212339` vs `_214718`); rug/prop spawn
  (19690,36270) new build stable grounded y=969, no bounce
  (`reborn_20260930_214555`). A/B binary kept: `bin64\reborn_client_prewall.exe`.

### 2026-09-30 - collision - Thin-wall walk-through (horizontal motion vs the degenerate normal)
- Field report (user, end area): "the last wall i can just walk through... i
  simply walk through for no reason" at the 玉门关建筑001_002 south wall
  (x 27168..27768, z 33864..33875, y 853..1224). Session log scan + geometry
  check: their last move (27606,33546)->(27508,34057) crossed the wall face
  with only +2 blocked substeps.
- Root cause: the horizontal twin of the roof bug. Once the capsule centre
  crosses a thin face inside one substep, the closest-point normal points ALONG
  the motion and the push-out ejects the capsule out the FAR side (the "+2
  blocks" were pushes through, not stops). PhysX sweeps avoid this; the host's
  discrete 19-20 u substeps + 17 u radius do not.
- Fix: `Resolve(..., hMoveX, hMoveZ)`: a horizontal contact whose normal has a
  forward component (dot > 0.2 of the move) is flipped so the push opposes the
  motion (same contract as the vertical `vMotion` flip). Callers pass the
  per-substep direction (client substep loop + airborne rise; `MoveResolved`
  passes dx,dz).
- Verified: selftest 32/32 (`thin_wall_no_popthrough` blocked px 13); in-game
  A/B at the wall: old crossed to z=34125 in 2 s (2 blocks), new stops at
  z=33852/33884 both directions with `blocked by inst=949
  jz_xb玉门关建筑001_002_hd.mesh` and slides along it
  (`reborn_20260930_222047` vs `_222711`/`_223142`); regressions re-checked:
  building back wall spot stable (18915,952), rug/prop spawn stable grounded.

### 2026-10-01 - collision - Map-wide wall-sweep audit + the creep class
- Built the audit the user asked for ("stop making me find bugs"): the
  `collision_selftest_<exe>.exe audit <structuresBin> [foliageBin] [stride]`
  mode sweeps a capsule against every wall-like face in the baked data with
  the real contact/resolve code (grid lookup included) and flags faces that do
  not stop it. 龙门寻宝 has 2.24M tris (1.78M wall-like per mesh; ~13M
  instance-level faces).
- Two audit bugs found and fixed while calibrating (both were audit-side, not
  client-side): (1) the audit set grounded=true so its mini-resolve took the
  CCT step-up branch - every face whose top is within the 64 u budget was
  legitimately stepped over (33,960 -> 19,079 flags after testing airborne);
  (2) substeps of 20 u exceed the 17 u radius, so a thin SMALL face (foliage
  leaf/branch, rock sliver) degenerates to an edge push and the capsule creeps
  through (19,079 -> 7,710 after the substep cap). Root-cause chain verified
  offline: the client's w2l and world-distance 3x3 are correct; a Python
  replica of the contact math found the 5.4 u contact the audit missed.
- Client fix: movement substeps are capped below the capsule radius
  (`subCap = min(20, radius*0.9)` in the client and in the audit sweep);
  registered as deviation 4d.
- Full-map numbers (龙门寻宝, stride 64 = every 64th wall face, 208,803 tests,
  ~8 min): 3.7% residual flags - concentrated in scenery/foliage tiny faces
  (`sd_崖壁狱门`, `st_mj戈壁碎石/石头/石拱`, `cq_玉门关城墙`, 楼兰哨台); only 32
  flags in the play area, all small decorative props at unreachable heights.
  Structural wall blocking (the user's field class) is closed; the tiny-face
  creep is registered OPEN (next: anti-motion clamp for near-forward contacts
  or continuous contact).
- A/B/notes: the user's session (pid 33448) blocks the shared exe name - test
  builds run as `reborn_client_colltest*.exe` (own namespace).

### 2026-10-01 - collision - Band-aid audit pass: step budget cleared, tree prisms registered
- User asked to move toward "less band-aid, more original". Audited the top
  candidates before touching them:
  - **Step budget 64 u is NOT a band-aid** - it is the engine's own landing
    tolerance (`ProcessVerticalMove` 0x14031A25E) and is backed by a live-game
    field case (51 u interior house floors walkable). The PhysX `stepOffset`
    50 u is the CCT default whose gameplay applicability is unproven
    (`G-1/G-13`). Changing to 50 would regress a proven behavior -> dropped
    from the plan; registered as deviation 4f with the reason.
  - **Tree trunk prisms ARE a registered host deviation** (62/68 trees have a
    degenerate shipped `.CollisionMesh`; the prism is measured from the visual
    mesh). Deleting it would make those trees walk-through, so it stays with
    the recovery path documented (`.srt` decode or engine obstacle production)
    - registered as 4e in the comparison doc + in the exporter docstring.
- Remaining genuine recovery work, priority order: slope model
  (`ProcessDropSpeed` + live engine cell slope), cave floors under holes,
  obstacle production reverse, capsule values, engine-physics-in-host.

### 2026-10-01 - collision - Cheap-step pass: two stale audit claims corrected, slope re-confirmed parked
- Ran the "cheap + original" list down to evidence:
  - **Slope (T2)**: the earlier research re-scoped it as an AIRBORNE slide
    feature (`ProcessDropSpeed`) over the engine's in-memory cell word + road
    flags, not a walking climb limit; not client-derivable cheaply -> stays
    parked (`COLLISION_SYSTEM_COMPARISON.md` 8.3 T2). Do NOT present it as a
    cheap fix.
  - **Cave floors**: verified the underground IS baked - the scene data has the
    山洞 meshes (`sd_崖壁狱门fb_001_hd`) and 25 fully-underground instances
    (y to -1200) in the bin. The blanket "holes fall bottomless" claim was
    stale -> corrected; re-open only with a cited hole cell.
  - **Canopy columns**: no such code exists (grep); the real tree deviation is
    the host-measured trunk prisms (62/68 degenerate CollisionMesh) -> already
    registered as 4e; the STATUS doc line was stale -> corrected.
  - Step budget: NOT a band-aid (engine landing tolerance + 51 u field case);
    kept, registered as 4f.
- Net: the registered deviation list is now accurate; the remaining genuine
  items (slope slide, obstacle production, capsule values, engine physics) are
  deep recovery work, not cheap steps.

### 2026-10-01 - collision - Obstacle production REVERSED (engine rule ported)
- Deep RE pass on `KG3D_LoaderNoRenderX64.dll` + `PhysicsEngineX64.dll` (game
  client copies in a temp dir; never the installs):
  - The loader's property readers: `_LoadDotIni` (`0x18002c6d0`) reads
    `[Display] bAutoProduceObstacle` (default 1); `_LoadDotMeshDotIni`
    (`0x18002c9a8`) reads per-LOD-submesh `bLogicObstacle` / `bSelectable` /
    `bCastShadow` / `bTransparentCamera` / `bRecomputeNormals` (defaults
    1/1/1/0/0) from `[DisplaySub-LodN-SubM]`.
  - The physics chain (strings in `PhysicsEngineX64.dll`): actor option
    `ppszCollsionFileArray` -> `_CreateCollisionFromMeshFile` ->
    `PhysicsShapeFactory::CreateCacheShapeFromFile` / `_CreateFromGeometryData`
    (box/capsule/sphere/convex/heightfield/triangle) - the authored
    `.CollisionMesh`/`.proxymesh` siblings win; otherwise the mesh itself.
  - **Rule (HIGH):** the produced obstacle = the union of LOD0 submeshes with
    `bLogicObstacle=1`; `bAutoProduceObstacle=0` or no logic submesh -> no
    obstacle (unless a sibling exists).
- Host bug found while porting: the oflags exporter took the FIRST ini
  candidate (`<base>.ini`, a stub with no DisplaySub sections) and never saw
  the full `<base>.mesh.ini` -> bits 1/2 were 0 on 343/681 meshes. Fixed by
  merging both files (the engine reads both); all 5 maps re-exported.
- Client: the skip rule now ports both bits (`(auto==0 || logic_any==0) &&
  no sibling`); new selftest `obstacle_flag_logic0_walkthrough` (33/33 PASS).
  In-game: pile still blocks (logic=1), rug stable (rug mesh now correctly has
  no engine obstacle - terrain provides the support).
- Note for future edits: never rewrite source files with PowerShell
  `Get-Content|Set-Content` (PS 5.1 reads BOM-less UTF-8 as ANSI and mangles
  the Chinese literals - this broke the prop selftests until restored from
  git and re-applied with the edit tool).

### 2026-10-01 - collision - Capsule K/V deep pass (runtime boundary confirmed)
- Goal: replace the host-chosen capsule 17/116 with the engine's gameplay
  values. Result: confirmed NOT recoverable from shipped data, with new
  evidence: `SIMWorldX64.dll` interns `capsules radius`/`capsules length` in
  two tiny accessors (`0x180001ba0`, `0x180002700` - key handle stored into a
  global) and the `_AddCapsules` builder (`0x1800223b0`) is a validator +
  vtable dispatch with no default constants. The values live in the runtime
  semantic K/V table; no shipped file carries them:
  `physic_character_param.krl.txt` = `EnableCharacterCapsule=1` + ragdoll
  bodies only; `physic_shape_param.krl.txt` capsule r50/l50 = named per-object
  shape; extracted `player_*` configs have no radius/height.
- Disposition: stays a registered host value (17/116) until a runtime/capture
  source exists (P3 row updated with the RVAs). Next deep item:
  engine-physics-in-host (feed baked geometry into PhysicsEngineX64 and use
  its own character sweep) - a multi-session project.

### 2026-10-01 - collision - Engine-physics-in-host: vtables mapped
- First step of the endgame track: static disasm of `PhysicsEngineX64.dll`'s
  four vtables - PhysicsTerrain @0xFCFE0, PhysicsScene @0xFA7B8,
  StaticPhysicsSceneManager @0xFCCA8, PhysicsManager @0xFA6C0 - every entry's
  RVA + role recorded in `proof/movement/phys_engine_vtables.txt` (HIGH).
- Key findings: the terrain vtable has NO height query (vt[7] is a
  region/tile mask test; the heightfield is a PhysX object) -> engine queries
  must go through a `PhysicsScene` instance; `CreatePhysicsScene` (manager
  vt[14]) needs a real engine-side arg (the adapter's scene object), which the
  P5 probe does not yet have. StaticSceneMgr vt[2]/vt[4] roles corrected vs
  the earlier note.
- Next: (a) find the adapter's engineArg (KG3DEngineAdapterX64 import chain),
  (b) else reuse the engine's own PxPhysics/PxCooking (manager fields) to
  build a private scene with our baked geometry, then A/B the engine's
  floor/sweep vs our solver at the field spots.

### 2026-10-01 - collision - Engine PhysicsScene CREATED in-host (endgame unblocked)
- The P5 probe now creates the engine's own PhysicsScene inside our client:
  `CreatePhysicsScene` (manager vt[16], hr=0) with the engineArg = the active
  engine scene resolved exactly like EngineRay does
  (`KG3DEngineDX11EX64!KG3D_GetEngine2 -> GetActiveWindow2 -> Get3DScene2`).
  The adapter's own recipe was decoded first (`KG3DEngineManager::Init` @0x737da:
  LoadLibrary PhysicsEngineX64 -> GetPhysicsManager -> mgr vt[0] Init(cfg,
  engineArg=[engineMgr+0x2018], 0) -> SetWorkingDir).
- Scene vtable runtime-confirmed 1:1 with the static dump; the manager dump was
  shifted by 2 (runtime vt[14]=CreatePhysXTerrain, vt[15]=dynLoader,
  vt[16]=CreatePhysicsScene - all working in-host). Query candidates recorded
  (vt[16]=SweepEx 0x1C5B0 anchor 0x1C7FB + 11 more) in
  `proof/movement/phys_engine_vtables.txt`.
- Evidence: `reborn_20261001_170644.log` / `_170929.log` (physprobe lines).
- Next: recover the query ABI (disasm the vt[16]/raycast call sites), call the
  engine's sweep/raycast against the loaded terrain + our baked geometry, then
  A/B vs `TerrainSampler`/our solver at the field spots.

### 2026-10-01 - collision - Gameplay query API found (SIMWorldX64 exports)
- Scene query functions mapped by their KGLOG strings: scene vt[16] =
  `PhysicsScene::SweepEx` (9 args; caller outside PhysicsEngineX64),
  vt[8]/vt[9] = `Overlap`, vt[18] = `ComputePenetrations`.
- Better: the game's own gameplay queries are EXPORTED by `SIMWorldX64.dll` -
  `PxWorld::GetFloorHeight` (0x31F20), `PxWorld::RayCast` (0x32FB0),
  `PxWorld::RayCastDirect` (0x332D0) - with the setup chain ctor 0x319F0 /
  Initialize(KVTable@Semantic) 0x32070 / Setup 0x337A0 / SetupPhysic(PEAX,PEAX)
  0x33D70. Recorded in `proof/movement/phys_engine_vtables.txt`.
- Next: LoadLibrary SIMWorldX64 in the host, build a PxWorld, Initialize +
  SetupPhysic with our in-host engine scene, then A/B GetFloorHeight /
  RayCastDirect vs TerrainSampler at the field spots.

### 2026-10-01 - collision - PxWorld needs semantic factories; direct wrapper route found
- Follow-up on the query API: `CreateSIMWorld` (SIMWorldX64 @0x351C0) takes a
  Semantic TABLE object and drives its factory calls (vt[0x170]/0x168/0x3A8)
  plus `represent\simworld.json` - the PxWorld route needs the game's semantic
  component factories (the represent layer we do not boot). Parked.
- Direct route: `PxWorld::SetupPhysic` -> `0x18001A6E0(arg1,arg2)` (singleton,
  0x2043B8 bytes) -> `PhysicScene::_Init` @0x180024140 (assert-named): stores
  arg1/arg2, calls `arg2.vt[0x38]()` for the terrain desc (cell/counts) and
  builds the PhysX scene at [this+0x18]; `GetFloorHeight` calls the wrapper's
  vt[0x20]. So calling `SIMWorldX64+0x1A6E0(arg1,arg2)` in-host + the wrapper's
  vt[0x20] gives the engine's floor query without PxWorld/semantic tables.
- Next: pin arg1/arg2 from the SetupPhysic caller, then A/B vs TerrainSampler.

### 2026-10-01 - collision - Engine-physics track: route 1 parked (semantic dependency), route 2 scoped
- Traced `PxWorld::Setup` (0x3380B..0x33D54): the wrapper's two physics objects
  are created by the SEMANTIC table factory (`table.vt[0x170](key,1)` ->
  [this+8]/[this+0x10]; callbacks via vt[0x3A8]), then `0x1A6E0(objA,objB)`.
  The same two objects `PhysicScene::_Init` consumes. So the SIMWorld query
  layer depends on the game's represent semantic factories - not bootable from
  our host without the represent layer. Route 1 parked (documented).
- Route 2 (scoped): drive the engine's own PhysX 3.3.4 directly - the engine's
  PxPhysics/PxCooking objects are reachable from the PhysicsManager fields
  (`_InitPhysX` strings 0xf91e8/0xf9210); cook our baked triangles and run the
  engine's own character sweep. Self-contained, no represent dependency.
- Multi-step outcome: engine PhysicsScene in-host (kept), query API mapped,
  wrapper/semantic boundary documented, route 2 defined as the next build.
