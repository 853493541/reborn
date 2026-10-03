# Host collision system vs the game's collision system — full comparison

Date: 2026-09-29
Branch: `agent/collision-improvement`
Purpose: one place that answers "what does the real client's collision system
consist of, what have we ported, what do we have, and what do we NOT have".
Companion docs: `JX3_COLLISION_SYSTEM.md` (game truth + gap register
`G-0..G-35`), `COLLISION_SYSTEM_STATUS.md` (working audit),
`STRUCTURE_COLLISION_RESEARCH.md`, `JX3_STEP_FORGIVENESS_RESEARCH.md`,
`REAL_CLIENT_MAP_COLLISION.md`.

Legend: **[PORTED]** host implements the game mechanism · **[PART]** partial ·
**[PROXY]** host stand-in for game data that is not in the client install ·
**[MISSING]** researched, not implemented · **[SERVER]** server-owned or absent
client-side · **[N/A]** does not apply to this product.

---

## 1. System-by-system view (game domains from `JX3_COLLISION_SYSTEM.md` §1.1)

| # | Domain | Game (client truth) | Host (`client/`, `tools/`) | Status |
|---|---|---|---|---|
| 1 | Core physics runtime | PhysX 3.3.4 (`PhysicsEngineX64.dll`), gameplay wrapper `SIMWorldX64.dll` (`PxWorld::RayCast`, `GetFloorHeight`, `SweepEx`) | own float solver: `FoliageCollision` capsule/triangle + substeps; no PhysX (MovieEditor never creates a physics scene — proven) | **[PROXY]** |
| 2 | Collision math | `KBaseX64.dll` | in-house: closest-point-triangle, segment-segment, Möller–Trumbore | **[PORTED]** (equivalent math) |
| 3 | Terrain | R32 heights + `.hlb` holes, 512-cell regions, streaming; `ProcessVerticalMove` ground clamp; `ProcessDropSpeed` slope projection | `TerrainSampler.cs` (heights + holes, Z-flip verified); ground rules from `ProcessVerticalMove` (snap up, 64 u tolerance); **slope projection missing** | **[PART]** |
| 4 | Static world | `sceneinfo_full` objects + 4×4 matrices; physic white/black lists; sibling `.mesh.ini` flags; PhysX static actors | `bake_map_collision.py` → FCOL v2 bins; GB18030 physic lists; `.cflags` (camera only); exact capsule contact; CCT top-step (64 u budget, up-sweep clearance) | **[PART]** — obstacle flags not ported (§3) |
| 5 | Foliage / SpeedTree | `.foliage` v1, `.CollisionMesh`, canopy columns | byte-exact foliage decode; shipped `CollisionMesh` used verbatim; 6 degenerate trunks left walk-through (matches client); **canopy columns host-made** | **[PART]** |
| 6 | Dynamic objects | doodads/doors/chests (server stream), state-machine props (visual only, G-9), movable obstacles (radius+points, server state G-10), conveyors (params decoded G-11), carriers | none | **[SERVER/MISSING]** |
| 7 | Character body | kinematic capsule + SIMWorld foot solver; capsule values are semantic K/V (`capsule r50/l50` shape lib); CCT defaults recovered (`stepOffset 0.5 m`, `slopeLimit 45°`, `contactOffset 0.1`) | capsule `r=17 u, h=116 u` host-chosen (`RC_RADIUS/HEIGHT`); step budget 64 u with CCT up-sweep (the raise must clear the blocker) + standability gate on the support raise; contact offset not modelled | **[PART]** |
| 8 | Movement model | `KCharacter` **15 Hz integer** integration | per-frame float integration | **[WRONG vs G-14]** |
| 9 | Jump/fall/swim/fly | `JumpParam.tab`, `JumpFrameParam.tab`, `SkillMove.tab`; swim step `0x14031B640` | single jump/gravity constants; 轻功 chain, sprint dive, wall jump, suspend/fly, mount, swim all missing | **[PART/MISSING]** |
| 10 | Ragdoll/death | `KPhysicsRagdoll` (11-body), PhysX articulation | none (`bAddPlayerPhysicsActor=0`) | **[MISSING]** |
| 11 | Rays/filters/camera | `RayIntersection*` mask `0x301`, `FilterCamera`, `GetFloorHeight` | bake raycast + engine terrain/scene rays; mask/filter model recovered; camera deviations B1–B14 registered | **[PART]** |
| 12 | Picking/selection | cursor rays, `PickRayWalk`, target.lua | none (server validates) | **[SERVER]** |
| 13 | Triggers/volumes | `KG3DSceneResponse`, zone mgr, PhysX triggers | none (no collision consumer found, G-22) | **[N/A]** |
| 14 | Water/fluids | waterline math decoded, `.WaterData` editor-only, shipped water in scene blocks | none | **[MISSING]** |
| 15 | Navigation | NAVX64/PathEngine — server/standalone stack, no client module | none | **[SERVER]** |
| 16 | Combat collision | CastMode shapes, missiles, bone boxes, server results | none (client plays effects only) | **[SERVER]** |

