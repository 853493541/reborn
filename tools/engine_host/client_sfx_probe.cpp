// client_sfx_probe.cpp — client-stack recon probe (read-only; see
// docs/engine_host/SFX_WIRING_PLAN.md).
//
// Boots the client engine the way the game does:
//   X3DEngine facade -> adapter -> game file layer (Engine_Lua5X64) -> engine
//   (window provided by the host via a CreateTargetWindow hook),
// then runs the engine's own actor/model/animation path and the direct
// KG3D_CreateSFXFromFile tests.
//
// Runtime root: set RC_PROBE_ROOT to a client-root copy
// (default: %TEMP%\opencode\skillv2\client_root).
#include <windows.h>
#include <intrin.h>
#include <stdio.h>
#include <stdarg.h>

static void logf(const char* fmt, ...)
{
    va_list ap;
    va_start(ap, fmt);
    vfprintf(stdout, fmt, ap);
    fprintf(stdout, "\n");
    fflush(stdout);
    va_end(ap);
}

static void gbk(const wchar_t* src, char* out, int cap)
{
    WideCharToMultiByte(936, 0, src, -1, out, cap, NULL, NULL);
}

// ---- inline hook -----------------------------------------------------------
static int installInlineHook(HMODULE mod, DWORD rva, void* hook, BYTE* saved, BYTE** trampOut, int len)
{
    BYTE* target = (BYTE*)mod + rva;
    if (len < 12) len = 12;
    memcpy(saved, target, len);
    BYTE* tramp = (BYTE*)VirtualAlloc(NULL, 64, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    if (tramp == NULL) return 0;
    memcpy(tramp, saved, len);
    BYTE* p = tramp + len;
    *p++ = 0x48; *p++ = 0xB8;
    *(void**)p = target + len; p += 8;
    *p++ = 0xFF; *p++ = 0xE0;
    DWORD oldp = 0;
    if (!VirtualProtect(target, len, PAGE_EXECUTE_READWRITE, &oldp)) return 0;
    BYTE patch[12];
    patch[0] = 0x48; patch[1] = 0xB8;
    *(void**)(patch + 2) = hook;
    patch[10] = 0xFF; patch[11] = 0xE0;
    memcpy(target, patch, 12);
    memset(target + 12, 0x90, len - 12);
    VirtualProtect(target, len, oldp, &oldp);
    FlushInstructionCache(GetCurrentProcess(), target, len);
    *trampOut = tramp;
    return 1;
}

// ---- hooks -----------------------------------------------------------------
static HWND g_probeHwnd = NULL;
static BYTE g_ctwSaved[32];
static BYTE* g_ctwTramp = NULL;

typedef long (__fastcall *CreateTargetWindowFn)(void* self, void* hwnd, void** out);
static long __fastcall hookCreateTargetWindow(void* self, void* hwnd, void** out)
{
    logf("[CreateTargetWindow] hwnd=%p -> using %p", hwnd, g_probeHwnd);
    if (g_probeHwnd != NULL) hwnd = g_probeHwnd;
    return ((CreateTargetWindowFn)g_ctwTramp)(self, hwnd, out);
}

static BYTE g_lfSaved[32];
static BYTE* g_lfTramp = NULL;
static int g_lfForceMode = 0;

typedef void* (__fastcall *LoadFileFn)(const char* path, int flags);
static void* __fastcall hookLoadFile(const char* path, int flags)
{
    if (path != NULL && (strstr(path, ".tani") != NULL || strstr(path, ".ani") != NULL))
    {
        printf("[LoadFile] flags=%d path=%s\n", flags, path);
        fflush(stdout);
        if (g_lfForceMode > 0 && flags == 0) flags = g_lfForceMode;
    }
    return ((LoadFileFn)g_lfTramp)(path, flags);
}

static int __cdecl hookPrintfLog(int channel, const char* fmt, ...)
{
    va_list ap;
    va_start(ap, fmt);
    printf("[KGLOG ch=%d] ", channel);
    vprintf(fmt, ap);
    printf("\n");
    fflush(stdout);
    va_end(ap);
    return 0;
}

static int patchIat(HMODULE mod, void* realFn, void* replacement)
{
    if (mod == NULL || realFn == NULL) return 0;
    BYTE* base = (BYTE*)mod;
    int patched = 0;
    __try
    {
        PIMAGE_DOS_HEADER dos = (PIMAGE_DOS_HEADER)base;
        PIMAGE_NT_HEADERS nt = (PIMAGE_NT_HEADERS)(base + dos->e_lfanew);
        DWORD size = nt->OptionalHeader.SizeOfImage;
        for (DWORD i = 0; i + 8 <= size; i += 8)
        {
            if (*(void**)(base + i) == realFn)
            {
                DWORD oldp = 0;
                if (VirtualProtect(base + i, 8, PAGE_READWRITE, &oldp))
                {
                    *(void**)(base + i) = replacement;
                    VirtualProtect(base + i, 8, oldp, &oldp);
                    patched++;
                }
            }
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { }
    return patched;
}

static LONG WINAPI vehHandler(PEXCEPTION_POINTERS ep)
{
    if (ep->ExceptionRecord->ExceptionCode == 0xC0000409)
        logf("[VEH] fast-fail at=%p", ep->ExceptionRecord->ExceptionAddress);
    return EXCEPTION_CONTINUE_SEARCH;
}

static HWND createProbeWindow(void)
{
    WNDCLASSEXA wc;
    memset(&wc, 0, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.style = CS_OWNDC;
    wc.lpfnWndProc = DefWindowProcA;
    wc.hInstance = GetModuleHandleA(NULL);
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.lpszClassName = "client_sfx_probe_wnd";
    RegisterClassExA(&wc);
    HWND h = CreateWindowExA(0, "client_sfx_probe_wnd", "client_sfx_probe",
                             WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 1280, 720,
                             NULL, NULL, wc.hInstance, NULL);
    if (h != NULL) ShowWindow(h, SW_SHOW);
    return h;
}

// ---- direct SFX create test ------------------------------------------------
static void tryCreate(HMODULE eng, const char* tag, const wchar_t* wpath, void* owner)
{
    char path[512];
    gbk(wpath, path, sizeof(path));
    void* sfx = NULL;
    void* out = NULL;
    float mtx[16] = { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
    DWORD exc = 0;
    DWORD64 fault = 0;
    typedef void* (__fastcall *CreateFn)(void*, const char*, void*, void*, void*, void*, int, void*);
    CreateFn create = (CreateFn)((BYTE*)eng + 0xBE5610);
    __try { sfx = create(owner, path, NULL, NULL, NULL, mtx, 0, &out); }
    __except (exc = GetExceptionCode(),
              fault = (DWORD64)((PEXCEPTION_POINTERS)GetExceptionInformation())->ExceptionRecord->ExceptionAddress,
              EXCEPTION_EXECUTE_HANDLER) { sfx = NULL; }
    logf("%s: obj=%p exc=0x%08X fault_rva=0x%llX", tag, sfx, (unsigned)exc,
         (unsigned long long)(fault > (DWORD64)eng ? (fault - (DWORD64)eng) : 0));
    if (sfx != NULL)
    {
        __try
        {
            void** vt = *(void***)sfx;
            logf("   vtable=%p vt0=0x%llX", vt, (unsigned long long)((BYTE*)vt[0] - (BYTE*)eng));
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { logf("   vtable unreadable"); }
    }
}

// ---- tag-driven SFX create hook (engine-internal effect creation) ----------
static BYTE g_csSaved[48];
static BYTE* g_csTramp = NULL;
typedef void* (__fastcall *CreateSfxHookFn)(void*, const char*, void*, void*, void*, void*, int, void**);
static void* __fastcall hookCreateSfx(void* owner, const char* path, void* a3, void* a4,
                                      void* a5, void* mtx, int a6, void** out)
{
    void* r = ((CreateSfxHookFn)g_csTramp)(owner, path, a3, a4, a5, mtx, a6, out);
    if (path != NULL && (strstr(path, ".Sfx") != NULL || strstr(path, ".pss") != NULL))
    {
        printf("[TagSfx] create path=%s -> %p\n", path, r);
        fflush(stdout);
    }
    return r;
}

int main(void)
{
    static char g_rootA[MAX_PATH];
    if (GetEnvironmentVariableA("RC_PROBE_ROOT", g_rootA, MAX_PATH) == 0)
    {
        GetTempPathA(MAX_PATH, g_rootA);
        strcat_s(g_rootA, MAX_PATH, "opencode\\skillv2\\client_root");
    }
    wchar_t root[MAX_PATH];
    MultiByteToWideChar(CP_ACP, 0, g_rootA, -1, root, MAX_PATH);
    wchar_t bin64[MAX_PATH], dll[MAX_PATH];
    swprintf_s(bin64, MAX_PATH, L"%s\\bin64", root);
    swprintf_s(dll, MAX_PATH, L"%s\\X3DEngine.dll", bin64);

    SetCurrentDirectoryW(root);
    AddVectoredExceptionHandler(1, vehHandler);
    g_probeHwnd = createProbeWindow();
    logf("[window] hwnd=%p", g_probeHwnd);
    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    AddDllDirectory(bin64);

    // 1) facade boot (the game's entry)
    HMODULE x3d = LoadLibraryExW(dll, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!x3d) { logf("X3DEngine load failed err=%lu", GetLastError()); return 2; }
    typedef int (__cdecl *fn_void)(void);
    fn_void pre = (fn_void)GetProcAddress(x3d, "?PreInitX3DEngine@NSX3DEngine@@YAHXZ");
    fn_void load = (fn_void)GetProcAddress(x3d, "?LoadX3DEngine@NSX3DEngine@@YAHXZ");
    if (pre) logf("PreInitX3DEngine -> %d", pre());
    if (load) logf("LoadX3DEngine -> %d", load());

    // 2) adapter interface
    HMODULE adapter = GetModuleHandleA("KG3DEngineAdapterX64.dll");
    void* engIface = NULL;
    if (adapter != NULL)
    {
        typedef long (__cdecl *fn_out)(void** out);
        fn_out get3d = (fn_out)GetProcAddress(adapter, "Get3DEngineInterface");
        if (get3d) { long r = get3d(&engIface); logf("Get3DEngineInterface r=%d -> %p", (int)r, engIface); }
    }

    // 3) engine DLL + hooks (log capture, window, load-file mode)
    wchar_t engPath[MAX_PATH];
    swprintf_s(engPath, MAX_PATH, L"%s\\KG3DEngineDX11EX64.dll", bin64);
    HMODULE eng = LoadLibraryExW(engPath, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    logf("client engine load -> %p", eng);
    if (eng == NULL) return 3;
    {
        HMODULE kgc = GetModuleHandleA("KGCommonX64.dll");
        if (kgc != NULL)
        {
            void* realLog = GetProcAddress(kgc, "KG_PrintfLog");
            logf("KG_PrintfLog hooked engine=%d adapter=%d",
                 patchIat(eng, realLog, (void*)hookPrintfLog),
                 patchIat(adapter, realLog, (void*)hookPrintfLog));
        }
        int pc = installInlineHook(eng, 0x8AEF30, (void*)hookCreateTargetWindow,
                                   g_ctwSaved, &g_ctwTramp, 15);
        g_lfForceMode = 0;
        int plf = installInlineHook(eng, 0xB0F870, (void*)hookLoadFile, g_lfSaved, &g_lfTramp, 20);
        int pcs = installInlineHook(eng, 0xBE5610, (void*)hookCreateSfx, g_csSaved, &g_csTramp, 21);
        logf("hooks: CreateTargetWindow=%d LoadFile=%d CreateSfx=%d", pc, plf, pcs);
    }

    // 4) game file layer (Engine_Lua5X64): root + pak store
    {
        HMODULE lua = GetModuleHandleA("Engine_Lua5X64.dll");
        if (lua == NULL)
        {
            wchar_t lp[MAX_PATH];
            swprintf_s(lp, MAX_PATH, L"%s\\Engine_Lua5X64.dll", bin64);
            lua = LoadLibraryExW(lp, NULL,
                LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        }
        if (lua != NULL)
        {
            typedef void (__cdecl *SetRootFn)(const char*);
            ((SetRootFn)((BYTE*)lua + 0xB5400))(g_rootA);
            ((SetRootFn)((BYTE*)lua + 0xB5220))(g_rootA);
            typedef int (__cdecl *InitPakFn)(const char*, const char*, const char*,
                                             int, int, int, int, int, void*);
            int pr = ((InitPakFn)((BYTE*)lua + 0xCC2D0))(
                "C:/SeasunGame/Game/JX3/Pakv4", "Trunk.Dir", "", 0, 0, 0, 0, 0, (void*)"");
            logf("file layer: root set, KG_InitPakV4FileSystem -> %d", pr);
        }
    }

    // 5) create the engine (the game's call: iface->vt[0](0, 4))
    void* engine = NULL;
    if (engIface != NULL)
    {
        void** ivt = *(void***)engIface;
        logf("iface vtable=%p slot0=%p", ivt, ivt[0]);
        if (ivt == (void**)((BYTE*)adapter + 0x29FFF0))
        {
            typedef long (__fastcall *InitFn)(void* self, void* rdx, int r8d);
            long r = ((InitFn)ivt[0])(engIface, NULL, 4);
            logf("iface->vt[0](0,4) -> %ld", r);
        }
        typedef void* (__stdcall *GetEngine2Fn)(void);
        GetEngine2Fn ge2 = (GetEngine2Fn)GetProcAddress(eng, "KG3D_GetEngine2");
        engine = ge2 ? ge2() : NULL;
        logf("KG3D_GetEngine2 -> %p", engine);
    }

    // 6) scene + view (host setup the game does)
    void* scene = NULL;
    if (engine != NULL)
    {
        typedef long (__fastcall *CreateEmptySceneFn)(void* self, size_t size, void** out);
        ((CreateEmptySceneFn)((BYTE*)eng + 0x8AFAE0))(engine, 0, &scene);
        typedef void* (__fastcall *GetWinFn)(void* self);
        typedef void* (__fastcall *GetSceneFn)(void* self);
        void* window = ((GetWinFn)GetProcAddress(eng,
            "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ"))(engine);
        if (window != NULL && scene != NULL)
        {
            typedef long (__fastcall *CreateViewFn)(void*, void*, const char*, void*, void**, int);
            void* view = NULL;
            long vrc = ((CreateViewFn)((BYTE*)eng + 0x8AF3A0))(engine, scene, "probe_view", NULL, &view, 0);
            long arc = view ? ((long (__fastcall *)(void*, void*, int, int))
                ((BYTE*)eng + 0xA6B870))(window, view, 0, 1) : -1;
            logf("scene=%p view=%p (create=0x%08X add=0x%08X) Get3DScene2=%p", scene, view,
                 (unsigned)vrc, (unsigned)arc,
                 ((GetSceneFn)GetProcAddress(eng,
                    "?Get3DScene2@KG3D_Window@@UEAAPEAVKG3D_Scene@@XZ"))(window));
        }
    }

    // 7) actor from a real client model + engine animation controller + play
    if (engine != NULL)
    {
        // read-mode scan on the tani (hook bypass: call the original via trampoline directly)
        {
            char taniPath[512];
            char dirAni[64];
            gbk(L"动作", dirAni, sizeof(dirAni));
            sprintf_s(taniPath, sizeof(taniPath),
                      "data\\source\\player\\f1\\%s\\F1HA393_start01.tani", dirAni);
            for (int mode = 0; mode <= 12; mode++)
            {
                void* rd = ((LoadFileFn)g_lfTramp)(taniPath, mode);
                if (rd == NULL) { logf("  mode %d -> NULL", mode); continue; }
                __try
                {
                    BYTE* rb = (BYTE*)rd;
                    void* buf = *(void**)(rb + 0x10);
                    unsigned size = *(unsigned*)(rb + 0x18);
                    BYTE* bb = (BYTE*)buf;
                    logf("  mode %2d -> size=0x%X head=%c%c%c%c %02X %02X %02X %02X", mode, size,
                         (bb[0]>=32&&bb[0]<127)?bb[0]:'.',(bb[1]>=32&&bb[1]<127)?bb[1]:'.',
                         (bb[2]>=32&&bb[2]<127)?bb[2]:'.',(bb[3]>=32&&bb[3]<127)?bb[3]:'.',
                         bb[4],bb[5],bb[6],bb[7]);
                }
                __except (EXCEPTION_EXECUTE_HANDLER) { logf("  mode %d read fault", mode); }
            }
        }
        // API matrix: which game APIs read the tani, and do any decompress?
        {
            HMODULE lua = GetModuleHandleA("Engine_Lua5X64.dll");
            char taniPath[512];
            char dirAni[64];
            gbk(L"动作", dirAni, sizeof(dirAni));
            sprintf_s(taniPath, sizeof(taniPath),
                      "data\\source\\player\\f1\\%s\\F1HA393_start01.tani", dirAni);
            if (lua != NULL)
            {
                __try
                {
                    void* (*kof)(const char*) = (void* (*)(const char*))((BYTE*)lua + 0xC02E0);
                    void* buf = kof(taniPath);
                    logf("KG_OpenFile -> %p", buf);
                    if (buf != NULL)
                    {
                        BYTE* b = (BYTE*)buf;
                        logf("  q0=%016llX q1=%016llX q2=%016llX",
                             *(unsigned long long*)b, *(unsigned long long*)(b + 8),
                             *(unsigned long long*)(b + 0x10));
                        void* dp = *(void**)(b + 0x10);
                        unsigned sz = *(unsigned*)(b + 0x18);
                        if (dp != NULL)
                        {
                            BYTE* p = (BYTE*)dp;
                            logf("  +10=%p +18=0x%X head=%c%c%c%c %02X %02X %02X %02X", dp, sz,
                                 (p[0]>=32&&p[0]<127)?p[0]:'.',(p[1]>=32&&p[1]<127)?p[1]:'.',
                                 (p[2]>=32&&p[2]<127)?p[2]:'.',(p[3]>=32&&p[3]<127)?p[3]:'.',
                                 p[4],p[5],p[6],p[7]);
                        }
                    }
                }
                __except (EXCEPTION_EXECUTE_HANDLER) { logf("KG_OpenFile fault"); }
                for (int gf = 0; gf <= 1; gf++)
                {
                    __try
                    {
                        void* (*gof)(const char*, int, int) =
                            (void* (*)(const char*, int, int))((BYTE*)lua + 0xB2F50);
                        void* r = gof(taniPath, gf, 0);
                        logf("g_OpenFile(flag=%d) -> %p", gf, r);
                        if (r != NULL)
                        {
                            BYTE* p = (BYTE*)r;
                            logf("  q0=%016llX head=%c%c%c%c", *(unsigned long long*)p,
                                 (p[0]>=32&&p[0]<127)?p[0]:'.',(p[1]>=32&&p[1]<127)?p[1]:'.',
                                 (p[2]>=32&&p[2]<127)?p[2]:'.',(p[3]>=32&&p[3]<127)?p[3]:'.');
                        }
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER) { logf("g_OpenFile(flag=%d) fault", gf); }
                }
                __try
                {
                    void* (*kop)(const char*, int) =
                        (void* (*)(const char*, int))((BYTE*)lua + 0xCC670);
                    void* fo = kop(taniPath, 1);
                    logf("KG_OpenPakV4File(1) -> %p", fo);
                    if (fo != NULL)
                    {
                        void* inner = *(void**)((BYTE*)fo + 8);
                        if (inner != NULL)
                        {
                            typedef unsigned (__fastcall *SizeFn)(void*);
                            unsigned sz = ((SizeFn)(*(void***)inner)[4])(inner);
                            void* rec = ((void* (__fastcall *)(void*, void*, unsigned))
                                         (*(void***)inner)[2])(inner, NULL, 0);
                            logf("  inner=%p size=0x%X rec=%p", inner, sz, rec);
                        }
                    }
                }
                __except (EXCEPTION_EXECUTE_HANDLER) { logf("KG_OpenPakV4File fault"); }
                for (int m = 1; m <= 12; m++)
                {
                    __try
                    {
                        void* (*ld2)(const char*, int) =
                            (void* (*)(const char*, int))((BYTE*)eng + 0xB0FC60);
                        void* w = ld2(taniPath, m);
                        if (w == NULL) { logf("LD2 mode %2d -> NULL", m); continue; }
                        void* out = NULL;
                        typedef long (__fastcall *Wfn)(void*, int, void**);
                        long hr = ((Wfn)(*(void***)w)[4])(w, 0, &out);
                        logf("LD2 mode %2d -> w=%p vt4=0x%08X out=%p", m, w, (unsigned)hr, out);
                        if (out != NULL)
                        {
                            BYTE* o = (BYTE*)out;
                            logf("   out q0=%016llX q1=%016llX q2=%016llX q3=%016llX",
                                 *(unsigned long long*)o, *(unsigned long long*)(o+8),
                                 *(unsigned long long*)(o+0x10), *(unsigned long long*)(o+0x18));
                        }
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER) { logf("LD2 mode %2d fault", m); }
                }
            }
        }
        // wrapper OpenFile with a decompress flag: wrapper = Lua+0x1730B8
        {
            HMODULE lua = GetModuleHandleA("Engine_Lua5X64.dll");
            char taniPath[512];
            char dirAni[64];
            gbk(L"动作", dirAni, sizeof(dirAni));
            sprintf_s(taniPath, sizeof(taniPath),
                      "data\\source\\player\\f1\\%s\\F1HA393_start01.tani", dirAni);
            void* wrapper = lua ? *(void**)((BYTE*)lua + 0x1730B8) : NULL;
            logf("pak wrapper=%p", wrapper);
            if (wrapper != NULL)
            {
                for (int df = 0; df <= 1; df++)
                {
                    __try
                    {
                        typedef void* (__fastcall *OpenFn)(void* self, const char* path, void* a3, int flag);
                        void* f = ((OpenFn)(*(void***)wrapper)[2])(wrapper, taniPath, NULL, df);
                        logf("wrapper OpenFile(decompress=%d) -> %p", df, f);
                        if (f != NULL)
                        {
                            void* inner = *(void**)((BYTE*)f + 8);
                            if (inner != NULL)
                            {
                                typedef void* (__fastcall *GetDataFn2)(void* self, void*, unsigned);
                                void* data = ((GetDataFn2)(*(void***)inner)[2])(inner, NULL, 0);
                                if (data != NULL)
                                {
                                    BYTE* dp = (BYTE*)data;
                                    logf("   record head: %c%c%c%c %02X %02X %02X %02X", 
                                         (dp[0]>=32&&dp[0]<127)?dp[0]:'.',(dp[1]>=32&&dp[1]<127)?dp[1]:'.',
                                         (dp[2]>=32&&dp[2]<127)?dp[2]:'.',(dp[3]>=32&&dp[3]<127)?dp[3]:'.',
                                         dp[4],dp[5],dp[6],dp[7]);
                                }
                            }
                        }
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER) { logf("wrapper OpenFile(%d) fault", df); }
                }
            }
        }
        typedef long (__fastcall *CreateActorFn)(void* self, const char* path, void* a3,
                                                 void** out, unsigned flags, void* opts);
        char mpath[512];
        gbk(L"data\\source\\player\\f1\\部件\\f1_3094_body_hd.mesh", mpath, sizeof(mpath));
        void* actor = NULL;
        long mrc = ((CreateActorFn)((BYTE*)eng + 0x8B2DA0))(engine, mpath, NULL, &actor, 0, NULL);
        logf("CreateActorFromFile(mesh) -> rc=0x%08X actor=%p", (unsigned)mrc, actor);
        if (actor != NULL)
        {
            void* model = *(void**)((BYTE*)actor + 0x358);
            logf("actor+0x358 model=%p", model);
            if (model != NULL)
            {
                typedef void* (__fastcall *AllocFn)(void*, size_t, size_t);
                void* ctrl = ((AllocFn)((BYTE*)eng + 0xB0F300))(NULL, 0x3A8, 8);
                ((void (__fastcall *)(void*))((BYTE*)eng + 0xBC1100))(ctrl);
                long sar = ((long (__fastcall *)(void*, void*))((BYTE*)eng + 0xBC2120))(ctrl, actor);
                logf("controller=%p SetActor=0x%08X", ctrl, (unsigned)sar);
                char taniPath[512];
                char dirAni[64];
                gbk(L"动作", dirAni, sizeof(dirAni));
                sprintf_s(taniPath, sizeof(taniPath),
                          "data\\source\\player\\f1\\%s\\F1HA393_start01.tani", dirAni);
                typedef long (__fastcall *CreateAnimFn)(void*, const char*, void**, int, int);
                void* anim = NULL;
                {
                    wchar_t ip[MAX_PATH];
                    wchar_t rootW[MAX_PATH];
                    MultiByteToWideChar(CP_ACP, 0, g_rootA, -1, rootW, MAX_PATH);
                    swprintf_s(ip, MAX_PATH, L"%s\\bin64\\KIndexpackX64.dll", rootW);
                    HMODULE ipk = LoadLibraryExW(ip, NULL,
                        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                    logf("KIndexpackX64.dll -> %p (err=%u)", ipk, ipk ? 0 : GetLastError());
                    if (ipk != NULL)
                    {
                        typedef long (__cdecl *InitIpmFn)(void*, void*);
                        InitIpmFn initIpm = (InitIpmFn)GetProcAddress(ipk, "g_InitIndexPackManager");
                        long ihr = (initIpm != NULL) ? initIpm(NULL, NULL) : -1;
                        logf("g_InitIndexPackManager -> 0x%08X", (unsigned)ihr);
                        void* sing = *(void**)((BYTE*)ipk + 0x3D220);
                        typedef long (__cdecl *GetReaderFn)(void**);
                        GetReaderFn gr = (GetReaderFn)GetProcAddress(ipk, "g_GetIndexPackageReader");
                        void* rout = NULL;
                        long rhr = (gr != NULL) ? gr(&rout) : -1;
                        logf("index singleton=%p g_GetIndexPackageReader -> 0x%08X out=%p",
                             sing, (unsigned)rhr, rout);
                    }
                }
                void* mm = ((void* (__cdecl *)(const char*))((BYTE*)eng + 0xB0F720))(g_rootA);
                logf("mode manager -> %p", mm);
                {
                    void* wrapper = *(void**)((BYTE*)eng + 0x2D22598);
                    void* inner = (wrapper != NULL) ? *(void**)((BYTE*)wrapper + 0x10) : NULL;
                    void* cb = (inner != NULL) ? *(void**)((BYTE*)inner + 0x160) : NULL;
                    void* mod = (inner != NULL) ? *(void**)((BYTE*)inner + 0x168) : NULL;
                    void* out = NULL;
                    long hr = (cb != NULL) ? ((long (__fastcall *)(void**))cb)(&out) : -1;
                    void* mapper = NULL;
                    if (wrapper != NULL)
                        mapper = ((void* (__fastcall *)(void*))(((void***)wrapper)[0][27]))(wrapper);
                    long mc = -1;
                    if (mapper != NULL)
                        mc = ((long (__fastcall *)(void*, const char*, void*, void*))
                              (((void***)mapper)[0][1]))(mapper, taniPath, NULL, NULL);
                    logf("mm: wrapper=%p inner=%p cb=%p mod=%p hr=0x%08X out=%p mapper=%p check=%ld",
                         wrapper, inner, cb, mod, (unsigned)hr, out, mapper, mc);
                }
                long arc = -1;
                {
                    typedef long (__fastcall *InitAttachTaniFn)(void* actor, const char* path,
                                                               unsigned flags);
                    long iat = ((InitAttachTaniFn)((BYTE*)eng + 0x83A6F0))(actor, taniPath, 0);
                    void* a2 = *(void**)((BYTE*)actor + 0x368);
                    logf("_InitAttachTani -> rc=0x%08X actor+368=%p", (unsigned)iat, a2);
                    __try
                    {
                        void** singleton = *(void***)((BYTE*)eng + 0x2CF1038);
                        void* mgr = ((void* (__fastcall *)(void*))
                                     ((*(void***)singleton)[0x460 / 8]))(singleton);
                        logf("  attach mgr=%p", mgr);
                        if (mgr != NULL)
                        {
                            void* desc = ((void* (__fastcall *)(void*, const char*, int))
                                          ((*(void***)mgr)[0x60 / 8]))(mgr, taniPath, 0);
                            logf("  desc=%p", desc);
                            if (desc != NULL)
                            {
                                const char* rp = ((const char* (__fastcall *)(void*))
                                                  ((*(void***)desc)[3]))(desc);
                                logf("  resolved='%s'", rp ? rp : "(null)");
                            }
                        }
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER) { logf("  attach resolve fault"); }
                    if (a2 != NULL) { arc = 0; anim = a2; }
                }
                logf("CreateAnimationFromFile(a4=0,a5=0) -> rc=0x%08X anim=%p", (unsigned)arc, anim);
                if (anim != NULL)
                {
                    typedef long (__fastcall *StartAnimFn)(void*, void*, int, float,
                        unsigned, unsigned, void*, void*, void*);
                    long src = ((StartAnimFn)((BYTE*)eng + 0xBC1C70))(ctrl, anim, 0, 1.0f, 0, 0, NULL, NULL, NULL);
                    logf("StartAnimation -> rc=0x%08X", (unsigned)src);
                    typedef long (__fastcall *FrameMoveFn)(void*);
                    FrameMoveFn fm = (FrameMoveFn)((BYTE*)eng + 0xBC2620);
                    for (int f = 0; f < 120; f++)
                    {
                        long frc = fm(ctrl);
                        if (f == 0 || f == 59 || f == 119) logf("ctrl FrameMove[%d] -> 0x%08X", f, (unsigned)frc);
                        Sleep(16);
                    }
                    logf("animation frames done");
                }
            }
        }
    }

    // 8) direct .Sfx create tests with the engine singleton owner
    if (engine != NULL)
    {
        void* owner = NULL;
        __try
        {
            void** g = *(void***)((BYTE*)eng + 0x2CF1038);
            if (g != NULL) owner = ((void* (__fastcall *)(void*))((*(void***)g)[8]))(g);
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { }
        logf("owner chain -> %p", owner);
        tryCreate(eng, "existing .Sfx", L"data\\source\\other\\特效\\技能\\SFX\\增益\\c纯阳坐忘.Sfx", owner);
        tryCreate(eng, "pss", L"data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策撼如雷02_重制.pss", owner);
    }

    logf("probe done");
    return 0;
}


