// Water source per SPEC_STATES_P2 section "Water surface data semantics".
//
// Truth: the authored per-map surface list (client/WaterSurfaces.cs, generated
// by tools/character/water_surfaces.py from
// data\source\maps\<map>\water\surface\watersurfacelist.json in the client
// PakV4 store). This list is the RENDER/WAVE placement; the gameplay region is
// the terrain logic cell (m_pCell = [char+0x50]; flag bit0; surface =
// word[+6]<<6; floor = word[+4]<<6, helper 0x140312440) whose writer is still
// untraced (P1). The interim mapping is the authored rectangle:
//   center = Postion, half-extents = 0.5*BaseWidth*ScaleX x 0.5*BaseLenght*ScaleZ,
//   yaw = RotY; type 0 = global plane; always gated on ground < surface.
// Labeled P1-provisional. RC_WATER boxes remain a TEST-ONLY override.
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
            float hx = 0.5f * b.BaseW * b.ScaleX;
            float hz = 0.5f * b.BaseL * b.ScaleZ;
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
        return found;
    }

    public string Describe()
    {
        return string.Format("waterBodies={0} waterBoxes={1} (P1 authored-rect mapping; true region = terrain cell layer)",
            _bodies.Length, _boxes.Count);
    }
}
