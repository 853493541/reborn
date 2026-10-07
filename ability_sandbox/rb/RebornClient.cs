// RebornClient 鈥?M1.1 scaffold: real map + animated player (dummy + KGModelCLR),
// walk/run/jump/fall, follow camera, one skill key, HUD stub.
// Build: client\build_client.cmd    Run: bin64\reborn_client.exe (cwd = editor root)
//
// Env:
//   RC_MAP=<vfs jsonmap>          default 榫欓棬瀵诲疂
//   RC_SPAWN=x,y,z                optional spawn (y optional -> terrain)
//   RC_AUTORUN=ms                 exit after N ms (0 = until window closed)
//   RC_SHOTS=2000,5000,...        screenshot times (ms)
//   RC_CLIP_IDLE/WALK/RUN/JUMP/FALL/SKILL=<vfs .ani/.tani path>
//   RC_SKILL_MS=8000              skill clip duration before returning to state clip
//   RC_YAW_OFFSET=0               model facing calibration (radians)
//   RC_SCALE=1                    player model scale
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using MovieEngineCLR;
using MovieEditor.ActorEditor;

internal static class RebornClient
{
    static string outDir;
    static Action<string> Log;

    static string StrOf(Dictionary<string, object> d, string k)
    {
        object v;
        return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
    }

    internal class ProcStep
    {
        public int T;
        public int Dur;   // authored duration in ms (anim length / effect life)
        public string Kind = "";
        public string V = "";
        public string N = "";
        public string K = "";
        public float X, Y, Z;
        public float S = 1f;
    }

    internal class SfxRetryItem
    {
        public string path = "";
        public string name = "";
        public long due;
        public int tries;
    }

    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    const uint SND_ASYNC = 0x0001;
    const uint SND_NODEFAULT = 0x0002;
    const uint SND_FILENAME = 0x00020000;

    // sfx_shim.dll probe (engine SFX factories); see native/sfx_shim.cpp
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibrary(string path);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    delegate int SfxProbeFn();

    delegate int SfxPlayFn([MarshalAs(UnmanagedType.LPStr)] string path, float x, float y, float z);

    delegate IntPtr SfxStatusFn();

