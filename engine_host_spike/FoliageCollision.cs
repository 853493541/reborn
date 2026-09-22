// Real foliage structure collision for the map host.
//
// Data comes from tools/export_foliage_collision.py (foliage_collision.bin),
// which decodes the game's .foliage instance files (SceneManagerx64.dll
// SceneFileLoader_Foliage_Binary) and pairs solid patterns (deadwood, cactus,
// rocks) with their real .mesh geometry.
//
// Collision: the player is a vertical capsule; nearby instances are tested
// triangle-by-triangle (mesh local space, yaw about +Y, uniform scale),
// the deepest contact pushes the player out and up-facing contacts provide
// stand-on ground (walkable rock slabs).

using System;
using System.Collections.Generic;
using System.IO;

public sealed class FoliageCollision
{
    const uint MAGIC = 0x4C4F4346; // 'FCOL'

    public struct Contact
    {
        public float nx, ny, nz;   // world normal (unit, points toward player)
        public float depth;        // world penetration depth
        public float py;           // world y of the contact point
    }

    sealed class MeshData
    {
        public int pattern;
        public float sceneScale;
        public float[] verts;      // 3 per vertex
        public int[] tris;         // 3 per triangle
        public float[] triMin;     // 3 per triangle
        public float[] triMax;     // 3 per triangle
        public float minX, minY, minZ, maxX, maxY, maxZ;
    }

    struct Instance
    {
        public int pattern;
        public float x, y, z, yaw, scale;
        public float cos, sin;
        public float radius;       // world horizontal radius (mesh AABB)
        public float hMin, hMax;   // world vertical extent
        public MeshData mesh;
    }

    readonly List<Instance> _inst = new List<Instance>();
    readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
    readonly float _cell;

    public int InstanceCount { get { return _inst.Count; } }
    public int MeshCount { get; private set; }

