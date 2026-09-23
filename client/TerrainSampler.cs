// Height sampler on top of the real terrain data loader
// (PhysicsEngine::KG3D_PhysxTerrainDataLoader_Source::LoadRegion).
// Copied from engine_host_spike/MapSpike.cs so the product client is self-contained.
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

    IntPtr _loader = IntPtr.Zero;
    IntPtr _buf = IntPtr.Zero;
    int _size, _nrx, _nrz, _count, _curIx = -1, _curIz = -1;
    float _cell, _originX, _originZ;
    Action<string> _log;

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
        try
        {
            IntPtr vt = Marshal.ReadIntPtr(_loader);
            var load = Fn<LoadRegionFn>(Marshal.ReadIntPtr(vt, 3 * IntPtr.Size));
            IntPtr a = Marshal.AllocHGlobal(8), b = Marshal.AllocHGlobal(8), c = Marshal.AllocHGlobal(8);
            try
            {
                int ok = load(_loader, ix, iz, _buf, _count, a, b, c);
                if (ok != 0) { _curIx = ix; _curIz = iz; }
                else _log("LoadRegion failed (" + ix + "," + iz + ")");
            }
            finally { Marshal.FreeHGlobal(a); Marshal.FreeHGlobal(b); Marshal.FreeHGlobal(c); }
        }
        catch (Exception e) { _log("EnsureRegion ex: " + e.Message); }
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

    public void Dispose()
    {
        if (_buf != IntPtr.Zero) Marshal.FreeHGlobal(_buf);
        _buf = IntPtr.Zero;
    }
}
