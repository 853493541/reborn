# SFX Wiring Plan — retire the free-standing dummy effect (2026-09-30)

Status: **in progress — probe works, interfaces mapped.** This is the registered deviation
from `docs/EXPERIENCES.md` (caster-follow emulated by re-adding a dummy; effects are
free-standing scene dummies, not engine SFX).

## Wiring attempt (2026-09-30, `RC_SFX_ENGINE=1` + `RC_Shim_SfxPlay`)

**The engine now creates the effect itself.** `KG3D_CreateSFXFromFile` (engine RVA
`0xBE4000`, internal) is called from `sfx_shim.dll` with the correct owner and returns a
live `KG3D_SFX` instance:

```
engine sfx play rc=0 owner=0x… ownervt=0x… vt0=0x3B1E5C0 vt1=0x3B1CBF0 …
  path=…\t_天策撼如雷02_重制.pss -> obj=0x… exc=0x00000000
```

Signature + context recovered from the engine's own callers:

```
KG3D_SFX* KG3D_CreateSFXFromFile(void* owner /*rcx*/, const char* path /*rdx*/,
    void* r8, void* r9, void* a5, void* a6 /*world matrix*/, int a7, void* a8 /*out*/)
```

- **owner** = `[engine+0x2CF7038] -> vt[8]()` (the iface the engine's caller stores as
  `out[0]`; the earlier `vt[10]()`+helper `0x8ABAB0` path is a *different* context field
  and produced a garbage vtable — that was the AV cause).
- **a6** = a world matrix (translation = caster position, passed from the host).
- SFX pool verified initialized (`[engine+0x2CF7BB0]` non-null; created by
  `KG3D_Engine::Init`).

**Still missing: driving/attaching the instance.** The engine's tag update (code
@`0xE3412A`, inside an animation/tag function) does this after creation:

1. `KG3D_CreateSFXFromFile(...)` → SFX object.
2. `__RTDynamicCast(sfx, 0, IKG3D_Model, IKG3D_NormalModel, 0)` → the play interface.
3. `vt[0xD58](model, 1, 1, 0)` → play; `vt[0xD60](model, ...)` → attach.

The shim replicates 2+3 (VCRUNTIME140 `__RTDynamicCast`, descriptors at engine RVAs
`0x260E0A0`/`0x260EB40`). **Result**: the cast returns the same pointer and the created
**PSS** object's `vt[0xD58]` is the `return 0` stub — i.e. the play path applies to
**`.Sfx`-derived** effects (`IKG3D_NormalModel`), not to bare-PSS instances. The `.Sfx`
path is exactly the one that AVs on this (09-14) engine build — so finishing the
engine-driven effect likely requires the client-stack host (`CLIENT_STACK_PIVOT.md`) or
the `.Sfx` parser difference. Evidence: `Skill_20260930_1902*.log`
(`model=0x… play=0xE5420 rc=0`), shim status line.

Earlier wrong lead (documented so it is not repeated): `0x76E300` looked like a spawn
(create + follow-up call) but its asserts are `pcszModelPath` / `piModel` /
`_BakeSubsetTexturesToFileWithIndex` — it is a **model/bake loader**, not the SFX spawn.
The real SFX-module caller is `0xE3412A` (animation/tag update).

## Client-engine probe (2026-09-30, `client_sfx_probe.cpp`, temp)

Booted the client facade read-only (`PreInitX3DEngine -> 1`, `LoadX3DEngine -> 1`), then
loaded the client `KG3DEngineDX11EX64.dll` explicitly and called its
`CreateSFXFromFile` (client RVA `0xBE5610`). Results:

- `KG3D_GetEngine2()` returns **null** after the facade init — the engine instance is not
  created by `LoadX3DEngine` alone (it loads lazily when the movie engine / game flow
  drives it). `GetActiveWindow2`/`Get3DScene2` therefore also return null.
