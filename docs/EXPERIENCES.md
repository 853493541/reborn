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

### 2026-09-29 — controls — WW air charge = player tech (state ends on release, velocity kept)
- Did: remodelled per the user's clarification: air WW applies the fly velocity (authored
  End triple 1875 u/s fwd + 2100 u/s down) as a **state**; releasing W ends the state but
  keeps the velocity, so the character keeps falling forward+down fast (emergent player
  tech, not an authored move). No special animation — normal air clips only. `RC_WW_DEMO`
  now also simulates the release.
- Evidence: demo run 22:36 — charge at 8.0s, `W release -> state ended, velocity kept
  (vxy=1875 u/s, vy=-3431 u/s)` at 8.5s, lands t=10s; selftest PASS; commit fb073f8.
- Outcome: solved. The velocity values remain the authored End triple; the release/keep
  combination is the player-discovered part.

### 2026-09-29 — controls — forward number found: replay the shipped flight curve
- Did: user said the forward (125 u/f = 1875 u/s) was too low. Found the real numbers in
  the shipped `settings/JumpFrameParam.tab` flight curves (the only ones shipped: schools
  10/11): per-frame VelocityXY ramps **150 -> 205 -> 273 u/f (2250 -> 3075 -> 4095 u/s)**
  and the dive reaches **Z -949 u/f (-14235 u/s)**. The End triple (125/-140) is only the
  hover/end velocity, not the flight forward. Sandbox now **replays the school-10 jump-1
  curve** (81 frames at 15 Hz) on air WW; releasing W stops the curve and keeps the current
  velocity. Selftest asserts frames=81, XY peak=205, dive=-949.
- Evidence: selftest `FLY_CURVE_PEAK_XY: PASS (205)`, `FLY_CURVE_DIVE_Z: PASS (-949)`;
  demo run 22:42 — curve start (150/150), dive reached vy=-11565 u/s at 0.5 s; commit 6654363.
- Outcome: solved. The curve is the authored forward/down profile; forward is 1.6-2.2x the
  old 125 u/f.

### 2026-09-29 — controls — WW charge forward-dominant (no instant drop)
- Did: user felt the curve replay dropped too fast ("forward more not down more"). Switched
  the air WW to a forward-dominant charge: forward = the curve's XY peak **205 u/f
  (3075 u/s)**; while the state is held the vertical is held (level); releasing W ends the
  state and gravity builds the down (fast forward + gradual fall). Tunable with
  `RC_WW_FWD` / `RC_WW_DOWN` (u/f). No special animation.
- Evidence: demo run 22:53 (RC_JUMP_MULT=50) — `charge fwd=205 u/f (3075 u/s) down=0`,
  release keeps vxy=3075, then vy builds -1007 -> -5967 while Z advances 24224 -> 36558;
  selftest `CHARGE_FORWARD: PASS (205)`; commit 5086599.
- Outcome: solved.

### 2026-09-29 — controls — key 1 = WW release (independent of the physical W key)
- Did: rebound key `1` from the WW trigger to the **W-release action**, usable while W is
  still physically held: it ends the WW state (velocity kept) or turns the sprint off.
  Double-tap W remains the WW trigger. Charge forward default lowered to the curve entry
  150 u/f (2250 u/s, RC_WW_FWD); charge clip plays once (playType 1, RC_GLIDE_PLAY).
  HUD updated.
- Evidence: build exit=0; selftest PASS; commit 5b8566b; app relaunched pid 27296.
- Outcome: solved.

### 2026-09-29 — controls — WW charge was dying on the double-tap's own keyup
- Did: user reported key 1 "doesn't work". Log showed the charge ending ~50 ms after the
  trigger (`charge 24.860 -> W released 24.911`) — the double-tap's **own second W keyup**
  was ending the state, so key 1 always found "nothing active". Fix: ignore a W keyup
  within 250 ms of the charge trigger (that release belongs to the double-tap); the state
  now persists until key 1 or a deliberate later W release.
