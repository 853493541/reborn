using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    /// <summary>Minimal TGA reader for the uncompressed/RLE BGRA images shipped by the client.</summary>
    public static class Tga
    {
        public static BitmapSource Load(string path)
        {
            var b = File.ReadAllBytes(path);
            if (b.Length < 18) throw new InvalidDataException("TGA too small: " + path);
            int idLength = b[0];
            int colorMapType = b[1];
            int imageType = b[2];
            int width = b[12] | (b[13] << 8);
            int height = b[14] | (b[15] << 8);
            int bpp = b[16];
            int descriptor = b[17];
            if (colorMapType != 0 || (imageType != 2 && imageType != 3 && imageType != 10 && imageType != 11))
                throw new NotSupportedException($"TGA image type {imageType} not supported: {path}");
            if (width <= 0 || height <= 0) throw new InvalidDataException("TGA bad size: " + path);
            bool topDown = (descriptor & 0x20) != 0;
            int bytesPerPixel = Math.Max(1, bpp / 8);
            int offset = 18 + idLength;
            int pixelCount = width * height;
            var pixels = new byte[pixelCount * 4];
            int written = 0;

            void Put(byte[] src, int si)
            {
                int row = written / width;
                int col = written % width;
                int y = topDown ? row : height - 1 - row;
                int di = (y * width + col) * 4;
                if (bytesPerPixel == 4)
                {
                    pixels[di] = src[si];
                    pixels[di + 1] = src[si + 1];
                    pixels[di + 2] = src[si + 2];
                    pixels[di + 3] = src[si + 3];
                }
                else if (bytesPerPixel == 3)
                {
                    pixels[di] = src[si];
                    pixels[di + 1] = src[si + 1];
                    pixels[di + 2] = src[si + 2];
                    pixels[di + 3] = 255;
                }
                else
                {
                    byte gray = src[si];
                    pixels[di] = gray;
                    pixels[di + 1] = gray;
                    pixels[di + 2] = gray;
                    pixels[di + 3] = 255;
                }
                written++;
            }

            bool rle = imageType == 10 || imageType == 11;
            bool gray = imageType == 3 || imageType == 11;
            if (!rle)
            {
                int step = gray ? 1 : bytesPerPixel;
                for (int i = 0; i < pixelCount; i++) Put(b, offset + i * step);
            }
            else
            {
                int p = offset;
                var px = new byte[bytesPerPixel];
                while (written < pixelCount && p < b.Length)
                {
                    int header = b[p++];
                    int count = (header & 0x7F) + 1;
                    if ((header & 0x80) != 0)
                    {
                        Array.Copy(b, p, px, 0, bytesPerPixel);
                        p += bytesPerPixel;
                        for (int i = 0; i < count && written < pixelCount; i++) Put(px, 0);
                    }
                    else
                    {
                        for (int i = 0; i < count && written < pixelCount; i++)
                        {
                            Put(b, p);
                            p += bytesPerPixel;
                        }
                    }
                }
            }

            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
