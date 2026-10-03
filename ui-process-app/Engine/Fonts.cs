using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Media;

using MapUiApp.Engine;

namespace UiProcessApp.Engine
{
    /// <summary>
    /// KGUI font schemes: font.ini ([scheme] FontID/Color/Size/BorderColor/BorderSize)
    /// + fontlist.ini ([fontID] base Size) + color.txt (name → RGB), as shipped.
    /// </summary>
    public static class Fonts
    {
        private sealed class Scheme
        {
            public int FontId;
            public string ColorName = "white";
            public int Size;
            public string BorderColorName;
            public int BorderSize;
        }

        private static readonly Dictionary<int, Scheme> Schemes = new Dictionary<int, Scheme>();
        private static readonly Dictionary<int, int> FontSizes = new Dictionary<int, int>();

        /// <summary>FontID -> shipped font file (fontlist.ini `File=`, e.g. \UI\Font\fzht_GBK.ttf).</summary>
        private static readonly Dictionary<int, string> FontFiles = new Dictionary<int, string>();
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        public static bool Loaded { get; private set; }

        public static void Load(string fontIni, string fontListIni, string colorTxt)
        {
            LoadColors(colorTxt);

            if (File.Exists(fontListIni))
            {
                foreach (var (section, values) in ReadIni(fontListIni))
                {
                    if (!int.TryParse(section, out var id)) continue;
                    FontSizes[id] = ParseInt(values, "Size", 20);
                    var file = Get(values, "File", null);
                    if (!string.IsNullOrWhiteSpace(file)) FontFiles[id] = file;
                }
            }

            if (File.Exists(fontIni))
            {
                foreach (var (section, values) in ReadIni(fontIni))
                {
                    if (!int.TryParse(section, out var id)) continue;
                    Schemes[id] = new Scheme
                    {
                        FontId = ParseInt(values, "FontID", 0),
                        ColorName = Get(values, "Color", "white"),
                        Size = ParseInt(values, "Size", 0),
                        BorderColorName = Get(values, "BorderColor", null),
                        BorderSize = ParseInt(values, "BorderSize", 0),
                    };
                }
            }
            Loaded = Schemes.Count > 0;
        }

        public static bool TryGet(int schemeId, out double size, out Color color, out Color? border)
        {
            size = 14;
            color = Color.FromRgb(240, 240, 240);
            border = null;
            if (!Schemes.TryGetValue(schemeId, out var scheme)) return false;
            size = scheme.Size > 0
                ? scheme.Size
                : FontSizes.TryGetValue(scheme.FontId, out var baseSize) ? baseSize : 14;
            if (Colors.TryGetValue(scheme.ColorName ?? "", out var c)) color = c;
            if (scheme.BorderSize > 0 && scheme.BorderColorName != null &&
                Colors.TryGetValue(scheme.BorderColorName, out var bc))
                border = bc;
            return true;
        }

        /// <summary>
        /// Resolves a named color from the shipped color.txt (the engine's
        /// `FontColor` key overrides the font scheme's fill color with one of these).
        /// A literal `#RRGGBB` is accepted too: quality colors are engine Lua RGB
        /// values (GetItemFontColorByQuality in ui/script/item.lua) that have no
        /// color.txt name, e.g. quality 2 = #00D24B.
        /// </summary>
        public static bool TryGetColor(string name, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            if (trimmed.StartsWith("#") && trimmed.Length == 7 &&
                uint.TryParse(trimmed.Substring(1), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var rgb))
            {
                color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                return true;
            }
            return Colors.TryGetValue(trimmed, out color);
        }

        /// <summary>
        /// File name of the shipped font for a scheme (fontlist FontID -> File), e.g.
        /// "fzht_GBK.ttf" for scheme 18. The engine renders every label with these
        /// fonts, so the glyph shapes and vertical metrics must come from them.
        /// </summary>
        public static bool TryGetFontFile(int schemeId, out string fileName)
        {
            fileName = null;
            if (!Schemes.TryGetValue(schemeId, out var scheme)) return false;
            if (!FontFiles.TryGetValue(scheme.FontId, out var file) || string.IsNullOrWhiteSpace(file)) return false;
            fileName = Path.GetFileName(file.Replace('\\', '/'));
            return !string.IsNullOrWhiteSpace(fileName);
        }

        private static void LoadColors(string path)
        {
            if (!File.Exists(path)) return;
            foreach (var raw in TextFile.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("name")) continue;
                var parts = line.Split('\t');
                if (parts.Length < 4) continue;
                if (byte.TryParse(parts[1], out var r) && byte.TryParse(parts[2], out var g) &&
                    byte.TryParse(parts[3], out var b))
                    Colors[parts[0].Trim()] = Color.FromRgb(r, g, b);
            }
        }

        private static IEnumerable<(string Section, Dictionary<string, string> Values)> ReadIni(string path)
        {
            string section = null;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in TextFile.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (section != null) yield return (section, values);
                    section = line.Substring(1, line.Length - 2).Trim();
                    values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                var eq = line.IndexOf('=');
                if (eq <= 0 || section == null) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            if (section != null) yield return (section, values);
        }

        private static string Get(Dictionary<string, string> values, string key, string fallback)
            => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        private static int ParseInt(Dictionary<string, string> values, string key, int fallback)
            => values.TryGetValue(key, out var value) &&
               int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed : fallback;
    }
}