- Evidence: log 23:45 (charge at 24.860 ended by W release at 24.911; key-1 presses logged
  `nothing active`); fix build exit=0, selftest PASS, commit 10c8a7d; relaunched pid 33124.
- Outcome: solved.

### 2026-09-30 — controls — fall terminal cap from Sprint.tab (real fall number)
- Did: the fall had no terminal cap (accelerated to the engine hard clamp 2048 u/f = 30720
  u/s). Added the shipped dive/fall cap: `settings/Sprint.tab` school 4 `MaxVelocityZ` =
  **900 u/f (13500 u/s, ~70 m/s at 192 u/m)**. Gravity stays the table's 11 u/f2 (2475
  u/s2); chain End gravity is 12 u/f2 (2700 u/s2). Selftest asserts FALL_CAP = -900.
- Evidence: build exit=0; selftest PASS; commit 9e012f8; app relaunched pid 37500.
- Outcome: solved. Real fall numbers: gravity per school/stage in `JumpParam.tab` G columns
  (clamped [0,31] u/f2), terminal in `Sprint.tab` MaxVelocityZ (900/1000 u/f), authored
  profile in `JumpFrameParam.tab` Z (dive -949 u/f).
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


### 2026-09-30 — movement/controls — 轻功 full analysis + sandbox port
- Did: merged main into feature/ww-sandbox (camera merges + mini-sandbox) and ported the
  WW changes onto the new client; built the sandbox client 
eborn_client_mini.exe and
  ran it on the cropped map (RC_MAP=...龙门寻宝_s.jsonmap; LoadMap 141 ms, 1x1 region).
  Wrote docs/movement/JX3_QINGGONG_BEHAVIOR.md: full client-side 轻功 analysis (terms,
  official key map, stage table, movement law, fall/turn, costs, WW tech, open items).
- Evidence: merge commit 5f257ea; mini run log 13:21 (
s=reborn_client_mini.memory,
  LoadMap result=0 ms=141, TerrainSampler 1x1); doc registered in docs/movement/README.md.
- Outcome: sandbox is now the iteration target; analysis doc added. Fall+turn answer: the
  horizontal velocity is speed+heading, steered toward the facing each frame
  (ProcessAcceleration), so turning curves the fall while gravity keeps the fall rate.
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


### 2026-09-30 — client — sandbox + 轻功 version: newest main merged, WW fully ported
- Did: merged the newest main (765447f: double-jump 二段跳/flip + calibrated jump
  RC_JUMP_SCALE 0.52 + camera-relative steering + mini-sandbox) into feature/ww-sandbox
  and integrated the 轻功 changes into the new client: WW state hold guards the per-jump
  gravity (if (!wwStateActive) vy -= curJumpGravity*dt), the Vz clamps + Sprint.tab fall
  cap (900 u/f) apply after it, landing resets the WW state, the charge clip + HUD CHARGE
  coexist with the 二段跳 clip/state display. build_client.cmd compiles both JumpTable.cs
  and WwRules.cs. Built the sandbox client 
eborn_client_mini.exe and ran it on the
  cropped map; the WW demo was retimed for the calibrated airtime.
- Evidence: merge 5bfb956 (5 conflict hunks resolved, 0 markers), demo run 14:32 on the
  1x1 map (LoadMap 141 ms): jump 8.0s -> charge 8.4s (fwd 150 u/f = 2250 u/s) -> release
  10.2s (velocity kept); selftest PASS; commits 819951e; app relaunched pid 34116.
- Outcome: solved. This branch is now the sandbox + 轻功 version (run via
  	ools\sandbox\run_sandbox.cmd / 
eborn_client_mini.exe + RC_MAP cropped map).


### 2026-09-30 — client — sandbox client renamed for the 轻功 feature
- Did: the branch's sandbox client was the generic 
eborn_client_mini.exe; renamed the
  feature build to 
eborn_client_qinggong.exe (window title derives to
  sandbox-qinggong, AGENTS 2.7) and pointed 	ools/sandbox/run_sandbox.cmd at it.
- Evidence: build exit=0; selftest PASS; run log 14:44 (
s=reborn_client_qinggong.memory,
  cropped map); commit 41e23a9.
