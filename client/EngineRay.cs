// Native scene rays through the host engine - the same backends the game's
// camera obstruction uses (game mask 0x301 includes terrain + render entities).
// The scene-level KG3D_Scene::RayIntersection* functions reject external calls
// via an internal guard, so this shim calls the concrete backends the engine
// itself forwards to (found in the engine's own RayIntersection bodies):
//   terrain:       terrain = [scene+0x950]; terrain->vt[+0xB8](pos, dir, maxDist, out)
//   entities:      this = [[scene+0x910]+8]; fn 0x180A5E4C0(pos, dir, maxDist, out)
// The engine/window/scene come from the exports KG3D_GetEngine2 ->
// ?GetActiveWindow2@KG3D_Engine@@... -> ?Get3DScene2@KG3D_Window@@...
using System;
using System.Runtime.InteropServices;

internal sealed class EngineRay
{
    const int RVA_RAY_TERRAIN = 0x976260;   // KG3D_Scene::RayIntersectionTerrain
    const int RVA_RAY_SCENE = 0x975EE0;     // KG3D_Scene::RayIntersection
    const int RVA_SPACE_RAY = 0xA5E4C0;     // space-manager ray (0x1809760EC)
    const int RVA_ENTITY_RAY = 0x53CB80;    // scene-node backend the scene ray
                                            // calls at 0x180976144

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    // KG3D_GetEngine2 returns g_pEngine directly
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr GetEngine2Fn();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr EngineMethodFn(IntPtr self);

    // scene-level ray (guarded; kept as a fallback)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int RayTerrainFn(IntPtr scene, float[] pos, float[] dir, float maxDist,
                              IntPtr unused, out float retDist, out int retIntersect);

    // terrain object / space-manager ray: fn(this, pos, dir, maxDist, float* out)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int TerrainVtFn(IntPtr terrain, float[] pos, float[] dir, float maxDist, out float outDist);

    Action<string> _log;
    IntPtr _module = IntPtr.Zero;
    IntPtr _getWindow = IntPtr.Zero, _getScene = IntPtr.Zero;
    IntPtr _scene = IntPtr.Zero;
    TerrainVtFn _terrainVt;
    RayTerrainFn _sceneRay;
    TerrainVtFn _spaceRay, _entityRay;
    IntPtr _terrainObj = IntPtr.Zero, _spaceObj = IntPtr.Zero, _entityObj = IntPtr.Zero;
    long _nextTry = 0;

    public int LastHr, LastHit;
    public float LastDist;

    public bool Available { get { return _terrainVt != null && _spaceRay != null; } }

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

            long t1 = 0, t2 = 0;
            _terrainObj = Marshal.ReadIntPtr(new IntPtr(_scene.ToInt64() + 0x950));
            t1 = _terrainObj.ToInt64();
            t2 = Marshal.ReadIntPtr(new IntPtr(_scene.ToInt64() + 0xA28)).ToInt64();
            if (_terrainObj != IntPtr.Zero)
            {
                IntPtr vt = Marshal.ReadIntPtr(_terrainObj);
                IntPtr fn = Marshal.ReadIntPtr(new IntPtr(vt.ToInt64() + 0xB8));
                _terrainVt = (TerrainVtFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(TerrainVtFn));
            }
            IntPtr spaceMgr = Marshal.ReadIntPtr(new IntPtr(_scene.ToInt64() + 0x910));
            if (spaceMgr != IntPtr.Zero)
            {
                _spaceObj = Marshal.ReadIntPtr(new IntPtr(spaceMgr.ToInt64() + 8));
                _spaceRay = (TerrainVtFn)Marshal.GetDelegateForFunctionPointer(
                    new IntPtr(_module.ToInt64() + RVA_SPACE_RAY), typeof(TerrainVtFn));
            }
            // third backend: [scene+0xAD8] -> +0x5F8 -> value (0x180976105)
            IntPtr holder = Marshal.ReadIntPtr(new IntPtr(_scene.ToInt64() + 0xAD8));
            int entCount = 0;
            if (holder != IntPtr.Zero)
            {
                entCount = Marshal.ReadInt32(new IntPtr(holder.ToInt64() + 0x600));
                if (entCount > 0)
                {
                    IntPtr list = Marshal.ReadIntPtr(new IntPtr(holder.ToInt64() + 0x5F8));
                    if (list != IntPtr.Zero) _entityObj = Marshal.ReadIntPtr(list);
                    if (_entityObj != IntPtr.Zero)
                        _entityRay = (TerrainVtFn)Marshal.GetDelegateForFunctionPointer(
                            new IntPtr(_module.ToInt64() + RVA_ENTITY_RAY), typeof(TerrainVtFn));
                }
            }
            _log(string.Format("EngineRay: ready scene=0x{0:X} terrain+950=0x{1:X} terrain+A28=0x{2:X} vtB8={3} space=0x{4:X} spaceRay={5} entity=0x{6:X} entityRay={7} entCount={8}",
                _scene.ToInt64(), t1, t2, _terrainVt != null, _spaceObj.ToInt64(), _spaceRay != null,
                _entityObj.ToInt64(), _entityRay != null, entCount));
            return true;
        }
        catch (Exception e)
        {
            _log("EngineRay lazy ex: " + e.Message);
            return false;
        }
    }

    float CastVt(TerrainVtFn fn, IntPtr obj, float ax, float ay, float az, float bx, float by, float bz)
    {
        if (fn == null || obj == IntPtr.Zero) return -1f;
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-3f) return -1f;
        var pos = new float[] { ax, ay, az };
        var dir = new float[] { dx / len, dy / len, dz / len };
        float dist;
        try
        {
            LastHr = fn(obj, pos, dir, len, out dist);
            LastDist = dist;
            LastHit = LastHr != 0 ? 1 : 0;
            if (LastHr == 0) return -1f;
            if (dist < 0f || dist > len + 1f) return -1f;
            return dist;
        }
        catch { return -1f; }
    }

    float CastScene(RayTerrainFn fn, float ax, float ay, float az, float bx, float by, float bz)
    {
        if (fn == null || _scene == IntPtr.Zero) return -1f;
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
        catch { return -1f; }
    }

    // Nearest terrain hit of the segment A->B; -1 when nothing is hit.
    public float RayTerrain(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady() || _terrainVt == null) return -1f;
        return CastVt(_terrainVt, _terrainObj, ax, ay, az, bx, by, bz);
    }

    // The scene-level KG3D_Scene::RayIntersection (guarded). Used to test
    // whether the guard passes when called inside the engine frame.
    public float RaySceneLevel(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady()) return -1f;
        return CastScene(_sceneRay, ax, ay, az, bx, by, bz);
    }

    // Entity/scene-node hit. The scene-level ray is guarded, so call the space
    // manager the scene forwards to; fall back to the scene ray if absent.
    public float RayScene(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady()) return -1f;
        float best = -1f;
        if (_spaceRay != null && _spaceObj != IntPtr.Zero)
            best = CastVt(_spaceRay, _spaceObj, ax, ay, az, bx, by, bz);
        if (_entityRay != null && _entityObj != IntPtr.Zero)
        {
            float d = CastVt(_entityRay, _entityObj, ax, ay, az, bx, by, bz);
            if (d > 0f && (best < 0f || d < best)) best = d;
        }
        if (best < 0f && _sceneRay != null)
            best = CastScene(_sceneRay, ax, ay, az, bx, by, bz);
        return best;
    }
}
