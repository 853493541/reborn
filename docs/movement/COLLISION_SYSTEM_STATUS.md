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
| R32 detail heights | **[MISSING] (not needed)** | BCH is authoritative; R32 relation unresolved (§7.6) |
| Terrain normals/materials | **[MISSING]** | not exposed by the sampler |
| Streaming | **[PART]** | engine streams; sampler caches 1 region; spawn warmup handles region lag |

## 2. Static world (structures, bake)

| Item | Status | Notes |
|---|---|---|
| Bake pipeline (`sceneinfo_full` → distinct meshes → FCOL v2) | **[OK]** | `tools/bake_map_collision.py`, `export_structure_collision.py` |
| Engine physic white/black lists | **[OK — new]** | GBK decode fixed this branch; 60 rejects (29 file_black + 23 folder_black + 8 no_whitelist) |
| H1 predicate A/B vs live game | **[PART]** | `G-35`: implemented, never A/B-verified; folder-white admits all props/carpets |
| Per-object flags (`comLogic.obstacleOption`, `enablePhysicsConfig`, unit-template keys) | **[N/A on this map]** | all 0/uniform (`G-3`, `G-21`) |
| SpeedTree shipped `.CollisionMesh` | **[OK]** | used verbatim |
| Degenerate tree trunks measured from visual mesh | **[OK]** | 62/68; 6 stay walk-through like the client (`G-33`) |
| Tree canopy columns | **[WRONG/HOST]** | host-generated colliders (registration required); not game data |
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
| Step-up (climb obstacles whose top ≤ stepOffset) | **[OK — new]** | contact-local top + `lowTop`; selftest cases |
| Contact offset / skin | **[MISSING]** | not modelled |

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
6. **Holes have no cave floor** — a hole cell falls bottomless until cave meshes
   are baked beneath.
7. **Capsule 17/116 / step 50 u** — host/engine-default values; exact gameplay
   capsule and step (G-1/G-13) still unextracted.
8. **Merged interiors** (e.g. `jz_xb玉门关建筑001_003sw_hd`) — walls, floors,
   furniture and carpets are one mesh; the CCT top rule now handles low parts, but
   per-part semantics remain server state.
9. **User-reported door wood + interior carpet: OPEN, not confirmed solved.** The
   fixes (capsule bottom at feet; CCT top step) make floor-level/thin geometry not
   block in the host model, and the interior coordinate from the user log ran with
   0 blocked events (`183202`), but the user has not confirmed the exact objects.
   Verification protocol: hit the object, COPY LOG, then read
   `blocked by … top=<x> feet=<y>` — top ≤ feet+50 means a host bug (fix), top >
   feet+50 means a real >0.5 m face per the engine's step rule. If a low object
   still blocks because the contacting triangle spans high (merged geometry), the
   next implementation is an engine-style **forward floor probe** at the movement
   target (query the walkable floor; climb when floor ≤ feet+stepOffset), instead
   of relying on the contact triangle's top.
10. Dead code: `FoliageCollision.LocalTop` was superseded by `triTop`/`lowTop` (cleanup).
## 9. Verified this branch (quick index)

- exact capsule/triangle contact; segment contact; substeps; self-test gate (16 checks)
- holes (loader + flip) with engine A/B at region (0,0)
- GB18030 physic lists applied with the audit's exact 60 rejects
- ground rules from `ProcessVerticalMove`; capsule bottom at the feet
- CCT top step (within `stepOffset`), low-touching-face and below-feet handling
- named blockers (`blocked by inst=… mesh=… top=… feet=…`) + COPY LOG

## Reproduce

```powershell
client\build_client.cmd
C:\SeasunGame\MovieEditor\bin64\collision_selftest_reborn_client_collision.exe   # 16/16
# engine A/B run: see the run logs cited in docs/EXPERIENCES.md (2026-09-29 entries)
```
