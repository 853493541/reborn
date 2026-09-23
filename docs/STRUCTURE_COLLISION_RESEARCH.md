# Structure collision research — how the game handles houses/walls

Date: 2026-09-22
Branch: `feature/real-map-collision`

Question: how does the real client give houses/walls/rocks collision, and can we
reuse it?

## 1. Engine pipeline (verified from binaries)

PhysicsEngineX64.dll contains the full static-scene collision system:

| Stage | Symbol | Notes |
|---|---|---|
| Scene file descriptor | `CreateSceneFileDataLoader` → `_TryToCreateJsonSourceFileLoader` | builds `%s\entities\%s_sceneinfo.json` |
| Per-region object list | `SceneFileDataLoader_JsonSource::LoadRegionData` → `_LoadRegionFileData` | `%hs\entities\sceneinfo\%03u_%03u.json` |
| Scene manager | `StaticPhysicsSceneManager::LoadFromFile` / `UpdateScene` | manager vt[15] product; streams regions around the player |
| Region manager | `SceneRegionManager::_CreatePxActorInRegion` | quad-tree + streaming |
| Actor creation | `PhysicsSceneActor::_CreatePxActorFromMdlFile` / `_CreatePxActorFromMeshFile` | `PhysicsSceneActor::AddToPxScene` |
| Collision geometry | `_GetCollisionGeometryFilesFromFile` (`srt`/`st`/`mdl`/`group`/`prefab`/`stree`) → `_GetCollisionGeometryFilesFromMdlFile` / `...FromGroupFile` (`proxymesh`, `CollisionMesh`, `mesh`) | `_CreateCollisionDataFromFile` → PhysX `PxTriangleMesh` / `PxConvexMesh` / `PxHeightField` |
| Per-object flags | `comLogic.obstacleOption`, `comLogic.enablePhysicsConfig`; `bUnitWalkable`, `bUnitCanPass`, `bBulletWalkable`, `bBulletCanPass`, `bAutoPathing` | `KG3DSceneResponse` |

`worldObjects` JSON schema (from `UnserializeEditorJsonStructure`):
```json
"worldObjects": {
  "{uuid}": {
    "comBasic":  { "actorLocalMatrix":[16], "actorBoundBoxMin":[3], "actorBoundBoxMax":[3] },
    "comEditor": { "templateFile":"...mdl/.srt/.prefab" },
    "comLogic":  { "obstacleOption":0, "enablePhysicsConfig":0 },
    "_tableName":"...", "_tableType":"..."
  }
}
```
So the engine's intended structure collision = `worldObjects` → model collision
meshes (`proxymesh`/`CollisionMesh`) → PhysX actors.

## 2. What the shipped client data actually contains

Extracted with the official `PakV4SfxExtract.exe`:

- `landscape/heightmap/*.r32` + `hole/*.hlb` → terrain heightfield (used by our
  TerrainSampler). **Terrain only** — verified 长安 heightfield is smooth rolling
  terrain, no building plateaus.
