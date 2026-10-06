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
