// AbilityPicker - JX3 animation picker on the MovieEditor engine host.
// Lists zhenchuan abilities with tani candidates; selecting a candidate plays it
// on the real actor in the engine viewport. Full Tani.rt catalog search included.
//
// Build: ability_picker\build.cmd      Run: bin64\ability_picker.exe (cwd = editor root)
//
// Env:
//   AP_DATA=<json>    ability_candidates.json (default: <exe>\ability_picker\ability_candidates.json)
//   AP_REVIEW=<json>  review file (default: <exe>\ability_picker\review.json)
//   AP_OUT=<dir>      logs + screenshots (default: <exe>\ability_picker\out)
//   AP_SMOKE=1        auto-pick first candidate, screenshot, exit
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using MovieEngineCLR;
using MovieEditor.ActorEditor;

internal static class AbilityPicker
{
    // ---------- model ----------
    internal class Ability
    {
        public string Key;
        public string Id;
        public string Prefix;
        public string Kind;
        public string Name;
        public string Status;
        public string Notes;
        public List<string> Tanis = new List<string>();
        public List<string> Sounds = new List<string>();
    }

    internal class ReviewItem
    {
        public string chosen = "";
        public string note = "";
        public string updated = "";
    }

    static readonly List<Ability> abilities = new List<Ability>();
    static readonly Dictionary<string, ReviewItem> review = new Dictionary<string, ReviewItem>();
    static readonly List<string> catalogPaths = new List<string>();
    static readonly List<string> catalogFiltered = new List<string>();
    static readonly Queue<int[]> pending = new Queue<int[]>();

    static string editorRoot = @"C:\SeasunGame\MovieEditor";
    static string startupPath;
    static string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
    static string actorPath;
    static string dataPath, reviewPath, outDir;
    static Action<string> Log;

    static Form form;
    static Panel viewport;
    static TreeView tree;
    static TextBox searchBox, addPathBox, catalogSearch, noteBox;
    static CheckBox onlyUnresolved, autoPlay;
    static ComboBox speedBox;
    static ListBox catalogList;
    static Label lblAbility, lblCurrent, lblSounds, statusLabel;

    static KGEngineCLR engine;
    static KGSceneCLR scene;
    static KGModelCLR model;
    static Timer frameTimer;

    static string curClip = "";
    static float speed = 1f;
    static string selectedAbilityKey = "";
    static string selectedTani = "";
    static bool smoke;
    static int smokeIndex = -1;
    static long smokeStart;
    static bool smokePlayed, smokeResetCam, smokeDone;
    static bool rebuilding;

