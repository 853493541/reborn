# SPEC_STATES_P3 — water interaction (client truth, focused)

**Area:** character · **Branch:** agent/3x-integration · **Date:** 2026-10-07
**Scope:** the full player↔water interaction (entry, swim, float, jump, exit) decoded from the
game client, the host's current deviations, the real water-data source, and the fix plan.
Supersedes the interaction parts of `SPEC_STATES_P2.md` where they conflict.

## 1. Client interaction semantics (HIGH; evidence = E2 water-entry research)

**No swim key exists.** `ON_SWIM`/`SwimTo` (0x14031D770) are script/server move orders only
(valid from states 6/7); a player never enters water with a key.

1. **Walk/run in (grounded).** The per-frame `KCharacter::ProcessVerticalMove` 0x140318C50
   case 0 (`0x31995E`) converts the active RunTo/WalkTo destination move into **state 6**
   (moving) when `submersion >= T`, or **state 7** (in place) otherwise — from states 2/3,
   while still on the ground. `T = max(0, [+0x16C]*[+0x40]/100)` ≈ **627 u** default.
2. **Wade.** Below `T` no swim state is entered: the player keeps walking on the cell floor.
3. **Fall in.** state 4 with `Vz<0` and `submersion >= T` → **state 7** (stops, floats).
4. **Float.** PVM end (`0x31A0C2-0x31A10A`): in a water cell with the default mode byte
   (`[+0xee0]==1`) the player's root is placed at **`cellTop` = `word[cell+6]<<6`** (the water
   surface) — no sink-to-bottom for a normal player; states 6/7 add a `[+0x170]` clamp.
