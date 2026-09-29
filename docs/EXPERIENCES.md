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