    public FoliageCollision(string path, float cellSize = 800f)
    {
        _cell = cellSize;
        byte[] d = File.ReadAllBytes(path);
        int o = 0;
        uint magic = BitConverter.ToUInt32(d, o); o += 4;
        if (magic != MAGIC) throw new InvalidDataException("bad FCOL magic");
        uint version = BitConverter.ToUInt32(d, o); o += 4;
        if (version != 1) throw new InvalidDataException("bad FCOL version");
        int meshCount = (int)BitConverter.ToUInt32(d, o); o += 4;
        int instCount = (int)BitConverter.ToUInt32(d, o); o += 4;
        MeshCount = meshCount;

        var meshes = new Dictionary<int, MeshData>();
        for (int m = 0; m < meshCount; m++)
        {
            var md = new MeshData();
            md.pattern = (int)BitConverter.ToUInt32(d, o); o += 4;
            md.sceneScale = BitConverter.ToSingle(d, o); o += 4;
            int vc = (int)BitConverter.ToUInt32(d, o); o += 4;
            int tc = (int)BitConverter.ToUInt32(d, o); o += 4;
            md.verts = new float[vc * 3];
            Buffer.BlockCopy(d, o, md.verts, 0, vc * 12); o += vc * 12;
            md.tris = new int[tc * 3];
            Buffer.BlockCopy(d, o, md.tris, 0, tc * 12); o += tc * 12;
            md.triMin = new float[tc * 3];
            md.triMax = new float[tc * 3];
            md.minX = md.minY = md.minZ = float.MaxValue;
            md.maxX = md.maxY = md.maxZ = float.MinValue;
            for (int i = 0; i < vc; i++)
            {
                float x = md.verts[i * 3], y = md.verts[i * 3 + 1], z = md.verts[i * 3 + 2];
                if (x < md.minX) md.minX = x; if (x > md.maxX) md.maxX = x;
                if (y < md.minY) md.minY = y; if (y > md.maxY) md.maxY = y;
                if (z < md.minZ) md.minZ = z; if (z > md.maxZ) md.maxZ = z;
            }
            for (int t = 0; t < tc; t++)
            {
                float mnx = float.MaxValue, mny = float.MaxValue, mnz = float.MaxValue;
                float mxx = float.MinValue, mxy = float.MinValue, mxz = float.MinValue;
                for (int k = 0; k < 3; k++)
                {
                    int vi = md.tris[t * 3 + k] * 3;
                    float x = md.verts[vi], y = md.verts[vi + 1], z = md.verts[vi + 2];
                    if (x < mnx) mnx = x; if (x > mxx) mxx = x;
                    if (y < mny) mny = y; if (y > mxy) mxy = y;
                    if (z < mnz) mnz = z; if (z > mxz) mxz = z;
                }
                md.triMin[t * 3] = mnx; md.triMin[t * 3 + 1] = mny; md.triMin[t * 3 + 2] = mnz;
                md.triMax[t * 3] = mxx; md.triMax[t * 3 + 1] = mxy; md.triMax[t * 3 + 2] = mxz;
            }
            meshes[md.pattern] = md;
        }

        for (int i = 0; i < instCount; i++)
        {
            var it = new Instance();
            it.pattern = (int)BitConverter.ToUInt32(d, o); o += 4;
            it.x = BitConverter.ToSingle(d, o); o += 4;
            it.y = BitConverter.ToSingle(d, o); o += 4;
            it.z = BitConverter.ToSingle(d, o); o += 4;
            it.yaw = BitConverter.ToSingle(d, o); o += 4;
            it.scale = BitConverter.ToSingle(d, o); o += 4;
            MeshData md;
            if (!meshes.TryGetValue(it.pattern, out md)) continue;
            it.mesh = md;
            it.cos = (float)Math.Cos(it.yaw);
            it.sin = (float)Math.Sin(it.yaw);
            float ex = Math.Max(Math.Abs(md.minX), Math.Abs(md.maxX));
            float ez = Math.Max(Math.Abs(md.minZ), Math.Abs(md.maxZ));
            it.radius = (float)Math.Sqrt(ex * ex + ez * ez) * it.scale;
            it.hMin = it.y + md.minY * it.scale;
            it.hMax = it.y + md.maxY * it.scale;
            _inst.Add(it);
        }
        BuildGrid();
    }

    long CellKey(int cx, int cz)
    {
        return ((long)cx << 32) ^ (uint)cz;
    }

