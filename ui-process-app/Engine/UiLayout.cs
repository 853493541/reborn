using System;
using System.Collections.Generic;
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
        public readonly Dictionary<string, FrameworkElement> Elements = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, IniSection> Sections = new Dictionary<string, IniSection>(StringComparer.OrdinalIgnoreCase);
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
                var element = CreateElement(section, textures, parentNames.Contains(section.Name));
                if (element == null) continue;
                result.Elements[section.Name] = element;
                result.Sections[section.Name] = section;
            }

            result.Root = result.Elements[rootSection.Name];
            var buttonSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootWidth = (double)rootSection.GetInt("Width");
            var rootHeight = (double)rootSection.GetInt("Height");

            // KGUI containers often omit Width/Height. Handles auto-size to their
            // content, other containers inherit the nearest sized ancestor.
            var intrinsic = new Dictionary<string, (double W, double H)>(StringComparer.OrdinalIgnoreCase);
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
            double ElementSizeW(IniSection section)
            {
                var own = Intrinsic(section, 0);
                if (own.W > 0) return own.W;
                var imagePath = section.Get("Image");
                if (!string.IsNullOrWhiteSpace(imagePath)) return 0; // resolved later from the atlas
                var text = section.Get("$Text");
                if (!string.IsNullOrWhiteSpace(text)) return Math.Max(8, GameData.ResolveString(text).Length * 12);
                return 0;
            }
            double ElementSizeH(IniSection section)
            {
                var own = Intrinsic(section, 0);
                return own.H > 0 ? own.H : (string.IsNullOrWhiteSpace(section.Get("$Text")) ? 0 : 16);
            }

            var sizeCache = new Dictionary<string, (double W, double H)>(StringComparer.OrdinalIgnoreCase);
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
            var tabX = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
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

            // HandleType 3 (list) and 6 (auto-newline row, the type
            // FormatAllItemPosByAutoNewLine checks for) lay their items out themselves:
            // items flow left-to-right, wrap when the next one would exceed the
            // container width, then each row is aligned by HAlign/VAlign. The authored
            // Left/PosType of an item is ignored.
            var listPos = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);
            foreach (var list in ini.Sections)
            {
                var handleType = list.GetInt("HandleType");
                if (handleType != 3 && handleType != 6) continue;
                double listW = SizeOf(list).W;
                if (listW <= 0) continue;
                // Invisible alternatives (Alpha=0 state variants) do not take list space.
                var items = ini.Sections.Where(s => !ReferenceEquals(s, list) &&
                    string.Equals(s.Get("._Parent"), list.Name, StringComparison.OrdinalIgnoreCase) &&
                    s.GetInt("Alpha", 255) > 0 &&
                    !(s.Get("Visible") != null && s.GetBool("Visible") == false)).ToList();
                if (items.Count == 0) continue;

                double rowSpacing = list.GetInt("RowSpacing");
                int hAlign = list.GetInt("HAlign");
                int vAlign = list.GetInt("VAlign");

                var rows = new List<List<IniSection>>();
                var rowWidths = new List<double>();
                var rowHeights = new List<double>();
                var currentRow = new List<IniSection>();
                double cursor = 0, rowHeight = 0;
                foreach (var item in items)
                {
                    var size = SizeOf(item);
                    if (currentRow.Count > 0 && cursor + size.W > listW + 0.5)
                    {
                        rows.Add(currentRow); rowWidths.Add(cursor); rowHeights.Add(rowHeight);
                        currentRow = new List<IniSection>(); cursor = 0; rowHeight = 0;
                    }
                    currentRow.Add(item);
                    cursor += size.W;
                    rowHeight = Math.Max(rowHeight, size.H);
                }
                rows.Add(currentRow); rowWidths.Add(cursor); rowHeights.Add(rowHeight);

                double y = 0;
                for (int r = 0; r < rows.Count; r++)
                {
                    double start = hAlign switch
                    {
                        1 => (listW - rowWidths[r]) / 2,
                        2 => listW - rowWidths[r],
                        _ => 0,
                    };
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

            var absPos = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase)
            {
                [rootSection.Name] = (0, 0),
            };
            // Previous sibling per parent, in INI order (the engine's item list order),
            // which PosType 7/9 anchor against.
            var prevSibling = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lastByParent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in ini.Sections)
            {
                var parent = section.Get("._Parent") ?? "";
                if (lastByParent.TryGetValue(parent, out var last) && last != section.Name)
                    prevSibling[section.Name] = last;
                lastByParent[parent] = section.Name;
            }
            foreach (var section in ini.Sections.OrderBy(Depth))
            {
                if (!result.Elements.TryGetValue(section.Name, out var element)) continue;
                if (element == result.Root) continue;
                var parentName = section.Get("._Parent");
                if (string.IsNullOrWhiteSpace(parentName)) continue;
                if (!result.Elements.TryGetValue(parentName, out var parent) || parent is not Canvas parentCanvas) continue;
                var parentSection = result.Sections[parentName];
                var parentAbs = absPos.TryGetValue(parentName, out var pa) ? pa : (0, 0);
                Attach(parentCanvas, parentSection, element, section, buttonSlots, rootWidth, rootHeight, SizeOf, tabX, listPos, parentAbs, prevSibling, result);
                double ax = Canvas.GetLeft(element); if (double.IsNaN(ax)) ax = 0;
                double ay = Canvas.GetTop(element); if (double.IsNaN(ay)) ay = 0;
                absPos[section.Name] = (parentAbs.X + ax, parentAbs.Y + ay);
            }
            return result;
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

        private static FrameworkElement CreateElement(IniSection section, UiTexCache textures, bool isParent = false)
        {
            var type = section.Get("._WndType") ?? "";
            if (SkipTypes.Contains(type)) return null;

            if (ContainerTypes.Contains(type))
            {
                var container = new Canvas();
                var width = section.GetInt("Width");
                var height = section.GetInt("Height");
                if (width > 0) container.Width = width;
                if (height > 0) container.Height = height;
                if (Wireframe)
                {
                    container.Background = new SolidColorBrush(Color.FromArgb(16, 0x6F, 0xC0, 0xEF));
                    container.ToolTip = section.Name;
                }
                return container;
            }

            if (type == "Image" || type == "WndButton" || type == "WndCheckBox")
            {
                var imagePath = section.Get("Image");
                var width = section.GetInt("Width");
                var height = section.GetInt("Height");
                var frame = type switch
                {
                    // Buttons render their normal-state frame group; Frame is only a fallback.
                    "WndButton" => FrameOrGroup(section, "NormalGroup", textures, imagePath),
                    "WndCheckBox" => FrameOrGroup(section, "UnCheckAndEnable", textures, imagePath),
                    // Frame=-1 is authored for state/runtime-driven frames; default to 0.
                    _ => Math.Max(0, section.GetInt("Frame", 0)),
                };
                var autoSize = section.GetBool("AutoSize");
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    // No atlas: keep the authored size, drop truly empty spacers unless wireframe.
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
                    // Atlas/frame missing: the engine still lays the element out at the
                    // authored size (or the atlas frame size when it later loads).
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
                FrameworkElement visual;
                if (imageType == 16)
                {
                    // 琉璃·境 frosted glass: the frame is a white/gradient mask the engine
                    // uses as a blur+translucency shape (PanelBg frame 6 = solid fill,
                    // frame 4 = soft-edged popup). WPF has no backdrop blur offscreen, so
                    // approximate with the 琉璃 panel tone masked by the frame's alpha.
                    double gw = drawWidth > 0 ? drawWidth : source.PixelWidth;
                    double gh = drawHeight > 0 ? drawHeight : source.PixelHeight;
                    var glass = new Border
                    {
                        Width = gw,
                        Height = gh,
                        Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x2E, 0x3B, 0x49)),
                        OpacityMask = new ImageBrush(source) { Stretch = Stretch.Fill },
                    };
                    visual = glass;
                }
                else if (imageType == 10 && source != null && drawWidth > 0 && drawHeight > 0)
                {
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
                        return isParent ? CreateFallbackContainer(section, true) : null;
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
                var block = new TextBlock
                {
                    Text = text.Replace("\\n", "\n").Replace("\\t", "\t"),
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
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
                if (vAlign == 1 && height > 0 && !block.Text.Contains("\n"))
                {
                    block.LineHeight = height;
                    block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                }
                if (Wireframe) block.ToolTip = section.Name;

                // The engine draws glyphs even when they outgrow the authored box.
                // A fixed WPF Width would clip them, so labels that overflow modestly
                // get a sized canvas host and are allowed to spill; long marquee/tip
                // strings keep their authored box.
                double textWidth = MeasureTextWidth(block, text, fontSize);
                if (width > 0 && textWidth > width + 0.5 && textWidth <= width * 1.6)
                {
                    var box = new Canvas { Width = width, Height = height > 0 ? height : 0 };
                    double offset = hAlign == 1 ? (width - textWidth) / 2
                        : hAlign == 2 ? width - textWidth : 0;
                    Canvas.SetLeft(block, offset);
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
        /// Engine AnchorArgs: "parentSide,selfSide,dx,dy" 鈥?align a point on this element
        /// to the matching point on the parent, then apply the pixel offsets.
        /// </summary>
        private static bool TryAnchorArgs(IniSection section, double parentWidth, double parentHeight, double elementWidth, double elementHeight, out double left, out double top)
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
            left = parentWidth * parentFraction.X - elementWidth * selfFraction.X + dx;
            top = parentHeight * parentFraction.Y - elementHeight * selfFraction.Y + dy;
            return true;
        }

        private static void Attach(Canvas parent, IniSection parentSection, FrameworkElement element, IniSection section, HashSet<string> buttonSlots, double rootWidth, double rootHeight, Func<IniSection, (double W, double H)> sizeOf, Dictionary<string, double> tabX = null, Dictionary<string, (double X, double Y)> listPos = null, (double X, double Y) parentAbs = default, Dictionary<string, string> prevSibling = null, UiBuildResult build = null)
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

            if (TryAnchorArgs(section, parentWidth, parentHeight, elementWidth, elementHeight, out var anchorLeft, out var anchorTop))
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
                case 10: // right-center aligned in the parent
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
                case 8: // right-aligned in the window when no offset is authored
                    // Tiny auto-size parents (e.g. MapQueue's Handle_Dots) mean "keep
                    // the authored origin"; window-right would park them at the edge.
                    if (left == 0 && rootWidth > 0 && parentWidth >= 32)
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

            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
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


