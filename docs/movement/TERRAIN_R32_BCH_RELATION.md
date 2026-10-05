# R32 ↔ BCH relation — renderer vs physics heightmaps

**Date:** 2026-10-04 · **Scope:** item 1.3 residual — the audit listed "R32 detail
heights [MISSING] (not needed); BCH is authoritative; R32 relation unresolved".
This note closes the relation with the extracted pairs in
`proof/collision/terrain_extra/` (龙门寻宝_002_002, 龙门寻宝_000_000,
海岛绝境_000_000).

## Formats

| File | Path | Layout |
|---|---|---|
| `.r32` | `landscape/heightmap/<map>_i_j.r32` | 513×513 **float32, no header** (1,052,676 B); values in a narrow normalized band (龙门_002_002: 0.500397–0.510966) |
| `.bch` | `landscape/heightmap_bc/<map>_i_j.bch` | **36-byte header + samples² float32**; per-region normalized [0,1] |

BCH header (龙门寻宝_002_002): magic `01 00 00 bc`; `samples=513` u32 @4;
`cells=512` u32 @8; `hdrSize=36` u32 @20; flags @24; two floats @28/@32
(8983.16 / 325.25 for 002_002; 21561.1 / 3274.0 for 000_000).
Grid size can differ per file: `_002_002.bch` is 513 samples/512 cells,
`_000_000.bch` is **129 samples/128 cells** (a coarse variant; provenance is the
same `heightmap_bc/` dir — which regions ship it and why is open).

## Relation (HIGH)

The two files are the **same heightfield**:

- **Row flip in Z**: BCH row = `511 − worldZ`; R32 row = `worldZ` (the same flip
  direction as the hole mask).
- **Exact affine**: `r32 = r32_min + bch_flip * (r32_max − r32_min)`
  — i.e. `bch = (r32 − min) / (max − min)` under the flip. For 龙门_002_002:
  slope `0.010569`, intercept `0.500397` (= r32 min), max residual **2e-8**
  (float precision), correlation 1.0.

Implications:

- Renderer and physics read one field in two normalizations; there is no
  render-vs-physics mismatch to worry about.
- A client-side decoder can read `.bch` directly (36-byte header + float grid)
  without the engine loader, if ever needed.
- The absolute-height mapping is applied by the engine (not in these files).

## Open

- **Header float semantics.** Candidate `h = f32 + b * (f28 − f32)` gives
  651.5 cm at the MINI sandbox spawn (23334,24224) vs the logged sampler value
  761 cm — not confirmed (off by ~110 cm). Next probe: disasm
  `_LoadHegihtRegionBCH` (PhysicsEngineX64) or A/B `LoadRegion` output at known
  cells to recover the exact de-normalization.
- The coarse BCH variants: `_000_000.bch` is 129 samples float32 (format @16=1),
  `_001_002.bch` is 129 samples **16-bit** (format @16=0, payload 129²×2). The
  16-bit variant decodes to ~6090 cm at (-1000,24224) under the header min/max
  hypothesis, while the engine loader reports a **stable 0** there (30 s, fresh
  loads — see `TERRAIN_REGION_STREAMING.md` §3). Either the coarse variant's
  normalization differs or the loader does not use it for physics. Open; next
  probe = disasm `_LoadHegihtRegionBCH` (format field @16 / flags @24).

## Reproduce

```powershell
.venv\Scripts\python.exe tools\movement\r32_bch_relation.py `
    proof\collision\terrain_extra\龙门寻宝_002_002.r32 `
    proof\collision\terrain_extra\龙门寻宝_002_002.bch
# -> affine r32 = 0.010569 * bch_flip + 0.500397, max residual 0.000000019
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| R32 = 513² float32, no header | HIGH | file size + decode (`r32_bch_relation.py`) |
| BCH = 36-byte header + samples² float32, per-region [0,1] | HIGH | header decode + stats |
| BCH is row-flipped and exactly affine to R32 | HIGH | correlation 1.0, residual 2e-8 |
| header floats are (max, min) in cm | LOW | candidate only; prediction misses the logged sample by 110 cm |
