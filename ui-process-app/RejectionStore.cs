using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UiProcessApp
{
    /// <summary>
    /// "不需要" (not needed) rejections: pressing X in the viewer moves the currently shown
    /// window into a not-needed stage at the bottom of the catalog; pressing X again restores
    /// it to its original stage. State lives in a small side file
    /// (Data/rejected.tsv: windowId TAB originalStageId) so the catalog JSON stays untouched
    /// and the rejection is fully reversible.
    /// </summary>
    public static class RejectionStore
    {
        public const string NotNeededStageId = "not-needed";
        public const string NotNeededStageTitle = "不需要";

        public static string FilePath(string appRoot) => Path.Combine(appRoot, "Data", "rejected.tsv");

        public static Dictionary<string, string> Load(string appRoot)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var path = FilePath(appRoot);
            if (!File.Exists(path)) return map;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split('\t');
                if (parts.Length >= 1 && parts[0].Length > 0)
                    map[parts[0]] = parts.Length > 1 ? parts[1] : "";
            }
            return map;
        }

        public static void Save(string appRoot, Dictionary<string, string> map)
        {
            var path = FilePath(appRoot);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var lines = new List<string>
            {
                "# windowId\toriginalStageId - X in the viewer marks 不需要; X again restores",
            };
            foreach (var pair in map.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                lines.Add(pair.Key + "\t" + pair.Value);
            File.WriteAllLines(path, lines);
        }

        public static StageInfo NotNeededStage(Inventory inv, bool create)
        {
            var stage = inv.Stages.FirstOrDefault(s =>
                string.Equals(s.Id, NotNeededStageId, StringComparison.OrdinalIgnoreCase));
            if (stage == null && create)
            {
                stage = new StageInfo
                {
                    Id = NotNeededStageId,
                    Title = NotNeededStageTitle,
                    Summary = "已标记不需要的界面（查看时按 X 移入；再次按 X 移回原分类）。",
                    Windows = new List<WindowInfo>(),
                };
                inv.Stages.Add(stage);
            }
            return stage;
        }

        public static void Apply(Inventory inv, Dictionary<string, string> rejected)
        {
            if (inv?.Stages == null || rejected == null || rejected.Count == 0) return;
            var stage = NotNeededStage(inv, true);
            foreach (var id in rejected.Keys.ToList())
            {
                var window = inv.Stages
                    .SelectMany(s => s.Windows ?? new List<WindowInfo>())
                    .FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));
                if (window == null)
                {
                    rejected.Remove(id);   // stale entry (window left the catalog)
                    continue;
                }
                var current = inv.Stages.FirstOrDefault(s => (s.Windows ?? new List<WindowInfo>()).Contains(window));
                if (current != null && string.Equals(current.Id, NotNeededStageId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (current != null && string.IsNullOrWhiteSpace(rejected[id])) rejected[id] = current.Id;
                if (current != null) current.Windows.Remove(window);
                stage.Windows.Add(window);
            }
        }

        public static bool IsRejected(Inventory inv, WindowInfo window)
        {
            var stage = NotNeededStage(inv, false);
            return stage != null && window != null && (stage.Windows ?? new List<WindowInfo>()).Contains(window);
        }

        /// <summary>X toggle: a rejected window returns to the stage recorded in the side file.</summary>
        public static void Toggle(Inventory inv, WindowInfo window, Dictionary<string, string> rejected)
        {
            if (inv == null || window == null || rejected == null) return;
            var stage = NotNeededStage(inv, true);
            if (IsRejected(inv, window))
            {
                stage.Windows.Remove(window);
                var origin = rejected.TryGetValue(window.Id, out var s) ? s : null;
                rejected.Remove(window.Id);
                var target = inv.Stages.FirstOrDefault(x =>
                                 string.Equals(x.Id, origin, StringComparison.OrdinalIgnoreCase))
                             ?? inv.Stages.LastOrDefault(x =>
                                 !string.Equals(x.Id, NotNeededStageId, StringComparison.OrdinalIgnoreCase));
                if (target == null) return;
                target.Windows = target.Windows ?? new List<WindowInfo>();
                target.Windows.Add(window);
            }
            else
            {
                var current = inv.Stages.FirstOrDefault(s => (s.Windows ?? new List<WindowInfo>()).Contains(window));
                if (current == null) return;
                rejected[window.Id] = current.Id;
                current.Windows.Remove(window);
                stage.Windows.Add(window);
            }
        }
    }
}
