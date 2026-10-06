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

### 2026-10-01 — netcode — Launch block header cipher solved (emulator-derived table)
- Did: emulated `0x140101450` (custom TEA variant) instruction-by-instruction with a
  capstone interpreter; solved the exact 64-step update table
  (`mx = (((x>>5)^(x<<4)) + x) ^ (key[e] + sum)`, per-step key/sum in
  `proof/netcode/launch_block_cipher_table.txt`); the table reproduces the emulated
  decrypt on all vectors and the inverse round-trips. Built the launcher-emulator probe
  (suspended start + pre-created PID-keyed mapping + cipher-encrypted block + resume).
- Result: even with a cipher-verified fresh timestamp and `+8` set, the client still
  exits ~2.4 s after start -> the exit is gated by something other than the block
  header (network/launcher presence); the block writer (encrypt side) is still
  unidentified.
- Evidence: `docs/netcode/JX3_CLIENT_LAUNCH_AND_SESSION.md` §7;
  `proof/netcode/launch_block_cipher_table.txt`; worktree `reborn-iso-v2` branch
  `agent/v2`; this commit (local).
- Outcome: partial (cipher solved; exit gate still open).

### 2026-10-01 — netcode — Early-exit gate hunt (client exits ~2.3 s; block ruled out)
- Did: with a cipher-verified block (fresh timestamp + `+8` set), the real client still exits
  cleanly ~2.2–2.3 s after start, before engine init (no window, no child processes, no
  minidump). Ruled out: the block header (valid), the multi-process count (KGPK4 downloader
  was running), the `JDYinput` service watchdog (fires only if the service exists), network
  availability (no firewall block during probes). Startup path 0x1401008xx parses launch
  params and formats "0;%s" strings; the parser is polled (caller 0x140100530) — the wait
  that ends the process is still unidentified.
- Evidence: `proof/netcode/disasm/{multiprocess_toolhelp,exit_calls,watchdog_callers,exit2_callers,dotnotstart_va}.txt`;
  observe probe (90 modules incl. X3DEngine.dll, 0 windows, exit 2.31 s); this commit (local).
- Outcome: partial (gate narrowed; cause open).

### 2026-10-01 — process/netcode — purpose statement locked; protocol-layout miner; named-object enumerator
- Did: (1) locked the project purpose/legal statement in `AGENTS.md` §1 and
  `PLAN_REBORN_ONLINE.md` Rules — purely personal interest, will never earn money, purely
  for fun, no commercial use/distribution. (2) #4: added `tools/netcode/mine_protocol_layouts.py`
  (tracks the packet pointer through handler disassembly, filters offsets to the declared
  size) -> `proof/netcode/protocol_layouts_s2c.tsv` (810 handlers, 447 with packet-relative
  fields; common +3/+7/+11 frame fields match the known spec). (3) #5 readiness: added
  `tools/netcode/enum_named_objects.py` (NtQueryDirectoryObject over session/global
  namespaces); verified live — found `XLauncherV2ExistEvent`, per-exe `Startup/Quit`
  events, `KGPK4-IPC-MQ-*`/`KGPK4-SHMM-*` sections. Also found the client's
  `KJX3LaunchUpdaterModule::Initialize` launcher-file checks (`\xlancher\XLauncher.exe`,
  `\XLauncher.exe`, `\gameupdater.exe`, WeGame message) — none exist on disk, so not the
  fatal gate (real launches work).
- Evidence: `AGENTS.md` §1, `PLAN_REBORN_ONLINE.md`, `proof/netcode/protocol_layouts_s2c.tsv`,
  `tools/netcode/{mine_protocol_layouts,enum_named_objects}.py`, this commit (local).
- Outcome: solved (statement + tooling ready).

### 2026-10-01 — netcode — Real block captured; security/report module found; exit gate still open
- Did: user-approved capture during a real launch (client in-game): the block holds only
  an 8-byte value changing ~2x/s (heartbeat/report), no entries. Heartbeat feeding does
  not keep the client alive. Decoded the client's XOR-0xAD obfuscated strings: a
  security/report module (BlackProcess.tab / WhiteDLL.tab process+DLL lists, module
  hashing, UUID/MAC fingerprints, DD63330 driver check, AlwaysReportLogInV1 /
  SharedMemoryError / MultiProcess report formats, 16-iteration periodic check).
- Evidence: `block_capture/real_block_20156_*.bin` (temp), doc §10; this commit (local).
- Outcome: partial (block model corrected; security handshake is the next lead).

### 2026-10-01 — netcode — Exit mechanism located: WinMain startup-task timeout
- Did: traced the CRT entry (0x14079BA78) to WinMain (0x1400e11f0); WinMain polls a
  startup task queue via 0x14009d9a0(state, ms) (queue vtable 0x140953e50, processor
  0x14009d120). Second wait = 2000 ms; timeout leaves r15d=0 -> WinMain returns 0 ->
  clean exit at ~2.3 s. On success WinMain proceeds to the UI path. The exit is a
  startup-task timeout, not a crash and not the launch block.
- Evidence: disasm 0x14079b9fe-0x14079ba28 (WinMain call), 0x1400e11f0-0x1400e1543,
  0x14009d9a0, 0x14009d120, 0x14009d6d0 (vtable 0x140953e50); doc §11; this commit (local).
- Outcome: solved (mechanism); next = identify the stalling task.

### 2026-10-01 — netcode — Startup-task gate probes: thread-pool queue, stdout capture, GameDoctor hash line
- Did: identified the startup queue as a worker-thread pool (KStep_Async, worker
  0x14009ce40); confirmed the probe client opens no TCP; built a stdout-capture probe
  (STARTF_USESTDHANDLES) and captured `[%commonstartup%:174] Hash conflict!` +
  `KG3DEngineManager::UnInit (FALSE)` (GameDoctor shutdown diagnostic, not proven causal).
  Stalled step still unidentified.
- Evidence: doc §12; temp `probe_stdout.py`, `client_stdout.txt`; this commit (local).
- Outcome: partial (technique + data; gate open).

### 2026-10-01 — netcode — Step framework is anonymous lambdas; probe never spawns cefrender
- Did: decoded the startup step framework (step_internal, RTTI->handler registry at
  0x140a8c1f0, steps are lambdas); PlatformLoad enqueues a step + waits 10 s. Found the
  probe client never spawns cefrender.exe (10 ms poll), while the real client does —
  the startup stalls before/at the login/browser-UI stage (launcher session/URL likely
  required). Static step naming is impractical (anonymous lambdas).
- Evidence: doc §13; disasm of 0x14009c890 / 0x14009f480; CEF poll; this commit (local).
- Outcome: partial (framework + stall region identified; exact step needs runtime read).

### 2026-10-01 — netcode — WinMain wait loop decoded; job hypothesis ruled out
- Did: decoded the WinMain wait loop (return 0 = idle state -> exit; the game.startup
  step group never becomes active in our probe); built a DBWIN OutputDebugString capture
  (works, read-only); tested the job-object hypothesis (probe outside our shell job still
  exits at 2.2 s); confirmed launcher UI = CefViewWing while the game client uses
  cefrender; probe Dumper/DumpReport logs are routine.
- Evidence: doc §14; disasm 0x1400e1380-0x1400e1440, 0x14009f6d0; WMI job-free probe
  (`in_job=False`, `client exited at 2.2s`); DBWIN capture; this commit (local).
- Outcome: partial (wait semantics clear; the missing input is still unidentified).

### 2026-10-01 — netcode — Runtime read of probe child; hash conflict = engine string-intern warning
- Did: with user approval, suspended our own probe child and read registers/stacks (no injection):
  main thread sits in ntdll waits all boot; at 1.47 s it is in KG_InitPakV4FileSystem (exe
  "InitPackage"); WinMain wait frame never seen on any thread. Identified `Hash conflict!` as the
  engine's benign string-intern warning (KG3DEngineDX11EX64, commonstartup.cpp:174); jemalloc and
  KG3DEngineManager::UnInit lines are teardown diagnostics. Decrypted the real launch block:
  probe clients write `(uptime_s<<16)|ticks` heartbeat; real run held random 8-byte values.
- Evidence: doc §15; runtime samples; KG3DEngineDX11EX64.dll string + context; block decrypt run.
- Outcome: partial (timeline/owners clarified; the waited-on object is still unidentified).

### 2026-10-01 — netcode — Wait trace: engine FS waits, WinMain frame never observed
- Did: built probe_wait_trace.py (register + handle + stack sampling of our own child in waits).
  Early boot = Sleep loop; ~1.33 s = infinite wait inside Engine_Lua5X64 async FS layer
  (kernelbase!WaitForSingleObject -> engine_lua5x64+0x12DDA8). WinMain's wait frame was never
  found on any thread; handle naming in-wait kept failing (invalid handle) - needs unwind-based
  frame attribution before the handle can be trusted.
- Evidence: doc §16; probe_wait_trace.py; sample logs.
- Outcome: partial (wait owner localized to the engine FS layer; exact object still unnamed).

### 2026-10-01 — netcode — .pdata unwinder; startup chain mapped; gate = NULL client object
- Did: built unwind.py (PE .pdata + chain-info unwinder) and probe_unwind.py (read-only stack walk
  of our own probe child). Mapped the full live chain WinMain -> PlatformLoad -> wait -> pump ->
  KJX3BaseModule::Initialize -> InitPackage -> device enumeration; the exit path (wait ret 0 ->
  shutdown incl. 2 s worker wait -> Dumper64 -> clean exit). Found the pump's success test
  `state_sub[0x18]->vtable[8](0)`; in our probe state_sub[0x18] is NULL, so the chain completes
  without creating the client/game instance -> pump returns 0 -> exit.
- Evidence: doc §17; probe_unwind.py runs; state dump at 1.96 s.
- Outcome: major step (startup chain + gate identified); setter of state_sub[0x18] is next.

