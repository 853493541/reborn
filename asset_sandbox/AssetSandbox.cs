// AssetSandbox — browse and display 绝境战场 runtime doodad assets on the real map.
//
// Data (extract once, gitignored local game data):
//   python tools\netcode\mode\extract_doodad_represent.py
//   -> assets\mode\doodad\{doodad.txt, DoodadTemplate.tab, doodad_index.tsv}
//
// Build: asset_sandbox\build_asset_sandbox.cmd
// Run:   asset_sandbox\run_asset_sandbox.cmd   (sets AS_DATA, cwd = editor root)
//
// Display path: every unique RepresentID used by the 沙漠风暴 / 沙漠风暴_寻宝模式
// DoodadTemplate sets, spawned with AddDummyModel + the model path from
// doodad.txt (the table the real client's JX3DoodadRepresent renders).
// AS_REPRESENT=1 switches to KGSceneCLR.AddRepresentModel, but note the
// MovieEditor host resolves that ID space to actors/NPCs, not doodads.
//
// Controls: right-drag rotate, Alt/Shift+right-drag pan, wheel zoom, WASD/QE
// move, Shift fast, R reset camera, F focus, double-click list = spawn.
//
// Env:
//   AS_DATA=<dir>        data dir (default <exeDir>\asset_sandbox_data)
//   AS_MAP=<jsonmap>     default 龙门寻宝
//   AS_AUTORUN=ms        exit after N ms (0 = interactive, default 0)
//   AS_SMOKE=1,46396,..  spawn these represent ids at the stage, then exit
//   AS_SHOTS=3000,6000   screenshot times (ms); default in smoke: 2500,5000
//   AS_REPRESENT=1       use KGSceneCLR.AddRepresentModel instead of the mesh
//                        path (host space = actors/NPCs, not doodads)
//   AS_SPACING=250       stage grid spacing (world units)
//   AS_GLOW_SCALE=1      multiplier on the table EffectScale for the glow PSS
//   AS_REF=1             spawn the 花萝 actor next to the stage as a 1.7 m scale reference
//   AS_STAGE=x,y,z       explicit stage center (y optional -> terrain)
//   AS_FLAT=1            keep the explicit stage y for all spawns (flat pad)
//   AS_CAM_DIST / AS_CAM_UP   initial camera distance / height (default 1100/350)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MovieEngineCLR;

internal static class AssetSandbox
{
    const int CMS_FORWARD = 1, CMS_BACK = 2, CMS_MOVE_LEFT = 64, CMS_MOVE_RIGHT = 128,
              CMS_UP = 256, CMS_DOWN = 2048, CMS_FAST = 4096;

    sealed class DoodadRow
    {
        public int Rid;
        public string Name = "";
        public int Templates = 1;
        public string Set = "";
        public string Drops = "";
        public float Scale = 1f;
        public float EffectScale = 1f;
        public string Model = "";
        public string Ani = "";
        public string Sfx = "";
        public override string ToString() { return Rid + "  " + Name; }
    }

    sealed class Spawned
    {
        public bool IsRepresent;
        public bool IsSfx;
        public long Handle;
        public string Name = "";
        public int Rid;
    }

    static readonly List<DoodadRow> AllRows = new List<DoodadRow>();
    static readonly List<Spawned> SpawnedList = new List<Spawned>();
    static readonly List<int[]> Pending = new List<int[]>();
    static readonly List<long> ShotTimes = new List<long>();

    static KGSceneCLR scene;
    static KGEngineCLR engine;
    static TerrainSampler sampler;
    static Form form;
    static Panel viewport;
    static ListView list;
    static TextBox txtSearch;
    static CheckBox[] setChecks;
    static readonly string[] SET_NAMES = { "\u9F99\u95E8", "\u6D77\u5C9B", "\u767D\u9F99", "\u5BFB\u5B9D" };
    static Label status;
    static Label hud;
    static TextBox logBox;
    static string outDir;
    static int seq;
    static float stageX, stageY, stageZ;
    static float spacing = 250f;
    static float glowScale = 1f;
    static bool useRepresent;
    static bool refEnabled;
    static bool refAlive;
    static bool flatMode;
    static float camDist = 1100f;
    static float camUp = 350f;
    static string actorPath;
    static Func<int, int, int> MakeLParam = delegate(int x, int y)
    {
        return ((y & 0xFFFF) << 16) | (x & 0xFFFF);
    };

