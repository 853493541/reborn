// P5 probe (env RC_PHYS_PROBE=1): drive the game's own physics stack inside
// the running client host - the engine FS is up here (TerrainSampler already
// creates the physics terrain data loader), so the terrain + static scene
// managers can be created for real. Sequence from the legacy engine-host probe
// (commit 2997649^, docs/movement/REAL_CLIENT_MAP_COLLISION.md):
//   GetPhysicsManager -> CreatePhysXTerrain -> LoadTerrain(vt5) ->
//   UpdateTerrain(vt2) x N -> CreatePhysicsSceneDynamicLoader(vt15).
using System;
using System.Runtime.InteropServices;
using System.Threading;

internal static class PhysicsProbe
{
    const int RVA_MANAGER_SINGLETON = 0x11B5D0;
    const int RVA_FS_PTR = 0x11F4F8;

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int GetManagerFn(out IntPtr mgr);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int PathFn(IntPtr self, IntPtr path);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateTerrainFn(IntPtr self, out IntPtr outPtr, IntPtr cfg);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int BoolArgFn(IntPtr self, IntPtr arg);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int FourArgFn(IntPtr self, IntPtr a, IntPtr b, IntPtr c);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr GetPtrFn();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr MethodPtrFn(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int Float2Fn(IntPtr self, float x, float z);
    [StructLayout(LayoutKind.Sequential)]
    struct F2 { public float a; public float b; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int F2Fn(IntPtr self, F2 p, F2 q);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

    static IntPtr Vt(IntPtr obj, int index)
    {
        IntPtr vt = Marshal.ReadIntPtr(obj);
        return Marshal.ReadIntPtr(vt, index * IntPtr.Size);
    }

    static string Hex(IntPtr p) { return "0x" + p.ToInt64().ToString("X"); }

    static T Fn<T>(IntPtr p) where T : class
    {
        return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
    }

    public static void Run(Action<string> log, string mapPath)
    {
        IntPtr h = GetModuleHandleA("PhysicsEngineX64.dll");
        log("physprobe: module=" + Hex(h));
        if (h == IntPtr.Zero) { log("physprobe: PhysicsEngineX64.dll not loaded"); return; }

        IntPtr getMgrPtr = GetProcAddress(h, "GetPhysicsManager");
        if (getMgrPtr == IntPtr.Zero) { log("physprobe: GetPhysicsManager missing"); return; }
        var getMgr = Fn<GetManagerFn>(getMgrPtr);
        IntPtr mgr = IntPtr.Zero;
        int hr = getMgr(out mgr);
        log("physprobe: GetPhysicsManager hr=" + hr + " mgr=" + Hex(mgr));
        if (mgr == IntPtr.Zero) return;
        log("physprobe: mgr vtable rva=0x" + (Marshal.ReadIntPtr(mgr).ToInt64() - h.ToInt64()).ToString("X"));
        log("physprobe: fs=" + Hex(Marshal.ReadIntPtr(h, RVA_FS_PTR)));
        // route 2 foundation: the engine's own PhysX singletons (offsets from
        // _InitPhysX's PxCreate* stores, 2026-10-01)
        log("physprobe: pxFoundation=[mgr+0x10]=" + Hex(Marshal.ReadIntPtr(mgr, 0x10))
            + " pxPhysics=[mgr+0x38]=" + Hex(Marshal.ReadIntPtr(mgr, 0x38))
            + " pxCooking=[mgr+0x40]=" + Hex(Marshal.ReadIntPtr(mgr, 0x40)));

        IntPtr cfg = Marshal.AllocHGlobal(16);
        Marshal.WriteInt32(cfg, 0, 2);
        Marshal.WriteInt32(cfg, 4, 1);
        Marshal.WriteInt32(cfg, 8, 0);
        Marshal.WriteInt32(cfg, 12, 2);
        IntPtr terrain = IntPtr.Zero;
        try
        {
            var createTerrain = Fn<CreateTerrainFn>(Vt(mgr, 14));
            hr = createTerrain(mgr, out terrain, cfg);
            log("physprobe: CreatePhysXTerrain hr=" + hr + " terrain=" + Hex(terrain));
        }
        finally { Marshal.FreeHGlobal(cfg); }
        if (terrain == IntPtr.Zero) return;
        log("physprobe: terrain vtable rva=0x" + (Marshal.ReadIntPtr(terrain).ToInt64() - h.ToInt64()).ToString("X"));
        for (int i = 0; i < 16; i++)
        {
            long vr = Vt(terrain, i).ToInt64() - h.ToInt64();
            if (vr > 0x1000 && vr < 0x100000)
                log(string.Format("physprobe:   terr vt[{0}] rva=0x{1:X}", i, vr));
        }

        // Engine-side scene argument for CreatePhysicsScene (manager vt[14]):
        // the adapter's KG3DEngineManager::Init @0x737da calls
        // mgr vt[0] Init(cfg, [engineMgr+0x2018], 0) then SetWorkingDir - the
        // engine object is our active engine scene, resolved exactly like
        // EngineRay does (KG3DEngineDX11EX64!KG3D_GetEngine2 ->
        // GetActiveWindow2 -> Get3DScene2).
        IntPtr engScene = IntPtr.Zero;
        try
        {
            IntPtr hEng = GetModuleHandleA("KG3DEngineDX11EX64.dll");
            if (hEng != IntPtr.Zero)
            {
                IntPtr pGet = GetProcAddress(hEng, "KG3D_GetEngine2");
                IntPtr pWin = GetProcAddress(hEng, "?GetActiveWindow2@KG3D_Engine@@UEAAPEAVKG3D_Window@@XZ");
                IntPtr pScene = GetProcAddress(hEng, "?Get3DScene2@KG3D_Window@@UEAAPEAVKG3D_Scene@@XZ");
                log("physprobe: engine exports get=" + (pGet != IntPtr.Zero) + " win=" + (pWin != IntPtr.Zero) + " scene=" + (pScene != IntPtr.Zero));
                if (pGet != IntPtr.Zero && pWin != IntPtr.Zero && pScene != IntPtr.Zero)
                {
                    var getEngine = Fn<GetPtrFn>(pGet);
                    var getWindow = Fn<MethodPtrFn>(pWin);
                    var getScene = Fn<MethodPtrFn>(pScene);
                    IntPtr eng = getEngine();
                    IntPtr win = eng == IntPtr.Zero ? IntPtr.Zero : getWindow(eng);
                    engScene = win == IntPtr.Zero ? IntPtr.Zero : getScene(win);
                    log("physprobe: engine=" + Hex(eng) + " window=" + Hex(win) + " engScene=" + Hex(engScene));
                }
            }
        }
        catch (Exception e) { log("physprobe: engine scene resolve ex: " + e.Message); }

        if (engScene != IntPtr.Zero)
        {
            IntPtr outBuf = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(outBuf, IntPtr.Zero);
                var createScene = Fn<FourArgFn>(Vt(mgr, 16));
                int hr2 = createScene(mgr, outBuf, engScene, IntPtr.Zero);
                IntPtr scene = Marshal.ReadIntPtr(outBuf);
                log("physprobe: CreatePhysicsScene(vt16) hr=" + hr2 + " scene=" + Hex(scene));
                if (scene != IntPtr.Zero)
                {
                    log("physprobe: scene vtable rva=0x" + (Marshal.ReadIntPtr(scene).ToInt64() - h.ToInt64()).ToString("X"));
                    for (int i = 0; i < 24; i++)
                    {
                        long vr = Vt(scene, i).ToInt64() - h.ToInt64();
                        if (vr > 0x1000 && vr < 0x100000)
                            log(string.Format("physprobe:   scene vt[{0}] rva=0x{1:X}", i, vr));
                        else
                            log(string.Format("physprobe:   scene vt[{0}] (not code) 0x{1:X}", i, Vt(scene, i).ToInt64()));
                    }
                }
            }
            catch (Exception e) { log("physprobe: CreatePhysicsScene ex: " + e.Message); }
            finally { Marshal.FreeHGlobal(outBuf); }
        }

        IntPtr p2 = Marshal.StringToHGlobalAnsi(mapPath);
        try
        {
            var loadFull = Fn<FourArgFn>(Vt(terrain, 5));
            int ok = loadFull(terrain, p2, IntPtr.Zero, mgr);
            log("physprobe: LoadTerrain(vt5) ok=" + ok);
        }
        finally { Marshal.FreeHGlobal(p2); }

        IntPtr regionMgr = Marshal.ReadIntPtr(terrain, 0x58);
        log("physprobe: regionMgr=" + Hex(regionMgr)
            + " table=" + Hex(Marshal.ReadIntPtr(terrain, 0x48))
            + " countX=" + Marshal.ReadInt32(terrain, 0x24)
            + " countY=" + Marshal.ReadInt32(terrain, 0x28));

        // stream at the map probe position (RC_PHYS_POS=x,y,z)
        float px = 147463.5f, py = 5231.0f, pz = 49911.7f;
        string posEnv = Environment.GetEnvironmentVariable("RC_PHYS_POS");
        if (!string.IsNullOrEmpty(posEnv))
        {
            string[] pp = posEnv.Split(',');
            float.TryParse(pp[0], out px);
            if (pp.Length > 1) float.TryParse(pp[1], out py);
            if (pp.Length > 2) float.TryParse(pp[2], out pz);
        }
        IntPtr posBuf = Marshal.AllocHGlobal(12);
        Marshal.WriteInt32(posBuf, 0, BitConverter.ToInt32(BitConverter.GetBytes(px), 0));
        Marshal.WriteInt32(posBuf, 4, BitConverter.ToInt32(BitConverter.GetBytes(py), 0));
        Marshal.WriteInt32(posBuf, 8, BitConverter.ToInt32(BitConverter.GetBytes(pz), 0));
        try
        {
            var update = Fn<BoolArgFn>(Vt(terrain, 2));
            for (int step = 0; step < 40; step++) { update(terrain, posBuf); Thread.Sleep(50); }
            log(string.Format("physprobe: UpdateTerrain x40 at ({0:F0},{1:F0},{2:F0}) done", px, py, pz));
        }
        catch (Exception e) { log("physprobe: UpdateTerrain ex: " + e.Message); }
        finally { Marshal.FreeHGlobal(posBuf); }

        // first LIVE engine-physics call: the engine terrain's own point query
        // (PhysicsTerrain vt[7], decoded 2026-10-01: world->cell via the
        // terrain matrix, tile array [+0x48], 8x8 sub-cell bit test -> bool).
        // Guarded: only called after LoadTerrain + streaming.
        try
        {
            log(string.Format("physprobe: terr fields cell={0:F0} countX={1} countZ={2} stepX={3:F1} stepZ={4:F1} originX={5:F0} originZ={6:F0}",
                Marshal.PtrToStructure<float>(terrain + 0x20),
                Marshal.ReadInt32(terrain, 0x24), Marshal.ReadInt32(terrain, 0x28),
                Marshal.PtrToStructure<float>(terrain + 0x30),
                Marshal.PtrToStructure<float>(terrain + 0x34),
                Marshal.PtrToStructure<float>(terrain + 0x38),
                Marshal.PtrToStructure<float>(terrain + 0x3C)));
            log(string.Format("physprobe: terr matrix row0=({0:F2},{1:F2},{2:F2},{3:F1}) row3=({4:F1},{5:F1},{6:F1},{7:F1})",
                Marshal.PtrToStructure<float>(terrain + 0xB0), Marshal.PtrToStructure<float>(terrain + 0xB4),
                Marshal.PtrToStructure<float>(terrain + 0xB8), Marshal.PtrToStructure<float>(terrain + 0xBC),
                Marshal.PtrToStructure<float>(terrain + 0xE0), Marshal.PtrToStructure<float>(terrain + 0xE4),
                Marshal.PtrToStructure<float>(terrain + 0xE8), Marshal.PtrToStructure<float>(terrain + 0xEC)));
            var pointQuery = Fn<F2Fn>(Vt(terrain, 7));
            float[] xs = new float[] { px, 23334f, 18915f, 20450f, 0f };
            float[] zs = new float[] { pz, 24224f, 36850f, 31000f, 0f };
            for (int i = 0; i < xs.Length; i++)
            {
                F2 p = new F2(); p.a = xs[i]; p.b = zs[i];
                F2 q = new F2(); q.a = 0f; q.b = 0f;
                int r = pointQuery(terrain, p, q);
                log(string.Format("physprobe: terrain vt[7] point({0:F0},{1:F0}) -> {2}", xs[i], zs[i], r));
            }
            // tile entries: [terrain+0x48] array, stride 0x30, index regionZ*8+regionX
            IntPtr tileArr = Marshal.ReadIntPtr(terrain, 0x48);
            log("physprobe: tileArr=" + Hex(tileArr));
            for (int i = 0; i < xs.Length; i++)
            {
                int rx = (int)((xs[i] + 102400f) / 51200f);
                int rz = (int)((zs[i] + 102400f) / 51200f);
                int idx = rz * 8 + rx;
                if (idx < 0 || idx > 63) { log(string.Format("physprobe: tile idx {0} out of range", idx)); continue; }
                IntPtr te = tileArr + idx * 0x30;
                log(string.Format("physprobe: tile[{0}] (r{1},{2}) type={3} f4={4} f8={5} p10={6}",
                    idx, rx, rz, Marshal.ReadInt32(te), Marshal.ReadInt32(te, 4), Marshal.ReadInt32(te, 8), Hex(Marshal.ReadIntPtr(te, 0x10))));
            }
        }
        catch (Exception e) { log("physprobe: terrain vt[7] ex: " + e.Message); }

        IntPtr posBuf2 = Marshal.AllocHGlobal(12);
        Marshal.WriteInt32(posBuf2, 0, BitConverter.ToInt32(BitConverter.GetBytes(px), 0));
        Marshal.WriteInt32(posBuf2, 4, BitConverter.ToInt32(BitConverter.GetBytes(py), 0));
        Marshal.WriteInt32(posBuf2, 8, BitConverter.ToInt32(BitConverter.GetBytes(pz), 0));

        IntPtr dynLoader = IntPtr.Zero;
        IntPtr cfg2 = Marshal.AllocHGlobal(16);
        Marshal.WriteInt32(cfg2, 0, 2);
        Marshal.WriteInt32(cfg2, 4, 1);
        Marshal.WriteInt32(cfg2, 8, 0);
        Marshal.WriteInt32(cfg2, 12, 2);
        try
        {
            var createDyn = Fn<CreateTerrainFn>(Vt(mgr, 15));
            hr = createDyn(mgr, out dynLoader, cfg2);
            log("physprobe: CreatePhysicsSceneDynamicLoader hr=" + hr + " dynLoader=" + Hex(dynLoader));
        }
        finally { Marshal.FreeHGlobal(cfg2); }
        if (dynLoader != IntPtr.Zero)
        {
            long baseRva = Marshal.ReadIntPtr(dynLoader).ToInt64() - h.ToInt64();
            log("physprobe: dyn vtable rva=0x" + baseRva.ToString("X"));
            for (int i = 0; i < 16; i++)
                log(string.Format("physprobe:   dyn vt[{0}] rva=0x{1:X}", i, (Vt(dynLoader, i).ToInt64() - h.ToInt64())));

            // StaticPhysicsSceneManager::LoadFromFile(sceneDir, mapName)
            // (recon: 0x2BB80 = dyn vt[4]; CreateSceneFileDataLoader arg2 =
            //  scene base dir; arg3 must be non-null -> map name)
            string sceneDir = mapPath;
            try
            {
                string fn = System.IO.Path.GetFileName(sceneDir);
                sceneDir = sceneDir.Substring(0, sceneDir.Length - fn.Length).TrimEnd('\\');
            }
            catch { }
            string mapName = System.IO.Path.GetFileNameWithoutExtension(mapPath);
            log("physprobe: LoadFromFile dir=" + sceneDir + " name=" + mapName);
            IntPtr dirBuf = Marshal.StringToHGlobalAnsi(sceneDir);
            IntPtr nameBuf = Marshal.StringToHGlobalAnsi(mapName);
            IntPtr r8buf = Marshal.AllocHGlobal(16);
            try
            {
                var loadFromFile = Fn<FourArgFn>(Vt(dynLoader, 4));
                int ok = loadFromFile(dynLoader, dirBuf, nameBuf, IntPtr.Zero);
                log("physprobe: LoadFromFile ok=" + ok);
            }
            catch (Exception e) { log("physprobe: LoadFromFile ex: " + e.Message); }
            finally { Marshal.FreeHGlobal(dirBuf); Marshal.FreeHGlobal(nameBuf); Marshal.FreeHGlobal(r8buf); }

            // StaticPhysicsSceneManager::UpdateScene(float3 pos) (dyn vt[2])
            try
            {
                var updateScene = Fn<BoolArgFn>(Vt(dynLoader, 2));
                for (int i = 0; i < 5; i++) { updateScene(dynLoader, posBuf2); Thread.Sleep(50); }
                log("physprobe: UpdateScene x5 done");
            }
            catch (Exception e) { log("physprobe: UpdateScene ex: " + e.Message); }
        }
        Marshal.FreeHGlobal(posBuf2);

        // scan the process for live engine physics objects (vtable RVAs)
        if (Environment.GetEnvironmentVariable("RC_PHYS_SCAN") == "1")
        {
            long hb = h.ToInt64();
            long[] known = new long[] { 0xFA7B8, 0xFCCA8, 0xFCFE0, 0xFD198, 0xFA6C0, 0xFD2B0 };
            string[] names = new string[] { "PhysicsScene", "StaticSceneMgr", "PhysicsTerrain", "TerrainRegionMgr", "PhysicsManager", "TerrainLoader" };
            var counts = new int[known.Length];
            long addr = 0x10000;
            while (addr < 0x7FFFFFFFFFFF)
            {
                MEMORY_BASIC_INFORMATION mbi;
                if (VirtualQuery(new IntPtr(addr), out mbi, new IntPtr(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)))) == IntPtr.Zero)
                    break;
                long rsize = mbi.RegionSize.ToInt64();
                if (rsize <= 0) break;
                bool readable = mbi.State == 0x1000 && (mbi.Protect & 0x101) == 0 && mbi.Type == 0x20000;
                if (readable && rsize <= 16L * 1024 * 1024)
                {
                    try
                    {
                        byte[] buf = new byte[(int)rsize];
                        Marshal.Copy(new IntPtr(addr), buf, 0, (int)rsize);
                        for (int i = 0; i + 8 <= buf.Length; i += 8)
                        {
                            long v = BitConverter.ToInt64(buf, i);
                            long rva = v - hb;
                            for (int k = 0; k < known.Length; k++)
                                if (rva == known[k]) counts[k]++;
                        }
                    }
                    catch { }
                }
                addr += rsize;
            }
            for (int k = 0; k < known.Length; k++)
                log(string.Format("physprobe: scan {0} (rva 0x{1:X}) = {2}", names[k], known[k], counts[k]));
        }
    }
}