- `landscape/regioninfo/<map>_%03u_%03u.json` → materials/LOD factors only.
- `<map>.SRScene` → **interactive props only** (`.state` → `.mdl`), e.g.
  长安: 96 guards/wolves; 雁门关之役: 257 mechanisms; 太原: 171 props.
  Record layout: header 320 (长安 324), stride 320 (长安 324), position/scale/quat
  at +204/+216/+228, path at +264 (NUL-terminated, may exceed 56 bytes into the
  next record's unused padding).
- `entities/<map>_sceneinfo.json` → descriptor only (region size/origin).
- `entities/sceneinfo/%03u_%03u.json` → **`"worldObjects": {}` for all 150 maps
  scanned** (1714 region files, all ≤100 bytes).
- `entities/separate_full/globalObjects.json` → only global configs
  (view probes, sky, wind, GDEnv). `GDEnvObjects.json` similar.

Also verified:
- The MovieEditor editor engine **never creates a PhysicsScene** for the loaded
  map: a full process memory scan (1.3 GB, vtables RVA `0xFA7B8` scene,
  `0xFCFE0` terrain, `0xFCCA8` dyn loader) found zero objects. So our host has
  no hidden engine-side structure collision to query.
- `KG_OpenDirectory`/`KG_ReadDirectory` in KGCommonX64.dll use `FindFirstFileW`
  on disk, not the PakV4 VFS — cannot list pak contents.
- Pak `.idx` files are compressed/hashed; no path names.

## 3. Conclusion

In the shipped client data we can inspect, **structures (houses/walls) have no
client-side placement list**. The renderer draws them (baked into the landscape
render mesh `landscape/terrain.inspack` and/or server-streamed scene entities),
while the engine's `worldObjects` collision path has no data. In the live game
these objects are scene entities streamed by the server; the client cooks their
collision from the model's `proxymesh`/`CollisionMesh` meshes and adds it to
the PhysX scene via `PhysicsSceneActor::AddToPxScene`.

Therefore offline structure collision must be **generated from the rendered
geometry**, not read from a collision file.

## 4. Options for our host

1. **Landscape render mesh → collision (best coverage).**
   Extract `landscape/terrain.inspack` (baked landscape render mesh, likely
   includes baked structures), parse its mesh(es), build a BVH/triangle set and
   add capsule-vs-triangle collision to the player (plus the existing
   heightfield). Requires reversing the `inspack` container.
2. **Hook the engine's model loader** (`KG3D_LoadFile` / model factory) inside
   the host to enumerate every mesh the map actually renders, then cook
   collision from those meshes (or their bboxes). Gives exact coverage incl.
   server-streamed objects the host renders.
3. **Interactive-prop collision from `.SRScene`** (gates/barrels): parse
   placements, load `.state` → `.mdl` collision mesh, add colliders. Small but
   fully data-driven and directly usable now.
4. **`worldObjects` support** (future maps/editor projects): implement the JSON
   path with AABB colliders from `actorBoundBoxMin/Max` and `obstacleOption` /
   `enablePhysicsConfig` flags.

Recommended order: 2 (exact, uses what is rendered) → 1 (offline, robust) →
3 → 4.

## 5. Evidence files

- `engine_host_spike/recon_physics*.txt` — PhysicsEngine API/vtable map
- `engine_host_spike/recon_sceneloader*.txt` — scene file loader paths
- `engine_host_spike/recon_entityloader.txt` — SceneManagerx64 entity loader
- `proof/map_spike/entities/` — extracted sceneinfo JSONs (empty worldObjects)
- `proof/map_spike/sceneinfo.json`, `*_landscapeinfo.json`

## 6. SOLVED: foliage instance format + working collision (2026-09-22)

The 5075 `worldObjects` from `龙门寻宝.jsonmap` are NOT shipped in the pak
(`entities/` region files are empty stubs; only `separate_full/globalObjects.json`
exists, and it holds just 96 view probes). For this map the real obstacles are
the **foliage system** instances: rocks/cactus/deadwood are foliage patterns
with real `.mesh` geometry.

### 6.1 `.foliage` v1 format (SceneManagerx64.dll, verified byte-exact)

Loader: `SceneFileLoader_Foliage_Binary::LoadInstances` @0x180020700,
`ReadInstanceData` @0x1800213a0, readers `ReadUint`/`ReadUint1`/`Readfloat`
@0x1800219a0/0x180021a40/0x180021ae0 (vtable @0x180063960).

```
header 40 B:  u32 magic 'FOLI', u32 version=1, u32 fileLength,
              u32 unitCount, u32 totalInstances, u32 flags
unitCount x 72 B heads:  GUID(38B) ... +0x40 f32 baseZ, +0x44 u32 cellCount
per unit:     flags&4 -> u32 pattern mask
              cellCount x { u16 cell (lo=cellX, hi=cellY); u8 count }
              then per cell: count x instance records
instance:     u32 flags; x,y,z = Readfloat(mode 0,2,4 /100);
              normal = 3 x (u8/100-1) [bit26] or 2 bytes + derived z;
              scale = 1 or 3 x Readfloat(mode 8,0xa,0xc /100);
              bit14 -> rotation extra; u34 = ReadUint(0xf) | ReadUint(0x11)<<15
              (low 15 bits = FoliagePatternID, high = texture index);
              Readfloat(0x15 /10); bit23/27 skip 1; bit28 skip 0x40;
              ReadUint1(0x1d) (selector 0 consumes nothing); bit31 +4
position:     engine coords (x, y=height, z); cell bias
              (cellX*400, baseZ, cellY*400); region origin
              (-102400 + ix*51200, -102400 + iz*51200)
              (ProcessInstances @0x180013c20 adds region offset to x,z)
orientation:  normal = up vector (~+Y); +0x24 RandRotation = 2 angles
              (byte/255*2pi); yaw used for collision
```

Decoded result for 龙门寻宝 (13 region files): 4028 instances, pattern IDs
2/3 = grass (.srt, no mesh), 4 = deadwood, 5 = cactus, 6/7 = rocks
(`foliageinfo_editor.json` table). 394 solid instances.

### 6.2 Pipeline + runtime collision

- `tools/decode_foliage.py` - .foliage parser (byte-exact, count-checked)
- `tools/export_foliage_collision.py` - writes `foliage_collision.bin`
  (meshes + instances) to `engine_host_spike/collision_data/` and bin64
- `engine_host_spike/FoliageCollision.cs` - capsule vs triangle-mesh with
  spatial hash, yaw + uniform scale, side push-out, step-up (<=70u) onto
  walkable rock tops, SupportHeight ground probe
- `engine_host_spike/MapSpike.cs` - player mode integrates it (env
  `MAP_FOLIAGE_COLLISION=0` disables, `MAP_PLAYER_RADIUS/HEIGHT` tune capsule)

### 6.3 Verified in game

- cactus cluster (62724, 53206): player walks +z, stops at z=53278 before the
  cactus at z=53371, `blocked=True` (13 events) - proof:
  `proof/map_spike/structure_collision/cactus_block.log`
- rock boulder (104424, 180840): player steps up onto the boulder,
  `ground=3468` vs terrain 3453 (step-up), walks off normally
- regenerating: `python tools/export_foliage_collision.py`

## 7. SOLVED: the map's real world objects (walls/buildings/props)

The per-region object files the engine loads are
`<map>/entities/sceneinfo_full/%03u_%03u.json` - the scene loader template is
`%s%s\entities\%%s_full\%%03u_%%03u.json` (SceneManagerx64 OnSyncLoad builds it
with the placeholder filled by the string "sceneinfo"; verified in the
disassembly at 0x18004b7e3-0x18004b816: fmt string at 0x180069288, arg
"sceneinfo" at 0x180069158).

All 64 region files exist for 龙门寻宝 (11.9 MB total, 4965 objects:
4777 mesh objects + 186 SpeedTree .srt + 2 misc). Each object carries:

  comRender.actorModel          mesh path (data\source\maps_source\...)
  comBasic.actorLocalMatrix     4x4 row-major, translation in row 3
  comBasic.actorBoundBoxMin/Max world-space bounds
  comLogic / comCustomInfo      (no explicit physics flags: all are static)

Models include 城墙 (cq_龙门城墙/玉门关城墙), 建筑 (jz_xb楼兰三间房/哨台/
玉门关建筑), 石头 (st_xb龙门荒漠石), 栈道/工地物件, props (jars, boxes,
tables), etc.

Pipeline:
- `tools/export_structure_collision.py` reads the region JSONs, extracts all
  589 distinct .mesh files from the pak, parses them and writes
  `structure_collision.bin` (v2 format: meshes + instances with full 4x4
  matrices and world AABBs). Run: `python tools/export_structure_collision.py`
- `engine_host_spike/FoliageCollision.cs` now loads both
  `foliage_collision.bin` (v1) and `structure_collision.bin` (v2) into one
  instance list (5171 instances, 593 meshes), with per-mesh triangle grids and
  exact world-space capsule/triangle distances through the instance matrix
  (handles anisotropic scale). Step-up/support logic unchanged.

Verified in game: at a 楼兰三间房 building (15007, 25400 area) the player is
blocked (19 blocked events); wooden 栈道 posts stop the player at (14900, 25900).
Proof screenshot: bin64/map_spike_out/map_t25000ms.png (wooden posts).

## 8. SpeedTree collision: the baked .CollisionMesh files

PhysicsEngineX64.dll `_GetCollisionGeometryFilesFromFile` handles the tree
extensions: for `.srt`/`.st` it builds `<base>.CollisionMesh` from the file
name and uses that as the collision geometry (verified in the disassembly at
0x18002455b-0x1800245cd: strncpy of ".CollisionMesh" after the base name, then
KG3D_ConvertToStandardHashString).

Those files ship in the pak next to the .srt, e.g.
`Data/source/maps_source/树/S_xb多枝枯树003_001.CollisionMesh` (28,962 bytes).
They are regular HSEM meshes (parse with mesh.py).

tools/export_structure_collision.py now maps every `.srt` object to its
`.CollisionMesh` sibling, so the export covers all 4,963 objects
(4,777 .mesh + 186 .srt) with 622 meshes.

Note: the shipped collision meshes vary - 118/186 trees have a real trunk
collider standing on the ground; the rest are small/buried boxes (the real
game lets the player walk there too). Verified in game: blocked at a tree
trunk (26144/23648 area, trunk world AABB x 25810..25902 z 23592..23674,
player stopped at z=23610).

Final coverage: 5,357 collision instances / 626 meshes
(structures 4,963 + foliage 394), all from the game's own local files.
