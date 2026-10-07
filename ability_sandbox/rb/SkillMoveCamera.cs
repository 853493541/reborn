// SkillMoveCamera.cs - per-skill camera FOV table (Represent/camera/
// skill_move_camera.txt) + the temporary-FOV effect state machine (P2).
//
// Table columns (GB18030 header, verified 2026-10-06):
//   SkillID | bAniTag | 过渡时间(切入)ms | 过渡时间(切出)ms |
//   广角增幅(弧度 0~2PI) / 固定广角(角度 >=30) | 持续时间ms |
//   屏幕特效开关 | 边缘色差(0~10) | 色差饱和度(0~1)
//   - value < 30  -> FOV increase in radians over the current base angle
//   - value >= 30 -> fixed FOV in degrees
//
// The client-side FOV interpolation curve is still open research
// (JX3_CAMERA_RESEARCH.md §9: "FOV interpolation curve still open"), so the
// ramp here is LINEAR and marked provisional in HOST_DEVIATIONS.md.
// Post-FX fields (screen FX / edge aberration / saturation) are logged only -
// the host has no post-render pipeline (registered boundary).
//
// C# 5 (csc Framework64 v4.0.30319). Reads the embedded table copy
// (build resource `skill_move_camera.txt`) or a file path (RC_SKILL_MOVE_TABLE).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

class SkillMoveCamera
{
    public class Row
    {
        public int SkillId;
        public int AniTag;
        public float EnterMs;
        public float ExitMs;
        public float FovValue;
        public bool FixedFov;      // FovValue >= 30 -> degrees
        public float DurationMs;
        public bool ScreenFx;
        public float Edge;
        public float Sat;
    }

    static readonly Dictionary<int, Row> rows = new Dictionary<int, Row>();

    public static Row Get(int skillId)
    {
        Row r;
        return rows.TryGetValue(skillId, out r) ? r : null;
    }

    public static int Count { get { return rows.Count; } }

    public static void LoadEmbedded(Action<string> log)
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream("skill_move_camera.txt"))
            {
                if (s == null) { log("skillmove: embedded table missing"); return; }
                using (StreamReader r = new StreamReader(s, Encoding.GetEncoding(936)))
                    Parse(r.ReadToEnd(), "<embedded>", log);
            }
        }
        catch (Exception e) { log("skillmove: embedded load ex: " + e.Message); }
    }

    public static void LoadFile(string path, Action<string> log)
    {
        try
        {
            string text = File.ReadAllText(path, Encoding.GetEncoding(936));
            Parse(text, path, log);
        }
        catch (Exception e) { log("skillmove: load ex (" + path + "): " + e.Message); }
    }

    static void Parse(string text, string src, Action<string> log)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int added = 0;
        for (int i = 1; i < lines.Length; i++)   // line 0 = header
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;
            string[] c = line.Split('\t');
            int sid;
            if (!int.TryParse(c[0].Trim(), out sid) || sid <= 0) continue;   // skip the 0/0 default row
            Row r = new Row();
            r.SkillId = sid;
            if (c.Length > 1) int.TryParse(c[1].Trim(), out r.AniTag);
            r.EnterMs = F(c, 2);
            r.ExitMs = F(c, 3);
            r.FovValue = F(c, 4);
            r.DurationMs = F(c, 5);
            r.ScreenFx = F(c, 6) > 0.5f;
            r.Edge = F(c, 7);
            r.Sat = F(c, 8);
            r.FixedFov = r.FovValue >= 30f;
            rows[sid] = r;
            added++;
        }
        log(string.Format("skillmove table: {0} rows from {1}", added, src));
    }

    static float F(string[] c, int idx)
    {
        if (idx >= c.Length) return 0f;
        float v;
        if (float.TryParse(c[idx].Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;
        return 0f;
    }

    // Temporary-FOV effect: linear in-ramp, hold, linear out-ramp (provisional).
    public class Effect
    {
        Row row;
        double startMs;

        public bool Active { get { return row != null; } }
        public int SkillId { get { return row != null ? row.SkillId : 0; } }

        public void Start(Row r, double nowMs)
        {
            row = r;
            startMs = nowMs;
        }

        public void Stop()
        {
            row = null;
        }

        // Current FOV angle (radians). Returns -1 when the effect has ended
        // (caller restores the base factor). stage: 0 = in, 1 = hold, 2 = out.
        public double AngleAt(double nowMs, double baseAngleRad, out double phase, out int stage)
        {
            phase = 1.0;
            stage = 0;
            if (row == null) return -1.0;
            double t = nowMs - startMs;
            if (t < 0.0) return -1.0;
            double target = row.FixedFov
                ? row.FovValue * Math.PI / 180.0
                : baseAngleRad + row.FovValue;
            if (row.EnterMs > 0.5 && t < row.EnterMs)
            {
                stage = 0;
                phase = t / row.EnterMs;
                return baseAngleRad + (target - baseAngleRad) * phase;
            }
            if (t < row.EnterMs + row.DurationMs)
            {
                stage = 1;
                phase = 1.0;
                return target;
            }
            double t2 = t - row.EnterMs - row.DurationMs;
            if (row.ExitMs > 0.5 && t2 < row.ExitMs)
            {
                stage = 2;
                phase = t2 / row.ExitMs;
                return target + (baseAngleRad - target) * phase;
            }
            row = null;   // ended (one-shot)
            return -1.0;
        }
    }
}