    [STAThread]
    static void Main()
    {
        startupPath = Path.Combine(editorRoot, "bin64");
        actorPath = Path.Combine(editorRoot, "source", "花萝无动作.actor");
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        dataPath = Env("AP_DATA", Path.Combine(baseDir, "ability_picker", "ability_candidates.json"));
        reviewPath = Env("AP_REVIEW", Path.Combine(baseDir, "ability_picker", "review.json"));
        outDir = Env("AP_OUT", Path.Combine(baseDir, "ability_picker", "out"));
        Directory.CreateDirectory(outDir);
        Log = delegate(string s)
        {
            try { File.AppendAllText(Path.Combine(outDir, "ability_picker.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); }
            catch { }
            Console.WriteLine(s);
        };
        smoke = Env("AP_SMOKE", "0") == "1";
        int.TryParse(Env("AP_SMOKE_INDEX", "-1"), out smokeIndex);
        Log("start data=" + dataPath + (smoke ? (" smoke=1 index=" + smokeIndex) : ""));

        LoadCandidates();
        LoadReview();
        LoadCatalog();

        BuildForm();
        form.Show();
        Application.DoEvents();

        // ---------- engine host init (SpikeHost pattern) ----------
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
        catch (Exception e) { Log("Init3DEngine ex: " + e); SetStatus("FATAL: engine init exception"); return; }
        Log(string.Format("Init3DEngine={0} err={1}", ok, err));
        if (ok == 0) { SetStatus("FATAL: engine init failed"); return; }

        try { Log("editor.Init result=" + editor.Init(editorRoot, err, form.Handle.ToInt64())); }
        catch (Exception e) { Log("editor.Init ex: " + e.Message); }

        scene = engine.NewEmptyScene();
        if (scene == null) { SetStatus("FATAL: no scene"); return; }
        long winId = scene.AddOutputWindow("", viewport.Handle.ToInt64(), 2);
        Log("winId=" + winId);

        var actor = new KGMovieActorCLR();
        actor.Init();
        try { ActorEditorCommandHelper.LoadFromFile(actor, actorPath, 0); }
        catch (Exception e) { Log("LoadFromFile ex: " + e.Message); }
        long handle = actor.GetModelHandle();
        Log("actor handle=" + handle);
        scene.AppendModel(handle);
        try { scene.FocusOnModel(); } catch (Exception e) { Log("FocusOnModel ex: " + e.Message); }

        model = new KGModelCLR();
        model.AttachModel(handle);
        SetStatus("engine ready - select a candidate to play");

        frameTimer = new Timer();
        frameTimer.Interval = 15;
        frameTimer.Tick += delegate { Frame(); };
        frameTimer.Start();

        if (smoke) smokeStart = Environment.TickCount;
        Application.Run(form);
    }

    // ---------- frame ----------
    static void Frame()
    {
        try
        {
            while (pending.Count > 0)
            {
                int[] a = pending.Dequeue();
                if (a[0] == 100 || a[0] == 101 || a[0] == 102 || a[0] == 103)
                {
                    float x = 0f, y = 0f, z = 0f;
                    if (a[0] == 102) { scene.FocusOnModel(); continue; }
                    if (a[0] == 103) { scene.ResetCameraPosLookAtUp(); continue; }
                    scene.GetCameraPos(ref x, ref y, ref z);
                    scene.SetCameraPos(x, y + (a[0] == 100 ? 60f : -60f), z, false);
                }
                else
                {
                    int lp = ((a[3] & 0xFFFF) << 16) | (a[2] & 0xFFFF);
                    scene.ExecAction(a[0], a[1], 0, lp);
                }
            }
            engine.FrameMove();
            engine.Render();
        }
        catch (Exception e) { Log("frame ex: " + e.Message); }

        if (smoke)
        {
            long el = Environment.TickCount - smokeStart;
            if (!smokePlayed && el > 800)
            {
                smokePlayed = true;
                Ability pick = null;
                if (smokeIndex >= 0 && smokeIndex < abilities.Count) pick = abilities[smokeIndex];
                if (pick == null || pick.Tanis.Count == 0)
                {
                    foreach (Ability a in abilities) { if (a.Tanis.Count > 0) { pick = a; break; } }
                }
                if (pick != null && pick.Tanis.Count > 0)
                {
                    selectedAbilityKey = pick.Key;
                    Log("smoke pick: " + pick.Name + " -> " + pick.Tanis[0]);
                    PlayTani(pick.Tanis[0], true);
                }
            }
            if (!smokeResetCam && el > 3500)
            {
                smokeResetCam = true;
                try { scene.ResetCameraPosLookAtUp(); } catch { }
            }
            if (!smokeDone && el > 6000)
            {
                smokeDone = true;
                Shot("smoke");
                form.Close();
            }
        }
    }

    // ---------- data ----------
    static void LoadCandidates()
    {
        abilities.Clear();
        if (!File.Exists(dataPath)) { return; }
        var ser = new JavaScriptSerializer();
        var root = ser.DeserializeObject(File.ReadAllText(dataPath, Encoding.UTF8)) as Dictionary<string, object>;
        if (root == null || !root.ContainsKey("abilities")) return;
        foreach (object o in (object[])root["abilities"])
        {
            var d = o as Dictionary<string, object>;
            if (d == null) continue;
            var ab = new Ability();
            ab.Key = Str(d, "key");
            ab.Id = Str(d, "id");
            ab.Prefix = Str(d, "prefix");
            ab.Kind = Str(d, "kind");
            ab.Name = Str(d, "name");
            ab.Status = Str(d, "status");
            ab.Notes = Str(d, "notes");
            ab.Tanis = StrList(d, "tanis");
            ab.Sounds = StrList(d, "sounds");
            abilities.Add(ab);
        }
    }

    static void LoadReview()
    {
        review.Clear();
        if (!File.Exists(reviewPath)) return;
        try
        {
            var ser = new JavaScriptSerializer();
            var root = ser.DeserializeObject(File.ReadAllText(reviewPath, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("items")) return;
            var items = root["items"] as Dictionary<string, object>;
            if (items == null) return;
            foreach (KeyValuePair<string, object> kv in items)
            {
                var d = kv.Value as Dictionary<string, object>;
                if (d == null) continue;
                var ri = new ReviewItem();
                ri.chosen = Str(d, "chosen");
                ri.note = Str(d, "note");
                ri.updated = Str(d, "updated");
                review[kv.Key] = ri;
            }
        }
        catch (Exception e) { Log("LoadReview ex: " + e.Message); }
    }

    static void SaveReview()
    {
        try
        {
            var items = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ReviewItem> kv in review)
            {
                var d = new Dictionary<string, string>();
                d["chosen"] = kv.Value.chosen;
                d["note"] = kv.Value.note;
                d["updated"] = kv.Value.updated;
                items[kv.Key] = d;
            }
            var root = new Dictionary<string, object>();
            root["updatedAt"] = DateTime.Now.ToString("s");
            root["items"] = items;
            var ser = new JavaScriptSerializer();
            File.WriteAllText(reviewPath, ser.Serialize(root), Encoding.UTF8);
            SetStatus("review saved: " + reviewPath);
        }
        catch (Exception e) { Log("SaveReview ex: " + e.Message); }
    }

    static void LoadCatalog()
    {
        catalogPaths.Clear();
        string p = Path.Combine(editorRoot, "ResourcePack", "Tani.rt");
        if (!File.Exists(p)) { Log("catalog missing: " + p); return; }
        try
        {
            Encoding gbk = Encoding.GetEncoding(936);
            foreach (string line in File.ReadAllLines(p, gbk))
            {
                string[] c = line.Split('\t');
                if (c.Length >= 3
                    && c[1].IndexOf(".tani", StringComparison.OrdinalIgnoreCase) >= 0
                    && c[2].IndexOf("\\player\\f1\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    catalogPaths.Add(c[1]);
                }
            }
            Log("catalog f1 tanis: " + catalogPaths.Count);
        }
        catch (Exception e) { Log("LoadCatalog ex: " + e.Message); }
    }

    // ---------- ui ----------
    static void BuildForm()
    {
        form = new Form();
        form.Text = "Ability Animation Picker - click candidates to play (drag orbit | wheel zoom | Q/E height | F focus | R reset | F5 replay | F12 shot)";
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new Size(1680, 980);
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { Replay(); e.Handled = true; }
            if (e.KeyCode == Keys.F12) { Shot("manual"); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.S) { SaveReview(); e.Handled = true; }
        };

        var main = new SplitContainer();
        main.Dock = DockStyle.Fill;
        main.Orientation = Orientation.Vertical;
        main.SplitterDistance = 470;
        main.Panel1MinSize = 320;
        form.Controls.Add(main);

        // left: filters
        var top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 62;
        top.Controls.Add(MkLabel("Find", 6, 9));
        searchBox = new TextBox();
        searchBox.Location = new Point(52, 6);
        searchBox.Width = 180;
        searchBox.TextChanged += delegate { BuildTree(); };
        top.Controls.Add(searchBox);
        onlyUnresolved = new CheckBox();
        onlyUnresolved.Text = "only unresolved";
        onlyUnresolved.Location = new Point(240, 6);
        onlyUnresolved.AutoSize = true;
        onlyUnresolved.CheckedChanged += delegate { BuildTree(); };
        top.Controls.Add(onlyUnresolved);
        autoPlay = new CheckBox();
        autoPlay.Text = "auto-play on select";
        autoPlay.Location = new Point(240, 30);
        autoPlay.AutoSize = true;
        autoPlay.Checked = true;
        top.Controls.Add(autoPlay);
        top.Controls.Add(MkLabel("Speed", 6, 34));
        speedBox = new ComboBox();
        speedBox.Location = new Point(52, 30);
        speedBox.Width = 80;
        speedBox.DropDownStyle = ComboBoxStyle.DropDownList;
        speedBox.Items.AddRange(new object[] { "0.25", "0.5", "1.0", "1.5", "2.0" });
        speedBox.SelectedIndex = 2;
        speedBox.SelectedIndexChanged += delegate
        {
            float.TryParse((string)speedBox.SelectedItem, out speed);
            if (speed <= 0f) speed = 1f;
        };
        top.Controls.Add(speedBox);
        main.Panel1.Controls.Add(top);

        // left: tools
        var tools = new Panel();
        tools.Dock = DockStyle.Bottom;
        tools.Height = 96;
        addPathBox = new TextBox();
        addPathBox.Location = new Point(6, 6);
        addPathBox.Width = 450;
        addPathBox.Text = "data\\source\\player\\f1\\动作\\";
        tools.Controls.Add(addPathBox);
        tools.Controls.Add(MkButton("Play", 6, 32, 56, delegate { if (selectedTani != "") PlayTani(selectedTani, true); }));
        tools.Controls.Add(MkButton("Replay", 66, 32, 62, delegate { Replay(); }));
        tools.Controls.Add(MkButton("Add ->", 132, 32, 62, delegate { AddCandidate(addPathBox.Text.Trim()); }));
        tools.Controls.Add(MkButton("Chosen", 198, 32, 62, delegate { SetChosen(); }));
        tools.Controls.Add(MkButton("Clear", 264, 32, 56, delegate { ClearChosen(); }));
        tools.Controls.Add(MkButton("Save", 324, 32, 56, delegate { SaveReview(); }));
        tools.Controls.Add(MkButton("Shot", 384, 32, 56, delegate { Shot("manual"); }));
        tools.Controls.Add(MkButton("Reload", 6, 62, 62, delegate { LoadCandidates(); LoadReview(); BuildTree(); SetStatus("reloaded"); }));
        tools.Controls.Add(MkButton("Copy path", 72, 62, 74, delegate
        {
            if (selectedTani != "") { Clipboard.SetText(selectedTani); SetStatus("copied: " + selectedTani); }
        }));
        statusLabel = MkLabel("loading...", 152, 66);
        statusLabel.AutoSize = false;
        statusLabel.Width = 300;
        tools.Controls.Add(statusLabel);
        main.Panel1.Controls.Add(tools);

        // left: tree
        tree = new TreeView();
        tree.Dock = DockStyle.Fill;
        tree.HideSelection = false;
        tree.AfterSelect += delegate(object s, TreeViewEventArgs e)
        {
            if (rebuilding || e.Node == null) return;
            if (e.Node.Tag is Ability)
            {
                selectedAbilityKey = ((Ability)e.Node.Tag).Key;
                ShowAbility((Ability)e.Node.Tag);
            }
            else if (e.Node.Tag is string)
            {
                selectedTani = (string)e.Node.Tag;
                if (autoPlay.Checked) PlayTani(selectedTani, true);
                else ShowTani(selectedTani);
            }
        };
        tree.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && selectedTani != "") { PlayTani(selectedTani, true); e.Handled = true; }
        };
        main.Panel1.Controls.Add(tree);
        tree.BringToFront();

        // right split: viewport + tabs
        var right = new SplitContainer();
        right.Dock = DockStyle.Fill;
        right.Orientation = Orientation.Horizontal;
        right.SplitterDistance = 700;
        right.Panel1MinSize = 300;
        main.Panel2.Controls.Add(right);

        viewport = new Panel();
        viewport.Dock = DockStyle.Fill;
        viewport.BackColor = Color.Black;
        viewport.MouseDown += delegate(object s, MouseEventArgs e)
        {
            int act = e.Button == MouseButtons.Left ? 1 : e.Button == MouseButtons.Middle ? 3 : 0;
            if (act != 0)
            {
                pending.Enqueue(new int[] { 30, 1, e.X, e.Y });
                pending.Enqueue(new int[] { act, 1, e.X, e.Y });
            }
        };
        viewport.MouseMove += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) pending.Enqueue(new int[] { 1, 1, e.X, e.Y });
            else if (e.Button == MouseButtons.Middle) pending.Enqueue(new int[] { 3, 1, e.X, e.Y });
        };
        viewport.MouseUp += delegate(object s, MouseEventArgs e) { pending.Enqueue(new int[] { 30, 1, e.X, e.Y }); };
        viewport.MouseWheel += delegate(object s, MouseEventArgs e) { pending.Enqueue(new int[] { 31, e.Delta < 0 ? 0 : 1, 0, 0 }); };
        form.MouseWheel += delegate(object s, MouseEventArgs e) { pending.Enqueue(new int[] { 31, e.Delta < 0 ? 0 : 1, 0, 0 }); };
        right.Panel1.Controls.Add(viewport);

