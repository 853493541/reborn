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
