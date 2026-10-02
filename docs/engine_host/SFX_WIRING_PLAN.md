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

### Client SFX manager vtable map (2026-10-01, live probe)

`engine+0x2c10` = the SFX manager (COM-like). Its vtable has **15 slots** (engine RVAs):
`vt0 0xB71AC0` = QueryInterface (matches interface vtables against globals
`0x1FC9D50/0x1FC9D58`, `0x1CCA7F0/0x1CCA7F8`; stores the adjusted pointer, AddRefs);
`vt1 0xE7370` = AddRef; `vt2 0xB71B40` = Release; `vt3 0xB727B0`; `vt4 0xB71BA0`;
`vt5 0xB71D50`; `vt6 0xB71EF0`; `vt7 0xB71FD0`; `vt8 0xB720A0`; `vt9 0xB721D0`;
`vt10 0xB72310`; `vt11 0xB72480` (copies a matrix from rdx); `vt12 0xB72600`;
`vt13 0xB726E0` (CreateScreen3DSFX forward target, `+0x68`); `vt14 0xB729D0` (dtor).
Most slots forward to sub-objects at `[mgr+0xbd8]+off` (`+0x210/+0x420/+0x4d0/+0x580/+0x630`).

`[mgr+0xbd8]` = a sub-manager whose vtable is a repeating 4-slot pattern ×8 entries
(`[0xB75400, 0xB75370, 0xB76780, 0x22F8690]`, `[0xB75270, 0xB75370, 0xB76730, 0x22F8828]`,
`[0xB75C50, 0xB75370, 0xB76960, 0x22F8668]`, `[0xB75C50, …, 0xB76910, 0x22F8640]`,
`[0xB758F0, …, 0xB768C0, 0x22F85F0]`, `[0xB75590, …, 0xB76870, 0x22F8798]`,
`[0xB76610, …, 0xB76A50, 0x22F88A0]`, `[0xB758F0, …, 0xB76A00, 0x22F8878]`) — 8 SFX
type entries, each with a shared helper (`0xB75370`) and a small type function.

The engine's own bind entry `KG3D_SFXModel::BindData` (client `0xE345DC`, called via a
runtime function pointer — no static callers/vtable slot) is the path that creates+casts+
attaches+plays; driving it needs the SFX-model context object. This object graph is the
next RE chunk for engine-driven playback (evidence `submgr.out`, `play3.out`).

### Actor-from-file probe + case finding (2026-10-01)

`KG3D_Engine::CreateActorFromFile` (client export `0x8B2DA0`) with the real `.Sfx`:
`KG3D_CreateModelFromFile` → `KG3D_SFX::Init` → `KG3D_CreateSFXFromFile` **fails**
(`KG3D_LoadFile failed … sfx\增益\c纯阳坐忘.sfx`; note the engine **lowercases** the path).
The same path via the **direct create with the engine-singleton owner works in both
cases** (mixed `SFX\…Sfx` and lowercase `sfx\….sfx` both produce a non-null object,
`exc=0`) — so the actor path's failure is its **owner/FS context**, not the case: the
owner passed by the actor/model path resolves through a different file layer than the
singleton owner used by the engine's own SFX flow. Evidence `actor.out`, `case.out`.

Practical takeaway: engine-driven playback should go through the owner used by the
engine's own SFX flow (the singleton `[engine+0x2CF1038] -> vt[8]()`), which already
creates the `.Sfx` cleanly; the bind/attach context remains the open piece.

### Data/FS boundary found (2026-10-01)

Direct FS probes on the live client engine:
```
g_IsFileExist(mesh)=0   sfx=1          (game layer, Engine_Lua5X64 0xB5060)
KG3D_LoadFile(mesh)=0   sfx=<non-null> (engine file manager, engine 0xB0F870)
```
The **game file layer serves the PakV4 store only** — a loose file copied into the
working root (`client_root\data\source\player\f1\测试\f1_3094_body_hd.mesh`) is **not**
visible, even after `g_SetRootPath` + `g_SetFilePath` (`Lua+0x170060` / `Lua+0x170170`).
Consequence: engine-driven actor/animation/tag playback can only use **PakV4-resident**
assets; the player models are not in PakV4 (they exist only in the updater's
`_HttpFileForDebug_` cache — the local install is a partial client, models streamed on
demand). The engine's animation tag system **does exist** (engine `GetAnimTagSystem`
non-null, vtable 28 slots in `KG3D_AnimationTagX64.dll`, RVAs `0xAA40…0xCD90`), but with
no loadable actor/animation the tag path cannot fire.

