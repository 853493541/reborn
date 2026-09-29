using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    /// <summary>
    /// The engine's "diced" image mode (ImageType=10): corners keep their pixel size,
    /// edges and center stretch. Implemented as a 3x3 grid of cropped images.
    /// </summary>
    public sealed class NineSliceImage : Grid
    {
        public NineSliceImage(BitmapSource source, double width, double height)
            : this(source, width, height, -1, -1, -1, -1)
        {
        }

        public NineSliceImage(BitmapSource source, double width, double height,
                              int left, int top, int right, int bottom)
        {
            Width = width;
            Height = height;
            SnapsToDevicePixels = true;

            if (source.PixelWidth < 3 || source.PixelHeight < 3)
            {
                Children.Add(new Image { Source = source, Stretch = Stretch.Fill });
                return;
            }

            if (left < 0 || top < 0 || right < 0 || bottom < 0)
            {
                int uniform = Math.Max(1, Math.Min(source.PixelWidth, source.PixelHeight) / 3);
                left = top = right = bottom = uniform;
            }
            left = Math.Min(Math.Max(1, left), source.PixelWidth - 2);
            right = Math.Min(Math.Max(1, right), source.PixelWidth - left - 1);
            top = Math.Min(Math.Max(1, top), source.PixelHeight - 2);
            bottom = Math.Min(Math.Max(1, bottom), source.PixelHeight - top - 1);

            var columns = new[] { left, source.PixelWidth - left - right, right };
            var rows = new[] { top, source.PixelHeight - top - bottom, bottom };

            for (int c = 0; c < 3; c++)
                ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = c == 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(columns[c]),
                });
            for (int r = 0; r < 3; r++)
                RowDefinitions.Add(new RowDefinition
                {
                    Height = r == 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(rows[r]),
                });

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int x = c == 0 ? 0 : (c == 1 ? left : source.PixelWidth - right);
                    int y = r == 0 ? 0 : (r == 1 ? top : source.PixelHeight - bottom);
                    var crop = new CroppedBitmap(source, new Int32Rect(x, y, columns[c], rows[r]));
                    crop.Freeze();
                    var image = new Image { Source = crop, Stretch = Stretch.Fill };
                    Grid.SetRow(image, r);
                    Grid.SetColumn(image, c);
                    Children.Add(image);
                }
            }
        }
    }
}
