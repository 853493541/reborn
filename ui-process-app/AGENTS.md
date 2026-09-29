# ui-process-app — agent notes

WPF (`net5.0-windows`) explorer for the recovered 绝境战场 UI surfaces, rendered from
the real KGUI layout INIs and official string tables.

- Run: `dotnet run --project ui-process-app`. Release binary:
  `ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe`.
- Gate: `UiProcessApp.exe --selftest` must report **0 failed** (currently 20
  rendered / 1 skipped; writes `ui_process_selftest.txt` next to the exe).
- `Data/ui_inventory.json` is generated from `docs/netcode/JX3_MODE_UI_INVENTORY.md` —
  update the doc first, then regenerate; keep the evidence paths in the inventory.
- Text assets in `Data/text/` are UTF-8 copies of the game files (see
  `tools/prepare_ui_text.py`) or decoded string libs (the global `g_tStrings`
  table, `tools/ui/extract_lua_string_table.py`); never point the renderer at the
  game install directly.
- Textures come from the git-ignored `proof/minimap/ui` extraction when present;
  missing art is expected and must render as placeholder/wireframe, not invented.
- Desktop only — no web tooling. Root `AGENTS.md` rules apply.
