// sound_probe.dll - read-only diagnostic hooks for the host Wwise path (1.6).
//
// Installs inline hooks on KG3D_WwiseX64.dll's exported
//   AK::SoundEngine::PostEvent (id / char* / wchar_t* overloads)
//   AK::SoundEngine::LoadBank  (wchar_t* overload)
// and appends every call (arguments) to a log file. The hooks tail-call the
// original code through per-hook trampolines, so engine behaviour is
// unchanged; the module is loaded only by clients that set RC_SOUND_HOOK=1.
//
// Version guard: each hook verifies the first 15 prologue bytes it steals and
// refuses (returns negative) on mismatch - same pattern as camera_shim's D6
// patch. Call with RC_SoundProbe_Status() for the per-hook result string.
#include <windows.h>
#include <stdio.h>
#include <stdarg.h>
#include <stdint.h>
#include <string.h>

static FILE* g_log = NULL;
static char g_status[512];

static void Log(const char* fmt, ...)
{
    if (g_log == NULL) return;
    va_list ap;
    va_start(ap, fmt);
    vfprintf(g_log, fmt, ap);
    va_end(ap);
    fputc('\n', g_log);
    fflush(g_log);
}

typedef unsigned int (__cdecl *PostEventIdFn)(unsigned int, unsigned long long,
    unsigned int, void*, void*, unsigned int, void*, unsigned int);
typedef unsigned int (__cdecl *PostEventStrFn)(const char*, unsigned long long,
    unsigned int, void*, void*, unsigned int, void*, unsigned int);
typedef unsigned int (__cdecl *PostEventWStrFn)(const wchar_t*, unsigned long long,
    unsigned int, void*, void*, unsigned int, void*, unsigned int);
typedef int (__cdecl *LoadBankWStrFn)(const wchar_t*, unsigned int*);

static PostEventIdFn g_tPostEventId = NULL;
static PostEventStrFn g_tPostEventStr = NULL;
static PostEventWStrFn g_tPostEventWStr = NULL;
static LoadBankWStrFn g_tLoadBankWStr = NULL;

static unsigned int __cdecl HookPostEventId(unsigned int id, unsigned long long go,
    unsigned int flags, void* cb, void* cookie, unsigned int extFlags, void* extSrc,
    unsigned int playingId)
{
    Log("PostEvent id=%u (0x%08X) go=0x%llX flags=0x%X", id, id, go, flags);
    return g_tPostEventId(id, go, flags, cb, cookie, extFlags, extSrc, playingId);
}

static unsigned int __cdecl HookPostEventStr(const char* name, unsigned long long go,
    unsigned int flags, void* cb, void* cookie, unsigned int extFlags, void* extSrc,
    unsigned int playingId)
{
    Log("PostEvent name='%s' go=0x%llX flags=0x%X", name ? name : "(null)", go, flags);
    return g_tPostEventStr(name, go, flags, cb, cookie, extFlags, extSrc, playingId);
}

static unsigned int __cdecl HookPostEventWStr(const wchar_t* name, unsigned long long go,
    unsigned int flags, void* cb, void* cookie, unsigned int extFlags, void* extSrc,
    unsigned int playingId)
{
    Log("PostEvent wname='%ls' go=0x%llX flags=0x%X", name ? name : L"(null)", go, flags);
    return g_tPostEventWStr(name, go, flags, cb, cookie, extFlags, extSrc, playingId);
}

static int __cdecl HookLoadBankWStr(const wchar_t* name, unsigned int* outId)
{
    int rc = g_tLoadBankWStr(name, outId);
    Log("LoadBank wname='%ls' -> rc=%d bankId=%u", name ? name : L"(null)", rc,
        outId ? *outId : 0u);
    return rc;
}

// Inline-hook: patch `steal` bytes at target with "mov rax, detour; jmp rax"
// (12 bytes, NOP-padded), after copying the stolen prologue into a trampoline
// that continues with an absolute jump to target+steal.
static int InstallHook(BYTE* target, const BYTE* expect, int steal,
                       void* detour, void** trampOut)
{
    if (target == NULL || steal < 12 || steal > 24) return -1;
    if (memcmp(target, expect, steal) != 0) return -2;
    BYTE* tr = (BYTE*)VirtualAlloc(NULL, 64, MEM_COMMIT | MEM_RESERVE,
                                   PAGE_EXECUTE_READWRITE);
    if (tr == NULL) return -3;
    memcpy(tr, target, steal);
    BYTE* p = tr + steal;
    p[0] = 0xFF; p[1] = 0x25;
    *(DWORD*)(p + 2) = 0;
    *(void**)(p + 6) = target + steal;
    DWORD old;
    if (!VirtualProtect(target, steal, PAGE_EXECUTE_READWRITE, &old)) return -4;
    BYTE patch[16];
    patch[0] = 0x48; patch[1] = 0xB8;
    *(void**)(patch + 2) = detour;
    patch[10] = 0xFF; patch[11] = 0xE0;
    for (int i = 12; i < steal; i++) patch[i] = 0x90;
    memcpy(target, patch, steal);
    VirtualProtect(target, steal, old, &old);
    FlushInstructionCache(GetCurrentProcess(), target, steal);
    *trampOut = tr;
    return 0;
}

