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
