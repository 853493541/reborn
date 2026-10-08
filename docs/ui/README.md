# docs/ui - UI research index

UI system report + map/minimap research. Interactive viewer: `ui-process-app/`.

| Doc | Title |
|---|---|
| `BATTLE_FLOATING_UI.md` | Battle floating UI catalog — nameplates, combat text, cast bar, buff/target/feedback HUD (client-sourced, 2026-09-30) |
| `FONT_SCHEME_SYSTEM.md` | KGUI font/color code system — `FontScheme=#<id>`/`FontColor=<name>` resolution chain, 421 schemes / 36 slots / 106 colors, usage census |
| `MAP_MINIMAP_RESEARCH.md` | Map & Minimap research — how the game does it, and how 龙门绝境 maps |
| `UI_SYSTEM_REPORT.md` | JX3 Client UI System — Reproduction Report |
| `BASIC_UI_INVENTORY.md` | Basic UI scope register — all 1,210 client `ui/Config/Default` INIs (extracted 2026-10-03), classes, tiers, exclusions |
| `BASIC_UI_HUD.md` | HUD core deep research — MainBarPanel / Player / Target family (composition decoded) + remaining HUD register; authored-state verification policy |
| `BASIC_UI_PANELS.md` | Panel group register — the 推荐 shortlist panels: INI facts, pages/LSH, authored-vs-runtime split, review notes |
| `UI_RENDER_FIDELITY_PLAN.md` | Render fidelity retrospective (what made early renders "out of place") + tiered depth contract + P1-P5 plan (status badges, contact sheet, runtime replay) |
| `UI_RUNTIME_REPLAY.md` | The correct system: KGUI conformance (engine-derived, no per-window patches) + script runtime-state replay; systemic census + decompiler blocker |
| `UI_SYSTEM_COVERAGE.md` | Coverage audit vs the official client: per-axis percentages (constructs ≈99.7%, art ≈98.5%, text ≈84.6%, runtime replay 65% of scripted windows), gap list, reproduce |
| `UI_INTERACTION_REPLAY.md` | Interaction replay (the missing system): replay_server.lua dispatches the scripts' own event handlers (proven: bag checkbox toggle), viewer wiring design |
| `UI_REAL_CLIENT_ASSESSMENT.md` | Fidelity vs the real client: GT inventory, evidence-class matrix (client binary / client data / GT capture / self-assessed / unmeasured), partial-replay regression finding |
| `UI_RUNTIME_GAP.md` | Measured runtime-consumption gap: the replay's 671 dropped state-bearing calls (item-creation 169, arrangement 330, render 53, state 119) with the full per-window list and the fix plan — the measured reason lists stay empty and items sit at authored coordinates |
| `UI_OOB_FIX_PLAN.md` | Out-of-window (oob) fix plan (2026-10-07): measured class split — viewer placement bug / hidden-but-shown / authored overhang / clip false positive — engine sizing + clip truth, phases P0-P5, acceptance per class |
| `UI_WIREUP_PLAN.md` | Full remaining wire-up plan (2026-10-06 baseline): phases 0-7 — census truth, the 22 ERR windows by class, the 9 NOENTRY drivers, page-set/list/tree semantics, animation, interaction completion, extraction tails, GT fidelity |
| `HANDOFF_UI.md` | Session handoff (2026-10-07): current gates, what was done, open items, provisional VM patches, critical gotchas, commands — read before continuing the UI work |

## Tools

| Tool | Purpose |
|---|---|
| `tools/ui/extract_lua_string_table.py` | Decode a compiled UI string lib (`ui/String/string.lua`, `ui/String/hotkeystring.lua`, bound by `ui/module_info.xml` to `g_tStrings`/`g_tHotKey`) into the `ID\tLength\tString` TSV the viewer loads (`ui-process-app/Data/text/ui/String/string.txt`). Extract the lib from PakV4 first: `tools\netcode\extract_pak_paths.py --list <paths.txt with ui\String\string.lua> --out-dir <dir>`. |
| `tools/ui/ini_construct_census.py` | KGUI construct census / conformance check: scans all shipped INIs and reports usage of every WndType/PosType/ImageType/HandleType/FirstItemPosType/AnchorDst against the viewer's coverage (`--strict` exits 1 while any is unhandled). Part of `docs/ui/UI_RUNTIME_REPLAY.md`. |
| `tools/ui/runtime_gap_report.py` | Runtime-state gap report: classifies every recorded replay call against the viewer's consumer (`LayoutPlanBuilder.ApplyRuntimeMutations`), joins per-window render status, writes `docs/ui/UI_RUNTIME_GAP.md` (the missing-items / wrong-placement inventory). |
| `tools/ui/replay_harness.lua` | Batch replay harness (run by 32-bit PUC Lua 5.1): loads the client's engine-base libs + one window script, runs its entry chain against the INI and records every UI mutation to a TSV (`Data/runtime_state/<stem>.tsv`). |
| `tools/ui/replay_server.lua` | Interactive replay server (same shim): keeps the window loaded and dispatches the scripts' own event handlers on `EVENT <section> <handler>` with `MOUSE x y` / `CLIENT w h` environment lines; the viewer's click/hover/drag/typing layer and `--click`/`--drag` drive it. |
| `tools/ui/replay_all.py` | Replays every scripted window through `replay_harness.lua` and writes `Data/runtime_state/replay_summary.tsv` (the OK/ERR/NOENTRY gate; only OK windows' state is applied by the viewer). |
| `tools/ui/scan_window_aliases.py` | Scans the window scripts' own `SETGLOBAL` opener definitions (`OpenBankPanel` → `BigBankPanel`) into `Data/ui_window_aliases.tsv`, so recorded popup chains resolve to catalog windows. |
| `tools/ui_scheme_lookup.py` | Resolve `FontScheme=#<id>`/`FontColor=<name>` against the shipped `uifontscheme.ini`/`font.ini`/`fontlist.ini` chain; prints slot sizes/colors and usage rows. |
| `tools/prepare_ui_fonts.py` | Stage the shipped font/color-ini inputs into the local (ignored) work dir for offline comparison. |
| `tools/pvp/dump_fn_disasm.py` | Annotate RIP-relative string references in disasm dumps (`@addr` labels) — used for the represent-call/markup analysis. |