Fidelity definition (`JX3_COLLISION_SYSTEM.md` §1.3): positions/blocking,
support/falling, camera placement, targetability/hit outcomes observable-equal.
Host today is faithful in blocking geometry (mesh-level), ground rule and
camera obstruction, and deviates in movement integrator, slope, props and all
server-owned dynamic state.

## 2. What we have vs what we do NOT have (data inventory)

### 2.1 In the client install — we have it

| Data | Location | Host use |
|---|---|---|
| Terrain heights (R32) + holes (`.hlb`) | paks | `TerrainSampler` |
| Static object list + matrices (`sceneinfo_full/*.json`) | paks | bake |
| Object render meshes (`.mesh`) | paks | bake |
| Foliage instances (`.foliage`) | paks | bake |
| SpeedTree `.CollisionMesh` | paks | bake (verbatim) |
| Physic lists (`physic_file/folder_white/black`) | paks | bake gate |
| Per-mesh `.mesh.ini` flags (see §3) | paks | **only `bObscatleCamera` used** |
| `comLogic.obstacleOption` / `enablePhysicsConfig` | `sceneinfo_full` | scanned: uniformly 0 on 724 objects (G-3) |
| Physic params (`physic_shape_param`, `physic_rigid_param`, `physic_character_param`, `physic_conveyor_belt_param`) | paks | decoded in proof, **not wired** |
| Jump/move tables (`JumpParam`, `JumpFrameParam`, `SkillMove`) | paks | partially ported |

### 2.2 In the client install — we do NOT have it

| Data | Why not | Consequence |
|---|---|---|
| `bUnitWalkable` / `bUnitCanPass` / `bBulletWalkable` / `bBulletCanPass` / `bAutoPathing` / `nPathingType` / `fPathingHeight` **values** | engine unit-template keys exist in `KG3DEngineX64.dll`; the value source (unit templates / scene-response stream) is not in the shipped files found so far (`G-21`) | prop passability is a host proxy (§6) |
| `bAutoProduceObstacle` / `bLogicObstacle` **semantics** | **REVERSED 2026-10-01 (HIGH, disasm)**: `KG3D_LoaderNoRenderX64` readers `_LoadDotIni` (`0x18002c6d0`) + `_LoadDotMeshDotIni` (`0x18002c9a8`) parse `[Display] bAutoProduceObstacle` (default 1) and per-LOD-submesh `bLogicObstacle` (default 1) from TWO files (`<base>.ini` + `<base>.mesh.ini`; a stub `.ini` ships for many meshes - first-file-wins was a host bug). The produced obstacle = the union of LOD0 submeshes with `bLogicObstacle=1`; no logic submesh OR auto=0 -> nothing (unless an authored sibling). Physics chain: actor `option.ppszCollsionFileArray` -> `PhysicsEngine::_CreateCollisionFromMeshFile` / `PhysicsShapeFactory::CreateCacheShapeFromFile` (`PhysicsEngineX64.dll`, strings `0x1800fb180`/`0x1800faab8`) | host now ports both bits (`.oflags` bit0/bit1; skip when either is 0 and no sibling) |
| Server scene response set (which objects are active/nav-walkable at runtime) | server stream (`G-25`, `G-35`) | H1 folder-white admits all `maps_source` props |
| Doodad/door open/closed state | server | closed geometry blocks in host |
| Movable/advanced obstacle live states | server (`G-10`) | none |
| Conveyor live state | server; params local | none |
| Navmesh data | not in client install (`G-25`) | none |
| 15 Hz movement snapshots / server authority | netcode | host is its own authority |

## 3. Per-object / per-unit flag inventory (the missing semantics)

Every mesh ships a sibling `.mesh.ini`; every placed object ships `comLogic`.
Ported column: does the host use it today.

