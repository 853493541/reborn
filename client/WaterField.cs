// Water source for the swim state (character 3.x, W6).
//
// The game's real water surface comes from the logic cell the character carries
// (KCharacter::GetWaterline 0x140312440: m_pCell = [char+0x50]; cell flag bit0
// = water; surface = word[cell+6]<<6; base = word[cell+4]<<6) streamed from the
// region data. That stream is NOT reachable in the host: the managed API has no
// water query (only Flux sim toggles), the physics terrain loader has no water
// layer, and `water/regiondata/RegionInfo.json` is a MISS in the client paks
// (see docs/character/3_5_3_7_RAGDOLL_SWIM_FLY.md, "Host wiring notes").
//
// REGISTERED PROVISIONAL (AGENTS §6): the host maps axis-aligned water boxes
// via RC_WATER="x0,z0,x1,z1,surface[,base];..." (units), e.g. a river stretch.
// Swim physics on top of it uses the decoded values (player submersion factor
// 0.589 = 6h*11/112, float height = max(base, surface - factor*h), swim speed
// CharacterSwimSpeed=20 u/frame). Re-open when the engine water nodes / the
// KMiniScene cell stream is decoded (trace the m_pCell writer).
using System;
using System.Collections.Generic;
using System.Globalization;

internal sealed class WaterField
{
    struct Box
    {
        public float X0, Z0, X1, Z1;
        public float Surface;
        public float Base;
        public bool HasBase;
    }

    readonly List<Box> _boxes = new List<Box>();
    public int Count { get { return _boxes.Count; } }

    // player submersion factor: 6h*11/112 (waterline research S2/S3)
    public const float PlayerSubmersionFactor = 0.5892857f;

    public static WaterField FromEnv(string spec)
    {
        WaterField w = new WaterField();
        if (string.IsNullOrEmpty(spec)) return w;
        string[] parts = spec.Split(';');
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
                if (f.Length >= 6 && f[5].Trim().Length > 0)
                {
                    b.Base = float.Parse(f[5], ns, ci);
                    b.HasBase = true;
                }
                if (b.X1 < b.X0) { float t = b.X0; b.X0 = b.X1; b.X1 = t; }
                if (b.Z1 < b.Z0) { float t = b.Z0; b.Z0 = b.Z1; b.Z1 = t; }
                w._boxes.Add(b);
            }
            catch (FormatException) { }
        }
        return w;
    }

    // water at (x,z): surface + base (bottom). base falls back to the sample
    // point of the caller when HasBase is false (the caller passes the terrain
    // ground as fallback via SampleOrGround).
    public bool Sample(float x, float z, out float surface, out float baseY)
    {
        surface = 0f;
        baseY = 0f;
        bool found = false;
        for (int i = 0; i < _boxes.Count; i++)
        {
            Box b = _boxes[i];
            if (x < b.X0 || x > b.X1 || z < b.Z0 || z > b.Z1) continue;
            surface = b.Surface;
            baseY = b.Base;
            found = true;
        }
        return found;
    }

    public bool HasBase(float x, float z)
    {
        for (int i = 0; i < _boxes.Count; i++)
        {
            Box b = _boxes[i];
            if (x < b.X0 || x > b.X1 || z < b.Z0 || z > b.Z1) continue;
            return b.HasBase;
        }
        return false;
    }

    public string Describe()
    {
        return string.Format("water boxes={0} submersionFactor={1:F4}", _boxes.Count, PlayerSubmersionFactor);
    }
}
