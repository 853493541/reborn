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
        public List<string> Events = new List<string>();
        public List<string> Wems = new List<string>();
        public List<string> Ids = new List<string>();
        public string Matched = "";
        public string MatchSource = "";
        public string Deduced = "";
        public string DeduceNote = "";
        public List<string> MatchedExtra = new List<string>();
        public bool Ip;
        public string IpNote = "";
        public bool NoAnim;
        public string Mech = "";
        public List<ProcStep> Process = new List<ProcStep>();
    }

    internal class ProcStep
    {
        public int T;
        public string Kind = "";
        public string V = "";
        public string N = "";
        public string K = "";
        public float X, Y, Z;
        public float S = 1f;
        public float SX, SY, SZ;
    }

    internal class ReviewItem
    {
        public string chosen = "";
        public string note = "";
        public string updated = "";
    }

    static readonly List<Ability> abilities = new List<Ability>();
    static readonly HashSet<string> matchedTanis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, ReviewItem> review = new Dictionary<string, ReviewItem>();
    static readonly List<string> catalogPaths = new List<string>();
    static readonly List<string> catalogFiltered = new List<string>();
    static readonly Queue<int[]> pending = new Queue<int[]>();

    static string editorRoot = @"C:\SeasunGame\MovieEditor";
    static string startupPath;
    static string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
    static string actorPath;
    static string dataPath, reviewPath, outDir, soundDir;
    static Action<string> Log;
    static readonly HashSet<string> soundMissLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    const uint SND_ASYNC = 0x0001;
    const uint SND_NODEFAULT = 0x0002;
    const uint SND_FILENAME = 0x00020000;

    static Form form;
    static SplitContainer mainSplit, rightSplit;
    static Panel viewport;
    static TreeView tree;
    static TextBox searchBox, addPathBox, catalogSearch, noteBox, restartBox, mechBox;
    static Panel processPanel;
    static Label processStatus;
    static Button processPlayBtn;
    static TabControl tabsCtl;
    static TabPage tabProcessPage;
    static ListView processList;
    static Ability processAbility;
    static List<ProcStep> runSteps = new List<ProcStep>();
    static int runIndex;
    static int runLastT;
    static long runStart;
    static bool runActive;
    static string autoProcessName = "";
    static int camOutFrames;
    static int smokeShotMs = -1;
    static CheckBox onlyUnresolved, autoPlay, restartChk, tracedChk, soundChk, ipChk;
    static ComboBox speedBox;
    static ListBox catalogList;
    static Label lblAbility, lblCurrent, lblSounds, statusLabel;

    static KGEngineCLR engine;
    static KGSceneCLR scene;
    static KGModelCLR model;
    static Timer frameTimer;

    static string curClip = "";
    static string lastPlayPath = "";
    static long lastPlayStart;
    static float speed = 1f;
    static bool userSplit;
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
        soundDir = Env("AP_SOUND_DIR", Path.Combine(baseDir, "ability_picker", "sound"));
        Directory.CreateDirectory(soundDir);
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
        try { Log("represent path 70025 -> [" + scene.GetRepresentModelPath(70025) + "]"); }
        catch (Exception e) { Log("represent probe ex: " + e.Message); }
        autoProcessName = Env("AP_PROCESS", "");
        int.TryParse(Env("AP_SHOT_MS", ""), out smokeShotMs);
        int withIds = 0, matched = 0, withSound = 0;
        foreach (Ability a in abilities)
        {
            if (a.Ids.Count > 0) withIds++;
            if (a.Matched != "") matched++;
            if (HasSound(a)) withSound++;
        }
        SetStatus("engine ready - " + abilities.Count + " abilities, " + withIds + " with ids, "
            + matched + " with identified tani, " + withSound + " with sound");

        frameTimer = new Timer();
        frameTimer.Interval = 15;
        frameTimer.Tick += delegate { Frame(); };
        frameTimer.Start();

        if (autoProcessName != "")
        {
            foreach (Ability a in abilities)
            {
                if (a.Name == autoProcessName)
                {
                    TreeNode node = null;
                    foreach (TreeNode g in tree.Nodes)
                        foreach (TreeNode an in g.Nodes)
                        {
                            Ability x = an.Tag as Ability;
                            if (x != null && x.Name == autoProcessName) { node = an; break; }
                        }
                    if (node != null) tree.SelectedNode = node;
                    StartProcess(a);
                    break;
                }
            }
        }

        if (smoke) smokeStart = Environment.TickCount;
        Application.Run(form);
    }

    // ---------- frame ----------
    static void Frame()
    {
        // review mode: restart the clip when the repeat interval elapses so the
        // move plays again from frame 0 (MovieEditor loops seamlessly instead)
        if (restartChk != null && restartChk.Checked && lastPlayPath != "")
        {
            int ms;
            if (int.TryParse(restartBox.Text.Trim(), out ms) && ms >= 300
                && Environment.TickCount - lastPlayStart >= ms)
            {
                lastPlayStart = Environment.TickCount;
                try
                {
                    model.PlayAnimation(lastPlayPath, 0, speed, 0);
                    PlayCue(FindAbility(selectedAbilityKey));
                    Log("restart -> " + Short(lastPlayPath));
                }
                catch (Exception e) { Log("restart ex: " + e.Message); }
            }
        }
        if (camOutFrames > 0)
        {
            try { scene.ExecAction(31, 1, 0, 1); } catch { }
            camOutFrames--;
        }
        if (runActive)
        {
            long el = Environment.TickCount - runStart;
            while (runIndex < runSteps.Count && runSteps[runIndex].T <= el)
            {
                RunStep(runSteps[runIndex]);
                runIndex++;
            }
            if (processPanel != null) processPanel.Invalidate();
            if (el > runLastT + 600) StopProcess();
        }
        try
        {
            while (pending.Count > 0)
            {
                int[] a = pending.Dequeue();
                if (a[0] == 31) { scene.ExecAction(31, 1, a[1], 1); continue; }
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
                    TreeNode leaf = FindLeaf(pick);
                    if (leaf != null)
                    {
                        Log("smoke pick (tree click path): " + pick.Name + " -> " + leaf.Text);
                        tree.SelectedNode = leaf;
                    }
                    else
                    {
                        selectedAbilityKey = pick.Key;
                        Log("smoke pick: " + pick.Name + " -> " + pick.Tanis[0]);
                        PlayTani(pick.Tanis[0], true);
                    }
                }
            }
            if (!smokeResetCam && el > 3500)
            {
                smokeResetCam = true;
                try { scene.ResetCameraPosLookAtUp(); } catch { }
            }
            int shotAt = smokeShotMs > 0 ? smokeShotMs : (autoProcessName != "" ? 3200 : 6000);
            if (!smokeDone && el > shotAt)
            {
                smokeDone = true;
                Shot("smoke");
                form.Close();
            }
        }
    }

    static TreeNode FindLeaf(Ability ab)
    {
        if (tree == null) return null;
        foreach (TreeNode g in tree.Nodes)
            foreach (TreeNode a in g.Nodes)
            {
                Ability x = a.Tag as Ability;
                if (x != null && x.Key == ab.Key && a.Nodes.Count > 0)
                    return a.Nodes[0];
            }
        return null;
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
            ab.Events = StrList(d, "events");
            ab.Wems = StrList(d, "wems");
            ab.Ids = StrList(d, "ids");
            ab.Matched = Str(d, "matched");
            ab.MatchSource = Str(d, "matchSource");
            ab.Deduced = Str(d, "deduced");
            ab.DeduceNote = Str(d, "deduceNote");
            ab.MatchedExtra = StrList(d, "matchedExtra");
            ab.Ip = Str(d, "ip").Equals("true", StringComparison.OrdinalIgnoreCase);
            ab.IpNote = Str(d, "ipNote");
            ab.NoAnim = Str(d, "noAnim").Equals("true", StringComparison.OrdinalIgnoreCase);
            ab.Mech = Str(d, "mech");
            object po;
            if (d.TryGetValue("process", out po))
            {
                object[] parr = po as object[];
                if (parr != null)
                {
                    foreach (object po2 in parr)
                    {
                        var pd = po2 as Dictionary<string, object>;
                        if (pd == null) continue;
                        var st = new ProcStep();
                        int t;
                        int.TryParse(Str(pd, "t"), out t);
                        st.T = t;
                        st.Kind = Str(pd, "kind");
                        st.V = Str(pd, "v");
                        st.N = Str(pd, "n");
                        st.K = Str(pd, "k");
                        float f;
                        if (float.TryParse(Str(pd, "x"), out f)) st.X = f;
                        if (float.TryParse(Str(pd, "y"), out f)) st.Y = f;
                        if (float.TryParse(Str(pd, "z"), out f)) st.Z = f;
                        if (float.TryParse(Str(pd, "s"), out f) && f > 0f) st.S = f;
                        if (float.TryParse(Str(pd, "sx"), out f) && f > 0f) st.SX = f;
                        if (float.TryParse(Str(pd, "sy"), out f) && f > 0f) st.SY = f;
                        if (float.TryParse(Str(pd, "sz"), out f) && f > 0f) st.SZ = f;
                        ab.Process.Add(st);
                    }
                }
            }
            abilities.Add(ab);
        }
        RebuildMatched();
    }

    // the identified tani per ability id (single file) -> shown green in the lists
    static void RebuildMatched()
    {
        matchedTanis.Clear();
        foreach (Ability a in abilities)
        {
            if (a.Matched != "") matchedTanis.Add(a.Matched);
            foreach (string m in a.MatchedExtra)
                if (m != "") matchedTanis.Add(m);
        }
        Log("identified matched tani files: " + matchedTanis.Count);
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

        mainSplit = new SplitContainer();
        var main = mainSplit;
        main.Dock = DockStyle.Fill;
        main.Orientation = Orientation.Vertical;
        main.SplitterDistance = 470;
        main.Panel1MinSize = 320;
        main.SplitterMoved += delegate { userSplit = true; };
        form.Controls.Add(main);

        // left: filters
        var top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 88;
        top.Controls.Add(MkLabel("Find", 6, 9));
        searchBox = new TextBox();
        searchBox.Location = new Point(52, 6);
        searchBox.Width = 180;
        searchBox.Text = Env("AP_FIND", "");
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
        top.Controls.Add(MkLabel("Restart (ms)", 6, 58));
        restartBox = new TextBox();
        restartBox.Location = new Point(84, 55);
        restartBox.Width = 60;
        restartBox.Text = Env("AP_RESTART_MS", "2000");
        top.Controls.Add(restartBox);
        restartChk = new CheckBox();
        restartChk.Text = "restart on repeat";
        restartChk.Location = new Point(152, 58);
        restartChk.AutoSize = true;
        restartChk.Checked = Env("AP_RESTART", "1") == "1";
        top.Controls.Add(restartChk);
        tracedChk = new CheckBox();
        tracedChk.Text = "traced tani";
        tracedChk.Location = new Point(282, 58);
        tracedChk.AutoSize = true;
        tracedChk.Checked = Env("AP_TRACED", "1") == "1";
        tracedChk.CheckedChanged += delegate { BuildTree(); FilterCatalog(); };
        top.Controls.Add(tracedChk);
        soundChk = new CheckBox();
        soundChk.Text = "sound";
        soundChk.Location = new Point(382, 30);
        soundChk.AutoSize = true;
        soundChk.Checked = Env("AP_SOUND", "0") == "1";
        top.Controls.Add(soundChk);
        ipChk = new CheckBox();
        ipChk.Text = "IP";
        ipChk.Location = new Point(452, 58);
        ipChk.AutoSize = true;
        ipChk.CheckedChanged += delegate { BuildTree(); };
        top.Controls.Add(ipChk);
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
                Ability sel = (Ability)e.Node.Tag;
                selectedAbilityKey = sel.Key;
                ShowAbility(sel);
                // clicking an ability with a process plays the whole ability
                // and brings the Process pane to the front
                if (sel.Process.Count > 0)
                {
                    if (tabsCtl != null && tabProcessPage != null) tabsCtl.SelectedTab = tabProcessPage;
                    StartProcess(sel);
                }
            }
            else if (e.Node.Tag is string)
            {
                selectedTani = (string)e.Node.Tag;
                // clicking a leaf must also bind its parent ability so the
                // sound lookup works without selecting the ability row first
                if (e.Node.Parent != null && e.Node.Parent.Tag is Ability)
                    selectedAbilityKey = ((Ability)e.Node.Parent.Tag).Key;
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
        rightSplit = new SplitContainer();
        var right = rightSplit;
        right.Dock = DockStyle.Fill;
        right.Orientation = Orientation.Horizontal;
        right.SplitterDistance = 700;
        right.Panel1MinSize = 300;
        right.SplitterMoved += delegate { userSplit = true; };
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
        catalogList.DrawMode = DrawMode.OwnerDrawFixed;
        catalogList.ItemHeight = 16;
        catalogList.DrawItem += delegate(object s, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < catalogFiltered.Count)
            {
                bool matched = matchedTanis.Contains(catalogFiltered[e.Index]);
                Color c = matched ? Color.FromArgb(0, 130, 0) : e.ForeColor;
                using (SolidBrush b = new SolidBrush(c))
                    e.Graphics.DrawString(catalogList.Items[e.Index].ToString(), e.Font, b, e.Bounds);
            }
            e.DrawFocusRectangle();
        };
        catalogList.DoubleClick += delegate
        {
            if (catalogList.SelectedIndex >= 0 && catalogList.SelectedIndex < catalogFiltered.Count)
                PlayTani(catalogFiltered[catalogList.SelectedIndex], true);
        };
        tabCatalog.Controls.Add(catalogList);
        catalogList.BringToFront();
        FilterCatalog();
        tabs.TabPages.Add(tabCatalog);

        var tabMech = new TabPage("Mechanism");
        mechBox = new TextBox();
        mechBox.Multiline = true;
        mechBox.ReadOnly = true;
        mechBox.ScrollBars = ScrollBars.Vertical;
        mechBox.Dock = DockStyle.Fill;
        mechBox.BackColor = Color.FromArgb(250, 250, 245);
        mechBox.Text = "(select an ability in the tree - its full mechanism appears here)";
        tabMech.Controls.Add(mechBox);
        tabs.TabPages.Add(tabMech);

        var tabProcess = new TabPage("Process");
        tabProcessPage = tabProcess;
        tabsCtl = tabs;
        processPanel = new Panel();
        processPanel.Dock = DockStyle.Fill;
        processPanel.BackColor = Color.White;
        processPanel.Paint += delegate(object s, PaintEventArgs e) { DrawProcess(e.Graphics); };
        processPanel.Resize += delegate { processPanel.Invalidate(); };
        tabProcess.Controls.Add(processPanel);
        processList = new ListView();
        processList.View = View.Details;
        processList.FullRowSelect = true;
        processList.GridLines = true;
        processList.Dock = DockStyle.Bottom;
        processList.Height = 210;
        processList.Columns.Add("t (ms)", 55);
        processList.Columns.Add("kind", 60);
        processList.Columns.Add("asset / value", 330);
        processList.Columns.Add("note", 330);
        processList.Columns.Add("status", 90);
        processList.DoubleClick += delegate
        {
            if (processAbility != null) StartProcess(processAbility);
        };
        tabProcess.Controls.Add(processList);
        var procBar = new Panel();
        procBar.Dock = DockStyle.Top;
        procBar.Height = 30;
        processPlayBtn = MkButton("play process", 4, 3, 100, delegate
        {
            if (runActive) StopProcess();
            else if (processAbility != null) StartProcess(processAbility);
        });
        procBar.Controls.Add(processPlayBtn);
        procBar.Controls.Add(MkButton("copy", 108, 3, 56, delegate { CopyProcess(); }));
        processStatus = MkLabel("select an ability", 172, 8);
        procBar.Controls.Add(processStatus);
        tabProcess.Controls.Add(procBar);
        procBar.BringToFront();
        tabs.TabPages.Add(tabProcess);

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

        form.Shown += delegate { ApplyViewportSplit(); };
        form.Resize += delegate { if (!userSplit) ApplyViewportSplit(); };

        BuildTree();
    }

    // viewport gets ~70% width / ~72% height by default so it is at least half
    // of the window; once the user drags a splitter we stop auto-laying out
    static void ApplyViewportSplit()
    {
        try
        {
            int w = form.ClientSize.Width;
            int h = form.ClientSize.Height;
            if (w > 0 && mainSplit != null)
                mainSplit.SplitterDistance = Math.Max(mainSplit.Panel1MinSize, (int)(w * 0.30));
            if (h > 0 && rightSplit != null && rightSplit.Height > 0)
                rightSplit.SplitterDistance = Math.Max(rightSplit.Panel1MinSize, (int)(rightSplit.Height * 0.72));
        }
        catch (Exception e) { Log("split ex: " + e.Message); }
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
        // featured: abilities with a staged process (click = play the full ability)
        var procList = new List<Ability>();
        foreach (Ability ab in abilities)
            if (ab.Process.Count > 0) procList.Add(ab);
        if (procList.Count > 0)
        {
            TreeNode pg = new TreeNode("★ 流程 Process (click to play)");
            pg.ForeColor = Color.FromArgb(0, 90, 160);
            foreach (Ability ab in procList)
            {
                TreeNode pn = new TreeNode(ab.Name + "  (" + ab.Process.Count + " steps)");
                pn.Tag = ab;
                pn.ForeColor = Color.FromArgb(0, 90, 160);
                pg.Nodes.Add(pn);
            }
            tree.Nodes.Add(pg);
        }
        string q = searchBox.Text.Trim();
        var groups = new Dictionary<string, TreeNode>();
        foreach (Ability ab in abilities)
        {
            string chosen = review.ContainsKey(ab.Key) ? review[ab.Key].chosen : "";
            if (onlyUnresolved.Checked && !string.IsNullOrEmpty(chosen)) continue;
            if (ipChk.Checked && !ab.Ip) continue;
            // traced filter: keep zhenchuan abilities (with ids); entries with no
            // ids and no traced match are legacy extras -> hidden
            if (tracedChk.Checked && ab.Ids.Count == 0 && ab.Matched == "") continue;
            if (q.Length > 0)
            {
                bool hit = ab.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hit)
                    foreach (string id in ab.Ids)
                        if (id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
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
            string idLabel = ab.Ids.Count > 0 ? string.Join("/", ab.Ids.ToArray()) : ab.Id;
            string label = ab.Name + (idLabel != "" ? ("  [" + idLabel + "]") : "") + "   (" + ab.Tanis.Count + ")";
            if (!string.IsNullOrEmpty(chosen))
                label += "  ->  " + Short(chosen);
            if (ab.Ip) label = "[IP] " + label;
            TreeNode node = new TreeNode(label);
            node.Tag = ab;
            if (ab.Ip) node.ForeColor = Color.FromArgb(140, 60, 170);
            if (ab.NoAnim)
            {
                TreeNode na = new TreeNode("(no animation)");
                na.Tag = "";
                na.ForeColor = Color.FromArgb(120, 120, 120);
                node.Nodes.Add(na);
            }
            else
            foreach (string t in ab.Tanis)
            {
                // traced filter: a resolved ability shows only its green files
                // (all phases); an unresolved one shows all candidates for review
                if (tracedChk.Checked && ab.Matched != "" && !IsMatch(ab, t)) continue;
                bool isMatch = IsMatch(ab, t);
                bool deduced = ab.Deduced != "" && string.Equals(t, ab.Deduced, StringComparison.OrdinalIgnoreCase);
                TreeNode leaf = new TreeNode(deduced ? "~ " + Short(t) : Short(t));
                leaf.Tag = t;
                // green = evidence match for this ability or a known matched file;
                // orange = suggested (deduced, unverified) for this ability
                if (isMatch) leaf.ForeColor = Color.FromArgb(0, 130, 0);
                else if (deduced) leaf.ForeColor = Color.FromArgb(200, 110, 0);
                else if (matchedTanis.Contains(t)) leaf.ForeColor = Color.FromArgb(0, 130, 0);
                if (string.Equals(chosen, t, StringComparison.OrdinalIgnoreCase))
                    leaf.Text = "* " + leaf.Text;
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

    // ---------- ability process (staged playback + timeline) ----------
    static string FindTani(Ability ab, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return "";
        if (ab != null)
            foreach (string t in ab.Tanis)
                if (t.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return t;
        foreach (string p in catalogPaths)
            if (p.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return @"data\source\player\f1\动作\" + p;
        return "";
    }

    static void StartProcess(Ability ab)
    {
        if (ab == null || ab.Process.Count == 0)
        {
            SetStatus("no process defined for " + (ab != null ? ab.Name : "-"));
            return;
        }
        StopProcess();
        processAbility = ab;
        runSteps = ab.Process;
        runIndex = 0;
        runLastT = 0;
        foreach (ProcStep s in runSteps) if (s.T > runLastT) runLastT = s.T;
        runStart = Environment.TickCount;
        runActive = true;
        selectedAbilityKey = ab.Key;
        if (processPlayBtn != null) processPlayBtn.Text = "stop";
        SetStatus("process: " + ab.Name + " - " + runSteps.Count + " steps");
        if (processPanel != null) processPanel.Invalidate();
    }

    static void StopProcess()
    {
        if (scene != null)
        {
            try { scene.RemoveDummyModel("proc_anchor"); } catch { }
        }
        runActive = false;
        // stop the restart-on-repeat loop that would otherwise keep replaying
        // the last process animation + sound forever
        lastPlayPath = "";
        if (processPlayBtn != null) processPlayBtn.Text = "play process";
        if (processPanel != null) processPanel.Invalidate();
    }

    static void RunStep(ProcStep s)
    {
        try
        {
            if (s.Kind == "anim")
            {
                string path = FindTani(processAbility, s.V);
                if (path != "")
                {
                    model.PlayAnimation(path, 0, speed, 0);
                    curClip = path;
                    lastPlayPath = path;
                    lastPlayStart = Environment.TickCount;
                }
                Log("proc anim -> " + s.V + " = " + path);
            }
            else if (s.Kind == "sound")
            {
                string wav = Path.Combine(soundDir, s.V + ".wav");
                if (soundChk != null && soundChk.Checked && File.Exists(wav))
                    PlaySound(wav, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
                Log("proc sound -> " + wav + (soundChk != null && soundChk.Checked ? "" : " (sound off)"));
            }
            else if (s.Kind == "chain" || s.Kind == "move" || s.Kind == "action")
            {
                // known step of the real ability, not staged yet (visual listed
                // on the timeline in red): chain PSS render / pull movement / DoAction
                Log("proc " + s.Kind + " (not staged) -> " + s.V + " - " + s.N);
            }
            else if (s.Kind == "dummy")
            {
                var pos = new CLRfloat3();
                pos.x = s.X; pos.y = s.Y; pos.z = s.Z;
                var quat = new CLRfloat4();
                quat.w = 1f;
                var sc = new CLRfloat3();
                sc.x = s.SX > 0f ? s.SX : s.S;
                sc.y = s.SY > 0f ? s.SY : s.S;
                sc.z = s.SZ > 0f ? s.SZ : s.S;
                string key = s.K != "" ? s.K : "proc_anchor";
                long dh = scene.AddDummyModel(key, s.V, pos, quat, sc);
                Log("proc dummy -> " + key + " handle=" + dh + " @ " + s.X + "," + s.Y + "," + s.Z + " " + s.V);
            }
            else if (s.Kind == "remove")
            {
                scene.RemoveDummyModel(s.V != "" ? s.V : "proc_anchor");
                Log("proc remove -> " + s.V);
            }
            else if (s.Kind == "camera")
            {
                if (s.V.StartsWith("out:"))
                {
                    int n;
                    if (!int.TryParse(s.V.Substring(4), out n)) n = 3;
                    scene.ResetCameraPosLookAtUp();
                    camOutFrames = n * 3;
                }
                else if (s.V == "reset")
                {
                    scene.ResetCameraPosLookAtUp();
                }
                Log("proc camera -> " + s.V);
            }
        }
        catch (Exception e) { Log("proc step ex (" + s.Kind + "): " + e.Message); }
    }

    static bool IsUnsolvedStep(ProcStep s)
    {
        return s.Kind == "chain" || s.Kind == "move" || s.Kind == "action";
    }

    static void CopyProcess()
    {
        var sb = new StringBuilder();
        foreach (Ability a in abilities)
        {
            if (a.Process.Count == 0) continue;
            sb.AppendLine("## " + a.Name
                + (a.Ids.Count > 0 ? (" [" + string.Join("/", a.Ids.ToArray()) + "]") : "")
                + "  process (" + a.Process.Count + " steps)");
            foreach (ProcStep s in a.Process)
                sb.AppendLine(string.Format("t={0,5}ms  {1,-8} {2,-42} {3}{4}",
                    s.T, s.Kind, s.V, s.N, IsUnsolvedStep(s) ? "  [UNSOLVED - not staged]" : ""));
            if (a.Mech != "") sb.AppendLine("mechanism: " + a.Mech);
            sb.AppendLine();
        }
        if (sb.Length == 0) { SetStatus("no process to copy"); return; }
        try
        {
            Clipboard.SetText(sb.ToString());
            SetStatus("process copied to clipboard (" + sb.Length + " chars)");
        }
        catch (Exception e) { Log("copy ex: " + e.Message); }
    }

    static void FillProcessList()
    {
        if (processList == null) return;
        processList.BeginUpdate();
        processList.Items.Clear();
        if (processAbility != null)
        {
            foreach (ProcStep s in processAbility.Process)
            {
                var it = new ListViewItem(s.T.ToString());
                it.SubItems.Add(s.Kind);
                it.SubItems.Add(s.V);
                it.SubItems.Add(s.N);
                it.SubItems.Add(IsUnsolvedStep(s) ? "UNSOLVED" : "staged");
                if (IsUnsolvedStep(s)) it.ForeColor = Color.FromArgb(200, 0, 0);
                else if (s.Kind == "dummy") it.ForeColor = Color.FromArgb(140, 60, 170);
                else if (s.Kind == "sound") it.ForeColor = Color.FromArgb(170, 100, 0);
                processList.Items.Add(it);
            }
        }
        processList.EndUpdate();
    }

    static void DrawProcess(Graphics g)
    {
        g.Clear(Color.White);
        using (Font f = new Font("Microsoft YaHei", 8.5f))
        {
            Ability ab = processAbility;
            if (ab == null || ab.Process.Count == 0)
            {
                g.DrawString("no process defined for this ability yet", f, Brushes.Gray, 12, 12);
                return;
            }
            int w = processPanel.ClientSize.Width;
            int baseY = 140;
            int total = runLastT;
            if (ab != processAbility) { }
            foreach (ProcStep s in ab.Process) if (s.T > total) total = s.T;
            if (total < 1000) total = 1000;
            total += 200;
            int x0 = 30, x1 = Math.Max(x0 + 50, w - 30);
            using (Pen axis = new Pen(Color.FromArgb(180, 180, 180)))
                g.DrawLine(axis, x0, baseY, x1, baseY);
            for (int t = 0; t <= total; t += 200)
            {
                int x = x0 + (int)((long)(x1 - x0) * t / total);
                g.DrawLine(Pens.LightGray, x, baseY - 4, x, baseY + 4);
                g.DrawString((t / 1000.0).ToString("0.0") + "s", f, Brushes.Gray, x - 8, baseY + 6);
            }
            int row = 0;
            foreach (ProcStep s in ab.Process)
            {
                int x = x0 + (int)((long)(x1 - x0) * s.T / total);
                Color c = s.Kind == "anim" ? Color.FromArgb(0, 130, 0) :
                          s.Kind == "sound" ? Color.FromArgb(200, 110, 0) :
                          s.Kind == "dummy" ? Color.FromArgb(140, 60, 170) :
                          s.Kind == "remove" ? Color.FromArgb(150, 150, 150) :
                          (s.Kind == "chain" || s.Kind == "move") ? Color.FromArgb(200, 0, 0) : Color.DimGray;
                int y = baseY - 30 - (row % 4) * 26;
                using (SolidBrush b = new SolidBrush(c))
                using (Pen p = new Pen(c))
                {
                    g.FillEllipse(b, x - 5, baseY - 5, 10, 10);
                    g.DrawLine(p, x, baseY - 5, x, y + 14);
                    string label = s.T + "ms [" + s.Kind + "] " + (s.N != "" ? s.N : Short(s.V));
                    g.DrawString(label, f, b, x + 6, y);
                }
                row++;
            }
            if (runActive)
            {
                long el = Environment.TickCount - runStart;
                int x = x0 + (int)Math.Min(x1 - x0, (long)(x1 - x0) * el / total);
                using (Pen ph = new Pen(Color.Red, 2))
                    g.DrawLine(ph, x, 20, x, baseY + 16);
            }
        }
    }

    // an ability can match several clips (multi-phase skills); all are green
    static bool IsMatch(Ability ab, string t)
    {
        if (ab.Matched != "" && string.Equals(t, ab.Matched, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (string m in ab.MatchedExtra)
            if (string.Equals(t, m, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
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
            if (tracedChk.Checked && !matchedTanis.Contains(p)) continue;
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
            lastPlayPath = path;
            lastPlayStart = Environment.TickCount;
            string snd = PlayCue(FindAbility(selectedAbilityKey));
            Log("play -> " + path + " (result " + pr + ")" + snd);
            ShowTani(path);
            SetStatus("playing: " + Short(path) + snd);
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

    // play the ability's confirmed wav (decoded from wem) if available
    static string PlayCue(Ability ab)
    {
        if (ab == null || soundChk == null || !soundChk.Checked) return "";
        foreach (string wem in ab.Wems)
        {
            string wav = Path.Combine(soundDir, wem + ".wav");
            if (File.Exists(wav))
            {
                try
                {
                    PlaySound(wav, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
                    Log("sound -> " + wav);
                    return " | sound " + wem;
                }
                catch (Exception e) { Log("sound ex: " + e.Message); return ""; }
            }
        }
        string missKey = ab.Key + ":" + ab.Wems.Count;
        if (ab.Ids.Count > 0 && soundMissLogged.Add(missKey))
            Log("sound: no decoded wav for " + ab.Name + " (wems " + ab.Wems.Count + "), run tools\\fetch_sounds.py");
        return ab.Wems.Count > 0 ? " | sound not fetched" : " | no sound data";
    }

    static bool HasSound(Ability ab)
    {
        foreach (string wem in ab.Wems)
            if (File.Exists(Path.Combine(soundDir, wem + ".wav"))) return true;
        return false;
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
        RebuildMatched();
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
        lblAbility.Text = "ability: " + ab.Name + "   ids=" + (ab.Ids.Count > 0 ? string.Join("/", ab.Ids.ToArray()) : "-")
            + "   status=" + ab.Status
            + "   matched=" + (ab.Matched != "" ? (Short(ab.Matched) + " (" + ab.MatchSource + ")") : "-")
            + (ab.MatchedExtra.Count > 0 ? ("   +phases=" + ab.MatchedExtra.Count) : "")
            + (ab.Deduced != "" ? ("   suggested=" + Short(ab.Deduced) + " (unverified: " + ab.DeduceNote + ")") : "")
            + (ab.NoAnim ? "   [no animation]" : "")
            + (ab.Ip ? ("   [IP] " + ab.IpNote) : "");
        noteBox.Text = review.ContainsKey(ab.Key) ? review[ab.Key].note : "";
        if (mechBox != null) mechBox.Text = ab.Mech != "" ? ab.Mech : "(no mechanism notes yet)";
        processAbility = ab;
        if (processStatus != null)
            processStatus.Text = ab.Process.Count > 0
                ? (ab.Process.Count + " steps - double-click a row or press play (red=unsolved)")
                : "no process defined";
        if (processPanel != null) processPanel.Invalidate();
        FillProcessList();
    }

    static void ShowTani(string path)
    {
        Ability ab = FindAbility(selectedAbilityKey);
        if (ab != null)
        {
            lblAbility.Text = "ability: " + ab.Name + "   key=" + ab.Key + "   candidates=" + ab.Tanis.Count;
            if (mechBox != null) mechBox.Text = ab.Mech != "" ? ab.Mech : "(no mechanism notes yet)";
            processAbility = ab;
        }
        lblCurrent.Text = "candidate: " + path;
        if (ab != null && ab.Wems.Count > 0)
            lblSounds.Text = "wems: " + string.Join(", ", ab.Wems.GetRange(0, Math.Min(5, ab.Wems.Count)).ToArray())
                + "   events: " + string.Join(", ", ab.Events.GetRange(0, Math.Min(3, ab.Events.Count)).ToArray());
        else
            lblSounds.Text = "wems: -";
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
