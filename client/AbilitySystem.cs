// AbilitySystem.cs - dataset-driven ability casting for the reborn client.
//
// Ported from the MovieEditor ability sandbox (v5, 2026-10-06): every ability's
// authored process (anim / sound / effect) comes from
// bin64\ability_picker\ability_candidates.json. The matched .tani carries the
// animation AND its embedded tag records, so playing the tani lets the engine's
// own tag manager spawn/render the authored .Sfx effects (verified on the ME
// host; the client uses the same engine build).
//
// P = picker panel, 1 = cast the selected ability, click an icon = select+cast.
// C# 5 (csc Framework64 v4.0.30319).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using MovieEngineCLR;

internal static class AbilitySystem
{
    internal class ProcStep
    {
        public int T;
        public int Dur;   // authored duration in ms (anim length / effect life)
        public string Kind = "";
        public string V = "";
        public string N = "";
    }

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    const uint SND_ASYNC = 0x0001;
    const uint SND_NODEFAULT = 0x0002;
    const uint SND_FILENAME = 0x00020000;

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr LoadLibraryA(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr mod, string name);
    delegate int SfxPlayFn(string path, float x, float y, float z);
    static bool warmed = false;

    static KGSceneCLR scene;
    static Action<string> log;
    static Func<string, int> playClip;
    static Form form;
    static string dataDir = "";
    static string soundDir = "";
    static readonly List<string> names = new List<string>();
    static readonly Dictionary<string, List<ProcStep>> procByName = new Dictionary<string, List<ProcStep>>();
    static readonly Dictionary<string, List<string>> tanisByName = new Dictionary<string, List<string>>();
    static readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();
    static readonly List<PictureBox> icons = new List<PictureBox>();
    static Panel panel;
    static string sel = "";
    static bool castReq = false;
    static bool active = false;
    static long startMs = 0, untilMs = 0, animUntil = 0;
    static long cooldownUntil = 0;   // rapid re-casts AV the engine tag manager
    static int lastFormW = 0, lastFormH = 0;
    static int stepIdx = 0;
    static bool pss = false;
    static string pssPath = "";
    static float lastX = 1e9f, lastZ = 1e9f;
    static string castName = "";
    static bool soundOn = true;

    public static string Selected { get { return sel; } }
    public static bool Active { get { return active; } }

    // the client's state-clip machine must not override the ability animation
    // while it plays (otherwise the cast shows only sound, no motion/effects)
    public static bool AnimActiveAt(long now) { return active && now < animUntil; }

    public static void Init(KGSceneCLR sceneIn, Action<string> logIn, Func<string, int> playClipIn,
                            Form formIn, string startupDir)
    {
        scene = sceneIn; log = logIn; playClip = playClipIn; form = formIn;
        dataDir = Path.Combine(startupDir, "ability_picker");
        soundDir = Path.Combine(dataDir, "sound");
        LoadDataset();
        BuildPanel();
        if (names.Count > 0) sel = names[0];
        string pre = Environment.GetEnvironmentVariable("RC_ABILITY");
        if (pre != null && pre.Length > 0 && names.Contains(pre)) sel = pre;
        log("abilities: " + names.Count + " loaded (P panel, 1 casts) selected=" + sel);
    }