| Key | Ships in | Consumed by (game) | Census on 龙门寻宝 | Host |
|---|---|---|---|---|
| `bObscatleCamera` | `.mesh.ini` `[Display]` | KG3D mesh property → camera filter | 316/692 zero (map) | **[PORTED]** `.cflags` |
| `bObstacleCamera` | `.mesh.ini` per LOD submesh | KG3D | mixed | **not ported** |
| `bAutoProduceObstacle` | `.mesh.ini` `[Display]` | `KG3DEngineX64` (field `+0x194`), `KG3DEngineDX11EX64`, `KG3D_LoaderNoRenderX64` | bake census: 19 zero of 681 (flags/lanterns/racks/mats/hut) | **[PORTED P0]** `.oflags` bit0 |
| `bLogicObstacle` | `.mesh.ini` per LOD submesh | `KG3DEngineDX11EX64`, `KG3D_LoaderNoRenderX64` (reader `0x18002c9a8`, default 1) | 37 meshes with no logic submesh (rug, mats, jars...), 1 mixed | **[PORTED 2026-10-01]** `.oflags` bit1; obstacle = union of logic=1 LOD0 submeshes |
| `bCollisionOnly` | `.mesh.ini` per LOD submesh | `KG3DEngineDX11EX64` | mostly 0 | **not ported** |
| `bTransparentCamera`, `bRecomputeNormals` | `.mesh.ini` per submesh | KG3D | — | not ported (not collision) |
| `bUnitWalkable`, `bUnitCanPass` | engine unit-template keys (`KG3DEngineX64` strings 0x692740/0x692750) | scene-response units | **values not in shipped files** | **not ported / no data** |
| `bBulletWalkable`, `bBulletCanPass`, `bAutoPathing`, `nPathingType`, `fPathingHeight` | same stringset | same | **no data** | **not ported / no data** |
| `comLogic.obstacleOption`, `enablePhysicsConfig` | `sceneinfo_full` objects | editor/stream | uniformly 0 (G-3) | n/a (no behaviour) |
| physic lists | `Represent/physic/*.txt` (GB18030) | `StaticPhysicsSceneManager` | 60 rejects | **[PORTED]** |
| `physic_shape_param` (box/sphere/capsule/mesh shapes) | `.krl.txt` | SIMWorld shape builder | decoded | **not ported** |
| `physic_conveyor_belt_param` | `.krl.txt` | SIMWorld conveyor | decoded (G-11) | **not ported** |

**Bottom line:** the game decides prop/unit collision and walkability through
these flags; the host currently ports only the camera flag and the list gate.
Solid-prop handling is therefore a **proxy**, not the game rule (§6).

## 4. Gap register rollup (`G-0..G-35` vs host)

Solved gaps are game-side research; the host column marks whether the
mechanism is in the product.

| Gap | Game truth | Host |
|---|---|---|
| G-0 units (1 u = 1 cm, 100 u/m) | solved | used |
| G-1 capsule values | desc mapped; values semantic (`capsule r50/l50`) | host 17/116 **[HOST-CHOSEN]** |
| G-2 holes | solved | ported (A/B region 0,0) |
| G-3 `comLogic` flags | uniform 0, no impact | n/a |
| G-4 geometry classes | `.mesh`/`.srt` only | ported |
| G-5 FOLI scale | fixed in exporter | ported |
| G-6 non-uniform foliage scale | non-issue | ported |
| G-7 per-map `.cflags` | solved | ported (5 maps) |
| G-9 state-machine props | visual only | n/a |
| G-10 movable obstacles | radius+points, server state | **[SERVER]** |
| G-11 conveyors | decoded | **not ported** |
| G-12 actor-vs-actor | soft toggles only | n/a |
| G-13 step/slope | CCT defaults recovered | step ported (64 u), **slope missing** |
| G-14 15 Hz integer movement | research complete | **not ported** |
| G-15 swim | solved | **not ported** |
| G-16 ragdoll | solved | **[MISSING]** |
| G-17 camera mask bits | solved | ported (camera) |
| G-18 cursor scene-pos | solved | n/a |
| G-19 target.lua | solved | n/a (server) |
| G-20 ground-target constants | backlog | n/a |
| G-21 SceneResponse semantics | plugin solved; unit-template keys are engine config read at load | **values not found; solid-prop proxy** |
| G-22 environment volumes | negative | n/a |
| G-23 interaction range | solved | n/a |
| G-24 water/Flux | solved (scope) | **not ported** |
| G-25 navmesh | server stack | **[SERVER]** |
| G-26..G-34 combat/skill | solved/documented/server | n/a (server) |
| G-35 physic-list precedence (H1) | loaders located; rule implemented | ported; offline audit exact (60 = 29 file_black + 23 folder_black + 8 no_whitelist). **In-game A/B closed:** it needs live-game movement observation, which the locked constraints ban (no real-client hijack, §8); the client-side counterpart (list files + loader rule) is fully ported |