// ---- native playback (1.6): drive the engine's own Wwise with game data ----
typedef int (__cdecl *RegisterGameObjFn)(unsigned long long, const char*);
typedef int (__cdecl *AddDefaultListenerFn)(unsigned long long);
typedef int (__cdecl *LoadBankMemViewFn)(const void*, unsigned int, unsigned int*);
typedef bool (__cdecl *IsInitializedFn)();
typedef int (__cdecl *GetAudioSettingsFn)(void*);
typedef int (__cdecl *GetSourcePlayPosFn)(unsigned int, int*, bool);
typedef int (__cdecl *SetOutputVolumeFn)(unsigned long long, float);

static RegisterGameObjFn g_registerGameObj = NULL;
static AddDefaultListenerFn g_addDefaultListener = NULL;
static LoadBankMemViewFn g_loadBankMemoryView = NULL;
static PostEventIdFn g_postEventId = NULL;
static IsInitializedFn g_isInitialized = NULL;
static GetAudioSettingsFn g_getAudioSettings = NULL;
static GetSourcePlayPosFn g_getSourcePlayPos = NULL;
static SetOutputVolumeFn g_setOutputVolume = NULL;
typedef int (__cdecl *RenderAudioFn)(bool);
static RenderAudioFn g_renderAudio = NULL;

static int ResolveAudio()
{
    HMODULE m = GetModuleHandleA("KG3D_WwiseX64.dll");
    if (m == NULL) m = LoadLibraryA("KG3D_WwiseX64.dll");
    if (m == NULL) return -1;
    g_registerGameObj = (RegisterGameObjFn)GetProcAddress(m,
        "?RegisterGameObj@SoundEngine@AK@@YA?AW4AKRESULT@@_KPEBD@Z");
    g_addDefaultListener = (AddDefaultListenerFn)GetProcAddress(m,
        "?AddDefaultListener@SoundEngine@AK@@YA?AW4AKRESULT@@_K@Z");
    g_loadBankMemoryView = (LoadBankMemViewFn)GetProcAddress(m,
        "?LoadBankMemoryView@SoundEngine@AK@@YA?AW4AKRESULT@@PEBXIAEAI@Z");
    g_postEventId = (PostEventIdFn)GetProcAddress(m,
        "?PostEvent@SoundEngine@AK@@YAII_KIP6AXW4AkCallbackType@@PEAUAkCallbackInfo@@@ZPEAXIPEAUAkExternalSourceInfo@@I@Z");
    g_isInitialized = (IsInitializedFn)GetProcAddress(m,
        "?IsInitialized@SoundEngine@AK@@YA_NXZ");
    g_getAudioSettings = (GetAudioSettingsFn)GetProcAddress(m,
        "?GetAudioSettings@SoundEngine@AK@@YA?AW4AKRESULT@@AEAUAkAudioSettings@@@Z");
    g_getSourcePlayPos = (GetSourcePlayPosFn)GetProcAddress(m,
        "?GetSourcePlayPosition@SoundEngine@AK@@YA?AW4AKRESULT@@IPEAH_N@Z");
    g_setOutputVolume = (SetOutputVolumeFn)GetProcAddress(m,
        "?SetOutputVolume@SoundEngine@AK@@YA?AW4AKRESULT@@_KM@Z");
    g_renderAudio = (RenderAudioFn)GetProcAddress(m,
        "?RenderAudio@SoundEngine@AK@@YA?AW4AKRESULT@@_N@Z");
    Log("sound_probe: audio register=%p listener=%p loadBankMem=%p postEvent=%p init=%p setVol=%p",
        g_registerGameObj, g_addDefaultListener, g_loadBankMemoryView, g_postEventId,
        g_isInitialized, g_setOutputVolume);
    return (g_registerGameObj && g_addDefaultListener && g_loadBankMemoryView && g_postEventId) ? 0 : -2;
}

