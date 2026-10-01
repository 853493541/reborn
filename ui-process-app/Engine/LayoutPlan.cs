using System;
using System.Collections.Generic;
using System.IO;
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
            /// <summary>Text child whose $Text gets the matching RowTexts entry (by
            /// name suffix); when empty the first Text section in the clone is used.
            /// The shipped list is data-driven, so the sample rows carry the values
            /// observed in the reference capture.</summary>
            public string TextSection { get; set; }
            public List<string> RowTexts { get; set; }
            /// <summary>Image child whose Frame gets the matching RowFrames entry (by
            /// name suffix) — e.g. the NPC filter rows' checkbox states, which the
            /// script swaps between UnCheckNormal 4 and CheckNormal 0
            /// (MiddleMap.decompiled.lua:501-504, UpdateCheckTitle :5602-5640).</summary>
            public string ImageSection { get; set; }
            public List<int> RowFrames { get; set; }
            /// <summary>Table-driven rows: when set, the rows come from the shipped
            /// per-map tables (minimap/npc.tab + doodad.tab category rows, UTF-8
            /// copies under Data/table) instead of Count/RowTexts — the same source
            /// the Lua's UpdateNpcDoodad appends from (kind + defaultcheck).</summary>
            public List<RowSource> RowSources { get; set; }
            /// <summary>Image_NpcOption frames for the table rows' check state:
            /// CheckNormal 0 / UnCheckNormal 4 (MiddleMap.decompiled.lua:501-504).</summary>
            public int CheckedFrame { get; set; } = 0;
            public int UncheckedFrame { get; set; } = 4;
            /// <summary>Child section names dropped from every cloned item — the
            /// script's UpdateAreaOrNpcTruckState swaps the trunk's Bg1/Bg2 art and
            /// the capture shows no Image_ListCover/Image_Minimize in the rows, so
            /// the viewer hides them per row state (the clone comes from the raw
            /// INI, so the window-level `hide` list cannot reach it).</summary>
            public List<string> Hide { get; set; }
        }

    /// <summary>One shipped table feeding list rows: category rows are selected by
    /// `FilterColumn == FilterValue` (npc.tab npcid=0 / doodad.tab doodadid=0).</summary>
    public sealed class RowSource
    {
        public string Table { get; set; }
        public string KindColumn { get; set; }
        public string CheckColumn { get; set; }
        public string FilterColumn { get; set; }
        public string FilterValue { get; set; }
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

        /// <summary>
        /// Tabs the client hides while their activity is off (InitWndBattleField drops
        /// modes whose IsActivityOn is false). They only get a slot in the strip when
        /// the rendered page itself is that tab (i.e. the player is on it).
        /// </summary>
        public List<string> Gated { get; set; }
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
    /// When `Table` is set the text comes from a shipped data table (UTF-8 copy
    /// under Data/table) instead of a literal — e.g. Text_Region is
    /// WorldMap_GetRegion(regionID).szRegionName (RegionMap.tab) and
    /// Text_SmallMap is Table_GetMiddleMap(mapID)[i] (MapList.tab MiddleMap0).
    /// </summary>
    public sealed class TextOverride
    {
        public string Section { get; set; }
        public string Text { get; set; }
        public string Table { get; set; }
        public string TableKey { get; set; }
        public string TableKeyColumn { get; set; }
        public string TableColumn { get; set; }
    }

    /// <summary>
    /// Runtime image override: the Lua swaps an element's atlas and/or frame after
    /// load from live data (UpdateAnniversaryTabIcon -> FromUITex(activity icon path,
    /// frame) for the tab badges). The inventory carries the values matching the
    /// captured state; a null Image/Frame keeps the authored value. `Checked`
    /// replays the script's `CheckBox:Check(true)` (the checked frame group is
    /// painted, e.g. the MiddleMap 地图 tab).
    /// </summary>
    public sealed class ImageOverride
    {
        public string Section { get; set; }
        public string Image { get; set; }
        public int? Frame { get; set; }
        public bool? Checked { get; set; }
    }

    /// <summary>
    /// Runtime appended text item: the MessageBox module builds its body with
    /// `handleMsg:AppendItemFromString(text, font)` (there is no authored section
    /// for it), so the static render injects a Text child into the list handle.
    /// The inventory carries the string for the captured state.
    /// </summary>
    public sealed class AppendSpec
    {
        public string Container { get; set; }
        public string Text { get; set; }
        public int? Font { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? Top { get; set; }
        public int? HAlign { get; set; }
        public int? VAlign { get; set; }
    }

    /// <summary>
    /// A viewer-defined page for windows the game switches by data instead of by
    /// `Page_*` sections (the loading screen shows a different map per transfer).
    /// Selecting the page replays its overrides on top of the window's own.
    /// </summary>
    public sealed class PageState
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public List<ImageOverride> Images { get; set; }
        public List<TextOverride> Texts { get; set; }
        public List<AdjustSpec> Adjust { get; set; }
        /// <summary>Sections dropped for this page on top of the window's own
        /// hide list (e.g. the BattleFieldMap's map-suffixed team/line elements
        /// that only exist for map 512 — the runtime looks up
        /// Handle_Team_&lt;currentMapID&gt;_n, so other maps show none).</summary>
        public List<string> Hide { get; set; }
    }

    /// <summary>
    /// Runtime geometry override: some windows resize/reposition parts of their
    /// layout from the script (MapQueue.UpdateListSize sets the list, background
    /// and 自动进入 checkbox heights/offsets from the row count). The inventory
    /// carries the values for the state the static render shows.
    /// </summary>
    public sealed class AdjustSpec
    {
        public string Section { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double? RelX { get; set; }
        public double? RelY { get; set; }
        /// <summary>Overrides the authored ImageType (10 = nine-slice) when the
        /// client's render proves a frame is diced but the INI omits the key.</summary>
        public int? ImageType { get; set; }
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
                if (text == null || string.IsNullOrWhiteSpace(text.Section)) continue;
                string value = text.Text;
                if (!string.IsNullOrWhiteSpace(text.Table))
                {
                    var table = TabTable.Load(text.Table);
                    value = table?.Lookup(text.TableKeyColumn, text.TableKey, text.TableColumn);
                }
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (!filtered.ByName.TryGetValue(text.Section, out var section)) continue;
                section.Values["$Text"] = value;
            }
        }

        /// <summary>Applies the Lua's runtime FromUITex calls (inventory `images`).</summary>
        public static void ApplyImages(IniFile filtered, IEnumerable<ImageOverride> images)
        {
            if (images == null) return;
            foreach (var image in images)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.Section)) continue;
                if (!filtered.ByName.TryGetValue(image.Section, out var section)) continue;
                if (!string.IsNullOrWhiteSpace(image.Image)) section.Values["Image"] = image.Image;
                if (image.Frame.HasValue)
                    section.Values["Frame"] = image.Frame.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (image.Checked.HasValue)
                    section.Values["CheckedWhenCreate"] = image.Checked.Value ? "1" : "0";
            }
        }

        /// <summary>
        /// Mirrors AppendItemFromString: injects a Text child into a list handle.
        /// The injected item flows through the normal HandleType 3/6 layout (and is
        /// sized by its measured text), so the MessageBox body renders where the
        /// script would put it. A `Top` offset wraps the text in a spacer box because
        /// list items ignore authored offsets (Wnd_Msg authors FlexMarginTop=20).
        /// </summary>
        public static void ApplyAppends(IniFile filtered, IEnumerable<AppendSpec> appends)
        {
            if (appends == null) return;
            int index = 0;
            foreach (var append in appends)
            {
                index++;
                if (append == null || string.IsNullOrWhiteSpace(append.Container) ||
                    string.IsNullOrWhiteSpace(append.Text)) continue;
                if (!filtered.ByName.TryGetValue(append.Container, out var container)) continue;

                var name = $"__append_{append.Container}_{index}";
                if (filtered.ByName.ContainsKey(name)) continue;
                var width = append.Width ?? container.GetInt("Width");
                var height = append.Height ?? 20;
                var top = append.Top ?? 0;
                var parent = append.Container;
                if (top > 0)
                {
                    var box = new IniSection { Name = name + "_box" };
                    box.Values["._WndType"] = "WndWindow";
                    box.Values["._Parent"] = append.Container;
                    if (width > 0)
                        box.Values["Width"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    box.Values["Height"] = (height + top).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    filtered.Sections.Add(box);
                    filtered.ByName[box.Name] = box;
                    parent = box.Name;
                }

                var section = new IniSection { Name = name };
                section.Values["._WndType"] = "Text";
                section.Values["._Parent"] = parent;
                section.Values["$Text"] = append.Text;
                section.Values["FontScheme"] = (append.Font ?? 18)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["Left"] = "0";
                section.Values["Top"] = top.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (width > 0)
                    section.Values["Width"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["Height"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["HAlign"] = (append.HAlign ?? 1)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["VAlign"] = (append.VAlign ?? 1)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                filtered.Sections.Add(section);
                filtered.ByName[name] = section;
            }
        }

        /// <summary>Applies the Lua's runtime SetSize/SetRelPos calls (inventory `adjust`).</summary>
        public static void ApplyAdjustments(IniFile filtered, IEnumerable<AdjustSpec> adjustments)
        {
            if (adjustments == null) return;
            foreach (var adjust in adjustments)
            {
                if (adjust == null || string.IsNullOrWhiteSpace(adjust.Section)) continue;
                if (!filtered.ByName.TryGetValue(adjust.Section, out var section)) continue;
                void Set(string key, double? value)
                {
                    if (value.HasValue)
                        section.Values[key] = value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                Set("Width", adjust.Width);
                Set("Height", adjust.Height);
                Set("Left", adjust.Left);
                Set("Top", adjust.Top);
                Set("RelX", adjust.RelX);
                Set("RelY", adjust.RelY);
                if (adjust.ImageType.HasValue)
                    section.Values["ImageType"] = adjust.ImageType.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
        /// only the listed modes and pin each one at x0 + index*step. The pinned
        /// sections carry TabFixed so the WndPageSet auto-flow leaves them alone.
        /// Activity-gated tabs (strip.Gated) stay off unless the rendered page is
        /// that tab; radio tabs check on click, so the rendered page's tab is the
        /// checked one (its CheckedWhenCreate is forced on, siblings off).
        /// </summary>
        public static void ApplyTabs(IniFile filtered, TabStrip strip, string selectedPage = null)
        {
            if (strip == null || string.IsNullOrWhiteSpace(strip.Parent) || strip.Show == null) return;
            var gated = new HashSet<string>(strip.Gated ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            string TabForPage(string page) => string.IsNullOrWhiteSpace(page) || !page.StartsWith("Page_", StringComparison.OrdinalIgnoreCase)
                ? null
                : "CheckBox_" + page.Substring("Page_".Length);

            var wanted = new List<string>();
            foreach (var name in strip.Show)
            {
                if (gated.Contains(name) &&
                    !string.Equals(name, TabForPage(selectedPage), StringComparison.OrdinalIgnoreCase))
                    continue;
                wanted.Add(name);
            }
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

            var selectedTab = TabForPage(selectedPage);
            for (int i = 0; i < wanted.Count; i++)
            {
                if (!filtered.ByName.TryGetValue(wanted[i], out var section)) continue;
                section.Values["Left"] = (strip.X0 + i * strip.Step).ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["TabFixed"] = "1";
            }

            if (selectedTab != null && wantedSet.Contains(selectedTab) &&
                filtered.ByName.TryGetValue(selectedTab, out var selectedSection) &&
                selectedSection.GetInt("CheckedWhenCreate") == 0)
            {
                foreach (var name in wanted)
                    if (filtered.ByName.TryGetValue(name, out var tab))
                        tab.Values["CheckedWhenCreate"] = "0";
                selectedSection.Values["CheckedWhenCreate"] = "1";
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
                    // Runtime map layers (storm-line segments) are not chrome; counting
                    // them as old-skin art makes the whole map layer look like an old
                    // root once the heat-map items are hidden (it then gets dropped as
                    // a duplicate of the new-skin panel background).
                    if (image.IndexOf("StormLine", StringComparison.OrdinalIgnoreCase) >= 0) continue;
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

                // Table-driven rows (RowSources) come from the shipped per-map tables;
                // otherwise the authored prototype count + RowTexts/RowFrames are used.
                var tableRows = ReadRowSources(template.RowSources);
                int count = tableRows.Count > 0 ? tableRows.Count : (template.Count > 0 ? template.Count : 1);
                double rowHeight = prototype.GetInt("Height");
                var templateHide = new HashSet<string>(template.Hide ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                for (int row = 0; row < count; row++)
                {
                    var prefix = $"__lt_{template.Container}_{row}_";
                    var clones = new List<IniSection>();
                    void Walk(IniSection section, string parentName)
                    {
                        if (templateHide.Contains(section.Name)) return;
                        var clone = new IniSection { Name = prefix + section.Name };
                        foreach (var pair in section.Values) clone.Values[pair.Key] = pair.Value;
                        clone.Values["._Parent"] = parentName;
                        clones.Add(clone);
                        if (byParent.TryGetValue(section.Name, out var children))
                            foreach (var child in children) Walk(child, clone.Name);
                    }
                    Walk(prototype, template.Container);
                    if (clones.Count == 0) continue;

                    var root = clones[0];
                    root.Values["PosType"] = "0";
                    root.Values["Left"] = "0";
                    root.Values["Top"] = (rowHeight * row).ToString(System.Globalization.CultureInfo.InvariantCulture);

                    string rowText = tableRows.Count > 0 ? tableRows[row].Text
                        : (template.RowTexts != null && row < template.RowTexts.Count ? template.RowTexts[row] : null);
                    if (!string.IsNullOrEmpty(rowText))
                    {
                        IniSection textClone = null;
                        if (!string.IsNullOrWhiteSpace(template.TextSection))
                        {
                            var suffix = template.TextSection;
                            textClone = clones.FirstOrDefault(c =>
                                c.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                        }
                        if (textClone == null)
                            textClone = clones.FirstOrDefault(c => c.Get("._WndType") == "Text");
                        if (textClone != null) textClone.Values["$Text"] = rowText;
                    }

                    int rowFrame = tableRows.Count > 0
                        ? (tableRows[row].Checked ? template.CheckedFrame : template.UncheckedFrame)
                        : (template.RowFrames != null && row < template.RowFrames.Count ? template.RowFrames[row] : -1);
                    if (rowFrame >= 0)
                    {
                        IniSection imageClone = null;
                        if (!string.IsNullOrWhiteSpace(template.ImageSection))
                        {
                            var suffix = template.ImageSection;
                            imageClone = clones.FirstOrDefault(c =>
                                c.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                        }
                        if (imageClone != null)
                            imageClone.Values["Frame"] = rowFrame.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    foreach (var clone in clones)
                    {
                        filtered.Sections.Add(clone);
                        filtered.ByName[clone.Name] = clone;
                    }
                }
            }
        }

        private sealed class TableRow
        {
            public string Text;
            public bool Checked;
        }

        private static List<TableRow> ReadRowSources(List<RowSource> sources)
        {
            var rows = new List<TableRow>();
            if (sources == null) return rows;
            foreach (var source in sources)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.Table)) continue;
                var table = TabTable.Load(source.Table);
                if (table == null) continue;
                int kindIndex = table.ColumnIndex(source.KindColumn ?? "kind");
                int checkIndex = table.ColumnIndex(source.CheckColumn ?? "defaultcheck");
                int filterIndex = string.IsNullOrWhiteSpace(source.FilterColumn)
                    ? -1 : table.ColumnIndex(source.FilterColumn);
                string filterValue = source.FilterValue ?? "0";
                foreach (var row in table.Rows)
                {
                    if (filterIndex >= 0)
                    {
                        var value = filterIndex < row.Length ? row[filterIndex].Trim() : "";
                        if (!string.Equals(value, filterValue, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    var kind = kindIndex >= 0 && kindIndex < row.Length ? row[kindIndex].Trim() : "";
                    if (string.IsNullOrWhiteSpace(kind)) continue;
                    var check = checkIndex >= 0 && checkIndex < row.Length ? row[checkIndex].Trim() : "";
                    rows.Add(new TableRow
                    {
                        Text = kind,
                        Checked = check.Length > 0 && check != "0",
                    });
                }
            }
            return rows;
        }
    }

    /// <summary>
    /// Reader for the shipped tab-separated data tables (UTF-8 copies under
    /// Data/table): first row is the column header, values are matched by column
    /// name. Mirrors KG_Table/g_tTable access for the tables the MiddleMap uses —
    /// ui/scheme/case/MapList.tab (dwRegionID, MiddleMap0..N), RegionMap.tab
    /// (RegionName) and each map's minimap/npc.tab + doodad.tab (kind,
    /// defaultcheck; table_defs_dynamic.lua g_tMapNpcTitle/g_tMapDoodad).
    /// </summary>
    public sealed class TabTable
    {
        private static readonly Dictionary<string, TabTable> Cache =
            new Dictionary<string, TabTable>(StringComparer.OrdinalIgnoreCase);

        public readonly List<string> Columns = new List<string>();
        public readonly List<string[]> Rows = new List<string[]>();

        public static TabTable Load(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) return null;
            if (Cache.TryGetValue(relative, out var hit)) return hit;
            TabTable table = null;
            var path = Path.Combine(Paths.AppRoot ?? "", "Data", "table",
                                    relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                table = new TabTable();
                var header = false;
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.TrimEnd('\r');
                    if (line.Length == 0) continue;
                    var cells = line.Split('\t');
                    if (!header)
                    {
                        foreach (var cell in cells) table.Columns.Add(cell.Trim());
                        header = true;
                        continue;
                    }
                    table.Rows.Add(cells);
                }
            }
            Cache[relative] = table;
            return table;
        }

        public int ColumnIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            for (int i = 0; i < Columns.Count; i++)
                if (string.Equals(Columns[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public string Lookup(string keyColumn, string key, string valueColumn)
        {
            int keyIndex = ColumnIndex(string.IsNullOrWhiteSpace(keyColumn) && Columns.Count > 0 ? Columns[0] : keyColumn);
            int valueIndex = ColumnIndex(valueColumn);
            if (keyIndex < 0 || valueIndex < 0 || key == null) return null;
            foreach (var row in Rows)
            {
                if (keyIndex >= row.Length) continue;
                if (!string.Equals(row[keyIndex].Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                return valueIndex < row.Length ? row[valueIndex].Trim() : null;
            }
            return null;
        }
    }
}
