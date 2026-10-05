# Map quality tiers — what the 5 BR maps actually ship

**Date:** 2026-10-04 · **Scope:** item 1.2 (scene/map loading) — quality-dir reality for
all five BR maps. Companion: `MINI_SANDBOX_CLIENT.md` §Quality tiers (龙门寻宝 probe).

## Method

1. Extract each map's `.jsonmap` and read its `filePaths` declarations
   (`bd/bddnc/mb/low`, plus the implicit HD root).
2. Probe the declared paths with the official extractor
   (`build_sandbox.run_pakv4`): per map × tier — `environment.json`,
   `playerEnvironment.json`, `<map>.rcidx`, `env_probe/skybox_s.dds`, plus the HD-root
   and tier `landscape/heightmap/<map>_002_002.r32` witnesses — via
   `tools/probe_map_quality.py`. 100 candidates, 40 hits. (A separate foliage witness
   probe, 20 extra candidates, missed in every tier.)

## Declared vs shipped

| Tier | Declared by | Shipped |
|---|---|---|
| **hd** (root, implicit) | all 5 | scene data: `landscape/heightmap/<map>_002_002.r32` present for all 5 (1,052,676 B); runtime file set per `MINI_SANDBOX_CLIENT.md` |
| **bd** | all 5 | **all 5 maps**: `environment.json` (28–44 KB), `playerEnvironment.json` (1.8–3 KB), `<map>.rcidx` (33 B, `{"RCEffectName":"jx3bd"}`), `env_probe/skybox_s.dds` (1,048,688 B) |
| **low** | all 5 | **all 5 maps**: `environment.json` (5.5–7 KB), `playerEnvironment.json` (2.6–2.8 KB), `<map>.rcidx` (38–43 B, `{"RCEffectName":"defaultlow"}`); no skybox |
| **bddnc** | 4 maps (not 海岛, not 夜晚) | **zero files** at every probed path (alias of `bd/` in the jsonmap) |
| **mb** | all 5 | **zero files** at every probed path (environment, playerEnvironment, rcidx, skybox, foliage, heightmap) |

Notes:

- Quality tiers carry **environment/spec data only** — the heightmap and foliage
  samples miss in every tier dir; scene geometry is HD-root only (matches the
  earlier 龙门寻宝 32-path probe).
- `bd` and `low` differ by an `RCEffectName` (`jx3bd` / `defaultlow`) plus a much
  smaller `low/environment.json` — i.e. the downshift is environment/effect-level,
  not geometry-level.
- `bddnc`/`mb` are declared but unshipped (editor/procedural tiers); a loader must
  fall back to HD root when they are absent.

## Implication for 1.8–1.10 (rendering/weather/LOD)

- There is **no lower-quality geometry to downshift to** for the BR maps; preset
  work must drive the engine's render/effect options (the `config_*.ini` tiers and
  `RCEffectName` consumers), not map file selection.
- Next probe for that workstream: find the consumer of `RCEffectName` in
  `KG3DEngineDX11EX64.dll` / `KG3DEngineX64.dll` and link it to the
  `zhcn_hd\config\config_*.ini` presets.

## Reproduce

```powershell
.venv\Scripts\python.exe tools\probe_map_quality.py    # HITS/MISSES, 40 of 100
# .rcidx decode: it is a tiny JSON file; read it directly
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| bd + low ship on all 5 maps (env/spec/rcidx/skybox) | HIGH | 40/120 probe hits, per-map rows |
| bddnc + mb ship zero files | HIGH | probe misses at all declared paths |
| quality dirs carry no heightmap/foliage | HIGH | witness probes miss in every tier |
| downshift is environment/effect-level | MED | `RCEffectName` + env size deltas; consumer not yet traced |
