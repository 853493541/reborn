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
#include <math.h>
#include <dbghelp.h>
#pragma comment(lib, "dbghelp.lib")

static HMODULE g_eng = NULL;
static HMODULE g_repModule = NULL;

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

// "module.dll+0xRVA" for a function pointer (log-friendly)
static const char* fnLoc(void* fn)
{
    static char buf[128];
    HMODULE mod = NULL;
    if (fn != NULL && GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                                         GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                         (LPCSTR)fn, &mod) && mod != NULL)
    {
        char name[MAX_PATH];
        GetModuleFileNameA(mod, name, MAX_PATH);
        const char* base = strrchr(name, '\\');
        base = (base != NULL) ? base + 1 : name;
        sprintf_s(buf, sizeof(buf), "%s+0x%llX", base,
                  (unsigned long long)((BYTE*)fn - (BYTE*)mod));
    }
    else
        sprintf_s(buf, sizeof(buf), "%p", fn);
    return buf;
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
static volatile LONG g_flagWatchArmed = 0;
static volatile LONG g_flagWatchHit = 0;
static BYTE g_ctwSaved[32];
static BYTE* g_ctwTramp = NULL;
static int g_castRequest = -1;
static int g_autoCastDone = 0;
static char g_abil[32][512];
static char g_abilName[32][128];
static int g_abilCount = 0;

static LRESULT CALLBACK HostWndProc(HWND h, UINT m, WPARAM w, LPARAM l)
{
    if (m == WM_KEYDOWN)
    {
        if (w >= '1' && w <= '9') g_castRequest = (int)(w - '1');
    }
    else if (m == WM_CLOSE)
    {
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcA(h, m, w, l);
}

static void loadAbilities(const char* path)
{
    FILE* f = NULL;
    if (fopen_s(&f, path, "rb") != 0 || f == NULL)
    {
        logf("[host] abilities file not found: %s", path);
        return;
    }
    char line[1024];
    while (fgets(line, sizeof(line), f) != NULL && g_abilCount < 32)
    {
        char* tab = strchr(line, '\t');
        if (tab == NULL) continue;
        *tab = 0;
        char* nl = strpbrk(tab + 1, "\r\n");
        if (nl != NULL) *nl = 0;
        strcpy_s(g_abil[g_abilCount], sizeof(g_abil[0]), line);
        strcpy_s(g_abilName[g_abilCount], sizeof(g_abilName[0]), tab + 1);
        g_abilCount++;
    }
    fclose(f);
    logf("[host] abilities loaded: %d", g_abilCount);
}

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
    if (path != NULL && (strstr(path, "entities") != NULL || strstr(path, "sceneinfo") != NULL ||
                         strstr(path, "f1_3094") != NULL || strstr(path, "npc") != NULL))
    {
        printf("[host] LoadFile path=%s\n", path);
        fflush(stdout);
        if (strstr(path, "f1_3094") != NULL)
        {
            static int bt = 0;
            if (bt < 3)
            {
                bt++;
                void* frames[16];
                USHORT n = RtlCaptureStackBackTrace(1, 16, frames, NULL);
                for (USHORT i = 0; i < n; i++)
                {
                    DWORD64 a = (DWORD64)frames[i];
                    printf("[host]   bt[%u] = 0x%llX (eng+0x%llX)\n", i, a,
                           (a > (DWORD64)g_eng) ? (a - (DWORD64)g_eng) : 0);
                }
                fflush(stdout);
            }
        }
    }
    return ((LoadFileFn)g_lfTramp)(path, flags);
}

// stub object for represent Param managers we cannot construct yet: a valid
// vtable of no-op methods (return 0) so calls don't fault on a null vtable.
static long __fastcall stubRet0(void)
{
    return 0;
}

static void* makeStubObject(size_t bytes)
{
    static void* stubVt[0x100];
    static int vtInit = 0;
    if (!vtInit)
    {
        for (int i = 0; i < 0x100; i++)
            stubVt[i] = (void*)stubRet0;
        vtInit = 1;
    }
    void* obj = calloc(1, bytes);
    if (obj != NULL)
        *(void**)obj = stubVt;
    return obj;
}

// ---- game exe as a callable module (DONT_RESOLVE + manual IAT) --------------
// The event managers (KJX3LogicEventModule / KJX3RepresentEventModule /
// KEventCommonMgr) are exe-internal classes; map the exe and call its code.
static HMODULE g_exeModule = NULL;
static void* g_exeLogicEvent = NULL;
static void* g_exeRepEvent = NULL;
static void* g_exeLogicMgr = NULL;
static void* g_exeRepMgr = NULL;
static void* g_exeCommonMgr = NULL;
static void* g_exeDispatcher = NULL;

// exe CRT helpers needed by the magic-static guards in OnInitialize; the exe's
// CRT was never initialized, so these are stubbed to drive the init block.
static void __fastcall exeGuardHeader(void* guard)
{
    *(int*)guard = -1; // mark "initialized" so the init block runs
}

static void __fastcall exeGuardFooter(void* guard)
{
}

static int __cdecl exeAtexit(void* fn)
{
    return 0;
}

// zeroing replacement for the represent's fallback allocator (rep+0xB75754, via
// the 0x82BA thunk): the copy-ctor treats the new object's list fields as zeroed.
static void* __fastcall repCallocNew(size_t n)
{
    static int nCalls = 0;
    nCalls++;
    if (nCalls <= 5)
        logf("[host] repCallocNew(%llu) call #%d", (unsigned long long)n, nCalls);
    return calloc(1, n);
}

static void patchAbsJmp(void* at, void* target)
{
    DWORD oldp = 0;
    if (VirtualProtect(at, 12, PAGE_EXECUTE_READWRITE, &oldp))
    {
        BYTE* p = (BYTE*)at;
        p[0] = 0x48; p[1] = 0xB8;
        *(void**)(p + 2) = target;
        p[10] = 0xFF; p[11] = 0xE0;
        VirtualProtect(at, 12, oldp, &oldp);
    }
}
static void* __fastcall exeNew(size_t n)
{
    return malloc(n);
}

static int mapGameExe(const wchar_t* exePath)
{
    HMODULE exe = LoadLibraryExW(exePath, NULL, DONT_RESOLVE_DLL_REFERENCES);
    logf("[host] JX3ClientX64.exe mapped -> %p (err=%u)", exe,
         exe ? 0 : GetLastError());
    if (exe == NULL)
        return 0;
    BYTE* b = (BYTE*)exe;
    PIMAGE_DOS_HEADER dos = (PIMAGE_DOS_HEADER)b;
    PIMAGE_NT_HEADERS nt = (PIMAGE_NT_HEADERS)(b + dos->e_lfanew);
    DWORD impRva = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress;
    PIMAGE_IMPORT_DESCRIPTOR imp = (PIMAGE_IMPORT_DESCRIPTOR)(b + impRva);
    int resolved = 0, failed = 0;
    for (; imp->Name != 0; imp++)
    {
        const char* dllName = (const char*)(b + imp->Name);
        HMODULE dep = GetModuleHandleA(dllName);
        if (dep == NULL)
            dep = LoadLibraryA(dllName);
        if (dep == NULL)
        {
            failed++;
            continue;
        }
        PIMAGE_THUNK_DATA oft = (PIMAGE_THUNK_DATA)(b + imp->OriginalFirstThunk);
        PIMAGE_THUNK_DATA ft = (PIMAGE_THUNK_DATA)(b + imp->FirstThunk);
        for (; oft->u1.AddressOfData != 0; oft++, ft++)
        {
            FARPROC fn = NULL;
            if (oft->u1.Ordinal & IMAGE_ORDINAL_FLAG)
                fn = GetProcAddress(dep, (LPCSTR)(oft->u1.Ordinal & 0xFFFF));
            else
            {
                PIMAGE_IMPORT_BY_NAME ibn =
                    (PIMAGE_IMPORT_BY_NAME)(b + oft->u1.AddressOfData);
                fn = GetProcAddress(dep, (LPCSTR)ibn->Name);
            }
            if (fn != NULL)
            {
                DWORD oldp = 0;
                if (VirtualProtect(&ft->u1.Function, 8, PAGE_READWRITE, &oldp))
                {
                    ft->u1.Function = (ULONG_PTR)fn;
                    VirtualProtect(&ft->u1.Function, 8, oldp, &oldp);
                    resolved++;
                }
            }
            else
                failed++;
        }
    }
    logf("[host] exe IAT resolved=%d failed=%d", resolved, failed);
    // NOTE: the exe's malloc/free route to the shared UCRT heap already loaded in
    // this process - no allocator patch needed (a patch caused heap corruption).
    // run the exe's CRT static initializers (arrays found in mainCRTStartup:
    // C++ .CRT$XC 0x7B9CC0-0x7BA4C0, C .CRT$XI 0x7BA4C8-0x7BA4E8)
    {
        int ran = 0, failedInit = 0;
        void** arrays[2][2] = {
            { (void**)(b + 0x7B9CC0), (void**)(b + 0x7BA4C0) },
            { (void**)(b + 0x7BA4C8), (void**)(b + 0x7BA4E8) },
        };
        // initializers that crash the host (full game subsystems); skip and log
        char noInit[8];
        int skipAll = (GetEnvironmentVariableA("RC_HOST_EXE_NOINIT", noInit,
                                               sizeof(noInit)) != 0);
        char crtOnly[8];
        int crtOnlyMode = (GetEnvironmentVariableA("RC_HOST_EXE_CRTONLY", crtOnly,
                                                   sizeof(crtOnly)) != 0);
        static const DWORD skipInit[] = { 0x843EC };
        for (int a = 0; a < 2 && !skipAll; a++)
        {
            int cppIdx = -1;
            for (void** p = arrays[a][0]; p < arrays[a][1]; p++)
            {
                if (*p == NULL)
                    continue;
                cppIdx++;
                DWORD off = (DWORD)((BYTE*)(*p) - b);
                // CRT-only mode: run just the first C++ entry (the CRT init) and
                // all C entries; skip the game's static ctors.
                if (crtOnlyMode && a == 0 && cppIdx > 0)
                {
                    logf("[host] exe init[%d] exe+0x%X SKIPPED (game ctor)", a, off);
                    continue;
                }
                int skip = 0;
                for (int k = 0; k < (int)(sizeof(skipInit) / sizeof(skipInit[0])); k++)
                    if (off == skipInit[k]) skip = 1;
                if (skip)
                {
                    logf("[host] exe init[%d] exe+0x%X SKIPPED", a, off);
                    continue;
                }
                logf("[host] exe init[%d] = exe+0x%X", a, off);
                __try { ((void (__cdecl *)(void))(*p))(); ran++; }
                __except (EXCEPTION_EXECUTE_HANDLER) { failedInit++; }
            }
        }
        logf("[host] exe static initializers ran=%d failed=%d", ran, failedInit);
    }
    g_exeModule = exe;
    return 1;
}

// headless host: never let a modal dialog block the process
static int WINAPI hookMessageBoxA(HWND h, LPCSTR text, LPCSTR caption, UINT type)
{
    logf("[host] MessageBoxA suppressed: '%s' | '%s'",
         caption ? caption : "", text ? text : "");
    return 1; // IDOK
}

