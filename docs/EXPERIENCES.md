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

## 2026-10-04 — RL manager gate root cause: SO3Represent::Init(Param) needs the full X3DEngine facade stack

- The engine manager (`singleton+0xB0`) is set only by `SO3Represent::Init(Param*)` =
  singleton vtable slot [0] (vtable 0xC99348 -> thunk 0xF63C -> 0x3E6070); verified by
  dumping the vtable (slot[0] == 0xF63C) and the Init prologue (asserts at 0x3E6085+).
- Init Param (cbSize == 0xD0) requires 8 engine interfaces (all asserted non-null):
  +0x08 p3DEngineManager, +0x10 p3DModelManager, +0x18 p3DEngineXLogic,
  +0x20 p3DSceneResponseMgr, +0x28 p3DResourceConverter, +0x30 p3DMovieCore,
  +0x38 p3DUI, +0xC8 pStepCtrl.
- Where they come from: the game exe imports `X3DEngine.dll`
  (`NSX3DEngine::GetK3EngineMgr`, `GetK3EngineXRepresentLogic`, `LoadX3DEngine`,
  `PreInitX3DEngine`, `GetFilePath`, `GetViewMgr`, ...). X3DEngine.dll is a thin facade
  (GetK3EngineMgr = `mov rax,[X3DEngine+0x2F418]; ret`); the globals are set by its own
  LoadX3DEngine init. The game exe calls `CreateSO3Represent()` (no args, stores at
  module+0x18) and later `vt[0](&param)` with the facade objects.
- Known mappings: p3DEngineXLogic ~ KG3DEngineX64!Get3DEngineXLogicInterface (also
  X3DEngine GetK3EngineXRepresentLogic); p3DResourceConverter = KG_GetConvertResource
  (already used by the host); p3DUI = JX3UIX64!CreateSO3UI (logic module InitUI).
  p3DModelManager / p3DSceneResponseMgr / p3DMovieCore / pStepCtrl are X3DEngine-internal
  objects with no standalone factories found in bin64 exports.
- Conclusion: the RL/represent path is gated behind the game's complete engine bring-up
  (X3DEngine facade). Two options for next session:
  (A) initialize the game's own X3DEngine.dll facade in the host
      (LoadLibrary -> PreInitX3DEngine -> LoadX3DEngine) and use its getters - this is the
      game's own path and likely yields all 8 interfaces + the RL scene naturally;
  (B) hand-assemble the 8 interfaces (only ~3 have known factories; the rest are
      X3DEngine-internal) - likely a dead end.
- Host: `RC_HOST_RLLOADER=1` now attempts NewScene(mgr,1,&out) when singleton+0xB0 != 0
  (stable; mgr is 0 until Init runs).
- Evidence: rl_mgrset.txt / rl_setter.txt (Init asserts), rl_initbody.txt (Param fields),
  rl_x3d.txt (X3DEngine facade), rl_exeinit.txt (exe CreateSO3Represent), host_char_rl7.out.

## 2026-10-04 — RL Init Param interface factories located; 2/8 already live in the host

- The host already loads+inits the X3DEngine facade (`PreInitX3DEngine` + `LoadX3DEngine`,
  client_host.cpp ~1118) - runtime probe (RC_HOST_RLLOADER=1, host_char_rl8.out):
  `GetK3EngineMgr() -> non-null (module-static object)`,
  `GetK3EngineXRepresentLogic() -> non-null (heap)`, ViewMgr/ScreenMgr non-null.
- Param factory map (for SO3Represent::Init, cbSize 0xD0):
  - p3DEngineManager = X3DEngine!GetK3EngineMgr (LIVE)
  - p3DEngineXLogic = X3DEngine!GetK3EngineXRepresentLogic or
    KG3DEngineAdapterX64!Get3DEngineXLogicInterface (LIVE)
  - p3DResourceConverter = JX3ResourceConvertX64!KG_GetConvertResource (already used)
  - p3DUI = JX3UIX64!CreateSO3UI (0x8DCC0; same as logic module InitUI)
  - p3DSceneResponseMgr = KG3DSceneResponseX64!GetSceneResponse (0x6720)
  - p3DMovieCore = KG_MovieEngineX64!KG_GetMovieEngine (0x37E0) or
    KG3DMovieX64!GetMovieEngine (0x1224C0) or KG3DEngineAdapterX64!GetMovieEngine (0x6CAF0)
  - p3DModelManager = NOT FOUND yet (no export; rep DLL has internal RLModelManager
    initialized with pi3DEngineManager)
  - pStepCtrl = NOT FOUND yet
- Host probe added: X3D getter logging inside RC_HOST_RLLOADER (build OK, run exit 0).
- Next: identify p3DModelManager/pStepCtrl via the rep DLL's use sites of
  "g_pRL->m_p3DModelManager" (0xCA22C0) and the Init-adjacent asserts; then build the
  Param and call singleton vt[0] -> manager -> NewScene -> scene -> character.
- Evidence: host_char_rl8.out, rl_scanexports.txt (factory exports), rl_repgetter.txt.

## 2026-10-04 — RL SCENE CREATED in-host: manager field written directly; NewScene -> scene non-null

- Found the game's Param fill: exe `KJX3RepresentModule::Initialize` (0xBC150):
  Param at [rsp+0x50], cbSize 0xD0; call `singleton->vt[0](&param)` at 0xBC473.
  Field map (validated at runtime):
  +0x08 engineMgr (holder+0x18), +0x10 engineMgr->vt[9](), +0x18 engine XLogic
  (holder+0x20), +0x20 engineMgr->vt[0x50](), +0x28 resource converter, +0x30 movie core
  (holder+0x28), +0x38 engineMgr->vt[0xF](), +0x70 pSO3World, +0x78 pSO3WorldClient,
  +0xC8 pStepCtrl (local object).
- Host probe (host_char_rl9.out): all 8 interface params resolved non-null from the live
  X3DEngine facade + factories; `SO3Represent::Init` passed them and failed later on
  `Param.pSO3World` (line 694), then HUNG in its failure path (do not call Init without
  the logic worlds).
- Fix (host wiring, env-gated RC_HOST_RLLOADER=1): write the game's own K3EngineMgr into
  `singleton+0xB0` (the field Init sets) and skip Init. Result (host_char_rl10.out):
  `NewScene(mgr,1) -> 0x0 scene=0x2217BE21D18`, `GetRLScene -> non-null`, full run exit 0
  (screenshot written). **RL scene gate closed.**
- Remaining: `scene -> local character` (rep+0x58CE20) returns 0; `core(roleType=6)`
  returned 0 this run. Next: disassemble 0x58CE20 to learn the character requirement,
  then CreateHangPet (0x42D1F0) -> visible player.
- Evidence: host_char_rl10.out, rl_oninit.txt (Param fill), rl_initbody.txt (asserts).

## 2026-10-04 — Character gate: scene has id 0 / no main-scene + local player; UGC path identified

- Local-character chain (rep+0x58CE20): `sceneId = [scene+0xF1970]`; `0x924B(id)` =
  `id==0 ? GetMainScene(0x1F9DD) : GetScene(singleton+0x24F40, id)`; then
  `[scene+0xF29E8]` (local-player field), `0x1B9D7(scene)`, `[x+0x20]+0x70`.
  Host probe (host_char_rl12.out): `char chain: sceneId=0 world=0` - our NewScene scene
  has id 0 and is not the main scene, so the lookup returns null.
- Scene constructor (rep+0x588F40, no direct callers - vtable/thunk) initializes
  `[scene+0xF1970] = 0` (id assigned later); only writer of +0xF1970 in the DLL is that
  constructor.
- `KRLScene::GetPlayerAround` (0x58D0A0) uses the same local-player chain.
- Standalone-character candidates (game's own preview paths):
  - `KRLUGC::CreateScene` / `KRLUGC::CreateMainCharacter` (0x33A620; asserts pMainScene +
    pRLCharacter via 0x134F3 -> GetLocalPlayerCharacter) - UGC = the character-preview
    system (MovieEditor uses this class).
  - `LuaCreateHangPet` (0x5BE120) requires `pScene->m_pHangPetWorld` + a local character;
    `pCharacter->SetHangPet(nType, pet)` attaches (string 0xCBECC8).
- Next probes: (a) find how the main scene + local player are created in the game
  (KRLSceneMgr::Append 0xCBB010; KGameWorldHandler::OnNewCharacterDisplayData) or
  (b) drive the UGC path (KRLUGC::CreateScene -> CreateMainCharacter) which is the
  editor/preview path designed for hosts without a game world.
- Evidence: host_char_rl12.out, rl_world.txt (0x924B), rl_scenehead2.txt (ctor),
  rl_ugc1.txt (CreateMainCharacter).

## 2026-10-04 — CreateRLScene needs full Init; complete required-Param list; exe SO3World creation located

- `CreateRLScene` (rep+0xB0B5C0 = `KGameWorldHandler::NewScene`; also used by
  CreateNewReplayScene 0xB023F0, NewUIScene 0xB19570, KRLUGC::CreateScene 0x33A760 via
  thunk 0x53FD) is the game's real scene creation: registers the scene id, calls the
  engine-manager NewScene (0x16DB5), and requires `singleton+0x100` (m_pSO3World) plus
  `[mgr+0x260]` (a facade-held manager) - both null in this host. Called in-host it
  faults (SEH-caught, run stable exit 0); fallback direct NewScene still yields an
  unregistered scene (id 0), chain `sceneId=0 world=0`.
- Complete SO3Represent::Init required-Param list (assert chain 0x3E6093-0x3E637B):
  +0x08 p3DEngineManager, +0x10 p3DModelManager, +0x18 p3DEngineXLogic,
  +0x20 p3DSceneResponseMgr, +0x28 p3DResourceConverter, +0x30 p3DMovieCore,
  +0x38 p3DUI, +0x40 pDispatcher, +0x68 pRLUIHandler, +0x70 pSO3World,
  +0x78 pSO3WorldClient, +0x90 pEventCommonMgr, +0x98 pLogicEventMgr,
  +0xA0 pRepresentEventMgr, +0xC8 pStepCtrl. The RL scene/character path needs the full
  game stack (logic worlds + event managers + dispatcher + UI handler), i.e. the game's
  own bring-up.
- The game exe's SO3World creation found (0xB034B): `new(0x567408)` + ctor `0x16EAD0` +
  `g_pSO3World` (exe global 0x15855B8) + `world->Init` (0x173F00) with three objects from
  the exe's module holders (`[holder+0x18]`). Logic module alternative: SO3World ctor
  0x125A00 + init 0x12B7E0 (JX3LogicEditOperationX64, needs InitLogic's args).
- Next options: (a) study MovieEditor's working represent host (MovieEngineCLR
  KGRepresentHelper::InitRepresent + its JX3RepresentX64 build) - a reference for
  rendering characters without a game world (AGENTS: host-behavior evidence);
  (b) boot the exe's own game stack (SO3World + event managers + dispatcher + UI
  handler) and call the full Init; (c) drive the UGC preview path (still needs
  CreateRLScene -> m_pSO3World, so same gate).
- Evidence: host_char_rl15.out, rl_crs.txt (CreateRLScene), rl_asserts2.txt (full list),
  rl_sow3.txt (exe SO3World creation).

## 2026-10-04 — RL unit path works: GetRepresentIDFromPath -> 'F1', GetUnit('F1') -> live unit

- MovieEditor represent host study: MovieEngineCLR.dll (C++/CLI) references only
  `CreateRLLoader` (no CreateSO3Represent, no UGC, no CreateMainCharacter) - its
  character-render path is in managed IL, not statically visible without an IL dumper
  (no ildasm on this machine; no new deps without asking).
- JX3LogicStandaloneX64.dll is self-contained **table-parser tooling** (`JX3Represent::Init`
  -> reads `represent/doodad.txt` via LoadDoodad/LoadOneDoodad into an internal list),
  not a renderer - it is the converter-side reader.
- KGRLLoader vtable (rep+0xC9C668) fully resolved via thunks:
  [0] 0x3F9C20 update, [1] 0x3F4E50 GetUnit wrapper, [2] 0x3F4EF0 GetUnit fetcher,
  [3] 0x3F5540, [4] 0x3F9D60 ReleaseUnit, [5] 0x3F9EF0, [6] 0x3F5620 LoadPlayerAllModel,
  [7] 0x3F4870 GetEquipmentScale, [8] 0x3F5420, [9] 0x3F4540, [10] 0x3F49F0,
  [11] 0x3F4680, [12] 0x3F4790, [13] 0x3F4E40, [14] 0x3F44A0,
  [15] 0x3F4C60 GetRepresentIDFromPath, [16] 0x3F51C0 GetUnitFromPath,
  [17] 0x3F9940 LoadUnitFromFile, [18] 0x3FA100.
- Signatures (from IL): `GetRepresentIDFromPath(this, szPath, char* outIdStr, count)`
  -> bool, writes the id as text (leading '0' trimmed, '.' terminator);
  `GetUnit(this, out{dword id; void* unit}, const char* id)` (slot 1) wraps
  slot 2 `GetUnit(this, const char* id)` -> unit pointer.
- **Working in-host (host_char_rl19.out, exit 0)**:
  `GetRepresentIDFromPath("Data\source\player\F1\部件\Mdl\F1.mdl") -> 1 id='F1'`;
  `GetUnit('F1') -> unit 0x294CF23EFC0` (live, stable across runs). The player model is
  now reachable through the game's own RL loader API - no game world needed.
- Unit object: first qword = 0x00000001524C3030 (id/flags, NOT a module vtable) - the
  unit is likely POD KRL data; find its class via ReleaseUnit (0x3F9D60) and the
  scene-attach path next.
