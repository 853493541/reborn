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

### 2026-09-29 — repo — Rule: research = copy & analyze, never affect installs
- Did: added the copy-and-analyze rule — `AGENTS.md` §4 (allowed: copy files out into
  ignored dirs, analyze offline, observe running; forbidden: writes, renames, deletes,
  patches, injection — on disk or in memory) and §8 (installs read-only; documented
  `MovieEditor\bin64` build outputs are the only exception). Mirrored in
  `tools/netcode/AGENTS.md` and `native/AGENTS.md` (no in-memory patching; the broken
  `RC_PatchD6` trampoline is cited as the cautionary example).
- Evidence: `AGENTS.md` §4/§8, `tools/netcode/AGENTS.md`, `native/AGENTS.md`.
- Outcome: solved.

### 2026-09-29 — repo — Rule: never push to origin unless explicitly asked
- Did: strengthened the git rule — `AGENTS.md` §2 default mode ("Never push to `origin`
  (any branch) unless the user explicitly asks; pushing is always an explicit request;
  local commits are fine"), `#iso` step 5 (commit after every change; never push unless
  asked — replaces "push after every commit"), and §13 Git hard rule (same wording).
- Evidence: `AGENTS.md`; this commit, kept local per the new rule.
- Outcome: solved.

### 2026-09-29 — repo — Rule: mandatory closing game-design check
- Did: `AGENTS.md` §15 now requires every response to end with the game-design check
  ("Does this respect the game's own design — or are we inventing new ways / band-aid
  fixes?" -> "No — we are not inventing fixes ourselves; we connect the real game
  engines and reproduce the original game design, not band-aid around it."), with the
  caveat that a provisional deviation must be named explicitly (§6) rather than
  silently answered "no".
- Evidence: `AGENTS.md` §15; this commit (local).
- Outcome: solved.

### 2026-09-29 — repo — Rule: sandbox windows title as sandbox-<featurename>
- Did: `AGENTS.md` §2 item 7 requires feature/sandbox apps to set their top-left window
  title to `sandbox-<featurename>`; implemented: the client derives `sandbox-<slug>` from
  `reborn_client_<slug>.exe` (canonical keeps `JX3`), `ability_sandbox` ->
  `sandbox-ability`, `asset_sandbox` -> `sandbox-asset`; all three rebuilt (`exit=0` /
  `build OK`); `client/AGENTS.md` note added.
- Evidence: `client\RebornClient.cs`, `ability_sandbox\rb\RebornClient.cs`,
  `asset_sandbox\AssetSandbox.cs`, `AGENTS.md` §2.7; this commit (local).
- Outcome: solved.

### 2026-09-29 — tooling — "Too many images in request" (API caps images at 30)
**Problem:** long sessions that Read image files (screenshots, proof PNGs) accumulate
image parts in the conversation; every later request fails with
`invalid_request_error: Too many images in request: 31 > 30`, blocking the session.
**Tried:** mitigation is not possible once the cap is hit - prevention only.
**Outcome:** solved (prevention).
**Why:** the API counts every image in the whole conversation, not just the latest
message; attachments persist in history. One session must stay under 30 total.
**Re-open criteria:** the provider raises the cap, or opencode strips old image parts
from history automatically.
**Links:** `tools/proof/image_stats.py`, `tools/proof/README.md`, `AGENTS.md` §13 Images.

### 2026-09-29 — engine_host — Mini-sandbox branch review (`agent/mini-sandbox`)
- Did: reviewed the cropped-loose-map sandbox branch vs `main`: builder
  (`tools/sandbox/build_sandbox.py`, official extractor, world coords preserved,
  manifest + sha1), client delta (RC_PHYS_DLL, init/LoadMap ms, title), verified runs
  (`proof/sandbox/*`: 1x1 = 6.5 MiB, LoadMap 156 ms vs 1.6 s full, fps 231-274).
- Evidence: merge-base diff `ff67fff..agent/mini-sandbox`; `MINI_SANDBOX_CLIENT.md`.
- Outcome: solved (review). Verdict: pull-ready; conflicts = title hunk + doc appends;
  convention fix needed (`Sandbox-pure` -> `sandbox-mini`); builder `--client-root` arg
  accepted but unused.

### 2026-09-29 — engine_host — Install-write audit: shader cache under zhcn_hd\CachedShaders
**Problem:** the copy-and-analyze rule (`AGENTS.md` §4/§8) bans writes under
`C:\SeasunGame` except documented `MovieEditor\bin64` outputs. A read-only check during
the mini-sandbox review found files in
`C:\SeasunGame\Game\JX3\bin\zhcn_hd\CachedShaders` dated 09-29 20:32/21:00/21:02/21:28
that coincide with our engine runs (20:32 is the branch's disclosed junction experiment).
**Tried:** read-only attribution (no writes); repo grep - the path is not documented
anywhere.
**Outcome:** open (finding) - attribution not yet proven by a controlled run.
**Why:** likely the engine writes its shader cache under the asset root (`workingDir` =
`zhcn_hd` in the client), i.e. every host run may write there.
**Re-open criteria:** controlled canonical run watching `zhcn_hd\CachedShaders` mtimes
before/after; if confirmed, find an engine redirect or register a documented deviation.
**Links:** `docs/engine_host/MINI_SANDBOX_CLIENT.md` dead-end #1; `AGENTS.md` §4/§8.

### 2026-09-29 — repo — Rule: full-chain ownership (the agent is the tester)
- Did: `AGENTS.md` §15 now requires the agent to own the whole chain for any repeated
  problem or fix request: reproduce deterministically (scripted repro, logs, numeric
  fingerprint), fix against the repro, prove it solved with before/after evidence plus
  the §12 gates, and never ask the user to retry and report back.
- Evidence: `AGENTS.md` §15; this commit (local).
- Outcome: solved.

### 2026-09-29 — client — Code provenance audit (original vs game-derived)
- Did: audited `client/*.cs` + `native/camera_shim.cpp` (main `2a973a0`) and the client
  deltas of all 8 active isolation branches. Counted lines, IL/RVA citations, engine
  call sites, and data-file provenance; filed as `docs/engine_host/CLIENT_PROVENANCE.md`
  (registered in the area index).
- Evidence: 6,545 client lines total; 71 IL/RVA citations; 233 engine/API call sites;
  100% of source written by us — game-derived parts are behaviour rules (camera 23%,
  collision) + engine calls (58%) + config readers (4%) + native shim (15%); data files
  from game: `camera.json`, `scene_init_param.txt`, baked collision bins, mesh flags.
- Outcome: solved (audit), no code changes.
- Re-open: re-run after branch merges; line-level attribution would need per-hunk review.

### 2026-09-29 — engine_host — Mini sandbox client plan (small-map dev root)
- Did: measured the loop's weight (PakV4 **192.34 GB**, client root 21.62 GB,
  MovieEditor 2.46 GB) and planned a mini sandbox: sandbox asset root with its own
  `clientconfig.ini` + mini `PakV4` (or loose tree), trace-driven dependency closure,
  native repack via the engine's own `KG_PAKFS_*` write API (verified exports:
  `WriteFile`, `MakeSubPackage`, `CreateDirTree`, `EnableFileTrace`, …), and a small
  scene tier (existing `source\Map\EmptyMap`/`512Simple2`, then a 2×2 region crop of
  龙门寻宝 around region (2,2), world coords preserved).
- Evidence: `docs/engine_host/MINI_SANDBOX_CLIENT.md` (sizes, formats, exports, phases
  S0–S4); local scans + dumpbin; no install writes.
- Outcome: plan committed on `agent/mini-sandbox`; S0 (loose-map load spike) and S1
  (foreign asset root spike) are the next gates.

### 2026-09-29 — client — Mini sandbox: cropped loose map via RC_MAP (verified)
- Did: built `tools/sandbox/build_sandbox.py` (crop a real map to a loose map dir,
  world coords preserved by re-anchoring `WorldOrigin` + renaming region files) and
  verified the client loads it via `RC_MAP=<absolute path>`: InitPak 406 ms,
  LoadMap 281 ms, `TerrainSampler regions=2x2 origin=(0,0)`, real spawn
  `(23334,761,24224)`, 5351 collision instances, screenshots render the real scene.
  Client additions: `RC_PHYS_DLL` override + init/LoadMap elapsed-ms logs.
- Lesson (dead end): a custom sandbox asset root (junctions + copied configs)
  stalls `InitPak` ~181 s waiting on the StreamDownloader IPC, and **writable
  junctions leak engine writes into the install** — shader-cache files in
  `zhcn_hd\CachedShaders` (20:32:25) and a minidump in `zhcn_hd\bin64\minidump`
  (20:32:45) were written during the experiment; they cannot be undone (installs
  are read-only). Never junction writable dirs; absolute `RC_MAP` needs no root
  change at all and is the supported path.
- Evidence: `proof/sandbox/{mini_run.log,mini_00_15000ms.png,mini_01_30000ms.png,
  fingerprints.txt,map_manifest.json}`; `docs/engine_host/MINI_SANDBOX_CLIENT.md`.
- Outcome: solved. Sandbox = 21 MB loose map dir; scene load ~0.3 s.

### 2026-09-29 — client — Mini sandbox size trim: 1×1 crop, 6.5 MB (1/3)
- Did: added the 1×1 build (`--crop 2,2,1,1`, region (2,2) = the spawn region)
  and ran removal tests for the runtime file set: `heightmap/*.r32` is required
  by the renderer, `heightmap_bc/*.bch` by the physics terrain loader, and
  `blendmap_bc/*.r8` is an editor bake cache the runtime never reads. The
  builder now drops `.r8` by default (`--keep-bake-caches` keeps it). Final
  1×1 bundle: 6.5 MiB / 38 files = 32 % of the 2×2 (20.1 MiB); LoadMap 156 ms,
  spawn `(23334,761,24224)`, render verified. `run_sandbox.cmd` points at it
  and sets the title `sandbox-mini` (client derives `sandbox-<slug>` from the
  exe name per `AGENTS.md` §2.7; `RC_TITLE` overrides).
- Lesson: a spawn-height check is not enough to validate a trim — dropping
  `.r32` kept the physics height but rendered sky where the ground was; only the
  screenshot fingerprint caught it. Trim validation = screenshot + spawn + zero
  load failures in the engine log.
- Evidence: `proof/sandbox/{mini_s_run.log,mini_s_00_8000ms.png,
  map_s_manifest.json,fingerprints.txt}`; `docs/engine_host/MINI_SANDBOX_CLIENT.md`
  (size profiles + runtime file set).
- Outcome: solved. 1/3-size sandbox runs with identical terrain/props/spawn.

### 2026-09-29 — client — Quality-tier probe: no low-quality map ships
- Did: probed the `.jsonmap`-declared quality sets (`hd/bd/bddnc/mb/low`) for
  龙门寻宝 — 32 candidate paths across the non-HD sets returned only
  `bd/env_probe/skybox_s.dds`. `MapList.tab` has no quality variants (296/676/677
  → same jsonmap; 297 is the night map). The HD client loads the root HD set;
  `bd/` extras it asks for (`focus_face_env_params.json`,
  `volumetricCloud.json`) are absent in the install and non-fatal. No
  lower-quality map exists to shrink the sandbox with.
- Evidence: probe output (extractor, read-only); `MapList.tab` rows;
  full-client engine log `KG3D_Engine_2026_09_29_21_42_17.log` paths.
- Outcome: solved (answered); documented in `MINI_SANDBOX_CLIENT.md` §Quality tiers.

### 2026-09-29 — client — Startup cost is engine pre-draw, not the map
- Did: measured launch→playable for the sandbox (26.8 s) and broke down the
  engine log: `Init3DEngine` 24.4 s, of which ~22 s is
  `KG3D_Engine::StartPreDrawShader` (`预渲染总开关:开启`); `LoadMap` is only
  0.17 s. Root cause of the 22 s: `data/public/PreDrawSetting.ini` ships
  `ShouldPreDraw=1` + `PreDrawThreadMaxNum=1`; this machine's GPU score is
  61656 vs the 10257 threshold, but the one-thread cap is what makes the
  warm-up slow. Full client pays the same 24 s; the sandbox cannot change it.
- Lesson: engine init is a fixed host cost (~24 s) — scene cropping only cuts
  map load (1.6 s → 0.17 s) and memory. Faster startup requires overriding
  `PreDrawSetting.ini` (pak, install read-only) via a writable root (InitPak
  stall) or an engine-level bypass — both open research items.
- Evidence: `docs/engine_host/MINI_SANDBOX_CLIENT.md` §Startup breakdown;
  engine log `KG3D_Engine_2026_09_29_21_54_26.log`; extracted
  `PreDrawSetting.ini`; `_PreDrawMachineInfo.txt`.
- Outcome: explained; no fix applied (would need install write — forbidden).

### 2026-09-29 — repo — Merge mini-sandbox into main + naming alignment
- Did: merged `agent/mini-sandbox` (`f3e9511`) — union-resolved the window-title hunk
  (main derivation + `RC_TITLE` override), kept both experience-log entry sets and both
  `docs/engine_host/README.md` rows/tools table. Post-merge: `run_sandbox.cmd` builds
  and launches `reborn_client_mini.exe` (title `sandbox-mini`; `RC_TITLE` dropped),
  MINI doc updated, `build_sandbox.py` derives extractor/pak from `--client-root`
  (dead constants removed). Rebuilt canonical client (`exit=0`); `camera_smoke.exe`
  ALL PASS; bin64 `reborn_client_pure.exe` leftovers removed.
- Evidence: merge `f3e9511`; `client\build_client.cmd` exit=0; `camera_smoke.exe` ALL PASS.
- Outcome: solved. `main` local-only ahead of origin (no push).

### 2026-09-29 — repo — Rule: local-first (no server-side excuses)
- Did: `AGENTS.md` §4 now requires local-first research: whatever the game needs exists
  in the client/MovieEditor installs (prediction, UI data, tables, configs, formats);
  "it comes from the server / we cannot find it" is not an acceptable answer, and a
  server-side claim needs a cited client-side counterpart. Mirrored in
  `tools/netcode/AGENTS.md`.
- Evidence: `AGENTS.md` §4; `tools/netcode/AGENTS.md`; this commit (local).
- Outcome: solved.

### 2026-09-29 — repo — Rule: closing game-design check answers "Yes"
- Did: wording change in `AGENTS.md` §15 — the mandatory closing check now reads
  "Does this follow the client's own truth - no invented fixes or band-aids? **Yes** -
  we connect the real game engines and reproduce the original game design; nothing was
  invented or band-aided around." A provisional deviation must still be named explicitly
  (a "Yes" must never hide it). Reason: the old "No" answer read as if something was
  wrong, while it was intended to mean "no invented fixes".
- Evidence: `AGENTS.md` §15; this commit (local).
- Outcome: solved.

