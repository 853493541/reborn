using System;
using System.Collections.Generic;
using System.IO;

namespace MapUiApp.Engine
{
    /// <summary>Case-insensitive resolver for the extracted client UI tree.</summary>
    public sealed class AssetResolver
    {
        public readonly string Root;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _dirCache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public AssetResolver(string root)
        {
            Root = root;
        }

        public string Resolve(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) return null;
            if (_cache.TryGetValue(relative, out var hit)) return hit;
            var normalized = relative.Replace('\\', '/');
            string full = null;
            // The extraction root is the "ui" folder itself, so "ui\Image\..." maps to "<root>\Image\...".
            if (normalized.StartsWith("ui/", StringComparison.OrdinalIgnoreCase))
                full = ResolveInternal(normalized.Substring(3));
            full ??= ResolveInternal(normalized);
            _cache[relative] = full;
            return full;
        }

        public string ResolveSibling(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var direct = FindChild(directory, fileName);
            if (direct != null && File.Exists(direct)) return direct;
            var stem = Path.GetFileNameWithoutExtension(fileName);
            foreach (var extension in new[] { ".tga", ".dds", ".png" })
            {
                var alternative = FindChild(directory, stem + extension);
                if (alternative != null && File.Exists(alternative)) return alternative;
            }
            return null;
        }

        private string ResolveInternal(string relative)
        {
            var parts = relative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = Root;
            foreach (var part in parts)
            {
                var next = FindChild(current, part);
                if (next == null) return null;
                current = next;
            }
            return File.Exists(current) ? current : null;
        }

        private string FindChild(string directory, string name)
        {
            if (!_dirCache.TryGetValue(directory, out var listing))
            {
                listing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(directory))
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                        listing[Path.GetFileName(entry)] = entry;
                }
                _dirCache[directory] = listing;
            }
            return listing.TryGetValue(name, out var path) ? path : null;
        }
    }
}