    static void LoadDataset()
    {
        names.Clear(); procByName.Clear(); tanisByName.Clear();
        try
        {
            string p = Path.Combine(dataDir, "ability_candidates.json");
            if (!File.Exists(p)) { log("abilities: dataset missing " + p); return; }
            var ser = new JavaScriptSerializer();
            var root = ser.DeserializeObject(File.ReadAllText(p, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("abilities")) return;
            foreach (object o in (object[])root["abilities"])
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                string nm = Str(d, "name");
                if (nm == "") continue;
                object pv;
                if (!d.TryGetValue("process", out pv) || !(pv is object[]) || ((object[])pv).Length == 0) continue;
                string mt = Str(d, "matched");
                if (mt == "") continue;                 // unresolved duplicate row
                if (procByName.ContainsKey(nm)) continue;   // first row wins
                var steps = new List<ProcStep>();
                foreach (object po in (object[])pv)
                {
                    var pd = po as Dictionary<string, object>;
                    if (pd == null) continue;
                    var st = new ProcStep();
                    int ti = 0; int.TryParse(Str(pd, "t"), out ti); st.T = ti;
                    st.Kind = Str(pd, "kind"); st.V = Str(pd, "v"); st.N = Str(pd, "n");
                    int dm = 0; int.TryParse(Str(pd, "durMs"), out dm); st.Dur = dm;
                    steps.Add(st);
                }
                if (steps.Count == 0) continue;
                procByName[nm] = steps;
                var tl = new List<string>();
                object tv;
                if (d.TryGetValue("tanis", out tv) && tv is object[])
                    foreach (object t in (object[])tv) if (t != null) tl.Add(t.ToString());
                tanisByName[nm] = tl;
                names.Add(nm);
            }
        }
        catch (Exception e) { log("abilities: dataset ex " + e.Message); }
    }

    static string Str(Dictionary<string, object> d, string k)
    {
        object v;
        return (d.TryGetValue(k, out v) && v != null) ? v.ToString() : "";
    }

    static string ResolveAnim(string name, string v)
    {
        if (v.IndexOf('\\') >= 0) return v;
        List<string> tl;
        if (tanisByName.TryGetValue(name, out tl))
            foreach (string t in tl)
                if (t.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0) return t;
        return v;
    }

    static Image IconFor(string name)
    {
        Image img;
        if (iconCache.TryGetValue(name, out img)) return img;
        img = null;
        try
        {
            string iconDir = Path.Combine(dataDir, "icons");
            string dataPath = Path.Combine(dataDir, "skill_data.json");
            if (File.Exists(dataPath))
            {
                var ser = new JavaScriptSerializer();
                var root = ser.DeserializeObject(File.ReadAllText(dataPath, Encoding.UTF8)) as Dictionary<string, object>;
                object abilObj;
                if (root != null && root.TryGetValue("abilities", out abilObj))
                {
                    var abil = abilObj as Dictionary<string, object>;
                    object dv;
                    if (abil != null && abil.TryGetValue(name, out dv))
                    {
                        var dd = dv as Dictionary<string, object>;
                        string png = dd != null ? Str(dd, "iconPng") : "";
                        if (png != "")
                        {
                            string p = Path.Combine(iconDir, png);
                            if (File.Exists(p))
                            {
                                using (var fs = File.OpenRead(p))
                                using (var tmp = Image.FromStream(fs))
                                    img = new Bitmap(tmp);
                            }
                        }
                    }
                }
            }
        }
        catch { img = null; }
        iconCache[name] = img;
        return img;
    }

    static void BuildPanel()
    {
        panel = new Panel();
        panel.Size = new Size(260, 420);
        panel.BackColor = Color.FromArgb(210, 0, 0, 0);
        panel.Visible = true;   // visible by default; P toggles
        var grid = new FlowLayoutPanel();
        grid.Location = new Point(6, 6);
        grid.Size = new Size(248, 378);
        grid.AutoScroll = true;
        grid.BackColor = Color.FromArgb(12, 12, 12);
        grid.FlowDirection = FlowDirection.LeftToRight;
        grid.WrapContents = true;
        var tip = new ToolTip();
        tip.InitialDelay = 200; tip.ReshowDelay = 100; tip.AutoPopDelay = 20000;
        for (int i = 0; i < names.Count; i++)
        {
            string nm = names[i];
            var pb = new PictureBox();
            pb.Size = new Size(32, 32);
            pb.Margin = new Padding(1);
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.Cursor = Cursors.Hand;
            pb.BackColor = Color.FromArgb(24, 24, 24);
            Image img = IconFor(nm);
            if (img != null) pb.Image = img;
            tip.SetToolTip(pb, nm);
            pb.Click += delegate
            {
                sel = nm;
                for (int k = 0; k < icons.Count; k++)
                    icons[k].BorderStyle = (icons[k] == pb) ? BorderStyle.FixedSingle : BorderStyle.None;
                castReq = true;
                log("ability click-cast: " + nm);
            };
            grid.Controls.Add(pb);
            icons.Add(pb);
        }
        panel.Controls.Add(grid);
        var soundBox = new CheckBox();
        soundBox.Text = "sound";
        soundBox.ForeColor = Color.White;
        soundBox.Checked = true;
        soundBox.Location = new Point(6, 392);
        soundBox.AutoSize = true;
        soundBox.CheckedChanged += delegate { soundOn = soundBox.Checked; };
        panel.Controls.Add(soundBox);
        form.Controls.Add(panel);
        Action place = delegate
        {
            panel.Location = new Point(Math.Max(0, form.ClientSize.Width - 272), 36);
        };
        form.Resize += delegate { place(); };
        place();
        if (panel.Visible) panel.BringToFront();
    }