- Outcome: solved.


### 2026-09-30 — client — sandbox super jump + 大轻功 client name
- Did: re-applied the super jump to the sandbox client: RC_JUMP_SCALE default 5.2
  (10x the calibrated height, apex ~19 m; 0.52 restores the calibrated 1.92 m). Renamed
  the feature client to 
eborn_client_daqinggong.exe with window title 大轻功
  (RC_TITLE=大轻功 in 	ools/sandbox/run_sandbox.cmd).
- Evidence: build exit=0; selftest PASS; run log 15:02 (
s=reborn_client_daqinggong.memory,
  cropped map, LoadMap 172 ms); commit 5174d3f; app pid 1284.
- Outcome: solved.


### 2026-09-30 — client — WW state now ends when W is not held (falls after release)
- Did: user reported the charge never fell after releasing W. Cause: the double-tap keyup
  was ignored by the 250 ms guard and, with no later W press/release, nothing ended the
  state. Fix: after the grace, if W is not held (!pW) the state ends (velocity kept) and
  gravity takes over; key 1 still releases while holding. Demo now holds W during the charge.
- Evidence: demo run 15:32 on the cropped map — charge 8.4 s level (vy=0) -> release 10.2 s
  (velocity kept 2250 u/s) -> grounded by 12 s; selftest PASS; commit ec8376d; pid 30748.
- Outcome: solved.


### 2026-09-30 — client — WW release = 45-degree forward-down dash
- Did: per the user's spec, the WW release (key 1, W keyup, or W not held) now converts
  the kept forward speed into a forward-down dash at a configurable angle, default 45 deg:
  y = -vxy * tan(angle) (2250/2250 u/s at 45). All release paths share wwEndState.
  Hold behavior is incidental; the release is the dash. RC_WW_DASH_ANGLE overrides.
- Evidence: demo run 15:49 on the cropped map — ww release: forward-down dash 45 deg ->
  vxy=2250 u/s, vy=-2250 u/s, grounded by 12 s; selftest PASS; commit 59b7ac6; pid 10636.
- Outcome: solved.


### 2026-09-30 — client — WW release dash fires on any W release
- Did: user saw no change because the double-tap grace delayed/ignored their quick release.
  Removed the grace and the not-held auto-end: any W keyup while the WW state is active now
  triggers the 45-degree forward-down dash immediately; key 1 remains the explicit release.
- Evidence: build exit=0; selftest PASS; commit a99bc87; relaunched pid 13028 (title 大轻功,
  cropped map). Note: only 
eborn_client_daqinggong carries these changes — other
  workstream clients (cam-wwdrag/collision) do not.
- Outcome: solved.


### 2026-09-30 — client — WW release dash holds the 45-degree line
- Did: the release dash was being buried by the super-jump-scaled gravity (5.2x), so an
  instant release read as a straight drop. Added wwDashActive: during the release dash
  gravity is suspended and the velocity holds xy + y = -vxy*tan(45) to the ground
  (HUD state DASH). Landing clears it.
- Evidence: demo run 16:21 — release xy=2250, vy=-2250 (held to landing), landed at
  t=12s with forward/down ≈ 1700/1440 units (≈45 deg); selftest PASS; commit 2c6af05;
  relaunched pid 20332.
- Outcome: solved.


### 2026-09-30 — controls — ground hold-W research: 疾跑段 (通用疾速跑 buff 12085)
- Did: resolved what holding W does on the ground from Sprint\Condition.tab:
  every school has <MOVEFORWARD;1> rows (JumpCount 0, Jumping 0, BuffID 12085,
  OTAction 12085;150) = <school>·疾跑段 / 萍踪侠影·疾跑段 — the 轻功 accelerated run,
  buff 12085 通用疾速跑, ground + water-surface (RunOnWater 0/1) variants. Velocity
  range = Sprint.tab (MaxXY 120 u/f = 1800 u/s = 28 尺/s, newer schools 127); sprint
  animation AniFrame 40–105; SprintCamera pull-back. Release W -> HoldW=0 +
  CheckEndSprint. Double-tap W is 【WW上冲】, not the sprint (controls doc note superseded).
  Doc: docs/movement/JX3_QINGGONG_BEHAVIOR.md §2.1.
