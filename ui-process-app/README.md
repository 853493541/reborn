# JX3 绝境战场 — UI Process Explorer (WPF)

Interactive browser for every UI surface recovered during the 绝境战场 research:
the queue panel, queue tracker, ready prompt, loading window, staging countdown,
in-match HUD, death/revive, the PVPShowFinal settlement panel and the exit flow.

The app renders the **real KGUI layout INIs** (not screenshots) with the shared
`UiLayout` renderer from `map-ui-app`, and resolves every `STR_*` label through
the official PakV4 string tables.

## Run

```powershell
dotnet run --project ui-process-app
```

or build once and start the executable:

```powershell
dotnet build ui-process-app -c Release
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe
```

Headless verification (loads the inventory, builds every window with an
extracted INI, writes `ui_process_selftest.txt` next to the exe):

```powershell
UiProcessApp.exe --selftest
```

App assets (layout INIs, fonts, scheme tables) are game data and are not
committed. Stage them once per checkout — without them the selftest cannot build
windows and text falls back to Microsoft YaHei UI / default schemes. Also run
`tools/prepare_ui_text.py` for the map text copies:

```powershell
.venv\Scripts\python.exe tools\prepare_ui_configs.py   # layouts/settlement INIs from the inventory
.venv\Scripts\python.exe tools\prepare_ui_fonts.py     # fonts + scheme tables
UiProcessApp.exe --fonttest
# expect: family=FZHei-B01 resolved=True
#         scheme #18: size=15 color=#F0F0F0 file=fzht_GBK.ttf
#         scheme #43: size=20 color=#000000 file=fzht_GBK.ttf
#         scheme #212: size=14 color=#F0F0F0 file=fzht_GBK.ttf
```

Current selftest: **rendered=77 skipped=4 failed=0** over the 81-window inventory.
Four skips: the ready prompt (native) and staging countdown (no renderer) have no
INI by design; `pvp-random-force` and `skill-glossary` are marked PARTIAL because
the static renderer hits a WPF logical-child error on those two INIs. Stages 9-11
render the battle-HUD appendix from `docs/ui/BATTLE_FLOATING_UI.md` §A (60
modules: objective/score panels, warnings, team/raid lists, buff monitors, loot
rolls, death/revive, skill bars/hints/panels, mode HUD).

## What it shows

| pane | content |
|---|---|
| left tree | stages (queue → match → loading → staging → HUD → death → settlement → exit) and their windows |
| Details | summary, status (PROVEN / PARTIAL), evidence list (click `open` to reveal the file), interface elements, key labels |
| Layout | the KGUI window rebuilt from its INI with a zoom slider; textures come from `proof/minimap/ui` when that extraction exists |
| Labels 文案 | every `STR_*` referenced by the window/layout, resolved to Chinese |
| INI 源码 | the raw layout file |

Search box filters windows by name/summary/label.

### Layout controls

- **Page** — KGUI tab sections (`Page_*`): only the selected tab's content renders.
- **隐藏 (Hide)** — comma-separated section names whose whole subtree is hidden;
  `Name_*` wildcards are supported. Windows in `Data/ui_inventory.json` may carry a
  default `hide` list (the queue panel hides extra full-window backgrounds and the
  mutually-exclusive `Handle_Mathing_*` matching-state blocks).
- **结构/名称 Wireframe** — draws section names / outlines missing-art elements.

The renderer follows the engine's own rules where decoded: `AnchorArgs` uses the
KGUI side names (TOPRIGHT/LEFTCENTER/…), containers inherit the nearest sized
ancestor, re-issued suffixed sections render once (newest wins), `PosType`
alignments follow the map-ui-app-verified mapping, and every extracted
`ui/Scheme/Case/*.txt` string table is loaded (6,900+ ids).

Offscreen verification (no GUI), useful for diffing renders:

```powershell
UiProcessApp.exe --render queue-panel --page Page_DesertStorm `
  --hide Image_Glassmorphism_960x624 --only Handle_BGTEAM --out out.png [--wire]
```

Rendered text uses the shipped font schemes (`font.ini`/`fontlist.ini`/`color.txt`)
and unresolved string ids are hidden instead of shown raw.

## Data

- `Data/ui_inventory.json` — the stage/window/element/label inventory, generated
  from `docs/netcode/JX3_MODE_UI_INVENTORY.md` (evidence paths included).
- `assets/ui/Config/**` — extracted KGUI layouts (converted to UTF-8).
- `assets/ui/Scheme/Case/string.txt` — official UI strings (UTF-8 copy).
- `assets/ui/Font/**` — the 5 shipped client UI fonts, copied locally by
  `tools/prepare_ui_fonts.py` (game assets, never committed).
- `assets/ui/Scheme/Case/{font.ini,fontlist.ini,fontpathlist.ini,color.txt}` —
  the shipped scheme tables (taken from the tracked extraction), copied by the
  same tool so `FontScheme`/`FontColor` codes resolve (see
  `docs/ui/FONT_SCHEME_SYSTEM.md`).
- `assets/pak/**` — settlement panel (`PVPShowFinal*`) + `string_PVPAcount.txt`.

## Notes

- Missing windows (storm HUD `STR_TIMEDESERT`/`STR_LEFTPEELE` renderer, ready
  prompt, death overlay) appear with status PARTIAL and no INI; the hunt list is
  in `docs/netcode/JX3_MODE_UI_INVENTORY.md` §8.
- Textures for queue/settlement panels were never extracted, so those windows
  render as layout + text placeholders. Map windows get full art when the
  git-ignored `proof/minimap/ui` extraction is present next to the worktree.
- Set `UIPROC_APP_ROOT` to point at `ui-process-app` if you move the exe away
  from the repository.
