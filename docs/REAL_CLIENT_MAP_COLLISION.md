# Real client map collision — recon + working host probe

Date: 2026-09-21
Branch: `feature/real-map-collision`

Goal: build the map collision system from the **real game components** (same
approach as the rendering workstream hosting MovieEditor engine DLLs), after
understanding how the client does it.

## 1. Architecture (verified against binaries + configs + logs)

| Layer | Component | Evidence |
|---|---|---|
| Physics middleware | PhysX 3.3.4 (`PhysX3_x64.dll`, `PhysX3Common_x64.dll`, `PhysX3Cooking_x64.dll`, `PhysX3CharacterKinematic_x64.dll`) | imports of `PhysicsEngineX64.dll`; embedded `physx334` paths |
| Map collision loader | `PhysicsEngineX64.dll` (game) / `PhysicsX64.dll` (editor build) — identical 5 C exports | PE export tables |
| Scene bridge | `KG3DEngineAdapterX64.dll` loads `PhysicsEngineX64.dll`, `GetPhysicsManager`, then `manager->vt[0] Init` / `vt[2] SetWorkingDir` | adapter disasm `recon_adapter_init.txt` |
| Gameplay world | `SIMWorldX64.dll` (`CreateSIMWorld`, `PxWorld::RayCast`, `PxWorld::GetFloorHeight`) imported by `JX3RepresentX64.dll` (also `QueryCollisionPosition`) | PE exports/imports |
| Nav | `NAVX64.dll` (`CreateNAV`, `PathEngineNavi::FindPath`) + `PathEngine.dll` | PE exports |
| Config | `config.ini:199-201` / `EngineStaticConfig.ini:12-17`: `bEnableSceneCollision=1`, `bUseLODMeshCollision=1`, `bAddPlayerPhysicsActor=0`, `bCreateInternalPhysXScene=1`, `DrawPhysicsObstacle=0` | config files |

### Map data formats
- `data\source\maps\<map>\<map>.jsonmap` → describes quality dirs; collision lives in
  `landscape/<map>_landscapeinfo.json`.
- Terrain height map: 16-bit PNG (`KG3DHeightMap::LoadPngFile` requires
  `bit_depth == 16`); terrain holes in `hole/%03u_%03u.png` or `.hlb`
  (`KG3D_PhysxTerrainDataLoader_Source::_LoadHoleFromPNG/_LoadHoleFromHLB`),
  height regions decoded by `_LoadHegihtRegionR32` / `_LoadHegihtRegionBCH`.
- Objects: collision geometry resolved from `.mdl`/`.group`/`.prefab`/`.srt`/`.stree`
  files (`PhysicsEngine::_GetCollisionGeometryFilesFrom*`), mesh entries matched by
  `mesh` / `CollisionMesh` / `proxymesh`; cooked via `CreateCacheShapeFromFile`.
- Per-object semantics: `bUnitWalkable`, `bUnitCanPass`, `bBulletWalkable`,
  `bBulletCanPass`, `bAutoPathing`.

## 2. PhysicsEngineX64.dll API map (image base 0x180000000)

`GetPhysicsManager(&manager)` returns the static manager (singleton at RVA
`0x11B5D0`, vtable RVA `0xFA6B0`).

PhysicsManager vtable (relevant entries):
| idx | RVA | method |
|---|---|---|
| 0 | 0xF860 | `Init(rdx, r8=fileSystem)` → `_InitPhysX` (idempotent only if not initialised; **calling it after the engine init destroys state**) |
| 2 | 0x109B0 | `SetWorkingDir(const char* root)` → creates Semantic/Lua-backed file system when absent |
| 14 | 0x108B0 | `CreatePhysXTerrain(PhysicsTerrain** out, Config* cfg)` |
| 15 | 0x10930 | `CreatePhysicsSceneDynamicLoader(out, cfg)` (`StaticPhysicsSceneManager`, object streaming) |
| 16 | 0x10460 | `CreatePhysicsScene(out, engineArg)` (needs a real engine-side arg; do not pass 0) |

`Config` = 16 bytes: `{int nPreLoadSize; int nForceLoadSize; int nUpdateDelta; int nMaxCacheCount}`,
asserts `nPreLoadSize >= nForceLoadSize > 0`, others `>= 0`.

C exports:
- `CreatePhysicsTerrainDataLoader(const char* pszTerrainPath, const char* opt, out Loader*)`
  (accepts the `.jsonmap` path; derives `landscape/<name>_landscapeinfo.json`).
