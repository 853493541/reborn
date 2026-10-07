// SkillMove.tab - the per-move displacement table (SPEC_MOTION.md §1.2, §2.3).
//
// Client truth: a skill's displacement is the SkillMove.tab per-tick velocity
// curve applied by KCharacter::OnSkillMove - heading := facing once at start;
// per 15 Hz tick V = min(VXY,127)*(255-w)/255 along the heading byte angle
// (0 = +X, 64 = +Y, unit pi/128), VZ feeds the vertical integrator, heading
// += DirectionXY with byte wrap. The .tani floats / root motion are NOT the
// displacement source (SPEC_MOTION §1.4).
//
// Embedded rows are verbatim from the EXP store extraction
// (settings/SkillMove.tab, 952,312 B, 935 rows; official PakV4SfxExtract;
// scratch copy in %TEMP%\opencode\spec3x\ex_slot\settings). The full table can
// be loaded at runtime with RC_SKILLMOVE_TAB=<extracted SkillMove.tab path>.
//
// Skill -> SkillMoveID: the shipped client store (settings/ObjSlotInfo.tab) is
// all-zero in 1-6-0, so the host owns this map (SPEC_MOTION §2.3 note) - a
// registered host-data gap (fill per skill from the live producer). The map is
// empty until that data lands; RC_SKILL_MOVEID overrides for tests/authoring.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

internal sealed class SkillMoveRow
{
    public int Id;
    public int IgnoreGravity, TotalFrame, Column;
    public int OnlyFly, Death, KeepVelocity, CanJump, CanBanMask;
    public int[] Vxy = new int[0];   // per frame index
    public int[] Vz = new int[0];
    public int[] Dir = new int[0];
    public int[] Frame = new int[0]; // -1 = no quad at that index (slot disabled)
}

