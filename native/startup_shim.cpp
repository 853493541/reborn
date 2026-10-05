// startup_shim.dll - host override of an editor-only startup stall in the
// MovieEditor engine DLLs. Registered host deviation D7
// (docs/engine_host/PREDRAW_STARTUP.md).
//
// Finding (2026-10-04): KG3D_MaterialShaderManager::_initShaderUpload() in
// KG3D_MaterialSystemX64.dll opens a blocking TCP connect() to the shader
// compile database 10.11.10.102:1433 (Seasun LAN, unreachable here) during
// engine init. The engine handles "server not ready" correctly (it skips the
// shader-list upload), but only after the Windows TCP SYN timeout - measured
// as a ~22 s SynSent stall inside Init3DEngine.
//
// RC_STARTUP=nodb rewrites that IP literal to 0.0.0.0 in memory, so connect()
// fails immediately with WSAEADDRNOTAVAIL (10049) and the engine takes its
// own existing not-ready path without the wait. Data-only write (no code
// patch, no trampoline).
//
// The literal is found by scanning the module's readable sections for the
// exact 12-byte string followed by NUL - no hardcoded RVA and no PE-stamp
// gate, so an engine update that keeps the same server address keeps working
// unchanged. The PE stamp/size are only reported (`build=ok|mismatch`) for
// diagnosis. If the literal is gone (address changed / feature removed) the
// shim refuses (`site-not-found`), logs why, and the client stays on the
// shipped path - re-derive per docs/engine_host/FAST_STARTUP.md.
//
//   RC_STARTUP=nodb        rewrite the shader-DB IP (startup fix)
//   unset / engine / 0     no-op (shipped behaviour, DLL never loaded)
//
// Kill switch: do not set RC_STARTUP / RC_STARTUP=engine.

#include <windows.h>
#include <stdio.h>
#include <string.h>

#define SHIM_EXPORT extern "C" __declspec(dllexport)

// KG3D_MaterialSystemX64.dll, measured 2026-10-04 (reported only; not a gate)
static const DWORD MATERIAL_TIMESTAMP = 0x6AA7BCE6;
static const DWORD MATERIAL_SIZE_OF_IMAGE = 0xAE7000;

static const char kDbIp[13] = "10.11.10.102";    // + implicit NUL at [12]
static const int  kMaxSites = 8;

static const char* kMaterialName = "KG3D_MaterialSystemX64.dll";

static int   g_nodb = 0;
static int   g_applied = 0;
static char  g_status[256] = "not initialised";
static volatile LONG g_registered = 0;
static PVOID g_cookie = NULL;

static bool PeMatches(BYTE* base, DWORD stamp, DWORD soi)
{
    if (base == NULL || base[0] != 'M' || base[1] != 'Z') return false;
    DWORD pe = *(DWORD*)(base + 0x3C);
    if (*(DWORD*)(base + pe) != 0x4550) return false; // 'PE\0\0'
    return *(DWORD*)(base + pe + 0x08) == stamp
        && *(DWORD*)(base + pe + 0x50) == soi;
}

// Scan readable sections for the exact literal (standalone C string, next
// byte NUL) and rewrite it to "0.0.0.0". Returns the number of sites patched.
static int PatchLiteral(BYTE* base, DWORD sizeOfImage, BYTE* pe)
{
    int sites = 0;
    WORD nsec = *(WORD*)(pe + 0x06);
    DWORD optSize = *(WORD*)(pe + 0x14);
    BYTE* sec = pe + 0x18 + optSize;
    for (WORD i = 0; i < nsec && sites < kMaxSites; i++, sec += 0x28)
    {
        DWORD chars = *(DWORD*)(sec + 0x24);
        if ((chars & IMAGE_SCN_MEM_READ) == 0) continue;
        DWORD va = *(DWORD*)(sec + 0x0C);
        DWORD vsize = *(DWORD*)(sec + 0x08);
        if (va >= sizeOfImage) continue;
        if (va + vsize > sizeOfImage) vsize = sizeOfImage - va;
        BYTE* p = base + va;
        BYTE* end = p + vsize;
        for (BYTE* q = p; q + 13 <= end; q++)
        {
            if (q[0] != '1' || memcmp(q, kDbIp, 12) != 0) continue;
            if (q[12] != 0) { q += 12; continue; }   // not a standalone literal
            DWORD old = 0;
            if (!VirtualProtect(q, 8, PAGE_READWRITE, &old)) continue;
            memcpy(q, "0.0.0.0", 7);
            q[7] = 0;
            VirtualProtect(q, 8, old, &old);
            sites++;
            q += 12;
            if (sites >= kMaxSites) break;
        }
    }
    return sites;
}