- Next: find how the RL scene turns a unit into a render entity (scene actor/ECS
  component from unit), attach the F1 unit to the scene, verify render
  (image_stats fingerprint). Evidence: host_char_rl19.out, rl_getunit.txt.

## 2026-10-04 — MovieEditor path confirmed: represent WITHOUT full Init; player-parts core creator located

- MovieEditor `MovieEngineCLR.dll` (`KGRepresentHelper::InitRepresent`) uses only
  `CreateRLLoader` (strings 0x2B82F8-0x2B8360) - no SO3Represent::Init. Its character
  APIs: `InitPlayerModel`, `GetPlayerModel`, `SetFaceModel`, `ReleasePlayerModel`
  (+ `KGSceneCLR::LoadModelByRepresentID` / `AddRepresentModel`). This confirms the
  standalone RL path (CreateRLLoader + player model APIs) is the intended
  "character without a game world" route; the 15-object Init is not needed for it.
- `LuaLoadPlayerParts` (0x41C210) = look up the HangPet core by id (thunk 0x10000 ->
  0x42D860 map lookup, `it->second` at +0x28) then `0x16054 -> 0x422F50(core, partsA[13],
  partsB[13], count)`. The core must exist first - created by `HangPetWorld::_CreateCore`
  (assert 0x42E548): alloc 0x788 (core) + `0xFA1(core, cfg, ...)` Init + map insert.
- Host error fixed conceptually: the core is NOT created by 0x42D860 (lookup); the
  creator `HangPetWorld::_CreateCore` (function containing 0x42E495/0x42E548) is the
  next call target, then `0x422F50` (LoadPlayerParts) assembles the player model.
