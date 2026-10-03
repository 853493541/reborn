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
        // world-space top of the contacting triangle: the CCT step budget is
        // compared against the surface that actually blocks (merged meshes mix
        // low planks and tall walls in one mesh, so the neighbourhood is not
        // usable for this)
        public float triTop;
        // lowest top among all horizontal contacts touching the capsule: a low
        // plank at a tall wall touches the capsule too, and the deepest contact
        // may be the tall face. If any touching face is within the step budget,
        // the controller climbs (CCT top rule).
        public float lowTop;
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
        // game KG3DMesh [Display] bObscatleCamera (default 1); the camera
        // obstruction query skips meshes with false
        public bool blocksCamera = true;
        public int meshIndex = -1;
        // host proxy for the client's per-unit passability (bUnitWalkable /
        // bUnitCanPass values are not in the shipped files, G-21): objects
        // under the 小物件 taxonomy are solid volumes - the capsule is never
        // inside them. Set from the .meshes.txt sidecar at load.
        public bool propSolid = false;
    }

    sealed class Instance
    {
        public MeshData mesh;
        public bool fromFoliage;
        public float[] l2w;   // 16, row-major, row-vector: w = l * M
        public float[] w2l;   // 16, inverse
        public float m00, m01, m02, m10, m11, m12, m20, m21, m22; // 3x3 for world deltas
        public float maxLocalFromWorld; // for grid expansion
        public float minX, minY, minZ, maxX, maxY, maxZ; // world AABB
    }

    readonly List<Instance> _inst = new List<Instance>();
    readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
    readonly float _cell;
    List<int> _cand = new List<int>(64);
    readonly List<int> _stepScratch = new List<int>(64);
    readonly bool _useObstacleFlags;
    // meshes skipped because the shipped .mesh.ini says bAutoProduceObstacle=0
    public int NoObstacleSkipped;

    public int InstanceCount { get { return _inst.Count; } }
    public int MeshCount { get; private set; }
    public string LastEjectDbg = "";
    int _cameraFlagZero;
    string[] _meshPaths;   // optional mesh-index -> source model path sidecar

    public FoliageCollision(string foliagePath, string structurePath = null, float cellSize = 800f,
                            bool useObstacleFlags = true)
    {
        _cell = cellSize;
        _useObstacleFlags = useObstacleFlags;
        if (!string.IsNullOrEmpty(foliagePath) && File.Exists(foliagePath))
            LoadV1(foliagePath);
        if (!string.IsNullOrEmpty(structurePath) && File.Exists(structurePath))
            LoadV2(structurePath);
        BuildGrid();
    }

    // ---- loading ----

    void AddInstance(MeshData md, float[] m, float bminX, float bminY, float bminZ,
                     float bmaxX, float bmaxY, float bmaxZ, bool foliage = false)
    {
        var it = new Instance();
        it.mesh = md;
        it.fromFoliage = foliage;
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
                // bObscatleCamera from the game's mesh property inis (2026-09-28
                // extraction): rock patterns 6/7 ship =0 (the camera ignores
                // them); deadwood/cactus (4/5) have no ini -> ctor default 1
                meshes[pattern].blocksCamera = !(pattern == 6 || pattern == 7);
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
                            x + rad, y + md.maxY * s, z + rad, true);
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
            for (int i = 0; i < meshCount; i++) { meshes[i] = ReadMesh(r, 2); meshes[i].meshIndex = i; }
            // optional sidecar from tools/export_structure_collision.py:
            // mesh index -> source model path (diagnostics / named blockers)
            string mf = path + ".meshes.txt";
            if (File.Exists(mf))
            {
                try
                {
                    string[] lines = File.ReadAllLines(mf);
                    _meshPaths = new string[meshCount];
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string ln = lines[i];
                        int tab = ln.IndexOf('\t');
                        if (tab <= 0) continue;
                        int mi;
                        if (                        int.TryParse(ln.Substring(0, tab), out mi) && mi >= 0 && mi < meshCount)
                            _meshPaths[mi] = ln.Substring(tab + 1);
                    }
                    for (int i = 0; i < meshCount; i++)
                    {
                        string mp = _meshPaths[i];
                        if (mp == null) continue;
                        // volumetric furniture only (cabinets/crates/tables/
                        // barrels/vats): thin sheets (rugs, banners, bones)
                        // legitimately share walkable space and are excluded.
                        if (mp.IndexOf("\u67dc", StringComparison.Ordinal) >= 0 ||
                            mp.IndexOf("\u7bb1", StringComparison.Ordinal) >= 0 ||
                            mp.IndexOf("\u684c", StringComparison.Ordinal) >= 0 ||
                            mp.IndexOf("\u6876", StringComparison.Ordinal) >= 0 ||
                            mp.IndexOf("\u7f38", StringComparison.Ordinal) >= 0 ||
                            mp.IndexOf("\u575b", StringComparison.Ordinal) >= 0 ||
                            // sparse stacked props (log piles etc.): the render
                            // mesh has air gaps the capsule can slip through
                            // (wj_木堆001_hd, AABB y 787..1057); solid like the rest
                            mp.IndexOf("\u5806", StringComparison.Ordinal) >= 0)
                            meshes[i].propSolid = true;
                    }
                }
                catch { }
            }
            // per-mesh bObscatleCamera sidecar written by
            // tools/export_structure_collision.py (game default = 1)
            string cf = path + ".cflags";
            if (File.Exists(cf))
            {
                try
                {
                    using (var cf2 = new BinaryReader(File.OpenRead(cf)))
                    {
                        if (cf2.ReadUInt32() == 0x474C4643)
                        {
                            int n = cf2.ReadInt32();
                            for (int i = 0; i < n && i < meshCount; i++)
                            {
                                bool b = cf2.ReadByte() != 0;
                                meshes[i].blocksCamera = b;
                                if (!b) _cameraFlagZero++;
                            }
                        }
                    }
                }
                catch { }
            }
            // per-mesh obstacle-flag sidecar (plan P0) written by
            // tools/export_obstacle_flags.py; bit0 = bAutoProduceObstacle
            // ([Display]). Meshes with the flag 0 get NO physics - the shipped
            // game data says the engine does not auto-produce an obstacle for
            // them (no authored CollisionMesh sibling exists either).
            byte[] oflags = null;
            string of = path + ".oflags";
            if (_useObstacleFlags && File.Exists(of))
            {
                try
                {
                    byte[] raw = File.ReadAllBytes(of);
                    if (raw.Length >= 8 && BitConverter.ToUInt32(raw, 0) == 0x474C464F)
                    {
                        int n = (int)BitConverter.ToUInt32(raw, 4);
                        if (n == meshCount && raw.Length >= 8 + n)
                            oflags = raw;
                    }
                }
                catch { }
            }
            for (int i = 0; i < instCount; i++)
            {
                int mi = r.ReadInt32();
                var m = new float[16];
                for (int k = 0; k < 16; k++) m[k] = r.ReadSingle();
                r.ReadInt32();
                float bminX = r.ReadSingle(), bminY = r.ReadSingle(), bminZ = r.ReadSingle();
                float bmaxX = r.ReadSingle(), bmaxY = r.ReadSingle(), bmaxZ = r.ReadSingle();
                if (mi < 0 || mi >= meshCount) continue;
                // Engine auto-obstacle rule (KG3D_LoaderNoRenderX64 property
                // reader + .mesh.ini schema, 2026-10-01): the produced obstacle
                // is the union of LOD0 submeshes with bLogicObstacle=1; a mesh
                // with bAutoProduceObstacle=0 OR no logic-obstacle submesh
                // produces nothing - unless an authored collision sibling
                // exists (bit5), which the file-selection chain falls back to.
                if (oflags != null
                    && ((oflags[8 + mi] & 0x01) == 0 || (oflags[8 + mi] & 0x02) == 0)
                    && (oflags[8 + mi] & 0x20) == 0)
                {
                    NoObstacleSkipped++;
                    continue;
                }
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

    // Phase-1 A/B grid probe: true when a capsule contact exists at feet
    // position (x,y,z) with the given radius/height. Same InstanceContact
    // contract as the solver (py = feet, height = total capsule height).
    // Forcefaces: ignore the solid-prop AABB proxy and test the baked
    // triangles (the engine-side A/B cooks the same triangles).
    readonly List<int> _abCand = new List<int>(16);
    bool _abForceFaces = false;
    public int LastTouchInst = -1;
    public float LastTouchDepth = 0f;
    public bool CapsuleTouches(float x, float y, float z, float radius, float height)
    {
        GatherCandidates(x, z, radius + 64f, _abCand);
        bool found = false;
        _abForceFaces = true;
        try
        {
            for (int k = 0; k < _abCand.Count; k++)
            {
                Contact c = new Contact();
                c.lowTop = float.MaxValue;
                if (InstanceContact(_inst[_abCand[k]], x, y, z, radius, height, ref c) && c.depth > 0.01f)
                {
                    found = true;
                    LastTouchInst = _abCand[k];
                    LastTouchDepth = c.depth;
                    break;
                }
            }
        }
        finally { _abForceFaces = false; }
        return found;
    }

    // Nearest world-space hit of the segment A->B against the structure and
    // foliage instances (camera obstruction). Returns the distance from A
    // along A->B in world units, or -1 when nothing is hit.
    // frontFacesOnly (camera probes): skip back-facing hits. A probe whose
    // origin sits inside a mesh only finds that mesh's exit faces; treating
    // those as walls produced a phantom hit inches behind the anchor and a
    // negative (crossing) pull. The game's render-entity ray sees the drawn
    // front surface, so front faces are the faithful set.
    public int LastInst = -1;
    public int LastTri = -1;
    // penetration recorder support (RC_CAM_PENDBG): which mesh was hit and
    // whether the camera gate would have skipped it (cflags=0 foliage)
    public bool LastBlocksCamera = true;
    public bool LastFromFoliage = false;
    // blocked-contact recorder (RC_COL_PROF): the deepest side contact that
    // produced a push-out / blocked result in the last Resolve.
    public int LastBlockedInst = -1;
    public float LastBlockedDepth, LastBlockedNx, LastBlockedNy, LastBlockedNz, LastBlockedPy;
    public float LastBlockedTriTop;
    // profiling counters (always counted; cheap int adds)
    public long ProfInstTouches, ProfTriTests;

    public float Raycast(float ax, float ay, float az, float bx, float by, float bz,
                         bool structuresOnly = false, bool frontFacesOnly = false,
                         bool cameraGate = false)
    {
        LastInst = -1;
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-3f) return -1f;
        float minX = Math.Min(ax, bx), maxX = Math.Max(ax, bx);
        float minY = Math.Min(ay, by), maxY = Math.Max(ay, by);
        float minZ = Math.Min(az, bz), maxZ = Math.Max(az, bz);
        GatherCandidates((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f,
                         Math.Max(maxX - minX, maxZ - minZ) * 0.5f, _cand);
        float bestT = float.MaxValue;
        int bestInst = -1, bestTri = -1;
        bool bestBlocks = true, bestFol = false;
        for (int ci = 0; ci < _cand.Count; ci++)
        {
            Instance it = _inst[_cand[ci]];
            if (structuresOnly && it.fromFoliage) continue;
            // game camera query: FilterCamera skips meshes whose
            // bObscatleCamera is 0 (KG3DMesh display block)
                // flag=0 structures still block the camera (host deviation,
                // registered): the game fades bObscatleCamera=0 meshes, the
                // host has no fade yet, so ignoring them would see through a
                // visible wall (T2 inst 262 is exactly that case). The gate
                // therefore only removes flag=0 foliage (rocks/grass cards),
                // where the bake already carries the real blockers.
                if (cameraGate && it.fromFoliage && !it.mesh.blocksCamera) continue;
            if (it.maxY < minY || it.minY > maxY) continue;
            if (it.maxX < minX || it.minX > maxX) continue;
            if (it.maxZ < minZ || it.minZ > maxZ) continue;
            float[] w2l = it.w2l;
            if (w2l == null) continue;
            float lAx = ax * w2l[0] + ay * w2l[4] + az * w2l[8] + w2l[12];
            float lAy = ax * w2l[1] + ay * w2l[5] + az * w2l[9] + w2l[13];
            float lAz = ax * w2l[2] + ay * w2l[6] + az * w2l[10] + w2l[14];
            float lBx = bx * w2l[0] + by * w2l[4] + bz * w2l[8] + w2l[12];
            float lBy = bx * w2l[1] + by * w2l[5] + bz * w2l[9] + w2l[13];
            float lBz = bx * w2l[2] + by * w2l[6] + bz * w2l[10] + w2l[14];
            float ldx = lBx - lAx, ldy = lBy - lAy, ldz = lBz - lAz;
            MeshData md = it.mesh;
            float lminx = Math.Min(lAx, lBx), lmaxx = Math.Max(lAx, lBx);
            float lminz = Math.Min(lAz, lBz), lmaxz = Math.Max(lAz, lBz);
            int cx0 = (int)((lminx - md.gx0) / md.gcell);
            int cx1 = (int)((lmaxx - md.gx0) / md.gcell);
            int cz0 = (int)((lminz - md.gz0) / md.gcell);
            int cz1 = (int)((lmaxz - md.gz0) / md.gcell);
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
                        float t;
                        bool front;
                        if (!RayTri(md.verts, md.tris, md.cellTri[k],
                                    lAx, lAy, lAz, ldx, ldy, ldz, out t, out front)) continue;
                        if (frontFacesOnly && !front) continue;
                        if (t < bestT)
                        {
                            bestT = t;
                            bestInst = _cand[ci];
                            bestTri = md.cellTri[k];
                            bestBlocks = it.mesh.blocksCamera;
                            bestFol = it.fromFoliage;
                        }
                    }
                }
            }
        }
        LastInst = bestInst;
        LastTri = bestTri;
        LastBlocksCamera = bestBlocks;
        LastFromFoliage = bestFol;
        return bestT == float.MaxValue ? -1f : bestT * len;
    }

    // Moller-Trumbore; t in [0,1] along O + t*D. `front` is true for a hit on
    // the face's front side (ray travels against the triangle normal;
    // d.n = -det, so det > 0 is a front hit).
    static bool RayTri(float[] v, int[] tris, int tri,
                       float ox, float oy, float oz,
                       float dx, float dy, float dz, out float t, out bool front)
    {
        t = 0f;
        front = false;
        int i0 = tris[tri * 3] * 3, i1 = tris[tri * 3 + 1] * 3, i2 = tris[tri * 3 + 2] * 3;
        float e1x = v[i1] - v[i0], e1y = v[i1 + 1] - v[i0 + 1], e1z = v[i1 + 2] - v[i0 + 2];
        float e2x = v[i2] - v[i0], e2y = v[i2 + 1] - v[i0 + 1], e2z = v[i2 + 2] - v[i0 + 2];
        float px = dy * e2z - dz * e2y;
        float py = dz * e2x - dx * e2z;
        float pz = dx * e2y - dy * e2x;
        float det = e1x * px + e1y * py + e1z * pz;
        if (det > -1e-9f && det < 1e-9f) return false;
        float inv = 1f / det;
        float tx = ox - v[i0], ty = oy - v[i0 + 1], tz = oz - v[i0 + 2];
        float u = (tx * px + ty * py + tz * pz) * inv;
        if (u < 0f || u > 1f) return false;
        float qx = ty * e1z - tz * e1y;
        float qy = tz * e1x - tx * e1z;
        float qz = tx * e1y - ty * e1x;
        float w = (dx * qx + dy * qy + dz * qz) * inv;
        if (w < 0f || u + w > 1f) return false;
        t = (e2x * qx + e2y * qy + e2z * qz) * inv;
        front = det > 0f;
        return t >= 0f && t <= 1f;
    }

    // Closest point on segment AB to point P.
    static void ClosestPointOnSegment(float ax, float ay, float az, float bx, float by, float bz,
                                      float px, float py, float pz,
                                      out float qx, out float qy, out float qz)
    {
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float dd = dx * dx + dy * dy + dz * dz;
        float t = 0f;
        if (dd > 1e-12f)
        {
            t = ((px - ax) * dx + (py - ay) * dy + (pz - az) * dz) / dd;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
        }
        qx = ax + dx * t; qy = ay + dy * t; qz = az + dz * t;
    }

    // Closest points between segments P1Q1 and P2Q2 (Ericson, RTCD 5.1.9);
    // returns the squared distance; c1/c2 receive the closest points.
    static float ClosestPtSegmentSegment(
        float p1x, float p1y, float p1z, float q1x, float q1y, float q1z,
        float p2x, float p2y, float p2z, float q2x, float q2y, float q2z,
        out float c1x, out float c1y, out float c1z,
        out float c2x, out float c2y, out float c2z)
    {
        float d1x = q1x - p1x, d1y = q1y - p1y, d1z = q1z - p1z;
        float d2x = q2x - p2x, d2y = q2y - p2y, d2z = q2z - p2z;
        float rx = p1x - p2x, ry = p1y - p2y, rz = p1z - p2z;
        float a = d1x * d1x + d1y * d1y + d1z * d1z;
        float e = d2x * d2x + d2y * d2y + d2z * d2z;
        float f = d2x * rx + d2y * ry + d2z * rz;
        float s, t;
        if (a <= 1e-12f && e <= 1e-12f) { s = t = 0f; }
        else if (a <= 1e-12f) { s = 0f; t = f / e; if (t < 0f) t = 0f; else if (t > 1f) t = 1f; }
        else
        {
            float c = d1x * rx + d1y * ry + d1z * rz;
            if (e <= 1e-12f) { t = 0f; s = -c / a; if (s < 0f) s = 0f; else if (s > 1f) s = 1f; }
            else
            {
                float b = d1x * d2x + d1y * d2y + d1z * d2z;
                float denom = a * e - b * b;
                s = denom > 1e-12f ? (b * f - c * e) / denom : 0f;
                if (s < 0f) s = 0f; else if (s > 1f) s = 1f;
                t = (b * s + f) / e;
                if (t < 0f)
                {
                    t = 0f;
                    s = -c / a;
                    if (s < 0f) s = 0f; else if (s > 1f) s = 1f;
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = (b - c) / a;
                    if (s < 0f) s = 0f; else if (s > 1f) s = 1f;
                }
            }
        }
        c1x = p1x + d1x * s; c1y = p1y + d1y * s; c1z = p1z + d1z * s;
        c2x = p2x + d2x * t; c2y = p2y + d2y * t; c2z = p2z + d2z * t;
        float dx = c1x - c2x, dy = c1y - c2y, dz = c1z - c2z;
        return dx * dx + dy * dy + dz * dz;
    }

    // Exact capsule-axis (segment A->B) vs triangle contact. The closest pair
    // is found in local space (endpoint/face, vertex/segment, edge/edge, plus
    // a segment-triangle crossing test); the distance is then measured in
    // world space through the instance 3x3, so anisotropic scales stay exact.
    // Replaces the former 6-point axis sampling, which missed surfaces that
    // sit between two sample heights (e.g. railings at 15 u).
    // Capsule-vs-AABB (boxy capsule approximation) used for solid props: push
    // along the smallest overlap axis; the surface height is the box top so the
    // CCT step branch can only step onto the top within the step budget.
    bool AabbContact(Instance it, float px, float py, float pz,
                     float radius, float height, ref Contact best)
    {
        float ox = Math.Min(px + radius, it.maxX) - Math.Max(px - radius, it.minX);
        if (ox <= 0.01f) return false;
        float oz = Math.Min(pz + radius, it.maxZ) - Math.Max(pz - radius, it.minZ);
        if (oz <= 0.01f) return false;
        float oy = Math.Min(py + height, it.maxY) - Math.Max(py, it.minY);
        if (oy <= 0.01f) return false;
        float nx = 0f, ny = 0f, nz = 0f, depth;
        if (ox <= oz && ox <= oy)
        {
            nx = (px < (it.minX + it.maxX) * 0.5f) ? -1f : 1f;
            depth = ox;
        }
        else if (oz <= oy)
        {
            nz = (pz < (it.minZ + it.maxZ) * 0.5f) ? -1f : 1f;
            depth = oz;
        }
        else
        {
            ny = (py < (it.minY + it.maxY) * 0.5f) ? -1f : 1f;
            depth = oy;
        }
        best.nx = nx; best.ny = ny; best.nz = nz;
        best.depth = depth;
        best.py = it.maxY;
        best.triTop = it.maxY;
        best.lowTop = it.minY;
        return true;
    }

    bool InstanceContact(Instance it, float px, float py, float pz,
                         float radius, float height, ref Contact best)
    {
        // Solid props (registered host proxy) collide as their world AABB box:
        // the render mesh of a stacked prop has air gaps and tiered faces the
        // capsule can slip or step through (wj_木堆001_hd); a solid box blocks
        // like furniture and can only be climbed within the step budget.
        // Engine rule: static .mesh objects collide as their triangles (P1
        // research: the game cooks the render mesh as the obstacle). The old
        // AABB proxy blocked the empty corners of non-rectangular furniture
        // (field case 2026-10-02: wj_erg柜子001, nearest vertex 394 u from the
        // blocked corner) - props use faces like everything else.
        float[] w2l = it.w2l;
        if (w2l == null) return false;
        ProfInstTouches++;
        // Capsule convention: `py` is the FEET, `height` the total capsule
        // height. The axis runs between the end-sphere centers, i.e. from
        // py+radius to py+height-radius, so the capsule bottom sits exactly at
        // the feet (geometry at/below the floor can never touch it). The old
        // model used py..py+height as the axis, which extended the capsule a
        // full radius below the feet and made floor edges/thresholds collide.
        float segLen = height - 2f * radius;
        if (segLen < 0f) segLen = 0f;
        float lAx = px * w2l[0] + (py + radius) * w2l[4] + pz * w2l[8] + w2l[12];
        float lAy = px * w2l[1] + (py + radius) * w2l[5] + pz * w2l[9] + w2l[13];
        float lAz = px * w2l[2] + (py + radius) * w2l[6] + pz * w2l[10] + w2l[14];
        float lBx = lAx + segLen * w2l[4];
        float lBy = lAy + segLen * w2l[5];
        float lBz = lAz + segLen * w2l[6];

        MeshData md = it.mesh;
        bool found = false;
        float rLocal = radius * it.maxLocalFromWorld * 1.05f;
        // candidate cells around the whole segment
        float mnx = Math.Min(lAx, lBx) - rLocal, mxx = Math.Max(lAx, lBx) + rLocal;
        float mnz = Math.Min(lAz, lBz) - rLocal, mxz = Math.Max(lAz, lBz) + rLocal;
        int cx0 = (int)((mnx - md.gx0) / md.gcell);
        int cx1 = (int)((mxx - md.gx0) / md.gcell);
        int cz0 = (int)((mnz - md.gz0) / md.gcell);
        int cz1 = (int)((mxz - md.gz0) / md.gcell);
        if (cx0 < 0) cx0 = 0; if (cz0 < 0) cz0 = 0;
        if (cx1 >= md.gx) cx1 = md.gx - 1; if (cz1 >= md.gz) cz1 = md.gz - 1;
        if (cx0 > cx1 || cz0 > cz1) return false;
        float segDx = lBx - lAx, segDy = lBy - lAy, segDz = lBz - lAz;
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
                    ProfTriTests++;
                    int i0 = md.tris[tri * 3] * 3, i1 = md.tris[tri * 3 + 1] * 3, i2 = md.tris[tri * 3 + 2] * 3;
                    // closest pair, local space (pair and both points are
                    // committed together or not at all)
                    float cpx = 0f, cpy = 0f, cpz = 0f, qx = 0f, qy = 0f, qz = 0f;
                    float bestSq = float.MaxValue;
                    // segment endpoints vs triangle
                    {
                        float tx, ty, tz;
                        ClosestPointOnTri(md.verts, md.tris, tri, lAx, lAy, lAz, out tx, out ty, out tz);
                        float ax = lAx - tx, ay = lAy - ty, az = lAz - tz;
                        float dd = ax * ax + ay * ay + az * az;
                        if (dd < bestSq) { bestSq = dd; qx = lAx; qy = lAy; qz = lAz; cpx = tx; cpy = ty; cpz = tz; }
                        ClosestPointOnTri(md.verts, md.tris, tri, lBx, lBy, lBz, out tx, out ty, out tz);
                        ax = lBx - tx; ay = lBy - ty; az = lBz - tz;
                        dd = ax * ax + ay * ay + az * az;
                        if (dd < bestSq) { bestSq = dd; qx = lBx; qy = lBy; qz = lBz; cpx = tx; cpy = ty; cpz = tz; }
                    }
                    // triangle vertices vs segment
                    for (int v = 0; v < 3; v++)
                    {
                        int iv = md.tris[tri * 3 + v] * 3;
                        float tx, ty, tz;
                        ClosestPointOnSegment(lAx, lAy, lAz, lBx, lBy, lBz,
                            md.verts[iv], md.verts[iv + 1], md.verts[iv + 2],
                            out tx, out ty, out tz);
                        float ax = tx - md.verts[iv], ay = ty - md.verts[iv + 1], az = tz - md.verts[iv + 2];
                        float dd = ax * ax + ay * ay + az * az;
                        if (dd < bestSq)
                        {
                            bestSq = dd;
                            qx = tx; qy = ty; qz = tz;
                            cpx = md.verts[iv]; cpy = md.verts[iv + 1]; cpz = md.verts[iv + 2];
                        }
                    }
                    // triangle edges vs segment
                    for (int e = 0; e < 3; e++)
                    {
                        int ea = md.tris[tri * 3 + e] * 3;
                        int eb = md.tris[tri * 3 + (e + 1) % 3] * 3;
                        float tx, ty, tz, ex, ey, ez;
                        float sd2 = ClosestPtSegmentSegment(
                            lAx, lAy, lAz, lBx, lBy, lBz,
                            md.verts[ea], md.verts[ea + 1], md.verts[ea + 2],
                            md.verts[eb], md.verts[eb + 1], md.verts[eb + 2],
                            out tx, out ty, out tz, out ex, out ey, out ez);
                        if (sd2 < bestSq)
                        {
                            bestSq = sd2;
                            qx = tx; qy = ty; qz = tz;
                            cpx = ex; cpy = ey; cpz = ez;
                        }
                    }
                    // segment crossing the triangle face: distance 0
                    float ht;
                    bool hfront;
                    if (RayTri(md.verts, md.tris, tri, lAx, lAy, lAz, segDx, segDy, segDz, out ht, out hfront))
                    {
                        bestSq = 0f;
                        qx = lAx + segDx * ht; qy = lAy + segDy * ht; qz = lAz + segDz * ht;
                        cpx = qx; cpy = qy; cpz = qz;
                    }
                    // world-space distance through the instance 3x3
                    float dx = qx - cpx, dy = qy - cpy, dz = qz - cpz;
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
                        float abx = md.verts[i1] - md.verts[i0], aby = md.verts[i1 + 1] - md.verts[i0 + 1], abz = md.verts[i1 + 2] - md.verts[i0 + 2];
                        float acx = md.verts[i2] - md.verts[i0], acy = md.verts[i2 + 1] - md.verts[i0 + 1], acz = md.verts[i2 + 2] - md.verts[i0 + 2];
                        nx = aby * acz - abz * acy; ny = abz * acx - abx * acz; nz = abx * acy - aby * acx;
                        float wnx = nx * it.m00 + ny * it.m10 + nz * it.m20;
                        float wny = nx * it.m01 + ny * it.m11 + nz * it.m21;
                        float wnz = nx * it.m02 + ny * it.m12 + nz * it.m22;
                        float nl = (float)Math.Sqrt(wnx * wnx + wny * wny + wnz * wnz);
                        if (nl < 1e-9f) continue;
                        nx = wnx / nl; ny = wny / nl; nz = wnz / nl;
                        // orient toward the capsule axis midpoint
                        float amx = (lAx + lBx) * 0.5f, amy = (lAy + lBy) * 0.5f, amz = (lAz + lBz) * 0.5f;
                        float ox = amx - cpx, oy = amy - cpy, oz = amz - cpz;
                        if (nx * ox + ny * oy + nz * oz < 0f) { nx = -nx; ny = -ny; nz = -nz; }
                    }
                    if (!found || depth > best.depth)
                    {
                        best.nx = nx; best.ny = ny; best.nz = nz;
                        best.depth = depth;
                        best.py = cpx * it.l2w[1] + cpy * it.l2w[5] + cpz * it.l2w[9] + it.l2w[13];
                        // world top of the contacting triangle (CCT step test)
                        float v0y = md.verts[i0] * it.l2w[1] + md.verts[i0 + 1] * it.l2w[5] + md.verts[i0 + 2] * it.l2w[9] + it.l2w[13];
                        float v1y = md.verts[i1] * it.l2w[1] + md.verts[i1 + 1] * it.l2w[5] + md.verts[i1 + 2] * it.l2w[9] + it.l2w[13];
                        float v2y = md.verts[i2] * it.l2w[1] + md.verts[i2 + 1] * it.l2w[5] + md.verts[i2 + 2] * it.l2w[9] + it.l2w[13];
                        best.triTop = Math.Max(v0y, Math.Max(v1y, v2y));
                        found = true;
                    }
                    // low horizontal face touching the capsule (any depth)
                    if (Math.Sqrt(nx * nx + nz * nz) > 0.5f)
                    {
                        float v0y = md.verts[i0] * it.l2w[1] + md.verts[i0 + 1] * it.l2w[5] + md.verts[i0 + 2] * it.l2w[9] + it.l2w[13];
                        float v1y = md.verts[i1] * it.l2w[1] + md.verts[i1 + 1] * it.l2w[5] + md.verts[i1 + 2] * it.l2w[9] + it.l2w[13];
                        float v2y = md.verts[i2] * it.l2w[1] + md.verts[i2 + 1] * it.l2w[5] + md.verts[i2 + 2] * it.l2w[9] + it.l2w[13];
                        float ttop = Math.Max(v0y, Math.Max(v1y, v2y));
                        if (ttop < best.lowTop) best.lowTop = ttop;
                    }
                }
            }
        }
        return found;
    }

    public bool Resolve(ref float px, ref float py, ref float pz,
                        float radius, float height,
                        ref float ground, ref bool grounded,
                        float stepHeight = 70f, float vMotion = 0f,
                        float hMoveX = 0f, float hMoveZ = 0f)
    {
        bool blocked = false;
        for (int iter = 0; iter < 3; iter++)
        {
            Contact best = new Contact();
            best.lowTop = float.MaxValue;
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
                float horiz = (float)Math.Sqrt(best.nx * best.nx + best.nz * best.nz);
                if (horiz > 0.5f && grounded)
                {
                    // CCT step semantics (PxControllerDesc.stepOffset, recovered
                    // from PhysicsEngineX64; budget = the 64 u = 1 尺 ground
                    // tolerance): while GROUNDED, an obstacle whose top is
                    // within the budget never blocks - the controller climbs it.
                    // Airborne moves never step (the CCT steps only off a floor
                    // contact), otherwise a jump would let the player climb any
                    // wall whose top is below the current jump height.
                    // (The old lowTop fallback - step onto the lowest top of a
                    // contact cluster - was a host heuristic, census #4; the
                    // engine CCT steps onto the actual contact surface only.)
                    float top = best.triTop;
                    if (top > ground && top <= py + stepHeight)
                    {
                        // CCT up-sweep: the raise must actually clear the
                        // blocking face. If the same obstacle still overlaps
                        // the capsule at the raised height the "step" is a
                        // ledge on a continuing wall (field case: the Yumen
                        // building back wall ledge ladder y 975/990/1030/1040,
                        // 2026-09-30) - climbing it ratchets the player up the
                        // wall with the capsule embedded in the face.
                        // Engine CCT step (PxControllerDesc: stepOffset 0.5 m,
                        // slopeLimit 0.707): up-sweep clear -> forward sweep by
                        // the move at the raised height -> down-sweep lands on
                        // the highest surface within the step range; the landing
                        // must be walkable (slopeLimit). The queries use a
                        // scratch candidate list - the resolve loop iterates
                        // _cand.
                        System.Collections.Generic.List<int> savedCand = _cand;
                        _cand = _stepScratch;
                        try
                        {
                            float lox = px + hMoveX, loz = pz + hMoveZ;
                            if (!CapsuleBlocked(px, top + 0.1f, pz, radius, height)
                                && !CapsuleBlocked(lox, top + 0.1f, loz, radius, height))
                            {
                                // down-sweep: highest surface within the step
                                // range - the mesh support or the caller's
                                // terrain ground (the terrain is not in the
                                // collision mesh set).
                                float landY = ground;
                                float sh = SupportHeight(lox, loz, top - stepHeight, top + 0.1f);
                                bool meshLanding = sh > ground + 0.01f;
                                if (meshLanding) landY = sh;
                                if (landY > float.MinValue + 1f
                                    && (!meshLanding
                                        || SurfaceNormalY(lox, landY - 0.05f, loz, radius, height) >= 0.707f))
                                {
                                    px = lox; pz = loz;
                                    py = landY + 0.1f;
                                    ground = landY;
                                    grounded = true;
                                    stepUp = true;
                                }
                            }
                        }
                        finally { _cand = savedCand; }
                        if (!stepUp)
                        {
                            StepRejectCount++;
                            LastStepRejectTop = top;
                        }
                    }
                }
            }
            if (!stepUp && best.depth > 0.01f)
            {
                // Horizontal faces resist the vertical motion: a face hit while
                // rising must push DOWN, a face hit while falling must push UP.
                // Without this the degenerate normal can flip once the face
                // passes the capsule midpoint and eject the capsule through a
                // thin slab (field case: jumping up through a roof).
                if (vMotion != 0f && Math.Abs(best.ny) > 0.5f)
                    best.ny = -Math.Sign(vMotion) * Math.Abs(best.ny);
                // Vertical faces resist the horizontal motion the same way:
                // once the capsule centre crosses a thin wall, the degenerate
                // closest-point normal points ALONG the motion and the push
                // would eject it out the far side (field case: the 玉门关
                // building 001_002 wall at z~33870, 2026-09-30 walk-through).
                float hlen = (float)Math.Sqrt(hMoveX * hMoveX + hMoveZ * hMoveZ);
                if (hlen > 1e-4f && Math.Sqrt(best.nx * best.nx + best.nz * best.nz) > 0.5f)
                {
                    float hdot = (best.nx * hMoveX + best.nz * hMoveZ) / hlen;
                    if (hdot > 0.2f) { best.nx = -best.nx; best.nz = -best.nz; }
                }
                px += best.nx * best.depth;
                py += best.ny * best.depth;
                pz += best.nz * best.depth;
                // Creep guard (2026-10-02): a move larger than the push capacity
                // crosses a thin face (sprint into a wall = embedded in the mesh).
                // CCT slide: cancel only the advance BEYOND the push capacity; the
                // normal contact push+slide is untouched.
                if (bestIdx >= 0 && hMoveX * hMoveX + hMoveZ * hMoveZ > 1e-8f)
                {
                    float chx = best.nx, chz = best.nz;
                    float chl = (float)Math.Sqrt(chx * chx + chz * chz);
                    if (chl > 0.5f)
                    {
                        chx /= chl; chz /= chl;
                        if (chx * hMoveX + chz * hMoveZ > 0f) { chx = -chx; chz = -chz; }
                        float into = -(hMoveX * chx + hMoveZ * chz);
                        float excess = into - best.depth;
                        if (excess > 0f) { px += excess * chx; pz += excess * chz; }
                    }
                }
                float horiz = (float)Math.Sqrt(best.nx * best.nx + best.nz * best.nz);
                if (horiz > 0.5f)
                {
                    blocked = true;
                    LastBlockedInst = bestIdx;
                    LastBlockedDepth = best.depth;
                    LastBlockedNx = best.nx; LastBlockedNy = best.ny; LastBlockedNz = best.nz;
                    LastBlockedPy = best.py;
                    LastBlockedTriTop = best.triTop;
                }
            }
            // Support from a contact only when the surface is walkable: the
            // engine's slopeLimit is 0.707 (cos 45 deg, PxControllerDesc ctor);
            // steeper surfaces (log-pile curves, >45 deg slopes) never carry
            // the ground - the controller slides instead of climbing.
            if (best.ny > 0.707f && best.py > ground && best.py <= py + 60f)
            {
                ground = best.py;
                if (py <= ground + 2f) grounded = true;
            }
            if (stepUp) break;
        }
        return blocked;
    }

    // True when the capsule at (px,py,pz) overlaps any obstacle by more than
    // the contact skin. The CCT "does this position fit" test: the step branch
    // accepts a step only when the raised capsule is clear, and the caller
    // raises ground to a support surface only when the capsule can stand on it
    // (rejects overhang undersides of stepped walls).
    public float LastProbeDepth, LastProbePy, LastProbeTriTop;
    public int LastProbeInst = -1;
    public int StepRejectCount;
    public float LastStepRejectTop;
    public bool CapsuleBlocked(float px, float py, float pz, float radius, float height)
    {
        GatherCandidates(px, pz, radius + 600f, _cand);
        for (int k = 0; k < _cand.Count; k++)
        {
            Instance it = _inst[_cand[k]];
            if (py + height < it.minY - 60f || py > it.maxY + 60f) continue;
            if (px < it.minX - radius || px > it.maxX + radius) continue;
            if (pz < it.minZ - radius || pz > it.maxZ + radius) continue;
            Contact probe = new Contact();
            probe.lowTop = float.MaxValue;
            if (InstanceContact(it, px, py, pz, radius, height, ref probe) && probe.depth > 0.1f)
            {
                LastProbeDepth = probe.depth;
                LastProbePy = probe.py;
                LastProbeTriTop = probe.triTop;
                LastProbeInst = _cand[k];
                return true;
            }
        }
        return false;
    }

    // Support-raise variant of the fit test: only a DOWNWARD-facing contact
    // means the capsule is under an overhang (a stepped wall's molding) and
    // the raised surface is not standable. Horizontal contacts are side
    // overlaps a solid prop pushes out afterwards (field case: the 龙门寻宝
    // rug beside wj_木堆/道具 inst 575 - the strict test rejected the drop to
    // the rug and the player bounced airborne on the prop edge, 2026-09-30).
    public bool CapsuleBlockedDown(float px, float py, float pz, float radius, float height)
    {
        GatherCandidates(px, pz, radius + 600f, _cand);
        for (int k = 0; k < _cand.Count; k++)
        {
            Instance it = _inst[_cand[k]];
            if (py + height < it.minY - 60f || py > it.maxY + 60f) continue;
            if (px < it.minX - radius || px > it.maxX + radius) continue;
            if (pz < it.minZ - radius || pz > it.maxZ + radius) continue;
            Contact probe = new Contact();
            probe.lowTop = float.MaxValue;
            if (InstanceContact(it, px, py, pz, radius, height, ref probe) &&
                probe.depth > 0.1f && probe.ny < -0.3f)
            {
                LastProbeDepth = probe.depth;
                LastProbePy = probe.py;
                LastProbeTriTop = probe.triTop;
                LastProbeInst = _cand[k];
                return true;
            }
        }
        return false;
    }

    // Landing surface slope (engine PxControllerDesc ctor: slopeLimit 0.707 =
    // cos 45 deg). Returns the most up-facing contact normal under the capsule;
    // the step branch rejects landings steeper than the limit (log-pile curved
    // tops: 51% of the up faces are steeper - the engine controller would not
    // climb them).
    public float SurfaceNormalY(float px, float py, float pz, float radius, float height)
    {
        GatherCandidates(px, pz, radius + 600f, _cand);
        float bestNy = 0f;
        for (int k = 0; k < _cand.Count; k++)
        {
            Instance it = _inst[_cand[k]];
            if (py + height < it.minY - 60f || py > it.maxY + 60f) continue;
            if (px < it.minX - radius || px > it.maxX + radius) continue;
            if (pz < it.minZ - radius || pz > it.maxZ + radius) continue;
            Contact probe = new Contact();
            probe.lowTop = float.MaxValue;
            if (InstanceContact(it, px, py, pz, radius, height, ref probe) && probe.depth > 0.01f)
            {
                if (probe.ny > bestNy) bestNy = probe.ny;
            }
        }
        return bestNy;
    }

    // Offline wall-sweep audit (map-wide walk-through detector): every
    // wall-like triangle must stop a capsule sweep aimed at it from either
    // side. Runs the real contact/resolve path (grid lookup included), so
    // walk-through classes are found map-wide without the engine.
    // stride > 1 samples every Nth wall face for fast triage.
    public int AuditWalls(List<string> report, int maxReport,
                          float radius, float height, float stepHeight,
                          out int tested, int stride = 1)
    {
        int failed = 0, seen = 0;
        tested = 0;
        for (int ii = 0; ii < _inst.Count; ii++)
        {
            Instance it = _inst[ii];
            MeshData md = it.mesh;
            if (md == null || it.l2w == null || md.tris == null) continue;
            if (md.propSolid) continue;   // props use the AABB proxy, not faces
            int triCount = md.tris.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                int i0 = md.tris[t * 3] * 3, i1 = md.tris[t * 3 + 1] * 3, i2 = md.tris[t * 3 + 2] * 3;
                float ax = md.verts[i0] * it.l2w[0] + md.verts[i0 + 1] * it.l2w[4] + md.verts[i0 + 2] * it.l2w[8] + it.l2w[12];
                float ay = md.verts[i0] * it.l2w[1] + md.verts[i0 + 1] * it.l2w[5] + md.verts[i0 + 2] * it.l2w[9] + it.l2w[13];
                float az = md.verts[i0] * it.l2w[2] + md.verts[i0 + 1] * it.l2w[6] + md.verts[i0 + 2] * it.l2w[10] + it.l2w[14];
                float bx = md.verts[i1] * it.l2w[0] + md.verts[i1 + 1] * it.l2w[4] + md.verts[i1 + 2] * it.l2w[8] + it.l2w[12];
                float by = md.verts[i1] * it.l2w[1] + md.verts[i1 + 1] * it.l2w[5] + md.verts[i1 + 2] * it.l2w[9] + it.l2w[13];
                float bz = md.verts[i1] * it.l2w[2] + md.verts[i1 + 1] * it.l2w[6] + md.verts[i1 + 2] * it.l2w[10] + it.l2w[14];
                float cx = md.verts[i2] * it.l2w[0] + md.verts[i2 + 1] * it.l2w[4] + md.verts[i2 + 2] * it.l2w[8] + it.l2w[12];
                float cy = md.verts[i2] * it.l2w[1] + md.verts[i2 + 1] * it.l2w[5] + md.verts[i2 + 2] * it.l2w[9] + it.l2w[13];
                float cz = md.verts[i2] * it.l2w[2] + md.verts[i2 + 1] * it.l2w[6] + md.verts[i2 + 2] * it.l2w[10] + it.l2w[14];
                float ux = bx - ax, uy = by - ay, uz = bz - az;
                float vx = cx - ax, vy = cy - ay, vz = cz - az;
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl < 1e-6f) continue;
                if ((float)Math.Sqrt(nx * nx + nz * nz) / nl < 0.5f) continue; // walls only
                float ymin = Math.Min(ay, Math.Min(by, cy));
                float ymax = Math.Max(ay, Math.Max(by, cy));
                if (ymax - ymin < 1f) continue;
                seen++;
                if (stride > 1 && (seen % stride) != 0) continue;
                float hnx = nx, hnz = nz;
                float hl = (float)Math.Sqrt(hnx * hnx + hnz * hnz);
                hnx /= hl; hnz /= hl;
                float fcx = (ax + bx + cx) / 3f, fcz = (az + bz + cz) / 3f;
                for (int hh = 0; hh < 3; hh++)
                {
                    float fy = ymin + (ymax - ymin) * (0.25f + 0.25f * hh);
                    float feet = fy - height * 0.5f;
                    for (int side = 0; side < 2; side++)
                    {
                        float sgn = side == 0 ? 1f : -1f;
                        float px = fcx + hnx * sgn * (radius + 6f);
                        float pz = fcz + hnz * sgn * (radius + 6f);
                        float py = feet;
                        // approach must be clear: if other geometry of this
                        // instance already overlaps the start position the
                        // face cannot be tested in isolation (dense stacks)
                        Contact c0 = new Contact();
                        c0.lowTop = float.MaxValue;
                        if (InstanceContact(it, px, py, pz, radius, height, ref c0) && c0.depth > 0.5f)
                            continue;
                        float dx = -hnx * sgn * (2f * radius + 20f);
                        float dz = -hnz * sgn * (2f * radius + 20f);
                        // mini-resolve against THIS instance only (same
                        // contact/push contract as Resolve; skips the global
                        // candidate scan for audit speed). Airborne: no CCT
                        // step-up - a wall face must BLOCK, stepping over low
                        // faces is correct behavior and not a walk-through.
                        float ground = py;
                        bool grounded = false;
                        float len = (float)Math.Sqrt(dx * dx + dz * dz);
                        float subCap = radius * 0.9f;
                        if (subCap < 1f) subCap = 1f;
                        int n = (int)Math.Ceiling(len / subCap);
                        if (n < 1) n = 1;
                        for (int s2 = 0; s2 < n; s2++)
                        {
                            px += dx / n; pz += dz / n;
                            for (int iter = 0; iter < 3; iter++)
                            {
                                Contact best = new Contact();
                                best.lowTop = float.MaxValue;
                                if (!InstanceContact(it, px, py, pz, radius, height, ref best)) break;
                                if (best.depth <= 0.01f) break;
                                float horiz = (float)Math.Sqrt(best.nx * best.nx + best.nz * best.nz);
                                bool stepped = false;
                                if (horiz > 0.5f && grounded)
                                {
                                    float top = best.triTop;
                                    if (top > py + stepHeight && best.lowTop <= py + stepHeight)
                                        top = best.lowTop;
                                    if (top > ground && top <= py + stepHeight)
                                    {
                                        Contact probe = new Contact();
                                        probe.lowTop = float.MaxValue;
                                        if (!InstanceContact(it, px, top + 0.1f, pz, radius, height, ref probe)
                                            || probe.depth <= 0.1f)
                                        {
                                            py = top + 0.1f;
                                            ground = top;
                                            stepped = true;
                                        }
                                    }
                                }
                                if (stepped) break;
                                if (horiz > 0.5f)
                                {
                                    float hdot = (best.nx * dx + best.nz * dz) / (len + 1e-6f);
                                    if (hdot > 0.2f) { best.nx = -best.nx; best.nz = -best.nz; }
                                }
                                px += best.nx * best.depth;
                                py += best.ny * best.depth;
                                pz += best.nz * best.depth;
                            }
                        }
                        float sd = (px - fcx) * hnx * sgn + (pz - fcz) * hnz * sgn;
                        tested++;
                        if (sd < -8f)
                        {
                            failed++;
                            if (report != null && report.Count < maxReport)
                            {
                                // diag: where the w2l path puts the face centroid vs
                                // the triangle's own local verts (inverse check)
                                float dlx = fcx * it.w2l[0] + fy * it.w2l[4] + fcz * it.w2l[8] + it.w2l[12];
                                float dly = fcx * it.w2l[1] + fy * it.w2l[5] + fcz * it.w2l[9] + it.w2l[13];
                                float dlz = fcx * it.w2l[2] + fy * it.w2l[6] + fcz * it.w2l[10] + it.w2l[14];
                                float lcx = (md.verts[i0] + md.verts[i1] + md.verts[i2]) / 3f;
                                float lcy = (md.verts[i0 + 1] + md.verts[i1 + 1] + md.verts[i2 + 1]) / 3f;
                                float lcz = (md.verts[i0 + 2] + md.verts[i1 + 2] + md.verts[i2 + 2]) / 3f;
                                report.Add(string.Format(
                                    "FAIL inst={0} mesh={1} bb x{2:F0}..{3:F0} z{4:F0}..{5:F0} tri={6} top={7:F0} face=({8:F0},{9:F0},{10:F0}) sd={11:F1} w2lLoc=({12:F0},{13:F0},{14:F0}) triLoc=({15:F0},{16:F0},{17:F0})",
                                    ii, md.meshIndex, it.minX, it.maxX, it.minZ, it.maxZ, t, ymax, fcx, fy, fcz, sd,
                                    dlx, dly, dlz, lcx, lcy, lcz));
                            }
                        }
                    }
                }
            }
        }
        return failed;
    }

    // Moves the capsule by (dx,dz) in substeps of at most maxSubStep,    // resolving contacts after each substep, so a move longer than the
    // capsule radius cannot tunnel through a thin collider. Terrain ground
    // and slope decisions stay with the caller; ref ground/grounded follow
    // the same Resolve semantics. Returns true when any substep was blocked.
    public bool MoveResolved(ref float px, ref float py, ref float pz,
                             float dx, float dz, float radius, float height,
                             float maxSubStep, ref float ground, ref bool grounded,
                             float stepHeight = 70f)
    {
        float len = (float)Math.Sqrt(dx * dx + dz * dz);
        int n = 1;
        if (maxSubStep > 0.01f && len > maxSubStep)
        {
            n = (int)Math.Ceiling(len / maxSubStep);
            if (n > 64) n = 64;
        }
        bool blocked = false;
        for (int i = 0; i < n; i++)
        {
            px += dx / n;
            pz += dz / n;
            if (Resolve(ref px, ref py, ref pz, radius, height, ref ground, ref grounded, stepHeight, 0f, dx / n, dz / n))
                blocked = true;
        }
        return blocked;
    }

    // Highest world-space vertex Y of an instance's geometry inside a square of
    // half-size `half` around (x, z): the obstacle top used by CCT step
    // semantics (top within stepOffset => never blocks). Works for merged
    // meshes whose low features carry no up-facing triangle.
    public float LocalTop(int instIdx, float x, float z, float half)
    {
        if (instIdx < 0 || instIdx >= _inst.Count) return float.MinValue;
        Instance it = _inst[instIdx];
        float[] w2l = it.w2l;
        if (w2l == null) return float.MinValue;
        MeshData md = it.mesh;
        float lx0 = float.MaxValue, lz0 = float.MaxValue, lx1 = float.MinValue, lz1 = float.MinValue;
        for (int c = 0; c < 4; c++)
        {
            float wx = x + ((c & 1) == 0 ? -half : half);
            float wz = z + ((c & 2) == 0 ? -half : half);
            float lx = wx * w2l[0] + wz * w2l[8] + w2l[12];
            float lz = wx * w2l[2] + wz * w2l[10] + w2l[14];
            if (lx < lx0) lx0 = lx; if (lx > lx1) lx1 = lx;
            if (lz < lz0) lz0 = lz; if (lz > lz1) lz1 = lz;
        }
        int cx0 = (int)((lx0 - md.gx0) / md.gcell), cx1 = (int)((lx1 - md.gx0) / md.gcell);
        int cz0 = (int)((lz0 - md.gz0) / md.gcell), cz1 = (int)((lz1 - md.gz0) / md.gcell);
        if (cx0 < 0) cx0 = 0; if (cz0 < 0) cz0 = 0;
        if (cx1 >= md.gx) cx1 = md.gx - 1; if (cz1 >= md.gz) cz1 = md.gz - 1;
        if (cx0 > cx1 || cz0 > cz1) return float.MinValue;
        float best = float.MinValue;
        for (int cz = cz0; cz <= cz1; cz++)
        {
            int rowBase = cz * md.gx;
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = rowBase + cx;
                for (int k = md.cellStart[c]; k < md.cellStart[c + 1]; k++)
                {
                    int t = md.cellTri[k];
                    int i0 = md.tris[t * 3] * 3, i1 = md.tris[t * 3 + 1] * 3, i2 = md.tris[t * 3 + 2] * 3;
                    // tight world-XZ AABB test against the probe square (grid
                    // cells are loose: a triangle may span many cells)
                    float ax = md.verts[i0] * it.l2w[0] + md.verts[i0 + 1] * it.l2w[4] + md.verts[i0 + 2] * it.l2w[8] + it.l2w[12];
                    float az = md.verts[i0] * it.l2w[2] + md.verts[i0 + 1] * it.l2w[6] + md.verts[i0 + 2] * it.l2w[10] + it.l2w[14];
                    float bx = md.verts[i1] * it.l2w[0] + md.verts[i1 + 1] * it.l2w[4] + md.verts[i1 + 2] * it.l2w[8] + it.l2w[12];
                    float bz = md.verts[i1] * it.l2w[2] + md.verts[i1 + 1] * it.l2w[6] + md.verts[i1 + 2] * it.l2w[10] + it.l2w[14];
                    float dx = md.verts[i2] * it.l2w[0] + md.verts[i2 + 1] * it.l2w[4] + md.verts[i2 + 2] * it.l2w[8] + it.l2w[12];
                    float dz = md.verts[i2] * it.l2w[2] + md.verts[i2 + 1] * it.l2w[6] + md.verts[i2 + 2] * it.l2w[10] + it.l2w[14];
                    float tminx = Math.Min(ax, Math.Min(bx, dx)), tmaxx = Math.Max(ax, Math.Max(bx, dx));
                    float tminz = Math.Min(az, Math.Min(bz, dz)), tmaxz = Math.Max(az, Math.Max(bz, dz));
                    if (tmaxx < x - half || tminx > x + half) continue;
                    if (tmaxz < z - half || tminz > z + half) continue;
                    float ay = md.verts[i0] * it.l2w[1] + md.verts[i0 + 1] * it.l2w[5] + md.verts[i0 + 2] * it.l2w[9] + it.l2w[13];
                    float by = md.verts[i1] * it.l2w[1] + md.verts[i1 + 1] * it.l2w[5] + md.verts[i1 + 2] * it.l2w[9] + it.l2w[13];
                    float dy = md.verts[i2] * it.l2w[1] + md.verts[i2 + 1] * it.l2w[5] + md.verts[i2 + 2] * it.l2w[9] + it.l2w[13];
                    float ty = Math.Max(ay, Math.Max(by, dy));
                    if (ty > best) best = ty;
                }
            }
        }
        return best;
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
                // Floor query: winding is a rendering property, not a physics
                // one (engine GetFloorHeight / CCT floor test). Field case:
                // wj_erg地毯001_hd (龙门寻宝 house rug, inst 485) is a ~800 u
                // plate authored with inverted winding (ny=-1); requiring
                // wny>0 made the client step off the rug onto terrain and the
                // 64 u drop-tolerance snapped the player back down every frame.
                if (nl < 1e-9f || Math.Abs(wny) / nl < 0.5f) continue;
                float wy = lx * it.l2w[1] + ly * it.l2w[5] + lz * it.l2w[9] + it.l2w[13];
                if (wy < yLow || wy > yHigh) continue;
                if (wy > best) best = wy;
            }
        }
        return best;
    }

    // Host proxy (registered) for the client's per-unit passability: the
    // bUnitWalkable/bUnitCanPass values for placed objects are not in the
    // shipped files (G-21), but the objects are. Volumetric furniture
    // (cabinet/crate/table/barrel/vat mesh classes) is solid: when the capsule
    // overlaps a prop's world AABB, it is pushed out along the minimum-
    // translation axis — a wall-like contact (small per-frame push), not an
    // ejection. Buildings keep mesh-shell collision; thin sheets are excluded
    // by the near-geometry gate; the shipped meshes use inverted winding, so
    // no winding is used anywhere here.
    public bool SolidPropPush(ref float px, ref float py, ref float pz,
                              float radius, float height, float ground,
                              bool grounded, float stepHeight,
                              out int outInst)
    {
        outInst = -1;
        LastEjectDbg = "";
        float cy = py + height * 0.5f;
        GatherCandidates(px, pz, 300f, _cand);
        for (int ci = 0; ci < _cand.Count; ci++)
        {
            Instance it = _inst[_cand[ci]];
            if (!it.mesh.propSolid) continue;
            if (!NearGeometry(it, px, cy, pz, 80f)) continue;
            float ox = Math.Min(px + radius, it.maxX) - Math.Max(px - radius, it.minX);
            if (ox <= 0.01f) continue;
            float oz = Math.Min(pz + radius, it.maxZ) - Math.Max(pz - radius, it.minZ);
            if (oz <= 0.01f) continue;
            float oy = Math.Min(py + height, it.maxY) - Math.Max(py, it.minY);
            if (oy <= 0.01f) continue;
            // CCT step semantics: while GROUNDED, a prop whose top is within the
            // step budget is stepped onto - never ejected sideways (field case
            // 2026-10-02: 30 u props in the house pushed the sprinting player).
            if (grounded && it.maxY > py + 0.1f && it.maxY - py <= stepHeight)
            {
                py = it.maxY;
                LastEjectDbg += string.Format(" step oy={0:F0}", oy);
                outInst = _cand[ci];
                return true;
            }
            if (ox <= oz && ox <= oy)
            {
                float cx2 = (it.minX + it.maxX) * 0.5f;
                px = (px < cx2) ? it.minX - radius : it.maxX + radius;
            }
            else if (oz <= oy)
            {
                float cz2 = (it.minZ + it.maxZ) * 0.5f;
                pz = (pz < cz2) ? it.minZ - radius : it.maxZ + radius;
            }
            else
            {
                float cy2 = (it.minY + it.maxY) * 0.5f;
                float down = it.minY - height;
                if (py < cy2 && down >= ground - 1f) py = down;   // never underground
                else py = it.maxY;                                 // step onto the top
            }
            LastEjectDbg += string.Format(" push ox={0:F0} oy={1:F0} oz={2:F0}", ox, oy, oz);
            outInst = _cand[ci];
            return true;
        }
        return false;
    }

    bool NearGeometry(Instance it, float x, float y, float z, float margin)
    {
        float[] w2l = it.w2l;
        if (w2l == null) return false;
        float lx = x * w2l[0] + y * w2l[4] + z * w2l[8] + w2l[12];
        float ly = x * w2l[1] + y * w2l[5] + z * w2l[9] + w2l[13];
        float lz = x * w2l[2] + y * w2l[6] + z * w2l[10] + w2l[14];
        MeshData md = it.mesh;
        float limLoc = margin * it.maxLocalFromWorld;
        float limSq = limLoc * limLoc;
        int cx0 = (int)((lx - limLoc - md.gx0) / md.gcell);
        int cx1 = (int)((lx + limLoc - md.gx0) / md.gcell);
        int cz0 = (int)((lz - limLoc - md.gz0) / md.gcell);
        int cz1 = (int)((lz + limLoc - md.gz0) / md.gcell);
        if (cx0 < 0) cx0 = 0; if (cz0 < 0) cz0 = 0;
        if (cx1 >= md.gx) cx1 = md.gx - 1; if (cz1 >= md.gz) cz1 = md.gz - 1;
        if (cx0 > cx1 || cz0 > cz1) return false;
        for (int cz = cz0; cz <= cz1; cz++)
        {
            int rowBase = cz * md.gx;
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int c = rowBase + cx;
                int s0 = md.cellStart[c], s1 = md.cellStart[c + 1];
                for (int k = s0; k < s1; k++)
                {
                    float tx, ty, tz;
                    ClosestPointOnTri(md.verts, md.tris, md.cellTri[k], lx, ly, lz,
                                      out tx, out ty, out tz);
                    float dx = lx - tx, dy = ly - ty, dz = lz - tz;
                    if (dx * dx + dy * dy + dz * dz < limSq) return true;
                }
            }
        }
        return false;
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

    // diagnostics: source model path of the mesh behind an instance/mesh index
    public int GetInstanceMesh(int idx)
    {
        if (idx < 0 || idx >= _inst.Count) return -1;
        return _inst[idx].mesh.meshIndex;
    }

    public string GetMeshPath(int meshIndex)
    {
        if (_meshPaths == null || meshIndex < 0 || meshIndex >= _meshPaths.Length) return null;
        return _meshPaths[meshIndex];
    }

    public string Describe()
    {
        return string.Format("instances={0} meshes={1} camflag0={2} noObstacle={3}",
            _inst.Count, MeshCount, _cameraFlagZero, NoObstacleSkipped);
    }
}