static void ApplyMaterial(BYTE* base)
{
    if (g_applied || !g_nodb) return;
    g_applied = 1;
    if (base == NULL || base[0] != 'M' || base[1] != 'Z') return;
    BYTE* pe = base + *(DWORD*)(base + 0x3C);
    if (*(DWORD*)pe != 0x4550) return; // 'PE\0\0'
    bool buildOk = PeMatches(base, MATERIAL_TIMESTAMP, MATERIAL_SIZE_OF_IMAGE);
    DWORD soi = *(DWORD*)(pe + 0x50);
    int sites = PatchLiteral(base, soi, pe);
    if (sites > 0)
        sprintf_s(g_status, "mode=nodb applied=1 sites=%d build=%s",
                  sites, buildOk ? "ok" : "mismatch");
    else
        sprintf_s(g_status, "mode=nodb applied=0 site-not-found build=%s",
                  buildOk ? "ok" : "mismatch");
}

typedef struct { USHORT Length; USHORT MaximumLength; PWSTR Buffer; } ShimUnicodeString;

typedef struct
{
    ULONG Flags;
    const ShimUnicodeString* FullDllName;
    const ShimUnicodeString* BaseDllName;
    PVOID DllBase;
    ULONG SizeOfImage;
} ShimDllNotificationData;

typedef VOID (CALLBACK *ShimLdrNotify)(ULONG reason,
    const ShimDllNotificationData* data, PVOID context);
typedef LONG (NTAPI *ShimLdrRegister)(ULONG flags, ShimLdrNotify fn,
    PVOID context, PVOID* cookie);

static VOID CALLBACK OnDllNotification(ULONG reason,
    const ShimDllNotificationData* data, PVOID context)
{
    (void)context;
    if (reason != 1 || data == NULL || data->DllBase == NULL) return; // LOADED
    const ShimUnicodeString* name = data->BaseDllName;
    if (name == NULL || name->Buffer == NULL) return;
    if (name->Length == 0 || name->Length > 64 * 2) return;
    wchar_t buf[65];
    int len = name->Length / 2;
    for (int i = 0; i < len; i++) buf[i] = name->Buffer[i];
    buf[len] = 0;
    if (_wcsicmp(buf, L"KG3D_MaterialSystemX64.dll") == 0)
        ApplyMaterial((BYTE*)data->DllBase);
}

SHIM_EXPORT int RC_Startup_EarlyInit()
{
    char v[64];
    DWORD n = GetEnvironmentVariableA("RC_STARTUP", v, sizeof(v));
    if (n == 0 || v[0] == 0
        || _stricmp(v, "engine") == 0 || _stricmp(v, "0") == 0)
    {
        sprintf_s(g_status, "mode=engine applied=0 (RC_STARTUP unset)");
        return 0;
    }
    if (_stricmp(v, "nodb") == 0)
    {
        g_nodb = 1;
    }
    else
    {
        sprintf_s(g_status, "mode=bad-value applied=0 (use nodb|engine)");
        return 2;
    }

    // Register the notification first, then re-check: covers a load racing
    // between the check and the registration.
    HMODULE ntdll = GetModuleHandleA("ntdll.dll");
    ShimLdrRegister reg = ntdll
        ? (ShimLdrRegister)GetProcAddress(ntdll, "LdrRegisterDllNotification")
        : NULL;
    if (reg != NULL && InterlockedCompareExchange(&g_registered, 1, 0) == 0)
    {
        if (reg(0, OnDllNotification, NULL, &g_cookie) != 0)
        {
            InterlockedExchange(&g_registered, 0);
            sprintf_s(g_status, "mode=nodb applied=0 ldr-register-failed");
            return 3;
        }
    }
    HMODULE mod = GetModuleHandleA(kMaterialName);
    if (mod != NULL)
    {
        ApplyMaterial((BYTE*)mod);
    }
    else if (!g_applied)
    {
        sprintf_s(g_status, "mode=nodb applied=0 armed (waiting %s)", kMaterialName);
    }
    return 0;
}

SHIM_EXPORT const char* RC_Startup_Status()
{
    return g_status;
}