- Evidence: Condition.tab rows 17–22 / 47–54 / 135–142 / 223–230 / 311–318…; Buff.tab
  12085 = 通用疾速跑; number.krl walk 6 / run 20 尺/s; Sprint.tab caps.
- Outcome: research done; sandbox still runs plain run on hold-W (not modelled).


### 2026-09-30 — controls — 疾跑段 speed numbers
- Did: resolved the hold-W 疾跑段 speed. Sprint state buffs: 12085 通用疾速跑 +
  12190 通用疾跑按住 (removed by the 12085 script on exit); staged speed-percent buffs
  疾速第零~三段 (atMoveSpeedPercent 256=+25%, 768=+75%) while InSprint. Velocity cap =
  Sprint.tab MaxVelocityXY 120 u/f (0-16) / 127 u/f (17+): 1800 u/s = 28.1 尺/s ~ 9.4 m/s
  at 15 Hz (1920 u/s at the 16 fps convention); normal run 20 u/f = 320 u/s (5 尺/s).
  Doc: JX3_QINGGONG_BEHAVIOR.md 2.1.
- Evidence: Buff.tab 12085/12190/11338-11343; Sprint.tab; number.krl; commit next.
- Outcome: research done.


### 2026-09-30 — client — 疾跑段 implemented in the sandbox
- Did: ground hold-W now enters the 疾跑段: jipaoActive (grounded + W held + moving,
  not walk/Shift) ramps 25% -> 75% -> 100% of the Sprint.tab cap (school 4 MaxVelocityXY
  120 u/f = 1800 u/s) on 16-frame stages, with the 12085 通用疾速跑 / 12190 通用疾跑按住
  log semantics; release -> CheckEndSprint (speed back to run). HUD/log state JIPAO.
  Selftest asserts JIPAO_CAP = 1800.
- Evidence: demo run 16:56 on the cropped map — jipao: enter -> spd 450 -> 1350 -> 1800
  u/s (JIPAO) -> jipao: end; positions advance ~3540 u per 2 s at the cap; selftest PASS;
  commit 308ef8d; relaunched pid 19296.
- Outcome: solved.


### 2026-09-30 — client — 万花大轻功「点墨江山」 implemented in the sandbox
- Did: full school-4 qinggong flow from the extracted scripts/tables:
  20628 trigger (CanCast 气力值 >= 10000, cost 100*CONSUME_BASE -> 2500 with the
  CONSUME_BASE=25 hypothesis, SetTimer(30) -> BirdFlyTo + LockBirdMoveZ Z-locked
  fly, buffs 13422/14626/13836), Space stages JC1..JC5 = 纵跃段/一段/二段/三段/四段
  using JumpParam rows J1..J5 with the segment-end End triple 125/-140/12, 20630
  急坠 = SetPassiveVelocityZ(-2000) (bypasses the 900 u/f fall cap), 气力值 drains
  (float 35/s, bird move 206/s, stage 300) + ground regen, HUD state WH-CAST/WH-FLY/
  WH-<stage>/WH-DIVE + 气力值, help text, RC_WH_DEMO timeline, RC_WH_COST/DELAY_MS/
  REGEN envs. Selftest asserts the trigger cost, plunge frame, timer and stage count.
- Evidence: RC_WH_DEMO=1 run 17:13 — 20628 cast (power 7500) -> BirdFlyTo fly ->
  纵跃段 J1 50/160/8 -> End 125/-140/12 -> 一段 J2 -> 二段 J3 100/700/36 (y 8400+)
  -> 三段/四段 J4/J5 100/-250/8 -> 急坠 vy=-30000 -> landed (dive=True); costs
  7500->7096->6445->5610->4980->4474. Selftest WH_* PASS.
- Open: 弈韵 JC6..JC11 branch, 双人/踩人 rows, per-stage animations, CONSUME_BASE
  exact value, regen D0, SetTimer unit (docs/movement/JX3_QINGGONG_BEHAVIOR.md §7.1).
