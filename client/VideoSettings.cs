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
    // config.ini CammeraAngle = 0.837757 rad (~48 deg): the install default
    // and the divisor for the SetViewAngleFactor.
    public const double DefaultAngle = 0.837757;
    // game panel 广角: slider 30..60 deg, default 50 deg when unset
    public const double PanelMinDeg = 30.0;
    public const double PanelMaxDeg = 60.0;
    public const double PanelDefaultDeg = 50.0;

    // Returns the SetViewAngleFactor value (angle / default) and logs the
    // source. RC_VIEW_ANGLE still overrides for testing.
    public static float ViewAngleFactor(string editorRoot, double userWidAngleDeg, Action<string> log)
    {
        double angleDeg = PanelDefaultDeg;   // game panel default: 50 deg
        string src = "panel-default";
        if (userWidAngleDeg > 0.0)
        {
            // per-user panel value from custom.dat; the game adds the engine
            // fMinCameraAngle when the raw value is below 30 (cap not available
            // in the host), then clamps to 30..60
            angleDeg = userWidAngleDeg < PanelMinDeg ? PanelMinDeg : userWidAngleDeg;
            src = "custom.dat";
        }
        else try
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
                    double deg = v * 180.0 / Math.PI;
                    if (deg >= PanelMinDeg && deg <= PanelMaxDeg)
                    {
                        angleDeg = deg;
                        src = Path.GetFileName(p);
                    }
                }
                break;
            }
        }
        catch (Exception e) { log("VideoSettings ex: " + e.Message); }
        // game panel clamp: 30..60 deg, default 50
        if (angleDeg < PanelMinDeg) angleDeg = PanelMinDeg;
        if (angleDeg > PanelMaxDeg) angleDeg = PanelMaxDeg;
        double angle = angleDeg * Math.PI / 180.0;
        double factor = angle / DefaultAngle;
        log(string.Format("fov source={0} angle={1:F2} deg ({2:F4} rad) factor={3:F4} [panel 30-60, default 50]",
            src, angleDeg, angle, factor));
        return (float)factor;
    }
}
