using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    /// <summary>
    /// Minimal DDS reader for the client's UI atlases. The PakV4 UI textures ship as
    /// single-mip DXT5 (BC3) / DXT1 (BC1) files, which WPF cannot decode natively.
    /// </summary>
    public static class Dds
    {
        public static BitmapSource Load(string path)
        {
            var b = File.ReadAllBytes(path);
            if (b.Length < 128 || b[0] != 'D' || b[1] != 'D' || b[2] != 'S' || b[3] != ' ')
                throw new InvalidDataException("Not a DDS file: " + path);
            int height = BitConverter.ToInt32(b, 12);
            int width = BitConverter.ToInt32(b, 16);
            int mips = BitConverter.ToInt32(b, 28);
            int pfFlags = BitConverter.ToInt32(b, 80);
            var fourCc = System.Text.Encoding.ASCII.GetString(b, 84, 4);
            if (width <= 0 || height <= 0) throw new InvalidDataException("DDS bad size: " + path);
            if (mips <= 0) mips = 1;

            var pixels = new byte[width * height * 4];
            int offset = 128;
            int bw = Math.Max(1, (width + 3) / 4);
            int bh = Math.Max(1, (height + 3) / 4);
            bool dxt1 = fourCc == "DXT1";
            bool dxt5 = fourCc == "DXT5";
            if (!dxt1 && !dxt5) throw new NotSupportedException($"DDS format {fourCc} not supported: {path}");

            int blockSize = dxt1 ? 8 : 16;
            for (int by = 0; by < bh; by++)
            {
                for (int bx = 0; bx < bw; bx++)
                {
                    if (offset + blockSize > b.Length) break;
                    if (dxt5) DecodeDxt5Block(b, offset, pixels, width, height, bx * 4, by * 4);
                    else DecodeDxt1Block(b, offset, pixels, width, height, bx * 4, by * 4);
                    offset += blockSize;
                }
            }

            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static void Set(byte[] pixels, int width, int height, int x, int y, byte r, byte g, byte b, byte a)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = (y * width + x) * 4;
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }

        private static void ColorEndpoints(byte[] src, int o, out byte r0, out byte g0, out byte b0, out byte r1, out byte g1, out byte b1)
        {
            int c0 = src[o] | (src[o + 1] << 8);
            int c1 = src[o + 2] | (src[o + 3] << 8);
            b0 = (byte)((c0 & 0x1F) * 255 / 31);
            g0 = (byte)(((c0 >> 5) & 0x3F) * 255 / 63);
            r0 = (byte)(((c0 >> 11) & 0x1F) * 255 / 31);
            b1 = (byte)((c1 & 0x1F) * 255 / 31);
            g1 = (byte)(((c1 >> 5) & 0x3F) * 255 / 63);
            r1 = (byte)(((c1 >> 11) & 0x1F) * 255 / 31);
        }

        private static void DecodeDxt1Block(byte[] src, int o, byte[] pixels, int width, int height, int x0, int y0)
        {
            ColorEndpoints(src, o, out var r0, out var g0, out var b0, out var r1, out var g1, out var b1);
            uint bits = BitConverter.ToUInt32(src, o + 4);
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    int code = (int)((bits >> (2 * (j * 4 + i))) & 0x3);
                    byte r, g, b, a = 255;
                    switch (code)
                    {
                        case 0: r = r0; g = g0; b = b0; break;
                        case 1: r = r1; g = g1; b = b1; break;
                        case 2: r = (byte)((2 * r0 + r1) / 3); g = (byte)((2 * g0 + g1) / 3); b = (byte)((2 * b0 + b1) / 3); break;
                        default:
                            if (r0 <= r1) { r = 0; g = 0; b = 0; a = 0; }
                            else { r = (byte)((r0 + 2 * r1) / 3); g = (byte)((g0 + 2 * g1) / 3); b = (byte)((b0 + 2 * b1) / 3); }
                            break;
                    }
                    Set(pixels, width, height, x0 + i, y0 + j, r, g, b, a);
                }
            }
        }

        private static void DecodeDxt5Block(byte[] src, int o, byte[] pixels, int width, int height, int x0, int y0)
        {
            int a0 = src[o];
            int a1 = src[o + 1];
            ulong abits = 0;
            for (int k = 0; k < 6; k++) abits |= (ulong)src[o + 2 + k] << (8 * k);
            ColorEndpoints(src, o + 8, out var r0, out var g0, out var b0, out var r1, out var g1, out var b1);
            uint cbits = BitConverter.ToUInt32(src, o + 12);
            var alpha = new int[8];
            alpha[0] = a0;
            alpha[1] = a1;
            if (a0 > a1)
            {
                for (int i = 1; i <= 6; i++) alpha[i + 1] = ((7 - i) * a0 + i * a1) / 7;
            }
            else
            {
                for (int i = 1; i <= 4; i++) alpha[i + 1] = ((5 - i) * a0 + i * a1) / 5;
                alpha[6] = 0;
                alpha[7] = 255;
            }
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    int idx = j * 4 + i;
                    int acode = (int)((abits >> (3 * idx)) & 0x7);
                    int ccode = (int)((cbits >> (2 * idx)) & 0x3);
                    byte r, g, b;
                    switch (ccode)
                    {
                        case 0: r = r0; g = g0; b = b0; break;
                        case 1: r = r1; g = g1; b = b1; break;
                        case 2: r = (byte)((2 * r0 + r1) / 3); g = (byte)((2 * g0 + g1) / 3); b = (byte)((2 * b0 + b1) / 3); break;
                        default: r = (byte)((r0 + 2 * r1) / 3); g = (byte)((g0 + 2 * g1) / 3); b = (byte)((b0 + 2 * b1) / 3); break;
                    }
                    Set(pixels, width, height, x0 + i, y0 + j, r, g, b, (byte)alpha[acode]);
                }
            }
        }
    }

    /// <summary>Dispatches atlas texture decoding by file magic (TGA or DDS).</summary>
    public static class TextureLoader
    {
        public static BitmapSource Load(string path)
        {
            var b = File.ReadAllBytes(path);
            if (b.Length >= 4 && b[0] == 'D' && b[1] == 'D' && b[2] == 'S' && b[3] == ' ')
                return Dds.Load(path);
            return Tga.Load(path);
        }
    }
}
