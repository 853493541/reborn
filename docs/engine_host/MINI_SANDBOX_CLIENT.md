# Mini Sandbox — a cropped loose map for feature work (verified)

Date: 2026-09-29 · Branch: `agent/mini-sandbox` (`#iso`)
Status: **VERIFIED** — the client loads a cropped loose map of 龙门寻宝 with real
terrain, props, collision and spawn. Size-trimmed 1×1 build: **6.5 MB**
(32 % of the 2×2 build), render + physics verified.

## TL;DR

```powershell
# build the map once (read-only extraction from the real pak)
.venv\Scripts\python.exe tools\sandbox\build_sandbox.py --map 龙门寻宝 --crop 2,2,1,1 --name 龙门寻宝_s

# run any client build against it
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
C:\SeasunGame\MovieEditor\bin64\reborn_client.exe
```

`RC_MAP` accepts an **absolute OS path**; the engine resolves the map's sibling
files (`landscape/`, `entities/`, `foliage/`, `env_probe/`) through the same
directory. Everything else (map props, textures, actor clips, paks) keeps
loading from the normal client install — the sandbox is a small map folder, not
an install. HIGH — verified runs, `proof/sandbox/mini_run.log` (2×2) and
`mini_s_run.log` (1×1).

## Size profiles (all verified or measured)

| build | area | files | size | note |
|---|---|---:|---:|---|
| `--crop 2,2,2,2` (default) | ~1.0 km² | 98 | 20.1 MiB | full scene, current sandbox |
| same, cache dropped | ~1.0 km² | 77 | 14.6 MiB | `blendmap_bc/*.r8` removed |
| `--crop 2,2,1,1` | ~0.25 km² | 38 | **6.5 MiB** | 32 % of the 2×2; render + spawn verified |

The 1×1 crop is region (2,2) of the source map — the region that contains the
real spawn `(23334,761,24224)`.

### Runtime file set (verified by removal tests)

- `landscape/heightmap/<name>_i_j.r32` — **required** (renderer terrain mesh;
  dropping it renders sky where the ground should be).
- `landscape/heightmap_bc/<name>_i_j.bch` — **required** (physics terrain
  loader; dropping it makes the spawn height sample 0).
- `landscape/blendmap/<name>_i_j_<layer>.png` — kept (terrain material blend).
- `landscape/blendmap_bc/*.r8` — editor bake cache, **not read at runtime**;
  dropped by default (`--keep-bake-caches` keeps it).
- `entities/sceneinfo_full/*.json` (props), `env_probe/*` (skybox), foliage,
  regioninfo, materials, water, globals — kept.

## Crop model (world coordinates preserved)

- `RegionSize 512` × `UnitSize 100` = one 51200-unit region; 龙门寻宝 is 8×8 with
  `WorldOrigin (-102400,-102400)`.
- Keeping regions (2,2)..(3,3), renaming the region files to 0-based indices and
  setting `WorldOrigin=(0,0)` / `RegionTableSize=2x2` keeps every absolute world
  coordinate identical (spawn, object matrices, foliage, baked collision bins).
- The builder writes the crop as `<name>_mini` with all `<name>`-prefixed files
  renamed; `<name>.jsonmap`/`.SRScene`/`.rcidx`/`_Setting.ini` embed no map name
  (checked) so renaming is safe.

## Builder

`tools/sandbox/build_sandbox.py` (registered in `docs/engine_host/README.md`):

1. extracts ~130 candidate paths through the official `PakV4SfxExtract.exe`
   (globals + per-region heightmap/blendmap/regioninfo/sceneinfo/foliage),
2. writes the kept regions with renamed indices,
3. patches `entities/<name>_sceneinfo.json` + `landscape/<name>_landscapeinfo.json`
   (`WorldOrigin`, `RegionTableSize`),
4. writes `<name>_manifest.json` (per-file sha1 + misses) next to the map.

Result for the default crop: **98 files / 20.1 MiB** (1×1: 38 files / 6.5 MiB);
misses are editor-only variants (`entities/sceneinfo/*`, `procedural b2+`,
`mb/` quality) and are non-fatal in the run.

## Verified numbers

| stage | 1×1 (21:30) | 2×2 (21:04) | full client |
|---|---|---|---|
| InitPath / InitMemory / InitPak | 31 / 0 / **406 ms** | 16 / 0 / 406 ms | ~1 s |
| Init3DEngine | 24.6 s | 24.4 s | ~25 s |
| LoadMap (client) | **156 ms** | 281 ms | ~1.6 s (8×8) |
| engine `load scene` | 0.125 s | 0.203 s | — |
| TerrainSampler | `regions=1x1 origin=(0,0)` | `regions=2x2 origin=(0,0)` | `8x8 -102400` |
| spawn | `(23334,761,24224)` | same | same |
| scripted run total | ~46 s | ~68 s | ~2 min+ |