    public static void Toggle()
    {
        if (panel == null) return;
        panel.Visible = !panel.Visible;
        if (panel.Visible) panel.BringToFront();
        log("abilities: panel " + (panel.Visible ? "open" : "closed") + " selected=" + sel);
    }

    public static void RequestCast() { castReq = true; }

    public static void SelectNext(int dir)
    {
        if (names.Count == 0) return;
        int i = names.IndexOf(sel);
        i = (i + dir + names.Count) % names.Count;
        sel = names[i];
        log("ability select: " + sel);
    }

    static void WarmUp(float px, float py, float pz)
    {
        try
        {
            string sfxDir = Path.Combine(dataDir, "sfx");
            if (!Directory.Exists(sfxDir)) { log("sfx warm: no sfx dir"); return; }
            IntPtr mod = LoadLibraryA(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sfx_shim.dll"));
            if (mod == IntPtr.Zero) mod = LoadLibraryA("sfx_shim.dll");
            if (mod == IntPtr.Zero) { log("sfx warm: sfx_shim.dll missing"); return; }
            IntPtr fp = GetProcAddress(mod, "RC_Shim_SfxPlay");
            if (fp == IntPtr.Zero) { log("sfx warm: RC_Shim_SfxPlay missing"); return; }
            SfxPlayFn play = (SfxPlayFn)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(
                fp, typeof(SfxPlayFn));
            string[] files = Directory.GetFiles(sfxDir, "*.sfx");
            int ok = 0;
            foreach (string f in files)
                if (play(f, px + 20000f, py - 2000f, pz + 20000f) == 0) ok++;
            log("sfx warm: " + ok + "/" + files.Length + " cached (far position)");
        }
        catch (Exception e) { log("sfx warm ex: " + e.Message); }
    }

    static void StartCast(long now, float px, float py, float pz, float yaw)
    {
        List<ProcStep> steps;
        if (!procByName.TryGetValue(sel, out steps) || steps.Count == 0) return;
        active = true; startMs = now; stepIdx = 0; pss = false; pssPath = "";
        lastX = 1e9f; lastZ = 1e9f; castName = sel;
        int animMs = 1000, pssMs = 3000;
        foreach (ProcStep s in steps)
        {
            if (s.Kind == "anim" && s.Dur > 0) animMs = s.Dur;
            if (s.Kind == "dummy" && s.Dur > 0) pssMs = s.Dur;
        }
        untilMs = now + pssMs + 120;
        animUntil = now + animMs + 150;
        cooldownUntil = untilMs + 2000;
        try { scene.RemoveDummyModel("cast_pss"); } catch { }
        log("cast: " + sel + " steps=" + steps.Count + " animMs=" + animMs + " pssMs=" + pssMs);
    }

    public static void Tick(long now, float px, float py, float pz, float yaw)
    {
        // one-time .Sfx warm-up (the engine AVs the FIRST create of ~27 of the
        // staged tags once the scene has settled; creating them once far from
        // the player caches the resources so cast-time spawns succeed - the
        // proven ME-sandbox recipe, via the shared sfx_shim.dll)
        if (!warmed)
        {
            warmed = true;
            WarmUp(px, py, pz);
        }
        // the picker panel follows window resizes / fullscreen (form.Resize
        // alone misses the maximized path on some hosts)
        if (panel != null && form != null &&
            (form.ClientSize.Width != lastFormW || form.ClientSize.Height != lastFormH))
        {
            lastFormW = form.ClientSize.Width; lastFormH = form.ClientSize.Height;
            panel.Location = new Point(Math.Max(0, lastFormW - 272), 36);
            if (panel.Visible) panel.BringToFront();
        }
        if (castReq)
        {
            castReq = false;
            // guard: a cast while the previous effect is still running (or in
            // the short cooldown) AVs the engine's tag manager - drop it
            if (active || now < cooldownUntil)
            {
                log("cast blocked: " + (active ? "effect still playing" : "cooldown")
                    + " (" + sel + ")");
            }
            else StartCast(now, px, py, pz, yaw);
        }
        if (!active) return;
        long rel = now - startMs;
        List<ProcStep> steps;
        if (!procByName.TryGetValue(castName, out steps)) { active = false; return; }
        while (stepIdx < steps.Count && steps[stepIdx].T <= rel)
        {
            ProcStep st = steps[stepIdx];
            stepIdx++;
            try
            {
                if (st.Kind == "anim")
                {
                    string path = ResolveAnim(castName, st.V);
                    int pr = playClip(path);
                    log("cast anim -> " + st.V + " = " + path + " (" + pr + ")");
                }
                else if (st.Kind == "sound")
                {
                    if (soundOn)
                    {
                        string wav = Path.Combine(soundDir, st.V + ".wav");
                        if (File.Exists(wav)) PlaySound(wav, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
                    }
                    log("cast sound -> " + st.V);
                }
                else if (st.Kind == "dummy")
                {
                    pss = true; pssPath = st.V;
                    log("cast dummy -> " + st.V);
                }
            }
            catch (Exception e) { log("cast step ex (" + st.Kind + "): " + e.Message); }
        }
        // PSS dummy: spawn + throttled follow (the sandbox recipe; the engine
        // reuses the handle, so a re-add restarts the effect at the caster)
        if (pss && pssPath.Length > 0)
        {
            if (lastX > 1e8f)
            {
                lastX = px; lastZ = pz;
                var pp = new CLRfloat3(); pp.x = px; pp.y = py + 2f; pp.z = pz;
                float half = yaw * 0.5f;
                var pr = new CLRfloat4(); pr.y = (float)Math.Sin(half); pr.w = (float)Math.Cos(half);
                var ps = new CLRfloat3(); ps.x = 1f; ps.y = 1f; ps.z = 1f;
                long h = scene.AddDummyModel("cast_pss", pssPath, pp, pr, ps);
                log("cast pss -> " + pssPath + " handle=" + h + " (follows caster)");
            }
            else if (Math.Abs(px - lastX) > 32f || Math.Abs(pz - lastZ) > 32f)
            {
                lastX = px; lastZ = pz;
                var pp = new CLRfloat3(); pp.x = px; pp.y = py + 2f; pp.z = pz;
                float half = yaw * 0.5f;
                var pr = new CLRfloat4(); pr.y = (float)Math.Sin(half); pr.w = (float)Math.Cos(half);
                var ps = new CLRfloat3(); ps.x = 1f; ps.y = 1f; ps.z = 1f;
                long h = scene.AddDummyModel("cast_pss", pssPath, pp, pr, ps);
                log("cast pss re-added handle=" + h + " (effect restarted)");
            }
        }
        if (now >= untilMs)
        {
            active = false;
            if (pss)
            {
                pss = false;
                try { scene.RemoveDummyModel("cast_pss"); } catch { }
            }
            log("cast done: " + castName);
        }
    }
}
