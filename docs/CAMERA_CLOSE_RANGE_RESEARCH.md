# Close-range camera + obstruction return - engine research (2026-09-24)

Question: (a) how fast should the camera return after leaving a wall, and
(b) what makes the character disappear when the camera gets close?
Evidence: `KG3DEngineX64.dll` (game build), disasm in `proof/netcode/disasm/`
(`engine_trackcam_update.txt`, `engine_loader_flex.txt`,
`engine_trackcam_ctor.txt`, `xref_perspective*.txt`, `engine_proj_*.txt`).

## 1. The engine's camera update, in order

`KG3DTrackCamera` frame update (around `0x1804C0DD6`):

1. **Flex (position follower)** `0x1804C0E84..0x1804C10F7` - runs only when
   `+0x1C4 != 0` (flex initialised) and `+0x208 != 0` (`bUseFlexibilitySys`):
   ```
   E   = current(+0x170) - reference(+0x17C)     ; current minus target
   S  += ( -fFlex*E - fDamp*S ) * dt             ; S at +0x1B8/1BC/1C0
   X   = current + S * dt
   if angle( current->X , current->reference ) <= 0.05 rad (const @0x180674314):
        current = X                               ; accept, +0x1DC |= 1
   else:
        current = reference ; +0x1C4 = 0          ; snap + disable flex
   ```
   `fFlex`/`fDamp` are copied from the parsed `[Camera]` settings into the camera
   by the loader (`0x1804C18B9..0x1804C18C5`): `[cam+0x1FC] = fFlexCoefficient`,
   `[cam+0x200] = fDampCoefficient`. Settings defaults: **fFlex 1.5**,
   **fDamp 2.828** (constructor fallbacks differ per camera class: 1.5/1.5 and
   0.8/0.7; the ini/loader values win).
2. **Obstruction** `0x1804C10FD..0x1804C1312` - only when `+0x204 != 0`
   (`bObstructdAvert`): calls the probe helper `0x1804C1B40` with the candidate
   position. If the helper returns it unchanged (no hit) the state is cleared
   (`+0x1CC = 0`, `+0x1DC &= ~4`).
3. On a hit the helper returns `C' = A + u*max(0.001, minHit)`; the caller
   applies the **18 u** clearance `0x1806ED3F0`:
   `final = C' + normalize(A - C') * 18`.
   The new position is applied when either
   - `|A - C'| < |A - current|` (the candidate shortens the anchor ray), or
   - `|C' - ref|^2 < T^2` with `T = 50` (`0x180678E14`) while free and
     `T = 100` (`0x18067117C`) while `+0x1CC != 0` (already obstructed).
   On apply: `+0x170 = final`, flex state `S = 0` (`+0x1B8/1BC/1C0`),
   `+0x1C4 = 1`, `+0x1CC = 1`, `+0x1DC |= 0xC`.
4. Engine camera setters (vtable `+0x28`, `+0x88`, `+0x18`, `+0x20`) with the
   final position, the anchor/look-at at `+0x1AC/0x1B0/0x1B4`.

So the *return* after a wall is: flex state was reset to 0 on the pull; once
the helper reports no hit the state clears and the flex moves the position
toward the reference (the desired chase position) with the same fFlex/fDamp
spring - **no separate zoom-out rate exists**, and `fDisZoomRate` (`+0x38`),
`fFlexRate` (`+0x3C`) are read by the loader but **not used** in this update
path. `fChaseRate` is used by the key-rotation path only.

## 2. No character-hide/fade in the camera path

The track camera update never touches model visibility. Searches run:

- `FadeStartDis/FadeReferDis/FadeReferCof/FadeTime` in `KG3DEngineX64.dll` -
  belong to `caption.ini` nameplate fading (`NewHPBar/FontConfig` loader
  `0x1802D5DE0`), not the character.
- `SetSmallCullNear`, `KG3DModel::GetCameraNearRayIntersect` - dev command /
  distance query; no visibility switch.
- `KRLCharacter::SetPlayerControlVisibleState` (`0x1804E4150`),
  `SetRepresentHideFlag` (`0x180502E80`), `KRLMovie::UpdateCharacterVisible`
  (`0x180597E80`) in `JX3RepresentX64.dll` - per-represent/control-state hides
  driven by gameplay, not by camera distance.

The projection path (`D3DXMatrixPerspectiveFovLH` call `0x180379CD0`) takes
**zn from the view object** (vtable `+0xA8` slot 3) and zf as an argument -
i.e. the near plane is a view setting, not a camera-obstruction rule. The
official "character disappears when the camera is close" is therefore a
**near-plane/clipping effect of the game's view**, not an explicit hide call.
The local install does not ship the value (view setup comes from the engine at
runtime); `MovieEngineCLR.dll` exposes a native `KMovieActionCameraNearPlane`
(`fNearPlane`) for movie actions but there is **no managed API** to set or read
the near plane from the host.

## 3. What this means for the host

