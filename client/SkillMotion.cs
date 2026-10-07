// SkillMotion — W5.5 host hook for the .tani authored motion vector.
//
// The .tani container is the GATA tag container
// (KG3DAnimationTagDataContainer::_Load, KG3DEngineX64.dll 0x180291490;
// docs/character/3_2_3_3_LOCOMOTION_MOTION.md). The authored motion vector
// sits in the SFX payload entries (stride 0x130; vector block at entry+0xE8)
// as 8 floats: (0, 1, 0, dx, dz, w, w, w) with w ~= 1, dx/dz in u.
// On 太阴指 the selected magnitude matches the measured .ani root-motion max
// deviation (157.033 authored vs 156.72 measured, 0.2%). The SFX sub-tag
// semantics are not decoded yet (W5.2 open), so the selector is provisional:
// last non-zero signature candidate in file order.
using System;
using System.IO;
using System.Text;

internal static class SkillMotion
{
    public static bool Ok;
    public static float Dx, Dz, Mag;
    public static string Info = "off";

    public static void Load(string path)
    {
        Ok = false;
        if (path == null || path.Length == 0) { Info = "off"; return; }
        try
        {
            if (!File.Exists(path)) { Info = "missing"; return; }
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 0x140 || b[0] != (byte)'G' || b[1] != (byte)'A' ||
                b[2] != (byte)'T' || b[3] != (byte)'A')
            { Info = "not-gata"; return; }
            int n = b.Length / 4;
            int count = 0;
            float selDx = 0f, selDz = 0f;
            long selOff = -1;
            StringBuilder cand = new StringBuilder();
            for (int i = 0; i + 8 <= n; i++)
            {
                int o = i * 4;
                float f0 = BitConverter.ToSingle(b, o);
                if (!(Math.Abs(f0) <= 0.001f)) continue;
                float f1 = BitConverter.ToSingle(b, o + 4);
                if (!(Math.Abs(f1 - 1f) <= 0.001f)) continue;
                float f2 = BitConverter.ToSingle(b, o + 8);
                if (!(Math.Abs(f2) <= 0.001f)) continue;
                float dx = BitConverter.ToSingle(b, o + 12);
                float dz = BitConverter.ToSingle(b, o + 16);
                float f5 = BitConverter.ToSingle(b, o + 20);
                float f6 = BitConverter.ToSingle(b, o + 24);
                float f7 = BitConverter.ToSingle(b, o + 28);
                if (!(Math.Abs(f5 - 1f) <= 0.02f)) continue;
                if (!(Math.Abs(f6 - 1f) <= 0.02f)) continue;
                if (!(Math.Abs(f7 - 1f) <= 0.02f)) continue;
                float m = (float)Math.Sqrt(dx * dx + dz * dz);
                count++;
                if (cand.Length > 0) cand.Append(' ');
                cand.Append(string.Format("[0x{0:x}:{1:F2},{2:F2}|{3:F2}]", o, dx, dz, m));
                if (m > 1f) { selDx = dx; selDz = dz; selOff = o; }
            }
            if (selOff < 0) { Info = "no-motion (" + count + " candidate(s))"; return; }
            Dx = selDx; Dz = selDz;
            Mag = (float)Math.Sqrt(Dx * Dx + Dz * Dz);
            Ok = true;
            Info = string.Format("sel@0x{0:x} ({1:F3},{2:F3}) mag={3:F3} candidates: {4}",
                selOff, Dx, Dz, Mag, cand.ToString());
        }
        catch (Exception ex)
        {
            Info = "error: " + ex.GetType().Name;
        }
    }
}
