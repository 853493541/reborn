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

        Console.WriteLine("actorPath=" + actorPath);
        Console.WriteLine("taniPath=" + taniPath);

        var form = new Form();
        form.Text = "JX3 Engine Host \u2014 \u98CE\u6765\u5434\u5C71 (spike)";
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
        bool withSound = Environment.GetEnvironmentVariable("SPIKE_SOUND") == "1";

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
            try { sound.Init(startupPath, form.Handle.ToInt64()); }
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

        int[] shots = { 0, 2000, 4000, 7000 };
        int ci = 0;
        bool loop = Environment.GetEnvironmentVariable("SPIKE_LOOP") != "0";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!form.IsDisposed)
        {
            sound.FrameMove();
            engine.FrameMove();
            engine.Render();
            Application.DoEvents();
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
                ci = shots.Length;
                sw.Restart();
                Console.WriteLine("loop restart");
            }
            Thread.Sleep(10);
        }
        Console.WriteLine("DONE");
    }
}


