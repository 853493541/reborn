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
| 4 | Static world | `sceneinfo_full` objects + 4×4 matrices; physic white/black lists; sibling `.mesh.ini` flags; PhysX static actors | `bake_map_collision.py` → FCOL v2 bins; GB18030 physic lists; `.cflags` (camera only); exact capsule contact; CCT top-step (64 u budget) | **[PART]** — obstacle flags not ported (§3) |
| 5 | Foliage / SpeedTree | `.foliage` v1, `.CollisionMesh`, canopy columns | byte-exact foliage decode; shipped `CollisionMesh` used verbatim; 6 degenerate trunks left walk-through (matches client); **canopy columns host-made** | **[PART]** |
| 6 | Dynamic objects | doodads/doors/chests (server stream), state-machine props (visual only, G-9), movable obstacles (radius+points, server state G-10), conveyors (params decoded G-11), carriers | none | **[SERVER/MISSING]** |
| 7 | Character body | kinematic capsule + SIMWorld foot solver; capsule values are semantic K/V (`capsule r50/l50` shape lib); CCT defaults recovered (`stepOffset 0.5 m`, `slopeLimit 45°`, `contactOffset 0.1`) | capsule `r=17 u, h=116 u` host-chosen (`RC_RADIUS/HEIGHT`); step budget 64 u; contact offset not modelled | **[PART]** |
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
| `bAutoProduceObstacle` / `bLogicObstacle` / `bCollisionOnly` **semantics** | flags ship in `.mesh.ini`, consumers located (`KG3DEngineDX11EX64.dll`, `KG3D_LoaderNoRenderX64.dll`), production rule not yet reversed | host ignores them; props classified by name taxonomy (§6) |
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
| `bAutoProduceObstacle` | `.mesh.ini` `[Display]` | `KG3DEngineX64` (field `+0x194`), `KG3DEngineDX11EX64`, `KG3D_LoaderNoRenderX64` | bake census: 17 zero of 681 (flags/lanterns/racks/mats/hut) | **[PORTED P0]** `.oflags` bit0; auto=0 → no physics |
| `bLogicObstacle` | `.mesh.ini` per LOD submesh | `KG3DEngineDX11EX64`, `KG3D_LoaderNoRenderX64` | mixed per submesh (`jz_破旧兵营001_002` has 0+1) | **not ported** |
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
| G-35 physic-list precedence (H1) | loaders located; rule implemented | ported; **in-game A/B pending** |

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
| 4 | `lowTop` fallback in the step rule | rule | `FoliageCollision.Resolve` |
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
| P0 | **DONE 2026-09-30** for 龙门寻宝: `tools/export_obstacle_flags.py` + `<bin>.oflags` loader; 17 meshes / 56 instances skipped (`noObstacle=56`, `reborn_20260930_135518.log`), selftest 24/24; other 4 maps need their `.meshes.txt` sidecars first | `.mesh.ini` (census done) | removes wrong collisions |
| P1 | **RESOLVED (negative) 2026-09-30:** no generated obstacle shape exists client-side. `PhysicsEngineX64` (client+MovieEditor, identical imports) takes `PxCreateCooking`, registers box/sphere/capsule/convex/triangle/heightfield geometries, and its symbol blob names `_GetCollisionGeometryFilesFromGroupFile` (`.CollisionMesh`/`.proxymesh`/`.mesh`), `_CreateCollisionDataFromFile`, `_CreatePxActorFromMeshFile`. The baked props have no sibling collision file, so the fallback is the render `.mesh` cooked as a triangle mesh. Our baked triangle geometry therefore equals the game's obstacle geometry; there is no AABB/generated-box path for statics (AABBs exist only as `actorBoundBox` metadata). Prop-interior solidity is server/nav; the AABB push stays a labeled proxy. Remaining refinement: per-submesh `bLogicObstacle` filtering in the bake (few mixed meshes) | client DLLs + paks | - |
| P2 | **Sized 2026-09-30:** `ProcessDropSpeed` @`0x140316BE0` is Q5/Q8 fixed-point over two 3-bit cell slope fields (`(cell>>1)&7`, `(cell>>4)&7`; slope 0-1 skips the projection) - porting it plus the 15 Hz integrator is an integer-model task (G-14) with its own verification, not a scalar patch | `REBORN_JUMP_FALL_SPEC.md`, `proof/gravity/disasm` | removes #1, #2; feel parity in air/landing/slopes |
| P3 | **Lead 2026-09-30:** `physic_character_param.krl.txt` is the ragdoll body (bone capsules r6/l10-12), not the movement capsule. The gameplay capsule is shape-library entry `6 = capsule radius 50 length 50` in `physic_shape_param.krl.txt`; next probe = find who references shape id 6 (template/scenario) and the SIMWorld `capsules radius/length` K/V values, then map the unit scale to the host capsule (currently 17/116 host-chosen) | engine semantic K/V + shape library | removes #15/#16 |
| P4 | Locate the unit-template value source for `bUnitWalkable`/`bUnitCanPass` (reader xrefs in `KG3DEngineX64` → pak path) | client + paks | replaces #9 taxonomy with the game's per-unit rule |
| P5 | Endgame option: host the game's own physics stack (`SIMWorldX64` / `PhysicsEngineX64` + `StaticPhysicsSceneManager`) in the client host | game `bin64` DLLs (present) | full parity by construction; separate milestone, larger RE |
| P6 | Dynamic/server-owned state (doodads, movable obstacles, conveyors, water) | server stream | only if the product simulates world state |

