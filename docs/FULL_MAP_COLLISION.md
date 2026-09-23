# Full map collision from the real client data - how it was achieved

Date: 2026-09-22
Branch: `feature/real-map-collision`
Map used as the test case: 龙门寻宝 (desert / 玉门关 / 楼兰)

## Goal

Make every structure on the map collide in our engine host using **only the
game's own local files** - no hand-authored shapes, no guesses. The map host
(`engine_host_spike/map_spike_host.exe`) loads the real KG3D engine and renders
the real map, so the collision must come from the same data the client uses.

## Final result

- Runtime collision list: **5,351 instances / 699 meshes**
  - terrain heightfield (real engine collision, already present)
  - **4,963 world objects** (walls, buildings, rocks, props, trees) from
    `entities/sceneinfo_full/%03u_%03u.json`
  - **394 foliage solids** (cactus, rocks, deadwood) from the `.foliage` files
- Verified in game by walking into each class of object (logs + screenshots in
  `proof/map_spike/structure_collision/`).

## Phase 1 - foliage (cactus / rocks / deadwood)

1. The map's obstacles are foliage instances: 13 region files
   `foliage/foliageinfo/%03u_%03u.foliage` (magic `FOLI`).
2. Decoded the binary format by disassembling `SceneManagerx64.dll`:
   - `SceneFileLoader_Foliage_Binary::LoadInstances` @0x180020700
   - `SceneFileLoader_Foliage_Binary::ReadInstanceData` @0x1800213a0
   - readers `ReadUint` @0x1800219a0, `ReadUint1` @0x180021a40,
     `Readfloat` @0x180021ae0
   - `FoliageRegion::LoadFromFile` / `ProcessInstances` @0x180013c20 shows the
     region offset is added to x and z only, and the cell bias vec3 is
     `(cellX*400, baseZ, cellY*400)`
3. Format (v1, flags=4): 40-byte header, 72-byte unit heads, per-unit cell
   entries `{u16 cell, u8 count}`, then bit-packed variable-length instance
   records; the packed `u34` field holds the **pattern id** in its low 15 bits
   and the texture index above it.
4. Decoder: `tools/decode_foliage.py` (byte-exact: all 13 files parse to the
   exact file length with matching instance counts).
5. Patterns (from `foliage/龙门寻宝_foliageinfo_editor.json`): 2/3 grass
   (.srt, no mesh), 4 deadwood, 5 cactus, 6/7 rocks.
6. Export + runtime: `tools/export_foliage_collision.py` ->
   `foliage_collision.bin` (v1); `engine_host_spike/FoliageCollision.cs` does
   capsule-vs-triangle collision with step-up and support.
7. Verified: player blocked by cacti (13 events; stops at z=53278 before the
   cactus at z=53371), steps onto rock boulders (ground=3468 vs terrain 3453).

## Phase 2 - the map's world objects (walls / buildings / props)

The map's `.jsonmap` declares `worldObjectCount: 5075`, but the obvious
`entities/*.json` region files are 67-byte empty stubs, and the engine log
shows `_LoadFileData` failures while loading them - so the objects do not come
from there.

Found the real path by reading the engine instead of guessing:

- `SceneManagerx64::SceneFileLoader_Jsonmap::OnSyncLoad` builds the region file
  template `%s%s\entities\%%s_full\%%03u_%%03u.json` (init at 0x18004ca5a,
  template stored at loader+0x250) and fills it at 0x18004b7e3-0x18004b816:
  the format string is at 0x180069288 and the `%s_full` placeholder is filled
  with the literal string **"sceneinfo"** (0x180069158).
- => the real files are `entities/sceneinfo_full/%03u_%03u.json`.
- All 64 region files ship: 11.9 MB, **4,965 objects** (4,777 mesh objects +
  186 SpeedTree `.srt` + 2 misc).

Each object carries everything needed:

```
comRender.actorModel          mesh path (data\source\maps_source\...)
comBasic.actorLocalMatrix     4x4 row-major, translation in row 3
comBasic.actorBoundBoxMin/Max world-space bounds
comLogic / comCustomInfo      (no explicit physics flags: static objects)
```

Models include 城墙 (`cq_龙门城墙*`, `cq_玉门关城墙*`), 建筑
(`jz_xb楼兰三间房/哨台/玉门关建筑`), 石头 (`st_xb龙门荒漠石`), 栈道/工地物件,
props (jars, boxes, tables, fire basins) - 589 distinct meshes.

