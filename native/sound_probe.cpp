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