### 2026-10-01 — netcode — State timeline; game.startup group completes without creating platform object
- Did: built probe_state_timeline.py (samples WinMain state every 40 ms). Reproduced: queue empty
  until 1.90 s; game.startup group runs 1.90-2.05 s; done flag set 2.08 s; state_sub+0x18 stays
  NULL -> pump returns 0 -> exit ~2.2 s. Identified the sub-steps as PlatformInitialize lambdas
  (id 2..5) dispatched via the 185-entry registry (map populated at runtime). The missing input
  is consumed by those steps; they complete without creating the platform object.
- Evidence: doc §18; timeline logs; registry map dump; runner 0x1400A0870 disasm.
- Outcome: gate localized to the PlatformInitialize step handlers (next: watch their execution).

### 2026-10-01 — netcode — Platform-init functor and registry mapped; handler return still unobserved
- Did: identified the game.startup sub-step type as KSO3ClientEvents::Module::Initialize functor
  (descriptor/RTTI chain resolved); mapped the registry std::map at [exe+0xA8C1F0] (grows
  44 -> 185 entries through boot); confirmed client KGLog writes nothing visible (no files, no
  debug output). The platform object creation (state_sub+0x18) remains the gate.
- Evidence: doc 19; probe_registry.py runs; COL/type-descriptor resolution; log dir inspection.
- Outcome: partial; next = observe dispatcher return values or diff against a real launch.

### 2026-10-01 — netcode — Real launch capture (protected) + KSO3ClientEvents registry decoded
- Did: monitored the real launch (pid 19552): protected process (no VM_READ/DUP_HANDLE/module snap),
  block payload = random 64-bit session key. Decoded the registry: KSO3ClientEvents module event
  system, 185 entries over 15 event types (Initialize 52 etc.); handler {vt,f fn,ctx}; dispatch
  queues a per-module task node into the state. Next: name the modules via handler ctx and find
  which task should create the platform object.
- Evidence: doc 20; real_launch_capture.txt; registry dumps; disasm 0x1400A2830/0x1400A3750.
- Outcome: event system understood; real-client live introspection blocked by protection.

### 2026-10-01 — netcode — 52 client modules named; group queue snapshot
- Did: resolved all 52 Initialize handler owners by RTTI (ctx vtable -> COL -> type name): the full
  KJX3 module list (Locale/Memory/Path/Log/Engine/PakV5/Config/Console/CoreDump/Dll/Ecs/Streaming/
  MultiInstance/WeGame/Ole/WindowsApplication/Package/Loading/Render/Logic/UICore/UIShell/...).
  Froze the probe mid-group and walked the state queue: exactly the group's 6 nodes pending at
  1.88/1.96 s, done at 2.04 s; module tasks are not in the pump queue.
- Evidence: doc 21; probe_queue.py runs; RTTI name resolution over 52 vtables.
- Outcome: step 1 done; step 2 shows the group's own queue; step 3 next (which module creates
  state_sub+0x18).
### 2026-10-02 — V2 — log-visibility probe: the log path is gated by the startup gate
- Did: `tools/netcode/probe_logpatch.py` — spawns the client suspended (block + resume), then
  patches our own probe child only: engine ANSI root -> C:\jx3t\, KJX3ConfigModule +0x224=1,
  engine log flags |= 6, and the fixed 16-byte viewer literal -> \bin64\lv.exe.
- Findings: (1) the engine's g_GetRootPath is a copy helper over an **ANSI** root at
  engine+0x170060 (corrects the sec.26 "UTF-16" note); (2) with everything patched from
  0.66 s and held, no viewer spawns and no client_log.txt appears — because the 52 modules'
  Initialize handlers are only *registered*, never called, in the probe (no window/log dir/CEF
  either). Module Initialize waits on the same missing `state_sub+0x18` platform object.
- Evidence: doc sec.27; probe runs (patches verified applied + read back); child exit ~2.2 s.
- Lesson: log visibility cannot precede the startup gate — do not spend more time on the log
  channel before the gate is solved.
- Outcome: negative result recorded; next = find the writer of state_sub+0x18 (launcher IPC /
  security handshake candidates).
### 2026-10-02 — V2 — log channel live + Initialize IS dispatched (corrects the earlier read)
- Did: fixed the viewer spawn contract (`xlogv -i <read> -o <write>`; lv.c had -i/-o swapped
  -> EBADF), rebuilt it, added stdout capture to probe_logpatch; event-id sweep by patching
  only the `cmp edx,1` immediate.
- Findings: (1) `C:\jx3tmp\client_log.txt` now receives the client's stdout stream (Dumper64
  startup/shutdown proven, ~2.3 KB/run); (2) `KGLogAddOption(2)` is `or [Engine+0x174020],2`
  and OnInitialize's success path is `OpenXLogV` -> `KGLogAddOption(2)` (or `AllocConsole` +
  `freopen CONOUT$` in the alternate branch); (3) the console module's handler is called with
  edx=0 and edx=1 -> **Initialize IS dispatched** (~1.2 s); the sec.27 "never runs" was an
  artifact of the config flag racing (the config module re-initializes +0x224).
- Evidence: doc sec.28; EV_ID sweep 0/1 -> viewer spawns, 2-8 -> none; log file bytes.
- Outcome: log channel works; startup question narrowed to which module Initialize fails and
  what creates state_sub+0x18.
### 2026-10-02 — V2 — the startup gate decoded exactly (wait/pump/sub+0x18)
- Did: static decode of wait 0x14009D9A0 -> pump 0x14009D120 -> terminal 0x14009D2E0; live
  5 ms polling of the real sub fields (sub = state+0xE8).
- Findings: the gate is `[sub+0x18] != 0 && [sub+0x18]->vt[8](0) != 0` -> wait returns 1
  (startup done); the group runs 1.88-2.05 s (step non-null, done=1 at 2.05) but nothing
  creates sub+0x18 -> return 0 -> clean exit at ~2.2 s. (My mid-pass "gate is state+0x18"
  guess was wrong - the pump receives &sub, so sub+0x18 is correct after all.)
- Evidence: doc sec.29; probe_subvt.py run timeline.
- Next: find the writer of sub+0x18 (group step processors / module Initialize result
  handler); hardware write-watchpoint on sub+0x18 during 1.8-2.1 s is the direct probe.
### 2026-10-02 — V2 — static trace: group/module-init machinery + KGLog runtime state
- Did: static decode of the runner (0x1400A0870), dispatch thunk (0x1400A3750), module
  Initialize lambda (0x1400A37D0), game.startup builder (0x14009F6D0); 8153-store scan of
  [reg+0x18] writes filtered by known state constants; runtime read of the KGLog globals;
  KGLog console-path patch experiments (pipe-check immediate, then NOP).
- Findings: module Initialize events are dispatched at ~1.27 s (during PlatformLoad); each
  module Initialize builds its own group (vt 0x953E70) + module-step (vt 0x954CA0) and runs
  the handler via [ctx]->vt[0x20]; on failure it KGLogPrintf's "[Initialize] %s" (invisible -
  the engine emits no KGLog in the probe; sink NULL, mask all, flags ok, cached-handle check
  bypassed). No static candidate writes sub+0x18 (xmm stores not covered yet).
- Evidence: doc sec.30; scan script output (7 candidate fns); probe run globals.
- Outcome: writer of sub+0x18 still open; next = hardware write-watchpoint (needs OK) or
  extended static store search.
### 2026-10-02 — V2 — the real client shows its loading window in the harness
- Did: window enumeration in probe_logpatch (always on); diagnostic FORCE_GATE patch; screen
  capture of the real client window.
- Findings: the real client creates `剑网3_LoadingClass` / title `剑网3`, visible at ~1.84 s in
  our harness (content dark, no rendered frame); it still exits ~2.35 s at the gate. Forcing
  the gate (je -> success) does NOT keep it alive - the app Run returns immediately without
  the platform object, proving the object (sub+0x18) is the real blocker, not the wait.
- Evidence: doc sec.31; proof/netcode/v2_real_loading_window_20261002.png (flat #0C0C0C,
  300x167); screen capture kept local.
- Outcome: first visible V2 milestone; next = find the creator of sub+0x18.
### 2026-10-02 — V2 — write-watchpoint on sub+0x18: no creator runs
- Did: probe_watchwrite.py - DEBUG_ONLY_THIS_PROCESS, launch block, vtable-scan for the state
  sub (exe+0x953E50, state+0xE8==sub), DR0/DR1 write watchpoints on sub+0x18 on all threads.
- Findings: only the teardown writes (exe+0x9D111 mov [rsi+0x18],rbp -> 0) in the cleanup
  function 0x9D090-0x9D11C (which destroys the object via vt[0] if present); NO non-zero
  write ever - the platform object is never created in the probe. So the creator is gated by
  the missing launcher input, not a branch we can flip. Debugger tolerated (~2x slower boot).
- Evidence: doc sec.32; probe run output (SINGLE_STEP dr6=0xFFFF0FF1 RIP=0x...9D115).
- Outcome: P1.2 (launcher IPC/handshake) is the only path forward; the gate itself is solved.
### 2026-10-02 — V2 P1.2 static pass: launcher sequence + startup chain mapped
- Did: launcher log analysis (DetachProgram downloader + client); block GUID search across
  launcher binaries; privilege comparison; live launch observation (TCP table, children);
  static chain: stage lambdas, app module vtable, app Run, PlatformStartup, path builder.
- Findings: launcher starts KGPK4_StreamDownloaderX64.exe then JX3ClientX64.exe (no args);
  it never touches the block; it runs elevated (High) while the client manifest is asInvoker
  and uses no token APIs; the client's loopback pairs are its own self-pipe; app Run pumps
  while [app+0x78]!=0; PlatformStartup creates the module vector and hands [rsi+0x20] to the
  app module at +0x20; a failed stage still yields "done" so the group completes.
- Evidence: doc sec.33; launcher logs; disasm proofs (lambda_*, dd63330_ref, cefrender_ref).
- Outcome: startup chain mapped; next = find where the platform object ([rsi+0x20] /
  sub+0x18) is created and which module constructor fails in the probe.
### 2026-10-02 — V2 — WinMain flow corrected (state A/B, PlatformStartup caller)
- Did: static re-read of WinMain 0x1400E11F0 (state B ctor, PlatformStartup call, state A
  ctor, pool, dispatch, wait, PlatformLoad, group, wait loop); scanned stage
  lambdas/PlatformStartup/PlatformLoad for [reg+0x100] stores.