Achievable locally with current data: engine-driven playback of **PakV4-resident
effects** (e.g., `c纯阳坐忘.Sfx` — create proven) once the bind context is cracked;
actor/animation-driven authored effects need runtime model assets the local install
lacks. Evidence `fs.out`, `fspath.out`, `tag3.out`.

Follow-up probe (`fs2.out`): **both case variants of the SFX load** through the engine
loader and the game layer (`g_IsFileExist sfx=1 sfxLow=1`,
`KG3D_LoadFile sfx=<non-null> sfxLow=<non-null>`), so the actor path's earlier
lowercased-path failure is **not** a case issue — the actor path's `KG3D_SFXData` load
goes through a **different owner/context FS** (`KG3D_LoadFileToMemory` under the
actor/model context) that our host does not set up. The working owner remains the engine
singleton (`[engine+0x2CF1038] -> vt[8]()`). Loose files under the working root stay
invisible to both FS layers (pak-only), confirmed with the copied player mesh.

Scene setup now works in the probe (`view.out`): `CreateEmptyScene(0, &scene) -> 0`,
`CreateSceneViewFrom3DScene(scene, "probe_view", NULL, &view, 0) -> 0`,
`AddSceneView_SceneView(window, view, 0, 1) -> 0` → **`Get3DScene2(window)` returns the
scene**. The SFX bind still returns `E_NOINTERFACE` with a scene present — the missing
piece is the SFX model's attach/bind context (its `vt[0xD60]` is the shared stub on the
directly-created object), i.e. registration through the engine's own SFX flow
(scene-object/SFX-model context), not scene availability.

### Actor from a real client model + engine's own SFX-model bind site (2026-10-01)

Path bug fixed: the player model dirs are `动作`/`部件` (not what the mojibake suggested);
`data\source\player\f1\部件\f1_3094_body_hd.mesh` **is PakV4-resident** and
`CreateActorFromFile(mesh) -> rc=0, actor=<live>` now succeeds (actor vtable mapped,
40 slots; `g_IsFileExist(mesh)=1`). All **757 ability tanis are PakV4-resident**
(`PakV4SfxExtract`: extracted 757 / not found 0). The engine's animation tag system is
present (`GetAnimTagSystem` non-null). Remaining for the original tag path: the
animation-play entry (the controller/`IKG3D_Model` play method; the actor's animation
controller is created internally).

**Engine's own SFX-model creation+bind** (client engine): function `0xE272B4`
(runtime-dispatched; direct `BindData` caller at `0xE28414`) allocates a **0xC8-byte
`KG3D_SFXModel`**, initialises matrices from `0x1F6D740` (identity), then calls
`KG3D_SFXModel::BindData (0xE345E0)` with `(model, r12, [rsp+0x48], [r15+0x128][rdi*8])`
— i.e. the engine iterates a per-index model list from the loaded SFX data and binds
each model. This is the bind context to replicate: load the SFX data (direct create
works), then run this per-model allocate+bind with the engine's own data object.

### Original play path wired (2026-10-01)

- The model inside the actor is at **`actor+0x358`** (`m_piCurModel`); it is a
  `KG3D_SkinModel` (vtable mapped; `Update(pAniInfo)`, `FrameMove`, `SetBoneTransform`).
- The **animation controller** is `alloc(0x3A8)` + ctor `0xBC1100` (engine's own
  allocation site at `0x81D3C7`); `SetActor(actor)` (export `0xBC2120`) returned
  **S_OK**; the play entry is
  `?StartAnimation@KG3D_AnimationController@@…(KG3D_Animation*, KG3D_ANI_PLAY_TYPE, float, …)`
  at `0xBC1C70` (controller vtable slot 22), advanced with `FrameMove` (`0xBC2620`).