### 8.3 P2 task breakdown (15 Hz integer model + slope)

| Task | Content | Source | Verify |
|---|---|---|---|
| T1 tick | **DONE (first cut) 2026-09-30:** fixed 15 Hz accumulator (hitch cap 4), physics block runs per whole 1/15 s tick with `pdt`, positions quantized to integer cm after every tick (`reborn_20260930_143156.log`: integer positions, jump air ~1.0 s, FPS 235-306). the per-tick displacement is now integral too (`step = round(sp * pdt)`); remaining: align walk/run/sprint constants to the engine's exact per-frame integers (T4 below) | `REBORN_JUMP_FALL_SPEC.md`, `proof/movement/disasm/client_movement_symbols.txt` | `tools/gravity/verify_model.py` extended |
| T2 slope (slide) | **Mapped fully 2026-09-30** (`0x140316BE0..0x1403171FD`, ~150 instr; the rest of the dump is other functions). Semantics: gate on `Vz[+0x270]!=0`; read terrain cell slope fields `slope1=(cell>>1)&7`, `slope2=(cell>>4)&7`; zero `Vz` and return for `slope2<=1` (flat/gentle = no drop) or states 0x1A..0x1D / `[+0xC20]` in 0x1A..0x1B; else Q12 trig (64-entry table `0x14020EE10`, angle = slope2*8 /64 circle) projects the velocity onto the slope, magnitude via `sqrtf`; if the projection is below the per-state threshold (`[rsp+0x60]` dwords x16) and `[+0x260]==0` -> `Vz=0` (stop, return 1); otherwise clamp `Vxy_fixed[+0x268]` 0..0x7FF and `Vz[+0x270]` -0x800..0x7FF and switch the move state. **This is an airborne/steep-slope slide model, NOT a walking slope-climb limit** - the walking path (WalkTo/RunTo) has no slope limit client-side (server validates). Requirements to port: expose the packed cell word from `TerrainSampler` (slope fields), dump the 64-entry trig table values, per-state thresholds; verify on steep terrain | `proof/gravity/disasm/process_drop_speed.txt` | offline vectors + in-game steep-slope slide run |
| T3 render glue | **DONE (first cut) 2026-09-30:** model placement and camera anchor use the interpolated render position between the pre-tick state and the current tick state (`rpx/rpy/rpz`, alpha = tick fraction); gameplay math stays in the tick | this doc §8.1 #1 | A/B telemetry (`RC_CAM_YDBG`-style) |

| T4 constants | **partially resolved 2026-09-30:** `JumpParam.tab` school 0 jump 0 = (XY 40, Z 90, G 11) integers -> 90 u/f x 15 = 1350 u/s and 11 u/f2 x 225 = 2475 u/s2 = the client's exact constants (15 Hz confirmed). Locomotion table not in the extracted set: `Sprint.tab` is dive caps (10..120 u/f XY, 8..900 u/f Z); the UI 5 尺/s at 1 尺 = 64 u = 320 u/s is 4.69 rounded at 20 u/f (300 u/s). Next: extract the walk/run table (settings/*.tab or user speed config) | `proof/gravity/JumpParam.tab`, `Sprint.tab` | in-game speed telemetry vs table |

Re-scope 2026-09-30: T2 is a steep-slope slide feature (airborne only), not a walking climb limit; the walking path has no client slope limit. Cost/benefit puts T2 behind P3/P4 unless steep-slope slides are a product need.

P0 is small and immediate; P2 is the biggest feel win; P1/P4 are the research
that removes the remaining proxies; P5 is the only "rebuild" that reaches full
fidelity and should be its own milestone decision.

## 7. Reproduce

```powershell
client\build_client.cmd
C:\SeasunGame\MovieEditor\bin64\collision_selftest_reborn_client_collision.exe   # 22/22
# in-game: RC_DEMO_COLLIDE=1 RC_SPAWN=<x,y,z> RC_DEMO_DIR=<dx,dz> (logs in bin64\reborn_out)
# flag census: %TEMP%\opencode\flag_stats.py (fetches the 587 sibling inis via pss_assets.run_pakv4)
```
