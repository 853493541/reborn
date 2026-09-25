using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    /// <summary>Diagnostics: which atlas frames resolve, and dumps of key frames.</summary>
    public static class Probe
    {
        public static void Run(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var assets = new AssetResolver(GameData.UiRoot);
            var textures = new UiTexCache(assets);
            var lines = new List<string>();
            var inis = new[]
            {
                ("MiniMap", Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "MiniMap.ini")),
                ("MiddleMap", Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "MiddleMap.ini")),
                ("BattleFieldMap", Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "BattleField", "BattleFieldMap.ini")),
            };

            foreach (var (name, path) in inis)
            {
                if (!File.Exists(path))
                {
                    lines.Add($"{name}: INI MISSING {path}");
                    continue;
                }
                var ini = IniFile.Load(path);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var section in ini.Sections)
                {
                    var image = section.Get("Image");
                    if (string.IsNullOrWhiteSpace(image)) continue;
                    var frame = section.GetInt("Frame", 0);
                    if (!seen.Add(image + "#" + frame)) continue;
                    var file = assets.Resolve(image);
                    var tex = textures.Get(image);
                    var texture = tex?.Texture;
                    var source = textures.GetFrame(image, frame);
                    lines.Add($"{name} | {section.Name} | {image} | frame {frame} | imageType {section.GetInt("ImageType")} | target {section.GetInt("Width")}x{section.GetInt("Height")} | file {(file != null ? "ok" : "MISSING")} | frames {(tex != null ? tex.Frames.Length.ToString() : "-")} | texture {(texture != null ? texture.PixelWidth + "x" + texture.PixelHeight : "NONE")} | used {(source != null ? source.PixelWidth + "x" + source.PixelHeight : "EMPTY")}");
                }
            }

            var lensBuild = UiLayout.Build(IniFile.Load(Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "MiniMap.ini")), assets, textures);
            System.Windows.Controls.Canvas.SetLeft(lensBuild.Root, 1660);
            System.Windows.Controls.Canvas.SetTop(lensBuild.Root, 0);
            ReportLayout(lensBuild, "LENS", lines);

            var middleBuild = UiLayout.Build(IniFile.Load(Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "MiddleMap.ini")), assets, textures);
            System.Windows.Controls.Canvas.SetLeft(middleBuild.Root, 41);
            System.Windows.Controls.Canvas.SetTop(middleBuild.Root, 55);
            ReportLayout(middleBuild, "MIDDLE", lines);
            foreach (var name in new[] { "Image_Search", "Image_Alpha", "Image_Center", "Image_Map" })
            {
                if (!middleBuild.Elements.TryGetValue(name, out var element)) { lines.Add($"ELEMENT {name} MISSING"); continue; }
                var image = element as System.Windows.Controls.Image;
                var source = image?.Source as BitmapSource;
                lines.Add($"ELEMENT {name} | type {element.GetType().Name} | source {(source != null ? source.PixelWidth + "x" + source.PixelHeight : "NONE")} | section frame {middleBuild.Sections[name].GetInt("Frame")} | image {middleBuild.Sections[name].Get("Image")}");
                if (source != null)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    using var stream = File.Create(Path.Combine(outputDirectory, $"element-{name}.png"));
                    encoder.Save(stream);
                }
            }

            var battleBuild = UiLayout.Build(IniFile.Load(Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "BattleField", "BattleFieldMap.ini")), assets, textures);
            System.Windows.Controls.Canvas.SetLeft(battleBuild.Root, 785);
            System.Windows.Controls.Canvas.SetTop(battleBuild.Root, 298);
            ReportLayout(battleBuild, "BATTLE", lines);

            var chromeBuild = UiLayout.Build(IniFile.Load(Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "MiddleMap.ini")), assets, textures);
            if (chromeBuild.Elements.TryGetValue("Image_Map", out var mapSlot)) mapSlot.Visibility = Visibility.Collapsed;
            var chromeHost = new System.Windows.Controls.Canvas { Width = 1410, Height = 890 };
            System.Windows.Controls.Canvas.SetLeft(chromeBuild.Root, 0);
            System.Windows.Controls.Canvas.SetTop(chromeBuild.Root, 0);
            chromeHost.Children.Add(chromeBuild.Root);
            chromeHost.Measure(new Size(1410, 890));
            chromeHost.Arrange(new Rect(0, 0, 1410, 890));
            chromeHost.UpdateLayout();
            SaveVisual(Path.Combine(outputDirectory, "middlemap-chrome.png"), chromeHost, 1410, 890);

            var bundle = GameData.LoadMap("longmen-day");
            lines.Add($"MAP longmen-day | middlemap {(bundle.Middlemap != null ? bundle.Middlemap.PixelWidth + "x" + bundle.Middlemap.PixelHeight : "NULL")} | areas {bundle.Areas.Count} | config {bundle.Config.Count} | logical {bundle.Width}x{bundle.Height} | scale {bundle.Scale} | textroot {GameData.TextRoot}");

            var view = new MapViewport(new UiTexCache(assets), 928, 812);
            view.SetMap(bundle);
            view.Measure(new Size(928, 812));
            view.Arrange(new Rect(0, 0, 928, 812));
            view.UpdateLayout();
            var viewBitmap = new RenderTargetBitmap(928, 812, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            viewBitmap.Render(view);
            var viewEncoder = new PngBitmapEncoder();
            viewEncoder.Frames.Add(BitmapFrame.Create(viewBitmap));
            using (var viewStream = File.Create(Path.Combine(outputDirectory, "viewport-map.png"))) viewEncoder.Save(viewStream);

            var battle = new BattleView(new UiTexCache(assets), 300, 263);
            battle.SetMap(bundle);
            battle.Measure(new Size(300, 263));
            battle.Arrange(new Rect(0, 0, 300, 263));
            battle.UpdateLayout();
            SaveVisual(Path.Combine(outputDirectory, "battlefield-view.png"), battle, 300, 263);

            var mosaicFit = new System.Windows.Controls.Image
            {
                Source = GameData.LoadPng(GameData.MosaicPath),
                Width = 300,
                Height = 263,
                Stretch = System.Windows.Media.Stretch.Uniform,
            };
            mosaicFit.Measure(new Size(300, 263));
            mosaicFit.Arrange(new Rect(0, 0, 300, 263));
            SaveVisual(Path.Combine(outputDirectory, "mosaic-fit.png"), mosaicFit, 300, 263);

            DumpRaw(Path.Combine(outputDirectory, "mask-minimapsharp.png"), assets.Resolve("ui/Image/UItimate/Minimap/MinimapSharp.tga"));

            DumpFrame(textures, "ui/Image/UICommon/HelpPanel.UITex", 5, Path.Combine(outputDirectory, "frame-help-5.png"));
            DumpFrame(textures, "ui/Image/UICommon/CommonPanel.UITex", 45, Path.Combine(outputDirectory, "frame-common-45.png"));
            DumpFrame(textures, "ui/Image/UICommon/CommonPanel.UITex", 4, Path.Combine(outputDirectory, "frame-common-4.png"));
            DumpFrame(textures, "ui/Image/UICommon/CommonPanel.UITex", 66, Path.Combine(outputDirectory, "frame-common-66.png"));
            DumpFrame(textures, "ui/Image/MiddleMap/MapWindow.UITex", 32, Path.Combine(outputDirectory, "frame-mapwindow-32.png"));
            DumpFrame(textures, "ui/Image/MiddleMap/MapWindow.UITex", 33, Path.Combine(outputDirectory, "frame-mapwindow-33.png"));
            DumpFrame(textures, "ui/Image/MiddleMap/MapWindow.UITex", 34, Path.Combine(outputDirectory, "frame-mapshadow-34.png"));
            DumpFrame(textures, "ui/Image/MiddleMap/MapWindow3.UITex", 0, Path.Combine(outputDirectory, "frame-mbg3-0.png"));
            DumpFrame(textures, "ui/Image/MiddleMap/MapWindow4.UITex", 0, Path.Combine(outputDirectory, "frame-mbg4-0.png"));
            DumpFrame(textures, "ui/Image/UICommon/HelpPanel.UITex", 4, Path.Combine(outputDirectory, "frame-help-4.png"));
            DumpFrame(textures, "ui/Image/UItimate/Minimap/Minimap.UITex", 0, Path.Combine(outputDirectory, "frame-lens-bg-0.png"));
            DumpFrame(textures, "ui/Image/UItimate/Minimap/Minimap.UITex", 49, Path.Combine(outputDirectory, "frame-lens-corner-49.png"));
            DumpFrame(textures, "ui/Image/Minimap/Minimap.UITex", 22, Path.Combine(outputDirectory, "frame-btn-22.png"));
            DumpFrame(textures, "ui/Image/Minimap/BattleMinimap3.UITex", 36, Path.Combine(outputDirectory, "frame-marker-36.png"));

            ReportAtlas(textures, lines, outputDirectory, "ui/Image/MiddleMap/MapWindow6.UITex", new[] { 4, 38, 39, 40, 32, 33, 35, 37, 43, 7 });
            ReportAtlas(textures, lines, outputDirectory, "ui/Image/UItimate/UICommon/Button.UITex", new[] { 121, 122, 123 });
            ReportAtlas(textures, lines, outputDirectory, "ui/Image/Common/DialogueLabel.UITex", new[] { 108, 110, 112, 114, 116, 86 });
            ReportAtlas(textures, lines, outputDirectory, "ui/Image/UItimate/UICommon/Button4.UITex", new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            ReportAtlas(textures, lines, outputDirectory, "ui/Image/UItimate/UICommon/Common.UITex", new[] { 0, 1 });
            ReportAtlas(textures, lines, outputDirectory, "ui/Image/UItimate/UICommon/MainBarPanel.UITex", new[] { 0, 1, 2, 3 });

            foreach (var probePath in new[]
            {
                "ui/Image/UItimate/UICommon/Common.UITex",
                "ui\\Image\\UItimate\\UICommon\\Common.UITex",
                "ui/Image/MiddleMap/MapWindow6.UITex",
                "ui\\Image\\MiddleMap\\MapWindow6.UITex",
            })
            {
                var resolved = assets.Resolve(probePath);
                var atlas = textures.Get(probePath);
                lines.Add($"RESOLVE {probePath} -> {resolved ?? "NULL"} | frames {(atlas != null ? atlas.Frames.Length : -1)} | groups {(atlas != null ? atlas.Groups.Length : -1)} | tex {(atlas?.Texture != null ? atlas.Texture.PixelWidth + "x" + atlas.Texture.PixelHeight : "NONE")}");
            }
            DumpFrame(textures, "ui\\Image\\UItimate\\UICommon\\Common.UITex", 0, Path.Combine(outputDirectory, "frame-search-bs-0.png"));
            DumpFrame(textures, "ui/Image/UItimate/UICommon/Common.UITex", 0, Path.Combine(outputDirectory, "frame-search-fs-0.png"));

            File.WriteAllLines(Path.Combine(outputDirectory, "probe.txt"), lines);
        }

        private static void ReportAtlas(UiTexCache textures, List<string> lines, string outputDirectory, string uit, int[] frames)
        {
            var tex = textures.Get(uit);
            if (tex == null)
            {
                lines.Add($"ATLAS {uit} | MISSING");
                return;
            }
            lines.Add($"ATLAS {uit} | frames {tex.Frames.Length} | groups {tex.Groups.Length} | tex {tex.TextureWidth}x{tex.TextureHeight}");
            for (int g = 0; g < tex.Groups.Length; g++)
            {
                lines.Add($"  group {g} -> start {tex.Groups[g].StartFrame} count {tex.Groups[g].Count} interval {tex.Groups[g].Interval}");
                if (g > 40) { lines.Add("  ..."); break; }
            }
            for (int i = 0; i < tex.Frames.Length && i < 8; i++)
            {
                var f = tex.Frames[i];
                lines.Add($"  frame {i} -> {f.X},{f.Y} {f.W}x{f.H} flag {f.Flag}");
            }
            foreach (var index in frames)
            {
                if (index < 0 || index >= tex.Frames.Length) continue;
                var f = tex.Frames[index];
                lines.Add($"  wanted frame {index} -> {f.X},{f.Y} {f.W}x{f.H} flag {f.Flag}");
            }
            var name = System.IO.Path.GetFileNameWithoutExtension(uit);
            DumpFrameGrid(textures, uit, frames, System.IO.Path.Combine(outputDirectory, name + "-sheet.png"));
        }

        private static void DumpFrameGrid(UiTexCache textures, string uit, int[] frames, string path)
        {
            var sources = new List<System.Windows.Media.Imaging.BitmapSource>();
            foreach (var frame in frames)
            {
                var source = textures.GetFrame(uit, frame);
                if (source != null) sources.Add(source);
            }
            if (sources.Count == 0) return;
            int cell = 0;
            foreach (var source in sources) cell = Math.Max(cell, Math.Max(source.PixelWidth, source.PixelHeight));
            cell = Math.Max(16, cell);
            int columns = Math.Min(4, sources.Count);
            int rows = (sources.Count + columns - 1) / columns;
            var visual = new System.Windows.Controls.Canvas { Width = columns * cell, Height = rows * cell, Background = System.Windows.Media.Brushes.Magenta };
            for (int i = 0; i < sources.Count; i++)
            {
                var image = new System.Windows.Controls.Image { Source = sources[i], Stretch = System.Windows.Media.Stretch.None };
                System.Windows.Controls.Canvas.SetLeft(image, (i % columns) * cell + 1);
                System.Windows.Controls.Canvas.SetTop(image, (i / columns) * cell + 1);
                visual.Children.Add(image);
            }
            visual.Measure(new Size(visual.Width, visual.Height));
            visual.Arrange(new Rect(0, 0, visual.Width, visual.Height));
            visual.UpdateLayout();
            SaveVisual(path, visual, (int)visual.Width, (int)visual.Height);
        }

        private static void ReportLayout(UiBuildResult build, string name, List<string> lines)
        {
            foreach (var pair in build.Elements)
            {
                double x = 0, y = 0;
                System.Windows.FrameworkElement element = pair.Value;
                while (element != null)
                {
                    x += double.IsNaN(System.Windows.Controls.Canvas.GetLeft(element)) ? 0 : System.Windows.Controls.Canvas.GetLeft(element);
                    y += double.IsNaN(System.Windows.Controls.Canvas.GetTop(element)) ? 0 : System.Windows.Controls.Canvas.GetTop(element);
                    if (element == build.Root) break;
                    element = element.Parent as System.Windows.FrameworkElement;
                }
                var width = double.IsNaN(pair.Value.Width) ? 0 : pair.Value.Width;
                var height = double.IsNaN(pair.Value.Height) ? 0 : pair.Value.Height;
                var section = build.Sections[pair.Key];
                var state = pair.Value.Parent == null && pair.Value != build.Root
                    ? "orphan"
                    : pair.Value.Visibility == Visibility.Visible ? "VISIBLE" : "hidden";
                lines.Add($"{name} LAYOUT | {pair.Key} | {x:0.#},{y:0.#} | {width:0.#}x{height:0.#} | posType {section.GetInt("PosType")} | parent {section.Get("._Parent")} | {state}");
            }
        }

        private static void SaveVisual(string path, System.Windows.Media.Visual visual, int width, int height)
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        private static void DumpRaw(string path, string file)
        {
            if (file == null)
            {
                File.WriteAllText(path + ".missing.txt", "missing");
                return;
            }
            try
            {
                var source = Tga.Load(file);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using var stream = File.Create(path);
                encoder.Save(stream);
            }
            catch (Exception exception)
            {
                File.WriteAllText(path + ".error.txt", exception.Message);
            }
        }

        private static void DumpFrame(UiTexCache textures, string uit, int frame, string path)
        {
            var source = textures.GetFrame(uit, frame);
            if (source == null)
            {
                File.WriteAllText(path + ".missing.txt", "missing frame or texture");
                return;
            }
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