- `KG3D_Engine::CreateAnimationFromFile` (`0x8B6EB0`) **fails on the pak `.tani`**:
  `unsupport ani type 1970238300` — the loader checks the buffer magic against **`ANIM`**
  (`0x4D494E41`, code `0xC31538`) but the pak `.tani` files start with **`GATA`**
  (`data\source\player\f1\动作\…tani` head = `GATA\0\0\0\0data\source\…`) — the GATA
  container must be unwrapped to the raw ANIM payload before the animation loader runs
  (the engine's data-manager reader path does not unwrap it in our host). Next piece:
  the GATA unwrap layer (engine `KG3D_BufferReader` / data-manager type registration).

GATA follow-up (`reader2.out`): the engine's own `KG3D_LoadFile(tani)` returns a reader
whose buffer **still starts with `GATA`** (`buffer head: GATA 01 00 00 00 64 61 74 61` =
`GATA` + flags + `data…`), i.e. the engine FS does not unwrap either. The animation data
manager checks the **unwrapped** payload magic (`ANIM`), so the GATA unwrap must come
from the layer the game uses for streamed/packed files (streaming file manager / game
`g_OpenFile` `IFile` read). Load flags a4 = 0/1/2 all fail identically.

Container details (`loose2.out`, `kgopen.out`): the `.tani` GATA wrapper's inner name is
**`.ani`** (`data\source\player\f1\动作\f1b01ty…02.ani`), payload after the path is not
`ANIM` (likely compressed) — the extractor dumps the raw PakV4 record. **Loose files ARE
served** by the game layer (`g_IsFileExist("data\zz_loose_test.txt") = 1` — the earlier
"pak-only" conclusion was a wrong-path artifact), so writing an unwrapped file into the
working root is a viable route if pak precedence allows. `KG_OpenFile(tani)` (game-layer
`0xC02E0`) returned NULL — needs the right API/args; the GATA unwrap layer is the last
blocker for the original tag-driven playback. Leads for the unwrap: game-layer
`KG_OpenPakV4File` (`0xCC670`), `g_OpenFile` (`0xB2F50`), `g_OpenAloneFile` (`0xB2EA0`),
and the PakV4 manager's `KG_PAKFS_*` APIs.

Latest probes (2026-10-01 late): **pak wins over loose** — a fake `ANIM` file written to
the tani's loose path did not change the load error, so the loose-file route cannot
override a pak-resident file. The "unsupport ani type" value `0x75736F5C` is literally
the GATA header's **path bytes at offset 12** (`…data\source…` → `\sou`), i.e. the
animation data manager reads a **type field at offset 12** of the buffer while the pak
`.tani` layout has the path there (`GATA` + flags + path + NUL + payload) — the manager
expects a different container layout than the extractor's raw pak record. The engine's
own `KG3D_DataContainer` (`GetData/PopData`, engine `0xB3D940/0xB3DB90/0xB3DCD0`, type
count 0xD) parses GATA, and `KG3D_CreateAnimationFromFile` (`0xC41840 → 0xC41EA0`)
uses it — the remaining piece is making the animation path use the container parser (or
reproducing the pak record layout the manager expects).

### Pak record vs extractor output — compression (2026-10-01)

`KG_OpenPakV4File(tani, 1)` returns the PakV4 file object (`KGPK4_FileSystemX64` class;
inner object at `pakfile+8`, vtable `KGPK4` base+`0xA7540…`; `vt[3]`=`KPakV4File::Seek`,
`vt[4]`=size getter, `vt[2]`→storage `vt[0x20]` = record pointer). Its record buffer:
`GATA` + **flags=1** + path + NUL + payload, size **0xF48 (3912)**. The extractor's file
is `GATA` + **flags=0** + path + NUL + payload, size **5532** — i.e. the pak record is
**compressed** (flags=1) and the extractor writes the **decompressed** form (flags=0);
payload heads differ (`00 00 10 00 …` in the pak vs `0e a0 00 00 …` from the extractor).
The engine's `KG3D_LoadFile` reader also returned the compressed record (flags=1, 3912).
Conclusion: the animation manager expects the **decompressed inner file** (extractor
form) or a container parser that honours the flags; the unwrap/decompress step of the
PakV4 read path is the exact last layer. Next: the PakV4 manager's decompressing read
(`KG_PAKFS_*` record APIs / storage read modes).

Read-mode discovery (`flags.out`, `clean.out`): **`KG3D_LoadFile(path, flag)` selects the
read mode** — mode 0 returns the raw pak record (`GATA` flags=1, 3912 B), mode 1/2
return a **204-byte `ANIM` chunk** (`size=0xCC head=ANIM`). Hooking `KG3D_LoadFile` to
force mode 1 for `.ani`/`.tani` removes the "unsupport ani type" error but the animation
still fails (`*ppiRetAnimation`), so the manager needs a different mode/container than
the 204-byte chunk (likely the full decompressed container). The probe is now clean and
focused (`tools/engine_host/client_sfx_probe.cpp`): window hook → facade/adapter/file
layer → engine (`vt[0](0,4) -> 0`) → scene+view → actor from a PakV4 model
(`actor+0x358` model) → controller (`0x3A8` alloc + ctor, `SetActor` S_OK) →
`CreateAnimationFromFile` → `StartAnimation`/`FrameMove`, plus the direct `.Sfx`/`.pss`
create tests (both `exc=0`).

LoadFile sequence probe (`lfseq.out`): **the tani path does not go through
`KG3D_LoadFile`** (only the actor's face `.ani`s and an SFX-mesh `.ani` do) — the
animation data manager creates its **own reader**, which reads the raw pak record
(mode-0 equivalent) and fails the `ANIM` type check. `KG_OpenPakV4File`/wrapper
`OpenFile(path, NULL, 0|1)` both return file objects (decompress flag not yet
distinguished; the returned object's inner pointer was null in the probe). Next: the
animation data manager's reader creation (its own read mode / the wrapper's decompressing
read), not `KG3D_LoadFile`.

Branch trace (`ani2.out`, `trace4.out`): the animation entry is `KG3D_Animation` (vtable
`0x22195C0`; `vt[25]` = `LoadFromFile 0xC30DA0`; the `.tani` fall-through calls `vt[28]`
= `0xC347A0` — the generic loader that would call the mode-aware `0xB0FC60(path, mode)`
with the mode from `engine->vt[0xa10]()`), but for the ability `.tani` **neither
`0xC347A0` nor `0xB0FC60` fires** — the failure comes from the shared
`KG3D_DataManager::Load` helper (string refs `0x437D0C/0xA4D0E6/0xA4D129/0xA54E01/
0xA63B9A`, error line 75) that reads the raw pak record and rejects the `GATA` type. So
the fix target is that shared data-manager load helper (its reader/mode), not the
animation class. Probe restored to the clean committed state.

Helper internals (`0x437CFC`): the helper calls the data object's **`vt[5]`** with the
reader (`this->vt[5](reader)`; `KG3D_Animation` vt[5] = `0xA49680`, the templated
`KG3D_DataManager<T>` parse) and logs `KG3D_DataManager::Load` line 75 on failure. The
reader is created by the helper's **caller**; the parse (type check `ANIM` at
`0xC31538`) rejects the `GATA` pak record. Remaining: find the helper's caller (reader
creation — game-layer `KG_OpenFile`/wrapper OpenFile mode) and make it read the
decompressed container (or pass the extractor-form data).

