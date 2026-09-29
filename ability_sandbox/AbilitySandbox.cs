// AbilitySandbox - playable ability sandbox on the MovieEditor engine host.
// Loads a real JX3 map (龙门寻宝/龙门绝境 area), spawns the player, and runs an
// ability's cast process (animation + effects + device models + movement +
// sounds) when the user clicks it. Shares the ability dataset with the picker.
//
// Build: ability_sandbox\build.cmd      Run: bin64\ability_sandbox.exe (cwd = editor root)
//
// Env:
//   AS_DATA=<json>    ability_candidates.json (default: <exe>\ability_picker\ability_candidates.json)
//   AS_SOUND_DIR=<d>  decoded wav dir (default: <exe>\ability_picker\sound)
//   AS_OUT=<dir>      logs + screenshots (default: <exe>\ability_sandbox\out)
//   AS_MAP=<name|path> map to load (default: 龙门寻宝)
//   AS_SMOKE=1        auto-cast AS_CAST, screenshot, exit
//   AS_CAST=<ability> ability to auto-cast (smoke)
//   AS_SHOT_MS=<ms>   smoke screenshot time (default 3200)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using MovieEngineCLR;
using MovieEditor.ActorEditor;

internal static class AbilitySandbox
{
    // ---------- model ----------
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

    internal class Ability
    {
        public string Key;
        public string Name;
        public List<string> Ids = new List<string>();
        public List<string> Tanis = new List<string>();
        public List<string> Wems = new List<string>();
        public string Matched = "";
        public bool Ip;
        public string IpNote = "";
        public bool NoAnim;
        public string Mech = "";
        public List<ProcStep> Process = new List<ProcStep>();
    }

    static readonly List<Ability> abilities = new List<Ability>();
    static readonly Queue<int[]> pending = new Queue<int[]>();

    static string editorRoot = @"C:\SeasunGame\MovieEditor";
    static string startupPath;
    static string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
    static string actorPath;
    static string dataPath, outDir, soundDir;
    static string mapName;
    static Action<string> Log;

    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    const uint SND_ASYNC = 0x0001;
    const uint SND_NODEFAULT = 0x0002;
    const uint SND_FILENAME = 0x00020000;

    static Form form;
    static Panel viewport;
    static ListBox abilityList;
    static CheckBox soundChk;
    static Label statusLabel, curLabel;
    static Timer frameTimer;

    static KGEngineCLR engine;
    static KGSceneCLR scene;
    static KGModelCLR model;
    static long actorHandle;

    static string curClip = "";
    static string lastPlayPath = "";
    static long lastPlayStart;
    static float speed = 1f;
    static Ability selected;
    static bool smoke;
    static string smokeCast = "";
    static int smokeShotMs = 3200;
    static long smokeStart;
    static bool smokeCastDone, smokeShotDone;

    // ---------- camera (ported from the client's later camara-fix branch) ----------
    static CameraSystem camSys;
    static CameraSettings cameraSettings;
    static double viewX, viewY, viewZ;
    static Point lockCenter;
    static bool mouseLocked, lmbDown, rmbDown, dragArmed;
    static Point pressPoint;
    static readonly Queue<int[]> orbitQueue = new Queue<int[]>();
    static float anchorX, anchorY, anchorZ;
    static bool anchorSet;
    static long lastAlignMs, lastCamMs;

    static int MakeLParam(int x, int y) { return ((y & 0xFFFF) << 16) | (x & 0xFFFF); }
    static double WrapAngle(double a) { while (a > Math.PI) a -= Math.PI * 2; while (a < -Math.PI) a += Math.PI * 2; return a; }

