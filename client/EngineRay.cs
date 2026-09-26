// Native scene ray through the host engine - the same backend the game's
// camera obstruction uses (KG3D_Scene::RayIntersectionTerrain, game mask
// 0x301 includes terrain). The client loads KG3DEngineDX11EX64.dll; the
// engine/window/scene are reached through its exports:
//   KG3D_GetEngine() -> ?GetActiveWindow2@KG3D_Engine@@... -> ?Get3DScene2@KG3D_Window@@...
// RayIntersectionTerrain contract (proof/netcode/disasm/host_ray_terrain_fn.txt):
//   rcx=scene, rdx=float3 pos, r8=float3 dir, xmm3=maxDist,
//   r9 unused, stack: float* retDist, int* retIntersect
using System;
using System.Runtime.InteropServices;

internal sealed class EngineRay
{
    const int RVA_RAY_TERRAIN = 0x976260;   // 0x180976260 - image base
    const int RVA_RAY_SCENE = 0x975EE0;     // 0x180975EE0 - image base

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    // KG3D_GetEngine2 simply returns g_pEngine (no arguments)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr GetEngine2Fn();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr EngineMethodFn(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int RayTerrainFn(IntPtr scene, float[] pos, float[] dir, float maxDist,
                              IntPtr unused, out float retDist, out int retIntersect);

    Action<string> _log;
    IntPtr _module = IntPtr.Zero;
    IntPtr _getWindow = IntPtr.Zero, _getScene = IntPtr.Zero;
    IntPtr _scene = IntPtr.Zero;
    RayTerrainFn _terrain, _sceneRay;
    long _nextTry = 0;

    public int LastHr, LastHit;
    public float LastDist;

    public bool Available { get { return _scene != IntPtr.Zero && _terrain != null; } }

    public EngineRay(Action<string> log)
    {
        _log = log;
        try
        {
            _module = GetModuleHandleA("KG3DEngineDX11EX64.dll");
            if (_module == IntPtr.Zero)
            {
                log("EngineRay: host engine module not loaded");
                return;
            }
            IntPtr getEngine = GetProcAddress(_module, "KG3D_GetEngine2");
            _getWindow = GetProcAddress(_module, "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
            _getScene = GetProcAddress(_module, "?Get3DScene2@KG3D_Window@@UEAAPEAVKG3D_Scene@@XZ");
            if (getEngine == IntPtr.Zero || _getWindow == IntPtr.Zero || _getScene == IntPtr.Zero)
            {
                log(string.Format("EngineRay: export missing engine={0} window={1} scene={2}",
                    getEngine != IntPtr.Zero, _getWindow != IntPtr.Zero, _getScene != IntPtr.Zero));
                return;
            }
            _terrain = (RayTerrainFn)Marshal.GetDelegateForFunctionPointer(
                new IntPtr(_module.ToInt64() + RVA_RAY_TERRAIN), typeof(RayTerrainFn));
            _sceneRay = (RayTerrainFn)Marshal.GetDelegateForFunctionPointer(
                new IntPtr(_module.ToInt64() + RVA_RAY_SCENE), typeof(RayTerrainFn));
            log("EngineRay: exports ok, waiting for engine/scene");
        }
        catch (Exception e)
        {
            log("EngineRay ex: " + e.Message);
        }
    }

    // The engine/window/scene only exist after the host initialises them, so
    // resolve them lazily (retry at most once per second).
    bool EnsureReady()
    {
        if (Available) return true;
        long now = Environment.TickCount;
        if (now < _nextTry) return false;
        _nextTry = now + 1000;
        try
        {
            IntPtr getEngine = GetProcAddress(_module, "KG3D_GetEngine2");
            var engineFn = (GetEngine2Fn)Marshal.GetDelegateForFunctionPointer(getEngine, typeof(GetEngine2Fn));
            IntPtr engine = engineFn();
            if (engine == IntPtr.Zero) return false;
            var winFn = (EngineMethodFn)Marshal.GetDelegateForFunctionPointer(_getWindow, typeof(EngineMethodFn));
            IntPtr window = winFn(engine);
            if (window == IntPtr.Zero) return false;
            var sceneFn = (EngineMethodFn)Marshal.GetDelegateForFunctionPointer(_getScene, typeof(EngineMethodFn));
            _scene = sceneFn(window);
            if (_scene == IntPtr.Zero) return false;
            _log("EngineRay: ready scene=0x" + _scene.ToString("X"));
            return true;
        }
        catch (Exception e)
        {
            _log("EngineRay lazy ex: " + e.Message);
            return false;
        }
    }

    float Cast(RayTerrainFn fn, float ax, float ay, float az, float bx, float by, float bz)
    {
        if (fn == null) return -1f;
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-3f) return -1f;
        var pos = new float[] { ax, ay, az };
        var dir = new float[] { dx / len, dy / len, dz / len };
        float dist;
        int hit;
        try
        {
            LastHr = fn(_scene, pos, dir, len, IntPtr.Zero, out dist, out hit);
            LastDist = dist;
            LastHit = hit;
            if (LastHr != 0 || hit == 0) return -1f;
            if (dist < 0f || dist > len + 1f) return -1f;
            return dist;
        }
        catch
        {
            return -1f;
        }
    }

    // Nearest terrain hit of the segment A->B; -1 when nothing is hit.
    public float RayTerrain(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady()) return -1f;
        return Cast(_terrain, ax, ay, az, bx, by, bz);
    }

    // General scene ray (KG3D_Scene::RayIntersection - entities/scene nodes).
    public float RayScene(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady()) return -1f;
        return Cast(_sceneRay, ax, ay, az, bx, by, bz);
    }
}