Template-instantiation probe (`dcl3.out`): the container loader `KG3D_DataManager<T>`
is instantiated at least at **`0x437AD0`** (init path — fires for
`data\source\npc_source\a021\模型\A021.txt`) and **`0xA4D010`** (animation-shaped
instance) — but the ability `.tani` failure goes through **neither**; the
`unsupport ani type` log has 5 string refs (`0x437D0C/0xA4D0E6/0xA4D129/0xA54E01/
0xA63B9A`), so the tani's instantiation is another site. Next: identify the tani's
instantiation by hooking the type-check log site (`0xC3157E`, the
`unsupport ani type` emitter) and walking its caller, or by scanning all five refs'
function entries.

**Resolved (`.pdata` boundaries):** the whole tani load lives in
`KG3D_Animation::LoadFromFile` (`0xC30DA0..0xC34093`; parse + `unsupport ani type`
type check `0xC31538` are inlined in it; `vt[25]` already pointed here). The reader
creation is at `0xC30FD6..0xC31021`: `call 0xB101F0` (mode check) →
- nonzero: `engine->vt[0x508]()` = mode → `0xB0FC60(path, mode)` (mode-aware loader;
  wraps `manager->vt[0xD8]()->vt[2](path,mode)` in a 0x20-byte reader wrapper) →
  reader->vt[4](0,&out) → `[this+0x28]`
