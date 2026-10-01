using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MapUiApp.Engine;
using UiProcessApp.Engine;

namespace UiProcessApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Contains("--selftest"))
            {
                var exit = RunSelfTest();
                Shutdown(exit);
                return;
            }
            if (e.Args.Contains("--render"))
            {
                var exit = RunRender(e.Args);
                Shutdown(exit);
                return;
            }
            if (e.Args.Contains("--frame"))
            {
                var exit = RunFrame(e.Args);
                Shutdown(exit);
                return;
            }
            if (e.Args.Contains("--fonttest"))
            {
                var exit = RunFontTest(e.Args);
                Shutdown(exit);
                return;
            }
            if (e.Args.Contains("--audit"))
            {
                var exit = RunAudit(e.Args);
                Shutdown(exit);
                return;
            }
            base.OnStartup(e);
        }

        /// <summary>
        /// Debug: dump one atlas frame to PNG so frame/group resolution can be checked.
        ///   UiProcessApp.exe --frame Button.UITex g18 --out frame.png
        /// </summary>
        private static int RunFrame(string[] args)
        {
            try
            {
                string name = null, spec = null, outPath = null;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "--frame") name = args[i + 1];
                    else if (args[i] == "--out") outPath = args[i + 1];
                    else if (args[i] == "--index") spec = args[i + 1];
                }
                if (name == null) throw new ArgumentException("--frame needs a UITex name");
                spec ??= "0";
                Paths.Locate();
                var roots = Paths.ResolveRoots();
                string file = null;
                foreach (var root in roots)
                    foreach (var candidate in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                        if (string.Equals(Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase))
                        { file = candidate; break; }
                if (file == null) throw new FileNotFoundException($"UITex '{name}' not found");

                var assets = new AssetResolver(roots);
                var tex = new UiTex(file, assets);
                int index;
                string label;
                if (spec.StartsWith("g", StringComparison.OrdinalIgnoreCase))
                {
                    var group = int.Parse(spec.Substring(1));
                    index = tex.GetGroupFrame(group);
                    label = $"group {group} -> frame {index}";
                }
                else
                {
                    index = int.Parse(spec);
                    label = $"frame {index}";
                }
                var source = tex.GetFrame(index);
                if (source == null) throw new InvalidOperationException($"{label}: no bitmap");
                var host = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
                    Width = source.PixelWidth,
                    Height = source.PixelHeight,
                    Child = new System.Windows.Controls.Image { Source = source, Stretch = Stretch.None },
                };
                host.Measure(new Size(source.PixelWidth, source.PixelHeight));
                host.Arrange(new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                var bitmap = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                outPath ??= Path.Combine(AppContext.BaseDirectory, $"frame_{index}.png");
                using var stream = File.Create(outPath);
                encoder.Save(stream);
                File.WriteAllText(outPath + ".txt",
                    $"{name}: frames={tex.Frames.Length} groups={tex.Groups.Length} {label} size={source.PixelWidth}x{source.PixelHeight}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("frame dump failed: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// Offscreen render for verification:
        ///   UiProcessApp.exe --render &lt;windowId&gt; [--page Page_X] [--out file.png] [--wire]
        /// </summary>
        private static int RunRender(string[] args)
        {
            try
            {
                string windowId = null, page = null, outPath = null, hide = null, only = null, dump = null;
                bool wire = args.Contains("--wire");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "--render") windowId = args[i + 1];
                    else if (args[i] == "--page") page = args[i + 1];
                    else if (args[i] == "--out") outPath = args[i + 1];
                    else if (args[i] == "--hide") hide = args[i + 1];
                    else if (args[i] == "--only") only = args[i + 1];
                    else if (args[i] == "--dump") dump = args[i + 1];
                }
                if (windowId == null) throw new ArgumentException("--render needs a window id");

                Paths.Locate();
                var inventoryPath = Path.Combine(AppContext.BaseDirectory, "Data", "ui_inventory.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(inventoryPath), options);
                WindowInfo window = null;
                foreach (var stage in inventory.Stages)
                    foreach (var w in stage.Windows ?? new List<WindowInfo>())
                        if (string.Equals(w.Id, windowId, StringComparison.OrdinalIgnoreCase)) window = w;
                if (window?.Path == null) throw new ArgumentException($"window '{windowId}' has no layout");

                var rel = window.Path.Replace('/', Path.DirectorySeparatorChar);
                var iniPath = window.Root == "pak"
                    ? Path.Combine(Paths.PakRoot, rel)
                    : Path.Combine(Paths.AppRoot, "assets", "ui", rel);
                var ini = IniFile.Load(iniPath);
                var effectivePage = page ?? window.Page;
                PageState pageState = null;
                if (window.Pages != null && window.Pages.Count > 0)
                {
                    pageState = window.Pages.FirstOrDefault(pg =>
                            string.Equals(pg.Id, effectivePage, StringComparison.OrdinalIgnoreCase)) ?? window.Pages[0];
                    effectivePage = null; // viewer pages are state, not INI Page_* tabs
                }
                var plan = LayoutPlanBuilder.Build(ini, effectivePage);
                LayoutPlanBuilder.ApplyHide(plan.Filtered, hide ?? window.Hide);
                LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
                LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
                LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, effectivePage);
                LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, LoadTemplateIni);
                LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, ScriptShown(window));
                LayoutPlanBuilder.ApplyOnly(plan.Filtered, only);
                LayoutPlanBuilder.ApplyTexts(plan.Filtered, window.Texts);
                LayoutPlanBuilder.ApplyImages(plan.Filtered, window.Images);
                LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, window.Adjust);
                if (pageState != null)
                {
                    LayoutPlanBuilder.ApplyTexts(plan.Filtered, pageState.Texts);
                    LayoutPlanBuilder.ApplyImages(plan.Filtered, pageState.Images);
                    LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, pageState.Adjust);
                    if (pageState.Hide != null && pageState.Hide.Count > 0)
                        LayoutPlanBuilder.ApplyHide(plan.Filtered, string.Join(",", pageState.Hide));
                }
                LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
                var resolverRoot = Paths.ProofUiRoot ?? Path.Combine(Paths.AppRoot, "assets", "ui");
                var assets = new AssetResolver(Paths.ResolveRoots());
                var textures = new UiTexCache(assets);
                UiLayout.Wireframe = wire;
                var build = UiLayout.Build(plan.Filtered, assets, textures);
                var overlayRoot = BuildOverlayVisual(window, assets, textures, wire);

                double width = plan.Filtered.Sections[0].GetInt("Width");
                double height = plan.Filtered.Sections[0].GetInt("Height");
                if (width <= 0) width = 1280;
                if (height <= 0) height = 720;

                FrameworkElement composedRoot = build.Root;
                if (overlayRoot != null)
                {
                    var grid = new Grid { Width = width, Height = height };
                    grid.Children.Add(overlayRoot);
                    grid.Children.Add(build.Root);
                    composedRoot = grid;
                }

                // Window placement on the client (inventory offsetX/offsetY): shift
                // only the main window; the overlay stays at (0,0) — the client shows
                // the MiddleMap below the WorldMap band (WorldMap at the client top).
                double offsetX = window.OffsetX ?? 0;
                double offsetY = window.OffsetY ?? 0;
                if (offsetX != 0 || offsetY != 0)
                {
                    build.Root.Margin = new Thickness(offsetX, offsetY, 0, 0);
                    build.Root.HorizontalAlignment = HorizontalAlignment.Left;
                    build.Root.VerticalAlignment = VerticalAlignment.Top;
                    width += Math.Max(0, offsetX);
                    height += Math.Max(0, offsetY);
                    if (composedRoot is Grid offsetGrid)
                    {
                        offsetGrid.Width = width;
                        offsetGrid.Height = height;
                    }
                }

                var host = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
                    Width = width,
                    Height = height,
                    Child = composedRoot,
                };
                host.Measure(new Size(width, height));
                host.Arrange(new Rect(0, 0, width, height));
                host.UpdateLayout();

                // The client draws window children without clipping them, so decorative
                // art the INI authors just outside the window rect (RevivePanel's icon,
                // PVPShowPanel's title, settlement logos) is visible in game. Grow the
                // render canvas by that overhang; parked off-window elements (overhang
                // beyond the limit) stay excluded.
                var overhang = ComputeOverhang(build, width, height);
                if (overhang.L > 0 || overhang.T > 0 || overhang.R > 0 || overhang.B > 0)
                {
                    var expanded = new Canvas
                    {
                        Width = width + overhang.L + overhang.R,
                        Height = height + overhang.T + overhang.B,
                        Background = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
                    };
                    host.Child = null;
                    Canvas.SetLeft(composedRoot, overhang.L);
                    Canvas.SetTop(composedRoot, overhang.T);
                    expanded.Children.Add(composedRoot);
                    host.Child = expanded;
                    host.Width = expanded.Width;
                    host.Height = expanded.Height;
                    host.Measure(new Size(expanded.Width, expanded.Height));
                    host.Arrange(new Rect(0, 0, expanded.Width, expanded.Height));
                    host.UpdateLayout();
                }

                if (!string.IsNullOrWhiteSpace(dump))
                {
                    var lines = new List<string>();
                    foreach (var pair in build.Elements)
                    {
                        var element = pair.Value;
                        string text = null;
                        if (element is TextBlock tb) text = tb.Text;
                        else if (element is Canvas canvas && canvas.Children.Count == 1 && canvas.Children[0] is TextBlock inner) text = inner.Text;
                        try
                        {
                            var point = element.TransformToAncestor(build.Root).Transform(new Point(0, 0));
                            lines.Add($"{pair.Key}\tx={point.X:F0}\ty={point.Y:F0}\tw={element.ActualWidth:F0}\th={element.ActualHeight:F0}\t{text ?? ""}");
                        }
                        catch
                        {
                            lines.Add($"{pair.Key}\t(not in tree)\t{text ?? ""}");
                        }
                    }
                    File.WriteAllLines(dump, lines);
                }

                var bitmap = new RenderTargetBitmap((int)host.Width, (int)host.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                outPath ??= Path.Combine(AppContext.BaseDirectory, $"render_{windowId}.png");
                using (var stream = File.Create(outPath)) encoder.Save(stream);
                Console.WriteLine($"rendered {windowId} page={pageState?.Id ?? effectivePage ?? "(all)"} sections={plan.Filtered.Sections.Count} " +
                                  $"art={(Paths.ProofUiRoot != null ? "on" : "missing")} -> {outPath}");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "render_error.txt"), ex.ToString());
                Console.WriteLine("render failed: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// Union of the visible art/text elements that stick out of the window rect
        /// (clamped so parked off-window elements are ignored). Used to grow the
        /// render canvas like the unclipped client does.
        /// </summary>
        internal static (double L, double T, double R, double B) ComputeOverhang(UiBuildResult build, double width, double height)
        {
            const double limit = 48;
            double l = 0, t = 0, r = 0, b = 0;
            foreach (var pair in build.Elements)
            {
                if (!build.Sections.TryGetValue(pair.Key, out var section)) continue;
                var type = section.Get("._WndType") ?? "";
                if (type != "Image" && type != "Text" && type != "WndButton" && type != "WndCheckBox") continue;
                var element = pair.Value;
                if (element.Visibility != Visibility.Visible) continue;
                try
                {
                    var p = element.TransformToAncestor(build.Root).Transform(new Point(0, 0));
                    double w = element.ActualWidth, h = element.ActualHeight;
                    if (w <= 0 && h <= 0) continue;
                    double dl = -p.X, dt = -p.Y, dr = p.X + w - width, db = p.Y + h - height;
                    if (dl > limit || dt > limit || dr > limit || db > limit) continue;
                    l = Math.Max(l, dl);
                    t = Math.Max(t, dt);
                    r = Math.Max(r, dr);
                    b = Math.Max(b, db);
                }
                catch { }
            }
            return (Math.Max(0, l), Math.Max(0, t), Math.Max(0, r), Math.Max(0, b));
        }

        /// <summary>
        /// Debug: render the same label with the shipped font file and the system
        /// fallback side by side to verify font resolution.
        ///   UiProcessApp.exe --fonttest [--out file.png]
        /// </summary>
        private static int RunFontTest(string[] args)
        {
            try
            {
                string outPath = null;
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "--out") outPath = args[i + 1];
                Paths.Locate();
                var assets = new AssetResolver(Paths.ResolveRoots());
                var path = assets.Resolve("ui/Font/fzht_GBK.ttf") ?? assets.Resolve("ui/Font/fzxk.ttf");
                var report = new StringBuilder();
                FontFamily fileFamily = null;
                if (path != null)
                {
                    var glyph = new GlyphTypeface(new Uri(path));
                    var name = glyph.FamilyNames.Values.FirstOrDefault() ?? "?";
                    fileFamily = new FontFamily(new Uri(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar),
                                                "./" + Path.GetFileName(path) + "#" + name);
                    var tf = new Typeface(fileFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    bool ok = tf.TryGetGlyphTypeface(out var g);
                    report.AppendLine($"font={path} family={name} resolved={ok} uri={g?.FontUri}");
                }
                else report.AppendLine("font file not found");

                var stack = new StackPanel { Background = Brushes.Black };
                foreach (var family in new[] { fileFamily, new FontFamily("Microsoft YaHei UI") })
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "绝境战场",
                        FontSize = 15,
                        Foreground = Brushes.White,
                        FontFamily = family ?? new FontFamily("Microsoft YaHei UI"),
                    });
                }
                var host = new Border { Width = 260, Height = 60, Child = stack };
                host.Measure(new Size(260, 60));
                host.Arrange(new Rect(0, 0, 260, 60));
                host.UpdateLayout();
                var bitmap = new RenderTargetBitmap(260, 60, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                outPath ??= Path.Combine(AppContext.BaseDirectory, "font_test.png");
                using (var stream = File.Create(outPath)) encoder.Save(stream);
                report.AppendLine("saved " + outPath);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "font_test.txt"), report.ToString());
                Console.WriteLine(report.ToString());
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("fonttest failed: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// Full-system audit: builds every inventory window with the production pass
        /// order and reports objective issues per window (missing atlas frames,
        /// unresolved string ids, elements drawn outside the window bounds).
        ///   UiProcessApp.exe --audit [--out file.txt]
        /// </summary>
        private static int RunAudit(string[] args)
        {
            try
            {
                string outPath = null;
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "--out") outPath = args[i + 1];

                Paths.Locate();
                var inventoryPath = Path.Combine(AppContext.BaseDirectory, "Data", "ui_inventory.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(inventoryPath), options);

                var assets = new AssetResolver(Paths.ResolveRoots());
                var textures = new UiTexCache(assets);
                var report = new StringBuilder();
                int totalPlaceholders = 0, totalUnresolved = 0, totalOutOfBounds = 0;

                foreach (var stage in inventory.Stages)
                {
                    foreach (var window in stage.Windows ?? new List<WindowInfo>())
                    {
                        if (string.IsNullOrWhiteSpace(window.Path)) continue;
                        var rel = window.Path.Replace('/', Path.DirectorySeparatorChar);
                        var iniPath = window.Root == "pak"
                            ? Path.Combine(Paths.PakRoot, rel)
                            : Path.Combine(Paths.AppRoot, "assets", "ui", rel);
                        if (!File.Exists(iniPath)) continue;

                        var ini = IniFile.Load(iniPath);
                        var plan = LayoutPlanBuilder.Build(ini, window.Page);
                        LayoutPlanBuilder.ApplyHide(plan.Filtered, window.Hide);
                        LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
                        LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
                        LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, window.Page);
                        LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, LoadTemplateIni);
                        LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, ScriptShown(window));
                        LayoutPlanBuilder.ApplyTexts(plan.Filtered, window.Texts);
                        LayoutPlanBuilder.ApplyImages(plan.Filtered, window.Images);
                        LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, window.Adjust);
                        if (window.Pages != null && window.Pages.Count > 0)
                        {
                            var auditPage = window.Pages.FirstOrDefault(pg =>
                                    string.Equals(pg.Id, window.Page, StringComparison.OrdinalIgnoreCase)) ?? window.Pages[0];
                            LayoutPlanBuilder.ApplyTexts(plan.Filtered, auditPage.Texts);
                            LayoutPlanBuilder.ApplyImages(plan.Filtered, auditPage.Images);
                            LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, auditPage.Adjust);
                        }
                        LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
                        var build = UiLayout.Build(plan.Filtered, assets, textures);

                        double width = plan.Filtered.Sections[0].GetInt("Width");
                        double height = plan.Filtered.Sections[0].GetInt("Height");
                        if (width <= 0) width = 1280;
                        if (height <= 0) height = 720;

                        var host = new Border { Width = width, Height = height, Child = build.Root };
                        host.Measure(new Size(width, height));
                        host.Arrange(new Rect(0, 0, width, height));
                        host.UpdateLayout();

                        var outOfBounds = new List<string>();
                        foreach (var pair in build.Elements)
                        {
                            var element = pair.Value;
                            if (element.Visibility != Visibility.Visible) continue;
                            if (element.ActualWidth <= 0 && element.ActualHeight <= 0) continue;
                            try
                            {
                                var p = element.TransformToAncestor(build.Root).Transform(new Point(0, 0));
                                double w = element.ActualWidth, h = element.ActualHeight;
                                if (p.X < -1 || p.Y < -1 || p.X + w > width + 1 || p.Y + h > height + 1)
                                    outOfBounds.Add($"{pair.Key} ({p.X:F0},{p.Y:F0} {w:F0}x{h:F0})");
                            }
                            catch { }
                        }

                        totalPlaceholders += build.Placeholders.Count;
                        totalUnresolved += build.UnresolvedStrings.Count;
                        totalOutOfBounds += outOfBounds.Count;

                        report.AppendLine($"== {window.Id} ({window.Title}) {width:0}x{height:0} " +
                                          $"sections={plan.Filtered.Sections.Count} elements={build.Elements.Count} " +
                                          $"placeholders={build.Placeholders.Count} unresolved={build.UnresolvedStrings.Count} " +
                                          $"outOfBounds={outOfBounds.Count}");
                        foreach (var item in build.Placeholders.Take(40)) report.AppendLine("   placeholder  " + item);
                        foreach (var item in build.UnresolvedStrings.Take(40)) report.AppendLine("   unresolved   " + item);
                        foreach (var item in outOfBounds.Take(40)) report.AppendLine("   outOfBounds  " + item);
                    }
                }
                report.AppendLine($"TOTAL placeholders={totalPlaceholders} unresolved={totalUnresolved} outOfBounds={totalOutOfBounds}");

                outPath ??= Path.Combine(AppContext.BaseDirectory, "ui_process_audit.txt");
                File.WriteAllText(outPath, report.ToString());
                Console.WriteLine($"audit -> {outPath} placeholders={totalPlaceholders} unresolved={totalUnresolved} outOfBounds={totalOutOfBounds}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("audit failed: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// Sections the window script shows even though they carry
        /// LockShowAndHide=1: the mode tabs (ShowModeTabs) plus the inventory's
        /// script-shown list (e.g. the activity badges of the tab row).
        /// </summary>
        internal static IEnumerable<string> ScriptShown(WindowInfo window)
        {
            var shown = new List<string>();
            if (window?.Show != null) shown.AddRange(window.Show);
            if (window?.Tabs?.Show != null) shown.AddRange(window.Tabs.Show);
            return shown;
        }

        /// <summary>
        /// Resolves a list-item prototype INI referenced by the inventory. Prototypes
        /// live either in PakV4 (root "pak", flat names) or the extracted ui tree.
        /// </summary>
        /// <summary>
        /// Builds the optional second window drawn behind the main one (inventory
        /// `overlay`): the client shows the WorldMap behind the MiddleMap, so the
        /// reference capture is a composite (MiddleMap chrome + WorldMap top-left,
        /// region list, close). Returns null when there is no overlay.
        /// </summary>
        internal static FrameworkElement BuildOverlayVisual(WindowInfo window, AssetResolver assets,
            UiTexCache textures, bool wire)
        {
            var spec = window?.Overlay;
            if (spec == null || string.IsNullOrWhiteSpace(spec.Path)) return null;
            var rel = spec.Path.Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(Paths.AppRoot, "assets", "ui", rel);
            if (!File.Exists(path)) return null;
            var ini = IniFile.Load(path);
            var plan = LayoutPlanBuilder.Build(ini, null);
            LayoutPlanBuilder.ApplyHide(plan.Filtered, spec.Hide);
            LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, spec.Show ?? new List<string>());
            LayoutPlanBuilder.ApplyTexts(plan.Filtered, spec.Texts);
            LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, spec.Adjust);
            var build = UiLayout.Build(plan.Filtered, assets, textures);
            // Pin the overlay to the top-left of the composite grid: a fixed-size
            // root inside a taller grid would otherwise be centered by WPF.
            build.Root.HorizontalAlignment = HorizontalAlignment.Left;
            build.Root.VerticalAlignment = VerticalAlignment.Top;
            return build.Root;
        }

        internal static IniFile LoadTemplateIni(string relative)
        {            var rel = relative.Replace('/', Path.DirectorySeparatorChar);
            var candidates = new[]
            {
                Path.Combine(Paths.PakRoot, rel),
                Path.Combine(Paths.AppRoot, "assets", "ui", rel),
                Path.Combine(Paths.AppRoot, "assets", "ui", "Config", "Default", Path.GetFileName(rel)),
            };
            foreach (var candidate in candidates)
                if (File.Exists(candidate)) return IniFile.Load(candidate);
            throw new FileNotFoundException($"list template ini '{relative}' not found");
        }

        /// <summary>
        /// Headless verification: loads the inventory, then builds every window layout
        /// that has an extracted KGUI INI. Writes ui_process_selftest.txt next to the exe.
        /// </summary>
        private static int RunSelfTest()
        {
            var report = new StringBuilder();
            var ok = true;
            try
            {
                Paths.Locate();
                report.AppendLine($"AppRoot   = {Paths.AppRoot}");
                report.AppendLine($"RepoRoot  = {Paths.RepoRoot}");
                report.AppendLine($"UiRoot    = {Paths.UiRoot}");
                report.AppendLine($"PakRoot   = {Paths.PakRoot}");
                report.AppendLine($"Strings   = {Strings.Table.Count}");

                var inventoryPath = Path.Combine(AppContext.BaseDirectory, "Data", "ui_inventory.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(inventoryPath), options);

                var resolverRoot = Paths.ProofUiRoot ?? Path.Combine(Paths.AppRoot, "assets", "ui");
                var assets = new AssetResolver(Paths.ResolveRoots());
                var textures = new UiTexCache(assets);

                int rendered = 0, skipped = 0, failed = 0;
                foreach (var stage in inventory.Stages)
                {
                    foreach (var window in stage.Windows ?? new List<WindowInfo>())
                    {
                        if (string.IsNullOrWhiteSpace(window.Path)) { skipped++; continue; }
                        var rel = window.Path.Replace('/', Path.DirectorySeparatorChar);
                        var iniPath = window.Root == "pak"
                            ? Path.Combine(Paths.PakRoot, rel)
                            : Path.Combine(Paths.AppRoot, "assets", "ui", rel);
                        if (!File.Exists(iniPath))
                        {
                            failed++;
                            report.AppendLine($"FAIL missing ini: {window.Id} -> {iniPath}");
                            continue;
                        }
                        try
                        {
                            var ini = IniFile.Load(iniPath);
                            var plan = LayoutPlanBuilder.Build(ini, window.Page);
                            LayoutPlanBuilder.ApplyHide(plan.Filtered, window.Hide);
                            LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
                            LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
                LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, window.Page);
                            LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, LoadTemplateIni);
                            LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, ScriptShown(window));
                            LayoutPlanBuilder.ApplyTexts(plan.Filtered, window.Texts);
                            LayoutPlanBuilder.ApplyImages(plan.Filtered, window.Images);
                            LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, window.Adjust);
                            if (window.Pages != null && window.Pages.Count > 0)
                            {
                                var selfPage = window.Pages.FirstOrDefault(pg =>
                                        string.Equals(pg.Id, window.Page, StringComparison.OrdinalIgnoreCase)) ?? window.Pages[0];
                                LayoutPlanBuilder.ApplyTexts(plan.Filtered, selfPage.Texts);
                                LayoutPlanBuilder.ApplyImages(plan.Filtered, selfPage.Images);
                                LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, selfPage.Adjust);
                            }
                            LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
                            var build = UiLayout.Build(plan.Filtered, assets, textures);
                            rendered++;
                            report.AppendLine($"OK   {window.Id,-22} sections={plan.Filtered.Sections.Count,-5} " +
                                              $"elements={build.Elements.Count,-5} {window.Title}");
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            report.AppendLine($"FAIL {window.Id}: {ex.Message}");
                        }
                    }
                }
                report.AppendLine($"rendered={rendered} skipped={skipped} failed={failed}");
                ok = failed == 0 && rendered > 0;
            }
            catch (Exception ex)
            {
                report.AppendLine("FATAL " + ex);
                ok = false;
            }

            var outPath = Path.Combine(AppContext.BaseDirectory, "ui_process_selftest.txt");
            File.WriteAllText(outPath, report.ToString());
            return ok ? 0 : 1;
        }
    }
}
