# Terrain region streaming — border loads + bounded region cache

**Date:** 2026-10-04 · **Branch:** `agent/terrain-stream-holes` (worktree
`Desktop\reborn-iso-terrain-stream-holes`)
**Scope:** item 1.3 (terrain heightfield) streaming half: region loads at map-region
borders. Companion docs: `COLLISION_SYSTEM_STATUS.md`, `TERRAIN_REGION_STREAMING` audit
origin `COLLISION_SYSTEM_COMPARISON.md` §8.

## 1. Engine model vs host

The engine's `PhysicsTerrain` streams regions around the player (`UpdateTerrain` +
`CreatePhysXTerrain` Config `{nPreLoadSize, nForceLoadSize, nUpdateDelta,
nMaxCacheCount}` — `REAL_CLIENT_MAP_COLLISION.md` §2). The host is a direct
`PhysicsEngineX64` *data-loader* client (`CreatePhysicsTerrainDataLoader` /
`LoadRegion` / `LoadHoleRegion`) and kept exactly **one** region buffer: any query in
another region reloaded ~1 MB synchronously inside the movement tick.

Three host consumers sample terrain across a region border every frame while it is
near:
`RebornClient.cs` player ground `SampleGround` (`:2495`, `:2570`), the camera
obstruction ray march (`:3172`), and the camera ground clamp (`:3299`). Player and
camera sitting on opposite sides of a border ping-pong the single slot.

## 2. Measurement (龙门寻宝, border x=0, regions (1,2)↔(2,2))

Harness: `RC_DEMO_COLLIDE=1 RC_CROSS_BACK=1 RC_DEMO_DIR=1,0 RC_SPAWN=-1000,0,24224
RC_AUTORUN=17000` — run east across the border 7.5 s, reverse, run back
(`RC_CROSS_BACK` is the test-only reverse at t=10.5 s). Every region load is logged
(`terrain load (ix,iz) ms=…`) and summarized at exit (`terrain stats …`).

| build | region slots | loads | msTotal | msMax | heartbeat fps |
|---|---|---|---|---|---|
| telemetry only (`ce9d1b6`, old behavior) | 1 | **16** | 21.8 | 5.0 | 175–187 (one 22 dip) |
| cache control (`c32f095`, `RC_TERR_CACHE=1`) | 1 | **85** | 26.8 | 3.7 | (spawn-settle thrash dominated) |
| cache default (`c32f095`, `RC_TERR_CACHE=4`) | 4 | **3** | 4.3 | 3.6 | 276–332 |

- The cap=1 control had a longer spawn ground-settle loop (`py=0` warmup) than the
  baseline, so its count differs — both single-slot runs show the same defect:
  adjacent-region ping-pong, e.g. baseline `(2,2)→(1,2)→(2,2)→(1,2)→(2,2)` inside
  12 ms (`reborn_20261004_223128.log` 22:32:02.52) and
  `(1,2)→(2,2)→(1,2)→(2,2)→(1,2)` inside 75 ms (22:32:12.75–12.83).
- Cache runs: **zero loads during either crossing** — the 3 loads are the loader
  prime `(2,2)`, spawn `(1,2)` and one spawn-settle probe `(0,2)`
  (`reborn_20261004_223711.log`).
- Load cost itself is small: 0.2–1.1 ms steady, 3.5–5.0 ms for the first loads. The
  defect was load **frequency** (per frame at a border), not single-load cost; no
  frame-scale hitch was observed even at cap=1 (`msMax` 3.7 ms).

## 3. Implementation

`client/TerrainSampler.cs`:

- `List<Region>` **LRU cache** (cap `RC_TERR_CACHE`, default **4**): each entry keeps
  its `(ix,iz)`, height grid, packed hole mask, `HasHoles` and last-use stamp.
  Eviction frees the oldest buffers; `Dispose` frees all.
- Load telemetry: per-load `terrain load (ix,iz) ms=… holes=… cache=…` and an exit
  `terrain stats terrLoads=… msTotal=… msMax=… cache=… zeroRetries=…` line.
- **All-zero region loads are never cached** (`zeroRetries`): the loader can return a
  zero grid before the engine has streamed the region; retry on the next call instead
  of poisoning the cache.
- **Spawn settle** (`RebornClient`): waits for the sampled height to stop changing
  (bounded 2 s) and accepts it — a genuine 0-height spot must not stall. Measured
  250 ms at the low spawn (-1000,24224) vs 10,047 ms before the fix; 281 ms /
  py=761 at the M1 spawn (23334,24224), matching the M1 proof.
