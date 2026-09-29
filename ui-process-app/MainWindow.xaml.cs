using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapUiApp.Engine;
using UiProcessApp.Engine;

namespace UiProcessApp
{
    public partial class MainWindow : Window
    {
        private Inventory _inventory;
        private Canvas _layoutCanvas;
        private WindowInfo _currentWindow;
        private IniFile _currentIni;
        private LayoutPlan _currentPlan;
        private bool _updatingPages;
        private double _zoom = 1.0;
        private bool _zoomUserSet;
        private bool _settingZoom;

        public MainWindow()
        {
            InitializeComponent();
            UiLayout.Wireframe = false;
        }

        private sealed class LayoutPlan
        {
            public IniFile Filtered;
            public List<string> Pages;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Paths.Locate();
            _inventory = LoadInventory();
            BuildTree(null);
            StatusText.Text =
                $"assets={Paths.AppRoot}   ui={(Paths.ProofUiRoot != null ? Paths.ProofUiRoot : Path.Combine(Paths.AppRoot, "assets", "ui"))}   " +
                $"strings={Strings.Table.Count}";
            if (StageTree.Items.Count > 0 && StageTree.Items[0] is TreeViewItem firstStage && firstStage.Items.Count > 0)
            {
                var first = (TreeViewItem)firstStage.Items[0];
                first.IsSelected = true;
                first.BringIntoView();
            }
        }

