using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    public sealed class MapEntry
    {
        public readonly string Id;
        public readonly string Display;
        public readonly string Resource;
        public readonly string Variant;
        public readonly string Folder;

        public MapEntry(string id, string display, string resource, string variant, string folder)
        {
            Id = id;
            Display = display;
            Resource = resource;
            Variant = variant;
            Folder = folder;
        }
    }

    public sealed class AreaRow
    {
        public string Name;
        public double X;
        public double Y;
    }

    public sealed class MapBundle
    {
        public MapEntry Entry;
        public Dictionary<string, string> Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<AreaRow> Areas = new List<AreaRow>();
        public string MiddlemapPath;
        public BitmapSource Middlemap;
        public double Width = 1024, Height = 896, Scale = 1, StartX, StartY;

        public double ToImageX(double worldX) => (worldX - StartX) * Scale;
        public double ToImageY(double worldY) => Height - (worldY - StartY) * Scale;

        public (double x, double y) WorldCenter()
        {
            double cx = StartX + (Width / 2) / Scale;
            double cy = StartY + (Height / 2) / Scale;
            return (cx, cy);
        }

        /// <summary>The player's stand-in position: the map's main area (first area.tab row).</summary>
        public (double x, double y) PlayerWorld()
        {
            if (Areas.Count > 0 && (Areas[0].X != 0 || Areas[0].Y != 0)) return (Areas[0].X, Areas[0].Y);
            return WorldCenter();
        }
    }

    public static class GameData
    {
        public static readonly MapEntry[] Maps =
        {
            new MapEntry("longmen-day", "龙门绝境", "龙门寻宝", "日间", "龙门寻宝minimap_mb"),
            new MapEntry("longmen-night", "龙门绝境·夜", "龙门寻宝_夜晚", "夜晚", "龙门寻宝_夜晚minimap_mb"),
            new MapEntry("cangming", "沧溟绝境", "海岛绝境", "标准", "海岛绝境minimap_mb"),
            new MapEntry("bailong", "白龙绝境", "白龙绝境", "标准", "白龙绝境minimap_mb"),
            new MapEntry("tianyuan", "天原绝境", "天原绝境", "标准", "天原绝境minimap_mb"),
            new MapEntry("erhai", "洱海绝境", "洱海绝境", "标准", "洱海绝境minimap_mb"),
            new MapEntry("linhai", "林海绝境", "林海绝境", "奇境寻宝", "林海绝境minimap_mb"),
        };

        public static string RepoRoot { get; private set; }
        public static string ProofRoot { get; private set; }
        public static string TextRoot { get; private set; }
        public static string UiRoot { get; private set; }
        public static readonly Dictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Keys whose string table was not extracted; values reconstructed from the
        // matching entries that do exist (e.g. STRING_WORLDMAP == STR_MIDDLEMAP_WORLDMAP).
        private static readonly Dictionary<string, string> StringAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["STRING_WORLDMAP"] = "世界地图",
            ["STR_EXPLORE"] = "探索",
            ["STR_M_SCALE"] = "缩放",
        };

        public static string ResolveString(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (Strings.TryGetValue(value, out var resolved)) return resolved;
            return StringAliases.TryGetValue(value, out var alias) ? alias : value;
        }

        private static void LoadStrings()
        {
            foreach (var file in new[] { "string.txt", "String_Comman.txt", "string_ArenaCorpsPanel.txt" })
                LoadStringTable(Path.Combine(TextRoot, "ui", "Scheme", "Case", file));
        }

        private static void LoadStringTable(string path)
        {
            if (!File.Exists(path)) return;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.TrimEnd();
                if (line.Length == 0 || line.StartsWith("ID\t")) continue;
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                var key = parts[0].Trim();
                if (!key.StartsWith("STR_", StringComparison.OrdinalIgnoreCase)) continue;
                Strings[key] = ExtractMarkupText(parts[2]);
            }
        }

        private static string ExtractMarkupText(string markup)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(markup, "text=\"([^\"]*)\"");
            if (matches.Count == 0) return markup;
            var builder = new System.Text.StringBuilder();
            foreach (System.Text.RegularExpressions.Match match in matches) builder.Append(match.Groups[1].Value);
            return builder.ToString();
        }

        public static void Locate()
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "proof", "minimap")))
                {
                    RepoRoot = dir;
                    break;
                }
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            if (RepoRoot == null) throw new DirectoryNotFoundException("Could not locate the repository root (proof/minimap) above " + AppContext.BaseDirectory);
            ProofRoot = Path.Combine(RepoRoot, "proof", "minimap");
            UiRoot = Path.Combine(ProofRoot, "ui");
            TextRoot = Path.Combine(RepoRoot, "map-ui-app", "assets", "text");
            LoadStrings();
        }

        public static MapBundle LoadMap(string id)
        {
            var entry = Array.Find(Maps, m => m.Id == id) ?? Maps[0];
            var bundle = new MapBundle { Entry = entry };
            var textBase = Path.Combine(TextRoot, "extracted", "data", "source", "maps", entry.Folder);
            var configPath = Path.Combine(textBase, "config.ini");
            if (File.Exists(configPath))
            {
                var sections = ParseIniSections(File.ReadAllText(configPath));
                bundle.Config = sections.TryGetValue("middlemap0", out var middlemap) ? middlemap : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            var areaPath = Path.Combine(textBase, "area.tab");
            if (File.Exists(areaPath)) bundle.Areas = ParseAreas(File.ReadAllText(areaPath));
            bundle.MiddlemapPath = Path.Combine(ProofRoot, "extracted", "data", "source", "maps", entry.Folder, "middlemap.png");
            if (File.Exists(bundle.MiddlemapPath)) bundle.Middlemap = LoadPng(bundle.MiddlemapPath);
            bundle.Width = GetDouble(bundle.Config, "width", 1024);
            bundle.Height = GetDouble(bundle.Config, "height", 896);
            bundle.Scale = GetDouble(bundle.Config, "scale", 1);
            bundle.StartX = GetDouble(bundle.Config, "startx", 0);
            bundle.StartY = GetDouble(bundle.Config, "starty", 0);
            return bundle;
        }

        public static string MosaicPath => Path.Combine(ProofRoot, "screenshots", "08_minimap_mosaic_correct_half.png");

        public static BitmapSource LoadPng(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static Dictionary<string, Dictionary<string, string>> ParseIniSections(string text)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> current = null;
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[line.Substring(1, line.Length - 2)] = current;
                    continue;
                }
                if (current == null) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                current[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return result;
        }

        private static List<AreaRow> ParseAreas(string text)
        {
            var rows = new List<AreaRow>();
            var lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0) return rows;
            var headers = lines[0].Split('\t');
            int nameIndex = Array.FindIndex(headers, h => h.Trim().Equals("name", StringComparison.OrdinalIgnoreCase));
            int xIndex = Array.FindIndex(headers, h => h.Trim().Equals("x", StringComparison.OrdinalIgnoreCase));
            int yIndex = Array.FindIndex(headers, h => h.Trim().Equals("y", StringComparison.OrdinalIgnoreCase));
            if (nameIndex < 0 || xIndex < 0 || yIndex < 0) return rows;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var parts = lines[i].Split('\t');
                if (parts.Length <= Math.Max(nameIndex, Math.Max(xIndex, yIndex))) continue;
                if (!double.TryParse(parts[xIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) continue;
                if (!double.TryParse(parts[yIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) continue;
                var name = parts[nameIndex].Trim();
                if (name.Length == 0) continue;
                rows.Add(new AreaRow { Name = name, X = x, Y = y });
            }
            return rows;
        }

        private static double GetDouble(Dictionary<string, string> config, string key, double fallback)
            => config.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }
}
