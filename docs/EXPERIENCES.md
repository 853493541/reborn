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

### 2026-09-30 — repo — Rule: feature title mandatory on every client update
- Did: strengthened `AGENTS.md` §2.7 — every client update for a feature must set the
  top-left window title to that feature (`reborn_client_<slug>.exe` -> `sandbox-<slug>`;
  sandboxes `sandbox-ability`/`sandbox-asset`; `RC_TITLE=<feature>` overrides one-off
  canonical runs). Identical titles across client windows are a rule violation.
  Mirrored in `client/AGENTS.md`.
- Evidence: `AGENTS.md` §2.7; `client/AGENTS.md`; this commit (local).
- Outcome: solved.

### 2026-09-30 — repo — Rule: blocked client start must name the conflicting session
- Did: `AGENTS.md` §2.4 now requires that when a client start is blocked by the
  single-instance/namespace guard, the response must end by naming the conflicting
  session — process name, PID, start time and namespace — so the user knows which
  window/session to close. Mirrored in `client/AGENTS.md`.
- Evidence: `AGENTS.md` §2.4; `client/AGENTS.md`; this commit (local).
- Outcome: solved.

### 2026-09-30 — camera — cam-wwdrag branch verification (pre-merge)
- Did: verified `agent/cam-wwdrag` @ `0d958f8` (8 commits, clean worktree): rebuilt its
  camera smoke from a detached temp worktree — **ALL PASS**, including the new
  sprint-drag constant-length regression (worst rel err **0.0115**, matches the report).
  `git merge-tree main agent/cam-wwdrag` → exit 0: **conflict-free merge** (main's newer
  guard-rule docs commit is picked up automatically).
- Evidence: temp-worktree smoke output; merge-tree preflight; report's live logs
  `reborn_20260930_1519/1524/1619/1646`.
- Outcome: verified; awaiting the merge decision (no merge performed).

### 2026-09-30 — client — Merge cam-wwdrag into main + rebuild full & sandbox clients
- Did: merged `agent/cam-wwdrag` (`18885a1`, conflict-free) — camera orbit `SmoothTime`
  fix (sprint drag no longer collapses the radius; smoke regression 1.15%), WW/double-tap-W
  sprint removed per user, zoom moved to `+/-` (A12), first-load pitch sign + per-role
  saved view, start at max range (C10). Rebuilt BOTH from merged main: canonical
  `reborn_client.exe` and sandbox `reborn_client_mini.exe` (both `git=18885a1`); gates:
  camera smoke ALL PASS, gravity model, loot selftest PASS, jx3_model PASS; relaunched
  both (full 8x8 map + sandbox 1x1 map, identical spawn, jump flip active).
- Evidence: merge `18885a1`; `build_info.txt` + `build_info_reborn_client_mini.exe.txt`;
  logs `reborn_20260930_172038.log` (full) / `_172041.log` (sandbox).
- Outcome: solved (local only; main 13 ahead of origin).

### 2026-09-30 — engine — D6 crash returned: stale shared shim clobbered by old-branch build
**Problem:** after the merges the full client AV'd again while dragging the camera below
the character and looking up (the D6 signature). Log `reborn_20260930_173446.log` ended
abruptly at 17:42:29 (spd=320, no clean exit).
**Tried:** compared the merge vs the camera tip — shim source identical, so the merge did
not lose the fix.
**Outcome:** solved (build-process regression, not a merge regression).
**Why:** the shared `bin64\camera_shim.dll` had been rebuilt at 16:56 by a worktree whose
branch predates the D6 fix (`agent/collision-improvement` / `agent/double-jump` have 0
`RC_D6Seed` refs); its exports lacked `RC_D6Seed`, so the D6 lazy-hash AV returned.
Rebuilt the shim from main (17:46:57), exports verified, loaded shim logs
`d6=seed slot=0x...`.
**Prevention:** `native\build_shim.cmd` now refuses to build from a source without
`RC_D6Seed` and verifies that export after linking; the client logs the loaded shim's
`d6=` status in every run; `AGENTS.md` §2.8 + `native/AGENTS.md` document the shared-shim
rule.
**Verification:** driven repro (`tools/camera/drive_client.ps1`, 88 s of drag-down /
look-up + WASD): client ran 305-308 fps throughout, no crash, clean `DONE` exit.
**Links:** `native/camera_shim.cpp` (`RC_D6Seed`), `docs/camera/HOST_DEVIATIONS.md` D6,
`tools/camera/drive_client.ps1`.

### 2026-09-30 — sandbox — Half-size (quadrant) terrain: DEAD END (physics loader requires 512-sample regions)
**Problem:** requested an even smaller sandbox — 1/2 the loaded map of the current 1×1
crop (6.48 MB).
**Tried:** built a quadrant crop (`--half`): r32 and bch cropped 513²→257², bch header
grid fields patched 513/512→257/256, blendmap PNGs cropped 257², `landscapeinfo`
`RegionSize` 512→256 (map `龙门寻宝_h`, 4.93 MB). The client loads the map and even
reports the halved region (`TerrainSampler: size=256 regions=1x1 cell=100 origin=(0,0)`),
but the real physics terrain loader rejects every region: `LoadRegion failed (0,0)`
repeated (`client/TerrainSampler.cs:101-107` → `PhysicsEngineX64` `LoadRegion`).
**Outcome:** dead end.
**Why:** the shipped physics terrain loader requires full 512-sample regions per tile;
sub-region grids are not supported by the engine loader. The 1×1 region crop is the
minimum terrain the engine can load (whole-region granularity).
**Re-open criteria:** only if a loader entry accepting other region sizes is found, or
the game ships sub-region maps — otherwise stay with 1×1.
**Links:** `client/TerrainSampler.cs`; `tools/sandbox/build_sandbox.py` (`--half`
reverted); experiment output `C:\jx3tmp\reborn_sandbox\map\龙门寻宝_h`.

### 2026-09-30 — repo — Project agent skills added (.opencode/skills)
- Did: added five project skills under `.opencode/skills/` (auto-loaded by opencode):
  `engine-run` (build/launch/log/guard/shim loop), `sandbox-map` (cropped loose map +
  its engine limits), `merge-finalize` (preflight, proven conflict policy, dual rebuild,
  gates, no-push rule), `crash-triage` (D6 signature, stale shared shim, driven repro,
  minidump reader), `verify-proof` (must-stay-green gates, numeric fingerprints,
  definition of done). Skills load at opencode startup — restart to pick them up.
- Evidence: `.opencode/skills/*/SKILL.md`; this commit (local).
- Outcome: solved.

### 2026-09-30 — repo — Third-party general skills installed
- Did: reviewed and installed three general skills under `.opencode/skills/`:
  `karpathy-guidelines` (adapted behavioral rules, attributed; upstream states no
  license), `diagnosing-bugs` (Matt Pocock, MIT, installed with attribution and license
  field), `skill-creator` (Anthropic, Apache-2.0, trimmed adaptation with a modification
  notice — the upstream bundled eval tooling is not included). Sources recorded in each
  skill's `metadata`.
- Evidence: `.opencode/skills/{karpathy-guidelines,diagnosing-bugs,skill-creator}/SKILL.md`;
  this commit (local).
- Outcome: solved. Restart opencode to load.

### 2026-09-30 — repo — RE methodology skill installed (re-binary-analysis)
- Did: installed `re-binary-analysis` under `.opencode/skills/` — an adapted, trimmed
  version of Masriyan's "Reverse Engineering & Binary Analysis" skill (MIT, attributed;
  bundled `binary_analyzer.py` not included; repo rules + local tools referenced instead).
  Skipped `haikow/claude-reverse-skills` — the repo has **no license**, so no copying.
  Checked option #2: **Ghidra is not installed** on this machine (so the Ghidra-based
  skills need a new dependency → pending approval); `.venv` already has `capstone` +
  `pefile`, `lief` missing.
- Evidence: `.opencode/skills/re-binary-analysis/SKILL.md`; this commit (local).
- Outcome: solved. Restart opencode to load.

### 2026-09-30 — repo — Rule: game client primary, MovieEditor = visual resource
- Did: `AGENTS.md` §4 now states the **game client** (`...\zhcn_hd` binaries/IL/paks) is
  the primary source for every mechanism/behavior/format/value question; **MovieEditor is
  a visual resource** and supporting host-behavior evidence only — no digging into
  MovieEditor to answer a game-client topic. The evidence hierarchy was updated to match
  (repo docs → game client code/IL → raw extracted caches; MovieEditor for visuals/host
  behavior only).
- Evidence: `AGENTS.md` §4; this commit (local).
- Outcome: solved.

### 2026-09-30 — camera — Camera system re-audit (implementation status + quality)
- Did: refreshed the camera conformance audit (`CONFORMANCE_CHECKS.md` was the
  2026-09-25 baseline @ `00f1237`). Landed-since list + counts + quality assessment
  added. Current counts: core follow camera 12 implemented / 2 partial / 0 missing;
  data-fidelity parameters 3 / 6 / 5; extra camera families & controls 5 / 3 / 21.
  Core is engine-faithful (engine position+look-at setters, recovered model, verified
  invariants); the weak layer is data placeholders (anchor/footprint/caps/rows/FOV
  default/near plane) and the missing camera families.
- Evidence: `docs/camera/CONFORMANCE_CHECKS.md` (2026-09-30 section),
  `docs/camera/HOST_DEVIATIONS.md`, code (`client/CameraSystem.cs`,
  `client/CameraSettings.cs`, `client/RebornClient.cs`); this commit (local).
- Outcome: solved (audit).

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

### 2026-09-30 — camera/client — WW trigger removed; sprint placement smoothing fixed; no OS-input testing
- Problem: user report "ww + right drag will falsely bring camera zoom in".
  Reproduced deterministically (`reborn_client_cam-wwdrag.exe`, sandbox map,
  `RC_CAM_DEBUG/SHAKEDBG`): `camdbg mode=sprint dist=1305 obst=0 hit=-1` while
  the real anchor->camera distance `r` collapsed 1305 -> 443 and recovered only
  after the drag stopped (before-log mean r/len 0.77 over 95 unobstructed
  samples, 48 < 0.9).
- Root cause: the resolved-offset placement smoothing (`rSm`) read the *active
  mode row's* `SmoothTime`; in sprint mode that is the sprint row's 0.5 s
  (`SprintCameraSmoothTime`, the pull-back constant already applied by
  `UpdateDistance`) instead of the shared `CharacterCameraSmoothTime` (60 ms;
  PENETRATION_PLAN C1). Per-axis smoothing of the rotating offset then shortens
  the vector through its chord - the B15 class, amplified 8x by the wrong
  constant.
- Fix: placement smoothing uses the character row's `SmoothTime` in every mode
  (live `RebornClient.cs` + model `CameraSystem.Update`); offline regression
  `camera_smoke` "sprint drag keeps constant-length orbit" (old code 47.6% ->
  fixed 1.15% rel err). Live after-fix: mean r/len 0.99 over 136 unobstructed
  samples; only the synthetic >30 rad/s flick troughs dip.
- Then per user decision the WW (double-tap W) sprint trigger was **removed**
  from the client ("for now"); the sprint camera row stays reachable only via
  the `RC_CAM_MODE` test harness. HUD/help and the controls/camera docs updated.
- Process: the external `tools/camera/drive_client.ps1` driver moves the real
  desktop mouse/keyboard - the user stopped it mid-run. From now on: internal
  env-driven test modes only; never drive the user's mouse/keyboard. My driver
  changes were reverted to main; the README row now carries the hijack warning.
- Evidence: `reborn_out/reborn_20260930_151919.log` (before), `_152453.log`
  (after), `_153426.log` (no-WW run, DONE); `camera_smoke_cam-wwdrag.exe` ALL
  PASS; commits `b186564` (fix), `850940c` (driver revert), `3cb1246` (WW
  removal).
- Outcome: WW removed; the camera placement fix kept (valid for any camera mode
  whose row SmoothTime differs from the character row).

### 2026-09-30 — camera/client — Zoom moved from the wheel to +/- keys
- Did: user decision — the wheel no longer zooms; the zoom feature moves to the
  `+`/`-` keys. Removed the `MouseWheel` handlers (panel/HUD/form), added
  `Oemplus`/numpad `Add` = CameraZoomIn (x0.9) and `OemMinus`/numpad
  `Subtract` = CameraZoomOut (x1.1), same `CameraSystem.ZoomBy` rule + clamps.
  HUD help updated (`+/- zoom`); registered as host binding deviation A12
  (the real client binds CAMERAZOOMIN/OUT to the wheel).
- Evidence: `client/RebornClient.cs` (wheel handlers removed, key branch);
  `camera_smoke_cam-wwdrag.exe` ALL PASS; feature client rebuilt
  (`bin64\reborn_client_cam-wwdrag.exe`).
- Outcome: solved (host binding; wheel inert).

### 2026-09-30 — camera/client — First-load camera angle: game pitch sign was inverted
**Problem:** user report — "when game starts the camera looks from the same
height"; the real client feels like it starts from above (~45 deg). Research:
what is the true first-load angle?
**Research (client truth):** the real client restores the per-role saved view
first (`g_Scene_tCameraRuntime` in the role `custom.dat`; this role:
`fYaw=2.3360588550568, fPitch=-0.35000029206276`), with the map init
(`scene_init_param` / `number.krl CameraInitPitch=-0.17`) as the fallback. Game
pitch data is negative when the camera is above/looking down: defaults
fPitch/CameraInitPitch -0.35/-0.17, `SprintCameraPitch=-0.35`, and
`hotkeys.lua` F11 `Camera_SetForceReset(yaw, -pi/12, 1)` = the standard behind
view "pitch -15 deg". The host model pitch is the opposite (positive = above;
measured model +0.15 -> `vpitch` -0.31), so the old host start (model -0.17 ->
vpitch +0.009) was level — the sign was never flipped, and the custom.dat
picker could miss the role file (E6).
**Fix:** negate game pitch on application (startup + map init), F11 reset now
model +pi/12 (game -15 deg), and `FindLatestCustomDat` prefers the newest
custom.dat that carries the runtime block.
**Evidence:** log `reborn_out/reborn_20260930_161907.log` —
`CameraSettings: loaded real per-role camera settings: userdata\...\custom.dat`;
`camera init applied mapId=-1 yaw=2.336 gamePitch=-0.350 modelPitch=0.350`;
camdbg `pitch=0.350 vyaw=2.336 vpitch=-0.492` (= 28.2 deg down, camera above
the head), vs the pre-fix level `vpitch=+0.009`. `camera_smoke_cam-wwdrag.exe`
ALL PASS. Docs: `REAL_VALUES.md` §4 (chain + convention), `HOST_DEVIATIONS.md`
E6 update.
**Re-open:** the exact real follow distance (C10) still scales the perceived
angle; if the CDN per-mode rows land, re-derive the view angle with the real
`TargetDistance`/`CameraHeight`.

### 2026-09-30 — camera/client — Start both follow rows at max range (user decision)
- Did: user report — after the start-angle change the camera no longer starts at
  max range. Per the earlier "both start from max" decision (distance + 广角;
  FOV is already the client panel max), the host now sets **both** the character
  and sprint rows' `TargetDistance` + `InitCameraDistance` to the clamped
  `fMaxCameraDistance` and starts `Distance` there, so the follow camera holds
  at max instead of easing back to the 1245 u client number (C10).
- Evidence: log `reborn_out/reborn_20260930_164645.log` —
  `CameraSystem ready: mode=character dist=2000u`, camdbg `dist=2000 r=2077`
  (offset incl. height), `pitch=0.350 vpitch=-0.441` (camera above, ~25 deg
  down). `camera_smoke_cam-wwdrag.exe` ALL PASS. `HOST_DEVIATIONS.md` C10
  updated.
- Outcome: solved (host/user decision; 1245 stays the client-number reference,
  max is the host start).

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

### 2026-09-30 - controls/client - Movement input core + control keys (agent/move-controls)
- Did: C1/C2 input core (`client/HotkeyTable.cs`): real `ui/hotkey/default.txt` +
  `bindings.ini` decoded at startup (embedded snapshot of
  `proof/movement/extracted/`; `RC_HOTKEY_DIR` live override), VK+modifier
  encoding, Shift ignored for the movement set only (host debug x10); movement
  commands dispatched from the table (W/Up, S/Down, A, D, Left/Right, Space,
  Numpad /, G/NumLock); turn-in-place (camera follows); autorun; jump takeoff
  horizontal velocity from the row's JumpSpeedXY (clamp 0..127, 15 Hz x
  RC_JUMP_SCALE) + ballistic air carry (no WASD air steering - no horizontal
  input term in ProcessAcceleration); landing branch: drop > FallDownHeightFloor
  (500 u) plays the authored F1 FallFloorAnimation (f1b02yd + fist + smalljump c).
- Evidence: run `reborn_out/reborn_20260930_222145.log` (exit 0,
  ns=reborn_client_move_controls.memory): `hotkeys: source=embedded rows=286
  commands=430`; `jump xy takeoff vj=(216,-225) u/s`; forward land +352 u from
  takeoff; `land drop=600u roll=1 clip=... rc=0`; curated
  `proof/controls/movement_controls_run.txt`; scripted `RC_DEMO_MOVE=1`.
- Gates: jx3_model 10x PASS, verify_model exit 0, capture selftest PASS,
  `hotkey_parse.py --movement-check` 9/9 PASS, build exit 0.
- Open: integer 15 Hz port (character logic; combat tables 16 fps per
  JX3_COLLISION_SYSTEM.md:190-191), air-steer/click-to-move path, autorun exact
  cancel set, rebinding/contexts (C3-C6).


### 2026-09-30 - controls/client - Operation modes: classical vs joystick movement routing (agent/move-controls)
- Did: per-mode input routing in the client. CLASSICAL (normal): lateral/back
  input keeps the facing - side-step / back-pedal with the authored
  F1b02yd strafe-left/right and backpedal-01 clips (selected by travel angle vs
  facing); JOYSTICK: turn-to-heading for all input (run clip), camera untouched
  by movement. Turn-key camera coupling fixed: heading<->camera-yaw is the
  cameraYawBehind reflection (Forward(yaw)=(-cos,-sin)); the earlier direct
  "+=" turned the camera the wrong way. Drag uses the row's 15 deg
  CameraAdjustYawWhenMoveTurnDisableAngle dead zone. HUD shows op; periodic log
  gains gait=/mode=.
- Evidence: classical run reborn_20260930_232408.log (strafe/back d=0.00 + the
  two clips, turn char +1.88 / camera follow -1.62, land roll rc=0); joystick
  run reborn_20260930_232519.log (d=-1.57, no strafe clips, camera unchanged);
  clip load pre-check reborn_20260930_231207.log; curated
  proof/controls/control_modes_run.txt; camera_smoke_control_modes ALL PASS.
- Gates: jx3_model 10x PASS, verify_model exit 0, capture selftest PASS.
- Open: M1 per-mode follow mode (nCameraMode [0..3] semantics), M2 reset
  speeds (no reset path yet), free view (Camera_IsInFreeView not located);
  turn-key drag rate is a host interpretation of the documented coupling.


### 2026-09-30 - controls/client - Operation-mode settings: decode pass + switch plumbing (agent/move-controls)
- Did: fresh disasm of JX3RepresentX64.dll camera node setters (type 0xD, pair
  selector [node+0x34], 0=joystick): drag +0x6C/+0x84, drag-pitch +0x70/+0x88,
  max distance +0x74/+0x8C, spring reset +0x78/+0x90, camera reset +0x7C/+0x94,
  follow mode +0x80/+0x98; clamps [0.01,10] / [1,2000] / [0,3] read from the
  binary. SetCameraFollowCharacterAction stores [obj+0x27C], gating the
  UpdateCameraFollowAction path (0x180b0e820). Host: CameraSettings
  ApplyOperationMode() clamps + applies the role's per-mode follow mode and
  reset speeds on every switch (F7 / RC_MODE / RC_MODE_SWITCH_AT), logged.
- Evidence: run reborn_20260930_234456.log (opmode applied: classical
  followMode=0 springReset=1.00 cameraReset=1.00 -> switch test @6000ms ->
  joystick same, exit 0); proof/controls/control_modes_run.txt addendum;
  OPERATION_MODES_PLAN.md 7c.
- Gates: camera_smoke ALL PASS, jx3_model 10x PASS, verify_model exit 0,
  capture selftest PASS.
- Open (no invention): per-frame consumer of follow mode [0..3] and of the
  reset speeds; Camera_IsInFreeView absent from all candidate binaries' string
  tables (hashed Lua registration?). Next: type-0xD node vtable update trace or
  live debug on the real client.


### 2026-09-30 — ui — Battle floating UI catalog from client data
- Did: user asked for battle floating UI (头顶/浮动战斗界面). `#iso` worktree
  `../reborn-iso-battle-floating-ui`, branch `agent/battle-floating-ui`. Extracted
  65/67 UI module paths + 8/11 native caption configs + 6/6 generic-bar follow-ups
  from PakV4 (new harness `proof/ui/evidence/battle_hud/*.txt`), decompiled 33
  modules with a locally built unluac (repo jar is a 9-byte placeholder; build
  recipe in `proof/ui/battle_hud/SOURCES.txt`), re-verified the native caption
  xrefs (`_LoadCaptionConfig 0x180058420`, `PlaySkillEffectText 0x18059FA10`,
  `LuaScene_GetCharacterSkillEffectTextPos 0x1800BBF60`).
- Findings: two floating layers — native `KG3D_CaptionManager` (nameplates,
  `data/public/caption*.ini`, relation colors/icon atlas, `number.krl` layout)
  and KGUI panels (CombatText = damage numbers via world→screen tracks; TopBuff
  per-player head-top buff row + `settings/TopBuff.tab`; target/self frames;
  buff bars; kill feed; combo; combat score; warnings). Corrections: `CastingPanel`
  is the refine/铸造 window, NOT the cast bar; `ProgressBar` is the generic bar;
  `Target.ini` does not exist — `Target.lua` composes `TargetPlayer10/11|S|…`
  (`TargetCommon.ini` found, 878 sections).
- Gaps (each with a named next probe): cast-bar event consumer, `TargetS.ini` MISS,
  `UISetting_HeadTop.ini` absent from PakV4, `caption_images.ini` MISS, ShowModeID
  semantics.
- Evidence: `docs/ui/BATTLE_FLOATING_UI.md` §6; index rows updated in
  `docs/ui/README.md` and `docs/GAME_SYSTEMS_RESEARCH_MAP.md` §4.


### 2026-09-30 — ui — Font coverage: all 5 shipped UI fonts prepared for the renderer
- Question: "do we have all the fonts the game uses?" Answer: the client UI font
  set is exactly **5 loose files** in `<game>\ui\Font\` — `fzht_GBK.ttf` (方正黑体),
  `fzxk.ttf` (行楷), `fzjz.ttf` (剪纸), `FangZhengKaiTi-GBK.ttf` (方正楷体, referenced
  by `fontlist.ini`), plus `MSJH.TTF` (unreferenced fallback; `fontpathlist.ini`
  lists only 3 families). All 5 are in the install; **none were in the repo**
  (`ui-process-app/assets` is git-ignored), so the WPF renderer fell back to
  Microsoft YaHei UI — only one sibling worktree had them copied ad hoc.
- Did: new `tools/prepare_ui_fonts.py` copies the shipped `ui/Font/*` into
  `ui-process-app/assets/ui/Font/` and verifies every `File=` entry of
  `fontlist.ini`/`fontpathlist.ini`; ui-process-app README/AGENTS and
  `docs/ui/UI_SYSTEM_REPORT.md` §3.5/§10 updated.
- Proof: `--fonttest` before `font file not found` vs after
  `family=FZHei-B01 resolved=True`; numeric fingerprints
  `proof/ui/battle_hud/fonttest_before.png` sha256 `41a0147f25b7b221` vs
  `fonttest_after.png` `bd34a77b958bd2b9` (per-cell RGB differs); all 5 copies
  SHA256-match the install.
- Note: editor-only fonts (`MovieEditor\ResourcePack`: Daum/tahoma/aridda/…),
  addon user fonts and the H5 mini-game fonts are separate from the KGUI set.


### 2026-09-30 — ui — Font/color code system decoded (FontScheme=#43)
- Question: layouts reference text styles by code (`FontScheme=43`,
  `FontColor=yellow2`) — what is that system? Answer: 421 font schemes in
  `\UI\Scheme\Elem\font.ini` (id → FontID + Color/Size/Border*/Projection*),
  36 slots in `fontlist.ini` (File + base Size/Vertical/Dpi/Chat), 3 families in
  `fontpathlist.ini` feeding 4 of the 5 shipped `ui/Font` files, and 106 rows /
  105 names in `color.txt` (red6 duplicated; lookup case-insensitive). 358/421
  schemes use the fontlist base size (`Size=0`; 346/358 names agree — engine math
  still open at `KFontSchemeMgr::LoadScheme` `0x1801F5C30`).
- Did: new `docs/ui/FONT_SCHEME_SYSTEM.md` + `tools/ui_scheme_lookup.py`
  (`--list/--color/--scan/--census`); census over 160 INIs = 4,595 refs / 125
  distinct schemes, 0 unknown ids, top `#18 方正黑体15白-阴影灰7` (2,397);
  engine symbols re-xrefed (`LoadScheme 0x1801F5C30`, `LoadFontList 0x1801F5790`,
  `LoadFontPathList 0x1801F63D0`); per-state font keys counted (MouseOverFont 904
  etc.). Renderer gaps recorded: projection (阴影) not drawn, per-state fonts,
  WndEdit caret/placeholder, `<Dn>` rich-text tags.