        var tabs = new TabControl();
        tabs.Dock = DockStyle.Fill;
        right.Panel2.Controls.Add(tabs);

        var tabCatalog = new TabPage("Catalog (Tani.rt f1)");
        catalogSearch = new TextBox();
        catalogSearch.Dock = DockStyle.Top;
        catalogSearch.TextChanged += delegate { FilterCatalog(); };
        tabCatalog.Controls.Add(catalogSearch);
        var catBottom = new Panel();
        catBottom.Dock = DockStyle.Bottom;
        catBottom.Height = 34;
        catBottom.Controls.Add(MkButton("Add to selected ability", 6, 4, 170, delegate
        {
            if (catalogList.SelectedIndex >= 0 && catalogList.SelectedIndex < catalogFiltered.Count)
                AddCandidate(catalogFiltered[catalogList.SelectedIndex]);
        }));
        catBottom.Controls.Add(MkButton("Play", 182, 4, 60, delegate { if (catalogList.SelectedIndex >= 0) PlayTani(catalogFiltered[catalogList.SelectedIndex], true); }));
        tabCatalog.Controls.Add(catBottom);
        catalogList = new ListBox();
        catalogList.Dock = DockStyle.Fill;
        catalogList.DoubleClick += delegate
        {
            if (catalogList.SelectedIndex >= 0 && catalogList.SelectedIndex < catalogFiltered.Count)
                PlayTani(catalogFiltered[catalogList.SelectedIndex], true);
        };
        tabCatalog.Controls.Add(catalogList);
        catalogList.BringToFront();
        FilterCatalog();
        tabs.TabPages.Add(tabCatalog);

