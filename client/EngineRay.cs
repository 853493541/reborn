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
    const int RVA_SPACE_RAY = 0xA5E4C0;     // 0x180A5E4C0 - the space manager
                                            // ray the scene's RayIntersection
                                            // calls (0x1809760EC)

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    // KG3D_GetEngine2 simply returns g_pEngine (no arguments)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr GetEngine2Fn();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr EngineMethodFn(IntPtr self);

    // the engine's view-manager singleton getter (0x1801433E0) takes the
    // address of a global (0x18086ECC0) and returns the object whose
    // vt+0xA8 returns fov/aspect/near/far (see 0x180379CD0)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr MgrGetterFn(IntPtr globalAddr);

    // projection getter (vt+0xA8): out fov, aspect, znear, zfar
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int ProjFn(IntPtr view, out float fov, out float aspect, out float znear, out float zfar);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int RayTerrainFn(IntPtr scene, float[] pos, float[] dir, float maxDist,
                              IntPtr unused, out float retDist, out int retIntersect);

    // The engine's terrain ray ultimately calls the terrain object itself:
    //   terrain->vt[+0xB8](pos, dir, maxDist, float* outDist)  (0x1809763C6)
    // Calling it directly is exactly what the game camera's terrain backend
    // does and skips the scene-level guard that rejects external calls.
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int TerrainVtFn(IntPtr terrain, float[] pos, float[] dir, float maxDist, out float outDist);

    Action<string> _log;
    IntPtr _module = IntPtr.Zero;
    IntPtr _getWindow = IntPtr.Zero, _getScene = IntPtr.Zero;
    IntPtr _scene = IntPtr.Zero;
    RayTerrainFn _terrain, _sceneRay;
    TerrainVtFn _terrainVt, _spaceRay;
    IntPtr _terrainObj = IntPtr.Zero, _spaceObj = IntPtr.Zero;
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
            // the terrain object paths inside the scene, per the game-engine
            // disasm; if both are null the active window's scene is not the
            // map scene (or this build stores the terrain elsewhere)
            long t1 = 0, t2 = 0;
            try
            {
                _terrainObj = Marshal.ReadIntPtr(new IntPtr(_scene.ToInt64() + 0x950));
                t1 = _terrainObj.ToInt64();
                t2 = Marshal.ReadIntPtr(new IntPtr(_scene.ToInt64() + 0xA28)).ToInt64();
                if (_terrainObj != IntPtr.Zero)
                {
                    IntPtr vt = Marshal.ReadIntPtr(_terrainObj);
                    IntPtr fn = Marshal.ReadIntPtr(new IntPtr(vt.ToInt64() + 0xB8));
                    _terrainVt = (TerrainVtFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(TerrainVtFn));
                }
                // entity/scene-node ray: [scene+0x910] -> +8 is the space
                // object the scene ray forwards to (0x1809760B7..0xE8)
                IntPtr spaceMgr = Marshal.ReadIntPtr(new IntPtr(_scene.ToInt64() + 0x910));
                if (spaceMgr != IntPtr.Zero)
                {
                    _spaceObj = Marshal.ReadIntPtr(new IntPtr(spaceMgr.ToInt64() + 8));
                    _spaceRay = (TerrainVtFn)Marshal.GetDelegateForFunctionPointer(
                        new IntPtr(_module.ToInt64() + RVA_SPACE_RAY), typeof(TerrainVtFn));
                }
            }
            catch { }
            _log(string.Format("EngineRay: ready scene=0x{0:X} terrain+950=0x{1:X} terrain+A28=0x{2:X} vtB8={3} space=0x{4:X} spaceRay={5}",
                _scene.ToInt64(), t1, t2, _terrainVt != null, _spaceObj.ToInt64(), _spaceRay != null));
            // NOTE: the view-manager singleton path (0x1801433E0(&global
            // 0x18086ECC0) -> vt+0xA8) blocks when called from the client
            // thread (the client froze right after this call), so the
            // near-plane read is left to a future pass from an engine-context
            // callback. Do not re-add it here without a timeout/thread guard.
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

    // Near-plane probe on a worker thread with a timeout: the view-manager
    // getter blocked the client thread, so it must never run inline.
    public void ProbeNearPlane(int timeoutMs, Action<string> log)
    {
        float fov = 0, aspect = 0, zn = 0, zf = 0;
        int hr = -1;
        IntPtr mgr = IntPtr.Zero;
        var th = new System.Threading.Thread(delegate()
        {
            try
            {
                var mgrFn = (MgrGetterFn)Marshal.GetDelegateForFunctionPointer(
                    new IntPtr(_module.ToInt64() + 0x1433E0), typeof(MgrGetterFn));
                mgr = mgrFn(new IntPtr(_module.ToInt64() + 0x86ECC0));
                if (mgr != IntPtr.Zero)
                {
                    IntPtr vt = Marshal.ReadIntPtr(mgr);
                    IntPtr proj = Marshal.ReadIntPtr(new IntPtr(vt.ToInt64() + 0xA8));
                    var projFn = (ProjFn)Marshal.GetDelegateForFunctionPointer(proj, typeof(ProjFn));
                    hr = projFn(mgr, out fov, out aspect, out zn, out zf);
                }
            }
            catch { }
        });
        th.IsBackground = true;
        th.Start();
        if (!th.Join(timeoutMs))
        {
            log("EngineRay near probe: TIMEOUT (view-manager getter needs engine context)");
            return;
        }
        log(string.Format("EngineRay near probe: mgr=0x{0:X} hr={1} fov={2:F3} aspect={3:F3} near={4:F3} far={5:F0}",
            mgr.ToInt64(), hr, fov, aspect, zn, zf));
    }

    // Nearest terrain hit of the segment A->B; -1 when nothing is hit.
    public float RayTerrain(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady() || _terrainVt == null) return -1f;
        return Cast(_terrainVt, ax, ay, az, bx, by, bz);
    }

    float Cast(TerrainVtFn fn, float ax, float ay, float az, float bx, float by, float bz)
    {
        if (fn == null || _terrainObj == IntPtr.Zero) return -1f;
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-3f) return -1f;
        var pos = new float[] { ax, ay, az };
        var dir = new float[] { dx / len, dy / len, dz / len };
        float dist;
        try
        {
            LastHr = fn(_terrainObj, pos, dir, len, out dist);
            LastDist = dist;
            LastHit = LastHr != 0 ? 1 : 0;
            if (LastHr == 0) return -1f;
            if (dist < 0f || dist > len + 1f) return -1f;
            return dist;
        }
        catch
        {
            return -1f;
        }
    }

    // General scene ray (entities/scene nodes). The scene-level
    // RayIntersection is guarded, so call the space manager it forwards to
    // (0x180A5E4C0) directly - the same call the engine makes.
    public float RayScene(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady()) return -1f;
        if (_spaceRay == null || _spaceObj == IntPtr.Zero)
            return Cast(_sceneRay, ax, ay, az, bx, by, bz);
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-3f) return -1f;
        var pos = new float[] { ax, ay, az };
        var dir = new float[] { dx / len, dy / len, dz / len };
        float dist;
        try
        {
            int hr = _spaceRay(_spaceObj, pos, dir, len, out dist);
            LastHr = hr;
            LastDist = dist;
            LastHit = hr != 0 ? 1 : 0;
            if (hr == 0) return -1f;
            if (dist < 0f || dist > len + 1f) return -1f;
            return dist;
        }
        catch
        {
            return -1f;
        }
    }
}
