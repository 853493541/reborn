// RebornClient — M1.1 scaffold: real map + animated player (dummy + KGModelCLR),
// walk/run/jump/fall, follow camera, one skill key, HUD stub.
// Build: client\build_client.cmd    Run: bin64\reborn_client.exe (cwd = editor root)
//
// Env:
//   RC_MAP=<vfs jsonmap>          default 龙门寻宝
//   RC_SPAWN=x,y,z                optional spawn (y optional -> terrain)
//   RC_AUTORUN=ms                 exit after N ms (0 = until window closed)
//   RC_SHOTS=2000,5000,...        screenshot times (ms)
//   RC_CLIP_IDLE/WALK/RUN/JUMP/FALL/SKILL=<vfs .ani/.tani path>
//   RC_SKILL_MS=8000              skill clip duration before returning to state clip
//   RC_YAW_OFFSET=0               model facing calibration (radians)
//   RC_SCALE=1                    player model scale
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MovieEngineCLR;

internal static class RebornClient
{
    static string outDir;
    static Action<string> Log;

    [STAThread]
    private static void Main(string[] args)
    {
        string editorRoot = @"C:\SeasunGame\MovieEditor";
        string startupPath = Path.Combine(editorRoot, "bin64");
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

        outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reborn_out");
        Directory.CreateDirectory(outDir);
        var logLines = new System.Collections.Generic.List<string>();
        Log = delegate(string s)
        {
            try { File.AppendAllText(Path.Combine(outDir, "reborn.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); }
            catch { }
            Console.WriteLine(s);
            lock (logLines)
            {
                logLines.Add(DateTime.Now.ToString("HH:mm:ss") + " " + s);
                if (logLines.Count > 200) logLines.RemoveRange(0, logLines.Count - 200);
            }
        };
        Log("start map=" + mapPath);

        var form = new Form();
        form.Text = "JX3";
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

        var scene = new KGSceneCLR();
        int loadResult = scene.LoadMap(mapPath, false);
        Log("LoadMap result=" + loadResult);
        if (loadResult < 0) { Log("FATAL: LoadMap failed"); return; }
        scene.SetActiveEnvironment();
        long winId = scene.AddOutputWindow("", panel.Handle.ToInt64(), 0);
        Log("winId=" + winId);

        TerrainSampler sampler = null;
        try
        {
            sampler = new TerrainSampler(
                @"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll", mapPath, Log);
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

        // ---------------- player ----------------
        float px = 0f, py = 0f, pz = 0f, vy = 0f;
        float viewX = 0f, viewY = 0f, viewZ = 1f;   // spawn orientation (measured once)
        bool grounded = false;
        // JX3-modeled camera (engine_host_spike/CameraSystem.cs, ported)
        CameraSystem camSys = new CameraSystem();
        CameraSettings cameraSettings = null;
        {
            double sc;
            if (double.TryParse(Env("RC_CAMERA_SCALE", ""), out sc) && sc > 0) camSys.UnitsPerMeter = sc;
            string camCfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "camera.json");
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
        try
        {
            // FOV: the editor's view-angle factor. A wider value makes the
            // character look smaller (open item: the game's fFovy is missing),
            // so it can be tuned for testing with RC_VIEW_ANGLE.
            Log("view angle factor=" + scene.GetViewAngleFactor());
            float va;
            if (float.TryParse(Env("RC_VIEW_ANGLE", ""), out va) && va > 0f)
            {
                scene.SetViewAngleFactor(va);
                Log("view angle factor set to " + va);
            }
        }
        catch (Exception e) { Log("view angle: " + e.Message); }
        float worldDirX = 0f, worldDirZ = 0f;
        int lastKeySig = -1;
        long handle = 0, attachedHandle = -999;
        var model = new KGModelCLR();
        string curClip = null;
        float curYaw = 0f;
        float lastModelX = float.MaxValue, lastModelZ = float.MaxValue, lastModelYaw = float.MaxValue;

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
                for (int i = 0; i < 3; i++) { engine.FrameMove(); engine.Render(); Application.DoEvents(); }
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
                px = 23334f; py = 761f; pz = 24224f;
            }
            // The physics terrain loader tracks the engine's streamed terrain:
            // right after the camera jumps it can return all-zero heights for
            // the spawn region (observed on 龙门寻宝). Pump frames and retry
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
                var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
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
        bool jumpPressed = false, skillPressed = false, spaceDown = false, oneDown = false;
        bool walkMode = false;   // real default is run; "/" (TOGGLERUN) switches to walk
        bool wSprint = false;    // double-tap W and hold -> sprint (8.5 尺/s)
        long lastWPress = 0;
        bool demo = Env("RC_DEMO", "0") == "1", demoJumped = false, demoSkilled = false;
        bool demoCollide = Env("RC_DEMO_COLLIDE", "0") == "1", demoTeleported = false;
        bool camDemo = Env("RC_CAM_DEMO", "0") == "1";
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
            if ((lmbDown || rmbDown) && !mouseLocked) lockMouse();
        };
        MouseEventHandler onMouseUp = delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) lmbDown = false;
            else if (e.Button == MouseButtons.Right) rmbDown = false;
            if (!lmbDown && !rmbDown && mouseLocked) unlockMouse();
        };
        MouseEventHandler onMouseMove = delegate(object s, MouseEventArgs e)
        {
            if (!mouseLocked || (!lmbDown && !rmbDown)) return;
            System.Drawing.Point p = panelPoint(s, e);
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
        form.MouseWheel += wheel;
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) unlockMouse();
            if (e.KeyCode == Keys.W)
            {
                if (!pW)   // new press (auto-repeat keeps pW true)
                {
                    long t = Environment.TickCount;
                    if (t - lastWPress < 350) wSprint = true;
                    lastWPress = t;
                }
                pW = true;
            }
            else if (e.KeyCode == Keys.S) pS = true;
            else if (e.KeyCode == Keys.A) pA = true;
            else if (e.KeyCode == Keys.D) pD = true;
            else if (e.KeyCode == Keys.ShiftKey) shiftDown = true;
            else if (e.KeyCode == Keys.Space && !spaceDown) { spaceDown = true; jumpPressed = true; }
            else if (e.KeyCode == Keys.D1 && !oneDown) { oneDown = true; skillPressed = true; }
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
                // CameraReset: behind the character, pitch -15 deg, distance 1x
                camSys.SetMaxDistance(camSys.Row.F("InitCameraDistance", 6.0));
                alignEngineCamera(cameraYawBehind(), -Math.PI / 12.0);
                Log("camera reset: behind character, pitch -15deg");
            }
            else if (e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
            {
                // CameraSetView(0) / (180): yaw preset relative to facing
                measureView();
                double cp = Math.Asin(Math.Max(-1.0, Math.Min(1.0, viewY)));
                double by = cameraYawBehind() + (e.KeyCode == Keys.End ? Math.PI : 0.0);
                alignEngineCamera(by, cp);
                Log("camera view preset: " + (e.KeyCode == Keys.End ? "front" : "behind"));
            }
        };
        form.KeyUp += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) { pW = false; wSprint = false; }
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
        // "16帧等于1秒", UNIT_SCALE...md §2): walk 6 / run 20 u/frame -> 96 / 320
        // u/s. Cross-check: the official UI shows 跑步速度 5 尺/秒 and
        // 20 u/frame * 16 fps = 320 u/s = 5 * 64 u (1 尺 = 64 u). Host controls:
        // default RUN, "/" toggles WALK, hold Shift for a 10x testing speed.
        float pGravity = -2475f, pJumpV = 1350f;
        float pSpeed = 96f, pRun = 320f;
        float pSprint = 8.5f * 64f;   // double-tap W hold: 8.5 尺/s = 544 u/s
        // Real character size (docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md;
        // 1 unit = 1 cm): the loaded 花萝 actor (f1_1004 head + f1_2227 dress
        // parts) measures 115.58 u = 1.16 m from the extracted bind-pose
        // meshes. Capsule radius scaled from the old adult preset (25 at 170)
        // by the same ratio.
        float playerRadius = 17f, playerHeight = 116f;
        float.TryParse(Env("RC_RADIUS", "17"), out playerRadius);
        float.TryParse(Env("RC_HEIGHT", "116"), out playerHeight);
        int blockedEvents = 0;
        long colCalls = 0, colBlockedCalls = 0;
        bool colDebug = Env("RC_COL_DEBUG", "0") == "1";
        long lastMs = 0, lastLog = 0, lastHud = 0, skillUntil = 0, lastCamMeasure = 0, lastCamLog = 0, lastOrbitMs = 0;
        bool camDebug = Env("RC_CAM_DEBUG", "0") == "1";
        bool rotTest = Env("RC_ROT_TEST", "0") == "1";
        int rotTestStep = -1;
        long rotTestStart = 0;
        string fixedCam = Env("RC_FIXED_CAM", "");
        bool fixedCamSet = false;
        long frames = 0, fpsAt = 0, fps = 0;
        int shotIdx = 0;
        long[] shots = ParseShots(Env("RC_SHOTS", "3000,8000,15000,30000"));

        // Initialize from the real scene_init_param row (or the user's saved
        // runtime yaw/pitch with RC_CUSTOM_DAT). Maps without their own row
        // (e.g. map 296) have no real init data: keep the spawn view instead
        // of applying another map's yaw. 300 px/pass and the engine orbit is
        // ~0.0015 rad/px, so a large correction needs several passes.
        bool applyCamInit = cameraSettings.HasSceneInit || cameraSettings.HasSavedRuntime;
        if (applyCamInit)
        {
            alignEngineCamera(cameraSettings.InitYaw, cameraSettings.InitPitch);
            Log(string.Format("camera init applied mapId={0} yaw={1:F3} pitch={2:F3}",
                cameraSettings.MapId, camSys.Yaw, camSys.Pitch));
        }
        else
        {
            measureView();
            Log(string.Format("camera init skipped mapId={0} (no scene_init_param row; keeping spawn view)",
                cameraSettings.MapId));
        }
        if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
            camSys.Yaw = Math.Atan2(-viewZ, -viewX);

        // Camera aim geometry: the placement keeps the engine's aim on the
        // anchor (camY = anchor.y - tan(pitch) * distance), so the tracked
        // pitch must match the engine's view pitch. Maps with a real scene row
        // already got it from the init loop above; maps without one keep the
        // spawn view direction, so align the engine pitch once to the row
        // geometry (-atan2(CameraHeight, distance)) and adopt it as tracked
        // pitch. Loop-limited: continuous vertical orbit deltas break the
        // engine screenshot path. RC_PITCH_ALIGN=0 skips the alignment.
        bool pitchAlign = Env("RC_PITCH_ALIGN", "1") == "1" && !applyCamInit;
        double alignHeight = camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter;
        double alignPitch = -Math.Atan2(alignHeight, Math.Max(1.0, camSys.Distance));
        if (pitchAlign)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                measureView();
                double measured = Math.Asin(Math.Max(-1.0, Math.Min(1.0, viewY)));
                int dyp = (int)Math.Round(-(alignPitch - measured) / 0.00121);
                if (dyp > 200) dyp = 200;
                if (dyp < -200) dyp = -200;
                if (dyp > -8 && dyp < 8) break;
                scene.ExecAction(30, 1, 0, makeLParam(lockCenter.X, lockCenter.Y));
                scene.ExecAction(1, 1, 0, makeLParam(lockCenter.X, lockCenter.Y + dyp));
                Log("camera pitch align: dyp=" + dyp + " pass=" + pass);
            }
            camSys.Pitch = alignPitch;
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

            if (orbitQueue.Count > 0)
            {
                int ox = 0, oy = 0;
                while (orbitQueue.Count > 0) { int[] d = orbitQueue.Dequeue(); ox += d[0]; oy += d[1]; }
                // row per-frame clamps (CameraMaxDeltaYaw/Pitch, row speeds in
                // CAMERA_REAL_VALUES.md) converted through the measured engine
                // orbit sensitivity: 0.0018 rad/px yaw, 0.00121 rad/px pitch
                CameraParams orow = camSys.Row;
                double yawMaxPx = orow.F("CameraMaxDeltaYaw", 2.0 * Math.PI) / 0.0018;
                double pitchMaxPx = orow.F("CameraMaxDeltaPitch", 1.56) / 0.00121;
                if (ox > yawMaxPx) ox = (int)yawMaxPx; else if (ox < -yawMaxPx) ox = (int)-yawMaxPx;
                if (oy > pitchMaxPx) oy = (int)pitchMaxPx; else if (oy < -pitchMaxPx) oy = (int)-pitchMaxPx;
                scene.ExecAction(30, 1, 0, makeLParam(lockCenter.X, lockCenter.Y));
                scene.ExecAction(1, 1, 0, makeLParam(lockCenter.X + ox, lockCenter.Y + oy));
                // track yaw and pitch ourselves: the engine orbit also moves
                // the camera position, and a nudge here made the camera flash
                // and glide back. Pitch tracking keeps the placement geometry
                // consistent with the engine's aim (see the placement below).
                camSys.Yaw -= ox * 0.0018f;
                float twoPi = 2f * (float)Math.PI;
                if (camSys.Yaw > (float)Math.PI) camSys.Yaw -= twoPi;
                if (camSys.Yaw < -(float)Math.PI) camSys.Yaw += twoPi;
                camSys.Pitch -= oy * 0.00121f;
                double pmax = Math.PI / 2.0 - 0.05;
                if (camSys.Pitch > pmax) camSys.Pitch = pmax;
                else if (camSys.Pitch < -pmax) camSys.Pitch = -pmax;
                lastOrbitMs = now;
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
                // engine ROTATE_CAMERA mapping: 0.0035 rad/px (MAP_CAMERA_SENS)
                if (now >= 2000 && now < 6000)
                {
                    int px2 = (int)(dt * 1.0f / 0.0035f);
                    if (px2 < 1) px2 = 1;
                    orbitQueue.Enqueue(new int[] { px2, 0 });
                }
                if (now >= 6000 && now < 9000)
                {
                    int py2 = (int)(dt * 0.5f / 0.0035f);
                    if (py2 < 1) py2 = 1;
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
            if (now - lastCamMeasure >= 1000 && now - lastOrbitMs > 400)
            {
                lastCamMeasure = now;
                measureView();
                if (Math.Abs(viewX) > 1e-4f || Math.Abs(viewZ) > 1e-4f)
                    camSys.Yaw = Math.Atan2(-viewZ, -viewX);
            }
            if (camDebug && now - lastCamLog >= 500)
            {
                lastCamLog = now;
                float dbgx = 0f, dbgy = 0f, dbgz = 0f;
                scene.GetCameraPos(ref dbgx, ref dbgy, ref dbgz);
                Log(string.Format("camdbg mode={0} yaw={1:F2} pitch={2:F2} dist={3:F0} cam=({4:F0},{5:F0},{6:F0})",
                    camSys.Mode, camSys.Yaw, camSys.Pitch, camSys.Distance, dbgx, dbgy, dbgz));
            }

            // movement is camera-relative: forward = camera -> anchor
            double cfx, cfz;
            camSys.Forward(out cfx, out cfz);
            float hx = (float)cfx;
            float hz = (float)cfz;

            // skill
            if (skillPressed)
            {
                skillPressed = false;
                skillUntil = now + skillMs;
                curClip = null;
                setClip(clipSkill);
                Log("skill cast");
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

            // horizontal move + slope blocking (map-host rules)
            float ground = sampler != null ? sampler.Sample(px, pz) : py;
            bool blocked = false;
            if (moving)
            {
                float sp = (shiftDown ? pRun * 10f : (walkMode ? pSpeed : pRun)) / len;
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

            // RMB (CAMERAORSELECTORMOVESTICKY) also turns the character to the
            // camera direction; LMB drag rotates the camera only.
            if (rmbDown)
                curYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));

            // object/foliage collision (walls, buildings, rocks, trees)
            if (col != null)
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
            if (grounded)
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
            if (!grounded)
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

            // animation state
            if (skillUntil > now) { /* skill clip playing */ }
            else if (!grounded) setClip(vy > 0f ? clipJump : clipFall);
            else if (moving) setClip(walkMode ? clipWalk : clipRun);
            else setClip(clipIdle);

            // model update (only when changed; keeps animation alive)
            if (Math.Abs(px - lastModelX) > 0.5f || Math.Abs(pz - lastModelZ) > 0.5f ||
                Math.Abs(curYaw - lastModelYaw) > 0.01f)
            {
                placePlayer(px, py, pz, curYaw);
                if (handle != attachedHandle)
                {
                    model.AttachModel(handle);
                    attachedHandle = handle;
                }
                lastModelX = px; lastModelZ = pz; lastModelYaw = curYaw;
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
                bool sprinting = movingNow && shiftDown;
                if (sprinting)
                {
                    if (camSys.Mode != CameraSystem.MODE_SPRINT)
                        camSys.SwitchMode(CameraSystem.MODE_SPRINT, false);
                }
                else if (camSys.Mode != CameraSystem.MODE_CHARACTER)
                {
                    camSys.SwitchMode(CameraSystem.MODE_CHARACTER, false);
                }
                double dist = camSys.UpdateDistance(dt, sprinting, pRun / camSys.UnitsPerMeter)
                              * cameraSettings.EyeScale;

                // The nudge's vertical component jitters, so the horizontal
                // direction comes from the tracked yaw; the height comes from
                // the tracked pitch: the engine aims at the anchor with pitch P,
                // so the camera sits at anchor.y - tan(P) * distance (at the
                // startup pitch -atan2(CameraHeight, distance) this equals the
                // row CameraHeight, 200 u at 600 u).
                double vx = hx, vz = hz;
                double hlen = Math.Sqrt(vx * vx + vz * vz);
                if (hlen < 1e-6) { vx = 0; vz = 1; hlen = 1; }
                vx /= hlen; vz /= hlen;

                double pitchOffset = Math.Tan(camSys.Pitch);
                double ax2 = px, ay2 = py + 90.0, az2 = pz;
                double camX = ax2 - vx * dist;
                double camY = ay2 - pitchOffset * dist;
                double camZ = az2 - vz * dist;

                // obstruction: terrain above the anchor->camera ray
                if (sampler != null)
                {
                    const double margin = 20.0;
                    double ddy = camY - ay2;
                    double ddx = camX - ax2, ddz = camZ - az2;
                    double rayLen = Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                    const int steps = 14;
                    for (int i = 2; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        float g = sampler.Sample((float)(ax2 + ddx * t), (float)(az2 + ddz * t));
                        if (g + margin > ay2 + ddy * t)
                        {
                            double d2 = Math.Max(150.0, t * rayLen - 40.0);
                            camX = ax2 - vx * d2;
                            camY = ay2 - pitchOffset * d2;
                            camZ = az2 - vz * d2;
                            break;
                        }
                    }
                    float camGround = sampler.Sample((float)camX, (float)camZ) + 30f;
                    if (camY < camGround) camY = camGround;
                }
                scene.SetCameraPos((float)camX, (float)camY, (float)camZ, false);
                }
            }
            catch (Exception e) { Log("camera system ex: " + e.Message); }

            engine.FrameMove();
            engine.Render();
            Application.DoEvents();

            if (now - lastHud >= 250)
            {
                lastHud = now;
                string state = skillUntil > now ? "SKILL" : !grounded ? (vy > 0f ? "JUMP" : "FALL")
                             : moving ? (shiftDown ? "RUN x10" : walkMode ? "WALK" : "RUN") : "IDLE";
                float moveSpeed = shiftDown ? pRun * 10f : (walkMode ? pSpeed : pRun);
                hud.Text = string.Format(
                    "JX3\nfps {0}\npos {1:F0},{2:F0},{3:F0}\nstate {4}{5} hits {6}\nspeed {7:F1} \u5C3A/s\ncam {8} yaw {9:F2} dist {10:F0}\nclip {11}\nWASD move | / walk-run | Shift 10x | Space jump | 1 skill | C teleport\nLMB drag = camera | RMB drag = camera+turn | wheel zoom | F11 reset | Home/End view (Esc unlock)",
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
                Log(string.Format("t={0}s fps={1} pos=({2:F0},{3:F0},{4:F0}) vy={5:F0} grounded={6} blocked={7} hits={8} colCalls={9} colBlocked={10}{11} clip={12}",
                    now / 1000, fps, px, py, pz, vy, grounded, blocked, blockedEvents,
                    colCalls, colBlockedCalls, nearInfo,
                    curClip == null ? "-" : Path.GetFileName(curClip)));
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