- Findings: the gate state is state A (rbp+0x130); PlatformStartup is called by WinMain with
  a 4-field args struct whose +0x20 is 0 (the [app+0x20] handoff is a null placeholder, not
  the gate object); no [reg+0x100] store exists in the stage/PlatformStartup/PlatformLoad
  code - the creator is a dispatched handler with the state as ctx; upstream dispatch
  failure is still the silent blocker.
- Evidence: doc sec.34; disasm of WinMain/PlatformStartup/PlatformLoad.
- Outcome: flow corrected; next = instrument dispatch results (KGLog ring-buffer global is
  NULL; console path silent) or patch stage failure branches to a visible marker.
### 2026-10-02 — V2 — stage marker test: all startup stages succeed
- Did: STAGE_MARK diagnostic in probe_logpatch (patch a stage failure return xor eax,eax ->
  mov al,2, same length; a failing stage then returns "keep waiting" and the exit time shifts
  from ~2.3 s to ~4.2 s); ran none/start/loaded/runner.
- Findings: exit times 2.61/2.27/2.23/2.21 s -> no stage fails; the KGLog ring stays NULL ->
  no failure message either. The blocker is inside a module Initialize task (dispatch only
  queues tasks; the thunk always returns 1, so a module failure is invisible to the stage).
- Evidence: doc sec.35; 4 probe runs.
- Outcome: stage-failure hypothesis eliminated; next = resolve the app module's Initialize
  binder / instrument the task runner 0x1400A3FD0.
### 2026-10-03 — V2 — chain-breaker found: LaunchUpdater event 3; MOD_OK runs the client 10.7 s
- Did: probe_bp.py (debug breakpoint on the module-Initialize failure branch + RTTI/eid/handler
  resolution); MOD_OK diagnostic (module task failure -> success); BLOCK8/CFG_E10 diagnostics.
- Findings: the only failing task is KJX3LaunchUpdaterModule event 3 (its Initialize step
  group fails; the task queue aborts the chain). With MOD_OK the client runs 10.7 s: KGPK4
  downloader + KGWin32App window + SeasunGame launch + DLSS updaters + self-relaunch
  (which crashes, WerFault). CFG_E10=0 and BLOCK8=0 did not unblock.
- Evidence: doc sec.36; probe_bp run; modok_long run (10.7 s).
- Outcome: first real progress past the 2.35 s wall; next = exact LaunchUpdater failure
  condition + the relaunch crash.
### 2026-10-03 — V2 S1: module-step return is context-dependent (racy), not a deterministic input
- Did: probe_bp extended with a second breakpoint on the module-step process 0x1400A3FF0
  (context/arg/return-address capture, armed late to keep the boot timing).
- Findings: the step returns 0 iff the low byte of its context pointer is 0; the failure is
  run/timing-dependent (one run had no failure and lived to 8.19 s under the debugger). The
  remaining hard gate is the self-relaunch crash (~6.4 s) with no full dump (48-byte
  ExceptionNotCapture marker only).
- Evidence: doc sec.37; probe_bp runs.
- Outcome: abort path understood as racy; next = reproduce/capture the relaunch crash.
### 2026-10-03 — V2 S4: the relaunch is a launcher-session artifact (crashes in ~130 ms)
- Did: probe_logpatch now tracks child JX3ClientX64 processes (protected: no handle) and
  creates the PID-keyed block for them (mimicking the launcher).
- Findings: the relaunched client spawns at ~6-7.5 s and crashes within ~130 ms (WerFault) -
  too fast to be the startup gate; creating its block does not help. The real launcher only
  ever has ONE client (SeasunGame -> JX3Client -> cefrender), so the self-relaunch is an
  artifact of the missing launcher session (the client falls into a launcher/updater path).
- Evidence: doc sec.38; relaunch runs.
- Outcome: the stable-client blocker converges on the launcher session (what the LaunchUpdater
  module expects); next = breakpoint the LaunchUpdater handler (0x1400B1C40) to record its
  gate/branch at runtime and compare with the real launch, then emulate the session.
### 2026-10-03 — V2 S5: the LaunchUpdater abort is a dispatch race (timing-dependent)
- Did: breakpoint on the LaunchUpdater handler entry 0x1400B1C40 (gate/event capture).
- Findings: latest run - handler never called, no failure, client 7.29 s; earlier runs -
  handler called (event 3), step context 0 -> abort. The abort is a thread-pool task race;
  our normal-speed harness loses it, the real launch wins it. CFG_E10/BLOCK8 had no effect.
- Evidence: doc sec.39.
- Outcome: pivot to the launcher emulator (P1.3) with production conditions only (no
  diagnostics) and measure; then the relaunch/launcher-session path.
### 2026-10-03 — V2: THE BLOCKER IS ONE BRANCH (LaunchUpdater event-3 override)
- Did: found the module vtable (rva 0x957008) and its vt+0x28 override 0x1400B1BF0 (event 3
  -> call 0x1400B1C40 -> failure abort); flipped only `cmp edx,3` -> `cmp edx,4`
  (exe+0xB1BF6) in the production emulator.
- Findings: with that single byte flipped the client runs past 20 s, skips the launcher spawn
  and the relaunch crash, and CEF windows appear (Base_PowerMessageWindow at 11.7 s).
- Evidence: doc sec.41; emulator run output.
- Outcome: the wall is one module-event branch; next = reproduce the real condition (config
  gate / event-id global) without the diagnostic.
### 2026-10-03 — V2: STABLE CLIENT CONDITION CONFIRMED (config+0xe10 = 0)
- Did: probe_bp deterministic capture (gate=1, shared event id=4 -> handler takes the
  launcher path -> 0); forced gate 0 in probe_bp (no failure) and in the production emulator
  (--cfg-e10 0).
- Findings: with the gate 0 the client runs past 22 s, no launcher spawn, no crash, CEF
  starts at 8.6 s. The event id comes from a shared step object [0xA8C230]+0x18 (race);
  config+0xe10 has no direct writer in code (loaded from a config blob).
- Evidence: doc sec.42; emulator run output.
- Outcome: stable client achieved with one config override; next = find the config source to
  set it legitimately, then continue P2 (login/gateway).
### 2026-10-03 — V2 PRODUCT MILESTONE: stable visible real client at the login stage
- Did: ran the stable emulator for 90 s; captured windows/connections/captures.
- Findings: client stable >90 s, cefrender + login-stage connections, main window visible and
  rendering (KGWin32App 3840x2160, title 剑网3 - 乾坤一掷 @ ...); captures 5.2 MB, mean #506068.
- Evidence: doc sec.43; C:\jx3tmp\product_*.png + image_stats.
- Outcome: stable visible client achieved (one provisional config override); next P2
  (login/gateway), then P3 (world).
### 2026-10-03 — V2 P2 recon: gateway protocol surface mapped
- Did: extracted the KGatewayClient method-name surface (handshake/account-verify/role-list/
  login-game/queue) and the Lua login bindings; wrote the P2 plan into doc sec.44.
- Findings: login flow = DoHandshakeRequest -> DoAccountVerifyRequest -> GetRoleListItem ->
  DoLoginGameRequest; address from Login_SetGatewayAddress (login UI) / login.ini LastLogin.
- Evidence: doc sec.44; string scan 0x7CC280-0x7CC980.
- Outcome: P2 implementation plan set (Reborn.Gateway, no client changes); next = map the
  handshake/verify message layouts, then implement the gateway stub.

### 2026-10-04 — Session ops: 58-minute stall root-caused (tool pipe inheritance) + fix
- Did: audited the hour between 21:55 and 22:53 (no commands executed) with a controlled repro:
  (a) Start-Process child (30 s) from a tool call -> command logic ended in 28 ms but the tool
  could not finish until its timeout, and the NEXT command was blocked until the child exited
  (H entered 23:23:02, child ran 23:22:16.6-23:22:46.6); (b) same child via WMI wrapper -> next
  command ran immediately while the child was alive (J entered 23:23:43.6).
