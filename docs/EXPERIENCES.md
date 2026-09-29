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