    [STAThread]
    private static void Main(string[] args)
    {
        string editorRoot = @"C:\SeasunGame\MovieEditor";
        string startupPath = Path.Combine(editorRoot, "bin64");
        string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
        string mapPath = Env("AS_MAP",
            "data\\source\\maps\\\u9F99\u95E8\u5BFB\u5B9D\\\u9F99\u95E8\u5BFB\u5B9D.jsonmap");
        string dataDir = Env("AS_DATA", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "asset_sandbox_data"));
        useRepresent = Env("AS_REPRESENT", "0") == "1";
        refEnabled = Env("AS_REF", "0") == "1";
        flatMode = Env("AS_FLAT", "0") == "1";
        float.TryParse(Env("AS_CAM_DIST", "1100"), NumberStyles.Float, CultureInfo.InvariantCulture, out camDist);
        float.TryParse(Env("AS_CAM_UP", "350"), NumberStyles.Float, CultureInfo.InvariantCulture, out camUp);
        actorPath = Path.Combine(editorRoot, "source", "\u82B1\u841D\u65E0\u52A8\u4F5C.actor");
        float.TryParse(Env("AS_SPACING", "250"), NumberStyles.Float, CultureInfo.InvariantCulture, out spacing);
        float.TryParse(Env("AS_GLOW_SCALE", "1"), NumberStyles.Float, CultureInfo.InvariantCulture, out glowScale);
        if (glowScale <= 0f) glowScale = 1f;
        long autoRunMs = 0;
        long.TryParse(Env("AS_AUTORUN", "0"), out autoRunMs);

        outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "asset_sandbox_out");
        Directory.CreateDirectory(outDir);
        LogInit();

        string[] smokeIds = null;
        string smoke = Env("AS_SMOKE", "");
        if (smoke.Length > 0) smokeIds = smoke.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (smokeIds != null && autoRunMs <= 0) autoRunMs = 9000;
        string shots = Env("AS_SHOTS", smokeIds != null ? "2500,5000,8000" : "");
        foreach (string s in shots.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            long t;
            if (long.TryParse(s, out t)) ShotTimes.Add(t);
        }

        Log("start map=" + mapPath);
        Log("data=" + dataDir + " useRepresent=" + useRepresent + " smoke=" + (smokeIds == null ? "-" : smoke.Length > 0 ? "yes" : "-"));

        if (!LoadRows(dataDir)) return;

        BuildUi();
        form.Show();
        Application.DoEvents();

        var baselib = new KGBaseCLR();
        engine = new KGEngineCLR();
        var editor = new KGMovieEditorCLR();

        engine.SetRootPath(workingDir);
        try { baselib.InitConsoleLog(); } catch (Exception e) { Log("InitConsoleLog: " + e.Message); }
        Directory.CreateDirectory(Path.Combine(startupPath, "logs"));
        int r1 = 0, r2 = 0, r3 = 0;
        try { r1 = baselib.InitPath(workingDir, false); } catch (Exception e) { Log("InitPath ex: " + e.Message); }
        try { r2 = baselib.InitMemory("MovieEditor.memory"); } catch (Exception e) { Log("InitMemory ex: " + e.Message); }
        try { r3 = baselib.InitPak(false); } catch (Exception e) { Log("InitPak ex: " + e.Message); }
        Log(string.Format("InitPath={0} InitMemory={1} InitPak={2}", r1, r2, r3));

        int err = 1;
        int ok = 0;
        try { ok = engine.Init3DEngine(startupPath, startupPath, workingDir, 0, "./configHttpFile.ini", ref err); }
        catch (Exception e) { Log("Init3DEngine ex: " + e); return; }
        Log(string.Format("Init3DEngine={0} err={1}", ok, err));
        if (ok == 0) { Log("FATAL: engine init failed"); return; }
        try { Log("editor.Init result=" + editor.Init(editorRoot, err, form.Handle.ToInt64())); }
        catch (Exception e) { Log("editor.Init ex: " + e.Message); }

        scene = new KGSceneCLR();
        int loadResult = scene.LoadMap(mapPath, false);
        Log("LoadMap result=" + loadResult);
        if (loadResult < 0) { Log("FATAL: LoadMap failed"); return; }
        scene.SetActiveEnvironment();
        long winId = scene.AddOutputWindow("", viewport.Handle.ToInt64(), 0);
        Log("winId=" + winId);

        try
        {
            string physDll = Path.Combine(workingDir, "bin64", "PhysicsEngineX64.dll");
            sampler = new TerrainSampler(physDll, mapPath, Log);
        }
        catch (Exception e) { Log("TerrainSampler ex: " + e.Message); }

        SetupStage();
        ProbeRepresentApi();
        if (refEnabled) SpawnRef();

        if (smokeIds != null)
        {
            var rows = new List<DoodadRow>();
            foreach (string s in smokeIds)
            {
                int rid;
                if (!int.TryParse(s.Trim(), out rid)) continue;
                DoodadRow row = FindRow(rid);
                if (row == null)
                {
                    row = new DoodadRow();
                    row.Rid = rid;
                    row.Name = "(not in index)";
                    row.Set = "smoke";
                }
                rows.Add(row);
            }
            SpawnRows(rows);
        }

        Log("ready - right-drag rotate, wheel zoom, WASD move, double-click list to spawn");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int shotIndex = 0;
        while (!form.IsDisposed)
        {
            FlushPending();
            try { engine.FrameMove(); engine.Render(); } catch (Exception e) { Log("frame ex: " + e.Message); }
            Application.DoEvents();
            long ms = sw.ElapsedMilliseconds;
            while (shotIndex < ShotTimes.Count && ms >= ShotTimes[shotIndex])
            {
                Shot("auto_" + ShotTimes[shotIndex]);
                shotIndex++;
            }
            if (autoRunMs > 0 && ms >= autoRunMs) break;
            Thread.Sleep(15);
        }
        Log("DONE");
    }

    // ------------------------------------------------------------------ data

    static bool LoadRows(string dataDir)
    {
        string index = Path.Combine(dataDir, "doodad_index.tsv");
        if (File.Exists(index))
        {
            try
            {
                foreach (string line in File.ReadAllLines(index, Encoding.UTF8))
                {
                    string[] c = line.Split('\t');
                    if (c.Length < 12 || c[0] == "RepresentID") continue;
                    var row = new DoodadRow();
                    row.Rid = int.Parse(c[0]);
                    row.Name = c[1];
                    int.TryParse(c[2], out row.Templates);
                    row.Set = c[3];
                    row.Drops = c[4];
                    float.TryParse(c[5], NumberStyles.Float, CultureInfo.InvariantCulture, out row.Scale);
                    if (row.Scale <= 0f) row.Scale = 1f;
                    float.TryParse(c[6], NumberStyles.Float, CultureInfo.InvariantCulture, out row.EffectScale);
                    if (row.EffectScale <= 0f) row.EffectScale = 1f;
                    row.Model = c[9];
                    row.Ani = c[10];
                    row.Sfx = c[11];
                    AllRows.Add(row);
                }
                Log("rows=" + AllRows.Count + " from " + index);
                return AllRows.Count > 0;
            }
            catch (Exception e) { Log("index read ex: " + e.Message); }
        }
        string raw = Path.Combine(dataDir, "doodad.txt");
        string tmpl = Path.Combine(dataDir, "DoodadTemplate.tab");
        if (File.Exists(raw) && File.Exists(tmpl))
            return BuildRowsFromRaw(raw, tmpl);
        Log("FATAL: no doodad_index.tsv / doodad.txt+DoodadTemplate.tab in " + dataDir);
        Log("run: python tools\\netcode\\mode\\extract_doodad_represent.py");
        return false;
    }

    static bool BuildRowsFromRaw(string rawPath, string tmplPath)
    {
        var rep = new Dictionary<int, string[]>();
        bool first = true;
        foreach (string line in File.ReadAllLines(rawPath, Encoding.GetEncoding(936)))
        {
            string[] c = line.Split('\t');
            if (first) { RepHeader = c; first = false; continue; }
            if (c.Length < 2) continue;
            int rid;
            if (int.TryParse(c[0], out rid)) rep[rid] = c;
        }
        string[] header = null;
        var byRid = new Dictionary<int, DoodadRow>();
        foreach (string line in File.ReadAllLines(tmplPath, Encoding.GetEncoding(936)))
        {
            string[] c = line.Split('\t');
            if (header == null) { header = c; continue; }
            if (c.Length < header.Length) continue;
            string mapName = Field(header, c, "MapName");
            if (mapName != "\u6C99\u6F20\u98CE\u66B4" && mapName != "\u6C99\u6F20\u98CE\u66B4_\u5BFB\u5B9D\u6A21\u5F0F") continue;
            int rid;
            if (!int.TryParse(Field(header, c, "RepresentID"), out rid) || rid <= 0) continue;
            string[] r;
            if (!rep.TryGetValue(rid, out r)) continue;
            DoodadRow row;
            if (!byRid.TryGetValue(rid, out row))
            {
                row = new DoodadRow();
                row.Rid = rid;
                row.Name = Field(header, c, "Name");
                row.Scale = ParseScale(RepField(r, "ModelScale"));
                row.EffectScale = ParseScale(RepField(r, "EffectScale"));
                row.Model = RepField(r, "IdleModelFile");
                row.Ani = RepField(r, "IdleAniFile");
                row.Sfx = RepField(r, "IdleSFXFile");
                byRid[rid] = row;
            }
            row.Templates++;
            string drop = "";
            for (int i = 1; i <= 10; i++)
            {
                string v = Field(header, c, "Drop" + i);
                if (v.Length > 0) { drop = v; break; }
            }
            if (drop.Length > 0 && row.Drops.IndexOf(drop, StringComparison.Ordinal) < 0)
                row.Drops = row.Drops.Length == 0 ? drop : row.Drops + ", " + drop;
            row.Set = SetLabel(drop, row.Model, row.Name);
            if (row.Set == "\u6797\u6D77") byRid.Remove(rid);
        }
        AllRows.AddRange(byRid.Values);
        Log("rows=" + AllRows.Count + " (raw join)");
        return AllRows.Count > 0;
    }

    static string Field(string[] header, string[] cols, string name)
    {
        for (int i = 0; i < header.Length; i++)
            if (header[i] == name) return i < cols.Length ? cols[i].Trim() : "";
        return "";
    }

    static string RepField(string[] repRow, string name)
    {
        // doodad.txt header is the first row; resolve once and cache column index
        string[] h = RepHeader;
        if (h == null) return "";
        for (int i = 0; i < h.Length; i++)
            if (h[i] == name) return i < repRow.Length ? repRow[i].Trim() : "";
        return "";
    }

    static string[] RepHeader;
    static float ParseScale(string s)
    {
        float f;
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && f > 0f) return f;
        return 1f;
    }

    static string SetLabel(string drop, string model, string name)
    {
        string low = (drop + " " + model + " " + name).ToLowerInvariant();
        if (drop.IndexOf("_lw", StringComparison.OrdinalIgnoreCase) >= 0
            || drop.IndexOf("_skill", StringComparison.OrdinalIgnoreCase) >= 0
            || low.IndexOf("lhjj") >= 0 || low.IndexOf("\u6797\u6D77") >= 0) return "\u6797\u6D77";
        if (low.IndexOf("_sea") >= 0 || low.IndexOf("hdjj") >= 0 || low.IndexOf("\u6D77\u5C9B") >= 0) return "\u6D77\u5C9B";
        if (low.IndexOf("_bl") >= 0 || low.IndexOf("blk") >= 0 || low.IndexOf("\u767D\u9F99") >= 0) return "\u767D\u9F99";
        if (low.IndexOf("_xunbao") >= 0) return "\u5BFB\u5B9D";
        return "\u9F99\u95E8";
    }

    static DoodadRow FindRow(int rid)
    {
        foreach (DoodadRow r in AllRows) if (r.Rid == rid) return r;
        return null;
    }

    // -------------------------------------------------------------------- ui

    static void BuildUi()
    {
        form = new Form();
        form.Text = "sandbox-asset - \u7EDD\u5883\u6218\u573A doodad assets";
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new Size(1500, 920);
        form.KeyPreview = true;

        var outer = new SplitContainer();
        outer.Dock = DockStyle.Fill;
        outer.Orientation = Orientation.Horizontal;
        outer.SplitterWidth = 5;
        form.Controls.Add(outer);

        var main = new SplitContainer();
        main.Dock = DockStyle.Fill;
        main.Orientation = Orientation.Vertical;
        main.SplitterDistance = 500;
        main.FixedPanel = FixedPanel.Panel1;
        outer.Panel1.Controls.Add(main);

        var left = new TableLayoutPanel();
        left.Dock = DockStyle.Fill;
        left.ColumnCount = 1;
        left.RowCount = 5;
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));
        main.Panel1.Controls.Add(left);

        txtSearch = new TextBox();
        txtSearch.Dock = DockStyle.Fill;
        txtSearch.Text = "";
        txtSearch.ForeColor = Color.Gray;
        txtSearch.Text = "\u641C\u7D22\u540D\u79F0 / RepresentID / \u6A21\u578B";
        txtSearch.GotFocus += delegate { if (txtSearch.ForeColor == Color.Gray) { txtSearch.Text = ""; txtSearch.ForeColor = Color.Black; } };
        txtSearch.LostFocus += delegate { if (txtSearch.Text.Length == 0) { txtSearch.ForeColor = Color.Gray; txtSearch.Text = "\u641C\u7D22\u540D\u79F0 / RepresentID / \u6A21\u578B"; } };
        txtSearch.TextChanged += delegate { FillList(); };
        left.Controls.Add(txtSearch, 0, 0);

        var sets = new FlowLayoutPanel();
        sets.Dock = DockStyle.Fill;
        sets.WrapContents = false;
        setChecks = new CheckBox[SET_NAMES.Length];
        for (int i = 0; i < SET_NAMES.Length; i++)
        {
            var cb = new CheckBox();
            cb.Text = SET_NAMES[i];
            cb.AutoSize = true;
            cb.Margin = new Padding(3, 6, 10, 0);
            cb.Checked = SET_NAMES[i] == "\u767D\u9F99"; // default filter: 白龙 only
            cb.CheckedChanged += delegate { FillList(); };
            setChecks[i] = cb;
            sets.Controls.Add(cb);
        }
        left.Controls.Add(sets, 0, 1);

        var buttons = new FlowLayoutPanel();
        buttons.Dock = DockStyle.Fill;
        buttons.WrapContents = true;
        AddButton(buttons, "Spawn", delegate { SpawnRows(SelectedRows()); });
        AddButton(buttons, "Spawn x5", delegate { SpawnRow5(); });
        AddButton(buttons, "Remove last", delegate { RemoveLast(); });
        AddButton(buttons, "Clear", delegate { ClearScene(); });
        AddButton(buttons, "Snap", delegate { scene.ResetCameraPosLookAtUp(); });
        AddButton(buttons, "Shot", delegate { Shot("manual"); });
        AddButton(buttons, "Reload", delegate { Reload(); });
        AddButton(buttons, "Ref", delegate { ToggleRef(); });
        left.Controls.Add(buttons, 0, 2);

        list = new ListView();
        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.GridLines = true;
        list.MultiSelect = true;
        list.HideSelection = false;
        list.Columns.Add("ID", 62);
        list.Columns.Add("\u540D\u79F0", 120);
        list.Columns.Add("\u5957\u88C5", 46);
        list.Columns.Add("\u6A21\u578B", 200);
        list.Columns.Add("Drop", 130);
        list.SelectedIndexChanged += delegate { UpdateStatus(); };
        list.DoubleClick += delegate { SpawnRows(SelectedRows()); };
        left.Controls.Add(list, 0, 3);

        status = new Label();
        status.Dock = DockStyle.Fill;
        status.Font = new Font("Consolas", 8.5f);
        status.Text = "\u9009\u4E2D\u4E00\u884C\u558A\u51FA RepresentID / \u5F15\u64CE\u6A21\u578B\u8DEF\u5F84";
        left.Controls.Add(status, 0, 4);

        viewport = new Panel();
        viewport.Dock = DockStyle.Fill;
        viewport.BackColor = Color.Black;
        main.Panel2.Controls.Add(viewport);
        hud = new Label();
        hud.AutoSize = true;
        hud.ForeColor = Color.White;
        hud.BackColor = Color.FromArgb(160, 0, 0, 0);
        hud.Font = new Font("Consolas", 10f);
        hud.Padding = new Padding(6);
        hud.Location = new Point(10, 10);
        hud.Text = "loading...";
        viewport.Controls.Add(hud);

        logBox = new TextBox();
        logBox.Dock = DockStyle.Fill;
        logBox.Multiline = true;
        logBox.ReadOnly = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.BackColor = Color.Black;
        logBox.ForeColor = Color.LightGreen;
        logBox.Font = new Font("Consolas", 8.5f);
        outer.Panel2.Controls.Add(logBox);

        form.Shown += delegate
        {
            try
            {
                outer.SplitterDistance = outer.Height - 170;
                main.SplitterDistance = 500;
            }
            catch { }
        };

        HookViewportInput();
        HookKeyboard();
        FillList();
    }

    static void AddButton(Control parent, string text, EventHandler click)
    {
        var b = new Button();
        b.Text = text;
        b.Width = 84;
        b.Height = 30;
        b.Click += click;
        parent.Controls.Add(b);
    }

    static void HookViewportInput()
    {
        viewport.MouseMove += delegate(object s, MouseEventArgs e)
        {
            Pending.Add(new int[] { 30, 1, e.X, e.Y });
            if (e.Button == MouseButtons.Right)
            {
                int a = (Control.ModifierKeys & Keys.Shift) != 0 ? 4
                      : (Control.ModifierKeys & Keys.Alt) != 0 ? 1 : 3;
                Pending.Add(new int[] { a, 1, e.X, e.Y });
            }
        };
        MouseEventHandler wheel = delegate(object s, MouseEventArgs e)
        {
            Pending.Add(new int[] { 31, e.Delta < 0 ? 0 : 1, 0, 1 });
        };
        viewport.MouseWheel += wheel;
        form.MouseWheel += wheel;
    }

    static void HookKeyboard()
    {
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (form.ActiveControl is TextBox || form.ActiveControl is ComboBox) return;
            int f = 0;
            if (e.KeyCode == Keys.W) f = CMS_FORWARD;
            else if (e.KeyCode == Keys.S) f = CMS_BACK;
            else if (e.KeyCode == Keys.A) f = CMS_MOVE_LEFT;
            else if (e.KeyCode == Keys.D) f = CMS_MOVE_RIGHT;
            else if (e.KeyCode == Keys.Q) f = CMS_UP;
            else if (e.KeyCode == Keys.E) f = CMS_DOWN;
            else if (e.KeyCode == Keys.ShiftKey) f = CMS_FAST;
            else if (e.KeyCode == Keys.R) { try { scene.ResetCameraPosLookAtUp(); } catch { } return; }
            else if (e.KeyCode == Keys.F) { Pending.Add(new int[] { 82, 0, 0, 0 }); return; }
            if (f == 0) return;
            if (e.Shift && f != CMS_UP && f != CMS_DOWN && f != CMS_FAST) f |= CMS_FAST;
            Pending.Add(new int[] { 80, f, 1, 0 });
            e.Handled = true;
        };
        form.KeyUp += delegate(object s, KeyEventArgs e)
        {
            int f = 0;
            if (e.KeyCode == Keys.W) f = CMS_FORWARD;
            else if (e.KeyCode == Keys.S) f = CMS_BACK;
            else if (e.KeyCode == Keys.A) f = CMS_MOVE_LEFT;
            else if (e.KeyCode == Keys.D) f = CMS_MOVE_RIGHT;
            else if (e.KeyCode == Keys.Q) f = CMS_UP;
            else if (e.KeyCode == Keys.E) f = CMS_DOWN;
            else if (e.KeyCode == Keys.ShiftKey) f = CMS_FAST;
            if (f == 0) return;
            Pending.Add(new int[] { 80, f, 0, 0 });
        };
    }

    static void FlushPending()
    {
        while (Pending.Count > 0)
        {
            int[] cmd = Pending[0];
            Pending.RemoveAt(0);
            try
            {
                if (cmd[0] == 80) scene.SetCamareMoveState(cmd[1], cmd[2]);
                else if (cmd[0] == 82) scene.ExecAction(1001, 0, 0, 0);
                else if (cmd[0] == 31) scene.ExecAction(31, 1, cmd[1], 1);
                else scene.ExecAction(cmd[0], cmd[1], 0, MakeLParam(cmd[2], cmd[3]));
            }
            catch (Exception e) { Log("action " + cmd[0] + " ex: " + e.Message); }
        }
    }

    // ------------------------------------------------------------------ list

    static void FillList()
    {
        if (list == null) return;
        string q = txtSearch.ForeColor == Color.Gray ? "" : txtSearch.Text.Trim();
        var picked = new List<string>();
        if (setChecks != null)
            foreach (CheckBox cb in setChecks)
                if (cb.Checked) picked.Add(cb.Text);
        bool allSets = picked.Count == 0; // none checked = all
        list.BeginUpdate();
        list.Items.Clear();
        foreach (DoodadRow r in AllRows)
        {
            if (!allSets && picked.IndexOf(r.Set) < 0) continue;
            if (q.Length > 0
                && r.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                && r.Rid.ToString().IndexOf(q, StringComparison.Ordinal) < 0
                && r.Model.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var it = new ListViewItem(r.Rid.ToString());
            it.SubItems.Add(r.Name);
            it.SubItems.Add(r.Set);
            it.SubItems.Add(Short(r.Model));
            it.SubItems.Add(Short(r.Drops));
            it.Tag = r;
            list.Items.Add(it);
        }
        list.EndUpdate();
        hud.Text = string.Format("rows {0}/{1} | spawned {2} | {3}", list.Items.Count, AllRows.Count, SpawnedList.Count, useRepresent ? "represent(actor space)" : "dummy mesh");
    }

    static string Short(string s)
    {
        if (s.Length <= 46) return s;
        return "..." + s.Substring(s.Length - 43);
    }

    static List<DoodadRow> SelectedRows()
    {
        var rows = new List<DoodadRow>();
        foreach (ListViewItem it in list.SelectedItems)
            if (it.Tag is DoodadRow) rows.Add((DoodadRow)it.Tag);
        return rows;
    }

    static void UpdateStatus()
    {
        List<DoodadRow> rows = SelectedRows();
        if (rows.Count == 0) { status.Text = ""; return; }
        DoodadRow r = rows[0];
        string mp = "", ap = "";
        try { mp = scene.GetRepresentModelPath(r.Rid); } catch (Exception e) { mp = "ex:" + e.Message; }
        try { ap = scene.GetRepresentAniPath(r.Rid); } catch (Exception e) { ap = "ex:" + e.Message; }
        status.Text = string.Format("rid={0}  {1}\r\nengine model: {2}\r\nengine ani: {3}\r\ndata model: {4}",
            r.Rid, r.Name,
            mp == null ? "(null)" : mp.Length == 0 ? "(empty)" : mp,
            ap == null ? "(null)" : ap.Length == 0 ? "(empty)" : ap,
            r.Model);
    }

    static void Reload()
    {
        AllRows.Clear();
        SpawnedList.Clear();
        string dataDir = Env("AS_DATA", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "asset_sandbox_data"));
        LoadRows(dataDir);
        FillList();
    }

    // ---------------------------------------------------------------- spawn

    static void SetupStage()
    {
        string stageEnv = Env("AS_STAGE", "");
        if (stageEnv.Length > 0)
        {
            string[] p = stageEnv.Split(',');
            float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out stageX);
            if (p.Length > 1) float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out stageY);
            if (p.Length > 2) float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out stageZ);
            if (sampler != null) stageY = sampler.Sample(stageX, stageZ); // flat pad at true terrain height
            PlaceCamera(0f, 1f);
            Log(string.Format("stage explicit ({0:F0},{1:F0},{2:F0}) flat={3}", stageX, stageY, stageZ, flatMode));
            return;
        }

        float dx = 0f, dz = 1f;
        try
        {
            scene.ResetCameraPosLookAtUp();
            Pump(300);
            float ax = 0f, ay = 0f, az = 0f;
            scene.GetCameraPos(ref ax, ref ay, ref az);
            scene.SetCamareMoveState(1, 1);
            Pump(600);
            scene.SetCamareMoveState(1, 0);
            float bx = 0f, by = 0f, bz = 0f;
            scene.GetCameraPos(ref bx, ref by, ref bz);
            dx = bx - ax; dz = bz - az;
            float dl = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dl > 1f) { dx /= dl; dz /= dl; }
            stageX = bx + dx * 900f;
            stageZ = bz + dz * 900f;
            if (sampler != null)
            {
                stageY = sampler.Sample(stageX, stageZ);
            }
            else
            {
                scene.SetCameraPos(stageX, 50000f, stageZ, false);
                Pump(150);
                float gx = 0f, gy = 0f, gz = 0f;
                scene.GetCameraPos(ref gx, ref gy, ref gz);
                stageY = gy;
            }
            PlaceCamera(dx, dz);
            Log(string.Format("stage ({0:F0},{1:F0},{2:F0}) dir=({3:F2},{4:F2})", stageX, stageY, stageZ, dx, dz));
        }
        catch (Exception e)
        {
            Log("stage ex: " + e.Message);
            stageX = 0f; stageY = 0f; stageZ = 0f;
        }
    }

    static void PlaceCamera(float dx, float dz)
    {
        float camX = stageX - dx * camDist;
        float camY = stageY + camUp;
        float camZ = stageZ - dz * camDist;
        if (sampler != null)
        {
            float g = sampler.Sample(camX, camZ) + 60f;
            if (camY < g) camY = g;
        }
        scene.SetCameraPos(camX, camY, camZ, false);
    }

    static void ProbeRepresentApi()
    {
        // The host's represent ID space is actors/NPCs (the game's doodad table
        // represent/doodad/doodad.txt is loaded by JX3DoodadRepresent in the game
        // logic, not by this host), so this only documents the mismatch.
        foreach (int rid in new int[] { 46396, 46457, 47182 })
        {
            try
            {
                string mp = scene.GetRepresentModelPath(rid);
                string ap = scene.GetRepresentAniPath(rid);
                Log(string.Format("host represent probe {0}: model='{1}' ani='{2}' (actor space)", rid, mp, ap));
            }
            catch (Exception e) { Log("represent probe " + rid + " ex: " + e.Message); }
        }
    }

    static void SpawnRows(List<DoodadRow> rows)
    {
        if (rows.Count == 0) { Log("spawn: nothing selected"); return; }
        ClearScene(); // single-preview: a new spawn replaces the previous one
        int n = rows.Count;
        int cols = (int)Math.Ceiling(Math.Sqrt(n));
        for (int i = 0; i < n; i++)
        {
            int gx = i % cols;
            int gz = i / cols;
            float x = stageX + (gx - (cols - 1) * 0.5f) * spacing;
            float z = stageZ + gz * spacing;
            float y = (flatMode || sampler == null) ? stageY : sampler.Sample(x, z);
            SpawnOne(rows[i], x, y, z);
        }
        FillList();
    }

    static void SpawnRow5()
    {
        List<DoodadRow> rows = SelectedRows();
        if (rows.Count == 0) { Log("spawn x5: nothing selected"); return; }
        DoodadRow r = rows[0];
        var five = new List<DoodadRow>();
        for (int i = 0; i < 5; i++) five.Add(r);
        SpawnRows(five);
    }

    static bool SpawnOne(DoodadRow row, float x, float y, float z)
    {
        var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
        var rot = new CLRfloat4(); rot.x = 0f; rot.y = 0f; rot.z = 0f; rot.w = 1f;
        string name = "as_" + row.Rid + "_" + (seq++);
        string how = null;

        if (useRepresent)
        {
            try
            {
                long h = scene.AddRepresentModel(row.Rid, pos, rot);
                if (h != 0 && h != -1)
                {
                    how = "represent handle=" + h;
                    SpawnedList.Add(new Spawned { IsRepresent = true, Handle = h, Rid = row.Rid, Name = name });
                }
                else
                {
                    Log("AddRepresentModel(" + row.Rid + ") -> " + h);
                }
            }
            catch (Exception e) { Log("AddRepresentModel(" + row.Rid + ") ex: " + e.Message); }
        }

        if (how == null && row.Model.Length > 0)
        {
            try
            {
                var scl = new CLRfloat3(); scl.x = row.Scale; scl.y = row.Scale; scl.z = row.Scale;
                long h = scene.AddDummyModel(name, Vfs(row.Model), pos, rot, scl);
                if (h > 0)
                {
                    how = "dummy handle=" + h + " scale=" + row.Scale;
                    SpawnedList.Add(new Spawned { IsRepresent = false, Handle = h, Rid = row.Rid, Name = name });
                    if (row.Ani.Length > 0)
                    {
                        try
                        {
                            var m = new KGModelCLR();
                            m.AttachModel(h);
                            Log("  ani " + Vfs(row.Ani) + " -> " + m.PlayAnimation(Vfs(row.Ani), 0, 1.0f, 0));
                        }
                        catch (Exception e) { Log("  ani ex: " + e.Message); }
                    }
                    if (row.Sfx.Length > 0)
                    {
                        try
                        {
                            string sname = name + "_sfx";
                            float gsc = row.EffectScale * glowScale;
                            var escl = new CLRfloat3();
                            escl.x = gsc; escl.y = gsc; escl.z = gsc;
                            long h2 = scene.AddDummyModel(sname, Vfs(row.Sfx), pos, rot, escl);
                            if (h2 > 0)
                            {
                                SpawnedList.Add(new Spawned { IsSfx = true, Handle = h2, Rid = row.Rid, Name = sname });
                                Log("  glow " + Vfs(row.Sfx) + " scale=" + gsc + " -> " + h2);
                            }
                            else Log("  glow -> " + h2 + " " + Vfs(row.Sfx));
                        }
                        catch (Exception e) { Log("  glow ex: " + e.Message); }
                    }
                }
                else Log("AddDummyModel -> " + h + " model=" + Vfs(row.Model));
            }
            catch (Exception e) { Log("AddDummyModel ex: " + e.Message); }
        }

        if (how == null)
        {
            Log(string.Format("FAIL rid={0} '{1}' model='{2}'", row.Rid, row.Name, row.Model));
            return false;
        }
        Log(string.Format("spawn rid={0} '{1}' at ({2:F0},{3:F0},{4:F0}) via {5}", row.Rid, row.Name, x, y, z, how));
        return true;
    }

    static string Vfs(string path)
    {
        return path.Replace('/', '\\').Trim();
    }

    static void RemoveLast()
    {
        if (SpawnedList.Count == 0) return;
        Spawned s = SpawnedList[SpawnedList.Count - 1];
        SpawnedList.RemoveAt(SpawnedList.Count - 1);
        try
        {
            if (s.IsRepresent) scene.RemoveRepresnetModel((int)s.Handle);
            else scene.RemoveDummyModel(s.Name);
            Log("removed rid=" + s.Rid + " " + (s.IsRepresent ? "represent" : "dummy"));
        }
        catch (Exception e) { Log("remove ex: " + e.Message); }
        FillList();
    }

    static void ClearScene()
    {
        try { scene.ClearRepresentModel(); } catch (Exception e) { Log("ClearRepresentModel ex: " + e.Message); }
        try { scene.ClearDummyModel(); } catch (Exception e) { Log("ClearDummyModel ex: " + e.Message); }
        SpawnedList.Clear();
        refAlive = false;
        Log("scene cleared");
        if (refEnabled) SpawnRef();
        FillList();
    }

    static void ToggleRef()
    {
        refEnabled = !refEnabled;
        if (!refEnabled)
        {
            try { scene.RemoveDummyModel("as_ref"); } catch { }
            refAlive = false;
            Log("ref off");
            FillList();
            return;
        }
        SpawnRef();
        FillList();
    }

    static void SpawnRef()
    {
        if (refAlive || scene == null) return;
        try
        {
            float x = stageX - spacing * 2f, z = stageZ + spacing * 0.5f;
            float y = (flatMode || sampler == null) ? stageY : sampler.Sample(x, z);
            var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
            var rot = new CLRfloat4(); rot.x = 0f; rot.y = 0f; rot.z = 0f; rot.w = 1f;
            var scl = new CLRfloat3(); scl.x = 1f; scl.y = 1f; scl.z = 1f;
            long h = scene.AddDummyModel("as_ref", actorPath, pos, rot, scl);
            if (h > 0) { refAlive = true; Log("ref actor (\u82B1\u841D, human ~1.7 m) -> " + h); }
            else Log("ref actor -> " + h + " path=" + actorPath);
        }
        catch (Exception e) { Log("ref ex: " + e.Message); }
    }

    // ---------------------------------------------------------------- misc

    static void Pump(int ms)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            engine.FrameMove();
            engine.Render();
            Application.DoEvents();
            Thread.Sleep(15);
        }
    }

    static void Shot(string name)
    {
        try
        {
            string png = Path.Combine(outDir, name + "_" + DateTime.Now.ToString("HHmmss") + ".png");
            scene.SetScreenShot(png, 2);
            scene.DoScreenShotImmediate();
            Log("shot " + Path.GetFileName(png) + " exists=" + File.Exists(png));
        }
        catch (Exception e) { Log("shot ex: " + e.Message); }
    }

    static string Env(string name, string def)
    {
        string v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    static void LogInit()
    {
        try { File.WriteAllText(Path.Combine(outDir, "asset_sandbox.log"), ""); } catch { }
    }

    static void Log(string s)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + s;
        try { File.AppendAllText(Path.Combine(outDir, "asset_sandbox.log"), line + "\r\n"); } catch { }
        if (logBox != null)
        {
            if (logBox.TextLength > 200000) logBox.Text = logBox.Text.Substring(logBox.TextLength - 100000);
            logBox.AppendText(line + "\r\n");
        }
        Console.WriteLine(s);
    }
}
