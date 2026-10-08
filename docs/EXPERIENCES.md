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



### 2026-10-05 - Audio (1.6) - native root cause completed: no media chunks, no IO opens

- Did: parsed the bank's HIRC offline: event `0xC9634CCA` = one EventAction
  (scope 3, type 4 = Play) -> Sound `0x9253BB` -> media `161340541` (size 22,067).
  The shipped `skillremake.bnk` contains only `BKHD` + `HIRC` chunks (**no
  `DIDX`/`DATA` media**; fresh extraction from the pak is byte-identical), so the
  media must be streamed. Added a `KernelBase!CreateFileW` hook (15-byte prologue,
  tail-called) and an IO diagnostic (`streamMgr`/resolver pointers): the stream
  manager and resolver are non-null, but **zero media opens reach the OS**.
- Why: the host's Wwise stream device has no usable low-level IO hook for these
  media (the game client supplies its IO/media-delivery layer; the editor install
  does not). Not a path/cwd/language problem - all were set and the hook stayed
  silent.
- Outcome: native streamed playback is a **documented boundary**; the client keeps
  the verified fallback (post, check position, else winmm WAV) so the skill sound
  is audible. Re-open: recover the game's `IAkLowLevelIOHook`/media delivery with
  evidence (no ABI guessing).
- Evidence: probe log additions (`streamMgr=... resolver=...`, `playPos rc=2`,
  no `CreateFileW` lines), HIRC parse outputs; docs corrected.


### 2026-10-05 - Unit scale (1.7) - character-size table found; gameplay capsule dig closes negative

- Did: dug the client for G-1 (exact gameplay capsule), per user request.
  Found the authored character-size table `Represent/player/player.txt`
  (`ModelHeight` 185/173/190/125 + `ModelScale`; consumer `JX3RepresentX64.dll`,
  Lua `GetMasterModelHeight` `0x1804171f0`). Capsule dig: the Semantic K/V keys
  `capsules radius`/`capsules length` exist only as schema in `SIMWorldX64.dll`
  (interner `0x180001bc0` -> global `0x18005E3B8`; consumer ~`0x180032400` ->
  `PhysicScene::_AddCapsules` `0x1800223b0`; only `0.01f` epsilons) and
  `JX3RepresentX64.dll` (interner `0x180036f60` -> global `0x180EC6EA0`, no
  reader); a full byte scan of both installs + extraction trees found no data
  file carrying the keys; shape-lib capsule id 6 (`r50/l50`) is a dynamic shape;
  `physic_character_param.krl.txt` is the ragdoll list.
- Evidence: `docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md` §4/§5;
  `docs/movement/COLLISION_SYSTEM_COMPARISON.md` P3 update.
- Outcome: 1.7 closes as a runtime/server K/V boundary (HIGH negative) with a new
  authored size source; the host capsule stays a registered proxy, with optional
  per-role scaling from `ModelHeight` pending approval.

### 2026-10-05 - Unit scale (1.7) - body-type capsule table (RC_BODY, 4 hardcoded sizes)

- Did: per user request, skipped `player.txt` parsing and hardcoded the four
  canonical body heights (f1/m1 125, f2 173, m2 185) into the capsule derivation
  (`r = 0.136*H`, `h = 0.928*H`; env `RC_BODY=f1|m1|f2|m2`; explicit
  `RC_RADIUS`/`RC_HEIGHT` still win) — a registered proxy from the authored table,
  no parsing, no new files.
- Verified: build ok; runs log `capsule body=m2 ... r=25.2 h=171.7` and f2
  23.5/160.5; m2-capsule crossing demo clean (`terrain stats` 2 loads, `DONE`);
  collision selftest 36/36.
- Evidence: `UNIT_SCALE_AND_CHARACTER_SIZE.md` §4 (implementation paragraph);
  `client/RebornClient.cs`.


### 2026-10-05 - Camera (B) - workstream B plan: .mani tracks + skill-FOV + minimal UI

- Did: entered `#iso` as agent B (`agent/camera-tracks`, off main `8e0352a`); grounded the
  plan on local evidence: `.mani` samples in the editor tree (`turningeye.mani`, 1008 B,
  magic **`ACON`** + u32 `42` at +4), `player_rush_camera.txt` (4 rows -> `data/movie/
  camera/16.mani`, `17.mani`), the 8-row UTF-8 `skill_move_camera.txt` (columns decoded),
  and the managed API dump (only editor playback: `ExportCameraTrack` / `SetCameraTrack
  PlaySpeedPerMS` / `SetCameraTrackPlayMethod` — no load-by-name, so host playback needs
  our own ACON reader). Wrote `docs/camera/CAMERA_TRACKS_PLAN.md` (P0 corpus/format ->
  P1 playback -> P2 skill FOV -> P3 minimal UI -> P4 closures) and registered it.
- Boundaries carried (not faked): auto mode switching (WW removed by user decision +
  absent engine states), glider/dynamic-follow, edge/saturation post FX, rush/dialog
  gameplay triggers (scripted path only), engine `[Camera]` ini absent.
- Evidence: `docs/camera/CAMERA_TRACKS_PLAN.md`; `docs/camera/README.md`; managed API dump
  `%TEMP%\opencode\api_cameratracks.txt`.
- Outcome: plan ready; P0 next. No code changed yet.

### 2026-10-06 - Camera (B) - P0 done: `.mani` ACON format decoded + verified

- Did: decoded the `.mani` container from the game-client binaries and verified it on the
  shipped corpus. **ACON** = sequence of 40-byte section headers `{u32 magic, u32 classId,
  32 zeros}` + class payload. Cameradata files = class 25 (meta) + class 10 (camera track):
  `8x0 + {1, dur, 1, 0} + A-hdr {nA, 0, z0, y0} + (nA-1) x {x, frame, z, y} + B-hdr
  {x0, 0, nB, 0} + nB x {x, a, b, frame}` (B's final key is a loop closure at frame 1;
  duration = last frame + 1; keys sparse). Rush variant (`16/17.mani`) has a different key
  grammar - deferred, not guessed.
- How: xref/disasm of `KG3DMovieX64.dll` (validator `0x1238d0` reads 40 bytes and checks
  magic + classId; factory `0x1ba7a0` id 10 -> ctor `0x19b8d0`, `+0xb8=10`; header writer
  `0x123d20`; object loader `0x1bb170`), plus statistical/differential analysis of the
  extracted samples (quat-norm scan for the rush transform stride; frame-classification
  runs for the cameradata A/B sections). Earlier hypotheses (u32@+4 = count, 32-byte
  records, 16-byte flat elements) were all **disproved** by exact-consumption parsing.
- Evidence: `docs/camera/MANI_FORMAT.md`; `tools/camera/mani_probe.py` (`--selftest`
  14/14 PASS, `--verify` 10/10 exact); `proof/camera_tracks/mani_keys.tsv`;
  `proof/camera_tracks/disasm/*.txt` (validator, factory, case/ctor, loader, writer).
- Outcome: P0 done; P1 (host playback of cameradata tracks) unblocked. Local only.

### 2026-10-06 - Camera (B) - decode lessons (dead ends worth remembering)

- `u32@+4` is the **classId**, not a record count: it is constant per class across file
  sizes (25 for the set section, 10 for the track section, 42 for the editor turningeye
  files). Any "count" interpretation breaks on the next sample.
- Records are **not** a flat uniform array: cameradata class-10 payload is meta + A keys
  (frame at +4) + B keys (frame at +12, loop key at the end). Fixed-stride assumptions
  (32 B "records", 16 B "elements") survived several files by coincidence and then broke
  on 21_2/23_0/30_1 - the fix was to require **exact payload consumption** and to classify
  elements by which slot carries a monotonically increasing frame.
- Rush `.mani` are a different grammar despite the same class id 10 - do not assume one
  Load per id; the marker words differ (`{1, dur, 0, 1}` vs `{1, dur, 1, 0}`).

### 2026-10-06 - Camera (B) - P1 done: host plays cameradata `.mani` tracks

- Did: `client/CameraTrack.cs` (ACON decoder + sampler, C# 5) and `RC_CAM_ANI=<path>[,loop]`
  (+`RC_CAM_ANI_FPS`, default 30 from `SceneCameraAni.tab` duration/enter-ms) wired into the
  main camera block: track A -> camera position, track B -> look-at, applied through the
  existing engine set path; obstruction/shake/terrain-clamp/snapguard bypassed for authored
  tracks; per-second `camani` log (sampled vs applied). Rush variant rejected with a clear
  message (deferred). Build script lists the new source; feature exe
  `reborn_client_cameratracks.exe` (title `sandbox-cameratracks`).
- Verified: engine run of `13_0.mani` - sampled == applied on every logged frame (e.g.
  frame 90.1 cam=(103270,997,89308) applied=(103270,997,89308)), camera advances
  30 -> 174 frames and holds the last pose, no crash; C# sampler matches the Python
  reference (<=5 u at 0.1-frame rows); 4 screenshots distinct (sha256 + 4x4 RGB);
  `camera_smoke_cameratracks` ALL PASS; collision 36/36; jx3_model 10x; gravity/loot PASS.
- Evidence: `proof/camera_tracks/p1_run_20261006.txt`; `docs/camera/MANI_FORMAT.md` §3;
  `docs/camera/CAMERA_TRACKS_PLAN.md` P1.
- Outcome: P1 done. P2 (skill-move FOV) next. Local only.

### 2026-10-06 - Camera (B) - P2 done: skill-move camera FOV effect

- Did: `client/SkillMoveCamera.cs` parses `skill_move_camera.txt` (embedded resource +
  `RC_SKILL_MOVE_TABLE` override) and implements the temporary-FOV state machine;
  `RC_SKILL_MOVE_CAM=<skill>,<ms>` scripted trigger applies the angle through
  `SetViewAngleFactor` (factor = angle / 0.837757). **Column decode corrected**: the header
  (GB18030) says 广角增幅(弧度)/固定广角(角度≥30) = FOV, not a rotation rate - value <30 is a
  radian FOV increase over the base, >=30 is a fixed FOV in degrees (the earlier plan row had
  it wrong; fixed). Post-FX fields (screen FX / edge aberration / saturation) logged only.
- Provisional: the client's FOV interpolation curve is still open research, so the ramp is
  LINEAR - registered as `HOST_DEVIATIONS.md` B16 with re-open criteria
  (`ApplySkillMoveCameraTag` `0x1802F8D20` / KRLCameraAni FOV writes).
- Verified: skill 124841 run - ramp 60.0 -> 64.8 -> 69.6 -> 75.0 deg exactly matches
  `base + 0.30 rad * phase`, effect ends at enter+exit (t=4001 ms) back to base factor
  1.250, 3 distinct screenshot fingerprints, no crash. Gates: camera_smoke ALL PASS
  (rebuilt after the change), collision 36/36.
- Evidence: `proof/camera_tracks/p2_run_20261006.txt`;
  `docs/camera/CAMERA_TRACKS_PLAN.md` P2; `docs/camera/HOST_DEVIATIONS.md` B16.
- Outcome: P2 done. P3 (minimal camera UI) next. Local only.

### 2026-10-06 - Camera (B) - P3 done: minimal camera UI (HUD line + test keys)

- Did: HUD camera line extended (mode/yaw/dist/fov/obst+len + `ani f../..` + `skillmove sN`
  when active; top line names the camera row); host test keys F5 (row cycle), F6/F8 (base
  FOV +/-5 deg), PgUp/PgDn (distance +/-100 u); `RC_HUD_LOG=1` logs the composed HUD text so
  the panel content is verifiable without reading images. Settings stay read-only (no
  custom.dat write); persistence deferred to the settings-UI (registered).
- Verified: baseline run `fov 60deg obst=ON len=1830` vs track+skillmove run
  `fov 100deg obst=off len=1862 ani f174/175 skillmove s1` (exactly the P1 held frame and
  the P2 held 60+0.7 rad target); layered-buffer dumps 633x237 vs 678x237 with distinct
  hashes; no crash.
- Evidence: `proof/camera_tracks/p3_run_20261006.txt`;
  `docs/camera/CAMERA_TRACKS_PLAN.md` P3.
- Outcome: P3 done. P4 (closures: CLIENT_AUDIT statuses, README, boundaries) next.

### 2026-10-06 - Camera (B) - P4 done: workstream B closed (P0-P4)

- Did: closed the workstream - `CLIENT_AUDIT.md` missing items 4 (track camera -> DONE via
  P1) and 7 (skill-move FOV -> DONE, screen FX logged only) annotated with a dated status
  note, item 8 annotated (P3 read-only HUD, custom.dat write path still deferred); plan
  boundaries updated (rush `.mani` grammar + gameplay hooks remain open); README index and
  tools table already carried `MANI_FORMAT.md` + `mani_probe.py` from P0.
- Result: `.mani` camera tracks play in the host (sampled == applied, cross-checked against
  the Python decoder), skill-move FOV effect works from the real table, HUD shows the camera
  state, all four phases verified in-engine with numeric fingerprints.
- Gates: camera_smoke ALL PASS (feature build `camera_smoke_cameratracks.exe`),
  collision_selftest 36/36, jx3_model 10x, gravity PASS, loot selftest PASS,
  `mani_probe.py --selftest` 14/14, `--verify` cameradata 10/10.
- Commits: `c521185` (plan), `38acd0a` (P0), `0d68302`+`cb5a4f2` (P1), `bb15cfc` (P2),
  `b21da84` (P3), this P4 docs commit. Branch `agent/camera-tracks`, local only.
- Open (registered): rush `.mani` grammar; gameplay triggers for rush/dialog; FOV ramp curve
  (HOST_DEVIATIONS B16); edge/saturation post FX; settings write path.


### 2026-10-05 - Movement/stability - spawn AV triage: out-of-extent fixed; underwater-grounded boundary

- Did: root-caused the 2026-10-04 AV (KG3DEngineDX11EX64+0x12282B3) with a discriminating
  run matrix on clean main: it triggers when the actor's x/z are **outside the map extent**
  (海岛绝境 origin (0,0), 4x4 grid; the original coords assumed the 龙门 origin). 8x8 maps
  tolerate out-of-extent test spawns. Fixed with a spawn-extent clamp in `RebornClient`
  (`TerrainSampler` now exposes the loader extent) - the original repro now exits clean
  (DONE) while inside spawns are unchanged.
- After the clamp a second position-dependent AV remains: a **grounded actor below sea
  level** on 海岛 (sampled height < 0) still AVs at the same offset, while ungrounded free
  fall through a hole (y to -44045) is clean. Registered as a boundary (water/underwater
  render path suspected - same class as the TrueSky/editor-install gaps); next probes in
  the doc.
- Evidence: `docs/movement/VOID_SPAWN_CRASH_TRIAGE.md`; crash logs `reborn_20261005_21*`;
  dump `reborn_client.exe.42808.dmp`. Gates: collision 36/36; gravity/jx3_model/loot PASS.
- Outcome: partial - out-of-extent crash fixed and verified (before/after); underwater
  boundary open.

### 2026-10-05 - Movement/collision - C residuals: .srt recon, slope server rule, capsule contract

- Did: bounded `.srt` recon - sceneinfo_full references bare `.srt` basenames
  (`S_xb多枝枯树003_*`), direct foliage-path guesses missed (folder field not decoded);
  recorded the two native recovery routes (resolve+parse the SpeedTree binary, or cook the
  tree mesh through the game's own PhysicsEngine) with the prism proxy unchanged. Wrote the
  definitive slope/drop boundary (shipped BCH has no packed cell slopes; the rule needs
  server/nav cell data) as a server contract, plus the capsule/step server-contract table
  (64 u step, 0.707 slope, `RC_BODY` table from the capsule-dig branch; contact offset N/A).
- Evidence: `docs/movement/COLLISION_RESIDUALS_STATUS.md`; extraction probes in
  `%TEMP%\opencode\srt_*`.
- Outcome: C2/C3/C4 documented as boundaries + server contract; no code change (nothing
  invented).

### 2026-10-05 - Movement/collision - underwater AV root-cause pass + .srt path resolved

- Did: (1) underwater AV - option matrix eliminated (`nWaterEffectLevel=0`, `RC_QUALITY=1`
  still AV), crash site disassembled: NULL rbtree-lookup deref at `+0x12282B3` in function
  RVA `0x1226A70..0x1228419` (lookup helper `0x18105CF50`, key from static `0x182D5BD10`);
  host has zero water wiring (G-24: water = compressed scene blocks + `_Water.mesh`,
  never loaded) -> native-fix probe = load the water layer. (2) `.srt` path RESOLVED via
  sceneinfo `comRender.actorModel`: `Data\source\maps_source\树\<name>.srt`; sample
  extracted (.srt `SRT 07.0.0` 398,236 B; `.CollisionMesh` 28,962 B HSEM = the file
  FULL_MAP_COLLISION cites; `.mesh` 43,761 B) - pipeline already correct, no change.
- Evidence: `docs/movement/VOID_SPAWN_CRASH_TRIAGE.md` §2.1-2.4,
  `docs/movement/COLLISION_RESIDUALS_STATUS.md` §1, `proof/movement/disasm/crash_*`.
- Outcome: boundary fully characterized with next probes; no invented fix.

### 2026-10-06 - Movement/collision - underwater AV scope corrected + BCH min scan

- Did: (1) falsified "grounded below sea level = AV" — 龙门 real sub-zero cell
  `(121500,43500)` y=-1216 grounded -> DONE (only console assert spam); 白龙 edge-cell
  runs clean; the AV reproduces only on 海岛. (2) Offline BCH header+payload scan of all
  5 maps: sub-zero regions 龙门 14/64 (min -6727), 龙门_夜晚 14/64, 白龙 2/64 (-2726),
  天原 44/64 (sentinel -819200), 海岛 16/16 (-17361). (3) BCH conversion live-verified
  (`worldY = f32@32 + v*(f32@28-f32@32)`, row=Z no flip; 5652.2 vs 5652 and -7119.7 vs
  -7120) — closes `TERRAIN_R32_BCH_RELATION.md`'s open header-semantics item. (4)
  `RayIntersection` assert spam (line 1701) appears at every grounded sub-zero test
  (console-only, not in log) and shares the failing path (MED). (5) the crash function
  has no direct callers (indirect-only; deeper RE in next probes).
- Evidence: `docs/movement/VOID_SPAWN_CRASH_TRIAGE.md` §2.0-2.5; logs
  `reborn_20261006_1545..1600`; `proof/movement/disasm/crash_*`.
- Outcome: scope corrected and documented; no code change (nothing invented).


### 2026-10-05 - Host polish - workstream D forked (#iso) + plan of record

- Forked `agent/host-polish` (worktree `Desktop\reborn-iso-host-polish`) off main
  (`8e0352a`) to close the host-polish remainder of system 1: clean shutdown, device/
  window settings, loading screen, native option read-back, and the two 1.10 LOD pose
  probes. Audio (1.6), weather semantics (1.9) and packaging stay parked.
- Recon grounding the plan: `KGEngineCLR.UnInit3DEngine()` + `KGBaseCLR.UnInit/UninitLog/
  UnInitMemory` exist (clean shutdown); `GetEngineOption(ref proxy)` +
  `GetEngineOptionFromConfigFile(path, out proxy)` exist (read-back; proxy fields not
  public - probe first); `HudOverlay` is created before `Init3DEngine` (overlay can show
  during init); the `Init3DEngine` 5th argument (`./configHttpFile.ini`) is a candidate
  for init-time resolution keys.
- Plan of record: `docs/engine_host/HOST_POLISH_PLAN.md` (D1-D7 with repro/verification
  and boundary policy). Base note: forked off main per team convention; the unmerged
  `agent/item1-completion` work is re-derived where needed instead of depended on.
- Outcome: plan committed; execution next (D1 shutdown first).

### 2026-10-05 - Host polish - D1 clean shutdown: safe subset + engine-teardown boundary

- Did: wired guarded shutdown at the end of the run loop (`sound.UnInit()` +
  `baselib.UninitLog()` + `baselib.UnInitMemory()`, `RC_SHUTDOWN` selects steps) and
  A/B'd each step with exit codes: `0`/`sound`/`log`/`mem` all exit 0;
  `engine.UnInit3DEngine()` AVs the process after `DONE` (0xC0000005). Default is the
  safe subset; engine uninit stays opt-in (`RC_SHUTDOWN=engine`) for reproduction.
- Evidence: `proof/host/d1_shutdown_ab.txt`; logs
  `reborn_20261005_213411..213628.log` (fingerprint `reborn_client_hostpolish.exe`).
- Outcome: D1 done - clean shutdown for Wwise/log/memory; engine-teardown boundary
  registered (re-open: recover the editor's close sequence or an engine fix).

### 2026-10-05 - Host polish - D2-D5: option schema, window sizing, loading overlay, foliage clamp

- D2 (option read-back, PARTIAL): `RC_OPT_PROBE`/`RC_OPT_DUMP` recover 49 public fields
  of `KGEngineOptionProxyCLR` (schema), but the values are the **defaults**, not the
  applied preset (tier9 `nShadowType=3` vs dump `0`) - the proxy is the panel option
  object. Active-value read-back needs the native `GetOption`/adapter-save route
  (boundary + next probe). Evidence: `proof/host/active_t{1,9}.ini`.
- D3 (device/window settings, DONE): `RC_WIDTH/RC_HEIGHT` drive the render target
  (engine screenshot 1280x720 -> 1600x900); `RC_FULLSCREEN=1` = borderless 1920x1080;
  a **nonexistent** `RC_INIT_CFG` path still inits (`Init3DEngine=1 ms=3141`) -> the
  `configHttpFile.ini` argument is inert in this build. Evidence:
  `proof/host/size_{base,1600}.png`.
- D4 (loading screen, DONE): `client/LoadingOverlay.cs` (420x84 NOACTIVATE/TOOLWINDOW)
  with phase text; window enumeration on the shipped 24 s path: overlay present at
  t=8 s, **gone** at t=33 s (after spawn). `RC_NOLOADING=1` disables.
- D5 (foliage density, DONE): at the densest `.foliage` cell, density 0 removes 2/64
  cells (8x8 fingerprint), 100 == base, 999 == 100 -> **clamp at 100 confirmed in-host**.
  Evidence: `proof/host/foliage8/*.png`.
- Outcome: D2-D5 executed; D2 leaves a registered boundary (values-not-authoritative).

### 2026-10-05 - Host polish - D6 close-up LOD pose: model-LOD keys are effective

- Did: probed the model-LOD keys at a close-up pose (teleport-in-front-of-structure demo,
  tier 9, 8x8 fingerprints): `fNodeLodLowLimit=20` changes **27/64** cells,
  `fModelLodRadius=100,..` 9/64, `bEnableModelLodViewAngle=0` 9/64,
  `fNodeLodHighLimit=100` 7/64, `nMinimumModelLod=3` 6/64.
- Correction: the earlier vista-only "0/64, inert" classification was distance/grid
  resolution - the keys work at close range (`LOD_CULL_MATRIX.md` on the item1 branch
  should note this at merge).
- Evidence: `proof/host/lod_close/{base,fNodeLodLowLimit_20,fModelLodRadius_100}.png`;
  logs `reborn_20261005_22*`.
- Outcome: D6 done - model-LOD controls verified as effective; no boundary needed.

### 2026-10-05 - Host polish - D7: HUD hitch readout

- Did: added a `hitch <n>ms` line to the info panel - max unclamped frame delta since the
  last HUD update (250 ms window), reset per update; `RC_HUD_OPEN=1` opens the panel.
- Verify: build exit 0; camera_smoke ALL PASS; collision selftest 36/36 (both exercise
  the 250 ms hud.SetText path); HUD-open run screenshot `proof/host/hud_hitch.png`.
- Outcome: D7 done - workstream D (D1-D7) complete on `agent/host-polish`.

### 2026-10-06 - Item 1.x merge into main (finalize)

- Did: merged `agent/item1-completion` @ `0ac8de1` into main `--no-ff` -> `2b01a36`;
  zero conflicts (main `8e0352a` was an ancestor of the branch, `merge-tree` exit 0
  preflight). 98 files: camera tracks P0-P4 (`CameraTrack.cs`, `SkillMoveCamera.cs`,
  HUD keys, `mani_probe.py`), stability physics + spawn guard v2, host polish
  (`LoadingOverlay.cs`, shutdown, window/device, D6/D7), audio probe, LOD/cull matrix,
  proof + docs.
- Verify: canonical + mini builds exit 0; `camera_smoke` ALL PASS; `collision_selftest`
  36/36; gravity/loot/jx3_model PASS. Tag `item1-complete-20261006`.
- Note: during the branch-merge sequence the original spawn repro AV regression (camera
  merge flip, corner case) was caught by the per-merge checks and fixed by guard v2
  (`5ddc628`, validated clamped spawn + relocation to solid above-sea-level ground;
  repro clean x2, default spawn unchanged).
- Outcome: item 1.x complete on main (pushed to origin per explicit request).
  Registered boundaries remain: FOV ramp provisional, spawn test-guard, underwater/slope
  AV (server-owned), option read-back D2 partial.



### 2026-09-29 — UI — 排队界面 render vs live screenshot (string table + GT state replay)
- Did: compared `proof/minimap/screenshots/Screenshot-given-1.png` with the
  `ui-process-app` queue-panel render. Root cause of the missing labels: the
  window's declared `StringTable=ui\Scheme\Case\string_ArenaCorpsPanel.txt` was
  absent from the local extraction, so 绝境战场/个人评分/单场奖励/随机地图/
  技能平衡/bottom-button labels did not resolve (the renderer hides unresolved
  ids). Extracted the table from PakV4, committed a UTF-8 copy under
  `ui-process-app/Data/text/ui/Scheme/Case/` and made `Engine/Paths.cs` merge
  `Data/text/**/Scheme/Case/*.txt` tables (first id wins). Replayed the reference
  state: `show` the three `Image_AnniversaryIcon1/2/3` badges and override their
  frame to 23 (赛季; the INI default 21 is 周年) through a new inventory `images`
  field (`LayoutPlanBuilder.ApplyImages`), plus sample values 个人评分=1873 /
  飞沙令=10000/10000 via `texts`. Staged the git-ignored local art extraction
  (`ui-process-app/assets`: Config/Scheme/Font/pak + flat `assets/uitex` from
  PakV4) since main's checkout lacks it; README documents the layout.
- Evidence: `UiProcessApp.exe --selftest` 19 rendered / 0 failed; `--audit`
  queue-panel placeholders=0 unresolved=0 outOfBounds=0; region-RGB + feature
  fingerprint `proof/ui/evidence/queue_panel_gt_vs_app_fingerprint.txt`
  (score-yellow GT=97/APP=95, badge-orange GT=445/APP=357) and side-by-side
  `proof/ui/evidence/queue_panel_gt_vs_app.png`; this commit.
- Outcome: solved in worktree `reborn-iso-queue-ui-compare` (branch
  `agent/queue-ui-compare`). Open: `Paths.Locate` still requires an
  `assets/ui/Config` marker, so a fresh checkout needs the local extraction before
  windows with uncommitted INIs render (not addressed here).


### 2026-09-29 — UI — Match-found prompt: generic MessageBox + global g_tStrings
- Did: the "2. 匹配成功" tree item rendered nothing because the inventory only had
  the native-MessageBox note (status NATIVE, no INI). Research found the prompt is
  the engine's generic KGUI `ui/Config/Default/MessageBox/MessageBox.ini`, opened
  as `MB_entermap` by `ui/script/module.lua`; its strings are NOT in
  `ui/Scheme/Case/string.txt` but in the global `g_tStrings` lib
  `ui/String/string.lua` (bound by `ui/module_info.xml`, found via
  `ui/filepath.txt` `SchemeGlobalStringValuable` + manifest). New tool
  `tools/ui/extract_lua_string_table.py` decodes the Lua-5.1 `SETTABLE`
  key/value constants (SETTABLE A B C: B=key, C=value) into the committed TSV
  `ui-process-app/Data/text/ui/String/string.txt` (8,242 ids: 确定/取消,
  MSG_BRACKET `<D0>(<D1>)`, STR_SWITCHMAP_GFZ_TIP 你要传送到"<D0>"地图吗？).
  Renderer gained inventory `appends` (`ApplyAppends`, mirrors
  `AppendItemFromString`, optional `top` spacer because list items ignore
  authored offsets) and the window entry `ready-confirm` (hide CheckBox_Msg +
  Btn_Option3; texts 确定(30)/取消; body at the 龙门绝境 sample).
- Evidence: `--render ready-confirm` -> `proof/ui/evidence/ready_confirm_render.png`;
  `--selftest` 20 rendered / 0 failed; `--audit` ready-confirm 1 authored
  placeholder (`Image_Option1`, empty Image in the INI); queue-panel render
  byte-identical before/after adding the global table; `MapList.tab` 296 =
  龙门绝境; this commit.
- Outcome: solved; strings available for every window now. Lesson: the UI
  `$Text` table (`Scheme/Case`) and the Lua `g_tStrings` table (`String/*.lua`)
  are different sources — check `ui/module_info.xml` libs when an id is missing.


### 2026-09-29 — UI — Match prompt layout: script resize + AnchorDst
- Did: the first `ready-confirm` render only showed the parked INI layout
  (500x120, buttons at x=20/150, X floating at the far right). Reproduced
  `MessageBox.lua`'s runtime geometry (lua:829-842 mostly, plus 655-659/824 for
  the close button) with inventory `adjust`: body 205px (measured with the
  shipped fzht_GBK at 15px via PIL) → content 226x83, window 226x103, panel
  (Image_Bg/Glassmorphism, stretch = content+56) 282x103, option row y=55,
  buttons x=20/118 (10px gap), `Btn_Close` + `CheckBox_Msg` hidden.
  Implemented `AnchorDst` support in `UiLayout` (`TryAnchorArgs` target rect +
  `ResolveAnchorDst` by section name when its abs rect is known) so the panel
  ornaments anchor to `../Image_Bg` top/bottom-centre instead of the parent's
  box. Note: an item's `Top` is relative to its parent — the first attempt set
  the buttons' Top=55 *and* moved the option container to y=55 (double offset).
- Evidence: render `proof/ui/evidence/ready_confirm_render.png` (282x103 panel,
  centred body/buttons, no X); `--selftest` 20 rendered / 0 failed; whole
  inventory `--audit` stable (166 placeholders / 8 unresolved / 82
  outOfBounds); MiddleMap / PVPShowPanel / ExitPanel re-rendered without
  breakage; queue-panel render still byte-identical; this commit.
- Outcome: solved. Open: an `AnchorDst` whose target is attached later in INI
  order still falls back to the parent anchor.


### 2026-09-29 — UI — Loading screen: per-map background + state replay
- Did: the `loading-window` (LoadingPanel) render was empty because (a) the
  Carriage/CharButton progress chrome and the per-map loading art were missing
  from the local extraction, and (b) `TextureLoader` only decoded DDS/TGA even
  though `IsRawImage` claimed `.png` (the CDN `_mb` art and the native loading
  BMPs are PNG/BMP). Extracted `ui/Image/login/{Carriage,CharButton}.{UITex,Tga}`
  from PakV4, added PNG/BMP decoding (WPF `BitmapFrame`, magic-sniffed), pulled
  the 龙门绝境 loading art `loadinglmxb.png` from the CDN hpkg
  (`105/dhirli24xvjuv.hpkg` via `tools/netcode/extract_hpkg_member.py`), and
  replayed `LoadingPanel.lua`'s normal map-load state: `Image_Bg` ← the map's
  minimap `config.ini [loading] image=` art, letterboxed 1280x720 in the
  1280x960 design canvas; `Handle_Traffic` hidden (the `ShowProgress` proto /26
  shows the traffic bar only while `IsTrafficState()`).
- Evidence: `--audit` loading-window placeholders 13 → 0, unresolved 0;
  `--selftest` 20 rendered / 0 failed; queue-panel render byte-identical; render
  `proof/ui/evidence/loading_panel_render.png`; this commit.
- Outcome: solved. Open: the native GDI `KWindowsLoadingWnd` composition
  (`ui/Loading/background{1..18}.bmp`, 600x333) is still not shown by the viewer.


### 2026-09-29 — UI — All map loading screens + bar position fix
- Did: extended the loading window to all seven 绝境 maps (296 龙门绝境,
  297 龙门绝境·夜, 410 沧溟绝境, 512 白龙绝境, 532 天原绝境, 645 洱海绝境,
  709 林海绝境), one viewer entry per map (`loading-<id>`), each with its own
  `[loading] image=` art pulled from the CDN hpkg (7 PNGs in the local assets).
  Fixed the bar position: the previous render used the parked INI geometry
  (bar at y=495, letterboxed art); `LoadingPanel.lua`'s `CorrectShow` sizes
  `Image_Bg` to the client (+4/+2 at -3,-1) and places the progress bar at
  (10% w, 89.4% h) = (128,858) with an 80%-wide track and a 912px fill,
  message handles at (128,838) and the tip panel at (655,115). Replayed via
  inventory `adjust`.
- Evidence: `--selftest` 26 rendered / 0 failed; `--audit` `loading-*`
  placeholders 0 / unresolved 0; renders
  `proof/ui/evidence/loading_panel_296_render.png` +
  `loading_panel_532_render.png`; this commit.
- Outcome: solved.


### 2026-09-29 — UI — Viewer chrome: Chinese-only names, layout-first tabs, page labels
- Did: app UX pass on `ui-process-app`. Left tree and right header now show the
  window's Chinese name only (English id moved to the tooltip), the stage list
  bullet drops the English title, the big summary line under the header is gone
  (status moved to the Details heading), the 布局 Layout tab is first and the
  default, and the Page selector shows the game's own mode labels
  (`Page_X` → `CheckBox_X` → its `Text_*` `$Text`; e.g. Page_DesertStorm →
  五人模式, Page_DesertStorm_Skill → 乱武模式, Page_Zombie → 李渡鬼域) instead of
  raw ids, falling back to the id when the INI authors no label. Also repaired
  the double-encoded Chinese literals in `MainWindow.xaml.cs`
  (绐楀彛/璇佹嵁/鐣岄潰鍏冪礌/鍏抽敭鏂囨/闅愯棌/鈥? → 窗口/证据/界面元素/关键文案/
  隐藏/•).
- Evidence: `--selftest` 20 rendered / 0 failed; build 0 errors; page labels
  cross-checked against `string_ArenaCorpsPanel.txt`; this commit.
- Outcome: solved.


### 2026-09-29 — UI — In-match HUD / death / settlement pass (art + strings + state)
- Did: worked through the remaining inventory windows. Extracted the missing
  atlases from PakV4 (`RougeLike/NewRougeSkillBar`, `PVPUI1/2/3/4/5/16`,
  `PVPWatch`, `SystemButton`, `Box`, `BlackMarket1`, `JYUi_06`, `TeachingPanel7`,
  `TargetBg`, `Player`, `AssistNewbie`, `DesertStorm3`, `RevivePanel`, `Target`,
  `TopMenu`, `RaidTotal`, `RoomPanel`, `RaidRelated`, `Voice1`) and the
  per-window `StringTable=` files the labels needed (`String_Comman` for
  MiddleMap, `string_PVP` for PVPShowPanel, `string_Novice` for
  RevivePanel/Teammate, `String_RougeLike` for DynamicBattleRoyale,
  `string_TeachingPanel`), committed as UTF-8 copies under
  `Data/text/ui/Scheme/Case/`. Replayed the minimap TOPRIGHT lens layout from
  `Minimap.UpdateAnchorCorner` (Wnd_Corner 27,0 / Wnd_Minimap 0,32 /
  CheckBox_Switch 205,-2), wired the extracted `middlemap.png` sample into the
  MiddleMap/BattleFieldMap `Image_Map` elements, and made raw textures ignore
  the authored `Frame` (MiddleMap's Image_Map is Frame=1).
- Evidence: `--audit` placeholders 153 → 15 (all authored `Image no Image`
  runtime-filled), unresolved 8 → 0; `--selftest` 26 rendered / 0 failed;
  render sheet `proof/ui/evidence/hud_windows_render.png`; this commit.
- Outcome: solved. Open: Teammate's five `PosType=10` slots overlap (the
  stacking rule is not decoded; PosType 10 semantics are unknown per
  `UI_SYSTEM_REPORT.md` §4.4).


### 2026-09-29 — UI — Loading screen: one window with per-map pages, 16:9 canvas
- Did: collapsed the seven `loading-<map>` viewer entries into a single
  `loading-window` whose `pages` (296 龙门绝境 / 297 龙门绝境·夜 / 410 沧溟绝境 /
  512 白龙绝境 / 532 天原绝境) switch only `Image_Bg`; 645 洱海绝境 / 709
  林海绝境 dropped for now per user direction. Added `PageState` to
  `LayoutPlan` and a custom-page branch to the renderer (a page with no INI
  replays the base layout, then applies its `images`/`texts`/`adjust`), wired
  the Page selector through `MainWindow`/`App` (`--render --page`, `--audit`,
  `--selftest`), and changed the display canvas from the authored 4:3
  1280x960 to 16:9 1280x720 (`LoadingPanel` w/h; bar at (128,644), tip at
  (470,709), `Image_Bg` 1284x722 at -3,-1). Stage titles and tabs are now
  Chinese-only (`ChineseOnly()` in the tree/header).
- Evidence: `--audit` `loading-window` placeholders 0 / unresolved 0 (5
  outOfBounds = authored 1280x960 `Handle_Total`, the intentional `Image_Bg`
  overhang, and the tip text that sits in it); `--selftest` 20 rendered / 0
  failed; renders lp_296/297/410/512/532.png all show the right map art with
  the bar/handles/tip; audit totals 15 placeholders / 0 unresolved / 83
  outOfBounds. Lesson: PowerShell does not wait for GUI-subsystem exes, so
  `& UiProcessApp.exe --audit; Get-Content audit.txt` reads the previous run's
  file — use `Start-Process -Wait`.
- Outcome: solved (uncommitted; user asked to hold commits). Open: 645/709
  pages intentionally not added.


### 2026-09-29 — UI — Loading PakV4 tip hidden; staging countdown decode (no KGUI renderer)
- Did: (a) hid `Handle_PakV4Msg` in the `loading-window` entry — the
  在登录器的资源管理中提前下载更多资源 tip is the PakV4 streaming prompt, absent on a
  normal map load (audit section count 19 → 15, its outOfBounds tip text gone).
  (b) Decoded the staging 开局倒计时/安全区: the phase clock (S2C 0x11B →
  +0x1b490) is consumed only by `BattleFieldMap.UpdateTime` →
  `Wnd_Title/Text_Time`, but `BattleFieldMap.Init` hides `Text_Time` when
  `IsInTreasureBattleFieldMap()` (decompiled:1352-1359) and that predicate covers
  every 绝境 map (296/297/410/512/532/645/676/677/709/715). The countdown frame
  `UI_黑山绝境_倒计时通用底框.pss` is an editor/runtime effect (no INI references
  it) and the safe-zone circle is engine `MapCircle` + `SFX_CircleNew`
  (`C_自己圈范围_677.pss`, decompiled:5317-5341). A first construction showing
  `Text_Time` was reverted after the treasure-map branch check.
- Evidence: `--audit` loading-window 19→15 sections / placeholders 0 / tip text
  gone; `--selftest` 20 rendered / 1 skipped / 0 failed; render of the loading
  page in the session temp dir shows no tip line; `JX3_MODE_UI_INVENTORY.md` §5
  updated with the OPEN state.
- Outcome: loading solved; staging renderer stays OPEN (not a KGUI window).
  Lesson: check the mode predicate branches (`IsInTreasureBattleFieldMap`) before
  wiring a state — the same `UpdateTime` text is hidden in this mode.


### 2026-09-29 — UI — Catalog numbering X.Y; MiddleMap (5.1) frame+map state
- Did: (a) the viewer tree now numbers every window `stage.window` from the
  inventory order (5.1 = MiddleMap) — shown in the tree, the header and the
  stage window list. (b) MiddleMap 5.1 rendered a pile of parked runtime widgets
  (GF counters 400/400, heat-map toolbar 显示统计点/数字·争夺区域人数·刷新统计,
  scale/alpha sliders, event marks, draw line, player/teammate markers); the
  inventory `hide` list now suppresses those layers so the render is the frame
  (translucent `MapWindow3/4` bg tiles under `Handle_Bg_0!`) + `Image_Map` only.
- Evidence: `--selftest` 20 rendered / 1 skipped / 0 failed; `--audit` middlemap
  148/142 → 13/12 sections/elements, placeholders 0; render in the session temp
  dir shows the map over the translucent frame with no overlaps. JSON summary +
  `JX3_MODE_UI_INVENTORY.md` §6 viewer-state updated.
- Outcome: solved. Open: the map sits at the authored (184,0) rect; the runtime
  `CorrectPos` clamp/zoom replay is not modelled (static state).


### 2026-09-29 — UI — MiddleMap 5.1: authored UI restored (correction)
- Did: the previous "frame + map only" hide list suppressed the authored UI
  (地图/探索 tabs, world-title button, 浩气盟/恶人谷 camp filters, GF counters +
  heat toolbar, 标记设置, scale/alpha sliders) — those sections are
  `LockShowAndHide=0` (authored visible) and were hidden by a mode-state guess.
  The hide list now keeps the authored UI visible and hides only the parked
  data-marker layers (person/teammate, born/treasure/event marks, draw/storm
  lines, area name, traffic, quest/NPC marks) plus the command-mode/quest-filter
  subtrees and script-locked widgets (`Wnd_CommandMap`, `CheckBox_QuestPage`,
  `Btn_Close`, `WndContainer_HeatMapDetail`).
- Evidence: `--render middlemap` shows the map with tabs, camp filters, counters,
  heat toolbar, 标记设置 and sliders; `--selftest` 20 rendered / 1 skipped / 0
  failed; `--audit` middlemap 13/12 → 69 sections. Lesson: `LockShowAndHide=0` is
  the authored-visible default — don't hide it without script evidence
  (`RefreshAllMarks` hides only `CheckBox_QuestPage`).
- Outcome: solved. Open: the map still draws at the authored (184,0) rect behind
  the top/bottom rows (runtime `UpdateMapPos` fit/center + `CorrectPos` clamp not
  replayed; the viewer has no clip).


### 2026-09-29 — UI — MiddleMap 5.1 composite (WorldMap behind) + GT replay
- Did: (a) the WorldMap overlay was drawn *in front* of MiddleMap, so its body
  background dimmed the whole map; the client shows it *behind*
  (`WorldMap_ShowBehindMiddleMap(false, true)`, MiddleMap.decompiled.lua:22298 →
  WorldMap.decompiled.lua:8314-8356) and hides the WorldMap map scroll/list
  while keeping its chrome — the render now composites overlay-then-main in both
  the headless and interactive pipelines. (b) The WorldMap top band is shown via
  the overlay `show` list (its band images are `LockShowAndHide=1`); MiddleMap's
  duplicate band/close are no longer double-drawn. (c) The region list replays
  the capture rows: `Text_WM` 世界 (`STRING_TITLE_WORLDMAP`), `Text_Region` 陇右,
  `Text_SmallMap` 龙门荒漠 (`STRING_LONGMEN`); `Wnd_SmallMaps` adjusted to
  top 39 (the runtime list sits under `Wnd_Region`; authored 161 is parked).
  (d) `WndContainer_GFInfo` hidden — `UpdateHeatMapState` hides it when
  `CanShowHeatMap()` is false (:25045-25081), matching the capture. (e) New
  viewer features: `WndEdit` `$Placeholder` text (search boxes 城镇或秘境 / NPC)
  and `ListTemplate.imageSection`/`rowFrames` (per-row image frames; NPC filter
  checks 跨地图交通 + 其他商人 via `Image_NpcOption` frame 0/4, :501-504).
- Evidence: `--selftest` 20 rendered / 1 skipped / 0 failed; `--audit` middlemap
  106 sections / 105 elements, placeholders 0, outOfBounds 1 (`Handle_Mbg`
  authored overhang); render matches `proof/minimap/screenshots/5.1 Example.png`
  (top band chrome, 世界/陇右/龙门荒漠 list, checked NPC rows, single NPC
  placeholder). Art check: the capture's map is the 龙门荒漠 painted region art
  (same family as the extracted 龙门寻宝 sample), *not* the WorldMap
  `NewWorld01..20` satellite tiles (offline match score ~0.49 vs ~0.9 expected);
  the sample stays.
- Outcome: solved. Open: region-list names are runtime `WorldMapZoning` data
  (陇右 from the capture); the 龙门荒漠 open-world art variant is not extracted.


### 2026-09-29 — UI — MiddleMap: real 龙门荒漠 pack wired (no capture placeholders)
- Did: replaced the capture-derived values with the game's own data. (a) Found
  the table paths in the extracted UI script `table_defs.lua` (WorldMapZoning =
  `ui/Scheme/Case/WorldMap/WorldMapZoning.txt`, RegionMap = `ui/Scheme/Case/
  RegionMap.tab`, dynamic per-map `g_tMapNpcTitle` = `minimap/npc.tab`), then
  located the 龙门荒漠 CDN pack through the web-viewer resource index
  (`resource-index.jsonl` → hpkg `114/n7rauk6wpgaiw.hpkg` + `50/oc23ca7fgr3io.hpkg`)
  and extracted `config.ini`, `npc.tab`, `doodad.tab`, `middlemap.png` with
  `tools/netcode/extract_hpkg_member.py`. (b) `Image_Map` now draws the real art
  (2048×1792; `MiddleMap.UpdateMapPos` :9263-9320 fits it to 928×812 = 0.453;
  the capture measures 0.455, correlation 0.93 — the old 龙门寻宝 sample was
  visibly a different variant). (c) Region rows from real tables: Text_Region =
  `RegionMap.tab`[7].RegionName (region 7 = `MapList.tab` row 23 `Region`),
  Text_SmallMap = `MapList.tab` row 23 `MiddleMap0` = 龙门荒漠, Text_ListTitle =
  `MIDDLEMAP_COMMON_NPC`. (d) NPC rows from `npc.tab` + `doodad.tab` category
  rows (`kind`, filter `npcid`/`doodadid` == 0) with `defaultcheck` — shipped
  default 0 → unchecked; the capture's two checks are the player's saved
  StorageServer filter. (e) Viewer: `TextOverride.Table*` +
  `ListTemplate.RowSources`/`CheckedFrame`/`UncheckedFrame`; committed UTF-8
  table copies under `Data/table/`; `Strings.Load` now accepts any identifier id.
- Evidence: `--render middlemap` matches `5.1 Example.png` (real art with 往 …
  arrows and banner, 世界/陇右/龙门荒漠, 10 real rows); offline patch match of
  the art vs the capture 0.925 at scale 0.820 gt-px/art-px; `--selftest` 20/1/0;
  `--audit` 15 placeholders / 0 unresolved / 79 outOfBounds.
- Outcome: solved. Lesson: the MiddleMap labels/rows/art are all in shipped
  per-map packs; `table_defs.lua` + `resource-index.jsonl` are the lookup path —
  never transcribe from a screenshot.


### 2026-09-29 — UI — MiddleMap: capture re-check, window placement + tab state
- Did: re-measured the render against `5.1 Example.png` with a high-pass region
  alignment (text/edges survive the capture's glass darkening). Found the whole
  MiddleMap window ~30 px too high: the capture's 地图 tab underline sits at
  y=113 (authored 86), the left list rows at 94/140/183 (ours 64/110/153), the
  bottom bar at ~802 (ours 768), the art element at y≈28 (ours 0) — while the
  WorldMap chrome (band at 0..33) matched. Fixes: (a) new `WindowInfo.OffsetX/Y`
  (inventory `offsetY: 33`) shifts only the main window; the overlay stays at
  the client top (the composite grid + fixed-size children were also pinned to
  Top/Left so WPF does not center them); (b) `ImageOverride.Checked` replays
  `CheckBox:Check(true)` → `CheckBox_MapPage` renders the selected frame;
  (c) `CheckBox_ExplorePage` hidden (script hides it when `GetMapExploreInfo` is
  empty :3920-3958; the capture shows no 探索 tab).
- Evidence: high-pass alignment residuals after the fix: left-list dy=6,
  search/title dy=6 (corr 0.72), bottom bar dy=0, rows dy=14 (the runtime
  title-row height differs from the authored 54; open); render
  `middlemap19.png` 1410×923; `--selftest` 20/1/0; `--audit` 15/0/79.
- Outcome: solved. Lesson: a composite capture needs per-window placement
  (AnchorDst=client) — a window-local render matches only after replaying the
  window's offset on the client; measure with high-pass alignment, not raw
  pixels (the glass/art background dominates raw correlation).


### 2026-09-29 — UI — MiddleMap: placement fine-tuned to the capture (27/43)
- Did: the first offset guess (+33) left the window 6 px low and the NPC rows
  11 px low. Re-fit against the capture with per-feature ink bboxes + high-pass
  alignment: `offsetY` 33 → 27 (list/tab/search/title/art now dy=0..2) and
  `Handle_Mode` height 54 → 43 (the runtime title-row height; the panel rows now
  land at the capture's y=228.6/266.9). The x residual (~5-10 px) is the
  capture's own left border/scale (the band title is centered at 705 in window
  coords → the image origin is ~6-10 px), not a window offset.
- Evidence: high-pass alignment dy: left-list 0, tabs 2, search/title 0, rows
  -2, bottom bar -6, band 6; ink-bbox row check 229/267 vs 228.6/266.9;
  `--selftest` 20/1/0; `--audit` 15/0/79; render `middlemap20.png` (1410x917).
- Outcome: solved. Open: the bottom bar and WorldMap band carry a ~6 px
  residual (not one rigid offset fits every panel); the capture's two checked
  NPC rows are player state.


### 2026-09-29 — UI — MiddleMap: the map's ShapTexture border (missing mask)
- Did: the capture's map area has a soft feathered border (the map fades into
  the glass), while the render drew the source PNG's hard rectangle. Root cause:
  KGUI `ShapTexture` + `AlphaShap=1` masks a container subtree with the shape
  texture's alpha — `Handle_Border` uses `ui/Image/UItimate/UIMask/MapMask.tga`
  (100x80 RGBA, soft rounded-rect alpha, stretched over the 936x764 border) —
  and the renderer ignored it. Fix: extract the mask from PakV4 and apply it as
  the container's `OpacityMask` (ImageBrush, Stretch=Fill) for any section with
  `ShapTexture` + `AlphaShap=1` (also covers the WorldMap guide container and
  the round minimap lens, `MinimapSharp.tga`).
- Evidence: edge crops (left/top/bottom) now show the same soft fade as
  `5.1 Example.png`; `--selftest` 20/1/0; `--audit` 15/0/79; render
  `middlemap21.png`.
- Outcome: solved. Lesson: KGUI masks (`ShapTexture`/`AlphaShap`) are part of
  the authored look — a hard-edged source texture in a render means a missing
  mask, not a bad asset.


### 2026-09-29 — UI — MiddleMap: missing-piece audit vs the capture
- Did: high-pass energy diff on a 15x12 grid (GT detail vs render) to find
  content the capture has and the render lacks. Found and fixed: (a) the craft
  trunk row (`显示采集点` = `MIDDLEMAP_CRAFT`) was missing entirely — the script
  appends `Handle_CraftTrunk` (a `Handle_Mode` clone) after the NPC list
  (`UpdateAreaOrNpcList` :5686-5706); replayed as a second `lists` entry + the
  list height set to 10*38=380 (`SetHeightByAllItemHeight`), so the row lands at
  the capture's y=613 (+9.5 cell → -4.5). (b) the trunk rows' `Image_Minimize`
  green arrows are not in the capture (frame 8/12 not drawn there) — hidden.
  (c) the `龙门荒漠` main-city row sat ~47 px too far right: the capture's row
  content spans x 26..125 like the region rows, so `Wnd_SmallMaps` left 53 → 6
  (left cell +5.3 → the row now at 25..132).
- Evidence: grid worst cells now ≤ +3.4 (art-region brightness + the capture's
  own background UI through the glass); `--selftest` 20/1/0; `--audit` 15/0/79;
  render `middlemap24.png`.
- Outcome: solved. Open: residual +3 cells are the capture's brighter art
  grading and its background game UI showing through the translucent panels,
  not MiddleMap elements.


### 2026-09-29 — UI — MiddleMap: top band color (glass tone, no gradient art)
- Did: the capture's top band is a uniform dark-teal glass strip (52,70,70),
  while the render drew the script-locked HousePVP band gradient (frame 25/24/45/
  35, green, (55,79,73)). The capture shows no gradient art in the band, so the
  overlay no longer shows `Image_TopBg1/Bg02/Break/Bg03`; the band is the
  WorldMap's `Handle_Bg_1410x875` glass (`Image_Glassmorphism` frame 6 +
  `Image_TitleBg` frame 2). Also neutralized the `ImageType=16` glass tone
  (0x2E3B49 → 0x33393E): the old tone was bluer than both the MiddleMap band and
  the queue-panel captures; the glass panels now read (48,74,79) vs the capture's
  (52,70,70) (was (55,79,73) green).
- Evidence: band samples per x; `--selftest` 20/1/0; `--audit` 15/0/79; render
  `middlemap27.png`.
- Outcome: solved. Open: the band's remaining blue residual (~9/255) comes from
  the PanelBg TitleBg art and the engine's real backdrop blur (unavailable
  offscreen); the glass tone is the documented approximation.


