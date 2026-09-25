# JX3 Map UI (WPF)

Standalone Windows app that recreates the three in-game map windows from the
extracted game assets — not from screenshots. It reads the real KGUI layout INI
files, the `.UITex` atlas frame descriptors, the original TGA textures, and the
per-map data bundles.

## Run

```powershell
dotnet run --project map-ui-app
```

or build once and start the executable:

```powershell
dotnet build map-ui-app -c Release
map-ui-app\bin\Release\net5.0-windows\MapUiApp.exe
```

## Controls

| input | action |
|---|---|
| `M` | toggle the MiddleMap window (the real map + window UI) |
| `B` | toggle the draggable BattleFieldMap panel |
| `Esc` | close the MiddleMap |
| `1`–`7` | switch map variant (龙门绝境 day/night, 沧溟, 白龙, 天原, 洱海, 林海) |
| mouse wheel | zoom the MiddleMap / minimap lens (hover over it) |
| mouse drag | pan the MiddleMap, drag the BattleFieldMap by its top bar |
| click | the lens `M` button opens the MiddleMap, its +/− buttons zoom the lens |

## What is real

- Window layouts: `ui/Config/Default/{MiniMap,MiddleMap,BattleField/BattleFieldMap}.ini`
- Atlas frames: `.UITex` (92-byte header + 20-byte frame records), decoded at runtime
- Textures: original `.Tga` (uncompressed/RLE BGRA) decoded at runtime
- Map art: per-map `middlemap.png`, minimap tile mosaic, `area.tab` labels, `config.ini` transform
- Strings: `ui/Scheme/Case/string.txt` + per-window string tables

## Diagnostics

```powershell
MapUiApp.exe --probe <dir>          # asset/frame report + frame dumps
MapUiApp.exe --dump <dir>           # render scene/lens/panel/window PNGs offscreen
MapUiApp.exe --map cangming         # start on another map
```

## Notes

- Text assets are converted to UTF-8 copies under `assets/text/` by
  `tools/prepare_ui_text.py` (run it after changing the extracted files).
- Storm-line segments and a few chrome textures are stored as DDS and are not
  decoded yet; dynamic markers/heat-map values are runtime data in the game.
