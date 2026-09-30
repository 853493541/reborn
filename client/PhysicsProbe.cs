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
        }
    }
}