internal static class SkillMoveTable
{
    // "id|igng|total|col|fly|death|keep|jump|ban|f,vxy,vz,dir;..."
    static readonly string[] Embedded = new string[]
    {
        // 876: pure in-place row (zero VXY/VZ, no DIR) - standing-cast test row
        "876|0|8|8|0|0|0|0|0|0,0,0,0;1,0,0,0;2,0,0,0;3,0,0,0;4,0,0,0;5,0,0,0;6,0,0,0;7,0,0,0;8,0,0,0",
        // 300: flying dash, VXY up to 333 (clamped 127), DIR +64/-64 at F0/F1, keep velocity
        "300|1|51|51|0|0|1|0|0|0,0,0,64;1,333,1352,-64;2,329,1328,0;3,321,1280,0;4,309,1207,0;5,292,1111,0;6,271,990,0;7,245,848,0;8,211,712,0;9,187,602,0;10,173,516,0;11,170,455,0;12,179,418,0;13,198,407,0;14,231,417,0;15,275,420,0;16,301,398,0;17,306,353,0;18,290,284,0;19,252,190,0;20,193,73,0;21,155,4,0;22,153,-2,0;23,151,-8,0;24,148,-14,0;25,146,-19,0;26,144,-24,0;27,142,-29,0;28,140,-34,0;29,139,-39,0;30,137,-43,0;31,135,-47,0;32,134,-51,0;33,132,-55,0;34,131,-59,0;35,130,-62,0;36,129,-65,0;37,127,-68,0;38,126,-71,0;39,125,-73,0;40,125,-76,0;41,124,-78,0;42,123,-80,0;43,122,-81,0;44,122,-83,0;45,121,-84,0;46,121,-85,0;47,121,-86,0;48,120,-87,0;49,120,-87,0;50,120,-87,0",
        // 700: DIR -128 at F3, small VXY, no keep (delta-row test)
        "700|0|25|10|0|0|0|0|0|0,0,0,64;1,2,0,64;2,1,0,0;3,15,0,-128;4,14,0,0;5,4,0,0;6,2,0,0;7,4,0,128;8,1,0,0;9,0,0,-128;10,0,0,128;11,0,0,0;12,1,0,-128;13,8,0,0;14,14,0,0;15,7,0,0;16,3,0,0;17,0,0,0;18,1,0,128;19,0,0,0;20,0,0,-128;21,0,0,0;22,0,0,0;23,0,0,128;24,0,0,0",
        // 200: flying move, IgnoreGravity=1, VZ 621..1720 held, keep velocity
        "200|1|33|33|0|0|1|0|0|0,0,0,64;1,19,621,-64;2,39,929,0;3,68,1498,0;4,70,1653,0;5,76,1720,0;6,85,1698,0;7,99,1587,0;8,114,1509,0;9,120,1505,0;10,123,1418,0;11,126,1331,0;12,128,1243,0;13,130,1154,0;14,131,1065,0;15,132,976,0;16,133,885,0;17,132,795,0;18,132,703,0;19,131,611,0;20,129,519,0;21,127,426,0;22,124,332,0;23,121,238,0;24,117,143,0;25,113,48,0;26,103,-35,0;27,90,-101,0;28,77,-162,0;29,67,-216,0;30,59,-262,0;31,53,-298,0;32,50,-326,0",
        // 55: gravity row with VZ 553..590 (no ignore-gravity) - vertical test row
        "55|0|16|0|0|0|0|0|0|0,0,0,64;1,67,553,-64;2,69,581,0;3,70,590,0;4,69,581,0;5,67,553,0;6,64,506,0;7,59,440,0;8,54,363,0;9,54,311,0;10,54,265,0;11,50,219,0;12,44,171,0;13,35,123,0;14,23,74,0;15,8,25,0",
        // 2: death move (SkillMoveDeath=1), 50 frames
        "2|0|50|50|0|1|0|0|0|0,0,0,64;1,65,0,64;2,0,0,-64;3,0,0,0;4,0,0,0;5,0,0,0;6,0,0,0;7,0,0,0;8,0,0,0;9,12,0,64;10,54,0,0;11,12,0,0;12,0,0,-64;13,0,0,0;14,42,0,64;15,0,0,-64;16,0,0,0;17,0,0,0;18,0,0,0;19,0,0,0;20,0,0,0;21,0,0,0;22,0,0,0;23,74,0,64;24,67,0,0;25,50,0,0;26,40,0,0;27,34,0,0;28,27,0,0;29,21,0,0;30,19,0,0;31,17,0,0;32,16,0,0;33,16,0,0;34,17,0,0;35,20,0,0;36,22,0,0;37,21,0,0;38,16,0,0;39,6,0,0;40,0,0,-64;41,0,0,0;42,0,0,0;43,0,0,0;44,0,0,0;45,0,0,0;46,0,0,0;47,0,0,0;48,0,0,0;49,0,0,0;50,0,0,0;51,0,0,0;52,0,0,0;53,0,0,0;54,0,0,0;55,0,0,0"
    };

    // Host-owned skill -> moveID map (SPEC_MOTION §2.3 step 1; registered
    // host-data gap - the shipped ObjSlotInfo.tab is all-zero). Key = skill clip
    // basename, lowercase. Empty until the live producer is found: a cast of an
    // unmapped skill performs no displacement (no invented row).
    static readonly Dictionary<string, int> SkillMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    static Dictionary<int, SkillMoveRow> _full;

    public static int MapSkill(string skillKey)
    {
        if (string.IsNullOrEmpty(skillKey)) return 0;
        int id;
        return SkillMap.TryGetValue(skillKey, out id) ? id : 0;
    }

    public static SkillMoveRow Load(int id)
    {
        if (id <= 0) return null;
        for (int i = 0; i < Embedded.Length; i++)
        {
            int sep = Embedded[i].IndexOf('|');
            if (sep > 0 && int.Parse(Embedded[i].Substring(0, sep)) == id)
                return ParseCompact(Embedded[i]);
        }
        if (_full == null)
        {
            string path = Environment.GetEnvironmentVariable("RC_SKILLMOVE_TAB");
            _full = string.IsNullOrEmpty(path) || !File.Exists(path)
                ? new Dictionary<int, SkillMoveRow>()
                : ParseTsv(path);
        }
        SkillMoveRow r;
        return _full.TryGetValue(id, out r) ? r : null;
    }

