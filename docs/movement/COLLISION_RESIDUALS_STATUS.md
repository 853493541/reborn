# Collision residuals — status, boundaries and server contract (C workstream)

**Date:** 2026-10-05 · **Branch:** `agent/stability-physics`
**Scope:** item 1.3/1.4 residuals: `.srt` tree trunks, slope/drop parity, capsule/step
server contract. Companion: `COLLISION_SYSTEM_STATUS.md`, `JX3_COLLISION_SYSTEM.md`,
`TERRAIN_R32_BCH_RELATION.md`, `VOID_SPAWN_CRASH_TRIAGE.md`.

## 1. `.srt` tree trunks (1.4) — recon complete, pipeline confirmed

**Path RESOLVED (2026-10-05):** the sceneinfo `comRender.actorModel` field carries the
full logical path — `Data\source\maps_source\树\S_xb多枝枯树003_001.srt` (folder `树`).
The physics engine appends `.CollisionMesh` to the base name
(`PhysicsEngineX64::_GetCollisionGeometryFilesFromFile`, `FULL_MAP_COLLISION.md` §Phase 3).

Extraction verified end-to-end through the pak (`run_pakv4`, one sample):

| File | Size | Evidence |
|---|---|---|
| `…/maps_source/树/S_xb多枝枯树003_001.srt` | 398,236 B | header ASCII `SRT 07.0.0` (SpeedTree 7), md5 `0896851391…` |
| `…/S_xb多枝枯树003_001.CollisionMesh` | 28,962 B | HSEM mesh, md5 `2434c6b125…` — the exact file `FULL_MAP_COLLISION.md` calls "used verbatim (118 trees)" |
| `…/S_xb多枝枯树003_001.mesh` | 43,761 B | visual mesh (used by the measured-trunk path for the 68 degenerate trees) |

So the bake's tree handling is already correct: shipped HSEM `CollisionMesh` verbatim
where usable (118 trees), measured 250-u trunk prism from the visual mesh where the
fragment is degenerate (62 of 68; 6 `s_xca小树` walk-through as in the client). Earlier
"path unknown" probes only guessed the wrong folder name (`植被`/`foliage`); no pipeline
change is needed and the registered proxy 4e stays as-is.

## 2. Slope/drop (`ProcessDropSpeed`, 1.3) — definitive client-side boundary

The rule reads the **in-memory terrain cell** (two 3-bit slope fields + road/path flags).
The shipped physics heightmap payload is a float/half grid only
(`TERRAIN_R32_BCH_RELATION.md`: no packed cell words), and the cell object comes from the
engine scene/nav stream — not in the client install. **The client cannot source slope
projection; it is a server rule.**

Server contract (for M2+; one shared rules source, `docs/netcode/README.md` constants):
- 15 Hz integer integration; `Vz ∈ [−2048, 2047]`; gravity from the per-jump `JumpParam` row.
- Walking: terrain rises clamp up (`y = min(y, ground)`), 64 u = 1 尺 landing tolerance,
  **no client slope limit** (the host's former rise budget was an invention and was removed).
- Drop processing: `slope2 <= 1` (normal terrain) → `Vz = 0`; projection/slides only on
  authored road/slope cells (server/nav data).
- Re-open client-side work only with a server/nav map bundle or an in-memory cell capture.

## 3. Capsule / step server contract (1.4)

| Value | Client host | Server must own |
|---|---|---|
| step budget | **64 u** (game-side `ProcessVerticalMove` tolerance; PhysX ctor default 0.5 m was rejected, commit `1a20b96`) | same 64 u |
| slope limit | 0.707 (cos 45°) on mesh landing/support | same |
| capsule | default 17/116 host-chosen + `RC_BODY` table (f1/m1/f2/m2 → r=0.136·H, h=0.928·H; 花萝 17/116) supplied by the `agent/capsule-dig` branch (merges separately) | per-body values from the same table |
| contact offset | PxControllerDesc ctor default 0.1 m — **N/A** (not the gameplay solver, same disposition as stepOffset) | n/a |

No code change in this doc; it records the contract so prediction and server rules share
one source when M2 lands.

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| `.srt` path = `Data\source\maps_source\树\<name>.srt`; CollisionMesh sibling ships | HIGH | sceneinfo `actorModel` field + pak extraction (sizes/md5 above) |
| shipped BCH carries no packed cell slopes | HIGH | `TERRAIN_R32_BCH_RELATION.md` (payload = float/half grid) |
| step/slope/capsule contract values | HIGH | `COLLISION_SYSTEM_COMPARISON.md` §8.1 4f, `capsule-dig` branch |
