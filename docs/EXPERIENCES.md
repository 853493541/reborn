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
