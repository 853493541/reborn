// camera_shim.dll - Step C ABI glue into the host engine
// (docs/camera/HANDOFF.md). Contains no camera logic: it only calls the
// engine's own functions with the right objects/context, so the client keeps
// the game mechanism instead of approximating it.
//
// Priorities (from the measured deviations register):
//   1. view near plane get/set   (C7/B1/B6 exit; H2 see-through)
//   2. absolute camera Y/look-at (A1-A9, B6, D3 exit)
//   3. FilterCamera/mask 0x301 scene query (D1/D4/D5 exit)
//
// Version guard: the engine module must match the build this shim was
// developed against (PE TimeDateStamp + SizeOfImage). Any other build makes
// RC_Shim_Init fail and the client falls back to the current behaviour.
//
// Current stage: object recon. The host engine's RTTI gives the vtable RVAs
// (image base 0x180000000) for KG3D_Camera / KG3D_SceneView / KG3D_Window /
// KG3D_Scene; RC_Probe_FindObjects scans the engine/window/scene objects for
// pointers whose vtable matches, which locates the live instances without
// calling anything.

#include <windows.h>
#include <stdio.h>

#define SHIM_EXPORT extern "C" __declspec(dllexport)

// KG3DEngineDX11EX64.dll, measured 2026-09-26
static const DWORD ENGINE_TIMESTAMP = 0x6AA7C1F5;
static const DWORD ENGINE_SIZE_OF_IMAGE = 0x2EA7000;

// RVAs verified in client/EngineRay.cs and docs/camera/
static const DWORD RVA_RAY_TERRAIN = 0x976260; // KG3D_Scene::RayIntersectionTerrain

// RTTI-derived vtable RVAs (VA - 0x180000000)
static const DWORD RVA_VT_CAMERA   = 0x21EF6E0; // .?AVKG3D_Camera@@
static const DWORD RVA_VT_CAMERA2  = 0x21EF6F0;
static const DWORD RVA_VT_SCENEVIEW = 0x21D4970; // .?AVKG3D_SceneView@@
static const DWORD RVA_VT_SCENEVIEW2 = 0x21D4DE8;
static const DWORD RVA_VT_WINDOW   = 0x21E1970; // .?AVKG3D_Window@@
static const DWORD RVA_VT_SCENE    = 0x21C5598; // .?AVKG3D_Scene@@

static HMODULE g_engine = NULL;
static BYTE*   g_base = NULL;
static DWORD   g_size = 0;
static char    g_status[4096] = "not initialised";

// Objects found by the last RC_Probe_Deep run: embedded vtable instances.
static const int kMaxFound = 32;
static void* g_foundPtr[kMaxFound];
static char  g_foundCls[kMaxFound][16];
static int   g_foundCount = 0;
static int   g_foundOff[kMaxFound];

static const char* kEngineName = "KG3DEngineDX11EX64.dll";

typedef void* (__stdcall* GetEngine2Fn)(void);
typedef void* (__stdcall* EngineMethodFn)(void* self);

static bool PeMatches(BYTE* base)
{
    if (base == NULL || base[0] != 'M' || base[1] != 'Z') return false;
    DWORD pe = *(DWORD*)(base + 0x3C);
    if (*(DWORD*)(base + pe) != 0x4550) return false; // 'PE\0\0'
    DWORD stamp = *(DWORD*)(base + pe + 0x08);
    DWORD soi = *(DWORD*)(base + pe + 0x50);
    return stamp == ENGINE_TIMESTAMP && soi == ENGINE_SIZE_OF_IMAGE;
}

