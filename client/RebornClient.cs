// RebornClient — M1.1 scaffold: real map + animated player (dummy + KGModelCLR),
// walk/run/jump/fall, follow camera, one skill key, HUD stub.
// Build: client\build_client.cmd    Run: bin64\reborn_client.exe (cwd = editor root)
//
// Env:
//   RC_MAP=<vfs jsonmap>          default 龙门寻宝
//   RC_SPAWN=x,y,z                optional spawn (y optional -> terrain)
//   RC_DUMMY=<representid>        spawn one 试炼木桩 near spawn (default 35901; 0 = off)
//   RC_DUMMY_DIST=<units>         dummy distance along the view dir (default 400)
//   RC_DUMMY_NAME/LEVEL/HP        target-frame values (default 初级试炼木桩/131/500000000)
//   RC_TAB_AT=ms,ms               smoke: target-next (Tab) at these times
//   RC_AUTORUN=ms                 exit after N ms (0 = until window closed)
//   RC_SHOTS=2000,5000,...        screenshot times (ms)
//   RC_CLIP_IDLE/WALK/RUN/JUMP/FALL/SKILL=<vfs .ani/.tani path>
//   RC_SKILL_MS=8000              skill clip duration before returning to state clip
//   RC_YAW_OFFSET=0               model facing calibration (radians)
//   RC_SCALE=1                    player model scale
//   RC_PHYS_DLL=<path>            terrain sampler physics DLL (default: client copy)
//   RC_TERR_CACHE=4               terrain region cache slots (LRU; >= 1)
// RC_MAP accepts an absolute OS path (mini sandbox maps: tools/sandbox).
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MovieEngineCLR;

// Engine control ids (control.lua / Camera_EnableControl): the host keeps the
// same table the client stores in its control property store.
internal static class ControlId
{
    public const int Forward = 0, Backward = 1, TurnLeft = 2, TurnRight = 3,
        StrafeLeft = 4, StrafeRight = 5, Camera = 6, StickCamera = 7,
        Walk = 8, Jump = 9, AutoRun = 10, Follow = 11, Up = 12, Down = 13;
}

internal static class Ctrl
{
    public static void Set(ref int mask, int id, bool on)
    {
        if (on) mask |= 1 << id; else mask &= ~(1 << id);
    }
    public static bool Get(int mask, int id) { return (mask & (1 << id)) != 0; }
}

internal static class RebornClient
{
    static string outDir;
    static Action<string> Log;
    static string adHabit = "strafe";   // classic A/D: strafe (default.txt) | turn
    static double moveYaw = 0.0;        // movement/control frame; turn keys rotate it
    // P4 control probe (RC_PROBE_CONTROL=1): read-only engine field capture.
    // Host JX3RepresentX64.dll RVAs come from docs/controls/CONTROL_MODES_P4_PROBE.md
    // (MovieEditor build 2026-09-14, not the game-client RVAs).
    static bool probeControl = false;
    static long nextProbeMs = 0;
    static bool probeTableDone = false;
    // J3 sprint input state (decoded: IsKeyDoubleDown window 250 ms; StartSprint
    // casts skill 6754 + player:Sprint(true); CheckEndSprint -> EndSprint).
    static bool sprintOn = false;

