using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapUiApp.Engine
{
    /// <summary>Case-insensitive resolver for one or more extracted client UI trees,
    /// searched in order (app-extracted atlases first, then the map extraction).</summary>
    public sealed class AssetResolver
    {
        public readonly string Root;
        private readonly string[] _roots;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _dirCache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public AssetResolver(params string[] roots)
        {
            _roots = (roots ?? new string[0]).Where(r => !string.IsNullOrWhiteSpace(r)).ToArray();
            Root = _roots.FirstOrDefault();
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

        /// <summary>All existing texture candidates for a sibling name, in preference
        /// order (the direct name first, then stem.tga/.dds/.png). The atlas loader uses
        /// this to skip a stale texture that cannot cover the atlas's frame extent
        /// (Cangjian ships a 128x128 tga beside the real 868x428 dds).</summary>
        public System.Collections.Generic.List<string> ResolveSiblingCandidates(string directory, string fileName)
        {
            var list = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(fileName)) return list;
            var direct = FindChild(directory, fileName);
            if (direct != null && File.Exists(direct)) list.Add(direct);
            var stem = Path.GetFileNameWithoutExtension(fileName);
            foreach (var extension in new[] { ".tga", ".dds", ".png" })
            {
                var alternative = FindChild(directory, stem + extension);
                if (alternative != null && File.Exists(alternative) && !list.Contains(alternative)) list.Add(alternative);
            }
            return list;
        }

        private string ResolveInternal(string relative)
        {
            var parts = relative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var root in _roots)
            {
                var current = root;
                bool ok = true;
                foreach (var part in parts)
                {
                    var next = FindChild(current, part);
                    if (next == null) { ok = false; break; }
                    current = next;
                }
                if (ok && File.Exists(current)) return current;
            }
            // Flat extraction roots (files placed directly under the root): match by name.
            if (parts.Length > 0)
            {
                var fileName = parts[parts.Length - 1];
                foreach (var root in _roots)
                {
                    var index = NameIndex(root);
                    if (index.TryGetValue(fileName, out var hit)) return hit;
                    // The client's texture loader dispatches TGA/DDS: a section may
                    // request X.tga while the pak ships X.dds (e.g. the personal-card
                    // avatar HHTX_003). Try the sibling extension names.
                    var stem = Path.GetFileNameWithoutExtension(fileName);
                    foreach (var extension in new[] { ".UITex", ".tga", ".dds", ".png" })
                    {
                        if (index.TryGetValue(stem + extension, out var alt)) return alt;
                    }
                }
            }
            return null;
        }

        private readonly Dictionary<string, Dictionary<string, string>> _nameIndex =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, string> NameIndex(string root)
        {
            if (_nameIndex.TryGetValue(root, out var index)) return index;
            index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    index[Path.GetFileName(file)] = file;
            }
            _nameIndex[root] = index;
            return index;
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