static void* ResolveScene()
{
    HMODULE mod = GetModuleHandleA(kEngineName);
    if (mod == NULL) return NULL;
    GetEngine2Fn getEngine = (GetEngine2Fn)GetProcAddress(mod, "KG3D_GetEngine2");
    EngineMethodFn getWindow = (EngineMethodFn)GetProcAddress(mod,
        "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
    EngineMethodFn getScene = (EngineMethodFn)GetProcAddress(mod,
        "?Get3DScene2@KG3D_Window@@UEAAPEAVKG3D_Scene@@XZ");
    if (getEngine == NULL || getWindow == NULL || getScene == NULL) return NULL;
    void* engine = getEngine();
    if (engine == NULL) return NULL;
    void* window = getWindow(engine);
    if (window == NULL) return NULL;
    return getScene(window);
}

SHIM_EXPORT int RC_Shim_Init()
{
    if (g_base != NULL) return 0;
    g_engine = GetModuleHandleA(kEngineName);
    if (g_engine == NULL)
    {
        sprintf_s(g_status, "engine module not loaded");
        return 1;
    }
    g_base = (BYTE*)g_engine;
    if (!PeMatches(g_base))
    {
        sprintf_s(g_status, "engine build mismatch (timestamp/size)");
        g_base = NULL;
        return 2;
    }
    g_size = *(DWORD*)(g_base + *(DWORD*)(g_base + 0x3C) + 0x50);
    sprintf_s(g_status, "ready base=0x%p size=0x%X peak=0x%08X",
              g_base, g_size, *(DWORD*)(g_base + RVA_RAY_TERRAIN));
    return 0;
}

SHIM_EXPORT const char* RC_Shim_Status()
{
    return g_status;
}

SHIM_EXPORT int RC_Shim_AbiSelfTest()
{
    if (RC_Shim_Init() != 0) return -1;
    return 0x53484D01;
}

static bool Readable(const void* p, size_t n)
{
    MEMORY_BASIC_INFORMATION mbi;
    if (VirtualQuery(p, &mbi, sizeof(mbi)) == 0) return false;
    if (mbi.State != MEM_COMMIT) return false;
    if (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) return false;
    BYTE* end = (BYTE*)p + n;
    if (end < (BYTE*)p) return false;
    return end <= (BYTE*)mbi.BaseAddress + mbi.RegionSize;
}

// Bytes readable from p within its current region (0 when not readable).
static size_t ReadableSpan(const void* p)
{
    MEMORY_BASIC_INFORMATION mbi;
    if (VirtualQuery(p, &mbi, sizeof(mbi)) == 0) return 0;
    if (mbi.State != MEM_COMMIT) return 0;
    if (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) return 0;
    BYTE* regionEnd = (BYTE*)mbi.BaseAddress + mbi.RegionSize;
    if (regionEnd <= (BYTE*)p) return 0;
    return (size_t)(regionEnd - (BYTE*)p);
}

struct VtName { DWORD rva; const char* name; };
static const VtName kVts[] = {
    { RVA_VT_CAMERA,     "Camera" },
    { RVA_VT_CAMERA2,    "Camera2" },
    { RVA_VT_SCENEVIEW,  "SceneView" },
    { RVA_VT_SCENEVIEW2, "SceneView2" },
    { RVA_VT_WINDOW,     "Window" },
    { RVA_VT_SCENE,      "Scene" },
};

// Scan [obj, obj+range) for qwords whose value equals a known vtable VA
// (base+rva). A match means that qword is the start of (or a pointer to) a
// live object of that class. Both the holder offset and the target pointer
// are reported.
SHIM_EXPORT const char* RC_Probe_FindObjects(unsigned range)
{
    if (RC_Shim_Init() != 0) return g_status;
    void* scene = ResolveScene();
    if (scene == NULL) { sprintf_s(g_status, "scene unresolved"); return g_status; }

    struct Holder { void* obj; size_t off; const char* cls; void* val; };
    Holder hits[64];
    int n = 0;

    void* roots[3] = { NULL, NULL, NULL };
    HMODULE mod = GetModuleHandleA(kEngineName);
    GetEngine2Fn getEngine = (GetEngine2Fn)GetProcAddress(mod, "KG3D_GetEngine2");
    EngineMethodFn getWindow = (EngineMethodFn)GetProcAddress(mod,
        "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
    roots[0] = scene;
    if (getEngine && getWindow) { roots[1] = getEngine(); roots[2] = getWindow(roots[1]); }

    for (int r = 0; r < 3; r++)
    {
        BYTE* o = (BYTE*)roots[r];
        if (o == NULL) continue;
        for (size_t off = 0; off + 8 <= range && n < 64; off += 8)
        {
            if (!Readable(o + off, 8)) break;
            void* v = *(void**)(o + off);
            for (int k = 0; k < (int)(sizeof(kVts) / sizeof(kVts[0])); k++)
            {
                if (v == g_base + kVts[k].rva)
                {
                    hits[n].obj = o; hits[n].off = off; hits[n].cls = kVts[k].name; hits[n].val = v;
                    n++;
                    break;
                }
            }
        }
    }

    int written = sprintf_s(g_status, "roots scene=%p engine=%p window=%p hits=%d",
                            roots[0], roots[1], roots[2], n);
    for (int i = 0; i < n && written < (int)sizeof(g_status) - 96; i++)
    {
        written += sprintf_s(g_status + written, sizeof(g_status) - written,
                             " | %s@%p+0x%zX vtable=%p",
                             hits[i].cls, hits[i].obj, hits[i].off, hits[i].val);
    }
    return g_status;
}

// Dump 64 bytes at obj+off as floats/hex; helps identify near/far fields.
SHIM_EXPORT const char* RC_Probe_Dump(void* obj, unsigned off)
{
    if (RC_Shim_Init() != 0) return g_status;
    if (!Readable((BYTE*)obj + off, 64)) { sprintf_s(g_status, "unreadable"); return g_status; }
    float* f = (float*)((BYTE*)obj + off);
    int written = sprintf_s(g_status, "%p+0x%X f:", obj, off);
    for (int i = 0; i < 16 && written < (int)sizeof(g_status) - 32; i++)
        written += sprintf_s(g_status + written, sizeof(g_status) - written, " %.3g", f[i]);
    return g_status;
}

// Scan the engine module's writable committed memory for qwords equal to a
// known vtable VA. A hit is the first qword of a statically allocated object
// of that class (singletons and static sub-objects).
SHIM_EXPORT const char* RC_Probe_ScanModule()
{
    if (RC_Shim_Init() != 0) return g_status;
    int written = sprintf_s(g_status, "module vtables:");
    int hits = 0;
    BYTE* p = g_base;
    BYTE* end = g_base + g_size;
    while (p < end && hits < 24)
    {
        MEMORY_BASIC_INFORMATION mbi;
        if (VirtualQuery(p, &mbi, sizeof(mbi)) == 0) break;
        BYTE* regionEnd = (BYTE*)mbi.BaseAddress + mbi.RegionSize;
        bool wr = mbi.State == MEM_COMMIT &&
                  (mbi.Protect & (PAGE_READWRITE | PAGE_WRITECOPY | PAGE_EXECUTE_READWRITE));
        if (wr)
        {
            for (BYTE* q = (BYTE*)mbi.BaseAddress; q + 8 <= regionEnd && hits < 24; q += 8)
            {
                void* v = *(void**)q;
                for (int k = 0; k < (int)(sizeof(kVts) / sizeof(kVts[0])); k++)
                {
                    if (v == g_base + kVts[k].rva)
                    {
                        written += sprintf_s(g_status + written, sizeof(g_status) - written,
                                             " | %s@%p", kVts[k].name, q);
                        hits++;
                        break;
                    }
                }
            }
        }
        p = regionEnd;
    }
    if (hits == 0)
        sprintf_s(g_status + written, sizeof(g_status) - written, " none");
    return g_status;
}

// One-level pointer scan: for qwords P in the root objects that are readable
// pointers, scan the target object for the vtable VAs. Reports
// root+0xOFF -> target+0xOFF2.
SHIM_EXPORT const char* RC_Probe_Deep(unsigned range)
{
    if (RC_Shim_Init() != 0) return g_status;
    g_foundCount = 0;
    void* scene = ResolveScene();
    if (scene == NULL) { sprintf_s(g_status, "scene unresolved"); return g_status; }
    HMODULE mod = GetModuleHandleA(kEngineName);
    GetEngine2Fn getEngine = (GetEngine2Fn)GetProcAddress(mod, "KG3D_GetEngine2");
    EngineMethodFn getWindow = (EngineMethodFn)GetProcAddress(mod,
        "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
    void* roots[3] = { scene, NULL, NULL };
    if (getEngine && getWindow) { roots[1] = getEngine(); roots[2] = getWindow(roots[1]); }

    int written = sprintf_s(g_status, "deep:");
    int hits = 0;
    for (int r = 0; r < 3; r++)
    {
        BYTE* o = (BYTE*)roots[r];
        if (o == NULL) continue;
        for (size_t off = 0; off + 8 <= range && hits < 24; off += 8)
        {
            size_t span = ReadableSpan(o + off);
            if (span < 8) break;
            void* t = *(void**)(o + off);
            size_t tspan = ReadableSpan(t);
            if (tspan < 8) continue;
            size_t limit = tspan < 0x8000 ? tspan : 0x8000;
            for (size_t off2 = 0; off2 + 8 <= limit && hits < 24; off2 += 8)
            {
                void* v = *(void**)((BYTE*)t + off2);
                for (int k = 0; k < (int)(sizeof(kVts) / sizeof(kVts[0])); k++)
                {
                    if (v == g_base + kVts[k].rva)
                    {
                        written += sprintf_s(g_status + written, sizeof(g_status) - written,
                                             " | r%d+0x%zX -> %p+0x%zX %s",
                                             r, off, t, off2, kVts[k].name);
                        hits++;
                        if (g_foundCount < kMaxFound)
                        {
                            g_foundPtr[g_foundCount] = (BYTE*)t + off2;
                            strncpy_s(g_foundCls[g_foundCount], kVts[k].name, _TRUNCATE);
                            g_foundCount++;
                        }
                        break;
                    }
                }
            }
        }
    }
    if (hits == 0)
        sprintf_s(g_status + written, sizeof(g_status) - written, " none");
    return g_status;
}

SHIM_EXPORT int RC_Probe_ObjectCount()
{
    return g_foundCount;
}

SHIM_EXPORT void* RC_Probe_Object(int i)
{
    if (i < 0 || i >= g_foundCount) return NULL;
    return g_foundPtr[i];
}

SHIM_EXPORT const char* RC_Probe_ObjectClass(int i)
{
    if (i < 0 || i >= g_foundCount) return "";
    return g_foundCls[i];
}

SHIM_EXPORT int RC_Probe_ObjectOff(int i)
{
    if (i < 0 || i >= g_foundCount) return -1;
    return g_foundOff[i];
}

// Find exact float triples (x,y,z) in [obj, obj+len); reports up to 12 offsets.
SHIM_EXPORT const char* RC_Probe_FindTriple(void* obj, unsigned len, float x, float y, float z)
{
    if (RC_Shim_Init() != 0) return g_status;
    size_t span = ReadableSpan(obj);
    if (span < 12) { sprintf_s(g_status, "unreadable"); return g_status; }
    if (len > span) len = (unsigned)(span - 12);
    int written = sprintf_s(g_status, "triple:");
    int hits = 0;
    for (unsigned off = 0; off + 12 <= len && hits < 12; off += 4)
    {
        float* f = (float*)((BYTE*)obj + off);
        if (f[0] == x && f[1] == y && f[2] == z)
        {
            written += sprintf_s(g_status + written, sizeof(g_status) - written,
                                 " +0x%X", off);
            hits++;
        }
    }
    if (hits == 0)
        sprintf_s(g_status + written, sizeof(g_status) - written, " none");
    return g_status;
}

// Dump 8 qwords at obj+off, annotation: engine pointer -> base+rva, low dword
// as float. Reveals vtables/pointers/floats in one call.
SHIM_EXPORT const char* RC_Probe_DumpQ(void* obj, unsigned off)
{
    if (RC_Shim_Init() != 0) return g_status;
    if (!Readable((BYTE*)obj + off, 64)) { sprintf_s(g_status, "unreadable"); return g_status; }
    unsigned long long* q = (unsigned long long*)((BYTE*)obj + off);
    int written = sprintf_s(g_status, "%p+0x%X:", obj, off);
    for (int i = 0; i < 8 && written < (int)sizeof(g_status) - 48; i++)
    {
        unsigned long long v = q[i];
        float f;
        memcpy(&f, &v, 4);
        if (v >= (unsigned long long)g_base && v < (unsigned long long)(g_base + g_size))
            written += sprintf_s(g_status + written, sizeof(g_status) - written,
                                 " [q+%u]rva+0x%llX", i * 8, v - (unsigned long long)g_base);
        else
            written += sprintf_s(g_status + written, sizeof(g_status) - written,
                                 " [q+%u]%llX(f=%.4g)", i * 8, v, f);
    }
    return g_status;
}

// Dump count floats at obj+off (count <= 64).
SHIM_EXPORT const char* RC_Probe_DumpF(void* obj, unsigned off, unsigned count)
{
    if (RC_Shim_Init() != 0) return g_status;
    if (count > 64) count = 64;
    if (!Readable((BYTE*)obj + off, (size_t)count * 4)) { sprintf_s(g_status, "unreadable"); return g_status; }
    float* f = (float*)((BYTE*)obj + off);
    int written = sprintf_s(g_status, "%p+0x%X:", obj, off);
    for (unsigned i = 0; i < count && written < (int)sizeof(g_status) - 24; i++)
        written += sprintf_s(g_status + written, sizeof(g_status) - written, " %.6g", f[i]);
    return g_status;
}

// Read a qword at an absolute VA; annotation engine pointer -> rva.
SHIM_EXPORT const char* RC_Probe_ReadQ(void* va)
{
    if (RC_Shim_Init() != 0) return g_status;
    if (!Readable(va, 8)) { sprintf_s(g_status, "unreadable"); return g_status; }
    unsigned long long v = *(unsigned long long*)va;
    if (v >= (unsigned long long)g_base && v < (unsigned long long)(g_base + g_size))
        sprintf_s(g_status, "[%p]=0x%llX -> rva+0x%llX", va, v, v - (unsigned long long)g_base);
    else
        sprintf_s(g_status, "[%p]=0x%llX", va, v);
    return g_status;
}

// Write a float at obj+off (bounded readability check); returns the value read
// back. Used for the differential near-plane tests.
SHIM_EXPORT int RC_Probe_WriteF(void* obj, unsigned off, float v)
{
    if (RC_Shim_Init() != 0) return -1;
    if (!Readable((BYTE*)obj + off, 4)) return -2;
    *(float*)((BYTE*)obj + off) = v;
    return 0;
}

// ---- Step C capability 2: absolute camera position + look-at -------------
// The active render camera object is the one whose fields contain the live
// camera position returned by KGSceneCLR.GetCameraPos. Binding scans the same
// roots as RC_Probe_Deep for that triple, then RC_CamSet writes position and
// target directly, bypassing the host SetCameraPos Y clamp (B6).
static void* g_camObj = NULL;
static int   g_camPosOff = -1;

static void* RootsScene(void* roots[3])
{
    void* scene = ResolveScene();
    roots[0] = scene; roots[1] = NULL; roots[2] = NULL;
    if (scene == NULL) return NULL;
    HMODULE mod = GetModuleHandleA(kEngineName);
    GetEngine2Fn getEngine = (GetEngine2Fn)GetProcAddress(mod, "KG3D_GetEngine2");
    EngineMethodFn getWindow = (EngineMethodFn)GetProcAddress(mod,
        "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
    if (getEngine && getWindow) { roots[1] = getEngine(); roots[2] = getWindow(roots[1]); }
    return scene;
}

// slot pointers are checked to lie inside the engine module before any call
static bool InEngineCode(const void* p)
{
    if (p == NULL || g_base == NULL) return false;
    const BYTE* b = (const BYTE*)p;
    return b >= g_base && b < g_base + g_size;
}

// Plausible x64 function start: the address is in the engine module, the
// first bytes are not int3/zero padding, and either the 4 bytes before are
// int3 padding (a real function boundary in this build) or the first bytes
// match a common prologue. This rejects data pointers and mid-function
// entries (the earlier KG3D_Camera call crash).
static bool PlausibleFn(const void* p)
{
    const BYTE* b = (const BYTE*)p;
    if (!InEngineCode(p) || !Readable((BYTE*)b - 4, 12)) return false;
    if (b[0] == 0xCC || b[0] == 0x00) return false;
    bool pad = b[-1] == 0xCC && b[-2] == 0xCC && b[-3] == 0xCC && b[-4] == 0xCC;
    if (pad) return true;
    if (b[0] == 0x48 && b[1] == 0x89 && (b[2] == 0x5C || b[2] == 0x4C || b[2] == 0x54)) return true;
    if (b[0] == 0x48 && b[1] == 0x83 && b[2] == 0xEC) return true;
    if (b[0] == 0x48 && b[1] == 0x81 && b[2] == 0xEC) return true;
    if (b[0] == 0x40 && (b[1] == 0x53 || b[1] == 0x55 || b[1] == 0x56 || b[1] == 0x57)) return true;
    if (b[0] == 0x48 && b[1] == 0x8B && b[2] == 0xC4) return true;
    if (b[0] == 0x4C && b[1] == 0x8B && (b[2] == 0xDC || b[2] == 0xD1 || b[2] == 0xD9)) return true;
    if (b[0] == 0x4C && b[1] == 0x89 && (b[2] == 0x44 || b[2] == 0x4C || b[2] == 0x54)) return true;
    return false;
}

// A camera object: vtable in the engine module and the setters the managed
// camera API uses (vt[+0x50] position, vt[+0x58] look-at) are engine code too.
static bool LooksLikeCameraObject(void* t)
{
    if (t == NULL) return false;
    const void* vt = Readable((BYTE*)t, 8) ? *(void**)t : NULL;
    if (vt == g_base + RVA_VT_CAMERA || vt == g_base + RVA_VT_CAMERA2) return true;
    if (!InEngineCode(vt) || !Readable((BYTE*)vt, 0x68)) return false;
    void* setPos = *(void**)((const BYTE*)vt + 0x50);
    void* setTgt = *(void**)((const BYTE*)vt + 0x58);
    void* getPos = *(void**)((const BYTE*)vt + 0x48);
    void* getTgt = *(void**)((const BYTE*)vt + 0x60);
    // the real camera has distinct position/target accessors; generic state
    // objects expose one shared function in several slots (seen in the dump)
    if (setPos == setTgt || getPos == getTgt) return false;
    return PlausibleFn(setPos) && PlausibleFn(setTgt) &&
           PlausibleFn(getPos) && PlausibleFn(getTgt);
}

SHIM_EXPORT int RC_CamBind(float x, float y, float z)
{
    if (RC_Shim_Init() != 0) return -1;
    g_camObj = NULL; g_camPosOff = -1;
    void* roots[3];
    if (RootsScene(roots) == NULL) return -2;
    // pass 1: real camera objects (known vtables or the camera vtable contract)
    // pass 2: any object containing the triple (render snapshot fallback)
    for (int pass = 0; pass < 2; pass++)
    {
        for (int r = 0; r < 3; r++)
        {
            BYTE* o = (BYTE*)roots[r];
            if (o == NULL) continue;
            for (size_t off = 0; off + 8 <= 0x20000; off += 8)
            {
                size_t span = ReadableSpan(o + off);
                if (span < 8) break;
                void* t = *(void**)(o + off);
                size_t tspan = ReadableSpan(t);
                if (tspan < 0x40) continue;
                if (pass == 0 && !LooksLikeCameraObject(t)) continue;
                size_t limit = tspan < 0x1000 ? tspan : 0x1000;
                for (size_t ot = 0; ot + 12 <= limit; ot += 4)
                {
                    float* f = (float*)((BYTE*)t + ot);
                    if (f[0] == x && f[1] == y && f[2] == z)
                    {
                        g_camObj = t; g_camPosOff = (int)ot;
                        return pass;   // 0 = camera object, 1 = snapshot fallback
                    }
                }
            }
        }
    }
    return -3;
}

SHIM_EXPORT int RC_CamSet(float x, float y, float z, float tx, float ty, float tz, int writeTarget)
{
    if (g_camObj == NULL || g_camPosOff < 0) return -1;
    if (!Readable((BYTE*)g_camObj + g_camPosOff, 0x30)) return -2;
    float* p = (float*)((BYTE*)g_camObj + g_camPosOff);
    p[0] = x; p[1] = y; p[2] = z;
    if (writeTarget)
    {
        float* t = p + 4;   // target/look-at sits 0x10 after the position triple
        t[0] = tx; t[1] = ty; t[2] = tz;
    }
    return 0;
}

// D6 crash guard (registered host bypass, kill switch RC_PATCH_D6=0).
// KG3DEngineDX11EX64+0x11D03B6 dereferences a null material-store pointer
// (the editor install lacks the build-machine DataStores): at 0x11D03B6
//   mov rbx,[rsi] / mov rcx,[rsp+0x78] / ...
// The trampoline only changes the rsi==0 case: it returns E_FAIL through the
// function epilogue (0x11D0840) instead of dereferencing null. The non-null
// path executes the original two instructions and resumes at 0x11D03BE.
static void* g_d6cave = NULL;

SHIM_EXPORT int RC_PatchD6()
{
    if (RC_Shim_Init() != 0) return -1;
    BYTE* site = g_base + 0x11D03B6;
    const BYTE expect[8] = { 0x48, 0x8B, 0x1E, 0x48, 0x8B, 0x4C, 0x24, 0x78 };
    if (memcmp(site, expect, 8) != 0) return -2;   // version guard
    if (g_d6cave == NULL)
    {
        g_d6cave = VirtualAlloc(NULL, 64, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (g_d6cave == NULL) return -3;
    }
    BYTE* c = (BYTE*)g_d6cave;
    BYTE* resume = g_base + 0x11D03BE;
    BYTE* epilogue = g_base + 0x11D0840;
    int i = 0;
    c[i++] = 0x48; c[i++] = 0x85; c[i++] = 0xF6;             // test rsi,rsi
    c[i++] = 0x74; c[i++] = 0x0D;                             // jz +13 -> bail
    c[i++] = 0x48; c[i++] = 0x8B; c[i++] = 0x1E;              // mov rbx,[rsi]
    c[i++] = 0x48; c[i++] = 0x8B; c[i++] = 0x4C; c[i++] = 0x24; c[i++] = 0x78; // mov rcx,[rsp+0x78]
    c[i++] = 0xE9; *(int*)(c + i) = (int)(resume - (c + i + 4)); i += 4;
    c[i++] = 0xB8; *(int*)(c + i) = 0x80004005; i += 4;       // mov eax,E_FAIL
    c[i++] = 0xE9; *(int*)(c + i) = (int)(epilogue - (c + i + 4)); i += 4;
    DWORD old;
    if (!VirtualProtect(site, 5, PAGE_EXECUTE_READWRITE, &old)) return -4;
    site[0] = 0xE9;
    *(int*)(site + 1) = (int)(c - (site + 5));
    VirtualProtect(site, 5, old, &old);
    FlushInstructionCache(GetCurrentProcess(), site, 5);
    return 0;
}

SHIM_EXPORT const char* RC_PatchD6Info()
{
    if (g_base == NULL) { sprintf_s(g_status, "engine not init"); return g_status; }
    BYTE* site = g_base + 0x11D03B6;
    sprintf_s(g_status, "site=%p byte0=%02X cave=%p", site, site[0], g_d6cave);
    return g_status;
}

SHIM_EXPORT void* RC_CamObject()
{
    return g_camObj;
}

SHIM_EXPORT const char* RC_CamInfo()
{
    sprintf_s(g_status, "cam=%p posOff=0x%X bound=%d",
              g_camObj, g_camPosOff, (g_camObj != NULL && g_camPosOff >= 0) ? 1 : 0);
    return g_status;
}

// Engine-faithful camera set: call the camera object's own vtable setters,
// exactly the calls the managed KGSceneCLR.SetCameraPos IL makes
// (cam->vt[+0x50](pos,0) and cam->vt[+0x58](target,0)); no field writes, so
// the engine updates its own state including the look-at.
// Exact mirror of MovieEngineCLR KGSceneCLR.SetCameraPos IL on the client's
// own scene object (m_pScene handed over by the client):
//   cam = scene->vt[+0x50]();
//   cam->vt[+0x50](pos, 0); cam->vt[+0x58](target, 0);
static void* g_sceneObj = NULL;
static void* g_camFromScene = NULL;

SHIM_EXPORT int RC_CamUseScene(void* scene)
{
    g_sceneObj = scene;
    g_camFromScene = NULL;
    return scene != NULL ? 0 : -1;
}

// Given the CLR wrapper object (KGSceneCLR), find its native m_pScene field:
// scan the first 0x400 bytes for a pointer whose vtable is engine code, whose
// get-camera slot (vt[+0x50]) is engine code, and which actually returns a
// camera whose own setters (vt[+0x50]/[+0x58]) are engine code. Returns the
// field offset or a negative error; stores scene and camera on success.
// Use a camera object found by the object scanner directly (it must carry the
// engine camera vtable contract: vt[+0x50] position setter, vt[+0x58] target).
SHIM_EXPORT int RC_CamUseObj(void* cam)
{
    g_sceneObj = NULL;
    g_camFromScene = NULL;
    if (cam == NULL || RC_Shim_Init() != 0) return -1;
    if (!Readable((BYTE*)cam, 8)) return -2;
    void* vt = *(void**)cam;
    if (!InEngineCode(vt) || !Readable((BYTE*)vt, 0x60)) return -3;
    if (!InEngineCode(*(void**)((BYTE*)vt + 0x50)) ||
        !InEngineCode(*(void**)((BYTE*)vt + 0x58))) return -4;
    g_camFromScene = cam;
    return 0;
}

SHIM_EXPORT const char* RC_DumpClr(void* clrObj)
{
    int n = sprintf_s(g_status, "clr=%p:", clrObj);
    if (clrObj == NULL || RC_Shim_Init() != 0) return g_status;
    int shown = 0;
    for (size_t off = 0; off + 8 <= 0x400 && shown < 10; off += 8)
    {
        if (!Readable((BYTE*)clrObj + off, 8)) break;
        void* p = *(void**)((BYTE*)clrObj + off);
        if (p == NULL || !Readable((BYTE*)p, 8)) continue;
        void* vt = *(void**)p;
        if (!InEngineCode(vt)) continue;
        void* s50 = Readable((BYTE*)vt, 0x58) ? *(void**)((BYTE*)vt + 0x50) : NULL;
        void* s70 = Readable((BYTE*)vt, 0x78) ? *(void**)((BYTE*)vt + 0x70) : NULL;
        n += sprintf_s(g_status + n, sizeof(g_status) - n,
                       " +0x%zX=%p vt=%p s50=%p(%d) s70=%p(%d)",
                       off, p, vt, s50, InEngineCode(s50) ? 1 : 0,
                       s70, InEngineCode(s70) ? 1 : 0);
        shown++;
    }
    return g_status;
}

SHIM_EXPORT int RC_CamUseClr(void* clrObj)
{
    g_sceneObj = NULL;
    g_camFromScene = NULL;
    if (clrObj == NULL || RC_Shim_Init() != 0) return -1;
    for (size_t off = 0; off + 8 <= 0x400; off += 8)
    {
        if (!Readable((BYTE*)clrObj + off, 8)) break;
        void* p = *(void**)((BYTE*)clrObj + off);
        if (p == NULL || !Readable((BYTE*)p, 8)) continue;
        void* vt = *(void**)p;
        if (!InEngineCode(vt) || !Readable((BYTE*)vt, 0x58)) continue;
        void* fnGet = *(void**)((BYTE*)vt + 0x50);
        if (!InEngineCode(fnGet)) continue;
        void* cam = ((void*(*)(void*))fnGet)(p);
        if (cam == NULL || !Readable((BYTE*)cam, 8)) continue;
        void* cvt = *(void**)cam;
        if (!InEngineCode(cvt) || !Readable((BYTE*)cvt, 0x60)) continue;
        if (!InEngineCode(*(void**)((BYTE*)cvt + 0x50)) ||
            !InEngineCode(*(void**)((BYTE*)cvt + 0x58))) continue;
        g_sceneObj = p;
        g_camFromScene = cam;
        return (int)off;
    }
    return -2;
}

SHIM_EXPORT const char* RC_CamSceneInfo()
{
    sprintf_s(g_status, "scene=%p cam=%p", g_sceneObj, g_camFromScene);
    return g_status;
}

SHIM_EXPORT const char* RC_CamVt2Info()
{
    if (g_camFromScene == NULL) { sprintf_s(g_status, "no cam"); return g_status; }
    void** cvt = *(void***)g_camFromScene;
    if (cvt == NULL) { sprintf_s(g_status, "cam=%p no vt", g_camFromScene); return g_status; }
    void* setPos = Readable((BYTE*)cvt, 0x60) ? *(void**)((BYTE*)cvt + 0x50) : NULL;
    void* setTgt = Readable((BYTE*)cvt, 0x60) ? *(void**)((BYTE*)cvt + 0x58) : NULL;
    sprintf_s(g_status, "cam=%p vt=%p setPos=%p(%d) setTgt=%p(%d)",
              g_camFromScene, cvt, setPos, InEngineCode(setPos) ? 1 : 0,
              setTgt, InEngineCode(setTgt) ? 1 : 0);
    return g_status;
}

SHIM_EXPORT const char* RC_DumpObj(void* obj)
{
    if (obj == NULL) { sprintf_s(g_status, "null"); return g_status; }
    void** vt = Readable((BYTE*)obj, 8) ? *(void***)obj : NULL;
    if (vt == NULL) { sprintf_s(g_status, "obj=%p no vt", obj); return g_status; }
    sprintf_s(g_status, "obj=%p vt=%p 48=%p(%d) 50=%p(%d) 58=%p(%d) 60=%p(%d)",
              obj, vt,
              Readable((BYTE*)vt, 0x50) ? *(void**)((BYTE*)vt + 0x48) : NULL,
              Readable((BYTE*)vt, 0x50) && PlausibleFn(*(void**)((BYTE*)vt + 0x48)) ? 1 : 0,
              Readable((BYTE*)vt, 0x58) ? *(void**)((BYTE*)vt + 0x50) : NULL,
              Readable((BYTE*)vt, 0x58) && PlausibleFn(*(void**)((BYTE*)vt + 0x50)) ? 1 : 0,
              Readable((BYTE*)vt, 0x60) ? *(void**)((BYTE*)vt + 0x58) : NULL,
              Readable((BYTE*)vt, 0x60) && PlausibleFn(*(void**)((BYTE*)vt + 0x58)) ? 1 : 0,
              Readable((BYTE*)vt, 0x68) ? *(void**)((BYTE*)vt + 0x60) : NULL,
              Readable((BYTE*)vt, 0x68) && PlausibleFn(*(void**)((BYTE*)vt + 0x60)) ? 1 : 0);
    return g_status;
}

SHIM_EXPORT int RC_CamSetVt2(float x, float y, float z, float tx, float ty, float tz)
{
    void* cam = g_camFromScene;
    if (cam == NULL && g_sceneObj != NULL)
    {
        if (!Readable((BYTE*)g_sceneObj, 8)) return -2;
        void** svt = *(void***)g_sceneObj;
        if (svt == NULL || !Readable((BYTE*)svt, 0x58)) return -2;
        void* fnGet = *(void**)((BYTE*)svt + 0x50);
        if (!InEngineCode(fnGet)) return -3;
        cam = ((void*(*)(void*))fnGet)(g_sceneObj);
        if (cam == NULL) return -4;
    }
    if (cam == NULL) cam = g_camObj;   // bound by RC_CamBind
    if (cam == NULL) return -1;
    void** cvt = *(void***)cam;
    if (cvt == NULL || !Readable((BYTE*)cvt, 0x60)) return -5;
    void* setPos = *(void**)((BYTE*)cvt + 0x50);
    void* setTgt = *(void**)((BYTE*)cvt + 0x58);
    // the engine AVs when a non-function vtable entry is called; require a
    // plausible function prologue (the KG3D_Camera scanner object failed this)
    if (!PlausibleFn(setPos) || !PlausibleFn(setTgt) || setPos == setTgt) return -6;
    float p[3] = { x, y, z };
    float t[3] = { tx, ty, tz };
    ((void(*)(void*, float*, int))setPos)(cam, p, 0);
    ((void(*)(void*, float*, int))setTgt)(cam, t, 0);
    return 0;
}

// position setter only on the bound camera object (isolates which call AVs)
SHIM_EXPORT int RC_CamPosOnly(float x, float y, float z)
{
    void* cam = g_camObj;
    if (cam == NULL) return -1;
    void** cvt = Readable((BYTE*)cam, 8) ? *(void***)cam : NULL;
    if (cvt == NULL || !Readable((BYTE*)cvt, 0x58)) return -2;
    void* fn = *(void**)((BYTE*)cvt + 0x50);
    if (!PlausibleFn(fn)) return -6;
    float p[3] = { x, y, z };
    ((void(*)(void*, float*, int))fn)(cam, p, 0);
    return 0;
}

// hex dump of the bound camera object's vtable slot code (for signature
// decoding without calling it)
SHIM_EXPORT const char* RC_SlotBytes(unsigned off, unsigned len)
{
    void* cam = g_camObj;
    if (cam == NULL) { sprintf_s(g_status, "no cam"); return g_status; }
    void** cvt = Readable((BYTE*)cam, 8) ? *(void***)cam : NULL;
    if (cvt == NULL || !Readable((BYTE*)cvt, off + 8)) { sprintf_s(g_status, "no vt"); return g_status; }
    BYTE* fn = (BYTE*)*(void**)((BYTE*)cvt + off);
    if (!InEngineCode(fn) || !Readable(fn, len)) { sprintf_s(g_status, "slot %X not code", off); return g_status; }
    int n = sprintf_s(g_status, "slot%X=%p:", off, fn);
    for (unsigned i = 0; i < len && i < 32; i++)
        n += sprintf_s(g_status + n, sizeof(g_status) - n, " %02X", fn[i]);
    return g_status;
}

// call the bound camera object's vtable slot at `off` with the look-at triple
// (slot hunting: 0x58 expected, neighbours tried if the class differs)
SHIM_EXPORT int RC_CamTgtSlot(float tx, float ty, float tz, unsigned off)
{
    void* cam = g_camObj;
    if (cam == NULL) return -1;
    void** cvt = Readable((BYTE*)cam, 8) ? *(void***)cam : NULL;
    if (cvt == NULL || !Readable((BYTE*)cvt, off + 8)) return -2;
    void* fn = *(void**)((BYTE*)cvt + off);
    if (!PlausibleFn(fn)) return -6;
    float t[3] = { tx, ty, tz };
    ((void(*)(void*, float*, int))fn)(cam, t, 0);
    return 0;
}

SHIM_EXPORT int RC_CamSetVt(float x, float y, float z, float tx, float ty, float tz)
{
    if (g_camObj == NULL) return -1;
    void** vt = *(void***)g_camObj;
    if (vt == NULL || !Readable((BYTE*)vt, 0x60)) return -2;
    void* setPos = *(void**)((BYTE*)vt + 0x50);
    void* setTgt = *(void**)((BYTE*)vt + 0x58);
    if (!InEngineCode(setPos) || !InEngineCode(setTgt)) return -3;
    float p[3] = { x, y, z };
    float t[3] = { tx, ty, tz };
    ((void(*)(void*, float*, int))setPos)(g_camObj, p, 0);
    ((void(*)(void*, float*, int))setTgt)(g_camObj, t, 0);
    return 0;
}

SHIM_EXPORT const char* RC_CamVtInfo()
{
    if (g_camObj == NULL) { sprintf_s(g_status, "no cam"); return g_status; }
    void** vt = *(void***)g_camObj;
    if (vt == NULL) { sprintf_s(g_status, "no vt"); return g_status; }
    void* setPos = Readable((BYTE*)vt + 0x58, 8) ? *(void**)((BYTE*)vt + 0x50) : NULL;
    void* setTgt = Readable((BYTE*)vt + 0x60, 8) ? *(void**)((BYTE*)vt + 0x58) : NULL;
    sprintf_s(g_status, "cam=%p vt=%p setPos=%p inCode=%d setTgt=%p inCode=%d",
              g_camObj, vt, setPos, InEngineCode(setPos) ? 1 : 0,
              setTgt, InEngineCode(setTgt) ? 1 : 0);
    return g_status;
}

// Find up to 8 (object, offset, y) records whose floats match (x, ?, z).
// Used for the differential SetCameraPos write scan.
SHIM_EXPORT const char* RC_Probe_FindAll(float x, float z)
{
    if (RC_Shim_Init() != 0) return g_status;
    g_foundCount = 0;
    void* roots[3];
    if (RootsScene(roots) == NULL) { sprintf_s(g_status, "scene unresolved"); return g_status; }
    int written = sprintf_s(g_status, "findall(%.1f,%.1f):", x, z);
    int hits = 0;
    for (int r = 0; r < 3 && hits < 8; r++)
    {
        BYTE* o = (BYTE*)roots[r];
        if (o == NULL) continue;
        for (size_t off = 0; off + 8 <= 0x20000 && hits < 8; off += 8)
        {
            size_t span = ReadableSpan(o + off);
            if (span < 8) break;
            void* t = *(void**)(o + off);
            size_t tspan = ReadableSpan(t);
            if (tspan < 0x40) continue;
            size_t limit = tspan < 0x2000 ? tspan : 0x2000;
            for (size_t ot = 0; ot + 12 <= limit && hits < 8; ot += 4)
            {
                float* f = (float*)((BYTE*)t + ot);
                if (f[0] == x && f[2] == z)
                {
                    written += sprintf_s(g_status + written, sizeof(g_status) - written,
                                         " | r%d+0x%zX->%p+0x%zX y=%.2f",
                                         r, off, t, ot, f[1]);
                    hits++;
                    if (g_foundCount < kMaxFound)
                    {
                        g_foundPtr[g_foundCount] = t;
                        g_foundOff[g_foundCount] = (int)ot;
                        strncpy_s(g_foundCls[g_foundCount], "src", _TRUNCATE);
                        g_foundCount++;
                    }
                }
            }
        }
    }
    if (hits == 0)
        sprintf_s(g_status + written, sizeof(g_status) - written, " none");
    return g_status;
}

// Write position (and target at +0x10) of record i found by RC_Probe_FindAll.
// Used between FrameMove and Render to test which record drives the view.
SHIM_EXPORT int RC_CamSetIndex(int i, float x, float y, float z,
                               float tx, float ty, float tz)
{
    if (i < 0 || i >= g_foundCount) return -1;
    BYTE* p = (BYTE*)g_foundPtr[i];
    int off = g_foundOff[i];
    if (!Readable(p + off, 0x20)) return -2;
    float* f = (float*)(p + off);
    f[0] = x; f[1] = y; f[2] = z;
    if (Readable(p + off + 0x10, 0x10))
    {
        float* t = (float*)(p + off + 0x10);
        t[0] = tx; t[1] = ty; t[2] = tz;
    }
    return 0;
}

// Pointer to the engine's managed-camera input object: [engine+0x1F58].
SHIM_EXPORT void* RC_Probe_InputPtr()
{
    if (RC_Shim_Init() != 0) return NULL;
    HMODULE mod = GetModuleHandleA(kEngineName);
    GetEngine2Fn getEngine = (GetEngine2Fn)GetProcAddress(mod, "KG3D_GetEngine2");
    if (getEngine == NULL) return NULL;
    void* engine = getEngine();
    if (engine == NULL || !Readable((BYTE*)engine + 0x1F58, 8)) return NULL;
    void* p = *(void**)((BYTE*)engine + 0x1F58);
    return Readable(p, 8) ? p : NULL;
}

// Byte snapshot/diff for API-driven field discovery (T0.1): snapshot a range,
// call a working engine API, then report the 4-byte offsets that changed.
static BYTE g_snap[0x2000];
static void* g_snapObj = NULL;
static unsigned g_snapLen = 0;

SHIM_EXPORT int RC_Probe_Snap(void* obj, unsigned len)
{
    if (len > sizeof(g_snap)) len = sizeof(g_snap);
    if (!Readable(obj, len)) return -1;
    memcpy(g_snap, obj, len);
    g_snapObj = obj;
    g_snapLen = len;
    return 0;
}

SHIM_EXPORT const char* RC_Probe_SnapDiff()
{
    if (g_snapObj == NULL) return "no snapshot";
    if (!Readable(g_snapObj, g_snapLen)) return "unreadable";
    int written = sprintf_s(g_status, "diff:");
    int n = 0;
    for (unsigned off = 0; off + 4 <= g_snapLen && n < 24; off += 4)
    {
        float a, b;
        memcpy(&a, g_snap + off, 4);
        memcpy(&b, (BYTE*)g_snapObj + off, 4);
        if (memcmp(g_snap + off, (BYTE*)g_snapObj + off, 4) != 0)
        {
            written += sprintf_s(g_status + written, sizeof(g_status) - written,
                                 " +0x%X: %.6g -> %.6g", off, a, b);
            n++;
        }
    }
    if (n == 0)
        sprintf_s(g_status + written, sizeof(g_status) - written, " none");
    return g_status;
}

// ---- CLR scene-proxy route (engine-faithful camera object) ----------------
// The managed KGSceneCLR.SetCameraPos IL works on this.m_pScene (an
// IKG3DSceneProxy*), not on the engine export scene. The client reads that
// field by name and hands the pointer here; all calls below are the exact
// calls the IL makes, SEH-guarded so a bad slot returns instead of AVing.
static bool ExecutableCode(const void* p)
{
    if (p == NULL) return false;
    MEMORY_BASIC_INFORMATION mbi;
    if (VirtualQuery(p, &mbi, sizeof(mbi)) == 0) return false;
    if (mbi.State != MEM_COMMIT) return false;
    DWORD prot = mbi.Protect & 0xFF;
    return prot == PAGE_EXECUTE || prot == PAGE_EXECUTE_READ ||
           prot == PAGE_EXECUTE_READWRITE || prot == PAGE_EXECUTE_WRITECOPY;
}

SHIM_EXPORT const char* RC_ModuleOf(void* p)
{
    if (p == NULL) { sprintf_s(g_status, "(null)"); return g_status; }
    HMODULE h = NULL;
    if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                           GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           (LPCSTR)p, &h) && h != NULL)
    {
        char path[MAX_PATH]; path[0] = 0;
        GetModuleFileNameA(h, path, MAX_PATH);
        const char* base = path;
        for (const char* q = path; *q; q++) if (*q == '\\' || *q == '/') base = q + 1;
        sprintf_s(g_status, "%s+0x%llX", base,
                  (unsigned long long)((const BYTE*)p - (const BYTE*)h));
    }
    else
    {
        sprintf_s(g_status, "unmapped exec=%d", ExecutableCode(p) ? 1 : 0);
    }
    return g_status;
}

SHIM_EXPORT void* RC_Probe_ReadP(void* va)
{
    if (!Readable(va, 8)) return NULL;
    return *(void**)va;
}

// camera = scene->vt[+0x50](scene)   (the managed IL's own getter call)
SHIM_EXPORT void* RC_SceneCamRead(void* scene)
{
    if (scene == NULL || RC_Shim_Init() != 0) return NULL;
    if (!Readable((BYTE*)scene, 8)) return NULL;
    void* vt = *(void**)scene;
    if (!Readable((BYTE*)vt, 0x58)) return NULL;   // vtable is rdata, not code
    void* fn = *(void**)((BYTE*)vt + 0x50);
    if (!ExecutableCode(fn)) return NULL;
    __try { return ((void*(__stdcall*)(void*))fn)(scene); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return NULL; }
}

// cam->vt[+0x48](&pos), cam->vt[+0x60](&tgt)  (read-only)
SHIM_EXPORT int RC_CamGetVt(void* cam, float* outPos, float* outTgt)
{
    if (cam == NULL || outPos == NULL || outTgt == NULL) return -1;
    if (!Readable((BYTE*)cam, 8)) return -2;
    void* vt = *(void**)cam;
    if (!Readable((BYTE*)vt, 0x68)) return -3;   // vtable is rdata
    void* fnPos = *(void**)((BYTE*)vt + 0x48);
    void* fnTgt = *(void**)((BYTE*)vt + 0x60);
    if (!ExecutableCode(fnPos) || !ExecutableCode(fnTgt)) return -4;
    __try
    {
        ((void(__stdcall*)(void*, float*))fnPos)(cam, outPos);
        ((void(__stdcall*)(void*, float*))fnTgt)(cam, outTgt);
        return 0;
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -5; }
}

// cam->vt[+0x50](pos, 0); cam->vt[+0x58](target, 0) - the game mechanism:
// position plus look-at, no managed Y clamp, no orbit.
SHIM_EXPORT int RC_CamSetVt3(void* cam, float x, float y, float z,
                             float tx, float ty, float tz)
{
    if (cam == NULL) return -1;
    if (!Readable((BYTE*)cam, 8)) return -2;
    void* vt = *(void**)cam;
    if (!Readable((BYTE*)vt, 0x60)) return -3;   // vtable is rdata
    void* fnPos = *(void**)((BYTE*)vt + 0x50);
    void* fnTgt = *(void**)((BYTE*)vt + 0x58);
    if (!ExecutableCode(fnPos) || !ExecutableCode(fnTgt)) return -4;
    float p[3] = { x, y, z };
    float t[3] = { tx, ty, tz };
    __try
    {
        ((void(__stdcall*)(void*, float*, int))fnPos)(cam, p, 0);
        ((void(__stdcall*)(void*, float*, int))fnTgt)(cam, t, 0);
        return 0;
    }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -5; }
}

// ---- D6 crash-context diagnostic (RC_D6DBG=1, no behavior change) ---------
// The engine's worker dereferences a null registry entry at +0x11D03B6 (see
// docs/camera/HOST_DEVIATIONS.md D6). This VEH only snapshots the object graph
// (registers, the r12 chain, the registry map and its root node) to a file,
// then lets the crash proceed unchanged - diagnostics, not a patch.
static const DWORD RVA_D6_SITE = 0x11D03B6;
static char  g_d6File[MAX_PATH] = "";
static PVOID g_d6Veh = NULL;

static void D6LogPtr(FILE* f, const char* label, unsigned long long v)
{
    if (v == 0 || v == ~0ULL) { fprintf(f, "%s=0x%llX\n", label, v); return; }
    HMODULE mod = NULL;
    if (GetModuleHandleExA(0x00000004 | 0x00000002, (LPCSTR)v, &mod) && mod != NULL)
    {
        char name[MAX_PATH] = "";
        GetModuleFileNameA(mod, name, MAX_PATH);
        fprintf(f, "%s=0x%llX %s+0x%llX\n", label, v, name,
                (unsigned long long)(v - (unsigned long long)mod));
    }
    else fprintf(f, "%s=0x%llX\n", label, v);
}

static unsigned long long D6Q(unsigned long long a)
{
    if (a == 0 || !Readable((void*)a, 8)) return 0;
    return *(unsigned long long*)a;
}

static LONG CALLBACK D6Veh(EXCEPTION_POINTERS* ep)
{
    if (ep == NULL || ep->ExceptionRecord->ExceptionCode != 0xC0000005)
        return EXCEPTION_CONTINUE_SEARCH;
    if (g_base == NULL)
    {
        HMODULE m = GetModuleHandleA(kEngineName);
        if (m != NULL) g_base = (BYTE*)m;
    }
    if (g_base == NULL || ep->ExceptionRecord->ExceptionAddress != g_base + RVA_D6_SITE)
        return EXCEPTION_CONTINUE_SEARCH;
    CONTEXT* c = ep->ContextRecord;
    FILE* f = NULL;
    fopen_s(&f, g_d6File, "a");
    if (f == NULL) return EXCEPTION_CONTINUE_SEARCH;
    fprintf(f, "== D6 site pid=%lu tid=%lu rip=0x%llX ==\n",
            GetCurrentProcessId(), GetCurrentThreadId(), (unsigned long long)c->Rip);
    const char* names[16] = { "rax","rcx","rdx","rbx","rsp","rbp","rsi","rdi",
                              "r8","r9","r10","r11","r12","r13","r14","r15" };
    unsigned long long vals[16] = { c->Rax,c->Rcx,c->Rdx,c->Rbx,c->Rsp,c->Rbp,c->Rsi,c->Rdi,
                                    c->R8,c->R9,c->R10,c->R11,c->R12,c->R13,c->R14,c->R15 };
    for (int i = 0; i < 16; i++) D6LogPtr(f, names[i], vals[i]);
    unsigned long long p = c->R12;
    for (int i = 0; i < 4 && p != 0; i++)
    {
        p = D6Q(p);
        char lbl[32];
        sprintf_s(lbl, "[r12]^%d", i + 1);
        D6LogPtr(f, lbl, p);
    }
    unsigned long long rbp = c->Rbp;
    unsigned long long m68 = D6Q(rbp - 0x68);
    D6LogPtr(f, "*(rbp-68h)", m68);
    unsigned long long o2 = m68 ? D6Q(m68) : 0;
    D6LogPtr(f, "**(rbp-68h)", o2);
    fprintf(f, "[rbp-70h]=0x%llX [rbp-0B8h]=0x%llX [rbp-0C0h]=0x%llX [rsp+70h]=0x%llX [rsp+78h]=0x%llX\n",
            D6Q(rbp - 0x70), D6Q(rbp - 0xB8), D6Q(rbp - 0xC0),
            D6Q(c->Rsp + 0x70), D6Q(c->Rsp + 0x78));
    if (o2 != 0)
    {
        unsigned long long root = D6Q(o2 + 0x58 + 0x20);
        fprintf(f, "map(o2+58h).root=0x%llX key=0x%llX left=0x%llX right=0x%llX\n",
                root, root ? D6Q(root + 0x20) : 0,
                root ? D6Q(root + 8) : 0, root ? D6Q(root + 0x10) : 0);
    }
    fprintf(f, "== end ==\n");
    fclose(f);
    return EXCEPTION_CONTINUE_SEARCH;
}

// Seed the engine's lazy name-hash for "RCPI_Scene" (RVA 0x2D5BBD0). The
// engine computes it on demand with an FNV-1 loop, but the guard gate
// (`cmp [guard], tls; jg init`) can let a worker thread through before the
// init ran, leaving the hash 0 -> the registry lookup misses -> AV. Writing
// the same value the engine would compute is using its own mechanism.
SHIM_EXPORT int RC_D6Seed()
{
    if (RC_Shim_Init() != 0) return -1;
    if (g_base == NULL) return -2;
    unsigned long long* slot = (unsigned long long*)(g_base + 0x2D5BBD0);
    if (*slot == 0)
        *slot = 0x392E0BFA0428F080ULL;   // FNV-1 of "RCPI_Scene" (engine loop)
    return *slot == 0x392E0BFA0428F080ULL ? 0 : -3;
}

SHIM_EXPORT const char* RC_D6SeedInfo()
{
    sprintf_s(g_status, "seed slot=0x%p value=0x%llX",
              g_base ? g_base + 0x2D5BBD0 : NULL,
              g_base ? *(unsigned long long*)(g_base + 0x2D5BBD0) : 0ULL);
    return g_status;
}

SHIM_EXPORT int RC_D6Dbg()
{
    if (g_d6File[0] == 0)
    {
        DWORD n = GetEnvironmentVariableA("RC_D6DBG_FILE", g_d6File, MAX_PATH);
        if (n == 0)
        {
            char dir[MAX_PATH];
            GetTempPathA(MAX_PATH, dir);
            sprintf_s(g_d6File, "%sd6dbg_%lu.txt", dir, GetCurrentProcessId());
        }
    }
    if (g_d6Veh != NULL) return 0;
    g_d6Veh = AddVectoredExceptionHandler(1, D6Veh);
    return g_d6Veh != NULL ? 0 : 1;
}

// ---- character 3.x: actor handle probe (docs/character/3_1_RIG_SOCKETS.md) --
// The KGSceneCLR.AddDummyModel return value is pointer-like; identify the
// object type before wiring the socket/bone getters (research open question 1).
// Read-only: loads pointers only, every deref SEH-guarded.
static const char* ModulePlus(void* p, char* buf, size_t n)
{
    HMODULE h = NULL;
    if (p != NULL && GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                                        GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                        (LPCSTR)p, &h) && h != NULL)
    {
        char path[MAX_PATH]; path[0] = 0;
        GetModuleFileNameA(h, path, MAX_PATH);
        const char* base = path;
        for (const char* q = path; *q; q++) if (*q == '\\' || *q == '/') base = q + 1;
        sprintf_s(buf, n, "%s+0x%llX", base, (unsigned long long)((const BYTE*)p - (const BYTE*)h));
    }
    else
    {
        sprintf_s(buf, n, "unmapped exec=%d", ExecutableCode(p) ? 1 : 0);
    }
    return buf;
}

// MSVC x64 RTTI: vt[-1] -> CompleteObjectLocator {.., pTypeDescriptor RVA @+0xC};
// TypeDescriptor = {vfptr, spare, char name[] @+0x10} (decorated ".?AV...@@").
static const char* RttiName(void* vt, char* buf, size_t n)
{
    buf[0] = 0;
    if (vt == NULL || !Readable((BYTE*)vt - 8, 8)) return buf;
    void* col = NULL;
    __try { col = *(void**)((BYTE*)vt - 8); } __except (EXCEPTION_EXECUTE_HANDLER) { return buf; }
    if (col == NULL || !Readable((BYTE*)col, 0x14)) return buf;
    DWORD tdRva = 0;
    __try { tdRva = *(DWORD*)((BYTE*)col + 0x0C); } __except (EXCEPTION_EXECUTE_HANDLER) { return buf; }
    HMODULE h = NULL;
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                            (LPCSTR)vt, &h) || h == NULL) return buf;
    BYTE* td = (BYTE*)h + tdRva;
    if (!Readable(td, 0x18)) return buf;
    const char* name = (const char*)(td + 0x10);
    if (Readable(name, 1)) strncpy_s(buf, n, name, _TRUNCATE);
    return buf;
}

