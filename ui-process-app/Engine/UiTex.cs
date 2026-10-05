using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MapUiApp.Engine
{
    public sealed class UiTexFrame
    {
        public int X, Y, W, H, Flag;
    }

    public struct UiTexGroup
    {
        public int Count;
        public int StartFrame;
        public int Interval;
    }

    /// <summary>
    /// Parser for the client's .UITex atlas descriptor: 92-byte header
    /// ("UI", version, texture width/height, frame count, 64-byte texture name)
    /// followed by 20-byte frame records (x, y, width, height, flag).
    /// </summary>
    public sealed class UiTex
    {
        public readonly string Path;
        public readonly string TextureName;
        public readonly int TextureWidth;
        public readonly int TextureHeight;
        public readonly UiTexFrame[] Frames;
        public readonly UiTexGroup[] Groups;

        private readonly AssetResolver _assets;
        private BitmapSource _texture;
        private bool _textureLoaded;

        public UiTex(string path, AssetResolver assets)
        {
            Path = path;
            _assets = assets;
            var b = File.ReadAllBytes(path);
            if (b.Length < 92 || b[0] != (byte)'U' || b[1] != (byte)'I')
                throw new InvalidDataException("Not a UITex file: " + path);
            TextureWidth = BitConverter.ToInt32(b, 4);
            TextureHeight = BitConverter.ToInt32(b, 8);
            int count = Math.Max(0, BitConverter.ToInt32(b, 12));
            TextureName = Encoding.ASCII.GetString(b, 24, 64).TrimEnd('\0');
            var frames = new List<UiTexFrame>(count);
            for (int i = 0; i < count; i++)
            {
                int o = 92 + i * 20;
                if (o + 20 > b.Length) break;
                frames.Add(new UiTexFrame
                {
                    X = BitConverter.ToInt32(b, o),
                    Y = BitConverter.ToInt32(b, o + 4),
                    W = BitConverter.ToInt32(b, o + 8),
                    H = BitConverter.ToInt32(b, o + 12),
                    Flag = BitConverter.ToInt32(b, o + 16),
                });
            }
            Frames = frames.ToArray();

            // Frame groups follow the frame table; the header carries their count at
            // offset 16. Each record is: u32 frameCount; a 0 count is an empty group
            // (4 bytes only); otherwise frameCount × (u32 frameIndex, u32 intervalMs)
            // — the per-frame entries are 8 bytes each, NOT a shared start/interval
            // plus count-1 bare indices (that mis-read multi-frame groups and
            // desynced the whole table, so buttons fell back to their authored Frame
            // — e.g. DynamicBattleRoyale Btn_Option showed the wrong atlas art).
            // Buttons/checkboxes reference these group ids (NormalGroup/...), and the
            // ids are local to the atlas (e.g. Button.UITex group 96 = frame 92).
            var groups = new List<UiTexGroup>();
            int groupCount = Math.Max(0, BitConverter.ToInt32(b, 16));
            int p = 92 + count * 20;
            for (int i = 0; i < groupCount; i++)
            {
                if (p + 4 > b.Length) break;
                int framesInGroup = BitConverter.ToInt32(b, p);
                p += 4;
                if (framesInGroup <= 0)
                {
                    groups.Add(new UiTexGroup());
                    continue;
                }
                if (p + framesInGroup * 8 > b.Length) break;
                int startFrame = BitConverter.ToInt32(b, p);
                int interval = BitConverter.ToInt32(b, p + 4);
                p += framesInGroup * 8;
                groups.Add(new UiTexGroup { Count = framesInGroup, StartFrame = startFrame, Interval = interval });
            }
            Groups = groups.ToArray();
        }

        /// <summary>
        /// Buttons/checkboxes reference frame groups, not frame indices; the group table
        /// maps a group id to its first frame (the normal-state artwork). Returns -1 when
        /// the group is empty or outside the table so the caller can fall back to Frame.
        /// </summary>
        public int GetGroupFrame(int group)
        {
            if (group < 0) return group;
            if (group < Groups.Length && Groups[group].Count > 0) return Groups[group].StartFrame;
            return -1;
        }

        public BitmapSource Texture
        {
            get
            {
                if (!_textureLoaded)
                {
                    _textureLoaded = true;
                    var directory = System.IO.Path.GetDirectoryName(Path);
                    var file = _assets.ResolveSibling(directory, TextureName);
                    if (file != null)
                    {
                        try { _texture = TextureLoader.Load(file); }
                        catch { _texture = null; }
                    }
                }
                return _texture;
            }
        }

        private readonly Dictionary<int, (int L, int T, int R, int B)> _diced =
            new Dictionary<int, (int L, int T, int R, int B)>();

        /// <summary>
        /// Border widths for ImageType=10 ("diced") frames, detected from the pixels:
        /// the number of identical columns/rows at each edge (rounded corners/edges).
        /// </summary>
        public (int L, int T, int R, int B) GetDicedBorders(int index)
        {
            if (_diced.TryGetValue(index, out var cached)) return cached;
            var result = (0, 0, 0, 0);
            var frame = GetFrame(index);
            if (frame != null && frame.PixelWidth > 2 && frame.PixelHeight > 2)
            {
                int w = frame.PixelWidth, h = frame.PixelHeight;
                var pixels = new int[w * h];
                frame.CopyPixels(pixels, w * 4, 0);
                int max = Math.Max(1, Math.Min(w, h) / 2);
                result = (
                    Math.Min(DetectEdge(pixels, w, h, true, true), max),
                    Math.Min(DetectEdge(pixels, w, h, false, true), max),
                    Math.Min(DetectEdge(pixels, w, h, true, false), max),
                    Math.Min(DetectEdge(pixels, w, h, false, false), max));
            }
            _diced[index] = result;
            return result;
        }

        private readonly Dictionary<int, (int L, int R)> _hCaps =
            new Dictionary<int, (int L, int R)>();

        /// <summary>
        /// Cap widths for ImageType=11 (horizontal three-slice): the number of leading and
        /// trailing columns whose vertical extent differs from the flat middle. The frame's
        /// outside tone is opaque (the corners of PVPUI22's plaques are (16,16,16) vs the
        /// shape's (13,13,13)), so the shape is detected by color, not alpha. Verified
        /// against the live capture: PVPUI22 frame 11 (48x20 drawn at 68x20) keeps 10 px
        /// caps, only the middle stretches (plain stretch flattened the pointed caps).
        /// </summary>
        public (int L, int R) GetHorizontalCaps(int index)
        {
            if (_hCaps.TryGetValue(index, out var cached)) return cached;
            var result = (0, 0);
            var frame = GetFrame(index);
            if (frame != null && frame.PixelWidth > 4 && frame.PixelHeight > 2)
            {
                int w = frame.PixelWidth, h = frame.PixelHeight;
                var pixels = new int[w * h];
                frame.CopyPixels(pixels, w * 4, 0);
                int reference = pixels[(h / 2) * w + w / 2];
                int refAlpha = (reference >> 24) & 0xFF;
                // Translucent art carries its shape in the alpha channel (PVPUI22's
                // plaques are ~alpha 45 black veils over the world); opaque art carries
                // it in the color (the frame dump composites over a background, so the
                // alpha cannot be read from it - the render's own pixels are used).
                bool byAlpha = refAlpha > 0 && refAlpha < 200;
                int alphaFloor = Math.Max(8, refAlpha / 2);

                bool MatchesShape(int color)
                {
                    if (byAlpha) return ((color >> 24) & 0xFF) >= alphaFloor;
                    return Math.Abs(((color >> 16) & 0xFF) - ((reference >> 16) & 0xFF)) <= 2 &&
                           Math.Abs(((color >> 8) & 0xFF) - ((reference >> 8) & 0xFF)) <= 2 &&
                           Math.Abs((color & 0xFF) - (reference & 0xFF)) <= 2;
                }

                int CountEdge(bool fromStart)
                {
                    int count = 0;
                    for (; count < w / 2; count++)
                    {
                        int x = fromStart ? count : w - 1 - count;
                        int top = -1, bottom = -1;
                        for (int y = 0; y < h; y++)
                        {
                            if (!MatchesShape(pixels[y * w + x])) continue;
                            if (top < 0) top = y;
                            bottom = y;
                        }
                        if (top == 0 && bottom == h - 1) break;
                    }
                    return count;
                }

                int l = CountEdge(true);
                int r = CountEdge(false);
                if (l + r < w - 1) result = (l, r);
            }
            _hCaps[index] = result;
            return result;
        }

        private static int DetectEdge(int[] pixels, int w, int h, bool horizontal, bool fromStart)
        {
            int limit = horizontal ? w : h;
            int outer = horizontal ? h : w;
            int count = 0;
            for (int k = 0; k < limit; k++)
            {
                bool same = true;
                for (int j = 0; j < outer; j++)
                {
                    int a = horizontal
                        ? pixels[j * w + (fromStart ? k : w - 1 - k)]
                        : pixels[(fromStart ? k : h - 1 - k) * w + j];
                    int b = horizontal
                        ? pixels[j * w + (fromStart ? 0 : w - 1)]
                        : pixels[(fromStart ? 0 : h - 1) * w + j];
                    if (a != b) { same = false; break; }
                }
                if (!same) break;
                count++;
            }
            return count;
        }

        public BitmapSource GetFrame(int index)
        {
            if (index < 0 || index >= Frames.Length) return null;
            var frame = Frames[index];
            if (frame.W <= 0 || frame.H <= 0) return null;
            var texture = Texture;
            if (texture == null) return null;
            if (frame.X < 0 || frame.Y < 0 || frame.X + frame.W > texture.PixelWidth || frame.Y + frame.H > texture.PixelHeight) return null;
            var cropped = new CroppedBitmap(texture, new Int32Rect(frame.X, frame.Y, frame.W, frame.H));
            cropped.Freeze();
            return cropped;
        }
    }

    public sealed class UiTexCache
    {
        private readonly AssetResolver _assets;
        private readonly Dictionary<string, UiTex> _cache = new Dictionary<string, UiTex>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BitmapSource> _raw = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        public UiTexCache(AssetResolver assets)
        {
            _assets = assets;
        }

        public AssetResolver Assets => _assets;

        private static bool IsRawImage(string path)
        {
            var ext = System.IO.Path.GetExtension(path);
            return ext.Equals(".tga", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".dds", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".png", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Some sections point Image straight at a texture file instead of a .UITex
        /// atlas (e.g. MiddleMap's LinkLine.tga); the engine loads those directly.
        /// </summary>
        private BitmapSource GetRaw(string path)
        {
            if (_raw.TryGetValue(path, out var hit)) return hit;
            BitmapSource source = null;
            try { source = TextureLoader.Load(path); }
            catch { source = null; }
            _raw[path] = source;
            return source;
        }

        public UiTex Get(string uitPath)
        {
            if (string.IsNullOrWhiteSpace(uitPath)) return null;
            if (_cache.TryGetValue(uitPath, out var hit)) return hit;
            var file = _assets.Resolve(uitPath);
            UiTex tex = null;
            if (file != null)
            {
                try { tex = new UiTex(file, _assets); }
                catch { tex = null; }
            }
            _cache[uitPath] = tex;
            return tex;
        }

        public BitmapSource GetFrame(string uitPath, int frame)
        {
            var file = _assets.Resolve(uitPath);
            if (file != null && IsRawImage(file))
                return GetRaw(file); // a raw texture has a single frame; the authored Frame is ignored
            var tex = Get(uitPath);
            return tex?.GetFrame(frame);
        }

        /// <summary>True when the atlas holds a frame record at this index whose rect is
        /// empty (w/h 0): the engine draws nothing there. Such an authored Frame is an
        /// intentional "no art" entry, not a missing asset (the audit counted them as
        /// placeholders).</summary>
        public bool IsEmptyFrame(string uitPath, int frame)
        {
            var tex = Get(uitPath);
            if (tex == null || frame < 0 || frame >= tex.Frames.Length) return false;
            var f = tex.Frames[frame];
            return f.W <= 0 || f.H <= 0;
        }

        public int GetGroupFrame(string uitPath, int group)
        {
            var tex = Get(uitPath);
            return tex != null ? tex.GetGroupFrame(group) : -1;
        }

        public (int L, int T, int R, int B) GetDicedBorders(string uitPath, int frame)
        {
            var tex = Get(uitPath);
            return tex != null ? tex.GetDicedBorders(frame) : (0, 0, 0, 0);
        }

        public (int L, int R) GetHorizontalCaps(string uitPath, int frame)
        {
            var tex = Get(uitPath);
            return tex != null ? tex.GetHorizontalCaps(frame) : (0, 0);
        }
    }
}
