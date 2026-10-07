// Height sampler on top of the real terrain data loader
// (PhysicsEngine::KG3D_PhysxTerrainDataLoader_Source::LoadRegion).
// Copied from engine_host_spike/MapSpike.cs so the product client is self-contained.
//
// Streaming: the engine's PhysicsTerrain streams regions around the player
// (UpdateTerrain + a bounded region cache, Config nMaxCacheCount). This sampler
// is a direct data-loader client, so it keeps a small bounded LRU cache of
// region buffers (RC_TERR_CACHE, default 4) - with a single slot the player,
// the camera ground clamp and the camera ray march ping-pong two adjacent
// regions across a border and reload (~1 MB + decode) every frame.
//
// Telemetry: every load is timed and the totals are logged at exit
// (`terrain stats ...`), so a scripted border-crossing run is reproducible
// before/after evidence.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal sealed class TerrainSampler : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr LoadLibraryExA(string path, IntPtr hFile, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string name);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateDataLoaderFn(IntPtr path, IntPtr opt, out IntPtr loader);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int DescFn(IntPtr self, IntPtr out32);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int LoadRegionFn(IntPtr self, int nX, int nZ, IntPtr pData, int nCount,
                              IntPtr outA, IntPtr outB, IntPtr outC);

    // vt[4] PhysicsEngine::KG3D_PhysxTerrainDataLoader_Source::LoadHoleRegion.
    // pRetByteArray receives a packed 1-bit-per-cell hole mask
    // (nRegionSize*nRegionSize/8 bytes); pOutFlag is set to 1 when the region
    // has no holes at all (assert nArraySize == nRegionSize^2/8, verified in
    // engine_host_spike/recon_loader_methods.txt and the _ConvertHoleData
    // disasm proof/collision/disasm/hole_convert_holedata.txt).
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int LoadHoleRegionFn(IntPtr self, int nX, int nZ, IntPtr pData,
                                  int nArraySize, IntPtr pOutFlag);

    // one cached terrain region: height grid + packed hole mask
    sealed class Region
    {
        public int Ix, Iz;
        public IntPtr Buf;
        public IntPtr Hole;
        public bool HasHoles;
        public long LastUse;
    }

    IntPtr _loader = IntPtr.Zero;
    int _size, _nrx, _nrz, _count;
    int _holeRowBytes;
    float _cell, _originX, _originZ;
    Action<string> _log;
    readonly List<Region> _cache = new List<Region>();
    Region _cur;
    readonly int _cacheCap;
    long _use;
    // region loads that came back all-zero (engine stream not ready): retried
    // on the next call and never cached as zeros
    readonly Dictionary<long, int> _zeroTries = new Dictionary<long, int>();
    public int ZeroRetries;

    // streaming telemetry (per-run load cost)
    public int Loads;
    public double LoadMsTotal;
    public double LoadMsMax;
    public double LastLoadMs;

    static float ToF(IntPtr p, int off)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(p, off)), 0);
    }

    static T Fn<T>(IntPtr p) where T : class
    {
        return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
    }

    public TerrainSampler(string physDll, string mapPath, Action<string> log, int cacheCap)
    {
        _log = log;
        _cacheCap = cacheCap < 1 ? 1 : cacheCap;
        IntPtr h = GetModuleHandleA("PhysicsEngineX64.dll");
        if (h == IntPtr.Zero) h = LoadLibraryExA(physDll, IntPtr.Zero, 0x8);
        if (h == IntPtr.Zero)
            throw new InvalidOperationException("PhysicsEngineX64.dll not loadable, err=" + Marshal.GetLastWin32Error());
        IntPtr p = GetProcAddress(h, "CreatePhysicsTerrainDataLoader");
        if (p == IntPtr.Zero) throw new InvalidOperationException("CreatePhysicsTerrainDataLoader not found");

        IntPtr mp = Marshal.StringToHGlobalAnsi(mapPath);
        IntPtr loader = IntPtr.Zero;
        try
        {
            var create = Fn<CreateDataLoaderFn>(p);
            int ok = create(mp, IntPtr.Zero, out loader);
            if (ok == 0 || loader == IntPtr.Zero) throw new InvalidOperationException("loader create failed");
        }
        finally { Marshal.FreeHGlobal(mp); }
        _loader = loader;

        IntPtr d = Marshal.AllocHGlobal(32);
        try
        {
            for (int i = 0; i < 32; i += 4) Marshal.WriteInt32(d, i, 0);
            IntPtr vt = Marshal.ReadIntPtr(loader);
            var desc = Fn<DescFn>(Marshal.ReadIntPtr(vt, 2 * IntPtr.Size));
            desc(loader, d);
            _size = Marshal.ReadInt32(d, 0);
            _nrx = Marshal.ReadInt32(d, 4);
            _nrz = Marshal.ReadInt32(d, 8);
            _cell = ToF(d, 0x10);
            _originX = ToF(d, 0x18);
            _originZ = ToF(d, 0x1C);
        }
        finally { Marshal.FreeHGlobal(d); }

        _count = (_size + 1) * (_size + 1);
        _holeRowBytes = (_size + 7) / 8;
        log(string.Format("TerrainSampler: size={0} regions={1}x{2} cell={3} origin=({4},{5}) cache={6}",
            _size, _nrx, _nrz, _cell, _originX, _originZ, _cacheCap));
    }

    int RegionIndex(float v, float origin, int n)
    {
        int i = (int)Math.Floor((v - origin) / (_size * _cell));
        if (i < 0) i = 0;
        if (i >= n) i = n - 1;
        return i;
    }

    void EnsureRegion(int ix, int iz)
    {
        if (_cur != null && _cur.Ix == ix && _cur.Iz == iz) { _cur.LastUse = ++_use; return; }
        Region r = null;
        for (int i = 0; i < _cache.Count; i++)
        {
            if (_cache[i].Ix == ix && _cache[i].Iz == iz) { r = _cache[i]; break; }
        }
        if (r == null)
        {
            r = LoadRegionEntry(ix, iz);
            if (r == null) return;   // load failed; keep the previous region
        }
        r.LastUse = ++_use;
        _cur = r;
    }

    Region LoadRegionEntry(int ix, int iz)
    {
        Region r = new Region();
        r.Ix = ix; r.Iz = iz;
        r.Buf = Marshal.AllocHGlobal(_count * 4);
        r.Hole = Marshal.AllocHGlobal(_holeRowBytes * _size);
        Marshal.Copy(new byte[_holeRowBytes * _size], 0, r.Hole, _holeRowBytes * _size);
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        bool ok = false;
        try
        {
            IntPtr vt = Marshal.ReadIntPtr(_loader);
            var load = Fn<LoadRegionFn>(Marshal.ReadIntPtr(vt, 3 * IntPtr.Size));
            IntPtr a = Marshal.AllocHGlobal(8), b = Marshal.AllocHGlobal(8), c = Marshal.AllocHGlobal(8);
            try
            {
                ok = load(_loader, ix, iz, r.Buf, _count, a, b, c) != 0;
            }
            finally { Marshal.FreeHGlobal(a); Marshal.FreeHGlobal(b); Marshal.FreeHGlobal(c); }

            // real terrain holes (cave voids): packed 1-bit-per-cell mask
            r.HasHoles = false;
            IntPtr holeFn = Marshal.ReadIntPtr(vt, 4 * IntPtr.Size);
            if (ok && holeFn != IntPtr.Zero && r.Hole != IntPtr.Zero)
            {
                var loadHole = Fn<LoadHoleRegionFn>(holeFn);
                IntPtr flag = Marshal.AllocHGlobal(4);
                try
                {
                    Marshal.WriteInt32(flag, 0, 1);
                    int hok = loadHole(_loader, ix, iz, r.Hole, _size * _size / 8, flag);
                    if (hok != 0) r.HasHoles = Marshal.ReadInt32(flag) == 0;
                }
                finally { Marshal.FreeHGlobal(flag); }
            }
        }
        catch (Exception e) { _log("EnsureRegion ex: " + e.Message); }
        sw.Stop();
        Loads++;
        LastLoadMs = sw.Elapsed.TotalMilliseconds;
        LoadMsTotal += LastLoadMs;
        if (LastLoadMs > LoadMsMax) LoadMsMax = LastLoadMs;
        if (!ok)
        {
            _log(string.Format("LoadRegion failed ({0},{1}) ms={2:F1}", ix, iz, LastLoadMs));
            Marshal.FreeHGlobal(r.Buf);
            Marshal.FreeHGlobal(r.Hole);
            return null;
        }
        // The loader can return an all-zero grid before the engine has streamed
        // the region (observed at spawn in RC_SPAWN regions). Do not cache
        // zeros - report not-ready and retry on the next call (the engine
        // streams within seconds once the player model is active). After 600
        // tries the zeros are cached to avoid an unbounded retry loop.
        bool allZero = true;
        for (int i = 0; i < _count; i++)
        {
            if (Marshal.ReadInt32(r.Buf, i * 4) != 0) { allZero = false; break; }
        }
        if (allZero)
        {
            long key = ((long)ix << 32) ^ (uint)iz;
            int tries = 0;
            _zeroTries.TryGetValue(key, out tries);
            tries++;
            _zeroTries[key] = tries;
            ZeroRetries++;
            if (tries == 1 || (tries % 100) == 0)
                _log(string.Format("terrain region ({0},{1}) all-zero (not streamed yet), retry #{2}", ix, iz, tries));
            if (tries <= 600)
            {
                Marshal.FreeHGlobal(r.Buf);
                Marshal.FreeHGlobal(r.Hole);
                return null;
            }
            _log(string.Format("terrain region ({0},{1}) still all-zero after {2} retries - caching", ix, iz, tries));
        }
        _log(string.Format("terrain load ({0},{1}) ms={2:F1} holes={3} cache={4}",
            ix, iz, LastLoadMs, r.HasHoles ? 1 : 0, _cache.Count + 1));
        // the fresh entry is the most recently used: mark it before the
        // eviction pass, otherwise it evicts itself (cap=1 freed the buffers
        // we were about to return)
        r.LastUse = ++_use;
        _cache.Add(r);
        while (_cache.Count > _cacheCap)
        {
            int ev = 0;
            for (int i = 1; i < _cache.Count; i++)
                if (_cache[i].LastUse < _cache[ev].LastUse) ev = i;
            Marshal.FreeHGlobal(_cache[ev].Buf);
            Marshal.FreeHGlobal(_cache[ev].Hole);
            _cache.RemoveAt(ev);
        }
        return r;
    }

    float H(IntPtr buf, int idx)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(buf, idx * 4)), 0);
    }

    public float Sample(float x, float z)
    {
        if (_loader == IntPtr.Zero) return 0f;
        int ix = RegionIndex(x, _originX, _nrx), iz = RegionIndex(z, _originZ, _nrz);
        EnsureRegion(ix, iz);
        Region r = _cur;
        if (r == null) return 0f;
        float gx = (x - _originX) / _cell - ix * _size;
        float gz = (z - _originZ) / _cell - iz * _size;
        if (gx < 0f) gx = 0f;
        if (gx > _size - 1) gx = _size - 1;
        if (gz < 0f) gz = 0f;
        if (gz > _size - 1) gz = _size - 1;
        int x0 = (int)Math.Floor(gx), z0 = (int)Math.Floor(gz);
        int x1 = x0 + 1; if (x1 > _size) x1 = _size;
        int z1 = z0 + 1; if (z1 > _size) z1 = _size;
        float fx = gx - x0, fz = gz - z0;
        int stride = _size + 1;
        float h00 = H(r.Buf, z0 * stride + x0);
        float h10 = H(r.Buf, z0 * stride + x1);
        float h01 = H(r.Buf, z1 * stride + x0);
        float h11 = H(r.Buf, z1 * stride + x1);
        float a = h00 + (h10 - h00) * fx;
        float b = h01 + (h11 - h01) * fx;
        return a + (b - a) * fz;
    }

    // Ground height at (x,z); false when the point is over a real terrain
    // hole (cave/void), where the game has no collision and the player falls.
    // The hole cell is hole only when all four of its height samples are
    // marked in the mask (decoded _ConvertHoleData rule).
    public bool SampleGround(float x, float z, out float height)
    {
        height = Sample(x, z);
        Region r = _cur;
        if (r == null || !r.HasHoles) return true;
        int ix = RegionIndex(x, _originX, _nrx), iz = RegionIndex(z, _originZ, _nrz);
        if (ix != r.Ix || iz != r.Iz) return true;
        float gx = (x - _originX) / _cell - ix * _size;
        float gz = (z - _originZ) / _cell - iz * _size;
        int cx = (int)Math.Floor(gx), cz = (int)Math.Floor(gz);
        if (cx < 0) cx = 0; if (cz < 0) cz = 0;
        if (cx >= _size) cx = _size - 1; if (cz >= _size) cz = _size - 1;
        // A/B against the extracted .hlb (see the plan doc): the engine's
        // packed mask is row-flipped in Z relative to the raw file, so the
        // engine cell for grid row cz is (_size - 1 - cz). Verified on
        // 海岛绝境_000_000: 234/234 hole cells match with the flip and 0/234
        // without it.
        cz = _size - 1 - cz;
        int b = _holeRowBytes * cz + (cx >> 3);
        byte v = Marshal.ReadByte(r.Hole, b);
        return (v & (1 << (cx & 7))) == 0;
    }

    // Debug/A-B support: state of the region used by the last query.
    public bool HasHoles { get { return _cur != null && _cur.HasHoles; } }
    public int HoleRegionX { get { return _cur == null ? -1 : _cur.Ix; } }
    public int HoleRegionZ { get { return _cur == null ? -1 : _cur.Iz; } }
    public int RegionSize { get { return _size; } }

    // Map extent from the terrain descriptor. Used to validate spawn/test
    // coordinates: the engine AVs (KG3DEngineDX11EX64+0x12282B3) when the
    // actor is outside the map extent on the 4x4 海岛绝境 map
    // (docs/movement/VOID_SPAWN_CRASH_TRIAGE.md).
    public float ExtentMinX { get { return _originX; } }
    public float ExtentMinZ { get { return _originZ; } }
    public float ExtentMaxX { get { return _originX + _nrx * _size * _cell; } }
    public float ExtentMaxZ { get { return _originZ + _nrz * _size * _cell; } }

    // streaming telemetry summary (exit log / A-B runs)
    public string StatsLine()
    {
        return string.Format("terrLoads={0} msTotal={1:F1} msMax={2:F1} last={3:F1} cache={4} zeroRetries={5}",
            Loads, LoadMsTotal, LoadMsMax, LastLoadMs, _cache.Count, ZeroRetries);
    }

    public byte[] HoleMaskCopy()
    {
        Region r = _cur;
        if (r == null || r.Hole == IntPtr.Zero) return null;
        byte[] b = new byte[_holeRowBytes * _size];
        Marshal.Copy(r.Hole, b, 0, b.Length);
        return b;
    }

    public void Dispose()
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            if (_cache[i].Buf != IntPtr.Zero) Marshal.FreeHGlobal(_cache[i].Buf);
            if (_cache[i].Hole != IntPtr.Zero) Marshal.FreeHGlobal(_cache[i].Hole);
        }
        _cache.Clear();
        _cur = null;
    }
}