SHIM_EXPORT const char* RC_ActorProbe(long long handle)
{
    if (RC_Shim_Init() != 0) return g_status;
    void* h = (void*)(intptr_t)handle;
    char m1[160] = { 0 }, m2[160] = { 0 }, m3[160] = { 0 }, m4[160] = { 0 };
    if (h == NULL || !Readable(h, 8))
    {
        sprintf_s(g_status, "handle=%p readable=0", h);
        return g_status;
    }
    void* vt = NULL, *pm = NULL, *pmvt = NULL, *list = NULL;
    int typeAt2A0 = -1;
    __try { vt = *(void**)h; } __except (EXCEPTION_EXECUTE_HANDLER) { vt = NULL; }
    __try { if (Readable((BYTE*)h + 0x358, 8)) pm = *(void**)((BYTE*)h + 0x358); }
    __except (EXCEPTION_EXECUTE_HANDLER) { pm = NULL; }
    __try { if (pm != NULL && Readable(pm, 8)) pmvt = *(void**)pm; }
    __except (EXCEPTION_EXECUTE_HANDLER) { pmvt = NULL; }
    __try { if (Readable((BYTE*)h + 0x2A0, 4)) typeAt2A0 = *(int*)((BYTE*)h + 0x2A0); }
    __except (EXCEPTION_EXECUTE_HANDLER) { typeAt2A0 = -1; }
    __try { if (Readable((BYTE*)h + 0x7E0, 8)) list = *(void**)((BYTE*)h + 0x7E0); }
    __except (EXCEPTION_EXECUTE_HANDLER) { list = NULL; }
    int w = sprintf_s(g_status, "handle=%p vt=%p [%s]", 
                      h, vt, ModulePlus(vt, m1, sizeof(m1)));
    if (w > 0 && (size_t)w < sizeof(g_status))
    {
        w += sprintf_s(g_status + w, sizeof(g_status) - w, " rtti=%s",
                       RttiName(vt, m4, sizeof(m4)));
        if (w > 0 && (size_t)w < sizeof(g_status))
            w += sprintf_s(g_status + w, sizeof(g_status) - w,
                           " +0x358=%p [%s] type@0x2A0=%d +0x7E0=%p [%s]",
                           pm, pm == NULL ? "null" : ModulePlus(pm, m2, sizeof(m2)),
                           typeAt2A0, list, list == NULL ? "null" : ModulePlus(list, m3, sizeof(m3)));
    }
    return g_status;
}

