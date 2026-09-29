# Client collision improvement — gap audit and plan

Date: 2026-09-29
Branch: `agent/collision-improvement` (isolation worktree `reborn-iso-collision-improvement`)
Scope: the **main client** (`client/reborn_client.exe`) — the player capsule, terrain
support, baked structure/foliage collision and camera obstruction. Movement animation
and netcode are out of scope.

Status legend: **[MISSING]** not implemented · **[WEAK]** implemented, fidelity gap ·
**[TOOL]** verification/tooling gap · **[BACKLOG]** known, an implementation task.

## 1. What the client has today (verified)

| Component | Where | State |
|---|---|---|
| Terrain heights (real loader `LoadRegion`) | `client/TerrainSampler.cs:95-142` | works; 1-region cache, bilinear |
| Baked foliage + structures (FCOL bins) | `client/FoliageCollision.cs` | works; 800 u hash grid + per-mesh CSR triangle grid |
| Player capsule | `client/RebornClient.cs:925` (`17/116`, env `RC_RADIUS/HEIGHT`) | explicit params (exact CCT values not extracted, `JX3_COLLISION_SYSTEM.md` G-1) |
| Terrain slope rule | `RebornClient.cs:1588-1597` | `gh - ground > 70` at the **actual per-frame step** |
| Structure resolve | `RebornClient.cs:1621-1651`, `FoliageCollision.Resolve` | 3 iterations, deepest contact push-out, 70 u step-up |
| Support probe | `FoliageCollision.SupportHeight` | highest up-facing surface (`ny ≥ 0.5`) |
| Camera obstruction | `FoliageCollision.Raycast` + `client/EngineRay.cs` | Möller–Trumbore + native engine rays |
| Gravity/jump | `RebornClient.cs:1661-1679` | real table values as continuous u/s (`-2475`, `1350`) at variable dt |

The runtime rules the client follows are the calibrated host rules documented in
`JX3_COLLISION_SYSTEM.md` §11.4/§12.4 and `FULL_MAP_COLLISION.md` §Runtime collision design.

## 2. Missing parts (priority order)

### C-1 [MISSING] Terrain holes (cave voids) are solid ground
The game loader exposes `LoadHoleRegion` (loader vt[4] `0x180032540`): it opens
`<dir>\hole\<map>_%03u_%03u.hlb` (`.png` fallback) and packs a 1-bit-per-cell hole mask
(`RegionSize²/8` bytes) used by `_CreatePxActor`. `TerrainSampler` never calls it, so on
maps that ship holes (海岛绝境 ships `000_000.hlb` / `001_001.hlb`, 263,169 B = 513²)
the player stands on void (`JX3_COLLISION_SYSTEM.md` §7.2, §7.5 "Holes are not loaded",
§7.6; register G-2 solved research-side).
Mask semantics (decoded from `_ConvertHoleData` @ RVA `0x32e90`, dump:
`proof/collision/disasm/hole_convert_holedata.txt`): raw image is `(n+1)²` bytes,
row = Z, col = X; a **cell** `(x,z)` is a hole only when **all four** corner samples are
`≤ 0x7F` (255 = solid; 247-254 at edges); packed mask bit = `x & 7` of byte
`z * ceil(n/8) + (x >> 3)`, **bit set = hole**; the out flag is `1` when no hole exists.
Confidence: HIGH for the converter math (disasm), MED for row/col orientation until the
engine A/B in §4-F2.

### C-2 [WEAK] Capsule contact is 6-point sampled
`FoliageCollision.InstanceContact` (`client/FoliageCollision.cs:567-648`) samples 6 points
along the 116 u axis (23.2 u apart). A thin wall/pole/railing between samples is missed or
mis-penetrated, and the reported depth is a sample approximation. The exact primitive is
segment (capsule axis) vs triangle: segment endpoint→triangle face, triangle vertex→segment,
and segment↔triangle edge pairs. Evidence: `JX3_COLLISION_SYSTEM.md` §9.5.

### C-3 [WEAK] Slope rule is speed/framerate dependent
`gh - ground > 70` compares the rise across the **whole per-frame step**
(`RebornClient.cs:1585-1596`), so the effective slope limit depends on speed and frame time:
at walk (96 u/s, 16 ms) a ~86° rise blocks, at Shift-10x (3200 u/s) a ~24° rise blocks.
The map-host rule is a fixed 40 u look-ahead with a 70 u rise budget (≈60°); the client was
supposed to use the same (`REAL_CLIENT_MAP_COLLISION.md` §Object / steep-terrain collision).
Evidence: `JX3_COLLISION_SYSTEM.md` §12.4 (host rule), G-13 (exact CCT offset unknown).

