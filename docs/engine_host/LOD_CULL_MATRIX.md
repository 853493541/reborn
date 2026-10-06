# LOD / culling / performance — per-option caps matrix (1.10)

**Date:** 2026-10-05 · **Branch:** `agent/lod-perf` (worktree `Desktop\reborn-iso-lod-perf`)
**Scope:** item 1.10 first matrix — one key per run, tier 9 base, two poses, numeric
fingerprints. Companion: `RENDERING_OPTIONS.md` (key↔offset reference), P2 caps in §4b.

## Method

- Build `reborn_client_lodperf.exe`; `RC_QUALITY=9` + one `RC_OPT_<KEY>=<value>` per run
  (VideoOptions merges the override into a temp preset; isolation verified by the
  render-options P2 correction note).
- Poses: house `(18991,962,33853)` and vista `(23334,761,24224)` (the dune where tier 1
  vs 9 showed large deltas).
- Metric: screenshot at t=3.5 s, `image_stats --grid 4x4`, a cell counts as changed when
  any channel differs by >2 units vs the tier-9 baseline of the same pose.
- `fps` in the table is the t=4 s heartbeat (warm-up snapshot — **not causal**; the
  reliable tier ladder is `RENDERING_OPTIONS.md` §4b/§4c at t=18 s).

## Results (cells changed / 16)

| Key | Override value | House | Vista | Verdict at these poses |
|---|---|---:|---:|---|
| `nShadowType` | 0 | 0 | **8** | **visible (shadow render off)** |
| `fSpeedTreeCullDist` | 5000 | 0 | **6** | **visible (SpeedTrees culled)** |
| `fSimpleModelCullDist` | 1000 | 0 | 1 | marginal (1 cell) |
| `fModelLodRadius` | 1000,2000,3000,4000 | 0 | 0 | no-op at these poses |
| `nMinimumModelLod` | 3 | 0 | 0 | no-op at these poses |
| `bEnableModelLodViewAngle` | 0 | 0 | 0 | no-op at these poses |
| `fFoliageCullDist` | 5000 | 0 | 0 | no-op (no foliage in view) |
| `nFoliageDensity` | 0 | 0 | 0 | no-op (load-time / no foliage) |
| `nSpeedTreeDensity` | 0 | 0 | 0 | no-op (likely load-time) |
| `fNodeLodLowLimit` | 20 | 0 | 0 | no-op at these poses |
| `fNodeLodHighLimit` | 100 | 0 | 0 | no-op at these poses |
| `fParticleSystemCullDist` | 1000 | 0 | 0 | no-op (no particles in view) |

Evidence (committed): `proof/render/lod_vista/{base_t9,nShadowType,fSpeedTreeCullDist,fSimpleModelCullDist}.png`;
the full 26-run set is in `bin64\reborn_out` run logs (uncommitted, attributable by the
`reborn_client_lodperf` fingerprint).

## Findings

1. **Shadow type and SpeedTree cull distance are the visible levers** at a vista pose;
   both change large parts of the frame. `nShadowType=0` costs nothing (336 vs 342 fps
   warm-up) and removes shadows — a real quality/perf lever.
2. **Model LOD / node-LOD / view-angle keys show no visible delta at these poses** with
   4×4 cells; either they are below the coarse grid's resolution at the tested distances,
   or they are load-time options (applied before the map is built). A finer grid (8×8) and
   a zoomed pose are needed to classify them.
3. **Foliage keys need a foliage-rich pose** (the dune has SpeedTrees, not `.foliage`
   instances); `nFoliageDensity` and `nSpeedTreeDensity` likely need a map reload to apply
   (load-time), matching the render-options clamp finding (`nFoliageDensity` clamps at 100).

## Open (next passes)

- Foliage-rich pose: pick a coordinate from the foliage bake bins, re-run the foliage keys
  + `bEnableFoliage*` toggles; confirm the density clamp and load-time behaviour.
- Finer fingerprint grid (8×8) + a close-up pose for the LOD keys.
- Causal fps ladder: tier 1/5/9 × 3 poses at t≥18 s (extend `RENDERING_OPTIONS.md` §4b).

## Reproduce

```powershell
$env:RC_CLIENT_EXE='reborn_client_lodperf.exe'; client\build_client.cmd
# cwd = C:\SeasunGame\MovieEditor
$env:RC_QUALITY='9'; $env:RC_SPAWN='23334,0,24224'; $env:RC_STARTUP='nodb'
$env:RC_SHOTS='3500'; $env:RC_AUTORUN='5000'
$env:RC_OPT_nShadowType='0'          # one key per run
& bin64\reborn_client_lodperf.exe
# compare rc_00_3500ms.png vs the tier-9 baseline with:
.venv\Scripts\python.exe tools\proof\image_stats.py <baseline.png> <run.png> --grid 4x4
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| `nShadowType` / `fSpeedTreeCullDist` visibly change the vista frame | HIGH | 8/16 and 6/16 cells, committed PNGs |
| other keys are no-ops at the tested poses | MED | 4×4 grid only; load-time vs distance-insensitive not separated |
| t=4 s fps values are non-causal | HIGH | warm-up window; render-options t=18 s numbers used instead |
