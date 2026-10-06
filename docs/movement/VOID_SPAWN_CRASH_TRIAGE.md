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
**standing on terrain below sea level** is. Hypothesis (MED): the editor install's
water/underwater render path differs from the game client (same boundary class as
TrueSky/volumetric cloud). Not yet root-caused.

**Product impact:** 海岛绝境's below-sea-level terrain can AV the client when the actor
walks there. All samples so far are scripted/test coordinates; no default-spawn walk has
crossed the zone yet.

**Next probes:** (a) engine option toggles for water/underwater effects in the editor
host; (b) same test on other water maps; (c) disassemble `+0x12282B3`; (d) capture the
engine minidump stack with a symbol-annotated walk to identify the offending subsystem.

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
| water/underwater subsystem is the cause | MED | correlation + same boundary class; not disassembled |