- Bug found by the cap=1 control run: the fresh entry was added with `LastUse=0`, so
  the eviction pass freed the entry it was about to return (AV 0xC0000005 at the
  next sample, dump `reborn_client_terrainstream.exe.16820.dmp`). Fixed by marking
  the new entry MRU before the eviction pass. The control run now exits 0.
- No behavior change: same engine calls, same sampled values, same hole semantics
  (re-verified below). This is data residence/timing only — not a new gameplay rule.

## 4. Hole A/B status (this build)

Inventory: the shipped hole masks live at `data/source/maps/<map>/landscape/hole/
<map>_iii_jjj.hlb` — **54 files, 4256 hole cells** total (extract with the
`run_pakv4` helper in `tools/sandbox/build_sandbox.py`; inventory with
`check_hole_mask.py --scan <tree>`). Only three maps ship holes: 海岛绝境 (4x4,
origin (0,0)), 白龙绝境 and 天原绝境 (8x8, origin (-102400,-102400)); 龙门寻宝 and
龙门寻宝_夜晚 ship none — matching every 龙门 run reporting `holes=0`.

A/B on the cache build (`RC_HOLE_DUMP` engine dump vs the extracted `.hlb`, one
region per map, spawned on a hole-free region-center cell so the sampler visits the
region without falling):

| map | region | hole cells | result |
|---|---|---|---|
| 海岛绝境 | (0,0) | 234 | **PASS**, mask identical (32768 bytes) |
| 白龙绝境 | (3,4) | 96 | **PASS**, mask identical |
| 天原绝境 | (4,3) | 1400 | **PASS**, mask identical |

The decode rule (all four corner samples ≤ 0x7F + Z row flip) is therefore verified
on all three hole-bearing maps and both origins/grids. The remaining hole regions
share the same map-agnostic decode path; per-region A/B for them is optional.

## 5. Pre-existing crash found during the A/B attempt (out of scope)

海岛绝境 + spawn over the hole region at altitude (`RC_SPAWN=-25600,1000,-25600
RC_SPAWN_Y=1`) → AV after ~3 s at `KG3DEngineDX11EX64.dll+0x12282B3` (not the known
D6 `+0x11D03B6`; shim `d6=seed` loaded). Reproduced on clean main
(`reborn_client.exe`, `aad94d8`) at the identical address; solid-ground spawn on the
same map exits clean. **Not caused by the region cache** — logged in
`docs/EXPERIENCES.md` (2026-10-04) with dumps and next probe.

## Reproduce

```powershell
# build the feature client (worktree root)
$env:RC_CLIENT_EXE = "reborn_client_terrainstream.exe"; client\build_client.cmd
# baseline / control / cache (cwd = C:\SeasunGame\MovieEditor)
$env:RC_DEMO_COLLIDE='1'; $env:RC_CROSS_BACK='1'; $env:RC_SPAWN='-1000,0,24224'
$env:RC_DEMO_DIR='1,0'; $env:RC_AUTORUN='17000'
& bin64\reborn_client_terrainstream.exe            # default cache 4
$env:RC_TERR_CACHE='1'; & bin64\reborn_client_terrainstream.exe   # control
# read `terrain load` / `terrain stats` lines from bin64\reborn_out\reborn_<ts>.log
# hole inventory: extract the shipped .hlb tree (build_sandbox.run_pakv4 with
# data/source/maps/<map>/landscape/hole/<map>_iii_jjj.hlb paths) then scan it
.venv\Scripts\python.exe tools\collision\check_hole_mask.py --scan <extracted-tree>
# A/B one region: run RC_MAP=<map jsonmap> RC_SPAWN=<hole-free cell> RC_HOLE_DUMP=<dir>
.venv\Scripts\python.exe tools\collision\check_hole_mask.py --hlb <map>_iii_jjj.hlb --dump <dir>\holes_<map>_iii_jjj.bin
```

Gates: `collision_selftest_reborn_client_terrainstream.exe` 36/36 PASS;
`tools\gravity\verify_model.py` PASS.

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| single-slot border ping-pong (16 loads/17 s; cap=1 85) | HIGH | run logs `reborn_20261004_223128/223600.log` |
| cache removes crossing loads (3 total, 0 per crossing) | HIGH | `reborn_20261004_223711.log` |
| per-load cost 0.2–5.0 ms (no frame-scale hitch) | HIGH | the same logs |
| hole decode verified on all hole-bearing maps (234/96/1400 cells) | HIGH | `check_hole_mask.py` A/B PASS x3 |
| island void-fall AV is pre-existing | HIGH | canonical `aad94d8` same fault address, dumps 37100/52348 |