### 2026-09-29 — client — Collision + camera-wall-clip merge review (offline gates run)
- Did: full check of `agent/collision-improvement` (39 commits, 26 files) and
  `agent/camera-wall-clip` (15 commits, 17 files) vs main. Preflight conflicts are narrow:
  `client/RebornClient.cs` + `docs/EXPERIENCES.md` (camera adds `client/build_client.cmd`).
  Built and ran each branch's offline gate from detached temp worktrees (no shared-state
  writes): **collision selftest 19/19 PASS**, **camera smoke ALL PASS** (operation modes +
  obstruction rules).
- Evidence: temp-worktree builds/runs; `git merge-tree` preflight; branch logs.
- Outcome: both merge-worthy; recommended order camera first, then collision; held because
  both agent worktrees are dirty (collision 6 files, camera 3 files) - merge when the
  subjects are committed/clean (or accept the tips as-is).
- Re-open: merge + post-merge gates (collision 19/19, camera smoke, live T1/door spot).

### 2026-09-29 — client — Merge camera-wall-clip (camera pen v3) into main
- Did: merged `agent/camera-wall-clip` @ `0a8d60b` (`d450ba6`). Conflicts resolved: window
  title kept main's `sandbox-<slug>` derivation (branch's hardcoded "Camera Pen v3"
  dropped; `RC_TITLE` can reproduce it), `build_client.cmd` kept main's feature-build
  contract, `EXPERIENCES.md` = main's entries + the branch's 300 added lines. Client
  rebuilt (`exit=0`); camera smoke ALL PASS; shim source compiles with
  `RC_D6Seed`/`RC_D6Dbg`/`RC_Shim` exports verified via a temp build.
- Blocked: bin64 `camera_shim.dll` rebuild (activates the D6 seed) while three agent
  clients hold the DLL (`reborn_client_campen_v3`, `reborn_client_collision`,
  `reborn_client_ww_sandbox`) - run `native\build_shim.cmd` once they close.
- Evidence: merge `d450ba6`; temp shim export check; `camera_smoke.exe` ALL PASS.
- Outcome: merged (local only); D6 fix pending the shim rebuild.

### 2026-09-30 — client — Merge double-jump into main (二段跳 flip + calibration + S6 steering)
- Did: merged `agent/double-jump` (`7f06700`). Conflicts resolved: `RebornClient.cs` via
  `git merge-file --ours` (auto-merged jump code kept; `selfSlug` restored for the
  per-workstream config dir), `build_client.cmd` kept main's feature-build contract
  (branch's per-slug config copies dropped), `EXPERIENCES.md` = main's + 5 branch
  entries. Client rebuilt (`exit=0`); camera smoke ALL PASS; `tools/gravity/verify_model.py`
  passes (J1 default row; J0 1.92 m calibration); live run logs
  `jump: mode=flip school=0 scale=0.520 (apex 191u ~ 191cm per jump)`.
- Evidence: merge `7f06700`; `build_info.txt` git=7f06700; `reborn_20260930_132754.log`.
- Outcome: solved (local only).

### 2026-09-30 — repo — Aborted a wrong-branch merge (ww-sandbox) — ask, never infer
- Did: started merging `feature/ww-sandbox` into main based on inference from its
  "sandbox port notes"; the user stopped it — that branch was not requested.
  `git merge --abort` restored main to `765447f` (clean); the branch/worktree untouched.
- Evidence: `git status -sb` -> `## main...origin/main`; `git log -1` -> `765447f`.
- Outcome: solved (no harm). Lesson: when the branch choice is ambiguous, ask and wait —
  never infer the target from branch content.

### 2026-09-29 — camera — penetration research inventory + main-tip drift audit
- Did: read the camera docs set (`PENETRATION_PLAN`, `WALL_OBSTRUCTION`,
  `HANDOFF`, `HOST_DEVIATIONS`, `COMPLETION_PLAN`, `CLOSE_RANGE_RESEARCH`,
  `FIX_SUGGESTIONS`, `CONFORMANCE_CHECKS`), the camera git history
  (`bdfb586`..`3fd31b4`), and the live code camera block (`RebornClient.cs`,
  `CameraSystem.CameraObstruction`, `FoliageCollision.Raycast`). Confirmed main
  carries the engine look-at/absolute-Y path (D3/B6 closed), 5/9 probes with
  the per-mesh `.cflags` gate, NoCross/scene-min/degenerate-hit guards and the
  `RC_CAM_PENDBG` recorder. Drifts found: (1) the live default
  `RC_CAM_HITWIN`/`RC_CAM_HITWINDOW` is `0`, so B11 hit stabilization is OFF on
  main although `PENETRATION_PLAN` C1d and `HOST_DEVIATIONS` B11 say 0.25 s
  default-on (all added in the `3fd31b4` WIP checkpoint); (2) `docs/controls/
  CONTROLS_GAP_REGISTER.md` S9 and `docs/camera/CONFORMANCE_CHECKS.md` still
  describe the pre-obstruction state; (3) generated foliage bins + `.cflags`
  remain tracked under `engine_host_spike/collision_data`.
- Side-job queue (from `PENETRATION_PLAN`): P0 recorder audit on T1-T4 with the
  current build; P4 missing drawn classes (landscape/bd/subscene -> camera-only
  FCOL); P2 probe footprint (blocked, live host recon); P5 band-aid removal
  after the audit.
- Evidence: `git merge-base --is-ancestor 3fd31b4 main` -> true;
  `client/RebornClient.cs:429,1854,1941`; `client/FoliageCollision.cs:452`.
- Outcome: partial (research done, fix queue defined, drifts not yet fixed).

### 2026-09-29 — camera — T1 penetration root cause: front-only probes; double-sided fix landed
**Problem:** after the engine look-at/guard work the user still reported wall
penetration. The Step-1 recorder had never been run over the T1 sweep, and the
T1 cavity (`18985,682,24515`) is the original user spot.
**Tried:** built the workstream client `reborn_client_camclip.exe` (and first
fixed `client/build_client.cmd` to honor the AGENTS §2 `RC_CLIENT_EXE`
contract it claimed to implement), re-ran the T1 demo sweep with
`RC_CAM_PENDBG=1`, then A/B'd the front-only probe filter and the hit window.
**Outcome:** solved (fix landed, default on).
**Why:** main-tip defaults produced 18 event lines / 192 event-frames at T1:
the resolved camera sat on the far side of rock shell inst 897 (reverse bake
hit 1-2 u from the camera) and of scene-only geometry (reverse scene hit 6-11 u,
bake clear) while the forward front-face-only probes were blind - once the
camera slips past a surface whose front faces it, the forward query can never
see it again and the spring returns through the geometry. A/B: front-only
192 frames; front-only + hit-window(0.25 s) 118; **double-sided 0**.
`RC_CAM_BACKFACE` now defaults on (`=0` restores front-only), registered as
B14 with the real `FilterCamera` (D1) winding rule as the exit. Exact
regressions: T2 `hit=206 len=188`, T4 `hit=186 len=168`, T1 `hit=11 len=0`,
userspot idle pull 155 (`hit=173`) 0 jumps, 45 s route 0 events/jumps,
camera_smoke ALL PASS, exit 0/DONE.
Also corrected a doc/code drift: B11 hit stabilization is opt-in on main
(`RC_CAM_HITWIN` default 0), not the C1d "shipped 0.25".
**Re-open criteria:** the native `FilterCamera` winding rule is recovered, or
double-sided probes cause over-pulling somewhere (kill switch `RC_CAM_BACKFACE=0`).
**Links:** `docs/camera/PENETRATION_PLAN.md` progress log 2026-09-29 (camclip);
`docs/camera/HOST_DEVIATIONS.md` B11/B14; logs `reborn_20260929_122635`
(before) and `_123534`/`_123619`/`_123704`/`_123851`/`_123947` (after) in
`bin64\reborn_out`.

### 2026-09-29 — camera — client-truth max defaults (follow distance 2000 u, 广角 60°)
- Did: user decision "default both to true max = client truth": the character
  follow distance default moved from the invented 6 m placeholder to **2000 u
  (20 m)** - the client's own maximum (`VideoSettingPanel.fMaxCameraDistance`
  default 2000, engine cap 2000, `JX3RepresentX64` const blob `0x180d2af90`);
  广角 default stays the client's panel maximum **60°** (log source renamed
  `client-max-default`; the engine `fMaxCameraAngle` cap is not recoverable in
  this install). Also carried the session build tag (`Camera Pen v3`) and the
  `obstdbg` instance/flag logging.
- Evidence: `client/camera.json` character row, `CameraSystem.cs:101/129/142`,
  `RebornClient.cs:488/889`, `VideoSettings.cs`,
  `docs/camera/HOST_DEVIATIONS.md` C6/C10;
  `camera_smoke_wallclip.exe` **ALL PASS (26 checks**, incl. the new
  "distance default = client max 2000 u"); live log
  `reborn_20260929_162023.log`: `CameraSystem ready: ... dist=2000u`,
  `fov source=... angle=60.00`.
- Outcome: solved. Committed on `agent/camera-wall-clip`; **not pushed** (new
  main rule `ff67fff`: never push unless explicitly asked). The per-slug test
  client picks the config from `bin64\reborn_campen_v3\camera.json`.

