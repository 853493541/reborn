// Height sampler on top of the real terrain data loader
// (PhysicsEngine::KG3D_PhysxTerrainDataLoader_Source::LoadRegion).
// Copied from engine_host_spike/MapSpike.cs so the product client is self-contained.
//
// Streaming telemetry: a region buffer is (size+1)^2 floats (~1 MB), and a region
// change loads synchronously inside the movement tick - the only terrain-side
// hitch source. Every load is timed and the totals are logged at exit
// (`terrain stats ...`), so a scripted border-crossing run is reproducible
// before/after evidence.
using System;
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

    IntPtr _loader = IntPtr.Zero;
    IntPtr _buf = IntPtr.Zero;
    IntPtr _hole = IntPtr.Zero;
    int _size, _nrx, _nrz, _count, _curIx = -1, _curIz = -1;
    int _holeRowBytes, _holeIx = -1, _holeIz = -1;
    bool _hasHoles;
    float _cell, _originX, _originZ;
    Action<string> _log;

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

    public TerrainSampler(string physDll, string mapPath, Action<string> log)
    {
        _log = log;
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
        _buf = Marshal.AllocHGlobal(_count * 4);
        _holeRowBytes = (_size + 7) / 8;
        _hole = Marshal.AllocHGlobal(_holeRowBytes * _size);
        Marshal.Copy(new byte[_holeRowBytes * _size], 0, _hole, _holeRowBytes * _size);
        log(string.Format("TerrainSampler: size={0} regions={1}x{2} cell={3} origin=({4},{5})",
            _size, _nrx, _nrz, _cell, _originX, _originZ));
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
        if (ix == _curIx && iz == _curIz) return;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        bool ok = false;
        try
        {
            IntPtr vt = Marshal.ReadIntPtr(_loader);
            var load = Fn<LoadRegionFn>(Marshal.ReadIntPtr(vt, 3 * IntPtr.Size));
            IntPtr a = Marshal.AllocHGlobal(8), b = Marshal.AllocHGlobal(8), c = Marshal.AllocHGlobal(8);
            try
            {
                ok = load(_loader, ix, iz, _buf, _count, a, b, c) != 0;
                if (ok) { _curIx = ix; _curIz = iz; }
            }
            finally { Marshal.FreeHGlobal(a); Marshal.FreeHGlobal(b); Marshal.FreeHGlobal(c); }

            // real terrain holes (cave voids): packed 1-bit-per-cell mask
            _hasHoles = false;
            _holeIx = -1; _holeIz = -1;
            IntPtr holeFn = Marshal.ReadIntPtr(vt, 4 * IntPtr.Size);
            if (holeFn != IntPtr.Zero && _hole != IntPtr.Zero)
            {
                var loadHole = Fn<LoadHoleRegionFn>(holeFn);
                IntPtr flag = Marshal.AllocHGlobal(4);
                try
                {
                    Marshal.WriteInt32(flag, 0, 1);
                    int hok = loadHole(_loader, ix, iz, _hole, _size * _size / 8, flag);
                    if (hok != 0)
                    {
                        _holeIx = ix; _holeIz = iz;
                        _hasHoles = Marshal.ReadInt32(flag) == 0;
                    }
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
        if (ok)
            _log(string.Format("terrain load ({0},{1}) ms={2:F1} holes={3}",
                ix, iz, LastLoadMs, _hasHoles ? 1 : 0));
        else
            _log(string.Format("LoadRegion failed ({0},{1}) ms={2:F1}", ix, iz, LastLoadMs));
    }

    float H(int idx)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(_buf, idx * 4)), 0);
    }

    public float Sample(float x, float z)
    {
        if (_buf == IntPtr.Zero) return 0f;
        int ix = RegionIndex(x, _originX, _nrx), iz = RegionIndex(z, _originZ, _nrz);
        EnsureRegion(ix, iz);
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
        float h00 = H(z0 * stride + x0);
        float h10 = H(z0 * stride + x1);
        float h01 = H(z1 * stride + x0);
        float h11 = H(z1 * stride + x1);
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
        if (!_hasHoles) return true;
        int ix = RegionIndex(x, _originX, _nrx), iz = RegionIndex(z, _originZ, _nrz);
        if (ix != _holeIx || iz != _holeIz) return true;
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
        byte v = Marshal.ReadByte(_hole, b);
        return (v & (1 << (cx & 7))) == 0;
    }

    // Debug/A-B support: the packed mask of the region loaded last.
    public bool HasHoles { get { return _hasHoles; } }
    public int HoleRegionX { get { return _holeIx; } }
    public int HoleRegionZ { get { return _holeIz; } }
    public int RegionSize { get { return _size; } }

    // streaming telemetry summary (exit log / A-B runs)
    public string StatsLine()
    {
        return string.Format("terrLoads={0} msTotal={1:F1} msMax={2:F1} last={3:F1}",
            Loads, LoadMsTotal, LoadMsMax, LastLoadMs);
    }

    public byte[] HoleMaskCopy()
    {
        if (_hole == IntPtr.Zero) return null;
        byte[] b = new byte[_holeRowBytes * _size];
        Marshal.Copy(_hole, b, 0, b.Length);
        return b;
    }

    public void Dispose()
    {
        if (_buf != IntPtr.Zero) Marshal.FreeHGlobal(_buf);
        _buf = IntPtr.Zero;
        if (_hole != IntPtr.Zero) Marshal.FreeHGlobal(_hole);
        _hole = IntPtr.Zero;
    }
}