- Evidence: doc §8 + `proof/ui/evidence/scheme/*` (tracked).


### 2026-09-30 — ui — Font/color scheme engine decode + cast-bar driver found
- Did: continued the open items. Added RIP-relative string annotation to
  `tools/pvp/dump_fn_disasm.py`; committed annotated disasm of the KGUIX64 font
  pipeline (`proof/ui/evidence/battle_hud/re/kgui_font/`).
- Decoded (HIGH): `LoadScheme` record 0x40 B (`FontID@0 / Size@4 / BorderSize@8u16 /
  ProjectionSize@Cu16 / Color@0x10 / BorderColor@0x14 / ProjectionColor@0x18 /
  Name[32]@0x1C / FontScale@0x3C`, file via `SchemeElemFont`, Size default 12);
  `SetFontScheme` (item+0x2F8 scheme id, +0x2FC slot, +0x300 fill ARGB, +0x314
  max(0,FontScale); border/projection setters 0x180121FC0/0x180122090 take
  `(u16 size, ARGB)`); `LoadFont` (36 slots, `size=(slot.Size+mgr+0x4C)*mgr+0x48`);
  `SetFontScale` (KWndStation::SetUIScale → mgr+0x48 → LoadFont → UI_SCALED);
  `KColorSchemeMgr::Init` (color.txt = 4-col `siii` tab, array + linear **first-match**
  scan); `UpdateCodePage` (locale font path list); per-state font keys in the
  button/edit decoders; `GrayFontColor` = `uiconfig.ini [GrayFontColor] 207/207/207`.
- Fixed renderer deviation: `red6` is defined twice — engine (and now
  `ui-process-app/Engine/Fonts.cs` + `tools/ui_scheme_lookup.py`) uses the **first**
  row `255,27,27` (scheme 208 is used by one shipped layout).
- `Size=0` closure: all 58 differing `Size>0` override schemes are unused by every
  shipped layout → the renderer rule matches all real usage; the engine glyph-size
  consumer site stays unpinned (next probe: writers of `KItemText+0x2F4`).
- Cast bar: driver is the native represent layer via `REPRESENT_CALL` →
  `ui/script/representcommand.lua` `CreateProgressBar` → `GeneralProgressBar_Create
  (name, tableID, …)` → `ProgressBar.tab` presets (161 rows incl. `szIniName`) +
  `ProgressBarPlus.txt` / `AutoProgressBarInfo.txt`; `CASTINGBAR_START/END` have no
  Lua consumer in any extracted corpus (native listener). `BATTLE_FLOATING_UI.md`
  updated.
- Verified: `dotnet build` 0 errors; `--fonttest` → `family=FZHei-B01 resolved=True`;
  `ui_scheme_lookup.py --color red6` → `#FF1B1B` (first row).


### 2026-09-30 — ui — Battle-UI open items closed: target layouts, UISetting page, caption_images
- Target layouts: `Target.lua` naming decoded — players `TargetPlayer10` (not enemy) /
  `TargetPlayer11` (enemy); NPCs `"Target"..GetNpcIntensity(npc)..relation` with
  `nIntensity 2|6→4, 5→3, 4→2, else 1` and relation 2/1/0; `S` is appended only in
  standard-target mode (`Target.bStandard`). All 28 layouts extracted (12 NPC + 2
  player, each ±S) + `TargetCommon.ini`; `TargetS.ini` never exists.
- `UISetting_HeadTop.ini` closed: the page is `WndContainer_HeadTop` inside
  `UISetting.ini` (HIT, 505 KB / 1,749 sections).
- `caption_images.ini` closed: the string is absent from the current
  `KG3DEngineAdapterX64.dll` (probe + string scan) — no such file in this build.
- Evidence: `docs/ui/BATTLE_FLOATING_UI.md` §2.2/§4/§6; tracked candidate list
  `proof/ui/evidence/battle_hud/pakv4_candidates_target.txt`.


### 2026-09-30 — ui — Remaining UI open items: WhoSeeMe, ShowModeID, markup, alias
- WhoSeeMe binding conflict **closed**: `WhoSeeMe.lua` registers the window
  (`ui/Config/Default/WhoSeeMe.ini`, `Normal/WhoSeeMe`, `WhoSeeMe.bCheck`,
  `SYNC_SELECT_ME_PLAYER_NOTIFY`); `HatredPanel.lua` only drives HatredPanel — the
  INI `ScriptFile=HatredPanel.lua` is stale.
- `ShowModeID` **partially decoded** (dumps `re/kgui_showmode/`): comma list (≤128,
  ids ≤127) → 128-bit mask at `window+0xC58`; bit 0 (Default) is set/cleared from
  `ShowWhenHideUI != 0` right after parsing (`0x1800CE034`); `[Balloon] ShowModeID`
  uses the same mask (fn `0x18020E250`). Visibility test (`IsVisibleInShowMode`)
  still open. `UI_SYSTEM_REPORT.md` §9 updated.
- Inline markup: format produced by represent `OnReloadTable` (`0x18031FE40`):
  `<text> text="…" font=N r=R g=G b=B </text>` (`font=10 r=255 g=165 b=0` template);
  KGUI string.txt uses the `font=N`-only variant with N≤177 (scheme ids, MED). The
  parser itself is not in KGUI/JX3UIX64 string tables — still open.
- `CombatText` `REPRESENT_*` alias table is dead code — `OnEvent` dispatches by
  explicit string compare (decompiled 1104-1113); closed.
- Evidence: `docs/ui/BATTLE_FLOATING_UI.md` §4; `re/kgui_showmode/`,
  `re/represent_markup/` (tracked).


### 2026-09-30 — ui — ShowModeID fully decoded (allow-list for special modes)
- Mask-use scan (16 functions read `window+0xC58`) → the render gate at
  `0x180131E6F`/`0x18015737F` is: no active show mode (`KWndStation+0xCB24==0`) →
  window drawn (normal play ignores `ShowModeID`); with an active mode id
  (`+0xCB28`): drawn iff `mask[word] & (1<<(mode&63))`, **except** windows with
  flag `0x800` (`ShowWhenHideUI`) which always draw. So `ShowModeID` is an
  allow-list for special modes, and `ShowWhenHideUI=1` is the persistent HUD.
- `UI_SYSTEM_REPORT.md` §9 rewritten; `BATTLE_FLOATING_UI.md` §4.6 closed;
  evidence `re/kgui_showmode/window_mask_uses.txt` + InitShowModeInfos dump
  (showmode.txt loader) committed.


### 2026-09-30 — ui — Border/projection setters decoded (clamps + item fields)
- Added `@<addr>` support to `tools/pvp/dump_fn_disasm.py` (dump a function by
  address), then dumped the three font setters:
  `0x180121FC0` border — **size clamped ≤4**, color `item+0x304`, size byte `+0x310`;
  `0x180122090` projection — **clamped ≤255**, color `item+0x308`, byte `+0x311`;
  `0x180122160` builds the per-glyph draw struct (fill/border/proj colors
  `+0x80/+0x84/+0x88`, sizes `+0xA8/+0xA9`, resolved font-size float `+0x2F4` →
  `+0x94`, FontScale `+0x314` → `+0x90`). Border color alpha is attenuated by
  `(item+0x14 × item+0x18)/65025`.
- `FONT_SCHEME_SYSTEM.md` §2.2 updated; dumps
  `re/kgui_font/setters/` committed. Renderer gap remains: projection not drawn,
  border approximated (`DropShadowEffect`).


### 2026-09-30 — ui — Rich text: shipping text layer is KGUICocosX64
- Byte scan of `bin64` for markup tokens: `KGUIX64.dll` has only `RichText` (6×);
  **`KGUICocosX64.dll` has `<text`, `richtext`, 164× `RichText`** plus
  `TipRichText`/`GetRichText`/`OnRichTextOpenUrl`/`RichTextImageRenderer` and the
  lowercase INI key table (`richtext`, `multiline`, `halign`, `showall`,
  `reversemask`, `shaptexture`, …) — i.e. the Cocos control layer is the shipping
  text/rich-text runtime, and the decoder is case-insensitive.
- `ccui.KGUIText` Lua binding table at `0x1803A4490`
  (`SetFontScheme/GetFontScheme`, `SetRichText`, `SetAutoEtc`, `MultiLine`, …);
  layout exposes fragment runs (`text/rowTop/relX/relY/absX/absY/width/height/
  visible/alpha/isTextFragmentRun`) — the markup parser feeds these runs.
  Exact parser not pinned (next probe: `SetRichText` binding target / fragment
  builder). Represent produces the markup: `OnReloadTable` `0x18031FE40`,
  template `font=10 r=255 g=165 b=0`.
- Dumps `re/cocos_richtext/` committed; `FONT_SCHEME_SYSTEM.md` §2.4 updated.


### 2026-09-30 — ui — KGUI-vs-Cocos is a gray feature; EndOfBattle/ComboWinEffect openers
- **Renderer choice decoded**: `JX3ClientX64.exe` `KLoadGrayFeatureConfig`
  (`0x140099820`) reads feature `KGUIUseCocos` (`Percent` default 0/0-100,
  `Override` in per-user settings, `IntraNet`); live `config/gray_config.ini` has an
  empty `[KGUIUseCocos]` section → **Cocos UI inactive; KGUIX64 is the live control
  renderer on this install**. Dumps `re/cocos_gray/`.
- **Openers decoded**: `EndOfBattle` ← `ON_CASTLE_END_ACTIVITY`
  (`ui/script/module.lua`: `CampMaps.ClearData(); EndOfBattle.Open(arg0)`);
  `ComboWinEffect` ← `ON_ARENA_COMBO_WIN` (`ui/script/arena_head.lua`).
- `GeneralProgressBar` producer is not in any readable binary (likely protected
  `JX3ClientX64Base.dll`) — per-action row mapping needs a runtime probe;
  `FullScreenWarning` opener and `ProgressBar.Start/Finish` callers still open.
- Evidence: `BATTLE_FLOATING_UI.md` §2.4/§4.8; `re/cocos_gray/` committed.


### 2026-09-30 — ui — KGUI rich-text flag + draw-extent path; static probes exhausted
- `LuaItemText_SetRichText` (`KGUIX64 0x1801978F0`) toggles item flag bit 23
  (`0x800000`); `IsRichText` reads it; six text-processing functions test the bit
  (`0x180106CBF`, `0x180107A2F`, `0x18011F8FF`, `0x18012006F`, `0x1801207AF`,
  `0x180120FBF`) — parser body not isolated; next probe is a runtime breakpoint on
  a known markup label.
- Text draw path continues through virtual font-renderer calls
  (`0x1800FBE70` extent filler, vtable `+0x138/+0x148/+0x158`); projection
  rasterization stays unresolved statically.
- Remaining statically-blocked items (need runtime probes): markup parser,
  `GeneralProgressBar` native caller (protected base DLL), `FullScreenWarning`
  opener, `ProgressBar.Start/Finish` callers, live `IsUseCocos` confirmation.
- Dumps `re/kgui_richtext/`, `re/kgui_font/glyph/` committed.


### 2026-09-30 — ui — Full UI Lua sweep (1,615 files) closes the opener questions
- Extracted every `ui/Config/**/*.lua` + `ui/Script/**/*.lua` from the manifest in
  one batch pass (`pss_assets.run_pakv4` grouped chunks; 1,615/1,619 HIT) to the
  local ignored `proof/ui/battle_hud/ui_sweep/`; candidate list tracked
  (`proof/ui/evidence/battle_hud/pakv4_candidates_fullui_lua.txt`).
- Needle results: **`CASTINGBAR` = 0** (native-only listener; cast/generic bars are
  native `REPRESENT_CALL` → `representcommand.lua` → `GeneralProgressBar`),
  `CreateProgressBar`/`REPRESENT_CALL` only self, exact-identifier `ProgressBar`
  only infra (`globalmgr.lua` custom data, `table*.lua` paths) — no `Start/Finish`
  caller; `FullScreenWarning` opened by `DynamicCarrierBar` (≤15% HP → `Open(1000)`
  extend-loop) and `CoinShop_View`; `EndOfBattle`/`ComboWinEffect` confirmed.
- Tracked findings: `proof/ui/evidence/battle_hud/sweep_findings.md`;
  `BATTLE_FLOATING_UI.md` §2.1/§2.4/§4.8/§6 updated.
- Follow-up: catalog appendix (§A) lists 61 battle-HUD modules found by name in the
  sweep; their INIs were batch-probed **61/61 HIT** and recorded
  (`appendix_inis.tsv` + `pakv4_candidates_appendix_inis.txt`). Notable gates:
  `WarningTipPanel` Topmost1 CENTER with a 12-mode list, `LootRoll` Topmost2
  `TOPCENTER,0,240` with `ShowModeID=0,15`, `FightingWarning` Topmost2; naming
  quirks `WeaponSkillBar.ini` root `SpArmsActionBar`, `PVPInput.ini` root
  `SkillIntroduce`.


### 2026-09-30 — ui — Live renderer = Cocos (corrected) + live text API map
- **Correction:** this build runs the **Cocos UI**. Evidence: `config/cocos_config.ini
  [Main] KGUIUseCocos=1`; `gray_config.ini [KGUIUseCocos] Percent=5` (client-rewritten
  2026-09-27; the earlier empty read was stale); `gray_usersettings.ini
  [Environment] IntraNet=0`; the `IsUseCocos` binding (`JX3ClientX64.exe 0x1400A5750`,
  table `0x140A3E210`) returns constant 1; `ui/Script/base.lua` sets
  `USE_COCOS = IsUseCocos()`; the client module list (`%s%s.dll`) includes `KGUICocos`.
- `KGUICocosX64` ships a 1:1 `KFontSchemeMgr` port (same `SchemeElemFont` keys, 36
  slots, `(slot+mgr+0x4C)*mgr+0x48`, slot array `+0x57B8`) and the full `ccui.KGUIText`
  API (binding table `0x1803A4490` parsed: SetText/SetString/SetFontScheme/
  SetFontBorder/SetFontShadow/SetRichText/GetFragmentRuns/...).
- Live scheme apply `0x180346AA0` → glyph font builder `0x180347E80` passes size+scale;
  scheme→style converter `0x1802CC070` defaults style size to 16. Legacy KGUIX64
  ignores scheme `Size`; the live Cocos path uses it — the exact `Size=0 → slot base`
  substitution point remains the one open renderer detail.
- Docs: `FONT_SCHEME_SYSTEM.md` §2.1/§2.4, `BATTLE_FLOATING_UI.md` §1.2; dumps
  `re/cocos_font/`, `re/cocos_richtext/`, `re/cocos_gray/` committed (`d5eb54a`).


### 2026-09-30 — ui — FINAL font Size answer (supersedes the note above)
- The Cocos style/decoration converter (`KGUICocosX64 0x1802CC070`, dumped) reads the
  scheme record's `FontID (+0)`, `BorderSize (+8)`, `ProjectionSize (+0xC)`, colors
  (`+0x10/+0x14/+0x18`) and `FontScale (+0x3C)` — **never `Size (+4)`**; its style
  size is a constant default `0x10` (16) fallback.
- Both `KFontSchemeMgr::LoadFont` ports create the 36 slot fonts at
  `(slot.Size + mgr+0x4C) * mgr+0x48` = the **fontlist slot base size** × global
  scale. Therefore **effective rendered size = slot base size × scales**, and the
  scheme `Size` field is editor metadata in *both* renderers (legacy KGUI and live
  Cocos). The 58 differing `Size>0` schemes are unused placeholders — zero impact.
- `FONT_SCHEME_SYSTEM.md` §2.1 rewritten to the final answer; no renderer change
  needed (`Size>0 ? Size : base` coincides with the slot base for every used scheme).


### 2026-09-30 — ui — Static-analysis bedrock: protected module is the boundary
- `JX3ClientX64Base.dll` probed: `.tp6d` section **entropy 8.00**, 20.8 MB `.tvm0`
  VM section, and **no plaintext** for `CreateProgressBar`, `REPRESENT_CALL`,
  `</text>`, `font=`, `text="`, `<text`, `KGUICocos`, `IsUseCocos`. The two
  remaining unknowns (native `CreateProgressBar` producer / per-action
  `ProgressBar.tab` row; exact UI markup parser body) are therefore in that
  protected module or in a literal-free parser; both need a live debugger session,
  which the no-injection rules exclude. Documented as the final boundary in
  `FONT_SCHEME_SYSTEM.md` §2.4.
- Cocos `<`/`>` compare scan (57 functions) and `SetText`/`SetString` callee dumps
  (`0x180349E00`, `0x180345D10`, `0x180337AC0`, `0x180338020`) show no char-level
  tag parser — the label markup is handled outside the readable UI DLLs.


