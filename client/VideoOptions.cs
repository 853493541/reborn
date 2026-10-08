// Engine option application (P3 of docs/engine_host/RENDERING_OPTIONS_PLAN.md).
// Uses the engine's own managed API: KGEngineCLR.SetEngineOptionFromConfigFile
// (loads a config.ini-format preset), plus the dynamic-weather API.
//
// Env:
//   RC_OPT_FILE=<path>          apply this ini (any shipped config_*.ini)
//   RC_QUALITY=<1..9|bd1..bd7|default>  apply a shipped preset by tier
//   RC_OPT_<KEY>=<value>        per-option override merged into the preset
//   RC_WEATHER=0|1              engine dynamic weather on/off
//   RC_WEATHER_PARAMS=12 floats comma-separated -> SetDynamicWeatherParameters
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MovieEngineCLR;

internal static class VideoOptions
{
    static string Env(string name, string fallback)
    {
        string v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    static string ResolveQuality(string gameConfigDir, string quality, Action<string> log)
    {
        string pattern;
        if (quality == "default") pattern = "config.default.ini";
        else if (quality.StartsWith("bd"))
            pattern = "config_bd_" + quality.Substring(2) + "_*.ini";
        else pattern = "config_" + quality + "_*.ini";
        try
        {
            string[] hits = Directory.GetFiles(gameConfigDir, pattern);
            if (hits.Length == 0) { log("VideoOptions: no preset for RC_QUALITY=" + quality + " (" + pattern + ")"); return ""; }
            Array.Sort(hits);
            log("VideoOptions: RC_QUALITY=" + quality + " -> " + hits[0]);
            return hits[0];
        }
        catch (Exception e) { log("VideoOptions resolve ex: " + e.Message); return ""; }
    }

    static string MergeOverrides(string basePath, List<KeyValuePair<string, string>> overrides,
                                 string startupPath, Action<string> log)
    {
        try
        {
            Encoding gbk = Encoding.GetEncoding(936);
            string[] lines = File.ReadAllLines(basePath, gbk);
            List<string> outLines = new List<string>();
            bool[] used = new bool[overrides.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string replaced = line;
                for (int o = 0; o < overrides.Count; o++)
                {
                    string key = overrides[o].Key;
                    int eq = line.IndexOf('=');
                    if (eq > 0 && line.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        string head = line.Substring(0, eq).Trim();
                        if (head.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            replaced = key + "=" + overrides[o].Value;
                            used[o] = true;
                            break;
                        }
                    }
                }
                outLines.Add(replaced);
            }
            List<string> appended = new List<string>();
            for (int o = 0; o < overrides.Count; o++)
                if (!used[o]) { appended.Add(overrides[o].Key + "=" + overrides[o].Value); log("VideoOptions: override appended " + overrides[o].Key + "=" + overrides[o].Value); }
            if (appended.Count > 0)
            {
                outLines.Add("[KG3DENGINE]");
                outLines.AddRange(appended);
            }
            string outDir = Path.Combine(startupPath, "reborn_out");
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, "renderopts_" + DateTime.Now.ToString("HHmmss") + ".ini");
            File.WriteAllLines(outPath, outLines.ToArray(), gbk);
            log("VideoOptions: merged " + overrides.Count + " override(s) -> " + outPath);
            return outPath;
        }
        catch (Exception e) { log("VideoOptions merge ex: " + e.Message); return basePath; }
    }

    public static void Apply(KGEngineCLR engine, string gameConfigDir, string startupPath, Action<string> log)
    {
        string file = Env("RC_OPT_FILE", "");
        string quality = Env("RC_QUALITY", "");
        List<KeyValuePair<string, string>> overrides = new List<KeyValuePair<string, string>>();
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
        {
            string k = (string)e.Key;
            if (k != null && k.StartsWith("RC_OPT_", StringComparison.OrdinalIgnoreCase) &&
                !k.Equals("RC_OPT_FILE", StringComparison.OrdinalIgnoreCase))
                overrides.Add(new KeyValuePair<string, string>(k.Substring(7), (string)e.Value));
        }
        if (file == "" && quality != "") file = ResolveQuality(gameConfigDir, quality, log);
        if (file != "" && overrides.Count > 0) file = MergeOverrides(file, overrides, startupPath, log);
        if (file != "")
        {
            try
            {
                int r = engine.SetEngineOptionFromConfigFile(file);
                log("VideoOptions: SetEngineOptionFromConfigFile=" + r + " file=" + file);
            }
            catch (Exception e) { log("VideoOptions apply ex: " + e.Message); }
        }

        string weather = Env("RC_WEATHER", "");
        if (weather == "1" || weather == "0")
        {
            try { log("VideoOptions: EnableDynamicWeather(" + weather + ")=" + engine.EnableDynamicWeather(int.Parse(weather))); }
            catch (Exception e) { log("VideoOptions weather ex: " + e.Message); }
        }
        string wp = Env("RC_WEATHER_PARAMS", "");
        if (wp != "")
        {
            string[] parts = wp.Split(',');
            if (parts.Length == 12)
            {
                float[] f = new float[12];
                bool ok = true;
                for (int i = 0; i < 12; i++)
                {
                    if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out f[i])) { ok = false; break; }
                }
                if (ok)
                {
                    try
                    {
                        int r = engine.SetDynamicWeatherParameters(f[0], f[1], f[2], f[3], f[4], f[5],
                                                                   f[6], f[7], f[8], f[9], f[10], f[11]);
                        log("VideoOptions: SetDynamicWeatherParameters=" + r);
                    }
                    catch (Exception e) { log("VideoOptions weather params ex: " + e.Message); }
                }
                else log("VideoOptions: RC_WEATHER_PARAMS parse failed");
            }
            else log("VideoOptions: RC_WEATHER_PARAMS needs 12 comma-separated floats");
        }
    }
}
