# Client code provenance — original vs game-derived (audit)

Date: 2026-09-29 · Confidence: HIGH for line counts/markers, MED for the category
percentages (file-role classification, not line-level attribution).

Scope: the main client (`client/*.cs`, `native/camera_shim.cpp`) on `main`
(`2a973a0`) plus the client deltas of the 8 active isolation branches.

## 1. The headline answer

**100% of the client source is written by us.** Nothing is copied verbatim from
decompiled IL, the game's managed assemblies, or third-party code. What is
"taken from the game client" is:

1. **Behaviour rules** re-implemented from client IL / engine evidence (camera model,
   collision formats, obstruction), cited in comments (RVA/IL/spec).
2. **The engine itself** — the client links and calls the real DLLs
   (`MovieEngineCLR.dll`, `KG3DEngine*`, `SemanticX64`, baselib) instead of
   reimplementing them; 233 engine/API call sites.
3. **Data files** extracted or generated from game assets (camera tables, per-map init
   yaw/pitch, baked collision, mesh flags).

## 2. Size and provenance markers (main)

| File | Lines | IL/RVA cites | game/spec/engine mentions | Role |
|---|---:|---:|---:|---|
| `client/RebornClient.cs` | 2686 | 32 | 208 | host loop + input + camera placement + probes (original, engine calls) |
| `client/FoliageCollision.cs` | 765 | 2 | 8 | collision-bin reader + ray math (format from IL recon) |
| `client/CameraSystem.cs` | 740 | 9 | 26 | camera model ported from the IL spec (game rule) |
| `client/EngineRay.cs` | 411 | 20 | 63 | wrapper over the real engine ray APIs |
| `client/CameraShim.cs` | 334 | 1 | 7 | P/Invoke to `camera_shim.dll` (engine vtable calls) |
| `client/CameraSmoke.cs` | 208 | 7 | 7 | camera smoke test harness (ours) |
| `client/CameraSettings.cs` | 207 | 0 | 2 | reads the player's real camera settings (game data) |
| `client/TerrainSampler.cs` | 131 | 0 | 4 | engine terrain height sampler |
| `client/VideoSettings.cs` | 81 | 0 | 7 | reads the game FOV/广角 settings |
| `native/camera_shim.cpp` | 982 | — | — | C++ shim calling engine vtables (original; `RC_PatchD6` is a disabled, broken experiment) |
| **Total** | **6545** | **71** | **332** | |

Category roll-up (file-role classification):

| Category | Lines | Share |
|---|---:|---:|
| Host/glue calling the real engine (original structure) | 3770 | 58% |
| Game-rule ports (camera/collision logic from IL) | 1505 | 23% |
| Config readers of game data | 288 | 4% |
| Native engine shim (C++) | 982 | 15% |

## 3. Data taken from the game (not code)

| Artifact | Provenance |
|---|---|
| `client/camera.json` | per-mode camera rows recovered from the game camera config chain (`Represent/camera/*`, `docs/camera/CONFIG_FILES.md`) |
| `client/scene_init_param.txt` | per-map init yaw/pitch (maps 0/1/653…), game-derived |
| `engine_host_spike/collision_data/*.bin` + `.cflags` | baked from game map assets by `tools/bake_map_collision.py` / `export_structure_collision.py` |
| `camera_mesh_flags.json` | per-mesh `bObscatleCamera` flags extracted from game content (`tools/export_camera_flags.py`) |
| `samples/`, `proof/` | extracted game assets/evidence (never redistributed; ignored where raw) |

## 4. Active branches — client deltas vs `main`

| Branch (isolation worktree) | Last commit | Client delta |
|---|---|---|
| `agent/collision-improvement` | floor query ignores winding | +1030/−142 (6 files) |
| `feature/ww-sandbox` | key-1 WW trigger + chain XY dash | +265/−25 (4 files) |
| `agent/double-jump` | merge camera-wall-clip (B14 probes + feature-build contract) | +252/−108 (4 files) |
| `agent/camera-wall-clip` | close-hide faithful-mechanism recon + test knobs | +239/−121 (8 files) |
| `agent/mini-sandbox` | 1/3-size sandbox map + `sandbox-` title | +33/−15 (2 files) |
| `agent/skillv2-sandbox` | authored PSS hint-circle from game data | +14/−67 (3 files) |
| `agent/queue-ui-compare` | HUD/death/settlement pass | +14/−67 (3 files) |
| `agent/daqinggong` | 双w entry scripts + WW scenario decode | +1/−10 (2 files) |

All are feature worktrees per `AGENTS.md` §2; none are merged. The provenance above
applies to their client changes as well (same source base + game-derived rules).

## Reproduce

```powershell
# size + provenance markers
git ls-files "client/*.cs" | ForEach-Object { "$((Get-Content $_ | Measure-Object -Line).Lines)  $_" }
Select-String -Path client\*.cs -Pattern "RVA|0x[0-9A-Fa-f]{5,}|IL\b|decompil" | Measure-Object
# engine call sites
Select-String -Path client\*.cs -Pattern "baselib\.|engine\.|editor\.|scene\.|KG3D|MovieEngineCLR|DllImport|Marshal\." | Measure-Object
# branch deltas
git branch --list "agent/*" "feature/*" | ForEach-Object { git diff --stat "main..$_" -- client/ }
```
