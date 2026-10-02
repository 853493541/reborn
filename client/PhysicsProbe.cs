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
    delegate int Bool2Fn(IntPtr self, IntPtr a, IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate uint WriteFn(IntPtr self, IntPtr src, uint count);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate uint ReadFn(IntPtr self, IntPtr dest, uint count);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr Ptr2Fn(IntPtr self, IntPtr a);
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
        // PhysX-direct step 3 (opt-in RC_PX_MESH=1, test exe): identify and call
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