static int WINAPI hookMessageBoxW(HWND h, LPCWSTR text, LPCWSTR caption, UINT type)
{
    logf("[host] MessageBoxW suppressed");
    return 1;
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

static void describeAddr(DWORD64 a, char* out, size_t n);

static LONG WINAPI vehHandler(PEXCEPTION_POINTERS ep)
{
    if (ep->ExceptionRecord->ExceptionCode == 0xC0000005 ||
        ep->ExceptionRecord->ExceptionCode == 0xC0000409)
    {
        HMODULE m = NULL;
        wchar_t path[MAX_PATH] = { 0 };
        const wchar_t* base = L"?";
        if (GetModuleHandleExW(4 /*FROM_ADDRESS*/,
                               (LPCWSTR)ep->ExceptionRecord->ExceptionAddress,
                               &m) && m != NULL)
        {
            GetModuleFileNameW(m, path, MAX_PATH);
            const wchar_t* slash = wcsrchr(path, L'\\');
            if (slash != NULL) base = slash + 1;
            logf("[VEH] exc=0x%08X at=%p in %ls+0x%llX",
                 ep->ExceptionRecord->ExceptionCode,
                 ep->ExceptionRecord->ExceptionAddress, base,
                 (unsigned long long)((DWORD64)ep->ExceptionRecord->ExceptionAddress -
                                      (DWORD64)m));
        }
        else
        {
            logf("[VEH] exc=0x%08X at=%p (module?)", ep->ExceptionRecord->ExceptionCode,
                 ep->ExceptionRecord->ExceptionAddress);
        }
        // stack trace for the first few AVs inside the represent module
        static int vehTraces = 0;
        DWORD64 fa = (DWORD64)ep->ExceptionRecord->ExceptionAddress;
        int inRep = (g_repModule != NULL && fa >= (DWORD64)g_repModule &&
                     fa < (DWORD64)g_repModule + 0x2000000);
        if (inRep && vehTraces < 5)
        {
            vehTraces++;
            void* frames[20];
            USHORT n = RtlCaptureStackBackTrace(0, 20, frames, NULL);
            char d[64];
            for (USHORT i = 0; i < n; i++)
            {
                describeAddr((DWORD64)frames[i], d, sizeof(d));
                logf("[VEH]   bt[%u] = %s", i, d);
            }
        }
    }
    else if (ep->ExceptionRecord->ExceptionCode == EXCEPTION_SINGLE_STEP &&
             g_flagWatchArmed && g_flagWatchHit < 10)
    {
        g_flagWatchHit++;
        DWORD64 rip = (DWORD64)ep->ExceptionRecord->ExceptionAddress;
        logf("[host] flag write watch #%d: rip=eng+0x%llX",
             g_flagWatchHit, (rip > (DWORD64)g_eng) ? (rip - (DWORD64)g_eng) : 0);
        ep->ContextRecord->Dr6 = 0;
        if (g_flagWatchHit >= 10)
        {
            ep->ContextRecord->Dr0 = 0;
            ep->ContextRecord->Dr7 = 0;
        }
        return EXCEPTION_CONTINUE_EXECUTION;
    }
    return EXCEPTION_CONTINUE_SEARCH;
}

// real .Sfx through the engine's own factory (client build: no AV)
static void* createRealSfx(HMODULE eng, float x, float y, float z)
{
    void* owner = NULL;
    void** g = *(void***)((BYTE*)eng + 0x2CF1038);
    if (g != NULL) owner = ((void* (__fastcall *)(void*))((*(void***)g)[8]))(g);
    char sfxPath[512];
    gbk(L"data\\source\\other\\特效\\技能\\SFX\\增益\\c纯阳坐忘.Sfx", sfxPath, sizeof(sfxPath));
    void* sfx = NULL;
    void* out = NULL;
    float mtx[16] = { 1,0,0,0, 0,1,0,0, 0,0,1,0, x, y, z, 1 };
    DWORD exc = 0;
    typedef void* (__fastcall *CreateFn)(void*, const char*, void*, void*, void*,
                                         void*, int, void*);
    __try { sfx = ((CreateFn)((BYTE*)eng + 0xBE5610))(owner, sfxPath, NULL, NULL,
                                                     NULL, mtx, 0, &out); }
    __except (exc = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { sfx = NULL; }
    logf("[host] .Sfx create (%.0f,%.0f,%.0f) -> obj=%p exc=0x%08X owner=%p",
         x, y, z, sfx, (unsigned)exc, owner);
    return sfx;
}

static void castAbility(int idx, void* actor, void* ctrl, HMODULE eng)
{
    if (idx < 0 || idx >= g_abilCount || actor == NULL) return;
    typedef long (__fastcall *InitAttachTaniFn)(void*, const char*, unsigned);
    long iat = ((InitAttachTaniFn)((BYTE*)eng + 0x83A6F0))(actor, g_abil[idx], 0);
    void* anim = *(void**)((BYTE*)actor + 0x368);
    logf("[host] cast[%d] '%s' _InitAttachTani=0x%08X anim=%p",
         idx, g_abilName[idx], (unsigned)iat, anim);
    if (anim != NULL && ctrl != NULL)
    {
        typedef long (__fastcall *StartAnimFn)(void*, void*, int, float, unsigned,
                                               unsigned, void*, void*, void*);
        long src = ((StartAnimFn)((BYTE*)eng + 0xBC1C70))(ctrl, anim, 0, 1.0f, 0, 0,
                                                         NULL, NULL, NULL);
        logf("[host] cast[%d] StartAnimation=0x%08X", idx, (unsigned)src);
    }
    createRealSfx(eng, 23334.0f, 762.0f, 24224.0f);
}

// render-proxy acquisition hook: proves whether the engine asks for a render
// proxy per scene object (and for which GUID)
static BYTE g_arpSaved[32];
static BYTE* g_arpTramp = NULL;
typedef long (__fastcall *ArpFn)(void*, void*, unsigned char, void**);
static long __fastcall hookAcquireProxy(void* self, void* scene, unsigned char b, void** out)
{
    const char* guid = NULL;
    __try
    {
        guid = ((const char* (__fastcall *)(void*))((BYTE*)g_eng + 0x9BBC60))(self);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { guid = NULL; }
    long r = ((ArpFn)g_arpTramp)(self, scene, b, out);
    printf("[host] AcquireRenderActorProxy guid=%s b=%u -> 0x%08X\n",
           guid ? guid : "(?)", (unsigned)b, (unsigned)r);
    fflush(stdout);
    return r;
}

// mesh render-data factory (0xC4D7F0) trace: who builds render data, for which mesh
static BYTE g_facSaved[32];
static BYTE* g_facTramp = NULL;
static void* __fastcall hookMeshFactory(void* a1, void* a2, void* a3, void* a4)
{
    void* r = ((void* (__fastcall *)(void*, void*, void*, void*))g_facTramp)(a1, a2, a3, a4);
    static int n = 0;
    if (n < 10)
    {
        printf("[host] meshFactory a1=%p a2=%p a3=%p a4=%p -> %p\n", a1, a2, a3, a4, r);
        void* frames[12];
        USHORT f = RtlCaptureStackBackTrace(1, 12, frames, NULL);
        for (USHORT i = 0; i < f; i++)
        {
            DWORD64 a = (DWORD64)frames[i];
            printf("[host]   mfbt[%u] = eng+0x%llX\n", i,
                   (a > (DWORD64)g_eng) ? (a - (DWORD64)g_eng) : 0);
        }
        fflush(stdout);
        n++;
    }
    return r;
}

// render-data build (0xC4DE80, called from the batch at 0xD8AF40): does f1's mesh
// reach it, and does it get GPU buffers ([mesh+0x4a8]/[mesh+0x4b0])?
static BYTE g_bldSaved[32];
static BYTE* g_bldTramp = NULL;
static void tryPrintName(const char* tag, void* p)
{
    if (p == NULL) return;
    __try
    {
        char* np = *(char**)((BYTE*)p + 8);
        if (np == NULL) return;
        char name[160];
        int k = 0;
        while (k < 150 && np[k] != 0) { name[k] = np[k]; k++; }
        name[k] = 0;
        if (k > 8 && strstr(name, "mesh") != NULL)
            printf("[host] build %s=%p name='%s'\n", tag, p, name);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { }
}
static void* __fastcall hookBuildData(void* a1, void* a2, void* a3, void* a4)
{
    static int n = 0;
    if (n < 20)
    {
        n++;
        printf("[host] buildData a1=%p a2=%p a3=%p a4=%p\n", a1, a2, a3, a4);
        tryPrintName("a1", a1);
        tryPrintName("a2", a2);
        tryPrintName("a3", a3);
        fflush(stdout);
    }
    void* r = ((void* (__fastcall *)(void*, void*, void*, void*))g_bldTramp)(a1, a2, a3, a4);
    __try
    {
        void* cand[3] = { a1, a2, a3 };
        for (int i = 0; i < 3; i++)
        {
            char* np = (cand[i] != NULL) ? *(char**)((BYTE*)cand[i] + 8) : NULL;
            if (np != NULL && strstr(np, "f1_3094") != NULL)
                printf("[host] buildData f1 mesh=%p -> %p flag484=0x%02X buf1=%p buf2=%p\n",
                       cand[i], r, *(unsigned char*)((BYTE*)cand[i] + 0x484),
                       *(void**)((BYTE*)cand[i] + 0x4a8),
                       *(void**)((BYTE*)cand[i] + 0x4b0));
        }
        fflush(stdout);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { }
    return r;
}

// KeepMeshData registry readout: count distinguishes loose tab (1367) vs PakV4
// copy (1364) - answers whether the builder reads the loose file at all.
static void dumpRegistryCount(const char* tag)
{
    __try
    {
        void* reg = *(void**)((BYTE*)g_eng + 0x2CFA548);
        if (reg != NULL)
            logf("[host] registry %s: reg=%p live=%llu tomb=%llu cap=%llu", tag, reg,
                 *(unsigned long long*)((BYTE*)reg + 0x28),
                 *(unsigned long long*)((BYTE*)reg + 0x20),
                 *(unsigned long long*)((BYTE*)reg + 0x30));
        else
            logf("[host] registry %s: null", tag);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] registry %s: fault", tag); }
}

// keep-mesh-data checker (0xC4D430): decides whether a NormalMesh's render data
// is built; props (fd0==0) and KeepMeshData_FileList models pass, character
// meshes from the loose tree do not. Log calls; let f1_3094 pass for the test.
static BYTE g_chkSaved[32];
static BYTE* g_chkTramp = NULL;
static int __fastcall hookKeepCheck(void* mesh)
{
    int r = ((int (__fastcall *)(void*))g_chkTramp)(mesh);
    char name[256];
    name[0] = 0;
    __try
    {
        char* np = *(char**)((BYTE*)mesh + 8);
        if (np != NULL)
        {
            int k = 0;
            while (k < 250 && np[k] != 0) { name[k] = np[k]; k++; }
            name[k] = 0;
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { }
    static int n = 0;
    if (n < 6 || strstr(name, "f1_3094") != NULL)
    {
        void* reg = NULL;
        __try { reg = *(void**)((BYTE*)g_eng + 0x2CFA548); }
        __except (EXCEPTION_EXECUTE_HANDLER) { reg = NULL; }
        printf("[host] keepCheck '%s' -> %d (reg=%p live=%llu tomb=%llu)\n", name, r, reg,
               (reg != NULL) ? *(unsigned long long*)((BYTE*)reg + 0x28) : 0,
               (reg != NULL) ? *(unsigned long long*)((BYTE*)reg + 0x20) : 0);
        void* frames[10];
        USHORT f = RtlCaptureStackBackTrace(1, 10, frames, NULL);
        for (USHORT i = 0; i < f; i++)
        {
            DWORD64 a = (DWORD64)frames[i];
            printf("[host]   kcbt[%u] = eng+0x%llX\n", i,
                   (a > (DWORD64)g_eng) ? (a - (DWORD64)g_eng) : 0);
        }
        fflush(stdout);
        if (n < 6) n++;
    }
    // Host adaptation (test): the KeepMeshData registry is PakV4-sourced and does
    // not list character meshes, so their render data is never built. Force-keep
    // the f1_3094 character mesh so the engine's own render-data build runs.
    if (strstr(name, "f1_3094") != NULL)
        return 1;
    return r;
}

// scene-object trace hooks (A1): ctor / Init(entityInfo) / SetSfxOrPss
static BYTE g_socSaved[32];
static BYTE* g_socTramp = NULL;
static long __fastcall hookSceneObjectCtor(void* self)
{
    long r = ((long (__fastcall *)(void*))g_socTramp)(self);
    static int n = 0;
    if (n < 60) { printf("[host] SceneObject::ctor self=%p\n", self); fflush(stdout); n++; }
    return r;
}

static BYTE g_soiSaved[32];
static BYTE* g_soiTramp = NULL;
static void* g_entitySO = NULL;
static void* g_propSO = NULL;
static void* g_soList[1200];
static int g_soCount = 0;

// classification method (engine 0x852E00): returns bit 8 of [this-0x68C] on the
// adjusted SceneActor interface; FetchModelFromActor turns it into IsPlayerObject.
// Hook logs calls and lets us test forcing 0 for the sandbox entity's actor.
static BYTE g_ispSaved[32];
static BYTE* g_ispTramp = NULL;
static long g_entityAdjThis = 0;
static int g_ispSeen = 0;
static int __fastcall hookIsPlayer(void* thisAdj)
{
    int r = ((int (__fastcall *)(void*))g_ispTramp)(thisAdj);
    if (g_entityAdjThis == 0 && g_entitySO != NULL)
    {
        __try
        {
            void* sceneActor = *(void**)((BYTE*)g_entitySO + 0x100);
            BYTE* a8 = (sceneActor != NULL) ? *(BYTE**)((BYTE*)sceneActor + 8) : NULL;
            if (a8 != NULL)
            {
                int off = *(int*)(a8 + 4);
                BYTE* iface = (BYTE*)sceneActor + 8 + off;
                g_entityAdjThis = (long)(iface - *(int*)(iface - 4));
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { }
    }
    if (g_ispSeen < 12 || (g_entityAdjThis != 0 && (long)thisAdj == g_entityAdjThis))
    {
        g_ispSeen++;
        logf("[host] isPlayer(this=%p) -> %d (entityAdj=%p)", thisAdj, r,
             (void*)g_entityAdjThis);
    }
    return r;
}


static long __fastcall hookSceneObjectInit(void* self, void* entityInfo)
{
    long r = ((long (__fastcall *)(void*, void*))g_soiTramp)(self, entityInfo);
    const char* guid = NULL;
    __try { guid = ((const char* (__fastcall *)(void*))((BYTE*)g_eng + 0x9BBC60))(self); }
    __except (EXCEPTION_EXECUTE_HANDLER) { guid = NULL; }
    printf("[host] SceneObject::Init self=%p info=%p -> 0x%08X guid=%s\n",
           self, entityInfo, (unsigned)r, guid ? guid : "(?)");
    fflush(stdout);
    if (r >= 0 && guid != NULL && guid[0] != 0)
    {
        if (strstr(guid, "aaaaaaaa-1111-2222-3333") != NULL)
        {
            g_entitySO = self;
            // catch the async flag classifier: watch writes to [self+0x10] on this
            // thread (Dr0, 4-byte write) - the VEH logs the writer's RIP
            char watchFlag[8];
            if (GetEnvironmentVariableA("RC_HOST_WATCH", watchFlag, sizeof(watchFlag)) != 0)
            __try
            {
                CONTEXT ctx;
                memset(&ctx, 0, sizeof(ctx));
                ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS;
                if (GetThreadContext(GetCurrentThread(), &ctx))
                {
                    ctx.Dr0 = (DWORD64)((BYTE*)self + 0x10);
                    ctx.Dr7 = (ctx.Dr7 & ~0x000F0001ULL) | 0x000D0001ULL;
                    if (SetThreadContext(GetCurrentThread(), &ctx))
                    {
                        g_flagWatchArmed = 1;
                        logf("[host] flag write watch armed on %p", (void*)((BYTE*)self + 0x10));
                    }
                }
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] flag watch arm fault"); }
        }
        else if (g_propSO == NULL)
            g_propSO = self;
        if (g_soCount < 1200)
            g_soList[g_soCount++] = self;
    }
    return r;
}

// engine's game-layer interface: SetModelHandleCallBack(engine, loadedCb,
// movedCb, unloadedCb) at 0x8CA400. The engine invokes loadedCb from
// OnSceneActorLoadedCallBack when a scene actor's model reaches the scene.
typedef long (__fastcall *ModelHandleCbFn)(void*, unsigned char, int, void*,
                                           const char*, void*, void*, void*,
                                           void*, int, unsigned long long);
static int g_mhCount = 0;
static long __fastcall onModelHandle(void* actor, unsigned char loaded, int a3,
                                     void* mesh, const char* path, void* mtx,
                                     void* v1, void* v2, void* param, int a10,
                                     unsigned long long a11)
{
    if (path != NULL && g_mhCount < 200)
    {
        printf("[host] ModelHandleCb actor=%p loaded=%d mesh=%p path=%s\n",
               actor, (int)loaded, mesh, path);
        fflush(stdout);
        g_mhCount++;
    }
    return 0;
}

// render-data readiness flag the OnSceneActorLoaded handler gates on:
// model->vt[0x1a8]() -> z; rdi = [z+0x10]; ready = byte[rdi+0x484] & 1
static int modelReadyFlag(void* model)
{
    int flag = -1;
    __try
    {
        if (model != NULL)
        {
            void** mvt = *(void***)model;
            void* z = ((void* (__fastcall *)(void*))mvt[0x1a8 / 8])(model);
            if (z != NULL)
            {
                void* rdi = *(void**)((BYTE*)z + 0x10);
                if (rdi != NULL)
                    flag = *(unsigned char*)((BYTE*)rdi + 0x484);
            }
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { flag = -2; }
    return flag;
}

static void dumpSceneObject(const char* tag, void* so)
{
    if (so == NULL) { logf("[host] SO %s = null", tag); return; }
    __try
    {
        unsigned flags = *(unsigned*)((BYTE*)so + 0x10);
        void* actor = *(void**)((BYTE*)so + 0x100);
        void* model = (actor != NULL) ? *(void**)((BYTE*)actor + 0x358) : NULL;
        const char* guid = ((const char* (__fastcall *)(void*))((BYTE*)g_eng + 0x9BBC60))(so);
        float wm[16];
        memset(wm, 0, sizeof(wm));
        ((void (__fastcall *)(void*, float*))((BYTE*)g_eng + 0x9BA510))(so, wm);
        void* mbegin = *(void**)((BYTE*)so + 0xd8);
        void* mend = *(void**)((BYTE*)so + 0xe0);
        logf("[host] SO %s self=%p flags=0x%08X guid=%s actor=%p model=%p ready484=0x%02X mvec=%p..%p pos=(%.1f,%.1f,%.1f)",
             tag, so, flags, guid ? guid : "(?)", actor, model, modelReadyFlag(model),
             mbegin, mend, wm[12], wm[13], wm[14]);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] SO %s dump fault", tag); }
}

// dump the scene objects nearest to the camera (rendered props) for comparison
static void dumpLoadedObjects(void)
{
    int withActor = 0;
    float cx = 23334.0f, cz = 24224.0f;
    int order[1200];
    float dist[1200];
    int n = 0;
    for (int i = 0; i < g_soCount; i++)
    {
        __try
        {
            float wm[16];
            memset(wm, 0, sizeof(wm));
            ((void (__fastcall *)(void*, float*))((BYTE*)g_eng + 0x9BA510))(g_soList[i], wm);
            if (*(void**)((BYTE*)g_soList[i] + 0x100) != NULL) withActor++;
            float dx = wm[12] - cx, dz = wm[14] - cz;
            dist[n] = dx * dx + dz * dz;
            order[n] = i;
            n++;
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { }
    }
    for (int a = 0; a < n; a++)
        for (int b = a + 1; b < n; b++)
            if (dist[b] < dist[a]) { float td = dist[a]; dist[a] = dist[b]; dist[b] = td;
                                     int ti = order[a]; order[a] = order[b]; order[b] = ti; }
    int shown = 0;
    for (int a = 0; a < n && shown < 10; a++)
    {
        void* so = g_soList[order[a]];
        __try
        {
            const char* guid = ((const char* (__fastcall *)(void*))((BYTE*)g_eng + 0x9BBC60))(so);
            void* actor = *(void**)((BYTE*)so + 0x100);
            logf("[host] nearSO dist=%.0f flags=0x%08X guid=%s actor=%p",
                 dist[a] > 0 ? sqrtf(dist[a]) : 0.0f,
                 *(unsigned*)((BYTE*)so + 0x10), guid ? guid : "(?)", actor);
            shown++;
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { }
    }
    logf("[host] SO total=%d withActor=%d", g_soCount, withActor);
}

static BYTE g_sfpSaved[32];
static BYTE* g_sfpTramp = NULL;
static long __fastcall hookSetSfxOrPss(void* self, int on)
{
    long r = ((long (__fastcall *)(void*, int))g_sfpTramp)(self, on);
    const char* guid = NULL;
    __try { guid = ((const char* (__fastcall *)(void*))((BYTE*)g_eng + 0x9BBC60))(self); }
    __except (EXCEPTION_EXECUTE_HANDLER) { guid = NULL; }
    printf("[host] SetSfxOrPss self=%p on=%d guid=%s\n", self, on, guid ? guid : "(?)");
    fflush(stdout);
    return r;
}

// represent-init probe: run the singleton lifecycle init on a worker thread and
// capture its stack if it blocks (A1 follow-up)
static volatile LONG g_repInitDone = 0;
static volatile LONG g_repProbePending = 0;
static volatile LONG g_rlProbePending = 0;
static void* g_repSingleton = NULL;
static void* g_rlCtx = NULL;
static void* g_lastPet = NULL;
static void* g_entityCtrl = NULL;
static void* g_so3World = NULL;
static void* g_rlLoader = NULL;
static void* g_ifMgr = NULL;
static void* g_ifModelMgr = NULL;
static void* g_ifXLogic = NULL;
static void* g_ifSceneResp = NULL;
static void* g_ifConv = NULL;
static void* g_ifMovie = NULL;
static void* g_ifUI = NULL;

// logic-module init watchdog: if CreateJX3LogicOperation wedges, suspend the main
// thread after 20 s, dump its stack (module+offset) and exit.
static volatile LONG g_logicDone = 0;
static HANDLE g_mainThreadHandle = NULL;
static HANDLE g_logicThread = NULL;
static HMODULE g_logicModule = NULL;
static HMODULE g_x3dModule = NULL;
static HMODULE g_luaModule = NULL;

static void describeAddr(DWORD64 a, char* out, size_t n)
{
    struct { HMODULE m; const char* name; } mods[5] = {
        { g_eng, "eng" }, { g_repModule, "rep" }, { g_logicModule, "logic" },
        { g_x3dModule, "x3d" }, { g_luaModule, "lua" },
    };
    for (int i = 0; i < 5; i++)
    {
        if (mods[i].m != NULL && a >= (DWORD64)mods[i].m &&
            a < (DWORD64)mods[i].m + 0x8000000)
        {
            sprintf_s(out, n, "%s+0x%llX", mods[i].name,
                      (unsigned long long)(a - (DWORD64)mods[i].m));
            return;
        }
    }
    sprintf_s(out, n, "0x%llX", (unsigned long long)a);
}

// The logic init spawns its own thread and waits - run the whole init on a worker
// thread so the main thread keeps the engine pumping (the game calls it with the
// engine frame loop running).
static volatile LONG g_logicStarted = 0;
static DWORD WINAPI logicInitThread(LPVOID param)
{
    typedef void* (__fastcall *CreateLogicFn)(const char*, void*, const char*);
    CreateLogicFn cl = (CreateLogicFn)param;
    static unsigned char dummyFactory[0x100];
    memset(dummyFactory, 0, sizeof(dummyFactory));
    // The module formats paths as "%sbin64\%s" - the base path must end with a
    // backslash or LoadConvertModule fails and its error MessageBoxA blocks.
    char rootCopy[MAX_PATH];
    strcpy_s(rootCopy, MAX_PATH, g_rootA);
    size_t rl = strlen(rootCopy);
    if (rl > 0 && rootCopy[rl - 1] != '\\' && rl + 1 < MAX_PATH)
    {
        rootCopy[rl] = '\\';
        rootCopy[rl + 1] = 0;
    }
    logf("[host] logic init thread: CreateJX3LogicOperation('%s')", rootCopy);
    __try
    {
        // The full entry (needed: its pre-init sets the log/recorder state that
        // InitLogic's 0x160F90 getter requires). It creates the world/loader/UI,
        // then may fault in the game-context step (which expects the represent to
        // be Init'd already - our frame60 Phase B handles that).
        void* op = cl(rootCopy, dummyFactory, "reborn_client_host");
        logf("[host] logic init thread: CreateJX3LogicOperation -> %p", op);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    { logf("[host] logic init thread fault"); }
    if (g_logicModule != NULL)
    {
        void* world = *(void**)((BYTE*)g_logicModule + 0x9C1320);
        void* loader = *(void**)((BYTE*)g_logicModule + 0xA01D98);
        logf("[host] logic init thread: g_pSO3World=%p g_pRLLoader=%p", world, loader);
        g_so3World = world;
    }
    g_logicDone = 1;
    return 0;
}

static int isCodeAddr(DWORD64 a, char* out, size_t n)
{
    struct { HMODULE m; const char* name; } mods[5] = {
        { g_eng, "eng" }, { g_repModule, "rep" }, { g_logicModule, "logic" },
        { g_x3dModule, "x3d" }, { g_luaModule, "lua" },
    };
    for (int i = 0; i < 5; i++)
    {
        if (mods[i].m == NULL)
            continue;
        BYTE* base = (BYTE*)mods[i].m;
        if (a < (DWORD64)base || a >= (DWORD64)base + 0x8000000)
            continue;
        __try
        {
            PIMAGE_DOS_HEADER dos = (PIMAGE_DOS_HEADER)base;
            PIMAGE_NT_HEADERS nt = (PIMAGE_NT_HEADERS)(base + dos->e_lfanew);
            PIMAGE_SECTION_HEADER sec = IMAGE_FIRST_SECTION(nt);
            for (int k = 0; k < nt->FileHeader.NumberOfSections; k++)
            {
                DWORD64 lo = (DWORD64)base + sec[k].VirtualAddress;
                DWORD64 hi = lo + sec[k].Misc.VirtualSize;
                if (a >= lo && a < hi &&
                    (sec[k].Characteristics & IMAGE_SCN_MEM_EXECUTE))
                {
                    sprintf_s(out, n, "%s+0x%llX", mods[i].name,
                              (unsigned long long)(a - (DWORD64)base));
                    return 1;
                }
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { }
        return 0;
    }
    return 0;
}

static DWORD WINAPI logicWatchdog(LPVOID)
{
    Sleep(20000);
    if (g_logicDone)
        return 0;
    logf("[host] logic watchdog: still wedged after 20s - dumping main-thread stack");
    if (g_mainThreadHandle != NULL)
    {
        SuspendThread(g_mainThreadHandle);
        CONTEXT ctx;
        memset(&ctx, 0, sizeof(ctx));
        ctx.ContextFlags = CONTEXT_CONTROL | CONTEXT_INTEGER;
        if (GetThreadContext(g_mainThreadHandle, &ctx))
        {
            char mdesc[64];
            describeAddr((DWORD64)ctx.Rip, mdesc, sizeof(mdesc));
            logf("[host] watchdog rip=%p (%s) rsp=0x%llX rbp=0x%llX",
                 (void*)ctx.Rip, mdesc, (unsigned long long)ctx.Rsp,
                 (unsigned long long)ctx.Rbp);
            char desc[64];
            DWORD64* sp = (DWORD64*)ctx.Rsp;
            for (int i = 0; i < 512; i++)
            {
                DWORD64 v = 0;
                __try { v = sp[i]; }
                __except (EXCEPTION_EXECUTE_HANDLER)
                { logf("[host]   stack read fault at %d", i); break; }
                describeAddr(v, desc, sizeof(desc));
                logf("[host]   stack[%d] = %s", i, desc);
            }
        }
        ResumeThread(g_mainThreadHandle);
    }
    logf("[host] watchdog done");
    ExitProcess(0);
    return 0;
}
static void* g_lastEntity = NULL;

// entity factory (represent 0xAEDFD0 CreateEntityByName): capture created entities
static BYTE g_efSaved[32];
static BYTE* g_efTramp = NULL;
static void* __fastcall hookEntityFactory(void* name)
{
    void* r = ((void* (__fastcall *)(void*))g_efTramp)(name);
    if (name != NULL)
    {
        logf("[host] entityFactory('%s') -> %p", (const char*)name, r);
        g_lastEntity = r;
    }
    return r;
}

static DWORD WINAPI repInitThread(LPVOID)
{
    __try
    {
        void** rvt = *(void***)g_repSingleton;
        ((long (__fastcall *)(void*))rvt[1])(g_repSingleton);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { }
    InterlockedExchange(&g_repInitDone, 1);
    return 0;
}

static BOOL CALLBACK enumProcHostWnd(HWND h, LPARAM lp)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(h, &pid);
    if (pid == GetCurrentProcessId())
    {
        char cls[128] = { 0 };
        char title[256] = { 0 };
        GetClassNameA(h, cls, sizeof(cls) - 1);
        GetWindowTextA(h, title, sizeof(title) - 1);
        printf("[host]   window hwnd=%p class='%s' title='%s' visible=%d\n",
               h, cls, title, IsWindowVisible(h));
        fflush(stdout);
    }
    return TRUE;
}

static volatile LONG g_repSlot = -1;
static DWORD WINAPI repSlotThread(LPVOID)
{
    __try
    {
        void** rvt = *(void***)g_repSingleton;
        ((long (__fastcall *)(void*))rvt[g_repSlot])(g_repSingleton);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { }
    InterlockedExchange(&g_repInitDone, 1);
    return 0;
}

static void probeRepresentSlots(void)
{
    typedef void* (__cdecl *GetEcsFn)(void);
    GetEcsFn ge = (GetEcsFn)GetProcAddress(g_repModule, "GetRepresentECSRootEntity");
    int slots[] = { 2, 3, 4, 5, 6, 7, 11, 12, 13, 14 };
    for (int i = 0; i < 10; i++)
    {
        InterlockedExchange(&g_repInitDone, 0);
        g_repSlot = slots[i];
        HANDLE th = CreateThread(NULL, 0, repSlotThread, NULL, 0, NULL);
        int waited = 0;
        while (waited < 3000 && InterlockedCompareExchange(&g_repInitDone, 0, 0) == 0)
        {
            MSG msg;
            while (PeekMessageA(&msg, NULL, 0, 0, PM_REMOVE))
            {
                TranslateMessage(&msg);
                DispatchMessageA(&msg);
            }
            Sleep(50);
            waited += 50;
        }
        void* ecs = (ge != NULL) ? ge() : NULL;
        logf("[host] represent slot %d done=%ld %dms ecs=%p", slots[i],
             (long)InterlockedCompareExchange(&g_repInitDone, 0, 0), waited, ecs);
        if (ecs != NULL)
        {
            logf("[host] ECS root found after slot %d", slots[i]);
            break;
        }
        if (InterlockedCompareExchange(&g_repInitDone, 0, 0) == 0)
            logf("[host]   slot %d hung (thread left wedged)", slots[i]);
        CloseHandle(th);
    }
}

// ECS hierarchy builder (JX3RepresentX64 0x924B20): creates the "root"/"reference"
// entities and stores them to globals 0xF512A8/0xF512B0 (GetRepresentECSRootEntity
// at 0x2680 returns 0xF512A8). The exe module system normally wires this at runtime;
// call it directly after the singleton lifecycle init.
static void probeRepresentHierarchy(void)
{
    if (g_repModule == NULL) return;
    // ECS world create (0x920D10): stores the world object at global 0xF51298,
    // which the entity factory (0x920C40) requires. Normally created by the
    // module init that faults in this host.
    __try
    {
        void* world = *(void**)((BYTE*)g_repModule + 0xF51298);
        logf("[host] represent ECS world before = %p", world);
        if (world == NULL)
        {
            struct { void* b; void* e; void* c; } vec = { NULL, NULL, NULL };
            ((void (__fastcall *)(void*))((BYTE*)g_repModule + 0x920D10))(&vec);
            logf("[host] world create called: vec=%p..%p..%p world=%p", vec.b, vec.e, vec.c,
                 *(void**)((BYTE*)g_repModule + 0xF51298));
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] world create fault"); }
    typedef void (__cdecl *BuildFn)(void);
    __try
    {
        ((BuildFn)((BYTE*)g_repModule + 0x924B20))();
        logf("[host] represent hierarchy builder (0x924B20) called");
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    { logf("[host] represent hierarchy builder fault"); }
    // KRL entity/component creation: CreateEntity(world, &name, type=1) at
    // 0x2EA6C0, CreateComponent(entity, type=2) at 0x2EA580. The factory hook
    // captures the created entity pointer. Only when the hierarchy is healthy.
    void* worldNow = *(void**)((BYTE*)g_repModule + 0xF512A8);
    if (worldNow == NULL) return;
    __try
    {
        void* world = *(void**)((BYTE*)g_repModule + 0xF51298);
        const char* name = "player";
        char ce = ((char (__fastcall *)(void*, void*, int))
                   ((BYTE*)g_repModule + 0x2EA6C0))(world, (void*)&name, 1);
        logf("[host] KRL CreateEntity(world=%p, 'player', 1) -> %d entity=%p",
             world, (int)ce, g_lastEntity);
        if (g_lastEntity != NULL)
        {
            char cc = ((char (__fastcall *)(void*, int))
                       ((BYTE*)g_repModule + 0x2EA580))(g_lastEntity, 2);
            logf("[host] KRL CreateComponent(entity=%p, 2) -> %d", g_lastEntity, (int)cc);
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    { logf("[host] KRL entity/component create fault"); }
    __try
    {
        void* root = *(void**)((BYTE*)g_repModule + 0xF512A8);
        void* ref = *(void**)((BYTE*)g_repModule + 0xF512B0);
        logf("[host] represent globals: root=%p reference=%p", root, ref);
        typedef void* (__cdecl *GetEcsFn)(void);
        GetEcsFn ge = (GetEcsFn)GetProcAddress(g_repModule, "GetRepresentECSRootEntity");
        logf("[host] GetRepresentECSRootEntity -> %p", (ge != NULL) ? ge() : NULL);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] represent globals query fault"); }
}

static void probeRepresentInit(void)
{
    if (g_repSingleton == NULL) return;
    HANDLE th = CreateThread(NULL, 0, repInitThread, NULL, 0, NULL);
    if (th == NULL) return;
    int waited = 0;
    while (waited < 6000 && InterlockedCompareExchange(&g_repInitDone, 0, 0) == 0)
    {
        MSG msg;
        while (PeekMessageA(&msg, NULL, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessageA(&msg);
        }
        Sleep(50);
        waited += 50;
    }
    logf("[host] represent init done=%ld after %dms",
         (long)InterlockedCompareExchange(&g_repInitDone, 0, 0), waited);
    if (InterlockedCompareExchange(&g_repInitDone, 0, 0) != 0)
    {
        __try
        {
            typedef void* (__cdecl *GetEcsFn)(void);
            GetEcsFn ge = (GetEcsFn)GetProcAddress(g_repModule, "GetRepresentECSRootEntity");
            void* ecs = (ge != NULL) ? ge() : NULL;
            logf("[host] ECS root after init -> %p", ecs);
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] ECS root query fault"); }
        probeRepresentHierarchy();
        probeRepresentSlots();
        return;
    }
    if (InterlockedCompareExchange(&g_repInitDone, 0, 0) == 0)
    {
        SuspendThread(th);
        CONTEXT ctx;
        memset(&ctx, 0, sizeof(ctx));
        ctx.ContextFlags = CONTEXT_CONTROL;
        if (GetThreadContext(th, &ctx))
        {
            printf("[host] represent init HUNG rip=0x%llX rsp=0x%llX\n",
                   (unsigned long long)ctx.Rip, (unsigned long long)ctx.Rsp);
            DWORD64* sp = (DWORD64*)ctx.Rsp;
            int shown = 0;
            for (int i = 0; i < 512 && shown < 20; i++)
            {
                DWORD64 v = 0;
                __try { v = sp[i]; } __except (EXCEPTION_EXECUTE_HANDLER) { break; }
                if (v > (DWORD64)g_repModule && v < (DWORD64)g_repModule + 0x8000000)
                {
                    printf("[host]   stack[%d] rep+0x%llX\n", i, v - (DWORD64)g_repModule);
                    shown++;
                }
                else if (v > (DWORD64)g_eng && v < (DWORD64)g_eng + 0x8000000)
                {
                    printf("[host]   stack[%d] eng+0x%llX\n", i, v - (DWORD64)g_eng);
                    shown++;
                }
            }
            fflush(stdout);
        }
        ResumeThread(th);
        logf("[host] windows of this process at hang:");
        EnumWindows(enumProcHostWnd, 0);
        // the thread is wedged; exit the process cleanly after logging
        logf("[host] represent init wedged - terminating probe");
        ExitProcess(0);
    }
}

// actor-loaded callback capture: dump the real scene-node param struct the engine uses
static BYTE g_alcSaved[48];
static BYTE* g_alcTramp = NULL;
static int g_alcSeen = 0;
static void* g_f1Model = NULL;
static void* g_propModel = NULL;
static void* g_npcModel = NULL;
static int g_f1CtrlDone = 0;

static void dumpMeshState(const char* tag, void* model)
{
    if (model == NULL) { logf("[host] mesh %s = null", tag); return; }
    __try
    {
        void** mvt = *(void***)model;
        void* z = ((void* (__fastcall *)(void*))mvt[0x1a8 / 8])(model);
        void* rdi = (z != NULL) ? *(void**)((BYTE*)z + 0x10) : NULL;
        void* fd = (rdi != NULL) ? *(void**)((BYTE*)rdi + 0x18) : NULL;
        int fd0 = (fd != NULL) ? *(int*)fd : -1;
        int flag = (rdi != NULL) ? *(unsigned char*)((BYTE*)rdi + 0x484) : -1;
        char name[256];
        name[0] = 0;
        if (rdi != NULL)
        {
            char* np = *(char**)((BYTE*)rdi + 8);
            if (np != NULL)
            {
                int k = 0;
                while (k < 250 && np[k] != 0) { name[k] = np[k]; k++; }
                name[k] = 0;
            }
        }
        void* buf1 = (rdi != NULL) ? *(void**)((BYTE*)rdi + 0x4a8) : NULL;
        void* buf2 = (rdi != NULL) ? *(void**)((BYTE*)rdi + 0x4b0) : NULL;
        logf("[host] mesh %s model=%p z=%p rdi=%p fd=%p fd0=%d flag484=0x%02X buf1=%p buf2=%p name='%s'", tag,
             model, z, rdi, fd, fd0, flag, buf1, buf2, name);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] mesh %s fault", tag); }
}
typedef void (__fastcall *ActorLoadedCbFn)(void*, void*, unsigned char, int, void*,
                                           const char*, void*, void*, int,
                                           unsigned long long);
static void __fastcall hookActorLoaded(void* engine, void* actor, unsigned char loaded,
                                       int a4, void* model, const char* path, void* mtx,
                                       void* param, int a9, unsigned long long a10)
{
    if (path != NULL)
    {
        printf("[host] OnSceneActorLoaded path=%s loaded=%d actor=%p model=%p param=%p\n",
               path, loaded, actor, model, param);
        if (strstr(path, "f1_3094") != NULL && g_f1Model == NULL)
            g_f1Model = model;

        if (strstr(path, "maps_source") != NULL && g_propModel == NULL)
            g_propModel = model;
        if (strstr(path, "a303") != NULL && g_npcModel == NULL)
            g_npcModel = model;
        if (strstr(path, "player") != NULL || strstr(path, "f1_3094") != NULL ||
            g_alcSeen < 25)
        {
            g_alcSeen++;
            __try
            {
                int type = -1, ebp = -1, flag484 = -1;
                void* z = NULL;
                void* rdi = NULL;
                if (model != NULL)
                {
                    void** mvt = *(void***)model;
                    type = ((int (__fastcall *)(void*))mvt[0x118 / 8])(model);
                    z = ((void* (__fastcall *)(void*))mvt[0x1a8 / 8])(model);
                    if (z != NULL)
                    {
                        rdi = *(void**)((BYTE*)z + 0x10);
                        if (rdi != NULL)
                            flag484 = *(unsigned char*)((BYTE*)rdi + 0x484);
                    }
                }
                if (actor != NULL)
                {
                    void** avt = *(void***)actor;
                    void* x = ((void* (__fastcall *)(void*))avt[0xbc0 / 8])(actor);
                    if (x != NULL)
                    {
                        void** xvt = *(void***)x;
                        void* y = ((void* (__fastcall *)(void*))xvt[0x40 / 8])(x);
                        if (y != NULL)
                        {
                            void** yvt = *(void***)y;
                            ebp = ((int (__fastcall *)(void*))yvt[0x68 / 8])(y);
                        }
                    }
                    // resolve the classification methods on the scene object's
                    // SceneActor wrapper ([so+0x100], the object FetchModelFromActor
                    // classifies): MSVC adjustor pattern -> iface; vt[0x130]/vt[0x120]
                    // decide IsPlayerObject / IsMainCharactor.
                    __try
                    {
                        void* sceneActor = (g_entitySO != NULL)
                            ? *(void**)((BYTE*)g_entitySO + 0x100) : NULL;
                        BYTE* a8 = (sceneActor != NULL) ? *(BYTE**)((BYTE*)sceneActor + 8) : NULL;
                        if (a8 != NULL)
                        {
                            int off = *(int*)(a8 + 4);
                            BYTE* iface = (BYTE*)sceneActor + 8 + off;
                            void** ivt = *(void***)iface;
                            void* f130 = ivt[0x130 / 8];
                            void* f120 = ivt[0x120 / 8];
                            int r130 = ((int (__fastcall *)(void*))f130)(iface);
                            int r120 = ((int (__fastcall *)(void*))f120)(iface);
                            logf("[host]   class methods: iface=%p f130=%s f120=%s r130=%d r120=%d",
                                 iface, fnLoc(f130), fnLoc(f120), r130, r120);
                        }
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER)
                    { logf("[host]   class methods: fault"); }
                }
                unsigned long long vtRva = 0;
                if (rdi != NULL)
                    vtRva = (unsigned long long)((BYTE*)(*(void**)rdi) - (BYTE*)g_eng);
                printf("[host]   gate: modelType=0x%X ebp=%d z=%p rdi=%p flag484=0x%02X rdiVt=eng+0x%llX\n",
                       type, ebp, z, rdi, flag484, vtRva);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            { printf("[host]   gate: fault\n"); }
        }
        if (param != NULL)
        {
            BYTE* p = (BYTE*)param;
            for (int i = 0; i < 10; i++)
                printf("   param[%02X]=%016llX\n", i * 8,
                       (unsigned long long)*(unsigned long long*)(p + i * 8));
        }
        fflush(stdout);
    }
    ((ActorLoadedCbFn)g_alcTramp)(engine, actor, loaded, a4, model, path, mtx, param,
                                  a9, a10);
}

static HWND createHostWindow(void)
{
    WNDCLASSEXA wc;
    memset(&wc, 0, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.style = CS_OWNDC;
    wc.lpfnWndProc = HostWndProc;
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

    g_mainThreadHandle = OpenThread(
        THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_QUERY_INFORMATION,
        FALSE, GetCurrentThreadId());
    HMODULE x3d = LoadLibraryExW(dll, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (x3d == NULL) { logf("[host] X3DEngine load failed err=%lu", GetLastError()); return 2; }
    g_x3dModule = x3d;
    typedef int (__cdecl *fn_void)(void);
    int x3dPre = ((fn_void)GetProcAddress(x3d, "?PreInitX3DEngine@NSX3DEngine@@YAHXZ"))();
    int x3dLoad = ((fn_void)GetProcAddress(x3d, "?LoadX3DEngine@NSX3DEngine@@YAHXZ"))();
    logf("[host] X3D PreInit=%d Load=%d", x3dPre, x3dLoad);

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
    if (engIface != NULL)
    {
        __try
        {
            logf("[host] engIface+0x260 -> %p (facade+0x260 later)", 
                 *(void**)((BYTE*)engIface + 0x260));
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        { logf("[host] engIface+0x260 read fault"); }
    }
    if (eng == NULL) return 3;

    int hookOk = installInlineHook(eng, 0x8AEF30, (void*)hookCreateTargetWindow,
                                   g_ctwSaved, &g_ctwTramp, 15);
    int lfOk = installInlineHook(eng, 0xB0F870, (void*)hookLoadFileSrc,
                                 g_lfSaved, &g_lfTramp, 20);
    g_eng = eng;
    int arpOk = installInlineHook(eng, 0x9BB730, (void*)hookAcquireProxy,
                                  g_arpSaved, &g_arpTramp, 22);
    int socOk = 1; // ctor hook not installable (RIP-relative in prologue)
    int soiOk = installInlineHook(eng, 0x9B9030, (void*)hookSceneObjectInit,
                                  g_soiSaved, &g_soiTramp, 16);
    int sfpOk = installInlineHook(eng, 0xE6690, (void*)hookSetSfxOrPss,
                                  g_sfpSaved, &g_sfpTramp, 16);
    int alcOk = installInlineHook(eng, 0x8CA470, (void*)hookActorLoaded,
                                  g_alcSaved, &g_alcTramp, 30);
    int facOk = installInlineHook(eng, 0xC4D7F0, (void*)hookMeshFactory,
                                  g_facSaved, &g_facTramp, 15);
    int chkOk = installInlineHook(eng, 0xC4D430, (void*)hookKeepCheck,
                                  g_chkSaved, &g_chkTramp, 18);
    int bldOk = installInlineHook(eng, 0xC4DE80, (void*)hookBuildData,
                                  g_bldSaved, &g_bldTramp, 15);
    int ispOk = installInlineHook(eng, 0x852E00, (void*)hookIsPlayer,
                                  g_ispSaved, &g_ispTramp, 15);
    logf("[host] keepCheck hook=%d buildData hook=%d isPlayer hook=%d", chkOk, bldOk, ispOk);
    logf("[host] hooks: window=%d loadfile=%d acquireProxy=%d soCtor=%d soInit=%d setSfx=%d actorLoaded=%d meshFactory=%d",
         hookOk, lfOk, arpOk, socOk, soiOk, sfpOk, alcOk, facOk);
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
        g_luaModule = lua;
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
            // the game initializes the engine's size-class allocator early; the
            // client logic module's SO3World (5.6 MB) allocates through it.
            {
                typedef int (__cdecl *KMemInitFn)(const char*);
                KMemInitFn kmem = (KMemInitFn)GetProcAddress(lua,
                    "?Initialize@KMemory@@YAHQEBD@Z");
                int kr = (kmem != NULL) ? kmem("reborn_client_host.memory") : -1;
                logf("[host] KMemory::Initialize -> %d", kr);
            }
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
    __try
    {
        ((void (__fastcall *)(void*, void*, void*, void*))
         ((BYTE*)eng + 0x8CA400))(engine, (void*)onModelHandle,
                                  (void*)onModelHandle, (void*)onModelHandle);
        logf("[host] SetModelHandleCallBack registered (loaded/moved/unloaded)");
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    { logf("[host] SetModelHandleCallBack fault"); }

    // represent module (the client's character/effect layer - A1 showed the engine
    // scene alone does not render character actors)
    {
        wchar_t rp[MAX_PATH];
        swprintf_s(rp, MAX_PATH, L"%s\\JX3RepresentX64.dll", bin64);
        HMODULE rep = LoadLibraryExW(rp, NULL,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        logf("[host] JX3RepresentX64.dll -> %p (err=%u)", rep, rep ? 0 : GetLastError());
        if (rep != NULL)
        {
            typedef void* (__cdecl *CreateRepFn)(void);
            CreateRepFn cr = (CreateRepFn)GetProcAddress(rep, "CreateSO3Represent");
            void* r = (cr != NULL) ? cr() : NULL;
            logf("[host] CreateSO3Represent -> %p", r);
            typedef void* (__cdecl *GetEcsFn)(void);
            GetEcsFn ge = (GetEcsFn)GetProcAddress(rep, "GetRepresentECSRootEntity");
            void* ecs = (ge != NULL) ? ge() : NULL;
            logf("[host] GetRepresentECSRootEntity -> %p", ecs);
            g_repSingleton = r;
            g_repModule = rep;
            installInlineHook(rep, 0xAEDFD0, (void*)hookEntityFactory,
                              g_efSaved, &g_efTramp, 16);
            {
                char rpFlag[8];
                if (GetEnvironmentVariableA("RC_HOST_REPINIT", rpFlag, sizeof(rpFlag)) != 0)
                    g_repProbePending = 1; // run in the frame loop (engine pump active)
            }
            // CreateRLLoader only validates a 0xC0 tag on its env argument (client
            // InitLogic builds a stack struct with just that tag). Test it directly:
            // this is the game's own character loader without the module environment.
            {
                char rlFlag[8];
                if (GetEnvironmentVariableA("RC_HOST_RLLOADER", rlFlag, sizeof(rlFlag)) != 0)
                {
                    g_rlProbePending = 1; // deferred: vt[1] + scene + character at frame 5
                    typedef void* (__cdecl *CreateRLLoaderFn)(void*);
                    CreateRLLoaderFn crl = (CreateRLLoaderFn)GetProcAddress(rep, "CreateRLLoader");
                    unsigned char env[0x80];
                    memset(env, 0, sizeof(env));
                    *(unsigned*)env = 0xC0;
                    void* loader = NULL;
                    __try { loader = (crl != NULL) ? crl(env) : NULL; }
                    __except (EXCEPTION_EXECUTE_HANDLER) { loader = NULL; }
                    logf("[host] CreateRLLoader(tag=0xC0) -> %p (fn=%p)", loader, crl);
                    if (loader != NULL)
                    {
                        void** lvt = *(void***)loader;
                        g_rlLoader = loader;
                        logf("[host] RLLoader vtable=%p", (void*)lvt);
                        // represent logging -> host log (shows the loader's own errors;
                        // the DLL logs via Engine_Lua5X64!KGLogPrintf)
                        HMODULE lua = GetModuleHandleA("Engine_Lua5X64.dll");
                        if (lua != NULL)
                            patchIat(rep, GetProcAddress(lua,
                                "?KGLogPrintf@@YAHW4KGLOG_PRIORITY@@QEBDZZ"),
                                (void*)hookPrintfLog);
                        // the client's InitLogic loads the resource converter right
                        // after CreateRLLoader: JX3ResourceConvertX64.dll ->
                        // KG_GetConvertResource(void** out) (returns the singleton)
                        __try
                        {
                            wchar_t rcp[MAX_PATH];
                            swprintf_s(rcp, MAX_PATH, L"%s\\JX3ResourceConvertX64.dll",
                                       bin64);
                            HMODULE rcm = LoadLibraryExW(rcp, NULL,
                                LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR |
                                LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                            logf("[host] JX3ResourceConvertX64.dll -> %p", rcm);
                            if (rcm != NULL)
                            {
                                typedef long (__cdecl *GetConvFn)(void**);
                                GetConvFn gc = (GetConvFn)GetProcAddress(rcm,
                                    "KG_GetConvertResource");
                                void* conv = NULL;
                                long rc = (gc != NULL) ? gc(&conv) : -1;
                                logf("[host] KG_GetConvertResource -> 0x%08X conv=%p",
                                     (unsigned)rc, conv);
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] resource converter fault"); }
                        // SO3Represent::Init(Param) (singleton vt[0]) needs 8 engine
                        // interfaces; the game gets them from the X3DEngine facade
                        // (already loaded/inited by this host). Probe the getters so we
                        // know which Param fields are reachable in-host.
                        __try
                        {
                            HMODULE x3d = GetModuleHandleA("X3DEngine.dll");
                            logf("[host] X3DEngine module -> %p", x3d);
                            if (x3d != NULL)
                            {
                                typedef void* (__cdecl *GetterFn)(void);
                                GetterFn gm = (GetterFn)GetProcAddress(x3d,
                                    "?GetK3EngineMgr@NSX3DEngine@@YAPEAVIX3DEngineManager@@XZ");
                                GetterFn gl = (GetterFn)GetProcAddress(x3d,
                                    "?GetK3EngineXRepresentLogic@NSX3DEngine@@YAPEAUIKG3DEngineXRepresentLogic@@XZ");
                                GetterFn gv = (GetterFn)GetProcAddress(x3d,
                                    "?GetViewMgr@NSX3DEngine@@YAPEAVIView@1@XZ");
                                GetterFn gs = (GetterFn)GetProcAddress(x3d,
                                    "?GetScreenMgr@NSX3DEngine@@YAPEAVIScreen@@XZ");
                                logf("[host] X3D getters: K3EngineMgr=%p K3EngineXRepLogic=%p ViewMgr=%p ScreenMgr=%p",
                                     gm ? gm() : (void*)-1, gl ? gl() : (void*)-1,
                                     gv ? gv() : (void*)-1, gs ? gs() : (void*)-1);
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] X3D getter probe fault"); }
                        // Logic module via its own entry: CreateJX3LogicOperation(
                        // basePath, factory, name) runs KGJX3LogicOperation::Init ->
                        // InitLogic (0x113150) which creates g_pSO3World, the RLLoader,
                        // the resource converter and the UI - the objects the represent
                        // Param requires. Hand-building the world faults on module
                        // globals this init sets. NOTE: this call currently WEDGES
                        // (job/thread wait) - separate flag so it does not hang the
                        // other probes.
                        char logicFlag[8];
                        if (GetEnvironmentVariableA("RC_HOST_LOGIC", logicFlag,
                                                    sizeof(logicFlag)) != 0)
                        __try
                        {
                            wchar_t lp2[MAX_PATH];
                            swprintf_s(lp2, MAX_PATH,
                                       L"%s\\JX3LogicEditOperationX64.dll", bin64);
                            HMODULE logic = LoadLibraryExW(lp2, NULL,
                                LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR |
                                LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                            logf("[host] JX3LogicEditOperationX64.dll -> %p", logic);
                            g_logicModule = logic;
                            if (logic != NULL)
                            {
                                // Skip KGJX3LogicOperation::Init's game-context step
                                // (0x8B7DB) - it runs before the represent Init and
                                // faults (heap corruption). Jump to the success
                                // epilogue (0x8B8EF). In-memory host adaptation.
                                {
                                    BYTE* p = (BYTE*)logic + 0x8B7DB;
                                    DWORD oldp = 0;
                                    if (VirtualProtect(p, 5, PAGE_EXECUTE_READWRITE, &oldp))
                                    {
                                        p[0] = 0xE9;
                                        *(int*)(p + 1) =
                                            (int)(((BYTE*)logic + 0x8B8EF) - (p + 5));
                                        VirtualProtect(p, 5, oldp, &oldp);
                                        logf("[host] patched logic+0x8B7DB -> +0x8B8EF (skip game-context)");
                                    }
                                }
                                HMODULE u32 = GetModuleHandleA("user32.dll");
                                if (u32 != NULL)
                                {
                                    int m1 = patchIat(logic,
                                        GetProcAddress(u32, "MessageBoxA"),
                                        (void*)hookMessageBoxA);
                                    int m2 = patchIat(logic,
                                        GetProcAddress(u32, "MessageBoxW"),
                                        (void*)hookMessageBoxW);
                                    logf("[host] logic MessageBox IAT patched A=%d W=%d",
                                         m1, m2);
                                }
                                typedef void* (__fastcall *CreateLogicFn)(
                                    const char*, void*, const char*);
                                CreateLogicFn cl = (CreateLogicFn)GetProcAddress(logic,
                                    "CreateJX3LogicOperation");
                                if (cl != NULL && g_logicStarted == 0)
                                {
                                    g_logicStarted = 1;
                                    g_logicDone = 0;
                                    // worker thread: the main thread returns to the
                                    // engine frame loop (the game calls the logic init
                                    // with the engine running; its internal thread wait
                                    // needs the engine pumping).
                                    g_logicThread = CreateThread(NULL, 0,
                                        logicInitThread, (void*)cl, 0, NULL);
                                }
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] logic module init fault"); }
                        // map the game exe and call its event-module creators
                        {
                            char exeFlag[8];
                            if (GetEnvironmentVariableA("RC_HOST_EXE", exeFlag,
                                                        sizeof(exeFlag)) != 0)
                            {
                                wchar_t ep[MAX_PATH];
                                swprintf_s(ep, MAX_PATH, L"%s\\JX3ClientX64.exe", bin64);
                                if (mapGameExe(ep))
                                {
                                    __try
                                    {
                                        void* g = ((void* (__fastcall *)(void))
                                                   ((BYTE*)g_exeModule + 0xAFEC0))();
                                        logf("[host] exe trivial getter -> %p", g);
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] exe trivial getter fault"); }
                                    // lazy globals the exe module Create reads directly:
                                    // 0xA8C1F0 (a list head) is built by the lazy getter
                                    // at 0x9DB60 - call it first.
                                    __try
                                    {
                                        void* pm = ((void* (__fastcall *)(size_t))
                                                    ((BYTE*)g_exeModule + 0x79B800))(0x20);
                                        logf("[host] exe malloc(0x20) -> %p (IAT malloc=%p)",
                                             pm, *(void**)((BYTE*)g_exeModule + 0x7B96E8));
                                        ((void (__fastcall *)(void*))
                                         ((BYTE*)g_exeModule + 0x9DB60))(NULL);
                                        logf("[host] exe lazy 0x9DB60 -> global 0xA8C1F0=%p",
                                             *(void**)((BYTE*)g_exeModule + 0xA8C1F0));
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] exe lazy 0x9DB60 fault"); }
                                    __try
                                    {
                                        void* m = ((void* (__fastcall *)(void))
                                                   ((BYTE*)g_exeModule + 0xAFD60))();
                                        logf("[host] KJX3LogicEventModule::Create -> %p", m);
                                        if (m != NULL)
                                        {
                                            unsigned char* mb = (unsigned char*)m;
                                            logf("[host]   module bytes: %02X %02X %02X %02X | %02X %02X %02X %02X | %02X %02X %02X %02X | %02X %02X %02X %02X",
                                                 mb[0],mb[1],mb[2],mb[3],mb[4],mb[5],mb[6],mb[7],
                                                 mb[8],mb[9],mb[10],mb[11],mb[12],mb[13],mb[14],mb[15]);
                                            g_exeLogicEvent = m;
                                        }
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] KJX3LogicEventModule::Create fault"); }
                                    __try
                                    {
                                        void* m = ((void* (__fastcall *)(void))
                                                   ((BYTE*)g_exeModule + 0xBA7D0))();
                                        logf("[host] KJX3RepresentEventModule::Create -> %p", m);
                                        if (m != NULL)
                                        {
                                            unsigned char* mb = (unsigned char*)m;
                                            logf("[host]   module bytes: %02X %02X %02X %02X | %02X %02X %02X %02X | %02X %02X %02X %02X | %02X %02X %02X %02X",
                                                 mb[0],mb[1],mb[2],mb[3],mb[4],mb[5],mb[6],mb[7],
                                                 mb[8],mb[9],mb[10],mb[11],mb[12],mb[13],mb[14],mb[15]);
                                            g_exeRepEvent = m;
                                        }
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] KJX3RepresentEventModule::Create fault"); }
                                    // OnInitialize(module, 1) initializes the static
                                    // manager object and sets [module+0x18] = manager
                                    // stub the CRT helpers used by the magic-static
                                    // guards so OnInitialize's init block runs
                                    patchAbsJmp((BYTE*)g_exeModule + 0x79B6E0,
                                                (void*)exeGuardHeader);
                                    patchAbsJmp((BYTE*)g_exeModule + 0x79B680,
                                                (void*)exeGuardFooter);
                                    patchAbsJmp((BYTE*)g_exeModule + 0x79B3F0,
                                                (void*)exeAtexit);
                                    // force the guard "not initialized" branch
                                    *(int*)((BYTE*)g_exeModule + 0xA8F411) = 1;
                                    *(int*)((BYTE*)g_exeModule + 0xA8FFC1) = 1;
                                    *(int*)((BYTE*)g_exeModule + 0xA8D670) = 1;
                                    // KJX3CommonEventModule: Create 0xA4700 +
                                    // OnInitialize 0xA42F0 (manager exe+0xA8D680)
                                    {
                                        void* cm = NULL;
                                        __try
                                        {
                                            cm = ((void* (__fastcall *)(void))
                                                  ((BYTE*)g_exeModule + 0xA4700))();
                                            logf("[host] KJX3CommonEventModule::Create -> %p", cm);
                                        }
                                        __except (EXCEPTION_EXECUTE_HANDLER)
                                        { logf("[host] common Create fault"); }
                                        if (cm != NULL)
                                        {
                                            // OnInitialize sets [module+0x18] = manager
                                            // before its later holder call faults
                                            __try
                                            {
                                                ((long (__fastcall *)(void*, int))
                                                 ((BYTE*)g_exeModule + 0xA42F0))(cm, 1);
                                            }
                                            __except (EXCEPTION_EXECUTE_HANDLER)
                                            { logf("[host] common OnInitialize fault"); }
                                            g_exeCommonMgr = *(void**)((BYTE*)cm + 0x18);
                                            logf("[host] common event mgr -> %p",
                                                 g_exeCommonMgr);
                                        }
                                    }
                                    // KJX3RenderModule: Create 0xB8E40 + Initialize 0xB7D20
                                    // (edx=1 -> LoadX3DEngine path; sets the facade and
                                    // the holder fields, incl. facade+0x260).
                                    __try
                                    {
                                        void* rm = ((void* (__fastcall *)(void))
                                                    ((BYTE*)g_exeModule + 0xB8E40))();
                                        logf("[host] KJX3RenderModule::Create -> %p", rm);
                                        if (rm != NULL)
                                        {
                                            long ir = ((long (__fastcall *)(void*, int))
                                                       ((BYTE*)g_exeModule + 0xB7D20))(rm, 1);
                                            logf("[host] KJX3RenderModule::Initialize(1) -> 0x%08X",
                                                 (unsigned)ir);
                                            long ir2 = ((long (__fastcall *)(void*, int))
                                                        ((BYTE*)g_exeModule + 0xB7D20))(rm, 2);
                                            logf("[host] KJX3RenderModule::Initialize(2) -> 0x%08X",
                                                 (unsigned)ir2);
                                            long ir4 = ((long (__fastcall *)(void*, int))
                                                        ((BYTE*)g_exeModule + 0xB7D20))(rm, 4);
                                            logf("[host] KJX3RenderModule::Initialize(4) -> 0x%08X",
                                                 (unsigned)ir4);
                                            void* facade = *(void**)((BYTE*)rm + 0x18);
                                            logf("[host] render module: facade=%p xlogic=%p",
                                                 facade, *(void**)((BYTE*)rm + 0x20));
                                            if (facade != NULL)
                                                logf("[host] facade+0x260 -> %p",
                                                     *(void**)((BYTE*)facade + 0x260));
                                        }
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] render module fault"); }
                                    // KJX3ConvertResourceModule: Create 0xA65C0 +
                                    // OnInitialize 0xA6470 (may create the engine's
                                    // resource manager, facade+0x260).
                                    __try
                                    {
                                        void* crm = ((void* (__fastcall *)(void))
                                                     ((BYTE*)g_exeModule + 0xA65C0))();
                                        logf("[host] KJX3ConvertResourceModule::Create -> %p", crm);
                                        if (crm != NULL)
                                        {
                                            long cir = ((long (__fastcall *)(void*, int))
                                                        ((BYTE*)g_exeModule + 0xA6470))(crm, 1);
                                            logf("[host] KJX3ConvertResourceModule::OnInitialize(1) -> 0x%08X",
                                                 (unsigned)cir);
                                        }
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] convert resource module fault"); }
                                    // script dispatcher module: Create 0xB2FA0 stores the
                                    // module at exe+0xA8C220; the dispatcher = +0x18.
                                    __try
                                    {
                                        void* dm = ((void* (__fastcall *)(void))
                                                    ((BYTE*)g_exeModule + 0xB2FA0))();
                                        void* holder = *(void**)((BYTE*)g_exeModule + 0xA8C220);
                                        if (holder != NULL)
                                            g_exeDispatcher = (BYTE*)holder + 0x18;
                                        logf("[host] dispatcher module=%p holder=%p disp=%p",
                                             dm, holder, g_exeDispatcher);
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] dispatcher module fault"); }
                                    if (g_exeLogicEvent != NULL)
                                    {
                                        __try
                                        {
                                            ((long (__fastcall *)(void*, int))
                                             ((BYTE*)g_exeModule + 0xAF990))(
                                                g_exeLogicEvent, 1);
                                            g_exeLogicMgr =
                                                *(void**)((BYTE*)g_exeLogicEvent + 0x18);
                                            logf("[host] logic event mgr -> %p", g_exeLogicMgr);
                                        }
                                        __except (EXCEPTION_EXECUTE_HANDLER)
                                        { logf("[host] logic OnInitialize fault"); }
                                    }
                                    if (g_exeRepEvent != NULL)
                                    {
                                        __try
                                        {
                                            ((long (__fastcall *)(void*, int))
                                             ((BYTE*)g_exeModule + 0xBA430))(
                                                g_exeRepEvent, 1);
                                            g_exeRepMgr =
                                                *(void**)((BYTE*)g_exeRepEvent + 0x18);
                                            logf("[host] represent event mgr -> %p", g_exeRepMgr);
                                        }
                                        __except (EXCEPTION_EXECUTE_HANDLER)
                                        { logf("[host] represent OnInitialize fault"); }
                                    }
                                    // the exe's Param fill reads these holder slots:
                                    // [0xA8BF20] (+0x18) dispatcher, and
                                    // [0xA8BFC0]/[0xA8C010]/[0xA8C060] (+0x18) the three
                                    // event managers.
                                    __try
                                    {
                                        void* hDisp = *(void**)((BYTE*)g_exeModule + 0xA8BF20);
                                        void* hCommon = *(void**)((BYTE*)g_exeModule + 0xA8BFC0);
                                        void* hLogic = *(void**)((BYTE*)g_exeModule + 0xA8C010);
                                        void* hRep = *(void**)((BYTE*)g_exeModule + 0xA8C060);
                                        logf("[host] exe holders: disp=%p common=%p logic=%p rep=%p",
                                             hDisp, hCommon, hLogic, hRep);
                                        if (hDisp) logf("[host]   disp+0x18=%p", *(void**)((BYTE*)hDisp + 0x18));
                                        if (hCommon) logf("[host]   common+0x18=%p", *(void**)((BYTE*)hCommon + 0x18));
                                        if (hLogic) logf("[host]   logic+0x18=%p", *(void**)((BYTE*)hLogic + 0x18));
                                        if (hRep) logf("[host]   rep+0x18=%p", *(void**)((BYTE*)hRep + 0x18));
                                    }
                                    __except (EXCEPTION_EXECUTE_HANDLER)
                                    { logf("[host] exe holder read fault"); }
                                }
                            }
                        }
                        // SO3Represent::Init(Param) probe - fill the game's Param (0xD0)
                        // exactly like the exe's KJX3RepresentModule::Initialize fill
                        // (0xBC263): mgr = GetK3EngineMgr; modelMgr = mgr->vt[9]();
                        // xlogic = GetK3EngineXRepresentLogic; sceneResp = mgr->vt[0x50]();
                        // conv = KG_GetConvertResource; movie = KG_GetMovieEngine;
                        // ui = mgr->vt[0xF](); pStepCtrl = local object (placeholder here).
                        __try
                        {
                            HMODULE x3d = GetModuleHandleA("X3DEngine.dll");
                            if (x3d != NULL && g_repSingleton != NULL)
                            {
                                typedef void* (__cdecl *GetterFn)(void);
                                GetterFn gm = (GetterFn)GetProcAddress(x3d,
                                    "?GetK3EngineMgr@NSX3DEngine@@YAPEAVIX3DEngineManager@@XZ");
                                GetterFn gl = (GetterFn)GetProcAddress(x3d,
                                    "?GetK3EngineXRepresentLogic@NSX3DEngine@@YAPEAUIKG3DEngineXRepresentLogic@@XZ");
                                void* mgr = (gm != NULL) ? gm() : NULL;
                                void* xlogic = (gl != NULL) ? gl() : NULL;
                                void* modelMgr = NULL;
                                void* sceneResp = NULL;
                                void* ui = NULL;
                                if (mgr != NULL)
                                {
                                    void** mvt = *(void***)mgr;
                                    modelMgr = ((void* (__fastcall *)(void*))mvt[9])(mgr);
                                    sceneResp = ((void* (__fastcall *)(void*))mvt[0x50])(mgr);
                                    ui = ((void* (__fastcall *)(void*))mvt[0xF])(mgr);
                                }
                                void* conv = NULL;
                                {
                                    HMODULE rcm = GetModuleHandleA("JX3ResourceConvertX64.dll");
                                    if (rcm != NULL)
                                    {
                                        typedef long (__cdecl *GetConvFn)(void**);
                                        GetConvFn gc = (GetConvFn)GetProcAddress(rcm,
                                            "KG_GetConvertResource");
                                        if (gc != NULL) gc(&conv);
                                    }
                                }
                                void* movie = NULL;
                                {
                                    wchar_t mp[MAX_PATH];
                                    swprintf_s(mp, MAX_PATH,
                                               L"%s\\KG_MovieEngineX64.dll", bin64);
                                    HMODULE mm = LoadLibraryExW(mp, NULL,
                                        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR |
                                        LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                                    if (mm != NULL)
                                    {
                                        typedef void* (__cdecl *GetMovieFn)(void);
                                        GetMovieFn gmo = (GetMovieFn)GetProcAddress(mm,
                                            "KG_GetMovieEngine");
                                        if (gmo != NULL) movie = gmo();
                                    }
                                }
                                logf("[host] Init probe: mgr=%p modelMgr=%p xlogic=%p sceneResp=%p conv=%p movie=%p ui=%p",
                                     mgr, modelMgr, xlogic, sceneResp, conv, movie, ui);
                                g_ifMgr = mgr;
                                g_ifModelMgr = modelMgr;
                                g_ifXLogic = xlogic;
                                g_ifSceneResp = sceneResp;
                                g_ifConv = conv;
                                g_ifMovie = movie;
                                g_ifUI = ui;
                                if (mgr != NULL)
                                {
                                    logf("[host] Init probe: [mgr+0x260]=%p [mgr+0x10]=%p",
                                         *(void**)((BYTE*)mgr + 0x260),
                                         *(void**)((BYTE*)mgr + 0x10));
                                    // The represent's map-file lookup (0x16A09 ->
                                    // 0x80D710) resolves names through [mgr+0x260]
                                    // (a file/resource bundle). The facade exposes
                                    // GetNativeFileBundle - fill it if empty.
                                    if (*(void**)((BYTE*)mgr + 0x260) == NULL)
                                    {
                                        typedef void* (__cdecl *GetterFn)(void);
                                        GetterFn gb = (GetterFn)GetProcAddress(x3d,
                                            "?GetNativeFileBundle@NSX3DEngine@@YAPEAVINativeFileBundle@@XZ");
                                        void* bundle = (gb != NULL) ? gb() : NULL;
                                        logf("[host] X3D GetNativeFileBundle -> %p", bundle);
                                        if (bundle != NULL)
                                        {
                                            *(void**)((BYTE*)mgr + 0x260) = bundle;
                                            logf("[host] [mgr+0x260] set to bundle");
                                        }
                                    }
                                }
                                unsigned char param[0xD0];
                                static unsigned char stepCtrl[0x100];
                                memset(param, 0, sizeof(param));
                                memset(stepCtrl, 0, sizeof(stepCtrl));
                                *(unsigned*)param = 0xD0;
                                *(void**)(param + 0x08) = mgr;
                                *(void**)(param + 0x10) = modelMgr;
                                *(void**)(param + 0x18) = xlogic;
                                *(void**)(param + 0x20) = sceneResp;
                                *(void**)(param + 0x28) = conv;
                                *(void**)(param + 0x30) = movie;
                                *(void**)(param + 0x38) = ui;
                                *(void**)(param + 0xC8) = stepCtrl;
                                // SO3Represent::Init also requires pSO3World(+0x70) /
                                // pSO3WorldClient(+0x78) - the logic module's worlds -
                                // and hangs in its failure path here. The RL scene path
                                // only needs the engine manager in singleton+0xB0 (the
                                // field Init would set), so write it directly (host
                                // wiring; the manager object is the game's own
                                // K3EngineMgr). The full Init remains the re-open path.
                                if (mgr != NULL &&
                                    *(void**)((BYTE*)g_repSingleton + 0xB0) == NULL)
                                {
                                    *(void**)((BYTE*)g_repSingleton + 0xB0) = mgr;
                                }
                                logf("[host] singleton+0xB0 (mgr) -> %p",
                                     *(void**)((BYTE*)g_repSingleton + 0xB0));
                                // CreateRLScene requires singleton+0x100 (m_pSO3World,
                                // the field the full Init sets). We created the world
                                // via the client's own logic module (Init_ForEditor).
                                if (g_so3World != NULL &&
                                    *(void**)((BYTE*)g_repSingleton + 0x100) == NULL)
                                {
                                    *(void**)((BYTE*)g_repSingleton + 0x100) = g_so3World;
                                }
                                logf("[host] singleton+0x100 (SO3World) -> %p",
                                     *(void**)((BYTE*)g_repSingleton + 0x100));
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] SO3Represent::Init probe fault"); }
                        __try
                        {
                            long pm = ((long (__fastcall *)(void*))lvt[6])(loader);
                            logf("[host] RLLoader::LoadPlayerAllModel -> 0x%08X", (unsigned)pm);
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] LoadPlayerAllModel fault"); }
                        // RL unit APIs: GetRepresentIDFromPath (vt[15],
                        // 0x3F4C60) is (this, szPath, char* outIdStr, count) - it
                        // writes the id as text and returns a bool. GetUnit (vt[1]/
                        // vt[2]) returns the assembled RL unit (the renderable
                        // object) for an id.
                        __try
                        {
                            typedef long (__fastcall *PathFn)(void*, const char*,
                                                              char*, unsigned);
                            typedef void* (__fastcall *UnitFn)(void*, const char*);
                            char idStr[0x108];
                            memset(idStr, 0, sizeof(idStr));
                            const char* f1mdl =
                                "Data\\source\\player\\F1\\\xB2\xBF\xBC\xFE\\Mdl\\F1.mdl";
                            long gr = ((PathFn)lvt[15])(loader, f1mdl, idStr,
                                                        sizeof(idStr));
                            logf("[host] GetRepresentIDFromPath(F1.mdl) -> %ld id='%s'",
                                 gr, idStr);
                            if (idStr[0] != 0)
                            {
                                // vt[1]: GetUnit(this, out{dword id; void* unit}, id)
                                unsigned char out[0x20];
                                memset(out, 0, sizeof(out));
                                long ok = ((long (__fastcall *)(void*, void*,
                                                                 const char*))
                                           lvt[1])(loader, out, idStr);
                                logf("[host] GetUnit('%s') -> %ld id=%u unit=%p",
                                     idStr, ok, *(unsigned*)out,
                                     *(void**)(out + 8));
                                // vt[2]: direct fetcher
                                void* u2 = ((UnitFn)lvt[2])(loader, idStr);
                                logf("[host] GetUnit('%s') vt2 -> %p", idStr, u2);
                                if (u2 != NULL)
                                {
                                    unsigned char* ub = (unsigned char*)u2;
                                    logf("[host] unit bytes: %02X %02X %02X %02X %02X %02X %02X %02X | %02X %02X %02X %02X %02X %02X %02X %02X",
                                         ub[0], ub[1], ub[2], ub[3], ub[4], ub[5], ub[6], ub[7],
                                         ub[8], ub[9], ub[10], ub[11], ub[12], ub[13], ub[14], ub[15]);
                                    DWORD64 first = *(DWORD64*)ub;
                                    // KGRL unit files/structs start with the magic "RL00"
                                    // (bytes 52 4C 30 30) + version - they are data, not
                                    // C++ objects with a vtable.
                                    if ((*(DWORD*)ub) == 0x30304C52)
                                        logf("[host] unit magic='RL00' ver=%u (KGRL data struct)",
                                             *(unsigned*)(ub + 4));
                                    else
                                        logf("[host] unit first qword=0x%llX", first);
                                }
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] RL unit probe fault"); }
                        // the RL Lua state lives embedded in the SO3Represent singleton
                        // at +0x25BC0; the singleton's activate (vt[1]) inits it via
                        // rep+0x16130 (the rest of vt[1] faults in this host).
                        __try
                        {
                            void* singleton = g_repSingleton;
                            if (singleton != NULL)
                            {
                                // rep+0x5BC9F0(slot) = CreateLuaInterface(NULL,NULL) +
                                // init; stores the interface at the embedded slot
                                ((long (__fastcall *)(void*))
                                 ((BYTE*)rep + 0x5BC9F0))((BYTE*)singleton + 0x25BC0);
                                void* lua = *(void**)((BYTE*)singleton + 0x25BC0);
                                logf("[host] RL lua state (singleton+0x25BC0) -> %p", lua);
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] RL lua state init fault"); }
                        // local player assembly (the game's Lua LoadPlayerParts path):
                        // ctx = 0x42DAD0(); core = 0x42D860(ctx, roleType);
                        // 0x422F50(core, partsA[13], partsB[13], count)
                        __try
                        {
                            typedef void* (__fastcall *GetCtxFn)(void*);
                            void* ctx = ((GetCtxFn)((BYTE*)rep + 0x42DAD0))(NULL);
                            logf("[host] RL ctx -> %p", ctx);
                            g_rlCtx = ctx;
                            void* core = NULL;
                            if (ctx != NULL)
                            {
                                core = ((void* (__fastcall *)(void*, int))
                                        ((BYTE*)rep + 0x42D860))(ctx, 6);
                                logf("[host] RL player core(roleType=6) -> %p", core);
                            }
                            if (core != NULL)
                            {
                                int partsA[13];
                                int partsB[13];
                                memset(partsA, 0, sizeof(partsA));
                                memset(partsB, 0, sizeof(partsB));
                                long lp = ((long (__fastcall *)(void*, void*, void*, int))
                                           ((BYTE*)rep + 0x422F50))(core, partsA, partsB, 13);
                                logf("[host] RL LoadPlayerParts(core, zeros, zeros, 13) -> 0x%08X",
                                     (unsigned)lp);
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] local player assembly fault"); }
                        // LuaCreateHangPet's internal path: scene -> local character ->
                        // CreateHangPet(world, sceneField, representID, character, cfg, type)
                        __try
                        {
                            void* singleton = g_repSingleton;
                            void* scene = (singleton != NULL)
                                ? ((void* (__fastcall *)(void*))
                                   ((BYTE*)rep + 0x3E5E80))(singleton) : NULL;
                            logf("[host] RL scene -> %p", scene);
                            void* character = (scene != NULL)
                                ? ((void* (__fastcall *)(void*))
                                   ((BYTE*)rep + 0x58CE20))(scene) : NULL;
                            logf("[host] RL local character -> %p", character);
                            // local-player lookup chain (0x58CE20): sceneId ->
                            // 0x924B(id) world -> [world+0xF29E8] -> 0x1B9D7(world)
                            // -> [x+0x20]+0x70. Log each step to find the missing one.
                            if (scene != NULL && character == NULL)
                            {
                                __try
                                {
                                    unsigned sceneId = *(unsigned*)
                                        ((BYTE*)scene + 0xF1970);
                                    void* world = ((void* (__fastcall *)(unsigned))
                                                   ((BYTE*)rep + 0x924B))(sceneId);
                                    logf("[host] char chain: sceneId=%u world=%p",
                                         sceneId, world);
                                    if (world != NULL)
                                    {
                                        void* f = *(void**)((BYTE*)world + 0xF29E8);
                                        logf("[host] char chain: [world+0xF29E8]=%p", f);
                                        if (f != NULL)
                                        {
                                            void* x = ((void* (__fastcall *)(void*))
                                                       ((BYTE*)rep + 0x1B9D7))(world);
                                            logf("[host] char chain: 0x1B9D7(world)=%p", x);
                                            if (x != NULL)
                                                logf("[host] char chain: [x+0x20]=%p",
                                                     *(void**)((BYTE*)x + 0x20));
                                        }
                                    }
                                }
                                __except (EXCEPTION_EXECUTE_HANDLER)
                                { logf("[host] char chain probe fault"); }
                            }
                            if (scene != NULL && character != NULL)
                            {
                                void* world = *(void**)((BYTE*)scene + 0xF2988);
                                int sceneField = *(int*)((BYTE*)scene + 0xF1970);
                                unsigned char cfg[0x60];
                                memset(cfg, 0, sizeof(cfg));
                                typedef void* (__fastcall *CreateHangPetFn)(
                                    void*, int, int, void*, void*, int);
                                void* pet = ((CreateHangPetFn)
                                             ((BYTE*)rep + 0x42D1F0))(
                                    world, sceneField, 6, character, cfg, 1);
                                logf("[host] RL CreateHangPet(world=%p, field=%d, id=6, char=%p, cfg, type=1) -> %p",
                                     world, sceneField, character, pet);
                            }
                        }
                        __except (EXCEPTION_EXECUTE_HANDLER)
                        { logf("[host] CreateHangPet fault"); }
                    }
                }
            }
            // NOTE: singleton lifecycle init (vt[1], the game's KJX3RepresentModule
            // activate path) spins on a represent lock here - defer it until the
            // frame loop is pumping (see docs/EXPERIENCES.md).
        }
    }

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
    gbk(L"data\\source\\npc_source\\f1\\部件\\f1_3094_body_hd.mesh", mpath, sizeof(mpath));
    float actorMtx[16] = {
        1,0,0,0, 0,1,0,0, 0,0,1,0,
        23334.0f, 761.0f, 24224.0f, 1.0f
    };
    void* actor = NULL;
    long mrc = ((CreateActorFn)((BYTE*)eng + 0x8B2DA0))(engine, mpath, scene, &actor, 0,
                                                        actorMtx);
    logf("[host] CreateActorFromFile -> 0x%08X actor=%p", (unsigned)mrc, actor);
    if (actor != NULL)
    {
        __try
        {
            void* model = *(void**)((BYTE*)actor + 0x358);
            unsigned char param[0x200];
            memset(param, 0, sizeof(param));
            typedef void (__fastcall *ActorLoadedFn)(void*, void*, unsigned char, int,
                                                     void*, const char*, void*, void*,
                                                     int, unsigned long long);
            ((ActorLoadedFn)((BYTE*)eng + 0x8CA470))(engine, actor, 1, 0, model, mpath,
                                                     actorMtx, param, 0, 0);
            logf("[host] OnSceneActorLoadedCallBack sent (model=%p)", model);
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { logf("[host] actor-loaded callback fault"); }
    }

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
    createRealSfx(eng, 23334.0f, 762.0f, 24224.0f);

    // ability list for casting (keys 1..9); auto-cast #0 once after boot
    {
        char abilPath[MAX_PATH];
        if (GetEnvironmentVariableA("RC_HOST_ABILITIES", abilPath, MAX_PATH) == 0)
            sprintf_s(abilPath, MAX_PATH, "%s\\abilities.txt", rootA);
        loadAbilities(abilPath);
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
            MSG msg;
            while (PeekMessageA(&msg, NULL, 0, 0, PM_REMOVE))
            {
                TranslateMessage(&msg);
                DispatchMessageA(&msg);
            }
            if (g_castRequest >= 0)
            {
                castAbility(g_castRequest, actor, ctrl, eng);
                g_castRequest = -1;
            }
            if (f == 30 && !g_autoCastDone && g_abilCount > 0)
            {
                g_autoCastDone = 1;
                castAbility(0, actor, ctrl, eng);
            }
            if (f == 5 && g_rlProbePending)
            {
                g_rlProbePending = 0;
                logf("[host] deferred RL probe at frame %d", f);
                __try
                {
                    if (g_repSingleton != NULL && g_repModule != NULL)
                    {
                        // NOTE: singleton vt[1] (activate) blocks in this host (job
                        // processor spinlock) - do not call it. The game's full scene
                        // creation is CreateRLScene (0xB0B5C0 = KGameWorldHandler::
                        // NewScene): registers the scene in the singleton map, uses the
                        // engine manager and loads the map file. Called the way
                        // KRLUGC::CreateScene does:
                        // CreateRLScene(id, 0x10, 0, 0, 0, mapFile, 0, sceneName, 0).
                        void* mgr = *(void**)((BYTE*)g_repSingleton + 0xB0);
                        logf("[host] RL engine manager (singleton+0xB0) -> %p", mgr);
                        void* scene = NULL;
                        if (mgr != NULL)
                        {
                            // CreateRLScene (0xB0B5C0) is the game's full scene
                            // creation but it requires the full represent Init
                            // (singleton+0x100 m_pSO3World etc.) - it faults here.
                            // Own SEH so the fallback + chain probe still run.
                            __try
                            {
                                typedef long (__fastcall *CreateRLSceneFn)(
                                    unsigned id, unsigned type, unsigned a3, unsigned a4,
                                    unsigned long long a5, const char* mapFile,
                                    unsigned long long a7, const char* sceneName,
                                    unsigned long long a9);
                                long cs = ((CreateRLSceneFn)
                                           ((BYTE*)g_repModule + 0xB0B5C0))(
                                    2, 0x10, 0, 0, 0,
                                    "data\\source\\maps\\\xE9\xBE\x99\xE9\x97\xA8\xE5\xAF\xBB\xE5\xAE\x9D_s\\\xE9\xBE\x99\xE9\x97\xA8\xE5\xAF\xBB\xE5\xAE\x9D_s.jsonmap",
                                    0, "\xE9\xBE\x99\xE9\x97\xA8\xE5\xAF\xBB\xE5\xAE\x9D_s", 0);
                                logf("[host] RL CreateRLScene(id=2) -> 0x%08X", (unsigned)cs);
                            }
                            __except (EXCEPTION_EXECUTE_HANDLER)
                            { logf("[host] RL CreateRLScene fault (needs full Init: m_pSO3World)"); }
                            scene = ((void* (__fastcall *)(unsigned))
                                     ((BYTE*)g_repModule + 0x924B))(2);
                            logf("[host] RL scene by id 2 -> %p", scene);
                            if (scene != NULL)
                            {
                                logf("[host] RL scene: id=%u 3DScene=%p",
                                     *(unsigned*)((BYTE*)scene + 0xF1970),
                                     *(void**)((BYTE*)scene + 0xF1978));
                            }
                        }
                        if (scene == NULL && mgr != NULL)
                        {
                            long ns = ((long (__fastcall *)(void*, int, void**))
                                       ((BYTE*)g_repModule + 0x16DB5))(mgr, 1, &scene);
                            logf("[host] RL NewScene(mgr, 1) -> 0x%08X scene=%p",
                                 (unsigned)ns, scene);
                        }
                        if (scene == NULL)
                            scene = ((void* (__fastcall *)(void*))
                                     ((BYTE*)g_repModule + 0x3E5E80))(g_repSingleton);
                        logf("[host] RL scene -> %p", scene);
                        // local-player lookup chain (0x58CE20): sceneId ->
                        // 0x924B(id) world -> [world+0xF29E8] -> 0x1B9D7(world)
                        // -> [x+0x20]+0x70. Log each step to find the missing one.
                        if (scene != NULL)
                        {
                            __try
                            {
                                unsigned sceneId = *(unsigned*)
                                    ((BYTE*)scene + 0xF1970);
                                void* world = ((void* (__fastcall *)(unsigned))
                                               ((BYTE*)g_repModule + 0x924B))(sceneId);
                                logf("[host] char chain: sceneId=%u world=%p",
                                     sceneId, world);
                                if (world != NULL)
                                {
                                    void* f = *(void**)((BYTE*)world + 0xF29E8);
                                    logf("[host] char chain: [world+0xF29E8]=%p", f);
                                    if (f != NULL)
                                    {
                                        void* x = ((void* (__fastcall *)(void*))
                                                   ((BYTE*)g_repModule + 0x1B9D7))(world);
                                        logf("[host] char chain: 0x1B9D7(world)=%p", x);
                                        if (x != NULL)
                                            logf("[host] char chain: [x+0x20]=%p",
                                                 *(void**)((BYTE*)x + 0x20));
                                    }
                                }
                            }
                            __except (EXCEPTION_EXECUTE_HANDLER)
                            { logf("[host] char chain probe fault"); }
                        }
                        void* character = (scene != NULL)
                            ? ((void* (__fastcall *)(void*))
                               ((BYTE*)g_repModule + 0x58CE20))(scene) : NULL;
                        logf("[host] RL local character -> %p", character);
                        // Standalone player-model path (MovieEditor's route): the HangPet
                        // core does not need a character - it is only a map key in
                        // CreateHangPet (0x42D1F0 -> 0x42E2B0); NULL just never matches an
                        // existing core. The Lua binding's call:
                        // CreateHangPet(world, sceneId, representID, 0, character, nType,
                        // &cfg) then LoadPlayerParts(core, partsA[13], partsB[13], count).
                        if (g_rlCtx != NULL)
                        {
                            unsigned sceneId = (scene != NULL)
                                ? *(unsigned*)((BYTE*)scene + 0xF1970) : 0;
                            // HangPetCore::Init (0x421F20) requires
                            // [master+0x39F0] = m_pFrameData non-null; the master is the
                            // 5th arg (character). Probe: a fake master positioned at the
                            // camera with a zeroed frame-data struct (documented deviation;
                            // re-open with a real local character once the logic world
                            // exists). cfg layout (Lua binding): +0x00 szRoot, +0x08 szMdl,
                            // +0x10 szBone, +0x18 fScale.
                            static unsigned char fakeMaster[0x8000];
                            static unsigned char fakeFrame[0x400];
                            memset(fakeMaster, 0, sizeof(fakeMaster));
                            memset(fakeFrame, 0, sizeof(fakeFrame));
                            *(void**)(fakeMaster + 0x39F0) = fakeFrame;
                            // cfg layout (Lua binding): +0x00 szRoot, +0x08 szMdl,
                            // +0x10 szBone, +0x18 fScale. The RL system resolves RL
                            // resource names (GetUnit('F1') works), not source file
                            // paths - try RL-style names (each attempt SEH-guarded).
                            static const char* combos[][2] = {
                                { "F1", "F1" },
                                { "F1", "F1.mdl" },
                                { "Represent/player/F1", "Represent/player/F1/F1.mdl" },
                            };
                            typedef void* (__fastcall *CreateHangPetFn)(
                                void*, unsigned, unsigned, int, void*, int, void*);
                            for (int ci = 0; ci < 3; ci++)
                            {
                                unsigned char cfg[0x60];
                                memset(cfg, 0, sizeof(cfg));
                                *(const char**)(cfg + 0x00) = combos[ci][0];
                                *(const char**)(cfg + 0x08) = combos[ci][1];
                                *(const char**)(cfg + 0x10) = "";
                                *(float*)(cfg + 0x18) = 1.0f;
                                void* pet = NULL;
                                __try
                                {
                                    pet = ((CreateHangPetFn)
                                           ((BYTE*)g_repModule + 0x42D1F0))(
                                        g_rlCtx, sceneId, 6, 0, fakeMaster, 1, cfg);
                                }
                                __except (EXCEPTION_EXECUTE_HANDLER)
                                { pet = (void*)-1; }
                                logf("[host] RL CreateHangPet cfg[%d] szRoot='%s' szMdl='%s' -> %p",
                                     ci, combos[ci][0], combos[ci][1], pet);
                                if (pet != NULL && pet != (void*)-1)
                                {
                                    g_lastPet = pet;
                                    break;
                                }
                            }
                            void* pet = g_lastPet;
                            if (pet != NULL)
                            {
                                int partsA[13];
                                int partsB[13];
                                memset(partsA, 0, sizeof(partsA));
                                memset(partsB, 0, sizeof(partsB));
                                long lp = ((long (__fastcall *)(void*, void*, void*, int))
                                           ((BYTE*)g_repModule + 0x422F50))(
                                    pet, partsA, partsB, 13);
                                logf("[host] RL LoadPlayerParts(core=%p, zeros, zeros, 13) -> 0x%08X",
                                     pet, (unsigned)lp);
                            }
                        }
                    }
                }
                __except (EXCEPTION_EXECUTE_HANDLER)
                { logf("[host] deferred RL probe fault"); }
            }
            if (f == 40 && !g_logicDone && g_logicThread != NULL)
            {
                logf("[host] frame40: logic init thread still running - dumping its stack");
                SuspendThread(g_logicThread);
                CONTEXT lctx;
                memset(&lctx, 0, sizeof(lctx));
                lctx.ContextFlags = CONTEXT_CONTROL | CONTEXT_INTEGER;
                if (GetThreadContext(g_logicThread, &lctx))
                {
                    char mdesc[64];
                    describeAddr((DWORD64)lctx.Rip, mdesc, sizeof(mdesc));
                    logf("[host] logic thread rip=%p (%s) rsp=0x%llX rbp=0x%llX",
                         (void*)lctx.Rip, mdesc, (unsigned long long)lctx.Rsp,
                         (unsigned long long)lctx.Rbp);
                    // proper unwind walk (dbghelp)
                    STACKFRAME64 sf;
                    memset(&sf, 0, sizeof(sf));
                    sf.AddrPC.Offset = lctx.Rip;
                    sf.AddrPC.Mode = AddrModeFlat;
                    sf.AddrFrame.Offset = lctx.Rbp;
                    sf.AddrFrame.Mode = AddrModeFlat;
                    sf.AddrStack.Offset = lctx.Rsp;
                    sf.AddrStack.Mode = AddrModeFlat;
                    char desc[64];
                    for (int i = 0; i < 40; i++)
                    {
                        if (!StackWalk64(IMAGE_FILE_MACHINE_AMD64, GetCurrentProcess(),
                                         g_logicThread, &sf, &lctx, NULL, NULL, NULL,
                                         NULL))
                            break;
                        if (sf.AddrPC.Offset == 0)
                            break;
                        describeAddr(sf.AddrPC.Offset, desc, sizeof(desc));
                        logf("[host]   walk[%d] %s", i, desc);
                    }
                    // raw stack scan for executable-section addresses (return addrs)
                    DWORD64* sp = (DWORD64*)lctx.Rsp;
                    for (int i = 0; i < 512; i++)
                    {
                        DWORD64 v = 0;
                        __try { v = sp[i]; }
                        __except (EXCEPTION_EXECUTE_HANDLER) { break; }
                        if (isCodeAddr(v, desc, sizeof(desc)))
                            logf("[host]   code[%d] %s", i, desc);
                    }
                }
                ResumeThread(g_logicThread);
            }
            if (f == 60 && g_logicDone && g_so3World != NULL && g_repSingleton != NULL)
            {
                __try
                {
                    if (*(void**)((BYTE*)g_repSingleton + 0x100) == NULL)
                        *(void**)((BYTE*)g_repSingleton + 0x100) = g_so3World;
                    logf("[host] frame60: singleton+0x100 (SO3World) -> %p",
                         *(void**)((BYTE*)g_repSingleton + 0x100));
                    // Phase B: full SO3Represent::Init(Param) now that the logic world
                    // exists. MessageBoxes are suppressed, so a failed Init returns
                    // (its KGLOG names the next missing object) instead of hanging.
                    // Init requires m_p3DEngineManager/m_pSO3World to be NULL (it sets
                    // them from the Param) - clear the earlier bypass writes.
                    __try
                    {
                        *(void**)((BYTE*)g_repSingleton + 0xB0) = NULL;
                        *(void**)((BYTE*)g_repSingleton + 0x100) = NULL;
                        unsigned char param[0xD0];
                        static unsigned char stepCtrl[0x100];
                        static unsigned char dummyDispatcher[0x100];
                        static unsigned char dummyEvtCommon[0x100];
                        static unsigned char dummyEvtLogic[0x100];
                        static unsigned char dummyEvtRep[0x100];
                        memset(param, 0, sizeof(param));
                        memset(stepCtrl, 0, sizeof(stepCtrl));
                        memset(dummyDispatcher, 0, sizeof(dummyDispatcher));
                        memset(dummyEvtCommon, 0, sizeof(dummyEvtCommon));
                        memset(dummyEvtLogic, 0, sizeof(dummyEvtLogic));
                        memset(dummyEvtRep, 0, sizeof(dummyEvtRep));
                        *(unsigned*)param = 0xD0;
                        *(void**)(param + 0x08) = g_ifMgr;
                        *(void**)(param + 0x10) = g_ifModelMgr;
                        *(void**)(param + 0x18) = g_ifXLogic;
                        *(void**)(param + 0x20) = g_ifSceneResp;
                        *(void**)(param + 0x28) = g_ifConv;
                        *(void**)(param + 0x30) = g_ifMovie;
                        *(void**)(param + 0x38) = g_ifUI;
                        *(void**)(param + 0x70) = g_so3World;
                        // KGSO3WorldClientInterface is a static object in the logic
                        // module (getter 0x440B60 = lea rax,[rip+0x56C091] -> +0x9ACBF8).
                        if (g_logicModule != NULL)
                        {
                            *(void**)(param + 0x78) = (BYTE*)g_logicModule + 0x9ACBF8;
                            // pSO3UI (+0x60) = the logic module's g_pUI
                            // (InitLogic stores it at logic+0xA00D80).
                            void* lui = *(void**)((BYTE*)g_logicModule + 0xA00D80);
                            *(void**)(param + 0x60) = lui;
                            // pRLUIHandler (+0x68) = g_pGameWorldUIHandler
                            // (logic+0x9C1328, adjacent to g_pSO3World).
                            void* uiH = *(void**)((BYTE*)g_logicModule + 0x9C1328);
                            *(void**)(param + 0x68) = uiH;
                            logf("[host] frame60: logic g_pUI -> %p g_pGameWorldUIHandler -> %p",
                                 lui, uiH);
                        }
                        // remaining required fields: dispatcher (+0x40) and the three
                        // event managers (+0x90/+0x98/+0xA0) - no standalone globals
                        // found yet; stub objects with a valid no-op vtable (a zeroed
                        // buffer faults on call [0]).
                        *(void**)(param + 0x40) = (g_exeDispatcher != NULL)
                            ? g_exeDispatcher : makeStubObject(0x400);
                        *(void**)(param + 0x90) = (g_exeCommonMgr != NULL)
                            ? g_exeCommonMgr : makeStubObject(0x400);
                        // real event managers created by the game exe's own modules
                        *(void**)(param + 0x98) = (g_exeLogicMgr != NULL)
                            ? g_exeLogicMgr : makeStubObject(0x400);
                        *(void**)(param + 0xA0) = (g_exeRepMgr != NULL)
                            ? g_exeRepMgr : makeStubObject(0x400);
                        // zero the represent's fallback allocations (the Init's
                        // copy-ctor expects fresh objects' list fields to be 0)
                        if (g_repModule != NULL)
                            patchAbsJmp((BYTE*)g_repModule + 0xB74A10,
                                        (void*)repCallocNew);
                        // stepCtrl: the represent reads [Param+0xC8] -> stepCtrl object
                        // -> [stepCtrl+0x10] = a pool allocator ([0]=block size,
                        // [8]=free list). Block size 0 makes the allocator fall back to
                        // operator new (rep+0x3E5387 path) - no pool needed.
                        {
                            // pStepCtrl = the control buffer itself (list owner at
                            // +0x70/+0x78/+0x80); [0] -> object whose +0x10 is the
                            // pool allocator (block size 0 -> operator-new fallback).
                            static unsigned char stepBuf[0x200];
                            static unsigned char stepA[0x200];
                            static unsigned char stepAlloc[0x40];
                            memset(stepBuf, 0, sizeof(stepBuf));
                            memset(stepA, 0, sizeof(stepA));
                            memset(stepAlloc, 0, sizeof(stepAlloc));
                            *(void**)(stepA + 0x10) = stepAlloc;
                            *(void**)stepBuf = stepA;
                            *(void**)(param + 0xC8) = stepBuf;
                        }
                        logf("[host] frame60: param logicMgr=%p repMgr=%p",
                             *(void**)(param + 0x98), *(void**)(param + 0xA0));
                        void** svt60 = *(void***)g_repSingleton;
                        long ir60 = ((long (__fastcall *)(void*, void*))
                                     svt60[0])(g_repSingleton, param);
                        logf("[host] frame60: SO3Represent::Init(Param) -> 0x%08X",
                             (unsigned)ir60);
                        logf("[host] frame60: after Init singleton+0xB0=%p +0x100=%p",
                             *(void**)((BYTE*)g_repSingleton + 0xB0),
                             *(void**)((BYTE*)g_repSingleton + 0x100));
                    }
                    __except (EXCEPTION_EXECUTE_HANDLER)
                    { logf("[host] frame60 Init fault"); }
                    void* mgr60 = *(void**)((BYTE*)g_repSingleton + 0xB0);
                    if (mgr60 != NULL)
                    {
                        typedef long (__fastcall *CreateRLSceneFn)(
                            unsigned id, unsigned type, unsigned a3, unsigned a4,
                            unsigned long long a5, const char* mapFile,
                            unsigned long long a7, const char* sceneName,
                            unsigned long long a9);
                        long cs = ((CreateRLSceneFn)
                                   ((BYTE*)g_repModule + 0xB0B5C0))(
                            2, 0x10, 0, 0, 0,
                            "data\\source\\maps\\\xE9\xBE\x99\xE9\x97\xA8\xE5\xAF\xBB\xE5\xAE\x9D_s\\\xE9\xBE\x99\xE9\x97\xA8\xE5\xAF\xBB\xE5\xAE\x9D_s.jsonmap",
                            0, "\xE9\xBE\x99\xE9\x97\xA8\xE5\xAF\xBB\xE5\xAE\x9D_s", 0);
                        logf("[host] frame60: CreateRLScene(id=2) -> 0x%08X",
                             (unsigned)cs);
                        void* scene60 = ((void* (__fastcall *)(unsigned))
                                         ((BYTE*)g_repModule + 0x924B))(2);
                        logf("[host] frame60: scene by id 2 -> %p", scene60);
                        if (scene60 != NULL)
                            logf("[host] frame60: scene id=%u 3DScene=%p",
                                 *(unsigned*)((BYTE*)scene60 + 0xF1970),
                                 *(void**)((BYTE*)scene60 + 0xF1978));
                    }
                }
                __except (EXCEPTION_EXECUTE_HANDLER)
                { logf("[host] frame60 fault"); }
            }
            if (f == 5 && g_repProbePending)
            {
                g_repProbePending = 0;
                logf("[host] deferred represent probe at frame %d", f);
                char rpMode[8] = { 0 };
                GetEnvironmentVariableA("RC_HOST_REPINIT", rpMode, sizeof(rpMode));
                if (rpMode[0] == '2')
                {
                    logf("[host] represent probe mode 2: skip lifecycle init");
                    probeRepresentHierarchy();
                }
                else
                {
                    probeRepresentInit();
                }
            }
            if (f == 10 && g_entitySO != NULL)
            {
                // With the f1 mesh force-kept (render data built), try the engine's
                // own visibility/force-render switches + model re-fetch on the
                // entity scene object.
                __try
                {
                    long fm = ((long (__fastcall *)(void*))((BYTE*)eng + 0x9B9D50))(g_entitySO);
                    // rendering props carry flags 0x00008180; the JSON-created entity
                    // carries 0x00008980 (extra bit 0x800). Force the prop flag set.
                    unsigned oldFlags = *(unsigned*)((BYTE*)g_entitySO + 0x10);
                    *(unsigned*)((BYTE*)g_entitySO + 0x10) = 0x00008180;
                    logf("[host] entity SO flags 0x%08X -> 0x00008180", oldFlags);
                    ((void (__fastcall *)(void*, int))((BYTE*)eng + 0xE6530))(g_entitySO, 1);
                    ((void (__fastcall *)(void*, int))((BYTE*)eng + 0xE6510))(g_entitySO, 1);
                    int rv = ((int (__fastcall *)(void*))((BYTE*)eng + 0xE6290))(g_entitySO);
                    int fr = ((int (__fastcall *)(void*))((BYTE*)eng + 0xE62A0))(g_entitySO);
                    void* a2 = ((void* (__fastcall *)(void*))((BYTE*)eng + 0x9BB960))(g_entitySO);
                    void* proxy = NULL;
                    long ap = ((long (__fastcall *)(void*, void*, int, void**))
                               ((BYTE*)eng + 0x9BB730))(g_entitySO, scene, 0, &proxy);
                    logf("[host] entity SO force: fetch=0x%08X renderVisible=%d forceRender=%d actor2=%p proxyRet=0x%08X proxy=%p",
                         (unsigned)fm, rv, fr, a2, (unsigned)ap, proxy);
                    if (a2 != NULL)
                    {
                        logf("[host] entity actor vtable=%p", *(void**)a2);
                        // skinned mesh: attach the f1 animation + controller and
                        // frame-move it (without bone updates the mesh is degenerate).
                        char dirAni[64], taniPath[512];
                        gbk(L"动作", dirAni, sizeof(dirAni));
                        sprintf_s(taniPath, sizeof(taniPath),
                                  "data\\source\\player\\f1\\%s\\F1HA393_start01.tani", dirAni);
                        long iat = ((long (__fastcall *)(void*, const char*, unsigned))
                                    ((BYTE*)eng + 0x83A6F0))(a2, taniPath, 0);
                        void* anim = *(void**)((BYTE*)a2 + 0x368);
                        typedef void* (__fastcall *AllocFn)(void*, size_t, size_t);
                        void* ec = ((AllocFn)((BYTE*)eng + 0xB0F300))(NULL, 0x3A8, 8);
                        ((void (__fastcall *)(void*))((BYTE*)eng + 0xBC1100))(ec);
                        long sar = ((long (__fastcall *)(void*, void*))
                                    ((BYTE*)eng + 0xBC2120))(ec, a2);
                        long src = 0;
                        if (anim != NULL)
                            src = ((long (__fastcall *)(void*, void*, int, float,
                                    unsigned, unsigned, void*, void*, void*))
                                   ((BYTE*)eng + 0xBC1C70))(ec, anim, 0, 1.0f,
                                    0, 0, NULL, NULL, NULL);
                        g_entityCtrl = ec;
                        logf("[host] entity anim: attach=0x%08X anim=%p ctrl=%p setActor=0x%08X start=0x%08X",
                             (unsigned)iat, anim, ec, (unsigned)sar, (unsigned)src);
                    }
                }
                __except (EXCEPTION_EXECUTE_HANDLER)
                { logf("[host] entity SO force fault"); }
            }
            if (f == 30 || f == 200)
            {
                dumpRegistryCount(f == 30 ? "@30" : "@200");
                dumpSceneObject(f == 30 ? "entity@30" : "entity@200", g_entitySO);
                dumpSceneObject(f == 30 ? "prop@30" : "prop@200", g_propSO);
                dumpMeshState(f == 30 ? "f1@30" : "f1@200", g_f1Model);
                dumpMeshState(f == 30 ? "prop@30" : "prop@200", g_propModel);
                dumpMeshState(f == 30 ? "npc@30" : "npc@200", g_npcModel);
                if (actor != NULL)
                    logf("[host] manual actor=%p model=%p ready484=0x%02X (frame %d)", actor,
                         *(void**)((BYTE*)actor + 0x358),
                         modelReadyFlag(*(void**)((BYTE*)actor + 0x358)), f);
            }
            if (f == 200)
                dumpLoadedObjects();



            if (ctrl != NULL) ctrlFm(ctrl);
            if (g_entityCtrl != NULL) ctrlFm(g_entityCtrl);
            // while the logic init thread is running, pump the RL loader (its job
            // lock is a suspect for the init's wait).
            if (!g_logicDone && g_rlLoader != NULL && g_logicStarted)
            {
                __try
                {
                    void** lvt = *(void***)g_rlLoader;
                    ((long (__fastcall *)(void*))lvt[0])(g_rlLoader);
                }
                __except (EXCEPTION_EXECUTE_HANDLER) { }
            }
            engFm(engine);
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