### 2026-09-29 — UI — MiddleMap: main-city row overlays (3 yellow things)
- Did: the `龙门荒漠` row drew three stacked state overlays — `Image_BgNormal`
  (MapWindow6 frame 33) + `Image_BgOn` + `Image_BgSelect` (frame 7, a tan
  flourish) — plus the runtime-only `Handle_MainS`/`Handle_Weather` icons
  (Button frame 67, CampMap3 frame 17). The capture shows one subtle row bg and
  no flourish/weather/M: hidden `Image_BgOn`, `Image_BgSelect`, `Handle_Weather`,
  `Handle_MainS` (the script toggles the On/Select variants and fills the weather
  from runtime data, so the authored-visible set was wrong).
- Evidence: row crop matches the capture (icon + 龙门荒漠 + subtle bg, no yellow
  art); text ink y 187 vs the capture's 188.7; `--selftest` 20/1/0; `--audit`
  15/0/79; render `middlemap28.png`.
- Outcome: solved. Open: the capture's row text renders slightly larger (the
  runtime may set a different font scheme; not yet decoded).


### 2026-09-30 — UI — MiddleMap: 龙门荒漠 row vs 陇右 alignment (left 0, crest top 4)
- Did: the user reported the 龙门荒漠 row not aligned with the 陇右 row. Box
  correlation of the left list against the capture (`5.1 Example.png`, GT
  resized 2541→1410; the capture has a ~3 px left border, world/region rows sit
  at +3/0): the smallmap row bg/text/crest sat at −4/+2, −4/+2, −3/+11. Fixes:
  `Wnd_SmallMaps` left 6 → 0 (row content now +2/+3 like the other rows) and
  `Image_MapLogo` top −6 → +4 (crest +11 → +0, centered on `Text_SmallMap` like
  the capture). The atlas and INI are hash-identical to the client's (PakV4
  re-extract `ui/Image/MiddleMap/MapWindow6.UITex|Tga`), so the crest delta is
  the runtime row placement, not an art change — the authored `Top=-6` alone
  puts the crest 10 px above the capture.
- Evidence: strip map y180..206 corr 0.42-0.50 → 0.68-0.82; logo box corr
  0.841 → 0.881 at (+2,+0); `--selftest` 20/1/0; `--audit` 15/0/79; renders
  `middlemap29.png` (before) / `middlemap30.png` (after).
- Outcome: solved. Open: the capture's crest carries a soft bloom ~10 px below
  the art (engine glow, not in the atlas) — not replicated.