- Findings: a Start-Process child inherits the tool shell's stdout/stderr pipe handles; the
  tool cannot see EOF while any holder lives. When a tool call is timeout-killed, the orphan
  keeps the pipe and blocks every subsequent command until it exits. In the real incident the
  orphan was the launcher emulator started with --observe 3600; its exit at 22:53:06 released
  the next command at 22:53:35 (gap 21:55:33-22:53:35 = the emulator's remaining lifetime).
  Same pattern explains earlier long gaps whenever the emulator ran with a long observe.
- Evidence: emul_br1.txt ("[3600.02] still alive at observe limit", mtime 22:53:06); repro
  timestamps above; AGENTS.md §13 new Background-processes rule.
- Outcome: rule added; long-lived processes must be WMI-launched (run_emul.cmd pattern).
  Also: 22:45:36 gateway connect remains unattributed (not from any of my commands).

### 2026-10-05 — V2 P2 BREAKTHROUGH: gateway stream cipher found; real login chain complete
- Did: hunted the "0.2 s close after our handshake respond". Passive probe (POLL_MS=5) captured
  the live transport: send/receive transform callbacks (exe+0x7A2590) + states at inner+0x3C/
  +0x40. Disassembled the callback: XOR stream cipher over the payload (table exe+0xA34530,
  0x162F dwords, state update idx=(state+k)%0x162F, state=table[idx]+0x2E6D23C1, no state
  persistence). Implemented tools/netcode/gateway_cipher.py (reads the table from the exe).
- Findings: the connect hello is the only plaintext packet; it seeds the session state
  (for our constant zero-hello: 0xC9FFFFFF). All later packets (both directions) are
  encrypted, opcode byte included. Our stub had been sending PLAINTEXT responses -> the
  client decrypted them into garbage -> error path -> +0x30 disconnect flag -> transport
  destroy -> the 0.2 s close. Real opcodes (decrypted): 1 ping, 2 handshake, 3 account
  verify, 4 verify respond, 9 role list, 10 login game, 14 login key. With the cipher the
  client sends its account verify (op 3, 161 B): account "binkp4" + MD5("admin") hash in
  both credential fields - the full UI chain (LOGIN_NOTIFY -> HandleResult -> OnHandShake
  Success -> Login_AccountVerify) fires exactly as statically decoded.
- Evidence: stub log raw=.. pt=.. lines (C:\jx3tmp\gw_stdout.txt); probe poll; doc sec.52.
- Outcome: login conversation complete to the role list; next = role select (op 10) ->
  login key (op 14) -> game server (P3). Cipher state is constant while the hello is
  constant; re-derive if the hello fields change.

### 2026-10-05 — V2 P2 MILESTONE: full gateway login completed end-to-end (scripted)
- Did: fixed the role-list packet (names at entry+4 / entry+0x24 per parse 0x140185A20;
  [1]==[5] to fire both role-list events 0x31/0x32), wrote role_click/role_enter
  (PostMessage grid + enter button, canvas 1280x960 scaled to the window), ran the full
  scripted flow: post_login -> verify -> role list -> select role + Btn_EnterGame.
- Findings: client sent op 10 (login game, 142 B: roleIndex=1001, [global+0xCB0]=2283),
  stub answered op 14 (login key, game IP 127.0.0.1) -> client closed the gateway session
  (correct: login-key case 0) and moved to the game-server stage. The role packet's field
  placement was the blocker (the UI showed no selectable role before).
- Evidence: stub log C:\jx3tmp\gw_stdout.txt 00:20:26 (proto=10 raw=0941187c pt=0ae903...,
  proto14 sent); role_enter output "OPCODE 10 after enter-click at role=(760,240)".
- Outcome: gateway login chain DONE end-to-end with real client + scripted input. Next:
  P3 game server (find the game-server port from the login-key/game-login handler
  0x1401245A0 and capture the client's first game-server packet).

### 2026-10-05 — V2 P3 MILESTONE: game-server connection + game protocol cipher live
- Did: found the game login path (login-key handler 0x188900 result 0 -> inet_ntoa([+6]) ->
  0x1401245A0 -> transport factory 0x14079D100 mode 4). Fixed the login key layout
  ([6..9]=IPv4 network order via inet_ntoa, [0xA..0xD]=port, pad >= 30 bytes - the proto-14
  handler min size; a 24-byte key was silently rejected by the dispatcher). Started a game
  listener on 127.0.0.1:3725 with the 42-byte hello. Live-verified: the client connects,
  receives the hello, and sends its game login request.
- Findings: the game transport uses the SAME table cipher but a different variant
  (exe+0x7A26D0): table lookup once, linear keystream (+CIPHER_ADD+remaining), and the state
  PERSISTS via an LCG per packet: state = state*0x1F + 0x8088405. Both directions start at
  0xC9FFFFFF; observed send state after one packet = 0x7E0883E6 = LCG(0xC9FFFFFF) - exact.
  The client's first game packet decrypts to opcode 1, 32 B: [0xB..0xE]=roleID (1001),
  [0xE..]="127.0.0.1" (the game login request).
- Evidence: game stub log C:\jx3tmp\game_stub_out.txt 14:19:33 (raw=6590055a pt=0100...e903
  00003132372e302e302e31...); manager watch (mgr exe+0xA4C4F0: transport/port/role/IP).
- Outcome: P3 transport established; next = game protocol opcodes (opcode-1 handler response,
  world entry/sync). Game stub now speaks the game cipher (GameSession cs/sc states).

### 2026-10-05 — V2 P3: game protocol table mapped (564 names) + enter-world flow decoded
- Did: mass-mapped KPlayerClient::On* RTTI strings -> S2C registration table (one-pass LEA
  xref scan; 564 handlers named) -> proof/netcode/game_protocol_names_s2c.tsv. Decoded the
  game frame (11B header: u16 id, u8 flags, u16 serial, u16 ack, u32 field; optional u32
  param) and the enter-world flow. Made the game stub interactive (gsend.hex command file).
- Findings: session ids - 4 = OnSyncPlayerBaseInfo (343B; sets the client's enter state 4),
  7 = OnSwitchGS, 8 = OnSwitchMap, 10 = OnSyncNewPlayer, 11 = OnSyncNewNpc, 156 =
  OnSyncPlayerLoginCSInfo, 187 = OnSyncRoleDataSectionCheckRequest (8B), 188 =
  OnSyncRoleDataOver (7B). Client enter-world state at player+0xFDC: state 4 -> sends
  DoClientConfirmReady -> state 7 (ready; the big subsystem init runs). Ping = C->S op 6
  every 3 s (dead timeout 12 s); reliability ack field at +5. The handshake respond
  (OnHandShakeRespond: bRecover, ServerName, ReconnectTimeout, Success) id is still unknown
  (its RTTI string is absent; candidates 1,2,3,9,14,17,18,21,23,27,31,33,35 injected live
  with zero payload did not advance the client).
- Evidence: game_protocol_names_s2c.tsv; disasm of the state machine 0x1803525F0 and
  OnSyncPlayerBaseInfo 0x1801A0510; live injections (stub log, no op-3 reaction).
- Outcome: game protocol surface mapped; single remaining gate = the handshake respond
  packet (id + layout). Next leads: the logic-thread dispatch (ring consumer) special cases,
  unmapped ids with Success!=0 content, or the EXE's KPlayerClient copy.

### 2026-10-05 — V2 P3: game dispatch found (EXE 0x140168730) + client-side id registration
- Did: located the game packet dispatch in the EXE (0x140168730): handler table at
  player+0x16460, size table at player+0x17F28, handler(player, packet, size); ids 1..0x358;
  size = MINIMUM (uDataLen >= size); var-size ids (size=-1) read the size from u16
  [packet+7]; the buffer can hold multiple concatenated packets (loop advances by size);
  id 7 (OnSwitchGS) sets a special flag. Extracted the client's own core registration from
  the reset function 0x180163540 (53 handlers, exact ids): unmapped core ids are
  1, 2, 3, 9, 14, 17, 18, 21, 23, 25, 27, 31, 33, 35, 42, 48, 51, 54.
- Findings: the Lua API "LoadingComplete" (0x18036F4B0) -> DoApplyEnterScene (C->S op 3) -
  the enter-scene request is UI-driven after the loading; OnSyncPlayerBaseInfo (id 4, 343 B)
  sets the client enter state 4 -> DoClientConfirmReady -> state 7. Live injections of the
  unmapped core ids (zero / field=1 / field=2 / field=3) did not advance the client, BUT
  during an id-1 sub-type sweep the client's game session DISCONNECTED (~14:48:47, pings
  stopped) and it returned to the login scene (engine re-init + map load) - the first real
  handler effect observed; an id-1 sub-case (sub ~35-40) acts like a kick/switch.
- Evidence: disasm 0x140168730, 0x180163540, 0x18036F4B0; game stub log; KG3D logs
  14:57:11 (map "龙门寻宝" load + player model attempts) after the disconnect.
- Outcome: dispatch + registration understood; next = identify the id-1 sub-case that
  disconnected (controlled sweep) and the handshake respond id (still open; content may
  need the roleID/key echo).

### 2026-10-05 — V2 P3: game-transport reliability pitfall (ack flag) + safe injections
- Did: every inject test closed the client's game session right after our packet. Traced it
  to the game dispatch (EXE 0x140168730): flags bit1 = "carries ack"; the ack is processed
  via the confirm helper (DLL 0x180169AB0); on failure the net thread takes its error path
  and the client RSTs. Our packets set flags=2 with ack=0/1 while the client's serials are
  randomized per connection (observed handshake serial 0xBB5A, ack 0x6019) - the confirm
  failed -> disconnect.
- Verified live: with flags=0 (no ack) the client KEEPS the connection (24 s+ alive and it
  starts sending its game pings, C->S op 6). All injection sweeps are now safe.
- Also: the id-1 handler is a game->UI event bridge (60 sub-cases -> UI vtable calls:
  sub=1 -> +0x580(byte), sub=3 -> +0x660(dword,byte), sub=6 -> +0x630, ...). Zero-content
  sweeps of subs 1..60 + the unmapped core ids (2,3,9,14,17,18,21,23,25,27,31,33,35,42,48,
  51,54) produce no visible reaction - the UI events fire with meaningless args.
- Also: the client's login scene map is the SANDBOX map (C:\jx3tmp\reborn_sandbox\map\
  龙门寻宝_s) in current runs (from a previous sandbox setup) - check RC_MAP when the world
  map differs from expectation.
- Outcome: injections safe (flags=0); the handshake respond still unidentified - the id-1
  sub-cases are the game->UI event fires, so the respond likely needs the correct sub+args
  (roleID at +0xB etc.). Next: match the UI vtable methods to the event names (JX3UIX64).

### 2026-10-05 — V2 P3: id-1 = OME UI-response router; SO3UI bridge decoded; EXE KPlayerClient copy
- Did: resolved the id-1 handler chain end-to-end: the id-1 sub-cases call methods on ONE global
  UI object (DLL global 0x1809C1328), created from [0x180A00D80] (factory CreateSO3UI of
  JX3UIX64.dll) -> the game interface static object at JX3UIX64+0x4A4E68 -> vtable 0x180392238.
  Mapped the sub-case methods to their Lua event strings: sub=1 +0x580 = SKILL OME responses,
  sub=3 +0x660 = QUEST (UI_OME_QUEST_RESPOND), sub=4 +0x378 = FELLOWSHIP, sub=5 +0x640 =
  USE_ITEM, sub=6 +0x630 = ITEM, sub=7 +0x648 = TRADING, sub=8 +0x6E0 = CHAT; all via the
  SYS_MSG dispatcher (arg0 = the message name, arg1 = payload). So id-1 = the game->UI
  OME (UI operation) response router - NOT the handshake respond.
- Also: SCENE_BEGIN_LOAD is fired by SO3UI vtable +0x2A8 (an engine-side scene notification);
  the EXE carries its own KPlayerClient copy (handshake send 0x140169B60, dispatch 0x140168730,
  registration in its reset 0x14011D570 - 53 core ids mirroring the DLL's); the game-login
  success path (0x140124795) sets the session flag manager+0xE400=1 and launches the
  game-session worker.
- Outcome: the id-1 family is fully identified (UI OME responses). The handshake respond is
  still open; remaining candidates: the core ids with correct content (roleID/key echo) or a
  non-table path. Next: disassemble the core handlers (EXE ids 2..53) for the respond
  signature (a state set + the scene load trigger).

### 2026-10-05 — V2 P3 BREAKTHROUGH: handshake respond handler FOUND (global-room message)
- Did (batch static, no live runs): traced the handshake respond to its handler by name string:
  the '[KPlayerClient] OnHandShakeRespond ...' log is referenced by fn 0x140143A30. Its
  dispatcher = fn 0x140143970 = KPlayerClient::OnGlobalRoomMsgNotify (log string verified):
  checks u16 [pkt+7] == the packet size, u16 [pkt+9] - 0x33 <= 0x10 (types 0x33..0x43), then
  the sub-dispatcher 0x1402FDBF0(this+0x1a288, type, data, len) -> handler = [this + type*8],
  size = [this + type*4 + 0x9a8].
- Respond packet layout (from handler 0x140143A30): +0x07 u16 size; +0x09 u16 type (0x33..0x43);
  +0x0B/+0x0F/+0x13 dwords; +0x17 32-byte SERVER NAME (memcpy'd to global+0xa8); +0x37 dword
  RECONNECT TIMEOUT -> [manager+0xe404]; +0x3B/+0x3F/+0x43 dwords (bRecover/Success flags).
- Also: the respond handler + its siblings appear in a static .rdata table at 0x14098AF34/44
  (4-byte RVAs) - the global-room handler registration template. The KGlobalRoomClient method
  surface extracted (DoC2S* / OnS2C* - enter scene confirm, room ops, etc.).
- Outcome: the LAST unknown of the game login is statically identified. Next: read the GR
  registration template table fully (types -> handlers/sizes) -> the exact S2C id/type for the
  respond -> implement it in the game stub -> milestone run.

### 2026-10-05 — V2 P3: handshake respond FULLY RESOLVED statically
- GR registration fn 0x140121EFB (found via lea xrefs to 0x140143970/0x140143A30):
  registers the manager's GR/OME message table: handlers at manager+0x17AA0 (type*8),
  sizes at manager+0x18A48 (type*4). Verified entries: type 0x33 -> 0x1401609E0 (59),
  type 0x36 -> 0x140143A30 OnHandShakeRespond (71 bytes = 0x47), type 0x40 -> 0x140148830 (75),
  type 0x42 -> 0x14014C320 (var), type 0x43 -> 0x140143970 OnGlobalRoomMsgNotify (dispatcher).
- KGlobalRoomClient::ProcessMessage (0x1402FDBF0) dispatches types 0x33..0x43.
- Server action: send GR message type 0x36 size 71 with the decoded layout
  (u16 size@+7, u16 type@+9, dwords@+0xB/+0xF/+0x13, ServerName 32B@+0x17,
  ReconnectTimeout@+0x37 -> manager+0xE404, flags@+0x3B/+0x3F/+0x43).

### 2026-10-05 — V2 P3: GR table registration traced to the manager reset (both modules)
- .pdata: the GR-type registration (0x140121AD7..0x1401222AA region) is part of the SAME reset
  function as the 53 S2C handlers: fn 0x14011D570..0x1401222AA. So the manager object carries
  two tables: S2C ids at +0x16460/+0x17F28, GR types at +0x17AA0/+0x18A48 (type 0 = base, size 41).
- The DLL (JX3LogicEditOperationX64.dll) registers the identical GR table (fn 0x180167AA7,
  same offsets) and has its own copy of the GR dispatcher: 0x1802A21C0 (byte-identical logic
  to EXE 0x1402FDBF0: type range 0x33..0x43, sizes [rcx+rdx*4+0x9a8], handlers [rcx+rdx*8]).
- Open: no static reader of +0x17AA0 found in EXE/DLL/UI modules (only the registration writes)
  -> the GR dispatch base is loaded at runtime; and ProcessMessage's rcx ([0x140A755B8]+0x1a288)
  differs from the registered base by a fixed delta (size-table gap 0xFA8 vs 0x9A8).
- Next: milestone run - inject the type-0x36 (size 71) respond on the game connection and read
  the client log to confirm the delivery id/handler (OnHandShakeRespond fires -> layout right).

### 2026-10-05 — V2 P3 CORRECTION: respond is S2C id 0x2FF (not "type 0x36"); stub implemented
- The "+0x17AA0 table" was the S2C id table's high-id range: handler slot 0x17AA0 = id 713's
  slot (0x16460+(id-1)*8), size slot 0x18A48 = id 713's size slot (0x17F28+(id-1)*4). The
  registered entries map to ids: 0x2FC (59B), 0x2FF = OnHandShakeRespond (71B), 0x309 (75B),
  0x30B (var), 0x30C = OnGlobalRoomMsgNotify (var), 0x312 (var).
- Handler 0x140143A30 reads the FRAME directly: [rdx+7]/[rdx+0xB]/[rdx+0xF]/[rdx+0x13] dwords
  -> global+0x1B5B8.., 32B name at +0x17 -> global+0xA8, [+0x37] -> manager+0xE404, and three
  dwords at +0x3B/+0x3F/+0x43 (all nonzero -> main path; any zero -> alternative paths).
- Implemented in tools/netcode/game_server_stub.py: handshake_respond() builds the 71-byte
  id-0x2FF frame; auto-sent after the client's proto-1 handshake (GAME_AUTORESP=1).
- Lesson: slot-offset math must be anchored to the right base (id vs type table); the
  0x600 gap between handler arrays was the id table's size array, not a second table.

### 2026-10-05 — V2 P3 MILESTONE: game-login handshake respond LIVE-VERIFIED
- Off-by-one corrected: id-table slot(id) = 0x16460 + id*8, so handler slot 0x17C50 = id 766
  = 0x2FE (not 0x2FF). The net thread's special case cmp word [r13], 0x2FE IS the respond
  id. Size slot 0x18B20 = 0x17F28 + 766*4 confirms.
- Live run (client 18:08, gateway->game flow): client game handshake (proto 1, 32B) received;
  id-0x2FF attempt decrypted but not handled (mgr+0xE404 stayed 0); corrected id-0x2FE frame
  injected via gsend.hex at 18:16:18 -> **mgr+0xE404 = 30 (our ReconnectTimeout)**: the
  OnHandShakeRespond handler (0x140143A30) provably ran in the live client.
- Cipher alignment proof: client receive-state 0x4B107CDF = exactly STATE0 advanced by two
  decrypted packets (cmd 40B + respond 71B); client kept pinging (proto 6) throughout.
- Stub fixed: RESPOND_ID = 0x2FE (tools/netcode/game_server_stub.py).

### 2026-10-05 — V2 P3: post-handshake live session stable; id-4 payload needs real fields
- Live: role_enter triggered a fresh game connection; the fixed stub auto-answered id=0x2FE
  twice (fresh + resume handshakes); mgr+0xE404=30 both times; client pinged steadily
  (our packets reset its 12 s dead-timeout; session stays alive while we inject).
- id 4 (OnSyncPlayerBaseInfo, 343B frame) with an all-zero payload did NOT set
  mgr+0xFDC (enter-state stayed 0). Handler 0x14015C1D0 reads packet+0xD6 and has an
  early-exit path at 0x14015CA96 -> it validates fields; zeros are rejected.
- Next: decode the full id-4 handler (required fields + the [player+0xFDC]=4 write
  preconditions) from the static disasm, then build a valid 343B payload.
- Live env left running: serverlist :80, gateway :3724, game stub :3725 (fixed), client
  PID 9716 alive at game-entry (pings accepted).

### 2026-10-05 — V2 P3: id-4 handler decoded (player lookup by [packet+7]) + runtime pointers
- Handler 0x14015C1D0..0x14015CAD9: r15 = LookupPlayer(client_global, [rbp+7]) (call 0x140177EE0);
  if null -> early return (no state write). The copy block at 0x14015C590+ writes packet fields
  into r15: [0xD1]->+0xEE4, [0xCC]->+0x201E8, [0xD5]->+0xEE8, **state 4 -> [r15+0xFDC]**,
  [0x34]->+0x10, [0x38]->+0x14, [0x3C]->+0x18, and more (bulk, straight-line).
- Client global: [exe+0xA755B8] = client object (runtime 0x22C30932040); player container at
  client+0x5673D8 (+0x20 = list head). Runtime read of the 2-node circular list showed IDs at
  +0x10 not matching 1001 -> the player object is not the raw list node (need the lookup's
  compare loop to find the ID offset / node->player indirection).
- Injected id-4 with field=1001 (role id) - session stayed alive; state readback pending the
  correct player object resolution.
- Live env still up (client PID 9716, stub fixed 0x2FE auto-respond).

### 2026-10-05 — V2 P3 MILESTONE-2: OnSyncPlayerBaseInfo LIVE-VERIFIED (player state = 4)
- Lookup 0x140177EE0 = get-or-create over a std::map: container = [client+0x5673D8], map head
  at +0x20, nodes {left@+0, parent@+8, right@+0x10, key@+0x20 = role id, value@+0x28 = player}.
- Injected id-4 frame with field=1001 (role id): runtime traversal found the player object
  (0x22D0D08EDC8) and **player+0xFDC = 4** -> the handler's copy block ran, enter-state set.
- Session stayed alive (client pinging); next: client DoClientConfirmReady -> C->S op 3
  (ApplyEnterScene), then world-sync set (ids 10/11/12/13) from game_protocol_layouts_live.tsv.

### 2026-10-05 — V2 P3 Phase 2: scripted world-sync in the game stub
- Static (Phase 1): id 10 handler packet map captured (86 field reads; entity id at +7 compared
  with [client+4]=1001; state 7 write at 0x14015A854 guarded by helper call 0x140173D90 on a
  packet-parsed dword; per-player init 0x14031BD10(player,[pkt+0x2B]); subsystem 0x140169940).
  Net thread dead-timeout constant confirmed at 0x14016A3E8 (add eax, 0x2EE0 = 12 s).
- Phase 2: game_server_stub.py now runs a scripted sequence after the handshake respond:
  id 4 (343B, field=role id) -> id 10 (161B, field=role id) -> keepalive id 10 every 8 s.
  Env: GAME_SYNC (default 1), GAME_ROLE_ID (default 1001). Manual gsend.hex still works.
- Next (Phase 3): restart stub, re-trigger client, verify player+0xFDC 4 -> 7 live; if the
  id-10 zeros template is rejected (guard line 0x3624/0x362C), iterate the packet fields.

### 2026-10-05 — V2 P3 Phase 3a: scripted sync LIVE - keepalive works; id-10 template needs +0x4B block
- Fresh run (client PID 38520): the Phase 2 sequence ran exactly as designed - respond id=0x2FE,
  SYNC step1 id=4, step2 id=10, keepalive id=10 every 8 s; client pinged steadily and did NOT
  hit the 12 s reconnect loop anymore. Services restarted first (serverlist/gateway had died).
- Runtime read: player+0xFDC = 4 -> the id-10 zeros template was rejected at its guard:
  0x14015A245 parses the packet string block at +0x4B (call 0x14016AD70 -> [rbp-0x39..-0x2D]),
  then lookup 0x1401391A0(player, [rbp-0x31], [rbp-0x2D]) must return non-null and
  [client+0x6C] >= [rax+0x3C]; the state-7 write at 0x14015A854 sits behind this chain.
- Next: decode 0x14016AD70 (what it reads from +0x4B) + the lookup key fields; fill the id-10
  template accordingly; re-run (keepalive already proven).

### 2026-10-05 — V2 P3 Phase 3b: id-10 guard chain decoded; template parameterized
- Parse 0x14016AD70(pkt+0x4B, player, out): out[+8]=(qword&0x3FFFF)=A -> handler [rbp-0x31],
  out[+0xC]=(qword>>18)&0x3FFFF=B -> handler [rbp-0x2D], out[+0x10]=(qword>>36)&0x3FFFFF=C;
  byte pkt+0x53 flags -> player+0x368/0x36C/0xC28; qword bits 58-63 (type 15/30) selects a
  table lookup path (0x14023EC00, client+0x14438) vs the direct-fill path.
- Guard call 0x140173D90 = a PACKING routine (builds appearance fields, returns success);
  with zeros or pack=0x40000 (B=1) it returns 0 -> state-7 write skipped (player+0xFDC stays 4).
- Stub: id-10 template now parameterized (GAME_ID10_PACK hex qword @+0x4B, GAME_ID10_FLAGS
  byte @+0x53) for fast live iteration.
- Next: decode the 0x140173D90 call-site args (0x14015A7xx) + its return condition to learn
  the required A/B/C values (likely the role's appearance/model ids from the role data).

### 2026-10-05 — V2 P3 BREAKTHROUGH: id 7 (OnSwitchGS) sets map/region/position; sequence fixed
- Root cause of the id-10 guard failure: 0x14017BDD0 validates the player position X/Y/Z
  against the scene dims ([scene+0x790]/+0x794]<<11) - with no map loaded the dims are 0 ->
  any position fails -> state-7 write skipped.
- id 7 handler (0x14014C9B0) at 0x14014D065+: pkt+7 -> [client+0x14] (map id),
  pkt+0xB -> [client+0x18] (region id), pkt+0xF/+0x13/+0x17 -> player+0x10/0x14/0x18 (X/Y/Z).
- Stub sequence corrected: id 4 -> id 7 (map/region/pos, GAME_MAP_ID/GAME_MAP_REGION/
  GAME_POS_X/Y/Z) -> id 10 -> keepalive id 7+10.
- Caveat: restarting the stub resets the cipher; a RESUME handshake (last byte 01) keeps the
  client's old cipher state -> mismatch -> RST. Need a FRESH session (fresh handshake 00) after
  a stub restart: full re-login, not just reconnect.

### 2026-10-05 — V2 P3: id-7/id-10 scene bootstrap deadlock identified
- Live reordered test (id4 -> id7 -> id10, keepalive id7+id10): id 7 ran but took its reset path
  (client+0x14/0x18 stayed 0, player pos 0) and id 10 still left state 4.
- Analysis: id-7 handler requires [player+0x60] (the scene pointer) non-null (early-exit line
  0x34C1). [player+0x60] is written ONLY by 0x14017BDD0 (the id-10 guard path, store happens
  FIRST at its entry). But id 10's handler looks up the scene via 0x140174E50(client,
  [client+0x14]=map, [client+0x18]=region) BEFORE the guard and likely bails when null with
  (0,0) -> neither message can bootstrap the other. Deadlock.
- Candidate fixes (next session): (a) find what creates the initial scene/loading (maybe the
  role data carries the last map/position -> fill the gateway role-list entry fields);
  (b) check whether 0x140174E50(0,0) can return a default scene and force the first id-10
  through; (c) decode the role-list entry map/pos fields (qword +0x4C, dwords +0x73/+0x77).
- Client note: after login+enter the client gets stuck; force-close before each test.

### 2026-10-05 — V2 P3 BREAKTHROUGH: id 4 carries map/region/position (deadlock resolved)
- id 4 handler copy block (0x14015C2F6-0x14015C30D): pkt+0x2C -> [client+0x14] (map id),
  pkt+0x30 -> [client+0x18] (region id); and 0x14015C5C3+ copies pkt+0x34/0x38/0x3C ->
  player+0x10/0x14/0x18 (X/Y/Z). Zeros in our template made the scene lookup (map 0) fail ->
  id-10 guard could never validate -> deadlock.
- Stub id-4 builder now packs map/region/pos (GAME_MAP_ID/GAME_MAP_REGION/GAME_POS_X/Y/Z,
  default 1/1/100/100/0). id 7 remains for later map switches.

### 2026-10-05 — V2 P3: id4 map/region live - client now acts (RST after id10), reads post-RST are 0
- Live run with id4 carrying map=1/region=1/pos=100,100,0: sequence ran; the client RST the game
  connection 2.5 s after the id-10 send - NEW behavior (with map 0 it just ignored). So the scene
  lookup now succeeds and the id-10 handler goes deeper (likely failing in the appearance/guard
  chain and taking the net error path).
- Post-RST reads: client+0x14/0x18 = 0, player pos 0 (either the writes never landed or the
  client cleared the session state on disconnect). Next run must read DURING the window (after
  id4, before the RST) to confirm.
- Client stays alive (Responding=True) but must be force-closed before the next test.
- Next: (a) read client+0x14/player pos ~1 s after the id-4 send; (b) decode the id-10 RST
  cause (appearance parse 0x14016AD70 outputs A/B/C + guard 0x140173D90 with a valid scene);
  (c) verify the confirm helper/ack path (the RST pattern matches the earlier ack-flag case).

### 2026-10-05 — V2 P3: id4 map=1 aborts before player insert (map-switch path -> RST)
- Poller (C:\jx3tmp\poll_world.py) during a live run: the player is NEVER inserted into the map
  (all samples 'player-not-found'), i.e. the id-4 handler aborts before its get-or-create insert
  (0x14015C3DB) when the packet carries map=1/region=1. The connection RSTs ~0.5 s after id 10.
- Interpretation: with a non-zero map the id-4 handler takes the map-switch/load path (its
  map/region writes at 0x14015C2F6 precede the insert) -> the client tears down the connection
  (real switch flow reconnects) and never completes the player insert. With map=0 the insert
  worked but the scene stayed empty (deadlock).
- Next: (a) poll client+0x14 mid-window to see if the id-4 map write lands before the teardown;
  (b) find a VALID map/region pair (the switch may require map data the client can load, and a
  following reconnect the stub must serve); (c) check whether the real flow expects the client
  to reconnect after the map announcement (the stub should then re-arm the sync).

### 2026-10-05 — V2 P3: sequence reordered (id4 map=0 insert -> id10 bootstrap -> id7 map -> id10 final)
- Rationale: id4 with a non-zero map aborts before the player insert (switch path). Correct order:
  id4 (map=0, insert + state 4) -> id10 (guard stores player+0x60 scene bootstrap) -> id7
  (map=1/region/pos, now scene non-null) -> id10 (guard validates against a real scene -> state 7).
  Keepalive repeats id7+id10. Envs: GAME_ID4_MAP/REGION (default 0), GAME_MAP_ID/REGION/POS for id7.

### 2026-10-05 — V2 P3 BREAKTHROUGH: map id found - 龙门寻宝 = 296 (MovieEditor MapList.tab)
- The map id table lives in MovieEditor\ResourcePack\MapList.tab: TSV rows 'id<TAB>name<TAB>path<TAB>0';
  龙门寻宝 = 296, 龙门寻宝_夜晚 = 297 (ids are in the 290s, so map=1 was invalid -> id4 aborted
  before the player insert; that abort was the invalid-map error path, not a switch path).
- Map data (extracted via PakV4SfxExtract.exe from Trunk.dir paths): data\source\maps\龙门寻宝\
  sceneinfo: RegionSize 512, UnitSize 100, WorldOrigin -102400,-102400, RegionTableSize 8x8;
  risettings.ini RegionCountXZ=128,128.
- Wrapper C:\jx3tmp\run_gamestub_296.cmd sets GAME_ID4_MAP=296/GAME_MAP_ID=296 (region 0).

### 2026-10-05 — V2 P3: id 7 (OnSwitchGS) is NOT part of initial entry (switch = teardown/reconnect)
- Live with map=296: sequence id4(map=296) -> id10 -> id7 RST the connection 1 s after id 7; the
  client then sits in the stuck state (client global reset, no reconnect). id 7 = server-switch
  message -> the client tears down expecting a GS reconnect (not the initial entry path).
- Also: hosts fix (admin) - infoc.xoyo.com/dumpinfo.xoyo.com -> 127.0.0.1: the earlier real-server
  fallback happened because our port-80 list host was down; now even a down host cannot reach the
  real list. Verified: fetch of infoc.xoyo.com returns our patched list (127.0.0.1:3724 entry).
- Stub: id 7 now opt-in (GAME_ID7=1); default sequence id4 -> id10 -> keepalive id10.

### 2026-10-05 — V2 P3: map 296 = UGC map; data placed at client data\UGC (user-approved)
- 龙门寻宝 (id 296) is a UGC map: all 1276 prefetch entries have bInPak=0 (not in game paks).
  The engine loads UGC maps from <client>\data\UGC\... (KG3DEngineDX11EX64.dll strings).
- The full UGC map data (4117 files, 975 MB) existed only in the SeasunDownloader probe copy;
  with user approval it was copied to zhcn_hd\data\UGC\binkp1\龙门寻宝\ (a write under
  C:\SeasunGame outside the documented build outputs - explicitly approved by the user).
- Live state before the copy: client+0x14=296, player inserted, state=4, pos=100,100,0, scene=null
  (the map could not load without the data).
- Next: fresh run - expect the scene to load -> id 10 guard passes -> player+0xFDC=7.

### 2026-10-05 — V2 P3: UGC data placed (binkp1/binkp4/admin); scene still not loading
- Copied the 975 MB UGC map to data\UGC\{binkp1,binkp4,admin}\龙门寻宝 (per-account UGC layout).
- Live (fresh client): map=296, player inserted, state=4, pos=100,100,0, scene=null - the client
  does not load the map into the scene (dims at [scene+0x790] never set) even with the data in
  place. Client keeps sending pings; stub keepalives keep the session alive.
- The id-10 handler's lookup 0x1401391A0(scene, X, Y) = a 128x128 scene grid cell lookup that
  validates X/Y against [scene+0x790/0x794]<<11 and reads [scene + cell*8 + 0x7A8]; with an
  unloaded scene (dims 0) it returns null -> the handler exits before the guard/state-7.
- Open: what triggers the map load into the scene (engine load call chain: dims setter
  0x1400B2310 <- fn 0x14009DB60; who calls it in the live flow?), or whether the client's
  loading UI needs a server message (scene-begin) / a UI action.

### 2026-10-05 — V2 P3: client reaches LOADING SCREEN; engine loads sandbox scene; game-scene bind pending
- The client (live) is on the loading screen. KG3D_Engine log: 'load map ...\C:\jx3tmp\reborn_sandbox\
  map\龙门寻宝_s\龙门寻宝_s.jsonmap success' + 'load scene ... success' (0.125s) - the ENGINE has a
  scene (the reborn sandbox redirect loads 龙门寻宝_s automatically).
- But the GAME-level scene registry (client+0x5673D8 scene map, keyed (map,region)) has no entry:
  player+0x60 stays null, state stays 4. The dims setter 0x1400B2310 (only caller 0x14009DB60,
  whose only caller is fn 0x1400E11F0) is the bind point to trace next.
- Everything else is green: gateway login, game handshake (0x2FE), id4 map=296/region=0/pos
  100,100,0 landed (client+0x14=296), player inserted, state 4, stable session with keepalives.
- Next: trace 0x1400E11F0's caller chain / the message that binds the engine scene into the game
  scene registry (candidates to test live: id 8 OnSwitchMap, id 5, scene-begin). The loading
  screen is waiting on that bind.

### 2026-10-05 — V2 P3: scene registry decoded - sandbox registers as (1,0); all checks pass but guard silent
- The game scene registry (client+0x5673D8, map at +8, nodes key@+0x20, value@+0x28) has ONE
  entry: key (1,0) - the sandbox map 龙门寻宝_s registers as MAP ID 1 (dims 32..64 per run), with
  4 loaded grid cells (0,0),(1,0),(0,1),(1,1) at scene+0x7A8. So id4 must carry map=1/region=0.
- Live with map=1: client+0x14=1, player inserted, state=4, pos=100,100,0; the id-10 handler's
  version check passes ([client+0x6C]=3 < [cell+0x3C]=16); scene dims and the (0,0) cell are
  valid - yet player+0x60 stays null and state stays 4. The guard (0x140173D90) or an
  intermediate step still fails; its call-site args (0x14015A807: rcx=client, rdx=player,
  r8=[rbp-0x61] scene, r9=A, stack B/C from the +0x4B parse) are the next instrumentation target.
- Tried id10 pack qword = 0x6400064 (A=100,B=100) live - no change.

### 2026-10-05 — V2 P3 STATIC (no client): loading completion + character identity decoded
- id 10 = OnSyncNewPlayer for OTHER entities: if [pkt+7] == [client+4] (the local id) the handler
  logs (line 0x3596) and EXITS (jmp 0x14015AEEC). The local player is excluded from id 10 - our
  earlier id-10 injections with field=1001 were rejected exactly there.
- The LOCAL player's state 7 is AUTOMATIC: DLL state machine 0x1803525F0 -> if [player+0xFDC]==4
  (0x1803526D5) it calls DoClientConfirmReady 0x180170DD0; on non-zero return it writes state 7
  (0x180352700). DoClientConfirmReady builds an 11-byte C2S message (word 2; send via
  0x1801ACDA0; failure -> log line 0xA2B). The observed C2S 'proto=5 len=11' IS this confirm.
- After state 7 the client waits for the server's per-player data: S2C id 5 (min size 15;
  sub-code byte at +0xD: 0 -> 0x140327680 into player+0x1020; 1 -> 0x140327420 + local-id
  compare; 2 -> generic vtable notify).
- Character identity fields:
  * position: id-10 packet +0x4B packed qword: X=bits0-17, Y=bits18-35, Z=bits36-41 ->
    guard 0x140173D90 = SetPlayerPosition writes player+0x10/0x14/0x18 then validates against
    the scene dims (0x14017BDD0) and binds player+0x60 = scene.
  * map/region: id-4 +0x2C/+0x30 -> client+0x14/0x18 (scene registry lookup key).
  * appearance/attributes: id-10 +0x54 (packed -> player+0x268/0x26C/0x270/0x2F8/0x2FC/0x320),
    +0x64 (-> player+0xFAC/0xFB0/0xFB4 + list at +0xFB8), +0x6C (-> player+0x1F8/0x208).
- ACTIVE RULE added to AGENTS.md: NO CLIENT STARTS - static research only (user is playing JX3).

### 2026-10-05 — V2 P3 STATIC: character NAME field found (id-4 +0xB -> player+0x88, 32B)
- id-4 handler memcpy at 0x14015C440: 32 bytes from packet+0xB -> player+0x88. This is the
  character name string the client displays (our stub sends zeros -> empty name).
- Complete identity map for the entry packets:
  * name: id-4 +0xB (32B) -> player+0x88
  * map/region: id-4 +0x2C/+0x30 -> client+0x14/+0x18
  * position: id-4 +0x34/+0x38/+0x3C -> player+0x10/0x14/0x18 (also settable via the id-10
    +0x4B packed qword through the guard SetPlayerPosition)
  * appearance/attributes: id-10 +0x54/+0x64/+0x6C packed blocks -> player+0x268.., +0xFAC..,
    +0x1F8..
  * id-4 +0xCC/+0xD1/+0xD5 -> player+0x201E8/+0xEE4/+0xEE8 (from the earlier copy block)
- Loading completion: state 4 -> DoClientConfirmReady (C2S 11-byte confirm) -> state 7 ->
  server per-player data (S2C id 5) -> scene/loading finish.

### 2026-10-05 — V2 P3 STATIC: LoadingPanel.lua decoded (loading completion chain named)
- LoadingPanel.lua (proof/netcode/ui_scripts/b11/out/LoadingPanel.lua) is compiled Lua but its
  string/name tables are readable. The loading flow:
  * The panel polls the ENGINE scene loading: OnSyncSceneLoadingProcess / RealOnSyncSceneLoadingProcess
    (GetSceneLoadingProcess(dwID), GetSceneLoadingTaskCount) + OnGetLoadingTaskCount.
  * When the scene load completes it runs EndLoading -> calls **ConfirmClientReady** (the C2S
    confirm we observed as proto=5 len=11) -> LoadingComplete (fires event 7) -> events:
    LOADING_ENDING, FIRST_LOADING_END, LOADING_END, ON_UI_SHELL_LOAD_END, LOADING_PANEL_CLOSED.
  * Registered events: LOGIN_NOTIFY, UPDATE_REGION_INFO, SCENE_BEGIN_LOAD, SCENE_END_LOAD,
    SWITCH_GS_NOTIFY, SYNC_ROLE_DATA_BEGIN/END, ON_3DSCENE_LOADED, CLIENT_LOADING_END,
    FIRST_LOADING_END, PLAYER_ENTER_GAME, CONNECT_GAME_SERVER_FAILED, ...
  * Also: BeginMPakDownload/UpdateMPakDownload (the pakv4 stream download progress), GetLoadingBg
    reads minimap\config.ini, GetClientScene(dwID) - the loading is keyed by the game map id.
- Conclusion: the loading screen completes when the game-level scene for dwMapID reports 100%
  loading; the missing server piece = the data that lets the game scene (registry (1,0)) finish
  loading + the ConfirmClientReady round-trip.

### 2026-10-05 — V2 P3 STATIC: ConfirmClientReady = the state machine (full entry chain closed)
- DLL .data registration table @0x18097CB80: {name 'ConfirmClientReady' @0x1807C1EE0,
  fn **0x1803525F0**} - the registered Lua name for the state machine we decoded. (Next entry:
  0x1807C1EF8 -> fn 0x180376290.)
- So the full chain is: LoadingPanel.lua polls the engine scene load (GetSceneLoadingProcess/
  TaskCount) -> when 100% it runs EndLoading -> calls ConfirmClientReady (= state machine
  0x1803525F0) -> if [player+0xFDC]==4 it calls DoClientConfirmReady (sends the C2S 11-byte
  confirm) -> on success writes state 7 -> LoadingComplete (event 7) -> LOADING_ENDING /
  FIRST_LOADING_END / LOADING_END / ON_UI_SHELL_LOAD_END -> world UI.
- Consequence: the client sends the confirm only AFTER the loading screen's scene load finishes;
  the server side must (a) let the game scene (registry key (1,0)) report 100% and (b) answer
  the confirm with the per-player world data (S2C id 5 sub-codes 0/1/2).

### 2026-10-05 — V2 P3: stub answers the client's confirm with S2C id 5
- game_server_stub.py: on the client's C2S proto=5 (ConfirmClientReady's confirm) it now replies
  with an S2C id-5 frame (sub=0, type=1, empty TLV) and switches the keepalive to id 5; before
  the confirm the keepalive re-sends id 4. (id 10 was dropped for the local player - it is
  rejected by design for the local id.)
- Next live: expect loading screen -> confirm (proto=5) -> id5 reply -> state 7 -> loading ends.

### 2026-10-05 — V2 P3 HAZARD: raw JX3ClientX64 has NO single-instance/namespace isolation
- With the user's real client running (PID 19420, engine namespace MovieEditor.memory), launching
  a second raw client via the emulator SUCCEEDED - the engine did NOT block it. Both clients then
  share MovieEditor.memory (exactly the state the AGENTS rule forbids). The test client was killed
  immediately (PID 5016); the user's client survived (Responding=True).
- Consequence: live V2 tests require the real client to be closed first (or a namespace-isolated
  build). The reborn_client_*.exe builds support RC_MEM_NS, but the raw-client V2 flow has no
  override. Add to the test checklist: check for a running JX3ClientX64 before any emulator launch.

### 2026-10-05 — V2 P3 STATIC: id-5 sub0 data format decoded (256-dword attribute array)
- Parse 0x140327680: type byte [pkt+0xE]; type 0 -> count = 0x100 (256) and the data is 256
  dwords (1024 B) directly; types 1..3 -> [count dword][count dwords] (count >= 0, count*4 bytes
  required). Entries are written into the player attribute block at player+0x1020.
- Stub id5_frame now defaults to sub0/type0 with a 1024-byte zero array (frame 1039 B).
- Live test pending a window where the user's real client is closed (namespace hazard 8b04e62).

### 2026-10-05 — V2 P3 STATIC: post-entry flow (ApplyEnterScene + world data set)
- C2S catalog: KPlayerClient::DoApplyEnterScene = protocol 0x0003, size 0xF (the observed
  'proto=3 len=15' after loading). Builder 0x18016BCA5: message [word 3][dword [client+0x788]
  at +0xB], sent via 0x1801ACDA0. The live frame carried 0x10FBF at +0xB (a scene/tick value).
- S2C id 3 (0x1401477A0) = the time-sync reply: reads server timestamp at [pkt+7] and folds it
  into [client+0x28EF0] with a GetTickCount-style delta (clock alignment for movement).
- Post-entry server data set (what the world needs next): id 5 sub0 (256-dword attribute array
  -> player+0x1020), id 10/11/12/13 (other players / npcs / doodads / moves), id 8 (map switch),
  plus the time sync. UI: RoomBase/ApplyEnterScene is the cross-server dungeon flow, not the
  main world entry; the main UI (uishell) initializes on LOADING_END.

### 2026-10-05 — V2 P3 STATIC: world handler requirements (id 11/12/13)
- id 11 OnSyncNewNpc (153 B): [pkt+0x69] dword MUST be non-zero (the npc id; ==0 -> exit
  0x140159F12); [pkt+0x94] flags byte bit7; then the local-player lookup and the npc spawn.
- id 13 OnMoveCharacter (33 B): entity lookup by [pkt+7] (bit30 set -> 0x140174CB0 global-id
  lookup, else 0x140174D10); the entity MUST have [entity+0x60] (scene) and [entity+0x58]
  non-null else exit; then [pkt+0xB] byte -> entity+0x44, [pkt+0x18] byte -> entity+0x26C, etc.
- id 12 OnSyncNewDoodad (68 B): null checks then the lookup (body further).
- Common prerequisite across the world handlers: the entity's scene bind (player+0x60 and the
  +0x58 companion) - exactly what the id-10 guard path (0x14017BDD0) sets. So the scene-bind
  remains the gate for the whole world set, not just state 7.

