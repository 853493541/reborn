using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    /// <summary>
    /// The game scene: real MiddleMap, Minimap lens and BattleFieldMap windows
    /// built from the extracted UI layouts and atlases.
    /// </summary>
    public sealed class MapScene : Canvas
    {
        public const double DesignWidth = 1920;
        public const double DesignHeight = 1080;

        private readonly AssetResolver _assets;
        private readonly UiTexCache _textures;
        private MapBundle _bundle;
        private UiBuildResult _lensBuild;
        private UiBuildResult _middleBuild;
        private UiBuildResult _battleBuild;
        private LensView _lensView;
        private MapViewport _mapView;
        private BattleView _battleView;
        private Image _backdrop;
        private bool _middleOpen;
        private bool _battleDragging;
        private bool _battleSituation;
        private Point _battleDragStart;
        private double _battleStartLeft;
        private double _battleStartTop;

        public MapScene(string mapId)
        {
            Width = DesignWidth;
            Height = DesignHeight;
            ClipToBounds = true;
            Background = new SolidColorBrush(Color.FromRgb(6, 8, 7));
            _assets = new AssetResolver(GameData.UiRoot);
            _textures = new UiTexCache(_assets);
            _bundle = GameData.LoadMap(mapId);
            BuildBackdrop();
            BuildLens();
            BuildBattlefield();
            BuildMiddleMap();
            UpdateViews();
            MiddleOpen = true;
        }

        public bool MiddleOpen
        {
            get => _middleOpen;
            set
            {
                _middleOpen = value;
                _middleBuild.Root.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (value) Panel.SetZIndex(_middleBuild.Root, 100);
            }
        }

        public bool BattleVisible
        {
            get => _battleBuild.Root.Visibility == Visibility.Visible;
            set => _battleBuild.Root.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public string SituationLabel => _battleSituation ? "战场" : "世界";

        /// <summary>
        /// The game never shows every map window at once: the normal world shows the
        /// lens (and the MiddleMap on demand), while battlefield modes show the lens
        /// plus the draggable BattleFieldMap panel.
        /// </summary>
        public void ToggleSituation()
        {
            _battleSituation = !_battleSituation;
            MiddleOpen = false;
            BattleVisible = _battleSituation;
        }

        public void SelectMap(string id)
        {
            _bundle = GameData.LoadMap(id);
            UpdateViews();
        }

        public string MapDisplay => _bundle.Entry.Display;

        private void BuildBackdrop()
        {
            _backdrop = new Image { Stretch = Stretch.UniformToFill, Width = DesignWidth, Height = DesignHeight };
            Children.Add(_backdrop);
            Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = DesignWidth,
                Height = DesignHeight,
                Fill = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
            });
        }

        private void BuildLens()
        {
            var ini = IniFile.Load(Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "MiniMap.ini"));
            _lensBuild = UiLayout.Build(ini, _assets, _textures);
            Canvas.SetLeft(_lensBuild.Root, DesignWidth - 260);
            Canvas.SetTop(_lensBuild.Root, 0);
            Children.Add(_lensBuild.Root);

            HideDynamicLayers(_lensBuild);
            HideSections(_lensBuild,
                "Handle_EYSInfo",
                "Text_Name", "Text_Fresher", "Image_Annunce", "Text_ct",
                "Btn_Mail", "Btn_Questionnaire",
                "Btn_ZoomIn", "Btn_ZoomOut",
                "Btn_Arena", "Btn_ArenaForbid", "Btn_BattleFieldForbid",
                "Btn_FindTeam", "Btn_LeavePVP", "Btn_LeaveFB", "Btn_LeaveStudio", "Btn_LeaveJJCRouge",
                "Btn_RSearch", "Btn_RCopy", "Btn_NpcSearch", "Btn_Help",
                "Handle_warning", "Handle_num", "Handle_Tuisong", "Handle_Selfie", "Handle_Calender",
                "WndContainer_CommandList", "WndContainer_Observer",
                "Wnd_EYSOver", "WndContainer_Emergency",
                "Btn_Pelican", "Btn_LightOff", "Btn_Emergency", "Wnd_CustomMode");
            GetSlotSize(_lensBuild, "Minimap_Map", 202, 202, out var width, out var height);
            _lensView = new LensView(_assets, _textures, width, height);
            AttachTo(_lensBuild, "Minimap_Map", _lensView);

            Wire(_lensBuild, "Btn_BigMap", () => MiddleOpen = !MiddleOpen);
            Wire(_lensBuild, "Btn_ZoomIn", () => _lensView.ZoomBy(1.1));
            Wire(_lensBuild, "Btn_ZoomOut", () => _lensView.ZoomBy(1 / 1.1));
        }

        private void BuildBattlefield()
        {
            var ini = IniFile.Load(Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "BattleField", "BattleFieldMap.ini"));
            _battleBuild = UiLayout.Build(ini, _assets, _textures);
            Canvas.SetLeft(_battleBuild.Root, 785);
            Canvas.SetTop(_battleBuild.Root, 298);
            Children.Add(_battleBuild.Root);

            HideDynamicLayers(_battleBuild);
            HideSections(_battleBuild,
                "Wnd_Route",
                "CheckBox_Hq", "CheckBox_Er",
                "Text_Time", "Image_Set", "Btn_Operate",
                "Handle_Info");
            GetSlotSize(_battleBuild, "Image_Map", 300, 263, out var width, out var height);
            _battleView = new BattleView(_textures, width, height);
            AttachTo(_battleBuild, "Image_Map", _battleView);

            _battleBuild.Root.MouseLeftButtonDown += BattleMouseDown;
            _battleBuild.Root.MouseMove += BattleMouseMove;
            _battleBuild.Root.MouseLeftButtonUp += BattleMouseUp;
        }

        private void BuildMiddleMap()
        {
            var ini = IniFile.Load(Path.Combine(GameData.TextRoot, "ui", "Config", "Default", "MiddleMap.ini"));
            _middleBuild = UiLayout.Build(ini, _assets, _textures);
            Canvas.SetLeft(_middleBuild.Root, 41);
            Canvas.SetTop(_middleBuild.Root, 55);
            Panel.SetZIndex(_middleBuild.Root, 100);
            _middleBuild.Root.Visibility = Visibility.Collapsed;
            Children.Add(_middleBuild.Root);

            HideDynamicLayers(_middleBuild);
            HideSections(_middleBuild,
                "Wnd_CommandMap",
                // The window chrome is re-issued in the INI: the *_0 set (Handle_Bg_Common,
                // right-anchored 974px panel) is the live one, Handle_Bg is the legacy copy.
                "Handle_Bg",
                "Image_TrafficBg",
                "Handle_TipsTitle",
                "Text_Title", "Text_Title1",
                "Btn_BattleMap",
                "Btn_QuitCommand", "Btn_EnterCommand",
                "Text_Level18",
                "CheckBox_Hq", "CheckBox_Er", "Handle_GF",
                "WndFlexContainer_AllEPBar",
                "Wnd_Quest",
                "CheckBox_QuestPage",
                "Wnd_HeatMapState", "CheckBox_HeatMap", "Btn_RefreshHeatMap",
                "WndContainer_HeatMapDetail", "WndFlexContainer_Explore",
                "Handle_Identify",
                "Btn_WorldMap", "Btn_City",
                "WndContainer_RegionList",
                "Handle_Mode", "Handle_Npc", "Handle_Area",
                "CheckBox_QuestShowLow",
                "Text_Scale", "Scroll_Scale", "Text_ScalePer",
                "Text_Serch",
                "Scroll_List", "Btn_Up", "Btn_Down");
            GetSlotSize(_middleBuild, "Image_Map", 928, 812, out var width, out var height);
            _mapView = new MapViewport(_textures, width, height);
            AttachTo(_middleBuild, "Image_Map", _mapView);

            if (_middleBuild.Elements.TryGetValue("Text_MarkSetting", out var markLabel) && markLabel is TextBlock markBlock)
            {
                Canvas.SetLeft(markBlock, 34);
                Canvas.SetTop(markBlock, 4);
            }
            if (_middleBuild.Elements.TryGetValue("Text_AlphaPer", out var alphaPer) && alphaPer is TextBlock alphaBlock)
            {
                Canvas.SetLeft(alphaBlock, 1090);
                Canvas.SetTop(alphaBlock, 772);
            }
            BuildSearchBox();
            SetAlphaSlider(1.0);
            Wire(_middleBuild, "Btn_Close", () => MiddleOpen = false);
        }

        /// <summary>
        /// The sidebar search box: the game puts a placeholder label and an edit field in
        /// the same slot; recreate that as one edit with the label as its placeholder.
        /// </summary>
        private void BuildSearchBox()
        {
            if (!_middleBuild.Elements.TryGetValue("Edit_Search", out var slot) || slot is not Canvas edit) return;
            var placeholder = new TextBlock
            {
                Text = "搜索：",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 150)),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(placeholder, 0);
            Canvas.SetTop(placeholder, 1);
            var input = new TextBox
            {
                Width = 160,
                Height = 18,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12,
                Padding = new Thickness(0),
            };
            input.TextChanged += (s, e) => placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            edit.Children.Add(placeholder);
            edit.Children.Add(input);
        }

        /// <summary>The game's map alpha slider; the app draws the map art at that alpha.</summary>
        private void SetAlphaSlider(double alpha)
        {
            alpha = Math.Max(0, Math.Min(1, alpha));
            _mapView.ArtOpacity = alpha;
            if (_middleBuild.Elements.TryGetValue("Handle_Alpha", out var track) && track is Canvas trackCanvas)
            {
                double trackWidth = double.IsNaN(trackCanvas.Width) ? 164 : trackCanvas.Width;
                if (_middleBuild.Elements.TryGetValue("Btn_SpliteAlpha", out var thumb) && thumb is FrameworkElement thumbElement)
                {
                    double thumbWidth = double.IsNaN(thumbElement.Width) ? 40 : thumbElement.Width;
                    Canvas.SetLeft(thumbElement, (trackWidth - thumbWidth) * alpha);
                }
            }
            if (_middleBuild.Elements.TryGetValue("Text_AlphaPer", out var label) && label is TextBlock labelBlock)
                labelBlock.Text = $"{(int)Math.Round(alpha * 100)}%";
        }

        private void UpdateViews()
        {
            _lensView.SetMap(_bundle);
            _mapView.SetMap(_bundle);
            _battleView.SetMap(_bundle);
            LoadBackdrop();
            if (_middleBuild.Elements.TryGetValue("Text_Title_0!", out var title) && title is TextBlock titleBlock)
                titleBlock.Text = _bundle.Entry.Display;
            PopulateSidebarTrunks();
        }

        /// <summary>
        /// The sidebar is a search browser: with an empty search box the game clears the
        /// list and shows the two category rows (NPC / craft). The area rows only appear
        /// after a category is expanded, so recreate the default state here.
        /// </summary>
        private void PopulateSidebarTrunks()
        {
            if (!_middleBuild.Elements.TryGetValue("Handle_NpcOrAreaList", out var listElement) || listElement is not Canvas list) return;
            var stale = new System.Collections.Generic.List<UIElement>();
            foreach (var child in list.Children)
                if (child is Canvas) stale.Add((UIElement)child);
            foreach (var child in stale) list.Children.Remove(child);

            var rowBackground = _textures.GetFrame("ui/Image/MiddleMap/MapWindow6.UITex", 32);
            var titles = new[] { "NPC", "技艺" };
            for (int i = 0; i < titles.Length; i++)
            {
                var row = new Canvas { Width = 260, Height = 54 };
                if (rowBackground != null)
                {
                    var background = new Image { Source = rowBackground, Width = 212, Height = 32, Stretch = Stretch.Fill };
                    Canvas.SetLeft(background, 13);
                    Canvas.SetTop(background, 0);
                    row.Children.Add(background);
                }
                var title = new TextBlock
                {
                    Text = titles[i],
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    FontSize = 12,
                    Foreground = Brushes.White,
                    Width = 200,
                    Height = 20,
                };
                Canvas.SetLeft(title, 44);
                Canvas.SetTop(title, 8);
                row.Children.Add(title);
                Canvas.SetLeft(row, 0);
                Canvas.SetTop(row, i * 54);
                list.Children.Add(row);
            }
        }

        private void LoadBackdrop()
        {
            var mapDirectory = Path.Combine(GameData.ProofRoot, "extracted", "data", "source", "maps", _bundle.Entry.Folder);
            string file = null;
            if (Directory.Exists(mapDirectory))
            {
                var candidates = Directory.GetFiles(mapDirectory, "loading*.png");
                if (candidates.Length > 0) file = candidates[0];
            }
            _backdrop.Source = file != null ? GameData.LoadPng(file) : null;
        }

        /// <summary>
        /// The map-layer handles (marks, storm lines, team markers, menus) are populated
        /// by the game at runtime; a static recreation hides them so the base window reads.
        /// </summary>
        private static void HideDynamicLayers(UiBuildResult build)
        {
            foreach (var pair in build.Sections)
            {
                var parent = pair.Value.Get("._Parent");
                var name = pair.Key;
                var dynamic =
                    (string.Equals(parent, "Handle_Map", StringComparison.OrdinalIgnoreCase) && !name.Equals("Image_Map", StringComparison.OrdinalIgnoreCase)) ||
                    name.Equals("Wnd_BigMap", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Image_Matching", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Animate_Matching", StringComparison.OrdinalIgnoreCase);
                if (dynamic && build.Elements.TryGetValue(name, out var element))
                    element.Visibility = Visibility.Collapsed;
            }
        }

        private static void DumpVisibleTexts(UiBuildResult build, string path)
        {
            var lines = new System.Collections.Generic.List<string>();
            foreach (var pair in build.Elements)
            {
                var element = pair.Value;
                bool visible = true;
                FrameworkElement node = element;
                while (node != null)
                {
                    if (node.Visibility != Visibility.Visible) { visible = false; break; }
                    if (node == build.Root) break;
                    node = node.Parent as FrameworkElement;
                }
                if (!visible) continue;
                double x = 0, y = 0;
                node = element;
                while (node != null)
                {
                    x += double.IsNaN(Canvas.GetLeft(node)) ? 0 : Canvas.GetLeft(node);
                    y += double.IsNaN(Canvas.GetTop(node)) ? 0 : Canvas.GetTop(node);
                    if (node == build.Root) break;
                    node = node.Parent as FrameworkElement;
                }
                var detail = element is TextBlock text ? text.Text : element is Image image ? "image " + (image.Source != null ? image.Source.Width + "x" + image.Source.Height : "NULL") + " " + SamplePixel(image.Source as System.Windows.Media.Imaging.BitmapSource) : element.GetType().Name;
                lines.Add($"{pair.Key} | {detail} | {x:0.#},{y:0.#} | {element.Width:0.#}x{element.Height:0.#} | parent {build.Sections[pair.Key].Get("._Parent")}");
            }
            File.WriteAllLines(path, lines);
        }

        private static string SamplePixel(System.Windows.Media.Imaging.BitmapSource source)
        {
            if (source == null || source.PixelWidth < 1 || source.PixelHeight < 1) return "";
            try
            {
                var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                var buffer = new byte[4];
                converted.CopyPixels(new Int32Rect(converted.PixelWidth / 2, converted.PixelHeight / 2, 1, 1), buffer, 4, 0);
                return $"center rgba({buffer[2]},{buffer[1]},{buffer[0]},{buffer[3]})";
            }
            catch
            {
                return "";
            }
        }

        private static void HideSections(UiBuildResult build, params string[] names)
        {
            foreach (var name in names)
            {
                if (build.Elements.TryGetValue(name, out var element))
                    element.Visibility = Visibility.Collapsed;
            }
        }

        private static void GetSlotSize(UiBuildResult build, string sectionName, double fallbackWidth, double fallbackHeight, out double width, out double height)
        {
            width = fallbackWidth;
            height = fallbackHeight;
            if (build.Elements.TryGetValue(sectionName, out var slot) && slot is Canvas canvas)
            {
                if (!double.IsNaN(canvas.Width) && canvas.Width > 0) width = canvas.Width;
                if (!double.IsNaN(canvas.Height) && canvas.Height > 0) height = canvas.Height;
            }
        }

        private static void AttachTo(UiBuildResult build, string sectionName, FrameworkElement child)
        {
            if (build.Elements.TryGetValue(sectionName, out var slot) && slot is Canvas canvas) canvas.Children.Add(child);
        }

        private static void Wire(UiBuildResult build, string sectionName, Action action)
        {
            if (!build.Elements.TryGetValue(sectionName, out var element)) return;
            element.Cursor = Cursors.Hand;
            element.MouseLeftButtonUp += (s, e) => { action(); e.Handled = true; };
        }

        private void BattleMouseDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(_battleBuild.Root);
            if (position.Y > 40) return;
            _battleDragging = true;
            _battleDragStart = e.GetPosition(this);
            _battleStartLeft = Canvas.GetLeft(_battleBuild.Root);
            _battleStartTop = Canvas.GetTop(_battleBuild.Root);
            _battleBuild.Root.CaptureMouse();
        }

        private void BattleMouseMove(object sender, MouseEventArgs e)
        {
            if (!_battleDragging) return;
            var position = e.GetPosition(this);
            Canvas.SetLeft(_battleBuild.Root, _battleStartLeft + (position.X - _battleDragStart.X));
            Canvas.SetTop(_battleBuild.Root, _battleStartTop + (position.Y - _battleDragStart.Y));
        }

        private void BattleMouseUp(object sender, MouseButtonEventArgs e)
        {
            _battleDragging = false;
            _battleBuild.Root.ReleaseMouseCapture();
        }

        public void Dump(string directory)
        {
            Directory.CreateDirectory(directory);
            MiddleOpen = false;
            BattleVisible = false;
            Layout();
            SavePng(Path.Combine(directory, "scene-default.png"), DesignWidth, DesignHeight);
            SaveCrop(Path.Combine(directory, "lens.png"), (int)(DesignWidth - 470), 0, 470, 270);
            DumpVisibleTexts(_lensBuild, Path.Combine(directory, "lens-elements.txt"));
            BattleVisible = true;
            Layout();
            SaveCrop(Path.Combine(directory, "battlefield-panel.png"), 785, 298, 305, 299);
            DumpVisibleTexts(_battleBuild, Path.Combine(directory, "battle-texts.txt"));
            BattleVisible = false;
            MiddleOpen = true;
            Layout();
            DumpVisibleTexts(_middleBuild, Path.Combine(directory, "middle-elements.txt"));
            SavePng(Path.Combine(directory, "scene-middlemap.png"), DesignWidth, DesignHeight);
            SaveCrop(Path.Combine(directory, "middlemap-window.png"), 41, 55, 1410, 890);
            MiddleOpen = false;
        }

        private void Layout()
        {
            Measure(new Size(Width, Height));
            Arrange(new Rect(0, 0, Width, Height));
            UpdateLayout();
        }

        private void SavePng(string path, double width, double height)
        {
            var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        private void SaveCrop(string path, int x, int y, int width, int height)
        {
            var full = new RenderTargetBitmap((int)DesignWidth, (int)DesignHeight, 96, 96, PixelFormats.Pbgra32);
            full.Render(this);
            var crop = new CroppedBitmap(full, new Int32Rect(x, y, width, height));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(crop));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
