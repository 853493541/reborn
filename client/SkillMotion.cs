// SkillMotion - .tani ANI_TAG container probe (SPEC_MOTION.md §3; corrected
// 2026-10-07: the file's group type 2 = MotionTag, NOT type 1 - see below).
//
// .tani = ANI_TAG_FILE (GATA magic): header 0x130 bytes {magic, version, base
// .ani path, groupCount @0x10C}; then per group a 12-byte header
// {i32 type, i32 version, i32 count} + the payload read by that type's class.
// Group classes (tani factory _NewTagData 0x18001DB60, KG3D_AnimationTagX64.dll;
// jump table @RVA 0x1DEA4, cases 0..5):
//   0 = SFXTag_Group_Data    (alloc 0x298, vtable 0x180048CC8, loader 0x180004AB0)
//   1 = sound group          (alloc 0x508, vtable 0x1800490B0; payload carries
//                              the "FMOD" magic + Wwise event names; rest opaque)
//   2 = MotionTag_Group_Data (alloc 0x160, vtable 0x180048AC0; LoadFromFile =
//                              vtable slot 3 = 0x180003000, fn ptr @0x180048AD8)
//   3 = 0x130 / 4 = 0x58D8 / 5 = 0x1B0 (classes not decoded)
// Payload sizes (proven from the loaders):
//   type 0: u32 n1 + n1*0x130 + count*(0x164 + 8*4 + 0x64 + (v==3 ? 4 : 0))
//   type 2: v0 = count*0x970; v1 = u32,u32 + count*(0x188 + tag payloads);
//           v2 = u32 n2 + n2*0x130 then v1
// Unknown types: the walker scans forward for the next plausible group header
// (structural - no raw float/signature scanning). The client does NOT consume
// this for movement (displacement = SkillMove.tab, SPEC_MOTION §1.4); the probe
// logs what the file carries.
using System;
using System.IO;
using System.Text;

internal static class SkillMotion
{
    public static string Info = "off";
    static Action<string> _log;