- `CreatePhysicsTerrain`, `CreatePhysicsSceneDynamicLoader`, `CreateSceneFileDataLoader`.

PhysicsTerrain vtable (`0xFCFF0`):
| idx | RVA | method |
|---|---|---|
| 2 | 0x2DAB0 | `UpdateTerrain(float3 pos)` — streams regions around `pos` (force/preload ranges) |
| 3 | 0x2DC60 | low-level `LoadTerrain(desc)` (desc setup only) |
| 5 | 0x2DF90 | **full `LoadTerrain(pszPath, opt, ownerPtr)`** — creates its own data loader, reads `landscapeinfo.json`, builds region table + region manager + PhysX actors |
| 12 | 0x2E890 | `GetTileBBox` |

Terrain data loader vtable (`0xFD2C0`): `vt[2] GetTerrainDesc(out 32 bytes)`,
`vt[3] LoadRegion(x, z, ...)`, `vt[4] LoadHoleRegion(x, z, byteArray, size, outFlag)`.

Region table: `terrain+0x24` = region count X, `+0x28` = count Y,
`+0x48` = array of 0x30-byte entries (`+0` type, `+8` region data object pointer).
`type == 2` = loaded. Terrain descriptor (observed for 龙门寻宝):
`tileSize=512, regions 8x8, scale 100,100, origin -102400,-102400`.

## 3. Working probe (proven)

`engine_host_spike/MapSpike.cs` gained `MAP_COLLISION_PROBE` mode. It runs inside
the existing map host after `LoadMap`, which means:

- the engine already loaded `PhysicsEngineX64.dll` and initialised the manager
  (`Physics Engine initialize success` in the engine log),
- the Semantic/Lua file system is up, so the loader can read the client PakV4
  through the VFS (no asset extraction needed).

Verified sequence and results (map 龙门寻宝):

```
GetPhysicsManager                     -> mgr 0x...03B5D0 (vtable rva 0xFA6B0)
Init                                   -> SKIPPED (foundation already present)
CreatePhysXTerrain(out, cfg)           -> hr=0
CreatePhysicsTerrainDataLoader(jsonmap)-> ok=1  (loader reads landscapeinfo.json)
loader vt[2] GetTerrainDesc            -> 512, 8, 8, 64, 100f,100f, -102400f,-102400f
terrain vt[5] LoadTerrain(path,0,mgr)  -> ok=1  (region table 8x8 allocated, region mgr created)
terrain vt[2] UpdateTerrain(pos) x40   -> region[20] type=2 (tile containing 147463,5231,49911)
```

Run:

```powershell
$env:MAP_COLLISION_PROBE='1'; $env:MAP_PROBE_ONLY='1'; $env:MAP_PROBE_DEEP='1'
& "C:\SeasunGame\MovieEditor\bin64\map_spike_host.exe"   # cwd must be C:\SeasunGame\MovieEditor
# log: C:\SeasunGame\MovieEditor\bin64\map_spike_out\map.log
```

Rebuild host (source `engine_host_spike/MapSpike.cs`):

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /target:exe `
  /out:"C:\SeasunGame\MovieEditor\bin64\map_spike_host.exe" `
  /r:"C:\SeasunGame\MovieEditor\bin64\MovieEngineCLR.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll `
  engine_host_spike\MapSpike.cs