## 5. Registered host deviations (do not hide)

| Item | Where | Re-open when |
|---|---|---|
| Movement per-frame float instead of 15 Hz integer | `RebornClient` loop | G-14 port |
| Slope too permissive (any rise climbs) | ground rule | `ProcessDropSpeed` port |
| Capsule 17/116 / step 64 u | host constants | exact values extracted (G-1/G-13) |
| Tree canopy columns host-generated | bake | `.srt` canopy decode |
| Camera 20 Hz obstruction cap | `RC_CAM_OBSTHF` | engine camera cadence recovered |
| Camera stabilizers B8/B9/B11/B12/B13/B14 | `docs/camera/HOST_DEVIATIONS.md` | P4 native filter/represent port |
| **Solid-prop proxy** (name taxonomy 柜/箱/桌/桶/缸/坛 + AABB push) | `FoliageCollision.SolidPropPush` | §3 flags ported + `bAutoProduceObstacle` rule or `bUnitWalkable` data |
| H1 folder-white list rule | bake | in-game per-object A/B (G-35) |
| Hole cells bottomless | terrain | cave meshes baked |

### 5.1 Why the solid-prop fix is "partly" game-respect (recorded verbatim scope)

1. **Deciding which props are solid**: game uses `bAutoProduceObstacle` /
   `bLogicObstacle` (shipped, consumers known) and per-unit
   `bUnitWalkable`/`bUnitCanPass`; the host uses mesh-name taxonomy.
2. **Solid shape**: game generates the obstacle in its physics pipeline; host
   uses the world AABB.
3. **Missing values**: `bUnitWalkable`/`bUnitCanPass` values are not in the
   shipped files we have (G-21).

Behaviour (cannot be inside furniture) matches; data/shape are proxies until
those three items are ported.

## 6. Verification matrix (host)

| Check | Command / evidence | Result |
|---|---|---|
| Offline collision gate | `bin64\collision_selftest_reborn_client_collision.exe` | 22/22 PASS |
| Hole mask A/B | `tools/collision/check_hole_mask.py` vs `RC_HOLE_DUMP` | region (0,0) match |
| Physic-list rejects | bake audit | 60 (29+23+8) |
| Ground/step rules | selftest step cases + in-game logs | 64 u budget, 51 u floors |
| Rug floor (inverted winding) | `reborn_20260929_212757.log` | py=924 on rug, hits=0 |
| Solid prop (cabinet) | `reborn_20260929_234229.log` | held at z=36704, 2–9 u pushes |
| Camera step snap (B14) | `reborn_20260929_220246.log` | max 6.0 u/frame (was 63.9) |


## 8. Invented-behaviour census and accuracy plan (2026-09-29)

"Invented" = host does something the game does not, or uses a host number/rule
where the engine has one. Registered camera deviations are counted here too
where they affect collision/camera geometry. Excludes harness switches
(demo/env) and telemetry.

### 8.1 Census — 27 active behaviours