- Outcome: solved for the base chain.


### 2026-09-30 — client — 万花 animations wired (real F1 clips, engine-AV avoidance)
- Did: replaced the 苍云俯冲a charge clip with the real 万花 (school 4) 大轻功
  animations: hover/fly = F1bqg万花加强滞空_01 (player_suspend.krl.txt
  ZhiKongQingGong:2 【万花】), stages = 加强一段跳b / 加强二段跳a / 加强三段跳a /
  加强二段跳b / 加强俯冲b (Tani.rt 万花 series), 急坠 = 加强俯冲b. Clip envs
  RC_CLIP_WH_FLY / WH_S1..S5 / WH_PLUNGE; RC_CLIP_GLIDE retired. Demo times
  RC_WH_DEMO_S5_MS / RC_WH_DEMO_PLUNGE_MS for long-window tests.
- Lesson (engine AV, 0xC0000005): F1bqg万花四段跳a_空.tani and F1bqg万花俯冲a01.tani
  crash KGEngineCLR when played airborne, but are fine as idle clips — same class
  as the documented f1b02yd二段跳a.tani AV. Isolated by bisecting the clip set
  (exit codes D/E/F), then verified the substitutes over 5 s airborne windows.
- Evidence: demo runs 17:36 (exit=0, DONE) — hover 加强滞空 -> JC1 一段跳b -> JC2
  二段跳a -> JC3 三段跳a -> JC4 二段跳b -> JC5 俯冲b -> 急坠 俯冲b -> landed;
  clip -> lines all load with no setClip ex. Selftest PASS.
- Outcome: solved; 万花 clips visible in the 大轻功 window.

### 2026-09-30 — research — release-W (登顶) exit path traced from the client
- Question: what happens when W is released / the 大轻功 exits?
- Answer (client evidence): W-up -> action 5 【松开W登顶】 (Sprint/Action.tab,
  `<MOVEFORWARD;1>`). Near a summit point (doodad_summit.krl.txt cone 45 deg,
  horizontal <= 6000, vertical <= 5600; SummitDistance=20000, SummitFadeTime=2000,
  SummitAdjustY=30) the stage rows' 登顶 variants (CanTowerFlag=1) enter the
  summit: the player_summit.txt 踩点 A/B/C performance (~1846 ms; 万花 =
  `*bqg万花踩尖_上a01/b01.tani`) then KRLSummit::Stand ("当前处在轻功登顶状态").
  Otherwise buff 13422 (全门派战斗轻功状态监控) ends and its ScriptFile
  `skill/轻功/轻功状态结束处理.lua` OnRemove runs: `StopBirdFly()` +
  `UnlockBirdMoveZ()` + aircombat camera off -> horizontal speed persists,
  gravity resumes (fall cap 900 u/f), 气力值 drain stops.
- Automatic ends (`气力值持续消耗.lua` Apply): `nSprintPower == 0`, or altitude
  `Flyheight < 6*8*64 = 3072 u` -> StopBirdFly + UnlockBirdMoveZ.
- Evidence: extracted scripts under `proof/controls/sprint/out/scripts/skill/轻功/`
  (轻功状态结束处理.lua, 气力值持续消耗.lua), Buff.tab rows 13422/13836/13889/
  13730/13761, player_summit.txt, doodad_summit.krl.txt, number.krl.txt; doc
  `docs/movement/JX3_QINGGONG_BEHAVIOR.md` §7.2; commit e59129d.
- Sandbox gap noted: 45 deg dash stand-in vs the real StopBirdFly glide; no
  altitude auto-end / summit state yet.
- Outcome: answered; doc + experience recorded.

