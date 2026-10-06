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
    const int RVA_VERTICAL_RAY = 0xA5E1D0;  // KG3D_SpaceManager::RayIntersectionVertical
                                            // (this, posXZ, range, float* pRetHeight)

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32.dll")]
    static extern IntPtr TlsGetValue(int dwTlsIndex);

    [DllImport("ntdll.dll")]
    static extern int NtQueryInformationThread(IntPtr threadHandle, int infoClass,
                                               IntPtr buffer, int bufferLength, out int returnLength);

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

    // vertical backend: fn(this, float[2] posXZ, float range, float* pRetHeight)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int VertFn(IntPtr self, float[] posXZ, float range, out float height);

    // engine camera object contract (recovered from MovieEngineCLR KGSceneCLR
    // SetCameraPos IL 2026-09-27): scene->vt[+0x50]() returns the camera
    // object; cam->vt[+0x48](float[3]) reads its position; cam->vt[+0x50]
    // (float[3],0) sets position; cam->vt[+0x58](float[3],0) sets the
    // look-at target; cam->vt[+0x60](float[3]) reads the target. The managed
    // API translates the target by the position delta (keeps the old view);
    // calling the target setter is the game's look-at path.
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr GetCamFn(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void SetVecFn(IntPtr self, float[] v, int flag);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void GetVecFn(IntPtr self, float[] v);

    Action<string> _log;
    IntPtr _module = IntPtr.Zero;
    IntPtr _getWindow = IntPtr.Zero, _getScene = IntPtr.Zero;
    IntPtr _scene = IntPtr.Zero;
    IntPtr _camObj = IntPtr.Zero;
    long _modBase, _modEnd;   // engine module range, guards the vtable calls
    TerrainVtFn _terrainVt;
    RayTerrainFn _sceneRay;
    TerrainVtFn _spaceRay, _entityRay;
    VertFn _verticalRay;
    IntPtr _terrainObj = IntPtr.Zero, _spaceObj = IntPtr.Zero, _entityObj = IntPtr.Zero;
    // backend guards: cmp [global], tls+0x89B0; jg skip. A per-thread cell we
    // can raise by hand so the guarded scene/vertical rays run.
    IntPtr _guardCell = IntPtr.Zero;
    IntPtr _guardA = IntPtr.Zero, _guardB = IntPtr.Zero, _guardC = IntPtr.Zero;
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
            try
            {
                _modBase = _module.ToInt64();
                int lfanew = Marshal.ReadInt32(new IntPtr(_module.ToInt64() + 0x3C));
                int sizeOfImage = Marshal.ReadInt32(new IntPtr(_module.ToInt64() + lfanew + 0x50));
                _modEnd = _modBase + sizeOfImage;
            }
            catch { _modBase = 0; _modEnd = 0; }

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
            if (_spaceObj != IntPtr.Zero)
                _verticalRay = (VertFn)Marshal.GetDelegateForFunctionPointer(
                    new IntPtr(_module.ToInt64() + RVA_VERTICAL_RAY), typeof(VertFn));
            }
            // guard cell for the scene-level ray (TLS index 0x1826546C4,
            // cell +0x89B0; globals 0x182D58298 / 0x182D5829C). Read the TLS
            // array from the TEB (gs:[0x58]) so no TLS API is needed.
            try
            {
                _guardA = new IntPtr(_module.ToInt64() + 0x2D58298);
                _guardB = new IntPtr(_module.ToInt64() + 0x2D5829C);
                _guardC = new IntPtr(_module.ToInt64() + 0x2D58B2C);
                IntPtr teb = IntPtr.Zero;
                IntPtr buf = Marshal.AllocHGlobal(0x30);
                try
                {
                    int ret;
                    if (NtQueryInformationThread(new IntPtr(-2), 0, buf, 0x30, out ret) == 0)
                        teb = Marshal.ReadIntPtr(buf, 8);   // TebBaseAddress
                }
                finally { Marshal.FreeHGlobal(buf); }
                if (teb != IntPtr.Zero)
                {
                    int tlsIndex = Marshal.ReadInt32(new IntPtr(_module.ToInt64() + 0x26546C4));
                    IntPtr tlsArray = Marshal.ReadIntPtr(new IntPtr(teb.ToInt64() + 0x58));
                    IntPtr slot = Marshal.ReadIntPtr(new IntPtr(tlsArray.ToInt64() + tlsIndex * 8));
                    if (slot != IntPtr.Zero)
                        _guardCell = new IntPtr(slot.ToInt64() + 0x89B0);
                }
            }
            catch { }
            _log(string.Format("EngineRay: ready scene=0x{0:X} terrain+950=0x{1:X} terrain+A28=0x{2:X} vtB8={3} space=0x{4:X} spaceRay={5} entity=0x{6:X} entityRay={7} entCount={8} guardCell=0x{9:X}",
                _scene.ToInt64(), t1, t2, _terrainVt != null, _spaceObj.ToInt64(), _spaceRay != null,
                _entityObj.ToInt64(), _entityRay != null, entCount, _guardCell.ToInt64()));
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

    // Raise the per-thread guard cell so the engine's own guard passes
    // (pass condition: [global] <= tls+0x89B0).
    void SatisfyGuard()
    {
        if (_guardCell == IntPtr.Zero) return;
        try
        {
            int a = Marshal.ReadInt32(_guardA);
            int b = Marshal.ReadInt32(_guardB);
            int c = Marshal.ReadInt32(_guardC);
            int need = a > b ? a : b;
            if (c > need) need = c;
            int cur = Marshal.ReadInt32(_guardCell);
            if (need > cur) Marshal.WriteInt32(_guardCell, need);
        }
        catch { }
    }

    // The scene-level KG3D_Scene::RayIntersection (guarded). Used to test
    // whether the guard passes when called inside the engine frame.
    public float RaySceneLevel(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady()) return -1f;
        return CastScene(_sceneRay, ax, ay, az, bx, by, bz);
    }

    // Vertical backend: height of the first geometry hit on a vertical line at
    // (x, z) within range; -1 when nothing is hit. This is the game mask's
    // vertical probe (cliffs, terrain-baked walls).
    public float RayVerticalHeight(float x, float y, float z, float range, out int hr)
    {
        hr = -1;
        if (!EnsureReady() || _verticalRay == null || _spaceObj == IntPtr.Zero) return -1f;
        SatisfyGuard();
        var p = new float[] { x, y, z };
        float h;
        try
        {
            hr = _verticalRay(_spaceObj, p, range, out h);
            if (hr == 0) return -1f;
            return h;
        }
        catch { hr = -1; return -1f; }
    }

    // Engine camera object (recovered from MovieEngineCLR KGSceneCLR IL):
    // scene->vt[+0x50]() -> camera; cam->vt[+0x50](pos,0) sets position;
    // cam->vt[+0x58](target,0) sets the look-at target. The vtable pointers
    // are checked to lie inside the engine module before any call.
    bool InModule(IntPtr p)
    {
        long v = p.ToInt64();
        return _modBase != 0 && v >= _modBase && v < _modEnd;
    }

    // The scene object KGSceneCLR actually drives is its private m_pScene
    // pointer, not necessarily the Get3DScene2 result. Bind it from the CLR
    // object so the vtable slots are the same ones the managed camera API uses.
    public void BindSceneObject(object sceneClr)
    {
        try
        {
            Type t = sceneClr.GetType();
            var f = t.GetField("m_pScene",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) { _log("EngineCam: m_pScene field not found"); return; }
            var h = System.Runtime.InteropServices.GCHandle.Alloc(
                sceneClr, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                long fieldOff = Marshal.OffsetOf(t, "m_pScene").ToInt64();
                IntPtr p = Marshal.ReadIntPtr(
                    new IntPtr(h.AddrOfPinnedObject().ToInt64() + fieldOff));
                if (p != IntPtr.Zero)
                {
                    _scene = p;
                    _camObj = IntPtr.Zero;
                    _log(string.Format("EngineCam: bound m_pScene=0x{0:X}", p.ToInt64()));
                }
                else _log("EngineCam: m_pScene is null");
            }
            finally { h.Free(); }
        }
        catch (Exception e) { _log("EngineCam bind ex: " + e.Message); }
    }

    IntPtr GetEngineCamera()
    {
        if (!EnsureReady() || _scene == IntPtr.Zero) return IntPtr.Zero;
        if (_camObj != IntPtr.Zero) return _camObj;
        try
        {
            IntPtr vt = Marshal.ReadIntPtr(_scene);
            IntPtr fn = Marshal.ReadIntPtr(new IntPtr(vt.ToInt64() + 0x50));
            if (!InModule(fn)) { _log("EngineCam: scene vt+0x50 outside module"); return IntPtr.Zero; }
            var getCam = (GetCamFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(GetCamFn));
            IntPtr cam = getCam(_scene);
            if (cam == IntPtr.Zero) return IntPtr.Zero;
            _camObj = cam;
            _log(string.Format("EngineCam: scene=0x{0:X} cam=0x{1:X}", _scene.ToInt64(), cam.ToInt64()));
            return cam;
        }
        catch (Exception e) { _log("EngineCam ex: " + e.Message); return IntPtr.Zero; }
    }

    // Sets the engine camera position and look-at target directly (the game's
    // own two calls; no Y clamp, no target translation).
    bool _engineSetLogged = false;
    int _engineFailLog = 0;

    public bool SetCameraEngine(float px, float py, float pz, float tx, float ty, float tz)
    {
        if (!_engineSetLogged)
        {
            _engineSetLogged = true;
            _log(string.Format("EngineCam: call scene={0} ready={1}", _scene.ToInt64(),
                _terrainVt != null));
        }
        IntPtr cam = GetEngineCamera();
        if (cam == IntPtr.Zero)
        {
            if (_engineFailLog < 3) { _engineFailLog++; _log("EngineCam: no camera object"); }
            return false;
        }
        try
        {
            IntPtr camVt = Marshal.ReadIntPtr(cam);
            IntPtr setPos = Marshal.ReadIntPtr(new IntPtr(camVt.ToInt64() + 0x50));
            IntPtr setTgt = Marshal.ReadIntPtr(new IntPtr(camVt.ToInt64() + 0x58));
            if (!InModule(setPos) || !InModule(setTgt))
            {
                _log(string.Format("EngineCam: setter outside module pos={0} tgt={1}",
                    InModule(setPos), InModule(setTgt)));
                return false;
            }
            var fPos = (SetVecFn)Marshal.GetDelegateForFunctionPointer(setPos, typeof(SetVecFn));
            var fTgt = (SetVecFn)Marshal.GetDelegateForFunctionPointer(setTgt, typeof(SetVecFn));
            float[] p = { px, py, pz };
            float[] t = { tx, ty, tz };
            fPos(cam, p, 0);
            fTgt(cam, t, 0);
            return true;
        }
        catch (Exception e) { _log("EngineCam set ex: " + e.Message); return false; }
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
        // full scene query (all backends + filters) once the guard is raised
        SatisfyGuard();
        float gl = CastScene(_sceneRay, ax, ay, az, bx, by, bz);
        if (gl > 0f && (best < 0f || gl < best)) best = gl;
        return best;
    }
}
