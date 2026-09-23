using System;
using System.IO;
using System.Runtime.InteropServices;
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
        bool doCollisionProbe = Environment.GetEnvironmentVariable("MAP_COLLISION_PROBE") == "1";
        bool probeOnly = Environment.GetEnvironmentVariable("MAP_PROBE_ONLY") == "1";
        bool probeDeep = Environment.GetEnvironmentVariable("MAP_PROBE_DEEP") == "1";
        bool camTest = Environment.GetEnvironmentVariable("MAP_CAMTEST") == "1";
        bool playerMode = Environment.GetEnvironmentVariable("MAP_PLAYER") == "1";
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
        bool pW = false, pA = false, pS = false, pD = false, pJump = false;
        bool teleportToStructure = false;
        string shownTitle = "";
        bool firstPersonCam = Environment.GetEnvironmentVariable("MAP_PLAYER_CAM") == "1";
        bool followMode = playerMode && !firstPersonCam;
        bool needReMeasure = false;
        float followDist = 800f;
        long lastMeasureMs = 0;
        // JX3-modeled follow camera (docs/netcode/REBORN_CAMERA_SPEC.md)
        CameraSystem camSys = null;
        double camSens = 0.0035;
        int lastMouseX = -1, lastMouseY = -1;
        double lastMoveYaw = double.NaN;
        if (playerMode && followMode)
        {
            camSys = new CameraSystem();
            string sensEnv = Environment.GetEnvironmentVariable("MAP_CAMERA_SENS");
            if (!string.IsNullOrEmpty(sensEnv)) double.TryParse(sensEnv, out camSens);
            string scaleEnv = Environment.GetEnvironmentVariable("MAP_CAMERA_SCALE");
            if (!string.IsNullOrEmpty(scaleEnv))
            {
                double sc;
                if (double.TryParse(scaleEnv, out sc) && sc > 0) camSys.UnitsPerMeter = sc;
            }
            string camCfg = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "camera.json");
            if (File.Exists(camCfg))
            {
                try { camSys.LoadConfig(camCfg); Log("camera config: " + camCfg); }
                catch (Exception e) { Log("camera config ex: " + e.Message); }
            }
            camSys.SwitchMode(CameraSystem.MODE_CHARACTER);
            camSys.Pitch = camSys.Row.F("InitCameraPitch", -20.0 * CameraSystem.DEG);
            Log(string.Format("CameraSystem ready: mode={0} dist={1:F0}u height={2:F0}u units/m={3}",
                camSys.Mode, camSys.Distance, camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter,
                camSys.UnitsPerMeter));
        }
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
            if (e.Button == MouseButtons.Right)
            {
                lastMouseX = e.X; lastMouseY = e.Y;
            }
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
                if (playerMode && followMode && camSys != null)
                {
                    // JX3 follow camera owns yaw/pitch in follow mode
                    if (lastMouseX >= 0)
                    {
                        camSys.Mouse((e.X - lastMouseX) * camSens, (e.Y - lastMouseY) * camSens);
                    }
                    lastMouseX = e.X; lastMouseY = e.Y;
                    return;
                }
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
            if (playerMode && followMode)
            {
                followDist -= (e.Delta > 0 ? 1f : -1f) * 150f;
                if (followDist < 300f) followDist = 300f;
                if (followDist > 3000f) followDist = 3000f;
                if (camSys != null)
                {
                    // zoom = TargetDistance in meters (script hook set_max_distance)
                    double td = camSys.Rows[CameraSystem.MODE_CHARACTER].F("TargetDistance", 6.0);
                    td -= (e.Delta > 0 ? 1.0 : -1.0) * 0.5;
                    if (td < 2.0) td = 2.0;
                    if (td > 30.0) td = 30.0;
                    camSys.SetMaxDistance(td);
                }
                return;
            }
            pending.Enqueue(new int[] { 31, e.Delta < 0 ? 0 : 1, 0, 1 });
        };
        panel.MouseWheel += wheel;
        form.MouseWheel += wheel;
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (playerMode && e.KeyCode == Keys.F)
            {
                followMode = !followMode;
                needReMeasure = true;
                Log("camera mode -> " + (followMode ? "FOLLOW (third person)" : "FREE"));
                try
                {
                    form.Text = followMode
                        ? "JX3 Map Player - FOLLOW (WASD walk, Space jump, F = free cam, wheel = distance)"
                        : "JX3 Map Player - FREE cam (WASD walk, arrows/QE move cam, F = follow)";
                }
                catch { }
                return;
            }
            if (playerMode && e.KeyCode == Keys.C)
            {
                teleportToStructure = true;
                return;
            }
            if (playerMode && followMode)
            {
                if (e.KeyCode == Keys.W) pW = true;
                else if (e.KeyCode == Keys.S) pS = true;
                else if (e.KeyCode == Keys.A) pA = true;
                else if (e.KeyCode == Keys.D) pD = true;
                else if (e.KeyCode == Keys.Space) pJump = true;
                else if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Shift) shiftDown = true;
                return;
            }
            if (playerMode && !followMode)
            {
                // Free camera: WASD still walks the character (never lose it),
                // arrow keys / Q / E move the camera.
                if (e.KeyCode == Keys.W) pW = true;
                else if (e.KeyCode == Keys.S) pS = true;
                else if (e.KeyCode == Keys.A) pA = true;
                else if (e.KeyCode == Keys.D) pD = true;
                else if (e.KeyCode == Keys.Space) pJump = true;
                else if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Shift) shiftDown = true;
                else if (e.KeyCode == Keys.Up) camKey(Keys.W, 1);
                else if (e.KeyCode == Keys.Down) camKey(Keys.S, 1);
                else if (e.KeyCode == Keys.Left) camKey(Keys.A, 1);
                else if (e.KeyCode == Keys.Right) camKey(Keys.D, 1);
                else if (e.KeyCode == Keys.Q) camKey(Keys.Q, 1);
                else if (e.KeyCode == Keys.E) camKey(Keys.E, 1);
                else if (e.KeyCode == Keys.Add) pending.Enqueue(new int[] { 81, 25, 1, 0 });
                else if (e.KeyCode == Keys.Subtract) pending.Enqueue(new int[] { 81, 26, 1, 0 });
                else if (e.KeyCode == Keys.R) pending.Enqueue(new int[] { 83, 0, 0, 0 });
                return;
            }
            if (playerMode && e.KeyCode == Keys.Space) { pJump = true; return; }
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
            if (playerMode)
            {
                if (e.KeyCode == Keys.W) pW = false;
                else if (e.KeyCode == Keys.S) pS = false;
                else if (e.KeyCode == Keys.A) pA = false;
                else if (e.KeyCode == Keys.D) pD = false;
                else if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Shift) shiftDown = false;
                else if (!followMode && e.KeyCode == Keys.Up) camKey(Keys.W, 0);
                else if (!followMode && e.KeyCode == Keys.Down) camKey(Keys.S, 0);
                else if (!followMode && e.KeyCode == Keys.Left) camKey(Keys.A, 0);
                else if (!followMode && e.KeyCode == Keys.Right) camKey(Keys.D, 0);
                else if (!followMode && e.KeyCode == Keys.Q) camKey(Keys.Q, 0);
                else if (!followMode && e.KeyCode == Keys.E) camKey(Keys.E, 0);
                else if (!followMode && e.KeyCode == Keys.Add) pending.Enqueue(new int[] { 81, 25, 0, 0 });
                else if (!followMode && e.KeyCode == Keys.Subtract) pending.Enqueue(new int[] { 81, 26, 0, 0 });
                return;
            }
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

            // MAP_EXPORT_FBX=path : dump the whole rendered scene (objects +
            // terrain) with world transforms via the engine's own exporter.
            string fbxEnv = Environment.GetEnvironmentVariable("MAP_EXPORT_FBX");
            if (!string.IsNullOrEmpty(fbxEnv))
            {
                try
                {
                    for (int i = 0; i < 30; i++)
                    {
                        engine.FrameMove();
                        engine.Render();
                        Application.DoEvents();
                    }
                    int er = scene.ExportSceneToFbx(1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        "map_export", fbxEnv);
                    Log(string.Format("ExportSceneToFbx(1,1,1) -> {0} exists={1} size={2}",
                        er, File.Exists(fbxEnv), File.Exists(fbxEnv) ? new FileInfo(fbxEnv).Length : 0));
                    int er2 = scene.ExportSceneToFbx(1, 1, 1, 1, 1, 1, 1, 0, 1024, 1024, 0, 0,
                        "map_export", fbxEnv.Replace(".fbx", "_full.fbx"));
                    Log(string.Format("ExportSceneToFbx(all) -> {0} exists={1} size={2}",
                        er2, File.Exists(fbxEnv.Replace(".fbx", "_full.fbx")),
                        File.Exists(fbxEnv.Replace(".fbx", "_full.fbx")) ? new FileInfo(fbxEnv.Replace(".fbx", "_full.fbx")).Length : 0));
                    int er3 = scene.ExportSceneToUsd(fbxEnv.Replace(".fbx", ".usd"));
                    Log(string.Format("ExportSceneToUsd -> {0} exists={1} size={2}",
                        er3, File.Exists(fbxEnv.Replace(".fbx", ".usd")),
                        File.Exists(fbxEnv.Replace(".fbx", ".usd")) ? new FileInfo(fbxEnv.Replace(".fbx", ".usd")).Length : 0));
                    try
                    {
                        string savePath = fbxEnv.Replace(".fbx", "_scene.kms");
                        int sr = scene.SaveToFile(savePath);
                        Log(string.Format("SaveToFile -> {0} exists={1} size={2}",
                            sr, File.Exists(savePath), File.Exists(savePath) ? new FileInfo(savePath).Length : 0));
                    }
                    catch (Exception e2) { Log("SaveToFile ex: " + e2.Message); }
                }
                catch (Exception e) { Log("ExportSceneToFbx ex: " + e.Message); }
            }
        }
        else
        {
            Console.WriteLine("FATAL: LoadMap failed");
            Log("FATAL: LoadMap failed");
        }

        if (doCollisionProbe)
        {
            string physDll = Environment.GetEnvironmentVariable("MAP_PHYS_DLL");
            if (string.IsNullOrEmpty(physDll))
            {
                physDll = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll";
            }
            CollisionProbe.Run(Log, physDll, workingDir, mapPath, probeDeep);
            if (probeOnly)
            {
                Console.WriteLine("PROBE DONE (probe-only mode)");
                Log("PROBE DONE (probe-only mode)");
                return;
            }
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

        if (camTest)
        {
            float ox = 0f, oy = 0f, oz = 0f;
            scene.GetCameraPos(ref ox, ref oy, ref oz);
            Log(string.Format("camtest original=({0:F1},{1:F1},{2:F1})", ox, oy, oz));
            scene.SetCameraPos(ox, 60000f, oz, false);
            float ax = 0f, ay = 0f, az = 0f;
            scene.GetCameraPos(ref ax, ref ay, ref az);
            Log(string.Format("camtest high + false -> ({0:F1},{1:F1},{2:F1})", ax, ay, az));
            scene.SetCameraPos(ax, 60000f, az, true);
            float bx = 0f, by = 0f, bz = 0f;
            scene.GetCameraPos(ref bx, ref by, ref bz);
            Log(string.Format("camtest high + true  -> ({0:F1},{1:F1},{2:F1})", bx, by, bz));
            scene.SetCameraPos(ox, oy + 1000f, oz, true);
            float cx = 0f, cy = 0f, cz = 0f;
            scene.GetCameraPos(ref cx, ref cy, ref cz);
            Log(string.Format("camtest orig+1000 + true -> ({0:F1},{1:F1},{2:F1})", cx, cy, cz));

            // world point (systemCamera0): try below-ground clamp
            scene.SetCameraPos(147463f, -100000f, 49911f, false);
            float dx = 0f, dy = 0f, dz = 0f;
            scene.GetCameraPos(ref dx, ref dy, ref dz);
            Log(string.Format("camtest world low(false) -> ({0:F1},{1:F1},{2:F1})", dx, dy, dz));
            scene.SetCameraPos(147463f, 60000f, 49911f, false);
            scene.GetCameraPos(ref dx, ref dy, ref dz);
            Log(string.Format("camtest world high(false) -> ({0:F1},{1:F1},{2:F1})", dx, dy, dz));

            // map-relative origin
            scene.SetCameraPos(0f, -100000f, -600f, false);
            scene.GetCameraPos(ref dx, ref dy, ref dz);
            Log(string.Format("camtest origin low(false) -> ({0:F1},{1:F1},{2:F1})", dx, dy, dz));

            scene.SetCameraPos(ox, oy, oz, true);
            Console.WriteLine("CAMTEST DONE");
            Log("CAMTEST DONE");
            return;
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

        TerrainSampler sampler = null;
        FoliageCollision foliageCol = null;
        float plX = 0f, plY = 0f, plZ = 0f, pvy = 0f;
        float pdX = 0f, pdY = 0f, pdZ = 1f;
        bool grounded = false, actorOk = false;
        // Real game values from settings/JumpParam.tab + Represent/common/number.krl.txt
        // (docs/JX3_GRAVITY_RESEARCH.md): g = 11 u/tick^2, Vz = 90 u/tick,
        // 15 ticks/s, 1 m = 192 u -> 12.89 m/s^2 / 7.03 m/s, apex 1.92 m.
        // Walk 6 / run 20 尺/s -> 2.0 / 6.67 m/s.
        float pGravity = -1289f, pJumpV = 703f, pSpeed = 200f, pRun = 667f;
        float playerRadius = 25f, playerHeight = 170f;
        string envPR = Environment.GetEnvironmentVariable("MAP_PLAYER_RADIUS");
        if (!string.IsNullOrEmpty(envPR)) float.TryParse(envPR, out playerRadius);
        string envPH = Environment.GetEnvironmentVariable("MAP_PLAYER_HEIGHT");
        if (!string.IsNullOrEmpty(envPH)) float.TryParse(envPH, out playerHeight);
        bool playerDemo = Environment.GetEnvironmentVariable("MAP_PLAYER_DEMO") == "1";
        long lastDemoJump = 0;
        int demoShots = 0;
        string playerModel = Environment.GetEnvironmentVariable("MAP_PLAYER_MODEL");
        if (string.IsNullOrEmpty(playerModel))
            playerModel = @"C:\SeasunGame\MovieEditor\source\" + "\u82B1\u841D\u65E0\u52A8\u4F5C.actor";
        float playerScale = 0.5f;
        string envScale = Environment.GetEnvironmentVariable("MAP_PLAYER_SCALE");
        if (!string.IsNullOrEmpty(envScale)) float.TryParse(envScale, out playerScale);
        string envG = Environment.GetEnvironmentVariable("MAP_PLAYER_GRAVITY");
        if (!string.IsNullOrEmpty(envG)) float.TryParse(envG, out pGravity);
        string envJ = Environment.GetEnvironmentVariable("MAP_PLAYER_JUMP");
        if (!string.IsNullOrEmpty(envJ)) float.TryParse(envJ, out pJumpV);
        string envSp = Environment.GetEnvironmentVariable("MAP_PLAYER_SPEED");
        if (!string.IsNullOrEmpty(envSp)) float.TryParse(envSp, out pSpeed);
        string envRun = Environment.GetEnvironmentVariable("MAP_PLAYER_RUN");
        if (!string.IsNullOrEmpty(envRun)) float.TryParse(envRun, out pRun);
        long lastPlayerMs = 0;
        long lastPlayerLog = 0;
        if (playerMode)
        {
            string physDll = Environment.GetEnvironmentVariable("MAP_PHYS_DLL");
            if (string.IsNullOrEmpty(physDll))
                physDll = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll";
            try { sampler = new TerrainSampler(physDll, mapPath, Log); }
            catch (Exception e) { Log("TerrainSampler ex: " + e.Message); }

            string colEnv = Environment.GetEnvironmentVariable("MAP_FOLIAGE_COLLISION");
            if (colEnv != "0")
            {
                string colPath = colEnv;
                if (string.IsNullOrEmpty(colPath) || colEnv == "1")
                {
                    colPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                        "collision_data", "foliage_collision.bin");
                }
                try
                {
                    string colDir = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                        "collision_data");
                    // per-map files first (<map>_foliage_collision.bin), fall back
                    // to the generic names
                    string mapName = System.IO.Path.GetFileNameWithoutExtension(mapPath);
                    if (!string.IsNullOrEmpty(mapName))
                    {
                        string p = System.IO.Path.Combine(colDir, mapName + "_foliage_collision.bin");
                        if (File.Exists(p)) colPath = p;
                    }
                    string structPath = null;
                    if (Environment.GetEnvironmentVariable("MAP_STRUCTURE_COLLISION") != "0")
                    {
                        string s = !string.IsNullOrEmpty(mapName)
                            ? System.IO.Path.Combine(colDir, mapName + "_structure_collision.bin")
                            : null;
                        if (s == null || !File.Exists(s))
                            s = System.IO.Path.Combine(colDir, "structure_collision.bin");
                        if (File.Exists(s)) structPath = s;
                    }
                    foliageCol = new FoliageCollision(colPath, structPath);
                    Log("FoliageCollision loaded: " + foliageCol.Describe()
                        + " foliage=" + System.IO.Path.GetFileName(colPath)
                        + " structures=" + (structPath == null ? "(none)" : System.IO.Path.GetFileName(structPath)));
                }
                catch (Exception e) { Log("FoliageCollision ex: " + e.Message); }
            }

            string dumpEnv = Environment.GetEnvironmentVariable("MAP_SAMPLE_DUMP");
            if (!string.IsNullOrEmpty(dumpEnv) && sampler != null)
            {
                try
                {
                    string[] dp = dumpEnv.Split(',');
                    float dx0 = float.Parse(dp[0]), dz0 = float.Parse(dp[1]);
                    float dstep = float.Parse(dp[2]);
                    int dnx = int.Parse(dp[3]), dnz = int.Parse(dp[4]);
                    string dpath = dp[5];
                    using (var w = new System.IO.StreamWriter(dpath, false))
                    {
                        w.WriteLine("{0} {1} {2} {3} {4}", dx0, dz0, dstep, dnx, dnz);
                        for (int iz = 0; iz < dnz; iz++)
                        {
                            var sb = new System.Text.StringBuilder();
                            for (int ix = 0; ix < dnx; ix++)
                            {
                                if (ix > 0) sb.Append(' ');
                                sb.Append(sampler.Sample(dx0 + ix * dstep, dz0 + iz * dstep).ToString("F2"));
                            }
                            w.WriteLine(sb.ToString());
                        }
                    }
                    Log("sample dump -> " + dpath);
                }
                catch (Exception e) { Log("sample dump ex: " + e.Message); }
            }

            string ptsEnv = Environment.GetEnvironmentVariable("MAP_SAMPLE_POINTS");
            if (!string.IsNullOrEmpty(ptsEnv) && sampler != null)
            {
                foreach (string pt in ptsEnv.Split(';'))
                {
                    if (string.IsNullOrEmpty(pt)) continue;
                    string[] pp = pt.Split(',');
                    if (pp.Length < 2) continue;
                    float sx, sz;
                    if (!float.TryParse(pp[0], out sx) || !float.TryParse(pp[1], out sz)) continue;
                    Log(string.Format("sample ({0:F1},{1:F1}) -> h={2:F2}", sx, sz, sampler.Sample(sx, sz)));
                }
            }

            string spawn = Environment.GetEnvironmentVariable("MAP_PLAYER_SPAWN");
            bool spawnGiven = !string.IsNullOrEmpty(spawn);
            if (!spawnGiven)
            {
                // Measure the default camera forward direction, then spawn the
                // player in front of the camera so it is visible at start.
                try
                {
                    scene.ResetCameraPosLookAtUp();
                    engine.FrameMove(); engine.Render(); Application.DoEvents();
                    float ax = 0f, ay = 0f, az = 0f;
                    scene.GetCameraPos(ref ax, ref ay, ref az);
                    scene.SetCamareMoveState(1, 1);
                    for (int i = 0; i < 60; i++)
                    {
                        engine.FrameMove(); engine.Render(); Application.DoEvents();
                        Thread.Sleep(16);
                    }
                    scene.SetCamareMoveState(1, 0);
                    float bx = 0f, by = 0f, bz = 0f;
                    scene.GetCameraPos(ref bx, ref by, ref bz);
                    float dx = bx - ax, dz = bz - az;
                    float dl = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (dl > 1f) { pdX = dx / dl; pdZ = dz / dl; pdY = 0f; }
                    plX = bx + pdX * 500f;
                    plZ = bz + pdZ * 500f;
                    Log(string.Format("camera forward=({0:F2},{1:F2}) cam=({2:F0},{3:F0},{4:F0}) spawn=({5:F0},{6:F0})",
                        pdX, pdZ, bx, by, bz, plX, plZ));
                }
                catch (Exception e) { Log("camera dir ex: " + e.Message); plX = 0f; plZ = -600f; }
            }
            else
            {
                try
                {
                    string[] sp = spawn.Split(',');
                    plX = float.Parse(sp[0]); plZ = float.Parse(sp[2]);
                }
                catch { }
            }
            plY = sampler != null ? sampler.Sample(plX, plZ) : 0f;

            try
            {
                var pos = new CLRfloat3(); pos.x = plX; pos.y = plY; pos.z = plZ;
                var rot = new CLRfloat4(); rot.x = 0f; rot.y = 0f; rot.z = 0f; rot.w = 1f;
                var scl = new CLRfloat3(); scl.x = playerScale; scl.y = playerScale; scl.z = playerScale;
                scene.RemoveDummyModel("jumper");
                long r = scene.AddDummyModel("jumper", playerModel, pos, rot, scl);
                actorOk = r > 0;
                Log("player AddDummyModel(\"" + playerModel + "\") -> " + r);
            }
            catch (Exception e) { Log("AddDummyModel ex: " + e.Message); }

            // MAP_DUMMY_MESH=path,scale[,dx,dy,dz] : render a mesh as a dummy
            // model near the spawn for orientation/scale calibration.
            string dummyEnv = Environment.GetEnvironmentVariable("MAP_DUMMY_MESH");
            if (!string.IsNullOrEmpty(dummyEnv))
            {
                try
                {
                    string[] dp = dummyEnv.Split(',');
                    float dscale = dp.Length > 1 ? float.Parse(dp[1]) : 1f;
                    float dox = dp.Length > 2 ? float.Parse(dp[2]) : 600f;
                    float doy = dp.Length > 3 ? float.Parse(dp[3]) : 0f;
                    float doz = dp.Length > 4 ? float.Parse(dp[4]) : 0f;
                    var dpos = new CLRfloat3(); dpos.x = plX + dox; dpos.y = plY + doy; dpos.z = plZ + doz;
                    var drot = new CLRfloat4(); drot.x = 0f; drot.y = 0f; drot.z = 0f; drot.w = 1f;
                    var dscl = new CLRfloat3(); dscl.x = dscale; dscl.y = dscale; dscl.z = dscale;
                    scene.RemoveDummyModel("calib");
                    long dr = scene.AddDummyModel("calib", dp[0], dpos, drot, dscl);
                    Log(string.Format("calib AddDummyModel(\"{0}\", scale={1}, at ({2:F0},{3:F0},{4:F0})) -> {5}",
                        dp[0], dscale, dpos.x, dpos.y, dpos.z, dr));
                }
                catch (Exception e) { Log("dummy mesh ex: " + e.Message); }
            }

            if (firstPersonCam)
            {
                try { scene.SetCameraPos(plX, plY + 90f, plZ, true); } catch { }
            }
            else
            {
                try
                {
                    scene.SetCameraPos(plX - pdX * followDist, plY + 60f, plZ - pdZ * followDist, false);
                    float cx = 0f, cy = 0f, cz = 0f;
                    scene.GetCameraPos(ref cx, ref cy, ref cz);
                    Log(string.Format("follow camera at ({0:F0},{1:F0},{2:F0}) dist={3:F0} dir=({4:F2},{5:F2})",
                        cx, cy, cz, followDist, pdX, pdZ));
                    try { form.Text = "JX3 Map Player - FOLLOW (F = free camera)"; } catch { }
                }
                catch (Exception e) { Log("camera place ex: " + e.Message); }
            }
            Log(string.Format("player start=({0:F0},{1:F0},{2:F0}) gravity={3} jump={4} speed={5} firstPerson={6}",
                plX, plY, plZ, pGravity, pJumpV, pSpeed, firstPersonCam));

            string gridEnv = Environment.GetEnvironmentVariable("MAP_SAMPLE_GRID");
            if (!string.IsNullOrEmpty(gridEnv) && sampler != null)
            {
                try
                {
                    string[] p3 = gridEnv.Split(',');
                    float gx0 = float.Parse(p3[0]), gz0 = float.Parse(p3[2]);
                    float step = 2000f;
                    int n = (p3.Length > 3) ? int.Parse(p3[3]) : 8;
                    float bestH = float.MinValue, bestX = 0f, bestZ = 0f;
                    for (int ix = -n; ix <= n; ix++)
                    {
                        for (int iz = -n; iz <= n; iz++)
                        {
                            float x = gx0 + ix * step, z = gz0 + iz * step;
                            float h = sampler.Sample(x, z);
                            if (h > bestH) { bestH = h; bestX = x; bestZ = z; }
                        }
                    }
                    Log(string.Format("grid scan around ({0:F0},{1:F0}) step={2:F0} n={3}: max height {4:F0} at ({5:F0},{6:F0})",
                        gx0, gz0, step, n, bestH, bestX, bestZ));
                    for (int i = -10; i <= 10; i++)
                    {
                        float z = gz0 + i * 500f;
                        Log(string.Format("  profile z={0:F0} h={1:F0}", z, sampler.Sample(gx0, z)));
                    }

                    string mapEnv = Environment.GetEnvironmentVariable("MAP_SAMPLE_MAP");
                    if (mapEnv == "1")
                    {
                        int half = 40;
                        float step2 = 1000f;
                        float[] hs = new float[(2 * half + 1) * (2 * half + 1)];
                        float hmin = float.MaxValue, hmax = float.MinValue;
                        int k = 0;
                        for (int iz = -half; iz <= half; iz++)
                        {
                            for (int ix = -half; ix <= half; ix++)
                            {
                                float h = sampler.Sample(gx0 + ix * step2, gz0 + iz * step2);
                                hs[k++] = h;
                                if (h < hmin) hmin = h;
                                if (h > hmax) hmax = h;
                            }
                        }
                        Log(string.Format("height map around ({0:F0},{1:F0}) step={2:F0} min={3:F0} max={4:F0}", gx0, gz0, step2, hmin, hmax));
                        string ramp = " .:-=+*#%@";
                        k = 0;
                        for (int iz = -half; iz <= half; iz++)
                        {
                            var sb = new System.Text.StringBuilder();
                            for (int ix = -half; ix <= half; ix++)
                            {
                                float h = hs[k++];
                                float t = (hmax > hmin) ? (h - hmin) / (hmax - hmin) : 0f;
                                int ci2 = (int)(t * (ramp.Length - 1));
                                sb.Append(ramp[ci2]);
                            }
                            Log("  " + sb.ToString());
                        }
                    }
                }
                catch (Exception e) { Log("grid scan ex: " + e.Message); }
            }
        }

        // Measures the camera view direction (x,z) by nudging the camera forward
        // a few frames, so follow mode can place the camera behind the character
        // even after the user rotated the view.
        Action measureView = delegate
        {
            try
            {
                float ax = 0f, ay = 0f, az = 0f;
                scene.GetCameraPos(ref ax, ref ay, ref az);
                scene.SetCamareMoveState(1, 1);
                for (int i = 0; i < 3; i++)
                {
                    engine.FrameMove();
                    engine.Render();
                    Application.DoEvents();
                }
                scene.SetCamareMoveState(1, 0);
                float bx = 0f, by = 0f, bz = 0f;
                scene.GetCameraPos(ref bx, ref by, ref bz);
                float dx = bx - ax, dz = bz - az;
                float dl = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dl > 0.5f) { pdX = dx / dl; pdZ = dz / dl; pdY = 0f; }
            }
            catch { }
        };

        if (playerMode && !firstPersonCam &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAP_PLAYER_SPAWN")))
        {
            measureView();
            Log(string.Format("spawn camera dir=({0:F2},{1:F2})", pdX, pdZ));
        }
        if (camSys != null && (Math.Abs(pdX) > 1e-4f || Math.Abs(pdZ) > 1e-4f))
        {
            camSys.Yaw = Math.Atan2(-pdZ, -pdX);
            Log(string.Format("camera yaw init={0:F3} (view dir {1:F2},{2:F2})", camSys.Yaw, pdX, pdZ));
        }

        if (playerMode && Environment.GetEnvironmentVariable("MAP_ORBIT_TEST") == "1")
        {
            try
            {
                measureView();
                Log(string.Format("orbit-test dir before=({0:F2},{1:F2})", pdX, pdZ));
                scene.ExecAction(30, 1, 0, makeLParam(700, 360));
                scene.ExecAction(1, 1, 0, makeLParam(700, 360));
                for (int i = 1; i <= 24; i++)
                {
                    scene.ExecAction(1, 1, 0, makeLParam(700 + i * 6, 360));
                    engine.FrameMove();
                    engine.Render();
                    Application.DoEvents();
                }
                measureView();
                Log(string.Format("orbit-test dir after=({0:F2},{1:F2})", pdX, pdZ));
            }
            catch (Exception e) { Log("orbit-test ex: " + e.Message); }
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
            if (playerMode && sampler != null)
            {
                long nowMs = runSw.ElapsedMilliseconds;
                float dt = (nowMs - lastPlayerMs) / 1000f;
                lastPlayerMs = nowMs;
                if (dt < 0f) dt = 0f;
                if (dt > 0.05f) dt = 0.05f;

                if (followMode && camSys != null)
                {
                    // movement is camera-relative: forward = camera -> anchor
                    double cfx, cfz;
                    camSys.Forward(out cfx, out cfz);
                    pdX = (float)cfx; pdZ = (float)cfz;
                }
                float hl = (float)Math.Sqrt(pdX * pdX + pdZ * pdZ);
                float hx = hl > 1e-4f ? pdX / hl : 0f;
                float hz = hl > 1e-4f ? pdZ / hl : 1f;
                float dirX = 0f, dirZ = 0f;
                float rX = hz, rZ = -hx;
                if (pW) { dirX += hx; dirZ += hz; }
                if (pS) { dirX -= hx; dirZ -= hz; }
                if (pA) { dirX -= rX; dirZ -= rZ; }
                if (pD) { dirX += rX; dirZ += rZ; }
                float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
                if (playerDemo && grounded && (nowMs % 2000) < 1400)
                {
                    dirX += hx;
                    dirZ += hz;
                    len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
                }
                if (teleportToStructure && foliageCol != null)
                {
                    teleportToStructure = false;
                    float nx, ny, nz;
                    float d = foliageCol.NearestInstance(plX, plZ, out nx, out ny, out nz);
                    if (d < float.MaxValue)
                    {
                        float ddx = plX - nx, ddz = plZ - nz;
                        float dl = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
                        if (dl < 1f) { ddx = 1f; ddz = 0f; dl = 1f; }
                        plX = nx + ddx / dl * 320f;
                        plZ = nz + ddz / dl * 320f;
                        plY = sampler != null ? sampler.Sample(plX, plZ) : plY;
                        pvy = 0f;
                        // switch to follow camera right behind the character so the
                        // obstacle is immediately visible
                        followMode = true;
                        needReMeasure = true;
                        float fdx = -ddx / dl, fdz = -ddz / dl;
                        pdX = fdx; pdZ = fdz;
                        try
                        {
                            scene.SetCameraPos(plX - fdx * followDist, plY + 60f, plZ - fdz * followDist, false);
                        }
                        catch { }
                        Log(string.Format("teleport to structure: was {0:F0}u away, now at ({1:F0},{2:F0},{3:F0})",
                            d, plX, plY, plZ));
                    }
                    else Log("no solid structure found");
                }

                bool blocked = false;
                float mx = 0f, mz = 0f;
                float ground = sampler.Sample(plX, plZ);
                if (len > 0f)
                {
                    float sp = (shiftDown ? pRun : pSpeed) / len;
                    float step = sp * dt;
                    float ux = dirX / len, uz = dirZ / len;
                    mx = ux * step; mz = uz * step;
                    float look = Math.Max(step, 40f);
                    float lx = ux * look, lz = uz * look;
                    const float maxRise = 70f;
                    float gAhead = sampler.Sample(plX + lx, plZ + lz);
                    if (gAhead <= plY + maxRise)
                    {
                        plX += mx; plZ += mz;
                        ground = sampler.Sample(plX, plZ);
                    }
                    else
                    {
                        blocked = true;
                        float gX = sampler.Sample(plX + lx, plZ);
                        if (gX <= plY + maxRise)
                        {
                            plX += mx;
                            ground = sampler.Sample(plX, plZ);
                        }
                        else
                        {
                            float gZ = sampler.Sample(plX, plZ + lz);
                            if (gZ <= plY + maxRise)
                            {
                                plZ += mz;
                                ground = sampler.Sample(plX, plZ);
                            }
                        }
                    }
                }
                // real foliage structure collision (rocks / cactus / deadwood)
                if (foliageCol != null)
                {
                    // step-up probe: walk onto / stand on low rock tops
                    float stepGround = foliageCol.SupportHeight(plX, plZ, plY - 20f, plY + 70f);
                    if (len > 0f)
                    {
                        float ux2 = dirX / len, uz2 = dirZ / len;
                        for (int si = 1; si <= 3; si++)
                        {
                            float sd = playerRadius + si * 25f;
                            float sh2 = foliageCol.SupportHeight(plX + ux2 * sd, plZ + uz2 * sd,
                                plY - 20f, plY + 70f);
                            if (sh2 > stepGround) stepGround = sh2;
                        }
                    }
                    if (stepGround > ground)
                    {
                        ground = stepGround;
                    }
                    else
                    {
                        bool sBlocked = foliageCol.Resolve(ref plX, ref plY, ref plZ,
                            playerRadius, playerHeight, ref ground, ref grounded);
                        if (sBlocked) blocked = true;
                        if (grounded)
                        {
                            float sh = foliageCol.SupportHeight(plX, plZ, plY - 150f, plY + 60f);
                            if (sh > ground) ground = sh;
                        }
                    }
                }
                if (grounded)
                {
                    if (plY - ground > 150f) grounded = false;   // walked off a ledge
                    else if (plY > ground) plY = ground;          // stick to slopes
                    else if (ground - plY <= 70f) plY = ground;   // step up (terrain/rock)
                }
                // visible collision feedback in the window title
                string baseTitle = followMode
                    ? "JX3 Map Player - FOLLOW (WASD walk, Space jump, F = free cam, C = teleport to structure)"
                    : "JX3 Map Player - FREE cam (WASD walk, arrows/QE cam, F = follow, C = teleport to structure)";
                string wantTitle = blocked ? "BLOCKED by structure  |  " + baseTitle : baseTitle;
                if (wantTitle != shownTitle)
                {
                    shownTitle = wantTitle;
                    try { form.Text = wantTitle; } catch { }
                }
                if (playerDemo && grounded && nowMs - lastDemoJump > 2000)
                {
                    pvy = pJumpV;
                    grounded = false;
                    lastDemoJump = nowMs;
                }
                if (pJump && grounded) { pvy = pJumpV; grounded = false; }
                pJump = false;
                pvy += pGravity * dt;
                plY += pvy * dt;
                if (plY <= ground)
                {
                    plY = ground;
                    if (pvy < 0f) pvy = 0f;
                    grounded = true;
                }
                else if (plY > ground + 1f)
                {
                    grounded = false;
                }

                if (followMode && camSys == null)
                {
                    if (needReMeasure || nowMs - lastMeasureMs >= 500)
                    {
                        needReMeasure = false;
                        lastMeasureMs = nowMs;
                        measureView();
                    }
                }
                if (firstPersonCam)
                {
                    try { scene.SetCameraPos(plX, plY + 90f, plZ, true); } catch { }
                }
                else if (followMode && camSys != null)
                {
                    try
                    {
                        // JX3-modeled follow camera (REBORN_CAMERA_SPEC.md):
                        // anchor + rotated offset, SmoothTime exponential
                        // smoothing, move-pitch, yaw-follow, sprint pull-back,
                        // obstruction pull-in (terrain ray-march).
                        bool movingNow = len > 0f;
                        if (movingNow && shiftDown)
                        {
                            if (camSys.Mode != CameraSystem.MODE_SPRINT)
                                camSys.SwitchMode(CameraSystem.MODE_SPRINT, false);
                            camSys.SprintSpeed = (pRun / 192.0);
                        }
                        else if (camSys.Mode != CameraSystem.MODE_CHARACTER)
                        {
                            camSys.SwitchMode(CameraSystem.MODE_CHARACTER, false);
                        }
                        double turn = 0;
                        if (movingNow)
                        {
                            double mvYaw = Math.Atan2(dirZ, dirX);
                            if (!double.IsNaN(lastMoveYaw))
                            {
                                double d = mvYaw - lastMoveYaw;
                                while (d > Math.PI) d -= 2 * Math.PI;
                                while (d < -Math.PI) d += 2 * Math.PI;
                                turn = d;
                            }
                            lastMoveYaw = mvYaw;
                        }
                        else lastMoveYaw = double.NaN;

                        double[] anchor = { plX, plY, plZ };
                        Func<double[], double[], double?> obst = delegate(double[] a, double[] p)
                        {
                            // ray from the character's chest toward the camera;
                            // terrain above the ray pulls the camera in
                            const double eyeUp = 80.0, margin = 20.0;
                            double ddx = p[0] - a[0], ddy = p[1] - a[1], ddz = p[2] - a[2];
                            double dist = Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                            const int steps = 14;
                            for (int i = 2; i <= steps; i++)
                            {
                                double t = (double)i / steps;
                                float g = sampler.Sample((float)(a[0] + ddx * t), (float)(a[2] + ddz * t));
                                if (g + margin > a[1] + eyeUp + ddy * t) return t * dist;
                            }
                            return null;
                        };
                        camSys.Update(dt, anchor, movingNow, turn, 0, obst);

                        float camX = (float)camSys.Pos[0], camY = (float)camSys.Pos[1], camZ = (float)camSys.Pos[2];
                        float camGround = sampler.Sample(camX, camZ) + 30f;
                        if (camY < camGround) camY = camGround;
                        scene.SetCameraPos(camX, camY, camZ, false);
                    }
                    catch (Exception e) { Log("camera system ex: " + e.Message); }
                }
                else if (followMode)
                {
                    try
                    {
                        // fallback third-person camera (no camera system)
                        float baseY = plY + 50f;
                        float useDist = followDist;
                        for (int i = 1; i <= 16; i++)
                        {
                            float t = i / 16f;
                            float sx = plX - hx * followDist * t;
                            float sz = plZ - hz * followDist * t;
                            float g = sampler.Sample(sx, sz);
                            if (g + 10f > baseY)
                            {
                                useDist = followDist * ((i - 1) / 16f);
                                break;
                            }
                        }
                        if (useDist < 80f) useDist = 80f;
                        float camX = plX - hx * useDist;
                        float camZ = plZ - hz * useDist;
                        float camY = baseY;
                        float camGround = sampler.Sample(camX, camZ) + 30f;
                        if (camY < camGround) camY = camGround;
                        if (camY > plY + 120f) camY = plY + 120f;
                        scene.SetCameraPos(camX, camY, camZ, false);
                    }
                    catch { }
                }
                if (actorOk)
                {
                    try
                    {
                        var pos = new CLRfloat3(); pos.x = plX; pos.y = plY; pos.z = plZ;
                        var rot = new CLRfloat4(); rot.x = 0f; rot.y = 0f; rot.z = 0f; rot.w = 1f;
                        var scl = new CLRfloat3(); scl.x = playerScale; scl.y = playerScale; scl.z = playerScale;
                        scene.RemoveDummyModel("jumper");
                        scene.AddDummyModel("jumper", playerModel, pos, rot, scl);
                    }
                    catch (Exception e) { Log("jumper update ex: " + e.Message); actorOk = false; }
                }
                if (nowMs - lastPlayerLog >= 250)
                {
                    lastPlayerLog = nowMs;
                    float dbgx = 0f, dbgy = 0f, dbgz = 0f;
                    try { scene.GetCameraPos(ref dbgx, ref dbgy, ref dbgz); } catch { }
                    float ddx = dbgx - plX, ddz = dbgz - plZ;
                    Log(string.Format("player pos=({0:F0},{1:F0},{2:F0}) ground={3:F0} vy={4:F0} grounded={5} blocked={6} cam=({7:F0},{8:F0},{9:F0}) camDistXZ={10:F0}",
                        plX, plY, plZ, ground, pvy, grounded, blocked, dbgx, dbgy, dbgz, (float)Math.Sqrt(ddx * ddx + ddz * ddz)));
                }
                if (playerDemo && demoShots < 6 && !grounded && pvy < 0f &&
                    nowMs - lastDemoJump >= 250 && nowMs - lastDemoJump <= 600)
                {
                    demoShots++;
                    string png = Path.Combine(outDir, string.Format("jump_{0:D2}.png", demoShots));
                    try
                    {
                        scene.SetScreenShot(png, 2);
                        scene.DoScreenShotImmediate();
                        Log(string.Format("jump shot {0} y={1:F0} vy={2:F0} -> {3}", demoShots, plY, pvy, png));
                    }
                    catch (Exception e) { Log("jump shot ex: " + e.Message); }
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

// Collision probe: hosts the real game physics module (PhysicsEngineX64.dll),
// initialises its PhysicsManager through the manager vtable, and drives the
// terrain loader pipeline. All offsets are RVAs derived from binary recon
// (see engine_host_spike/recon_physics*.txt).
internal static class CollisionProbe
{
    const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

    // Preferred image base of PhysicsEngineX64.dll minus itself: RVAs below.
    const int RVA_MANAGER_SINGLETON = 0x11B5D0;
    const int RVA_ALLOC_PTR = 0x11F450;
    const int RVA_MEM_MGR = 0x11F460;
    const int RVA_FS_PTR = 0x11F4F8;

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr LoadLibraryExA(string path, IntPtr hFile, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int GetManagerFn(out IntPtr mgr);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int TwoArgFn(IntPtr self, IntPtr a, IntPtr b);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int PathFn(IntPtr self, IntPtr path);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int OutFn(IntPtr self, IntPtr cfg, out IntPtr outPtr);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateTerrainFn(IntPtr self, out IntPtr outPtr, IntPtr cfg);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateSceneFn(IntPtr self, out IntPtr scene, IntPtr arg);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateDataLoaderFn(IntPtr path, IntPtr opt, out IntPtr loader);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void DescFn(IntPtr self, IntPtr out32);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int BoolArgFn(IntPtr self, IntPtr arg);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int FourArgFn(IntPtr self, IntPtr a, IntPtr b, IntPtr c);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int LoadRegionFn(IntPtr self, int nX, int nZ, IntPtr pData, int nCount,
                              IntPtr outA, IntPtr outB, IntPtr outC);

    static Action<string> _log;

    static void Log(string s)
    {
        Console.WriteLine(s);
        if (_log != null) { try { _log(s); } catch { } }
    }

    static T Fn<T>(IntPtr p) where T : class
    {
        return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
    }

    static IntPtr Vt(IntPtr obj, int index)
    {
        IntPtr vt = Marshal.ReadIntPtr(obj);
        return Marshal.ReadIntPtr(vt, index * IntPtr.Size);
    }

    static string Hex(IntPtr p) { return "0x" + p.ToInt64().ToString("X"); }

    static float F(IntPtr p, int off) { return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(p, off)), 0); }

    static void DumpFields(string tag, IntPtr obj, int[] offsets)
    {
        if (obj == IntPtr.Zero) { Log("  " + tag + ": null"); return; }
        foreach (int off in offsets)
        {
            IntPtr q = Marshal.ReadIntPtr(obj, off);
            int i = Marshal.ReadInt32(obj, off);
            float f = F(obj, off);
            Log(string.Format("  {0}+0x{1:X2}: ptr={2} int={3} float={4:F3}", tag, off, Hex(q), i, f));
        }
    }

    static bool IsReadable(IntPtr p, int size)
    {
        MEMORY_BASIC_INFORMATION mbi;
        if (VirtualQuery(p, out mbi, new IntPtr(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)))) == IntPtr.Zero)
            return false;
        if (mbi.State != 0x1000) return false;
        if ((mbi.Protect & 0x01) != 0 || (mbi.Protect & 0x100) != 0) return false;
        long end = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
        return p.ToInt64() + size <= end;
    }

    static void ScanProcessForPhysicsObjects(IntPtr h, Action<string> log)
    {
        long hBase = h.ToInt64();
        long hEnd = hBase + 0x200000;
        var known = new System.Collections.Generic.Dictionary<long, string>();
        known[0xFA6B0] = "PhysicsManager";
        known[0xFA7B8] = "PhysicsScene";
        known[0xFCFE0] = "PhysicsTerrain";
        known[0xFCCA8] = "StaticPhysicsSceneManager";
        known[0xFD198] = "TerrainRegionMgr";
        known[0xFCAC0] = "SceneRegionManager";
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        long addr = 0x10000;
        long limit = 0x7FFFFFFFFFFF;
        long scanned = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        MEMORY_BASIC_INFORMATION mbi;
        while (addr < limit && sw.ElapsedMilliseconds < 120000 && scanned < 3L * 1024 * 1024 * 1024)
        {
            if (VirtualQuery(new IntPtr(addr), out mbi, new IntPtr(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)))) == IntPtr.Zero)
                break;
            long regionSize = mbi.RegionSize.ToInt64();
            if (regionSize <= 0) break;
            uint prot = mbi.Protect;
            bool readable = mbi.State == 0x1000 && (prot & 0x01) == 0 && (prot & 0x100) == 0;
            bool priv = mbi.Type == 0x20000;
            if (readable && priv && regionSize <= 16L * 1024 * 1024)
            {
                long remaining = regionSize;
                long cur = addr;
                while (remaining > 0 && sw.ElapsedMilliseconds < 120000 && scanned < 3L * 1024 * 1024 * 1024)
                {
                    int chunk = (int)Math.Min(remaining, 4L * 1024 * 1024);
                    byte[] buf;
                    try { buf = new byte[chunk]; Marshal.Copy(new IntPtr(cur), buf, 0, chunk); }
                    catch { break; }
                    scanned += chunk;
                    for (int off = 0; off + 8 <= chunk; off += 8)
                    {
                        long v = BitConverter.ToInt64(buf, off);
                        if (v <= hBase || v >= hEnd) continue;
                        if (!IsReadable(new IntPtr(v), 8)) continue;
                        long q;
                        try { q = Marshal.ReadIntPtr(new IntPtr(v)).ToInt64(); } catch { continue; }
                        long rva = q - hBase;
                        string name;
                        if (known.TryGetValue(rva, out name))
                        {
                            int c;
                            counts.TryGetValue(name, out c);
                            if (c < 5)
                                log(string.Format("  scan: {0} obj={1:X} slot={2:X} vtableRva=0x{3:X}", name, v, cur + off, rva));
                            counts[name] = c + 1;
                        }
                    }
                    cur += chunk;
                    remaining -= chunk;
                }
            }
            addr += regionSize;
        }
        log("scan done: " + sw.ElapsedMilliseconds + "ms scanned=" + (scanned / (1024 * 1024)) + "MB");
        foreach (var kv in counts) log("  scan count " + kv.Key + " = " + kv.Value);
    }

    public static void Run(Action<string> log, string physDll, string clientRoot, string mapPath, bool deep)
    {
        _log = log;
        Log("=== collision probe start ===");
        Log("physDll=" + physDll);
        Log("clientRoot=" + clientRoot);
        Log("mapPath=" + mapPath);

        IntPtr already = GetModuleHandleA("PhysicsEngineX64.dll");
        Log("PhysicsEngineX64 already loaded: " + Hex(already));
        Log("PhysicsX64 (editor) loaded: " + Hex(GetModuleHandleA("PhysicsX64.dll")));
        Log("Semanticx64 loaded: " + Hex(GetModuleHandleA("Semanticx64.dll")));
        Log("Engine_Lua5X64 loaded: " + Hex(GetModuleHandleA("Engine_Lua5X64.dll")));

        IntPtr h = already != IntPtr.Zero ? already : LoadLibraryExA(physDll, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (h == IntPtr.Zero)
        {
            Log("LoadLibraryEx FAILED err=" + Marshal.GetLastWin32Error());
            return;
        }
        Log("module base=" + Hex(h));

        if (Environment.GetEnvironmentVariable("MAP_PROBE_SCAN") == "1")
        {
            Log("scanning process for engine physics objects ...");
            ScanProcessForPhysicsObjects(h, Log);
        }

        IntPtr allocPtr = Marshal.ReadIntPtr(h, RVA_ALLOC_PTR);
        IntPtr memMgr = Marshal.ReadIntPtr(h, RVA_MEM_MGR);
        IntPtr fsPtr = Marshal.ReadIntPtr(h, RVA_FS_PTR);
        IntPtr singleton = h + RVA_MANAGER_SINGLETON;
        Log("pre-init: allocPtr=" + Hex(allocPtr) + " memMgr=" + Hex(memMgr) + " fs=" + Hex(fsPtr));
        Log("pre-init: manager singleton obj=" + Hex(singleton) + " field0=" + Hex(Marshal.ReadIntPtr(singleton)));

        IntPtr getMgrPtr = GetProcAddress(h, "GetPhysicsManager");
        if (getMgrPtr == IntPtr.Zero) { Log("GetPhysicsManager not found"); return; }
        var getMgr = Fn<GetManagerFn>(getMgrPtr);
        IntPtr mgr = IntPtr.Zero;
        int hr = getMgr(out mgr);
        Log("GetPhysicsManager hr=" + hr + " mgr=" + Hex(mgr));
        if (mgr == IntPtr.Zero) return;
        Log("mgr vtable=" + Hex(Marshal.ReadIntPtr(mgr)) + " (rva 0x" + (Marshal.ReadIntPtr(mgr).ToInt64() - h.ToInt64()).ToString("X") + ")");
        for (int i = 0; i < 20; i++)
        {
            IntPtr fn = Vt(mgr, i);
            Log(string.Format("  mgr vt[{0}] rva=0x{1:X}", i, (fn.ToInt64() - h.ToInt64())));
        }
        DumpFields("mgr", mgr, new int[] { 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x60 });

        // 1) SetWorkingDir(clientRoot) - creates the Semantic-backed file system.
        IntPtr path = Marshal.StringToHGlobalAnsi(clientRoot);
        try
        {
            var setWd = Fn<PathFn>(Vt(mgr, 2));
            hr = setWd(mgr, path);
            Log("SetWorkingDir hr=" + hr + " fs=" + Hex(Marshal.ReadIntPtr(h, RVA_FS_PTR)));
        }
        finally { Marshal.FreeHGlobal(path); }

        // 2) Init(rdx=0, r8=0) - only if the engine host has not already done it.
        IntPtr foundation = Marshal.ReadIntPtr(mgr, 0x10);
        Log("mgr m_pPhysXFoundation=" + Hex(foundation));
        if (foundation == IntPtr.Zero)
        {
            var init = Fn<TwoArgFn>(Vt(mgr, 0));
            hr = init(mgr, IntPtr.Zero, IntPtr.Zero);
            Log("Init hr=" + hr + " allocPtr=" + Hex(Marshal.ReadIntPtr(h, RVA_ALLOC_PTR)) + " memMgr=" + Hex(Marshal.ReadIntPtr(h, RVA_MEM_MGR)));
        }
        else
        {
            hr = 0;
            Log("Init SKIPPED: manager already initialised by engine host");
        }
        DumpFields("mgr", mgr, new int[] { 0x10, 0x30, 0x38, 0x40, 0x48, 0x50, 0x60 });

        // Scan manager fields for objects created by this module (vtable RVAs).
        long[] knownVt = new long[] { 0xFA6B0, 0xFA7B8, 0xFCFE0, 0xFD2B0, 0xFBC98 };
        long hBase = h.ToInt64(), hEnd = hBase + 0x200000;
        for (int off = 0; off < 0x300; off += 8)
        {
            long p = Marshal.ReadIntPtr(mgr, off).ToInt64();
            if (p <= hBase || p >= hEnd) continue;
            long vq = Marshal.ReadIntPtr(new IntPtr(p)).ToInt64();
            long rva = vq - hBase;
            Log(string.Format("  mgr+0x{0:X2} -> object {1:X} vtable rva=0x{2:X}{3}", off, p, rva,
                Array.IndexOf(knownVt, rva) >= 0 ? "  <== known vtable" : ""));
        }

        // 3) CreatePhysicsScene(out scene, arg) - optional: the engine host already
        //    owns a scene; the manager init requires a real engine-side argument.
        if (Environment.GetEnvironmentVariable("MAP_PROBE_SCENE") == "1")
        {
            IntPtr scene = IntPtr.Zero;
            var createScene = Fn<CreateSceneFn>(Vt(mgr, 16));
            hr = createScene(mgr, out scene, IntPtr.Zero);
            Log("CreatePhysicsScene hr=" + hr + " scene=" + Hex(scene));
            if (scene != IntPtr.Zero)
            {
                Log("scene vtable rva=0x" + (Marshal.ReadIntPtr(scene).ToInt64() - h.ToInt64()).ToString("X"));
                DumpFields("scene", scene, new int[] { 0x8, 0x10, 0x18, 0x20, 0x28 });
            }
        }

        // 4) CreatePhysXTerrain(config, out terrain).
        IntPtr cfg = Marshal.AllocHGlobal(16);
        Marshal.WriteInt32(cfg, 0, 2);   // nPreLoadSize
        Marshal.WriteInt32(cfg, 4, 1);   // nForceLoadSize
        Marshal.WriteInt32(cfg, 8, 0);   // nUpdateDelta
        Marshal.WriteInt32(cfg, 12, 2);  // nMaxCacheCount
        IntPtr terrain = IntPtr.Zero;
        try
        {
            // manager vt[14] = CreatePhysXTerrain(PhysicsTerrain** ppOut, Config* pConfig)
            var createTerrain = Fn<CreateTerrainFn>(Vt(mgr, 14));
            hr = createTerrain(mgr, out terrain, cfg);
            Log("CreatePhysXTerrain hr=" + hr + " terrain=" + Hex(terrain));
        }
        finally { Marshal.FreeHGlobal(cfg); }
        if (terrain != IntPtr.Zero)
        {
            Log("terrain vtable rva=0x" + (Marshal.ReadIntPtr(terrain).ToInt64() - h.ToInt64()).ToString("X"));
            DumpFields("terrain", terrain, new int[] { 0x10, 0x20, 0x24, 0x28, 0x30, 0x34, 0x40, 0x48, 0x50, 0x58, 0x60 });
        }

        // 5) CreatePhysicsTerrainDataLoader(jsonmap, 0, out loader).
        IntPtr loader = IntPtr.Zero;
        var createLoaderPtr = GetProcAddress(h, "CreatePhysicsTerrainDataLoader");
        if (createLoaderPtr == IntPtr.Zero) { Log("CreatePhysicsTerrainDataLoader not found"); return; }
        IntPtr mapPathAnsi = Marshal.StringToHGlobalAnsi(mapPath);
        try
        {
            var createLoader = Fn<CreateDataLoaderFn>(createLoaderPtr);
            int ok = createLoader(mapPathAnsi, IntPtr.Zero, out loader);
            Log("CreatePhysicsTerrainDataLoader ok=" + ok + " loader=" + Hex(loader));
        }
        finally { Marshal.FreeHGlobal(mapPathAnsi); }
        if (loader == IntPtr.Zero) { Log("loader creation failed; stopping probe"); return; }
        Log("loader vtable rva=0x" + (Marshal.ReadIntPtr(loader).ToInt64() - h.ToInt64()).ToString("X"));
        DumpFields("loader", loader, new int[] { 0x10, 0x14, 0x18, 0x1c, 0x20, 0x24, 0x28, 0x2c, 0x30, 0x34 });

        // 5b) CreatePhysicsSceneDynamicLoader(out dynLoader, config) - manager vt[15].
        IntPtr dynLoader = IntPtr.Zero;
        IntPtr cfg2 = Marshal.AllocHGlobal(16);
        Marshal.WriteInt32(cfg2, 0, 2);
        Marshal.WriteInt32(cfg2, 4, 1);
        Marshal.WriteInt32(cfg2, 8, 0);
        Marshal.WriteInt32(cfg2, 12, 2);
        try
        {
            var createDyn = Fn<CreateTerrainFn>(Vt(mgr, 15));
            hr = createDyn(mgr, out dynLoader, cfg2);
            Log("CreatePhysicsSceneDynamicLoader hr=" + hr + " dynLoader=" + Hex(dynLoader));
        }
        finally { Marshal.FreeHGlobal(cfg2); }
        if (dynLoader != IntPtr.Zero)
        {
            Log("dynLoader vtable rva=0x" + (Marshal.ReadIntPtr(dynLoader).ToInt64() - h.ToInt64()).ToString("X"));
            for (int i = 0; i < 16; i++)
            {
                Log(string.Format("  dyn vt[{0}] rva=0x{1:X}", i, (Vt(dynLoader, i).ToInt64() - h.ToInt64())));
            }
            DumpFields("dyn", dynLoader, new int[] { 0x8, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x58, 0x60, 0x68, 0x78, 0x88, 0x98 });
        }

        // 6) loader vt[2] = GetTerrainDesc(out 32 bytes).
        IntPtr desc32 = Marshal.AllocHGlobal(64);
        for (int i = 0; i < 64; i += 4) Marshal.WriteInt32(desc32, i, 0);
        var getDesc = Fn<DescFn>(Vt(loader, 2));
        getDesc(loader, desc32);
        for (int i = 0; i < 32; i += 4)
        {
            Log(string.Format("  desc32+0x{0:X2}: int={1} float={2:F4}", i, Marshal.ReadInt32(desc32, i), F(desc32, i)));
        }

        if (!deep)
        {
            Log("(shallow probe; set MAP_PROBE_DEEP=1 for LoadTerrain)");
            Log("=== collision probe end ===");
            return;
        }

        // 6b) loader vt[3] = LoadRegion(nX, nZ, float* pHeights, count, outA, outB, outC):
        //     fills (nRegionSize+1)^2 raw float heights straight from the game data.
        {
            int regionSize = Marshal.ReadInt32(desc32, 0);          // 512
            int nRegionX = Marshal.ReadInt32(desc32, 4);            // 8
            int nRegionZ = Marshal.ReadInt32(desc32, 8);            // 8
            float spanX = F(desc32, 0x10);                          // 100 per grid cell
            float originX = F(desc32, 0x18);                        // -102400
            float originZ = F(desc32, 0x1C);
            float pxr = 147463.5f, pzr = 49911.7f;
            string posEnv2 = Environment.GetEnvironmentVariable("MAP_PROBE_POS");
            if (!string.IsNullOrEmpty(posEnv2))
            {
                string[] pp2 = posEnv2.Split(',');
                pxr = float.Parse(pp2[0]); pzr = float.Parse(pp2[2]);
            }
            float regionWorld = regionSize * spanX;                 // 51200
            int ix = (int)Math.Floor((pxr - originX) / regionWorld);
            int iz = (int)Math.Floor((pzr - originZ) / regionWorld);
            if (ix < 0) ix = 0; if (ix >= nRegionX) ix = nRegionX - 1;
            if (iz < 0) iz = 0; if (iz >= nRegionZ) iz = nRegionZ - 1;
            int count = (regionSize + 1) * (regionSize + 1);
            Log(string.Format("LoadRegion: region=({0},{1}) size={2} count={3} buf={4} bytes", ix, iz, regionSize, count, count * 4));
            IntPtr buf = Marshal.AllocHGlobal(count * 4);
            IntPtr oa = Marshal.AllocHGlobal(64), ob = Marshal.AllocHGlobal(64), oc = Marshal.AllocHGlobal(64);
            Marshal.WriteInt32(oa, 0, 0); Marshal.WriteInt32(ob, 0, 0); Marshal.WriteInt32(oc, 0, 0);
            try
            {
                var loadRegion = Fn<LoadRegionFn>(Vt(loader, 3));
                int ok = loadRegion(loader, ix, iz, buf, count, oa, ob, oc);
                Log("LoadRegion ok=" + ok + " outA=" + Marshal.ReadInt32(oa) + " outB=" + Marshal.ReadInt32(ob) + " outC=" + Marshal.ReadInt32(oc));
                float mn = float.MaxValue, mx = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    float v = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(buf, i * 4)), 0);
                    if (v < mn) mn = v;
                    if (v > mx) mx = v;
                }
                Log(string.Format("LoadRegion heights min={0:F1} max={1:F1}", mn, mx));
                for (int i = 0; i < 8; i++)
                {
                    float v = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(buf, i * 4)), 0);
                    Log(string.Format("  height[{0}]={1:F2}", i, v));
                }
                // sample centre of region
                int cx2 = regionSize / 2, cz2 = regionSize / 2;
                float hv = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(buf, (cz2 * (regionSize + 1) + cx2) * 4)), 0);
                Log(string.Format("LoadRegion centre height={0:F2} at world ({1:F0},{2:F0})", hv,
                    originX + (ix * regionSize + cx2) * spanX, originZ + (iz * regionSize + cz2) * spanX));
            }
            catch (Exception e) { Log("LoadRegion ex: " + e.Message); }
            finally { Marshal.FreeHGlobal(buf); Marshal.FreeHGlobal(oa); Marshal.FreeHGlobal(ob); Marshal.FreeHGlobal(oc); }
        }

        // 7) terrain vt[5] = full LoadTerrain(path, opt, ownerPtr):
        //    creates its own data loader, reads landscapeinfo.json, builds the
        //    region table and the physx scene region manager.
        if (terrain != IntPtr.Zero)
        {
            IntPtr path2 = Marshal.StringToHGlobalAnsi(mapPath);
            try
            {
                var loadFull = Fn<FourArgFn>(Vt(terrain, 5));
                int ok = loadFull(terrain, path2, IntPtr.Zero, mgr);
                Log("LoadTerrain(vt5) ok=" + ok);
            }
            finally { Marshal.FreeHGlobal(path2); }
            DumpFields("terrain", terrain, new int[] { 0x20, 0x24, 0x28, 0x30, 0x34, 0x40, 0x48, 0x50, 0x58, 0x60, 0x70, 0x80, 0xf0, 0xfc, 0x118, 0x120 });
            IntPtr regionMgr = Marshal.ReadIntPtr(terrain, 0x58);
            if (regionMgr != IntPtr.Zero)
            {
                Log("regionMgr=" + Hex(regionMgr) + " vtable rva=0x" + (Marshal.ReadIntPtr(regionMgr).ToInt64() - h.ToInt64()).ToString("X"));
                DumpFields("regionMgr", regionMgr, new int[] { 0x8, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x58, 0x60, 0x68, 0x70, 0x78, 0x140, 0x150, 0x160, 0x170, 0x178 });
            }
            IntPtr table = Marshal.ReadIntPtr(terrain, 0x48);
            int countX = Marshal.ReadInt32(terrain, 0x24), countY = Marshal.ReadInt32(terrain, 0x28);
            Log(string.Format("region table={0} countX={1} countY={2}", Hex(table), countX, countY));

            // 7b) terrain vt[2] = UpdateTerrain(float3 pos): stream regions near pos.
            string posEnv = Environment.GetEnvironmentVariable("MAP_PROBE_POS");
            float px = 147463.5f, py = 5231.0f, pz = 49911.7f;   // systemCamera0 (world)
            if (!string.IsNullOrEmpty(posEnv))
            {
                string[] pp = posEnv.Split(',');
                px = float.Parse(pp[0]); py = float.Parse(pp[1]); pz = float.Parse(pp[2]);
            }
            IntPtr posBuf = Marshal.AllocHGlobal(12);
            Marshal.WriteInt32(posBuf, 0, BitConverter.ToInt32(BitConverter.GetBytes(px), 0));
            Marshal.WriteInt32(posBuf, 4, BitConverter.ToInt32(BitConverter.GetBytes(py), 0));
            Marshal.WriteInt32(posBuf, 8, BitConverter.ToInt32(BitConverter.GetBytes(pz), 0));
            try
            {
                var update = Fn<BoolArgFn>(Vt(terrain, 2));
                for (int step = 0; step < 40; step++)
                {
                    update(terrain, posBuf);
                    Thread.Sleep(100);
                }
                Log(string.Format("UpdateTerrain x40 at ({0:F0},{1:F0},{2:F0}) done", px, py, pz));
            }
            catch (Exception e) { Log("UpdateTerrain ex: " + e.Message); }
            finally { Marshal.FreeHGlobal(posBuf); }

            if (table != IntPtr.Zero && countX > 0 && countY > 0)
            {
                int total = countX * countY;
                int loaded = 0;
                IntPtr firstRegionObj = IntPtr.Zero;
                for (int i = 0; i < total; i++)
                {
                    int type = Marshal.ReadInt32(table, i * 0x30);
                    IntPtr p8 = Marshal.ReadIntPtr(table, i * 0x30 + 8);
                    IntPtr p10 = Marshal.ReadIntPtr(table, i * 0x30 + 0x10);
                    if (type != 0)
                    {
                        loaded++;
                        Log(string.Format("  region[{0}] type={1} p8={2} p10={3}", i, type, Hex(p8), Hex(p10)));
                        if (firstRegionObj == IntPtr.Zero && p8 != IntPtr.Zero) firstRegionObj = p8;
                    }
                }
                Log(string.Format("regions total={0} nonzero={1}", total, loaded));
                if (firstRegionObj != IntPtr.Zero)
                {
                    Log("first region obj=" + Hex(firstRegionObj) + " vtable rva=0x" + (Marshal.ReadIntPtr(firstRegionObj).ToInt64() - h.ToInt64()).ToString("X"));
                    DumpFields("regionObj", firstRegionObj, new int[] { 0x8, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38 });
                    IntPtr dataObj = Marshal.ReadIntPtr(firstRegionObj, 8);
                    if (dataObj != IntPtr.Zero)
                    {
                        long dvt = Marshal.ReadIntPtr(dataObj).ToInt64();
                        Log("region data obj=" + Hex(dataObj) + " first=" + Hex(new IntPtr(dvt)) + " rva=0x" + (dvt - h.ToInt64()).ToString("X"));
                        DumpFields("dataObj", dataObj, new int[] { 0x8, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x58, 0x60, 0x68, 0x70, 0x78, 0x80, 0x88, 0x90, 0x98, 0xa0, 0xa8, 0xb0 });
                    }
                }
            }

            // Scan terrain/regionMgr/manager for the PhysicsScene vtable (rva 0xFA7B8).
            IntPtr[] objs = new IntPtr[] { terrain, regionMgr, mgr };
            string[] names = new string[] { "terrain", "regionMgr", "mgr" };
            for (int oi = 0; oi < objs.Length; oi++)
            {
                IntPtr o = objs[oi];
                if (o == IntPtr.Zero) continue;
                for (int off = 0; off < 0x200; off += 8)
                {
                    long p = Marshal.ReadIntPtr(o, off).ToInt64();
                    if (p <= hBase || p >= hEnd) continue;
                    long q = Marshal.ReadIntPtr(new IntPtr(p)).ToInt64();
                    long rva = q - hBase;
                    if (rva == 0xFA7B8 || rva == 0xFA6B0 || rva == 0xFD198 || rva == 0xFCFF0 || rva == 0xFD2C0)
                    {
                        Log(string.Format("  {0}+0x{1:X2} -> {2:X} vtable rva=0x{3:X}", names[oi], off, p, rva));
                    }
                }
            }
        }
        Log("=== collision probe end ===");
    }
}

