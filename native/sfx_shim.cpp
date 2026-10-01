// sfx_shim.dll — engine SFX wiring probe (isolated name; the shared
// camera_shim.dll may be locked by other feature clients).
//
// Calls the engine's own exported SFX factories on the live engine instance
// (KG3D_GetEngine2) and dumps the returned interface vtables, so the real
// effect path can be wired instead of the free-standing dummy approximation.
// SEH-guarded: a faulting factory is reported, not fatal. Writes
// <exe dir>\Skill\out\sfx_probe.log.
#include <windows.h>
#include <stdio.h>

#define SHIM_EXPORT extern "C" __declspec(dllexport)

static const char* kEngineName = "KG3DEngineDX11EX64.dll";
static HMODULE g_engine = NULL;
static BYTE* g_base = NULL;
static char g_status[4096] = "not initialised";

typedef void* (__stdcall* GetEngine2Fn)(void);

SHIM_EXPORT int RC_Shim_SfxProbe()
{
    if (g_base == NULL)
    {
        g_engine = GetModuleHandleA(kEngineName);
        if (g_engine == NULL) { sprintf_s(g_status, "engine module not loaded"); return 1; }
        g_base = (BYTE*)g_engine;
        sprintf_s(g_status, "ready base=0x%p", g_base);
    }
    GetEngine2Fn getEngine = (GetEngine2Fn)GetProcAddress(g_engine, "KG3D_GetEngine2");
    if (getEngine == NULL) { sprintf_s(g_status, "sfx: KG3D_GetEngine2 missing"); return 1; }
    void* engine = getEngine();
    if (engine == NULL) { sprintf_s(g_status, "sfx: engine instance null"); return 2; }

    typedef long (__fastcall *SfxCreateFn)(void* self, void** out);
    SfxCreateFn createScreen = (SfxCreateFn)GetProcAddress(g_engine,
        "?CreateScreen3DSFX@KG3D_Engine@@UEAAJPEAPEAUIKG3D_Screen3DSFX@@@Z");
    SfxCreateFn createTrack = (SfxCreateFn)GetProcAddress(g_engine,
        "?CreateSFXTrackData@KG3D_Engine@@UEAAJPEAPEAUIKE3D_SFXTrackData@@@Z");

    char buf[4096];
    int n = sprintf_s(buf, "engine=0x%p createScreen=%p createTrack=%p\n",
                      engine, createScreen, createTrack);

    DWORD exc = 0;
    void* sfx = NULL;
    long rs = 0;
    if (createScreen != NULL)
    {
        __try { rs = createScreen(engine, &sfx); }
        __except (exc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { sfx = NULL; }
    }
    n += sprintf_s(buf + n, sizeof(buf) - n, "CreateScreen3DSFX r=0x%08X obj=0x%p exc=0x%08X\n",
                   (unsigned)rs, sfx, (unsigned)exc);
    if (sfx != NULL)
    {
        __try
        {
            void** vt = *(void***)sfx;
            n += sprintf_s(buf + n, sizeof(buf) - n, "  vtable=0x%p rva=0x%X\n",
                           vt, (unsigned)((BYTE*)vt - g_base));
            for (int i = 0; i < 16 && n < (int)sizeof(buf) - 96; i++)
                n += sprintf_s(buf + n, sizeof(buf) - n, "  vt[%02d] rva=0x%X\n",
                               i, (unsigned)((BYTE*)vt[i] - g_base));
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { n += sprintf_s(buf + n, sizeof(buf) - n, "  vtable unreadable\n"); }
    }

    exc = 0;
    void* track = NULL;
    long rt = 0;
    if (createTrack != NULL)
    {
        __try { rt = createTrack(engine, &track); }
        __except (exc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { track = NULL; }
    }
    n += sprintf_s(buf + n, sizeof(buf) - n, "CreateSFXTrackData r=0x%08X obj=0x%p exc=0x%08X\n",
                   (unsigned)rt, track, (unsigned)exc);
    if (track != NULL)
    {
        __try
        {
            void** vt = *(void***)track;
            n += sprintf_s(buf + n, sizeof(buf) - n, "  vtable=0x%p rva=0x%X\n",
                           vt, (unsigned)((BYTE*)vt - g_base));
            for (int i = 0; i < 12 && n < (int)sizeof(buf) - 96; i++)
                n += sprintf_s(buf + n, sizeof(buf) - n, "  vt[%02d] rva=0x%X\n",
                               i, (unsigned)((BYTE*)vt[i] - g_base));
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { n += sprintf_s(buf + n, sizeof(buf) - n, "  vtable unreadable\n"); }
    }

    {
        // the engine's SFX manager lives at engine+0x2c10 (ME build); dump its
        // vtable so the world-effect API can be mapped (slot +0x68 is
        // CreateScreen3DSFX's forward target)
        void* mgr = NULL;
        __try { mgr = *(void**)((BYTE*)engine + 0x2c10); }
        __except (EXCEPTION_EXECUTE_HANDLER) { mgr = NULL; }
        n += sprintf_s(buf + n, sizeof(buf) - n, "sfxManager=0x%p\n", mgr);
        if (mgr != NULL)
        {
            __try
            {
                void** vt = *(void***)mgr;
                n += sprintf_s(buf + n, sizeof(buf) - n, "  mgr vtable=0x%p rva=0x%X\n",
                               vt, (unsigned)((BYTE*)vt - g_base));
                for (int i = 0; i < 32 && n < (int)sizeof(buf) - 96; i++)
                    n += sprintf_s(buf + n, sizeof(buf) - n, "  mvt[%02d] rva=0x%X\n",
                                   i, (unsigned)((BYTE*)vt[i] - g_base));
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { n += sprintf_s(buf + n, sizeof(buf) - n, "  mgr vtable unreadable\n"); }
        }
    }

    {
        char exe[MAX_PATH] = {0};
        GetModuleFileNameA(NULL, exe, MAX_PATH);
        char* slash = strrchr(exe, '\\');
        if (slash != NULL) *slash = 0;
        char logPath[MAX_PATH];
        sprintf_s(logPath, "%s\\Skill\\out\\sfx_probe.log", exe);
        FILE* f = NULL;
        if (fopen_s(&f, logPath, "w") == 0 && f != NULL)
        {
            fputs(buf, f);
            fclose(f);
        }
    }
    sprintf_s(g_status, "sfx probe done (see Skill\\out\\sfx_probe.log)");
    return 0;
}

SHIM_EXPORT const char* RC_Shim_SfxStatus()
{
    return g_status;
}

// ---- real engine SFX playback -------------------------------------------
// KG3D_CreateSFXFromFile (engine RVA 0xBE4000, ME build): (owner=scene,
// path, a3..a8 optional). Returns the engine's KG3D_SFX instance or null.
// SEH-guarded; version-checked (timestamp/size) because the entry is an
// internal RVA, not an export.
static const DWORD ENGINE_TIMESTAMP = 0x6AA7C1F5;
static const DWORD ENGINE_SIZE_OF_IMAGE = 0x2EA7000;
static const DWORD RVA_CREATE_SFX_FROM_FILE = 0xBE4000;

typedef void* (__fastcall *CreateSfxFromFileFn)(void* owner, const char* path,
    void* a3, void* a4, void* a5, void* a6, int a7, void* a8);

static bool PeMatches2(BYTE* base)
{
    if (base == NULL || base[0] != 'M' || base[1] != 'Z') return false;
    DWORD pe = *(DWORD*)(base + 0x3C);
    DWORD ts = *(DWORD*)(base + pe + 8);
    DWORD size = *(DWORD*)(base + pe + 0x50);
    return ts == ENGINE_TIMESTAMP && size == ENGINE_SIZE_OF_IMAGE;
}

SHIM_EXPORT int RC_Shim_SfxPlay(const char* path, float x, float y, float z)
{
    if (g_base == NULL)
    {
        g_engine = GetModuleHandleA(kEngineName);
        if (g_engine == NULL) { sprintf_s(g_status, "engine module not loaded"); return 1; }
        g_base = (BYTE*)g_engine;
        if (!PeMatches2(g_base)) { g_base = NULL; sprintf_s(g_status, "engine build mismatch"); return 2; }
    }
    if (path == NULL || path[0] == 0) { sprintf_s(g_status, "sfx play: empty path"); return 3; }

    typedef void* (__stdcall* GetEngine2Fn)(void);
    typedef void* (__stdcall* EngineMethodFn)(void* self);
    GetEngine2Fn getEngine = (GetEngine2Fn)GetProcAddress(g_engine, "KG3D_GetEngine2");
    EngineMethodFn getWindow = (EngineMethodFn)GetProcAddress(g_engine,
        "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
    EngineMethodFn getScene = (EngineMethodFn)GetProcAddress(g_engine,
        "?Get3DScene2@KG3D_Window@@UEAAPEAVKG3D_Scene@@XZ");
    if (getEngine == NULL || getWindow == NULL || getScene == NULL)
    { sprintf_s(g_status, "sfx play: engine accessors missing"); return 4; }
    void* engine = getEngine();
    if (engine == NULL) { sprintf_s(g_status, "sfx play: engine instance null"); return 5; }
    void* window = getWindow(engine);
    void* scene = window != NULL ? getScene(window) : NULL;

    // owner chain: mirror the engine's own tag-spawn caller (code @0x76E51A):
    // singleton @RVA 0x2CF7038 -> vt[10]() -> helper @0x8ABAB0(&out) = owner
    void* owner = NULL;
    void* iface = NULL;
    DWORD oexc = 0;
    char ownerInfo[256] = {0};
    // engine caller @0x76E51A: out[0] = singleton->vt[8]() (the owner iface);
    // out[8] = wrapper from vt[10]()+0x8ABAB0 (a different context)
    __try
    {
        void** g = *(void***)(g_base + 0x2CF7038);
        if (g != NULL)
        {
            typedef void* (__fastcall *GetIfaceFn)(void* self);
            GetIfaceFn getOwner = (GetIfaceFn)(*(void***)g)[8];
            owner = getOwner(g);
            GetIfaceFn getCtx = (GetIfaceFn)(*(void***)g)[10];
            iface = getCtx(g);
        }
    }
    __except (oexc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { owner = NULL; }
    if (owner != NULL)
    {
        void** vt = NULL;
        __try
        {
            vt = *(void***)owner;
            sprintf_s(ownerInfo, "ownervt=0x%p vt0=0x%X vt1=0x%X vt2=0x%X",
                      vt, (unsigned)((BYTE*)vt[0] - g_base), (unsigned)((BYTE*)vt[1] - g_base),
                      (unsigned)((BYTE*)vt[2] - g_base));
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { sprintf_s(ownerInfo, "ownervt unreadable"); }
    }

    if (owner == NULL) { sprintf_s(g_status, "sfx play: owner chain failed exc=0x%08X scene=0x%p", (unsigned)oexc, scene); return 6; }

    // SFX pool global (written by KG3D_CreateSFXPoolManager @0x197FA0, called
    // from KG3D_Engine::Init): if null, init it before creating effects
    void* pool = NULL;
    DWORD pexc = 0;
    __try { pool = *(void**)(g_base + 0x2CF7BB0); }
    __except (EXCEPTION_EXECUTE_HANDLER) { pool = NULL; }
    long poolRc = -1;
    if (pool == NULL)
    {
        __try
        {
            typedef long (__fastcall *PoolFn)(void);
            PoolFn mkPool = (PoolFn)(g_base + 0x197FA0);
            poolRc = mkPool();
        }
        __except (pexc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { poolRc = -1; }
        __try { pool = *(void**)(g_base + 0x2CF7BB0); }
        __except (EXCEPTION_EXECUTE_HANDLER) { pool = NULL; }
    }

    CreateSfxFromFileFn create = (CreateSfxFromFileFn)(g_base + RVA_CREATE_SFX_FROM_FILE);
    void* sfx = NULL;
    void* outParam = NULL;
    float mtx[16] = { 1,0,0,0, 0,1,0,0, 0,0,1,0, x,y,z,1 };
    DWORD exc = 0;
    DWORD64 fault = 0;
    PEXCEPTION_POINTERS ep = NULL;
    char stackDump[1024] = {0};
    // args mirror the engine's SFX-module caller (code @0xE3412A): a6 = world
    // matrix, a8 = out slot; r9/a5/a7 zero
    __try { sfx = create(owner, path, NULL, NULL, NULL, mtx, 0, &outParam); }
    __except (ep = GetExceptionInformation(),
              exc = ep->ExceptionRecord->ExceptionCode,
              fault = (DWORD64)ep->ExceptionRecord->ExceptionAddress,
              EXCEPTION_EXECUTE_HANDLER)
    {
        sfx = NULL;
        // walk the faulting stack: log the first frames as module-relative RVAs
        DWORD64* sp = (DWORD64*)ep->ContextRecord->Rsp;
        int n = 0;
        for (int i = 0; i < 16 && n < (int)sizeof(stackDump) - 40; i++)
        {
            DWORD64 v = 0;
            __try { v = sp[i]; } __except (EXCEPTION_EXECUTE_HANDLER) { break; }
            HMODULE mod = NULL;
            char name[MAX_PATH] = {0};
            if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                   (LPCSTR)v, &mod) && mod != NULL)
                GetModuleFileNameA(mod, name, MAX_PATH);
            const char* bn = strrchr(name, '\\');
            bn = bn ? bn + 1 : name;
            n += sprintf_s(stackDump + n, sizeof(stackDump) - n, "%s+0x%llX ", bn, (unsigned long long)(v - (DWORD64)mod));
        }
    }
    sprintf_s(g_status, "sfx play owner=0x%p iface=0x%p %s scene=0x%p pool=0x%p poolRc=%d path=%s mtx=(%.0f,%.0f,%.0f) -> obj=0x%p out=0x%p exc=0x%08X fault_rva=0x%X stack=[%s]",
              owner, iface, ownerInfo, scene, pool, (int)poolRc, path, mtx[12], mtx[13], mtx[14],
              sfx, outParam, (unsigned)exc,
              (unsigned)(fault > (DWORD64)g_base ? (fault - (DWORD64)g_base) : 0), stackDump);
    return sfx != NULL ? 0 : 7;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_DETACH)
    {
        g_engine = NULL;
        g_base = NULL;
    }
    return TRUE;
}