    void BuildGrid()
    {
        for (int i = 0; i < _inst.Count; i++)
        {
            Instance it = _inst[i];
            int cx0 = (int)Math.Floor((it.x - it.radius) / _cell);
            int cx1 = (int)Math.Floor((it.x + it.radius) / _cell);
            int cz0 = (int)Math.Floor((it.z - it.radius) / _cell);
            int cz1 = (int)Math.Floor((it.z + it.radius) / _cell);
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

    // ---- vector helpers (local mesh space) ----

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

    // Finds the deepest capsule contact against one instance (local space test).
    bool InstanceContact(ref Instance it, float px, float py, float pz,
                         float radius, float height, ref Contact best)
    {
        float inv = 1f / it.scale;
        float rl = radius * inv;
        // world -> local (undo yaw, then offset, then scale)
        float dx = px - it.x, dy = py - it.y, dz = pz - it.z;
        float lx = (dx * it.cos - dz * it.sin) * inv;
        float ly = dy * inv;
        float lz = (dx * it.sin + dz * it.cos) * inv;

        int samples = 6;
        bool found = false;
        for (int s = 0; s < samples; s++)
        {
            float t = samples == 1 ? 0f : (float)s / (samples - 1);
            float qx = lx, qy = ly + t * height * inv, qz = lz;
            float qminx = qx - rl, qminy = qy - rl, qminz = qz - rl;
            float qmaxx = qx + rl, qmaxy = qy + rl, qmaxz = qz + rl;
            MeshData md = it.mesh;
            int tc = md.tris.Length / 3;
            for (int tri = 0; tri < tc; tri++)
            {
                if (md.triMax[tri * 3] < qminx || md.triMin[tri * 3] > qmaxx) continue;
                if (md.triMax[tri * 3 + 1] < qminy || md.triMin[tri * 3 + 1] > qmaxy) continue;
                if (md.triMax[tri * 3 + 2] < qminz || md.triMin[tri * 3 + 2] > qmaxz) continue;
                float cpx, cpy, cpz;
                ClosestPointOnTri(md.verts, md.tris, tri, qx, qy, qz, out cpx, out cpy, out cpz);
                float ex = qx - cpx, ey = qy - cpy, ez = qz - cpz;
                float dist = (float)Math.Sqrt(ex * ex + ey * ey + ez * ez);
                if (dist >= rl) continue;
                float depth = rl - dist;
                // local normal
                float nx, ny, nz;
                if (dist > 1e-6f) { nx = ex / dist; ny = ey / dist; nz = ez / dist; }
                else
                {
                    // deep inside: use triangle normal
                    int i0 = md.tris[tri * 3] * 3, i1 = md.tris[tri * 3 + 1] * 3, i2 = md.tris[tri * 3 + 2] * 3;
                    float abx = md.verts[i1] - md.verts[i0], aby = md.verts[i1 + 1] - md.verts[i0 + 1], abz = md.verts[i1 + 2] - md.verts[i0 + 2];
                    float acx = md.verts[i2] - md.verts[i0], acy = md.verts[i2 + 1] - md.verts[i0 + 1], acz = md.verts[i2 + 2] - md.verts[i0 + 2];
                    nx = aby * acz - abz * acy; ny = abz * acx - abx * acz; nz = abx * acy - aby * acx;
                    float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nl < 1e-9f) continue;
                    nx /= nl; ny /= nl; nz /= nl;
                }
                // world normal = Ry(yaw) * n
                float wnx = nx * it.cos + nz * it.sin;
                float wny = ny;
                float wnz = -nx * it.sin + nz * it.cos;
                float wdepth = depth * it.scale;
                if (!found || wdepth > best.depth)
                {
                    best.nx = wnx; best.ny = wny; best.nz = wnz;
                    best.depth = wdepth;
                    // world contact point
                    best.py = it.y + cpy * it.scale;
                    found = true;
                }
            }
        }
        return found;
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
                    {
                        if (!outIdx.Contains(lst[k])) outIdx.Add(lst[k]);
                    }
                }
            }
        }
    }

    // Pushes the capsule out of structures. Returns true when a side contact
    // blocked movement; ground is raised to the highest stand-on surface.
    public bool Resolve(ref float px, ref float py, ref float pz,
                        float radius, float height,
                        ref float ground, ref bool grounded)
    {
        bool blocked = false;
        var cand = new List<int>(16);
        for (int iter = 0; iter < 3; iter++)
        {
            Contact best = new Contact();
            bool any = false;
            int bestIdx = -1;
            GatherCandidates(px, pz, radius + 500f, cand);
            for (int k = 0; k < cand.Count; k++)
            {
                Instance it = _inst[cand[k]];
                if (py + height < it.hMin - 60f || py > it.hMax + 60f) continue;
                float ddx = px - it.x, ddz = pz - it.z;
                float rr = it.radius + radius + 4f;
                if (ddx * ddx + ddz * ddz > rr * rr) continue;
                if (InstanceContact(ref it, px, py, pz, radius, height, ref best))
                {
                    any = true;
                    bestIdx = cand[k];
                }
            }
            if (!any) break;
            bool stepUp = false;
            if (bestIdx >= 0)
            {
                Instance bi = _inst[bestIdx];
                float horiz = (float)Math.Sqrt(best.nx * best.nx + best.nz * best.nz);
                // low obstacle whose top is within step range: climb it instead
                // of pushing the capsule sideways (walkable rock tops)
                if (horiz > 0.5f && bi.hMax > ground && bi.hMax <= py + 70f)
                {
                    ground = bi.hMax;
                    grounded = true;
                    stepUp = true;
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
            // stand-on support from up-facing contacts
            if (best.ny > 0.55f && best.py > ground && best.py <= py + 60f)
            {
                ground = best.py;
                if (py <= ground + 2f) grounded = true;
            }
            if (stepUp) break;
        }
        return blocked;
    }

    // Vertical support probe: highest up-facing triangle under (x,z) within
    // [yLow, yHigh]. Returns float.MinValue when nothing is hit.
    public float SupportHeight(float x, float z, float yLow, float yHigh)
    {
        float best = float.MinValue;
        var cand = new List<int>(16);
        GatherCandidates(x, z, 300f, cand);
        for (int k = 0; k < cand.Count; k++)
        {
            Instance it = _inst[cand[k]];
            if (it.hMax < yLow || it.hMin > yHigh) continue;
            float ddx = x - it.x, ddz = z - it.z;
            float rr = it.radius + 8f;
            if (ddx * ddx + ddz * ddz > rr * rr) continue;
            float inv = 1f / it.scale;
            float lx = (ddx * it.cos - ddz * it.sin) * inv;
            float lz = (ddx * it.sin + ddz * it.cos) * inv;
            MeshData md = it.mesh;
            int tc = md.tris.Length / 3;
            for (int tri = 0; tri < tc; tri++)
            {
                if (lx < md.triMin[tri * 3] || lx > md.triMax[tri * 3]) continue;
                if (lz < md.triMin[tri * 3 + 2] || lz > md.triMax[tri * 3 + 2]) continue;
                int i0 = md.tris[tri * 3] * 3, i1 = md.tris[tri * 3 + 1] * 3, i2 = md.tris[tri * 3 + 2] * 3;
                float ax = md.verts[i0], az = md.verts[i0 + 2];
                float bx = md.verts[i1], bz = md.verts[i1 + 2];
                float cx = md.verts[i2], cz = md.verts[i2 + 2];
                float v0x = bx - ax, v0z = bz - az;
                float v1x = cx - ax, v1z = cz - az;
                float v2x = lx - ax, v2z = lz - az;
                float den = v0x * v1z - v1x * v0z;
                if (Math.Abs(den) < 1e-9f) continue;
                float u = (v2x * v1z - v1x * v2z) / den;
                float w = (v0x * v2z - v2x * v0z) / den;
                if (u < 0f || w < 0f || u + w > 1f) continue;
                float ay = md.verts[i0 + 1], by = md.verts[i1 + 1], cy = md.verts[i2 + 1];
                float ly = ay + (by - ay) * u + (cy - ay) * w;
                // triangle normal y
                float nx = (by - ay) * (cz - az) - (bz - az) * (cy - ay);
                float ny = (bz - az) * (cx - ax) - (bx - ax) * (cz - az);
                float nz = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl < 1e-9f || ny / nl < 0.5f) continue;
                float wy = it.y + ly * it.scale;
                if (wy < yLow || wy > yHigh) continue;
                if (wy > best) best = wy;
            }
        }
        return best;
    }

    // Nearest solid instance to (x,z); returns distance or float.MaxValue.
    public float NearestInstance(float x, float z, out float nx, out float ny, out float nz)
    {
        float best = float.MaxValue;
        nx = ny = nz = 0f;
        for (int i = 0; i < _inst.Count; i++)
        {
            Instance it = _inst[i];
            float dx = x - it.x, dz = z - it.z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < best)
            {
                best = d;
                nx = it.x; ny = it.y; nz = it.z;
            }
        }
        return best;
    }

    public string Describe()
    {
        int per4 = 0, per5 = 0, per6 = 0, per7 = 0;
        for (int i = 0; i < _inst.Count; i++)
        {
            switch (_inst[i].pattern)
            {
                case 4: per4++; break;
                case 5: per5++; break;
                case 6: per6++; break;
                case 7: per7++; break;
            }
        }
        return string.Format("instances={0} (deadwood={1} cactus={2} rock6={3} rock7={4}) meshes={5}",
            _inst.Count, per4, per5, per6, per7, MeshCount);
    }
}
