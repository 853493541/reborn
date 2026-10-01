// TargetDummySandbox — browse and display the real JX3 木桩 (target dummy) NPCs
// on the engine host (MovieEditor DLLs).
//
// Data (extract once, gitignored local game data):
//   python tools\netcode\mode\extract_target_dummies.py
//   -> assets\mode\dummy\dummy_index.tsv (+ raw sNpcTemplate zone tables)
//
// Build: target_dummy_sandbox\build_target_dummy_sandbox.cmd
// Run:   target_dummy_sandbox\run_target_dummy_sandbox.cmd  (sets TD_DATA, cwd = editor root)
//
// Display path: the shipped NPC template rows are keyed by RepresentID
// (ZhuChengMuZhuang zone: 江湖木桩 37023-37026, 试炼木桩 35901-35904 etc.).
// Spawn tries KGSceneCLR.AddRepresentModel(representID) first (the editor's own
// actor/NPC path); if that fails it falls back to AddDummyModel with
// GetRepresentModelPath(representID) + the GetRepresentAniPath(representID) idle
// animation. Both the engine model path and the handle are logged.
//
// PvP note: the 主城木桩 dummies are the game's damage-test targets (fight-stat
// sessions "对抗木桩@太原木桩X", buff 28487 = 对抗伤害测试中; the 木桩心法属性
// heart adds 化劲/御劲). See docs/pvp/TARGET_DUMMY_RESEARCH.md.
//
// Env:
//   TD_DATA=<dir>       data dir (default <exeDir>\target_dummy_sandbox_data)
//   TD_MAP=<jsonmap>    default 龙门寻宝
//   TD_SMOKE=id,id,..   spawn these RepresentIDs, then exit
//   TD_SHOTS=ms,..      screenshot times (ms; smoke default 2500,5000,8000)
//   TD_AUTORUN=ms       exit after N ms (0 = interactive)
//   TD_PATH=1           skip AddRepresentModel, use AddDummyModel(model path) directly
//   TD_SCALE=<f>        model scale for the AddDummyModel fallback (default 1)
//   TD_YAW=<deg>        spawn yaw (default 180)
//   TD_REF=1            spawn the 花萝 actor (~1.7 m) as a scale reference
//   TD_STAGE=x,y,z      explicit stage center (y optional -> terrain)
//   TD_FLAT=1           keep the explicit stage y for all spawns (flat pad)
//   TD_SPACING=<u>      stage grid spacing (default 250)
//   TD_CAM_DIST / TD_CAM_UP   initial camera distance / height (default 1100/350)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MovieEngineCLR;

internal static class TargetDummySandbox
{
    const int CMS_FORWARD = 1, CMS_BACK = 2, CMS_MOVE_LEFT = 64, CMS_MOVE_RIGHT = 128,
              CMS_UP = 256, CMS_DOWN = 2048, CMS_FAST = 4096;

    const string MEM_NS = "TargetDummySandbox.memory";

    sealed class DummyRow
    {
        public int Rid;
        public int NpcId;
        public string Name = "";
        public string Group = "";
        public int Level;
        public long MaxLife;
        public long MaxMana;
        public int Intensity;
        public int Defense;
        public int MagicDefense;
        public string Script = "";
        public string Zone = "";
        public override string ToString() { return Rid + "  " + Name; }
    }

    sealed class Spawned
    {
        public bool IsRepresent;
        public long Handle;
        public string Name = "";
        public int Rid;
    }

