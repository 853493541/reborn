# Collision system — implementation status audit

Date: 2026-09-29
Branch: `agent/collision-improvement`
Scope: what of the researched JX3 collision system is **implemented** in the
client host (`client/`), what is **missing**, what is **wrong**, per subsystem.
Sources: `JX3_COLLISION_SYSTEM.md` (29 sections + gap register G-0..G-35),
`REBORN_JUMP_FALL_SPEC.md`, `REAL_CLIENT_MAP_COLLISION.md`,
`FULL_MAP_COLLISION.md`, `STRUCTURE_COLLISION_RESEARCH.md`,
`JX3_STEP_FORGIVENESS_RESEARCH.md`, `CLIENT_COLLISION_IMPROVEMENT_PLAN.md`,
and the code in this branch.

Legend: **[OK]** implemented and verified · **[PART]** implemented with known
gaps · **[MISSING]** researched, not implemented · **[WRONG]** implemented but
deviates from the engine · **[SERVER]** server-owned, out of client scope.

## 1. Terrain

| Item | Status | Notes |
|---|---|---|
| Height sampling via real loader (`LoadRegion`, vt[3]) | **[OK]** | `client/TerrainSampler.cs`; BCH verified against live samples (`§7.6`) |
| Holes (`LoadHoleRegion`, vt[4]) | **[OK]** | this branch; Z-flip A/B verified region 0,0; fall-through confirmed |
| Hole A/B on other regions/maps | **[PART]** | only 海岛绝境 region (0,0) compared |
| Terrain slope model (`ProcessDropSpeed`: cell slope projection, `Vz=0` air-stop) | **[MISSING]** | host snaps onto any rise (see 4., over-permissive) |
| R32 detail heights | **[RESOLVED 2026-10-04]** | same field as BCH: BCH = per-region normalized [0,1], row-flipped; `r32 = r32_min + bch_flip*(r32_max-r32_min)` (residual 2e-8); header floats semantics open — `docs/movement/TERRAIN_R32_BCH_RELATION.md` |
| Terrain normals/materials | **[MISSING]** | not exposed by the sampler |
| Streaming | **[PART - cache 2026-10-04]** | engine streams; sampler keeps a bounded LRU of regions (`RC_TERR_CACHE`, default 4) — kills border ping-pong reloads (16-85 loads/17 s -> 3, 0 per crossing), `docs/movement/TERRAIN_REGION_STREAMING.md`; spawn warmup handles region lag |

## 2. Static world (structures, bake)

