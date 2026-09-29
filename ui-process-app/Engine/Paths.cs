using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UiProcessApp.Engine
{
    /// <summary>
    /// Locates the app's asset roots. The UI-process worktree keeps the extracted
    /// config/strings inside ui-process-app/assets (git-ignored, re-extract from
    /// PakV4/text, see the README) or, when present, proof/minimap/ui; the committed
    /// UTF-8 text copies (per-window string tables) live in Data/text.
    /// </summary>
    public static class Paths
    {
        public static string AppRoot { get; private set; }
        public static string RepoRoot { get; private set; }
        public static string UiRoot { get; private set; }
        public static string PakRoot { get; private set; }
        public static string SchemeRoot { get; private set; }
        public static string ProofUiRoot { get; private set; }

        /// <summary>Asset roots in search order: app-extracted atlases, the map
        /// extraction (textures), then the app's INI-only copy.</summary>
        public static string[] ResolveRoots()
        {
            var roots = new List<string>
            {
                Path.Combine(AppRoot, "assets", "uitex"),
                ProofUiRoot,
                Path.Combine(AppRoot, "assets", "ui"),
            };
            return roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)).ToArray();
        }

        public static void Locate()
        {
            var env = Environment.GetEnvironmentVariable("UIPROC_APP_ROOT");
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            {
                AppRoot = env;
            }
            else
            {
                var dir = AppContext.BaseDirectory;
                for (int i = 0; i < 10 && dir != null; i++)
                {
                    if (Directory.Exists(Path.Combine(dir, "assets", "ui", "Config")))
                    {
                        AppRoot = dir;
                        break;
                    }
                    dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
                }
            }
            if (AppRoot == null)
                throw new DirectoryNotFoundException(
                    "Could not locate ui-process-app/assets above " + AppContext.BaseDirectory +
                    " (set UIPROC_APP_ROOT to override).");

            RepoRoot = Path.GetDirectoryName(AppRoot.TrimEnd(Path.DirectorySeparatorChar));

            // The extracted texture tree (proof/minimap/ui) is git-ignored, so look for it:
            //   1) UIPROC_UI_ROOT override
            //   2) this worktree
            //   3) sibling worktrees (e.g. Desktop\reborn\proof\minimap\ui)
            //   4) fall back to the app's INI-only asset copy (wireframe rendering)
            var explicitRoot = Environment.GetEnvironmentVariable("UIPROC_UI_ROOT");
            if (!string.IsNullOrWhiteSpace(explicitRoot) && Directory.Exists(explicitRoot))
            {
                ProofUiRoot = explicitRoot;
            }
            else
            {
                var candidates = new List<string> { Path.Combine(RepoRoot ?? "", "proof", "minimap", "ui") };
                var parent = Path.GetDirectoryName((RepoRoot ?? "").TrimEnd(Path.DirectorySeparatorChar));
                if (parent != null && Directory.Exists(parent))
                {
                    foreach (var dir in Directory.EnumerateDirectories(parent))
                        candidates.Add(Path.Combine(dir, "proof", "minimap", "ui"));
                }
                ProofUiRoot = candidates.FirstOrDefault(Directory.Exists);
            }
            UiRoot = ProofUiRoot ?? Path.Combine(AppRoot, "assets", "ui");
            PakRoot = Path.Combine(AppRoot, "assets", "pak");
            SchemeRoot = Path.Combine(AppRoot, "assets", "ui", "Scheme", "Case");

            // Load every extracted string table (each window's StringTable= plus the
            // base tables); ids are global so order only matters for duplicates.
            var tables = new List<string>();
            if (Directory.Exists(SchemeRoot))
                tables.AddRange(Directory.GetFiles(SchemeRoot, "*.txt"));
            var proofScheme = Path.Combine(ProofUiRoot ?? "", "Scheme", "Case");
            if (Directory.Exists(proofScheme))
                tables.AddRange(Directory.GetFiles(proofScheme, "*.txt"));
            // Committed UTF-8 text copies (Data/text, see tools/prepare_ui_text.py).
            // They carry the per-window StringTable= files the local extraction lacks
            // (e.g. string_ArenaCorpsPanel.txt for NewBattleFieldQueue).
            var appTextScheme = Path.Combine(AppRoot, "Data", "text", "ui", "Scheme", "Case");
            if (Directory.Exists(appTextScheme))
                tables.AddRange(Directory.GetFiles(appTextScheme, "*.txt"));
            tables.Add(Path.Combine(PakRoot, "string_PVPAcount.txt"));
            Strings.Load(tables.ToArray());

            Fonts.Load(Path.Combine(SchemeRoot, "font.ini"),
                       Path.Combine(SchemeRoot, "fontlist.ini"),
                       Path.Combine(SchemeRoot, "color.txt"));
        }
    }
}