### 2026-09-30 — client — release exit now the real StopBirdFly semantics (45° dash removed)
- Did: `wwEndState` rewritten to the traced client exit (buff 13422 →
  `skill/轻功/轻功状态结束处理.lua`): StopBirdFly + UnlockBirdMoveZ — the fly ends,
  the horizontal speed (`leapSpeedXY`) persists as the forward glide, gravity
  integrates Vz with the row-0 base gravity (`WwRules.FallGravityPerSecond2` =
  2475 u/s²), the 900 u/f fall cap applies, the 气力值 drain stops. The 45° dash
  (`wwDashActive` / `RC_WW_DASH_ANGLE`) removed; `pGravity`/`curJumpGravity`
  moved before the exit delegate so it can set the fall gravity; the HUD DASH
  state removed; `RC_WH_DEMO_RELEASE_MS` added for the exit test; WwRules
  `FALL_GRAVITY` selftest added.
- Evidence: release run 18:31 exit=0 — release during JC3: `wh chain end (登顶):
  stage 3` → `wh StopBirdFly + UnlockBirdMoveZ: vxy=1500 u/s persists, vy=4846
  u/s; fall gravity 2475 u/s2` → the ballistic rise decayed (vy 4846 → 941 over
  1.5 s) with the forward motion continuing; standard chain run 18:32 exit=0
  DONE; selftest PASS; commit c6cbe28.
- Open: the summit state (no summit points in the sandbox map) and the
  altitude < 3072 u auto-end (`GetAltitude` units unverified).
- Outcome: solved; the sandbox release matches the client truth.

