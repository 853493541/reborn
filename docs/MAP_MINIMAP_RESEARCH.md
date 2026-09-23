# Map & Minimap research — how the game does it, and how 龙门绝境 maps

Date: 2026-09-22
Scope: native JX3 client, MovieEditor, and the Reborn host/viewer.
Evidence: client `settings/MapList.tab` + `SubMapInfo.tab` (extracted), CDN resource
index, extracted minimap bundle in `proof/minimap/`, IL dumps in
`engine_host_spike/recon_camera.txt`, viewer code in
`C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer`.

## 0. Key finding — 龙门绝境 is the display name of map `龙门寻宝`

The client catalog `settings/MapList.tab` (extracted to
`proof/minimap/client_settings/MapList.tab`, GBK TSV) has these rows:

| ID | Name (resource) | DisplayName | ResourcePath | MiniMapResourcePath |
|---|---|---|---|---|
| 296 | 龙门寻宝 | 龙门绝境 | `data\source\maps\龙门寻宝\龙门寻宝.jsonmap` | *(empty)* |
| 297 | 龙门寻宝_夜晚 | 龙门绝境·夜 | `data\source\maps\龙门寻宝_夜晚\龙门寻宝_夜晚.jsonmap` | *(empty)* |
| 676 | 龙门寻宝 | 龙门绝境 | same as 296 (MaxPlayerCount 108) | *(empty)* |
| 677 | 龙门寻宝 | 龙门绝境 | same as 296 (MaxPlayerCount 108) | *(empty)* |

Other 绝境 maps follow the same pattern (`海岛绝境` = DisplayName `沧溟绝境`,
`林海绝境` etc.). Battle-royale rows share `Type=2`, `IsBattlefield=1`,
`MaxCopyCount=512`, `MaxPlayerCount=118` (296) / `108` (676/677),
`BanSkillMask=512`, `BattleRelationMask=2`, `CampType=5`.

`MiniMapResourcePath` is empty for 龙门绝境; only the newest map (`林海绝境`)
sets it (`data\source\maps\林海绝境`). The runtime therefore falls back to the
default convention `<map resource folder>minimap` — confirmed by
`jx3-web-map-viewer/tools/materialize-cdn-map.mjs:385-387`:

```js
if (options.includeMinimap) {
  prefixes.push(`data/source/maps/${options.mapName}minimap`);
  prefixes.push(`data/source/maps/${options.mapName}minimap_mb`);
}
```

`settings/SubMapInfo.tab` maps sub-map → main-map placement
(`SubMapID, MainMapID, MainPosX, MainPosY, SubRegionWidth, SubRegionHeight`);
龙门绝境 (296/676/677/297) has **no** sub-map rows.

## 1. What the client actually ships for 龙门绝境

Extracted from the CDN hpkg cache (see §6 for the tool) into
`proof/minimap/extracted/data/source/maps/`:

```
龙门寻宝minimap/          (engine variant, DDS)
  0_-2_-2.dds … 0_33_33.dds    1296 tiles, 128x128 DXT1 + mips (11,064 B each)
  0_33_33.tga                  (one tile also shipped as TGA)
  middlemap.tga                3,670,060 B — full painted map (day/night differ)
  loadinglmxb.dds              1920x1080 loading screen
  config.ini                   tile + middlemap transforms
  area.tab / area.tab.fmt      named zone table
  npc.tab                      empty (0 B)
龙门寻宝minimap_mb/        (PNG variant, same content)
  0_-2_-2.png … 0_33_33.png    1296 tiles, 128x128 PNG
  middlemap.png                2048x1792 (config says 1024x896)
  loadinglmxb.png
  config.ini / area.tab / npc.tab
龙门寻宝_夜晚minimap/       (night variant, same structure, loadinglmjj2.*)
龙门寻宝_夜晚minimap_mb/
```

`npc.tab` empty means 龙门绝境 has **no static map markers**; area labels come
from `area.tab`, and dynamic markers (players, loot, airdrops, storm) come from
gameplay/netcode (`KMapMark`, `SyncMidMapMark`, see §5).

### config.ini (GBK)

```ini
[middlemap0]
name=龙门寻宝
image=middlemap.tga        ; or middlemap.png in _mb
width=1024
height=896
scale=0.005867004          ; image px per world unit
startx=-21476.04
starty=-10226.68
copy=0
fresherroom=0
battlefield=0

[loading]
image=loadinglmxb.dds
width=1920
height=1080

[config]
width=128                  ; tile size in px
scale=0.02                 ; tile px per world unit
```

### Tile grid math

- The 36×36 grid (`tx`,`ty` = -2..33) forms a 4608×4608 image (128 px/tile).
  Filenames are `0_<Z>_<X>` (second number = world Z, third = world X),
  north-up; the mosaic order is `gx = ty + 2`, `gy = 33 - tx` (verified by
  edge continuity: mean adjacent-edge delta 12.5 vs ~27.5 for all other
  flips/swaps; `07_minimap_mosaic_correct_4608.png` matches the `middlemap`
  island layout).