    static SkillMoveRow ParseCompact(string s)
    {
        string[] parts = s.Split('|');
        SkillMoveRow r = new SkillMoveRow();
        r.Id = int.Parse(parts[0]);
        r.IgnoreGravity = int.Parse(parts[1]);
        r.TotalFrame = int.Parse(parts[2]);
        r.Column = int.Parse(parts[3]);
        r.OnlyFly = int.Parse(parts[4]);
        r.Death = int.Parse(parts[5]);
        r.KeepVelocity = int.Parse(parts[6]);
        r.CanJump = int.Parse(parts[7]);
        r.CanBanMask = int.Parse(parts[8]);
        string[] quads = parts[9].Split(';');
        int maxF = r.TotalFrame;
        for (int i = 0; i < quads.Length; i++)
        {
            string[] q = quads[i].Split(',');
            int f = int.Parse(q[0]);
            if (f > maxF) maxF = f;
        }
        int n = maxF + 1;
        r.Frame = new int[n]; r.Vxy = new int[n]; r.Vz = new int[n]; r.Dir = new int[n];
        for (int i = 0; i < n; i++) r.Frame[i] = -1;
        for (int i = 0; i < quads.Length; i++)
        {
            string[] q = quads[i].Split(',');
            int f = int.Parse(q[0]);
            r.Frame[f] = f;
            r.Vxy[f] = int.Parse(q[1]);
            r.Vz[f] = int.Parse(q[2]);
            r.Dir[f] = int.Parse(q[3]);
        }
        return r;
    }

    // Full TSV (GBK) parser: SkillMoveID, IngoreGravity, TotalFrame, Column,
    // SkillMoveOnlyFly, SkillMoveDeath, SkillMoveEndButKeepVelocity, CanJump,
    // CanBanMask, then quadruples FrameN, VelocityXYN, VelocityZN, DirectionXYN.
    static Dictionary<int, SkillMoveRow> ParseTsv(string path)
    {
        var map = new Dictionary<int, SkillMoveRow>();
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.GetEncoding(936));
        }
        catch (Exception)
        {
            return map;
        }
        for (int li = 1; li < lines.Length; li++)
        {
            string line = lines[li];
            if (line.Length == 0) continue;
            string[] f = line.Split('\t');
            if (f.Length < 13) continue;
            int id;
            if (!int.TryParse(f[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                continue;
            SkillMoveRow r = new SkillMoveRow();
            r.Id = id;
            r.IgnoreGravity = ParseInt(f[1]);
            r.TotalFrame = ParseInt(f[2]);
            r.Column = ParseInt(f[3]);
            r.OnlyFly = ParseInt(f[4]);
            r.Death = ParseInt(f[5]);
            r.KeepVelocity = ParseInt(f[6]);
            r.CanJump = ParseInt(f[7]);
            r.CanBanMask = ParseInt(f[8]);
            var fs = new List<int>(); var vs = new List<int>(); var zs = new List<int>(); var ds = new List<int>();
            for (int i = 9; i + 3 < f.Length; i += 4)
            {
                string a = f[i].Trim(), b = f[i + 1].Trim(), c = f[i + 2].Trim(), d = f[i + 3].Trim();
                if (a.Length == 0 && b.Length == 0 && c.Length == 0 && d.Length == 0) break;
                int fr, vx, vz, dr;
                if (!int.TryParse(a, out fr) || !int.TryParse(b, out vx) ||
                    !int.TryParse(c, out vz) || !int.TryParse(d, out dr)) break;
                fs.Add(fr); vs.Add(vx); zs.Add(vz); ds.Add(dr);
            }
            int maxF = r.TotalFrame;
            for (int i = 0; i < fs.Count; i++) if (fs[i] > maxF) maxF = fs[i];
            int n = maxF + 1;
            r.Frame = new int[n]; r.Vxy = new int[n]; r.Vz = new int[n]; r.Dir = new int[n];
            for (int i = 0; i < n; i++) r.Frame[i] = -1;
            for (int i = 0; i < fs.Count; i++)
            {
                int fr = fs[i];
                if (fr < 0 || fr >= n) continue;
                r.Frame[fr] = fr; r.Vxy[fr] = vs[i]; r.Vz[fr] = zs[i]; r.Dir[fr] = ds[i];
            }
            map[id] = r;
        }
        return map;
    }

    static int ParseInt(string s)
    {
        int v;
        return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
    }
}