| Aspect | Engine | Host now | Action |
|---|---|---|---|
| Return rate | fFlex 1.5 / fDamp 2.828 position spring; snap when the move direction leaves the 0.05 rad guard; flex disabled after a guard trip until the next obstacle | same constants on the distance scalar; no guard/snap | **add the guard form** (`v += (-k*E - c*v)*dt`, `X = current + v*dt`, snap on direction failure); behaviour equals the engine for collinear returns |
| Obstruction apply | candidate shorter OR within 50/100 u of the reference | pull to hit-18 while a hit exists; release 100 u past desired | close for the shortening case; document the candidate-based threshold |
| Character near camera | view near-plane clipping (value not shipped) | park the dummy model below the map (<45 u, restore >70 u) | raise the restore threshold so the model reappears only when the camera is clear of the body; exact game value is not obtainable from the local data |

Open item for the native-interface recon: read/set the view near plane
(vtable slot at `view+0xA8` slot 3) so the host can reproduce the official
clipping instead of the park-below hack.

## 4. Host engine vs game engine (measured 2026-09-24)

The running client does **not** load the game engine that the rules above come
from. Loaded modules of `reborn_client.exe`:

```
MovieEngineCLR.dll, KG_EngineEditorX64.dll, KG3DEngineDX11EX64.dll,
kg3d_objectX64.dll, kg3d_DataCenterX64.dll, PhysicsEngineX64.dll,
PhysX3_x64.dll, X3DEngine.dll, ...
```

i.e. the **editor/DX11 engine** (`KG3DEngineDX11EX64.dll`), not the game's
`KG3DEngineX64.dll`. The obstruction/flex/near-plane addresses in sections 1-2
are the game engine; the editor engine is a different build and the managed
host exposes only `SetCameraPos` / `ExecAction` / `SetViewAngleFactor`. The
official camera rules cannot be invoked through the managed wrapper.

## 5. Why walls are still see-through: measured coverage

`structure_collision.bin` (the camera ray source) was parsed directly:

- FCOL v2: 695 meshes, 4957 instances.
- Bake coverage of the map's world objects: **4963 / 4965** actors kept
  (`.mesh` 4777, `.srt` 186) - the bake is not the problem.
- Around the spawn point the nearest baked instance is **1425 u away**
  (`23334,24224`), while the screenshots show buildings right there. Those
  buildings are **terrain / subscene geometry**, not `worldObjects`, so they
  have no camera ray data. The game engine's camera probes include terrain
  (`RayIntersectionTerrain` backend of mask `0x301`); the host only has a
  heightfield `TerrainSampler`, which cannot see vertical terrain surfaces or
  terrain-baked buildings.

Path to exact behaviour:

1. Bake the map **terrain mesh** (landscape render geometry) into the camera
   collision set - pragmatic, matches the game's terrain ray; or
2. Recon the host engine / PhysX scene (`KG3DEngineDX11EX64.dll` +
   `PhysicsEngineX64.dll`) for a native scene ray, which would hit everything
   the renderer draws (the same idea as the game's mask `0x301`).

## 6. The host engine contains the same ray family (found 2026-09-24)

`KG3DEngineDX11EX64.dll` (the engine the client actually loads) has the game's
ray backends under the same names:

- `KG3D_Scene::RayIntersection` @ `0x180975EE0`
- `KG3D_Scene::RayIntersectionTerrain` @ `0x180976260`
- `KG3D_Landscape::_RayIntersectImp`, `KG3D_SpaceManager::RayIntersection`,
  `KG3D_SpaceNode::RayIntersection` ...

Exports to reach the objects (so no vtable guessing is needed):

```
KG3D_GetEngine()                                      -> KG3D_Engine*
?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ(engine) -> window
?Get3DScene2@KG3D_Window@@UEAAPEAVKG3D_Scene@@XZ(window)       -> scene
```

`RayIntersectionTerrain` contract from the disasm
(`proof/netcode/disasm/host_ray_terrain_fn.txt`):

```
rcx = scene
rdx = pPos   (float[3], ray origin)
r8  = pDir   (float[3], ray direction)
xmm3 = fMaxDist (float)
stack arg 5 = pRetMinDistanceRet (float*)
stack arg 6 = pbRetIntersect (int*)
```

It forwards to the terrain object's vtable (`+0xB8` / `+0xD8`) and returns the
nearest terrain hit distance + bool. The scene-vtable entry for
`RayIntersection` sits at `.rdata 0x1821C5708`; `RayIntersection` itself takes
the same shape plus one more argument slot (stack args at `+0x20/+0x28`
relative to the caller frame) - that extra slot (filter/mask) is the remaining
contract detail to pin down before wiring the shim.

So the exact game terrain ray is callable from the host: a small C# P/Invoke
shim (`KG3D_GetEngine` -> window -> scene) plus an RVA delegate for
`RayIntersectionTerrain` gives the camera probes the same terrain backend the
game uses, and the existing `structure_collision.bin` raycast covers the
entity/props backend.
