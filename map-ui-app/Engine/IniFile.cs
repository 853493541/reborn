using System;
using System.Collections.Generic;
using System.IO;

namespace MapUiApp.Engine
{
    public sealed class IniSection
    {
        public string Name = "";
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Get(string key) => Values.TryGetValue(key, out var value) ? value : null;

        public int GetInt(string key, int fallback = 0)
            => int.TryParse(Get(key), out var value) ? value : fallback;

        public bool GetBool(string key, bool fallback = false)
            => int.TryParse(Get(key), out var value) ? value != 0 : fallback;
    }

    public sealed class IniFile
    {
        public readonly List<IniSection> Sections = new List<IniSection>();
        public readonly Dictionary<string, IniSection> ByName = new Dictionary<string, IniSection>(StringComparer.OrdinalIgnoreCase);

        public static IniFile Load(string path)
        {
            var ini = new IniFile();
            IniSection current = null;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = new IniSection { Name = line.Substring(1, line.Length - 2) };
                    ini.Sections.Add(current);
                    ini.ByName[current.Name] = current;
                    continue;
                }
                var eq = line.IndexOf('=');
                if (eq <= 0 || current == null) continue;
                current.Values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            InheritFromBaseSections(ini);
            return ini;
        }

        /// <summary>
        /// The client keeps re-issued element definitions under suffixed names
        /// (e.g. Image_Bg4_0_0 is a newer Image_Bg4_0). The engine reuses the earlier
        /// definition's placement unless the newer one authors it, so do the same for
        /// the only placement keys that are left implicit: Left and Top.
        /// </summary>
        private static void InheritFromBaseSections(IniFile ini)
        {
            foreach (var section in ini.Sections)
            {
                var name = section.Name;
                for (int guard = 0; guard < 4; guard++)
                {
                    string baseName = null;
                    if (name.EndsWith("_0!", StringComparison.OrdinalIgnoreCase)) baseName = name.Substring(0, name.Length - 3);
                    else if (name.EndsWith("_0", StringComparison.OrdinalIgnoreCase)) baseName = name.Substring(0, name.Length - 2);
                    else if (name.EndsWith("!", StringComparison.OrdinalIgnoreCase)) baseName = name.Substring(0, name.Length - 1);
                    if (baseName == null) break;
                    name = baseName;
                    if (!ini.ByName.TryGetValue(baseName, out var baseSection)) continue;
                    if (!section.Values.ContainsKey("Left") && baseSection.Values.TryGetValue("Left", out var left)) section.Values["Left"] = left;
                    if (!section.Values.ContainsKey("Top") && baseSection.Values.TryGetValue("Top", out var top)) section.Values["Top"] = top;
                    break;
                }
            }
        }
    }
}
