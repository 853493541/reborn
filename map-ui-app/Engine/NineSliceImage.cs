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
        {
            Width = width;
            Height = height;
            SnapsToDevicePixels = true;

            int maxBorder = Math.Max(1, Math.Min(source.PixelWidth, source.PixelHeight) / 2);
            int border = Math.Max(1, Math.Min(source.PixelWidth, source.PixelHeight) / 3);
            if (border > maxBorder) border = maxBorder;

            var columns = new[] { border, Math.Max(1, source.PixelWidth - border * 2), border };
            var rows = new[] { border, Math.Max(1, source.PixelHeight - border * 2), border };

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
                    int x = c == 0 ? 0 : (c == 1 ? border : source.PixelWidth - border);
                    int y = r == 0 ? 0 : (r == 1 ? border : source.PixelHeight - border);
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