5. **Jump in water.** Input Space from states 1..7 with `depth >= T` → **state 5** (plain jump
   profile from the character's attributes); shallow (`depth < T`) → normal state 4 jump out;
   deep + descending → back to state 7. **State 8 is script-only.**
6. **Exit.** At the shallow edge (`depth < T`): no move → state 1 (stand), otherwise state 3
   (run) — **automatic**, no key.

**Submersion = `cellTop − max(ground, y)`** (`KCharacter::GetWaterline` 0x140312440):
`cell = [char+0x50]`, water gate = `byte[cell] & 1`, surface `word[cell+6]<<6`, floor
`word[cell+4]<<6`. Walking on the floor makes submersion = the local water depth.

## 2. Host deviations (the "completely false" interaction)

| # | Host | Violates the client rule |
|---|---|---|
| 1 | ground snap `py = ground` runs while grounded | in a water cell the client raises the root to `cellTop` (float), never the floor |
| 2 | entry gated on `!grounded` (older code) / airborne | the client enters state 6 from grounded states 2/3 |
| 3 | water source = `WaterField` (authored render rectangle, P1) | the gameplay region is the **terrain cell water flag**, not the render surface list |
| 4 | depth gate uses the render surface height | must use the cell `word[+6]`/`word[+4]` pair (or the engine's own water query) |
| 5 | jump-in = state 8, only when not moving | input jump = **state 5**, accepted from states 1..7 while deep |
| 6 | `swimState` not reset on exit in some paths | leaving water returns to state 1/3 and clears the swim mode |
| 7 | water sampled once before the move substeps | evaluate at the post-move position every frame |

## 3. The real water source (engine truth — fixes #3/#4)

The MovieEditor engine has a **static water system independent of the failing flux sim**:

- `KG3D_SceneNodeWaterManager::AddWater` @0x180546de0, `::_LoadWaterParam` @0x180548b60
  (water scene nodes loaded from the map).
- Water height query `_GetWaterHeightData` @0x180297c70: reads the water model at
  `this+0x4338` (`piModel` assert, KGLOG line 0x16b) and queries it via virtual calls
  (`[vt+0x1a0]`, then `[vt+0x40]`), returning a scaled height.
- `_FillWaterData` @0x180b98130, `SaveWaterData` @0x180298350.
- Client-side cell water is decompressed by `UnCompressWaterData` (KG3DEngineX64.dll
  0x18035cc10; `Terrain_Block_Water_Compress==0xb`).

**Fix plan:** expose the engine's water height to the host via the shim (camera_shim pattern):
obtain the scene's water manager and call `_GetWaterHeightData` (or read the water node's
height directly) — signature/object wiring needs a short disasm of the manager accessor.
Fallback if the manager is not reachable from the CLR scene: decode the terrain-cell water
block (format 0xb) and read flag/surface/floor per cell. Either way the host stops using the
authored render rectangle for gameplay.

**Note:** water VISIBILITY (rendering) remains the separate asset boundary
(`FluxWaterDefault_BWater.JsonIns` missing from the MovieEditor install) — this spec is about
interaction, which does not depend on the flux render.

## 4. Acceptance criteria (host, testable)

1. Walk from the 龙门 shore into the lake: at `depth >= 627 u` the player enters **state 6**
   (moving) / **7** (idle) and **floats at the cell surface** (root y = surface), not the floor.
2. Shallow water (`depth < 627`): the player **wades** on the floor, no swim state.
3. Falling in (airborne descent over deep water): **state 7**, floats.
4. Space in deep water: **state 5** rise, then back to float; shallow: normal jump out.
5. Walk out at the shallow edge: **automatic** return to run/stand, swim mode cleared.
6. The region is the real water (cell flag / engine water query) — no render-rect heuristic.
7. Gates: build 0, camera_smoke ALL PASS, collision 36/36; driven walk-in proof + fingerprint.
### True water region source found (2026-10-07): `water/regiondata/RegionInfo.json`

Each map ships `water\regiondata\RegionInfo.json` (e.g. 80,664 B for 龙门), the water region
tree used to place water per region - NOT the body-center circle and NOT the render list:
```
{ "ReferNode": { "<nodeKey>": { "GSInfo":..., "BlockData": [
    { "WaterUniqueCount":1, "LeafNodeIndex":N,
      "WUnique_0": { "WaterSurfaceID":S, "HasNorMask":1, "HasHoleMask":0 } }, ... ] } } }
```
龙门: one ReferNode with **256 leaf nodes** (LeafNodeIndex 0..255), all `WaterSurfaceID=0` -
a large water surface spanning the region's leaf grid (consistent with a real lake, unlike the
40-u render rect). The surface height per id comes from `watersurfacelist.json`.

**Next step:** map `LeafNodeIndex` -> world cell bounds (the water region tree mirrors the
terrain region grid: regions x leaves) and use it as the host water region, replacing the P1
`4096*Scale` provisional. This is shipped data (no engine RE), so it is the tractable path to
the true region.
### Decisive path (2026-10-07): query the engine's own water via the shim

Rather than fully decoding the RegionInfo `ReferNode`->origin, call the engine's water
query directly (the engine loads the correct water):
- `KG3DEngineDX11EX64.dll` `_GetWaterHeightData` @0x180297c70 (the only xref of the
  `GetWaterHeight` string). It reads the water model at `this+0x4338` (`piModel` assert,
  KGLOG line 0x16b) and queries it via virtual calls (`[vt+0x1a0]`, then `[vt+0x40]`),
  returning a scaled height (double).
