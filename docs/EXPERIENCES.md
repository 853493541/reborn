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
