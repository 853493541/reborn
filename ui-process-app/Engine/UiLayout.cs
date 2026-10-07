using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MapUiApp.Engine
{
    public sealed class UiBuildResult
    {
        public FrameworkElement Root;
        public readonly Dictionary<string, FrameworkElement> Elements = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        public readonly Dictionary<string, IniSection> Sections = new Dictionary<string, IniSection>(StringComparer.Ordinal);

        /// <summary>
        /// Element per section object. A few shipped INIs carry case-differing twins
        /// (e.g. EndOfBattle's header `Text_JiFen_1` vs row `Text_Jifen_1`), which the
        /// name-keyed maps collapse; the layout walk must stay per-section.
        /// </summary>
        public readonly Dictionary<IniSection, FrameworkElement> ElementsByRef = new Dictionary<IniSection, FrameworkElement>();

        /// <summary>Sections whose atlas/frame did not resolve (drawn as placeholders).</summary>
        public readonly List<string> Placeholders = new List<string>();

        /// <summary>Text sections whose string id is missing from the loaded tables.</summary>
        public readonly List<string> UnresolvedStrings = new List<string>();
    }

    /// <summary>
    /// Builds a WPF visual tree from a KGUI layout INI (the real game layout files)
    /// using the extracted .UITex atlases for every Image/Button frame.
    /// </summary>
    public static class UiLayout
    {
        /// <summary>Draws section-name labels and tints placeholders so layouts are
        /// readable even when the referenced textures were not extracted.</summary>
        public static bool Wireframe = false;

        private static readonly HashSet<string> ContainerTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WndFrame", "WndWindow", "Handle", "Box", "Null", "WndContainer",
            "WndFlexContainer", "WndScroll", "WndNewScrollBar", "WndEdit", "WndMinimap",
        };

        private static readonly HashSet<string> SkipTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SFX", "Animate", "Shadow",
        };

        private static readonly HashSet<string> SkipSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Image_Hover_Remote", "Image_Hover_Skill", "Animate_New",
        };

        // Runtime state overlays the game only shows on specific events.
        private static readonly HashSet<string> HiddenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Handle_Black", "Image_OnAttack", "Image_Die",
        };

        public static UiBuildResult Build(IniFile ini, AssetResolver assets, UiTexCache textures)
        {
            var result = new UiBuildResult();
            if (ini.Sections.Count == 0) throw new InvalidOperationException("Layout INI has no sections");
            ApplyButtonLabelFonts(ini);
            var rootSection = ini.Sections[0];

            // Every section referenced as a parent must exist as a container even when
            // its own type is unknown to us (WndPage/WndList/...), or its whole subtree
            // would be orphaned.
            var parentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in ini.Sections)
            {
                var parent = section.Get("._Parent");
                if (!string.IsNullOrWhiteSpace(parent)) parentNames.Add(parent);
            }

            foreach (var section in ini.Sections)
            {
                if (section != rootSection && SkipSections.Contains(section.Name)) continue;
                var element = CreateElement(section, textures, parentNames.Contains(section.Name), result);
                if (element == null) continue;
                result.Elements[section.Name] = element;
                result.Sections[section.Name] = section;
                result.ElementsByRef[section] = element;
            }

            result.Root = result.Elements[rootSection.Name];
            var buttonSlots = new HashSet<string>(StringComparer.Ordinal);
            var rootWidth = (double)rootSection.GetInt("Width");
            var rootHeight = (double)rootSection.GetInt("Height");

            // KGUI containers often omit Width/Height. Handles auto-size to their
            // content, other containers inherit the nearest sized ancestor.
            var intrinsic = new Dictionary<string, (double W, double H)>(StringComparer.Ordinal);
            (double W, double H) Intrinsic(IniSection section, int depth)
            {
                if (intrinsic.TryGetValue(section.Name, out var cached)) return cached;
                double w = section.GetInt("Width");
                double h = section.GetInt("Height");
                if ((w <= 0 || h <= 0) && depth < 12)
                {
                    double maxX = 0, maxY = 0;
                    bool any = false;
                    foreach (var child in ini.Sections)
                    {
                        if (!string.Equals(child.Get("._Parent"), section.Name, StringComparison.OrdinalIgnoreCase)) continue;
                        any = true;
                        var childSize = Intrinsic(child, depth + 1);
                        if (childSize.W <= 0 || childSize.H <= 0)
                        {
                            // Leaves without authored sizes still occupy their content
                            // size (text extent / frame pixels).
                            var measured = MeasuredSize(child);
                            if (childSize.W <= 0) childSize.W = measured.W;
                            if (childSize.H <= 0) childSize.H = measured.H;
                        }
                        double childLeft = child.GetInt("Left");
                        double childTop = child.GetInt("Top");
                        maxX = Math.Max(maxX, childLeft + childSize.W);
                        maxY = Math.Max(maxY, childTop + childSize.H);
                    }
                    if (any)
                    {
                        if (w <= 0) w = maxX;
                        if (h <= 0) h = maxY;
                    }
                }
                var size = (w, h);
                intrinsic[section.Name] = size;
                return size;
            }

            var sizeCache = new Dictionary<string, (double W, double H)>(StringComparer.Ordinal);
            // Content size when the INI authors none: the frame's pixel size for images,
            // the measured text for labels (the engine lays items out by their content).
            (double W, double H) MeasuredSize(IniSection section)
            {
                if (!result.Elements.TryGetValue(section.Name, out var element)) return (0, 0);
                if (!double.IsNaN(element.Width) && !double.IsNaN(element.Height))
                    return (element.Width, element.Height);
                if (element is TextBlock block)
                {
                    block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    return (block.DesiredSize.Width, block.DesiredSize.Height);
                }
                return (0, 0);
            }
            (double W, double H) SizeOf(IniSection section)
            {
                if (sizeCache.TryGetValue(section.Name, out var cached)) return cached;
                double w = section.GetInt("Width");
                double h = section.GetInt("Height");
                var own = Intrinsic(section, 0);
                if (w <= 0) w = own.W;
                if (h <= 0) h = own.H;
                if (w <= 0 || h <= 0)
                {
                    var measured = MeasuredSize(section);
                    if (w <= 0) w = measured.W;
                    if (h <= 0) h = measured.H;
                }
                var parentName = section.Get("._Parent");
                if ((w <= 0 || h <= 0) && !string.IsNullOrWhiteSpace(parentName) &&
                    ini.ByName.TryGetValue(parentName, out var parentSection) && parentSection != section)
                {
                    var parentSize = SizeOf(parentSection);
                    if (w <= 0) w = parentSize.W;
                    if (h <= 0) h = parentSize.H;
                }
                if (w <= 0) w = rootWidth;
                if (h <= 0) h = rootHeight;
                var size = (w, h);
                sizeCache[section.Name] = size;
                return size;
            }
            foreach (var section in ini.Sections) SizeOf(section);

            // WndPageSet arranges its WndCheckBox children as a tab strip; the INI
            // authors every tab at the same origin, the control flows them. Tabs the
            // Lua pinned explicitly (ShowModeTabs -> SetRelX) keep their Left.
            var tabX = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var pageSet in ini.Sections)
            {
                if (!string.Equals(pageSet.Get("._WndType"), "WndPageSet", StringComparison.OrdinalIgnoreCase)) continue;
                double x = -1;
                foreach (var child in ini.Sections)
                {
                    if (!string.Equals(child.Get("._Parent"), pageSet.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(child.Get("._WndType"), "WndCheckBox", StringComparison.OrdinalIgnoreCase)) continue;
                    if (child.Get("TabFixed") != null) continue;
                    if (x < 0) x = child.GetInt("Left");
                    tabX[child.Name] = x;
                    x += SizeOf(child).W;
                }
            }

            // HandleType 1/2/3/5/6 lay their items out themselves (engine dispatcher
            // 0x1801064f5: types 0-3/6 share the row/list path; the post pass
            // 0x180106cc0 routes 1/2/3/6 to the same list path; type 5 is the distinct
            // 0x180107410 path but its INI properties - RowSpacing, PixelScroll,
            // vertical ShapTexture masks - are the same vertical scroll list, so it
            // flows with them). Items flow left-to-right, wrap when the next one would
            // exceed the container width, then each row is aligned by HAlign/VAlign.
            // The authored Left/PosType of an item is ignored. Type 4 is the
            // AppendString row (handled in LayoutPlanBuilder.Build).
            // See proof/ui/evidence/re/kgui_handle_layout.txt.
            var listPos = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            foreach (var list in ini.Sections)
            {
                var handleType = list.GetInt("HandleType");
                // The runtime marks containers the script arranges (FormatAllItemPos/
                // FormatAllContentPos) so the engine's flow pass applies to them too.
                bool runtimeFlow = list.Get("$FormatItems") == "1";
                if (handleType != 1 && handleType != 2 && handleType != 3 && handleType != 5 && handleType != 6 && !runtimeFlow) continue;
                // Wrap at the authored control width (the engine's list box); fall back
                // to the measured size only when the INI leaves it open.
                double listW = list.GetInt("Width");
                if (listW <= 0) listW = SizeOf(list).W;
                if (listW <= 0)
                {
                    // A runtime-arranged AutoSize container grows to its items: lay
                    // them in one row (the engine's FormatAllContentPos then sizes the
                    // container from the items' extent).
                    if (!runtimeFlow) continue;
                    listW = double.MaxValue;
                }
                // Invisible alternatives (Alpha=0 state variants) do not take list space.
                var candidates = ini.Sections.Where(s => !ReferenceEquals(s, list) &&
                    string.Equals(s.Get("._Parent"), list.Name, StringComparison.OrdinalIgnoreCase) &&
                    s.GetInt("Alpha", 255) > 0 &&
                    !(s.Get("Visible") != null && s.GetBool("Visible") == false)).ToList();
                // A runtime-arranged container flows its appended items, not the authored
                // background/decoration children (the engine's content list).
                if (runtimeFlow)
                {
                    var runtimeItems = candidates.Where(s => s.Get("$RuntimeItem") == "1").ToList();
                    if (runtimeItems.Count > 0) candidates = runtimeItems;
                }
                var items = candidates;
                if (items.Count == 0) continue;

                double rowSpacing = list.GetInt("RowSpacing");
                int hAlign = list.GetInt("HAlign");
                int vAlign = list.GetInt("VAlign");
                // An AutoSize list handle grows to its items instead of wrapping them
                // (RemainingTimeNotify's Handle_Count is 61px wide but holds two 32px
                // digit items; without this they wrapped into two rows).
                bool autoSizeList = list.GetBool("AutoSize");

                // FirstItemPosType (engine jump table 0x180108964, cases 0x1801086a3..
                // 0x18010874d) aligns the first item inside the container and sets where
                // the flow starts: 0 = the item's own authored offset, 1/10 bottom,
                // 3 v-center, 5/7 right, 6 h-center, 8/11 bottom-right, 9 right-center,
                // 12 bottom-center, 2/4 origin.
                double originX = 0, originY = 0;
                var firstItem = items[0];
                var firstSize = SizeOf(firstItem);
                double listH = list.GetInt("Height");
                if (listH <= 0) listH = SizeOf(list).H;
                bool listWValid = listW > 0 && listW < 1e9;
                switch (list.GetInt("FirstItemPosType", -1))
                {
                    case 0:
                        originX = firstItem.GetInt("Left");
                        originY = firstItem.GetInt("Top");
                        break;
                    case 1:
                    case 10:
                        originY = listH - firstSize.H;
                        break;
                    case 3:
                        originY = (listH - firstSize.H) / 2;
                        break;
                    case 5:
                    case 7:
                        if (listWValid) originX = listW - firstSize.W;
                        break;
                    case 6:
                        if (listWValid) originX = (listW - firstSize.W) / 2;
                        break;
                    case 8:
                    case 11:
                        if (listWValid) originX = listW - firstSize.W;
                        originY = listH - firstSize.H;
                        break;
                    case 9:
                        if (listWValid) originX = listW - firstSize.W;
                        originY = (listH - firstSize.H) / 2;
                        break;
                    case 12:
                        if (listWValid) originX = (listW - firstSize.W) / 2;
                        originY = listH - firstSize.H;
                        break;
                }

                var rows = new List<List<IniSection>>();
                var rowWidths = new List<double>();
                var rowHeights = new List<double>();
                var currentRow = new List<IniSection>();
                double cursor = 0, rowHeight = 0;
                foreach (var item in items)
                {
                    var size = SizeOf(item);
                    if (!autoSizeList && currentRow.Count > 0 && cursor + size.W > listW + 0.5)
                    {
                        rows.Add(currentRow); rowWidths.Add(cursor); rowHeights.Add(rowHeight);
                        currentRow = new List<IniSection>(); cursor = 0; rowHeight = 0;
                    }
                    currentRow.Add(item);
                    cursor += size.W;
                    rowHeight = Math.Max(rowHeight, size.H);
                }
                rows.Add(currentRow); rowWidths.Add(cursor); rowHeights.Add(rowHeight);

                double y = originY;
                for (int r = 0; r < rows.Count; r++)
                {
                    double start = (r == 0 ? originX : 0) + (hAlign switch
                    {
                        1 => (listWValid ? (listW - rowWidths[r]) / 2 : 0),
                        2 => listWValid ? listW - rowWidths[r] : 0,
                        _ => 0,
                    });
                    double x = start;
                    foreach (var item in rows[r])
                    {
                        var size = SizeOf(item);
                        double itemY = y + (vAlign switch
                        {
                            1 => (rowHeights[r] - size.H) / 2,
                            2 => rowHeights[r] - size.H,
                            _ => 0,
                        });
                        listPos[item.Name] = (x, itemY);
                        x += size.W;
                    }
                    y += rowHeights[r] + rowSpacing;
                }
                // SetSizeByAllItemSize: the engine sizes the container to the items'
                // extent after arranging them.
                if (list.Get("$SizeByItems") == "1" && rows.Count > 0)
                {
                    double maxRowWidth = 0;
                    foreach (var rw in rowWidths) maxRowWidth = Math.Max(maxRowWidth, rw);
                    double totalHeight = 0;
                    foreach (var rh in rowHeights) totalHeight += rh;
                    totalHeight += rowSpacing * (rows.Count - 1);
                    // Never grow past the authored box (the engine sizes the container to
                    // the items but the authored rect is the layout slot).
                    double authoredW = list.GetDouble("Width");
                    double authoredH = list.GetDouble("Height");
                    double newW = authoredW > 0 ? Math.Min(authoredW, maxRowWidth) : maxRowWidth;
                    double newH = authoredH > 0 ? Math.Max(authoredH, totalHeight) : totalHeight;
                    list.Values["Width"] = newW.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    list.Values["Height"] = newH.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            // Flex containers (WndFlexContainer/FlexHandle) are Yoga nodes: the engine lays
            // their children out by flexbox (KGUIX64 exports the Yoga API; the INI keys map
            // 1:1 to YGNodeStyleSet*: Direction/FlexDirection/Wrap/JustifyContent/AlignItems/
            // AlignContent/FlexMargin*/FlexPadding*/FlexGrow/FlexShrink). The authored child
            // Left/Top are parked prototypes (ACC_Excellent's buttons sit at Top=632).
            double Distribute(int mode, double free, int count, out double gap)
            {
                gap = 0;
                switch (mode)
                {
                    case 1: return free / 2;
                    case 2: return free;
                    case 3: gap = count > 1 ? free / (count - 1) : 0; return 0;
                    case 4: gap = count > 0 ? free / count : 0; return gap / 2;
                    case 5: gap = count > 0 ? free / (count + 1) : 0; return gap;
                    default: return 0;
                }
            }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var flex in ini.Sections)
            {
                var flexType = flex.Get("._WndType");
                if (!string.Equals(flexType, "WndFlexContainer", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(flexType, "FlexHandle", StringComparison.OrdinalIgnoreCase) &&
                    // WndList authors the same flex properties (JustifyContent/FlexDirection/
                    // AlignContent/AlignItems/AutoLength/ContainerType) - a scrollable flex list.
                    !string.Equals(flexType, "WndList", StringComparison.OrdinalIgnoreCase)) continue;
                var kids = ini.Sections.Where(s => !ReferenceEquals(s, flex) &&
                    string.Equals(s.Get("._Parent"), flex.Name, StringComparison.OrdinalIgnoreCase) &&
                    s.GetInt("Alpha", 255) > 0).ToList();
                if (kids.Count == 0) continue;

                double padL = flex.GetDouble("FlexPaddingLeft");
                double padT = flex.GetDouble("FlexPaddingTop");
                double padR = flex.GetDouble("FlexPaddingRight");
                double padB = flex.GetDouble("FlexPaddingBottom");
                double innerW = Math.Max(0, flex.GetDouble("Width") - padL - padR);
                double innerH = Math.Max(0, flex.GetDouble("Height") - padT - padB);
                int flexDir = flex.GetInt("FlexDirection");   // Yoga: 0 column, 1 column-reverse, 2 row, 3 row-reverse
                int wrap = flex.GetInt("Wrap");               // 0 nowrap, 1 wrap, 2 wrap-reverse
                int justify = flex.GetInt("JustifyContent");  // 0 start, 1 center, 2 end, 3 between, 4 around, 5 evenly
                int align = flex.GetInt("AlignItems");        // 0 auto(=stretch), 1 start, 2 center, 3 end, 4 stretch
                int alignContent = flex.GetInt("AlignContent");
                bool row = flexDir == 2 || flexDir == 3;
                bool reverse = flexDir == 1 || flexDir == 3;
                var ordered = reverse ? Enumerable.Reverse(kids).ToList() : kids;

                double MainMargin(IniSection s) => row
                    ? s.GetDouble("FlexMarginLeft") + s.GetDouble("FlexMarginRight")
                    : s.GetDouble("FlexMarginTop") + s.GetDouble("FlexMarginBottom");
                double CrossMargin(IniSection s) => row
                    ? s.GetDouble("FlexMarginTop") + s.GetDouble("FlexMarginBottom")
                    : s.GetDouble("FlexMarginLeft") + s.GetDouble("FlexMarginRight");
                double MainSize(IniSection s) { var z = SizeOf(s); return row ? z.W : z.H; }
                double CrossSize(IniSection s) { var z = SizeOf(s); return row ? z.H : z.W; }
                double Limit() => row ? innerW : innerH;

                // line breaking along the main axis
                var lines = new List<List<IniSection>>();
                var current = new List<IniSection>();
                double used = 0;
                foreach (var item in ordered)
                {
                    double itemMain = MainSize(item) + MainMargin(item);
                    if (wrap != 0 && current.Count > 0 && Limit() > 0 && used + itemMain > Limit() + 0.5)
                    {
                        lines.Add(current); current = new List<IniSection>(); used = 0;
                    }
                    current.Add(item); used += itemMain;
                }
                lines.Add(current);
                if (wrap == 2) lines.Reverse();

                // grow/shrink distribute each line's free main space
                var lineCross = new List<double>();
                foreach (var line in lines)
                {
                    double free = Limit() - line.Sum(it => MainSize(it) + MainMargin(it));
                    double growSum = line.Sum(it => Math.Max(0, it.GetDouble("FlexGrow")));
                    double shrinkSum = line.Sum(it => Math.Max(0, it.GetDouble("FlexShrink")));
                    if (free > 0.5 && growSum > 0)
                    {
                        foreach (var it in line)
                        {
                            double g = Math.Max(0, it.GetDouble("FlexGrow"));
                            if (g <= 0) continue;
                            double add = free * g / growSum;
                            if (row) it.Values["Width"] = (it.GetDouble("Width") + add).ToString(inv);
                            else it.Values["Height"] = (it.GetDouble("Height") + add).ToString(inv);
                        }
                    }
                    else if (free < -0.5 && shrinkSum > 0)
                    {
                        foreach (var it in line)
                        {
                            double sh = Math.Max(0, it.GetDouble("FlexShrink"));
                            if (sh <= 0) continue;
                            double size = MainSize(it);
                            double cut = -free * sh * size / Math.Max(1e-6, line.Sum(x => Math.Max(0, x.GetDouble("FlexShrink")) * MainSize(x)));
                            if (row) it.Values["Width"] = Math.Max(0, size - cut).ToString(inv);
                            else it.Values["Height"] = Math.Max(0, size - cut).ToString(inv);
                        }
                    }
                    lineCross.Add(line.Max(it => CrossSize(it) + CrossMargin(it)));
                }
                double crossFree = (row ? innerH : innerW) - lineCross.Sum();
                double lineGap;
                double crossCursor = (row ? padT : padL) +
                    Distribute(alignContent, Math.Max(0, crossFree), lines.Count, out lineGap);

                for (int li = 0; li < lines.Count; li++)
                {
                    var line = lines[li];
                    double lineFree = Limit() - line.Sum(it => MainSize(it) + MainMargin(it));
                    double itemGap;
                    double cursor = (row ? padL : padT) + Distribute(justify, Math.Max(0, lineFree), line.Count, out itemGap);
                    foreach (var it in line)
                    {
                        double mL = row ? it.GetDouble("FlexMarginLeft") : it.GetDouble("FlexMarginTop");
                        double mR = row ? it.GetDouble("FlexMarginRight") : it.GetDouble("FlexMarginBottom");
                        cursor += mL;
                        double itemCross = CrossSize(it);
                        double cT = row ? it.GetDouble("FlexMarginTop") : it.GetDouble("FlexMarginLeft");
                        double cB = row ? it.GetDouble("FlexMarginBottom") : it.GetDouble("FlexMarginRight");
                        double crossPos;
                        bool stretch = align == 0 || align == 4;
                        if (stretch && it.Get(row ? "Height" : "Width") == null)
                        {
                            double grown = Math.Max(0, lineCross[li] - cT - cB);
                            if (row) { it.Values["Height"] = grown.ToString(inv); itemCross = grown; }
                            else { it.Values["Width"] = grown.ToString(inv); itemCross = grown; }
                            crossPos = cT;
                        }
                        else
                        {
                            crossPos = align switch
                            {
                                1 => cT,
                                2 => (lineCross[li] - itemCross) / 2,
                                3 => lineCross[li] - itemCross - cB,
                                _ => cT,
                            };
                        }
                        double x = row ? cursor : crossCursor + crossPos;
                        double y = row ? crossCursor + crossPos : cursor;
                        listPos[it.Name] = (x, y);
                        cursor += itemCross * 0 + (row ? SizeOf(it).W : SizeOf(it).H) + mR;
                    }
                    crossCursor += lineCross[li] + lineGap;
                }
            }


            // Attach parents before children so every element can resolve its absolute
            // origin, which PosType 1/8/11 need (they are window-relative, not parent-relative).
            int Depth(IniSection section)
            {
                int depth = 0;
                var cursor = section.Get("._Parent");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                {
                    depth++;
                    cursor = ini.ByName.TryGetValue(cursor, out var parent) ? parent.Get("._Parent") : null;
                }
                return depth;
            }

            var absPos = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal)
            {
                [rootSection.Name] = (0, 0),
            };
            // Previous sibling per parent, in INI order (the engine's item list order),
            // which PosType 7/9 anchor against.
            var prevSibling = new Dictionary<string, string>(StringComparer.Ordinal);
            var lastByParent = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var section in ini.Sections)
            {
                var parent = section.Get("._Parent") ?? "";
                if (lastByParent.TryGetValue(parent, out var last) && last != section.Name)
                    prevSibling[section.Name] = last;
                lastByParent[parent] = section.Name;
            }
            foreach (var section in ini.Sections.OrderBy(Depth))
            {
                if (!result.ElementsByRef.TryGetValue(section, out var element)) continue;
                if (element == result.Root) continue;
                var parentName = section.Get("._Parent");
                if (string.IsNullOrWhiteSpace(parentName)) continue;
                if (!result.Sections.TryGetValue(parentName, out var parentSection)) continue;
                if (!result.ElementsByRef.TryGetValue(parentSection, out var parent) || parent is not Canvas parentCanvas) continue;
                var parentAbs = absPos.TryGetValue(parentName, out var pa) ? pa : (0, 0);
                Attach(parentCanvas, parentSection, element, section, buttonSlots, rootWidth, rootHeight, SizeOf, tabX, listPos, parentAbs, prevSibling, result, absPos);
                double ax = Canvas.GetLeft(element); if (double.IsNaN(ax)) ax = 0;
                double ay = Canvas.GetTop(element); if (double.IsNaN(ay)) ay = 0;
                absPos[section.Name] = (parentAbs.X + ax, parentAbs.Y + ay);
            }
            return result;
        }

        /// <summary>
        /// The engine draws a button's label with the button's own state font scheme
        /// (WndButton NormalFont), not with the child Text's authored FontScheme. The
        /// 7.1 capture shows ACC_TreasureFinal's Btn_Leave label at scheme 3 (16px
        /// white) although Text_Leave authors scheme 28 (20px), and Btn_Export at
        /// scheme 18 (15px white) although Text_Export authors 27 (yellow) — both
        /// children are copy-paste leftovers (25% of the shipped buttons carry such
        /// mismatches). Normalize the label schemes here so size and colour follow the
        /// button.
        /// </summary>
        private static void ApplyButtonLabelFonts(IniFile ini)
        {
            var byParent = new Dictionary<string, List<IniSection>>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in ini.Sections)
            {
                var parent = section.Get("._Parent") ?? "";
                if (!byParent.TryGetValue(parent, out var children))
                    byParent[parent] = children = new List<IniSection>();
                children.Add(section);
            }

            foreach (var button in ini.Sections)
            {
                var type = button.Get("._WndType") ?? "";
                if (!type.Equals("WndButton", StringComparison.OrdinalIgnoreCase) &&
                    !type.Equals("WndCheckBox", StringComparison.OrdinalIgnoreCase)) continue;
                var normalFont = button.GetInt("NormalFont", 0);
                if (normalFont <= 0) continue;

                var stack = new Stack<IniSection>();
                if (byParent.TryGetValue(button.Name, out var direct))
                    foreach (var child in direct) stack.Push(child);
                while (stack.Count > 0)
                {
                    var section = stack.Pop();
                    if (string.Equals(section.Get("._WndType"), "Text", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(section.Get("$Text")))
                        section.Values["FontScheme"] = normalFont
                            .ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (byParent.TryGetValue(section.Name, out var children))
                        foreach (var child in children) stack.Push(child);
                }
            }
        }

        /// <summary>Container stand-in for section types we do not implement (WndPage,
        /// WndList, ...) when other sections are parented to them.</summary>
        private static FrameworkElement CreateFallbackContainer(IniSection section, bool isParent)
        {
            var canvas = new Canvas();
            var width = section.GetInt("Width");
            var height = section.GetInt("Height");
            if (width > 0) canvas.Width = width;
            if (height > 0) canvas.Height = height;
            if (Wireframe && isParent) canvas.ToolTip = section.Name;
            return canvas;
        }

        private static FrameworkElement CreateElement(IniSection section, UiTexCache textures, bool isParent, UiBuildResult result)
        {
            var type = section.Get("._WndType") ?? "";
            if (SkipTypes.Contains(type)) return null;

            // Item box: the engine paints the item's icon into the slot
            // (Box:SetObjectIcon); the viewer draws the icon atlas frame when the
            // inventory supplies one via the image override.
            if (type == "Box")
            {
                var iconPath = section.Get("Image");
                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    var iconFrame = section.GetInt("Frame", 0);
                    var iconSource = textures.GetFrame(iconPath, iconFrame);
                    if (iconSource != null)
                    {
                        var icon = new Image
                        {
                            Source = iconSource,
                            Stretch = Stretch.Uniform,
                            SnapsToDevicePixels = true,
                        };
                        var boxW = section.GetInt("Width");
                        var boxH = section.GetInt("Height");
                        icon.Width = boxW > 0 ? boxW : iconSource.PixelWidth;
                        icon.Height = boxH > 0 ? boxH : iconSource.PixelHeight;
                        // Rarity frame (UpdateItemBoxExtend): an overlay ON TOP of the
                        // icon so all four sides stay visible; the icon sits inside the
                        // frame by the border thickness.
                        if (UiProcessApp.Engine.Fonts.TryGetColor(section.Get("$BorderColor"), out var boxBorder))
                        {
                            var boxTh = Math.Max(1, section.GetInt("$BorderWidth", 1));
                            var framedBox = new Grid
                            {
                                Width = icon.Width + boxTh * 2,
                                Height = icon.Height + boxTh * 2,
                            };
                            icon.Margin = new Thickness(boxTh);
                            framedBox.Children.Add(icon);
                            framedBox.Children.Add(new Border
                            {
                                BorderBrush = new SolidColorBrush(boxBorder),
                                BorderThickness = new Thickness(boxTh),
                                IsHitTestVisible = false,
                            });
                            return framedBox;
                        }
                        return icon;
                    }
                }
            }

            if (ContainerTypes.Contains(type))
            {
                var container = new Canvas();
                var width = section.GetInt("Width");
                var height = section.GetInt("Height");
                if (width > 0) container.Width = width;
                if (height > 0) container.Height = height;
                if (section.GetInt("$Clip") == 1) container.ClipToBounds = true;
                // A WndScroll is a viewport: its content (ScrollHandle items and any
                // overhanging children) is clipped to the scroll's rect, as the engine does.
                else if (type.Equals("WndScroll", StringComparison.OrdinalIgnoreCase)) container.ClipToBounds = true;
                if (Wireframe)
                {
                    container.Background = new SolidColorBrush(Color.FromArgb(16, 0x6F, 0xC0, 0xEF));
                    container.ToolTip = section.Name;
                }
                // KGUI ShapTexture with AlphaShap=1 masks the subtree with the shape
                // texture's alpha: the MiddleMap's Handle_Border uses MapMask.tga (a
                // soft rounded rect) so the map feathers into the surrounding glass,
                // and the minimap lens uses MinimapSharp.tga. Without the mask the
                // map shows its hard-edged source rectangle.
                var shapeTexture = section.Get("ShapTexture");
                // KGUI WndMinimap names the lens mask `sharptexture` and paints the
                // active map itself; without a live map the engine falls back to
                // `defaulttexture` (defualtminimap) with the self marker (`image`
                // frame `selfframe`) pinned at the lens centre (MiniMap.ini
                // Minimap_Map: defaulttexture/sharptexture/image/selfframe).
                if (type.Equals("WndMinimap", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(shapeTexture)) shapeTexture = section.Get("sharptexture");
                    var defaultTexture = section.Get("defaulttexture");
                    if (!string.IsNullOrWhiteSpace(defaultTexture))
                    {
                        var mapImage = textures.GetFrame(defaultTexture, 0);
                        if (mapImage != null)
                        {
                            var mapBrush = new ImageBrush(mapImage) { Stretch = Stretch.UniformToFill };
                            // The engine draws the map texture around the player at the
                            // map config's scale ([config] scale=0.02 vs the middlemap
                            // art's 0.005867 -> ~3.4x), so the lens shows a local region.
                            var lensZoom = section.GetDouble("$LensZoom", 1.0);
                            if (lensZoom > 1.0)
                                mapBrush.RelativeTransform = new ScaleTransform(lensZoom, lensZoom, 0.5, 0.5);
                            container.Background = mapBrush;
                        }
                    }
                    var selfTexture = section.Get("image");
                    if (!string.IsNullOrWhiteSpace(selfTexture))
                    {
                        var selfImage = textures.GetFrame(selfTexture, section.GetInt("selfframe", 0));
                        if (selfImage != null)
                        {
                            var arrow = new Image { Source = selfImage, Stretch = Stretch.None };
                            double aw = selfImage.PixelWidth, ah = selfImage.PixelHeight;
                            Canvas.SetLeft(arrow, width > 0 ? (width - aw) / 2 : 0);
                            Canvas.SetTop(arrow, height > 0 ? (height - ah) / 2 : 0);
                            container.Children.Add(arrow);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(shapeTexture) && section.GetInt("AlphaShap") == 1)
                {
                    var shape = textures.GetFrame(shapeTexture, 0);
                    if (shape != null)
                        container.OpacityMask = new ImageBrush(shape) { Stretch = Stretch.Fill };
                }
                // KGUI WndEdit draws its authored $Placeholder (e.g. the world-map
                // search box "城镇或秘境" = STRING_Tip1) until the player types.
                if (type == "WndEdit")
                {
                    var rawPlaceholder = section.Get("$Placeholder");
                    string placeholder = null;
                    if (!string.IsNullOrWhiteSpace(rawPlaceholder))
                    {
                        if (GameData.TryResolveString(rawPlaceholder, out var resolvedPlaceholder))
                            placeholder = resolvedPlaceholder;
                        else if (!rawPlaceholder.StartsWith("STR", StringComparison.OrdinalIgnoreCase))
                            placeholder = rawPlaceholder;
                    }
                    double editSize = 14;
                    Brush editBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    if (UiProcessApp.Engine.Fonts.TryGet(section.GetInt("FontScheme", 212),
                                                         out var editFontSize, out var editColor, out _))
                    {
                        editSize = editFontSize;
                        editBrush = new SolidColorBrush(editColor);
                    }
                    // A real input (the engine's WndEdit): the placeholder shows while empty
                    // and unfocused; typing dispatches OnEditChanged through the interaction
                    // server (MainWindow wires the TextBox to the section).
                    var edit = new System.Windows.Controls.TextBox
                    {
                        Background = System.Windows.Media.Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = editBrush,
                        FontSize = editSize,
                        FontFamily = ResolveFontFamily(section, textures.Assets),
                        Width = section.GetInt("Width"),
                        Height = section.GetInt("Height"),
                        VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
                        Tag = placeholder ?? "",
                    };
                    if (section.GetInt("TextAlign", 0) == 1)
                        edit.TextAlignment = System.Windows.TextAlignment.Center;
                    if (!string.IsNullOrWhiteSpace(placeholder))
                    {
                        edit.Text = placeholder;
                        edit.Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
                        edit.GotFocus += (o, args) =>
                        {
                            if (edit.Text == (string)edit.Tag)
                            {
                                edit.Text = "";
                                edit.Foreground = editBrush;
                            }
                        };
                        edit.LostFocus += (o, args) =>
                        {
                            if (string.IsNullOrEmpty(edit.Text))
                            {
                                edit.Text = (string)edit.Tag;
                                edit.Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
                            }
                        };
                    }
                    container.Children.Add(edit);
                }
                return container;
            }

            if (type == "Image" || type == "WndButton" || type == "WndCheckBox")
            {
                var imagePath = section.Get("Image");
                var width = section.GetInt("Width");
                var height = section.GetInt("Height");
                // A checkbox created checked (CheckedWhenCreate=1, e.g. the queue mode
                // tabs) paints its checked state frame group, not the unchecked one;
                // the runtime's Check()/Enable() mutations override both.
                var isChecked = type == "WndCheckBox" && section.GetInt("CheckedWhenCreate") == 1;
                if (type == "WndCheckBox" && section.Get("$Checked") != null)
                    isChecked = section.Get("$Checked") == "1";
                bool disabled = section.Get("$Disabled") == "1";
                int GroupOr(string groupKey, string fallbackKey)
                {
                    var group = section.GetInt(groupKey, -1);
                    if (group >= 0 && !string.IsNullOrWhiteSpace(imagePath))
                    {
                        var grouped = textures.GetGroupFrame(imagePath, group);
                        if (grouped >= 0) return grouped;
                    }
                    return FrameOrGroup(section, fallbackKey, textures, imagePath);
                }
                var frame = type switch
                {
                    // Buttons render their normal-state frame group; Frame is only a fallback.
                    "WndButton" => disabled
                        ? GroupOr("DisableGroup", "NormalGroup")
                        : FrameOrGroup(section, "NormalGroup", textures, imagePath),
                    "WndCheckBox" => disabled
                        ? GroupOr(isChecked ? "CheckAndDisable" : "UnCheckAndDisable",
                                  isChecked ? "CheckAndEnable" : "UnCheckAndEnable")
                        : FrameOrGroup(section, isChecked ? "CheckAndEnable" : "UnCheckAndEnable", textures, imagePath),
                    // Frame=-1 is authored for state/runtime-driven frames; default to 0.
                    _ => Math.Max(0, section.GetInt("Frame", 0)),
                };
                var autoSize = section.GetBool("AutoSize");
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    // No atlas: keep the authored size, drop truly empty spacers unless wireframe.
                    if (width > 0 || height > 0 || autoSize)
                        result.Placeholders.Add($"{section.Name} [{type} no Image]");
                    if (width <= 0 && height <= 0 && !autoSize)
                        return Wireframe ? Placeholder(section.Name, width, height) : (FrameworkElement)new Canvas { Visibility = Visibility.Collapsed };
                    if (Wireframe) return Placeholder(section.Name, width, height);
                    var placeholder = new Canvas();
                    if (width > 0) placeholder.Width = width;
                    if (height > 0) placeholder.Height = height;
                    return placeholder;
                }
                var source = textures.GetFrame(imagePath, frame);
                if (source == null)
                {
                    // An authored empty frame (w/h 0 in the atlas) means "draw nothing",
                    // not a missing asset - the engine skips it.
                    if (textures.IsEmptyFrame(imagePath, frame))
                        return Wireframe ? Placeholder(section.Name, width, height)
                                         : (FrameworkElement)new Canvas { Visibility = Visibility.Collapsed };
                    // Atlas/frame missing: the engine still lays the element out at the
                    // authored size (or the atlas frame size when it later loads).
                    result.Placeholders.Add($"{section.Name} [{type} {imagePath} frame={frame}]");
                    if (width <= 0 && height <= 0 && !autoSize)
                        return Wireframe ? Placeholder(section.Name, width, height) : (FrameworkElement)new Canvas { Visibility = Visibility.Collapsed };
                    return Placeholder(section.Name, width, height);
                }
                var imageType = section.GetInt("ImageType");
                // The engine draws the frame at ImageWidth/ImageHeight when authored
                // (buttons paint a small texture inside a larger hit area).
                var imageWidth = section.GetInt("ImageWidth");
                var imageHeight = section.GetInt("ImageHeight");
                var drawWidth = imageWidth > 0 ? imageWidth : width;
                var drawHeight = imageHeight > 0 ? imageHeight : height;
                // AutoSize image: the engine draws the frame at its natural pixel size
                // (the authored Width/Height is just the editor box; RemainingTimeNotify's
                // 123x41 labels stretched small MapWindow8 frames and read blurry).
                if (autoSize)
                {
                    drawWidth = source.PixelWidth;
                    drawHeight = source.PixelHeight;
                }
                FrameworkElement visual;
                if (imageType == 16)
                {
                    // 琉璃·境 frosted glass: the frame is a white/gradient mask the engine
                    // uses as a blur+translucency shape (PanelBg frame 6 = solid fill,
                    // frame 4 = soft-edged popup). WPF has no backdrop blur offscreen, so
                    // approximate with the 琉璃 panel tone masked by the frame's alpha.
                    // The tone is matched to the captures (the MiddleMap band and the
                    // queue panel glass measure ~(52,70,70)/(67,82,79), a neutral dark
                    // teal — a bluer tone reads unnatural against the glass panels).
                    double gw = drawWidth > 0 ? drawWidth : source.PixelWidth;
                    double gh = drawHeight > 0 ? drawHeight : source.PixelHeight;
                    var glass = new Border
                    {
                        Width = gw,
                        Height = gh,
                        Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x33, 0x39, 0x3E)),
                        OpacityMask = new ImageBrush(source) { Stretch = Stretch.Fill },
                    };
                    visual = glass;
                }
                else if ((imageType == 10 || imageType == 12 || (imageType >= 17 && imageType <= 19)) &&
                         source != null && drawWidth > 0 && drawHeight > 0)
                {
                    // The engine's draw dispatch puts ImageType 10/11/12/17/18/19 on ONE
                    // diced path (client KGUIX64 0x180117D7C: cmp eax,0xa/0xb/0xc/0x11/0x12/0x13
                    // -> je 0x180117CE7). The slice geometry comes from the image data, not
                    // the type id: prefer the four detected borders (nine-slice), fall back
                    // to plain stretch. Type 11 keeps the horizontal-caps path below.
                    var borders = textures.GetDicedBorders(imagePath, frame);
                    int l = Math.Max(0, borders.L), t = Math.Max(0, borders.T);
                    int r = Math.Max(0, borders.R), b = Math.Max(0, borders.B);
                    if (l > 0 && t > 0 && r > 0 && b > 0 &&
                        l + r < source.PixelWidth && t + b < source.PixelHeight)
                    {
                        visual = new NineSliceImage(source, drawWidth, drawHeight, l, t, r, b);
                    }
                    else
                    {
                        var plain = new Image
                        {
                            Source = source,
                            Stretch = Stretch.Fill,
                            SnapsToDevicePixels = true,
                            Width = drawWidth,
                            Height = drawHeight,
                        };
                        visual = plain;
                    }
                }
                else if (imageType == 11 && drawWidth > 0 && drawHeight > 0 &&
                         textures.GetHorizontalCaps(imagePath, frame) is var caps11 &&
                         caps11.L > 0 && caps11.R > 0 &&
                         caps11.L + caps11.R < source.PixelWidth - 1)
                {
                    // ImageType=11: the caps keep their pixel size, only the middle
                    // stretches (the KGUI dispatch groups 10/11/12; GT-verified on the
                    // queue panel's reward plaque - a plain stretch flattened its caps).
                    visual = new HorizontalSliceImage(source, drawWidth, drawHeight, caps11.L, caps11.R);
                }
                else
                {
                    var image = new Image
                    {
                        Source = source,
                        Stretch = Stretch.Fill,
                        SnapsToDevicePixels = true,
                    };
                    // KGUI images default to the frame's natural size when no size is authored.
                    if (drawWidth > 0) image.Width = drawWidth;
                    else if (source != null) image.Width = source.PixelWidth;
                    if (drawHeight > 0) image.Height = drawHeight;
                    else if (source != null) image.Height = source.PixelHeight;

                    if (imageType == 8)
                    {
                        image.RenderTransformOrigin = new Point(0.5, 0.5);
                        image.RenderTransform = new ScaleTransform(-1, 1);
                    }
                    visual = image;
                }

                // ImagePercent scales the frame's opacity (the 随机地图 flourishes are
                // authored 0.35, the old-skin chrome 1.0).
                var imagePercent = section.GetDouble("ImagePercent");
                if (imagePercent > 0 && imagePercent < 1)
                    visual.Opacity = imagePercent;

                // Buttons and checkboxes own child handles/texts; host them in a canvas so
                // the INI hierarchy (and its offsets) survives.
                if (type == "WndButton" || type == "WndCheckBox")
                {
                    var host = new Canvas();
                    if (width > 0) host.Width = width;
                    else host.Width = visual.Width;
                    if (height > 0) host.Height = height;
                    else host.Height = visual.Height;
                    Canvas.SetLeft(visual, section.GetInt("ImageRelX"));
                    Canvas.SetTop(visual, section.GetInt("ImageRelY"));
                    host.Children.Add(visual);
                    return host;
                }
                // Inventory `$BorderColor`/`$BorderWidth`: the engine's item boxes
                // (UpdateItemBoxExtend) draw a rarity-colored frame around the cell.
                // The frame is an overlay ON TOP of the art (a wrapping Border hid its
                // top/bottom lines under the sibling icon art drawn later in the row).
                if (UiProcessApp.Engine.Fonts.TryGetColor(section.Get("$BorderColor"), out var rarityBorder))
                {
                    var thickness = Math.Max(1, section.GetInt("$BorderWidth", 1));
                    var framed = new Grid();
                    visual.Margin = new Thickness(thickness);
                    if (!double.IsNaN(visual.Width)) framed.Width = visual.Width + thickness * 2;
                    if (!double.IsNaN(visual.Height)) framed.Height = visual.Height + thickness * 2;
                    framed.Children.Add(visual);
                    framed.Children.Add(new Border
                    {
                        BorderBrush = new SolidColorBrush(rarityBorder),
                        BorderThickness = new Thickness(thickness),
                        IsHitTestVisible = false,
                    });
                    return framed;
                }
                return visual;
            }

            if (type == "Text")
            {
                var rawText = section.Get("$Text");
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    if (Wireframe) return Placeholder(section.Name, section.GetInt("Width"), section.GetInt("Height"), false);
                    return isParent ? CreateFallbackContainer(section, true) : null;
                }
                if (!GameData.TryResolveString(rawText, out var text))
                {
                    // Missing string-table entry: hide the label instead of showing the id.
                    if (rawText.StartsWith("STR", StringComparison.OrdinalIgnoreCase))
                    {
                        result.UnresolvedStrings.Add($"{section.Name} {rawText}");
                        return isParent ? CreateFallbackContainer(section, true) : null;
                    }
                    text = rawText;
                }
                double fontSize = 14;
                Brush foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                Color? borderColor = null;
                if (UiProcessApp.Engine.Fonts.TryGet(section.GetInt("FontScheme", 212),
                                                     out var schemeSize, out var schemeColor, out var schemeBorder))
                {
                    fontSize = schemeSize;
                    foreground = new SolidColorBrush(schemeColor);
                    borderColor = schemeBorder;
                }
                // The engine's text decoder reads FontColor as a color name and
                // overrides the scheme's fill (e.g. Title_EveryWin_Reward_1 =
                // yellow2 while its scheme is white).
                if (UiProcessApp.Engine.Fonts.TryGetColor(section.Get("FontColor"), out var fontColor))
                    foreground = new SolidColorBrush(fontColor);
                var block = new TextBlock
                {
                    Text = text.Replace("\\n", "\n").Replace("\\t", "\t"),
                    FontFamily = ResolveFontFamily(section, textures.Assets),
                    FontSize = fontSize,
                    Foreground = foreground,
                };
                if (borderColor.HasValue)
                {
                    block.Effect = new DropShadowEffect
                    {
                        Color = borderColor.Value,
                        BlurRadius = 1,
                        ShadowDepth = 0,
                        Opacity = 1,
                    };
                }
                var width = section.GetInt("Width");
                var height = section.GetInt("Height");
                var hAlign = section.GetInt("HAlign");
                var vAlign = section.GetInt("VAlign");
                if (hAlign == 1) block.TextAlignment = TextAlignment.Center;
                else if (hAlign == 2) block.TextAlignment = TextAlignment.Right;
                if (Wireframe) block.ToolTip = section.Name;

                // The engine (KItemText) lays the text with the shipped font's own
                // line metrics: VAlign=1 centers the ascent+descent line box in the
                // item, so the ink lands slightly above the box center (the descent
                // hangs below). Pinning WPF's LineHeight to the box instead drags the
                // ink down ~3px, so position the block explicitly in a canvas host.
                double textWidth = MeasureTextWidth(block, text, fontSize);
                double textHeight = MeasureTextHeight(block, text, fontSize);
                // AutoSize text takes its WIDTH from the measured content (the engine's
                // KItemText measures the string and the PosType 9 flow packs items at
                // the measured width): ACC_TreasureFinal's Text_Line is AutoSize=1
                // PosType=9, so the 击/助 pair reads "2/4" — the authored 34px box left
                // "/" centred in its own box and the columns apart. The authored HEIGHT
                // is kept: it carries the row's vertical centering (VAlign=1), and
                // shrinking it would float the "/" above the digits.
                if (section.GetBool("AutoSize") && textWidth > 0)
                    width = (int)Math.Ceiling(textWidth);
                double offsetY = 0;
                if (height > 0)
                {
                    if (vAlign == 1) offsetY = (height - textHeight) / 2;
                    else if (vAlign == 2) offsetY = height - textHeight;
                }
                bool overflowing = width > 0 && textWidth > width + 0.5 && textWidth <= width * 1.6;
                bool needsHost = overflowing || width > 0 && (hAlign == 1 || hAlign == 2) || height > 0 && offsetY != 0;
                if (needsHost)
                {
                    var box = new Canvas
                    {
                        Width = width > 0 ? width : textWidth,
                        Height = height > 0 ? height : textHeight,
                    };
                    double offsetX = hAlign == 1 ? (width - textWidth) / 2
                        : hAlign == 2 ? width - textWidth : 0;
                    // FormattedText under-measures some CJK strings; when the text fits
                    // the authored box, let WPF align it exactly instead of trusting
                    // the measured width.
                    if (width > 0 && !overflowing && (hAlign == 1 || hAlign == 2))
                    {
                        block.Width = width;
                        block.TextAlignment = hAlign == 1 ? TextAlignment.Center : TextAlignment.Right;
                        offsetX = 0;
                    }
                    Canvas.SetLeft(block, offsetX);
                    Canvas.SetTop(block, offsetY);
                    box.Children.Add(block);
                    return box;
                }
                if (width > 0) block.Width = width;
                if (height > 0) block.Height = height;
                return block;
            }

            // Unknown control type: keep it as an empty container when it has children.
            return isParent ? CreateFallbackContainer(section, true) : null;
        }

        private static readonly Dictionary<string, FontFamily> FontFamilies = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The engine renders every label with the shipped font of the scheme's
        /// FontID (fontlist.ini `File=`, e.g. 方正黑体 fzht_GBK.ttf): glyph shapes and
        /// the ascent/descent used by VAlign centering come from that file, not from
        /// a system font. Falls back to a system family when the file is absent.
        /// </summary>
        private static FontFamily ResolveFontFamily(IniSection section, AssetResolver assets)
        {
            if (!UiProcessApp.Engine.Fonts.TryGetFontFile(section.GetInt("FontScheme", 212), out var file))
                return new FontFamily("Microsoft YaHei UI");
            if (FontFamilies.TryGetValue(file, out var cached)) return cached;
            FontFamily family = null;
            var path = assets?.Resolve("ui/Font/" + file);
            if (path != null && File.Exists(path))
            {
                try
                {
                    // A file-based family only resolves when the fragment names the
                    // font's internal family (WPF ignores the file otherwise).
                    var glyph = new GlyphTypeface(new Uri(path));
                    var familyName = glyph.FamilyNames.Values.FirstOrDefault() ?? Path.GetFileNameWithoutExtension(path);
                    family = new FontFamily(new Uri(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar),
                                            "./" + Path.GetFileName(path) + "#" + familyName);
                    var probe = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    if (!probe.TryGetGlyphTypeface(out _)) family = null;
                }
                catch { family = null; }
            }
            if (family == null) family = new FontFamily("Microsoft YaHei UI");
            FontFamilies[file] = family;
            return family;
        }

        private static double MeasureTextWidth(TextBlock block, string text, double fontSize)
        {
            try
            {
                var typeface = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch);
                var formatted = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    1.0);
                return formatted.WidthIncludingTrailingWhitespace;
            }
            catch
            {
                return 0;
            }
        }

        private static double MeasureTextHeight(TextBlock block, string text, double fontSize)
        {
            try
            {
                var typeface = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch);
                var formatted = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    1.0);
                return formatted.Height;
            }
            catch
            {
                return fontSize;
            }
        }

        private static int FrameOrGroup(IniSection section, string groupKey, UiTexCache textures, string imagePath)
        {
            // The engine applies the state's group id to the item; when the group is
            // empty/out of range the item keeps its authored Frame.
            var group = section.GetInt(groupKey, -1);
            if (group >= 0 && !string.IsNullOrWhiteSpace(imagePath))
            {
                var frame = textures.GetGroupFrame(imagePath, group);
                if (frame >= 0) return frame;
            }
            if (group >= 0 && string.IsNullOrWhiteSpace(imagePath)) return group + 1;
            // Frame=-1 is authored for state/runtime-driven frames; default to the
            // first frame so the element still shows its base art.
            var fallback = section.GetInt("Frame", 0);
            return fallback < 0 ? 0 : fallback;
        }

        private static Canvas Placeholder(string name, double width, double height, bool tint = true)
        {
            var canvas = new Canvas
            {
                Width = width > 0 ? width : 48,
                Height = height > 0 ? height : 16,
                ToolTip = name,
                Background = new SolidColorBrush(Color.FromArgb(255, 0x2A, 0x2A, 0x2A)),
            };
            // A faint border so missing-art elements keep a readable silhouette.
            var outline = new System.Windows.Shapes.Rectangle
            {
                Width = canvas.Width,
                Height = canvas.Height,
                Stroke = new SolidColorBrush(Color.FromArgb(140, 0x60, 0x60, 0x60)),
                StrokeThickness = 1,
                IsHitTestVisible = false,
            };
            canvas.Children.Add(outline);
            if (Wireframe)
            {
                canvas.Children.Add(new TextBlock
                {
                    Text = name,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 0xA0, 0xB8, 0xD0)),
                    Margin = new Thickness(2, 1, 0, 0),
                    IsHitTestVisible = false,
                });
            }
            return canvas;
        }

        /// <summary>
        /// Anchor fractions for the 13 KGUI side names, in the engine's GetSide order
        /// (TOP, BOTTOM, LEFT, RIGHT, TOPLEFT, TOPRIGHT, BOTTOMLEFT, BOTTOMRIGHT,
        /// CENTER, LEFTCENTER, RIGHTCENTER, TOPCENTER, BOTTOMCENTER).
        /// </summary>
        private static readonly (double X, double Y)[] SideFractions =
        {
            (0.5, 0.0), (0.5, 1.0), (0.0, 0.5), (1.0, 0.5),
            (0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0),
            (0.5, 0.5), (0.0, 0.5), (1.0, 0.5), (0.5, 0.0), (0.5, 1.0),
        };

        private static int SideIndex(string name)
        {
            switch ((name ?? "").Trim().ToUpperInvariant())
            {
                case "TOP": return 0;
                case "BOTTOM": return 1;
                case "LEFT": return 2;
                case "RIGHT": return 3;
                case "TOPLEFT": return 4;
                case "TOPRIGHT": return 5;
                case "BOTTOMLEFT": return 6;
                case "BOTTOMRIGHT": return 7;
                case "CENTER": return 8;
                case "LEFTCENTER": return 9;
                case "RIGHTCENTER": return 10;
                case "TOPCENTER": return 11;
                case "BOTTOMCENTER": return 12;
                default: return -1;
            }
        }

        /// <summary>
        /// Engine AnchorArgs: "parentSide,selfSide,dx,dy" — align a point on this element
        /// to the matching point on the parent, then apply the pixel offsets. When the
        /// section carries AnchorDst, the alignment basis is that section's rect instead
        /// (resolved by name after stripping the "../" path prefix), like the engine's
        /// AnchorDst target lookup.
        /// </summary>
        private static bool TryAnchorArgs(IniSection section, double parentWidth, double parentHeight, double elementWidth, double elementHeight, out double left, out double top, (double X, double Y, double W, double H)? target = null)
        {
            left = top = 0;
            var raw = section.Get("AnchorArgs");
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var parts = raw.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            var parentSide = SideIndex(parts[0]);
            var selfSide = SideIndex(parts[1]);
            if (parentSide < 0 || selfSide < 0) return false;
            double dx = parts.Length > 2 && double.TryParse(parts[2], out var px) ? px : 0;
            double dy = parts.Length > 3 && double.TryParse(parts[3], out var py) ? py : 0;
            var parentFraction = SideFractions[parentSide];
            var selfFraction = SideFractions[selfSide];
            double basisX = target?.X ?? 0;
            double basisY = target?.Y ?? 0;
            double basisWidth = target?.W ?? parentWidth;
            double basisHeight = target?.H ?? parentHeight;
            left = basisX + basisWidth * parentFraction.X - elementWidth * selfFraction.X + dx;
            top = basisY + basisHeight * parentFraction.Y - elementHeight * selfFraction.Y + dy;
            return true;
        }

        /// <summary>
        /// Resolves a section's AnchorDst to a rect in parent-local coordinates. "root" is
        /// the window; "../Name"/"../../Name" resolve to the named section (names are
        /// unique) and yield its absolute rect minus the parent's origin. Unresolvable
        /// targets return null so the anchor falls back to the parent (pre-AnchorDst
        /// behavior); sections anchored later in the tree are not available yet.
        /// </summary>
        private static (double X, double Y, double W, double H)? ResolveAnchorDst(IniSection section, (double X, double Y) parentAbs, double rootWidth, double rootHeight, Func<IniSection, (double W, double H)> sizeOf, Dictionary<string, (double X, double Y)> absPos, UiBuildResult build)
        {
            var raw = section.Get("AnchorDst");
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Trim().Replace('\\', '/');
            if (string.Equals(raw, "root", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "client", StringComparison.OrdinalIgnoreCase))
                return (0 - parentAbs.X, 0 - parentAbs.Y, rootWidth, rootHeight);
            if (build == null || absPos == null) return null;
            string name = null;
            foreach (var part in raw.Split('/'))
                if (!string.IsNullOrWhiteSpace(part) && part != "." && part != "..") name = part;
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (!build.Sections.TryGetValue(name, out var targetSection)) return null;
            if (!absPos.TryGetValue(name, out var abs)) return null;
            var size = sizeOf(targetSection);
            return (abs.X - parentAbs.X, abs.Y - parentAbs.Y, size.W, size.H);
        }

        private static void Attach(Canvas parent, IniSection parentSection, FrameworkElement element, IniSection section, HashSet<string> buttonSlots, double rootWidth, double rootHeight, Func<IniSection, (double W, double H)> sizeOf, Dictionary<string, double> tabX = null, Dictionary<string, (double X, double Y)> listPos = null, (double X, double Y) parentAbs = default, Dictionary<string, string> prevSibling = null, UiBuildResult build = null, Dictionary<string, (double X, double Y)> absPos = null)
        {
            if (ReferenceEquals(parent, element)) return; // guard against aliased self-parents
            double left = section.GetInt("Left");
            if (tabX != null && tabX.TryGetValue(section.Name, out var tabLeft)) left = tabLeft;
            double top = section.GetInt("Top");
            var parentSize = sizeOf(parentSection);
            double parentWidth = parentSize.W;
            double parentHeight = parentSize.H;
            double elementWidth = ElementWidth(element, section, sizeOf);
            double elementHeight = ElementHeight(element, section, sizeOf);

            // List containers position their items themselves; the authored PosType/Left
            // of an item is meaningless (prototypes are parked off-window).
            if (listPos != null && listPos.TryGetValue(section.Name, out var listItem))
            {
                left = listItem.X;
                top = listItem.Y;
                Canvas.SetLeft(element, left);
                Canvas.SetTop(element, top);
                if (Wireframe && element.ToolTip == null) element.ToolTip = section.Name;
                ApplyAlphaAndVisibility(element, section);
                parent.Children.Add(element);
                return;
            }

            var anchorTarget = ResolveAnchorDst(section, parentAbs, rootWidth, rootHeight, sizeOf, absPos, build);
            if (TryAnchorArgs(section, parentWidth, parentHeight, elementWidth, elementHeight, out var anchorLeft, out var anchorTop, anchorTarget))
            {
                left = anchorLeft;
                top = anchorTop;
                Canvas.SetLeft(element, left);
                Canvas.SetTop(element, top);
                if (Wireframe && element.ToolTip == null) element.ToolTip = section.Name;
                ApplyAlphaAndVisibility(element, section);
                parent.Children.Add(element);
                return;
            }

            switch (section.GetInt("PosType"))
            {
                case 1: // bottom-aligned in the window (keeps its own Left)
                    if (rootHeight > 0) top = rootHeight - elementHeight - parentAbs.Y;
                    break;
                case 2: // left-center aligned in the parent (keeps authored x)
                    if (parentHeight > 0) top += (parentHeight - elementHeight) / 2;
                    break;
                case 10: // engine item-flow when the parent lays out items
                         // (FirstItemPosType != 0): below the previous item; otherwise
                         // right-center aligned in the parent (Map window art rows).
                    if (parentSection != null && parentSection.GetInt("FirstItemPosType") != 0)
                    {
                        if (prevSibling != null && prevSibling.TryGetValue(section.Name, out var prevName10) &&
                            build != null && build.Elements.TryGetValue(prevName10, out var prevEl10) &&
                            prevEl10 is FrameworkElement pe10 && pe10.Visibility == Visibility.Visible &&
                            build.Sections.TryGetValue(prevName10, out var prevSec10))
                        {
                            double px10 = Canvas.GetLeft(pe10); if (double.IsNaN(px10)) px10 = 0;
                            double py10 = Canvas.GetTop(pe10); if (double.IsNaN(py10)) py10 = 0;
                            left = px10;
                            top = py10 + ElementHeight(pe10, prevSec10, sizeOf);
                        }
                        break;
                    }
                    if (parentWidth > 0) left += parentWidth - elementWidth;
                    if (parentHeight > 0) top += (parentHeight - elementHeight) / 2;
                    break;
                case 6: // centered on the given point (player markers)
                    left -= elementWidth / 2;
                    top -= elementHeight / 2;
                    break;
                case 7: // right of the previous sibling (falls back to parent's right edge)
                    if (!TryFlowAfterPrevious(parent, section, prevSibling, build, sizeOf, ref left, ref top) &&
                        left == 0 && parentWidth > 0)
                        left = parentWidth - elementWidth;
                    break;
                case 8: // right-aligned in the window when no offset is authored...
                    // ...for plain handles. In an item-flow handle (FirstItemPosType != 0)
                    // it continues the previous item's line instead: LootList's money row
                    // lays "12 [gold] / 34 [silver] 56 [copper]" that way (the glyph items
                    // are PosType 8 after their numbers).
                    if (parentSection != null && parentSection.GetInt("FirstItemPosType") != 0 &&
                        TryFlowAfterPrevious(parent, section, prevSibling, build, sizeOf, ref left, ref top))
                        break;
                    // Tiny auto-size parents (e.g. MapQueue's Handle_Dots) mean "keep
                    // the authored origin"; window-right would park them at the edge.
                    // An explicitly authored/overridden Left (even 0) wins - the main
                    // bar's slot strip is a left-anchored flow row whose first image
                    // authors no Left and gets parked right otherwise.
                    if (left == 0 && section.Get("Left") == null && rootWidth > 0 && parentWidth >= 32)
                        left = rootWidth - elementWidth - parentAbs.X;
                    break;
                case 11: // bottom-right aligned in the window when no offset is authored
                    if (left == 0 && top == 0 && rootWidth > 0) left = rootWidth - elementWidth - parentAbs.X;
                    if (top == 0 && rootHeight > 0) top = rootHeight - elementHeight - parentAbs.Y;
                    break;
                case 12: // centered horizontally, bottom-aligned
                    if (parentWidth > 0) left = (parentWidth - elementWidth) / 2;
                    if (parentHeight > 0) top = parentHeight - elementHeight;
                    break;
                case 9: // flows to the right of the previous sibling
                    if (!TryFlowAfterPrevious(parent, section, prevSibling, build, sizeOf, ref left, ref top) &&
                        parent.Children.Count > 0)
                    {
                        var previous = parent.Children[parent.Children.Count - 1] as FrameworkElement;
                        if (previous != null)
                        {
                            left = Canvas.GetLeft(previous) + (double.IsNaN(previous.Width) ? 0 : previous.Width);
                            top = Canvas.GetTop(previous);
                        }
                    }
                    break;
            }

            // CorrectPos: the engine clamps a runtime-placed control into its parent.
            if (section.Get("$CorrectPos") == "1")
            {
                if (parentWidth > 0 && elementWidth <= parentWidth)
                    left = Math.Min(Math.Max(0, left), parentWidth - elementWidth);
                if (parentHeight > 0 && elementHeight <= parentHeight)
                    top = Math.Min(Math.Max(0, top), parentHeight - elementHeight);
            }
            // SetScrollPos: the scroll control's content handle shifts by -pos.
            if (parentSection != null && section.Name.StartsWith("Handle_", StringComparison.OrdinalIgnoreCase) &&
                parentSection.Get("$ScrollPos") != null &&
                double.TryParse(parentSection.Get("$ScrollPos"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var scrollPos))
            {
                top -= scrollPos;
            }

            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
            if (Wireframe && element.ToolTip == null) element.ToolTip = section.Name;
            if (Wireframe && element.ToolTip == null) element.ToolTip = section.Name;

            ApplyAlphaAndVisibility(element, section);

            // Several buttons/checkboxes share one slot as context alternatives; only one shows in game.
            if (section.Name.StartsWith("Btn_", StringComparison.OrdinalIgnoreCase) ||
                section.Name.StartsWith("CheckBox_", StringComparison.OrdinalIgnoreCase))
            {
                bool taken = false;
                foreach (var slot in buttonSlots)
                {
                    if (!slot.StartsWith(parentSection.Name + "|", StringComparison.OrdinalIgnoreCase)) continue;
                    var parts = slot.Substring(parentSection.Name.Length + 1).Split(',');
                    if (parts.Length != 2) continue;
                    if (Math.Abs(double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) - left) <= 8 &&
                        Math.Abs(double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) - top) <= 8)
                    {
                        taken = true;
                        break;
                    }
                }
                if (taken) element.Visibility = Visibility.Collapsed;
                else buttonSlots.Add($"{parentSection.Name}|{left:0.#},{top:0.#}");
            }

            parent.Children.Add(element);
        }

        /// <summary>Authored size, else the measured text size, else the intrinsic subtree size.</summary>
        private static double ElementWidth(FrameworkElement element, IniSection section, Func<IniSection, (double W, double H)> sizeOf)
        {
            if (!double.IsNaN(element.Width)) return element.Width;
            if (element is TextBlock)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return element.DesiredSize.Width;
            }
            return sizeOf(section).W;
        }

        private static double ElementHeight(FrameworkElement element, IniSection section, Func<IniSection, (double W, double H)> sizeOf)
        {
            if (!double.IsNaN(element.Height)) return element.Height;
            if (element is TextBlock)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return element.DesiredSize.Height;
            }
            return sizeOf(section).H;
        }

        /// <summary>Places an element right after its previous sibling (PosType 7/9).</summary>
        private static bool TryFlowAfterPrevious(Canvas parent, IniSection section, Dictionary<string, string> prevSibling, UiBuildResult build, Func<IniSection, (double W, double H)> sizeOf, ref double left, ref double top)
        {
            if (prevSibling == null || build == null) return false;
            if (!prevSibling.TryGetValue(section.Name, out var prevName)) return false;
            if (!build.Elements.TryGetValue(prevName, out var prevElement) || prevElement is not FrameworkElement previous) return false;
            if (previous.Visibility != Visibility.Visible) return false;
            if (!build.Sections.TryGetValue(prevName, out var prevSection)) return false;
            double px = Canvas.GetLeft(previous); if (double.IsNaN(px)) px = 0;
            double py = Canvas.GetTop(previous); if (double.IsNaN(py)) py = 0;
            left = px + ElementWidth(previous, prevSection, sizeOf);
            top = py;
            return true;
        }

        private static void ApplyAlphaAndVisibility(FrameworkElement element, IniSection section)
        {
            var alpha = section.GetInt("Alpha", 255);
            if (alpha < 255 && section.Name != "Image_Map") element.Opacity = Math.Max(0, alpha) / 255.0;

            if (section.GetBool("Visible") == false && section.Get("Visible") != null) element.Visibility = Visibility.Collapsed;
            if (HiddenSections.Contains(section.Name) || section.Name.StartsWith("Image_Black", StringComparison.OrdinalIgnoreCase))
                element.Visibility = Visibility.Collapsed;
        }
    }
}


