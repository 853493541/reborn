# docs/ui - UI research index

UI system report + map/minimap research. Interactive viewer: `ui-process-app/`.

| Doc | Title |
|---|---|
| `BATTLE_FLOATING_UI.md` | Battle floating UI catalog — nameplates, combat text, cast bar, buff/target/feedback HUD (client-sourced, 2026-09-30) |
| `FONT_SCHEME_SYSTEM.md` | KGUI font/color code system — `FontScheme=#<id>`/`FontColor=<name>` resolution chain, 421 schemes / 36 slots / 106 colors, usage census |
| `MAP_MINIMAP_RESEARCH.md` | Map & Minimap research — how the game does it, and how 龙门绝境 maps |
| `UI_SYSTEM_REPORT.md` | JX3 Client UI System — Reproduction Report |

Tools:

| Tool | Purpose |
|---|---|
| `tools/ui_scheme_lookup.py` | Resolve `FontScheme=#<id>`/`FontColor=<name>` against the shipped `uifontscheme.ini`/`font.ini`/`fontlist.ini` chain; prints slot sizes/colors and usage rows |
| `tools/prepare_ui_fonts.py` | Stage the shipped font/color-ini inputs into the local (ignored) work dir for offline comparison |
| `tools/pvp/dump_fn_disasm.py` | Annotate RIP-relative string references in disasm dumps (`@addr` labels) — used for the represent-call/markup analysis |
