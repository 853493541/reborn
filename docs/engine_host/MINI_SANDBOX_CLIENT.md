# Mini Sandbox Client — a small-map development environment (plan)

Date: 2026-09-29 · Branch: `agent/mini-sandbox` (`#iso`) · Status: **PLAN** (research complete, no code yet)

## Problem

Feature development currently boots the full game install as the asset host:

| Root | Size | Role in the loop |
|---|---:|---|
| `C:\SeasunGame\Game\JX3\PakV4` | **192.34 GB** | asset store (all maps) read through `Trunk.dir` |
| `...\JX3\bin\zhcn_hd` (client root) | **21.62 GB** | VFS root / workingDir (`bin64` 1.51, `interface` 6.30, `ui` 2.64, `logs` 9.07) |
| `C:\SeasunGame\MovieEditor` | **2.46 GB** | engine host (DLLs, shaders, configs, editor source) |

Measured 2026-09-29 (see Reproduce). A feature (camera, controls, ability, UI,
netcode) needs one small scene, one actor and the code under test — not 214 GB
of maps. Engine init is ~24 s and a test run ~2 min.

Goal: a **mini sandbox client** = real engine + a small sandbox asset root + a
small scene (a slice of the real map). Same engine, same code, same feature
exes (per-worktree builds, `AGENTS.md` §2) — only the asset source shrinks.

## Findings (evidence)

### 1. How the client finds assets — HIGH

- `client/RebornClient.cs:68-72` hardcodes `editorRoot = C:\SeasunGame\MovieEditor`,
  `workingDir = C:\SeasunGame\Game\JX3\bin\zhcn_hd`, and passes `workingDir` to
  `baselib.InitPath(...)` + `InitPak(false)` + `engine.Init3DEngine(...)`.
  The map default is `data\source\maps\龙门寻宝\龙门寻宝.jsonmap` (VFS path).
- `zhcn_hd\clientconfig.ini` (text, GBK):
  `[PakV4] PakDir=../../PakV4`, `DirFileName=Trunk.Dir`, `UseFileName=0`,
  `[FileSystem] EnableFileTrace=0`.
- `C:\SeasunGame\Game\JX3\PakV4`: `Trunk.dir` (290 MB index, magic `DIRFILE`,
  version bytes `33`), `Package.cfg`, `versionmap.cfg`, and ~400 hash-named
  storage volumes (`<n>\000.dat` + `000.idx` + `buckets.idx` + `storage.cfg`).
  Largest single volume: `1014` = 63.75 GB. 192.34 GB total.
- => the asset root is already parameterized by `workingDir` + a
  `clientconfig.ini` next to it. Nothing in the client code hardcodes the pak
  path except that config file. **A sandbox root only needs its own
  `clientconfig.ini` + a mini `PakV4`.**

### 2. The engine can write its own pak stores — HIGH (symbols), MED (semantics)

`dumpbin /exports C:\SeasunGame\MovieEditor\bin64\KGPK4_FileSystemX64.dll`
(reproduce below) includes:

- `KG_PAKFS_EnableFileTrace` — official file-open trace switch.
- `KG_PAKFS_WriteFile`, `KG_PAKFS_WriteFileByKey`
- `KG_PAKFS_CreateDirTree(W)`, `KG_PAKFS_DirTree_FlushDirTree`,
  `KG_PAKFS_DirTree_UpdateEntry`, `KG_PAKFS_CreateDataConfigFile`
- `KG_PAKFS_MakeSubPackage(Ex)`, `KG_PAKFS_OptimizePackage(Ex/W)`,
  `KG_PAKFS_MakePatch`, `KG_PAKFS_RebuildStorage`, `KG_PAKFS_SortStorage`,
  `KG_PAKFS_ResourceMgr_*`
- `KG_PAKFS_IsUseFileName` (the `UseFileName` mode switch)
- `KG_PAKFS_OpenFileSystem(Ex/W)`, `KG_PAKFS_IsFileExist`,
  `KG_PAKFS_CollectAllFileNames`, `KG_PAKFS_DirTree_GetEntry*`

`KIndexpackX64.dll` exports `g_GetIndexPackagePacker` /
`g_GetIndexPackageReader` / `g_MakeIndexPackageByMemory`.

Semantic names suggest a full read/write store API; exact call contracts are
undocumented (IL/symbol research needed). This is the *native-first* route to a
mini store — no invented formats.

### 3. The scene format is crop-friendly — HIGH (files), MED (crop math)

From the extracted real map (`proof/map_spike/龙门寻宝_extracted/...`,
`proof/map_spike/sceneinfo.json`, `proof/map_spike/龙门寻宝_landscapeinfo.json`)
and the editor map `MovieEditor\source\Map\512Simple2\...`:

- `<map>.jsonmap` = per-quality-dir layout (`hd/bd/mb/low`) + object counts.
- `<map>_sceneinfo.json` / `<map>_landscapeinfo.json`:
  `RegionSize=512`, `LeafNodeSize=64`, `UnitSize/UnitScale=100`
  (region = 512 × 100 = 51200 world units), `WorldOrigin.x/y`, `RegionTableSize.x/y`.
- Real 龙门寻宝: `WorldOrigin=(-102400,-102400)`, `RegionTableSize=8x8`
  (8×51200 = 409600 u ≈ 4 km), spawn `(23334,24224)` → region `(2,2)`.
- Per-region files are named `<map>_%03u_%03u.*`:
  `landscape/heightmap/*.r32`, `landscape/blendmap/*`, `landscape/regioninfo/*.json`,
  `entities/sceneinfo_full/%03u_%03u.json` (world objects),
  `foliage/foliageinfo/%03u_%03u.foliage` (obstacles).
- Crop recipe (to verify in S3): keep regions (2,2)..(3,3), rename region files
  to 000..001, set `WorldOrigin=(0,0)` and `RegionTableSize=2x2`. Because region
  world position = `WorldOrigin + index*51200` and object placements are
  absolute, this **preserves world coordinates** — spawn, collision bins and
  baked data stay valid.

### 4. Small editor maps already exist as loose files — HIGH (files)

`C:\SeasunGame\MovieEditor\source\Map\` ships `EmptyMap` and `512Simple2`
(1 region each, `RegionTableSize=1x1`, JSON + heightmap/blendmap files, a few MB).
The editor loads maps from this loose `source\Map` tree; whether **our client
host** can `LoadMap` one of these source paths is untested (S0 spike).

## Architecture

```
C:\SeasunGame\MovieEditor                 engine host, shared, read-only (2.46 GB)
<repo or ignored dir>\sandbox\            built artifact, gitignored
  asset_root\                             becomes workingDir
    clientconfig.ini                      PakDir=PakV4
    PakV4\                                mini store (Trunk.dir + volumes) or loose tree
    userdata\                             optional; never committed (privacy)
  map\                                    mini scene (editor map copy or real-map crop)
  manifest.json                           provenance: logical paths, source hashes, engine build