    static void MeasureView()
    {
        try
        {
            float ax = 0f, ay = 0f, az = 0f;
            scene.GetCameraPos(ref ax, ref ay, ref az);
            scene.SetCamareMoveState(1, 1);
            for (int i = 0; i < 3; i++) { engine.FrameMove(); Application.DoEvents(); }
            scene.SetCamareMoveState(1, 0);
            float bx = 0f, by = 0f, bz = 0f;
            scene.GetCameraPos(ref bx, ref by, ref bz);
            scene.SetCameraPos(ax, ay, az, false);
            float dx = bx - ax, dy = by - ay, dz = bz - az;
            float dl = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dl > 0.5f) { viewX = dx / dl; viewY = dy / dl; viewZ = dz / dl; }
        }
        catch { }
    }

    static void AlignEngineCamera(double targetYaw, double targetPitch)
    {
        for (int pass = 0; pass < 6; pass++)
        {
            MeasureView();
            double currentYaw = Math.Atan2(-viewZ, -viewX);
            double currentPitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, viewY)));
            double yawDelta = WrapAngle(targetYaw - currentYaw);
            double pitchDelta = targetPitch - currentPitch;
            if (Math.Abs(yawDelta) < 0.02 && Math.Abs(pitchDelta) < 0.02) break;
            int dx2 = (int)Math.Round(-yawDelta / 0.0018);
            int dy2 = (int)Math.Round(-pitchDelta / 0.00121);
            if (dx2 > 300) dx2 = 300; if (dx2 < -300) dx2 = -300;
            if (dy2 > 300) dy2 = 300; if (dy2 < -300) dy2 = -300;
            scene.ExecAction(30, 1, 0, MakeLParam(lockCenter.X, lockCenter.Y));
            scene.ExecAction(1, 1, 0, MakeLParam(lockCenter.X + dx2, lockCenter.Y + dy2));
            engine.FrameMove();
        }
    }

    static double GeometricAimPitch()
    {
        double h = camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter;
        double d = Math.Max(1.0, camSys.Distance * cameraSettings.EyeScale);
        double[] off = new double[3];
        CameraSystem.DesiredOffset(camSys.Yaw, camSys.Pitch, d, h, off);
        double horiz = Math.Sqrt(off[0] * off[0] + off[2] * off[2]);
        return -Math.Atan2(off[1], Math.Max(1e-3, horiz));
    }

    static void InitCamera(string baseDir, string mapPath)
    {
        camSys = new CameraSystem();
        string camCfg = Path.Combine(baseDir, "ability_sandbox", "cam", "camera.json");
        if (File.Exists(camCfg))
        {
            try { camSys.LoadConfig(camCfg); Log("camera config: " + camCfg); }
            catch (Exception e) { Log("camera config ex: " + e.Message); }
        }
        camSys.SwitchMode(CameraSystem.MODE_CHARACTER);
        try
        {
            cameraSettings = CameraSettings.Load(editorRoot, mapPath, baseDir, Log);
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("MaxCameraDistance", cameraSettings.MaxCameraDistance);
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("MinCameraDistance", cameraSettings.MinCameraDistance);
            camSys.SetDragSpeed(cameraSettings.DragSpeed);
            camSys.Pitch = cameraSettings.InitPitch;
            camSys.Yaw = cameraSettings.InitYaw;
            camSys.Distance = camSys.Row.F("InitCameraDistance", 6.0) * camSys.UnitsPerMeter;
        }
        catch (Exception e) { Log("CameraSettings ex: " + e.Message); }
        Log(string.Format("CameraSystem ready: mode={0} dist={1:F0}u yaw={2:F3} pitch={3:F3} unitsPerMeter={4:F1}",
            camSys.Mode, camSys.Distance, camSys.Yaw, camSys.Pitch, camSys.UnitsPerMeter));
    }

    static void UpdateCamera()
    {
        if (camSys == null) return;
        try
        {
            long now = Environment.TickCount;
            double dt = lastCamMs == 0 ? 0.016 : (now - lastCamMs) / 1000.0;
            lastCamMs = now;
            if (dt > 0.05) dt = 0.05;
            camSys.UpdateDistance(dt, false);   // zoom dynamics bookkeeping

            // The engine owns the camera (position + look). Rotation is fed to
            // the engine's own ROTATE_CAMERA input as cursor-locked pixel
            // deltas - the same emulation path the client uses when the native
            // camera set is unavailable. One bounded event per queued delta.
            while (orbitQueue.Count > 0)
            {
                int[] o = orbitQueue.Dequeue();
                int sx = o[0], sy = o[1];
                if (sx > 30) sx = 30; if (sx < -30) sx = -30;
                if (sy > 30) sy = 30; if (sy < -30) sy = -30;
                if (sx == 0 && sy == 0) continue;
                scene.ExecAction(30, 1, 0, MakeLParam(lockCenter.X, lockCenter.Y));
                scene.ExecAction(1, 1, 0, MakeLParam(lockCenter.X + sx, lockCenter.Y + sy));
                camSys.Yaw += sx * 0.0018;
                camSys.Pitch += sy * 0.00121;
                if (camSys.Pitch > Math.PI / 2 - 0.05) camSys.Pitch = Math.PI / 2 - 0.05;
                if (camSys.Pitch < -Math.PI / 2 + 0.05) camSys.Pitch = -Math.PI / 2 + 0.05;
            }
        }
        catch (Exception e) { Log("UpdateCamera ex: " + e.Message); }
    }

    static List<ProcStep> runSteps = new List<ProcStep>();
    static int runIndex, runLastT;
    static long runStart;
    static bool runActive;
    static int camOutFrames;
    static Ability castAbility;

    [STAThread]
    static void Main()
    {
        startupPath = Path.Combine(editorRoot, "bin64");
        actorPath = Path.Combine(editorRoot, "source", "花萝无动作.actor");
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        dataPath = Env("AS_DATA", Path.Combine(baseDir, "ability_picker", "ability_candidates.json"));
        outDir = Env("AS_OUT", Path.Combine(baseDir, "ability_sandbox", "out"));
        Directory.CreateDirectory(outDir);
        soundDir = Env("AS_SOUND_DIR", Path.Combine(baseDir, "ability_picker", "sound"));
        mapName = Env("AS_MAP", "龙门寻宝");
        Log = delegate(string s)
        {
            try { File.AppendAllText(Path.Combine(outDir, "ability_sandbox.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); }
            catch { }
            Console.WriteLine(s);
        };
        smoke = Env("AS_SMOKE", "0") == "1";
        smokeCast = Env("AS_CAST", "");
        int.TryParse(Env("AS_SHOT_MS", "3200"), out smokeShotMs);
        Log("start data=" + dataPath + " map=" + mapName + (smoke ? " smoke=1 cast=" + smokeCast : ""));

        Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
        {
            Log("THREAD EX: " + e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
        {
            Log("FATAL EX: " + e.ExceptionObject);
        };

        LoadAbilities();
        LoadCatalog();
        try
        {
            BuildForm();
            form.Show();
            Application.DoEvents();
        }
        catch (Exception e) { Log("BuildForm/Show ex: " + e); return; }

        // ---------- engine host init ----------
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

        int err = 1, ok = 0;
        try { ok = engine.Init3DEngine(startupPath, startupPath, workingDir, 0, "./configHttpFile.ini", ref err); }
        catch (Exception e) { Log("Init3DEngine ex: " + e); SetStatus("FATAL: engine init exception"); return; }
        Log(string.Format("Init3DEngine={0} err={1}", ok, err));
        if (ok == 0) { SetStatus("FATAL: engine init failed"); return; }

        try { Log("editor.Init result=" + editor.Init(editorRoot, err, form.Handle.ToInt64())); }
        catch (Exception e) { Log("editor.Init ex: " + e.Message); }

        // SceneForm/RebornClient pattern: bare scene -> LoadMap(sync) -> env -> window
        scene = new KGSceneCLR();
        if (scene == null) { SetStatus("FATAL: no scene"); return; }
        string mapPath = mapName.IndexOf('\\') >= 0
            ? mapName
            : @"data\source\maps\" + mapName + "\\" + mapName + ".jsonmap";
        int mapRc = -1;
        try { mapRc = scene.LoadMap(mapPath, false); }
        catch (Exception e) { Log("LoadMap ex: " + e.Message); }
        Log("LoadMap(" + mapPath + ") result=" + mapRc);
        if (mapRc < 0) { SetStatus("FATAL: LoadMap failed (" + mapRc + ")"); return; }
        try { Log("SetActiveEnvironment=" + scene.SetActiveEnvironment()); }
        catch (Exception e) { Log("SetActiveEnvironment ex: " + e.Message); }
        long winId = scene.AddOutputWindow("", viewport.Handle.ToInt64(), 0);
        Log("winId=" + winId);

        // ---------- player ----------
        var actor = new KGMovieActorCLR();
        actor.Init();
        try { ActorEditorCommandHelper.LoadFromFile(actor, actorPath, 0); }
        catch (Exception e) { Log("LoadFromFile ex: " + e.Message); }
        actorHandle = actor.GetModelHandle();
        Log("actor handle=" + actorHandle);
        scene.AppendModel(actorHandle);
        try { scene.FocusOnModel(); } catch (Exception e) { Log("FocusOnModel ex: " + e.Message); }

        model = new KGModelCLR();
        model.AttachModel(actorHandle);
        // camera: later-branch client input model (camara-fix) driving the
        // engine-owned camera (cursor-locked drag orbit + wheel zoom + F11)
        InitCamera(baseDir, mapPath);
        for (int i = 0; i < 3; i++) { engine.FrameMove(); Application.DoEvents(); }
        SetStatus("map=" + mapName + " - drag L/RMB to orbit, wheel zoom, F11 reset");

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
            if (el > runLastT + 600) StopCast();
        }
        try
        {
            while (pending.Count > 0)
            {
                int[] a = pending.Dequeue();
                if (a[0] == 80) { try { scene.SetCamareMoveState(a[1], a[2]); } catch { } continue; }
                if (a[0] == 81) { try { scene.ExecAction(a[1], a[2], 0, 0); } catch { } continue; }
                if (a[0] == 82) { try { scene.ExecAction(1001, 0, 0, 0); } catch { } continue; }
                if (a[0] == 83) { try { scene.ResetCameraPosLookAtUp(); } catch { } continue; }
                if (a[0] == 31) { scene.ExecAction(31, 1, a[1], 1); continue; }
                int lp = ((a[3] & 0xFFFF) << 16) | (a[2] & 0xFFFF);
                scene.ExecAction(a[0], a[1], 0, lp);
            }
            UpdateCamera();
            engine.FrameMove();
            engine.Render();
        }
        catch (Exception e) { Log("frame ex: " + e.Message); }

        if (smoke)
        {
            long el = Environment.TickCount - smokeStart;
            if (!smokeCastDone && el > 800 && smokeCast != "")
            {
                smokeCastDone = true;
                foreach (Ability a in abilities)
                    if (a.Name == smokeCast) { selected = a; StartCast(a); break; }
            }
            if (!smokeShotDone && el > smokeShotMs)
            {
                smokeShotDone = true;
                Shot("smoke");
                form.Close();
            }
        }
    }

    // ---------- cast ----------
    static readonly List<string> catalogPaths = new List<string>();

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

    static void StartCast(Ability ab)
    {
        if (ab == null) { SetStatus("select an ability first"); return; }
        StopCast();
        castAbility = ab;
        selected = ab;
        runSteps = ab.Process;
        runIndex = 0;
        runLastT = 0;
        foreach (ProcStep s in runSteps) if (s.T > runLastT) runLastT = s.T;
        runStart = Environment.TickCount;
        runActive = true;
        if (runSteps.Count == 0)
        {
            // no process defined yet: at least play the matched animation
            string t = ab.Matched != "" ? ab.Matched : (ab.Tanis.Count > 0 ? ab.Tanis[0] : "");
            if (t != "") PlayAnim(t);
            SetStatus("cast " + ab.Name + ": no process steps yet (played " + Short(t) + ")");
            runActive = false;
            return;
        }
        SetStatus("casting " + ab.Name + " - " + runSteps.Count + " steps");
        ShowCurrent(ab.Name);
    }

    static void StopCast()
    {
        if (scene != null)
        {
            try { scene.RemoveDummyModel("proc_anchor"); } catch { }
            try { scene.RemoveDummyModel("proc_a"); } catch { }
            try { scene.RemoveDummyModel("proc_b"); } catch { }
        }
        runActive = false;
        lastPlayPath = "";
    }

    static void PlayAnim(string path)
    {
        if (path == null || path == "") return;
        try
        {
            int pr = model.PlayAnimation(path, 0, speed, 0);
            curClip = path;
            lastPlayPath = path;
            lastPlayStart = Environment.TickCount;
            Log("anim -> " + path + " (" + pr + ")");
        }
        catch (Exception e) { Log("PlayAnim ex: " + e.Message); }
    }

    static void RunStep(ProcStep s)
    {
        try
        {
            if (s.Kind == "anim")
            {
                string path = FindTani(castAbility, s.V);
                PlayAnim(path);
                Log("cast anim -> " + s.V + " = " + path);
            }
            else if (s.Kind == "sound")
            {
                string wav = Path.Combine(soundDir, s.V + ".wav");
                if (soundChk != null && soundChk.Checked && File.Exists(wav))
                    PlaySound(wav, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
                Log("cast sound -> " + wav + (soundChk != null && soundChk.Checked ? "" : " (sound off)"));
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
                Log("cast dummy -> " + key + " handle=" + dh + " @ " + s.X + "," + s.Y + "," + s.Z);
            }
            else if (s.Kind == "remove")
            {
                Log("cast remove calling -> " + s.V);
                scene.RemoveDummyModel(s.V != "" ? s.V : "proc_anchor");
                Log("cast remove done -> " + s.V);
            }
            else if (s.Kind == "camera")
            {
                if (s.V.StartsWith("out:"))
                {
                    int n;
                    if (!int.TryParse(s.V.Substring(4), out n)) n = 3;
                    camOutFrames = n * 3;
                }
                Log("cast camera -> " + s.V);
            }
            else if (s.Kind == "chain" || s.Kind == "move" || s.Kind == "action")
            {
                Log("cast " + s.Kind + " (not staged) -> " + s.V + " - " + s.N);
            }
        }
        catch (Exception e) { Log("cast step ex (" + s.Kind + "): " + e.Message); }
    }

    // ---------- data ----------
    static void LoadAbilities()
    {
        abilities.Clear();
        if (!File.Exists(dataPath)) { Log("dataset missing: " + dataPath); return; }
        var ser = new JavaScriptSerializer();
        var root = ser.DeserializeObject(File.ReadAllText(dataPath, Encoding.UTF8)) as Dictionary<string, object>;
        if (root == null || !root.ContainsKey("abilities")) return;
        foreach (object o in (object[])root["abilities"])
        {
            var d = o as Dictionary<string, object>;
            if (d == null) continue;
            var ab = new Ability();
            ab.Key = Str(d, "key");
            ab.Name = Str(d, "name");
            ab.Ids = StrList(d, "ids");
            if (ab.Ids.Count == 0) continue;
            ab.Tanis = StrList(d, "tanis");
            ab.Wems = StrList(d, "wems");
            ab.Matched = Str(d, "matched");
            // sandbox list: only resolved abilities (identified tani)
            if (ab.Matched == "") continue;
            ab.NoAnim = Str(d, "noAnim").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (ab.NoAnim) continue;
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
        Log("abilities loaded: " + abilities.Count);
    }

    // ---------- ui ----------
    static void BuildForm()
    {
        form = new Form();
        form.Text = "Ability Sandbox - " + mapName + "  (drag orbit | wheel zoom | Q/E height | F focus | R reset)";
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new Size(1680, 980);
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F12) { Shot("manual"); e.Handled = true; return; }
            if (e.KeyCode == Keys.Space) { if (selected != null) StartCast(selected); e.Handled = true; return; }
            if (e.KeyCode == Keys.Escape) { StopCast(); e.Handled = true; return; }
            if (e.KeyCode == Keys.F11 && camSys != null)
            {
                // CameraReset: reset the engine camera focus + model pitch
                try { scene.ResetCameraPosLookAtUp(); } catch { }
                camSys.Yaw = Math.Atan2(-Math.Cos(0.0), -Math.Sin(0.0));
                camSys.Pitch = -Math.PI / 12.0;
                try { scene.FocusOnModel(); } catch { }
                e.Handled = true;
            }
        };

        var main = new SplitContainer();
        main.Dock = DockStyle.Fill;
        main.Orientation = Orientation.Vertical;
        main.Panel1MinSize = 240;
        form.Controls.Add(main);
        Action applySplit = delegate
        {
            try
            {
                int w = form.ClientSize.Width;
                if (w > 0) main.SplitterDistance = Math.Max(main.Panel1MinSize, (int)(w * 0.20));  // viewport ~80%
            }
            catch { }
        };
        form.Shown += delegate { applySplit(); };
        form.Resize += delegate { applySplit(); };

        var top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 34;
        var castBtn = new Button();
        castBtn.Text = "cast (space)";
        castBtn.Location = new Point(6, 4);
        castBtn.Width = 100;
        castBtn.Click += delegate { if (selected != null) StartCast(selected); };
        top.Controls.Add(castBtn);
        var stopBtn = new Button();
        stopBtn.Text = "stop (esc)";
        stopBtn.Location = new Point(112, 4);
        stopBtn.Width = 90;
        stopBtn.Click += delegate { StopCast(); SetStatus("stopped"); };
        top.Controls.Add(stopBtn);
        soundChk = new CheckBox();
        soundChk.Text = "sound";
        soundChk.Location = new Point(210, 7);
        soundChk.AutoSize = true;
        top.Controls.Add(soundChk);
        main.Panel1.Controls.Add(top);

        var bottom = new Panel();
        bottom.Dock = DockStyle.Bottom;
        bottom.Height = 64;
        statusLabel = new Label();
        statusLabel.Location = new Point(6, 4);
        statusLabel.AutoSize = false;
        statusLabel.Width = 340;
        statusLabel.Height = 28;
        bottom.Controls.Add(statusLabel);
        curLabel = new Label();
        curLabel.Location = new Point(6, 32);
        curLabel.AutoSize = false;
        curLabel.Width = 340;
        curLabel.Height = 28;
        bottom.Controls.Add(curLabel);
        main.Panel1.Controls.Add(bottom);

        abilityList = new ListBox();
        abilityList.Dock = DockStyle.Fill;
        abilityList.DrawMode = DrawMode.OwnerDrawFixed;
        abilityList.ItemHeight = 17;
        abilityList.IntegralHeight = false;
        abilityList.DrawItem += delegate(object s, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < abilities.Count)
            {
                Ability ab = abilities[e.Index];
                Color c = e.ForeColor;
                if (ab.Ip) c = Color.FromArgb(140, 60, 170);
                else if (ab.NoAnim) c = Color.FromArgb(150, 150, 150);
                else if (ab.Matched != "") c = Color.FromArgb(0, 130, 0);
                string txt = (ab.Process.Count > 0 ? "[P] " : "") + ab.Name
                    + "  (" + (ab.Ids.Count > 0 ? string.Join("/", ab.Ids.ToArray()) : "-") + ")";
                using (SolidBrush b = new SolidBrush(c))
                    e.Graphics.DrawString(txt, e.Font, b, e.Bounds);
            }
            e.DrawFocusRectangle();
        };
        abilityList.SelectedIndexChanged += delegate
        {
            if (abilityList.SelectedIndex >= 0 && abilityList.SelectedIndex < abilities.Count)
            {
                selected = abilities[abilityList.SelectedIndex];
                SetStatus("selected: " + selected.Name + "  steps=" + selected.Process.Count
                    + (selected.NoAnim ? "  [no animation]" : ""));
            }
        };
        abilityList.DoubleClick += delegate { if (selected != null) StartCast(selected); };
        main.Panel1.Controls.Add(abilityList);
        abilityList.BringToFront();

        viewport = new Panel();
        viewport.Dock = DockStyle.Fill;
        viewport.BackColor = Color.Black;
        // camera input: real client semantics (later branch) - LMB/RMB drag
        // rotate the camera (cursor lock + dead zone + dragspeed), wheel =
        // Camera_Zoom(0.9/1.1), F11 = reset behind the character (-15 deg).
        lockCenter = new Point(300, 300);
        viewport.Resize += delegate { lockCenter = new Point(viewport.ClientSize.Width / 2, viewport.ClientSize.Height / 2); };
        Action lockMouse = delegate
        {
            mouseLocked = true;
            Cursor.Hide();
            try { Cursor.Position = viewport.PointToScreen(lockCenter); } catch { }
        };
        Action unlockMouse = delegate
        {
            mouseLocked = false;
            lmbDown = false;
            rmbDown = false;
            Cursor.Show();
        };
        viewport.MouseDown += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) lmbDown = true;
            else if (e.Button == MouseButtons.Right) rmbDown = true;
            pressPoint = e.Location;
            dragArmed = true;
        };
        viewport.MouseUp += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) lmbDown = false;
            else if (e.Button == MouseButtons.Right) rmbDown = false;
            dragArmed = false;
            if (!lmbDown && !rmbDown && mouseLocked) unlockMouse();
        };
        viewport.MouseMove += delegate(object s, MouseEventArgs e)
        {
            if (!lmbDown && !rmbDown) return;
            if (!dragArmed) return;
            if (!mouseLocked)
            {
                int mdx = e.X - pressPoint.X, mdy = e.Y - pressPoint.Y;
                if (mdx * mdx + mdy * mdy < 16) return;   // 4 px dead zone
                lockMouse();
                return;
            }
            int dx = e.X - lockCenter.X, dy = e.Y - lockCenter.Y;
            if (dx != 0 || dy != 0)
            {
                int sx = (int)Math.Round(dx * (cameraSettings != null ? cameraSettings.DragSpeed : 1.0));
                int sy = (int)Math.Round(dy * (cameraSettings != null ? cameraSettings.DragPitchSpeed : 1.0));
                orbitQueue.Enqueue(new int[] { sx, sy });
                try { Cursor.Position = viewport.PointToScreen(lockCenter); } catch { }
            }
        };
        viewport.MouseWheel += delegate(object s, MouseEventArgs e) { if (camSys != null) camSys.ZoomBy(e.Delta > 0 ? -1.0 : 1.0); };
        form.MouseWheel += delegate(object s, MouseEventArgs e) { if (camSys != null) camSys.ZoomBy(e.Delta > 0 ? -1.0 : 1.0); };
        main.Panel2.Controls.Add(viewport);

        foreach (Ability ab in abilities) abilityList.Items.Add(ab.Name);
        if (abilities.Count > 0) abilityList.SelectedIndex = 0;
    }

    static void SetStatus(string s)
    {
        if (statusLabel != null) statusLabel.Text = s;
        Log(s);
    }

    static void ShowCurrent(string s)
    {
        if (curLabel != null) curLabel.Text = "cast: " + s;
    }

    static void Shot(string tag)
    {
        try
        {
            string png = Path.Combine(outDir, "shot_" + tag + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            scene.SetScreenShot(png, 2);
            scene.DoScreenShotImmediate();
            Log("shot -> " + png);
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
