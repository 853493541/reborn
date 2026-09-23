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
        float yawOffset = 0f;
        float.TryParse(Env("RC_YAW_OFFSET", "0"), out yawOffset);
        float scale = 1f;
        float.TryParse(Env("RC_SCALE", "1"), out scale);
        long skillMs = 8000;
        long.TryParse(Env("RC_SKILL_MS", "8000"), out skillMs);
        long autoRunMs = 0;
        long.TryParse(Env("RC_AUTORUN", "0"), out autoRunMs);

        outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reborn_out");
        Directory.CreateDirectory(outDir);
        Log = delegate(string s)
        {
            try { File.AppendAllText(Path.Combine(outDir, "reborn.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n"); }
            catch { }
            Console.WriteLine(s);
        };
        Log("start map=" + mapPath);

        var form = new Form();
        form.Text = "JX3 Reborn";
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
        hud.Location = new System.Drawing.Point(10, 10);
        hud.Text = "loading...";
        panel.Controls.Add(hud);
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
        float viewX = 0f, viewZ = 1f;       // camera view direction (horizontal)
        bool grounded = false;
        float followDist = 800f;
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
                for (int i = 0; i < 3; i++) { engine.FrameMove(); engine.Render(); Application.DoEvents(); }
                scene.SetCamareMoveState(1, 0);
                float bx = 0f, by = 0f, bz = 0f;
                scene.GetCameraPos(ref bx, ref by, ref bz);
                float dx = bx - ax, dz = bz - az;
                float dl = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dl > 0.5f) { viewX = dx / dl; viewZ = dz / dl; }
            }
            catch { }
        };

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
                float cx = 0f, cy = 0f, cz = 0f;
                scene.GetCameraPos(ref cx, ref cy, ref cz);
                px = cx + viewX * 500f;
                pz = cz + viewZ * 500f;
            }
            py = sampler != null ? sampler.Sample(px, pz) : 0f;
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
        try { scene.SetCameraPos(px - viewX * followDist, py + 60f, pz - viewZ * followDist, false); } catch { }

        // ---------------- input ----------------
        bool pW = false, pA = false, pS = false, pD = false, shiftDown = false;
        bool jumpPressed = false, skillPressed = false, spaceDown = false, oneDown = false;
        bool demo = Env("RC_DEMO", "0") == "1", demoJumped = false, demoSkilled = false;
        bool demoCollide = Env("RC_DEMO_COLLIDE", "0") == "1", demoTeleported = false;
        bool demoTeleport = Env("RC_COL_TELEPORT", "0") == "1";
        float demoDirX = 0f, demoDirZ = 0f;
        {
            string[] dd = Env("RC_DEMO_DIR", "0,1").Split(',');
            if (dd.Length >= 2) { float.TryParse(dd[0], out demoDirX); float.TryParse(dd[1], out demoDirZ); }
        }
        bool cDown = false, teleportToStructure = false;
        int dragAction = 0;
        var pending = new System.Collections.Generic.Queue<int[]>();
        Func<int, int, int> makeLParam = delegate(int x, int y) { return ((y & 0xFFFF) << 16) | (x & 0xFFFF); };
        panel.MouseDown += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragAction = 1;
                pending.Enqueue(new int[] { 30, 1, e.X, e.Y });
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
            }
        };
        MouseEventHandler wheel = delegate(object s, MouseEventArgs e)
        {
            followDist -= (e.Delta > 0 ? 1f : -1f) * 150f;
            if (followDist < 300f) followDist = 300f;
            if (followDist > 3000f) followDist = 3000f;
        };
        panel.MouseWheel += wheel;
        form.MouseWheel += wheel;
        form.KeyPreview = true;
        form.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) pW = true;
            else if (e.KeyCode == Keys.S) pS = true;
            else if (e.KeyCode == Keys.A) pA = true;
            else if (e.KeyCode == Keys.D) pD = true;
            else if (e.KeyCode == Keys.ShiftKey) shiftDown = true;
            else if (e.KeyCode == Keys.Space && !spaceDown) { spaceDown = true; jumpPressed = true; }
            else if (e.KeyCode == Keys.D1 && !oneDown) { oneDown = true; skillPressed = true; }
            else if (e.KeyCode == Keys.C && !cDown) { cDown = true; teleportToStructure = true; }
        };
        form.KeyUp += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) pW = false;
            else if (e.KeyCode == Keys.S) pS = false;
            else if (e.KeyCode == Keys.A) pA = false;
            else if (e.KeyCode == Keys.D) pD = false;
            else if (e.KeyCode == Keys.ShiftKey) shiftDown = false;
            else if (e.KeyCode == Keys.Space) spaceDown = false;
            else if (e.KeyCode == Keys.D1) oneDown = false;
            else if (e.KeyCode == Keys.C) cDown = false;
        };
        panel.Focus();

        // ---------------- main loop ----------------
        // real game values (settings/JumpParam.tab + number.krl.txt)
        float pGravity = -1289f, pJumpV = 703f, pSpeed = 200f, pRun = 667f;
        float playerRadius = 25f, playerHeight = 170f;
        float.TryParse(Env("RC_RADIUS", "25"), out playerRadius);
        float.TryParse(Env("RC_HEIGHT", "170"), out playerHeight);
        int blockedEvents = 0;
        long colCalls = 0, colBlockedCalls = 0;
        bool colDebug = Env("RC_COL_DEBUG", "0") == "1";
        long lastMs = 0, lastMeasure = 0, lastLog = 0, lastHud = 0, skillUntil = 0;
        long frames = 0, fpsAt = 0, fps = 0;
        int shotIdx = 0;
        long[] shots = ParseShots(Env("RC_SHOTS", "3000,8000,15000,30000"));
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

            while (pending.Count > 0)
            {
                int[] cmd = pending.Dequeue();
                try
                {
                    if (cmd[0] == 31) scene.ExecAction(31, 1, cmd[1], 1);
                    else scene.ExecAction(cmd[0], cmd[1], 0, makeLParam(cmd[2], cmd[3]));
                }
                catch (Exception e) { Log("action ex: " + e.Message); }
            }

            if (demo)
            {
                pW = now >= 2000 && now < 12000;
                shiftDown = now >= 7000 && now < 12000;
                pA = now >= 14000 && now < 18000;
                if (now >= 12500 && !demoJumped) { demoJumped = true; jumpPressed = true; }
                if (now >= 18500 && !demoSkilled) { demoSkilled = true; skillPressed = true; }
            }
            if (demoCollide)
            {
                if (demoTeleport && now >= 2000 && !demoTeleported) { demoTeleported = true; teleportToStructure = true; }
                pW = now >= 3000 && now < 9000;
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
                        viewX = fx; viewZ = fz;
                        demoDirX = fx; demoDirZ = fz;
                        curYaw = (float)Math.Atan2(fx, fz);
                        try { scene.SetCameraPos(px - fx * followDist, py + 60f, pz - fz * followDist, false); } catch { }
                        Log(string.Format("teleport to structure: {0:F0}u away, at ({1:F0},{2:F0},{3:F0})", d, px, py, pz));
                    }
                    else Log("no solid structure found");
                }
            }

            if (now - lastMeasure >= 500) { lastMeasure = now; measureView(); }
            float hl = (float)Math.Sqrt(viewX * viewX + viewZ * viewZ);
            float hx = hl > 1e-4f ? viewX / hl : 0f;
            float hz = hl > 1e-4f ? viewZ / hl : 1f;

            // skill
            if (skillPressed)
            {
                skillPressed = false;
                skillUntil = now + skillMs;
                curClip = null;
                setClip(clipSkill);
                Log("skill cast");
            }

            // input -> direction
            float dirX = 0f, dirZ = 0f;
            float rX = hz, rZ = -hx;
            if (pW) { dirX += hx; dirZ += hz; }
            if (pS) { dirX -= hx; dirZ -= hz; }
            if (pA) { dirX -= rX; dirZ -= rZ; }
            if (pD) { dirX += rX; dirZ += rZ; }
            if (demoCollide) { dirX = demoDirX; dirZ = demoDirZ; }
            float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            bool moving = len > 0.01f && skillUntil <= now;

            // horizontal move + slope blocking (map-host rules)
            float ground = sampler != null ? sampler.Sample(px, pz) : py;
            bool blocked = false;
            if (moving)
            {
                float sp = (shiftDown ? pRun : pSpeed) / len;
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
            else if (moving) setClip(shiftDown ? clipRun : clipWalk);
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

            // follow camera
            try
            {
                float baseY = py + 50f;
                float useDist = followDist;
                if (sampler != null)
                {
                    for (int i = 1; i <= 16; i++)
                    {
                        float t = i / 16f;
                        float sx = px - hx * followDist * t;
                        float sz = pz - hz * followDist * t;
                        if (sampler.Sample(sx, sz) + 10f > baseY) { useDist = followDist * ((i - 1) / 16f); break; }
                    }
                }
                if (useDist < 80f) useDist = 80f;
                float camX = px - hx * useDist;
                float camZ = pz - hz * useDist;
                float camY = baseY;
                float camGround = sampler != null ? sampler.Sample(camX, camZ) + 30f : camY;
                if (camY < camGround) camY = camGround;
                if (camY > py + 120f) camY = py + 120f;
                scene.SetCameraPos(camX, camY, camZ, false);
            }
            catch { }

            engine.FrameMove();
            engine.Render();
            Application.DoEvents();

            if (now - lastHud >= 250)
            {
                lastHud = now;
                string state = skillUntil > now ? "SKILL" : !grounded ? (vy > 0f ? "JUMP" : "FALL")
                             : moving ? (shiftDown ? "RUN" : "WALK") : "IDLE";
                hud.Text = string.Format(
                    "JX3 Reborn M1\nfps {0}\npos {1:F0},{2:F0},{3:F0}\nstate {4}{5} hits {6}\nclip {7}\nWASD move | Shift run | Space jump | 1 skill | C teleport | L-drag orbit | wheel zoom",
                    fps, px, py, pz, state, blocked ? " (blocked)" : "", blockedEvents,
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