| # | Behaviour | Kind | Where |
|---|---|---|---|
| 1 | per-frame float integration instead of 15 Hz integer | rule | `RebornClient` loop |
| 2 | no slope projection/air-stop (`ProcessDropSpeed`) | rule | ground step |
| 3 | 64 u step budget applied to mesh obstacles (engine `stepOffset` is 0.5 m) | number | `RC_STEP_HEIGHT` |
| 4 | ~~`lowTop` fallback in the step rule~~ **REMOVED 2026-10-02** (census audit): the engine CCT steps onto the actual contact surface only. The step is now the engine sequence: up-sweep → forward sweep by the move at the raised height → down-sweep landing (highest surface within stepOffset, terrain included), with the engine slopeLimit 0.707 enforced on the landing and on ground support | rule | `FoliageCollision.Resolve` |
| 4b | step raise requires CCT up-sweep clearance (raised capsule must not overlap the blocker; else block+push-out, no embedding); support raise only to a standable surface (downward contacts reject; horizontal overlaps left to the prop push) | rule | `FoliageCollision.CapsuleBlocked/CapsuleBlockedDown` |
| 4c | horizontal faces resist the horizontal motion (a face whose normal points along the move is flipped - thin-wall pop-through prevented, field case z~33870 building 001_002) | rule | `FoliageCollision.Resolve` (hMove) |
| 4d | movement substeps capped below the capsule radius (`min(20, 0.9*radius)`) - thin small faces (foliage/rock slivers) no longer creep at 20 u substeps | number | `RebornClient` loop + audit |
| 4e | tree trunk prisms measured from the VISUAL mesh when the shipped `.CollisionMesh` is degenerate (62/68 trees in 龙门寻宝); not game data, kept to avoid a walk-through hole. Recover via `.srt` decode or engine obstacle production | proxy | `export_structure_collision.trunk_prism_from_mesh` |
| 4f | step budget **64 u** (game-side `KCharacter::ProcessVerticalMove` ground/landing tolerance `0x14031A25E` = 1 尺; 51 u house-floor field case) vs the PhysX `PxControllerDesc` ctor default stepOffset 0.5 m = 50 u (ctor dump `proof/collision/disasm/pxcontrollerdesc_ctor.txt`). Audit 2026-10-02: the client prediction has **no client-side step rule** (server-authoritative, §8.3) and the player body is the SIMWorld/KCharacter solver (G-1 — no proof it uses a PxController), so the PhysX library default is not the gameplay value; 64 u kept. slopeLimit 0.707 + contactOffset 0.1 enforced from the same dump on the step landing and ground support. `RC_STEP_HEIGHT` overrides | number | `RC_STEP_HEIGHT` |
| 5 | 20 u movement substep | number | `RebornClient` loop |
| 6 | floor source = terrain sample + `SupportHeight` (not native `GetFloorHeight`) | proxy | both |
| 7 | winding-agnostic floor query (game winding is inverted) | rule | `SupportHeight` |
| 8 | hole cells bottomless (no cave floor) | rule | terrain |
| 9 | solid-prop decision by mesh-name taxonomy | rule | `FoliageCollision` loader |
| 10 | solid shape = world AABB (game uses mesh/convex) | rule | `SolidPropPush` |
| 11 | prop push gates (`NearGeometry` 80 u, axis choice) | number | `SolidPropPush` |
| 12 | H1 folder-white list predicate unverified | rule | bake |
| 13 | tree canopy columns generated (extruded prisms) | rule | bake |
| 14 | degenerate trunk columns sized from visual mesh (6/68 match client walk-through) | proxy | bake |
| 15 | capsule 17/116 host-chosen | number | `RC_RADIUS/HEIGHT` |
| 16 | CCT `contactOffset` 0.1 recovered but not modelled | proxy | solver |
| 17 | in-house solver instead of PhysX/SIMWorld | proxy | `FoliageCollision` |
| 18 | camera anchor = chest + 90 u (game: head/socket) | number | `RebornClient` |
| 19 | 20 Hz camera obstruction query cap | number | `RC_CAM_OBSTHZ` |
| 20 | B4 camera terrain-ray substitute (heightfield march) | proxy | camera |
| 21 | B8 crossing guard (pull floors at 0) | guard | camera |
| 22 | B9 degenerate-hit guard (`RC_CAM_HITMIN` 3 u) | guard | camera |
| 23 | B10 final-camera wall gate (default off) | guard | camera |
| 24 | B11 hit stabilization window (0.4 s) | guard | camera |
| 25 | B12 scene near-hit floor (80 u) | guard | camera |
| 26 | B13 flag=0 structures block (no fade) | guard | camera |
| 27 | B14 anchor-Y step smoother | guard | camera |

Kind totals: **rules 10 · numbers/gates 6 · proxies 5 · guards 6.**
Camera register totals for reference: A 11 + B 14 + C 9 + D 6 = 40 rows
(`docs/camera/HOST_DEVIATIONS.md`), of which 8 are counted above (B4, B8-B14,
anchor, 20 Hz).

### 8.2 Plan — shortest path to a more accurate system

Not a from-scratch rebuild first: the solver core (exact capsule/triangle
contact, substeps, step/ground rules) is already game-shaped. The accuracy gap
is **data/semantics + integrator**. Ordered by impact/effort:

| Phase | Work | Data/source | Effect |
|---|---|---|---|
| P0 | **DONE + reversed 2026-10-01:** `tools/export_obstacle_flags.py` + `<bin>.oflags`; the engine rule (disasm, HIGH) = the obstacle is the union of LOD0 submeshes with `bLogicObstacle=1`; skip when `bAutoProduceObstacle=0` OR no logic submesh, unless an authored sibling exists (bit5). Fixed: the exporter must MERGE `<base>.ini` + `<base>.mesh.ini` (the stub `.ini` ships for many meshes and first-file-wins lost every DisplaySub key -> logic bits were 0 on 343/681 meshes). All 5 maps re-exported (龙门寻宝 auto=0 19 / no-logic 37; 夜晚 25/41; 白龙 30/63; 天原 48/51; 海岛 0/0). Selftest 33/33 incl. `obstacle_flag_logic0_walkthrough`; in-game: pile still blocks, rug stable | `.mesh.ini` + `KG3D_LoaderNoRenderX64` disasm | done |
| P1 | **RESOLVED (negative) 2026-09-30:** no generated obstacle shape exists client-side. `PhysicsEngineX64` (client+MovieEditor, identical imports) takes `PxCreateCooking`, registers box/sphere/capsule/convex/triangle/heightfield geometries, and its symbol blob names `_GetCollisionGeometryFilesFromGroupFile` (`.CollisionMesh`/`.proxymesh`/`.mesh`), `_CreateCollisionDataFromFile`, `_CreatePxActorFromMeshFile`. The baked props have no sibling collision file, so the fallback is the render `.mesh` cooked as a triangle mesh. Our baked triangle geometry therefore equals the game's obstacle geometry; there is no AABB/generated-box path for statics (AABBs exist only as `actorBoundBox` metadata). Prop-interior solidity is server/nav; the AABB push stays a labeled proxy. Remaining refinement: per-submesh `bLogicObstacle` filtering in the bake (few mixed meshes) | client DLLs + paks | - |
| P2 | **Sized 2026-09-30:** `ProcessDropSpeed` @`0x140316BE0` is Q5/Q8 fixed-point over two 3-bit cell slope fields (`(cell>>1)&7`, `(cell>>4)&7`; slope 0-1 skips the projection) - porting it plus the 15 Hz integrator is an integer-model task (G-14) with its own verification, not a scalar patch | `REBORN_JUMP_FALL_SPEC.md`, `proof/gravity/disasm` | removes #1, #2; feel parity in air/landing/slopes |
| P3 | **BLOCKED (runtime K/V) - re-verified 2026-10-01.** Shape-library id 6 (`capsule r50/l50`) and the rigid params are consumed by `PxWorld::GetRigidParam` / `ShapeData` for **dynamic** actors; the gameplay movement capsule is the SIMWorld scenario K/V (`capsules radius`/`capsules length`). New disasm: the keys are interned by tiny accessors `SIMWorldX64.dll 0x180001ba0` / `0x180002700` (store the key handle into a global) and consumed by the `_AddCapsules` builder `0x1800223b0` (validator + vtable dispatch; no default constants). No shipped value source: `physic_character_param.krl.txt` has only `EnableCharacterCapsule=1` + ragdoll bodies; `physic_shape_param.krl.txt` capsule r50/l50 is a named per-object shape; the extracted `player_*` configs contain no radius/height. Host keeps 17/116 (scaled from the 花萝 bind pose) as a registered value until a runtime/capture source exists | engine semantic K/V | closes as runtime boundary |
| P4 | **Probes negative 2026-09-30c (3 sets):** (1) the scene objects' `comEditor.templateFile = "npc.json"` is an editor template; 12 candidate pak paths absent. (2) `Represent/filepath.ini` lists only the 4 physic files we already use (`physic_shape/rigid/character/conveyor`) + the white/black lists. (3) `Represent/npc/npc_property.krl.txt`, `npc_model.ini`, `common/scene_config.txt`, `Common/scene_data.krl.txt` contain no walkable/pass/obstacle keys. Values presumably arrive with the server scene-response stream. Next candidate: decode the `KG3DSceneResponse` binary format (G-21) or the server unit templates. Until then the solid-prop proxy (taxonomy + AABB push) stands as the registered stand-in | paks + engine | removes the solid-prop proxy |
| P5 | **Feasibility confirmed; static payoff bounded 2026-09-30d.** In-client the game's stack runs (`RC_PHYS_PROBE=1`): manager, `LoadTerrain ok=1`, `UpdateTerrain` streaming, `StaticPhysicsSceneManager` created (dyn vt[2]=UpdateScene 0x2BA90, vt[4]=LoadFromFile 0x2BB80). `LoadFromFile(sceneDir, mapName)` returns ok=0 because the shipped runtime `entities/sceneinfo/*` does not exist (only editor `sceneinfo_full/*`; pak check) - the static object set is server-streamed, same boundary as P1/P4. Remaining P5 value: engine PhysX query API on terrain/dynamic actors, not a static-collision source. Scan diagnostic fixed (private-memory filter). Standalone `tools/p5_physics_probe` kept as the fresh-process bootstrap proof | game `bin64` DLLs | bounded | Appendix: **query API closed as optional** 2026-09-30 - `SweepEx` anchor (name/assert at `0x18001C7FB`, scene vtable RVA `0xFA7B8`, vt[16] = RVA `0x1C5B0`); the call-site ABI is not recovered, and blind-calling PhysX would be guesswork; next probe = wide dump of the vt[16] call site. Terrain queries are already covered (TerrainSampler + EngineRay), so this is not required for current-map parity. **Update 2026-10-01:** the vtables are now mapped (`proof/movement/phys_engine_vtables.txt`: PhysicsTerrain @0xFCFE0 / PhysicsScene @0xFA7B8 / StaticSceneMgr @0xFCCA8 / PhysicsManager @0xFA6C0, every entry's RVA + role); the terrain has NO height query (its vt[7] is a region/tile mask test - the heightfield is a PhysX object), so engine queries need a PhysicsScene instance (manager vt[14] `CreatePhysicsScene` requires the engine-side arg). Engine-physics-in-host continues: (a) find the adapter's engineArg, (b) else reuse the engine's own PxPhysics/PxCooking (manager fields) for a private scene with our baked geometry, then A/B the engine's floor/sweep vs our solver at the field spots |
| P6 | Dynamic/server-owned state (doodads, movable obstacles, conveyors, water) | server stream | only if the product simulates world state |