### 2026-09-29 — camera — "zooms in while turning, returns when I stop" (B15)
**Problem:** user report: pressing W (running a slope/turning) the camera
continuously zoomed in "for no reason", then sprang back to the 2000 u default
after stopping.
**Tried:** reproduced with `RC_CAM_DEBUG/SHAKEDBG/OBSTDBG/PENDBG` (log
`reborn_20260929_163131`): the penetration recorder showed **0 events** and
`betweendbg` clear, `hit=-1` on every frame of the burst - so the pull was not
geometry. `jumpdbg` showed `offLen` collapsing 2050 -> 1520 u in ~60 ms at a
fast camera flick, then recovering over ~3 s.
**Outcome:** solved (fix landed in code; live feel check pending).
**Why:** the per-axis SmoothTime smoothing of the *rotating* orbit offset
shortens the vector through its chord; the obstruction state machine received
that shortened `offLen` as its desired length and, since `target < Distance`
applies immediately (the engine's no-threshold shortening rule), snapped the
camera in - then the 1.5/2.828 flex eased it back over seconds. Fix: probes +
`CameraObstruction` now use the **raw desired offset** (the candidate line the
engine queries); the per-axis smoothing stays once, on the resolved offset
(`rSm`). The double `camOffSmooth` stage was removed.
**Re-open criteria:** a fast flick still produces a resolved-length jump in
`jumpdbg` (`smstep` > 2 u), or the T2/T4 obstruction invariants drift.
**Links:** `docs/camera/HOST_DEVIATIONS.md` B15; logs `_163131` (before),
`_165430` (after); `client/RebornClient.cs`.

### 2026-09-29 — camera — "still happens": sprint row still targeted 6 m
- Did: user retest still showed "press W (Shift) -> camera zooms in, stop ->
  back". Log `reborn_20260929_165430` camdbg: `mode=sprint dist=1334 -> 905 ->
  660`, then `mode=character dist=1995` - the **sprint row** still carried the
  old 6 m placeholder, so Shift+W ramped the distance target 2000 -> 660 and
  back. (The B15 fix is visible in the same log: during a ~7 rad/s flick the
  raw `offLen` moves but the smoothed camera `sm` stays ~2018, no collapse.)
- Fix: sprint row `TargetDistance`/`InitCameraDistance` = 20 m in
  `camera.json` + `CameraSystem.DefaultRow`, same client-truth max as the
  character row. The sprint pull-back (+60 u) clamps at the 2000 u max.
- Evidence: `reborn_20260929_171501` (`CameraSystem ready: ... dist=2000u`),
  `camera_smoke_wallclip` ALL PASS. Outcome: solved pending user feel check.

### 2026-09-29 — controls — operation-modes implementation plan (CLASSICAL/JOYSTICK)
- Did: wrote `docs/controls/OPERATION_MODES_PLAN.md` (registered in the
  controls index): game truth per mode, input routing matrix, switch key
  (`F7` host + `RC_MODE` env; the real client switches via the
  `UISetting_Operation_Switch` panel and has no default hotkey), per-mode
  settings (`nCameraModeInClassicMode/JoystickMode`), phases P0-P4 and tests.
  Found that the client already has a partial joystick branch keyed off the
  **wrong** setting (`tCameraStatic.nCameraMode == 1`, the follow mode, at
  `RebornClient.cs:806-823`) - P1 moves it onto an explicit `OperationMode`.
- Evidence: `docs/controls/JX3_CAMERA_CONTROLS.md` §3,
  `controls/RESEARCH_RESOLVED_GAPS.md` §4-5,
  `proof/controls/ui_lua/OperationSwitch.joystick.txt`,
  `hotkeys_script.dump.txt`.
- Outcome: plan committed; implementation starts at P0 on request.
- Re-open: implement P0 when the user picks it up (no code changes yet).

### 2026-09-29 — controls — operation modes P0-P2 landed (F7 switch)
- Did: implemented the plan: `CameraOperationMode` pure gating model +
  `CameraSettings.OperationMode` with `RC_MODE`, `F7` toggle (host key; real
  client switches via the UI panel), `op=` in `camdbg`, `OPMODE=` fingerprint;
  moved the always-rotate/cursor-lock branch off `tCameraStatic.nCameraMode`
  onto the operation mode; joystick disables the RMB body-turn (body follows
  the movement heading). Per-mode follow values (`nCameraModeIn*Mode`) are
  parsed + logged; applying them and the turn-rate model stay open (P2/P3
  partial). Controls register C12 -> PARTIAL.
- Evidence: `client/CameraSystem.cs` (`CameraOperationMode`),
  `client/CameraSettings.cs`, `client/RebornClient.cs`;
  `camera_smoke_wallclip` ALL PASS (29 checks: mode gating + parse);
  `RC_MODE=joystick` run `reborn_20260929_175413.log` (`op=joystick`, DONE,
  exit 0); test client rebuilt (`reborn_client_campen_v3.exe`, merged tree +
  ported edits, log `reborn_20260929_175819.log`, `OPMODE=classical`).
- Outcome: P0-P2 solved (P2 partial), P3 partial, P4 open.
- Re-open: turn-rate model + follow-mode semantics + reset-speed application.

### 2026-09-29 — camera — default follow distance corrected to 1245 u (cap vs initial)
**Problem:** after the "max default" change the camera terrain-pulled constantly
("zooms in for no reason"): `jumpdbg src=[probe4 ... terr=1934->1365]`,
`obstdbg probe0 ... terr=671` at rest, `clamp=1` - the native 5-probe query
pulling because a 2000 u camera sits at ground level behind the player.
**Tried:** traced the original game's distance sources in the disassembly and
shipped data instead of guessing again.
**Outcome:** solved (default changed to 1245 u; exact initial value still not
provable from local data).
**Why:** `fMaxCameraDistance` (default 2000, custom.dat) is only the zoom-out
cap - `SetCameraMaxDistance` `0x180ace520` clamps and writes the camera node's
cap field `+0x74`/`+0x8C`, never the current distance. The character camera
reads **no** `InitCameraDistance` (string xrefs only in the air-combat
`0x180ac9db5` and carrier `0x180b1c876` loaders). The distance is not persisted
either (`g_Scene_tCameraRuntime` = yaw/pitch/eyeScale only). The only shipped
camera-distance number is `number.krl CameraMaxDistance = 1245 u`, loaded into
`CommonNumber+0x98`; no reader of that field was located in this build (same
as the documented legacy `NearByWallDistance`), so 1245 is the best
client-authored value but the exact initial distance remains unproven until
the CDN `camera_common.krl.txt` row is obtained.
**Re-open criteria:** a `CommonNumber+0x98` reader is found (proves/refutes
1245), or the CDN per-mode row arrives.
**Links:** `docs/camera/HOST_DEVIATIONS.md` C10; `proof/netcode/disasm/
camera_maxdistance_xrefs.txt` (loader store at `+0x98`),
`camera_sLoad_calls.txt`, `camera_wall_obstruction.txt` §11;
`proof/gravity/number.krl.txt`. Code: `camera.json` (character + sprint rows),
`CameraSystem.cs`, `CameraSmoke.cs` (`distance default = client number 1245 u`),
live log `reborn_20260929_181215.log` (before), `camera_smoke_wallclip` ALL
PASS after.

### 2026-09-29 — camera — close-camera character hide threshold (B1 fix)
**Problem:** user report: dragging the camera up close shows the **inside of
the character**; the game hides/fades the character at some point (like "too
close") instead.
**Tried:** re-read the original-design evidence (`CLOSE_RANGE_RESEARCH.md` §2:
no explicit hide in the camera path; the effect is view near-plane clipping +
the engine's model fade, measured at camLen ~36..96 u in HANDOFF §4) and
inspected the host park-below hack.
**Outcome:** solved (threshold fixed; true exit remains the near-plane setter).
**Why:** the hack hid the dummy when camera->chest-anchor distance < **90 u** -
exactly the head offset (~90 u above the chest anchor). A camera sitting inside
the head hovers at camDist ~90, so the hide never fired and the inside stayed
visible. Raised the hide threshold to **105 u** (restore still >150 u) and added
`hideNear hide/show camDist=` logs. Host approximation registered under B1.
**Re-open criteria:** the view near-plane setter lands (C7/B1 exit) and the
park-below hack can be deleted.
**Links:** `docs/camera/HOST_DEVIATIONS.md` B1; `docs/camera/CLOSE_RANGE_RESEARCH.md`
§2; HANDOFF §4 ladder; test client `reborn_client_campen_v3.exe`
(`6b55d5b+modes+hide105`, log `reborn_20260929_203437.log`).

### 2026-09-29 — engine — D6 trampoline verdict: BEX64 side effect even single-instance
- Did: the test client ran with `RC_PATCH_D6=1` (single instance, `patchD6 rc=0
  cave=0000000170EE0000`); while walking "down" the app died. Event log:
  `c0000005`, **module unknown**, fault offset `0x7FFE70EE0000` - the documented
  BEX64 jump-to-data pattern (earlier occurrence `0x7FFE622B0000` with three
  overlapping clients), i.e. the bail path's skipped cleanup corrupting state.
- Evidence: Windows Application Error event; run log
  `reborn_20260929_203437.log` (idle at 20:43 before the fault);
  `docs/camera/HOST_DEVIATIONS.md` D6 updated.
- Outcome: the trampoline trades D6 for a rarer but fake crash; it stays
  **off by default** and must not be re-enabled until the bail path is fixed
  (proper local cleanup) or the missing editor DataStores/material root cause
  is fixed. Test client restarted with `PATCH_D6=0` (log
  `reborn_20260929_204449.log`).
- Re-open: fix the trampoline's bail path, or isolate/preconvert the missing
  materials (D6 exit).

### 2026-09-29 — engine — D6 dump analysis + hide-thrash correlation
- Did: parsed the WER crash dump
  `%LOCALAPPDATA%\CrashDumps\reborn_client_campen_v3.exe.30400.dmp` with a new
  stdlib tool (`tools/camera/minidump_exc.py`, registered in the camera README).
- Found: fault `KG3DEngineDX11EX64+0x11D03B6`, **`rsi=0`** (the null
  material/store pointer), worker thread `tid 36460`; stack candidates inside
  `KG3DEngineDX11EX64` (e.g. `+0x1F6D790`, `+0xBF77A0`, `+0x745A89`) and the
  editor host wrappers `KG_EngineEditorX64+0x26F6B/+0x2725E`; no asset path on
  the stack (register pointers are objects, not strings).
- Log correlation: in the seconds before the fault the camera was at extreme
  pitch (`-1.553`) pulled to 70 u, and `hideNear` **thrashed**
  (`hide 104.9 -> show 150.1 -> hide 105.0 -> show 150.5`); every `show`
  re-adds the dummy model, i.e. engine content churn on the streamed-content
  path that AVs.
- Mitigation: show threshold 150 -> **250** (hide 105 unchanged) to cut the
  churn; D6 root cause (missing editor DataStores / null material) stays open.
- Evidence: log `reborn_20260929_204449.log` (+ hide lines), tool output saved
  in the session; new build `reborn_client_campen_v3.exe` (log
  `reborn_20260929_205411.log`). Outcome: partial (risk reduced, root open).

### 2026-09-29 — camera — faithful-mechanism recon for close-camera hiding
**Problem:** the user called out the park-below threshold tuning as a band-aid;
the real game uses the view near plane + engine model fade (CLOSE_RANGE_RESEARCH
§2), so find an engine-authored mechanism instead of tuning thresholds.
**Tried:** (a) dumped the managed wrapper API (`RC_API_DUMP`): only
`KGEngineCLR.SetMainPlayerType/GetMainPlayerType` exist, no near-plane/visibility
API; (b) A/B `SetMainPlayerType(0/1)` at T1 with park-below off and the camera
pulled to len=0 - the engine culls the character identically in both, and the
one crash seen in the first mp0 run did not reproduce (3 runs each, all exit 0);
(c) engine exports: `KG3D_SceneObject::SetPlayerObject/IsPlayerObject` /
`SetMainCharactor`, camera-property schema `NearPlane/FarPlane/
AutoComputeClipPanes` (consumed by `KG3D_Engine::CreateCamera`), no near-plane
setter export.
**Outcome:** partial - the main-player route is not the mechanism; the near
plane remains the faithful target (camera/view vtable projection block).
**Why:** the engine already culls the model at len=0 natively; the residual
"see inside" band is where the host approximation sits.
**Re-open criteria:** locate the camera/view projection near-plane field or the
`KG3D_CAMERA_PROPERTY` write path, then delete B1.
**Links:** `docs/camera/HOST_DEVIATIONS.md` B1; harness knobs
`RC_API_DUMP`, `RC_MAINPLAYER`, `RC_CAM_PITCH/RC_CAM_YAW`
(`client/RebornClient.cs`); A/B screenshots `%TEMP%\campen_ab\t1_mp{0,1}.png`.

### 2026-09-29 — engine — D6 root cause pinned: missing `RCPI_Scene` registry entry
**Problem:** the user crashed again (same D6 `+0x11D03B6`) after running ~4 s
and stopping with the camera <105 u; asked to reproduce and find the cause.
**Tried:** WER dump `...30272.dmp` -> `tools/camera/minidump_exc.py`; then full
`dumpbin /disasm` of `KG3DEngineDX11EX64.dll` and targeted decompilation of the
faulting function.
**Outcome:** root cause identified (fix path clear, not yet implemented).
**Why:** fault function RVA `0x11CE760` (worker/streaming path) lazily FNV-hashes
the named context string **`RCPI_Scene`** (RVA `0x21CB2A8`), looks it up in a
global registry, and on lookup failure stores `rsi=0` then dereferences without
a null check (`mov rbx,[rsi]` at `+0x11D03B6`). The editor host's registry lacks
that entry - the documented missing build-machine content. The in-function
failure cleanup at `0x1811D0778` frees locals that are not yet initialized at
the crash point, so the old trampoline (skip-to-epilogue) was guaranteed to
corrupt (BEX64).
**Re-open criteria:** locate the `RCPI_Scene` registrar (or trigger its
creation) and init it in the host; otherwise write a patch that performs the
function's own partial cleanup before returning failure.
**Links:** `proof/netcode/disasm/d6_rcpi_scene.txt`; `docs/camera/
HOST_DEVIATIONS.md` D6; dump `%LOCALAPPDATA%\CrashDumps\reborn_client_campen_v3.exe.30272.dmp`;
`tools/camera/minidump_exc.py`.

### 2026-09-29 — engine — D6 FIXED: seed the engine's uncomputed `RCPI_Scene` hash
**Problem:** the app kept AVing at `KG3DEngineDX11EX64+0x11D03B6` (`rsi=0`) in
interactive play; earlier work only diagnosed it (missing `RCPI_Scene`
registry lookup).
**Tried:** (a) live-context VEH in `camera_shim.dll` (`RC_D6DBG=1`) that dumps
registers + the registry graph at the exact fault site; (b) a synthetic input
driver (`tools/camera/drive_client.ps1`) to reproduce the interaction-only
crash unattended; (c) seed the engine's lazy hash value from the shim.
**Outcome:** solved (default-on).
**Why:** the VEH capture showed `[rbp-70h]=0` at the crash - the engine's
per-function lazy FNV-1 hash of `RCPI_Scene` (slot `base+0x2D5BBD0`) was never
computed: the guard gate `cmp [guard], tls; jg init` (`0x1811D0335`) let the
worker through before the init ran, so the map lookup used key 0 and the
null result was dereferenced without a check. `camera_shim.dll` now writes the
exact hash the engine's own loop would compute (`0x392E0BFA0428F080`) into the
slot at load (`RC_D6Seed`, no code patch, `RC_D6SEED=0` opts out).
**Evidence:** driven interactive A/B at the crash spot: baseline
(`RC_D6SEED=0`) **2/2 crashes** with `key=0` captures; seeded **2/2 alive**
(240 s and 200 s driver runs).
**Re-open criteria:** a crash at another lazy-hash site (VEH will capture it),
or the engine's own init starts running (verified build).
**Links:** `docs/camera/HOST_DEVIATIONS.md` D6; `native/camera_shim.cpp`
(`RC_D6Seed`/`RC_D6Dbg`); `tools/camera/drive_client.ps1`;
`tools/camera/minidump_exc.py`.

### 2026-09-29 — movement — 二段跳 research + client jump chain (reborn_client_double_jump)
- Did: decoded the double-jump press model from `KCharacter::Jump` (chain gate
  `jumpCount >= MaxJumpCount[school]` 0x140313B19, row = current count at
  0x140313B20, Represent `DOUBLE_JUMP` state + `GetDoubleJumpEndOffset`),
  extracted the full per-school chain (`--chain`; J0 invariant 40,90,11, J1 is
  the 二段跳 signature, gravity clamp 0..31 matters for schools 10/11),
  reproduced it in `client/JumpTable.cs` + RebornClient (J0 unchanged, J1+ per
  press, landing reset, reject log), and added isolated feature-build naming
  (`RC_CLIENT_EXE`, own memory namespace + guard, per-exe build_info).
- Evidence: `tools/gravity/parse_jump_tables.py --chain`; verify_model §6;
  in-engine runs in `proof/gravity/double_jump_reborn_run.txt` (A/C clean:
  `djb press n=2` -> `djb land n=2` -> DONE; B/D reproduce the
  `f1b02yd二段跳a.tani` AV, so the tani is opt-in and the client defaults to the
  jump clip); commits `e261499`, `c937973`.
- Outcome: solved. Open: segment-end End-triple trigger, JumpFrameParam curves,
  fly-state re-press path, tani AV.

### 2026-09-29 — movement — 二段跳 corrected: flip mode (user feedback), tani vs ani
- Did: user reported the chain-mode double jump (J1 ballistic, 11.7 m) as "way
  too high / wrong action". Confirmed via MIN2 that the jump clips are in-place
  (bip01 Y=0 all frames), so the arc is physics; the J1 row is the 轻功 flight
  chain and its takeoff-burst/End-phase trigger is still undecoded. Implemented
  the plain 二段跳 as `RC_DJUMP=flip` (provisional): the air press re-uses the
  J0 triple (one extra normal jump, max 2) and plays the authored
  `f1b02yd二段跳a.ani`; kept the raw table chain as `RC_DJUMP=chain` for research.
- Evidence: `proof/gravity/double_jump_reborn_run.txt` run E (exit 0; press n=2
  at first-jump apex y=1008 -> land y=646, ~1.9 m extra; `clip -> ...二段跳a.ani
  (0)`; reject n=3), runs B/D (tani wrapper AV 0xC0000005). Doc §1/§4/§5 updated.
- Outcome: solved provisionally; re-open when the `ModifySprintEndSpeed` trigger
  is decoded (then use J1 burst + End triple instead of the J0 re-use).

### 2026-09-29 — movement — 二段跳 unit calibration: RC_JUMP_SCALE=0.52
- Did: user still reported the flip jump as "way too high" and asked to make the
  unit research work. Per `UNIT_SCALE_AND_CHARACTER_SIZE.md` / `JX3_COLLISION_SYSTEM.md`
  G-0 (canonical 1 u = 1 cm), the raw `J0` triple gives a 368 u = 3.68 m apex,
  ~2x the spec's in-game 1.92 m (`REBORN_JUMP_FALL_SPEC.md`; MapSpike used
  `703 u/s`, `1289 u/s^2`). Added `RC_JUMP_SCALE` (default 0.52 = 100/192)
  scaling takeoff AND gravity together, so `J0` realises 1.92 m with the 1.09 s
  air time; applies to flip and chain modes.
- Evidence: `proof/gravity/double_jump_reborn_run.txt` run F (exit 0):
  `jump: mode=flip school=0 scale=0.520 (apex 191u)`; press n=2 at y=835 after
  press n=1 at y=646 (191 u = 1.91 m); double-jump apex ~3.8 m. Client log line
  added at startup.
- Outcome: solved. Raw-table mode still available with `RC_JUMP_SCALE=1`.

### 2026-09-29 — client — standing-jump stutter: model transform gate ignored Y
- Did: user reported a stationary jump stutters/"sticks in the middle" (both
  jumps), while holding W looked correct. Root cause: the model-update gate
  (`RebornClient.cs`) only re-placed the dummy when X/Z or yaw changed, so a
  standing jump (py only) never moved the model; with W held the X/Z deltas
  masked it. Added Y to the gate + `lastModelY`, and an apex sample log
  (`djb apex n= py= modelY=`) so the fix is numerically checkable.
- Evidence: `proof/gravity/double_jump_reborn_run.txt` run G (exit 0):
  `djb apex n=2 py=1023 modelY=1022` (model tracks physics; before the fix the
  model stayed at the y=646 takeoff height).
- Outcome: solved.

### 2026-09-29 — controls — camera-relative steering + turn model (S6), client renamed
- Did: user reported RMB drag could not turn the character while walking. Per the
  control notes (`JX3_MOVEMENT_CONTROLS.md` §2; `CAMERAORSELECTORMOVESTICKY`
  rotates camera **and** character; gap register S6) the client held the world
  direction while the key set was unchanged, so camera rotation never steered.
  Removed the latch (movement direction recomputed camera-relative every frame),
  added the `RunTo` turn model (heading vs facing; facing turns at the π rad/s
  host fallback; a >112.5° turn halves speed and turn step), reused the rate in
  the RMB turn, and renamed the feature build to
  `reborn_client_double_jump_control.exe`.
- Evidence: `proof/controls/steering_run.txt` (demo camera orbit at t=6s:
  `dir=(0,1)` → `(0.78,0.62)`, `yaw` 0.00 → 0.90, path curves; exit 0);
  `CONTROLS_GAP_REGISTER.md` S6 → DONE; `JX3_MOVEMENT_CONTROLS.md` §6 updated.
- Outcome: solved for the steering rule; the server `+0x48` per-frame turn step
  is still undecoded (host π rad/s fallback documented).
### 2026-09-29 — ability sandbox — 临时飞爪: phantom vertical probe was the Z "half way"
**Problem:** 临时飞爪 pulls stopped mid-height ("z change seems to have a limit").
**Tried:** removing the 1200-u climb cap and the fixed 3.5-s pull deadline (helped but not
the real cause); raising the game-mask vertical probe start (10000 → 30000) produced a
bogus 29083-u "top" and the pull landed floating in the sky; a 2-float `posXZ` probe shape
returned `hr=0` everywhere (inconclusive by itself).
**Outcome:** solved.
**Why:** the game-mask vertical probe (`KG3D_SpaceManager::RayIntersectionVertical`,
`EngineRay.RayVerticalHeight`) returns **phantom collision heights** — at plain dune columns
it reports ~9045 u / ~29122 u while scene/terrain descent rays report 878 / 872 / sampler 868.
Targets stacked a 1200-u cap and a 10000-u start on top of that, so pulls stopped at
ledges/phantoms. Fix: resolve the target/floor from `visibleTop(x,z)` = max(first `RayScene`
hit cast straight down from y=40000, baked terrain sampler) — the authored visible surface —
no cap; pull budget is 3-D distance based; the game-mask probe is retained only for the
camera-obstruction ladder. Indicator: the original `鼠标移动.Sfx` / selection SFX cannot be
played by MovieEngineCLR (AV), so the aim point now shows only the authored
`释放_范围选择01.mesh` range-select ring at a visible scale (`SB_FEI_RING_SCALE`, default 4)
— no unrelated assets.
**Re-open criteria:** if the engine pick ray / an SFX play path is wired, use them instead.
**Links:** commits on `agent/skillv2-sandbox`; climb log 2026-09-29 16:51
(`marker (24534,2919,21524) climb=2158` → `landed at (24534,2919,21524)`);
`docs/movement/JX3_COLLISION_SYSTEM.md` §16.4 + G-20.

### 2026-09-29 — Skill sandbox — rename to Skill + authored PSS hint-circle indicator
- Did: renamed the sandbox client to **Skill** (`bin64\Skill.exe`, window title "Skill",
  runtime dir `bin64\Skill\`, memory namespace `Skill.memory`, brand `Skill`); replaced the
  white raw range-select mesh marker with the client's authored ground indicator PSS
  `data\source\other\HD特效\其他\Pss\t_提示圈圆_6尺黄_贴地.pss` — the yellow 6-尺
  ground-hugging hint circle at authored size/color, scale 1 (`SB_FEI_RING_SCALE` override).
- Why: the range-select `.Sfx` cannot be played by the host (`AddDummyModel` AVs;
  `AddStateMachineModel` E_FAIL) and the raw mesh ships without material/texture (untextured
  white). PSS dummies do play (asset_sandbox recipe) and the client's ground-target look is
  the 提示圈 family — verified side-by-side (`t_提示圈3尺[_绿|_高度25_黄]`,
  `j_姜棠目标圈_6尺`, `y_雨轻红指示圈6米`, `z_治疗提示圈`).
- Evidence: `Skill_20260929_181018.log` + `rc_00_7000ms.png` (7 candidate PSS, colored rings);
  final aim run `Skill_20260929_181543.log` + `rc_00_3500ms.png` (`scale=1.0 marker=<h>`,
  yellow circle at the aim point); `ability_sandbox\build.cmd` → `bin64\Skill.exe`.
- Outcome: solved (indicator size+color are now the authored ones).
- Re-open: if a native SFX path (`KG3DScene::GetSceneSFXEditor` / `KG3DSFX`) is wired, play
  the true `鼠标移动.Sfx` cursor effect and the `Selection_ShowSFX` selection effect too.

### 2026-09-29 — Skill sandbox — CORRECTION: PSS hint circle was the wrong resource; roof Z fixed
- Did: (1) reverted the indicator to the correct resource — the `释放_范围选择01` family
  (`data\source\other\特效\技能\mesh\释放\释放_范围选择01.mesh`, authored size, scale 1,
  `SB_FEI_RING_SCALE` override). The 提示圈 PSS was a wrong substitution and is removed.
  (2) Fixed the "target a rooftop, stop in mid-air and lock" case: the target/floor resolve
  now uses the **collision bake** (`FoliageCollision.Raycast` straight down from y=40000) max
  baked terrain — not `RayScene`, which hit non-collidable visuals and let `roofHold` lock the
  player floating. (3) Widened the `SB_SCAN` grid to ±3000/±4200 u to find roofs.
- Why the mesh still "doesn't display right": the engine falls back to the **error material**
  (dark/red; washes out on bright ground, visible only against dark surfaces) because the mesh
  has no material in the VFS; the authored glow/textures live in the `.Sfx` emitters, which the
  host cannot play (AddDummyModel AVs; AddStateMachineModel E_FAIL). A faithful display needs
  the engine effect path (`KG3DScene::GetSceneSFXEditor` / `KG3DSFX`) — not wired.
- Evidence: roof/rock column (26034,24524) → target Y=1168, `landed at (26034,1168,24524)`
  standing on the collision top (`Skill_20260929_1844*.log`, `rc_01_9000ms.png`); tall rock
  (24534,21524) → collision top 2886; marker crop at scale 1 shows the error-material pattern
  (`crop_mesh1x.png`, temp). `build_candidates.py` compiles.
- Outcome: Z roof case solved; indicator resource restored, display remains host-limited.
- Re-open: native SFX/effect path.

### 2026-09-29 — Skill sandbox — 如意法 (32247): first dataset-driven ability cast
- Did: added 如意法 to the ability panel (P) and a **dataset-driven process runner**
  (anim / sound / dummy steps read from `ability_candidates.json`, generic loader shared
  with 飞爪); process added to the tracked dataset, the runtime copy, and the `PROCESS`
  dict in `build_candidates.py`. Steps: base `.ani`
  `data\source\player\f1\动作\f1smj10双刀buff04.ani`, sound 75054615
  (`riyuejiaohui.wav`), authored PSS
  `data\source\other\hd特效\技能\pss\发招\m_明教清净心01.pss` (spawns at the caster and
  follows movement).
- Why the base `.ani`: the matched tani `F1smj10双刀buff04_清净心01.tani` embeds `.Sfx`
  tags (`m明教元素18/19.sfx`, `g光晕02.sfx`, `释放_气场聚集03.sfx`) — playing it AVs the
  host in `KGEngineCLR.Render()` (exit `0xC0000005`, `SB_RUYI_NOPSS=1` A/B proved the PSS
  was not the cause); its base `.ani` is tag-free and plays.
- Evidence: `Skill_20260929_2048*.log` — cast → anim/sound/dummy → PSS handle →
  `ruyifa done` → `DONE`, exit 0; `rc_00_2500ms.png` (authored fire-pillar PSS on the
  caster), `rc_01_4500ms.png`.
- Outcome: solved — first generic, data-driven ability cast (no special-casing).
- Re-open: none (the PSS runs its own authored duration; add a `状态` buff-PSS step when
  staging the sustained part).

### 2026-09-29 — Skill sandbox — 如意法 timing from authored data (939 ms anim once, 12.48 s PSS)
- Did: the fixed 3 s cast window looped the 939 ms base `.ani` ~3x and cut the PSS at 3 s.
  Timings now come from the authored data: anim step `durMs=939` (31 f @ 33 fps, MIN2) and
  PSS step `durMs=12480` (max emitter `DelayTime+DurationTime`, `RepeatTimes=1`); the runner
  ends the skill clip at the anim length (idle resumes) and the PSS dummy at its authored
  life. Dataset + `PROCESS` updated; `ProcStep.Dur` parsed.
- Evidence: `Skill_20260929_2112*.log` — `ruyifa cast: animMs=939 pssMs=12480`, clip returns
  to idle at +0.94 s, `ruyifa done` at +12.6 s; shots `rc_00_2500/rc_01_5000/rc_02_11000ms.png`.
- Outcome: solved (intermediate; the whole staged-process runner is slated for deletion in the
  "engine effects online" milestone — the engine should play the tani's own tags).
- Re-open: engine tag-SFX path online.

### 2026-09-29 — Skill sandbox — .Sfx tag AV root cause: stale MovieEditor engine build
**Problem:** playing a tani with `.Sfx` tags AVs the host in `KGEngineCLR.Render()`
(`0xC0000005`); PSS-tagged tanis play fine.
**Tried:** editor init mirrored (KG3DSoundCLR.Init, SetActorCreateOption, rtxradius command)
— no change; actor path (KGMovieActorCLR + AppendModel) crashes identically → not the model
type; loose-file loading works (absolute paths accepted), so the ruyifa tani was copied to
temp and patched per Sfx path (paths zeroed in place).
**Outcome:** root cause identified (environment, not our code).
**Why:** per-file isolation on the patched tanis: all-Sfx-zeroed = clean; only
`m明教元素19.sfx` / `g光晕02.sfx` / `释放_气场聚集03.sfx` = CRASH; only `m明教元素18.sfx` =
clean; another tani's single Sfx (`d001014伞开灰.sfx`) = clean. The engine builds differ:
MovieEditor `bin64\KG3DEngineDX11EX64.dll` = **2026-09-14** (41,512,880 B) vs client
`zhcn_hd\bin64\KG3DEngineDX11EX64.dll` = **2026-09-27** (41,492,920 B, SHA256 `9B49…AA99`).
The crashing Sfx use emitter blocks authored for the newer engine; the 09-14 build AVs on
them. (The client-bundled MovieEditor is older still, 2026-04-28.)
**Re-open criteria:** update the canonical MovieEditor install to the engine build matching
the client (rule 6: use the matching engine, no workarounds), then replay the ruyifa tani —
all tags should fire. Editor config to adopt when initializing the host
(`MovieEditor\MovieEditorConfig.xml`): `AniPlayMode=ADDCURRENT_CIRCLE`,
`ActorCreateOption=APEX|CLIENT_OBJECT`, `EnableModelAsyncLoad/MapAsyncLoad=True`,
`RtxRadiusMode=HIGHT`.
**Links:** temp variants + run logs in `%TEMP%\opencode\skillv2\` (`ruyi_no_sfx` clean,
`ruyi_only19/02/03` crash, `ruyi_only18` clean); `SB_ACTOR_TEST` hook in
`ability_sandbox\rb\RebornClient.cs`.

### 2026-09-29 — Skill sandbox — generic dataset-driven cast runner + 5 more abilities
- Did: generalized the 如意法-specific runner into one **dataset-driven cast runner**
  (`loadCastAbility` / `castSteps` / `castActive`), and made the P panel list every
  dataset ability that has a staged process. Staged 5 more abilities in the tracked
  dataset + `PROCESS` in `build_candidates.py` (anim length from MIN2, wem from the
  confirmed list, PSS life from the emitter max `DelayTime+DurationTime`):
  五蕴皆空 (576 ms + 157383905), 凌太虚 (620 ms + 45472664),
  撼如雷 (939 ms + 22651465 + `t_天策撼如雷02_重制.pss` 8400 ms),
  傍花随柳 (1697 ms + 7390771 + `w_万花蓄力脚下.pss` 14880 ms),
  天地无极 (1394 ms + 252612285). The base `.ani` is played (not the tani): the tanis
  embed `.Sfx` tags and the MovieEditor engine build is stale (see entry above).
- Also fixed dataset hygiene: the generator now attaches `process` **only to the
  resolved row** of each name (the dataset carries duplicate rows, resolved + empty;
  the host takes the first resolved row). The previous hand-patch (stage5.py) had the
  same semantics, but the generator re-added steps to unresolved rows — now regenerating
  the dataset reproduces the committed file (only newer MECH/note text differs).
- Evidence: smoke logs in `%TEMP%\opencode\skillv2\` (`run5_1` 五蕴皆空, `run5_2` 凌太虚,
  `run5_3` 撼如雷, `run5_4b` 傍花随柳 steps=3, `run5_5` 天地无极); regenerated runtime
  dataset verified in an interactive session
  (`MovieEditor\bin64\Skill\out\Skill_20260929_234219.log`: 傍花随柳 steps=3
  animMs=1697 pssMs=14880 + PSS handle, 凌太虚/天地无极 casts, `DONE`).
  `py_compile` + `build_candidates.py` regen: 383 abilities, same counters as the
  committed dataset; A/B field compare showed only the 2 newer doc-text rows differ.
- Outcome: solved — 7 abilities now cast from authored data through one generic runner.
- Re-open: engine tag-SFX path (stale MovieEditor build); when it is updated, the tani's
  own `.Sfx` tags replace the staged `.ani`+PSS approximations.

### 2026-09-30 — Engine host — client-stack pivot: recon + mixed-host A/B (negative)
- Did (per user direction: answers from the game client, not MovieEditor): (1) recon of
  the client `zhcn_hd\bin64` stack — client adapter exports the same 4 host entry points
  as the MovieEditor adapter, engine exports the full `KG3D_Engine` class (1959 exports),
  and `KG_MovieEngineX64.dll` (`KG_CreateMovieEngine`/`KG_GetMovieEngine`) is the native
  module the MovieEditor adapter dynamically loads by name (client-only; MovieEditor
  pairs `MovieEngineCLR` + `KG_EngineEditorX64` instead); the client adapter's manager
  code is byte-identical at the same RVAs as the MovieEditor one. (2) Mixed-host A/B:
  temp copy of `MovieEditor\bin64`, swapped 30 client engine/tag/plugin DLLs, new
  `RC_BIN64` engine-dir override in the host.
- Result: **negative** — control (ME modules) AV `0xC0000005` right after the tagged
  ruyifa tani starts (`play=0`); treatment (client 09-27 engine) fast-fails `0xC0000409`
  in `KG3DEngineDX11EX64.dll` (offset `0x18c82c4`) at the same point. The client engine
  *was* loaded (`CameraShim: engine build mismatch`; WER timestamp matches), and the
  engine stdout shows a missing VFS resource (`foliage/blendmap/clusterinfo.json`).
  Conclusion: the editor shell is not a faithful host; the `.Sfx` failure is
  host-context, not only the stale 09-14 build.
- Evidence: `engine_host_spike/recon_client_stack_exports.txt`,
  `recon_client_movie_disasm.txt`; run logs + WER in `%TEMP%\opencode\skillv2\`
  (`host_client_engine\...\Skill_20260930_002*.log`, `ce_treat3.out`); plan in
  `docs/engine_host/CLIENT_STACK_PIVOT.md` (registered in the area README).
- Outcome: pivot decided; next probe = native client-stack host (client adapter/engine/
  movie engine + client VFS, no MovieEditor/CLR), goal "ability id in, engine reads the
  authored data itself".
- Re-open: native probe result; if it passes, retire the staged anim/sound/PSS playlists.

### 2026-09-30 — Engine host — client stack boots read-only (native probe, step 1)
- Did: temp copy of the client `bin64` + a temp client root (install untouched); native
  probe (`client_boot_probe.cpp`, temp) loads the client `X3DEngine.dll` with
  `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR` and calls the client's own init facade.
- Result: `PreInitX3DEngine -> 1`, `LoadX3DEngine -> 1`; loaded modules = `X3DEngine.dll`
  + `KG3DEngineAdapterX64.dll` only. The 3D engine (`KG3DEngineDX11EX64.dll`) and movie
  engine (`KG_MovieEngineX64.dll`) do **not** load from the facade alone (polled 60 s);
  `GetK3EngineMgr` does not trigger it either. Manually loading `KG_MovieEngineX64.dll`
  and calling `KG_CreateMovieEngine(out)` creates the movie-engine object (returns in
  `AL`, not the int) and `KG_GetMovieEngine()` returns it — the engine is created lazily
  when the movie engine is driven.
- Evidence: probe logs in `%TEMP%\opencode\skillv2\` (`probe_stdout.txt`), plan doc
  `docs/engine_host/CLIENT_STACK_PIVOT.md` (updated with the boot table).
- Outcome: client stack is hostable read-only; next = map the movie-engine object's
  3 vtables from `MovieEngineCLR` IL/disasm (`engine_host_spike/recon_il.txt`) and drive
  engine/scene/actor/animation, then replay the tagged tani.
- Re-open: engine-load trigger + movie-engine vtable map.

### 2026-09-30 — ability sandbox — merge main + run on the mini sandbox map
- Did: merged `main` into `agent/skillv2-sandbox` (mini-sandbox tooling + latest client
  features; conflicts: EXPERIENCES append-merge, ability_sandbox window title resolved to
  `sandbox-ability` per main's naming rule). Rebuilt `Skill.exe` and added
  `ability_sandbox/run_sandbox.cmd` (RC_MAP = the 1x1 cropped map, mirroring
  `tools/sandbox/run_sandbox.cmd`).
- Evidence: run log `Skill_20260930_1432*.log` — `LoadMap result=0`,
  `TerrainSampler: size=512 regions=1x1 cell=100 origin=(0,0)`,
  `spawn=(23334,761,24224)`; map built by `tools/sandbox/build_sandbox.py`.
- Outcome: the skill sandbox runs on the mini sandbox scene (sandbox + skills replaces
  full-client + skills for feature work).
- Re-open: none.

### 2026-09-30 — ability sandbox — cast cooldown: one press = one cast
- Problem: an ability cast (e.g. 撼如雷) could be re-triggered by spam presses; every
  press restarted the animation and spawned the effect again (the user saw the animation
  "repeatedly happen").
- Fix (host rule, C# only): 3 s cooldown from the moment a cast commits, applied to every
  cast path (dataset abilities, 风来吴山, 临时飞爪 confirm). Same-ability recast is
  also blocked while its effect is still live (`castActive`), and a new cast explicitly
  removes the previous `cast_pss` dummy. Gated presses log `cast blocked: cooldown Ns` or
  `cast blocked: effect still playing`.
- Evidence: scripted spam run `Skill_20260930_1451*.log` — cast once at 48.385 s; presses
  at +1.0 s / +2.1 s blocked (cooldown 2 s / 1 s), +3.1 s blocked (effect still playing);
  `cast done` at +8.5 s; exit 0. Sandbox map: `regions=1x1 origin=(0,0)`.
- Outcome: solved — one press plays the animation/effect exactly once.
- Re-open: none (the authored PSS may contain its own repeating pulses; that is data).

### 2026-09-30 — ability sandbox — cast effect anchored (one spawn, timeline once)
- Problem: the user still saw the cast "animation" happen multiple times (reported after the
  3 s cooldown). Verified with logs + screenshot fingerprints: the character clip is set
  once per cast (939 ms for 撼如雷) and returns to idle at +972 ms — one play; the PSS
  dummy is spawned once; the cooldown blocks spam presses. The remaining host artifact was
  the effect re-add on movement (the PSS dummy was re-added every >16 u to follow the
  caster, which can restart the authored timeline).
- Change: the cast PSS is now spawned ONCE at the cast point and never re-added on
  movement (anchored; the authored timeline plays once). The handle log now says
  `(once, anchored)`.
- Evidence: scripted run `Skill_20260930_1513*.log` — cast → clip once →
  `cast pss -> ... (once, anchored)` → `cast done` +8.5 s, with W held during the whole
  effect; screenshot series `rc_*_2000..3100ms.png` shows a single motion arc (pairwise
  diff peak mid-clip, no periodicity).
- Outcome: one cast = one animation, one sound, one effect instance.
- Re-open: if repetition persists, identify whether it is the authored PSS multi-pulse
  timeline (30 emitters, staggered delays up to 1.44 s over 8.4 s — authored data) or a
  specific visual the user can point at.

### 2026-09-30 — ability sandbox — P panel: client icons + hover desc + click to cast
- Did: new tool `ability_picker/tools/build_skill_data.py` pulls each ability's data straight
  from the client tables — `ui/Scheme/Case/Skill.txt` (Name/Desc/ShortDesc/SimpleDesc +
  IconID), `ui/Scheme/Case/Icon.txt` (IconID → `ui/Image/Icon/<FileName>.UITex` + frame),
  `settings/skill/skills.tab` (kind/school/cast mode/script) and `SkillRealization.tab`.
  Icons: the UITex names its texture (descriptor says `.Tga`, the shipped file is `.dds` —
  extension swap), extracted through the official `PakV4SfxExtract.exe`, cropped to the
  frame rect and written as PNG to `bin64\ability_picker\icons\<skillid>.png`.
  All 8 staged ids resolved: 65119 撼如雷, 65101 五蕴皆空, 65113 傍花随柳, 65138 凌太虚,
  65062 天地无极, 28031 临时飞爪, 32247 如意法, 27967 风来吴山.
- Panel: the P list is now owner-drawn (48 px client icon + name + kind/school), hover
  shows the client's own tooltip text (markup `<SKILL ...>`/`<BUFF ...>` stripped), and a
  click casts the clicked ability (same 3 s cooldown gate as the 1 key).
- Evidence: log `Skill_20260930_1617*.log` — `skill data: 8 abilities`,
  `ability panel shown`; desktop capture crop shows the icon list + hover tooltip;
  tool output `8 abilities, 8 icons`; tracked `ability_picker/data/skill_data.json`.
- Outcome: solved — the panel is client-data driven (icon/desc), click-to-cast wired to
  the verified cast path.
- Re-open: none.

### 2026-09-30 — ability sandbox — P panel icon grid + 6 more staged abilities
- Panel: the P list is now an icon grid (FlowLayoutPanel, 6 per row, 32 px client icons,
  no labels). Hover shows the client tooltip (description, markup stripped); a click casts
  the clicked ability (same 3 s cooldown gate). Replaced the owner-drawn ListBox.
- Staging: the next 6 abilities in the user's ID-list order, same pipeline as before
  (matched tani → base `.ani` duration + authored PSS life + wem):
  65161 鹊踏枝 (anim 1303 ms, PSS 10560 ms), 65165 雷震子 (394 ms, no PSS),
  65097 韦陀献杵 (1394 ms, PSS 9600 ms), 65048 夺命蛊 (348 ms, no PSS),
  65159 帝骖龙翔 (1394 ms, PSS 24000 ms), 65154 坐忘无我 (1394 ms, PSS 9600 ms).
  `PROCESS` updated in `build_candidates.py`; the regenerated dataset matches the staged
  one exactly (0 diffs). `skill_data.json` rebuilt for all 154 dataset ids (154 icons).
- Evidence: `Skill_20260930_1701*.log` — `skill data: 154 abilities`,
  `cast: 鹊踏枝 steps=3 animMs=1303 pssMs=10560`, anim/sound/pss
  `(once, anchored)`, `cast done`, exit 0; commits `3eff80e`, `a3ca2f2`.
- Outcome: solved — icon-grid panel + 14 staged abilities with client icons/desc.
- Re-open: none.

### 2026-09-30 — ability sandbox — caster-bound effects follow the caster
- Problem: 撼如雷 / 鹊踏枝 effects stayed at the cast point after the caster moved (the
  earlier "anchored" fix removed the follow).
- Diagnosis: the host plays the effect as a free-standing scene dummy
  (`AddDummyModel`); the client binds these effects to the caster socket through the tani's
  SFX tags (`KG3D_SFX_BIND_TYPE`), which the managed API does not expose and the stale
  engine AVs on. So the follow is a host approximation — the honest gap.
- Fix: restore follow-on-move (re-add the dummy when the caster moved > 32 u, throttled).
  The engine reuses the same dummy object (handle unchanged, no `re-added handle=` log —
  no timeline restart observed).
- Evidence: `RC_DEMO=1` walk during the effect (`Skill_20260930_1716*.log`, pos z
  24224 → 25187); screenshots `rc_00_3000/rc_01_6000/rc_02_9000ms.png` — at 6 s the bright
  effect centroid is x=634 vs screen centre x=640 after ~950 u of movement (an anchored
  effect would drift off-centre).
- Outcome: caster-bound effects follow the caster again; the faithful fix (engine socket
  binding / SFX track) stays the next engine-wiring step.
- Re-open: engine SFX bind (`CreateScreen3DSFX` / `KG3DSceneSFXEditor::NewSFX`).

### 2026-09-30 — ability sandbox — all remaining abilities staged (149 total) + SFX core recon
- Staging: a batched pipeline (matched tani → base `.ani` + first authored PSS + wem)
  staged **all 136 remaining** dataset abilities in one pass (0 missing): 136 tanis,
  130 anis, 113 PSS parsed; tracked + runtime datasets patched; the regenerated dataset
  matches exactly (0 diffs); 149 abilities now carry a `process`. The 8 missing sounds
  were fetched with `fetch_sounds.py`; `skill_data.json` rebuilt for all 154 ids (icons).
  Sample in-engine cast: 九转归一 — `animMs=816 pssMs=11520`, PSS `(follows caster)`,
  `cast done`, exit 0.
- Core recon (the deviation from the follow-up): both engine builds export the same SFX
  factories (`CreateScreen3DSFX`, `CreateSFXTrackData`, `DestroySFXTrackData`,
  `Get/SetAnimTagSystem`); the interfaces are pure virtual (vtable RE needed);
  `CreateScreen3DSFX` forwards to the SFX manager at `engine+0x2c10`, vtable slot
  `+0x68`. Plan + acceptance in `docs/engine_host/SFX_WIRING_PLAN.md` (registered in the
  area README): map the interface vtables, add a native shim, replace the `cast_pss`
  dummy path, then re-test the `.Sfx` tags.
- Evidence: `stage_all.py` (temp) output `abilities to stage: 136 … staged: 136 missing: 0`;
  `candidates_regen4.json` 0 diffs; `Skill_20260930_1722*.log`; commits `674829d`,
  `dd5df84`; plan doc `docs/engine_host/SFX_WIRING_PLAN.md`.
- Outcome: all abilities implemented; the engine SFX wiring (core deviation fix) is
  scoped with its first RE step done (factories + forwarder chain).
- Re-open: SFX vtable mapping → shim → A/B proof.


### 2026-10-01 - engine host - client engine instance live + real `.Sfx` create proven
- Window: the adapter passed a bad HWND to `KG3D_Engine::CreateTargetWindow` (client
  export `0x8AEF30`); a real Win32 window (1280x720) substituted via a 15-byte inline
  hook unblocked init: `iface->vt[0](0,4) -> 0` and `KG3D_GetEngine2()` returns a live
  client engine instance (log: `window init, size is :1264, 681`,
  `[Adapter] version=1.1.9.9 init success`).
- Decisive SFX test on the live client engine (`c纯阳坐忘.Sfx`, owner = client singleton
  `[engine+0x2CF1038] -> vt[8]()`): **no AV** (`obj` non-null, `exc=0`) - the ME
  09-14 build AVs on the same file. The core bug is a build difference; the client
  engine is the fix.
- Client play-path mapped from the engine's own bind code (`0xE348CA`): create
  (`0xBE5610`) -> `__RTDynamicCast` (descriptors `0x26080A0`/`0x2608B40`) ->
  `vt[0xD58]` hook -> `vt[0xD60]` attach -> `vt[0x180]` bind/play with the world
  matrix -> `vt[0x190]` handle. `.Sfx` has a real `vt[0x180]` (`0xBCC9F0`);
  bare `.pss` is stubbed at all four slots. `vt[0x180]` without the engine's attach
  context returns `E_NOINTERFACE` - attach/scene binding is the next step.
- Evidence: `%TEMP%\opencode\skillv2\{win2,play3}.out`; probe `client_sfx_probe.cpp`;
  plan `docs/engine_host/SFX_WIRING_PLAN.md`.
- Re-open: attach context + scene binding -> engine-driven playback A/B in the app.

### 2026-10-01 - engine host - client-stack probe: scene live, FS/data boundary, bind context open
- Scene: the probe now creates an empty scene + scene view (`CreateEmptyScene(0) -> 0`,
  `CreateSceneViewFrom3DScene -> 0`, `AddSceneView_SceneView -> 0`) so
  `Get3DScene2(window)` returns a live scene; the SFX bind (`vt[0x180]`) still
  returns `E_NOINTERFACE` - the missing piece is the SFX-model attach/bind context
  (`KG3D_SFXModel::BindData`, engine `0xE345E0`, called via a runtime pointer; the
  direct object's `vt[0xD60]` is the shared stub).
- FS/data boundary: the game file layer serves the **PakV4 store only** - loose files
  copied into the working root stay invisible (`g_IsFileExist(mesh)=0` vs `sfx=1`;
  `KG3D_LoadFile` same) even after `g_SetRootPath` + `g_SetFilePath`. Player models
  are not in PakV4 (only in the updater cache) - the local install is a partial client,
  models streamed on demand. Both case variants of the SFX load fine; the actor path's
  failure is its **owner/context FS**, not case. The engine's animation tag system exists
  (`GetAnimTagSystem` non-null; `KG3D_AnimationTagX64.dll` vtable 28 slots) but
  cannot fire without a loadable actor/animation.
- The probe is preserved in-repo: `tools/engine_host/client_sfx_probe.cpp` +
  `build_client_sfx_probe.cmd` (root via `RC_PROBE_ROOT`; registered in the area
  README tools table).
- Evidence: `%TEMP%\opencode\skillv2\{fs2,view,envroot}.out`; commits `8d9c529`,
  `6346af5`, `1365b98`.
- Re-open: SFX-model attach context (or runtime assets from a full client update) ->
  engine-driven playback A/B in the app.

## 2026-10-02 �� Phase 3 client host: loose-map bridge + source landscape loader (deviation)

- Built 
ative/client_host/client_host.cpp (Phase 3 host core): boots the client stack
  the game way (facade -> adapter -> file layer -> engine, host window via the
  CreateTargetWindow hook), then map -> view -> actor -> authored animation
  (_InitAttachTani -> StartAnimation) -> real .Sfx (KG3D_CreateSFXFromFile, exc=0)
  -> 240-frame loop -> engine screenshot API.
- **Provisional deviations (host file layer, in-memory, no install writes):**
  1. file-mode wrapper (KG3D_StdFileSystem, engine+0x2D22598) existence predicates
     (vt[8]/vt[9]) are patched to also accept loose files under the engine root ��
     otherwise the landscape loaders reject every loose sandbox-map file
     (Landscape System lost file). Original predicates are tried first.
  2. heightmap_bc hidden from KG3D_LoadFile so the engine selects its own
     source-format landscape loader (BC loader requires pak-indexed baked data).
  3. View for a source scene: CreateSceneViewFrom3DScene(engine, scene, NULL, ...)
     (a name makes the engine try to load a packed map, mask 0x10203040).
- Why: the sandbox map is intentionally loose (	ools/sandbox/build_sandbox.py);
  the client engine expects indexed/pak files for landscape checks. Re-open: if maps
  are ever served from an indexed store (pak/cache), drop the bridge and the hide.
- Evidence: %TEMP%\opencode\skillv2\host15.out (CreateSceneFromSource -> 0x0,
  view create=0x0 add=0x0, actor/animation/SFX all rc=0); commit 57e3268 + this one.
- Remaining: black render (no camera pose) -> KG3D_Engine::CreateCamera (0x8AF8E0)
  with the spawn/camera data, then the app port.

## 2026-10-02 �� Phase 3 render breakthrough: SceneViewEx paint pair

- The client host's window paint crashed at frame 14 in a lazy shadow update
  ( x70FDE9, helper 0x70E700 reading [r8+0x10] from a bad object) when using
  BeginPaintView/EndPaintView (0xA6BEF0/0xA6C3C0).
- **Fix**: use the SceneViewEx pair BeginPaintSceneViewEx (0xA6C1B0) /
  EndPaintSceneViewEx (0xA6C480). 240 frames paint cleanly and the engine
  screenshot shows the real sandbox map (terrain/ruins/sky; host_shot24.png).
- Also fixed before it: sandbox data gaps (d/env_probe/*.dds from the pak;
  d/shadowparam.json from the engine's global rcdata default) �� builder updated.
- Camera pose: KG3D_CAMERA_POSE = {eye[3], target[3], up[3]} (0x24 bytes) via
  KG3D_Camera::SetPose (0xB36540); the view re-derives its camera from the map's
  systemCamera.json, so host pose control is the next item.
- Evidence: %TEMP%\opencode\skillv2\host24.out, host_shot24/25.png; commits
  cda0b6c, 94f2955, ec50b37.

## 2026-10-02 �� Phase 3: ability casting works; actor render registration still open

- The client host now reads the staged ability list (skill_data.json -> 24 entries) and
  casts on keys 1..9 (auto-cast once at boot): _InitAttachTani resolves the skill tani
  (anim non-null; rc E_FAIL is the post-load attach step), StartAnimation=0x0, and a
  real .Sfx is created per cast (exc=0). Verified host_cast.out (��ʱ��צ).
- Actor visibility attempts: (a) passing the scene as CreateActorFromFile's 3rd arg -
  no change; (b) injecting a player worldObject entity into the map's
  entities/sceneinfo_full/000_000.json (	ools/sandbox/add_player_entity.py, cloned
  record with the f1 mesh + spawn matrix) - map still loads but the character is not
  drawn. The scene renders map props (worldObjects) yet not our injected/standalone
  actor: likely the engine scene alone does not spawn/stream actor worldObjects; the
  game's represent/logic layer (JX3RepresentX64) drives actor entities.
- Terrain ground also still not drawn (same class: scene-state/render-option wiring).
- Next: either wire the engine's scene-object render path (KG3D_SceneObject +
  AcqureRenderActorProxy 0x9BB730) or integrate the represent module for actors.
- Evidence: %TEMP%\opencode\skillv2\host_cast.out, host_entity.out; commit 9a8f1a0.

## 2026-10-02 �� Phase 3: injected map entity IS processed (model path resolved), still not drawn

- Decisive probe: set the injected player worldObject's comRender.actorModel to a
  nonexistent path -> the engine logged
  missing model file data\source\player\f1\����\zz_entity_probe.mesh.
  So the map's entities/sceneinfo_full IS read by the client engine and the entity's
  model path is resolved (restored to the real f1 mesh afterwards).
- The entity's actor still does not appear in the render (frames identical with/without
  it; host_char.png mean #A2B4B5) - the engine scene spawns the world object and
  loads its model but the actor is not drawn (render-proxy/animation state, or actor
  entities are driven by the game logic/represent layer).
- Host also now logs entity/model LoadFile paths (diag filter in the LoadFile hook).
- Evidence: %TEMP%\opencode\skillv2\host_probe.out (missing-model line),
  host_char.out/png.

## 2026-10-02 �� Phase 3: AcqureRenderActorProxy never called by the engine (render path is elsewhere)

- Hooked KG3D_SceneObject::AcqureRenderActorProxy (0x9BB730) with GUID logging
  (GetGUIDString 0x9BBC60) and ran the full host: **zero calls** for the whole run
  (map props render anyway) - so that API is not the normal actor render path.
- The injected entity's model IS resolved (missing-model probe earlier), but the actor
  still does not render; the scene's actor draw path for entity actors remains open.
- Evidence: %TEMP%\opencode\skillv2\host_proxy.out; host keeps the proxy hook
  (harmless, logs any future calls).

## 2026-10-02 �� Phase 3: entity spawn diagnostics (SceneObject Init/Fetch/Update hooks)

- Hooked KG3D_SceneObject::Init(type,...) (0x9B9440), FetchModelFromActor (0x9B9D50),
  UpdateFromActor (0x9B9AD0) with GUID logging: **zero calls** in the full run - the
  scene's entity spawn does not use these exported overloads (it uses other internal
  paths, e.g. the SOURCE_MAP_SCENE_ENTITY_INFO init).
- The f1 model + JsonInspack keep loading **during the frame loop** (per-frame lines in
  host_spawn.out), i.e. the actor/model load appears to retry each frame - likely the
  entity's actor spawn not completing (or a re-fetch path). Next probe: remove the
  injected entity and compare (persist => standalone actor re-fetch; stop => entity).
- Evidence: %TEMP%\opencode\skillv2\host_spawn.out.

## 2026-10-02 �� Phase 3: entity on/off comparison (decisive)

- With the injected player entity: the f1 mesh + JsonInspack load **repeatedly during
  the frame loop** (3+ times, no error lines). Without the entity: exactly **1** load
  (the standalone actor at creation). So the entity's actor/model path keeps re-loading
  (spawn retry or re-fetch) but never reaches a drawn actor.
- The frame loop also takes >120 s in the recent runs (likely GPU/engine contention with
  the still-running Skill.exe; earlier runs finished ~40 s) - kill the app before long
  host diagnostics.
- Next: trace the repeated-load caller (entity spawn internal path) or accept the game
  represent layer as the actor driver; then actor render + terrain, then the app port.

## 2026-10-02 �� Phase 3: SFX play/registration path mapped (effect not standalone)

- The engine's own play sequence after KG3D_CreateSFXFromFile (caller at 0x44FEE9,
  inside the animation-controller SFX-tag playback block 0x44FExx):
  1. sfx->vt[0x150] (slot 42) with &local -> reads the SFX world position;
  2. host object [ctx+0x18] -> t[0xa0] (slot 20) with the position -> places;
  3. host [ctx+0x18] -> t[0x58] (slot 11) with dx=sfx -> **registers the SFX
     with the host**;
  4. host callback [+0x2170]/[+0x2178] ->  xC45470.
- So a created SFX is inert until registered with a host (controller/scene object);
  the scene owns an SFX particle renderer (KG3D_SceneObjectContainer::
  _InitSFXParticleRenderer), i.e. effects render through scene objects.
- Host diagnostics that crash: inline hooks on FetchModelFromActor/UpdateFromActor
  (bad trampolines) - removed; keep only clean-prologue hooks (CreateTargetWindow,
  LoadFile, AcqureRenderActorProxy).
- Evidence: disasm above; commit c73072 (stable host).

## 2026-10-03 �� A1 ANSWERED: scene object created; actor draw is the game layer's job

- Hooked KG3D_SceneObject::Init(SOURCE_MAP_SCENE_ENTITY_INFO*) (0x9B9030): it fires for
  **every** world object (724) with rc=0, including our injected player entity
  (guid={aaaaaaaa-...} -> 0x0). So the scene-object registration exists and succeeds.
- Marking our entity with the engine's own character flags (SetCharactorObject 0xE6490,
  SetMainCharactor 0xE64D0) changes nothing visually.
- Backtrace on the entity's model load (RtlCaptureStackBackTrace in the LoadFile hook):
  the f1 mesh + JsonInspack load on an **async worker thread** (chain via
  0x87F288/0x891771/0x889711 -> engine external thread), i.e. the engine's scene entity
  loader does stream the actor model; the loads succeed, no error.
- Conclusion: the engine scene creates scene objects and streams their models, but the
  **render actor for characters is created by the game layer (JX3RepresentX64 / logic)**,
  not by the engine scene alone. A1's "host/registration object" therefore does not exist
  inside the engine scene; the remaining path to a visible character is the represent
  module integration (or an engine-side render proxy created the way the game does).
- Also: engine FrameMove is now called every frame again (the old f<8 hack removed; the
  SceneViewEx paint pair already fixed the shadow crash).
- Evidence: %TEMP%\opencode\skillv2\host_a3.out (Init trace), host_a6.out (backtrace).

## 2026-10-03 �� Represent module: booted; lifecycle init blocks (game env needed)

- Host now loads JX3RepresentX64.dll and calls CreateSO3Represent (non-null) +
  GetRepresentECSRootEntity (null until the module's lifecycle init runs).
- The game's boot (JX3ClientX64.exe KJX3RepresentModule): Load (0xBC7xx) does
  GetProcAddress("CreateSO3Represent") + call; the module **vtable RVA 0x95A350**
  (COL 0x96F840 -> TD .?AVKJX3RepresentModule@@); lifecycle handler 0xBC8F0 calls the
  singleton's t[1] (activate) / t[2] (deactivate).
- Calling the singleton's t[1] from our host **blocks/hangs** (no return; killed at
  150 s) - it expects the game module environment (hierarchy/resources/network context).
  Reverted to keep the host usable; do not call vt[1] without replicating that env.
- Next probe: run vt[1] on a worker thread with a watchdog and capture its stack to see
  what it waits on; or find the module environment objects (KJX3RepresentModule +0x18
  singleton holder) the game builds before activation.
- Evidence: %TEMP%\opencode\skillv2\host_rep2.out; commit pending.

## 2026-10-03 �� Represent init: message pump required; ECS hierarchy still not created

- CreateSO3Represent singleton lifecycle init (vt[1]) **completes in 50-200 ms when the
  main thread pumps messages** during the wait (PeekMessage loop) - without the pump it
  hangs >6 s (first run completed by luck). The represent init does cross-thread work
  that needs the message pump.
- After vt[1], GetRepresentECSRootEntity is still **NULL**: the ECS hierarchy
  (CreateHierarchy 0x924825 writer at 0x924BC3 -> global 0xF512A8) is not created by
  vt[1]. The hierarchy function (0x924640..0x924D3E) has **no static references**
  (not in any vtable/table) - it is wired at runtime by the game's module system
  (KJX3RepresentModule in JX3ClientX64.exe: slot0 ctor 0xBC100 sets vtable 0x95A350,
  slot4 Load 0xBC6A0 creates the singleton, slot6 0xBC8F0 lifecycle).
- Next probes: (a) call the singleton's remaining vtable slots with the pump and check
  the ECS root after each; (b) hook the ECS-root writer (0x924BC3) at runtime to catch
  the real caller; (c) engine-side alternative: OnSceneActorLoadedCallBack (0x8CA470)
  registration for our actor.
- Evidence: %TEMP%\opencode\skillv2\host_rep4/6.out; commit pending.

## 2026-10-03 �� TERRAIN RENDERS: OnSceneActorLoadedCallBack unlocks the scene draw

- After the actor is created, sending the engine's own OnSceneActorLoadedCallBack
  (0x8CA470: engine, actor, loaded=1, 0, model=actor+0x358, path, matrix, zeroed
  param struct, 0, 0) makes the **terrain render** (sand ground under the props).
  Before: props floated on sky. After: host_loaded.png shows the dune + flat sand
  with buildings/cart/trees standing on it (mean #AEB6B0 vs #A2B4B5).
- The lower frame is void/sky (cropped region edge - the 1x1 crop's terrain ends).
- Character still not visible (the callback registered the actor into the scene state
  but the draw of the actor itself is still pending; possibly needs the correct
  scene-node param struct or the represent actor path).
- Evidence: %TEMP%\opencode\skillv2\host_loaded.out/png.
## 2026-10-04 — Character draw root cause: KeepMeshData registry (PakV4 copy) gates render-data build

- The engine scene DOES render live scene objects: moving an existing prop in
  `entities/sceneinfo_full/000_000.json` moves the visible building; a clone of a
  building record or of an npc_source model (A303) added at the spawn renders.
  So the map entity list is the render source (no pre-baked static world).
- The f1_3094 player model does NOT render as a scene object, while the A303 NPC model
  and prop models do. Root-cause chain (runtime-verified):
  - `KG3D_NormalMesh` (+0x484 bit 0) is set from the keep-check `0xC4D430(mesh)`; the
    check passes for props (fd0==0), npc_source prefixes, or names present in the
    KeepMeshData registry; f1 fails it (`keepCheck f1_3094_body_hd.mesh -> 0`,
    A303 -> 1).
  - The registry (global 0x2CFA548) is built once at engine init (builder 0xBE4BC0,
    caller 0x8BB4B9) from `data/public/KeepMeshData_FileList.tab` read through the
    **PakV4** file layer: renaming the loose client_root copy changes nothing (A303
    still passes), so loose-file edits to that tab are ignored.
  - When the check fails, the render-data build (0xC4DE80, called from the batch at
    0xD8AF40/0xC5F3F7) skips the buffers ([mesh+0x4a8]/[mesh+0x4b0] stay null) and the
    mesh has nothing to draw. Forcing the checker to 1 for f1 did not render it either
    (next probe: hook 0xC4DE80 and log f1's mesh + post state).
- Negative results (all tried, no visual change): entity SO flags
  (IsPlayerObject/StaticModel/ForceRender/NotForCull/EnterScene + SetCullDataDirty),
  attaching a real KG3D_AnimationController + _InitAttachTani + StartAnimation to the
  entity's own scene actor, sending OnSceneActorLoadedCallBack with the engine's real
  param struct, loading the f1 mesh from the pak instead of loose, copying the f1 mesh
  to a maps_source path, disabling SubSetReplace in the mesh.ini, and the f1 mannequin
  .mdl (resolves to f1_1000c_body_hd.mesh) — none render.
- Also: real .Sfx created at the spawn (obj non-null, exc=0) does not render; SFX are
  actors without scene objects, same actor-draw gap.
- Next probes: (a) hook 0xC4DE80 to see whether f1's mesh reaches the render-data build
  and why it bails; (b) hook the registry builder's file stream (0xB0FE10 at 0xBE4C12 /
  the line iterator at 0xBE4CD0) to serve an augmented KeepMeshData list; (c) represent
  module integration (the game layer path for player models).
- Host additions this session (diagnostics): keepCheck logging hook (0xC4D430),
  meshFactory trace (0xC4D7F0), per-model mesh state dumps (name/fd0/flag484) at
  frames 30/200, nearest-scene-object dump, notify branch probe (model vt[0x118] /
  actor chain), SFX spawn placement.
- Evidence: %TEMP%\opencode\skillv2\host_char*.out/png, host_npc*.out/png,
  host_f1test.out, host_notab.out, host_final.out/png (clean state, 725 objects).

## 2026-10-04 — Follow-up probes: registry is PakV4-sourced; SO flags set by a model-based classifier

- The render-data build `0xC4DE80` (called from the batch `0xD8AF40`) is **never
  called** in the host run (hook installed, zero hits). The keep-check calls seen at
  load come from the render-data creation path instead: chain
  `0xC4DBF5 (check+set bit0) <- 0xC4E3C0 (create render data) <- 0xBE3D75 (model
  loader) <- 0xC04A2E <- ...` for both props and f1.
- The KeepMeshData registry (global 0x2CFA548) has **1480 live entries** at load
  ([+0x28] = live, [+0x20] = tombstones — the earlier "count=0" read the wrong
  field). Adding 500 dummy lines plus f1 variants to the loose
  `data/public/KeepMeshData_FileList.tab` leaves the live count at 1480 — the builder
  reads the **PakV4 copy**, the loose file is ignored (proven, not inferred).
- SceneObject flags are set **asynchronously after Init** by a model-based classifier:
  at Init the f1 entity carries 0x180 (RenderVisible|InsideSpaceNode, like props), and
  after the model loads it becomes 0x10190 (IsPlayerObject|FrameMove|DisableAngleFilter|
  SmoothFramemove); the A303 clone becomes 0x8180 (IsStaticModel|...). Forcing static
  flags at Init or at frame 120 (with SetCullDataDirty) and setting a prop `_tableName`
  on the f1 entity change nothing — the classification keys on the mesh/model itself.
- Conclusion: the engine scene's static render path deliberately excludes player models;
  a player body will only draw through the game layer (represent) or by changing how the
  mesh classifies (fd0=0x60 vs A303 fd0=0x26 vs props fd0=0). Next probes:
  (a) find the classifier (who writes IsPlayerObject/FrameMove after model load) and its
  key; (b) hook the PakV4 read of the tab / the builder's file reader (0xB0FE10 at
  0xBE4C12) to inject f1 into the registry and test whether registry membership changes
  classification; (c) represent integration.
- Host diagnostics added: `buildData` hook (0xC4DE80, currently zero hits),
  registry live/tombstone/cap readout, keepCheck backtraces (kcbt), buf1/buf2
  ([mesh+0x4a8]/[0x4b0]) in the mesh dumps.
- Evidence: %TEMP%\opencode\skillv2\host_kcbt.out, host_reg2/3/4.out, host_dummy.out
  (500-line proof), host_static.out, host_tname.out, host_final2.out (clean state).

## 2026-10-04 — Classifier found: FetchModelFromActor sets IsPlayerObject from the actor's vt[0x130]

- Hardware write-watchpoint (Dr0 on the entity SO flag dword, VEH single-step logging,
  gated by RC_HOST_WATCH) caught the flag writers. The model-based classification is in
  `KG3D_SceneObject::FetchModelFromActor` (0x9B9D50): at 0x9B9E4D it calls the scene
  actor's `vt[0x130]()` and sets/clears IsPlayerObject (0x10000) accordingly (0x9B9E66),
  then `vt[0x120]()` sets IsMainCharactor (0x40) (0x9B9E9A). A second site
  (0x9BA1E0-0x9BA272) sets FrameMove/StaticModel-style bits from further actor state.
  Other writers caught: 0xA61314 (`or [so+0x10],0x80` per-frame InsideSpaceNode in the
  window update), 0xA62548, 0x850850/0x850879, 0xA42E1B.
- So the classification key is the **scene actor's vt[0x130]** (a property of the
  loaded model/actor), not the entity JSON, `_tableName`, model path, or the keep
  registry. Flag overrides at Init / frame 120 / `_tableName` do not render the player
  model, and the object still has no GPU buffers (buf1/buf2 null) — the static-world
  render path excludes player-classified models by design.
- FetchModelFromActor is **not hookable with the generic call-trampoline**: its prologue
  is `mov rax,rsp; mov [rax+0x10],rbx; ...; sub rsp,0x2d0` (rsp-capturing frame setup),
  so a C hook's own frame + call push shifts rsp and the copied prologue computes a
  wrong frame -> stack-cookie check fails and the paint faults at frame 2 (verified;
  reverted). A jmp-based naked detour would be needed for that function.
- Host: watchpoint mechanism kept but arms only with `RC_HOST_WATCH=1`; the failed
  classifier hook and the rsp-adjusted installer variant were removed.
- Next probes (unchanged): represent-module integration for player models, or a
  jmp-based detour on FetchModelFromActor to test forced classification end-to-end.
- Evidence: %TEMP%\opencode\skillv2\host_watch.out (writer list), host_watch2.out
  (10 writers), host_class.out / host_class2.out (hook faults), host_final3.out/png
  (restored clean state).

## 2026-10-04 — Represent integration breakthrough: ECS world + hierarchy can be created directly

- The singleton lifecycle init (vt[1]) is nondeterministic in this host: with the frame
  loop pumping it either completes in 50 ms **after faulting** (AV inside the job
  processor at represent+0x372CAC, swallowed by the thread wrapper) or spins forever on
  a job lock (represent+0x372CC8). Either way the ECS world global (0xF51298) stays null,
  so the entity factory (0x920C40) returns null and the hierarchy builder fails.
- **Direct path that works (RC_HOST_REPINIT=2):** skip the lifecycle init and call
  - `represent+0x920D10` (ECS world create; takes a vector ptr, stores the world object
    at global 0xF51298) with a zeroed local vector, then
  - `represent+0x924B20` (hierarchy builder; calls CreateEntityByName("root") and
    ("reference") via thunk 0x15BF4 -> 0xAEDFD0, stores them at globals 0xF512A8/0xF512B0).
  Result (host_rep11.out): `world=000002B2507666D0`, `root=000002B25F819680`,
  `GetRepresentECSRootEntity -> 000002B25F819680`, slots 2+ run clean, frame loop
  completes. This is the first live represent ECS in the host.
- The represent DLL (16.8 MB) exposes only 3 exports (CreateSO3Represent 0xF9CA,
  GetRepresentECSRootEntity 0x2680 -> global 0xF512A8, CreateRLLoader 0x141F5); the rest
  is the singleton vtable + internal "KRL" character API found in strings:
  `CreateEntity`, `LoadEntityFromFile`, `PlayPlayerAnimation`, `PlayNPCAnimation`,
  `HandleCharacterAnimation`, `KRL_ENTITY_TYPE::{AVATOR,PLAYER,NPC}`, `pLocalCharacter`,
  `ReloadAllCharacter`, `scene[main]`, `camera controller` etc. ECS system entities are
  created by name (e.g. "scene[main]" at 0xADF277).
- Next probes for a visible player: (a) find the local-player creation path in the KRL
  API (CreateEntity/LoadEntityFromFile with an entity file + KRL_ENTITY_TYPE::PLAYER)
  and the f1 model binding; (b) attach the created entity under scene[main]; (c) drive
  the represent tick each frame; (d) check whether the represent-created actor is drawn
  by the engine scene (game-layer scene node path).
- Host: represent probe deferred to frame 5 (engine pump active); RC_HOST_REPINIT=1 =
  init+hierarchy+slots, =2 = skip init (working path); default runs unchanged and green.
- Evidence: %TEMP%\opencode\skillv2\host_rep9.out (init fault), host_rep10.out (hang),
  host_rep11.out (ECS root live), host_final4.out/png (default path clean).

## 2026-10-04 — Engine-side character route conclusively closed; represent KRL component API mapped

- The classification key resolved at runtime: the SceneActor wrapper ([so+0x100]) uses
  the MSVC adjustor pattern; both FetchModelFromActor slots (0x130 IsPlayerObject,
  0x120 IsMainCharactor) land on the engine thunk `0x87EAF4 -> 0x852E00`, which returns
  **bit 8 of the dword at [adjusted_this-0x68C]**.
- Forcing that method to return 0 for the sandbox entity's actor at load time (both
  classification calls overridden; logged `isPlayer entity override: 1 -> 0`) still
  does **not** render the f1 body (screenshot diff vs clean = noise). Combined with the
  earlier results (flags, registry, keep-check, buffers, controller), the engine-scene
  route for the player model is conclusively closed: player models are excluded from the
  static-world render path by design and drawn only by the game layer (represent).
- Represent status: live ECS root in the host (previous entry). `CreateRLLoader`
  (export 0x141F5 -> 0x3FB580) requires a game-environment object (magic tag 0xC0 at
  [rcx]) - the same missing module environment that makes the singleton lifecycle init
  fault. The KRL API surface is mapped by strings: `CreateEntity`, `CreateComponent`,
  `LoadEntityFromFile`, `LoadComponentFromFile`, `ApplyComponentAction`, and component
  classes `RendererComponent` (LuaLoadModel / LuaPlayAnimation / LuaSetRoleType /
  LuaSetVisible / LuaPlaySFX / LuaSetRepresentID ...), `BehaviorComponent`,
  `CameraComponent`, `PhysxComponent`.
- Next represent probes (minimal character path without the loader/environment):
  (a) map `CreateComponent` + the KRL_COMPONENT_TYPE enum (find the enum/table used by
  CreateComponent); (b) create a RendererComponent on an entity, call LuaLoadModel with
  the f1 mesh and LuaSetRoleType, parent the entity under "scene[main]"; (c) find the
  per-frame represent tick (the singleton's frame update) and call it from the host
  frame loop; (d) check whether the represent renderer creates an engine actor/scene node
  that the engine draws.
- Host: isPlayer hook (0x852E00) is log-only now (override removed); class-method probe
  retained. Default run green (host_final5.out).
- Evidence: %TEMP%\opencode\skillv2\host_cls2.out (method resolution), host_isp.out
  (override + no render), host_final5.out, rep_comp.txt (component API strings).

## 2026-10-04 — Represent: ECS-root success is nondeterministic; KRL create needs a healthy world

- Re-running the mode-2 direct path (world create 0x920D10 + hierarchy builder 0x924B20)
  now faults in the builder (`represent hierarchy builder fault`, VEH at a heap-looking
  address) in most runs; the earlier `root=...` success (host_rep11) was the fluke. A
  faulted builder leaves the represent state bad enough that the later paint AVs
  (exit 0xC0000005). So the direct path is not yet reliable - the represent module
  expects its game environment (the same missing bootstrap that makes the singleton
  lifecycle init fault/hang).
- KRL API mapped for the next attempt:
  - `CreateEntity(world, &name, type=1)` at represent+0x2EA6C0: asserts type==1, calls
    the entity factory thunk 0x15BF4 -> 0xAEDFD0 (`CreateEntityByName`, returns the
    entity pointer), then a world notify `0x4B33(world, world)`.
  - `CreateComponent(entity, type=2)` at represent+0x2EA580: asserts type==2, checks
    the entity's component list ([entity+0x20]) for duplicates (max index 0x26),
    allocates via 0xE5D4 and attaches via 0x1384A(entity, component).
  - Component classes (from strings): RendererComponent (LuaLoadModel, LuaPlayAnimation,
    LuaSetRoleType, LuaSetVisible, LuaSetRepresentID, LuaPlaySFX...), BehaviorComponent,
    CameraComponent, PhysxComponent; entity/system names "scene[main]", "camera mode",
    "camera controller".
  - `CreateRLLoader` (export 0x141F5 -> 0x3FB580) requires a game-environment object
    (magic tag 0xC0 at [rcx]) - the exe-provided module environment.
- Host: entity-factory hook (0xAEDFD0) captures created entities; mode-2 now skips the
  faulting slots and guards the KRL create on a healthy hierarchy root. Default runs
  remain green (host_final6.out, mean #A2B4B5, exit 0).
- Blocker statement: the represent path needs the game module environment (services/jobs)
  that JX3ClientX64.exe builds before activating KJX3RepresentModule. Next options:
  (a) replicate the environment bootstrap (map the exe module system), (b) retry the
  direct path with retries/ordering until the builder is stable (nondeterministic), or
  (c) accept the engine-only sandbox (map + props + SFX + animations) and defer the
  player body.
- Evidence: %TEMP%\opencode\skillv2\host_rep11.out (one-off success), host_krl.out /
  host_krl2.out (builder faults), host_final6.out/png (default green), rep_funcs.txt,
  rep_cc.txt, rep_ce.txt (KRL API disassembly).

## 2026-10-04 — BLOCKER BROKEN: KGRLLoader works standalone (0xC0 tag only, no module environment)

- Traced the client's own bootstrap: `JX3LogicEditOperationX64.dll` (client bin64)
  `InitLogic` creates `g_pSO3World` (0x125A00) + init (0x12B7E0), then builds a stack
  env struct whose **first dword is 0xC0**, loads `JX3RepresentX64.dll`
  (`LoadConvertModule` 0x112FC0) and calls `GetProcAddress("CreateRLLoader")` +
  `CreateRLLoader(&env)` -> `g_pRLLoader`. The env struct carries only the 0xC0 tag at
  creation time (CreateRLLoader checks `[rcx]==0xC0` and nothing else).
- **Host test (RC_HOST_RLLOADER=1):** `CreateRLLoader(&{0xC0})` -> non-null loader
  `0000014C5093D990`, vtable rep+0xC9C668, run completes cleanly (exit 0). No SO3
  singleton, no lifecycle init, no module environment needed. MovieEditor's own
  bootstrap (MovieEngineCLR `KGRepresentHelper`) also uses CreateRLLoader (not
  CreateSO3Represent), confirming this is the host path.
- KGRLLoader API (named via its assert strings + vtable disassembly):
  `LoadPlayerAllModel`, `LoadUnitFromFile` (takes a VFS file object, version <= 0x38),
  `LoadUnitFromBufferV1..V5`, `GetUnitFromPath`, `GetRepresentIDFromPath`, `GetUnit`,
  `ReleaseUnit`, `GetAnimationRelation`, `GetRandomAnimationID`, `GetEquipmentScale`.
  Vtable methods live at rep+0x3F4xxx-0x3F9xxx; thunks at rep+0x1xxxx.
- Next: disassemble `GetUnitFromPath` (rep+0x3F51xx) for the unit path/extension it
  expects; find the unit files in the pak index (no ".kgrl" in Trunk.Dir - extension
  naming still open); then `GetUnitFromPath(loader, path)` -> unit -> engine actor ->
  render. `ConvertKGRLUnitToJson` (MovieEditor build export) hints the unit format.
- Evidence: %TEMP%\opencode\skillv2\host_rl.out (loader live), le_call.txt/le_init.txt
  (client InitLogic), rl_names.txt (KGRLLoader API), rl_vt3.txt (vtable methods).

## 2026-10-04 — KGRLLoader call path verified; loader errors visible; resource converter identified as the missing init

- Host now calls the loader methods directly (RC_HOST_RLLOADER=1): `CreateRLLoader` ->
  non-null, `rlvt[6]` = `KGRLLoader::LoadPlayerAllModel` -> 0 (fails at its line 408
  init), `rlvt[16]` = `KGRLLoader::GetUnitFromPath(...f1_3094_body_hd.mesh)` -> NULL
  (`GetRepresentIDFromPath` fails at its line 486). No crash; run completes.
- Represent logging is now captured: the DLL logs via
  `Engine_Lua5X64!?KGLogPrintf@@YAHW4KGLOG_PRIORITY@@QEBDZZ`; patching that IAT in
  JX3RepresentX64.dll routes its KGLOG errors into the host log (this is how the two
  failures above were seen).
- Full KGRLLoader vtable mapped (real impls after thunks): [0] 0x3F9C20 update,
  [1]/[2] GetUnit, [3] lock/update, [4] ReleaseUnit, [5] iterate, [6] LoadPlayerAllModel
  (0x3F5620, creates the 0x26470-byte `g_pRL` singleton + inits at +0x120/+0x1A0),
  [7] GetEquipmentScale, [8]/[9] iterate units, [10..15] helpers, [16] GetUnitFromPath
  (0x3F51C0), [17] LoadUnitFromFile (0x3F9940, takes a VFS file object, version <= 0x38),
  [18] 0x3FA100.
- Client InitLogic post-CreateRLLoader steps (JX3LogicEditOperationX64 0x11343A+):
  logs "Init RLLoader ... ... [%s]", then loads **`JX3ResourceConvertX64.dll`**
  (`m_hConvertModule`, present in both client and MovieEditor bin64, single export
  `KG_GetConvertResource` at 0x1870) and calls `KG_GetConvertResource(&global)`
  (logic-module global 0x9A0D90). This resource-converter init is the missing piece for
  `LoadPlayerAllModel` / `GetRepresentIDFromPath`.
- Next: replicate the post-loader step (load the converter, call KG_GetConvertResource
  with the right env/config), then LoadPlayerAllModel -> unit -> engine actor -> render;
  the unit file naming/extension for GetUnitFromPath is still open (no .kgrl in the pak
  index; GetRepresentIDFromPath maps a model path to an RL id).
- Evidence: %TEMP%\opencode\skillv2\host_unit.out / host_unit2.out (loader errors),
  rl_full.txt (vtable map), le_post.txt (client post-loader init).

## 2026-10-04 — RL system runs in the host: LoadPlayerAllModel OK; local player path mapped; HangPet controller env is the last gate

- Fixed the two missing pieces from the client's InitLogic:
  1. `Represent\filepath.ini` (the RL path table) was missing from the host root; the
     pak-extracted copy (from
     `...\SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\pakv4-probe\represent-out\represent`)
     was installed under `client_root\Represent\` -> `KFilePath::Init` succeeds.
  2. `JX3ResourceConvertX64.dll` loaded + `KG_GetConvertResource(void** out)` (returns
     the singleton; no env arg) -> path converter ready.
  After that **`KGRLLoader::LoadPlayerAllModel -> 1`** and the RL tables stream from the
  paks (`[rl-bin-tab] Represent/player/equip/...`).
- RL player table found: `Represent/Player/player.txt` (capital P; pak-extracted):
  role types 0..6, role 6 (rtLittleGirl) -> `Data\source\player\F1\部件\Mdl\F1.mdl`.
  `GetUnitFromPath` expects a KGRL unit file (fails `uFileSize >
  sizeof(KGRL_FILE_HEADER_EX)` on the .mdl) - the units are server/`LoadUnitFromBuffer`
  generated, not on disk.
- Local player assembly path mapped (the game's Lua binding `LoadPlayerParts`, 22 params,
  at 0x41C210):
  - `ctx = rep+0x42DAD0()` -> the HangPetWorld (RL context) - currently **fails**:
    `HangPetControllerLua::CreateEnv` (m_pScript, line 3143) -> `HangPetWorld::_Init`
    -> ctx NULL.
  - `core = rep+0x42D860(ctx, roleType)` -> per-role player core (map lookup; filled by
    LoadPlayerAllModel).
  - `rep+0x422F50(core, partsA[13], partsB[13], count)` -> assembles the player.
- HangPet behavior scripts are `represent/scripts/%s` (BehaviorComponent::Init 0x3FCDAC);
  the pak-extracted `represent/scripts/dummy/behavior_base.lua` and
  `rust_animation/rust_animation.lua` are installed, but the HangPet controller's script
  name comes from its component config, which the game's logic module sets up.
- Next: find who creates the HangPet entity/controller with its behavior script (client
  logic module `JX3LogicEditOperationX64.dll` or the represent DLL's HangPetWorld init
  path) and supply that config, then ctx -> core -> LoadPlayerParts -> visible player.
- Evidence: %TEMP%\opencode\skillv2\host_char_rl.out (LoadPlayerAllModel + HangPet env
  error), rl_names.txt, rl_vt3.txt, rl_core.txt (assembly API), hp_scripts.txt,
  rl_extract2\player.txt.

## 2026-10-04 — Last-gate trace: HangPet script comes from the global world object (rep+0xEDDFE0, field +0x25BC0)

- `HangPetControllerLua::CreateEnv` (rep+0x41EF10) reads `[[rep+0xEDDFE0]+0x25BC0]` as
  m_pScript (line 0xC47) and copies it to rep+0xEE5DC0; null -> CreateEnv fails ->
  `HangPetWorld::_Init` (ctx 0x42DAD0) fails -> ctx NULL -> no player core.
- rep+0xEDDFE0 holds a pointer to the game's global world/context object (written by the
  game init, not by the represent DLL - no direct writer in the represent image); its
  +0x25BC0 field is read in ~3900 sites across the DLL (the big game-world object).
- Also mapped: `LuaCreateHangPet(L, representID, type[, {szRoot,szMdl,szBone}])` at
  0x5BE120 (needs `pScene->m_pHangPetWorld` non-null, i.e. the same world init); the
  binding is the game's local-avatar creation entry.
- So the remaining integration is the game's global world/scripting bootstrap (the
  exe/module environment again), or finding a setter/creator for the +0x25BC0 script on
  that object in the represent DLL's own init path (next probe: find who creates the
  world object at rep+0xEDDFE0 - scan for stores of its address, incl. SIB encodings,
  and for the script slot writer at +0x25BC0 with 0x89 modrm forms).
- Evidence: hp_env.txt, hp_ctx.txt, hp_g2.txt, hp_scriptset.txt, hp_world.txt.

## 2026-10-04 — RL Lua state: singleton+0x25BC0 is an embedded script container; creation path still open

- Confirmed `CreateSO3Represent` (rep+0x3E7E10) allocates the 0x26470-byte singleton and
  stores it at rep+0xEDDFE0 — the same global `HangPetControllerLua::CreateEnv` reads.
  `singleton+0x25BC0` is an **embedded** object (BehaviorComponent::Init passes its
  address to the script loader 0x7CA7; CreateEnv reads its first qword as the script/state
  pointer).
- The singleton activate (vt[1] -> 0x3E5AE0) initializes it at 0x3E5B31 via
  rep+0x16130 -> 0x5C2FA0, but 0x5C2FA0 is a **container cleanup loop**, not a Lua state
  creator — calling it standalone leaves the state NULL (verified: `RL lua state
  (singleton+0x25BC0) -> 0`), and CreateEnv still fails with m_pScript.
- So the script/state creation for that embedded container happens in another part of
  vt[1] (or needs the engine's scripting module / game env); the host attempt is
  env-gated (RC_HOST_RLLOADER=1) and harmless. Next probe: trace the writer of the first
  qword at singleton+0x25BC0 (scan for stores relative to the singleton, incl. through
  helper calls) or find the RL script-manager creation in the vt[1] call list
  (0x26BF7/0x175F3/0x270C/0x1E80D/0x12715 ...) by testing each standalone.
- Evidence: host_char_rl2.out, hp_lua.txt, hp_bc.txt, hp_slotw.txt.

## 2026-10-04 — RL Lua state + HangPet ctx now live in the host; scene creation is the last step

- Found the RL Lua state creator: rep+0x5BC9F0(slot) calls `CreateLuaInterface(NULL,NULL)`
  (Engine_Lua5X64) and inits it, storing the interface at the embedded slot
  (singleton+0x25BC0). Called in the host -> `RL lua state -> 0000022251EB0260`.
- With the state present, `HangPetWorld` ctx creation (rep+0x42DAD0) now **succeeds**:
  `RL ctx -> 0000022251EB1340` (no more m_pScript error). This closes the "module
  environment" gate for the RL path - the Lua state was the missing piece.
- Next gate: the RL **scene** (rep+0x3E5E80(singleton)) is still null (created by another
  singleton init, `SO3RL::NewScene` wrapper 0x3EA420 -> vt[0xA8] factory). Without the
  scene: `RL scene -> 0` -> no local character -> CreateHangPet skipped.
- Full local-player call path now wired in the host (env-gated RC_HOST_RLLOADER=1):
  `scene = 0x3E5E80(singleton)`; `character = 0x58CE20(scene)`;
  `CreateHangPet = 0x42D1F0(world, sceneField, representID, character, cfg, type)`
  (the internal of the LuaCreateHangPet binding; sceneField = [scene+0xF1970],
  world = [scene+0xF2988], cfg = zeroed role config). Needs a non-null scene.
- Evidence: host_char_rl3.out (lua state + ctx), host_char_rl4.out (scene null),
  hp_luacreate.txt (CreateLuaInterface caller), hp_lch2.txt (CreateHangPet path),
  hp_scene.txt (NewScene).

## 2026-10-04 — RL scene creation is the final gate; vt[1] hang confirmed; probe stable without it

- Host run with the deferred RL probe (vt[1] call removed): stable, exit 0, frame loop
  done; `RL lua state -> live`, `RL ctx -> live`, `RL scene -> 0` (twice: represent
  block + frame-5 deferred), no hang.
- The singleton activate (vt[1]) **blocks** even with the Lua state present and the
  frame-loop pump running (confirmed again; the run wedged at frame 5 and had to be
  killed). So the scene map (singleton+0x24F40, init 0x1F1C2) and the RL scene
  (`SO3RL::NewScene` wrapper 0x3EA420 -> [rcx]->vt[0xA8] factory, no direct callers -
  vtable-dispatched) must be created manually.
- GetRLScene (0x3E5E80) = GetScene(singleton+0x24F40, [singleton+0x68]).
- Next probe: find the scene factory object/vtable path for vt[0xA8] (trace the wrapper
  0x3EA420's rcx origin - likely a scene-manager object created by one of the vt[1]
  sub-inits 0x26BF7/0x175F3/0x270C/0x1E80D/0x12715, testable standalone), then
  insert the scene into the map at +0x24F40 and run scene -> character -> CreateHangPet.
- Evidence: host_char_rl6.out (stable), host_char_rl5.out (vt[1] hang), hp_scene.txt.

## 2026-10-04 — RL scene creation call located: NewScene(mgr = g_pRL->m_p3DEngineManager, 1, &out); manager is vt[1]-created

- Found all three scene-creation call sites (KRLMovie::SwitchScene 0x58531C, NewExScene
  0xB0B834, NewScene 0xB0BEF9): each does
  `rcx = [singleton+0xB0] (g_pRL->m_p3DEngineManager); edx = 1; r8 = &scene;
   call 0x16DB5 (thunk -> 0x3EA420 -> [rcx]->vt[0xA8])`.
- In the host, `singleton+0xB0` is **NULL** (the engine manager is created by the
  singleton activate vt[1], which blocks here), so NewScene is skipped (verified:
  `RL engine manager (singleton+0xB0) -> 0`). Host now attempts NewScene when non-null
  (env-gated RC_HOST_RLLOADER=1; stable, exit 0).
- Next probes: (a) find the engine-manager creation (the [singleton+0xB0] writer - the
  store uses disp8 0xB0 = "-0x50" in disasm; the writer scan needs to key on the
  singleton register, e.g. by tracing vt[1]'s 0xB0 read at 0x3E5B42 backwards to its
  creation, or testing the vt[1] sub-inits 0x26BF7/0x175F3/0x270C/0x1E80D/0x12715
  standalone); (b) or get the engine manager from the engine host interface directly
  (KG3D_Engine::GetSceneManagerDLL 0x8CB3A0 / adapter interface) if it is the same
  object; then NewScene -> scene -> character -> CreateHangPet.
- Evidence: host_char_rl7.out (mgr null), rl_nscallers.txt (call sites), rl_subinits.txt.