Pipeline:

- `tools/export_structure_collision.py` reads the region JSONs, extracts all
  distinct meshes from the pak, parses them and writes
  `structure_collision.bin` (v2: meshes + instances with full 4x4 matrices and
  world AABBs).
- `FoliageCollision.cs` loads both files into one instance list, builds a
  per-mesh triangle grid, and measures capsule/triangle distances **in world
  space through the instance matrix** (exact for anisotropic scales).

Verified: blocked at a 楼兰三间房 building (19 blocked events), blocked at
wooden scaffold posts.

## Phase 3 - SpeedTrees

Trees are `.srt` world objects. The physics engine's rule for them:

- `PhysicsEngineX64::_GetCollisionGeometryFilesFromFile` handles `.srt`/`.st`
  at 0x18002455b: it appends **".CollisionMesh"** to the file's base name and
  uses that as the collision geometry (verified in the disassembly:
  strncpy of ".CollisionMesh" + `KG3D_ConvertToStandardHashString`).
- Those files ship next to the trees, e.g.
  `Data/source/maps_source/树/S_xb多枝枯树003_001.CollisionMesh`
  (28,962 bytes, a regular HSEM mesh) - used verbatim (118 trees).
- 68 trees ship only a **degenerate fragment** (e.g. a 26x37x7 box for a
  1731x1125x1639 tree). Each tree also ships its **visual mesh**
  (`<base>.mesh`); for those 68 the collider is **measured** from it:
  convex hull (monotone chain) of the geometry within 250 world units of the
  base, extruded into a capped prism - the actual low trunk/branch
  cross-section. 62 of 68 measured; 6 small `s_xca小树` trees ship no visual
  mesh under the expected name and stay walk-through (as in the real client).

Verified: blocked at a shipped trunk collider (player stops at 25868/23602
against trunk AABB x25810..25902 z23592..23674 - exactly the capsule radius
away) and at a measured collider (28962/24728 area, 21 blocked events;
collider = 16-vertex prism, local XZ 311x350, height 255).

## Formats (reference)

### .foliage v1

```
header 40 B:  u32 'FOLI', u32 version=1, u32 fileLength,
              u32 unitCount, u32 totalInstances, u32 flags
unitCount x 72 B heads: GUID(38B) +0x40 f32 baseZ, +0x44 u32 cellCount
per unit:     flags&4 -> u32 pattern mask
              cellCount x { u16 cell (lo=cellX, hi=cellY); u8 count }
              per cell: count x instance records
instance:     u32 flags; x,y,z = Readfloat(mode 0,2,4 /100);
              normal = 3 x (u8/100-1); scale = 1 or 3 x Readfloat(8,0xa,0xc /100);
              RandRotation (2 x u8/255*2pi); u34 = patternId | texture<<15;
              Readfloat(0x15 /10); bit23/27 skip 1; bit28 skip 0x40;
              ReadUint1(0x1d) (selector 0 consumes nothing); bit31 +4
position:     (x, y=height, z); cell bias (cellX*400, baseZ, cellY*400);
              region origin (-102400 + ix*51200, -102400 + iz*51200)
```

### structure_collision.bin (v2)

```
u32 'FCOL', u32 version=2, u32 meshCount, u32 instanceCount
mesh:     u32 vertCount, u32 triCount, f32 verts[3V], u32 tris[3T]
instance: u32 meshIndex, f32 m[16] (local->world, row-vector w = l*M),
          u32 pad, f32 bboxMin[3], f32 bboxMax[3] (world)
```

## Runtime collision design (`engine_host_spike/FoliageCollision.cs`)

- Player = vertical capsule (radius 25, height 170 units; tune with
  `MAP_PLAYER_RADIUS` / `MAP_PLAYER_HEIGHT`).
- Spatial hash of instances (800-unit cells) + per-mesh triangle grid (CSR).
- Capsule samples are transformed into mesh local space; the distance to the
  closest triangle is measured **in world space** (`d_world = d_local * M3`),
  so anisotropic instance scales are exact. Normals come back through the same
  transform.
- Side contacts push out with sliding; contacts with an up normal set the
  stand-on ground; low obstacles (top within 70 units) are stepped onto;
  `SupportHeight` probes the highest up-facing surface under the player.