- Implement `RC_WaterHeight(void* obj, float x, float z)` in `native/camera_shim.cpp`
  (camera_shim pattern: resolve the module base, SEH-guard the call) and pass the scene
  object (the host's `KGSceneCLR.m_pScene`, already reachable). Test at the water1 basin
  (67039,58548, expected surface ~150) and a dry point (expect none).
- If the scene pointer is not the right `this`, obtain the water manager the engine uses
  (`KG3D_SceneNodeWaterManager`, `AddWater` @0x180546de0) via the scene's members.
- This gives the exact per-position water height (surface) for the interaction; the
  terrain-cell floor comes from the existing TerrainSampler. Then apply the P3 entry/float
  semantics and drop the P1 provisional.

Alternative (data-only): finish the `ReferNode`->origin decode in
`KG3D_LoaderNoRenderX64.dll` @0x1800245b0 (block grid decoded: 16x16 blocks x 32 cells x
100 u = 3200 u; region 512 cells).

---

## Region decode SOLVED + baked (2026-10-07, `agent/3x-integration`)

The `ReferNode` key -> world origin is decoded; the alternative above is taken and the P1
provisional is replaced. **The earlier "16x16 blocks x 32 cells = 51200-u region" guess is
corrected: the water region is 16x16 leaves x 64 cells = 1024 cells = 102400 u.**

**Key -> world origin (HIGH).** `KG3D_LoaderNoRenderX64.dll` fn `0x1800245b0` reads
`%s\regiondata\RegionInfo.json` and, per terrain region, builds the key with
`"%d%03d_%d%03d"` (`0x180024854`) from `V = trunc(minWorld / (RegionSize*UnitScale))`:
`minWorldX` / `minWorldZ` are the region descriptor's `+0xc` / `+0x14`
(`_InitializeAreaRegions` `0x18002f300`: `minWorldX = N*col*UnitScale + WorldOriginX`,
`desc+8 = N = RegionSize`). The first format field is `1` when `V >= 0` else `0`, the next
three are `abs(V)`. `RegionSize = 512` (`cmp eax,0x200` at `0x180024702`, `N*100/100`), so
`V = worldOrigin / 51200`. Decoded key `1000_1000` => world origin **(0,0)**.

**Region extent (HIGH).** The leaf grid is 16x16 (`LeafNodeIndex & 0xF`, `>> 4`, 256
entries). Shipped `landscapeinfo.json`: `RegionSize 512`, `LeafNodeSize 64`, `UnitScale
100`. So a water region is **1024 cells = 102400 u** (16 leaves x 64 cells x 100 u), i.e.
2x2 terrain regions. Cross-check: the shipped 龙门寻宝 water body (67039,58548) falls inside
the decoded `1000_1000` AABB (0..102400) but *outside* a 51200 one, and the shipped UGC
`water\regiondata\Regionlist.json` = `{RegionCount:1, Region0:[0,0]}` agrees.

**Height.** The region's `WaterSurfaceID` selects a water surface; the shipped
`watersurfacelist.json` body inside the region AABB supplies the height (**150** for
龙门寻宝). Tool: `tools/character/water_region.py` (decodes the key, resolves the height,
emits `client/WaterRegions.cs`); `client/WaterField.cs` samples it (no P1 for maps with a
region); `RC_WATER` boxes remain a test-only override.

**Interaction semantics implemented** (`client/RebornClient.cs`, post-move): grounded
walk/run -> state 6 (moving) / 7 (idle) at `depth >= T` (627), float `y = max(ground,
surface)`, airborne descent -> state 7 (fall-in), Space -> state 5, automatic exit ->
`swimState = 0` at `depth < T`. Proof: `proof/character/3x_states/p3_*.txt` +
`p3_*_*.png` (fingerprints: in-water dark `#313544` vs land brown `#775E3C`).

**Registered remaining (AGENTS §6):** the RegionInfo `HasNorMask`/`HasHoleMask` masks
(which narrow the shoreline inside a block) are not decoded, so the host floods the region
AABB wherever `ground < surface`. Re-open: decode the `%s.data` / `*.mwdata` per-leaf mask
and apply it in `WaterField.Sample`. Water **rendering** stays the separate asset boundary
(`FluxWaterDefault_BWater.JsonIns` absent in MovieEditor). `WaterRegions.cs` ships region
data only for 龙门寻宝 / 龙门寻宝_夜晚 (the maps with a decoded RegionInfo copy).