### 2026-09-30 — UI — MiddleMap: search box dicing + trunk-row icons (eye vs magnifier)
- Did: the user reported (a) the search box's left showing an extra "dusted"
  band, (b) a search magnifier drawn over the trunk rows' eye icon, twice. Root
  causes: `Image_Search` (20x20 `Common` frame 0 box art) was stretched to
  262x22, turning the 1 px borders into ~13 px bands — the client draws it
  diced (the WorldMap's sibling `Image_SearchBg` carries `ImageType=10`; the
  capture's borders measure 1 px); and the trunk rows draw the authored-visible
  `Image_ListCover` (MapWindow6 frame 30 magnifier) over the eye that lives in
  `Image_ListBg1`/`Bg2`'s art (frame 32 open / 35 closed). The runtime swaps
  Bg1/Bg2 + shows ListCover/Minimize from the expand state
  (`UpdateAreaOrNpcTruckState` :4501-4592): NPC trunk expanded (Bg1), craft
  trunk collapsed (Bg2). Fixes: adjust `Image_Search` to `imageType 10` (new
  AdjustSpec.ImageType), hide `Image_ListCover`/`Image_ListBg2`/`Image_Minimize`
  for the static NPC trunk and `Image_ListBg1`/`Image_ListCover`/`Image_Minimize`
  for the craft clone (new list-template `hide`, since clones come from the raw
  INI and the window `hide` list cannot reach `__lt_*` names — the old
  `__lt_*` entries were no-ops).
- Evidence: box correlations mm30→mm32: search 0.794→0.884, trunk row
  0.801→0.832, craft trunk 0.727→0.789; trunk icon matches frame 32 (NPC) /
  frame 35 (craft) instead of the magnifier; `--selftest` 20/1/0; `--audit`
  15/0/79; render `middlemap32.png`.
- Outcome: solved. Open: (a) the decompiled `UpdateAreaOrNpcTruckState` shows
  `Image_ListCover` when the trunk is expanded, yet the capture (real client, that
  state) shows no magnifier — the viewer follows the capture; re-open if the
  engine's `FormatAllItemPos`/draw-order rule is decoded; (b) the capture's UI
  scale is ~1.8145, not the 1.8021 used in earlier passes (fit: dx = 0.0064x +
  2.4, dy = 0.0072y − 0.7 over 16 patches) — residual offsets grow with x/y;
  re-derive the per-element alignment with the corrected scale when touching
  those rows again.


### 2026-09-30 — UI — MiddleMap: 龙门荒漠 selected-row highlight (BgSelect)
- Did: the user reported the 龙门荒漠 row's line sitting too close under the
  text. The capture's bar is `Image_BgSelect` (MapWindow6 frame 7): its ink RGB
  (93,131,103) matches the capture's bar (91,118,100), while `Image_BgNormal`
  (frame 33, (50,95,88)) is the unselected art. The script shows BgSelect for
  the selected row (UpdateNameListState :14107-14124; the current map is
  龙门荒漠), so the viewer hides `Image_BgNormal`/`Image_BgOn` instead of
  `Image_BgSelect` and places BgSelect at top 5 (authored -5 puts the bar's
  bottom edge 10 px above the capture's — same runtime row-placement fit as the
  crest).
- Evidence: strip correlations mm32→mm33: y200..210 0.83→0.90, y206..216
  0.68→0.94, y212..222 0.88→0.94, all at +3/+1 like the rest of the list;
  `--selftest` 20/1/0; `--audit` 15/0/79; render `middlemap33.png`.
- Outcome: solved. Open: the capture's bar bottom edge carries the engine's
  bloom (brighter green edge) — not replicated, same as the crest bloom.


### 2026-09-30 — UI — MiddleMap: battlefield map pages (龙门绝境/龙门寻宝, not 龙门荒漠)
- Did: the user corrected the map identity — the M-map must show the battlefield
  map 龙门绝境/龙门寻宝, not the world-map village 龙门荒漠 the 5.1 capture
  happens to show (the capture's art matches 龙门荒漠minimap_mb at 0.98 and its
  row reads 龙门荒漠). Wired the five `BATTLE_FIELD` maps as pages like the
  loading window: 296/297/410/512/532 (`MapList.tab` rows 龙门绝境,
  龙门绝境·夜, 沧溟绝境, 白龙绝境, 天原绝境), each page = the map's minimap pack
  art (龙门寻宝/龙门寻宝_夜晚/海岛绝境/白龙绝境/天原绝境 `minimap_mb`
  middlemap.png) + `Table_GetMiddleMap(id).MiddleMap0` row label. Battlefield
  state per the client data: `Wnd_Region` hidden (all battlefield rows
  Region=0, `UpateRegionBtn` :11989-11995 returns) and no NPC filter rows (all
  five packs ship a 0-byte `minimap/npc.tab` and no `doodad.tab`, so
  `UpdateNpcDoodad` appends nothing). The world rows (陇右 + the ten NPC rows)
  remain only in the capture as the layout reference. Art copies are git-ignored
  under `ui-process-app/assets/ui/data/source/maps/`.
- Evidence: `--render middlemap --page 296..532` → `Text_SmallMap` =
  龙门绝境/龙门绝境·夜/沧溟绝境/白龙绝境/天原绝境; inset art corr 0.983 (296) /
  0.987 (410) vs the packs; `--selftest` 20/1/0; `--audit` 15/0/79.
- Outcome: solved. Open: the battlefield list's vertical re-format when the
  region row hides (engine `FormatAllContentPos`) is not reproduced — the
  smallmap row is pinned at the container top (`adjust` top 0, fixed in the
  next entry); re-open when the format rule is decoded.


### 2026-09-30 — UI — MiddleMap: battlefield list spacing + empty-list chrome
- Did: the user reported an empty gap in the left list between 世界 and the map
  row (the region row is hidden on the battlefield pages but `Wnd_SmallMaps`
  was still pinned at the world-state y) and that an empty right list should
  not show its title. Fixes: `Wnd_SmallMaps` adjust top 39 → 0 (the row stacks
  at the container top, right under 世界 — the engine's `FormatAllContentPos`
  re-format is not reproduced, the viewer pins the stacked position); the trunk
  rows are hidden (window `hide Handle_Mode` + list-template `hide Handle_Mode`;
  `ApplyListTemplates` now skips an empty clone list instead of crashing on
  `clones[0]`), so no 显示常用NPC标记/显示采集点 titles; and the NPC list
  scrollbar (`Scroll_List`) is hidden — the capture shows no scrollbar (its
  list fits, ours is empty).
- Evidence: `--render middlemap --page 296` → `Wnd_SmallMaps x=3 y=105`,
  trunk/scrollbar absent, sections 52; `--selftest` 20/1/0; `--audit` 15/0/79.
- Outcome: solved.


### 2026-09-30 — UI — BattleFieldMap: five battlefield pages
- Did: the user asked for the 战场地图 to get one page per battlefield map like
  the M window. Added pages 296/297/410/512/532, each overriding `Image_Map`
  with that map's minimap-pack art (same packs as the loading/M windows). The
  INI's map-suffixed team/line elements exist for two maps only — 512
  (`Image_MapLine_512_*`, `Image_CLine_512_*`, `Handle_Team_512_*` with
  `Image_Num*_512_*`/`Image_Team[M/L]512_*`/`Text_512_*`) and 709
  (`Image_M709_*`/`Handle_Team_709_*`); the runtime looks up
  `Handle_Team_<currentMapID>_n` :3324-3358 and `Image_CLine_` :3653, so the
  four other pages hide both sets (wildcard `*_512_*` + `*709*`) and page 512
  hides `*709*` — a new per-page `hide` (`PageState.Hide`, applied after the
  window-level overrides in both render paths). Page 512 keeps the ring
  segments + team numbers.
- Evidence: `--render battlefield-map --page 296` → 0 `_512_`/709 lines, 105
  sections; `--page 512` → 48 `_512_` lines, 0 709, 135 sections; the map area
  reproduces the 296 pack art feature-for-feature (ascii match) and the 512
  page shows the 512 ring/team art; `--selftest` 20/1/0; `--audit` 15/0/79.
- Outcome: solved. Open: the ring/team elements' authored positions are the
  editor's 512 sample; the runtime storm-ring geometry (MapCircle) is still
  engine-driven and not replayed.


### 2026-09-30 — UI — BattleFieldMap: drop the non-treasure-mode elements
- Did: the user reported elements on the panel that do not belong to the
  battlefield maps. The window-level hide now drops: the faction/camp set
  (`CheckBox_Hq`/`CheckBox_Er` + `Handle_Hq`/`Handle_Er` 浩气盟/恶人谷,
  `Handle_MainCmd_*`/`Handle_OtherCmd_*` commander markers, `Handle_CampObBoard`,
  and the PK/area counts under `Handle_GFArea`/`Handle_GFList_Num`), the
  camp-battle line tabs (`Wnd_Route` — they overlapped the 显示人数 controls),
  the heat-map grid (`Handle_GFAreaList`/`Handle_GFArea`/`Handle_GFList_Num`;
  the 显示人数 checkbox starts unchecked, same call as the middlemap's hidden
  `WndContainer_GFInfo`) and the runtime item prototypes the client clears and
  re-appends (`Handle_DataMod`/`Image_Data`, `Handle_MapIcon` (boss/vehicle/
  医圣), `Handle_Gather`/`Handle_Mark`/`Handle_Arrow`, `Handle_EventMod`,
  `Image_DrawLine1`, `Image_Player`, `Image_Teammate`).
- Found + fixed a viewer bug while doing it: with the heat-map items hidden the
  skin filter re-classified `Handle_Map` as an old-skin chrome root (its
  remaining old art = the `ui\Image\MiddleMap\StormLine\*` segments) and
  dropped the whole map layer as a duplicate of `Image_Bg` (14 of the 15
  sections vanished). `HasLargeOldArt` now skips `StormLine` art (runtime map
  layers are not chrome). Debugged with a temporary `RC_DEBUG_HIDE` dump.
- Evidence: `--render battlefield-map --page 296` → 31 elements (map + title
  bar only), page 512 → 61 (map + title bar + 512 ring/team); the map survives
  the heat-map hide (bisect C4: 88 lines, was 44 without the fix); `--selftest`
  20/1/0; `--audit` 15/0/79.
- Outcome: solved.


### 2026-09-30 — UI — BattleFieldMap: top-bar controls are mode-gated too
- Did: the user questioned 显示人数/刷新/跟随 on the panel; the scripts say they
  do not belong to a treasure map. `CanShowHeatMap` (map.lua:347-371,
  decompiled with unluac) returns true only for `IsInTongWarFieldMap()` or
  `CommandBase.CanShowHeatMap()` (the command mode) — so the 显示人数 checkbox
  (`CheckBox_ShowNum.Show(CanShowHeatMap(...))`, BattleFieldMap :8198-8207) and
  the 刷新 button (hidden unless the heat map is on, :8208-8224) are absent for
  296/297/410/512/532; 跟随 (`CheckBox_Follow` → `OnCheckBoxFollow` →
  `On_JueJin_Middle_Map_FollowLeader`) is the 绝境 line-choose-phase control,
  hidden at init and after `MIDDLE_MAP_ON_JUEJING_STOPCHOOSELINE`. Added
  `CheckBox_Follow,CheckBox_ShowNum,Btn_Refresh` to the window hide. Title bar
  now keeps only `Btn_Setting` (PopupMenu) + `CheckBox_Minimize` (ExpandFrame).
- Evidence: `--render battlefield-map --page 296` → 25 elements (map + title
  bar), page 512 → 55; `--selftest` 20/1/0; `--audit` 15/0/79.
- Outcome: solved.


### 2026-09-30 — UI — BattleFieldMap: example storm line (5.3)
- Did: the user asked to show an example 风暴线/圈 (the real one is positioned
  from the runtime storm data). The script shows `Handle_StormLine` +
  `Handle_CircleNew` for every treasure map (`InitMapData` :5317-5349), so the
  viewer now keeps both shown and lays an example ring from the atlas art:
  generic `Image_Line1..10` (`StormLine3.UITex` f0-9, dotted path segments) on
  pages 296/297/410/532 and the map-512 set `Image_MapLine_512_1..6` (f10-15,
  numbered 1-6) on page 512 — each segment scaled 300/928 and placed evenly
  around a ring (center 150,131 r≈90) via `adjust` (the authored INI positions
  are the editor's palette, all parked top-left). `Image_LineA/C/E_*` reference
  `StormLine4.UITex` (not extracted) → hidden; `Image_M709_*` hidden.
- Evidence: renders `stormf296.png` / `stormf512.png` (ring visible on both);
  `--selftest` 20/1/0; `--audit` 15/0/80 (the +1 out-of-bounds is the 928x812
  `Handle_StormLine` container itself).
- Outcome: solved as an example. Open (registered deviation): the ring
  geometry (center/radius/rotation order) is an example, not the runtime storm
  data; the 圈 is the `SFX_CircleNew` particle (`C_自己圈范围_677.pss`) and has
  no bitmap — re-open when the storm sync (`OnSyncSceneHeatMap`/storm data) is
  replayed or a PSS renderer exists.


### 2026-09-30 — UI — BattleFieldMap: no storm-line example (correction)
- Did: the user corrected the previous example approach — only 白龙 (512) ships
  the line data, so no example is drawn for the other maps; the 圈 is not
  visible (the SFX particle has no bitmap). Reverted the example-ring `adjust`
  entries and the `Handle_StormLine`/`Handle_CircleNew` `show`; page 512 keeps
  its own `Image_CLine_512_*` choice-line ring (the script's `ShowLootMode`
  :3636-3679 grays/normalizes the chosen line), the other pages draw nothing.
  `Image_LineA/C/E_*` (`StormLine4` atlas not extracted) and `*709*` stay
  hidden. The example entry above is superseded by this one.
- Evidence: `--render battlefield-map --page 296` → 25 sections, 0 line
  elements; `--page 512` → 55 sections with the six `Image_CLine_512_*` ring
  elements; `--selftest` 20/1/0; `--audit` 15/0/80.
- Outcome: solved (data-driven). Open: the runtime storm sync
  (`OnSyncSceneHeatMap` / storm data) and the `SFX_CircleNew` PSS remain
  unreplayed — the 圈 needs a PSS renderer or the storm-data replay.


### 2026-09-30 — UI — .UITex frame-group parser fix (5.4 red axes button)
- Did: the user asked why 5.4 (DynamicBattleRoyale) shows a red crossed-axes
  sign at the right. It is `Btn_Option` (a real button — `OnLButtonClick` →
  `OpenHotkeyPanel("BattleRoyaleBar")`, lua:501-505) but its art resolved
  wrong: `NormalGroup=57` → `GetGroupFrame` returned -1 → the viewer fell back
  to the authored `Frame=5` (the red axes). Root cause: the `.UITex` group
  table was parsed as `(count, startFrame, interval)` + `(count−1)` bare u32
  indices; the real record is u32 `count` then `count` × 8-byte
  `(frameIndex, intervalMs)` entries — the mis-read desynced the table at the
  first multi-frame group (CommonPanel2 group at count=8), so every later
  group resolved to -1. Fixed `UiTex.cs`; group 57 now resolves to frame 105
  (the gear icon) and the button reads correctly.
- Evidence: `--frame CommonPanel2.UITex --index g57` → frame 105 (was -1);
  the Python re-parse consumes the group area exactly (4004/4004 bytes);
  5.4 render before/after (`dsb_right.png` red axes → `dsb1_right.png` gear);
  middlemap render diff after the fix: mean 0.008 (127 px > 20, scrollbar
  area only); `--selftest` 20/1/0; `--audit` 15/0/79.
- Outcome: solved. Open: groups whose multi-frame animations need per-frame
  timing still use only the first entry's interval (not animated in the static
  render anyway).


### 2026-09-30 — UI — viewer defaultWindow rule; catalog cleanup; 5.9 options
- Did: (a) new rule (`ui-process-app/AGENTS.md`): when a session works on a
  catalog item, set it as the viewer default — root `defaultWindow` in
  `Data/ui_inventory.json`; `MainWindow` now selects that window at startup
  (falls back to the first window when missing). (b) Removed the BR dynamic
  skill bar (the user's 5.4) from the viewer catalog: JSON window entry deleted,
  the §6 doc row marked research-only; the selftest drops to 19 rendered.
  (c) Identified the 5.9 target — the main-message-line (系统消息行, a top bar
  whose options come from `MessageLineList.txt`: 29 show/hide rows in 4 groups;
  `Btn_Settings` opens `OpenNumericalPanel`, lua:1630-1657; default shown =
  ONLINE_DELAY/DATE/WE_GAME_RAIL_ID, lua:100-111). `defaultWindow` =
  main-message-line.
- Evidence: MessageLineList.txt extracted from PakV4 (`ui/Scheme/Case/`) — 协作 3
  / 对抗 6 / 休闲 11 / 其他 9 rows; `--selftest` 19/1/0; `--audit` 15/0/79.
- Outcome: done. Open: the numerical panel (`OpenNumericalPanel`) is a
  game-side panel not in the extracted UI corpus; the table is the option list.


### 2026-09-30 — UI — main-message-line subset replay (时间/网络延迟/渲染FPS/逻辑FPS/飞沙令)
- Did: the user asked to show only 时间, 网络延迟, 渲染FPS, 逻辑FPS and the
  飞沙令 amount. Replayed the Lua model: the segments are runtime clones of
  `Handle_Info` appended into `Handle_MainMessage` (`addCommom`), so the viewer
  clones that item 5× from `MainMessageLine.ini` and flows the clones right —
  new `ListTemplate.Flow = "row"` sets the clone root PosType 9 (placed after
  the previous sibling's measured width). Titles via `RowTexts` on
  `Text_TitleI`; values as static samples on `Text_ContentI`
  (12:00/30/60/60/0). The static template blocks (`Handle_Currency`,
  `Handle_Info`, `Handle_Money`, `Handle_Camp` — incl. the editor test text)
  are hidden; `adjust` shrinks each clone's content block + `Image_HighlightI`
  to its measured content width (82/92/91/91/68) so the items sit adjacent,
  matching the client's content-sized items (`SetSizeByAllItemSize`).
- Evidence: `--render main-message-line` → `mml2.txt`: clones at x=65/147/239/
  330/421 (widths 82/92/91/91/68), texts 时间=12:00, 网络延迟=30, 渲染FPS=60,
  逻辑FPS=60, 飞沙令=0; `mml2.png` 1572x32 sha256 43b2cb7e9d1b59fd (fingerprint
  `image_stats.py`); `--selftest` 19/1/0 (main-message-line sections=45);
  `--audit` 15/0/78.
- Outcome: done. Open: values are samples (GetPing/GetFPS/currency data at
  runtime); 飞沙令 has no icon in the commom template; the numerical panel
  (`OpenNumericalPanel`) is not in the extracted UI corpus.


### 2026-09-30 — UI — main-message-line vs the 5.8 example (order, colons, value colors)
- Did: the user supplied `proof/minimap/screenshots/5.8 Example.png` (1199x98)
  and asked what differs. Compared numerically (glyph template matching against
  rendered font candidates + per-run mean RGB — no image attachments). Diffs
  found and fixed: (a) item order — the client shows 网络延迟, 时间, 渲染FPS,
  逻辑FPS (the tShow order), not 时间 first; (b) titles carry the fullwidth
  colon — the Lua appends `g_tStrings.STR_COLON` ("：", lua:5755 commom / 6297
  currency); (c) value colors — the Lua switches the value label's font scheme
  by the live value: `getPingFont` (≤300ms → 105 green2, ≤800 → 101 orange2,
  else 102 red2) and `getFPSFont` (≥40 → 105, ≥20 → 101, else 102), schemes read
  from the shipped font.ini (101=orange2 255,150,0; 105=green2 0,200,72);
  (d) values — ping "100", date-time "2026-08-22 00:31:45" (the DATE item shows
  the full date-time), FPS "34"/"34". Viewer: new `fontScheme` field on the text
  override (LayoutPlan.TextOverride + ApplyTexts) applies the scheme to the
  section before layout; item widths re-measured (content + trailing blank 8).
- Evidence: `--render main-message-line` → `mml4.txt` (items at x=65/180/386/
  492/598; titles 网络延迟：/时间：/渲染FPS：/逻辑FPS：/飞沙令：; values
  100/2026-08-22 00:31:45/34/34/0); `mml4.png` ink colors: ping #00C848,
  FPS #E18909 (schemes 105/101), title #D0D50F (scheme 27), value white;
  `--selftest` 19/1/0; `--audit` 15/0/78; fingerprint 1572x32 sha256
  335927a61272337d.
- Outcome: done. Open: the 5.8 capture has no 飞沙令 item (kept per request,
  commom template, no icon); the example shows no obvious per-item highlight box
  — `Image_HighlightI` stays authored-visible (no Lua driver found; re-open with
  a hover/GT capture).


### 2026-09-30 — UI — main-message-line: drop the per-item highlight plate
- Did: the user reported the render looked like a pasted screenshot — every item
  sat on a wide plate with a "lighting" glow. That plate is `Image_HighlightI`
  (Common.UITex frame 4, 176x28 — a dark plate with a bright core), authored in
  the item prototype but with no Lua driver (no Show/SetFrame/SetAlpha call in
  MainMessageLine.lua — it is the engine's hover highlight). The 5.8 GT shows no
  plate behind the items (its bar profile is smooth; the settings-button plate
  at the left IS visible in both). Dropped it from the clones via the list
  template's `hide` (cloned sections 45 → 40) and removed the dead adjust
  entries.
- Evidence: `--render main-message-line` → `mml5.txt` (no HighlightI sections;
  items unchanged at x=65/180/386/492/598); `mml5.png` bg profile now uniform
  (49 left of the glass edge, 37 beyond) instead of 43-99 plate banding;
  `--selftest` 19/1/0; `--audit` 15/0/78; fingerprint 1572x32 sha256
  99aa8a370ba196d4.
- Outcome: done. Open: the hover highlight is not replayed (static render);
  re-open with a hover GT if needed.


### 2026-09-30 — UI — main-message-line: hide the glassmorphism overhang
- Did: the user reported the bar's left side taller than the right. The left
  region is `Image_Glassmorphism` (770x32, PanelBg.UITex frame 6 = a 48x48 white
  mask, ImageType=16) while the bar (`Image_LineBg`) is 1572x22 — the viewer's
  ImageType=16 stand-in (a solid #33393E plate masked by the frame) rendered a
  10px plate below the bar plus a tint over its left. In the engine ImageType=16
  is a backdrop blur (the mask selects the blur region); the 5.8 GT bar is a
  uniform 22 tall and its left/right brightness ratio matches the world's (no
  glass tint) — the blur is invisible over the dark LineBg. Hid the element in
  this window (the stand-in stays for the MiddleMap/queue panels where captures
  show visible frosted glass).
- Evidence: `--render main-message-line` → canvas 1572x32 → 1572x22, left/right
  bg rows both 37 (was left 49-52 / right 16-37); `--selftest` 19/1/0;
  `--audit` 15/0/77; fingerprint 1572x22 sha256 b66201afddb7efb9.
- Outcome: done. Open: the glass blur itself is not reproduced (no backdrop);
  re-open if a capture shows visible glass on the message line.


### 2026-09-30 — UI — 2.1 ready-confirm fix + per-map pages; 5.4 removed; 5.5 loot replay
- Did: (a) 2.1 (ready-confirm / MB_entermap): the user reported the confirm/reject
  label color. `Text_Option1/2` author FontScheme=1 (black) while the buttons
  carry `NormalFont=18` (white) — the corpus convention (ExitPanel sure/cancel
  pairs = 18) shows the button state font is the label's color source, so the
  viewer now overrides both to 18 via the new `fontScheme` text field. Also
  fixed the body string (the entry had 你要传送到...; the shipped
  `STR_SWITCHMAP_GFZ_TIP` = 需要前往的"<D0>"地图吗？) and added one page per
  battlefield map (296/297/410/512/532) whose texts override the appended body
  (`__append_Handle_Message_1`). To let page texts reach appended sections,
  `ApplyAppends` now runs before the text passes in all three render paths
  (still after `ApplyLockedVisibility`, so locked ancestors can't eat the
  synthetic sections).
- (b) 5.4 (PVPShowPanel) removed from the viewer catalog: JSON entry deleted,
  doc §6 row marked research-only (same treatment as the BR skill bar); the
  selftest drops to 18 rendered.
- (c) 5.5 (LootList) researched and corrected: the Lua rebuilds the list per
  container (money row via `UpdateMoneyShow`, then one `Handle_Item` per entry
  with `Text_Item` = item name colored by quality, `Box_Item` = icon + count,
  `Image_Auction` only for the loot master) and `Btn_Sure` = STR_PICK_ALL ..
  STR_BRACKETS(AUTOINTERACT key). The viewer now replays a sample container via
  list clones: money row (authored 9999/99/99) + two item rows (金创药/止血草 —
  real 沙漠风暴 drop-table names from `mode_doodad_inventory.txt`) stacked at
  y=53/106, `Text_Sure` = 全部拾取［F］, the static templates + `Image_Auction`
  + the hover glows hidden.
- Evidence: `--render ready-confirm` → rc2/rc3 (body 需要前往的"龙门绝境"地图吗？/
  ..."龙门绝境·夜"... per page; label ink #C2CFCF vs black before);
  `--render loot-list` → `ll2.txt` (rows at y=40/93/146, no overlaps; Text_Sure
  全部拾取［F］); `--selftest` 18/1/0; `--audit` 11/0/59.
- Outcome: done. Open: the AUTOINTERACT key hint uses the F default (static
  sample); loot item icons/count overlays need the item data
  (`Table_GetItemIconID`); the pickup bar/smart-loot settings live outside this
  window.


### 2026-09-30 — UI — 2.1 pages in the GUI + 5.5 loot rows with rarity
- Did: (a) the user reported every ready-confirm page still showed 龙门绝境 —
  the GUI path (MainWindow) still ran `ApplyAppends` after the page texts (only
  App.xaml.cs's three paths had been reordered); moved it there too, so the
  page body overrides now apply in the viewer.
- (b) 5.5 loot rows: names switched to the user's items (麻布绷带 first,
  月影沙 second) and the rarity display implemented the way the Lua does it:
  the item name color replays `SetFontColor(GetItemFontColorByQuality(...))`
  through the engine's own `FontColor` key (new `fontColor` text-override field;
  the viewer already resolves color.txt names) and the slot frame approximates
  `UpdateItemBoxExtend` with a new `borderColor` image override (a rarity-
  colored Border around the authored slot art). The exact item data
  (`settings/Item/Item.tab`: quality/genre/icon id) is not in the local
  extracts — `settings/沙漠风暴/*.tab` and the item tables MISS in every
  extraction log — so the quality values are PROVISIONAL: 麻布绷带 普通/white,
  月影沙 优秀/green2 (re-open: extract Item.tab or take a loot-window GT).
- Evidence: `--render ready-confirm --page 410` → body 需要前往的"沧溟绝境"
  地图吗？; `--render loot-list` → row0 name ink #CCCED0 + slot edge #F0F0F0
  (white), row1 name ink #01C448 + slot edge #00C848 (green2); `--selftest`
  18/1/0; `--audit` 11/0/59.
- Outcome: done (rarity values provisional). Open: item icons/count overlays
  (`Table_GetItemIconID` + the icon atlas) not reproducible yet.


### 2026-10-01 — UI — 2.1 body placement root cause (append width before adjust)
- Did: the user reported the ready-confirm body sat on the right. Root cause:
  moving `ApplyAppends` before the text passes also moved it before the window
  adjustments, so the appended box captured the container's AUTHORED width
  (Handle_Message 500) instead of the adjusted 226, and the centered body was
  offset by (500-text)/2. Fix: the append spec now carries `"width": 226`
  explicitly (the adjusted content width). Verified against the render image:
  the body is centered in the panel (panel pixel center 141, body ink center
  ~137), buttons read white.
- Also switched centered labels to exact WPF alignment (`block.Width` +
  `TextAlignment`) when the text fits the authored box — `FormattedText`
  under-measures some CJK strings and the offset math drifted.
- Evidence: `--render ready-confirm` → rc10.png (body centered; DBG probes
  showed box/host/block all at x=0 with a 226-wide block); `--selftest`
  18/1/0; `--audit` 11/0/57.
- Outcome: done. Open: 5.4 loot icons/rarity still blocked on the item data —
  every extraction attempt MISSes `settings/Item/Item.tab`, the mode drop
  tables and the icon textures (`ui/Image/System/...`); the icon registry
  (`proof/ui/evidence/scheme/icon.txt`) exists but the item→icon mapping does
  not. Next probe: a loot-window GT screenshot from the live client (like the
  5.1/5.8 examples) or an extraction path for the item icon pak.


### 2026-10-01 — UI — 5.4 loot icons recovered from the UI pak + Box icon support
- Did: the user insisted the loot rows carry icons. Root cause of the earlier
  misses: the icon textures do NOT live under `ui/Image/System/...` — the icon
  registry `ui/Scheme/Case/icon.txt` (icon id -> FileName+frame+Kind+SubKind)
  stores paths relative to **`ui/Image/Icon/`** (e.g. icon 18648 =
  `System\Actionskill\skill_22_11_25_1.UITex` -> `ui/Image/Icon/System/
  Actionskill/skill_22_11_25_1.UITex` + `.dds` atlas). The atlas extension is
  `.dds`, not `.Tga`. Extracted the mode's icon set (the 2022-11 era: 3
  Actionskill + 1 Coin + 2 GameplaySkill + 27 scroll items + 370 Drug icons to
  identify them). 麻布绷带 = icon 18648 (the id from the user's own fight-stat
  loot record `{"麻布绷带",false,1,18648,1}`); 月影沙 = icon 18652
  (`System\GameplaySkill\item_22_11_30_2`) — the only 2022-11 gameplay-item
  icon, provisional until the item table is reachable. Viewer: `Box` sections
  now paint their `Image` (the icon atlas frame) so `Box_Item` rows show the
  icon; icons copied to `ui-process-app/assets/ui/Image/Icon/...` (git-ignored).
- Evidence: `--render loot-list` → ll6.png (麻布绷带 icon + white frame,
  月影沙 icon + green frame, 全部拾取［F］); the extraction path HITs
  (`ui\Image\Icon\System\Actionskill\skill_22_11_25_1.UITex/.dds`); the icon
  contact sheets (drug/gameplay/DJ) used for identification; `--selftest`
  18/1/0; `--audit` 11/0/57.
- Outcome: done (月影沙 icon + both qualities still provisional). Open: the
  exact item->icon mapping + qualities need `settings/Item/Item.tab`
  (unreachable in every local pak) or a loot GT; count overlays not replayed.


### 2026-10-01 — UI — 5.4 loot: drop the money row, re-pick icons + rarities
- Did: the user reported the icons/rarities wrong and asked to drop the first
  loot row. (a) The money row (a sample artifact — the real box holds only the
  two items) is gone: the money list clone removed and the item clones now stack
  from the top (adjust entries dropped). (b) Icons re-picked from the shipped
  icon sets instead of the 2022-11 mode set: 麻布绷带 = icon 6011
  (`System\Drug\CL_0417_01`, a cloth roll), 月影沙 = icon 1321
  (`System\Drug\medicNew01b`, the blue powder — the powder series reads as a
  sand). (c) Rarities changed to 优良/green2 (麻布绷带) and 精良/blue2
  (月影沙) — still reasoned, not table-derived.
- Evidence: `--render loot-list` → ll7.png (two rows only: 麻布绷带 with the
  cloth-roll icon + green name/frame, 月影沙 with the blue-powder icon + blue
  name/frame, 全部拾取［F］); `--selftest` 18/1/0; `--audit` 11/0/57.
- Outcome: done. Open: the exact item->icon/quality mapping still needs the
  item table or a loot GT — the current picks are the closest shipped-art
  matches, clearly provisional.


### 2026-10-01 — UI — 7.1 settlement panel: replay the mode (battlefield) variant
- Did: the user reported 7.1 (PVPShowFinal, 结算面板) completely wrong. The
  render was the authored editor state: test team names 左边躺尸队/右边艺术行为队,
  test numbers (8000000/1000000/111111111), one parked prototype per side, and
  the ARENA strings (将在99秒后传出竞技场, 离开名剑大会). The Lua
  (`PVPShowFinal.lua`, decompiled for this) is shared by the arena tournaments
  and the battlefield: InitPanel shows the arena tournament logos
  (Image_Title_Master/Jingji = PVPUI7 frames 1/9, 剑网3竞技群英赛/争霸赛) only for
  the arena mode; UpdateOneSideList appends one row per stat entry with
  Name/Kill/Damage/Health/NearDeath and formats numbers >= 10000 as 万
  (MPNEY_TENTHOUSETHOUSAND, lua:474-495); team names fall back to
  STR_PVP_PLAYER_TEAM_NAME_L/R (左方/右方); the banish warning and leave label
  switch to the battlefield strings (STR_BATTLEFIELD_BANISH 将在<n>秒后传出战场,
  STR_UISET_BFCENCEL 离开战场). Viewer now replays the battlefield variant: 左方/
  右方, 将在30秒后传出战场, 离开战场, 3 sample rows/side with 万-formatted stats.
- Evidence: `--render pvp-show-final` → psf2.png (headers 左方/右方, three rows
  per side, 将在30秒后传出战场, 离开战场); the PVPUI7 title frames viewed
  (frames 1/9 = 群英赛/争霸赛 tournament logos — arena only, stay hidden);
  `--selftest` 18/1/0; `--audit` 11/0/57.
- Outcome: done. Open: the row data is a static sample (the real settlement is
  fed by BATTLE_FIELD_SYNC_STATISTICS), and the 7.2/7.3 L/R list windows still
  show the editor test values.


### 2026-10-01 — UI — recovered the real battle-end settlement (EndOfBattle)
- Did: the user rejected the PVPShowFinal replay ("not arena") and asked for the
  绝境战场 result final. The window was not in the 144-file dictionary corpus;
  found it by searching the shipped shell scripts: `module.lua` calls
  `EndOfBattle.Open(...)` on `ON_CASTLE_END_ACTIVITY`. Extracted it from PakV4
  by name: `ui/Config/Default/EndOfBattle.{ini,lua}` + its string table
  `ui/scheme/case/string_EndOfBattle.txt` (攻城结算数据, camp scores, 战斗信息 /
  成就信息 rows, 奖励预览 with 战阶/威名点/帮会奖励 and 名剑币). Added it as a new
  catalog window (stage 7 first entry, defaultWindow) and replayed the runtime
  rows: prototype `Handle_Item` hidden, 3 battle-record + 2 achievement clones
  with sample values, per-camp score columns.
- Crash found while rendering: two sections differ only by case
  (`Text_JiFen_1` header under Handle_MiddleBg vs `Text_Jifen_1` row under
  Handle_Item) and the viewer's case-insensitive `result.Elements` merged them,
  attaching one element twice ("Specified element is already the logical child").
  Fixed by tracking elements per section object (`UiBuildResult.ElementsByRef`)
  in the layout walk; name-keyed maps stay for the inventory overrides/dump.
- Evidence: `--render end-of-battle` → eob5.png (攻城结算数据, 20000 vs 20000,
  3+3 record rows, 2+2 achievement rows, 奖励预览, 主战场/奇袭场 tabs);
  `--selftest` 19/1/0; `--audit` 18/0/61. The extracted files are committed
  under `ui-process-app/assets/pak/` + the UTF-8 string copy in `Data/text`.
- Outcome: done. Open: the record/achievement values are samples (runtime data
  from g_tTable.EndBattle/EndOfBattleInfo), and 18 placeholders remain (some of
  the window's atlases are not extracted yet).


### 2026-10-01 — UI — 7.1 hunt: EndOfBattle is the 攻城 settlement, not the BR result
- Did: after fixing PVPShowFinal to the battlefield variant the user still said
  "not arena", and after adding EndOfBattle they said "completely wrong 结算".
  Verified against the client data that EndOfBattle is the 攻城/阵营 settlement
  (its title comes from tLine.szName = 攻城结算数据 for that battle line; the
  camp scores + 拥有城池 records are the siege data), and that the only
  battlefield settlement the data wires up is PVPShowFinal (opened by
  PVPShowPanel on BATTLE_FIELD_SYNC_STATISTICS with tPQ/tInfo/tName; the Lua's
  battlefield path merges tPQStat and reuses the same L/R lists). The user
  could not describe the expected window ("idk what to say"), so the exact BR
  result remains unidentified locally.
- Evidence: `EndOfBattle.lua` (Open takes tMainWarInfo/tSneakWarInfo;
  UpdateTop sets Text_Title = tLine.szName), the PVPShowPanel→PVPShowFinal
  reference in its Lua, `--selftest` 19/1/0.
- Outcome: partial. EndOfBattle stays in the catalog as its own window
  (renamed 攻城结算（战斗结束）), defaultWindow moved back to pvp-show-final.
  Open: the BR result needs a user-provided in-game capture (like the 5.1/5.8
  examples) or an enumeration of the ui pak — all local name-guessing
  (DesertStorm*/BattleField*/Result/Final/EndOfBattle variants) came back MISS.


### 2026-10-01 — UI — client-side hunt for the 绝境 result window (why it's blocked)
- Did: per the user's instruction, went back to the client install instead of
  the notes. Checked, read-only: (a) the PakV4 store `C:\SeasunGame\Game\JX3\
  Pakv4\<group>\<n>.dat` (2,264 files) — the .dat files DO contain plaintext
  strings but the directory index is `.idx` + `.htr` (HTREE001) HASHES, no name
  list; a byte-scan of all .dat files for `STR_JIESUAN`/`STR_SHOW2` found
  nothing (entries are compressed), and `ui/Config` occurs only inside data
  rows (e.g. AssistNewbieTip.ini), not as a file list; (b) the client binaries
  (JX3ClientX64[Base], JX3UIX64, KGUICocosX64, JX3LogicEditOperationX64) —
  zero occurrences of PVPShowFinal/EndOfBattle/DesertStorm/window names (all
  KGUI windows are data-driven); (c) client logs (KG3D_Engine, KGPK4,
  JX3Client) — engine only, no UI window log (bReleaseVerPrintUILog=0);
  (d) userdata custom.dat — only PVPShowPanel/FightingNum keys;
  (e) MovieEditor ResourcePack — editor assets only, no game UI;
  (f) the addon corpus — no settlement window references.
  Conclusion: the BR result window's NAME is not recoverable from the local
  install (hashed pak store + no UI log); the PakV4SfxExtract tool can only be
  probed by exact path, and every candidate pattern MISSes.
- Next probe (concrete): an in-game capture of the 绝境 result (drop into
  `proof/minimap/screenshots/`), or enable the client UI log
  (config.ini bReleaseVerPrintUILog=1) and open the result once — the log then
  names the window INI directly, which extracts in seconds.
- Outcome: blocked, documented. No fake window was shipped.


### 2026-10-01 — UI — BREAKTHROUGH: the 绝境 result window is ACC_TreasureFinal (via ui/module_info.xml)
- Did: superseded the "blocked" conclusion of the entry above. The PakV4 store is
  hashed, but the UI module MANIFEST `ui/module_info.xml` probes by exact path and
  HITs (188,406 bytes). It lists every window module + its script file, including
  the battlefield-final family: `ACC_BFShowFinal` (BattleField/ACC_BFShowFinal.lua),
  `ACC_TreasureFinal` (BattleField/ACC_TreasureFinal.lua), `ACC_MobaShowFinal`,
  `ACC_JJCRougeShowFinal` (ArenaTower/), `ACC_WinOrDefect`, `ACC_DesertStormInfo`.
  The 绝境/寻宝 result = module `ACC_TreasureFinal`; extracted
  `ui/Config/Default/BattleField/ACC_TreasureFinal.{ini,lua}` (14,694 + 9,412
  bytes) and rendered it (new stage-7 catalog entry `treasure-final`,
  defaultWindow; 5 sample member rows; 0 placeholders/0 unresolved).
- Window facts (from the decompiled Lua + INI): 1920x700; `Text_Rank` =
  `FormatString(STR_TREASURE_RANK, nRank, nTotal)` ("队伍排名：<D0>/<D1>", badge
  `Image_Title`/`Handle_Rank` digits + `Table_GetTreasureInfoTitle`, rank 1-3
  plays `Handle_SFX` copper/silver/gold); rows clone `Handle_Player` in
  `Handle_FinalList` (my team, `GetMyTeamMemberData` sorted by field 12;
  Name/DECAPITATE_COUNT/KILL_COUNT/BEST_ASSIST_KILL_COUNT/HARM_OUTPUT/
  SPECIAL_OP_3/SPECIAL_OP_6 -> Text_PlayerName/KillNum/XSNum/BestZGNum/HarmNum/
  JJFenNum/ResultNum, per-row `Handle_Reward` award icon+count);
  `Btn_Leave`->LeaveALLBattleField (离开战场), `Btn_Export`->ExportData
  (导出数据, 比赛信息导出成功), `Text_Time`=STR_BATTLEFIELD_TIME_USED,
  `Text_WarningTime`=STR_NEW_BANISH_1..2 countdown; Handle_Win/Handle_Fail +
  PVPUI12 chrome are LockShowAndHide=1 (the win/fail branch lives in the sibling
  ACC_BFShowFinal.lua for its own window, not in ACC_TreasureFinal.lua).
- Fix surfaced by the window: shipped INIs carry CASE-DIFFERING TWINS
  (`Text_playerName` header vs `Text_PlayerName` row prototype; EndOfBattle has
  Text_JiFen_1/Text_Jifen_1). The plan dictionaries were OrdinalIgnoreCase, so
  the header's ancestor walk resolved through the row prototype to the hidden
  `Handle_Player` and the header silently vanished. Made section identity
  case-sensitive (`IniFile.ByName`, `UiBuildResult.Elements/Sections`, UiLayout
  caches) and added `LayoutPlanBuilder.TryFind` (exact first, case-insensitive
  fallback) for inventory-supplied names, so sloppy-cased override entries keep
  working. A scan of all extracted INIs confirmed no real `._Parent` case
  mismatches (only virtual layer names Normal/Topmost/Lowest...).
- Evidence: `tools/netcode/extract_pak_paths.py --list` HIT `ui/module_info.xml`
  and the two ACC_TreasureFinal files; render dump shows Text_playerName at
  (655,135) + 5 rows; `--selftest` 20 rendered/1 skipped/0 failed; `--audit`
  placeholders=18 unresolved=0 outOfBounds=64 with treasure-final 0/0.
- Outcome: the 绝境 result window is recovered and rendering; awaiting the
  user's visual check of `treasure-final`. Open: whether the runtime shows the
  PVPUI12 chrome/Win-Fail via the open animation (left off, documented in the
  inventory doc row).


### 2026-10-01 — UI — treasure-final follow-up: panel plate + centering (user feedback)
- Did: user confirmed the window identity ("yes that's correct") and reported the
  PVPUI12 panel plate not visible + the composition not centered. Loaded the
  tween file `ui/Animation/ACC_TreasureFinal_Ani.ini` (all fades only, no shows),
  decompiled the sibling `ACC_BFShowFinal.lua`/`ACC_JJCShowFinal.lua` and
  `battlefield_base.lua`, and compared `ACC_BFShowFinal.ini` (content page
  centered 284..1650) with the treasure INI (panel at x=0, table at 600/620,
  title/buttons centered on 960). The module script never shows the
  LSH=1 chrome; the module's open path calls `ApplyBattleFieldStatistics`,
  a global not present in the extracted corpus (definition still unfound).
- Change (provisional, viewer-side inventory data only): show the chrome
  (Image_Outline/BgL1/Bgr2/ZSL/ZSR) and recenter the panel frame + table on the
  window (adjust: panel 401, header 403, list 415, ZSL 414, ZSR 1358,
  Text_Time 418; Handle_Win/Fail also 401 for when they are shown). Follows
  the user's client observation that the plate is visible; the alternative
  (authored offsets are runtime-correct) stays possible.
- Evidence: `--selftest` 20/1/0; render dump shows panel 401..1519, table
  415..1505 centered on 960; treasury render checked as PNG (panel + table
  aligned, title plaque on the top bar).
- Outcome: visible fix; re-open when the client window is observed directly
  (or a capture confirms the authored offsets). No code change needed beyond
  the earlier case-sensitivity fix.


### 2026-10-01 — UI — treasure-final matched to the 7.1 example capture (card overlay, row formats)
- Did: the user supplied `proof/minimap/screenshots/7.1 Example.png` (the real window). Reading it
  (numerically + visually) showed the earlier viewer-side guesses were wrong and fixed the entry:
  (1) no PVPUI12 chrome in the real window (my `show` was reverted); (2) the table keeps the authored
  right-shifted position (my centering `adjust` was reverted); (3) the personal card IS open at the left
  (Wnd_PersonCard), showing the own player's avatar/name/title/level/装备分数 and three stat rows;
  (4) row semantics from the authored prototype texts: Text_Line = "/" (so Text_KillNum+Line+XSNum render
  as the 击/助 pair "2/4"), Text_BestZGNum = 最佳助攻 (0), Text_HarmNum = 万 (4407.7万), Text_JJFenNum =
  本场表现分 (44277), Text_ResultNum = 个人评分结算 (24), rewards 168; rank badge = Image_Num2 frame 9
  (digit d = frame 8+d) with Image_Num1 removed for rank 1.
- Change: catalog entry reworked (row samples, header widths Text_JJFen/Text_Result so 本场表现分 /
  个人评分结算 are not clipped) + a new `overlay` (front) rendering
  `ui/Config/Default/PersonalCard/PersonalCard_ShowData.ini` into the settlement: engine additions are
  OverlaySpec `lists` + `front` (ApplyListTemplates in BuildOverlayVisual, overlay drawn above the main
  root) and AssetResolver extension fallback (the card avatar requests HHTX_003.tga, the pak ships
  HHTX_003.dds — the client's texture loader dispatches TGA/DDS). Card atlases extracted (PersonalCard*,
  txk14, TextShadow, yirong13, Feedanimials, UItimate/Button, HHTX_003.dds); PersonalCard_Decoration bk/tx
  are runtime decoration names (not in the pak) so Image_Frame/Image_AvatarFrame stay hidden.
- Evidence: `--selftest` 20/1/0 after the change; render side-by-side with the capture (same names, pairs,
  万 values, rank 1, card rows 签到次数 673 / 成衣 92 / 披风 4).
- Outcome: the settlement reproduces the capture's structure and content; open question only = the
  decoration frame art (player-specific, not shipped).


### 2026-10-01 — UI — treasure-final: drop the card column, placeholder samples (user correction)
- Did: user asked to remove the left personal-card column ("the card is not needed") and called out
  that filling the render with their screenshot's values is not work. Corrections applied:
  (1) the `overlay` (PersonalCard_ShowData front card) was removed from the treasure-final entry;
  (2) row/header samples went back to neutral placeholders (云舟过影 / 云舟过影-style names, pairs like
  "2/4", 万-formatted damage, scores) that only demonstrate the formats learned from the INI and Lua —
  the capture is no longer replayed; (3) rank sample back to the INI's authored digits (第18名) with
  the matching 队伍排名 text; the PVPUI12 chrome stays off.
- Incident (fixed): two scripted patches matched the FIRST `"texts": [` in the file instead of the
  treasure entry's — the first clobbered queue-panel's texts, the second left treasure-final untouched.
  queue-panel's 7 texts were restored from `git show HEAD:ui-process-app/Data/ui_inventory.json` (raw
  byte redirect; PowerShell pipe mangles UTF-8). Verified by re-rendering queue-panel (绝境战场 title,
  1873, 10000/10000 present). Lesson: scope JSON text edits by locating the entry id first.
- Evidence: `--selftest` 20/1/0 after the restore; treasure render shows the table only (no card) with
  placeholder samples; queue-panel render intact.
- Outcome: catalog entry honest (shipped INI + real formats + placeholders); the card overlay engine
  support (OverlaySpec lists/front, TGA/DDS fallback) stays available for other windows.


### 2026-10-01 — UI — treasure-final: neutral backdrop + recentred table (card column was the left anchor)
- Research answer on the background: the window ships NO opaque background. Its only backdrop art is
  Image_Bg1 (ui/Image/UItimate/UICommon/PVPShowFinal1.UITex frame 11, ImageType=12, 200x200 art with
  alpha ~60/255 stretched to 1920x600) plus the per-row Image_Bg bands - i.e. a darkening veil meant to
  compose over the live game world; the PVPUI12 chrome that would frame it is LockShowAndHide=1 and the
  capture shows it off. In isolation the background is therefore transparent (the client shows the world
  through it), so the viewer's black host read wrong.
- Change: new per-window inventory field `backdrop` (hex) applied to the render host and the viewer canvas
  (App.BackdropBrush; default remains #101010). treasure-final uses #33393E (the repo's documented neutral
  glass tone) so the authored translucency reads; the table block is re-centred on the window
  (Handle_Titile 403, Handle_FinalList 415) since the personal-card column is gone; header widths kept.
- Evidence: `--selftest` after rebuild; v7 render shows the gray-blue backdrop, the centered table, title
  and buttons; no card column.
- Outcome: visible state matches the shipped INI with an explicit, documented viewer choice for the
  backdrop (not invented art). Re-open: if a world backdrop asset is ever wanted, use a real capture.


### 2026-10-01 — UI — treasure-final vs the 7.1 capture: three engine behaviours proven, one instruction overruled
- Research (all against `proof/minimap/screenshots/7.1 Example.png`, downscaled 1/1.73 to design px):
  1. The capture's leave label is ~16px white and the export label ~15px white, while the INI authors
     Text_Leave scheme 28 (20px) and Text_Export scheme 27 (yellow). Both buttons carry NormalFont=3 and
     NormalFont=18 — the engine labels buttons with the button's own font scheme. A corpus scan shows 74/298
     shipped button labels differ from their button's NormalFont (copy-paste leftovers), so this is a real
     engine rule, now implemented as `UiLayout.ApplyButtonLabelFonts`.
  2. The countdown is not the authored Text_WarningTime: `OnFrameBreathe` clears Handle_WarningTime and
     AppendItemFromString()s three segments with fonts 257 (将在) / 258 (seconds, yellow2) / 257
     (秒后传出战场) + FormatAllItemPos — matching the capture's yellow "29". Implemented via the inventory
     `appends` with a new `flow` (PosType 9, measured width) and Text_WarningTime hidden.
  3. The 击/助 pair reads "2/4" tight in the capture because Text_Line is AutoSize=1 PosType=9: the engine
     measures the "/" and flows Text_XSNum right after it. The viewer now sizes AutoSize text to its content
     instead of the authored box.
  4. The window scale question: the capture is the window at ~1.73x (the client's UI scale: row pitch
     104 px / 60 design), centred on a 2696 px view and cropped; the authored layout matches it exactly
     (card 200..576 -> capture 13..663, table 620..1710 -> 740..2625 around the content centre 1348). No
     authored "scale" exists beyond that; the oversized look was the button/child font schemes above.
  5. Instruction overruled with evidence: 导出数据 is present in the capture (white, underlined) — it was
     kept and only restyled (button NormalFont 18), not removed.
- Evidence: cmp_leave.png composite (capture vs render, same design scale) matches on all three items;
  `--selftest` 20/1/0; `--audit` placeholders=18 unresolved=0 outOfBounds=64 (unchanged).
- Outcome: the settlement's bottom bar and row pair now follow the engine's own behaviours, proven from the
  capture + INI/Lua, not from style guesses.


### 2026-10-01 — UI — treasure pair alignment + app true scale (user follow-up)
- Slash bug: making AutoSize text content-sized also shrank its HEIGHT, so Text_Line's "/" lost
  the row's VAlign centering and floated to the top line. Fix: AutoSize sets WIDTH only; the authored
  height stays and carries the vertical centering. Rendered pair now reads "2/4"/"0/3" on one line
  (cmp_pair.png), matching the capture.
- "Overall UI way too big": the viewer canvas runs in DIPs; on a high-DPI display WPF scales it, so at
  fit zoom the panel drew at 2 device px per design px. The client draws the window in real pixels.
  MainWindow.FitZoom now caps the fit at 1 device pixel per design pixel (1.0 / DpiScaleX via
  VisualTreeHelper.GetDpi) — on a 200% display the panel shows at its true size. The manual zoom slider
  is unchanged (the user can still zoom past 1:1).
- Context from the client's own config (`C:\SeasunGame\Game\JX3\bin\zhcn_hd\config.ini` [UIEditor]):
  StandardCanvas 1280x960, Canvas 3840x2160 — the client scales its UI canvas to the display; the 7.1
  capture measures ~1.7x the design (row pitch 104 / 60), i.e. it is the window at the client's UI scale.
- Evidence: cmp_pair.png (capture vs render, same design scale) + --selftest 20/1/0.


### 2026-10-01 — UI — treasure-final: full-screen frame + 导出数据 dropped (user call)
- The window is a wide overlay (1920x700) whose veil (Image_Bg1 1920x600) spans the screen middle with the
  core content centred; the client centres the window (editor root at (screen-window)/2: 507,265 on the
  2934x1230 canvas). The viewer now supports a per-window full-screen frame: `screenWidth`/`screenHeight`
  render the frame at that size with the window centred on the neutral backdrop (App render + MainWindow
  canvas; overhang expansion is skipped when framed). treasure-final uses 1920x1080.
- 导出数据: hidden per the user's explicit call (hide Btn_Export) although the capture shows it - recorded
  as a user decision, not a research finding.
- Evidence: treasure_v10.png (1920x1080 frame, window centred, pair "2/4", countdown with yellow 29, no
  export button); --selftest 20/1/0.


### 2026-10-01 — UI — revert the full-screen frame; stage 7 trimmed to 绝境结算 (user call)
- The full-screen frame (screenWidth/screenHeight + framed render path) was judged a bad fix and reverted
  end to end: WindowInfo fields, App render path and MainWindow canvas are back to the window-rect frame
  (backdrop colour kept); the treasure entry's screen fields were removed.
- Catalog rename: 7.1 `treasure-final` cn = 绝境结算 (was 寻宝结算（绝境）).
- Viewer entries 7.2-7.5 removed per user: end-of-battle, pvp-show-final, pvp-show-final-l,
  pvp-show-final-r (their INIs/assets stay on disk; the doc rows are tagged "viewer entry removed
  2026-10-01 per user"). Catalog is now 17 windows; `--selftest` 16 rendered / 1 skipped / 0 failed;
  counts updated in ui-process-app/AGENTS.md and the inventory doc.


### 2026-10-02 — UI — 8.1 leave-menu: missing prompt restored (ExitPanel = the exit/return confirm)
- Research (ExitPanel.ini + ExitPanel.lua from PakV4): the window is the client exit/return confirm, not a
  battlefield-leave menu. 420x108, anchored TOPCENTER,TOPCENTER,0,360, ShowModeID 27,26,24,22,12,36.
  OpenExitPanel(szReason) sets Text_ExitGame from g_tStrings: close -> EXIT_QUIT (你确定要退出游戏吗？),
  loginclose -> EXIT_LOGIN_QUIT, returntologin -> EXIT_RETURN_LOGIN (你确定要返回到登录吗？), returntorole ->
  EXIT_RETURN_CHOOSE (你确定要返回到角色选择吗？). Btn_Sure exits/ReInitUI, Btn_Cancel + Esc/Enter close.
  Text_ExitGame ships WITHOUT $Text (runtime-set) - that was the missing piece in the viewer render.
- Change: catalog entry renamed cn 退出游戏; Text_ExitGame sample = 你确定要退出游戏吗？; summary/evidence/
  elements/labels rewritten from the Lua (EXIT_* + STR_SURE/STR_CANCEL). Image_Back = runtime full-screen
  dimmer (UpdateBgImageSize -> Station.GetClientSize), documented and left off in the static render.
- Evidence: exit_v2.png shows [⚠] 你确定要退出游戏吗？ with 确定/取消; --selftest 16/1/0.


### 2026-10-02 — UI — 8.1 exit: message alignment (viewer interpretation)
- User: "the text is not in right place". Verified the render against the shipped data first: ExitPanel.ini
  authors Text_ExitGame at Left=107 (glued to the icon at 76..106), Width=241, HAlign=0 (left), VAlign=1;
  the icon+box group (76..348) is centred on the 420 dialog, but every shipped reason is ~150-180px so the
  left-aligned ink hugs the icon and the composition reads left-heavy. The Lua only SetText, no alignment.
- Change: AdjustSpec gained HAlign; leave-menu uses adjust hAlign=1 so the runtime message centres in its
  authored box (icon stays at 76). Labeled a viewer interpretation in the entry/doc with re-open criteria
  (a capture of the live dialog); flagged as an inference, not a shipped value.
- Evidence: exit_v3.png (icon left, message centred, 确定/取消 below); --selftest 16/1/0.


### 2026-10-02 — UI — 8.1 exit follow-up + zoom cap reverted (user feedback)
- Icon: with the message centred in its box, the authored Image_Warning at 76 floated 50px away from the
  ink; moved to the message's left edge (adjust left=124) so `[!] + message` read as one centred group
  (the authored 76 assumed a message that fills the 241px box; no shipped reason does).
- "most panels so small now": the FitZoom DPI cap (1 device px per design px) made every panel show at
  half size on the user's high-DPI display. Reverted to the plain fit (cap 1.0 DIP); the panels are back
  to the previous size. Lesson: the cap changed every window, not just the settlement - removed.
- Re-verified the ExitPanel.ini against a fresh PakV4 extraction (byte-identical, so no stale layer).
- Evidence: exit_v4.png ([!] + centred message + 确定/取消), --selftest 16/1/0.


### 2026-10-02 — UI — commit bb10ed6; 3.1 tip area check; 5.2 minimap first pass
- Commit: bb10ed6 "UI process app: ACC_TreasureFinal 绝境结算 ... ExitPanel ... stage 7 trimmed"
  (10 files, +628/-119; the untracked string_EndOfBattle.txt stays out - its window left the catalog).
- 3.1 loading tip area: the top-right box is Handle_Tip/Text_Tip 510x101 (scheme 160 = 15px); the module's
  UpdateLayout right-anchors it at SetRelPos(clientW - w - 100, 115) - the POSITION follows the resolution,
  the box size does not (scaled only by the UI scale). At 15px the box holds ~34 CJK chars/line x 4-5 lines,
  enough for the loading stories; content = g_tLoadingStory -> minimap/loadingstory.tab per map (not in the
  local extractions, so the area renders empty). Entry adjusted to the exact 1280 anchor (670,115).
- 5.2 minimap first pass: the engine's WndMinimap behaviour implemented - defaulttexture painted in the lens,
  sharptexture (MinimapSharp.tga) as the AlphaShap mask, self marker (`image` frame `selfframe`) pinned at
  the lens centre; image overrides now target `defaulttexture` for WndMinimap sections; the entry gained the
  five map pages using the same middlemap.png art as MiddleMap. Reference for the next pass:
  proof/minimap/screenshots/05_minimap_ingame_crop.png (player-era capture) shows the live state - centred
  arrow, green vision ring (Handle_EYSOver), expanded side button columns left/right, 全图 label - the
  module parks the side containers at negative X and animates them in; replicate that state next.
- Evidence: minimap_v3.png (map in the lens + arrow + mask), --selftest 16/1/0.


### 2026-10-02 — UI — 5.2 minimap full trace: the missing piece is the Wnd_Over sub-window
- Traced MiniMap.ini + Minimap.decompiled.lua end to end. Everything the live capture shows (map name,
  全图/big-map button, the button columns, the timer) lives inside the `Wnd_Over` WndWindow, which carries
  LockShowAndHide=1 - the viewer's blanket rule hid the whole sub-window, hence "5.2 incomplete".
- Engine rule corrected: LockShowAndHide hides item-type sections (the queue tabs/badges evidence stands),
  but a WndWindow/WndFrame sub-window is shown together with its parent - the script toggles the individual
  buttons (ShowBtnCmd/UpdateBattleFieldButton/UpdateArenaButton/UpdatePVPButton/...). Evidence: the live
  capture 05_minimap_ingame_crop.png shows the Wnd_Over button columns; BattleFieldMap's Wnd_BigMap
  (also LSH=1, WndWindow) must equally show when the map is open.
- Effect: minimap 38 -> 75 sections (name + full button set + timer + map), BattleFieldMap 55 -> 76
  (Wnd_BigMap), buff-list +3, death-revive +2; placeholders drop 18 -> 11 (hidden sub-windows carried some);
  --selftest still 16/1/0. Entry summary rewritten with the Wnd_Over inventory + runtime name source
  (MiddleMap.GetMapAreaName on UPDATE_REGION_INFO).


### 2026-10-02 — UI — 5.2 lens zoom + 5.3 regression reverted (user feedback)
- 5.3 regression: the blanket "sub-windows are exempt from LockShowAndHide" rule changed BattleFieldMap
  (Wnd_BigMap appeared). Reverted to the original all-types rule; BattleFieldMap is back to its correct
  state (55/52). The minimap's Wnd_Over is now shown per-entry (`show: ["Wnd_Over"]`, the same mechanism
  the queue tabs use) instead of by a global rule change.
- 5.2 lens map: the whole-map background was wrong. The engine draws the map texture at the map config's
  [config] scale (0.02) while the middlemap art uses [middlemap0] scale (0.005867), i.e. the lens shows
  ~3.41x the middlemap art (a local region around the player). ImageOverride gained `Zoom`; the WndMinimap
  brush applies it around the centre; the pages carry zoom 3.41. The render now shows a local area
  (鸣沙山 label) instead of the whole map.
- Evidence: minimap_v5.png (local region + arrow); --selftest 16/1/0 with battlefield-map 55/52 restored.


### 2026-10-02 — UI — 5.2 minimap cleanups (user call)
- Hidden per user: Text_ct (the yellow 00:30 timer), Text_Fresher (the 分线 stub - the module sets it at
  runtime per fresher room), WndContainer_Emergency (the 鹈鹕时刻/灯火还/急救 emergency bar), Image_AIGenerate
  (the baked "生成中" label under the selfie button), and the red hot-point badges (Handle_warning, Handle_num,
  Handle_Tuisong + its Storage/Point children, Image_hotPoint, Image_HotPointSelfie, Image_HotPointCalender).
- Text_Name now shows the page's map name (龙门绝境 / 龙门绝境·夜 / 沧溟绝境 / 白龙绝境 / 天原绝境 from the page
  labels) instead of the authored 稻香村 leftover; the runtime value = MiddleMap.GetMapAreaName on
  UPDATE_REGION_INFO (the player's sub-area), so a static page can only show the map's own name.
- Evidence: minimap_v6.png (43 sections; clean buttons, map name, local map + arrow); --selftest 16/1/0.


### 2026-10-02 — UI — 5.2: remove the lens "?" icon; defaultWindow = minimap
- The big "?" inside the lens = Btn_Selfie's round icon (the AI-selfie button; its atlas glyph reads as a
  question mark) - hidden, together with Btn_Help (its "?" art also overlapped the lens) and Btn_WBLou.
- defaultWindow set to `minimap` so the viewer opens on the active 5.2 item (the working-item rule).
- Evidence: minimap_v10.png (38 sections; no ? icon; teal-pixel scan finds none in the lens), --selftest 16/1/0.


### 2026-10-02 — UI — 5.2: the green "?" is Btn_Questionnaire (correction)
- The user clarified: the target was the GREEN question mark INSIDE the lens, not the surrounding buttons.
  Restored Btn_Selfie / Btn_Help / Btn_WBLou (wrongly hidden before); the green "?" = Btn_Questionnaire
  (survey button, art SystemButton_1.UITex frame 17, 48x48, authored 0x0 -> module shows it only when a
  survey is available; the viewer drew it at (26,86) over the lens) - hidden now.
- Evidence: green-pixel scan inside the lens 83 -> 1; minimap_v11.png shows the clean lens with the
  surrounding buttons back; --selftest 16/1/0.


### 2026-10-02 — UI — 5.2: top-right corner widget removed (outside the ring)
- Clarified target: the thing outside the ring at the top right = the corner widget (Image_Coner + its
  CheckBox_Switch toggle with the chevrons) - hidden. Btn_Selfie (the round swirl at the ring edge) restored
  again; it was not the target.
- Evidence: minimap_v13.png (clean top-right outside the lens, swirl back), --selftest 16/1/0.


### 2026-10-02 — UI — 4.1 staging research: the countdown IS a KGUI window (RemainingTimeNotify)
- Answer to "do we really have nothing to display": NO. Traced the staging family via the module
  manifest: LoadingWaiting (spinner), FBCountDown (FB 10:58:59 generic), ChallengeCountDown (duel),
  TeamCountdown (team timer), PQNextStage/PQwarning (regional PQ), DynamicBattleRoyale (BR skill bar),
  and - the find - `RemainingTimeNotify` (ui/Config/Default/RemainingTimeNotify.{ini,lua}, PakV4):
  anchored TOPCENTER,0,150; Open(nSeconds) draws the remaining time as big RougeLike.UITex digit frames
  (frame = digit; verified frame 3 = the digit 3) plus SFX_Num ROUGELIKE_KILL<digit>.pss, switching the
  Image_Minute/Image_Second labels (n%60==0 -> minutes), auto-close ~5s (OnFrameBreathe).
- Also recovered `BattleFieldHSLHNotice` (黑山绝境 notice panel, 毒蘑菇即将出现/方位提示 art) - the
  mode's event notice window; the phase announcements (绝境将在30/20/10秒后开始) arrive as system
  messages on MainMessageLine (already built as 5.8); safe zone stays engine-drawn.
- Catalog: `staging-countdown` gets the RemainingTimeNotify layout (30s sample, digits 3/0, list flow,
  Image_Second shown/Image_Minute hidden; digit clones shown via the show list since Image_Num is
  LSH=1). Engine tweak: AutoSize list handles no longer wrap their items (Handle_Count is 61px for two
  32px digits; before, the digits stacked into two rows).
- Evidence: staging_v5.png (剩余时间 [3][0] 秒钟); --selftest 17/0/0 now includes the new render.


### 2026-10-02 — UI — 4.1 countdown: digits visible + label size (AutoSize images, clone alpha)
- User: the seconds label resolution looked wrong and the actual numbers were missing. Causes found:
  (1) Image_Num is authored `Alpha=0` (the parked prototype); the script's cloned digits render in game,
  our clones inherited alpha 0 -> invisible. AdjustSpec gained `Alpha`; the two clone digits get alpha 255.
  (2) Image_Minute/Image_Second are `AutoSize=1` with authored 123x41 but frame 73 is 69x41 - the viewer
  stretched them (blurry). Image creation now prefers the frame's natural pixel size for AutoSize images
  (engine semantics; the authored box is the editor box).
- Evidence: staging_v6.png reads 剩余时间 30 秒钟 with the big digit glyphs; --selftest 17/0/0;
  --audit placeholders=11 unresolved=0 outOfBounds=57 (unchanged totals).


### 2026-10-02 — UI — 4.1 countdown pages (5/10/15/20/30 s)
- Added five viewer pages to `staging-countdown`, each setting the clone digit frames
  (frame = digit; verified frame 5 = the "5" glyph) and hiding the tens clone for the single-digit
  5s state (the hidden clone drops out of the list layout, so the lone digit centres itself).
- Evidence: stage_p5.png reads 剩余时间 5 秒钟; 10/15/20/30 render the two-digit states; --selftest 17/0/0.


### 2026-10-02 — UI — 5.4 loot-list re-decoded from the client (money row, exact quality colors, item flow)
- Re-extracted LootList.{ini,lua} (byte-identical to the assets) and decompiled the Lua end to end:
  (1) the money row is a real runtime row (Handle_Money appended when GetLootMoney>0; UpdateMoneyShow splits
  gold/silver/copper, shows Image_Gold/Silver/Copper (LootPanel frames 11/10/12) and sets Box_Money icon =
  GetMoneyIcon: gold>10 -> icon 94 System\Coin\coin01 (ui_extensions.lua), silver 96/97, copper 98/99);
  (2) item names use SetFontColor(GetItemFontColorByQuality) - exact engine RGB: q1 (250,250,250),
  q2 (0,210,75), q3 (0,126,255), q4 (255,45,255), q5 (255,165,0) (ui/script/item.lua) - the earlier
  green2/blue2 guesses were close but not the shipped values; (3) rows are engine items: PosType 10 under a
  handle with FirstItemPosType 2/4, which APPENDS below the previous item - the viewer now implements that
  (Attach case 10 branches on the parent's FirstItemPosType) and ApplyListTemplates no longer overrides
  PosType-10 prototypes, so the view stacks money row + clones in append order.
- Viewer: Fonts.TryGetColor accepts #RRGGBB (engine Lua RGB values); extracted LootPanel + the six Coin
  atlases from the pak.
- Evidence: loot_v3.png (money 12/34/56 + coin icon, 麻布绷带 green #00D24B, 月影沙 blue #007EFF, correct
  row order y=40/93/146); --selftest 17/0/0; --audit outOfBounds 57->58 (row stacking), placeholders=11.
- Known gaps (documented in the entry): Box over-text stack overlay and UpdateItemBoxExtend box art
  (borderColor approximation), money coin glyphs overlap at the authored PosType 8 spot.


### 2026-10-02 — UI — 5.4 money row: the engine item flow lays it inline
- User: the money texts and the gold icon were in the wrong place. Cause: the money children use
  PosType 8 (Image_Gold, Image_Silver, Text_Copper, Image_Copper) which the viewer treated as
  window-right (the documented rule for plain handles), parking the glyphs at the panel edge and
  hiding the copper behind one. In an ITEM-FLOW handle (FirstItemPosType != 0, Handle_Money = 4) PosType 8
  means "continue the previous item's line": the row lays out as 12 [gold ingot] on line 1 and
  34 [silver ingot] 56 [copper coin] inline on line 2, all right-aligned to x=197. The frames: LootPanel
  10/11/12 = silver ingot / gold ingot / copper coin (verified by frame dumps).
- Viewer: Attach case 8 now continues the previous item when the parent has FirstItemPosType != 0 (the
  window-right rule still applies to plain handles - the queue/飞沙令 validation stands).
- Evidence: loot_v4.png (12+ingot / 34+ingot 56+coin, money row first, items below); --selftest 17/0/0.


### 2026-10-02 — UI — 5.4 rarity frame fully visible (Box overlay)
- The rarity border was a wrapping Border around Image_ItemBg: the icon art (Box_Item, drawn later
  in the row, with its own full-bleed dark background) covered the frame's top line and the row
  divider covered the bottom. Moved the rarity frame onto the Box_Item and made it an overlay Grid
  drawn ON TOP of the icon (icon inset by the border thickness) - all four sides now visible.
- Verified by pixel probe: top/bottom/left/right of the green frame all read (0,210,75) = #00D24B;
  --selftest 17/0/0.


### 2026-10-02 — UI — 1.1 queue-panel: activity badges off + GT-measured 个人评分 / ? offsets
- User: remove the 周年 tab badges; the 个人评分 row and a question-mark icon sat wrong.
- Badges: UpdateAnniversaryTabIcon (NewBattleFieldQueue.decompiled.lua:6506-6553) shows
  Image_AnniversaryIcon1/2/3 (PartnerTeam.UITex frame 21 = 周年 / 23 = 赛季, LockShowAndHide=1); the
  inventory previously pinned them via a window-level `show` list. Removed, so the LSH default keeps
  them hidden (user decision 2026-10-02: the queue window renders without the activity badges; the
  reference capture happened during the anniversary event).
- Offsets measured on the live reference (Screenshot-given-1.png; 1720/960 = 1.7917): the 个人评分
  label ink starts at 25.5 (app 30.5) while 1873 and its ? match within 1px - the engine's
  format-table spacing sits the AutoSize label at parent-11 (authored -6). The 随机地图 ? (Btn_Rull_DS)
  measures box 610 (authored 605), and the whole 技能平衡 row (? Image_BuffRule, gem Box_Buff, right-
  aligned Text_Buff) sits 9px right of the authored WndContainer_Buff (744 -> 753).
- Viewer inventory: three `adjust` entries (Text_MyScore left=-11, Btn_Rull_DS left=601,
  WndContainer_Buff left=753). Provisional (AdjustSpec sets the authored Left, not canvas x):
  re-open when the KGUI FormatAllItemPos item-spacing algorithm is RE'd - the engine positions
  these from its own font metrics + format rules, the viewer previews the authored INI.
- Verified: ink-column probes before/after - label GT (25..37)(41..53)(55..68)(71..83)(88..89) vs APP
  (25..38)(41..53)(56..68)(71..83)(88..89); 随机地图 ? GT 619..624 vs APP 618..624; 技能平衡 row ?
  GT 919..924 vs APP 917..923; --selftest 17/0/0.
- Known minor gaps: Box_Buff gem art lacks the reference's bright sparkle (icon 10000 frame), and the
  个人评分 row sits ~2px high vs the reference (within downscale noise; not adjusted).


### 2026-10-02 — UI — 真传模式 queue page (design variant based on 五人模式)
- User: on the queue page remove the 3 tooltip ? icons + 技能平衡, reduce the mode strip to a single
  tab 经典模式, rename the bottom-right 快捷组队 to 绝境武学, and expose the result as a NEW page
  真传模式 loaded by default - all based on the current 五人模式 (Page_DesertStorm) state.
- Implementation is viewer-only: new `zhenzhuan-queue` entry (cn 真传模式, status DESIGN) in
  Data/ui_inventory.json - same INI/page/sample state as queue-panel plus:
  tabs.show reduced to CheckBox_DesertStorm with Text_DesertStorm overridden to 经典模式;
  hide "Image_Rule_4,Btn_Rull_DS,Image_BuffRule,WndContainer_Buff"; text override
  Text_BtnQuickTeam_D = 绝境武学; the queue-panel's Text_MyScore adjust kept. No INI or asset edits.
- This is an intentional project design deviation (NOT client truth) - flagged in the entry summary,
  the §1 note in docs/netcode/JX3_MODE_UI_INVENTORY.md, and here, so it can never be mistaken for the
  shipped queue window.
- Verified: zz_v1 dump - only CheckBox_DesertStorm/Text_DesertStorm (经典模式) drawn, no Image_Rule_4 /
  Btn_Rull_DS / Image_BuffRule / WndContainer_Buff sections, Text_BtnQuickTeam_D = 绝境武学;
  --selftest 18/0/0; defaultWindow = zhenzhuan-queue.


### 2026-10-02 — UI — 单场奖励 plaque: ImageType=11 decoded (caps were flattened by the stretch)
- User: "the effect below 单场奖励 is a bracket, that thing is broken on right side, it doesn't close,
  left is fine" - the reward plaque's pointed caps.
- Root cause: Image_DoubleBg_5 (PVPUI22 frame 11, 48x20) is drawn at Width=68 with ImageType=11; the
  viewer plain-stretched the whole frame, which widens the pointed caps and flattens their slopes so
  the right cap reads as open. The engine's draw dispatch (KGUIX64 0x180117D7C, `mov eax,[rbx+0x410]`)
  puts ImageType 10/11/12 (and 17/18/19) on ONE diced path, and the live capture keeps the caps at
  native size - only the middle stretches. Frame dumps: PVPUI22 frame 8 (68x20) is the same art at the
  authored size, i.e. a native-caps/wide-middle version of frame 11.
- Viewer: new HorizontalSliceImage (left/right caps fixed, middle star-stretched) + UiTex
  GetHorizontalCaps - the cap widths are detected from the frame's vertical profile (alpha when the
  art is translucent, color when opaque). Lesson: the --frame dump composites over an opaque host, so
  a translucent frame's alpha is invisible there (PVPUI22's plaque is a ~alpha-45 black veil; the
  dump read (13,13,13) vs (16,16,16) - the shape only shows in the render's own pixels).
- GT-verified: caps 10/10 for frame 11; diff before/after is confined to the plaque bbox
  (447,575)-(513,595); the left cap and the closing right cap now match Screenshot-given-1.
- Scope check: only this element changes across the whole catalog (the selftest's type-11 debug:
  Image_Line, Image_New_2_2_2_0, MiddleMap Image_Alpha/_1 all detect caps=0 -> unchanged plain
  stretch); --selftest 18/0/0. UI_SYSTEM_REPORT.md item 3 updated (11 = horizontal three-slice).


### 2026-10-02 — UI — catalog rename pass (排队界面, 载入窗口, ...) + queue-panel entry removed
- User: remove the queue-panel catalog entry (the faithful 五人模式 replica, 1.1); rename the designed
  真传模式 page to 排队界面; and rename the other catalog items to short Chinese names: 载入窗口,
  起飞倒计时, 大地图, 小地图, 战场地图, 攻击/防御数值变动, 顶部信息, 团队列表, 重伤提示, 结算界面,
  离开确认 (the items not listed - 匹配成功确认框, 拾取窗口, 对局统计行, 增益/减益/目标增益 - keep their
  names).
- Viewer data only: the queue-panel entry was deleted (its GT-measured `adjust` values live on in the
  zhenzhuan-queue entry; the research stays in the §1 note and the earlier EXPERIENCES entries);
  zhenzhuan-queue cn = 排队界面 (id kept, still defaultWindow); the rest are `cn` edits in
  Data/ui_inventory.json. No INI/asset or engine changes.
- Docs: JX3_MODE_UI_INVENTORY.md §1 note records the rename + removal; ui-process-app/AGENTS.md and
  README gate counts refreshed (16/1 -> 17/0).
- Verified: --selftest 17/0/0; zhenzhuan-queue render intact (68 sections); the tree lists
  1.1 排队界面 / 1.2 队列追踪窗口 and the renamed stages.


### 2026-10-02 — UI — single-list relist (all items 1.x) + 重伤提示 options removed
- User: "relist, they all become 1.x" - merge the 8 flow stages into one catalog list so every window
  numbers as 1.1..1.17 (the app numbers stage.window); and for 重伤提示 (death-revive) remove the
  原地疗伤 and 复活点复活 options because the mode now allows both actions directly.
- Viewer data: ui_inventory.json stages merged into one stage (id `ui`, title 绝境战场界面, flow summary;
  the window entries themselves are byte-identical, order = queue -> match -> loading -> staging -> hud
  -> death -> settlement -> exit). death-revive gains `hide: "Btn_Sure,Btn_Cancel"` (the dump showed
  Text_Sure = 原地疗伤, Text_Cancel = 复活点复活) + a design-variant note in its summary.
- Lesson: the stage merge is text surgery on the hand-formatted JSON - the first attempt cut the file's
  trailing root brace (lines[779:] was empty); validate with json.load BEFORE writing.
- Verified: --selftest 17/0/0; death-revive render 26 -> 20 sections with no Btn_Sure/Btn_Cancel drawn
  (only the 重伤 icon + 空格键或点击队友头像... notice remains).


### 2026-10-02 — UI — 备用 stage: four windows moved out of the main list
- User: move the windows formerly at 1.2, 1.10, 1.11, 1.14 into a 备用 (spare) stage as 2.x.
- Viewer data: second stage `spare` (title 备用) added after 绝境战场界面; the moved entries are
  2.1 队列追踪窗口 (map-queue), 2.2 攻击/防御数值变动 (fighting-num), 2.3 对局统计行
  (fighting-statistic), 2.4 团队列表 (teammate) - byte-identical blocks, order kept. Main list is now
  1.1..1.13 (排队界面 .. 离开确认).
- Same text-surgery method; this time json.loads validated the rebuilt text BEFORE writing (the earlier
  lesson), and the first splice attempt failed validation (double stage-close brace) so nothing was
  written.
- Verified: --selftest 17/0/0; tree shows 绝境战场界面 1.1..1.13 + 备用 2.1..2.4.


### 2026-10-03 — UI — BASIC UI session 1: full Config/Default inventory + extraction
- User approved the basic-UI deep-research plan (full default UI scope; verification against INI/authored
  state only - no GT captures; new catalog stage 基础界面; HUD core first).
- Inventory: every `ui\Config\Default\**.lua` entry in the tracked manifest
  (proof/netcode/ui_lua_probe/out/ui/module_info.xml) converted to its candidate INI path -> 1,365 paths;
  extracted with the official PakV4 extractor (`tools/netcode/extract_pak_paths.py --batch 250`, new
  `--batch` option for bulk sweeps) -> **1,210 HIT / 155 MISS** (MISS = script-only/helper entries with
  no same-path INI). 1,079 of the 1,210 were new locally (assets/ui held 131 of them).
- Deliverables: docs/ui/BASIC_UI_INVENTORY.md (scope register: coverage summary, class tables for HUD/
  panels/menus/world, the 790-row unclassified tier-5 list, excluded classes, MISS appendix, reproduce
  commands) registered in docs/ui/README.md; evidence under proof/ui/basic_ui/ (extracted_inis.tsv,
  missing_inis.txt, candidate_inis.txt, extract.log, SOURCES.txt); INIs copied to the ignored
  ui-process-app/assets/ui/Config/Default (1,211 files).
- Catalog: new empty stage 基础界面 inserted as stage 2; 备用 is now stage 3 (3.x). The research sessions
  fill 基础界面 group by group (HUD core first).
- Classification is a documented working heuristic (first-match regex); per-group sessions refine it.
- Verified: --selftest 17/0/0 (17 windows unchanged); JSON stages = 绝境战场界面 / 基础界面 / 备用.


### 2026-10-03 — UI — BASIC UI session 2: HUD core deep research + first 基础界面 entries
- 54 HUD Lua extracted from PakV4 + decompiled (unluac); corpus kept local-ignored at
  proof/ui/basic_ui/decompiled (new .gitignore entry). Target family INIs (29) extracted into assets.
- Doc docs/ui/BASIC_UI_HUD.md (registered): MainBarPanel (93 sections; slot strip geometry = 157+14*46+113
  = 914 = bar width; TGA probe: left cap alpha-67 plate, middle/right frames transparent - slot icons are
  runtime skill data), Player (88 sections, sample texts baked in), Target family - composition decoded:
  Target.lua opens TargetPlayer10/11 or Target<Intensity><Relation>(+S) and appends TargetCommon.ini
  sections into Handle_Energy per kungfu (AppendItemFromIni, Target.decompiled.lua:967-989); remaining
  HUD register + native-driven caveats (cast bar CASTINGBAR has no Lua consumer).
- Entries: 2.1 main-bar, 2.2 player-frame, 2.3 target-frame (PARTIAL, backdrop #33393E - the HUD art is
  dark/translucent and invisible on #101010). Gaps documented: runtime slot icons, Player values/buffs,
  TargetCommon append (no multi-INI append in the viewer yet), ChatPanel rows, progress-bar behavior.
- Engine: UiLayout case 8 (PosType 8) now honors an explicitly authored/overridden Left (even 0) - the
  main bar's strip authors no Left and got parked at the window's right edge otherwise; BR window-right
  still applies when no Left key exists.
- Verified: --selftest 20/0/0; renders mb_v4/pf_v2/tf_v2 (bar frame + kungfu box; player frame with
  sample texts; target frame bars).


### 2026-10-03 — UI — BASIC UI bulk bring-in: 991 authored-state entries (catalog now 1,008)
- User: "thats not enough, i need way more UI". Generated catalog entries programmatically for every
  in-scope window (classified groups 201 + the full tier-5 other set 787) on top of the hand-built
  trio: entry = INI's authored state (id/title/path/backdrop #33393E + generated summary with section
  count and root geometry), status PARTIAL, no Lua replay, no GT capture (user-approved policy).
- Catalog: 13 BR + 991 basic UI + 4 spare = **1,008 windows**; --selftest 1008/0/0.
- Two authored-state edge cases fixed: AccelerateBall and HLBOp_Main author LockShowAndHide=1 roots
  (runtime-shown bar / invisible 0x0 anchor host) - "Layout INI has no sections" came from the LSH
  filter dropping the whole subtree; both entries now carry show lists. Lesson: that error message can
  mean the FILTERED set is empty, not the file (the plan pipeline applies LSH after loading).
- Docs: BASIC_UI_INVENTORY.md gains the bulk bring-in section; AGENTS/README gate counts -> 1,008.
- Verified: full --selftest 1008/0/0 (one earlier FAIL hlbop-main fixed via show; re-run green).


### 2026-10-03 — UI — catalog curated: 3 basic-UI stages + official Chinese names (备用 last)
- User: "thats way too many, i need their official chinese name, and you may need to catalogize them by
  2.x 3.x 4.x; keeping 备用 as last still."
- Dropped the 787 tier-5 "other panels/dialogs" entries; the 204 classified windows are regrouped into
  three stages: 2. 基础HUD (49 = 46 HUD + the hand-built trio), 3. 功能面板 (107), 4. 菜单与交互 (48);
  备用 stays the last stage (5). Catalog 221 windows; --selftest 221/0/0.
- Official Chinese names: per-window title text ($Text of Text_Title/*Title sections) resolved through
  each window's own StringTable (49 `ui\Scheme\Case\*.txt` extracted for this pass, GBK) + the global
  g_tStrings; 96 windows got an official cn (角色/背包/武学/交易行/储物箱/帮会/系统设置/快捷键设置/
  表情动作/...). Lesson: the fresh per-window string tables are GBK (the Data/text copies are UTF-8);
  console pipes mangle CJK - write evidence files via cmd redirection or Python raw bytes.
- Windows whose data has no authored title (most HUD elements) keep the English title (no invented
  translations); per-window refinement can add descriptive names later.
- Correction (same day): the user asked why the amount decreased - "way too many" meant too many in ONE
  stage, not "delete them". The 787 tier-5 entries were restored as stage 5 其他界面 (before 备用, which
  stays last) and the same official-name pass was run over them: 86 more StringTables extracted
  (80 HIT / 6 MISS; strip leading "/" - a leading slash MISSes in PakV4), 321 of 787 named. Catalog back
  to 1,008 windows; --selftest 1008/0/0 (hlbop-main needed its show list re-added - the regeneration
  does not carry per-entry overrides). Total official names: 417.


### 2026-10-03 — UI — 推荐 stage: curated review shortlist (25 windows)
- User: "sory things out as 推荐, you reecomand me som of the panels we will see if those are what i
  need" - a review shortlist of the core UI.
- 25 entries MOVED from the groups into a new first stage 推荐: HUD core (主技能栏, 玩家状态框,
  目标框（玩家）, 聊天窗口, 任务追踪, CompassPanel, ExpLine, 地图) + main panels (角色, 背包, 武学,
  社交, 邮件, 交易行, 储物箱, 团队, 帮会, 交易, 坐骑槽位, 宠物秘鉴) + settings (系统设置, 快捷键设置,
  界面设置, 主界面自定义模式, 系统菜单). Groups shrink accordingly (42/96/41); 其他界面 787 and 备用
  stay; catalog still 1,008; --selftest 1008/0/0.
- CompassPanel / ExpLine have no authored title string - kept English (no invented translation).
- Stage rebuild lesson: when moving blocks between stages, re-emit each stage's remaining blocks with
  recomputed trailing commas (a removed last block leaves a dangling comma otherwise); validate with
  json.loads before writing.


### 2026-10-03 — UI — coverage audit: what is left out (and the 29 lua-less INIs added)
- User: "any UI we have left out?" Audited catalog vs the 1,240 local Config/Default INIs: 1,008
  cataloged -> 233 uncatalogued. Of those, 204 are deliberate scope exclusions (login/entry 31, BR
  sub-panels 50, arena/JJC 23, other modes/minigames 77, housing 20, debug 3) and 29 are lua-less INIs
  not in the manifest candidate set (the Target frame variants + ReputationPanel).
- Added the 29: 28 target-frame variants (TargetCommon, Target10..42 + S, TargetPlayer10S/11/11S) to
  基础HUD and ReputationPanel to 功能面板. Catalog 1,037; --selftest 1037/0/0.
- Remaining known gap: the 155 manifest script entries with no same-path INI (helpers/data, not
  windows; `proof/ui/basic_ui/missing_inis.txt`), plus the general caveat that the pak has no
  enumerable INI listing - further lua-less INIs like the Target family may exist and get probed per
  need. The audit table lives in docs/ui/BASIC_UI_INVENTORY.md (Coverage audit).


### 2026-10-03 — UI — 推荐 review pass: render audit of all 25 + panel register doc
- Rendered all 25 推荐 windows and measured content vs the backdrop: 24 render authored content;
  raidpanel was nearly empty (content 0.04) because its tabs/member slots are LockShowAndHide=1 -
  fixed with a show list (CheckBox_Team1..5 + Image_Member1..5 + title/minimize; 49 sections drawn).
  main-bar stays frame-only (slot icons = runtime skill data, documented).
- New doc docs/ui/BASIC_UI_PANELS.md (registered): per-panel INI facts (sections/root/pages/LSH),
  authored-vs-runtime split and review notes for the shortlist panels (CharacterPanel 882 sections/9
  pages, GuildMainPanel 1625/15, UISetting 1749, WorldMap 1061/469 LSH, ...).
- Next fidelity step per panel: replay one sample row/slot per runtime list once the row templates are
  identified (rows are created by each panel's Lua).
- Verified: --selftest 1037/0/0; raidpanel render 49 sections.


### 2026-10-04 — UI — viewer X key: reject a view into 不需要 (not needed)
- User: "pressing x to set a view to be in category 'not needed', to reject one from the list".
- Implementation: new `RejectionStore` (side file `Data/rejected.tsv`: windowId TAB originalStageId) -
  X in the viewer moves the currently shown window into a `不需要` stage appended at the bottom of the
  tree; X again restores it to its original stage (the side file keeps the catalog JSON untouched and
  the action reversible). The tree rebuilds (numbers update) and the window is re-selected.
  Headless equivalent for scripting/testing: `UiProcessApp.exe --reject <windowId>` (toggles).
  Key handler ignores presses while a TextBox has focus or with modifiers.
- Verified: `--reject characterpanel` twice -> rejected (not-needed=1) then restored (not-needed=0),
  rejected.tsv back to header-only; --selftest 1037/0/0 (JSON untouched).


### 2026-10-04 — UI — X key "doesnt work" fix: Chinese IME + startup focus
- User: "doesnt work" (the X reject key in the viewer). Two input-path causes fixed:
  1) with a Chinese IME active the letter arrives as Key.ImeProcessed (the real key is in
     ImeProcessedKey) - the handler now accepts both; a press was seen working once (a user press
     wrote questtracelist to rejected.tsv) which fits an intermittent IME-off/on path.
  2) startup keyboard focus stayed on the search box, whose guard ignores letter keys - OnLoaded now
     calls StageTree.Focus() so X works without an extra click.
  Also made rejected.tsv loading BOM-tolerant (PowerShell Set-Content -Encoding UTF8 writes a BOM).
- Testing note: driving the GUI key path programmatically needs real foreground (SetForegroundWindow
  is blocked by the foreground lock; SendKeys without the check can type into the wrong window) - the
  guarded send_x.ps1 aborts unless the foreground PID matches; verify by hand or via the headless
  --reject path.
- Follow-up (same day): the user clarified the intended flow - after rejecting, the selection must
  advance to the NEXT window in the list (previous one at the end) so a run can be rejected without
  scrolling back. The handler now computes the flat tree order before the toggle and selects the
  successor (fallback: the moved window).


### 2026-10-04 ? UI ? viewer render speed: shared caches + layout cache + next-window prewarm
- User: "it takes a while, can we do faster" (the X sweep re-rendered every next window).
- RenderLayout refactor: AssetResolver/UiTexCache are now shared for the app session (atlas TGAs
  decode once), INIs are memoized (32-entry), built layouts are cached per (window, page, wireframe,
  hide) with a 6-entry cap, and after each render the NEXT catalog window is built on the dispatcher's
  Background priority (same pure builder - no UI reads) so the X sweep renders from cache. The
  AssetNote line shows build=NNN ms / (cached).
- Verified: build clean; app starts and stays responsive (pid check). First visits still build (big
  panels take their time); repeats and prewarmed neighbours are instant.


### 2026-10-04 ? UI ? catalog everything: the 204 previously-excluded INIs added (1,240 windows)
- User: "keep getting more UI". Added every remaining local Config/Default INI (login/entry 31, BR
  sub-panels 50, arena/JJC 23, other modes/minigames 77, housing 20, debug 3 = 204) as a new stage
  6 ??????? (before ??), with the same official-name pass (18 more StringTables extracted,
  16 HIT; 73 of 204 named). Catalog 1,240; --selftest 1240/0/0.
- Mid-wave mistake + fix: a scan for "invisible script hosts" (root 0x0 + LSH=1 + MousePenetrable=1)
  used a regex that swallowed the WHOLE file's keys (so later sections' LockShowAndHide leaked into
  the root's key set) and dropped 11 real windows (incl. DLCPanel 2,049 sections) and then added 442
  bogus `show` lists. Reverted: a single-section-aware parser (first section only) proved only
  accelerateball + hlbop-main genuinely have LSH=1 roots; the 10 dropped windows were restored (9
  without show, HLBOp_Main keeps its show) and their summaries regenerated. HomelandEventHandler.ini
  (single 0x0 LSH=1 section, no content) stays dropped as the only bare host.
- Lesson: parse INI roots with an explicit "first section only" loop, never a `[\s\S]*` grab; and
  validate a bulk show-list change on a sample render before trusting the scan.


### 2026-10-04 ? UI ? deep art extraction: 1.5 GB of atlases into the viewer texture root
- User: renders still placeholder-heavy -> pull the referenced art from the client paks.
- Scanned all 1,240 INIs: 1,300 `UITex` references + 578 direct texture names. Pass 1 extracted the
  1,338 missing referenced files; pass 2 parsed each `.UITex` header for its sibling `TextureName`
  (offset 24) and pulled 1,046 more. `ui-process-app/assets/uitex` (git-ignored) went 154 -> 2,479
  files (~1.5 GB); shortlist renders now show real frames/badges/icons.
- Post-art `--audit`: placeholders=899 unresolved=3262 outOfBounds=6533 across the 1,240 windows.
- Lesson: extracting the `.UITex` sibling textures (not just the referenced atlases) is what
  converts most placeholder frames; non-ASCII texture names were skipped in pass 2.


### 2026-10-04 ? UI ? recommended page defaults + BigBagPanel normal-mode render
- User: "right now everything is messed up" (the 推荐 shortlist after the art unlock).
- Page defaults (viewer `page`, not INI edits) for the multi-page panels: characterpanel
  Page_Equipment, newskillpanel Page_Kungfu, socialpanel Page_Friend, mailpanel Page_Receive,
  auctionpanel Page_Business, horsepanel Page_Horse, newpet Page_MyPet, questtracelist
  Page_QuestTraceList, guildmainpanel Page_OverView. GuildMainPanel 783 -> 201 sections.
- BigBagPanel was the worst: the authored state is the extended-mode geometry (605-wide filter rows,
  PosType 10 Handle_Total centered by the viewer, six category rows stacked by the engine's
  FormatAllItemPos and unclipped over the lower controls). Fixed from `BigBagPanel.lua` (module
  defaults bCompact=false / aOpen all true / nFrameW 440 nFrameH 410 L16-36; init UpdateSize L6788):
  root width 440, backgrounds/title resized to the frame, Handle_Total pinned (0,0),
  Handle_Bag_Compact + Wnd_Dismantling hidden, Text_Bag2/3/4 shown, filter checkboxes at container
  origin, Handle_Bag_Normal clipped to its 330x111 scroll viewport. Render 166 -> 145 sections,
  content 0.277 -> 0.477, canvas 443x449 -> 445x411.
- New viewer `adjust` fields `posType` and `clip` (ClipToBounds via `$Clip`); both documented in
  Engine/LayoutPlan.cs with the Lua lines.
- Gate: --selftest 1240/0/0; --audit placeholders=899 unresolved=3262 outOfBounds=6533.


### 2026-10-04 ? UI ? recommended shortlist expanded 25 -> 61 (core HUD + panels + menus)
- User: "recommand more". Promoted 36 windows into stage 1 推荐 (JSON move; catalog stays 1,240):
  HUD 15 (targettarget/targetbuff/targetdebuff/debufflist/teambuff/teamswitchbtn/worldmark/cdprocess/
  combopanel/fullscreenwarning/accelerateball/topbuff/bufffold/targetresourcebar/targetskill),
  maps (minimap/middlemap), panels 11 (charinfo/matrix/kungfupanel/craftpanel/teambuilding/
  teambuildingplayerset/rankingpanel/reputationpanelnew/guildbankpanel/petpanel/personalcard-showdata),
  menus 8 (emotionpanel/videosettingpanel/soundsettingpanel/dialoguepanel/activitypanel/
  achievementpanel/questbar/camppanel). Stage summary updated; the six already-rejected windows
  (auctionpanel/compasspanel/guildmainpanel/mailpanel/newpet/questtracelist) stay in 不需要.
- Move mechanics: text surgery on the stage `windows` arrays (extract top-level element spans per
  stage, rebuild with the original 8-space element indent) - a json.dumps round-trip reformats short
  arrays (`"show": [ "x" ]`) and would have diffed the whole 872 KB file.
- Verified: --selftest 1240/0/0; GUI relaunched, 推荐 now 61 windows.


### 2026-10-04 ? UI ? reviewer confirmed 推荐 1.1-1.38 -> new stage liked (2.x) + 20 more promoted
- User: "1.1-1.38 will move to liked, which is the new 2.x" / "add 20 more recommended".
- The 38 confirmed visible items (main-bar ... matrix; the six rejected windows are excluded from
  the visible list) moved out of 推荐 into a new stage `liked`, inserted as stage 2 (2.x). 推荐 (1.x)
  now holds the 17 remaining candidates + 20 newly promoted: actionbar/actionbarbind/buffmonitor/
  targetfaceset/targetcommon, charinfomore/equipmentshare/viewequip/dismantle/formationpanel/
  guildlistpanel/goldteam/addfriendpanel/friendrank/toybox, questguide/systemmenu-list/
  systemmenu-right/activitysignin, partnerteam. Catalog still 1,240; stages: recommended 43 (37
  visible), liked 38, ui 11, hud-ui 48, panels-ui 76, menus-ui 29, other-ui 780, modes-ui 211, spare 4.
- The tree numbering is 1-based per stage (MainWindow.BuildTree), so "1.1-1.38" is the displayed
  numbering of the visible (post-reject) list; the boundary was confirmed with the user before the
  move (last item = matrix, 奇穴).
- Verified: --selftest 1240/0/0; GUI relaunched.


### 2026-10-04 ? UI ? fidelity plan P1-P3: status scan + badges, contact sheet, TargetCommon append
- User: "i want you to go for all 3, and then check for completion" (P1/P2/P3 of
  docs/ui/UI_RENDER_FIDELITY_PLAN.md).
- P1 `--status` -> `ui-process-app/Data/render_status.tsv` (per window: size/sections/elements/
  leaves/placeholders/unresolved/outOfBounds/pages/page/lsh/runtime-hosts/flags) + stage totals.
  The viewer loads it: status bar shows `status=<rows>`, the AssetNote line appends
  `status: ph=.. str=.. oob=.. flags`, and tree tooltips carry the flags.
- P2 `--contact-sheet <stageId|title|number> [--out sheet.png] [--cols N] [--max N]`: labeled
  thumbnail grid for batch review; rejection-aware numbering (reads Data/rejected.tsv so X.Y matches
  the viewer and the title shows the hidden count). recommended sheet = 14 windows after the
  reviewer's 34 rejects.
- P3: `appendIni` engine support (`LayoutPlan.ApplyAppendIni` + `WindowInfo.AppendIni` +
  ScriptShown merge) and `target-frame` now renders TargetPlayer10 + TargetCommon's Handle_TM under
  Handle_Energy (36 -> 46 sections; sample kungfu 唐门). Runtime-shell markers delivered through the
  P1 flags (`shell`, `runtime-hosts=N`, `pages=N no-default`). Remaining: per-list sample rows (the
  scan quantifies 615 runtime-host windows) and generalized scroll clipping.
- Completion check: placed=1240 shell=134 runtime-hosts=615; selftest 1240/0/0; rejections respected.
  Fixed stale `originalStageId` in rejected.tsv for 16 windows the catalog had moved to liked, so an
  X-restore returns them to liked (not recommended).
- New shared helper `App.BuildWindowPlan` (status/contact-sheet pipeline) + `App.LoadIniTolerant`.
- Verified: --selftest 1240/0/0; --status; --contact-sheet recommended (14 windows); target-frame
  46 sections; GUI relaunched.


### 2026-10-04 ? UI ? correct-system pivot: systemic census + engine dispatch + script-replay feasibility
- User: "EVERY WINDOW ... was all wrong, no case by case fix is allowed, you have to make the correct
  system". Stopped per-window patches; diagnosed the systemic gaps over all 1,240 INIs.
- Census (new tool `tools/ui/ini_construct_census.py`, registered in docs/ui/README.md):
  WndPage/WndPageSet 513 sections, list/tree types 152, scene/web ~86 (rendered as generic
  containers today = approximate); PosType 3/4/5 (70) unhandled; HandleType 1/2/4/5 (137) unhandled;
  FirstItemPosType 1/2/3/4/7/8/9 (98) approximated as a boolean; AnchorDst special 848 + relative 826.
- Engine evidence: client KGUIX64.dll 0x180117D7C (`cmp eax,0xa/0xb/0xc/0x11/0x12/0x13 -> je
  0x180117CE7`) puts ImageType 10/11/12/17/18/19 on ONE diced path -> the viewer's ImageType-12
  plain stretch was wrong (289 sections). Fixed in UiLayout (unified diced condition).
  AnchorDst=client (847 sections / 548 files, mostly window roots) fell back to the parent basis ->
  fixed (client = window/client rect for standalone renders).
- Scripts: 121 same-name window scripts (38/53 liked, 21/48 recommended) mutate runtime state; the
  viewer replayed a handful by hand. Decompiled 132/133 with unluac 1.2.3 into
  `proof/ui/basic_ui/decompiled/` (git-ignored). Compile-audit with the client's bundled LuaJIT
  (lua51.dll via ctypes, verified running Lua 5.1): 148/186 compile, 38 fail with unluac label-scope
  bugs (CharacterPanel/BigBagPanel/NewSkillPanel/GuildMainPanel/KungFuPanel/WorldMap/Target/
  ActionBar/Minimap/QuestTraceList). Blocker for execution; options: newer unluac, label repair.
- New doc `docs/ui/UI_RUNTIME_REPLAY.md` (registered): the correct system = KGUI conformance
  (engine-derived, census-gated) + script runtime-state replay + status dashboard; no per-window
  fixes. Work order: decompiler fix -> replay harness -> conformance pass in census order.
- Verified: --selftest 1240/0/0 after the engine fixes; census tool output (14 unhandled variants).


### 2026-10-04 ? UI ? script replay WORKS: PUC Lua 5.1 built, original bytecode runs, mutations captured
- The "correct system" breakthrough: the extracted window scripts are standard Lua 5.1 bytecode
  (ESC "Lua" + version 0x51; 32-bit header: int/size_t/instr = 4, number = 8). LuaJIT refuses PUC
  bytecode and the client's own VM (`Engine_Lua5X64.dll`) does not export the Lua API, so **PUC Lua
  5.1.5 was built from source** (lua.org tarball, MSVC x86 via vcvars32) - the ORIGINAL compiled
  scripts now load and run; the unluac decompiler (and its label-scope bugs) is not needed.
- `tools/ui/replay_harness.lua`: parses the window INI into a section tree, exposes recording UI
  proxies, sets `_G.this` (the engine's event global - the scripts call `this:Lookup`, not the arg),
  runs the module chunk and calls `OnFrameCreate`, writes a `section/method/args` TSV.
  Verified: BigBagPanel -> **234 mutations** (SetSize 594x624 extended branch, Handle_Bag_Normal/
  Compact SetSize, Show/Hide, SetRelPos, Check, FormatAllItemPos); the module is the global
  `_G.BigBagPanel` (41 functions; nFrameW=440 / nFrameH=410 / nExtendFrameW=594 as hand-extracted).
- Remaining: stub-tune data branches (the bag run took the extended path because a stubbed branch
  made `bExtendPackage` truthy; user-setting defaults must be pinned), then emit
  `Data/runtime_state/<window>.tsv` and make the viewer consume it.
- Debug tooling that cracked it: patched `lvm.c` prints `ARITH op/regs/types/PC` on arithmetic
  errors; `string.dump` + `unluac --disassemble` maps the failing instruction; traceback function
  names come from the bytecode's debug info; `debug.getupvalue`/handler inspection located the
  `this`-global vs argument mix-up.
- Docs: `UI_RUNTIME_REPLAY.md` Layer B + work order + reproduce updated.


### 2026-10-04 ? UI ? script replay batch: module() env fix, 78/122 windows replay OK
- The scripts start with `module("Name", ExportExternalLib)` (Lua 5.1 loadlib module system), which
  re-points the chunk's environment at a fresh plain table; the engine's option function wires the
  module globals. The harness now overrides `module` to chain `env.__index = _G` afterwards (capturing
  `getfenv`/`setmetatable`/`_G` as upvalues - `module` re-setfenv's its own caller). Coverage jumped
  39 -> 78 OK of 122 scripted windows.
- Data-stub field rules by Hungarian prefix: `b*` -> false, `s*` -> "", `t*`/`h*`/`p*` -> permissive
  sub-objects, other camelCase -> 0; globals `Is*`/`Has*`/`Can*` -> false; predicates false so the
  bag does not take the limited-map/extended branch. Top recordings: BigBagPanel 793 mutations,
  Player 179, TopMenu 106, MailPanel 101, SocialPanel 92, MainBarPanel 89.
- `tools/ui/replay_all.py` batch runner writes `ui-process-app/Data/runtime_state/<stem>.tsv` +
  `replay_summary.tsv` (git-ignored with rejected.tsv/render_status.tsv).
- Next: make the viewer consume the runtime TSVs instead of hand overrides; keep stub-tuning the
  44 partial scripts (data-object shapes).
- Verified: replay_all 78 OK / 44 partial; --selftest 1240/0/0 (viewer unchanged).


### 2026-10-04 ? UI ? system completion recheck: 79/122 replay OK, viewer consumption still missing
- Re-ran the gates: census 14 unhandled variants (unchanged), batch replay with an extended entry
  chain (OnFrameCreate -> OnLoad -> OnCreate -> Init -> OnOpen) -> **79 OK / 43 partial** of 122
  scripted windows, 3,265 mutations total; no-entry down to 3 (Balloon/TradingSure/UISetting).
- Tier coverage (scripted windows): T-A (liked+recommended, 59) = 35 OK / 23 partial / 1 no-entry;
  T-B (27) = 18/7/2; T-C (36) = 26/10/0. Top recordings: BigBagPanel 793, Player 179, TopMenu 106,
  MailPanel 101, SoundSettingPanel 93, MiniMap 92, SocialPanel 91, MainBarPanel 89.
- Verdict recorded in UI_RUNTIME_REPLAY.md: the replay system (VM + shim + batch) is proven, but the
  display has not changed yet - the missing piece is the viewer consuming Data/runtime_state/*.tsv
  instead of hand overrides. Layer A (KGUI conformance) not started beyond the ImageType/anchor fixes.
- Verified: replay_all 79 OK / 43 partial; --selftest 1240/0/0; git clean at 975947f + this change.


### 2026-10-04 ? UI ? viewer consumes runtime state: panels visibly change (bag 594x624 runtime=284)
- User: "for panels i clicked, NOTHING got better" - true: the replay had no viewer side. Added
  `LayoutPlanBuilder.ApplyRuntimeState(IniFile, iniPath)`: loads `Data/runtime_state/<stem>.tsv` and
  applies SetSize/SetW/SetH/SetRelPos/SetAbsPos/SetRelX/SetRelY/SetFrame/SetText/SetFontScheme/
  SetAlpha/Show/Hide/SetVisible (last wins) BEFORE the inventory overrides so curated entries still
  win; a root `Hide` is ignored (the engine shows the window after init). Wired into the GUI, the
  headless render, audit, status/contact-sheet plan builder and the selftest pipeline.
- Cleanup of a superseded band-aid: the bag's hand `adjust` (440-wide root/backgrounds, selector X)
  was authored from the mis-decompiled script; the replay gives the truth (root 594x624,
  Image_Glassmorphism 594x624, Image_HBg 594x591, selector SetRelX 416), so those entries were
  removed (kept: hide/show, Handle_Total posType, Handle_Bag_Normal clip, filter-checkbox tops).
- Renders: bigbagpanel sections=145 runtime=284 (all six bag rows laid out, 594x624),
  player-frame runtime=47, main-bar runtime=17, socialpanel runtime=15, characterpanel runtime=6;
  AssetNote shows `runtime=N`. --selftest 1240/0/0 with runtime state in the gate.
- Verified: --render bigbagpanel runtime=284, dump root 594x624; --selftest 1240/0/0; app relaunched.


### 2026-10-04 ? UI ? 推荐 1.1-1.15 confirmed -> liked (2.x, 53 total); +20 third batch; fidelity plan
- User: "good to go for the 15, move them to likes, recommend me 20 more ... we need to improve the
  display ... but first we need a plan".
- Moved the current visible 推荐 1.1-1.15 (门派 … 隐元秘鉴) to liked (2.x; 53 windows now); 推荐
  keeps the remainder + 20 third-batch promotions: 聊天设置/共享背包/宠物信息/宠物技能/武学指导/阅读/
  相册/谁看过我/科举/按键/顶部菜单/任务对比/帮会战功榜/活动列表/师父奖励/招收帮众/我的名片/账号好友/
  宠物动作条/武器技能条. Catalog still 1,240; stages: recommended 48 (42 visible), liked 53, ui 11,
  hud-ui 45, panels-ui 64, menus-ui 24, other-ui 780, modes-ui 211, spare 4.
- New doc `docs/ui/UI_RENDER_FIDELITY_PLAN.md` (registered in `docs/ui/README.md`): retrospective of
  the "out of place -> almost right shape" evolution (engine semantics / runtime state / resources
  layers, with the fixing commits) + a tiered fidelity contract (T-A core / T-B functional / T-C
  catalog, with a stop rule) + P1-P5 plan (status badges, contact sheet, runtime replay for T-A,
  render sanity checks, depth dashboard).
- Also normalized the stage JSON back to `"windows": [` spacing (the batch-2 move script had eaten
  the space; valid JSON but noisy in diffs).
- Verified: --selftest 1240/0/0; GUI relaunched.


### 2026-10-04 ? UI ? full UI-system coverage audit vs the official client (new doc UI_SYSTEM_COVERAGE.md)
- User: "run a full check on UI rendering system from game official client, how much percentage".
  Measured every axis over all 1,240 windows:
  - constructs (usage-weighted): WndType 99.6%, PosType 99.9%, ImageType 100%, HandleType 99.5%,
    FirstItemPosType 99.6%, AnchorDst 100% (mechanism) -> aggregate 99.7%.
  - art: 40,802 Image sections, 899 placeholders of which 278 are the intentional TextureName=no ->
    ~602 real missing -> 98.5% of image instances.
  - text: 21,191 Text sections, 3,254 unresolved ids (2,911 unique - a long tail of per-module
    string tables not extracted) -> 84.6%; weakest asset axis.
  - runtime: 122/1,240 windows ship scripts (9.8%); replay 79 full (65% of scripted) + 43 partial;
    the viewer now consumes the state.
  - behavior (events/animation/3D/web/native bars): 0% in the viewer by design - the product runs
    the real engine for those.
- Verdict: static layout+art ~98-99%, text ~85%, runtime 65% of the scripted subset; overall ~95%
  for the viewer's static-review purpose. Gap list: extract module string tables, stub-tune the 43
  partial replays, close the census list.
- Docs: UI_SYSTEM_COVERAGE.md (registered in docs/ui/README.md).
- Verified: --audit placeholders=899 unresolved=3254 outOfBounds=6584; --status placed=1240
  shell=136 runtime-hosts=615; replay 79 OK/43 partial; --selftest 1240/0/0.


### 2026-10-04 ? UI ? interaction replay: the client's own handlers dispatch (bag checkbox toggles)
- User challenge: "how come 99% of the UI is still broken, what percentage did you go against, real
  client or your notes, i actually think i need interactions". Answer: the coverage percentages are
  self-assessed implementation coverage measured with our own census/audit tools - NOT against the
  real client's pixels (no GT for the basic UI). UI_SYSTEM_COVERAGE.md now says this in a §0
  ("what this measures and what it does not").
- Agreed direction: interactions are the missing system. Built `tools/ui/replay_server.lua` (same
  shim as the batch harness, long-running): loads a window module + INI, prints
  `READY handlers=...` (29 On* handlers for the bag), and accepts `EVENT <section> <handler> [args]`
  / `STATE` / `QUIT`, returning the mutation delta as TSV.
- Proven: `EVENT CheckBox_Compact OnCheckBoxCheck` -> RESULT OK + delta (`CheckBox_Compact Check
  false`, `Handle_Bag_Compact Hide`, `BigBagPanel SetSize 594 624`, FormatAllItemPos...) - the
  client's own checkbox handler toggles compact/normal exactly as in the game.
- New doc UI_INTERACTION_REPLAY.md (registered): architecture (viewer spawns one server per window;
  hit-test from the built element rects; handler by section type; delta applied as an in-memory
  overlay; cache invalidation + re-render), scope and next steps. Viewer wiring is the next build.
- Verified: server READY 29 handlers; EVENT -> RESULT OK + 7-step delta; --selftest unchanged
  1240/0/0 (viewer untouched this pass).


### 2026-10-04 ? UI ? recheck vs the real client: partial replay broke GT windows (minimap 5->40 sections)
- User: "recheck against real client and give me more assessment". GT inventory: the repo's real-client
  UI captures are the 绝境战场-era set (queue Screenshot-given-1, minimap/battlefield crops,
  middlemap 2048x1792) - there is NO GT for the basic-UI panels, so their fidelity stays unmeasured.
- Recheck finding: applying PARTIAL replay state blindly broke GT-matched windows - the minimap
  render collapsed from its full subtree to 5 sections (a partial replay records Hide during init for
  sections the engine shows later). Fix: `ApplyRuntimeState` applies a window's state only when
  `replay_summary.tsv` marks the replay OK; partials fall back to the authored/GT-matched state.
  After: minimap 40 sections runtime=0, bigbagpanel 145 sections runtime=284 (complete replay kept).
- New doc `UI_REAL_CLIENT_ASSESSMENT.md` (registered): GT inventory + validation history, the
  evidence-class matrix (client binary / client data / GT capture / self-assessed / unmeasured),
  and the capture wishlist needed to measure basic-UI fidelity (bag normal+compact+expanded,
  character pages, skill, social, mail, auction, guild, settings, plus post-click states).
- Verified: minimap render 40 sections (was 5), bigbagpanel runtime=284, --selftest 1240/0/0.


### 2026-10-04 ? UI ? issue register item 1 closed: module string tables added (unresolved 3254 -> 49)
- The 144 tables in the temp extraction (`reorg_tables_out`) were never loaded by the viewer (only
  ~21 of them were copied into Data/text/assets). Copied the missing 136 into
  `ui-process-app/Data/text/ui/Scheme/Case` as UTF-8 (the viewer's TextFile decoder handles UTF-8
  and GBK by content).
- Audit: unresolved 3,254 -> **49** (98.5% drop); placeholders 899 -> 901; outOfBounds 6,584 -> 6,903
  (runtime-state geometry + resolved text widths move elements; many oob are legitimate overhang).
  Selftest 1240/0/0.
- The remaining 49 ids are a dev/unreached tail (`STR_COLLECTION*` x24, `STR_GUILD_ALLIANCELOLI`,
  `STR_MATCHING`, `STR_TESTTEXT_TIME`, `STR_MICROT`, ...); their tables MISS in the searched paks
  under all name variants tried (string_Collection/EquipRecommend/String_PQTeach), so they are not
  extractable by path today.
- Docs updated: UI_SYSTEM_COVERAGE text axis 84.6% -> 99.8%; UI_REAL_CLIENT_ASSESSMENT register item 1
  marked fixed.
- Verified: --audit unresolved=49; --selftest 1240/0/0.


### 2026-10-04 ? UI ? issue register item 2: texture pair extraction (real missing 602 -> 434)
- The audit's 899 placeholders split into 278 intentional `TextureName=no` (runtime-assigned art)
  and ~602 real missing frames. First attempt extracted the listed paths one by one (121 files) and
  made the audit WORSE (901 -> 1025, then 1088 after restoring from art_out): overwriting a `.UITex`
  without its `.Tga` sibling (or vice versa) creates frame/atlas mismatches.
- Fix: build the pair list (each `.UITex` + its `.Tga` sibling, same directory) -> extract 208 files
  -> copy both -> placeholders **712** (278 `no` + 434 real), art coverage ~98.5% -> ~98.9%.
  Lesson: never copy a UITex or its atlas alone; extract and copy the pair.
- Remaining misses MISS in the scanned paks (ReputationPanel1.UITex, QuestPanelButton.UITex, ...) -
  not extractable by path today.
- Verified: --audit placeholders=712 (no=278 real=434) unresolved=49 outOfBounds=6901;
  --selftest 1240/0/0.


### 2026-10-04 ? UI ? issue register item 4: partial replays 43 -> 41 (number metatable + proxy fields)
- Lua 5.1 resolves comparison/arithmetic metamethods on the LEFT operand only, so `number < proxy`
  and `number + proxy` aborted replays. Fix: `debug.setmetatable(0, {...})` gives the number type
  neutral metamethods (no __concat - native string/number concat stays untouched). Also unknown
  camelCase fields now return permissive proxies instead of 0 (container fields the scripts index
  keep working); known numeric prefixes (n/d/i/f/x/y/u) stay 0.
- Batch: 79 -> **81 OK / 41 partial**, 3,298 mutations; tiers T-A 35/23/1, T-B 19/6/2, T-C 27/9/0.
  The viewer's completed-replay rule means the newly OK windows now get their runtime state.
- Remaining error classes (41): index-a-number on module-helper fields, compare table/number,
  for-limit from a method-as-value, one `sub` string case, etc. - per-case stub tuning.
- Verified: replay_all 81 OK/41 partial; --selftest 1240/0/0.


### 2026-10-04 ? UI ? interaction wiring: clicks dispatch the client's own handlers in the viewer
- Wired the proven dispatch core into the WPF viewer: `MainWindow` spawns one `replay_server.lua`
  per selected window (completed replays only), reads `READY handlers=...`, stops it on window/stage
  change. A left click hit-tests the built element tree (VisualTreeHelper.HitTest -> walk up to a
  section), picks the handler by type (CheckBox_* -> OnCheckBoxCheck, Box_* -> OnItemLButtonClick,
  else OnLButtonClick), sends EVENT, reads the delta until END, appends it to a per-window overlay
  and re-renders (`ApplyRuntimeMutations` shared with the on-disk state; cache cleared).
- `BuildLayoutCanvas` now returns the `UiBuildResult` (stored for hit-testing) and applies the
  per-window overlay after the on-disk runtime state; interactive windows bypass the layout cache.
- Server: `OnCheckBoxCheck` toggles the checkbox state before firing (as the engine does).
- Verified: app spawns lua32 for bigbagpanel (pid pair observed); server CLI RESULT OK + delta;
  --selftest 1240/0/0. A human click is the remaining live check.
- Next: hover (OnMouseEnter/Leave), item-level clicks with row index, window chains, handler count
  in the AssetNote.


### 2026-10-04 ? UI ? hover wired; failed method/property split reverted (Lua 5.1 comparison rule)
- Hover: mouse-move hit-tests the section under the cursor and dispatches OnMouseLeave/OnMouseEnter
  when it changes (same replay server; only when the module exposes the handlers). Clicks and hover
  share `HitTestSection`.
- Failed experiment: splitting PascalCase keys into methods (verb prefixes) vs properties (proxies)
  broke the batch - 1 OK/121 ERR then a hang. Root cause: **Lua 5.1 order comparisons between
  different types error regardless of metatables** (`luaV_lessthan`: ttype(l) != ttype(r) -> error),
  so property proxies on the right of `number <` abort scripts; the number-type metatable cannot
  rescue it (it does help arithmetic, which does consult metamethods). Reverted to the known-good
  shim (81 OK / 41 partial) and kept the shadowing cleanup (`selfProxy`, forward `local proxy`).
- Verified: replay_all back to 81 OK/41 partial; app+server pair spawns (UiProcessApp 40556/lua32
  8540); --selftest 1240/0/0.


### 2026-10-04 ? UI ? interaction semantics fix (`this` = the control) + headless --click check
- The bag checkbox handler branches on `this:GetName() == "CheckBox_Compact"`; the server was
  setting `this` to the window root, so the branch never ran (delta was Lookups only). Engine truth:
  **`this` is the control that fired the event** (the root is `this` only for frame events). Fixed in
  replay_server.lua; the handler now runs its branch (`SetButtonMaxState` on Btn_Max etc.).
- New headless interaction check: `UiProcessApp.exe --click <windowId> <section> [handler]
  [--out file.png]` spawns the server, dispatches one EVENT, applies the delta on top of the runtime
  state and renders. Verified: `--click bigbagpanel CheckBox_Compact OnCheckBoxCheck` -> 8-step delta,
  145 sections rendered (diff vs base render is small because the base state is already normal mode -
  the handler sets compact=false).
- Verified: --click delta + render; --selftest 1240/0/0.


### 2026-10-04 ? UI ? window-chain recording (OpenWindow/CloseWindow) + server regression fixed
- The shim now records window-chain calls: `_G.OpenWindow`/`CloseWindow` append to a list; the batch
  RESULT carries `opens=N`, the server prints `WINDOW <path>` lines, the viewer appends
  `opens=<basenames>` to the AssetNote, `--click` prints them. Handlers that open/close windows are
  now visible instead of silent no-ops.
- Mid-edit regression: the make_server.py template kept the old pcall/print block, producing a server
  with a duplicate block (syntax error: `'end' expected ... near 'elseif'`), which silently made
  `--click` return mutations=0. Fixed the template; server re-verified (READY/RESULT/END; Btn_Close
  dispatch 0 mutations - the close handler uses CloseBigBagPanel, a different global).
- Verified: replay_all 81 OK/41 partial; --click render; --selftest 1240/0/0.


### 2026-10-04 ? UI ? click navigation + all window-chain call forms; Yoga export lead
- Window-chain recording covers every form the scripts use: `Wnd.OpenWindow`/`Station.OpenWindow`/
  bare `OpenWindow`, plus engine helpers matched by name (`Open*`/`Close*` -> recorded as the helper
  name, e.g. `opens=OpenBankPanel`). Clicks now FOLLOW the chain: a matching catalog window is
  selected (history push), `Close*` pops; hover never navigates. Verified:
  `--click bigbagpanel Btn_Bank OnLButtonClick` -> `opens=OpenBankPanel`.
  Limitation: cross-module calls (AuctionPanel.Open) need that module loaded.
- Item-clone clicks (`__lt_` rows -> OnItemLButtonClick) + handler count in the AssetNote.
- Tried returning nil for unset UI properties (engine-faithful) - cost one OK window and fixed
  nothing (the UpdateAnchor cluster fails on `this` being a number, not the property) -> reverted.
- Lead: client `KGUIX64.dll` exports the **Yoga layout API** (`YGNodeCalculateLayout`,
  `YGNodeStyleSet*`) -> WndFlexContainer/FlexHandle are flexbox containers; evidence-backed path for
  the ~117 flex sections (recorded in UI_REAL_CLIENT_ASSESSMENT.md).
- Verified: replay_all 81 OK/41 partial; --selftest 1240/0/0.


### 2026-10-04 ? UI ? zero-size frames draw nothing (placeholders 712 -> 679) + art gap classified
- The audit's 434 real placeholders split by cause: 33 are frames that exist in the atlas with a
  **zero-size rect** (the engine draws nothing; `UiTexCache.IsEmptyFrame` + UiLayout now collapse
  them without a placeholder), 161 are stale-atlas frames (the INI references frames/groups the local
  atlas version lacks - e.g. QuestPanelButton.UITex is 32 frames while its group table starts at
  frame 300), 231 are files absent from the scanned paks (mpbj.tga, Cloud.tga).
- Audit: placeholders 712 -> 679 (278 `no` + 401 real), outOfBounds 6886; --selftest 1240/0/0.
- Remaining art work needs a full pak index or the correct atlas versions (blocked on extractor
  coverage, not on the viewer).


### 2026-10-05 ? UI ? UITex v1 atlas layout decoded from the engine (placeholders 679 -> 534; art ~99.4%)
- The remaining 161 "stale-atlas frames" were not stale files: 261 of 1,121 atlases are version 1
  and the viewer parsed them with the version-2 layout. Reverse-engineered the loader in
  `KGUIX64.dll` (`UI::KImageInfoMgr::LoadUITexFile` RVA 0xE9240; strings `UITEXFILEHEADER`,
  `UITEXFRAMEDATASTRUCTURE`, `nAnimateCount`; capstone 5.0.7 on a read-only copy): 88-byte header
  (frame count @0x0C, animate count @0x10, version = dword 0 >> 16), then n 20-byte frames, then
  the animate/group table `u32 count; count × (frame, interval)`. v1 frames start at 88 as
  `flag,x,y,w,h` (groups at `88+n*20`); v2 frames at 92 as `x,y,w,h,flag` (groups at `92+n*20`).
  The v1 last frame shares its flag word with the group table's first word (the engine reads v1
  frames from 88), so the final record carries only its 16-byte rect.
- Fixed `UiTex.cs` (version-aware offsets + v1 last-frame rule). Empirical check first: with the
  v1 offset, 261/261 v1 atlases parse to EOF with valid group frames (vs 0/261 at the v2 offset).
- Audit: placeholders 679 -> 534 (278 `no` + 256 real: 231 files absent, 16 authored Frames beyond
  the atlas, 9 misc); real missing 401 -> 256; art ~99.0% -> ~99.4%; --selftest 1240/0/0.
- QuestPanelButton `NormalGroup=3` now resolves to its authored frame instead of the bogus
  `Frame=35` fallback; `Arena_JJC11` etc. no longer placeholder.


### 2026-10-05 ? UI ? replay harness: module env fix + handle/PascalCase rules (81 -> 85 OK, 3,410 mutations)
- Root cause of a whole error class: Lua 5.1 `module()` setfenv's **its caller**, and the harness
  called it through a wrapper, so the wrapper got the module table while the script chunk kept `_G`:
  module globals leaked into `_G` and the module table stayed empty, so scripts reading their own
  module table (`ArenaOpponent.Anchor`, `Craft`, `BattleField`, `LiveShowBuff`, `BrightMarkTitle`)
  failed with nil/function errors. Proved with a disassembly of the module bytecode (built
  `luac32.exe` from the same Lua 5.1.5 sources) + a `string.dump`/`debug.getinfo` probe of the
  running function. Fix: wrapper uses `getfenv(1)` (its env = module table), chains `__index = _G`,
  and `setfenv(2, env)` on the chunk. 81 -> 82 OK.
- Section-proxy rule: unauthored `hXxx`/`tXxx`/`pXxx` properties now resolve to the child control
  (`hBtnProperty` -> `BtnProperty`) or a permissive proxy instead of 0 (the engine exposes handles
  there; 0 aborted `self.hBtnProperty:...`). HorsePanel + LuckyMeeting recovered. 82 -> 84 OK.
- Permissive-proxy rule: unknown PascalCase globals are callable+indexable proxies (module tables
  indexed as `Craft.Foo` vs functions called as `Craft.Foo()`); plain functions broke the first form.
  ExpLine recovered; several partials got further. 84 -> 85 OK.
- Batch: 85 OK / 37 partial, 3,410 mutations (from 81/41, 3,298). No previously-OK window regressed.
- Remaining classes (stub-tuning): numeric loop bounds (`'for' limit must be a number`), fields
  compared numerically (mixed-type comparisons cannot use metatables in Lua 5.1), module fields
  nil (`frame`), pairs/sort on non-tables; 2 no-entry windows (Balloon/TradingSure).


### 2026-10-05 ? UI ? replay harness: scalar/anchor getters (85 -> 95 OK, 3,446 mutations; bag kept at 793)
- Disassembled the failing functions with the `luac32` listing (dumped via `string.dump` from the
  running harness): the `'for' limit` class came from scalar getters returning proxies
  (`GetArenaPlayerCount`, `GetBoxSize`) and the `attempt to index a number` class from
  `GetDefaultAnchor` returning 0 (the generic section `Get*` fallback) while the scripts store it
  as an anchor table (`X.tAnchor = self:GetDefaultAnchor()`).
- Fixes: permissive-proxy scalar getters (`Get*Count/Num/ID/Index/Level/Time/Frame`) return 0;
  section `GetDefaultAnchor`/`GetFrameAnchor` return an anchor proxy. **`Get*Size` stays a proxy on
  purpose**: a trial with Size -> 0 dropped BigBagPanel 793 -> 595 mutations (the bag's arithmetic
  path), so the rule excludes Size and the bag stays at 793.
- Batch: 85 -> **95 OK / 27 partial, 3,446 mutations** (BigBagPanel 793, no regressions). Ported the
  same three rules to `replay_server.lua` (server smoke: ComboPanel READY handlers=5).
- Remaining: table-typed fields compared numerically (FightingStatistic, MiniMap, MiddleMap,
  SafePanel, SystemMenu_*, VideoSettingPanel), nil module fields (RaidPanel `frame`, PLOActionBar
  `tAnchor`), pairs/sort/gmatch/sub on non-tables, 2 no-entry.


### 2026-10-05 ? UI ? replay harness: numeric-getter scope tuned (95 -> 98 OK; regressions caught by batch)
- Widened the scalar-getter rule to a shared `numericGetter(k)` (`Get` + Count/Num/ID/Id/Index/Level/
  Score/Screen/Rate/Percent) used by both the permissive proxy and the `_G` auto-stub (global calls
  like `GetAddTrainSkillCount()` bypassed the proxy-field rule). SystemMenu_Left/Right +
  VideoSettingPanel recovered.
- The batch caught two regressions immediately: including `Time$`/`Frame$` made `GetTodayTime`
  return 0 (it is a month/day table) and `GetMgFrame`/`GetGameFrame` return 0 (frame objects) -
  EditBox and LuckyMeeting broke. Excluding Time/Frame fixed them and the batch landed at
  **98 OK / 24 partial, 3,545 mutations** (BigBagPanel still 793).
- Lesson: every stub-type heuristic must be batch-verified both ways (gain + no regression); the
  suite is the guard. Ported the final rules to `replay_server.lua`.
- Remaining: compare with table/nil (FightingStatistic, MiniMap, MiddleMap, MiniGameDescription,
  TeamBuilding), pairs/sort/gmatch/sub/ipairs on non-tables, nil module fields (RaidPanel `frame`,
  PLOActionBar `tAnchor`), length nil (ReputationPanel), `is_bind` call on a number (SafePanel),
  2 no-entry.


### 2026-10-05 ? UI ? replay harness second pass: 98 -> 113 OK (3,931 mutations), 9 partial left
- Method: batch-driven stub tuning with the suite as the regression guard, each fix verified against
  the failing function's own bytecode (`luac32` listings + `string.dump`/`debug.getupvalue` probes):
  - `is_*` API predicates; `CAN_*` numeric constants (MiniMap); `g_tStrings` authored data tables
    (`t*`, `_MENU`/`_LIST`/`_TAGS`) vs strings (TeamBuilding, SoundSettingPanel); multi-value proxy
    `__call` (FBlist lists); numeric-key element proxies (DynamicRouge); `GetActivePage` from the
    INI `page=`; `GetFirstChild`/`GetNext` child walk; string helpers (`StringReplaceW`/`StringFindW`,
    `DateToTime`/`GetCurrentTime`, `string.*` coercion) for MailPanel/NewBattleFieldQueue.
  - **Build patch**: the temp `lua32.exe` now returns false for mixed-type `<`/`<=` and coerces
    tables to 0 in `luaV_tonumber` (permissive stub environment; documented in the replay doc).
    Fixes the compare cluster and Bullet's `for` limit; a test-rig patch, not product behavior.
  - Regressions caught and reverted along the way: ALL_CAPS -> 0 broke 28 windows; `Get*Time`/`Frame`
    -> 0 broke EditBox/LuckyMeeting; both narrowed.
- Batch: **113/122 OK / 9 partial, 3,931 mutations** (from 81/41, 3,298 this session). Remaining
  partials hinge on module-local tables the engine populates at runtime (AccelerateBall, CraftPanel,
  FBlist, PLActionBar, RaidPanel, ReputationPanel) or lack an init hook (Balloon, TradingSure) —
  stubbing them would mean inventing data, so they stay partial.
- `replay_server.lua` shim re-spliced from the harness (UI proxies + engine stubs sections) so the
  live interaction path shares every rule; server smoke: TeamBuilding READY handlers=19.


### 2026-10-05 ? UI ? engine base data layer from module_info.xml (113 -> 135 OK, 143 scripted windows)
- The 9 data-blocked windows needed the engine's own UI data, not stubs. Found the manifest:
  `ui/module_info.xml` (PakV4) lists 1,016 UI modules; `loginscript` (load=true) carries
  `ui/Script/common/table_defs.lua` (g_tTableFile, 847 tables), `ui/Script/table.lua`
  (g_tTable + Table_* accessors), `ui/Script/video_base.lua` (VideoBase); the `string` module
  carries the real `ui/String/string.lua` (g_tStrings). Extracted the 122 load=true lib/data files
  to the git-ignored `ui-process-app/assets/ui/` + `ui/engine_base.txt` manifest.
- Harness loads them before the window script with `module()` overridden to `setfenv(2, _G)` (so
  data globals stay global; the engine's ExportExternalLib net effect). `RC_ENGINE_BASE_DEBUG=1`
  prints loaded/failed + pre-entry globals. Side benefit: the extraction supplied 21 previously
  missing window scripts -> **143 scripted windows** (was 122).
- Also fixed: `_`-prefixed section fields (`_AutoPosInfo`) now return data proxies (the real
  `InitFrameAutoPosInfo` from base.lua indexed the function fallback; 10 windows).
- Batch: **135/143 OK / 8 partial, 4,690 mutations**. AccelerateBall is genuinely stale: the current
  `video_base.lua` caps literal has 9 keys and no `aScreenSizeLimitedRate` (the module expects it) -
  documented as a client-side inconsistency, not stubbed.
- Ported the loader + `_` rule to `replay_server.lua` (shim re-spliced; smoke READY handlers=19).
- Follow-up: adding every `type="lib"` module to the base list (181 files total, +58 extracted)
  lifted it to **137/143 OK, 4,816 mutations**; the remaining 8 are the stale AccelerateBall plus
  engine-list/module-local shape mismatches (CraftPanel/FBlist sort nil, EmotionPanel pairs nil,
  PLActionBar/RaidPanel/ReputationPanel nil locals, ReputationPanelNew concat table).


### 2026-10-05 ? UI ? UI table data files wired (KG_Table stub + 848 TSV tables) -> 138/143 OK
- The `Table_Get*` consumers read `g_tTable.X:Search/GetRow/GetRowCount`, but the entries were
  only descriptors (`Path`/`Title`) - the engine's C++ `KG_Table.Load` fills them from the shipped
  UI tables. Dumped the descriptor Path list (848 entries) and extracted every table file
  (`ui/Scheme/Case/**/*.txt|.tab`, TSV, GBK) into the git-ignored assets.
- Harness `KG_Table.Load` stub parses the real file: header + rows, Title descriptors map columns
  to typed fields (`f` type letter, `t` name; numeric columns default 0), lazy per-table (parse on
  first use), `Search(key)` matches the first key column, unknown PascalCase methods return nil.
  Note: writing `gt[k] = obj` tripped a base-lib `__newindex` assert - use rawset.
- Result: CraftPanel + DynamicRougeActionBarSetting recovered; mutations 4,816 -> **5,044**;
  **138/143 OK / 7 partial**. Ported the loader to replay_server.lua (splice; smoke READY).
- Remaining 7: AccelerateBall (stale caps key, evidence in video_base.lua), EmotionPanel/FBlist/
  ReputationPanelNew (module vs real-table shape), PLActionBar/RaidPanel/ReputationPanel (nil
  engine-set locals).


### 2026-10-05 ? UI ? replay diagnostics: debug.getupvalue is blind on the game's stripped bytecode
- Chasing the last nil-upvalue windows (PLActionBar `tAnchor`, RaidPanel `frame`, ReputationPanel)
  exposed a diagnostic trap: `debug.getupvalue(f, 1)` returns **no values** for the game's
  bytecode even when the prototype has upvalues (`string.dump` + luac show 7). Cause: Lua 5.1's
  `aux_upvalue` bounds-checks `p->sizeupvalues` (the debug-name array size), and the client's
  compiler ships `sizeupvalues=0` (names stripped) while `nups>0`; the VM still uses the upvalues.
  Never trust getupvalue absence on client bytecode - use `string.dump`+`luac` instead.
- With the dump-based view: RaidPanel.Init's nested closure indexes `upval.frame` where the
  upvalue chain resolves to a nil register value at creation; PLActionBar/ReputationPanel similar.
  These three need engine-set module state at open time, not shipped data - left partial with this
  evidence rather than fabricated. Final: **138/143 OK, 5,044 mutations**.


### 2026-10-05 ? UI ? measured runtime gap: 671 dropped state-bearing calls (the missing-items answer)
- User: "UIs are still missing items, wrongly placed - what can possibly be the reason". Built
  `tools/ui/runtime_gap_report.py`: classifies every recorded replay call against the viewer's
  consumer (`LayoutPlanBuilder.ApplyRuntimeMutations` handles only 14 property methods), joins
  per-window render status, writes `docs/ui/UI_RUNTIME_GAP.md`.
- Result: **671 state-bearing calls dropped** - item-creation 169 (Clear/AppendItemFromIni/
  AppendContentFromIni/CreateItemData/... -> list rows never materialize), arrangement 330
  (FormatAllItemPos/SetPoint/CorrectPos/SetSizeByAllItemSize/SetScrollPos/... -> items sit at
  authored coords), render 53 (FromUITex/SetImageType/...), state 119 (Enable/Check/Expand/
  ActivePage). Consumed 1,110; noise (Lookup/RegisterEvent/getters) 3,269.
- Concrete: BigBagPanel drops 40 item-creation + 86 arrangement calls - `Clear`+9x
  `AppendContentFromIni CheckBox_FilterMain0..8` (the filter checkboxes) and
  `FormatAllItemPos`/`SetSizeByAllItemSize Handle_Bag*` (the bag grid). TopMenu drops 20
  `FormatAllContentPos`; MailPanel 13 arrangement + 8 render; SecurityCard 28 item-creation.
- The fix is executing the client's own recorded calls in the viewer (materialize clones, then run
  the format/correct/scroll passes), not approximating them - the data is already in
  `Data/runtime_state/*.tsv`.


### 2026-10-05 ? UI ? runtime consumption implemented: dropped 671 -> 26 (items in place)
- Executed the client's own recorded calls in the viewer:
  - **item-creation**: `Clear` removes the container's item list (authored prototype included),
    `AppendItemFromIni`/`AppendContentFromIni`/`AppendItemFromString` materialize the source
    subtree as clones (unique prefixed descendant names, `._Parent` rewired; cross-INI sources
    load from the assets root), `RemoveItem` drops the last item. The existing HandleType 3/6
    flow pass then positions them; containers the script arranges get a `$FormatItems` marker so
    the same flow applies (`WndContainer_FilterMainList` etc.).
  - **arrangement**: `SetSizeByAllItemSize` sizes the container from the arranged rows,
    `SetPoint` -> `AnchorArgs` (dstSide,srcSide,dx,dy), `CorrectPos` clamps into the parent,
    `SetScrollPos` shifts the scroll's `Handle_*` content, `SetStepCount`/`EnableScroll`/
    `SetHAlign`/`Scale`/`SetOverText*` store their values.
  - **render/state**: `FromUITex` (image source), `SetImageType`, `SetPercentage`, `SetFontColor`;
    `Enable` -> `$Disabled` (button `DisableGroup`, checkbox `Check/UnCheckAndDisable`),
    `Check` -> `$Checked` (checked frame), `Expand`, `ActivePage`.
- Also fixed the recorder: `record()` kept only 4 args, dropping `SetPoint`'s dx/dy and the
  `AppendContentFromIni` newName - now 8 (both harness and server), batch re-run 138/143 OK.
- Measured: `runtime_gap_report.py` dropped **671 -> 26** (item-creation 169->5, arrangement
  330->8, render 53->13, state 119->0; consumed 1110->1753). Audit placeholders 531->516,
  outOfBounds 6924->6892; selftest 1240/0/0. BigBagPanel applies 373 mutations (was 237) and
  materializes the 9 filter checkboxes + subtrees (selftest sections 123 -> status 145).
- Remaining 26: `AppendItemFromData` (function-arg rows, SocialPanel), drag registrations (no
  visual), frame-animation/icon-source calls.


### 2026-10-05 ? UI ? runtime item placement fixed (bag filter row in place; the A/B method)
- User: "still don't see good changes". Built the A/B loop: `--status` with the runtime state vs
  with `Data/runtime_state` renamed away (static), diffing per-window sections/elements/oob.
  It exposed three real bugs in my consumption:
  1. `Clear` (remove all descendants) blanked lists the under-recorded append loops could not
     rebuild (ActivityList 236 -> 13 sections). Fix: `Clear` clears only items added by earlier
     appends and defers to the next append (no append -> keep the authored content).
  2. The flow pass arranged ALL children of a runtime container, including authored backgrounds:
     BigBagPanel's filter container has `Handle_BG` (~595 high) as a child, so the 18 runtime
     checkboxes were flowed to y=624 (the window bottom, invisible). Fix: runtime-arranged
     containers flow only `$RuntimeItem` clones (fall back to all children when none), wrapping
     at the authored width. Bag filter row now at (472,0), first clone (0,0) relative.
  3. `SetSizeByAllItemSize` grew containers to the single-row item extent, stretching the bag to
     2158 wide; capped by the authored box.
- Also: per-item `SetText` after an append now lands on the newest clone's Text child (the harness
  records the clone's Lookup+SetText on the container - the filter labels 全部/装备/... now apply).
- A/B after: runtime renders are now FULLER than static for the item windows (BigBagPanel 145->182
  sections incl. the 10 filter checkboxes, ActivityList 236->251, SecurityCard 132->201 with oob
  97->12, HatredPanel 20->55). Selftest 1240/0/0; audit ph=535 oob=7359.
- Remaining visible gaps: the stub-data Hide branches over-hide some windows (LuckyMeeting
  145->3 - the replay hides both page variants because the mode data is a stub); the materialized
  clones' text children sit at their authored offsets (counted oob, the engine clips).


### 2026-10-05 ? UI ? over-hide guards + Open phase (no window collapses; A/B clean)
- The A/B showed 12 windows rendering FEWER sections under the runtime state; LuckyMeeting
  145->3 and systemmenu-right 28->3 were stub-session reset hides (the init hides every state
  variant/page part, then shows the active one from live data that is stubbed). Two guards:
  a hide whose parent would have no visible child is reverted, and a hide set leaving less than
  half the authored sections visible is dropped entirely (keep the authored layout - the client's
  own default). Worst deficit is now safepanel 125->105 (84%, legitimate state hides).
- Harness/server also run the module's `Open` best-effort after the entry chain (the engine opens
  the window after creating it; some inits gate their refresh on the open flag). Best-effort so a
  failing Open cannot fail a window: batch stays 138/143 OK.
- Final A/B: 6 windows fuller than static (SecurityCard +69, BigBagPanel +37, HatredPanel +35,
  ActivityList +15, FilterPanel +6, PartyRecruitPanel +2), 12 modest deficits, no collapse.
  Audit ph=538 oob=7388; selftest 1240/0/0.


### 2026-10-05 ? UI ? full-flow check (4 questions): scripts are the missing half; two stale claims corrected
- Q1 system: manifest 1,016 modules / 1,783 scripts; local 314/1,783 (18%) - 1,461 .lua missing
  (1,235 Config/Default, 96 ui/Script, 73 scripts/Include/UIscript). Probe: 5/5 missing scripts
  (CoinShop_Main, HouseUpgrade, module, UIscriptDanmu, TwoDimensionalLogin) extract by exact manifest
  path -> extraction gap, not availability. 1,075 of the missing scripts have a local catalog INI
  (windows that would gain replay once extracted).
- Q2 resources: art 99.4% (256 real placeholders = 231 files + 16 frames + 9 misc); text 99.85%
  (49/3,303); fonts 5; atlases 1,121 parse. Re-probe: QuestPanelButton.UITex+.Tga DO extract now
  (the 2026-10-04 "not extractable by path today" claim was stale); Cloud.tga/mpbj.tga MISS at the
  authored paths (genuinely absent/renamed).
- Q3 decode: INI 1,240/1,240; replays 138/143 OK; constructs 14 unhandled variants (305 occurrences)
  + 868 approximate sections (~1.3%).
- Q4 wiring: dropped calls 671->26; A/B 6 fuller, no collapse; but runtime covers 143 windows only.
- Written into UI_REAL_CLIENT_ASSESSMENT.md ?5b (registered doc); next chain: extract 1,461 scripts ->
  replay batch -> wire -> art re-probe -> construct decode.


### 2026-10-05 ? UI ? chain step 1-3: missing scripts extracted (1,742/1,783), replay 138 -> 1,148 OK
- Built the missing list from the manifest (1,469) and extracted via the official PakV4 extractor with
  a new `--keep-paths` option (manifest-path placement; window scripts then flattened next to their
  INIs like the INI corpus): **1,420 HIT / 40 MISS**, manifest 314 -> **1,742/1,783 (97.7%)**; the 41
  missing are 22 `ui/Traits/mobilestreaming` (mobile root), 4 `mainbar_*`, ~15 CJK/corrupted manifest
  paths. Also fixed a UnicodeEncodeError printing U+FFFD manifest paths (stdout reconfigure).
- `engine_base.txt` extended 181 -> 207 (every manifest layer-1 load=true / lib-type file now present;
  incl. ui/Script/module.lua, customdata.lua, questlogic.lua, common libs) - the engine's own base set.
- `replay_all.py` got a TimeoutExpired guard (a hanging window no longer kills the batch).
- Full batch: **OK=1148 ERR=63** of 1,211 scripted windows (was 138/7 of 143); 1,210/1,240 catalog
  windows now have a script. ERR classes: pairs/sort on nil stubs, index-a-number containers,
  isPlaying/isItemShow should-be-functions, concat-with-table, assert in Init, 18 no-entry
  base-class scripts.
- A/B vs static: 20 windows fuller (tongbaogift +752 sections 164->916, creditspanel +112, charge +110,
  selectmacroiconpanel +64, buffmonitor +53, vampirecountpanel +40, macrosettingpanel +36), 29
  state-driven reductions (worst dynamicweathersetting 133->92, 69% - no collapse), selftest
  1240/0/0, audit ph=538 oob=8698.
- Verified: extract log 1420 HIT/40 MISS; replay_summary OK=1148 ERR=63; --selftest 1240/0/0.


### 2026-10-05 ? UI ? item-list wiring: appends clone into the prototype's own list (the big "wrong place" class)
- Evidence: across all recorded appends, **260/301 have the Lua receiver != the prototype's
  authored parent** (Page_Progress -> Handle_QuestList, SelectMacroIconPanel -> Handle_Icon,
  HatredPanel -> Handle_HaredList, WndScroll_Activities -> Handle_List, ...). The engine appends the
  clone into the list that owns the prototype, not into the receiver; our clones were parented to the
  receiver, so runtime items landed in the wrong coordinate space / were arranged by the wrong section.
- Fix (LayoutPlan): `ResolveAppendContainer` = prototype's authored `._Parent` when present in the
  file, else the receiver (through its `ScrollHandle`); `FormatAllItemPos`/`SetSizeByAllItemSize`/
  `Clear` now apply to the containers the receiver's appends actually went into; WndScroll content
  resolves through `ScrollHandle` (its own Left/Top offsets the items). UiLayout: a WndScroll viewport
  clips its content (`ClipToBounds`), as the engine does.
- Regression caught and fixed the same pass: with the clones moved out, the receiver's `$FormatItems`
  stacked its authored children (Page_Progress's scroll got flowed as an item) - a receiver whose
  appends landed elsewhere no longer formats its own children (cleanup pass before return).
- Evidence of effect: selectmacroiconpanel oob 56 -> 5 (items now in Handle_Icon); tongbaogift scroll
  back at its authored (79,100) with items stacked in Handle_QuestList (79,537, 108px pitch); charge
  items unchanged in Handle_List. A/B: 19 fuller / 29 fewer, no collapse; selftest 1240/0/0; audit
  oob 8804 (the +117 over baseline = scroll content logically overflowing viewports that now clip).
- Verified: --selftest 1240/0/0; --audit oob=8804; commit 98b619f.


### 2026-10-05 ? UI ? AppendItemFromData wired via CreateItemData (GuildBankPanel 68 -> 359)
- The last dropped item-creation class: `CreateItemData(ini, proto)` creates a data source, then
  the script loops `AppendItemFromData(data)` (GuildBankPanel's bytecode: `for i = 1, 98 do ... end` -
  the row count is authored in the script). The recorder serializes the data arg as the source's
  tostring `[GuildBankPanel]`; the viewer now stores CreateItemData per window and materializes each
  AppendItemFromData row from that ini+section, through the same owning-list container logic.
- GuildBankPanel 68 -> 359 sections (98 rows), SocialPanel 5 rows; gap report's item-creation 103 -> 0.
  Verified: --selftest 1240/0/0; --status guildbankpanel sections=359.


### 2026-10-05 ? UI ? HandleType decode from KGUIX64: 1/2 are the same list path as 3/6
- Found the Lua binding table via the 'FormatAllItemPos' string data pointers (.rdata
  0x1805d0490/0x1805d0db0): FormatAllItemPos -> 0x1801a32e0, Clear/SetRowSpacing/
  SetSizeByAllItemSize/... neighbors. Config parser offsets: HandleType -> config+0x5b8,
  FirstItemPosType +0x5bc, RowSpacing +0x5e8, Min/MaxRowHeight +0x5ec/0x5f0; config -> runtime
  copy (fn 0x180105950): runtime+0x2f4/0x2f8/0x304/0x2fc/0x300 (scaled by +0x534).
- Arrangement dispatcher (0x1801064f5): HandleType 0-3 and 6 -> the common row/list path
  (0x1801065c0 extent + 0x180106cc0 post pass); 4 -> 0x180107e20; 5 -> 0x180107410 (distinct).
  Post pass (0x180106d25) routes 1/2/3/6 to the same list path. Evidence dump:
  proof/ui/evidence/re/kgui_handle_layout.txt (cited in UI_RENDER_FIDELITY_PLAN.md).
- Viewer: the authored list flow now accepts HandleType 1/2 too (was 3/6) - AsuraPanel's
  Handle_MemberList (type 2) arranges its children; 4/5 remain unhandled and documented.
  A/B stable (20 fuller / 28 fewer), selftest 1240/0/0, audit oob 8784.


### 2026-10-05 ? UI ? FirstItemPosType decode + replay stub classes (OK 1148 -> 1158)
- FirstItemPosType (runtime +0x2f8) decoded from the engine's jump table at 0x180108964
  (13 cases): the FIRST ITEM's alignment inside the container sets the flow origin -
  0 = item's own authored offset, 1/10 bottom, 3 v-center, 5/7 right, 6 h-center,
  8/11 bottom-right, 9 right-center, 12 bottom-center, 2/4 origin. Implemented as the
  flow origin in UiLayout (evidence appended to kgui_handle_layout.txt).
- Replay stub classes fixed (harness): camelCase predicates is<Upper> return a false
  function (isPlaying/isItemShow were hitting the ^i numeric rule); the number type
  metatable gains __index/__newindex/__len (scripts index/assign/len a stub 0 - the
  missing __newindex was the "attempt to index a number value" on ASSIGNMENT);
  JSON.decode(table) passes tables through (Selfie). Batch: 1148 -> **1158 OK / 53 ERR**;
  flipped CampFireworks, IdentityPanel, NumericalPanel, QuestGuide, LuckyPerson,
  CrossingChoosePanel, InterludePanel, InterludeHSLHPanel, Partner, EYaShaInterlude.
- Gap report: drag registration reclassified as interaction (not static layout);
  state-bearing visual drops 32 -> **22** (arrangement 1 + render 21).
- Remaining ERR classes: 18 no-entry (base-class scripts), pairs/sort on a nil module
  global, table.concat with a table element (~5), nil-upvalue indexes, assert in Init.
- Verified: replay_summary OK=1158 ERR=53; --selftest 1240/0/0; commits 83d913e, 0570050, 02e1186.


### 2026-10-05 ? UI ? stub tolerances: nil collections + concat (OK 1158 -> 1167)
- pairs/ipairs/table.sort wrapped: a non-table stub (nil module global, stub 0) iterates as an
  empty collection instead of aborting - EmotionPanel 22 -> 1420 mutations, FBlist, NewQuestPanel.
- Second temp-Lua build patch: ltablib.c addfield coerces a non-string table element to "" for
  table.concat (documented in UI_RUNTIME_REPLAY.md; rebuild recipe there) - flips CastingPanel,
  LightingCityPanel, RefinePanel, ReputationPanelNew, TongArena (347 mutations).
- Batch: 1158 -> **1167 OK / 44 ERR**. Viewer selftest 1240/0/0; gap report windows=1196
  (more windows now replay), visual drops still 22.
- Remaining ERR classes: 18 no-entry base-class scripts, `#nil` module globals, nil upvalues,
  assert in Init, and a few per-case stubs.


### 2026-10-05 ? UI ? long session: entry fallback, VM nil coercions, child-by-name (OK 1167 -> 1177)
- Entry fallback: class-style scripts (WishPanel, ActivityTipPanel, InstanceInfo, PVPMessageBoard)
  define no On* entry - the harness now calls the module's own `Open` (what the engine does when the
  window opens).
- VM nil-coercion patches (lvm.c, test-rig, documented in UI_RUNTIME_REPLAY.md): `#nil` -> 0,
  nil in arithmetic -> 0, nil in `..` -> "" - the remaining failures where a real engine table
  field the client fills at startup is absent.
- string.match/format/rep/upper/lower coerce non-string first args; the section proxy resolves an
  authored child control by exact name (`self.Text_SelectM`, `self.WndContainer_X`) before the
  generic method fallback (indexing that function aborted) - flipped QuickConsumePanel,
  QuickConsumeShare, PersonalCard_BirthdaySetPop, AccelerateBall, LoginScene.
- Batch: 1167 -> **1177 OK / 34 ERR** (day start 1148/63). Viewer selftest 1240/0/0; gap report
  windows=1201; visual drops still 22. Remaining: ~10 no-entry (pure class defs), ~20 nil-field
  indexes on script/real-lib tables, 2 insert-args, 1 assert, 2 upvalues.


### 2026-10-05 ? UI ? class-style entry + NOENTRY disposition (OK 1177 -> 1178, ERR 34 -> 24)
- Decoded the engine's `class()` helper (ui/Script/class.lua): method assignments go through a
  `__newindex` into an internal members table, and `new()` builds instances with `__index` = that
  members table - so a class-style script's OnFrameCreate is NOT on the `<Stem>_Base` table, only on
  its instances. The harness now instantiates `<Stem>_Base:new()` (pcall) and looks for the entry on
  the instance (BubblePanel flips OK).
- The 9 windows that define only OnLButtonClick/OnFrameBreathe-style handlers (no create/load/open
  init - Cyclopaedia_*, Debug, FieldPQPanel, GoldTeamSetSubsidy, IrrigatePanel) now report
  **NOENTRY** instead of ERR: they are popups driven by their opener, so the authored INI is their
  complete state (honest disposition, not a failure).
- Batch: **OK=1178 ERR=24 NOENTRY=9** of 1,211 scripted; UI_SYSTEM_COVERAGE row updated
  (97.4% of scripted windows fully replayed). Selftest 1240/0/0; gap windows=1202.


### 2026-10-05 ? UI ? insert/JsonDecode tolerances; assert tolerance reverted (OK 1178 -> 1180)
- `table.insert(t, Table_GetPath(...))`: the stub multi-return gave 0/1/N values and Lua 5.1
  rejects wrong arg counts. The wrapper keeps the first value (append) or (pos, value) when the
  first is a number - flips RougeLikeFinal, RougeLikeTimeEvent.
- `JsonDecode` (the base lib's wrapper, not JSON.decode) now passes tables through - Selfie
  progressed past the decode error (still ERR later on a nil field).
- Tried a tolerant `assert` for WulinShenghuiDuizhen (stub-data assert): it flipped that window but
  BROKE EmotionPanel (its pcall-guarded assert branch changed; 1420 mutations -> 35). Reverted -
  lesson: an assert inside pcall is control flow, not just a guard.
- Batch: **OK=1180 ERR=22 NOENTRY=9**; selftest 1240/0/0; gap windows=1202.


### 2026-10-05 ? UI ? C1.X hardship stage (review the hard tail on top)
- User: mark the remaining hard windows as hardship and put them in a catalog stage "C1.X" on top to
  review whether they are really needed. Created stage `hardship` / title `C1.X` as stage 1 with the
  **26 in-catalog hard windows** (22 partial replay + 4 NOENTRY; the 5 Cyclopaedia_* NOENTRY are
  sub-panels, not catalog entries); each keeps `originalStageId` for promotion back, the viewer's X
  reject still works (rejected returns to C1.X). `defaultWindow` = achievementpanel so the review
  starts on the set. Total stays 1,240; selftest 1240/0/0; commit 23349b0.


### 2026-10-06 ? UI ? C1.X resolved: plactionbar kept (-> liked), 25 rejected (不需要)
- User review of C1.X: only 1.12 (PLActionBar) is needed; everything else goes to 不需要.
- PLActionBar moved to the `liked` stage (explicitly liked, not its original hud-ui);
  defaultWindow=plactionbar. The hardship stage is removed; the other 25 windows stay in their
  original stages in the JSON and are moved to the 不需要 stage at load by the rejection store
  (Data/rejected.tsv: windowId TAB originalStageId) - fully reversible with X.
- First attempt dropped the 25 windows from the JSON entirely (removing the stage loses its
  windows); fixed by restoring them to their originalStageId stages and using the side file.
- Full recheck: selftest 1240/0/0; audit ph=538 unresolved=49 oob=9346; status refreshed; replay
  batch reproducible OK=1180 ERR=22 NOENTRY=9; gap windows=1202, visual drops 22.


### 2026-10-06 ? UI ? wire-up sweep: HandleType 4/5, render tail, flex (Yoga), WndList, interaction, art probe
- **HandleType 4/5**: decoded the engine paths (type 5 = 0x180107410 distinct list variant; type 4 =
  0x180107e20) and the INI semantics - type 5 handles author RowSpacing/PixelScroll/vertical masks
  (same vertical scroll list) and now join the list flow; type 4 handles author
  AppendStringType/$AppendString (the engine appends the string as a text item at load) - Build()
  synthesizes the Text item (KFActionBarPanel, MobileBuffList).
- **Render tail**: FromTextureFile consumed when the recorded arg is a real path
  (MonopolyCardUseConfirm's TreasureChest1.UITex resolves); animation (SetAnimateGroup*/SetAnimation/
  SetLoopCount) and interaction (drag/scroll-step/tooltip) reclassified honestly in the gap report;
  FormatTextForDraw = engine draw hint (noise); FromIconID stays data-blocked. Visual drops 22 -> 1.
- **Flex (Yoga)**: decoded the flex parser (0x1800bad60) - Direction/FlexDirection/Wrap/
  JustifyContent/AlignItems/AlignContent/FlexPadding*/FlexMargin*/FlexGrow/FlexShrink map 1:1 to
  YGNodeStyleSet*; implemented a Yoga-compatible flex pass in UiLayout (enum values = Yoga's).
  ACC_Excellent's buttons were parked at Top=632 and now land inside their 1700x35 container;
  WndList authors the same flex properties and joins the pass. Audit oob 9349 -> 9300.
- **Interaction**: wheel over a WndScroll translates its ScrollHandle content (clamped, step =
  ScrollStep or 40); WndEdit is a real TextBox with the authored placeholder and dispatches
  OnEditChanged <text> to the script via the server. Drag stays documented (interaction category).
- **Art tail**: scanned all INIs for art references (1,996 paths), 562 missing basenames, probed via
  PakV4 (811 candidates: 205 HIT / 601 MISS); copied only complete UITex+Tga pairs (the pair rule) -
  +180 files in the corpus; placeholders ~flat (537 -> 539; 7 lone copies reverted).
- Gates: selftest 1240/0/0; audit ph=539 unresolved=50 oob=9299; commits 04171b4, bedda1f, 5dee6d9.


### 2026-10-06 ? UI ? plan execution: string libs + census truth + PosType flow (0 unhandled)
- Phase 6 (text): decoded the remaining UI string libs into Data/text/ui/String (auctionstring 95,
  gluestring 182, dungeonstring 28, EquipInquireString 22, HoroCompass 27, expressionstring 16, ...,
  +~380 ids; the loader picks the directory up automatically). The 50-id tail (STR_COLLECTION* 23,
  STR_GUILD_ALLIANCELOLI x4, ...) is NOT in any reachable pak table: probed string_Collection.txt,
  String.txt (uppercase, HIT but different table), variants - all MISS; documented as unreachable/dev.
- Phase 0 (census truth): updated the census handled sets (HandleType 1-6, FirstItemPosType 0-12,
  flex/list exact, PosType 0-12) - **0 unhandled constructs** (was 14 variants).
- PosType tail: decoded the per-item flow jump table (fn 0x180108600, table 0x180108998) - each
  item's PosType places it relative to the previous item's rect (1 left-bottom, 2 left, 3
  left-vcenter, 4 above, 5 right-bottom, 6 above-hcenter, 7/11 right, 8 right-bottom, 9/12
  below-hcenter, 10 below, 0 new line). Implemented in TryFlowAfterPrevious; Attach routes
  3/4/5/9/10/12 through it for item-flow handles. Evidence appended to kgui_handle_layout.txt.
- Gates: selftest 1240/0/0; audit ph=539 unresolved=50 oob=9499 (list content overflow, legitimate);
  commits 2dd611d, e766aa0.


### 2026-10-06 ? UI ? plan phases 3-4: page-set semantics + animation wiring
- Page-sets: the mapping is authored on the WndPageSet (Page_i=<page> + CheckBox_i=<tab>, PageCount);
  the viewer now shows exactly one page per set - the viewer's selection wins for its own set, every
  other (nested) set shows its authored default (the tab with CheckedWhenCreate=1, else Page_0).
  The combo lists the top set's pages and prefers the engine default (LayoutPlanBuilder.DefaultPage).
  Effect: audit placeholders 539 -> **504**, unresolved 50 -> **49**, oob 9499 -> **7484** (the
  all-pages overhang is gone; nested page content now renders).
- Animation: SetAnimateGroupNormal applies the resting group frame (UiLayout NormalFrame), the
  MouseOver/MouseDown groups are recorded for the interaction layer; SetAnimation/SetLoopCount/
  SetTextAutoTipEnabled are consumed as static no-ops. Gap report: animation + tooltip calls
  consumed; visual drops stay **1** (FromIconID, data-blocked), interaction 10 (drag).
- Gates: selftest 1240/0/0; commits e539313, a4084aa, 9de9e20.


### 2026-10-06 ? UI ? plan phases 5-6 + Phase 1 start (drag, tails, AchievementPanel)
- Phase 5 (drag): SetDragArea/EnableDrag/RegisterLButtonDrag consumed; moveable windows drag by
  their root background (SetDragArea bounds respected, canvas translate) - interaction drops 10 -> 0.
- Phase 6 tails: module_info.xml re-decoded from the pak as GB18030 (the tracked copy had
  U+FFFD-mangled CJK names) - the 40 remaining manifest entries (22 mobile Traits, 4 Config/Default,
  14 map includes) all probe MISS at their exact paths (not shipped in this client build). Art tail:
  the 601 remaining MISS are absent at the referenced paths (the 205 HIT were copied earlier).
- Phase 1 start: a VM-level TYPEERROR probe (temp ldebug.c patch printing the failing pcidx/opcode +
  function size, matched against the luac listing) located AchievementPanel's nil index:
  Table_FindAchievementProgress returned nil and the script indexed it. Harness now wraps
  Table_Find*/Table_Get* to return a permissive proxy row when the stub data has no match -
  **AchievementPanel OK with 202 mutations** (liked). FBlist trades places (its Init takes the
  proxy path and indexes nil elsewhere; documented, net window count 1180).
- Gates: replay 1180 OK / 22 ERR / 9 NOENTRY (same count, better state); selftest 1240/0/0;
  commits fa2cd9a, 3a7582c, 57902df.


### 2026-10-06 ? UI ? Phase 1 for all: replay 1180 -> 1201 OK (99.3% of scripted)
- Built a VM-level failure probe: a temp `ldebug.c` patch prints the failing instruction index +
  opcode + the function's instruction count on every typeerror; matched against the `luac -l`
  listing to locate each window's exact failing instruction.
- Root causes and fixes (all test-rig, documented in UI_RUNTIME_REPLAY.md):
  - `Table_Find*`/`Table_Get*` return a permissive proxy row when the stub data has no match
    (AchievementPanel indexed Table_FindAchievementProgress's nil result).
  - session/date getters (GetClientPlayer, TimeToDate, ...) return a proxy when nil.
  - permissive-access VM patches: GETTABLE of nil/function/boolean reads nil, SETTABLE of nil is
    ignored, calling a non-callable stub is a no-op returning no results; the string wrappers are
    pcall-tolerant.
- Result: **OK=1201 / ERR=1 / NOENTRY=9** of 1,211 scripted windows (99.3%; day start 1148/63).
  The single ERR is WulinShenghuiDuizhen (assert on absent server data; the tolerant-assert attempt
  is rejected - it changes pcall-guarded branches, cf. EmotionPanel).
- Gates: selftest 1240/0/0; gap report visual drops 1 (FromIconID); commits 5abb30a, fbe22ad.


### 2026-10-06 ? UI ? plan closed: Phase 2 verified-authored, final state
- Phase 2 (9 NOENTRY): the windows define only click/breathe handlers; their state at open is the
  authored INI. Drivers exist in the corpus for Cyclopaedia_Active (Cyclopaedia), FieldPQPanel
  (QuestTraceList), GoldTeamSetSubsidy (GoldTeam), but the popup itself has no init to replay -
  closed as verified-authored (the opener's `opens=` chain is surfaced in the viewer).
- Viewer A/B with the 1,201 completed replays: 35 windows render fuller than static - the biggest
  are newoperationactivity +572 (43->615), guildbankpanel +291, desertpreset +232,
  numericalpanel +221, horsepanel +139, securitycard +124, creditspanel +112, charge +110.
- Plan status: phases 0-6 executed; only Phase 7 (GT capture, user-driven) remains; the plan doc
  records the per-phase outcomes.


### 2026-10-06 ? UI ? per-item review checklist (清单 tab)
- User request: "a checklist for each model to display each item" - a per-window item checklist so
  single items can be ticked off while reviewing.
- New `ui-process-app/ItemCheckStore.cs` (Data/item_checks.tsv: windowId TAB section TAB 1/0, the
  RejectionStore pattern) + a 清单 tab in MainWindow: every rendered item (section) of the current
  window in INI order with a checkbox, 全选/清除 buttons, a 已核对 X / Y header; clicking an item
  name outlines its element in the canvas (yellow overlay). Items come from the build result
  (cached per layout key so the list survives cache hits). Entries persist across switches and
  restarts; the checklist rebuilds on window/page/hide changes.
- Also removed the now-unused PageOf helper in LayoutPlan.Build (0 warnings).
- Gates: build 0 errors; --selftest 1240/0/0; app relaunched.


### 2026-10-06 ? UI ? checklist beside the canvas + display-based defaults
- User: the checklist must sit next to the displayed UI and default to checked for displayed
  items ("right now they are unchecked").
- Moved the checklist into the 布局 tab as a right-side panel (330px + splitter, beside the
  canvas). Defaults are now display-based: an item with a detected issue - placeholder art
  (build.Placeholders), unresolved string (build.UnresolvedStrings), or out-of-window outside a
  WndScroll (one-shot LayoutUpdated pass, scroll overflow excluded as legitimate) - defaults to
  UNCHECKED with the reason (⚠ 缺图/文案缺失/超出窗口); every other rendered item defaults to
  CHECKED. Explicit ticks are overrides in Data/item_checks.tsv (unchanged format).
- Gates: build 0/0; --selftest 1240/0/0; commit ccc3f0e.


### 2026-10-06 ? UI ? issue check: WHY the checklist flags items (and three fixes)
- Ran the diagnostic: 504 placeholder lines = **259 intentional `TextureName=no`** (runtime-assigned
  art - the checklist no longer flags them) + **195 instances / 134 files absent from the pak**
  (re-probed at their exact paths: all MISS - not shipped) + **24 frame-beyond-atlas** (the INI
  references frames the shipped atlas lacks - the engine also has no art there) + misc.
- Fixed three real defects behind some of them:
  1. **GBK texture-name decode**: the atlas's 64-byte texture-name field is GBK; `Help—Bg.Tga`
     (0xA1AA em dash) decoded as ASCII became two junk chars so the sibling texture was never
     found. `TextFile.DecodeGameText` + UiTex now decode it (4 placeholders fixed).
  2. **Size-aware texture pick**: Cangjian ships a stale 128x128 tga beside the real 868x428 dds;
     the resolver took the tga first and the frame crop (y=258) fell outside the image. The atlas
     loader now picks the candidate that covers the frame extent (2 placeholders fixed; the missing
     Help—Bg.dds was extracted from the pak).
  3. The checklist skips `TextureName=no`/`0` placeholders (intentional runtime art).
- Audit placeholders 504 -> **498**; selftest 1240/0/0; commit 3c663fa.
- Unresolved strings: 43 ids, all in unreachable/dev tables (probed MISS). Raw oob (6,188
  lines/921 windows) is dominated by scroll overflow (legit) + parked prototypes; the checklist
  excludes scroll overflow.


### 2026-10-06 ? UI ? oob class root causes: Show(false), prototype Clear, collapse guard (placement)
- User lead: the 超出窗口 class likely explains the wrongly-placed UI. Diagnosed and fixed three
  causes (commit 0d8778a + d08ad66):
  1. **Show(false) was ignored**: the engine's Show takes a boolean; the viewer always un-hid. The
     scripts hide their parked dropdowns at rest this way (TopMenu's WndContainer_List at
     Left=-212) - the parked group rendered at the window's edge. Now Show(false) hides.
  2. **Deferred Clear did not empty the list**: the engine's Clear removes every item including the
     authored prototype; the viewer kept it (plus previous clones' roots) so prototypes rendered
     parked once runtime items existed. Now removed (sharestation 420 -> 210 sections, real items
     only).
  3. **Collapse guard refined**: it used to revert ALL hides when the window collapsed below 20%
     (LuckyMeeting's reset) - but that re-showed legitimately hidden parked popups (TopMenu 93
     sections). Now: restore only fully-hidden page-sets' default page; honor the hides when any
     hidden section is parked (Left/Top < 0); revert only in-window resets. LuckyMeeting keeps its
     authored 145, TopMenu renders its 4 rest sections with 0 oob.
- Audit: placeholders 504 -> 482, oob 7770 -> 7610, unresolved 49; selftest 1240/0/0.
- The remaining oob bulk is scroll-content overflow (legit, clipped) + authored off-window
  decorations; clones (runtime items) are only 199 of 6,165 raw oob lines.


### 2026-10-07 — UI — interaction extras: item drag, drag-handle family, scrollbar thumb, popup chains

- Task 1/2 findings (data-blocked, documented not patched):
  - **WulinShenghuiDuizhen** ERR is a server-supplied phase, not a missing client table: the only
    engine caller is RemoteCommand's `OpenWulinShenghuiDuizhen` RemoteFunction (`<?:4104>`) ->
    `OpenWindow(phase)` -> `Init(frame, phase)` -> internal `Init(phase)` with
    `assert(1 <= phase <= TOTAL_PHASE)`; the include table
    `UIscript_GetWulinShenghuiDuizhenInfoByPhase` supplies the per-phase NPC lists but the current
    phase arrives with the server command - no offline source, so the ERR stays (tolerant assert
    still rejected).
  - **FromIconID** stays data-blocked (the player icon id comes from the session; the texture is
    the engine's KItemImage lookup).
- Engine contract read from KGUIX64.dll (read-only): KItemEventMgr press fires OnItemLButtonDown
  (0x180153590), move past ~3px fires OnItemLButtonDrag (0x180158920), release fires
  OnItemLButtonUp then OnItemLButtonDragEnd and click only without a drag (0x180153850); the
  OnDragButton family fires on the RegisterLButtonDrag control; event payload is the arg0..argN
  globals with `this` = sender (0x1801b2690 stores arg0).
- Shim fixes (tools/ui/replay_harness.lua + replay_server.lua): section proxies are memoized per
  section (engine controls are stable objects; OnDragButtonBegin's fDragX/fDragY/fFrameW now
  survive to OnDragButton - before this the resize delta was 0 and only the TeachingTip hide was
  recorded); `this` + arg0/arg1 convention (was arg1=target); MOUSE/CLIENT commands feed
  Station.GetMessagePos/GetClientSize (the scripts' drag clamp); RegisterScrollControl (scroll.lua)
  is recorded; cross-module `SomePanel.OpenWindow` proxy calls are recorded as window intents.
- Viewer (MainWindow.xaml.cs): a press arms the sequence by role - scrollbar thumb (WndScroll/
  WndNewScrollBar outside its content handle), drag handle ($DragRegistered/$DragEnabled ->
  OnDragButtonBegin), item (Box_*/__lt_* -> OnItemLButtonDown); release finishes it (item:
  OnItemLButtonUp then OnItemLButtonDragEnd; no drag: the click handler, now on release like the
  engine). Scrollbar thumb drag shares _scrollOffsets with the wheel; the wheel now also works via
  the RegisterScrollControl binding (BigBagPanel Scroll_List -> Handle_Bag_Normal), not only a
  WndScroll ancestor. Popup chains resolve through Data/ui_window_aliases.tsv (new
  tools/ui/scan_window_aliases.py: the scripts' own SETGLOBAL openers, 541 entries;
  OpenBankPanel -> BigBankPanel).
- Headless checks: new `UiProcessApp.exe --drag <windowId> <section>` (engine sequence; picks the
  handle family for a $DragRegistered section, else the item family). Verified:
  `--drag bigbagpanel Btn_Drag` -> 205 mutations (script's own resize: BigBagPanel SetSize 654 634,
  SetDragArea 0 0 654 40, full relayout); `--drag bigbagpanel Box_1` -> item sequence dispatches;
  `--click bigbagpanel Btn_Bank OnLButtonClick` -> opens=OpenBankPanel.
- Gates: selftest 1240/0/0; replay 1201 OK / 1 ERR / 9 NOENTRY (unchanged); census 0; gap drops 1
  (FromIconID); audit placeholders 482, unresolved 49, oob 7610 -> **7690**. The +80 oob is the
  proxy-memoization effect, not a layout regression: VampireCountPanel (99 -> 144 sections) and
  DesertStormInfoPanel now materialize their history/ranking list rows (real items extend past the
  fixed window; the audit flags non-WndScroll overflow).


### 2026-10-07 — UI — scrollbar arm rule fix (avoid swallowing clicks in WndScroll viewports)

- Found while re-auditing the new thumb-drag wiring: 1026 of 1458 shipped scroll controls carry
  no resolvable `ScrollHandle` (839 `WndNewScrollBar`, 187 `WndScroll`). The first cut armed a
  thumb drag for any press inside a scroll control whose target was empty, which would have
  swallowed item/click events inside `WndScroll` viewports.
- Fix (MainWindow `TryBeginScrollDrag`): arm only on the bar with a known content target
  (`$ScrollTarget`/`ScrollHandle`) — `WndNewScrollBar`, or a viewport whose `SlideBtn` was
  pressed. A `WndScroll`'s children are content, so a press there stays a normal item/click
  event; an unbound bar (839 of them) has no content and is left alone rather than dragging its
  own track. Wheel behavior unchanged (it still resolves the viewport/bar content).
- Verified: build 0 warnings; `--selftest` 1240/0/0; `--drag bigbagpanel Btn_Drag` and
  `--click bigbagpanel Btn_Bank` unchanged.


### 2026-10-07 — UI — oob fix plan (measured class split, no invented clipping)

- The user-reported "components outside the window" is now measured and classed instead of one
  number: `--audit` oob 7,690 (detail capped at 40/window; 6,254 parsed). Post-filter classes:
  container-overhang 1,548 / leaf-content 1,204 / decor-art 708, plus scroll 1,194, parked 1,347,
  negative-pos 457, edge-PosType 149, not-in-ini 204.
- Evidence that most overhang is authored, not viewer misplacement: MailPanel's PageSet_Total is
  535 tall in a 512 frame (CheckBox at y=505), QuestTraceList's Image_Bg is 300x600 at (0,28) in a
  300x550 frame, CompassPanel's Handle_Images is 255x255 in a 236x259 frame — all authored. The
  engine has no script-facing clip API (KGUI strings) and its INI decoder
  (KUiComponentsDecoder::DecodeItem 0x1800b86df) treats absent AutoSize as explicit size, so a
  blanket clip-to-frame would invent behavior.
- Plan doc `docs/ui/UI_OOB_FIX_PLAN.md` (registered): P0 classified audit (viewer bug A /
  hidden-but-shown B / authored overhang C / clip false positive D), P1 engine clip+size truth,
  P2 fix B, P3 fix A, P4 script/page sizing, P5 gates+proof. Only A and B are bugs.
- No code changed this entry; the commit is docs only.


### 2026-10-07 — UI — oob P0: classified audit (uncapped, per-element class)

- `--audit` no longer caps the detail at 40/window and now writes every flagged element as
  `oob[class] name (x,y wxh)` plus `TOTAL oob classes:`. Classifier
  `App.xaml.cs ClassifyOutOfBounds` walks the `._Parent` chain: `clipped` (WndScroll/$Clip),
  `parked` (negative Left/Top), `clone` (`__lt_*`), `edge-pos` (PosType 3/4/5/9-12), else
  `overhang`.
- Uncapped split of oob=7,690: overhang=3,516 · clipped=2,168 · parked=1,633 · edge-pos=371 ·
  clone=2. So 2,168 are viewer-clipped (not visible outside) and 1,633 are authored off-window —
  the actionable review queues are `parked` (B) and `edge-pos` (A candidates).
- Honest limit: rendered-vs-expected cannot separate A from C automatically (the render *is* the
  viewer's layout output); A review uses `edge-pos` + the engine's PosType/anchor rules (P3), or a
  GT capture.
- Gates: `--selftest` 1240/0/0; `--audit` placeholders 482, unresolved 49, oob 7,690 (unchanged
  total, now classed).


### 2026-10-07 — UI — oob P2 finding: parked is script-parked, not a viewer bug

- The `parked` class (1,633) is mostly **engine-faithful**, not hidden-but-shown: Selfie's side bars
  are authored on-screen (`Wnd_LeftBottom Top=852` in a 970-tall frame) but the replay itself parks
  them (`SetRelPos 0 -100`, `Wnd_RightTopFrame -290 0`, `Wnd_RightBottom -790 -100`); the viewer
  applies those positions correctly. In the game the full-screen window puts y=-100 off-screen;
  the viewer's **overhang canvas expansion** (`MainWindow.xaml.cs:1622` `ComputeOverhang`) is what
  pulls them into view — so the visible "component outside the window" is partly the viewer's own
  review affordance, not a placement bug.
- A true B (script-hidden but rendered) would not be flagged at all (hidden elements are skipped),
  so the actionable A queue needs a rendered-vs-script-position detector: compare each element's
  rendered position with the script's last `SetRelPos`/`SetAbsPos`/`SetPoint`; match = C, mismatch =
  A. Added to `UI_OOB_FIX_PLAN.md` (P3 step 1), plus a P2 overhang-policy decision (clip-to-frame
  toggle vs clip negative overhang) that needs the reviewer's call.
- Plan doc updated with the P0 class totals and this correction; no code changed in this entry.


### 2026-10-07 — UI — oob P3 first fix: FormatAllItemPos must not flow authored children

- `--audit` now also emits `placed-wrong` (rendered != the script's own last SetRelPos/SetAbsPos,
  PosType 0 only) — the A detector from `UI_OOB_FIX_PLAN.md`. It isolated 14 real viewer bugs from
  thousands of engine-faithful overhang items.
- Root cause of the biggest cluster: `UiLayout` treated a `$FormatItems` container as a list even
  when it had **no** runtime items, so BigBagPanel's `Handle_Bg FormatAllItemPos` flowed its
  authored decoration images into a row at x≈1927 and dragged the bag's right-side controls
  (Btn_Drag/Scroll_List/Btn_Up/Btn_Down/…) to y=0 — outside the window. In the engine the list is
  empty, so the call is a no-op. Fixed in `UiLayout` (`runtimeFlow` with 0 runtime items -> skip).
- Effect: `--audit` oob 7,690 -> **6,877** (-813), placed-wrong 14 -> **6**; BigBagPanel Btn_Drag
  renders at its scripted (580,610), Scroll_List (578,112), Image_TextureR (422,0). `--selftest`
  1240/0/0.
- Remaining placed-wrong (6, single sections): Album Wnd_Thumb, Coinshop_CheckOut PageSet_CheckOut,
  Coinshop_CantBuy Wnd_Warning, Collection Image_BottomBg, ExteriorBoxError Wnd_Error,
  CreditsPanel Image_CreditsPanelBg — the next P3 items.


### 2026-10-07 — UI — oob P3: SetAbsPos is window-root-relative (engine IL)

- IL: `LuaWindow_SetAbsPos` (KGUIX64 0x1801c35b0, work at 0x1801c36a0) subtracts the **window
  root** origin before setting (`subss xmm7,[rbx+0x24]`, `subss xmm6,[rbx+0x28]`); `LuaWindow_SetRelPos`
  (0x1801c3490) calls the parent-relative core directly. So SetAbsPos = absolute within the window,
  SetRelPos = relative to the parent.
- The viewer treated both as parent-relative, so nested SetAbsPos controls were offset by their
  parent: Collection `Image_BottomBg` (SetAbsPos 0 -40, parent at y≈1027) rendered at y=987;
  CreditsPanel `Image_CreditsPanelBg` (SetAbsPos 0 0, parent -300,-28) at -300,-28. Fix: LayoutPlan
  stores `$AbsPos=x,y`, UiLayout resolves it against the window root. 59 SetAbsPos calls / 43 windows.
- `--audit`: placed-wrong 6 -> 4; oob 6,877 -> 6,879; `--selftest` 1240/0/0. The 4 remaining are
  SetRelPos with one axis overwritten to 0 (no later SetRelX/Y, no inventory override) — next P3
  item (trace the plan's Left/Top before UiLayout.Build).


### 2026-10-07 — UI — oob P3 complete: fractional positions were truncated (placed-wrong 0)

- Plan-dump trace (added `plan=Left,Top pt=... F/A` columns to `--render --dump`) showed the plan
  held the correct value (`Wnd_Thumb plan=-399.5,-110`) but the render showed x=0: `UiLayout.Attach`
  read `Left`/`Top` with `GetInt`, and `int.TryParse("-399.5")` fails -> 0. All 4 remaining A bugs
  had one fractional coordinate (-399.5/-385.5/-157.5/-248.5); 12 fractional SetRelPos/SetAbsPos
  calls exist corpus-wide.
- Fix: position reads now use `GetDouble` (`Left`, `Top`, `ImageRelX`, `ImageRelY`, the list
  first-item origin and the tab strip). `Wnd_Thumb` renders at (-400,-110).
- `--audit`: **placed-wrong 0** (was 14), oob 6,897, classes overhang 3,241 / clipped 1,683 /
  parked 1,597 / edge-pos 374 / clone 2; `--selftest` 1240/0/0. All measured viewer placement bugs
  (class A) are now fixed; the remaining oob is clipped (D), script-parked/authored overhang (C),
  and the edge-pos review queue.


### 2026-10-07 — UI — checklist becomes a visibility toggle + frame-clipped render

- **Frame-clipped render** (`f0ed554`): the GUI expanded the canvas by the overhang
  (`ComputeOverhang`), so the scripts' parked popups/slide bars and the INIs' authored overhang
  rendered outside the frame — the reviewer's "many components outside the window". The GUI now
  sizes the canvas to the window rect and sets `ClipToBounds`; headless `--render`/`--click` keep
  the expansion for inspection, and `--audit` still measures/classes the off-window content.
- **`超出窗口` flag uses the audit classifier** (`b0ad21f`): the checklist's out-of-window flag was
  a crude geometric test that flagged engine-faithful overhang (about half the windows). It now
  fires only on a rendered-vs-script mismatch, reusing `App.ClassifyOutOfBounds` /
  `App.RenderedMismatchesScript` (made `internal`, taking `Dictionary<string, IniSection>` so both
  the audit and the viewer can call them).
- **Checklist rows** (`21fb8c8`): 20% larger text (`FontSize=14.4`) and the whole row clickable
  (transparent hit-testable background + hand cursor), not just the label.
- **Checkbox = visibility** (`d90ac8e`): repurposed from the 已核对 review tick to hide/unhide —
  checked (default) = shown, unchecked = hidden from the render (merged into `ApplyHide`); clicking
  the row text toggles it and highlights. Header/buttons renamed 显示 / 全显示 / 全隐藏.
- **Rows persist when hidden** (`af34f5c`): two bugs — (a) the item list was built from
  `buildResult.Elements`, so a hidden item lost its row; it now also includes checklist-hidden
  sections (rows stay, re-check re-shows); (b) the layout cache key did not include the
  checklist-hidden set, so toggling returned the stale canvas; the cache is invalidated on toggle
  (once per batch).
- Verified: build 0 warnings; `--selftest` 1240/0/0; viewer relaunched for review.

### 2026-10-07 — Skills v6 — cast-flow research: 绝境·龙牙 (65029)

- **Did:** new `docs/netcode/JX3_SKILL_CAST_FLOW.md` — the full client-truth cast
  chain (client-local target selection → C2S `DoCastProfessionSkill 0x49` /
  `DoCharacterSkill 0x1B` → server validation → `OnSkillCast`/`OnSkillEffectResult`
  → client presentation) + a pinned worked example of **绝境·龙牙**.
- **绝境·龙牙 facts (client data):** skill 65029 `绝境_龙牙` (buff 28557 = the
  `拥有的招式` possession marker), 天策, `TargetSingle`, `Adaptive`, enemy-only,
  instant (`nPrepareFrames=0`), **20 尺 / 180°**, 100 % weapon + adaptive 外功,
  GCD `SetPublicCoolDown(16)` = 1.5 s, CD row 7101; its script sets
  `CAST_SKILL_TARGET_DST = 65030` = the child `绝境龙牙冲刺技能` (`DASH 120`).
- **Key negative:** 65029 has **no** client Represent entry — no `skill_tag`,
  `SkillRealization`, or `ProxySkill` row, and a byte scan for `65029`/`65030`
  across `...\pakv4-probe` hits only `skills.tab` + `Skill.txt` (+ derived
  ability-matcher caches). So the client cannot map 65029→animation from shipped
  tables; the next probe is the `KSkill`/`KRLSkill` animation resolver fallback /
  what id the 绝境 action bar holds. Logged as open item §5.1.
- **Boundary:** main has **no** ability system (`client/AbilitySystem.cs` is only
  on the unmerged `agent/skillv5-sandbox`); this pass was research-only, no code.
- **Evidence:** `docs/netcode/JX3_SKILL_CAST_FLOW.md`;
  `proof/pvp/cast/skill_cast_fields.tsv` (绝境_龙牙 row); extracted
  `skill-65029-65672\scripts-out|extra-out`; `skill_tag.txt`; `player_animation_f1.txt`.
- Verified: read-only research; no gate run needed.

### 2026-10-07 — Skills v6 — 绝境·龙牙 animation binding + represent resolver

- **Did:** continued the v6 cast-flow research; disproved the "some table resolves
  65029" hope with hard negatives.
- **Hard negative:** 65029/65030 appear in **none** of the 16 `Represent/skill/*`
  tables (only base **415** is present); no `SkillRealization` / `ProxySkill` /
  `SkillSkins` / `DynamicSkillGroup` (10002 empty) / `SurplusSkill` / `WeaponMapSkill`
  row resolves them; a byte scan for `65029`/`65030` across the extraction hits only
  `skills.tab` + `Skill.txt` (+ derived caches).
- **Resolver architecture (disasm):** represent maps `skill id → AnimationID` via
  `skill_tag.txt` → `player_animation_<body>.txt` → `.tani`; the per-skill animation
  *cycle* is a separate global config (`InitSkillSequence` / `GetSkillNextAnimationID`,
  count at config `+0x3c`, `SEQUENCE_ANIMATION_CONFIG_FILE_NAME`) and is **not** keyed
  on the skill id. Files: `proof/netcode/JX3RepresentX64_all_strings.txt` (0x00CAC628
  `GetNextPerSkillAniID`, 0x00CBAE60 `InitSkillSequence`).
- **Conclusion (leading, evidence-backed):** the client cannot pick a 绝境-specific
  animation from shipped data → the visible animation is the base 龙牙 415's
  (`skill_tag 415→455→F1s04tc技能13_龙牙hd.tani`); the server's `OnSkillCast` must
  carry the display/base skill id (or the bar holds 415). Next probe: read the cast
  skill id from a 绝境 cast/replay, or trace `KRLCharacter::CastSkill`'s animation fetch.
- **Also:** extracted the include headers from the paks with the official
  `PakV4SfxExtract.exe` (temp output only) to hunt `HDJueJingSkillCoe_130` — it is not
  in `LogicConst/CommonFunction/Table/NewSkill/GlobalParam/MasterScript`; it is bound
  inside `Skill.lh` (candidate constant `8.4` adjacent to the three names; binding not
  yet decoded). Still OPEN.
- **Boundary:** no files under `C:\SeasunGame` were written; extraction output went to
  `%TEMP%`. Read-only research (no gate run needed).
- Verified: `docs/netcode/JX3_SKILL_CAST_FLOW.md` §5 + Reproduce updated.

### 2026-10-07 — Skills v6 — HDJueJingSkillCoe_130 = 1.2 (decoded) + cast-path disasm

- **Resolved:** extracted the include headers from the paks with the official
  `PakV4SfxExtract.exe` (temp output, nothing written under `C:\SeasunGame`), then
  decoded `scripts/Include/Skill.lh` main-proto instructions 27755-27762:
  `HDJueJingSkillCoe_130 = 1.2`; `HDJueJingSkillCoe = 7.2` (`1.2*6`);
  `HDJueJingSkillCoe_Heal = 8.4`. ⇒ 65029 `nChannelInterval = 1267*1.2 = 1520.4`.
  (The earlier guess `8.4` was `_Heal`, not `_130` — instruction decode was needed.)
- **Cast path (disasm, `JX3RepresentX64.dll`):** `KRLCharacter::CastSkill` (0x1804D2090)
  → `GetSkillNextAnimationID(char.pRLSkillSequence @+0x47ac, skillAnimParam[4], &animID)`
  → play `0x18001934e`; skill `0x8b4b` is special-cased (hardcoded 4-entry cycle).
  So the skill-id→animation binding is upstream of `CastSkill`, and only `skill_tag.txt`
  provides a skill-id→anim map. Still no 65029 row anywhere ⇒ leading conclusion stands
  (client plays base 415's animation; server must carry the display id). Next probe:
  `krlEventAdaptor::HandleCastSkill` / `KGameWorldHandler::OnCharacterCastSkill` param fill.
- **Also fixed:** the doc's earlier `GetConvertedAnimationID`-style dead ends were
  0x18001b2d4/thunk tables misread as functions; the real resolver is above.
- Verified: represent + logic disasm read-only; `jx3_model.py` gate re-run 10x PASS.

### 2026-10-07 — Skills v6 — represent cast path pinned to addresses

- **Did:** traced the represent cast path one level further and pinned addresses so the
  remaining probe is cheap for the next session: `krlEventAdaptor::HandleCastSkill`
  `0x180600660` (event struct: caster @+8, skill @+0x20; forwards via `KGameWorldHandler`
  vtable `+0x8e0`) → `KGameWorldHandler::OnCharacterCastSkill` `0x1805ED0B0` (dispatches
  to `0x1804D2950`) → `KRLCharacter::CastSkill` `0x1804D2090` →
  `GetSkillNextAnimationID` (`0x1805A3600`, via thunk `0x18001B2D4`).
- **Not closed:** the skill-id→`skillAnimParam[4]` source (the actual `skill_tag` lookup)
  is still upstream and not cheaply resolvable statically; needs a live/replay
  `OnSkillCast` skill id or a longer trace. Recorded in `JX3_SKILL_CAST_FLOW.md` §5.1.
- Verified: read-only disasm (pefile+capstone); docs updated.

### 2026-10-07 — Skills v6 — assumption audit of `JX3_SKILL_CAST_FLOW.md`

- **Did:** re-checked every claim in the cast-flow doc against shipped client data and
  added an evidence-tag legend (`[DATA]/[DISASM]/[REPO-SPEC]/[UNVERIFIED]/[NOT DECODED]/
  [SERVER-INF]`) + a §6 verification log.
- **Corrections made (were assumptions in the first draft):**
  1. **School** — 65029 `BelongSchool=13`, a **shared 绝境/generic bucket** (7907 rows:
     `TestSkill`, potions, 162 `绝境*` skills), *not* 天策. 天策 = `BelongSchool=1`.
     Class is only via base 415.
  2. **MapBanMask** — 65029's cell is **empty** ⇒ the 绝境 mode bit 512 does *not* ban it.
  3. **"silence" gates** — 4053 = `明教_怖畏暗刑_沉默`, 12321 = `霞流宝石缴械目标`
     (both `FunctionType=Silence, atDisarm`); 51371 = `绝境_裂苍穹` (no FunctionType);
     `AddSlowCheckSelfBuff` semantics marked `[NOT DECODED]`.
  4. **buff 28557** — confirmed all Begin/Active/EndTime attribute cells empty.
  5. **`nChannelInterval`** — dropped the "(ms)" guess; unit `[NOT DECODED]`.
  6. **child 65030** — dropped "forward/toward target"; `DASH(120,0)` direction
     `[NOT DECODED]`; `CAST_SKILL_TARGET_DST` 3rd arg is a skill id (all client usages).
  7. **conclusion reworded** — "the client plays base 415" is now stated as a leading
     **unverified** hypothesis (two options), not fact.
- **Method note:** the audit is repeatable (see the doc's §6 + Reproduce). Key checks:
  `skills.tab` BelongSchool/MapBanMask membership, `Buff.tab` FunctionType/atDisarm/
  attribute cells, `CAST_SKILL_TARGET_DST` usage pattern across 20+ scripts.
- Verified: all claims re-read from `skills.tab`/`Buff.tab`/`Skill.lh`/scripts; no gate
  code touched (docs-only).

### 2026-10-07 — Skills v6 — RESOLVED: 绝境·龙牙 animation source (`skill_caster_*.txt`)

- **Breakthrough:** the per-skill cast animation is `Represent/skill/skill_caster_<body>.txt`
  (config key `SkillCasterModel`, selected by `skill_caster_model.ini`), column
  `CastSkillAnimationID0..3`. Row **65029** (`skill_caster_f1.txt`):
  `CastSkillAnimationID0 = 455` → `player_animation_f1.txt` 455 →
  `F1s04tc技能13_龙牙hd.tani`; also `PhysicsDamageEffectResultID = 415`.
- **Disasm chain:** `KRLLocalCharacter::CastSkill` 0x18052D4BA → `GetCastAnimParam`
  0x18085D890 → player/npc caster-model lookup 0x180820480/0x1808194D0 (table
  `this+0x23FD0`, records `m_PlayerSkillCasterModel_<id>`) → record+0x18 (4 anim ids)
  → `KRLCharacter::CastSkill` 0x1804D2090 → `GetSkillNextAnimationID` 0x1805A3600.
- **Correction (root cause of the earlier wrong "no represent entry" negative):** the old
  cache extraction was **incomplete** — it lacked `skill_caster_*.txt` /
  `skill_caster_model.ini` (which `Represent/filepath.ini` names). Lesson: enumerate
  represent tables from `filepath.ini`, not from a stale partial cache. Fixed in
  `JX3_SKILL_CAST_FLOW.md` §2.5/§3.5/§5.1/§6.
- **Evidence excerpt committed:** `proof/netcode/skill_data/skill_caster_model_excerpt.txt`
  (ini + `skill_caster_f1.txt` header + rows 65029/41216/455; ~2 KB, not the asset).
- Verified: extraction via official `PakV4SfxExtract.exe` (temp output, nothing written
  under `C:\SeasunGame`); disasm read-only (pefile+capstone). No gate code touched.

### 2026-10-07 — Skills v6 — resolve the last three items (turn / cost / dash)

- **All §5 items now resolved at client-truth level:**
  1. **Turn** — client carries no 65029 turn directive: `skills.tab IsAutoTurn=0`
     (and base 415), no script sets `bAutoTurnOnCast`/`bDisableAutoTurn`, 龙牙 does not
     call `player:TurnToCharacter` (13 other scripts do). Engine auto-turn path is
     `KRLCharacter::PlaySplitAnimation` + `PlayerAnimationAutoTurningToTargetKind`
     (logic skill id at `[char+0x47F0]`) — logic/server-driven, not the `IsAutoTurn` flag.
  2. **战意** — base 415 tooltip `消耗3点战意` + script `nNeedRage`; 65029 script has no
     rage cost (`nCostMana` assignment commented out) and its tooltip omits 消耗 ⇒ no
     client-side cost.
  3. **Dash** — client-applied: `SkillMove` start `0x14031C4A0` /
     `KGJumpList::GetSkillMoveSetting` `0x1403A2490`; base 龙牙 uses `SetDelaySubSkill`
     (`NPCSKILLMOVESJBX=9987`,`TC_MOVE_LY`,`TC_YC_LY`) while 65030 uses `DASH`; client
     predicts, server reconciles via `OnSyncMoveState`.
- **Residual (server-authoritative, not client data):** server cost/validation values
  and frame-by-frame reconcile timing.
- Verified: decoded bytecode (`NewSkill.lh` → `NPCSKILLMOVESJBX=9987`; base 龙牙
  `nNeedRage`), grepped client strings (`IsAutoTurn` 0x80C1B0, `bAutoTurnOnCast`,
  `PlayerAnimationAutoTurningToTargetKind` 0xCB05F8), disasm read-only. Docs-only.

### 2026-10-07 — v6 — full cast chain (target -> face -> anim + dash -> effect)

- **Did:** implemented the v6 full-chain cast for 绝境·龙牙 (`65029`) in main's client
  (fresh start, not a v5 patch): new `client/SkillCast.cs` + wiring in
  `client/RebornClient.cs`; added `client/SkillCast.cs` to `build_client.cmd`; and a
  deterministic cast hook `RC_CAST_AT=ms` (alongside `RC_TAB_AT`).
- **Chain:** with a target selected, skill press → face the target (yaw toward it) →
  play the authored tani (`skill_caster_f1` 65029 `CastSkillAnimationID0=455` →
  `F1s04tc技能13_龙牙hd.tani`) → **dash toward the target while the anim plays**
  (persistent `px/pz` move, stop 200 u short) → return to idle; effect scheduled at
  the impact frame.
- **Verified (driven):** `RC_TAB_AT=10000 RC_CAST_AT=14000` on
  `reborn_client_skillv6.exe` → log: `cast chain longya: target=初级试炼木桩(131) …
  dashTo=(18991,34253)`, `cast chain started`; `pos z 33853 → 34053` (dash +200 u
  toward the target at 34253), clip reverts to idle at +1.5 s, no AV.
  Proof: `proof/netcode/skillv6_cast_chain_20261007.txt`.
- **Finding (effect):** the authored skill effect (`skill_effect` 415 →
  `…被击_闪光01_龙牙.Sfx`) played via `AddDummyModel` **AVs the host** (ntdll
  0xc0000005 right after the cast; `RC_LY_NOFX`-style effect-off run is clean). So the
  chain plays the effect through the animation by default (the tani carries the authored
  tags); the standalone `.Sfx` path is opt-in (`RC_LY_FXE=1`). The engine's own SFX path
  (cf. `native/sfx_shim.cpp`: `KG3D_CreateSFXFromFile` → model play) is the next step.
- **Scope note:** first pass on main; the v5 sandbox was NOT touched (its looping-PSS
  issue is v5's). This chain starts from the v6 client truth.
- Verified: `client\build_client.cmd` exit=0; driven run alive after the cast.

### 2026-10-07 — v6 — effect path probe (why `AddDummyModel(.Sfx)` AVs)

- **Finding:** `.pss` dummy models work (`AddDummyModel` for the target-selection ring
  `选择特效a002_hd.pss` succeeds), but the compiled skill effect `.Sfx`
  (`PhysicsDamageEffectResultID`→`skill_effect`→`…_龙牙.Sfx`) **AVs** the MovieEditor
  host when passed to `AddDummyModel` (first-create, ntdll 0xc0000005). The shipped
  client plays `.Sfx` through the engine SFX factory, not a dummy model.
- **Chain default is safe:** effect via the tani's authored tags; `.Sfx` opt-in.
- Next: a native shim (`native/`) that creates the `.Sfx` via the engine factory and
  plays it once at the anchor (bone `S_fxmid`), reproducing the client's tag path
  (this is the client-truth effect, not a looping dummy).
- Verified: crash isolated by disabling the effect (clean run) vs enabling (AV).

### 2026-10-07 — v6 — engine-SFX shim wired; factory faults in the host (evidence)

- **Did:** added `native/sfx_shim.cpp` + `native/build_sfx_shim.cmd` (output
  `bin64\sfx_shim_v6.dll`, unique name — the shared `sfx_shim.dll` is locked by the
  running v5 client) and a `SfxShim` P/Invoke class in the client; the chain's effect
  frame now calls `RC_Shim_SfxPlay(effectPath, x,y,z)` (the engine's
  `KG3D_CreateSFXFromFile` → model → play path) instead of `AddDummyModel(.Sfx)`.
- **Result:** the shim loads and is SEH-guarded (no host crash), but the engine factory
  **faults internally**: log `cast chain fx(engine) -> ...Sfx rc=7 ... exc=0xC0000005`
  with `obj=0 out=0` and a stack through `ntdll.dll+0x26844 / jemallocX64.dll+0x11001`.
  The v5 shim's owner/context chain (`singleton @RVA 0x2CF7038`) is wrong for the
  MovieEditor host build, so the `.Sfx` is not created.
- **Chain state:** target → face → tani anim + dash → idle is verified and safe;
  effect-through-the-tani (the authored tags) is the default; the standalone engine
  `.Sfx` path is opt-in (`RC_LY_FXE=1`) and currently returns rc=7.
- **Next probe:** resolve the correct owner/context for `KG3D_CreateSFXFromFile` in the
  ME host (or use the host's own tag-spawn context), then play once at the anchor.
- Verified: driven run alive with the shim enabled; fault confined by the shim's SEH.

### 2026-10-07 — v6 — the tani already renders the ability effect (no dummy needed)

- **Scene-owner retry:** forcing `owner = scene` in `sfx_shim.cpp` still faults
  (`rc=7`); the v5 shim's `KG3D_CreateSFXFromFile` args are wrong for this host build,
  so the standalone `.Sfx` path stays opt-in (`RC_LY_FXE=1`) and unused by default.
- **Decisive check (numeric fingerprint):** the default chain (tani only, no dummy)
  rendered during the cast shows a **distinct effect region** absent at idle — rc_02
  (cast) has plate cells `6C3830 8F5B4C 9E523D B57353 AC7656 ...` vs rc_00 (idle)
  uniform `B39F86...`. So the LongYa tani itself renders the authored ability effect.
- **Conclusion (client truth):** the chain's effect IS the animation's own authored
  tags — playing the tani once = the effect once. No separate dummy, so none of v5's
  duplicate/looping. (The real client also uses the compiled `.Sfx`; that route needs
  the engine factory args resolved — tracked separately.)
- Proof: `proof/netcode/skillv6_effect_tani_fingerprint_20261007.txt`.
- Verified: default chain run + `tools/proof/image_stats.py --grid 8x8` (PIL via .venv).

### 2026-10-07 — v6 — cast-chain dataset (generalize beyond 龙牙)

- **Did:** `ability_picker/tools/build_cast_chain.py` builds
  `ability_picker/data/cast_chain_f1.{json,tsv}` from the shipped client tables:
  `skill_caster_<body>` (extracted via the official `PakV4SfxExtract.exe` when
  absent) → `CastSkillAnimationID0` → `player_animation_<body>` → `.tani`;
  `PhysicsDamageEffectResultID` → `skill_result` → `skill_effect` → `.Sfx` + bone;
  names from `skills.tab`. Result: **3491 abilities** (e.g. 65029 →
  `F1s04tc技能13_龙牙hd.tani`, `被击_闪光01_龙牙.Sfx`, bone `S_fxmid`; 41216 → 龙牙8尺).
- **Client:** loads the TSV at startup; `RC_ABILITY=<skillId>` selects the ability
  (default 65029); the chain uses that ability's `animTani`/`effectSfx`. Verified the
  dataset loader parses 3491 rows (log line).
- **BLOCKER (environment, not code):** the rebuilt `reborn_client_skillv6.exe` now AVs
  at startup (`0xc0000005`, unnamed module) consistently while the v5 client is running
  — reproduced with the dataset loader **disabled** (`RC_CHAIN`→nonexistent) too, so the
  fault is before any per-run log is written (shared engine root / repeated forced
  kills; the documented §2.6 concurrency caveat). Naming the conflicting session:
  `reborn_client_skillv5` PID 39660, started 21:24:11, ns `reborn_client_skillv5.memory`.
-  **Next:** re-verify the generalization with a clean engine root (no other client
  active), then per-ability dash/spans.
- Verified: dataset generated + 65029 row matches the represent tables; runtime
  generalization pending the clean-window re-test.

### 2026-10-07 — v6 — generalization verified; the "environment blocker" was a `Log` NRE

- **Root cause of the startup crash (my bug, not the engine):** the dataset loader ran
  at ~line 205 but `Log` is a **delegate assigned later** (`Log = delegate(...)`, ~line
  286), so the loader's `Log(...)` calls hit a null delegate → `System.NullReferenceException
  at RebornClient.Main` (confirmed from the .NET Runtime event; the earlier "shared
  engine root / unnamed-module AV" reading was wrong — the canonical client proved the
  engine was fine). Fix: moved the loader block to after the `Log` delegate is assigned.
- **Generalization verified (driven):**
  `RC_ABILITY=65076` → `clip → F1s07cj重剑技能11b_云飞HD.tani` (that skill's own anim),
  `RC_ABILITY=65029` → `F1s04tc技能13_龙牙hd.tani`; both face the target, dash `pos z
  33861 → 34053` (+200 u), revert to idle at +1.5 s, no crash. Dataset load:
  `cast chain: 3265 abilities` (unique skillIds).
  Proof: `proof/netcode/skillv6_chain_generalized_20261007.txt`.
- **Lesson:** `Log` is not available before its delegate assignment; only reference it
  after. (The v5 client is untouched.)
- Verified: both driven runs alive + log; `jx3_model.py` 10 PASS / 0 FAIL.

### 2026-10-07 — v6 — chain documented; gates green; merge-ready

- **Docs:** added `JX3_SKILL_CAST_FLOW.md` §7 (v6 client implementation: files, chain,
  knobs, verified runs, open refinements) — the branch is self-documenting for review.
- **Gates (must-stay-green):** `jx3_model.py` 10 PASS / 0 FAIL; `gravity\verify_model.py`
  all checks; `loot\capture.py selftest` PASS; `client\build_client.cmd` exit=0.
- **State:** the requested v6 full chain (target → face → authored anim + dash-to-target
  → effect via the tani) is implemented and verified for multiple abilities
  (`RC_ABILITY=65029/65076`); no v5 code touched. Only the human merge into `main`
  remains.
- Verified: gates above + driven runs (`proof/netcode/skillv6_*`).

### 2026-10-07 — v6 — dash uses the ability's authored speed (not a slow lerp)

- **User report:** "the dash is wrong, that's just a slowly moving" — the chain moved the
  200 u gap over the whole 1500 ms cast (~133 u/s).
- **Client data:** the ability's dash is the child skill's `ATTRIBUTE_TYPE.DASH` value
  (`绝境龙牙冲刺技能` 65030 = `DASH 120`); the base 龙牙 authors its motion via
  `SetDelaySubSkill(NPCSKILLMOVESJBX.TC_MOVE_LY, NPCSKILLMOVESJBX.TC_YC_LY, 1.0)` where
  `NPCSKILLMOVESJBX = { TC_MOVE_LY = 41570, TC_YC_LY = 12 }` (decoded from `NewSkill.lh`;
  41570 resolves to skill `重攻击`). So the dash is an authored, fast move.
- **Fix:** `SkillCast` now dashes at the authored **speed in engine u/frame**
  (child `DASH` value, 120 u/frame = 1920 u/s at GAME_FPS 16), covering the gap and then
  holding while the anim finishes: `dashMs = clamp(travel / speed, 80 ms, animMs)`.
- **Verified (driven):** log `cast chain 65029: … dash=120u/f ->104ms stop=200`; the
  player covers the gap in ~0.1 s (`t=12s z=33868 → t=14s z=34053`), face + anim + no
  crash. Knob `RC_LY_DASH` (u/frame, default 120).
- Verified: `client\build_client.cmd` exit=0; driven run alive.

### 2026-10-07 — v6 — per-ability dash in the dataset (dash only if authored)

- **Did:** `build_cast_chain.py` now parses each skill's script for its dash — its own
  `ATTRIBUTE_TYPE.DASH`/`DASH(...)`/`DASH_FORWARD(frames,speed)`, or its
  `CAST_SKILL_TARGET_DST` child's dash — and emits a `dash` column (engine u/frame) in
  `cast_chain_f1.{json,tsv}`. `SkillCast` treats `dash<=0` as **no dash** (stays in
  place); the client uses the dataset value (fallback knob `RC_LY_DASH`).
- **Verified (driven):** `65029` dataset `dash=120` (from child `65030` DASH 120) →
  cast `dash=120u/f ->104ms`, player reaches the target; `65076` dataset `dash=0` →
  `dash=0u/f`, no movement (correct: not a dash skill). Both runs alive.
- **Note:** only scripts present in the ability-matcher extraction are parsed, so most
  abilities currently resolve `dash=0` (no dash) — a data-coverage gap, not a wrong
  value; melee dash skills get their authored speed where the script is available.
- Verified: `client\build_client.cmd` exit=0; driven runs `65029`/`65076` alive.

### 2026-10-07 — v6 — widen script extraction (dash coverage 3 -> 31)

- **Did:** `build_cast_chain.py` now extracts its own scripts — every `skills.tab`
  `ScriptFile` via the official `PakV4SfxExtract.exe` (`scripts/skill/<ScriptFile>`) to a
  temp dir, instead of relying on the partial ability-matcher extraction. `--scripts`
  overrides the dir. Dash coverage rose **3 -> 31** abilities (plaintext scripts parse;
  bytecode scripts yield no dash).
- **Verified:** `65029` dash=120, `65076` dash=0 still; 31 abilities with dash>0.
- **Caveat:** base school skills whose scripts ship as Lua **bytecode** still resolve
  `dash=0` (parse cannot read AddAttribute args); a bytecode-aware dash extractor
  (lua51 instruction decode) is the next step for full coverage.
- Verified: builder exit=0; dataset regenerated.

### 2026-10-07 — v6 — bytecode-aware dash extractor (dash coverage 31 -> 95)

- **Did:** the builder now parses **Lua 5.1 bytecode** scripts too (`lua51_dump` reused):
  `_attr_arg` walks the instruction stream for `GETTABLE ATTRIBUTE_TYPE['<DASH*>']` and
  returns the LOADK argument (DASH -> arg0, DASH_FORWARD/BACKWARD/LEFT/RIGHT -> arg1
  speed; `CAST_SKILL_TARGET_DST` -> arg0 child). Dash coverage **31 -> 95** abilities.
- **Verified:** `65029` dash=120; top dash skills sensible (`空穴来风` 360,
  `画影残月（剑纯）` 240, `触石雨冲刺技能`/`疾回马枪冲刺`/`鹤归孤山冲刺` 200).
- **Wart:** DASH(distance) vs DASH_FORWARD(frames,speed) have different semantics but
  are both consumed as "u/frame" by `SkillCast`; correct for 龙牙 (DASH 120); revisit if
  a skill's dash misbehaves.
- Verified: builder exit=0; dataset regenerated; 65029 still 120.

### 2026-10-07 — v6 — dash speed unit CONFIRMED (点/帧)

- **User asked to verify the dash speed before pushing.** Confirmed from the skill
  scripts' own comments: the field is `nDashSpeed`, and its unit is **`点/帧`** (raw
  GBK bytes `\xb5\xe3/\xd6\xa1` = 点/帧 = points per frame = engine units/frame), e.g.
  `nDashSpeed = 100/120` (绝境_老虎波, 绝境_狼牙棒, 轻功通用冲刺). So `DASH(120,0)` =
  **120 u/frame = 1920 u/s at GAME_FPS 16** — exactly what `SkillCast` implements
  (`dashSpeedPerFrame`, `speedUpS = value * GameFps`). **The dash speed is correct.**
- Verified: raw byte decode of the script comment; consistent across 4+ dash scripts.

### 2026-10-07 — v6 — hotkey slots + numbered ability bar (5 more abilities)

- **Did:** `RebornClient` now resolves a **slot list** (`RC_SLOTS`, default
  `65029,65120,65087,65076,65036,65026`); keys `1..N` select a slot and cast it, each
  slot carrying its own anim/effect/dash from the cast-chain dataset. Added
  `client/AbilityBar.cs` — an always-visible top-right numbered panel (per-pixel-alpha
  layered window, `WS_EX_NOACTIVATE`, mirrors `HudOverlay.cs`), showing `key → ability
  name` with the selected slot highlighted. `RC_CAST_AT` grew `ms[:slot]` so one run can
  exercise several slots; `RC_BAR_DUMP=<png>` saves the buffer for fingerprints.
- **Verified:** one driven run (`RC_CAST_AT=34000:1,…,54000:6`) cast all 6 slots; each
  switched `clip=` to its own authored `.tani` (龙牙/渊/净世破魔击/云飞玉皇/龙吟/三环套月,
  dash 120/120/90/0/0/0), returned to idle, no AV, clean shutdown. Bar buffer `168×188`
  `sha256=8fac109136253991` (`proof/netcode/skillv6_bar.png`). Proof:
  `proof/netcode/skillv6_slots_and_bar_20261007.txt`.
- **Note:** no new mechanism — adding an ability stays a data/`RC_SLOTS` change; the bar
  is presentation only.

### 2026-10-07 — v6 — remove legacy FLWS (风来吴山) cast default

- **Did:** casting with **no target** used to fall back to the old skill clip `clipSkill`
  = 风来吴山 (FLWS) + its FLWS sound; the `RC_DEMO` autopilot also fired a skill press at
  18.5 s (same FLWS). Both removed — a no-target cast is now a no-op (the ability system
  requires a target); the FLWS sound is opt-in (`RC_SKILL_SOUND=1`) and off by default.
- **Verified:** driven run `reborn_client_skillv6.exe`: no-target cast → `cast: no target
  - nothing cast` + `cast skipped (no target)` (no FLWS clip/sound); targeted cast →
  `cast chain 65029 … started`; clean `DONE`. Proof:
  `proof/netcode/skillv6_no_flws_20261007.txt`. Gates: jx3_model 10 PASS, gravity PASS,
  loot selftest PASS.
- **Note:** this is P1 of the skill-system plan (see `docs/netcode/JX3_SKILL_CAST_FLOW.md`
  §7 / the plan in the session log): later steps = v5 roster + icons panel, camera max
  default, real prepare/channel/GCD cast state, full-coverage verify.

### 2026-10-08 — v6 — ability roster + panel (Phase A/B) + camera default max

- **Did:** (camera, item 3) default camera range = panel hard max (2000 u), not the
  per-role `custom.dat` `fMaxCameraDistance` (821 u) — `CameraSystem ready … dist=2000u`.
  (Phase A) `ability_picker/tools/build_roster.py` joins v5's `skill_data.json` (154
  abilities: name/ids/matched/castMode/channel/icon) with `cast_chain_f1.tsv` →
  `ability_picker/data/roster_f1.tsv` (header + 154 rows; 130 joined; 5 channel=1).
  (Phase B) `client/AbilityPanel.cs` — our own top-level icon grid (P toggles; click =
  active; key `1` casts the active ability); `selectSlot` now falls back to the roster;
  `RC_ABILITY` can name any roster ability.
- **Lessions (warts):** adding WinForms controls to the render host **before engine
  init hangs the host** → the panel is a separate top-level window created **lazily in
  the frame loop** (after `Init3DEngine`). `Form.BackColor` must be **opaque** (alpha
  throws "Control does not support transparent background colors"). A data TSV the
  client reads with a skip-first-line convention needs a **header row** (the roster
  initially dropped 27844 → 153/154).
- **Verified:** driven `reborn_client_skillv6.exe`: Init3DEngine ok, `ability panel: 154
  entries, 154 icons`, roster ability 27844 听风吹雪 `cast chain 27844 … started`, clean
  `DONE`. Proof: `proof/netcode/skillv6_ability_panel_20261008.txt`,
  `skillv6_camera_max_20261007.txt`. Gates: jx3_model 10 PASS, gravity PASS, loot
  selftest PASS.
- **Remaining (plan):** Phase C = real prepare/channel/GCD cast state (items 4+6);
  Phase D = coverage sweep over all 154.

### 2026-10-08 — v6 — cast time (prepare) from skill scripts (Phase C)

- **Did:** `ability_picker/tools/build_cast_frames.py` parses each skill's Lua script
  (skills.tab `ScriptFile`) for `skill.nPrepareFrames` / `nChannelFrame` /
  `nChannelInterval` (text regex; bytecode via `lua51_dump` SETTABLE) →
  `cast_frames_f1.tsv` (41141 skills; 157 with nPrepareFrames, 121 channel), joined into
  `roster_f1.tsv`. `SkillCast` now takes `prepareMs`: `commitMs = max(prepare, effectFrame)`,
  `totalMs = max(animMs, commitMs)`; the clip holds for `totalMs`. `selectSlot` prefers the
  roster row (it carries prepareFrames) over the cast-chain row.
- **Verified:** 65076 绝境_云飞玉皇 (script `nPrepareFrames = 24`) → `prepareMs=1500`,
  `commitMs=1500` (prepared, not instant); 65029 绝境_龙牙 → `prepareMs=0`, `commitMs=520`
  (instant). Clean `DONE`. Proof `proof/netcode/skillv6_cast_frames_20261008.txt`.
- **Open:** runtime channel ticks (`nChannelFrame`/`nChannelInterval`) not modelled yet
  (prepare only); `nChannelInterval` expressions using `HDJueJingSkillCoe` resolve to -1
  (the coefficient is a documented global, not yet folded in); GCD (cooldown row 16) still
  to come.

### 2026-10-08 — v6 — coverage sweep (Phase D) — cast-accumulation AV

- **Did:** `RC_SWEEP=<ms>` (+ `RC_SWEEP_N`, `RC_SKIP_IDS`) selects+casts each roster ability
  in turn — the coverage harness. SelectSlot-prefer-roster keeps each ability's own
  anim/prepare. (Also fixed `selectSlot` to prefer the roster row, which carries
  `prepareFrames`.)
- **Finding (BLOCKER):** the engine **AVs after ~5–7 rapid sequential casts**
  (`KGEngineCLR.Render()` / `FrameMove()`), independent of the specific ability (run1 died
  on 27895, run2 on 27863). Same class as v5's "rapid re-casts AV the engine tag manager"
  (handoff item 4/6). The sweep harness is correct; the **guard/warm-up** (per-cast
  cooldown + sfx warm-up + per-tani blacklist, as in v5 `AbilitySystem`) is not yet ported
  to v6 → Phase D full-coverage is blocked on it.
- **Verified:** sweep selects+casts each ability (run2 cast 7 in a row before the AV);
  proof `proof/netcode/skillv6_sweep_finding_20261008.txt`. Gates: jx3_model 10 PASS,
  gravity PASS, loot selftest PASS.
- **Next:** port v5's cast guard/cooldown + sfx warm-up into v6, then re-run the full 154
  sweep to build the coverage report and blacklist any AVing tanis.