    [STAThread]
    private static void Main(string[] args)
    {
        // Per-build engine memory namespace (isolation, not exclusion): feature
        // builds (reborn_client_<slug>.exe) get their own namespace so 2+ clients
        // can run at the same time without sharing engine memory. The canonical
        // reborn_client.exe keeps MovieEditor.memory. RC_MEM_NS overrides.
        string memNs = Env("RC_MEM_NS", "");
        string selfSlug = null;
        {
            string myName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            if (memNs.Length == 0)
                memNs = (myName == "reborn_client") ? "MovieEditor.memory" : myName + ".memory";
            if (myName.StartsWith("reborn_client_"))
                selfSlug = myName.Substring("reborn_client_".Length);
        }
        // Single-instance guard: block only processes that share this build's
        // memory namespace. Different feature builds have different namespaces
        // and are allowed to run concurrently; the canonical build excludes the
        // apps that still hardcode MovieEditor.memory. RC_ALLOW_MULTI=1 overrides.
        if (Env("RC_ALLOW_MULTI", "0") != "1")
        {
            try
            {
                var me = System.Diagnostics.Process.GetCurrentProcess();
                string[] peers;
                if (memNs == "MovieEditor.memory")
                    peers = new string[] { "reborn_camfp", "reborn_client", "asset_sandbox", "ability_picker" };
                else
                    peers = new string[] { me.ProcessName };
                foreach (string name in peers)
                {
                    foreach (var other in System.Diagnostics.Process.GetProcessesByName(name))
                    {
                        if (other.Id == me.Id) continue;
                        // Blocked-start diagnostics: the dialog keeps its title
                        // on the feature build and names the conflicting session
                        // (pid/start/title); a line is also appended to
                        // reborn_out\guard_block.txt so a missed dialog is
                        // discoverable from logs (rc=blocked marker).
                        string started = "?";
                        string title = "";
                        try { started = other.StartTime.ToString("HH:mm:ss"); } catch { }
                        try { title = other.MainWindowTitle; } catch { }
                        try
                        {
                            string gdir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reborn_out");
                            Directory.CreateDirectory(gdir);
                            File.AppendAllText(Path.Combine(gdir, "guard_block.txt"),
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " +
                                me.ProcessName + " blocked by " + name + " pid=" + other.Id +
                                " start=" + started + " title=[" + title + "]\r\n");
                        }
                        catch { }
                        System.Windows.Forms.MessageBox.Show(
                            name + " is already running (pid " + other.Id +
                            ", started " + started + ", window [" + title + "])" +
                            " and shares memory namespace " + memNs +
                            ". Close it first or set RC_ALLOW_MULTI=1.",
                            me.ProcessName + ": start blocked");
                        return;
                    }
                }
            }
            catch { }
        }
        string editorRoot = @"C:\SeasunGame\MovieEditor";
        string startupPath = Path.Combine(editorRoot, "bin64");
        string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
        // Terrain sampler physics DLL override (sandbox/portable roots); default
        // is the client's copy, same as the canonical host.
        string physDll = Env("RC_PHYS_DLL",
            @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll");
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
        // Landing branch (player_suspend.krl.txt F1 rows): FallFloorAnimation
        // data/source/player/F1/动作/F1b02yd握拳小跳c.ani is played when the
        // landed height difference exceeds FallDownHeightFloor (500 u).
        string clipLand = Env("RC_CLIP_LAND", f1 + "f1b02yd\u63E1\u62F3\u5C0F\u8DF3c.ani");
        // Strafe / back-pedal locomotion for the operation-mode routing
        // (CLASSICAL side-step / back-pedal): the F1 authored clips
        // (player_animation_f1.txt KindID 6 / 57). Verified loadable+playable
        // in-engine (reborn_20260930_231207.log, rc=0, no AV).
        string clipStrafeL = Env("RC_CLIP_STRAFE_L", f1 + "F1b02yd\u632A\u6B65\u5DE6.tani");
        string clipStrafeR = Env("RC_CLIP_STRAFE_R", f1 + "F1b02yd\u632A\u6B65\u53F3.tani");
        string clipBack = Env("RC_CLIP_BACK", f1 + "F1b02yd\u540E\u900001.tani");
        // TOGGLESITDOWN (decoded: OnUseSkill(17) / Stand()): the F1 catalog's
        // looping 打坐 clip (kind 1, loop 1) is the sit pose.
        string clipSit = Env("RC_CLIP_SIT", f1 + "F1b02dj\u6253\u5750a.tani");
        // TOGGLESHEATH (decoded: SetSheath flag): the b02 draw transition
        // (F1b02ty拔剑01_start01) and the drawn-stance loop
        // (F1b02ty拔剑01_st01_持续, kind 31 loop 1). No 收剑 clip ships for b02;
        // sheathing falls back to the normal idle.
        string clipSheathDraw = Env("RC_CLIP_SHEATH_DRAW", f1 + "F1b02ty\u62D4\u525101_start01.ani");
        string clipSheathHold = Env("RC_CLIP_SHEATH_HOLD", f1 + "F1b02ty\u62D4\u525101_st01_\u6301\u7EED.tani");
        long landClipMs = 700;
        long.TryParse(Env("RC_CLIP_LAND_MS", "700"), out landClipMs);
        float fallDownHeightFloor = 500f;   // FallDownHeightFloor (player_suspend.krl.txt)
        float.TryParse(Env("RC_FALL_ROLL", "500"), out fallDownHeightFloor);
        long landClipUntil = 0;
        // Airborne horizontal state: takeoff/End triple JumpSpeedXY / walk-off
        // momentum (u/s); airStartY = height when the character left the ground.
        float vjx = 0f, vjz = 0f, airStartY = 0f;
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
        var tabAt = new System.Collections.Generic.List<long>();
        foreach (string s in Env("RC_TAB_AT", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            long tt;
            if (long.TryParse(s.Trim(), out tt)) tabAt.Add(tt);
        }
        // RC_CLICK_AT=ms,x,y[;ms,x,y...]  smoke: left click at panel pixel (x,y)
        // (same path as the real LMB click: pick under cursor, else deselect)
        var clickAt = new System.Collections.Generic.List<int[]>();
        foreach (string s in Env("RC_CLICK_AT", "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = s.Trim().Split(',');
            int ms0, cx0, cy0;
            if (parts.Length == 3 && int.TryParse(parts[0].Trim(), out ms0)
                && int.TryParse(parts[1].Trim(), out cx0) && int.TryParse(parts[2].Trim(), out cy0))
                clickAt.Add(new int[] { ms0, cx0, cy0 });
        }

        outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reborn_out");
        Directory.CreateDirectory(outDir);
        // keep a per-run log (overwrite-safe for parallel sessions) and the
        // stable reborn.log used by the analysis scripts
        string runLog = Path.Combine(outDir, "reborn_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
        var logLines = new System.Collections.Generic.List<string>();
        Log = delegate(string s)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n";
            try { File.AppendAllText(Path.Combine(outDir, "reborn.log"), line); } catch { }
            try { File.AppendAllText(runLog, line); } catch { }
            Console.WriteLine(s);
            lock (logLines)
            {
                logLines.Add(DateTime.Now.ToString("HH:mm:ss") + " " + s);
                if (logLines.Count > 400) logLines.RemoveRange(0, logLines.Count - 400);
            }
        };
        // short visible tag from the exe name: reborn_client_collision.exe ->
        // "collision" (canonical reborn_client.exe -> "canonical"); shown in
        // the window title and the HUD's first line so parallel clients are
        // distinguishable at a glance
        string buildTag = Path.GetFileNameWithoutExtension(
            System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (buildTag.StartsWith("reborn_client_")) buildTag = buildTag.Substring("reborn_client_".Length);
        else if (buildTag == "reborn_client") buildTag = "canonical";

        // Build fingerprint (camera workstream logs are the camFP=True set):
        // exe name + mtime + build_info git hash + the active camera flag
        // defaults. Ends "which build/flags produced this log" ambiguity.
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeName = Path.GetFileName(exePath);
            string exeMtime = ""; string git = "?"; string dirty = "?";
            try { exeMtime = File.GetLastWriteTime(exePath).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
            try
            {
                string dir = Path.GetDirectoryName(exePath);
                string bi = Path.Combine(dir, "build_info_" + Path.GetFileName(exePath) + ".txt");
                if (!File.Exists(bi)) bi = Path.Combine(dir, "build_info_" + Path.GetFileNameWithoutExtension(exePath) + ".txt");
                if (!File.Exists(bi)) bi = Path.Combine(dir, "build_info.txt");
                if (File.Exists(bi))
                {
                    foreach (string ln in File.ReadAllLines(bi))
                    {
                        if (ln.StartsWith("git=")) git = ln.Substring(4);
                        else if (ln.StartsWith("dirty=")) dirty = ln.Substring(6);
                    }
                }
            }
            catch { }
            Log(string.Format(
                "build={0} {1} git={2} dirty={3} camFP=True flags=(ENGINESET={4},LOOKPACK={5},RATECAP={6},LOADPACE={7},FULLLOAD={8},PATCH_D6={9},PITCH_ALIGN={10},PLAYER_HIDE={11},SNAPGUARD={12},CROSS={13},HITMIN={14},WALLGATE={15},SCENERAY={16},SCENEMIN={17},BACKFACE={18},HITWIN={19},OPMODE={20})",
                exeName, exeMtime, git, dirty,
                Env("RC_CAM_ENGINESET", "1"), Env("RC_CAM_LOOKPACK", "0"),
                Env("RC_CAM_RATECAP", "0"), Env("RC_CAM_LOADPACE", "1"),
                Env("RC_FULLLOAD", "0"), Env("RC_PATCH_D6", "0"),
                Env("RC_PITCH_ALIGN", "1"), Env("RC_PLAYER_HIDE", "1"),
                Env("RC_CAM_SNAPGUARD", "0"), Env("RC_CAM_CROSS", "0"), Env("RC_CAM_HITMIN", "3.0"), Env("RC_CAM_WALLGATE", "0"), Env("RC_CAM_SCENERAY", "1"), Env("RC_CAM_SCENEMIN", "80"),
                Env("RC_CAM_BACKFACE", "1"), Env("RC_CAM_HITWIN", Env("RC_CAM_HITWINDOW", "0")),
                Env("RC_MODE", "joystick")));
        }
        Log("start map=" + mapPath);
        Log("asset_root=" + workingDir + " phys=" + physDll);

        var form = new Form();
        // Window title: feature builds (reborn_client_<slug>.exe) identify as
        // sandbox-<slug> so concurrent sandbox windows are distinguishable
        // (AGENTS.md, parallel client feature work); RC_TITLE overrides.
        {
            string appTitle = Env("RC_TITLE", "");
            if (appTitle.Length == 0)
            {
                string myProcName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                if (myProcName == "reborn_client") appTitle = "JX3";
                else if (myProcName.StartsWith("reborn_client_"))
                    appTitle = "sandbox-" + myProcName.Substring("reborn_client_".Length);
                else appTitle = myProcName;
            }
            form.Text = appTitle;
        }
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new System.Drawing.Size(1280, 720);
        // Window / render-target sizing (D3): the engine renders into a child window of
        // the panel, so the client size drives the render target. RC_WIDTH/RC_HEIGHT
        // override it; RC_FULLSCREEN=1 goes borderless maximized.
        {
            int rw = 0, rh = 0;
            if (int.TryParse(Env("RC_WIDTH", ""), out rw) && int.TryParse(Env("RC_HEIGHT", ""), out rh)
                && rw >= 320 && rh >= 240)
                form.ClientSize = new System.Drawing.Size(rw, rh);
            if (Env("RC_FULLSCREEN", "0") == "1")
            {
                form.FormBorderStyle = FormBorderStyle.None;
                form.WindowState = FormWindowState.Maximized;
            }
            Log(string.Format("window: client={0}x{1} fullscreen={2}",
                form.ClientSize.Width, form.ClientSize.Height, Env("RC_FULLSCREEN", "0")));
        }
        var panel = new Panel();
        panel.Dock = DockStyle.Fill;
        form.Controls.Add(panel);
        // M1.7: the engine renders into a child window of orm, so WinForms
        // child controls sit behind the 3D output. The HUD is a separate
        // top-level layered overlay (client/HudOverlay.cs) owned by orm;
        // Esc toggles the information panel (info + control mode + COPY LOG);
        // nothing is shown while it is closed (no on-screen panel hints).
        var hud = new HudOverlay();
        if (Env("RC_HUD_OPEN", "0") == "1") hud.ShowInfo = true;   // test: start open
        form.Show();
        hud.PlaceOver(form);
        // Loading overlay (D4): WinForms controls sit behind the engine's child window,
        // so the loading text is a separate top-level window; RC_NOLOADING=1 disables.
        LoadingOverlay loading = null;
        if (Env("RC_NOLOADING", "0") != "1")
        {
            try { loading = new LoadingOverlay(); loading.Phase("Initializing engine..."); }
            catch { loading = null; }
        }
        // COPY LOG row (clickable inside the open panel); copies the recent
        // run log to the clipboard.
        hud.OnCopyLog = delegate
        {
            try
            {
                string text;
                lock (logLines) { text = string.Join("\r\n", logLines.ToArray()); }
                if (text.Length == 0) text = "(no log yet)";
                Clipboard.SetText(text);
                Log("copied " + text.Length + " chars of log to clipboard");
            }
            catch (Exception e) { Log("clipboard copy failed: " + e.Message); }
        };
        Application.DoEvents();

        // Startup override (deviation D7): only armed when RC_STARTUP is set;
        // must happen before KGEngineCLR/Init3DEngine so the DLL-load
        // notification is registered before KG3D_MaterialSystemX64.dll loads.
        string startupEnv = Env("RC_STARTUP", "");
        bool startupOverride = startupEnv.Length > 0;
        if (startupOverride)
            Log("Startup shim: " + StartupShim.Init());

        var baselib = new KGBaseCLR();
        var engine = new KGEngineCLR();
        var editor = new KGMovieEditorCLR();
        var sound = new KG3DSoundCLR();

        engine.SetRootPath(workingDir);
        try { baselib.InitConsoleLog(); } catch (Exception e) { Log("InitConsoleLog: " + e.Message); }
        Directory.CreateDirectory(Path.Combine(startupPath, "logs"));
        int r1 = 0, r2 = 0, r3 = 0;
        long tInit = Environment.TickCount;
        try { r1 = baselib.InitPath(workingDir, false); } catch (Exception e) { Log("InitPath ex: " + e.Message); }
        long m1 = Environment.TickCount - tInit;
        try { r2 = baselib.InitMemory(memNs); } catch (Exception e) { Log("InitMemory ex: " + e.Message); }
        long m2 = Environment.TickCount - tInit - m1;
        try { r3 = baselib.InitPak(false); } catch (Exception e) { Log("InitPak ex: " + e.Message); }
        long m3 = Environment.TickCount - tInit - m1 - m2;
        Log(string.Format("InitPath={0} InitMemory={1} InitPak={2} ns={3} ms=({4},{5},{6})", r1, r2, r3, memNs, m1, m2, m3));

        int err = 1;
        int ok = 0;
        long t3d = Environment.TickCount;
        string initCfg = Env("RC_INIT_CFG", "./configHttpFile.ini");
        Log("initcfg=" + initCfg);
        if (loading != null) loading.Phase("Initializing engine...");
        try { ok = engine.Init3DEngine(startupPath, startupPath, workingDir, 0, initCfg, ref err); }
        catch (Exception e) { Log("Init3DEngine ex: " + e); return; }
        Log(string.Format("Init3DEngine={0} err={1} ms={2}", ok, err, Environment.TickCount - t3d));
        if (startupOverride)
            Log("Startup: " + StartupShim.Status());
        if (ok == 0) { Log("FATAL: engine init failed"); return; }
        try { VideoOptions.Apply(engine, Env("RC_GAME_CONFIG_DIR", @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\config"), startupPath, Log); }
        catch (Exception e) { Log("VideoOptions ex: " + e.Message); }

        // D2: engine option read-back. `KGEngineOptionProxyCLR` has public fields
        // (recovered by RC_OPT_PROBE=1); `GetEngineOption(ref proxy)` fills them with
        // the engine's active values. RC_OPT_DUMP=<file> writes them as key=value.
        string optDump = Env("RC_OPT_DUMP", "");
        if (Env("RC_OPT_PROBE", "0") == "1" || optDump.Length > 0)
        {
            try
            {
                Type pt = typeof(KGEngineCLR).Assembly.GetType("MovieEngineCLR.KGEngineOptionProxyCLR");
                Log("optprobe type=" + (pt == null ? "(not found)" : pt.FullName));
                if (pt != null)
                {
                    if (Env("RC_OPT_PROBE", "0") == "1")
                    {
                        foreach (System.Reflection.MemberInfo mi in pt.GetMembers(
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                            Log("optprobe member " + mi.MemberType + " " + mi.Name);
                    }
                    object proxy = null;
                    try { proxy = Activator.CreateInstance(pt); } catch (Exception e) { Log("optprobe new ex: " + e.Message); }
                    if (proxy != null)
                    {
                        System.Reflection.MethodInfo gm = typeof(KGEngineCLR).GetMethod("GetEngineOption");
                        if (gm != null)
                        {
                            try
                            {
                                object[] gmArgs = new object[] { proxy };
                                object rc = gm.Invoke(engine, gmArgs);
                                Log("optprobe GetEngineOption rc=" + rc);
                                proxy = gmArgs[0];
                            }
                            catch (Exception e) { Log("optprobe GetEngineOption ex: " + e.Message); }
                        }
                        else Log("optprobe GetEngineOption method not found");

                        System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
                        foreach (System.Reflection.FieldInfo fi in pt.GetFields(
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            string v = "(err)";
                            try
                            {
                                object o = fi.GetValue(proxy);
                                Array arr = o as Array;
                                if (arr != null)
                                {
                                    string[] parts = new string[arr.Length];
                                    for (int ai = 0; ai < parts.Length; ai++)
                                        parts[ai] = arr.GetValue(ai).ToString();
                                    v = string.Join(",", parts);
                                }
                                else v = o == null ? "(null)" : o.ToString();
                            }
                            catch { }
                            lines.Add(fi.Name + "=" + v);
                            if (Env("RC_OPT_PROBE", "0") == "1") Log("optprobe value " + fi.Name + "=" + v);
                        }
                        if (optDump.Length > 0)
                        {
                            try { File.WriteAllLines(optDump, lines.ToArray()); Log("optdump -> " + optDump + " keys=" + lines.Count); }
                            catch (Exception e) { Log("optdump ex: " + e.Message); }
                        }
                    }
                }
            }
            catch (Exception e) { Log("optprobe ex: " + e.Message); }
        }
        try { Log("editor.Init result=" + editor.Init(editorRoot, err, form.Handle.ToInt64())); }
        catch (Exception e) { Log("editor.Init ex: " + e.Message); }

        // ---- audio (step 1) ------------------------------------------------
        // Wwise via the engine's own KG3DSoundCLR (the spike's call, now in the
        // product client). The engine's tani SoundTag does not fire in the host
        // (SOUND_PATH.md, Frida: Wwise inits, no LoadBank/PostEvent), so the
        // skill sound is played from the decoded WAV as a REGISTERED PROVISIONAL
        // until the native tag path is recovered (re-open: the SoundTag fires
        // with the banks loaded in the host).
        // Wwise streamed media: the shell resolves its relative BasePath
        // (config.ini [WwiseSetting] BasePath=data/wwiseaudio/...) at sound init,
        // and the install is read-only. With RC_SOUND_MEDIA we switch the process
        // cwd to a staged media tree BEFORE sound.Init so that base path resolves
        // against it (data/wwiseaudio/GeneratedSoundBanks/Windows/<lang>/<id>.wem).
        string mediaRoot = Env("RC_SOUND_MEDIA", "");
        if (mediaRoot.Length > 0 && Directory.Exists(mediaRoot))
        {
            try
            {
                if (SetCurrentDirectoryW(mediaRoot))
                    Log("sound-native: cwd for Wwise IO -> " + mediaRoot);
                else Log("sound-native: cwd switch failed");
            }
            catch (Exception e) { Log("sound-native cwd ex: " + e.Message); }
        }
        bool soundReady = false;
        if (Env("RC_SOUND", "1") != "0")
        {
            try { sound.Init(startupPath, form.Handle.ToInt64()); soundReady = true; Log("sound: KG3DSoundCLR.Init ok"); }
            catch (Exception e) { Log("sound: KG3DSoundCLR.Init ex: " + e.Message); }
        }
        else Log("sound: disabled (RC_SOUND=0)");
        string skillWav = Path.Combine(Application.StartupPath, "flws_sound.wav");
        if (!File.Exists(skillWav)) { Log("sound: skill wav missing at " + skillWav); skillWav = null; }
        else Log("sound: skill wav " + skillWav);

        // Sound-shell recon (1.6 native audio): which shell/branch is active and
        // which sound modules the host actually loads. Env-gated, read-only.
        if (Env("RC_SOUND_DBG", "0") == "1")
        {
            try
            {
                string cfg = Path.Combine(workingDir, "config.ini");
                string ww = "(config.ini missing)";
                try
                {
                    if (File.Exists(cfg))
                    {
                        string[] cl = File.ReadAllLines(cfg);
                        for (int i = 0; i < cl.Length; i++)
                            if (cl[i].IndexOf("Wwise", StringComparison.OrdinalIgnoreCase) >= 0)
                            { ww = cl[i].Trim(); break; }
                    }
                }
                catch (Exception e2) { ww = "cfg ex: " + e2.Message; }
                Log("sound-dbg config=" + cfg + " -> " + ww);
                Type st = typeof(KG3DSoundCLR);
                foreach (System.Reflection.FieldInfo fi in st.GetFields(
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                {
                    object v = null;
                    try { v = fi.GetValue(sound); } catch { }
                    Log("sound-dbg field " + fi.Name + " (" + fi.FieldType.Name + ") = " + (v == null ? "(null)" : v.ToString()));
                }
                foreach (System.Diagnostics.ProcessModule m in System.Diagnostics.Process.GetCurrentProcess().Modules)
                {
                    string mn = m.ModuleName;
                    if (mn.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Wwise", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("FMOD", StringComparison.OrdinalIgnoreCase) >= 0)
                        Log("sound-dbg module " + mn);
                }
            }
            catch (Exception e) { Log("sound-dbg ex: " + e.Message); }
        }

        // Native Wwise path (1.6): loads sound_probe.dll which (a) hooks the
        // engine's PostEvent/LoadBank calls for evidence and (b) can load the
        // game's own banks and post the skill event through the engine's Wwise.
        // RC_SOUND_NATIVE=1 + RC_BANK=<skillremake.bnk>; WAV stays the fallback.
        bool soundNative = false;
        uint nativeEvent = 3378728138;   // FLWS event id (SOUND_PATH.md)
        try
        {
            uint ev;
            if (uint.TryParse(Env("RC_SOUND_EVENT", ""), out ev)) nativeEvent = ev;
        }
        catch { }
        bool wantNative = Env("RC_SOUND_NATIVE", "1") != "0" && Env("RC_BANK", "").Length > 0;
        if (Env("RC_SOUND_HOOK", "0") == "1" || wantNative)
        {
            try
            {
                string probeDll = Path.Combine(Application.StartupPath, "sound_probe.dll");
                string probeLog = Path.Combine(Application.StartupPath, "reborn_out", "sound_probe.log");
                IntPtr hp = SoundProbe.Load(probeDll);
                if (hp == IntPtr.Zero)
                    Log("sound-hook: load failed (" + probeDll + ")");
                else
                {
                    int pr = SoundProbe.Init(probeLog);
                    Log("sound-hook: init rc=" + pr + " status=" + SoundProbe.Status());
                    if (wantNative)
                    {
                        string bank = Env("RC_BANK", "");
                        if (bank.Length == 0) Log("sound-native: RC_BANK not set");
                        else
                        {
                            string bankDir = Path.GetDirectoryName(bank);
                            string initBnk = Path.Combine(bankDir, "Init.bnk");
                            if (File.Exists(initBnk))
                                Log("sound-native: Init.bnk rc=" + SoundProbe.LoadBankW(initBnk));
                            int br = SoundProbe.LoadBankW(bank);
                            if (br >= 0) { soundNative = true; Log("sound-native: bank ok id/rc=" + br); }
                            else Log("sound-native: bank failed rc=" + br);

                        }
                    }
                }
            }
            catch (Exception e) { Log("sound-hook ex: " + e.Message); }
        }

        var scene = new KGSceneCLR();
        // Recon: dump the managed wrapper API surface for the player / near-plane
        // paths (B1 exit; docs/camera/CLOSE_RANGE_RESEARCH.md §2). Env-gated.
        if (Env("RC_API_DUMP", "0") == "1")
        {
            DumpApi("KGEngineCLR", typeof(KGEngineCLR));
            DumpApi("KGSceneCLR", typeof(KGSceneCLR));
            DumpApi("KGModelCLR", typeof(KGModelCLR));
        }
        if (Env("RC_MAINPLAYER", "") != "")
        {
            int mp;
            if (int.TryParse(Env("RC_MAINPLAYER", "0"), out mp))
            {
                try
                {
                    System.Reflection.MethodInfo mi = typeof(KGEngineCLR).GetMethod("SetMainPlayerType");
                    if (mi == null) Log("mainplayer: KGEngineCLR.SetMainPlayerType not exposed");
                    else
                    {
                        mi.Invoke(engine, new object[] { mp });
                        Log("mainplayer: SetMainPlayerType(" + mp + ") ok");
                    }
                }
                catch (Exception e) { Log("mainplayer ex: " + e.Message); }
            }
        }
        // D6 mitigation: ask the engine to fully load the scene up front so the
        // lazy material/shader loader (missing build-machine DataStores -> AV)
        // is not raced while running through the map. Env-gated for A/B first.
        bool fullLoad = Env("RC_FULLLOAD", "0") == "1";
        if (loading != null) loading.Phase("Loading map...");
        long tMap = Environment.TickCount;
        int loadResult = scene.LoadMap(mapPath, false);
        long mMap = Environment.TickCount - tMap;
        if (fullLoad)
        {
            try
            {
                int fr = scene.SetSceneFullLoading(true);
                Log("fullload rc=" + fr + " progress=" + scene.GetLoadingProgress().ToString("F3"));
                if (loading != null) loading.Phase("Loading scene... " + (scene.GetLoadingProgress() * 100.0).ToString("F0") + "%");
            }
            catch (Exception e) { Log("fullload ex: " + e.Message); }
        }
        Log("LoadMap result=" + loadResult + " ms=" + mMap);
        if (loading != null) loading.Phase("Preparing scene...");
        if (loadResult < 0) { Log("FATAL: LoadMap failed"); return; }
        // Environment data override (weather workstream A1): apply a host-side
        // copy of environment.json/playerEnvironment.json via ResetEnvironment
        // (no install write). Used to drive dayNightCycle authored values.
        if (Env("RC_ENV_DIR", "") != "")
        {
            try
            {
                int rr = scene.ResetEnvironment(Env("RC_ENV_DIR", ""));
                Log("env override dir=" + Env("RC_ENV_DIR", "") + " rc=" + rr);
            }
            catch (Exception e) { Log("env override ex: " + e.Message); }
        }
        scene.SetActiveEnvironment();
        // Environment/day-night recon probe (weather workstream): exact managed
        // signatures + the active dynamic-environment timeline path.
        if (Env("RC_ENV_PROBE", "0") == "1")
        {
            try
            {
                Type st = typeof(KGSceneCLR);
                string[] names = new string[] { "SetTrueSkyDayTime", "GetTrueSkyDayTime",
                    "UpdateSeasonRelativeYearTime", "GetSeasonRelativeYearTime",
                    "SetSeasonParam", "GetSeasonParam", "CreateGDBTimelineCurveFromFile",
                    "SetGlobalDynamicEnvTimelineInterpolationForAllTimelineKey",
                    "GetGlobalDynamicEnvTimelineInterpolationForAllTimelineKey",
                    "GetCurrentGDBTimelineCurvePath", "EnableSunLightArcBall",
                    "SetActiveEnvironment", "ResetEnvironment", "GetEnvironment" };
                foreach (string mn in names)
                {
                    foreach (System.Reflection.MethodInfo mi in st.GetMethods())
                    {
                        if (mi.Name != mn) continue;
                        string ps = "";
                        foreach (System.Reflection.ParameterInfo pi in mi.GetParameters())
                            ps += pi.ParameterType.Name + " " + pi.Name + ", ";
                        Log("envprobe sig " + mn + " -> " + mi.ReturnType.Name + " (" + ps + ")");
                    }
                }
                try
                {
                    string mods = "";
                    foreach (System.Diagnostics.ProcessModule m in System.Diagnostics.Process.GetCurrentProcess().Modules)
                    {
                        if (m.ModuleName.IndexOf("TrueSky", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.ModuleName.IndexOf("Wwise", StringComparison.OrdinalIgnoreCase) >= 0)
                            mods += m.ModuleName + " ";
                    }
                    Log("envprobe modules: " + (mods.Length == 0 ? "(none)" : mods));
                }
                catch (Exception e) { Log("envprobe modules ex: " + e.Message); }
                try
                {
                    object p = st.InvokeMember("GetCurrentGDBTimelineCurvePath",
                        System.Reflection.BindingFlags.InvokeMethod, null, scene, new object[0]);
                    Log("envprobe gdbTimeline=" + (p == null ? "(null)" : p.ToString()));
                }
                catch (Exception e) { Log("envprobe gdbTimeline ex: " + e.Message); }
                try
                {
                    object t = st.InvokeMember("GetTrueSkyDayTime",
                        System.Reflection.BindingFlags.InvokeMethod, null, scene, new object[0]);
                    Log("envprobe trueSkyDayTime=" + (t == null ? "(null)" : t.ToString()));
                }
                catch (Exception e) { Log("envprobe trueSkyDayTime ex: " + e.Message); }
            }
            catch (Exception e) { Log("envprobe ex: " + e.Message); }
        }
        // Environment phase-2 probe: seasonal time/params, sun arcball, GDB
        // interpolation + the KG_EnvironmentCLR method surface (A1 candidates).
        if (Env("RC_ENV_PROBE", "0") == "2")
        {
            try
            {
                object env = scene.GetEnvironment();
                if (env != null)
                {
                    foreach (System.Reflection.MethodInfo mi in env.GetType().GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (mi.DeclaringType == typeof(object)) continue;
                        string ps = "";
                        foreach (System.Reflection.ParameterInfo pi in mi.GetParameters())
                            ps += pi.ParameterType.Name + ",";
                        Log("envobj " + mi.Name + " -> " + mi.ReturnType.Name + " (" + ps + ")");
                    }
                }
                int sr1 = scene.UpdateSeasonRelativeYearTime(0.25f);
                Log(string.Format("season relative set rc={0} get={1:F3}", sr1, scene.GetSeasonRelativeYearTime()));
                scene.SetSeasonParam(true, 0.25f, 1.0f);
                bool be = false; float st = 0f, si = 0f;
                scene.GetSeasonParam(ref be, ref st, ref si);
                Log(string.Format("season param enable={0} time={1:F3} intensity={2:F3}", be, st, si));
                int sr2 = scene.EnableSunLightArcBall(1);
                Log("sunArcBall rc=" + sr2);
                int sr3 = scene.SetGlobalDynamicEnvTimelineInterpolationForAllTimelineKey(1.0f);
                Log(string.Format("gdbInterp rc={0} get={1:F3}", sr3, scene.GetGlobalDynamicEnvTimelineInterpolationForAllTimelineKey()));
            }
            catch (Exception e) { Log("envprobe2 ex: " + e.Message); }
        }
        // Phase-3 probe: real-system day time + sun/moon intensity together.
        if (Env("RC_ENV_PROBE", "0") == "3")
        {
            try
            {
                object env = scene.GetEnvironment();
                Type et = env.GetType();
                Log(string.Format("envprobe3 init dayTime={0} tz={1} sunMax={2} moonMax={3}",
                    InvokeEnv(et, env, "GetRealSystemDayTime"),
                    InvokeEnv(et, env, "GetRealSystemTimezone"),
                    InvokeEnv(et, env, "GetRealSystemMaxSunLightIntensity"),
                    InvokeEnv(et, env, "GetRealSystemMaxMoonLightIntensity")));
                InvokeEnv(et, env, "SetRealSystemTimezone", 0f);
                InvokeEnv(et, env, "SetRealSystemDayTime", 0.25f);
                InvokeEnv(et, env, "SetRealSystemMaxSunLightIntensity", 6.0f);
                InvokeEnv(et, env, "SetRealSystemMaxMoonLightIntensity", 0.37f);
                Log(string.Format("envprobe3 after dayTime={0} sunMax={1} moonMax={2}",
                    InvokeEnv(et, env, "GetRealSystemDayTime"),
                    InvokeEnv(et, env, "GetRealSystemMaxSunLightIntensity"),
                    InvokeEnv(et, env, "GetRealSystemMaxMoonLightIntensity")));
            }
            catch (Exception e) { Log("envprobe3 ex: " + e.Message); }
        }
        // Day-time knob (weather workstream A1): the environment object's
        // real-system day time is the day-night driver (RC_ENV_PROBE dump);
        // SetTrueSkyDayTime is a no-op without KG3D_TrueSkyX64.dll (game-client only).
        if (Env("RC_DAYTIME", "") != "")
        {
            float v;
            if (float.TryParse(Env("RC_DAYTIME", "0.5"), out v))
            {
                try
                {
                    object env = scene.GetEnvironment();
                    if (env != null)
                    {
                        InvokeEnv(env.GetType(), env, "SetRealSystemDayTime", v);
                        InvokeEnv(env.GetType(), env, "SetRealSystemTimezone", 0f);
                        object g = InvokeEnv(env.GetType(), env, "GetRealSystemDayTime");
                        Log(string.Format("daytime real-system set {0:F3} -> get {1}", v, g));
                    }
                    scene.SetTrueSkyDayTime(v);
                    Log(string.Format("daytime truesky set {0:F3} -> get {1:F3}", v, scene.GetTrueSkyDayTime()));
                }
                catch (Exception e) { Log("daytime ex: " + e.Message); }
            }
        }
        long winId = scene.AddOutputWindow("", panel.Handle.ToInt64(), 0);
        Log("winId=" + winId);

        // CLR scene-proxy route: read the managed KGSceneCLR.m_pScene field by
        // reflection (the byte-scan of the wrapper could not) and hand it to
        // the shim, which calls the engine camera getters SEH-guarded.
        if (Env("RC_CAM_CLR", "0") == "1" || Env("RC_CAM_ENGINESET", "1") != "0")
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
            // bounded region cache (the engine's streaming keeps several regions;
            // a single slot reloads on every border ping-pong - measured 16 loads
            // in a 17 s crossing run)
            int terrCache = 4;
            int.TryParse(Env("RC_TERR_CACHE", "4"), out terrCache);
            sampler = new TerrainSampler(physDll, mapPath, Log, terrCache);
        }
        catch (Exception e) { Log("TerrainSampler ex: " + e.Message); }

        // Prime the physics terrain loader before the engine starts streaming
        // (its first region load initialises the source reader; if the first
        // call happens after the engine's camera jump it can return all-zero
        // heights for the spawn region on 龙门寻宝).
        if (sampler != null)
        {
            sampler.Sample(0f, 0f);
            System.Threading.Thread.Sleep(300);
            sampler.Sample(0f, 0f);
        }

        // P5 probe: drive the game's own physics stack inside this host
        if (Env("RC_PHYS_PROBE", "0") == "1")
        {
            try { PhysicsProbe.Run(Log, mapPath); }
            catch (Exception e) { Log("physprobe ex: " + e.Message); }
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
                col = new FoliageCollision(fp, sp, 800f, Env("RC_OBST_FLAGS", "1") == "1");
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

        // D6 crash guard (registered host bypass, now OPT-IN): make the
        // engine's null material-store deref return E_FAIL instead of AVing.
        // Version-guarded in the shim. NOT default-on: a session with it active
        // produced a BEX64 jump-to-data (18:28:14, three overlapping instances
        // running) - the skipped cleanup may corrupt state later, so it needs
        // A/B evidence before it can ship. Enable with RC_PATCH_D6=1.
        if (CameraShim.Available && Env("RC_PATCH_D6", "0") == "1")
        {
            int prc = CameraShim.PatchD6();
            Log("patchD6 rc=" + prc + " " + CameraShim.PatchD6Info());
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
        // COPY POS row: copies the displayed position (the panel's pos line).
        hud.OnCopyPos = delegate
        {
            try
            {
                string pos = string.Format("pos {0:F0},{1:F0},{2:F0}", px, py, pz);
                Clipboard.SetText(pos);
                Log("copied pos to clipboard: " + pos);
            }
            catch (Exception e) { Log("clipboard copy failed: " + e.Message); }
        };
        float viewX = 0f, viewY = 0f, viewZ = 1f;   // spawn orientation (measured once)
        bool grounded = false;
        // JX3-modeled camera (engine_host_spike/CameraSystem.cs, ported)
        CameraSystem camSys = new CameraSystem();
        CameraObstruction camObst = new CameraObstruction();
        double.TryParse(Env("RC_CAM_HITWIN", Env("RC_CAM_HITWINDOW", "0.4")), out camObst.HitWindow);
        CameraShake camShake = new CameraShake();
        // near-plane ladder knob: clearance used by the obstruction response
        double clearanceOverride;
        if (double.TryParse(Env("RC_CAM_CLEARANCE", ""), out clearanceOverride) && clearanceOverride > 0.0)
            camObst.Clearance = clearanceOverride;
        // crossing guard (registered host stabilizer, on by default): with our
        // chattering bake hit the signed pull oscillates across the anchor and
        // the engine look-at flips the view 180 deg per flip (nausea). Floor
        // the pull at the anchor; RC_CAM_CROSS=1 restores the native crossing.
        camObst.NoCross = Env("RC_CAM_CROSS", "0") != "1";
        // degenerate-hit guard threshold (u); 0 disables (native behaviour)
        double hitMinDist;
        if (!double.TryParse(Env("RC_CAM_HITMIN", "3.0"), out hitMinDist) || hitMinDist < 0.0)
            hitMinDist = 0.0;
        // B5 final-camera wall gate: host band-aid, off by default (it can fire
        // at wall edges and adds a jump of its own; the pull + crossing guard
        // already keep the camera on the near side). RC_CAM_WALLGATE=1 restores.
        bool wallGate = Env("RC_CAM_WALLGATE", "0") == "1";
        // double-sided camera probes, default on (RC_CAM_BACKFACE=0 restores
        // the old front-only rule). The penetration recorder showed the
        // resolved camera on the far side of surfaces whose front faces point
        // at the camera (T1 sweep: reverse cast hit 1-11 u from the camera,
        // forward front-only probes blind), i.e. front-only can strand the
        // camera outside the obstruction set. A/B 2026-09-29: T1 sweep 192
        // event-frames -> 0; T2 hit=206 len=188 and T4 hit=186 len=168 exact;
        // 0 shake events. Registered host fix; exits with the real
        // FilterCamera pass (D1) once its winding rule is proven.
        bool probeFrontOnly = Env("RC_CAM_BACKFACE", "1") != "1";
        // raw scene backend in the camera probes (game mask 0x301 includes it).
        // D1/D4: unfiltered - it hits the player's own model (57 u at the user
        // spot -> camera slammed to 39) and exit/grazing faces near the origin.
        // Near walls are the bake's job (the bake is map geometry, no self);
        // scene hits closer than RC_CAM_SCENEMIN (default 80 u) are dropped.
        // RC_CAM_SCENERAY=0 removes the backend entirely.
        bool sceneRayCam = Env("RC_CAM_SCENERAY", "1") == "1";
        float sceneMin = 80f;
        float.TryParse(Env("RC_CAM_SCENEMIN", "80"), out sceneMin);
        bool playerHidden = false;
        CameraSettings cameraSettings = null;
        {
            // per-workstream config dir for feature builds (bin64\reborn_<slug>)
            string cfgDir = AppDomain.CurrentDomain.BaseDirectory;
            if (selfSlug != null)
            {
                string slugDir = Path.Combine(cfgDir, "reborn_" + selfSlug);
                if (Directory.Exists(slugDir)) cfgDir = slugDir;
            }
            double sc;
            if (double.TryParse(Env("RC_CAMERA_SCALE", ""), out sc) && sc > 0) camSys.UnitsPerMeter = sc;
            // Classic A/D default: STRAFE (shipped default.txt binds A/D to
            // STRAFELEFT/RIGHT; decoded hotkeys 0/76 classical branch =
            // ResponseWASDKey + Camera_EnableControl(CONTROL_STRAFE_*)).
            // RC_ADHABIT=turn opts into the turn-in-place habit (the free-view
            // TurnLeft/RightStart branch used by the joystick mode).
            adHabit = Env("RC_ADHABIT", "strafe").Trim().ToLowerInvariant();
            if (adHabit != "turn") adHabit = "strafe";
            string camCfg = Path.Combine(cfgDir, "camera.json");
            if (File.Exists(camCfg))
            {
                try { camSys.LoadConfig(camCfg); Log("camera config: " + camCfg); }
                catch (Exception e) { Log("camera config ex: " + e.Message); }
            }
            Log("ad habit: " + adHabit + " (RC_ADHABIT=strafe|turn)");
            camSys.SwitchMode(CameraSystem.MODE_CHARACTER);
            cameraSettings = CameraSettings.Load(
                editorRoot, mapPath, cfgDir, Log);
            cameraSettings.ApplyOperationMode();
            Log("opmode applied: " + cameraSettings.DescribeApplied());
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("MaxCameraDistance", cameraSettings.MaxCameraDistance);
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("MinCameraDistance", cameraSettings.MinCameraDistance);
            // Game-data pitch is NEGATIVE when the camera sits above the anchor
            // (looking down): g_Scene_tCameraRuntime fPitch default -0.35,
            // number.krl CameraInitPitch -0.17 / SprintCameraPitch -0.35, and
            // hotkeys.lua F11 passes -pi/12 for the standard behind view
            // ("pitch -15 deg"). The model pitch is the opposite (positive =
            // camera above, measured: model +0.15 -> engine view vpitch -0.31),
            // so negate on application (2026-09-30 start-angle research).
            camSys.Pitch = -cameraSettings.InitPitch;
            camSys.Yaw = cameraSettings.InitYaw;
            // deterministic test pose (camera A/B harness): override the loaded
            // yaw/pitch without touching the settings sources
            double poseOv;
            if (double.TryParse(Env("RC_CAM_PITCH", ""), out poseOv)) camSys.Pitch = poseOv;
            if (double.TryParse(Env("RC_CAM_YAW", ""), out poseOv)) camSys.Yaw = poseOv;
            // user decision 2026-09-30: both follow rows start at the max range
            // (fMaxCameraDistance; 广角 is already the client panel max). This
            // overrides the 1245 u client-number initial (C10) - target AND
            // init distance are max, so the follow camera holds at max instead
            // of easing back to the row value.
            double maxDistM = camSys.ClampDistanceUnits(cameraSettings.MaxCameraDistance)
                              / camSys.UnitsPerMeter;
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("TargetDistance", maxDistM);
            camSys.Rows[CameraSystem.MODE_CHARACTER].Set("InitCameraDistance", maxDistM);
            camSys.Rows[CameraSystem.MODE_SPRINT].Set("TargetDistance", maxDistM);
            camSys.Rows[CameraSystem.MODE_SPRINT].Set("InitCameraDistance", maxDistM);
            camSys.Distance = maxDistM * camSys.UnitsPerMeter;
            // deterministic test distance (world units): RC_CAM_DIST=100 puts
            // the camera close-up while RC_CAM_PITCH tilts it (repro harness)
            double distOv;
            if (double.TryParse(Env("RC_CAM_DIST", ""), out distOv) && distOv > 0.0)
            {
                camSys.Rows[CameraSystem.MODE_CHARACTER].Set("TargetDistance", distOv / camSys.UnitsPerMeter);
                camSys.Rows[CameraSystem.MODE_CHARACTER].Set("InitCameraDistance", distOv / camSys.UnitsPerMeter);
                camSys.Distance = distOv;
            }
            Log(string.Format("CameraSystem ready: mode={0} dist={1:F0}u height={2:F0}u units/m={3} op={4}",
                camSys.Mode, camSys.Distance,
                camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter, camSys.UnitsPerMeter,
                CameraOperationMode.Name(cameraSettings.OperationMode)));
        }
        float baseViewAngle = 1f;
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
            baseViewAngle = va;
            Log("view angle factor applied=" + va);
        }
        catch (Exception e) { Log("view angle: " + e.Message); }
        // P2-T1: fixed 15 Hz logic tick (engine KCharacter model) + integer cm.
        // Physics runs in whole 1/15 s ticks; the render/camera interpolate the
        // remaining fraction (P2-T3).
        float moveAcc = 0f;
        float lastTickX = 0f, lastTickY = 0f, lastTickZ = 0f;
        bool lastTickInit = false;
        long handle = 0, attachedHandle = -999;
        var model = new KGModelCLR();
        string curClip = null;
        float curYaw = 0f;
        float lastModelX = float.MaxValue, lastModelY = float.MaxValue, lastModelZ = float.MaxValue, lastModelYaw = float.MaxValue;

        Func<string, int> setClip = delegate(string path)
        {
            if (path == curClip) return 0;
            try
            {
                int pr = model.PlayAnimation(path, 0, 1.0f, 0);
                Log("clip -> " + path + " (" + pr + ")");
                curClip = path;
                return pr;
            }
            catch (Exception e) { Log("setClip ex: " + e.Message); return -1; }
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
                // default test spawn on 龙门寻宝 (override with RC_SPAWN=x,y,z)
                px = 18991f; py = 962f; pz = 33853f;
            }
            // Spawn-extent validation (crash guard): on the 4x4 海岛绝境 map an
            // out-of-extent actor AVs the engine render stack
            // (KG3DEngineDX11EX64+0x12282B3, reproduced 2026-10-05; see
            // docs/movement/VOID_SPAWN_CRASH_TRIAGE.md). Clamp test spawns into
            // the map extent (one cell margin) with a loud log; in play the
            // actor cannot leave the extent (x/z are server/map-owned).
            if (sampler != null)
            {
                float loX = sampler.ExtentMinX + 100f, hiX = sampler.ExtentMaxX - 100f;
                float loZ = sampler.ExtentMinZ + 100f, hiZ = sampler.ExtentMaxZ - 100f;
                float cx = px, cz = pz;
                if (cx < loX) cx = loX; else if (cx > hiX) cx = hiX;
                if (cz < loZ) cz = loZ; else if (cz > hiZ) cz = hiZ;
                if (cx != px || cz != pz)
                {
                    Log(string.Format(
                        "spawn clamped into map extent: ({0:F0},{1:F0}) -> ({2:F0},{3:F0}) [extent {4:F0}..{5:F0} x {6:F0}..{7:F0}]",
                        px, pz, cx, cz, sampler.ExtentMinX, sampler.ExtentMaxX,
                        sampler.ExtentMinZ, sampler.ExtentMaxZ));
                    px = cx; pz = cz;
                    // A clamped point can still land on sub-sea-level ground or a
                    // hole, where the engine render stack AVs (2026-10-06, with the
                    // camera workstream active; see VOID_SPAWN_CRASH_TRIAGE.md §2).
                    // Relocate to the nearest in-extent point with solid ground
                    // above sea level (bounded spiral over real loader data).
                    float g;
                    if (!sampler.SampleGround(px, pz, out g) || g <= 0f)
                    {
                        float[] sdx = new float[] { 1f, 0f, -1f, 0f, 1f, -1f, 1f, -1f };
                        float[] sdz = new float[] { 0f, 1f, 0f, -1f, 1f, 1f, -1f, -1f };
                        bool found = false;
                        float bx = px, bz = pz, bg = 0f;
                        for (int ring = 1; ring <= 64 && !found; ring++)
                        {
                            float r = ring * 1024f;
                            for (int k = 0; k < 8 && !found; k++)
                            {
                                float tx = px + sdx[k] * r, tz = pz + sdz[k] * r;
                                if (tx < loX || tx > hiX || tz < loZ || tz > hiZ) continue;
                                float tg;
                                if (sampler.SampleGround(tx, tz, out tg) && tg > 0f)
                                {
                                    bx = tx; bz = tz; bg = tg; found = true;
                                }
                            }
                        }
                        if (found)
                        {
                            Log(string.Format(
                                "spawn relocated to solid ground: ({0:F0},{1:F0}) -> ({2:F0},{3:F0}) ground={4:F0}",
                                px, pz, bx, bz, bg));
                            px = bx; pz = bz;
                        }
                        else
                            Log("spawn guard: no solid above-sea-level point found in extent; keeping clamped spawn");
                    }
                }
            }
            // The physics terrain loader tracks the engine's streamed terrain:
            // right after the camera jumps it can return all-zero heights for
            // the spawn region. The engine streams around the PLAYER MODEL, which
            // is placed after this block - sample once here and, when the data is
            // not ready yet, settle after placePlayer below (camera-only warmup
            // was tried and does not trigger the stream).
            // RC_SPAWN_Y=1 keeps the provided absolute Y (indoor test spawns:
            // floors above terrain are scene meshes, not terrain).
            if (Env("RC_SPAWN_Y", "0") != "1")
            {
                py = sampler != null ? sampler.Sample(px, pz) : 0f;
                if (sampler != null && py == 0f)
                    Log("spawn terrain not streamed yet - settling after actor placement");
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

        // Spawn ground settle (deferred): the engine streams terrain around the
        // player model; until the spawn region arrives the loader returns zeros
        // (observed with RC_SPAWN in a region the map-default camera had not
        // streamed). Pump frames, then re-place the actor once real heights
        // arrive (same-name AddDummyModel keeps the handle).
        if (Env("RC_SPAWN_Y", "0") != "1" && sampler != null)
        {
            bool settleDbg = Env("RC_SETTLE_DBG", "0") == "1";
            long warm = Environment.TickCount;
            // The loader can be mid-stream right after the actor appears: wait
            // until the sampled value stops changing (bounded 2 s), then accept
            // it. Waiting for a NON-zero value is wrong - a genuine 0-height
            // spot (e.g. the low ground west of the 龙门 mesa) stalled 10 s and
            // never recovered (2026-10-04 run).
            float g = sampler.Sample(px, pz);
            float prev = g;
            Pump(engine, 250);
            g = sampler.Sample(px, pz);
            while (g != prev && Environment.TickCount - warm < 2000)
            {
                if (settleDbg)
                    Log(string.Format("spawn settle sample={0:F1} t={1}ms", g, Environment.TickCount - warm));
                prev = g;
                Pump(engine, 250);
                g = sampler.Sample(px, pz);
            }
            if (Math.Abs(g - py) > 0.5f)
            {
                py = g;
                placePlayer(px, py, pz, curYaw);
            }
            Log(string.Format("spawn ground settle took {0}ms py={1:F0}",
                Environment.TickCount - warm, py));
        }

        // ---------------- target selection state (Targeting.cs) ----------------
        // Target HUD art/layout comes from the game client's own UI files
        // (TargetTarget.ini + .UITex atlases + ui/Font), extracted by
        // tools/netcode/ui/extract_target_frame.py; RC_UI_ROOT points at the
        // extracted tree. No hand-drawn substitute: missing art draws nothing.
        var targetSelector = new TargetSelector();
        UiTargetFrameRenderer targetUi = null;
        try
        {
            string uiRoot = Env("RC_UI_ROOT", "");
            if (uiRoot.Length > 0)
            {
                string fontDir = Env("RC_UI_FONT_DIR",
                    @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\ui\Font");
                targetUi = new UiTargetFrameRenderer(uiRoot, fontDir,
                    Path.Combine(uiRoot, "ui", "Scheme", "Elem"));
                foreach (string w in targetUi.Warnings) Log("target ui: " + w);
            }
            else Log("target ui: RC_UI_ROOT not set - target HUD art disabled");
        }
        catch (Exception e) { Log("target ui ex: " + e.Message); }
        var targetFrame = new TargetFrameControl(targetUi);
        targetFrame.PlaceOver(form);
        bool targetHudOn = Env("RC_TARGET_HUD", "0") != "0";

        // ---------------- in-world target indicator (KRLTarget visuals) ----------------
        // The game's own selection visuals come from ForceRelationCareTable
        // (represent/common/force_relation_care.txt, loaded by KRLTarget): each
        // relation row maps to
        //   SFXFile = data/source/other/HD特效/其他/Pss/选择特效aXXX_hd.pss
        //   SFXEn   = data/source/other/HD特效/其他/Pss/J_角色箭头面向.pss
        // (relation 2 = Enemy -> a002). The engine shows them attached to the
        // target; we spawn the same client assets at the selected target via
        // AddDummyModel (the engine loads PSS + textures from the game client's
        // own VFS). No hand-drawn substitute: missing art draws nothing.
        bool indEnabled = Env("RC_INDICATOR", "1") != "0";
        string indSel = Env("RC_INDICATOR_SEL",
            "data\\source\\other\\HD\u7279\u6548\\\u5176\u4ED6\\Pss\\\u9009\u62E9\u7279\u6548a002_hd.pss");
        string indArrow = Env("RC_INDICATOR_ARROW",
            "data\\source\\other\\HD\u7279\u6548\\\u5176\u4ED6\\Pss\\J_\u89D2\u8272\u7BAD\u5934\u9762\u5411.pss");
        float indY = 0f, indArrowY = 8f, indArrowScale = 0.5f, indArrowYaw = 0f;
        {
            float v;
            if (float.TryParse(Env("RC_INDICATOR_Y", ""), out v)) indY = v;
            if (float.TryParse(Env("RC_INDICATOR_ARROW_Y", ""), out v)) indArrowY = v;
            if (float.TryParse(Env("RC_INDICATOR_ARROW_SCALE", ""), out v)) indArrowScale = v;
            if (float.TryParse(Env("RC_INDICATOR_ARROW_YAW", ""), out v)) indArrowYaw = v;
        }
        bool indAlways = Env("RC_INDICATOR_ALWAYS", "0") == "1";
        TargetEntity dummyTarget = null;
        float dummyYaw = 0f;

        // ---------------- target dummy (sandbox-target-dummy) ----------------
        // One 试炼木桩 near the spawn point: RepresentID -> engine model path
        // (same actor space the editor NPC palette uses), placed RC_DUMMY_DIST
        // units along the measured view direction, standing on sampled terrain.
        // RC_DUMMY=0 disables. Idle animation via GetRepresentAniPath.
        try
        {
            int dummyRid = 35901;   // 初级试炼木桩 (ZhuChengMuZhuang zone)
            int.TryParse(Env("RC_DUMMY", "35901"), out dummyRid);
            if (dummyRid > 0)
            {
                float dummyDist = 400f;
                float.TryParse(Env("RC_DUMMY_DIST", "400"), out dummyDist);
                float dx = viewX, dz = viewZ;
                float dl = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dl < 1e-4f) { dx = 0f; dz = 1f; } else { dx /= dl; dz /= dl; }
                float tx = px + dx * dummyDist;
                float tz = pz + dz * dummyDist;
                float ty = sampler != null ? sampler.Sample(tx, tz) : py;
                if (ty == 0f) ty = py;
                string dummyModel = scene.GetRepresentModelPath(dummyRid);
                string dummyAni = scene.GetRepresentAniPath(dummyRid);
                var tpos = new CLRfloat3(); tpos.x = tx; tpos.y = ty; tpos.z = tz;
                float tyaw = (float)Math.Atan2(-dx, -dz);   // face the player
                dummyYaw = tyaw;
                float thalf = tyaw * 0.5f;
                var trot = new CLRfloat4(); trot.x = 0f; trot.y = (float)Math.Sin(thalf); trot.z = 0f; trot.w = (float)Math.Cos(thalf);
                var tscl = new CLRfloat3(); tscl.x = 1f; tscl.y = 1f; tscl.z = 1f;
                long dummyHandle = 0;
                if (dummyModel != null && dummyModel.Length > 0)
                    dummyHandle = scene.AddDummyModel("target_dummy", dummyModel.Replace('/', '\\'), tpos, trot, tscl);
                Log(string.Format("target dummy rid={0} model='{1}' ani='{2}' handle={3} at ({4:F0},{5:F0},{6:F0})",
                    dummyRid, dummyModel, dummyAni, dummyHandle, tx, ty, tz));
                if (dummyHandle > 0)
                {
                    // Target-frame values from the shipped sNpcTemplate row
                    // (docs/pvp/TARGET_DUMMY_RESEARCH.md: 初级试炼木桩 Lv131,
                    // MaxLife 500,000,000); RC_DUMMY_* overrides.
                    var tent = new TargetEntity();
                    tent.Handle = dummyHandle;
                    tent.Name = Env("RC_DUMMY_NAME", "\u521D\u7EA7\u8BD5\u70BC\u6728\u6869"); // 初级试炼木桩
                    if (!int.TryParse(Env("RC_DUMMY_LEVEL", "131"), out tent.Level)) tent.Level = 131;
                    if (!long.TryParse(Env("RC_DUMMY_HP", "500000000"), out tent.MaxHp)) tent.MaxHp = 500000000L;
                    tent.Hp = tent.MaxHp;
                    tent.X = tx; tent.Y = ty; tent.Z = tz;
                    targetSelector.Add(tent);
                    dummyTarget = tent;
                    Log(string.Format("target entity registered: {0} lv{1} hp={2} (Tab = facing cone search)",
                        tent.Name, tent.Level, tent.MaxHp));
                }
                if (dummyHandle > 0 && dummyAni != null && dummyAni.Length > 0)
                {
                    var dummyAnim = new KGModelCLR();
                    dummyAnim.AttachModel(dummyHandle);
                    Log("target dummy ani -> " + dummyAnim.PlayAnimation(dummyAni.Replace('/', '\\'), 0, 1.0f, 0));
                }
            }
            else Log("target dummy disabled (RC_DUMMY=0)");
        }
        catch (Exception e) { Log("target dummy ex: " + e.Message); }

        // camera yaw from the measured engine view direction (camera -> anchor)
        if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
        {
            camSys.Yaw = Math.Atan2(-viewZ, -viewX);
            // classical movement runs along the facing: spawn facing the view
            curYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));
            moveYaw = camSys.Yaw;
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
        // C1/C2 input core: the real binding table (ui/hotkey/default.txt +
        // bindings.ini) is decoded at startup; movement commands below are
        // dispatched from it instead of hardcoded keys.
        HotkeyTable hotkeys = HotkeyTable.Load(Env("RC_HOTKEY_DIR", ""), Log);
        // binding context ("" = normal play). Rows from other contexts must not
        // fire here (e.g. MINIGAME_JUMP on W). RC_HOTKEY_CTX is the test path
        // until the runtime contexts (morph/summon/minigame) exist.
        hotkeys.Context = Env("RC_HOTKEY_CTX", "").Trim();
        Log("hotkeys: context='" + hotkeys.Context + "'");
        {
            string[] probe = new string[] { "MOVEFORWARD", "MOVEBACKWARD", "STRAFELEFT",
                "STRAFERIGHT", "TURNLEFT", "TURNRIGHT", "JUMP", "TOGGLERUN", "TOGGLEAUTORUN" };
            for (int hi = 0; hi < probe.Length; hi++)
                Log("hotkey " + probe[hi] + " = " +
                    HotkeyTable.Describe(hotkeys.Get(probe[hi])));
        }
        bool pTurnL = false, pTurnR = false, autorunOn = false;
        // base character actions (decoded handlers): sit = OnUseSkill(17 打坐) /
        // Stand(); sheath = SetSheath flag (gates: sitting blocks it; the
        // fight/bird/horse/tower/buff gates are always false in the host).
        bool sitting = false, sheathOn = false;
        // mount core (client/MountSystem.cs, W4 horse phase 1): RC_MOUNT_RIDE=0..3
        // selects the horse model; RC_HORSE_* / RC_MOUNT_CLIP_* override the
        // extracted ride_rush values.
        MountState mount = null;
        {
            int mride = 0;
            int.TryParse(Env("RC_MOUNT_RIDE", "0"), out mride);
            mount = new MountState(mride,
                Env("RC_HORSE_MODEL", ""),
                Env("RC_HORSE_CLIP_IDLE", ""), Env("RC_HORSE_CLIP_RUN", ""),
                Env("RC_HORSE_CLIP_JUMP", ""),
                Env("RC_MOUNT_CLIP_RIDE", ""), Env("RC_MOUNT_CLIP_JUMP", ""));
            Log("mount: data ride=" + mride + " model='" + mount.Model + "' rider='" + mount.RiderClip + "'");
        }
        long sheathDrawUntil = 0;   // draw transition window (拔剑 start clip)
        int unhandledCmd = 0;
        string lastUnhandled = "";
        bool demoMove = Env("RC_DEMO_MOVE", "0") == "1";
        probeControl = Env("RC_PROBE_CONTROL", "0") == "1";
        bool sprintTest = Env("RC_SPRINT_TEST", "0") == "1";
        bool sprintT1 = false, sprintT2 = false, sprintT3 = false, sprintT4 = false;
        bool mvAuth = false, mvAuthOff = false, mvJumped = false, mvTurn = false, mvTurnDone = false;
        bool mvStrafe = false, mvStrafeDone = false, mvBack = false, mvBackDone = false, mvDrop = false, mvDone = false;
        bool mvSit = false, mvSitDone = false, mvSheath = false, mvSheathDone = false;
        bool mountTest = Env("RC_MOUNT_TEST", "0") == "1";
        bool mtMounted = false, mtFwd = false, mtJump1 = false, mtJump2 = false, mtStop = false;
        bool mtMount2 = false, mtDown = false, mtDone = false;
        bool mvWA = false, mvWADone = false, mvWD = false, mvWDDone = false;
        int demoRmbWa = 0;
        int.TryParse(Env("RC_DEMO_RMBWA", "0"), out demoRmbWa);   // 1 = hold RMB, 2 = + orbit drag
        int demoRmbStrafe = 0;
        int.TryParse(Env("RC_DEMO_RMBSTRAFE", "0"), out demoRmbStrafe);   // 1 = hold RMB during the A-strafe phase
        float strafeX0 = 0f, strafeZ0 = 0f, backX0 = 0f, backZ0 = 0f, waX0 = 0f, waZ0 = 0f, wdX0 = 0f, wdZ0 = 0f;
        float waYaw0 = 0f, wdYaw0 = 0f;
        double waCam0 = 0.0, wdCam0 = 0.0;
        float strafeYaw0 = 0f, backYaw0 = 0f, turnYaw0 = 0f;
        double strafeCam0 = 0.0, backCam0 = 0.0, turnCam0 = 0.0;
        long modeSwitchAt = 0;
        long.TryParse(Env("RC_MODE_SWITCH_AT", "0"), out modeSwitchAt);
        bool modeSwitched = false;
        bool demo = Env("RC_DEMO", "0") == "1", demoJumped = false, demoJumped2 = false, demoTurned = false, demoSkilled = false;
        bool demoCollide = Env("RC_DEMO_COLLIDE", "0") == "1", demoTeleported = false;
        bool demoCrossBack = Env("RC_CROSS_BACK", "0") == "1", demoCrossBackDone = false;
        bool demoCrossLogged = false;
        bool supDbg = Env("RC_SUPDBG", "0") == "1";
        int supDbgN = 0;
        bool camDemo = Env("RC_CAM_DEMO", "0") == "1";
        bool camZoomSeq = Env("RC_CAM_ZOOMSEQ", "0") == "1";
        int zoomSeqStep = -1;
        bool nineRay = Env("RC_CAM_9RAY", "0") == "1";      // alternate 9-ray probe set
        string camMode = Env("RC_CAM_MODE", "");            // force a camera mode row
        bool demoTeleport = Env("RC_COL_TELEPORT", "0") == "1";
        float demoDirX = 0f, demoDirZ = 0f;
        {
            string[] dd = Env("RC_DEMO_DIR", "0,1").Split(',');
            if (dd.Length >= 2) { float.TryParse(dd[0], out demoDirX); float.TryParse(dd[1], out demoDirZ); }
        }
        bool cDown = false, teleportToStructure = false;
        bool iDown = false;   // "I" toggles the info panel (alias of Esc)
        bool divDown = false;
        TargetEntity indTarget = null;
        // Command executor (host equivalent of the ui/script hotkey handlers):
        // the movement set is dispatched from the real table; other commands
        // are counted as unhandled - no fake handlers for combat/UI yet.
        Action<string, bool> runCommand = delegate(string name, bool down)
        {
            switch (name)
            {
                case "MOVEFORWARD": pW = down; break;
                case "MOVEBACKWARD": pS = down; if (down) autorunOn = false; break;
                case "STRAFELEFT": pA = down; if (down) autorunOn = false; break;
                case "STRAFERIGHT": pD = down; if (down) autorunOn = false; break;
                case "TURNLEFT": pTurnL = down; break;
                case "TURNRIGHT": pTurnR = down; break;
                case "JUMP":
                    if (down) { if (!spaceDown) { spaceDown = true; jumpPressed = true; } }
                    else spaceDown = false;
                    break;
                case "TOGGLERUN":
                    if (down)
                    {
                        if (!divDown)
                        {
                            divDown = true;
                            walkMode = !walkMode;
                            Log("movement mode: " + (walkMode ? "WALK" : "RUN") + " (TOGGLERUN)");
                        }
                    }
                    else divDown = false;
                    break;
                case "TOGGLEAUTORUN":
                    if (down) { autorunOn = !autorunOn; Log("autorun: " + (autorunOn ? "on" : "off")); }
                    break;
                case "TOGGLESITDOWN":
                    // decoded 0/98: nMoveState == ON_SIT ? Stand() : OnUseSkill(17, ..)
                    if (down)
                    {
                        if (sitting) { sitting = false; Log("sit: stand (Stand)"); }
                        else { sitting = true; Log("sit: down (OnUseSkill 17 打坐)"); }
                    }
                    break;
                case "TOGGLESHEATH":
                    // decoded 0/97 gates: sit/death/fight/bird/horse/tower/buff
                    // block it; the host only models the sit gate (the others
                    // are always false).
                    if (down)
                    {
                        if (sitting) Log("sheath rejected: sitting");
                        else
                        {
                            sheathOn = !sheathOn;
                            if (sheathOn) sheathDrawUntil = (long)Environment.TickCount + 800;
                            Log("sheath: " + (sheathOn ? "drawn" : "sheathed"));
                        }
                    }
                    break;
                case "RIDEHORSE":
                    // decoded action RideHorse();/DownHorse(); (default hotkey T=84).
                    // Guards: mount needs ground + not sitting (the decoded
                    // [+0x208]/[+0x160] guards map to host state; inventory-side
                    // preconditions are deviation 2 in MountSystem.cs).
                    if (down)
                    {
                        if (!mount.Mounted)
                        {
                            if (!grounded) Log("mount rejected: airborne (RideHorse guard)");
                            else if (sitting) Log("mount rejected: sitting");
                            else mount.Mount(scene, px, py, pz, curYaw, Log);
                        }
                        else mount.Dismount(scene, Log);
                    }
                    break;
                default:
                    unhandledCmd++;
                    lastUnhandled = name;
                    break;
            }
        };
        // J3 sprint: double-tap detection on the movement command set, exact
        // decoded window 250 ms (hotkeys 0/36 Hotkey.GetKeyTimeInterval < 250),
        // gated by the ResponseWASDKey rules (down + isDouble; tower/bird/horse
        // are always false in the host). Engine Sprint(true)/skill 6754 are
        // not modeled (J3 open) - logged, never faked as a speed.
        System.Collections.Generic.Dictionary<string, long> cmdDownAt =
            new System.Collections.Generic.Dictionary<string, long>();
        System.Collections.Generic.Dictionary<string, bool> cmdHeld =
            new System.Collections.Generic.Dictionary<string, bool>();
        Func<string, bool> sprintKey = delegate(string name)
        {
            return name == "MOVEFORWARD" || name == "MOVEBACKWARD"
                || name == "STRAFELEFT" || name == "STRAFERIGHT"
                || name == "TURNLEFT" || name == "TURNRIGHT";
        };
        Action<string, bool> keyCommand = delegate(string name, bool down)
        {
            bool isDouble = false;
            if (down)
            {
                bool held;
                if (!cmdHeld.TryGetValue(name, out held) || !held)
                {
                    long last;
                    long t = (long)Environment.TickCount;
                    if (cmdDownAt.TryGetValue(name, out last) && t - last < 250)
                        isDouble = true;
                    cmdDownAt[name] = t;
                    cmdHeld[name] = true;
                }
            }
            else
            {
                cmdHeld[name] = false;
            }
            runCommand(name, down);
            if (isDouble && sprintKey(name) && !sprintOn)
            {
                sprintOn = true;
                Log("sprint: StartSprint (" + name + " double-tap 250ms; skill 6754, Sprint(true) engine state not modeled - J3 open)");
            }
            if (!down && sprintOn && sprintKey(name))
            {
                sprintOn = false;
                Log("sprint: EndSprint (Sprint(false), SetSprintTopPoint)");
            }
        };
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
            curYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));
            moveYaw = camSys.Yaw;
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
            if (Env("RC_CAM_ENGINESET", "1") != "0") return;
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
        // Host change 2026-09-30 (user decision): the zoom moved off the wheel
        // onto the +/- keys; the wheel is inert.
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
        // Click = select under cursor; an empty pick clears the target. This is
        // the client's own CAMERAORSELECTORMOVE semantics (LMB: rotate camera or
        // select under cursor; down/up -> CameraOrSelectOrMoveStart/Stop(0),
        // ui/hotkey/bindings.ini:309-313) with the client's clear path
        // SetTarget(player, NO_TARGET, 0) (KTarget::SetTarget accepts NO_TARGET=1;
        // docs/controls/JX3_TARGET_SELECTION.md section 6).
        Action<int, int> clickSelectAt = delegate(int cxp, int cyp)
        {
            float ccx = 0f, ccy = 0f, ccz = 0f;
            scene.GetCameraPos(ref ccx, ref ccy, ref ccz);
            double w = Math.Max(1, panel.ClientSize.Width);
            double h = Math.Max(1, panel.ClientSize.Height);
            double nx = (cxp - w / 2.0) / (w / 2.0);
            double ny = (h / 2.0 - cyp) / (h / 2.0);
            double fov = cameraSettings.WidAngleDeg > 0 ? cameraSettings.WidAngleDeg : 50.0;
            TargetEntity picked = targetSelector.Pick(ccx, ccy, ccz, px, py + 90f, pz,
                (float)nx, (float)ny, fov);
            if (picked != null)
            {
                targetSelector.Current = picked;
                Log("target=" + picked.Name + " (click pick, cursor)");
            }
            else if (targetSelector.Current != null)
            {
                targetSelector.Current = null;
                Log("click: deselect (nothing under cursor)");
            }
            else Log("click: no target under cursor");
        };
        MouseEventHandler onMouseUp = delegate(object s, MouseEventArgs e)
        {
            // S7: a press that never moved never locked the cursor - that press
            // was a click and the camera was not rotated.
            bool leftClick = e.Button == MouseButtons.Left && lmbDown && !mouseLocked;
            if (e.Button == MouseButtons.Left) lmbDown = false;
            else if (e.Button == MouseButtons.Right) rmbDown = false;
            dragArmed = false;
            // drag ended: release the cursor (both modes - the decoded client
            // only locks the cursor while dragging; no mode keeps it locked)
            if (!lmbDown && !rmbDown && mouseLocked) unlockMouse();
            // click (no drag) = select the target under the cursor; an empty
            // pick deselects. Host ray approximation (no world->screen in the
            // managed host); rendering medium only, the selection model follows
            // the client (docs/controls/JX3_TARGET_SELECTION.md section 6).
            if (leftClick)
            {
                System.Drawing.Point cp = panelPoint(s, e);
                clickSelectAt(cp.X, cp.Y);
            }
        };
        MouseEventHandler onMouseMove = delegate(object s, MouseEventArgs e)
        {
            // Decoded client (corrected 2026-10-03): the camera rotates only
            // while an LMB/RMB drag is held, in BOTH modes - Scene.lua starts
            // Camera_BeginDrag on button-down and a plain mouse move never
            // rotates. Scene_LockMouseRotation is a LockInputControl flag bit
            // (engine 0x180b00950), NOT an always-rotate / cursor-hide.
            if (!lmbDown && !rmbDown) return;
            if (!dragArmed) return;
            System.Drawing.Point p = panelPoint(s, e);
            if (!mouseLocked)
            {
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
        // Real default binding (ui/hotkey/default.txt): CAMERAZOOMIN/OUT on the
        // wheel (codes 256/257) -> Camera_Zoom(0.9 / 1.1). Restored 2026-10-01
        // for classic-control completeness (the +/- keys stay as a host extra;
        // the 2026-09-30 wheel-inert host deviation A12 is superseded).
        MouseEventHandler onWheel = delegate(object s, MouseEventArgs e)
        {
            if (e.Delta > 0) camSys.ZoomBy(-1.0);        // CAMERAZOOMIN  x0.9
            else if (e.Delta < 0) camSys.ZoomBy(1.0);    // CAMERAZOOMOUT x1.1
        };
        Control[] hitTargets = new Control[] { panel };
        foreach (Control c in hitTargets)
        {
            c.MouseDown += onMouseDown;
            c.MouseUp += onMouseUp;
            c.MouseMove += onMouseMove;
            c.MouseWheel += onWheel;
        }
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                // SEARCH_ENEMY (Tab) / SELECT_PREV_TARGET (Ctrl+Tab), target.lua
                targetSelector.Cycle(px, pz, curYaw, e.Control, Log);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            if (e.KeyCode == Keys.ShiftKey) shiftDown = true;
            // real binding table first: movement commands dispatch through the
            // game's own rows (W/Up, S/Down, A, D, Left, Right, Space, Num/, G)
            System.Collections.Generic.List<string> hcmds =
                hotkeys.Match((int)e.KeyCode, e.Control, e.Shift, e.Alt);
            for (int hi = 0; hi < hcmds.Count; hi++) keyCommand(hcmds[hi], true);
            // host/test keys outside the movement command set
            if (e.KeyCode == Keys.D1 && !oneDown) { oneDown = true; skillPressed = true; }
            else if (e.KeyCode == Keys.C && !cDown) { cDown = true; teleportToStructure = true; }
            else if (e.KeyCode == Keys.I && !iDown) { iDown = true; hud.ToggleInfo(); hud.UpdateLayered(); }
            else if (e.KeyCode == Keys.F7 || e.KeyCode == Keys.OemQuestion)
            {
                // operation-mode switch (host keys: "/" and F7; the real client
                // switches in the UISetting_Operation_Switch panel and has no
                // default hotkey - docs/controls/OPERATION_MODES_PLAN.md)
                cameraSettings.OperationMode =
                    cameraSettings.OperationMode == CameraOperationMode.Joystick
                        ? CameraOperationMode.Classical : CameraOperationMode.Joystick;
                cameraSettings.ApplyOperationMode();
                Log("opmode applied: " + cameraSettings.DescribeApplied());
                // mode switch never locks the cursor; release a drag lock
                if (!lmbDown && !rmbDown) unlockMouse();
            }
            else if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
            {
                // CameraZoomIn: Camera_Zoom(0.9) (moved off the wheel, 2026-09-30)
                camSys.ZoomBy(-1.0);
            }
            else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
            {
                // CameraZoomOut: Camera_Zoom(1.1)
                camSys.ZoomBy(1.0);
            }
            else if (e.KeyCode == Keys.F11)
            {
                // Camera reset: behind the character, distance 1x. hotkeys.lua
                // CameraReset -> Camera_SetForceReset(yaw, -pi/12, 1): game
                // pitch -15 deg (camera above) -> model +pi/12.
                camSys.SetMaxDistance(camSys.ClampDistanceUnits(
                    camSys.Row.F("InitCameraDistance", 12.45) * camSys.UnitsPerMeter) / camSys.UnitsPerMeter);
                camSys.Yaw = cameraYawBehind();
                camSys.Pitch = Math.PI / 12.0;
                alignAim();
                Log("camera reset: behind character, game pitch -15deg (model +15)");
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
            else if (e.KeyCode == Keys.F5)
            {
                // host test key (P3): cycle the camera rows. No gameplay trigger
                // (WW removed 2026-09-30); rows are test-reachable only.
                string[] rows = new string[] {
                    CameraSystem.MODE_CHARACTER, CameraSystem.MODE_SPRINT,
                    CameraSystem.MODE_CARRIER, CameraSystem.MODE_AIR_COMBAT,
                    CameraSystem.MODE_NPC_DIALOG, CameraSystem.MODE_GOD };
                int ridx = 0;
                for (int i = 0; i < rows.Length; i++) if (rows[i] == camSys.Mode) ridx = i;
                string nextRow = rows[(ridx + 1) % rows.Length];
                camSys.SwitchMode(nextRow, false);
                Log("camera row -> " + nextRow + " (F5 cycle)");
            }
            else if (e.KeyCode == Keys.F6 || e.KeyCode == Keys.F8)
            {
                // host test key (P3): base FOV +/- 5 deg (factor = angle / 0.837757)
                double d5 = 5.0 * Math.PI / 180.0;
                double ang = baseViewAngle * VideoSettings.DefaultAngle +
                             (e.KeyCode == Keys.F8 ? d5 : -d5);
                if (ang < 10.0 * Math.PI / 180.0) ang = 10.0 * Math.PI / 180.0;
                if (ang > 170.0 * Math.PI / 180.0) ang = 170.0 * Math.PI / 180.0;
                baseViewAngle = (float)(ang / VideoSettings.DefaultAngle);
                try { scene.SetViewAngleFactor(baseViewAngle); } catch (Exception) { }
                Log(string.Format("camera fov base -> {0:F1} deg (factor {1:F3})",
                    ang * 180.0 / Math.PI, baseViewAngle));
            }
            else if (e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
            {
                // host test key (P3): follow distance +/- 100 u
                double d = camSys.Distance + (e.KeyCode == Keys.PageUp ? 100.0 : -100.0);
                if (d < 100.0) d = 100.0;
                if (d > 5000.0) d = 5000.0;
                camSys.Distance = d;
                Log(string.Format("camera distance -> {0:F0} u (PgUp/PgDn)", d));
            }
        };
        form.KeyUp += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey) shiftDown = false;
            System.Collections.Generic.List<string> hcmds =
                hotkeys.Match((int)e.KeyCode, e.Control, e.Shift, e.Alt);
            for (int hi = 0; hi < hcmds.Count; hi++) keyCommand(hcmds[hi], false);
            if (e.KeyCode == Keys.D1) oneDown = false;
            else if (e.KeyCode == Keys.C) cDown = false;
            else if (e.KeyCode == Keys.I) iDown = false;
        };
        panel.Focus();
        // Esc toggles the information panel (open <-> close) no matter which
        // child window has focus: the engine's native child window can hold
        // focus, where the form's KeyPreview would never see the key.
        var escFilter = new EscKeyFilter();
        escFilter.OnEscape = delegate
        {
            unlockMouse();
            targetSelector.Current = null;
            hud.ToggleInfo();
            hud.UpdateLayered();
        };
        Application.AddMessageFilter(escFilter);

        // ---------------- main loop ----------------
        // table values converted from 15 logic frames/s into continuous seconds
        // (1 world unit = 1 cm; exact 15 Hz integer model is the next movement pass)
        // Real table values at the documented gameplay frame rate (GAME_FPS=16,
        // "16帧等于1秒", UNIT_SCALE...md §2): walk 6 / run 20 u/frame -> 96 / 320
        // u/s. Cross-check: the official UI shows 跑步速度 5 尺/秒 and
        // 20 u/frame * 16 fps = 320 u/s = 5 * 64 u (1 尺 = 64 u). Host controls:
        // default RUN, Num/ toggles WALK, hold Shift for a 10x testing speed.
        float pGravity = -2475f;   // school-0 J0 gravity (11 u/f2) as u/s2; J0 v0 = 1350 u/s
        // 二段跳 / jump chain (docs/movement/JX3_DOUBLE_JUMP_RESEARCH.md): per-press
        // takeoff triples from settings/JumpParam.tab (client/JumpTable.cs),
        // converted at the verified 15 Hz logic tick: v[u/s] = vz*15, g[u/s2] = g*225.
        //   flip  = one extra jump with the J0 profile (the real 二段跳; provisional
        //           until the J1 burst/End phase trigger is decoded)
        //   chain = the raw J1..MaxJumpCount 轻功 rows (ballistic shortcut, high)
        string djumpMode = Env("RC_DJUMP", "flip");
        // Unit calibration (docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md): 1 u = 1 cm,
        // and the movement spec's in-game jump is apex 192 u / 1.09 s air
        // (REBORN_JUMP_FALL_SPEC.md; MapSpike used 703 u/s, 1289 u/s^2). The raw table
        // triple (90/11 u/frame) gives 368 u = 3.7 m - ~2x too high. Scaling takeoff
        // AND gravity by 100/192 keeps the 1.09 s air time and realises 1.92 m.
        float jumpScale = 0.52f;
        float.TryParse(Env("RC_JUMP_SCALE", "0.52"), out jumpScale);
        if (jumpScale <= 0f) jumpScale = 1f;
        int jumpSchool = 0;
        int.TryParse(Env("RC_JUMP_SCHOOL", "0"), out jumpSchool);
        if (jumpSchool < 0 || jumpSchool >= JumpTable.MaxJumpCount.Length) jumpSchool = 0;
        // Authored f1b02yd二段跳a.tani resolves but AVs the host (documented);
        // the underlying .ani is the flip pose the client plays instead.
        string clipDJump = Env("RC_CLIP_DJUMP", f1 + "f1b02yd\u4E8C\u6BB5\u8DF3a.ani");
        if (clipDJump == "0") clipDJump = "";   // explicit: reuse RC_CLIP_JUMP
        bool djumpLog = Env("RC_DJUMP_LOG", "0") == "1";
        int jumpCount = 0;
        float curJumpGravity = -pGravity;
        Log(string.Format("jump: mode={0} school={1} scale={2:F3} (apex {3:F0}u ~ {3:F0}cm per jump)",
            djumpMode, jumpSchool, jumpScale, 0.5f * (90f * 15f * jumpScale) * (90f * 15f * jumpScale) / (11f * 225f * jumpScale)));
        // Real locomotion speeds from the shipped CommonNumber table
        // (proof/gravity/number.krl.txt): CharacterWalkSpeed=6, CharacterRunSpeed=20
        // in units per 15 Hz logic frame -> 90 / 300 u/s (exact integer per tick;
        // the merge reconciles the old 16 fps 96/320 conversion to the engine's
        // verified 15 Hz tick).
        float pSpeed = 90f, pRun = 300f;
        // Mounted speeds (CommonNumber CharacterRideWalkSpeed=8 /
        // CharacterRideRunSpeed=40 u/logic-frame at the 15 Hz tick -> 120/600 u/s;
        // proof/gravity/number.krl.txt).
        float rideWalk = 120f, rideRun = 600f;
        {
            float rv;
            if (float.TryParse(Env("RC_RIDE_WALK", ""), out rv) && rv > 0f) rideWalk = rv;
            if (float.TryParse(Env("RC_RIDE_RUN", ""), out rv) && rv > 0f) rideRun = rv;
        }
        // Real character size (docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md;
        // 1 unit = 1 cm): the loaded 花萝 actor (f1_1004 head + f1_2227 dress
        // parts) measures 115.58 u = 1.16 m from the extracted bind-pose
        // meshes. Capsule radius scaled from the old adult preset (25 at 170)
        // by the same ratio.
        float playerRadius = 17f, playerHeight = 116f;
        // Body-type capsule (registered proxy): authored heights from
        // Represent/player/player.txt ModelHeight (cm; UNIT_SCALE doc §4)
        // scaled by the host proportion r=0.136*H, h=0.928*H (125 -> 17/116,
        // the M1 花萝 values). RC_BODY=f1|m1|f2|m2; explicit RC_RADIUS /
        // RC_HEIGHT still win.
        {
            string body = Env("RC_BODY", "");
            float bodyH = body == "f1" ? 125f
                        : body == "m1" ? 125f
                        : body == "f2" ? 173f
                        : body == "m2" ? 185f : 0f;
            if (bodyH > 0f)
            {
                playerRadius = (float)Math.Round(bodyH * 0.136f, 1);
                playerHeight = (float)Math.Round(bodyH * 0.928f, 1);
                Log(string.Format("capsule body={0} ModelHeight={1:F0} -> r={2:F1} h={3:F1} (player.txt proxy)",
                    body, bodyH, playerRadius, playerHeight));
            }
        }
        string radEnv = Env("RC_RADIUS", "");
        if (radEnv.Length > 0) float.TryParse(radEnv, out playerRadius);
        string hgtEnv = Env("RC_HEIGHT", "");
        if (hgtEnv.Length > 0) float.TryParse(hgtEnv, out playerHeight);
        // Character step budget (host proxy for the server-authoritative step;
        // the client's own prediction has no capsule-vs-mesh blocking at all,
        // CLIENT_COLLISION_IMPROVEMENT_PLAN 8.3). 64 u = the game-side ground/landing
        // tolerance constant (KCharacter::ProcessVerticalMove 0x14031A25E,
        // 1 尺) and the value covering the 51 u house-floor field case. The
        // PhysX PxControllerDesc ctor default (stepOffset 0.5 m = 50 u; dump
        // proof/collision/disasm/pxcontrollerdesc_ctor.txt) belongs to the
        // PhysX controller layer only - G-1: no proof the player uses a
        // PxController (the gameplay body is the SIMWorld/KCharacter solver).
        // RC_STEP_HEIGHT overrides. Registered deviation 4f (OPEN).
        float stepHeight = 64f;
        float.TryParse(Env("RC_STEP_HEIGHT", "64"), out stepHeight);
        int blockedEvents = 0;
        long colCalls = 0, colBlockedCalls = 0;
        bool colDebug = Env("RC_COL_DEBUG", "0") == "1";
        // host proxy: 小物件 props are solid (RC_PROP_SOLID=0 disables)
        // Default OFF since 2026-10-02: props now collide as their mesh
        // triangles (the engine rule), so the AABB shover is redundant and
        // ejected players from empty AABB corners.
        bool propSolid = Env("RC_PROP_SOLID", "0") == "1";
        int propFixEvents = 0;
        long lastMs = 0, lastLog = 0, lastHud = 0, skillUntil = 0, lastCamMeasure = 0, lastCamLog = 0, lastOrbitMs = 0, lastPostLog = 0, lastMouseDragMs = 0;
        long hitchMaxMs = 0;   // max unclamped frame delta since the last status line
        long hudHitchMs = 0;   // max unclamped frame delta since the last HUD update (D7)
        // camera anchor-Y smooth-follow (B14): the engine smooths the followed
        // character position (JX3RepresentX64 "DynamicFollowSmoothObjectPosition",
        // CharacterCameraSmoothTime=60 ms in Represent/common/number.krl.txt).
        // The host feeds the raw physics py as the anchor, so a discrete step
        // snap (up to the 64 u ground tolerance) teleported the camera. Only
        // discrete snaps are eased; continuous slope/jump motion passes through.
        // 2026-10-03: DEFAULT OFF - the anchor now uses the interpolated render
        // height (rpy, P2-T3 design EXPERIENCES 2026-09-30) so the 15 Hz tick
        // staircase never reaches the camera (the airborne jump was raw: ydbg
        // rawstep 41/35/30... sm=rawstep). RC_CAM_YFOLLOW=1 restores B14 (A/B).
        double camYSmooth = 0.0, camYPrevRaw = 0.0;
        bool camYInit = false, camYEasing = false;
        bool camYFollow = Env("RC_CAM_YFOLLOW", "0") == "1";
        bool camYDbg = Env("RC_CAM_YDBG", "0") == "1";
        double camYAnchorPrev = 0.0;
        bool camYAnchorInit = false;
        double camYMaxRate = 1200.0;
        double.TryParse(Env("RC_CAM_YRATE", "1200"), out camYMaxRate);
        double[] rSm = new double[3];
        bool rSmInit = false;
        bool shakeDbg = Env("RC_CAM_SHAKEDBG", "0") == "1";
        int shakePrevSign = 0;
        long shakeLastLog = 0;
        double shakePrevLen = 0.0;
        double shakePrevSm = 0.0;
        bool shakeHavePrev = false;
        var shakeFlips = new System.Collections.Generic.List<long>();
        bool orbitApplied = false;
        float dbgIntX = 0f, dbgIntY = 0f, dbgIntZ = 0f;
        // penetration recorder (RC_CAM_PENDBG=1, diagnostics only): reverse
        // cast camera -> anchor every frame with all three backends and log an
        // attributed event when drawn geometry sits between them; keep the
        // last ~150 frames of per-probe hits as the event's context.
        bool penDbg = Env("RC_CAM_PENDBG", "0") == "1";
        string penMapName = "";
        try { penMapName = System.IO.Path.GetFileNameWithoutExtension(mapPath); } catch { }
        // terrain-hole A/B dump (RC_HOLE_DUMP=<dir>): writes the packed mask
        // of every hole region the terrain sampler loads, for comparison with
        // the extracted .hlb files (proof/collision/terrain_extra)
        string holeDumpDir = Env("RC_HOLE_DUMP", "");
        int lastHoleIx = int.MinValue, lastHoleIz = int.MinValue;
        int lastBlkInstLogged = -1;
        // collision profile (RC_COL_PROF=1): per-frame collision cost and the
        // deepest contact that last blocked the move
        bool colProf = Env("RC_COL_PROF", "0") == "1";
        var colSw = new System.Diagnostics.Stopwatch();
        double colMsSum = 0, colMsMax = 0;
        long colProfFrames = 0, colCallsPrev = 0;
        var camSw = new System.Diagnostics.Stopwatch();
        double camMsSum = 0, camMsMax = 0;
        long camProfFrames = 0;
        // camera obstruction query cadence: the native ray set costs ~1.3-2.4 ms
        // per query and up to ~10 ms on stall frames next to large buildings
        // (RC_COL_PROF: nat+vert), which halved the frame rate at the 玉门关
        // building (122 vs 240 fps). The game's camera runs at its render
        // cadence; the host caps the managed query set to 20 Hz (env
        // RC_CAM_OBSTHZ, 0 = every frame); placement smoothing/hysteresis still
        // run every frame on the last hit. PROVISIONAL host policy - re-open
        // when the engine's own camera query cadence is recovered.
        double camQueryHz = 20.0;
        {
            double cv;
            if (double.TryParse(Env("RC_CAM_OBSTHZ", "20"), out cv)) camQueryHz = cv;
        }
        double camQueryAcc = 0, lastCamHit = -1.0;
        var bakeSw = new System.Diagnostics.Stopwatch();
        var natSw = new System.Diagnostics.Stopwatch();
        var vertSw = new System.Diagnostics.Stopwatch();
        var sampSw = new System.Diagnostics.Stopwatch();
        double bakeMsSum = 0, natMsSum = 0, vertMsSum = 0, sampMsSum = 0;
        var penRing = new System.Collections.Generic.List<string>();
        var penCur = new System.Text.StringBuilder();
        long penLastLog = 0, penLastSummary = 0;
        int penEvents = 0;
        double penLastDcam = 0; float penLastG1 = -1, penLastG2 = -1, penLastT1 = -1, penLastS1 = -1;
        bool dbgIntSet = false;
        double dbgHit = -1.0, dbgLen = 0.0, dbgEffDist = 0.0;
        string dbgSrc = "";
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
        bool hudLog = Env("RC_HUD_LOG", "0") == "1";   // P3: log the HUD text (test)
        // M0 knob: disable the park-below character hide so the engine's own
        // near-plane clipping can be bracketed with the clearance ladder
        bool hideNear = Env("RC_PLAYER_HIDE", "1") == "1";
        // late object scan (initialized scene view/camera) when RC_CAM_SCAN=1
        bool camScan = Env("RC_CAM_SCAN", "0") == "1", camScanDone = false;
        // Step C capability 2: write the engine camera object directly
        // (absolute Y / look-at); bind once the live camera exists
        bool nativeCam = Env("RC_CAM_NATIVE", "0") == "1", camBound = false;
        // engine-faithful set: position + look-at through the engine camera
        // object (EngineRay.SetCameraEngine) instead of the managed
        // SetCameraPos target-translation; no Y clamp, no orbit events
        bool engineSetCam = Env("RC_CAM_ENGINESET", "1") != "0";   // engine-faithful default; RC_CAM_ENGINESET=0 opts out
        // D6 host pacing: while the scene reports incomplete loading, scale the
        // orbit/view deltas so the engine cannot be asked to show content whose
        // material/shader is still loading (the editor engine AVs there).
        // Registered host pacing (bends feel while loading); RC_CAM_LOADPACE=0
        // disables, RC_CAM_LOADDBG=1 logs the progress signal.
        bool loadPace = Env("RC_CAM_LOADPACE", "1") != "0";
        bool loadDbg = Env("RC_CAM_LOADDBG", "0") == "1";
        float loadProg = 1f;
        long lastLoadLog = 0;
        // D6 pacing experiment, now OPT-IN (RC_CAM_RATECAP=1): cap orbit deltas
        // at 1.5 rad/s. Default off - it bends drag feel (rotation rate), which
        // the user noticed as broken controls, and it did not prevent the D6
        // crash anyway.
        bool rateCap = Env("RC_CAM_RATECAP", "0") == "1";
        uint vtgtSlot = 0;
        {
            string vs = Env("RC_CAM_VTGT_SLOT", "");
            if (vs.Length > 0) uint.TryParse(vs, System.Globalization.NumberStyles.HexNumber, null, out vtgtSlot);
        }
        bool camSetTarget = Env("RC_CAM_SET_TARGET", "0") == "1";
        bool camSnapGuard = Env("RC_CAM_SNAPGUARD", "0") == "1";
        // Workstream B P1: .mani camera-track playback (RC_CAM_ANI=<path>[,loop]).
        // The sampler drives camera + look-at through the normal engine set path;
        // obstruction/shake/snapguard are bypassed for authored tracks.
        CameraTrack camTrack = null;
        bool camTrackActive = false;
        long lastTrackLog = 0;
        {
            string camAniEnv = Env("RC_CAM_ANI", "");
            if (camAniEnv.Length > 0)
            {
                bool trackLoop = false;
                int comma = camAniEnv.LastIndexOf(',');
                if (comma > 1)
                {
                    string tail = camAniEnv.Substring(comma + 1).Trim().ToLowerInvariant();
                    if (tail == "loop") { trackLoop = true; camAniEnv = camAniEnv.Substring(0, comma); }
                }
                try
                {
                    camTrack = CameraTrack.Load(camAniEnv);
                    float fpsOv;
                    if (float.TryParse(Env("RC_CAM_ANI_FPS", ""), out fpsOv) && fpsOv > 0f)
                        camTrack.Fps = (int)fpsOv;
                    camTrack.Play(trackLoop);
                    Log("camera track loaded: " + camTrack.Describe() + " loop=" + trackLoop);
                }
                catch (Exception e)
                {
                    Log("camera track load failed: " + e.Message);
                    camTrack = null;
                }
            }
        }
        // Workstream B P2: skill-move camera FOV (skill_move_camera.txt, GB18030
        // columns decoded 2026-10-06: value <30 = FOV increase in radians,
        // >=30 = fixed FOV in degrees). The FOV ramp is LINEAR (provisional -
        // the client's curve is still open, HOST_DEVIATIONS); post-FX fields
        // are logged only (no post pipeline in the host).
        SkillMoveCamera.LoadEmbedded(Log);
        string smcTable = Env("RC_SKILL_MOVE_TABLE", "");
        if (smcTable.Length > 0) SkillMoveCamera.LoadFile(smcTable, Log);
        SkillMoveCamera.Effect skillMoveFx = null;
        SkillMoveCamera.Row skillMoveRow = null;
        double skillMoveFireMs = -1.0;
        {
            string smc = Env("RC_SKILL_MOVE_CAM", "");
            if (smc.Length > 0)
            {
                string[] parts = smc.Split(',');
                int sid;
                if (int.TryParse(parts[0].Trim(), out sid))
                {
                    skillMoveRow = SkillMoveCamera.Get(sid);
                    if (skillMoveRow == null)
                    {
                        Log("skillmove: no table row for skill " + sid + " (effect skipped)");
                    }
                    else
                    {
                        skillMoveFireMs = 1000.0;
                        if (parts.Length > 1)
                        {
                            double fm;
                            if (double.TryParse(parts[1].Trim(), out fm)) skillMoveFireMs = fm;
                        }
                        skillMoveFx = new SkillMoveCamera.Effect();
                        Log(string.Format("skillmove armed skill={0} fire={1:F0}ms enter={2:F0} exit={3:F0} dur={4:F0} fov={5} fx={6} edge={7} sat={8}",
                            sid, skillMoveFireMs, skillMoveRow.EnterMs, skillMoveRow.ExitMs,
                            skillMoveRow.DurationMs,
                            skillMoveRow.FixedFov
                                ? skillMoveRow.FovValue.ToString("F0") + "deg"
                                : skillMoveRow.FovValue.ToString("F2") + "rad+",
                            skillMoveRow.ScreenFx ? 1 : 0, skillMoveRow.Edge, skillMoveRow.Sat));
                    }
                }
                else Log("skillmove: bad RC_SKILL_MOVE_CAM=" + smc);
            }
        }
        long lastSkillMoveLog = 0;
        int skillMoveStage = -1;   // HUD: current skill-FOV stage (0 in / 1 hold / 2 out)
        bool camPokeOnce = Env("RC_CAM_POKE_ONCE", "0") == "1";
        // legacy look-at approximation: experiment only, default OFF. The
        // engine-faithful path (m_pScene -> cam vt+0x50 pos / vt+0x58 look-at,
        // see EngineRay/CameraShim) now owns the look-at; this orbit-flip was
        // the pre-engine fallback and is kept only for A/B (enable with
        // RC_CAM_LOOKPACK=1). Rate-limited to ~1.5 rad/s and stationary-only.
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
        long[] shots = ParseShots(Env("RC_SHOTS", "3000,8000,15000,30000"));

        // Initialize the model yaw/pitch from the real scene_init_param row (or
        // the user's saved runtime values with RC_CUSTOM_DAT). Maps without
        // their own row keep the spawn view. Model pitch is the offset
        // parameter of the JX3 sphere offset; the engine view pitch follows
        // from it (see geometricAimPitch / docs/camera/FIX_SPEC.md).
        bool applyCamInit = cameraSettings.HasSceneInit || cameraSettings.HasSavedRuntime;
        if (applyCamInit)
        {
            camSys.Yaw = cameraSettings.InitYaw;
            // game pitch -> model pitch (negate; see the convention note at the
            // settings application above)
            camSys.Pitch = -cameraSettings.InitPitch;
            Log(string.Format("camera init applied mapId={0} yaw={1:F3} gamePitch={2:F3} modelPitch={3:F3}",
                cameraSettings.MapId, camSys.Yaw, cameraSettings.InitPitch, camSys.Pitch));
        }
        else
        {
            measureView();
            if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
                camSys.Yaw = Math.Atan2(-viewZ, -viewX);
            curYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));
            moveYaw = camSys.Yaw;
            Log(string.Format("camera init skipped mapId={0} (no scene_init_param row; keeping spawn view)",
                cameraSettings.MapId));
        }

        // One-time engine aim alignment (loop-limited: continuous vertical
        // orbit deltas break the engine screenshot path). RC_PITCH_ALIGN=0 skips.
        if (Env("RC_PITCH_ALIGN", "1") == "1" && Env("RC_CAM_ENGINESET", "1") == "0")
        {
            Log(string.Format("camera aim align: yaw={0:F3} modelPitch={1:F3} aimPitch={2:F3}",
                camSys.Yaw, camSys.Pitch, geometricAimPitch()));
            alignAim();
        }
        // the game keeps the follow distance at the row value; MaxCameraDistance
        // is only the cap the zoom can reach (starting at the cap made
        // the camera pump when walls passed in/out of range)
        camSys.Distance = camSys.ClampDistanceUnits(camSys.Distance);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (loading != null) { loading.Done(); loading = null; }
        while (!form.IsDisposed)
        {
            long now = sw.ElapsedMilliseconds;
            long rawFrameMs = now - lastMs;   // unclamped: hitch evidence
            if (rawFrameMs > hitchMaxMs) hitchMaxMs = rawFrameMs;
            if (rawFrameMs > hudHitchMs) hudHitchMs = rawFrameMs;
            float dt = (now - lastMs) / 1000f;
            lastMs = now;
            if (dt < 0f) dt = 0f;
            if (dt > 0.05f) dt = 0.05f;
            frames++;
            if (now - fpsAt >= 1000) { fps = frames * 1000 / (now - fpsAt); frames = 0; fpsAt = now; }
            if (loadPace || loadDbg)
            {
                try { loadProg = scene.GetLoadingProgress(); } catch { loadProg = 1f; }
                if (loadDbg && now - lastLoadLog >= 1000)
                {
                    lastLoadLog = now;
                    Log("loadprog=" + loadProg.ToString("F3"));
                }
            }
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
                // raw mouse drag? (distinguishes the user's drag from
                // synthesised model deltas - gates the RMB body carry and the
                // mouse-steers-moveYaw rule)
                bool rawMouse = orbitQueue.Count > 0;
                if (rawMouse) lastMouseDragMs = now;
                while (orbitQueue.Count > 0) { int[] d = orbitQueue.Dequeue(); ox += d[0]; oy += d[1]; }
                if (loadPace && loadProg < 0.999f)
                {
                    // D6 host pacing: the engine is still loading content; move
                    // the view at a quarter rate instead of racing the loader
                    ox /= 4; oy /= 4;
                }
                if (rateCap)
                {
                    // D6 view-rate cap (registered host pacing)
                    double maxYawPx2 = 1.5 * dt / 0.0018;
                    if (maxYawPx2 < 1.0) maxYawPx2 = 1.0;
                    if (ox > maxYawPx2) ox = (int)maxYawPx2;
                    else if (ox < -maxYawPx2) ox = (int)-maxYawPx2;
                    double maxPitchPx2 = 1.5 * dt / 0.00121;
                    if (maxPitchPx2 < 1.0) maxPitchPx2 = 1.0;
                    if (oy > maxPitchPx2) oy = (int)maxPitchPx2;
                    else if (oy < -maxPitchPx2) oy = (int)-maxPitchPx2;
                }
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
                // mouse drag = steering: the movement frame follows the camera
                if (rawMouse) moveYaw = yawNew;

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
                // control check: rotate the camera mid-run (like an RMB drag);
                // the camera-relative run must curve after this (turn model)
                if (now >= 6000 && !demoTurned) { demoTurned = true; orbitQueue.Enqueue(new int[] { 500, 0 }); }
                pA = now >= 14000 && now < 18000;
                if (now >= 12500 && !demoJumped) { demoJumped = true; jumpPressed = true; }
                // second press while airborne (ground jump apex ~0.55 s) -> 二段跳
                if (now >= 13000 && demoJumped && !demoJumped2) { demoJumped2 = true; jumpPressed = true; }
                if (now >= 18500 && !demoSkilled) { demoSkilled = true; skillPressed = true; }
            }
            if (demoCollide)
            {
                if (demoTeleport && now >= 2000 && !demoTeleported) { demoTeleported = true; teleportToStructure = true; }
                if (demoCrossBack)
                {
                    // terrain streaming A/B harness: run RC_DEMO_DIR one way
                    // across a region border for 7.5 s, reverse, run 5.5 s back
                    // (RC_SPAWN near the border). Region-load telemetry is
                    // logged per load + summarized at exit.
                    if (now >= 3000 && !demoCrossLogged)
                    {
                        demoCrossLogged = true;
                        Log(string.Format("cross run start dir=({0:F1},{1:F1}) pos=({2:F0},{3:F0})",
                            demoDirX, demoDirZ, px, pz));
                    }
                    if (now >= 10500 && !demoCrossBackDone)
                    {
                        demoCrossBackDone = true;
                        demoDirX = -demoDirX; demoDirZ = -demoDirZ;
                        Log(string.Format("cross run reverse dir=({0:F1},{1:F1}) pos=({2:F0},{3:F0})",
                            demoDirX, demoDirZ, px, pz));
                    }
                    pW = now >= 3000 && now < 16000;
                }
                else pW = now >= 3000 && now < 9000;
            }
            // scripted jump-only probe (RC_DEMO_JUMP=1): one full jump at t=5.5 s
            // (lands on a heartbeat sample mid-air), no walking - vertical
            // penetration test (roofs) at a spawn.
            if (Env("RC_DEMO_JUMP", "0") == "1" && now >= 5500 && !demoJumped)
            {
                demoJumped = true;
                jumpPressed = true;
            }
            if (demoMove)
            {
                // scripted movement-controls run (RC_DEMO_MOVE=1): autorun ->
                // forward jump -> turn key -> classical strafe / back-pedal ->
                // 600 u drop (roll). Run with RC_MODE=classical|joystick to
                // compare the operation-mode routing (yaw/gait fingerprints).
                if (now >= 2500 && !mvAuth) { mvAuth = true; runCommand("TOGGLEAUTORUN", true); runCommand("TOGGLEAUTORUN", false); }
                if (now >= 5000 && !mvJumped) { mvJumped = true; runCommand("JUMP", true); runCommand("JUMP", false); }
                if (now >= 6000 && !mvAuthOff) { mvAuthOff = true; runCommand("TOGGLEAUTORUN", true); runCommand("TOGGLEAUTORUN", false); }
                if (now >= 6800 && !mvStrafe) { mvStrafe = true; strafeYaw0 = curYaw; strafeCam0 = camSys.Yaw; strafeX0 = px; strafeZ0 = pz; runCommand("STRAFELEFT", true); if (demoRmbStrafe >= 1) rmbDown = true; }
                if (now >= 7800 && !mvStrafeDone) { mvStrafeDone = true; rmbDown = false; runCommand("STRAFELEFT", false); Log(string.Format("movetest strafe mode={0} rmb={1} yaw0={2:F2} yaw1={3:F2} d={4:F2} cam0={5:F2} cam1={6:F2} dpos=({7:F0},{8:F0}) dist={9:F0}", CameraOperationMode.Name(cameraSettings.OperationMode), demoRmbStrafe, strafeYaw0, curYaw, curYaw - strafeYaw0, strafeCam0, camSys.Yaw, px - strafeX0, pz - strafeZ0, (float)Math.Sqrt((px - strafeX0) * (px - strafeX0) + (pz - strafeZ0) * (pz - strafeZ0)))); }
                if (now >= 8100 && !mvBack) { mvBack = true; backYaw0 = curYaw; backCam0 = camSys.Yaw; backX0 = px; backZ0 = pz; runCommand("MOVEBACKWARD", true); }
                if (now >= 9100 && !mvBackDone) { mvBackDone = true; runCommand("MOVEBACKWARD", false); Log(string.Format("movetest back mode={0} yaw0={1:F2} yaw1={2:F2} d={3:F2} cam0={4:F2} cam1={5:F2} dpos=({6:F0},{7:F0}) dist={8:F0}", CameraOperationMode.Name(cameraSettings.OperationMode), backYaw0, curYaw, curYaw - backYaw0, backCam0, camSys.Yaw, px - backX0, pz - backZ0, (float)Math.Sqrt((px - backX0) * (px - backX0) + (pz - backZ0) * (pz - backZ0)))); }
                if (now >= 9500 && !mvTurn) { mvTurn = true; turnYaw0 = curYaw; turnCam0 = camSys.Yaw; runCommand("TURNRIGHT", true); }
                if (now >= 10100 && !mvTurnDone) { mvTurnDone = true; runCommand("TURNRIGHT", false); Log(string.Format("movetest turn yaw0={0:F2} yaw1={1:F2} d={2:F2} cam0={3:F2} cam1={4:F2} camd={5:F2}", turnYaw0, curYaw, WrapAngle(curYaw - turnYaw0), turnCam0, camSys.Yaw, WrapAngle(camSys.Yaw - turnCam0))); }
                if (now >= 10800 && !mvDrop) { mvDrop = true; py += 600f; Log(string.Format("movetest drop600 y={0:F0} (fall > FallDownHeightFloor)", py)); }
                if (now >= 12600 && !mvSit) { mvSit = true; runCommand("TOGGLESITDOWN", true); runCommand("TOGGLESITDOWN", false); }
                if (now >= 13300 && !mvSitDone) { mvSitDone = true; runCommand("TOGGLESITDOWN", true); runCommand("TOGGLESITDOWN", false); Log("movetest sit done"); }
                if (now >= 13600 && !mvSheath) { mvSheath = true; runCommand("TOGGLESHEATH", true); runCommand("TOGGLESHEATH", false); }
                if (now >= 14600 && !mvSheathDone) { mvSheathDone = true; runCommand("TOGGLESHEATH", true); runCommand("TOGGLESHEATH", false); Log("movetest sheath done"); }
                // W+A / W+D free-view windows: A/D turn while W runs -> curve
                if (now >= 15100 && !mvWA) { mvWA = true; waX0 = px; waZ0 = pz; waYaw0 = curYaw; waCam0 = camSys.Yaw; runCommand("MOVEFORWARD", true); runCommand("STRAFELEFT", true); if (demoRmbWa >= 1) rmbDown = true; }
                if (mvWA && !mvWADone && demoRmbWa >= 2) orbitQueue.Enqueue(new int[] { 2, 0 });   // simulated RMB drag
                if (now >= 16300 && !mvWADone) { mvWADone = true; rmbDown = false; runCommand("STRAFELEFT", false); runCommand("MOVEFORWARD", false); Log(string.Format("movetest WA mode={0} rmb={1} dpos=({2:F0},{3:F0}) dist={4:F0} dyaw={5:F2} dcam={6:F2}", CameraOperationMode.Name(cameraSettings.OperationMode), demoRmbWa, px - waX0, pz - waZ0, (float)Math.Sqrt((px - waX0) * (px - waX0) + (pz - waZ0) * (pz - waZ0)), WrapAngle(curYaw - waYaw0), WrapAngle(camSys.Yaw - waCam0))); }
                if (now >= 16600 && !mvWD) { mvWD = true; wdX0 = px; wdZ0 = pz; wdYaw0 = curYaw; wdCam0 = camSys.Yaw; runCommand("MOVEFORWARD", true); runCommand("STRAFERIGHT", true); }
                if (now >= 17800 && !mvWDDone) { mvWDDone = true; runCommand("STRAFERIGHT", false); runCommand("MOVEFORWARD", false); Log(string.Format("movetest WD mode={0} dpos=({1:F0},{2:F0}) dist={3:F0} dyaw={4:F2} dcam={5:F2}", CameraOperationMode.Name(cameraSettings.OperationMode), px - wdX0, pz - wdZ0, (float)Math.Sqrt((px - wdX0) * (px - wdX0) + (pz - wdZ0) * (pz - wdZ0)), WrapAngle(curYaw - wdYaw0), WrapAngle(camSys.Yaw - wdCam0))); }
                if (now >= 19000 && !mvDone) { mvDone = true; Log(string.Format("movetest summary yaw={0:F2} pos=({1:F0},{2:F0},{3:F0}) autorun={4} mode={5}", curYaw, px, py, pz, autorunOn ? 1 : 0, CameraOperationMode.Name(cameraSettings.OperationMode))); }
            }
            if (sprintTest)
            {
                // scripted double-tap sprint input test (same keyCommand path
                // as real key events): 12000 down, 12100 up, 12150 down
                // (150 ms gap < 250 ms window), 12650 up.
                if (now >= 12000 && !sprintT1) { sprintT1 = true; keyCommand("MOVEFORWARD", true); }
                if (now >= 12100 && !sprintT2) { sprintT2 = true; keyCommand("MOVEFORWARD", false); }
                if (now >= 12150 && !sprintT3) { sprintT3 = true; keyCommand("MOVEFORWARD", true); }
                if (now >= 12650 && !sprintT4) { sprintT4 = true; keyCommand("MOVEFORWARD", false); }
            }
            if (mountTest)
            {
                // scripted mount run (W4 proof): mount -> mounted run -> horse
                // jump -> midair second press (dismount + normal jump) -> stop ->
                // remount -> standing dismount. Every step logs its state.
                if (now >= 2500 && !mtMounted) { mtMounted = true; runCommand("RIDEHORSE", true); runCommand("RIDEHORSE", false); Log("mounttest t=2.5 mount press mounted=" + (mount.Mounted ? 1 : 0)); }
                if (now >= 3200 && !mtFwd) { mtFwd = true; pW = true; Log("mounttest t=3.2 forward (mounted run)"); }
                if (now >= 4500 && !mtJump1) { mtJump1 = true; jumpPressed = true; Log("mounttest t=4.5 horse jump"); }
                if (now >= 5600 && !mtJump2) { mtJump2 = true; jumpPressed = true; Log("mounttest t=5.6 second press midair"); }
                if (now >= 7500 && !mtStop) { mtStop = true; pW = false; Log(string.Format("mounttest t=7.5 stop mounted={0} grounded={1} pos=({2:F0},{3:F0},{4:F0})", mount.Mounted ? 1 : 0, grounded ? 1 : 0, px, py, pz)); }
                if (now >= 8500 && !mtMount2) { mtMount2 = true; runCommand("RIDEHORSE", true); runCommand("RIDEHORSE", false); Log("mounttest t=8.5 remount mounted=" + (mount.Mounted ? 1 : 0)); }
                if (now >= 9800 && !mtDown) { mtDown = true; runCommand("RIDEHORSE", true); runCommand("RIDEHORSE", false); Log("mounttest t=9.8 dismount press mounted=" + (mount.Mounted ? 1 : 0)); }
                if (now >= 10600 && !mtDone) { mtDone = true; Log(string.Format("mounttest summary mounted={0} clip={1}", mount.Mounted ? 1 : 0, curClip == null ? "-" : Path.GetFileName(curClip))); }
            }
            if (probeControl && now >= nextProbeMs)
            {
                nextProbeMs = now + 2000;
                ProbeControl();
            }
            if (modeSwitchAt > 0 && !modeSwitched && now >= modeSwitchAt)
            {
                // scripted operation-mode switch (test harness; same path as F7)
                modeSwitched = true;
                cameraSettings.OperationMode =
                    cameraSettings.OperationMode == CameraOperationMode.Joystick
                        ? CameraOperationMode.Classical : CameraOperationMode.Joystick;
                cameraSettings.ApplyOperationMode();
                Log("opmode switch (test @" + modeSwitchAt + "ms): " + cameraSettings.DescribeApplied());
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

            if (camZoomSeq)
            {
                // test input only (no camera behavior): zoom steps out x3 then
                // in x3 at 1 s intervals after the rotation/pitch demo
                if (now >= 13000 && now < 19000)
                {
                    int step = (int)((now - 13000) / 1000);
                    if (step != zoomSeqStep)
                    {
                        zoomSeqStep = step;
                        camSys.ZoomBy(step < 3 ? 1.0 : -1.0);
                    }
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
                Log(string.Format("camdbg mode={0} yaw={1:F3} pitch={2:F3} vyaw={3:F3} vpitch={4:F3} dist={5:F0} r={6:F1} cam=({7:F0},{8:F0},{9:F0}) obst={10} hit={11:F0} len={12:F0} eff={13:F0} clamp={14} op={15}",
                    camSys.Mode, camSys.Yaw, camSys.Pitch, vyaw, vpitch, camSys.Distance, rgeo, dbgx, dbgy, dbgz,
                    dbgObst ? 1 : 0, dbgHit, dbgLen, dbgEffDist, double.IsNaN(aimPitchOverride) ? 0 : 1,
                    CameraOperationMode.Name(cameraSettings.OperationMode)));
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

            // Movement frame (decoded): the engine controls are CAMERA controls
            // (FORWARD/BACKWARD/STRAFE/TURN relative to the camera), so the input
            // frame is the camera in both modes; the BODY faces the travel
            // direction (RunTo, KRLLocalCharacter face yaw +0x30). That is why
            // W+D runs the forward clip while the body faces the diagonal, and
            // pure D (no W) is the 挪步 side-step.
            bool followsHeading = CameraOperationMode.BodyFollowsHeading(cameraSettings.OperationMode);
            bool classicalMode = !followsHeading;
            // Movement frame: the control frame moveYaw (camera yaw + the turn
            // keys' rotation). Mouse drags set moveYaw to the camera so mouse
            // steering keeps working; the camera follows moveYaw via the row.
            double cfx = -Math.Cos(moveYaw), cfz = -Math.Sin(moveYaw);
            float hx = (float)cfx;
            float hz = (float)cfz;

            // skill
            if (skillPressed)
            {
                skillPressed = false;
                skillUntil = now + skillMs;
                curClip = null;
                setClip(clipSkill);
                // camera shake on the cast (host default; per-skill shake rows
                // are data-gated)
                camShake.Start(2.0, 0.5, 0.8, 3);
                bool playedNative = false;
                if (soundNative)
                {
                    // ensure the media cwd + Wwise language at cast time too
                    // (the engine resets the process cwd during map load)
                    string media = Env("RC_SOUND_MEDIA", "");
                    if (media.Length > 0) SoundProbe.SetMediaDir(media);
                    uint pid = SoundProbe.PostEvent(nativeEvent, 1);
                    Log("sound: native post id=" + nativeEvent + " playing=" + pid);
                    if (pid != 0 && SoundProbe.Diag(pid) == 1) playedNative = true;
                    else
                    {
                        soundNative = false;
                        Log("sound-native: no rendering (streamed media unresolved) - WAV fallback");
                    }
                }
                if (!playedNative && skillWav != null)
                {
                    bool played = PlaySound(skillWav, IntPtr.Zero,
                        SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
                    Log("sound: skill wav play rc=" + played);
                }
                Log("skill cast");
            }

            // input -> direction (camera controls in both modes; the body faces
            // the travel). A/D follow the player's A/D HABIT (official: the
            // tutorial teaches the turn vs strafe habits; shipped default.txt
            // binds A/D to STRAFE*). The keyboard never writes the camera; a
            // moving character is followed through the cached
            // CameraAdjustYawWhenMoveTurn row (see the follow call below).
            float inX = 0f, inZ = 0f;
            float rX = hz, rZ = -hx;
            // Free view: mainscene.lua's CameraStatus_Set calls
            // CameraStatus_Animation(mode ~= 'god camera'); free view is ON in
            // normal play. RC_FREEVIEW=0 keeps the god-camera branch (the
            // Turn<->Strafe handler swap) reachable for tests.
            bool freeView = Env("RC_FREEVIEW", "1") != "0";
            // Control table (decoded): ids 0..13, built from keys/mouse exactly
            // like Camera_EnableControl fills the client's control store.
            // Decoded: CLASSICAL A/D are STRAFE-bound -> strafe (default.txt);
            // the "turn" habit is a CLASSICAL option (RC_ADHABIT=turn). In
            // JOYSTICK the free-move handler (OperationModeBase 0/16
            // FreeMoveControl) maps the TURN controls to the LATERAL vector
            // axis - A/D (strafe handler -> TurnLeftStart) and the arrows both
            // feed strafe; there is no in-place keyboard rotation (auto-face
            // turns the body to the travel), and the mouse owns the camera.
            bool adStrafe = classicalMode && (adHabit != "turn" || rmbDown || !freeView);
            bool turnL, turnR, strafeL, strafeR;
            if (classicalMode)
            {
                turnL = pTurnL || (pA && !adStrafe);
                turnR = pTurnR || (pD && !adStrafe);
                strafeL = pA && adStrafe;
                strafeR = pD && adStrafe;
            }
            else
            {
                turnL = false;
                turnR = false;
                strafeL = pA || pTurnL;
                strafeR = pD || pTurnR;
            }
            int ctrl = 0;
            // both mouse buttons held = auto-forward (official classic scheme)
            Ctrl.Set(ref ctrl, ControlId.Forward, pW || autorunOn || (lmbDown && rmbDown));
            Ctrl.Set(ref ctrl, ControlId.Backward, pS);
            Ctrl.Set(ref ctrl, ControlId.TurnLeft, turnL);
            Ctrl.Set(ref ctrl, ControlId.TurnRight, turnR);
            Ctrl.Set(ref ctrl, ControlId.StrafeLeft, strafeL);
            Ctrl.Set(ref ctrl, ControlId.StrafeRight, strafeR);
            Ctrl.Set(ref ctrl, ControlId.Camera, lmbDown);
            Ctrl.Set(ref ctrl, ControlId.StickCamera, rmbDown);
            Ctrl.Set(ref ctrl, ControlId.AutoRun, autorunOn);
            Ctrl.Set(ref ctrl, ControlId.Walk, walkMode);
            // Intents (GetMoveInfo analog): forward, strafeRight, rotationRight.
            float fwdAxis = (Ctrl.Get(ctrl, ControlId.Forward) ? 1f : 0f)
                          - (Ctrl.Get(ctrl, ControlId.Backward) ? 1f : 0f);
            float latAxis = (Ctrl.Get(ctrl, ControlId.StrafeRight) ? 1f : 0f)
                          - (Ctrl.Get(ctrl, ControlId.StrafeLeft) ? 1f : 0f);
            float rotAxis = (Ctrl.Get(ctrl, ControlId.TurnRight) ? 1f : 0f)
                          - (Ctrl.Get(ctrl, ControlId.TurnLeft) ? 1f : 0f);
            // sitting stands up on any movement intent (move / turn / jump)
            if (sitting && (pW || pS || pA || pD || pTurnL || pTurnR || autorunOn))
            {
                sitting = false;
                Log("sit: stand (movement)");
            }
            if (Ctrl.Get(ctrl, ControlId.Forward)) { inX += hx; inZ += hz; }
            if (Ctrl.Get(ctrl, ControlId.Backward)) { inX -= hx; inZ -= hz; }
            if (Ctrl.Get(ctrl, ControlId.StrafeLeft)) { inX -= rX; inZ -= rZ; }
            if (Ctrl.Get(ctrl, ControlId.StrafeRight)) { inX += rX; inZ += rZ; }
            float inLen = (float)Math.Sqrt(inX * inX + inZ * inZ);
            if (inLen > 1e-4f) { inX /= inLen; inZ /= inLen; }
            float dirX = inX, dirZ = inZ;
            if (demoCollide) { dirX = demoDirX; dirZ = demoDirZ; }
            float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            bool moving = len > 0.01f && skillUntil <= now;
            // Locomotion clip by INPUT OCTANT (branch semantics; per render
            // frame - must NOT live inside the tick loop, where non-tick
            // frames would reset it to 0 and flicker run<->strafe at 15 Hz):
            // any forward intent = forward run/walk, backward = back-pedal,
            // pure lateral = step.
            int gait = 0;
            if (moving)
            {
                if (followsHeading) gait = 0;
                else if (fwdAxis > 0f) gait = 0;
                else if (fwdAxis < 0f) gait = 3;
                else if (latAxis > 0.01f) gait = 2;
                else if (latAxis < -0.01f) gait = 1;
            }
            // keyboard turn rate: the LOCAL camera-controller rotation speed
            // (row RotationSpeed; loader default 0.00314 rad/ms = pi rad/s, both
            // values are local data - no server involvement). pi stays only as
            // the missing-row fallback.
            float charTurnRate = (float)(camSys.Row.F("RotationSpeed", 0.0) * 1000.0);
            if (charTurnRate < 0.1f) charTurnRate = (float)Math.PI;

            // P2-T1: whole logic ticks only (66.7 ms); remaining time is the
            // render interpolation fraction (P2-T3).
            const float MOVE_TICK = 1f / 15f;
            moveAcc += dt;
            int moveTicks = (int)(moveAcc / MOVE_TICK);
            if (moveTicks > 4) moveTicks = 4;          // hitch guard
            moveAcc -= moveTicks * MOVE_TICK;
            if (moveTicks > 0)
            {
                lastTickX = px; lastTickY = py; lastTickZ = pz;
                lastTickInit = true;
            }
            bool blocked = false;
            for (int mti = 0; mti < moveTicks; mti++)
            {
            float pdt = MOVE_TICK;
            // horizontal move + slope blocking (map-host rules); terrain holes
            // (real LoadHoleRegion data) carry no ground at all. Long moves are
            // split into substeps so a step cannot tunnel a thin collider, and
            // the climb check uses a fixed 40 u look-ahead so the slope limit
            // does not depend on speed/framerate (map-host rule, C-3/C-4).
            float ground = py;
            bool groundOk = true;
            if (sampler != null) groundOk = sampler.SampleGround(px, pz, out ground);
            float mvx = 0f, mvz = 0f;
            float subStep = 0f;
            int subCount = 1;
            if (moving)
            {
                float baseSp = shiftDown ? pRun * 10f
                            : mount.Mounted ? (walkMode ? rideWalk : rideRun)
                            : walkMode ? pSpeed
                            : pRun;
                // classical S / S+A / S+D: back-pedal at walk pace (user-
                // observed; number.krl ships no back speed - walk 6 u/f is the
                // authored slow pace). Pure lateral (no forward/back) is the
                // walk-tier side-step (挪步 clip cadence); a forward component
                // runs. Joystick always faces the travel -> run tier.
                bool backPedal = classicalMode && fwdAxis < 0f;
                bool sideOnly = classicalMode && fwdAxis == 0f && Math.Abs(latAxis) > 0.01f;
                float sp = (backPedal || sideOnly ? (shiftDown ? pSpeed * 10f : mount.Mounted ? rideWalk : pSpeed) : baseSp) / len;
                float ux = dirX / len, uz = dirZ / len;
                float heading = (float)Math.Atan2(ux, uz);
                // turn model (KCharacter::RunTo 0x14031B780; docs/movement/
                // JX3_CHARACTER_MOVEMENT_RESEARCH.md §3.5): heading = travel
                // direction; facing turns toward it at the turn rate; a turn
                // > 112.5 deg (0x50/0x100 of the circle) halves movement speed
                // and the turn step that tick.
                // Branch control semantics: JOYSTICK faces the travel instantly
                // (decoded KCharacter::TurnTo writes the target heading
                // [char+0x44] directly); CLASSICAL keeps the RunTo turn model
                // (turn step per tick, >112.5 deg halves speed and turn step).
                bool forwardish = fwdAxis > 0f || demoCollide;
                if (followsHeading)
                {
                    curYaw = heading;
                }
                else if (forwardish)
                {
                    float dYaw = heading - curYaw;
                    while (dYaw > Math.PI) dYaw -= 2f * (float)Math.PI;
                    while (dYaw < -Math.PI) dYaw += 2f * (float)Math.PI;
                    bool hardTurn = Math.Abs(dYaw) > 2.0071f;
                    if (hardTurn) sp *= 0.5f;
                    float turnStep = charTurnRate * pdt * (hardTurn ? 0.5f : 1f);
                    if (Math.Abs(dYaw) <= turnStep) curYaw = heading;
                    else curYaw += Math.Sign(dYaw) * turnStep;
                }
                // the engine moves integer units per logic frame (u/f); make
                // the per-tick displacement integral too
                float step = (float)Math.Round(sp * pdt);
                if (step < 1f && sp > 0f) step = 1f;
                mvx = ux; mvz = uz;
                // substep cap stays BELOW the capsule radius: at >= radius a
                // thin small face (foliage leaf/branch) lets the centre pass
                // the sheet inside one substep and the contact degenerates to
                // an edge push - the capsule creeps through (audit class
                // 2026-09-30, full-map sweep).
                float subCap = Math.Min(20f, playerRadius * 0.9f);
                if (subCap < 1f) subCap = 1f;
                if (step > subCap) subCount = (int)Math.Ceiling(step / subCap);
                if (subCount > 64) subCount = 64;
                subStep = step / subCount;
            }
            if (colProf) colSw.Restart();
            for (int si = 0; si < subCount; si++)
            {
                float sdx = 0f, sdz = 0f;
                if (moving)
                {
                    sdx = mvx * subStep;
                    sdz = mvz * subStep;
                    // Engine ground rule (KCharacter::ProcessVerticalMove,
                    // 0x140318E73: y = min(y, ground); ground = terrain cell
                    // height): terrain rises never block horizontally, they
                    // raise the character. No rise-budget check here.
                }
                px += sdx;
                pz += sdz;
                if (sampler != null) groundOk = sampler.SampleGround(px, pz, out ground);

                // object/foliage collision (walls, buildings, rocks, trees)
                if (col != null)
                {
                    // CCT semantics only: movement climbs via faces the
                    // capsule actually contacts (Resolve's step branch).
                    // (The caller-side SupportHeight raise was a host shortcut:
                    // any surface within +64 u under the capsule centre lifted
                    // the player every tick, which chained up stepped props
                    // like the 270 u wood pile.)
                    colCalls++;
                    float gBefore = ground;
                    bool sBlocked = col.Resolve(ref px, ref py, ref pz,
                        playerRadius, playerHeight, ref ground, ref grounded, stepHeight,
                        0f, mvx, mvz);
                    if (sBlocked) { blocked = true; blockedEvents++; colBlockedCalls++; }
                    if (ground > gBefore + 0.01f) groundOk = true;   // structure support
                    if (grounded)
                    {
                        // Support raise: only to a surface the capsule can
                        // actually stand on. Without the fit test an overhang
                        // underside (a stepped wall's molding) under the
                        // capsule centre lifted the player every tick - field
                        // case: the Yumen building back wall (2026-09-30).
                        float sh = col.SupportHeight(px, pz, py - 150f, py + 60f);
                        if (sh > ground && !col.CapsuleBlockedDown(px, sh, pz, playerRadius, playerHeight))
                        { ground = sh; groundOk = true; }
                        else if (sh > ground && supDbg && supDbgN < 40)
                        {
                            supDbgN++;
                            Log(string.Format(
                                "suprej sh={0:F1} py={1:F1} pos=({2:F0},{3:F0}) depth={4:F2} cpy={5:F1} triTop={6:F1} inst={7} stepRej={8} stepTop={9:F1}",
                                sh, py, px, pz, col.LastProbeDepth, col.LastProbePy,
                                col.LastProbeTriTop, col.LastProbeInst, col.StepRejectCount, col.LastStepRejectTop));
                        }
                    }
                }
            }
            if (col != null && propSolid)
            {
                int pfInst;
                if (col.SolidPropPush(ref px, ref py, ref pz, playerRadius, playerHeight, ground, grounded, 70f, out pfInst))
                {
                    propFixEvents++;
                    if (propFixEvents <= 20)
                        Log(string.Format("propfix inst={0} pos=({1:F0},{2:F0},{3:F0}) dbg={4}",
                            pfInst, px, py, pz, col.LastEjectDbg));
                }
            }
            if (colProf)
            {
                colSw.Stop();
                double msProf = colSw.Elapsed.TotalMilliseconds;
                colMsSum += msProf;
                colProfFrames++;
                if (msProf > colMsMax) colMsMax = msProf;
            }
            // facing: joystick instant / classical RunTo above (per tick)

            // TURNLEFT/TURNRIGHT (arrows) plus classical free-view A/D.
            // DECODED (hotkeys.lua): turn keys are a CHARACTER control -
            // TurnLeftStart -> SetControl(CONTROL_TURN_LEFT) via the mode
            // wrapper. In CLASSICAL the host turns the view too (the
            // user-requested turn-habit/arrow behavior); in JOYSTICK the input
            // layer above feeds the turn controls into the lateral vector
            // (OperationModeBase 0/16), so rotAxis stays 0 and the mouse owns
            // the camera (the decoded keyboard path never writes it).
            if (grounded && rotAxis != 0f)
            {
                float tstep = charTurnRate * (float)pdt;
                double dyawKey = -rotAxis * tstep;
                if (classicalMode)
                {
                    // classical: turn keys rotate the VIEW (mouse sign); the
                    // engine camera is fed through the orbit path and the
                    // character turns to the camera direction (RMB-carry
                    // relation).
                    camSys.Yaw += dyawKey;
                    adjYawPx += (int)Math.Round(-dyawKey / 0.0018);
                    moveYaw = camSys.Yaw;
                    float targetYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));
                    float d = targetYaw - curYaw;
                    while (d > Math.PI) d -= 2f * (float)Math.PI;
                    while (d < -Math.PI) d += 2f * (float)Math.PI;
                    float cstep = charTurnRate * (float)pdt;
                    if (Math.Abs(d) <= cstep) curYaw = targetYaw;
                    else curYaw += Math.Sign(d) * cstep;
                }
                else
                {
                    // joystick: turn the CHARACTER in place; the mouse owns
                    // the view (the decoded keyboard path never writes the
                    // camera). Facing delta = -camera delta = dyawKey.
                    curYaw += (float)dyawKey;
                }
            }
            // keep the facing and camera yaw wrapped: the movement turn model
            // compares against wrapped headings, and an unwrapped facing makes
            // dYaw alias across +/-pi (turn flips to the long way around).
            while (curYaw > (float)Math.PI) curYaw -= 2f * (float)Math.PI;
            while (curYaw < -(float)Math.PI) curYaw += 2f * (float)Math.PI;
            while (camSys.Yaw > Math.PI) camSys.Yaw -= 2.0 * Math.PI;
            while (camSys.Yaw < -Math.PI) camSys.Yaw += 2.0 * Math.PI;

            // RMB (CAMERAORSELECTORMOVESTICKY) also turns the character to the
            // camera direction while the user is actually dragging (the decoded
            // camera->face write fires on mouse deltas, not on a held button);
            // LMB drag rotates the camera only. Rate-limited (S6), no snap.
            if (rmbDown && (now - lastMouseDragMs < 150) &&
                CameraOperationMode.RmbTurnsBody(cameraSettings.OperationMode))
            {
                float targetYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));
                float d = targetYaw - curYaw;
                while (d > Math.PI) d -= 2f * (float)Math.PI;
                while (d < -Math.PI) d += 2f * (float)Math.PI;
                float step = charTurnRate * (float)pdt;
                if (Math.Abs(d) <= step) curYaw = targetYaw;
                else curYaw += Math.Sign(d) * step;
            }

            // grounded / step / drop - engine rules (KCharacter::ProcessVerticalMove
            // 0x140318E73 clamps y = min(y, ground); the 64 u = 1 尺 landing
            // tolerance at 0x14031A25E):
            //  - higher ground raises the character (no rise-budget on terrain
            //    or steppable structures);
            //  - drops up to 64 u stay snapped (walking down slopes);
            //  - larger drops make the character airborne.
            if (grounded)
            {
                if (!groundOk) { grounded = false; vy = 0f; }
                else if (py < ground) py = ground;
                else if (vy > 0f) grounded = false;          // ascending: a jump is never re-grounded
                else if (py - ground <= 64f) py = ground;
                else { grounded = false; vy = 0f; }
            }

            // jump + 二段跳: press 1 = J0; in the air press 2 = flip mode (one
            // extra normal-strength jump) or chain mode (raw J1.. table rows)
            if (jumpPressed)
            {
                jumpPressed = false;
                if (sitting) { sitting = false; Log("sit: stand (jump)"); }
                bool mountHandled = false;
                if (mount.Mounted)
                {
                    // KCharacter::Jump mounted branches (0x140313A30..A88): the
                    // horse triple 60/180/11 when jumpCount==0; jumpCount>=1 while
                    // mounted rejects; at jumpCount==1 the press dismounts first
                    // (DownHorse) and the normal jump rules then apply.
                    if (!grounded && jumpCount == 1)
                    {
                        mount.Dismount(scene, Log);
                    }
                    else if (!grounded)
                    {
                        Log("mount jump reject n=" + jumpCount + " (airborne)");
                        mountHandled = true;
                    }
                    else
                    {
                        jumpCount = 1;
                        vy = 180f * 15f * jumpScale;
                        curJumpGravity = 11f * 225f * jumpScale;
                        grounded = false;
                        airStartY = py;
                        float xySpd = 60f * 15f * jumpScale;
                        if (len > 0.01f) { vjx = dirX / len * xySpd; vjz = dirZ / len * xySpd; }
                        else { vjx = 0f; vjz = 0f; }
                        Log(string.Format(
                            "mount jump triple=(60,180,11) vy={0:F0} g={1:F0} bonus=0 ([+0x34C] script) pos=({2:F0},{3:F0},{4:F0})",
                            vy, curJumpGravity, px, py, pz));
                        mountHandled = true;
                    }
                }
                if (!mountHandled)
                {
                if (grounded) jumpCount = 0;
                int nextJump = jumpCount + 1;
                bool chainMode = djumpMode == "chain";
                bool djumpOn = djumpMode != "0";
                int maxJump = chainMode ? JumpTable.MaxJumpCount[jumpSchool] : 2;
                int[] trip = null;
                if (nextJump <= maxJump && nextJump <= JumpTable.Triples[jumpSchool].Length &&
                    (nextJump == 1 || djumpOn))
                {
                    // chain mode reads the pressed row; flip mode reuses J0
                    trip = JumpTable.Triples[jumpSchool][chainMode ? nextJump - 1 : 0];
                }
                if (trip != null)
                {
                    jumpCount = nextJump;
                    vy = trip[1] * 15f * jumpScale;
                    int gc = trip[2]; if (gc < 0) gc = 0; else if (gc > 31) gc = 31;
                    curJumpGravity = gc * 225f * jumpScale;
                    grounded = false;
                    airStartY = py;
                    // takeoff horizontal velocity: JumpSpeedXY of the row
                    // (clamp [0,127] per JumpTo/KJump), converted at the 15 Hz
                    // logic tick, along the input direction. A standing jump
                    // stays ballistic-vertical: with no move intent the client
                    // commit path carries no horizontal velocity (the air
                    // commit path is an open item, not invented here).
                    int xyc = trip[0]; if (xyc < 0) xyc = 0; else if (xyc > 127) xyc = 127;
                    float xySpd = xyc * 15f * jumpScale;
                    if (len > 0.01f)
                    {
                        vjx = dirX / len * xySpd;
                        vjz = dirZ / len * xySpd;
                    }
                    else
                    {
                        vjx = 0f; vjz = 0f;
                    }
                    if (djumpLog || demoMove) Log(string.Format(
                        "jump xy takeoff vj=({0:F0},{1:F0}) u/s dir=({2:F2},{3:F2})",
                        vjx, vjz, dirX / (len > 0.01f ? len : 1f), dirZ / (len > 0.01f ? len : 1f)));
                    if (djumpLog) Log(string.Format(
                        "djb press n={0} mode={1} triple={2},{3},{4} vy={5:F0} g={6:F0} pos={7:F0},{8:F0},{9:F0}",
                        jumpCount, djumpMode, trip[0], trip[1], trip[2], vy, curJumpGravity, px, py, pz));
                }
                else if (djumpLog) Log(string.Format(
                    "djb reject n={0} max={1} grounded={2} mode={3}",
                    nextJump, maxJump, grounded ? 1 : 0, djumpMode));
                }
            }

            // gravity (per-jump magnitude; J0 11 u/f2 -> 2475 u/s2 = the old constant)
            if (!grounded)
            {
                float vyBefore = vy;
                vy -= curJumpGravity * pdt;
                py += vy * pdt;
                // apex sample: the model transform must have followed the physics
                // height (modelY ~ py); a stale modelY is the standing-jump stutter
                if (djumpLog && vyBefore > 0f && vy <= 0f) Log(string.Format(
                    "djb apex n={0} py={1:F0} modelY={2:F0}", jumpCount, py, lastModelY));
                // rising motion is resolved too (field case: a jump must not
                // pass up through a thin roof slab; horizontal faces oppose the
                // rise). Falling keeps the existing ground snap - resolving it
                // here created a fall->push-up ratchet at overlapping ledges.
                if (col != null && vy > 0f)
                    col.Resolve(ref px, ref py, ref pz,
                        playerRadius, playerHeight, ref ground, ref grounded, stepHeight, vy, mvx, mvz);
                if (py <= ground && groundOk)
                {
                    py = ground;
                    float impact = vy;
                    if (vy < 0f) vy = 0f;
                    grounded = true;
                    // landing branch: height difference vs FallDownHeightFloor
                    // (player_suspend.krl.txt F1: 500 u) -> the authored landing
                    // animation; otherwise the normal resume.
                    float drop = airStartY - py;
                    vjx = 0f; vjz = 0f;
                    if (drop > fallDownHeightFloor)
                    {
                        landClipUntil = now + landClipMs;
                        int lrc = setClip(clipLand);
                        Log(string.Format("land drop={0:F0}u roll=1 clip={1} rc={2}",
                            drop, Path.GetFileName(clipLand), lrc));
                        if (lrc != 0)
                        {
                            // clip not playable in this build: keep the branch
                            // timing, fall back to the known-good fall clip
                            clipLand = clipFall;
                            landClipUntil = now + 400;
                            setClip(clipLand);
                            Log("land roll clip fallback -> " + Path.GetFileName(clipLand));
                        }
                    }
                    if (djumpLog && jumpCount > 0) Log(string.Format(
                        "djb land n={0} pos={1:F0},{2:F0},{3:F0} vy={4:F0} drop={5:F0}",
                        jumpCount, px, py, pz, impact, drop));
                    jumpCount = 0;
                }
            }
            else jumpCount = 0;
            // engine integer positions (u = cm)
            px = (float)Math.Round(px);
            py = (float)Math.Round(py);
            pz = (float)Math.Round(pz);
            }
            // P2-T3 render position: interpolate between the pre-tick state and
            // the current tick state by the remaining tick fraction.
            float rAlpha = lastTickInit ? Math.Min(1f, moveAcc / MOVE_TICK) : 0f;
            float rpx = lastTickX + (px - lastTickX) * rAlpha;
            float rpy = lastTickY + (py - lastTickY) * rAlpha;
            float rpz = lastTickZ + (pz - lastTickZ) * rAlpha;

            // animation state
            if (skillUntil > now) { /* skill clip playing */ }
            else if (mount.Mounted) setClip(!grounded ? mount.RiderJump : mount.RiderClip);
            else if (!grounded) setClip(vy > 0f ? (jumpCount > 1 && clipDJump.Length > 0 ? clipDJump : clipJump) : clipFall);
            else if (now < landClipUntil) setClip(clipLand);
            else if (sitting) setClip(clipSit);
            else if (moving) setClip(
                gait == 1 ? clipStrafeL :
                gait == 2 ? clipStrafeR :
                gait == 3 ? clipBack :
                walkMode ? clipWalk : clipRun);
            else if (sheathOn && (long)Environment.TickCount < sheathDrawUntil) setClip(clipSheathDraw);
            else setClip(sheathOn ? clipSheathHold : clipIdle);

            // model update (only when changed; keeps animation alive).
            // Y must be part of the gate: a standing jump changes py only, and
            // without it the model stays at the takeoff height (stutter/"stuck
            // in the middle"); moving jumps updated via X/Z and looked fine.
            if (Math.Abs(rpx - lastModelX) > 0.5f || Math.Abs(rpy - lastModelY) > 0.5f ||
                Math.Abs(rpz - lastModelZ) > 0.5f ||
                Math.Abs(curYaw - lastModelYaw) > 0.01f)
            {
                placePlayer(rpx, rpy, rpz, curYaw);
                lastModelX = rpx; lastModelY = rpy; lastModelZ = rpz; lastModelYaw = curYaw;
            }
            if (mount.Mounted)
                mount.Update(scene, rpx, rpy, rpz, curYaw, grounded, moving, Log);
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
                // Camera follow mode (client CAMERA_MODE enum, enum_ui.lua
                // pc2977-2987: 0 NEVER_FOLLOW, 1 AUTO_FOLLOW, 2 ALWAYS_FOLLOW;
                // the per-mode value nCameraModeIn<Mode> reaches the camera node
                // via the decoded Camera_SetFollowMode binding -> +0x80/+0x98).
                // AUTO = the move+turn row (CameraAdjustYawWhenMoveTurn);
                // ALWAYS also follows a plain move; NEVER is mouse-only.
                // Joystick follows the TRAVEL direction (the client's
                // RotatePlayer drives Camera_SetResetSpeed per frame from the
                // movement direction), classical follows the control frame.
                // RC_FOLLOW_MODE overrides for scripted tests.
                int followMode = cameraSettings.ActiveFollowMode;
                string followEnv = Env("RC_FOLLOW_MODE", "");
                if (followEnv.Length > 0)
                {
                    int fv;
                    if (int.TryParse(followEnv, out fv)) followMode = fv;
                }
                if (followMode < 0) followMode = 0;
                else if (followMode > 2) followMode = 2;
                if (followMode != 0 && movingNow && !lmbDown && !rmbDown)
                {
                    bool followNow = classicalMode
                        ? (followMode == 2 || rotAxis != 0f)
                        : true;   // joystick: AUTO and ALWAYS follow the travel
                    if (followNow)
                    {
                        // Target = the TRAVEL heading (camera convention). Using
                        // the control frame here was a no-op (it equals the
                        // camera unless dragging); the client's row follows the
                        // run direction. Forward W: travel == camera -> no-op;
                        // strafe/diagonal: the camera swings behind the travel.
                        double followYaw = Math.Atan2(-dirZ, -dirX);
                        camSys.FollowYaw(followYaw, dt);
                    }
                }
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
                // no automatic camera-mode switching: the sprint trigger
                // (double-tap W, WW) was removed 2026-09-30; the sprint row is
                // reachable only through the RC_CAM_MODE test harness.
                // P1 track playback clock (frames at 30 fps; RC_CAM_ANI_FPS overrides)
                camTrackActive = camTrack != null && camTrack.Active;
                if (camTrackActive) camTrack.Update(dt * 1000.0);
                double dist = camSys.UpdateDistance(dt) * cameraSettings.EyeScale;
                // any distance change (zoom, sprint pull-back, EyeScale)
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
                bool doSmooth = (cameraSettings == null || cameraSettings.CameraSmoothing) &&
                                Env("RC_CAM_NOSMOOTH", "0") != "1";
                // Placement smoothing is one shared state in every camera mode
                // (CharacterCameraSmoothTime, 60 ms; PENETRATION_PLAN C1). The
                // sprint row's SmoothTime (0.5 s) is SprintCameraSmoothTime, the
                // sprint pull-back constant already applied by UpdateDistance -
                // reading the active row here collapsed the orbit radius while
                // dragging in sprint mode (2026-09-30 camera-wwdrag repro).
                double stime = Math.Max(
                    camSys.Rows[CameraSystem.MODE_CHARACTER].F("SmoothTime", 0.06), 1e-3);
                // B14 anchor-Y smooth-follow: the engine smooths the followed
                // character position (JX3RepresentX64 "DynamicFollowSmoothObjectPosition";
                // CharacterCameraSmoothTime = 60 ms, Represent/common/number.krl.txt).
                // The host feeds raw physics py + 90 as the anchor, so a discrete
                // vertical snap (step/ledge, up to the 64 u ground tolerance)
                // teleported the camera. Only one-frame snaps (|dy| > 5 u while
                // grounded) are eased over SmoothTime; continuous slope motion and
                // airborne frames pass through. Kill switch RC_CAM_YFOLLOW=0.
                double ay2Raw = py + 90.0;
                if (!camYInit)
                {
                    camYSmooth = ay2Raw; camYPrevRaw = ay2Raw; camYInit = true;
                }
                if (grounded && Math.Abs(ay2Raw - camYPrevRaw) > 5.0) camYEasing = true;
                double camYBefore = camYSmooth;
                if (camYEasing && camYFollow && doSmooth)
                {
                    double dy3 = ay2Raw - camYSmooth;
                    if (Math.Abs(dy3) > 0.05)
                    {
                        // exponential follow (native rule) with a frame-time
                        // clamp; the catch-up rate is capped so a multi-snap
                        // climb (hundreds of u in a few frames) is traversed at
                        // a bounded speed instead of teleporting (RC_CAM_YRATE).
                        double yFrac = Math.Min(dt, 0.005) / stime;
                        double yStep = dy3 * yFrac;
                        double yCap = camYMaxRate * Math.Min(dt, 0.02);
                        if (Math.Abs(yStep) > yCap) yStep = (yStep > 0.0 ? yCap : -yCap);
                        camYSmooth += yStep;
                    }
                    else
                    {
                        camYSmooth = ay2Raw;
                        camYEasing = false;
                    }
                }
                else
                {
                    camYSmooth = ay2Raw;
                    camYEasing = false;
                }
                double ax2 = rpx, ay2 = camYFollow ? camYSmooth : (rpy + 90.0), az2 = rpz;
                if (camYDbg)
                {
                    double aStep = camYAnchorInit ? ay2 - camYAnchorPrev : 0.0;
                    if (Math.Abs(aStep) > 1.0 || Math.Abs(ay2Raw - camYPrevRaw) > 4.0)
                        Log(string.Format("ydbg rawstep={0:F1} sm={1:F1} anchorstep={2:F1} grounded={3}",
                            ay2Raw - camYPrevRaw, camYSmooth - camYBefore, aStep, grounded));
                }
                camYPrevRaw = ay2Raw;
                camYAnchorPrev = ay2; camYAnchorInit = true;
                // Camera probes + obstruction use the candidate (desired) camera
                // line, not the per-axis smoothed offset: smoothing a rotating
                // vector through its chord shortens it, and feeding that to the
                // obstruction state machine snapped the camera in on fast flicks
                // (immediate "shortening") with a slow flex return (HOST_DEVIATIONS
                // B15). The game's per-axis SmoothTime now applies once, to the
                // resolved offset (rSm) below.
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
                if (colProf) camSw.Restart();
                bool doCamQuery = !camTrackActive;
                if (camQueryHz > 0.0)
                {
                    camQueryAcc += dt;
                    if (camQueryAcc >= 1.0 / camQueryHz) camQueryAcc = 0.0;
                    else doCamQuery = false;
                }
                if (!doCamQuery) hitDist = lastCamHit;
                if (col != null && doCamQuery)
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
                    if (colProf) { bakeSw.Restart(); natSw.Restart(); }
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
                        // game camera class set: every instance passes through
                        // the per-mesh bObscatleCamera gate (cflags sidecars);
                        // front faces only unless the double-sided experiment
                        // (RC_CAM_BACKFACE=1) is on
                        float h = col.Raycast(px2, py2, pz2, qx2, qy2, qz2, false, probeFrontOnly, true);
                        float bh = h;
                        int bInst = col.LastInst, bTri = col.LastTri;
                        bool bBlk = col.LastBlocksCamera, bFol = col.LastFromFoliage;
                        // engine rays: the game's camera mask 0x301 covers terrain
                        // and scene entities, which the baked set cannot fully cover
                        if (colProf) { bakeSw.Stop(); natSw.Start(); }
                        float th = engineRay.RayTerrain(px2, py2, pz2, qx2, qy2, qz2);
                        if (th > 0f && (h <= 0f || th < h)) h = th;
                        float sh = sceneRayCam ? engineRay.RayScene(px2, py2, pz2, qx2, qy2, qz2) : -1f;
                        if (colProf) { natSw.Stop(); bakeSw.Start(); }
                        if (sh > 0f && sh < sceneMin) sh = -1f;   // self/exit faces (B12)
                        if (sh > 0f && (h <= 0f || sh < h)) h = sh;
                        // degenerate-hit guard (registered, RC_CAM_HITMIN=0
                        // disables): a hit a few units from the probe origin is
                        // the raw scene backend's self/exit/grazing face (bake
                        // clear, scene 0.1-2.3 u in the T1 sweep), not a wall
                        // between anchor and camera - a wall that close to the
                        // head would floor the pull at the anchor anyway, so
                        // ignoring it only removes the false teleport.
                        if (h > 0f && h < hitMinDist)
                        {
                            if (obstDbg && now - lastObstLog >= 500)
                                Log(string.Format("obstdbg degenerate probe{0} off=({1:F0},{2:F0},{3:F0}) h={4:F1} (bake={5:F1} terr={6:F1} scene={7:F1}) ignored",
                                    p, ox2, oy2, oz2, h, bh, th, sh));
                            h = -1f;
                        }
                        if (h > 0f && (hitDist < 0.0 || h < hitDist))
                        {
                            hitDist = h;
                            hitSrc = string.Format("probe{0} off=({1:F0},{2:F0},{3:F0}) bake={4:F0} terr={5:F0} scene={6:F0}",
                                p, ox2, oy2, oz2, bh, th, sh);
                        }
                        if (obstDbg && h > 0f && h < 700f && now - lastObstLog >= 500)
                            Log(string.Format("obstdbg probe{0} off=({1:F0},{2:F0},{3:F0}) bake={4:F1}(inst={8},tri={9},blk={10},fol={11}) terr={5:F1} scene={6:F1} h={7:F1}",
                                p, ox2, oy2, oz2, bh, th, sh, h, bInst, bTri, bBlk ? 1 : 0, bFol ? 1 : 0));
                        if (penDbg)
                            penCur.Append(string.Format(" p{0} off=({1:F0},{2:F0},{3:F0}) bake={4:F0}(i={5},t={6},blk={7},fol={8}) terr={9:F0} scene={10:F0} h={11:F0}",
                                p, ox2, oy2, oz2, bh, bInst, bTri, bBlk ? 1 : 0, bFol ? 1 : 0, th, sh, h));
                    }
                }
                // engine vertical backend: the game mask's vertical probe.
                // Sampling it along the camera line catches vertical/cliff
                // geometry no horizontal ray reports.
                if (colProf && doCamQuery)
                {
                    bakeSw.Stop(); natSw.Stop();
                    bakeMsSum += bakeSw.Elapsed.TotalMilliseconds;
                    natMsSum += natSw.Elapsed.TotalMilliseconds;
                }
                // The vertical ladder is a host-authored extra (not in the
                // recovered engine probe set); when the horizontal probes
                // already found a wall it is skipped - next to the big 玉门关
                // building it alone cost ~1.9 ms/frame (RC_COL_PROF).
                if (doCamQuery && hitDist < 0.0)
                {
                if (colProf) vertSw.Restart();
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
                if (colProf) { vertSw.Stop(); vertMsSum += vertSw.Elapsed.TotalMilliseconds; }
                }
                // terrain read as another obstruction ray (center probe march)
                if (sampler != null && doCamQuery)
                {
                    if (colProf) sampSw.Restart();
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
                if (colProf && doCamQuery) { sampSw.Stop(); sampMsSum += sampSw.Elapsed.TotalMilliseconds; }
                lastCamHit = hitDist;
                hitDist = camObst.Stabilize(dt, hitDist);
                double camLen = camObst.Update(dt, offLen, hitDist);
                dbgHit = hitDist; dbgLen = camLen; dbgObst = camObst.Obstructed;
                dbgEffDist = dist; dbgSrc = hitSrc;
                if (colProf && doCamQuery)
                {
                    camSw.Stop();
                    double cms = camSw.Elapsed.TotalMilliseconds;
                    camMsSum += cms;
                    camProfFrames++;
                    if (cms > camMsMax) camMsMax = cms;
                }
                // resolved-offset smoothing (their plan step 3): the pull result
                // moves at the same per-axis limited rate as the orbit offset,
                // so a one-frame hit change (corner-probe graze 123->88, backend
                // flicker 3/18 u) cannot teleport the camera. Anchor-relative:
                // player motion and orbit changes still follow immediately.
                double s0 = camLen / offLen;
                double[] rWant = { camOff[0] * s0, camOff[1] * s0, camOff[2] * s0 };
                // clamp the smoothing step clock: a streaming hitch (dt ~ 0.5 s)
                // would otherwise turn the limited approach into a snap
                double dtc = Math.Min(dt, 0.05);
                if (!rSmInit)
                {
                    rSm[0] = rWant[0]; rSm[1] = rWant[1]; rSm[2] = rWant[2]; rSmInit = true;
                }
                else if (doSmooth)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        double d3 = rWant[i] - rSm[i];
                        if (Math.Abs(d3) > 1e-6 && Math.Abs(d3) > Math.Abs(d3) * dtc / stime)
                            rSm[i] += d3 * dtc / stime;
                        else
                            rSm[i] = rWant[i];
                    }
                }
                else
                {
                    rSm[0] = rWant[0]; rSm[1] = rWant[1]; rSm[2] = rWant[2];
                }
                // shake detector (RC_CAM_SHAKEDBG=1): the nausea case is the
                // signed pull oscillating across the anchor (view flips pi).
                // Count sign flips in a rolling 2 s window and log bursts.
                if (shakeDbg)
                {
                    // frame-to-frame resolved-length jumps (position teleports):
                    // raw = the pull value, sm = the smoothed camera the player
                    // actually sees
                    double smLen = Math.Sqrt(rSm[0] * rSm[0] + rSm[1] * rSm[1] + rSm[2] * rSm[2]);
                    if (shakeHavePrev && Math.Abs(camLen - shakePrevLen) > 5.0)
                    {
                        // smstep = the camera the player sees moved this much in
                        // the last frame; acceptance: raw |d| > 5 -> |smstep| <= 2
                        Log(string.Format("jumpdbg prev={0:F1} now={1:F1} d={2:F1} sm={3:F1} smstep={4:F2} hit={5:F0} src=[{6}] offLen={7:F0}",
                            shakePrevLen, camLen, camLen - shakePrevLen, smLen, smLen - shakePrevSm, hitDist, hitSrc, offLen));
                    }
                    shakePrevLen = camLen; shakePrevSm = smLen; shakeHavePrev = true;
                    int sign = camLen < 0 ? -1 : (camLen > 0 ? 1 : 0);
                    if (shakePrevSign != 0 && sign != 0 && sign != shakePrevSign)
                    {
                        shakeFlips.Add(now);
                    }
                    if (sign != 0) shakePrevSign = sign;
                    while (shakeFlips.Count > 0 && now - shakeFlips[0] > 2000) shakeFlips.RemoveAt(0);
                    if (shakeFlips.Count >= 3 && now - shakeLastLog >= 1000)
                    {
                        shakeLastLog = now;
                        Log(string.Format("shakedbg flips={0}/2s camLen={1:F1} hit={2:F0} yaw={3:F3} pitch={4:F3} obst={5}",
                            shakeFlips.Count, camLen, hitDist, camSys.Yaw, camSys.Pitch,
                            camObst.Obstructed ? 1 : 0));
                    }
                }
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

                double camX, camY, camZ;
                if (camTrackActive)
                {
                    // authored .mani track: camera position from track A, look-at
                    // from track B; no orbit/obstruction/shake/terrain-clamp.
                    double tkx, tky, tkz, tax, tay, taz;
                    camTrack.Sample(camTrack.Frame,
                        out tkx, out tky, out tkz, out tax, out tay, out taz);
                    ax2 = tax; ay2 = tay; az2 = taz;
                    camX = tkx; camY = tky; camZ = tkz;
                    aimPitchOverride = double.NaN;
                }
                else
                {
                camX = ax2 + rSm[0];
                camY = ay2 + rSm[1];
                camZ = az2 + rSm[2];
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
                }
                // final-camera wall gate (T1.5): the camera->anchor segment must
                // be clear; if any wall sits between, retract along that line so
                // the camera can never sit on the far side of geometry
                if (!camTrackActive && wallGate && engineRay.Available)
                {
                    // same camera gate as the probes
                    float g1 = col != null ? col.Raycast((float)camX, (float)camY, (float)camZ,
                        (float)ax2, (float)ay2, (float)az2, false, true, true) : -1f;
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
                if (!usedNativeCam && camTrackActive)
                {
                    // authored track: one exact managed set, no snap guard
                    scene.SetCameraPos((float)camX, (float)camY, (float)camZ, false);
                }
                else if (!usedNativeCam)
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
                if (camTrackActive && now - lastTrackLog >= 1000)
                {
                    lastTrackLog = now;
                    float tx2 = 0f, ty2 = 0f, tz2 = 0f;
                    try { scene.GetCameraPos(ref tx2, ref ty2, ref tz2); } catch { }
                    Log(string.Format("camani frame={0:F1}/{1:F0} cam=({2:F0},{3:F0},{4:F0}) aim=({5:F0},{6:F0},{7:F0}) applied=({8:F0},{9:F0},{10:F0})",
                        camTrack.Frame, camTrack.Duration, camX, camY, camZ, ax2, ay2, az2, tx2, ty2, tz2));
                }
                // P2 skill-move camera FOV (scripted trigger; gameplay hook waits
                // for the skill runtime - FLWS has no table row).
                if (skillMoveRow != null)
                {
                    if (!skillMoveFx.Active && skillMoveFireMs >= 0.0 && now >= skillMoveFireMs)
                    {
                        skillMoveFx.Start(skillMoveRow, now);
                        Log(string.Format("skillmove start skill={0} t={1}ms enter={2:F0} exit={3:F0} dur={4:F0} fov={5} (screenFX={6} edge={7} sat={8} logged only)",
                            skillMoveRow.SkillId, now, skillMoveRow.EnterMs, skillMoveRow.ExitMs,
                            skillMoveRow.DurationMs,
                            skillMoveRow.FixedFov
                                ? skillMoveRow.FovValue.ToString("F0") + "deg"
                                : skillMoveRow.FovValue.ToString("F2") + "rad+",
                            skillMoveRow.ScreenFx ? 1 : 0, skillMoveRow.Edge, skillMoveRow.Sat));
                    }
                    if (skillMoveFx.Active)
                    {
                        double smPhase; int smStage;
                        double smAngle = skillMoveFx.AngleAt(now, baseViewAngle * VideoSettings.DefaultAngle,
                            out smPhase, out smStage);
                        skillMoveStage = smStage;
                        if (smAngle > 0.0)
                        {
                            float smFactor = (float)(smAngle / VideoSettings.DefaultAngle);
                            try { scene.SetViewAngleFactor(smFactor); } catch (Exception) { }
                            if (now - lastSkillMoveLog >= 500)
                            {
                                lastSkillMoveLog = now;
                                Log(string.Format("skillmove stage={0} phase={1:F2} angle={2:F1}deg factor={3:F3} (linear ramp, provisional)",
                                    smStage, smPhase, smAngle * 180.0 / Math.PI, smFactor));
                            }
                        }
                        else
                        {
                            try { scene.SetViewAngleFactor(baseViewAngle); } catch (Exception) { }
                            Log(string.Format("skillmove end t={0}ms -> base factor {1:F3}", now, baseViewAngle));
                            skillMoveRow = null;   // one-shot scripted effect
                        }
                    }
                }

                // Character visibility near the camera: the native client fades
                // the character out as the camera closes in (engine model fade
                // measured at camLen ~36..96 u, HANDOFF section 4) on top of the
                // view near plane. The host has no visibility/near-plane API, so
                // the dummy is parked below the map while the camera is inside
                // the character volume. Threshold must exceed the head offset
                // (~90 u above the chest anchor) - a camera inside the head
                // hovers at camDist ~90 and a 90 u threshold never fired
                // (reported: "I see the inside of the character").
                double camDist = Math.Sqrt((camX - ax2) * (camX - ax2) +
                                           (camY - ay2) * (camY - ay2) +
                                           (camZ - az2) * (camZ - az2));
                if (hideNear)
                {
                    if (!playerHidden && camDist < 105.0)
                    {
                        playerHidden = true;
                        Log(string.Format("hideNear hide camDist={0:F1} (B1 host approximation)", camDist));
                        placePlayer(px, py, pz, curYaw);
                    }
                    else if (playerHidden && camDist > 250.0)
                    {
                        playerHidden = false;
                        Log(string.Format("hideNear show camDist={0:F1}", camDist));
                        placePlayer(px, py, pz, curYaw);
                    }
                }
                }
            }
            catch (Exception e) { Log("camera system ex: " + e.Message); }

            engine.FrameMove();
            if (soundReady) { try { sound.FrameMove(); } catch { } }
            if (soundNative) { try { SoundProbe.Render(); } catch { } }
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

            if (penDbg)
            {
                // Step 1 penetration recorder (RC_CAM_PENDBG=1, diagnostics
                // only): reverse cast actual camera -> anchor with all three
                // backends every frame; an event fires when drawn geometry sits
                // between them. The last ~150 frames of per-probe hits are the
                // event context; classification is manual (plan steps 3-4).
                penRing.Add(string.Format("{0} cam=({1:F0},{2:F0},{3:F0}) anchor=({4:F0},{5:F0},{6:F0}) len={7:F0} hit={8:F0} src=[{9}] obst={10}{11}",
                    now, preX, preY, preZ, preAX, preAY, preAZ, dbgLen, dbgHit, dbgSrc, dbgObst ? 1 : 0, penCur.ToString()));
                if (penRing.Count > 150) penRing.RemoveAt(0);
                penCur.Length = 0;
                if (preSet && col != null)
                {
                    float abx = 0f, aby = 0f, abz = 0f;
                    try { scene.GetCameraPos(ref abx, ref aby, ref abz); } catch { }
                    float ax = preAX, ay = preAY, az = preAZ;
                    double ddx = abx - ax, ddy = aby - ay, ddz = abz - az;
                    double dcam = Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                    if (dcam > 5.0)
                    {
                        // both orientations: a reverse cast meets the face the
                        // forward probe saw as its front (front-only would skip
                        // every wall here)
                        float g1 = col.Raycast(abx, aby, abz, ax, ay, az, false, false, true);
                        int g1i = col.LastInst, g1t = col.LastTri;
                        bool g1b = col.LastBlocksCamera, g1f = col.LastFromFoliage;
                        float g2 = col.Raycast(abx, aby, abz, ax, ay, az, false, false, false);
                        int g2i = col.LastInst, g2t = col.LastTri;
                        bool g2b = col.LastBlocksCamera, g2f = col.LastFromFoliage;
                        float t1 = engineRay.RayTerrain(abx, aby, abz, ax, ay, az);
                        float s1 = engineRay.RayScene(abx, aby, abz, ax, ay, az);
                        penLastDcam = dcam; penLastG1 = g1; penLastG2 = g2; penLastT1 = t1; penLastS1 = s1;
                        // the scene backend hits the player's own model near
                        // the anchor end; the bake is map geometry only
                        const double endMargin = 2.0, bodyMargin = 40.0;
                        bool eG2 = g2 > 0f && g2 < dcam - endMargin;
                        bool eT = t1 > 0f && t1 < dcam - endMargin;
                        bool eS = s1 > 0f && s1 < dcam - bodyMargin;
                        if (eG2 || eT || eS)
                        {
                            penEvents++;
                            if (now - penLastLog >= 500)
                            {
                                penLastLog = now;
                                float rdx = abx - ax, rdy = aby - ay, rdz = abz - az;
                                float rl = (float)Math.Sqrt(rdx * rdx + rdy * rdy + rdz * rdz);
                                float rr = -1f, sl = -1f;
                                int slHr = 0, slHit = 0;
                                if (rl > 1f)
                                {
                                    rr = engineRay.RayTerrain(ax, ay, az,
                                        ax + rdx / rl * 600f, ay + rdy / rl * 600f, az + rdz / rl * 600f);
                                    sl = engineRay.RaySceneLevel(ax, ay, az,
                                        ax + rdx / rl * 600f, ay + rdy / rl * 600f, az + rdz / rl * 600f);
                                    slHr = engineRay.LastHr; slHit = engineRay.LastHit;
                                }
                                Log(string.Format("pendbg event n={0} map={1} player=({2:F0},{3:F0},{4:F0}) yaw={5:F3} pitch={6:F3} camActual=({7:F0},{8:F0},{9:F0}) camIntended=({10:F0},{11:F0},{12:F0}) anchor=({13:F0},{14:F0},{15:F0}) dcam={16:F1} len={17:F1} hit={18:F0} src=[{19}] gated={20:F0}(i={21},t={22},blk={23},fol={24}) ungated={25:F0}(i={26},t={27},blk={28},fol={29}) terr={30:F0} scene={31:F0} rayPost={32:F0} sceneLevel={33:F0}(hr={34},hit={35})",
                                    penEvents, penMapName, px, py, pz, camSys.Yaw, camSys.Pitch,
                                    abx, aby, abz, dbgIntX, dbgIntY, dbgIntZ, ax, ay, az, dcam,
                                    dbgLen, dbgHit, dbgSrc,
                                    g1, g1i, g1t, g1b ? 1 : 0, g1f ? 1 : 0,
                                    g2, g2i, g2t, g2b ? 1 : 0, g2f ? 1 : 0,
                                    t1, s1, rr, sl, slHr, slHit));
                                for (int i = 0; i < penRing.Count; i++) Log("pendbg ring " + penRing[i]);
                            }
                        }
                    }
                }
                if (now - penLastSummary >= 10000)
                {
                    penLastSummary = now;
                    Log(string.Format("pendbg summary events={0} frames={1} lastDcam={2:F1} g1={3:F0} g2={4:F0} t1={5:F0} s1={6:F0}",
                        penEvents, penRing.Count, penLastDcam, penLastG1, penLastG2, penLastT1, penLastS1));
                }
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
                string state = (mount.Mounted ? "MOUNT " : "")
                             + (skillUntil > now ? "SKILL" : !grounded ? ((vy > 0f ? "JUMP" : "FALL") + (jumpCount > 1 ? jumpCount.ToString() : ""))
                             : now < landClipUntil ? "LAND"
                             : moving ? (shiftDown ? "RUN x10" : walkMode ? "WALK" : "RUN") : "IDLE");
                float moveSpeed = shiftDown ? pRun * 10f
                                : mount.Mounted ? (walkMode ? rideWalk : rideRun)
                                : walkMode ? pSpeed
                                : pRun;
                double hudFovFactor = 1.0;
                try { hudFovFactor = scene.GetViewAngleFactor(); } catch (Exception) { }
                string camExtra = string.Format(" fov {0:F0}deg obst {1} len {2:F0}",
                    hudFovFactor * VideoSettings.DefaultAngle * 180.0 / Math.PI,
                    camObst.Obstructed ? "ON" : "off", dbgLen);
                if (camTrackActive)
                    camExtra += string.Format(" ani f{0:F0}/{1:F0}", camTrack.Frame, camTrack.Duration);
                if (skillMoveRow != null)
                    camExtra += string.Format(" skillmove s{0}", skillMoveStage);
                hud.SetText(string.Format(
                    "JX3\nfps {0}\npos {1:F0},{2:F0},{3:F0}\nstate {4}{5} hits {6}\nspeed {7:F1} \u5C3A/s\ncam {8} yaw {9:F2} dist {10:F0}{11}\nclip {12}\nmount {13}\nhitch {14}ms\nWASD move | / walk-run | Shift 10x | Space jump | 1 skill | T mount | C teleport | Esc info\nLMB drag = camera | RMB drag = camera+turn | +/- zoom | F11 reset | Home/End view\nF5 row | F6/F8 fov | PgUp/PgDn dist",
                    fps, px, py, pz, state, blocked ? " (blocked)" : "", blockedEvents,
                    moving ? moveSpeed / 64f : 0f,
                    camSys.Mode, camSys.Yaw, camSys.Distance, camExtra,
                    curClip == null ? "-" : Path.GetFileName(curClip),
                    mount.Mounted ? ("on ride " + mount.RideType) : "off",
                    hudHitchMs));
                if (hudLog)
                    Log("hudtext " + string.Format(
                        "fps={0} cam={1} yaw={2:F2} dist={3:F0} fov={4:F0}deg obst={5} len={6:F0}{7} hitch={8}ms mount={9}",
                        fps, camSys.Mode, camSys.Yaw, camSys.Distance,
                        hudFovFactor * VideoSettings.DefaultAngle * 180.0 / Math.PI,
                        camObst.Obstructed ? "ON" : "off", dbgLen, camExtra, hudHitchMs,
                        mount.Mounted ? ("on" + mount.RideType) : "off"));
                hudHitchMs = 0;
                // top-left control-mode name (always visible)
                hud.SetModeText("CONTROL: "
                    + (cameraSettings.OperationMode == CameraOperationMode.Joystick
                        ? "JOYSTICK" : "CLASSICAL")
                    + "   [/] switch   cam " + camSys.Mode + " (F5)");
                hud.PlaceOver(form);
                hud.UpdateLayered();
            }
            // target frame (Targeting.cs): real client UI composited over the viewport
            if (targetFrame != null && targetHudOn)
            {
                targetFrame.Target = targetSelector.Current;
                if (targetSelector.Current != null)
                {
                    double tdx = targetSelector.Current.X - px, tdz = targetSelector.Current.Z - pz;
                    targetFrame.Distance = Math.Sqrt(tdx * tdx + tdz * tdz);
                    targetFrame.PlaceOver(form);
                }
                targetFrame.UpdateLayered();
            }
            // in-world indicator (KRLTarget visuals): selection effect + arrow
            // at the current target, removed when the selection changes
            if (indEnabled)
            {
                TargetEntity want = indAlways ? dummyTarget : targetSelector.Current;
                if (want != indTarget)
                {
                    try
                    {
                        if (indTarget != null)
                        {
                            scene.RemoveDummyModel("target_indicator_sel");
                            scene.RemoveDummyModel("target_indicator_arrow");
                        }
                        indTarget = want;
                        if (want != null)
                        {
                            var irot = new CLRfloat4(); irot.x = 0f; irot.y = 0f; irot.z = 0f; irot.w = 1f;
                            var iscl = new CLRfloat3(); iscl.x = 1f; iscl.y = 1f; iscl.z = 1f;
                            var spos = new CLRfloat3(); spos.x = want.X; spos.y = want.Y + indY; spos.z = want.Z;
                            long hs = scene.AddDummyModel("target_indicator_sel", indSel, spos, irot, iscl);
                            // facing arrow: authored in the ground plane (XZ), so
                            // it sits at the feet and yaws with the target facing
                            double ah = (dummyYaw + indArrowYaw) * 0.5;
                            var arot = new CLRfloat4();
                            arot.x = 0f; arot.y = (float)Math.Sin(ah); arot.z = 0f; arot.w = (float)Math.Cos(ah);
                            var ascl = new CLRfloat3();
                            ascl.x = indArrowScale; ascl.y = indArrowScale; ascl.z = indArrowScale;
                            var apos = new CLRfloat3(); apos.x = want.X; apos.y = want.Y + indArrowY; apos.z = want.Z;
                            long ha = scene.AddDummyModel("target_indicator_arrow", indArrow, apos, arot, ascl);
                            Log(string.Format("target indicator '{0}' h={1} y=+{2:F0} | arrow h={3} y=+{4:F0} s={5:F2} yaw={6:F2}",
                                indSel, hs, indY, ha, indArrowY, indArrowScale, dummyYaw + indArrowYaw));
                        }
                        else Log("target indicator removed");
                    }
                    catch (Exception e) { Log("target indicator ex: " + e.Message); }
                }
            }
            while (tabAt.Count > 0 && now >= tabAt[0])
            {
                tabAt.RemoveAt(0);
                Log("RC_TAB_AT -> Tab (target next)");
                targetSelector.Cycle(px, pz, curYaw, false, Log);
            }
            while (clickAt.Count > 0 && now >= clickAt[0][0])
            {
                int cx = clickAt[0][1], cy = clickAt[0][2];
                clickAt.RemoveAt(0);
                Log(string.Format("RC_CLICK_AT -> click at {0},{1}", cx, cy));
                clickSelectAt(cx, cy);
            }
            if (now - lastLog >= 2000)
            {
                lastLog = now;
                // name the first blocker of each new contact (evidence: which
                // source model blocks the player, from the .meshes.txt sidecar)
                if (col != null && blocked && col.LastBlockedInst >= 0 &&
                    col.LastBlockedInst != lastBlkInstLogged)
                {
                    lastBlkInstLogged = col.LastBlockedInst;
                    int bmi = col.GetInstanceMesh(col.LastBlockedInst);
                    string bmp = col.GetMeshPath(bmi);
                    Log(string.Format("blocked by inst={0} mesh={1} top={2:F1} feet={3:F1} {4}",
                        col.LastBlockedInst, bmi, col.LastBlockedTriTop, py,
                        bmp == null ? "(no name sidecar)" : bmp));
                }
                if (holeDumpDir.Length > 0 && sampler != null && sampler.HasHoles &&
                    (sampler.HoleRegionX != lastHoleIx || sampler.HoleRegionZ != lastHoleIz))
                {
                    lastHoleIx = sampler.HoleRegionX;
                    lastHoleIz = sampler.HoleRegionZ;
                    try
                    {
                        byte[] mask = sampler.HoleMaskCopy();
                        string fp = Path.Combine(holeDumpDir,
                            string.Format("holes_{0}_{1:D3}_{2:D3}.bin", penMapName, lastHoleIx, lastHoleIz));
                        using (System.IO.BinaryWriter w = new System.IO.BinaryWriter(System.IO.File.Create(fp)))
                        {
                            w.Write(1);
                            w.Write(sampler.RegionSize);
                            w.Write(lastHoleIx);
                            w.Write(lastHoleIz);
                            w.Write(mask.Length);
                            w.Write(mask);
                        }
                        Log("hole dump -> " + fp + " bytes=" + mask.Length);
                    }
                    catch (Exception e) { Log("hole dump ex: " + e.Message); }
                }
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
                if (colProf)
                {
                    long profTri = 0, profInst = 0;
                    if (col != null)
                    {
                        profTri = col.ProfTriTests;
                        profInst = col.ProfInstTouches;
                        col.ProfTriTests = 0;
                        col.ProfInstTouches = 0;
                    }
                    nearInfo += string.Format(" colms=avg{0:F2}/max{1:F2} tri={2} inst={3} calls={4}",
                        colProfFrames > 0 ? colMsSum / colProfFrames : 0.0, colMsMax,
                        profTri, profInst, colCalls - colCallsPrev);
                    nearInfo += string.Format(" camms=avg{0:F2}/max{1:F2} n={6} bake={2:F2} nat={3:F2} vert={4:F2} samp={5:F2}",
                        camProfFrames > 0 ? camMsSum / camProfFrames : 0.0, camMsMax,
                        camProfFrames > 0 ? bakeMsSum / camProfFrames : 0.0,
                        camProfFrames > 0 ? natMsSum / camProfFrames : 0.0,
                        camProfFrames > 0 ? vertMsSum / camProfFrames : 0.0,
                        camProfFrames > 0 ? sampMsSum / camProfFrames : 0.0,
                        camProfFrames);
                    colMsSum = 0; colMsMax = 0; colProfFrames = 0; colCallsPrev = colCalls;
                    camMsSum = 0; camMsMax = 0; camProfFrames = 0;
                    bakeMsSum = 0; natMsSum = 0; vertMsSum = 0; sampMsSum = 0;
                    if (col != null && blocked && col.LastBlockedInst >= 0)
                    {
                        float bx0, by0, bz0, bx1, by1, bz1;
                        col.GetInstanceBounds(col.LastBlockedInst, out bx0, out by0, out bz0, out bx1, out by1, out bz1);
                        nearInfo += string.Format(" blkInst={0} depth={1:F1} n=({2:F2},{3:F2},{4:F2}) py={5:F0} AABB({6:F0},{7:F0},{8:F0})-({9:F0},{10:F0},{11:F0})",
                            col.LastBlockedInst, col.LastBlockedDepth,
                            col.LastBlockedNx, col.LastBlockedNy, col.LastBlockedNz, col.LastBlockedPy,
                            bx0, by0, bz0, bx1, by1, bz1);
                    }
                }
                float curSpd = !moving ? 0f
                             : shiftDown ? pRun * 10f
                             : mount.Mounted ? (walkMode ? rideWalk : rideRun)
                             : walkMode ? pSpeed
                             : pRun;
                string moveMode = !moving ? (mount.Mounted ? "RIDE_IDLE" : "IDLE")
                                : shiftDown ? "RUN10"
                                : mount.Mounted ? "RIDE"
                                : walkMode ? "WALK"
                                : "RUN";
                Log(string.Format("t={0}s fps={1} pos=({2:F0},{3:F0},{4:F0}) vy={5:F0} grounded={6} blocked={7} hits={8} colCalls={9} colBlocked={10} spd={13:F0}u/s({14}) yaw={15:F2} dir=({16:F2},{17:F2}) auto={18} vj=({19:F0},{20:F0}) cmds_unhandled={21}({22}) gait={23} mode={24} ctx='{25}' sprint={26} hitch={27}{11} clip={12} mount={28}",
                    now / 1000, fps, px, py, pz, vy, grounded, blocked, blockedEvents,
                    colCalls, colBlockedCalls, nearInfo,
                    curClip == null ? "-" : Path.GetFileName(curClip),
                    curSpd, moveMode, curYaw, dirX, dirZ, autorunOn ? 1 : 0, vjx, vjz,
                    unhandledCmd, lastUnhandled, gait,
                    CameraOperationMode.Name(cameraSettings.OperationMode),
                    hotkeys.Context, sprintOn ? 1 : 0, hitchMaxMs,
                    mount.Mounted ? ("on" + mount.RideType) : "off"));
                hitchMaxMs = 0;
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
        if (sampler != null) Log("terrain stats " + sampler.StatsLine());
        // Clean shutdown (workstream D1). Isolation A/B (proof/host/d1_shutdown_ab.txt):
        // sound/log/mem uninit exit cleanly; engine.UnInit3DEngine() AVs the process at
        // exit (0xC0000005 after DONE) - so it is opt-in only for reproduction
        // (RC_SHUTDOWN=engine) and the default releases the safe subsystems.
        // RC_SHUTDOWN: "safe" (default) = sound+log+mem; "all" = same + engine (AV);
        // "engine" = repro only; "0" = disabled.
        string shut = Env("RC_SHUTDOWN", "safe");
        if (shut != "0")
        {
            if (shut == "all" || shut == "safe" || shut == "sound")
            { try { sound.UnInit(); Log("shutdown sound ok"); } catch (Exception e) { Log("shutdown sound ex: " + e.Message); } }
            if (shut == "all" || shut == "engine")
            { try { engine.UnInit3DEngine(); Log("shutdown engine ok"); } catch (Exception e) { Log("shutdown engine ex: " + e.Message); } }
            if (shut == "all" || shut == "safe" || shut == "log")
            { try { baselib.UninitLog(); Log("shutdown log ok"); } catch (Exception e) { Log("shutdown log ex: " + e.Message); } }
            if (shut == "all" || shut == "safe" || shut == "mem")
            { try { baselib.UnInitMemory(); Log("shutdown memory ok"); } catch (Exception e) { Log("shutdown memory ex: " + e.Message); } }
        }
        Log("DONE");
    }

    static string Env(string name, string def)
    {
        string v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    // Recon helper: log the public managed methods whose name matters for the
    // player / visibility / near-plane paths.
    // Reflection invoke by name+arity for the KG_EnvironmentCLR surface
    // (no compile-time signature needed for recon probes).
    static object InvokeEnv(Type t, object o, string name, params object[] args)
    {
        foreach (System.Reflection.MethodInfo mi in t.GetMethods())
        {
            if (mi.Name != name) continue;
            if (mi.GetParameters().Length != args.Length) continue;
            try { return mi.Invoke(o, args); } catch { }
        }
        return null;
    }

    static void DumpApi(string label, Type t)
    {
        System.Reflection.MethodInfo[] ms = t.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Static);
        foreach (System.Reflection.MethodInfo mi in ms)
        {
            string n = mi.Name;
            if (n.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Near", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Visible", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("View", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Object", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Weather", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Day", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Environment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Sky", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Cloud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Season", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)
                Log("api " + label + "." + n + "(" + mi.ReturnType.Name + ")");
        }
    }

    // P4 probe: read-only snapshot of the engine animation param table
    // (KTableList vector at [singleton]+0x1A0+0x1E2B8, 0x54-byte entries).
    // Entry fields decoded in docs/controls/CONTROL_MODES_P5_ANIM.md.
    [HandleProcessCorruptedStateExceptions]
    static void ProbeControl()
    {
        try
        {
            long repBase = 0;
            Process proc = Process.GetCurrentProcess();
            foreach (ProcessModule m in proc.Modules)
            {
                if (string.Equals(m.ModuleName, "JX3RepresentX64.dll",
                        StringComparison.OrdinalIgnoreCase))
                {
                    repBase = m.BaseAddress.ToInt64();
                    break;
                }
            }
            if (repBase == 0)
            {
                Log("probe: JX3RepresentX64.dll not loaded");
                return;
            }
            long singleton = ReadQWord(repBase + 0xF06A50);
            if (singleton == 0)
            {
                Log("probe animtable: singleton null base=0x" + repBase.ToString("X"));
                return;
            }
            long container = singleton + 0x1A0;
            long data = ReadQWord(container + 0x1E2B8);
            uint count = ReadU32(container + 0x1E2C0);
            Log(string.Format("probe animtable base=0x{0:X} singleton=0x{1:X} data=0x{2:X} count={3}",
                repBase, singleton, data, count));
            if (!probeTableDone && data != 0 && count > 0 && count <= 2048)
            {
                probeTableDone = true;
                int stride = 0x54;
                byte[] buf = new byte[(int)count * stride];
                Marshal.Copy(new IntPtr(data), buf, 0, buf.Length);
                for (int i = 0; i < count; i++)
                {
                    int o = i * stride;
                    Log(string.Format(
                        "probe entry {0}: mode={1} index={2} id0={3} clip0=0x{4:X} idMove={5} thrLo={6:F4} spdLo={7:F4} clipLo=0x{8:X} thrHi={9:F4} spdHi={10:F4} clipHi=0x{11:X}",
                        i,
                        BitConverter.ToUInt32(buf, o),
                        BitConverter.ToUInt32(buf, o + 4),
                        BitConverter.ToUInt32(buf, o + 0x30),
                        BitConverter.ToUInt32(buf, o + 0x34),
                        BitConverter.ToUInt32(buf, o + 0x4C),
                        BitConverter.ToSingle(buf, o + 0x50),
                        BitConverter.ToSingle(buf, o + 0x54),
                        BitConverter.ToUInt32(buf, o + 0x58),
                        BitConverter.ToSingle(buf, o + 0x68),
                        BitConverter.ToSingle(buf, o + 0x6C),
                        BitConverter.ToUInt32(buf, o + 0x70)));
                }
            }
        }
        catch (Exception e)
        {
            Log("probe ex: " + e.Message);
        }
    }

    [HandleProcessCorruptedStateExceptions]
    static long ReadQWord(long va)
    {
        byte[] b = new byte[8];
        Marshal.Copy(new IntPtr(va), b, 0, 8);
        return BitConverter.ToInt64(b, 0);
    }

    [HandleProcessCorruptedStateExceptions]
    static uint ReadU32(long va)
    {
        byte[] b = new byte[4];
        Marshal.Copy(new IntPtr(va), b, 0, 4);
        return BitConverter.ToUInt32(b, 0);
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

    // Native sound probe loader (RC_SOUND_HOOK=1): see native/sound_probe.cpp.
    internal static class SoundProbe
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr LoadLibraryA(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr h, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int InitFn(IntPtr logPathAnsi);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr StatusFn();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int LoadBankWFn([MarshalAs(UnmanagedType.LPWStr)] string path);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate uint PostEventFn(uint eventId, ulong go);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int DiagFn(uint playingId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int RenderFn();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int SetMediaDirFn([MarshalAs(UnmanagedType.LPWStr)] string dir);
        static IntPtr _init = IntPtr.Zero, _status = IntPtr.Zero, _loadBank = IntPtr.Zero, _postEvent = IntPtr.Zero, _diag = IntPtr.Zero, _render = IntPtr.Zero, _mediaDir = IntPtr.Zero;
        public static IntPtr Load(string path)
        {
            IntPtr h = LoadLibraryA(path);
            if (h != IntPtr.Zero)
            {
                _init = GetProcAddress(h, "RC_SoundProbe_Init");
                _status = GetProcAddress(h, "RC_SoundProbe_Status");
                _loadBank = GetProcAddress(h, "RC_SoundProbe_LoadBankW");
                _postEvent = GetProcAddress(h, "RC_SoundProbe_PostEvent");
                _diag = GetProcAddress(h, "RC_SoundProbe_Diag");
                _render = GetProcAddress(h, "RC_SoundProbe_Render");
                _mediaDir = GetProcAddress(h, "RC_SoundProbe_SetMediaDir");
            }
            return h;
        }
        public static int SetMediaDir(string dir)
        {
            if (_mediaDir == IntPtr.Zero) return -1;
            SetMediaDirFn f = (SetMediaDirFn)Marshal.GetDelegateForFunctionPointer(_mediaDir, typeof(SetMediaDirFn));
            return f(dir);
        }
        public static int Render()
        {
            if (_render == IntPtr.Zero) return -1;
            RenderFn f = (RenderFn)Marshal.GetDelegateForFunctionPointer(_render, typeof(RenderFn));
            return f();
        }
        public static int Diag(uint playingId)
        {
            if (_diag == IntPtr.Zero) return -1;
            DiagFn f = (DiagFn)Marshal.GetDelegateForFunctionPointer(_diag, typeof(DiagFn));
            return f(playingId);
        }
        public static int LoadBankW(string path)
        {
            if (_loadBank == IntPtr.Zero) return -1;
            LoadBankWFn f = (LoadBankWFn)Marshal.GetDelegateForFunctionPointer(_loadBank, typeof(LoadBankWFn));
            return f(path);
        }
        public static uint PostEvent(uint id, ulong go)
        {
            if (_postEvent == IntPtr.Zero) return 0;
            PostEventFn f = (PostEventFn)Marshal.GetDelegateForFunctionPointer(_postEvent, typeof(PostEventFn));
            return f(id, go);
        }
        public static int Init(string logPath)
        {
            if (_init == IntPtr.Zero) return -1;
            InitFn f = (InitFn)Marshal.GetDelegateForFunctionPointer(_init, typeof(InitFn));
            IntPtr p = Marshal.StringToHGlobalAnsi(logPath);
            try { return f(p); }
            finally { Marshal.FreeHGlobal(p); }
        }
        public static string Status()
        {
            if (_status == IntPtr.Zero) return "(no status export)";
            StatusFn f = (StatusFn)Marshal.GetDelegateForFunctionPointer(_status, typeof(StatusFn));
            IntPtr p = f();
            return p == IntPtr.Zero ? "(null)" : Marshal.PtrToStringAnsi(p);
        }
    }

    // cwd switch for the Wwise streamed-media base path (RC_SOUND_MEDIA).
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetCurrentDirectoryW(string path);

    // Provisional skill sound: the decoded FLWS WAV (SOUND_PATH.md) played via
    // winmm, because the engine's tani SoundTag does not fire in the host.
    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    const uint SND_ASYNC = 0x0001, SND_NODEFAULT = 0x0002, SND_FILENAME = 0x00020000;

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

// Application-wide Esc key filter: the form's KeyPreview only sees keys routed
// through WinForms controls, so when the engine's native child window has focus
// Esc would go there and the toggle would silently not fire. A message filter
// sees WM_KEYDOWN for every window in the process; bit 30 of lParam marks key
// auto-repeat, so one physical press = one toggle. The message is consumed.
internal sealed class EscKeyFilter : System.Windows.Forms.IMessageFilter
{
    public Action OnEscape;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != 0x0100) return false;                       // WM_KEYDOWN
        if (m.WParam.ToInt32() != 0x1B) return false;            // VK_ESCAPE
        long lp = m.LParam.ToInt64();
        if ((lp & (1L << 30)) == 0 && OnEscape != null) OnEscape();
        return true;
    }
}