# JX3 Collision & Physics — Full-System Analysis and Reproduction Reference

**Branch:** `research/game-collision-system` (base `c189535`, camera/`camara-fix` line)
**Date:** 2026-09-28
**Scope:** every collision/physics domain of the real JX3 client and its server-visible behaviour:
terrain, static world, foliage, dynamic objects, character body and movement, ragdoll, rays,
picking, triggers/volumes, water, navigation, combat targeting/hit detection, projectiles, and
the network authority layer. Two layers are documented side by side:

* **[client-truth]** — what the shipped client binaries and data provably do;
* **[inferred-server]** — the server-side behaviour that is not present in the client, modelled
  from protocol shape, tables and derived rules so the behaviour can be reproduced end-to-end.

This document is written to be sufficient to **reproduce the observable collision behaviour**.
It does not replace the deep-dive docs already in `docs/` (camera, gravity, map bake); it
consolidates them and adds the domains that had no synthesized reference yet.

---

## Table of contents

**Part I — Foundations**
1. [Scope and reproduction target](#1-scope-and-reproduction-target)
2. [Evidence taxonomy and source dumps](#2-evidence-taxonomy-and-source-dumps)
3. [Units, coordinates, time](#3-units-coordinates-time)
4. [Architecture and end-to-end flows](#4-architecture-and-end-to-end-flows)

**Part II — World collision**
5. [Core physics stack](#5-core-physics-stack)
6. [Collision math library (KBaseX64)](#6-collision-math-library-kbasex64)
7. [Terrain collision](#7-terrain-collision)
8. [Static scene objects and regions](#8-static-scene-objects-and-regions)
9. [Foliage, SpeedTree and the bake pipeline](#9-foliage-speedtree-and-the-bake-pipeline)
10. [Dynamic world objects](#10-dynamic-world-objects)

**Part III — Bodies and movement**
11. [Character body and kinematic solver](#11-character-body-and-kinematic-solver)
12. [Movement model (15 Hz integer)](#12-movement-model-15-hz-integer)
13. [Jump, fall, swim, fly, parkour](#13-jump-fall-swim-fly-parkour)
14. [Ragdoll, hit reactions, death](#14-ragdoll-hit-reactions-death)

**Part IV — Queries and interaction**
15. [Rays, filters and camera obstruction](#15-rays-filters-and-camera-obstruction)
16. [Picking, selection and targeting](#16-picking-selection-and-targeting)
17. [Triggers, zones, volumes, interaction](#17-triggers-zones-volumes-interaction)
18. [Water and fluids](#18-water-and-fluids)
19. [Navigation and pathing](#19-navigation-and-pathing)

**Part V — Combat collision**
20. [Targeting shapes (CastMode)](#20-targeting-shapes-castmode)
21. [Hit detection](#21-hit-detection)
22. [Projectiles and missiles](#22-projectiles-and-missiles)
23. [Damage, control and immunity (collision-adjacent)](#23-damage-control-and-immunity-collision-adjacent)
24. [Inferred server model](#24-inferred-server-model)

**Part VI — Reproduction and verification**
25. [Reproduction blueprints](#25-reproduction-blueprints)
26. [Cross-cutting gotchas](#26-cross-cutting-gotchas)
27. [Verification matrix](#27-verification-matrix)
28. [Evidence index and RE tooling](#28-evidence-index-and-re-tooling)
29. [Prioritized gap register](#29-prioritized-gap-register)

---

# Part I — Foundations

## 1. Scope and reproduction target

### 1.1 Domains in scope

| # | Domain | Client system | Primary evidence |
|---|---|---|---|
| 1 | Core physics runtime | PhysX 3.3.4, `PhysicsEngineX64.dll`, `SIMWorldX64.dll` | `docs/REAL_CLIENT_MAP_COLLISION.md`, `proof/gravity/SIMWorldX64_exports.txt` |
| 2 | Collision math | `KBaseX64.dll` | `proof/netcode/exports_KBaseX64.txt` |
| 3 | Terrain | heightfield loader, regions, holes, streaming | `docs/REAL_CLIENT_MAP_COLLISION.md`, `engine_host_spike/recon_terrain_*.txt` |
| 4 | Static world | `StaticPhysicsSceneManager`, `sceneinfo_full` JSON | `docs/STRUCTURE_COLLISION_RESEARCH.md`, `docs/FULL_MAP_COLLISION.md` |
| 5 | Foliage / SpeedTree | `.foliage`, `.CollisionMesh`, bake pipeline | `docs/FULL_MAP_COLLISION.md`, `tools/decode_foliage.py` |
| 6 | Dynamic objects | doodads, state machines, movable/advanced obstacles, conveyors, carriers | `proof/netcode/disasm/*`, `proof/gravity/SIMWorldX64_strings.txt` |
| 7 | Character body | kinematic capsule + SIMWorld foot solver; `KRLCharacterControllerComponent` | `proof/gravity/disasm/physics_scene_setup.txt`, string dumps |
| 8 | Movement model | `KCharacter` 15 Hz integer integration | `docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md`, `proof/movement/disasm/*` |
| 9 | Jump/fall/swim/fly | `JumpParam.tab`, `JumpFrameParam.tab`, `SkillMove.tab` | `docs/JX3_GRAVITY_RESEARCH.md`, `docs/REBORN_JUMP_FALL_SPEC.md` |
| 10 | Ragdoll/death | `KPhysicsRagdoll`, 11-body presets | `proof/gravity/physic_character_param.krl.txt`, `proof/gravity/disasm/ragdoll.txt` |
| 11 | Rays/filters/camera | `RayIntersection*`, `FilterCamera`, track camera | `docs/CAMERA_WALL_OBSTRUCTION.md`, `proof/netcode/camera_wall_obstruction.txt` |
| 12 | Picking/selection | cursor rays, `PickRayWalk`, `target.lua` | `proof/netcode/disasm/cursor_*`, `docs/CAMERA_INPUT_CONTROLS.md` |
| 13 | Triggers/volumes | `KG3DSceneResponse`, zone mgr, PhysX triggers, interaction | string dumps (`KG3DEngineX64`, `SIMWorldX64`, Represent) |
| 14 | Water/fluids | waterline math, `WaterData`, flux collision map | `proof/gravity/disasm/get_waterline.txt`, `recon_managed_api.txt` |
| 15 | Navigation | `NAVX64.dll`, `KNavMeshQuery`, `DoNavTo` | `docs/netcode/JX3_NETCODE_RESEARCH.md`, string dumps |
| 16 | Combat collision | CastMode shapes, `GetLogicRaycastHit`, bone boxes, missiles, server result | `docs/pvp/*`, `proof/pvp/*`, `proof/netcode/skill_data/*` |

### 1.2 Out of scope

Rendering/visibility, UI, audio, animation compression formats, and item/inventory logic except
where they expose collision data (e.g. `.CollisionMesh`, bone bounds, doodad radii).

### 1.3 Fidelity definition

A reproduction is considered faithful when, for the same inputs and state, the following are
observably identical: **positions and blocking** (can the actor be here?), **support height and
falling**, **camera placement under obstruction**, **targetability and hit outcomes**, and
**network-visible state changes** (move state, skill results). Frame-exact physics determinism
is *not* claimed where the client itself is server-corrected.

---

## 2. Evidence taxonomy and source dumps

Every statement in this document is tagged:

| Tag | Meaning |
|---|---|
| **[DISASM]** | verified against in-repo disassembly with addresses |
| **[DATA]** | extracted shipped table / Lua / config / asset bytes |
| **[IL]** | verified managed IL dump (editor CLR API only) |
| **[DOC]** | prior repo research doc; its own confidence applies |
| **[NAME]** | symbol/string exists, behaviour not decoded |
| **[HOST]** | our reimplementation, not game evidence |
| **[SERVER-INF]** | inferred server behaviour (not present client-side) |

### 2.1 Primary binary dumps

| Dump | Module | Contents |
|---|---|---|
| `proof/movement/KG3DEngineX64_strings.txt` (= `proof/netcode/engine_strings.txt`, identical) | `KG3DEngineX64.dll` | engine strings incl. `RayIntersection*`, mesh display flags, water manager, `KG3DSceneResponse` holder |
| `proof/netcode/adapter_strings.txt` | `KG3DEngineAdapterX64.dll` | adapter config keys, `KG3DSceneProxy::RayIntersectionModel`, `PhysicsOverlapCallBack`, scene-response manager |
| `proof/gravity/SIMWorldX64_strings.txt` + `SIMWorldX64_exports.txt` | `SIMWorldX64.dll` | gameplay PhysX wrapper: export RVAs, trigger/zone/conveyor/npc-list keys, ray result struct |
| `proof/gravity/JX3ClientX64_exe_strings.txt` | `JX3ClientX64.exe` | logic: `KCharacter`, `KNpc`, nav, waterline, interaction, advanced obstacles |
| `proof/gravity/JX3RepresentX64_strings.txt` (= `proof/netcode/JX3RepresentX64_all_strings.txt`) | `JX3RepresentX64.dll` | character controller component, water height, missile/bullet, picking, camera lock |
| `proof/movement/KBaseX64_strings.txt` + `proof/netcode/exports_KBaseX64.txt` | `KBaseX64.dll` | collision math exports |
| `proof/netcode/NAVX64_net_strings.txt` | `NAVX64.dll` | `PathEngineNavi` obstacle API (thin) |
| `proof/netcode/mode_ui/strings/kg3dsceneresapi_strings.txt` | `KG3DSceneResAPI.dll` | doodad/water/scene data loaders |
| `proof/netcode/mode_juejing/JX3ClientX64_exe_net_strings.txt` | logic (curated netcode scan) | skill fields, protocol names |
| `engine_host_spike/recon_*.txt` | `PhysicsEngineX64.dll`, adapter | vtable/function recon incl. `SweepEx`, terrain loader, static scene manager |

### 2.2 Disassembly transcripts

`proof/gravity/disasm/` (jump/fall/water/ragdoll/physics scene), `proof/movement/disasm/`
(WalkTo/MoveTo/sync/input), `proof/netcode/disasm/` (protocol, camera, cursor, scene load),
`proof/pvp/netcode/disasm*/` (combat handlers). `camara-fix` adds `cursor_*`, `locktarget_set`,
`aircombat_loader`, `engine_obstruct`, `engine_camera_contract`.

### 2.3 Tools

`tools/dump_va.py` (range disassembler), `tools/recon_physics*.py` (PhysicsEngine recon),
`tools/movement/find_xrefs.py` (call/field xrefs), `tools/dump_il*.fsx` (managed IL),
`tools/gravity/parse_jump_tables.py` + `verify_model.py`, `tools/netcode/*` (skill scripts,
motion, protocol), `tools/pvp/*` (attributes, cast/hitstiff), `tools/bake_map_collision.py`
(collision bins), `pss_assets.py` (PakV4 extraction backend).

---

## 3. Units, coordinates, time

### 3.1 Canonical units

* **1 world unit = 1 cm** — established from the character mesh census: adult body
  `m2_1018_body_hd.mesh` bounding height 181.64 u = 1.816 m
  (`docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md:15-31`,
  `proof/netcode/character_size/mesh_census.json`). Ragdoll limb radii 5–8 u = 5–8 cm
  (`docs/CAMERA_REAL_VALUES.md:27`).
* **1 尺 (game foot) = 64 units = 0.64 m** — `LENGTH_BASE = 64.0`
  (`proof/pvp/cast_cooldown_resources.md:38-45`); skill ranges are written in 尺 and
  multiplied by 64. `HEIGHT_BASE = 512.0`, `PERCENT_BASE = 1024`, `GAME_FPS = 16`.
* Terrain cells are 100 u = 1 m; region = 512 cells = 51,200 u = 512 m.

**Unresolved conflict [known]:** `docs/JX3_GRAVITY_RESEARCH.md:109-112` and
`docs/REBORN_JUMP_FALL_SPEC.md:14` use **1 m = 192 u** (implying 1 尺 = 64 u = 1/3 m), which
contradicts the mesh census (1 u = 1 cm). The movement/gravity *integer* model (90 u/frame
takeoff, 11 u/frame² gravity) is independent of the label; only the m/s and m/s² conversions
differ (13.5 m/s / 24.75 m/s² at 100 u/m vs 7.03 m/s / 12.89 m/s² at 192 u/m). See §26.1 for
the decisive experiment. Use **100 u/m** for all reproduction unless a domain is explicitly
calibrated otherwise.

### 3.2 Coordinates

* World is **X/Z horizontal, Y up**; terrain origin `(-102400, -102400)`, 8×8 regions of
  51,200 u (`docs/REAL_CLIENT_MAP_COLLISION.md:69-70`).
* Region index `floor((v - origin) / (RegionSize*UnitScale))`, clamped; loader returns a
  `(512+1)²` float height grid per region (R32 or BCH/RLE encoding).
* Instance matrices in `sceneinfo_full` are **row-major with translation in row 3**; the
  runtime convention used by the baked bins is **row-vector** `w = l · M`
  (`docs/FULL_MAP_COLLISION.md:138-145`).
* Foliage cells add `(cellX*400, baseZ, cellY*400)` to instance local positions; region origin
  `(-102400 + ix*51200, -102400 + iz*51200)`
  (`docs/FULL_MAP_COLLISION.md:120-136`).

### 3.3 Time

| Clock | Rate | Where |
|---|---|---|
| Character logic | **15 Hz** (66.7 ms/frame) | `KCharacter` movement, `DoMoveCtrl`; `docs/JX3_GRAVITY_RESEARCH.md:56-104` |
| Combat table time | **16 fps** (`GAME_FPS=16`) | skills/buffs/debuff frames |
| Physics world | **50 Hz fixed 20 ms** | PhysX scene step (`proof/gravity/disasm/physics_scene_setup.txt:192-235`) |
| Animation | 30/33 fps samples | `.tani` clips (46 frames @ 33 fps for 太阴指) |
| Network | server tick + routine sync 0x6E, ping 3000 ms | `docs/netcode/JX3_PROTOCOL_SPEC.md:30-113` |

---

## 4. Architecture and end-to-end flows

### 4.1 Module graph

```
JX3ClientX64.exe ── game logic (KCharacter, KNpc, KScene, skills, interaction)
   │
JX3RepresentX64.dll ── representation (KRLCharacter, controller component, bullets, water)
   │
KG3DEngineX64.dll ── engine (scene, terrain render, rays, water manager, visibility)
   ├─ KG3DEngineDX11EX64.dll  (game render path; no bObscatleCamera/FilterCamera)
   ├─ KG_EngineEditorX64.dll  (editor/host path used by MovieEditor host)
   ├─ PhysicsEngineX64.dll    (static physics middleware wrapper)
   ├─ SIMWorldX64.dll         (gameplay PhysX world: PxWorld)
   ├─ NAVX64.dll              (PathEngine nav wrapper)
   ├─ KG3DSceneResAPI.dll     (doodad/water/scene data loaders)
   └─ KG3D_ApexClothing / FlexibleBody / IKSystem / NVBlastDestruction (aux)
        │
PhysX 3.3.4 (PhysX3_x64, PhysX3Common_x64, PhysX3Cooking_x64, PhysX3CharacterKinematic_x64)
```

### 4.2 World collision flow

```
pak (jsonmap / landscapeinfo.json / sceneinfo_full/*.json / .foliage / .mesh / .CollisionMesh)
  → PhysicsEngine loaders (terrain loader, StaticPhysicsSceneManager, SceneFileDataLoader_JsonSource)
  → PhysX actors (PxHeightField per region, PxTriangleMesh/PxConvexMesh per object)
  → queries: PhysicsScene::SweepEx / SIMWorld PxWorld::RayCast / GetFloorHeight
  → gameplay (KCharacter support/blocking, skills, camera)
```

Our host reproduces this offline: terrain via the real loader (`TerrainSampler`), static world
via baked triangle bins (`FoliageCollision`) — see §7, §9.

### 4.3 Movement frame flow [client-truth]

```
input (hotkey → UI Lua → CommitInput)
  → KCharacter state machine (+0x1F4), facing/turning (+0x44/+0x48), heading (+0x26C)
  → 15 Hz integer integration (ProcessAcceleration / ProcessVerticalMove / ProcessDropSpeed)
  → ground/support from physics (GetFloorHeight / scene support + slope cells)
  → Represent pose (KRLCharacterFrameData) + anim selection
  → C→S: DoMoveCtrl (seq) / DoSyncDirection; S→C: OnMoveCharacter / OnSyncMoveState/Ctrl/Param
```

### 4.4 Combat flow [client-truth + server]

```
input cast → KSkill (skills.tab CastMode + Lua range/angle data)
  → C→S DoCharacterSkill / DoCastProfessionSkill (target or XYZ)
  → server legality: range/angle/LOS(Use3DObstacle)/reachable(CheckReachable)/state masks
  → server hit resolution (shape, target count, falloff)  ← not in client
  → S→C OnSkillEffectResult (9-byte results), OnSkillBeatBack/RayEffect/ChainEffect
  → client plays hit anim/SFX; no client hit test exists
```

### 4.5 Query ownership summary

| Query | Owner | Reproducible today? |
|---|---|---|
| Terrain height | `PhysicsEngineX64` terrain data loader (raw grids) | yes, host loader |
| Static object blocking | PhysX actors via `StaticPhysicsSceneManager` | offline bake (approximation) |
| Gameplay ray/sweep | `SIMWorldX64` (`PxWorld::RayCast`, `GetFloorHeight`, `SweepEx`) | API known, not wired |
| Scene rays (camera/pick) | `KG3DEngineX64` (`RayIntersection*`) | partly wired in host `/camara-fix` |
| Combat hit resolution | **server** | only via inferred model (§24) |
| Nav | `NAVX64` + `KNavMeshQuery` | data format unknown (§19) |

# Part II — World collision

## 5. Core physics stack

### 5.1 PhysX 3.3.4 [DISASM][DATA]

Build paths embedded in the binaries prove PhysX 3.3.4 is statically linked/customised:
`j:\sword3\devenv\physx-3.3.4\...` (`proof/movement/KG3DEngineX64_strings.txt:573927,574114`)
and `E:\buildagent\workspace\root\JX3\hd_code\DataStores\DevEnv\physx334\...`
(`proof/gravity/SIMWorldX64_strings.txt:19113`). Modules loaded by the client
(`proof/netcode/net_import_modules.txt:249-277`):

```
PhysicsEngineX64.dll   (game)   /  PhysicsX64.dll (editor)
PhysX3_x64.dll  PhysX3Common_x64.dll  PhysX3Cooking_x64.dll
PhysX3CharacterKinematic_x64.dll     (linked by PhysicsEngineX64)
PhysXCooking64.dll  PhysXCore64.dll  PhysXLoader64.dll   (editor/legacy names)
```

Core object/type names in the engine dump: `PxMaterial`, `PxConvexMesh`, `PxTriangleMesh`,
`PxHeightField`, `PxRigidActor`, `PxRigidDynamic`, `PxArticulation`, `PxShape`, `PxBoxGeometry`,
`PxCapsuleGeometry`, `PxConvexMeshGeometry`, `PxSphereGeometry`, `PxTriangleMeshGeometry`,
`PxHeightFieldGeometry`, `PxCloth`, `PxParticleFluid`
(`proof/movement/KG3DEngineX64_strings.txt:574642-574697`).

### 5.2 `PhysicsEngineX64.dll` — static physics middleware [DISASM]

Exports (image base `0x180000000`; `engine_host_spike/recon_physics*.txt`):

| Export | VA | Purpose |
|---|---|---|
| `GetPhysicsManager` | `0x18000F850` | returns the singleton manager (`lea rax,[singleton]; ...`) |
| `CreatePhysicsTerrainDataLoader` | `0x180031320` | raw terrain height loader (used by host) |
| `CreatePhysicsTerrain` | `0x18002EAE0` | terrain object factory |
| `CreatePhysicsSceneDynamicLoader` | `0x18002CB40` | `StaticPhysicsSceneManager` factory |
| `CreateSceneFileDataLoader` | `0x180025CF0` | scene JSON/doodad loader factory |

Manager singleton at RVA `0x11B5D0`, vtable RVA `0xFA6B0`; vtable entries
(`engine_host_spike/recon_physics_funcs.txt:22-62`):

| slot | RVA | Method |
|---|---|---|
| 0 | `0x18000F860` | `Init(fs)` → `_InitPhysX` (idempotent only pre-init; calling after engine init destroys state) |
| 2 | `0x1800109B0` | `SetWorkingDir(root)` (creates Semantic/Lua VFS) |
| 14 | `0x1800108B0` | `CreatePhysXTerrain(out, cfg)` — cfg 16 B `{nPreLoadSize, nForceLoadSize, nUpdateDelta, nMaxCacheCount}`, asserts `nPreLoadSize >= nForceLoadSize > 0` |
| 15 | `0x180010930` | `CreatePhysicsSceneDynamicLoader(out, cfg)` → `StaticPhysicsSceneManager` |
| 16 | `0x180010460` | `CreatePhysicsScene(out, engineArg)` — needs a real engine-side arg |

`PhysicsScene` vtable `0x1800FA7B8`, `SweepEx` string `0x1800FAA58`, vt[16] RVA `0x1C5B0`
(`engine_host_spike/recon_physics3.txt:76`, `proof/gravity/recon?` — see
`docs/REAL_CLIENT_MAP_COLLISION.md:128-131`). **There is no `PhysicsScene::RayCast` and no
`PhysicsScene::GetFloorHeight` inside `PhysicsEngineX64.dll`**
(`engine_host_spike/recon_physics3.txt:78-80`); gameplay ray/floor queries live in
`SIMWorldX64.dll`, scene sweeps in `SweepEx`.

Controller-manager creation inside the physics scene (this is the *editor/physics* character
path, not necessarily the gameplay body — see §11):
`PxCreateControllerManager(scene, 0)` at `0x180018C40`, manager stored `[scene+0x10]`
(`proof/gravity/disasm/physics_scene_setup.txt:150-166`); scene gravity default `(0,-9.81,0)`
(`:79-87`); fixed step 20 ms (`:192-235`); controller params derived from a scale vector with
multipliers `0.01`, `0.025`, `0.2`, `0.04`, constant `0.4` (`:37-67`) — **field labels
unidentified** (gap G-1).

Terrain object vtable `0x1800FCFE0` (`engine_host_spike/recon_physics2.txt:4`):
vt[2] `UpdateTerrain` `0x18002DAB0`, vt[5] full `LoadTerrain` `0x18002DF90`,
vt[12] `GetTileBBox` `0x18002E890`. Terrain data loader vtable `0x1800FD2B0`:
vt[2] `GetTerrainDesc` `0x1800321C0`, vt[3] `LoadRegion` `0x180032200`,
vt[4] `LoadHoleRegion` `0x180032540` (`engine_host_spike/recon_loader_methods.txt`).

### 5.3 Static scene manager internals [DISASM]

`StaticPhysicsSceneManager` (`engine_host_spike/recon_dynloader.txt`):

| Field | Meaning |
|---|---|
| `+0x28` | region world size |
| `+0x2C/+0x30` | origin x/z |
| `+0x34` | sub-region size (`=0x80`) |
| `+0x38/+0x3C` | region counts x/y |
| `+0x40` | total regions |
| `+0x48` | `PxScene` |
| `+0x50` | region table (stride 0x10: state + actor list) |
| `+0x58` | scene-file loader |
| `+0x60` | `SceneRegionManager` |
| `+0x68` | update flag |
| `+0x6C/+0x74` | last streaming position |

`LoadFromFile` `0x18002BB80`, `Unload` `0x18002BF00`, `UpdateScene` `0x18002BA90`
(delta² vs `(regionCount·range)²`), `_UpdateRegionsInPreloadingRange` `0x18002C0C0`,
`_UpdateRegionsInForceLoading` `0x18002C330`; region states `0` empty, `1` preloading,
`2` force-loaded, `3/4` unloading (`recon_dynloader.txt:617-631,874-890`). Actors are added
through `PhysicsSceneActor::AddToPxScene` / removed via `RemoveFromPxScene`
(`recon_dynloader.txt:1018,1040,1161`).

### 5.4 Collision geometry resolution [DISASM]

`PhysicsEngine::_GetCollisionGeometryFilesFromFile` dispatches by extension
(`docs/STRUCTURE_COLLISION_RESEARCH.md:167-217`):

| Source | Collision geometry rule |
|---|---|
| `.srt` / `.st` (SpeedTree) | sibling `<base>.CollisionMesh` (string appended at `0x18002455B`, used verbatim) |
| `.mdl` / `.group` / `.prefab` | mesh entries matched by `CollisionMesh` / `proxymesh` / `mesh`, cooked via `CreateCacheShapeFromFile` |
| `.pxm` | physics mesh (`proof/movement/KG3DEngineX64_strings.txt:544079`) |
| `.mesh` | triangle mesh from mesh file (`_GetCollisionGeometryFilesFromMdlFile` `0x180023A3F`, `_CreateCollisionFromMeshFile` `0x18002281C`) |

Supporting functions: `_GetCollisionGeometryFilesFromGroupFile` `0x180023985`,
`_GetStreeMeshFile` `0x180023CE2`/`0x180023D30`,
`PhysicsShapeFactory::CreateShapeFromGeometryData` `0x1800FAB50`,
`KPhysicsActor::_AttachShapeWithMetadata` / `IsOverlapWithMeshGeometry`
(`engine_host_spike/recon_collision_naming.txt`, `recon_physics_funcs.txt:12-15`).

Per-mesh display/physics flags written by `KG3DMesh::SavePropertyToIni`
(`proof/movement/KG3DEngineX64_strings.txt:546005-546016`):

| Flag | Default | Effect |
|---|---|---|
| `bAutoProduceObstacle` | 1 | mesh participates in auto obstacle generation |
| `bObscatleCamera` | 1 | camera obstruction ray blocks on this mesh (0 = camera ignores) |
| `bHeightTest` | 1 | height-test eligible |
| `bOccluder` | 1 | visibility occluder |

The `bObscatleCamera` data path is fully recovered: `KG3DMeshFileDataLoader::Load`
(`0x180264540`, 2026-09-27 build) derives `.ini` siblings and `_LoadMeshProperty`
(`0x180267360`) reads `[Display] bObscatleCamera`; a missing ini keeps the ctor default 1
(`docs/CAMERA_PENETRATION_PLAN.md:165-186`). Our exporter converts these to `.cflags` sidecars
(§9.4).

**Static-physics selection is list-driven (recovered 2026-09-28).** The engine does not use
`comLogic.obstacleOption` to decide per-object physics; it filters models through four config
files (`Represent/physic/`, extracted to `proof/collision/physic/`):

| File | Size | Content |
|---|---|---|
| `physic_folder_white.txt` | 6 | allowed folders: `maps_source`, `home`, `item`, `树`, `whitebox_k` |
| `physic_folder_black.txt` | 6 | filtered folders: `灌木`, `花草`, `树`, `NPC_source`, `water` |
| `physic_file_white.txt` | 2,559 | explicit model stems allowed |
| `physic_file_black.txt` | 2,205 | explicit model stems filtered |

Additional physic configs recovered: `physic_shape_param.krl.txt` (named shape library — box
500×50×50 @y50, sphere r26, mesh, two boxes, **capsule r50/l50**, more), 
`physic_rigid_param.krl.txt` (mass, static/dynamic friction, restitution, combine mode,
contact-modification masks, initial velocities/damping), and
`physic_conveyor_belt_param.krl.txt`. See §8.4 for the audit and the implemented rule.

### 5.5 `SIMWorldX64.dll` — gameplay PhysX world [DISASM]

Export table (`proof/gravity/SIMWorldX64_exports.txt`):

| Export | RVA | Use |
|---|---|---|
| `CreateSIMWorld` / `DestroySIMWorld` | `0x351C0` / `0x35270` | lifecycle |
| `PxWorld::Initialize` / `Setup` / `SetupPhysic` | `0x32070` / `0x337A0` / `0x33D70` | world bring-up |
| `PxWorld::GetFloorHeight` | `0x31F20` | ground height query (gameplay) |
| `PxWorld::RayCast` | `0x32FB0` | scene ray with result struct |
| `PxWorld::RayCastDirect` | `0x332D0` | direct ray |
| `PxWorld::SetGravity` | `0x33660` | world gravity (forwarder to inner slot) |
| `PxWorld::AddForce` / `AddTorque` / `SetVelocity` / `SetAngularVelocity` | `0x31DD0` / `0x31E80` / `0x33770` / `0x33510` | dynamic bodies |
| `PxWorld::GetRigidParam` | `0x31FB0` | rigid params |
| `PxWorld::SetMeshMass/Material/Simulatable/StaticPhysxParams` | `0x336B0/0x336E0/0x33720/0x33750` | mesh physics props |
| `PxWorld::GetSimulationEvent` | `0x31FF0` | contact/trigger events |
| `PxWorld::LoadScene/LoadSceneObject/UnloadSceneObject/UpdateSceneObject` | — | scene object streaming |
| `PxWorld::OnSetZonePhysLevel` | `0x32ED0` | zone physics level |
| `PxWorld::OnReloadRegionTerrian` | `0x32A20` | terrain region reload |
| `SetConveyorBeltParam` / `OnSetConveyorBeltParam` | `0x33540` / `0x32E80` | conveyor belts |
| `AddSummitPoint` / `RemoveSummitPoint` / `SetupSummitPoint` | — | summit/marker points |

Ray result struct field names (`proof/gravity/SIMWorldX64_strings.txt:19021-19049`):
`raylen`, `rayradius`, `raydir`, `raypoint`, `raynormal`, `raydist`, `raycheckquerymask`,
`ray cast ti`, `ray cast touch`, `raytrigger`, `rayvalid`, `ray has block`, `ray nb touches`,
`overlap triiger`, `overlap valid`.

Scene builder semantics (`SIMWorldX64_strings.txt:18679-18931`): `PhysicScene::_Init`,
`ClearScene`, `_CookTriangleMesh`, `_CookTerrian`, `_AddTerrian`, `_AddGeometry`, `_AddMesh`,
`_AddBox`, `_AddCapsules`, `_AddSphere`; shape semantic keys `capsules radius` (:18896),
`capsules length` (:18899), `sphere radius` (:18902), `half ext x/y/z` (:18887-93),
`meshpath` (:18905), `rigid dynamic param` (:18920), `shape type` (:18882).

Trigger/zone/conveyor semantic keys (`SIMWorldX64_strings.txt`): `npclist`/`npcid` (:19012-13),
`remoteplayerlist` (:19063), `subactor` (:18826), `worldmatrix` (:18825),
`ZoneMgr::{Init,ClearScene,_OnMeshLoaded,_AddBuildinMesh,_UpdateStatic,_UpdateDynamic}` (:18784-805),
`px zone phys level` (:18968), `obj add/remove/update trigger` (:18922-26), `cam trigger`
(:18851), `raytrigger` (:19039), `overlap triiger` (:19048), `conveyor belt` (:18952-59).

**Re-verified 2026-09-28** against the live module (`proof/collision/recon/SIMWorldX64_exports.txt`,
65 exports). Additional entries not in the original proof dump: `GetPlayerPos`,
`GetCameraParam`, `IsRush`, `IsSummitPointEnable`, `HightSummitPoint`, `Activate`,
`GetActivateTransform`, `LoadJoint`, `DebugSimulate`, `GetDebugLines`,
`SetLinearDamping`/`SetAngularDamping`, and the full `RayCastDirect` signature
`(const FVECTOR3& from, const FVECTOR3& dir, float dist, float radius, int mask, FVECTOR3& outPoint,
FVECTOR3& outNormal)` — i.e. a direct ray returns both hit point and normal by reference.
`LoadScene` takes `(int, const char*, const char*, bool)`; `UnloadSceneObject(ObjectID&)` and
`UpdateSceneObject(ObjectID&, FMATRIX4&)` confirm object ids are stable `ObjectID` structs.

### 5.6 Adapter bridge and config [DISASM]

`KG3DEngineAdapterX64` loads `PhysicsEngineX64.dll`, resolves `GetPhysicsManager`, stores it at
`[rsi+0x9D8]`, then calls `Init(fs)` and `SetWorkingDir` (`engine_host_spike/recon_adapter_init.txt:113-155`).
Config keys and the fields they fill (`engine_host_spike/recon_adapter_mgruse.txt:426-461`):

| Key | Field | Shipped value |
|---|---|---|
| `bCreateInternalPhysXScene` | `+0x680` | 1 |
| `bEnableSceneCollision` | `+0x564` | 1 |
| `bUseLODMeshCollision` | `+0x568` | 1 |
| `bEnableSceneNodeAngleCull` | — | — |
| `bAddPlayerPhysicsActor` | — | 0 |
| `bEnableCapsuleCollision` | — | — |
| `bEnableRagdoll` | — | — |
| `bEnableForceField` | — | — |
| `bEnableFluxWater` | — | — |

Values from `config.ini:199-201` per `docs/REAL_CLIENT_MAP_COLLISION.md:19`; `DrawPhysicsObstacle=0`.
Other engine-facing API: `KG3DPhysiscManager::{QueryPhysXParam, CreateCapsule, CreatePxScene,
NewOnePhysiscScene, DeleteOnePhysiscScene, _ForceAllSceneFinishSimulate}`,
`KG3DEngineManager::GetPhysxInterface`, config `data\public\physics.ini`
(`proof/movement/KG3DEngineX64_strings.txt:536465,536504-536794,539983`).

### 5.7 Auxiliary physics modules [DATA][NAME]

`KG3D_ApexClothingX64.dll`, `KG3D_ApexFrameworkX64.dll`, `KG3D_FlexibleBodyX64.dll`,
`KG3D_IKSystemX64.dll`, `KG3D_NVBlastDestructionX64.dll`, `KG3D_TressFXX64.dll`
(`proof/netcode/net_import_modules.txt:133-173`). `KG3DModel::{_SetPhysicsScene,
_SetPhysicsSceneNew}` and `CMD_SetPhysicsScene`, `SetPhysXLOD/SetPhysXLODMin`
(`proof/movement/KG3DEngineX64_strings.txt:543500-543503,543794,551416-551422`).
`KG3DFlexibleBody::AddToPhysicsScene` / `RemoveFromPhysicsScene` (:542999-543082).
Character config `proof/gravity/physic_character_param.krl.txt:1-5`:
`EnableCharacterCapsule=1`, `EnableCharacterStatic=1`, `EnableDestructibleComponent=1`,
`DestructibleRadius=20`, `DestructibleDamage=100`.

---

## 6. Collision math library (KBaseX64)

`KBaseX64.dll` is the shared math library used by logic and representation. Exports
(`proof/netcode/exports_KBaseX64.txt`):

| Group | Exports |
|---|---|
| Box/AABB | `KBox` ctor, `KAABBbox`, `BuildAABB@KBox`, `Transform@KAABBbox`, `ComputeAABBboxFromLocal`, `GetClosestPointTo@KBox` |
| Intersections | `Intersect@KBox`, `IntersectXY`, `Overlap@KBox`, `IsInside`, `IsInsideOrOn`, `IsInsideXZ`, `IsIntersect@KAABBbox`, `IsIntersect@KSphere`, `IsIntersectXZ@KAABBbox`, `SphereAABBIntersection`, `TestAABBPlane`, `TestPointInAABBBox`, `TestTriangleAABB` |
| Rays | `KRectRay`, `IsOutAABBbox@KRectRay`, `IsOutSphere@KRectRay`, `IsXZInAabbBox`, `LineBoxIntersection`, `PointBoxIntersection`, `RayAaBBboxTest`, `RayFacesTest`, `RayPlaneTest`, `RaySphereIntTest`, `RayTriangleTest`, `TestSegmentBoundingBoxesIntersect`, `TestSegmentIntersect` |
| Picking | `GetObjectLocalPickRay`, `GetWorldPickRay`, `ToObjectLocalPickRay` |

These are the exact primitives to reproduce for client-identical geometric tests; the
corresponding string symbols are in `proof/movement/KBaseX64_strings.txt:178481,179043,181956`.
The scene-level ray dispatch (`RayAaBBboxTest` etc.) is one level above these primitives.

---

## 7. Terrain collision

### 7.1 Description and observed values [DATA]

The `.jsonmap` references `landscape/<map>_landscapeinfo.json`. Observed descriptor for
龙门寻宝 (`proof/map_spike/龙门寻宝_landscapeinfo.json:2-19`, probe log
`docs/REAL_CLIENT_MAP_COLLISION.md:84-92`):

```
RegionSize 512        LeafNodeSize 64       UnitScale 100
RegionTableSize 8x8   HeightfieldMaximum 409600   HeightfieldMinimum -409600
Flags 7               BakeVersion 1
```

`GetTerrainDesc` (loader vt[2] `0x1800321C0`) returns 32 bytes read by the host as
`{size, regionsX, regionsZ, leaf, cell, cell, originX, originZ}`:
`512, 8, 8, 64, 100f, 100f, -102400f, -102400f` (`engine_host_spike/MapSpike.cs:1890-1904`).
World span per region = `RegionSize × UnitScale` = 51,200 u; region index is
`floor((v − origin) / 51200)` clamped (`MapSpike.cs:1912-1918`).

### 7.2 Height data and holes [DISASM]

* Raw height grids via `LoadRegion(ix, iz, float* buf, (size+1)², outA, outB, outC)` — loader
  vt[3] `0x180032200`; decoders `_LoadHegihtRegionR32` / `_LoadHegihtRegionBCH`
  (`engine_host_spike/recon_loader_methods.txt:2-33`); observed `ok=1`, `outC=513`.
* Holes via `LoadHoleRegion` (vt[4] `0x180032540`): files `%hs\hole\%hs_%03u_%03u.hlb` (or
  `.png`), decoded by `_LoadFromPNGMemory` (formats `0x3D`, `0x1C`; assert
  `nArraySize == nRegionSize*nRegionSize/8`) (`recon_loader_methods.txt:238-443`).
* PhysX actor creation `KG3D_PhysxTerrainData::_CreatePxActor` `0x180011910`: heightfield of
  `(nRegionSize+1)²` samples with the border row/column duplicated so neighbouring tiles match
  (`engine_host_spike/recon_pxactor.txt:263,352-413`).
* Region table on the terrain object: `+0x24` count X, `+0x28` count Y, `+0x48` array of
  0x30-byte entries (`+0` type, `+8` data pointer); `type==2` = loaded
  (`docs/REAL_CLIENT_MAP_COLLISION.md:67-70`).

### 7.3 Streaming [DISASM]

`UpdateTerrain(pos)` = terrain vt[2] `0x18002DAB0` streams force/preload ranges around the
position; probe calls it 40× and observes `region[20] type=2` for `(147463,5231,49911)`
(`docs/REAL_CLIENT_MAP_COLLISION.md:59,91`). Full map load uses terrain vt[5]
`LoadTerrain(path,0,mgr)` which builds its own loader, reads `landscapeinfo.json`, allocates the
region table and creates the region manager.

### 7.4 Render-side terrain extras [NAME]

Terrain holes are also drawn: `GetBrushTerrainHoleMesh`, `%s_TerrainHole.mesh`,
`GetModelTerrainHoleMesh`, flags `eKG3DSNMF_TerrainHoleModel`
(`proof/gravity/JX3RepresentX64_strings.txt:764251-766621`). Water bounding planes per
terrain node/block: `KG3DTerrainRenderData_Node/Block::GetWaterBoundingPlanes`
(`proof/movement/KG3DEngineX64_strings.txt:536009,536053`).

### 7.5 Host reproduction [HOST]

`client/TerrainSampler.cs` (identical in main/camara-fix/ability clones) wraps the same loader:
`CreatePhysicsTerrainDataLoader(mapPath)` → `GetTerrainDesc` → `LoadRegion`; caches exactly one
region (~1 MB) and bilinearly samples `Sample(x,z)` with clamped coordinates
(`TerrainSampler.cs:44-142`). Startup warms up with two samples 300 ms apart
(`RebornClient.cs` setup) and a spawn settle loop. **Holes are not loaded** — a hole region is
still solid ground (gap G-2). No normals/materials are exposed; steep cliffs appear as large
height deltas and are handled by the 70-u rise rule (§12.4, §25.2).

### 7.6 Hole/water asset probes (2026-09-28)

Two candidate batteries (24 paths, `proof/collision/recon/terrain_extra_candidates*.txt`) could
not locate the hole/water files for 龙门寻宝 under the guessed templates (`<map>/hole/*.hlb`,
`<map>/landscape/hole/*`, `<map>/landscape/*.WaterData`, `…/water/*.WaterData`). The loader
format strings and PNG/HLB decoders remain the authoritative description of the formats
(§7.2); the shipped file naming for holes/water is still an open item — next step is to
disassemble the loader path builder (`KG3D_PhysxTerrainDataLoader_Source`) for the exact
`%hs` composition and to test a map known to ship water (e.g. 海岛绝境).

The map does ship `<map>.SRScene` (524 B, extracted to `proof/collision/terrain_extra/`):
magic `SRS\0`, body all-zero on 龙门寻宝 — the SceneResponse state file (§17.1).

---

## 8. Static scene objects and regions

### 8.1 The two JSON paths [DISASM]

The shipped `entities/<map>_sceneinfo.json` + `entities/sceneinfo/%03u_%03u.json` files contain
`"worldObjects": {}` on all 150 maps scanned (1,714 region files)
(`docs/STRUCTURE_COLLISION_RESEARCH.md:39-63`). The real HD objects live in
`entities/sceneinfo_full/%03u_%03u.json`: template
`%s%s\entities\%s_full\%03u_%03u.json` built by `SceneFileLoader_Jsonmap::OnSyncLoad`
(init `0x18004CA5A`, fill `0x18004B7E3-0x18004B816`, format string `0x180069288`, literal
`sceneinfo` at `0x180069158`) (`docs/FULL_MAP_COLLISION.md:57-66`).

### 8.2 World-object schema [DATA]

Per object (example from 龙门寻宝):

```json
"comRender.actorModel":      "data\\source\\maps_source\\...mesh|...srt",
"comBasic.actorLocalMatrix": [16 floats, row-major, translation row 3],
"comBasic.actorBoundBoxMin": [x,y,z], "comBasic.actorBoundBoxMax": [x,y,z],
"comLogic.obstacleOption / enablePhysicsConfig": (physics semantics, see §8.5),
"comEditor.templateFile":    prefab/template link
```

`worldObjectCount` example: 5,075; extracted 4,965 objects (4,777 mesh + 186 SpeedTree + 2
misc) (`docs/FULL_MAP_COLLISION.md:52-79`).

### 8.3 Loader and streaming [DISASM]

`CreateSceneFileDataLoader` → `SceneFileDataLoader_JsonSource` (`Init`, `_LoadRegionFileData`,
`LoadRegionData`, `_LoadSceneDesc`, `FreeRegionData`; `engine_host_spike/recon_jsonsource.txt`)
feeds `StaticPhysicsSceneManager::LoadFromFile`. Region stream states and actor
add/remove per §5.3. Scene region manager `SceneRegionManager::_CreatePxActorInRegion`
(`docs/STRUCTURE_COLLISION_RESEARCH.md:17-23`). Editor process scans found **no live
PhysicsScene** inside MovieEditor, which is why our host bakes meshes offline instead
(`docs/STRUCTURE_COLLISION_RESEARCH.md:56-60`).

### 8.4 Physics selection: audit + implemented engine rule (2026-09-28)

**World-object flag audit [DATA].** All 64 region files / 4,965 objects of 龙门寻宝 were parsed
(`tools/collision/audit_physic_lists.py` companion scan): every object carries
`comLogic = {obstacleOption: 0, enablePhysicsConfig: 0}`; no other flag keys exist on the
objects. So per-object variation is zero on this map and the earlier "flags ignored" concern
(G-3) has **no behavioural impact** for it: the flags are uniformly 0.

**The real selection mechanism is the physic white/black lists** (§5.4). Audit of the 617
distinct models in the map's `sceneinfo_full` against the lists
(`proof/collision/physic/audit_lists.txt`):

| Category | Models |
|---|---|
| folder whitelist only | 557 |
| folder white **and** black (`树` trees) | 30 |
| folder white + file white | 18 |
| no list hit (doodad/effect meshes) | 6 |
| folder white + file black | 5 |
| file white + file black | 1 |
| **file list hits** | 19 white / 6 black |

**Implemented rule (hypothesis H1, `export_structure_collision.py --physic-lists`):**
`file_black` always excludes; `folder_black` excludes unless the file is explicitly
`file_white`; otherwise the model must be `folder_white` or `file_white`. SpeedTree `.srt`
objects bypass the lists because their collision comes from the separate
`<base>.CollisionMesh` path (trees do block in the client). Applying the rule to 龙门寻宝
accepts 4,903 of 4,963 objects; 60 are rejected: **29 file_black props** (wall lanterns, pen
holders, calligraphy vats, water vat, one wall segment), **23 blacklisted `.mesh` trees**, and
**8 non-whitelisted doodad/effect meshes**. The baker now extracts the lists and passes them
automatically (`--no-physic-lists` opts out).

Engine unit-template keys remain separate (`bUnitWalkable`, `bUnitCanPass`, `bBulletWalkable`,
`bBulletCanPass`, `bAutoPathing`, `nPathingType`, `fPathingHeight` —
`proof/movement/KG3DEngineX64_strings.txt:541388-541401`) and are not part of the static-mesh
gate; their consumers are still unreversed (G-21).

### 8.5 SpeedTree and LOD [DISASM/DATA]

Trees are `.srt` world objects; collision is `<base>.CollisionMesh` used verbatim (see §5.4).
68 shipped degenerate fragments on 龙门寻宝 are replaced by measured trunk prisms (§9.2).
`bUseLODMeshCollision=1` means the LOD mesh may serve as collision; our exporter uses the base
visual mesh only (gap G-4).

---

## 9. Foliage, SpeedTree and the bake pipeline

This is the **currently deployed collision path in our clients**. It is a faithful *subset*:
static triangle geometry derived from the game's own placement files.

### 9.1 `.foliage` FOLI v1 format [DISASM/DATA]

Decoder: `tools/decode_foliage.py` (reverse-engineered from `SceneManagerx64.dll`
`SceneFileLoader_Foliage_Binary::LoadInstances 0x180020700` / `ReadInstanceData 0x1800213A0`
/ readers `0x1800219A0/0x180021A40/0x180021AE0`).

```
header 40 B: u32 'FOLI' (0x494C4F46), u32 version=1, u32 fileLength,
             u32 unitCount, u32 totalInstances, u32 flags
unitCount × 72 B: GUID(38) ... +0x40 f32 baseZ, +0x44 u32 cellCount
flags bit2/3/4 add u32 mask/level/extra per unit (bits 0/1 rejected: editor head / dense)
per cell entry: u16 cell (lo=cellX, hi=cellY), u8 count
instance: u32 flags;
          x,y,z  = Readfloat(mode 0,2,4 /100)
          normal = 2×(u8/100−1) + third ±sqrt(...)
          scale  = 1 or 3 × Readfloat(mode 8,0xA,0xC /100)
          rotation: 2 bytes b/255·2π (yaw = first) OR bit14 skip
          u34 = patternId | texture<<15   (low 15 bits = pattern)
          Readfloat(0x15 /10), ReadUint1(0x1D); skip bits 23(+1)/27(+1)/28(+0x40)/31(+4)
position: (x,y,z); cell bias (cellX*400, baseZ, cellY*400);
region origin (−102400 + ix*51200, −102400 + iz*51200)
```

Validated byte-exact on all 13 龙门寻宝 files. Patterns: 2/3 grass (`.srt`, no mesh), 4
deadwood, 5 cactus, 6/7 rocks (`docs/FULL_MAP_COLLISION.md:26-48`).

### 9.2 Baked binary formats [DATA]

**FCOL v1** (`foliage_collision.bin`, `tools/export_foliage_collision.py:107-128`):

```
u32 'FCOL' (0x4C4F4346), u32 version=1, u32 meshCount, u32 instanceCount
mesh:     u32 patternId, f32 sceneScale, u32 vertCount, u32 triCount,
          f32 verts[3V], u32 tris[3T]
instance: u32 patternId, f32 x,y,z,yaw,scale
```

**FCOL v2** (`structure_collision.bin`, `tools/export_structure_collision.py:271-288`):

```
u32 'FCOL', u32 version=2, u32 meshCount, u32 instanceCount
mesh:     u32 vertCount, u32 triCount, f32 verts[3V], u32 tris[3T]
instance: u32 meshIndex, f32 m[16] (row-vector w=l·M), u32 pad,
          f32 bboxMin[3], f32 bboxMax[3]
```

**CFGL sidecar** (`<bin>.cflags`, camara-fix): `u32 0x474C4643 ('CFLG'), u32 count,
u8 flags[count]` — one `bObscatleCamera` byte per mesh, order-aligned with the bin meshes
(§5.4, `docs/CAMERA_PENETRATION_PLAN.md:165-186`).

### 9.3 SpeedTree and degenerate-tree measurement [DATA]

Rule: `.srt` → `<base>.CollisionMesh` sibling. A shipped CollisionMesh is rejected as
degenerate when `height < 150` **or** XZ extent `< 30`; then a trunk prism is measured from the
visual `<base>.mesh`: select vertices within `250/scale` local units of the mesh base (retry
2×), 2-D monotone-chain convex hull, extrude between base and base+250 u into a capped prism
(`export_structure_collision.py:113-133,238-257`; `docs/FULL_MAP_COLLISION.md:94-116`).
Trees with neither usable CollisionMesh nor visual mesh stay walk-through, as in the client.

### 9.4 Bake orchestration and coverage [HOST]

`tools/bake_map_collision.py` steps: read `RegionTableSize` from `<map>_sceneinfo.json`
(default 8×8) → extract `.foliage` → pattern table → 4 pattern meshes → run both exporters into
`<map>_foliage_collision.bin` / `<map>_structure_collision.bin` (+ `.cflags` with
`--flags`, camara-fix) → optional copy to `bin64\collision_data`. `MapSelector.cs` triggers the
bake and launches the host with `MAP_PATH`/`MAP_PLAYER`.

Baked coverage (2026-09-22, verified by parsing the bins):

| map | foliage bin | structure bin | runtime load |
|---|---|---|---|
| 龙门寻宝 | 4 meshes / 394 inst | 695 meshes / 4,957 inst | 5,351 inst / 699 meshes |
| 龙门寻宝_夜晚 | 4 / 394 | 709 / 5,127 | 5,521 / 713 |
| 白龙绝境 | 4 / 0 | 822 / 5,377 | 5,377 / 826 |
| 天原绝境 | 4 / 279 | 535 / 6,393 | 6,672 / 539 |
| 海岛绝境 | 4 / 0 | 245 / 4,177 | 4,177 / 249 |

(`docs/FULL_MAP_COLLISION.md:227-241`; bin headers re-parsed 2026-09-28.)

### 9.5 Runtime library (`FoliageCollision.cs`) [HOST]

* Loads v1 then v2 (+ `.cflags`), builds an 800-u instance hash grid and a per-mesh CSR
  triangle grid (`n = clamp(ceil(sqrt(triCount)), 4, 64)`).
* Player = vertical capsule; 6 samples along the axis; capsule points transformed to mesh
  local space; distances measured in world space through the instance 3×3 (exact under
  anisotropic scale); deepest contact wins.
* `Resolve`: 3 iterations; candidates `radius+600`; AABB pre-cull; step-up when contact normal
  is horizontal and instance top ≤ `py+70`; push-out `p += n*depth`; ground from up-facing
  contacts (`ny>0.55`).
* `SupportHeight`: highest up-facing triangle under the point within `[yLow,yHigh]`
  (normal gate `ny/|n| ≥ 0.5`).
* `Raycast` (camara-fix): Möller–Trumbore, optional `structuresOnly`, `frontFacesOnly`,
  `cameraGate` (`.cflags`); exposes `LastInst`/`LastTri`.

Known fidelity bugs (gap register G-5..G-8): v1 `sceneScale` may be applied twice
(exporter writes `scale*sceneScale`, runtime multiplies by the mesh-record sceneScale again);
non-uniform instance scales are dropped (only `scale[0]` survives); `comLogic` flags ignored;
per-map `.cflags` exist only for the five local maps.

---

## 10. Dynamic world objects

### 10.1 Doodads [DISASM/DATA]

Doodads are the runtime dynamic entities (doors, chests, corpses, NPC-drops, herbs/minerals).
Evidence: `KRLDoodad::{SetSelectable, LoadCorpseSocket, BindSfx, UpdatePosition,
GetTerrainSlopeRotationLH}` (`proof/netcode/JX3RepresentX64_net_strings.txt:311-314,2089`),
`KGameWorldHandler::{CreateDoodadEntity, OnDoodadBeHit, OnDoodadAfterHit, OnRemoveDoodad}`
(:2280-2318,3103), loaders `KG3DNpcDoodadReader::{SearchKrlFile, LoadNpcTemplate,
LoadDoodadTemplate, LoadDoodadTxt, LoadReplaceTab}` (`kg3dsceneresapi_strings.txt:65341-65836`),
tables `settings/doodadtemplate.tab`, `settings/NpcTemplate.tab`, `represent/doodad/doodad.txt`,
`data/path/doodad.tab` / `doodad_srt.tab`. Sync chain fully documented:
`OnSyncNewDoodad` `0x18019D510` (68 B), `OnSyncDoodadState` `0x1801975F0` (14 B),
`OnSyncLootList` `0x18019BA60`, `DoApplyLootList` 0x4D, `DoLootMoney` 0x51
(`docs/netcode/JX3_LOOT_PROTOCOL_LAYOUTS.md:14-105`).
Doodad kinds include `DOOR`, `CHAIR`, `TREASURE`, `BANQUET`, `CRAFT_HERB/MINERAL`, `NPCDROP`,
`DIALOG`, `QUEST`, `CORPSE` (`proof/gravity/JX3ClientX64_exe_strings.txt:717895-717935`).
Doodad table columns include `DynamicObstacle`, `DisableNavObstacle`, `Kind=7` containers with
`OpenPrepareFrame`, `OverLootFrame`, `CanPick`, `MaxLootRange` (mode rows), `CanOperateEach`
(`proof/netcode/mode_juejing/doodad_tables_parse.txt:20-47`,
`docs/netcode/JX3_MODE_LOOT_SYSTEM.md:55-101,151,169`).

Client collision behaviour: doodads can contribute dynamic obstacles and nav obstacles (flags
above); selection/pick path is `KCharacter::DoPickPrepare/OnPickPrepare/OnBreakPicking`
(`JX3ClientX64_exe_strings.txt:755270-755304`), Represent `PickDoodad`
(`JX3RepresentX64_strings.txt:748573,749205`). No decoded per-doodad collider shape; the
server owns interaction legality (range, `MaxLootRange=5` in 绝境 MapList rows).

### 10.2 Advanced / movable dynamic obstacles [DISASM/NAME]

`KAdvancedDynamicObstacle::{Add, Remove, ChangeState, CheckState, CaculatePoints,
ChangeObstacle}` (`JX3ClientX64_exe_strings.txt:754539-754576`), asserted group cap
`MAX_OBSTACLE_GROUP`, radius `m_nRadius > 0`, collision helpers
`CheckCollisionRange(pObstacle,x,y,z,faceDir)` / `CheckOtherCollision(0,pObstacle,...)` in
`KScene` (`:709646-709701`). Network handlers:
`KPlayerClient::{OnAddAdvancedDynamicObstacle, OnRemoveAdvancedDynamicObstacle,
OnIgnoreDynamicObstacleState}` (`:707635-709030`). Caps: `CloseAdvancedDynamicObstacleFlag`,
`MaxAdvancedDynamicObstacleCountPerRegion`, `ADVANCED_DYNAMIC_OBSTACLE` (`:738999-739007`).
Flags `DynamicObstacle` / `Use3DObstacle` on world objects and skills
(`:736935,740903`, `proof/pvp/cast/targeting_flags.txt:2`). Editor buffers
`KEditorObstacle::Serialize`, `MAX_OBSTACLE_BUFFER_SIZE`, `MAX_MOVABLE_OBSTACLE_BUFFER_SIZE`
(`proof/netcode/JX3LogicEditOperation_net_strings.txt:2794,3124-3170`). Homeland land
obstacles use the same family with HTTP apply/save tokens
(`JX3ClientX64_exe_strings.txt:747461-752800`).

### 10.3 State-machine props and editor objects [IL]

`KGSceneCLR::AddStateMachineModel` / `ClearStateMachineModel`
(`engine_host_spike/recon_managed_api.txt:673-674`). Editor IL: `StateMachineManager`,
`LoadAllStateMachineToScene` calls `AddStateMachineModel(uuid, statePath, pos, quat, scale)`
(`proof/engine_host/recon_actor_il.txt:46-131`); `LoadSceneStateMachineInfoToList` scans
`entities\sceneinfo\*.json` `worldObjects` whose `comRender.actorModel` ends `.state`, strips to
`.ini`, reads `actorLocalMatrix`/`uuid` (`recon_actor_il2.txt:1-234`);
`EPT_Action_StateMachine_IniPath=1250`, `StateID=1251`, `EAT_StateMachine=98`
(`recon_actor_il3.txt:1255-1256,1415`). Engine classes `KGStateMachine`,
`KGStateMachineState` (`proof/movement/KG3DEngineX64_strings.txt:602649-655`).
State machines are visual/animation state holders; whether they carry colliders is not
decoded (gap G-9).

### 10.4 Platforms, conveyors, carriers [NAME/DATA]

* Auto-move editor tracks: `EAT_AutoMove=73`, `EPT_Action_AutoMove_Axis/Speed/AutoDropGround/
  LocalAxis=722-725` (`recon_actor_il3.txt:727-730,1390`).
* Conveyor: `SetConveyorBeltParam` / `OnSetConveyorBeltParam` exports (`SIMWorldX64_exports.txt:35,44`),
  config `represent/physic/physic_conveyor_belt_param.krl.txt` (`proof/gravity/filepath.ini:369`),
  Lua API `PhysxComponent::LuaSetConveyorBeltParam` (`JX3RepresentX64_strings.txt:740191-740599`).
* Carriers / manned space: `KRLMannedSpace::{BindCharacter, UnbindCharacter, SetSelectable,
  LoadSkillBuffPart}`, `KRLLocalCharacter::MountMannedSpace`,
  `MannedSpace=Represent/rides/mannedspace.krl.txt`
  (`JX3RepresentX64_net_strings.txt:320-322,2101`, `filepath.ini:106`).
* Dynamic-body Lua surface: `LuaCreateShape`, `LuaCreateRevoluteJoint`, `LuaSetVelocity`,
  `LuaSetAngularVelocity`, `LuaSetLinearDamping/AddForce/AddTorque/AddPhysx`
  (`JX3RepresentX64_strings.txt:740191-740813`).
* Scene-object physics enums: `KG3D_SCENE_OBJECT_PHYSICS_TYPE`, `..._GEOMETRY_TYPE`,
  `KG3D_SCENE_OBJECT_TYPE`, `KG3D_JOINT_TYPE`, `KG3D_DestructionComponentType`
  (`recon_managed_api.txt:35-140`).

### 10.5 Summary of reproduction status

| Object class | Client collision | Reproducible now |
|---|---|---|
| static world (sceneinfo_full) | PhysX actors | bake (§9) |
| foliage solids | PhysX actors from `.foliage` | bake (§9) |
| SpeedTree | `.CollisionMesh` | bake (§9) |
| doodads | dynamic/nav obstacle flags | server-model only |
| advanced/movable obstacles | `KAdvancedDynamicObstacle` points/radius | no (gap G-10) |
| state-machine props | unknown collider link | no (gap G-9) |
| conveyors | Px conveyor | no (gap G-11) |
| carriers | character binding, inherited motion | no (gap G-11) |

# Part III — Bodies and movement

## 11. Character body and kinematic solver

### 11.1 What the body is (and is not) [DISASM + negative evidence]

The repo previously assumed a PhysX character controller (`PxCapsuleControllerDesc`). That claim
is **not backed by any dump in the repository**: no `PxController`, `PxCCT`, or
`PxCapsuleControllerDesc` strings exist. What is proven:

* the gameplay body is a **kinematic capsule + game-side foot/trajectory solver** tracked by
  `SIMWorldX64` (shape builder `PhysicScene::_AddCapsules`, semantic keys `capsules radius` /
  `capsules length`, `proof/gravity/SIMWorldX64_strings.txt:18727,18896,18899`);
* the movement/support logic lives in `KCharacter` (logic DLL) with a represent-side controller
  component `KRLCharacterControllerComponent` / `KGameWorldCharacterController::GetMoveInfo`
  (`proof/gravity/JX3RepresentX64_strings.txt:753913,777325,804668`);
* `PhysicsEngineX64` additionally creates a PhysX controller manager when a physics scene is
  built (`PxCreateControllerManager(scene,0)` `0x180018C40`) with parameters derived from a
  scale vector by multipliers `0.01`, `0.025`, `0.2`, `0.04` and constant `0.4`
  (`proof/gravity/disasm/physics_scene_setup.txt:37-67`). **Which multiplier maps to radius,
  half-height, slope limit, step offset, contact offset/skin is unidentified** (gap G-1).
  This path may serve the editor/cinematic avatar rather than the online character.

**Resolved constants (2026-09-28, `proof/collision/disasm/physics_controller_setup.txt`):** the
multipliers are exactly `0.01`, `0.025`, `0.2`, `0.04` applied to scale components (x for
three of them, z for one) plus the constant `0.4`; the same function (`PhysicsScene::_InitPhysXScene`
`0x180018990`) builds the scene description with gravity from the caller's vector or
`(0, -9.81, 0)`, a default filter shader fallback `0x1800169A9`, scene flags
`eENABLE_ACTIVETRANSFORMS (0x2)` always plus `eENABLE_CCD (0x4)` when `[params+0x20]` and
`eREQUIRE_RW_LOCK (0x1000)` when `[params+0x21]`, creates the controller manager via
`PxCreateControllerManager(scene, 0)` and stores it at `PhysicsScene+0x10`; the fixed-step tick
(`0x180018C60`) accumulates dt/1000 into `+0x34` against `+0x30` with a 20 ms step and calls
scene vt[`0x318`], vt[`0x1B0`], vt[`0x1D0`], vt[`0x320`]. **Which product maps to radius,
half-height, slope limit, step offset and skin is still unproven** — the honest state of G-1.
The named shape library (`physic_shape_param.krl.txt`, capsule r50/l50 at index 6) is a
separate per-object shape table, not the player body.

Further G-1 evidence (2026-09-28): `PhysicsEngineX64.dll` really does construct PhysX
characters — RTTI strings for `.?AVPxCapsuleControllerDesc@physx@@`,
`.?AVPxControllerDesc@physx@@`, `.?AVPhysicsController@PhysicsEngine@@`,
`.?AUIPhysicsController@PhysicsEngine@@` and `PhysicsEngine::_CreateCapsule`
(`proof/collision/recon/PhysicsEngineX64_strings.txt`), and
`PhysX3CharacterKinematic_x64.dll` exports the 3.3.4 prototype
`PxControllerManager::createController(PxPhysics&, PxScene*, const PxControllerDesc&)`.
The desc validator compiled into the game (`PxControllerDesc::isValid`,
`proof/collision/disasm/pxcontrollerdesc_defaults.txt`, xref to the deprecation message
`0x1800fa250`) checks floats at desc offsets `+0x2c`, `+0x38`, `+0x3c`, `+0x40`, `+0x44`
(≥ 0) and `+0x48` (≤ 1) plus the deprecated/current callback pair at `+0x50`/`+0x58` and the
material at `+0x70` — so the live desc layout is partially mapped; the exact game-side field
assignment (which value is radius/half-height vs slope/step) remains G-1.

### 11.2 The SIMWorld kinematic solver [NAME]

The solver is configured through semantic keys captured in `SIMWorldX64`:

| Key | Probable role |
|---|---|
| `solverFlag` / `solverFlagUnevenTerrian` / `solverFlagTwoBone` | solver mode selectors (uneven terrain, two-bone foot) |
| `useGroundPenetrationFixup` | push out of ground penetration |
| `useTrajectorySlopeAlignment` | align motion to slope |
| `footAlignToSurfaceAngleLimit` / `footAlignToSurfaceMaxSlopeAngle` | foot IK/support angle limits |
| `footLiftingHeightLimit` | max lift before foot leaves ground |
| `inputDeltaTime`, `inputGroundContactTime` | per-step inputs |
| `inputAchievedRequestedMovement` | movement actually achieved vs requested |
| `inputCharacterWorldRootTransform(Old)`, `inputCharacterTrajectoryDeltaPos/Quat` | pose trajectory in/out |
| `straightestLegFactor` | foot solver tuning |

(`proof/gravity/SIMWorldX64_strings.txt:18535-18573`, `proof/netcode/SIMWorldX64_net_strings.txt:12-57`.)
No consumer/disassembly of these keys exists yet; they are the top target of RE pass P0-1
(§29).

### 11.3 Collision shape and actor interaction [NAME]

* Character capsules are registered with SIMWorld (`_AddCapsules`); the world also tracks
  `npclist` / `npcid` / `remoteplayerlist` / `subactor` / `worldmatrix`
  (`SIMWorldX64_strings.txt:18822-19063`) — i.e. NPCs and remote players have capsules known to
  the physics world, but the collision filtering between characters is not decoded.
* Representation exposes a per-character collision switch:
  `KRLCharacter::SetInteractor(bManaged, bCollision, nType)` /
  `LuaSetInteractor` (`JX3RepresentX64_strings.txt:743276-743287`), plus mutual intersection
  responses `KRLCharacter::ResponseIntersectMutual` / `ResponseIntersect`,
  `OnEnableIntersect`, `IntersectFeedback` (`JX3RepresentX64_net_strings.txt:731487-759174`).
  These look like the crowd push-apart/soft-collision system (gap G-12).
* Character position support in logic: `KCharacter::GetNearByObstacle` (`:755352`),
  `IgnoreDynamicObstacle` (`:755369`), `AdjustPosZByMiniScene(±Move)` (`:755441-755448`),
  `TryMoveBeforehand` / `TryMoveDelta` (`:755358-755361`).
* `KCharacter::GetFloorHeight` exists in represent (`JX3RepresentX64_strings.txt:747018`) and
  SIMWorld provides `PxWorld::GetFloorHeight` — this is the authoritative ground query.

### 11.4 Reproduction recipe (client-observable)

1. Represent the body as a vertical capsule of height `H` and radius `r`. Known constraint:
   the visual adult body is 181.64 u tall; ragdoll limbs are 5–12 u, so a plausible radius is
   15–25 u. **The exact gameplay capsule radius/height has not been extracted**; our clients
   use 17/116 (`RebornClient`) and 25/170 (`MapSpike`).
2. Each logic frame (15 Hz): integrate intent (§12), then resolve support:
   `ground = max(terrain_height, highest walkable static surface under the capsule)`;
   clamp `y` to ground when grounded; start a fall when `y − ground` exceeds the fall start
   rule (§13).
3. Block horizontal motion when a step would raise ground by more than the climb budget
   (client step offset unknown; our rule: 70 u) and slide along the blocking axis.
4. Actor-vs-actor blocking is **not reproduced** (no proven behaviour; treat characters as
   non-colliding unless G-12 closes).

---

## 12. Movement model (15 Hz integer)

### 12.1 Input to state [DISASM]

```
hotkey (default.txt) → UI Lua bindings (control.lua ids) → CommitInput 0x1805E3270
  → KCharacter walk/run/strafe/turn methods → move-state field +0x1F4
```

| Field | Meaning |
|---|---|
| `+0x1F4` | move state (2 walk, 3 run, 4 jump, 7 swim, 0x17 sprint-dash; full enum in `proof/pvp/buff_control_system.md:236-255`) |
| `+0x26C` | heading |
| `+0x44` | facing |
| `+0x48` | turn rate |
| `+0x320` | gravity accumulator |
| `+0x2F8` | speed |
| `+0x170` | scaled speed / ground term used by `ProcessVerticalMove` |
| `+0xC08` | turn/sync counter (`docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md:151-174`; `proof/pvp/netcode/disasm/KPlayerClient__OnSyncMoveState.txt`) |

Ground-capable move states are masked by `0x1000011E` = `{1,2,3,4,8,0x1C}`
(`docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md:180-181,338-343`).

### 12.2 Movement methods [DISASM]

| Method | Address | Notes |
|---|---|---|
| `KCharacter::WalkTo` | `0x140320F30` | walk destination |
| `KCharacter::RunTo` | `0x14031B540` | run destination |
| `KCharacter::MoveTo` | `0x140314230` | generic move |
| `KCharacter::MoveCharacter` | `KGSO3WorldClientInterface` | server apply path |
| `KCharacter::TurnTo` / `LuaTurnTo` / `LuaTurnToCharacter` / `OnSetTurnRange` | — | turning, range clamp |
| `KCharacter::ProcessAcceleration` | `0x1403165D0` | per-frame accel/gravity (`proof/gravity/disasm/process_acceleration.txt:74-236`) |
| `KCharacter::ProcessVerticalMove` | `0x140318C50` | `y += Vz`, ground clamp, landing (`process_vertical_move.txt:81-199,1439-1453`) |
| `KCharacter::ProcessDropSpeed` | `0x140316BE0` | fall speed, slope stop, air stop (`process_drop_speed.txt:10-157`) |
| `KCharacter::CellMoveNav` / `ProcessNavAutoFly` | — | nav-follow movement |
| `KCharacter::EmendateDestCoordinate(WithScene)` | — | destination correction vs scene |
| `KCharacter::AdjustPosZ` / `AdjustPosZByMiniScene` | — | vertical settle on static scene |
| `KCharacter::UpdateWorldPosition` / `AdjustLocalPosition` | — | transform update |
| `KCharacter::LuaWorldToLocalInMovableObstacle` | — | local space on movable obstacles (calls at `client_movement_symbols.txt:2218-2251`) |

### 12.3 The integer integration (verified)

Constants (`proof/gravity/disasm/*`, `docs/JX3_GRAVITY_RESEARCH.md:63-104`):

```
takeoff velocity   Vz0 = 90 u/frame
gravity            g   = 11 u/frame²   (per-jump-table triple, clamped [0,31])
velocity clamps    Vxy ∈ [0,127], Vz ∈ [-2048, 2047]
integration order  y += Vz; Vz -= g        (order B, matches jump-arc compensation)
tick               1/15 s
```

Per-school takeoff triples come from `JumpParam.tab` (23 schools × 183 columns,
`proof/gravity/verification.txt:4-9`); discrete apex ≈ 414 u / 18 air frames. Continuous
approximation is acceptable if the discrete order is kept for reproduction of animation timing.
Turn penalty: a direction change > `0x50/0x100` (112.5°) halves speed
(`docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md:217-241`).

Slope: the terrain cell packs a slope value `(cell >> 1) & 7`; slope ≤ 8 forces `Vz = 0`
(stand on slope), per `process_drop_speed.txt:48`, `docs/JX3_GRAVITY_RESEARCH.md:120-168`.
Ledge/fall behaviour is emergent from `ProcessVerticalMove` ground clamp + `ProcessDropSpeed`;
there is no explicit "ledge" symbol (gap G-13: exact step-up offset unknown; our clients use
70 u and a 150-u ledge rule).

### 12.4 Host implementations [HOST]

| Client | gravity | jump | walk / run / sprint | capsule | step / ledge |
|---|---|---|---|---|---|
| `engine_host_spike/MapSpike.cs` | −1289 | 703 | 200 / 667 | 25 / 170 | 70 / 150 |
| `client/RebornClient.cs` | −2475 | 1350 | 96 / 320 / 563.2 | 17 / 116 | 70 / 150 |
| ability clone (`ability_sandbox/rb`) | same as RebornClient | | | | |

Both use: terrain slope block at 70-u rise with single-axis slide, structure
`SupportHeight`/`Resolve`, ground snap, `grounded` gating for jump, Euler gravity. Neither
implements the real 15 Hz state machine, turn model, or table-driven jump (gap G-14).

---

## 13. Jump, fall, swim, fly, parkour

### 13.1 Data tables [DATA]

| Table | Content |
|---|---|
| `proof/gravity/JumpParam.tab` | 23 schools × 183 cols; per-school jump triplets `(VelocityXY, VelocityZ, Gravity)` + `End`; wall/horse/dash variants (`docs/JX3_GRAVITY_RESEARCH.md:471-535`) |
| `proof/gravity/JumpFrameParam.tab` | 128 per-frame keyframes `(Frame, VelocityXY, VelocityZ, DirectionXY)`; **only schools 10/11 shipped rows in this build** (`:515-517`) |
| `proof/gravity/Sprint.tab` | sprint caps (school 0/1/2 max Z 900 u/f, school 17 1000 u/f) |
| `proof/gravity/SkillMove.tab` | 919 rows: `IngoreGravity`, `TotalFrame`, `SkillMoveOnlyFly`, `SkillMoveDeath`, `SkillMoveEndButKeepVelocity`, `CanJump`, `CanBanMask`, per-frame `FrameN/VelocityXYN/VelocityZN/DirectionXYN` |
| `proof/gravity/ParkourMove.tab` | parkour moves |
| `proof/gravity/player_suspend.krl.txt` | landing thresholds: `FallDownHeightFloor=500`, `AdjustFloor=50`, water variants 500/50 |

### 13.2 Phase model [DOC/DISASM]

Jump phases and table selection are documented in `docs/JX3_GRAVITY_RESEARCH.md:170-262`:
takeoff → ascending (frame curve) → apex → fall (`ProcessDropSpeed`) → land / roll / death.
Fall caps and hard clamps in `verification.txt:42-54`. Roll threshold 500 u; water landing
thresholds 500/50. Fall animation selection via `Fall%s%sAnimation` (`fall_animation.txt:4`).

### 13.3 Swim [DISASM]

* `KCharacter::GetWaterline` `0x140312400`: waterline from height with magic divisors —
  NPC `h/20 ... 0.30·h`, player factor ≈ `0.586·h` (`get_waterline.txt:10-17`); submersion
  helper `0x140312440`.
* `KCharacter::SwimTo` `0x14031D770` valid in states 6/7, sets state 7 (`swim_to.txt:4`);
  swim speed `CharacterSwimSpeed=20` 尺/s (`proof/gravity/number.krl.txt`).
* Flags/ops: `CHECK_CAN_SWIM`, `HORSE_CAN_SWIM`, `ADD_SWIM_WATER_LINE_COF`,
  `ON_SWIM`, `ON_SWIM_JUMP` (`JX3ClientX64_exe_strings.txt:716702-719855`).
  Represent clips `SwimStand/Forward/Backward/Left/Right` (`camera_player_config.txt:2602-2610`).
* Per-frame swim step not decoded (gap G-15; see §18).

### 13.4 Fly and parkour [DISASM/NAME]

`FlyTo` `0x140310B70`, `EndFlyJump` `0x140310960` (asserts state 0x21),
`BirdFlyTo` `0x14030C940`, `OnParkour` `0x140314510`, `SprintDash` `0x14031CC00`
(`proof/gravity/disasm/{fly_to,end_fly_jump,bird_fly_to,on_parkour,sprint_dash}.txt`).
Lightfoot/轻功 chains are table-driven via `SkillMove` rows; chain-segment timing frame
condition open (`docs/REBORN_JUMP_FALL_SPEC.md:116-127`).

### 13.5 Reproduction blueprint

Use `docs/REBORN_JUMP_FALL_SPEC.md` as the implementation-ready spec: constants table,
per-frame pseudocode, single-jump numbers, status tables per subsystem. The discrete order
(`y += v; v -= g`) must be kept if animation alignment matters. SkillMove rows provide dash,
fly, death and forced-motion trajectories; `IngoreGravity` and `SkillMoveEndButKeepVelocity`
flags control blending with the integrator.

---

## 14. Ragdoll, hit reactions, death

### 14.1 Ragdoll [DISASM/DATA]

* Engine entry `PhysicsEngine::KPhysicsRagdoll::AddPhysicsBone` `0x180002AE0`
  (`engine_host_spike/recon_physics_funcs.txt:7`).
* Activation from skill-move config: ID bit 30 gate, component flag `[+0x30] & 0x2000000`,
  timestamp `[+0x74B8]`, call with `0x40`, `RagdollTime [rsi+0x22C]`
  (`proof/netcode/disasm/skillmove_config_use.txt:826-864`).
* Config: `+0x22C` RagdollTime, `+0x230` RagdollBlendWeight, `+0x228` SkillTurningTime
  (`proof/gravity/disasm/ragdoll.txt:995-1013`); values `RagdollTime=5000`,
  `RagdollBlendWeight=1.0` (`proof/gravity/number.krl.txt:145-146`).
* Body presets: 4 groups × **11 rigid bodies** each, with fields
  `socket / rigid_id / radius / length / pos / yaw-pitch-roll`; sockets include
  `bip01 l/r foretwist`, `upperarm`, `calf`, `thigh`, `foot`, `pelvis`; radii 5–8 u, lengths
  10–12 u (`proof/gravity/physic_character_param.krl.txt:7-546`).
* Death move: `SkillMove.tab` rows with `SkillMoveDeath=1` (25 of 918; first id 2, 50 frames);
  state `FALL_DEATH` (`JX3RepresentX64_strings.txt:752471`).
* Config switch `bEnableRagdoll` (`proof/netcode/adapter_strings.txt:168788`); actor switch
  `EnableCharacterStatic=1`, destructible `DestructibleRadius=20`, `DestructibleDamage=100`
  (`physic_character_param.krl.txt:1-5`).
* Ragdoll blend/update loop not traced (gap G-16).

### 14.2 Hit reactions [DATA/NAME]

Server-driven; client only plays back. Skill columns (`proof/pvp/cast_cooldown_resources.md:372-401`,
`proof/pvp/attributes/skills_header.txt:114-117`):

| Column | Meaning | Example range |
|---|---|---|
| `CauseBeatBreak` | interrupt | 551 skills |
| `CauseBeatBack` | knockback | 3,325 skills |
| `HitStiffDelayFrame` | hit-stop delay | 1–15 frames |
| `HitStiffSkillMoveID` | forced SkillMove row | 881/891/893/906 used |
| `HitStiffVelocityXY` | knockback speed | 80–400 |
| `HitStiffAccelerateXY` | knockback accel | 1000–7000 |
| `IsExactHit` / `bIsExactHitInAir` | exact-hit variants | semantics open |

Concrete rows: `proof/pvp/cast/hitstiff_examples.txt:1-60`. Knockback displacement is applied
server-side through `atKnockedBackRate/atRepulsedRate/atPullRate = -1024` semantics
(`proof/pvp/buff_control_system.md:182-230`) and skill ops `GRAVITY_BASE/PERCENT`,
`CALL_KNOCKED_OFF_PARABOLA`, `CALL_REPULSED`, `REPULSED_RATE`
(`JX3ClientX64_exe_strings.txt:714271-716795`). Represent states/animations:
`ON_KNOCKED_DOWN/BACK/OFF`, `KNOCK_DOWN/BACK/OFF`, `PULL`, `REPULSED`
(`JX3RepresentX64_strings.txt:752311-762083`); shake tables `behit_shake.txt` (2/10/100 ms and
5/50/100 ms) and `skill_result.txt` (`proof/pvp/cast_cooldown_resources.md:399-401`).

### 14.3 Bone bounds [NAME]

`KG3DSkeletonBoneBound::{LoadFromFile, SaveToFile, AddBoneBoundFromAniFile,
AddBoneBoundFromModelST}`, `KG3DBip::GetSkeletonBoneBound`, `BoneBound%d`
(`proof/movement/KG3DEngineX64_strings.txt:545914-563951`); managed enums
`KG3D_BONE_BOUND_TYPE/UNIQUE/STATE`, `KG3D_RIGIDBODY_COLLISION_GROUP`,
`KG3D_PhysicsBoneIndexPair`, `KG3D_JOINT_TYPE` (`engine_host_spike/recon_managed_api.txt:41-45,559`).
Runtime consumers: `KG3DModel::IsRayIntersectBoneBox` / `UpdateBoneOBBox` / `DrawBoneOBBox`,
`vBoneBox`, `KG3DMesh::CreateOBBoxOfBone` (§21.2). Proven only for camera near-ray so far.

# Part IV — Queries and interaction

## 15. Rays, filters and camera obstruction

### 15.1 Engine scene-ray family [DISASM/NAME]

| Symbol | Kind | Purpose |
|---|---|---|
| `RayIntersectionTerrain` (+ `GroundHeightGetter` functor) | string `0x0083A2F0` | terrain ground-height ray |
| `RayIntersectionVertical` | string `0x0083A370` | vertical ray (column scan) |
| `RayIntersectionEntity` (+ `_SceneBlock`, `_TerrainNodes`, `CNodeVisitor`) | strings `0x0083A410-0x0083A650` | entity container ray across scene nodes |
| `KG3DIntersectionFilter` | RTTI `0x00831C18` | result filter |
| `KG3DScene::RayIntersectRenderEntity` | `0x1804A19F0` | editor scene entity-list ray (per-entity vt+0x930) |
| `KG3DSceneProxy::RayIntersectionModel` / `RayIntersectionWithObject` | adapter strings | model ray on the adapter proxy |
| `KG3DScene::GetPickRayFromWindowsPosition` / `PickRayWalk` | adapter strings | screen→world pick ray |
| `KG3DModel::IsRayIntersectBoneBox` / `GetCameraNearRayIntersect` | `0x1801EF000` | per-model near/bone ray |

A plain horizontal scene ray named `RayIntersection` was not found in the captured string
tables; the horizontal gameplay ray is the `SIMWorld` path (§5.5) and the editor horizontal ray
is the adapter's `RayIntersectionModel`.

### 15.2 Query wrapper, masks and filters [DISASM]

The camera obstruction query wrapper `0x180475800` passes option `1`; `0x180446920` maps option
bit 0 to internal mask `0x301` (`and r10d,1; edx=0x301; cmovne`) and attaches static filters:

* option 1 → `FilterCamera` `0x180946678` (RTTI `.?AUFilterCamera@?A0x573b9fc1@@`);
* option 2 → `FilterLogic` `0x180946688` (RTTI `.?AUFilterLogic@?A0x573b9fc1@@`).

Filter slot 0 `0x180446560` calls display-block getter `0x180254080` (returns `KG3DMesh+0x194`)
and tests `[+4]` = `bObscatleCamera`; non-zero → pass. Dispatcher `0x18032EA40` evaluates
backends by mask bits and keeps the nearest positive hit. Bit 9 reaches
`RayIntersectRenderEntity`; bit 8 reaches helpers `0x18032ED10 → 0x18032EF50/0x18032F0B0 →
0x18008AF20/0x18038B6E0/0x1803AA8B0`. **The exact `0x301` bit split is unresolved** (gap G-17).
Evidence: `proof/netcode/camera_wall_obstruction.txt:96-130,154-157,184-221,283-289`,
`docs/CAMERA_WALL_OBSTRUCTION.md:96-107,242-244`.

### 15.3 Camera obstruction loop [DISASM]

`KG3DTrackCamera` update:

1. gate `bObstructdAvert` at parent `+0x204` (compiled default 1);
2. helper `0x1804C1B40`: default `+0x15C == 0` → **5 rays** (centre + 4 corners); `+0x15C != 0`
   → **9 rays** (centre + 8 at π/4);
3. per ray: origin = probe point on the anchor side, direction = normalised anchor→camera,
   max distance = segment length, output init `FLT_MAX`;
4. nearest hit `h`; `u = normalize(C − A)`; `C' = A + u·max(0.001, h)`; no hit → unchanged;
5. clearance: `C'' = C' + normalize(A − C')·18.0` (constant `0x1806ED3F0`);
6. hysteresis window: 50 u when not obstructed / 100 u when already obstructed
   (state flags `+0x1CC`, `+0x1DC`);
7. flex spring: `S += (−fFlex·E − fDamp·S)·dt; X = desired + S·dt`, `fFlex=1.5`,
   `fDamp=2.828`.

Per-model near ray: `KG3DModel::GetCameraNearRayIntersect` `0x1801EF000` is gated by the same
`bObscatleCamera` mesh flag and tests element flag `0x20000`, then
`KG3DModel::IsRayIntersectBoneBox` (`camera_wall_obstruction.txt:158-170`).

### 15.4 Host implementation status [HOST]

| Aspect | main | camara-fix (`c189535`) |
|---|---|---|
| Obstruction source | terrain 14-step march only | baked `FoliageCollision.Raycast` + `EngineRay` terrain/scene/entity + vertical ladder + terrain march |
| Per-mesh gate | none | `.cflags` `bObscatleCamera` + foliage pattern table (6/7 = 0) |
| Response | pull to `max(150, t·len−40)`, ground clamp | `CameraObstruction` state machine: signed pull to `hit−18`, 50/100 hysteresis, spring 1.5/2.828 |
| 5/9 rays | n/a | 5 default, `RC_CAM_9RAY=1` for 9; 22-u probe footprint (`RC_CAM_OBSTR_FOOT`) |
| Final gate | n/a | re-cast camera→anchor, retract to `max(1, gg−25)` (deviation B5, to delete after parity) |
| Near behaviour | none | engine near-plane API absent; player parked 100000 u below at camera distance <90 and restored >150 |
| Look-at | managed `SetCameraPos` only | engine `cam->vt[+0x58](anchor)` via `camera_shim.dll` (default `RC_CAM_ENGINESET=1`) |

Host `EngineRay` RVAs (`client/EngineRay.cs:15-21`, module `KG3DEngineDX11EX64.dll`):
terrain `0x976260`, scene `0x975EE0`, space manager `0xA5E4C0`, entity backend `0x53CB80`,
vertical `0xA5E1D0`; terrain object at `[scene+0x950]` called via `vt+0xB8`; space object
`[[scene+0x910]+8]`; entity list `[scene+0xAD8]→+0x5F8`, count `+0x600`; scene-level ray is
guarded by a TEB cell (`gs:[0x58]`, index module+`0x26546C4`, cell `+0x89B0`).
Known host deviations: D5 vertical backend returns no hits in the editor scene; C2 invented
22-u footprint; C7 near plane unmeasured (getter deadlocks); D6 intermittent content AV under
fast view changes (mitigated with a 1.5 rad/s cap and full-load). Register:
`docs/CAMERA_HOST_DEVIATIONS.md`, plan: `docs/CAMERA_PENETRATION_PLAN.md`.

---

## 16. Picking, selection and targeting

### 16.1 Input semantics [DOC/DISASM]

LMB = `CAMERAORSELECTORMOVE` (rotate camera **or select under cursor**), RMB =
`CAMERAORSELECTORMOVESTICKY` (camera + character turn)
(`docs/CAMERA_INPUT_CONTROLS.md:19-29`); binding lines
`proof/movement/extracted/ui_hotkey_default.txt:35-36`. Cursor position functions:
`GetRLCursorPos` (view-manager `vt+0x30`) and `GetRLCursorScenePos` (screen→scene through
`0x18001740E` with global `+0x24AE8`) — recovered in
`reborn-camara-fix/proof/netcode/disasm/cursor_pos_fns.txt:1-175`; internals not annotated
(gap G-18).

### 16.2 Pick rays [DISASM/NAME]

Adapter/editor: `KG3DScene::GetPickRayFromWindowsPosition`, `PickRayWalk`,
`KG3D_Camera::GetPickRayFromWindowsPosition` (`proof/netcode/adapter_strings.txt:171855-173428`).
Math: `KBaseX64` `GetWorldPickRay` / `GetObjectLocalPickRay` / `ToObjectLocalPickRay`
(§6). Client pick flow: `KCharacter::DoPickPrepare` → `OnPickPrepare` / `OnBreakPickPrepare` /
`OnBreakPicking` (`JX3ClientX64_exe_strings.txt:755270-755304`); represent
`KRLLocalCharacter::PickDoodad` / `KRLRemoteCharacter::PickDoodad`, states `PICKING_DOODAD`,
`OTACTION_PICKING`, events `OnCharacterBegin/EndPickDoodad`, `HandleBegin/EndPickDoodad`
(`JX3RepresentX64_strings.txt:748573-757917`). Inventory pickup (`KInventory::PickUpItem`) is
non-spatial.

### 16.3 Target selection [DISASM/NAME]

* **No `DoSelectTarget` opcode exists** — targeting is client-side visual selection; combat
  casts carry the target/coordinates (`docs/pvp/...`); auto-target is a cast convenience.
* UI commands: `AttackTarget`, `FollowTarget`, `SearchNextTarget`, `SelectPrevTarget`,
  `SelectTargetTarget`, `SearchTarget_SwitchOnlyPlayer`
  (`proof/netcode/ui_lua_probe/out/ui/hotkey/bindings.ini:57-393`).
* Client auto-select: `KSkill::AutoSelectTarget`, `m_pBaseInfo->bAutoSelectTarget`,
  `ProcessDisableSelectTarget`, `m_nDisableSelectTargetCounter`
  (`proof/pvp/combat_netcode.md:93-97`); skills.tab column `AutoSelectTarget`; attributes
  `atAutoSelectTarget`, `atAutoSelectTargetByLifeless`.
* Lock target camera: `OnSetCharacterCameraLockTarget` (fn `0x180322D00`),
  `CameraLockTargetConfig` (`reborn-camara-fix/proof/netcode/disasm/locktarget_set.txt:4`;
  `docs/CAMERA_CONFIG_FILES.md:20,161`).
* **UI targeting logic decompiled (2026-09-28).** `ui/script/target.lua` / `skill.lua` are
  Lua 5.1 bytecode; unluac (fetched to temp, output stripped-debug but readable) produced
  `proof/collision/ui_scripts/{Target,target_b03,skill}.utf8.lua`. Recovered semantics
  (`target_b03.utf8.lua`):
  - candidate source `GetSearchTargetPlayer()`; selection by type
    `SelectTarget(TARGET.PLAYER|NPC, id)`;
  - `TARGET` enum includes `PLAYER`, `NPC`, `DOODAD`, `FURNITURE`, `DUMMY`, `NO_TARGET`;
  - filters: `CanSelectPlayer(id)`, `IsCorpseAndCanLoot(id)`, player priority
    `g_nTabPlayerPriority`, `bOnlyPlayer` setting, enemy/other setting token `"Enmey"`
    (`L1_1.ENMEY`), old/new-version path via `SearchTarget_IsOldVerion`;
  - `skill.lua` uses `SKILL_CAST_MODE.TARGET_SINGLE/TARGET_CHAIN/TARGET_AREA`, per-skill
    `nCastMode`, `bAutoSelectTarget`, `bForbidSelectTarget`, `bAllyTarget`,
    `IsSelfCastSkill/SetSelfCastSkill`, `SetAutoTarget/IsAutoTarget`, `Target_GetTargetData`,
    `TARGET.NO_TARGET`.
  Combined with the absence of a select opcode (§16.1), client targeting is a **local list
  selection feeding the cast packet**, not a replicated action. Residual gap: exact candidate
  ordering/filter priorities in the C++ `GetSearchTargetPlayer` (G-19 residual).

### 16.4 Ground-target reproduction (临时飞爪) [HOST]

The ability client implements the only ground-target path in our repo
(`ability_sandbox/rb/RebornClient.cs:1139-1187`): camera pos + a view basis, `tan(50°/2)`
hardcoded FOV, panel aspect, cursor NDC (`lastMousePt`) → ray → `engineRay.RayTerrain` (4000-u
probe) → clamp to `FEI_MAX_RANGE = 40 尺 = 2560 u` → `sampler.Sample` vertical snap → marker
mesh. Two-press targeting (first press aims, second confirms). This matches the skill data
(`PointArea`, `CastSkillXYZ`) but the FOV/range constants are invented approximations
(gap G-20).

---

## 17. Triggers, zones, volumes, interaction

### 17.1 Scene response plugin [DISASM/NAME]

`KG3DSceneResponseHolder::{RegisterDll, UnRegisterDll, LoadDLLFromConfig, GetSceneResponse}`
loads `KG3DSceneResponseX64.dll` from config (`proof/movement/KG3DEngineX64_strings.txt:563869-882`);
adapter side `KG3D_CreateSceneResponseData`, `bAsyncLoadSceneResponse`,
`KG3DSceneResponseManagerHolder::Init`, `KG3D_CreateSceneResponseManager`
(`proof/netcode/adapter_strings.txt:166867,168364,172087-172108`); represent holds
`g_pRL->m_pi3DSceneResponseMgr` and calls `LoadSceneResponseEntities`
(`JX3RepresentX64_strings.txt:738952,747249,778869`). This is the system through which
per-object semantics (`bUnitWalkable` … `bAutoPathing`) are surfaced to gameplay
(`docs/STRUCTURE_COLLISION_RESEARCH.md:21`). Behaviour not decoded (gap G-21).

### 17.2 Physics zones and triggers [NAME/DISASM]

* `ZoneMgr::{Init, ClearScene, _OnMeshLoaded, _AddBuildinMesh, _UpdateStatic, _UpdateDynamic,
  _ClearMeshDef}`, key `px zone phys level`, export `OnSetZonePhysLevel` `0x32ED0`
  (`proof/gravity/SIMWorldX64_strings.txt:18784-18968`, `SIMWorldX64_exports.txt:36`).
* Triggers are PhysX trigger shapes plus bookkeeping keys: `obj add/remove/update trigger`,
  `cam trigger`, `raytrigger`, `overlap triiger`, `overlap valid`, `raycheckquerymask`,
  `ray cast touch`, `ray has block`, `ray nb touches` (`SIMWorldX64_strings.txt:18851-19049`);
  PhysX enums `eTRIGGER_SHAPE`, `eTRIGGER_PAIRS`, `eREPORT_FOREIGN_OBJECTS_TO_TRIGGER_NOTIFY`,
  `eREPORT_TO_FOREIGN_CLIENTS_TRIGGER_NOTIFY`
  (`proof/movement/KG3DEngineX64_strings.txt:574166-574493`).
* Represent callbacks: `LOGICAL_TRIGGER_CALLBACK`, `OnTriggerEvent`,
  `RL_PHYSX_TRIGGER_STATUS_TYPE`, move-state `TRIGGER`
  (`JX3RepresentX64_strings.txt:738825,752584,752640,776161`). Map data carries
  `RepresentTrigger` / `LogicalObstacle` entries (`proof/netcode/mode_juejing/map_loader_paths.txt:9`).
* PhysX simulation events are retrievable (`PxWorld::GetSimulationEvent` `0x31FF0`;
  `PxSimulationEventCallback`, `PxContactModifyCallback` RTTIs in SIMWorld).

### 17.3 Environment volumes and indoor [DISASM/NAME]

`KG3DRepresentEnvironmentVolume::_ReadParamFromFile`, `Volume`, `ExteriorVolume`,
`InteriorVolume`, `KG3DRepresentVolume::{SaveToInformationBuffer, GetTranslation,
CreateFromScenePoly, _CreateMeshFromScenePoly}`
(`KG3DEngineX64_strings.txt:535069-535331`); `QueryPositionIsIndoor` registration/callback
(`adapter_strings.txt:170409-170415`); represent `max_check_is_indoor_pos_diff`
(`JX3RepresentX64_strings.txt:741836`); scene outdoor manager `m_lpSceneOutDoorSpaceMgr`
(`KG3DEngineX64_strings.txt:533500`). Used for rain/ambient/audio classification; collision
interaction unknown (gap G-22).

### 17.4 Interaction [NAME]

* Range: `CUSTOM_INTERACT_RANGE`, `ProcessCustomInteractRange`, `nCustomInteractRange`,
  `INTERACTION_ERROR` (`JX3ClientX64_exe_strings.txt:717104,724985,764263,770317`); value not
  extracted (gap G-23).
* Flow: `STKREPRESENT_EVENT_INTERACT_BEGIN/END`; `KRLRushState::{BeginInteract, NextInteract,
  SetInteractionFreeze, OnInteractionAnimationFinished}`,
  `KRLCharacter::{BeginInteract, EndInteract, UpdateInteractionActive, UpdateInteraction}`,
  `KGameWorldHandler::OnInteractBegin/End`, `krlEventAdaptor::HandleInteractBegin/End`
  (`JX3RepresentX64_strings.txt:745616-757271`). `InteractTitleEffectID`,
  `InteractTitleFaceAngle` (`:758347-350`).
* Homeland furniture: `SceneInteraction::InteractionManager` / `Interactor` with
  `GetNearbyObjectInfo`, `CharacterBind/Unbind`, `DummyBind/Unbind`, `GetTransmitPosition`
  (`:767840-768502`).
* Doors/chests are doodads (§10.1); open legality is server-validated (range/ownership);
  `KDoodad::CheckOpen`, `KPlayer::OpenDoodad`
  (`JX3ClientX64_exe_strings.txt:759897,760854-861`).

**Negative finding:** no `EnterArea`, `LeaveArea`, `AreaTrigger`, or `TriggerVolume` symbols
exist; the game uses callback-style triggers and skill-side area radii instead.

---

## 18. Water and fluids

### 18.1 Waterline and swim [DISASM]

* `KCharacter::GetWaterline` `0x140312400` computes the swim line from the water height with
  magic divisors: NPC `0x66666667` (÷20, 0.30·h), player `0x92492493` (≈0.586·h);
  submersion helper `0x140312440` (`proof/gravity/disasm/get_waterline.txt:4-26`).
* `KCharacter::SwimTo` `0x14031D770` (valid states 6/7 → state 7), speed 20 尺/s
  (`swim_to.txt:4`, `docs/JX3_GRAVITY_RESEARCH.md:296-311`).
* Represent surface queries: `GetWaterHeight`, `ValidWaterHeightDiff`, `GetAdjustWaterDelta`,
  `KRLCharacterFrameData::UpdateWaterHeight`, `KRLCharacter::UpdateIsUnderWater`,
  `CheckIsLogicWater`, `EnableNewWaterHeight`, `GetAdjustHeightForWater`, `bUnderWater`,
  `m_bIsInWater` (`JX3RepresentX64_strings.txt:741229-778980`).
* Landing: `FallDownHeightWater=500`, `FallDownAdjustWater=50`, `FallWaterAnimation`
  (`proof/gravity/player_suspend.krl.txt:39-46`); fall kind `Water`
  (`JX3RepresentX64_strings.txt:745406`).

### 18.2 Water data and rendering volumes [NAME]

* Engine water: `KG3DWaterManager`, `KG3DWaterNode`, `KG3DWaterBlock`,
  `KG3DWaterHeightShareVB`, `LoadWaterData`, `SaveWaterData`
  (`KG3DEngineX64_strings.txt:564124-564170`); scene accessors
  `KG3DSceneSection::GetWaterHeight`, `KG3DGround::SetWaterHeight`
  (`:554004,554088`); `KG3DTerrainRenderData_Node/Block::GetWaterBoundingPlanes`
  (`:536009,536053`).
* Map data: `%hs.WaterData`, `_FillMapWaterData`, `_FillWaterData`,
  `KG3D_LoadTerrainWaterData`, `WaterData.uWaterBlockCount < ARRAYSIZE(...)`
  (`proof/netcode/mode_ui/strings/kg3dsceneresapi_strings.txt:57392-79673`); water meshes
  `%s_Water.mesh`, flag `eKG3DSNMF_WaterModel`
  (`JX3RepresentX64_strings.txt:764257-766628`).

### 18.3 Flux water [NAME]

Managed API (`engine_host_spike/recon_managed_api.txt:713-715`):
`ResetFluxWaterSimulation`, `UpdateFluxCollisionHeightMap`, `EnableFluxWaterSimulation`;
enums `KG3D_FluxSourceShape`, `KG3D_FLUX_MODEL_TYPE`, `KG3D_OBJECT_WATER_STATE`,
`KG3D_INTERACT_FLUDS_LEVEL` (`:33-35,138,163`); adapter switch `bEnableFluxWater`
(`adapter_strings.txt:168775`); editor actions `EPT_ENGINE_OPTION_ENABLE_FLUX_WATER=441`,
`ENABLE_FLUX_SIMULATION=442` (`proof/engine_host/recon_actor_il3.txt:446-447`).
**The native Flux collision implementation and whether it affects swim gameplay is unknown**
(gap G-24). `UpdateFluxCollisionHeightMap` is the most promising route to a real water-collision
height map for the host.

---

## 19. Navigation and pathing

### 19.1 Stack [DOC/NAME]

**Full export surface recovered 2026-09-28** (`proof/collision/recon/NAVX64_exports.txt`,
27 exports; keyword strings `NAVX64_strings.txt`, including the error
`Can't load nav mesh from %s` at file offset `0x74F8`; PDB path
`...\Common\NAV\x64\Release_2019\NAVX64.pdb`):

| Group | Exports |
|---|---|
| lifecycle | `CreateNAV`, `?Init@PathEngineNavi(QEAA_NPEBD)` — `Init(const char*)`, `Release` |
| scene data | `LoadSceneData(const char*) → size_t`, `LoadSceneNaviMesh(const char* p0,p1,p2,p3) → bool`, `ReleaseSceneNaviMesh` |
| pathfinding | `FindPath(float×7) → bool`, `FindPathNow(float×6, protected) → iPath*`, `FindCurvedPath(float×11 + int)`, `FindPathWithWayPoint(float×8 + int)`, `FindWayPointPath(int64, int64)`, `GetPathPoint`, `GetPathPointCount`, `GetWayPoints`, `GetWayPointCount`, `FindNearestWayPointWithRadius(float×4)` |
| obstacles | `AddObstacle(std::vector<int>& ids, bool, float, float, float) → id`, `EnableObstacle(id, bool)`, `RemoveObstacle(id)` |
| load/persist | `LoadPathFromIPath(iPath*)` / `LoadPathFromIPath(iPath*, float)` |

Import table (same recon): only `KERNEL32` (incl. `LoadLibraryA`, `GetProcAddress`), VC runtime
and CRT — **no import of `PathEngine.dll`**. CRT imports `fopen_s`, `fread`, `_ftelli64`,
`_fseeki64`, `malloc/free` prove the navmesh data is **file-based binary read inside NAVX64**;
PathEngine is either statically linked or dynamically loaded. `PathEngine.dll` (1.5 MB) is the
licensed SDK itself: only 5 exports (`DLLExport_GetIPathEngine`, allocator/wrapper functions),
strings show content processing and federation tiling (`iMeshFederation`,
`buildTileMeshFromContent`, “Pathfind preprocess persistent data version is incompatible”,
tiles/`partitionWithGridSize`/`startFacePerTile` fields)
(`proof/collision/recon/PathEngine_exports.txt`, `PathEngine_strings.txt`).

**Loader and format decoded further (2026-09-28, second pass).** Raw byte scans of all 339
modules in `bin64` (ASCII + UTF-16) found **no module other than NAVX64/PathEngine themselves
that references `NAVX64`, `CreateNAV`, `LoadSceneNaviMesh`, `PathEngine` or `CreateNAV`** —
nothing imports them. `KNavMeshQuery` literals exist only in `JX3ClientX64.exe`; there are no
Recast/Detour strings. Conclusion: the client does **not** host this navigator; NAVX64 +
PathEngine.dll are the standalone/server nav stack shipped in the same folder, and the client's
`KNavMeshQuery` gets its mesh from another (unlocated) source.

Decoded from the committed disassembly (`proof/collision/disasm/navx64_*.txt`):

* `PathEngineNavi::Init(const char* dllPath)` is a **`LoadLibraryA(dllPath)` shim** plus
  `GetLastError`/`SetLastError`/`GetProcAddress` (`navx64_init.txt:8-35`): the passed path is the
  real implementation DLL (`PathEngine.dll` ships beside it). `CreateNAV` then builds the
  `PathEngineNavi` object.
* `LoadSceneNaviMesh(this, p0, p1, p2, p3)` (`navx64_loadscenenavimesh.txt`):
  1. checks `p0` exists with `fopen_s`+`fclose` (0x4535-0x455d);
  2. computes the sizes of all four paths (helper `0x180003720` = open/seek-end/tell/close);
  3. `malloc(max size)` — one shared scratch buffer;
  4. allocates a refcounted 0x20-byte **stream object** (vtable, buffer, refcounts) and reads
     **p0** wholly into it;
  5. parses a **12-byte binary header** `{int a -> +0x78, int b -> +0x7c, int c -> +0x80
     (c<=0 => 1)}` then calls `stream->vt[0x40](name, buf+0xC, size-0xC, 0)` (0x4629-0x4684);
  6. calls `ReleaseSceneNaviMesh`, stores the loaded mesh at `+8`, builds an 8-int array from
     `b` as `{-b, b, b, b, b, -b, -b, -b}` and calls `stream->vt[0x38]` -> stored at `+0x10`
     (0x46a3-0x4705);
  7. `stream->vt[0x1f8]()` -> `+0x70`;
  8. if **p2** exists and is non-empty: read it and call `stream->vt[0x1c8](+0x10, buf, size)`
     (0x4736-0x4771);
  9. if **p1** is non-empty: read it and call `stream->vt[0x1e8](+0x10, buf, size)`
     (0x4771-0x479d); else fall back to `stream->vt[0x160](+0x10, 1, 0)` (0x479f-0x47b4);
  10. `stream->vt[0x188](+0x10, 0)`;
  11. if **p3** is non-empty: read it, NUL-terminate, and parse it as **text** via
      `0x1800065C0(&this[0x88], buf, size)` (0x47ca-0x47fd) — the only textual component;
  12. returns success and releases the stream.

So a navmesh is a **four-file set**: `p0` binary mesh (12-byte header + payload), `p1` and `p2`
binary overlay sets, `p3` text (parsed with a token scanner that bounds names at 31 chars and
buffers at 0x1000, `navx64_text_parser.txt`). Files are read with CRT `fopen_s`/`fread`, i.e.
from the on-disk/server data directory, **not** the client pak VFS — consistent with the files
being absent from the client install. Error strings: `Can't load nav mesh from %s` (0x74F8),
`Can't build agent shape with radius %d` (0x7518).

Client-side query wrapper: `KNavMeshQuery::Init`, `m_pNavMesh`, `m_pNavMeshQuery`,
`KNavMeshQuery::QueryPath` (`JX3ClientX64_exe_strings.txt:766962-766971`); `KPlayer::LuaNavTo`,
`KPlayerClient::DoNavTo` / `DoNavStop`, `KPlayerClient::OnNavResult`, `pNavResult`
(`:704033,709110,759904,773329`); world interface `KGSO3WorldClientInterface::QueryPath`
(`:710349`). Lua helpers `GetNavResult` / `LuaGetNavResult` (`:730232,734597`).
Host/represent auto-move: `AutoMoveToPoint` / `AutoMoveToTarget` / follow-action
(`JX3RepresentX64_strings.txt:730637-754953`).

### 19.2 Protocol [DISASM]

| Message | ID | Size | Notes |
|---|---|---|---|
| `DoNavTo` (C→S) | `0x01BF` | 91 B | destination path request |
| `DoNavStop` (C→S) | `0x01C0` | — | cancel |
| `OnNavResult` (S→C) | not named in table | — | path/result |

(`proof/netcode/c2s_protocol_catalog.tsv:259-260`, `docs/netcode/JX3_PROTOCOL_SPEC.md:164`.)

### 19.3 Walkability and patrol data [NAME]

* Unit flags `bAutoPathing`, `nPathingType`, `fPathingHeight` (§8.4).
* Patrol/waypoints: `ENTER_PATROL_PATH`, `LEAVE_PATROL_PATH`, `DO_PATROL`, `nPatrolPathID`,
  `nPatrolOrderIndex` (`JX3ClientX64_exe_strings.txt:719621-779931`);
  `KG3DRepresentWayPoint::{GetTranslation, GetRotation}`, `KG3DRepresentTrafficPoint`,
  `AddTrafficPoint` (`KG3DEngineX64_strings.txt:538338-560275`); map loader
  `Load WayPointSet` / `TrafficPoint` (`map_loader_paths.txt:9,20`); represent guides
  `navigate new/param/go` (`JX3RepresentX64_strings.txt:733137-733143`).
* Nav obstacles: doodad columns `DynamicObstacle`/`DisableNavObstacle`; advanced obstacle
  system (§10.2); `KCharacter::CellMoveNav` / `ProcessNavAutoFly` for in-cell movement;
  `nMoveObstacleType` in logic.

### 19.4 Open items

Known after the 2026-09-28 recon: the complete NAVX64 API, that navmesh is loaded from
files read in-module (`fopen_s`/`fread`), that `LoadSceneNaviMesh` consumes **four paths**, and
that PathEngine federation tiling/preprocess persistence is the underlying SDK.

Still unknown (gap G-25, RE pass P0-3):

1. the four file names/extensions and the caller-side template that builds them (the client
   holds no `navi` literals — search `KNavMeshQuery::Init` and `KGSO3WorldClientInterface::QueryPath`
   callers in `JX3ClientX64.exe`; disassemble `LoadSceneNaviMesh` RVA `0x44D0` and `Init` RVA
   `0x4060` for filename handling);
2. the binary format parsed by NAVX64 (locate the magic/version checks; PathEngine strings
   mention versioned “pathfind preprocess persistent data”);
3. `QueryPath` algorithm, agent radius/filters, waypoint semantics;
4. the `bAutoPathing` consumer that feeds source geometry into the bake.

Until then, pathfinding reproduction uses direct steering, and the `.nav` data remains
un-reproducible.

# Part V — Combat collision

## 20. Targeting shapes (CastMode)

### 20.1 The enum [DATA]

Full engine enum run (`proof/pvp/attributes/jx3client_region_80cf00.txt:32-58`, mirrored in
`proof/netcode/JX3ClientX64_exe_net_strings.txt:6104-6110`):

```
Sector, SectorOfDepth, TargetAngleSector, Rectangle, RectangleOfDepth,
TargetAngleRectangle, CasterArea, CasterAreaOfDepth, TargetArea, PointArea,
PointAreaOfCasterTeam, PointRectangle, CasterSingle, TargetSingle, TargetChain,
Point, Item, TargetLeader, PartyArea, TargetTeamArea, TargetHoodle,
CasterSpreadCircle, TargetRay, CasterConvexHullArea, PointAreaFindFirst,
SectorOfAttention, CasterAreaOfAttention, Column
```

`CastMode` is skills.tab column 10 of 117 (`proof/pvp/buffs/skills_header.txt:12`).
Population census over the real skill corpus (`proof/pvp/cast_cooldown_resources.md:62-66`):

| CastMode | rows | | CastMode | rows |
|---|---|---|---|---|
| `TargetSingle` | 14,028 | | `TargetArea` | 339 |
| `CasterSingle` | 13,231 | | `PartyArea` | 186 |
| `CasterArea` | 6,420 | | `CasterAreaOfDepth` | 116 |
| `PointArea` | 2,057 | | `PointRectangle` | 83 |
| `Sector` | 2,027 | | `CasterSpreadCircle` | 80 |
| `Rectangle` | 1,508 | | `TargetRay` | 72 |
| `TargetChain` | 640 | | `TargetLeader` | 59 |

### 20.2 Geometric fields [DATA]

Per-skill data comes from Lua `GetSkillLevelData` (columns in `skill_range_report.tsv`,
field semantics in `tools/netcode/scan_skill_ranges.py:5-10,33-55`,
`proof/pvp/cast/skill_cast_fields.tsv`):

| Field | Meaning | Unit |
|---|---|---|
| `nMinRadius` / `nMaxRadius` | cast window (min/max range) | 尺 = 64 u |
| `nAreaRadius` | area radius for area/point modes | 尺 |
| `nAngleRange` | sector/cone angle | 1/256 turn; degrees = value × 1.40625; 256 = 360° |
| `nRectWidth` / `nHeight` | rectangle width (and height/depth) | 尺 / u |
| `nProtectRadius` | dead zone around caster | 尺 |
| `nTargetCountLimit` | max targets; **< 0 = unlimited** | count |
| `nChainBranch` / `nChainDepth` | chain jumps | count |
| `nBulletVelocity` | projectile speed | points per frame |
| `nAttackAttenuationCof` | AoE damage falloff, 1024 = 100% | per-mille-ish |
| `nWeaponDamagePercent` | weapon damage scaling | percent |
| `bIsExactHit` / `bIsExactHitInAir` | precision-hit variants | flag |
| `nCastHeight`, `nMaxAltitude`, `bFullAngleInAir` | air/altitude gating | u / flag |
| `bBulletDestroyScript` | projectile destroy callback | flag |

Raw examples: `proof/netcode/mode_juejing/sample_item_script.txt:173-241`
(`nChainBranch/nChainDepth`, `nMinRadius/nMaxRadius`, `nAngleRange`, `nAreaRadius`,
`nTargetCountLimit` comment “小于0 代表目标数量不限制”, `nBulletVelocity`), sector angles
120.938° and 14 raw ≈ 19.7° (`skill_range_report.tsv:24,1390`), rectangle 7 尺 / 20 尺
(`:22,52`), 风来吴山 10 尺 / 10 targets + 10 尺 radius
(`proof/netcode/skill_data/ability_range_examples.md:66-68`).

### 20.3 Additional gating columns [DATA]

skills.tab: `Use3DObstacle` col 33 (1 on 17,075 rows), `CheckReachable` col 34 (1 on 201),
`IgnoreRangeBlock` col 107 (729 rows) (`proof/pvp/cast/targeting_flags.txt:2,11,65-73`);
`LongRange` 93, `AOE` 94, `RangePutOpti` 97, `CanCastOnTower` 87, `IsFrost` 106,
`SelfMoveStateMask`/`TargetMoveStateMask` 63/64 (+Mask2/backups),
`IgnoreControl` 111, `IgnoreImmunityCast` 110, shield bypass 35/36, `AutoSelectTarget` 52,
`TargetJumpCountMask` 89 (`proof/pvp/buffs/skills_header.txt:35-117`,
`proof/pvp/combat_netcode.md:165-169,298`).

### 20.4 Where shape resolution happens [DISASM/negative]

The client exposes only helpers: `KSkill::CheckTargetRange`, `LuaCheckDistance`,
`LuaCheckAngle`, `CheckAngle`, `LuaCheckTargetState`
(`proof/netcode/JX3ClientX64_exe_net_strings.txt:5964,8527-8533`). There is **no client hit/AoE
resolution** — the server owns legality and results (`proof/pvp/combat_netcode.md:304`).
Reproduction therefore needs the inferred model (§24), driven by these fields.

---

## 21. Hit detection

### 21.1 Authority and result packet [DISASM]

* Combat is fully server-arbitrated; only `OnSkillEffectResult` changes HP
  (`proof/pvp/combat_netcode.md:28-58,205-206,352-353`); no rollback (`:233-248`).
* `OnSkillEffectResult` layout (header confirmed from the full handler disassembly
  `proof/pvp/netcode/disasm/KPlayerClient__OnSkillEffectResult.txt`): packet flags at `+0x1b`
  (bit0 miss/absorb path, bit1, bit2, bit3, bit6, bit7) and `+0x1c` (bit0, bit2);
  `casterID` dword at `+9`; `targetID` dword at `+0x0d`; byte `+0x15`; dword `+0x16`
  (damage/skill value); byte `+0x1a`; `cResultCount` int8 at `+0x22`; `KSKILL_RESULT` records
  start at `+0x23`, stride 9 (`offset = count*9 + 0x23`, assert at `0x18018f9df`). The handler
  feeds five-dword view structs `{caster,target,byte,dword,byte}` into presentation callbacks
  via vt[`+0x4b0/+0x4b8/+0x4d0/+0x4d8/+0x4e0/+0x550/+0x568/+0x570/+0x578`]. **Per-record
  9-byte field meaning still requires the per-record consumer trace** (G-26 residual).
* Event family: `OnSkillPrepare/Cast/Channel/EffectResult/BeatBack/RayEffect/ChainEffect/
  PointChainSkillEffect` (`combat_netcode.md:99-131,194-216`).
* C→S casts: `DoCastProfessionSkill 0x49/0x20`, `DoCharacterSkill 0x1B/0x1D`,
  `DoCastHoardSkill 0x104/0x1F`, `DoStartHoardSkill 0x103/0x21`, revive `0xB9`; **no
  `DoSelectTarget`** (`combat_netcode.md:68-97`, `proof/pvp/combat_opcodes.tsv`).

### 21.2 Geometric hit primitives [NAME/DISASM]

* **Logic ray** (the closest thing to a hit test visible client-side):
  `GetLogicRaycastHit` / `KScriptFuncList::LuaGetLogicRaycastHit`
  (`JX3ClientX64_exe_strings.txt:728506,730422`; Lua constant catalogs
  `proof/pvp/attributes/NewSkill.lh.strings.txt:95`, `include_str_hits.txt:70`), alongside
  `GetLogicDirection`, `GetFloor`, `GetInterceptPoint`, `GetAdaptedPoint`
  (`NewSkill.lh.strings.txt:85-99`). Used by skill scripts and the map loader.
* **Bone boxes**: `KG3DModel::IsRayIntersectBoneBox` (inner test `0x180376BE0`, element flag
  `0x20000`), `UpdateBoneOBBox`, `CreateOBBoxOfBone`, `vBoneBox`, `KG3DBip::GetSkeletonBoneBound`
  (§14.3); skeleton bone-bound assets load via `KG3DSkeletonBoneBound::*`. Only proven for the
  **camera near-ray** so far; use for skills is unevidenced (gap G-27).
* **Body bone box sync**: `KPlayerClient::OnSyncBodyBoneBoxSize`,
  `KPlayer::LuaGetBodyBoneBoxSize`, `GetBodyBoneBoxSize`, `m_BodyBoneBox`,
  `BODY_BONE_BOX_MAX_SIZE` (`JX3ClientX64_exe_strings.txt:706402,739291-739466,770101,773575`) —
  but the containing class is `KBodyReshapingBox` (body customisation), so this is **not
  confirmed as a combat volume** (gap G-27).
* **Overlap/sweep**: PhysX callback templates in SIMWorld (`PxHitCallback<PxSweepHit>`,
  `PxOverlapHit`, `PxQueryFilterCallback`), adapter `PhysicsOverlapCallBack`, and
  `PhysicsScene::SweepEx` (§5.5). No decoded combat consumer.

### 21.3 Damage primitives and attributes [DATA]

Skill script primitives `SKILL_*_DAMAGE` / `_RAND` / `CALL_*`, `nDamageBase`, `nDamageRand`,
`nWeaponDamagePercent`, `nAttackAttenuationCof` (`docs/pvp/JX3_PVP_BATTLE_RESEARCH.md:110-124`).
Attribute ops `PHYSICS_*` include `PHYSICS_HIT_BASE_RATE`, `PHYSICS_HIT_VALUE`,
`ALL_TYPE_HIT_VALUE`, per-school `*_HIT_BASE_RATE/_HIT_VALUE`, `PHYSICS_CRITICAL_*`,
`PHYSICS_OVERCOME/RESIST/DEFENCE/SHIELD/REFLECTION/BLOCK`, `PHYSICS_DAMAGE_ABSORB`,
`PHYSICS_MANA_SHIELD`, `SET_HIT_REDUCE_RATE`
(`proof/gravity/JX3ClientX64_exe_strings.txt:714384-717476`). Hit-clamp fields
`nPhysicsHitValue` / `nPhysicsHitBaseRate` (`:6215-6219`), `nPhysicsAttackPower(Base)`.

### 21.4 Hit feedback [DATA]

Hit-stiff/beat-back columns and examples per §14.2. Hit presentation handlers:
`KRLCharacter::{BeHitted, BeHittedByNpc, BeHittedByPlayer, PlayBeHittedAnimation}`,
`KGameWorldHandler::OnCharacterBeHitted`, `krlEventAdaptor::HandleCharacterBeHitted`,
event `KREPRESENT_EVENT_CHAE_BE_HITTED`, tables `BeHitEffect` / `SkillHitEffect`
(`JX3RepresentX64_strings.txt:747056-761467`).

### 21.5 Explicit gaps

Weapon hitboxes/traces have **zero evidence** (gap G-28); `OnSkillRayEffect` body disassembly is
missing (only the handler name); `IsExactHit` semantics undecoded; `KSKILL_RESULT` split
unknown.

---

## 22. Projectiles and missiles

### 22.1 Runtime pipeline [NAME]

Representation owns projectile visuals/motion; damage still arrives from the server.

| Symbol | Role |
|---|---|
| `KRLBuffBullet::{LaunchBullet, UpdateBullet, PlayHitSFX, AddBulletToScene}` | buff bullets |
| `krlSkillBullet` / manager `{StartFly, GetTarPosition}` | skill bullets |
| `KRLMissile::{HitTarget, PlayHitSfx, NoticeBeHitShake, OnMissileDamageTagArrive}` | missiles |
| `KGameWorldHandler::OnNotifySkillBullet` | server notification |
| tables `SkillMissileTable`, `BuffBulletTable`, `SkillBullet`, `MissileFixedTrackFrameTable` | data |

(`proof/netcode/JX3RepresentX64_net_strings.txt:232-233,806,1815-1837,1899-1920,1941-1950,2322-2362,2433-2580`.)
Fields `nBulletVelocity` (points/frame), `nSkillBulletType`, `nSkillBulletSubType`,
`bBulletDestroyScript` (`JX3ClientX64_exe_net_strings.txt:8448,8486-8487`,
`sample_item_script.txt:209`, `exe_skillmove_strings.txt:39`).

### 22.2 Recovered data model (2026-09-28) [DATA]

Extraction root is **`Represent/`** (capital R) via `pss_assets.run_pakv4`; reproducible with
`tools/netcode/extract_pak_paths.py --list proof/collision/recon/missile_candidates.txt`
(candidates + phase files). All extracted tables are committed under
`proof/collision/missile/` and `proof/collision/skill_tables/`.

| Table | Rows | Key fields |
|---|---|---|
| `missile.txt` (`MissileTable`) | 413 | `MissileID` → `FightControllerID1..4` (flight), `HitControllerID1..4`, `MissControllerID1..4` |
| `skill_missile.txt` (`SkillMissileTable`) | 412 | `SklillMissileID` [sic], `Desc`, `MissileID1..10`, `HitModelParamID` |
| `missile_controller.txt` | 420 | `MissileControllerID`, flags `bTakeChangeVelocity`, `bTakeChargeModel`, `bChain`, `bReverseModel`, `RotationIgnoreY`; refs `ModelParamID`, `HomingParamID`, `ParabolaParamID`, `YAxisParamID`, `TimelineParamID`, `CurveParamID` |
| `missile_phase_line.txt` | 5 | phase config (below) |
| `missile_phase_circle.txt` | 2 | " |
| `missile_phase_fixedtrack.txt` | 3 | " |
| `missile_phase_lightning.txt` | 3 | " |
| `missile_param_homing.txt` | 99 | `BoneNameCaster/Target`, `InitialVelocity`, `Acceleration`, `MaxVelocity`, `TargetRadius`, `ConvergenceFactor`, `HitRemainTime`, `SFXRemainTime`, `RandomByBone`, `MinRandomCount`, LOD distances, `Rotation` |
| `missile_param_parabola.txt` | 68 | caster/target offsets, `GPSType`, `bFaceByTarget`, `bParabolaAutoToTarget`, `bScrew`, `Gravity`, `GravityAngle`, `InitialVelocity`, `Acceleration`, `MaxVelocity`, `AngleFactorType`, horizontal/vertical angle factors |
| `missile_param_timeline.txt` | 4 | `Millisecond`, `fDistance` |
| `missile_param_y_axis.txt` | 1 | `Y` |
| `missile_param_curve.krl.txt` | — | curve definitions (Lua) |
| `missile_fixed_track_frame.txt` | 14 | `MissilePhaseID` + 25 frames × (X,Y,Z) |
| `buff_bullet.txt` (`BuffBulletTable`) | 181 (34 cols) | body type, buff slot/id, launch/burst skill ids, bullet model, caster socket, target bone, scale, bind FX, hit FX, and charge/launch/recover/end/burst timings + animation speeds (decoded header: `buff_bullet_header_utf8.txt`) |
| `skill_bullet.krl.txt` | — | `nSkillBulletType` enum → per-type `{CommonModel, CommonFlyAni, SingleTargetAni (per body type), FlyEndAni, OutTime, Offset, Angle}` (types e.g. `CYShield`, `PureShadow`, `SelfShadow`, `TargetShadow`) |
| `skill_result.txt` | 686 | `EffectResultID`, `EffectType`, `EffectID`, `Note` — result-effect mapping |
| `behit_shake.txt` | 3 | `RepresentID`, `OscillationCount`, `MaxOffset`, `Duration`, `OscillationType` (e.g. 2/10/100/0) |

Phase table common columns: `MissilePhaseID`, `Design_Name`, `AnimationSpeed`,
`ModelScaleBegin/End`, `AnimationType`, `MeshFile`, `MaterialFile`, `AnimationFile`, `SFXFile`,
`CasterType`, `MovementType`, `SymmetricalType`, `MissileDirection`, `Number`, `ReferenceType`,
`ZLockType`, `BoneNameCaster`, `BoneNameTarget`, `IsShockWave`.

`missile_phase_model.ini` defines the whole taxonomy:

```
field types   none, line, circle, homing, fixedtrack, fulmination
caster types  last, caster, target
movement      straight, parabola, random
symmetrical   odd, even
direction     horizontal, vertical
reference     axis, centric
z-lock        terrain, free
phase files   line / circle / fixedtrack / lightning
param files   model / homing / parabola / timeline / y_axis / curve / controller
```

### 22.3 Reproduction meaning

The client projectile system is a **data-driven visual controller**: skill → `skill_missile` →
`missile` record → controller flags + phase + mover params → animated bullet with per-body-type
variants and hit FX. **Collision/hit resolution is not client-side**; damage arrives as
`OnSkillEffectResult`, and bullet notifications (`OnNotifySkillBullet`, `OnSkillRayEffect`,
`OnSkillChainEffect`) trigger presentation. A faithful client reproduces the controller motion
(homing/parabola/timeline/fixed-track) and the phase visuals; an inferred server (P0-2
remaining) simulates the same movers and applies `TargetRadius`/`HitRemainTime` hit windows.

### 22.4 Open items

* tick base for velocity/acceleration fields (`nBulletVelocity` “points/frame” — likely
  `GAME_FPS=16`, needs capture fitting);
* `KParabolaMissileProcessor` internals (RE pass P0-2 residual);
* server-side projectile world collision (`SweepEx`/LOS) unverified.

---

## 23. Damage, control and immunity (collision-adjacent)

Collision outcomes feed this pipeline; the reverse (CC preventing movement) matters for
reproduction.

### 23.1 Mitigation order [DOC, mixed verified/inferred]

`hit check → dodge/parry → shield → resist → 化劲/御劲 → block → absorb → reflect → final`
with per-step evidence markers (`proof/pvp/attributes_and_damage.md:270-282`); practical
restatement `docs/pvp/REBORN_PVP_BATTLE_SPEC.md:83-98`. Rating→% formulas unknown
(gap G-30). `1024 = 100%` throughout.

### 23.2 Control/DR [DATA]

* Control categories: `{2 slow, 3 fear, 4 root, 5 silence, 6 雷霆, 7 锁足, 8 stun, 9 taunt,
  11 knockdown}`; displacement is rate-based (`atKnockedBackRate/atRepulsedRate/atPullRate =
  −1024`) (`proof/pvp/buff_control_system.md:182-230`).
* DR `DecayType.tab`: 10 rows, frames 160/320/96, `DecayFrame == ImmunityFrame`; loader
  `KBuffManager::LoadBuffDecayInfo` (`:300-332`); ladder/reset not shipped in client.
* 解控/免控 sets per skill (e.g. 生太极 {2,3,4,7,8,11}+knockdown; 镇山河 block+invincible)
  (`:334-371`).
* Skill flags: `IgnoreControl` col 111, `IgnoreImmunityCast` col 110, shield bypass 35/36; cast
  report `proof/pvp/cast_cooldown_resources.md:423-426`; `IgnoreImmunityCast` examples
  (`proof/pvp/cast/targeting_flags.txt:74-82`).
* Move-state masks (`SelfMoveStateMask`/`TargetMoveStateMask`) gate which skills can be cast in
  which movement states — the bridge between CC and movement collision.

---

## 24. Inferred server model

**Everything in this chapter is [SERVER-INF]** unless tagged otherwise. It is assembled from
protocol shapes, client tables, handler field maps and the absence of client-side resolution.
It is the model to implement when reproducing full gameplay behaviour; parameter fitting
against captured sessions is required.

### 24.1 Cast validation order

```
C→S DoCharacterSkill / DoCastProfessionSkill { skillId, targetId | x,y,z }
server:
  1. actor alive/valid; skill known; cooldown/charge/resource ok
  2. self move-state allowed  (SelfMoveStateMask,  MoveState)
  3. target state/type mask   (TargetMoveStateMask, TargetTypeMask, bAttackable)
  4. range:  nMinRadius ≤ dist2D ≤ nMaxRadius        (dist in 尺×64 u)
  5. angle:  nAngleRange/256·360° around facing      (sector and angle variants only)
  6. LOS:    if Use3DObstacle → ray/segment vs static world (no foliage? see G-17/G-21)
  7. reach:  if CheckReachable → nav path exists (PathEngine)
  8. immunity/control gates  (IgnoreImmunityCast, IgnoreControl, DR tables)
  9. dedupe/caps: nTargetCountLimit (negative = unlimited), per-target DR/ICD
 10. schedule effect at cast/channel frames; on channel ticks re-check 4/6
```

### 24.2 AoE shape resolution (geometry)

All distances in world units (`1 尺 = 64 u`); angles `raw/256·360°`:

| CastMode | Candidate set | Test |
|---|---|---|
| `CasterSingle` | self | caster is the point |
| `TargetSingle` | target entity | id-based |
| `CasterArea` | entities in radius around caster | 2-D distance ≤ `nAreaRadius·64`, vertical window unknown |
| `TargetArea` | around target | same, centre = target |
| `PointArea` / `PointRectangle` / `PointAreaFindFirst` | ground point | range gate to point, then area/rect at the point |
| `Sector` / `SectorOfDepth` / `TargetAngleSector` | cone from caster | angle ≤ `nAngleRange/256·360°`, radius ≤ max, depth layering |
| `Rectangle` / `RectangleOfDepth` | oriented box | half-width `nRectWidth·64/2`, length from cast range, height `nHeight` |
| `TargetChain` | chained | first by range, then nearest within `nChainBranch`/`nChainDepth` |
| `CasterSpreadCircle` | burst around caster | radius; per-target falloff by distance |
| `TargetRay` | line | segment from caster through target; unknown width |
| `TargetLeader` / `PartyArea` / `TargetTeamArea` | team/party | caster team filter |
| `Column` / `SectorOfAttention` / `CasterConvexHullArea` / `TargetHoodle` / `Point` / `Item` | unknown | semantics undecoded (gap G-31) |

Ordering of multi-target selection (nearest first? target priority?) is not in the client
(gap G-32). Damage falloff uses `nAttackAttenuationCof` where shipped (1024 = 100%).

### 24.3 Projectiles (inferred)

```
spawn at caster socket (S_fxmid/S_rh pattern for chains, ability_candidates.json:41)
velocity = nBulletVelocity points/frame (server tick base unknown; 16 fps?)
trajectory: direct / parabola (KParabolaMissileProcessor)
mover params: homing/parabola/timeline/y_axis/curve tables (§22.2)
hit window: TargetRadius + Hit/SFXRemainTime (homing), target radius/bone (parabola)
per server tick: integrate, collide vs world (SweepEx/LOS rules) and vs candidate entities
on hit: server applies result and emits OnNotifySkillBullet / OnSkillRayEffect /
         OnSkillChainEffect; client plays visual only
```

### 24.4 Movement authority

The client predicts locally with the 15 Hz integer model; the server owns control locks
(`MoveCtrl`, `nDisableMoveCtrlCounter`) and periodically applies
`OnMoveCharacter` / `OnSyncMoveState` / `OnSyncMoveCtrl` / `OnSyncMoveParam`; reconciliation
uses the sequence at `+0xF9C` (`docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md:354-468`). CC and
displacement are applied through move states and forced `SkillMove` rows, not client physics.

### 24.5 Reproduction validation strategy

1. Use captured mode sessions (`proof/netcode/mode_juejing/`) to compare processed positions
   and event timing against the inferred model.
2. Fit unknown constants (target ordering, vertical windows, projectile tick base) from
   capture traces; register every fitted parameter as `[SERVER-INF fitted]`.
3. Keep the client-truth model authoritative for anything the client computes
   (camera, movement integration, support, animations).

# Part VI — Reproduction and verification

## 25. Reproduction blueprints

Each blueprint lists the data, the algorithm, and the fidelity status. Constants/format tables
are in the referenced chapters to avoid duplication.

### 25.1 World collision stack

```
1. terrain:  loader(GetTerrainDesc, LoadRegion per region) → (513)² float grid
             world(x,z) → region idx, local cell, bilinear height
             (holes: LoadHoleRegion 1-bit mask — NOT yet in host, G-2)
2. statics:  baked FCOL v1 (foliage solids) + v2 (sceneinfo_full meshes)
             + CFGL sidecar per mesh (camera gate, not player collision)
3. runtime:  instance grid 800 u → per-mesh triangle CSR → capsule vs triangles
             Resolve (3 iters, step ≤70, slide) + SupportHeight
4. queries:  Raycast(segment, structuresOnly?, frontFacesOnly?, cameraGate?)
```

Fidelity: blocking/support for baked classes exact to the game's own geometry; classes absent
from the bake (billboard trees, dynamic objects, landscape-only meshes) differ (gaps G-8, G-33).

### 25.2 Actor movement and support (client-truth approximation)

```
per frame (fixed 1/15 s for logic):
  intent dir (camera-relative) → target speed (walk/run/sprint)
  terrain support  ground = Sample(x,z)
  slope block:      lookahead 40 u; if rise > 70 u, try axis slides
  static support:   SupportHeight(x,z, y-20, y+70) + 3 forward probes
                    else Resolve(capsule) → blocked, ground, grounded
  ledge:            if grounded and y-ground > 150 → airborne
  jump:             if grounded and jump pressed → vy = takeoff
  gravity:          vy += g·dt; y += vy·dt; land when y ≤ ground
```

To lift this to full fidelity: replace the continuous integrator with the 15 Hz integer model
(§12.3), port the turn model and move-state machine (§12.1), use real table triples per school
(§13.1), and derive slope/step thresholds from the solver fields once RE pass P0-1 lands.

### 25.3 Camera obstruction

Assemble from `docs/CAMERA_WALL_OBSTRUCTION.md` + `camara-fix` implementation:

```
anchor A = player + (0, 90, 0) chest offset
desired C = A + offset(yaw,pitch,dist)
probes = 5 (or 9) points on a footprint perpendicular to the offset
for each probe: nearest positive hit among
   - baked static ray (cameraGate, front faces)
   - engine terrain ray
   - engine scene/entity ray
   - vertical ladder (14 samples, terrain)
   - heightfield march (14 samples)
hit = min
CameraObstruction state machine:
   pull to max(0.001, hit) − 18   (always on a shortening hit)
   window 50 free / 100 obstructed for receding hits
   spring S' = −1.5·E − 2.828·S
final gate: camera→anchor retract to max(1, hit−25)
near: player park below at <90, restore >150
```

Remaining deltas: vertical backend no-hit in host (D5), footprint 22 u (C2), near plane (C7),
mask split (G-17), per-map `.cflags` coverage (G-7).

### 25.4 Combat targeting / hit (server model)

Blueprints per §24: shape test library over candidate entities, validation order, target
ordering policy (default: nearest-first by caster distance — **fitted**, G-32), falloff, result
packets. Client side: play `OnSkillEffectResult`, hit-stiff, knockback, SFX.

### 25.5 Dynamic objects, water, nav

* Doodads: model as server-owned entities with optional dynamic/nav obstacle markers; client
  syncs spawn/state/loot packets (§10.1).
* Advanced/movable obstacles: implement only if a scene needs them; semantics undecoded
  (G-10).
* Water: waterline function per §18.1 + flat/`WaterData` plane height; Flux collision needs RE
  (G-24).
* Nav: blocked until navmesh format is recovered (G-25); meanwhile use direct steering/
  click-to-move approximations.

---

## 26. Cross-cutting gotchas

### 26.1 The unit conflict (decisive experiment needed)

Two conventions coexist in repo docs:

| Convention | Evidence | Implication |
|---|---|---|
| **1 u = 1 cm** (recommended) | mesh census 181.64 u = 1.816 m; ragdoll limbs 5–12 u; camera 100 u/m; terrain cell 100 u = 1 m; 尺 = 64 u = 0.64 m | jump 90 u/frame = 13.5 m/s; walk 6 尺/s = 3.84 m/s |
| 1 m = 192 u | gravity/movement docs, derived from assuming 尺 = 1/3 m | jump 90 u/frame = 7.03 m/s; walk 6 尺/s = 2.0 m/s |

**Decisive experiment:** place two ground markers at a measured world distance (terrain cells
are exactly 100 u), walk for N frames, compare speed; and measure the character mesh against a
terrain cell in the host render. Until then, all *integer* physics is convention-free; only
display/derived m/s numbers change. Recommended: use 1 u = 1 cm and restate gravity docs'
metric labels accordingly.

### 26.2 Matrix conventions

* `sceneinfo_full` `actorLocalMatrix`: row-major, translation in row 3.
* Baked v2 runtime: row-vector `w = l · M`; inverse matrices are built once per instance.
* Foliage v1 builds `Ry(yaw)·scale` with translation at `m[12..14]`.
* Bone/skeleton transforms are row-vector in the engine (`x' = x·M`).

### 26.3 Region/coordinate math

`region = clamp(floor((v − origin)/ (RegionSize·UnitScale)), 0, count-1)`;
local cell `(v − origin − region·51200)/100`; heights bilinear over a 513² grid with duplicated
border for tile stitching. Foliage cell bias and region origin formulas are in §9.1.

### 26.4 Frequency and determinism

Logic 15 Hz, physics 50 Hz, combat data 16 fps, animation 30/33 fps. The client is not
deterministic under server correction; reproduction tests must compare at event boundaries
(casts, landings, syncs), not predicted frames.

### 26.5 Filters and LOD

Camera rays use the `bObscatleCamera` gate; player collision ignores it; LOD mesh collision is
enabled in config (`bUseLODMeshCollision=1`) but our bake uses base meshes (G-4). Skill LOS
gating (`Use3DObstacle`) has no reversed implementation (G-21).

### 26.6 Known documentation drift to fix over time

* `docs/FULL_MAP_COLLISION.md:243-253` claims MapSelector lists all 621 maps; code filters to 5.
* `docs/FULL_MAP_COLLISION.md:168` lists a proof `structure_collision.bin` that is not present.
* `bake_map_collision.py` defines `--max-regions` but never uses it.
* Historic `Describe()` pattern breakdown (deadcount/cactus/rock) no longer printed.
* `PxCapsuleControllerDesc` claim (G-1) is unverified; treat as open.
* Generic `collision_data/structure_collision.bin` (+`.cflags`) is a generated duplicate of the
  龙门寻宝 pair and is intentionally untracked.

---

## 27. Verification matrix

| Domain | Existing proof | Reproduction check |
|---|---|---|
| terrain sampling | probe log `docs/REAL_CLIENT_MAP_COLLISION.md:84-92`; `TerrainSampler` warm-up | sample known coords; cross-check `SetCameraPos` Y-snap oracle (`SPIKE_B_MAP_NOTES.md:43-44`) |
| baked structures | `proof/map_spike/structure_collision/*.log/.png` (cactus, building, tree, measured tree) | walk into each class; expect `blocked=True` and stop distance ≈ capsule radius |
| physic-list filter | `proof/collision/physic/audit_lists.txt` (617 models / 4,963 objects) | re-run `tools/collision/audit_physic_lists.py`; assert 4,903/60 accepted/rejected; walk into a blacklisted prop (should not block) |
| foliage counts/scales | bin headers parse (section 9.4 table) | re-parse `FCOL` headers; assert counts/sizes |
| foliage decode | `tools/decode_foliage.py` exact-length validation | decode all 13 files; length + instance-count match |
| gravity/jump | `proof/gravity/verification.txt` (apex 414 u, 18 frames, per-school) | `python tools/gravity/verify_model.py` diff |
| movement sync | `proof/movement/disasm/*`, `client_movement_symbols.txt` | replay `DoMoveCtrl`/`OnSyncMoveState` field maps against captures |
| camera obstruction | T1–T4 runs in `docs/CAMERA_PENETRATION_PLAN.md:127-163`; `proof/nearplane_ladder.png`, `proof/lookpack_ab.png` | `camera_smoke.exe` ALL PASS + spot sweeps (penetration frames = 0) |
| camera rays | `proof/netcode/engine_camera_contract.txt` | compare host `EngineRay` hits vs shim/bake |
| combat fields | `proof/pvp/cast/skill_cast_fields.tsv`, `skill_range_report.tsv` | re-run scanners; row counts and unit conversions |
| loot/doodad layouts | `proof/netcode/disasm/OnSync*.txt`, `JX3_LOOT_PROTOCOL_LAYOUTS.md` | decode captures; field offsets match |
| protocol movement | `proof/netcode/c2s_protocol_catalog.tsv`, `reference/jx3_model.py` smoke 10/10 | replay model against captures |

Commands (from repo root unless noted):

```powershell
python tools/decode_foliage.py C:\jx3tmp\foliage_dump\*.foliage
python tools/bake_map_collision.py --map 龙门寻宝 --copy-to C:/SeasunGame/MovieEditor/bin64/collision_data
python tools/gravity/verify_model.py
$env:MAP_COLLISION_PROBE='1'; $env:MAP_PROBE_ONLY='1'; $env:MAP_PROBE_DEEP='1'
& "C:\SeasunGame\MovieEditor\bin64\map_spike_host.exe"
client\build_client.cmd   # then camera_smoke.exe
```

---

## 28. Evidence index and RE tooling

### 28.1 String/export dumps (with roles)

| File | Role |
|---|---|
| `proof/movement/KG3DEngineX64_strings.txt` | engine: rays, mesh flags, water, scene response, physics manager |
| `proof/netcode/adapter_strings.txt` | adapter: config keys, physics scene bridge, pick rays, overlaps |
| `proof/gravity/SIMWorldX64_strings.txt` + `_exports.txt` | gameplay physics: PxWorld exports, solver keys, triggers, zones |
| `proof/gravity/JX3ClientX64_exe_strings.txt` | logic: KCharacter/KNpc, movement, nav, water, interaction, obstacles |
| `proof/gravity/JX3RepresentX64_strings.txt` | represent: controller component, bullets/missiles, water height, picking |
| `proof/movement/KBaseX64_strings.txt` + `proof/netcode/exports_KBaseX64.txt` | collision math exports |
| `proof/netcode/NAVX64_net_strings.txt` | nav obstacle symbol (thin) |
| `proof/netcode/mode_ui/strings/kg3dsceneresapi_strings.txt` | doodad/water/scene data loaders |
| `engine_host_spike/recon_*.txt` | PhysicsEngineX64/adapter vtable + function recon |
| `proof/collision/recon/NAVX64_exports.txt` / `_strings.txt` | NAVX64 `PathEngineNavi` API surface (27 exports) |
| `proof/collision/recon/PathEngine_exports.txt` / `_strings.txt` | PathEngine SDK surface + content-processing strings |
| `proof/collision/recon/SIMWorldX64_exports.txt` / `_strings.txt` | re-verified SIMWorld export surface |
| `proof/collision/missile/*` | extracted missile/bullet/phase/controller tables (2026-09-28) + decoded `buff_bullet_header_utf8.txt` |
| `proof/collision/physic/*` | engine physic configs (file/folder white/black lists, shape/rigid/conveyor params) + UTF-8 copies + `audit_lists.txt` |
| `proof/collision/disasm/*` | 2026-09-28 disassembly: `PhysicsScene::_InitPhysXScene` constants, NAVX64 `Init`/`LoadSceneNaviMesh`/IO helpers/text parser, `PxControllerDesc::isValid` |
| `proof/collision/recon/NAVX64_all_strings.txt` | every ASCII string in NAVX64 (error texts, factory names) |
| `proof/collision/ui_scripts/*.utf8.lua` | unluac decompilation of `Target.lua` / `target.lua` / `skill.lua` (client targeting model) |
| `proof/collision/recon/PhysicsEngineX64_strings.txt` | RTTI + `_CreateCapsule` + PhysX controller-desc strings |
| `proof/collision/recon/PhysX3CharacterKinematic_*` | CharacterKinematic exports/strings (`createController` prototype) |
| `proof/collision/terrain_extra/*` | extracted `<map>.SRScene` (magic `SRS`, empty body) |
| `proof/collision/recon/terrain_extra_candidates*.txt`, `physic_config_candidates.txt` | extraction candidate batteries (method evidence) |
| `proof/netcode/loot_protocol` (`disasm/OnSync*`) | doodad/loot packet layouts |

### 28.2 Disassembly transcript dirs

| Dir | Covers |
|---|---|
| `proof/gravity/disasm/` | jump/fall/swim/waterline/ragdoll/physics scene/SetGravity callers |
| `proof/movement/disasm/` | WalkTo/RunTo/MoveTo/sync/input/hotkey |
| `proof/netcode/disasm/` | protocol, camera set/obstruction, cursor, scene load, motion tags |
| `proof/pvp/netcode/disasm*/` | combat handlers (`KPlayerClient::OnSync*`, skill results) |
| `reborn-camara-fix/proof/netcode/disasm/` | camera contract, obstruction, cursor, lock target, air combat |

### 28.3 Tools

| Tool | Use |
|---|---|
| `tools/dump_va.py` | disassemble VA ranges with string/IAT resolution |
| `tools/recon_physics.py` … `recon_physics4.py`, `recon_physics_funcs.py` | PhysicsEngineX64 recon |
| `tools/movement/find_xrefs.py` | call/field xrefs, window disasm |
| `tools/dump_il.fsx`, `dump_il_actor.fsx` | managed IL dumps (editor APIs) |
| `tools/gravity/parse_jump_tables.py`, `verify_model.py` | jump/gravity tables + numeric model |
| `tools/netcode/*` | skill scripts, ranges, motion, protocol, loot, map loader |
| `tools/pvp/*` | attributes, cast, hitstiff, control, modes |
| `tools/decode_foliage.py`, `export_foliage_collision.py`, `export_structure_collision.py`, `export_camera_flags.py`, `bake_map_collision.py` | collision bake |
| `pss_assets.py` (`run_pakv4`) | PakV4 extraction backend |
| `tools/collision/recon_module.py` | dump PE exports + keyword strings for any module (used for NAVX64/PathEngine/SIMWorld/SceneResponse) |
| `tools/collision/disasm_range.py` | RVA-range disassembler with IAT/export/float-constant annotation (used for the controller and NAVX64 passes) |
| `tools/collision/audit_physic_lists.py` | classify map models against the physic white/black lists (produces `audit_lists.txt`) |
| `tools/collision/find_import_calls.py` | linear-disassemble a PE and list all call sites of named imports/exports (used for NAVX64 CRT I/O) |

### 28.4 Proof directories

`proof/map_spike/` (baked collision + player logs/screens), `proof/gravity/` (tables + disasm),
`proof/movement/` (input + movement disasm), `proof/netcode/` (protocol, skills, camera,
doodads, modes), `proof/pvp/` (combat attributes/cast/buffs/modes), and per-worktree camera
proofs (`reborn-camara-fix/proof/`).

---

## 29. Prioritized gap register

IDs are stable references for future work. “Method” names the concrete next step.

### P0 — blocks reproduction

| ID | Gap | Method |
|---|---|---|
| G-1 | character solver/controller field labels — **constants resolved 2026-09-28** (0.01/0.025/0.2/0.04/0.4, scene flags, gravity, step tick; `proof/collision/disasm/physics_controller_setup.txt`); field mapping still open | locate the `createController(desc)` call site and the SIMWorld solver key consumers; label fields against the PhysX 3.3.4 `PxCapsuleControllerDesc` layout |
| G-29 | projectile/missile system — **client data model recovered 2026-09-28** (`proof/collision/missile/`, §22.2); server simulation and tick-base fitting still open | disassemble `KRLMissile::Update/HitTarget`, `KParabolaMissileProcessor`; fit velocity tick base from captures |
| G-25 | navmesh + `QueryPath` + obstacles + `bAutoPathing` — **decoded 2026-09-28**: 4-file set (p0 binary 12-byte header + stream payload; p1/p2 binary overlays; p3 text), loaded by the `LoadLibraryA` shim `Init(path)`; **no client module references NAVX64/PathEngine**, so this is the server/standalone stack and the data lives outside the pak | remaining: locate the server data files/naming and the client-side `KNavMeshQuery` mesh source |
| G-21 | `KG3DSceneResponse` semantics — **partial 2026-09-28**: the plugin has just 2 exports (`GetSceneResponse`, `GetStateFileInfo`) and no flag literals; the map state file `<map>.SRScene` (magic `SRS`) is empty on 龙门寻宝; the static gate is the physic lists (§8.4); `bUnitWalkable`/`bUnitCanPass`/`bBullet*`/`bAutoPathing` consumers still unreversed | disassemble `GetStateFileInfo` consumers and the engine unit-template readers around `KG3DEngineX64!0x541388-0x541401` |
| G-24 | water volumes + `UpdateFluxCollisionHeightMap` | disassemble `_FillMapWaterData`/`KG3D_LoadTerrainWaterData`; find native flux implementation |
| G-3 | `comLogic` flags ignored by bake — **audited 2026-09-28: all 4,965 objects are uniformly `obstacleOption=0`/`enablePhysicsConfig=0`; no behavioural impact**; the real gate is the physic white/black lists, now implemented (`--physic-lists`, H1) | validate H1 precedence in-game (G-35) |
| G-5 | FOLI `sceneScale` double-apply | unit test with known non-1.0 patterns (天原 deadwood 1.299998, 龙门 rock6 0.5921); fix exporter or runtime |
| G-0 | unit conflict 100 vs 192 u/m | run the decisive walk-speed/mesh-vs-cell experiment (§26.1); correct docs consistently |

### P1 — fidelity

| ID | Gap | Method |
|---|---|---|
| G-27 | combat use of bone boxes / body bone box | disassemble `OnSyncBodyBoneBoxSize`; trace `IsRayIntersectBoneBox` callers beyond camera |
| G-10 | advanced/movable obstacle runtime + net handlers | disassemble `KScene::ChangeAdvancedDynamicObstacleState`, `CheckCollisionRange`; decode `OnAdd/RemoveAdvancedDynamicObstacle` payloads |
| G-19 | `target.lua` / `skill.lua` target selection — **decompiled 2026-09-28** (`proof/collision/ui_scripts/`): local candidate list, `SelectTarget(PLAYER/NPC/DOODAD/FURNITURE/DUMMY)`, filters (`CanSelectPlayer`, `IsCorpseAndCanLoot`, `g_nTabPlayerPriority`, `bOnlyPlayer`), per-skill cast modes/flags recovered | residual: C++ `GetSearchTargetPlayer` candidate ordering/filters |
| G-23 | interaction range value + server legality | find `nCustomInteractRange` source (INI/table); trace `ProcessCustomInteractRange` |
| G-17 | camera ray mask `0x301` bit split / 9-ray trigger `+0x15C` | disassemble dispatcher `0x18032EA40` and camera field reader |
| G-15 | per-frame swim step | trace `SwimTo` update path and represent water frame data |
| G-16 | ragdoll blend/update loop | disassemble `KPhysicsRagdoll` update + represent blend |
| G-13 | real step-up offset / slope limit | part of G-1; validate in game by stair tests |
| G-7 | per-map `.cflags` for maps beyond the five | run `export_camera_flags.py` per extracted entity dump; copy sidecars |
| G-35 | physic-list rule precedence (H1) and the `.srt` bypass are hypotheses; the folder `树` is in both lists | A/B: bake 龙门寻宝 with/without the list filter; walk into a blacklisted prop (wall lantern/pen holder) and a `s` mesh tree; compare blocking against the live client |
| G-2 | terrain hole/water file naming not found under guessed templates (24 candidates) | disassemble the loader path composition; probe a water-heavy map (海岛绝境); extract and parse `.hlb`/`WaterData` once located |

### P2 — completeness

| ID | Gap | Method |
|---|---|---|
| G-33 | billboard-only SpeedTrees (海岛 758) walk-through | decode `.srt`/billboard geometry or accept client behaviour |
| G-6 | non-uniform foliage scale dropped | store 3 scales in FCOL v3 or reject |
| G-26 | `KSKILL_RESULT` 9-byte split — **header layout recovered 2026-09-28** (flags `+0x1b/+0x1c`, caster `+9`, target `+0xd`, `+0x15`, `+0x16`, `+0x1a`, count `+0x22`, records `0x23+9i`); per-record field meaning open | trace the per-record consumer and compare against combat captures |
| G-30 | rating→% damage formulas | attribute dump diffing + capture fitting |
| G-28 | weapon collision/traces | search weapon model/bone paths; likely none client-side |
| G-31 | undecoded CastMode variants (`Column`, `TargetHoodle`, `SectorOfAttention`, `CasterConvexHullArea`, `PointAreaFindFirst`) | find example skills + captures |
| G-32 | multi-target ordering policy | capture fitting (server) |
| G-18 | cursor scene-pos conversion internals | annotate `GetRLCursorScenePos` disasm |
| G-20 | ability ground-target constants (FOV/aspect/range) | recover from camera contract instead of hardcoding |
| G-9 | state-machine prop colliders | trace `KGStateMachine` physics attachment |
| G-11 | conveyor/carrier runtime math | disassemble conveyor param application, manned-space bind |
| G-12 | actor-actor collision / push-apart | trace `ResponseIntersect*`, `SetInteractor(bCollision)` consumers |
| G-14 | hosts still on continuous Euler movement | port §12.3 integer model |
| G-22 | environment volumes' collision role | disassemble `_ReadParamFromFile` and `QueryPositionIsIndoor` consumers |
| G-34 | movement S2C IDs other than `OnSyncMoveState=0x19` | annotate protocol table entries |

### Closing statement

The client-truth layers (world bake, movement integration, camera obstruction, query rays) are
documented to reproduction fidelity. The server-authoritative layers (combat resolution,
projectiles, nav legality) are documented as the inferred model with explicitly fitted
parameters. The P0 table lists exactly what new reverse-engineering closes the remaining
observable gaps.





