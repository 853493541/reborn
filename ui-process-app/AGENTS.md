# ui-process-app — agent notes

WPF (`net5.0-windows`) explorer for the recovered JX3 UI surfaces (绝境战场 mode + the
default 基础界面), rendered from the real KGUI layout INIs and official string tables.
The basic-UI scope register is `docs/ui/BASIC_UI_INVENTORY.md`; HUD deep research lives
in `docs/ui/BASIC_UI_HUD.md`.

- Run: `dotnet run --project ui-process-app`. Release binary:
  `ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe`.
- **不需要 (not needed): press `X`** in the viewer to move the currently shown window into a
  `不需要` stage at the bottom of the tree; press `X` again to restore it to its original stage.
  State is a side file `Data/rejected.tsv` (windowId TAB originalStageId) — the catalog JSON stays
  clean; headless equivalent: `UiProcessApp.exe --reject <windowId>` (toggles).
- **Working on a catalog item → set it as the default**: when a session works on
  one of the windows in the list (the user's "5.x"), update the root
  `defaultWindow` in `Data/ui_inventory.json` to that window id (the viewer opens
  on it; `MainWindow` falls back to the first window when it is missing). Leave it
  pointing at the last item worked on so the user never has to switch manually.
- Gate: `UiProcessApp.exe --selftest` must report **0 failed** (currently 1,037
  rendered / 0 skipped; writes `ui_process_selftest.txt` next to the exe).
- `Data/ui_inventory.json` is generated from `docs/netcode/JX3_MODE_UI_INVENTORY.md` —
  update the doc first, then regenerate; keep the evidence paths in the inventory.
- Text assets in `Data/text/` are UTF-8 copies of the game files (see
  `tools/prepare_ui_text.py`) or decoded string libs (the global `g_tStrings`
  table, `tools/ui/extract_lua_string_table.py`); never point the renderer at the
  game install directly.
- Textures come from the git-ignored `proof/minimap/ui` extraction when present;
  missing art is expected and must render as placeholder/wireframe, not invented.
- Desktop only — no web tooling. Root `AGENTS.md` rules apply.
