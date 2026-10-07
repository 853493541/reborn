// Water source per SPEC_STATES.md section 1 / P1.
//
// Truth: the authored per-map surface list (client/WaterSurfaces.cs, generated
// by tools/character/water_surfaces.py from
// data\source\maps\<map>\water\surface\watersurfacelist.json in the client
// PakV4 store). The gameplay logic water is the character cell
// (m_pCell = [char+0x50]; flag bit0; surface = word[+6]<<6; floor =
// word[+4]<<6, helper 0x140312440) streamed from the not-yet-traced
// KMiniScene/.Map.Logical loader (SPEC_STATES P1), so the host maps the
// authored surfaces to cells with a REGISTERED HEURISTIC:
//   - type 0 (ocean): global plane - water wherever ground(x,z) < surfaceY;
//   - type 1 (bwater): within radius 4096 u * Scale of the body center and
//     ground(x,z) < surfaceY.
// Calibrated against the one known in-game water point (龙门寻宝 64293,55238,
// spec section 1.1): 4302 u from water1 (Scale 2 -> radius 8192).
// Re-open: trace the cell-stream writer (P1).
//
// RC_WATER boxes remain a TEST-ONLY override (same P1): those are the only
// hand-authored data allowed, and they are never a default.
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

    // P1 heuristic: authored surface base radius (u) multiplied by Scale.
    public const float SurfaceBaseRadius = 4096f;

    public int BodyCount { get { return _bodies.Length; } }
    public int BoxCount { get { return _boxes.Count; } }
    public int Count { get { return _bodies.Length + _boxes.Count; } }

    public static WaterField Create(string mapName, string boxSpec)
    {
        WaterField w = new WaterField();
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

    // water at (x,z): surface height. The authored surface only holds where
    // the terrain is below it (a floating plane does not flood hills).
    public bool Sample(float x, float z, float ground, out float surface)
    {
        surface = 0f;
        bool found = false;
        for (int i = 0; i < _boxes.Count; i++)
        {
            Box b = _boxes[i];
            if (x < b.X0 || x > b.X1 || z < b.Z0 || z > b.Z1) continue;
            surface = b.Surface;
            found = true;
        }
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
            float r = SurfaceBaseRadius * Math.Max(b.ScaleX, b.ScaleZ);
            if (dx * dx + dz * dz <= r * r)
            {
                surface = b.Y;
                found = true;
            }
        }
        return found;
    }

    public string Describe()
    {
        return string.Format("waterBodies={0} waterBoxes={1} (P1 heuristic radius=Scale*{2:F0})",
            _bodies.Length, _boxes.Count, SurfaceBaseRadius);
    }
}
