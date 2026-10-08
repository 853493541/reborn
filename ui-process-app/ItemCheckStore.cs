using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UiProcessApp
{
    /// <summary>
    /// Per-window item checklist: the reviewer ticks each rendered item (section) off
    /// the list while inspecting a window. State lives in a side file
    /// (Data/item_checks.tsv: windowId TAB section TAB 1/0) so the catalog JSON stays
    /// clean; entries survive window switches and app restarts.
    /// </summary>
    public static class ItemCheckStore
    {
        public static string FilePath(string appRoot) => Path.Combine(appRoot, "Data", "item_checks.tsv");

        public static Dictionary<string, bool> Load(string appRoot)
        {
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var path = FilePath(appRoot);
            if (!File.Exists(path)) return map;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split('\t');
                if (parts.Length >= 3 && parts[0].Length > 0)
                    map[Key(parts[0], parts[1])] = parts[2] == "1";
            }
            return map;
        }

        public static void Save(string appRoot, Dictionary<string, bool> map)
        {
            var path = FilePath(appRoot);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var lines = new List<string>
            {
                "# windowId\tsection\t1=checked - the per-item review checklist",
            };
            foreach (var pair in map.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                lines.Add(pair.Key.Replace('\u0001', '\t') + "\t" + (pair.Value ? "1" : "0"));
            File.WriteAllLines(path, lines);
        }

        public static string Key(string windowId, string section) => windowId + "\u0001" + section;

        public static bool IsChecked(Dictionary<string, bool> map, string windowId, string section)
            => map.TryGetValue(Key(windowId, section), out var v) && v;

        public static void Set(Dictionary<string, bool> map, string windowId, string section, bool value)
            => map[Key(windowId, section)] = value;
    }
}
