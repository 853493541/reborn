# docs/ui - UI research index

UI system report + map/minimap research. Interactive viewer: `ui-process-app/`.

| Doc | Title |
|---|---|
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

## Tools

| Tool | Purpose |
|---|---|
| `tools/ui/extract_lua_string_table.py` | Decode a compiled UI string lib (`ui/String/string.lua`, `ui/String/hotkeystring.lua`, bound by `ui/module_info.xml` to `g_tStrings`/`g_tHotKey`) into the `ID\tLength\tString` TSV the viewer loads (`ui-process-app/Data/text/ui/String/string.txt`). Extract the lib from PakV4 first: `tools\netcode\extract_pak_paths.py --list <paths.txt with ui\String\string.lua> --out-dir <dir>`. |
| `tools/ui/ini_construct_census.py` | KGUI construct census / conformance check: scans all shipped INIs and reports usage of every WndType/PosType/ImageType/HandleType/FirstItemPosType/AnchorDst against the viewer's coverage (`--strict` exits 1 while any is unhandled). Part of `docs/ui/UI_RUNTIME_REPLAY.md`. |
