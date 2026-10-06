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
            /// <summary>"row" flows the clones left-to-right (each after the previous
            /// sibling's measured width, PosType 9) instead of the default vertical
            /// stack — the message line's items flow right (FormatAllItemPos).</summary>
            public string Flow { get; set; }
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

        /// <summary>
        /// Replays the Lua's SetFontScheme (e.g. MainMessageLine's getFPSFont/getPingFont
        /// switch the value label between the shipped schemes 101 orange2 / 102 red2 /
        /// 105 green2 by the live value). Null keeps the authored FontScheme.
        /// </summary>
        public int? FontScheme { get; set; }

        /// <summary>
        /// Replays the Lua's SetFontColor calls (e.g. the loot rows use
        /// GetItemFontColorByQuality): a shipped color.txt name written to the
        /// engine's own FontColor key, which overrides the scheme's fill.
        /// </summary>
        public string FontColor { get; set; }
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

        /// <summary>
        /// Rarity frame for item slots: the engine's `UpdateItemBoxExtend` draws a
        /// quality-colored border around an item box; the viewer paints it around
        /// the authored slot art. `BorderColor` is a shipped color.txt name,
        /// `BorderWidth` the frame thickness (default 1).
        /// </summary>
        public string BorderColor { get; set; }
        public int? BorderWidth { get; set; }

        /// <summary>
        /// Zoom for a WndMinimap lens background (the engine draws the map texture at
        /// the map config's [config] scale around the player; the inventory passes the
        /// derived factor so the lens shows a local region instead of the whole map).
        /// </summary>
        public double? Zoom { get; set; }
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
        /// <summary>Horizontal flow (PosType 9, measured width, no authored box): the
        /// runtime AppendItemFromString + FormatAllItemPos sequence in a HandleType 3
        /// row, e.g. ACC_TreasureFinal's banishing countdown = 将在(f257) / seconds(f258)
        /// / 秒后传出战场(f257).</summary>
        public bool? Flow { get; set; }
    }

    /// <summary>
    /// Runtime multi-INI append: the script pulls a subtree out of another INI and
    /// attaches it under a handle of the open window — `Target.lua` does
    /// `Handle_Energy:AppendItemFromIni(TargetCommonPath, &lt;kungfu handle&gt;, nil, true)`
    /// (Target.decompiled.lua:967-989), so the target frame is TargetPlayer10.ini +
    /// the selected class handle from TargetCommon.ini. The item keeps its authored
    /// relative position; its `._Parent` is rewired to `container`.
    /// </summary>
    public sealed class AppendIniSpec
    {
        public string Container { get; set; }
        /// <summary>Second INI path relative to the same root as the window's own path.</summary>
        public string Path { get; set; }
        /// <summary>Section whose subtree is appended (e.g. TargetCommon's Handle_TM).</summary>
        public string Item { get; set; }
        /// <summary>LockShowAndHide=1 sections the script shows for this state (merged
        /// into the window's script-shown set).</summary>
        public List<string> Show { get; set; }
        /// <summary>Sections dropped from the appended subtree.</summary>
        public List<string> Hide { get; set; }
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
        /// <summary>Overrides the authored text alignment. The engine draws runtime-set
        /// messages centred in their box even where the INI leaves HAlign at the left
        /// default (ExitPanel's Text_ExitGame box is 241 wide for a ~150px message and
        /// the icon+box group is centred on the dialog).</summary>
        public int? HAlign { get; set; }
        /// <summary>Overrides Alpha. The authored prototypes of runtime clones are often
        /// parked invisible (RemainingTimeNotify's Image_Num is Alpha=0 in the INI) while
        /// the script's cloned items render fully — the inventory replays the visible
        /// state for the clone sections.</summary>
        public int? Alpha { get; set; }
        /// <summary>Overrides the authored PosType when the viewer's inference for it is
        /// wrong for this section. BigBagPanel's Handle_Total authors PosType 10 but sits
        /// at the window origin: the script's UpdateSize resizes its background children
        /// (Handle_Bg/Image_Glassmorphism/Image_HBg) to the full frame (BigBagPanel.lua
        /// L1525-1555, init UpdateSize call L6788-6795), so the frame cannot be offset.</summary>
        public int? PosType { get; set; }
        /// <summary>Clips the container's children to its authored rect. The engine clips
        /// a scroll control's content handle: BigBagPanel registers Handle_Bag_Normal with
        /// RegisterScrollControl (BigBagPanel.lua L6742-6752) and lays the six category
        /// rows out with FormatAllItemPos (L1442-1455), clipping them to the 330x111
        /// viewport.</summary>
        public bool? Clip { get; set; }
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
        /// <summary>
        /// Section lookup for inventory-supplied names: exact match first, then a
        /// case-insensitive scan. Section identity is case-sensitive in the engine
        /// (shipped INIs carry case-differing twins), so the plan's ByName must not
        /// fold them together; the fallback keeps override entries authored with
        /// sloppy casing working.
        /// </summary>
        private static bool TryFind(IniFile file, string name, out IniSection section)
        {
            if (file.ByName.TryGetValue(name, out section)) return true;
            foreach (var candidate in file.Sections)
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    section = candidate;
                    return true;
                }
            }
            section = null;
            return false;
        }

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
                    // checkboxes carry LockShowAndHide=1 and own the tab label; the
                    // minimap's Wnd_Over button column is shown from the inventory in
                    // the same way the module toggles its buttons).
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
                if (!TryFind(filtered, text.Section, out var section)) continue;
                if (!string.IsNullOrWhiteSpace(value))
                    section.Values["$Text"] = value;
                if (text.FontScheme.HasValue)
                    section.Values["FontScheme"] = text.FontScheme.Value
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text.FontColor))
                    section.Values["FontColor"] = text.FontColor;
            }
        }

        /// <summary>Applies the Lua's runtime FromUITex calls (inventory `images`).</summary>
        public static void ApplyImages(IniFile filtered, IEnumerable<ImageOverride> images)
        {
            if (images == null) return;
            foreach (var image in images)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.Section)) continue;
                if (!TryFind(filtered, image.Section, out var section)) continue;
                // A WndMinimap paints its texture from `defaulttexture` (no live map in
                // the viewer), so an image override targets that key for lens sections.
                var textureKey = string.Equals(section.Get("._WndType"), "WndMinimap",
                    StringComparison.OrdinalIgnoreCase) ? "defaulttexture" : "Image";
                if (!string.IsNullOrWhiteSpace(image.Image)) section.Values[textureKey] = image.Image;
                if (image.Zoom.HasValue)
                    section.Values["$LensZoom"] = image.Zoom.Value
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (image.Frame.HasValue)
                    section.Values["Frame"] = image.Frame.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (image.Checked.HasValue)
                    section.Values["CheckedWhenCreate"] = image.Checked.Value ? "1" : "0";
                if (!string.IsNullOrWhiteSpace(image.BorderColor))
                    section.Values["$BorderColor"] = image.BorderColor;
                if (image.BorderWidth.HasValue)
                    section.Values["$BorderWidth"] = image.BorderWidth.Value
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                if (!TryFind(filtered, append.Container, out var container)) continue;

                var name = $"__append_{append.Container}_{index}";
                if (filtered.ByName.ContainsKey(name)) continue;
                var flow = append.Flow == true;
                var width = append.Width ?? (flow ? 0 : container.GetInt("Width"));
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
                section.Values["HAlign"] = (append.HAlign ?? (flow ? 0 : 1))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["VAlign"] = (append.VAlign ?? 1)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (flow) section.Values["PosType"] = "9";
                filtered.Sections.Add(section);
                filtered.ByName[name] = section;
            }
        }

        /// <summary>
        /// Appends a subtree of a second INI under a container of the filtered plan
        /// (the Lua's AppendItemFromIni). The item's `._Parent` is rewired to the
        /// container; descendants keep their chains; names already present are skipped
        /// (the engine clones, the viewer reuses the authored section names).
        /// </summary>
        public static void ApplyAppendIni(IniFile filtered, IEnumerable<AppendIniSpec> specs, Func<string, IniFile> load)
        {
            if (specs == null || load == null) return;
            foreach (var spec in specs)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.Container) ||
                    string.IsNullOrWhiteSpace(spec.Path) || string.IsNullOrWhiteSpace(spec.Item)) continue;
                if (!TryFind(filtered, spec.Container, out _)) continue;
                var source = load(spec.Path);
                if (source == null || !TryFind(source, spec.Item, out var item)) continue;

                var hidden = new HashSet<string>(
                    spec.Hide ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                bool Dropped(string name)
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var cursor = name;
                    while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                    {
                        if (hidden.Contains(cursor)) return true;
                        cursor = source.ByName.TryGetValue(cursor, out var s) ? s.Get("._Parent") : null;
                    }
                    return false;
                }

                foreach (var section in source.Sections)
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var cursor = section.Name;
                    bool inSubtree = false;
                    while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                    {
                        if (string.Equals(cursor, item.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            inSubtree = true;
                            break;
                        }
                        cursor = source.ByName.TryGetValue(cursor, out var s) ? s.Get("._Parent") : null;
                    }
                    if (!inSubtree || Dropped(section.Name)) continue;
                    if (filtered.ByName.ContainsKey(section.Name)) continue;
                    var clone = new IniSection { Name = section.Name };
                    foreach (var pair in section.Values) clone.Values[pair.Key] = pair.Value;
                    if (ReferenceEquals(section, item))
                        clone.Values["._Parent"] = spec.Container;
                    filtered.Sections.Add(clone);
                    filtered.ByName[clone.Name] = clone;
                }
            }
        }

        /// <summary>
        /// Applies the runtime state recorded by tools/ui/replay_all.py
        /// (Data/runtime_state/&lt;stem&gt;.tsv): the client's own script mutations replayed
        /// offline (docs/ui/UI_RUNTIME_REPLAY.md). Called before the inventory overrides
        /// so curated entries still win.
        /// </summary>
        public static int ApplyRuntimeState(IniFile filtered, string iniPath)
        {
            if (filtered == null || string.IsNullOrWhiteSpace(iniPath)) return 0;
            var stem = Path.GetFileNameWithoutExtension(iniPath);
            if (string.IsNullOrWhiteSpace(stem)) return 0;
            var statePath = FindRuntimeStateFile(stem);
            if (statePath == null) return 0;
            // Only apply replays that completed: a partial replay's mutations stop
            // mid-init (sections hidden before the engine shows them), which would
            // break GT-matched windows (the minimap lost its whole subtree).
            if (!ReplayCompleted(statePath)) return 0;
            return ApplyRuntimeMutations(filtered, File.ReadAllLines(statePath), iniPath);
        }

        /// <summary>
        /// Applies mutation lines (`section TAB method TAB args`, the replay TSV format) —
        /// shared by the on-disk runtime state and the interaction overlay (viewer clicks
        /// dispatched to tools/ui/replay_server.lua, docs/ui/UI_INTERACTION_REPLAY.md).
        /// </summary>
        public static int ApplyRuntimeMutations(IniFile filtered, IEnumerable<string> lines, string iniPath = null)
        {
            if (filtered == null || lines == null) return 0;
            var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootName = filtered.Sections.Count > 0 ? filtered.Sections[0].Name : null;
            var sourceCache = new Dictionary<string, IniFile>(StringComparer.OrdinalIgnoreCase);
            var pendingClear = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedByContainer = new Dictionary<string, List<IniSection>>(StringComparer.OrdinalIgnoreCase);
            var lastCloneByContainer = new Dictionary<string, IniSection>(StringComparer.OrdinalIgnoreCase);
            int applied = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length < 2 || parts[0] == "section") continue;
                if (!TryFind(filtered, parts[0], out var section)) continue;
                switch (parts[1])
                {
                    case "SetSize":
                        SetValue(section, "Width", parts, 2);
                        SetValue(section, "Height", parts, 3);
                        applied++;
                        break;
                    case "SetW":
                        SetValue(section, "Width", parts, 2);
                        applied++;
                        break;
                    case "SetH":
                        SetValue(section, "Height", parts, 2);
                        applied++;
                        break;
                    case "SetRelPos":
                    case "SetAbsPos":
                        SetValue(section, "Left", parts, 2);
                        SetValue(section, "Top", parts, 3);
                        applied++;
                        break;
                    case "SetRelX":
                        SetValue(section, "Left", parts, 2);
                        applied++;
                        break;
                    case "SetRelY":
                        SetValue(section, "Top", parts, 2);
                        applied++;
                        break;
                    case "SetFrame":
                        SetValue(section, "Frame", parts, 2);
                        applied++;
                        break;
                    case "SetText":
                        if (parts.Length > 2)
                        {
                            // Per-item text: the harness records the clone's Lookup+SetText
                            // on the container (it cannot model the clone), so a SetText
                            // right after an append lands on the newest clone's Text child.
                            var textSection = section;
                            if (lastCloneByContainer.TryGetValue(section.Name, out var lastClone))
                            {
                                var textChild = FindTextChild(filtered, lastClone.Name);
                                if (textChild != null) textSection = textChild;
                            }
                            textSection.Values["$Text"] = string.Join("\t", parts.Skip(2));
                            applied++;
                        }
                        break;
                    case "SetFontScheme":
                        SetValue(section, "FontScheme", parts, 2);
                        applied++;
                        break;
                    case "SetAlpha":
                        SetValue(section, "Alpha", parts, 2);
                        applied++;
                        break;
                    case "Show":
                        hidden.Remove(section.Name);
                        applied++;
                        break;
                    case "Hide":
                        if (!string.Equals(section.Name, rootName, StringComparison.OrdinalIgnoreCase))
                            hidden.Add(section.Name);
                        applied++;
                        break;
                    case "SetVisible":
                        if (parts.Length > 2 && parts[2].Equals("false", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.Equals(section.Name, rootName, StringComparison.OrdinalIgnoreCase))
                                hidden.Add(section.Name);
                        }
                        else
                            hidden.Remove(section.Name);
                        applied++;
                        break;
                    // ---- runtime item population (the engine's list building) ----
                    case "Clear":
                        // The engine empties the container's item list (the authored
                        // prototype is an item too). Defer it: remove the old items only
                        // when the script actually appends replacements, so a replay whose
                        // data-driven append loop under-recorded cannot blank a list.
                        pendingClear.Add(section.Name);
                        applied++;
                        break;
                    case "AppendItemFromIni":
                    case "AppendItemFromData":
                    case "AppendContentFromIni":
                    {
                        // AppendItemFromIni(container, iniPath, item [, flag]) and
                        // AppendContentFromIni(container, iniPath, item [, newName]) clone
                        // the named INI subtree under the container (the engine's item).
                        var source = FindAppendSource(filtered, iniPath, parts, sourceCache);
                        if (source != null)
                        {
                            if (pendingClear.Remove(section.Name) &&
                                addedByContainer.TryGetValue(section.Name, out var previous))
                            {
                                foreach (var clone in previous) RemoveDescendants(filtered, clone.Name);
                                previous.Clear();
                            }
                            var desired = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4] : null;
                            var appended = AppendClone(filtered, source, section.Name, desired);
                            lastCloneByContainer[section.Name] = appended;
                            if (!addedByContainer.TryGetValue(section.Name, out var list))
                            {
                                list = new List<IniSection>();
                                addedByContainer[section.Name] = list;
                            }
                            list.Add(appended);
                            applied++;
                        }
                        break;
                    }
                    case "AppendItemFromString":
                    {
                        // Text-only item: a minimal Text section carrying the string.
                        if (parts.Length > 2)
                        {
                            var name = UniqueName(filtered, section.Name + "~Text");
                            var item = new IniSection { Name = name };
                            item.Values["._WndType"] = "Text";
                            item.Values["._Parent"] = section.Name;
                            item.Values["$Text"] = parts[2];
                            AddSection(filtered, item);
                            applied++;
                        }
                        break;
                    }
                    // ---- the engine's arrangement passes ----
                    case "FormatAllItemPos":
                    case "FormatAllContentPos":
                        section.Values["$FormatItems"] = "1";
                        applied++;
                        break;
                    case "SetSizeByAllItemSize":
                        section.Values["$SizeByItems"] = "1";
                        applied++;
                        break;
                    case "SetPoint":
                        // SetPoint(srcSide, sx, sy, dstSide, dx, dy) -> the viewer's
                        // AnchorArgs "dstSide,srcSide,dx,dy" (ApplyAnchors format).
                        if (parts.Length > 7)
                        {
                            section.Values["AnchorArgs"] = parts[5] + "," + parts[2] + "," + parts[6] + "," + parts[7];
                            applied++;
                        }
                        break;
                    case "SetOverTextPosition":
                        SetValue(section, "Left", parts, 2);
                        SetValue(section, "Top", parts, 3);
                        applied++;
                        break;
                    case "SetOverTextFontScheme":
                        SetValue(section, "FontScheme", parts, 2);
                        applied++;
                        break;
                    // ---- runtime render source / mode ----
                    case "FromUITex":
                        if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                            section.Values["Image"] = parts[2];
                        SetValue(section, "Frame", parts, 3);
                        applied++;
                        break;
                    case "SetImageType":
                        SetValue(section, "ImageType", parts, 2);
                        applied++;
                        break;
                    case "SetPercentage":
                        SetValue(section, "$Percentage", parts, 2);
                        applied++;
                        break;
                    case "SetFontColor":
                        if (parts.Length > 2) section.Values["$FontColor"] = parts[2];
                        applied++;
                        break;
                    // ---- control state ----
                    case "Enable":
                        if (parts.Length > 2)
                            section.Values["$Disabled"] = parts[2].Equals("false", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
                        applied++;
                        break;
                    case "Check":
                        if (parts.Length > 2)
                            section.Values["$Checked"] = parts[2].Equals("false", StringComparison.OrdinalIgnoreCase) ? "0" : "1";
                        applied++;
                        break;
                    case "CorrectPos":
                        section.Values["$CorrectPos"] = "1";
                        applied++;
                        break;
                    case "SetScrollPos":
                        if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                            section.Values["$ScrollPos"] = parts[2];
                        applied++;
                        break;
                    case "SetStepCount":
                        SetValue(section, "StepCount", parts, 2);
                        applied++;
                        break;
                    case "EnableScroll":
                        section.Values["$ScrollEnabled"] = "1";
                        applied++;
                        break;
                    case "SetHAlign":
                        SetValue(section, "HAlign", parts, 2);
                        applied++;
                        break;
                    case "Scale":
                    case "SetScale":
                        SetValue(section, "Scale", parts, 2);
                        applied++;
                        break;
                    case "SetOverText":
                        if (parts.Length > 2) section.Values["$OverText"] = parts[2];
                        applied++;
                        break;
                    case "SetMapPath":
                        if (parts.Length > 2) section.Values["MapPath"] = parts[2];
                        applied++;
                        break;
                    case "SetItemStartRelPos":
                        if (parts.Length > 2) section.Values["ItemStartRelPos"] = parts[2];
                        applied++;
                        break;
                    case "CreateItemData":
                    case "SetObject":
                    case "SetObjectIcon":
                    case "SetObjectSelected":
                        // Item-data bookkeeping: the visual rows come from the INI
                        // prototypes, so these are recorded as applied without a
                        // layout effect (an empty bag cell renders empty in game too).
                        applied++;
                        break;
                    case "RemoveItem":
                        RemoveLastChild(filtered, section.Name);
                        applied++;
                        break;
                    case "Expand":
                        section.Values["$Expanded"] = "1";
                        applied++;
                        break;
                    case "ActivePage":
                        if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                            filtered.Sections[0].Values["page"] = parts[2];
                        applied++;
                        break;
                }
            }

            if (hidden.Count > 0)
            {
                bool Dropped(string name)
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var cursor = name;
                    while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                    {
                        if (hidden.Contains(cursor)) return true;
                        cursor = filtered.ByName.TryGetValue(cursor, out var s) ? s.Get("._Parent") : null;
                    }
                    return false;
                }
                // Never let the replay empty a container. Some init functions first hide
                // every state variant / page part and then show the active one from live
                // data; when that data is stubbed the Show never runs and the authored
                // layout would be wiped (LuckyMeeting hid both page variants, 145 -> 3
                // sections). A parent whose visible children would all be hidden keeps
                // them instead - the authored state is the client's own default.
                for (int pass = 0; pass < 4; pass++)
                {
                    var parentsWithVisible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var section in filtered.Sections)
                    {
                        if (Dropped(section.Name)) continue;
                        parentsWithVisible.Add(section.Get("._Parent") ?? "");
                    }
                    var restore = new List<string>();
                    foreach (var name in hidden)
                    {
                        if (!filtered.ByName.TryGetValue(name, out var section)) continue;
                        var parent = section.Get("._Parent") ?? "";
                        if (string.IsNullOrEmpty(parent) || Dropped(parent)) continue;
                        if (!parentsWithVisible.Contains(parent)) restore.Add(name);
                    }
                    if (restore.Count == 0) break;
                    foreach (var name in restore) hidden.Remove(name);
                }
                var keep = filtered.Sections.Where(s => !Dropped(s.Name)).ToList();
                // A replay whose hides collapse the window (less than half the authored
                // sections visible) is a stub-session reset whose mode-driven Show never
                // ran (LuckyMeeting hid both page variants, 145 -> 3 sections). Keep the
                // authored layout in that case; the authored INI is the client's default.
                if (keep.Count * 2 < filtered.Sections.Count)
                    keep = filtered.Sections.ToList();
                if (keep.Count != filtered.Sections.Count)
                {
                    filtered.Sections.Clear();
                    filtered.ByName.Clear();
                    foreach (var section in keep)
                    {
                        filtered.Sections.Add(section);
                        filtered.ByName[section.Name] = section;
                    }
                }
            }
            return applied;
        }

        private static void SetValue(IniSection section, string key, string[] parts, int index)
        {
            if (parts.Length <= index || string.IsNullOrWhiteSpace(parts[index])) return;
            if (double.TryParse(parts[index], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                section.Values[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AddSection(IniFile file, IniSection section)
        {
            file.Sections.Add(section);
            file.ByName[section.Name] = section;
        }

        private static string UniqueName(IniFile file, string desired)
        {
            if (!file.ByName.ContainsKey(desired)) return desired;
            for (int i = 1; i < 1000; i++)
            {
                var candidate = desired + "~" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!file.ByName.ContainsKey(candidate)) return candidate;
            }
            return desired + "~" + Guid.NewGuid().ToString("N");
        }

        private static IniSection CloneSection(IniSection source, string name)
        {
            var clone = new IniSection { Name = name };
            foreach (var pair in source.Values) clone.Values[pair.Key] = pair.Value;
            return clone;
        }

        /// <summary>
        /// Removes every descendant of a container (the engine's Clear empties the
        /// container's item list; the authored prototype is an item too).
        /// </summary>
        private static void RemoveDescendants(IniFile file, string rootName)        {
            var doomed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            stack.Push(rootName);
            while (stack.Count > 0)
            {
                var name = stack.Pop();
                foreach (var candidate in file.Sections.ToList())
                {
                    if (!string.Equals(candidate.Get("._Parent"), name, StringComparison.OrdinalIgnoreCase)) continue;
                    if (doomed.Add(candidate.Name)) stack.Push(candidate.Name);
                }
            }
            if (doomed.Count == 0) return;
            file.Sections.RemoveAll(s => doomed.Contains(s.Name));
            foreach (var name in doomed) file.ByName.Remove(name);
        }

        /// <summary>First Text descendant of a clone (the item's label element).</summary>
        private static IniSection FindTextChild(IniFile file, string rootName)
        {
            var queue = new Queue<string>();
            queue.Enqueue(rootName);
            IniSection fallback = null;
            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                foreach (var candidate in file.Sections)
                {
                    if (!string.Equals(candidate.Get("._Parent"), name, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(candidate.Get("._WndType"), "Text", StringComparison.OrdinalIgnoreCase))
                    {
                        if (candidate.Name.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0) return candidate;
                        if (fallback == null) fallback = candidate;
                    }
                    queue.Enqueue(candidate.Name);
                }
            }
            return fallback;
        }

        /// <summary>Removes a container's last item (the engine's RemoveItem).</summary>
        private static void RemoveLastChild(IniFile file, string container)
        {
            IniSection last = null;
            foreach (var candidate in file.Sections)
            {
                if (string.Equals(candidate.Get("._Parent"), container, StringComparison.OrdinalIgnoreCase))
                    last = candidate;
            }
            if (last == null) return;
            file.Sections.Remove(last);
            file.ByName.Remove(last.Name);
        }

        /// <summary>
        /// Materializes a runtime item: clones the source subtree under the container
        /// (AppendItemFromIni / AppendContentFromIni). Descendants get unique prefixed
        /// names so multiple clones of the same prototype can coexist.
        /// </summary>
        private static IniSection AppendClone(IniFile file, IniSection source, string container, string desiredName)
        {
            var rootName = UniqueName(file, string.IsNullOrWhiteSpace(desiredName) ? source.Name : desiredName);
            var root = CloneSection(source, rootName);
            root.Values["._Parent"] = container;
            root.Values["$RuntimeItem"] = "1";
            AddSection(file, root);
            var stack = new Stack<(IniSection Src, string Parent)>();
            stack.Push((source, rootName));
            while (stack.Count > 0)
            {
                var (src, parent) = stack.Pop();
                foreach (var child in file.Sections.ToList())
                {
                    if (ReferenceEquals(child, source)) continue;
                    if (!string.Equals(child.Get("._Parent"), src.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    var childName = UniqueName(file, parent + "~" + child.Name);
                    var clone = CloneSection(child, childName);
                    clone.Values["._Parent"] = parent;
                    AddSection(file, clone);
                    stack.Push((child, childName));
                }
            }
            return root;
        }

        /// <summary>
        /// Resolves an append call's source section: same file first, then the named
        /// sibling INI under the same assets root (AppendItemFromIni's iniPath arg).
        /// </summary>
        private static IniSection FindAppendSource(IniFile filtered, string iniPath, string[] parts,
            Dictionary<string, IniFile> cache)
        {
            if (parts.Length < 4 || string.IsNullOrWhiteSpace(parts[3])) return null;
            var item = parts[3];
            if (TryFind(filtered, item, out var source)) return source;
            var path = parts[2];
            if (string.IsNullOrWhiteSpace(path)) return null;
            var key = path.Replace('\\', '/');
            if (!cache.TryGetValue(key, out var file))
            {
                file = null;
                try
                {
                    var root = FindAssetsRoot(iniPath);
                    if (root != null)
                    {
                        var full = Path.Combine(root, key.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(full)) file = IniFile.Load(full);
                    }
                }
                catch
                {
                    file = null;
                }
                cache[key] = file;
            }
            if (file != null && file.ByName.TryGetValue(item, out source)) return source;
            return null;
        }

        private static string FindAssetsRoot(string iniPath)
        {
            if (string.IsNullOrWhiteSpace(iniPath)) return null;
            var dir = Path.GetDirectoryName(Path.GetFullPath(iniPath));
            while (!string.IsNullOrWhiteSpace(dir))
            {
                if (string.Equals(Path.GetFileName(dir), "assets", StringComparison.OrdinalIgnoreCase)) return dir;
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
            return null;
        }

        private static string FindRuntimeStateFile(string stem)
        {
            var roots = new[]
            {
                Path.Combine(Paths.AppRoot ?? "", "Data", "runtime_state"),
                Path.Combine(AppContext.BaseDirectory ?? "", "Data", "runtime_state"),
            };
            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                var exact = Path.Combine(root, stem + ".tsv");
                if (File.Exists(exact)) return exact;
                foreach (var file in Directory.GetFiles(root, "*.tsv"))
                    if (string.Equals(Path.GetFileNameWithoutExtension(file), stem, StringComparison.OrdinalIgnoreCase))
                        return file;
            }
            return null;
        }

        /// <summary>True when replay_summary.tsv marks this stem OK (full replay).</summary>
        private static bool ReplayCompleted(string statePath)
        {
            try
            {
                var summary = Path.Combine(Path.GetDirectoryName(statePath), "replay_summary.tsv");
                if (!File.Exists(summary)) return true; // no summary: trust the state file
                var stem = Path.GetFileNameWithoutExtension(statePath);
                foreach (var line in File.ReadAllLines(summary))
                {
                    var parts = line.Split('\t');
                    if (parts.Length < 2) continue;
                    if (string.Equals(parts[0], stem, StringComparison.OrdinalIgnoreCase))
                        return parts[1].StartsWith("OK", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }
            return true;
        }

        /// <summary>Applies the Lua's runtime SetSize/SetRelPos calls (inventory `adjust`).</summary>
        public static void ApplyAdjustments(IniFile filtered, IEnumerable<AdjustSpec> adjustments)
        {
            if (adjustments == null) return;
            foreach (var adjust in adjustments)
            {
                if (adjust == null || string.IsNullOrWhiteSpace(adjust.Section)) continue;
                if (!TryFind(filtered, adjust.Section, out var section)) continue;
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
                if (adjust.HAlign.HasValue)
                    section.Values["HAlign"] = adjust.HAlign.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (adjust.Alpha.HasValue)
                    section.Values["Alpha"] = adjust.Alpha.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (adjust.PosType.HasValue)
                    section.Values["PosType"] = adjust.PosType.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (adjust.Clip == true)
                    section.Values["$Clip"] = "1";
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
                if (!TryFind(filtered, anchor.Section, out var section)) continue;
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
                if (!TryFind(filtered, wanted[i], out var section)) continue;
                section.Values["Left"] = (strip.X0 + i * strip.Step).ToString(System.Globalization.CultureInfo.InvariantCulture);
                section.Values["TabFixed"] = "1";
            }

            if (selectedTab != null && wantedSet.Contains(selectedTab) &&
                TryFind(filtered, selectedTab, out var selectedSection) &&
                selectedSection.GetInt("CheckedWhenCreate") == 0)
            {
                foreach (var name in wanted)
                    if (TryFind(filtered, name, out var tab))
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
                if (!filtered.ByName.ContainsKey(template.Container) &&
                    !filtered.Sections.Any(s => string.Equals(s.Name, template.Container, StringComparison.OrdinalIgnoreCase))) continue;

                IniFile source;
                try { source = load(template.Ini); }
                catch { continue; }
                if (source == null || !TryFind(source, template.Item, out var prototype)) continue;

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
                    bool engineItemFlow = prototype.GetInt("PosType") == 10 &&
                        filtered.ByName.TryGetValue(template.Container, out var containerSection) &&
                        containerSection.GetInt("FirstItemPosType") != 0;
                    if (string.Equals(template.Flow, "row", StringComparison.OrdinalIgnoreCase))
                    {
                        // Horizontal flow (the message line's items): PosType 9 places
                        // each clone right after the previous sibling's measured width.
                        root.Values["PosType"] = "9";
                        root.Values["Left"] = "0";
                        root.Values["Top"] = "0";
                    }
                    else if (engineItemFlow)
                    {
                        // The prototype is an engine item (PosType 10 in a handle with
                        // FirstItemPosType != 0, e.g. LootList's rows): the view stacks
                        // each clone below the previous item, mixing authored rows
                        // (LootList's money row) and clones in order.
                    }
                    else
                    {
                        root.Values["PosType"] = "0";
                        root.Values["Left"] = "0";
                        root.Values["Top"] = (rowHeight * row).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

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