// Wwise state diagnostics: initialized? audio settings? is the posted source
// actually advancing? (the shell render thread calls RenderAudio via FrameMove)
extern "C" __declspec(dllexport) int __cdecl RC_SoundProbe_Diag(unsigned int playingId)
{
    if (ResolveAudio() != 0) return -1;
    Log("sound_probe: IsInitialized=%d", g_isInitialized ? (g_isInitialized() ? 1 : 0) : -1);
    if (g_getAudioSettings)
    {
        unsigned char st[64];
        memset(st, 0, sizeof(st));
        int rc = g_getAudioSettings(st);
        unsigned int a = *(unsigned int*)(st + 0);
        unsigned int b = *(unsigned int*)(st + 4);
        unsigned int c = *(unsigned int*)(st + 8);
        Log("sound_probe: GetAudioSettings rc=%d samplesPerFrame=%u samplesPerSec=%u outType/extra=%u",
            rc, a, b, c);
    }
    if (playingId != 0 && g_getSourcePlayPos)
    {
        int pos1 = -1, pos2 = -1;
        int r1 = g_getSourcePlayPos(playingId, &pos1, false);
        Sleep(300);
        int r2 = g_getSourcePlayPos(playingId, &pos2, false);
        Log("sound_probe: playPos rc=%d/%d pos=%d/%d", r1, r2, pos1, pos2);
        if (r2 == 1 && pos2 > pos1) return 1;   // AK_Success and progressing = rendering
        return 0;
    }
    return 0;
}

// Audio render tick: the shell does not run Wwise's render loop in this host,
// so the client calls this each frame (1024 samples @48k = 21.3 ms per tick).
extern "C" __declspec(dllexport) int __cdecl RC_SoundProbe_Render()
{
    if (g_renderAudio == NULL) return -1;
    return g_renderAudio(false);
}

// Set the main output volume (diagnostic; ids 0 and -1 are tried).
extern "C" __declspec(dllexport) int __cdecl RC_SoundProbe_SetVolume(float v)
{
    if (ResolveAudio() != 0) return -1;
    int r0 = g_setOutputVolume ? g_setOutputVolume(0, v) : -1;
    int r1 = g_setOutputVolume ? g_setOutputVolume((unsigned long long)-1, v) : -1;
    Log("sound_probe: SetOutputVolume v=%.2f id0=%d idFF=%d", v, r0, r1);
    return r0;
}

// Load a .bnk from disk into Wwise memory; the buffer is kept for the session
// (Wwise may reference it while the bank is loaded). Returns 0 on success.
extern "C" __declspec(dllexport) int __cdecl RC_SoundProbe_LoadBankW(const wchar_t* path)
{
    if (ResolveAudio() != 0) return -1;
    FILE* f = _wfopen(path, L"rb");
    if (f == NULL) { Log("sound_probe: bank open failed '%ls'", path); return -2; }
    fseek(f, 0, SEEK_END);
    long sz = ftell(f);
    fseek(f, 0, SEEK_SET);
    if (sz <= 0) { fclose(f); Log("sound_probe: bank empty"); return -3; }
    void* buf = malloc((size_t)sz);
    if (buf == NULL) { fclose(f); return -4; }
    if (fread(buf, 1, (size_t)sz, f) != (size_t)sz) { fclose(f); free(buf); return -5; }
    fclose(f);
    unsigned int bankId = 0;
    int rc = g_loadBankMemoryView(buf, (unsigned int)sz, &bankId);
    Log("sound_probe: LoadBankMemoryView '%ls' size=%ld rc=%d bankId=%u", path, sz, rc, bankId);
    return rc == 0 ? (int)bankId : rc;
}

// Streamed-media root: the editor's default Wwise IO resolves streamed .wem
// files relative to the process cwd; the install is read-only, so playback
// temporarily switches cwd to a staged tree (restored once the stream opened).
static wchar_t g_mediaDir[512] = { 0 };

extern "C" __declspec(dllexport) const wchar_t* __cdecl RC_SoundProbe_Language()
{
    HMODULE m = GetModuleHandleA("KG3D_WwiseX64.dll");
    typedef const wchar_t* (__cdecl *GetLangFn)();
    GetLangFn f = m ? (GetLangFn)GetProcAddress(m, "?GetCurrentLanguage@StreamMgr@AK@@YAPEB_WXZ") : NULL;
    return f ? f() : L"(no StreamMgr)";
}

