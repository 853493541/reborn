// client_sfx_probe.cpp — test the CLIENT engine's KG3D_CreateSFXFromFile on a real
// .Sfx, read-only, no editor shell. Boots X3DEngine (PreInit/Load), gets the engine
// interface from the client adapter, then calls the client's create with GBK paths.
#include <windows.h>
#include <intrin.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <stdarg.h>

extern "C" __declspec(dllimport) unsigned short __stdcall RtlCaptureStackBackTrace(
    unsigned long FramesToSkip, unsigned long FramesToCapture, void** BackTrace, unsigned long* BackTraceHash);

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

typedef HANDLE (WINAPI *CreateFileWFn)(LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
static CreateFileWFn g_realCreateFileW = NULL;
static HANDLE WINAPI hookCreateFileW(LPCWSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES sa, DWORD disp, DWORD flags, HANDLE tmpl)
{
    HANDLE h = g_realCreateFileW(name, access, share, sa, disp, flags, tmpl);
    if (name != NULL && wcsstr(name, L"aterial") != NULL)
    {
        wprintf(L"[CreateFileW] %s -> %p\n", name, h);
        fflush(stdout);
    }
    return h;
}

typedef HANDLE (WINAPI *CreateFileAFn)(LPCSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
static CreateFileAFn g_realCreateFileA = NULL;
static HANDLE WINAPI hookCreateFileA(LPCSTR name, DWORD access, DWORD share,
                                     LPSECURITY_ATTRIBUTES sa, DWORD disp, DWORD flags, HANDLE tmpl)
{
    HANDLE h = g_realCreateFileA(name, access, share, sa, disp, flags, tmpl);
    if (name != NULL && (strstr(name, "aterial") != NULL || strstr(name, "Shader") != NULL || strstr(name, "hlsl") != NULL))
    {
        printf("[CreateFileA] %s -> %p\n", name, h);
        fflush(stdout);
    }
    return h;
}

static BYTE g_fsSaved[24];
static BYTE* g_fsTramp = NULL;

typedef long (__fastcall *FsCreateFn)(const char* root);
static long __fastcall hookFsCreate(const char* root)
{
    printf("[FsCreate] root='%s' caller=%p\n", root ? root : "(null)", _ReturnAddress());
    fflush(stdout);
    return ((FsCreateFn)g_fsTramp)(root);
}

static BYTE g_ceSaved[24];
static BYTE* g_ceTramp = NULL;

typedef long (__fastcall *CreateEngineFn)(void* param, void** out);
static long __fastcall hookCreateEngine(void* param, void** out)
{
    __try
    {
        printf("[CreateEngine] param=%p out=%p root='%s' fs@param+8=%p\n",
               param, out, (char*)((BYTE*)param + 0xeec), *(void**)((BYTE*)param + 8));
        fflush(stdout);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { printf("[CreateEngine] param read fault\n"); fflush(stdout); }
    return ((CreateEngineFn)g_ceTramp)(param, out);
}

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

static int installFsHook(HMODULE eng, DWORD rva)
{
    return installInlineHook(eng, rva, (void*)hookFsCreate, g_fsSaved, &g_fsTramp, 17);
}

static void logModules(const char* tag);

// ---- host window + CreateTargetWindow HWND substitution --------------------
static HWND g_probeHwnd = NULL;
static BYTE g_ctwSaved[32];
static BYTE* g_ctwTramp = NULL;

typedef long (__fastcall *CreateTargetWindowFn)(void* self, void* hwnd, void** out);
static long __fastcall hookCreateTargetWindow(void* self, void* hwnd, void** out)
{
    printf("[CreateTargetWindow] self=%p hwnd=%p -> using %p\n", self, hwnd, g_probeHwnd);
    fflush(stdout);
    if (g_probeHwnd != NULL) hwnd = g_probeHwnd;
    return ((CreateTargetWindowFn)g_ctwTramp)(self, hwnd, out);
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
    if (!RegisterClassExA(&wc) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
        printf("[window] RegisterClassEx failed err=%lu\n", GetLastError());
    HWND h = CreateWindowExA(0, "client_sfx_probe_wnd", "client_sfx_probe",
                             WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 1280, 720,
                             NULL, NULL, wc.hInstance, NULL);
    printf("[window] CreateWindowExA -> %p err=%lu\n", h, GetLastError());
    if (h != NULL) ShowWindow(h, SW_SHOW);
    return h;
}

static BYTE g_iwSaved[24];
static BYTE* g_iwTramp = NULL;

static void __cdecl hookInvokeWatson(const wchar_t* expr, const wchar_t* func, unsigned line, unsigned long long reserved)
{
    printf("[WATSON] expr=%ls func=%ls line=%u caller=%p\n",
           expr ? expr : L"(null)", func ? func : L"(null)", line, _ReturnAddress());
    fflush(stdout);
    // do not call the original - it fast-fails; just hang for stack capture
    for (;;) Sleep(1000);
}

static LONG WINAPI vehHandler(PEXCEPTION_POINTERS ep)
{
    if (true)
    {
        printf("[VEH] code=0x%08X at=%p\n", (unsigned)ep->ExceptionRecord->ExceptionCode,
               ep->ExceptionRecord->ExceptionAddress);
        void* frames[24];
        unsigned short nf = RtlCaptureStackBackTrace(0, 24, frames, NULL);
        for (unsigned short i = 0; i < nf; i++)
            printf("[VEH]   frame[%u]=%p\n", (unsigned)i, frames[i]);
        logModules("at-veh");
        fflush(stdout);
    }
    return EXCEPTION_CONTINUE_SEARCH;
}

static int patchLogIat(HMODULE mod, void* realFn, void* replacement)
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

static void logModules(const char* tag)
{
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, 0);
    if (snap == INVALID_HANDLE_VALUE) { printf("[mods %s] snapshot failed\n", tag); return; }
    MODULEENTRY32 me;
    me.dwSize = sizeof(me);
    if (Module32First(snap, &me))
    {
        do
        {
            const char* n = me.szModule;
            printf("[mods %s] %p size=0x%X %s\n", tag, me.modBaseAddr, (unsigned)me.modBaseSize, n);
        } while (Module32Next(snap, &me));
    }
    CloseHandle(snap);
}

static void logf(const char* fmt, ...)
{
    va_list ap;
    va_start(ap, fmt);
    vfprintf(stdout, fmt, ap);
    fprintf(stdout, "\n");
    fflush(stdout);
    va_end(ap);
}

typedef void* (__stdcall* GetEngine2Fn)(void);
typedef void* (__stdcall* EngineMethodFn)(void* self);
typedef void* (__fastcall* CreateSfxFromFileFn)(void* owner, const char* path,
    void* a3, void* a4, void* a5, void* a6, int a7, void* a8);

static const DWORD CLIENT_RVA_CREATE_SFX_FROM_FILE = 0xBE5610;

static void gbk(const wchar_t* src, char* out, int cap)
{
    WideCharToMultiByte(936, 0, src, -1, out, cap, NULL, NULL);
}

static void tryCreate(const char* tag, const wchar_t* wpath, void* owner)
{
    HMODULE eng = GetModuleHandleA("KG3DEngineDX11EX64.dll");
    if (eng == NULL) { logf("%s: engine module not loaded", tag); return; }
    char path[512];
    gbk(wpath, path, sizeof(path));
    CreateSfxFromFileFn create = (CreateSfxFromFileFn)((BYTE*)eng + CLIENT_RVA_CREATE_SFX_FROM_FILE);
    void* sfx = NULL;
    void* out = NULL;
    float mtx[16] = { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
    DWORD exc = 0;
    DWORD64 fault = 0;
    __try { sfx = create(owner, path, NULL, NULL, NULL, mtx, 0, &out); }
    __except (exc = GetExceptionCode(),
              fault = (DWORD64)((PEXCEPTION_POINTERS)GetExceptionInformation())->ExceptionRecord->ExceptionAddress,
              EXCEPTION_EXECUTE_HANDLER) { sfx = NULL; }
    logf("%s: owner=%p -> obj=%p out=%p exc=0x%08X fault_rva=0x%llX",
         tag, owner, sfx, out, (unsigned)exc,
         (unsigned long long)(fault > (DWORD64)eng ? (fault - (DWORD64)eng) : 0));
    if (sfx != NULL)
    {
        __try
        {
            void** vt = *(void***)sfx;
            logf("   vtable=%p vt0=0x%llX vt1=0x%llX vt121=0x%llX",
                 vt,
                 (unsigned long long)((BYTE*)vt[0] - (BYTE*)eng),
                 (unsigned long long)((BYTE*)vt[1] - (BYTE*)eng),
                 (unsigned long long)((BYTE*)vt[121] - (BYTE*)eng));
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { logf("   vtable unreadable"); }
        // play step (mirrors the engine tag update): __RTDynamicCast to
        // IKG3D_NormalModel, then vt[0xD58](model,1,1,0) and vt[0xD60](model,...)
        {
            DWORD pexc = 0;
            void* model = NULL;
            DWORD64 pfault = 0;
            __try
            {
                HMODULE vc = GetModuleHandleA("VCRUNTIME140.dll");
                typedef void* (__cdecl *CastFn)(void*, long, void*, void*, int);
                CastFn cast = vc ? (CastFn)GetProcAddress(vc, "__RTDynamicCast") : NULL;
                if (cast != NULL)
                    model = cast(sfx, 0, (void*)((BYTE*)eng + 0x26080A0),
                                 (void*)((BYTE*)eng + 0x2608B40), 0);
                logf("   cast -> model=%p (cast=%p)", model, cast);
                if (model != NULL)
                {
                    void** mvt = *(void***)model;
                    typedef long (__fastcall *PlayFn)(void* self, char a, char b, int c);
                    typedef long (__fastcall *AttachFn)(void* self, void* ctx, void* r8);
                    typedef long (__fastcall *PlayMtxFn)(void* self, void* mtx, void* r8);
                    typedef void* (__fastcall *HandleFn)(void* self);
                    PlayFn play = (PlayFn)mvt[0xD58 / 8];
                    AttachFn attach = (AttachFn)mvt[0xD60 / 8];
                    PlayMtxFn playMtx = (PlayMtxFn)mvt[0x180 / 8];
                    HandleFn getHandle = (HandleFn)mvt[0x190 / 8];
                    long prc = 0;
                    if (play != NULL) prc = play(model, 1, 1, 0);
                    float mtx[16] = { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
                    long arc = attach ? attach(model, NULL, NULL) : -1;
                    long mrc = playMtx ? playMtx(model, mtx, NULL) : -1;
                    void* h = getHandle ? getHandle(model) : NULL;
                    logf("   model mvt0=0x%llX slots: play=0x%llX attach=0x%llX playMtx=0x%llX handle=0x%llX",
                         (unsigned long long)((BYTE*)mvt[0] - (BYTE*)eng),
                         (unsigned long long)((BYTE*)play - (BYTE*)eng),
                         (unsigned long long)((BYTE*)attach - (BYTE*)eng),
                         (unsigned long long)((BYTE*)playMtx - (BYTE*)eng),
                         (unsigned long long)((BYTE*)getHandle - (BYTE*)eng));
                    logf("   rc: play=0x%08X attach=0x%08X playMtx=0x%08X handle=%p",
                         (unsigned)prc, (unsigned)arc, (unsigned)mrc, h);
                }
            }
            __except (pexc = GetExceptionCode(),
                      pfault = (DWORD64)((PEXCEPTION_POINTERS)GetExceptionInformation())->ExceptionRecord->ExceptionAddress,
                      EXCEPTION_EXECUTE_HANDLER)
            {
                logf("   play exc=0x%08X fault_rva=0x%X",
                     (unsigned)pexc,
                     (unsigned)(pfault > (DWORD64)eng ? (pfault - (DWORD64)eng) : 0));
            }
        }
    }
}

static char g_rootA[MAX_PATH];

int main(void)
{
    if (GetEnvironmentVariableA("RC_PROBE_ROOT", g_rootA, MAX_PATH) == 0)
    {
        GetTempPathA(MAX_PATH, g_rootA);
        strcat_s(g_rootA, MAX_PATH, "opencode\\skillv2\\client_root");
    }
    wchar_t root[MAX_PATH];
    MultiByteToWideChar(CP_ACP, 0, g_rootA, -1, root, MAX_PATH);
    wchar_t bin64[MAX_PATH];
    wchar_t dll[MAX_PATH];
    swprintf_s(bin64, MAX_PATH, L"%s\\bin64", root);
    swprintf_s(dll, MAX_PATH, L"%s\\X3DEngine.dll", bin64);

    SetCurrentDirectoryW(root);
    AddVectoredExceptionHandler(1, vehHandler);
    g_probeHwnd = createProbeWindow();
    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    AddDllDirectory(bin64);

    HMODULE x3d = LoadLibraryExW(dll, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!x3d) { logf("X3DEngine load failed err=%lu", GetLastError()); return 2; }
    typedef int (__cdecl *fn_void)(void);
    fn_void pre = (fn_void)GetProcAddress(x3d, "?PreInitX3DEngine@NSX3DEngine@@YAHXZ");
    fn_void load = (fn_void)GetProcAddress(x3d, "?LoadX3DEngine@NSX3DEngine@@YAHXZ");
    if (pre) logf("PreInitX3DEngine -> %d", pre());
    if (load) logf("LoadX3DEngine -> %d", load()); logModules("after-load");

    // engine interface from the client adapter (loaded by the facade)
    HMODULE adapter = GetModuleHandleA("KG3DEngineAdapterX64.dll");
    void* engIface = NULL;
    if (adapter != NULL)
    {
        typedef long (__cdecl *fn_out)(void** out);
        fn_out get3d = (fn_out)GetProcAddress(adapter, "Get3DEngineInterface");
        fn_out getMovie = (fn_out)GetProcAddress(adapter, "GetMovieEngine");
        void* p = NULL;
        if (get3d) { long r = get3d(&p); logf("adapter Get3DEngineInterface r=%d -> %p", (int)r, p); }
        engIface = p;
        p = NULL;
        if (getMovie) { long r = getMovie(&p); logf("adapter GetMovieEngine r=%d -> %p", (int)r, p); }
    }
    else logf("adapter module not loaded");

    wchar_t engPath[MAX_PATH]; swprintf_s(engPath, MAX_PATH, L"%s\\KG3DEngineDX11EX64.dll", bin64); HMODULE eng = LoadLibraryExW(engPath, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    logf("client engine load -> %p err=%lu", eng, GetLastError()); logModules("after-engine-load");

    // manager object the facade uses (facade global RVA 0xFA418) + GetK3EngineMgr
    {
        typedef void* (__cdecl *GetMgrFn)(void);
        GetMgrFn getMgr = (GetMgrFn)GetProcAddress(x3d, "?GetK3EngineMgr@NSX3DEngine@@YAPEAVIX3DEngineManager@@XZ");
        void* mgr = getMgr ? getMgr() : NULL;
        void* obj = *(void**)((BYTE*)x3d + 0xFA418);
        logf("GetK3EngineMgr -> %p   facade global 0xFA418 -> %p", mgr, obj);
        if (obj != NULL)
        {
            DWORD mexc = 0;
            __try
            {
            void** vt = *(void***)obj;
            logf("obj vtable=%p slot0=%p slot1=%p slot82=%p", vt, vt[0], vt[1], vt[82]);
            logf("  (no vt[0] call - it unloads the adapter; exploratory only)");
            }
            __except (mexc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER)
            {
                logf("obj vt call exc=0x%08X", (unsigned)mexc);
            }
        }
        if (eng != NULL)
        {
            typedef void* (__stdcall *GetEngine2Fn)(void);
            GetEngine2Fn ge2 = (GetEngine2Fn)GetProcAddress(eng, "KG3D_GetEngine2");
            logf("KG3D_GetEngine2 -> %p", ge2 ? ge2() : NULL);
        }
    }

    // adapter interface object (built by adapter ctor 0x72AF0, vtable 0x29FFF0);
    // vt[0] is the big Init that builds KG3D_ENGINE_INIT_PARAM + calls KG3D_CreateEngine
    if (adapter != NULL && engIface != NULL)
    {
        DWORD aexc = 0;
        void* aat = NULL;
        void* aav = NULL;
        CONTEXT* actx = NULL;
        logf("iface block: iface=%p adapter=%p", engIface, adapter);
        if (eng != NULL)
        {
            __try
            {
                void* fm0 = *(void**)((BYTE*)eng + 0x2D22598);
                logf("engine FS manager BEFORE init = %p", fm0);
                if (fm0 != NULL)
                    logf("  FS root before init = '%s'", (char*)((BYTE*)fm0 + 0xC));
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { logf("pre-init FS read failed"); }
        }
        {
            HMODULE kgc = GetModuleHandleA("KGCommonX64.dll");
            if (kgc != NULL)
            {
                typedef void* (__cdecl *CreateConsoleFn)(void);
                typedef void* (__cdecl *CreateFileFn)(const wchar_t*, const wchar_t*, int, int);
                typedef void (__cdecl *RegisterFn)(void* stream);
                typedef void (__cdecl *SetMaskFn)(unsigned mask);
                CreateConsoleFn cc = (CreateConsoleFn)GetProcAddress(kgc, "KG_CreateConsoleLogStream");
                CreateFileFn cf = (CreateFileFn)GetProcAddress(kgc, "KG_CreateFileLogStream");
                RegisterFn reg = (RegisterFn)GetProcAddress(kgc, "KG_RegisterLogStream");
                SetMaskFn sm = (SetMaskFn)GetProcAddress(kgc, "KG_SetLogMask");
                void* st = NULL;
                if (cf)
                {
                    wchar_t logPath[MAX_PATH]; swprintf_s(logPath, MAX_PATH, L"%s\\logs\\engine", root); st = cf(logPath,
                            L"client_sfx_probe", 1, 1);
                    logf("file log stream -> %p", st);
                }
                if (st == NULL && cc)
                {
                    st = cc();
                    logf("console log stream -> %p", st);
                }
                if (st != NULL && reg) reg(st);
                if (sm) sm(0xFFFFFFFF);
                logf("KG log stream registered");
                // hook KG_PrintfLog in the engine + adapter IATs so engine log goes to stdout
                void* realLog = GetProcAddress(kgc, "KG_PrintfLog");
                HMODULE engMod = GetModuleHandleA("KG3DEngineDX11EX64.dll");
                int p1 = patchLogIat(engMod, realLog, (void*)hookPrintfLog);
                int p2 = patchLogIat(adapter, realLog, (void*)hookPrintfLog);
                logf("KG_PrintfLog hooked: engine=%d adapter=%d (real=%p)", p1, p2, realLog);
                // hook CreateFileW/A in the engine IAT to trace file lookups
                g_realCreateFileW = (CreateFileWFn)GetProcAddress(GetModuleHandleA("KERNEL32.dll"), "CreateFileW");
                g_realCreateFileA = (CreateFileAFn)GetProcAddress(GetModuleHandleA("KERNEL32.dll"), "CreateFileA");
                int p3 = patchLogIat(engMod, (void*)g_realCreateFileW, (void*)hookCreateFileW);
                int p4 = patchLogIat(engMod, (void*)g_realCreateFileA, (void*)hookCreateFileA);
                logf("CreateFile hooked: W=%d A=%d", p3, p4);
                // substitute the host window in KG3D_Engine::CreateTargetWindow (15-byte prologue)
                {
                    int pc = installInlineHook(engMod, 0x8AEF30, (void*)hookCreateTargetWindow,
                                               g_ctwSaved, &g_ctwTramp, 15);
                    logf("CreateTargetWindow hook=%d hwnd=%p tramp=%p", pc, g_probeHwnd, g_ctwTramp);
                }
                // MSVCR110 (_invoke_watson) hook - installed after the engine load
                {
                    HMODULE crt = GetModuleHandleA("MSVCR110.dll");
                    if (crt == NULL) crt = LoadLibraryA("MSVCR110.dll");
                    if (crt != NULL)
                    {
                        void* iw = GetProcAddress(crt, "abort"); if (iw == NULL) iw = GetProcAddress(crt, "_invoke_watson");
                        logf("MSVCR110=%p _invoke_watson=%p", crt, iw);
                        if (iw)
                        {
                            int pi = installInlineHook(crt, (DWORD)((BYTE*)iw - (BYTE*)crt),
                                                      (void*)hookInvokeWatson, g_iwSaved, &g_iwTramp, 12);
                            logf("  watson hook=%d", pi);
                        }
                    }
                }
                (void)installFsHook; (void)hookFsCreate; (void)hookCreateEngine;
            }
            else logf("KGCommonX64.dll not loaded");
        }
        if (adapter != NULL)
        {
            typedef long (__cdecl *SetRootFn)(const char* path);
            SetRootFn setRoot = (SetRootFn)GetProcAddress(adapter, "SetEngineWorkingRootDirectory");
            if (setRoot)
            {
                long rr = setRoot(g_rootA);
                logf("SetEngineWorkingRootDirectory -> %ld", rr);
            }
            // Game file layer (Engine_Lua5X64) init - the adapter FS delegates to it.
            // Must run before the engine init, like KJX3PackageModule::Initialize does.
            {
                HMODULE lua = GetModuleHandleA("Engine_Lua5X64.dll");
                if (lua == NULL)
                {
                    wchar_t lp[MAX_PATH];
                    swprintf_s(lp, MAX_PATH, L"%s\\Engine_Lua5X64.dll", bin64);
                    lua = LoadLibraryExW(lp, NULL, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                }
                logf("Engine_Lua5X64 -> %p err=%lu", lua, GetLastError());
                if (lua != NULL)
                {
                    typedef void (__cdecl *LuaSetRootFn)(const char*);
                    LuaSetRootFn luaSetRoot = (LuaSetRootFn)((BYTE*)lua + 0xB5400);
                    luaSetRoot(g_rootA);
                    logf("g_SetRootPath done");
                    typedef void (__cdecl *LuaSetFileFn)(const char*);
                    LuaSetFileFn luaSetFile = (LuaSetFileFn)((BYTE*)lua + 0xB5220);
                    luaSetFile(g_rootA);
                    logf("g_SetFilePath done");
                    typedef int (__cdecl *InitPakFn)(const char*, const char*, const char*,
                                                     int, int, int, int, int, void*);
                    InitPakFn initPak = (InitPakFn)((BYTE*)lua + 0xCC2D0);
                    int pr = initPak("C:/SeasunGame/Game/JX3/Pakv4", "Trunk.Dir", "", 0, 0, 0, 0, 0, (void*)"");
                    logf("KG_InitPakV4FileSystem -> %d", pr);
                }
            }
            // inspect the iface fields the adapter init reads (rbx+0x20, rbx+0x2018)
            __try
            {
                BYTE* b = (BYTE*)engIface;
                logf("iface hexdump +0x2000..+0x2060:");
                for (int row = 0; row < 6; row++)
                {
                    BYTE* p = b + 0x2000 + row * 0x10;
                    logf("  +0x%04X: %016llX %016llX", 0x2000 + row * 0x10,
                         *(unsigned long long*)p, *(unsigned long long*)(p + 8));
                }
                char* dataPtr = *(char**)(b + 0x2020);
                if (dataPtr != NULL)
                    logf("  [iface+0x2020] -> '%s'", dataPtr);
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { logf("iface field read failed"); }
            // root source: object at iface+0x9d8, vt[2](&buf) should return the working root
            __try
            {
                void* cfg = *(void**)((BYTE*)engIface + 0x9d8);
                logf("iface+0x9d8 cfg object = %p", cfg);
                if (cfg != NULL)
                {
                    char buf[512];
                    buf[0] = 0;
                    void** vt = *(void***)cfg;
                    logf("cfg vtable=%p vt0=%p vt1=%p vt2=%p", vt, vt[0], vt[1], vt[2]);
                    typedef long (__fastcall *GetStrFn)(void* self, char* out);
                    long r = ((GetStrFn)vt[2])(cfg, buf);
                    logf("cfg vt[2] -> %ld buf='%s'", r, buf);
                }
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { logf("cfg object read failed"); }
            // facade objects that the host may pass into the manager init
            __try
            {
                typedef void* (__cdecl *GetObjFn)(void);
                GetObjFn getFilePath = (GetObjFn)GetProcAddress(x3d, "?GetFilePath@NSX3DEngine@@YAPEAVIFilePath@1@XZ");
                GetObjFn getClipboard = (GetObjFn)GetProcAddress(x3d, "?GetClipboard@NSX3DEngine@@YAPEAVIClipboard@@XZ");
                void* fp = getFilePath ? getFilePath() : NULL;
                void* cb = getClipboard ? getClipboard() : NULL;
                logf("facade GetFilePath -> %p  GetClipboard -> %p", fp, cb);
                if (fp != NULL)
                {
                    void** fvt = *(void***)fp;
                    logf("  filepath vtable=%p vt0=%p vt1=%p vt2=%p", fvt, fvt[0], fvt[1], fvt[2]);
                }
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { logf("facade object probe failed"); }
        }
        __try
        {
            void** ivt = *(void***)engIface;
            logf("iface vtable=%p slot0=%p (adapter+0x29FFF0=%p)",
                 ivt, ivt[0], (BYTE*)adapter + 0x29FFF0);
            if (ivt == (void**)((BYTE*)adapter + 0x29FFF0))
            {
                // the game's path: NSX3DEngine::KWindowsX3DEngine::Init ->
                // iface->vt[0](rdx=0, r8d=4)  (JX3ClientX64.exe 0x1F822..0x1F82F)
                typedef long (__fastcall *InitFn)(void* self, void* rdx, int r8d);
                long r = ((InitFn)ivt[0])(engIface, NULL, 4);
                logf("iface->vt[0](0, 4) -> %ld", r);
            }
            else logf("iface vtable mismatch");
        }
        __except (aexc = GetExceptionCode(),
                  aat = GetExceptionInformation()->ExceptionRecord->ExceptionAddress,
                  aav = (void*)(GetExceptionInformation()->ExceptionRecord->NumberParameters > 1 ? GetExceptionInformation()->ExceptionRecord->ExceptionInformation[1] : 0),
                  actx = GetExceptionInformation()->ContextRecord,
                  EXCEPTION_EXECUTE_HANDLER)
        {
            logf("iface vt[0] exc=0x%08X at=%p av_addr=%p", (unsigned)aexc, aat, aav);
            if (actx != NULL)
            {
                logf("  ctx rip=%p rsp=%p rbp=%p", (void*)actx->Rip, (void*)actx->Rsp, (void*)actx->Rbp);
                DWORD64* sp = (DWORD64*)actx->Rsp;
                for (int si = 0; si < 40; si++)
                {
                    DWORD64 v = sp[si];
                    if (v > 0x10000 && v < 0x00007FFFFFFFFFFF)
                        logf("  stack[%d]=%p", si, (void*)v);
                }
            }
            logModules("at-exception");
        }
        if (eng != NULL)
        {
            typedef void* (__stdcall *GetEngine2Fn)(void);
            GetEngine2Fn ge2 = (GetEngine2Fn)GetProcAddress(eng, "KG3D_GetEngine2");
            logf("KG3D_GetEngine2 (after iface init) -> %p", ge2 ? ge2() : NULL);
            // engine's own scene + actor-from-file path with the real .Sfx
            {
                void* engine = ge2 ? ge2() : NULL;
                if (engine != NULL)
                {
                    __try
                    {
                        typedef void* (__fastcall *GetWindowFn)(void* self);
                        typedef void* (__fastcall *GetSceneFn)(void* self);
                        GetWindowFn getWin = (GetWindowFn)GetProcAddress(eng,
                            "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
                        GetSceneFn getScene = (GetSceneFn)GetProcAddress(eng,
                            "?Get3DScene2@KG3D_Window@@UEAAPEAVKG3D_Scene@@XZ");
                        void* window = getWin ? getWin(engine) : NULL;
                        void* scene = (window && getScene) ? getScene(window) : NULL;
                        logf("window=%p scene=%p", window, scene);
                        // create an empty scene + scene view (the SFX bind context likely needs one)
                        if (engine != NULL)
                        {
                            typedef long (__fastcall *CreateEmptySceneFn)(void* self, size_t size, void** out);
                            CreateEmptySceneFn createScene = (CreateEmptySceneFn)((BYTE*)eng + 0x8AFAE0);
                            void* newScene = NULL;
                            long src = createScene(engine, 0, &newScene);
                            logf("CreateEmptyScene -> rc=0x%08X scene=%p", (unsigned)src, newScene);
                            if (newScene != NULL && window != NULL)
                            {
                                typedef long (__fastcall *CreateViewFn)(void* self, void* sc,
                                    const char* name, void* physics, void** out, int flags);
                                CreateViewFn createView = (CreateViewFn)((BYTE*)eng + 0x8AF3A0);
                                void* view = NULL;
                                long vrc = createView(engine, newScene, "probe_view", NULL, &view, 0);
                                logf("CreateSceneViewFrom3DScene -> rc=0x%08X view=%p", (unsigned)vrc, view);
                                if (view != NULL)
                                {
                                    typedef long (__fastcall *AddView2Fn)(void* win, void* vw, int source, int flag);
                                    AddView2Fn addView2 = (AddView2Fn)((BYTE*)eng + 0xA6B870);
                                    long a2 = addView2(window, view, 0, 1);
                                    logf("AddSceneView_SceneView -> rc=0x%08X; Get3DScene2 now=%p",
                                         (unsigned)a2, getScene ? getScene(window) : NULL);
                                }
                            }
                        }
                        // animation tag system state (game's own SFX-tag path)
                        typedef void* (__fastcall *GetTagFn)(void* self);
                        GetTagFn getTag = (GetTagFn)GetProcAddress(eng,
                            "?GetAnimTagSystem@KG3D_Engine@@UEBAPEAUIKG3D_AnimationTagSystem@@XZ");
                        void* tagSys = getTag ? getTag(engine) : NULL;
                        logf("AnimTagSystem = %p", tagSys);
                        if (tagSys != NULL)
                        {
                            HMODULE tagMod2 = GetModuleHandleA("KG3D_AnimationTagX64.dll");
                            void** tvt = *(void***)tagSys;
                            logf("  tag vtable=%p", tvt);
                            for (int i = 0; i < 28; i++)
                                logf("  tag vt[%d] = %p (rva 0x%llX)", i, tvt[i],
                                     (unsigned long long)((BYTE*)tvt[i] - (BYTE*)tagMod2));
                        }
                        if (tagSys == NULL)
                        {
                            HMODULE tagMod = GetModuleHandleA("KG3D_AnimationTagX64.dll");
                            if (tagMod == NULL)
                            {
                                wchar_t tp[MAX_PATH];
                                swprintf_s(tp, MAX_PATH, L"%s\\KG3D_AnimationTagX64.dll", bin64);
                                tagMod = LoadLibraryExW(tp, NULL,
                                    LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                            }
                            typedef void* (__cdecl *CreateTagFn)(void);
                            CreateTagFn createTag = tagMod
                                ? (CreateTagFn)GetProcAddress(tagMod, "KG3D_CreateAnimationTagSystem") : NULL;
                            void* made = createTag ? createTag() : NULL;
                            logf("KG3D_CreateAnimationTagSystem -> %p (mod=%p)", made, tagMod);
                            if (made != NULL)
                            {
                                typedef long (__fastcall *SetTagFn)(void* self, void* tag);
                                SetTagFn setTag = (SetTagFn)GetProcAddress(eng,
                                    "?SetAnimTagSystem@KG3D_Engine@@UEAAJPEAUIKG3D_AnimationTagSystem@@@Z");
                                long trc = setTag ? setTag(engine, made) : -1;
                                logf("SetAnimTagSystem -> 0x%08X, now GetAnimTagSystem=%p",
                                     (unsigned)trc, getTag ? getTag(engine) : NULL);
                            }
                        }
                        // CreateActorFromFile(this, path, arg3, out, flags, options)
                        typedef long (__fastcall *CreateActorFn)(void* self, const char* path,
                            void* arg3, void** out, unsigned flags, void* options);
                        CreateActorFn createActor = (CreateActorFn)((BYTE*)eng + 0x8B2DA0);
                        void* actor = NULL;
                        long rc = createActor(engine,
                            "data\\source\\other\\特效\\技能\\SFX\\增益\\c纯阳坐忘.Sfx",
                            NULL, &actor, 0, NULL);
                        logf("CreateActorFromFile(.Sfx) -> rc=0x%08X actor=%p", (unsigned)rc, actor);
                        // model actor (player mesh) - should load; dump its vtable
                        char mpath[512];
                        gbk(L"data\\source\\player\\f1\\部件\\f1_3094_body_hd.mesh", mpath, sizeof(mpath));
                        void* mactor = NULL;
                        long mrc = createActor(engine, mpath, NULL, &mactor, 0, NULL);
                        logf("CreateActorFromFile(mesh) -> rc=0x%08X actor=%p", (unsigned)mrc, mactor);
                        if (mactor != NULL)
                        {
                            void** avt = *(void***)mactor;
                            logf("  actor vtable=%p", avt);
                            for (int i = 0; i < 40; i++)
                                logf("  actor vt[%d] = 0x%llX", i,
                                     (unsigned long long)((BYTE*)avt[i] - (BYTE*)eng));
                            // m_piCurModel is at actor+0x358 (from KG3D_Actor::_FindSocketInBaseModel)
                            void* model = *(void**)((BYTE*)mactor + 0x358);
                            logf("  actor+0x358 model -> %p", model);
                            if (model != NULL)
                            {
                                void** mvt = *(void***)model;
                                logf("  model vtable=%p", mvt);
                                for (int i = 0; i < 48; i++)
                                    logf("  model vt[%d] = 0x%llX", i,
                                         (unsigned long long)((BYTE*)mvt[i] - (BYTE*)eng));
                                // find the animation controller: any object whose vtable's
                                // slot 22 (StartAnimation, eng+0xBC1C70) matches
                                void* ctrl = NULL;
                                __try
                                {
                                    BYTE* bases[2] = { (BYTE*)model, (BYTE*)mactor };
                                    for (int b = 0; b < 2 && ctrl == NULL; b++)
                                    {
                                        for (int o = 0; o < 0x2000 && ctrl == NULL; o += 8)
                                        {
                                            void* vt = *(void**)(bases[b] + o);
                                            if (vt == NULL) continue;
                                            __try
                                            {
                                                void* s22 = ((void**)vt)[22];
                                                if (s22 == (void*)((BYTE*)eng + 0xBC1C70))
                                                    ctrl = bases[b] + o;
                                            }
                                            __except (EXCEPTION_EXECUTE_HANDLER) { }
                                        }
                                    }
                                }
                                __except (EXCEPTION_EXECUTE_HANDLER) { logf("  ctrl scan fault"); }
                                logf("  animation controller (scanned) -> %p", ctrl);
                                // original path: allocate+ctor the controller (0x3A8, engine ctor),
                                // SetActor, CreateAnimationFromFile(tani), StartAnimation, FrameMove
                                if (ctrl == NULL)
                                {
                                    __try
                                    {
                                        typedef void* (__fastcall *AllocFn)(void* a, size_t size, size_t align);
                                        AllocFn alloc = (AllocFn)((BYTE*)eng + 0xB0F300);
                                        void* c = alloc(NULL, 0x3A8, 8);
                                        logf("  controller alloc -> %p", c);
                                        if (c != NULL)
                                        {
                                            typedef void (__fastcall *CtorFn)(void* self);
                                            CtorFn ctor = (CtorFn)((BYTE*)eng + 0xBC1100);
                                            ctor(c);
                                            logf("  controller ctor done, vtable=%p", *(void**)c);
                                            typedef long (__fastcall *SetActorFn)(void* self, void* actor);
                                            SetActorFn setActor = (SetActorFn)((BYTE*)eng + 0xBC2120);
                                            long sar = setActor(c, mactor);
                                            logf("  SetActor -> 0x%08X", (unsigned)sar);
                                            ctrl = c;
                                        }
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER) { logf("  controller create fault"); }
                                }
                                // original play path: CreateAnimationFromFile(tani) + StartAnimation + FrameMove
                                if (ctrl != NULL)
                                {
                                    typedef long (__fastcall *CreateAnimFn)(void* self, const char* path,
                                        void** out, int a4, int a5);
                                    CreateAnimFn createAnim = (CreateAnimFn)((BYTE*)eng + 0x8B6EB0);
                                    char taniPath[512];
                                    char dirAni[64];
                                    gbk(L"动作", dirAni, sizeof(dirAni));
                                    sprintf_s(taniPath, sizeof(taniPath),
                                              "data\\source\\player\\f1\\%s\\F1HA393_start01.tani", dirAni);
                                    void* anim = NULL;
                                    // game-layer file open: does it unwrap the GATA tani?
                                    {
                                        typedef void* (__cdecl *KGOpenFileFn)(const char*);
                                        HMODULE luaM = GetModuleHandleA("Engine_Lua5X64.dll");
                                        KGOpenFileFn kgopen = (KGOpenFileFn)((BYTE*)luaM + 0xC02E0);
                                        void* ifile = kgopen(taniPath);
                                        logf("  KG_OpenFile(tani) -> %p", ifile);
                                        if (ifile != NULL)
                                        {
                                            __try
                                            {
                                                void** ivt = *(void***)ifile;
                                                logf("  ifile vtable=%p", ivt);
                                                for (int i = 0; i < 16; i++)
                                                    logf("  ifile vt[%d] = 0x%llX", i,
                                                         (unsigned long long)((BYTE*)ivt[i] - (BYTE*)luaM));
                                                BYTE* ib = (BYTE*)ifile;
                                                logf("  ifile bytes[0..0x30]: %02X %02X %02X %02X %02X %02X %02X %02X  %02X %02X %02X %02X %02X %02X %02X %02X",
                                                     ib[0],ib[1],ib[2],ib[3],ib[4],ib[5],ib[6],ib[7],
                                                     ib[8],ib[9],ib[10],ib[11],ib[12],ib[13],ib[14],ib[15]);
                                            }
                                            __except (EXCEPTION_EXECUTE_HANDLER) { logf("  ifile read fault"); }
                                        }
                                    }
                                    {
                                        typedef void* (__fastcall *LoadFileFn2)(const char* path, int flags);
                                        LoadFileFn2 lf2 = (LoadFileFn2)((BYTE*)eng + 0xB0F870);
                                        void* reader = lf2(taniPath, 0);
                                        logf("  KG3D_LoadFile(tani) -> %p", reader);
                                        if (reader != NULL)
                                        {
                                            __try
                                            {
                                                BYTE* rb = (BYTE*)reader;
                                                logf("  reader[0..0x40]: %02X %02X %02X %02X %02X %02X %02X %02X  %02X %02X %02X %02X %02X %02X %02X %02X",
                                                     rb[0],rb[1],rb[2],rb[3],rb[4],rb[5],rb[6],rb[7],
                                                     rb[8],rb[9],rb[10],rb[11],rb[12],rb[13],rb[14],rb[15]);
                                                logf("  reader[0x10..0x20]: %02X %02X %02X %02X %02X %02X %02X %02X  %02X %02X %02X %02X %02X %02X %02X %02X",
                                                     rb[16],rb[17],rb[18],rb[19],rb[20],rb[21],rb[22],rb[23],
                                                     rb[24],rb[25],rb[26],rb[27],rb[28],rb[29],rb[30],rb[31]);
                                                void* buf = *(void**)(rb + 0x10);
                                                logf("  reader buffer -> %p", buf);
                                                if (buf != NULL)
                                                {
                                                    BYTE* bb = (BYTE*)buf;
                                                    logf("  buffer head: %c%c%c%c  %02X %02X %02X %02X %02X %02X %02X %02X",
                                                         bb[0],bb[1],bb[2],bb[3],bb[4],bb[5],bb[6],bb[7],
                                                         bb[8],bb[9],bb[10],bb[11],bb[12],bb[13],bb[14],bb[15]);
                                                }
                                            }
                                            __except (EXCEPTION_EXECUTE_HANDLER) { logf("  reader read fault"); }
                                        }
                                    }
                                    long arc2 = createAnim(engine, taniPath, &anim, 0, 0);
                                    logf("  CreateAnimationFromFile(a4=0) -> rc=0x%08X anim=%p", (unsigned)arc2, anim);
                                    if (anim == NULL)
                                    {
                                        arc2 = createAnim(engine, taniPath, &anim, 1, 0);
                                        logf("  CreateAnimationFromFile(a4=1) -> rc=0x%08X anim=%p", (unsigned)arc2, anim);
                                    }
                                    if (anim == NULL)
                                    {
                                        arc2 = createAnim(engine, taniPath, &anim, 2, 0);
                                        logf("  CreateAnimationFromFile(a4=2) -> rc=0x%08X anim=%p", (unsigned)arc2, anim);
                                    }
                                    if (anim != NULL)
                                    {
                                        typedef long (__fastcall *StartAnimFn)(void* self, void* ani,
                                            int playType, float speed, unsigned a5, unsigned a6,
                                            void* userdata, void* ik, void* dbone);
                                        StartAnimFn startAnim = (StartAnimFn)((BYTE*)eng + 0xBC1C70);
                                        long src2 = startAnim(ctrl, anim, 0, 1.0f, 0, 0, NULL, NULL, NULL);
                                        logf("  StartAnimation -> rc=0x%08X", (unsigned)src2);
                                        // run the engine's own animation update for a while
                                        typedef long (__fastcall *FrameMoveFn)(void* self);
                                        FrameMoveFn ctrlMove = (FrameMoveFn)((BYTE*)eng + 0xBC2620);
                                        for (int f = 0; f < 120; f++)
                                        {
                                            long frc = ctrlMove(ctrl);
                                            if (f == 0 || f == 59 || f == 119)
                                                logf("  ctrl FrameMove[%d] -> 0x%08X", f, (unsigned)frc);
                                            Sleep(16);
                                        }
                                        logf("  animation frames done");
                                    }
                                }
                            }
                        }
                        // FS-layer test: game layer (g_IsFileExist) vs engine loader (KG3D_LoadFile)
                        {
                            HMODULE lua2 = GetModuleHandleA("Engine_Lua5X64.dll");
                            if (lua2 != NULL)
                            {
                                typedef int (__cdecl *ExistFn)(const char*);
                                ExistFn exist = (ExistFn)((BYTE*)lua2 + 0xB5060);
                                char sp[512], sf[512], sfLow[512];
                                gbk(L"data\\source\\player\\f1\\部件\\f1_3094_body_hd.mesh", sp, sizeof(sp));
                                gbk(L"data\\source\\other\\特效\\技能\\SFX\\增益\\c纯阳坐忘.Sfx", sf, sizeof(sf));
                                gbk(L"data\\source\\other\\特效\\技能\\sfx\\增益\\c纯阳坐忘.sfx", sfLow, sizeof(sfLow));
                                logf("g_IsFileExist(mesh)=%d sfx=%d sfxLow=%d loose=%d",
                                     exist(sp), exist(sf), exist(sfLow), exist("data\\zz_loose_test.txt"));
                                typedef void* (__fastcall *LoadFileFn)(const char* path, int flags);
                                LoadFileFn lf = (LoadFileFn)((BYTE*)eng + 0xB0F870);
                                void* r1 = lf(sp, 0);
                                void* r2 = lf(sf, 0);
                                void* r3 = lf(sfLow, 0);
                                logf("KG3D_LoadFile(mesh)=%p sfx=%p sfxLow=%p", r1, r2, r3);
                            }
                        }
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER) { logf("scene/actor probe fault"); }
                }
            }
            // The game's own represent layer (most original path):
            // JX3RepresentX64!CreateSO3Represent -> ECS root -> actors/animations/tags/SFX
            {
                HMODULE rep = GetModuleHandleA("JX3RepresentX64.dll");
                if (rep == NULL)
                {
                    wchar_t rp[MAX_PATH];
                    swprintf_s(rp, MAX_PATH, L"%s\\JX3RepresentX64.dll", bin64);
                    rep = LoadLibraryExW(rp, NULL,
                        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                }
                logf("JX3RepresentX64 -> %p err=%lu", rep, GetLastError());
                if (rep != NULL)
                {
                    typedef void* (__cdecl *CreateRepFn)(void);
                    CreateRepFn createRep = (CreateRepFn)GetProcAddress(rep, "CreateSO3Represent");
                    void* r = createRep ? createRep() : NULL;
                    logf("CreateSO3Represent -> %p", r);
                    if (r != NULL)
                    {
                        void** rvt = *(void***)r;
                        logf("  represent vtable=%p (rep=%p)", rvt, rep);
                        for (int i = 0; i < 64; i++)
                            logf("  rep vt[%d] = 0x%llX", i,
                                 (unsigned long long)((BYTE*)rvt[i] - (BYTE*)rep));
                    }
                    typedef void* (__cdecl *RootFn)(void);
                    RootFn rootFn = (RootFn)GetProcAddress(rep, "GetRepresentECSRootEntity");
                    void* root = rootFn ? rootFn() : NULL;
                    logf("GetRepresentECSRootEntity -> %p", root);
                }
            }
            // SFX manager (engine+0x2c10) vtable dump
            {
                void* engine = ge2 ? ge2() : NULL;
                if (engine != NULL)
                {
                    __try
                    {
                        void* mgr = *(void**)((BYTE*)engine + 0x2c10);
                        logf("SFX manager = %p", mgr);
                        if (mgr != NULL)
                        {
                            void** mvt = *(void***)mgr;
                            logf("  vtable = %p", mvt);
                            for (int i = 0; i < 15; i++)
                                logf("  sfxmgr vt[%d] = 0x%llX", i,
                                     (unsigned long long)((BYTE*)mvt[i] - (BYTE*)eng));
                            // sub-manager the slots forward to ([mgr+0xbd8]+off)
                            void* sub = *(void**)((BYTE*)mgr + 0xbd8);
                            logf("  sub-mgr [mgr+0xbd8] = %p", sub);
                            if (sub != NULL)
                            {
                                void** svt = *(void***)sub;
                                logf("  sub vtable = %p", svt);
                                for (int i = 0; i < 32; i++)
                                    logf("  submgr vt[%d] = 0x%llX", i,
                                         (unsigned long long)((BYTE*)svt[i] - (BYTE*)eng));
                            }
                        }
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER) { logf("SFX manager dump failed"); }
                }
            }
            // engine file manager global (0x2D22598) stores root at +0xC
            __try
            {
                void* fm = *(void**)((BYTE*)eng + 0x2D22598);
                logf("engine FS manager = %p", fm);
                if (fm != NULL)
                    logf("  FS root stored = '%s'", (char*)((BYTE*)fm + 0xC));
                void* fs2018 = *(void**)((BYTE*)engIface + 0x2018);
                logf("iface+0x2018 after init = %p", fs2018);
                if (fs2018 != NULL)
                    logf("  fs vtable = %p (facade=%p adapter=%p engine=%p)", *(void**)fs2018, x3d, adapter, eng);
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { logf("FS manager read failed"); }
        }
    }

    // owner chain (mirrors the ME caller): singleton @RVA 0x2CF1038 -> vt[8]()
    void* owner = engIface;
    if (eng != NULL)
    {
        DWORD oexc = 0;
        void* o = NULL;
        __try
        {
            void** g = *(void***)((BYTE*)eng + 0x2CF1038);
            if (g != NULL)
            {
                typedef void* (__fastcall *GetFn)(void* self);
                GetFn getOwner = (GetFn)(*(void***)g)[8];
                o = getOwner(g);
            }
        }
        __except (oexc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { o = NULL; }
        logf("client owner chain -> %p exc=0x%08X", o, (unsigned)oexc);
        if (o != NULL) owner = o;
    }

    tryCreate("existing .Sfx (client owner)",
              L"data\\source\\other\\特效\\技能\\SFX\\增益\\c纯阳坐忘.Sfx", owner);
    tryCreate("existing .Sfx lowercased",
              L"data\\source\\other\\特效\\技能\\sfx\\增益\\c纯阳坐忘.sfx", owner);
    tryCreate("pss (client owner)",
              L"data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策撼如雷02_重制.pss", owner);

    logf("probe done");
    return 0;
}




















