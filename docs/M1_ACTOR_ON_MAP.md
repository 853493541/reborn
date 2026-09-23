# M1.2 — animated actor on a real map (PASSED)

**Date:** 2026-09-23
**Probe:** `client/ActorMapSpike.cs` -> `C:\SeasunGame\MovieEditor\bin64\actor_map_spike.exe`
**Evidence:** `C:\SeasunGame\MovieEditor\bin64\actor_map_out\*.png` + `actor_map.log`

## Result

An animated 花萝 with the real 风来吴山 tani (body animation + red blade/ring PSS SFX)
renders on the loaded 龙门寻宝 map, and its transform can be updated every frame
without restarting the animation.

## Recipe (working)

```csharp
// engine init: same chain as SpikeHost / MapSpike (editor root, not bin64)
var scene = new KGSceneCLR();
int r = scene.LoadMap(mapPath, false);        // r >= 0 = ok
scene.SetActiveEnvironment();
long win = scene.AddOutputWindow("", panel.Handle.ToInt64(), 0);  // 0 = SCENE_MAIN

// animated character on the map:
long h = scene.AddDummyModel("player", actorPath, pos, quat, scale);
var model = new KGModelCLR();
model.AttachModel(h);                          // returns 0 but works
model.PlayAnimation(taniPath, 0, 1.0f, 0);     // 0 = success-ish; animation + SFX play

// per-frame move: re-call AddDummyModel with the SAME name and new transform.
// No RemoveDummyModel needed; the same handle is returned and animation continues.
scene.AddDummyModel("player", actorPath, newPos, newQuat, scale);
```

## Findings

| Item | Result |
|---|---|
| `AddOutputWindow` flag | **0 = SCENE_MAIN** required for maps; 2 (OBJECT_PREVIEW) renders the empty default scene |
| `KGMovieActorCLR` + `scene.AppendModel` on a map | also renders (with animation + SFX), but has no per-frame transform API found |
| `AddDummyModel(name, actorPath, pos, rot, scale)` | renders the actor on the map, stands on terrain, returns a handle |
| `KGModelCLR.AttachModel(dummyHandle)` + `PlayAnimation` | **works** — dummy animates, PSS SFX play |
| Move without Remove | same handle returned; model moved; animation/SFX continued |
| `KGMovieEditorCLR.SetObjectProperty(id, matrixPtr)` | id=0 -> 0, other ids -> E_FAIL; not needed |
| `engine.SetMainPlayerType(0..3)` | no visible effect in this probe; not needed |
| `AddStateMachineModel` | map static-prop state machines (`<model>.state`); not needed for the player |

## Consequences for M1

- Player character = `AddDummyModel` + `KGModelCLR` (no `KGMovieActorCLR` needed for movement).
- Clip switching = call `PlayAnimation(otherAniPath, ...)` on the same `KGModelCLR`.
- Reposition = `AddDummyModel` same name + new transform (cheap; verify FPS at M1.1).
- The map camera default at map-relative origin framed the character; M1.1 uses the
  MapSpike follow camera instead.

## Recon artifacts

- `proof/engine_host/recon_actor_il*.txt` — IL scans (`tools/dump_il_actor.fsx`):
  `StateMachineManager::LoadAllStateMachineToScene`, `LogicalInfoPresenter::AddNpcToScene`,
  `EnumPropertyType` (full list), `ActorEditorCommandHelper::SetObjectProperty`.
- `engine_host_spike/recon_managed_api.txt` — MovieEngineCLR API dump.
