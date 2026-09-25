using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal sealed class CameraSettings
{
    public int MapId = -1;
    public double InitYaw = 0.5022619;
    public double InitPitch = -0.17;
    public double MaxCameraDistance = 2000.0;
    public double MinCameraDistance = 100.0; // engine cap not probed yet
    public double DragSpeed = 1.0;
    public double DragPitchSpeed = 1.0;
    public double EyeScale = 1.0;
    public bool HasSceneInit;
    public bool HasSavedRuntime;
    public bool HasCustomSettings;

    public static CameraSettings Load(string editorRoot, string mapPath, string appDir, Action<string> log)
    {
        var result = new CameraSettings();
        int overrideId;
        if (int.TryParse(Environment.GetEnvironmentVariable("RC_MAP_ID"), out overrideId))
            result.MapId = overrideId;
        else
            result.MapId = FindMapId(Path.Combine(editorRoot, "ResourcePack", "MapList.tab"), mapPath);

        string initPath = Path.Combine(appDir, "scene_init_param.txt");
        LoadSceneInit(initPath, result.MapId, result);

        string customPath = Environment.GetEnvironmentVariable("RC_CUSTOM_DAT");
        if (string.IsNullOrEmpty(customPath) && Environment.GetEnvironmentVariable("RC_LOAD_CUSTOM_DAT") == "1")
            customPath = FindLatestCustomDat(Path.Combine(@"C:\SeasunGame\Game\JX3\bin\zhcn_hd", "userdata"));
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            try
            {
                LoadCustomDat(File.ReadAllText(customPath), result);
                result.HasCustomSettings = true;
                log("CameraSettings: loaded real per-role camera settings");
            }
            catch (Exception e) { log("CameraSettings custom.dat ex: " + e.Message); }
        }

        log(string.Format("CameraSettings: mapId={0} sceneInit={1} yaw={2:F6} pitch={3:F3} max={4:F0} drag={5:F2}/{6:F2} eyeScale={7:F2}",
            result.MapId, result.HasSceneInit || result.HasSavedRuntime ? 1 : 0,
            result.InitYaw, result.InitPitch, result.MaxCameraDistance,
            result.DragSpeed, result.DragPitchSpeed, result.EyeScale));
        return result;
    }

    static int FindMapId(string mapListPath, string mapPath)
    {
        if (!File.Exists(mapListPath)) return -1;
        try
        {
            string wanted = NormalizePath(mapPath);
            string[] lines = File.ReadAllLines(mapListPath, Encoding.GetEncoding(936));
            foreach (string line in lines)
            {
                string[] fields = line.Split('\t');
                int id;
                if (fields.Length >= 3 && int.TryParse(fields[0].Trim(), out id) &&
                    NormalizePath(fields[2]) == wanted)
                    return id;
            }
        }
        catch { }
        return -1;
    }

    static string NormalizePath(string path)
    {
        return (path ?? "").Replace('/', '\\').Trim().Trim('"').ToLowerInvariant();
    }

    static void LoadSceneInit(string path, int mapId, CameraSettings settings)
    {
        if (mapId < 0 || !File.Exists(path)) return;
        try
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            foreach (string line in lines)
            {
                string[] fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                int id;
                double yaw, pitch;
                if (fields.Length >= 4 && int.TryParse(fields[0], out id) && id == mapId &&
                    double.TryParse(fields[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out yaw) &&
                    double.TryParse(fields[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out pitch))
                {
                    settings.InitYaw = yaw;
                    settings.InitPitch = pitch;
                    settings.HasSceneInit = true;
                    return;
                }
            }
        }
        catch { }
    }

    static string FindLatestCustomDat(string root)
    {
        if (!Directory.Exists(root)) return null;
        string best = null;
        DateTime bestTime = DateTime.MinValue;
        try
        {
            foreach (string path in Directory.GetFiles(root, "custom.dat", SearchOption.AllDirectories))
            {
                try
                {
                    DateTime time = File.GetLastWriteTimeUtc(path);
                    if (time > bestTime) { best = path; bestTime = time; }
                }
                catch { }
            }
        }
        catch { }
        return best;
    }

    static void LoadCustomDat(string text, CameraSettings settings)
    {
        string staticBlock = FindSection(text, "VideoSettingPanel.tCameraStatic");
        string runtimeBlock = FindSection(text, "g_Scene_tCameraRuntime");
        double value;
        if (TryNumber(staticBlock, "fMaxCameraDistance", out value) && value > 0)
            settings.MaxCameraDistance = value;
        if (TryNumber(staticBlock, "fDragSpeed", out value) && value > 0)
            settings.DragSpeed = value;
        if (TryNumber(staticBlock, "fDragPitchSpeed", out value) && value > 0)
            settings.DragPitchSpeed = value;
        if (TryNumber(runtimeBlock, "fYaw", out value))
        {
            settings.InitYaw = value;
            settings.HasSavedRuntime = true;
        }
        if (TryNumber(runtimeBlock, "fPitch", out value))
        {
            settings.InitPitch = value;
            settings.HasSavedRuntime = true;
        }
        if (TryNumber(runtimeBlock, "fCameraToObjectEyeScale", out value) && value > 0)
            settings.EyeScale = value;
    }

    static string FindSection(string text, string key)
    {
        string marker = "k=\"" + key + "\"";
        int keyAt = text.IndexOf(marker, StringComparison.Ordinal);
        if (keyAt < 0) return "";
        int valueAt = text.IndexOf("v=", keyAt, StringComparison.Ordinal);
        if (valueAt < 0) return "";
        int open = text.IndexOf('{', valueAt);
        if (open < 0) return "";
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text.Substring(open + 1, i - open - 1);
        }
        return "";
    }

    static bool TryNumber(string block, string key, out double value)
    {
        value = 0.0;
        if (string.IsNullOrEmpty(block)) return false;
        Match m = Regex.Match(block,
            @"(?:^|[,\s])" + Regex.Escape(key) + @"\s*=\s*(-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)");
        return m.Success && double.TryParse(m.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
