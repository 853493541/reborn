using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private Dictionary<string, string> _rejected;
        /// <summary>Window-opener globals scanned from the window scripts' own SETGLOBAL
        /// definitions (OpenBankPanel -> BigBankPanel); resolves recorded popup chains.</summary>
        private Dictionary<string, string> _windowAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Per-window render status from Data/render_status.tsv (P1 badge:
        /// placeholders/unresolved/outOfBounds + shell/runtime-host flags).</summary>
        private Dictionary<string, string> _status;

        // Interaction replay (docs/ui/UI_INTERACTION_REPLAY.md): a per-window
        // replay_server.lua process dispatches the client's own handlers; clicks
        // append their mutation deltas to a per-window overlay applied on re-render.
        private Process _replayServer;
        private StreamWriter _replayIn;
        private StreamReader _replayOut;
        private string _replayWindowId;
        private readonly HashSet<string> _replayHandlers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _runtimeOverlays =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _openedWindows =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _windowHistory = new List<string>();
        private UiBuildResult _lastBuild;
        private Dictionary<object, string> _elementToSection;
        private string _hoverSection;
        // Wheel scroll offsets per WndScroll section (engine-native viewport scroll).
        private readonly System.Collections.Generic.Dictionary<string, double> _scrollOffsets =
            new System.Collections.Generic.Dictionary<string, double>(StringComparer.Ordinal);
        // Window move (the engine's moveable windows / SetDragArea): drag the root
        // background to translate the whole window canvas.
        private bool _windowDragActive;
        private System.Windows.Point _windowDragStart;
        private double _windowDragOffX, _windowDragOffY;
        // Pointer state for the client's own item events (KGUIX64 KItemEventMgr
        // 0x180153590/0x180153850: OnItemLButtonDown on press, OnItemLButtonDrag while
        // held past the ~3px threshold, OnItemLButtonUp + OnItemLButtonDragEnd on
        // release, OnItemLButtonClick on a release without drag) and the drag-handle
        // family (OnDragButtonBegin/Drag/End, fired with `this` = the control the
        // script registered via RegisterLButtonDrag).
        private string _pointerDownSection;
        private System.Windows.Point _pointerDownPoint;
        private bool _pointerDragging;
        private bool _handleDragActive;
        private string _handleDragSection;
        private DateTime _lastDragDispatchUtc = DateTime.MinValue;
        // Scrollbar thumb drag (WndScroll/WndNewScrollBar): shares the wheel's offset.
        private string _scrollDragName;
        private double _scrollDragStartY;
        private double _scrollDragStartOffset;
        // Per-item review checklist (Data/item_checks.tsv) + the rendered-item list
        // per layout cache key + the canvas highlight overlay.
        private readonly System.Collections.Generic.Dictionary<string, bool> _itemChecks =
            new System.Collections.Generic.Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(string Name, string Type)>> _checklistCache =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(string Name, string Type)>>(StringComparer.OrdinalIgnoreCase);
        private System.Windows.Controls.Border _checkHighlight;
        // One-shot out-of-bounds pass after a render (the elements must be measured).
        private UiBuildResult _pendingOobBuild;
        private System.Collections.Generic.List<(string Name, string Type)> _pendingOobItems;
        private System.Collections.Generic.Dictionary<string, string> _pendingOobIssues;

        // Render speed: the resolver/texture cache is shared across renders (atlas TGAs
        // decode once per session) and built layouts are cached per window/page/hide so
        // re-visits are instant; the NEXT catalog window is pre-built in the background
        // so sweeping with X renders from cache.
        private static AssetResolver _sharedAssets;
        private static UiTexCache _sharedTextures;
        private readonly Dictionary<string, Canvas> _layoutCache = new Dictionary<string, Canvas>();
        private readonly Dictionary<string, string> _layoutNotes = new Dictionary<string, string>();
        private readonly List<string> _layoutOrder = new List<string>();
        private readonly Dictionary<string, IniFile> _iniCache = new Dictionary<string, IniFile>();
        private bool _prewarmQueued;
        private const int LayoutCacheCap = 6;
        private IniFile _currentIni;
        private LayoutPlan _currentPlan;
        private Dictionary<WindowInfo, string> _numbers;
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
            _rejected = RejectionStore.Load(Paths.AppRoot);
            RejectionStore.Apply(_inventory, _rejected);
            _windowAliases = LoadWindowAliases();
            foreach (var pair in ItemCheckStore.Load(Paths.AppRoot)) _itemChecks[pair.Key] = pair.Value;
            _status = LoadStatus();
            LayoutHost.LayoutUpdated += OnLayoutUpdated;
            BuildTree(null);
            StatusText.Text =
                $"assets={Paths.AppRoot}   ui={(Paths.ProofUiRoot != null ? Paths.ProofUiRoot : Path.Combine(Paths.AppRoot, "assets", "ui"))}   " +
                $"strings={Strings.Table.Count}   status={(_status != null ? _status.Count.ToString(CultureInfo.InvariantCulture) : "none")}   " +
                "|   X: 标记不需要 / 再按 X 恢复";
            if (StageTree.Items.Count > 0 && StageTree.Items[0] is TreeViewItem firstStage && firstStage.Items.Count > 0)
            {
                // Open on the catalog item currently being worked on
                // (`defaultWindow`), else the first window.
                TreeViewItem target = null;
                if (!string.IsNullOrWhiteSpace(_inventory?.DefaultWindow))
                {
                    foreach (TreeViewItem stageNode in StageTree.Items)
                        foreach (TreeViewItem node in stageNode.Items)
                            if (node.Tag is WindowInfo wi &&
                                string.Equals(wi.Id, _inventory.DefaultWindow, StringComparison.OrdinalIgnoreCase))
                            { target = node; break; }
                }
                target = target ?? (TreeViewItem)firstStage.Items[0];
                target.IsSelected = true;
                target.BringIntoView();
                // Keyboard focus on the tree (not the search box) so the X reject key
                // works right after startup without an extra click.
                StageTree.Focus();
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

        private Dictionary<string, string> LoadStatus()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Data", "render_status.tsv"),
                Path.Combine(Paths.AppRoot, "Data", "render_status.tsv"),
            };
            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 15 || parts[0] == "id") continue;
                    map[parts[0]] = $"ph={parts[7]} str={parts[8]} oob={parts[9]}  {parts[14]}";
                }
                if (map.Count > 0) return map;
            }
            return null;
        }

        private string StatusNote(WindowInfo window)
        {
            if (window == null || _status == null || !_status.TryGetValue(window.Id, out var s)) return "";
            return "  |  status: " + s;
        }

        private void BuildTree(string filter)
        {
            StageTree.Items.Clear();
            if (_inventory?.Stages == null) return;

            _numbers = new Dictionary<WindowInfo, string>();
            for (int si = 0; si < _inventory.Stages.Count; si++)
            {
                var all = _inventory.Stages[si].Windows ?? new List<WindowInfo>();
                for (int wi = 0; wi < all.Count; wi++)
                    _numbers[all[wi]] = (si + 1).ToString(CultureInfo.InvariantCulture) + "." +
                                        (wi + 1).ToString(CultureInfo.InvariantCulture);
            }

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
                    Header = ChineseOnly(stage.Title),
                    IsExpanded = true,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xD0, 0x90)),
                    Tag = stage,
                };
                foreach (var window in windows)
                {
                    var name = string.IsNullOrWhiteSpace(window.Cn) ? window.Title : window.Cn;
                    stageNode.Items.Add(new TreeViewItem
                    {
                        Header = Number(window) + "  " + name,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                        ToolTip = window.Title + StatusNote(window),
                        Tag = window,
                    });
                }
                StageTree.Items.Add(stageNode);
            }
        }

        /// <summary>Stable "stage.window" number (e.g. 5.1) assigned in BuildTree.</summary>
        private string Number(WindowInfo window)
        {
            if (window != null && _numbers != null && _numbers.TryGetValue(window, out var n)) return n;
            return "";
        }

        /// <summary>
        /// Drops a trailing English phrase from a catalog title ("1. 排队 Queue" →
        /// "1. 排队"), so the tree shows Chinese only.
        /// </summary>
        private static string ChineseOnly(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var trimmed = System.Text.RegularExpressions.Regex.Replace(
                text, @"\s+[A-Za-z][A-Za-z0-9 /'\-]*$", "");
            return string.IsNullOrWhiteSpace(trimmed) ? text : trimmed.TrimEnd();
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
            StopReplayServer();
            _currentWindow = null;
            _currentIni = null;
            _currentPlan = null;
            PageBox.Items.Clear();
            PageBox.IsEnabled = false;
            WindowTitle.Text = ChineseOnly(stage.Title);
            DetailsPanel.Children.Clear();
            AddHeading(ChineseOnly(stage.Title));
            AddParagraph(stage.Summary);
            AddHeading("窗口");
            foreach (var w in stage.Windows ?? new List<WindowInfo>())
            {
                var name = string.IsNullOrWhiteSpace(w.Cn) ? w.Title : w.Cn;
                AddBullet($"{Number(w)}  {name}  [{w.Status}]");
            }
            LabelsGrid.ItemsSource = new List<LabelRow>();
            IniText.Text = "";
            LayoutHost.Child = ShowMessage("Select a window to render its layout.");
        }

        private void ShowWindow(WindowInfo window)
        {
            _currentWindow = window;
            var displayName = string.IsNullOrWhiteSpace(window.Cn) ? window.Title : window.Cn;
            WindowTitle.Text = Number(window) + "  " + displayName;
            WindowTitle.ToolTip = window.Title;

            DetailsPanel.Children.Clear();
            AddHeading(Number(window) + "  " + displayName);
            AddParagraph($"[{window.Status}]");

            var iniPath = ResolveIniPath(window);
            AddHeading("证据");
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
                AddHeading("界面元素");
                foreach (var element in window.Elements) AddBullet(element);
            }

            if ((window.Labels?.Count ?? 0) > 0)
            {
                AddHeading("关键文案");
                foreach (var id in window.Labels)
                    AddBullet($"{id} = {Strings.Resolve(id)}");
            }

            _currentIni = GetIni(window);
            HideBox.Text = window.Hide ?? "";
            RenderLabels(window, iniPath);
            RenderIni(iniPath);
            UpdatePageBox();
            RenderLayout(window);
            StartReplayServer(window);
            if (_replayHandlers.Count > 0)
                AssetNote.Text += "  handlers=" + _replayHandlers.Count.ToString(CultureInfo.InvariantCulture);
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
            var customPages = _currentWindow?.Pages;
            if (customPages != null && customPages.Count > 0)
            {
                // Data-driven windows (the loading screen) define their own pages:
                // one entry per destination map, no INI Page_* filtering.
                foreach (var page in customPages)
                    PageBox.Items.Add(new ComboBoxItem { Content = page.Label ?? page.Id, Tag = page.Id });
                var customPreferred = customPages.FirstOrDefault(pg =>
                        string.Equals(pg.Id, _currentWindow.Page, StringComparison.OrdinalIgnoreCase)) ?? customPages[0];
                ComboBoxItem customSelected = null;
                foreach (ComboBoxItem item in PageBox.Items)
                {
                    if (string.Equals(item.Tag as string, customPreferred.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        customSelected = item;
                        break;
                    }
                }
                PageBox.SelectedItem = customSelected ?? PageBox.Items[0];
                _updatingPages = false;
                RebuildPlan();
                return;
            }
            var pages = BuildPlan(_currentIni, null).Pages;
            PageBox.Items.Add(new ComboBoxItem { Content = "(all)", Tag = null });
            foreach (var page in pages)
                PageBox.Items.Add(new ComboBoxItem { Content = PageDisplayName(_currentIni, page), Tag = page });
            var engineDefault = UiProcessApp.Engine.LayoutPlanBuilder.DefaultPage(_currentIni);
            var preferred = !string.IsNullOrWhiteSpace(_currentWindow?.Page) && pages.Contains(_currentWindow.Page)
                ? _currentWindow.Page
                : engineDefault != null && pages.Contains(engineDefault) ? engineDefault
                : pages.Contains("Page_DesertStorm") ? "Page_DesertStorm"
                : pages.Count > 0 ? pages[0] : null;
            ComboBoxItem selected = null;
            foreach (ComboBoxItem item in PageBox.Items)
            {
                if (string.Equals(item.Tag as string, preferred, StringComparison.OrdinalIgnoreCase))
                {
                    selected = item;
                    break;
                }
            }
            PageBox.SelectedItem = selected ?? PageBox.Items[0];
            _updatingPages = false;
            RebuildPlan();
        }

        /// <summary>
        /// Chinese selector label for a Page_* entry, taken from the game's own mode
        /// label: Page_X -> CheckBox_X -> its first Text descendant's $Text. Falls back
        /// to the raw page id when the INI authors no label (never machine-translated).
        /// </summary>
        private static string PageDisplayName(IniFile ini, string page)
        {
            var suffix = page.StartsWith("Page_", StringComparison.OrdinalIgnoreCase) ? page.Substring(5) : null;
            if (string.IsNullOrWhiteSpace(suffix)) return page;
            var checkbox = "CheckBox_" + suffix;
            if (!ini.ByName.ContainsKey(checkbox)) return page;
            foreach (var section in ini.Sections)
            {
                if (!string.Equals(section.Get("._WndType"), "Text", StringComparison.OrdinalIgnoreCase)) continue;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cursor = section.Get("._Parent");
                bool under = false;
                while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor))
                {
                    if (string.Equals(cursor, checkbox, StringComparison.OrdinalIgnoreCase)) { under = true; break; }
                    cursor = ini.ByName.TryGetValue(cursor, out var parent) ? parent.Get("._Parent") : null;
                }
                if (!under) continue;
                var raw = section.Get("$Text");
                if (!string.IsNullOrWhiteSpace(raw) && Strings.TryResolve(raw, out var label) &&
                    !string.IsNullOrWhiteSpace(label) &&
                    !string.Equals(label, raw, StringComparison.OrdinalIgnoreCase))
                    return label;
            }
            return page;
        }

        /// <summary>Selected page id (ComboBoxItem.Tag) or null for "(all)".</summary>
        private string SelectedPage()
        {
            if (PageBox.SelectedItem is ComboBoxItem item) return item.Tag as string;
            return PageBox.SelectedItem as string;
        }

        /// <summary>The viewer page state for the current selection, if the window defines pages.</summary>
        private PageState SelectedPageState()
        {
            var pages = _currentWindow?.Pages;
            if (pages == null || pages.Count == 0) return null;
            var id = SelectedPage();
            return pages.FirstOrDefault(pg => string.Equals(pg.Id, id, StringComparison.OrdinalIgnoreCase)) ?? pages[0];
        }

        private void RebuildPlan()
        {
            if (_currentIni == null) { _currentPlan = null; return; }
            var page = SelectedPage();
            if (string.IsNullOrWhiteSpace(page)) page = null;
            // Custom pages are viewer state, not INI Page_* tabs.
            if (_currentWindow?.Pages != null && _currentWindow.Pages.Count > 0) page = null;
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
                var key = LayoutCacheKey(window, CurrentPage(), HideBox.Text);
                if (_replayServer == null && _layoutCache.TryGetValue(key, out var cached))
                {
                    ShowCanvas(cached, (_layoutNotes.TryGetValue(key, out var cachedNote) ? cachedNote : "") + "  (cached)");
                    _lastBuild = null;
                    _elementToSection = null;
                    _checklistCache.TryGetValue(key, out var cachedItems);
                    PopulateItemChecklist(cachedItems ?? new System.Collections.Generic.List<(string Name, string Type)>());
                    SchedulePrewarm();
                    return;
                }
                var sw = Stopwatch.StartNew();
                var canvas = BuildLayoutCanvas(window, _currentIni, CurrentPage(), HideBox.Text, out var note, out var message, out var buildResult);
                sw.Stop();
                if (canvas == null)
                {
                    LayoutHost.Child = ShowMessage(message ?? "Layout render failed.");
                    AssetNote.Text = "";
                    return;
                }
                note = note + "  build=" + sw.ElapsedMilliseconds + "ms";
                StoreLayout(key, canvas, note);
                ShowCanvas(canvas, note);
                _lastBuild = buildResult;
                _elementToSection = buildResult == null
                    ? null
                    : buildResult.Elements.ToDictionary(kv => (object)kv.Value, kv => kv.Key);
                _scrollOffsets.Clear();
                // WndEdit inputs dispatch the script's own OnEditChanged with the typed text.
                if (buildResult != null)
                {
                    foreach (var kv in buildResult.Elements)
                    {
                        if (!(kv.Value is System.Windows.Controls.TextBox editBox)) continue;
                        if (!buildResult.Sections.TryGetValue(kv.Key, out var editSection)) continue;
                        if (!string.Equals(editSection.Get("._WndType"), "WndEdit", StringComparison.OrdinalIgnoreCase)) continue;
                        var editName = kv.Key;
                        var box = editBox;
                        box.TextChanged += (o, args) =>
                        {
                            if (_replayServer == null || _replayIn == null) return;
                            var text = box.Text ?? "";
                            if (text == (box.Tag as string ?? "")) return;
                            SendReplayEvent(editName, "OnEditChanged", false, text);
                        };
                    }
                }
                if (_openedWindows.TryGetValue(window.Id, out var openedNow) && openedNow.Count > 0)
                    AssetNote.Text += "  opens=" + string.Join(",", openedNow.Select(Path.GetFileName));
                // The per-item review checklist reflects the rendered items (INI order).
                if (buildResult != null && _currentIni != null)
                {
                    var items = _currentIni.Sections
                        .Where(s => buildResult.Elements.ContainsKey(s.Name))
                        .Select(s => (Name: s.Name, Type: s.Get("._WndType") ?? ""))
                        .ToList();
                    _checklistCache[key] = items;
                    var issues = CollectItemIssues(buildResult);
                    PopulateItemChecklist(items, issues);
                    _pendingOobBuild = buildResult;
                    _pendingOobItems = items;
                    _pendingOobIssues = issues;
                }
                else
                {
                    _checklistCache.TryGetValue(key, out var cachedItems);
                    PopulateItemChecklist(cachedItems ?? new System.Collections.Generic.List<(string Name, string Type)>());
                }
                SchedulePrewarm();
            }
            catch (Exception ex)
            {
                LayoutHost.Child = ShowMessage("Layout render failed: " + ex.Message);
                AssetNote.Text = "";
            }
        }

        /// <summary>Per-item issues from the build: placeholder art and unresolved string
        /// ids (the reasons shown in the checklist; out-of-bounds is added after measure).
        /// `TextureName=no`/`0` placeholders are intentional (the engine assigns that art
        /// at runtime) and are not flagged.</summary>
        private static System.Collections.Generic.Dictionary<string, string> CollectItemIssues(UiBuildResult build)
        {
            var issues = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in build.Placeholders)
            {
                var name = p.Split(' ')[0];
                if (string.IsNullOrEmpty(name) || issues.ContainsKey(name)) continue;
                var bracket = p.IndexOf('[');
                var detail = bracket >= 0 ? p.Substring(bracket + 1).TrimEnd(']') : "";
                if (detail.Contains("no Image") || detail.StartsWith("Image 0 ") || detail.StartsWith("Image frame"))
                    continue;  // intentional runtime-assigned art
                issues[name] = "缺图";
            }
            foreach (var u in build.UnresolvedStrings)
            {
                var name = u.Split(' ')[0];
                if (!string.IsNullOrEmpty(name) && !issues.ContainsKey(name)) issues[name] = "文案缺失";
            }
            return issues;
        }

        /// <summary>One-shot after each render: flag elements outside the window and
        /// refresh the checklist defaults (user overrides stay).</summary>
        private void OnLayoutUpdated(object sender, EventArgs e)
        {
            if (_pendingOobBuild == null) return;
            var build = _pendingOobBuild;
            _pendingOobBuild = null;
            try
            {
                var rootFe = build.Root as FrameworkElement;
                double width = rootFe != null && rootFe.ActualWidth > 0 ? rootFe.ActualWidth : 0;
                double height = rootFe != null && rootFe.ActualHeight > 0 ? rootFe.ActualHeight : 0;
                if (width <= 0 || height <= 0) return;
                var issues = _pendingOobIssues ?? new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in build.Elements)
                {
                    var el = pair.Value;
                    if (el.Visibility != Visibility.Visible) continue;
                    if (el.ActualWidth <= 0 && el.ActualHeight <= 0) continue;
                    // Scroll content legitimately overflows its viewport (the engine clips
                    // it); only flag items outside the window on a non-scroll path.
                    bool underScroll = false;
                    var cursor = pair.Key;
                    var guard = 0;
                    while (!string.IsNullOrWhiteSpace(cursor) && guard++ < 64)
                    {
                        if (!build.Sections.TryGetValue(cursor, out var sec)) break;
                        if (string.Equals(sec.Get("._WndType"), "WndScroll", StringComparison.OrdinalIgnoreCase))
                        {
                            underScroll = true;
                            break;
                        }
                        cursor = sec.Get("._Parent");
                    }
                    if (underScroll) continue;
                    try
                    {
                        var p = el.TransformToAncestor(build.Root).Transform(new Point(0, 0));
                        double w = el.ActualWidth, h = el.ActualHeight;
                        if (p.X < -1 || p.Y < -1 || p.X + w > width + 1 || p.Y + h > height + 1)
                            if (!issues.ContainsKey(pair.Key)) issues[pair.Key] = "超出窗口";
                    }
                    catch { }
                }
                PopulateItemChecklist(_pendingOobItems ?? new System.Collections.Generic.List<(string Name, string Type)>(), issues);
            }
            catch { }
        }

        /// <summary>Fills the item checklist with the rendered items of the current window
        /// (INI order). Items with a detected issue (placeholder art / unresolved string /
        /// out-of-bounds) default to UNCHECKED with the reason shown; everything else
        /// defaults to checked (it is displayed). Ticks are remembered as explicit
        /// overrides in Data/item_checks.tsv.</summary>
        private void PopulateItemChecklist(System.Collections.Generic.List<(string Name, string Type)> items,
            System.Collections.Generic.Dictionary<string, string> issues = null)
        {
            ClearItemHighlight();
            ItemCheckList.Children.Clear();
            if (_currentWindow == null)
            {
                ItemCheckSummary.Text = "已核对 0 / 0";
                return;
            }
            issues = issues ?? new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int checkedCount = 0;
            foreach (var item in items)
            {
                issues.TryGetValue(item.Name, out var issue);
                bool hasOverride = _itemChecks.TryGetValue(ItemCheckStore.Key(_currentWindow.Id, item.Name), out var ov);
                bool isChecked = hasOverride ? ov : string.IsNullOrEmpty(issue);
                if (isChecked) checkedCount++;
                var row = new System.Windows.Controls.DockPanel { Margin = new Thickness(0, 1, 0, 1) };
                var cb = new System.Windows.Controls.CheckBox
                {
                    IsChecked = isChecked,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 20,
                    Tag = item.Name,
                };
                cb.Checked += OnItemCheckToggled;
                cb.Unchecked += OnItemCheckToggled;
                System.Windows.Controls.DockPanel.SetDock(cb, Dock.Left);
                row.Children.Add(cb);
                var label = new System.Windows.Controls.TextBlock
                {
                    Text = item.Name + "   [" + item.Type + "]" + (string.IsNullOrEmpty(issue) ? "" : "   ⚠ " + issue),
                    Foreground = string.IsNullOrEmpty(issue)
                        ? new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8))
                        : new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x50)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = item.Name,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                label.MouseLeftButtonUp += (o, e) =>
                {
                    var tb = o as System.Windows.Controls.TextBlock;
                    if (tb != null) HighlightItem(tb.Tag as string);
                };
                row.Children.Add(label);
                ItemCheckList.Children.Add(row);
            }
            ItemCheckSummary.Text = string.Format("已核对 {0} / {1}", checkedCount, items.Count);
        }

        private void OnItemCheckToggled(object sender, RoutedEventArgs e)
        {
            if (_currentWindow == null) return;
            var cb = sender as System.Windows.Controls.CheckBox;
            if (cb == null || !(cb.Tag is string name)) return;
            ItemCheckStore.Set(_itemChecks, _currentWindow.Id, name, cb.IsChecked == true);
            ItemCheckStore.Save(Paths.AppRoot, _itemChecks);
            UpdateItemCheckSummary();
        }

        private void UpdateItemCheckSummary()
        {
            int total = 0, done = 0;
            foreach (var child in ItemCheckList.Children)
            {
                if (!(child is System.Windows.Controls.DockPanel row)) continue;
                foreach (var c in row.Children)
                {
                    if (c is System.Windows.Controls.CheckBox cb)
                    {
                        total++;
                        if (cb.IsChecked == true) done++;
                    }
                }
            }
            ItemCheckSummary.Text = string.Format("已核对 {0} / {1}", done, total);
        }

        private void OnItemCheckAll(object sender, RoutedEventArgs e) { SetAllChecks(true); }
        private void OnItemCheckNone(object sender, RoutedEventArgs e) { SetAllChecks(false); }

        private void SetAllChecks(bool value)
        {
            if (_currentWindow == null) return;
            foreach (var child in ItemCheckList.Children)
            {
                if (!(child is System.Windows.Controls.DockPanel row)) continue;
                foreach (var c in row.Children)
                {
                    if (c is System.Windows.Controls.CheckBox cb && cb.Tag is string name)
                    {
                        cb.IsChecked = value;  // fires OnItemCheckToggled (writes the store)
                    }
                }
            }
            UpdateItemCheckSummary();
        }

        /// <summary>Outlines the item's element in the canvas (a yellow overlay on the
        /// element's parent canvas at its position/size).</summary>
        private void HighlightItem(string sectionName)
        {
            ClearItemHighlight();
            if (_lastBuild == null || string.IsNullOrWhiteSpace(sectionName)) return;
            if (!_lastBuild.Elements.TryGetValue(sectionName, out var element)) return;
            var fe = element as FrameworkElement;
            if (fe == null) return;
            var parent = VisualTreeHelper.GetParent(fe) as System.Windows.Controls.Panel;
            if (parent == null) return;
            double x = Canvas.GetLeft(fe); if (double.IsNaN(x)) x = 0;
            double y = Canvas.GetTop(fe); if (double.IsNaN(y)) y = 0;
            double w = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
            double h = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
            if (double.IsNaN(w) || w <= 0) w = 40;
            if (double.IsNaN(h) || h <= 0) h = 20;
            _checkHighlight = new System.Windows.Controls.Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD0, 0x40)),
                BorderThickness = new Thickness(2),
                Width = w + 2,
                Height = h + 2,
                IsHitTestVisible = false,
                Background = new SolidColorBrush(Color.FromArgb(30, 0xFF, 0xD0, 0x40)),
            };
            Canvas.SetLeft(_checkHighlight, x - 1);
            Canvas.SetTop(_checkHighlight, y - 1);
            parent.Children.Add(_checkHighlight);
        }

        private void ClearItemHighlight()
        {
            if (_checkHighlight == null) return;
            if (VisualTreeHelper.GetParent(_checkHighlight) is System.Windows.Controls.Panel p)
                p.Children.Remove(_checkHighlight);
            _checkHighlight = null;
        }

        /// <summary>Starts the per-window interaction server (tools/ui/replay_server.lua):
        /// the client's own event handlers run offline and their mutation deltas feed the
        /// render. Only windows whose replay completed (replay_summary.tsv OK) qualify.</summary>
        private void StartReplayServer(WindowInfo window)
        {
            StopReplayServer();
            _replayHandlers.Clear();
            _replayWindowId = null;
            if (window == null || string.IsNullOrWhiteSpace(window.Path)) return;
            var stem = Path.GetFileNameWithoutExtension(window.Path);
            if (string.IsNullOrWhiteSpace(stem)) return;
            var iniPath = ResolveIniPath(window);
            var luaPath = Path.Combine(Paths.AppRoot, "assets", "ui", "Config", "Default", stem + ".lua");
            if (iniPath == null || !File.Exists(iniPath) || !File.Exists(luaPath)) return;
            var summary = Path.Combine(Paths.AppRoot, "Data", "runtime_state", "replay_summary.tsv");
            if (!File.Exists(summary)) return;
            var completed = false;
            foreach (var line in File.ReadAllLines(summary))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 2 && string.Equals(parts[0], stem, StringComparison.OrdinalIgnoreCase))
                {
                    completed = parts[1].StartsWith("OK", StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }
            if (!completed) return;
            var lua32 = Environment.GetEnvironmentVariable("LUA32");
            if (string.IsNullOrWhiteSpace(lua32))
                lua32 = @"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\lua-5.1.5\lua-5.1.5\build32\lua32.exe";
            var server = Path.Combine(Paths.RepoRoot ?? "", "tools", "ui", "replay_server.lua");
            if (!File.Exists(lua32) || !File.Exists(server)) return;
            try
            {
                var psi = new ProcessStartInfo(lua32,
                    "\"" + server + "\" \"" + luaPath + "\" auto \"" + iniPath + "\"")
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                _replayServer = Process.Start(psi);
                if (_replayServer == null) return;
                _replayIn = _replayServer.StandardInput;
                _replayOut = _replayServer.StandardOutput;
                var ready = _replayOut.ReadLine() ?? "";
                if (ready.StartsWith("READY handlers=", StringComparison.Ordinal))
                {
                    foreach (var h in ready.Substring("READY handlers=".Length)
                                 .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        _replayHandlers.Add(h.Trim());
                    _replayWindowId = window.Id;
                }
            }
            catch
            {
                StopReplayServer();
            }
        }

        private void StopReplayServer()
        {
            try { if (_replayIn != null) { _replayIn.Close(); _replayIn = null; } } catch { }
            try { if (_replayOut != null) { _replayOut.Close(); _replayOut = null; } } catch { }
            try
            {
                if (_replayServer != null)
                {
                    if (!_replayServer.HasExited) _replayServer.Kill();
                    _replayServer.Dispose();
                }
            }
            catch { }
            _replayServer = null;
        }

        /// <summary>Press → hit-test the built element tree → arm the engine's own
        /// pointer sequence: scrollbar thumb drag, the drag-handle family
        /// (OnDragButtonBegin) or the item sequence (OnItemLButtonDown). The click
        /// handler itself fires on release, as in the engine.</summary>
        private void OnLayoutClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (TryBeginWindowDrag(e)) return;
            if (_replayServer == null || _replayIn == null || _replayOut == null) return;
            if (_lastBuild == null || _elementToSection == null || _currentWindow == null) return;
            if (!string.Equals(_currentWindow.Id, _replayWindowId, StringComparison.OrdinalIgnoreCase)) return;
            var pos = e.GetPosition(LayoutHost);
            var sectionName = HitTestSection(pos);
            if (sectionName == null) return;
            _pointerDownSection = sectionName;
            _pointerDownPoint = pos;
            _pointerDragging = false;
            _handleDragActive = false;
            _handleDragSection = null;
            // Scrollbar: a press on the bar (not inside its content handle) starts a
            // native thumb drag bound to the wheel's offset.
            if (TryBeginScrollDrag(sectionName, pos)) { e.Handled = true; return; }
            // Drag handle (the control the script registered via RegisterLButtonDrag,
            // e.g. Btn_Drag): the engine's OnDragButton family with `this` = the handle.
            if (IsDragHandle(sectionName) && _replayHandlers.Contains("OnDragButtonBegin"))
            {
                _handleDragActive = true;
                _handleDragSection = sectionName;
                SendReplayEvent(sectionName, "OnDragButtonBegin", false, null, pos);
                e.Handled = true;
                return;
            }
            // Items: OnItemLButtonDown fires on press (the click fires on release).
            if (IsItemSection(sectionName) && _replayHandlers.Contains("OnItemLButtonDown"))
                SendReplayEvent(sectionName, "OnItemLButtonDown", false, null, pos);
        }

        private static bool IsItemSection(string section)
        {
            return section.StartsWith("Box_", StringComparison.OrdinalIgnoreCase) ||
                   section.StartsWith("__lt_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The control the window script registered for left-button drag
        /// (`RegisterLButtonDrag` -> $DragRegistered, e.g. BigBagPanel's Btn_Drag);
        /// the engine fires the OnDragButton family with `this` = that control.</summary>
        private bool IsDragHandle(string section)
        {
            if (_lastBuild == null || string.IsNullOrWhiteSpace(section)) return false;
            if (!_lastBuild.Sections.TryGetValue(section, out var sec)) return false;
            return sec.Get("$DragRegistered") == "1" || sec.Get("$DragEnabled") == "1";
        }

        private string PickHandler(string section)
        {
            if (section.StartsWith("CheckBox_", StringComparison.OrdinalIgnoreCase) &&
                _replayHandlers.Contains("OnCheckBoxCheck")) return "OnCheckBoxCheck";
            if (IsItemSection(section) && _replayHandlers.Contains("OnItemLButtonClick")) return "OnItemLButtonClick";
            if (_replayHandlers.Contains("OnLButtonClick")) return "OnLButtonClick";
            return null;
        }

        /// <summary>Hover dispatches OnMouseLeave/OnMouseEnter when the section under
        /// the cursor changes; while a press is armed the engine's drag events replace
        /// hover (item drag, drag handle, scrollbar thumb).</summary>
        private void OnLayoutMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_windowDragActive)
            {
                var pos = e.GetPosition(LayoutHost);
                if (LayoutHost.Child is FrameworkElement dragCanvas)
                {
                    dragCanvas.RenderTransform = new System.Windows.Media.TranslateTransform(
                        _windowDragOffX + (pos.X - _windowDragStart.X),
                        _windowDragOffY + (pos.Y - _windowDragStart.Y));
                }
                e.Handled = true;
                return;
            }
            if (_replayServer == null || _replayIn == null || _replayOut == null) return;
            if (_lastBuild == null || _elementToSection == null || _currentWindow == null) return;
            if (!string.Equals(_currentWindow.Id, _replayWindowId, StringComparison.OrdinalIgnoreCase)) return;
            var point = e.GetPosition(LayoutHost);
            bool leftDown = e.LeftButton == System.Windows.Input.MouseButtonState.Pressed;
            if (_scrollDragName != null && leftDown)
            {
                DragScrollThumb(point);
                e.Handled = true;
                return;
            }
            if (_handleDragActive && leftDown && _handleDragSection != null)
            {
                if (ShouldDispatchDrag())
                    SendReplayEvent(_handleDragSection, "OnDragButton", false, null, point);
                e.Handled = true;
                return;
            }
            if (_pointerDownSection != null && leftDown && IsItemSection(_pointerDownSection) &&
                _replayHandlers.Contains("OnItemLButtonDrag"))
            {
                var dx = point.X - _pointerDownPoint.X;
                var dy = point.Y - _pointerDownPoint.Y;
                // The engine's own threshold: distance² > 0xa (KGUIX64 0x180158c4d).
                if (_pointerDragging || (dx * dx + dy * dy) >= 10)
                {
                    _pointerDragging = true;
                    if (ShouldDispatchDrag())
                        SendReplayEvent(_pointerDownSection, "OnItemLButtonDrag", false, null, point);
                }
                e.Handled = true;
                return;
            }
            var section = HitTestSection(point);
            if (string.Equals(section, _hoverSection, StringComparison.Ordinal)) return;
            var previous = _hoverSection;
            _hoverSection = section;
            if (previous != null && _replayHandlers.Contains("OnMouseLeave"))
                SendReplayEvent(previous, "OnMouseLeave");
            if (section != null && _replayHandlers.Contains("OnMouseEnter"))
                SendReplayEvent(section, "OnMouseEnter");
        }

        /// <summary>Engine drag events fire per mouse-move message; the viewer's
        /// round trip + re-render is heavier, so throttle repeated drag dispatches.</summary>
        private bool ShouldDispatchDrag()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastDragDispatchUtc).TotalMilliseconds < 40) return false;
            _lastDragDispatchUtc = now;
            return true;
        }

        private string HitTestSection(Point point)
        {
            try
            {
                var hit = VisualTreeHelper.HitTest(LayoutHost, point);
                var visual = hit?.VisualHit;
                while (visual != null)
                {
                    if (_elementToSection.TryGetValue(visual, out var name)) return name;
                    visual = VisualTreeHelper.GetParent(visual);
                }
            }
            catch { }
            return null;
        }

        /// <summary>The engine's moveable windows: a mousedown on the root background
        /// (within its SetDragArea when one is recorded) starts a window move; the
        /// canvas is translated by the drag delta (viewer affordance for Moveable=1).</summary>
        private bool TryBeginWindowDrag(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_lastBuild == null || _elementToSection == null) return false;
            var sectionName = HitTestSection(e.GetPosition(LayoutHost));
            if (sectionName == null) return false;
            IniSection rootSec = null;
            foreach (var kv in _lastBuild.Elements)
            {
                if (!ReferenceEquals(kv.Value, _lastBuild.Root)) continue;
                _lastBuild.Sections.TryGetValue(kv.Key, out rootSec);
                break;
            }
            if (rootSec == null || !string.Equals(sectionName, rootSec.Name, StringComparison.OrdinalIgnoreCase)) return false;
            bool moveable = rootSec.GetInt("Moveable") == 1 || rootSec.Get("$DragEnabled") == "1" ||
                            rootSec.Get("$DragArea") != null;
            if (!moveable) return false;
            var pos = e.GetPosition(LayoutHost);
            var area = rootSec.Get("$DragArea");
            if (!string.IsNullOrWhiteSpace(area))
            {
                var parts = area.Split(',');
                if (parts.Length >= 4 &&
                    double.TryParse(parts[0], out var l) && double.TryParse(parts[1], out var t) &&
                    double.TryParse(parts[2], out var r) && double.TryParse(parts[3], out var b))
                {
                    double w = rootSec.GetDouble("Width"), h = rootSec.GetDouble("Height");
                    if (pos.X < l || pos.Y < t || (w > 0 && pos.X > w - r) || (h > 0 && pos.Y > h - b)) return false;
                }
            }
            _windowDragActive = true;
            _windowDragStart = pos;
            if (LayoutHost.Child is FrameworkElement canvas && canvas.RenderTransform is System.Windows.Media.TranslateTransform tt)
            {
                _windowDragOffX = tt.X;
                _windowDragOffY = tt.Y;
            }
            else
            {
                _windowDragOffX = 0;
                _windowDragOffY = 0;
            }
            e.Handled = true;
            return true;
        }

        /// <summary>Release: finish the engine's pointer sequence — the item drag
        /// (OnItemLButtonUp then OnItemLButtonDragEnd, the engine's up order) or the
        /// click handler when no drag happened; the drag handle ends its own family.</summary>
        private void OnLayoutMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _windowDragActive = false;
            if (_scrollDragName != null)
            {
                _scrollDragName = null;
                return;
            }
            if (_handleDragActive)
            {
                if (_handleDragSection != null && _replayHandlers.Contains("OnDragButtonEnd"))
                    SendReplayEvent(_handleDragSection, "OnDragButtonEnd", false, null, e.GetPosition(LayoutHost));
                _handleDragActive = false;
                _handleDragSection = null;
                _pointerDownSection = null;
                return;
            }
            if (_pointerDownSection != null && _pointerDragging && IsItemSection(_pointerDownSection))
            {
                if (_replayHandlers.Contains("OnItemLButtonUp"))
                    SendReplayEvent(_pointerDownSection, "OnItemLButtonUp", false, null, e.GetPosition(LayoutHost));
                if (_replayHandlers.Contains("OnItemLButtonDragEnd"))
                    SendReplayEvent(_pointerDownSection, "OnItemLButtonDragEnd", false, null, e.GetPosition(LayoutHost));
            }
            else if (_pointerDownSection != null)
            {
                var handler = PickHandler(_pointerDownSection);
                if (handler != null) SendReplayEvent(_pointerDownSection, handler, true, null, e.GetPosition(LayoutHost));
            }
            _pointerDownSection = null;
            _pointerDragging = false;
        }

        private void OnLayoutMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (_lastBuild == null || _elementToSection == null || _currentWindow == null) return;
            var sectionName = HitTestSection(e.GetPosition(LayoutHost));
            if (sectionName == null) return;
            var scrollName = FindScrollForSection(sectionName);
            if (scrollName == null) return;
            if (!TryGetScrollContent(scrollName, out var scrollSec, out var content)) return;
            double viewport = scrollSec.GetInt("Height");
            double contentH = content.Height;
            if (double.IsNaN(contentH) || contentH <= 0) contentH = content.ActualHeight;
            double max = Math.Max(0, contentH - viewport);
            if (max <= 0) return;
            double step = scrollSec.GetInt("ScrollStep", 40);
            double offset = _scrollOffsets.TryGetValue(scrollName, out var current) ? current : 0;
            ApplyScrollOffset(scrollName, offset - Math.Sign(e.Delta) * step);
            e.Handled = true;
        }

        /// <summary>Finds the scroll control a section scrolls with: a WndScroll /
        /// WndNewScrollBar ancestor, else the bar whose RegisterScrollControl binding
        /// (tList content handle) contains the section.</summary>
        private string FindScrollForSection(string sectionName)
        {
            var cursor = sectionName;
            var guard = 0;
            while (!string.IsNullOrWhiteSpace(cursor) && guard++ < 64)
            {
                if (!_lastBuild.Sections.TryGetValue(cursor, out var sec)) break;
                var type = sec.Get("._WndType") ?? "";
                if (type.Equals("WndScroll", StringComparison.OrdinalIgnoreCase) ||
                    type.Equals("WndNewScrollBar", StringComparison.OrdinalIgnoreCase)) return cursor;
                cursor = sec.Get("._Parent");
            }
            foreach (var kv in _lastBuild.Sections)
            {
                var target = kv.Value.Get("$ScrollTarget");
                if (string.IsNullOrWhiteSpace(target)) continue;
                if (IsUnderSection(sectionName, target, null)) return kv.Key;
            }
            return null;
        }

        /// <summary>A press on a scrollbar arms a thumb drag (the wheel offset is the
        /// shared state). Only the bar control counts: a `WndNewScrollBar` (or a
        /// viewport's `SlideBtn`) — a `WndScroll` is the viewport and its children are
        /// the content, so a press there stays a normal item/click event (1026 shipped
        /// scroll controls carry no ScrollHandle; arming on them would swallow clicks).</summary>
        private bool TryBeginScrollDrag(string sectionName, System.Windows.Point pos)
        {
            var scrollName = FindScrollForSection(sectionName);
            if (scrollName == null) return false;
            if (!_lastBuild.Sections.TryGetValue(scrollName, out var scrollSec)) return false;
            var type = scrollSec.Get("._WndType") ?? "";
            bool isBar = type.Equals("WndNewScrollBar", StringComparison.OrdinalIgnoreCase);
            if (!isBar)
            {
                // A viewport can carry its own slide button; only a press on that button
                // (not on arbitrary content) is a thumb drag.
                var slide = scrollSec.Get("SlideBtn");
                if (string.IsNullOrWhiteSpace(slide) || !IsUnderSection(sectionName, slide, scrollName)) return false;
            }
            // The content must be known (RegisterScrollControl binding or the authored
            // ScrollHandle). Without it, the first-child fallback is the bar's own track,
            // so a "drag" would translate the wrong element — do nothing instead.
            var target = scrollSec.Get("$ScrollTarget");
            if (string.IsNullOrWhiteSpace(target)) target = scrollSec.Get("ScrollHandle");
            if (string.IsNullOrWhiteSpace(target)) return false;
            // A press on a content item (inside the target handle) is an item event,
            // not a thumb drag.
            if (IsUnderSection(sectionName, target, scrollName)) return false;
            if (!TryGetScrollContent(scrollName, out _, out _)) return false;
            _scrollDragName = scrollName;
            _scrollDragStartY = pos.Y;
            _scrollDragStartOffset = _scrollOffsets.TryGetValue(scrollName, out var current) ? current : 0;
            return true;
        }

        private void DragScrollThumb(System.Windows.Point point)
        {
            if (!TryGetScrollContent(_scrollDragName, out var scrollSec, out var content)) return;
            double viewport = scrollSec.GetInt("Height");
            double contentH = content.Height;
            if (double.IsNaN(contentH) || contentH <= 0) contentH = content.ActualHeight;
            double max = Math.Max(0, contentH - viewport);
            double ratio = viewport > 0 ? max / Math.Max(1.0, viewport) : 1.0;
            ApplyScrollOffset(_scrollDragName, _scrollDragStartOffset + (_scrollDragStartY - point.Y) * Math.Max(1.0, ratio));
        }

        /// <summary>Resolves a scroll control's content element: the RegisterScrollControl
        /// target handle (or the authored ScrollHandle), else its first child panel.</summary>
        private bool TryGetScrollContent(string scrollName, out IniSection scrollSec, out FrameworkElement content)
        {
            scrollSec = null;
            content = null;
            if (_lastBuild == null || string.IsNullOrWhiteSpace(scrollName)) return false;
            if (!_lastBuild.Sections.TryGetValue(scrollName, out scrollSec)) return false;
            var handleName = scrollSec.Get("$ScrollTarget");
            if (string.IsNullOrWhiteSpace(handleName)) handleName = scrollSec.Get("ScrollHandle");
            if (!string.IsNullOrWhiteSpace(handleName) && _lastBuild.Elements.TryGetValue(handleName, out var he))
                content = he;
            if (content == null && _lastBuild.Elements.TryGetValue(scrollName, out var se) &&
                se is System.Windows.Controls.Panel sp && sp.Children.Count > 0)
                content = sp.Children[0] as FrameworkElement;
            return content != null;
        }

        private bool ApplyScrollOffset(string scrollName, double offset)
        {
            if (!TryGetScrollContent(scrollName, out var scrollSec, out var content)) return false;
            double viewport = scrollSec.GetInt("Height");
            double contentH = content.Height;
            if (double.IsNaN(contentH) || contentH <= 0) contentH = content.ActualHeight;
            double max = Math.Max(0, contentH - viewport);
            offset = Math.Max(0, Math.Min(max, offset));
            _scrollOffsets[scrollName] = offset;
            content.RenderTransform = new System.Windows.Media.TranslateTransform(0, -offset);
            return true;
        }

        /// <summary>True when `sectionName` is `ancestorName` or below it (walking the
        /// INI parent chain; stops at `stopAt` when given).</summary>
        private bool IsUnderSection(string sectionName, string ancestorName, string stopAt)
        {
            var cursor = sectionName;
            var guard = 0;
            while (!string.IsNullOrWhiteSpace(cursor) && guard++ < 128)
            {
                if (string.Equals(cursor, ancestorName, StringComparison.OrdinalIgnoreCase)) return true;
                if (stopAt != null && string.Equals(cursor, stopAt, StringComparison.OrdinalIgnoreCase)) return false;
                if (!_lastBuild.Sections.TryGetValue(cursor, out var sec)) return false;
                cursor = sec.Get("._Parent");
            }
            return false;
        }
        private void SendReplayEvent(string section, string handler, bool navigate = false, string arg = null,
                                     System.Windows.Point? pos = null)
        {
            try
            {
                var openedBefore = _openedWindows.TryGetValue(_currentWindow.Id, out var ob) ? ob.Count : 0;
                if (pos.HasValue)
                {
                    // The engine's Station.GetClientSize/GetMessagePos environment: the
                    // scripts clamp positions and delta script-driven drags with these.
                    var hostW = LayoutHost.ActualWidth > 0 ? LayoutHost.ActualWidth : 1920;
                    var hostH = LayoutHost.ActualHeight > 0 ? LayoutHost.ActualHeight : 1080;
                    _replayIn.WriteLine("CLIENT " + (int)hostW + " " + (int)hostH);
                    _replayIn.WriteLine("MOUSE " + (int)pos.Value.X + " " + (int)pos.Value.Y);
                }
                _replayIn.WriteLine("EVENT " + section + " " + handler + (arg == null ? "" : " " + arg));
                _replayIn.Flush();
                var lines = new List<string>();
                for (int i = 0; i < 5000; i++)
                {
                    var line = _replayOut.ReadLine();
                    if (line == null || line == "END") break;
                    if (line.StartsWith("RESULT ", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("WINDOW ", StringComparison.Ordinal))
                    {
                        if (!_openedWindows.TryGetValue(_currentWindow.Id, out var opened))
                            _openedWindows[_currentWindow.Id] = opened = new List<string>();
                        opened.Add(line.Substring("WINDOW ".Length).Trim());
                        continue;
                    }
                    if (line.IndexOf('\t') >= 0) lines.Add(line);
                }
                if (lines.Count > 0)
                {
                    if (!_runtimeOverlays.TryGetValue(_currentWindow.Id, out var overlay))
                        _runtimeOverlays[_currentWindow.Id] = overlay = new List<string>();
                    overlay.AddRange(lines);
                    _layoutCache.Clear();
                    _layoutOrder.Clear();
                    _layoutNotes.Clear();
                    RenderLayout(_currentWindow);
                }
                if (navigate) NavigateAfterEvent(openedBefore);
            }
            catch
            {
                StopReplayServer();
            }
        }

        /// <summary>Follow a click that opened another window (the game would show it) or
        /// closed the current one (pop the history). No-op when the path has no catalog entry.</summary>
        private void NavigateAfterEvent(int openedBefore)
        {
            if (_currentWindow == null) return;
            if (!_openedWindows.TryGetValue(_currentWindow.Id, out var opened)) return;
            for (int i = openedBefore; i < opened.Count; i++)
            {
                var entry = opened[i];
                if (entry.StartsWith("-", StringComparison.Ordinal))
                {
                    if (_windowHistory.Count > 0)
                    {
                        var back = _windowHistory[_windowHistory.Count - 1];
                        _windowHistory.RemoveAt(_windowHistory.Count - 1);
                        NavigateToWindow(back);
                    }
                    return;
                }
                var target = FindWindowByIniPath(entry);
                if (target != null && !string.Equals(target.Id, _currentWindow.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _windowHistory.Add(_currentWindow.Id);
                    NavigateToWindow(target.Id);
                    return;
                }
            }
        }

        /// <summary>Maps a recorded window-open intent to the catalog: the shim records
        /// ini paths (Wnd.OpenWindow), module names (SomePanel.OpenWindow) and opener
        /// globals (OpenBankPanel -> BigBankPanel via Data/ui_window_aliases.tsv).</summary>
        private WindowInfo FindWindowByIniPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var stem = Path.GetFileNameWithoutExtension(Path.GetFileName(path.Replace('\\', '/'))).ToLowerInvariant();
            if (string.IsNullOrEmpty(stem)) return null;
            var candidates = new List<string> { stem };
            if (_windowAliases.TryGetValue(stem, out var aliased)) candidates.Add(aliased.ToLowerInvariant());
            if (stem.StartsWith("open", StringComparison.Ordinal) && stem.Length > 4) candidates.Add(stem.Substring(4));
            if (stem.StartsWith("close", StringComparison.Ordinal) && stem.Length > 5) candidates.Add(stem.Substring(5));
            foreach (var stage in _inventory.Stages)
                foreach (var w in stage.Windows ?? new List<WindowInfo>())
                {
                    var wn = Path.GetFileNameWithoutExtension((w.Path ?? "").Replace('\\', '/')).ToLowerInvariant();
                    var wid = (w.Id ?? "").ToLowerInvariant();
                    foreach (var c in candidates)
                        if (c == wn || c == wid) return w;
                }
            return null;
        }

        /// <summary>Loads the opener-alias index scanned from the window scripts'
        /// SETGLOBAL definitions (tools/ui/scan_window_aliases.py).</summary>
        private static Dictionary<string, string> LoadWindowAliases()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var path = Path.Combine(Paths.AppRoot, "Data", "ui_window_aliases.tsv");
                if (!File.Exists(path)) return map;
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                        map[parts[0].Trim()] = parts[1].Trim();
                }
            }
            catch { }
            return map;
        }

        private void NavigateToWindow(string id)
        {
            foreach (TreeViewItem stageNode in StageTree.Items)
                foreach (TreeViewItem node in stageNode.Items)
                    if (node.Tag is WindowInfo wi && string.Equals(wi.Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        node.IsSelected = true;
                        node.BringIntoView();
                        return;
                    }
        }

        private void ShowCanvas(Canvas canvas, string note)
        {
            _layoutCanvas = canvas;
            LayoutScroll.UpdateLayout();
            _zoom = FitZoom(canvas.Width, canvas.Height);
            canvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
            LayoutHost.Child = canvas;
            _settingZoom = true;
            if (Math.Abs(ZoomSlider.Value - _zoom) > 0.001) ZoomSlider.Value = _zoom;
            _settingZoom = false;
            LayoutScroll.ScrollToHome();
            AssetNote.Text = note + StatusNote(_currentWindow);
        }

        private static string LayoutCacheKey(WindowInfo window, string page, string hideText)
        {
            return window.Id + "|" + (page ?? "(all)") + "|" + (UiLayout.Wireframe ? "wire" : "art") +
                   "|" + (hideText ?? "") + "|" + (window.Hide ?? "");
        }

        private void StoreLayout(string key, Canvas canvas, string note)
        {
            if (_layoutCache.Count >= LayoutCacheCap && _layoutOrder.Count > 0)
            {
                var oldest = _layoutOrder[0];
                _layoutOrder.RemoveAt(0);
                _layoutCache.Remove(oldest);
                _layoutNotes.Remove(oldest);
            }
            if (!_layoutCache.ContainsKey(key)) _layoutOrder.Add(key);
            _layoutCache[key] = canvas;
            _layoutNotes[key] = note;
        }

        /// <summary>Builds the arranged canvas for a window/page/hide combination. Reads no
        /// UI state (CurrentPage/HideBox are passed in) so it is safe to call for the
        /// prewarm of another window.</summary>
        private Canvas BuildLayoutCanvas(WindowInfo window, IniFile sourceIni, string page, string hideText,
                                         out string note, out string message, out UiBuildResult buildResult)
        {
            note = "";
            message = null;
            buildResult = null;
            var plan = LayoutPlanBuilder.Build(sourceIni, page);
            LayoutPlanBuilder.ApplyAppendIni(plan.Filtered, window.AppendIni, App.LoadIniTolerant);
            var runtimeApplied = LayoutPlanBuilder.ApplyRuntimeState(plan.Filtered, window.Path);
            if (_runtimeOverlays.TryGetValue(window.Id, out var overlay) && overlay.Count > 0)
                runtimeApplied += LayoutPlanBuilder.ApplyRuntimeMutations(plan.Filtered, overlay);
            LayoutPlanBuilder.ApplyHide(plan.Filtered, hideText);
            LayoutPlanBuilder.ApplySkin(plan.Filtered, window.Skin ?? "uitimate");
            LayoutPlanBuilder.ApplyAnchors(plan.Filtered, window.Anchors);
            LayoutPlanBuilder.ApplyTabs(plan.Filtered, window.Tabs, page ?? window.Page);
            LayoutPlanBuilder.ApplyListTemplates(plan.Filtered, window.Lists, App.LoadTemplateIni);
            LayoutPlanBuilder.ApplyLockedVisibility(plan.Filtered, App.ScriptShown(window));
            LayoutPlanBuilder.ApplyAppends(plan.Filtered, window.Appends);
            LayoutPlanBuilder.ApplyTexts(plan.Filtered, window.Texts);
            LayoutPlanBuilder.ApplyImages(plan.Filtered, window.Images);
            LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, window.Adjust);
            PageState pageState = null;
            if (window.Pages != null && window.Pages.Count > 0)
                pageState = window.Pages.FirstOrDefault(pg =>
                                string.Equals(pg.Id, page, StringComparison.OrdinalIgnoreCase)) ?? window.Pages[0];
            if (pageState != null)
            {
                LayoutPlanBuilder.ApplyTexts(plan.Filtered, pageState.Texts);
                LayoutPlanBuilder.ApplyImages(plan.Filtered, pageState.Images);
                LayoutPlanBuilder.ApplyAdjustments(plan.Filtered, pageState.Adjust);
                if (pageState.Hide != null && pageState.Hide.Count > 0)
                    LayoutPlanBuilder.ApplyHide(plan.Filtered, string.Join(",", pageState.Hide));
            }
            if (plan.Filtered.Sections.Count == 0)
            {
                message = "All sections are hidden (check the 隐藏 list).";
                return null;
            }
            var ini = plan.Filtered;
            var build = UiLayout.Build(ini, SharedAssets, SharedTextures);
            buildResult = build;
            var overlayRoot = App.BuildOverlayVisual(window, SharedAssets, SharedTextures, UiLayout.Wireframe);

            double width = ini.Sections[0].GetInt("Width");
            double height = ini.Sections[0].GetInt("Height");
            if (width <= 0) width = 1280;
            if (height <= 0) height = 720;

            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                Background = App.BackdropBrush(window),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            if (overlayRoot != null && (window.Overlay == null || window.Overlay.Front != true))
                canvas.Children.Add(overlayRoot);
            canvas.Children.Add(build.Root);
            if (overlayRoot != null && window.Overlay != null && window.Overlay.Front == true)
                canvas.Children.Add(overlayRoot);
            double offsetX = window.OffsetX ?? 0;
            double offsetY = window.OffsetY ?? 0;
            if (offsetX != 0 || offsetY != 0)
            {
                // Window placement on the client; only the main window shifts.
                Canvas.SetLeft(build.Root, offsetX);
                Canvas.SetTop(build.Root, offsetY);
                width += Math.Max(0, offsetX);
                height += Math.Max(0, offsetY);
                canvas.Width = width;
                canvas.Height = height;
            }
            // Clip the window to its own rect: the engine has no frame clip, but this is a
            // single-window review tool, and the scripts park popups / slide bars off-window
            // (SetRelPos) and the INI authors overhang; expanding the canvas revealed all of
            // it, so the reviewer saw "components outside the window". The off-window content
            // is still measured by --audit (oob classes); the render just stops showing it.
            canvas.Width = width;
            canvas.Height = height;
            canvas.ClipToBounds = true;
            canvas.Measure(new Size(width, height));
            canvas.Arrange(new Rect(0, 0, width, height));
            canvas.UpdateLayout();
            note = $"page={page ?? "(all)"}  size={width:0}x{height:0}  " +
                   $"sections={ini.Sections.Count}  rendered={CountVisible(build.Root)}  " +
                   (runtimeApplied > 0 ? $"runtime={runtimeApplied}  " : "") +
                   $"art={(Paths.ProofUiRoot != null ? "on" : "missing")}";
            return canvas;
        }

        private AssetResolver SharedAssets =>
            _sharedAssets ?? (_sharedAssets = new AssetResolver(Paths.ResolveRoots()));
        private UiTexCache SharedTextures =>
            _sharedTextures ?? (_sharedTextures = new UiTexCache(SharedAssets));

        private IniFile GetIni(WindowInfo window)
        {
            var path = ResolveIniPath(window);
            if (path == null || !File.Exists(path)) return null;
            if (_iniCache.TryGetValue(path, out var cached)) return cached;
            var ini = TryLoadIni(path);
            if (ini != null)
            {
                if (_iniCache.Count > 32) _iniCache.Clear();
                _iniCache[path] = ini;
            }
            return ini;
        }

        /// <summary>Builds the next catalog window's layout while the user looks at the
        /// current one, so the next X / selection renders from cache.</summary>
        private void SchedulePrewarm()
        {
            if (_prewarmQueued) return;
            _prewarmQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _prewarmQueued = false;
                var current = _currentWindow;
                if (current == null) return;
                var next = NextWindow(current);
                if (next == null || ReferenceEquals(next, current)) return;
                var page = string.IsNullOrWhiteSpace(next.Page) ? null : next.Page;
                if (next.Pages != null && next.Pages.Count > 0 &&
                    !next.Pages.Any(pg => string.Equals(pg.Id, page, StringComparison.OrdinalIgnoreCase)))
                    page = next.Pages[0].Id;
                var key = LayoutCacheKey(next, page, next.Hide ?? "");
                if (_layoutCache.ContainsKey(key)) return;
                try
                {
                    var ini = GetIni(next);
                    if (ini == null) return;
                    var canvas = BuildLayoutCanvas(next, ini, page, next.Hide ?? "", out var note, out _, out _);
                    if (canvas != null) StoreLayout(key, canvas, note);
                }
                catch
                {
                    // prewarm is best-effort; a failed build simply renders on selection
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private WindowInfo NextWindow(WindowInfo window)
        {
            var flat = _inventory.Stages
                .SelectMany(s => s.Windows ?? new List<WindowInfo>())
                .ToList();
            int index = flat.IndexOf(window);
            if (index < 0) return null;
            return index + 1 < flat.Count ? flat[index + 1] : (index > 0 ? flat[index - 1] : null);
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

        /// <summary>X marks the currently shown window as 不需要 (or restores it); the
        /// rejected set is kept in Data/rejected.tsv so the catalog JSON stays clean.</summary>
        private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // With a Chinese IME active the letter arrives as Key.ImeProcessed and the
            // real key lives in ImeProcessedKey; accept both spellings.
            var key = e.Key == System.Windows.Input.Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (key != System.Windows.Input.Key.X || _currentWindow == null) return;
            if (System.Windows.Input.Keyboard.FocusedElement is TextBox ||
                System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
            if (e.KeyboardDevice.Modifiers != System.Windows.Input.ModifierKeys.None) return;

            var window = _currentWindow;

            // Advance the selection to the next window in the list (previous one at the
            // end) so rejecting a run of windows needs no scrolling back to the spot.
            WindowInfo next = NextWindow(window);

            RejectionStore.Toggle(_inventory, window, _rejected);
            RejectionStore.Save(Paths.AppRoot, _rejected);
            BuildTree(SearchBox.Text.Trim());

            var select = next ?? window;
            foreach (TreeViewItem stageNode in StageTree.Items)
            {
                bool found = false;
                foreach (TreeViewItem node in stageNode.Items)
                {
                    if (!ReferenceEquals(node.Tag, select)) continue;
                    node.IsSelected = true;
                    node.BringIntoView();
                    found = true;
                    break;
                }
                if (found) break;
            }
            StatusText.Text = (RejectionStore.IsRejected(_inventory, window) ? "已标记不需要：" : "已移回原分类：") +
                              (string.IsNullOrWhiteSpace(window.Cn) ? window.Title : window.Cn);
            e.Handled = true;
        }

        private string CurrentPage()
        {
            var page = SelectedPage();
            return string.IsNullOrWhiteSpace(page) ? null : page;
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
                Text = "• " + text,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(14, 1, 0, 1),
            });
    }

    public sealed class Inventory
    {
        public string Title { get; set; }
        public string Generated { get; set; }
        /// <summary>Window id the viewer opens on (the catalog item currently
        /// being worked on — see ui-process-app/AGENTS.md).</summary>
        public string DefaultWindow { get; set; }
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
        public List<PageState> Pages { get; set; }
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
        /// <summary>Second INIs appended under a handle at runtime (TargetCommon into
        /// the target frame's Handle_Energy; see LayoutPlan.AppendIniSpec).</summary>
        public List<AppendIniSpec> AppendIni { get; set; }
        /// <summary>Window placement on the client (AnchorDst=client): the client
        /// draws this window at this offset (e.g. the MiddleMap sits 33 px below the
        /// WorldMap band that stays at the client top). Renders shift the window
        /// content by this amount, keeping the overlay at (0,0).</summary>
        public double? OffsetX { get; set; }
        public double? OffsetY { get; set; }
        /// <summary>Second KGUI window drawn behind this one (the client shows the
        /// WorldMap behind the MiddleMap, so the reference capture is a composite:
        /// MiddleMap chrome + WorldMap top-left/region list).</summary>
        public OverlaySpec Overlay { get; set; }
        public List<string> Evidence { get; set; }
        public List<string> Elements { get; set; }
        public List<string> Labels { get; set; }
        /// <summary>Host backdrop colour behind the window (hex, e.g. "#33393E"). The
        /// client composites semi-transparent windows (the battlefield settlement) over
        /// the live game world; the viewer uses a neutral tone for those instead of the
        /// default black so the authored translucency reads. Null = default dark host.</summary>
        public string Backdrop { get; set; }
    }

    public sealed class OverlaySpec
    {
        public string Path { get; set; }
        public string Hide { get; set; }
        public List<string> Show { get; set; }
        public List<TextOverride> Texts { get; set; }
        public List<AdjustSpec> Adjust { get; set; }
        /// <summary>Row clones for the overlay window (e.g. the personal card's three
        /// stat message buttons appended at runtime by PersonalCard_ShowData).</summary>
        public List<ListTemplate> Lists { get; set; }
        /// <summary>Draw the overlay above the main window instead of behind it (the
        /// settlement's Wnd_PersonCard is a child window drawn over the panel veil).</summary>
        public bool? Front { get; set; }
    }

    public sealed class LabelRow
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Source { get; set; }
    }
}