        private Inventory LoadInventory()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Data", "ui_inventory.json"),
                Path.Combine(Paths.AppRoot, "Data", "ui_inventory.json"),
            };
            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inv = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(path), options);
                if (inv?.Stages != null) return inv;
            }
            throw new FileNotFoundException("ui_inventory.json not found next to the app or in Data/.");
        }

        private void BuildTree(string filter)
        {
            StageTree.Items.Clear();
            if (_inventory?.Stages == null) return;
            foreach (var stage in _inventory.Stages)
            {
                var windows = stage.Windows ?? new List<WindowInfo>();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    windows = windows.Where(w => Matches(w, filter)).ToList();
                    if (windows.Count == 0 && !Matches(stage, filter)) continue;
                }
                var stageNode = new TreeViewItem
                {
                    Header = stage.Title,
                    IsExpanded = true,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xD0, 0x90)),
                    Tag = stage,
                };
                foreach (var window in windows)
                {
                    stageNode.Items.Add(new TreeViewItem
                    {
                        Header = $"{window.Title}  路  {window.Cn}",
                        Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                        Tag = window,
                    });
                }
                StageTree.Items.Add(stageNode);
            }
        }

        private static bool Matches(StageInfo stage, string filter)
        {
            return (stage.Title ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (stage.Summary ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Matches(WindowInfo window, string filter)
        {
            return (window.Title ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (window.Cn ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (window.Summary ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (window.Labels ?? new List<string>()).Any(l => l.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            BuildTree(SearchBox.Text.Trim());
        }

        private void OnTreeSelection(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem { Tag: WindowInfo window })
                ShowWindow(window);
            else if (e.NewValue is TreeViewItem { Tag: StageInfo stage })
                ShowStage(stage);
        }

        private void ShowStage(StageInfo stage)
        {
            _currentWindow = null;
            _currentIni = null;
            _currentPlan = null;
            PageBox.Items.Clear();
            PageBox.IsEnabled = false;
            WindowTitle.Text = stage.Title;
            WindowMeta.Text = stage.Summary;
            DetailsPanel.Children.Clear();
            AddHeading(stage.Title);
            AddParagraph(stage.Summary);
            AddHeading("绐楀彛");
            foreach (var w in stage.Windows ?? new List<WindowInfo>())
                AddBullet($"{w.Title} 路 {w.Cn}  [{w.Status}]");
            LabelsGrid.ItemsSource = new List<LabelRow>();
            IniText.Text = "";
            LayoutHost.Child = ShowMessage("Select a window to render its layout.");
        }

        private void ShowWindow(WindowInfo window)
        {
            _currentWindow = window;
            WindowTitle.Text = $"{window.Title}  路  {window.Cn}";
            WindowMeta.Text = $"[{window.Status}]  {window.Summary}";

            DetailsPanel.Children.Clear();
            AddHeading(window.Title);
            AddParagraph(window.Summary);

            var iniPath = ResolveIniPath(window);
            AddHeading("璇佹嵁");
            foreach (var evidence in window.Evidence ?? new List<string>())
            {
                var full = ResolveRepoPath(evidence);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                var text = new TextBlock
                {
                    Text = evidence,
                    Foreground = full != null ? new SolidColorBrush(Color.FromRgb(0x6F, 0xC0, 0xEF))
                                              : new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                row.Children.Add(text);
                if (full != null)
                {
                    var open = new Button { Content = "open", Padding = new Thickness(6, 1, 6, 1), FontSize = 11 };
                    open.Click += (_, _) => OpenInExplorer(full);
                    row.Children.Add(open);
                }
                DetailsPanel.Children.Add(row);
            }

            if ((window.Elements?.Count ?? 0) > 0)
            {
                AddHeading("鐣岄潰鍏冪礌");
                foreach (var element in window.Elements) AddBullet(element);
            }

            if ((window.Labels?.Count ?? 0) > 0)
            {
                AddHeading("鍏抽敭鏂囨");
                foreach (var id in window.Labels)
                    AddBullet($"{id} = {Strings.Resolve(id)}");
            }

            _currentIni = iniPath != null && File.Exists(iniPath) ? TryLoadIni(iniPath) : null;
            HideBox.Text = window.Hide ?? "";
            RenderLabels(window, iniPath);
            RenderIni(iniPath);
            UpdatePageBox();
            RenderLayout(window);
        }

        private static IniFile TryLoadIni(string path)
        {
            try { return IniFile.Load(path); }
            catch { return null; }
        }

        private void UpdatePageBox()
        {
            _updatingPages = true;
            PageBox.Items.Clear();
            if (_currentIni == null)
            {
                PageBox.IsEnabled = false;
                _updatingPages = false;
                return;
            }
            PageBox.IsEnabled = true;
            var pages = BuildPlan(_currentIni, null).Pages;
            PageBox.Items.Add("(all)");
            foreach (var page in pages) PageBox.Items.Add(page);
            var preferred = !string.IsNullOrWhiteSpace(_currentWindow?.Page) && pages.Contains(_currentWindow.Page)
                ? _currentWindow.Page
                : pages.Contains("Page_DesertStorm") ? "Page_DesertStorm"
                : pages.Count > 0 ? pages[0] : "(all)";
            PageBox.SelectedItem = preferred;
            _updatingPages = false;
            RebuildPlan();
        }

        private void RebuildPlan()
        {
            if (_currentIni == null) { _currentPlan = null; return; }
            var page = PageBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(page) || page == "(all)") page = null;
            _currentPlan = BuildPlan(_currentIni, page);
        }

        private void OnPageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingPages || _currentWindow == null) return;
            RebuildPlan();
            RenderLayout(_currentWindow);
        }

        /// <summary>
        /// KGUI files re-issue element definitions under suffixed names (newest wins) and
        /// group content into Page_* tabs. This builds the visible subset for one page,
        /// keeping only the newest section per logical name and remapping parents.
        /// </summary>
        private static LayoutPlan BuildPlan(IniFile ini, string selectedPage)
        {
            static int SuffixDepth(string name)
            {
                var parts = name.Split('_');
                int depth = 0;
                for (int i = parts.Length - 1; i > 0; i--)
                {
                    if (parts[i].Length > 0 && parts[i].All(char.IsDigit)) depth++;
                    else break;
                }
                return depth;
            }

            var order = new Dictionary<IniSection, int>();
            for (int i = 0; i < ini.Sections.Count; i++) order[ini.Sections[i]] = i;

            var groups = new Dictionary<string, List<IniSection>>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in ini.Sections)
            {
                var match = System.Text.RegularExpressions.Regex.Match(section.Name, @"^(.*?)(?:_\d+)+$");
                var key = match.Success ? match.Groups[1].Value : section.Name;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<IniSection>();
                list.Add(section);
            }

            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in groups)
            {
                var chosen = group.Value
                    .OrderByDescending(s => SuffixDepth(s.Name))
                    .ThenBy(s => order[s])
                    .First();
                keep.Add(chosen.Name);
                foreach (var section in group.Value) alias[section.Name] = chosen.Name;
            }

            string PageOf(string name)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var current = name;
                while (!string.IsNullOrWhiteSpace(current) && seen.Add(current))
                {
                    if (current.StartsWith("Page_", StringComparison.OrdinalIgnoreCase)) return current;
                    var parent = ini.ByName.TryGetValue(current, out var section) ? section.Get("._Parent") : null;
                    current = parent;
                }
                return null;
            }

            var chain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cursor = selectedPage;
            while (!string.IsNullOrWhiteSpace(cursor))
            {
                chain.Add(cursor);
                cursor = ini.ByName.TryGetValue(cursor, out var section) ? section.Get("._Parent") : null;
            }

            var pages = ini.Sections
                .Where(s => s.Name.StartsWith("Page_", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name)
                .ToList();

            var filtered = new IniFile();
            foreach (var section in ini.Sections)
            {
                if (!keep.Contains(section.Name)) continue;
                var page = PageOf(section.Name);
                bool visible = string.IsNullOrWhiteSpace(selectedPage)
                    || page == null
                    || chain.Contains(section.Name)
                    || string.Equals(page, selectedPage, StringComparison.OrdinalIgnoreCase);
                if (!visible) continue;

                var clone = new IniSection { Name = section.Name };
                foreach (var pair in section.Values) clone.Values[pair.Key] = pair.Value;
                var parent = clone.Get("._Parent");
                if (!string.IsNullOrWhiteSpace(parent) && alias.TryGetValue(parent, out var mapped))
                    clone.Values["._Parent"] = mapped;
                filtered.Sections.Add(clone);
                filtered.ByName[clone.Name] = clone;
            }
            return new LayoutPlan { Filtered = filtered, Pages = pages };
        }

        private void RenderLabels(WindowInfo window, string iniPath)
        {
            var rows = new List<LabelRow>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string id, string source)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) return;
                rows.Add(new LabelRow { Id = id, Text = Strings.Resolve(id), Source = source });
            }

            foreach (var id in window.Labels ?? new List<string>()) Add(id, "inventory");

            if (iniPath != null && File.Exists(iniPath))
            {
                var ini = IniFile.Load(iniPath);
                foreach (var section in ini.Sections)
                {
                    foreach (var value in section.Values.Values)
                    {
                        var trimmed = (value ?? "").Trim();
                        if (trimmed.StartsWith("STR_", StringComparison.OrdinalIgnoreCase))
                            Add(trimmed, section.Name);
                    }
                }
            }

            LabelsGrid.ItemsSource = rows;
        }

        private void RenderIni(string iniPath)
        {
            IniText.Text = iniPath != null && File.Exists(iniPath)
                ? File.ReadAllText(iniPath)
                : "(no extracted INI for this window)";
        }

        private void RenderLayout(WindowInfo window)
        {
            _layoutCanvas = null;
            if (_currentIni == null)
            {
                LayoutHost.Child = ShowMessage("No extracted KGUI layout for this window (see evidence in Details).");
                AssetNote.Text = "";
                return;
            }
            try
            {
                var plan = LayoutPlanBuilder.Build(_currentIni, CurrentPage());
                LayoutPlanBuilder.ApplyHide(plan.Filtered, HideBox.Text);
                LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
                LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
                LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, CurrentPage() ?? window.Page);
                LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, App.LoadTemplateIni);
                LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, App.ScriptShown(window));
                LayoutPlanBuilder.ApplyTexts(plan.Filtered, window.Texts);
                LayoutPlanBuilder.ApplyImages(plan.Filtered, window.Images);
                LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, window.Adjust);
                LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
                if (plan.Filtered.Sections.Count == 0)
                {
                    LayoutHost.Child = ShowMessage("All sections are hidden (check the 闅愯棌 list).");
                    AssetNote.Text = "";
                    return;
                }
                var ini = plan.Filtered;
                var resolverRoot = Paths.ProofUiRoot ?? Path.Combine(Paths.AppRoot, "assets", "ui");
                var assets = new AssetResolver(Paths.ResolveRoots());
                var textures = new UiTexCache(assets);
                var build = UiLayout.Build(ini, assets, textures);

                double width = ini.Sections[0].GetInt("Width");
                double height = ini.Sections[0].GetInt("Height");
                if (width <= 0) width = 1280;
                if (height <= 0) height = 720;

                _layoutCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    Background = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                _layoutCanvas.Children.Add(build.Root);
                _layoutCanvas.Measure(new Size(width, height));
                _layoutCanvas.Arrange(new Rect(0, 0, width, height));
                _layoutCanvas.UpdateLayout();
                var overhang = App.ComputeOverhang(build, width, height);
                if (overhang.L > 0 || overhang.T > 0 || overhang.R > 0 || overhang.B > 0)
                {
                    width += overhang.L + overhang.R;
                    height += overhang.T + overhang.B;
                    _layoutCanvas.Width = width;
                    _layoutCanvas.Height = height;
                    Canvas.SetLeft(build.Root, overhang.L);
                    Canvas.SetTop(build.Root, overhang.T);
                }
                LayoutScroll.UpdateLayout();
                _zoom = FitZoom(width, height);
                _layoutCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
                LayoutHost.Child = _layoutCanvas;
                _settingZoom = true;
                if (Math.Abs(ZoomSlider.Value - _zoom) > 0.001) ZoomSlider.Value = _zoom;
                _settingZoom = false;
                LayoutScroll.ScrollToHome();
                var page = PageBox.SelectedItem as string ?? "(all)";
                AssetNote.Text = $"page={page}  size={width:0}x{height:0}  " +
                                 $"sections={ini.Sections.Count}  rendered={CountVisible(build.Root)}  " +
                                 $"art={(Paths.ProofUiRoot != null ? "on" : "missing")}";
            }
            catch (Exception ex)
            {
                LayoutHost.Child = ShowMessage("Layout render failed: " + ex.Message);
                AssetNote.Text = "";
            }
        }

        private double FitZoom(double width, double height)
        {
            LayoutScroll.UpdateLayout();
            var viewportWidth = LayoutScroll.ViewportWidth > 0 ? LayoutScroll.ViewportWidth : 900;
            var viewportHeight = LayoutScroll.ViewportHeight > 0 ? LayoutScroll.ViewportHeight : 700;
            var fit = Math.Min(1.0, Math.Min((viewportWidth - 56.0) / width, (viewportHeight - 56.0) / height));
            return fit < 0.25 ? 0.25 : fit;
        }

        private void OnZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _zoom = e.NewValue;
            if (!_settingZoom && _layoutCanvas != null) _zoomUserSet = true;
            if (_layoutCanvas != null)
                _layoutCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        }

        private void OnLayoutViewportChanged(object sender, SizeChangedEventArgs e)
        {
            if (_zoomUserSet || _layoutCanvas == null) return;
            var zoom = FitZoom(_layoutCanvas.Width, _layoutCanvas.Height);
            if (Math.Abs(zoom - _zoom) < 0.001) return;
            _zoom = zoom;
            _layoutCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
            _settingZoom = true;
            ZoomSlider.Value = _zoom;
            _settingZoom = false;
        }

        private void OnWireframeChanged(object sender, RoutedEventArgs e)
        {
            UiLayout.Wireframe = WireframeBox.IsChecked == true;
            if (_currentWindow != null)
                RenderLayout(_currentWindow);
        }

        private void OnHideKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || _currentWindow == null) return;
            RenderLayout(_currentWindow);
            e.Handled = true;
        }

        private string CurrentPage()
        {
            var page = PageBox.SelectedItem as string;
            return string.IsNullOrWhiteSpace(page) || page == "(all)" ? null : page;
        }

        private static int CountVisible(DependencyObject root)
        {
            int count = 0;
            if (root is FrameworkElement fe && fe.Visibility == Visibility.Visible)
            {
                bool painted =
                    fe is Panel panel && panel.Background != null ||
                    fe is System.Windows.Controls.Image image && image.Source != null ||
                    fe is TextBlock block && !string.IsNullOrWhiteSpace(block.Text);
                if (painted) count++;
            }
            int children = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < children; i++)
                count += CountVisible(VisualTreeHelper.GetChild(root, i));
            return count;
        }

        private static string ResolveIniPath(WindowInfo window)
        {
            if (string.IsNullOrWhiteSpace(window.Path)) return null;
            var rel = window.Path.Replace('/', Path.DirectorySeparatorChar);
            return window.Root == "pak"
                ? Path.Combine(Paths.PakRoot, rel)
                : Path.Combine(Paths.AppRoot, "assets", "ui", rel);
        }

        private static string ResolveRepoPath(string evidence)
        {
            if (string.IsNullOrWhiteSpace(evidence) || Paths.RepoRoot == null) return null;
            var rel = evidence.Split(' ')[0].Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(Paths.RepoRoot, rel);
            return File.Exists(full) || Directory.Exists(full) ? full : null;
        }

        private static void OpenInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                else
                    Process.Start("explorer.exe", $"\"{path}\"");
            }
            catch
            {
                // best effort
            }
        }

        private UIElement ShowMessage(string text) =>
            new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6),
            };

        private void AddHeading(string text) =>
            DetailsPanel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xD0, 0x90)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 12, 0, 4),
            });

        private void AddParagraph(string text) =>
            DetailsPanel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 6),
            });

        private void AddBullet(string text) =>
            DetailsPanel.Children.Add(new TextBlock
            {
                Text = "鈥?" + text,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(14, 1, 0, 1),
            });
    }

    public sealed class Inventory
    {
        public string Title { get; set; }
        public string Generated { get; set; }
        public List<StageInfo> Stages { get; set; }
    }

    public sealed class StageInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public List<WindowInfo> Windows { get; set; }
    }

    public sealed class WindowInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Cn { get; set; }
        public string Status { get; set; }
        public string Root { get; set; }
        public string Path { get; set; }
        public string Page { get; set; }
        public string Summary { get; set; }
        public string Hide { get; set; }
        public string Skin { get; set; }
        public TabStrip Tabs { get; set; }
        public List<string> Show { get; set; }
        public List<TextOverride> Texts { get; set; }
        public List<AdjustSpec> Adjust { get; set; }
        public List<AnchorSpec> Anchors { get; set; }
        public List<ImageOverride> Images { get; set; }
        public List<AppendSpec> Appends { get; set; }
        public List<ListTemplate> Lists { get; set; }
        public List<string> Evidence { get; set; }
        public List<string> Elements { get; set; }
        public List<string> Labels { get; set; }
    }

    public sealed class LabelRow
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Source { get; set; }
    }
}
