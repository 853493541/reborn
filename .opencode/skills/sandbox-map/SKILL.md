---
name: sandbox-map
description: Use when building, running, or shrinking the cropped mini-sandbox map (fast feature testing on a small loose map) — build_sandbox.py, run_sandbox.cmd, RC_MAP absolute paths, TerrainSampler verification, and the known engine limits (1x1 is the minimum; sub-region/half terrain is rejected).
---

# Mini sandbox map (cropped loose map)

A cropped copy of a real map, extracted read-only from the pak and loaded loose via an
absolute `RC_MAP` path. The scene shrinks; everything else (props, textures, actor clips)
keeps loading from the normal client install. See `docs/engine_host/MINI_SANDBOX_CLIENT.md`.

## Build

```powershell
.venv\Scripts\python.exe tools\sandbox\build_sandbox.py --map 龙门寻宝 --crop 2,2,1,1 --name 龙门寻宝_s
```

- `--crop 2,2,1,1` = region (2,2), 1×1 — **the minimum the engine can load**.
- Output: `C:\jx3tmp\reborn_sandbox\map\<name>\` + `<name>_manifest.json` (per-file sha1).
- Extraction uses the official `PakV4SfxExtract.exe`; the install is read-only.
- World coordinates are preserved (WorldOrigin/RegionTableSize patched), so the real
  spawn `(23334,761,24224)` and the baked collision stay valid.
- **Never junction writable dirs** into the install (a 2026-09-30 experiment leaked
  shader caches into `zhcn_hd`).

## Run

- `tools\sandbox\run_sandbox.cmd` (sets `RC_MAP`, launches `reborn_client_mini.exe`,
  title auto `sandbox-mini`), or launch the feature exe yourself:
  `set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap` then start
  `bin64\reborn_client_mini.exe` with cwd `C:\SeasunGame\MovieEditor`.

## Verify (full-chain)

- Log must show `TerrainSampler: size=512 regions=1x1 cell=100 origin=(0,0)`,
  `spawn=(23334,761,24224)`, and **no** `LoadRegion failed`.
- Visual check = numeric fingerprint via `tools\proof\image_stats.py`, not a screenshot
  attachment.
- Trim validation: screenshot fingerprint + spawn + zero load failures (a spawn-height
  check alone missed a sky-render trim bug).

## Known limits (do not repeat)

- **Sub-region / half terrain is a dead end**: cropping to 257² (RegionSize 512→256,
  bch header patched) loads as a map but the real physics loader rejects every region
  (`LoadRegion failed (0,0)`, `client/TerrainSampler.cs` → `PhysicsEngineX64 LoadRegion`).
  The shipped loader requires full 512-sample regions. Logged in `docs/EXPERIENCES.md`.
- `landscape/blendmap_bc/*.r8` is an editor bake cache — dropped by default.
- `heightmap/*.r32` (render) and `heightmap_bc/*.bch` (physics) are both required.
- Quality tiers: 龙门寻宝 ships HD only (no low/mb map to downshift to).
- Startup is engine-init dominated (~24 s); map load is already ~0.16 s.