tools\sandbox\build_sandbox.py            plan -> extract -> repack -> verify
client: RC_ASSET_ROOT / RC_PHYS_DLL / RC_MAP   small env overrides
```

Size targets: Tier A (editor map + actor + clips) ≤ ~1 GB; Tier B (real-map
2×2 crop + assets) ≤ ~4 GB; both replace the 214 GB install for the run.

## Builder pipeline (recommended: trace-driven closure + native repack)

1. **Trace**: run a reference client against the sandbox root with
   `[FileSystem] EnableFileTrace=1` in the sandbox's own `clientconfig.ini`
   (never the install). Collect every logical path opened while the scripted
   feature scenario runs. (`KG_PAKFS_EnableFileTrace` exists as a symbol —
   output format/location to confirm in S2.)
2. **Extract**: pull each logical path with the official
   `PakV4SfxExtract.exe` (`pss_assets.run_pakv4` precedent,
   `tools/bake_map_collision.py` uses the same route). Read-only w.r.t. installs.
3. **Repack**: prefer `KG_PAKFS_MakeSubPackage`/`OptimizePackage`; fallback
   `CreateDirTree` + `WriteFile` loop. Output: mini `Trunk.dir` + volumes.
   Alternative if `UseFileName`/loose mode is real (`KG_PAKFS_IsUseFileName`):
   emit a loose tree and skip repacking entirely (S2 experiment).
4. **Fixed point**: re-run the sandbox with trace; missing files recovered by
   `CollectAllFileNames` or golden-run diffs; repeat until the run needs no
   files outside the mini store.
5. **Verify**: numeric fingerprint (per-region RGB), engine log (`LoadMap`
   result, terrain origin/regions, missing-file lines), collision walk checks.

## Mini scene tiers

| Tier | Scene | Effort | Use |
|---|---|---|---|
| A | existing editor map (`512Simple2` or `EmptyMap`) copied into the sandbox | hours (S0) | UI, ability, animation, camera, input |
| B | real 龙门寻宝 crop (2×2 regions around spawn), objects + foliage + rebaked collision | days (S3) | movement, collision, mode flow |
| C | purpose-built 1×1 test range (spawn pad, obstacle suite, water/edge cases) authored on a real-map copy | later | regression suite |

Tier B keeps world coordinates, so the existing baked
`龙门寻宝_*_collision.bin` remains valid as a fallback; re-baking only trims
55 MB → a few MB.

## Client changes (small, same branch)

- `RC_ASSET_ROOT` → override `workingDir` (defaults unchanged).
- `RC_PHYS_DLL` → TerrainSampler DLL path (default: MovieEditor's own
  `bin64\PhysicsEngineX64.dll`, which exists — no client bin64 needed).
- userdata/camera: if no sandbox userdata is provided, fall back to defaults
  (copying `custom.dat` is optional and must stay out of git).
- Log line: `asset_root=... map=... paks=...` for run attribution.
- Build stays `RC_CLIENT_EXE=reborn_client_mini_sandbox.exe` (`AGENTS.md` §2).

## Phases and gates

| Phase | Work | Gate |
|---|---|---|
| S0 | load a loose editor map (`source\Map\512Simple2\...`) via `RC_MAP` in a feature exe | screenshot + numeric fingerprint; `LoadMap` ≥ 0; terrain sampler prints origin/regions |
| S1 | sandbox asset root with `clientconfig.ini` PakDir → real PakV4 (junction copy), prove `RC_ASSET_ROOT` | 龙门寻宝 renders from sandbox root; fingerprint matches baseline run |
| S2 | builder v1 (trace → extract → MakeSubPackage/loose) | mini store ≤ 4 GB; scripted run has zero non-store file loads; manifest reproducible |
| S3 | `tools/sandbox/crop_map.py` + 2×2 real-map crop + collision rebake | terrain heights sampled at N points match the full map; walk/jump checks pass |
| S4 | productize: build/run scripts, docs, EXPERIENCES | one-command `build_sandbox.cmd` + smoke run documented |

S0/S1 are cheap and de-risk everything (root switch + loose-map resolution).
Do not start S2 repacking before S1 proves the engine accepts a foreign root.

## Risks / open questions

- `MakeSubPackage` / `OptimizePackage` semantics unknown (explicit file list?).
- Trace output format/location unknown; package-version checks
  (`KG_PAKFS_CheckVersionMatch`, `Package.cfg`, `versionmap.cfg`) may reject a
  hand-built store — test early, do not bypass integrity checks if the engine
  refuses (label any workaround *provisional*).
- Loose-file mode (`UseFileName` / `KG_PAKFS_IsUseFileName`) is speculative.
- Crop: `WorldOrigin` re-anchoring must be validated for terrain, sceneinfo
  objects and foliage (foliage region origin formula in
  `docs/movement/FULL_MAP_COLLISION.md` §Phase 1 supports the math; verify).
- Some map content may live outside the 8×8 region table (skybox/env probe are
  global — copy those whole).
- Concurrent runs share `MovieEditor` caches (`AGENTS.md` §2.6) — sandbox runs
  add no new shared state as long as they keep using feature exe names.

## Decision points

1. Ship S0+S1 now (recommended) before committing to the repack route?
2. Sandbox location: repo-ignored `sandbox\` (single disk) vs `C:\jx3tmp\...`
   (keeps the repo drive light). Recommend repo-ignored `sandbox\` + `.gitignore`.
3. Tier A first, or go straight for the Tier B real-map crop?

## Reproduce

```powershell
# install weights (2026-09-29)
$root='C:\SeasunGame\Game\JX3\PakV4'; (Get-ChildItem -Directory $root | ForEach-Object {
  (Get-ChildItem -Recurse -File $_.FullName | Measure-Object Length -Sum).Sum } |
  Measure-Object -Sum).Sum / 1GB
# zhcn_hd: 21.62 GB; MovieEditor: 2.46 GB (same per-subdir method)

# engine pak API exports
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207\bin\Hostx64\x64\dumpbin.exe" `
  /nologo /exports C:\SeasunGame\MovieEditor\bin64\KGPK4_FileSystemX64.dll

# map metadata
Get-Content -Encoding UTF8 C:\SeasunGame\MovieEditor\source\Map\512Simple2\512Simple2_sceneinfo.json
Get-Content proof\map_spike\sceneinfo.json
```

## Source confidence

| Claim | Conf. | Source |
|---|---|---|
| workingDir/PakDir wiring | HIGH | `client/RebornClient.cs:68-72`, `zhcn_hd\clientconfig.ini` |
| Pak store size/layout | HIGH | local scan 2026-09-29 (`Trunk.dir` 290 MB, 192.34 GB) |
| pak write/trace exports | HIGH | `dumpbin /exports` (command above) |
| mini-store repack viable | MED | export names only; S2 contract research pending |
| map region model + crop math | MED | `proof/map_spike/sceneinfo.json`, `龙门寻宝_landscapeinfo.json`, `docs/movement/FULL_MAP_COLLISION.md` |
| editor maps loadable via `RC_MAP` | LOW | files exist; S0 spike pending |
| `PhysicsEngineX64.dll` in MovieEditor bin64 | HIGH | local scan |
