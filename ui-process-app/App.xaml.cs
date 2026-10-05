using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            if (e.Args.Contains("--status"))
            {
                var exit = RunStatus(e.Args);
                Shutdown(exit);
                return;
            }
            if (e.Args.Contains("--contact-sheet"))
            {
                var exit = RunContactSheet(e.Args);
                Shutdown(exit);
                return;
            }
            if (e.Args.Contains("--click"))
            {
                var exit = RunClick(e.Args);
                Shutdown(exit);
                return;
            }
            if (e.Args.Contains("--reject"))
            {
                var exit = RunReject(e.Args);
                Shutdown(exit);
                return;
            }
            base.OnStartup(e);
        }

        /// <summary>
        /// Headless 不需要 toggle (same logic as the viewer's X key):
        ///   UiProcessApp.exe --reject &lt;windowId&gt;
        /// Moves the window into the not-needed stage (or restores it) and saves Data/rejected.tsv.
        /// </summary>
        private static int RunReject(string[] args)
        {
            try
            {
                string id = null;
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "--reject") id = args[i + 1];
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("--reject needs a window id");
                Paths.Locate();
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                Inventory inv = null;
                foreach (var candidate in new[]
                         {
                             System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "ui_inventory.json"),
                             System.IO.Path.Combine(Paths.AppRoot, "Data", "ui_inventory.json"),
                         })
                {
                    if (!System.IO.File.Exists(candidate)) continue;
                    inv = System.Text.Json.JsonSerializer.Deserialize<Inventory>(
                        System.IO.File.ReadAllText(candidate), options);
                    if (inv?.Stages != null) break;
                }
                if (inv == null) throw new System.IO.FileNotFoundException("ui_inventory.json not found");

                var rejected = RejectionStore.Load(Paths.AppRoot);
                RejectionStore.Apply(inv, rejected);
                var window = inv.Stages
                    .SelectMany(s => s.Windows ?? new List<WindowInfo>())
                    .FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));
                if (window == null)
                {
                    Console.WriteLine("reject failed: no window with id '" + id + "'");
                    return 1;
                }
                RejectionStore.Toggle(inv, window, rejected);
                RejectionStore.Save(Paths.AppRoot, rejected);
                var stage = inv.Stages.FirstOrDefault(s =>
                    string.Equals(s.Id, RejectionStore.NotNeededStageId, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine((rejected.ContainsKey(id) ? "rejected " : "restored ") + id +
                                  " | not-needed=" + (stage?.Windows?.Count ?? 0) +
                                  " | total=" + inv.Stages.Sum(s => s.Windows?.Count ?? 0));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("reject failed: " + ex.Message);
                return 1;
            }
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
                LayoutPlanBuilder.ApplyAppendIni(plan.Filtered, window.AppendIni, LoadIniTolerant);
                var runtimeApplied = LayoutPlanBuilder.ApplyRuntimeState(plan.Filtered, window.Path);
                LayoutPlanBuilder.ApplyHide(plan.Filtered, hide ?? window.Hide);
                LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
                LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
                LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, effectivePage);
                LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, LoadTemplateIni);
                LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, ScriptShown(window));
                LayoutPlanBuilder.ApplyOnly(plan.Filtered, only);
                LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
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
                    if (window.Overlay != null && window.Overlay.Front == true)
                    {
                        grid.Children.Add(build.Root);
                        grid.Children.Add(overlayRoot);
                    }
                    else
                    {
                        grid.Children.Add(overlayRoot);
                        grid.Children.Add(build.Root);
                    }
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
                    Background = BackdropBrush(window),
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
                        Background = BackdropBrush(window),
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
                                  $"runtime={runtimeApplied} art={(Paths.ProofUiRoot != null ? "on" : "missing")} -> {outPath}");
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
                        LayoutPlanBuilder.ApplyAppendIni(plan.Filtered, window.AppendIni, LoadIniTolerant);
                        LayoutPlanBuilder.ApplyRuntimeState(plan.Filtered, window.Path);
                        LayoutPlanBuilder.ApplyHide(plan.Filtered, window.Hide);
                        LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
                        LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
                        LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, window.Page);
                        LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, LoadTemplateIni);
                        LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, ScriptShown(window));
                        LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
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
        /// Compact per-window render status for the review badges (P1 of
        /// docs/ui/UI_RENDER_FIDELITY_PLAN.md):
        ///   UiProcessApp.exe --status [--out file.tsv]
        /// Writes id/stage/size/sections/elements/leaves/placeholders/unresolved/
        /// outOfBounds/pages/page/lsh/hosts/flags and prints stage totals.
        /// </summary>
        private static int RunStatus(string[] args)
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
                report.AppendLine("id\tstage\tw\th\tsections\telements\tleaves\tplaceholders\tunresolved\toutOfBounds\tpages\tpage\tlsh\thosts\tflags");

                var stageStats = new List<string>();
                int totalShell = 0, totalRuntime = 0, totalPlaced = 0;

                foreach (var stage in inventory.Stages)
                {
                    int n = 0, shell = 0, runtime = 0, placed = 0;
                    foreach (var window in stage.Windows ?? new List<WindowInfo>())
                    {
                        if (string.IsNullOrWhiteSpace(window.Path)) continue;
                        var filtered = BuildWindowPlan(window, out var ini);
                        if (filtered == null) continue;
                        var build = UiLayout.Build(filtered, assets, textures);

                        double width = filtered.Sections[0].GetInt("Width");
                        double height = filtered.Sections[0].GetInt("Height");
                        if (width <= 0) width = 1280;
                        if (height <= 0) height = 720;

                        var host = new Border { Width = width, Height = height, Child = build.Root };
                        host.Measure(new Size(width, height));
                        host.Arrange(new Rect(0, 0, width, height));
                        host.UpdateLayout();

                        int leaves = 0, outOfBounds = 0;
                        foreach (var pair in build.Elements)
                        {
                            var element = pair.Value;
                            if (element.Visibility != Visibility.Visible) continue;
                            if (element.ActualWidth <= 0 && element.ActualHeight <= 0) continue;
                            if (build.Sections.TryGetValue(pair.Key, out var section))
                            {
                                var type = section.Get("._WndType") ?? "";
                                if (type.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
                                    type.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
                                    type.Equals("WndButton", StringComparison.OrdinalIgnoreCase) ||
                                    type.Equals("WndCheckBox", StringComparison.OrdinalIgnoreCase))
                                    leaves++;
                            }
                            try
                            {
                                var p = element.TransformToAncestor(build.Root).Transform(new Point(0, 0));
                                if (p.X < -1 || p.Y < -1 ||
                                    p.X + element.ActualWidth > width + 1 ||
                                    p.Y + element.ActualHeight > height + 1)
                                    outOfBounds++;
                            }
                            catch { }
                        }

                        var parentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var section in filtered.Sections)
                        {
                            var parent = section.Get("._Parent");
                            if (!string.IsNullOrWhiteSpace(parent)) parentNames.Add(parent);
                        }
                        int hosts = 0;
                        foreach (var section in filtered.Sections)
                        {
                            var type = section.Get("._WndType") ?? "";
                            if (!(type.Equals("Handle", StringComparison.OrdinalIgnoreCase) ||
                                  type.Equals("Box", StringComparison.OrdinalIgnoreCase) ||
                                  type.Equals("WndList", StringComparison.OrdinalIgnoreCase) ||
                                  type.Equals("WndScroll", StringComparison.OrdinalIgnoreCase) ||
                                  type.Equals("WndContainer", StringComparison.OrdinalIgnoreCase) ||
                                  type.Equals("WndFlexContainer", StringComparison.OrdinalIgnoreCase)))
                                continue;
                            if (!parentNames.Contains(section.Name)) hosts++;
                        }

                        int lsh = ini.Sections.Count(s => s.GetInt("LockShowAndHide") == 1);
                        var pages = ini.Sections.Count(s => s.Name.StartsWith("Page_", StringComparison.OrdinalIgnoreCase));
                        var flags = new List<string>();
                        if (width <= 0 || height <= 0) flags.Add("host");
                        if (leaves <= 2) flags.Add("shell");
                        if (hosts >= 1) flags.Add("runtime-hosts=" + hosts);
                        if (pages > 0 && string.IsNullOrWhiteSpace(window.Page))
                            flags.Add("pages=" + pages + " no-default");
                        if (outOfBounds > 0) flags.Add("oob=" + outOfBounds);
                        if (build.Placeholders.Count > 0) flags.Add("ph=" + build.Placeholders.Count);
                        if (build.UnresolvedStrings.Count > 0) flags.Add("str=" + build.UnresolvedStrings.Count);
                        if (flags.Count == 0) flags.Add("ok");

                        n++;
                        if (leaves <= 2) shell++;
                        if (hosts >= 1) runtime++;
                        if (width > 0 && height > 0) placed++;
                        report.AppendLine(string.Join("\t", new[]
                        {
                            window.Id, stage.Id,
                            width.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                            height.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                            filtered.Sections.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            build.Elements.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            leaves.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            build.Placeholders.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            build.UnresolvedStrings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            outOfBounds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            pages.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            window.Page ?? "",
                            lsh.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            hosts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            string.Join(",", flags),
                        }));
                    }
                    stageStats.Add($"# stage {stage.Id}: windows={n} placed={placed} shell={shell} runtime-hosts={runtime}");
                    totalShell += shell;
                    totalRuntime += runtime;
                    totalPlaced += placed;
                }
                foreach (var line in stageStats) report.AppendLine(line);
                report.AppendLine($"# total: shell={totalShell} runtime-hosts={totalRuntime} placed={totalPlaced}");

                outPath ??= Path.Combine(Paths.AppRoot, "Data", "render_status.tsv");
                File.WriteAllText(outPath, report.ToString());
                Console.WriteLine($"status -> {outPath}");
                foreach (var line in stageStats) Console.WriteLine(line);
                Console.WriteLine($"# total: shell={totalShell} runtime-hosts={totalRuntime} placed={totalPlaced}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("status failed: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// Renders every window of one stage into a labeled thumbnail grid (P2 of
        /// docs/ui/UI_RENDER_FIDELITY_PLAN.md):
        ///   UiProcessApp.exe --contact-sheet &lt;stageId|title|number&gt; [--out sheet.png] [--cols N] [--max N]
        /// </summary>
        private static int RunContactSheet(string[] args)
        {
            try
            {
                string stageArg = null, outPath = null;
                int cols = 4, max = 80;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "--contact-sheet") stageArg = args[i + 1];
                    else if (args[i] == "--out") outPath = args[i + 1];
                    else if (args[i] == "--cols") int.TryParse(args[i + 1], out cols);
                    else if (args[i] == "--max") int.TryParse(args[i + 1], out max);
                }
                if (string.IsNullOrWhiteSpace(stageArg))
                    throw new ArgumentException("--contact-sheet needs a stage id, title or 1-based number");
                if (cols < 1) cols = 4;

                Paths.Locate();
                var inventoryPath = Path.Combine(AppContext.BaseDirectory, "Data", "ui_inventory.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(inventoryPath), options);

                StageInfo stage = null;
                int stageIndex = -1;
                for (int i = 0; i < inventory.Stages.Count; i++)
                {
                    var s = inventory.Stages[i];
                    if (string.Equals(s.Id, stageArg, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.Title, stageArg, StringComparison.OrdinalIgnoreCase) ||
                        (int.TryParse(stageArg, out var n) && n == i + 1))
                    {
                        stage = s;
                        stageIndex = i;
                        break;
                    }
                }
                if (stage == null) throw new ArgumentException($"stage '{stageArg}' not found");

                var assets = new AssetResolver(Paths.ResolveRoots());
                var textures = new UiTexCache(assets);
                var rejected = RejectionStore.Load(Paths.AppRoot) ?? new Dictionary<string, string>();
                int rejectedInStage = (stage.Windows ?? new List<WindowInfo>()).Count(w => rejected.ContainsKey(w.Id));
                var windows = (stage.Windows ?? new List<WindowInfo>())
                    .Where(w => !string.IsNullOrWhiteSpace(w.Path) && !rejected.ContainsKey(w.Id))
                    .Take(max).ToList();
                if (windows.Count == 0) throw new ArgumentException("stage has no renderable windows");

                const double cellW = 320, cellH = 240, pad = 8, labelH = 24;
                int rows = (windows.Count + cols - 1) / cols;
                int sheetW = (int)(cols * cellW);
                int sheetH = (int)(labelH + rows * (cellH + labelH + pad) + pad);
                var titleFace = new Typeface("Microsoft YaHei");
                var labelFace = new Typeface("Microsoft YaHei");
                var white = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
                var gray = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
                var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3E, 0x44)), 1);

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x18, 0x1A, 0x1E)), null,
                        new Rect(0, 0, sheetW, sheetH));
                    dc.DrawText(new FormattedText(
                            $"{stageIndex + 1}. {stage.Title}  ({windows.Count} windows" +
                            (rejectedInStage > 0 ? $", {rejectedInStage} rejected hidden" : "") + ")",
                            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                            titleFace, 16, white, 1.0),
                        new Point(8, 3));

                    for (int i = 0; i < windows.Count; i++)
                    {
                        var window = windows[i];
                        int col = i % cols, row = i / cols;
                        double x = col * cellW;
                        double y = labelH + row * (cellH + labelH + pad);
                        var name = string.IsNullOrWhiteSpace(window.Cn) ? window.Title : window.Cn;
                        var label = $"{stageIndex + 1}.{i + 1}  {name}";
                        try
                        {
                            var filtered = BuildWindowPlan(window, out _);
                            if (filtered == null)
                            {
                                dc.DrawText(new FormattedText(label + "  (no INI)", System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight, labelFace, 13, gray, 1.0), new Point(x + 6, y + 4));
                                continue;
                            }
                            var build = UiLayout.Build(filtered, assets, textures);
                            double width = filtered.Sections[0].GetInt("Width");
                            double height = filtered.Sections[0].GetInt("Height");
                            if (width <= 0) width = 1280;
                            if (height <= 0) height = 720;
                            var host = new Border
                            {
                                Width = width,
                                Height = height,
                                Background = BackdropBrush(window),
                                Child = build.Root,
                            };
                            host.Measure(new Size(width, height));
                            host.Arrange(new Rect(0, 0, width, height));
                            host.UpdateLayout();
                            var bmp = new RenderTargetBitmap(Math.Max(1, (int)width), Math.Max(1, (int)height),
                                96, 96, PixelFormats.Pbgra32);
                            bmp.Render(host);

                            double scale = Math.Min((cellW - pad * 2) / width, (cellH - pad * 2) / height);
                            if (scale > 1) scale = 1;
                            double dw = width * scale, dh = height * scale;
                            double dx = x + (cellW - dw) / 2, dy = y + (cellH - dh) / 2;
                            dc.DrawImage(bmp, new Rect(dx, dy, dw, dh));
                            dc.DrawRectangle(null, borderPen, new Rect(dx, dy, dw, dh));

                            dc.DrawText(new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight, labelFace, 13, white, 1.0), new Point(x + 6, y + cellH + 3));
                            var flags = $"{width:0}x{height:0}  {filtered.Sections.Count}s  " +
                                        $"{build.Placeholders.Count}ph  {build.UnresolvedStrings.Count}str";
                            dc.DrawText(new FormattedText(flags, System.Globalization.CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight, labelFace, 11, gray, 1.0), new Point(x + 6, y + cellH + 19));
                        }
                        catch (Exception ex)
                        {
                            dc.DrawText(new FormattedText(label + "  (failed: " + ex.Message + ")",
                                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                labelFace, 12, gray, 1.0), new Point(x + 6, y + 4));
                        }
                    }
                }

                var sheet = new RenderTargetBitmap(sheetW, sheetH, 96, 96, PixelFormats.Pbgra32);
                sheet.Render(visual);
                outPath ??= Path.Combine(AppContext.BaseDirectory, "contact_sheet_" + stage.Id + ".png");
                using (var stream = File.Create(outPath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(sheet));
                    encoder.Save(stream);
                }
                Console.WriteLine($"contact-sheet -> {outPath} windows={windows.Count} size={sheetW}x{sheetH}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("contact-sheet failed: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// Headless interaction check: dispatch one script event through
        /// tools/ui/replay_server.lua, apply the returned mutation delta on top of the
        /// window's runtime state and render the result (docs/ui/UI_INTERACTION_REPLAY.md).
        ///   UiProcessApp.exe --click &lt;windowId&gt; &lt;section&gt; [handler] [--out file.png]
        /// </summary>
        private static int RunClick(string[] args)
        {
            try
            {
                string windowId = null, section = null, handler = null, outPath = null;
                int ci = Array.IndexOf(args, "--click");
                if (ci >= 0)
                {
                    if (ci + 1 < args.Length) windowId = args[ci + 1];
                    if (ci + 2 < args.Length) section = args[ci + 2];
                    if (ci + 3 < args.Length && !args[ci + 3].StartsWith("--")) handler = args[ci + 3];
                }
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "--out") outPath = args[i + 1];
                if (windowId == null || section == null)
                    throw new ArgumentException("--click needs <windowId> <section> [handler]");
                handler ??= "OnLButtonClick";

                Paths.Locate();
                var inventoryPath = Path.Combine(AppContext.BaseDirectory, "Data", "ui_inventory.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(inventoryPath), options);
                WindowInfo window = null;
                foreach (var stage in inventory.Stages)
                    foreach (var w in stage.Windows ?? new List<WindowInfo>())
                        if (string.Equals(w.Id, windowId, StringComparison.OrdinalIgnoreCase)) window = w;
                if (window?.Path == null) throw new ArgumentException($"window '{windowId}' has no layout");

                var stem = Path.GetFileNameWithoutExtension(window.Path);
                var rel = window.Path.Replace('/', Path.DirectorySeparatorChar);
                var iniPath = window.Root == "pak" ? Path.Combine(Paths.PakRoot, rel)
                                                   : Path.Combine(Paths.AppRoot, "assets", "ui", rel);
                var luaPath = Path.Combine(Paths.AppRoot, "assets", "ui", "Config", "Default", stem + ".lua");
                if (!File.Exists(luaPath)) throw new ArgumentException("no script for " + stem);

                var delta = DispatchReplayEvent(luaPath, iniPath, section, handler);
                Console.WriteLine($"click {windowId} {section} {handler} -> mutations={delta.Count}");

                var filtered = BuildWindowPlan(window, out _);
                LayoutPlanBuilder.ApplyRuntimeMutations(filtered, delta);
                var assets = new AssetResolver(Paths.ResolveRoots());
                var textures = new UiTexCache(assets);
                var build = UiLayout.Build(filtered, assets, textures);
                double width = filtered.Sections[0].GetInt("Width");
                double height = filtered.Sections[0].GetInt("Height");
                if (width <= 0) width = 1280;
                if (height <= 0) height = 720;
                var host = new Border
                {
                    Width = width,
                    Height = height,
                    Background = BackdropBrush(window),
                    Child = build.Root,
                };
                host.Measure(new Size(width, height));
                host.Arrange(new Rect(0, 0, width, height));
                host.UpdateLayout();
                var overhang = ComputeOverhang(build, width, height);
                if (overhang.L > 0 || overhang.T > 0 || overhang.R > 0 || overhang.B > 0)
                {
                    var expanded = new Canvas
                    {
                        Width = width + overhang.L + overhang.R,
                        Height = height + overhang.T + overhang.B,
                        Background = BackdropBrush(window),
                    };
                    host.Child = null;
                    Canvas.SetLeft(build.Root, overhang.L);
                    Canvas.SetTop(build.Root, overhang.T);
                    expanded.Children.Add(build.Root);
                    host.Child = expanded;
                    host.Width = expanded.Width;
                    host.Height = expanded.Height;
                    host.Measure(new Size(expanded.Width, expanded.Height));
                    host.Arrange(new Rect(0, 0, expanded.Width, expanded.Height));
                    host.UpdateLayout();
                }
                outPath ??= Path.Combine(AppContext.BaseDirectory, $"click_{windowId}_{section}.png");
                var bitmap = new RenderTargetBitmap((int)host.Width, (int)host.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                using (var stream = File.Create(outPath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }
                Console.WriteLine($"rendered {filtered.Sections.Count} sections -> {outPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("click failed: " + ex.Message);
                return 1;
            }
        }

        /// <summary>Spawns replay_server.lua, dispatches one EVENT and returns the delta lines.</summary>
        private static List<string> DispatchReplayEvent(string luaPath, string iniPath, string section, string handler)
        {
            var lua32 = Environment.GetEnvironmentVariable("LUA32");
            if (string.IsNullOrWhiteSpace(lua32))
                lua32 = @"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\lua-5.1.5\lua-5.1.5\build32\lua32.exe";
            var server = Path.Combine(Paths.RepoRoot ?? "", "tools", "ui", "replay_server.lua");
            if (!File.Exists(lua32) || !File.Exists(server))
                throw new FileNotFoundException("lua32 or replay_server.lua missing");
            var psi = new ProcessStartInfo(lua32, "\"" + server + "\" \"" + luaPath + "\" auto \"" + iniPath + "\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var proc = Process.Start(psi))
            {
                proc.StandardOutput.ReadLine(); // READY
                proc.StandardInput.WriteLine("EVENT " + section + " " + handler);
                proc.StandardInput.Flush();
                var lines = new List<string>();
                for (int i = 0; i < 5000; i++)
                {
                    var line = proc.StandardOutput.ReadLine();
                    if (line == null || line == "END") break;
                    if (line.StartsWith("RESULT ", StringComparison.Ordinal)) continue;
                    if (line.IndexOf('\t') >= 0) lines.Add(line);
                }
                try { proc.Kill(); } catch { }
                return lines;
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
            if (window?.AppendIni != null)
                foreach (var spec in window.AppendIni)
                    if (spec?.Show != null) shown.AddRange(spec.Show);
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
            LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, spec.Lists, LoadTemplateIni);
            LayoutPlanBuilder.ApplyTexts(plan.Filtered, spec.Texts);
            LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, spec.Adjust);
            var build = UiLayout.Build(plan.Filtered, assets, textures);
            // Pin the overlay to the top-left of the composite grid: a fixed-size
            // root inside a taller grid would otherwise be centered by WPF.
            build.Root.HorizontalAlignment = HorizontalAlignment.Left;
            build.Root.VerticalAlignment = VerticalAlignment.Top;
            return build.Root;
        }

        /// <summary>
        /// Host backdrop for a window render: the inventory `backdrop` hex colour when set
        /// (semi-transparent windows composite over the game world on the client), else
        /// the viewer's default dark host.
        /// </summary>
        internal static SolidColorBrush BackdropBrush(WindowInfo window)
        {
            var hex = window?.Backdrop;
            if (!string.IsNullOrWhiteSpace(hex))
            {
                var s = hex.TrimStart('#');
                if (s.Length == 6 && uint.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return new SolidColorBrush(Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v));
            }
            return new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
        }

        internal static IniFile LoadTemplateIni(string relative)        {            var rel = relative.Replace('/', Path.DirectorySeparatorChar);
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

        /// <summary>Tolerant variant for optional second INIs (appendIni): missing
        /// sources leave the window renderable instead of failing the build.</summary>
        internal static IniFile LoadIniTolerant(string relative)
        {
            try { return LoadTemplateIni(relative); }
            catch { return null; }
        }

        /// <summary>
        /// The full inventory-driven plan pipeline (INI load + page + appendIni +
        /// hide/skin/anchors/tabs/lists/LSH/appends/texts/images/adjust + viewer page
        /// state). Shared by the headless status scan and the contact sheet; the
        /// interactive renderer keeps its own copy because it takes CLI page/hide
        /// overrides.
        /// </summary>
        internal static IniFile BuildWindowPlan(WindowInfo window, out IniFile rawIni)
        {
            rawIni = null;
            if (window == null || string.IsNullOrWhiteSpace(window.Path)) return null;
            var rel = window.Path.Replace('/', Path.DirectorySeparatorChar);
            var iniPath = window.Root == "pak"
                ? Path.Combine(Paths.PakRoot, rel)
                : Path.Combine(Paths.AppRoot, "assets", "ui", rel);
            if (!File.Exists(iniPath)) return null;
            rawIni = IniFile.Load(iniPath);
            var plan = LayoutPlanBuilder.Build(rawIni, window.Page);
            LayoutPlanBuilder.ApplyAppendIni(plan.Filtered, window.AppendIni, LoadIniTolerant);
            LayoutPlanBuilder.ApplyRuntimeState(plan.Filtered, window.Path);
            LayoutPlanBuilder.ApplyHide(plan.Filtered, window.Hide);
            LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
            LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
            LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, window.Page);
            LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, LoadTemplateIni);
            LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, ScriptShown(window));
            LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
            LayoutPlanBuilder.ApplyTexts(plan.Filtered, window.Texts);
            LayoutPlanBuilder.ApplyImages(plan.Filtered, window.Images);
            LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, window.Adjust);
            if (window.Pages != null && window.Pages.Count > 0)
            {
                var page = window.Pages.FirstOrDefault(pg =>
                        string.Equals(pg.Id, window.Page, StringComparison.OrdinalIgnoreCase)) ?? window.Pages[0];
                LayoutPlanBuilder.ApplyTexts(plan.Filtered, page.Texts);
                LayoutPlanBuilder.ApplyImages(plan.Filtered, page.Images);
                LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, page.Adjust);
                if (page.Hide != null && page.Hide.Count > 0)
                    LayoutPlanBuilder.ApplyHide(plan.Filtered, string.Join(",", page.Hide));
            }
            return plan.Filtered;
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
                            LayoutPlanBuilder.ApplyAppendIni(plan.Filtered, window.AppendIni, LoadIniTolerant);
                            LayoutPlanBuilder.ApplyRuntimeState(plan.Filtered, window.Path);
                            LayoutPlanBuilder.ApplyHide(plan.Filtered, window.Hide);
                            LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
                            LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
                LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, window.Page);
                            LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, LoadTemplateIni);
                            LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, ScriptShown(window));
                            LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
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
