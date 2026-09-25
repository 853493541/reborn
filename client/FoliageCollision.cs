// Real structure collision for the map host.
//
// Data comes from the game's own placement files:
//  - foliage_collision.bin   (v1): decoded .foliage instances (cactus/rocks/deadwood)
//  - structure_collision.bin (v2): entities/sceneinfo_full/%03u_%03u.json world
//    objects (walls, buildings, rocks, props) with their real meshes + matrices
//
// Collision: the player is a vertical capsule; nearby instances are tested
// triangle-by-triangle in mesh local space, distances are measured in world
// space through the instance matrix (exact for anisotropic scales), the
// deepest contact pushes the player out and up-facing contacts provide
// stand-on ground.

using System;
using System.Collections.Generic;
using System.IO;

public sealed class FoliageCollision
{
    const uint MAGIC = 0x4C4F4346; // 'FCOL'

    public struct Contact
    {
        public float nx, ny, nz;
        public float depth;
        public float py;
    }

    sealed class MeshData
    {
        public float[] verts;
        public int[] tris;
        // local-space triangle grid (CSR)
        public int gx, gz;
        public float gx0, gz0, gcell;
        public int[] cellStart;
        public int[] cellTri;
        public float minX, minY, minZ, maxX, maxY, maxZ;
    }

    sealed class Instance
    {
        public MeshData mesh;
        public float[] l2w;   // 16, row-major, row-vector: w = l * M
        public float[] w2l;   // 16, inverse
        public float m00, m01, m02, m10, m11, m12, m20, m21, m22; // 3x3 for world deltas
        public float maxLocalFromWorld; // for grid expansion
        public float minX, minY, minZ, maxX, maxY, maxZ; // world AABB
    }

    readonly List<Instance> _inst = new List<Instance>();
    readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
    readonly float _cell;
    readonly List<int> _cand = new List<int>(64);

    public int InstanceCount { get { return _inst.Count; } }
    public int MeshCount { get; private set; }

    public FoliageCollision(string foliagePath, string structurePath = null, float cellSize = 800f)
    {
        _cell = cellSize;
        if (!string.IsNullOrEmpty(foliagePath) && File.Exists(foliagePath))
            LoadV1(foliagePath);
        if (!string.IsNullOrEmpty(structurePath) && File.Exists(structurePath))
            LoadV2(structurePath);
        BuildGrid();
    }

    // ---- loading ----

    void AddInstance(MeshData md, float[] m, float bminX, float bminY, float bminZ,
                     float bmaxX, float bmaxY, float bmaxZ)
    {
        var it = new Instance();
        it.mesh = md;
        it.l2w = m;
        it.w2l = Invert4x4(m);
        it.m00 = m[0]; it.m01 = m[1]; it.m02 = m[2];
        it.m10 = m[4]; it.m11 = m[5]; it.m12 = m[6];
        it.m20 = m[8]; it.m21 = m[9]; it.m22 = m[10];
        // largest local displacement produced by a unit world displacement
        float lx = (float)Math.Sqrt(m[0] * m[0] + m[1] * m[1] + m[2] * m[2]);
        float ly = (float)Math.Sqrt(m[4] * m[4] + m[5] * m[5] + m[6] * m[6]);
        float lz = (float)Math.Sqrt(m[8] * m[8] + m[9] * m[9] + m[10] * m[10]);
        it.maxLocalFromWorld = 1f / Math.Max(1e-6f, Math.Min(Math.Min(lx, ly), lz));
        it.minX = bminX; it.minY = bminY; it.minZ = bminZ;
        it.maxX = bmaxX; it.maxY = bmaxY; it.maxZ = bmaxZ;
        _inst.Add(it);
    }