### 2026-09-30 — client — ground WW recast (WW上冲), 段数 HUD, power feedback
- Did: the double-tap W now casts the 大轻功 on the GROUND too — per the client
  truth (`JX3_DAQINGGONG_RESEARCH.md` scenario table: "Ground, school weapon |
  WW flag -> ... school trigger ... this is the school 大轻功"); the ground
  sprint stays HOLD W (疾跑段). `WwRules.Evaluate` no longer returns Sprint
  (the wSprint/pSprint stale double-tap-sprint path removed; the sprint camera
  now follows the 疾跑段/fly). Ground cast = the WW上冲 takeoff leap (the J1
  triple) then LockBirdMoveZ hovers at the apex (`whLaunch`); stage presses take
  over the launch; `grounded` is cleared at the fly start (bug found by the
  recast run: the takeoff never left the ground). Top-right overlay: 段数 N/5 +
  stage name while flying, 气力值/气力不足 (2 s red) while it refills/rejects.
  Ground regen default 4000/s (RC_WH_REGEN; D0 open, provisional).
  RC_WH_DEMO_NOJUMP / RC_WH_DEMO_RECAST_MS demo hooks.
- Evidence: ground-cast run 19:18 exit=0 — `wh WW上冲 takeoff (ground): J1 leap
  vy=2400` -> rise -> apex -> hover; recast run 19:20 exit=0 — release at JC3 ->
  land -> recast at 21.5 s accepted (power 7500) -> ground takeoff -> hover;
  standard chain run 19:19 exit=0 DONE; selftest GROUND_WW_CHARGE/AIR_WW_CHARGE
  PASS. The recast at 22.0 s first failed at 9806/10000 (regen 2000/s) — the
  4000/s default fixes the "only once" feel.
- Outcome: solved; land -> double-tap W casts again.

### 2026-09-30 — client — WW launch re-derived from SkillMove 336 (起跳 was wrong)
- Lesson: the earlier WW model was wrong twice. (1) The ground double-tap is
  【WW上冲】 = the school trigger (all school triggers: CanCast `nSprintPower >=
  10000`, cost 100*CONSUME_BASE, then `BirdFlyTo + LockBirdMoveZ`) — the ground
  sprint is HOLD W. (2) The activation launch is the trigger's `SkillMove 336`
  (settings/SkillMove.tab): IgnoreGravity=1, TotalFrame=31, XY=0, VelocityZ
  0/471/537/574/590/589/578/.../8 u/frame (a ~2 s vertical rise; SetTimer(30) ~=
  the launch). It is NOT the 起跳/纵跃段 (that is JC1, entered by Space), and the
  HUD must not name it 起跳.
- Did: the cast starts the 336 launch (`whLaunch`, frame-stepped at 15 Hz);
  stage presses are ignored during it (SkillMove CanJump=0); at frame 31
  BirdFlyTo + LockBirdMoveZ starts the Z-locked fly. HUD: 上冲/段名/急坠/滑翔 +
  段数 N/5 + 气力值; rejection shows 气力未满 需 10000 (the gate is the bar at
  10000, not "enough for the cost"). Altitude auto-end (< 3072 u) wired. Fixed a
  re-grounding bug: the landing check and the collision resolve re-grounded the
  character during the launch (skip both while whLaunch).
- Evidence: launch run 20:28 exit=0 — cast -> y 1099 -> 11699 (launch) ->
  `SetTimer(30) -> BirdFlyTo + LockBirdMoveZ` -> JC1..JC5 -> 急坠 ->
  `altitude 3014 < 3072 -> StopBirdFly` -> land; ground recast run 20:32 exit=0
  — recast -> y 890 -> 5095 (vy 8040) -> fly at 10479 -> hover. Selftest
  WH_LAUNCH_FRAMES/PEAK/MIN_ALTITUDE PASS.
- Outcome: solved; the process now matches the extracted scripts/tables.

### 2026-09-30 - research - full-system audit (大轻功 generators)
- User feedback: the sandbox does not match the live game; maybe a different
  大轻功 generator. Audit result: the system is a composition -
  (1) WW entry 37891 (双w进入轻功技能.lua, compiled; mount checks + dispatch),
  (2) the school trigger 20628 (gate 10000, cost 100*CONSUME_BASE, SkillMove 336
  launch, SetTimer(30) -> BirdFlyTo + LockBirdMoveZ, buffs 13422/14626/13836),
  (3) the fly monitor 13422, (4) the solo stage chain JC1..JC5, (5) the AIR DASH
  20788 通用空中冲刺 -> 20789: DashToPitchDirection(480, face, 160) + buff 14561
  (免控) + 14129 (换二段; 通用持续冲刺结束.lua -> Stop() + camera + SFX; atDriftFlag),
  (6) 急坠 20630, (7) the 弈韵 branch JC6..JC11 via 踩人/双人 (SpecialSprint 6..11),
  (8) 双人 rows, (9) 登顶 summit.
- School mapping CLOSED: JumpParam.WeaponMask = 1<<(WeaponRequest-1); 万花 weapon 6
  -> mask 32 -> JumpParam school 4 (our chain data is right); the curve schools
  (JumpFrameParam) are 10/11 = 丐帮/苍云, NOT 万花 - the double-jump doc label fixed.
- Sandbox consequence: missing the air dash (the live fast flight move) and the
  弈韵/双人/登顶 branches; the tuned fly-forward 150 u/f was borrowed from the
  丐帮 curve entry.
- Evidence: scripts under proof/controls/sprint/out/scripts/skill/ (双w进入轻功技能,
  江湖轻功_空中持续冲刺, 通用持续冲刺结束), Buff.tab 14129/14561/14626, skills.tab
  trigger WeaponRequest, JumpParam/SpecialSprint tables; doc §7.3.
- Outcome: audit recorded; the air dash + branches are the next build items.

### 2026-09-30 — client — 空中冲刺 dash implemented (20788/20789)
- Did: the fly double-tap W/S/A/D = 上冲/下冲/右冲/左冲 — the 空中冲刺 dash per
  skill 20788 通用空中冲刺: `DashToPitchDirection(480, face, 160)` (the dash
  follows the camera pitch; the horizontal from the camera forward/strafe),
  480 u/f = 7200 u/s for `WhDashFrames` 100 (nDashFrame level 1), the dash cost
  375/s (`OnFlyBirdMoveDashCost`), the end = 通用持续冲刺结束.lua `Stop()` (+ the
  camera/SFX notes); buffs 14561 免控 / 14129 换二段 logged. In-flight double-tap
  W no longer re-casts the trigger (that produced the bogus 气力未足 on a 7k bar);
  the stage press cancels the dash; demo hooks RC_WH_DEMO_DASH_MS +
  RC_WH_DEMO_S1..S4_MS.
- Evidence: dash run 21:16 exit=0 — `wh 20788 空中冲刺·上冲: DashToPitchDirection(480,
  face, pitch 0.00) -> 7200 u/s x 100 frames`, z 31532->76677 over 6 s (~7.5k u/s),
  power 6670->4419 (375/s), `wh 通用持续冲刺结束: Stop() (dash end, 100 frames)`;
  then the stages/急坠/登顶 exit; selftest WH_DASH_FRAME/FRAMES PASS.
- Open: the 弈韵 (踩人 target), 双人, 登顶 summit cannot be entered in a
  single-player sandbox; the 万花 bird-move speed is not in the extracted tables.
- Outcome: solved for the dash; the flight now matches the dash-based live feel.

### 2026-09-30 — client — live-feedback corrections: 气力值 scale + 长歌 dash revert
- User feedback (live game): the 上冲/下冲 behaviours are not right; the normal
  气力值 is ~700-1000 and a full bar flies ~1 minute; several numbers are off.
- Lesson/fix: (a) the 上冲/下冲/左冲/右冲 double-tap actions belong to the 长歌
  御空 system (Condition school 13, actions 13/14) — I had applied them to the
  万花 wrongly. The fly double-tap W is now a no-op (the 万花 rows have no such
  action) and the 20788 空中冲刺 dash moved to sandbox key 4. (b) The 气力值 UI
  scale: the scripts' nSprintPower is 10x the displayed bar (10000 = full =
  ~1000 shown); 10000 / OnFlyBirdMoveCost 206/s ~= 48 s ~= the observed ~1 min
  flight. HUD/gate/log now display the UI scale (WwRules.WhPowerUiScale 10).
