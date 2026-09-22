# Spike B — map display host recipe (PASSED 2026-09-21)

Result: MovieEditor engine DLLs hosted in our own C# process load and render the
real `龙门寻宝` map (scene), with terrain, sun sky and map props. Separate app
from the actor spike: `engine_host_spike/MapSpike.cs` -> `map_spike_host.exe`
(does not touch `spike_host.exe` / the actor workstream).

Evidence: `proof/map_spike/map_tour_00.png` (overview view) and
`proof/map_spike/map_tour_07.png` (world-coordinate close view at the map's
system camera position), plus `proof/map_spike/map.log`.

## How MovieEditor displays a map (reverse-engineered)

Map catalog: `C:\SeasunGame\MovieEditor\ResourcePack\MapList.tab` — GBK TSV
`ID \t Name \t ResourcePath \t UnderEditorTool`. 龙门寻宝 = ID 296 ->
`data\source\maps\龙门寻宝\龙门寻宝.jsonmap` (VFS path inside client PakV4).

Verified call chain (IL dump: `tools/dump_map_recon.fsx` -> `engine_host_spike/recon_map.txt`):

```
MainForm::LoadMap(id, path)                      // UI entry (menu 场景)
  -> new SceneForm(), LoadScene(id, path)
SceneForm::LoadScene(id, path)
  -> OnBeforeLoadMap:  m_Scene = new KGSceneCLR()      // bare instance, NO AttachScene
  -> int r = m_Scene.LoadMap(path, EditorConfig.EnableMapAysncLoad)   // r < 0 = fail
  -> OnAfterLoadMap:
       if EnableSceneFullLoading: m_Scene.SetSceneFullLoading(true)
       EnableBufferReaderCache(config)
       m_Scene.SetActiveEnvironment()
       KMovieCore.set_MainScene(m_Scene)          // static registration
       environment = m_Scene.GetEnvironment()
       m_viewControlForm.AddView(...)             // -> KGSceneCLR.AddOutputWindow AFTER LoadMap
```

Key findings:

1. **Order matters**: `AddOutputWindow` must come AFTER `LoadMap`. The native
   scene is created inside LoadMap; calling AddOutputWindow first returns -1
   and the render loop then asserts (`RegisterSceneViewListner`, `GetCameraPosition`)
   and screenshots crash with AccessViolation.
2. `LoadMap` returns int (< 0 = fail). Sync load (async=false) of 龙门寻宝 took
   ~1.6 s after engine init; `GetLoadingProgress()` reached 1.0 at ~2 s.
3. `SetCameraPos(x, y, z, false)` snaps y to the terrain height (engine camera
   collision). The 4th param is not a simple "relative" flag.
