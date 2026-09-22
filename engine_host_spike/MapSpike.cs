using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MovieEngineCLR;

// MapSpike �?separate map-display host (Spike B).
// Loads a MovieEditor map (龙门寻宝) into a fresh KGSceneCLR exactly the way
// SceneForm::LoadScene does: new KGSceneCLR() -> LoadMap(path, async) ->
// SetActiveEnvironment(). Renders the same AddOutputWindow + FrameMove/Render
// loop as the actor spike host. Kept as its own exe so the actor workstream
// (spike_host.exe) is untouched.
//
// Env switches:
//   MAP_ASYNC=1      pass true to LoadMap (editor default is false)
//   MAP_FULLLOAD=1   SetSceneFullLoading(true) after load
//   MAP_EDITOR=0     skip editor.Init
//   MAP_SOUND=1      init Wwise
//   MAP_AUTORUN=N    exit after N ms (default: run until window closed)
//   MAP_PATH=...     override map path (default 龙门寻宝)

internal static class MapSpike
{
    [STAThread]
    private static void Main(string[] args)
    {
        string editorRoot = @"C:\SeasunGame\MovieEditor";
        string startupPath = Path.Combine(editorRoot, "bin64");
        string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
        string defaultMapPath =
            "data\\source\\maps\\\u9F99\u95E8\u5BFB\u5B9D\\\u9F99\u95E8\u5BFB\u5B9D.jsonmap";
        string mapPath = Environment.GetEnvironmentVariable("MAP_PATH") ?? defaultMapPath;
        string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "map_spike_out");
        Directory.CreateDirectory(outDir);

