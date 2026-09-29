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
//   RC_DJUMP=flip                二段跳 mode: flip (one extra jump, default) |
//                                chain (raw 轻功 J1..MaxJumpCount chain) | 0 (off)
//   RC_JUMP_SCALE=0.52            jump takeoff+gravity scale (100/192 unit calibration)
//   RC_JUMP_SCHOOL=0              settings/JumpParam.tab school row (0..22)
//   RC_CLIP_DJUMP=<vfs path>      二段跳 clip (default: f1b02yd二段跳a.ani)
//   RC_DJUMP_LOG=1                log every press / land / reject (djb lines)
//   RC_MEM_NS=<name>              engine memory namespace override
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
        // Isolation (root AGENTS.md §2, parallel feature builds): a feature exe
        // reborn_client_<slug>.exe gets its own engine memory namespace and only
        // guards against a second instance of itself; the canonical
        // reborn_client.exe keeps MovieEditor.memory and excludes the known
        // shared-namespace apps. RC_MEM_NS overrides the namespace.
        string selfExe = Path.GetFileNameWithoutExtension(
            System.Reflection.Assembly.GetExecutingAssembly().Location);
        string selfSlug = null;
        if (selfExe.StartsWith("reborn_client_", StringComparison.Ordinal))
            selfSlug = selfExe.Substring("reborn_client_".Length);
        string memNs = Env("RC_MEM_NS", "");
        if (memNs.Length == 0) memNs = selfSlug != null ? selfExe + ".memory" : "MovieEditor.memory";
        string[] known;
        if (selfSlug != null) known = new string[] { selfExe };
        else known = new string[] { "reborn_camfp", "reborn_client", "ability_sandbox", "asset_sandbox", "ability_picker" };

        // Single-instance guard: concurrent clients sharing the engine/GPU/D3D
        // device and memory namespace destabilize each other (observed: three
        // overlapping runs 18:19/18:25/18:30 with a BEX64 crash in one).
        if (Env("RC_ALLOW_MULTI", "0") != "1")
        {
            try
            {
                var me = System.Diagnostics.Process.GetCurrentProcess();
                foreach (string name in known)
                {
                    foreach (var other in System.Diagnostics.Process.GetProcessesByName(name))
                    {
                        if (other.Id == me.Id) continue;
                        System.Windows.Forms.MessageBox.Show(
                            name + " is already running (pid " + other.Id +
                            "). Close it first or set RC_ALLOW_MULTI=1.",
                            "reborn_client");
                        return;
                    }
                }
            }
            catch { }
        }
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
                if (logLines.Count > 200) logLines.RemoveRange(0, logLines.Count - 200);
            }
        };
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
                "build={0} {1} git={2} dirty={3} camFP=True flags=(ENGINESET={4},LOOKPACK={5},RATECAP={6},LOADPACE={7},FULLLOAD={8},PATCH_D6={9},PITCH_ALIGN={10},PLAYER_HIDE={11},SNAPGUARD={12},CROSS={13},HITMIN={14},WALLGATE={15},SCENERAY={16},SCENEMIN={17},BACKFACE={18},HITWIN={19})",
                exeName, exeMtime, git, dirty,
                Env("RC_CAM_ENGINESET", "1"), Env("RC_CAM_LOOKPACK", "0"),
                Env("RC_CAM_RATECAP", "0"), Env("RC_CAM_LOADPACE", "1"),
                Env("RC_FULLLOAD", "0"), Env("RC_PATCH_D6", "0"),
                Env("RC_PITCH_ALIGN", "1"), Env("RC_PLAYER_HIDE", "1"),
                Env("RC_CAM_SNAPGUARD", "0"), Env("RC_CAM_CROSS", "0"), Env("RC_CAM_HITMIN", "3.0"), Env("RC_CAM_WALLGATE", "0"), Env("RC_CAM_SCENERAY", "1"), Env("RC_CAM_SCENEMIN", "80"),
                Env("RC_CAM_BACKFACE", "1"), Env("RC_CAM_HITWIN", Env("RC_CAM_HITWINDOW", "0"))));
        }
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
        try { r2 = baselib.InitMemory(memNs); } catch (Exception e) { Log("InitMemory ex: " + e.Message); }
        try { r3 = baselib.InitPak(false); } catch (Exception e) { Log("InitPak ex: " + e.Message); }
        Log(string.Format("InitPath={0} InitMemory={1} InitPak={2} ns={3}", r1, r2, r3, memNs));

        int err = 1;
        int ok = 0;
        try { ok = engine.Init3DEngine(startupPath, startupPath, workingDir, 0, "./configHttpFile.ini", ref err); }
        catch (Exception e) { Log("Init3DEngine ex: " + e); return; }
        Log(string.Format("Init3DEngine={0} err={1}", ok, err));
        if (ok == 0) { Log("FATAL: engine init failed"); return; }
        try { Log("editor.Init result=" + editor.Init(editorRoot, err, form.Handle.ToInt64())); }
        catch (Exception e) { Log("editor.Init ex: " + e.Message); }

        var scene = new KGSceneCLR();
        // D6 mitigation: ask the engine to fully load the scene up front so the
        // lazy material/shader loader (missing build-machine DataStores -> AV)
        // is not raced while running through the map. Env-gated for A/B first.
        bool fullLoad = Env("RC_FULLLOAD", "0") == "1";
        int loadResult = scene.LoadMap(mapPath, false);
        if (fullLoad)
        {
            try
            {
                int fr = scene.SetSceneFullLoading(true);
                Log("fullload rc=" + fr + " progress=" + scene.GetLoadingProgress().ToString("F3"));
            }
            catch (Exception e) { Log("fullload ex: " + e.Message); }
        }
        Log("LoadMap result=" + loadResult);
        if (loadResult < 0) { Log("FATAL: LoadMap failed"); return; }
        scene.SetActiveEnvironment();
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
        float viewX = 0f, viewY = 0f, viewZ = 1f;   // spawn orientation (measured once)
        bool grounded = false;
        // JX3-modeled camera (engine_host_spike/CameraSystem.cs, ported)
        CameraSystem camSys = new CameraSystem();
        CameraObstruction camObst = new CameraObstruction();
        double.TryParse(Env("RC_CAM_HITWIN", Env("RC_CAM_HITWINDOW", "0")), out camObst.HitWindow);
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
            string camCfg = Path.Combine(cfgDir, "camera.json");
            if (File.Exists(camCfg))
            {
                try { camSys.LoadConfig(camCfg); Log("camera config: " + camCfg); }
                catch (Exception e) { Log("camera config ex: " + e.Message); }
            }
            camSys.SwitchMode(CameraSystem.MODE_CHARACTER);
            cameraSettings = CameraSettings.Load(
                editorRoot, mapPath, cfgDir, Log);
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
            float va = VideoSettings.ViewAngleFactor(editorRoot, cameraSettings.WidAngleDeg, Log);
            float vaTest;
            if (float.TryParse(Env("RC_VIEW_ANGLE", ""), out vaTest) && vaTest > 0f)
            {
                va = vaTest;
                Log("view angle factor test override=" + va);
            }
            scene.SetViewAngleFactor(va);
            Log("view angle factor applied=" + va);
        }
        catch (Exception e) { Log("view angle: " + e.Message); }
        long handle = 0, attachedHandle = -999;
        var model = new KGModelCLR();
        string curClip = null;
        float curYaw = 0f;
        float lastModelX = float.MaxValue, lastModelY = float.MaxValue, lastModelZ = float.MaxValue, lastModelYaw = float.MaxValue;

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
        bool wSprint = false;    // double-tap W and hold -> sprint (8.8 尺/s)
        long lastWUp = 0, lastWDown = 0;
        bool demo = Env("RC_DEMO", "0") == "1", demoJumped = false, demoJumped2 = false, demoTurned = false, demoSkilled = false;
        bool demoCollide = Env("RC_DEMO_COLLIDE", "0") == "1", demoTeleported = false;
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
            // S7: a press that never moved never locked the cursor - that press
            // was a click and the camera was not rotated.
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
        form.MouseWheel += wheel;
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) unlockMouse();
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
        // "16帧等于1秒", UNIT_SCALE...md §2): walk 6 / run 20 u/frame -> 96 / 320
        // u/s. Cross-check: the official UI shows 跑步速度 5 尺/秒 and
        // 20 u/frame * 16 fps = 320 u/s = 5 * 64 u (1 尺 = 64 u). Host controls:
        // default RUN, "/" toggles WALK, hold Shift for a 10x testing speed.
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
        float pSpeed = 96f, pRun = 320f;
        float pSprint = 8.8f * 64f;   // double-tap W hold: 8.8 尺/s = 563.2 u/s
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
        long lastMs = 0, lastLog = 0, lastHud = 0, skillUntil = 0, lastCamMeasure = 0, lastCamLog = 0, lastOrbitMs = 0, lastPostLog = 0;
        double[] camOffSmooth = new double[3];
        bool camOffInit = false;
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
        if (Env("RC_PITCH_ALIGN", "1") == "1" && Env("RC_CAM_ENGINESET", "1") == "0")
        {
            Log(string.Format("camera aim align: yaw={0:F3} modelPitch={1:F3} aimPitch={2:F3}",
                camSys.Yaw, camSys.Pitch, geometricAimPitch()));
            alignAim();
        }
        // the game keeps the follow distance at the row value; MaxCameraDistance
        // is only the cap the wheel can zoom out to (starting at the cap made
        // the camera pump when walls passed in/out of range)
        camSys.Distance = camSys.ClampDistanceUnits(camSys.Distance);

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

            if (camZoomSeq)
            {
                // test input only (no camera behavior): wheel steps out x3 then
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
                // camera shake on the cast (host default; per-skill shake rows
                // are data-gated)
                camShake.Start(2.0, 0.5, 0.8, 3);
                Log("skill cast");
            }

            // input -> direction. The game recomputes camera-relative movement
            // every frame (MOVEFORWARD = camera forward; A/D strafe), so rotating
            // the camera steers the run (docs/controls/JX3_MOVEMENT_CONTROLS.md §2;
            // RMB = CAMERAORSELECTORMOVESTICKY rotates camera + character).
            float inX = 0f, inZ = 0f;
            float rX = hz, rZ = -hx;
            if (pW) { inX += hx; inZ += hz; }
            if (pS) { inX -= hx; inZ -= hz; }
            if (pA) { inX -= rX; inZ -= rZ; }
            if (pD) { inX += rX; inZ += rZ; }
            float inLen = (float)Math.Sqrt(inX * inX + inZ * inZ);
            if (inLen > 1e-4f) { inX /= inLen; inZ /= inLen; }
            float dirX = inX, dirZ = inZ;
            if (demoCollide) { dirX = demoDirX; dirZ = demoDirZ; }
            float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            bool moving = len > 0.01f && skillUntil <= now;
            // character yaw turn rate (rad/s): the game's per-frame turn step
            // (+0x48) is a server sync byte and not decoded; the host uses the
            // camera row RotationSpeed fallback pi rad/s (same as the RMB turn)
            float charTurnRate = (float)camSys.Row.F("RotationSpeed", 0.0);
            if (charTurnRate < 1f) charTurnRate = (float)Math.PI;

            // horizontal move + slope blocking (map-host rules)
            float ground = sampler != null ? sampler.Sample(px, pz) : py;
            bool blocked = false;
            if (moving)
            {
                float sp = (shiftDown ? pRun * 10f
                            : walkMode ? pSpeed
                            : wSprint ? pSprint
                            : pRun) / len;
                float ux = dirX / len, uz = dirZ / len;
                // turn model (KCharacter::RunTo 0x14031B780; docs/movement/
                // JX3_CHARACTER_MOVEMENT_RESEARCH.md §3.5): heading = travel
                // direction; facing turns toward it at the turn rate; a turn
                // > 112.5 deg (0x50/0x100 of the circle) halves movement speed
                // and the turn step that frame.
                float heading = (float)Math.Atan2(ux, uz);
                float dYaw = heading - curYaw;
                while (dYaw > Math.PI) dYaw -= 2f * (float)Math.PI;
                while (dYaw < -Math.PI) dYaw += 2f * (float)Math.PI;
                bool hardTurn = Math.Abs(dYaw) > 2.0071f;
                if (hardTurn) sp *= 0.5f;
                float turnStep = charTurnRate * dt * (hardTurn ? 0.5f : 1f);
                if (Math.Abs(dYaw) <= turnStep) curYaw = heading;
                else curYaw += Math.Sign(dYaw) * turnStep;
                float step = sp * dt;
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
            }

            // RMB (CAMERAORSELECTORMOVESTICKY) also turns the character to the
            // camera direction; LMB drag rotates the camera only. Rate-limited
            // (S6) instead of snapping the yaw in one frame.
            if (rmbDown)
            {
                float targetYaw = (float)Math.Atan2(-Math.Cos(camSys.Yaw), -Math.Sin(camSys.Yaw));
                float d = targetYaw - curYaw;
                while (d > Math.PI) d -= 2f * (float)Math.PI;
                while (d < -Math.PI) d += 2f * (float)Math.PI;
                float step = charTurnRate * (float)dt;
                if (Math.Abs(d) <= step) curYaw = targetYaw;
                else curYaw += Math.Sign(d) * step;
            }

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

            // jump + 二段跳: press 1 = J0; in the air press 2 = flip mode (one
            // extra normal-strength jump) or chain mode (raw J1.. table rows)
            if (jumpPressed)
            {
                jumpPressed = false;
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
                    if (djumpLog) Log(string.Format(
                        "djb press n={0} mode={1} triple={2},{3},{4} vy={5:F0} g={6:F0} pos={7:F0},{8:F0},{9:F0}",
                        jumpCount, djumpMode, trip[0], trip[1], trip[2], vy, curJumpGravity, px, py, pz));
                }
                else if (djumpLog) Log(string.Format(
                    "djb reject n={0} max={1} grounded={2} mode={3}",
                    nextJump, maxJump, grounded ? 1 : 0, djumpMode));
            }

            // gravity (per-jump magnitude; J0 11 u/f2 -> 2475 u/s2 = the old constant)
            if (!grounded)
            {
                float vyBefore = vy;
                vy -= curJumpGravity * dt;
                py += vy * dt;
                // apex sample: the model transform must have followed the physics
                // height (modelY ~ py); a stale modelY is the standing-jump stutter
                if (djumpLog && vyBefore > 0f && vy <= 0f) Log(string.Format(
                    "djb apex n={0} py={1:F0} modelY={2:F0}", jumpCount, py, lastModelY));
                if (py <= ground)
                {
                    py = ground;
                    float impact = vy;
                    if (vy < 0f) vy = 0f;
                    grounded = true;
                    if (djumpLog && jumpCount > 0) Log(string.Format(
                        "djb land n={0} pos={1:F0},{2:F0},{3:F0} vy={4:F0}",
                        jumpCount, px, py, pz, impact));
                    jumpCount = 0;
                }
            }
            else jumpCount = 0;

            // animation state
            if (skillUntil > now) { /* skill clip playing */ }
            else if (!grounded) setClip(vy > 0f ? (jumpCount > 1 && clipDJump.Length > 0 ? clipDJump : clipJump) : clipFall);
            else if (moving) setClip(walkMode ? clipWalk : clipRun);
            else setClip(clipIdle);

            // model update (only when changed; keeps animation alive).
            // Y must be part of the gate: a standing jump changes py only, and
            // without it the model stays at the takeoff height (stutter/"stuck
            // in the middle"); moving jumps updated via X/Z and looked fine.
            if (Math.Abs(px - lastModelX) > 0.5f || Math.Abs(py - lastModelY) > 0.5f ||
                Math.Abs(pz - lastModelZ) > 0.5f ||
                Math.Abs(curYaw - lastModelYaw) > 0.01f)
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
                // game per-axis dead-zone + SmoothTime (SetCharacterCameraPosition
                // @ 0x180B0F2BA..0x180B0F3A6, state +0x1B8/0x1BC/0x1C0; spec
                // docs/camera/FIX_SPEC.md, reference tools/netcode/reference/
                // camera_model.py): current += delta*dt/SmoothTime per axis, snap
                // when the step covers the delta. The host engine has no camera
                // smoothing of its own, so without this the orbit is raw.
                if (!camOffInit)
                {
                    camOffSmooth[0] = camOff[0]; camOffSmooth[1] = camOff[1];
                    camOffSmooth[2] = camOff[2]; camOffInit = true;
                }
                bool doSmooth = (cameraSettings == null || cameraSettings.CameraSmoothing) &&
                                Env("RC_CAM_NOSMOOTH", "0") != "1";
                double stime = Math.Max(camSys.Row.F("SmoothTime", 0.06), 1e-3);
                for (int i = 0; i < 3; i++)
                {
                    double d3 = camOff[i] - camOffSmooth[i];
                    if (doSmooth && Math.Abs(d3) > 1e-6 &&
                        Math.Abs(d3) > Math.Abs(d3) * dt / stime)
                        camOffSmooth[i] += d3 * dt / stime;
                    else
                        camOffSmooth[i] = camOff[i];
                }
                camOff[0] = camOffSmooth[0];
                camOff[1] = camOffSmooth[1];
                camOff[2] = camOffSmooth[2];
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
                        float th = engineRay.RayTerrain(px2, py2, pz2, qx2, qy2, qz2);
                        if (th > 0f && (h <= 0f || th < h)) h = th;
                        float sh = sceneRayCam ? engineRay.RayScene(px2, py2, pz2, qx2, qy2, qz2) : -1f;
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
                            Log(string.Format("obstdbg probe{0} off=({1:F0},{2:F0},{3:F0}) bake={4:F1}(inst={8},tri={9}) terr={5:F1} scene={6:F1} h={7:F1}",
                                p, ox2, oy2, oz2, bh, th, sh, h, col.LastInst, col.LastTri));
                        if (penDbg)
                            penCur.Append(string.Format(" p{0} off=({1:F0},{2:F0},{3:F0}) bake={4:F0}(i={5},t={6},blk={7},fol={8}) terr={9:F0} scene={10:F0} h={11:F0}",
                                p, ox2, oy2, oz2, bh, bInst, bTri, bBlk ? 1 : 0, bFol ? 1 : 0, th, sh, h));
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
                hitDist = camObst.Stabilize(dt, hitDist);
                double camLen = camObst.Update(dt, offLen, hitDist);
                dbgHit = hitDist; dbgLen = camLen; dbgObst = camObst.Obstructed;
                dbgEffDist = dist; dbgSrc = hitSrc;
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

                double camX = ax2 + rSm[0];
                double camY = ay2 + rSm[1];
                double camZ = az2 + rSm[2];
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
                if (wallGate && engineRay.Available)
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
                string state = skillUntil > now ? "SKILL" : !grounded ? ((vy > 0f ? "JUMP" : "FALL") + (jumpCount > 1 ? jumpCount.ToString() : ""))
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
                Log(string.Format("t={0}s fps={1} pos=({2:F0},{3:F0},{4:F0}) vy={5:F0} grounded={6} blocked={7} hits={8} colCalls={9} colBlocked={10} spd={13:F0}u/s({14}) yaw={15:F2} dir=({16:F2},{17:F2}){11} clip={12}",
                    now / 1000, fps, px, py, pz, vy, grounded, blocked, blockedEvents,
                    colCalls, colBlockedCalls, nearInfo,
                    curClip == null ? "-" : Path.GetFileName(curClip),
                    curSpd, moveMode, curYaw, dirX, dirZ));
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