4. The map's world content sits around world coordinates ~(147463, 5231, 49912)
   (systemCamera0 position in the map's `systemCamera.json`); the map-relative
   rect from `GetSceneRect` is x=-1024..3072, y=-1024..3072.
5. Non-fatal load noise: missing face meshes, `sound.pss` icon actor,
   `LightTagConfig.json`, shadow-mask/ao maps, `sceneShadow` asserts — map still
   loads and renders (same as the editor's own log pattern).

## Recipe (working sequence)

```csharp
// same init chain as Spike A (see SPIKE_NOTES.md)
baselib: InitConsoleLog / InitPath(workingDir,false) / InitMemory / InitPak(false)
engine.Init3DEngine(startupPath, startupPath, workingDir, 0, "./configHttpFile.ini", ref err)
editor.Init(editorRoot, err, form.Handle.ToInt64())          // editor ROOT, not bin64

var scene = new KGSceneCLR();                                // bare, no AttachScene
int r = scene.LoadMap("data\\source\\maps\\龙门寻宝\\龙门寻宝.jsonmap", false);
scene.SetActiveEnvironment();
long win = scene.AddOutputWindow("", panel.Handle.ToInt64(), 2);   // AFTER LoadMap

// frame loop
sound.FrameMove(); engine.FrameMove(); engine.Render(); Application.DoEvents();
// screenshots
scene.SetScreenShot(png, 2); scene.DoScreenShotImmediate();
```

## Env switches (map_spike_host.exe)

| Switch | Meaning |
|---|---|
| `MAP_PATH=...` | override map path (default 龙门寻宝) |
| `MAP_ASYNC=1` | LoadMap async=true |
| `MAP_FULLLOAD=1` | SetSceneFullLoading(true) after load |
| `MAP_EDITOR=0` | skip editor.Init |
| `MAP_SOUND=1` | init Wwise |
| `MAP_AUTORUN=N` | exit after N ms (0 = until window closed) |
| `MAP_RESET=1` / `MAP_SKY=1` | ResetCameraPosLookAtUp / FromSky after load |
| `MAP_CAMPOS=x,y,z` | SetCameraPos after load |
| `MAP_TOUR=x,y,z;...` | visit camera positions, screenshot each (map_tour_NN.png) |

Build:

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /target:exe `
  /out:"C:\SeasunGame\MovieEditor\bin64\map_spike_host.exe" `
  /r:"C:\SeasunGame\MovieEditor\bin64\MovieEngineCLR.dll" `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll engine_host_spike\MapSpike.cs
```

Run with cwd = `C:\SeasunGame\MovieEditor` (config/font/ResourcePack lookup),
exe sits in `bin64` (native DLL + managed assembly resolution).

## Map file bundle (龙门寻宝, extracted with PakV4SfxExtract)

`proof/map_spike/龙门寻宝_extracted/...`:

- `龙门寻宝.jsonmap` (2357 B) — quality-level dir layout; `worldObjectCount: 5075`,
  `actorCount: 5066`, `MapVersion: 1.1`
- `龙门寻宝_Setting.ini` (4058 B) — `[CameraSet]` hex doubles, `[LogicalScene]`
  2048x2048, `[Terrain]`, `[Water]`, fog, lights (camera hex = shared editor default)
- `龙门寻宝.rcidx` (38 B), `龙门寻宝.SRScene` (524 B `SRS` header)
- `systemCamera.json` (5550 B) — 4 `systemCameraN.json` template actors
  (cameras 1-3 at default (1,5000,1); camera0 at (147463.5, 5231.0, 49911.7))
- `environment.json` (10333 B) — warm daylight sun, day/night cycle on

Extraction recipe: `pss_assets.run_pakv4` (official `PakV4SfxExtract.exe`,
pathlist GBK-encoded) — see `_probe_map_files.py`.

## Camera controls (editor system, adopted — see RECON_CAMERA.md)

The host now replicates MovieEditor's input mapping instead of a homegrown one:

- **right-drag** = PAN_VIEW(3) | **Alt+right-drag** = ROTATE_CAMERA(1) orbit |
  **Shift+right-drag** = ROTATE_VIEW(4) (no-op in host, editor edit-state only)
- MOUSE_MOVE(30) sent before every drag action (input reference)
- **wheel** = MOUSE_WHEEL(31, 1, dir, 1)
- **W/S/A/D** = SetCamareMoveState 1/2/64/128, **Shift** = 4096 fast,
  **Q/E** = camera up/down 256/2048, **numpad +/-** = SPEED_UP/DOWN 25/26
- **F** = ZOOM_TO_OBJECT(1001), **Alt+F** = LOCATE_TO_OBJECT(1004),
  **R** = ResetCameraPosLookAtUp
- left-drag = selection refs (19/20/21/22), like the editor

Verified with `MAP_MOVETEST=1` (camera pos before/after each action, in
`map.log`): forward/fast/up/down/left all move the camera; PAN_VIEW works on the
map scene (it was a no-op on the actor empty scene); ROTATE_CAMERA orbits;
wheel zooms. ROTATE_VIEW(4) does nothing in the host.

## Rendered maps (proof in `proof/map_spike/`)

| Map | MapList.tab ID | world camera (systemCamera0) | look |
|---|---|---|---|
| 龙门寻宝 | 296 | (147463, 5231, 49912) | warm day |
| 龙门寻宝_夜晚 | 297 | (160187, 13972, 106129) | night (blue/dark) |
| 海岛绝境 | 410 | (136312, 1154, 102968) | bright sea |
| 白龙绝境 | 512 | (78921, 8265, 135919) | bright |
| 天原绝境 | 532 | (68135, 29128, 112142) | blue-tinted |

Each map dir has tour PNGs + the extracted descriptor bundle
(`<name>_id<ID>_extracted/`). Batch render: `engine_host_spike/render_map.ps1
-Name <map> -Tour "x,y,z;..."` (runs the host and copies PNGs to proof).

## Open questions / next steps

- Exact meaning of `SetCameraPos` 4th bool (terrain snap semantics).
- `LoadMap` async path untested (async=true + `GetLoadingProgress` pump).
- Actor-on-map (other workstream) will need the map's spawn point: likely
  `data\source\maps\龙门寻宝\...` player spawn data in the pak.
- Map list picker: `maplist.py` parses MapList.tab (621 entries; note IDs 296,
  676, 677 all carry the name 龙门寻宝 — verify which is the HD playable one).
