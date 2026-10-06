# Spawn/position AV triage — KG3DEngineDX11EX64+0x12282B3

**Date:** 2026-10-05 · **Branch:** `agent/stability-physics` (worktree `Desktop\reborn-iso-stability-physics`)
**Scope:** item 1.3 stability residual — the AV first seen during the hole A/B attempt
(`docs/movement/TERRAIN_REGION_STREAMING.md` §5, EXPERIENCES 2026-10-04).
Engine: MovieEditor `KG3DEngineDX11EX64.dll`; all crashes reproduced on **clean `main`**
(`8e0352a`, canonical `reborn_client.exe`) and on the feature build.

## 1. Original repro — root-caused and fixed: spawn outside the map extent

Repro: 海岛绝境 + `RC_SPAWN=-25600,1000,-25600 RC_SPAWN_Y=1` → AV at
`KG3DEngineDX11EX64+0x12282B3` ~2.7 s after spawn.

Cause: 海岛绝境 has origin `(0,0)` with a **4×4** region grid (extent 0..204800);
the coordinate assumed the 龙门 origin `(-102400,-102400)`, so the actor was
**outside the map extent**. Archived bounds (the sampler clamps its own region index; the
engine's own terrain/render streaming does not).

Discriminating runs (canonical, `RC_STARTUP=nodb`, 8 s):

| Spawn | x/z vs extent | Result |
|---|---|---|
| `(-25600,1000,-25600)` 海岛 | outside both | AV (5 logs, no DONE) |
| `(-25600,100,-25600)` 海岛 | outside both, low y | AV |
| `(-25600,1000,30450)` 海岛 | x outside only | AV |
| `(22850,1000,-25600)` 海岛 | z outside only | AV |
| `(300000,1000,300000)` 海岛 | outside high+ | AV |
| `(22850,1000,30450)` 海岛 | inside, over a hole | clean (DONE) |
| `(-150000,1000,-150000)` 龙门 (8×8) | outside | clean |
| `(400000,1000,400000)` 龙门 (8×8) | outside | clean |
| `(-110000,1000,-110000)` 白龙/天原 (8×8) | outside | clean |

**Fix (`client/`):** `TerrainSampler` exposes the loader's extent
(`ExtentMinX/MaxX/MinZ/MaxZ`); `RebornClient` validates `RC_SPAWN` and clamps outside
coordinates into the extent (one-cell margin) with a loud log:
`spawn clamped into map extent: (-25600,-25600) -> (100,100) [extent 0..204800 x 0..204800]`.
After the fix: the original repro exits clean (DONE); inside spawns unchanged.

## 2. New boundary (not fixed): grounded actor below sea level on 海岛绝境

After the clamp, in-extent spawns still AV at the **same engine offset** when the actor
stands/walks on terrain whose sampled height is **negative** (below sea level):

| Spawn / path | Actor state | Result |
|---|---|---|
| `(100,0,30450)` sampled y=-7120 | grounded below 0 | AV |
| `(204700,0,204700)` sampled y=-3098 | grounded below 0 | AV |
| `(100,200,30450)` RC_SPAWN_Y=1 | falls onto negative ground | AV |
| walk west from `(5000,30450)` y -1642→-2057 | grounded below 0 | AV (t≈2–4 s) |
| `(22850,1000,30450)` over a hole | **ungrounded** free fall to y=-44045 | **clean (DONE)** |
| `(100,1000,100)` corner | hovering at y=1000 (region not streamed) | clean (DONE) |

So: deep negative Y alone is not the trigger (free fall through the hole is clean);
**standing on terrain below sea level** is.

### 2.1 What it is not (probed 2026-10-05)

- **Not the water-quality/tier option:** `RC_OPT_nWaterEffectLevel=0` and `RC_QUALITY=1`
  both still AV on `(100,0,30450)` 海岛 (exit `-1073741819`).
- **Not a missing texture/resource of the water surface `_Water.mesh`:** the region scene
  JSONs (`海岛绝境/entities/sceneinfo_full/000_000.json`,
  `龙门寻宝/…/002_002.json`) carry only global render params
  (`RC.RTX.UnderWaterParam0/1`, `RC.RTX.Reflection.EnableWaterReflection`) and no water
  actorModels.

### 2.2 Disassembly of the crash site (static, on the DLL copy)

- Enclosing function (from `.pdata`): RVA `0x1226A70..0x1228419` (VA
  `0x181226A70..0x181228419`) in `KG3DEngineDX11EX64.dll`.
- Crash at `+0x12282B3`: `mov rbx, [rdi]` with `rdi = 0` — a **NULL dereference right
  after a failed lookup**:
  `call 0x18105CF50` (rbtree find; compares a qword key against `[node+0x20]`, walks
  `[node+8]`/`[node+0x10]`, returns NULL on miss) → `test rax,rax; je 0x1812282b3` —
  the not-found branch falls through with the previous `rdi` and dereferences it.
  Lookup key comes from the static `0x182D5BD10`; the map head lives at `object+0x58+0x20`
  (`PhysicsEngine`-style registry). Dumps:
  `proof/movement/disasm/crash_12282b3.txt`, `crash_ctx.txt`, `crash_fn_head.txt`,
  `crash_fn_full.txt`.
- Class match: same shape as the solved D6 NULL-registry crash (missing lazy registry
  entry), but here in the **render DLL** (DX11 engine), not the physics module.

### 2.3 Working root-cause narrative (MED→HIGH correlation)

In the real game the map's water is shipped as **compressed scene-block data**
(`KG3DSceneBlockData::UnCompressWaterData`, RVA `0x6C7440`) plus `%s_Water.mesh` surface
instances (`JX3_COLLISION_SYSTEM.md` §7 G-24; `.WaterData` is editor-authored only) and
the character water apply path (`KRLCharacterFrameData::UpdateWaterHeight`,
`UpdateIsUnderWater`, `GetAdjustHeightForWater`) — the host has **no water system at
all** (`COLLISION_SYSTEM_COMPARISON.md` row 14 = `[MISSING]`; zero `water` mentions in
`client/`). A grounded actor below the water plane activates the engine's underwater
render path, whose water registry entry the host never populates → the rbtree lookup
misses → NULL deref. This is a **host wiring gap** (water layer not loaded/instantiated),
not a data hole: the same scene data runs in the game client.

**Product impact:** 海岛绝境's below-sea-level terrain can AV the client when the actor
walks there. All samples so far are scripted/test coordinates; no default-spawn walk has
crossed the zone yet. 8×8 party maps sampled so far are grounded at positive heights
(龙门 `(0,0)/(±90000)`: y=4034..5034, clean); the r32 payload is normalized (0.5 band),
so an offline map-wide min-height scan needs the engine's de-normalization first.

### 2.4 Next probes (in order)

1. **Load the water layer** in the host scene wiring (compressed scene blocks +
   `_Water.mesh` instances) — the native fix candidate; start by finding which map files
   the game scene loader opens for water (`KG3DSceneBlockData` refs) and whether our
   `scene_init_param` path skips them.
2. Walk the 龙门/白龙 water surfaces (rivers/lakes) with a scripted path and check
   grounded-negative samples there (needs coordinates from a BCH de-normalization table).
3. Symbol-annotated minidump walk of the `0x181226A70` caller chain to name the exact
   render subsystem (RTX underwater post-FX vs water volume renderer).

## Reproduce

```powershell
# build the feature client (worktree root)
$env:RC_CLIENT_EXE = "reborn_client_stability.exe"; client\build_client.cmd
# cwd = C:\SeasunGame\MovieEditor
$env:RC_STARTUP='nodb'; $env:RC_AUTORUN='8000'
$env:RC_MAP='data\source\maps\海岛绝境\海岛绝境.jsonmap'
$env:RC_SPAWN='-25600,1000,-25600'; $env:RC_SPAWN_Y='1'
& bin64\reborn_client_stability.exe      # now clamps -> DONE (was 0xC0000005)
$env:RC_SPAWN='100,0,30450'; Remove-Item Env:RC_SPAWN_Y
& bin64\reborn_client_stability.exe      # boundary: AV on underwater ground
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| out-of-extent spawn AVs on 海岛; fixed by the clamp | HIGH | run matrix, logs `reborn_20261005_21*`, dump `reborn_client.exe.42808.dmp` |
| 8×8 maps tolerate out-of-extent test spawns | HIGH | 龙门/白龙/天原 runs clean |
| grounded-below-sea-level AV on 海岛 (same offset) | HIGH | 4 crash runs vs 2 clean ungrounded runs |
| crash = NULL rbtree-lookup deref in the render DLL | HIGH | static disasm, `proof/movement/disasm/crash_*` (function `0x181226A70..0x181228419`) |
| not the water-quality option / tier preset | HIGH | W1 `nWaterEffectLevel=0`, W2 `RC_QUALITY=1` both AV |
| host lacks any water system (scene-block water not loaded) | MED-HIGH | `COLLISION_SYSTEM_COMPARISON.md` row 14; `JX3_COLLISION_SYSTEM.md` G-24; zero water refs in `client/` |
