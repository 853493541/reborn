# Collision residuals — status, boundaries and server contract (C workstream)

**Date:** 2026-10-05 · **Branch:** `agent/stability-physics`
**Scope:** item 1.3/1.4 residuals: `.srt` tree trunks, slope/drop parity, capsule/step
server contract. Companion: `COLLISION_SYSTEM_STATUS.md`, `JX3_COLLISION_SYSTEM.md`,
`TERRAIN_R32_BCH_RELATION.md`, `VOID_SPAWN_CRASH_TRIAGE.md`.

## 1. `.srt` tree trunks (1.4) — bounded recon, no change

Current state (unchanged this session): the bake uses the shipped
`<base>.CollisionMesh` when usable; **62 of 68** SpeedTree objects in 龙门寻宝 ship a
degenerate fragment, so `export_structure_collision.trunk_prism_from_mesh` measures a
**250-u trunk prism from the visual mesh** (registered host proxy 4e).

Recon this session:
- The map's `entities/sceneinfo_full/002_002.json` references **8 `.srt` basenames**
  (`S_xb多枝枯树003_*`); the real logical path is resolved through a sceneinfo folder
  field that was not decoded before the extraction work dir was overwritten — the direct
  guesses (`maps_source/foliage/…`, `maps_source/植被/…`) all missed.
- The engine builds tree collision from `<base>.CollisionMesh` (per
  `PhysicsEngine::_GetCollisionGeometryFilesFromFile`); the shipped fragments are
  degenerate, so `_srt` itself carries no ready collision mesh in a known format.

Recovery options (evidence-first, pick one):
1. **Resolve the `.srt` path** (decode the sceneinfo folder field, extract one sample) and
   inspect the SpeedTree binary header. No local format spec → this is RE work.
2. **Engine obstacle production**: cook the tree's visual `.mesh` (or the degenerate
   `.CollisionMesh`) through the game's own `PhysicsEngineX64` in-host (the P5 probe
   already boots the physics stack) and bake the cooked obstacle — the native geometry
   instead of the prism.
3. Keep the registered prism proxy (current behaviour; trees block like the client per
   the audit's 62/68 substitution notes).

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
| `.srt` refs are basenames; direct path guesses miss | HIGH | this session's extraction probes |
| shipped BCH carries no packed cell slopes | HIGH | `TERRAIN_R32_BCH_RELATION.md` (payload = float/half grid) |
| step/slope/capsule contract values | HIGH | `COLLISION_SYSTEM_COMPARISON.md` §8.1 4f, `capsule-dig` branch |
