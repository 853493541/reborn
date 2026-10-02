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
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CookFn(IntPtr self, IntPtr name, IntPtr path, IntPtr r9,
                        IntPtr s5, IntPtr s6, IntPtr s7, IntPtr s8, IntPtr s9);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate ulong HashFn(IntPtr s);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr LoadMeshFn(IntPtr path);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int Bool1Fn(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr Ptr1Fn(IntPtr self);
    delegate void GetTriFn(IntPtr meshGeom, IntPtr pose, uint index, IntPtr outTri, IntPtr outIndices);
    delegate uint FindOverlapFn(IntPtr geom, IntPtr geomPose, IntPtr meshGeom, IntPtr meshPose,
        IntPtr results, uint maxResults, uint startIndex, IntPtr overflow);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate bool SweepFn(IntPtr unitDir, float distance, IntPtr geom, IntPtr pose, uint triCount,
        IntPtr triangles, IntPtr hit, IntPtr hitFlagsPtr, IntPtr cachedIndex, float inflation, bool anyHit);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int Bool2Fn(IntPtr self, IntPtr a, IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate uint WriteFn(IntPtr self, IntPtr src, uint count);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate uint ReadFn(IntPtr self, IntPtr dest, uint count);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr Ptr2Fn(IntPtr self, IntPtr a);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr ShapeFn(IntPtr self, IntPtr geom, IntPtr materials, ushort count, IntPtr isExclusive, uint shapeFlags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr MatFn(IntPtr self, IntPtr dst, uint maxCount, uint startIdx);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void Void1Fn(IntPtr self);
    static byte[] s_pxInBuf;
    static int s_pxInPos, s_pxInLen, s_readCalls;
    static uint PxStreamRead(IntPtr self, IntPtr dest, uint count)
    {
        s_readCalls++;
        int want = (int)count, avail = s_pxInLen - s_pxInPos;
        int n = want < avail ? want : (avail > 0 ? avail : 0);
        if (n > 0) { Marshal.Copy(s_pxInBuf, s_pxInPos, dest, n); s_pxInPos += n; }
        return (uint)n;
    }
    static ReadFn s_readKeep;
    static IntPtr MakePxInStream(byte[] data, int len)
    {
        s_readKeep = PxStreamRead;
        s_pxInBuf = data; s_pxInLen = len; s_pxInPos = 0; s_readCalls = 0;
        IntPtr vtbl = Marshal.AllocHGlobal(16);
        Marshal.WriteIntPtr(vtbl, 0, Marshal.GetFunctionPointerForDelegate(s_readKeep));
        Marshal.WriteIntPtr(vtbl, 8, Marshal.GetFunctionPointerForDelegate(s_dtorKeep));
        IntPtr s = Marshal.AllocHGlobal(16);
        Marshal.WriteIntPtr(s, 0, vtbl);
        Marshal.WriteIntPtr(s, 8, IntPtr.Zero);
        return s;
    }
    static IntPtr PxCookOk(IntPtr pxCook, IntPtr pxPhysics, Action<string> log)
    {
        IntPtr desc = Marshal.AllocHGlobal(0x60);
        for (int i = 0; i < 0x60 / 4; i++) Marshal.WriteInt32(desc, i * 4, 0);
        IntPtr verts = Marshal.AllocHGlobal(12 * 12);
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
            {
                int o = (i + j * 4) * 12;
                Marshal.WriteInt32(verts, o + 0, BitConverter.ToInt32(BitConverter.GetBytes(i * 100f), 0));
                Marshal.WriteInt32(verts, o + 4, BitConverter.ToInt32(BitConverter.GetBytes(0f), 0));
                Marshal.WriteInt32(verts, o + 8, BitConverter.ToInt32(BitConverter.GetBytes(j * 100f), 0));
            }
        IntPtr tris = Marshal.AllocHGlobal(12 * 6);
        int ti = 0;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 2; j++)
            {
                ushort a = (ushort)(i + j * 4), b = (ushort)(i + 1 + j * 4);
                ushort c = (ushort)(i + 1 + (j + 1) * 4), d = (ushort)(i + (j + 1) * 4);
                Marshal.WriteInt16(tris, ti++ * 2, (short)a);
                Marshal.WriteInt16(tris, ti++ * 2, (short)b);
                Marshal.WriteInt16(tris, ti++ * 2, (short)c);
                Marshal.WriteInt16(tris, ti++ * 2, (short)a);
                Marshal.WriteInt16(tris, ti++ * 2, (short)c);
                Marshal.WriteInt16(tris, ti++ * 2, (short)d);
            }
        Marshal.WriteInt32(desc, 0x00, 12);
        Marshal.WriteIntPtr(desc, 0x08, verts);
        Marshal.WriteInt32(desc, 0x10, 12);
        Marshal.WriteInt32(desc, 0x18, 6);
        Marshal.WriteIntPtr(desc, 0x20, tris);
        Marshal.WriteInt32(desc, 0x28, 12);
        Marshal.WriteInt16(desc, 0x30, 2);
        Marshal.WriteInt32(desc, 0x48, unchecked((int)0x3A83126F));
        s_pxStreamLen = 0;
        IntPtr stream = MakePxStream();
        int r = Fn<Bool2Fn>(Vt(pxCook, 4))(pxCook, desc, stream);
        Marshal.FreeHGlobal(verts); Marshal.FreeHGlobal(tris); Marshal.FreeHGlobal(desc);
        log("physprobe: PX_MESH cook=" + r + " bytes=" + s_pxStreamLen);
        return (r != 0 && s_pxStreamLen > 0) ? IntPtr.Add(pxPhysics, 0) : IntPtr.Zero;
    }
    static int s_pxStreamLen;
    static byte[] s_pxStreamBuf = new byte[1 << 20];
    static uint PxStreamWrite(IntPtr self, IntPtr src, uint count)
    {
        int n = (int)count;
        if (n > 0 && src != IntPtr.Zero && s_pxStreamLen + n <= s_pxStreamBuf.Length)
        {
            Marshal.Copy(src, s_pxStreamBuf, s_pxStreamLen, n);
            s_pxStreamLen += n;
        }
        return count;
    }
    static void PxStreamDtor(IntPtr self) { }
    static WriteFn s_writeKeep;
    static Void1Fn s_dtorKeep;
    static IntPtr s_pxStream;
    static IntPtr MakePxStream()
    {
        s_writeKeep = PxStreamWrite;
        s_dtorKeep = PxStreamDtor;
        IntPtr vtbl = Marshal.AllocHGlobal(16);
        Marshal.WriteIntPtr(vtbl, 0, Marshal.GetFunctionPointerForDelegate(s_writeKeep));
        Marshal.WriteIntPtr(vtbl, 8, Marshal.GetFunctionPointerForDelegate(s_dtorKeep));
        s_pxStream = Marshal.AllocHGlobal(16);
        Marshal.WriteIntPtr(s_pxStream, 0, vtbl);
        Marshal.WriteIntPtr(s_pxStream, 8, IntPtr.Zero);
        return s_pxStream;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int Bool3Fn(IntPtr self, IntPtr a, IntPtr b, IntPtr c);

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
        // PhysX-direct solve step: dump the runtime vtables so the cook/create
        // indices are read from the engine's own objects (never guessed).
        if (Environment.GetEnvironmentVariable("RC_PX_VT") == "1")
        {
            IntPtr pxPhys = Marshal.ReadIntPtr(mgr, 0x38);
            IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
            IntPtr hPhys = GetModuleHandleA("PhysX3_x64.dll");
            IntPtr hCook = GetModuleHandleA("PhysX3Cooking_x64.dll");
            IntPtr hCommon = GetModuleHandleA("PhysX3Common_x64.dll");
            log("physprobe: modules PhysX3=" + Hex(hPhys) + " Cooking=" + Hex(hCook) + " Common=" + Hex(hCommon));
            for (int i = 0; i < 60; i++)
            {
                long a = Vt(pxPhys, i).ToInt64();
                long r = a - hPhys.ToInt64();
                if (r > 0x1000 && r < 0x200000) log(string.Format("physprobe: pxPhysics vt[{0}] rva=0x{1:X}", i, r));
                else { log(string.Format("physprobe: pxPhysics vt[{0}] = 0x{1:X} (not in PhysX3)", i, a)); break; }
            }
            for (int i = 0; i < 30; i++)
            {
                long a = Vt(pxCook, i).ToInt64();
                long r = a - hCook.ToInt64();
                if (r > 0x1000 && r < 0x200000) log(string.Format("physprobe: pxCooking vt[{0}] rva=0x{1:X}", i, r));
                else { log(string.Format("physprobe: pxCooking vt[{0}] = 0x{1:X} (not in Cooking)", i, a)); break; }
            }
        }

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

        // Stage-1 attempt v2 (opt-in RC_PX_COOK=1, test exe only): the factory's
        // name/path args are STANDARD HASH STRINGS (KG3D_ConvertToStandardHashString
        // returns the 64-bit hash; the list nodes carry the hash at +0x10). The
        // hash conversion works in-host (h1=h2=4F65C0E9) but the factory call
        // still CRASHES: its r9/stack args are engine-internal objects (the actor
        // option etc.) built by the engine's own collision pipeline. Piecemeal
        // calling is not viable; the route needs the engine's internal init
        // chain (represent/scene) or the PhysX-direct ABI. Parked (2nd stage-1
        // abandon data point, 2026-10-01).
        if (Environment.GetEnvironmentVariable("RC_PX_COOK") == "1")
        {
            IntPtr factory = h + 0x129590;
            string meshPath = "data/source/maps_source/建筑配套/pj_玉门草棚001_hd.mesh";
            log("physprobe: PX_COOK v2 try factory=" + Hex(factory) + " path=" + meshPath);
            IntPtr hCommon = GetModuleHandleA("KGCommonX64.dll");
            IntPtr pConv = hCommon == IntPtr.Zero ? IntPtr.Zero
                : GetProcAddress(hCommon, "KG3D_ConvertToStandardHashString");
            if (pConv == IntPtr.Zero) { log("physprobe: PX_COOK: no KGCommon hash export"); }
            else
            {
                var conv = Fn<HashFn>(pConv);
                IntPtr pathBuf = Marshal.StringToHGlobalAnsi(meshPath);
                try
                {
                    ulong h1 = conv(pathBuf);
                    ulong h2 = conv(pathBuf);
                    log(string.Format("physprobe: PX_COOK hashes h1={0:X} h2={1:X}", h1, h2));
                    IntPtr outShape = Marshal.AllocHGlobal(IntPtr.Size);
                    Marshal.WriteIntPtr(outShape, IntPtr.Zero);
                    IntPtr b1 = Marshal.AllocHGlobal(8); Marshal.WriteByte(b1, 0);
                    IntPtr b2 = Marshal.AllocHGlobal(8); Marshal.WriteByte(b2, 0);
                    try
                    {
                        var cook = Fn<CookFn>(h + 0x1DC70);
                        int r = cook(factory, (IntPtr)(long)h1, (IntPtr)(long)h2, IntPtr.Zero,
                                     IntPtr.Zero, b1, outShape, (IntPtr)1, b2);
                        log("physprobe: PX_COOK ret=" + r + " outShape=" + Hex(Marshal.ReadIntPtr(outShape)));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(outShape);
                        Marshal.FreeHGlobal(b1); Marshal.FreeHGlobal(b2);
                    }
                }
                finally { Marshal.FreeHGlobal(pathBuf); }
            }
        }

        // PhysX-direct solve step 2 (opt-in RC_PX_COOK2=1, test exe): build a
        // minimal PxTriangleMeshDesc (quad) and call the engine's PxCooking:
        // vt[6]=validateTriangleMesh (bool, low risk - layout test) then
        // vt[4] (the triangle cook; identified via the cook helper 0x1F7E0).
        if (Environment.GetEnvironmentVariable("RC_PX_COOK2") == "1")
        {
            IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
            log("physprobe: PX_COOK2 cooking=" + Hex(pxCook));
            IntPtr desc = Marshal.AllocHGlobal(0x60);
            for (int i = 0; i < 0x60 / 4; i++) Marshal.WriteInt32(desc, i * 4, 0);
            // layout decoded from validator 0x7E40 + cooker 0x7970 field reads:
            // +0x00 pointsStride, +0x08 points, +0x10 nbVertices, +0x18 trianglesStride,
            // +0x20 triangles, +0x28 nbTriangles, +0x30 flags u16 (bit1=16-bit indices),
            // +0x48 convexEdgeThreshold f32 (must be 0.001)
            IntPtr verts = Marshal.AllocHGlobal(12 * 12);
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 3; j++)
                {
                    int o = (i + j * 4) * 12;
                    Marshal.WriteInt32(verts, o + 0, BitConverter.ToInt32(BitConverter.GetBytes(i * 100f), 0));
                    Marshal.WriteInt32(verts, o + 4, BitConverter.ToInt32(BitConverter.GetBytes(0f), 0));
                    Marshal.WriteInt32(verts, o + 8, BitConverter.ToInt32(BitConverter.GetBytes(j * 100f), 0));
                }
            IntPtr tris = Marshal.AllocHGlobal(12 * 6);
            int ti = 0;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                {
                    ushort a = (ushort)(i + j * 4), b = (ushort)(i + 1 + j * 4);
                    ushort c = (ushort)(i + 1 + (j + 1) * 4), d = (ushort)(i + (j + 1) * 4);
                    Marshal.WriteInt16(tris, ti * 2, (short)a); ti++;
                    Marshal.WriteInt16(tris, ti * 2, (short)b); ti++;
                    Marshal.WriteInt16(tris, ti * 2, (short)c); ti++;
                    Marshal.WriteInt16(tris, ti * 2, (short)a); ti++;
                    Marshal.WriteInt16(tris, ti * 2, (short)c); ti++;
                    Marshal.WriteInt16(tris, ti * 2, (short)d); ti++;
                }
            Marshal.WriteInt32(desc, 0x00, 12);       // pointsStride
            Marshal.WriteIntPtr(desc, 0x08, verts);   // points
            Marshal.WriteInt32(desc, 0x10, 12);       // nbVertices
            Marshal.WriteInt32(desc, 0x18, 6);        // trianglesStride
            Marshal.WriteIntPtr(desc, 0x20, tris);    // triangles
            Marshal.WriteInt32(desc, 0x28, 12);       // nbTriangles
            Marshal.WriteInt16(desc, 0x30, 2);        // flags: e16_BIT_INDICES
            Marshal.WriteInt32(desc, 0x48, unchecked((int)0x3A83126F)); // convexEdgeThreshold = 0.001 (required)
            try
            {
                IntPtr cookBase = GetModuleHandleA("PhysX3Cooking_x64.dll");
                IntPtr paramsPtr = IntPtr.Add(pxCook, 8);
                string ps = "";
                for (int i = 0; i < 0x30; i += 4)
                    ps += Marshal.ReadInt32(paramsPtr, i).ToString("X8") + " ";
                log("physprobe: PX_COOK2 params(+" + "8) = " + ps);
                string pf = "";
                for (int i = 0; i < 0x30; i += 4)
                    pf += BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(paramsPtr, i)), 0).ToString("0.###") + " ";
                log("physprobe: PX_COOK2 params floats = " + pf);
                int pureOk = 0;
                if (cookBase != IntPtr.Zero)
                {
                    var pure = Fn<Bool1Fn>(IntPtr.Add(cookBase, 0x7E40));
                    pureOk = pure(desc);
                    log("physprobe: PX_COOK2 validator(0x7E40) -> " + pureOk);
                }
                if (pureOk != 0)
                {
                    s_pxStreamLen = 0;
                    IntPtr stream = MakePxStream();
                    var cook = Fn<Bool2Fn>(Vt(pxCook, 4));
                    int r = cook(pxCook, desc, stream);
                    log("physprobe: PX_COOK2 cook(vt4) -> " + r + " streamBytes=" + s_pxStreamLen);
                    if (s_pxStreamLen >= 12)
                    {
                        string hdr = "";
                        for (int i = 0; i < 12; i++) hdr += s_pxStreamBuf[i].ToString("X2");
                        log("physprobe: PX_COOK2 cooked header=" + hdr);
                    }
                }
                else
                {
                    log("physprobe: PX_COOK2 desc rejected; cook skipped (crash-safe)");
                }
            }
            catch (Exception e) { log("physprobe: PX_COOK2 ex: " + e.Message); }
            finally { Marshal.FreeHGlobal(verts); Marshal.FreeHGlobal(tris); Marshal.FreeHGlobal(desc); }
        }
        // PhysX-direct step 5 (opt-in RC_PX_ACTOR2=1, test exe): actor->createShape
        // sweep (actor createShape attaches the shape automatically in PhysX 3.3).
        if (Environment.GetEnvironmentVariable("RC_PX_ACTOR2") == "1")
        {
            IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
            IntPtr pxPhysics = Marshal.ReadIntPtr(mgr, 0x38);
            PxCookOk(pxCook, pxPhysics, log);
            byte[] cooked = new byte[s_pxStreamLen];
            Array.Copy(s_pxStreamBuf, cooked, s_pxStreamLen);
            IntPtr mesh = Fn<Ptr2Fn>(Vt(pxPhysics, 8))(pxPhysics, MakePxInStream(cooked, cooked.Length));
            log("physprobe: PX_ACTOR2 mesh=0x" + mesh.ToInt64().ToString("X"));
            IntPtr xf = Marshal.AllocHGlobal(0x20);
            for (int i = 0; i < 8; i++) Marshal.WriteInt32(xf, i * 4, 0);
            Marshal.WriteInt32(xf, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
            IntPtr actor = Fn<Ptr2Fn>(Vt(pxPhysics, 6))(pxPhysics, xf);
            log("physprobe: PX_ACTOR2 actor=0x" + actor.ToInt64().ToString("X"));
            IntPtr mats = Marshal.AllocHGlobal(16 * 8);
            var matFn = Fn<MatFn>(Vt(pxPhysics, 24));
            IntPtr matRet = matFn(pxPhysics, mats, 16, 0);
            log("physprobe: PX_ACTOR2 materials ret=" + matRet.ToInt64() +
                " first=0x" + Marshal.ReadIntPtr(mats).ToInt64().ToString("X"));
            IntPtr geom = Marshal.AllocHGlobal(0x50);
            for (int i = 0; i < 0x50 / 4; i++) Marshal.WriteInt32(geom, i * 4, 0);
            Marshal.WriteInt32(geom, 0x00, 5);
            Marshal.WriteInt32(geom, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
            Marshal.WriteInt32(geom, 0x14, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
            Marshal.WriteInt32(geom, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
            Marshal.WriteInt32(geom, 0x2C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
            Marshal.WriteIntPtr(geom, 0x30, mesh);
            string li = Environment.GetEnvironmentVariable("RC_PX_ACTOR_IDX");
            if (Environment.GetEnvironmentVariable("RC_PX_AVT") == "1")
            {
                IntPtr pxBase = GetModuleHandleA("PhysX3_x64.dll");
                long b = pxBase.ToInt64();
                IntPtr vtbl = Marshal.ReadIntPtr(actor);
                log("physprobe: PX_AVT actor vtbl=0x" + vtbl.ToInt64().ToString("X"));
                for (int k = 0; k < 60; k++)
                {
                    long e = Marshal.ReadIntPtr(vtbl, k * 8).ToInt64();
                    log("physprobe: PX_AVT actor vt[" + k + "] rva=0x" + (e - b).ToString("X"));
                }
            }
            foreach (string tok in li.Split(','))
            {
                int k = int.Parse(tok);
                log("physprobe: PX_ACTOR2 try vt[" + k + "]");
                IntPtr s = Fn<ShapeFn>(Vt(actor, k))(actor, geom, mats, 1, IntPtr.Zero, 3);
                log("physprobe: PX_ACTOR2 vt[" + k + "] =0x" + s.ToInt64().ToString("X"));
            }
        }
        // shape from the cooked mesh. Indices via RC_PX_RIGID_IDX / RC_PX_SHAPE_IDX.
        // PhysX-direct step 6 (opt-in RC_PX_QUERY=1, test exe): PxMeshQuery midphase
        // exports of PhysX3Common_x64.dll - capsule vs our cooked mesh, no scene.
        if (Environment.GetEnvironmentVariable("RC_PX_QUERY") == "1")
        {
            IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
            IntPtr pxPhysics = Marshal.ReadIntPtr(mgr, 0x38);
            PxCookOk(pxCook, pxPhysics, log);
            byte[] cooked = new byte[s_pxStreamLen];
            Array.Copy(s_pxStreamBuf, cooked, s_pxStreamLen);
            IntPtr mesh = Fn<Ptr2Fn>(Vt(pxPhysics, 8))(pxPhysics, MakePxInStream(cooked, cooked.Length));
            log("physprobe: PX_QUERY mesh=0x" + mesh.ToInt64().ToString("X"));
            IntPtr geom = Marshal.AllocHGlobal(0x50);
            for (int i = 0; i < 0x50 / 4; i++) Marshal.WriteInt32(geom, i * 4, 0);
            Marshal.WriteInt32(geom, 0x00, 5);   // eTRIANGLEMESH
            Marshal.WriteInt32(geom, 0x04, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // scale.x
            Marshal.WriteInt32(geom, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // scale.y
            Marshal.WriteInt32(geom, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // scale.z
            Marshal.WriteInt32(geom, 0x1C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // quat.w
            Marshal.WriteIntPtr(geom, 0x28, mesh);  // mesh ptr (authoritative: read at +0x28 in getTriangle)
            IntPtr pose = Marshal.AllocHGlobal(0x20);
            for (int i = 0; i < 8; i++) Marshal.WriteInt32(pose, i * 4, 0);
            Marshal.WriteInt32(pose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
            IntPtr hCommon = GetModuleHandleA("PhysX3Common_x64.dll");
            IntPtr pGetTri = GetProcAddress(hCommon, "?getTriangle@PxMeshQuery@physx@@SAXAEBVPxTriangleMeshGeometry@2@AEBVPxTransform@2@IAEAVPxTriangle@2@PEAI3@Z");
            IntPtr pOverlap = GetProcAddress(hCommon, "?findOverlapTriangleMesh@PxMeshQuery@physx@@SAIAEBVPxGeometry@2@AEBVPxTransform@2@AEBVPxTriangleMeshGeometry@2@1PEAIIIAEA_N@Z");
            log("physprobe: PX_QUERY exports getTri=0x" + pGetTri.ToInt64().ToString("X") + " overlap=0x" + pOverlap.ToInt64().ToString("X"));
            if (pOverlap != IntPtr.Zero)
            {
                IntPtr cap = Marshal.AllocHGlobal(0x10);
                Marshal.WriteInt32(cap, 0x00, 2);
                Marshal.WriteInt32(cap, 0x04, BitConverter.ToInt32(BitConverter.GetBytes(17f), 0));
                Marshal.WriteInt32(cap, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(41f), 0));
                IntPtr capPose = Marshal.AllocHGlobal(0x20);
                for (int i = 0; i < 8; i++) Marshal.WriteInt32(capPose, i * 4, 0);
                Marshal.WriteInt32(capPose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                Marshal.WriteInt32(capPose, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(100f), 0));
                Marshal.WriteInt32(capPose, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(100f), 0));
                IntPtr results = Marshal.AllocHGlobal(64 * 4);
                IntPtr overflow = Marshal.AllocHGlobal(4);
                uint cnt = Fn<FindOverlapFn>(pOverlap)(cap, capPose, geom, pose, results, 64, 0, overflow);
                log("physprobe: PX_QUERY capsule overlap count=" + cnt + " overflow=" + Marshal.ReadInt32(overflow));
                string ids = "";
                for (int i = 0; i < cnt && i < 12; i++) ids += Marshal.ReadInt32(results, i * 4) + " ";
                log("physprobe: PX_QUERY overlap triangles = " + ids);
            }
            if (pGetTri != IntPtr.Zero)
            {
                IntPtr tri = Marshal.AllocHGlobal(0x30);
                IntPtr triIdx = Marshal.AllocHGlobal(12);
                for (int i = 0; i < 0x30 / 4; i++) Marshal.WriteInt32(tri, i * 4, 0);
                for (int i = 0; i < 3; i++) Marshal.WriteInt32(triIdx, i * 4, 0);
                Fn<GetTriFn>(pGetTri)(geom, pose, 0, tri, triIdx);
                string vs = "";
                for (int v = 0; v < 3; v++)
                    vs += "(" + BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(tri, v * 12 + 0)), 0).ToString("0.#") + "," +
                          BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(tri, v * 12 + 4)), 0).ToString("0.#") + "," +
                          BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(tri, v * 12 + 8)), 0).ToString("0.#") + ") ";
                log("physprobe: PX_QUERY triangle0 = " + vs + " idx=" +
                    Marshal.ReadInt32(triIdx) + "," + Marshal.ReadInt32(triIdx, 4) + "," + Marshal.ReadInt32(triIdx, 8));
            }
        }
        if (Environment.GetEnvironmentVariable("RC_PX_MK") == "1")
        {
            IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
            IntPtr pxPhysics = Marshal.ReadIntPtr(mgr, 0x38);
            PxCookOk(pxCook, pxPhysics, log);
            byte[] cooked = new byte[s_pxStreamLen];
            Array.Copy(s_pxStreamBuf, cooked, s_pxStreamLen);
            IntPtr mesh = Fn<Ptr2Fn>(Vt(pxPhysics, 8))(pxPhysics, MakePxInStream(cooked, cooked.Length));
            log("physprobe: PX_MK mesh=0x" + mesh.ToInt64().ToString("X"));
            IntPtr xf = Marshal.AllocHGlobal(0x20);
            for (int i = 0; i < 8; i++) Marshal.WriteInt32(xf, i * 4, 0);
            Marshal.WriteInt32(xf, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // quat w
            int rigidIdx = 6;
            string ri = Environment.GetEnvironmentVariable("RC_PX_RIGID_IDX");
            if (ri != null) rigidIdx = int.Parse(ri);
            IntPtr actor = Fn<Ptr2Fn>(Vt(pxPhysics, rigidIdx))(pxPhysics, xf);
            log("physprobe: PX_MK rigid(vt[" + rigidIdx + "]) actor=0x" + actor.ToInt64().ToString("X"));
            IntPtr geom = Marshal.AllocHGlobal(0x50);
            for (int i = 0; i < 0x50 / 4; i++) Marshal.WriteInt32(geom, i * 4, 0);
            Marshal.WriteInt32(geom, 0x00, 5);   // eTRIANGLEMESH
            Marshal.WriteInt32(geom, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // scale.x
            Marshal.WriteInt32(geom, 0x14, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // scale.y
            Marshal.WriteInt32(geom, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // scale.z
            Marshal.WriteInt32(geom, 0x2C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0)); // quat.w
            Marshal.WriteIntPtr(geom, 0x30, mesh);
            IntPtr mats = Marshal.AllocHGlobal(16 * 8);
            IntPtr matCount = Fn<MatFn>(Vt(pxPhysics, 24))(pxPhysics, mats, 16, 0);
            log("physprobe: PX_MK materials=" + matCount.ToInt64() + " first=0x" + Marshal.ReadIntPtr(mats).ToInt64().ToString("X"));
            int shapeIdx = 9;
            IntPtr lastShape = IntPtr.Zero;
            string si = Environment.GetEnvironmentVariable("RC_PX_SHAPE_IDX");
            if (si != null && si.Contains(","))
            {
                foreach (string tok in si.Split(','))
                {
                    int k = int.Parse(tok);
                    log("physprobe: PX_MK shape try vt[" + k + "]");
                    IntPtr s = Fn<ShapeFn>(Vt(pxPhysics, k))(pxPhysics, geom, mats, 1, IntPtr.Zero, 3);
                    log("physprobe: PX_MK shape vt[" + k + "] =0x" + s.ToInt64().ToString("X"));
                    if (s.ToInt64() > 0x10000)
                    {
                        lastShape = s;
                        IntPtr svt = Marshal.ReadIntPtr(s);
                        foreach (System.Diagnostics.ProcessModule m in System.Diagnostics.Process.GetCurrentProcess().Modules)
                        {
                            long lo = m.BaseAddress.ToInt64(), hi = lo + m.ModuleMemorySize;
                            if (svt.ToInt64() >= lo && svt.ToInt64() < hi)
                            {
                                log("physprobe: PX_MK vt[" + k + "] obj vt in " + m.ModuleName + " +0x" + (svt.ToInt64() - lo).ToString("X"));
                                IntPtr namePtr = Fn<Ptr1Fn>(Vt(s, 1))(s);
                                string cn = "";
                                if (namePtr != IntPtr.Zero)
                                    for (int i = 0; i < 48; i++)
                                    {
                                        byte b = Marshal.ReadByte(namePtr, i);
                                        if (b == 0) break;
                                        cn += (char)b;
                                    }
                                log("physprobe: PX_MK vt[" + k + "] obj class = '" + cn + "'");
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                if (si != null) shapeIdx = int.Parse(si);
                var shFn = Fn<ShapeFn>(Vt(pxPhysics, shapeIdx));
                IntPtr shape = shFn(pxPhysics, geom, mats, 1, IntPtr.Zero, 3);
                log("physprobe: PX_MK shape(vt[" + shapeIdx + "]) =0x" + shape.ToInt64().ToString("X"));
                if (shape.ToInt64() > 0x10000) lastShape = shape;
            }
            if (lastShape != IntPtr.Zero)
            {
                IntPtr pxBase = GetModuleHandleA("PhysX3_x64.dll");
                IntPtr svt = Marshal.ReadIntPtr(lastShape);
                log("physprobe: PX_MK shape vt rva=0x" + (svt.ToInt64() - pxBase.ToInt64()).ToString("X"));
                foreach (System.Diagnostics.ProcessModule m in System.Diagnostics.Process.GetCurrentProcess().Modules)
                {
                    long lo = m.BaseAddress.ToInt64(), hi = lo + m.ModuleMemorySize;
                    if (svt.ToInt64() >= lo && svt.ToInt64() < hi)
                        log("physprobe: PX_MK shape vt in " + m.ModuleName + " +0x" + (svt.ToInt64() - lo).ToString("X"));
                }
                if (pxBase != IntPtr.Zero)
                {
                    IntPtr namePtr = Fn<Ptr1Fn>(Vt(lastShape, 1))(lastShape);
                    string cn = "";
                    if (namePtr != IntPtr.Zero)
                        for (int i = 0; i < 48; i++)
                        {
                            byte b = Marshal.ReadByte(namePtr, i);
                            if (b == 0) break;
                            cn += (char)b;
                        }
                    log("physprobe: PX_MK shape class = '" + cn + "'");
                }
                int att = Fn<Bool2Fn>(Vt(actor, 6))(actor, lastShape, IntPtr.Zero);
                log("physprobe: PX_MK attachShape(vt6) -> " + att);
            }
        }
        // pxPhysics::createTriangleMesh(PxInputStream&) by sweeping vtable entries
        // with the cooked mesh stream. RC_PX_MESH_IDX restricts the sweep.
        if (Environment.GetEnvironmentVariable("RC_PX_MESH") == "1")
        {
            IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
            IntPtr pxPhysics = Marshal.ReadIntPtr(mgr, 0x38);
            PxCookOk(pxCook, pxPhysics, log);
            byte[] cooked = new byte[s_pxStreamLen];
            Array.Copy(s_pxStreamBuf, cooked, s_pxStreamLen);
            string only = Environment.GetEnvironmentVariable("RC_PX_MESH_IDX");
            for (int idx = 0; idx < 45; idx++)
            {
                if (only != null && !("," + only + ",").Contains("," + idx + ",")) continue;
                IntPtr inStream = MakePxInStream(cooked, cooked.Length);
                log("physprobe: PX_MESH try vt[" + idx + "] rva=" + Vt(pxPhysics, idx).ToString("X"));
                var fn = Fn<Ptr2Fn>(Vt(pxPhysics, idx));
                IntPtr ret = fn(pxPhysics, inStream);
                log("physprobe: PX_MESH vt[" + idx + "] ret=0x" + ret.ToInt64().ToString("X") +
                    " reads=" + s_readCalls + " consumed=" + s_pxInPos + "/" + s_pxInLen);
            }
        }

        // through the engine's own loader and compare vertex/triangle counts.
        if (Environment.GetEnvironmentVariable("RC_PX_SWEEP") == "1")
        {
            string binPath = Environment.GetEnvironmentVariable("RC_PX_SWEEP_BIN");
            if (string.IsNullOrEmpty(binPath))
                binPath = @"C:\SeasunGame\MovieEditor\bin64\collision_data\龙门寻宝_structure_collision.bin";
            try
            {
                using (var br = new System.IO.BinaryReader(System.IO.File.OpenRead(binPath)))
                {
                    br.ReadUInt32(); br.ReadInt32();
                    int meshCount = br.ReadInt32(); br.ReadInt32();
                    int[] vcs = new int[meshCount]; int[] tcs = new int[meshCount];
                    for (int i = 0; i < meshCount; i++)
                    {
                        vcs[i] = br.ReadInt32(); tcs[i] = br.ReadInt32();
                        br.BaseStream.Seek((long)vcs[i] * 12 + (long)tcs[i] * 12, System.IO.SeekOrigin.Current);
                    }
                    var paths = new string[meshCount];
                    foreach (string ln in System.IO.File.ReadAllLines(binPath + ".meshes.txt"))
                    {
                        string[] parts = ln.Split('\t');
                        if (parts.Length >= 2) { int k; if (int.TryParse(parts[0], out k) && k >= 0 && k < meshCount) paths[k] = parts[1]; }
                    }
                    var loadMesh = Fn<LoadMeshFn>(h + 0x3B3F0);
                    int matched = 0, mismatched = 0, failed = 0, tested = 0;
                    for (int i = 0; i < meshCount; i++)
                    {
                        string mp = paths[i];
                        if (string.IsNullOrEmpty(mp)) continue;
                        IntPtr pb = Marshal.StringToHGlobalAnsi(mp);
                        try
                        {
                            IntPtr data = loadMesh(pb);
                            tested++;
                            if (data == IntPtr.Zero) { failed++; continue; }
                            int ev = Marshal.ReadInt32(data); int et = Marshal.ReadInt32(data, 4);
                            if (ev == vcs[i] && et == tcs[i]) matched++;
                            else
                            {
                                mismatched++;
                                if (mismatched <= 20)
                                    log(string.Format("physprobe: PX_SWEEP MISMATCH mesh {0} {1} engine={2}/{3} bin={4}/{5}",
                                        i, mp, ev, et, vcs[i], tcs[i]));
                            }
                        }
                        finally { Marshal.FreeHGlobal(pb); }
                    }
                    log(string.Format("physprobe: PX_SWEEP tested={0} matched={1} mismatched={2} failed={3}",
                        tested, matched, mismatched, failed));
                }
            }
            catch (Exception e) { log("physprobe: PX_SWEEP ex: " + e.Message); }
        }

        // Stage-1 v3 (opt-in RC_PX_LOAD=1): the engine's own mesh-file loader
        // (PhysicsEngineX64 0x3B3F0(char* path) -> mesh file data, via the
        // engine FS/paks; logs 'Mesh file "%s" not exist' and returns null when
        // missing - low risk). Compare its geometry counts with our bake.
        if (Environment.GetEnvironmentVariable("RC_PX_LOAD") == "1")
        {
            string[] meshPaths = new string[]
            {
                "data/source/maps_source/建筑配套/pj_玉门草棚001_hd.mesh",
                "data/source/maps_source/小物件/室内/wj_木堆001_hd.mesh",
            };
            var loadMesh = Fn<LoadMeshFn>(h + 0x3B3F0);
            foreach (string mp in meshPaths)
            {
                IntPtr pb = Marshal.StringToHGlobalAnsi(mp);
                try
                {
                    IntPtr data = loadMesh(pb);
                    if (data == IntPtr.Zero)
                    {
                        log("physprobe: PX_LOAD " + mp + " -> null");
                        continue;
                    }
                    log(string.Format("physprobe: PX_LOAD {0} -> data={1} f0={2} f4={3} p10={4} p88={5}",
                        mp, Hex(data), Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4),
                        Hex(Marshal.ReadIntPtr(data, 0x10)), Hex(Marshal.ReadIntPtr(data, 0x88))));
                }
                finally { Marshal.FreeHGlobal(pb); }
            }
        }

        // PhysX-direct step 7 (opt-in RC_PX_FIELD=1): cook the real world-baked map
        // bin with the engine cooker, then capsule-overlap vertical scans at the
        // field spots via PxMeshQuery - the engine-side A/B reference.
        if (Environment.GetEnvironmentVariable("RC_PX_FIELD") == "1")
        {
            try
            {
                string binPath = Environment.GetEnvironmentVariable("RC_PX_FIELD_BIN");
                if (string.IsNullOrEmpty(binPath))
                    binPath = @"C:\SeasunGame\MovieEditor\bin64\collision_data\龙门寻宝_structure_collision.bin";
                long totalV = 0, totalT = 0;
                using (var br = new System.IO.BinaryReader(System.IO.File.OpenRead(binPath)))
                {
                    br.ReadUInt32(); br.ReadInt32();
                    int meshCount = br.ReadInt32(); br.ReadInt32();
                    for (int i = 0; i < meshCount; i++)
                    {
                        int vc = br.ReadInt32(), tc = br.ReadInt32();
                        totalV += vc; totalT += tc;
                        br.BaseStream.Seek((long)vc * 12 + (long)tc * 12, System.IO.SeekOrigin.Current);
                    }
                }
                log("physprobe: PX_FIELD bin meshes totalV=" + totalV + " totalT=" + totalT + " (" + binPath + ")");
                IntPtr verts = Marshal.AllocHGlobal((int)(totalV * 12));
                IntPtr tris = Marshal.AllocHGlobal((int)(totalT * 12));
                int vo = 0, to = 0, baseIdx = 0;
                using (var br = new System.IO.BinaryReader(System.IO.File.OpenRead(binPath)))
                {
                    br.ReadUInt32(); br.ReadInt32();
                    int meshCount = br.ReadInt32(); br.ReadInt32();
                    for (int i = 0; i < meshCount; i++)
                    {
                        int vc = br.ReadInt32(), tc = br.ReadInt32();
                        byte[] vb = br.ReadBytes(vc * 12);
                        byte[] tb = br.ReadBytes(tc * 12);
                        Marshal.Copy(vb, 0, IntPtr.Add(verts, vo * 12), vb.Length);
                        for (int k = 0; k < tc * 3; k++)
                        {
                            int idx = BitConverter.ToInt32(tb, k * 4);
                            Array.Copy(BitConverter.GetBytes(idx + baseIdx), 0, tb, k * 4, 4);
                        }
                        Marshal.Copy(tb, 0, IntPtr.Add(tris, to * 12), tb.Length);
                        vo += vc; to += tc; baseIdx += vc;
                    }
                }
                s_pxStreamBuf = new byte[1 << 28];
                IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
                IntPtr pxPhysics = Marshal.ReadIntPtr(mgr, 0x38);
                IntPtr desc = Marshal.AllocHGlobal(0x60);
                for (int i = 0; i < 0x60 / 4; i++) Marshal.WriteInt32(desc, i * 4, 0);
                Marshal.WriteInt32(desc, 0x00, 12);
                Marshal.WriteIntPtr(desc, 0x08, verts);
                Marshal.WriteInt32(desc, 0x10, (int)totalV);
                Marshal.WriteInt32(desc, 0x18, 12);
                Marshal.WriteIntPtr(desc, 0x20, tris);
                Marshal.WriteInt32(desc, 0x28, (int)totalT);
                Marshal.WriteInt32(desc, 0x48, unchecked((int)0x3A83126F));
                s_pxStreamLen = 0;
                IntPtr ostream = MakePxStream();
                int cr = Fn<Bool2Fn>(Vt(pxCook, 4))(pxCook, desc, ostream);
                log("physprobe: PX_FIELD cook=" + cr + " cookedBytes=" + s_pxStreamLen);
                byte[] cooked = new byte[s_pxStreamLen];
                Array.Copy(s_pxStreamBuf, cooked, s_pxStreamLen);
                IntPtr mesh = Fn<Ptr2Fn>(Vt(pxPhysics, 8))(pxPhysics, MakePxInStream(cooked, cooked.Length));
                log("physprobe: PX_FIELD mesh=0x" + mesh.ToInt64().ToString("X"));
                if (mesh == IntPtr.Zero) { log("physprobe: PX_FIELD mesh null; scan skipped"); }
                else
                {
                IntPtr geom = Marshal.AllocHGlobal(0x50);
                for (int i = 0; i < 0x50 / 4; i++) Marshal.WriteInt32(geom, i * 4, 0);
                Marshal.WriteInt32(geom, 0x00, 5);
                Marshal.WriteInt32(geom, 0x04, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                Marshal.WriteInt32(geom, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                Marshal.WriteInt32(geom, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                Marshal.WriteInt32(geom, 0x1C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                Marshal.WriteIntPtr(geom, 0x28, mesh);
                IntPtr mpose = Marshal.AllocHGlobal(0x20);
                for (int i = 0; i < 8; i++) Marshal.WriteInt32(mpose, i * 4, 0);
                Marshal.WriteInt32(mpose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                IntPtr hCommon = GetModuleHandleA("PhysX3Common_x64.dll");
                IntPtr pOverlap = GetProcAddress(hCommon, "?findOverlapTriangleMesh@PxMeshQuery@physx@@SAIAEBVPxGeometry@2@AEBVPxTransform@2@AEBVPxTriangleMeshGeometry@2@1PEAIIIAEA_N@Z");
                IntPtr cap = Marshal.AllocHGlobal(0x10);
                Marshal.WriteInt32(cap, 0x00, 2);
                Marshal.WriteInt32(cap, 0x04, BitConverter.ToInt32(BitConverter.GetBytes(17f), 0));
                Marshal.WriteInt32(cap, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(41f), 0));
                IntPtr capPose = Marshal.AllocHGlobal(0x20);
                IntPtr results = Marshal.AllocHGlobal(256 * 4);
                IntPtr overflow = Marshal.AllocHGlobal(4);
                IntPtr pGetTri2 = GetProcAddress(hCommon, "?getTriangle@PxMeshQuery@physx@@SAXAEBVPxTriangleMeshGeometry@2@AEBVPxTransform@2@IAEAVPxTriangle@2@PEAI3@Z");
                if (pGetTri2 != IntPtr.Zero)
                {
                    IntPtr tri = Marshal.AllocHGlobal(0x30);
                    IntPtr triIdx = Marshal.AllocHGlobal(12);
                    Fn<GetTriFn>(pGetTri2)(geom, mpose, 0, tri, triIdx);
                    float cx2 = 0, cy2 = 0, cz2 = 0;
                    float[] tv = new float[9];
                    for (int v = 0; v < 3; v++)
                        for (int c = 0; c < 3; c++)
                        {
                            tv[v * 3 + c] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(tri, v * 12 + c * 4)), 0);
                            if (c == 0) cx2 += tv[v * 3] / 3f;
                            if (c == 1) cy2 += tv[v * 3 + 1] / 3f;
                            if (c == 2) cz2 += tv[v * 3 + 2] / 3f;
                        }
                    log(string.Format("physprobe: PX_FIELD tri0=({0:0.#},{1:0.#},{2:0.#}) ({3:0.#},{4:0.#},{5:0.#}) ({6:0.#},{7:0.#},{8:0.#})",
                        tv[0], tv[1], tv[2], tv[3], tv[4], tv[5], tv[6], tv[7], tv[8]));
                    for (int i = 0; i < 8; i++) Marshal.WriteInt32(capPose, i * 4, 0);
                    Marshal.WriteInt32(capPose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                    Marshal.WriteInt32(capPose, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(cx2), 0));
                    Marshal.WriteInt32(capPose, 0x14, BitConverter.ToInt32(BitConverter.GetBytes(cy2), 0));
                    Marshal.WriteInt32(capPose, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(cz2), 0));
                    uint sc = Fn<FindOverlapFn>(pOverlap)(cap, capPose, geom, mpose, results, 256, 0, overflow);
                    log("physprobe: PX_FIELD selftest centroid overlap count=" + sc);
                }
                float[][] spots = new float[][] {
                    new float[] { 23334f, 24224f, 1500f, 0f },   // spawn: scan y 0..1500
                    new float[] { 18915f, 36850f, 1500f, 0f },   // wall
                    new float[] { 20450f, 31000f, 1500f, 0f } }; // pile
                for (int s = 0; s < spots.Length; s++)
                {
                    float x = spots[s][0], z = spots[s][1];
                    int prev = -1;
                    string trans = "";
                    for (int y = 0; y <= 1500; y += 25)
                    {
                        for (int i = 0; i < 8; i++) Marshal.WriteInt32(capPose, i * 4, 0);
                        Marshal.WriteInt32(capPose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                        Marshal.WriteInt32(capPose, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(x), 0));
                        Marshal.WriteInt32(capPose, 0x14, BitConverter.ToInt32(BitConverter.GetBytes((float)y), 0));
                        Marshal.WriteInt32(capPose, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(z), 0));
                        uint cnt = Fn<FindOverlapFn>(pOverlap)(cap, capPose, geom, mpose, results, 256, 0, overflow);
                        int c = (int)cnt;
                        if (c != prev) { trans += " y" + y + ":" + c; prev = c; }
                    }
                    log("physprobe: PX_FIELD spot" + s + " (" + x + "," + z + ") transitions:" + trans);
                }
                }
            }
            catch (Exception e) { log("physprobe: PX_FIELD ex: " + e.Message); }
        }
        // PhysX-direct step 8 (opt-in RC_PX_FIELD2=1): like RC_PX_FIELD but the
        // bin is parsed fully (locals + per-instance l2w + oflags rule) and the
        // world-baked instance triangles are cooked - apples-to-apples with the
        // solver's world placement (the step-7 cook used raw local verts and is
        // invalid for placement). Grid mode via RC_PX_GRID="x0,z0,x1,z1[,pitch,yStep]".
        if (Environment.GetEnvironmentVariable("RC_PX_FIELD2") == "1")
        {
            try
            {
                System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
                string binPath = Environment.GetEnvironmentVariable("RC_PX_FIELD_BIN");
                if (string.IsNullOrEmpty(binPath))
                    binPath = @"C:\SeasunGame\MovieEditor\bin64\collision_data\龙门寻宝_夜晚_structure_collision.bin";
                string gridSpec = Environment.GetEnvironmentVariable("RC_PX_GRID");
                float gx0 = 0, gz0 = 0, gx1 = 0, gz1 = 0, gpitch = 200f;
                int gyStep = 50, gyMax = 1500;
                bool grid = !string.IsNullOrEmpty(gridSpec);
                if (grid)
                {
                    string[] gp = gridSpec.Split(',');
                    gx0 = float.Parse(gp[0], ci); gz0 = float.Parse(gp[1], ci);
                    gx1 = float.Parse(gp[2], ci); gz1 = float.Parse(gp[3], ci);
                    if (gp.Length > 4) gpitch = float.Parse(gp[4], ci);
                    if (gp.Length > 5) gyStep = int.Parse(gp[5], ci);
                    if (gp.Length > 6) gyMax = int.Parse(gp[6], ci);
                }
                System.IO.BinaryReader br = new System.IO.BinaryReader(System.IO.File.OpenRead(binPath));
                br.ReadUInt32();
                int ver = br.ReadInt32();
                int meshCount = br.ReadInt32();
                int instCount = br.ReadInt32();
                log("physprobe: PX_FIELD2 v" + ver + " meshes=" + meshCount + " insts=" + instCount);
                float[][] mv = new float[meshCount][];
                int[][] mt = new int[meshCount][];
                for (int i = 0; i < meshCount; i++)
                {
                    int vc = br.ReadInt32(), tc = br.ReadInt32();
                    float[] vv = new float[vc * 3];
                    for (int k = 0; k < vc * 3; k++) vv[k] = br.ReadSingle();
                    int[] tt = new int[tc * 3];
                    for (int k = 0; k < tc * 3; k++) tt[k] = br.ReadInt32();
                    mv[i] = vv; mt[i] = tt;
                }
                byte[] oflags = null;
                string ofp = binPath + ".oflags";
                if (System.IO.File.Exists(ofp))
                {
                    byte[] raw = System.IO.File.ReadAllBytes(ofp);
                    if (raw.Length >= 8 + meshCount && BitConverter.ToUInt32(raw, 0) == 0x474C464F)
                        oflags = raw;
                }
                float rx0, rz0, rx1, rz1;
                if (grid) { rx0 = gx0 - 120; rz0 = gz0 - 120; rx1 = gx1 + 120; rz1 = gz1 + 120; }
                else { rx0 = 18915 - 700; rz0 = 24224 - 700; rx1 = 23334 + 700; rz1 = 36850 + 700; }
                System.Collections.Generic.List<float> wv = new System.Collections.Generic.List<float>(1 << 20);
                System.Collections.Generic.List<int> wt = new System.Collections.Generic.List<int>(1 << 20);
                int used = 0, skipped = 0;
                for (int i = 0; i < instCount; i++)
                {
                    int mi = br.ReadInt32();
                    float[] m = new float[16];
                    for (int k = 0; k < 16; k++) m[k] = br.ReadSingle();
                    br.ReadInt32();
                    float bx0 = br.ReadSingle(), by0 = br.ReadSingle(), bz0 = br.ReadSingle();
                    float bx1 = br.ReadSingle(), by1 = br.ReadSingle(), bz1 = br.ReadSingle();
                    if (mi < 0 || mi >= meshCount) continue;
                    if (oflags != null
                        && ((oflags[8 + mi] & 0x01) == 0 || (oflags[8 + mi] & 0x02) == 0)
                        && (oflags[8 + mi] & 0x20) == 0)
                    { skipped++; continue; }
                    if (bx1 < rx0 || bx0 > rx1 || bz1 < rz0 || bz0 > rz1) continue;
                    float[] vv = mv[mi];
                    int voff = wv.Count / 3;
                    for (int k = 0; k < vv.Length; k += 3)
                    {
                        float lx = vv[k], ly = vv[k + 1], lz = vv[k + 2];
                        wv.Add(lx * m[0] + ly * m[4] + lz * m[8] + m[12]);
                        wv.Add(lx * m[1] + ly * m[5] + lz * m[9] + m[13]);
                        wv.Add(lx * m[2] + ly * m[6] + lz * m[10] + m[14]);
                    }
                    int[] tt = mt[mi];
                    for (int k = 0; k < tt.Length; k++) wt.Add(tt[k] + voff);
                    used++;
                }
                br.Close();
                log("physprobe: PX_FIELD2 baked insts=" + used + " (oflagsSkipped=" + skipped +
                    ") verts=" + (wv.Count / 3) + " tris=" + (wt.Count / 3));
                int nv2 = wv.Count / 3, nt2 = wt.Count / 3;
                if (nt2 < 3) { log("physprobe: PX_FIELD2 nothing to cook"); }
                else
                {
                    IntPtr verts2 = Marshal.AllocHGlobal(nv2 * 12);
                    float[] wa = wv.ToArray();
                    Marshal.Copy(wa, 0, verts2, wa.Length);
                    IntPtr tris2 = Marshal.AllocHGlobal(nt2 * 12);
                    int[] ia = wt.ToArray();
                    Marshal.Copy(ia, 0, tris2, ia.Length);
                    s_pxStreamBuf = new byte[1 << 28];
                    IntPtr pxCook = Marshal.ReadIntPtr(mgr, 0x40);
                    IntPtr pxPhysics = Marshal.ReadIntPtr(mgr, 0x38);
                    IntPtr desc = Marshal.AllocHGlobal(0x60);
                    for (int i = 0; i < 0x60 / 4; i++) Marshal.WriteInt32(desc, i * 4, 0);
                    Marshal.WriteInt32(desc, 0x00, 12);
                    Marshal.WriteIntPtr(desc, 0x08, verts2);
                    Marshal.WriteInt32(desc, 0x10, nv2);
                    Marshal.WriteInt32(desc, 0x18, 12);
                    Marshal.WriteIntPtr(desc, 0x20, tris2);
                    Marshal.WriteInt32(desc, 0x28, nt2);
                    Marshal.WriteInt32(desc, 0x48, unchecked((int)0x3A83126F));
                    s_pxStreamLen = 0;
                    IntPtr ostream = MakePxStream();
                    int cr = Fn<Bool2Fn>(Vt(pxCook, 4))(pxCook, desc, ostream);
                    log("physprobe: PX_FIELD2 cook=" + cr + " cookedBytes=" + s_pxStreamLen);
                    byte[] cooked = new byte[s_pxStreamLen];
                    Array.Copy(s_pxStreamBuf, cooked, s_pxStreamLen);
                    IntPtr mesh = Fn<Ptr2Fn>(Vt(pxPhysics, 8))(pxPhysics, MakePxInStream(cooked, cooked.Length));
                    log("physprobe: PX_FIELD2 mesh=0x" + mesh.ToInt64().ToString("X"));
                    if (mesh != IntPtr.Zero)
                    {
                        IntPtr geom = Marshal.AllocHGlobal(0x50);
                        for (int i = 0; i < 0x50 / 4; i++) Marshal.WriteInt32(geom, i * 4, 0);
                        Marshal.WriteInt32(geom, 0x00, 5);
                        Marshal.WriteInt32(geom, 0x04, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                        Marshal.WriteInt32(geom, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                        Marshal.WriteInt32(geom, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                        Marshal.WriteInt32(geom, 0x1C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                        Marshal.WriteIntPtr(geom, 0x28, mesh);
                        IntPtr mpose = Marshal.AllocHGlobal(0x20);
                        for (int i = 0; i < 8; i++) Marshal.WriteInt32(mpose, i * 4, 0);
                        Marshal.WriteInt32(mpose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(1f), 0));
                        IntPtr hCommon = GetModuleHandleA("PhysX3Common_x64.dll");
                        IntPtr pOverlap = GetProcAddress(hCommon, "?findOverlapTriangleMesh@PxMeshQuery@physx@@SAIAEBVPxGeometry@2@AEBVPxTransform@2@AEBVPxTriangleMeshGeometry@2@1PEAIIIAEA_N@Z");
                        IntPtr cap = Marshal.AllocHGlobal(0x20);
                        Marshal.WriteInt32(cap, 0x00, 2);
                        Marshal.WriteInt32(cap, 0x04, BitConverter.ToInt32(BitConverter.GetBytes(17f), 0));
                        Marshal.WriteInt32(cap, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(41f), 0));
                        Marshal.WriteInt32(cap, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(17f), 0));
                        Marshal.WriteInt32(cap, 0x14, BitConverter.ToInt32(BitConverter.GetBytes(41f), 0));
                        IntPtr capPose = Marshal.AllocHGlobal(0x20);
                        IntPtr results = Marshal.AllocHGlobal(256 * 4);
                        IntPtr overflow = Marshal.AllocHGlobal(4);
                        if (grid)
                        {
                            for (float gx = gx0; gx <= gx1 + 0.5f; gx += gpitch)
                            {
                                for (float gz = gz0; gz <= gz1 + 0.5f; gz += gpitch)
                                {
                                    int prev = -1;
                                    string trans = "";
                                    for (int cy = 0; cy <= gyMax; cy += gyStep)
                                    {
                                        for (int i = 0; i < 8; i++) Marshal.WriteInt32(capPose, i * 4, 0);
                                        Marshal.WriteInt32(capPose, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                        Marshal.WriteInt32(capPose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                        Marshal.WriteInt32(capPose, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(gx), 0));
                                        Marshal.WriteInt32(capPose, 0x14, BitConverter.ToInt32(BitConverter.GetBytes((float)cy), 0));
                                        Marshal.WriteInt32(capPose, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(gz), 0));
                                        uint cnt = Fn<FindOverlapFn>(pOverlap)(cap, capPose, geom, mpose, results, 256, 0, overflow);
                                        int c = cnt > 0 ? 1 : 0;
                                        if (c != prev) { trans += " y" + cy + ":" + c; prev = c; }
                                    }
                                    log("PX_GRID " + (int)gx + " " + (int)gz + trans);
                                }
                            }
                        }
                        else
                        {
                            float[][] spots = new float[][] {
                                new float[] { 23334f, 24224f }, new float[] { 18915f, 36850f },
                                new float[] { 20450f, 31000f } };
                            for (int s = 0; s < spots.Length; s++)
                            {
                                float x = spots[s][0], z = spots[s][1];
                                int prev = -1;
                                string trans = "";
                                for (int cy = 0; cy <= gyMax; cy += gyStep)
                                {
                                    for (int i = 0; i < 8; i++) Marshal.WriteInt32(capPose, i * 4, 0);
                                    Marshal.WriteInt32(capPose, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                    Marshal.WriteInt32(capPose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                    Marshal.WriteInt32(capPose, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(x), 0));
                                    Marshal.WriteInt32(capPose, 0x14, BitConverter.ToInt32(BitConverter.GetBytes((float)cy), 0));
                                    Marshal.WriteInt32(capPose, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(z), 0));
                                    uint cnt = Fn<FindOverlapFn>(pOverlap)(cap, capPose, geom, mpose, results, 256, 0, overflow);
                                    int c = cnt > 0 ? 1 : 0;
                                    if (c != prev) { trans += " y" + cy + ":" + c; prev = c; }
                                }
                                log("physprobe: PX_FIELD2 spot" + s + " (" + x + "," + z + ") transitions:" + trans);
                            }
                        }
                        string sweepSpec = Environment.GetEnvironmentVariable("RC_PX_SWEEP3");
                        if (!string.IsNullOrEmpty(sweepSpec))
                        foreach (string specOne in sweepSpec.Split(';'))
                        {
                            string[] sp = specOne.Split(',');
                            float sx = float.Parse(sp[0], ci), sy = float.Parse(sp[1], ci), sz = float.Parse(sp[2], ci);
                            float sdx = float.Parse(sp[3], ci), sdy = float.Parse(sp[4], ci), sdz = float.Parse(sp[5], ci);
                            float sdist = float.Parse(sp[6], ci);
                            float slen = (float)Math.Sqrt(sdx * sdx + sdy * sdy + sdz * sdz);
                            if (slen < 1e-6f) slen = 1f;
                            sdx /= slen; sdy /= slen; sdz /= slen;
                            IntPtr fat = Marshal.AllocHGlobal(0x20);
                            int fatR = 24;
                            Marshal.WriteInt32(fat, 0x00, 2);
                            Marshal.WriteInt32(fat, 0x04, BitConverter.ToInt32(BitConverter.GetBytes((float)fatR), 0));
                            Marshal.WriteInt32(fat, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(41f), 0));
                            IntPtr fatPose = Marshal.AllocHGlobal(0x20);
                            IntPtr cand = Marshal.AllocHGlobal(256 * 4);
                            IntPtr ovf = Marshal.AllocHGlobal(4);
                            System.Collections.Generic.List<int> candSet = new System.Collections.Generic.List<int>(256);
                            for (int st = 0; st < 12; st++)
                            {
                                float tt = st / 11f;
                                for (int i = 0; i < 8; i++) Marshal.WriteInt32(fatPose, i * 4, 0);
                                Marshal.WriteInt32(fatPose, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                Marshal.WriteInt32(fatPose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                Marshal.WriteInt32(fatPose, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(sx + sdx * sdist * tt), 0));
                                Marshal.WriteInt32(fatPose, 0x14, BitConverter.ToInt32(BitConverter.GetBytes(sy + sdy * sdist * tt), 0));
                                Marshal.WriteInt32(fatPose, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(sz + sdz * sdist * tt), 0));
                                uint nck = Fn<FindOverlapFn>(pOverlap)(fat, fatPose, geom, mpose, cand, 256, 0, ovf);
                                for (int k = 0; k < nck && candSet.Count < 256; k++)
                                {
                                    int ti = Marshal.ReadInt32(cand, k * 4);
                                    if (!candSet.Contains(ti)) candSet.Add(ti);
                                }
                            }
                            uint nc = (uint)candSet.Count;
                            for (int k = 0; k < candSet.Count; k++) Marshal.WriteInt32(cand, k * 4, candSet[k]);
                            IntPtr triArr = Marshal.AllocHGlobal((int)nc * 36 + 36);
                            IntPtr oneTri = Marshal.AllocHGlobal(0x30);
                            IntPtr triIdx = Marshal.AllocHGlobal(12);
                            IntPtr pGetTri3 = GetProcAddress(hCommon, "?getTriangle@PxMeshQuery@physx@@SAXAEBVPxTriangleMeshGeometry@2@AEBVPxTransform@2@IAEAVPxTriangle@2@PEAI3@Z");
                            int kept = 0, filtered = 0;
                            for (int k = 0; k < nc; k++)
                            {
                                uint ti = (uint)Marshal.ReadInt32(cand, k * 4);
                                Fn<GetTriFn>(pGetTri3)(geom, mpose, ti, oneTri, triIdx);
                                float[] tv = new float[9];
                                bool ok = true;
                                for (int b = 0; b < 9; b++)
                                {
                                    tv[b] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(oneTri, b * 4)), 0);
                                    if (float.IsNaN(tv[b]) || float.IsInfinity(tv[b])) ok = false;
                                }
                                if (ok)
                                {
                                    float ux = tv[3] - tv[0], uy = tv[4] - tv[1], uz = tv[5] - tv[2];
                                    float vx = tv[6] - tv[0], vy = tv[7] - tv[1], vz = tv[8] - tv[2];
                                    float cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                                    float area2 = (float)Math.Sqrt(cx * cx + cy * cy + cz * cz);
                                    if (area2 < 1e-6f) ok = false;
                                }
                                if (!ok) { filtered++; continue; }
                                for (int b = 0; b < 9; b++)
                                    Marshal.WriteInt32(triArr, kept * 36 + b * 4, Marshal.ReadInt32(oneTri, b * 4));
                                kept++;
                            }
                            nc = (uint)kept;
                            if (sp.Length > 7 && sp[7] == "synth")
                            {
                                float wx = sx + sdx * sdist * 0.6f;
                                float wz = sz + sdz * sdist * 0.6f;
                                float wy = sy;
                                int o2 = 0;
                                int[,] vs = new int[,] {
                                    { BitConverter.ToInt32(BitConverter.GetBytes(wx), 0), BitConverter.ToInt32(BitConverter.GetBytes(wy - 60f), 0), BitConverter.ToInt32(BitConverter.GetBytes(wz - 40f), 0) },
                                    { BitConverter.ToInt32(BitConverter.GetBytes(wx), 0), BitConverter.ToInt32(BitConverter.GetBytes(wy + 60f), 0), BitConverter.ToInt32(BitConverter.GetBytes(wz - 40f), 0) },
                                    { BitConverter.ToInt32(BitConverter.GetBytes(wx), 0), BitConverter.ToInt32(BitConverter.GetBytes(wy), 0), BitConverter.ToInt32(BitConverter.GetBytes(wz + 40f), 0) } };
                                for (int v = 0; v < 3; v++)
                                    for (int c2 = 0; c2 < 3; c2++)
                                    { Marshal.WriteInt32(triArr, o2 * 4, vs[v, c2]); o2++; }
                                nc = 1;
                                log("physprobe: PX_SWEEP3 synth triangle at x=" + wx + " z=" + wz);
                            }
                            IntPtr sPose = Marshal.AllocHGlobal(0x20);
                            for (int i = 0; i < 8; i++) Marshal.WriteInt32(sPose, i * 4, 0);
                            Marshal.WriteInt32(sPose, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                            Marshal.WriteInt32(sPose, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                            Marshal.WriteInt32(sPose, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(sx), 0));
                            Marshal.WriteInt32(sPose, 0x14, BitConverter.ToInt32(BitConverter.GetBytes(sy), 0));
                            Marshal.WriteInt32(sPose, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(sz), 0));
                            IntPtr unit = Marshal.AllocHGlobal(12);
                            Marshal.WriteInt32(unit, 0, BitConverter.ToInt32(BitConverter.GetBytes(sdx), 0));
                            Marshal.WriteInt32(unit, 4, BitConverter.ToInt32(BitConverter.GetBytes(sdy), 0));
                            Marshal.WriteInt32(unit, 8, BitConverter.ToInt32(BitConverter.GetBytes(sdz), 0));
                            IntPtr hit = Marshal.AllocHGlobal(0x40);
                            for (int i = 0; i < 0x40 / 4; i++) Marshal.WriteInt32(hit, i * 4, 0);
                            IntPtr pSweep = GetProcAddress(hCommon, "?sweep@PxMeshQuery@physx@@SA_NAEBVPxVec3@2@MAEBVPxGeometry@2@AEBVPxTransform@2@IPEBVPxTriangle@2@AEAUPxSweepHit@2@V?$PxFlags@W4Enum@PxHitFlag@physx@@G@2@PEBIM_N@Z");
                            log("physprobe: PX_SWEEP3 spec=" + specOne + " candidates=" + nc + " filtered=" + filtered + " pSweep=0x" + pSweep.ToInt64().ToString("X"));
                            if (pSweep != IntPtr.Zero)
                            {
                                // PARKED (see proof 2026-10-02i): the actual-hit path
                                // crashes with map AND synthetic triangles, all flag
                                // variants and a caller-mimicked arg8 layout - the
                                // exported sweep needs internal state the standalone
                                // call does not provide (the in-penetration early-out
                                // is the only verified path). Diagnostic only.
                                log("physprobe: PX_SWEEP3 call skipped (hit path needs engine-internal state - proof 2026-10-02i)");
                            }
                        }
                        string posesSpec = Environment.GetEnvironmentVariable("RC_PX_POSES");
                        if (!string.IsNullOrEmpty(posesSpec))
                        {
                            IntPtr pOverlap2 = GetProcAddress(GetModuleHandleA("PhysX3Common_x64.dll"),
                                "?findOverlapTriangleMesh@PxMeshQuery@physx@@SAIAEBVPxGeometry@2@AEBVPxTransform@2@AEBVPxTriangleMeshGeometry@2@1PEAIIIAEA_N@Z");
                            IntPtr c2 = Marshal.AllocHGlobal(0x20);
                            Marshal.WriteInt32(c2, 0x00, 2);
                            Marshal.WriteInt32(c2, 0x04, BitConverter.ToInt32(BitConverter.GetBytes(17f), 0));
                            Marshal.WriteInt32(c2, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(41f), 0));
                            IntPtr cp = Marshal.AllocHGlobal(0x20);
                            IntPtr res2 = Marshal.AllocHGlobal(64 * 4);
                            IntPtr ov2 = Marshal.AllocHGlobal(4);
                            string[] poses = posesSpec.Split(';');
                            for (int pi = 0; pi < poses.Length; pi++)
                            {
                                string[] pc = poses[pi].Split(',');
                                if (pc.Length < 3) continue;
                                float qx = float.Parse(pc[0], ci), qy = float.Parse(pc[1], ci), qz = float.Parse(pc[2], ci);
                                for (int i = 0; i < 8; i++) Marshal.WriteInt32(cp, i * 4, 0);
                                Marshal.WriteInt32(cp, 0x08, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                Marshal.WriteInt32(cp, 0x0C, BitConverter.ToInt32(BitConverter.GetBytes(0.70710678f), 0));
                                Marshal.WriteInt32(cp, 0x10, BitConverter.ToInt32(BitConverter.GetBytes(qx), 0));
                                Marshal.WriteInt32(cp, 0x14, BitConverter.ToInt32(BitConverter.GetBytes(qy), 0));
                                Marshal.WriteInt32(cp, 0x18, BitConverter.ToInt32(BitConverter.GetBytes(qz), 0));
                                uint pcn = Fn<FindOverlapFn>(pOverlap2)(c2, cp, geom, mpose, res2, 64, 0, ov2);
                                log("PX_POSES " + pi + " " + qx.ToString("0.#") + " " + qy.ToString("0.#") + " " + qz.ToString("0.#") + " " + pcn);
                            }
                        }
                    }
                }
            }
            catch (Exception e) { log("physprobe: PX_FIELD2 ex: " + e.Message); }
        }
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