// ---- KG3DModelProxy actor/bone/socket calls (character 3.x) ----------------
// The AddDummyModel handle is a KG3DModelProxy (RTTI-verified); these call the
// proxy's own methods in KG_EngineEditorX64.dll with SEH guards. Read-only.
static HMODULE ProxyModule()
{
    return GetModuleHandleA("KG_EngineEditorX64.dll");
}

// Find bone (socket=0) or socket (socket=1): out16 = {handle, idx, flags}
SHIM_EXPORT int RC_ProxyFind(void* proxy, const char* name, int socket, unsigned char* out16)
{
    if (proxy == NULL || name == NULL || out16 == NULL) return -1;
    HMODULE m = ProxyModule();
    if (m == NULL) { sprintf_s(g_status, "KG_EngineEditorX64 not loaded"); return -2; }
    typedef int (*Fn)(void*, const char*, void*);
    Fn fn = (Fn)((BYTE*)m + (socket ? 0x4BA80 : 0x49160)); // FindSocket / FindBone
    __try { return fn(proxy, name, out16); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

// kind 0 = GetBoneMatrix, 1 = GetBoneMatrixLocal, 2 = GetSocketMatrix
SHIM_EXPORT int RC_ProxyMatrix(void* proxy, const unsigned char* info16, int kind, float* out16)
{
    if (proxy == NULL || info16 == NULL || out16 == NULL) return -1;
    HMODULE m = ProxyModule();
    if (m == NULL) return -2;
    typedef int (*Fn)(void*, const void*, float*);
    DWORD rva = (kind == 0) ? 0x45760 : ((kind == 1) ? 0x459A0 : 0x45A40);
    Fn fn = (Fn)((BYTE*)m + rva);
    __try { return fn(proxy, info16, out16); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

SHIM_EXPORT const char* RC_ProxyInfo(void* proxy)
{
    if (proxy == NULL) { sprintf_s(g_status, "proxy null"); return g_status; }
    char m1[160] = { 0 }, m3[160] = { 0 };
    void* actor = NULL, *actorVt = NULL;
    __try { if (Readable((BYTE*)proxy + 0x18, 8)) actor = *(void**)((BYTE*)proxy + 0x18); }
    __except (EXCEPTION_EXECUTE_HANDLER) { actor = NULL; }
    __try { if (actor != NULL && Readable(actor, 8)) actorVt = *(void**)actor; }
    __except (EXCEPTION_EXECUTE_HANDLER) { actorVt = NULL; }
    sprintf_s(g_status, "proxy=%p +0x18(actor)=%p [%s] rtti=%s",
              proxy, actor, actor ? ModulePlus(actor, m1, sizeof(m1)) : "null",
              actorVt ? RttiName(actorVt, m3, sizeof(m3)) : "?");
    return g_status;
}

// ---- KG3D_Actor socket/bone methods (docs/character/3_1_RIG_SOCKETS.md) ----
// actor = *(proxy+0x18) (RTTI .?AVKG3D_Actor@@); methods live in the engine
// module the shim already binds (KG3DEngineDX11EX64.dll).
SHIM_EXPORT void* RC_ProxyActor(void* proxy)
{
    void* actor = NULL;
    if (proxy == NULL) return NULL;
    __try { if (Readable((BYTE*)proxy + 0x18, 8)) actor = *(void**)((BYTE*)proxy + 0x18); }
    __except (EXCEPTION_EXECUTE_HANDLER) { actor = NULL; }
    return actor;
}

// out = BindExtraInfo {pActor, int socketIndex @+8, int fromBaseModel @+0xC}
SHIM_EXPORT int RC_ActorFindSocket(void* actor, const char* name, void* out, int flags)
{
    if (actor == NULL || name == NULL || out == NULL) return -1;
    if (RC_Shim_Init() != 0) return -4;
    typedef int (*Fn)(void*, const char*, void*, int);
    Fn fn = (Fn)(g_base + 0x81E5D0);            // KG3D_Actor::FindSocket
    __try { return fn(actor, name, out, flags); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

SHIM_EXPORT int RC_ActorSocketMatrix(void* actor, int idx, float* out16)
{
    if (actor == NULL || out16 == NULL) return -1;
    if (RC_Shim_Init() != 0) return -4;
    typedef int (*Fn)(void*, int, float*);
    Fn fn = (Fn)(g_base + 0x8204D0);            // KG3D_Actor::GetSocketMatrixLocal
    __try { return fn(actor, idx, out16); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

SHIM_EXPORT int RC_ActorBoneMatrix(void* actor, int idx, float* out16)
{
    if (actor == NULL || out16 == NULL) return -1;
    if (RC_Shim_Init() != 0) return -4;
    typedef int (*Fn)(void*, int, float*);
    Fn fn = (Fn)(g_base + 0x81F090);            // KG3D_Actor::GetBoneMatrixLocal
    __try { return fn(actor, idx, out16); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

// name -> KG3D standard hash (KGCommonX64.dll export, used by FindBones)
SHIM_EXPORT int RC_HashName(const char* name, unsigned long long* out64)
{
    if (name == NULL || out64 == NULL) return -1;
    HMODULE m = GetModuleHandleA("KGCommonX64.dll");
    if (m == NULL) return -2;
    typedef unsigned long long (*Fn)(const char*);
    Fn fn = (Fn)GetProcAddress(m, "KG3D_ConvertToStandardHashString");
    if (fn == NULL) return -3;
    __try { *out64 = fn(name); return 0; }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -4; }
}

// resolve a bone name-hash on the actor's current model: the internal helper
// KG3D_Actor::FindBones calls at 0x81E498; out16 = {?, int boneIdx @+8}
SHIM_EXPORT int RC_ActorFindBoneHash(void* actor, unsigned long long hash, void* out16)
{
    if (actor == NULL || out16 == NULL) return -1;
    if (RC_Shim_Init() != 0) return -4;
    void* model = NULL;
    __try { if (Readable((BYTE*)actor + 0x358, 8)) model = *(void**)((BYTE*)actor + 0x358); }
    __except (EXCEPTION_EXECUTE_HANDLER) { model = NULL; }
    if (model == NULL) return -5;
    typedef int (*Fn)(void*, unsigned long long, void*);
    Fn fn = (Fn)(g_base + 0x82F0A0);
    __try { return fn(actor, hash, out16); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

// ---- face apply exports (for the 3x-face workstream) -----------------------
// KG3DModelProxy::LoadMetaFaceDefinitionJson (fn 0x46FB0) forwards the JSON
// string to the actor's model; SetFaceLiftParams (fn 0x47710) forwards three
// args to the actor's vt[+0x538] (signature not decoded - passthrough).
SHIM_EXPORT int RC_ModelLoadMetaFaceJson(void* proxy, const char* jsonUtf8)
{
    if (proxy == NULL || jsonUtf8 == NULL) return -1;
    HMODULE m = ProxyModule();
    if (m == NULL) return -2;
    typedef int (*Fn)(void*, const char*);
    Fn fn = (Fn)((BYTE*)m + 0x46FB0);
    __try { return fn(proxy, jsonUtf8); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

SHIM_EXPORT int RC_ProxySetFaceLiftParams(void* proxy, void* a1, void* a2, void* a3)
{
    if (proxy == NULL) return -1;
    HMODULE m = ProxyModule();
    if (m == NULL) return -2;
    typedef int (*Fn)(void*, void*, void*, void*);
    Fn fn = (Fn)((BYTE*)m + 0x47710);
    __try { return fn(proxy, a1, a2, a3); }
    __except (EXCEPTION_EXECUTE_HANDLER) { return -3; }
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        char v[8];
        if (GetEnvironmentVariableA("RC_D6DBG", v, sizeof(v)) > 0 && v[0] == '1')
            RC_D6Dbg();
        // default on (RC_D6SEED=0 opts out): seed the engine's lazy name hash
        // the guard left at 0, which is the confirmed D6 null-lookup cause
        v[0] = 0;
        DWORD n = GetEnvironmentVariableA("RC_D6SEED", v, sizeof(v));
        if (n == 0 || v[0] != '0')
            RC_D6Seed();
    }
    if (reason == DLL_PROCESS_DETACH)
    {
        g_engine = NULL;
        g_base = NULL;
    }
    return TRUE;
}