- Evidence: build + selftest WH_POWER_UI_SCALE PASS; flow run 21:28 exit=0
  (power logged in the UI scale, e.g. 552 = 5520 internal); commit next.
- Open: the exact live controls/feel still diverge from the data reading —
  awaiting a concrete description (WW action, hold-W, Space stages, exit) to
  re-derive the flight model.
- Outcome: concrete errors fixed; the flight model needs live-input calibration.

### 2026-09-30 — client — 万花 大轻功 rebuilt to the live-game structure
- User gave the real flow: WW = 点墨江山·疾跑段 (fast run, NOT the trigger);
  疾跑段+Space = 纵跃段; Space = 一段..四段 (四段 = the end, then fall, Space
  inert); Shift at 一段 = 点墨江山·棋弈 (弈韵): Space cycles 弈韵一段..五段
  (五段+Space -> 弈韵一段), Shift in 棋弈 = 弈韵六段 (俯冲, the fall-out).
- Did: rebuilt the phase machine (1..5 base = 纵跃段/一段..四段, 6..11 =
  弈韵一段..六段): `WwRules.Evaluate` ground WW -> Sprint (疾跑段); the Space on
  the ground while the 疾跑段 runs -> the chain entry (the 20628 trigger + the
  336 launch, phase 1); the in-fly Space advances the phases (10 -> 6 cycle; 5
  = end, inert); the in-fly Shift (edge-latched) enters the 棋弈 from the
  一段/二段/三段 and dives out (六段 -> StopBirdFly) from the 弈韵; the 弈韵
  stages are a float (Z locked, pose cycling) because the J6..J10 rows are dives
  (MED/open). HUD/log names per phase; the Shift 10x debug disabled in the fly.
- Evidence: entry run 22:11 exit=0 — `jipao: enter` (疾跑段) -> `entry Space` ->
  `wh 20628 万花轻功触发 -> 点墨江山·纵跃段` -> the launch -> the fly -> the 一段;
  弈韵 run 22:09 exit=0 — 一段/二段 -> shift -> 弈韵一段 (float) -> the Space
  cycle 弈韵二/三/四段 (y stays 20454, no descent) -> shift2 -> `弈韵六段 (俯冲):
  the fly ends` -> the fall. Selftest PASS (GROUND_WW_SPRINT).
- Open: the 弈韵's authored motion (the float is a stand-in), the 四段->fall
  details, the dash key.
- Outcome: the structure now matches the live game; the 弈韵 motion is flagged.
