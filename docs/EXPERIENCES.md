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