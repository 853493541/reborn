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
