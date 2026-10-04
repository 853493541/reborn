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

static HMODULE g_eng = NULL;

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
static HMODULE g_repModule = NULL;
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

    HMODULE x3d = LoadLibraryExW(dll, NULL,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (x3d == NULL) { logf("[host] X3DEngine load failed err=%lu", GetLastError()); return 2; }
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
                                if (mgr != NULL)
                                    logf("[host] Init probe: [mgr+0x260]=%p [mgr+0x10]=%p",
                                         *(void**)((BYTE*)mgr + 0x260),
                                         *(void**)((BYTE*)mgr + 0x10));
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
    gbk(L"data\\source\\player\\f1\\部件\\f1_3094_body_hd.mesh", mpath, sizeof(mpath));
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
                        if (scene != NULL && character != NULL)
                        {
                            void* world = *(void**)((BYTE*)scene + 0xF2988);
                            int sceneField = *(int*)((BYTE*)scene + 0xF1970);
                            unsigned char cfg[0x60];
                            memset(cfg, 0, sizeof(cfg));
                            typedef void* (__fastcall *CreateHangPetFn)(
                                void*, int, int, void*, void*, int);
                            void* pet = ((CreateHangPetFn)
                                         ((BYTE*)g_repModule + 0x42D1F0))(
                                world, sceneField, 6, character, cfg, 1);
                            logf("[host] RL CreateHangPet -> %p", pet);
                        }
                    }
                }
                __except (EXCEPTION_EXECUTE_HANDLER)
                { logf("[host] deferred RL probe fault"); }
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