### 8.3 P2 task breakdown (15 Hz integer model + slope)

| Task | Content | Source | Verify |
|---|---|---|---|
| T1 tick | **DONE (first cut) 2026-09-30:** fixed 15 Hz accumulator (hitch cap 4), physics block runs per whole 1/15 s tick with `pdt`, positions quantized to integer cm after every tick (`reborn_20260930_143156.log`: integer positions, jump air ~1.0 s, FPS 235-306). the per-tick displacement is now integral too (`step = round(sp * pdt)`); remaining: align walk/run/sprint constants to the engine's exact per-frame integers (T4 below) | `REBORN_JUMP_FALL_SPEC.md`, `proof/movement/disasm/client_movement_symbols.txt` | `tools/gravity/verify_model.py` extended |
| T2 slope (slide) | **BLOCKED (scene/server data) 2026-09-30c.** The `0x14020EE10` helper is a 65-entry Q12 sine table (0..4096 for 0..90 deg, 1/64 steps) - dumped to `proof/gravity/heading_table_65x32.txt`. The function's inputs are the **in-memory terrain cell word** (`pEarthCell`, slope fields `(cell>>1)&7` / `(cell>>4)&7`, plus the road/path flag used by WalkTo). The shipped BCH heightmap is **normalized floats** (no packed cells), and the cell object/road flags come from the engine scene/nav (server-side) - same boundary class as G-21/G-25. Disposition: `slope2<=1` (normal terrain) -> `Vz=0` (no slide); slides engage only on authored road/slope cells. Feature not client-derivable; parked behind P5/server data | `proof/gravity/disasm/process_drop_speed.txt`, `heading_table_65x32.txt` | n/a |
| T3 render glue | **DONE (first cut) 2026-09-30:** model placement and camera anchor use the interpolated render position between the pre-tick state and the current tick state (`rpx/rpy/rpz`, alpha = tick fraction); gameplay math stays in the tick | this doc §8.1 #1 | A/B telemetry (`RC_CAM_YDBG`-style) |

