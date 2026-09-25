using System;
using System.Collections.Generic;
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

        // The extracted atlases and the INI layout come from different client builds:
        // some INI frame-group ids exceed the atlas' group table, so these sections pin
        // the frame the artwork actually lives in.
        private static readonly Dictionary<string, int> FrameOverrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "CheckBox_MapPage", 38 }, // MapWindow6.UITex tab background, highlighted (checked)
        };

        public static UiBuildResult Build(IniFile ini, AssetResolver assets, UiTexCache textures)
        {
            var result = new UiBuildResult();
            if (ini.Sections.Count == 0) throw new InvalidOperationException("Layout INI has no sections");
            var rootSection = ini.Sections[0];

            foreach (var section in ini.Sections)
            {
                if (section != rootSection && SkipSections.Contains(section.Name)) continue;
                var element = CreateElement(section, textures);
                if (element == null) continue;
                result.Elements[section.Name] = element;
                result.Sections[section.Name] = section;
            }

            result.Root = result.Elements[rootSection.Name];
            var buttonSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootWidth = (double)rootSection.GetInt("Width");
            var rootHeight = (double)rootSection.GetInt("Height");
            foreach (var section in ini.Sections)
            {
                if (!result.Elements.TryGetValue(section.Name, out var element)) continue;
                if (element == result.Root) continue;
                var parentName = section.Get("._Parent");
                if (string.IsNullOrWhiteSpace(parentName)) continue;
                if (!result.Elements.TryGetValue(parentName, out var parent) || parent is not Canvas parentCanvas) continue;
                var parentSection = result.Sections[parentName];
                Attach(parentCanvas, parentSection, element, section, buttonSlots, rootWidth, rootHeight);
            }
            return result;
        }

        private static FrameworkElement CreateElement(IniSection section, UiTexCache textures)
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
                    _ => section.GetInt("Frame", 0),
                };
                if (FrameOverrides.TryGetValue(section.Name, out var pinnedFrame)) frame = pinnedFrame;
                var autoSize = section.GetBool("AutoSize");
                if (width <= 0 && height <= 0 && !autoSize)
                {
                    var hidden = new Canvas { Visibility = Visibility.Collapsed };
                    return hidden;
                }
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    var placeholder = new Canvas();
                    if (width > 0) placeholder.Width = width;
                    if (height > 0) placeholder.Height = height;
                    return placeholder;
                }
                var source = textures.GetFrame(imagePath, frame);
                var imageType = section.GetInt("ImageType");
                FrameworkElement visual;
                if (imageType == 10 && source != null && width > 0 && height > 0)
                {
                    visual = new NineSliceImage(source, width, height);
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
                    if (width > 0) image.Width = width;
                    else if (source != null) image.Width = source.PixelWidth;
                    if (height > 0) image.Height = height;
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
                    host.Children.Add(visual);
                    return host;
                }
                return visual;
            }

            if (type == "Text")
            {
                var text = section.Get("$Text");
                if (string.IsNullOrWhiteSpace(text)) return null;
                text = GameData.ResolveString(text);
                var block = new TextBlock
                {
                    Text = text.Replace("\\n", "\n"),
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    FontSize = 12,
                    Foreground = Brushes.White,
                };
                var width = section.GetInt("Width");
                var height = section.GetInt("Height");
                if (width > 0) block.Width = width;
                if (height > 0) block.Height = height;
                var hAlign = section.GetInt("HAlign");
                if (hAlign == 1) block.TextAlignment = TextAlignment.Center;
                else if (hAlign == 2) block.TextAlignment = TextAlignment.Right;
                var vAlign = section.GetInt("VAlign");
                if (vAlign == 1 && height > 0 && !block.Text.Contains("\n"))
                {
                    block.LineHeight = height;
                    block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                }
                return block;
            }

            return null;
        }

        private static int FrameOrGroup(IniSection section, string groupKey, UiTexCache textures, string imagePath)
        {
            var group = section.GetInt(groupKey, -1);
            if (group >= 0 && !string.IsNullOrWhiteSpace(imagePath)) return textures.GetGroupFrame(imagePath, group);
            if (group >= 0) return group + 1;
            return section.GetInt("Frame", 0);
        }

        private static void Attach(Canvas parent, IniSection parentSection, FrameworkElement element, IniSection section, HashSet<string> buttonSlots)
        {
            double left = section.GetInt("Left");
            double top = section.GetInt("Top");
            double parentWidth = parentSection.GetInt("Width");
            double parentHeight = parentSection.GetInt("Height");
            double elementWidth = double.IsNaN(element.Width) ? 0 : element.Width;
            double elementHeight = double.IsNaN(element.Height) ? 0 : element.Height;

            switch (section.GetInt("PosType"))
            {
                case 6: // centered on the given point (player markers)
                    left -= elementWidth / 2;
                    top -= elementHeight / 2;
                    break;
                case 7: // right-aligned in the parent
                    if (left == 0 && parentWidth > 0) left = parentWidth - elementWidth;
                    break;
                case 8: // bottom-aligned in the parent
                    if (top == 0 && parentHeight > 0) top = parentHeight - elementHeight;
                    break;
                case 12: // centered horizontally, bottom-aligned
                    if (parentWidth > 0) left = (parentWidth - elementWidth) / 2;
                    if (parentHeight > 0) top = parentHeight - elementHeight;
                    break;
                case 9: // flows to the right of the previous sibling
                    if (parent.Children.Count > 0)
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

            var alpha = section.GetInt("Alpha", 255);
            if (alpha < 255 && section.Name != "Image_Map") element.Opacity = Math.Max(0, alpha) / 255.0;

            if (section.GetBool("Visible") == false && section.Get("Visible") != null) element.Visibility = Visibility.Collapsed;
            if (HiddenSections.Contains(section.Name) || section.Name.StartsWith("Image_Black", StringComparison.OrdinalIgnoreCase))
                element.Visibility = Visibility.Collapsed;

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
    }
}
