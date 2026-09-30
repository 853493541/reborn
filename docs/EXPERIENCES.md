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

### 2026-09-29 — controls — WW sandbox: 3 double-tap-W cases (feature/ww-sandbox)
- Did: new worktree `reborn-iso-ww-sandbox` (feature/ww-sandbox off main). Added
  `client/WwRules.cs` (pure rules + selftest) and wired it into RebornClient: double-tap W
  on ground -> sprint (existing 8.8 尺/s); double-tap W in air with the school weapon ->
  急坠 plunge (`SetPassiveVelocityZ(-2000)` u/f = -30000 u/s, engine Z clamp -2048..2047 u/f);
  wrong/no weapon -> no plunge (generic only). Env `RC_WW_WEAPON=right|wrong`. Feature build
  `reborn_client_ww_sandbox.exe` (own memory namespace, no shared bin64 writes).
- Evidence: `reborn_client_ww_sandbox.exe --ww-selftest` -> bin64\reborn_out\ww_sandbox_selftest.txt
  `RESULT PASS failures=0` (6 checks); build exit=0. Rule source: extracted 天策轻功急坠.lua
  `SetPassiveVelocityZ(-2000)` + skill 37891 mask 0xFFFFFFFF (agent/daqinggong commit e0c7be3).
- Outcome: sandbox ready. MED: the air double-tap -> 急坠 trigger mapping is inferred from the
  any-state WW entry skill + the plunge script; re-open if a live client comparison differs.

### 2026-09-29 — controls — WW = 纵跃段 chain (correction; feature/ww-sandbox)
- Did: replaced the air-WW plunge with the client-shipped 轻功 chain. Air WW now advances
  `JumpParam.tab` school 0 stages J1 300/20 -> J2 400/20 -> J3 -250/8 (MaxJumpCount 4;
  Vz u/frame, G u/frame^2), playing `f1b02yd二段跳a.tani` for the 纵跃段 and the
  ChongCiQingGong dive clip for the downward J3. Ground WW stays sprint; wrong weapon ->
  no leap. The term is real: `Z_纵跃UI.pss` / `Z_纵跃.dds` in the editor resource index.
- Evidence: selftest 13/13 PASS (chain triples + apex 11.7/20.8 m); build exit=0; both
  clips verified present via `PakV4SfxExtract.exe`. The chain data is client-shipped
  (`proof/gravity/JumpParam.tab`), so the movement is client-predicted — no server
  dependency; the earlier "server-side setter" note applies only to the separate 急坠
  skill script, not to the WW chain.
- Outcome: supersedes the plunge mapping above (kept as history). The 急坠
  `SetPassiveVelocityZ(-2000)` skill remains a separate school dive, not the WW action.

### 2026-09-29 — client — WW leap crash: engine AV from the 二段跳 clip + gravity sign
- Did: user WW crashed the sandbox. Reproduced deterministically: `RC_DEMO` with
  `RC_CLIP_JUMP=data\source\player\f1\动作\f1b02yd二段跳a.tani` AVs `KGEngineCLR.Render`
  (`System.AccessViolationException`, faulting ntdll 0xc0000005) on the first frame after
  the clip loads — the clip itself is the trigger, independent of the leap. Switched the
  chain to the per-jump `.ani` clips (`f1b02yd小跳b/c.ani`, REBORN_JUMP_FALL_SPEC §6) and
  the proven ChongCiQingGong dive `.tani` for the downward J3. Also fixed the stage gravity
  sign (was +4500, launching the character to Y=112k at the Z clamp) and added apex ->
  End-gravity (11) revert. Added `RC_WW_DEMO=1` (auto jump+leap at 6s/8s) as the
  deterministic live repro.
- Evidence: crash .NET Runtime event 21:21:40 (AV in `MovieEngineCLR.KGEngineCLR.Render`);
  isolated repro 21:24:19 with `RC_DEMO`; fix verified 21:31 run — leap vy=4500, g=-4500,
  apex ~23 m above ground, lands t=12s, no crash, app exits via `RC_AUTORUN`.
- Outcome: solved. Do not play `f1b02yd二段跳a.tani` standalone (engine bug); it is not the
  chain leap animation in the sandbox.

### 2026-09-29 — client — WW trigger on key 1 + authored chain XY dash
- Did: bound the WW action to key `1` (double-tap W kept; skill moved to `2`). Applied the
  authored `JumpParam` `JumpSpeedXY` per stage along the character facing: J1 30 u/f
  (450 u/s, forward-up), J2 50 (750 u/s, forward-up), J3 100 (1500 u/s, forward-down far
  dash). `RC_WW_DEMO` shows the leap now carries the character forward (~1430 u in the
  stage-1 demo) and lands cleanly.