    [STAThread]
    private static void Main(string[] args)
    {
        // diagnostic: unhandled managed exceptions land in %TEMP%\skill_unhandled.txt
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ue)
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "skill_unhandled.txt"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + ue.ExceptionObject + "\r\n");
            }
            catch { }
        };
        // SUPERVISOR (default on; SB_NO_SUPERVISOR=1 disables): if the engine
        // init race with another client kills the app early, relaunch it. Our
        // app is never "affected" by other clients - it self-heals. The
        // supervisor itself never loads the engine (zero interference).
        if (Env("SB_NO_SUPERVISOR", "0") != "1" && !(args != null && System.Array.Exists(args, delegate(string a) { return a == "--child"; })))
        {
            try
            {
                string self = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string supLog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(self), "Skill", "out", "supervisor.log");
                try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(supLog)); } catch { }
                for (int attempt = 1; attempt <= 8; attempt++)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(self, "--child");
                    psi.UseShellExecute = false;
                    psi.WorkingDirectory = @"C:\SeasunGame\MovieEditor";
                    var child = System.Diagnostics.Process.Start(psi);
                    var swc = System.Diagnostics.Stopwatch.StartNew();
                    child.WaitForExit();
                    swc.Stop();
                    try { System.IO.File.AppendAllText(supLog, System.DateTime.Now.ToString("HH:mm:ss.fff") + " child pid=" + child.Id + " ran " + swc.ElapsedMilliseconds + "ms attempt " + attempt + "\r\n"); } catch { }
                    if (swc.ElapsedMilliseconds > 60000) return;
                    System.Threading.Thread.Sleep(3000);
                }
            }
            catch { }
            return;
        }
        string editorRoot = Env("RC_EDITOR_ROOT", @"C:\SeasunGame\MovieEditor");
        // RC_BIN64: engine DLL directory override (probe hosts can point the
        // engine at another engine build without touching the canonical install)
        string startupPath = Env("RC_BIN64", Path.Combine(editorRoot, "bin64"));
        string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
        string mapPath = Env("RC_MAP",
            "data\\source\\maps\\\u9F99\u95E8\u5BFB\u5B9D\\\u9F99\u95E8\u5BFB\u5B9D.jsonmap");
        string actorPath = Path.Combine(editorRoot, "source", "\u82B1\u841D\u65E0\u52A8\u4F5C.actor");
        string flws =
            "data\\source\\player\\f1\\\u52A8\u4F5C\\f1s07cj\u91CD\u5251\u6280\u80FD15_\u98CE\u6765\u5434\u5C71\u7EA2\u8272hd.tani";
        string f1 = "data\\source\\player\\f1\\\u52A8\u4F5C\\";
        string clipIdle = Env("RC_CLIP_IDLE", f1 + "f1b01ty\u666E\u901A\u5F85\u673A01.ani");
        string clipWalk = Env("RC_CLIP_WALK", f1 + "f1b02yd\u884C\u8D70.ani");
        string clipRun = Env("RC_CLIP_RUN", f1 + "f1b02yd\u5954\u8DD1.ani");
        string clipJump = Env("RC_CLIP_JUMP", f1 + "f1b02yd\u5C0F\u8DF3b.ani");
        string clipFall = Env("RC_CLIP_FALL", f1 + "f1b02yd\u5C0F\u8DF3c.ani");
        string clipSkill = Env("RC_CLIP_SKILL", flws);
        // RC_ROT_TEST close-ups show the actor faces -Z at identity, so the yaw
        // that points it along the movement direction needs a pi offset.
        // (note: TryParse sets the out param to 0 on failure, so parse into a temp)
        float yawOffset = (float)Math.PI;
        {
            float yo;
            if (float.TryParse(Env("RC_YAW_OFFSET", ""), out yo)) yawOffset = yo;
        }
        float scale = 1f;
        float.TryParse(Env("RC_SCALE", "1"), out scale);
        long skillMs = 8000;
        long.TryParse(Env("RC_SKILL_MS", "8000"), out skillMs);
        long autoRunMs = 0;
        long.TryParse(Env("RC_AUTORUN", "0"), out autoRunMs);

        // ---- ability selection (P panel): dataset-driven, default 临时飞爪 ----
        string abilitySel = Env("SB_ABILITY", "feizhua");
        if (abilitySel == "feizhua") abilitySel = "临时飞爪";
        else if (abilitySel == "ruyifa") abilitySel = "如意法";
        else if (abilitySel == "flws") abilitySel = "风来吴山";
        string dataPath = Env("SB_DATA",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ability_picker", "ability_candidates.json"));
        string soundDir = Env("SB_SOUND_DIR",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ability_picker", "sound"));
        string sfxDir = Env("SB_SFX_DIR",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ability_picker", "sfx"));
        bool soundOn = Env("SB_SOUND", "1") == "1";   // default on; P panel checkbox toggles
        long autoSkillMs = 0;
        long.TryParse(Env("SB_CAST_MS", "0"), out autoSkillMs);
        bool autoSkillDone = false;
        bool clickCastRequested = false;   // P panel click -> cast in the frame loop

        var feiSteps = new List<ProcStep>();
        var feiTanis = new List<string>();
        string feiMatched = "";
        // generic dataset-driven cast state (any ability with a staged process)
        var castSteps = new List<ProcStep>();
        var castTanis = new List<string>();
        string castMatched = "";
        string castName = "";
        bool castActive = false;
        bool sfxBatchDone = false;
        bool sfxWarmDone = false;
        long castCycleNext = 0;
        int castCycleIdx = 0;
        List<SfxRetryItem> sfxRetry = new List<SfxRetryItem>();
        long castStart = 0, castUntil = 0;
        int castIdx = 0;
        bool castPss = false;
        bool castPssEngine = false;   // effect created by the engine (RC_SFX_ENGINE)
        string castPssPath = "";
        long castPssHandle = 0;
        float lastCastX = 1e9f, lastCastZ = 1e9f;
        // 3 s cooldown between ability casts: one press = one cast, one
        // animation, one effect. Spam presses are ignored (no restart).
        const long castCooldownMs = 3000;
        long castCooldownUntil = 0;
        var datasetAbilityNames = new List<string>();   // panel: abilities with a process
        var taniAbilityNames = new List<string>();      // abilities whose anim plays a tani
        var sfxTagNames = new HashSet<string>();        // panel: process has kind "sfx"
        System.Drawing.Point lastMousePt = new System.Drawing.Point(0, 0);
        bool feiAiming = false, feiConfirm = false, feiCancel = false;
        bool autoSkillConfirmDone = false;
        // dataset entry loader shared by every ability (anim/sound/dummy steps)
        Action<string, List<ProcStep>, List<string>, string[]> loadAbility =
            delegate(string abName, List<ProcStep> abSteps, List<string> abTanis, string[] abMatched)
        {
            try
            {
                if (!File.Exists(dataPath)) { Log("SB dataset missing: " + dataPath); return; }
                var ser = new JavaScriptSerializer();
                var root = ser.DeserializeObject(File.ReadAllText(dataPath, System.Text.Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("abilities")) return;
                foreach (object o in (object[])root["abilities"])
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    object nv;
                    if (!d.TryGetValue("name", out nv) || nv == null || nv.ToString() != abName) continue;
                    // the dataset has duplicate rows per name (resolved + empty);
                    // skip the unresolved duplicates
                    object mv0;
                    bool hasMatched = d.TryGetValue("matched", out mv0) && mv0 != null && mv0.ToString() != "";
                    object pv0;
                    bool hasProc = d.TryGetValue("process", out pv0) && pv0 is object[] && ((object[])pv0).Length > 0;
                    if (!hasMatched && !hasProc) continue;
                    object mv;
                    if (d.TryGetValue("matched", out mv) && mv != null) abMatched[0] = mv.ToString();
                    object tv;
                    if (d.TryGetValue("tanis", out tv) && tv is object[])
                        foreach (object t in (object[])tv) if (t != null) abTanis.Add(t.ToString());
                    object pv2;
                    if (d.TryGetValue("process", out pv2) && pv2 is object[])
                    {
                        foreach (object po in (object[])pv2)
                        {
                            var pd = po as Dictionary<string, object>;
                            if (pd == null) continue;
                            var st = new ProcStep();
                            int ti = 0;
                            object tvv;
                            if (pd.TryGetValue("t", out tvv) && tvv != null) int.TryParse(tvv.ToString(), out ti);
                            st.T = ti;
                            st.Kind = StrOf(pd, "kind");
                            st.V = StrOf(pd, "v");
                            st.N = StrOf(pd, "n");
                            st.K = StrOf(pd, "k");
                            int dm = 0;
                            if (int.TryParse(StrOf(pd, "durMs"), out dm)) st.Dur = dm;
                            float f;
                            if (float.TryParse(StrOf(pd, "x"), out f)) st.X = f;
                            if (float.TryParse(StrOf(pd, "y"), out f)) st.Y = f;
                            if (float.TryParse(StrOf(pd, "z"), out f)) st.Z = f;
                            if (float.TryParse(StrOf(pd, "s"), out f) && f > 0f) st.S = f;
                            abSteps.Add(st);
                        }
                    }
                    break;
                }
                Log(abName + " loaded: steps=" + abSteps.Count + " tanis=" + abTanis.Count + " matched=" + abMatched[0]);
            }
            catch (Exception e) { Log("loadAbility(" + abName + ") ex: " + e.Message); }
        };
        string[] feiMatchedBox = new string[1] { "" };
        string[] castMatchedBox = new string[1] { "" };
        Action loadFeiZhua = delegate { loadAbility("临时飞爪", feiSteps, feiTanis, feiMatchedBox); feiMatched = feiMatchedBox[0]; };
        Action<string> loadCastAbility = delegate(string abName)
        {
            castSteps.Clear(); castTanis.Clear();
            castName = abName; castMatched = ""; castMatchedBox[0] = "";
            loadAbility(abName, castSteps, castTanis, castMatchedBox);
            castMatched = castMatchedBox[0];
        };
        // panel list: every dataset ability that has a staged process
        Action loadDatasetNames = delegate
        {
            datasetAbilityNames.Clear();
            try
            {
                if (!File.Exists(dataPath)) return;
                var ser = new JavaScriptSerializer();
                var root = ser.DeserializeObject(File.ReadAllText(dataPath, System.Text.Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("abilities")) return;
                foreach (object o in (object[])root["abilities"])
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    string nm = StrOf(d, "name");
                    if (nm == "") continue;
                    object pv2;
                    if (!d.TryGetValue("process", out pv2) || !(pv2 is object[]) || ((object[])pv2).Length == 0) continue;
                    string mt = StrOf(d, "matched");
                    if (mt == "") continue;   // skip unresolved duplicate rows
                    if (!datasetAbilityNames.Contains(nm)) datasetAbilityNames.Add(nm);
                    foreach (object po in (object[])pv2)
                    {
                        var pd = po as Dictionary<string, object>;
                        if (pd == null || StrOf(pd, "kind") != "anim") continue;
                        if (StrOf(pd, "v").ToLower().EndsWith(".tani"))
                        {
                            if (!taniAbilityNames.Contains(nm)) taniAbilityNames.Add(nm);
                            break;
                        }
                    }
                }
            }
            catch (Exception e) { Log("loadDatasetNames ex: " + e.Message); }
            // sfx-tag category: abilities whose matched tani embeds authored
            // .Sfx tags (ability_picker\sfx_tags.json, the v5 tani-tag pass).
            // Independent of whether the process plays them right now - the
            // engine path creates but does not render yet (SFX_RE_TEST.md).
            sfxTagNames.Clear();
            try
            {
                string sfxTagsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ability_picker", "sfx_tags.json");
                if (File.Exists(sfxTagsPath))
                {
                    var ser2 = new JavaScriptSerializer();
                    var root2 = ser2.DeserializeObject(File.ReadAllText(sfxTagsPath, System.Text.Encoding.UTF8)) as Dictionary<string, object>;
                    if (root2 != null)
                        foreach (KeyValuePair<string, object> kv in root2) sfxTagNames.Add(kv.Key);
                }
            }
            catch (Exception e) { Log("loadSfxTags ex: " + e.Message); }
        };

        // client skill data (ability_picker/tools/build_skill_data.py): icon/desc/
        // school per ability, straight from the client's own Skill.txt + Icon.txt
        var skillData = new Dictionary<string, Dictionary<string, object>>();
        string skillDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ability_picker", "skill_data.json");
        string skillIconDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ability_picker", "icons");
        string skillDataError = "";
        try
        {
            if (File.Exists(skillDataPath))
            {
                var ser = new JavaScriptSerializer();
                var root = ser.DeserializeObject(File.ReadAllText(skillDataPath, System.Text.Encoding.UTF8)) as Dictionary<string, object>;
                object abilObj;
                if (root != null && root.TryGetValue("abilities", out abilObj))
                {
                    var abil = abilObj as Dictionary<string, object>;
                    if (abil != null)
                        foreach (KeyValuePair<string, object> kv in abil)
                        {
                            var d = kv.Value as Dictionary<string, object>;
                            if (d != null) skillData[kv.Key] = d;
                        }
                }
            }
        }
        catch (Exception e) { skillDataError = e.Message; }
        // client tooltip markup (<SKILL ...>, <BUFF ...>) is stripped for display
        Func<string, string> stripMarkup = delegate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            bool inTag = false;
            foreach (char ch in s)
            {
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(ch);
            }
            return sb.ToString();
        };
        Func<string, string> skillTipText = delegate(string nm)
        {
            string tip = nm;
            try
            {
                Dictionary<string, object> d;
                if (skillData.TryGetValue(nm, out d))
                {
                    string school = StrOf(d, "school");
                    string kind = StrOf(d, "kind");
                    if (school != "" || kind != "")
                        tip += "  [" + kind + (kind != "" && school != "" ? " " : "") + school + "]";
                    string desc = stripMarkup(StrOf(d, "desc"));
                    if (desc != "") tip += "\n" + desc;
                    string sd = stripMarkup(StrOf(d, "simpleDesc"));
                    if (desc == "" && sd != "") tip += "\n" + sd;
                }
            }
            catch { }
            return tip;
        };

        // engine SFX playback (RC_SFX_ENGINE=1): creates the effect through the
        // engine's own KG3D_CreateSFXFromFile (sfx_shim.dll) at the given world
        // position instead of the host dummy approximation
        Func<string, float, float, float, bool> engineSfxPlay = delegate(string path, float x, float y, float z)
        {
            try
            {
                string shimPath = Path.Combine(startupPath, "sfx_shim.dll");
                IntPtr shim = LoadLibrary(shimPath);
                if (shim == IntPtr.Zero)
                {
                    Log("engine sfx: shim not loaded err=" + Marshal.GetLastWin32Error());
                    return false;
                }
                IntPtr fn = GetProcAddress(shim, "RC_Shim_SfxPlay");
                if (fn == IntPtr.Zero) { Log("engine sfx: export missing"); return false; }
                var play = (SfxPlayFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(SfxPlayFn));
                int rc = play(path, x, y, z);
                IntPtr st = GetProcAddress(shim, "RC_Shim_SfxStatus");
                string status = st == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(
                    ((SfxStatusFn)Marshal.GetDelegateForFunctionPointer(st, typeof(SfxStatusFn)))());
                Log("engine sfx play rc=" + rc + " status=" + status);
                return rc == 0;
            }
            catch (Exception e) { Log("engine sfx ex: " + e.Message); return false; }
        };

        // resolve a process step's clip name to a vfs path
        Func<string, string> resolveTani = delegate(string needle)
        {
            if (string.IsNullOrEmpty(needle)) return "";
            foreach (string t in feiTanis)
                if (t.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return t;
            foreach (string t in castTanis)
                if (t.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return f1 + "F1" + needle + ".tani";
        };

        outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skill", "out");
        Directory.CreateDirectory(outDir);
        // keep a per-run log (overwrite-safe for parallel sessions) and the
        // stable reborn.log used by the analysis scripts
        string runLog = Path.Combine(outDir, "Skill_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
        var logLines = new System.Collections.Generic.List<string>();
        Log = delegate(string s)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n";
            try { File.AppendAllText(Path.Combine(outDir, "Skill.log"), line); } catch { }
            try { File.AppendAllText(runLog, line); } catch { }
            Console.WriteLine(s);
            lock (logLines)
            {
                logLines.Add(DateTime.Now.ToString("HH:mm:ss") + " " + s);
                if (logLines.Count > 200) logLines.RemoveRange(0, logLines.Count - 200);
            }
        };
        Log("start map=" + mapPath);
        Log("skill data: " + skillData.Count + " abilities"
            + (skillDataError != "" ? " (load ex: " + skillDataError + ")" : ""));
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fp = "brand=Skill exe=" + Path.GetFileName(exePath)
                + " build=" + File.GetLastWriteTime(exePath).ToString("yyyy-MM-dd HH:mm:ss")
                + " size=" + new FileInfo(exePath).Length
                + " pid=" + System.Diagnostics.Process.GetCurrentProcess().Id;
            Log(fp);
        }
        // NO LIMITS: multiple instances and other engine clients may run
        // together. We never block anything. Separation is by resources
        // (own engine memory namespace, own runtime dir), not by exclusion.
        {
            bool haveMutex = false;
            try
            {
                var mm = new System.Threading.Mutex(true, "Global\\Skill_SingleInstance", out haveMutex);
                GC.KeepAlive(mm);
            }
            catch { haveMutex = true; }
            if (!haveMutex)
                Log("note: another Skill instance is running - continuing (no limit)");
        }
        loadFeiZhua();
        loadDatasetNames();

        var form = new Form();
        form.Text = Env("RC_TITLE", "skill v5");
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new System.Drawing.Size(1280, 720);
        var panel = new Panel();
        panel.Dock = DockStyle.Fill;
        form.Controls.Add(panel);
        var hud = new Label();
        hud.AutoSize = true;
        hud.ForeColor = System.Drawing.Color.White;
        hud.BackColor = System.Drawing.Color.FromArgb(160, 0, 0, 0);
        hud.Font = new System.Drawing.Font("Consolas", 10f);
        hud.Padding = new Padding(6);
        hud.Location = new System.Drawing.Point(38, 10);
        hud.Text = "loading...";
        hud.Visible = false;   // info window starts collapsed; "I" toggles it
        panel.Controls.Add(hud);
        // "I" toggle in the top-left corner: expands/collapses the info window
        var infoToggle = new Label();
        infoToggle.AutoSize = false;
        infoToggle.Size = new System.Drawing.Size(22, 22);
        infoToggle.Location = new System.Drawing.Point(10, 10);
        infoToggle.Text = "I";
        infoToggle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        infoToggle.ForeColor = System.Drawing.Color.White;
        infoToggle.BackColor = System.Drawing.Color.FromArgb(160, 0, 0, 0);
        infoToggle.Font = new System.Drawing.Font("Consolas", 10f, System.Drawing.FontStyle.Bold);
        infoToggle.Cursor = Cursors.Hand;
        infoToggle.MouseClick += delegate { hud.Visible = !hud.Visible; };
        panel.Controls.Add(infoToggle);

        // ---- ability panel (P): select what key "1" casts ----
        var abilityBtn = new Label();
        abilityBtn.AutoSize = false;
        abilityBtn.Size = new System.Drawing.Size(140, 22);
        abilityBtn.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        abilityBtn.ForeColor = System.Drawing.Color.White;
        abilityBtn.BackColor = System.Drawing.Color.FromArgb(160, 0, 0, 0);
        abilityBtn.Font = new System.Drawing.Font("Consolas", 10f, System.Drawing.FontStyle.Bold);
        abilityBtn.Cursor = Cursors.Hand;
        var abilityPanel = new Panel();
        abilityPanel.Size = new System.Drawing.Size(260, 420);
        abilityPanel.BackColor = System.Drawing.Color.FromArgb(210, 0, 0, 0);
        abilityPanel.Visible = false;
        Func<string> abilityLabel = delegate { return "P: " + abilitySel; };
        var abilityItems = new List<string>();
        abilityItems.Add("风来吴山");                 // single-clip demo
        foreach (string nm in datasetAbilityNames) abilityItems.Add(nm);
        // client icons (build_skill_data.py -> bin64\ability_picker\icons\<id>.png)
        var skillIcons = new Dictionary<string, Image>();
        Func<string, Image> iconFor = delegate(string nm)
        {
            Image img;
            if (skillIcons.TryGetValue(nm, out img)) return img;
            img = null;
            try
            {
                Dictionary<string, object> d;
                if (skillData.TryGetValue(nm, out d))
                {
                    string png = StrOf(d, "iconPng");
                    if (png != "")
                    {
                        string p = Path.Combine(skillIconDir, png);
                        if (File.Exists(p))
                        {
                            // copy through a stream so the file is not locked
                            using (var fs = File.OpenRead(p))
                            using (var tmp = Image.FromStream(fs))
                                img = new Bitmap(tmp);
                        }
                    }
                }
            }
            catch { img = null; }
            skillIcons[nm] = img;
            return img;
        };
        // icon grid: 6 per row, grouped - abilities whose staged process carries
        // authored tani .Sfx tags first, then the rest (hover = client tooltip,
        // click = cast)
        var abilityGrid = new FlowLayoutPanel();
        abilityGrid.Location = new System.Drawing.Point(6, 6);
        abilityGrid.Size = new System.Drawing.Size(248, 378);
        abilityGrid.AutoScroll = true;
        abilityGrid.BackColor = System.Drawing.Color.FromArgb(12, 12, 12);
        abilityGrid.FlowDirection = FlowDirection.LeftToRight;
        abilityGrid.WrapContents = true;
        var skillTip = new ToolTip();
        skillTip.InitialDelay = 200;
        skillTip.ReshowDelay = 100;
        skillTip.AutoPopDelay = 20000;
        var sfxItems = new List<string>();
        var noneItems = new List<string>();
        foreach (string nm in abilityItems)
        {
            if (sfxTagNames.Contains(nm)) sfxItems.Add(nm); else noneItems.Add(nm);
        }
        var orderedItems = new List<string>();
        var sectionAt = new Dictionary<int, string>();
        if (sfxItems.Count > 0)
        {
            sectionAt[orderedItems.Count] = "sfx tags (" + sfxItems.Count + ")";
            orderedItems.AddRange(sfxItems);
        }
        sectionAt[orderedItems.Count] = "none (" + noneItems.Count + ")";
        orderedItems.AddRange(noneItems);
        Func<string, Label> sectionLabel = delegate(string txt)
        {
            var lb = new Label();
            lb.Text = txt;
            lb.ForeColor = System.Drawing.Color.FromArgb(255, 220, 140);
            lb.BackColor = System.Drawing.Color.FromArgb(12, 12, 12);
            lb.Font = new System.Drawing.Font("Consolas", 9f, System.Drawing.FontStyle.Bold);
            lb.Size = new System.Drawing.Size(210, 16);
            lb.Margin = new Padding(1, 4, 1, 1);
            return lb;
        };
        var abilityIcons = new List<PictureBox>();
        int abilitySelIdx = orderedItems.IndexOf(abilitySel);
        if (abilitySelIdx < 0) abilitySelIdx = 0;
        if (orderedItems.Count > 0) abilitySel = orderedItems[abilitySelIdx];
        for (int i = 0; i < orderedItems.Count; i++)
        {
            string name = orderedItems[i];
            string sect;
            if (sectionAt.TryGetValue(i, out sect))
            {
                Label slb = sectionLabel(sect);
                abilityGrid.Controls.Add(slb);
                abilityGrid.SetFlowBreak(slb, true);
            }
            var pb = new PictureBox();
            pb.Size = new System.Drawing.Size(32, 32);
            pb.Margin = new Padding(1);
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.Cursor = Cursors.Hand;
            pb.BackColor = System.Drawing.Color.FromArgb(24, 24, 24);
            pb.BorderStyle = (name == abilitySel) ? BorderStyle.FixedSingle : BorderStyle.None;
            Image img = iconFor(name);
            if (img != null) pb.Image = img;
            skillTip.SetToolTip(pb, skillTipText(name));
            pb.Click += delegate
            {
                abilitySel = name;
                for (int k = 0; k < abilityIcons.Count; k++)
                    abilityIcons[k].BorderStyle = (abilityIcons[k] == pb) ? BorderStyle.FixedSingle : BorderStyle.None;
                abilityBtn.Text = abilityLabel();
                clickCastRequested = true;
                Log("ability click-cast: " + abilitySel);
            };
            abilityGrid.Controls.Add(pb);
            abilityIcons.Add(pb);
        }
        abilityPanel.Controls.Add(abilityGrid);
        var soundBox = new CheckBox();
        soundBox.Text = "sound";
        soundBox.ForeColor = System.Drawing.Color.White;
        soundBox.Checked = soundOn;
        soundBox.Location = new System.Drawing.Point(6, 392);
        soundBox.AutoSize = true;
        soundBox.CheckedChanged += delegate { soundOn = soundBox.Checked; };
        abilityPanel.Controls.Add(soundBox);
        panel.Controls.Add(abilityPanel);
        abilityBtn.MouseClick += delegate
        {
            abilityPanel.Visible = !abilityPanel.Visible;
            if (abilityPanel.Visible) abilityPanel.BringToFront();
        };
        Action placeAbilityUi = delegate
        {
            abilityBtn.Location = new System.Drawing.Point(Math.Max(0, panel.ClientSize.Width - 150), 10);
            abilityPanel.Location = new System.Drawing.Point(Math.Max(0, panel.ClientSize.Width - 272), 36);
            abilityBtn.Text = abilityLabel();
        };
        panel.Resize += delegate { placeAbilityUi(); };
        panel.Controls.Add(abilityBtn);
        placeAbilityUi();

        form.Show();
        Application.DoEvents();

        var baselib = new KGBaseCLR();
        var engine = new KGEngineCLR();
        var editor = new KGMovieEditorCLR();
        var sound = new KG3DSoundCLR();

        engine.SetRootPath(workingDir);
        try { baselib.InitConsoleLog(); } catch (Exception e) { Log("InitConsoleLog: " + e.Message); }
        Directory.CreateDirectory(Path.Combine(startupPath, "logs"));
        int r1 = 0, r2 = 0, r3 = 0;
        try { r1 = baselib.InitPath(workingDir, false); } catch (Exception e) { Log("InitPath ex: " + e.Message); }
        try { r2 = baselib.InitMemory("Skill.memory"); } catch (Exception e) { Log("InitMemory ex: " + e.Message); }
        try { r3 = baselib.InitPak(false); } catch (Exception e) { Log("InitPak ex: " + e.Message); }
        Log(string.Format("InitPath={0} InitMemory={1} InitPak={2}", r1, r2, r3));

        int err = 1;
        int ok = 0;
        // NOTE: the engine root must stay bin64 - the engine loads root-relative
        // resources during init (data\public\EnginePreloadList.csv, version.cfg,
        // shader dirs). A subdir root crashes init instantly (verified).
        try { ok = engine.Init3DEngine(startupPath, startupPath, workingDir, 0, "./configHttpFile.ini", ref err); }
        catch (Exception e) { Log("Init3DEngine ex: " + e); return; }
        Log(string.Format("Init3DEngine={0} err={1}", ok, err));
        if (ok == 0) { Log("FATAL: engine init failed"); return; }
        try { Log("editor.Init result=" + editor.Init(editorRoot, err, form.Handle.ToInt64())); }
        catch (Exception e) { Log("editor.Init ex: " + e.Message); }

        // editor EngineLayer::Init order (rule 6): KG3DSoundCLR.Init + actor
        // options + engine command + async load flags. The .Sfx tag renderer
        // touches the Wwise/sound path, so the sound system must be up.
        try { Log("sound.Init result=" + sound.Init(startupPath, form.Handle.ToInt64())); }
        catch (Exception e) { Log("sound.Init ex: " + e.Message); }
        try { Log("SetActorCreateOption=" + engine.SetActorCreateOption(0)); }
        catch (Exception e) { Log("SetActorCreateOption ex: " + e.Message); }
        try { Log("ExecCommand(rtxradius 0)=" + engine.ExecCommand("rtxradius 0")); }
        catch (Exception e) { Log("ExecCommand ex: " + e.Message); }

        var scene = new KGSceneCLR();
        int loadResult = scene.LoadMap(mapPath, false);
        Log("LoadMap result=" + loadResult);
        if (loadResult < 0) { Log("FATAL: LoadMap failed"); return; }
        scene.SetActiveEnvironment();
        long winId = scene.AddOutputWindow("", panel.Handle.ToInt64(), 0);
        Log("winId=" + winId);

        // CLR scene-proxy route: read the managed KGSceneCLR.m_pScene field by
        // reflection (the byte-scan of the wrapper could not) and hand it to
        // the shim, which calls the engine camera getters SEH-guarded.
        if (Env("RC_CAM_CLR", "0") == "1" || Env("RC_CAM_ENGINESET", "0") == "1")
        {
            try
            {
                var fld = typeof(KGSceneCLR).GetField("m_pScene",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                IntPtr sp = IntPtr.Zero;
                if (fld == null) Log("clr: m_pScene field not found");
                else
                {
                    object pv = fld.GetValue(scene);
                    if (pv != null) unsafe { sp = (IntPtr)System.Reflection.Pointer.Unbox(pv); }
                    Log("clr m_pScene=0x" + sp.ToInt64().ToString("X") + " " + CameraShim.ModuleOf(sp));
                    if (sp != IntPtr.Zero)
                    {
                        IntPtr vt = CameraShim.ReadP(sp);
                        Log("clr scene.vt=0x" + vt.ToInt64().ToString("X") + " " + CameraShim.ModuleOf(vt));
                        Log("clr scene: " + CameraShim.DumpObj(sp));
                        IntPtr cam = CameraShim.SceneCam(sp);
                        Log("clr sceneCam=0x" + cam.ToInt64().ToString("X") + " " + CameraShim.ModuleOf(cam));
                        if (cam != IntPtr.Zero)
                        {
                            CameraShim.EngineCam = cam;   // engine-faithful set path
                            Log("clr cam: " + CameraShim.DumpObj(cam));
                            float[] pos = new float[3], tgt = new float[3];
                            int rc = CameraShim.CamGetVt(cam, pos, tgt);
                            float gx = 0f, gy = 0f, gz = 0f;
                            try { scene.GetCameraPos(ref gx, ref gy, ref gz); } catch { }
                            Log(string.Format("clr camGet rc={0} pos=({1:F1},{2:F1},{3:F1}) tgt=({4:F1},{5:F1},{6:F1}) managed=({7:F1},{8:F1},{9:F1})",
                                rc, pos[0], pos[1], pos[2], tgt[0], tgt[1], tgt[2], gx, gy, gz));
                        }
                    }
                }
            }
            catch (Exception e) { Log("clr ex: " + e.Message); }
        }

        TerrainSampler sampler = null;
        try
        {
            int terrCache = 4;
            int.TryParse(Env("RC_TERR_CACHE", "4"), out terrCache);
            sampler = new TerrainSampler(
                @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll", mapPath, Log, terrCache);
        }
        catch (Exception e) { Log("TerrainSampler ex: " + e.Message); }

        // Prime the physics terrain loader before the engine starts streaming
        // (its first region load initialises the source reader; if the first
        // call happens after the engine's camera jump it can return all-zero
        // heights for the spawn region on 榫欓棬瀵诲疂).
        if (sampler != null)
        {
            sampler.Sample(0f, 0f);
            System.Threading.Thread.Sleep(300);
            sampler.Sample(0f, 0f);
        }

        // baked object/foliage collision (derived from the game's own map files)
        FoliageCollision col = null;
        try
        {
            string colDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "collision_data");
            string mapName = Path.GetFileNameWithoutExtension(mapPath);
            string fp = Path.Combine(colDir, mapName + "_foliage_collision.bin");
            if (!File.Exists(fp)) fp = Path.Combine(colDir, "foliage_collision.bin");
            string sp = Path.Combine(colDir, mapName + "_structure_collision.bin");
            if (!File.Exists(sp)) sp = Path.Combine(colDir, "structure_collision.bin");
            if (File.Exists(fp) || File.Exists(sp))
            {
                col = new FoliageCollision(fp, sp);
                Log("FoliageCollision: " + col.Describe()
                    + " foliage=" + (File.Exists(fp) ? Path.GetFileName(fp) : "(none)")
                    + " structures=" + (File.Exists(sp) ? Path.GetFileName(sp) : "(none)"));
            }
            else Log("FoliageCollision: no bins in " + colDir);
        }
        catch (Exception e) { Log("FoliageCollision ex: " + e.Message); }

        // native terrain ray through the host engine (same backend as the game
        // camera probes; blocks terrain-baked walls the baked set misses)
        EngineRay engineRay = new EngineRay(Log);
        try { engineRay.BindSceneObject(scene); } catch { }

        // Step C native bridge (optional, version-checked): near plane /
        // absolute camera Y / FilterCamera ray; managed fallback if absent
        CameraShim.TryLoad(Log);

        // engine SFX wiring probe (RC_SFX_PROBE=1): loads the isolated
        // sfx_shim.dll and calls the engine's own CreateScreen3DSFX /
        // CreateSFXTrackData on the live engine instance; the shim dumps the
        // interface vtables to Skill\out\sfx_probe.log
        if (Env("RC_SFX_PROBE", "0") == "1")
        {
            try
            {
                string shimPath = Path.Combine(startupPath, "sfx_shim.dll");
                IntPtr shim = LoadLibrary(shimPath);
                if (shim == IntPtr.Zero)
                    Log("sfx probe: sfx_shim.dll not loaded (err=" + Marshal.GetLastWin32Error() + ")");
                else
                {
                    IntPtr fn = GetProcAddress(shim, "RC_Shim_SfxProbe");
                    if (fn == IntPtr.Zero) Log("sfx probe: export missing");
                    else
                    {
                        var probe = (SfxProbeFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(SfxProbeFn));
                        int rc = probe();
                        IntPtr st = GetProcAddress(shim, "RC_Shim_SfxStatus");
                        string status = st == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(
                            ((SfxStatusFn)Marshal.GetDelegateForFunctionPointer(st, typeof(SfxStatusFn)))());
                        Log("sfx probe rc=" + rc + " status=" + status);
                    }
                }
            }
            catch (Exception e) { Log("sfx probe ex: " + e.Message); }
        }

        // NOTE (2026-09-27): the engine camera contract is recovered (see
        // EngineRay comments: scene vt+0x50 -> camera, cam vt+0x50/+0x58
        // position/look-at setters) but the *native m_pScene pointer behind
        // the managed KGSceneCLR wrapper is not reachable from outside: the
        // weak-handle/__makeref object dump exposes no engine pointers, the
        // Get3DScene2 scene's vt+0x50 is not get-camera (returns 0), and the
        // object scanner's KG3D_Camera slot +0x50/+0x58 are not the setters
        // (mid-function pointers; calling them AVs the engine). The direct
        // engine set is therefore disabled until the wrapper is resolved
        // (hook inside the managed call or a C++/CLI helper with the headers).
        if (Env("RC_CAM_TRACE", "0") == "1")
        {
            var mods = System.Diagnostics.Process.GetCurrentProcess().Modules;
            foreach (string mn in new string[] { "SetCameraPos", "GetCameraPos", "SetViewAngleFactor", "ResetCameraPosLookAtUp" })
            {
                try
                {
                    var mi = scene.GetType().GetMethod(mn);
                    if (mi == null) { Log("trace " + mn + ": no method"); continue; }
                    IntPtr fp = mi.MethodHandle.GetFunctionPointer();
                    for (int depth = 0; depth < 8; depth++)
                    {
                        byte[] b = new byte[5];
                        System.Runtime.InteropServices.Marshal.Copy(fp, b, 0, 5);
                        if (b[0] != 0xE8 && b[0] != 0xE9) break;
                        int rel = BitConverter.ToInt32(b, 1);
                        IntPtr next = new IntPtr(fp.ToInt64() + 5 + rel);
                        Log(string.Format("trace {0} d{1} {2:X} -> {3:X}", mn, depth,
                            fp.ToInt64(), next.ToInt64()));
                        fp = next;
                    }
                    string mod = "dynamic/heap";
                    foreach (System.Diagnostics.ProcessModule pm in mods)
                    {
                        long mb = pm.BaseAddress.ToInt64(), me = mb + pm.ModuleMemorySize;
                        if (fp.ToInt64() >= mb && fp.ToInt64() < me)
                        {
                            mod = pm.ModuleName + "+0x" + (fp.ToInt64() - mb).ToString("X");
                            break;
                        }
                    }
                    byte[] dump = new byte[96];
                    System.Runtime.InteropServices.Marshal.Copy(fp, dump, 0, dump.Length);
                    Log(string.Format("trace {0} final={1:X} {2} bytes={3}", mn,
                        fp.ToInt64(), mod, BitConverter.ToString(dump).Replace("-", " ")));
                }
                catch (Exception e) { Log("trace " + mn + " ex: " + e.Message); }
            }
        }
        if (Env("RC_CAM_IL", "0") == "1")
        {
            foreach (string mn in new string[] { "SetCameraPos", "GetCameraPos", "SetViewAngleFactor", "ResetCameraPosLookAtUp" })
            {
                try
                {
                    var mi = scene.GetType().GetMethod(mn);
                    if (mi == null) { Log("il " + mn + ": no method"); continue; }
                    IntPtr fp = mi.MethodHandle.GetFunctionPointer();
                    byte[] buf = new byte[64];
                    System.Runtime.InteropServices.Marshal.Copy(fp, buf, 0, buf.Length);
                    Log(string.Format("il {0} fp=0x{1:X} bytes={2}",
                        mn, fp.ToInt64(), BitConverter.ToString(buf).Replace("-", " ")));
                }
                catch (Exception e) { Log("il " + mn + " ex: " + e.Message); }
            }
        }

        if (Env("RC_CAM_DIFF", "0") == "1")
        {
            try
            {
                float cx = 0f, cy = 0f, cz = 0f;
                scene.GetCameraPos(ref cx, ref cy, ref cz);
                Log(string.Format("diff base=({0:F1},{1:F1},{2:F1})", cx, cy, cz));
                scene.SetCameraPos(cx + 500f, cy, cz + 500f, false);
                Log("diff A: " + CameraShim.FindAll(cx + 500f, cz + 500f));
                for (int i = 0; i < CameraShim.ObjectCount() && i < 4; i++)
                {
                    IntPtr so = CameraShim.Object(i);
                    for (uint off = 0x5C0; off <= 0x6C0; off += 0x40)
                        Log(string.Format("diff dump {0}@{1:X}+0x{2:X}: {3}",
                            CameraShim.ObjectClass(i), so.ToInt64(), off,
                            CameraShim.DumpF(so, off, 16)));
                    Log(string.Format("diff q {0}@{1:X}+0x5C0: {2}",
                        CameraShim.ObjectClass(i), so.ToInt64(), CameraShim.DumpQ(so, 0x5C0)));
                }
                scene.SetCameraPos(cx + 900f, cy, cz + 900f, false);
                Log("diff B: " + CameraShim.FindAll(cx + 900f, cz + 900f));
                scene.SetCameraPos(cx, cy, cz, false);
            }
            catch (Exception e) { Log("diff ex: " + e.Message); }
        }

        if (Env("RC_CAM_INPUT", "0") == "1")
        {
            try
            {
                IntPtr ip = CameraShim.InputPtr();
                Log(string.Format("input ptr={0:X}", ip.ToInt64()));
                float ix = 0f, iy = 0f, iz = 0f;
                scene.GetCameraPos(ref ix, ref iy, ref iz);
                scene.SetCameraPos(ix, 12345.0f, iz, false);
                for (uint off = 0x5C0; off <= 0x680; off += 0x40)
                    Log(string.Format("input dump +0x{0:X}: {1}", off,
                        CameraShim.DumpF(ip, off, 16)));
                scene.SetCameraPos(ix, iy, iz, false);
            }
            catch (Exception e) { Log("input ex: " + e.Message); }
        }

        // ---------------- player ----------------
        float px = 0f, py = 0f, pz = 0f, vy = 0f;
        float viewX = 0f, viewY = 0f, viewZ = 1f;   // spawn orientation (measured once)
        bool grounded = false;
        // JX3-modeled camera (engine_host_spike/CameraSystem.cs, ported)
        CameraSystem camSys = new CameraSystem();
        CameraObstruction camObst = new CameraObstruction();
        CameraShake camShake = new CameraShake();
        // near-plane ladder knob: clearance used by the obstruction response
        double clearanceOverride;
        if (double.TryParse(Env("RC_CAM_CLEARANCE", ""), out clearanceOverride) && clearanceOverride > 0.0)
            camObst.Clearance = clearanceOverride;
        bool playerHidden = false;
        CameraSettings cameraSettings = null;
        {
            double sc;
            if (double.TryParse(Env("RC_CAMERA_SCALE", ""), out sc) && sc > 0) camSys.UnitsPerMeter = sc;
            string camCfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skill", "camera.json");
            if (File.Exists(camCfg))
            {
                try { camSys.LoadConfig(camCfg); Log("camera config: " + camCfg); }
                catch (Exception e) { Log("camera config ex: " + e.Message); }
            }
            camSys.SwitchMode(CameraSystem.MODE_CHARACTER);
            cameraSettings = CameraSettings.Load(
                editorRoot, mapPath, AppDomain.CurrentDomain.BaseDirectory, Log);
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("MaxCameraDistance", cameraSettings.MaxCameraDistance);
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("MinCameraDistance", cameraSettings.MinCameraDistance);
            camSys.Pitch = cameraSettings.InitPitch;
            camSys.Yaw = cameraSettings.InitYaw;
            camSys.Distance = camSys.Row.F("InitCameraDistance", 6.0) * camSys.UnitsPerMeter;
            Log(string.Format("CameraSystem ready: mode={0} dist={1:F0}u height={2:F0}u units/m={3}",
                camSys.Mode, camSys.Distance,
                camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter, camSys.UnitsPerMeter));
        }
        // projected vertical FOV actually applied to the engine view: the
        // install default (config.ini KG3DENGINE CammeraAngle = 0.837757 rad =
        // 48.0 deg, the SetViewAngleFactor divisor) x factor. The aim ray must
        // use this, not the panel-default 50 deg (G-20).
        double feiAimFovDeg = 60.0;
        try
        {
            // FOV: the editor's view-angle factor. A wider value makes the
            // character look smaller (open item: the game's fFovy is missing),
            // so it can be tuned for testing with RC_VIEW_ANGLE.
            Log("view angle factor=" + scene.GetViewAngleFactor());
            float va = VideoSettings.ViewAngleFactor(editorRoot, cameraSettings.WidAngleDeg, Log);
            float vaTest;
            if (float.TryParse(Env("RC_VIEW_ANGLE", ""), out vaTest) && vaTest > 0f)
            {
                va = vaTest;
                Log("view angle factor test override=" + va);
            }
            scene.SetViewAngleFactor(va);
            feiAimFovDeg = VideoSettings.DefaultAngle * 180.0 / Math.PI * va;
            Log("view angle factor applied=" + va + " aimFov=" + feiAimFovDeg.ToString("F2") + " deg");
        }
        catch (Exception e) { Log("view angle: " + e.Message); }
        float worldDirX = 0f, worldDirZ = 0f;
        int lastKeySig = -1;
        // SB_SCAN=1: one-time grid scan with the engine's own vertical probe
        // (find columns with a raised standable surface near the spawn) -
        // engine-driven target discovery, no guessing. One row per frame.
        bool scanOn = Env("SB_SCAN", "0") == "1";
        int scanRow = -14;
        long loopStartMs = 0;
        long handle = 0, attachedHandle = -999;
        var model = new KGModelCLR();
        string curClip = null;
        float curYaw = 0f;
        float lastModelX = float.MaxValue, lastModelZ = float.MaxValue, lastModelY = float.MaxValue, lastModelYaw = float.MaxValue;

        Action<string> setClip = delegate(string path)
        {
            if (path == curClip) return;
            try
            {
                int pr = model.PlayAnimation(path, 0, 1.0f, 0);
                Log("clip -> " + path + " (" + pr + ")");
                curClip = path;
            }
            catch (Exception e) { Log("setClip ex: " + e.Message); }
        };

        // measure camera view direction by nudging forward (map-host method)
        Action measureView = delegate
        {
            try
            {
                float ax = 0f, ay = 0f, az = 0f;
                scene.GetCameraPos(ref ax, ref ay, ref az);
                scene.SetCamareMoveState(1, 1);
                // no Render here: the nudge must not be visible on screen
                for (int i = 0; i < 3; i++) { engine.FrameMove(); Application.DoEvents(); }
                scene.SetCamareMoveState(1, 0);
                float bx = 0f, by = 0f, bz = 0f;
                scene.GetCameraPos(ref bx, ref by, ref bz);
                // put the camera back where it was: the nudge must not shift it
                scene.SetCameraPos(ax, ay, az, false);
                float dx = bx - ax, dy = by - ay, dz = bz - az;
                float dl = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dl > 0.5f) { viewX = dx / dl; viewY = dy / dl; viewZ = dz / dl; }
            }
            catch { }
        };

        // orbit calibration probe: measure pixel -> radians for ROTATE_CAMERA
        if (Env("RC_ORBIT_TEST", "0") == "1")
        {
            Action<int, int, int, int> send = delegate(int act, int a2, int x, int y)
            {
                scene.ExecAction(act, a2, 0, ((y & 0xFFFF) << 16) | (x & 0xFFFF));
                Pump(engine, 60);
            };
            Action<string> logDir = delegate(string tag)
            {
                float ax = 0f, ay = 0f, az = 0f;
                scene.GetCameraPos(ref ax, ref ay, ref az);
                scene.SetCamareMoveState(1, 1);
                for (int i = 0; i < 3; i++) {             engine.FrameMove();
            engine.Render(); Application.DoEvents(); }
                scene.SetCamareMoveState(1, 0);
                float bx = 0f, by = 0f, bz = 0f;
                scene.GetCameraPos(ref bx, ref by, ref bz);
                float dx = bx - ax, dy = by - ay, dz = bz - az;
                float dl = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dl > 1e-4f) Log(string.Format("orbit {0}: dir=({1:F3},{2:F3},{3:F3}) cam=({4:F0},{5:F0},{6:F0})",
                    tag, dx / dl, dy / dl, dz / dl, bx, by, bz));
                else Log("orbit " + tag + ": no movement");
            };
            try
            {
                logDir("start");
                send(30, 1, 640, 360);
                send(1, 1, 840, 360);   // +200 px horizontal
                logDir("after +200x");
                send(30, 1, 640, 360);
                send(1, 1, 640, 510);   // +150 px vertical
                logDir("after +150y");
                send(30, 1, 640, 360);
                send(1, 1, 640, 210);   // -150 px vertical
                logDir("after -150y");
            }
            catch (Exception e) { Log("orbit test ex: " + e.Message); }
            if (Env("RC_ORBIT_TEST_ONLY", "0") == "1") return;
        }

        try
        {
            scene.ResetCameraPosLookAtUp();
            Pump(engine, 300);
            measureView();
            string spawnEnv = Environment.GetEnvironmentVariable("RC_SPAWN");
            if (!string.IsNullOrEmpty(spawnEnv))
            {
                string[] sp = spawnEnv.Split(',');
                px = float.Parse(sp[0]); pz = float.Parse(sp[2]);
                if (sp.Length > 1) float.TryParse(sp[1], out py);
            }
            else
            {
                // default test spawn on 榫欓棬瀵诲疂 (override with RC_SPAWN=x,y,z)
                px = 23334f; py = 761f; pz = 24224f;
            }
            // The physics terrain loader tracks the engine's streamed terrain:
            // right after the camera jumps it can return all-zero heights for
            // the spawn region (observed on 榫欓棬瀵诲疂). Pump frames and retry
            // through the neighbouring region until real heights arrive.
            py = sampler != null ? sampler.Sample(px, pz) : 0f;
            if (sampler != null && py == 0f)
            {
                long warm = Environment.TickCount;
                while (py == 0f && Environment.TickCount - warm < 10000)
                {
                    Pump(engine, 250);
                    sampler.Sample(px - 51200f, pz);
                    py = sampler.Sample(px, pz);
                }
                Log("spawn ground settle took " + (Environment.TickCount - warm) + "ms");
            }
            Log(string.Format("spawn=({0:F0},{1:F0},{2:F0}) view=({3:F2},{4:F2})", px, py, pz, viewX, viewZ));
        }
        catch (Exception e) { Log("spawn ex: " + e.Message); }

        Action<float, float, float, float> placePlayer = delegate(float x, float y, float z, float yaw)
        {
            try
            {
                var pos = new CLRfloat3(); pos.x = x; pos.y = playerHidden ? y - 100000f : y; pos.z = z;
                float half = (yaw + yawOffset) * 0.5f;
                var rot = new CLRfloat4(); rot.x = 0f; rot.y = (float)Math.Sin(half); rot.z = 0f; rot.w = (float)Math.Cos(half);
                var scl = new CLRfloat3(); scl.x = scale; scl.y = scale; scl.z = scale;
                handle = scene.AddDummyModel("player", actorPath, pos, rot, scl);
                if (handle == 0 || handle == -1)
                {
                    scene.RemoveDummyModel("player");
                    handle = scene.AddDummyModel("player", actorPath, pos, rot, scl);
                }
            }
            catch (Exception e) { Log("placePlayer ex: " + e.Message); }
        };
        placePlayer(px, py, pz, curYaw);
        Log("player handle=" + handle);
        model.AttachModel(handle);
        attachedHandle = handle;
        setClip(clipIdle);
        Pump(engine, 500);
        // camera yaw from the measured engine view direction (camera -> anchor)
        if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
        {
            camSys.Yaw = Math.Atan2(-viewZ, -viewX);
            Log(string.Format("camera yaw init={0:F3} (view dir {1:F2},{2:F2})", camSys.Yaw, viewX, viewZ));
        }

        // ---------------- input ----------------
        bool pW = false, pA = false, pS = false, pD = false, shiftDown = false;
        bool userShot = false, forceDiag = false;
        long f9At = 0;
        bool f9Fired = false;
        long.TryParse(Env("RC_CAM_F9AT", ""), out f9At);
        bool jumpPressed = false, skillPressed = false, spaceDown = false, oneDown = false;
        bool walkMode = false;   // real default is run; "/" (TOGGLERUN) switches to walk
        bool wSprint = false;    // double-tap W and hold -> sprint (8.8 灏?s)
        long lastWUp = 0, lastWDown = 0;
        bool demo = Env("RC_DEMO", "0") == "1", demoJumped = false, demoSkilled = false;
        bool demoCollide = Env("RC_DEMO_COLLIDE", "0") == "1", demoTeleported = false;
        bool camDemo = Env("RC_CAM_DEMO", "0") == "1";
        bool nineRay = Env("RC_CAM_9RAY", "0") == "1";      // alternate 9-ray probe set
        string camMode = Env("RC_CAM_MODE", "");            // force a camera mode row
        bool demoTeleport = Env("RC_COL_TELEPORT", "0") == "1";
        float demoDirX = 0f, demoDirZ = 0f;
        {
            string[] dd = Env("RC_DEMO_DIR", "0,1").Split(',');
            if (dd.Length >= 2) { float.TryParse(dd[0], out demoDirX); float.TryParse(dd[1], out demoDirZ); }
        }
        bool cDown = false, teleportToStructure = false;
        bool divDown = false;
        bool mouseLocked = false;
        bool lmbDown = false, rmbDown = false;
        bool dragArmed = false;
        System.Drawing.Point pressPoint = new System.Drawing.Point(0, 0);
        var lockCenter = new System.Drawing.Point(panel.ClientSize.Width / 2, panel.ClientSize.Height / 2);
        var orbitQueue = new System.Collections.Generic.Queue<int[]>();
        Func<int, int, int> makeLParam = delegate(int x, int y) { return ((y & 0xFFFF) << 16) | (x & 0xFFFF); };
        Action lockMouse = delegate
        {
            mouseLocked = true;
            Cursor.Hide();
            try { Cursor.Position = panel.PointToScreen(lockCenter); } catch { }
        };
        Action unlockMouse = delegate
        {
            mouseLocked = false;
            lmbDown = false;
            rmbDown = false;
            Cursor.Show();
        };

        // Rotate the engine camera to a target yaw/pitch through the engine's
        // own ROTATE_CAMERA orbit (bounded passes; continuous vertical deltas
        // break the engine screenshot path). Engine orbit is ~0.0018 rad/px
        // yaw / ~0.00121 rad/px pitch.
        Action<double, double> alignEngineCamera = delegate(double targetYaw, double targetPitch)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                measureView();
                double currentYaw = Math.Atan2(-viewZ, -viewX);
                double currentPitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, viewY)));
                double yawDelta = WrapAngle(targetYaw - currentYaw);
                double pitchDelta = targetPitch - currentPitch;
                if (Math.Abs(yawDelta) < 0.015 && Math.Abs(pitchDelta) < 0.015) break;
                int dx2 = (int)Math.Round(-yawDelta / 0.0018);
                int dy2 = (int)Math.Round(-pitchDelta / 0.00121);
                if (dx2 > 300) dx2 = 300; if (dx2 < -300) dx2 = -300;
                if (dy2 > 300) dy2 = 300; if (dy2 < -300) dy2 = -300;
                scene.ExecAction(30, 1, 0, makeLParam(lockCenter.X, lockCenter.Y));
                scene.ExecAction(1, 1, 0, makeLParam(lockCenter.X + dx2, lockCenter.Y + dy2));
                engine.FrameMove();
            }
            measureView();
            if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
                camSys.Yaw = Math.Atan2(-viewZ, -viewX);
            camSys.Pitch = targetPitch;
        };

        // The model pitch is the offset parameter of the JX3 sphere offset;
        // the engine view pitch that keeps the anchor centered follows from it:
        //   aimPitch = -atan2(offsetY, |offsetXZ|)
        // (offset is anchor->camera, so the view direction camera->anchor is -offset).
        Func<double> geometricAimPitch = delegate()
        {
            double h = camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter;
            // the placement scales the row distance by EyeScale, so the aim
            // must use the same effective distance (S2)
            double d = Math.Max(1.0, camSys.Distance * cameraSettings.EyeScale);
            double[] off = new double[3];
            CameraSystem.DesiredOffset(camSys.Yaw, camSys.Pitch, d, h, off);
            double horiz = Math.Sqrt(off[0] * off[0] + off[2] * off[2]);
            return -Math.Atan2(off[1], Math.Max(1e-3, horiz));
        };

        // Align the engine aim to the geometry without touching the tracked
        // model pitch (alignEngineCamera would overwrite it with the view pitch).
        Action alignAim = delegate()
        {
            // engine-faithful set path owns the view (look-at); the orbit
            // alignment emulation must not run there.
            if (Env("RC_CAM_ENGINESET", "0") == "1") return;
            double modelPitch = camSys.Pitch;
            alignEngineCamera(camSys.Yaw, geometricAimPitch());
            camSys.Pitch = modelPitch;
        };

        // Aim pitch of any model pitch (the view pitch that keeps the anchor
        // centred for the current distance/height). Used by the feed-forward
        // that keeps the engine look on the anchor while the model pitch moves.
        Func<double, double> aimPitchOf = delegate(double p)
        {
            double h = camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter;
            double d = Math.Max(1.0, camSys.Distance * cameraSettings.EyeScale);
            return -Math.Atan2(Math.Sin(p) * d + h, Math.Cos(p) * d);
        };

        // Camera yaw that puts the camera behind the character (curYaw = facing).
        Func<double> cameraYawBehind = delegate()
        {
            return Math.Atan2(-Math.Cos(curYaw), -Math.Sin(curYaw));
        };

        // Real bindings (ui/hotkey/default.txt): LMB drag = rotate camera,
        // RMB drag = rotate camera and turn the character, wheel = x0.9/x1.1
        // zoom, F11 = reset behind the character (-15 deg pitch), Home/End =
        // view presets 0/180 relative to the character facing.
        // The handlers are shared by the panel and the HUD labels (a label
        // would otherwise swallow clicks), with coordinates mapped to the panel.
        Func<object, MouseEventArgs, System.Drawing.Point> panelPoint = delegate(object s, MouseEventArgs e)
        {
            Control c = s as Control;
            if (c == null || c == panel) return e.Location;
            return panel.PointToClient(c.PointToScreen(e.Location));
        };
        MouseEventHandler onMouseDown = delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) lmbDown = true;
            else if (e.Button == MouseButtons.Right) rmbDown = true;
            // S7: don't lock the cursor on press; a click must stay a click.
            // The lock (and camera rotation) starts once the pointer moves.
            pressPoint = panelPoint(s, e);
            dragArmed = true;
        };
        MouseEventHandler onMouseUp = delegate(object s, MouseEventArgs e)
        {
            // a press that never moved never locked the cursor -> it is a CLICK
            // (the game's CAMERAORSELECTORMOVE semantics: LMB click = select/ground cast)
            if (!mouseLocked && feiAiming)
            {
                if (e.Button == MouseButtons.Left) feiConfirm = true;
                else if (e.Button == MouseButtons.Right) feiCancel = true;
            }
            if (e.Button == MouseButtons.Left) lmbDown = false;
            else if (e.Button == MouseButtons.Right) rmbDown = false;
            dragArmed = false;
            // joystick mode keeps the cursor locked between drags
            if (!lmbDown && !rmbDown && mouseLocked && cameraSettings.CameraMode != 1) unlockMouse();
        };
        MouseEventHandler onMouseMove = delegate(object s, MouseEventArgs e)
        {
            bool joystick = cameraSettings.CameraMode == 1;
            if ((!lmbDown && !rmbDown) && !joystick) return;
            if (!joystick && !dragArmed) return;
            System.Drawing.Point p = panelPoint(s, e);
            if (!mouseLocked)
            {
                if (joystick)
                {
                    // operation mode 1 (joystick): Scene_LockMouseRotation -
                    // mouse movement rotates without holding a button
                    lockMouse();
                    return;
                }
                int mdx = p.X - pressPoint.X, mdy = p.Y - pressPoint.Y;
                if (mdx * mdx + mdy * mdy < 16) return;   // 4 px dead zone
                lockMouse();
                return;
            }
            int dx = p.X - lockCenter.X, dy = p.Y - lockCenter.Y;
            if (dx != 0 || dy != 0)
            {
                int sx = (int)Math.Round(dx * cameraSettings.DragSpeed);
                int sy = (int)Math.Round(dy * cameraSettings.DragPitchSpeed);
                orbitQueue.Enqueue(new int[] { sx, sy });
                try { Cursor.Position = panel.PointToScreen(lockCenter); } catch { }
            }
        };
        MouseEventHandler wheel = delegate(object s, MouseEventArgs e)
        {
            // CameraZoomIn/Out: Camera_Zoom(0.9 / 1.1)
            camSys.ZoomBy(e.Delta > 0 ? -1.0 : 1.0);
        };
        Control[] hitTargets = new Control[] { panel, hud };
        foreach (Control c in hitTargets)
        {
            c.MouseDown += onMouseDown;
            c.MouseUp += onMouseUp;
            c.MouseMove += onMouseMove;
            c.MouseWheel += wheel;
        }
        // track the pointer for ground targeting (临时飞爪 target under cursor)
        panel.MouseMove += delegate(object s, MouseEventArgs e) { lastMousePt = e.Location; };
        form.MouseWheel += wheel;
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (feiAiming) { feiCancel = true; e.Handled = true; }
                unlockMouse();
            }
            if (e.KeyCode == Keys.W)
            {
                long t = Environment.TickCount;
                if (!pW || t - lastWDown > 100)   // new press, not keyboard auto-repeat
                {
                    // double-tap: second press within 500 ms of the first release
                    if (lastWUp != 0 && t - lastWUp < 500)
                    {
                        wSprint = true;
                        Log("sprint on (double-tap W)");
                    }
                    lastWDown = t;
                }
                pW = true;
            }
            else if (e.KeyCode == Keys.S) pS = true;
            else if (e.KeyCode == Keys.A) pA = true;
            else if (e.KeyCode == Keys.D) pD = true;
            else if (e.KeyCode == Keys.ShiftKey) shiftDown = true;
            else if (e.KeyCode == Keys.Space && !spaceDown) { spaceDown = true; jumpPressed = true; }
            else if (e.KeyCode == Keys.D1 && !oneDown) { oneDown = true; skillPressed = true; }
            else if (e.KeyCode == Keys.P)
            {
                abilityPanel.Visible = !abilityPanel.Visible;
                if (abilityPanel.Visible) abilityPanel.BringToFront();
                Log("ability panel " + (abilityPanel.Visible ? "shown" : "hidden"));
            }
            else if (e.KeyCode == Keys.C && !cDown) { cDown = true; teleportToStructure = true; }
            else if ((e.KeyCode == Keys.Divide || e.KeyCode == Keys.OemQuestion) && !divDown)
            {
                // real TOGGLERUN binding (numpad /), also accept the main "/"
                divDown = true;
                walkMode = !walkMode;
                Log("movement mode: " + (walkMode ? "WALK" : "RUN"));
            }
            else if (e.KeyCode == Keys.F11)
            {
                // Camera reset: behind the character, model pitch -15 deg, distance 1x
                camSys.SetMaxDistance(camSys.ClampDistanceUnits(
                    camSys.Row.F("InitCameraDistance", 6.0) * camSys.UnitsPerMeter) / camSys.UnitsPerMeter);
                camSys.Yaw = cameraYawBehind();
                camSys.Pitch = -Math.PI / 12.0;
                alignAim();
                Log("camera reset: behind character, pitch -15deg");
            }
            else if (e.KeyCode == Keys.F9)
            {
                // user repro capture: screenshot + full camera/ray diagnostics
                userShot = true;
            }
            else if (e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
            {
                // CameraSetView(0) / (180): yaw preset relative to facing
                camSys.Yaw = cameraYawBehind() + (e.KeyCode == Keys.End ? Math.PI : 0.0);
                alignAim();
                Log("camera view preset: " + (e.KeyCode == Keys.End ? "front" : "behind"));
            }
        };
        form.KeyUp += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) { pW = false; wSprint = false; lastWUp = Environment.TickCount; }
            else if (e.KeyCode == Keys.S) pS = false;
            else if (e.KeyCode == Keys.A) pA = false;
            else if (e.KeyCode == Keys.D) pD = false;
            else if (e.KeyCode == Keys.ShiftKey) shiftDown = false;
            else if (e.KeyCode == Keys.Space) spaceDown = false;
            else if (e.KeyCode == Keys.D1) oneDown = false;
            else if (e.KeyCode == Keys.C) cDown = false;
            else if (e.KeyCode == Keys.Divide || e.KeyCode == Keys.OemQuestion) divDown = false;
        };
        panel.Focus();

        // ---------------- main loop ----------------
        // table values converted from 15 logic frames/s into continuous seconds
        // (1 world unit = 1 cm; exact 15 Hz integer model is the next movement pass)
        // Real table values at the documented gameplay frame rate (GAME_FPS=16,
        // "16甯х瓑浜?绉?, UNIT_SCALE...md 搂2): walk 6 / run 20 u/frame -> 96 / 320
        // u/s. Cross-check: the official UI shows 璺戞閫熷害 5 灏?绉?and
        // 20 u/frame * 16 fps = 320 u/s = 5 * 64 u (1 灏?= 64 u). Host controls:
        // default RUN, "/" toggles WALK, hold Shift for a 10x testing speed.
        float pGravity = -2475f, pJumpV = 1350f;
        float pSpeed = 96f, pRun = 320f;
        float pSprint = 8.8f * 64f;   // double-tap W hold: 8.8 灏?s = 563.2 u/s
        // Real character size (docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md;
        // 1 unit = 1 cm): the loaded 鑺辫悵 actor (f1_1004 head + f1_2227 dress
        // parts) measures 115.58 u = 1.16 m from the extracted bind-pose
        // meshes. Capsule radius scaled from the old adult preset (25 at 170)
        // by the same ratio.
        float playerRadius = 17f, playerHeight = 116f;
        float.TryParse(Env("RC_RADIUS", "17"), out playerRadius);
        float.TryParse(Env("RC_HEIGHT", "116"), out playerHeight);
        int blockedEvents = 0;
        long colCalls = 0, colBlockedCalls = 0;
        bool colDebug = Env("RC_COL_DEBUG", "0") == "1";
        long lastMs = 0, lastLog = 0, lastHud = 0, skillUntil = 0, lastCamMeasure = 0, lastCamLog = 0, lastOrbitMs = 0, lastPostLog = 0;
        // 临时飞爪: ground-target pull state
        bool feiPull = false;
        bool feiSeqActive = false;
        bool feiBuffered = false;
        bool feiHitSounded = false;
        long feiSeqStart = 0;
        long feiPullMs = 3500;          // pull budget (3-D distance based)
        float feiRayY = -1e9f;          // raw aim-ray hit Y (pre-surface resolve)
        float feiSurfY = -1e9f;         // visible top Y at the aimed column
        // Player-collidable top of a column: first hit of the baked collision
        // geometry (roofs, rocks, foliage) cast straight down, max with the
        // baked terrain. The scene-ray descent could hit non-collidable visuals
        // (a "roof" in mid-air that locked the player in place); the collision
        // bake is the geometry the player can actually stand on.
        Func<float, float, float> visibleTop = delegate(float sx, float sz)
        {
            float best = sampler != null ? sampler.Sample(sx, sz) : -1f;
            try
            {
                if (col != null)
                {
                    float d = col.Raycast(sx, 40000f, sz, sx, 0f, sz);
                    if (d > 0f)
                    {
                        float y = 40000f - d;
                        if (y > best) best = y;
                    }
                }
            }
            catch { }
            return best;
        };
        float feiPX = 0f, feiPY = 0f, feiPZ = 0f, feiDist = 0f;
        float lastMarkerX = 1e9f, lastMarkerZ = 1e9f;
        long lastPullLogMs = 0;
        float roofHoldX = 1e9f, roofHoldZ = 1e9f, roofHoldY = -1e9f;
        long lastAimMs = 0;
        float lastVhX = 1e9f, lastVhZ = 1e9f, lastVhY = -1f;
        long lastVhMs = 0;
        bool orbitApplied = false;
        float dbgIntX = 0f, dbgIntY = 0f, dbgIntZ = 0f;
        bool dbgIntSet = false;
        double dbgHit = -1.0, dbgLen = 0.0, dbgEffDist = 0.0;
        bool dbgObst = false;
        long lastObstLog = 0;
        long lastYawSync = 0;
        int pitchAimErrPx = 0;
        double yawCorr = 0.0;
        bool aimDirty = false;
        double lastAimDist = -1.0;
        double aimPitchOverride = double.NaN;   // set when the ground clamp moves the camera
        int adjYawPx = 0, adjPitchPx = 0;       // CameraMovePitch*/FollowYaw feed (RC_MOVE_PITCH)
        bool camDebug = Env("RC_CAM_DEBUG", "0") == "1";
        // M0 knob: disable the park-below character hide so the engine's own
        // near-plane clipping can be bracketed with the clearance ladder
        bool hideNear = Env("RC_PLAYER_HIDE", "0") == "1";   // sandbox: off (model re-add caused visible switching)
        // late object scan (initialized scene view/camera) when RC_CAM_SCAN=1
        bool camScan = Env("RC_CAM_SCAN", "0") == "1", camScanDone = false;
        // Step C capability 2: write the engine camera object directly
        // (absolute Y / look-at); bind once the live camera exists
        bool nativeCam = Env("RC_CAM_NATIVE", "0") == "1", camBound = false;
        // engine-faithful set: position + look-at through the engine camera
        // object (EngineRay.SetCameraEngine) instead of the managed
        // SetCameraPos target-translation; no Y clamp, no orbit events
        bool engineSetCam = Env("RC_CAM_ENGINESET", "0") == "1";
        uint vtgtSlot = 0;
        {
            string vs = Env("RC_CAM_VTGT_SLOT", "");
            if (vs.Length > 0) uint.TryParse(vs, System.Globalization.NumberStyles.HexNumber, null, out vtgtSlot);
        }
        bool camSetTarget = Env("RC_CAM_SET_TARGET", "0") == "1";
        bool camSnapGuard = Env("RC_CAM_SNAPGUARD", "0") == "1";
        bool camPokeOnce = Env("RC_CAM_POKE_ONCE", "0") == "1";
        // native look-at approximation (default ON 2026-09-27 late, kill switch
        // RC_CAM_LOOKPACK=0, registered D3). When the resolved obstruction
        // length crosses the anchor the engine orbit is rotated 180 deg (yaw +
        // mirrored pitch) through the normal orbit input, rate-limited to the
        // engine's own fMaxAngelVel (~1.5 rad/s, <=20 px/event). It engages and
        // holds only while stationary: the moving case AVs the host engine
        // (D6 - content loading on instant view changes), so movement keeps the
        // old view. This fixes the reported see-through for the stationary
        // repro; the engine-direct path (position setter works, target setter
        // still blocked) is the full-fix route.
        bool lookPack = Env("RC_CAM_LOOKPACK", "0") == "1";
        bool viewFlipped = false;
        int flipPxTarget = 0, flipPxDelivered = 0;
        int flipPitchTarget = 0, flipPitchDelivered = 0;
        int flipVerifyPass = 0;
        long lastFlipVerify = 0;
        long flipBusyUntil = 0;   // aim sync stays frozen while the flip settles
        int yawDiffState = 0;
        long yawDiffSent = 0;
        IntPtr yawDiffObj = IntPtr.Zero;
        int yawDiffPx = 0, yawDiffDone = 0;
        int.TryParse(Env("RC_CAM_YAWFDIFF", "0"), out yawDiffPx);   // 0 = off (kill switch)
        float yawSpeedTest = 0f;
        float.TryParse(Env("RC_CAM_YAWSPEED", ""), out yawSpeedTest);
        long yawTestLast = 0, yawTestLog = 0;
        double yawTestAccum = 0.0;
        bool camDiff2 = Env("RC_CAM_DIFF2", "0") == "1", camDiff2Done = false;
        int camPreIdx = -1;
        int.TryParse(Env("RC_CAM_PRE_IDX", ""), out camPreIdx);
        float preX = 0f, preY = 0f, preZ = 0f, preAX = 0f, preAY = 0f, preAZ = 0f;
        bool preSet = false;
        string[] pokeSpecs = null;
        bool rotTest = Env("RC_ROT_TEST", "0") == "1";
        int rotTestStep = -1;
        long rotTestStart = 0;
        string fixedCam = Env("RC_FIXED_CAM", "");
        bool fixedCamSet = false;
        // timed clearance ladder: "ms:value,ms:value,..." drives the obstruction
        // clearance so one run brackets the host near plane at a known wall
        long[] clrSeqAt = new long[0];
        double[] clrSeqVal = new double[0];
        int clrSeqIdx = 0;
        string clrSeqEnv = Env("RC_CAM_CLR_SEQ", "");
        if (clrSeqEnv.Length > 0)
        {
            string[] items = clrSeqEnv.Split(',');
            var ats = new System.Collections.Generic.List<long>();
            var vals = new System.Collections.Generic.List<double>();
            foreach (string it in items)
            {
                string[] kv2 = it.Split(':');
                long ta2; double cv2;
                if (kv2.Length == 2 && long.TryParse(kv2[0], out ta2) &&
                    double.TryParse(kv2[1], System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out cv2) && cv2 > 0.0)
                { ats.Add(ta2); vals.Add(cv2); }
            }
            clrSeqAt = ats.ToArray(); clrSeqVal = vals.ToArray();
        }
        long frames = 0, fpsAt = 0, fps = 0;
        int shotIdx = 0;
        long lastSetLog = 0;
        long[] shots = ParseShots(Env("RC_SHOTS", ""));

        // Initialize the model yaw/pitch from the real scene_init_param row (or
        // the user's saved runtime values with RC_CUSTOM_DAT). Maps without
        // their own row keep the spawn view. Model pitch is the offset
        // parameter of the JX3 sphere offset; the engine view pitch follows
        // from it (see geometricAimPitch / docs/camera/FIX_SPEC.md).
        bool applyCamInit = cameraSettings.HasSceneInit || cameraSettings.HasSavedRuntime;
        if (applyCamInit)
        {
            camSys.Yaw = cameraSettings.InitYaw;
            camSys.Pitch = cameraSettings.InitPitch;
            Log(string.Format("camera init applied mapId={0} yaw={1:F3} pitch={2:F3}",
                cameraSettings.MapId, camSys.Yaw, camSys.Pitch));
        }
        else
        {
            measureView();
            if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
                camSys.Yaw = Math.Atan2(-viewZ, -viewX);
            Log(string.Format("camera init skipped mapId={0} (no scene_init_param row; keeping spawn view)",
                cameraSettings.MapId));
        }

        // One-time engine aim alignment (loop-limited: continuous vertical
        // orbit deltas break the engine screenshot path). RC_PITCH_ALIGN=0 skips.
        if (Env("RC_PITCH_ALIGN", "1") == "1" && Env("RC_CAM_ENGINESET", "0") != "1")
        {
            Log(string.Format("camera aim align: yaw={0:F3} modelPitch={1:F3} aimPitch={2:F3}",
                camSys.Yaw, camSys.Pitch, geometricAimPitch()));
            alignAim();
        }
        // the game keeps the follow distance at the row value; MaxCameraDistance
        // is only the cap the wheel can zoom out to (starting at the cap made
        // the camera pump when walls passed in/out of range)
        camSys.Distance = camSys.ClampDistanceUnits(camSys.Distance);

        // ---- 临时飞爪 (28031): PointArea target ray + cast action ----
        // the area-selection resource (释放_范围选择01 family), exact authored
        // path from the .Sfx dependency list
        const string FEI_RANGE_UI = @"data\source\other\特效\技能\mesh\释放\释放_范围选择01.mesh";
        Func<float[]> computeFeiTarget = delegate
        {
            try
            {
                // scripted aim (tests): SB_AIM_WX/WZ replace the cursor ray with a
                // fixed world column; Y comes from the same standable-surface probe
                float awx, awz;
                if (float.TryParse(Env("SB_AIM_WX", ""), out awx) && float.TryParse(Env("SB_AIM_WZ", ""), out awz))
                {
                    float ahy = sampler != null ? sampler.Sample(awx, awz) : py;
                    // visible top at the aimed column (same backend as the
                    // cursor path; no height ceiling)
                    float aTop = visibleTop(awx, awz);
                    if (aTop > ahy)
                    {
                        ahy = aTop;
                        feiSurfY = aTop;
                    }
                    feiRayY = ahy;
                    float addx = awx - px, addz = awz - pz;
                    return new float[] { awx, ahy, awz, (float)Math.Sqrt(addx * addx + addz * addz) };
                }
                float cx0 = 0f, cy0 = 0f, cz0 = 0f;
                scene.GetCameraPos(ref cx0, ref cy0, ref cz0);
                float fx = (float)viewX, fy = (float)viewY, fz = (float)viewZ;
                float fl = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);
                if (fl < 1e-4f) return null;
                fx /= fl; fy /= fl; fz /= fl;
                float rgx = fz, rgz = -fx;                    // screen right = worldUp x forward
                float rgl = (float)Math.Sqrt(rgx * rgx + rgz * rgz);
                if (rgl < 1e-4f) { rgx = 1f; rgz = 0f; rgl = 1f; }
                rgx /= rgl; rgz /= rgl;
                float ux5 = -fx * fy, uy5 = 1f - fy * fy, uz5 = -fz * fy;   // up = Y - F*(Y.F)
                float vl = (float)Math.Sqrt(ux5 * ux5 + uy5 * uy5 + uz5 * uz5);
                if (vl < 1e-4f) { ux5 = 0f; uy5 = 1f; uz5 = 0f; vl = 1f; }
                ux5 /= vl; uy5 /= vl; uz5 /= vl;
                int pw = Math.Max(1, panel.ClientSize.Width), ph = Math.Max(1, panel.ClientSize.Height);
                System.Drawing.Point mp = lastMousePt;
                if (mp.X == 0 && mp.Y == 0) mp = new System.Drawing.Point(pw / 2, ph / 2);
                float nx = (mp.X - pw * 0.5f) / (pw * 0.5f);
                float ny = (ph * 0.5f - mp.Y) / (ph * 0.5f);
                float aimTestX;
                if (float.TryParse(Env("SB_AIM_NDC_X", ""), out aimTestX)) nx = aimTestX;
                double tanY = Math.Tan(feiAimFovDeg * Math.PI / 180.0 * 0.5);
                double tanX = tanY * ((double)pw / ph);
                float dx3 = fx + rgx * (float)(nx * tanX) + ux5 * (float)(ny * tanY);
                float dy3 = fy + uy5 * (float)(ny * tanY);
                float dz3 = fz + rgz * (float)(nx * tanX) + uz5 * (float)(ny * tanY);
                float dl3 = (float)Math.Sqrt(dx3 * dx3 + dy3 * dy3 + dz3 * dz3);
                dx3 /= dl3; dy3 /= dl3; dz3 /= dl3;
                // aim ray: nearest of terrain / scene (buildings are scene
                // geometry). RaySceneLevel is a guarded test backend - not used
                // here (native AV risk when sweeping over unloaded content).
                float bx4 = cx0 + dx3 * 4000f, by4 = cy0 + dy3 * 4000f, bz4 = cz0 + dz3 * 4000f;
                float best = -1f;
                try { float h1 = engineRay.RayTerrain(cx0, cy0, cz0, bx4, by4, bz4); if (h1 > 0f) best = h1; } catch { }
                try { float h2 = engineRay.RayScene(cx0, cy0, cz0, bx4, by4, bz4); if (h2 > 0f && (best < 0f || h2 < best)) best = h2; } catch { }
                float hitX, hitY, hitZ;
                if (best > 0f) { hitX = cx0 + dx3 * best; hitY = cy0 + dy3 * best; hitZ = cz0 + dz3 * best; }
                else { hitX = cx0 + dx3 * 1500f; hitY = cy0 + dy3 * 1500f; hitZ = cz0 + dz3 * 1500f; }
                float mdx = hitX - px, mdz = hitZ - pz;
                float mdl = (float)Math.Sqrt(mdx * mdx + mdz * mdz);
                const float FEI_MAX_RANGE = 40f * 64f;
                if (mdl > FEI_MAX_RANGE)
                {
                    float kk = FEI_MAX_RANGE / mdl;
                    hitX = px + mdx * kk; hitZ = pz + mdz * kk;
                    hitY = sampler != null ? sampler.Sample(hitX, hitZ) : hitY;
                }
                // visible top at the target column (roof/rock/terrain): if the
                // horizontal ray hit a wall face, resolve to the surface on top
                // of it. 28031's cast point is the picked point and the visible
                // top at the aimed column is what the pick lands on; no cap.
                feiRayY = hitY;
                float sTop = visibleTop(hitX, hitZ);
                if (sTop > hitY)
                {
                    feiSurfY = sTop;
                    hitY = sTop;
                }
                return new float[] { hitX, hitY, hitZ, mdl };
            }
            catch { return null; }
        };
        Action<long> doFeiCast = delegate(long nowMs)
        {
            float gy = sampler != null ? sampler.Sample(feiPX, feiPZ) : feiPY;
            feiSeqActive = true; feiSeqStart = nowMs;
            feiPull = false; feiBuffered = false; feiHitSounded = false;
            // pull budget from the full 3-D travel (DASH_TO_POINT(120) = 120 u
            // per logic frame at 15 fps): a fixed deadline cut tall pulls short
            float d3 = (float)Math.Sqrt(feiDist * feiDist + (feiPY - py) * (feiPY - py));
            feiPullMs = (long)(d3 / (120f * 15f) * 1000f) + 900;
            if (feiPullMs < 1200) feiPullMs = 1200;
            skillUntil = nowMs + feiPullMs;
            curClip = null;
            setClip(resolveTani("s16lxg链技能03_释放HD"));
            if (soundOn)
            {
                string wav = Path.Combine(soundDir, "62588785.wav");
                if (File.Exists(wav)) PlaySound(wav, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
            }
            Log("skill cast: 临时飞爪 -> target (" + (int)feiPX + "," + (int)feiPY + "," + (int)feiPZ
                + ") dist=" + (int)feiDist + "u range=2560u(40尺) pullMs=" + feiPullMs
                + " rawY=" + (int)feiRayY + " surfY=" + (int)feiSurfY
                + " climb=" + (int)(feiPY - py)
                + " terrainY=" + (int)gy + " device=67816/70025(hidden)");
        };

        // SB_ACTOR_TEST=1: play the ability tani on the editor's own actor object
        // (KGMovieActorCLR + AppendModel) instead of the dummy model - tests
        // whether .Sfx tag playback needs the actor path (rule 6: editor's way).
        if (Env("SB_ACTOR_TEST", "0") == "1")
        {
            try
            {
                var actor = new KGMovieActorCLR();
                actor.Init();
                ActorEditorCommandHelper.LoadFromFile(actor, actorPath, 0);
                long ah = actor.GetModelHandle();
                scene.AppendModel(ah);
                try { scene.FocusOnModel(); } catch { }
                var am = new KGModelCLR();
                am.AttachModel(ah);
                string tani = Env("SB_ACTOR_TANI", @"data\source\player\f1\动作\F1smj10双刀buff04_清净心01.tani");
                Log("actor test: handle=" + ah + " tani=" + tani + " play=" + am.PlayAnimation(tani, 0, 1.0f, 0));
            }
            catch (Exception e) { Log("actor test ex: " + e.Message); }
        }

        // SB_PROBE_MESHES=1: ground-mesh probe (find which candidate renders as a range ring)
        if (Env("SB_PROBE_MESHES", "0") == "1")
        {
            string[] probeMeshes = new string[]
            {
                @"data\source\other\特效\技能\MESH\释放\释放_范围选择01.Mesh",
                @"data\source\other\新特效\技能\Mesh\发招\Y_圆形1.Mesh",
                @"data\source\other\HD特效\技能\Mesh\发招\y_圆环01_hd.Mesh",
                @"data\source\other\HD特效\技能\Mesh\发招\D_单兵圈01.Mesh".Replace("HD特效","新特效"),
                @"data\source\other\HD特效\技能\Mesh\发招\F_范围圈270度.Mesh",
                @"data\source\other\HD特效\技能\Mesh\状态\Y_圆环01.Mesh"
            };
            for (int mi = 0; mi < probeMeshes.Length; mi++)
            {
                try
                {
                    var pp = new CLRfloat3();
                    pp.x = px;
                    pp.z = pz + 300f + mi * 450f;   // ordered by distance: 0 = nearest
                    pp.y = (sampler != null ? sampler.Sample(pp.x, pp.z) : py) + 8f;
                    var pr = new CLRfloat4(); pr.w = 1f;
                    var psc = new CLRfloat3(); psc.x = 1f; psc.y = 1f; psc.z = 1f;
                    long h = scene.AddDummyModel("probe_" + mi, probeMeshes[mi], pp, pr, psc);
                    Log("probe mesh " + mi + " handle=" + h + " -> " + probeMeshes[mi]);
                }
                catch (Exception e) { Log("probe ex " + mi + ": " + e.Message); }
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (!form.IsDisposed)
        {
            long now = sw.ElapsedMilliseconds;
            float dt = (now - lastMs) / 1000f;
            lastMs = now;
            if (dt < 0f) dt = 0f;
            if (dt > 0.05f) dt = 0.05f;
            frames++;
            if (now - fpsAt >= 1000) { fps = frames * 1000 / (now - fpsAt); frames = 0; fpsAt = now; }
            if (clrSeqIdx < clrSeqAt.Length && now >= clrSeqAt[clrSeqIdx])
            {
                camObst.Clearance = clrSeqVal[clrSeqIdx];
                Log(string.Format("clrseq t={0} clearance={1}", now, clrSeqVal[clrSeqIdx]));
                clrSeqIdx++;
            }

            // Aim sync: every 100 ms while dragging (plus once right after it
            // stops) read the engine view back with the nudge probe. Yaw is
            // authoritative there; the pitch error is stored as pixels and
            // combined into the next orbit action (closed loop). A per-frame
            // read-back is noisy, and a position read-back is circular (our
            // placement overwrites the position).
            bool dragging = orbitQueue.Count > 0 || (lastOrbitMs != 0 && now - lastOrbitMs < 150);
            // When the engine set path owns position + look-at (camera object
            // from m_pScene), the aim emulation must not run: it exists only to
            // compensate for the missing look-at and would fight the engine.
            bool engineSetActive = engineSetCam && CameraShim.EngineCam != IntPtr.Zero;
            if (engineSetActive)
            {
                yawCorr = 0.0;
                pitchAimErrPx = 0;
                orbitApplied = false;
            }
            // Near a wall with no input the aim probe is unreliable (the camera
            // sits at/around the anchor and any tiny yaw change swings the
            // obstruction ray), and a correction loop there kept creeping the
            // camera in until the wall cleared. Freeze the aim loops while
            // obstructed and idle; the drag path still runs normally.
            bool aimFrozen = (camObst.Obstructed && !dragging) ||
                             (lookPack && (viewFlipped || now < flipBusyUntil)) ||
                             (yawSpeedTest > 0f && now >= 2000 && now < 12000);
            if (aimFrozen)
            {
                yawCorr = 0.0;
                pitchAimErrPx = 0;
                orbitApplied = false;
                aimDirty = true;   // re-pin once the obstruction clears
            }
            else if (!engineSetActive && (dragging || orbitApplied || aimDirty))
            {
                lastYawSync = now;
                orbitApplied = false;
                measureView();
                if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
                {
                    // smooth correction: snapping Yaw to the measured value
                    // every sync made the camera shake while dragging
                    double vyawMeas = Math.Atan2(-viewZ, -viewX);
                    double d = vyawMeas - camSys.Yaw;
                    while (d > Math.PI) d -= 2.0 * Math.PI;
                    while (d < -Math.PI) d += 2.0 * Math.PI;
                    yawCorr = d;
                }
                double measuredPitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, viewY)));
                // If the ground clamp moved the camera off the orbit line, aim
                // at the anchor from the actual clamped position instead of the
                // unclamped geometry (S3).
                double aimTarget = double.IsNaN(aimPitchOverride)
                    ? aimPitchOf(camSys.Pitch) : aimPitchOverride;
                pitchAimErrPx = (int)Math.Round((measuredPitch - aimTarget) / 0.00121);
                // re-pin after a distance change (zoom / sprint / EyeScale) is
                // done once the engine aim is within a pixel of the target (S1)
                if (!dragging && Math.Abs(pitchAimErrPx) <= 1) aimDirty = false;
            }

            // Low-pass the (per-frame re-measured) yaw correction: fast enough
            // to keep the character centred at any drag speed, smooth enough
            // to filter the nudge noise.
            if (yawCorr != 0.0)
            {
                double ystep = yawCorr * (1.0 - Math.Exp(-Math.Min(0.05, dt) / 0.01));
                camSys.Yaw += ystep;
                yawCorr -= ystep;
                if (Math.Abs(yawCorr) < 1e-3) yawCorr = 0.0;
                double twoPiY = 2.0 * Math.PI;
                if (camSys.Yaw > Math.PI) camSys.Yaw -= twoPiY;
                if (camSys.Yaw < -Math.PI) camSys.Yaw += twoPiY;
            }

            if (orbitQueue.Count > 0 || pitchAimErrPx != 0 || adjYawPx != 0 || adjPitchPx != 0 ||
                flipPxDelivered != flipPxTarget || flipPitchDelivered != flipPitchTarget)
            {
                int ox = 0, oy = 0;
                while (orbitQueue.Count > 0) { int[] d = orbitQueue.Dequeue(); ox += d[0]; oy += d[1]; }
                // model-driven camera motion (move-pitch / yaw-follow) is fed to
                // the engine as orbit pixels: no raw drag counterpart exists, so
                // the full delta is synthesised
                int oxSend = ox + adjYawPx; adjYawPx = 0;
                // row per-frame clamps (CameraMaxDeltaYaw/Pitch, row speeds in
                // docs/camera/REAL_VALUES.md) converted through the measured engine
                // orbit sensitivity: 0.0018 rad/px yaw, 0.00121 rad/px pitch
                CameraParams orow = camSys.Row;
                double yawMaxPx = orow.F("CameraMaxDeltaYaw", 2.0 * Math.PI) / 0.0018;
                double pitchMaxPx = orow.F("CameraMaxDeltaPitch", 1.56) / 0.00121;
                if (ox > yawMaxPx) ox = (int)yawMaxPx; else if (ox < -yawMaxPx) ox = (int)-yawMaxPx;
                if (oy > pitchMaxPx) oy = (int)pitchMaxPx; else if (oy < -pitchMaxPx) oy = (int)-pitchMaxPx;

                // JX3 input (ApplyMouse): mouse X -> yaw, mouse Y -> pitch;
                // pitch += dy (drag down raises the camera offset, and the aim
                // - which stays on the character - looks further down). The
                // engine raw orbit turns the view the other way for pitch, so
                // the aim feed-forward is computed from the desired aim change:
                //   engine view pitch change = -(oy + oyFF) * 0.00121
                //   desired                  = aimPitchOf(P_new) - aimPitchOf(P_old)
                double yawNew = camSys.Yaw - ox * 0.0018;
                float twoPi = 2f * (float)Math.PI;
                if (yawNew > Math.PI) yawNew -= twoPi;
                if (yawNew < -Math.PI) yawNew += twoPi;
                camSys.Yaw = yawNew;

                double pOld = camSys.Pitch;
                camSys.Pitch += oy * 0.00121;
                // engine hard pitch limit (const 1.5550884 = pi/2 - 0.0157)
                double pmax = Math.PI / 2.0 - 0.0157;
                if (camSys.Pitch > pmax) camSys.Pitch = pmax;
                else if (camSys.Pitch < -pmax) camSys.Pitch = -pmax;
                double pNew = camSys.Pitch;

                double aimDelta = aimPitchOf(pNew) - aimPitchOf(pOld);
                int oyFF = (int)Math.Round(-aimDelta / 0.00121 - oy);
                oyFF += adjPitchPx; adjPitchPx = 0;
                // apply the closed-loop pitch error over frames (a full jump
                // right after the drag is the visible "adjustment")
                if (pitchAimErrPx != 0)
                {
                    // deadband: a residual of a pixel is nudge noise (S4)
                    if (Math.Abs(pitchAimErrPx) <= 1)
                    {
                        pitchAimErrPx = 0;
                    }
                    else
                    {
                        int apply = (int)Math.Round(pitchAimErrPx *
                            (1.0 - Math.Exp(-Math.Min(0.05, dt) / 0.01)));
                        if (apply == 0) apply = pitchAimErrPx > 0 ? 1 : -1;
                        oyFF += apply;
                        pitchAimErrPx -= apply;
                    }
                }
                if (oyFF > 400) oyFF = 400; else if (oyFF < -400) oyFF = -400;

                // look-at flip delivery: the host clamps the cursor to the
                // window, so the 180 deg flip is fed over frames (a fresh
                // action-30 reference each frame, <=500 px per step); it never
                // touches camSys.Yaw/Pitch - the placement must not rotate
                int flipStepX = 0, flipStepY = 0;
                if (flipPxDelivered != flipPxTarget)
                {
                    int diff = flipPxTarget - flipPxDelivered;
                    // rate-limit to the engine's own fMaxAngelVel (~pi/2 rad/s):
                    // an instant 180 deg burst AVed the host (shader parser,
                    // D6), while <=1.5 rad/s turned for 20 s without a crash
                    int maxStep = (int)Math.Round(1.5 * dt / 0.0018);
                    if (maxStep < 1) maxStep = 1;
                    if (maxStep > 20) maxStep = 20;
                    flipStepX = Math.Sign(diff) * Math.Min(Math.Abs(diff), maxStep);
                    flipPxDelivered += flipStepX;
                }
                if (flipPitchDelivered != flipPitchTarget)
                {
                    int diff = flipPitchTarget - flipPitchDelivered;
                    int maxStepY = (int)Math.Round(1.5 * dt / 0.00121);
                    if (maxStepY < 1) maxStepY = 1;
                    if (maxStepY > 12) maxStepY = 12;
                    flipStepY = Math.Sign(diff) * Math.Min(Math.Abs(diff), maxStepY);
                    flipPitchDelivered += flipStepY;
                }

                // one combined orbit per frame (a second ROTATE_CAMERA start
                // without a FrameMove would drop the first delta). With the
                // engine set path the view comes from look-at, so no orbit is
                // sent; the model yaw/pitch integration above still runs.
                if (!engineSetActive)
                {
                    scene.ExecAction(30, 1, 0, makeLParam(lockCenter.X, lockCenter.Y));
                    scene.ExecAction(1, 1, 0, makeLParam(lockCenter.X + oxSend + flipStepX,
                                                         lockCenter.Y + oy + oyFF + flipStepY));
                    orbitApplied = true;
                    lastOrbitMs = now;
                }
            }

            // look-at flip closed loop: the host clamps cursor moves, so the
            // exact delivered pixels are unknown; after a flip (or flip back)
            // settles, measure the engine view and feed the residual to the
            // targets so the render really aims at the anchor when crossed
            if (lookPack && flipVerifyPass > 0 && now - lastFlipVerify >= 700 &&
                flipPxDelivered == flipPxTarget && flipPitchDelivered == flipPitchTarget)
            {
                lastFlipVerify = now;
                flipVerifyPass--;
                measureView();
                float vcx = 0f, vcy = 0f, vcz = 0f;
                scene.GetCameraPos(ref vcx, ref vcy, ref vcz);
                double wantYaw, wantPitch;
                if (viewFlipped)
                {
                    double dx3 = px - vcx, dy3 = (py + 90.0) - vcy, dz3 = pz - vcz;
                    double dl3 = Math.Sqrt(dx3 * dx3 + dy3 * dy3 + dz3 * dz3);
                    if (dl3 < 1e-3) dl3 = 1e-3;
                    wantYaw = Math.Atan2(-dz3 / dl3, -dx3 / dl3);
                    wantPitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, dy3 / dl3)));
                }
                else
                {
                    wantYaw = camSys.Yaw;
                    wantPitch = aimPitchOf(camSys.Pitch);
                }
                double measYaw = Math.Atan2(-viewZ, -viewX);
                double dYaw = wantYaw - measYaw;
                while (dYaw > Math.PI) dYaw -= 2.0 * Math.PI;
                while (dYaw < -Math.PI) dYaw += 2.0 * Math.PI;
                double dPitch = wantPitch - Math.Asin(Math.Max(-1.0, Math.Min(1.0, viewY)));
                if (Math.Abs(dYaw) > 0.02 || Math.Abs(dPitch) > 0.02)
                {
                    // the host clamps each orbit burst, so the delivered pixels
                    // do not equal the requested ones; feed the measured
                    // residual back (bounded passes, spaced by the caller)
                    flipPxTarget += (int)Math.Round(-dYaw / 0.0018);
                    flipPitchTarget += (int)Math.Round(-dPitch / 0.00121);
                }
                Log(string.Format("lookpack verify flip={0} dYaw={1:F3} dPitch={2:F3} measuredYaw={3:F3} pxTarget={4}",
                    viewFlipped, dYaw, dPitch, measYaw, flipPxTarget));
            }

            if (demo)
            {
                pW = now >= 2000 && now < 12000;
                walkMode = now >= 7000 && now < 12000;   // demo walk phase
                pA = now >= 14000 && now < 18000;
                if (now >= 12500 && !demoJumped) { demoJumped = true; jumpPressed = true; }
                if (now >= 18500 && !demoSkilled) { demoSkilled = true; skillPressed = true; }
            }
            if (demoCollide)
            {
                if (demoTeleport && now >= 2000 && !demoTeleported) { demoTeleported = true; teleportToStructure = true; }
                pW = now >= 3000 && now < 9000;
            }
            if (rotTest)
            {
                if (rotTestStart == 0) rotTestStart = now;
                long step = (now - rotTestStart) / 2000;
                if (step != rotTestStep)
                {
                    rotTestStep = (int)step;
                    float[] yaws = { 0f, (float)Math.PI / 2, (float)Math.PI, -(float)Math.PI / 2 };
                    if (step >= 0 && step < yaws.Length)
                    {
                        curYaw = yaws[step];
                        placePlayer(px, py, pz, curYaw);
                        if (handle != attachedHandle) { model.AttachModel(handle); attachedHandle = handle; }
                        Log(string.Format("rot test yaw={0:F3} offset={1:F3}", curYaw, yawOffset));
                    }
                }
            }
            if (camDemo)
            {
                // engine ROTATE_CAMERA mapping measured on this host:
                // 0.0018 rad/px yaw, 0.00121 rad/px pitch (RC_ORBIT_TEST)
                if (now >= 2000 && now < 6000)
                {
                    // fast yaw sweep (~1.5 rad/s) to stress the smoothness
                    int px2 = (int)(dt * 3.0f / 0.0018f);
                    if (px2 < 1) px2 = 1;
                    orbitQueue.Enqueue(new int[] { px2, 0 });
                }
                if (now >= 6000 && now < 12000 && Env("RC_CAM_DEMO_PITCH", "1") == "1")
                {
                    // pitch probe: alternate direction so the sweep stays inside
                    // the row range (no ground clamp)
                    int py2 = (((now - 6000) / 1500) % 2 == 0) ? 1 : -1;
                    orbitQueue.Enqueue(new int[] { 0, py2 });
                }
            }

            if (teleportToStructure)
            {
                teleportToStructure = false;
                if (col != null)
                {
                    float nx, ny, nz;
                    float d = col.NearestInstance(px, pz, out nx, out ny, out nz);
                    if (d < float.MaxValue)
                    {
                        float ddx = px - nx, ddz = pz - nz;
                        float dl = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
                        if (dl < 1f) { ddx = 1f; ddz = 0f; dl = 1f; }
                        px = nx + ddx / dl * 320f;
                        pz = nz + ddz / dl * 320f;
                        py = sampler != null ? sampler.Sample(px, pz) : py;
                        vy = 0f; grounded = true;
                        float fx = nx - px, fz = nz - pz;
                        float fl = (float)Math.Sqrt(fx * fx + fz * fz);
                        if (fl > 1e-4f) { fx /= fl; fz /= fl; }
                        demoDirX = fx; demoDirZ = fz;
                        curYaw = (float)Math.Atan2(fx, fz);
                        Log(string.Format("teleport to structure: {0:F0}u away, at ({1:F0},{2:F0},{3:F0})", d, px, py, pz));
                    }
                    else Log("no solid structure found");
                }
            }

            // drift correction: measure the engine view direction only while the
            // mouse is idle (the nudge moves the camera, so keep it rare)
            if (!camObst.Obstructed && !engineSetActive &&
                !(lookPack && (viewFlipped || now < flipBusyUntil)) &&
                now - lastCamMeasure >= 1000 && now - lastOrbitMs > 400)
            {
                lastCamMeasure = now;
                measureView();
                if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
                {
                    double vyawMeas = Math.Atan2(-viewZ, -viewX);
                    double d = vyawMeas - camSys.Yaw;
                    while (d > Math.PI) d -= 2.0 * Math.PI;
                    while (d < -Math.PI) d += 2.0 * Math.PI;
                    yawCorr = d;
                }
            }
            if ((camDebug || forceDiag) && now - lastCamLog >= 500)
            {
                lastCamLog = now;
                if (forceDiag) Log("USERREPRO state follows");
                if (forceDiag && col != null)
                {
                    float cvx = 0f, cvy = 0f, cvz = 0f;
                    scene.GetCameraPos(ref cvx, ref cvy, ref cvz);
                    float vl2 = (float)Math.Sqrt(viewX * viewX + viewY * viewY + viewZ * viewZ);
                    if (vl2 > 1e-4f)
                    {
                        float qx2 = cvx + viewX / vl2 * 60f;
                        float qy2 = cvy + viewY / vl2 * 60f;
                        float qz2 = cvz + viewZ / vl2 * 60f;
                        float vb = col.Raycast(cvx, cvy, cvz, qx2, qy2, qz2);
                        float vt2 = engineRay.RayTerrain(cvx, cvy, cvz, qx2, qy2, qz2);
                        float vs2 = engineRay.RayScene(cvx, cvy, cvz, qx2, qy2, qz2);
                        Log(string.Format("viewray60 bake={0:F1} terr={1:F1} scene={2:F1} cam=({3:F0},{4:F0},{5:F0}) dir=({6:F2},{7:F2},{8:F2})",
                            vb, vt2, vs2, cvx, cvy, cvz,
                            viewX / vl2, viewY / vl2, viewZ / vl2));
                    }
                }
                float dbgx = 0f, dbgy = 0f, dbgz = 0f;
                scene.GetCameraPos(ref dbgx, ref dbgy, ref dbgz);
                double rdx = dbgx - px, rdy = dbgy - (py + 90.0), rdz = dbgz - pz;
                double rgeo = Math.Sqrt(rdx * rdx + rdy * rdy + rdz * rdz);
                measureView();
                double vyaw = Math.Atan2(-viewZ, -viewX);
                double vpitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, viewY)));
                Log(string.Format("camdbg mode={0} yaw={1:F3} pitch={2:F3} vyaw={3:F3} vpitch={4:F3} dist={5:F0} r={6:F1} cam=({7:F0},{8:F0},{9:F0}) obst={10} hit={11:F0} len={12:F0} eff={13:F0} clamp={14}",
                    camSys.Mode, camSys.Yaw, camSys.Pitch, vyaw, vpitch, camSys.Distance, rgeo, dbgx, dbgy, dbgz,
                    dbgObst ? 1 : 0, dbgHit, dbgLen, dbgEffDist, double.IsNaN(aimPitchOverride) ? 0 : 1));
                // NOTE: do NOT probe the near plane here. The view-manager
                // getter (0x1801433E0) deadlocks the engine even from a worker
                // thread (see EngineRay.ProbeNearPlane) - it can only run in the
                // engine's own frame context, which needs a native shim.
                // between= invariant: cast from the placed camera to the anchor;
                // a hit very close to the camera while unobstructed means the
                // camera is on the wrong side of a wall
                if (engineRay.Available)
                {
                    float bb = col != null ? col.Raycast(dbgx, dbgy, dbgz, px, py + 90f, pz) : -1f;
                    float bt = engineRay.RayTerrain(dbgx, dbgy, dbgz, px, py + 90f, pz);
                    float bs = engineRay.RayScene(dbgx, dbgy, dbgz, px, py + 90f, pz);
                    Log(string.Format("betweendbg cam->anchor bake={0:F0} terr={1:F0} scene={2:F0}", bb, bt, bs));
                }
                int vhr0;
                float vh0 = engineRay.RayVerticalHeight(px, 10000f, pz, 30000f, out vhr0);
                if (col != null)
                {
                    float hN = col.Raycast(px, py + 90f, pz, px, py + 90f, pz - 2000f);
                    Log(string.Format("vertprobe h={0:F0}(hr={1}) at player", vh0, vhr0));
                        float hS = col.Raycast(px, py + 90f, pz, px, py + 90f, pz + 2000f);
                        float hE = col.Raycast(px, py + 90f, pz, px + 2000f, py + 90f, pz);
                        float hW = col.Raycast(px, py + 90f, pz, px - 2000f, py + 90f, pz);
                        float tD = engineRay.RayTerrain(px, py + 90f, pz, px, py - 600f, pz);
                        float tN = engineRay.RayTerrain(px, py + 90f, pz, px, py + 90f, pz - 2000f);
                        int tHr = engineRay.LastHr, tHit = engineRay.LastHit;
                        float sN = engineRay.RayScene(px, py + 90f, pz, px, py + 90f, pz - 2000f);
                        int sHr = engineRay.LastHr, sHit = engineRay.LastHit;
                        float tE = engineRay.RayTerrain(px, py + 90f, pz, px + 2000f, py + 90f, pz);
                        float sE = engineRay.RayScene(px, py + 90f, pz, px + 2000f, py + 90f, pz);
                        Log(string.Format("obstprobe N={0:F0} S={1:F0} E={2:F0} W={3:F0} inst={4} ray={5} terrD={6:F0} terrN={7:F0}(hr={8},hit={9}) terrE={10:F0} sceneN={11:F0}(hr={12},hit={13}) sceneE={14:F0}",
                            hN, hS, hE, hW, col.InstanceCount, engineRay.Available ? 1 : 0,
                            tD, tN, tHr, tHit, tE, sN, sHr, sHit, sE));
                }
                forceDiag = false;
            }

            // scripted look-at (tests): face a world point so the engine
            // streams that area (streaming follows the camera view)
            {
                float lwx, lwz;
                if (float.TryParse(Env("SB_CAM_LOOKAT_WX", ""), out lwx)
                    && float.TryParse(Env("SB_CAM_LOOKAT_WZ", ""), out lwz))
                {
                    double dx2 = lwx - px, dz2 = lwz - pz;
                    if (Math.Abs(dx2) + Math.Abs(dz2) > 1.0)
                        camSys.Yaw = Math.Atan2(-dz2, -dx2);
                }
            }

            // movement is camera-relative: forward = camera -> anchor
            double cfx, cfz;
            camSys.Forward(out cfx, out cfz);
            float hx = (float)cfx;
            float hz = (float)cfz;

            // auto-cast (smoke/test): SB_CAST_MS (feizhua: aim then auto-confirm)
            if (clickCastRequested)
            {
                clickCastRequested = false;
                skillPressed = true;
            }

            // RC_CAST_CYCLE=<ms>: sweep - select + cast the next tani-playing
            // ability every <ms> (verification pass: effects + AV detection)
            {
                long cyc = 0;
                long.TryParse(Env("RC_CAST_CYCLE", "0"), out cyc);
                // gentle mode: the next cast waits for the previous to finish
                // (rapid tani switching AVs the engine - see EXPERIENCES)
                if (cyc > 0 && taniAbilityNames.Count > 0 && !castActive)
                {
                    if (castCycleNext == 0) castCycleNext = now + cyc;
                    if (now >= castCycleNext)
                    {
                        castCycleNext = now + cyc;
                        abilitySel = taniAbilityNames[castCycleIdx % taniAbilityNames.Count];
                        castCycleIdx++;
                        clickCastRequested = true;
                        Log("cast cycle -> " + abilitySel + " (" + castCycleIdx + "/" + taniAbilityNames.Count + ")");
                    }
                }
            }
            if (autoSkillMs > 0 && !autoSkillDone && now >= autoSkillMs)
            {
                autoSkillDone = true;
                skillPressed = true;
            }
            if (autoSkillMs > 0 && autoSkillDone && !autoSkillConfirmDone
                && feiAiming && now >= autoSkillMs + 600
                && Env("SB_AIM_HOLD", "0") != "1")
            {
                autoSkillConfirmDone = true;
                feiConfirm = true;
            }

            // skill
            if (skillPressed)
            {
                skillPressed = false;
                if (now < castCooldownUntil)
                {
                    Log("cast blocked: cooldown " + ((castCooldownUntil - now + 999) / 1000)
                        + "s (" + abilitySel + ")");
                }
                else if (castActive && abilitySel == castName)
                {
                    Log("cast blocked: effect still playing (" + abilitySel + ")");
                }
                else if (abilitySel == "临时飞爪")
                {
                    // PointArea: first press enters the targeting phase, the
                    // second press (or a ground click) confirms at the marker
                    if (!feiAiming)
                    {
                        feiAiming = true; feiConfirm = false; feiCancel = false;
                        Log("feizhua aiming: move the mouse to mark the point, LMB click to cast, Esc/right-click cancel");
                    }
                    else
                    {
                        feiConfirm = true;
                    }
                }
                else if (abilitySel == "风来吴山")
                {
                    skillUntil = now + skillMs;
                    castCooldownUntil = now + castCooldownMs;
                    curClip = null;
                    setClip(clipSkill);
                    // camera shake on the cast (host default; per-skill shake rows
                    // are data-gated)
                    camShake.Start(2.0, 0.5, 0.8, 3);
                    Log("skill cast: 风来吴山");
                }
                else
                {
                    // dataset-driven cast: any staged ability's process steps.
                    // Timing from the authored data (anim length, effect life);
                    // the animation plays ONCE.
                    loadCastAbility(abilitySel);
                    long animMs = 1000, pssMs = 3000;
                    foreach (ProcStep s in castSteps)
                    {
                        if (s.Kind == "anim" && s.Dur > 0) animMs = s.Dur;
                        if (s.Kind == "dummy" && s.Dur > 0) pssMs = s.Dur;
                    }
                    skillUntil = now + animMs + 40;
                    curClip = null;
                    castActive = true; castStart = now; castUntil = now + pssMs + 120;
                    // RC_CAST_CAP=<ms>: cap the cast duration (sweep runs)
                    {
                        long cap = 0;
                        long.TryParse(Env("RC_CAST_CAP", "0"), out cap);
                        if (cap > 0 && castUntil > now + cap) castUntil = now + cap;
                    }
                    castIdx = 0; castPss = false; castPssPath = "";
                    lastCastX = 1e9f; lastCastZ = 1e9f;
                    castCooldownUntil = now + castCooldownMs;
                    // a new cast replaces any previous effect instance
                    try { scene.RemoveDummyModel("cast_pss"); } catch { }
                    Log("cast: " + abilitySel + " steps=" + castSteps.Count + " animMs=" + animMs + " pssMs=" + pssMs);
                }
            }

            // 临时飞爪 targeting phase: marker at the ground point under the cursor
            if (feiAiming)
            {
                if (feiCancel)
                {
                    feiCancel = false; feiAiming = false;
                    try { scene.RemoveDummyModel("fei_marker"); } catch { }
                    Log("feizhua aiming cancelled");
                }
                else if (feiConfirm)
                {
                    feiConfirm = false; feiAiming = false;
                    try { scene.RemoveDummyModel("fei_marker"); } catch { }
                    if (feiDist > 0f) { doFeiCast(now); castCooldownUntil = now + castCooldownMs; }
                }
                else
                {
                    // native aim ray at ~10 Hz only (per-frame sweeping AVs the
                    // engine when the ray crosses unloaded content)
                    float[] tgt = null;
                    if (now - lastAimMs >= 100)
                    {
                        lastAimMs = now;
                        tgt = computeFeiTarget();
                    }
                    if (tgt != null)
                    {
                        feiPX = tgt[0]; feiPY = tgt[1]; feiPZ = tgt[2]; feiDist = tgt[3];
                        if (Math.Abs(feiPX - lastMarkerX) > 16f || Math.Abs(feiPZ - lastMarkerZ) > 16f)
                        {
                            lastMarkerX = feiPX; lastMarkerZ = feiPZ;
                            var mpos = new CLRfloat3(); mpos.x = feiPX; mpos.y = feiPY + 8f; mpos.z = feiPZ;
                            var mrot = new CLRfloat4(); mrot.w = 1f;
                            // authored size at scale 1; SB_FEI_RING_SCALE for review
                            float ringScale = 1f;
                            float.TryParse(Env("SB_FEI_RING_SCALE", "1"), out ringScale);
                            if (ringScale <= 0f) ringScale = 1f;
                            var mscl = new CLRfloat3(); mscl.x = ringScale; mscl.y = ringScale; mscl.z = ringScale;
                            long mh = scene.AddDummyModel("fei_marker", FEI_RANGE_UI, mpos, mrot, mscl);
                            Log("feizhua marker -> (" + (int)feiPX + "," + (int)feiPY + "," + (int)feiPZ + ") d=" + (int)feiDist
                                + "u / 2560u" + (feiDist <= 40f * 64f ? " [castable]" : " [out of range]")
                                + " rawY=" + (int)feiRayY + " surfY=" + (int)feiSurfY
                                + " climb=" + (int)(feiPY - py)
                                + " scale=" + ringScale.ToString("F1") + " marker=" + mh);
                        }
                    }
                }
            }

            // 临时飞爪 sequence beats: 5-frame release (chain+pull), hit sound, device life
            if (feiSeqActive)
            {
                long el = now - feiSeqStart;
                if (!feiPull && !feiBuffered && el >= 83)
                {
                    feiPull = true;
                    // dash animation from skill_dash: 28033 -> AnimationID 91076
                    // -> F1s16lxg链技能03b_hd.tani (the pull itself)
                    curClip = null;
                    setClip(resolveTani("s16lxg链技能03b_hd"));
                    Log("feizhua release (5 frames): chain 28032 + pull 28033 (03b_hd) -> ("
                        + (int)feiPX + "," + (int)feiPZ + ")");
                }
                if (!feiHitSounded && el >= 450)
                {
                    feiHitSounded = true;
                    if (soundOn)
                    {
                        string wav = Path.Combine(soundDir, "697798714.wav");
                        if (File.Exists(wav)) PlaySound(wav, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
                    }
                }
                if (el >= 2700)
                {
                    feiSeqActive = false;
                    Log("feizhua device gone (160帧寿命, was hidden)");
                }
            }

            // generic dataset process runner (anim / sound / dummy PSS on caster)
            if (castActive)
            {
                long rel = now - castStart;
                while (castIdx < castSteps.Count && castSteps[castIdx].T <= rel)
                {
                    ProcStep st = castSteps[castIdx];
                    castIdx++;
                    try
                    {
                        if (st.Kind == "anim")
                        {
                            // full vfs paths pass through; short names resolve
                            // against the ability's tani list
                            string path = st.V.IndexOf('\\') >= 0 ? st.V : resolveTani(st.V);
                            if (!string.IsNullOrEmpty(path)) { curClip = null; setClip(path); }
                            Log("cast anim -> " + st.V + " = " + path);
                        }
                        else if (st.Kind == "sound")
                        {
                            if (soundOn)
                            {
                                string wav = Path.Combine(soundDir, st.V + ".wav");
                                if (File.Exists(wav)) PlaySound(wav, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
                            }
                            Log("cast sound -> " + st.V);
                        }
                        else if (st.Kind == "dummy")
                        {
                            if (Env("SB_CAST_NOPSS", Env("SB_RUYI_NOPSS", "0")) == "1")
                            {
                                Log("cast dummy skipped (SB_CAST_NOPSS) -> " + st.V);
                            }
                            else
                            {
                                castPss = true; castPssPath = st.V;
                                Log("cast dummy -> " + st.V);
                            }
                        }
                        else if (st.Kind == "sfx")
                        {
                            // authored .Sfx (e.g. the tani's embedded tags): created
                            // and played by the engine itself via sfx_shim.dll's
                            // owner chain. Bare names resolve against
                            // bin64\ability_picker\sfx (SB_SFX_DIR overrides).
                            string sfxPath = st.V.IndexOf('\\') >= 0 || st.V.IndexOf(':') >= 0
                                ? st.V
                                : Path.Combine(sfxDir, st.V);
                            bool sfxOk = engineSfxPlay(sfxPath, px, py + 2f, pz);
                            Log("cast sfx -> " + st.V + " ok=" + (sfxOk ? 1 : 0));
                            if (!sfxOk)
                            {
                                // the engine create can hit a transient AV for some
                                // files in the cast context (shim-guarded -> NULL);
                                // retry a few times before giving up
                                SfxRetryItem ri = new SfxRetryItem();
                                ri.path = sfxPath; ri.name = st.V;
                                ri.due = now + 250; ri.tries = 0;
                                sfxRetry.Add(ri);
                            }
                        }
                    }
                    catch (Exception e) { Log("cast step ex (" + st.Kind + "): " + e.Message); }
                }
                if (castPss && castPssPath.Length > 0)
                {
                    // RC_SFX_ENGINE=1: the engine creates the effect itself
                    // (KG3D_CreateSFXFromFile); the dummy path is the fallback.
                    // Otherwise the dummy follows the caster by throttled re-adds
                    // (the engine reuses the same handle; the faithful fix is
                    // engine socket binding).
                    bool first = lastCastX > 1e8f;
                    if (first)
                    {
                        lastCastX = px; lastCastZ = pz;
                        bool engineOk = false;
                        if (Env("RC_SFX_ENGINE", "0") == "1")
                        {
                            string sfxTest = Env("RC_SFX_TEST_PATH", "");
                            engineOk = engineSfxPlay(sfxTest != "" ? sfxTest : castPssPath, px, py + 2f, pz);
                        }
                        castPssEngine = engineOk;
                        if (!engineOk)
                        {
                            var pp = new CLRfloat3(); pp.x = px; pp.y = py + 2f; pp.z = pz;
                            float half = curYaw * 0.5f;
                            var pr = new CLRfloat4(); pr.y = (float)Math.Sin(half); pr.w = (float)Math.Cos(half);
                            var ps = new CLRfloat3(); ps.x = 1f; ps.y = 1f; ps.z = 1f;
                            long h = scene.AddDummyModel("cast_pss", castPssPath, pp, pr, ps);
                            castPssHandle = h;
                            Log("cast pss -> " + castPssPath + " handle=" + h + " (follows caster)");
                        }
                    }
                    else if (!castPssEngine &&
                             (Math.Abs(px - lastCastX) > 32f || Math.Abs(pz - lastCastZ) > 32f))
                    {
                        lastCastX = px; lastCastZ = pz;
                        var pp = new CLRfloat3(); pp.x = px; pp.y = py + 2f; pp.z = pz;
                        float half = curYaw * 0.5f;
                        var pr = new CLRfloat4(); pr.y = (float)Math.Sin(half); pr.w = (float)Math.Cos(half);
                        var ps = new CLRfloat3(); ps.x = 1f; ps.y = 1f; ps.z = 1f;
                        long h = scene.AddDummyModel("cast_pss", castPssPath, pp, pr, ps);
                        if (h != castPssHandle) { castPssHandle = h; Log("cast pss re-added handle=" + h + " (effect restarted)"); }
                    }
                }
                if (now >= castUntil)
                {
                    castActive = false;
                    if (castPss)
                    {
                        castPss = false;
                        if (!castPssEngine) { try { scene.RemoveDummyModel("cast_pss"); } catch { } }
                        else Log("cast pss engine-managed (no dummy to remove)");
                    }
                    Log("cast done: " + castName);
                }
            }

            // pending engine-SFX creates that returned NULL in the cast context:
            // retry a few frames later (transient engine state, shim-guarded)
            if (sfxRetry.Count > 0)
            {
                for (int i = sfxRetry.Count - 1; i >= 0; i--)
                {
                    SfxRetryItem ri = sfxRetry[i];
                    if (now < ri.due) continue;
                    bool okR = engineSfxPlay(ri.path, px, py + 2f, pz);
                    ri.tries++;
                    if (okR)
                    {
                        Log("cast sfx retry -> " + ri.name + " ok=1 (try " + ri.tries + ")");
                        sfxRetry.RemoveAt(i);
                    }
                    else if (ri.tries >= 3)
                    {
                        Log("cast sfx retry -> " + ri.name + " gave up (3 tries, engine create NULL)");
                        sfxRetry.RemoveAt(i);
                    }
                    else
                    {
                        ri.due = now + 400;
                    }
                }
            }

            // RC_SFX_WARM (default 0; off while the engine effects do not
            // render): create every staged tag once at startup,
            // far from the player. The ME engine's first-time create for ~27 of
            // the tags AVs once the scene has settled (shim-guarded -> NULL);
            // after this warm-up the resource is cached and cast-time creates
            // succeed (measured 4/4 vs 2/4, 2026-10-06).
            if (!sfxWarmDone && Env("RC_SFX_WARM", "0") == "1")
            {
                sfxWarmDone = true;
                try
                {
                    string[] files = Directory.GetFiles(sfxDir, "*.sfx");
                    int wOk = 0;
                    foreach (string f in files)
                    {
                        bool w = engineSfxPlay(f, px + 20000f, py - 2000f, pz + 20000f);
                        if (w) wOk++;
                    }
                    Log("sfx warm: " + wOk + "/" + files.Length + " cached (far position)");
                }
                catch (Exception e) { Log("sfx warm ex: " + e.Message); }
            }

            // RC_SFX_BATCH=1: create+play every staged .sfx once and log the rc
            // table (which authored tags the engine accepts - wiring pass)
            long sfxBatchDelay = 0;
            long.TryParse(Env("RC_SFX_BATCH_DELAY", "0"), out sfxBatchDelay);
            if (Env("RC_SFX_BATCH", "0") == "1" && !sfxBatchDone && now >= sfxBatchDelay)
            {
                sfxBatchDone = true;
                try
                {
                    string[] files = Directory.GetFiles(sfxDir, "*.sfx");
                    Log("sfx batch: " + files.Length + " files in " + sfxDir);
                    foreach (string f in files)
                    {
                        bool sfxOkB = engineSfxPlay(f, px, py + 2f, pz);
                        Log("sfx batch -> " + Path.GetFileName(f) + " ok=" + (sfxOkB ? 1 : 0));
                    }
                    Log("sfx batch done");
                }
                catch (Exception e) { Log("sfx batch ex: " + e.Message); }
            }

            // input -> direction; hold the world-space direction while the key
            // set is unchanged (the camera may rotate without curving the run)
            float inX = 0f, inZ = 0f;
            float rX = hz, rZ = -hx;
            if (pW) { inX += hx; inZ += hz; }
            if (pS) { inX -= hx; inZ -= hz; }
            if (pA) { inX -= rX; inZ -= rZ; }
            if (pD) { inX += rX; inZ += rZ; }
            float inLen = (float)Math.Sqrt(inX * inX + inZ * inZ);
            if (inLen > 1e-4f) { inX /= inLen; inZ /= inLen; }
            int keySig = (pW ? 1 : 0) | (pS ? 2 : 0) | (pA ? 4 : 0) | (pD ? 8 : 0);
            if (keySig != lastKeySig) { lastKeySig = keySig; worldDirX = inX; worldDirZ = inZ; }
            float dirX = worldDirX, dirZ = worldDirZ;
            if (demoCollide) { dirX = demoDirX; dirZ = demoDirZ; }
            float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            bool moving = len > 0.01f && skillUntil <= now;

            // floor: terrain + throttled vertical scene probe (stand on roofs /
            // ledges the terrain sampler does not know about; native ray is
            // re-run only when the player moved or every 300 ms)
            float ground = sampler != null ? sampler.Sample(px, pz) : py;
            if (Math.Abs(px - lastVhX) > 120f || Math.Abs(pz - lastVhZ) > 120f
                || now - lastVhMs > 300)
            {
                lastVhX = px; lastVhZ = pz; lastVhMs = now;
                try
                {
                    lastVhY = visibleTop(px, pz);
                }
                catch { lastVhY = -1f; }
            }
            if (lastVhY > ground && lastVhY <= py + 90f) ground = lastVhY;
            if (roofHoldY > ground
                && Math.Abs(px - roofHoldX) <= 300f && Math.Abs(pz - roofHoldZ) <= 300f
                && roofHoldY <= py + 90f) ground = roofHoldY;
            bool blocked = false;
            if (moving)
            {
                float sp = (shiftDown ? pRun * 10f
                            : walkMode ? pSpeed
                            : wSprint ? pSprint
                            : pRun) / len;
                float step = sp * dt;
                float ux = dirX / len, uz = dirZ / len;
                float tryX = px + ux * step, tryZ = pz + uz * step;
                float gh = sampler != null ? sampler.Sample(tryX, tryZ) : ground;
                if (gh - ground > 70f)
                {
                    blocked = true;
                    float gx2 = sampler != null ? sampler.Sample(tryX, pz) : ground;
                    float gz2 = sampler != null ? sampler.Sample(px, tryZ) : ground;
                    if (gx2 - ground <= 70f) { px = tryX; }
                    else if (gz2 - ground <= 70f) { pz = tryZ; }
                }
                else { px = tryX; pz = tryZ; }
                curYaw = (float)Math.Atan2(ux, uz);
            }

            // 临时飞爪 pull: DASH_TO_POINT(120) = 120 u/frame at 15 logic fps,
            // full 3D - the pull climbs to the target height (roofs included)
            if (feiPull)
            {
                if (now - feiSeqStart > feiPullMs)
                {
                    feiPull = false; feiBuffered = true;
                    grounded = true; vy = 0f;
                    Log("feizhua pull timeout (target unreachable) after " + feiPullMs + "ms");
                }
                float pdx = feiPX - px, pdz = feiPZ - pz, pdy = feiPY - py;
                float pdl = (float)Math.Sqrt(pdx * pdx + pdz * pdz);
                float pstep = 120f * 15f * dt;
                if (pdl <= Math.Max(pstep, 8f) && Math.Abs(pdy) <= Math.Max(pstep, 8f))
                {
                    px = feiPX; pz = feiPZ; py = feiPY;
                    feiPull = false;
                    feiBuffered = true;
                    grounded = true; vy = 0f;
                    // roof hold: the pull target came from the aim ray - treat
                    // its height as the floor near the landing spot (no extra
                    // native probe calls; expires when walking off)
                    roofHoldX = feiPX; roofHoldZ = feiPZ; roofHoldY = feiPY;
                    curClip = null;
                    setClip(resolveTani("s16lxg链技能03_缓冲_HD"));
                    Log("feizhua landed at (" + (int)px + "," + (int)py + "," + (int)pz + ")");
                }
                else
                {
                    float ux3 = pdl > 1e-3f ? pdx / pdl : 0f;
                    float uz3 = pdl > 1e-3f ? pdz / pdl : 0f;
                    px += ux3 * pstep;
                    pz += uz3 * pstep;
                    float dyStep = Math.Sign(pdy) * Math.Min(Math.Abs(pdy), pstep);
                    py += dyStep;
                    // dash state: no ground glue while the pull is in flight
                    // (the client's DASH_TO_POINT move-state suspends it too);
                    // the floor re-engages on landing
                    grounded = true; vy = 0f;
                    if (now - lastPullLogMs >= 100)
                    {
                        lastPullLogMs = now;
                        Log("feizhua pull t=" + (now - feiSeqStart) + "ms pos=(" + (int)px + "," + (int)py + "," + (int)pz
                            + ") d3=" + (int)Math.Sqrt(pdx * pdx + pdz * pdz + pdy * pdy));
                    }
                    // only turn while there is real horizontal travel (tiny
                    // directions flipped the model left/right every frame)
                    if (pdl > 40f) curYaw = (float)Math.Atan2(ux3, uz3);
                }
            }

            // RMB (CAMERAORSELECTORMOVESTICKY) also turns the character to the
            // camera direction; LMB drag rotates the camera only. The turn is
            // rate-limited (S6) instead of snapping the yaw in one frame.
            if (rmbDown)
            {
                float targetYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));
                float d = targetYaw - curYaw;
                while (d > Math.PI) d -= 2f * (float)Math.PI;
                while (d < -Math.PI) d += 2f * (float)Math.PI;
                // RotationSpeed row values are engine int speeds (0.00314 in
                // the host rows), not rad/s; the engine's key-rotation default
                // fChaseRate is pi rad/s, so use a rad/s value only when the row
                // is clearly one, else pi (S6: 0.00314 was ~0.18 deg/s).
                float rate = (float)camSys.Row.F("RotationSpeed", 0.0);
                if (rate < 1f) rate = (float)Math.PI;
                float step = rate * (float)dt;
                if (Math.Abs(d) <= step) curYaw = targetYaw;
                else curYaw += Math.Sign(d) * step;
            }

            // object/foliage collision (walls, buildings, rocks, trees)
            // (skipped during a pull: the dash move-state owns the motion)
            if (col != null && !feiPull)
            {
                float stepGround = col.SupportHeight(px, pz, py - 20f, py + 70f);
                if (moving)
                {
                    float ux2 = dirX / len, uz2 = dirZ / len;
                    for (int si = 1; si <= 3; si++)
                    {
                        float sd = playerRadius + si * 25f;
                        float sh2 = col.SupportHeight(px + ux2 * sd, pz + uz2 * sd, py - 20f, py + 70f);
                        if (sh2 > stepGround) stepGround = sh2;
                    }
                }
                if (stepGround > ground)
                {
                    ground = stepGround;
                }
                else
                {
                    colCalls++;
                    bool sBlocked = col.Resolve(ref px, ref py, ref pz,
                        playerRadius, playerHeight, ref ground, ref grounded);
                    if (sBlocked) { blocked = true; blockedEvents++; colBlockedCalls++; }
                    if (grounded)
                    {
                        float sh = col.SupportHeight(px, pz, py - 150f, py + 60f);
                        if (sh > ground) ground = sh;
                    }
                }
            }

            // grounded / ledge / step (map-host rules)
            // (skipped during a pull - otherwise this glue snaps the climbing
            //  py straight back to the floor every frame: no Z pull)
            if (grounded && !feiPull)
            {
                if (py - ground > 150f) { grounded = false; vy = 0f; }
                else if (py > ground) py = ground;
                else if (ground - py <= 70f) py = ground;
            }

            // jump
            if (jumpPressed)
            {
                jumpPressed = false;
                if (grounded) { vy = pJumpV; grounded = false; }
            }

            // gravity
            if (!grounded && !feiPull)
            {
                vy += pGravity * dt;
                py += vy * dt;
                if (py <= ground)
                {
                    py = ground;
                    if (vy < 0f) vy = 0f;
                    grounded = true;
                }
            }

            if (scanOn)
            {
                if (loopStartMs == 0) loopStartMs = now;
                if (now - loopStartMs >= 8000 && scanRow <= 14)
                {
                    if (scanRow == -14) { scanRow = -14; }
                    if (scanRow <= 14)
                    {
                        float dz = scanRow * 300f;
                        for (int sx = -10; sx <= 10; sx++)
                        {
                            float dx = sx * 300f;
                            try
                            {
                                float vh3 = visibleTop(px + dx, pz + dz);
                                if (vh3 > py + 100f)
                                    Log("scan candidate (" + (int)(px + dx) + "," + (int)vh3 + "," + (int)(pz + dz)
                                        + ") rise=" + (int)(vh3 - py));
                            }
                            catch { }
                        }
                        scanRow++;
                        if (scanRow > 14) Log("scan done");
                    }
                }
            }

            // animation state
            if (skillUntil > now) { /* skill clip playing */ }
            else if (!grounded) setClip(vy > 0f ? clipJump : clipFall);
            else if (moving) setClip(walkMode ? clipWalk : clipRun);
            else setClip(clipIdle);

            // model update (only when changed; keeps animation alive)
            if (Math.Abs(px - lastModelX) > 0.5f || Math.Abs(py - lastModelY) > 0.5f ||
                Math.Abs(pz - lastModelZ) > 0.5f || Math.Abs(curYaw - lastModelYaw) > 0.01f)
            {
                placePlayer(px, py, pz, curYaw);
                lastModelX = px; lastModelY = py; lastModelZ = pz; lastModelYaw = curYaw;
            }
            // re-attach whenever the dummy handle changes, including while
            // stationary (the hide/show path re-adds the dummy; without this
            // the animated model stays on the old handle and can remain visible
            // while the camera is inside the character)
            if (handle != attachedHandle)
            {
                model.AttachModel(handle);
                attachedHandle = handle;
            }

            if (!string.IsNullOrEmpty(fixedCam))
            {
                if (!fixedCamSet)
                {
                    fixedCamSet = true;
                    string[] fc = fixedCam.Split(',');
                    if (fc.Length >= 3)
                    {
                        scene.SetCameraPos(float.Parse(fc[0]), float.Parse(fc[1]), float.Parse(fc[2]), true);
                        Log("fixed camera at " + fixedCam);
                    }
                }
            }
            // JX3 follow camera: the ENGINE camera owns the look direction (native
            // rotation from the mouse actions); the camera model drives the distance
            // dynamics (zoom / sprint pull-back / SmoothTime). The camera is placed
            // on the engine's own view line through the character, so it is centred.
            try
            {
                if (string.IsNullOrEmpty(fixedCam))
                {
                bool movingNow = len > 0f;
                // sprint camera mode follows the real trigger: double-tap W
                // (wSprint), not the Shift test-speed modifier
                bool sprinting = movingNow && wSprint;
                // mode harness: activate a mode row for testing (carrier /
                // air_combat / npc_dialog / god). The real gameplay triggers
                // (mount, dialog, air combat, spectate) do not exist in the
                // host yet, so this is the test path until they do.
                if (camMode.Length > 0 && camSys.Mode != camMode)
                    camSys.SwitchMode(camMode, false);

                // move-reactive camera (B6): row-gated; the real move-pitch
                // table is 0.0 in this build, so it stays opt-in until the
                // per-mode rows arrive. Any change is synthesised back to the
                // engine in the orbit block (adjYawPx / adjPitchPx).
                if (Env("RC_MOVE_PITCH", "0") == "1")
                {
                    double yawPreAdj = camSys.Yaw, pitchPreAdj = camSys.Pitch;
                    camSys.AdjustPitch(dt, movingNow);
                    if (movingNow) camSys.FollowYaw((float)Math.Atan2(-dirZ, -dirX), dt);
                    double dYawAdj = camSys.Yaw - yawPreAdj;
                    while (dYawAdj > Math.PI) dYawAdj -= 2.0 * Math.PI;
                    while (dYawAdj < -Math.PI) dYawAdj += 2.0 * Math.PI;
                    adjYawPx = (int)Math.Round(-dYawAdj / 0.0018);
                    double aimAdj = aimPitchOf(camSys.Pitch) - aimPitchOf(pitchPreAdj);
                    adjPitchPx = (int)Math.Round(-aimAdj / 0.00121);
                }
                // the automatic character/sprint mode logic must not override a
                // forced test mode (RC_CAM_MODE)
                if (camMode.Length == 0)
                {
                    if (sprinting)
                    {
                        if (camSys.Mode != CameraSystem.MODE_SPRINT)
                            camSys.SwitchMode(CameraSystem.MODE_SPRINT, false);
                    }
                    else if (camSys.Mode != CameraSystem.MODE_CHARACTER)
                    {
                        camSys.SwitchMode(CameraSystem.MODE_CHARACTER, false);
                    }
                }
                double dist = camSys.UpdateDistance(dt, sprinting, pRun / camSys.UnitsPerMeter)
                              * cameraSettings.EyeScale;
                // any distance change (wheel zoom, sprint pull-back, EyeScale)
                // changes the aim pitch; flag a re-pin (S1)
                if (Math.Abs(dist - lastAimDist) > 0.5)
                {
                    aimDirty = true;
                    lastAimDist = dist;
                }

                // JX3 sphere offset (SetCharacterCameraPosition @ 0x180B0E820,
                // docs/camera/FIX_SPEC.md): constant-length orbit around the anchor;
                // pitch only rotates it and CameraHeight is a separate additive
                // term. Never use tan(pitch) here (the old bug scaled the orbit
                // radius while dragging, so dragging changed the distance).
                double camHeight = camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter;
                double ax2 = px, ay2 = py + 90.0, az2 = pz;
                double[] camOff = new double[3];
                CameraSystem.DesiredOffset(camSys.Yaw, camSys.Pitch, dist, camHeight, camOff);
                double offLen = Math.Sqrt(camOff[0] * camOff[0] + camOff[1] * camOff[1] + camOff[2] * camOff[2]);
                if (offLen < 1e-3) offLen = 1e-3;
                double ux = camOff[0] / offLen, uy = camOff[1] / offLen, uz = camOff[2] / offLen;

                // Native JX3 obstruction: nearest hit of the anchor->camera
                // segment against structures/foliage (5-probe camera footprint)
                // and terrain; then 18 u clearance + 50/100 u hysteresis + flex
                // return (docs/camera/WALL_OBSTRUCTION.md).
                double hitDist = -1.0;
                string hitSrc = "";
                bool obstDbg = Env("RC_CAM_OBSTDBG", "0") == "1";
                if (col != null)
                {
                    double rx = uz, rz = -ux;
                    double rl = Math.Sqrt(rx * rx + rz * rz);
                    if (rl < 1e-6) { rx = 1.0; rz = 0.0; rl = 1.0; }
                    rx /= rl; rz /= rl;
                    double fx = uy * rz, fy = uz * rx - ux * rz, fz = -uy * rx;
                    const double foot = 22.0;
                    // game probe sets: default 5 rays (centre + 4 corners =
                    // perimeter 0/90/180/270), alternate 9 rays (centre + 8
                    // perimeter at 45 deg). The +0x15c trigger that selects the
                    // 9-ray mode is not recovered, so it stays opt-in.
                    int probeCount = nineRay ? 9 : 5;
                    for (int p = 0; p < probeCount; p++)
                    {
                        double ox2 = 0, oy2 = 0, oz2 = 0;
                        if (p > 0)
                        {
                            double a = nineRay ? (p - 1) * Math.PI / 4.0
                                               : (p - 1) * Math.PI / 2.0;
                            double ca = Math.Cos(a), sa = Math.Sin(a);
                            ox2 = (rx * ca + fx * sa) * foot;
                            oy2 = (fy * sa) * foot;
                            oz2 = (rz * ca + fz * sa) * foot;
                        }
                        float px2 = (float)(ax2 + ox2), py2 = (float)(ay2 + oy2), pz2 = (float)(az2 + oz2);
                        float qx2 = (float)(ax2 + ox2 + ux * offLen);
                        float qy2 = (float)(ay2 + oy2 + uy * offLen);
                        float qz2 = (float)(az2 + oz2 + uz * offLen);
                        float h = col.Raycast(px2, py2, pz2, qx2, qy2, qz2, true, true);
                        float bh = h;
                        // engine rays: the game's camera mask 0x301 covers terrain
                        // and scene entities, which the baked set cannot fully cover
                        float th = engineRay.RayTerrain(px2, py2, pz2, qx2, qy2, qz2);
                        if (th > 0f && (h <= 0f || th < h)) h = th;
                        float sh = engineRay.RayScene(px2, py2, pz2, qx2, qy2, qz2);
                        if (sh > 0f && (h <= 0f || sh < h)) h = sh;
                        if (h > 0f && (hitDist < 0.0 || h < hitDist))
                        {
                            hitDist = h;
                            hitSrc = string.Format("probe{0} off=({1:F0},{2:F0},{3:F0}) bake={4:F0} terr={5:F0} scene={6:F0}",
                                p, ox2, oy2, oz2, bh, th, sh);
                        }
                        if (obstDbg && h > 0f && h < 700f && now - lastObstLog >= 500)
                            Log(string.Format("obstdbg probe{0} off=({1:F0},{2:F0},{3:F0}) bake={4:F1}(inst={8},tri={9}) terr={5:F1} scene={6:F1} h={7:F1}",
                                p, ox2, oy2, oz2, bh, th, sh, h, col.LastInst, col.LastTri));
                    }
                }
                // engine vertical backend: the game mask's vertical probe.
                // Sampling it along the camera line catches vertical/cliff
                // geometry no horizontal ray reports.
                for (int i = 2; i <= 14; i++)
                {
                    double t = (double)i / 14.0;
                    int vhr;
                    float hv = engineRay.RayVerticalHeight(
                        (float)(ax2 + camOff[0] * t), 10000f, (float)(az2 + camOff[2] * t), 30000f, out vhr);
                    // window: a surface that belongs to a wall/ledge ahead is
                    // near the line; a distant roof overhead (e.g. the user
                    // spot: first surface 9045 u up) is not a wall and used to
                    // fire this ladder everywhere (false pull to ~68 u)
                    double lineY = ay2 + camOff[1] * t;
                    if (hv > 0f && hv + 20.0 > lineY && hv - lineY < 1500.0)
                    {
                        double vh = t * offLen;
                        if (hitDist < 0.0 || vh < hitDist)
                        {
                            hitDist = vh;
                            hitSrc = string.Format("vert i={0} hv={1:F0} t={2:F3}", i, hv, t);
                        }
                        if (obstDbg && now - lastObstLog >= 500) Log(string.Format("obstdbg vert i={0} hv={1:F0} t={2:F3} vh={3:F1}", i, hv, t, vh));
                        break;
                    }
                }
                // terrain read as another obstruction ray (center probe march)
                if (sampler != null)
                {
                    const double margin = 20.0;
                    const int steps = 14;
                    for (int i = 2; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        float g = sampler.Sample((float)(ax2 + camOff[0] * t), (float)(az2 + camOff[2] * t));
                        if (g + margin > ay2 + camOff[1] * t)
                        {
                            double th = t * offLen;
                            if (hitDist < 0.0 || th < hitDist)
                            {
                                hitDist = th;
                                hitSrc = string.Format("samp i={0} g={1:F0} t={2:F3}", i, g, t);
                            }
                            if (obstDbg && now - lastObstLog >= 500) Log(string.Format("obstdbg samp i={0} g={1:F0} t={2:F3} th={3:F1}", i, g, t, th));
                            break;
                        }
                    }
                }
                double camLen = camObst.Update(dt, offLen, hitDist);
                dbgHit = hitDist; dbgLen = camLen; dbgObst = camObst.Obstructed;
                dbgEffDist = dist;
                // look-at experiment: the engine view does not follow the camera
                // position (no managed look-at), so when the pull crosses the
                // anchor the render points away from it. Rotate the engine orbit
                // 180 deg so the view keeps aiming at the anchor (native chase
                // camera semantics) while crossed.
                if (lookPack && !engineSetCam)
                {
                    // safety: the D6 host AV fires when the view turns into
                    // unloaded content while moving, even at the safe rate
                    // (T3: 11 rate-limited flips while walking -> AV). The flip
                    // therefore engages/keeps only while stationary.
                    bool wantFlip = !movingNow &&
                                    (viewFlipped ? camLen < 5.0 : camLen < -2.0);
                    if (wantFlip != viewFlipped)
                    {
                        viewFlipped = wantFlip;
                        flipBusyUntil = now + 5000;
                        flipPxTarget = wantFlip ? -(int)Math.Round(Math.PI / 0.0018) : 0;
                        flipPitchTarget = wantFlip
                            ? (int)Math.Round(2.0 * camSys.Pitch / 0.00121) : 0;
                        flipVerifyPass = 6;
                        Log(string.Format("lookpack flip={0} camLen={1:F1} pitch={2:F3} pxTarget={3} pitchTarget={4}",
                            viewFlipped, camLen, camSys.Pitch, flipPxTarget, flipPitchTarget));
                    }
                }
                if (obstDbg && hitDist > 0.0 && hitDist < 80.0 && now - lastObstLog >= 500)
                {
                    lastObstLog = now;
                    Log(string.Format("obstdbg min={0:F1} src=[{1}] offLen={2:F1} camLen={3:F1}",
                        hitDist, hitSrc, offLen, camLen));
                }

                double s = camLen / offLen;
                double camX = ax2 + camOff[0] * s;
                double camY = ay2 + camOff[1] * s;
                double camZ = az2 + camOff[2] * s;
                aimPitchOverride = double.NaN;
                if (sampler != null)
                {
                    float camGround = sampler.Sample((float)camX, (float)camZ) + 30f;
                    if (camY < camGround)
                    {
                        camY = camGround;
                        // the clamp moved the camera off the orbit line: the
                        // view must aim at the anchor from the clamped point.
                        // Mark the aim dirty so the aim-sync block actually
                        // consumes the override (S3 gap).
                        double gh = Math.Sqrt((camX - ax2) * (camX - ax2) +
                                              (camZ - az2) * (camZ - az2));
                        aimPitchOverride = -Math.Atan2(camY - ay2, Math.Max(1e-3, gh));
                        aimDirty = true;
                    }
                }
                camShake.Update(dt);
                camX += camShake.Offset[0];
                camY += camShake.Offset[1];
                camZ += camShake.Offset[2];
                // final-camera wall gate (T1.5): the camera->anchor segment must
                // be clear; if any wall sits between, retract along that line so
                // the camera can never sit on the far side of geometry
                if (engineRay.Available)
                {
                    float g1 = col != null ? col.Raycast((float)camX, (float)camY, (float)camZ,
                        (float)ax2, (float)ay2, (float)az2, true, true) : -1f;
                    float g2 = engineRay.RayTerrain((float)camX, (float)camY, (float)camZ,
                        (float)ax2, (float)ay2, (float)az2);
                    float g3 = engineRay.RayScene((float)camX, (float)camY, (float)camZ,
                        (float)ax2, (float)ay2, (float)az2);
                    float gg = -1f;
                    if (g1 > 0f && (gg < 0f || g1 < gg)) gg = g1;
                    if (g2 > 0f && (gg < 0f || g2 < gg)) gg = g2;
                    if (g3 > 0f && (gg < 0f || g3 < gg)) gg = g3;
                    double fullLen = Math.Sqrt((camX - ax2) * (camX - ax2) +
                                               (camY - ay2) * (camY - ay2) +
                                               (camZ - az2) * (camZ - az2));
                    if (gg > 0f && fullLen > 1.0 && gg < fullLen - 30.0)
                    {
                        double gs = Math.Max(1.0, gg - 25.0) / fullLen;
                        camX = ax2 + (camX - ax2) * gs;
                        camY = ay2 + (camY - ay2) * gs;
                        camZ = az2 + (camZ - az2) * gs;
                    }
                }
                // bKeepY semantics (measured, M1): false = the host clamps Y
                // up to the engine surface at the camera xz (cliff spot:
                // intent 727 -> 1463); true = keeps the engine's current Y and
                // ignores ours. The managed API cannot force an absolute Y;
                // native camera path is the exit (host deviations register).
                bool usedNativeCam = false;
                if (engineSetCam && CameraShim.EngineCam != IntPtr.Zero)
                {
                    // Direct engine-faithful path: the camera object resolved
                    // through the CLR scene proxy (m_pScene -> vt[+0x50]) gets
                    // the managed IL's own calls - position setter vt[+0x50]
                    // (no managed Y clamp) and look-at setter vt[+0x58] with the
                    // anchor as target. SEH-guarded in the shim.
                    int drc = CameraShim.CamSetVt3(CameraShim.EngineCam,
                        (float)camX, (float)camY, (float)camZ,
                        (float)ax2, (float)ay2, (float)az2);
                    if (drc == 0)
                    {
                        // verify: if the engine camera did not take the position
                        // this is not the camera object - never skip the
                        // managed path on an unverified set (shaking bug)
                        float rx = 0f, ry = 0f, rz = 0f;
                        try { scene.GetCameraPos(ref rx, ref ry, ref rz); } catch { }
                        double rmove = Math.Sqrt((rx - camX) * (rx - camX) +
                                                 (ry - camY) * (ry - camY) +
                                                 (rz - camZ) * (rz - camZ));
                        usedNativeCam = rmove <= 1.0;
                        if (!usedNativeCam)
                        {
                            CameraShim.EngineCam = IntPtr.Zero;   // drop the bad object
                            if (camDebug && now - lastSetLog >= 500)
                            {
                                lastSetLog = now;
                                Log("engineSet direct no effect (moved=" + rmove.ToString("F1") +
                                    "), dropped cam, falling back");
                            }
                        }
                    }
                    if (camDebug && now - lastSetLog >= 500)
                    {
                        lastSetLog = now;
                        Log("engineSet direct rc=" + drc + " native=" + usedNativeCam +
                            " cam=" + CameraShim.ModuleOf(CameraShim.EngineCam));
                    }
                }
                else if (engineSetCam)
                {
                    // Engine-faithful look-at without knowing the target setter:
                    //  - the managed SetCameraPos translates the target by the
                    //    same delta as the position (view direction preserved)
                    //  - the engine position setter (shim RC_CamPosOnly) moves
                    //    the camera WITHOUT touching the target
                    // So: measure the current view direction D and distance to
                    // the anchor k, place the camera at A - D*k via the managed
                    // call (target becomes ~A), then put the camera at the real
                    // orbit position with the native setter -> the view aims at
                    // the anchor from the crossed position (no orbit events).
                    float cvx = 0f, cvy = 0f, cvz = 0f;
                    try { scene.GetCameraPos(ref cvx, ref cvy, ref cvz); } catch { }
                    measureView();
                    double k = Math.Sqrt((ax2 - cvx) * (ax2 - cvx) +
                                         (ay2 - cvy) * (ay2 - cvy) +
                                         (az2 - cvz) * (az2 - cvz));
                    if (k > 1.0 && (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f))
                    {
                        scene.SetCameraPos((float)(ax2 - viewX * k),
                                           (float)(ay2 - viewY * k),
                                           (float)(az2 - viewZ * k), false);
                        int prc = CameraShim.CamPosOnly((float)camX, (float)camY, (float)camZ);
                        if (prc == 0)
                        {
                            float rx = 0f, ry = 0f, rz = 0f;
                            try { scene.GetCameraPos(ref rx, ref ry, ref rz); } catch { }
                            double rmove = Math.Sqrt((rx - camX) * (rx - camX) +
                                                     (ry - camY) * (ry - camY) +
                                                     (rz - camZ) * (rz - camZ));
                            usedNativeCam = rmove <= 1.0;
                            if (camDebug && now - lastSetLog >= 500)
                            {
                                lastSetLog = now;
                                float mrx = 0f, mry = 0f, mrz = 0f;
                                Log(string.Format("enginelook rc=0 moved={0:F1} native={1} k={2:F0}",
                                    rmove, usedNativeCam, k));
                            }
                        }
                        else if (camDebug && now - lastSetLog >= 500)
                        {
                            lastSetLog = now;
                            Log("enginelook camposonly rc=" + prc + ", falling back");
                        }
                    }
                }
                else if (nativeCam && CameraShim.Bound)
                {
                    int brc = CameraShim.CamSet((float)camX, (float)camY, (float)camZ,
                        (float)ax2, (float)ay2, (float)az2, camSetTarget);
                    usedNativeCam = brc == 0;
                    if (!usedNativeCam && camDebug && now - lastSetLog >= 500)
                    {
                        lastSetLog = now;
                        Log("camset native failed rc=" + brc + ", falling back");
                    }
                }
                if (!usedNativeCam)
                {
                    // B7 (experimental, opt-in RC_CAM_SNAPGUARD=1): SetCameraPos
                    // lifts the camera to the render surface at its xz when the
                    // point lies under it. Retract along the anchor line until
                    // the host stops moving the camera; unsatisfiable pits keep
                    // the host position. Off by default - unproven as a default
                    // behaviour (see the deviations register).
                    int snapGuard = 0;
                    while (true)
                    {
                        scene.SetCameraPos((float)camX, (float)camY, (float)camZ, false);
                        if (!camSnapGuard) break;
                        float sx = 0f, sy = 0f, sz = 0f;
                        scene.GetCameraPos(ref sx, ref sy, ref sz);
                        double mv = Math.Sqrt((sx - camX) * (sx - camX) +
                                              (sy - camY) * (sy - camY) +
                                              (sz - camZ) * (sz - camZ));
                        if (mv <= 1.0) break;
                        if (++snapGuard > 4) break;
                        double sl = Math.Sqrt((camX - ax2) * (camX - ax2) +
                                              (camY - ay2) * (camY - ay2) +
                                              (camZ - az2) * (camZ - az2));
                        if (sl < 12.0) break;
                        double next = Math.Max(12.0, sl * 0.6);
                        double rs = next / sl;
                        camX = ax2 + (camX - ax2) * rs;
                        camY = ay2 + (camY - ay2) * rs;
                        camZ = az2 + (camZ - az2) * rs;
                    }
                    if (snapGuard > 0 && camDebug && now - lastSetLog >= 500)
                    {
                        lastSetLog = now;
                        Log(string.Format("snapguard retracts={0} finalLen={1:F0}",
                            snapGuard,
                            Math.Sqrt((camX - ax2) * (camX - ax2) +
                                      (camY - ay2) * (camY - ay2) +
                                      (camZ - az2) * (camZ - az2))));
                    }
                }
                dbgIntX = (float)camX; dbgIntY = (float)camY; dbgIntZ = (float)camZ; dbgIntSet = true;
                preX = (float)camX; preY = (float)camY; preZ = (float)camZ;
                preAX = (float)ax2; preAY = (float)ay2; preAZ = (float)az2; preSet = true;
                if (camDebug && now - lastSetLog >= 500)
                {
                    lastSetLog = now;
                    float sx = 0f, sy = 0f, sz = 0f;
                    scene.GetCameraPos(ref sx, ref sy, ref sz);
                    double sd = Math.Sqrt((sx - camX) * (sx - camX) +
                                          (sy - camY) * (sy - camY) +
                                          (sz - camZ) * (sz - camZ));
                    if (sd > 1.0)
                        Log(string.Format("setdbg moved={0:F1} intended=({1:F0},{2:F0},{3:F0}) actual=({4:F0},{5:F0},{6:F0})",
                            sd, camX, camY, camZ, sx, sy, sz));
                }

                // Character visibility near the camera: the native client relies
                // on view near-plane clipping (value not shipped, see
                // docs/camera/CLOSE_RANGE_RESEARCH.md). The host has no visibility
                // API, so hide the dummy while the REAL camera->anchor distance
                // (after the ground clamp) is inside the character's volume and
                // restore it once clearly outside - conservative radius and
                // hysteresis (host approximation, not a game value).
                double camDist = Math.Sqrt((camX - ax2) * (camX - ax2) +
                                           (camY - ay2) * (camY - ay2) +
                                           (camZ - az2) * (camZ - az2));
                if (hideNear)
                {
                    if (!playerHidden && camDist < 90.0)
                    {
                        playerHidden = true;
                        placePlayer(px, py, pz, curYaw);
                    }
                    else if (playerHidden && camDist > 150.0)
                    {
                        playerHidden = false;
                        placePlayer(px, py, pz, curYaw);
                    }
                }
                }
            }
            catch (Exception e) { Log("camera system ex: " + e.Message); }

            engine.FrameMove();
            // Step C test: write the model's exact placement into a post-process
            // camera record BETWEEN FrameMove and Render (bypasses the clamp)
            if (camPreIdx >= 0 && preSet && CameraShim.Available &&
                CameraShim.ObjectCount() > camPreIdx)
                CameraShim.CamSetIndex(camPreIdx, preX, preY, preZ, preAX, preAY, preAZ);
            engine.Render();
            Application.DoEvents();

            if (camDebug && dbgIntSet && now - lastPostLog >= 500)
            {
                lastPostLog = now;
                float abx = 0f, aby = 0f, abz = 0f;
                scene.GetCameraPos(ref abx, ref aby, ref abz);
                double pd = Math.Sqrt((abx - dbgIntX) * (abx - dbgIntX) +
                                      (aby - dbgIntY) * (aby - dbgIntY) +
                                      (abz - dbgIntZ) * (abz - dbgIntZ));
                // ray guard probe: called AFTER FrameMove/Render (inside the
                // engine frame) - tests whether the engine ray requires that
                float rdx = abx - px, rdy = aby - (py + 90f), rdz = abz - pz;
                float rl = (float)Math.Sqrt(rdx * rdx + rdy * rdy + rdz * rdz);
                float rr = -1f, sl = -1f;
                int slHr = 0, slHit = 0;
                if (rl > 1f)
                {
                    rr = engineRay.RayTerrain(px, py + 90f, pz,
                        px + rdx / rl * 600f, py + 90f + rdy / rl * 600f, pz + rdz / rl * 600f);
                    sl = engineRay.RaySceneLevel(px, py + 90f, pz,
                        px + rdx / rl * 600f, py + 90f + rdy / rl * 600f, pz + rdz / rl * 600f);
                    slHr = engineRay.LastHr; slHit = engineRay.LastHit;
                }
                Log(string.Format("postdbg intended=({0:F0},{1:F0},{2:F0}) actual=({3:F0},{4:F0},{5:F0}) moved={6:F1} rayPost={7:F0}(hr={8},hit={9}) sceneLevel={10:F0}(hr={11},hit={12})",
                    dbgIntX, dbgIntY, dbgIntZ, abx, aby, abz, pd, rr, engineRay.LastHr, engineRay.LastHit,
                    sl, slHr, slHit));
            }

            if ((nativeCam || engineSetCam) && !camBound && now >= 1500 && CameraShim.Available)
            {
                camBound = true;
                float bx = 0f, by = 0f, bz = 0f;
                try { scene.GetCameraPos(ref bx, ref by, ref bz); } catch { }
                int brc = CameraShim.CamBind(bx, by, bz);
                Log(string.Format("camBind rc={0} pos=({1:F1},{2:F1},{3:F1}) {4}",
                    brc, bx, by, bz, CameraShim.CamInfo()));
                if (engineSetCam)
                {
                    Log("camObjDump: " + CameraShim.DumpObj(CameraShim.CamObject()));
                    Log("slot50: " + CameraShim.SlotBytes(0x50, 24));
                    Log("slot58: " + CameraShim.SlotBytes(0x58, 24));
                    Log("slot60: " + CameraShim.SlotBytes(0x60, 24));
                    Log("slot68: " + CameraShim.SlotBytes(0x68, 24));
                }
            }
            if (camDiff2 && !camDiff2Done && now >= 1200 && CameraShim.Available)
            {
                camDiff2Done = true;
                string dp = Env("RC_CAM_DIFF2_POS", "");
                string[] dpv = dp.Split(',');
                float dux = 0f, duz = 0f;
                if (dpv.Length == 2)
                {
                    float.TryParse(dpv[0], out dux);
                    float.TryParse(dpv[1], out duz);
                }
                else
                {
                    float gx = 0f, gy = 0f, gz = 0f;
                    try { scene.GetCameraPos(ref gx, ref gy, ref gz); } catch { }
                    dux = gx; duz = gz;
                }
                Log(string.Format("diff2 scan({0:F1},{1:F1}): {2}", dux, duz,
                    CameraShim.FindAll(dux, duz)));
                for (int i = 0; i < CameraShim.ObjectCount() && i < 8; i++)
                {
                    IntPtr o = CameraShim.Object(i);
                    int off = CameraShim.ObjectOff(i);
                    if (off < 0) continue;
                    uint d0 = (uint)Math.Max(0, off - 0x40);
                    for (uint d = d0; d <= off + 0x60; d += 0x40)
                        Log(string.Format("diff2 dump {0}@{1:X} off=0x{2:X} +0x{3:X}: {4}",
                            CameraShim.ObjectClass(i), o.ToInt64(), off, d,
                            CameraShim.DumpF(o, d, 16)));
                }
            }

            if (Env("RC_CAM_FOVDIFF", "0") == "1" && !camScanDone && now >= 1500 && CameraShim.Available)
            {
                camScanDone = true;
                CameraShim.Deep(0x20000);
                IntPtr sv = IntPtr.Zero, rc = IntPtr.Zero;
                for (int i = 0; i < CameraShim.ObjectCount(); i++)
                {
                    string cl = CameraShim.ObjectClass(i);
                    if (cl == "SceneView" && sv == IntPtr.Zero) sv = CameraShim.Object(i);
                    if (cl == "Camera" && rc == IntPtr.Zero) rc = CameraShim.Object(i);
                }
                float f0 = scene.GetViewAngleFactor();
                Log(string.Format("fovdiff objects sv={0:X} cam={1:X} factor={2:F3}",
                    sv.ToInt64(), rc.ToInt64(), f0));
                if (sv != IntPtr.Zero)
                {
                    CameraShim.Snap(sv, 0x800);
                    scene.SetViewAngleFactor(f0 * 0.8f);
                    Log("fovdiff sv f0->0.8f0: " + CameraShim.SnapDiff());
                }
                if (rc != IntPtr.Zero)
                {
                    CameraShim.Snap(rc, 0x800);
                    scene.SetViewAngleFactor(f0);
                    Log("fovdiff cam 0.8f0->f0: " + CameraShim.SnapDiff());
                }
                scene.SetViewAngleFactor(f0);
            }

            // P2 guarded protocol test: rotate the engine view at a controlled
            // rate (rad/s) with small orbit steps; the instant 600 px jump
            // crashed in the shader parser (D6), this measures whether a slow
            // rotation lets the host's content loading keep up
            if (yawSpeedTest > 0f && now >= 2000 && now < 12000)
            {
                if (yawTestLast == 0) yawTestLast = now;
                double dt2 = (now - yawTestLast) / 1000.0;
                if (dt2 < 0.005) dt2 = 0.005;
                int pxYaw = (int)Math.Round(yawSpeedTest * dt2 / 0.0018);
                if (pxYaw > 20) pxYaw = 20;
                if (pxYaw < 1) pxYaw = 1;
                pxYaw = -pxYaw;
                scene.ExecAction(30, 1, 0, makeLParam(lockCenter.X, lockCenter.Y));
                scene.ExecAction(1, 1, 0, makeLParam(lockCenter.X + pxYaw, lockCenter.Y));
                yawTestAccum += -pxYaw * 0.0018;
                yawTestLast = now;
                if (now - yawTestLog >= 2000)
                {
                    yawTestLog = now;
                    Log(string.Format("yawspeed t={0} rad={1:F2}", now, yawTestAccum));
                }
            }

            // yaw/pitch diff probe (P2 groundwork): rotate the engine view by a
            // known orbit delta and diff the SceneView/Camera objects to find
            // where the engine stores the view angles. State: 0 wait, 1 armed
            // (sent, waiting for the engine frame), 2 done.
            if (yawDiffPx != 0 && CameraShim.Available)
            {
                if (yawDiffState == 0 && now >= 1500)
                {
                    yawDiffState = 1;
                    CameraShim.Deep(0x20000);
                    for (int i = 0; i < CameraShim.ObjectCount(); i++)
                    {
                        string cl = CameraShim.ObjectClass(i);
                        if (cl == "SceneView" && yawDiffObj == IntPtr.Zero) yawDiffObj = CameraShim.Object(i);
                    }
                    if (yawDiffObj != IntPtr.Zero) CameraShim.Snap(yawDiffObj, 0x800);
                    Log(string.Format("yawdiff armed sv={0:X} targetPx={1}", yawDiffObj.ToInt64(), yawDiffPx));
                }
                else if (yawDiffState == 1)
                {
                    if (yawDiffDone != yawDiffPx)
                    {
                        // rate-limited delivery (T1): instant delta AVed at
                        // +0xA6C75A; <=20 px/event, ~1.5 rad/s is the safe rate
                        int maxStep = (int)Math.Round(1.5 * dt / 0.0018);
                        if (maxStep < 1) maxStep = 1;
                        if (maxStep > 20) maxStep = 20;
                        int step = Math.Sign(yawDiffPx - yawDiffDone) *
                                   Math.Min(Math.Abs(yawDiffPx - yawDiffDone), maxStep);
                        yawDiffDone += step;
                        scene.ExecAction(30, 1, 0, makeLParam(lockCenter.X, lockCenter.Y));
                        scene.ExecAction(1, 1, 0, makeLParam(lockCenter.X + step, lockCenter.Y));
                    }
                    else
                    {
                        yawDiffSent = now;
                        yawDiffState = 2;
                    }
                }
                else if (yawDiffState == 2 && now >= yawDiffSent + 400)
                {
                    yawDiffState = 3;
                    if (yawDiffObj != IntPtr.Zero)
                        Log("yawdiff sv: " + CameraShim.SnapDiff());
                }
            }

            if (camScan && !camScanDone && now >= 2500 && CameraShim.Available)
            {
                camScanDone = true;
                Log("camscan: " + CameraShim.FindObjects(0x20000));
                Log("camscan-deep: " + CameraShim.Deep(0x20000));
                float scx = 0f, scy = 0f, scz = 0f;
                try { scene.GetCameraPos(ref scx, ref scy, ref scz); } catch { }
                Log(string.Format("camscan-pos=({0:F2},{1:F2},{2:F2}) objects={3}",
                    scx, scy, scz, CameraShim.ObjectCount()));
                for (int i = 0; i < CameraShim.ObjectCount() && i < 32; i++)
                {
                    IntPtr o = CameraShim.Object(i);
                    string cls = CameraShim.ObjectClass(i);
                    string tri = CameraShim.FindTriple(o, 0x2000, scx, scy, scz);
                    Log(string.Format("camscan-obj {0} {1} @{2:X} {3}", i, cls, o.ToInt64(), tri));
                    if (cls == "SceneView")
                    {
                        for (uint off = 0; off <= 0x700; off += 0x40)
                            Log(string.Format("camscan-q {0}+0x{1:X}: {2}",
                                cls, off, CameraShim.DumpQ(o, off)));
                        for (uint off = 0; off <= 0x700; off += 0x40)
                            Log(string.Format("camscan-f {0}+0x{1:X}: {2}",
                                cls, off, CameraShim.DumpF(o, off, 16)));
                    }
                    else if (cls == "Camera" &&
                             tri.IndexOf("none", StringComparison.Ordinal) < 0)
                    {
                        for (uint off = 0; off <= 0x400; off += 0x40)
                            Log(string.Format("camscan-f {0}+0x{1:X}: {2}",
                                cls, off, CameraShim.DumpF(o, off, 16)));
                    }
                }
                string poke = Env("RC_CAM_POKE", "");
                if (poke.Length > 0)
                {
                    foreach (string spec in poke.Split(';'))
                    {
                        string[] pp = spec.Split(':');
                        string[] kv = pp.Length == 2 ? pp[1].Split('=') : new string[0];
                        uint poff; float pv;
                        if (kv.Length != 2 ||
                            !uint.TryParse(kv[0], System.Globalization.NumberStyles.HexNumber,
                                           System.Globalization.CultureInfo.InvariantCulture, out poff) ||
                            !float.TryParse(kv[1], System.Globalization.NumberStyles.Float,
                                            System.Globalization.CultureInfo.InvariantCulture, out pv))
                        {
                            Log("campoke: bad format '" + spec + "' (want Class:hexoff=value)");
                            continue;
                        }
                        for (int i = 0; i < CameraShim.ObjectCount(); i++)
                        {
                            if (CameraShim.ObjectClass(i) != pp[0]) continue;
                            IntPtr o = CameraShim.Object(i);
                            int rc = CameraShim.WriteF(o, poff, pv);
                            Log(string.Format("campoke {0}@{1:X}+0x{2:X}={3} rc={4} now: {5}",
                                pp[0], o.ToInt64(), poff, pv, rc, CameraShim.DumpF(o, poff, 4)));
                        }
                    }
                }
                // per-frame re-apply (the renderer may rewrite the field)
                pokeSpecs = poke.Length > 0 ? poke.Split(';') : null;
            }
            if (pokeSpecs != null && !camPokeOnce && now >= 2400 && CameraShim.Available)
            {
                for (int s = 0; s < pokeSpecs.Length; s++)
                {
                    string[] pp = pokeSpecs[s].Split(':');
                    string[] kv = pp.Length == 2 ? pp[1].Split('=') : new string[0];
                    uint poff; float pv;
                    if (kv.Length != 2 ||
                        !uint.TryParse(kv[0], System.Globalization.NumberStyles.HexNumber,
                                       System.Globalization.CultureInfo.InvariantCulture, out poff) ||
                        !float.TryParse(kv[1], System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out pv)) continue;
                    for (int i = 0; i < CameraShim.ObjectCount(); i++)
                        if (CameraShim.ObjectClass(i) == pp[0])
                            CameraShim.WriteF(CameraShim.Object(i), poff, pv);
                }
            }

            if (now - lastHud >= 250)
            {
                lastHud = now;
                string state = skillUntil > now ? "SKILL" : !grounded ? (vy > 0f ? "JUMP" : "FALL")
                             : moving ? (shiftDown ? "RUN x10" : walkMode ? "WALK" : wSprint ? "SPRINT" : "RUN") : "IDLE";
                float moveSpeed = shiftDown ? pRun * 10f
                                : walkMode ? pSpeed
                                : wSprint ? pSprint
                                : pRun;
                hud.Text = string.Format(
                    "JX3\nfps {0}\npos {1:F0},{2:F0},{3:F0}\nstate {4}{5} hits {6}\nspeed {7:F1} \u5C3A/s\ncam {8} yaw {9:F2} dist {10:F0}\nclip {11}\nWASD move | Wx2 hold sprint | / walk-run | Shift 10x | Space jump | 1 skill | C teleport\nLMB drag = camera | RMB drag = camera+turn | wheel zoom | F11 reset | Home/End view (Esc unlock)",
                    fps, px, py, pz, state, blocked ? " (blocked)" : "", blockedEvents,
                    moving ? moveSpeed / 64f : 0f,
                    camSys.Mode, camSys.Yaw, camSys.Distance,
                    curClip == null ? "-" : Path.GetFileName(curClip));
            }
            if (now - lastLog >= 2000)
            {
                lastLog = now;
                string nearInfo = "";
                if (colDebug && col != null)
                {
                    float nx, ny, nz;
                    float nd = col.NearestInstance(px, pz, out nx, out ny, out nz);
                    var cand = new System.Collections.Generic.List<int>();
                    col.GatherCandidates(px, pz, 800f, cand);
                    nearInfo = string.Format(" near={0:F0} cand={1}", nd, cand.Count);
                    for (int ci = 0; ci < cand.Count && ci < 3; ci++)
                    {
                        float ax, ay, az, bx, by, bz;
                        if (col.GetInstanceBounds(cand[ci], out ax, out ay, out az, out bx, out by, out bz))
                            nearInfo += string.Format(" | i{0} AABB({1:F0},{2:F0},{3:F0})-({4:F0},{5:F0},{6:F0})",
                                cand[ci], ax, ay, az, bx, by, bz);
                    }
                    nearInfo += string.Format(" py={0:F0}", py);
                }
                float curSpd = !moving ? 0f
                             : shiftDown ? pRun * 10f
                             : walkMode ? pSpeed
                             : wSprint ? pSprint
                             : pRun;
                string moveMode = !moving ? "IDLE"
                                : shiftDown ? "RUN10"
                                : walkMode ? "WALK"
                                : wSprint ? "SPRINT"
                                : "RUN";
                Log(string.Format("t={0}s fps={1} pos=({2:F0},{3:F0},{4:F0}) vy={5:F0} grounded={6} blocked={7} hits={8} colCalls={9} colBlocked={10} spd={13:F0}u/s({14}){11} clip={12}",
                    now / 1000, fps, px, py, pz, vy, grounded, blocked, blockedEvents,
                    colCalls, colBlockedCalls, nearInfo,
                    curClip == null ? "-" : Path.GetFileName(curClip),
                    curSpd, moveMode));
            }
            if (f9At > 0 && !f9Fired && now >= f9At)
            {
                f9Fired = true;
                userShot = true;
            }
            if (userShot)
            {
                userShot = false;
                try
                {
                    string png = Path.Combine(outDir,
                        string.Format("rc_user_{0}ms.png", now));
                    scene.SetScreenShot(png, 2);
                    scene.DoScreenShotImmediate();
                    Log("USERREPRO shot -> " + png);
                }
                catch (Exception e) { Log("USERREPRO shot ex: " + e.Message); }
                forceDiag = true;
                lastCamLog = 0;
                lastPostLog = 0;
            }

            while (shotIdx < shots.Length && now >= shots[shotIdx])
            {
                try
                {
                    string png = Path.Combine(outDir, string.Format("rc_{0:D2}_{1}ms.png", shotIdx, shots[shotIdx]));
                    scene.SetScreenShot(png, 2);
                    scene.DoScreenShotImmediate();
                    Log("shot -> " + png);
                }
                catch (Exception e) { Log("shot ex: " + e.Message); }
                shotIdx++;
            }
            if (autoRunMs > 0 && now >= autoRunMs) break;
        }
        Log("DONE");
    }

    static string Env(string name, string def)
    {
        string v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    static double WrapAngle(double angle)
    {
        while (angle > Math.PI) angle -= 2.0 * Math.PI;
        while (angle < -Math.PI) angle += 2.0 * Math.PI;
        return angle;
    }

    static long[] ParseShots(string s)
    {
        if (string.IsNullOrEmpty(s)) return new long[0];
        string[] parts = s.Split(',');
        var list = new System.Collections.Generic.List<long>();
        foreach (string p in parts)
        {
            long v;
            if (long.TryParse(p.Trim(), out v) && v > 0) list.Add(v);
        }
        return list.ToArray();
    }

    static void Pump(KGEngineCLR engine, int ms)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            engine.FrameMove();
            engine.Render();
            Application.DoEvents();
            Thread.Sleep(16);
        }
    }
}