The dominant cost is engine init (~24 s) in every case; the scene load is
sub-second. Evidence: `proof/sandbox/mini_run.log` + `mini_00_15000ms.png`
(meanRGB 210,190,169), `mini_01_30000ms.png` (168,155,147) for 2×2;
`mini_s_run.log` + `mini_s_00_8000ms.png` (meanRGB 195,179,164) for 1×1;
`map_manifest.json` / `map_s_manifest.json`. The screenshots show the real
desert scene (sand, ruins, props) with the 花萝 actor at the real spawn.

## Client changes (small)

- `RC_PHYS_DLL` override for the terrain sampler physics DLL (default unchanged).
- Init/LoadMap elapsed-ms log lines (`InitPath=... ms=(a,b,c)`,
  `Init3DEngine=... ms=`, `LoadMap result=... ms=`).
- Header note: `RC_MAP` accepts an absolute path.

Feature builds stay per-worktree (`RC_CLIENT_EXE=reborn_client_mini_sandbox.exe`,
`AGENTS.md` §2).

## What it is / is not

- **Is**: fast scene for movement/camera/ability/UI work on real terrain — map
  load drops to ~0.3 s, no 8×8 scene streaming, 21 MB of scene data.
- **Is not**: a client-free install. The full install is still the asset source;
  only the *scene* shrinks. A standalone root was attempted and rejected below.

## Dead ends (do not repeat)

1. **Sandbox asset root via junctions — rejected.**
   - `InitPak` stalls ~181 s (vs 406 ms) when `workingDir` is a custom root; it
     waits on the StreamDownloader IPC. `PakDir` must be relative (`PakDir=PakV4`)
     and `bin64\KGPK4_StreamDownloaderX64.exe` must exist or `InitPak` returns
     `0x80004005`.
   - **Writable junctions leak into the install**: with `CachedShaders` and
     `bin64` junctioned, the engine wrote shader-cache files into
     `zhcn_hd\CachedShaders\*` (20:32:25) and a minidump into
     `zhcn_hd\bin64\minidump` (20:32:45) during the experiment. Rule: only
     read-only asset dirs may be junctions; all writable state must be a real
     sandbox dir. Disclosed in `docs/EXPERIENCES.md`.
2. Editor maps (`MovieEditor\source\Map\EmptyMap` / `512Simple2`) exist but are
   editor-source only; the real-map crop via absolute `RC_MAP` is the supported
   path.
3. **File-set trimming needs a screenshot, not just a spawn-height check.**
   Dropping `heightmap/*.r32` kept `spawn=(23334,761,24224)` (physics used
   `.bch`) but the render was sky where the ground should be. Dropping
   `heightmap_bc/*.bch` kept the render but sampled spawn height 0. Only
   `blendmap_bc/*.r8` proved droppable on both checks. Always verify a trim
   with the per-region RGB fingerprint of a fresh screenshot.

## Future (only if the full client must be dropped)

Standalone sandbox = trace-driven asset closure + native repack via the
engine's own `KG_PAKFS_*` write API (`WriteFile`, `MakeSubPackage`,
`CreateDirTree`, `EnableFileTrace`; exports verified). Not needed for the
dev-loop goal; deferred.

## Reproduce

```powershell
.venv\Scripts\python.exe tools\sandbox\build_sandbox.py --crop 2,2,1,1 --name 龙门寻宝_s
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
set RC_AUTORUN=20000
set RC_SHOTS=8000
C:\SeasunGame\MovieEditor\bin64\reborn_client.exe
# -> logs in C:\SeasunGame\MovieEditor\bin64\reborn_out\ ; terrain line must read
#    "TerrainSampler: size=512 regions=1x1 cell=100 origin=(0,0)"
#    and spawn must read "(23334,761,24224)"
# Run script: tools\sandbox\run_sandbox.cmd (title "Sandbox-pure")
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| `RC_MAP` absolute path + sibling resolution | HIGH | verified runs, `proof/sandbox/mini*_run.log` |
| crop math preserves world coords | HIGH | terrain/spawn/collision match full map; `FULL_MAP_COLLISION.md` region formula |
| `.r32` renderer / `.bch` physics / `.r8` unused | HIGH | removal runs + screenshots (dead end #3) |
| 1×1 = 6.5 MiB, render + spawn verified | HIGH | `mini_s_run.log`, `mini_s_00_8000ms.png` |
| root-overlay InitPak stall / leak | HIGH | measured `ms=(32,0,181453)`; install write audit |
| standalone repack viable | MED | export names only, untested |
