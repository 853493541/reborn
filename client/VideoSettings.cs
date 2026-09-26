// FOV / 广角 settings from the real config files.
// Game sources (controls/RESEARCH_RESOLVED_GAPS.md §3):
//   config.ini [KG3DENGINE] CammeraAngle = 0.837757 rad (~48 deg, key
//   misspelled in the shipped file) - the install default;
//   video panel VideoSetting_WidAngle = 30..60 deg, default 50 when unset.
// Applied as a projection-only factor through SetViewAngleFactor; it must not
// move the camera or change the distance.
using System;
using System.IO;
using System.Text.RegularExpressions;

internal static class VideoSettings
{
    public const double DefaultAngle = 0.837757; // rad, config.ini default

    // Returns the SetViewAngleFactor value (angle / default) and logs the
    // source. RC_VIEW_ANGLE still overrides for testing.
    public static float ViewAngleFactor(string editorRoot, Action<string> log)
    {
        double angle = DefaultAngle;
        string src = "default";
        try
        {
            string[] candidates = new string[]
            {
                Path.Combine(editorRoot, "config.ini"),
                Path.Combine(@"C:\SeasunGame\Game\JX3\bin\zhcn_hd", "config.ini"),
                Path.Combine(editorRoot, "config", "config.ini")
            };
            foreach (string p in candidates)
            {
                if (!File.Exists(p)) continue;
                string text = File.ReadAllText(p);
                // the shipped key is misspelled CammeraAngle; accept both
                Match m = Regex.Match(text,
                    @"(?im)^\s*(CammeraAngle|CameraAngle)\s*=\s*([-0-9.eE+]+)");
                double v;
                if (m.Success && double.TryParse(m.Groups[2].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0.1)
                {
                    angle = v;
                    src = Path.GetFileName(p);
                }
                break;
            }
        }
        catch (Exception e) { log("VideoSettings ex: " + e.Message); }
        double factor = angle / DefaultAngle;
        log(string.Format("fov source={0} angle={1:F4} rad factor={2:F4}", src, angle, factor));
        return (float)factor;
    }
}
