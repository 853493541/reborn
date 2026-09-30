# Client Stack Pivot — host the game client's own engine (2026-09-30)

Status: **decision input** — the MovieEditor engine-install decision is under revision
per the user's direction: *"look for answers from the game client, not MovieEditor"* /
*"whatever will reproduce the original process more"*.
Evidence: `engine_host_spike/recon_client_stack_exports.txt`,
`engine_host_spike/recon_client_movie_disasm.txt`, run logs in
`%TEMP%\opencode\skillv2\` (`ce_control.log`/app logs, `ce_treat3.out`, WER events).

## Why

The `.Sfx` tag AV blocker was attributed to the stale MovieEditor engine build
(09-14 vs client 09-27, `docs/EXPERIENCES.md` 2026-09-29). The client install is the
true resource (AGENTS §4); the client ships the same engine family at 09-27, so the
faithful host should use the **client's own stack**, not MovieEditor's.

## What the client stack is (verified)

Client `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64` (all 2026-09-27):

| Module | Exports | Role |
|---|---|---|
| `KG3DEngineAdapterX64.dll` | `SetEngineWorkingRootDirectory`, `Get3DEngineInterface`, `Get3DEngineXLogicInterface`, `GetMovieEngine`, `GetAsyncTaskSystemInterface` | same host entry points as the MovieEditor adapter (shared 4) |
| `KG3DEngineDX11EX64.dll` | 1959 exports = full `KG3D_Engine` class incl. `CreateSFXTrackData`, `CreateScreen3DSFX`, `GetAnimTagSystem`, `CreateEmptyScene`, `CreateSceneFromSource`, `CreateActorFromFile`, `FrameMove` | the engine |
| `KG_MovieEngineX64.dll` | `KG_CreateMovieEngine`, `KG_DestroyMovieEngine`, `KG_GetMovieEngine` | native movie engine (game variant) |
| `KG3D_AnimationTagX64.dll` | `KG3D_CreateAnimationTagSystem` | tani tag system (`.Sfx`/Sound/Motion/...) |
| `X3DEngine.dll` | `PreInitX3DEngine` / `LoadX3DEngine` / `UnLoadX3DEngine`, `GetK3EngineMgr`, `GetViewMgr`, ... | the game's engine facade (game exe imports it) |

Chain facts (disasm):

- The MovieEditor adapter dynamically does `LoadLibraryA("KG_MovieEngineX64.dll")` +
  `GetProcAddress("KG_CreateMovieEngine"/"KG_GetMovieEngine"/"KG_DestroyMovieEngine")`.
  That module exists **only in the client**; MovieEditor instead pairs
  `MovieEngineCLR.dll` (C++/CLI) with `KG_EngineEditorX64.dll` (editor modules that do
  not exist in the client).
- The client adapter's manager code is **byte-identical at the same RVAs** as the
  MovieEditor adapter's (`0x6c940` instance, `GetMovieEngine` `0x6caf0`,
  `SetEngineWorkingRootDirectory` `0x6cb50`, `Get3DEngineInterface` `0x6ca90`,
  manager `Init` region `0x735c0`) — the existing `engine_host_spike` recon applies.
- Client adapter deps (`RUSTLibX64`, `HASMLibX64`, `ClipMgrX64`, `KBaseX64`,
  `RemoteDebugger4RUSTX64`, `UGC_Corex64`, PhysX/embree/python35/...) are all present
  in the client bin64 (self-contained stack).

## Mixed-host experiment (failed, informative)

Goal: test the client engine inside the existing C# host without install writes.

Method: robocopy `MovieEditor\bin64` -> temp host dir; swap 30 engine/tag/plugin DLLs
to the client builds; run the existing `Skill.exe` with the new `RC_BIN64` override
(engine DLL dir) + `SB_ACTOR_TEST=1` (plays the crashing ruyifa tani
`F1smj10双刀buff04_清净心01.tani`).

| Run | Engine modules | Result |
|---|---|---|
| Control | MovieEditor 09-14 | AV `0xC0000005` (ntdll), log dies right after `actor test ... play=0` |
| Treatment | client 09-27 (engine + tag + plugins) | fast-fail `0xC0000409` in `KG3DEngineDX11EX64.dll` (offset `0x18c82c4`), same point |

Treatment proof the client engine was loaded: `CameraShim: unavailable rc=2 status=engine
build mismatch (timestamp/size)` (the shim was built for the MovieEditor engine) and the
WER faulting module timestamp matches the client build. The engine stdout also shows a
missing VFS resource (`data\source\maps\龙门寻宝\foliage/blendmap/clusterinfo.json`).

Conclusion: swapping DLLs inside the MovieEditor editor shell is **not** a faithful
reproduction (editor-mode init, different file system, missing resources) and does not
fix the tagged-tani failure. The `.Sfx` failure is **host-context**, not merely the
09-14 build. The faithful path is the client's own stack driven by the client's own
init/cast flow.

## Proposed probe (next)

`native/client_stack_probe.cpp` (MSVC, like `native/camera_shim.cpp`):

1. `LoadLibraryEx` the client adapter/movie engine/engine from `zhcn_hd\bin64` with
   `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR` (read-only use of the install; no writes).
2. `SetEngineWorkingRootDirectory(zhcn_hd)`; initialize the engine manager — either
   the RVA-identical manager `Init` (entry/signature to be recovered from the client
   adapter; ME recon in `recon_adapter_init.txt` is the reference) or the client's own
   facade `X3DEngine.dll` `PreInitX3DEngine`/`LoadX3DEngine` (exports at RVA
   `0x21580`/`0x214c0`; `PreInit` calls an internal init with one argument).
3. `Get3DEngineInterface` / `KG_CreateMovieEngine` -> create scene + actor, play the
   ruyifa tani, `FrameMove` loop, screenshot.
4. Success = the tagged tani plays; failure = log the assert/AV for the next iteration.

Open decision: build the probe against the manager `Init` (RVA path) or trace the
client `X3DEngine` init first. Whichever, the goal stays: **ability id in, engine reads
the authored data itself** — no hand-staged anim/sound/PSS playlists.

## Native boot probe (2026-09-30, running)

`client_boot_probe.cpp` (temp, `%TEMP%\opencode\skillv2\`): loads the client
`X3DEngine.dll` from a temp copy of `zhcn_hd\bin64` (robocopy; the game install is
never written), cwd = a temp client root, `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR`.

Verified results:

| Step | Result |
|---|---|
| `PreInitX3DEngine()` | **1** (ok) |
| `LoadX3DEngine()` | **1** (ok) |
| loaded modules after Load | `X3DEngine.dll` + `KG3DEngineAdapterX64.dll` (from the temp copy) |
| `GetK3EngineMgr()` | returns a facade manager pointer; does **not** load the engine |
| `KG3DEngineDX11EX64.dll` / `KG_MovieEngineX64.dll` | **not loaded** by the facade alone (polled 60 s) |
| manual `LoadLibrary KG_MovieEngineX64.dll` + `KG_CreateMovieEngine(out)` | object created (`out` non-null; the int return is garbage — the function returns in `AL`) |
| `KG_GetMovieEngine()` | returns the same object |

Conclusion: the client stack boots read-only, but the 3D engine is created **lazily**
by the movie engine / game flow — the facade init alone stops at the adapter. Next
step: drive the movie engine object (3 vtables at `[obj]`, `[obj+8]`, `[obj+0x10]`) —
map its interface from `MovieEngineCLR`'s IL/disasm (`engine_host_spike/recon_il.txt`,
`recon_managed_api.txt`) or find the game's engine-load trigger.

## Reproduce

```powershell
# recon (committed dumps)
engine_host_spike\recon_client_stack_exports.txt
engine_host_spike\recon_client_movie_disasm.txt

# mixed-host A/B (temp host dir + RC_BIN64 override; no install writes)
# control: MovieEditor modules; treatment: client modules swapped in
# run: SB_NO_SUPERVISOR=1 RC_MEM_NS=ce_probe SB_ACTOR_TEST=1 RC_AUTORUN=20000 RC_BIN64=<temp bin64> Skill.exe
# check: %TEMP%\opencode\skillv2\host_client_engine\bin64\Skill\out\Skill_*.log + WER event log

# native boot probe (temp)
build_client_probe.cmd  # cl /O2 /GS- /utf-8 client_boot_probe.cpp user32.lib
client_boot_probe.exe   # logs: PreInit/Load results, loaded modules, movie-engine object
```

Last verified: 2026-09-30
