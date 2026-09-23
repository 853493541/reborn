// ActorMapSpike — M1.2 probe: animated actor on a real map, moved per frame.
// Build (from repo root):
//   csc /nologo /platform:x64 /target:exe /out:"C:\SeasunGame\MovieEditor\bin64\actor_map_spike.exe" ^
//     /r:"C:\SeasunGame\MovieEditor\bin64\MovieEngineCLR.dll" ^
//     /r:"C:\SeasunGame\MovieEditor\bin64\MovieEditorHD.exe" ^
//     /r:System.Windows.Forms.dll /r:System.Drawing.dll client\ActorMapSpike.cs
// Run with cwd = C:\SeasunGame\MovieEditor, exe in bin64.
//
// Env:
//   AM_TESTS=actor,dummy,dummyanim,dummymove,mainplayer,setmatrix   (default: all)
//   AM_MAP=<vfs jsonmap path>        default 龙门寻宝
//   AM_SPAWN=x,y,z                   optional explicit spawn (y = ground)
//   AM_AUTORUN=ms                    exit after N ms (default: 20000)
//   AM_MOVE_MODE=add|remove          dummy move strategy (default add)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MovieEngineCLR;
using MovieEditor.ActorEditor;

internal static class ActorMapSpike
{
    static string outDir;
    static Action<string> Log;

