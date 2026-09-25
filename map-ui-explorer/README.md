# JX3 Map UI Explorer

Standalone workspace for inspecting the three in-game map components and the
extracted 龙门绝境 / 绝境 map data. It reads the existing evidence under
`../proof/minimap/`; it does not modify the original app or game files.

## Run

From this folder:

```powershell
node server.mjs
```

Open <http://127.0.0.1:3037>. No npm install or external packages are needed.

## What is shown

- MiddleMap (M): actual extracted map art, parsed `config.ini`, and `area.tab`
  locations when present.
- Minimap: the in-game crop, assembled tile mosaic, and extracted minimap UI
  atlases. The daytime 龙门寻宝 view has a verified full-grid mosaic; four
  individual PNG tile samples are retained locally. Other maps show their
  extracted big-map art, CDN tile counts, and any local tile samples.
- BattleFieldMap: real in-game crop, its draggable UI config, and extracted
  battlefield UI art links in the original TGA/DDS formats.
- Component notes: UI config/script, KGUI `WndMinimap`, Represent layer call,
  markers, radar, and heat-map evidence.

Tile counts refer to the local CDN index; they are separate from the number of
loose tile files materialized in this workspace and from the assembled mosaic.

Branch at creation: `main`.