        var tabDetails = new TabPage("Details");
        var info = new Panel();
        info.Dock = DockStyle.Top;
        info.Height = 92;
        lblAbility = MkLabel("ability: -", 8, 6);
        lblAbility.Width = 1100;
        info.Controls.Add(lblAbility);
        lblCurrent = MkLabel("candidate: -", 8, 30);
        lblCurrent.Width = 1100;
        info.Controls.Add(lblCurrent);
        lblSounds = MkLabel("sounds: -", 8, 54);
        lblSounds.Width = 1100;
        info.Controls.Add(lblSounds);
        tabDetails.Controls.Add(info);
        noteBox = new TextBox();
        noteBox.Multiline = true;
        noteBox.ScrollBars = ScrollBars.Vertical;
        noteBox.Dock = DockStyle.Fill;
        tabDetails.Controls.Add(noteBox);
        noteBox.BringToFront();
        tabs.TabPages.Add(tabDetails);

        BuildTree();
    }

    static Label MkLabel(string text, int x, int y)
    {
        var l = new Label();
        l.Text = text;
        l.Location = new Point(x, y);
        l.AutoSize = true;
        return l;
    }

    static Button MkButton(string text, int x, int y, int w, EventHandler onClick)
    {
        var b = new Button();
        b.Text = text;
        b.Location = new Point(x, y);
        b.Width = w;
        b.Height = 24;
        b.Click += onClick;
        return b;
    }

    static void SetStatus(string s)
    {
        if (statusLabel != null) statusLabel.Text = s;
        Log(s);
    }

    static void BuildTree()
    {
        if (tree == null) return;
        rebuilding = true;
        tree.BeginUpdate();
        tree.Nodes.Clear();
        string q = searchBox.Text.Trim();
        var groups = new Dictionary<string, TreeNode>();
        foreach (Ability ab in abilities)
        {
            string chosen = review.ContainsKey(ab.Key) ? review[ab.Key].chosen : "";
            if (onlyUnresolved.Checked && !string.IsNullOrEmpty(chosen)) continue;
            if (q.Length > 0)
            {
                bool hit = ab.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hit)
                    foreach (string t in ab.Tanis)
                        if (t.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
                if (!hit) continue;
            }
            string gname = ab.Prefix + "  (" + ab.Kind + ")";
            if (!groups.ContainsKey(gname))
            {
                TreeNode g = new TreeNode(gname);
                groups[gname] = g;
                tree.Nodes.Add(g);
            }
            string label = ab.Name + "   [" + ab.Tanis.Count + "]";
            if (!string.IsNullOrEmpty(chosen))
                label += "  ->  " + Short(chosen);
            TreeNode node = new TreeNode(label);
            node.Tag = ab;
            if (!string.IsNullOrEmpty(chosen)) node.ForeColor = Color.FromArgb(0, 130, 0);
            foreach (string t in ab.Tanis)
            {
                TreeNode leaf = new TreeNode(Short(t));
                leaf.Tag = t;
                if (string.Equals(chosen, t, StringComparison.OrdinalIgnoreCase))
                {
                    leaf.ForeColor = Color.FromArgb(0, 130, 0);
                    leaf.Text = "* " + leaf.Text;
                }
                if (string.Equals(selectedTani, t, StringComparison.OrdinalIgnoreCase))
                    leaf.BackColor = Color.FromArgb(210, 230, 255);
                node.Nodes.Add(leaf);
            }
            groups[gname].Nodes.Add(node);
        }
        tree.EndUpdate();
        tree.ExpandAll();
        rebuilding = false;
    }

    static void FilterCatalog()
    {
        if (catalogList == null) return;
        string q = catalogSearch.Text.Trim();
        catalogFiltered.Clear();
        catalogList.BeginUpdate();
        catalogList.Items.Clear();
        int n = 0;
        foreach (string p in catalogPaths)
        {
            if (q.Length > 0 && p.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
            catalogFiltered.Add(p);
            catalogList.Items.Add(Short(p));
            n++;
            if (n >= 800) break;
        }
        catalogList.EndUpdate();
    }

    // ---------- actions ----------
    static void PlayTani(string path, bool force)
    {
        if (path == null || path == "") return;
        if (!force && path == curClip) return;
        try
        {
            int pr = model.PlayAnimation(path, 0, speed, 0);
            curClip = path;
            Log("play -> " + path + " (result " + pr + ")");
            ShowTani(path);
            SetStatus("playing (result " + pr + "): " + Short(path));
        }
        catch (Exception e)
        {
            Log("PlayTani ex: " + e.Message);
            SetStatus("play failed: " + e.Message);
        }
    }

    static void Replay()
    {
        if (selectedTani == "") return;
        PlayTani(selectedTani, true);
    }

    static void AddCandidate(string path)
    {
        if (path == null || path.Trim() == "") return;
        path = path.Trim();
        Ability ab = FindAbility(selectedAbilityKey);
        if (ab == null)
        {
            SetStatus("select an ability node first (left tree), then add");
            return;
        }
        foreach (string t in ab.Tanis)
            if (string.Equals(t, path, StringComparison.OrdinalIgnoreCase)) { SetStatus("already in list"); return; }
        ab.Tanis.Add(path);
        selectedTani = path;
        BuildTree();
        SetStatus("added to " + ab.Name + ": " + Short(path));
        if (autoPlay.Checked) PlayTani(path, true);
    }

    static void SetChosen()
    {
        Ability ab = FindAbility(selectedAbilityKey);
        if (ab == null || selectedTani == "")
        {
            SetStatus("select an ability node + a candidate first");
            return;
        }
        ReviewItem ri;
        if (!review.ContainsKey(ab.Key)) { ri = new ReviewItem(); review[ab.Key] = ri; }
        else ri = review[ab.Key];
        ri.chosen = selectedTani;
        ri.note = noteBox.Text;
        ri.updated = DateTime.Now.ToString("s");
        SaveReview();
        BuildTree();
        SetStatus("chosen for " + ab.Name + ": " + Short(selectedTani));
    }

    static void ClearChosen()
    {
        Ability ab = FindAbility(selectedAbilityKey);
        if (ab == null) return;
        if (review.ContainsKey(ab.Key))
        {
            review[ab.Key].chosen = "";
            review[ab.Key].updated = DateTime.Now.ToString("s");
        }
        SaveReview();
        BuildTree();
        SetStatus("cleared chosen for " + ab.Name);
    }

    static void ShowAbility(Ability ab)
    {
        lblAbility.Text = "ability: " + ab.Name + "   key=" + ab.Key + "   id=" + ab.Id + "   status=" + ab.Status
            + (ab.Notes != "" ? ("   notes=" + ab.Notes) : "");
        noteBox.Text = review.ContainsKey(ab.Key) ? review[ab.Key].note : "";
    }

    static void ShowTani(string path)
    {
        Ability ab = FindAbility(selectedAbilityKey);
        if (ab != null)
            lblAbility.Text = "ability: " + ab.Name + "   key=" + ab.Key + "   candidates=" + ab.Tanis.Count;
        lblCurrent.Text = "candidate: " + path;
        if (ab != null && ab.Sounds.Count > 0)
            lblSounds.Text = "sounds: " + string.Join(", ", ab.Sounds.GetRange(0, Math.Min(6, ab.Sounds.Count)).ToArray());
        else
            lblSounds.Text = "sounds: -";
    }

    static Ability FindAbility(string key)
    {
        foreach (Ability a in abilities) if (a.Key == key) return a;
        return null;
    }

    static void Shot(string tag)
    {
        try
        {
            string png = Path.Combine(outDir, "shot_" + tag + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            scene.SetScreenShot(png, 2);
            scene.DoScreenShotImmediate();
            Log("shot -> " + png);
            SetStatus("screenshot: " + png);
        }
        catch (Exception e) { Log("Shot ex: " + e.Message); }
    }

    // ---------- helpers ----------
    static string Env(string k, string dflt)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return string.IsNullOrEmpty(v) ? dflt : v;
    }

    static string Short(string path)
    {
        if (path == null) return "";
        int i = path.LastIndexOf('\\');
        return i >= 0 ? path.Substring(i + 1) : path;
    }

    static string Str(Dictionary<string, object> d, string k)
    {
        object v;
        return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
    }

    static List<string> StrList(Dictionary<string, object> d, string k)
    {
        var outList = new List<string>();
        object v;
        if (!d.TryGetValue(k, out v) || v == null) return outList;
        object[] arr = v as object[];
        if (arr == null) return outList;
        foreach (object o in arr) if (o != null) outList.Add(o.ToString());
        return outList;
    }
}