| P0b | **DONE 2026-09-30:** `export_structure_collision.py` now substitutes the authored collision sibling (`_proxymesh.mesh` / `.proxymesh` / `.CollisionMesh`) when it exists in the pak (engine file-selection chain), with camera-flag lookup falling back to the render model. Re-baked all five maps: non-tree substitutions 白龙绝境 17, 天原绝境 133, 海岛绝境 8, 龙门寻宝_夜晚 15, 龙门寻宝 0; oflags regenerated for the new mesh order; client sanity unchanged (rug 924, hits=0, 238-248 fps, `reborn_20260930_154559.log`) | pak mesh files | done |
| T4 constants | **DONE 2026-09-30:** the shipped CommonNumber table (`proof/gravity/number.krl.txt`) has `CharacterWalkSpeed=6` / `CharacterRunSpeed=20` / `CharacterSwimSpeed=20` / Ride 8/40 (units per 15 Hz logic frame). Client now uses walk 90 u/s (6 u/f) and run 300 u/s (20 u/f) - exact integer per tick; in-game `spd=300u/s(RUN)` / `90u/s(WALK)` (`reborn_20260930_152403.log`). Sprint hold value remains a host test convenience (8.8 尺/s): the shipped rush tables (`player_rush_skill.txt`, `skill_rush_state.txt`) are 轻功 skill moves with no plain hold-to-sprint constant, so the game has no single sprint speed to port; `CharacterYawTurnSpeed=0.007465` noted for the camera A10 audit | `proof/gravity/number.krl.txt` | done |

Re-scope 2026-09-30: T2 is a steep-slope slide feature (airborne only), not a walking climb limit; the walking path has no client slope limit. Cost/benefit puts T2 behind P3/P4 unless steep-slope slides are a product need.

P0 is small and immediate; P2 is the biggest feel win; P1/P4 are the research
that removes the remaining proxies; P5 is the only "rebuild" that reaches full
fidelity and should be its own milestone decision.

## 9. Integration decision (2026-10-02): solver stays runtime; engine PhysX is the calibration gate

Evidence: `proof/movement/phys_engine_vtables.txt` 2026-10-02d/e/i; commits
`8d7e358`, `c1df994` (Phase-1 grid A/B) and `8cccdb6` (sweep boundary).

- **Decision**: the host keeps `FoliageCollision` (our solver) as the runtime
  collision backend. The engine's own PhysX (world-baked cook + `PxMeshQuery`
  midphase, opt-in `RC_PX_FIELD2`) remains the **calibration gate**, re-runnable
  on demand (`collision_grid_probe grid` + `tools/collision/pxdiff.py`).
- **Why**:
  1. Equivalence is measured: 0/7991 lattice poses at pitch 100 (0.00%); the
     coarse pitch-250 pair was triaged to the probe's 0.01-depth threshold.
  2. Runtime cost of the engine route: full-map cook ~9 s / ~105 MB cooked
     stream per map at startup - unacceptable for client boot; per-region lazy
     cooking is untested complexity.
  3. The solver is engine-rule-derived (oflags rule, baked geometry) and now
     calibrated against the engine rather than standing in for it.
- **Sweep closure**: the exported `PxMeshQuery::sweep` needs engine-internal
  state for its hit path (synthetic-triangle proof, 2026-10-02i); its
  in-penetration path matches the solver. Continuous motion is covered by the
  per-pose equivalence plus the solver-side wall/thin-wall A/Bs; no
  approximation is used in place of the sweep.
- **Re-open criteria**: a future grid run showing >0.5% systematic mismatches,
  or a reproduced walk-through class in play, re-opens an engine-backed query
  path (with per-region cooking) as a scoped task.

## 7. Reproduce

```powershell
client\build_client.cmd
C:\SeasunGame\MovieEditor\bin64\collision_selftest_reborn_client_collision.exe   # 33/33
# in-game: RC_DEMO_COLLIDE=1 RC_SPAWN=<x,y,z> RC_DEMO_DIR=<dx,dz> (logs in bin64\reborn_out)
# flag census: %TEMP%\opencode\flag_stats.py (fetches the 587 sibling inis via pss_assets.run_pakv4)
# engine-side calibration gate (Phase 1):
#   solver: collision_grid_probe.exe grid <night bin> x0 z0 x1 z1 pitch yStep [yMax]
#   engine: reborn_client_colltest5.exe with RC_PHYS_PROBE=1 RC_PX_FIELD2=1 RC_PX_GRID="x0,z0,x1,z1,pitch,yStep"
#   diff:   .venv\Scripts\python.exe tools\collision\pxdiff.py <engine log> <solver out> <yStep> [yMax]
#   last full run: pitch 100 over 18000..24000 x 24000..37000 = 0/7991 mismatch
```
