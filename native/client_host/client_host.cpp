// client_host.cpp — Phase 3 host core: boot the client engine stack the way the
// game does and drive the engine's own actor/animation/SFX path.
//
// Proves in one process:
//   boot (facade -> adapter -> file layer -> engine, host window via the
//   CreateTargetWindow hook) -> scene (+ optional map) -> view -> actor from a
//   PakV4 model -> authored animation (Actor::_InitAttachTani -> StartAnimation
//   -> FrameMove) -> real .Sfx created by the engine -> screenshot.
//
// Runtime root: RC_HOST_ROOT (default %TEMP%\opencode\skillv2\client_root).
// Map (optional): RC_HOST_MAP (VFS path or absolute jsonmap path).
// Screenshot: RC_HOST_SHOT (png path), default <root>\host_shot.png.
#include <windows.h>
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

static int installInlineHook(HMODULE mod, DWORD rva, void* hook, BYTE* saved,
                             BYTE** trampOut, int len)
{
    BYTE* target = (BYTE*)mod + rva;
    if (len < 12) len = 12;
    memcpy(saved, target, len);
    BYTE* tramp = (BYTE*)VirtualAlloc(NULL, 64, MEM_COMMIT | MEM_RESERVE,
                                      PAGE_EXECUTE_READWRITE);
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

static HWND g_hostHwnd = NULL;
static BYTE g_ctwSaved[32];
static BYTE* g_ctwTramp = NULL;

typedef long (__fastcall *CreateTargetWindowFn)(void* self, void* hwnd, void** out);
static long __fastcall hookCreateTargetWindow(void* self, void* hwnd, void** out)
{
    logf("[host] CreateTargetWindow hwnd=%p -> %p", hwnd, g_hostHwnd);
    if (g_hostHwnd != NULL) hwnd = g_hostHwnd;
    return ((CreateTargetWindowFn)g_ctwTramp)(self, hwnd, out);
}

// Loose sandbox map: the client engine's landscape loaders verify regioninfo
// through the file-mode wrapper, whose existence check does not see loose
// files. Hiding the baked heightmap (.bch) makes the engine pick its own
// source-format loader (the same family the ME engine uses).
static BYTE g_lfSaved[32];
static BYTE* g_lfTramp = NULL;
typedef void* (__fastcall *LoadFileFn)(const char* path, int flags);
static void* __fastcall hookLoadFileSrc(const char* path, int flags)
{
    if (path != NULL && strstr(path, "heightmap_bc") != NULL)
    {
        printf("[host] loose map -> source loader (hide %s)\n", path);
        fflush(stdout);
        return NULL;
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

// Loose-file bridge for the engine's file-mode wrapper (KG3D_StdFileSystem at
// engine+0x2D22598). The landscape loaders verify regioninfo through the
// wrapper's existence predicates (vt[8] name / vt[9] hash), which only know
// pak-indexed files; the sandbox map is loose. The bridge tries the original
// first, then checks the engine root on disk (host file-layer adaptation,
// registered in docs/EXPERIENCES.md).
static char g_rootA[MAX_PATH];
static long (__fastcall *g_wrapOrig8)(void*, const char*) = NULL;
static long (__fastcall *g_wrapOrig9)(void*, const char*) = NULL;

static long looseExists(const char* path)
{
    if (path == NULL || path[0] == 0) return 0;
    char full[MAX_PATH * 2];
    strcpy_s(full, sizeof(full), g_rootA);
    strcat_s(full, sizeof(full), "\\");
    strcat_s(full, sizeof(full), path);
    for (char* p = full; *p != 0; p++)
        if (*p == '/') *p = '\\';
    return (GetFileAttributesA(full) != INVALID_FILE_ATTRIBUTES) ? 1 : 0;
}

static long __fastcall wrapExistsName(void* self, const char* path)
{
    if (g_wrapOrig8 != NULL && g_wrapOrig8(self, path) != 0) return 1;
    long r = looseExists(path);
    if (r != 0) { printf("[host] wrapper loose-exists: %s\n", path); fflush(stdout); }
    return r;
}

static long __fastcall wrapExistsHash(void* self, const char* path)
{
    if (g_wrapOrig9 != NULL && g_wrapOrig9(self, path) != 0) return 1;
    long r = looseExists(path);
    if (r != 0) { printf("[host] wrapper loose-exists(hash): %s\n", path); fflush(stdout); }
    return r;
}

static LONG WINAPI vehHandler(PEXCEPTION_POINTERS ep)
{
    if (ep->ExceptionRecord->ExceptionCode == 0xC0000005 ||
        ep->ExceptionRecord->ExceptionCode == 0xC0000409)
        logf("[VEH] exc=0x%08X at=%p", ep->ExceptionRecord->ExceptionCode,
             ep->ExceptionRecord->ExceptionAddress);
    return EXCEPTION_CONTINUE_SEARCH;
}

static HWND createHostWindow(void)
{
    WNDCLASSEXA wc;
    memset(&wc, 0, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.style = CS_OWNDC;
    wc.lpfnWndProc = DefWindowProcA;
    wc.hInstance = GetModuleHandleA(NULL);
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.lpszClassName = "reborn_skill_host_wnd";
    RegisterClassExA(&wc);
    HWND h = CreateWindowExA(0, "reborn_skill_host_wnd", "sandbox-skillhost",
                             WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT,
                             1280, 720, NULL, NULL, wc.hInstance, NULL);
    if (h != NULL) ShowWindow(h, SW_SHOW);
    return h;
}

int main(void)
{
    char rootA[MAX_PATH];
    if (GetEnvironmentVariableA("RC_HOST_ROOT", rootA, MAX_PATH) == 0)
    {
        GetTempPathA(MAX_PATH, rootA);
        strcat_s(rootA, MAX_PATH, "opencode\\skillv2\\client_root");
    }
    wchar_t root[MAX_PATH], bin64[MAX_PATH], dll[MAX_PATH];
    MultiByteToWideChar(CP_ACP, 0, rootA, -1, root, MAX_PATH);
    strcpy_s(g_rootA, MAX_PATH, rootA);
    swprintf_s(bin64, MAX_PATH, L"%s\\bin64", root);
    swprintf_s(dll, MAX_PATH, L"%s\\X3DEngine.dll", bin64);

    AddVectoredExceptionHandler(1, vehHandler);
    SetCurrentDirectoryW(root);
    g_hostHwnd = createHostWindow();
    logf("[host] window=%p root=%s", g_hostHwnd, rootA);
    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    AddDllDirectory(bin64);

    HMODULE x3d = LoadLibraryExW(dll, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (x3d == NULL) { logf("[host] X3DEngine load failed err=%lu", GetLastError()); return 2; }
    typedef int (__cdecl *fn_void)(void);
    ((fn_void)GetProcAddress(x3d, "?PreInitX3DEngine@NSX3DEngine@@YAHXZ"))();
    ((fn_void)GetProcAddress(x3d, "?LoadX3DEngine@NSX3DEngine@@YAHXZ"))();

    HMODULE adapter = GetModuleHandleA("KG3DEngineAdapterX64.dll");
    void* engIface = NULL;
    if (adapter != NULL)
    {
        typedef long (__cdecl *fn_out)(void** out);
        fn_out get3d = (fn_out)GetProcAddress(adapter, "Get3DEngineInterface");
        if (get3d) get3d(&engIface);
    }

    wchar_t engPath[MAX_PATH];
    swprintf_s(engPath, MAX_PATH, L"%s\\KG3DEngineDX11EX64.dll", bin64);
    HMODULE eng = LoadLibraryExW(engPath, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    logf("[host] engine=%p iface=%p", eng, engIface);
    if (eng == NULL) return 3;

    int hookOk = installInlineHook(eng, 0x8AEF30, (void*)hookCreateTargetWindow,
                                   g_ctwSaved, &g_ctwTramp, 15);
    int lfOk = installInlineHook(eng, 0xB0F870, (void*)hookLoadFileSrc,
                                 g_lfSaved, &g_lfTramp, 20);
    logf("[host] hooks: window=%d loadfile=%d", hookOk, lfOk);
    {
        HMODULE kgc = GetModuleHandleA("KGCommonX64.dll");
        if (kgc != NULL)
        {
            void* realLog = GetProcAddress(kgc, "KG_PrintfLog");
            logf("[host] KG_PrintfLog hooked engine=%d adapter=%d",
                 patchIat(eng, realLog, (void*)hookPrintfLog),
                 patchIat(adapter, realLog, (void*)hookPrintfLog));
        }
    }

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
            ((SetRootFn)((BYTE*)lua + 0xB5400))(rootA);
            ((SetRootFn)((BYTE*)lua + 0xB5220))(rootA);
            typedef int (__cdecl *InitPakFn)(const char*, const char*, const char*,
                                             int, int, int, int, int, void*);
            int pr = ((InitPakFn)((BYTE*)lua + 0xCC2D0))(
                "C:/SeasunGame/Game/JX3/Pakv4", "Trunk.Dir", "", 0, 0, 0, 0, 0, (void*)"");
            logf("[host] file layer init=%d", pr);
        }
    }

    void* engine = NULL;
    if (engIface != NULL)
    {
        void** ivt = *(void***)engIface;
        if (ivt == (void**)((BYTE*)adapter + 0x29FFF0))
        {
            typedef long (__fastcall *InitFn)(void* self, void* rdx, int r8d);
            long r = ((InitFn)ivt[0])(engIface, NULL, 4);
            logf("[host] iface->vt[0](0,4) -> %ld", r);
        }
        typedef void* (__stdcall *GetEngine2Fn)(void);
        engine = ((GetEngine2Fn)GetProcAddress(eng, "KG3D_GetEngine2"))();
    }
    logf("[host] engine instance=%p", engine);
    if (engine == NULL) return 4;

    // engine file-mode manager (0x2D22598): created the way the game does
    // (KIndexpack init + the engine's own lazy getter).
    {
        wchar_t ip[MAX_PATH];
        swprintf_s(ip, MAX_PATH, L"%s\\KIndexpackX64.dll", bin64);
        HMODULE ipk = LoadLibraryExW(ip, NULL,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        logf("[host] KIndexpackX64.dll -> %p", ipk);
        if (ipk != NULL)
        {
            typedef long (__cdecl *InitIpmFn)(void*, void*);
            InitIpmFn initIpm = (InitIpmFn)GetProcAddress(ipk, "g_InitIndexPackManager");
            if (initIpm != NULL)
                logf("[host] g_InitIndexPackManager -> 0x%08X", (unsigned)initIpm(NULL, NULL));
        }
        typedef void* (__cdecl *GetModeMgrFn)(const char*);
        void* mm = ((GetModeMgrFn)((BYTE*)eng + 0xB0F720))(rootA);
        void* wrapper = *(void**)((BYTE*)eng + 0x2D22598);
        logf("[host] file-mode manager ret=%p wrapper=%p", mm, wrapper);
        if (wrapper != NULL)
        {
            void** wvt = *(void***)wrapper;
            DWORD oldp = 0;
            if (VirtualProtect(wvt, 16 * sizeof(void*), PAGE_EXECUTE_READWRITE, &oldp))
            {
                g_wrapOrig8 = (long (__fastcall *)(void*, const char*))wvt[8];
                g_wrapOrig9 = (long (__fastcall *)(void*, const char*))wvt[9];
                wvt[8] = (void*)wrapExistsName;
                wvt[9] = (void*)wrapExistsHash;
                VirtualProtect(wvt, 16 * sizeof(void*), oldp, &oldp);
                FlushInstructionCache(GetCurrentProcess(), wvt, 16 * sizeof(void*));
                logf("[host] wrapper loose bridge installed (orig8=%p orig9=%p)",
                     (void*)g_wrapOrig8, (void*)g_wrapOrig9);
            }
        }
    }

    // scene: try the map from RC_HOST_MAP, fall back to an empty scene
    void* scene = NULL;
    char mapPath[MAX_PATH];
    if (GetEnvironmentVariableA("RC_HOST_MAP", mapPath, MAX_PATH) != 0)
    {
        typedef long (__fastcall *SceneFromSourceFn)(void*, const char*, unsigned long long,
                                                     void*, void**);
        long src = ((SceneFromSourceFn)((BYTE*)eng + 0x8B03E0))(engine, mapPath, 0, NULL, &scene);
        logf("[host] CreateSceneFromSource('%s') -> 0x%08X scene=%p",
             mapPath, (unsigned)src, scene);
    }
    if (scene == NULL)
    {
        typedef long (__fastcall *CreateEmptySceneFn)(void*, size_t, void**);
        ((CreateEmptySceneFn)((BYTE*)eng + 0x8AFAE0))(engine, 0, &scene);
        logf("[host] CreateEmptyScene -> %p", scene);
    }
    if (scene == NULL) return 5;

    typedef void* (__fastcall *GetWinFn)(void* self);
    void* window = ((GetWinFn)GetProcAddress(eng,
        "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ"))(engine);
    void* view = NULL;
    if (window != NULL)
    {
        typedef long (__fastcall *CreateViewFn)(void*, void*, const char*, void*, void**, int);
        const char* viewArg = (mapPath[0] != 0) ? NULL : "host_view";
        long vrc = ((CreateViewFn)((BYTE*)eng + 0x8AF3A0))(engine, scene, viewArg,
                                                           NULL, &view, 0);
        long arc = view ? ((long (__fastcall *)(void*, void*, int, int))
                           ((BYTE*)eng + 0xA6B870))(window, view, 0, 1) : -1;
        long crc = (view != NULL) ? ((long (__fastcall *)(void*, void*, int))
                      ((BYTE*)eng + 0xA74BB0))(window, view, 0) : -1;
        long aw = (window != NULL) ? ((long (__fastcall *)(void*, void*))
                      ((BYTE*)eng + 0x8C7300))(engine, window) : -1;
        logf("[host] window=%p view=%p arg=%s (create=0x%08X add=0x%08X camera=0x%08X active=0x%08X)",
             window, view, viewArg ? viewArg : "(null)",
             (unsigned)vrc, (unsigned)arc, (unsigned)crc, (unsigned)aw);

        // camera pose from the sandbox spawn (Y-up), via the view's camera
        if (view != NULL)
        {
            __try
            {
                void** vvt = *(void***)view;
                void* camera = NULL;
                long grc = ((long (__fastcall *)(void*, void**))vvt[10])(view, &camera);
                float pose[9] = {
                    23334.0f, 761.0f + 6.0f, 24224.0f + 16.0f,
                    23334.0f, 761.0f + 1.5f, 24224.0f,
                    0.0f, 1.0f, 0.0f
                };
                long prc = -1;
                if (camera != NULL)
                    prc = ((long (__fastcall *)(void*, float*, int))
                           ((BYTE*)eng + 0xB36540))(camera, pose, 0);
                logf("[host] view camera=%p get=0x%08X SetPose=0x%08X",
                     camera, (unsigned)grc, (unsigned)prc);
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] camera setup fault"); }
        }
    }

    // actor from a PakV4 model, placed at the sandbox spawn via the create
    // options (4x4 row-major XMFLOAT4X4 translation at indices 12/13/14)
    typedef long (__fastcall *CreateActorFn)(void*, const char*, void*, void**,
                                             unsigned, void*);
    char mpath[512];
    gbk(L"data\\source\\player\\f1\\部件\\f1_3094_body_hd.mesh", mpath, sizeof(mpath));
    float actorMtx[16] = {
        1,0,0,0, 0,1,0,0, 0,0,1,0,
        23334.0f, 761.0f, 24224.0f, 1.0f
    };
    void* actor = NULL;
    long mrc = ((CreateActorFn)((BYTE*)eng + 0x8B2DA0))(engine, mpath, NULL, &actor, 0,
                                                        actorMtx);
    logf("[host] CreateActorFromFile -> 0x%08X actor=%p", (unsigned)mrc, actor);

    // authored animation on the engine's own attach path + controller
    void* ctrl = NULL;
    if (actor != NULL)
    {
        char dirAni[64], taniPath[512];
        gbk(L"动作", dirAni, sizeof(dirAni));
        sprintf_s(taniPath, sizeof(taniPath),
                  "data\\source\\player\\f1\\%s\\F1HA393_start01.tani", dirAni);
        typedef long (__fastcall *InitAttachTaniFn)(void*, const char*, unsigned);
        long iat = ((InitAttachTaniFn)((BYTE*)eng + 0x83A6F0))(actor, taniPath, 0);
        void* anim = *(void**)((BYTE*)actor + 0x368);
        logf("[host] _InitAttachTani -> 0x%08X anim=%p", (unsigned)iat, anim);

        typedef void* (__fastcall *AllocFn)(void*, size_t, size_t);
        ctrl = ((AllocFn)((BYTE*)eng + 0xB0F300))(NULL, 0x3A8, 8);
        ((void (__fastcall *)(void*))((BYTE*)eng + 0xBC1100))(ctrl);
        long sar = ((long (__fastcall *)(void*, void*))((BYTE*)eng + 0xBC2120))(ctrl, actor);
        logf("[host] controller=%p SetActor=0x%08X", ctrl, (unsigned)sar);
        if (anim != NULL)
        {
            typedef long (__fastcall *StartAnimFn)(void*, void*, int, float,
                unsigned, unsigned, void*, void*, void*);
            long src = ((StartAnimFn)((BYTE*)eng + 0xBC1C70))(ctrl, anim, 0, 1.0f,
                0, 0, NULL, NULL, NULL);
            logf("[host] StartAnimation -> 0x%08X", (unsigned)src);
        }
    }

    // real .Sfx through the engine's own factory (client build: no AV)
    {
        void* owner = NULL;
        void** g = *(void***)((BYTE*)eng + 0x2CF1038);
        if (g != NULL) owner = ((void* (__fastcall *)(void*))((*(void***)g)[8]))(g);
        char sfxPath[512];
        gbk(L"data\\source\\other\\特效\\技能\\SFX\\增益\\c纯阳坐忘.Sfx",
            sfxPath, sizeof(sfxPath));
        void* sfx = NULL;
        void* out = NULL;
        float mtx[16] = { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
        DWORD exc = 0;
        typedef void* (__fastcall *CreateFn)(void*, const char*, void*, void*, void*,
                                             void*, int, void*);
        __try { sfx = ((CreateFn)((BYTE*)eng + 0xBE5610))(owner, sfxPath, NULL, NULL,
                                                         NULL, mtx, 0, &out); }
        __except (exc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { sfx = NULL; }
        logf("[host] .Sfx create -> obj=%p exc=0x%08X owner=%p", sfx, (unsigned)exc, owner);
    }

    // frame loop: controller + engine FrameMove + window paint
    void* camObj = NULL;
    {
        __try
        {
            if (view != NULL)
            {
                void** vvt = *(void***)view;
                ((long (__fastcall *)(void*, void**))vvt[10])(view, &camObj);
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { camObj = NULL; }
    }
    logf("[host] frame camera=%p", camObj);
    {
        typedef long (__fastcall *FrameMoveFn)(void*);
        FrameMoveFn ctrlFm = (FrameMoveFn)((BYTE*)eng + 0xBC2620);
        FrameMoveFn engFm = (FrameMoveFn)((BYTE*)eng + 0x8C6330);
        typedef long (__fastcall *PaintFn)(void*);
        typedef long (__fastcall *PaintViewFn)(void*, void*);
        PaintFn beginPaint = (PaintFn)((BYTE*)eng + 0xA6C6E0);
        PaintViewFn beginView = (PaintViewFn)((BYTE*)eng + 0xA6C1B0);
        PaintViewFn endView = (PaintViewFn)((BYTE*)eng + 0xA6C480);
        PaintFn endPaint = (PaintFn)((BYTE*)eng + 0xA6FCD0);
        float camPose[9] = {
            23334.0f, 761.0f + 2.2f, 24224.0f - 6.0f,
            23334.0f, 761.0f + 1.2f, 24224.0f,
            0.0f, 1.0f, 0.0f
        };
        for (int f = 0; f < 240; f++)
        {
            if (ctrl != NULL) ctrlFm(ctrl);
            if (f < 8) engFm(engine);
            if (camObj != NULL)
                ((long (__fastcall *)(void*, float*, int))((BYTE*)eng + 0xB36540))(camObj, camPose, 0);
            if (window != NULL && view != NULL)
            {
                __try
                {
                    beginPaint(window);
                    beginView(window, view);
                    endView(window, view);
                    endPaint(window);
                }
                __except (EXCEPTION_EXECUTE_HANDLER)
                {
                    logf("[host] paint fault at frame %d", f);
                    break;
                }
            }
            Sleep(16);
        }
        logf("[host] frame loop done");
    }

    // screenshot through the engine window API
    if (window != NULL)
    {
        char shot[MAX_PATH];
        if (GetEnvironmentVariableA("RC_HOST_SHOT", shot, MAX_PATH) == 0)
            sprintf_s(shot, MAX_PATH, "%s\\host_shot.png", rootA);
        long sr = ((long (__fastcall *)(void*, const char*, int, int, int))
                   ((BYTE*)eng + 0xA7C5E0))(window, shot, 2, 0, 0);
        long dr = ((long (__fastcall *)(void*))((BYTE*)eng + 0xA7C750))(window);
        logf("[host] screenshot set=0x%08X do=0x%08X -> %s", (unsigned)sr, (unsigned)dr, shot);
        Sleep(1500);
    }

    logf("[host] done");
    return 0;
}