    static readonly List<DummyRow> AllRows = new List<DummyRow>();
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
    static CheckBox[] groupChecks;
    static string[] GROUP_NAMES = new string[0];
    static Label status;
    static Label hud;
    static TextBox logBox;
    static string outDir;
    static string exeName;
    static int seq;
    static float stageX, stageY, stageZ;
    static float spacing = 250f;
    static float modelScale = 1f;
    static float yawDeg = 180f;
    static bool refEnabled;
    static bool refAlive;
    static bool flatMode;
    static bool pathOnly;
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
        string mapPath = Env("TD_MAP",
            "data\\source\\maps\\\u9F99\u95E8\u5BFB\u5B9D\\\u9F99\u95E8\u5BFB\u5B9D.jsonmap");
        string dataDir = Env("TD_DATA", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "target_dummy_sandbox_data"));
        refEnabled = Env("TD_REF", "0") == "1";
        flatMode = Env("TD_FLAT", "0") == "1";
        pathOnly = Env("TD_PATH", "0") == "1";
        float.TryParse(Env("TD_SCALE", "1"), NumberStyles.Float, CultureInfo.InvariantCulture, out modelScale);
        if (modelScale <= 0f) modelScale = 1f;
        float.TryParse(Env("TD_YAW", "180"), NumberStyles.Float, CultureInfo.InvariantCulture, out yawDeg);
        float.TryParse(Env("TD_CAM_DIST", "1100"), NumberStyles.Float, CultureInfo.InvariantCulture, out camDist);
        float.TryParse(Env("TD_CAM_UP", "350"), NumberStyles.Float, CultureInfo.InvariantCulture, out camUp);
        float.TryParse(Env("TD_SPACING", "250"), NumberStyles.Float, CultureInfo.InvariantCulture, out spacing);
        actorPath = Path.Combine(editorRoot, "source", "\u82B1\u841D\u65E0\u52A8\u4F5C.actor");
        exeName = Path.GetFileName(Application.ExecutablePath);
        long autoRunMs = 0;
        long.TryParse(Env("TD_AUTORUN", "0"), out autoRunMs);

        outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "target_dummy_sandbox_out");
        Directory.CreateDirectory(outDir);
        LogInit();

        string[] smokeIds = null;
        string smoke = Env("TD_SMOKE", "");
        if (smoke.Length > 0) smokeIds = smoke.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (smokeIds != null && autoRunMs <= 0) autoRunMs = 9000;
        string shots = Env("TD_SHOTS", smokeIds != null ? "2500,5000,8000" : "");
        foreach (string s in shots.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            long t;
            if (long.TryParse(s, out t)) ShotTimes.Add(t);
        }

        LogFingerprint();
        Log("start map=" + mapPath);
        Log("data=" + dataDir + " smoke=" + (smoke.Length > 0 ? smoke : "-"));

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
        try { r2 = baselib.InitMemory(MEM_NS); } catch (Exception e) { Log("InitMemory ex: " + e.Message); }
        try { r3 = baselib.InitPak(false); } catch (Exception e) { Log("InitPak ex: " + e.Message); }
        Log(string.Format("InitPath={0} InitMemory={1} InitPak={2} ns={3}", r1, r2, r3, MEM_NS));

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
        ProbeRows();
        if (refEnabled) SpawnRef();

        if (smokeIds != null)
        {
            var rows = new List<DummyRow>();
            foreach (string s in smokeIds)
            {
                int rid;
                if (!int.TryParse(s.Trim(), out rid)) continue;
                DummyRow row = FindRow(rid);
                if (row == null)
                {
                    row = new DummyRow();
                    row.Rid = rid;
                    row.Name = "(not in index)";
                    row.Group = "smoke";
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
        string index = Path.Combine(dataDir, "dummy_index.tsv");
        if (!File.Exists(index))
        {
            Log("FATAL: no dummy_index.tsv in " + dataDir);
            Log("run: python tools\\netcode\\mode\\extract_target_dummies.py");
            return false;
        }
        try
        {
            var groups = new List<string>();
            foreach (string line in File.ReadAllLines(index, Encoding.UTF8))
            {
                string[] c = line.Split('\t');
                if (c.Length < 13 || c[0] == "RepresentID") continue;
                var row = new DummyRow();
                if (!int.TryParse(c[0], out row.Rid)) continue;
                int.TryParse(c[1], out row.NpcId);
                row.Name = c[2];
                row.Group = c[3];
                int.TryParse(c[4], out row.Level);
                long.TryParse(c[5], out row.MaxLife);
                long.TryParse(c[6], out row.MaxMana);
                int.TryParse(c[7], out row.Intensity);
                int.TryParse(c[8], out row.Defense);
                int.TryParse(c[9], out row.MagicDefense);
                row.Script = c[10];
                row.Zone = c[11];
                AllRows.Add(row);
                if (!groups.Contains(row.Group)) groups.Add(row.Group);
            }
            GROUP_NAMES = groups.ToArray();
            Log("rows=" + AllRows.Count + " groups=" + string.Join(",", GROUP_NAMES) + " from " + index);
            return AllRows.Count > 0;
        }
        catch (Exception e) { Log("index read ex: " + e.Message); }
        return false;
    }

    static DummyRow FindRow(int rid)
    {
        foreach (DummyRow r in AllRows) if (r.Rid == rid) return r;
        return null;
    }

    // -------------------------------------------------------------------- ui

    static void BuildUi()
    {
        form = new Form();
        form.Text = "sandbox-target-dummy - \u6C5F\u6E56\u6728\u6869 PvP dummies";
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
        main.SplitterDistance = 560;
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
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 74f));
        main.Panel1.Controls.Add(left);

        txtSearch = new TextBox();
        txtSearch.Dock = DockStyle.Fill;
        txtSearch.ForeColor = Color.Gray;
        txtSearch.Text = "\u641C\u7D22\u540D\u79F0 / RepresentID / NPCID";
        txtSearch.GotFocus += delegate { if (txtSearch.ForeColor == Color.Gray) { txtSearch.Text = ""; txtSearch.ForeColor = Color.Black; } };
        txtSearch.LostFocus += delegate { if (txtSearch.Text.Length == 0) { txtSearch.ForeColor = Color.Gray; txtSearch.Text = "\u641C\u7D22\u540D\u79F0 / RepresentID / NPCID"; } };
        txtSearch.TextChanged += delegate { FillList(); };
        left.Controls.Add(txtSearch, 0, 0);

        var groups = new FlowLayoutPanel();
        groups.Dock = DockStyle.Fill;
        groups.WrapContents = false;
        groupChecks = new CheckBox[GROUP_NAMES.Length];
        for (int i = 0; i < GROUP_NAMES.Length; i++)
        {
            var cb = new CheckBox();
            cb.Text = GROUP_NAMES[i];
            cb.AutoSize = true;
            cb.Margin = new Padding(3, 6, 10, 0);
            cb.Checked = false; // none checked = all
            cb.CheckedChanged += delegate { FillList(); };
            groupChecks[i] = cb;
            groups.Controls.Add(cb);
        }
        left.Controls.Add(groups, 0, 1);

        var buttons = new FlowLayoutPanel();
        buttons.Dock = DockStyle.Fill;
        buttons.WrapContents = true;
        AddButton(buttons, "Spawn", delegate { SpawnRows(SelectedRows()); });
        AddButton(buttons, "Spawn x5", delegate { SpawnRow5(); });
        AddButton(buttons, "Remove last", delegate { RemoveLast(); });
        AddButton(buttons, "Clear", delegate { ClearScene(); });
        AddButton(buttons, "Rot 45", delegate { yawDeg += 45f; Log("yaw=" + yawDeg); });
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
        list.Columns.Add("\u7EC4", 70);
        list.Columns.Add("\u540D\u79F0", 130);
        list.Columns.Add("RepresentID", 84);
        list.Columns.Add("NPCID", 70);
        list.Columns.Add("Lv", 34);
        list.Columns.Add("MaxLife", 86);
        list.Columns.Add("\u9632\u5FA1", 60);
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
                main.SplitterDistance = 560;
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
        if (groupChecks != null)
            foreach (CheckBox cb in groupChecks)
                if (cb.Checked) picked.Add(cb.Text);
        bool allGroups = picked.Count == 0; // none checked = all
        list.BeginUpdate();
        list.Items.Clear();
        foreach (DummyRow r in AllRows)
        {
            if (!allGroups && picked.IndexOf(r.Group) < 0) continue;
            if (q.Length > 0
                && r.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                && r.Rid.ToString().IndexOf(q, StringComparison.Ordinal) < 0
                && r.NpcId.ToString().IndexOf(q, StringComparison.Ordinal) < 0
                && r.Group.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var it = new ListViewItem(r.Group);
            it.SubItems.Add(r.Name);
            it.SubItems.Add(r.Rid.ToString());
            it.SubItems.Add(r.NpcId.ToString());
            it.SubItems.Add(r.Level > 0 ? r.Level.ToString() : "");
            it.SubItems.Add(r.MaxLife.ToString());
            it.SubItems.Add(r.Defense.ToString());
            it.Tag = r;
            list.Items.Add(it);
        }
        list.EndUpdate();
        hud.Text = string.Format("rows {0}/{1} | spawned {2} | ns={3} | represent-first{4}",
            list.Items.Count, AllRows.Count, SpawnedList.Count, MEM_NS, pathOnly ? " OFF" : "");
    }

    static List<DummyRow> SelectedRows()
    {
        var rows = new List<DummyRow>();
        foreach (ListViewItem it in list.SelectedItems)
            if (it.Tag is DummyRow) rows.Add((DummyRow)it.Tag);
        return rows;
    }

    static void UpdateStatus()
    {
        List<DummyRow> rows = SelectedRows();
        if (rows.Count == 0) { status.Text = ""; return; }
        DummyRow r = rows[0];
        string mp = "", ap = "";
        try { mp = scene.GetRepresentModelPath(r.Rid); } catch (Exception e) { mp = "ex:" + e.Message; }
        try { ap = scene.GetRepresentAniPath(r.Rid); } catch (Exception e) { ap = "ex:" + e.Message; }
        status.Text = string.Format(
            "rid={0} npc={1} {2} [{3}]  Lv{4}  HP {5}  def {6}/{7}\r\nscript: {8}\r\nengine model: {9}\r\nengine ani: {10}",
            r.Rid, r.NpcId, r.Name, r.Group, r.Level, r.MaxLife, r.Defense, r.MagicDefense,
            r.Script.Length > 0 ? r.Script : "-",
            mp == null ? "(null)" : mp.Length == 0 ? "(empty)" : mp,
            ap == null ? "(null)" : ap.Length == 0 ? "(empty)" : ap);
    }

    static void Reload()
    {
        AllRows.Clear();
        SpawnedList.Clear();
        string dataDir = Env("TD_DATA", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "target_dummy_sandbox_data"));
        LoadRows(dataDir);
        FillList();
    }

    // ---------------------------------------------------------------- spawn

    static void SetupStage()
    {
        string stageEnv = Env("TD_STAGE", "");
        if (stageEnv.Length > 0)
        {
            string[] p = stageEnv.Split(',');
            float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out stageX);
            if (p.Length > 1) float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out stageY);
            if (p.Length > 2) float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out stageZ);
            if (sampler != null) stageY = sampler.Sample(stageX, stageZ);
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

    static void ProbeRows()
    {
        // Resolve every indexed RepresentID once at startup: proves which IDs the
        // editor host knows and records the engine model path in the log.
        foreach (DummyRow r in AllRows)
        {
            string mp = "";
            try { mp = scene.GetRepresentModelPath(r.Rid); } catch (Exception e) { mp = "ex:" + e.Message; }
            Log(string.Format("probe rid={0} '{1}' model='{2}'", r.Rid, r.Name, mp == null ? "(null)" : mp));
        }
    }

    static void SpawnRows(List<DummyRow> rows)
    {
        if (rows.Count == 0) { Log("spawn: nothing selected"); return; }
        ClearScene();
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
    }

    static void SpawnRow5()
    {
        List<DummyRow> rows = SelectedRows();
        if (rows.Count == 0) { Log("spawn x5: nothing selected"); return; }
        DummyRow r = rows[0];
        var five = new List<DummyRow>();
        for (int i = 0; i < 5; i++) five.Add(r);
        SpawnRows(five);
    }

    static CLRfloat4 YawQuat(float deg)
    {
        double a = deg * Math.PI / 180.0;
        var q = new CLRfloat4();
        q.x = 0f;
        q.y = (float)Math.Sin(a / 2.0);
        q.z = 0f;
        q.w = (float)Math.Cos(a / 2.0);
        return q;
    }

    static bool SpawnOne(DummyRow row, float x, float y, float z)
    {
        var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
        CLRfloat4 rot = YawQuat(yawDeg);
        string name = "td_" + row.Rid + "_" + (seq++);
        string how = null;
        string mp = "";
        string ap = "";
        try { mp = scene.GetRepresentModelPath(row.Rid); } catch { }
        try { ap = scene.GetRepresentAniPath(row.Rid); } catch { }

        if (!pathOnly)
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

        if (how == null && mp != null && mp.Length > 0)
        {
            try
            {
                var scl = new CLRfloat3(); scl.x = modelScale; scl.y = modelScale; scl.z = modelScale;
                long h = scene.AddDummyModel(name, Vfs(mp), pos, rot, scl);
                if (h > 0)
                {
                    how = "dummy handle=" + h + " scale=" + modelScale;
                    SpawnedList.Add(new Spawned { IsRepresent = false, Handle = h, Rid = row.Rid, Name = name });
                    if (ap != null && ap.Length > 0)
                    {
                        try
                        {
                            var m = new KGModelCLR();
                            m.AttachModel(h);
                            Log("  ani " + Vfs(ap) + " -> " + m.PlayAnimation(Vfs(ap), 0, 1.0f, 0));
                        }
                        catch (Exception e) { Log("  ani ex: " + e.Message); }
                    }
                }
                else Log("AddDummyModel -> " + h + " model=" + Vfs(mp));
            }
            catch (Exception e) { Log("AddDummyModel ex: " + e.Message); }
        }

        if (how == null)
        {
            Log(string.Format("FAIL rid={0} '{1}' model='{2}'", row.Rid, row.Name, mp));
            return false;
        }
        Log(string.Format("spawn rid={0} '{1}' [{2}] npc={3} Lv{4} HP={5} def={6} at ({7:F0},{8:F0},{9:F0}) via {10} model='{11}' ani='{12}'",
            row.Rid, row.Name, row.Group, row.NpcId, row.Level, row.MaxLife, row.Defense, x, y, z, how, mp, ap));
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
            try { scene.RemoveDummyModel("td_ref"); } catch { }
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
            long h = scene.AddDummyModel("td_ref", actorPath, pos, rot, scl);
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
        try { File.WriteAllText(Path.Combine(outDir, "target_dummy_sandbox.log"), ""); } catch { }
    }

    static void LogFingerprint()
    {
        string mtime = "?";
        try { mtime = File.GetLastWriteTime(Application.ExecutablePath).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
        Log(string.Format("build={0} {1} ns={2} exeDir={3}", exeName, mtime, MEM_NS,
            AppDomain.CurrentDomain.BaseDirectory));
    }

    static void Log(string s)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + s;
        try { File.AppendAllText(Path.Combine(outDir, "target_dummy_sandbox.log"), line + "\r\n"); } catch { }
        if (logBox != null)
        {
            if (logBox.TextLength > 200000) logBox.Text = logBox.Text.Substring(logBox.TextLength - 100000);
            logBox.AppendText(line + "\r\n");
        }
        Console.WriteLine(s);
    }
}