- Index selection is decoded from `KWndMinimap` (§9):
  `map = scale*world + offset`, `tile = floor(map / width)` per axis, with
  `scale`/`width`/`offsetx`/`offsety` read from the per-map
  `<map>minimap\config.ini` `[config]`. For 龙门寻宝: `width=128`,
  `scale=0.02`, offsets 0 → **6400 world units per tile**.
- The tile artwork is a different painting from `middlemap` (blurred
  correlation over all crops/flips/scales peaks at ~0.22), so pixel alignment
  to `area.tab` needs the landmark calibration in open question 5.

### Big-map (middlemap) math

`middlemap` covers `1024 / 0.005867004 ≈ 174,535` units wide and
`896 / 0.005867004 ≈ 152,718` units high, anchored at
`(startx, starty) = (-21476.04, -10226.68)`:

```
imgX = (worldX - startx) * scale
imgY = height - (worldZ - starty) * scale     // image Y is top-down
```

Example — 响马营地 `(21632, 110208)` → `imgX≈253`, `imgY≈189` on the 1024×896
logical image (upper-left area of the map). This is the same `fStartX/fStartY/
fScale/nHeight` transform the editor uses (`MiddleMapData`, §3).

### area.tab

GBK TSV: `id, name, middlemap, backgroundmusic, type, x, y, z, show, fullscreensfx`.
Columns 6/7/8 are world `x`, `y`(= world Z / north-south), `z`(= height, 0 here).
18 named rows for 龙门绝境 (world coords):

| zone | x | y | zone | x | y |
|---|---|---|---|---|---|
| 响马营地 | 21632 | 110208 | 龙门客栈 | 44416 | 36992 |
| 飞沙山 | 30208 | 87808 | 南戈壁 | 63104 | 23296 |
| 玉门关 | 10752 | 87296 | 扬沙裂谷 | 100224 | 6272 |
| 孔雀海 | 53760 | 88448 | 月氏遗迹 | 104960 | 23040 |
| 鸣沙山 | 56320 | 61312 | 龙门峡谷 | 104192 | 34176 |
| 银沙石林 | 15872 | 50944 | 碎风岩壁 | 109824 | 50688 |
| 楼兰古城 | 16896 | 16768 | 清澜湖畔 | 114944 | 65408 |
| 血衣魔鬼城 | 84736 | 78208 | 古祭坛 | 108544 | 114432 |
| 乌孙旧都 | 77824 | 113920 | 龙门绝境 (map title) | 65408 | 65664 |

(`area.tab.fmt` = 11-byte format descriptor; `show` is empty for all real rows.)

## 2. How the client minimap / big map work

- **Catalog**: `KMapListFile` loads `settings/MapList.tab` and
  `settings/SubMapInfo.tab` (`JX3ClientX64_exe_strings.txt:736085, 736236,
  736239`). `MapParam` carries `ResourcePath` + `MiniMapResourcePath`
  (`:736124`).
- **Minimap layers**: `KGameWorldHandler::GetMinimapLayer`
  (`JX3RepresentX64_strings.txt:755431`) serves the layered minimap art; the
  path template is `data\source\maps\%sminimap\RegionLayer.tab` (`:751405`).
  For 龙门绝境 the shipped minimap folder has `config.ini/area.tab/npc.tab`
  instead of `RegionLayer.tab` (that file only exists for some maps, e.g.
  伊丽川, 万灵山庄, 太极宫 — see the CDN index), so the tile grid in
  `config.ini` is the layer definition for this map.
- **Markers**: `KMapMark` class; NPCs/doodads register via
  `KNpc::UpdateMiniMapMark`, `KDoodad::UpdateMiniMapMark`
  (`:760809, 760868`); network sync `DoSyncMidMapMark`/`OnSyncMidMapMark`/
  `OnSyncMapMarkInfo`; Lua `LuaSyncMidMapMark`, `LuaGetMapMark`/`GetMapMark`.
- **Radar modes**: `SetMinimapRadar`/`GetMiniMapRadar` with
  `mrtInvalid..mrtTotal`, `RadarID`, `dwCraftRadarID`, strings
  `RADAR/NO_RADAR/MINI_RADAR_TYPE`. **Fog of war**: `OnSetFogOfWar`
  (`JX3RepresentX64_strings.txt:732282`).
- **Coordinate conversion**: `MapConverter` (`m_tabMapConverter`, driven by
  `Represent/common/map_converter.krl.txt`) and
  `KMapListFile::GetSubMapPosition`/`GetMainMapPosition` for placing sub-maps
  on the main map.
- Big map vs minimap: the big map uses the `middlemap` image + `area.tab`
  labels + mark sync; the minimap streams `*minimap` tiles around the player at
  `[config] scale`.

## 2b. The three in-game map windows (M / top-right lens / draggable)

The client keeps per-window state in
`userdata/<account>/<server>/<role>/custom.dat` (a Lua table after a 16-byte
`CNDK` header; not copied here because it is account data). It names every map
window directly:

| window | key prefix | saved state (examples) | what it is |
|---|---|---|---|
| **MiddleMap** | `MiddleMap.*` | `nAlpha`, `tQuestShow`, `bShowActivitySymbol`, `aFlag`, `aTeamFlag`, `bShowExploreFinishFlag` | the big map opened by **M** (`ToggleMiddleMap()` / binding `TOGGLE_MIDDLEMAP`, `STR_SYSTEM_MAP` "地图 … 显示你当前所在区域的地图") |
| **Minimap** | `Minimap.*` | `bOpen=true`, `RadarType`, `RadarParam`, `RadarParam2`, `tDisplayCraftList`, `tSearchInfo`, `nVersion=2` | the top-right **lens**; buttons `M` (`STR_MINIMAP_BIGMAP`) and `+` (`STR_MINIMAP_ZOOMOUT`); toggle `STR_MINIMAP_SWITCH` |
| **BattleFieldMap** | `BattleFieldMap.*` | `Anchor={x,y,s,r}`, `bTurnOn`, `bExpand`, `fAlpha`, `bShowHeatMap` | the **draggable** battlefield map panel (绝境/战场); `STR_BATTLEFIELD_MAPTIP1` "点此返回小地图" |
| WorldMap (bonus) | `WorldMap.*` | `bFirstOpen`, `tOpenRecordToppingMapList` | world map opened from MiddleMap (`STR_MIDDLEMAP_ENTER` "进入世界地图"), has nav thumbnail/opacity/zoom (`STR_WORLDMAP_KAIGAN`, `STR_WORLD_TOUMING`, `STR_WORLD_SUOFANG`) |

`BattleFieldMap.Anchor` is the only map-window anchor present: it appears in
**all 92 characters** under `userdata/` (e.g.
`{x=-347.52,y=138.56,s="RIGHTCENTER",r="RIGHTCENTER"}`), which is why this is
the map that can be dragged; `MiddleMap`/`Minimap`/`WorldMap` store no anchor.

Data used by each window:

| window | art / data | markers / overlays |
|---|---|---|
| MiddleMap | per-map `middlemap.tga|png` + `config.ini [middlemap0]` transform + `area.tab` labels + `npc.tab` | `SyncMidMapMark`/`GetMapMark`, `KMapMark`, quest/team flags; `MiddleMap.aFlag/aTeamFlag` |
| Minimap | per-map tiles `0_<x>_<y>.dds|png` (`[config] width=128 scale=0.02`) streamed around the player; UI frame/atlas `ui/Image/Minimap/Minimap*.Tga` | `MapMark.Tga` icons, `KNpc/KDoodad::UpdateMiniMapMark`, radar modes `SetMinimapRadar`/`GetMiniMapRadar` (`Minimap.RadarType/RadarParam`), craft list `tDisplayCraftList` |
| BattleFieldMap | same minimap art, larger panel | heat map `KScene::LuaGetMapHeatInfo`, `KPlayer::LuaApplySceneHeatMap`; `bShowHeatMap`; storm/zone overlays |

### UI asset inventory extracted from PakV4 (proof/minimap/ui/, 563 files)

Full extraction of every asset referenced by the map layouts (with engine
extension swaps `.tga→.dds` etc.): 150 refs → 149 found; plus 125 `.UITex`
companion textures. The whole base-UI script set was then discovered by using
the window names in `custom.dat` as a dictionary (202 names → 253
`UI/Config/Default/*.{ini,lua}` files; inventory:
`proof/minimap/recon/ui_config_inventory.txt`). Highlights:

| file | size | content |
|---|---|---|
| `Config/Default/{MiniMap,MiddleMap,WorldMap,MainBarPanel,Balloon}.ini` | 31 KB–261 KB | window layouts (see component chain below) |
| `Config/Default/{Minimap,MiddleMap,WorldMap,MainBarPanel,Balloon}.lua` | 4 KB–230 KB | drivers, compiled Lua 5.1 bytecode (`\x1bLuaQ`) |
| `Scheme/Case/string.txt` | 151,701 B | all base UI strings incl. `STR_MIDDLEMAP_*`, `STR_MINIMAP_*`, `STR_WORLDMAP_*`, `STR_BATTLEFIELD_MAP*` |
| `Image/Minimap/MapMark.Tga` (+`.UITex`) | 1,173,778 B, 560×524 | big-map mark atlas (GM marks, flags, banners, frames, portraits) |
| `Image/Minimap/Minimap.Tga` | 2,955,794 B, 736×1004 | minimap icon atlas (arrows, NPC/quest/POI icons, numbers) |
| `Image/Minimap/Minimap2/3.Tga` | 1.57 MB / 1.18 MB | extra minimap + event/mode icons |
| `Image/Minimap/BattleMinimap.DDS`, `BattleMinimap2.Tga` | 264 KB / 745 KB | battlefield-minimap chrome |
| `Image/MiddleMap/MapWindow{,3..8}.Tga` | 1–5.2 MB each | big-map window frames/panels |
| `Image/MiddleMap/StormLine/*.DDS` | 41 files | storm-circle line segments (referenced as `.tga`, stored `.dds`) |
| `Image/MiddleMap/{LinkLine.tga,MapExplorr1.Tga}` | 12 KB / 34 KB | link lines + exploration progress |
| `Image/NewWorldMap/{NewWorldMap_*,map/NewWorld*,Details/NewDetail*}` | 1–4 MB | world-map layers/overlays |
| `Animation/{Middlemap,WorldMap,DaTangJiaYuan}_Ani.ini` | 8.7 KB | open/close tweens |