    public static void Probe(string path, Action<string> log)
    {
        _log = log;
        if (path == null || path.Length == 0) { Info = "off"; return; }
        try
        {
            if (!File.Exists(path)) { Info = "missing"; return; }
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 0x140 || b[0] != (byte)'G' || b[1] != (byte)'A' ||
                b[2] != (byte)'T' || b[3] != (byte)'A')
            { Info = "not-gata"; return; }
            int ver = BitConverter.ToInt32(b, 4);
            int groups = BitConverter.ToInt32(b, 0x10C);
            int nameEnd = 8; while (nameEnd < 0x10C && b[nameEnd] != 0) nameEnd++;
            string baseAni = Encoding.GetEncoding(936).GetString(b, 8, nameEnd - 8);
            Info = string.Format("gata v{0} groups={1} base={2}", ver, groups, baseAni);
            Log("motionprobe: " + Info);
            int off = 0x130;
            for (int gi = 0; gi < groups && gi < 16; gi++)
            {
                if (off + 12 > b.Length) { Log("motionprobe: group " + gi + " truncated"); break; }
                int t = BitConverter.ToInt32(b, off);
                int gv = BitConverter.ToInt32(b, off + 4);
                int c = BitConverter.ToInt32(b, off + 8);
                Log(string.Format("motionprobe: group[{0}] off=0x{1:X} type={2} ver={3} count={4}",
                    gi, off, t, gv, c));
                int sz = KnownPayload(b, off, t, gv, c);
                if (sz < 0)
                {
                    // opaque payload (types 1/3/4/5 not decoded): scan forward for
                    // the next group header such that the remaining groups walk to
                    // EOF (structural scan; SPEC_MOTION §9.3).
                    int cand = ScanNext(b, off + 16, groups - gi - 1);
                    if (cand < 0)
                    {
                        Log("motionprobe: type " + t + " payload opaque - no group-header scan solution; walk stops");
                        break;
                    }
                    Log("motionprobe: type " + t + " payload opaque; next group scanned at 0x" + cand.ToString("X"));
                    off = cand;
                    continue;
                }
                if (t == 2) LogMotion(b, off + 12, gv, c);
                off += 12 + sz;
            }
        }
        catch (Exception e) { Info = "error: " + e.GetType().Name; }
    }

    static void Log(string s) { if (_log != null) _log(s); }

    // known payload byte size, or -1
    static int KnownPayload(byte[] b, int off, int t, int v, int c)
    {
        int p = off + 12;
        if (t == 0)
        {
            if (p + 4 > b.Length) return -1;
            long n1 = BitConverter.ToUInt32(b, p);
            if (n1 > 4096) return -1;
            long sz = 4 + n1 * 0x130L + (long)c * (0x1E8 + (v == 3 ? 4 : 0));
            return sz > b.Length ? -1 : (int)sz;
        }
        if (t == 2)
        {
            if (v == 0) return c * 0x970;
            int q = p;
            if (v == 2)
            {
                if (q + 4 > b.Length) return -1;
                long n2 = BitConverter.ToUInt32(b, q);
                if (n2 > 4096) return -1;
                q += 4 + (int)n2 * 0x130;
            }
            if (q + 8 > b.Length) return -1;
            q += 8;
            for (int k = 0; k < c; k++)
            {
                if (q + 0x188 > b.Length) return -1;
                int ntags = BitConverter.ToInt32(b, q + 0x104);
                if (ntags < 0 || ntags > 64) return -1;
                long sum = 0;
                for (int i = 0; i < ntags; i++)
                {
                    if (q + 0x108 + i * 4 + 4 > b.Length) return -1;
                    sum += BitConverter.ToUInt32(b, q + 0x108 + i * 4);
                }
                q += 0x188 + (int)sum;
                if (q > b.Length) return -1;
            }
            return q - p;
        }
        return -1;
    }

    static bool HeaderOk(byte[] b, int off)
    {
        if (off < 0 || off + 12 > b.Length) return false;
        int t = BitConverter.ToInt32(b, off);
        int v = BitConverter.ToInt32(b, off + 4);
        int c = BitConverter.ToInt32(b, off + 8);
        return t >= 0 && t <= 5 && v >= 0 && v <= 3 && c >= 0 && c <= 4096;
    }

    // true when groupsLeft groups from header `off` walk exactly to EOF
    static bool WalkOk(byte[] b, int off, int groupsLeft)
    {
        if (groupsLeft == 0) return off == b.Length;
        if (!HeaderOk(b, off)) return false;
        int t = BitConverter.ToInt32(b, off);
        int v = BitConverter.ToInt32(b, off + 4);
        int c = BitConverter.ToInt32(b, off + 8);
        int ps = KnownPayload(b, off, t, v, c);
        if (ps >= 0) return WalkOk(b, off + 12 + ps, groupsLeft - 1);
        for (int cand = off + 16; cand + 12 <= b.Length; cand += 4)
            if (WalkOk(b, cand, groupsLeft - 1)) return true;
        return false;
    }

    static int ScanNext(byte[] b, int from, int groupsLeft)
    {
        for (int cand = from; cand + 12 <= b.Length; cand += 4)
            if (WalkOk(b, cand, groupsLeft)) return cand;
        return -1;
    }

    static void LogMotion(byte[] b, int p, int v, int c)
    {
        int q = p;
        if (v == 2)
        {
            long n2 = BitConverter.ToUInt32(b, q);
            Log("motionprobe: motion v2 n2=" + n2);
            q += 4 + (int)n2 * 0x130;
        }
        if (q + 8 > b.Length) return;
        int a = BitConverter.ToInt32(b, q), d = BitConverter.ToInt32(b, q + 4);
        q += 8;
        Log(string.Format("motionprobe: motion v{0} a={1} b={2} keys={3}", v, a, d, c));
        for (int k = 0; k < c && q + 0x188 <= b.Length; k++)
        {
            int time = BitConverter.ToInt32(b, q + 0x100);
            int ntags = BitConverter.ToInt32(b, q + 0x104);
            if (ntags < 0 || ntags > 64) break;
            var sizes = new StringBuilder();
            long sum = 0;
            for (int i = 0; i < ntags; i++)
            {
                int s = (int)BitConverter.ToUInt32(b, q + 0x108 + i * 4);
                sum += (uint)s;
                if (i > 0) sizes.Append(',');
                sizes.Append(s);
            }
            string hash = "";
            int he = q; while (he < q + 0x100 && b[he] != 0) he++;
            if (he > q) hash = Encoding.GetEncoding(936).GetString(b, q, he - q);
            Log(string.Format("motionprobe: key {0} time={1} hash='{2}' tags={3} sizes=[{4}]",
                k, time, hash, ntags, sizes.ToString()));
            q += 0x188 + (int)sum;
        }
    }
}