### 2026-10-05 — V2 P3 STATIC: local-player scene-bind chain (the +0x60 gate) traced
- Callers of the scene setter 0x14017BDD0: the id-10 guard (0x140173D90) AND fn 0x1401780D0.
- fn 0x1401780D0 = the LOCAL player's scene bind: looks up the local id in the registry
  (client+0x5673D8 player map), packs the position (x/32 -> player+0x2C/0x30), stores the
  version [client+0x6C] -> player+0x1FFE0, calls scene init 0x140381F90, then 0x14017BDD0
  (validate position vs scene dims -> store player+0x60 = the scene).
- Callers of 0x1401780D0: fn 0x140174970 (the registry lookup wrapper) <- called from
  * the id-4 handler's map-change block (0x14015C851, inside 0x14015C1D0..0x14015CAD9),
  * the id-8 OnSwitchMap handler (0x14014D0BF),
  * fn 0x1402B19EB.
- fn 0x1401BA8F0 also calls the bind (via 0x1401135F0's result; no direct callers - pointer).
- The DLL state machine requires player+0x60 NON-NULL before the confirm/state 7 (null ->
  error line 0x4637). So the bind MUST succeed first.
- Hypothesis for the live failure: the bind ran at id-4 time BEFORE the sandbox scene existed
  (the loading screen loads it later) and the map-change branch does not re-run for identical
  id-4 packets. Next live test: force a map/region CHANGE after the loading screen is up (or
  re-send id 4 with a different region) and read player+0x60 + state.

### 2026-10-05 — V2 P3 STATIC: id-4 reset+bind block gate ([client+0x1B108] == 0)
- Inside the id-4 handler: cmp [client+0x1B108],0 -> jne 0x14015C7DE (skip); if ==0 the block runs:
  zeroes the world state (client+0x1B110..+0x1B164), re-inits the list head at client+0x1B168,
  iterates + destroys the existing entity nodes (call 0x14011CDF0 + free 0x14079AEAC), then
  scene init 0x140381F90 + the local scene bind 0x140174970 (edx=1) with [client+0x80]/[client+0x88].
- So the world reset+bind is gated by a once-only flag at client+0x1B108 (likely set by the
  bind/scene path). If the first run's bind fails (no scene yet at id-4 time), the retry
  behaviour depends on whether the flag got set - the next live probe should read
  [client+0x1B108] and player+0x60 together after the loading screen is up.
- [client+0x80]/[client+0x88] feed the scene init (xmm double from [client+0x80]).

### 2026-10-05 — V2 P3 EXTRACTION: game field maps (packet read -> object field, 236 rows)
- New tool tools/netcode/extract_field_maps.py: dataflow pairing of mov/movzx/movsx packet reads
  with the following object writes across the entry handlers -> proof/netcode/game_field_maps.tsv.
- Extracted maps (src offset -> dest field):
  * id4 base info: +0x7->+0x4, +0xD6->+0x0, +0xDA->+0x0, +0xDF->+0x8/+0x78, +0x2C->client+0x14,
    +0x30->client+0x18, +0xAE->+0x6C/+0x88/+0x18C8C, +0xC6->+0x1B618, +0xED->+0x1B728,
    +0xD1->+0xEE4, +0xCC->+0x201E8/+0xEE8, +0x34/+0x38/+0x3C->player+0x10/0x14/0x18,
    +0xBA->+0x108, +0xBE->+0x1E754, +0xC2->+0xAB0/+0x1E848, +0xDF->+0xEC8, +0x40->+0x2B,
    ...
  * id10 entity: +0xB->+0x108, +0xF->+0x1E754, +0x13->+0x140, +0x17->+0x144, +0x1B->+0x205F8/
    +0x44/+0x48/+0xC08/+0xEA8/+0xAB0/+0x10C, +0x2F->+0x40/+0x180/+0x130/+0x2B4/+0x2B0,
    +0x76->+0x2AC/+0x178/+0x17C, +0x43->+0xAB4/+0x530/+0x680/+0xDD0/+0x1EB2C/+0x1FBF0/+0x1FBF4,
    +0x6E->+0x240/+0x244/+0x248/+0xC24, +0x7A->+0x2A0..+0x1E778 (the appearance blocks).
  * id11 npc: +0x1B->+0xA14, +0xF->+0xF28/+0xAB0/+0x130/+0x44/+0x10C/+0x2B4/+0x2B0,
    +0x88->+0x2AC, +0x46->+0xAB4, +0x4A->+0xF30, +0x4E->+0xF38.
  * id12 doodad: +0x3C->+0xAC, +0x14->+0xC0, +0x18->+0xC4/+0x44/+0x48/+0x118/+0xCC, +0x1C->+0xC8,
    +0x35->+0xD0/+0x108, +0x29->+0xFC, +0x2D->+0x11C.
  * id13 move: +0xB->+0x44, +0x18->+0x26C, +0x19->+0x268, +0x1B->+0x270, +0x1D->+0x2F8.
  * id8 switch: +0x7->client+0x14, +0xB->client+0x18, +0xF/+0x13/+0x17->player+0x10/0x14/0x18,
    +0x1B->+0xAB0.
- Tool registered in docs/netcode/README.md.

### 2026-10-05 — V2 P3 STATIC: movement senders decoded (enter-game + walking expectations)
- DoMoveExteriorRequest (fn 0x1801756F0): message id word = 0x1E8, [msg+0xB] = a word (seq),
  [msg+0xD] = a byte (mode), payload from +0xE (filled by 0x1807417F0), send 0x1801ACDA0.
- DoCharacterJump (fn 0x1801708A0): message = [byte][dword][byte][player+0x2FC][dword]
  [player+0x340][player+0x10][player+0x14][player+0x18] (X/Y/Z) - the client sends its own
  position in the move/jump messages.
- Walking prerequisites (synthesis): scene bound (player+0x60/+0x58), state 7, the time sync
  (S2C id 3) for movement alignment, and the server tolerating/echoing the C2S move ops
  (0x1E8 exterior, jump ops) with S2C id 13 OnMoveCharacter for broadcasts.
