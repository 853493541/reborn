# Spike A — engine host recipe (PASSED 2026-09-21)

Result: MovieEditor engine DLLs hosted in our own C# process play the real
`重剑技能15_风来吴山红色hd.tani` on the 花萝 actor, including the red PSS SFX.
Evidence: `proof/engine_host_spike/spike_t{0,2000,4000,7000}ms.png` (wide, run3) and
`proof/engine_host_spike/spike_closeup_t*.png` (camera focused, run4).

## Environment

| Item | Value |
|---|---|
| Editor install | `C:\SeasunGame\MovieEditor` (build 2026-09-14) |
| Engine dir | `C:\SeasunGame\MovieEditor\bin64` |
| Working dir (client root) | `C:\SeasunGame\Game\JX3\bin\zhcn_hd` (from `MovieEditor.ini [General] RootDir`) |
| Host | C# net48, x64, compiled with `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` |
| exe location | must sit in `bin64` (native DLL + managed assembly resolution) |
| cwd at runtime | editor root `C:\SeasunGame\MovieEditor` (config/font/ResourcePack lookup) |

## Working sequence

```csharp
var baselib = new KGBaseCLR();
var engine  = new KGEngineCLR();
var editor  = new KGMovieEditorCLR();
var sound   = new KG3DSoundCLR();          // optional, not needed for playback

engine.SetRootPath(workingDir);            // client root
baselib.InitConsoleLog();
baselib.InitPath(workingDir, false);       // 0 = ok
baselib.InitMemory("MovieEditor.memory");  // 0 = ok
baselib.InitPak(false);                    // 0 = ok

int err = 1;
engine.Init3DEngine(
    startupPath,        // bin64  (Application.StartupPath)
    startupPath,        // engine dir (KG3DEngineDX11EX64.dll location)
    workingDir,         // client root
    0,                  // enableHttpFile
    "./configHttpFile.ini",
    ref err);           // 1 = ok

editor.Init(editorRoot, err, form.Handle.ToInt64());   // editor ROOT (bin64 stripped), 0 = ok

var scene = engine.NewEmptyScene();
long winId = scene.AddOutputWindow("", panel.Handle.ToInt64(), 2);

var actor = new KGMovieActorCLR();
actor.Init();
ActorEditorCommandHelper.LoadFromFile(actor, actorPath, 0);   // .actor appearance

long handle = actor.GetModelHandle();
scene.AppendModel(handle);

var model = new KGModelCLR();
model.AttachModel(handle);
model.PlayAnimation(taniPath, 0, 1.0f, 0);   // (path, playType, speed, startFrame)

// frame loop
sound.FrameMove(); engine.FrameMove(); engine.Render(); Application.DoEvents();
// screenshot
scene.SetScreenShot(pngPath, 2); scene.DoScreenShotImmediate();
```

## Critical findings

1. `editor.Init` needs the **editor root** (`EditorLayer.StartupPath` =
   `Application.StartupPath.Replace("\\bin64","")`), not `bin64`. With `bin64`,
   `KFontMgr::Init` fails on `<root>\ResourcePack\Font.ini` and
   `KMovieEditor::Init` returns 0x80004005 → later `FrameMove` crashes.
2. Without `editor.Init`, `KGMovieActorCLR.GetModelHandle()` returns 0.
3. `KGMovieEditorCLR.FrameMove` is only safe after a successful `editor.Init`.
4. `Init3DEngine` returns int (1 = success); out param is `ref int`.
5. `AddOutputWindow(string, long hwnd, int flag=2)`; use a child panel handle.
6. Non-fatal missing assets in logs (face-lift meshes, landscape, sound.pss icon)
   do not block playback.
7. Actor path used: `C:\SeasunGame\MovieEditor\source\花萝无动作.actor`.
8. Tani path (logical VFS): `data\source\player\f1\动作\f1s07cj重剑技能15_风来吴山红色hd.tani`.

## Build command

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /target:exe `
  /out:"C:\SeasunGame\MovieEditor\bin64\spike_host.exe" `
  /r:"C:\SeasunGame\MovieEditor\bin64\MovieEngineCLR.dll" `
  /r:"C:\SeasunGame\MovieEditor\bin64\MovieEditorHD.exe" `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll engine_host_spike\SpikeHost.cs
```

Source: `engine_host_spike/SpikeHost.cs`
Env switches: `SPIKE_EDITOR=0` (skip editor), `SPIKE_SOUND=1` (init Wwise),
`SPIKE_LOOP=0` (exit after one 8 s pass; default loops until window close).

## Camera controls (mirrors MovieEditor `ViewWindow` -> `KGSceneCLR.ExecAction`)

| Input | ExecAction | Meaning |
|---|---|---|
| Left drag | `ExecAction(4, 1, 0, lParam)` | ROTATE_VIEW — orbit / angle |
| Middle drag | `ExecAction(3, 1, 0, lParam)` | PAN_VIEW — pan |
| Wheel | `ExecAction(31, 1, delta>0?1:0, 1)` | MOUSE_WHEEL — zoom |
| Q / E | `GetCameraPos` + `SetCameraPos(y +/- 50)` | camera height up / down |
| F | `scene.FocusOnModel()` | focus actor |
| R | `scene.ResetCameraPosLookAtUp()` | reset camera |

`lParam = ((y & 0xFFFF) << 16) | (x & 0xFFFF)`; drag end sends the same action
with arg2=0. Enum values from `MovieEditor.EXEACTION` (ROTATE_CAMERA=1,
ZOOM_VIEW=2, PAN_VIEW=3, ROTATE_VIEW=4, MOUSE_WHEEL=31).
