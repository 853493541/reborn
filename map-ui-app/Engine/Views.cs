using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    /// <summary>MiddleMap (M key) map artwork with area labels, zoom and pan.</summary>
    public sealed class MapViewport : Canvas
    {
        private readonly UiTexCache _textures;
        private readonly Canvas _inner = new Canvas();
        private readonly Image _art = new Image { Stretch = Stretch.Fill };
        private readonly Image _marker = new Image { Stretch = Stretch.Uniform, Width = 34, Height = 34 };
        private readonly List<TextBlock> _labels = new List<TextBlock>();
        private MapBundle _bundle;
        private double _zoom = 1, _panX, _panY;
        private bool _panning;
        private Point _panStart;
        private double _panOriginX, _panOriginY;

        public MapViewport(UiTexCache textures, double width, double height)
        {
            _textures = textures;
            Width = width;
            Height = height;
            ClipToBounds = true;
            Background = Brushes.Transparent;
            Children.Add(_inner);
            _inner.Children.Add(_art);
            _inner.Children.Add(_marker);
            MouseWheel += OnWheel;
            MouseLeftButtonDown += OnDown;
            MouseMove += OnMove;
            MouseLeftButtonUp += OnUp;
        }

        public void SetMap(MapBundle bundle)
        {
            _bundle = bundle;
            _zoom = 1;
            _panX = 0;
            _panY = 0;
            _art.Source = bundle.Middlemap;
            _art.Width = bundle.Width;
            _art.Height = bundle.Height;
            _marker.Source = _textures.GetFrame("ui/Image/Minimap/BattleMinimap3.UITex", 36);

            foreach (var label in _labels) _inner.Children.Remove(label);
            _labels.Clear();
            foreach (var area in bundle.Areas)
            {
                var block = new TextBlock
                {
                    Text = area.Name,
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    FontSize = 15,
                    Foreground = Brushes.White,
                    Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 5, ShadowDepth = 0, Opacity = 0.95 },
                };
                block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(block, bundle.ToImageX(area.X) - block.DesiredSize.Width / 2);
                Canvas.SetTop(block, bundle.ToImageY(area.Y) - block.DesiredSize.Height / 2);
                _inner.Children.Add(block);
                _labels.Add(block);
            }

            var (worldX, worldY) = bundle.PlayerWorld();
            Canvas.SetLeft(_marker, bundle.ToImageX(worldX) - 17);
            Canvas.SetTop(_marker, bundle.ToImageY(worldY) - 17);
            Apply();
        }

        public void ZoomBy(double factor)
        {
            _zoom = Math.Max(0.5, Math.Min(3.0, _zoom * factor));
            Apply();
        }

        /// <summary>Map-art alpha, driven by the window's opacity slider.</summary>
        public double ArtOpacity
        {
            get => _art.Opacity;
            set => _art.Opacity = Math.Max(0, Math.Min(1, value));
        }

        private void Apply()
        {
            if (_bundle == null) return;
            double fit = Math.Min(Width / _bundle.Width, Height / _bundle.Height);
            double scale = fit * _zoom;
            double offsetX = (Width - _bundle.Width * scale) / 2 + _panX;
            double offsetY = (Height - _bundle.Height * scale) / 2 + _panY;
            _inner.RenderTransform = new MatrixTransform(scale, 0, 0, scale, offsetX, offsetY);
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            ZoomBy(e.Delta > 0 ? 1.12 : 1 / 1.12);
            e.Handled = true;
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            _panning = true;
            _panStart = e.GetPosition(this);
            _panOriginX = _panX;
            _panOriginY = _panY;
            CaptureMouse();
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (!_panning) return;
            var position = e.GetPosition(this);
            _panX = _panOriginX + (position.X - _panStart.X);
            _panY = _panOriginY + (position.Y - _panStart.Y);
            Apply();
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            _panning = false;
            ReleaseMouseCapture();
        }
    }

    /// <summary>Top-right minimap lens: tile mosaic, circular mask, player arrow, zoom.</summary>
    public sealed class LensView : Canvas
    {
        private readonly Canvas _artLayer = new Canvas();
        private readonly Image _art = new Image { Stretch = Stretch.Fill };
        private readonly Image _selfDot = new Image { Stretch = Stretch.Uniform, Width = 18, Height = 18 };
        private readonly Image _arrow = new Image { Stretch = Stretch.Uniform, Width = 34, Height = 34 };
        private double _zoom = 1;
        private double _artWidth, _artHeight, _playerX, _playerY, _pxPerUnit;

        public LensView(AssetResolver assets, UiTexCache textures, double width, double height)
        {
            Width = width;
            Height = height;
            ClipToBounds = true;
            Background = Brushes.Transparent;
            RenderOptions.SetBitmapScalingMode(_artLayer, BitmapScalingMode.HighQuality);
            _artLayer.Children.Add(_art);
            Children.Add(_artLayer);

            _artLayer.Clip = new EllipseGeometry(new Point(width / 2, height / 2), width / 2 - 1, height / 2 - 1);

            _selfDot.Source = textures.GetFrame("ui/Image/Minimap/Minimap3.UITex", 20);
            Canvas.SetLeft(_selfDot, width / 2 - 9);
            Canvas.SetTop(_selfDot, height / 2 - 9);
            Children.Add(_selfDot);

            _arrow.Source = textures.GetFrame("ui/Image/Minimap/BattleMinimap3.UITex", 36);
            Canvas.SetLeft(_arrow, width / 2 - 17);
            Canvas.SetTop(_arrow, height / 2 - 17);
            Children.Add(_arrow);

            MouseWheel += (s, e) => { ZoomBy(e.Delta > 0 ? 1.1 : 1 / 1.1); e.Handled = true; };
        }

        public void ZoomBy(double factor)
        {
            _zoom = Math.Max(0.25, Math.Min(1.0, _zoom * factor));
            Apply();
        }

        public void SetMap(MapBundle bundle)
        {
            if (File.Exists(GameData.MosaicPath))
            {
                _art.Source = GameData.LoadPng(GameData.MosaicPath);
                _artWidth = 2304;
                _artHeight = 2304;
                var (worldX, worldY) = bundle.PlayerWorld();
                _playerX = (worldX + 12800) / 100.0;
                _playerY = (worldY + 12800) / 100.0;
                _pxPerUnit = 0.01;
            }
            else
            {
                _art.Source = bundle.Middlemap;
                _artWidth = bundle.Width;
                _artHeight = bundle.Height;
                var (worldX, worldY) = bundle.PlayerWorld();
                _playerX = bundle.ToImageX(worldX);
                _playerY = bundle.ToImageY(worldY);
                _pxPerUnit = bundle.Scale;
            }
            _art.Width = _artWidth;
            _art.Height = _artHeight;
            Apply();
        }

        private void Apply()
        {
            if (_art.Source == null || _pxPerUnit <= 0) return;
            double display = 0.04 * _zoom; // game config scale 0.02 at the default display zoom 2
            double scale = display / _pxPerUnit;
            Canvas.SetLeft(_art, Width / 2 - _playerX * scale);
            Canvas.SetTop(_art, Height / 2 - _playerY * scale);
            _art.RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    /// <summary>Draggable BattleFieldMap panel artwork: map fit, player marker.</summary>
    public sealed class BattleView : Canvas
    {
        private readonly Image _art = new Image { Stretch = Stretch.Fill };
        private readonly Image _marker = new Image { Stretch = Stretch.Uniform, Width = 30, Height = 30 };

        public BattleView(UiTexCache textures, double width, double height)
        {
            Width = width;
            Height = height;
            ClipToBounds = true;
            Background = Brushes.Transparent;
            Children.Add(_art);
            _marker.Source = textures.GetFrame("ui/Image/Minimap/BattleMinimap3.UITex", 36);
            Children.Add(_marker);
        }

        public void SetMap(MapBundle bundle)
        {
            _art.Source = bundle.Middlemap;
            _art.Width = bundle.Width;
            _art.Height = bundle.Height;
            double fit = Math.Min(Width / bundle.Width, Height / bundle.Height);
            double offsetX = (Width - bundle.Width * fit) / 2;
            double offsetY = (Height - bundle.Height * fit) / 2;
            _art.RenderTransform = new MatrixTransform(fit, 0, 0, fit, offsetX, offsetY);

            // BattleFieldMap.LPosToHPos: mapy is the bottom edge, world Y grows upwards.
            double scaleFinal = bundle.Scale * fit / bundle.Width;
            double mapX = offsetX;
            double mapY = offsetY + bundle.Height * fit;
            var (worldX, worldY) = bundle.PlayerWorld();
            Canvas.SetLeft(_marker, mapX + (worldX - bundle.StartX) * scaleFinal - 15);
            Canvas.SetTop(_marker, mapY - (worldY - bundle.StartY) * scaleFinal - 15);
        }
    }
}