    MeshData ReadMesh(BinaryReader r, int version)
    {
        var md = new MeshData();
        int vc = r.ReadInt32();
        int tc = r.ReadInt32();
        md.verts = new float[vc * 3];
        for (int i = 0; i < md.verts.Length; i++) md.verts[i] = r.ReadSingle();
        md.tris = new int[tc * 3];
        for (int i = 0; i < md.tris.Length; i++) md.tris[i] = r.ReadInt32();
        md.minX = md.minY = md.minZ = float.MaxValue;
        md.maxX = md.maxY = md.maxZ = float.MinValue;
        for (int i = 0; i < vc; i++)
        {
            float x = md.verts[i * 3], y = md.verts[i * 3 + 1], z = md.verts[i * 3 + 2];
            if (x < md.minX) md.minX = x; if (x > md.maxX) md.maxX = x;
            if (y < md.minY) md.minY = y; if (y > md.maxY) md.maxY = y;
            if (z < md.minZ) md.minZ = z; if (z > md.maxZ) md.maxZ = z;
        }
        BuildTriangleGrid(md);
        return md;
    }

    void BuildTriangleGrid(MeshData md)
    {
        int tc = md.tris.Length / 3;
        float ex = Math.Max(1f, md.maxX - md.minX);
        float ez = Math.Max(1f, md.maxZ - md.minZ);
        int n = (int)Math.Ceiling(Math.Sqrt(tc));
        if (n < 4) n = 4;
        if (n > 64) n = 64;
        md.gx = n; md.gz = n;
        md.gx0 = md.minX; md.gz0 = md.minZ;
        md.gcell = Math.Max(ex, ez) / n;
        if (md.gcell < 1e-3f) md.gcell = 1f;
        var counts = new int[n * n + 1];
        var triCell = new int[tc * 4];
        int[] cellOf = new int[tc];
        for (int t = 0; t < tc; t++)
        {
            int i0 = md.tris[t * 3] * 3, i1 = md.tris[t * 3 + 1] * 3, i2 = md.tris[t * 3 + 2] * 3;
            float mnx = Math.Min(md.verts[i0], Math.Min(md.verts[i1], md.verts[i2]));
            float mxx = Math.Max(md.verts[i0], Math.Max(md.verts[i1], md.verts[i2]));
            float mnz = Math.Min(md.verts[i0 + 2], Math.Min(md.verts[i1 + 2], md.verts[i2 + 2]));
            float mxz = Math.Max(md.verts[i0 + 2], Math.Max(md.verts[i1 + 2], md.verts[i2 + 2]));
            int cx0 = (int)((mnx - md.gx0) / md.gcell), cx1 = (int)((mxx - md.gx0) / md.gcell);
            int cz0 = (int)((mnz - md.gz0) / md.gcell), cz1 = (int)((mxz - md.gz0) / md.gcell);
            if (cx0 < 0) cx0 = 0; if (cz0 < 0) cz0 = 0;
            if (cx1 >= n) cx1 = n - 1; if (cz1 >= n) cz1 = n - 1;
            int cell = -1;
            if (cx0 == cx1 && cz0 == cz1) cell = cz0 * n + cx0;
            cellOf[t] = cell;
            if (cell >= 0) counts[cell + 1]++;
            else
            {
                for (int cz = cz0; cz <= cz1; cz++)
                    for (int cx = cx0; cx <= cx1; cx++)
                        counts[cz * n + cx + 1]++;
            }
        }
        for (int i = 1; i < counts.Length; i++) counts[i] += counts[i - 1];
        md.cellStart = counts;
        md.cellTri = new int[counts[n * n]];
        var fill = new int[n * n];
        for (int t = 0; t < tc; t++)
        {
            int cell = cellOf[t];
            if (cell >= 0)
            {
                md.cellTri[md.cellStart[cell] + fill[cell]++] = t;
                continue;
            }
            int i0 = md.tris[t * 3] * 3, i1 = md.tris[t * 3 + 1] * 3, i2 = md.tris[t * 3 + 2] * 3;
            float mnx = Math.Min(md.verts[i0], Math.Min(md.verts[i1], md.verts[i2]));
            float mxx = Math.Max(md.verts[i0], Math.Max(md.verts[i1], md.verts[i2]));
            float mnz = Math.Min(md.verts[i0 + 2], Math.Min(md.verts[i1 + 2], md.verts[i2 + 2]));
            float mxz = Math.Max(md.verts[i0 + 2], Math.Max(md.verts[i1 + 2], md.verts[i2 + 2]));
            int cx0 = (int)((mnx - md.gx0) / md.gcell), cx1 = (int)((mxx - md.gx0) / md.gcell);
            int cz0 = (int)((mnz - md.gz0) / md.gcell), cz1 = (int)((mxz - md.gz0) / md.gcell);
            if (cx0 < 0) cx0 = 0; if (cz0 < 0) cz0 = 0;
            if (cx1 >= n) cx1 = n - 1; if (cz1 >= n) cz1 = n - 1;
            for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int c = cz * n + cx;
                    md.cellTri[md.cellStart[c] + fill[c]++] = t;
                }
        }
    }

    void LoadV1(string path)
    {
        using (var r = new BinaryReader(File.OpenRead(path)))
        {
            if (r.ReadUInt32() != MAGIC) throw new InvalidDataException("bad FCOL magic");
            int version = r.ReadInt32();
            if (version != 1) throw new InvalidDataException("expected FCOL v1");
            int meshCount = r.ReadInt32();
            int instCount = r.ReadInt32();
            MeshCount += meshCount;
            var meshes = new Dictionary<int, MeshData>();
            for (int i = 0; i < meshCount; i++)
            {
                int pattern = r.ReadInt32();
                float sceneScale = r.ReadSingle();
                meshes[pattern] = ReadMesh(r, 1);
                meshes[pattern].gcell = meshes[pattern].gcell; // no-op
                _v1Scale[pattern] = sceneScale;
            }
            for (int i = 0; i < instCount; i++)
            {
                int pattern = r.ReadInt32();
                float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle();
                float yaw = r.ReadSingle(), scale = r.ReadSingle();
                MeshData md;
                if (!meshes.TryGetValue(pattern, out md)) continue;
                float s = scale * (float)_v1Scale[pattern];
                float c = (float)Math.Cos(yaw), sn = (float)Math.Sin(yaw);
                // Ry(yaw)*scale
                float[] m = new float[16];
                m[0] = c * s; m[1] = 0; m[2] = -sn * s; m[3] = 0;
                m[4] = 0; m[5] = s; m[6] = 0; m[7] = 0;
                m[8] = sn * s; m[9] = 0; m[10] = c * s; m[11] = 0;
                m[12] = x; m[13] = y; m[14] = z; m[15] = 1;
                float ex = Math.Max(Math.Abs(md.minX), Math.Abs(md.maxX));
                float ey = Math.Max(Math.Abs(md.minY), Math.Abs(md.maxY));
                float ez = Math.Max(Math.Abs(md.minZ), Math.Abs(md.maxZ));
                float rad = (float)Math.Sqrt(ex * ex + ez * ez) * s;
                AddInstance(md, m, x - rad, y + md.minY * s, z - rad,
                            x + rad, y + md.maxY * s, z + rad);
            }
        }
    }

    readonly Dictionary<int, float> _v1Scale = new Dictionary<int, float>();

    void LoadV2(string path)
    {
        using (var r = new BinaryReader(File.OpenRead(path)))
        {
            if (r.ReadUInt32() != MAGIC) throw new InvalidDataException("bad FCOL magic");
            int version = r.ReadInt32();
            if (version != 2) throw new InvalidDataException("expected FCOL v2");
            int meshCount = r.ReadInt32();
            int instCount = r.ReadInt32();
            MeshCount += meshCount;
            var meshes = new MeshData[meshCount];
            for (int i = 0; i < meshCount; i++) meshes[i] = ReadMesh(r, 2);
            for (int i = 0; i < instCount; i++)
            {
                int mi = r.ReadInt32();
                var m = new float[16];
                for (int k = 0; k < 16; k++) m[k] = r.ReadSingle();
                r.ReadInt32();
                float bminX = r.ReadSingle(), bminY = r.ReadSingle(), bminZ = r.ReadSingle();
                float bmaxX = r.ReadSingle(), bmaxY = r.ReadSingle(), bmaxZ = r.ReadSingle();
                if (mi < 0 || mi >= meshCount) continue;
                if (bmaxX <= bminX && bmaxY <= bminY && bmaxZ <= bminZ)
                {
                    // derive AABB from mesh corners
                    bminX = bminY = bminZ = float.MaxValue;
                    bmaxX = bmaxY = bmaxZ = float.MinValue;
                    MeshData md = meshes[mi];
                    for (int corner = 0; corner < 8; corner++)
                    {
                        float lx = (corner & 1) == 0 ? md.minX : md.maxX;
                        float ly = (corner & 2) == 0 ? md.minY : md.maxY;
                        float lz = (corner & 4) == 0 ? md.minZ : md.maxZ;
                        float wx = lx * m[0] + ly * m[4] + lz * m[8] + m[12];
                        float wy = lx * m[1] + ly * m[5] + lz * m[9] + m[13];
                        float wz = lx * m[2] + ly * m[6] + lz * m[10] + m[14];
                        if (wx < bminX) bminX = wx; if (wx > bmaxX) bmaxX = wx;
                        if (wy < bminY) bminY = wy; if (wy > bmaxY) bmaxY = wy;
                        if (wz < bminZ) bminZ = wz; if (wz > bmaxZ) bmaxZ = wz;
                    }
                }
                AddInstance(meshes[mi], m, bminX, bminY, bminZ, bmaxX, bmaxY, bmaxZ);
            }
        }
    }

    // ---- math ----

    static float[] Invert4x4(float[] m)
    {
        float[] inv = new float[16];
        inv[0] = m[5] * m[10] * m[15] - m[5] * m[11] * m[14] - m[9] * m[6] * m[15] + m[9] * m[7] * m[14] + m[13] * m[6] * m[11] - m[13] * m[7] * m[10];
        inv[4] = -m[4] * m[10] * m[15] + m[4] * m[11] * m[14] + m[8] * m[6] * m[15] - m[8] * m[7] * m[14] - m[12] * m[6] * m[11] + m[12] * m[7] * m[10];
        inv[8] = m[4] * m[9] * m[15] - m[4] * m[11] * m[13] - m[8] * m[5] * m[15] + m[8] * m[7] * m[13] + m[12] * m[5] * m[11] - m[12] * m[7] * m[9];
        inv[12] = -m[4] * m[9] * m[14] + m[4] * m[10] * m[13] + m[8] * m[5] * m[14] - m[8] * m[6] * m[13] - m[12] * m[5] * m[10] + m[12] * m[6] * m[9];
        inv[1] = -m[1] * m[10] * m[15] + m[1] * m[11] * m[14] + m[9] * m[2] * m[15] - m[9] * m[3] * m[14] - m[13] * m[2] * m[11] + m[13] * m[3] * m[10];
        inv[5] = m[0] * m[10] * m[15] - m[0] * m[11] * m[14] - m[8] * m[2] * m[15] + m[8] * m[3] * m[14] + m[12] * m[2] * m[11] - m[12] * m[3] * m[10];
        inv[9] = -m[0] * m[9] * m[15] + m[0] * m[11] * m[13] + m[8] * m[1] * m[15] - m[8] * m[3] * m[13] - m[12] * m[1] * m[11] + m[12] * m[3] * m[9];
        inv[13] = m[0] * m[9] * m[14] - m[0] * m[10] * m[13] - m[8] * m[1] * m[14] + m[8] * m[2] * m[13] + m[12] * m[1] * m[10] - m[12] * m[2] * m[9];
        inv[2] = m[1] * m[6] * m[15] - m[1] * m[7] * m[14] - m[5] * m[2] * m[15] + m[5] * m[3] * m[14] + m[13] * m[2] * m[7] - m[13] * m[3] * m[6];
        inv[6] = -m[0] * m[6] * m[15] + m[0] * m[7] * m[14] + m[4] * m[2] * m[15] - m[4] * m[3] * m[14] - m[12] * m[2] * m[7] + m[12] * m[3] * m[6];
        inv[10] = m[0] * m[5] * m[15] - m[0] * m[7] * m[13] - m[4] * m[1] * m[15] + m[4] * m[3] * m[13] + m[12] * m[1] * m[7] - m[12] * m[3] * m[5];
        inv[14] = -m[0] * m[5] * m[14] + m[0] * m[6] * m[13] + m[4] * m[1] * m[14] - m[4] * m[2] * m[13] - m[12] * m[1] * m[6] + m[12] * m[2] * m[5];
        inv[3] = -m[1] * m[6] * m[11] + m[1] * m[7] * m[10] + m[5] * m[2] * m[11] - m[5] * m[3] * m[10] - m[9] * m[2] * m[7] + m[9] * m[3] * m[6];
        inv[7] = m[0] * m[6] * m[11] - m[0] * m[7] * m[10] - m[4] * m[2] * m[11] + m[4] * m[3] * m[10] + m[8] * m[2] * m[7] - m[8] * m[3] * m[6];
        inv[11] = -m[0] * m[5] * m[11] + m[0] * m[7] * m[9] + m[4] * m[1] * m[11] - m[4] * m[3] * m[9] - m[8] * m[1] * m[7] + m[8] * m[3] * m[5];
        inv[15] = m[0] * m[5] * m[10] - m[0] * m[6] * m[9] - m[4] * m[1] * m[10] + m[4] * m[2] * m[9] + m[8] * m[1] * m[6] - m[8] * m[2] * m[5];
        float det = m[0] * inv[0] + m[1] * inv[4] + m[2] * inv[8] + m[3] * inv[12];
        if (Math.Abs(det) < 1e-12f) return null;
        det = 1f / det;
        for (int i = 0; i < 16; i++) inv[i] *= det;
        return inv;
    }

    static void ClosestPointOnTri(float[] v, int[] tris, int t, float qx, float qy, float qz,
                                  out float px, out float py, out float pz)
    {
        int i0 = tris[t * 3] * 3, i1 = tris[t * 3 + 1] * 3, i2 = tris[t * 3 + 2] * 3;
        float ax = v[i0], ay = v[i0 + 1], az = v[i0 + 2];
        float bx = v[i1], by = v[i1 + 1], bz = v[i1 + 2];
        float cx = v[i2], cy = v[i2 + 1], cz = v[i2 + 2];
        float abx = bx - ax, aby = by - ay, abz = bz - az;
        float acx = cx - ax, acy = cy - ay, acz = cz - az;
        float apx = qx - ax, apy = qy - ay, apz = qz - az;
        float d1 = abx * apx + aby * apy + abz * apz;
        float d2 = acx * apx + acy * apy + acz * apz;
        if (d1 <= 0f && d2 <= 0f) { px = ax; py = ay; pz = az; return; }
        float bpx = qx - bx, bpy = qy - by, bpz = qz - bz;
        float d3 = abx * bpx + aby * bpy + abz * bpz;
        float d4 = acx * bpx + acy * bpy + acz * bpz;
        if (d3 >= 0f && d4 <= d3) { px = bx; py = by; pz = bz; return; }
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float tt = d1 / (d1 - d3);
            px = ax + abx * tt; py = ay + aby * tt; pz = az + abz * tt; return;
        }
        float cpx = qx - cx, cpy = qy - cy, cpz = qz - cz;
        float d5 = abx * cpx + aby * cpy + abz * cpz;
        float d6 = acx * cpx + acy * cpy + acz * cpz;
        if (d6 >= 0f && d5 <= d6) { px = cx; py = cy; pz = cz; return; }
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float tt = d2 / (d2 - d6);
            px = ax + acx * tt; py = ay + acy * tt; pz = az + acz * tt; return;
        }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float tt = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            px = bx + (cx - bx) * tt; py = by + (cy - by) * tt; pz = bz + (cz - bz) * tt; return;
        }
        float denom = 1f / (va + vb + vc);
        float u = vb * denom, w = vc * denom;
        px = ax + abx * u + acx * w;
        py = ay + aby * u + acy * w;
        pz = az + abz * u + acz * w;
    }

    // ---- runtime ----

    long CellKey(int cx, int cz)
    {
        return ((long)cx << 32) ^ (uint)cz;
    }

    void BuildGrid()
    {
        for (int i = 0; i < _inst.Count; i++)
        {
            Instance it = _inst[i];
            int cx0 = (int)Math.Floor(it.minX / _cell);
            int cx1 = (int)Math.Floor(it.maxX / _cell);
            int cz0 = (int)Math.Floor(it.minZ / _cell);
            int cz1 = (int)Math.Floor(it.maxZ / _cell);
            // very large objects: cap the number of cells
            if ((long)(cx1 - cx0 + 1) * (cz1 - cz0 + 1) > 4096)
            {
                cx1 = cx0; cz1 = cz0;
            }
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cz = cz0; cz <= cz1; cz++)
                {
                    long key = CellKey(cx, cz);
                    List<int> lst;
                    if (!_grid.TryGetValue(key, out lst))
                    {
                        lst = new List<int>(4);
                        _grid[key] = lst;
                    }
                    lst.Add(i);
                }
            }
        }
    }

    public void GatherCandidates(float x, float z, float range, List<int> outIdx)
    {
        outIdx.Clear();
        int cx0 = (int)Math.Floor((x - range) / _cell);
        int cx1 = (int)Math.Floor((x + range) / _cell);
        int cz0 = (int)Math.Floor((z - range) / _cell);
        int cz1 = (int)Math.Floor((z + range) / _cell);
        for (int cx = cx0; cx <= cx1; cx++)
        {
            for (int cz = cz0; cz <= cz1; cz++)
            {
                List<int> lst;
                if (_grid.TryGetValue(CellKey(cx, cz), out lst))
                {
                    for (int k = 0; k < lst.Count; k++)
                        if (!outIdx.Contains(lst[k])) outIdx.Add(lst[k]);
                }
            }
        }
    }

    bool InstanceContact(Instance it, float px, float py, float pz,
                         float radius, float height, ref Contact best)
    {
        float[] w2l = it.w2l;
        if (w2l == null) return false;
        // world -> local for capsule endpoints
        float lAx = px * w2l[0] + py * w2l[4] + pz * w2l[8] + w2l[12];
        float lAy = px * w2l[1] + py * w2l[5] + pz * w2l[9] + w2l[13];
        float lAz = px * w2l[2] + py * w2l[6] + pz * w2l[10] + w2l[14];
        float hTop = height;
        float lBx = lAx + (0f * w2l[0] + hTop * w2l[4] + 0f * w2l[8]);
        float lBy = lAy + (0f * w2l[1] + hTop * w2l[5] + 0f * w2l[9]);
        float lBz = lAz + (0f * w2l[2] + hTop * w2l[6] + 0f * w2l[10]);

        MeshData md = it.mesh;
        int samples = 6;
        bool found = false;
        float rLocal = radius * it.maxLocalFromWorld * 1.05f;
        for (int s = 0; s < samples; s++)
        {
            float tt = samples == 1 ? 0f : (float)s / (samples - 1);
            float qx = lAx + (lBx - lAx) * tt;
            float qy = lAy + (lBy - lAy) * tt;
            float qz = lAz + (lBz - lAz) * tt;
            // triangle grid lookup
            int cx0 = (int)((qx - rLocal - md.gx0) / md.gcell);
            int cx1 = (int)((qx + rLocal - md.gx0) / md.gcell);
            int cz0 = (int)((qz - rLocal - md.gz0) / md.gcell);
            int cz1 = (int)((qz + rLocal - md.gz0) / md.gcell);
            if (cx0 < 0) cx0 = 0; if (cz0 < 0) cz0 = 0;
            if (cx1 >= md.gx) cx1 = md.gx - 1; if (cz1 >= md.gz) cz1 = md.gz - 1;
            if (cx0 > cx1 || cz0 > cz1) continue;
            for (int cz = cz0; cz <= cz1; cz++)
            {
                int rowBase = cz * md.gx;
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int c = rowBase + cx;
                    int s0 = md.cellStart[c], s1 = md.cellStart[c + 1];
                    for (int k = s0; k < s1; k++)
                    {
                        int tri = md.cellTri[k];
                        float cpx, cpy, cpz;
                        ClosestPointOnTri(md.verts, md.tris, tri, qx, qy, qz,
                                          out cpx, out cpy, out cpz);
                        float dx = qx - cpx, dy = qy - cpy, dz = qz - cpz;
                        // measure the distance in world space
                        float wx = dx * it.m00 + dy * it.m10 + dz * it.m20;
                        float wy = dx * it.m01 + dy * it.m11 + dz * it.m21;
                        float wz = dx * it.m02 + dy * it.m12 + dz * it.m22;
                        float dist = (float)Math.Sqrt(wx * wx + wy * wy + wz * wz);
                        if (dist >= radius) continue;
                        float depth = radius - dist;
                        float nx, ny, nz;
                        if (dist > 1e-5f) { nx = wx / dist; ny = wy / dist; nz = wz / dist; }
                        else
                        {
                            int i0 = md.tris[tri * 3] * 3, i1 = md.tris[tri * 3 + 1] * 3, i2 = md.tris[tri * 3 + 2] * 3;
                            float abx = md.verts[i1] - md.verts[i0], aby = md.verts[i1 + 1] - md.verts[i0 + 1], abz = md.verts[i1 + 2] - md.verts[i0 + 2];
                            float acx = md.verts[i2] - md.verts[i0], acy = md.verts[i2 + 1] - md.verts[i0 + 1], acz = md.verts[i2 + 2] - md.verts[i0 + 2];
                            nx = aby * acz - abz * acy; ny = abz * acx - abx * acz; nz = abx * acy - aby * acx;
                            float wnx = nx * it.m00 + ny * it.m10 + nz * it.m20;
                            float wny = nx * it.m01 + ny * it.m11 + nz * it.m21;
                            float wnz = nx * it.m02 + ny * it.m12 + nz * it.m22;
                            float nl = (float)Math.Sqrt(wnx * wnx + wny * wny + wnz * wnz);
                            if (nl < 1e-9f) continue;
                            nx = wnx / nl; ny = wny / nl; nz = wnz / nl;
                        }
                        if (!found || depth > best.depth)
                        {
                            best.nx = nx; best.ny = ny; best.nz = nz;
                            best.depth = depth;
                            best.py = cpy * it.m01 + cpx * it.m00 + cpz * it.m02 + it.l2w[13] * 0f; // placeholder, fixed below
                            best.py = cpx * it.l2w[1] + cpy * it.l2w[5] + cpz * it.l2w[9] + it.l2w[13];
                            found = true;
                        }
                    }
                }
            }
        }
        return found;
    }

    public bool Resolve(ref float px, ref float py, ref float pz,
                        float radius, float height,
                        ref float ground, ref bool grounded)
    {
        bool blocked = false;
        for (int iter = 0; iter < 3; iter++)
        {
            Contact best = new Contact();
            bool any = false;
            int bestIdx = -1;
            GatherCandidates(px, pz, radius + 600f, _cand);
            for (int k = 0; k < _cand.Count; k++)
            {
                Instance it = _inst[_cand[k]];
                if (py + height < it.minY - 60f || py > it.maxY + 60f) continue;
                if (px < it.minX - radius || px > it.maxX + radius) continue;
                if (pz < it.minZ - radius || pz > it.maxZ + radius) continue;
                if (InstanceContact(it, px, py, pz, radius, height, ref best))
                {
                    any = true;
                    bestIdx = _cand[k];
                }
            }
            if (!any) break;
            bool stepUp = false;
            if (bestIdx >= 0)
            {
                Instance bi = _inst[bestIdx];
                float horiz = (float)Math.Sqrt(best.nx * best.nx + best.nz * best.nz);
                if (horiz > 0.5f && bi.maxY > ground && bi.maxY <= py + 70f)
                {
                    float sh = SupportHeight(px, pz, py - 30f, bi.maxY + 5f);
                    if (sh > ground)
                    {
                        ground = sh;
                        grounded = true;
                        stepUp = true;
                    }
                }
            }
            if (!stepUp && best.depth > 0.01f)
            {
                px += best.nx * best.depth;
                py += best.ny * best.depth;
                pz += best.nz * best.depth;
                float horiz = (float)Math.Sqrt(best.nx * best.nx + best.nz * best.nz);
                if (horiz > 0.5f) blocked = true;
            }
            if (best.ny > 0.55f && best.py > ground && best.py <= py + 60f)
            {
                ground = best.py;
                if (py <= ground + 2f) grounded = true;
            }
            if (stepUp) break;
        }
        return blocked;
    }

    public float SupportHeight(float x, float z, float yLow, float yHigh)
    {
        float best = float.MinValue;
        GatherCandidates(x, z, 400f, _cand);
        for (int k = 0; k < _cand.Count; k++)
        {
            Instance it = _inst[_cand[k]];
            if (it.maxY < yLow || it.minY > yHigh) continue;
            if (x < it.minX - 4f || x > it.maxX + 4f) continue;
            if (z < it.minZ - 4f || z > it.maxZ + 4f) continue;
            float[] w2l = it.w2l;
            if (w2l == null) continue;
            float lx = x * w2l[0] + z * w2l[8] + w2l[12];
            float lz = x * w2l[2] + z * w2l[10] + w2l[14];
            MeshData md = it.mesh;
            int cx = (int)((lx - md.gx0) / md.gcell);
            int cz = (int)((lz - md.gz0) / md.gcell);
            if (cx < 0 || cz < 0 || cx >= md.gx || cz >= md.gz) continue;
            int c = cz * md.gx + cx;
            int s0 = md.cellStart[c], s1 = md.cellStart[c + 1];
            for (int kk = s0; kk < s1; kk++)
            {
                int tri = md.cellTri[kk];
                int i0 = md.tris[tri * 3] * 3, i1 = md.tris[tri * 3 + 1] * 3, i2 = md.tris[tri * 3 + 2] * 3;
                float ax = md.verts[i0], az = md.verts[i0 + 2];
                float bx = md.verts[i1], bz = md.verts[i1 + 2];
                float cx2 = md.verts[i2], cz2 = md.verts[i2 + 2];
                float v0x = bx - ax, v0z = bz - az;
                float v1x = cx2 - ax, v1z = cz2 - az;
                float v2x = lx - ax, v2z = lz - az;
                float den = v0x * v1z - v1x * v0z;
                if (Math.Abs(den) < 1e-12f) continue;
                float u = (v2x * v1z - v1x * v2z) / den;
                float w = (v0x * v2z - v2x * v0z) / den;
                if (u < 0f || w < 0f || u + w > 1f) continue;
                float ay = md.verts[i0 + 1], by = md.verts[i1 + 1], cy = md.verts[i2 + 1];
                float ly = ay + (by - ay) * u + (cy - ay) * w;
                // world normal
                float nx = (by - ay) * (cz2 - az) - (bz - az) * (cy - ay);
                float ny = (bz - az) * (cx2 - ax) - (bx - ax) * (cz2 - az);
                float nz = (bx - ax) * (cy - ay) - (by - ay) * (cx2 - ax);
                float wnx = nx * it.m00 + ny * it.m10 + nz * it.m20;
                float wny = nx * it.m01 + ny * it.m11 + nz * it.m21;
                float wnz = nx * it.m02 + ny * it.m12 + nz * it.m22;
                float nl = (float)Math.Sqrt(wnx * wnx + wny * wny + wnz * wnz);
                if (nl < 1e-9f || wny / nl < 0.5f) continue;
                float wy = lx * it.l2w[1] + ly * it.l2w[5] + lz * it.l2w[9] + it.l2w[13];
                if (wy < yLow || wy > yHigh) continue;
                if (wy > best) best = wy;
            }
        }
        return best;
    }

    public float NearestInstance(float x, float z, out float nx, out float ny, out float nz)
    {
        float best = float.MaxValue;
        nx = ny = nz = 0f;
        for (int i = 0; i < _inst.Count; i++)
        {
            Instance it = _inst[i];
            float cx = (it.minX + it.maxX) * 0.5f, cz = (it.minZ + it.maxZ) * 0.5f;
            float dx = x - cx, dz = z - cz;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < best)
            {
                best = d;
                nx = cx; ny = (it.minY + it.maxY) * 0.5f; nz = cz;
            }
        }
        return best;
    }

    public bool GetInstanceBounds(int idx, out float minX, out float minY, out float minZ,
                                  out float maxX, out float maxY, out float maxZ)
    {
        minX = minY = minZ = maxX = maxY = maxZ = 0f;
        if (idx < 0 || idx >= _inst.Count) return false;
        Instance it = _inst[idx];
        minX = it.minX; minY = it.minY; minZ = it.minZ;
        maxX = it.maxX; maxY = it.maxY; maxZ = it.maxZ;
        return true;
    }

    public string Describe()
    {
        return string.Format("instances={0} meshes={1}", _inst.Count, MeshCount);
    }
}
