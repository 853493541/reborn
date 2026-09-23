// Map selector / launcher for the map host.
//
// Lists every map from MovieEditor\ResourcePack\MapList.tab, marks the ones
// that already have baked collision data, can bake a map's collision in one
// click (tools/bake_map_collision.py) and launches map_spike_host.exe with
// the right MAP_PATH / mode / spawn.
//
// Build: csc /platform:x64 /target:winexe /r:System.Windows.Forms.dll
//        /r:System.Drawing.dll /out:map_selector.exe MapSelector.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal sealed class MapSelector : Form
{
    const string MapListPath = @"C:\SeasunGame\MovieEditor\ResourcePack\MapList.tab";
    const string HostExe = @"C:\SeasunGame\MovieEditor\bin64\map_spike_host.exe";
    const string WorkDir = @"C:\SeasunGame\MovieEditor";
    static readonly string CollisionDir =
        Path.Combine(Path.GetDirectoryName(HostExe), "collision_data");

    sealed class MapEntry
    {
        public int Id;
        public string Name;
        public string Path;
        public bool HasCollision;
        public override string ToString()
        {
            return (HasCollision ? "[collision] " : "           ") + Name;
        }
    }

    readonly List<MapEntry> _maps = new List<MapEntry>();
    ListBox _list;
    TextBox _filter, _spawn, _log;
    RadioButton _playerMode, _freeCam;
    Button _launch, _bake, _refresh;
    readonly string _repo;

    MapSelector()
    {
        _repo = Environment.GetEnvironmentVariable("MAP_SELECTOR_REPO")
                ?? @"C:\Users\Zhibin Ren\Desktop\reborn";

        Text = "JX3 Map Selector";
        Width = 900;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Consolas", 9f);

        var top = new Panel { Dock = DockStyle.Top, Height = 40 };
        var filterLabel = new Label { Text = "filter:", Left = 8, Top = 12, Width = 45 };
        _filter = new TextBox { Left = 55, Top = 8, Width = 260 };
        _filter.TextChanged += delegate { FillList(); };
        _refresh = new Button { Text = "reload", Left = 325, Top = 7, Width = 70 };
        _refresh.Click += delegate { LoadMaps(); };
        top.Controls.Add(filterLabel);
        top.Controls.Add(_filter);
        top.Controls.Add(_refresh);

        _playerMode = new RadioButton { Text = "player (walk)", Left = 420, Top = 10, Width = 110, Checked = true };
        _freeCam = new RadioButton { Text = "free camera", Left = 535, Top = 10, Width = 110 };
        var spawnLabel = new Label { Text = "spawn x,y,z:", Left = 655, Top = 12, Width = 85 };
        _spawn = new TextBox { Left = 742, Top = 8, Width = 130 };
        top.Controls.Add(_playerMode);
        top.Controls.Add(_freeCam);
        top.Controls.Add(spawnLabel);
        top.Controls.Add(_spawn);

        _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _list.DoubleClick += delegate { Launch(); };

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 250 };
        _launch = new Button { Text = "LAUNCH", Left = 8, Top = 8, Width = 150, Height = 34 };
        _launch.Click += delegate { Launch(); };
        _bake = new Button { Text = "Bake collision", Left = 166, Top = 8, Width = 150, Height = 34 };
        _bake.Click += delegate { Bake(); };
        var openCol = new Button { Text = "open collision_data", Left = 324, Top = 8, Width = 170, Height = 34 };
        openCol.Click += delegate { try { Process.Start(CollisionDir); } catch (Exception e) { Log(e.Message); } };
        _log = new TextBox
        {
            Left = 8, Top = 50, Width = bottom.Width - 30, Height = 190,
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.Gainsboro
        };
        bottom.Controls.Add(_launch);
        bottom.Controls.Add(_bake);
        bottom.Controls.Add(openCol);
        bottom.Controls.Add(_log);

        Controls.Add(_list);
        Controls.Add(bottom);
        Controls.Add(top);
        Load += delegate { LoadMaps(); };
    }

    void LoadMaps()
    {
        _maps.Clear();
        try
        {
            string text = Encoding.GetEncoding("gbk").GetString(File.ReadAllBytes(MapListPath));
            foreach (string line in text.Split('\n'))
            {
                string[] p = line.TrimEnd('\r').Split('\t');
                int id;
                if (p.Length < 3 || !int.TryParse(p[0], out id)) continue;
                var e = new MapEntry { Id = id, Name = p[1], Path = p[2].Trim() };
                e.HasCollision = File.Exists(Path.Combine(CollisionDir, e.Name + "_structure_collision.bin"));
                _maps.Add(e);
            }
            Log(string.Format("loaded {0} maps from MapList.tab ({1} with baked collision)",
                _maps.Count, _maps.FindAll(m => m.HasCollision).Count));
        }
        catch (Exception e) { Log("MapList.tab: " + e.Message); }
        FillList();
    }

    void FillList()
    {
        string f = _filter.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var m in _maps)
        {
            if (f.Length == 0 || m.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                _list.Items.Add(m);
        }
        _list.EndUpdate();
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    MapEntry Selected()
    {
        return _list.SelectedItem as MapEntry;
    }

    void Log(string s)
    {
        _log.AppendText(s + "\r\n");
    }

    void Launch()
    {
        var m = Selected();
        if (m == null) { Log("no map selected"); return; }
        try
        {
            var psi = new ProcessStartInfo(HostExe)
            {
                WorkingDirectory = WorkDir,
                UseShellExecute = false
            };
            psi.EnvironmentVariables["MAP_PATH"] = m.Path;
            psi.EnvironmentVariables.Remove("MAP_AUTORUN");
            psi.EnvironmentVariables.Remove("MAP_PLAYER_DEMO");
            psi.EnvironmentVariables.Remove("MAP_DUMMY_MESH");
            psi.EnvironmentVariables.Remove("MAP_EXPORT_FBX");
            if (_playerMode.Checked) psi.EnvironmentVariables["MAP_PLAYER"] = "1";
            else psi.EnvironmentVariables.Remove("MAP_PLAYER");
            string spawn = _spawn.Text.Trim();
            if (spawn.Length > 0) psi.EnvironmentVariables["MAP_PLAYER_SPAWN"] = spawn;
            else psi.EnvironmentVariables.Remove("MAP_PLAYER_SPAWN");
            Process.Start(psi);
            Log(string.Format("launched {0}  [{1}]  spawn={2}", m.Name, m.Path,
                spawn.Length > 0 ? spawn : "(default)"));
        }
        catch (Exception e) { Log("launch failed: " + e.Message); }
    }

    void Bake()
    {
        var m = Selected();
        if (m == null) { Log("no map selected"); return; }
        string python = Path.Combine(_repo, ".venv", "Scripts", "python.exe");
        if (!File.Exists(python)) python = "python";
        string script = Path.Combine(_repo, "tools", "bake_map_collision.py");
        if (!File.Exists(script)) { Log("baker not found: " + script); return; }
        Log("baking " + m.Name + " ...");
        var psi = new ProcessStartInfo(python)
        {
            WorkingDirectory = _repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        psi.Arguments = string.Format("\"{0}\" --map \"{1}\" --copy-to \"{2}\"",
            script, m.Name, CollisionDir);
        try
        {
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e)
            { if (e.Data != null) BeginInvoke(new Action(() => Log(e.Data))); };
            p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e)
            { if (e.Data != null) BeginInvoke(new Action(() => Log("ERR " + e.Data))); };
            p.Exited += delegate
            {
                BeginInvoke(new Action(() =>
                {
                    Log("bake finished: " + m.Name);
                    LoadMaps();
                }));
            };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }
        catch (Exception e) { Log("bake failed: " + e.Message); }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MapSelector());
    }
}
