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

            // Frame groups follow the frame table: (count, startFrame, intervalMs)
            // plus (count-1) extra u32 values for multi-frame groups.
            var groups = new List<UiTexGroup>();
            int p = 92 + count * 20;
            while (p + 12 <= b.Length)
            {
                int groupCount = BitConverter.ToInt32(b, p);
                int startFrame = BitConverter.ToInt32(b, p + 4);
                int interval = BitConverter.ToInt32(b, p + 8);
                if (groupCount == 0 && startFrame == 0 && interval == 0)
                {
                    groups.Add(new UiTexGroup());
                    p += 12;
                    continue;
                }
                if (groupCount < 1 || groupCount > 64 || startFrame < 0 || startFrame >= count || interval < 1 || interval > 1000) break;
                groups.Add(new UiTexGroup { Count = groupCount, StartFrame = startFrame, Interval = interval });
                p += 12 + (groupCount - 1) * 4;
            }
            Groups = groups.ToArray();
        }

        /// <summary>
        /// Buttons/checkboxes reference frame groups, not frame indices; the group table
        /// maps a group id to its first frame (the normal-state artwork).
        /// </summary>
        public int GetGroupFrame(int group)
        {
            if (group < 0) return group;
            if (Groups.Length > 0)
            {
                if (group < Groups.Length && Groups[group].Count > 0) return Groups[group].StartFrame;
                return group + Groups[0].StartFrame;
            }
            return group + 1;
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
                        try { _texture = Tga.Load(file); }
                        catch { _texture = null; }
                    }
                }
                return _texture;
            }
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

        public UiTexCache(AssetResolver assets)
        {
            _assets = assets;
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
            var tex = Get(uitPath);
            return tex?.GetFrame(frame);
        }

        public int GetGroupFrame(string uitPath, int group)
        {
            var tex = Get(uitPath);
            return tex != null ? tex.GetGroupFrame(group) : group + 1;
        }
    }
}