- Evidence: selftest 15/15 PASS (incl. `CHAIN_J1_XY=30`, `CHAIN_J2_XY=50`, `CHAIN_J3_XY=100`);
  demo run 21:42 — Z 24224 -> 25149 -> 25658, landed t=12s, no crash.
- Outcome: solved. Stage direction per the table: stages 1-2 forward-up, stage 3
  forward-down (the far dash).

### 2026-09-29 — client — Chinese stage names for the WW chain
- Did: resolved the official stage vocabulary from the shipped tables/UI:
  J0 ground jump = 跳跃 (小跳a/b/c); J1/J2 ascent = 纵跃 (一段/二段) —
  `Z_纵跃UI.pss` UI effect, buff 16516 "上升过程中免控", buff 13836
  "空中一段后撤换二段表现"; J3 downward = 急坠/俯冲 — per-school skills named
  `<style>·坠` (游龙步·坠 20234, 逍遥游·坠 20273, 百转千回·坠 20570, …), buffs
  13730/13761 "轻功急坠换动作", 13889 "通用急坠播表现Buff", per-school animation
  names 俯冲 (苍云俯冲/纯阳俯冲/…); glide = 滑翔 (蓬莱加强滑翔段, 丐帮一段冲a滑翔,
  鸟翔碧空三段 "滑翔至目标点"); summit = 登顶 (`STR_SPRINTPOWERINTOP`); the state
  itself = 新轻功状态 (`STR_SPRINT_POWER` tooltip: 双击W进入/按住保持/松开结束,
  空格做出各式动作) and 萍踪侠影 in ban buffs.
- Evidence: `settings/skill/Buff.tab` names, `ui/Scheme/Case/Skill.txt` names,
  editor resource index (Z_纵跃UI.pss), repo `string.txt` (STR_SPRINT*).
- Outcome: done (research only).

### 2026-09-29 — controls — WW = 纵跃段 + authored End glide (forward-down dash)
- Did: found the client-side sprint system: `ui\script\sprintbase.lua` +
  `hotkeys.lua` `ResponseDisplacementHotkey` (proto 44) dispatcher, driven by the UI
  tables `\UI\Scheme\Case\Sprint\Condition.tab` / `Action.tab` (extracted to
  `proof/controls/sprint/out`, registry in `ui\script\common\table_defs.lua`).
  Official key map (Action.tab): 双击W = 【WW上冲】, 双击S = 【SS下冲】, w = 【w纵跃】,
  Space = 一段..八段/急坠, Shift = 滑翔/高跃/急降, 按住W = 快W滑翔/上爬. Condition.tab:
  纵跃段 = JumpCount 1 "前跃第一段跳"; stages 一段/二段/三段鹰/四段斜降/六段俯冲; 双人冲刺/俯冲;
  凌霄登顶; 纵马疾驰·纵跃段.
- Motion: fresh `settings/JumpParam.tab` school 4 (万花, the F1 actor's school):
  J1 50/160/8, J2 70/240/7, J3 100/700/36, J4/J5 100/-250/8; shared **End triple
  xy 125 vz -140 g 12** = the forward-down glide (1875 u/s fwd, 2100 u/s down).
  Sandbox: air WW -> 纵跃段 takeoff, at apex -> End glide phase (dive clip).
- Evidence: demo run 22:05 — leap 2400 u/s up, End 1875 fwd / -2100 down, lands t=12s;
  selftest 17/17 PASS; tables committed under `proof/controls/sprint/`.
- Outcome: solved. The "downward+forward dash" is the authored End/glide phase after the
  纵跃段 takeoff (not an upward-only leap).

### 2026-09-29 — controls — WW air = 纵跃段 charge (every class, release-independent)
- Did: per the user's spec, air WW now applies the **charge directly** (no takeoff phase):
  the shared End triple `xy 125 u/f, vz -140 u/f, g 12` = 1875 u/s forward + 2100 u/s down,
  for every class (the triple is identical across schools in the fresh `JumpParam.tab`).
  The W release does not change it (the charge persists to the ground). Wrong weapon -> no
  charge. Ground WW stays sprint. `RC_WW_DEMO` reproduces it.
- Evidence: demo run 22:14 — `ww AIR: 纵跃段 charge vxy=125 vz=-140 g=12 -> 1875 u/s fwd,
  -2100 u/s down`, lands cleanly; selftest PASS; commit 40b51c9.
- Outcome: solved.