        Action<string> Log = delegate(string s)
        {
            try { File.AppendAllText(Path.Combine(outDir, "map.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); }
            catch { }
        };

        bool mapAsync = Environment.GetEnvironmentVariable("MAP_ASYNC") == "1";
        bool mapFullLoad = Environment.GetEnvironmentVariable("MAP_FULLLOAD") == "1";
        bool withEditor = Environment.GetEnvironmentVariable("MAP_EDITOR") != "0";
        bool withSound = Environment.GetEnvironmentVariable("MAP_SOUND") == "1";
        int autoRunMs = 0;
        int.TryParse(Environment.GetEnvironmentVariable("MAP_AUTORUN"), out autoRunMs);

        Console.WriteLine("mapPath=" + mapPath);
        Console.WriteLine("async={0} fullload={1} editor={2} autorun={3}", mapAsync, mapFullLoad, withEditor, autoRunMs);
        Log("start mapPath=" + mapPath + " async=" + mapAsync + " fullload=" + mapFullLoad + " editor=" + withEditor);

        var form = new Form();
        form.Text = "JX3 Map Host \u2014 \u9F99\u95E8\u5BFB\u5B9D (right-drag pan | Alt+right orbit | wheel zoom | WASD fly + Shift fast | Q/E up/down | F focus | R reset)";
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = new System.Drawing.Size(1280, 720);
        var panel = new Panel();
        panel.Dock = DockStyle.Fill;
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();

        var baselib = new KGBaseCLR();
        var engine = new KGEngineCLR();
        var editor = new KGMovieEditorCLR();
        var sound = new KG3DSoundCLR();

        engine.SetRootPath(workingDir);
        try { baselib.InitConsoleLog(); } catch (Exception e) { Console.WriteLine("InitConsoleLog: " + e.Message); }
        Directory.CreateDirectory(Path.Combine(startupPath, "logs"));

        int r1 = 0, r2 = 0, r3 = 0;
        try { r1 = baselib.InitPath(workingDir, false); } catch (Exception e) { Console.WriteLine("InitPath ex: " + e.Message); }
        try { r2 = baselib.InitMemory("MovieEditor.memory"); } catch (Exception e) { Console.WriteLine("InitMemory ex: " + e.Message); }
        try { r3 = baselib.InitPak(false); } catch (Exception e) { Console.WriteLine("InitPak ex: " + e.Message); }
        Console.WriteLine("InitPath={0} InitMemory={1} InitPak={2}", r1, r2, r3);
        Log(string.Format("InitPath={0} InitMemory={1} InitPak={2}", r1, r2, r3));

        int err = 1;
        int ok = 0;
        try
        {
            ok = engine.Init3DEngine(startupPath, startupPath, workingDir, 0, "./configHttpFile.ini", ref err);
        }
        catch (Exception e)
        {
            Console.WriteLine("Init3DEngine ex: " + e);
            return;
        }
        Console.WriteLine("Init3DEngine={0} err={1}", ok, err);
        Log(string.Format("Init3DEngine={0} err={1}", ok, err));
        if (ok == 0) { Console.WriteLine("FATAL: engine init failed"); return; }

        if (withEditor)
        {
            try
            {
                int editorResult = editor.Init(editorRoot, err, form.Handle.ToInt64());
                Console.WriteLine("editor.Init result={0}", editorResult);
                Log("editor.Init result=" + editorResult);
            }
            catch (Exception e) { Console.WriteLine("editor.Init ex: " + e.Message); }
        }
        else
        {
            Console.WriteLine("editor.Init SKIPPED (pure engine mode)");
        }
        if (withSound)
        {
            try { sound.Init(startupPath, form.Handle.ToInt64()); }
            catch (Exception e) { Console.WriteLine("sound.Init ex: " + e.Message); }
        }

        // SceneForm pattern: bare KGSceneCLR, no AttachScene.
        var scene = new KGSceneCLR();
        Console.WriteLine("scene null={0}", scene == null);
        if (scene == null) { Console.WriteLine("FATAL: no scene"); return; }

        // Camera controls �?adapted from MovieEditor (RECON_CAMERA.md):
        //   right drag       = PAN_VIEW (3)
        //   Alt+right drag   = ROTATE_CAMERA (1) orbit
        //   Shift+right drag = ROTATE_VIEW (4)
        //   left drag        = selection refs (19/30), not camera
        //   MOUSE_MOVE (30) sent before every drag action on move
        //   wheel            = MOUSE_WHEEL (31, 1, dir, 1)
        //   W/S/A/D + Shift  = SetCamareMoveState flags (CAMERA_MOVE_STATE)
        //   Q/E              = camera up/down (cmsCamareUp/Down 256/2048)
        //   numpad +/-       = SPEED_UP (25) / SPEED_DOWN (26)
        //   F                = ZOOM_TO_OBJECT (1001), Alt+F = LOCATE_TO_OBJECT (1004)
        //   R                = ResetCameraPosLookAtUp
        const int CMS_FORWARD = 1, CMS_BACK = 2, CMS_MOVE_LEFT = 64, CMS_MOVE_RIGHT = 128,
                  CMS_UP = 256, CMS_DOWN = 2048, CMS_FAST = 4096;
        Func<int, int, int> makeLParam = delegate(int x, int y)
        {
            return ((y & 0xFFFF) << 16) | (x & 0xFFFF);
        };
        bool shiftDown = false;
        var pending = new System.Collections.Generic.Queue<int[]>();
        Action<Keys, int> camKey = delegate(Keys k, int state)
        {
            int flags = 0;
            switch (k)
            {
                case Keys.W: flags = CMS_FORWARD; break;
                case Keys.S: flags = CMS_BACK; break;
                case Keys.A: flags = CMS_MOVE_LEFT; break;
                case Keys.D: flags = CMS_MOVE_RIGHT; break;
                case Keys.Q: flags = CMS_UP; break;
                case Keys.E: flags = CMS_DOWN; break;
            }
            if (flags == 0) return;
            if (shiftDown && k != Keys.Q && k != Keys.E) flags |= CMS_FAST;
            pending.Enqueue(new int[] { 80, flags, state, 0 });
        };
        panel.MouseDown += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int sel = (Control.ModifierKeys & Keys.Control) != 0 ? 20
                        : (Control.ModifierKeys & Keys.Alt) != 0 ? 21
                        : (Control.ModifierKeys & Keys.Shift) != 0 ? 22 : 19;
                pending.Enqueue(new int[] { sel, 1, e.X, e.Y });
            }
        };
        panel.MouseMove += delegate(object s, MouseEventArgs e)
        {
            pending.Enqueue(new int[] { 30, 1, e.X, e.Y });
            if (e.Button == MouseButtons.Right)
            {
                int a = (Control.ModifierKeys & Keys.Shift) != 0 ? 4
                      : (Control.ModifierKeys & Keys.Alt) != 0 ? 1 : 3;
                pending.Enqueue(new int[] { a, 1, e.X, e.Y });
            }
            else if (e.Button == MouseButtons.Left)
            {
                pending.Enqueue(new int[] { 19, 1, e.X, e.Y });
            }
        };
        panel.MouseUp += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) pending.Enqueue(new int[] { 19, 0, e.X, e.Y });
        };
        MouseEventHandler wheel = delegate(object s, MouseEventArgs e)
        {
            pending.Enqueue(new int[] { 31, e.Delta < 0 ? 0 : 1, 0, 1 });
        };
        panel.MouseWheel += wheel;
        form.MouseWheel += wheel;
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Shift) { shiftDown = true; pending.Enqueue(new int[] { 80, CMS_FAST, 1, 0 }); }
            else if (e.KeyCode == Keys.W || e.KeyCode == Keys.S || e.KeyCode == Keys.A || e.KeyCode == Keys.D
                     || e.KeyCode == Keys.Q || e.KeyCode == Keys.E) camKey(e.KeyCode, 1);
            else if (e.KeyCode == Keys.Add) pending.Enqueue(new int[] { 81, 25, 1, 0 });
            else if (e.KeyCode == Keys.Subtract) pending.Enqueue(new int[] { 81, 26, 1, 0 });
            else if (e.KeyCode == Keys.F)
                pending.Enqueue(new int[] { (Control.ModifierKeys & Keys.Alt) != 0 ? 84 : 82, 0, 0, 0 });
            else if (e.KeyCode == Keys.R) pending.Enqueue(new int[] { 83, 0, 0, 0 });
        };
        form.KeyUp += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Shift) { shiftDown = false; pending.Enqueue(new int[] { 80, CMS_FAST, 0, 0 }); }
            else if (e.KeyCode == Keys.W || e.KeyCode == Keys.S || e.KeyCode == Keys.A || e.KeyCode == Keys.D
                     || e.KeyCode == Keys.Q || e.KeyCode == Keys.E) camKey(e.KeyCode, 0);
            else if (e.KeyCode == Keys.Add) pending.Enqueue(new int[] { 81, 25, 0, 0 });
            else if (e.KeyCode == Keys.Subtract) pending.Enqueue(new int[] { 81, 26, 0, 0 });
        };
        panel.Focus();

        // LoadMap �?mirrors SceneForm::LoadScene (return < 0 = fail).
        var swLoad = System.Diagnostics.Stopwatch.StartNew();
        int loadResult = -1;
        try
        {
            loadResult = scene.LoadMap(mapPath, mapAsync);
        }
        catch (Exception e)
        {
            Console.WriteLine("LoadMap ex: " + e.Message);
            Log("LoadMap ex: " + e.Message);
        }
        swLoad.Stop();
        Console.WriteLine("LoadMap result={0} in {1} ms", loadResult, swLoad.ElapsedMilliseconds);
        Log(string.Format("LoadMap result={0} in {1} ms", loadResult, swLoad.ElapsedMilliseconds));

        if (loadResult >= 0)
        {
            if (mapFullLoad)
            {
                try { scene.SetSceneFullLoading(true); Log("SetSceneFullLoading(true) ok"); }
                catch (Exception e) { Log("SetSceneFullLoading ex: " + e.Message); }
            }
            try
            {
                int envr = scene.SetActiveEnvironment();
                Console.WriteLine("SetActiveEnvironment={0}", envr);
                Log("SetActiveEnvironment=" + envr);
            }
            catch (Exception e) { Log("SetActiveEnvironment ex: " + e.Message); }
            try
            {
                int rx = 0, ry = 0, rw = 0, rh = 0;
                int rr = scene.GetSceneRect(ref rx, ref ry, ref rw, ref rh);
                Console.WriteLine("GetSceneRect={0} x={1} y={2} w={3} h={4}", rr, rx, ry, rw, rh);
                Log(string.Format("GetSceneRect={0} x={1} y={2} w={3} h={4}", rr, rx, ry, rw, rh));
            }
            catch (Exception e) { Log("GetSceneRect ex: " + e.Message); }
        }
        else
        {
            Console.WriteLine("FATAL: LoadMap failed");
            Log("FATAL: LoadMap failed");
        }

        // AddOutputWindow AFTER LoadMap: the native scene only exists after
        // the map creates it (editor adds views in OnAfterLoadMap).
        // Flag 0 = SCENE_MAIN (editor's main view type; 2 = OBJECT_PREVEIW).
        long winId = -1;
        try
        {
            winId = scene.AddOutputWindow("", panel.Handle.ToInt64(), 0);
        }
        catch (Exception e)
        {
            Console.WriteLine("AddOutputWindow ex: " + e.Message);
            Log("AddOutputWindow ex: " + e.Message);
        }
        Console.WriteLine("winId={0}", winId);
        Log("winId=" + winId);
        if (winId < 0)
        {
            Console.WriteLine("FATAL: AddOutputWindow failed");
            Log("FATAL: AddOutputWindow failed");
        }

        // Camera setup (env-driven experiments).
        string camPosEnv = Environment.GetEnvironmentVariable("MAP_CAMPOS");
        if (!string.IsNullOrEmpty(camPosEnv))
        {
            try
            {
                string[] p = camPosEnv.Split(',');
                float cx = float.Parse(p[0]), cy = float.Parse(p[1]), cz = float.Parse(p[2]);
                int sr = scene.SetCameraPos(cx, cy, cz, false);
                Console.WriteLine("SetCameraPos({0},{1},{2}) -> {3}", cx, cy, cz, sr);
                Log(string.Format("SetCameraPos({0},{1},{2}) -> {3}", cx, cy, cz, sr));
            }
            catch (Exception e) { Log("SetCameraPos ex: " + e.Message); }
        }
        if (Environment.GetEnvironmentVariable("MAP_RESET") == "1")
        {
            try { scene.ResetCameraPosLookAtUp(); Log("ResetCameraPosLookAtUp ok"); }
            catch (Exception e) { Log("ResetCameraPosLookAtUp ex: " + e.Message); }
        }
        if (Environment.GetEnvironmentVariable("MAP_SKY") == "1")
        {
            try { scene.ResetCameraPosLookAtUpFromSky(); Log("ResetCameraPosLookAtUpFromSky ok"); }
            catch (Exception e) { Log("ResetCameraPosLookAtUpFromSky ex: " + e.Message); }
        }

        // MAP_TOUR: "x,y,z;x,y,z;..." �?visit camera positions, screenshot each.
        float[][] tour = null;
        string tourEnv = Environment.GetEnvironmentVariable("MAP_TOUR");
        if (!string.IsNullOrEmpty(tourEnv))
        {
            try
            {
                string[] parts = tourEnv.Split(';');
                tour = new float[parts.Length][];
                for (int ti = 0; ti < parts.Length; ti++)
                {
                    string[] p = parts[ti].Split(',');
                    tour[ti] = new float[] { float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]) };
                }
                scene.ResetCameraPosLookAtUp();
                Log("tour start: ResetCameraPosLookAtUp");
            }
            catch (Exception e) { Log("tour parse ex: " + e.Message); }
        }
        int tourIdx = 0;
        long tourShotAt = 0;
        int tourShotIdx = -1;

        // MAP_MOVETEST=1: programmatic camera-move verification (editor system).
        bool moveTest = Environment.GetEnvironmentVariable("MAP_MOVETEST") == "1";
        if (moveTest)
        {
            Action<string> cam = delegate(string tag)
            {
                float px = 0f, py = 0f, pz = 0f;
                scene.GetCameraPos(ref px, ref py, ref pz);
                Log(string.Format("movetest {0}: ({1:F0},{2:F0},{3:F0})", tag, px, py, pz));
            };
            Action<int, int> run = delegate(int frames, int sleepMs)
            {
                for (int i = 0; i < frames; i++) { engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(sleepMs); }
            };
            cam("start");
            try
            {
                scene.SetCamareMoveState(CMS_FORWARD, 1); run(90, 16);
                scene.SetCamareMoveState(CMS_FORWARD, 0); run(10, 16);
                cam("after forward");
                scene.SetCamareMoveState(CMS_FAST | CMS_FORWARD, 1); run(60, 16);
                scene.SetCamareMoveState(CMS_FAST | CMS_FORWARD, 0); run(10, 16);
                cam("after fast forward");
                scene.SetCamareMoveState(CMS_UP, 1); run(90, 16);
                scene.SetCamareMoveState(CMS_UP, 0); run(10, 16);
                cam("after up");
                scene.SetCamareMoveState(CMS_DOWN, 1); run(90, 16);
                scene.SetCamareMoveState(CMS_DOWN, 0); run(10, 16);
                cam("after down");
                scene.SetCamareMoveState(CMS_MOVE_LEFT, 1); run(90, 16);
                scene.SetCamareMoveState(CMS_MOVE_LEFT, 0); run(10, 16);
                cam("after left");
                cam("before pan");
                scene.ExecAction(30, 1, 0, makeLParam(640, 360));
                scene.ExecAction(3, 1, 0, makeLParam(640, 360));
                for (int i = 1; i <= 12; i++) { scene.ExecAction(3, 1, 0, makeLParam(640 + i * 8, 360)); run(1, 10); }
                cam("after pan");
                cam("before orbit");
                scene.ExecAction(30, 1, 0, makeLParam(700, 360));
                scene.ExecAction(1, 1, 0, makeLParam(700, 360));
                for (int i = 1; i <= 12; i++) { scene.ExecAction(1, 1, 0, makeLParam(700 + i * 8, 360 + i * 3)); run(1, 10); }
                cam("after orbit");
                cam("before rotateview");
                scene.ExecAction(4, 1, 0, makeLParam(700, 360));
                for (int i = 1; i <= 12; i++) { scene.ExecAction(4, 1, 0, makeLParam(700 + i * 8, 360)); run(1, 10); }
                cam("after rotateview");
                cam("before wheelup");
                scene.ExecAction(31, 1, 1, 1); run(20, 16);
                cam("after wheelup");
                scene.ExecAction(31, 1, 0, 1); run(20, 16);
                cam("after wheeldown");
                scene.ExecAction(25, 1, 0, 0); run(5, 10); cam("after speedup");
                scene.ExecAction(26, 1, 0, 0); run(5, 10); cam("after speeddown");
            }
            catch (Exception e) { Log("movetest ex: " + e.Message); }
            cam("end");
        }

        int[] shots = { 500, 1000, 2000, 4000, 8000, 15000, 25000, 40000, 60000 };
        int ci = 0;
        var runSw = System.Diagnostics.Stopwatch.StartNew();
        var progSw = System.Diagnostics.Stopwatch.StartNew();
        while (!form.IsDisposed)
        {
            while (pending.Count > 0)
            {
                int[] cmd = pending.Dequeue();
                if (cmd[0] == 80)
                {
                    try { scene.SetCamareMoveState(cmd[1], cmd[2]); }
                    catch (Exception e) { Log("SetCamareMoveState ex: " + e.Message); }
                }
                else if (cmd[0] == 81)
                {
                    try { scene.ExecAction(cmd[1], cmd[2], 0, 0); }
                    catch (Exception e) { Log("ExecAction ex: " + e.Message); }
                }
                else if (cmd[0] == 82) { try { scene.ExecAction(1001, 0, 0, 0); } catch { } }
                else if (cmd[0] == 83) { try { scene.ResetCameraPosLookAtUp(); } catch { } }
                else if (cmd[0] == 84) { try { scene.ExecAction(1004, 0, 0, 0); } catch { } }
                else if (cmd[0] == 31)
                {
                    scene.ExecAction(31, 1, cmd[1], 1);
                }
                else
                {
                    scene.ExecAction(cmd[0], cmd[1], 0, makeLParam(cmd[2], cmd[3]));
                }
            }
            sound.FrameMove();
            engine.FrameMove();
            engine.Render();
            Application.DoEvents();
            if (tour != null && tourIdx < tour.Length && tourShotIdx < 0)
            {
                if (runSw.ElapsedMilliseconds >= 2000 + (long)tourIdx * 1500)
                {
                    try
                    {
                        scene.SetCameraPos(tour[tourIdx][0], tour[tourIdx][1], tour[tourIdx][2], false);
                        float tx = 0f, ty = 0f, tz = 0f;
                        scene.GetCameraPos(ref tx, ref ty, ref tz);
                        Log(string.Format("tour[{0}] SetCameraPos({1:F0},{2:F0},{3:F0}) -> ({4:F0},{5:F0},{6:F0})",
                            tourIdx, tour[tourIdx][0], tour[tourIdx][1], tour[tourIdx][2], tx, ty, tz));
                        tourShotIdx = tourIdx;
                        tourShotAt = runSw.ElapsedMilliseconds;
                        tourIdx++;
                    }
                    catch (Exception e) { Log("tour SetCameraPos ex: " + e.Message); }
                }
            }
            else if (tour != null && tourShotIdx >= 0 && runSw.ElapsedMilliseconds - tourShotAt >= 300)
            {
                string png = Path.Combine(outDir, string.Format("map_tour_{0:D2}.png", tourShotIdx));
                try
                {
                    scene.SetScreenShot(png, 2);
                    scene.DoScreenShotImmediate();
                    Log(string.Format("tour shot {0} -> {1} exists={2}", tourShotIdx, png, File.Exists(png)));
                    Console.WriteLine("tour shot {0} -> {1}", tourShotIdx, png);
                }
                catch (Exception e) { Log("tour screenshot ex: " + e.Message); }
                tourShotIdx = -1;
            }
            if (progSw.ElapsedMilliseconds >= 2000)
            {
                progSw.Restart();
                try
                {
                    float p = scene.GetLoadingProgress();
                    float cx2 = 0f, cy2 = 0f, cz2 = 0f;
                    scene.GetCameraPos(ref cx2, ref cy2, ref cz2);
                    Log(string.Format("loading {0:F3} cam=({1:F0},{2:F0},{3:F0}) t={4}ms", p, cx2, cy2, cz2, runSw.ElapsedMilliseconds));
                    if (p < 1.0f) Console.WriteLine("loading {0:F3}", p);
                }
                catch { }
            }
            if (ci < shots.Length && runSw.ElapsedMilliseconds >= shots[ci])
            {
                string png = Path.Combine(outDir, string.Format("map_t{0:D5}ms.png", shots[ci]));
                try
                {
                    scene.SetScreenShot(png, 2);
                    scene.DoScreenShotImmediate();
                    Console.WriteLine("shot t={0}ms -> {1} exists={2}", shots[ci], png, File.Exists(png));
                    Log(string.Format("shot t={0}ms -> {1} exists={2}", shots[ci], png, File.Exists(png)));
                }
                catch (Exception e) { Log("screenshot ex: " + e.Message); }
                ci++;
            }
            if (autoRunMs > 0 && runSw.ElapsedMilliseconds >= autoRunMs) break;
            Thread.Sleep(10);
        }
        Console.WriteLine("DONE");
        Log("done");
    }
}
