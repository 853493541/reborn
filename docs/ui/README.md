# docs/ui - UI research index

UI system report + map/minimap research. Interactive viewer: `ui-process-app/`.

| Doc | Title |
|---|---|
| `MAP_MINIMAP_RESEARCH.md` | Map & Minimap research — how the game does it, and how 龙门绝境 maps |
| `UI_SYSTEM_REPORT.md` | JX3 Client UI System — Reproduction Report |
| `BASIC_UI_INVENTORY.md` | Basic UI scope register — all 1,210 client `ui/Config/Default` INIs (extracted 2026-10-03), classes, tiers, exclusions |
| `BASIC_UI_HUD.md` | HUD core deep research — MainBarPanel / Player / Target family (composition decoded) + remaining HUD register; authored-state verification policy |

## Tools

| Tool | Purpose |
|---|---|
| `tools/ui/extract_lua_string_table.py` | Decode a compiled UI string lib (`ui/String/string.lua`, `ui/String/hotkeystring.lua`, bound by `ui/module_info.xml` to `g_tStrings`/`g_tHotKey`) into the `ID\tLength\tString` TSV the viewer loads (`ui-process-app/Data/text/ui/String/string.txt`). Extract the lib from PakV4 first: `tools\netcode\extract_pak_paths.py --list <paths.txt with ui\String\string.lua> --out-dir <dir>`. |