`.UITex` = atlas descriptor (`UI` v2 header: tex w/h, frame count, then a
64-byte `.Tga` name and frame rects); `.Tga` is plain uncompressed 32-bit BGRA
(`desc=8`). PNG conversions are kept next to the originals (`*_half.png` for
quick viewing).

### Screenshots (proof/minimap/screenshots/)

| file | what it shows | source |
|---|---|---|
| `01_bigmap_middlemap.png` | MiddleMap (M) full art, 2048×1792 | extracted `龙门寻宝minimap_mb/middlemap.png` |
| `01b_bigmap_night_middlemap.png` | night variant | `龙门寻宝_夜晚minimap_mb/middlemap.png` |
| `05_minimap_ingame_crop.png` | the real top-right lens in game (round map, 全图/M/+ buttons, storm ring, player arrow) | crop of `ScreenShot/2026-04-18_15-53-50-000.jpg` (龙门绝境 gameplay) |
| `06_battlefieldmap_ingame_crop.png` | the real draggable BattleFieldMap panel (green heat map, player sphere, waypoint cone, "叹息风碑") | same screenshot |
| `07_minimap_mosaic_correct_4608.png` | all 1296 minimap tiles stitched in the verified order (1536²) | local CDN hpkg tiles |
| `08_minimap_mosaic_correct_half.png` | same at 2304² | same |