```

Env switches: `MAP_PHYS_DLL` (default game `PhysicsEngineX64.dll`),
`MAP_PROBE_SCENE=1` (attempt `CreatePhysicsScene`, needs engine arg — off by default),
`MAP_PROBE_POS=x,y,z` (streaming centre), `MAP_PROBE_DEEP=1` (full load + streaming).

## 4. Recon tooling added

- `tools/recon_physics.py` — exports + first-pass disasm.
- `tools/recon_physics2.py` — vtables of factory products + function-name xrefs.
- `tools/recon_physics3.py` — imports + manager vtable + pointer-slot xrefs.
- `tools/recon_physics4.py` — manager singleton + brute-force RIP xrefs.
- `tools/recon_physics_funcs.py` — function map by embedded KGLOG names.
- `tools/dump_va.py` — range disassembler with string/IAT resolution.
- Outputs kept in `engine_host_spike/recon_physics*.txt`, `recon_adapter_*.txt`,
  `recon_terrain_*.txt`, `recon_regionmgr.txt`.

## 5. Open work (next milestones)

1. **Height/ray query**: regions hold `KG3D_PhysxTerrainData` objects that add PhysX
   actors (`_CreatePxActor` → `0x180011910`). Next step is to locate the `PxScene`
   used by the region manager and expose `PhysicsScene::SweepEx` (scene vtable RVA
   `0xFA7B8`, `SweepEx` at vt[16] = RVA `0x1C5B0`) or PhysX raycast as the query API.
2. **Immediate fallback oracle**: `KGSceneCLR.SetCameraPos(x, high, z, false)` snaps
   Y to engine terrain height — usable today to cross-validate streamed region data.
3. **Object collision**: drive `StaticPhysicsSceneManager` (`manager vt[15]`,
   `LoadFromFile` for `.srt`/`.prefab`) to add static object actors to the same scene.
4. **Service wrapper**: expose the query as a small API (pipe/HTTP) for the viewer,
   replacing the web sidecar collision with real game components.

## 6. Player mode — small jumping person (working)

`engine_host_spike/run_player.cmd` launches the map host in player mode:
a small 花萝 actor (scale 0.5) stands, walks and jumps on the real map using
heights read straight from the game's terrain data loader.

Physics implementation (`TerrainSampler` in `MapSpike.cs`):
- `CreatePhysicsTerrainDataLoader(mapPath)` → `loader vt[2] GetTerrainDesc`
  (size 512, 8x8 regions, cell 100, origin -102400,-102400).
- `loader vt[3] LoadRegion(ix, iz, float* buf, (size+1)^2, ...)` loads the raw
  float height grid for a region (`ok=1`, `outC=513`); the sampler caches one
  region (1 MB) and bilinearly interpolates heights at any world (x, z).
- Player simulation: gravity `-1289 cm/s²` (12.89 m/s²), jump `703 cm/s`
  (~192 cm apex), walk `200`, run `667` — the real game values from
  `settings/JumpParam.tab` + `Represent/common/number.krl.txt`
  (see `docs/JX3_GRAVITY_RESEARCH.md`); landing snaps to the sampled ground height.
- Actor is repositioned each frame with `RemoveDummyModel` + `AddDummyModel`
  (map dummy-model system); follow camera keeps it framed.

Controls: `W/S/A/D` walk (always moves the character, camera-relative),
`Shift` run, `Space` jump, `F` toggle **FOLLOW** (third-person camera, default)
/ **FREE** camera, wheel = follow distance in FOLLOW mode. In FREE mode the
camera moves with arrow keys / `Q` / `E` (plus right-drag pan, Alt+right orbit,
wheel zoom). Env: `MAP_PLAYER_SCALE`, `MAP_PLAYER_SPAWN`, `MAP_PLAYER_JUMP`,
`MAP_PLAYER_GRAVITY`, `MAP_PLAYER_SPEED`, `MAP_PLAYER_RUN`, `MAP_PLAYER_CAM=1`
(first person), `MAP_PLAYER_DEMO=1` (auto walk + jump with screenshots).

Follow camera: every 500 ms it re-measures the camera view direction (nudge
forward 3 frames) and places the camera at `player - dir*followDist` with a
terrain-height clamp, so the character stays centred after the user orbits
the camera.

### Object / steep-terrain collision (added)

Movement no longer teleports over terrain: the player samples height with a
40 cm look-ahead and blocks when the ground would rise more than 70 cm
(≈60° max climbable slope), sliding along X or Z when possible; walking off a
ledge > 150 cm starts a fall. This makes the rock formations/mesas block the
character instead of letting them walk through/up.

Note on map objects: 龙门寻宝's static scene files
(`entities/<map>_sceneinfo.json` + `entities/sceneinfo/%03u_%03u.json`) contain
`"worldObjects": {}` — the HD map ships **no separate PhysX static objects**.
The visible rocks/ruins are baked into the landscape/heightfield, which is why
terrain-height collision plus slope blocking covers them. Maps that do ship
`worldObjects` can be handled later via
`CreateSceneFileDataLoader` + `StaticPhysicsSceneManager::LoadFromFile`
(scene file `.../entities/<name>_sceneinfo.json`, per-region files
`.../entities/sceneinfo/%03u_%03u.json`) into a `PhysicsScene` created with
`manager vt[16] CreatePhysicsScene(&scene, &desc)`.


Proof: `proof/map_spike/player/follow.png` (standing on a real dune),
`jump_apex.png` (mid-air), `player.log` (height sampling + jump integration log).

Run from a shell/window (it opens the map host window):

```powershell
.\engine_host_spike\run_player.cmd
# or: build map_spike_host.exe and cwd=C:\SeasunGame\MovieEditor with MAP_PLAYER=1
```

