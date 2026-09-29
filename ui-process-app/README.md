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

Current selftest: **15/15 windows rendered**, e.g. NewBattleFieldQueue 1,129
sections / 1,016 elements, BattleFieldMap 367 sections.

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
  from `docs/netcode/JX3_MODE_UI_INVENTORY.md` (evidence paths included). Window
  entries carry the static state to replay: `texts` (Lua `SetText` / sample
  values), `images` (Lua `FromUITex` frame swaps), `show` (script-shown
  `LockShowAndHide=1` sections), `hide`, `tabs`, `lists`, `anchors`, `adjust`.
- `Data/text/**` — committed UTF-8 copies of the game text the renderer loads
  (system tables + per-window `StringTable=` files such as
  `string_ArenaCorpsPanel.txt` for NewBattleFieldQueue; see
  `tools/prepare_ui_text.py`).
- `assets/` — git-ignored local extraction: `ui/Config/**` KGUI layouts,
  `ui/Scheme/Case/**`, `ui/Font/**` shipped fonts, `pak/` settlement INIs and the
  flat `uitex/` atlases pulled from PakV4. Fall back to `proof/minimap/ui` (and
  `UIPROC_UI_ROOT`) for art.

## Notes

- Missing windows (storm HUD `STR_TIMEDESERT`/`STR_LEFTPEELE` renderer, ready
  prompt, death overlay) appear with status PARTIAL and no INI; the hunt list is
  in `docs/netcode/JX3_MODE_UI_INVENTORY.md` §8.
- Game art is never committed: queue/settlement atlases are pulled from PakV4
  into the local `assets/uitex` (flat) or resolve from `proof/minimap/ui` when
  present; without either, those windows render as layout + text placeholders.
- Set `UIPROC_APP_ROOT` to point at `ui-process-app` if you move the exe away
  from the repository.