extern "C" __declspec(dllexport) int __cdecl RC_SoundProbe_SetMediaDir(const wchar_t* dir)
{
    if (dir == NULL) { g_mediaDir[0] = 0; return 0; }
    wcsncpy_s(g_mediaDir, dir, _TRUNCATE);
    wchar_t oldCwd[1024] = { 0 };
    GetCurrentDirectoryW(1024, oldCwd);
    BOOL ok = SetCurrentDirectoryW(g_mediaDir);
    HMODULE m = GetModuleHandleA("KG3D_WwiseX64.dll");
    typedef int (__cdecl *SetLangFn)(const wchar_t*);
    SetLangFn setLang = m ? (SetLangFn)GetProcAddress(m, "?SetCurrentLanguage@StreamMgr@AK@@YA?AW4AKRESULT@@PEB_W@Z") : NULL;
    int lr = setLang ? setLang(L"Base") : -1;
    Log("sound_probe: mediaDir='%ls' cwdSwitch=%d setLang(Base)=%d (was '%ls') lang='%ls'",
        g_mediaDir, ok ? 1 : 0, lr, oldCwd, RC_SoundProbe_Language());
    return ok ? 0 : -1;
}

// Register a local Wwise game object + listener and post an event on it.
extern "C" __declspec(dllexport) unsigned int __cdecl RC_SoundProbe_PostEvent(unsigned int eventId, unsigned long long go)
{
    if (ResolveAudio() != 0) return 0;
    int rr = g_registerGameObj(go, "reborn");
    int lr = g_addDefaultListener(go);
    Log("sound_probe: RegisterGameObj(%llu)=%d AddDefaultListener=%d", go, rr, lr);
    unsigned int pid = g_postEventId(eventId, go, 0, NULL, NULL, 0, NULL, 0);
    Log("sound_probe: PostEvent id=%u go=%llu -> playingId=%u (mediaDir=%ls)", eventId, go, pid,
        g_mediaDir[0] ? g_mediaDir : L"(none)");
    return pid;
}

extern "C" __declspec(dllexport) int __cdecl RC_SoundProbe_Init(const char* logPath)
{
    if (logPath != NULL && g_log == NULL)
    {
        g_log = fopen(logPath, "a");
        if (g_log == NULL) return -100;
        Log("sound_probe: init log=%s", logPath);
    }
    HMODULE m = GetModuleHandleA("KG3D_WwiseX64.dll");
    if (m == NULL) m = LoadLibraryA("KG3D_WwiseX64.dll");
    if (m == NULL)
    {
        Log("sound_probe: KG3D_WwiseX64.dll not loaded");
        return -101;
    }
    BYTE* base = (BYTE*)m;
    void* pGetMgr = (void*)GetProcAddress(m, "GetWwiseManager");
    Log("sound_probe: module=%p GetWwiseManager=%p", base, pGetMgr);

    static const BYTE expPostId[15] = { 0x48,0x89,0x5C,0x24,0x08, 0x48,0x89,0x6C,0x24,0x10, 0x48,0x89,0x74,0x24,0x18 };
    static const BYTE expPostStr[15] = { 0x48,0x89,0x5C,0x24,0x08, 0x48,0x89,0x74,0x24,0x10, 0x48,0x89,0x7C,0x24,0x18 };
    static const BYTE expPostWStr[15] = { 0x48,0x89,0x5C,0x24,0x08, 0x48,0x89,0x6C,0x24,0x10, 0x48,0x89,0x7C,0x24,0x18 };
    static const BYTE expLoadBankW[16] = { 0x48,0x89,0x5C,0x24,0x08, 0x48,0x89,0x7C,0x24,0x10, 0x55,0x48,0x8D,0x6C,0x24,0xA9 };

    int r1 = InstallHook(base + 0x32BD0, expPostId, 15, (void*)&HookPostEventId, (void**)&g_tPostEventId);
    int r2 = InstallHook(base + 0x32E10, expPostStr, 15, (void*)&HookPostEventStr, (void**)&g_tPostEventStr);
    int r3 = InstallHook(base + 0x32F30, expPostWStr, 15, (void*)&HookPostEventWStr, (void**)&g_tPostEventWStr);
    int r4 = InstallHook(base + 0x32090, expLoadBankW, 16, (void*)&HookLoadBankWStr, (void**)&g_tLoadBankWStr);
    sprintf_s(g_status, "postId=%d postStr=%d postWStr=%d loadBankW=%d", r1, r2, r3, r4);
    Log("sound_probe: hooks %s", g_status);
    return r1 + r2 + r3 + r4;
}

extern "C" __declspec(dllexport) const char* __cdecl RC_SoundProbe_Status()
{
    return g_status;
}

BOOL WINAPI DllMain(HINSTANCE, DWORD, LPVOID)
{
    return TRUE;
}