- Switches: `MAP_FOLIAGE_COLLISION=0`, `MAP_STRUCTURE_COLLISION=0`.

## Verification (proof files)

`proof/map_spike/structure_collision/`:
- `cactus_block.log/.png` - foliage blocking
- `structure_block.log/.png` - building + scaffold posts
- `tree_block.log/.png` - shipped trunk collider
- `tree_measured.log` - measured collider for a formerly degenerate tree
- `final_run.log`, `foliage_collision.bin`, `structure_collision.bin`

In-game controls used for testing: `C` teleports in front of the nearest
structure, the window title shows `BLOCKED` while pushing into something.

## Regenerating the collision data

```
python tools/decode_foliage.py C:\jx3tmp\foliage_dump\*.foliage   # sanity check
python tools/export_foliage_collision.py
python tools/export_structure_collision.py
```

Both exporters copy their output to
`C:\SeasunGame\MovieEditor\bin64\collision_data\`.

## Commits

- `6c63985` Structure collision: decode real .foliage data, foliage meshes
  block the player
- `a6b40e9` Structure collision: use the map's real world objects
  (walls/buildings/props)
- `102443e` SpeedTree collision: use the game's baked .CollisionMesh files
- `07c2918` Tree collision: use exactly the shipped CollisionMesh colliders
- `f5460b6` Tree collision: measure degenerate-tree colliders from their
  visual meshes

## Multi-map baking (any map, one command)

```
python tools/bake_map_collision.py --map <mapname> --copy-to C:/SeasunGame/MovieEditor/bin64/collision_data
```

What it does (all from the pak, nothing authored):
1. reads `entities/<map>_sceneinfo.json` for `RegionTableSize` (e.g. 8x8, 4x4)
2. extracts and decodes `foliage/foliageinfo/%03u_%03u.foliage`
3. extracts the foliage pattern table + the four pattern meshes
4. extracts `entities/sceneinfo_full/%03u_%03u.json` (the map's world objects)
5. writes `<map>_foliage_collision.bin` and `<map>_structure_collision.bin`

The host picks up per-map files automatically:
`bin64/collision_data/<map>_foliage_collision.bin` /
`<map>_structure_collision.bin`, falling back to the generic names.

Collision data is baked **offline** - the host never generates it at run
time; it only parses the bins at startup (龙门寻宝: 5,351 instances / 699
meshes load in ~0.3 s after the map itself loads).

### Status per map

- 龙门寻宝: complete - 4,963 world objects, 394 foliage solids, trees with
  the shipped CollisionMesh plus 62 measured from visual meshes.
- 海岛绝境 (4x4 regions): world objects baked (4,177 instances / 245 meshes,
  43 MB) and 15 foliage files decoded. Note: 758 of its trees ship degenerate
  CollisionMeshes AND no sibling visual `.mesh` (the .srt is billboard-only),
  so those trees stay walk-through unless the SpeedTree `.srt` geometry is
  decoded (open item). The rest of the map (buildings, walls, rocks, props)
  is covered.

### Baked maps (2026-09-22)

| map | regions | foliage files | world objects | structure bin | trees |
|---|---|---|---|---|---|
| 龙门寻宝 | 8x8 | 13 | 4,963 | 55.0 MB / 622 meshes | 118 shipped + 62 measured |
| 龙门寻宝_夜晚 | 8x8 | 13 | 4,963 | 55.9 MB / 709 meshes | 83 degenerate, 73 measured |
| 白龙绝境 | 8x8 | 60 | ~5,017 | 75.9 MB / 822 meshes | 452 degenerate, 168 measured |
| 天原绝境 | 8x8 | 31 | ~5,700 | 21.9 MB / 535 meshes | 324 degenerate, 259 measured |
| 海岛绝境 | 4x4 | 15 | ~4,000 | 43.6 MB / 245 meshes | 758 degenerate, 0 measured (no visual meshes) |

All baked with:
`python tools/bake_map_collision.py --map <name> --copy-to C:/SeasunGame/MovieEditor/bin64/collision_data`

Runtime load verified for 龙门寻宝 (5,351 inst), 龙门寻宝_夜晚 (5,521),
白龙绝境 (5,377), 天原绝境 (6,672) - each loads its per-map bins.
