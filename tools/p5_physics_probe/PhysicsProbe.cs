// P5 feasibility probe: run the game's own physics stack standalone.
// Sequence recovered from the legacy engine_host_spike/MapSpike.cs collision
// probe (commit 2997649^) - see docs/movement/REAL_CLIENT_MAP_COLLISION.md.
// Loads the game's PhysicsEngineX64.dll in a fresh process and drives:
//   SetWorkingDir -> GetPhysicsManager -> Init (if needed) ->
//   CreatePhysXTerrain -> CreatePhysicsTerrainDataLoader(map) ->
//   GetTerrainDesc -> LoadTerrain(vt5) -> UpdateTerrain(vt2) x N ->
//   CreatePhysicsSceneDynamicLoader(vt15).
// Env: MAP_PHYS_DLL (default game client DLL), MAP_PROBE_ROOT (default
// C:\SeasunGame\MovieEditor), MAP_PROBE_MAP (VFS jsonmap), MAP_PROBE_POS
// (x,y,z streaming centre), MAP_PROBE_DEEP=1 (do the deep steps).
using System;
using System.Runtime.InteropServices;
using System.Threading;

internal static class PhysicsProbe
{
    const int RVA_MANAGER_SINGLETON = 0x11B5D0;
    const int RVA_ALLOC_PTR = 0x11F450;
    const int RVA_MEM_MGR = 0x11F460;
    const int RVA_FS_PTR = 0x11F4F8;
    const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr LoadLibraryExA(string path, IntPtr hFile, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int GetManagerFn(out IntPtr mgr);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int TwoArgFn(IntPtr self, IntPtr a, IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int PathFn(IntPtr self, IntPtr path);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateTerrainFn(IntPtr self, out IntPtr outPtr, IntPtr cfg);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateDataLoaderFn(IntPtr path, IntPtr opt, out IntPtr loader);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void DescFn(IntPtr self, IntPtr out32);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int BoolArgFn(IntPtr self, IntPtr arg);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int FourArgFn(IntPtr self, IntPtr a, IntPtr b, IntPtr c);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int LoadRegionFn(IntPtr self, int nX, int nZ, IntPtr pData, int nCount,
                              IntPtr outA, IntPtr outB, IntPtr outC);

    static void Log(string s) { Console.WriteLine(s); Console.Out.Flush(); }

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

    static float F(IntPtr p, int off)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(p, off)), 0);
    }

    static int Main()
    {
        string physDll = Environment.GetEnvironmentVariable("MAP_PHYS_DLL");
        if (string.IsNullOrEmpty(physDll))
            physDll = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll";
        string root = Environment.GetEnvironmentVariable("MAP_PROBE_ROOT");
        if (string.IsNullOrEmpty(root)) root = @"C:\SeasunGame\MovieEditor";
        string mapPath = Environment.GetEnvironmentVariable("MAP_PROBE_MAP");
        if (string.IsNullOrEmpty(mapPath))
            mapPath = @"data\source\maps\龙门寻宝\龙门寻宝.jsonmap";
        bool deep = Environment.GetEnvironmentVariable("MAP_PROBE_DEEP") == "1";
        Log("physDll=" + physDll);
        Log("root=" + root);
        Log("map=" + mapPath);

        IntPtr h = GetModuleHandleA("PhysicsEngineX64.dll");
        Log("already loaded handle=" + Hex(h));
        // Standalone FS bootstrap: the Semantic/Lua-backed file system that
        // SetWorkingDir creates needs its modules present in the process (the
        // engine host loads them at startup). Preload them next to physDll.
        if (Environment.GetEnvironmentVariable("MAP_PROBE_PRELOAD") == "1")
        {
            string dir = System.IO.Path.GetDirectoryName(physDll);
            foreach (string name in new string[] {
                "KGCommonX64.dll", "SemanticX64.dll", "Engine_Lua5X64.dll",
                "KG3DEngineX64.dll", "KG3DEngineDX11X64.dll" })
            {
                string p = System.IO.Path.Combine(dir, name);
                if (!System.IO.File.Exists(p)) { Log("preload missing: " + p); continue; }
                IntPtr m = LoadLibraryExA(p, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
                Log("preload " + name + " -> " + Hex(m) + (m == IntPtr.Zero ? " err=" + Marshal.GetLastWin32Error() : ""));
            }
        }
        if (h == IntPtr.Zero)
            h = LoadLibraryExA(physDll, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (h == IntPtr.Zero)
        {
            Log("LoadLibraryEx FAILED err=" + Marshal.GetLastWin32Error());
            return 2;
        }
        Log("module base=" + Hex(h));

        IntPtr allocPtr = Marshal.ReadIntPtr(h, RVA_ALLOC_PTR);
        IntPtr memMgr = Marshal.ReadIntPtr(h, RVA_MEM_MGR);
        IntPtr fsPtr = Marshal.ReadIntPtr(h, RVA_FS_PTR);
        Log("pre-init: allocPtr=" + Hex(allocPtr) + " memMgr=" + Hex(memMgr) + " fs=" + Hex(fsPtr));

        IntPtr getMgrPtr = GetProcAddress(h, "GetPhysicsManager");
        if (getMgrPtr == IntPtr.Zero) { Log("GetPhysicsManager not found"); return 2; }
        var getMgr = Fn<GetManagerFn>(getMgrPtr);
        IntPtr mgr = IntPtr.Zero;
        int hr = getMgr(out mgr);
        Log("GetPhysicsManager hr=" + hr + " mgr=" + Hex(mgr));
        if (mgr == IntPtr.Zero) return 2;
        Log("mgr vtable rva=0x" + (Marshal.ReadIntPtr(mgr).ToInt64() - h.ToInt64()).ToString("X"));

        // 1) SetWorkingDir(root)
        IntPtr path = Marshal.StringToHGlobalAnsi(root);
        try
        {
            var setWd = Fn<PathFn>(Vt(mgr, 2));
            hr = setWd(mgr, path);
            Log("SetWorkingDir hr=" + hr + " fs=" + Hex(Marshal.ReadIntPtr(h, RVA_FS_PTR)));
        }
        finally { Marshal.FreeHGlobal(path); }

        // 2) Init if the foundation is absent (standalone: first init)
        IntPtr foundation = Marshal.ReadIntPtr(mgr, 0x10);
        Log("mgr m_pPhysXFoundation=" + Hex(foundation));
        if (foundation == IntPtr.Zero)
        {
            var init = Fn<TwoArgFn>(Vt(mgr, 0));
            hr = init(mgr, IntPtr.Zero, IntPtr.Zero);
            Log("Init hr=" + hr + " allocPtr=" + Hex(Marshal.ReadIntPtr(h, RVA_ALLOC_PTR))
                + " memMgr=" + Hex(Marshal.ReadIntPtr(h, RVA_MEM_MGR)));
        }
        else Log("Init SKIPPED: already initialised");

        // 3) CreatePhysXTerrain(out, cfg)
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
            Log("CreatePhysXTerrain hr=" + hr + " terrain=" + Hex(terrain));
        }
        finally { Marshal.FreeHGlobal(cfg); }

        // 4) CreatePhysicsTerrainDataLoader(map, 0, out loader)
        IntPtr loader = IntPtr.Zero;
        IntPtr createLoaderPtr = GetProcAddress(h, "CreatePhysicsTerrainDataLoader");
        if (createLoaderPtr == IntPtr.Zero) { Log("CreatePhysicsTerrainDataLoader not found"); return 2; }
        IntPtr mapAnsi = Marshal.StringToHGlobalAnsi(mapPath);
        try
        {
            var createLoader = Fn<CreateDataLoaderFn>(createLoaderPtr);
            int ok = createLoader(mapAnsi, IntPtr.Zero, out loader);
            Log("CreatePhysicsTerrainDataLoader ok=" + ok + " loader=" + Hex(loader));
        }
        finally { Marshal.FreeHGlobal(mapAnsi); }
        if (loader == IntPtr.Zero) { Log("loader creation failed; stopping"); return 3; }

        // 5) loader vt[2] GetTerrainDesc(out 32)
        IntPtr desc32 = Marshal.AllocHGlobal(64);
        for (int i = 0; i < 64; i += 4) Marshal.WriteInt32(desc32, i, 0);
        var getDesc = Fn<DescFn>(Vt(loader, 2));
        getDesc(loader, desc32);
        Log(string.Format("desc: size={0} nx={1} nz={2} cell={3:F2} origin=({4:F0},{5:F0})",
            Marshal.ReadInt32(desc32, 0), Marshal.ReadInt32(desc32, 4), Marshal.ReadInt32(desc32, 8),
            F(desc32, 0x10), F(desc32, 0x18), F(desc32, 0x1C)));

        if (!deep)
        {
            Log("shallow probe done (set MAP_PROBE_DEEP=1 for LoadTerrain/stream)");
            return 0;
        }

        // 6) terrain vt[5] LoadTerrain(path, 0, mgr)
        if (terrain != IntPtr.Zero)
        {
            IntPtr p2 = Marshal.StringToHGlobalAnsi(mapPath);
            try
            {
                var loadFull = Fn<FourArgFn>(Vt(terrain, 5));
                int ok = loadFull(terrain, p2, IntPtr.Zero, mgr);
                Log("LoadTerrain(vt5) ok=" + ok);
            }
            finally { Marshal.FreeHGlobal(p2); }

            IntPtr regionMgr = Marshal.ReadIntPtr(terrain, 0x58);
            Log("regionMgr=" + Hex(regionMgr));
            IntPtr table = Marshal.ReadIntPtr(terrain, 0x48);
            int countX = Marshal.ReadInt32(terrain, 0x24), countY = Marshal.ReadInt32(terrain, 0x28);
            Log(string.Format("region table={0} countX={1} countY={2}", Hex(table), countX, countY));

            // 6b) stream regions at MAP_PROBE_POS
            string posEnv = Environment.GetEnvironmentVariable("MAP_PROBE_POS");
            float px = 147463.5f, py = 5231.0f, pz = 49911.7f;
            if (!string.IsNullOrEmpty(posEnv))
            {
                string[] pp = posEnv.Split(',');
                px = float.Parse(pp[0]); py = float.Parse(pp[1]); pz = float.Parse(pp[2]);
            }
            IntPtr posBuf = Marshal.AllocHGlobal(12);
            Marshal.WriteInt32(posBuf, 0, BitConverter.ToInt32(BitConverter.GetBytes(px), 0));
            Marshal.WriteInt32(posBuf, 4, BitConverter.ToInt32(BitConverter.GetBytes(py), 0));
            Marshal.WriteInt32(posBuf, 8, BitConverter.ToInt32(BitConverter.GetBytes(pz), 0));
            try
            {
                var update = Fn<BoolArgFn>(Vt(terrain, 2));
                for (int step = 0; step < 40; step++) { update(terrain, posBuf); Thread.Sleep(100); }
                Log(string.Format("UpdateTerrain x40 at ({0:F0},{1:F0},{2:F0}) done", px, py, pz));
            }
            catch (Exception e) { Log("UpdateTerrain ex: " + e.Message); }
            finally { Marshal.FreeHGlobal(posBuf); }
        }

        // 7) manager vt[15] CreatePhysicsSceneDynamicLoader(out, cfg)
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
            Log("CreatePhysicsSceneDynamicLoader hr=" + hr + " dynLoader=" + Hex(dynLoader));
        }
        finally { Marshal.FreeHGlobal(cfg2); }
        if (dynLoader != IntPtr.Zero)
        {
            Log("dynLoader vtable rva=0x" + (Marshal.ReadIntPtr(dynLoader).ToInt64() - h.ToInt64()).ToString("X"));
            for (int i = 0; i < 16; i++)
                Log(string.Format("  dyn vt[{0}] rva=0x{1:X}", i, (Vt(dynLoader, i).ToInt64() - h.ToInt64())));
        }

        Log("P5 probe done");
        return 0;
    }
}
