using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MapUiApp.Engine;

namespace UiProcessApp.Engine
{
    public sealed class LayoutPlan
    {
        public IniFile Filtered;
        public List<string> Pages;
    }

    /// <summary>
    /// KGUI list containers (HandleType=3) hold item prototypes that live in a
    /// separate INI and are cloned into the list at runtime by AppendItemFromIni
    /// (e.g. PVPShowFinal -> Handle_FinalListL items come from PVPShowFinalL.ini).
    /// The prototype parked inside the window INI is never drawn; the clones are.
    /// This describes one such container so a static render can show the authored
    /// row layout instead of the off-window prototype.
    /// </summary>
    public sealed class ListTemplate
    {
        public string Container { get; set; }
        public string Ini { get; set; }
        public string Item { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Mode tab strip of a WndPageSet. The client never shows all tabs: every tab
    /// checkbox is authored at the same origin and the Lua's ShowModeTabs hides all
    /// of them, then shows only the modes bound to the entry point, flowing them at
    /// SetRelX(18 + index * 100) (NewBattleFieldQueue.decompiled.lua:4374-4451).
    /// </summary>
    public sealed class TabStrip
    {
        public string Parent { get; set; }
        public double X0 { get; set; } = 18;
        public double Step { get; set; } = 100;
        public List<string> Show { get; set; }
    }

    /// <summary>
    /// Runtime anchor override (the Lua's `SetPoint(s, sx, sy, r, x, y)`): align the
    /// self side of an element to the parent side, then offset. Examples:
    /// PVPShowPanel.UpdatePos anchors Wnd_HPListL/R to LEFTCENTER/RIGHTCENTER and
    /// Wnd_PlayerInfo to BOTTOMCENTER (PVPShowPanel.lua:155-183). Applied by writing
    /// the equivalent `AnchorArgs` key, which the layout already honours.
    /// </summary>
    public sealed class AnchorSpec
    {
        public string Section { get; set; }
        public string S { get; set; }
        public string R { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>
    /// Runtime text override: the Lua sets specific labels after load (e.g. the
    /// mode tab handlers set Handle_Total/Text_SkillTitle to the mode's title).
    /// </summary>
    public sealed class TextOverride
    {
        public string Section { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// KGUI files declare every page/tab variant as a separately named element
    /// (e.g. Btn_RoomQueue vs Btn_RoomQueue_B) and the engine looks them up by
    /// exact name. This builds the subset visible for one page by walking the
    /// _Parent chain: elements under another Page_* are dropped, everything
    /// without a Page_* ancestor (window chrome, tabs) stays.
    /// </summary>
    public static class LayoutPlanBuilder
    {
        public static LayoutPlan Build(IniFile ini, string selectedPage)
        {
            var pageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string PageOf(string name)
            {
                if (pageCache.TryGetValue(name, out var cached)) return cached;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var current = name;
                string result = null;
                while (!string.IsNullOrWhiteSpace(current) && seen.Add(current))
                {
                    if (current.StartsWith("Page_", StringComparison.OrdinalIgnoreCase)) { result = current; break; }
                    var parent = ini.ByName.TryGetValue(current, out var section) ? section.Get("._Parent") : null;
                    current = parent;
                }
                pageCache[name] = result;
                return result;
            }

            var chain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cursor = selectedPage;
            while (!string.IsNullOrWhiteSpace(cursor))
            {
                chain.Add(cursor);
                cursor = ini.ByName.TryGetValue(cursor, out var section) ? section.Get("._Parent") : null;
            }

            bool OnPage(string name)
            {
                if (string.IsNullOrWhiteSpace(selectedPage)) return true;
                if (chain.Contains(name)) return true;
                var page = PageOf(name);
                return page == null || string.Equals(page, selectedPage, StringComparison.OrdinalIgnoreCase);
            }

            var pages = ini.Sections
                .Where(s => s.Name.StartsWith("Page_", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name)
                .ToList();

            var filtered = new IniFile();
            foreach (var section in ini.Sections)
            {
                if (!OnPage(section.Name)) continue;

                var clone = new IniSection { Name = section.Name };
                foreach (var pair in section.Values) clone.Values[pair.Key] = pair.Value;
                filtered.Sections.Add(clone);
                filtered.ByName[clone.Name] = clone;
            }
            return new LayoutPlan { Filtered = filtered, Pages = pages };
        }

        /// <summary>Keeps only the subtrees under the listed sections (plus their ancestors).</summary>
        public static void ApplyOnly(IniFile filtered, string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;
            var wanted = new HashSet<string>(
                spec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            if (wanted.Count == 0) return;

            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in filtered.Sections)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cursor = section.Name;
                var chain = new List<string>();
                while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                {
                    chain.Add(cursor);
                    cursor = filtered.ByName.TryGetValue(cursor, out var s) ? s.Get("._Parent") : null;
                }
                if (chain.Any(keep2 => wanted.Contains(keep2)) || chain.Count <= 1)
                    foreach (var name in chain) keep.Add(name);
            }

            var survivors = filtered.Sections.Where(s => keep.Contains(s.Name)).ToList();
            filtered.Sections.Clear();
            filtered.ByName.Clear();
            foreach (var section in survivors)
            {
                filtered.Sections.Add(section);
                filtered.ByName[section.Name] = section;
            }
        }

        /// <summary>Drops sections whose name or ancestor chain hits a comma-separated list.</summary>
        public static void ApplyHide(IniFile filtered, string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;
            var hidden = new HashSet<string>(
                spec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            if (hidden.Count == 0) return;

            bool Matches(string name)
            {
                if (hidden.Contains(name)) return true;
                foreach (var pattern in hidden)
                {
                    if (pattern.IndexOf('*') < 0) continue;
                    var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                        .Replace("\\*", ".*") + "$";
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                            name, regex,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return true;
                }
                return false;
            }

            bool Dropped(string name)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cursor = name;
                while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                {
                    if (Matches(cursor)) return true;
                    cursor = filtered.ByName.TryGetValue(cursor, out var section) ? section.Get("._Parent") : null;
                }
                return false;
            }

            var keep = filtered.Sections.Where(s => !Dropped(s.Name)).ToList();
            filtered.Sections.Clear();
            filtered.ByName.Clear();
            foreach (var section in keep)
            {
                filtered.Sections.Add(section);
                filtered.ByName[section.Name] = section;
            }
        }

        /// <summary>
        /// Mirrors the engine's LockShowAndHide handling. The KGUI decoder treats
        /// sections with LockShowAndHide=1 specially: they are created with their
        /// show/hide state locked (the engine clears their visible flag and never
        /// lets the page logic touch them), so the window script is the only thing
        /// that shows them — ShowModeTabs for the tab checkboxes, UpdateAnniversaryTabIcon
        /// for the activity badges, UpdateButtonState/UpdateFeiSha for the queue-state
        /// widgets. The live client screenshot of the idle queue window confirms every
        /// LockShowAndHide=1 section is hidden except the ones the script shows, so a
        /// faithful static render hides them and re-shows the script-shown ones.
        /// </summary>
        public static void ApplyLockedVisibility(IniFile filtered, IEnumerable<string> shown)
        {
            var keep = new HashSet<string>(shown ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            bool Locked(string name)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cursor = name;
                while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                {
                    // A script-shown section unlocks its whole subtree (the tab
                    // checkboxes carry LockShowAndHide=1 and own the tab label).
                    if (keep.Contains(cursor)) return false;
                    if (filtered.ByName.TryGetValue(cursor, out var section) &&
                        section.GetInt("LockShowAndHide") == 1)
                        return true;
                    cursor = filtered.ByName.TryGetValue(cursor, out var s) ? s.Get("._Parent") : null;
                }
                return false;
            }

            var survivors = filtered.Sections.Where(s => !Locked(s.Name) || keep.Contains(s.Name)).ToList();
            if (survivors.Count == filtered.Sections.Count) return;
            filtered.Sections.Clear();
            filtered.ByName.Clear();
            foreach (var section in survivors)
            {
                filtered.Sections.Add(section);
                filtered.ByName[section.Name] = section;
            }
        }

        /// <summary>
        /// Applies the Lua's post-load SetText calls (e.g. the mode tab handlers
        /// write Handle_Total/Text_SkillTitle with the selected mode's title).
        /// </summary>
        public static void ApplyTexts(IniFile filtered, IEnumerable<TextOverride> texts)
        {
            if (texts == null) return;
            foreach (var text in texts)
            {
                if (text == null || string.IsNullOrWhiteSpace(text.Section) ||
                    string.IsNullOrWhiteSpace(text.Text)) continue;
                if (!filtered.ByName.TryGetValue(text.Section, out var section)) continue;
                section.Values["$Text"] = text.Text;
            }
        }

        /// <summary>
        /// Mirrors the Lua's SetPoint calls: writes AnchorArgs ("parentSide,selfSide,dx,dy")
        /// onto the listed sections so the runtime anchors survive into the static render.
        /// </summary>
        public static void ApplyAnchors(IniFile filtered, IEnumerable<AnchorSpec> anchors)
        {
            if (anchors == null) return;
            foreach (var anchor in anchors)
            {
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.Section) ||
                    string.IsNullOrWhiteSpace(anchor.S) || string.IsNullOrWhiteSpace(anchor.R)) continue;
                if (!filtered.ByName.TryGetValue(anchor.Section, out var section)) continue;
                section.Values["AnchorArgs"] = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3}", anchor.R, anchor.S, anchor.X, anchor.Y);
            }
        }

        /// <summary>
        /// Mirrors ShowModeTabs: hide every tab checkbox of the page set, then show
        /// only the listed modes and pin each one at x0 + index * step. The pinned
        /// sections carry TabFixed so the WndPageSet auto-flow leaves them alone.
        /// </summary>
        public static void ApplyTabs(IniFile filtered, TabStrip strip)
        {
            if (strip == null || string.IsNullOrWhiteSpace(strip.Parent) || strip.Show == null) return;
            var wanted = new List<string>(strip.Show);
            var wantedSet = new HashSet<string>(wanted, StringComparer.OrdinalIgnoreCase);
            bool IsTab(IniSection section) =>
                string.Equals(section.Get("._Parent"), strip.Parent, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(section.Get("._WndType"), "WndCheckBox", StringComparison.OrdinalIgnoreCase);

            var hiddenTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in filtered.Sections)
                if (IsTab(section) && !wantedSet.Contains(section.Name)) hiddenTabs.Add(section.Name);

            if (hiddenTabs.Count > 0)
            {
                bool UnderHiddenTab(string name)
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var cursor = name;
                    while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                    {
                        if (hiddenTabs.Contains(cursor)) return true;
                        cursor = filtered.ByName.TryGetValue(cursor, out var s) ? s.Get("._Parent") : null;
                    }
                    return false;
                }
                var keep = filtered.Sections.Where(s => !UnderHiddenTab(s.Name)).ToList();
                filtered.Sections.Clear();
                filtered.ByName.Clear();
                foreach (var section in keep)
                {
                    filtered.Sections.Add(section);
                    filtered.ByName[section.Name] = section;
                }
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                if (!filtered.ByName.TryGetValue(wanted[i], out var section)) continue;
                section.Values["Left"] = (strip.X0 + i * strip.Step).ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["TabFixed"] = "1";
            }
        }

        /// <summary>
        /// 琉璃·境 (the live client skin, 2024-12-23+) keeps the old layout but swaps the
        /// skin art. The INIs carry both sets side by side: old chrome points at
        /// ui/Image/UICommon|UITga, the new skin at ui/Image/UItimate. For the "uitimate"
        /// skin this drops old-art chrome whose box is covered by a same-area UItimate
        /// element (e.g. NewBattleFieldQueue Handle_Bg vs Handle_Bg_960x624,
        /// PVPShowFinal Image_Bg1 vs Image_BgPopUp). Small icons/mode art are left alone
        /// because no same-area replacement exists.
        /// </summary>
        public static void ApplySkin(IniFile filtered, string skin)
        {
            if (!string.Equals(skin, "uitimate", StringComparison.OrdinalIgnoreCase)) return;

            var byParent = new Dictionary<string, List<IniSection>>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in filtered.Sections)
            {
                var parent = section.Get("._Parent") ?? "";
                if (!byParent.TryGetValue(parent, out var siblings)) byParent[parent] = siblings = new List<IniSection>();
                siblings.Add(section);
            }

            // Art kind of a section subtree: 0 none, 1 old, 2 UItimate, 3 both.
            var kind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int Kind(IniSection section)
            {
                if (kind.TryGetValue(section.Name, out var cached)) return cached;
                int result = 0;
                var image = section.Get("Image") ?? "";
                if (image.IndexOf("UItimate", StringComparison.OrdinalIgnoreCase) >= 0) result |= 2;
                else if (image.IndexOf("ui\\Image", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         image.IndexOf("ui/Image", StringComparison.OrdinalIgnoreCase) >= 0) result |= 1;
                if (byParent.TryGetValue(section.Name, out var children))
                    foreach (var child in children) result |= Kind(child);
                kind[section.Name] = result;
                return result;
            }
            foreach (var section in filtered.Sections) Kind(section);

            bool HasOld(IniSection s) => (kind.TryGetValue(s.Name, out var k) && (k & 1) != 0);
            bool HasNew(IniSection s) => (kind.TryGetValue(s.Name, out var k) && (k & 2) != 0);

            // Topmost section of a pure old-skin (or pure new-skin) subtree. Mixed
            // containers (e.g. PVPShowFinal Handle_Bg holds both) are skipped so their
            // individual old children are evaluated on their own.
            bool IsRootOf(IniSection section, Func<IniSection, bool> test, Func<IniSection, bool> exclude)
            {
                if (!test(section) || exclude(section)) return false;
                var cursor = section.Get("._Parent");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                {
                    if (filtered.ByName.TryGetValue(cursor, out var parent) && test(parent) && !exclude(parent)) return false;
                    cursor = filtered.ByName.TryGetValue(cursor, out var s) ? s.Get("._Parent") : null;
                }
                return true;
            }

            bool SameArea(IniSection oldSection, IniSection newSection)
            {
                double ow = oldSection.GetInt("Width"), oh = oldSection.GetInt("Height");
                double nw = newSection.GetInt("Width"), nh = newSection.GetInt("Height");
                if (ow <= 0 || oh <= 0 || nw <= 0 || nh <= 0) return false;
                if (ow * oh < 30000) return false; // only chrome-sized elements
                if (Math.Abs(ow - nw) > ow * 0.08 || Math.Abs(oh - nh) > oh * 0.08) return false;
                double dx = Math.Abs(oldSection.GetInt("Left") - newSection.GetInt("Left"));
                double dy = Math.Abs(oldSection.GetInt("Top") - newSection.GetInt("Top"));
                return dx <= 48 && dy <= 48;
            }

            // Old roots only qualify as skin chrome when they actually paint a large old
            // background/frame; runtime layers (marker/event handles) must not be matched
            // just because a new-skin background happens to share their box.
            bool HasLargeOldArt(IniSection root)
            {
                var stack = new Stack<IniSection>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var section = stack.Pop();
                    var image = section.Get("Image") ?? "";
                    if (image.IndexOf("UItimate", StringComparison.OrdinalIgnoreCase) < 0 &&
                        (image.IndexOf("ui\\Image", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         image.IndexOf("ui/Image", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        double w = section.GetInt("Width"), h = section.GetInt("Height");
                        if (w * h >= 20000) return true;
                    }
                    if (byParent.TryGetValue(section.Name, out var children))
                        foreach (var child in children) stack.Push(child);
                }
                return false;
            }

            // New-skin candidates: any element that draws UItimate art, plus pure new
            // containers (so a container's box can supersede a matching old container).
            bool OwnNewImage(IniSection s)
            {
                var image = s.Get("Image") ?? "";
                return image.IndexOf("UItimate", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            var newRoots = filtered.Sections
                .Where(s => OwnNewImage(s) || IsRootOf(s, HasNew, HasOld))
                .ToList();
            var drop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var oldRoot in filtered.Sections.Where(s => IsRootOf(s, HasOld, HasNew) && HasLargeOldArt(s)))
            {
                if (!newRoots.Any(n => !ReferenceEquals(n, oldRoot) && SameArea(oldRoot, n))) continue;
                var stack = new Stack<IniSection>();
                stack.Push(oldRoot);
                while (stack.Count > 0)
                {
                    var section = stack.Pop();
                    drop.Add(section.Name);
                    if (byParent.TryGetValue(section.Name, out var children))
                        foreach (var child in children) stack.Push(child);
                }
            }
            if (drop.Count == 0) return;

            var keep = filtered.Sections.Where(s => !drop.Contains(s.Name)).ToList();
            filtered.Sections.Clear();
            filtered.ByName.Clear();
            foreach (var section in keep)
            {
                filtered.Sections.Add(section);
                filtered.ByName[section.Name] = section;
            }
        }

        /// <summary>
        /// Clones item prototypes from their own INI into list containers, mirroring
        /// AppendItemFromIni: the clone is re-parented to the container at its origin
        /// (the list layout stacks rows itself, so the prototype's parked PosType/Left
        /// are dropped), while the item's own children keep their relative offsets.
        /// </summary>
        public static void ApplyListTemplates(IniFile filtered, IEnumerable<ListTemplate> templates, Func<string, IniFile> load)
        {
            if (templates == null || load == null) return;
            foreach (var template in templates)
            {
                if (template == null || string.IsNullOrWhiteSpace(template.Container) ||
                    string.IsNullOrWhiteSpace(template.Ini) || string.IsNullOrWhiteSpace(template.Item)) continue;
                if (!filtered.ByName.ContainsKey(template.Container)) continue;

                IniFile source;
                try { source = load(template.Ini); }
                catch { continue; }
                if (source == null || !source.ByName.TryGetValue(template.Item, out var prototype)) continue;

                var byParent = new Dictionary<string, List<IniSection>>(StringComparer.OrdinalIgnoreCase);
                foreach (var section in source.Sections)
                {
                    var parent = section.Get("._Parent") ?? "";
                    if (!byParent.TryGetValue(parent, out var siblings)) byParent[parent] = siblings = new List<IniSection>();
                    siblings.Add(section);
                }

                int count = template.Count > 0 ? template.Count : 1;
                double rowHeight = prototype.GetInt("Height");
                for (int row = 0; row < count; row++)
                {
                    var prefix = $"__lt_{template.Container}_{row}_";
                    var clones = new List<IniSection>();
                    void Walk(IniSection section, string parentName)
                    {
                        var clone = new IniSection { Name = prefix + section.Name };
                        foreach (var pair in section.Values) clone.Values[pair.Key] = pair.Value;
                        clone.Values["._Parent"] = parentName;
                        clones.Add(clone);
                        if (byParent.TryGetValue(section.Name, out var children))
                            foreach (var child in children) Walk(child, clone.Name);
                    }
                    Walk(prototype, template.Container);

                    var root = clones[0];
                    root.Values["PosType"] = "0";
                    root.Values["Left"] = "0";
                    root.Values["Top"] = (rowHeight * row).ToString(System.Globalization.CultureInfo.InvariantCulture);

                    foreach (var clone in clones)
                    {
                        filtered.Sections.Add(clone);
                        filtered.ByName[clone.Name] = clone;
                    }
                }
            }
        }
    }
}
