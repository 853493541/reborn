using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    /// <summary>
    /// The engine's ImageType=11 render mode: the frame's left/right caps keep their
    /// pixel size while the middle stretches (the KGUI dispatch groups 10/11/12 on one
    /// path; verified on the queue panel's reward plaque - PVPUI22 frame 11 drawn at
    /// 68x20 against the live capture: a plain stretch flattened the pointed caps and
    /// the right cap read as "not closed").
    /// </summary>
    public sealed class HorizontalSliceImage : Grid
    {
        public HorizontalSliceImage(BitmapSource source, double width, double height,
                                    int left, int right)
        {
            Width = width;
            Height = height;
            SnapsToDevicePixels = true;

            int w = source.PixelWidth;
            if (w < 3 || left <= 0 || right <= 0 || left + right >= w - 1)
            {
                Children.Add(new Image { Source = source, Stretch = Stretch.Fill });
                return;
            }

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(left) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(right) });

            for (int c = 0; c < 3; c++)
            {
                int x = c == 0 ? 0 : (c == 1 ? left : w - right);
                int cw = c == 0 ? left : (c == 1 ? w - left - right : right);
                var crop = new CroppedBitmap(source, new Int32Rect(x, 0, cw, source.PixelHeight));
                crop.Freeze();
                var image = new Image { Source = crop, Stretch = Stretch.Fill };
                Grid.SetColumn(image, c);
                Children.Add(image);
            }
        }
    }
}