    [STAThread]
    private static void Main(string[] args)
    {
        string editorRoot = @"C:\SeasunGame\MovieEditor";
        string startupPath = Path.Combine(editorRoot, "bin64");
        string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
        string defaultMapPath =
            "data\\source\\maps\\\u9F99\u95E8\u5BFB\u5B9D\\\u9F99\u95E8\u5BFB\u5B9D.jsonmap";
        string mapPath = Environment.GetEnvironmentVariable("AM_MAP");
        if (string.IsNullOrEmpty(mapPath)) mapPath = defaultMapPath;
        string actorPath = Path.Combine(editorRoot, "source", "\u82B1\u841D\u65E0\u52A8\u4F5C.actor");
        string taniPath =
            "data\\source\\player\\f1\\\u52A8\u4F5C\\f1s07cj\u91CD\u5251\u6280\u80FD15_\u98CE\u6765\u5434\u5C71\u7EA2\u8272hd.tani";

        outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "actor_map_out");
        Directory.CreateDirectory(outDir);
        Log = delegate(string s)
        {
            try { File.AppendAllText(Path.Combine(outDir, "actor_map.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); }
            catch { }
            Console.WriteLine(s);
        };
        string tests = Environment.GetEnvironmentVariable("AM_TESTS");
        if (string.IsNullOrEmpty(tests)) tests = "actor,dummy,dummyanim,dummymove,mainplayer,setmatrix";
        string moveMode = Environment.GetEnvironmentVariable("AM_MOVE_MODE");
        if (string.IsNullOrEmpty(moveMode)) moveMode = "add";
        int autoRunMs = 20000;
        int.TryParse(Environment.GetEnvironmentVariable("AM_AUTORUN"), out autoRunMs);
        if (autoRunMs <= 0) autoRunMs = 20000;

        Log("start map=" + mapPath + " tests=" + tests + " move=" + moveMode);

        var form = new Form();
        form.Text = "JX3 Actor-on-Map Spike";
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

        try
        {
            int editorResult = editor.Init(editorRoot, err, form.Handle.ToInt64());
            Log("editor.Init result=" + editorResult);
        }
        catch (Exception e) { Log("editor.Init ex: " + e.Message); }

        var scene = new KGSceneCLR();
        if (scene == null) { Log("FATAL: no scene"); return; }
        int loadResult = scene.LoadMap(mapPath, false);
        Log("LoadMap result=" + loadResult);
        if (loadResult < 0) { Log("FATAL: LoadMap failed"); return; }
        int envr = scene.SetActiveEnvironment();
        Log("SetActiveEnvironment=" + envr);
        int rx = 0, ry = 0, rw = 0, rh = 0;
        int rr = scene.GetSceneRect(ref rx, ref ry, ref rw, ref rh);
        Log(string.Format("GetSceneRect={0} x={1} y={2} w={3} h={4}", rr, rx, ry, rw, rh));
        long winId = scene.AddOutputWindow("", panel.Handle.ToInt64(), 0); // 0 = SCENE_MAIN
        Log("winId=" + winId);

        // --- spawn placement: measure camera forward like the map host does ---
        float sx = 0f, sy = 0f, sz = 0f;
        string spawnEnv = Environment.GetEnvironmentVariable("AM_SPAWN");
        try
        {
            scene.ResetCameraPosLookAtUp();
            Pump(engine, 300);
            float ax = 0f, ay = 0f, az = 0f;
            scene.GetCameraPos(ref ax, ref ay, ref az);
            scene.SetCamareMoveState(1, 1);
            Pump(engine, 600);
            scene.SetCamareMoveState(1, 0);
            float bx = 0f, by = 0f, bz = 0f;
            scene.GetCameraPos(ref bx, ref by, ref bz);
            float dx = bx - ax, dz = bz - az;
            float dl = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dl > 1f) { dx /= dl; dz /= dl; }
            if (!string.IsNullOrEmpty(spawnEnv))
            {
                string[] sp = spawnEnv.Split(',');
                sx = float.Parse(sp[0]); sz = float.Parse(sp[2]);
                if (sp.Length > 1) float.TryParse(sp[1], out sy);
                scene.SetCameraPos(sx - dx * 700f, sy + 250f, sz - dz * 700f, false);
            }
            else
            {
                sx = bx + dx * 500f;
                sz = bz + dz * 500f;
            }
            Log(string.Format("cam start=({0:F0},{1:F0},{2:F0}) end=({3:F0},{4:F0},{5:F0}) dir=({6:F2},{7:F2}) spawn=({8:F0},{9:F0})",
                ax, ay, az, bx, by, bz, dx, dz, sx, sz));
        }
        catch (Exception e) { Log("spawn ex: " + e.Message); sx = 0f; sz = -600f; }

        // ground height via camera snap (SetCameraPos snaps Y to terrain)
        try
        {
            scene.SetCameraPos(sx, 50000f, sz, false);
            Pump(engine, 120);
            float gx = 0f, gy = 0f, gz = 0f;
            scene.GetCameraPos(ref gx, ref gy, ref gz);
            if (gy > -5000f && gy < 40000f) sy = gy;
            Log(string.Format("ground at spawn: y={0:F1}", sy));
            scene.SetCameraPos(sx - 600f, sy + 250f, sz - 600f, false);
        }
        catch (Exception e) { Log("ground ex: " + e.Message); }

        var spawnPos = new CLRfloat3(); spawnPos.x = sx; spawnPos.y = sy; spawnPos.z = sz;
        var identityRot = new CLRfloat4(); identityRot.x = 0f; identityRot.y = 0f; identityRot.z = 0f; identityRot.w = 1f;
        var unitScale = new CLRfloat3(); unitScale.x = 1f; unitScale.y = 1f; unitScale.z = 1f;

        bool runAll = tests.IndexOf("all", StringComparison.OrdinalIgnoreCase) >= 0;
        Func<string, bool> want = delegate(string t) { return runAll || tests.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0; };

        // ================= TEST: actor (KGMovieActorCLR on the map) =================
        if (want("actor"))
        {
            Log("=== TEST actor ===");
            try
            {
                var actor = new KGMovieActorCLR();
                actor.Init();
                ActorEditorCommandHelper.LoadFromFile(actor, actorPath, 0);
                long handle = actor.GetModelHandle();
                Log("actor handle=" + handle);
                long ap = scene.AppendModel(handle);
                Log("AppendModel -> " + ap);
                scene.FocusOnModel();
                var model = new KGModelCLR();
                model.AttachModel(handle);
                int pr = model.PlayAnimation(taniPath, 0, 1.0f, 0);
                Log("actor PlayAnimation -> " + pr);
                Pump(engine, 1500);
                Shot(scene, "01_actor_map");
                Pump(engine, 1500);
                Shot(scene, "01_actor_map_t3");
                try { scene.RemoveModel(handle); } catch (Exception e) { Log("RemoveModel ex: " + e.Message); }
                try { scene.ClearDummyModel(); } catch { }
            }
            catch (Exception e) { Log("actor test ex: " + e); }
        }

        // ================= TEST: dummy (static model on the map) =================
        long dummyHandle = 0;
        if (want("dummy") || want("dummyanim") || want("dummymove"))
        {
            Log("=== TEST dummy ===");
            try
            {
                dummyHandle = scene.AddDummyModel("probe_dummy", actorPath, spawnPos, identityRot, unitScale);
                Log("AddDummyModel -> " + dummyHandle);
                Pump(engine, 1200);
                Shot(scene, "02_dummy");
            }
            catch (Exception e) { Log("dummy test ex: " + e); }
        }

        // ================= TEST: dummyanim (animate the dummy handle) =================
        if (want("dummyanim") && dummyHandle != 0 && dummyHandle != -1)
        {
            Log("=== TEST dummyanim ===");
            try
            {
                var dm = new KGModelCLR();
                int ar = dm.AttachModel(dummyHandle);
                Log("dummy AttachModel -> " + ar);
                int pr = dm.PlayAnimation(taniPath, 0, 1.0f, 0);
                Log("dummy PlayAnimation -> " + pr);
                Pump(engine, 1200);
                Shot(scene, "03_dummy_anim");
                Pump(engine, 1200);
                Shot(scene, "03_dummy_anim_t2");
            }
            catch (Exception e) { Log("dummyanim ex: " + e); }
        }

        // ================= TEST: dummymove (per-frame transform) =================
        if (want("dummymove"))
        {
            Log("=== TEST dummymove mode=" + moveMode + " ===");
            try
            {
                var p = new CLRfloat3(); p.x = sx; p.y = sy; p.z = sz;
                float step = 200f; // units per second
                var sw = System.Diagnostics.Stopwatch.StartNew();
                long last = 0;
                int shot = 0;
                while (sw.ElapsedMilliseconds < 3000)
                {
                    long now = sw.ElapsedMilliseconds;
                    float t = now / 1000f;
                    p.x = sx + step * t;
                    if (moveMode == "remove") scene.RemoveDummyModel("probe_dummy");
                    long r = scene.AddDummyModel("probe_dummy", actorPath, p, identityRot, unitScale);
                    if (now - last >= 500) { last = now; Log(string.Format("move t={0:F1}s x={1:F0} AddDummyModel -> {2}", t, p.x, r)); }
                    if (shot < 3 && now >= (shot + 1) * 1000) { shot++; Shot(scene, "04_dummy_move_t" + shot); }
                    Pump(engine, 16);
                }
            }
            catch (Exception e) { Log("dummymove ex: " + e); }
        }

        // ================= TEST: mainplayer =================
        if (want("mainplayer"))
        {
            Log("=== TEST mainplayer ===");
            try
            {
                for (int body = 0; body < 4; body++)
                {
                    engine.SetMainPlayerType(body);
                    try { editor.SetMainPlayerPosVisible(1); } catch (Exception e) { Log("SetMainPlayerPosVisible ex: " + e.Message); }
                    try { editor.SyncMainPlayerPosAttachObj(0); } catch (Exception e) { Log("SyncMainPlayerPosAttachObj ex: " + e.Message); }
                    Log("SetMainPlayerType " + body + " get=" + engine.GetMainPlayerType());
                    Pump(engine, 800);
                    Shot(scene, "05_mainplayer_" + body);
                }
            }
            catch (Exception e) { Log("mainplayer ex: " + e); }
        }

        // ================= TEST: setmatrix (SetObjectProperty with a matrix) =================
        if (want("setmatrix"))
        {
            Log("=== TEST setmatrix ===");
            try
            {
                float[] m = new float[16];
                m[0] = 1f; m[5] = 1f; m[10] = 1f; m[15] = 1f;
                m[12] = sx + 300f; m[13] = sy; m[14] = sz;
                IntPtr buf = Marshal.AllocHGlobal(64);
                Marshal.Copy(m, 0, buf, 16);
                int[] ids = { (int)dummyHandle, 0, 1 };
                for (int i = 0; i < ids.Length; i++)
                {
                    try
                    {
                        int sr = editor.SetObjectProperty(ids[i], buf);
                        Log(string.Format("SetObjectProperty(id={0}) -> {1}", ids[i], sr));
                    }
                    catch (Exception e) { Log("SetObjectProperty(id=" + ids[i] + ") ex: " + e.Message); }
                }
                Marshal.FreeHGlobal(buf);
                Pump(engine, 800);
                Shot(scene, "06_setmatrix");
            }
            catch (Exception e) { Log("setmatrix ex: " + e); }
        }

        // ================= wrap up =================
        Log("tests done, idling " + autoRunMs + "ms (close window to exit)");
        var idle = System.Diagnostics.Stopwatch.StartNew();
        while (!form.IsDisposed && idle.ElapsedMilliseconds < autoRunMs)
        {
            engine.FrameMove();
            engine.Render();
            Application.DoEvents();
            Thread.Sleep(16);
        }
        Log("DONE");
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

    static void Shot(KGSceneCLR scene, string name)
    {
        try
        {
            float cx = 0f, cy = 0f, cz = 0f;
            scene.GetCameraPos(ref cx, ref cy, ref cz);
            string png = Path.Combine(outDir, name + ".png");
            scene.SetScreenShot(png, 2);
            scene.DoScreenShotImmediate();
            Log(string.Format("shot {0} exists={1} cam=({2:F0},{3:F0},{4:F0})", name, File.Exists(png), cx, cy, cz));
        }
        catch (Exception e) { Log("shot " + name + " ex: " + e.Message); }
    }
}
