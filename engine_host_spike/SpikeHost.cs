using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MovieEngineCLR;
using MovieEditor.ActorEditor;

internal static class SpikeHost
{
    [STAThread]
    private static void Main(string[] args)
    {
        string editorRoot = @"C:\SeasunGame\MovieEditor";
        string startupPath = Path.Combine(editorRoot, "bin64");
        string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
        string actorPath = Path.Combine(editorRoot, "source", "\u82B1\u841D\u65E0\u52A8\u4F5C.actor");
        string taniPath =
            "data\\source\\player\\f1\\\u52A8\u4F5C\\f1s07cj\u91CD\u5251\u6280\u80FD15_\u98CE\u6765\u5434\u5C71\u7EA2\u8272hd.tani";
        string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spike_out");
        Directory.CreateDirectory(outDir);

        // input debug log (file only - Console can block)
        Action<string> Log = delegate(string s)
        {
            try { File.AppendAllText(Path.Combine(outDir, "input.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); }
            catch { }
        };

        Console.WriteLine("actorPath=" + actorPath);
        Console.WriteLine("taniPath=" + taniPath);

        var form = new Form();
        form.Text = "JX3 Engine Host \u2014 \u98CE\u6765\u5434\u5C71 (L-drag angle | wheel zoom | Q/E height | F focus | R reset)";
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
        if (ok == 0) { Console.WriteLine("FATAL: engine init failed"); return; }

        bool withEditor = Environment.GetEnvironmentVariable("SPIKE_EDITOR") != "0";
        bool withSound = Environment.GetEnvironmentVariable("SPIKE_SOUND") != "0";

        if (withEditor)
        {
            try
            {
                int editorResult = editor.Init(editorRoot, err, form.Handle.ToInt64());
                Console.WriteLine("editor.Init result={0}", editorResult);
            }
            catch (Exception e) { Console.WriteLine("editor.Init ex: " + e.Message); }
        }
        else
        {
            Console.WriteLine("editor.Init SKIPPED (pure engine mode)");
        }
        if (withSound)
        {
            try
            {
                int soundResult = sound.Init(startupPath, form.Handle.ToInt64());
                Console.WriteLine("sound.Init result={0} (Wwise banks under data/Wwiseaudio/GeneratedSoundBanks/Windows/Base)", soundResult);
            }
            catch (Exception e) { Console.WriteLine("sound.Init ex: " + e.Message); }
        }
        else
        {
            Console.WriteLine("sound.Init SKIPPED");
        }

        var scene = engine.NewEmptyScene();
        Console.WriteLine("scene null={0}", scene == null);
        if (scene == null) { Console.WriteLine("FATAL: no scene"); return; }

        long winId = scene.AddOutputWindow("", panel.Handle.ToInt64(), 2);
        Console.WriteLine("winId={0}", winId);

        // Camera controls:
        //   left drag  = ROTATE_CAMERA (1)  orbit / angle (ROTATE_VIEW=4 has no effect in host)
        //   middle drag= PAN_VIEW    (3)  pan
        //   wheel      = MOUSE_WHEEL (31) zoom
        //   Q / E      = camera height up / down
        //   F = focus, R = reset
        Func<int, int, int> makeLParam = delegate(int x, int y)
        {
            return ((y & 0xFFFF) << 16) | (x & 0xFFFF);
        };
        int dragAction = 0;
        var pending = new System.Collections.Generic.Queue<int[]>();
        panel.MouseDown += delegate(object s, MouseEventArgs e)
        {
            dragAction = e.Button == MouseButtons.Left ? 1
                       : e.Button == MouseButtons.Middle ? 3 : 0;
            if (dragAction != 0)
            {
                // action 30 = MOUSE_MOVE sets the engine input reference (verified: 19 does not)
                pending.Enqueue(new int[] { 30, 1, e.X, e.Y });
                Log(string.Format("mouse down {0} at {1},{2}", e.Button, e.X, e.Y));
            }
        };
        panel.MouseMove += delegate(object s, MouseEventArgs e)
        {
            if (dragAction != 0) pending.Enqueue(new int[] { dragAction, 1, e.X, e.Y });
        };
        panel.MouseUp += delegate(object s, MouseEventArgs e)
        {
            if (dragAction != 0)
            {
                pending.Enqueue(new int[] { 30, 1, e.X, e.Y });
                dragAction = 0;
                Log(string.Format("mouse up {0} at {1},{2}", e.Button, e.X, e.Y));
            }
        };
        MouseEventHandler wheel = delegate(object s, MouseEventArgs e)
        {
            pending.Enqueue(new int[] { 31, e.Delta < 0 ? 0 : 1, 0, 0 });
        };
        panel.MouseWheel += wheel;
        form.MouseWheel += wheel;
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F) pending.Enqueue(new int[] { 102, 0, 0, 0 });
            if (e.KeyCode == Keys.R) pending.Enqueue(new int[] { 103, 0, 0, 0 });
            if (e.KeyCode == Keys.Q) pending.Enqueue(new int[] { 100, 0, 0, 0 });
            if (e.KeyCode == Keys.E) pending.Enqueue(new int[] { 101, 0, 0, 0 });
        };
        panel.Focus();

        var actor = new KGMovieActorCLR();
        actor.Init();
        try { ActorEditorCommandHelper.LoadFromFile(actor, actorPath, 0); }
        catch (Exception e) { Console.WriteLine("LoadFromFile ex: " + e.Message); }
        long handle = actor.GetModelHandle();
        Console.WriteLine("actor handle={0}", handle);
        scene.AppendModel(handle);
        try { scene.FocusOnModel(); Console.WriteLine("FocusOnModel ok"); }
        catch (Exception e) { Console.WriteLine("FocusOnModel ex: " + e.Message); }

        var model = new KGModelCLR();
        model.AttachModel(handle);
        int playResult = model.PlayAnimation(taniPath, 0, 1.0f, 0);
        Console.WriteLine("PlayAnimation result={0}", playResult);
        PlayFlwsSound(outDir);

        if (Environment.GetEnvironmentVariable("SPIKE_INPUTTEST") == "5")
        {
            Action<string> p = delegate(string tag)
            {
                float px = 0f, py = 0f, pz = 0f;
                scene.GetCameraPos(ref px, ref py, ref pz);
                Console.WriteLine("{0}: ({1:F1},{2:F1},{3:F1})", tag, px, py, pz);
            };
            Action<int, int, int, int> send = delegate(int act, int a2, int x, int y)
            {
                scene.ExecAction(act, a2, 0, makeLParam(x, y));
                engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(30);
            };
            Action reset = delegate
            {
                scene.ExecAction(1001, 1, 0, makeLParam(640, 360));
                engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(100);
            };
            Console.WriteLine("INPUTTEST5 start");

            reset(); p("reset");
            Console.WriteLine("-- A: 19,1 then 1,1 --");
            send(19, 1, 600, 300); p("  after 19,1");
            send(1, 1, 600, 300); p("  after 1,1 zero");
            send(1, 1, 610, 300); p("  after 1,1 +10");

            reset(); p("reset");
            Console.WriteLine("-- B: 30,1 then 1,1 --");
            send(30, 1, 600, 300); p("  after 30,1");
            send(1, 1, 600, 300); p("  after 1,1 zero");
            send(1, 1, 610, 300); p("  after 1,1 +10");

            reset(); p("reset");
            Console.WriteLine("-- C: 1,0 then 1,1 --");
            send(1, 0, 600, 300); p("  after 1,0");
            send(1, 1, 600, 300); p("  after 1,1 zero");
            send(1, 1, 610, 300); p("  after 1,1 +10");

            reset(); p("reset");
            Console.WriteLine("-- D: 30,0 then 1,1 --");
            send(30, 0, 600, 300); p("  after 30,0");
            send(1, 1, 600, 300); p("  after 1,1 zero");
            send(1, 1, 610, 300); p("  after 1,1 +10");

            reset(); p("reset");
            Console.WriteLine("-- E: 1,1 twice at down --");
            send(1, 1, 600, 300); p("  after 1,1 #1");
            send(1, 1, 600, 300); p("  after 1,1 #2");
            send(1, 1, 610, 300); p("  after 1,1 +10");

            Console.WriteLine("INPUTTEST5 done");
        }
        if (Environment.GetEnvironmentVariable("SPIKE_INPUTTEST") == "4")
        {
            Action<string> p = delegate(string tag)
            {
                float px = 0f, py = 0f, pz = 0f;
                scene.GetCameraPos(ref px, ref py, ref pz);
                Console.WriteLine("{0}: ({1:F1},{2:F1},{3:F1})", tag, px, py, pz);
            };
            Console.WriteLine("INPUTTEST4 start");
            p("t0");
            // drag A: orbit
            scene.ExecAction(19, 1, 0, makeLParam(600, 300));
            engine.FrameMove(); engine.Render(); Application.DoEvents();
            for (int i = 1; i <= 15; i++)
            {
                scene.ExecAction(1, 1, 0, makeLParam(600 + i * 10, 300 + i * 3));
                engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(10);
            }
            scene.ExecAction(19, 0, 0, makeLParam(750, 345));
            engine.FrameMove(); engine.Render(); Application.DoEvents();
            p("after drag A");
            // drag B: down at previous end, then a ZERO-delta move
            scene.ExecAction(19, 1, 0, makeLParam(750, 345));
            engine.FrameMove(); engine.Render(); Application.DoEvents();
            scene.ExecAction(1, 1, 0, makeLParam(750, 345));
            engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(100);
            p("after zero-delta move");
            scene.ExecAction(1, 1, 0, makeLParam(760, 345));
            engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(100);
            p("after +10px move");
            scene.ExecAction(19, 0, 0, makeLParam(760, 345));
            engine.FrameMove(); engine.Render(); Application.DoEvents();
            p("after release");
            Console.WriteLine("INPUTTEST4 done");
        }

        if (Environment.GetEnvironmentVariable("SPIKE_INPUTTEST") == "2")
        {
            Console.WriteLine("INPUTTEST2 start");
            float bx = 0f, by = 0f, bz = 0f;
            scene.GetCameraPos(ref bx, ref by, ref bz);
            Console.WriteLine("cam before = ({0:F1},{1:F1},{2:F1})", bx, by, bz);
            string before = Path.Combine(outDir, "inputtest2_before.png");
            scene.SetScreenShot(before, 2); scene.DoScreenShotImmediate();
            scene.ExecAction(1, 1, 0, makeLParam(300, 300));
            for (int i = 1; i <= 20; i++)
            {
                scene.ExecAction(1, 1, 0, makeLParam(300 + i * 15, 300 + i * 3));
                engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(10);
            }
            float ax = 0f, ay = 0f, az = 0f;
            scene.GetCameraPos(ref ax, ref ay, ref az);
            Console.WriteLine("cam after  = ({0:F1},{1:F1},{2:F1})", ax, ay, az);
            string mid = Path.Combine(outDir, "inputtest2_mid.png");
            scene.SetScreenShot(mid, 2); scene.DoScreenShotImmediate();
            Console.WriteLine("mid shot -> " + mid);
            int rel = scene.ExecAction(1, 0, 0, makeLParam(600, 360));
            Console.WriteLine("release ExecAction ret={0}", rel);
            engine.FrameMove(); engine.Render(); Application.DoEvents(); Thread.Sleep(200);
            string after = Path.Combine(outDir, "inputtest2_after_release.png");
            scene.SetScreenShot(after, 2); scene.DoScreenShotImmediate();
            Console.WriteLine("after shot -> " + after);
        }

        int[] shots = { 0, 2000, 4000, 7000 };
        int ci = 0;
        bool loop = Environment.GetEnvironmentVariable("SPIKE_LOOP") != "0";
        bool camlog = Environment.GetEnvironmentVariable("SPIKE_CAMLOG") == "1";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var camSw = System.Diagnostics.Stopwatch.StartNew();
        bool dragshot = Environment.GetEnvironmentVariable("SPIKE_DRAGSHOT") == "1";
        long lastCmdMs = -100000;
        long lastShotMs = -100000;
        int dragShotSeq = 0;
        while (!form.IsDisposed)
        {
            while (pending.Count > 0)
            {
                int[] cmd = pending.Dequeue();
                lastCmdMs = sw.ElapsedMilliseconds;
                if (cmd[0] == 1 || cmd[0] == 4 || cmd[0] == 3 || cmd[0] == 2 || cmd[0] == 19 || cmd[0] == 30)
                {
                    scene.ExecAction(cmd[0], cmd[1], 0, makeLParam(cmd[2], cmd[3]));
                    if (camlog)
                    {
                        float qx = 0f, qy = 0f, qz = 0f;
                        scene.GetCameraPos(ref qx, ref qy, ref qz);
                        Log(string.Format("cmd {0},{1} at {2},{3} -> campos {4:F1},{5:F1},{6:F1}",
                            cmd[0], cmd[1], cmd[2], cmd[3], qx, qy, qz));
                    }
                }
                else if (cmd[0] == 31)
                {
                    scene.ExecAction(31, 1, cmd[1], 1);
                }
                else if (cmd[0] == 100 || cmd[0] == 101)
                {
                    float cx = 0f, cy = 0f, cz = 0f;
                    scene.GetCameraPos(ref cx, ref cy, ref cz);
                    scene.SetCameraPos(cx, cy + (cmd[0] == 100 ? 50f : -50f), cz, false);
                }
                else if (cmd[0] == 102) scene.FocusOnModel();
                else if (cmd[0] == 103) scene.ResetCameraPosLookAtUp();
            }
            sound.FrameMove();
            engine.FrameMove();
            engine.Render();
            Application.DoEvents();
            if (dragshot && sw.ElapsedMilliseconds - lastCmdMs < 1500 && sw.ElapsedMilliseconds - lastShotMs >= 400)
            {
                lastShotMs = sw.ElapsedMilliseconds;
                dragShotSeq++;
                string dp = Path.Combine(outDir, string.Format("drag_{0:D3}.png", dragShotSeq));
                try { scene.SetScreenShot(dp, 2); scene.DoScreenShotImmediate(); }
                catch { }
            }
            if (camlog && camSw.ElapsedMilliseconds >= 1000)
            {
                camSw.Restart();
                float lx = 0f, ly = 0f, lz = 0f;
                scene.GetCameraPos(ref lx, ref ly, ref lz);
                Log(string.Format("campos {0:F1},{1:F1},{2:F1}", lx, ly, lz));
            }
            if (ci < shots.Length && sw.ElapsedMilliseconds >= shots[ci])
            {
                string png = Path.Combine(outDir, "spike_t" + shots[ci] + "ms.png");
                try
                {
                    scene.SetScreenShot(png, 2);
                    scene.DoScreenShotImmediate();
                    Console.WriteLine("shot t={0}ms -> {1} exists={2}", shots[ci], png, File.Exists(png));
                }
                catch (Exception e) { Console.WriteLine("screenshot ex: " + e.Message); }
                ci++;
            }
            if (sw.ElapsedMilliseconds >= 8000)
            {
                if (!loop) break;
                model.PlayAnimation(taniPath, 0, 1.0f, 0);
                PlayFlwsSound(outDir);
                ci = shots.Length;
                sw.Restart();
                Console.WriteLine("loop restart");
            }
            Thread.Sleep(10);
        }
        Console.WriteLine("DONE");
    }

    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

    private const uint SND_ASYNC = 0x0001;
    private const uint SND_FILENAME = 0x00020000;
    private const uint SND_NODEFAULT = 0x0002;

    private static void PlayFlwsSound(string outDir)
    {
        if (Environment.GetEnvironmentVariable("SPIKE_SOUND_PLAY") == "0") return;
        try
        {
            string wav = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flws_sound.wav");
            if (!File.Exists(wav)) wav = Path.Combine(outDir, "flws_sound.wav");
            if (!File.Exists(wav))
            {
                Console.WriteLine("sound wav missing: " + wav);
                return;
            }
            bool ok = PlaySound(wav, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
            Console.WriteLine("PlaySound({0}) = {1}", wav, ok);
        }
        catch (Exception e) { Console.WriteLine("PlaySound ex: " + e.Message); }
    }
}






