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

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_DETACH)
    {
        g_engine = NULL;
        g_base = NULL;
    }
    return TRUE;
}