// Height sampler on top of the real terrain data loader
// (PhysicsEngine::KG3D_PhysxTerrainDataLoader_Source::LoadRegion).
internal sealed class TerrainSampler : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr LoadLibraryExA(string path, IntPtr hFile, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string name);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int CreateDataLoaderFn(IntPtr path, IntPtr opt, out IntPtr loader);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int DescFn(IntPtr self, IntPtr out32);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int LoadRegionFn(IntPtr self, int nX, int nZ, IntPtr pData, int nCount,
                              IntPtr outA, IntPtr outB, IntPtr outC);

    IntPtr _loader = IntPtr.Zero;
    IntPtr _buf = IntPtr.Zero;
    int _size, _nrx, _nrz, _count, _curIx = -1, _curIz = -1;
    float _cell, _originX, _originZ;
    Action<string> _log;

    static float ToF(IntPtr p, int off)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(p, off)), 0);
    }

    static T Fn<T>(IntPtr p) where T : class
    {
        return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
    }

    public TerrainSampler(string physDll, string mapPath, Action<string> log)
    {
        _log = log;
        IntPtr h = GetModuleHandleA("PhysicsEngineX64.dll");
        if (h == IntPtr.Zero) h = LoadLibraryExA(physDll, IntPtr.Zero, 0x8);
        if (h == IntPtr.Zero)
            throw new InvalidOperationException("PhysicsEngineX64.dll not loadable, err=" + Marshal.GetLastWin32Error());
        IntPtr p = GetProcAddress(h, "CreatePhysicsTerrainDataLoader");
        if (p == IntPtr.Zero) throw new InvalidOperationException("CreatePhysicsTerrainDataLoader not found");

        IntPtr mp = Marshal.StringToHGlobalAnsi(mapPath);
        IntPtr loader = IntPtr.Zero;
        try
        {
            var create = Fn<CreateDataLoaderFn>(p);
            int ok = create(mp, IntPtr.Zero, out loader);
            if (ok == 0 || loader == IntPtr.Zero) throw new InvalidOperationException("loader create failed");
        }
        finally { Marshal.FreeHGlobal(mp); }
        _loader = loader;

        IntPtr d = Marshal.AllocHGlobal(32);
        try
        {
            for (int i = 0; i < 32; i += 4) Marshal.WriteInt32(d, i, 0);
            IntPtr vt = Marshal.ReadIntPtr(loader);
            var desc = Fn<DescFn>(Marshal.ReadIntPtr(vt, 2 * IntPtr.Size));
            desc(loader, d);
            _size = Marshal.ReadInt32(d, 0);
            _nrx = Marshal.ReadInt32(d, 4);
            _nrz = Marshal.ReadInt32(d, 8);
            _cell = ToF(d, 0x10);
            _originX = ToF(d, 0x18);
            _originZ = ToF(d, 0x1C);
        }
        finally { Marshal.FreeHGlobal(d); }

        _count = (_size + 1) * (_size + 1);
        _buf = Marshal.AllocHGlobal(_count * 4);
        log(string.Format("TerrainSampler: size={0} regions={1}x{2} cell={3} origin=({4},{5})",
            _size, _nrx, _nrz, _cell, _originX, _originZ));
    }

    int RegionIndex(float v, float origin, int n)
    {
        int i = (int)Math.Floor((v - origin) / (_size * _cell));
        if (i < 0) i = 0;
        if (i >= n) i = n - 1;
        return i;
    }

    void EnsureRegion(int ix, int iz)
    {
        if (ix == _curIx && iz == _curIz) return;
        try
        {
            IntPtr vt = Marshal.ReadIntPtr(_loader);
            var load = Fn<LoadRegionFn>(Marshal.ReadIntPtr(vt, 3 * IntPtr.Size));
            IntPtr a = Marshal.AllocHGlobal(8), b = Marshal.AllocHGlobal(8), c = Marshal.AllocHGlobal(8);
            try
            {
                int ok = load(_loader, ix, iz, _buf, _count, a, b, c);
                if (ok != 0) { _curIx = ix; _curIz = iz; }
                else _log("LoadRegion failed (" + ix + "," + iz + ")");
            }
            finally { Marshal.FreeHGlobal(a); Marshal.FreeHGlobal(b); Marshal.FreeHGlobal(c); }
        }
        catch (Exception e) { _log("EnsureRegion ex: " + e.Message); }
    }

    float H(int idx)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(_buf, idx * 4)), 0);
    }

    public float Sample(float x, float z)
    {
        if (_buf == IntPtr.Zero) return 0f;
        int ix = RegionIndex(x, _originX, _nrx), iz = RegionIndex(z, _originZ, _nrz);
        EnsureRegion(ix, iz);
        float gx = (x - _originX) / _cell - ix * _size;
        float gz = (z - _originZ) / _cell - iz * _size;
        if (gx < 0f) gx = 0f;
        if (gx > _size - 1) gx = _size - 1;
        if (gz < 0f) gz = 0f;
        if (gz > _size - 1) gz = _size - 1;
        int x0 = (int)Math.Floor(gx), z0 = (int)Math.Floor(gz);
        int x1 = x0 + 1; if (x1 > _size) x1 = _size;
        int z1 = z0 + 1; if (z1 > _size) z1 = _size;
        float fx = gx - x0, fz = gz - z0;
        int stride = _size + 1;
        float h00 = H(z0 * stride + x0);
        float h10 = H(z0 * stride + x1);
        float h01 = H(z1 * stride + x0);
        float h11 = H(z1 * stride + x1);
        float a = h00 + (h10 - h00) * fx;
        float b = h01 + (h11 - h01) * fx;
        return a + (b - a) * fz;
    }

    public void Dispose()
    {
        if (_buf != IntPtr.Zero) Marshal.FreeHGlobal(_buf);
        _buf = IntPtr.Zero;
    }
}