- Status: player character still NOT visible; not ready to show. Next probes:
  (a) call `HangPetWorld::_CreateCore(ctx, cfg, ...)` + `LoadPlayerParts` in the host
  (game's own player assembly), or (b) reproduce MovieEditor's `GetPlayerModel` path
  (RL loader GetUnit/model proxy + scene add).
- Evidence: MovieEngineCLR string map, rl_lpp.txt (binding), rl_createcore.txt.

## 2026-10-04 — CreateHangPet called in-host: master+frame-data requirement identified (HangPetCore::Init)

- Wired the Lua binding's exact call in the host:
  `CreateHangPet(ctx, sceneId, 6, 0, NULL, 1, &cfg)` (0x42D1F0 -> 0x42E2B0). Result: NULL
  with the represent KGLOG errors (IAT-patched into host log):
  `KGLOG_PROCESS_ERROR(m_pMaster && m_pMaster->m_pFrameData) at line 96 in HangPetCore::Init`
  and `KGLOG_PROCESS_ERROR(pNewPetCore->Init(pConfig)) at line 65 in HangPetWorld::_CreateCore`.
- `HangPetCore::Init` (0x421F20): master = `[core+0x6B0]` (the character arg, only a map key
  for lookup but REQUIRED here); `m_pFrameData = [master+0x39F0]` must be non-null; then it
  creates three RL actors `m_rlPet` ([core+0x130]), `m_rlItem` ([core+0x3F0]),
  `m_rlFace` ([core+0x290]) via `Create(g_pRL->GenerateID(m_dwSceneID), type)`, and asserts
  `sRoleConfig.szRoot && sRoleConfig.szMdl`.
- Creator call: `Init(core, world, sceneId, representID, arg4, nType, character)` - the cfg
  (arg7, stored [rsp+0x60]) is not passed to Init directly (used elsewhere in _CreateCore).
- To finish the standalone HangPet path: (a) provide a master object with
  `[master+0x39F0]` = valid frame data + the role config (szRoot/szMdl) that reaches Init,
  or (b) reproduce MovieEditor's `KGRepresentHelper::GetPlayerModel`/`InitPlayerModel`
  (RL model-proxy API, no character/master needed).
- Host state: probe stable, exit 0; scene from fallback NewScene; character still not
  visible. Evidence: host_char_rl16.out, rl_hpcinit.txt, rl_chp4.txt.

## 2026-10-04 — Fake-master probe: passes the master/frame-data assert, faults deeper in Init

- Host probe updated: CreateHangPet called with a fake master (static 0x8000 buffer,
  `[master+0x39F0]` = zeroed 0x400 frame-data) and cfg {szRoot="Data\source\player\F1",
  szMdl="Data\source\player\F1\部件\Mdl\F1.mdl", szBone="", fScale=1.0}.
- Result: the `m_pMaster && m_pMaster->m_pFrameData` assert is PASSED (no KGLOG error this
  run), but the call faults deeper inside Init - VEH `0xC0000005 at 0x7FFE21E417B0`
  (module not the rep/engine/X3D/convert; likely a late-loaded dependency such as the
  scene-response/movie/UI DLLs), caught by the block SEH ("deferred RL probe fault").
  Screenshot fingerprint changed vs the NULL-master run (sha/mean differ) but no character
  is identifiable in the 4-region stats - not proof of a rendered model.
- This is a registered deviation (fake master); re-open with a real local character once
  the logic world exists.
- Next: (a) identify the faulting module/offset (log module list or minidump) and satisfy
  the specific missing init; or (b) switch to MovieEditor's `KGRepresentHelper::
  GetPlayerModel`/`InitPlayerModel` (RL model-proxy path, no master).
- Evidence: host_char_rl17.out, image_stats on host_char_rl17.png vs host_char_rl16.png.

## 2026-10-04 — PLAYER UNIT FOUND: GetUnit('F1') returns a valid KGRL unit via the RL loader

- Host unit probe (fixed): `GetRepresentIDFromPath("F1.mdl") -> 1 id='F1'`;
  `GetUnit('F1') -> 1 unit=0x...` (vt[1] and vt[2] both non-null). The unit starts with
  magic "RL00" + version 1 (bytes 52 4C 30 30 01 00 00 00) - a KGRL data struct, not a
  vtable object (the earlier "unit vtable" fault was a host logging bug reading the magic
  as a pointer; guard fixed to detect "RL00").
- Full KGRLLoader vtable mapped (19 slots): [0] 0x3F9C20, [1] 0x3F4E50 GetUnit,
  [2] 0x3F4EF0 GetUnit, [3] 0x3F5540, [4] 0x3F9D60 ReleaseUnit, [5] 0x3F9EF0,
  [6] 0x3F5620 LoadPlayerAllModel, [7] 0x3F4870 GetEquipmentScale, [8] 0x3F5420,
  [9] 0x3F4540, [10] 0x3F49F0, [11] 0x3F4680, [12] 0x3F4790, [13] 0x3F4E40,
  [14] 0x3F44A0, [15] 0x3F4C60 GetRepresentIDFromPath, [16] 0x3F51C0 GetUnitFromPath,
  [17] 0x3F9940 LoadUnitFromFile, [18] 0x3FA100.
- VEH handler now logs the faulting module+offset (`GetModuleHandleExW(FROM_ADDRESS)`);
  the CreateHangPet fault is VCRUNTIME140+0x17B0 = an internal C++ exception-unwind helper,
  i.e. game code THREW during Init (secondary fault in EH), not a raw null deref.
- Remaining: turn the KGRL unit into a rendered RL actor/model - the MovieEditor's
  `KGRepresentHelper::GetUnitModel` + `KGSceneCLR::AddRepresentModel` path (its native
  code in MovieEngineCLR.dll) is the working reference.
- Evidence: host_char_rl19.out, rl_slots.txt, image/VT maps.

## 2026-10-04 — MovieEditor IL read (mddump tool); F1-as-NPC worldObject test; path-driven player flag cleared but no render

- Built `%TEMP%\opencode\skillv2\mddump` (dotnet 5 console + System.Reflection.Metadata,
  no external deps) - reads mixed-mode assemblies (PEReader + MetadataReader, IL bytes,
  call-token resolution). Extracted MovieEditor native RVAs:
  `KGRepresentHelper.InitRepresent` 0x25AA64, `GetUnitModel` 0x25DE9C,
  `InitPlayerModel` 0x259DBC, `GetPlayerModel` 0x25D980, `UnitModel.{ctor}` 0x25C7BC,
  `KMovieObjectHolder.{ctor}` 0x118720, `NewObject` 0x1189C0/0x1188B0/0x118A20,
  `GetModel` 0x118820.
- `GetPlayerModel` IL: `new KMovieObjectHolder` -> 6x `NewObject(sprintf_s names)` ->
  `GetModel` -> caches `IKG3DModelProxy*` in maps keyed by unsigned long. The MovieEditor
  character path = KMovieObjectHolder + engine model proxies (ME stack), not the client
  RL/KRLCharacter stack.
- Client rep's direct model pipeline (found; belongs to the Homeland system):
  `m_piSNE` ([g_pHomeland+0x400888]) -> `Node_Create` / `Model_CreateCommonModel(&hModel,
  szModel, ..., szAnim)` / `Node_SetUserData` / `Node_AddModel` (asserts 0x8D1360 block).
- Experiment: copied `data\source\player\f1` -> `data\source\npc_source\f1` (23 files)
  and pointed the map's worldObject `comRender.actorModel` at the npc_source path.
  Result: the path change DID clear the player classification (SO flags 0x00010190 ->
  0x00000190; the player bit is path-driven), the model files load
  (`wrapper loose-exists` + `LoadFile`), but it still does NOT render
  (keepCheck 0; the keep-mesh registry is PakV4-sourced - loose tab ignored; render-data
  build 0xC4DE80 never runs). Screenshot fingerprint unchanged. Map entity JSON restored
  from backup (`000_000.json.bak_f1npc` kept in the client_root).
- Conclusion: the F1 player-model render in the client engine needs the represent layer
  (game-stack entangled) or a pak-level keep-check entry; the MovieEditor-engine path is
  the proven character renderer.
- Evidence: me_getunit.txt, mddump output, host_f1npc1.out, host_f1npc1.png stats.

## 2026-10-04 — Direction clarified (real client engine only); RL-name cfg attempts still fault (needs a real master)

- Direction: the product path is the REAL GAME CLIENT engine (`zhcn_hd\bin64`: JX3RepresentX64,
  Engine_Lua5X64, KG3DEngineDX11EX64) - `client_host` already runs it. MovieEditor is a
  reference/visual resource only; the earlier "pivot" suggestion was withdrawn.
- Probe: `CreateHangPet` with RL-style cfg names (szRoot/szMdl = "F1" / "F1.mdl" /
  "Represent/player/F1...") - all three attempts fault (SEH, returned -1); the HangPet core
  requires a real master (character) and its RL actor creation throws - the represent/player
  system wall remains (same as the 15-object Init).
- Next client-engine probe: log C++ exceptions (VEH 0xE06D7363 + exception object type name)
  to identify exactly what `HangPetCore::Init` needs, then supply it.
- Evidence: host_char_rl20.out.

## 2026-10-04 — Full round: render gate isolated; SO3World path found (Init_ForEditor); plan doc added

- Render gate proven by hooking keepCheck (0xC4D430): props/skybox = 1 (rendered),
  ALL character meshes (npc_source/player) = 0. Registry is PakV4-sourced. Host force
  `keepCheck=1` for f1_3094 builds render data (flag484=0x01, kept buffers) but the draw
  still does not happen.
- JSON worldObjects (entities/sceneinfo_full/000_000.json) create SOs but are NEVER
  drawn - even a full rendering-prop record at the spawn (the render set comes from the
  map .SRScene binary). The manual engine actor (CreateActorFromFile) has a valid,
  render-ready model but no SO in the scene -> not drawn. The entity SO has a render
  proxy (AcqureRenderActorProxy ok) and forced flags 0x8180/renderVisible - still no draw.
- Conclusion: the client engine draws the static merged world only; characters require
  the represent layer (SO3Represent::Init / CreateRLScene), which needs the game logic
  stack.
- Progress toward the logic stack: `KSO3World::Init_ForEditor` (logic module 0x12B7E0,
  2nd arg unused) + `KMemory::Initialize` (Engine_Lua5X64 export) - after the memory init
  the SO3World allocation works (allocator fault gone); Init_ForEditor now faults in a
  null hash-map lookup (logic +0xE0998, rbp=NULL) - a logic-module global set by its own
  module init (CreateJX3LogicOperation -> 0x8B6B0), which hand-construction skips.
- Also: X3DEngine `GetNativeFileBundle()` returned null (facade field set later).
- Added `docs/engine_host/CLIENT_CHARACTER_PLAN.md` (phases A-D, registered in the area
  README). Map entity JSON restored to pristine (backup kept).
- Evidence: host_f1keep*.out, host_prop_*.out, host_fullprop.out, host_so3world2.out.

## 2026-10-04 - Phase A attempt: CreateJX3LogicOperation wedges

- Called the logic module's own entry CreateJX3LogicOperation(basePath, factory,
  name) in the host (behind new flag RC_HOST_LOGIC=1): the module loads, then the call
  WEDGES (no return, process had to be killed) - likely an internal job/thread wait or
  the dummy factory arg. The flag keeps other probes unaffected.
- Args decoded from KGJX3LogicOperation::Init (0x8B6B0): arg1 = base path (used for
  %s\\logs), arg2 = stored to logic global 0x975144, arg3 = name; it then calls
  InitLogic (0x113150) which creates g_pSO3World (logic+0x9C1320) etc.
- Next: instrument the wedge (thread/backtrace or stub the factory), or call InitLogic
  (0x113150) directly after satisfying its piRecorderFactory assert.

## 2026-10-04 - Phase A: logic init runs async (host stable); wedge is in the represent/convert-module step

- CreateJX3LogicOperation now runs on a worker thread (host no longer hangs; main thread
  keeps the engine frame loop pumping). The init thread still does not complete.
- Thread stack dump (frame40, RC_HOST_LOGIC=1) decodes the wedge: the stack carries the
  InitLogic strings for the **JX3DoodadRepresent -> LoadConvertModule(JX3RepresentX64.dll)
  -> m_hConvertModule** step (0x1133C0 region) plus the represent module name - i.e. the
  init blocks around loading/initializing the represent/convert module, consistent with
  the represent's job-processor lock (the same one that hangs singleton vt[1]).
- Host diagnostics added: logicInitThread + per-frame thread-stack dump (describeAddr for
  eng/rep/logic/x3d/lua) + frame60 deferred CreateRLScene (fires only once the world is
  ready).
- Next: pump the represent module while the init runs (call RLLoader::Update / the
  represent frame update each frame) or find the specific lock the init waits on.

## 2026-10-04 - Phase A: RL loader pump does not unblock the logic init

- Added a per-frame RLLoader::Update pump while the logic init thread runs - the init
  still blocks (frame40 stack dump shows it wedged at the same represent/convert-module
  step). The wait is not the loader job lock.
- Host is stable with RC_HOST_LOGIC=1 (async init, frame loop completes).
- Next: hook the logic module's LoadConvertModule (0x112FC0) / the represent load step
  to see the exact blocking call, or bypass that step.

## 2026-10-04 - PHASE A BREAKTHROUGH: the wedge was a modal MessageBox (malformed base path)

- Root cause of the CreateJX3LogicOperation wedge: the module formats paths as
  "%sbin64\%s" - our base path lacked the trailing backslash, so
  LoadConvertModule(JX3RepresentX64.dll) failed and its error path called **MessageBoxA**
  (identified via the IAT slot 0x765CE0), which blocks forever in a headless host.
- Fixes (host): base path passed with a trailing backslash; MessageBoxA/W IAT-patched in
  the logic module (suppressed + logged).
- Result: the logic init runs: **g_pSO3World created** (non-null, written into
  represent singleton+0x100 at frame60), its RL loader streams the player tables
  ([rl-bin-tab] Represent/player/equip/...), PB/event tables init, then the thread
  faults in a represent std::map destructor (rep+0x3FB265) called from
  KGJX3LogicOperation::Init (logic+0x8B880) - a cleanup crash after a partial init
  (g_pRLLoader still 0).
- Stack-dump tooling in host: dbghelp StackWalk64 + executable-section filter
  (isCodeAddr) + per-frame thread dump; VEH logs faulting module+offset.
- Next: identify the failing sub-init that triggers the destructor cleanup (or guard the
  destructor) so the logic init returns 1; then Phase B (full Param).

## 2026-10-04 - PHASE A DONE: logic module init completes; Phase B iterating (pSO3WorldClient next)

- Patched KGJX3LogicOperation::Init (logic+0x8B7DB) to jump to its success epilogue
  (0x8B8EF), skipping the game-context step that runs before the represent Init and
  faults (heap corruption). In-memory host adaptation (registered; re-open once the
  represent Init runs before it, i.e. the real game order).
- Result: `CreateJX3LogicOperation -> non-null` (op object created), `g_pSO3World`
  created, init thread completes cleanly, no heap corruption. Phase A done.
- Phase B (frame60 SO3Represent::Init with the world): Init now runs and its assert
  chain reports the next missing Param field:
  `KGLOG_PROCESS_ERROR(Param.pSO3WorldClient) at line 697 in SO3Represent::Init`.
  (MessageBox suppression makes failed Init return/name the object instead of hanging.)
- Remaining Param fields to supply: pSO3WorldClient (+0x78, class
  KGSO3WorldClientInterface in the logic module), pDispatcher (+0x40), pRLUIHandler
  (+0x68), pSO3UI (+0x90? +0xE0?), pEventCommonMgr (+0x90), pLogicEventMgr (+0x98),
  pRepresentEventMgr (+0xA0), pStepCtrl (+0xC8).
- Note: the last run's process hung after the Init failure (killed) - the failure path
  can still block; keep runs timeout-guarded.
- Evidence: host_logic14.out (init ok + pSO3WorldClient assert).

## 2026-10-04 - Phase B: three more Param objects resolved from the logic module; dummies crash in Init

- pSO3WorldClient (+0x78) = **static KGSO3WorldClientInterface in the logic module**
  (getter 0x440B60 = lea rax,[rip+0x56C091] -> logic+0x9ACBF8).
- pSO3UI (+0x60) = logic `g_pUI` (InitLogic stores at logic+0xA00D80).
- pRLUIHandler (+0x68) = logic `g_pGameWorldUIHandler` (logic+0x9C1328, adjacent to
  g_pSO3World at 0x9C1320).
- Init now passes those asserts; the remaining required fields are pDispatcher (+0x40)
  and the three event managers (+0x90 pEventCommonMgr / +0x98 pLogicEventMgr /
  +0xA0 pRepresentEventMgr) plus pStepCtrl (+0xC8). No standalone globals found for them
  (the exe passes holder-member pointers); with non-null dummies Init passes the asserts
  but faults inside the represent (rep+0x3DE628; the map-file lookup rep+0x80D728 also
  appears) - the dummies are used.
- Host: frame60 Param fill now includes the three real logic objects; run stable exit 0.
- Next: find the real dispatcher + event managers (logic-module classes or
  represent-created objects; the exe's fill reads them from its module holders), replace
  dummies, iterate until SO3Represent::Init returns 1; then CreateRLScene (Phase C).
- Evidence: host_logic15-18.out.

## 2026-10-04 - Phase B: stub managers get Init deeper (rep+0x3DE628 -> 0x3E53CD) but fault in their lock-free internals

- Host now builds stub manager objects (valid vtable of no-op methods returning 0) for
  pDispatcher/pEventCommonMgr/pLogicEventMgr/pRepresentEventMgr/pStepCtrl. With the
  zeroed stepCtrl the Init faulted at rep+0x3DE628 (copy-ctor on a null vtable); with
  stubs it advances to rep+0x3E53CD (a lock-free pool/list traversal with a corrupted
  node) - the managers have real semantics; stubs are not viable.
- The real classes are **exe-module classes**: KJX3LogicEventModule::Create (exe 0xAFE00
  region), KJX3RepresentEventModule::Create (0xBAAxx), KEventCommonMgr methods (0x96xxxx
  strings). Not present in JX3LogicEditOperationX64.dll, JX3RepresentX64.dll,
  JX3UIX64.dll or JX3ClientX64Base.dll.
- Options for the next session: (a) load JX3ClientX64.exe as a module with manual IAT
  resolution and call its module Create functions (faithful but heavy); (b) find the
  manager creation elsewhere (UI module Param?). The rest of the Param (world, world
  client, UI, UI handler) is real and passing.
- Evidence: host_logic19/20.out, rl_lem2.txt (exe module classes).

## 2026-10-04 - Exe-as-module path opened: mapped + IAT resolved + initializers; static-init crash is the next gate

- RC_HOST_EXE=1: `LoadLibraryExW(JX3ClientX64.exe, DONT_RESOLVE_DLL_REFERENCES)` maps the
  game exe in-process (0x7FF77F180000), manual IAT resolution works (873 imports, 0
  failed), the exe's trivial getter executes, and `KJX3LogicEventModule::Create` is
  callable (it reached its internals).
- Create faulted at exe+0xA3C58 reading an uninitialized exe global (0x142C1F0) - the
  exe's C++ statics are not constructed because its CRT startup never ran. Located the
  CRT init arrays from mainCRTStartup (entry 0x79BA78 -> 0x79B904):
  C++ .CRT$XC 0x7B9CC0-0x7BA4C0, C .CRT$XI 0x7BA4C8-0x7BA4E8. Running them: the first
  initializer (exe+0x79B8E8) succeeds, the second (exe+0x843EC, a subsystem global ctor
  at 0x110428) crashes the process with heap corruption (0xC0000374) - the exe's
  initializers are full game subsystems that assume the complete game process context.
- NOTE: do NOT patch the exe's allocator (its malloc/free route to the shared UCRT heap;
  a patch caused the corruption in the first attempt).
- Next: selective initialization - run only the initializer(s) that construct the
  globals the event modules need (e.g., the global whose ctor is 0x110428 / 0x142C1F0's
  owner) instead of the whole array; or call the specific global ctors directly before
  the module Create calls. Then KJX3LogicEventModule::Create / RepresentEventModule /
  KEventCommonMgr -> the three managers -> finish the Param -> Init returns 1.
- Evidence: host_exe1-3.out.

## 2026-10-04 - Phase B: ALL Param objects now REAL via the game exe's own modules (stubs retired)

- Exe-as-module path fully working (RC_HOST_EXE=1, RC_HOST_EXE_NOINIT=1):
  `LoadLibraryExW(DONT_RESOLVE)` + manual IAT (873/0) + NO static initializers
  (they crash: game subsystems) + CRT helper stubs for the magic-static guards
  (patch exe 0x79B6E0/0x79B680/0x79B3F0 -> host stubs; force guard globals
  exe+0xA8F411/0xA8FFC1/0xA8D670 = 1).
- Created by the exe's own creators and wired into the Param:
  - pDispatcher (+0x40) = dispatcher module Create 0xB2FA0 -> [exe+0xA8C220]+0x18
    (the script dispatcher; the module stores itself at 0xA8C220).
  - pEventCommonMgr (+0x90) = KJX3CommonEventModule Create 0xA4700 +
    OnInitialize 0xA42F0 -> manager exe+0xA8D680.
  - pLogicEventMgr (+0x98) = KJX3LogicEventModule Create 0xAFD60 +
    OnInitialize 0xAF990 -> manager exe+0xA8F420.
  - pRepresentEventMgr (+0xA0) = KJX3RepresentEventModule Create 0xBA7D0 +
    OnInitialize 0xBA430 -> manager exe+0xA8FFD0.
  (All Create functions store the module in the exe holder slots 0xA8C210/0xA8C1C0/
  0xA8C260/0xA8C220; manager = module+0x18.)
- SO3Represent::Init now receives only real objects (stub objects retired) but still
  faults at rep+0x3E53CD (a lock-free pool/list traversal) - the suspect is an
  INCOMPLETELY initialized real object: the SO3World comes from the patched logic init
  (game-context skipped) and/or the common manager's OnInitialize faulted partway
  (after setting the manager, before finishing its member init).
- Next: verify/complete each real object (finish the common manager's OnInitialize
  member init; check the SO3World's completeness), then Init should return 1.
- Evidence: host_exe1-18.out.

## 2026-10-04 - Init fault chain pinned: stepCtrl (Param+0xC8) needs a real vtable[2] factory

- VEH stack trace (host, in-rep faults) at the Init fault:
  bt[4] = rep+0x3E53CD (fault), bt[5] = rep+0x3DE631 (inside the copy-ctor 0x3DE610),
  bt[6] = rep+0x3E64EE (Init, right after `call 0x3DE610` at 0x3E64E9).
- Init code: `rsi = [rdx+0xC8]` = pStepCtrl; at 0x3E64E6 `rcx = rsi`; `call 0x3DE610` -
  the copy-ctor allocates the destination via `[rsi->vtable + 0x10]` (vtable[2]) and
  `call 0xD544` (thunk), then copies 0x88+ bytes. With our stub stepCtrl, vtable[2]
  returns 0 -> null write -> fault.
- So the LAST missing piece is a real **stepCtrl** object whose vtable[2] is a factory
  allocating 0xE8-byte objects. The exe's own Initialize passes `&local` (rbp+0x88 =
  rsp0+0x10 area) and never constructs it in the code scanned - the object must be
  constructed by a call not yet identified, or the local is a structure the represent
  Init fills. Next: locate the stepCtrl's construction/type (scan the exe Initialize
  again for writes to the local via rsp-relative addressing, or find the object class by
  the vtable[2] factory's allocation size 0xE8).
- Host file restored after a shell-edit corruption (git checkout b399993) + VEH stack
  trace re-added.
- Evidence: host_exe21/22.out.

## 2026-10-04 - PHASE B COMPLETE: SO3Represent::Init(Param) -> 1

- `SO3Represent::Init(Param) -> 0x00000001` (host_exe29.out): the full represent Init
  succeeds with all-real objects. After Init: `singleton+0xB0 = engine manager`,
  `singleton+0x100 = SO3World` (both set by the game's own Init from the Param).
- Final pieces of this round:
  - pStepCtrl decoded: the Param value is the control buffer itself; `[buf]` -> an
    object whose `+0x10` is a pool allocator ([0]=block size, [8]=free list). Block
    size 0 makes the allocator take the operator-new fallback (rep+0x3E5387).
  - The copy-ctor (rep+0x3DE610) then tail-calls a list append (rep+0x3E52A0) with
    `this = [buf]` - so that object must be a full zeroed 0x200 buffer (list at
    +0x70/+0x78/+0x80). Host builds: stepBuf(0x200, [0]=stepA) + stepA(0x200,
    +0x10=stepAlloc(0)) + Param+0xC8 = stepBuf.
  - The represent's fallback allocator (operator new at rep+0xB74A10) is patched to a
    zeroing calloc (repCallocNew) so fresh objects' list fields are 0.
- Phase B closed. Next (Phase C): CreateRLScene after the successful Init (the
  frame60 attempt still faults - check the post-Init scene-creation path).
- Evidence: host_exe29.out.

## 2026-10-04 - Phase C start: CreateRLScene needs [mgr+0x260]; module inits do not set it

- After the successful Init, CreateRLScene faults at rep+0x80D728: `[mgr+0x260]` null
  (0x80D710: mgr2=[mgr+0x260]; mgr2->vt[0x13](type) -> container; ->vt[7](name) ->
  item ->vt[0xB]() = the map-file resource).
- Found the adapter (KG3DEngineAdapterX64) constructs a 0x4C0 manager object whose
  +0x260 is set (0xF5940; called from 0xE64F0's function which builds/inits it) - the
  object type matches, but our host's Get3DEngineInterface object (engIface) and the
  X3DEngine facade both have +0x260 == 0.
- Called the exe's own module inits (all return 1, stable):
  KJX3RenderModule::Create 0xB8E40 + Initialize 0xB7D20 states 1/2/4 (LoadX3DEngine +
  facade/xlogic holder fields), KJX3ConvertResourceModule::Create 0xA65C0 +
  OnInitialize 0xA6470, KJX3CommonEventModule (manager exe+0xA8D680),
  KJX3LogicEventModule, KJX3RepresentEventModule, script dispatcher (0xB2FA0) - none
  sets facade+0x260.
- Next: find the setter of the facade's +0x260 (the game's engine bring-up: possibly
  the exe's PreInit path 0x9E121, another module init, or the adapter's manager object
  used as the Param's p3DEngineManager); or create the adapter's 0x4C0 manager via
  0xE6300 and pass it.
- Evidence: host_exe30-33.out.

## 2026-10-04 - Phase C: RL scene created AND registered in the singleton scene vector

- Decoded the scene map: a std::vector<Scene*> at singleton+0x24F40 (+0x18 begin,
  +0x20 end, +0x28 cap); GetScene (0x597740) scans it comparing [scene+0xF1970]==id;
  0x22E08 -> 0x5977D0 is the find/remove path (not the insert).
- Host Phase C sequence (frame60, after the successful Init):
  1. `0x22E08(map, 2)` (stale-slot op),
  2. `0x16DB5(mgr, 1, &scene)` -> engine scene created,
  3. `[scene+0xF1970] = 2`,
  4. push the scene pointer into the vector (host-side vector push incl. growth),
  5. `GetRLScene(2) -> scene` **non-null** (verified).
- Remaining gaps for a visible character:
  - `[scene+0xF1978]` (m_p3DScene) = 0 - the RL scene has no engine 3D scene attached
    (the game sets it via the map-file resource; the CreateRLScene resource-manager
    lookup [mgr+0x260] is the same gate).
  - `[world+0xF29E8]` (local player) = 0 and `local character (0x58CE20)` = 0 - the
    local player is created by the game's logic world.
  - CreateHangPet with the fake master throws (VCRUNTIME EH fault) - needs the real
    master/character + the scene's 3D scene.
- Evidence: host_exe35/36.out.

## 2026-10-04 - Phase D start: real NewExScene creates the RL scene (135MB) + attaches the 3D scene

- The RL scene map is a std::vector<Scene*> (singleton+0x24F40, +0x18/0x20/0x28);
  GetScene 0x597740 scans by [scene+0xF1970]==id; 0x22E08 -> 0x5977D0 is find/remove.
- The REAL scene creation lives in CreateRLScene's NewExScene tail (0xB0BA20+):
  alloc 0x8105850 (~135MB) + ctor (0x1ADCF/0x588F40), then
  [rlScene+0xF1978] = the engine 3D scene, [rlScene+0xF1970] = id, and scene inits
  (3D-scene vt[0x298]/vt[0x388], 0x164F(rlScene, mapFileObj, ...)).
- The map-file object (r14) comes from the resource lookup (0x16A09 -> 0x80D710 ->
  [mgr+0x260]) and is required by the RL scene init.
- Probe: the adapter stores its embedded sub-manager at [mgr+0x260] = mgr+0x30
  (adapter 0xF5B2F); setting the same on the X3DEngine facade did NOT satisfy the
  lookup (real CreateRLScene still faults at rep+0x80D728 - needs the actual resource
  manager object, not the facade's embedded field).
- Host Phase C/D sequence kept: manual engine-scene creation + vector push gives a
  GetRLScene(2) hit, but that object is the ENGINE scene, not the RL scene (3DScene
  field garbage) - the char chain faults on it. The proper path is the real
  CreateRLScene once [mgr+0x260] is real.
- Evidence: host_exe37.out.

## 2026-10-04 - Phase D: the resource lookup's real `this` is singleton+0x1A0; candidates narrowed

- Corrected: CreateRLScene's lookup call is `lea rcx,[singleton+0x1A0]` (not the facade);
  the resource manager = `[singleton+0x1A0+0x260]` = `[singleton+0x400]`.
- Fault-chain progression as candidates were supplied (all at the lookup call site
  0xB0B807 -> 0x16A09 -> 0x80D710):
  - `[..]=null` -> fault at 0x80D728 (`mov rax,[rcx]`).
  - XLogic (facade vt[0x290], valid vtable) -> fault moved to 0x80D736: its
    vt[0x13] does not return a valid container.
  - adapter manager (0xF5940-built, +0x260 = obj+0x30) -> fault at 0x80D72B: the
    sub-object has **no vtable** ([sub]=0) - not the resource manager; its inits
    (vt[0x20]/0x38/0x48 as 0xE6300 does) did not set one either.
- So the resource manager is another class with vt[0x13](type)->container,
  container vt[7](name)->item, item vt[0xB](). Likely an exe module interface
  (KJX3FileModule / KJX3PackageModule - the name->resource lookup) or the engine's
  file/resource manager.
- Next: create KJX3FileModule (Create 0x955EB8) / KJX3PackageModule (Create 0x958238)
  and try their interfaces as [singleton+0x1A0+0x260]; then the real CreateRLScene.
- Evidence: host_exe37-43.out.

## 2026-10-04 - Phase D: resource-manager candidates exhausted so far; file module crashes

- Candidates tried for [singleton+0x1A0+0x260] (= [singleton+0x400]) and results at the
  CreateRLScene lookup call (0x80D710):
  - null -> fault 0x80D728; facade embedded sub-object -> null vtable; XLogic (facade
    vt[0x290]) -> vt[0x13] not a container getter (fault 0x80D736); adapter manager
    (0xF5940-built) +0x260 sub -> no vtable (fault 0x80D72B).
  - KJX3FileModule::Create (0xAAA10) -> **fail-fasts the host** (0xC0000409, its ctor
    needs full game state); skipped. KJX3PackageModule untested (the file module crashed
    first). Host restored to the stable state (Init=1, run exit 0).
- Next candidates: the engine's own resource manager (KG3DEngineAdapter
  Get3DEngineXLogicInterface / material system / the engine file manager), the exe's
  KJX3ConvertResourceModule interface, or the KJX3PackageModule (test alone).
- Evidence: host_exe42-46.out.

## 2026-10-04 - Phase D BREAKTHROUGH: the resource manager is the MapConverter (game's own path)

- The `[singleton+0x1A0+0x260]` resource manager is built by the game at
  rep+0x834BE0 (part of the singleton's subsystem setup, one of ~30 named
  subsystems): `mgr = CreateRLFile()->vt[2](openedFile, 1, 1)` where
  `openedFile` = the resolved path string for "MapConverter"
  ("Represent/common/map_converter.krl.txt").
- `CreateRLFile` = SemanticX64.dll export (0x1F790, no args, 0x68-byte object).
- The opener = rep+0x3F08F0 (`[rep+0xEDDFE0]+0x120` file system -> vt[0xC](name)
  -> path string). `[rep+0xEDDFE0]` is the rep main object - **non-null at runtime**
  (DLL-initialized), its +0x128 = the file system.
- SemanticX64's file open (0x231D0 -> [0x61098] hook) needs the rep's file-IO:
  `SetFileIOFunctions(rep+0x788D, rep+0x1EB0, rep+0x10DC, rep+0x18926)`
  (rep IAT 0x109AAE8; installed by the rep at 0x3E43B5/0x3E68E0 in the
  SO3Represent init region). Without it the open returns null; with it, works.
- Result (host_exe51.out): `MapConverter mgr=...05E8 [vt]=...D320` (valid vtable),
  **`real CreateRLScene` no longer faults** - the lookup executes fully.
  CreateRLScene currently returns 0 (the by-name item lookup or the map load
  did not produce a scene; GetRLScene(2)=0 afterwards) - next: disassemble
  CreateRLScene return paths + check the name/type args.
- Lesson: two wrong-offset arithmetic slips (0x678D vs 0x788D) caused a bogus
  function pointer; always byte-verify rip-relative lea targets.

## 2026-10-04 - Phase D: KGameWorldHandler::NewScene path traced to the exact blocker (vt[0x70] E_FAIL)

- With the MapConverter manager installed, `real CreateRLScene` (rep+0xB0B5C0,
  KGameWorldHandler::NewScene) executes its full lookup without faulting but returns
  edi=0 without registering: the creation path bails earlier.
- Traced the creation path (0xB0B7CF..0xB0BAE9) and replicated it step by step:
  lookup (0x16A09) -> NewScene (0x16DB5) -> **3D scene vt[0x70](mapFile,0,type,&pos,0)**
  (0xB0B990) -> alloc RL scene (0x4494 size 0x8105850 + ctor 0x1ADCF) ->
  [rlScene+0xF1978]=3D scene, +0xF1974=0, +0xF1970=id -> vt[0x298]/vt[0x388] ->
  map load 0x164F (rep+0x58D800) -> register 0x1E4E8(singleton+0x24F40, rlScene, id)
  -> 0x141CD(id, 3Dscene).
- **Blocker found: `3D scene vt[0x70] -> 0x80004005 (E_FAIL)`** - the real call bails
  at 0xB0B995 (`jns` not taken -> error path -> return edi=0, no registration).
  Everything after (RL scene alloc etc.) works when forced (fields set, vt298/vt388
  fine); the map load 0x164F faults only because the scene was never bound.
- vt[0x70] is the 3D scene's map-bind method; the scene object's vtable lives in a
  module at 0x7FFE1E... (not rep/X3DEngine/logic; likely KG3DEngineAdapterX64 or a
  sibling) - next: identify the module + slot and trace the E_FAIL cause (missing
  input for the map converter? required prior scene activation?).
- Evidence: host_exe51-57.out.

## 2026-10-04 - Phase D: vt[0x70] E_FAIL fixed (GBK path); holder tables loaded; real call reaches map load

- Root cause of the 3D scene vt[0x70] E_FAIL: the host passed the map path as a UTF-8
  literal; the engine/adapter expects the ANSI/GBK path (same as RC_HOST_MAP is
  consumed at scene creation). Fix: read RC_HOST_MAP with GetEnvironmentVariableA in
  frame60. Result: **`3D scene vt[0x70] -> 0`** (was 0x80004005).
- Next blocker: the map load (rep+0x58D800) binary-searches the holder's table at
  singleton+0x1A0+0x1E3C0 (CommonForceRelationTable data; empty in this host) and
  faulted at rep+0x56815D on the empty table. Found the game's table loader =
  **rep+0x82C3C0(rcx = holder)**, called it in frame60:
  `holder tables loaded (arr=... n=18)`.
- With both fixes the real CreateRLScene now runs deep into the map load and ends in a
  **C++ exception** (VEH: 0xC0000005 at VCRUNTIME140.dll+0x17B0 = throw machinery;
  previous fault was an AV at rep+0x56815D). Next: capture the thrown exception
  details / find the throw site in the map load chain.
- Note: rep+0x22E08 is KRLSceneMgr::Remove (asserts `it != m_apScene.end()` when the
  scene is absent) - the manual scene path logs that assert but continues.
- Evidence: host_exe58-61.out.

## 2026-10-04 - Gate 1: map load now SUCCEEDS; exact crash = RL shadow bitmap with w=0/h=0

- With the holder tables loaded, the real CreateRLScene's map load (rep+0x58D800)
  **completes**: KGLOG `load scene "..." success. cost time = 0.141s` + all 725
  SceneObject::Init logs. No AV in the map load itself.
- The crash right after: the rep-side shadow manager KRLShadowMgr
  ([rep+0xED3F18], ctor at 0x36B4xx; +0x578/+0x5D8 vector/+0x610 descriptor)
  builds the RL scene's shadow-mask bitmap. Its descriptor (mgr+0x610) has
  **w=0 h=0 rb=1024 acc=0** -> the builder (0x33C490) computes the alignment mask
  `~(h-1)` = 0 -> `memset(dst = NULL, 0xFF, ...)` -> AV inside VCRUNTIME memset
  (0x17B0). Full stack: CreateRLScene -> map load -> 0x567BE5 fn -> 0x568010 fn ->
  0x36D3D0 (KRLShadowMgr get/create) -> 0x399F -> 0x33BE40 -> 0x1FCB2 -> 0x33C490.
- The dims setter = **rep+0x5AAC40** (`SetShadowMask(mgr, ptr1, ptr2, height, width)`;
  stores [mgr+8]=ptr1, [mgr+0x10]=ptr2, [mgr+0x628]=width(5th arg), [mgr+0x62C]=height)
  - no direct callers (called indirectly); never ran in this host.
- Cause: the map's whole-scene shadow mask data is missing from the extracted sandbox;
  the engine falls back to `data/public/defaultWhite.dds` (exists) but the rep-side
  descriptor stays 0. The map's `bd\shadowparam.json` exists; the mask texture files
  do not.
- Next probes: (a) find the exact mask file the engine's ReloadWholeSceneShadowMask
  wants for the map and provide it (check the original extraction), or (b) find the
  rep-side shadow-runtime init that should call the setter and why it is skipped.
- Evidence: host_exe62-63.out.

## 2026-10-04 - Gate 1: shadow AV converted to a graceful fail (KRLScene::InitShadowScene E_FAIL); exact descriptor owner still open

- Built a documented host adaptation (shadowDescFix + installShadowDescHook): a 17-byte
  entry hook on the rep's shadow-descriptor user (rep+0x33BE40) that can fix/skip the
  zero descriptor at USE time (the shadow runtime's init resets it mid-call, so
  pre-call fixes do not stick).
- With the hook set to the function's own skip guard (acc != 0), the real CreateRLScene
  NO LONGER AVs in memset: it now fails GRACEFULLY:
  `KRLScene::InitShadowScene` -> `KGLOG_COM_PROCESS_ERROR(0x80004005)` (line 1848) ->
  `KRLScene::Init` line 151 -> `NewExScene` line 386 -> scene uninit/Remove.
  (host_exe79.out)
- The mask-bitmap build is what InitShadowScene needs; skipping it yields E_FAIL, so
  the scene creation still aborts - but cleanly (no crash, no heap corruption from the
  shadow step).
- Open: the exact descriptor owner at the crash site. The hook's identity check
  (`desc == [rep+0xED3FA8]+0x68`) never matched the crashing descriptor
  (desc 0x...5FF770 vs runtime 0x...A04DEA-based) - the crashing descriptor belongs to
  another owner in the KRLShadowMgr/runtime tree. Next probe: log every hit with the
  caller's return address (RtlCaptureStackBackTrace in the hook) to identify the owner,
  then make InitShadowScene succeed with the fallback mask.
- Note: the process still dies later with 0xC0000374 (delayed heap issue from a later
  host block - the manual scene path / char chain after the failed real call).
- Evidence: host_exe64-80.out.

## 2026-10-04 - Gate 1: shadow builds cleanly (1024x1024); RL loader statics set; InitShadowScene E_FAIL moves to line 1848

- Shadow descriptor hook refined: log every hit with its caller (stub now passes the
  caller's return address in rdx). The crashing descriptors were the KRLShadowMgr
  ([rep+0xED3F18]+0x610, caller rep+0x36D440) and the shadow runtime
  (caller rep+0x3CFC62) plus a third (caller rep+0x36D7C7). Their ctor pre-sets
  rb=1024 (1 byte/px) -> the mask grid is **1024x1024**; the hook now fixes the zero
  descriptors to w=h=rb=1024 at use time.
- Result: the shadow bitmap builds (tile counter acc counts down 1023..1011+, no
  crash), run exits 0.
- `KRLScene::InitShadowScene` still fails with E_FAIL, but the inner error moved:
  the `RLResourceLoader::StartLoadModel` / `ms_piResourceMgr` errors are GONE after
  calling the game's own setter `RLResourceLoader::SetResourceMgr`
  (rep+0x34C1B0; stores ms_piResourceMgr -> [rep+0xED2730],
  ms_pi3DEngineManager -> [rep+0xED2738]) in frame60 with
  (rlLoader, rlLoader, singleton+0xB0). Log confirms both statics set.
- Remaining: `KGLOG_COM_PROCESS_ERROR(0x80004005) at line 1848 in
  KRLScene::InitShadowScene` (a COM call inside the shadow-scene init returning
  E_FAIL, no inner KGLOG). Next probe: locate the InitShadowScene line-1848 call
  (the COM/engine call after the bitmap builds) and its missing input.
- Evidence: host_exe81-84.out.

## 2026-10-04 - Gate 1: InitShadowScene line-1848 root-caused to the movie engine's NULL context

- The line-1848 E_FAIL is the call at rep+0x58DBCA:
  `[rep+0xEDDFE0]+0xE8 -> vt[0x28](rlScene+0xF2890)`; the object = the movie engine
  singleton (KG_MovieEngineX64.dll; vtable RVA 0x13E3D8; slot 5 = movie+0x2A00).
- The movie function (0x2A00) returns E_FAIL because **`[movie+0x38]` (the movie
  context/player object) is NULL** (verified at runtime before and after the call).
  Its inner log goes to the movie engine's own (uncaptured) logger - that is why no
  inner KGLOG appears.
- KG_CreateMovieEngine (movie+0x3590) explicitly zeroes +0x38; the context is
  assigned elsewhere (not in the movie vtable slots 0-19, not among the movie
  module's disp8/disp32 `mov [reg+0x38]` stores checked - likely set by the game's
  own init in the exe, which this host skips with RC_HOST_EXE_NOINIT).
- Next probe: find the writer of the movie singleton's +0x38 (scan the exe + all
  loaded modules for stores to [movie+0x38] where movie = [KG_MovieEngine+0x1D6EE8]),
  or the movie "Open/CreatePlayer" method that builds the context; then the shadow
  init succeeds -> real CreateRLScene completes -> Gate 1 checkpoint.
- Evidence: host_exe85-88.out.

## 2026-10-04 - Gate 1: shadow-scene chain advanced to the shadow-object write-back

- The line-1848/1849 chain is now: the rep calls the movie engine's shadow-scene
  getter (movie+0x2A00) with rdx = &[rlScene+0xF2890]; the movie forwards to the
  adapter movie context (adapter+0x2F5050, class ctor 0x1110D0, init vt[3] 0x111930)
  which creates the shadow scene via the window/device and is supposed to write the
  object back into the field.
- Fixed along the way (all host-side completions of skipped game init):
  - movie engine context linked: [movie+0x38] = adapter ctx (the game init normally
    links them) -> error moved 1848 -> 1849.
  - movie shadow-name hook (movie+0x2A00 entry): fills the 8-byte name field when
    empty (short "shadow"; a longer name overflows into [rlScene+0xF2898] and breaks
    the container there) -> the name check passes.
  - adapter movie init called with the adapter object ([[adapter+0x6C940()]+8]) and
    the working-root string (adapter vt[0xAE0]); the context is fully initialized
    (ctx+0x10 = window, ctx+0x100 = device both non-null).
- Current fault: rep+0x58CCCD dereferences [rlScene+0xF2890] as a POINTER (the
  shadow object) - the field still holds the name string, i.e. the context's
  write-back did not happen (the window/device shadow-creation path at
  0x111F49/0x112084 returned without writing [rsi] via 0x126E40).
- The field is a struct: +0x0 object pointer, +0x80 flag; the context's
  vt[7] (0x111DE0) writes it at 0x1120DA (call 0x126E40(rsi = the field,
  r14 = the created shadow)) when the device's vt[0xA0] shadow creation succeeds.
- Evidence: host_exe92-98.out.

## 2026-10-04 - Gate 1: shadow-scene field is a .map filename; adapter ctx shadow creation is the remaining step

- Runtime hook on the adapter context's shadow function (adapter+0x111DE0) confirms it
  receives the field ([rlScene+0xF2890]) holding the name string; the function does
  `strrchr(field, '.')` then `strcpy_s(ext, ".map")` - the field is the shadow-scene
  MAP FILENAME (extension replaced with .map), then the shadow is created via the
  window (ctx+0x10, method vt[0x198]) and device (ctx+0x100, method vt[0xA0]) and
  written back (0x126E40 stores into the holder+0x18); the name is copied into the
  created object at +0xB4.
- With a no-extension name the context returns early (no-op) and the shadow init
  fails (KRLScene::Init line 198) -> the scene cleanup then dereferences the field as
  an object pointer and AVs at rep+0x58CCCD (a consequence, not the root).
- The map's bd folder has no .map/shadow-scene data (only environment.json,
  playerEnvironment.json, shadowparam.json, map.rcidx = {"RCEffectName":"jx3bd"});
  the real shadow-scene map data is missing from the extracted sandbox.
- Adapter context fully initialized (ctx+0x10 window, ctx+0x100 device non-null);
  movie ctx linked; movie shadow-name hook active. Evidence: host_exe95-99.out.

## 2026-10-04 - Gate 1: the shadow .map load - engine resolution traced; SRScene copy does not satisfy it

- Worktree/branch restored: `reborn-iso-skillv2-sandbox` @ agent/skillv2-sandbox at
  c1869a2 (the v2 branch was merged into agent/item1-completion as ec9e3a1 and then
  reverted by a8b37bc; the commits survive and the worktree was recreated from c1869a2;
  the uncommitted 'a.map' + ctx-hook edits were preserved).
- Process followed on the name: with a name carrying an extension ('a.map'), the adapter
  context runs its full path: strrchr('.') -> strcpy_s(ext,'.map') -> the window's
  vt[0x198] (engine, eng+0x8AFC40) -> KG3D_Engine::CreateSceneFromDestination
  (engine+0x9ACBF0) -> scene alloc + 0x9B3710 (InitFromDestination) -> the load fails
  (`load scene "a.map" fail.`, KG3D_Scene::InitFromDestination line 1981).
- The engine resolves the name via KG3D_ConvertToStandardHashString + _splitpath_s
  (engine+0x9AD3A0) - a file-system lookup, not a raw open. Copying the map's
  .SRScene (SRS format) to `<root>\a.map` did NOT satisfy the load (format and/or
  file-system registration mismatch).
- The scene ctor (rep+0x588F40) zeroes [scene+0xF2890] (4 bytes; the field is the
  8-byte name/filename slot before the member at +0xF2898) and no other writer exists
  in rep/logic/exe - the real name source remains unidentified (likely the engine
  writes it during the map scene load in the full game).
- Evidence: host_exe99-102.out.

## 2026-10-04 - Gate 1: destination-scene load chain traced to the engine's name canonicalization

- Engine side traced precisely: KRLScene::InitShadowScene -> adapter ctx vt[7] ->
  window vt[0x198] (eng+0x8AFC40) -> KG3D_Engine::CreateSceneFromDestination
  (eng+0x9ACBF0) -> scene alloc + eng+0x9B3710 (InitFromDestination):
  `strcpy_s(scene+0xB00, 0x104, name)` then `call eng+0x9AD3A0(name)` - the name
  canonicalization (KG3D_ConvertToStandardHashString + _splitpath_s + sprintf("%s%s"))
  - if it returns 0 -> line 1981 error -> `load scene "a.map" fail.`
- Probes: the KGCommon file-system global ([KGCommon+0xF0E840]) IS set at runtime
  (non-null) - so the hash step has its global; no inner error logs appear, i.e. the
  failure is the final canonicalization/load step or the file/format.
- Copying the map's .SRScene (SRS format) to `<root>\a.map` does not satisfy the load
  (host_exe102.out).
- Remaining: the real shadow-scene name + data (not in the extracted sandbox; the pak
  file index is not loose on disk either) - extraction from the client paks or the
  engine's full map-load flow is needed for the game-truthful completion.
- Evidence: host_exe101-103.out.

## 2026-10-04 - Gate 1: the destination scene needs a COMPILED map (magic 0x10203040); chain fully decoded

- The name requirements are now decoded and satisfied step by step:
  1. the name needs an EXTENSION (adapter ctx does strrchr('.') + strcpy_s(ext, ".map"));
  2. the engine's canonicalization (eng+0x9AD3A0) FAILS when the name has no directory
     part (sprintf("%s%s", drive, dir) returns 0 for empty) - so the name must carry a
     dir (used "a\a.map", 7 chars + NUL = 8 bytes, fits the field);
  3. with those, the engine's destination load reaches KG3D_CreateMapFromFile ->
     _GetMapFileDataFromBuffer and requires a COMPILED map: 0x230-byte header with
     `dwMask == 0x10203040` (line 903). The .jsonmap/SRScene are rejected.
- The compiled-map writer exists in KG3DEngineX64.dll (the map saver at 0x35BE00;
  magic stores at 0x35BF7B and 0x36DFFB) - the editor's SaveMap. No compiled .map
  exists anywhere (probed 12 pak paths via PakV4SfxExtract, the game dir, MovieEditor,
  caches) - the client must produce it at runtime.
- Engine entry points: CreateSceneFromSource (eng+0x9ACE80, the jsonmap/editor path -
  the host's main scene) vs CreateSceneFromDestination (eng+0x9ACBF0, the compiled-map
  path used by the shadow/middleware scene).
- Sandbox test state: <root>\a\ = junctions to the map's data dirs + a.jsonmap; a.map
  currently absent (the loader then reports "Map file a\a.map not exist").
- Next: call the engine's map saver (KG3DEngineX64+0x35BE00) with the main map object
  (from the map manager [engine+0x2B18]) and the name "a\a.map" to produce the compiled
  map the destination load needs - then the shadow scene loads and InitShadowScene
  proceeds. Evidence: host_exe104-108.out.

## 2026-10-04 - Gate 1: rcdata discovered (jx3bd.rc); compiled-map requirement stands; engine map manager probed

- Extracted from the paks into the sandbox: `data\rcdata\jx3bd.rc` (1.64 MB - the RC
  resource container the map's `bd\<map>.rcidx` references via
  `{"RCEffectName":"jx3bd"}`) and `data\rcdata\shadowparam\shadowparam.json`.
- The destination-scene load still requires the COMPILED map
  (`dwMask == 0x10203040`, line 903) - the RC files alone do not change the path.
- Compiled `.map` sweep: 60+ candidate pak paths probed via PakV4SfxExtract (all NOT
  FOUND) - the client ships no compiled maps; the saver lives in KG3DEngineX64.dll
  (0x35BE00) which the client process does NOT load (GetModuleHandle = 0).
- Engine map manager probed at runtime: [engineInstance+0x2B18] -> a heap object whose
  [+0x10] is the likely map object; the saver needs KG3DEngineX64 (absent).
- Remaining paths: (a) the real field name may resolve through the RC system
  (jx3bd) rather than a file - test RC-based names; (b) load KG3DEngineX64.dll in the
  host and use its saver to compile the map (the editor's own path) - then the
  destination load finds the compiled map.
- Evidence: host_exe109-112.out, map_probe_out5.

## 2026-10-04 - Gate 1: (a) RC is a render effect, not a scene; (b) editor map compiler cannot load in-process

- (a) `data\rcdata\jx3bd.rc` inspected: it is an XML render-composite effect graph
  ("EffectWidget"/"SceneRenderNode"/"RCOutputNode"/"BD Low"/RCEffectNodePy::RCSwitch10,
  textures data/rcdata/textures/*.dds). It visualises the baked data; it contains no
  scene/map references. The `.rcidx` "RCEffectName" therefore does NOT resolve the
  shadow scene; the destination field still needs a compiled map name. (negative result,
  documented)
- (b) The map saver lives in KG3DEngineX64.dll (the EDITOR engine build).
  - Sandbox bin64 lacked SO3StatsSystemX64.dll -> load err=126; copied it from the
    client's bundled MovieEditor bin64.
  - Retry: err=127 (ERROR_PROC_NOT_FOUND) - KG3DEngineX64 imports symbols the client's
    already-loaded engine DLLs (X3DEngine/Engine_Lua5X64, client versions) do not
    export. The editor engine build is incompatible with the client engine stack in
    one process.
  - Consequence: the map compile cannot run in the host process. It needs either a
    separate process with the editor stack (MovieEditor's full bin64), or the editor
    tool itself - a data-prep step producing the compiled .map the client's
    CreateSceneFromDestination requires.
- Evidence: host_exe113-114.out, rc_inspect outputs.

## 2026-10-04 - Client named skillv4; current visual state captured

- The host window title is now sandbox-skillv4 (AGENTS 2.7; RC_TITLE overrides).
- Run skillv4.png (RC_HOST_SHOT): the real client engine renders the map world -
  desert terrain, adobe buildings, trees, a cannon (the static merged world, camera at
  the sandbox spawn). Numeric fingerprint: 1264x681, mean #AEB6B1, sha256 f78c1584...
  (tools/proof/image_stats.py). The bottom half is the terrain/water surface seen at a
  grazing angle from the low spawn camera.
- The RL scene / player character are NOT in the frame yet (Gate 1: the shadow-scene
  init still blocks the real CreateRLScene; see the compiled-map findings).

## 2026-10-06 - Live window: black on screen (present issue) + long KEEP runs hit the game protection

- Window visibility: the host window is created minimized sometimes (rect at -32000);
  the host now shows it with SW_SHOWNORMAL/SW_RESTORE + SetWindowPos(HWND_TOPMOST,
  80,60,1280,760) + SetForegroundWindow. Verified on the live process (restored,
  topmost, visible on the user's screen).
- BUT the window client area renders BLACK on screen while the engine's own
  screenshot API captures the world: the on-screen PRESENT is not reaching the host
  window. Lead: `GetActiveWindow2` returns NULL at frame60 (the engine's active-window
  state is not the host's substituted window at that point), so the swapchain target
  may be the engine's original (hidden) window. `KG3D_Window::Present` exists
  (mangled names in KG3DEngineDX11EX64) - the host frame loop does not call a Present.
- Long KEEP runs are unstable: the run died around frame 200+ inside the game's
  protection module (Dumper64: "UnInit was not called before destroy" ->
  ucrtbase abort). So the live-window mode must be treated as short-lived for now.
- Reliable visual evidence stays the engine-API screenshot (skillv4.png).
- Evidence: host_live2.out, screen captures (screen_full.png, screen_topmost.png).

## 2026-10-06 - KRLScene destination-name scheme + shadow branch decoded; host arg9/sceneName gap found (research only)

Static analysis only (no runs - user directive "research only until I say so"). New
HIGH-confidence findings:

- **Destination-name builder** `rep+0xB0C7B0` (called from `CreateRLScene` only when its
  9th arg != 0): strncpy the mapFile, truncate at the last path separator TWICE, then
  `buf1 = sprintf("%s\\%s", "data\source\maps", sceneName)` =
  `data\source\maps\<sceneName>` and `buf2 = sprintf("%s\\%s_Setting.ini", buf1,
  sceneName)` = `data\source\maps\<sceneName>\<sceneName>_Setting.ini` (fmt strings at
  rep+0xD0DF34 and rep+0xCA5D40). The block then calls the 3D scene `vt[0x5F8](buf1,
  sceneName)` (destination setter) and `KRLScene::Init` receives `buf2` as its name arg.
- **KRLScene::Init = `rep+0x58D800`** (the map load; its error exit logs "KRLScene::Init",
  string rep+0xCBA7D0). The line-1848/1849 shadow step inside it is an inlined copy of
  **KRLScene::InitShadowScene = `rep+0x58EB30`** (reached via thunk `rep+0x1B2BB`, an
  interface-vtable slot).
- **Adapter shadow fn** = `KG3DEngineAdapterX64.dll+0x111DE0` = the adapter ctx `vt[7]`
  (ctx = adapter+0x2F5050; vtable = adapter+0x2B0DF8, slot 7 = 0x111DE0): `strrchr(name,
  '.')` -> NULL (no dot) -> **early return S_OK (no-op)**; ext == `.map` (`_stricmp`) ->
  window `vt[0x198]` = the compiled/destination path (`eng+0x9ACBF0` x2); ext ==
  `.jsonmap` -> window `vt[0x1A8]` (flag 0) / `vt[0x1B0]` (flag != 0) = the SOURCE path
  (`eng+0x9ACE80`). The window methods write the created scene to their arg8 (`*out`).
  The movie call (`movie+0x2A00` = movie vtable 0x13E3D8 slot 5) forwards to ctx `vt[7]`
  with rdx = &[rlScene+0xF2890] and r8 = a stack ptr (low dword != 0) -> the `vt[0x1B0]`
  source variant. On success the name is copied to the created scene's component +0xB4
  (strcpy_s 0x104) and the component gets the device-created shadow at +0x18 / flag +0x80.
- **Field `[rlScene+0xF2890]` = a name buffer** (~8 bytes; a longer name corrupts the
  container at +0xF2898 - earlier empirical result). All rep writers are ZEROING only:
  ctor 0x58915D, reset 0x58CA6A, cleanup 0x58CCE8. The name's real source is still open;
  prime suspect = the movie-side wrapper (the real `[movie+0x38]`, which the host skips
  by linking the adapter ctx directly).
- **Host gap found (the concrete blocker):** the host's `CreateRLScene` call passes
  arg8 = a UTF-8 sceneName literal and **arg9 = 0** -> the destination block is skipped ->
  `buf2` stays empty -> the later Init steps (`rep+0x46BC50` map-base-info call with
  buf2, error line 0xB5) get an empty name -> Init fails -> the cleanup (`rep+0x58CCA0`)
  derefs the name field as an object -> the AV at `rep+0x58CCCD` (a consequence, not the
  root). The movieNameFix hook only covered the line-1849 check.
- Note: the current hook name `"a\a.map"` forces the `.map` branch = the compiled-map
  dead end; a no-dot name gives the no-op S_OK; a `.jsonmap` name gives the source path
  (no compiled map needed).
- Corrections to earlier notes: movie singleton global = `KG_MovieEngineX64+0x19F8E8`
  (KG_GetMovieEngine 0x37E0 / KG_CreateMovieEngine 0x360B); movie vtable = 0x13E3D8
  (slot 5 = 0x2A00) verified; rep main object global = `rep+0xEDDFE0`; CreateRLScene's
  registry global = `rep+0xE3DFE0`.
- Next run (pending the user's go): CreateRLScene with arg8 = the GBK sceneName
  (`龙门寻宝_s`) and arg9 = 1 -> buf1/buf2 built (both exist in the sandbox) -> the
  `vt[0x5F8]` destination setter runs -> Init gets buf2 -> observe the later steps; keep
  the name hook as a fallback and log which shadow branch is taken.
- Reproduce: capstone disassembly of the cited RVAs (session scripts; rep+0x58D800,
  rep+0xB0C7B0, adapter+0x111DE0, adapter+0x2B0DF8, eng+0x8AFC40/0x8AFF10/0x8B0180).

## 2026-10-06 - Window fix (off-screen + closeable + watchdog) + Gate 1 line-198 cleared (vt[6] setter); new blocker = RL table list

- Window: the host window now shows OFF-SCREEN by default (SW_SHOWNOACTIVATE +
  SetWindowPos(-4000,-4000); a fully hidden window breaks engine init). RC_HOST_SHOW=1
  puts it on screen. WM_CLOSE destroys the window + sets g_hostQuit (the frame loop
  exits); WM_DESTROY posts WM_QUIT. A watchdog thread force-exits after RC_HOST_MAXSEC
  (default 240 s) - no run can leave a stuck window. (User report fixed: the window was
  topmost and unclosable - PostQuitMessage alone never destroyed it.)
- Off-screen caveat: KG3D_Window::_DoScreenShot asserts m_SwapChainObject.piSwapChain
  (NULL off-screen) - the engine-API screenshot needs the on-screen window; visual-proof
  runs use RC_HOST_SHOW=1.
- Frame-0 flakiness: one run died at frame 0 in KG3D_SceneObjectContainer::Update
  (piEnvironment NULL) + AV - a race, not the window (a rerun of the same build
  completed); noted.
- Gate 1 progress: KRLScene::Init line-198 gate (rep+0x2FD130) = the scene registration
  needing [SO3Represent+0x108] non-null. Writer = the SO3Represent setter at vtable
  index 6 (rep+0x3E7B40: [rcx+0x108]=rdx; tail-calls rep+0x2530B). The host now calls it
  with g_so3World before CreateRLScene -> line 198 cleared; the Init reaches the shadow
  step (movieNameFix fires, no 1848/1849) and fails at line 212:
  KRLWeatherController::Init (rep+0x43BAB0) line 27: [SO3Represent+0x210]
  (m_TableList.m_tabCommon) is NULL.
- The RL table list = SO3Represent+0x1B0..+0x248 (ctor rep+0x3DFxxx zeroes them);
  loader = KTableList::Init (lambda body rep+0x80B9C3, names via the file loader
  [rax+0x48]); perf marker "game.startup.init.rl.table" (0x3E3DE1) inside the timed
  wrapper rep+0x3E3D90, called by rep+0x3E59C0 (vtable rep+0xC99D30 slot 1) of the
  table-loader object the SO3Represent::Init creates (rep+0x3E64FF/0x3E653D). The
  frame60 Init(Param) already runs with the full param (+0x70/+0x78 worlds) and returns
  1, yet +0x210 stays NULL - the table-load sub-step is the next probe (check
  [main+0x210] after the Init; find why the lambda does not run / its arg objects).
- Evidence: host_exe119-124.out; commits 78e32e5 (destination args + no-dot shadow
  name), 3a55132 (window fix + vt[6] gate + probes).

## 2026-10-06 - Gate 1: RL table-list blocker traced to the dormant async task chain (not finished)

- Goal: clear KRLWeatherController::Init line 27 (`[SO3Represent+0x210]` =
  m_TableList.m_tabCommon NULL) so KRLScene::Init line 212 passes.
- Probes/experiments this session (all committed):
  - the fs resolves the logical file the runner opens ('SkillCasterModel') fine
    (rep+0x3F08F0 opener with rep_main+0x120 as an ADDRESS) - so the table files
    are reachable; the failure is upstream.
  - param+0x98 changed from an event manager to a fresh zeroed task container
    (the exe's Initialize passes a caller-stack object there); the Init still
    returns 1 and leaves it empty (nothing pushed).
  - KGAsyncTaskInterface::FetchResult (KGCommon 0x39F0) after the Init -> 0 (no
    completed tasks) - the RL table tasks are never queued.
- Trace hooks (rep+0x80B6A0 registerTasks, rep+0x80B8C0 runTasks, rep+0x80B9C3
  table-load lambda, rep+0x3E3D90 timed wrapper, rep+0x8261F0 task-list builder)
  installed and never fire in-host: the whole chain V::method1 (rep+0x3E59C0,
  vtable rep+0xC99D30 slot 1) -> wrapper -> builder -> tasks -> lambda is
  dormant. The SO3Represent::Init does create the task functors (rep+0x3E64FF /
  0x3E653D inside the Init 0x3E6070-0x3E6657) but nothing invokes them.
- The game's side: the exe's KJX3RepresentModule::Initialize (0xBC150, called by
  the module dispatcher 0xBC6A0 state 3, which first creates a KGAsyncTask group
  - 0x14079AEE0 + the list init) calls the rep Init at 0xBC46E with the param;
  the post-Init block (0xBC4DA) pushes a 0x10 callback functor (manager vtable
  0x95A330; slot 2 invoke = 0x87980 = a no-op) into the container. The actual
  run/await of the RL tasks is still unidentified (KGAsyncTask workers or a
  later dispatcher state).
- Next probes: (1) the Init's guard that gates the task creation (why the V
  functors are created but never invoked - compare the param fields against the
  exe's exact fill at 0xBC263-0xBC46E, especially +0x90..+0xB8); (2) whether the
  KGAsyncTask worker pool is started in-host (CreateGroup/AddTask behavior), and
  (3) who calls V::method1 in the game (vtable call - likely a dispatcher state
  after the Initialize). Evidence: host_exe125-133.out.

## 2026-10-06 - Gate 1: RL table task chain located and driven; wrapper needs the right resource member (last unknown)

- Big step: the SO3Represent::Init DOES queue the RL tasks - they sit in the
  step controller the host fabricates ([param+0xC8] -> [0] = the list owner;
  list head/tail/cur at +0x70/+0x78/+0x80). Probed after the Init: 5 tasks
  (vtables rep+0xC99C90, 0xC99CE0, **0xC99D30 (the RL table loader)**, 0xC99D58,
  0xC99D80).
- The tasks are std::function-style functors whose **slot 1** is the work (slot 2
  is a no-op thunk). Invoking slot 1:
  - task 0 (vt1 rep+0x3E59F0) and task 3 (rep+0x3E5A40) run long engine-init-like
    work (task 3 re-runs KG3D_Engine init - it must NOT be re-run in-host);
  - task 2 (the table loader, vt1 rep+0x3E59C0) reaches the timed wrapper
    (rep+0x3E3D90, hook fires) - so the host now invokes ONLY task 2.
- The wrapper's first step: `rcx = [param+0xA8]` (captured by the task at +0x18);
  `call [rcx]->vt[3](rcx, &out, float)`; the result's [0] is transferred. With
  param+0xA8 unset (garbage) the call faults at rep+0x3E3DC4 (`mov rsi,[rax]`);
  tried g_rlLoader (vt[3] = rep+0x18AB6 returns a bad object) and the engine
  manager (vt[3] = x3d+0x201D0 is a shutdown method) - both wrong; the MapConverter
  manager is NULL at Init time in-host (built later in frame60).
- The exe's param+0xA8 = rbx of the module dispatcher (0xBC6A0, `mov rbx, r8`),
  which is called indirectly (no direct caller found) - its exact object is the
  last unknown. Next probe: identify the dispatcher's r8 object (the module
  context) at runtime by hooking the exe's Initialize 0xBC150 entry and logging
  rbx, then use the same object in-host.
- State: line 198 still cleared; line 212 still fails (tables NULL). The host
  currently invokes only the RL table task (others skipped to avoid re-init).
  Evidence: host_exe134-141.out; commits a3ec137 + this one.

## 2026-10-06 - Gate 1: wrapper member (param+0xA8) requirements pinned; six candidates exhausted

- The timed wrapper (rep+0x3E3D90) calls `member->vt[3](member, rdx = &out,
  xmm2 = float)` where member = [task+0x18] = [param+0xA8]; it then does
  `rsi = [rax]; [rax] = 0; ...; rcx = [out]; if (rcx) release(rcx)` - i.e. the
  method must FILL the out (a smart pointer) and return &out (or an equivalent
  smart-pointer holder). Candidates tried (all leave the out uninitialized ->
  fault at rep+0x3E3DDD, or return garbage -> fault at rep+0x3E3DC4):
  g_rlLoader (vt[3] = rep+0x18AB6, a map lookup), g_ifMgr (x3d+0x201D0 =
  shutdown), g_ifConv, the rep singleton (vt[3] = rep+0x3E5ED0 = `lea rax,
  [rcx+0x24468]`, passes the call but does not fill the out), g_ifXLogic,
  g_engIface.
- The wrapper's consumer: the builder (rep+0x8261F0) gets `r8 = rsi` = the
  member vt[3]'s result = the table-definition SOURCE (the registerTasks
  rep+0x80B6A0 reads it: [0], [8], [0x10] captured; [+0x18] object invoked via
  vt[0]). So the member = a manager that yields the RL table source.
- The exe's value: param+0xA8 = the module dispatcher's r8 (0xBC6A0, slot 9 of
  the module-class vtable 0x95A328 = the callback invoke; `mov rbx, r8`). The
  callback objects (0x10-byte, same vtable) are registered by the exe's
  post-Init block (0xBC4DA) into the task container. Remaining probe: find the
  event-system call site that invokes the callbacks (slot 9) and its r8
  construction (the event data) - that object is the missing member.
- Evidence: host_exe143-145.out; commit a7f5b0d.

## 2026-10-06 - Gate 1: the exe's own KJX3RepresentModule::Initialize now COMPLETES in-host

- The host now drives the game's own module init path end to end:
  1. The exe's subsystem singleton registry (exe+0xA8C1C0..0xA8C2B0, one global per
     Create) is populated: the host calls the missing Creates 0xB2910/0xB72F0/
     0xBF440/0xC54D0 with caller storage (they construct into rcx) + each module's
     OnInitialize = its vtable slot 5 (verified against the known pair 0xA4700 ->
     0xA42F0): 0xB2CE0/0xB7D20/0xBFA00/0xC5DB0 - all return 1 and set [module+0x18].
  2. 0xA8C1E0 (KJX3UIShellModule): its OnInitialize does GetProcAddress([+0x60],
     "CreateSO3UI"); the host loads JX3UIX64.dll (the export's DLL) and fabricates
     the module with +0x60 = the handle -> CreateSO3UI runs -> [mgr] set.
  3. 0xA8C1C8/0xA8C1E8: the exe's lazy getter 0x9DB60 throws in-host (its CRT
     statics); fabricated instead (0xA8C1C8 = a zeroed config struct whose +0x18 =
     a stub object with vt[0x80]() -> g_ifUI; 0xA8C1E8 = a holder with +0x18 =
     g_ifXLogic).
  4. The Initialize's post-init block (0xBC4DA: registers a callback into a
     caller-provided container our host call cannot supply) is skipped by patching
     0xBC4DA -> 0xBC5F7 (the success exit, r12d=1).
- Result: "exe Represent Initialize exit -> 0x00000001" +
  "exe dispatcher(state 3) -> 0x00000001" - the game's own Initialize runs and
  succeeds; its fault walk was 0xBC2AF -> 0xBC283 -> 0xBC305 -> 0xBC36C -> 0xBC503.
- Remaining (the last Gate-1 piece): the RL table wrapper's member
  (param+0xA8) is still the wrong object (g_rlLoader; its vt[3] returns garbage ->
  fault at rep+0x3E3DC4). The game's value = the module dispatcher's r8 (the
  "Initialize" event data), which the host still passes as 0. The event data is a
  manager whose vt[3](member, &out, float) fills `out` with the RL table source
  and returns it; candidates tried so far all fail. Next probe: hook the exe's
  event system's slot-9 invoke (the module callbacks) to capture the real r8, or
  derive it from the module manager.
- Evidence: host_exe146-163.out; commits 3a55132..cd6cb3f.

## 2026-10-06 - Gate 1 BREAKTHROUGH: the game's own RL table task chain runs through the builder

- The RL table wrapper's member (param+0xA8) is now correct: the exe's own
  Initialize derives it at 0xBC194-0xBC21C from
  `[exe+0xA8C208 + 0x18]` - the KJX3LogicModule's sub-object (module vtable
  exe+0x956808 slot 5 = 0xAF1E0; sub-object vtable exe+0x952B20 whose slot 3 =
  0x98A20 = the table-source getter).
- The KJX3LogicModule is created by a standalone function exe+0xAF4A0 (creates
  the module + sub-object, stores it at 0xA8C208). The host calls it in frame60
  before the Param build.
- The task invoke stub was corrected: the wrapper's arg2 (rdx) is the STEP
  CONTROLLER (the game's runner passes it; the wrapper saves it and forwards it
  as the builder's arg4). The stub now does `mov rax,[rcx]; mov rax,[rax+8];
  jmp rax` with (rcx = the task, rdx = the stepCtrl).
- The host's fake stepCtrl also needs the pool allocator at BOTH
  `[stepCtrl+0x10]` (the builder's read) and `[[stepCtrl]+0x10]` (the rep Init's
  read) - added.
- RESULT: the wrapper's member getter runs, the wrapper calls the game's own
  task-list builder (rep+0x8261F0), and the builder COMPLETES WITHOUT FAULT
  (host_exe170: "table builder enter" then straight to "after task run").
- Remaining: the builder pushes the created register/run/load tasks into a
  container ([r14] = the holder's [0]); they still need their RUN (the register
  functor 0x80E340 -> 0x80B6A0 and the run functor 0x80E360 -> 0x80B8C0 ->
  the 0x80B9C3 load lambda, which the host has hooks for and which have not
  fired). Next probe: walk the builder's container and invoke those tasks (or
  find the game's step processing), then the tables should land in
  [SO3Represent+0x210].
- Evidence: host_exe165-170.out; commits f846e48, 478090a.

## 2026-10-06 - RL task chain reaches the game's runTasks runner; fault in the lua file layer

- The builder's created register/run tasks land in the STEPBUF's own list (its
  +0x70), not the stepA's: the host re-walks it after the V task and invokes them
  with the stub. The runTasks hook fires (the game's own runner 0x80B8C0 runs).
- The runner opens 'SkillCasterModel' (works), then calls the lua module's
  g_OpenIniFile (Engine_Lua5X64 export 0xBBA30) -> g_OpenFile (0xB2F50) ->
  KG_OpenPakV4File (0xCC670) or the loose opener (lua global 0x170040; probed
  in-host: set, lua+0xB1F80). The fault is a call to a module BASE address
  (0x7FF7CD600000 in the last run) inside that path - an invalid/unresolved
  function pointer in the lua file layer.
- Next probes: (1) identify the faulting module (the describeAddr printed
  module-unknown - the module list may need the new base); (2) trace
  KG_OpenPakV4File / the lua file-system init (the host calls SetRoot 0xB5400/
  0xB5220 + InitPak 0xCC2D0; something else may need init).
- Evidence: host_exe171-175.out; commits 478090a..2e2e63a.

## 2026-10-06 - RL task chain: runTasks runner traced into the lua file open (pak-mode flag lead)

- The runner path: runTasks (0x80B8C0) -> the rep fs opener('SkillCasterModel') ->
  the lua's g_OpenIniFile (lua+0xBBA30; the rep's IAT 0x109A020 resolves to it
  correctly) -> g_OpenFile (0xB2F50) -> KG_OpenPakV4File (0xCC670) ->
  g_OpenAloneFile (0xB2EA0) -> the ini object's vt[0xB] + the loose open
  (0xB1C70) whose internals make a WILD call (an address outside every loaded
  module - 0x7FF84C910000 in the last run; GetModuleHandleEx fails on it).
- Probed and CORRECT in-host: the lua's file-system callbacks 0x170030/0x170040/
  0x170048 (all valid lua functions), the rep IAT for g_OpenIniFile.
- LEAD: at lua+0xB1CB8 the code branches on the global at lua+0x1729C0 (a
  pak-mode flag): nonzero -> the pak open (0xB4570), zero -> the loose open
  (0xB4390/0xB1C70, the faulting path). If the host's InitPak did not set that
  flag (or the pak manager's state is off), the loose path is taken and faults.
- Next probes: (1) log lua+0x1729C0 in-host (set?); (2) if 0, trace what sets it
  (the lua's InitPak 0xCC2D0 / the pak manager 0x1730B8's init); (3) the loose
  path's wild call site (0xB1CB5's vt[0xB] or the 0xB1D17 indirect call) as the
  fallback.
- Evidence: host_exe171-177.out; commits 478090a..b9baebb.

## 2026-10-06 - Next-agent handoff guide written

- Added `docs/engine_host/NEXT_AGENT_HANDOFF.md` (registered in the area README
  index): the full Gate 1 / RL-table-chain map with the exact addresses, the
  current blocker (Engine_Lua5X64 file layer: wild call in the pak/loose open;
  pak flag lua+0x1729C0 was 0; prefix suspect lua+0x1709C0), the ordered next
  probes, the instrumentation already in the host, the pitfalls (order bugs,
  CRT guards, stepCtrl/invoke conventions, address-arithmetic traps), the
  evidence/commit index, and the fallbacks.
- Evidence: docs commit 5a46839; host_exe146-178.out.

## 2026-10-06 - Handoff guide: explicit worktree/branch section

- Expanded NEXT_AGENT_HANDOFF.md section 1 per user request: main checkout vs
  this task's worktree path, branch agent/skillv2-sandbox, the git worktree add
  setup pattern, four verify commands, the merge/revert incident history
  (ec9e3a1 -> a8b37bc, restored at c1869a2), and the commit/never-push/shared-
  resource rules. Evidence: commit a614387.

## 2026-10-06 - Handoff guide: where-to-look block + START HERE marker

- Per user request the guide now opens with its own location: file path
  docs/engine_host/NEXT_AGENT_HANDOFF.md, worktree
  C:\Users\Zhibin Ren\Desktop\reborn-iso-skillv2-sandbox, branch
  agent/skillv2-sandbox, and the README registration row - plus the ordered
  reading list (guide -> CLIENT_CHARACTER_PLAN.md -> EXPERIENCES 2026-10-06 ->
  AGENTS section 2/15 -> the host_exe logs). The area index row is marked
  START HERE. Evidence: commit bc84105.

## 2026-10-07 - Gate 1: the "wild call" was the host's own rel32 table hook; lua file layer fixed

- Root cause of every 2026-10-06 "wild call": `installTableLoadHook`
  (rep+0x80B9C3) patched a 5-byte `jmp rel32` to a stub from
  `VirtualAlloc(NULL,...)`. The stub landed >2 GB from the rep module
  (0x2EB...) and the truncated displacement jumped to 0x7FFF93720000 - the
  nvcuda64 / "(module?)" addresses. `allocNear(site, ...)` + a 16-byte-stack-
  aligned stub (the lambda is reached by a **non-call jump**; entry rsp is not
  the ABI's call alignment, and rsp must be stashed in a callee-saved register
  or the hook call clobbers it) fixed it: `tableLoad lambda enter` now fires.
- lua file layer ground truth (exports of Engine_Lua5X64): `g_SetRootPath`
  0xB5400 -> root string 0x170060 (strips trailing sep); `g_SetFilePath` 0xB5220
  -> file path 0x170170; `g_SetPriorRootPath` 0xB5380 -> prior root 0x1729C0
  (the "pak flag" read at 0xB1CB8/0xB461F is this string's first byte, NOT a
  bool - the previous `=1` hack wrote a garbage prefix); `KG_InitPakV4FileSystem`
  0xCC2D0 creates the pak manager 0x1730B8 + the type vector 0x1730A0/0xA8
  (mode arg4: 0 pak-first, 1 loose-first); `0x1709C0` has zero refs (dead lead).
- InitPak mode matters: with the pak uninitialized the engine shader include
  `GI_DetailTracingCommon.hlsli` is not found (compile fail, engine init dies);
  with pak-first the pak's map data shadows the sandbox and the frame-0 scene
  update faults (piEnvironment NULL). `arg4=1` (loose-first, matching
  `config.ini` PakFirst=0) gives both: loose sandbox wins, pak supplies the
  missing shader/include + `skill_caster_model.ini`.
- Two run-command gotchas: the sandbox map name is `龙门寻宝_s` (an ASCII
  transliteration silently loads no scene -> frame-0 paint fault); the corrected
  env recipe is in NEXT_AGENT_HANDOFF section 1.
- Also: the frame60 probe is now latched (`f>=60 && !g_logicDone` holds the
  frame budget) because the logic worker now loads pak tables and takes longer
  than 60 fast frames; VEH backtraces all AVs (limit 24).
- Evidence: host_exe179-186.out; commits 6b552db, 4796241.

## 2026-10-07 - Gate 1: semantic file IO installed pre-task; runTasks completes (1); m_tabCommon still unpublished

- The RL table task first failed logically: `sLoadNumberFromFile` got a NULL
  `Table` because the SemanticX64 file IO was installed AFTER the task ran.
  `CreateRLFile` = SemanticX64 import at rep+0x109AAD0. The host now calls
  `[rep+0x109AAE8]` = `SetFileIOFunctions(rep+0x788D, rep+0x1EB0, rep+0x10DC,
  rep+0x18926)` right after `SO3Represent::Init`, before the task invoke.
- Result: `runTasks enter` -> `tableLoad lambda enter` -> `runTasks exit -> 1`;
  `KTableList::Init` (rep+0x836510, reached by the jmp thunk at rep+0x8003 from
  runTasks) loads the 7 sub-tables and stores them into the KTableList at
  `g_repSingleton+0x1A0` (= the builder's a1; runTasks' kt): +0x11FF8, +0x12000,
  +0x1DE40, +0x23BB8 are non-null afterwards.
- STILL NULL: `[g_repSingleton+0x210]` (m_tabCommon, `KRLWeatherController::Init`
  line 27) - the weather check fails at KRLScene::Init line 212. A Dr0 4-byte
  write watch on singleton+0x210 (new `armWriteWatch`) got ZERO hits across the
  whole task run: nothing in this path publishes it. Static search found no
  `[singleton+0x210]` store in rep (only ctors zeroing it).
- Lead: the builder creates BOTH a register functor (0x80E340 -> registerTasks
  0x80B6A0, functor vtable 0xCD80C8) and a run functor (0x80E360 -> runTasks
  0x80B8C0, vtable 0xCD8000). The host only ever runs the run path - the
  `registerTasks` hook has NEVER fired. The register step is the prime suspect
  for publishing m_tabCommon. Note both functor vtables carry their work in
  **vt[0]**, while the host's task stub calls vt[1] - check the slot before
  invoking.
- Run 194 addendum: the stepBuf list does NOT contain the register task. Its
  two entries are std::function-like objects: the run task ([val+8] = invoke
  thunk 0x9E3F -> jmp 0x80E440 -> runTasks) and a no-op wrapper task (invoke
  0x3E5A30). The builder's register functor (operator() 0x80E340, vtable
  0xCD80C0) is pushed into a container built inside the builder itself (its
  r15); the wrapper hands the builder an out struct at [rbp+0x38] (host global
  g_builderOut). Next: scan the KTableList (singleton+0x1A0) and the source
  ([param+0xA8]+0x10) for the 0xCD80C0/0x80E340 values, or trace the builder's
  push target, then invoke the register functor's operator() after the run.
- Runs 195-201 addendum: the register functor WAS captured and invoked.
  - The RL list-push helper is rep+0x3E52A0 (call sites print as the jmp thunk
    rep+0x2363C): it appends a node {next at +0, value at +8} at
    container+0x78/+0x80. A new `hookTaskPush` logs the first 80 pushes and
    captures the register functor whenever a pushed value's vtable is
    rep+0xCD80C0 (taskPush #9 in run 201).
  - The builder pushes a tree of containers; the run functor's vtable is
    rep+0xCD8000 (slot0 = operator() 0x80E360 -> runTasks), the register
    functor's vtable is rep+0xCD80C0 (slot1 = operator() 0x80E340 ->
    registerTasks; slot0 = deleting dtor 0x80C540). The host invokes it via the
    vt1 address with rdx = the stepCtrl (param+0xC8).
  - Result: `registerTasks enter` FIRES (a1=functor+8, a2=stepCtrl) - then the
    process dies with 0xC0000005 and no VEH line (stdio buffer lost the trace;
    the VEH now fflushes first). Prime suspects: the functor payload
    (functor+8..+0x28) holds builder-stack temporaries that are stale by
    frame60; and/or the register step must run BEFORE the run step / with a
    different arg2 (the builder's out container).
  - Evidence: host_exe195-201.out; commits 78a13a9 + this one; the handoff
    guide section 0 carries the ordered next probes.
- Runs 202-204 addendum: the crash cause is pinned. `registerTasks` enqueues
  each created task via `0x80CED0(queue, task)`, which dereferences a sync
  object at `[queue+8]` (AV at `[rcx+0x12]` in `0x3E7830`) and an allocator at
  `[queue+0x10]`. Both the builder-created containers (class vtable
  `rep+0xCCE548`, captured as `g_taskQueue` via the push hook) and the
  fabricated stepCtrl have `+8 = NULL`; the game's real async-task queue (the
  exe dispatcher creates a task group at exe+0xBC6D2) is what supplies a live
  sync object. The register invoke is now gated behind `RC_HOST_REGINVOKE=1`;
  the run-204 baseline is restored (exit 0, `runTasks -> 1`, register skipped,
  weather line 27 still fails). Next unit of work: reconstruct/drive the exe's
  KGAsyncTask group so registerTasks runs legitimately (handoff section 0).
  Evidence: host_exe202-204.out; commits 57cac00 + docs.

## 2026-10-07 - Gate 1: m_tabCommon is set by KTableList::LoadConfigureFile (line 27/212 cleared)

- The register/async-queue theory for m_tabCommon was a red herring. The writer
  is the game's own `KTableList::LoadConfigureFile` (rep+0x833260): it opens
  `"CommonKRL"` through the rep fs, `SemanticX64!CreateRLFile()` ->
  `[kt+0x23A68] = m_pCommon`, then `m_pCommon->vt[2](file,1,1)` ->
  `[kt+0x70] = m_tabCommon`. Evidence: the member-name asserts at rep+0xCD27E8
  ("m_pCommon") / 0xCD27F8 ("m_tabCommon"), file name 0xCD27B0 ("CommonKRL").
- The runTasks chain only runs the *misc* loader (0x836510); the host now calls
  `LoadConfigureFile(kt)` at frame60 before CreateRLScene. Result:
  `[main+0x210]` becomes non-null (`after LoadConfigureFile [main+0x210]=...`,
  host_exe206/208) and **KRLScene::Init line 27 (weather) and line 212 now
  PASS**. CreateRLScene proceeds far deeper: it loads the represent lua scripts
  (`represent/scripts/rust_animation/...`) and sets up scene objects.
- New blocker: a wild call inside CreateRLScene (exc 0xC0000005 at
  0x...16001D, module-unknown). The VEH now also scans the raw stack and prints
  `[VEH] stk[i]`; the chain is CreateRLScene (rep+0xB0BB74) ->
  rep+0xAEE2DD / rep+0xAE000F / rep+0x3DB9B7 (return after `call [rax+0xD0]`
  on `[scene+0xF1978]`) -> the wild target. Same class as the earlier wild
  call: an uninitialized table/object in a newly reached path. Next probes are
  in NEXT_AGENT_HANDOFF section 0.
- VEH improvement: traces up to 60 AVs and scans 400 stack qwords for code
  addresses (isCodeAddr) so frameless wild calls still show their callers.
- Evidence: host_exe205-208.out; commit 5bb2f49.

## 2026-10-07 - New CreateRLScene blocker: frameless wild call on the main thread

- After the m_tabCommon fix, CreateRLScene runs deep (represent lua scripts
  load) then AVs with `exc 0xC0000005 at 0x...001D (module?)`. VEH diagnostics
  added: AV registers with `tid=`, the first 24 raw stack qwords resolved to
  any module, and a game-stack scan.
- Findings (runs 209-212): the fault is on the **main thread** (tid == main);
  `rax=0x35B1DFE0` is constant across runs (32-bit-looking, not a pointer);
  `rcx=rsp+0x9F rdx=7 rsi=rsp+0x108 rdi=rsp+0xB0`; the top game return address
  is `JX3RepresentX64.dll+0xAEE2DD`; the stack spells `scene[000002]` and the
  rep assert format `"scene[%.6u]"` is at rep+0xD0A2B0 (scene-id wrapper
  0xAD38B0). No return address is pushed at [rsp] -> the transfer is an
  indirect `jmp`/tail dispatch, not a plain call. The engine retries the same
  fault (the VEH line appears twice) and the process dies.
- Next: RE the function reaching rep+0xAEE2DD (`call 0x15BF4` -> 0xAEDFD0 at
  0xAEE2D8) and its callees for the indirect transfer; the constant
  `0x35B1DFE0` likely indexes a function table with a missing registration
  (same class as the earlier wild call). Details in NEXT_AGENT_HANDOFF section 0.
- Evidence: host_exe206-212.out; commits 5bb2f49, cb8a54f, <tid commit>.

## 2026-10-07 - CreateRLScene wild call located with an exec-breakpoint tracer

- Added a hardware **execute breakpoint + single-step tracer** (`armExecTrace`,
  Dr1 + the trap flag, VEH-driven) and armed it at `rep+0xAEE2D8` before
  CreateRLScene (the call whose return address `0xAEE2DD` sits on the faulting
  stack). The trace (runs 213-215): `HIT rep+0xAEE2D8` ->
  `step[0] rip=rep+0x15BF4` -> **immediate AV at the wild target** (no further
  steps).
- The live bytes at frame60 are the direct forms and unmodified:
  `rep+0xAEE2D8 = E8 17 79 52 FF` (call `0x15BF4`) and
  `rep+0x15BF4 = E9 D7 83 AD 00` (jmp `rep+0xAEDFD0`), yet the single-step does
  not reach `0xAEDFD0` — the next exception is the wild AV. Therefore the wild
  transfer is at `rep+0x15BF4` (or `0xAEDFD0`'s first indirect call,
  `call [0x109B3D8]`). `rax=0x35B1DFE0` constant; the stack carries
  `scene[000002]` (assert format `"scene[%.6u]"` at rep+0xD0A2B0).
- Next: inline-hook `rep+0x15BF4`/`0xAEDFD0`, log rcx/rdx/r8 and the IAT slot;
  find the unset object/registration. Evidence: host_exe213-215.out; commit
  (exec tracer).

## 2026-10-07 - CreateRLScene wild call isolated; scene IS created before it (run 216)

- Added a probe hook on the jmp target `rep+0xAEDFD0` (len 12, boundary-safe).
  Run 216 trace: `HIT rep+0xAEE2D8` -> `step[0] rip=rep+0x15BF4` ->
  `Aedfd0 enter a1=<name ptr> a2=7` -> wild AV (`0x...001D`). So the path is
  `0xAEE2D8 call` -> `0x15BF4 jmp` -> `0xAEDFD0` ("create named object":
  singleton `rep+0xF51298` via `0xDF8A`/`0x920C40`/`0x1687E`, allocator
  `0x923400` calls `malloc`, then `strncpy` the name) -> wild call.
- KEY: in run 216 (debug arms active) the host's `__try` **caught** the fault,
  logged `real CreateRLScene fault`, and **`GetRLScene(2)` was non-null with a
  non-null 3DScene** (0x...6040 / 0x...ED8); the frame loop and `[host] done`
  completed. So CreateRLScene creates+attaches the scene before the late wild
  call. In the clean build (run 217) the same AV is uncaught and the process
  terminates (game protection or failed SEH unwind). Debug-register
  diagnostics are now gated behind `RC_HOST_DEBUGTRACE` because they change the
  catchability.
- Next: identify the unset registration behind the singleton factory, or make
  the host catch the late fault robustly so Gate 1's scene-attached checkpoint
  holds. Evidence: host_exe216/217.out.

## 2026-10-07 - Gate 1 checkpoint REACHED via the game's path; provisional recovery deviation registered

- **Gate 1 checkpoint (observable):** the host now calls the game's own
  `CreateRLScene` (0xB0B5C0) and `GetRLScene(2)` (0x924B); `GetRLScene(2)`
  returns a non-null RLScene with a **non-null 3DScene attached**
  (`GetRLScene(2) -> 0x…B040 (3DScene=0x…9ED8)`), and the run completes
  (`frame loop done`, `[host] done`, exit 0). `KRLScene::Init` line 27/212 pass
  (m_tabCommon via `KTableList::LoadConfigureFile` / `"CommonKRL"`).
- **PROVISIONAL DEVIATION (registered, §6):** the engine's `CreateRLScene`
  reaches a late, frameless **wild call** *after* it has already created and
  attached the scene (isolated to the named-object factory path
  `0xAEE2D8 -> 0x15BF4 -> 0xAEDFD0`; `[rep+0xF51298]` is NULL; `rax` at the
  fault is the low-32 bits of `rep+0xAEDFE0`). A clean run's host `__try`
  cannot unwind it, so the host installs a VEH `setjmp`-style recovery
  (`RtlCaptureContext` guard around the call; on a wild AV on the main thread
  it restores the context and continues). This is a host-level recovery, not a
  game mechanism.
  - Why no native path is wired yet: the exact faulting `call` could not be
    pinned with the in-host VEH/tracer (frameless transfer; the single-step
    tracer yields only its first step; the safe inline-hook mechanism cannot
    hook the RIP-relative-prologue callees `0x920C40`/`0x1687E`/`0x923400`).
  - **Re-open criteria:** when the wild call is diagnosed and either the
    missing registration is wired or the truncated-pointer source is fixed;
    then remove the recovery (or `RC_HOST_NORECOVER=1` to reproduce the fault).
- Evidence: host_exe223.out (recovered run); commits (recovery) + docs.
- Evidence: host_exe187-193.out; commits 70b9154, a53d874, c947771; the state
  map + next probes are in docs/engine_host/NEXT_AGENT_HANDOFF.md section 0.