### 2026-09-30 — ui — Scheme tables staged for the renderer + `--fonttest` scheme proof
- `tools/prepare_ui_fonts.py` now also copies the scheme tables
  (`font.ini/fontlist.ini/fontpathlist.ini/color.txt`) into the git-ignored
  `ui-process-app/assets/ui/Scheme/Case/` (the app's `SchemeRoot`).
- `UiProcessApp --fonttest` extended to print scheme resolutions; output matches
  the decoded client values: `#18 size=15 #F0F0F0`, `#43 size=20 #000000`
  (方正黑体20黑), `#212 size=14 #F0F0F0`, all `fzht_GBK.ttf`. This locks the
  scheme chain (id → size/color/border → FontID file) into a runnable check.
- Gates re-run green: `jx3_model.py` PASS, `verify_model.py` PASS,
  `loot/capture.py selftest` PASS. README/AGENTS + `FONT_SCHEME_SYSTEM.md`
  verified line updated.


### 2026-09-30 — ui — UI gate made runnable from a fresh checkout (asset staging)
- New `tools/prepare_ui_configs.py`: reads `Data/ui_inventory.json` and stages
  every referenced layout/settlement INI from PakV4 into the git-ignored
  `assets/ui/Config/Default/` + `assets/pak/` (21 files, all HIT). With the
  existing `prepare_ui_text.py` + `prepare_ui_fonts.py` the whole UI gate now runs
  in a fresh worktree.
- `UiProcessApp --selftest` in this branch: **rendered=19 skipped=2 failed=0**
  (skipped by design: ready-prompt native, staging-countdown no renderer); fonttest
  prints the scheme proof. AGENTS/README gate text updated from the stale 15/15.
- This also validates the earlier renderer changes (Fonts.cs first-wins) against
  the full 21-window inventory.


### 2026-09-30 — ui — Battle-HUD appendix rendered in ui-process-app (33 windows, 31/2/0)
- Extended `Data/ui_inventory.json` with stage 9 (`battle-hud`), the first render
  batch of the battle-HUD appendix from `docs/ui/BATTLE_FLOATING_UI.md` §A:
  BuffMonitor, BattleFieldObjective, BattleIntegral, FightingWarning,
  WarningTipPanel, TargetSkill, SingleFStatistic, LootRoll, NumericalPanel,
  TeamNumList, RecoverEquipment, SkillCDJingYuJue (12 windows; inventory is now
  9 stages / 33 windows).
- `tools/prepare_ui_configs.py` fixed to preserve subfolders (BattleIntegral lives
  at `Config/Default/BattleField/BattleIntegral.ini`; it was being flattened and
  MISSed the app's exact-path loader).
- `--selftest`: **rendered=31 skipped=2 failed=0**; README/AGENTS counts updated;
  inventory `generated`/`source` metadata now includes stage 9's catalog.


### 2026-09-30 — ui — Battle-HUD appendix batch 2 (58 windows, 56/2/0)
- Added stage 10 (`battle-hud-2`, 25 windows): BuffMonitorGeneral/YaoZong/DaoZong,
  MonsterBuffPanel/Choose/SkillPreset, TeamStatePop/Countdown/SwitchBtn/
  TagPlayers/PlayerTagList/NumListLong, LootRollMini/LootShowList,
  GoldTeamLootList/Distribution, BattleFieldHSLHNotice, DesertStormOB, NewSkillBar,
  SkillRemind/SkillTipPanel, FBCountNum, PQwarning, YaoZongSkillHint, MingJiaoSkill.
  Every INI path was re-probed in PakV4 first (`resolve_batch2_paths.py`), all
  `ui/Config/Default/<name>.ini`.
- Gate: **rendered=56 skipped=2 failed=0** over 58 windows (stage 9 = 12, stage 10
  = 25 appendix modules; 2 skipped by design). README/AGENTS counts updated.


### 2026-09-30 — ui — Battle-HUD appendix complete (81 windows, 77/4/0)
- Added stage 11 (`battle-hud-3`, 23 windows) covering the rest of the appendix:
  DynamicSkillBar, WeaponSkillBar, VkActionBar, DesertStormInfoPanel,
  ACC_BFInfo/ACC_DesertStormInfo/ACC_TreasureHuntInfo (BattleField subfolder),
  ZombieFightFinal, RoommateTeam, PVPInput, PVPRandomForce, VampireInfoPanel,
  InterludeHSLHPanel, MobaInformationPanel/MobaPVPList (BattleField),
  TongBattleTips/TongBattledragonTips (BattleField), BattleTipPanel,
  SkillIntroduce/Glossary/Formula/Teaching/Guide. Paths re-probed per module
  (`resolve_batch3_paths.py`); all 60 appendix modules now inventoried.
- Two renderer gaps found and marked **PARTIAL** (no `path`, so the gate skips
  them cleanly): `PVPRandomForce.ini` and `SkillGlossaryPanel.ini` throw a WPF
  "element is already the logical child of another element" error — no duplicate
  section names or parent cycles in the files; next probe: isolate the re-parented
  element (likely a content-host/PageSet re-issue).
- Final gate for the branch: **rendered=77 skipped=4 failed=0** over 81 windows;
  README/AGENTS counts updated.


### 2026-09-30 — ui — Renderer bug fixed: section names are case-sensitive
- **Root cause of the two PARTIAL windows:** real INIs contain **case-variant twin
  sections** (`PVPRandomForce`: `Handle_BG` vs `Handle_Bg`; `SkillGlossaryPanel`:
  `Image_Line1` vs `Image_line1`). The app keyed sections by name with
  `StringComparer.OrdinalIgnoreCase` (`IniFile.ByName`, `UiBuildResult.Elements/
  Sections`), so one twin overwrote the other and the surviving visual was added to
  two parents → WPF "element is already the logical child of another element".
- **Fix:** section-name identity is now `Ordinal` throughout (`IniFile.ByName` +
  tolerance fallback for suffix inheritance; `UiBuildResult` maps; UiLayout name-keyed
  maps/sets and parent comparisons; LayoutPlan name-keyed sets/maps and page
  comparisons). Diagnostics added: `AddChild` now names the failing section.
- **Proof:** `--selftest` went 77/4/0 → **79/2/0** (both windows render, nothing
  else regressed); inventory entries restored to PROVEN; README/AGENTS updated.
- This is a renderer parity fix (engine section names are case-sensitive), not a
  special case: any window with case twins now renders correctly.


### 2026-09-30 — scope correction: research only, app changes reverted
- **User correction:** "I never told you to fix anything, you were only supposed to
  be researching." The session had drifted from the UI research brief into changing
  `ui-process-app` (Fonts.cs color order, case-sensitive section identity in
  IniFile/UiLayout/LayoutPlan, `--fonttest` scheme output, inventory expansion to
  81 windows, app README/AGENTS + root gate text) and adding the app-staging tool
  `tools/prepare_ui_configs.py`.
- **Revert:** `ui-process-app/**` and root `AGENTS.md` restored to `main`;
  `tools/prepare_ui_configs.py` deleted; `tools/prepare_ui_fonts.py` restored to its
  fonts-only version; the research docs updated so they no longer claim any fix
  (`FONT_SCHEME_SYSTEM.md` §1.4/§5/§8 now record the `red6` last-wins and case-twin
  behavior as **known, unfixed deviations**). `main` was never touched; all of this
  lived on the isolated branch `agent/battle-floating-ui` and nothing was pushed.
- **Kept (research output):** `docs/ui/BATTLE_FLOATING_UI.md`,
  `docs/ui/FONT_SCHEME_SYSTEM.md`, index/EXPERIENCES updates, the `proof/ui/evidence/battle_hud/**`
  extraction + disasm evidence, and the pure-research tools `tools/ui_scheme_lookup.py`
  and the `dump_fn_disasm.py` annotation.

### 2026-09-30 — pvp/sandbox — 江湖木桩 target-dummy sandbox (dummies only)
- Did: extracted the shipped
  `settings\NpcTemplate\{ZhuChengMuZhuang,GongNengTongYongNPC}\sNpcTemplate.tab`
  dummy rows (14 dummies, 3 groups: 江湖木桩/试炼木桩/其他木桩; 试炼教官 rows are
  NPCs and excluded) via new `tools/netcode/mode/extract_target_dummies.py`; built
  `target_dummy_sandbox/` (own namespace `TargetDummySandbox.memory`, own out dir
  `target_dummy_sandbox_out`) listing name/RepresentID/NPCID/level/HP/defence and
  displaying the selected dummy. Display is represent-first
  (`AddRepresentModel(RepresentID)`), fallback
  `AddDummyModel(GetRepresentModelPath)` + `GetRepresentAniPath` idle ani
  (`TD_PATH=1`).
- Evidence: `proof/pvp/target_dummy_sandbox_smoke_20260930.txt` — all 14 IDs probe
  to `data\source\npc_source\练功木桩001\模型\wj_练功木桩001|002.mdl`, all spawn
  handles valid, screenshots non-empty (`target_dummy_sandbox_shots_20260930.txt`);
  fallback `target_dummy_sandbox_fallback_20260930.txt`. Gates: jx3_model 10x PASS,
  gravity model, loot selftest PASS.
- Note: PvP basis = live sNpcTemplate stats (主城试炼木桩 `MaxLife=500,000,000`,
  def 33k-83k) + buff 28496 木桩心法属性 化劲/御劲
  (`proof/pvp/attributes/buff_pvp_rows.txt:49`); the stale `pak_out4/Buff.tab`
  copy lacks that ID — re-extract the live `skill\Buff.tab` when combat lands
  (re-open criterion in `docs/pvp/TARGET_DUMMY_RESEARCH.md`).
- Outcome: solved (sandbox + docs delivered; model display verified).


### 2026-09-30 — client — one 试炼木桩 at the player spawn (sandbox-target-dummy)
- Did: user clarified "sandbox" = the client, so `client/RebornClient.cs` now
  spawns one dummy right after the player is placed: `RC_DUMMY` (default 35901
  初级试炼木桩; 0 = off) at `RC_DUMMY_DIST` (default 400 u) along the measured
  view direction, terrain-sampled; model via `GetRepresentModelPath`, idle clip
  via `GetRepresentAniPath` + `KGModelCLR`. Feature build
  `reborn_client_target-dummy.exe` (title `sandbox-target-dummy`, ns
  `reborn_client_target-dummy.memory`); canonical exe/configs untouched.
- Evidence: `proof/pvp/target_dummy_client_run_20260930.txt` — dummy handle valid
  at (23334,740,24624) vs player spawn (23334,761,24224); screenshot
  `rc_02_14000ms.png` shows the player and dummy together
  (`target_dummy_client_shots_20260930.txt`).
- Outcome: solved (requested in-client dummy delivered; the §4 browse sandbox
  stays as a separate research tool, not the deliverable).


### 2026-09-30 — controls — target selection research (how to target someone in front)
- Did: decompiled the shipped b03 targeting script and disassembled the engine
  Lua bindings; documented the real mechanism in
  `docs/controls/JX3_TARGET_SELECTION.md`: Tab runs `SearchForEnemy(player,
  nRadius, nAngle)` over 3 facing-axis cone zones (MidAxis 2560 u/15/level 3,
  Inner 512 u/85/level 2, Outer 1280 u/114/level 1), filters via
  `CanSelectNpc/Player` + `SELECTABLE_*`, sorts by player/screen/level then
  axis-offset weight `dist × sin(angle from facing)`, and calls
  `SelectTarget(TARGET.NPC|PLAYER, id)`; click uses the engine pick; selection is
  client-local (no opcode; target rides with cast intents).
- Evidence: `proof/pvp/disasm_targeting/` (7 functions incl.
  `KPlayer::LuaSearchForEnemy` @ 0x1403F03D0), decompiled
  `proof/collision/ui_scripts/target_b03.utf8.lua:17-113,234-317,335-423,862-913`,
  bytecode dump `proof/controls/ui_lua/target_script.dump.txt`.
- Open: `nAngle` unit (deg vs legacy 1.40625 unit, MED) — next probe disasm
  0x140242220; mouseover-cast.
- Outcome: research delivered (doc registered in `docs/controls/README.md`).


### 2026-09-30 — controls — target-selected HUD (what appears on select)
- Did: extracted the target-frame layout `ui/config/default/Player.ini` (88
  sections) + element library `TargetCommon.ini` (878 sections) and mapped the
  target module `ui/script/Target.lua` onto them; documented the HUD in
  `docs/controls/JX3_TARGET_SELECTION.md` §9: avatar+school icon, name/level,
  HP bar (hit flash, animated fill) + text, mana bar/no-mana state, absorb/shield
  overlays, camp/team relation icon, party/NPC marks, boss variant, invincible,
  in-combat glow, custom-mode label; buff/debuff rows via `BuffMgr` (timers,
  dispel highlight); action/cast progress bar (`ACTION_STATE`/`PROGRESS_BAR_TYPE`);
  per-school target handles (`Handle_TM/CJ/MJ/...` under `Handle_tot`).
- Evidence: `proof/controls/target_frame_elements_20260930.txt` (parsed summary);
  raw INIs not committed (game assets).
- Outcome: question answered; HUD inventory registered in the targeting doc.


### 2026-09-30 — client — real target HUD (client UI assets, no hand-drawn art)
- Did: replaced the hand-drawn target frame with a renderer that draws the
  client's own selected-target window `ui/Config/Default/TargetTarget.ini`
  (50 sections, script Target.lua) using its real `.UITex` atlases + `ui/Font`
  text schemes. New `tools/netcode/ui/extract_target_frame.py` pulls the
  layout + atlases + textures + scheme files from the client PakV4 into the
  git-ignored `assets/ui/targetframe/`; `client/UiClient.cs` parses the INI,
  resolves PosType placement, decodes UITex/TGA/DDS, and draws with the real
  fonts/colors. The HUD is composited by a per-pixel-alpha layered window
  (`TargetFrameControl : Form`, WS_EX_LAYERED + UpdateLayeredWindow) because a
  WinForms child control cannot blend over the engine's child HWND.
- Evidence: client run `reborn_20260930_233743.log` (`target=初级试炼木桩 ...
  zone=MidAxis`, no `target ui` warnings); window capture fingerprint
  `proof/controls/target_hud_client_20260930.txt` (TargetBg plate + HP bar +
  game-font name/level/camp visible). Gates: jx3_model 10x PASS, gravity,
  loot selftest PASS.
- Note: `Player.ini` (the first pick) is the player's own frame; the selected
  target window is `TargetTarget.ini` — corrected in
  `docs/controls/JX3_TARGET_SELECTION.md` §9.
- Outcome: solved; runtime-set portrait face / buff rows / cast bar are
  skipped until their state exists (missing art draws nothing, per §6).


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

### 2026-10-01 - controls/client - Correction: classical A/D turns (bytecode re-decode)
- Problem: the client implemented classical A/D as side-step, but in the real
  game (user report) classical A/D turn. Root cause: the earlier
  RESEARCH_RESOLVED_GAPS summary of hotkeys.lua was wrong - it described only
  the non-free-view branch.
- Fix: re-decoded the actual bytecode (lua51_probe): StrafeLeftStart proto 0/76
  branches CLASSICAL -> SetControl + Camera_IsInFreeView -> TurnLeftStart;
  JOYSTICK -> ResponseWASDKey (double-tap). Forward/back/turn have no mode
  branch. Host now: classical A/D turn in place by default (RC_FREEVIEW=1,
  camera drags behind via cameraYawBehind + 15 deg dead zone); RC_FREEVIEW=0
  restores the decoded side-step branch with the 挪步 clips; S stays
  back-pedal in classical.
- Evidence: runs 20261001_003459 (classical free view: d=-3.14, cam follows),
  20261001_003600 (RC_FREEVIEW=0: d=0.00 side-step), 20261001_003644 (joystick:
  d=-1.57); proof/controls/control_modes_run.txt; gates all PASS.
- Lesson: decode the handler bodies, not the string/global summaries.


### 2026-10-01 - controls/client - Mode-matched locomotion animation
- Did: locomotion clip selection by travel direction vs facing in both modes
  (>45 deg 挪步 left/right kind 6, >135 deg 后退01 kind 57, else walk/run).
  Classical side-step (RC_FREEVIEW=0) and S play the authored clips; the
  joystick pivot plays 挪步/后退 while the facing catches up, then 奔跑.
  Classical A/D turn-in-place rotates the model: the F1 catalog has no
  dedicated ground turn clip (kinds 5 run / 6 挪步 / 56 walk / 57-58 后退 /
  16-19 jumps), so none is invented.
- Evidence: runs 20261001_074718 (classical: 后退01, turn d=-3.14),
  074923 (freeview0: 挪步左 + 后退01), 074806 (joystick: 挪步左->奔跑,
  d=-1.57); proof/controls/control_modes_run.txt; build + smoke exit 0.
- Open: engine clip-selection criteria (thresholds are host values); ground
  turn-in-place clip unresolved (catalog-verified absent).


### 2026-10-01 - controls/client - Classic base actions: sit + sheath implemented
- Did: decoded ToggleSitDown (0/98: OnUseSkill(17 打坐) / Stand()) and
  ToggleSheath (0/97: SetSheath toggle; gates sit/death/fight/bird/horse/
  tower/buff) from the packed hotkeys.lua bytecode. Host: V/X toggles the
  looping F1b02dj打坐a.tani pose and stands on movement/jump; Z toggles the
  b02 draw sequence (F1b02ty拔剑01_start01 -> st01_持续 stance), rejected while
  sitting, back to idle on sheathe (no 收剑 clip ships for b02). HUD help and
  the classic-controls audit updated.
- Evidence: run reborn_20261001_081922.log (sit down/stand + 打坐a; sheath
  drawn -> 拔剑01_start01 -> st01_持续; sheathed; exit 0);
  proof/controls/control_modes_run.txt; camera_smoke ALL PASS; jx3_model 10x
  PASS; verify_model/capture selftest exit 0.
- Open: mount (T) needs the horse actor; follow/interact need targeting; the
  sheath gates not modellable in the host (fight/bird/horse/tower/buff) are
  always false.


### 2026-10-01 - controls/client - Full classic movement matrix (S slow, W+A/D diagonals)
- Problem: classic had only been given A/D turn; W+A/W+D and S were wrong
  (user report: S must be a slower backward move, W+A and W+D must differ).
- Research: decoded hotkeys.lua proto 0/46 (ResponseWASDKey) - it merges
  Forward/Backward and (Turn|Strafe)Left/Right into the 8-way MOVE_* intent
  and passes it to ResponseDisplacementHotkey (proto 0/44, the sprint/skill
  dispatcher). number.krl ships no back speed (walk 6 / run 20 only), so the
  observed slower backward = walk pace (96 u/s).
- Fix: host classic matrix - A/D alone turn (camera follows); W+A / W+D run
  the camera-relative 45 deg diagonal with the facing turning to it and no
  camera turn; S / S+A / S+D back-pedal at 96 u/s facing-kept; arrows always
  turn; run/walk speeds otherwise unchanged.
- Evidence: run reborn_20261001_083417.log - S dpos dist=96 (walk) with 后退
  clip; WA dpos=(33,382) / WD dpos=(383,-33) dist=384 (run speed, mirrored
  diagonals, camera unchanged); A alone d=-3.14 turn in place; exit 0.
  camera_smoke ALL PASS; jx3_model 10x PASS; verify/capture selftest exit 0.
- Open: diagonal-back clip selection uses the host angle thresholds; the
  yaw-turn-speed consumer is still not located (registered S6 fallback).


### 2026-10-01 - controls/client - Classic free view decoded; W+A/W+D are curves (not diagonals)
- Problem: W+A / W+D behaved wrong (host had guessed camera-relative diagonals).
- Research: found the actual free-view implementation in the shipped UI scripts:
  CameraStatus_Animation (mainscene.lua proto 0/0) swaps Turn*<->Strafe*
  handlers; CameraStatus_Set (0/3) calls it with (mode ~= 'god camera');
  CameraCommon.lua enters 'local camera' -> free view ON normally. So the
  classical strafe handler's free-view branch (TurnLeftStart/RightStart) runs
  in normal play: A/D turn, and W+A/W+D are turns while running (curves).
- Fix: reverted the diagonal behavior; keyboard turn now rotates the view at
  the char turn rate (standing: body turns too; moving: body follows the
  rotating heading via the turn model - one driver per case); curYaw/camSys.Yaw
  wrapped each frame, fixing a +/-pi alias that flipped the turn model.
  S stays walk-pace back-pedal.
- Evidence: run reborn_20261001_085528.log (WA dist=194 curve vs 384 straight,
  dyaw=-3.75; WD mirrored; back dist=96); joystick sanity run; camera_smoke
  ALL PASS; jx3_model 10x PASS; verify/capture selftest exit 0.
- Lesson: the answer was in another shipped script (mainscene.lua), not the
  engine binary - extract the whole ui/Script set before concluding.


### 2026-10-01 - controls/client - W+A + RMB-hold interaction verified; yaw-turn-rate probe negative
- Did: added RC_DEMO_RMBWA test knob (holds W+A and feeds a simulated RMB orbit
  drag through the same queue the mouse uses) and ran it; also re-attempted the
  CharacterYawTurnSpeed (+0x58) consumer hunt: CommonNumber accessor
  (0x180338bf0 = [0x180EDDFE0]+0x24C14) xref -> 95 call sites scanned for
  +0x4c/0x50/0x54/0x58 reads; none is the yaw-turn consumer.
- Result: W+A+RMB is additive and coherent - keyboard A rotates the view, the
  RMB drag rotates it further, the body follows the camera (RMB body-turn +
  movement model); simulated run WA rmb=1 dpos=(183,194) dist=266 dyaw=-2.84
  dcam=-3.44 (same direction, no fight/flip), exit 0.
- Open: the official A/D turn rate is still not pinned; the host uses the
  registered S6 fallback (pi rad/s). number.krl ships CharacterYawTurnSpeed =
  0.007465 but its unit/consumer is unlocated; in live play the per-frame turn
  step is also delivered by the server sync byte (+0x48). Next probe: live
  debug on the real client (or the engine's input payload commit path).


### 2026-10-01 - controls/client - Keyboard turn is local; RMB owns the camera
- Problem: user corrections - the A/D turn rate is NOT server-fed (fully local
  action), and classic RMB-hold + A/D must NOT turn the camera.
- Research: parsed the sLoadNumberFromFile loader (0x180857df0) key map: the
  real offsets are walk +0x48 / run +0x4C / yawTurn +0x54 / yawReset +0x58
  (movement doc was +4 off). The camera-controller default table
  (Represent .rdata VA 0x180d09f40) holds RotationSpeed=0.00314 rad/ms =
  pi rad/s - the local keyboard/camera turn rate. The mouse-camera hold
  (CONTROL_OBJECT_STICK_CAMERA, id 7) owns the camera while RMB is down.
  Hotkey_EnableTurnLeft/Right are tutorial gates only (Teaching.lua).
- Fix: charTurnRate = camera row RotationSpeed * 1000 (pi only as fallback);
  keyboard turn (A/D, arrows) suppressed while RMB is held in classical mode.
- Evidence: run 20261001_172009 (RM BWA=1: W+A with RMB, no mouse motion ->
  dcam=0.00 dyaw=0.00 dist=385 straight); RM BWA=2 drag -> dcam=-1.08
  mouse-only; A-alone still d=3.14/s and back dist=96; all gates PASS.


### 2026-10-01 - controls/client - CORRECTION: RMB turn rule was not decoded (process error)
- Error: after the user observed "classic RMB-hold + A/D does not turn the
  camera", I coded a suppression rule and labeled it "observed game
  behaviour"/"verified", implying a client mechanism. No such mechanism was
  found: the shipped Lua has no gate (strafe handlers run unconditionally), and
  Hotkey_EnableTurnLeft/Right are used only by Teaching.lua (tutorial).
- What is decoded: turn keys are a CHARACTER control (TurnLeftStart ->
  SetControl(CONTROL_TURN_*)); free view = all camera modes except god camera;
  the keyboard turn rate is local (camera row RotationSpeed).
- What is not: the engine consumer of CONTROL_TURN while
  CONTROL_OBJECT_STICK_CAMERA is active (Represent SetControl binding + event
  adaptor; client-exe consumer not traced).
- Fix: the rule stays (it matches the user-observed behaviour) but is now
  labeled a PROVISIONAL host rule with re-open criteria in
  docs/controls/CLASSIC_CONTROLS_AUDIT.md; the proof file carries a correction
  section. All 1551 core UI scripts were extracted and searched for a gate.
- Lesson: "verified" may only describe what the evidence shows; a user
  observation is a reproduction target, not a decoded mechanism.


### 2026-10-01 - controls/client - RMB turn gate REVERTED; the op-mode script layer fully decoded
- User: the RMB-held turn suppression is an invented rule, not the client's.
  Revert and get the real behavior from the game client.
- Did (game-client scripts, game client's own PakV4 extractor):
  1. Reverted the gate (commit bb91c08: `classicalMode && rmbDown` removed;
     A/D turn exactly as without RMB).
  2. Decoded the whole operation-mode layer from the shipped UI scripts:
     - OperationModeBase.lua: FreeMoveControl builds the joystick direction
       vector (W=(0,1) S=(0,-1) A=(-1,0) D=(1,0)), RotatePlayer smooths it at
       0.3/frame, heading from the FastArcTan table (tan(i*pi/128), 64 entries),
       SetPlayerRotation -> Camera_GetRTParams/ConvertYawToDirection -> TurnTo;
       ClearMoveState disables all six camera controls via Camera_EnableControl
       and resets the direction vectors; SetOperationMode(CLASSICAL) applies
       Camera_SetResetSpeed(1.0) + UseFullAngle(false) +
       Scene_LockMouseRotation(false), joystick flips both to true.
     - hotkeys.lua: MoveForward/BackwardStart/Stop (0/65-68) and
       TurnLeft/TurnRightStart/Stop (0/71-74) have no mode branch;
       StrafeLeftStart (0/76) is the only mode-branched handler - classical:
       wrapper(CONTROL_STRAFE_*, true) then if Camera_IsInFreeView() then
       TurnLeftStart/RightStart(); joystick: ResponseWASDKey('StrafeLeft',...)
       with Camera_EnableControl fallback. ResponseWASDKey (0/46) is the
       per-action key refcount -> 8-way MOVE_* displacement dispatcher.
     - mainscene.lua: Camera_IsInFreeView (proto 0/1) returns the flag stored
       by CameraStatus_Animation (proto 0/0); CameraStatus_Set (proto 0/3)
       ends with CameraStatus_Animation(mode ~= 'god camera'), so free view is
       true in normal play and false only in the god camera; true restores the
       real Turn* handler globals, false swaps in Strafe* (god camera).
     - Scene.lua: OnSceneRButtonDown -> Camera_BeginDrag(2.0) +
       Camera_EnableControl(CONTROL_OBJECT_STICK_CAMERA, true) (LMB ->
       CONTROL_CAMERA); Scene_SetMoveControl stores the flag and routes
       non-FORWARD controls to FreeMoveControl. No RMB branch anywhere.
- Result: no script-level RMB gate exists in any of the five consumers; the
  host now matches the scripts (turn works with RMB down). The engine consumer
  of CONTROL_TURN_* vs CONTROL_OBJECT_STICK_CAMERA (JX3RepresentX64.dll commit
  0x1805df660 -> applier 0x1805df7e0, 8-type switch) stays open and is the next
  probe; host has no provisional rule on this path any more.
- Evidence: rebuilt reborn_client_control_modes.exe + camera_smoke ALL PASS;
  jx3_model 10x PASS; verify_model exit 0; capture selftest PASS; relaunched
  (pid recorded in-session). Script dumps under
  %TEMP%\opencode\modes-re\ (opbase_full, hotkeys_full, scene_full,
  camcommon_full, mainscene_full); extracted scripts in %TEMP%\opencode\ui_ex.
- Lesson: the whole control truth was in the shipped scripts, not the exe;
  decode the mode/camera state machines (mainscene CameraStatus_*) before
  judging any observed input behavior.



### 2026-10-01 - controls/client - engine dig: the C binding layer has no RMB gate either; classical WASD is engine-side
- User: "keep digging" after the RMB script sweep.
- Did: decoded the Lua->C bridge in the game client's own UI DLL and the
  remaining movement handlers:
  - control.lua (new extraction): defines the control ids 0..13
    (FORWARD 0, BACKWARD 1, TURN_LEFT 2, TURN_RIGHT 3, STRAFE_LEFT 4,
    STRAFE_RIGHT 5, CAMERA 6, OBJECT_STICK_CAMERA 7, WALK 8, JUMP 9,
    AUTO_RUN 10, FOLLOW 11, UP 12, DOWN 13) and the Ctrl_Camera* stubs.
  - hotkeys proto 61 (the unnamed wrapper used by Move*/Turn* handlers):
    CLASSICAL -> Camera_EnableControl(control, flag);
    JOYSTICK -> Scene_EnableFreeMoveControl(control, flag).
  - hotkeys proto 63 (guard of the strafe handler's free-view branch):
    returns true (and calls Camera_EnableControl) ONLY when
    IsPlayerInOBDungeon(); nil otherwise. => the "classical free view A/D =
    strafe+turn" claim from earlier today is WRONG outside OB dungeons:
    normal classical WASD is consumed by the engine, Lua handlers are
    overrides (joystick free-move, OB camera, displacement).
  - JX3UIX64.dll (SHA256-verified copy): Camera_EnableControl = Lua* binding
    at 0x1800AC1F0 (name in the luaL_Reg table at file 0x4A1F70, func
    0x1800AC1F0): args (id, bool[, self]); ids 6/7 additionally call the
    camera vtable +0x110 and check the HRESULT; otherwise a core setter
    0x18011D820(self,id,bool); returns NOTHING to Lua. Camera_BeginDrag
    0x1800ABFD0: 1-4 args, bridges the represent interface vtable +0x118
    (mode, &x, &y, opt) and pushes 2 numbers back. Camera_LockControl
    0x1800ACEE0: timed lock (used by the 轻功 path: IsCharacterMoving(
    CONTROL_BACKWARD) + skill 9007). Camera_ToggleControl = AUTO_RUN/WALK
    toggles. MoveControlStart/Stop (hotkeys 0/94-95) = Scene_SetMoveControl
    (true/false).
  - JX3RepresentX64.dll 0x1805DF7E0 (input-state applier, 8 event types):
    writes controller fields +0x7C/+0x80/+0x84/+0x88/+0x8C..+0x94/+0x98 and
    calls vtable slots +0x88/+0x98/+0xA0/+0xA8/+0xB0/+0xB8 on [obj+0xA8].
    This is the engine input->character path, no RMB condition visible at
    this level (types are command kinds, not keys).
- Result: there is no RMB turn gate in the scripts nor in the UI C bindings;
  classical A/D is engine-side, so an RMB interaction could only live deeper
  in the represent input/controller (next probe: the key->input dispatch and
  the character controller consumer of the fields above).
- Host model gap (decoded direction, not yet implemented): TURN is a
  character control; the camera should follow per CameraAdjustYawWhenMoveTurn
  (moving-only, 15 deg dead zone) and RMB drag owns the camera. The host
  currently couples A/D 1:1 into camSys.Yaw (the camera-relative run frame),
  which keeps W+A curves working but makes A/D rotate the camera even while
  RMB is held. Moving the run frame to its own yaw + enabling FollowYaw while
  moving (skip while RMB) is the next change; needs the RC_DEMO_MOVE WA/turn
  fingerprints re-baselined.
- Evidence: %TEMP%\opencode\modes-re\ui_enablecontrol.txt, ui_begindrag.txt,
  ui_lockcontrol.txt, hk_proto61.txt, hk_proto63.txt, control_full.txt,
  apply_input.txt; docs/controls/CLASSIC_CONTROLS_AUDIT.md section 5.



### 2026-10-01 - controls/client - RESTARTED DIG: game-client camera manager + command chain found
- User: restart the research, find something.
- Did (game-client build, SHA-verified copies): mapped the Lua→C binding layer
  in JX3UIX64.dll (Camera_EnableControl 0x1800AC1F0 = pure setter,
  Camera_BeginDrag 0x1800ABFD0 = represent vtable +0x118 bridge,
  Camera_LockControl = timed 轻功 lock, MouseControlMoveEnable 0x1800ACF90 =
  type-0x54 state + notify; MoveControlStart/Stop = Scene_SetMoveControl);
  the exe command forwarders (KEventCommonMgr::BeginDragCamera/EndDragCamera/
  SetCameraDragParams/ForceResetCamera/EnableControlCamera ->
  world vtable +0x3D0/+0x3D8/+0x3E8/+0x428/+0x450); the character-controller
  input applier (AjustCtrlInput -> 0x1805DF350 -> 0x1805DF7E0, 8 event types);
  and the game-client camera mouse path: MouseMove 0x180B21300 + ApplyMouse
  0x180B1F520 with the manager fields (+0x2F0 controller array stride 0x20,
  +0x90/+0x94/+0x98/+0x9C accumulators, +0x1A8 moved flag, +0x1AC/+0x1B0
  yaw/pitch source-select flags), per-mode drag speeds applied from ctx
  +0x6C/+0x70 (classic) / +0x84/+0x88 (joystick), gate words
  [0x180EDDFE0+0x25CD0]/[+0x25CF0]. Camera rows (10x0x24 at
  [0x180EDDFE0+0x262F0], loader 0x180338C10) confirm
  CameraAdjustYawWhenMoveTurn +0x14 and DisableAngle +0x18.
- Also corrected: the strafe handler's classical free-view Turn call is
  OB-dungeon-only (hotkeys proto 63 = IsPlayerInOBDungeon guard), and the
  hotkeys proto-61 wrapper routes classical -> Camera_EnableControl,
  joystick -> Scene_EnableFreeMoveControl.
- Result: the only camera-yaw writer on the input path is the mouse drag
  (ApplyMouse); TURN controls feed the character controller queue. The host's
  A/D -> camSys.Yaw coupling is the deviation; camera follow belongs to the
  cached CameraAdjustYawWhenMoveTurn row. No return of the provisional gate.
- Evidence: modes-re\gc_mousemove.txt, gc_applymouse_fn.txt,
  gc_camrow_loader.txt, gc_ctrl_apply.txt, exe_eventcommon_camera.txt,
  ui_enablecontrol.txt, ui_begindrag.txt, ui_lockcontrol.txt, hk_proto61.txt,
  hk_proto63.txt, control_full.txt; OPERATION_MODES_PLAN.md sec 7d.



### 2026-10-01 - controls/client - dig continued: full mouse/rotation pipeline + character-yaw coupling
- Kept decoding (user: until the full system). New (game-client build):
  MouseMove 0x180B21300 -> ApplyMouse 0x180B1F520 -> ClampMouse 0x180B1FE70
  (five named controllers: carrier, "camera", sprint kind 0x1B, glider,
  npc-dialog) + ApplyRotation 0x180B1FA30 (dynamic-follow; calls
  pDynamicFollowCameraController) + alternate applier 0x180B211D0; the
  engine writes the CHARACTER yaw via 0x18001F05F -> 0x180530F00
  (`[character+0x30] = yaw`) from four sites (0x180B1FCC1 ApplyRotation,
  0x180B21282 alt applier, 0x180B10115 state-step, 0x180B24EDE reset),
  conditioned on dynamic-follow/stick state ([mgr+0x1AC]/[+0x1B0], state
  [mgr+0x5C], dead-zone test), NOT on keyboard A/D.
  Also: CreateSO3Represent allocates the global 0x180EDDFE0 object (0x26470 B)
  and CreateRLLoader the RLLoader; character controller input applier
  (0x1805DF350 -> 0x1805DF7E0) applies 8 command types into +0x7C..+0x98.
- Precise remaining unknowns (3): which control id (6 vs 7) sets [mgr+0x1B0]
  / [mgr+0x1AC]; the [mgr+0x5C] state enum 1..7; the character movement
  direction (facing vs camera) in the controller update.
- Evidence: modes-re\gc_applyrotation.txt, gc_clampmouse.txt, gc_applyalt.txt,
  gc_mousemove.txt, gc_applymouse_fn.txt, gc_charyaw_thunk.txt,
  gc_rowxref.txt, gc_camrow_loader.txt, gc_ctrl_apply.txt,
  exe_eventcommon_camera.txt; OPERATION_MODES_PLAN.md sec 7d (+addendum).



### 2026-10-01 - controls/client - final decode pieces: face yaw, move-state, UpdateRotation
- Closed the three open items as far as static analysis allows:
  (a) [char+0x30] = KRLLocalCharacter face yaw (setter 0x180530F00 via
  0x18001F05F; consumer UpdateFaceFootDirection 0x180534699 -> 0x1800236D7);
  (b) [controller+0x5C] = object move-state (UpdateObjectState 0x180B24F0A;
  states 5..7 = the water/air cluster; entering/leaving resets face yaw; the
  alternate camera applier's face write is gated to 5..7);
  (c) per-frame pipeline = UpdateRotation (carrier/glider/telescope + base
  0x180B20C30 + ApplyRotation 0x180B1FA30).
  SetControlOther (0x180530E40, KRLLocalCharacter) is the controller-switch
  target-id setter, not the control-flag sink.
- Consequence: the engine rule set is complete for the host; only exact
  per-state body-carry tuning (which states carry, at what dead zone) rests
  on the states 5..7 gate + the row CameraAdjustYawWhenMoveTurn.
- Evidence: modes-re\gc_applyrotation.txt, gc_applyalt.txt, gc_clampmouse.txt,
  gc_charyaw_thunk.txt, gc_setcontrolother_thunk.txt, exe_createso3.txt;
  OPERATION_MODES_PLAN.md sec 7d addendum 3.



### 2026-10-01 - controls/client - IMPLEMENTED the decoded classical model
- User: implement the fully decoded system.
- Change (client/RebornClient.cs): classical movement now runs along the
  character facing (spawn-synced at the three camera-init sites); the turn
  block writes curYaw only (joystick keeps its historic camera coupling); the
  camera follows a moving forward character via CameraSystem.FollowYaw
  (CameraAdjustYawWhenMoveTurn row, dead zone, moving-forward + no-drag gate).
- Evidence (run reborn_20261001_214418.log, build git=2a1191e dirty=1):
  A-alone: dyaw=3.13, dpos=(0,0), camd=0.00 (camera untouched - the decoded
  rule); back-pedal: cam unchanged, dist=96; W+A/W+D: 194 u curves, camera
  follows (dcam larger than dyaw because the pre-test facing offset is
  converged by the row). Gates: smoke ALL PASS, jx3_model 10x PASS,
  verify_model exit 0, loot selftest PASS. Client relaunched (pid per session).
- Note: two intermediate behaviors were caught by the demo harness and fixed
  before landing (back-pedal must not drag the camera -> forward-only follow
  gate; the follow input needed the facing->camera-yaw conversion atan2(-cos,
  -sin), the same involution the RMB body-carry uses).



### 2026-10-01 - controls/client - CHECK: was there a recent change to classic A/D/camera?
- User: "classic does not change camera with A/D - it used to be not like this,
  feel like a recent change, check."
- Checked: installed client GameInfo.dat = 1.5.0.9975 (PreVersion 1.5.0.9702),
  paks updated the same day; official 2026-09-28 notes (1.5.0.9971) have no
  control/camera item. Local build genealogy (game 09-27, MovieEditor 09-14,
  client-MovieEditor 04-28, bundled client 03-31) all carry the same camera
  systems; but ApplyRotation differs: old 03-31 applied up to four rotation
  channels to the character on any nonzero delta; current 09-27 is
  follow-state gated and writes a single face-yaw. Character-API thunks differ.
- Conclusion: a real camera->body coupling change exists between the March and
  September builds (public notes silent), consistent with the user's feeling;
  the current decoded behavior (A/D = character turn, camera follows only while
  moving forward) is the current client truth and is what the host implements.
- Evidence: docs section 7d addendum 4; old_applyrot.txt / old_updrot.txt in
  %TEMP%\opencode\modes-re\; GameInfo.dat; jx3.xoyo.com latest notes.


### 2026-10-01 - controls/client - A/D also turn the camera 1:1 (user request)
- User: "just add AD to also turn camera as well, but at what speed?" Answer
  from client data: 1:1 with the character turn - the local camera-controller
  RotationSpeed row (0.00314 rad/ms = pi rad/s); the old ApplyRotation wrote
  the identical yaw delta to both. Implemented: curYaw += dturn; camSys.Yaw -=
  dturn, except while RMB holds the drag (the observed classic rule).
- Evidence: run reborn_20261001_220719.log - A 1 s: yaw +3.14 / cam +3.14;
  TURNRIGHT 0.6 s: +1.88 / -1.88; W+A: 2.51 / -2.51; W+D mirrored; back-pedal
  camera kept. Smoke ALL PASS. Client relaunched (pid per session).



### 2026-10-01 - controls/client - WHY W+D+RMB walks diagonally with no camera turn
- User: W+D+RMB must walk upper-right with the camera still; find the real mechanism.
- Decoded: shipped default.txt binds A/D to STRAFELEFT/STRAFERIGHT (arrows =
  TURN); hotkeys proto 76/78 make the strafe handler call TurnStart ONLY when
  Camera_IsInFreeView(), which is why A/D turn + carry the camera 1:1 without
  RMB. RMB enables CONTROL_OBJECT_STICK_CAMERA (Scene proto 31) - stick/mouse-look
  mode where the heading is mouse-owned: turn cannot act, strafe moves the
  character laterally.
- Implemented: rmbStrafe = classical && rmbDown -> A/D add lateral input and do
  not turn; camera untouched (only the drag moves it). Evidence: WA rmb=1
  dpos=(-286,-257) dist=384 dcam=0.00 (diagonal); WD no-rmb still curving.
- Smoke ALL PASS; client relaunched (pid per session).
- Note: g_Scene_bMouseMove/SetMouseMove ('CheckBox_MouseMove') is the operation
  panel's click-to-move option, NOT this switch; MouseControlMoveEnable remains
  the engine-side mouse-move option binding.



### 2026-10-01 - controls/client - forward-right animation fixed (camera frame + drag-gated carry)
- User: "the animation needs update when walking forward right".
- Root cause: the facing-frame experiment made W+D fight - RmbTurnsBody held the
  body toward the camera while RunTo pulled it to the diagonal, so the gait
  angle exceeded 45 deg and the 挪步 side-step clip played instead of the run.
  The decoded control set is CAMERA controls; the body faces the TRAVEL
  direction; the engine's camera->face write fires on mouse DELTAS (ApplyRotation),
  not on a held RMB.
- Fix: reverted the movement frame to the camera (both modes); RMB body-carry
  now requires a real mouse drag (150 ms window).
- Evidence: WA rmb=1 dpos=(-49,-380) dist=384 dyaw=-0.79 dcam=0.00 - straight
  45-deg camera-frame diagonal, body aligned to the travel (run clip); WD no-rmb
  curve unchanged; strafe/turn 1:1 preserved. Smoke ALL PASS. Relaunched.



### 2026-10-01 - controls/client - animation from the engine's input octant; LMB owns the camera
- User: LMB hold must also stop the camera turning; and stop matching animation
  from described behaviour - decode the system.
- Decoded: represent locomotion state table (RunForward/WalkForward/RunBackward/
  WalkBackward + ComputeStrafe/pnStrafeRight lateral blend): the clip family
  follows the INPUT octant (ResponseWASDKey MOVE_* names), not the facing angle.
- Fix: gait from fwdAxis/latAxis (W=run/walk, S=后退, lateral=挪步); camera
  keyboard coupling suppressed while either mouse button holds the drag.
- Evidence: run 224006 - WA rmb=1 gait=0 clip=跑动 during the diagonal; numbers
  unchanged otherwise. Smoke ALL PASS. Relaunched (pid per session).



### 2026-10-01 - controls/client - RMB+A/D: walk-tier side-step; engine move-info decoded
- User: what happens on RMB+A/D? animation not supporting.
- Decoded: GetMoveInfo (0x1805DFE90) returns forward (+0x50), strafeRight (+0x4C),
  rotationRight (+0x3C); animation parameter names pnForward/pnStrafeRight/
  pnRotationRight; locomotion states RunForward/WalkForward/RunBackward/
  WalkBackward -> lateral is a BLEND into the run state, and pure lateral is the
  walk-tier 挪步 clip. No strafe-run state exists.
- Host: pure lateral now moves at walk pace (96) so the 挪步 clip matches;
  diagonal stays run; joystick always forward-family (body faces travel).
- Evidence: strafe rmb=1 dist=96 dcam=0; WA rmb=1 dist=384 dcam=0.00; turn
  1.88/-1.88; smoke ALL PASS. Relaunched.


### 2026-10-01 - controls/client - FULL movement system decoded across all layers
- Kept decoding per user request. Mapped: script bindings -> Camera_EnableControl
  (14 ids); the character-controller API (Move/Run/Jump/SetYaw/SetPitch/SetRoll/
  ToggleCharacterControl/GetMoveInfo) with intents fForward +0x50, fStrafeRight
  +0x4C, fRotationRight +0x3C; the setters push queue commands 5/6/7 into the
  applier 0x1805DF7E0; CommitInput 0x1805E3270 posts the frame commit; the anim
  state table (RunForward/WalkForward/RunBackward/WalkBackward + jump/swim/fly)
  with pnForward/pnStrafeRight/pnRotationRight blend params (lateral = blend;
  pure lateral = walk-tier 挪步; no strafe-run state); camera pipeline and the
  LMB/RMB heading-ownership rule.
- Boundary: the exe-side per-frame control->intent loop runs behind runtime-built
  interface tables (no static names), so it is characterized behaviorally, not
  symbolically; the host implements the same model.
- Docs: OPERATION_MODES_PLAN.md section 7e (full system); evidence in
  modes-re gc_strafe/gc_runforward/gc_pnstrafe/gc_getmoveinfo_*/gc_intent_*.


### 2026-10-01 - controls/client - input/camera plumbing fully decoded (vtable/action-table/property-store)
- Closed the loop: world vtable 0x180CC21E8 (slots 3D0/3D8/3E8/428/450 =
  BeginDrag/EndDrag/SetCameraDragParams/ForceReset/EnableControlCamera
  0x1805E36F0); EnableControlCamera posts to vtbl+7D0 -> thunk -> HandleRLAction
  0x1802F54B0 -> action table 0x180E96C00 (47 handlers; 6 = control-enable,
  7 = drag-state, plus HandleCamera/HandleCharacterAnimation/HandleSprint/...);
  handlers apply (propertyId,value) fields to the property store at
  [SO3+0x25F08] (property ids 0x1E player, 5 tick, 0x1C drag->SO3+0xC,
  control ids 0..13). Consumers read the store through the dynamic property
  system - that is the data-driven boundary, not an unknown.
- Evidence: gc_handlrlaction/gc_ctrl_action6/gc_ctrl_action7/
  gc_enablecontrolonly dumps + doc 7f.


### 2026-10-01 — process — Full-system exploration rule (no spot fixes)
- Did: user rule — when part of a system behaves wrong, the system is wired wrong, not
  just the spot that shows it; the user pointing at a specific wrong place is a symptom
  location, not the fix target (patching only there = band-aid). Added to `AGENTS.md` §6:
  explore the full system (inputs → state → outputs, data read, engine calls) before
  changing anything; trace the full chain to the root cause and fix the wiring.
- Evidence: `AGENTS.md` §6 (new paragraph); this commit (local).
- Outcome: solved (rule added).


### 2026-10-01 — repo — Cleanup: remove 4 merged worktrees/branches
- Did: verified containment (`git branch --merged main`, `git merge-base --is-ancestor`
  exit 0, `main..branch` = 0, worktrees clean) then removed worktrees
  `reborn-iso-cam-wwdrag`, `reborn-iso-camera-wall-clip`, `reborn-iso-double-jump`,
  `reborn-iso-mini-sandbox` and deleted `agent/cam-wwdrag`, `agent/camera-wall-clip`,
  `agent/double-jump`, `agent/mini-sandbox`. `branch -d` refused camera-wall-clip only
  because local was 15 ahead of its lagging upstream while fully merged to `main`;
  re-verified ancestor-of-main then `-D`. Origin refs untouched; only regenerable
  ignored artifacts (`__pycache__/`, `native/obj/`) were in the removed dirs.
- Evidence: `git worktree list` now 9 (main + 8 in-flight); remaining 8 branches
  unmerged/dirty and left alone.
- Outcome: solved (not committed; main checkout stays local-only).


### 2026-10-01 — netcode — JX3 client launch/session reality check (offline client verdict)
- Did: bounded probe (Stage A static + Stage B observation) of "take the real client,
  cut connections, private server". Recovered the streaming launch contract
  (`/u:/t:`, `/c`, `/wg`), proved the normal launch is **arg-less** (Dumper `-c`
  absent; WMI cmdline scrubbed) and gated on launcher/session presence: bare and
  `/u:/t:`/`/c` launches exit in ~2 s, code 0, before any network. Observed a normal
  run: explorer -> SeasunGame.exe -> JX3ClientX64.exe (+cefrender), game endpoint
  `109.244.61.154:3724`, XGSDK/xoyo HTTP(S), loopback IPC pairs. Protocol table
  (814 SIDs + sizes) already in-repo.
- Verdict: **not a cheap pivot** — requires launcher emulation (IPC handoff), XGSDK
  auth stub, server-list interception, and 814-message protocol implementation, plus
  protection layers (Dumper64/VMProtect/TP3) and rule changes. Current engine-host
  plan stays the cheaper/cleaner path.
- Evidence: `docs/netcode/JX3_CLIENT_LAUNCH_AND_SESSION.md`,
  `proof/netcode/disasm/streaming_parsecmdline.txt` + gateway/serverlist dumps,
  `proof/netcode/JX3Browser_strings.txt`; this commit (local).
- Outcome: solved (feasibility answered; no pivot).


### 2026-10-01 — repo — Merge: battle-floating-ui research → main
- Did: merged `agent/battle-floating-ui` (battle floating UI + KGUI font/color
  scheme research) into `main` as `0c750cc` (`--no-ff`). Sole conflict
  `docs/EXPERIENCES.md` (append-tail): kept the branch's 2026-09-30 blocks then
  main's 2026-10-01 blocks — chronological, both preserved. Net diff: 146 files,
  +36,812/−13 (`proof/` 136, `docs/` 6, `tools/` 3, `.gitignore` 1);
  `ui-process-app/**` and root `AGENTS.md` byte-identical to pre-merge main.
  Registered the three research tools in the `docs/ui/README.md` tools table.
  Also caught pre-merge: `tools/prepare_ui_fonts.py` had been written UTF-16 by
  PowerShell redirection — restored as UTF-8 (`8309d3a`).
- Verified: `jx3_model` 10/10 PASS; `verify_model` self-consistent; loot
  `SELFTEST PASS`; no client/native/app code in the merge, so no rebuild/smoke.
- Evidence: merge commit `0c750cc`; `docs/ui/BATTLE_FLOATING_UI.md`,
  `docs/ui/FONT_SCHEME_SYSTEM.md`, `proof/ui/evidence/battle_hud/**`.
- Outcome: solved (merged; pushed to `origin/main` at user request).


### 2026-10-01 — netcode — Launcher handoff mechanism recovered (shared memory + XTEA)
- Did: second-pass static RE of the launcher handoff. Found it is **not** a command line:
  PID-keyed named mapping `400BBBA7-F29F-4357-9B07-%04X-D62109852BD6` (0x275C bytes) +
  mutex `56992E93-3828-415E-AB04-%04X-33107B63107D`; the client opens/creates the mapping,
  copies it, and XTEA-decrypts in place (32 rounds, key words a0b1c2d3 e4f5a6b7 c8d9eafb
  0c1d2e3f). Launcher: `XCommonX64.dll` DetachProgram -> `OSUtil::_LaunchProgram`
  (`0x1800a82f0`) = plain `CreateProcessW` with no args; the session is filled into the
  block after (PID out-param). `KGatewayClient::OnSyncLoginKey` carries
  `pcszGameServerIP` (server points the client at the game server).
- Verdict update: launcher emulation is **reproducible without modifying the client**;
  the remaining work is the 0x275C block layout + the gateway protocol (814 IDs, sizes
  extracted). Feasibility answer changed from "blocked" to "possible, still large".
- Evidence: `docs/netcode/JX3_CLIENT_LAUNCH_AND_SESSION.md` §5-6;
  `proof/netcode/disasm/{xcommon_detach,detach_events,client_mappings,launchblock_callers}.txt`;
  `proof/netcode/launcher_*.txt`, `SeasunGame_launcher_strings.txt`; this commit (local).
- Outcome: solved (mechanism recovered; next gate = block capture).


### 2026-10-01 — netcode — Launch block format decoded (header + slots + custom TEA); writer still open
- Did: static decode of the launch block: 0x275C = 16-byte header + 20 slots × 0x1F7
  (payload 0x1E7 + 16-byte key); header +2 = GetTickCount()/1000 (freshness ≤ 10 s),
  +8 must be non-zero, +0xc = entry count (client zeroes it after read); first 8 bytes
  decrypted by a custom 16-round TEA variant (sum 0xC6EF3720, delta 0x61C88647, key
  a0b1c2d3 e4f5a6b7 c8d9eafb 0c1d2e3f). Parser is polled; client gives up ~2.4 s.
- Launcher-emulator mechanics proven (suspended start + pre-created PID-keyed mapping +
  write + resume); synthesized header-only block (both key-index variants) still exits
  at 2.4 s — the writer/encrypt is unidentified (no encrypt direction in client/logic
  DLL; launcher binaries lack the block GUID).
- Evidence: docs/netcode/JX3_CLIENT_LAUNCH_AND_SESSION.md §7;
  proof/netcode/disasm/{launchblock_parser,launchblock_decrypt,parser_caller,logic_reader_callers,logic_mapping_refs}.txt;
  this commit (local).
- Outcome: partial (format decoded; writer open).


### 2026-10-01 - client - in-world target indicator (KRLTarget assets, no hand-drawn art)
- Did: researched the marker drawn around a selected target (not the HUD):
  the represent layer's KRLTarget. Config is
  `represent/common/force_relation_care.txt` (ForceRelationCareTable): one row
  per relation with SFXFile (relation-coloured selection ring,
  `选择特效aXXX_hd.pss`), SFXEn (`J_角色箭头面向.pss` facing cone),
  SFXScale 1.8 and the relation colours; KRLTarget::Init/LoadFile/Show drive it
  and EnableBraceSfx attaches the CommonCursorEffect ring (`鼠标移动.Sfx`);
  the UI Lua (GlobalEventHandler.lua) calls TargetSelection_ShowSFX(relation,
  flag). Implemented in client/RebornClient.cs: on selection change spawn the
  client's own ring + facing cone at the target via AddDummyModel, remove on
  deselect; RC_TARGET_HUD default off (the request is the in-world marker).
- Evidence: run reborn_20261001_173107.log (Tab selects 初级试炼木桩 at 3s,
  indicator h>0), proof/controls/target_indicator_client_20261001.txt
  (before/after PNGs + 4x4 RGB; red px 10->128, yellow 150->7852).
- Dead end (documented): the native KRLTarget attach needs the represent game
  world; in the MovieEditor-hosted engine the represent singleton is null
  (JX3RepresentX64.dll RVA 0xF06A50 = 0), so AttachSceneObject/Show cannot run;
  the cursor-ring brace is a compiled .Sfx (AddDummyModel rejects it). Also the
  game-client JX3RepresentX64.dll is a different build than MovieEditor's -
  RVAs must come from the loaded copy. Re-open criteria in
  docs/controls/JX3_TARGET_SELECTION.md §10.
- Outcome: solved for the ring + facing cone; the brace/arrow composition waits
  on represent-world host support.


### 2026-10-01 - cleanup - remove target_dummy_sandbox browse app
- Did: deleted the separate browse/display app (	arget_dummy_sandbox/, added in
  30e458e) per request - the client sandbox (one 试炼木桩 + in-world indicator in
  client/RebornClient.cs) is the vehicle. Updated references: root AGENTS area
  map, docs/pvp/README tools line, docs/netcode/README tools table,
  docs/pvp/TARGET_DUMMY_RESEARCH.md (scope/§4/§5/Reproduce), and the target UI
  paint-error path in client/Targeting.cs (now bin64\reborn_out). Historical
  proof files (proof/pvp/target_dummy_sandbox_*) and the older EXPERIENCES entry
  stay untouched.
- Evidence: git rm of 4 files; client rebuild exit 0; grep shows no live
  references outside historical proof/EXPERIENCES.
- Outcome: branch carries only the client sandbox with the target dummy.


### 2026-10-01 - client - left click anywhere deselects the target
- Did: added the client's own click semantics to the sandbox. Proof chain found
  first: LMB is bound to `CAMERAORSELECTORMOVE` (=1, "rotate camera or select
  under cursor"; ui_hotkey_default.txt:35-36 + docs/movement/JX3_COLLISION_SYSTEM.md
  section 16.1), bindings.ini:309-313 maps down/up to
  CameraOrSelectOrMoveStart/Stop(0) (Lua handlers Ctrl_CameraOrSelectOrMoveStart/Stop
  in ui/script/control.lua + hotkeys.lua), and the clear path is the same setter
  with TARGET.NO_TARGET=1 (KTarget::SetTarget 0x140241C00; client calls
  SetTarget(player, NO_TARGET, 0) at 0x140318C33). Implemented in
  client/RebornClient.cs: a left click without drag runs the cursor pick; a hit
  selects, an empty pick clears (`click: deselect`), removing the in-world
  indicator. Added RC_CLICK_AT=ms,x,y so the click path is scriptable.
- Evidence: run reborn_20261001_203137.log (Tab at 3s selects; click at 5s ->
  "click: deselect (nothing under cursor)" -> "target indicator removed"),
  proof/controls/target_deselect_click_20261001.txt (+ before/after PNGs and
  4x4 RGB; warm pixels 463 -> 154, frame diff 6469 px at the dummy).
- Open: the empty-pick branch of CameraOrSelectOrMoveStop is MED - the handler
  name is runtime-registered (not a binary string); re-open criteria in
  docs/controls/JX3_TARGET_SELECTION.md section 6.
- Outcome: solved for the sandbox; the selection model follows the client.


### 2026-10-01 - client - click pick hit volume (off-target click deselects)
- Did: the deselect click kept the target when clicking NEAR the dummy because
  TargetSelector.Pick used a 12-degree cone around the entity direction. Replaced
  it with a ray-vs-vertical-body-cylinder test (radius 90 u, height 220 u,
  nearest ray hit; client/Targeting.cs) - any ray missing every body is an empty
  pick -> deselect. Extended RC_CLICK_AT to a list (ms,x,y;ms,x,y) for scripted
  click sequences.
- Evidence: run reborn_20261001_203922.log - Tab at 3s selects, click 620,430
  (~80 px off the body) -> "click: deselect" + indicator removed, click 700,440
  (on the body) -> "target=... (click pick)" + indicator back;
  proof/controls/target_deselect_hit_test_20261001.txt (warm px 13393 -> 7904 ->
  13392, 3 PNGs). Gates: jx3_model 10x PASS, gravity, loot selftest PASS.
- Open: the body cylinder is the host's stand-in for the engine's model pick
  (KCharacter::OnPickPrepare); re-open when the host exposes a real model pick
  (docs/controls/JX3_TARGET_SELECTION.md section 6).
- Outcome: solved; the user rule "not clicking on target = deselect" holds.


### 2026-10-01 — repo — target-dummy merged into main (fe57fd5)
- Did: merged `agent/sandbox-target-dummy` (16 commits: client 试炼木桩 spawn
  rid=35901 + Tab/click targeting + in-world ring/arrow indicator; target HUD
  compiled but off by default; extractors + docs/proof) into main; resolved the
  single EXPERIENCES conflict by keeping main's file and appending the branch's
  9 missing `###` blocks (168 insertions, 0 deletions). Main-side
  `client/RebornClient.cs` was untouched since the branch base, so the merge is
  a pure union; nothing else was merged.
- Evidence: merge `fe57fd5`; canonical build `reborn_client.exe`
  (git=fe57fd5, built 21:03:51); `camera_smoke.exe` ALL PASS; `jx3_model.py`
  10x PASS; `verify_model.py` ok; loot `capture.py selftest` PASS; scripted
  engine run (`RC_TITLE=Target-Dummy RC_TAB_AT=3000
  RC_CLICK_AT=5000,620,430;7000,700,440 RC_SHOTS=4500,6500,8500
  RC_AUTORUN=10000`) log `reborn_out/reborn_20261001_211147.log`: dummy
  handle=1992345400 at (23334,740,24624), Tab pick MidAxis/400u, click 620,430
  -> deselect + indicator removed, click 700,440 -> reselect + indicator
  respawned, DONE; shot fingerprints match the branch proof's per-region RGB
  (rc_00/01/02; only 1-unit lighting noise from a concurrent session).
- Note: console `KGLOG_ASSERT_EXIT(pRetMinDistanceRet) KG3D_Scene::RayIntersection`
  spam during the run is absent from all logs and correlates with the
  pre-existing foliage-collision ray calls (colCalls ~1300 per 2 s), not the
  merged feature; a separate `reborn_client_colltest4.exe` session ran
  concurrently in its own namespace during the run.
- Outcome: solved (local main, not pushed); branch + worktree left intact.


### 2026-10-01 — repo — sandbox client rebuilt from merged main
- Did: rebuilt the mini-sandbox feature client `reborn_client_mini.exe`
  (`RC_CLIENT_EXE=reborn_client_mini.exe`, title sandbox-mini) from merged main
  so the sandbox carries the target-dummy work; canonical `reborn_client.exe`
  was already rebuilt from the merge commit `fe57fd5`.
- Evidence: `build_info_reborn_client_mini.exe.txt` git=`e30edd8` built
  21:23:42; sandbox run log `reborn_out/reborn_20261001_212351.log` on the
  cropped map (`RC_MAP=...龙门寻宝_s.jsonmap`): `target dummy rid=35901
  handle=5733249480 at (23334,740,24624)`, Tab pick `zone=MidAxis dist=400u`,
  indicator spawned (`选择特效a002_hd.pss`), DONE; exit 0.
- Outcome: solved (local, not pushed).


### 2026-10-01 — repo — target-dummy worktree/branch removed after merge
- Did: removed the merged `reborn-iso-sandbox-target-dummy` worktree and deleted
  `agent/sandbox-target-dummy` (`branch -d`, tip 43a3398 = merge parent, 0 ahead
  of main, worktree clean). Desktop worktree folders: 8 -> 7; git worktrees 8 -> 7.
- Evidence: `git worktree list` (main + 6 in-flight), branch list has no
  `agent/sandbox-target-dummy`.
- Outcome: solved (local, not pushed).


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

### 2026-10-01 - Physician route 2: the engine's own PhysX cook+mesh bridge works in-host

- Goal: stop guessing ABI; the engine binaries carry the exact answer. The
  PxCooking/PxPhysics vtables were dumped live (`RC_PX_VT=1`) and then explained
  by disassembly of the MovieEditor-copy DLLs, not by SDK headers.
- What cracked it: the cooking desc layout is NOT the public 3.3.4 header order.
  The validator (0x7E40) + cooker (0x7970) field reads gave the real one:
  +0x00 pointsStride, +0x08 points, +0x10 nbVertices, +0x18 trianglesStride,
  +0x20 triangles, +0x28 nbTriangles, +0x30 u16 flags (bit1=16-bit),
  +0x48 convexEdgeThreshold (MUST be 0.001f or the helper errors).
- Managed stream ABI: PxOutputStream/PxInputStream = one method + dtor; a
  GetFunctionPointerForDelegate thunk works for both directions.
- Results (all in-host, test exe): `PxCooking vt[4]` + out-stream -> 620 B
  NXS1/MESH cooked mesh; `PxPhysics vt[8]` + in-stream -> PxTriangleMesh*.
- Sweep safety: vt[2] hangs (spin/lock), vt[1] is a global decref; identify
  entries by behavior (consumed bytes + return), log-before-call so a hang's
  culprit is visible in the last log line.
- Next: PxShape/PxRigidStatic/PxScene + queries, then A/B vs our solver.

### 2026-10-01 - PhysX shape slot: actor vtable decoded; identify objects by getConcreteTypeName

- Continue of the PhysX-direct build: after cook+createTriangleMesh, the actor
  side is mapped. PxRigidStatic vtable (RVA 0x273890, 16 entries): [1] =
  getConcreteTypeName, [6] = attachShape (crashes on non-shape), [3] = the
  destructor (calling it destroys the actor - it returns `this`), [5] hangs.
- Trick worth keeping: any PhysX object can be identified at runtime by calling
  its vt[1]() -> const char*; used to catch that physics vt[12] is
  createHeightField, not createShape.
- Empirically found: physics vt[6]=rigid actor creator, vt[8]=createTriangleMesh,
  vt[12]=createHeightField, vt[24]=getMaterials. createShape not yet found;
  sweep safety map recorded in proof/movement/phys_engine_vtables.txt.
- Incident + rule: rewrote PhysicsProbe.cs once with PowerShell
  Get-Content|Set-Content - it mangled the UTF-8 Chinese mesh paths. Restored
  line-wise from HEAD. NEVER touch source files with PS text cmdlets (AGENTS
  already says this; this is the second reminder).
- Next: sweep physics vt[23,25..44] for createShape, or trace the engine's own
  shape factory object used at PhysicsEngineX64 ~0x144CF.

### 2026-10-02 - controls/client - re-based host on the decoded control table
- User: implement the change plan (decode-faithful).
- Changed client/RebornClient.cs: added ControlId/Ctrl (ids 0..13); per-frame
  intents forward/strafeRight/rotationRight; movement from the intents;
  removed the A/D->camera 1:1 coupling (the registered deviation); RMB stick
  camera switches the strafe-bound A/D to lateral; keyboard no longer writes
  the camera anywhere; camera = mouse drag + CameraAdjustYawWhenMoveTurn row.
- Evidence: turn/A-alone camd=0.00; RMB strafe 96 camd=0.00; WA rmb=1 317
  camd=0.00; WD row curve. Smoke ALL PASS. Relaunched.


### 2026-10-02 - controls - official docs check (2 months): no control change; A/D habit is a choice
- Researched official notes Aug-Oct 2026: 9912/9920/9948/9-16/9971 - content
  and fixes only, no control/camera/turn changes (9975 installed = hotfix).
- Official help (zl/new-crjh): LMB = view only, RMB = view + character turn;
  WASD move, arrows move, Q/E strafe, both buttons/NumLock autorun. Older
  official posts document the A/D habit choice (turn vs strafe) and joystick
  mode's camera-direction movement.
- Consequence: the host's A/D=turn is the TURN habit, not the default; model
  the habit as an option. No game-side change to blame for the mismatches.


### 2026-10-02 - controls/client - complete real control scheme: A/D habit + official bindings
- Per user: remove the prebuilt control approximations and re-add the real
  scheme. Implemented: ControlId/Ctrl table; **A/D habit** (RC_ADHABIT=
  strafe|turn; default "strafe" = shipped default.txt); arrows turn; both
  mouse buttons = auto-forward (official help); RMB stick camera = mouse owns
  the heading (turn-habit A/D become strafe there); row follow only on the
  rotation intent (move+turn) - this fixed a strafe-diagonal feedback spiral.
- Evidence: strafe habit A-alone side-step 96 dist camd=0; WA/WD straight
  315/385 dcam=0.00; turn habit A-alone turn 3.14 camd=0; WA curve 375 dcam=1.
  Smoke ALL PASS. Default relaunched (strafe habit).


### 2026-10-02 - controls/client - classic default = TURN habit + moveYaw control frame
- User: A/D must not move in classic. Default habit flipped to "turn": A/D
  rotate in place; lateral only with the RMB stick camera. Added the moveYaw
  movement/control frame (mouse drag sets it; turn keys rotate it; the camera
  follows it via the row) so W+A/D curves instead of the camera-frame travel
  canceling the turn.
- Evidence: A-alone yaw 3.13 dpos=(0,0) camd=0; WA curve dist=193 dyaw=3.93;
  WD dist=192 dyaw=-4.04; back 96. Smoke ALL PASS. Relaunched (turn default).


### 2026-10-02 - controls/client - turn keys rotate the camera (classic turn)
- User: "AD should turn camera". Turn-habit A/D and arrows now rotate the view
  exactly like a mouse drag (right = yaw decrease) and feed the engine through
  the orbit-pixel path; the character turns to the camera direction (RMB-carry
  relation). Strafe-habit A/D keep side-stepping with no camera rotation.
- Evidence: turn habit A-alone yaw -3.14 camd -3.15 dpos=(0,0); TURNRIGHT 0.6s
  camd=-1.89; strafe habit A/D dpos=(-96,0) camd=0, TURNRIGHT camd=-1.89,
  WA/WD straight 384 dcam=0.00. Smoke ALL PASS. Relaunched (turn default).


### 2026-10-02 - controls - P0: full control-modes decode traceability matrix
- Started the full CLASSICAL+JOYSTICK decode (user: "full is EVERYTHING",
  animations included). P0 = `docs/controls/CONTROL_MODES_TRACEABILITY.md`:
  layer ledger, movement/camera/actions/mode-routing matrices, a 20-row
  animation matrix, data ledger, gaps G1-G10 + A1-A20 with owner phases
  (P1 Lua ... P9 publish), verification plan. Registered in controls README.
- No behavior claims changed; host observations marked as verification only.


### 2026-10-02 - controls - P1: Lua handler decode (joystick vector + mode routing)
- New `tools/controls/lua_index.py` (batch proto index); ran it over the full
  extracted UI script set (1549 files, 56,688 protos) -> proof/controls/
  lua_index_all.txt + lua_index_control.txt (committed recon).
- Decoded bodies (annex `CONTROL_MODES_LUA_ANNEX.md`, dumps committed):
  hotkeys 0/61 mode wrapper (CLASSICAL= Camera_EnableControl, else
  Scene_EnableFreeMoveControl); hotkeys 0/46 ResponseWASDKey = joystick
  analog vector (Turn+Strafe share axes, 8-way MOVE_* + MOVE_STOP on overflow,
  ResponseDisplacementHotkey routing, double-tap Forward -> StartSprint with
  tower/bird/horse guards); OperationModeBase 0/5 mode apply (persisted key
  StorageServer('CurrentOperationMode'), UseFullAngle/LockMouseRotation per
  mode, Camera_SetResetSpeed(1.0) classical, IsMobileKungfu gate open);
  0/19 toggle (SetCameraMode(nCameraModeIn<Mode>,true); mobile-kungfu forces
  JOYSTICK + NEVER_FOLLOW); UISetting_Operation_Switch 0/12 setter.
- Traceability statuses updated (R4/R6/R8 done, G9 Lua side done).


### 2026-10-02 - controls - P1 movement/drag handlers + OB wrapper correction
- Decoded (annex A6/A7, dumps committed): hotkeys 0/63 OB wrapper (returns
  true only in OB), 0/65 MoveForwardStart (HoldW + displacement skill 3799 +
  wrapper + ResponseWASDKey double-tap), 0/76 StrafeLeftStart (classical:
  OB short-circuit else free-view TurnLeftStart; joystick: ResponseWASDKey +
  Camera_EnableControl fallback), Scene 0/27/0/31 (desk drag: LMB->CONTROL_CAMERA,
  RMB->CONTROL_OBJECT_STICK_CAMERA, morph bypass), 0/36 EndDrag(1.0/2.0),
  0/61 Scene_LockMouseRotation rlcmd, 0/25 classical both-buttons autorun.
- **Corrected the earlier inverted note**: normal classical A/D DOES call
  TurnLeftStart via Camera_IsInFreeView; OB is the short-circuit. Superseded
  notes added in CLASSIC_CONTROLS_AUDIT and OPERATION_MODES_PLAN; matrix
  M4/C1/C2 updated.
- Lesson: bytecode TEST/JMP convention (JMP taken iff bool(R[A]) == C) matters;
  re-derive from a known body before trusting a chain.


### 2026-10-02 - controls - VM jump rule confirmed; mode branch mapping reopened
- Confirmed Lua 5.1 conditional semantics from lua.org lvm.c (OP_EQ: jump iff
  comparison == A; OP_TEST: jump iff l_isfalse != C) after two self-doctored
  readings collided. Applying it to hotkeys 0/76 INVERTS the mode->block
  mapping recorded in both older docs: mode==CLASSICAL -> ResponseWASDKey +
  Camera_EnableControl(STRAFE) fallback; else -> OB wrapper + free-view
  TurnLeftStart. Observable per-mode behavior now explicitly OPEN until the
  C-binding constants (P2) and engine consumer (P3/P4) are decoded; docs
  updated to stop asserting either direction.
- Resolved CLOSURE upvalues from the chunk pseudo-instructions: 0/76 [0]=0/63,
  [1]=false constant (skill branch dead), [2]=0/75 CanStrafeMove (fly-jump/
  sprint/horse); 0/65 [0]=false constant, [1]=wrapper 0/61, [2]=0/62
  ClientControlEnabled. Dumps committed (0/62, 0/64, 0/75).
- Lesson: never infer branch polarity from "obvious" intent; cite the VM rule
  first, then re-check every earlier decode that assumed it.


### 2026-10-02 - controls - P2: mode constants are Lua; mapping resolved
- Found `CLASSICAL_MODE = 0` / `JOYSTICK_MODE = 1` defined in the
  OperationModeBase.lua chunk (pc0-3) and `GetOperationMode` as a Lua closure
  over the shared current-mode upvalue (initial CLASSICAL; SetOperationMode
  writes it). No C binding involved for the mode itself.
- Consequence (with the confirmed VM jump rule): CLASSICAL A/D (STRAFE-bound)
  = ResponseWASDKey + Camera_EnableControl(CONTROL_STRAFE_*) = strafe habit;
  JOYSTICK A/D = OB wrapper + free-view TurnLeftStart = turn. The host's
  default "turn" corresponds to JOYSTICK mode, not CLASSICAL - corrected in
  CLASSIC_CONTROLS_AUDIT + OPERATION_MODES_PLAN + traceability (M4/G10).
- `Camera_IsClientControlDisabled` (mainscene 0/2) is a getter; CameraStatus_Set
  (0/3) writes the flag as (params.dis_ctrl == 1) and sets the free-view flag
  to (mode ~= 'god camera'). Full camera-param table inventory recorded (A8)
  for P4.
- P2 gap G11: C Lua-binding names (Scene_EnableFreeMoveControl etc.) are not
  plaintext in any bin64 module -> hash/registration table to be located;
  `KCharacter::LuaHoldW` and `KEventCommonMgr::EnableControlCamera` ARE
  present and give direct anchors for those.


### 2026-10-02 - controls - P1 complete: full movement handler roster
- Decoded the remaining handlers (annex A6.5/A7.6): forward/back start+stop
  (HoldW, OB short-circuit on backward, double-tap, CheckEndSprint), turn
  start+stop (enable flag + ResponseWASDKey + mode wrapper, no mode branch),
  strafe stop mirroring start; Scene 0/24 autorun clear, 0/26 control-setter
  helper (FORWARD routed to the both-buttons function), 0/6 IsInStickCamera =
  Hotkey_IsRMouseEnabled and bRDown, 0/94/0/95 Scene_SetMoveControl, and the
  mouse-move setting persistence key StorageServer('SceneMouseMove') (0/50/0/51).
- All skill-displacement branches are dead in this build (captured const false);
  enable flags all captured true. P1 Lua layer is complete for the movement /
  camera / mode handler set.


### 2026-10-02 - controls - P3 static: thunk + script-API + apply chain
- Built a call-ref scanner for JX3RepresentX64.dll: every control function is
  reached via a single low-region jmp thunk (CommitInput 0x1805E3270 via
  0x18000E9B2, GetMoveInfo via 0x18001AC8F, Move/Jump/clear, camera drag
  functions, etc.).
- Script API wrapper cluster 0x180AD35A0..0x180AD3A40 with resolved assert
  strings (GetMoveInfo L194, Move L130, Jump) forwarding to the engine thunks:
  the C-side character API layer.
- Input apply chain: KEventCommonMgr -> KGameWorldHandler::AjustCtrlInput
  (0x1805E1ED0) -> controller apply 0x1805DF350 -> queued-input applier
  0x1805DF7E0 (callers 0x1805DF459/0x1805DF6F9 iterate container [obj+8],
  node tick [node+0x10] compare = tick-ordered queue drain). CommitInput
  callers are vtable/runtime (boundary remains dynamic).
- Doc: docs/controls/CONTROL_MODES_P3_STATIC.md; proof/controls/p3/*.


### 2026-10-02 - controls - P5 start: animation selection algorithm
- Disassembled KRLRushState::UpdateMoveAnimation (0x1804C0700): clip selection is
  data-driven per state via a param struct from sub_1801252B: two speed
  thresholds ([param+0x50] low, [param+0x68] high) vs character speed
  [this+0xD0] select default/low/high tiers; tiers set playback speed
  [this+0x34] and transition clips (set A +0x34/+0x58/+0x70, set B
  +0x44/+0x60/+0x78, combat/stance selector [this+0x114]); anim id [this+0x30]
  (moving id [param+0x4C]); blend call 0x180003D50; two slots +0x1B8/+0x1C0.
- Dumps: proof/controls/p5/*.txt; doc docs/controls/CONTROL_MODES_P5_ANIM.md.
  Next: decode sub_1801252B (state->param table) and UpdateDirection.


### 2026-10-02 - controls - P5 lookup chain
- sub_1801252B thunks to 0x18085CE60: state->param lookup via the singleton
  [0x180EDDFE0]: index = [this+0xB0] (or 999 sentinel when the [this+0x74]
  flag is set and [base+0x26466]!=0), lookup sub_180005204(base+0x1A0, mode,
  index). Sibling 0x18085CEB0 maps raw index through a 10-entry {id,limit}
  table ([+4] count, index%count when flagged). Next: sub_180005204 + the
  singleton table to enumerate state param structs.


### 2026-10-02 - controls - P5 table layout
- Resolved sub_180005204 -> 0x180812D70 (key fetch with (mode,index) fallbacks)
  and 0x180812E00 (binary search over 0x54-byte entries in the vector at
  [container+0x1E2B8], count +0x1E2C0). The 84-byte entry IS the param struct
  UpdateMoveAnimation consumes. Table loaded via KTableList::LoadBinTextTab
  (0x180CD02A0); filename not an in-binary string -> entry enumeration goes via
  the runtime probe / table extractor. 0x26466 sentinel flag set from a config
  option query at 0x1803185E0.


### 2026-10-02 - controls - P4 static: field map + host build diff
- Host build diff (MovieEditor JX3RepresentX64 09-14): all anchors present
  (EnableControlCameraOnly ref 0x1802EE4FA, CameraAdjustYawWhenMoveTurn
  0x1803420EC, UpdateMoveAnimation 0x1804D04D3, GetMoveInfo 0x1805F2A49,
  ApplyRotation 0x180B373B5) - probe must use host RVAs, not game RVAs.
- Field writers (game client): +0x1B0 written only as zero in
  ResetCharacterCamera; +0x1AC written by the dynamic-follow state machine
  (0x180B1A6ED=1, 0x180B1B3CB var, 0x180B1D31F=0, 0x180B1D3EB var);
  ApplyMouse/UpdateRotation only read +0x1AC/+0x1B0. G2/G3 static conclusion:
  controls drive the state machine, not the flags.
- Probe plan documented (shim module-base/read exports + RC_PROBE_CONTROL
  telemetry + scripted runs).


### 2026-10-02 - controls - P4 probe run: host has no JX3 game-world layer
- Implemented RC_PROBE_CONTROL=1 (client, read-only: module base via .NET
  Process.Modules, Marshal.Copy reads; host RVA 0xF06A50 for the game-world
  singleton). Build exit 0, smoke ALL PASS.
- Result: the host loads the exact scanned MovieEditor JX3RepresentX64.dll but
  [base+0xF06A50] is null for the whole run - MovieEditor never instantiates
  the JX3 game-world (KTableList/animation params/character controller).
  Therefore runtime animation-table capture in the host is impossible; P5 must
  enumerate from the shipped BinText tables + static decode. The F1 catalog
  columns were documented; the 84-byte locomotion table (2 thresholds, 3 clip
  pairs) is a separate BinText table still to be found in the paks.


### 2026-10-02 - controls - P5 data source status
- No loose locomotion table on disk (depth-3 scan): only MovieEditor editor
  templates and the extracted samples catalogs (F1 per-kind rows + serial
  phased sets). The 84-byte locomotion param entry is a separate BinText table
  loaded by KTableList::LoadBinTextTab. Next: xref the loader string in the
  game client to find caller-supplied table names; enumerate the PakV4 index
  (Data\filepath.ini variants not present); hpkg extractor if packed.


### 2026-10-02 - controls - default-binding coverage count
- Fresh audit (proof/controls/hotkey_coverage.txt): 286 bound commands, 18
  handled (movement 9 + sit/sheath 2 + camera 7 incl. mouse drag), 268
  unhandled grouped: action bars 106, UI panels 60, other combat/state 82,
  targeting 10, rogue/BR 6, minigame 4. Movement+camera control plane is
  complete; the rest need their own systems (action bar/targeting/UI/stance).
  CLASSIC_CONTROLS_AUDIT.md section 5b updated (old '176 unhandled' stale).


### 2026-10-02 - controls - apply input core + full control plan
- FULL_CONTROL_PLAN.md: all 286 shipped bindings grouped (movement/camera 18
  done; input core P0; targeting P1; action bars P2; UI panels P3; stance/talent/
  NPC/capture P4; contexts P5) with dependencies and verification.
- Applied P0: HotkeyTable now context-aware (Match filters by the active
  context; MINIGAME_JUMP no longer aliases MOVEFORWARD), loads per-role
  hotkey_newlast.txt overrides (decoded format name/context/index/key; empty
  key = unbound; works with an override-only dir), and logs overrides count.
  Client: RC_HOTKEY_CTX sets the context, periodic log now carries ctx=.
- Smoke extended (camera_smoke now compiles HotkeyTable.cs + embeds the two
  hotkey resources): 4 new checks - W normal/MINIGAME_JUMP separation, W
  minigame context, A normal, override file applies over embedded defaults
  (2 overrides, W unbound from slot 1). Build exit 0; ALL PASS.


### 2026-10-02 - controls - classic A/D default = strafe (client truth)
- User: "AD is not supposed to be turning by default". Per the P2 decode
  (CLASSICAL=0: A/D strafe via ResponseWASDKey + CONTROL_STRAFE; joystick=turn)
  the host default flipped from turn to strafe; RC_ADHABIT=turn keeps the turn
  habit. Verified: A-alone d=0.00 side-step dist=96 camera kept; WA/WD straight
  385/384 dcam=0.00; arrows still turn (camd=-1.89). Smoke ALL PASS.


### 2026-10-02 - controls - joystick baseline assessed
- Kept version committed (HEAD 8b4e7d7); joystick evidence run
  reborn_20261002_164838.log: per-mode apply ok, auto-face ok (WA/WD straight,
  dcam=0); found two host bugs vs client truth: (1) joystick A/D run laterally
  instead of turning in place (client 0/76 joystick branch = free-view
  TurnLeftStart); (2) turn keys rotate the camera in joystick (decode: keyboard
  never writes the camera; mouse owns it). Patch list recorded in
  proof/controls/control_modes_run.txt.


### 2026-10-02 - controls - joystick J1+J2 (A/D turn, character-only turns)
- adStrafe now `classicalMode && (...)`: joystick A/D = turn-in-place per the
  decoded 0/76 joystick branch; classical keeps strafe default / RC_ADHABIT=turn
  option. Joystick turn keys rotate only the character (no camera write).
- Verified: joystick A-alone d=3.14 dpos=(0,0) cam kept; TURNRIGHT camd=0.00;
  classical regression unchanged (A strafe 96, arrows camd=-1.89). Smoke PASS.


### 2026-10-02 - controls - joystick corrected model (turn controls = lateral axis)
- Decoded OperationModeBase FreeMoveControl (0/16): the joystick builds a
  discrete vector - TURN_LEFT/RIGHT add nX, FORWARD/BACKWARD add nY - with
  SetPlayerRotation/TurnTo auto-facing and the camera mouse-only. So joystick
  A/D (via the strafe handler -> TurnLeftStart -> wrapper) and arrows are
  lateral movement with auto-face; my J1 (turn-in-place) was wrong and was
  corrected in the host (joystick turn controls fold into the strafe axis).
- Verified run 170201: A-alone dist=318 auto-face, camera untouched; WA/WD
  diagonals 348/383 camd=0.00. One non-reproducible camera-drift run (165931)
  noted as a transient.


### 2026-10-02 - controls - J3 sprint input ported
- Decoded the sprint chain (hotkeys 0/36/164/165/166): double-tap window is
  exactly 250 ms; StartSprint casts 6754 (non-GAI_BANG) + player:Sprint(true);
  EndSprint = SetSprintTopPoint + Sprint(false). Host ports the input side
  (fresh-press detection on the six movement commands, decoded window,
  Start/End logs, sprint= telemetry); engine Sprint state left open.
- Verified via RC_SPRINT_TEST scripted double-tap (150 ms gap) -> StartSprint/
  EndSprint logged; build exit 0, smoke ALL PASS.


### 2026-10-02 - controls - J4 decode: RotatePlayer joystick math + constants
- RotatePlayer (0/14) per frame in joystick: smooths the vector (0.3/call)
  toward the target built by FreeMoveControl, computes the byte heading via
  FastArcTan(|nX|/|nY|) + quadrant fixups (0..255), calls
  Camera_SetResetSpeed(dir * (bSprintFlag and 3.0 or 1.0) * 0.00125) and
  SetPlayerRotation(dir) -> TurnTo. Constants extracted from the chunk locals.
- Key insight: Camera_SetResetSpeed is the JOYSTICK CAMERA FOLLOW RATE written
  per frame from the direction (not a drag-release setting) - resolves the G5
  ambiguity; per-mode value at switch is only the base.
- Host port of the rate pending (no invented rate); decode committed in annex
  A11.


### 2026-10-02 - controls - joystick status close
- J1/J2/J3/J4 decode+input landed; follow-mode setter confirmed (clamp 0..3,
  +0x80 classic / +0x98 joystick); the remaining consumers (reset-speed
  application, follow [0..3], UseFullAngle) sit behind the engine property
  system / hashed bindings and are documented OPEN rather than approximated
  (OPERATION_MODES_PLAN 7h).


### 2026-10-02 - controls - spring integrator candidate; joystick consumers stay open
- 0x180B11E40 (camera region) is a spring/interpolation step over
  fields +0x74/+0x78/+0x7C (time) / +0x80..+0x88 (velocity) / +0x8C..+0x94
  (target) - offsets overlap the camera-node per-mode block but are reused as
  time/velocity, so it does NOT prove the per-mode springResetSpeed wiring.
- Follow-mode reads: none direct in the camera region (property-system
  boundary). UseFullAngle: no plaintext strings anywhere (hash-registered).
- Conclusion recorded in P4 doc 2c + OPERATION_MODES_PLAN 7h: these consumers
  need runtime tracing of the game-world layer, which the host cannot provide.


### 2026-10-02 - controls - J5 solved: camera follow mode pipe + gating
- Found the follow-mode enum (NEVER=0/AUTO=1/ALWAYS=2, enum_ui.lua) and the
  full native pipe: SetCameraMode -> UI_Camera_SetParams_S -> Camera_SetFollowMode
  (gc 0x1802EB1C0) -> node setter 0x180ACE3F0. Host now gates the camera follow
  by the per-mode value (custom.dat), joystick following the travel direction
  per the decoded RotatePlayer rate; RC_FOLLOW_MODE override for tests.
- Verified: joystick ALWAYS follows (A dcam 1.06, TURNRIGHT 1.57, WD -1.05);
  classical default 0 unchanged; smoke ALL PASS. Interpretation note + re-open
  criteria in annex A12.


### 2026-10-02 - controls - J6 pipes located (reset speed = property 0x17; UseFullAngle = logic flag)
- Camera_SetResetSpeed/SetSpringResetSpeed are plaintext bindings in JX3UIX64;
  engine side KGameWorldHandler::SetCameraResetSpeed (gc 0x1805FA810) builds an
  action event {0x1E playerId, value, 0x17 property, float} and posts via
  world vtbl +0x7D0 (HandleRLAction). Consumer = the action handler for that
  event kind (bounded next decode).
- UseFullAngle is NOT Represent: ProcessFullAnglePlayer in JX3ClientX64.exe
  sets player +0x20038/+0x2003C (air/轻功 full-angle camera) - logic layer,
  outside ground joystick scope.
- Proof: proof/controls/p4/gc_SetCameraResetSpeed.txt, exe_ProcessFullAnglePlayer.txt,
  exe_DoUseFullAngle.txt.


### 2026-10-02 - controls - reset-speed consumer chain decoded to one hop
- Chain: Camera_SetResetSpeed -> KGameWorldHandler::SetCameraResetSpeed
  (0x1805FA810; 4-field event {playerId, selector, prop 0x17, float}) ->
  HandleRLAction (0x1802F54B0; lookup 0x1805D9A70 over table 0x180E96C00) ->
  action table[0x1E] 0x1802EA9B0 -> selector table 0x180E92360 -> sel[0]
  0x1802EE050 (4-field apply) / sel[1] 0x1802EE250. One hop left (what sel[0]
  does with the value) before porting the A11 joystick rate.


### 2026-10-02 - controls - reset-speed lands in the camera-node command slot
- sel[0] (0x1802EE050) normalizes the event and calls applier 0x180AE75C0:
  walks the entity to the type-0xD camera node + type-0xE companion, then
  node[+0x30]=float value, node[+0x2C]=prop, node[+0x24]=1 (command slot);
  companion[+0x2C]=1; optional +0x64/+0x68; follow-up 0x180019A60.
- So the joystick/per-mode reset speed is queued in the camera node command
  slot and consumed by the camera update. Last hunt: the slot reader (narrow).
- Proof: proof/controls/p4/reset_speed_applier.txt, reset_speed_sel0_tail.txt.


### 2026-10-02 - controls - J8 solved (mode persistence) + J7 N/A; all joystick items closed but one
- J8: StorageServer('CurrentOperationMode') maps to per-role userpreferences.jx3dat
  (GBK text map, CurrentOperationMode={0|1}); host reads it read-only via
  RC_USER_PREFS and applies at startup. Verified: real role file parsed
  (CurrentOperationMode=0 classical), smoke ALL PASS.
- J7: no morph/OB camera system in the host -> N/A, documented (not stubbed).
- Residual: the camera-node command-slot reader (+0x24/+0x2C/+0x30) for the
  exact A11 rate integration; the slot applier is decoded, the reader needs a
  proper function-start finder.


### 2026-10-02 - controls - mode HUD, joystick default, "/" switch, instant joystick turn
- Top-left label shows CONTROL: JOYSTICK|CLASSICAL [/] switch; default mode is
  now joystick; "/" (and F7) switch the mode; the old "/" run-toggle removed.
- Joystick facing is instant per the client design: SetPlayerRotation ->
  KCharacter::TurnTo (0x14031E7C0) writes the target heading [char+0x44]
  directly; the display blends via tabCGAni KeepTurningFrame/TurningEpsilon
  (no ground turn clip ships). Host sets the facing directly in joystick; the
  classical RunTo path (rate + >112.5deg penalty) is unchanged.
- Verified: joystick A-alone snap yaw -1.57 dist=320; WA/WD 383 both (no
  penalty); classical regression identical; smoke ALL PASS.


### 2026-10-02 - controls - full audit (movement+camera+joystick)
- Gates: jx3_model 10 PASS, gravity exit 0, loot SELFTEST PASS, smoke ALL PASS,
  build exit 0 warning-free; parity/rules/server gates N/A in this worktree
  (no netcode/ tree).
- Scripted runs on the audited build: default joystick; joystick instant turn
  (A yaw snap -1.57 dist=320; WA/WD 383 no penalty); classical regression
  unchanged; sprint input Start/End; follow ALWAYS dcam 1.06/1.57/-1.05;
  mode persistence read. Hygiene: removed unused vars, fixed stale / text.
- Residuals: A11 rate integration (node slot reader), engine Sprint state,
  locomotion BinText filename, G11 registry, classical turn-key camera
  deviation (host-requested), camera-drift flake watch. Report:
  docs/controls/CONTROL_AUDIT_20261002.md.


### 2026-10-02 - controls - full plan audit + follow-target bug fix
- F1 BUG fixed: the classical AUTO/ALWAYS follow used the control frame
  (moveYaw) as target -> silent no-op; now the travel heading for both modes.
  Before 184457 (classical fm=2 camd=0.00 everywhere); after 184651 (strafe
  camera follows +1.05, WA 0.53, WD -1.06); fm=0 regression 184824 unchanged;
  joystick fm=2 unchanged; smoke ALL PASS.
- F2 deviations registered: classical turn-key camera coupling -> camera
  HOST_DEVIATIONS A13 (re-open criteria); host UX additions (HUD/default//`/`/
  instant turn) labelled host in OPERATION_MODES_PLAN 7h; RC_* test knobs noted.
- F3 no other band-aids found (sprint engine state open+logged, A11 residual,
  animation table data hunt, authored clips only, client-derived TurnTo).
- F4 plan phases: P0 done; P1-P5 out of the camera/movement scope.
- Audit section appended to docs/controls/CONTROL_AUDIT_20261002.md.

### 2026-10-02 — client — M1.7 HUD overlay + five-minute solo proof (M1 exit)
- Did: replaced the hidden WinForms HUD label with `client/HudOverlay.cs` (top-level
  layered click-through window owned by the host form; "I" key toggles the info box).
  Built `reborn_client_m1-final.exe`; drove a 5-minute solo session with posted keys
  (`tools/proof/run_solo5min.ps1`: I, W-hold run, Space jumps, 1 skills) and external
  window captures (`tools/proof/capture_window.ps1`, DPI-aware) every 30 s.
- Evidence: `docs/engine_host/M1_SOLO5_PROOF.md` — 10 captures with distinct sha256,
  HUD text in every scene capture; log: 316 s, path span 15555 u (243 chi), run 320 u/s,
  18 jump-clip switches, 5 skill casts, fps 131-287, no crash. Overlay window live:
  LAYERED|TRANSPARENT|TOOLWINDOW|NOACTIVATE 1510x310 over the viewport.
- Lesson: posted keys need `SetForegroundWindow` first (same as captures); `G` is a
  game-side hotkey with no client handler - use W-hold for movement; the shared
  `reborn_out` log dir mixes concurrent clients, select the log by `build=` fingerprint.
- Outcome: M1 exit criteria met (local branch `agent/m1-final`, not pushed).


### 2026-10-02 — merge — agent/m1-final -> main (M1.7 HUD overlay + solo-5 proof)
- Did: merged `agent/m1-final` (`--no-ff`, no conflicts) after a clean preflight
  (`merge-tree` exit 0). Rebuilt canonical `reborn_client.exe` + `reborn_client_mini.exe`;
  gates: `camera_smoke.exe` ALL PASS, `verify_model.py` pass, loot `selftest` PASS,
  `jx3_model.py` 10x PASS. Relaunch check: canonical client logs `build=reborn_client.exe`
  and shows the HUD overlay window (collapsed 44x44, LAYERED|TRANSPARENT) over the viewport.
- Evidence: merge `665e01c`; branch commits `97f0ce6` + `623ffe3`; proof
  `docs/engine_host/M1_SOLO5_PROOF.md`.
- Outcome: M1 exit criteria met on main (local, not pushed).

### 2026-10-02 - PhysX midphase A/B: the solver matches the engine at the field spots

- Completed the PhysX-direct path without a scene: PhysX3Common_x64.dll exports
  PxMeshQuery static functions by mangled name (987 exports); getTriangle +
  findOverlapTriangleMesh + sweep. Geometry layout corrected from getTriangle
  disasm: mesh ptr at +0x28, scale at +0x04 (live null-deref taught this).
- Full-map cook (2.26M tris -> 105 MB cooked stream, ~9 s); capsule self-test at
  triangle-0 centroid overlaps; vertical scans at the field spots on the right
  map (龙门寻宝_夜晚 - identified by closest-vertex scan across the 5 bins).
- Result: engine vs our solver agree at spawn/wall/pile (tool
  tools/collision/spot_ab.py). The engine-side reference is now reproducible.
- Note: cooked streams can exceed 64 MB; the probe uses a 256 MB buffer.

### 2026-10-02 - Phase-1 grid A/B: two wrong assumptions caught, then 0.15% match

- The structure bin is NOT world-baked: v2 = local meshes + per-instance l2w.
  The earlier "A/B positive" compared raw (untransformed) vertices on both
  sides - invalid; corrected by baking l2w (and the oflags rule) before cooking.
- PhysX 3.3 capsules run along the LOCAL X axis: an identity pose is a
  horizontal capsule. Corrected pose quaternion (z=w=sqrt(1/2)) gave exact
  per-point matches (25/25), then 1313/1325 region lattice (0.15%, both
  tangential-contact threshold artifacts, no phasing class).
- Rule of thumb reinforced: compare only after both sides use identical
  transforms, identical instance rules, and identical shape conventions.

### 2026-10-02 - Phase-1 reaches 0.00% (7991 poses); sweep ABI parked

- Dense grid (pitch 100) over the play region: 0/7991 mismatch after the
  world-bake + capsule-axis corrections. The coarse pitch-250 result (0.15%)
  was two tangential-contact threshold artifacts, not reproduced dense.
- PxMeshQuery::sweep direct call: the demangled 11-arg signature crashes in
  this build's frame layout (zero-triangle + valid-cachedIndex variants both
  crash). Parked with the diagnostic path; copy the engine's own caller next.
- Lesson: for an exported C++ static with a long mixed int/float arg list,
  the mangling is necessary but not sufficient - the binary ABI (stack frame)
  must be confirmed from a real caller when the first call crashes.

### 2026-10-02 - Sweep ABI: the crash was a POINTER arg (hitFlags), semantics still open

- Correct frame accounting (arg5 starts at callee rsp+0xC0) showed PxHitFlags is
  a POINTER in this build; passing a valid buffer made the sweep execute.
  Prove regressions: the two prior crashes were null derefs of cachedIndex and
  of the flags pointer passed as a value.
- The return bool/fields do not yet match the public 3.3.4 semantics (true with
  zero triangles; unfamiliar hit field offsets; long sweeps hang). Unverified -
  next is copying the engine's own caller frame.

### 2026-10-02 - Sweep cracked end-to-end: find the engine's own caller

- The decisive move was scanning the sibling DLLs' IMPORT tables for the export
  (PhysX3CharacterKinematic imports PxMeshQuery::sweep) and reading its call
  site: frame + hit struct layout + distance semantics all came out of the
  caller in one look (flags arg is a POINTER in this build; distance at hit+0x34).
- First validated data point matches our solver (initial-overlap -> distance 0).
- Long sweeps (600/3000u) do not return - respect the short-move boundary.

### 2026-10-02 - Re-audit against the plan + Phase 3 integration decision

- User call-out: offering (a)/(b)/(c) at the end of the sweep work contradicted
  the agreed phase plan. Corrected: re-audited and continued on the plan.
- Phase 1 DONE (0/7991 dense; coarse pair triaged). Phase 2 CLOSED with the
  engine-internal-state boundary for the sweep hit path (intent covered by the
  per-pose equivalence + solver A/Bs). Phase 3 recorded in
  COLLISION_SYSTEM_COMPARISON.md 9: solver stays runtime, engine PhysX is the
  re-runnable calibration gate; re-open criteria stated. Phases 4-5 pending.
- Audit also fixed stale Reproduce text (22/22 -> 33/33).

### 2026-10-02 - Phase-4 triage: audit residual is 1 real pass-through, 5 fine

- Reproduced the day-map audit (3.83% at stride 64) and triaged 6 play-area
  faces against the engine: all faces exist (engine overlaps along the paths);
  global Resolve blocks 5/6; one (mesh jz_lmxb...台001) passes through at a
  constant height while a static query at a 3.2u-higher pose contacts it.
- Kept the item OPEN rather than claiming closure: recorded the exact
  wallcheck/tinfo reproductions; the next probe is the per-instance triangle
  grid vs the resolve at that exact height.
- New tools: `wallcheck` (global-resolve path test), RC_PX_POSES (arbitrary
  pose queries) - both reused for future triage.

### 2026-10-02 - Face-2 triage correction: static contact OK, Resolve path at fault

- First read suggested a triangle-grid miss; the correct-bin tinfo disproved it
  (depth 13-17 at every path pose). The wallcheck still passes through => the
  bug is in the Resolve path (prime suspect: the thin-wall normal flip from
  21f1f39). Do not patch before instrumenting per-substep contacts.

### 2026-10-02 - The face-2 pass-through was a stale-build artifact; sample is clean

- Rebuilt and re-ran: the reported full pass-through at face 2 side=1 blocks
  (advanced 40/54, contacts every substep). Lesson repeated from earlier
  sessions: verify a surprising result on a fresh build before recording it as
  a bug; the per-substep debug (RC_WALL_DBG) made the true behavior visible.
- Sampled residual status: all 6 play-area faces gameplay-correct (blocked or
  stepped within the 70 budget). Phase 4 closed on the sample.

### 2026-10-02 - Phase-5 preflight green (merge pending explicit go)

- Feature build `reborn_client_collision.exe` rebuilt clean from the branch
  (exit=0; warnings only), collision_selftest 33/33, jx3_model 10/10, gravity
  PASS, loot SELFTEST PASS. Shared shim untouched (build script does not touch
  it). Merge to main is the only remaining step and needs the user's explicit
  go (locked repo rule).

### 2026-10-02 - Prop step-onto-top (can't-walk-over fix) + branch spawn

- User report on the branch build: sprinting in the house, low props (inst
  529/701/997: oy=30/116/6) ejected the player sideways ("propfix push") where
  the CCT should step onto a grounded-low obstacle. SolidPropPush picked the
  min-penetration axis and never used the step rule.
- Fix: SolidPropPush takes (grounded, stepHeight); while grounded, a prop whose
  top is within the budget is stepped onto (py = it.maxY) before the min-axis
  eject. Selftest prop_solid_step_low (34/34); the tall-prop eject tests
  unchanged.
- Branch default spawn set to (18991, 962, 33853) per user request (was
  23334,761,24224). Client rebuilt from the branch: pid 31940 "JX3 [collision]".

### 2026-10-02 - Invisible AABB blocks: props back to the engine rule (mesh triangles)

- User report: blocked in the house where NOTHING is visible (inst=748
  wj_erg柜子001, world AABB 655x486). Verified: the AABB is not bloated
  (stored == mesh-derived) but the nearest mesh VERTEX to the blocked corner
  is 394 u away - the furniture mesh is non-rectangular and the AABB proxy
  filled its empty corners (registered deviation #10).
- Fix: InstanceContact no longer routes propSolid to AabbContact - static
  .mesh objects collide as their TRIANGLES (the engine rule, P1 research);
  the AABB shover (SolidPropPush) defaults off (RC_PROP_SOLID=1 restores).
  Sceneinfo check: the object really is at (20329,923,36603) - our placement
  was right; only the proxy shape was wrong.
- Verified: the blocked pose (20001,982,36335) now reports only a 0.23 u tree
  trunk graze; the cabinet's real geometry still blocks; selftest 34/34.
  Trade-off (engine-faithful): the log pile's mesh gaps are penetrable again
  like the game's own cooked mesh.

### 2026-10-02 - Creep guard ported to the branch (sprint-through-wall fix)

- User report: on the branch client they could sprint INTO a building/display
  case mesh (tui at 19928,921,36797: capsule depth 17 inside inst=135 展柜 and
  inst=365 building). The creep guard (excess-only CCT slide) existed only on
  the merged main (tag postmerge-collision-fixes-20261002); the branch never
  had it, and the branch client already feeds (mvx, mvz) to Resolve.
- Ported: FoliageCollision.Resolve guard (cancel only the advance beyond the
  push capacity) + MoveResolved passes the substep delta; selftest
  fast_thin_face_no_creep (35/35 on the branch). The client's two Resolve call
  sites already pass the motion hints.
- Verified: creep wallcheck blocks at 4.7 u; selftest 35/35; client rebuilt
  (pid 2924 -> rebuilt).

### 2026-10-02 - Step budget = the engine's CCT value (50 u), not the host 64

- User: "i can walk go up this pile of thing, how can this happen". Client
  data answer: the pile mesh (wj_木堆001) is a staircase of 8-15 u ledges
  (levels 828,836,844,...) - every ledge is inside the engine CCT stepOffset
  (0.5 m = 50 u, recovered from the PxControllerDesc ctor dump), so the
  engine's own controller climbs it. The old AABB proxy that "fixed" it was a
  host invention (deviation #10), now removed.
- Deviation 4f re-open criterion met (the ctor dump: stepOffset 0.5 m,
  slopeLimit 0.707, contactOffset 0.1): the client step budget is now 50 u
  (was the host 64; RC_STEP_HEIGHT overrides). Note: a ~51 u house-floor step
  will now block unless the gameplay step turns out larger in the server
  movement - flagged for the field.

### 2026-10-02 - Post-version audit: conflicting rules vs the engine (pile climb root)

User asked for a full audit of conflicting (invented + real) rules after the pile
walk-up. Findings and fixes (all engine-grounded, branch only, no merge):

1. **Step budget**: host 64 u vs the engine PxControllerDesc ctor value
   stepOffset 0.5 m = 50 u (ctor dump pxcontrollerdesc_ctor.txt). Now 50.
2. **Step sequence**: our step = contact + up-sweep only; the engine CCT does
   up-sweep -> forward sweep by the move at the raised height -> down-sweep
   landing (highest surface within stepOffset, terrain included). Implemented.
3. **slopeLimit 0.707 (cos 45 deg)** was ignored: the landing and the ground
   support accepted ny > 0.55. Now 0.707 enforced - steep surfaces (the log
   pile's curved tops: 51% of its up faces are steeper than 45 deg) never
   carry the player. THE PILE CLIMB ROOT CAUSE: a host threshold let the
   capsule stand/step on the logs.
4. **lowTop fallback (census #4)** removed - the engine steps onto the actual
   contact surface, not the lowest top of a contact cluster.
- Verified: selftest 36/36 (new step_over_low_steep_obstacle; thin-plate and
  plank tests updated to the engine semantics: low obstacles are stepped over
  when the forward landing is walkable); pile wallcheck (grounded, +300):
  blocked at the base (y 801-814, no climb); wall-ledge field case still
  blocks cleanly. Client rebuilt pid 34176.
- Client data checks: wj_木堆001 pak probe = only the render .mesh (no
  CollisionMesh/proxymesh sibling) so the engine cooks the render mesh; the
  climb came from our rules, not the geometry.

### 2026-10-02 - Audit correction: step budget stays 64 u (PhysX ctor default is not the gameplay value)

Second audit pass on the step rule conflict, after the ctor dump was read as
"stepOffset 0.5 m = 50 u":

- The PhysX `PxControllerDesc` ctor defaults (0.5/0.1/0.707) are the **PhysX
  library defaults**, statically linked into PhysicsEngineX64.dll; a call-site
  scan of that DLL shows the game code there never calls the desc ctor or
  setToDefault - the desc construction, if any, is elsewhere (G-1 open).
- The docs already state the gameplay body is a kinematic capsule +
  SIMWorld/KCharacter solver, and the client's own prediction has **no
  capsule-vs-mesh blocking at all** (wall blocking is server-authoritative,
  CLIENT_COLLISION_IMPROVEMENT_PLAN 8.3) - so no client-side step constant
  exists; the host step is a server proxy.
- The game-side movement constant is 64 u = 1 尺 (`ProcessVerticalMove`
  0x14031A25E ground/landing tolerance) and the 51 u house-floor field case
  needs 64.
- Corrected: client step budget back to 64 u (RC_STEP_HEIGHT overrides),
  MoveResolved default 70 -> 64, probe wallcheck debug path 70 -> 64. The
  structural audit fixes stay: engine step sequence (up -> forward -> down
  sweep), slopeLimit 0.707 on the step landing + ground support (the pile
  climb root), lowTop heuristic removed.
- Verified: selftest 36/36; pile wallcheck (grounded, +300) still blocked at
  the base with 64; wall-ledge field case still blocks cleanly.


### 2026-10-02 - Merge finalize: collision-improvement into main (pre-M2 base)

User direction: park M2 (networking) on a side branch and merge the collision
work into main without M2 entanglement.

- M2 preserved: branch `m2-networking` (5d729b2, full M2 state; `agent/m2-model`
  also holds it). main reset to the pre-M2 tip `26d9e44`.
- Merge: `agent/collision-improvement` -> main (merge/collision-into-main).
  Conflicts (5): client/RebornClient.cs, client/build_client.cmd,
  docs/EXPERIENCES.md, docs/camera/PENETRATION_PLAN.md, docs/movement/README.md.
- Resolution: main's client (M1.7 HUD, targeting, camera, double jump, operation
  modes, JumpTable) + the branch's audited collision delivery:
  - 15 Hz integer tick + render interpolation (P2-T1/T3), 90/300 u/s speeds
    (6/20 u per tick; replaces the old 16 fps 96/320 conversion);
  - CCT-only collision calls (stepHeight 64, mvx/mvz for the creep guard), no
    caller-side SupportHeight shortcut, standable-support raise, airborne rise
    resolve, sampler holes (groundOk), substep cap < radius;
  - step sequence + slopeLimit 0.707 + lowTop removal (the audit), props as
    mesh triangles, wall-ledge/thin-wall fixes;
  - COPY LOG restored as the CopyLogOverlay widget (tag recipe), PhysicsProbe
    (RC_PHYS_PROBE) + build wiring, camera anchor-Y easing (B14) merged into
    main's camera block.
- FoliageCollision.cs / TerrainSampler.cs merged clean (branch superset; no
  ICollision line on the pre-M2 base - the interface returns with M2).
- Gates: collision selftest 36/36 PASS; gravity model PASS; loot selftest PASS;
  jx3_model PASS; camera smoke ALL PASS.
- M2 note: when `m2-networking` is re-merged, the client netcode integration
  (NetClient/SendMoveInput/SendJump/remote interpolation/reconciliation) must be
  ported into the tick-based movement, and the server GameMovement walk/run
  constants reconciled to 90/300 (15 Hz) - the work documented in the
  merge-dryrun/collision branch (5d729b2-based integrated result).


### 2026-10-03 - Sandbox: collision bins were the legacy fallback (fixed)

- Did: while updating the sandbox after the merge, found `reborn_client_mini.exe`
  (map `龙门寻宝_s`) falling back to the LEGACY generic
  `collision_data\structure_collision.bin` (MD5 1A49A6... vs the real map's
  0A636D...). The client derives the bin from the map name, so the sandbox never
  had the real map's collision. Copied `龙门寻宝_structure_collision.bin` (+
  .cflags/.meshes.txt/.oflags) and `龙门寻宝_foliage_collision.bin` to
  `龙门寻宝_s_*` (untracked bin64 data).
- Verified: sandbox log shows `FoliageCollision: instances=5107 meshes=685
  foliage=龙门寻宝_s_foliage_collision.bin structures=龙门寻宝_s_structure_collision.bin`,
  `TerrainSampler regions=1x1`, clean run `reborn_20261003_044206.log`.

### 2026-10-03 - Camera: anchor to the interpolated render height (jump judder)

- Did (user report: sandbox-mini camera shaking, during a jump): the merged
  client anchors the camera Y on the RAW 15 Hz tick py (B14 easing only covers
  grounded >5 u snaps; airborne passes raw). Reproduced numerically on the
  sandbox jump probe (RC_DEMO_JUMP=1 + RC_CAM_YDBG=1, `reborn_20261003_180743.log`):
  ydbg rawstep=41/35/30/24... with sm=rawstep - the camera followed the tick
  staircase while the model glided on the interpolated rpy.
- Fix: `client/RebornClient.cs` camera anchor now uses the interpolated render
  height: `ax2 = rpx, ay2 = camYFollow ? camYSmooth : (rpy + 90.0), az2 = rpz`
  (the P2-T1/T3 design, EXPERIENCES 2026-09-30); B14 easing default OFF
  (`RC_CAM_YFOLLOW=0`; =1 restores it for A/B). ydbg now also logs `anchorstep`.
- Verified: same jump run after the fix (`reborn_20261003_181024.log`): raw
  steps still 41/35 u, anchorstep ~2-3 u per render frame (smooth, no 15 Hz
  staircase); collision selftest 36/36; camera smoke ALL PASS.

### 2026-10-03 - Merge main into agent/move-controls (tick model + HudOverlay)

- Did: merged `main` @ 765c357 (15 Hz integer movement, collision system,
  targeting, HudOverlay, camera anchor) into the controls branch; 11
  `client/RebornClient.cs` hunks resolved by porting the branch control
  semantics into main's tick loop (per-tick classical RunTo / joystick instant
  facing, gait octant, integral step + capsule substeps), plus the
  `docs/EXPERIENCES.md` union. Merge commit b872d79.
- Post-merge fixes: removed main's per-tick `curYaw = atan2(mvx,mvz)` snap,
  turn-key block `dt` -> `pdt`, mode label moved into `HudOverlay.SetModeText`.
- Lesson (tooling): the hunk side-extraction dropped one closing brace (main's
  jump probe) - after scripted conflict resolution always brace-balance-scan
  before building (`depth != 0` catches it instantly).
- Lesson (proof): layered HUD windows are invisible to `PrintWindow` (flag 2)
  and to `CopyFromScreen` under DPI virtualization; added `RC_HUD_DUMP=<png>`
  to save the rendered overlay buffer for a numeric fingerprint.
- Verified: build exit=0; camera_smoke ALL PASS; jx3_model/gravity/loot gates
  PASS; scripted classical (`203918`) and joystick (`205138`, git=b872d79)
  demo runs match pre-merge fingerprints; HUD label buffers 318x33/327x33 with
  651/675 mode-brush pixels (proof/controls/hud_mode_label_*_20261003.png).

### 2026-10-03 - Fix A/D strafe clip flicker (merge regression)

- Symptom (user report: "pressing A/D plays wrong animation"): the locomotion
  clip alternated run<->strafe every ~65 ms while pure-lateral input was held.
- Root cause: the merge resolution computed the gait octant INSIDE the 15 Hz
  tick loop while `int gait = 0` stayed per render frame - non-tick frames
  (most frames at 200+ fps) reset gait to 0 -> run clip; tick frames set
  gait 1/2 -> strafe clip. Pre-merge branch computed gait once per frame, so
  the merge introduced it.
- Fix: `client/RebornClient.cs` computes gait per render frame next to
  `moving`; the in-tick-loop assignment is removed.
- Numeric fingerprint: clip transitions in the 1.2 s strafe window
  (RC_DEMO_MOVE classical) 32 -> 3 (`reborn_20261003_203918.log` ->
  `reborn_20261003_210457.log`); W+A window stable in both (2) - only pure
  A/D was affected. Lesson: per-frame state read by the render path must not
  be reset inside the 15 Hz tick loop.

### 2026-10-03 - Joystick mouse: drag-only camera, visible cursor (decode fix)

- User report: joystick mode hides the cursor (no hover); in the game the
  cursor is a normal visible cursor and the camera turns only while LMB/RMB
  are held.
- Decode: `Scene_LockMouseRotation` (joystick) -> engine handler `0x180b00950`
  sets/clears LockInputControl flag bit 0x10 (`flags |= 0x10` / `&= ~0x10`) -
  it is not an always-rotate and does not hide the cursor; cursor visibility is
  the separate script API `0x1802f7aa0` (`[KRL+0x25bb8]`, ShowCursor restore in
  the window-message handler). Scene.lua begins camera drag only on button-down
  (both modes).
- Fix: removed `MouseRotatesWithoutButtons`/`KeepsCursorLocked` and the
  joystick always-rotate branch; mouse rotation is drag-only in both modes and
  the cursor lock exists only while dragging. Smoke gating checks updated;
  joystick demo fingerprints unchanged (`reborn_20261003_214620.log`).
- Lesson: a console command name is not a behavior spec - decode the handler
  before mapping it onto host input (the branch's J2 over-interpretation).

### 2026-10-03 - UI: one Esc information panel (info + mode + COPY LOG)

- Request: merge the three on-screen panels (I info box, joystick mode label,
  COPY LOG button) into a single information panel opened with Esc; nothing is
  shown while it is closed.
- Did: `client/HudOverlay.cs` rewritten - hidden by default, Esc toggles; the
  panel shows the mode line, the run info text and a clickable COPY LOG row;
  the overlay is no longer click-through (it exists only while open, so it
  never blocks input when closed). `CopyLogOverlay` removed; the copy action
  is wired via `hud.OnCopyLog`. `I` stays an alias; Esc keeps unlock + target
  clear. Added `RC_HUD_OPEN=1` (start open, test) alongside `RC_HUD_DUMP`.
- Verified: build exit=0; camera_smoke ALL PASS; panel buffer fingerprint
  633x222 RGBA with 621 yellow (mode), 1817 white (info), 335 light-blue
  (COPY LOG) px (proof/controls/hud_info_panel_open_20261003.png); a closed
  run writes no dump (nothing shown).
- Esc toggles open/close: key-repeat guard (`escDown`, one toggle per press);
  verified by posting WM_KEYDOWN/WM_KEYUP VK_ESCAPE to our own client (no
  global input injection - drive_client.ps1 was NOT used): panel window
  visible=True 633x222 after the first Esc and visible=False after the second.
- Follow-up ("not every Esc will close"): the form KeyPreview misses keys when
  the engine's native child window holds focus. Replaced the form handlers
  with an `IMessageFilter` (`EscKeyFilter`) that sees WM_KEYDOWN for every
  window in the process, consumes Esc and ignores auto-repeat (lParam bit 30).
  Verified by posting Esc to the engine child window itself. Added a COPY POS
  button (copies the live "pos X,Y,Z" line) next to COPY LOG; click verified
  by posting mouse messages at the button rect.

### 2026-10-03 - Merge agent/move-controls into main (controls finalize)

- Merged the controls branch into main: `aad94d8` (`--no-ff`). Preflight
  `git merge-tree` exit=0 (the branch already contained main 765c357; no
  conflicts - the earlier main->branch merge was verified in `b872d79`).
- Contents: decoded CLASSICAL+JOYSTICK operation modes (hotkey-table dispatch,
  mode switch + persistence, sprint input, joystick vector/instant facing,
  follow gating), 15 Hz tick integration with per-frame gait (A/D strafe clip
  fix), drag-only mouse with visible cursor (Scene_LockMouseRotation decode
  correction), Esc information panel (info + mode + COPY LOG/POS, IMessageFilter
  Esc), HUD mode label, decode docs/proof/tools.
- Gates from main after the merge: canonical build exit=0 + `camera_smoke` ALL
  PASS; feature `reborn_client_control_modes.exe` build exit=0 + smoke ALL
  PASS; gravity `verify_model` PASS; loot selftest PASS; `jx3_model` 10 PASS.
- Clients relaunched: canonical `reborn_client.exe` (title Main-Full-Client)
  and sandbox `reborn_client_control_modes.exe` (title sandbox-control_modes).
- Local only: nothing pushed to origin.

### 2026-10-04 - Movement/terrain - region streaming: bounded LRU cache (1.3)

- Did: measured terrain region loads at a map-region border on 龙门寻宝 with a new
  test-only cross-back harness (`RC_CROSS_BACK`, reverses at t=10.5 s) and per-load
  telemetry. The single region slot ping-ponged between adjacent regions - 16 loads
  in a 17 s crossing run (cap=1 control: 85) - because the player ground query, the
  camera ray march and the camera ground clamp sample opposite sides of the border
  every frame. Replaced the slot with a bounded LRU (`RC_TERR_CACHE`, default 4;
  each entry = height grid + hole mask). The cap=1 control exposed a self-eviction
  bug (fresh entry added with LastUse=0 evicted itself -> AV 0xC0000005); fixed by
  marking it MRU before the eviction pass.
- Evidence: `docs/movement/TERRAIN_REGION_STREAMING.md`; logs
  `reborn_20261004_223128.log` (16 loads), `223600.log` (cap=1, 85 loads),
  `223711.log` (cap=4, 3 loads, 0 during crossings); commits `ce9d1b6`, `c32f095`.
- Outcome: solved - 0.2-5.0 ms per load (no frame-scale hitch); the defect was
  frequency. Hole (0,0) A/B re-PASS 234/234 on the cache build; collision selftest
  36/36; gravity `verify_model` PASS. Region (1,1) `.hlb` has 0 hole cells; broader
  hole A/B needs more `.hlb` extraction.

### 2026-10-04 - Crash triage - 海岛绝境 void-spawn AV (pre-existing, new site)

**Problem:** collecting a second hole A/B region on 海岛绝境, spawn over the hole
region at altitude (`RC_SPAWN=-25600,1000,-25600 RC_SPAWN_Y=1`) crashed ~3 s in.
**Tried:** `tools/camera/minidump_exc.py` on the dump: AV 0xC0000005 at
`KG3DEngineDX11EX64.dll+0x12282B3` (engine render stack), shim `d6=seed` loaded - a
new site, not the documented D6 `+0x11D03B6`. The same scenario on clean main
(`reborn_client.exe`, `aad94d8`) crashes at the identical address; solid-ground
spawn on the same map exits clean (`DONE`, `reborn_20261004_225645.log`).
**Outcome:** pre-existing on main; not caused by the terrain region cache (no host
frame from the sampler in the crash stack).
**Why:** scenario-specific - spawn above a hole region at altitude / bottomless fall.
Dumps: `%LOCALAPPDATA%\CrashDumps\reborn_client_terrainstream.exe.52348.dmp` and
`reborn_client.exe.37100.dmp`; crash log `reborn_20261004_224645.log`.
**Re-open criteria:** a void-fall session - repro at y=1000 over a hole; compare the
spawn-at-altitude path with the historical walk-in hole-fall run
(`proof/collision/client_holes/hole_fall_*`, 2026-09-29); dump the engine side only
if the cache/host frames appear.
**Links:** `docs/movement/TERRAIN_REGION_STREAMING.md` §5; crash site
`KG3DEngineDX11EX64+0x12282B3`.

### 2026-10-04 - Movement/terrain - hole A/B extended to all hole-bearing maps

- Did: extracted the shipped hole-mask tree (54 `.hlb`, 4256 hole cells across
  海岛绝境/白龙绝境/天原绝境; 龙门寻宝 and 夜晚 ship none) and A/B'd one region per
  map on the cache build (engine `RC_HOLE_DUMP` vs the extracted `.hlb`, spawn on a
  hole-free region-center cell). Added `check_hole_mask.py --scan` for the inventory.
- Evidence: PASS 海岛绝境 (0,0) 234/234; 白龙绝境 (3,4) 96/96; 天原绝境 (4,3)
  1400/1400 — all masks identical (32768 bytes); dumps in
  `%TEMP%\opencode\{bailong,tianyuan}_ab\`; logs `reborn_20261004_23*`.
- Outcome: solved - the decode rule (four-corner + Z flip) holds on both origins
  (0,0 4x4 and -102400,-102400 8x8). Corrects the earlier note that only two
  `.hlb` exist (that was just the extracted set).

### 2026-10-04 - Engine host - map quality tiers probed for all 5 BR maps

- Did: read each map's `.jsonmap` `filePaths` tiers and probed 100 declared paths
  with a new `tools/probe_map_quality.py`. `bd` + `low` ship on all five maps
  (environment.json, playerEnvironment.json, `.rcidx` with
  `RCEffectName=jx3bd/defaultlow`, bd skybox); `bddnc`/`mb` are declared but ship
  zero files; the tiers carry no heightmap/foliage - HD root is the only scene data.
- Evidence: `docs/engine_host/MAP_QUALITY_TIERS.md`; tool run "hits 40 of 100".
- Outcome: solved - no lower-quality geometry exists to downshift to; preset work
  (1.8-1.10) must drive render/effect options, next probe = `RCEffectName` consumer
  + `zhcn_hd\config\config_*.ini` linkage.

### 2026-10-04 - Tools - small correctness batch (hole mapping, PvP verifier, doc drift)

- Did: fixed `check_hole_mask.py --spawn-cells` world-Z mapping (converted/engine
  row -> world z = n-1-r), validated by the 2026-09-29 fall witness: world
  (22850,30450) is a hole only under the flipped mapping. Made
  `verify_pvp_evidence.py` source-portable (env overrides `REBORN_MAPLIST`/
  `REBORN_SKILLS` + repo-relative candidates, hard exit 2 when missing) and fixed
  the loot selftest count 8 -> 9 in the README + both AGENTS files.
- Evidence: `verify_pvp_evidence.py` exit 0 with 0 mismatches on all 6 tables;
  `--spawn-cells 400` prints `hole cell (228,207) -> world (22850,30450)`.
- Outcome: solved.

### 2026-10-04 - Movement/terrain - R32 <-> BCH relation resolved

- Did: decoded the `.bch` container (36-byte header + samples^2 float32, per-region
  normalized [0,1]) and compared it with the renderer `.r32` (513^2 float32, no
  header, ~0.5 band). They are the same field: BCH is row-flipped in Z and exactly
  affine to R32 (`r32 = r32_min + bch_flip*(range)`, residual 2e-8, corr 1.0).
  Added `tools/movement/r32_bch_relation.py` for the comparison.
- Evidence: `docs/movement/TERRAIN_R32_BCH_RELATION.md`; tool output
  `affine r32 = 0.010569 * bch_flip + 0.500397 max residual 0.000000019`.
- Outcome: partial - relation solved; the BCH header floats' exact semantics
  (candidate max/min in cm) remain open (prediction 651.5 vs logged sample 761);
  next probe = disasm `_LoadHegihtRegionBCH` or A/B `LoadRegion` at known cells.

### 2026-10-04 - Movement/terrain - spawn ground settle fixed (stable-value, not non-zero)

- Problem: `RC_SPAWN` in a not-yet-streamed region stalled the settle for 10 s and
  ended with py=0; one pre-cache run got a partial value (2019) at the same point.
- Tried: camera-column warmup (no effect), actor-based warmup (no effect),
  invalidate+reload every 250 ms (39 loads, still 0) - then measured the loader's
  answer: a stable **0** for 30+ s with fresh loads at (-1000,24224), i.e. the low
  ground west of the mesa. The engine loader is the gameplay truth; the old settle
  waited for a NON-zero value that never comes (the 2019 run was a pre-cache
  partial-load artifact).
- Fix: settle waits for the sample to stop changing (bounded 2 s) and accepts it;
  region loads that come back all-zero are never cached (retry; `zeroRetries`
  telemetry). Verified: settle 250 ms at (-1000,24224) (was 10,047 ms), 281 ms /
  py=761 at the M1 spawn (23334,24224) matching the M1 proof; crossing run
  unchanged (2 loads, 0 per crossing); collision 36/36; gravity PASS.
- Evidence: logs `reborn_20261005_000105` (old stall), `000403`/`000447` (fixed).
- Outcome: solved. Related open: the coarse 16-bit BCH variant disagrees with the
  loader at that point (~6090 vs 0) - `TERRAIN_R32_BCH_RELATION.md` §Open.

### 2026-10-05 - Movement/collision - contact offset 0.1 registered N/A (ctor default)

- Did: evidence check on the last "CCT default not modelled" row. `contactOffset`
  0.1 m is the base `PxControllerDesc` **ctor default** (`+0x38`, dump
  `pxcontrollerdesc_ctor.txt`); the online body is the SIMWorld/KCharacter solver
  (`bAddPlayerPhysicsActor=0`) with no recovered consumer/value (G-1) - the same
  class as `stepOffset` 0.5 m, which the host already rejected in favor of the
  64 u game-side tolerance (`1a20b96`, comparison §8.1 4f). Applying 0.1 m (10 u)
  would be a guess-fix, so it is registered N/A with re-open criteria.
- Evidence: `COLLISION_SYSTEM_STATUS.md` §4 row updated; comparison §8.1 #16;
  `JX3_STEP_FORGIVENESS_RESEARCH.md` §8 correction note.
- Outcome: solved (N/A with evidence). Re-open if the online character is proven
  to use the PxController or a SIMWorld skin value is recovered.
### 2026-10-05 - Audio - host audio step 1 (Wwise init + provisional skill WAV)

- Did: the product client now calls `KG3DSoundCLR.Init(startupPath, hwnd)` after
  `editor.Init` and `sound.FrameMove()` each frame; on skill cast it plays the
  decoded FLWS WAV (`bin64\flws_sound.wav`) via winmm. The engine's tani SoundTag
  still does not fire in the host (SOUND_PATH.md Frida: Wwise inits, no
  LoadBank/PostEvent), so the WAV play is a REGISTERED PROVISIONAL - re-open when
  the SoundTag fires with the banks loaded.
- Evidence: `docs/audio/HOST_AUDIO_STEP1.md`; run `reborn_20261005_135524.log`
  (`sound: KG3DSoundCLR.Init ok`, `sound: skill wav play rc=True`, `skill cast`;
  no engine Wwise lines). Feature build `reborn_client_audio.exe` (title
  `sandbox-audio`), clean exit, shim `d6=seed`.
- Outcome: solved (step 1); native tag path still open (LOW hypothesis: the
  client loads a bank the host does not).
### 2026-10-04 - engine_host - Startup stall was a shader-DB TCP timeout, not predraw (RC_STARTUP=nodb, D7)

- Did: chased the ~24 s engine init. Planned fix was a pre-draw thread override;
  built `predraw_shim` (force threads:4/8 via 6-byte substitutions at
  `KG3DEngineDX11EX64` RVAs `0x8CFF3F/0x8CFF47`, skip via NOP at `0x8CFF64`).
  It applied (`applied=1 sites=2`) but init stayed 24.2-24.6 s for every mode -
  threads, skip, baseline all identical.
- Profiled the gap: CPU ~45 % of one core, disk read avg ~0.14 MB/s, GPU mostly
  idle; `Get-NetTCPConnection` showed `10.11.10.102:1433` in `SynSent`. Disasm +
  strings in `KG3D_MaterialSystemX64.dll` pinned
  `KG3D_MaterialShaderManager::_initShaderUpload` (RVA `0xAAA980`): blocking
  `connect()` to the editor shader-compile DB. The predraw master-switch log
  line simply sits immediately before the SYN-timeout wait.
- Fix: `startup_shim.dll` (DLL-load notification on `KG3D_MaterialSystemX64.dll`,
  PE stamp/size + exact 13-byte string guard) rewrites the hardcoded IP literal
  at RVA `0x3D4518` to `0.0.0.0` in memory; `connect()` fails instantly
  (`WSAEADDRNOTAVAIL`) and the engine takes its own existing "server not ready"
  path. `RC_STARTUP=nodb`; unset = shipped behaviour and the DLL is never
  loaded. Data-only write, no code patch.
- Measured: `Init3DEngine` 24 563/24 187 ms -> 3 187/3 203 ms (engine
  `const time` 24.375 -> 2.844 s); full 8x8 map 3 094 ms + `LoadMap` 1 563 ms;
  spawn/terrain/per-region RGB unchanged; hitch p95 27 ms both.
- Lesson: a suspiciously fixed ~21 s wait is a TCP SYN timeout - check
  `SynSent` before instrumenting the work itself. A log line adjacent to a gap
  is not evidence of what the gap is.
- Dead end: the pre-draw thread override (removed after the A/B; negative
  evidence kept: forced threads had no effect, so the stall is not the predraw
  thread wait).
- Evidence: `docs/engine_host/FAST_STARTUP.md`; deviation D7
  (`docs/camera/HOST_DEVIATIONS.md`);
  `proof/engine_host/startup_nodb_2026_10_04/` (logs, engine init extracts,
  fingerprints).
- Outcome: solved; default rollout pending (sandbox launcher only, after user
  review).

### 2026-10-04 - engine_host - D7 hardening: content-scan guard (no hardcoded RVA)

- Did: replaced the hardcoded RVA `0x3D4518` + PE-stamp gate in
  `startup_shim.cpp` with a scan of the module's readable sections for the
  exact standalone literal `10.11.10.102` (byte-equal, next byte NUL); every
  match (cap 8) is rewritten to `0.0.0.0`. PE stamp/size are now reported only
  (`build=ok|mismatch`).
- Why: our own client rebuilds never change the installed engine DLL, but a
  game/MovieEditor update would have invalidated a stamp gate; with the scan,
  an update that keeps the address needs no action.
- Verified: `RC_STARTUP=nodb` run -> `Startup: mode=nodb applied=1 sites=1
  build=ok`, `Init3DEngine=3109 ms`; shim rebuild exports `RC_Startup_*`.
- Evidence: `native/startup_shim.cpp`; `docs/engine_host/FAST_STARTUP.md`;
  D7 register text.

### 2026-10-04 — Render options — improvement plan for areas 1.8–1.10 (#iso)

- Did: created the isolated worktree/branch for the rendering/LOD/weather track
  (parallel to the 1.3 terrain agent and the predraw agent); wrote
  `docs/engine_host/RENDERING_OPTIONS_PLAN.md` (corpus census + phases P0–P5 +
  boundaries with the other agents) and registered it in `docs/engine_host/README.md`.
- Census (read-only, PowerShell): 15 preset files in `zhcn_hd\config`; 9 main tiers
  234–375 key entries, `_bd_` family 472 each; 408 unique keys, 72 vary across tiers;
  `[ENGINEOPTION] nEngineGraphicsLevel=1..9` is the tier selector;
  `MovieEditor\config.ini` (376 entries, all `bEnableRC_*`, no level key) is the
  config our host actually loads (cwd = MovieEditor, install read-only).
- Evidence: census commands in the plan's Reproduce section; this commit.
- Outcome: partial (plan-only, as requested) — research/implementation phases
  P0–P5 defined with verification and gates.
- Re-open: n/a.

### 2026-10-04 — Render options — P0 execution (census tool + consumer xrefs)

- Did: implemented `tools/render/preset_census.py` (stdlib) → `proof/render/option_matrix.tsv`
  (490 keys), `varying.tsv` (72), `gpu_switch_summary.tsv` (20x32); wrote
  `docs/engine_host/RENDERING_OPTIONS.md` (P0.3/P0.4 partial).
- Findings (HIGH): engine reads `config.ini` from cwd via
  `KG3DEngineAdapterX64.dll!KG3D_LoadJX3Config_From_DX9` @RVA 0x5F9F0 (~150 keys with
  code defaults/clamps; `nEngineGraphicsLevel` cfg+0x260 default 0/3 by nRenderLevel==100);
  adapter save fn @0x67B10; `JX3UIX64.dll` @0x118970 = video-panel key/type/offset schema;
  `JX3ClientX64.exe!InitMachineConfig` @0x9A8A0 reads `config/machine_config.ini`.
  Open: preset-file selection owner (no `config_N`/`GpuSwitchOption` literals in bin64),
  per-option caps, apply path (install `config.ini` read-only).
- Evidence: `proof/render/*`, xref disasm under `proof/render/disasm/`; reproduce in
  `RENDERING_OPTIONS.md` §Reproduce.
- Outcome: partial — P0.1 done, P0.3/P0.4 partial (apply path not decided).
- Re-open: P0.4 selection owner; P1 apply path; P2 caps probe.

### 2026-10-04 — Render options — P0.4: active config is a generated merge

- Did: full-install scan (17,843 files, `Game\JX3` + `MovieEditor`, read-only) for the
  preset names and `GpuSwitchOptionTab`; compared the game's active `zhcn_hd\config.ini`
  against the `_bd_` presets.
- Findings: preset file names are referenced by **no** install binary; active config.ini
  (471 keys, `nEngineGraphicsLevel=1`) is a merge closest to `config_bd_1_zuijian`
  (412/467 shared values identical) — so the applied set is generated (base + bd tier +
  machine/user overrides), not a file copy. MovieEditor `config.ini` (host runtime) is a
  tier-7-ish editor config, install read-only. Per-user display settings also persist in
  `userpreferences.jx3dat` (player state).
- Evidence: `RENDERING_OPTIONS.md` §2.1; census values in `proof/render/`.
- Outcome: partial (research) — selection writer still open; apply path unchanged.
- Re-open: P1 apply-path probes; caps probe P2.

### 2026-10-04 — Render options — apply path implemented and proven (P1–P3), weather API (P4)

- Did: managed-API reflection found `MovieEngineCLR.KGEngineCLR.SetEngineOptionFromConfigFile(string)`
  (+ `EnableDynamicWeather`/`SetDynamicWeatherParameters`); implemented `client/VideoOptions.cs`
  (`RC_QUALITY=1..9/bd/ default`, `RC_OPT_FILE`, `RC_OPT_<KEY>` merge to a generated ini in
  `bin64\reborn_out`, `RC_WEATHER[_PARAMS]`) wired right after `Init3DEngine`; built feature
  client `reborn_client_renderopts.exe` (title `sandbox-renderopts`, own namespace); 5 runs.
- Results: tier1 vs tier9 differ on every 4x4 region (mean `#BAB197` vs `#C1BBA6`); tier-1
  repeat delta is 11 PNG bytes (noise) → tier effect causal; 3 `RC_OPT_*` overrides pull
  tier9 back to tier1-like (`#BAB198`); tier9 ≈ -27 % fps vs tier1 (391 vs 536, measured
  before another agent's client started); `EnableDynamicWeather(1)=0` success but run D is
  pixel-identical to tier1 (weather semantics/params open).
- Evidence: logs `reborn_20261004_231520/231631/231738/231852/231943/232104.log`,
  `proof/render/runs/{quality1,quality1b,quality9,quality9_override,weather1}.png`,
  `docs/engine_host/RENDERING_OPTIONS.md` §4/§4b/§4c.
- Outcome: solved for P1–P3 first cut; P4 partial (API found, effect not observable with
  defaults); P5 initial numbers.
- Re-open: weather param/scene semantics; `GpuSwitchOptionTab` matching; full option caps
  read-back (proxy has no public fields; native `GetOption` export or config round-trip).

### 2026-10-04 — Render options — gates + no-op regression

- Gates after the client change: `jx3_model` exit 0, `gravity/verify_model` exit 0,
  `loot/capture.py selftest` PASS, `collision_selftest_reborn_client_renderopts` 36/36,
  canonical `camera_smoke.exe` ALL PASS.
- No-op regression: `reborn_client_renderopts.exe` with no `RC_QUALITY`/`RC_OPT_*`/`RC_WEATHER`
  → exit 0, `LoadMap result=0`, zero `VideoOptions:` log lines (`reborn_20261004_232425.log`).

### 2026-10-04 — Render options — P2 caps matrix, P4 env decode, P5 second pose

- Did: 12 more engine runs. P2 isolated caps (tier 9 base, one key per run): bloom=0 is the
  dominant visible change (`#C1BBA6`→`#BAB198`, all cells); AO=0/SSR=0/nShadowType=0/
  nFoliageDensity=5 are no-ops at the house pose; `nFoliageDensity=999` is pixel-identical to
  tier 9 (100) → clamp confirmed behaviorally. P5 second pose (real spawn dune): tier 9 vs
  tier 1 = −34 % fps (369 vs 562) + large local image deltas. P4: decoded all map
  environment quality variants (HD root `enableDayNightCycle=1`, `bd`/`low` differ; player
  light rigs in `playerEnvironment.json`); day-night option gives only a 1–2-unit,
  non-evolving shift; dynamic-weather toggle still a no-op with defaults.
- Mistake + correction (recorded): the first caps batch leaked `RC_OPT_*` env vars between
  runs in one PowerShell process (AO chained into bloom/SSR); the 4 contaminated artifacts
  were deleted and re-run isolated; the doc table cites only the clean set.
- Evidence: `proof/render/runs/p2_*.png`, `proof/render/logs/p2_*.log`,
  `proof/render/environment_summary.txt`, doc §4b/§4c.
- Outcome: P2 caps first complete matrix; P4 data decoded / effect open; P5 two poses.
- Re-open: foliage caps at a foliage pose; per-LOD isolation; weather params/time source;
  GpuSwitch consumer (external tooling — absent from install).

### 2026-10-05 - Merge - render-options <- terrain-stream-holes (branch combine)

- Did: merged `agent/terrain-stream-holes` (7 commits: LRU terrain cache `c32f095`,
  hole A/B all maps, streaming audit, R32<->BCH relation, quality-tier probe) into
  `agent/render-options` at `8e3ef69`; the combined branch is the one the 1.3 agent
  fast-forwards to. Conflicts: 2 (`docs/EXPERIENCES.md` append - theirs first then ours
  by commit time; `docs/engine_host/README.md` rows - both kept). `client/RebornClient.cs`
  auto-merged (their telemetry vs our `VideoOptions.Apply` call site).
- Validation on the combined tree: feature build exit=0; gates `jx3_model` 10 PASS,
  gravity `verify_model` PASS, loot selftest PASS, collision selftest 36/36, `camera_smoke`
  ALL PASS; boot run `reborn_20261005_000130.log` `git=8e3ef69 dirty=0`, map load ok,
  `cache=4` telemetry + `terrain load (2,2) ms=4.1`, no `VideoOptions:` lines (no-op path).
- Observation for the terrain owner (not a merge blocker, MED): `r32_bch_relation.py` on
  the sandbox-renamed pair 000_000 (`C:\jx3tmp\reborn_sandbox\map\龙门寻宝_h`) prints
  `max residual = 0.006060575`, not 0 as the doc claims for 002_002 - would be worth
  re-checking whether the sandbox crop rename pairs the same regions.
- Handoff: in `reborn-iso-terrain-stream-holes`, commit/stash WIP then
  `git merge --ff-only agent/render-options` (or plain merge if new commits landed) and
  rebuild the feature client - the compile list now includes `client/VideoOptions.cs`.

### 2026-10-05 - Merge - agent/terrain-stream-holes into main (all tracks)

- Merged `agent/terrain-stream-holes` (`1928387`) into main with `--no-ff` (`19232f9`).
  The branch already contained `main` (`4d985a4`) and had absorbed `agent/audio-host`
  (`3a01ecf`) and `agent/render-options` (`1928387`), so the merge was conflict-free
  (`git merge-tree --write-tree` exit 0) and the merged tree equals the branch tree.
- Contents: terrain 1.3 (region LRU cache + telemetry, hole A/B on all hole-bearing
  maps, R32<->BCH relation, spawn settle), small fixes (tooling hygiene, map-quality
  probe, contact-offset N/A, audio step 1 provisional), predraw fast startup
  (`RC_STARTUP=nodb`, D7) and render-options 1.8-1.10 (`VideoOptions` apply path,
  preset census, weather decode, LOD/cull reference).
- Gates on the merged tree: unique feature build exit 0; `camera_smoke_terrainstream`
  ALL PASS; combined smoke `reborn_20261005_144117.log` (Init3DEngine 3,157 ms,
  `RC_QUALITY=1` applied, settle 250 ms, `terrLoads=2` 0 per crossing, clean DONE);
  collision 36/36; gravity PASS; `jx3_model` 10x PASS; loot selftest PASS.
- Canonical `reborn_client.exe` + sandbox `reborn_client_mini.exe` rebuilds are
  blocked by running clients (`reborn_client` PID 26676 started 13:55:38,
  `reborn_client_mini` PID 12904 started 14:05:32) - rebuild + relaunch once those
  windows are closed.
- Local only: not pushed to origin.

### 2026-10-05 - repo - Merge agent/predraw finalize (fast startup) + sandbox launcher codepage fix

- Merged `agent/predraw` into main (`4757ada`, `--no-ff`, conflict-free; main ==
  base `eab4277`). Contents: `RC_STARTUP=nodb` startup shim (D7, content-scan
  guard), `hitch=` metric, docs/proof, sandbox launcher default.
- My post-merge pass: `native\build_startup.cmd` + canonical + mini rebuilds
  exit 0; `camera_smoke` ALL PASS; gravity PASS; loot selftest PASS; `jx3_model`
  10x PASS. The parallel `agent/terrain-stream-holes` merge (`19232f9`) then
  absorbed the predraw merge along with render-options/audio/terrain tracks and
  re-ran the combined gates (smoke ALL PASS incl. `RC_STARTUP=nodb` Init
  3 157 ms; collision 36/36).
- Fix during finalize: `tools/sandbox/run_sandbox.cmd` is UTF-8, but cmd parses
  batch files in the OEM codepage (936), so the non-ASCII `RC_MAP` arrived
  mojibake (`榫欓棬瀵诲疂_s`) and `LoadMap` failed `E_FAIL ms=0` - latent since the
  launcher was written (agents had been setting `RC_MAP` manually). Added
  `chcp 65001 >nul` after `@echo off` (`2cbf004`); verified end-to-end: mini
  init 3 672 ms, `LoadMap` 156 ms, `regions=1x1`.
- Final state: main tip `2cbf004`; canonical + mini rebuilt from it at 14:56:45
  (`git=2cbf004`) and relaunched: `Main-Full-Client` (shipped 24.8 s path) and
  `sandbox-mini` (`RC_STARTUP=nodb`, 3.6 s init, `LoadMap` 156 ms).
- Observation (open, not fixed): two clients started in the same second share
  one `reborn_<ts>.log` (second-resolution filename) and their lines interleave;
  attribution still works via the `build=` fingerprints, but the name should
  gain a PID/ms suffix.
- Local only: not pushed to origin.

### 2026-10-05 - Engine host - A1 day-night: managed API decoded, effect is install/data-bound

- Did: extended the `RC_API_DUMP` filter (time/weather/env keywords), then added
  reflection probes (`RC_ENV_PROBE=1/2/3`) that recovered the exact managed surface:
  `KGSceneCLR.Set/GetTrueSkyDayTime(float)` (getter stuck at 0.5), season
  relative-year time + `Set/GetSeasonParam` (values stick), `CreateGDBTimelineCurveFromFile`
  + interpolation (E_FAIL, no timeline), `ResetEnvironment(dir)`, and the
  `KG_EnvironmentCLR` surface (225 methods: `SetRealSystemDayTime/Timezone/MaxSun/
  MaxMoonLightIntensity`, directional lights, wind, fog volumes, clouds, lens flares).
  Decoded the map's `dayNightCycle` object - the shipped HD env authors **all
  Max* intensities = 0**.
- Tried: day-time sweeps via TrueSky and real-system setters, season params, sun
  arcball, GDB interpolation, and an `RC_ENV_DIR` override (`ResetEnvironment` rc=0,
  MaxSun=6). **All frames identical within noise** (`proof/render/daynight/`).
- Outcome: boundary (HIGH) - the day-night effect needs the TrueSky module
  (`KG3D_TrueSkyX64.dll` exists only in the game client, not in MovieEditor;
  install read-only) and/or authored nonzero `dayNightCycle` intensities / a GDB
  timeline (0/56 probe paths). Knobs kept: `RC_DAYTIME`, `RC_ENV_DIR`. Re-open when
  TrueSky ships in the editor install or map data authors the cycle.
- Evidence: `docs/engine_host/RENDERING_OPTIONS.md` §4d; logs `reborn_20261005_17*`.

### 2026-10-05 - Engine host - A2/A3: dynamic weather inert, sky/cloud boundaries verified

- Did: swept `RC_WEATHER=1` + `RC_WEATHER_PARAMS` (all-1, all-100) - `EnableDynamicWeather(1)=0`
  applies but both frames are pixel-identical to the dry baseline; the 12-float semantics
  stay undecoded. Probed `KG3D_TrueSkyX64.dll` (game client only; absent from MovieEditor)
  and `volumetricCloud.json` (0/16 map-tier hits; only `focus_face_env_params.json` ships).
- Outcome: boundaries registered - dynamic weather needs the native consumer decode + the
  `oldSkyWeather` particle assets at a weather-authored state; TrueSky and volumetric cloud
  are install/asset-bound. Post-FX caps matrix at 3 poses still open (A3 remainder).
- Evidence: `proof/render/daynight/weather_w1.png` / `weather_w100.png`;
  `RENDERING_OPTIONS.md` §4d.

### 2026-10-05 - Engine host - 1.9 finished: data-truth resolution + post-FX caps matrix

- Did: resolved the day-night/weather question as a **data truth** - the BR maps contain
  no TrueSky keys, only `oldSkySkyBox`/`oldSkyWeather` + a static `sunlight`/`moonlight`,
  and a `dayNightCycle` object with all `Max*` = 0; a PATH experiment (game bin64 +
  `bShowTrueSky=1`) shows `KG3D_TrueSkyX64.dll` is never loaded (module dump). So
  day-night/TrueSky weather are **not applicable to the product's 5 maps**; the managed
  APIs stay available for content that references them.
- Also completed the post-FX caps matrix (tier 9, one key per run, 4x4 fingerprints):
  visible = `bEnableRC_Bloom` (16/16 all poses), `bEnableRC_AmbientOcclusion` (16/16 at
  the vista), `bEnableRC_Vignette` (12/16), `bEnableRC_AtmosphericFog` (15/16 at the
  field); no-op at these poses = HeightFog, LightShaftBloom, SSR, SunLensflare, EnvProbe.
- Outcome: 1.9 complete for the product maps (open: 12-float dynamic-weather semantics,
  TrueSky/volumetric-cloud assets, GDB timeline - all registered with re-open criteria).
- Evidence: `docs/engine_host/RENDERING_OPTIONS.md` §4d; `proof/render/postfx/` key pairs;
  logs `reborn_20261005_1745*`.

### 2026-10-05 - repo - Cleanup: agent/predraw worktree + branch closed after merge

- Removed worktree `Desktop\reborn-iso-predraw` and branch `agent/predraw`
  (merged into main via `4757ada`, absorbed by `19232f9`; no uncommitted work).
- Removed the stale feature artifacts from shared bin64:
  `reborn_client_predraw.exe`, `collision_selftest_reborn_client_predraw.exe`,
  `build_info_reborn_client_predraw.exe.txt` (built from the pre-merge branch).
- Main stays at `2d45e16` == `origin/main`; the running canonical/sandbox
  clients are the `2cbf004` rebuild (docs-only delta since).
- Local only: not pushed to origin.

### 2026-10-05 - Engine host - C (1.8 leftovers): key-offset generator + boundary closures

- Did: added `tools/render/key_offsets.py` - parses the adapter/UI disasm captures into a
  key -> struct-offset table (r9-destination + eax-store patterns): **290 keys**, exact on
  the self-check pairs (`nShadowType 0x5c`, `nEngineGraphicsLevel 0x260`,
  `fSpeedTreeCullDist 0xa40`) -> `proof/render/key_offsets.tsv`. Probed `configHttpFile`
  in 10 install binaries (both installs): **0 hits** -> the `Init3DEngine` argument is
  unused/ignored in this build. Closed the active-config merge owner and the
  `GpuSwitchOptionTab` consumer as external (no install binary references them).
- Evidence: `docs/engine_host/RENDERING_OPTIONS.md` §5 items 1-3 resolved;
  `proof/render/key_offsets.tsv`; `tools/render/key_offsets.py`.
- Outcome: C complete.

### 2026-10-05 - Engine host - B (1.10): LOD/cull per-option matrix at house + vista

- Did: 26 isolated runs (13 keys x 2 poses, tier 9 base, one `RC_OPT_<KEY>` per run)
  with 4x4 fingerprint diffs vs the tier-9 baseline. Visible levers at the vista:
  `nShadowType=0` (8/16 cells), `fSpeedTreeCullDist=5000` (6/16),
  `fSimpleModelCullDist=1000` (1/16, marginal). Model-LOD / node-LOD / view-angle /
  foliage / particle keys show no visible delta at these poses (coarse grid, load-time
  or no such geometry in view).
- Evidence: `docs/engine_host/LOD_CULL_MATRIX.md`;
  `proof/render/lod_vista/{base_t9,nShadowType,fSpeedTreeCullDist,fSimpleModelCullDist}.png`.
- Outcome: partial - first matrix committed; foliage-rich pose, 8x8 grid for the LOD
  keys and the causal tier fps ladder remain open.

### 2026-10-05 - Engine host - B remainder: foliage pose, 8x8 LOD re-check, tier fps ladder

- Did: parsed the baked foliage bin (FCOL v1) to pick the densest `.foliage` cluster
  (`(173747,98442)`, 42 instances) and ran the foliage keys there - `nFoliageDensity`
  0/999 no-op (**load-time**), cull/render toggles marginal (1-2/16 cells). Re-ran the
  no-op model-LOD keys at the vista with aggressive values on an **8x8** grid: 0/64
  cells each (inert in the editor host). Measured the causal tier ladder (t=16 s, 20 s
  runs): house 592/550/352 fps and vista 453/563/333 fps for tiers 1/5/9; hitch <=14 ms.
- Evidence: `docs/engine_host/LOD_CULL_MATRIX.md`;
  `proof/render/{foliage,lod8}/` key PNGs; logs `reborn_20261005_18*`.
- Outcome: B complete (first pass). Remaining open: foliage density map-reload A/B and
  a close-up pose for the model-LOD keys.

### 2026-10-05 - Camera - E: modes/skill-FOV/tracks status (blocked, no invented triggers)

- Did: investigated the 1.5 remainder instead of implementing invented triggers.
  (1) Auto mode switching: the WW sprint trigger was **removed by user decision**
  (EXPERIENCES 2026-09-30) and the real state sources (engine sprint/mount/dialog/
  spectate) do not exist in the host -> blocked; re-open when those states are modeled.
  (2) Skill-move camera: decoded `proof/netcode/camera_files/skill_move_camera.txt`
  (8 rows; duration/hold/yaw-rate/screen-FX/edge/saturation); the test skill has no row;
  the consumer is the SkillMove runtime -> blocked on the skill runtime.
  (3) Camera tracks: the managed surface (`KGMovieEditorCLR.ExportCameraTrack` /
  `SetCameraTrackPlaySpeedPerMS` / `SetCameraTrackPlayMethod` / `SetTrackForEditor`) is
  editor playback only; next probe = the `.mani`/KRLCameraAni consumer in the game client.
- Evidence: `docs/camera/MODE_ANIM_STATUS.md`; no code change (nothing invented).
- Outcome: E documented as blocked + next probes; nothing implemented by design.

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
  2. host object [ctx+0x18] -> 
t[0xa0] (slot 20) with the position -> places;
  3. host [ctx+0x18] -> 
t[0x58] (slot 11) with 
dx=sfx -> **registers the SFX
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
  singleton's 
t[1] (activate) / 
t[2] (deactivate).
- Calling the singleton's 
t[1] from our host **blocks/hangs** (no return; killed at
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


### 2026-10-05 - Audio (1.6) - native probe: tag path proven to stop before Wwise

- Did: recovered the host sound architecture (KG3DSoundCLR -> MovieEngineCLR loads
  **KG3DSoundX64.dll** editor shell -> FMOD + `KG3D_WwiseX64.dll` per
  `[WwiseSetting] UseWwise=1`; the game's `KG3DWwiseSoundX64.dll` is a thin shell not
  in MovieEditor) and added a native probe (`native/sound_probe.cpp`,
  `build_sound_probe.cmd`, `RC_SOUND_HOOK=1`) that inline-hooks
  `AK::SoundEngine::PostEvent` (id/ANSI/wchar) and `LoadBank(wchar)` in our own
  process, logging every call and tail-calling the originals.
- Result: hooks install (rc=0) and the whole run - including the t=18.5 s skill cast -
  shows **zero PostEvent and zero LoadBank calls** (`proof/audio/sound_probe_20261005.log`).
  The native shell/Wwise stack is loaded and `GetWwiseManager` resolves, so the stop is
  upstream of Wwise (Frida's spike finding now instrumented in-process, HIGH).
  PSS particle SFX tags do fire, so the anim tag system works; only the sound path stops.
- Next probes (ordered): hook `_OnProcessApplySoundTag` (INT3/VEH) to see whether the
  callback fires; hook `KG3DModel::EnableSfxSoundTag` (`0x1800BA760`) and, if never
  called, capture the model pointer via PlayAnimation and call it - the likely gate.
- Evidence: `docs/audio/NATIVE_AUDIO_PROBE.md` (indexed); module dump
  `reborn_20261005_182551.log`; probe run `reborn_20261005_183218.log`; committed
  `proof/audio/*`.

### 2026-10-05 - Audio (1.6) - native Wwise playback finished (game banks + skill event)

- Did: extended the native probe (`native/sound_probe.cpp`) into the playback path and
  wired it in the client: loads the game's own banks (`Init.bnk`, `skillremake.bnk` from
  the extracted `assets/sound/`) into the engine's `KG3D_WwiseX64.dll` via
  `LoadBankMemoryView`, registers game object 1 + default listener, and posts the FLWS
  event `3378728138` on skill cast. Env: `RC_BANK` enables native (default when set),
  `RC_SOUND_NATIVE=0` disables, `RC_SOUND_EVENT` overrides. The winmm WAV is now only
  the fallback when no bank is provided.
- Evidence: `reborn_20261005_184354.log` (`sound-native: Init.bnk rc=1`, `bank ok
  id/rc=1`, `sound: native post id=3378728138 playing=1`, clean DONE); probe log
  `proof/audio/sound_probe_native_20261005.log` (LoadBankMemoryView rc=1,
  RegisterGameObj/AddDefaultListener =1, PostEvent playingId=1). Wwise enum note:
  AKRESULT 1 = `AK_Success` (0 = `AK_NotImplemented`).
- Context: the engine's own tani SoundTag dispatch still never calls PostEvent in the
  host (instrumented, `NATIVE_AUDIO_PROBE.md` §3); the client posting the event with the
  game's bank/id is the product path. Recorded suspects if the engine path is ever
  restored: `_OnProcessApplySoundTag`, `KG3DModel::EnableSfxSoundTag` (`0x1800BA760`).
- Outcome: solved - 1.6 runtime audio plays through the engine's own Wwise.

### 2026-10-05 - Audio (1.6) - correction: Wwise API success is not audibility; WAV fallback active

- Finding: the previous entry concluded native playback (banks + `playingId=1`) but the
  user heard nothing. Diagnostics: `IsInitialized=1` (48 kHz, 1024/frame);
  `GetSourcePlayPosition(playingId)` -> `AK_Fail`, pos 0/0 in **every** tried
  configuration - staged `.wem` tree (file-id + source-name variants under
  `Base`/`English(US)`/`SFX`/root), process-cwd switch, `SetCurrentLanguage(Base)`,
  `RenderAudio` ticks. The bank has **0 embedded RIFF blocks**: the audio is streamed
  (`161340541.wem`), and the editor host's Wwise IO cannot resolve it (the game client
  supplies its own VFS-aware `IAkFileLocationResolver`; MovieEditor has none).
- Fix (product behavior): after `PostEvent`, the client requires the source position to
  advance (`SoundProbe.Diag`); if not, native is disabled and the winmm WAV plays the
  cast. Verified: `sound-native: no rendering (streamed media unresolved) - WAV
  fallback` followed by `sound: skill wav play rc=True`.
- Open (for a future pick-up): `AK::StreamMgr::SetFileLocationResolver` (export
  present in `KG3D_WwiseX64.dll`) with a resolver that serves the pak media, or reuse
  the game's resolver; then the fallback can be dropped.
- Evidence: `proof/audio/sound_probe_streamed_media_diag_20261005.log`,
  `proof/audio/client_audionative_fallback_20261005.log`; corrected docs
  `HOST_AUDIO_STEP1.md` Step 2 and `NATIVE_AUDIO_PROBE.md` §4.

