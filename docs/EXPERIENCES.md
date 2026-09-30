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
