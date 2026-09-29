using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using MapUiApp.Engine;

namespace UiProcessApp.Engine
{
    /// <summary>
    /// Loads the official PakV4 string tables (UTF-8 copies kept in assets/):
    /// ui/Scheme/Case/string.txt and the settlement table string_PVPAcount.txt.
    /// </summary>
    public static class Strings
    {
        public static readonly Dictionary<string, string> Table =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Ids the live INIs reference but the extracted PakV4 tables do not carry
        /// (the live client resolves them from a newer table snapshot). Each alias
        /// maps to the equivalent id whose shipped text matches the live client —
        /// e.g. Text_SkillTitle is set to STR_DESERTSTORM_TITLE by the 五人模式 tab
        /// handler (NewBattleFieldQueue.decompiled.lua:4477-4488) and the live queue
        /// window shows 绝境战场, which is STR_BATTLE_SHAMOT; Text_TIP_DSTime is set
        /// to STR_DSOPEN_TIME (lua:4350-4372) and the live window shows
        /// 每日12:00至次日凌晨1:00开放 = STR_AREAN_DSTIP.
        /// </summary>
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "STR_DESERTSTORM_TITLE", "STR_BATTLE_SHAMOT" },
            { "STRDES_TIME", "STR_AREAN_DSTIP" },
            { "STR_DSOPEN_TIME", "STR_AREAN_DSTIP" },
            { "STR_DSOPEN_WEEKTIME", "STR_DES_TIME" },
        };

        public static void Load(params string[] paths)
        {
            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;
                foreach (var raw in TextFile.ReadLines(path))
                {
                    var line = raw.TrimEnd();
                    if (line.Length == 0 || line.StartsWith("ID\t")) continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 3) continue;
                    var key = parts[0].Trim();
                    if (!key.StartsWith("STR_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Table.ContainsKey(key)) continue;
                    Table[key] = ExtractMarkupText(parts[2]);
                }
            }
        }

        public static string Resolve(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return TryResolve(value, out var resolved) ? resolved : value;
        }

        /// <summary>
        /// Resolves a string id. Falls back to inserting the missing separator for
        /// shipped data typos (e.g. STRDES_TIME → STR_DES_TIME).
        /// </summary>
        public static bool TryResolve(string value, out string resolved)
        {
            resolved = null;
            if (string.IsNullOrEmpty(value)) return false;
            if (Table.TryGetValue(value, out resolved)) return true;
            if (Aliases.TryGetValue(value, out var alias) && Table.TryGetValue(alias, out resolved)) return true;
            if (value.StartsWith("STR", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("STR_", StringComparison.OrdinalIgnoreCase))
            {
                var fixedId = "STR_" + value.Substring(3);
                if (Table.TryGetValue(fixedId, out resolved)) return true;
            }
            return false;
        }

        private static string ExtractMarkupText(string markup)
        {
            var matches = Regex.Matches(markup, "text=\"([^\"]*)\"");
            string text;
            if (matches.Count > 0)
            {
                var builder = new System.Text.StringBuilder();
                foreach (Match match in matches) builder.Append(match.Groups[1].Value);
                text = builder.ToString();
            }
            else
            {
                text = markup ?? "";
            }
            // Strip engine rich-text tokens: leading style tags (<D0>), inline icon
            // references (<1010>) and <image>/<text> wrappers without attributes.
            return Regex.Replace(text, "<[^<>]{1,64}>", "").Trim();
        }
    }
}
