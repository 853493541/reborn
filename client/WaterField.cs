// Water source per SPEC_STATES_P3.
//
// Real region: the shipped water region tree
// data\source\maps\<map>\water\regiondata\RegionInfo.json, decoded by
// tools/character/water_region.py into client/WaterRegions.cs. The ReferNode key
// encodes the region's world origin (KG3D_LoaderNoRenderX64.dll fn 0x1800245b0:
// key field = sign digit + 3 magnitude digits of minWorld/(RegionSize*UnitScale)
// = minWorld/51200); a region spans 1024 cells = 102400 u (16x16 leaves of 64
// cells). Height is the shipped water surface (watersurfacelist.json) inside the
// region. The engine narrows the shoreline with the region hole/normal masks
// (not decoded); the host floats water over the block AABB where ground < surface.
//
// Fallback (maps without a RegionInfo decode): the authored per-map surface list
// (WaterSurfaces.cs) mapped to cells with the old registered P1 heuristic. That
// path is NOT used for maps that carry a real region.
//
// RC_WATER boxes remain a TEST-ONLY override and always win.
using System;
using System.Collections.Generic;
using System.Globalization;

internal sealed class WaterField
{
    struct Box
    {
        public float X0, Z0, X1, Z1, Surface;
    }

    readonly List<Box> _boxes = new List<Box>();
    WaterSurfaces.Body[] _bodies = new WaterSurfaces.Body[0];
    WaterRegions.Region[] _regions = new WaterRegions.Region[0];

    public int BodyCount { get { return _bodies.Length; } }
    public int RegionCount { get { return _regions.Length; } }
    public int BoxCount { get { return _boxes.Count; } }
    public int Count { get { return _regions.Length + _bodies.Length + _boxes.Count; } }

    public static WaterField Create(string mapName, string boxSpec)
    {
        WaterField w = new WaterField();
        try { w._regions = WaterRegions.For(mapName); } catch (Exception) { }
        try { w._bodies = WaterSurfaces.For(mapName); } catch (Exception) { }
        if (!string.IsNullOrEmpty(boxSpec))
        {
            string[] parts = boxSpec.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                string[] f = p.Split(',');
                if (f.Length < 5) continue;
                Box b = new Box();
                try
                {
                    NumberStyles ns = NumberStyles.Float;
                    CultureInfo ci = CultureInfo.InvariantCulture;
                    b.X0 = float.Parse(f[0], ns, ci);
                    b.Z0 = float.Parse(f[1], ns, ci);
                    b.X1 = float.Parse(f[2], ns, ci);
                    b.Z1 = float.Parse(f[3], ns, ci);
                    b.Surface = float.Parse(f[4], ns, ci);
                    if (b.X1 < b.X0) { float t = b.X0; b.X0 = b.X1; b.X1 = t; }
                    if (b.Z1 < b.Z0) { float t = b.Z0; b.Z0 = b.Z1; b.Z1 = t; }
                    w._boxes.Add(b);
                }
                catch (FormatException) { }
            }
        }
        return w;
    }

    // water at (x,z): surface height. The surface only holds where the terrain is
    // below it (a floating plane does not flood hills).
    public bool Sample(float x, float z, float ground, out float surface)
    {
        surface = 0f;
        bool found = false;
        for (int i = 0; i < _regions.Length; i++)
        {
            WaterRegions.Region r = _regions[i];
            if (x < r.X0 || x > r.X1 || z < r.Z0 || z > r.Z1) continue;
            if (ground >= r.Surface) continue;
            surface = r.Surface;
            found = true;
        }
        if (_regions.Length == 0)
        {
            for (int i = 0; i < _bodies.Length; i++)
            {
                WaterSurfaces.Body b = _bodies[i];
                if (ground >= b.Y) continue;          // plane above the terrain only
                if (b.Type == 0)
                {
                    surface = b.Y;                    // ocean: global plane
                    found = true;
                    continue;
                }
                float dx = x - b.X;
                float dz = z - b.Z;
                // P1-registered footprint (fallback maps only): 4096 u * Scale.
                float hx = 4096f * b.ScaleX;
                float hz = 4096f * b.ScaleZ;
                if (hx <= 0f) hx = 1f;
                if (hz <= 0f) hz = 1f;
                if (b.RotY != 0f)
                {
                    float ca = (float)Math.Cos(-b.RotY);
                    float sa = (float)Math.Sin(-b.RotY);
                    float rx = dx * ca - dz * sa;
                    float rz = dx * sa + dz * ca;
                    dx = rx;
                    dz = rz;
                }
                if (Math.Abs(dx) <= hx && Math.Abs(dz) <= hz)
                {
                    surface = b.Y;
                    found = true;
                }
            }
        }
        // RC_WATER boxes: TEST-ONLY override, always wins.
        for (int i = 0; i < _boxes.Count; i++)
        {
            Box b = _boxes[i];
            if (x < b.X0 || x > b.X1 || z < b.Z0 || z > b.Z1) continue;
            surface = b.Surface;
            found = true;
        }
        return found;
    }

    public string Describe()
    {
        return string.Format("waterRegions={0} waterBodies={1} waterBoxes={2} (real RegionInfo tree; height from watersurfacelist)",
            _regions.Length, _bodies.Length, _boxes.Count);
    }
}