| Item | Status | Notes |
|---|---|---|
| Bake pipeline (`sceneinfo_full` → distinct meshes → FCOL v2) | **[OK]** | `tools/bake_map_collision.py`, `export_structure_collision.py` |
| Engine physic white/black lists | **[OK — new]** | GBK decode fixed this branch; 60 rejects (29 file_black + 23 folder_black + 8 no_whitelist) |
| H1 predicate A/B vs live game | **[PART]** | `G-35`: implemented, never A/B-verified; folder-white admits all props/carpets |
| Per-object flags (`comLogic.obstacleOption`, `enablePhysicsConfig`, unit-template keys) | **[N/A on this map]** | all 0/uniform (`G-3`, `G-21`) |
| SpeedTree shipped `.CollisionMesh` | **[OK]** | used verbatim |
| Degenerate tree trunks measured from visual mesh | **[OK]** | 62/68; 6 stay walk-through like the client (`G-33`) |
| Tree trunk prisms (degenerate shipped CollisionMesh) | **[HOST - registered 4e]** | 62/68 trees in 龙门寻宝 ship a degenerate `.CollisionMesh`; the exporter measures a trunk prism from the visual mesh (not game data). Kept (deleting = walk-through hole); recovery: `.srt` decode or engine obstacle production. NOTE: no host "canopy columns" exist in the code - that line was stale |
| LOD mesh collision (`bUseLODMeshCollision=1`) | **[MISSING] (not needed per G-4)** | baked maps ship `.mesh`/`.srt` only |
| Doors/gates as dynamic state | **[SERVER]** | client files have no per-door data; closed geometry blocks |
| Selected-object blocking (server's world set) | **[SERVER]** | host uses the baked client geometry as a stand-in |

## 3. Foliage

| Item | Status | Notes |
|---|---|---|
| `.foliage` v1 decode + solid patterns | **[OK]** | byte-exact decode; exporter/runtime |
| Camera mesh flags (`bObscatleCamera` → `.cflags`) | **[OK]** | camera gate |

## 4. Character body & controller

| Item | Status | Notes |
|---|---|---|
| Capsule radius/height | **[HOST-CHOSEN]** | 17/116 (`RC_RADIUS/HEIGHT`); exact gameplay values not extracted (`G-1`) |
| Capsule convention (feet = capsule bottom) | **[OK — new]** | axis `py+radius … py+height−radius`; removed the below-feet artifact |
| PhysX CCT defaults (stepOffset 0.5 m, slope 45°, contactOffset 0.1) | **[PART]** | recovered from the shipped DLL; **step applied, slope not**; gameplay applicability unproven (`G-1/G-13`) |
| Step-up (climb obstacles whose top ≤ step budget) | **[OK — new]** | contact-local top + `lowTop`; budget = 64 u = 1 尺 ground tolerance (field case: 51 u house floors); **CCT up-sweep: the raise must clear the blocker** (wall-ledge ladders block, no embedding, `CapsuleBlocked`); `RC_STEP_HEIGHT` |
| Contact offset / skin | **[N/A — 2026-10-05]** | `contactOffset` 0.1 m is a `PxControllerDesc` **ctor default** (`pxcontrollerdesc_ctor.txt` +0x38); the online body is the SIMWorld/KCharacter solver (`bAddPlayerPhysicsActor=0`) with no recovered consumer/value (G-1). Same disposition as `stepOffset` 0.5 m — the ctor default is not the gameplay value (kept 64 u, commit `1a20b96`). Re-open only if the online character is proven to use the PxController (then apply 0.1 m) or a SIMWorld skin value is recovered |

## 5. Movement & ground

| Item | Status | Notes |
|---|---|---|
| Ground rule: no rise budget, snap up, 64 u = 1 尺 tolerance, fall beyond | **[OK — new]** | `ProcessVerticalMove` clamp + landing (`§3.2`) |
| Local floor source (engine `PxWorld::GetFloorHeight`) | **[PART]** | mirrored by terrain sample + `SupportHeight`; not the native query |
| Continuous integration (u/s at variable dt) | **[WRONG vs G-14]** | engine is 15 Hz integer; feel drift in air/landing |
| Horizontal substepping (tunneling) | **[OK — new]** | ≤ 20 u substeps |
| Slope handling | **[WRONG]** | any terrain rise is climbed (no slope limit/projection) — cliffs climbable |
| 轻功 chain, `JumpFrameParam` curves, sprint dive, wall jump, parkour, suspend/fly/bird, mount, flash | **[MISSING]** | data decoded (`REBORN_JUMP_FALL_SPEC.md`); not wired |
| Swim per-frame step | **[MISSING]** | `§13.3` decoded |
| Knockback / move sync | **[SERVER]** | client arrays unused |
| Ragdoll | **[MISSING]** | `bAddPlayerPhysicsActor=0`; no player physics actor |

## 6. Dynamic world

| Item | Status | Notes |
|---|---|---|
| Doodads (doors, chests, herbs) | **[MISSING / SERVER]** | streams from server; kinds/tables researched (`§10.1`) |
| State-machine props | **[NEGATIVE]** | visual only (`G-9`) |
| Advanced/movable obstacles | **[SERVER]** | radius+points model researched (`G-10`) |
| Conveyors/carriers | **[MISSING]** | params decoded (`G-11`); not wired |
| Water volumes / swim waterline | **[MISSING]** | shipped water in compressed scene blocks (`G-24`) |

## 7. Queries, zones, nav, combat

| Item | Status | Notes |
|---|---|---|
| Camera obstruction (bake `Raycast` + engine terrain/scene rays) | **[OK]** | 20 Hz query cap is a **provisional host policy** |
| Scene rays for camera masks/filters | **[OK]** | mask 0x301, filter gates recovered (`G-17`) |
| Picking/targeting | **[MISSING]** | client UI-side only; server validates |
| Triggers/zones/environment volumes | **[MISSING / NEGATIVE]** | no collision consumer (`G-22`) |
| Nav/pathing (NAVX64/PathEngine) | **[SERVER]** | not in the client install (`G-25`) |
| Combat hit detection/targeting shapes/projectiles | **[SERVER]** | client plays effects only; models documented (`§21/§22`) |

## 8. What is wrong right now (priority order)

1. **Terrain slope is too permissive** — any rise snaps the player up (no
   `ProcessDropSpeed` slope projection/air-stop). Steep cliffs are climbable.
   Engine behavior: slope-projected motion + `Vz=0` stop.
2. **15 Hz integer movement not ported** (`G-14`) — air/landing timing and jump
   chain differ from the game; single-jump constants are right, the integrator is not.
3. **Physic-list rule H1 unverified** (`G-35`) — folder-white admits everything
   under `maps_source`, including props/carpets; the live game's per-object
   walk-through cannot be derived without the server/nav data.
4. **SpeedTree canopy columns are host-generated** — not from game data; must be
   labelled provisional (re-open with `.srt` geometry decoding).
5. **Camera obstruction 20 Hz cap** — provisional; engine camera cadence not recovered.
6. **Holes: cave floors verified baked (claim corrected).** The scene data
   contains the 山洞/cave meshes (`sd_崖壁狱门fb_001_hd` etc.) and 25
   fully-underground instances (y down to -1200) and they ARE in the baked bin.
   The earlier blanket "hole cells fall bottomless" line was stale; re-check a
   SPECIFIC hole only with a cited hole cell (none registered so far).
7. **Capsule 17/116 / step 64 u** — host/engine-default values; exact gameplay
   capsule and step (G-1/G-13) still unextracted.
8. **Merged interiors** (e.g. `jz_xb玉门关建筑001_003sw_hd`) — walls, floors,
   furniture and carpets are one mesh; the CCT top rule now handles low parts, but
   per-part semantics remain server state.
9. **Door wood + interior rug: rug SOLVED; door stays server state.** The
   "carpet" is `wj_erg地毯001_hd` (structure bin inst 485, mesh 75): an
   ~800×800 u plate at y 921.8–924.2 authored with **inverted winding**
   (both triangles ny = −1.00, verified offline from the baked bin; scan:
   up=0 down=2). `SupportHeight` required up-facing normals, so the rug was
   never ground; the capsule surface contact pushed the player **up** onto it
   and the 64 u drop-tolerance snapped him back to terrain every frame:
   py oscillates 921↔924, horizontal pushes are zero (`hits=0`), camera anchor
   bobs (shake) — exactly the user report. Fix: the floor query is
   winding-agnostic (`Math.Abs(wny)/nl >= 0.5`; winding is a rendering
   property, the engine floor test is orientation-agnostic). Selftest
   `inverted_floor_support` / `inverted_floor_stand` (19/19). In-game verified
   by the agent at the exact user coordinate: `reborn_20260929_212757.log`
   spawn (19295,887,36435) → t=2s pos (19935,**924**,36435) grounded `hits=0`
   `colCalls=0`; same in `212423.log`. Next blocker east is inst 773 — the
   same rug mesh placed **vertically** (top 1115.9, feet 924.6), a real
   obstacle (standing/folded rug), not a bug. Doorway passability remains
   door/doodad server state (item 4; plan §8.1).
10. Dead code: `FoliageCollision.LocalTop` was superseded by `triTop`/`lowTop` (cleanup).
11. **Prop shells (furniture interiors) are enterable — OPEN, server/nav state.**
    The open-front cabinet `wj_erg柜子002_hd` (idx 648) is a hollow shell
    (front panels y 922-1008, open band y 1008-1224, no bottom, shelves at
    1017/1076/1085). A grounded walk-in is blocked (repro stops at z=36714,
    `blocked by inst=1042 top=1017.2`); entry comes from a legal 51 u step up a
    nearby ledge plus the engine's `stepOffset` (0.5 m) over the closed-door
    top (1007.8), or a jump through the opening. The live game keeps the player
    out of props via server/nav movement authority (not in the client install,
    `G-35`); the H1 folder-white rule admits the prop for physics. A
    winding-based "stay on the visible side" ejection was implemented and
    **reverted**: the shipped meshes use inverted winding (rug ny=-1; cabinet
    front normal points +z into the cabinet), so winding cannot identify the
    visible side; an AABB-exit variant oscillated between the cabinet and the
    house shell. Options for a future session, in preference order:
    (a) **solid-prop approximation** — treat `小物件/`-class instances as solid
    volumes (exit via the nearest AABB face whose destination is free), a
    registered host stand-in for server solidity; (b) recover the real
    server/nav static-collision data (may not exist client-side);
    (c) accept as the documented boundary.
    **Update 2026-09-29:** (a) shipped (`SolidPropPush`): volumetric furniture
    classes (柜/箱/桌/桶/缸/坛) are solid — capsule/AABB overlap is resolved as
    a minimum-translation wall contact (2–10 u per-frame pushes, no ejection
    bounce); near-geometry gate keeps thin-sheet AABBs non-solid; buildings
    keep shell collision. Selftest 22/22; pressing into the cabinet holds at
    z=36704 wall-like (`reborn_20260929_234229.log`).
    Per-mesh obstacle flags recovered for future work: each `.mesh.ini` carries
    `bAutoProduceObstacle` + per-submesh `bLogicObstacle`/`bCollisionOnly`
    (consumers `KG3DEngineDX11EX64.dll`, `KG3D_LoaderNoRenderX64.dll`); the
    per-unit `bUnitWalkable`/`bUnitCanPass` values remain unrecovered (G-21).
## 9. Verified this branch (quick index)

- exact capsule/triangle contact; segment contact; substeps; self-test gate (19 checks)
- holes (loader + flip) with engine A/B at region (0,0)
- GB18030 physic lists applied with the audit's exact 60 rejects
- ground rules from `ProcessVerticalMove`; capsule bottom at the feet
- CCT top step (within `stepOffset`), low-touching-face and below-feet handling
- winding-agnostic floor query (inverted-winding rug, inst 485)
- named blockers (`blocked by inst=… mesh=… top=… feet=…`) + COPY LOG

## Reproduce

```powershell
client\build_client.cmd
C:\SeasunGame\MovieEditor\bin64\collision_selftest_reborn_client_collision.exe   # 19/19
# engine A/B run: see the run logs cited in docs/EXPERIENCES.md (2026-09-29 entries)
```