- The client adapter's `Get3DEngineInterface` returns a non-null pointer, but it is not
  the create's owner type (AV at `0xBE568E` when used).
- The client owner chain (`singleton @RVA 0x2CF1038 -> vt[8]()`, found by the same
  access pattern as the ME build) returns **null** — the singleton is not initialized by
  the facade boot either.
- With a null owner the create AVs at `fault_rva=0xBE568E` for every input (including
  `.pss`) — the test is inconclusive until a real engine instance is available.

Conclusion: testing (and using) the client engine's `.Sfx` path requires the **native
client-stack host** with the full client init (the engine instance + singleton are
created by the game's boot flow, not by `X3DEngine` PreInit/Load alone) — the
`CLIENT_STACK_PIVOT.md` milestone. The probe source + build script live in
`%TEMP%\opencode\skillv2\` (`client_sfx_probe.cpp`, `build_client_sfx_probe.cmd`).

## Client engine boot chain recovered (2026-09-30, same probe)

The full instance-creation chain is now mapped (all RVAs = client `bin64` build):

1. `X3DEngine.dll!LoadX3DEngine` (0x214C0) → facade manager (`GetK3EngineMgr`,
   facade global RVA `0xFA418`); it calls the facade manager's `vt[1]()` and `vt[82]()`
   but does **not** create the engine instance.
2. Adapter `KG3DEngineAdapterX64.dll!Get3DEngineInterface(&p)` → `p` = the 3D engine
   interface object (0x2180 bytes, built by adapter ctor `0x72AF0`, vtable
   `adapter+0x29FFF0`; object pointer = the interface itself).
3. **`p->vt[0]()` = the adapter manager Init (`0x730C0`)** — builds the
   `KG3D_ENGINE_INIT_PARAM` (stack struct, param base = caller rsp+0x60) and calls
   **`KG3D_CreateEngine(param, out)` (engine export `0x8D46C0`)** → `new(0x3A38)` +
   `KG3D_Engine::ctor` (`0x8B8900`) + `KG3D_Engine::Init` (`0x8B9810`). Init stores the
   engine singleton at engine global RVA `0x2CF1038` (writer `0x8BBDEB`; `KG3D_GetEngine2`
   `0x8D4890` reads it).
4. The engine Init boots the whole stack (log: WIC, memory, object pool, string table,
   debug server, file system, streaming file manager, middleware, D3D11 device on the
   RTX 5080, …) — **but file loading fails** (see next section).

**Working root**: the adapter stores it as a `std::string` at `iface+0x2020`
(`SetEngineWorkingRootDirectory` export `0x6CB50`; length at `iface+0x2030`). The manager
Init (`0x730C0`, code at `0x73155`) copies it into a stack buffer and then into
`param+0xEEC`; `KG3D_Engine::Init` uses `param+0xEEC` as the file-manager root
(`FS_GetOrCreate` at engine `0xB0F720` → `KG3D_CreateFileManager(root)` at `0xB105E0`
stores the root at `manager+0xC`).

**Blocker found (host integration gap, not an engine bug)**: `KG3D_Engine::Init` also
reads **`param+8` = `[iface+0x2018]`** — the **host-provided original file-system
object** (`0xB524A0` = `KG3D_CreateHttpFileSystem(pOrignalFS)`; the engine's FS manager
global is engine RVA `0x2D22598`). In our probe `iface+0x2018` is **0** (the adapter has
no writer for it; the game/editor host sets it), so:
- the engine's file manager is created with an **empty root** (verified at runtime:
  `[engine+0x2D22598]+0xC` = `''`),
- every lookup fails (`data\public\sound.ini`, `data\material\Shader_DX11_HD\Base\FullScreenUtils.hlsl`),
- shader creation fails → the engine's shader-failure path calls `DebugBreak`
  (engine `0xBB8E3A`, log `CreateNewShader:%s (%s)` + `KGLOG_COM_ASSERT_EXIT(0x80004005)`)
  → `iface->vt[0]()` returns `E_FAIL`, no engine instance.

Also note: `iface+0x2018` is read as an object with vtable calls at `0x73655`
(`vt[0xB8]`) and `0x7376A` (`vt[0x68]`), and the physics manager (`PhysicsEngineX64.dll`
export `GetPhysicsManager`, resolved via GetProcAddress by the adapter at `0x7384A`) is
created/looked up there; `physMgr->vt[2](&buf)` returned an empty string in our boot.

**Reusable instrumentation** (all in the probe, no game-install writes):
- `KG_PrintfLog` (KGCommonX64 export) hooked by patching the engine+adapter IAT slots
  (engine IAT RVA `0x1C14D08`) — the engine's full log now goes to probe stdout.
- `CreateFileW/A` IAT hooks show the engine does **not** open shader files via
  `CreateFile*` (VFS goes through the file-system object).
- Inline hook on engine `0xB0F720` (lazy FS create) — installed, but the FS manager in
  Init is created via the Init path, not the lazy getter.

**Data**: the engine needs the client data tree; our `client_root` copy now includes
`data\material` (shaders incl. `shader_dx11_hd\base\fullscreenutils.hlsl`) and
`data\public` copied from
`zhcn_hd\SeasunDownloaderV2.4\seasun\client\_HttpFileForDebug_\local\data` (the updater's
local cache, 5.4 GB, mirrors the client tree), `CachedShaders` (114 MB) + `zsCache` from
the real client, and `clientconfig.ini` `[PakV4] PakDir` pointed at
`C:/SeasunGame/Game/JX3/Pakv4` (read-only pak store). None of this fixes the lookups while
the FS object is missing.

**Next step for the client host**: replicate the game's boot — find how
`JX3ClientX64.exe` provides the original-FS object (`iface+0x2018`) / calls the manager
Init (args r8d flag, stack arg5; the adapter init reads stack arg at `[rbp+0x3520]` =
entry `[rsp+0x28]`), then call `iface->vt[0]()` with the same context. Candidate
references: the facade's `GetFilePath` object (tried: wrong type — null-deref inside Init),
the game exe's import call sites for `GetK3EngineMgr`/`LoadX3DEngine`.

## Update — FS object identified; engine now loads loose files (2026-09-30 late)

Follow-up on the same probe (CreateEngine hook now fixed to observe `param+0xeec`/`param+8`):

- **The working root IS delivered correctly**: `KG3D_CreateEngine` receives
  `root=client_root` (param+0xEEC) — the earlier "empty root" read was from the wrong
  object type (the FS-manager global holds the HTTP wrapper, not the base manager).
- **The original-FS object is the adapter's own FS class**, created during the manager
  Init: adapter function **`0x7AB90(this, path, flags, providedFS)`** — if `providedFS`
  (r9) is null it allocates 0x220 bytes, sets vtable **`adapter+0x29CB10`**, stores it at
  **`iface+0x2018`**; else it stores the provided object. The manager Init calls it at
  `0x732CC` with `r9=0` (adapter-internal FS) and the working-root path. This is the
  object the engine uses as `param+8` (`KG3D_CreateHttpFileSystem(pOrignalFS)`).
- **File lookups now work**: after fixing a nested-copy bug (the data trees had landed as
  `client_root\data\material\material\...`; fixed with robocopy), the engine's first
  shader load (`FullScreenUtils.hlsl`) succeeded and the failure moved to the **next**
  shader (`VG_DumpDecodedClusterCS.hlsl`) — i.e. the loose-file path is functional and
  the remaining gap is **files that only exist in the PakV4 store**.
- **The game's pak-backed FS**: `KGPK4_FileSystemX64.dll!KGPK4_CreateFileSystemWrapper`
  (+ `KG_PAKFS_*` API) is the client's pak file system; the game creates/configures it in
  `Engine_Lua5X64.dll` (function at RVA `0xCC2D0`: `LoadLibrary` → GetProcAddress
  `KGPK4_CreateFileSystemWrapper` → create → `wrapper->vt[3](callback)` → use; wrapper
  global at `Engine_Lua5X64+0x1730B8`). The adapter references the module names
  `CEngine_Lua5X64.dll` / `KIndexpackX64.dll` in its string table (dynamic loading).
- **Host options for pak-served files** (next session, in order): (a) find the adapter's
  pak-enable hook (`m_pGetPakV4EnableFunc` / `g_IsPakV4Enable` debug-symbol strings exist
  in the adapter) and the injection path for a provided FS object; (b) drive the game's
  wrapper (`KGPK4_CreateFileSystemWrapper` + `KG_PAKFS_OpenFileSystem` with the
  `clientconfig.ini [PakV4] PakDir` store) and pass it via `0x7AB90`'s `r9` path or the
  init stack arg5; (c) extract the needed files from the PakV4 store into `client_root`
  (iterative, works for the shader set but not a general solution).

**Instrumentation note**: inline-hooking `KG3D_CreateEngine` (`0x8D46C0`) works only if
the copied prologue covers **whole instructions incl. RIP-relative ones** (its first
RIP-relative `mov r8d,[rip+…]` needs ≥26 bytes) — and copied RIP-relative instructions
must not be relocated (a 17-byte copy crashed with a wild `av_addr`; the hook was removed
after the observation). `KG3D_CreateEngine` cannot be called twice (the engine global is
already set) — call it once per process.

## Proper FS fix — game file layer (2026-10-01, no more file copying)

The adapter's FS class is **not** a loose-file FS: its init method (adapter `0x5DF10`,
`rcx`=FS object, `rdx`=root path) loads **`Engine_Lua5X64.dll`** and resolves the game
file API into its callback slots —
`g_OpenFile`→+0x128, `g_OpenPakV5StreamFile`→+0x130, `g_IsFileExist`→+0x148,
`g_IsPakFileExist`→+0x150, `g_IsFileDataExist`→+0x178, `g_GetFileState`→+0x180,
`g_IfUsePackFileV3`→+0x168, `g_IsPakV4Enable`→+0x170, `g_PrepareFiles`,
`g_GetStreamDownloadLoad`, `g_GetRootPath`, `g_SetRootPath`.
The game's `KJX3PackageModule::Initialize` (JX3ClientX64.exe `0xB3C80`) first sets the
root (`Engine_Lua5X64!g_SetRootPath`, export `0xB5400`, root global `Lua+0x170060`) and
mounts the paks (`Engine_Lua5X64!KG_InitPakV4FileSystem`, export `0xCC2D0`, which loads
`KGPK4_FileSystemX64.dll` → `KGPK4_CreateFileSystemWrapper` → `wrapper->vt[1]` =
OpenFileSystem; the exe passes `[pakModule+0x93c]`=PakDir, `[+0xa40]`=DirFileName,
`[+0xb84]`=priority, plus ints/bytes from `clientconfig.ini [PakV4]`).

**Probe now does this properly** (before `iface->vt[0]()`):
```
g_SetRootPath("…\client_root")
KG_InitPakV4FileSystem("C:/SeasunGame/Game/JX3/Pakv4", "Trunk.Dir", "", 0, 0,0,0,0, (void*)"")
```
(9th arg is a dereferenced string — NULL AVs inside `KG_InitPakV4FileSystem` at `0xCC501`.)
Result: `KG_InitPakV4FileSystem -> 1`, and the engine's file lookups now resolve from the
**real PakV4 store** — the whole shader set loads (`shaderMap load finished … Shader map
created succeeded`, `Gpu Particle Shader created succeeded`, `Shader table created
succeeded`) with **no missing-file loop**.

Engine init now reaches: device → shaders → `KG3D_Engine dlss2 init` → `KObjectManager is
created` → pre-render pipeline (`ch=3` GBK string) → **abort `0xC0000409` in MSVCR110.dll
offset 0x740C4** (fast-fail; `KG3D_MaterialSystemX64.dll` + `KG3D_ShaderReflectionX64.dll`
just loaded, `KG3D_ImguiX64.dll` is the loaded MSVCR110 consumer). Vectored exception
logging shows no C++ EH — a direct CRT `_invoke_watson` (invalid parameter) path; next
probe: hook `_invoke_watson` in MSVCR110 (note: MSVCR110 loads only with the engine, so
install the hook after the engine DLL is loaded).

### Follow-up — Python, font, and window (2026-10-01)

The abort was traced (by hooking MSVCR110 `abort`, which IS the 0x740C4 fast-fail path)
to **python35.dll offset 0x1B699A**: the engine's embedded Python aborted because its
library was missing. Fix: copy `data\rcdata\PythonLib` (real client, 14.8 MB / 854 files)
into the working root.

Next missing piece: `KG3D_LoadFontFile` → the engine's default font
`ui\Font\FZHeiTi_GBK.ttf` (not shipped under that exact name; the client ships
`ui\Font\fzht_GBK.TTF`). Copy `ui\Font` into the root and provide `FZHeiTi_GBK.ttf`
(copy of `fzht_GBK.TTF`; probe-level data shim).

**Then the engine's core init SUCCEEDS**: log line
`-- KG3D_Engine initialize success. const time = 3.156s --`. The only remaining failure is
the **target window / swap chain**:
```
KG3D_Window::_CreateSwapChain -> 0x80070057 (E_INVALIDARG)
KG3D_Window::Init -> KG3D_CreateWindow -> KG3D_Engine::CreateTargetWindow -> E_FAIL
```
`?CreateTargetWindow@KG3D_Engine@@UEAAJPEAUHWND__@@PEAPEAUIKG3D_Window@@@Z` (export
`0x8AEF30`) takes an **HWND** + out-window; the engine's Init calls it with the HWND from
the init param, and our host provides none (the game creates its own window and passes it;
the adapter reads `config.ini` for size/mode — see `config.ini` refs at adapter
`0x5FA56`/`0x67BE6`). Setting the root `config.ini` to windowed 1280x720 did not change
the E_INVALIDARG — the next step is to create a real Win32 window in the host and supply
its HWND through the manager init path (the adapter manager's window field / the init
param), plus a message pump.

### Game boot path mapped (2026-10-01)

The game's real init path is now known (from `JX3ClientX64.exe`):
`KJX3RenderModule::Load` (`0xB7F3A`) → `LoadX3DEngine` + `GetK3EngineMgr`;
`KJX3RenderModule::Initialize` (`0xB78C0`) →
`facadeMgr->vt[4](objA, objB, objC)` (`0xB78F9`; facade `0x1F0F0` → `iface->vt[3]`),
`facadeMgr->vt[5](objD)` (`0xB7940`; facade `0x1FE00` → `iface->vt[4]`),
`facadeMgr->vt[2](0, 4)` (`0xB795D`; facade `0x1F7C0` =
`NSX3DEngine::KWindowsX3DEngine::Init` → **`iface->vt[0](rdx=0, r8d=4)`** — the engine
creation; r8d=4 did not change the window failure).
The three host objects passed to vt[4]/vt[5] are the exe's module singletons
(`[0xA8C1C0+0x18]`, `[0xA8C210+0x18]`, `[0xA8C260+0x18]`, `[0xA8C1E8+0x18]`) — these
carry the host window/config context the adapter needs; replicating them (or at least the
window) is the remaining host work.
Adapter manager vtable: slot 0 = Init1 `0x730C0`, slot 1 = Init2 `0x75650`, slot 2 =
UnInit `0x75A40`, slots 3/4 = `0x1BE40`/`0x1BBA0` (the host-object setters).

## CLIENT ENGINE INSTANCE LIVE + `.Sfx` CREATE PROVEN (2026-10-01)

**Window substitution solved the last init blocker.** The adapter passes a bad HWND to
`KG3D_Engine::CreateTargetWindow` (client engine export `0x8AEF30`); the probe now creates
a real Win32 window (`CreateWindowExA`, 1280x720, class `client_sfx_probe_wnd`) and
substitutes it with a 15-byte inline hook on `0x8AEF30` (prologue `push rbx…push r15` +
`sub rsp,0x40` = 15 bytes, no RIP-relative instructions → safe to relocate). Result:

```
[CreateTargetWindow] self=… hwnd=… -> using 0x1B0774
[KGLOG] window init, size is :1264, 681
[KGLOG] -- [Adapter] version=1.1.9.9 init success. --
iface->vt[0](0, 4) -> 0                      ← S_OK
KG3D_GetEngine2 (after iface init) -> 0x…    ← LIVE CLIENT ENGINE INSTANCE
```

**Decisive `.Sfx` test on the live client engine** (`c纯阳坐忘.Sfx`, the file that AVs the
ME 09-14 build), owner = client singleton `[engine+0x2CF1038] -> vt[8]()`:

```
existing .Sfx (client owner): obj=0x1E5C64A5A60 exc=0x00000000 fault_rva=0x0
pss (client owner):           obj=0x1E5D9809058 exc=0x00000000 fault_rva=0x0
```

**No AV — the client 09-27 engine creates the real `.Sfx` cleanly; the ME 09-14 build
AVs on the same file.** The core bug is a build difference, and the client engine is the
fix. (Probe note: the create's return is a full pointer — declaring it `long` truncates
it; use `void*`.)

**Play-path mapping on the client build** (from the engine's own bind code at
`0xE348CA`, client engine):
1. `KG3D_CreateSFXFromFile` (`0xBE5610`).
2. `__RTDynamicCast(sfx, 0, IKG3D_Model @0x26080A0, IKG3D_NormalModel @0x2608B40, 0)`
   — descriptors found as `_TypeDescriptor` structs (`.?AUIKG3D_Model@@` /
   `.?AUIKG3D_NormalModel@@`; ME equivalents shifted by `-0x6000`).
3. `model->vt[0xD58](model,1,1,0)` — a hook; result ignored by the engine.
4. `model->vt[0xD60](model, ctx, …)` — attach.
5. `model->vt[0x180](model, worldMatrix, …)` — the real bind/play entry.
6. `model->vt[0x190](model)` — handle.

Slot reality on the probe's created objects: `.Sfx` (`c纯阳坐忘`) has a **real**
`vt[0x180]` (`0xBCC9F0`) while `vt[0xD58]/[0xD60]/[0x190]` are the shared stub `0xE5420`
(`xor eax,eax; ret`); bare `.pss` is stubbed at all four. Calling `vt[0x180]` with an
identity matrix and no prior bind returns `0x80004002` (E_NOINTERFACE) — the attach
context/scene binding (the engine passes its SFX context at `[ctx+8]`) is the next
missing piece for engine-driven playback.

Probe: `%TEMP%\opencode\skillv2\client_sfx_probe.cpp` (evidence `win2.out`, `play3.out`).

## Core bug isolated (2026-09-30, direct create-call tests)

`RC_Shim_SfxPlay` now accepts **both** engine builds (ME 09-14 and client 09-27,
timestamp/size guarded, per-build `CreateSFXFromFile` RVA) and was used to isolate the
original `.Sfx` failure directly:

| Input | Result on the ME (09-14) build |
|---|---|
| missing `.Sfx` (`M明教元素18.Sfx`) | **graceful E_FAIL** — `KG3D_LoadFile failed …` → `KG3D_SFXData::LoadFromFile` → `CreateSFXFromFile` returns null, no fault |
| existing `.Sfx` (`c纯阳坐忘.Sfx`, extracted from the PakV4 store) | **AV `0xC0000005`** — the original crash, reproduced in isolation |
| `.pss` (`t_天策撼如雷02_重制.pss`) | create OK (`rc=0`), but the object lacks the `IKG3D_NormalModel` play interface (stub at `vt[0xD58]`) |

Notes:
- The ruyifa tag `.Sfx` files (`SFX\发招\M明教元素18/19.Sfx`, `G光晕02.Sfx`,
  `释放_气场聚集03.Sfx`) are **not present in any locally accessible store** (PakV4
  extraction and `zsCache` PakV5 cache both miss them) — the game streams them on
  demand, so the host sees a missing file.
- The ME engine **AVs on a real, existing `.Sfx`** — that is the core bug, now isolated
  from the tag system.

## Mixed host retest (2026-09-30)

Running the current host against the **client engine** in the temp mixed host crashes
identically to the earlier experiment: fast-fail `0xC0000409` in
`KG3DEngineDX11EX64.dll` offset `0x18C82C4`, right after `EngineRay: ready` — the editor
shell is incompatible with the client engine build. The client engine's `.Sfx` behavior
therefore cannot be tested through the editor shell; the **native client-stack host**
(`CLIENT_STACK_PIVOT.md`) is required to finish the engine-driven effect path.

## Probe results (2026-09-30, `RC_SFX_PROBE=1` + `native/sfx_shim.cpp`)

The engine's own SFX factories are **callable on the MovieEditor build** (no fault):

| Call | Result |
|---|---|
| `KG3D_GetEngine2()` (engine DLL export) | live engine instance |
| `KG3D_Engine::CreateScreen3DSFX(&obj)` | `S_OK`, object created |
| `KG3D_Engine::CreateSFXTrackData(&obj)` | `S_OK`, object created |
| `engine+0x2c10` | live SFX manager; 32-slot vtable dumped |

Dumped vtables (ME build, image base 0x180000000):
- `IKG3D_Screen3DSFX` (vtable RVA `0x21F6508`): refcount + matrix get/set + overlay
  fields — a screen-space overlay object, not the world PSS player.
- `IKE3D_SFXTrackData` (vtable RVA `0x225A430`): data holder (int get/set, path) — the
  editor's SFX track timeline data.
- **SFX manager** (vtable RVA `0x21F6638`, 32 slots, methods at `0xB706D0`…`0xB753E0`):
  the world-effect API surface; slot `+0x68` is the `CreateScreen3DSFX` forward target.

Probe log: `C:\SeasunGame\MovieEditor\bin64\Skill\out\sfx_probe.log` (regenerated by any
run with `RC_SFX_PROBE=1`).

## Next steps (in order)

1. Identify the manager methods: disassemble the 32 slots (`0xB706D0`…`0xB753E0`) and
   find the create-from-path / play / bind-to-socket / stop entries (look for string
   path parameters and `KG3D_SFX_BIND_TYPE` uses).
2. Add the real wiring export to `sfx_shim.dll` (`RC_Shim_SfxPlay(path, socket…)`) once
   the method map is known.
3. Replace the `cast_pss` dummy path in `RebornClient.cs`; A/B proof (effect created by
   the engine, socket-bound, no re-adds).
4. Then re-test the `.Sfx`-tagged tanis on the ME build (the tag AV may be a separate
   issue from direct SFX creation, which now provably works).

## Why

The client plays ability effects through the engine's own SFX system: the tani's SFX
tags spawn PSS bound to caster sockets (`KG3D_SFX_BIND_TYPE`), with authored offsets.
Our host instead spawns a PSS path via `KGSceneCLR.AddDummyModel(name, pssPath, pos…)`
and re-adds it on movement. Consequences: no bone attachment, no authored socket
offsets, and `.Sfx` tags still AV on the MovieEditor engine build (2026-09-14).

## Engine API surface (verified)

Both engine builds (MovieEditor 09-14 and client 09-27) export the same SFX factories
(`KG3DEngineDX11EX64.dll`, mangled exports):

| Export | Signature | RVA (ME build) |
|---|---|---|
| `?CreateScreen3DSFX@KG3D_Engine@@UEAAJPEAPEAUIKG3D_Screen3DSFX@@@Z` | `long KG3D_Engine::CreateScreen3DSFX(IKG3D_Screen3DSFX**)` | `0x8ae9f0` |
| `?CreateSFXTrackData@KG3D_Engine@@UEAAJPEAPEAUIKE3D_SFXTrackData@@@Z` | `long KG3D_Engine::CreateSFXTrackData(IKE3D_SFXTrackData**)` | `0x8b13e0` |
| `?DestroySFXTrackData@KG3D_Engine@@UEAAJPEAPEAUIKE3D_SFXTrackData@@@Z` | destroy | — |
| `?GetAnimTagSystem@KG3D_Engine@@…` / `?SetAnimTagSystem@…` | tag system accessors | `0x8b7920` |
| `?ClearAnimTagSystemWithRefZero@KG3D_Engine@@QEAAHXZ` | tag system reset | — |

The interfaces (`IKG3D_Screen3DSFX`, `IKE3D_SFXTrackData`, `IKG3D_AnimationTagSystem`)
are **pure virtual — no exported methods**; their vtables must be mapped from the
engine's own implementation.

`CreateScreen3DSFX` is a forwarder (disasm): `engine+0x2c10` is the SFX manager; its
vtable slot `+0x68` is the real implementation. Module references to `Screen3DSFX`
exist in `KG3DEngineAdapterX64.dll` (both builds) and `KG_EngineEditorX64.dll`.

## Plan

1. **Map the interfaces** (RE, offline on copies): follow `engine+0x2c10` → SFX manager
   vtable → slot `+0x68` implementation → the created object's vtable; identify
   create/play/bind/stop methods and `KG3D_SFX_BIND_TYPE` values. The ME editor's
   `KG_EngineEditorX64.dll` (`Screen3DSFX` ref) and the adapter are the usage references.
2. **Native shim** (`native/` — extend `camera_shim.cpp` pattern): export
   `RC_Shim_SfxPlay(path, socket, followCaster)` etc.; call the engine factories on the
   engine pointer the host already holds (via the adapter's `Get3DEngineInterface` or the
   managed engine handle). Replace the `cast_pss` dummy path in `RebornClient.cs`.
3. **A/B proof**: same ability, same map — effect bound to the caster's socket, follows
   without re-adds; screenshot fingerprint + log (no `AddDummyModel` for the effect).
4. **Then** replay the `.Sfx`-tagged tanis: if the wired SFX path works on the ME build,
   the stale-build AV question can be re-tested; otherwise this converges with the
   client-stack host (`CLIENT_STACK_PIVOT.md`).

Acceptance: a cast's effect is created by the engine SFX API, attached to the caster
socket, and plays its authored timeline once — no host dummy, no re-add.

## Reproduce (recon)

```powershell
# exports (mangled signatures) on both builds
python - <<'PY'
import pefile
for p in (r"C:\SeasunGame\MovieEditor\bin64\KG3DEngineDX11EX64.dll",
          r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineDX11EX64.dll"):
    pe = pefile.PE(p, fast_load=True)
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXPORT"]])
    for e in pe.DIRECTORY_ENTRY_EXPORT.symbols:
        n = e.name.decode() if e.name else ""
        if "SFX" in n or "AnimTag" in n:
            print("%08x %s" % (e.address, n))
PY
# CreateScreen3DSFX forwarder disasm: tools/collision/recon-style capstone dump (temp)
```

Last verified: 2026-09-30