### C-4 [WEAK] Discrete Resolve → tunneling at large steps
Horizontal motion teleports then pushes out (`RebornClient.cs:1587-1651`). A move larger
than the capsule radius (Shift-10x testing speed ≈ 51 u/frame, future dash/flash moves) can
cross a thin collider before any contact test runs. Fix: subdivide the move to
≤ radius/2 per substep and resolve each substep.

### C-5 [TOOL] No offline verification for collision
`camera_smoke.exe` covers the camera only. Every collision check today needs a live engine
run (`RC_COL_DEBUG`, teleport key `C`). The repo requires a reproduce/verify command per fix
(AGENTS §13), so the collision library needs a deterministic self-test executable that runs
without the engine or game assets.

### C-6 [BACKLOG] Exact 15 Hz movement/jump model
The client uses the real table constants converted to u/s and integrates at variable dt;
the real client is a 15 Hz integer state machine (`REBORN_JUMP_FALL_SPEC.md` §3, register
G-14). Out of scope for a collision pass; tracked here so it is not lost.

### C-7 [BACKLOG] Server-owned dynamics
Actor-vs-actor collision (G-12), movable obstacles/doodads (G-10), conveyors (G-11) are
server-owned; the client only needs them after netcode M2. No action.

### C-8 [BACKLOG] Water volumes
Shipped water is in compressed scene blocks (`JX3_COLLISION_SYSTEM.md` G-24); no collision
consumer in this pass. No action.

## 3. Fix plan (this branch)

Fixes land in this order; each is committed separately with its verify command.

1. **F4 first (tooling)** — `client/collision_selftest.cs`: synthetic-mesh tests over
   `FoliageCollision` (wall push-out, thin-pole detection, step-up, tall-wall block,
   support height, raycast, substepped move) using temp `.bin` files. Wired into
   `client/build_client.cmd`; runs without engine/assets. Gives every later fix a gate.
2. **F1 exact capsule contact** — replace the 6-sample loop in `InstanceContact` with
   exact segment-vs-triangle closest points; keep world-space distance/depth/normal
   semantics, `Resolve`/`SupportHeight`/`Raycast` APIs unchanged. Selftest case: 2 u-wide
   pole exactly at a former sample gap must block with penetration < 0.01.
3. **F2 terrain holes** — `TerrainSampler`: load the vt[4] packed mask with each region and
   expose `SampleGround(x, z, out height)` (false = over a hole; conservative cell test).
   `RebornClient`: terrain ground becomes optional; a hole under the player starts a fall
   and does not slope-block the move. Optional `RC_HOLE_DUMP=<file>` writes loaded masks
   for the A/B check against `proof/collision/terrain_extra/*.hlb` (Python conversion).
4. **F3 slope + substeps** — fixed 40 u look-ahead for the 70 u rise rule (speed
   independent) and horizontal movement substepped to ≤ max(radius/2, 20 u), resolving
   structures per substep. Selftest covers the substep helper; engine run re-checks a steep
   slope and a building.

Commit per fix: `Client: ...`. Docs: this file + `docs/movement/README.md` index +
`docs/EXPERIENCES.md`.

## 4. Verification

- **Offline gate (must pass):** `client\collision_selftest.exe` (built by
  `client\build_client.cmd`) → all checks PASS, exit 0.
- **Engine A/B (attempted, holes):** run the client on 海岛绝境 with `RC_HOLE_DUMP`,
  convert `proof/collision/terrain_extra/海岛绝境_000_000.hlb` with the decoded converter
  and diff the masks; walk into a hole cell and confirm a fall (log + screenshot under
  `proof/collision/client_holes/`).
- **Regression smoke (engine):** 龙门寻宝 walk into a cactus/rock (blocked), step onto a
  boulder (ground rises), fall off a ledge (fall starts) as in
  `FULL_MAP_COLLISION.md` verification.

## 5. Reproduce the hole-mask decode

```powershell
.venv\Scripts\python.exe tools\collision\disasm_range.py `
  "C:\SeasunGame\MovieEditor\bin64\PhysicsEngineX64.dll" 32e90 300 `
  proof\collision\disasm\hole_convert_holedata.txt
```

The converter is called from `LoadHoleRegion` (`engine_host_spike/recon_loader_methods.txt:391`).