The 4K source screenshots live in
`C:\SeasunGame\Game\JX3\bin\zhcn_hd\ScreenShot\`; the big map window (M) is not
open in any of them, so `01_*` is the true map art rather than a UI capture.

### Who creates the windows — component chain

Extracted base-UI files (proof/minimap/ui/Config/):

| file | role |
|---|---|
| `Default/MiniMap.ini` | top-right lens layout: `[Minimap]` (`._WndType=WndFrame`, `._Parent=Normal`, `ScriptFile=UI\Config\Default\Minimap.lua`, `AnchorArgs=TOPRIGHT,TOPRIGHT,0,0`, `ShowModeID=17,36`), `[Minimap_Map]` (`._WndType=WndMinimap`, `image=UI\Image\Minimap\Minimap.UITex`, `defaulttexture=…\defualtminimap.jpg`, `sharptexture=ui\Image\UItimate\Minimap\MinimapSharp.tga`, `MinimapType=1`), `[Btn_BigMap]` (frame 22, tip `STR_SYSTEM_MAP`), `[Btn_BattleField]` (frame 70), `[CheckBox_Switch]` (`STR_MINIMAP_SWITCH`) |
| `Default/Minimap.lua` (81,593 B) | lens driver: `GetMinimapLayer`, `SetMinimapRadar`/`MINI_RADAR_TYPE` (`NO_RADAR`, `FIND_CRAFT_DOODAD`, …), `RadarType`/`RadarParam2`, `GetMapMark`, `OpenMiddleMap`/`CloseMiddleMap`/`IsMiddleMapOpened`, `Btn_BattleField`, `IsInBattleField`, `IsInTreasureBattleFieldMap`, `NewBattleFieldQueue`, `GetQueueBFMapID` |
| `Default/MiddleMap.ini` (163,990 B) | M map layout: `[MiddleMap]`, `[Handle_MapMark]` (`ExportToLua=hMapMark`), textures `ui/Image/MiddleMap/MapWindow*.UITex`, `LinkLine.tga`, storm circle `ui/Image/MiddleMap/StormLine/*.tga`, `[Btn_WorldMap]`, `$Text=STR_MIDDLEMAP_*` |
| `Default/MiddleMap.lua` (229,646 B) | M map driver: 376 `MiddleMap` refs, `SyncMidMapMark` (team flags), `GetMapMark`, `GetMainMapPosition`/`GetSubMapPosition`, `GetMapHeatInfo`/`ApplySceneHeatMap`, `BattleFieldMap` |
| `Default/WorldMap.ini` + `WorldMap.lua` | world map window + driver |
| `Default/MainBarPanel.ini` + `.lua`, `Balloon.*` | main bar / balloon |
| `../scheme/elem/uiconfig.ini` | UI globals (canvas 1280×960, balloon config, ini loading) |

Runtime chain:

1. **Window creation** — `JX3UIX64.dll` (KGUI/KGUICocos XGUI) loads
   `UI/Config/Default/*.ini`; `WndFrame`/`WndWindow`/`WndMinimap` controls come
   from `KGUIX64.dll`/`KGUICocosX64.dll`; textures from
   `ui/Image/Minimap/*.UITex` + `ui/Image/MiddleMap/*`; labels from
   `ui/Scheme/Case/string.txt`; state (`Minimap.bOpen`, `RadarType`,
   `MiddleMap.nAlpha`, `BattleFieldMap.Anchor`, …) in
   `userdata/<acct>/<server>/<role>/custom.dat`.
2. **Map layer** — the engine side is `KGameWorldHandler::GetMinimapLayer`
   (`JX3RepresentX64.dll`), exposed to UI Lua as
   `KRepresentScriptTable::LuaSence_GetMinimapLayer` (`JX3UIX64.dll`); it reads
   the per-map minimap resources (`data/source/maps/<map>minimap/…`, or
   `RegionLayer.tab` where present). `MapConverter`/`m_tabMapConverter` in the
   same DLL places sub-maps on the main map.
3. **Catalog** — `KMapListFile::Init/Load` (`JX3LogicEditOperationX64.dll`)
   reads `MapList.tab` (+ `MiniMapResourcePath`) and `settings/SubMapInfo.tab`;
   `GetSubMapPosition`/`GetMainMapPosition` are the Lua-facing placements.
4. **Markers** — `KNpc::UpdateMiniMapMark` / `KDoodad::UpdateMiniMapMark` and
   `KPlayerClient::DoSyncMidMapMark`/`OnSyncMidMapMark`/`OnSyncMapMarkInfo`
   (`JX3LogicEditOperationX64.dll`) produce UI events
   `UPDATE_NPC_MINIMAP_MARK`, `UPDATE_DOODAD_MINIMAP_MARK`,
   `UPDATE_MID_MAP_MARK`, `UPDATE_MAP_MARK` handled by `JX3UIX64.dll`;
   `KPlayer::LuaSyncMidMapMark`/`LuaGetMapMark` bridge to Lua.
5. **Radar** — `KPlayer::LuaSetMinimapRadar`/`LuaGetMiniMapRadar` +
   `MINI_RADAR_TYPE`/`RadarID`; UI event `UPDATE_RADAR`.
6. **Heat map / battlefield map** — `KScene::UnPackHeatMapBinaryData`,
   `KPlayerClient::DoApplySceneHeatMap`/`OnSyncSceneHeatMap` →
   `ON_SYNC_SCENE_HEAT_MAP`. The draggable panel is **base UI**, in its own
   config folder: `UI/Config/Default/BattleField/BattleFieldMap.ini` +
   `BattleFieldMap.lua` (`[BattleFieldMap]` `._WndType=WndFrame`,
   `._Parent=Normal`, `Moveable=1`, `DragArea 305×40`,
   `ScriptFile=ui\Config\Default\BattleFieldMap.lua`, `ShowModeID=1,4,5,17`),
   chrome `ui/Image/Minimap/BattleMinimap{,2,3}.UITex` + storm lines
   (`Mini709_*`, `StormLine3/4`). Its Lua defines `BattleFieldMap_TurnOn`,
   the `BattleFieldMap` state table and `RegisterCustomData` keys.
7. **Network/window plumbing** — `KPlayerClient::DoSelectSwitchMapWindow`,
   `OPEN_SWITCH_MAP_WINDOW`, map queue events (`ON_MAP_QUEUE_POS_UPDATE`, …).

So the in-game lens reads: `UI/Config/Default/MiniMap.ini` + `Minimap.lua`
(layout/behaviour), `ui/Image/Minimap/Minimap*.UITex` (chrome/icons),
`ui/Scheme/Case/string.txt` (labels), `custom.dat` (state), the per-map
`<map>minimap` folder (art/config/area/npc), `settings/MapList.tab` +
`SubMapInfo.tab` (catalog), and live marker/radar data from the client logic
module.

## 3. MovieEditor's map (MiddleMapForm, IL-verified)

`engine_host_spike/recon_camera.txt:905-973` (`MiddleMapForm::
OnMiddleMapDoubleClick`), toggled by hotkey id 6 → `OpenMiddleMap`
(`MainForm.m_MiddleMapForm`), and `MiddleMapForm::RefreshPosLocation` /
`button_SetPos_Click` keep/set the position marker.

```csharp
img   = PictureBoxPointToImgPoint(mouse);
logicalX = fStartX + img.X / fScale;
logicalY = fStartY + (nHeight - img.Y) / fScale;
scene(x,y,z) = LogicalToScene(logicalX, 10000, logicalY);  // y = terrain height
scene.SetCameraPos(x, y, z, true);
```

`MiddleMapData { fStartX, fStartY, fScale, nHeight }` maps 1:1 onto
`config.ini [middlemap0]` (`startx`, `starty`, `scale`, `height`) — the editor
shows exactly the shipped `middlemap`. The editor also has
`MovieEditor/source/Map/<map>/temp_minimaps/` (empty in this install) for
generated minimaps; the viewer's `editor-minimap.png` is a stitched/derived
version of the same art.

## 4. Reborn viewer minimap (jx3-web-map-viewer)

`public/js/app.js`:

- `setupMinimap()` (line 486): canvas 256×256; the playable inner 4×4 regions
  (regions 2–5 of the 8×8 grid) occupy the centre 50%; outer regions are the
  gray border. Loads `${map}/minimap.png` (or `regioninfo.png`) if
  `environmentItems.minimap`, else falls back to heightmap shading.
- `_drawMinimapMode()` (578): three modes cycled by `#minimap-toggle`:
  `region` (minimap.png), `editor` (editor-minimap.png), `height`
  (heightmap).
- `_buildHeightmapMinimap()` (615): grayscale/brown ramp from
  `terrainSystem.minimapData` (200×200 from 25 samples/region —
  `public/js/terrain.js:378-401`).
- `updateMinimap()` (642): positions `#minimap-marker` and the `#minimap-fov`
  wedge from camera world position/yaw over `terrainSystem.getWorldBounds()`;
  click on the canvas teleports (uses full bounds, so clicking the gray border
  moves outside the playable area).

The `map-data/minimap.png` / `editor-minimap.png` currently in the repo are the
grayscale heightmap-derived images (the viewer's own `_mb` extraction produced
corrupt PNGs because hpkg members compressed with <64 B overhead are mistaken
for header-prefixed raw members — see §6).

## 5. Reborn host (MapSpike.cs)

No minimap widget yet. Available pieces:

- `scene.GetSceneRect` — map-relative rect (`MapSpike.cs:326`);
  `SPIKE_B_MAP_NOTES.md:46` records x=-1024..3072, y=-1024..3072 for 龙门寻宝.
- `TerrainSampler` (`MapSpike.cs:1542-1684`): origin (-102400,-102400),
  512-cell regions × 100-unit cells, 8×8 grid, bilinear `Sample(x,z)`.
- Player world position `plX/plY/plZ` (`MapSpike.cs:520-945`, logged at 944).
- `MAP_TOUR` camera positions and screenshots.

To render the real minimap for 龙门绝境: stream `<map>minimap/0_<tx>_<ty>.dds`
around `(plX, plZ)` (tile = 6400 units, 128 px), or draw
`middlemap.tga`/`.png` for the big map with the §1 transform, then overlay
`area.tab` labels and `KMapMark`-style markers.

## 6. Reproducing the extraction

- Client catalog: `PakV4SfxExtract.exe` via `pss_assets.run_pakv4`
  (`_probe_map_files.py` shows the pattern) for `settings/MapList.tab` and
  `settings/SubMapInfo.tab`.
- Base UI assets (also via `run_pakv4`, pathlist GBK):
  `ui/Scheme/Case/string.txt`, `ui/Image/Minimap/{Minimap,Minimap2,Minimap3,
  Minimap4,MapMark}.{UITex,Tga}` (PakV4 dirs are indexed by path hash, so the
  extractor must be given exact paths; guessed schemes under `ui/Scheme/<Module>/`
  return nothing — the map windows themselves are native, not `.ini` layouts).
  `.Tga` are uncompressed 32-bit BGRA; a pure-Python TGA→PNG converter is in the
  session scratch (`tga2png.py`), outputs under `proof/minimap/ui/Image/Minimap/`.
- Per-window state: `userdata/<account>/<server>/<role>/custom.dat`
  (Lua table, `CNDK` header) — search for `MiddleMap.`, `Minimap.`,
  `BattleFieldMap.`, `WorldMap.` keys.
- Minimap assets: the live CDN cache used by jx3-web-map-viewer already has all
  hpkg packages (`cache-extraction/online-cdn/downloads/<dir>_<name>.hpkg`) and
  an index (`resource-browser/resource-index.jsonl`). HPKG member extraction:
  decode the LZHAM index at offset 68, records at 300-byte stride
  (`path@+4, originalSize@+280, storedSize@+284, payloadOffset@+288`), payload
  starts at `64 + packedIndexSize`.
  **Pitfall**: when `storedSize - originalSize` is in `(0,64]` the member is
  still LZHAM (20-byte prefix) — try LZHAM at skip 0/4/…/48 before treating it
  as a header-prefixed raw member, otherwise the PNG comes out truncated.
  Working sample output: `proof/minimap/extracted/...` and the repaired
  `proof/minimap/extracted/minimap_samples/0_16_16.png`.

## 7. Open questions

1. `middlemap.png` is 2048×1792 while `config.ini` says 1024×896 — which
   resolution does the client actually sample? (2× supersample suspected.)
2. Storm-circle / airdrop / loot markers use the `KMapMark` types (`nType`)
   whose enum values are still unnamed; the record/sync layout itself is
   solved in §10.
3. Only layer 0 exists for 龙门绝境; maps with `RegionLayer.tab` may define
   multiple zoom layers, worth checking on 伊丽川/万灵山庄.
4. Night map (`龙门寻宝_夜晚`) has its own art: `middlemap.tga/.png` and the
   sampled tiles differ from the day map (verified by hash), and it uses
   `loadinglmjj2.*` instead of `loadinglmxb.*`.
5. **Tile calibration**: the control mechanism is solved (§9), but the world
   coordinates fed to it are scene/game-world values. Fitting four landmarks
   (`area.tab` anchors vs mosaic positions) gives ~3.8k–4.2k units/tile in
   `area.tab` space instead of the code's 6400, so either the anchors are not
   the visual centers or the two coordinate spaces differ by a scale. Fit with
   precise props (`.SRScene`/sceneinfo world positions) to close it.
6. **`BattleFieldMap` — solved.** It is base UI, living in the config
   subfolder `UI/Config/Default/BattleField/` (`BattleFieldMap.ini` + `.lua`,
   plus `MobaEnemyPanel.*`). The root `[BattleFieldMap]` is a `WndFrame`
   parented to `Normal`, `Moveable=1` (draggable) with a 305×40 drag area,
   shown in modes `ShowModeID=1,4,5,17`. Its Lua defines
   `BattleFieldMap_TurnOn` and the `BattleFieldMap` table
   (`bTurnOn`, `bExpand`, `fAlpha`, `bShowHeatMap`, `tCampCount`, `Anchor`)
   and registers the `BattleFieldMap.*` custom-data keys. The JX addon
   (`JX_Moba`) only injects dragon-timer items into `Normal/BattleFieldMap`.
   All battlefield chrome textures extracted (`BattleMinimap.DDS`,
   `BattleMinimap2/3.TGA`, `BMap_149.DDS`, `Mini709_*`, `StormLine3/4`).
7. Heat-map payload shape and storage are solved (§10); remaining: the exact
   `KHEAT_MAP_NODE_SYNC_INFO` field semantics (8 bytes: region x/y + counts)
   and the `nType` enum for marks.

## 8. Decompiled drivers (solved behaviour)

The four base UI scripts are compiled Lua 5.1 (`\x1bLuaQ`); they were
decompiled with unluac (source built locally from the GitHub mirror, since the
`unluac.jar` in reborn-netcode was a 9-byte "Not Found" placeholder). Output:

- `proof/minimap/ui/Config/Default/decompiled/{Minimap,MiddleMap,WorldMap,MainBarPanel}.decompiled.lua`

Behaviour recovered so far:

- **Minimap zoom**: `Minimap_Map:SetScale(2)` default; `Btn_ZoomIn` ×1.1 capped
  at **2.0**, `Btn_ZoomOut` ×0.9 floored at **0.5**; buttons enable/disable at
  the bounds (`Minimap.decompiled.lua:1030-1034, 7080-7167`).
- **`GetMinimapLayer(dwID, x, y, z)`** returns the layer **Z for a map mark**
  (stored as `Minimap.nMapMarkZ`, `Minimap.decompiled.lua:2956-2966`) — it is
  *not* the map-art fetch; the art is drawn by the native `WndMinimap` control.
- **Radar**: `GetClientPlayer():SetMinimapRadar(MINI_RADAR_TYPE.FIND_CRAFT_DOODAD
  / NO_RADAR)` toggled from the craft/gather buttons (`:4815-4964, 6341-8096`).
- **MiddleMap heat map**: `GetClientScene():GetMapHeatInfo(mapID)` →
  `{nRegionX, nRegionY, …}` entries stored in
  `MiddleMap.tHeatMapInfo[nRegionX][nRegionY]` plus `tCampCount`; modes
  `HEAT_MAP_MODE`/`HEAT_MAP_AREA`, `nHeatMapMode`, refresh cooldown,
  `ApplySceneHeatMap` request, `ON_SYNC_SCENE_HEAT_MAP` event
  (`MiddleMap.decompiled.lua:25779+`, strings `Btn_HeatMapState`,
  `CanAutoRefreshHeatMap`, `GetHeatMapCellLength`).
- **BattleFieldMap** driver: `UI/Config/Default/BattleField/BattleFieldMap.lua`
  (decompiled to `BattleField/BattleFieldMap.decompiled.lua`): defines
  `BattleFieldMap_TurnOn`, state table `{bTurnOn, bExpand, fAlpha,
  bShowHeatMap, tCampCount, Anchor, tData}`, `RegisterCustomData` keys,
  `GetClientScene():GetMapHeatInfo`, `GetClientPlayer():GetMapMark`, storm
  lines, player marker. Read by `MiddleMap.lua` via
  `BattleFieldMap.GetCBoxLastCheckBoxChooseLineID()`.

## 9. Native minimap control internals (KGUIX64.dll, static RE)

`UI::KWndMinimap` (also in `KGUICocosX64.dll` for mobile) is the native control
that renders the lens. Recon outputs:
`proof/minimap/recon/xref_kgui_minimap.txt`, `kgui_tile_loader.txt`,
`kgui_setmappath.txt`, `kgui_update_selfpos.txt`, `kgui_get_sendpos.txt`,
`kgui_pos_update_native.txt`, `kgui_sendpos_native.txt`.

| item | address | meaning |
|---|---|---|
| ctor | `0x1801342e0` | defaults: `scale=1.0` (`+0xbfc`), `width=128.0` (`+0xc00`), `offsetx/offsety=0` (`+0xc04/+0xc08`), map id `-1`, display zoom `1.0` (`+0xc14`) |
| `LuaMinimap_SetMapPath(L,path)` | `0x1801d3ff0` | builds `\<path>minimap\` + `config.ini`, reads `[config]` → `scale`, `width`, `offsetx`, `offsety` |
| `LuaMinimap_UpdateSelfPos(L,layer,x,y,z)` | `0x1801d5200` | `+0xd54=layer`; `mapX = scale*x + offsetx` → `+0xc30`; `mapZ = scale*z + offsety` → `+0xc34` |
| tile update | `0x180135940` | `tileX=floor(mapX/width)`, `tileZ=floor(mapZ/width)`, compares layer `+0x20`, x `+0x24`, y `+0x28`; loads via `'%s%d_%d_%d.dds'` with fallback `'.tga'` at `0x180135f8e` — **name = `<layer>_<Z>_<X>`** |
| `LuaMinimap_SetScale/GetScale` | `0x1801d4230` / `0x1801d4320` | clamps into display zoom `+0xc14` |
| click → world | `0x180134928` | `world = (map - offset) / scale` (inverse of the above), then fires `OnMinimapSendInfo` |

So for 龙门寻宝 (`config.ini [config] width=128 scale=0.02`, no offsets):
`tile = floor(world / 6400)` per axis, filenames `0_<Z>_<X>.dds`, and the
Lua driver sets default zoom 2 with 0.5–2.0 range. Note the coordinates the
game feeds the control are its scene/game-world values; the conversion is a
scene-object virtual call (`JX3RepresentX64.dll` `ScenePositionToGameWorldPosition`
wrapper at `0x180b2d8c0` → `scene->vtbl[0x120]`, inverse wrapper `0x180b2a730`),
so mapping them onto `area.tab` coordinates needs the scene vtable implementation
or a landmark fit (open question 5).

## 10. Mark / heat-map structures (static RE)

Recon outputs: `proof/minimap/recon/logic_*_*.txt`
(`JX3LogicEditOperationX64.dll`, image base 0x180000000).

**`KMapMark` record** — derived from `KPlayer::LuaGetMapMark` (`0x1803ea030`)
and `KPlayerClient::OnSyncMapMarkInfo` (`0x18019c120`); allocated 0x60 bytes,
stored in the mark list at `player+0x206a0`:

| offset | field | source packet field |
|---|---|---|
| `+0x30` | `dwID` | `pSync+0x07` (dword) |
| `+0x34` | `nX` | `pSync+0x0b` |
| `+0x38` | `nY` | `pSync+0x0f` |
| `+0x3c` | `nZ` | `pSync+0x13` |
| `+0x40` | `nType` | `pSync+0x17` |
| `+0x44` | `bFlash` | `pSync+0x1b` (byte) |
| `+0x48` | `nFlashTime` | `pSync+0x1c` |
| `+0x4c` | `nCamp` | `pSync+0x20` (byte) |
| `+0x50` | `dwOwnerID` | `pSync+0x21` |

The Lua binding returns exactly these fields (`dwID, nX, nY, nZ, nType,
bFlash, nFlashTime, nCamp, dwOwnerID`).

**Mid-map (team) marks** — `KPlayerClient::OnSyncMidMapMark` (`0x18019c4f0`)
reads `pSync+7/+0xb/+0xf/+0x13/+0x17` (dwords) and a 32-byte name at
`pSync+0x1b`; the client request `DoSyncMidMapMark` (`0x18017b0f0`) builds a
**0x3b (59)-byte packet, type `0x71` (113)**: header, 4 dwords, `char[32]`
name.

**Heat map** — `KScene::UnPackHeatMapBinaryData` (`0x1801591d0`):
- payload = `byGoodNodeCount` good nodes followed by `byEvilNodeCount` evil
  nodes; each `KHEAT_MAP_NODE_SYNC_INFO` is **8 bytes** (assert:
  `(good+evil)*sizeof(KHEAT_MAP_NODE_SYNC_INFO) == nDataSize`, `shl rcx,3`);
- nodes are appended to vectors at `scene+0x21348` (good) / `scene+0x21360`
  (evil); counts stored at `scene+0x20dc4/0x20dc8`;
- the consumer builds the grid with bounds at `scene+0x790/0x794` and a
  128-unit row stride; `GetClientScene():GetMapHeatInfo(mapID)` exposes it to
  Lua (`MiddleMap.tHeatMapInfo[nRegionX][nRegionY]`, `tCampCount`).

**Mark-type icons** (`MiddleMap.lua` table `L69_1`): `nType` -1 →
`RaidTotal.UITex` frame 115; `1..6` → `Money.UITex` frames 11,5,6,7,9,10;
`11..15` → `Money.UITex` frames 85,87,88,89,90. Identity categories:
`PQ=1, QUEST=2, NPC=3, DOODAD=4, MARK=5`. The symbolic `nType` names are
server-side (not in the client tables).

## 11. Addon encryption (solved) and what it revealed

Addon Lua files under `interface\<pack>\...` are encrypted with a custom block
cipher; the ciphertext starts `40 c9 6d 5a`, which decrypts to the Lua
`\x1bLuaQ` header. Recovered from `KGUIX64.dll`:

- key setup `0x180231230` (rcx = ctx, rdx = 16-byte key), block op
  `0x180231ca0` (rcx = 16-byte block, rdx = ctx, in place);
- wrapper `0x1801ae1c0`: for the **whole file including the first 4 bytes**,
  `P_i = op(C_i) XOR C_{i-1}`, `C_{-1} = IV`;
- `key = b"lKT#tOXyaC8lNP!Z"`, `IV = b"SWocY*IW5Hw!sBxL"`
  (`0x1801ae208..0x1801ae246`).

Tool: `tools/addon_decrypt.py` (ctypes calls the client's own functions; no
game process). All 303 addon Lua files were decrypted to
`proof/minimap/addons/<pack>/...` and decompiled where useful
(`proof/minimap/addons/JX/decompiled/`, `proof/minimap/addons/decompiled/`).

Findings: the draggable battlefield map is **base UI**
(`UI/Config/Default/BattleField/BattleFieldMap.*`, §7.6); the JX addon's
`JX_Moba` module only injects dragon-timer widgets into
`Normal/BattleFieldMap` and reads `BattleMinimap2.UITex` frames; `LM_!Base`
and `MY_!Base` only consume `Table_Is*BattleFieldMap`.