- zero: `0xB0F870(path, 0)` (raw loader) → **GATA record** → parse fails
The mode manager is the global `0x2D22598`, created by the engine's lazy getter
`0xB0F720(root)` → `0xB105E0(root)`: requires a non-null root string (r8; game
callers pass `engine+0xEEC`, `0x135760`, `0x1359EB`), allocates 0x170 bytes,
`runtime class` wrapper stored back after `0xB524A0`. Called from `0x8BA0CD`
(engine working-root setter) and `0x135767/0x1359F2`.
**REACHED — engine-driven animation play works (iat2.out/tag.out).** The engine's
own `.tani` path is `KG3D_Actor::_InitAttachTani` (`0x83A6F0`, the `.tani` branch of
the animation-create dispatch at `0x836CAE`): it asks `engine->vt[0x230]()` for the
attach manager, `manager->vt[0xC](taniPath)` for the descriptor, then
`descriptor->vt[3]()` returns the **resolved inner path** — the tani
`data\source\player\f1\动作\F1HA393_start01.tani` resolves to
`data\source\player\f1\动作\f1ha393_start01.ani` (the GATA container's inner file).
`0xC41EA0(resolvedPath)` then loads successfully (the file layer serves the `.ani`
from the `.tani` container decompressed), the animation object lands at
`actor+0x368`, `StartAnimation` (`0xBC1C70`) returns 0 and 120 `FrameMove`
(`0xBC2620`) calls return 0 — the authored clip plays on the client engine.
`_InitAttachTani` still reports `KGLOG_COM_PROCESS_ERROR(0x80004005)` at line
11447 (`call [anim vtable+0x90]`, the post-load attach/speed step), but the
animation object is valid and playable.
Tag-driven SFX: none fired during the 120 frames (the `[TagSfx]` lines in tag.out
are the probe's own direct `.Sfx`/`.pss` tests) — next: find what evaluates the
clip tags (controller/model update or the failing `vt+0x90` attach step) and/or
confirm this clip carries SFX tags.

**Tag-system API map (labeled from embedded name strings).** `KG3D_AnimationTagX64.dll`
vtable `0x49730` (27 slots): slot0 `0xAAD0` `Init`, slot1 `0xBA00` `FrameMove`,
slots2-4 `ClearWithRefZero*`, slots5-6 `CreateAnimationTaniFromFile`
(`0xBC50/0xBCB0`, `KG3D_AnimationTani_DataTable::SetSoundShell` inlined), slots7-10
`CreateAnimationTaniFromFile/FromData` (`0xBD30..0xBF10`), slot11 `0xC050`
`CreateAnimationTaniFromData`, slots13-23 TagTexture.ini helpers
(`data\public\TagEditor\TagTexture.ini`), slots24-25 `0xCD80/0xCD90`
`KG3D_AnimationTani::GenerateTagFromData` / `CreateAnimationTagFromData`.
SFX tag payload sample (uncompressed tani, `F1stm09通脉02…tani`): a tag entry
contains `01 00 00 00 | 0B 00 00 00 | 0B 00 00 00 | "data\source\other\特效\技能\sfx\<name>.sfx"` —
full lowercase-`.sfx` paths (GBK), e.g.
`data\source\other\特效\技能\sfx\尚武\尚武_...f301.sfx`. 143 of 1478 extracted tanis
contain `.sfx` strings; `F1HA393_start01` does **not** (that clip has no SFX tags —
pick a tagged clip, e.g. `f1stm09通脉02`, for the firing test).
Next: call `tagSystem->CreateAnimationTaniFromFile(taniPath)` (+ the tani object's
tag query) in the probe to enumerate SFX tags with their paths, then bridge active
tags to `KG3D_CreateSFXFromFile`; or drive `KRLAnimationFactory` from
JX3RepresentX64 for the full client behavior.

**Tag-system runtime probe (tsys.out/tani2.out).** Runtime tag-system vtable is the
0x49730 table **minus its first 2 entries** (runtime slot N = table index N-2).
Calling the "create" slots on
`data\source\player\f1\动作\F1stm09通道02连环弩.tani` (11 `.sfx` tags):
vt[7] (`0xBC50`), vt[9] (`0xBD30`), vt[10] (`0xBD80`), vt[12] (`0xBF10`) → null;
vt[11] (`0xBE60`) → an **empty `KG3D_AnimationTani_Data`** (vtable `0x4A638`;
member getters +0x20..+0x50 all null; slots: `LoadFromFile`, `SaveToFile`,
`_NewTagData(KG3D_ANIMTAG_TYPE)`, tag-list getters). The file-load path needs more
than a bare path (reader/data or the 3D-engine/sound shell — `SetSoundShell` guards
`m_pi3DEngine == nullptr`). The represent module (`JX3RepresentX64.dll`) remains the
client's own full consumer (`KRLAnimationFactory` + tag dispatcher 0x37F2C0-0x37F6D6)
— adopting it is the reliable route; the tani-data object is the fallback once its
load API is wired.

Client tag→effect implementation found (JX3RepresentX64.dll). The client's own
animation-tag bridge lives in the represent module: `KRLAnimationFactory::CreateSFXTag`
(0x37F400, internal — `[factory+0x218]` sub-object + `0xC874` build), plus
`ReactivatedCreateSFXTag` (0x37F490 region), `CreateMovieObjectTag` (0x37F2xx) and
`CreateFaceMotionTag` — a per-tag-type factory set invoked from a tag dispatcher at
0x37F2C0-0x37F6D6 (each logs its name on failure). The tag system
(`KG3D_AnimationTagX64.dll`, vtable 0x49730, 27 slots) holds default footstep SFX
roots in GBK at 0x4AAC0/0x4AAF8/0x4AB30/0x4AB68
(`data\sound\角色\人物\脚步\脚步声_{小型/中等/大型}体型人类_十路.Sfx`, `..._马蹄_十路.Sfx`)
wired into members +0x20..+0x38; `KG3D_AnimationSFXTag::GetTagInfo` (0x4910) fills
`{+0: tag, +8: int, +0xC: bool, +0x10: bool, +0x14: 1}` from `[tag+0x30]`.
Consequence: the engine parses/hands out tags; the **represent module is the client's
own consumer** that turns them into playable effects. Next (Phase 2): either (a) drive
the represent module's factory/ECS with the engine's animation update info (most
faithful; `CreateSO3Represent` already boots), or (b) host-side bridge: read the
animation's SFX tags from the tag system and play them with the engine's own
`KG3D_CreateSFXFromFile` (the MovieEditor adapter's `_OnProcessActiveSFXTag` pattern).

Probe test: `0xB0F720(g_rootA)` after engine init **returns 0 (success)** but the
tani still takes the raw path → `0xB101F0` still 0: the manager needs its
path→mode mapping (what `manager->vt[0xD8]()` returns and what `obj->vt[1](path)`
answers). Next: map the wrapper vtable from `0xB524A0` and the mapping init (who
fills it — likely an engine API keyed by data type/extension).

**Index-package resolution (ipm.out/ipm2.out):** the map is resolved:
- manager callback at `[manager+0x160]` = `g_GetIndexPackageReader` resolved via
  `GetProcAddress` from **`KIndexpackX64.dll`** (`[manager+0x168]` = its module);
  `g_DownloadHttpFile` at +0x150 from the module at +0x148.
- probe: preload `bin64\KIndexpackX64.dll` + call its own
  **`g_InitIndexPackManager(NULL, NULL)`** (returns 1, creates the singleton at
  `KIndexpack+0x3D220`; reader = singleton+8, vtable `0x35928`, vt[1] →
  `0x10DD0(reader, path, flags)`).
- The check consults the **index-package map table** (`singleton+0x280`, also
  `g_GetIndexPackageMapTable`): our table is empty → `check=0`.
- Reading: this is the **updater/streamed index package** (with
  `g_DownloadHttpFile` alongside) — NOT the PakV4 paks. So for a pak-resident
  `.tani` the raw path is the **expected** route in the game too; the remaining
  defect is the **data content**: engine `KG3D_LoadFile(path,0)` returns the
  3912-byte **GATA-compressed** record while the parse expects the decompressed
  `ANIM` container (extractor gives 5532 B, flags=0).
- Next: identify the reader returned by `0xB0F870` (class/vtable) and how the
  real parse gets decompressed bytes (`vt[8]` buffer? or a GATA handler in the
  data-manager/parse); compare with the extractor's flags=0 record.

### `KG3D_SFXModel` method map (2026-10-01)

Method-name strings (registered names, engine RVAs): `BindData 0x2257A40` (code
`0xE345E0`; creates+casts+binds, calls `vt[0xD58]/[0xD60]/[0x180]/[0x190]`),
`GetRenderUnit 0x2257AC8`, `_UpdateAniInfo 0x2257AE8`, `_SeekAniBySFXFrame 0x2257B28`,
`FrameMove 0x2257B70` (code around `0xE34C91`), `Update 0x2257B90` (code around
`0xE25C70`), `UpdateTargetRenderData 0x2257BA8`. No static vtable/factory found for the
class (instances are created via runtime registration / owner context), which is why the
engine's actor-from-file path and a standalone bind cannot be driven yet.

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
