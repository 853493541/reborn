// Smoke test for CameraSystem.cs - port of the Python reference checks
// (tools/netcode/reference/camera_model.py, 13/13). Run: camera_smoke.exe
//
// Uses UnitsPerMeter = 1 so the numbers match the reference exactly.

using System;

internal static class CameraSmoke
{
    static int _fail;

    static void Check(string name, bool cond, string detail = "")
    {
        Console.WriteLine((cond ? "PASS" : "FAIL") + ": " + name +
                          (detail.Length > 0 ? " - " + detail : ""));
        if (!cond) _fail++;
    }

    static void Main()
    {
        string tpl = Environment.GetEnvironmentVariable("CAMERA_SAVE_TEMPLATE");
        if (!string.IsNullOrEmpty(tpl))
        {
            var c = new CameraSystem();
            c.SaveTemplate(tpl);
            Console.WriteLine("template written: " + tpl);
            return;
        }
        double DEG = CameraSystem.DEG;
        var cam = new CameraSystem();
        cam.UnitsPerMeter = 1.0;
        cam.SwitchMode(CameraSystem.MODE_CHARACTER);
        cam.Pitch = cam.Row.F("InitCameraPitch", -20.0 * DEG);
        cam.Distance = cam.Row.F("InitCameraDistance", 6.0);

        double[] anchor = { 0, 0, 0 };
        for (int i = 0; i < 60; i++) cam.Update(1.0 / 60.0, anchor);
        double p = -20.0 * DEG;
        double ex = Math.Cos(p) * 6.0, ey = Math.Sin(p) * 6.0 + 2.0;
        Check("camera settles behind player",
              Math.Abs(cam.Pos[0] - ex) < 0.01 && Math.Abs(cam.Pos[2]) < 0.01 &&
              Math.Abs(cam.Pos[1] - ey) < 0.01,
              string.Format("pos=({0:F2},{1:F2},{2:F2})", cam.Pos[0], cam.Pos[1], cam.Pos[2]));

        // Part 1 regression (CAMERA_FIX_SPEC.md): pitch rotates a constant-length
        // JX3 sphere offset; it must not scale the distance with tan(pitch).
        var cam3 = new CameraSystem();
        cam3.UnitsPerMeter = 1.0;
        cam3.SwitchMode(CameraSystem.MODE_CHARACTER);
        cam3.Distance = 6.0;
        double[] pitches = { -40.0 * DEG, -20.0 * DEG, 0.0, 25.0 * DEG, 40.0 * DEG };
        double worst = 0.0;
        foreach (double pv in pitches)
        {
            // pin the idle/move pitch pull to the test pitch (Update drives Pitch
            // toward these rows every frame at PitchRate)
            cam3.Rows[CameraSystem.MODE_CHARACTER].Set("CameraMovePitchAdjustPitch", pv);
            cam3.Rows[CameraSystem.MODE_CHARACTER].Set("CameraMovePitchApplyAngle", pv);
            cam3.Pitch = pv;
            for (int i = 0; i < 90; i++) cam3.Update(1.0 / 60.0, anchor);
            double hhx = cam3.Pos[0] - anchor[0], hhz = cam3.Pos[2] - anchor[2];
            double horiz = Math.Sqrt(hhx * hhx + hhz * hhz);
            double err = Math.Max(Math.Abs(horiz - Math.Cos(pv) * 6.0),
                                  Math.Abs(cam3.Pos[1] - (Math.Sin(pv) * 6.0 + 2.0)));
            if (err > worst) worst = err;
        }
        Check("pitch drag keeps constant-length orbit", worst < 0.02,
              string.Format("worst err={0:F4}", worst));

        double[] offD = new double[3];
        CameraSystem.DesiredOffset(0.0, 90.0 * DEG, 6.0, 2.0, offD);
        Check("desired offset vertical at +90deg pitch",
              Math.Abs(offD[0]) < 1e-9 && Math.Abs(offD[1] - 8.0) < 1e-9 && Math.Abs(offD[2]) < 1e-9,
              string.Format("off=({0:F3},{1:F3},{2:F3})", offD[0], offD[1], offD[2]));

        cam.Mouse(0.2, 0.0);
        cam.Update(1.0 / 60.0, anchor);
        Check("mouse yaw orbits camera", Math.Abs(cam.Yaw + 0.2) < 1e-3,
              string.Format("yaw={0:F3}", cam.Yaw));

        for (int i = 0; i < 120; i++) cam.Update(1.0 / 60.0, anchor, true);
        Check("moving applies pitch",
              cam.MovePitchApplied &&
              Math.Abs(cam.Pitch - cam.Row.F("CameraMovePitchApplyAngle", -12 * DEG)) < 0.02,
              string.Format("pitch={0:F3}", cam.Pitch));

        cam.SwitchMode(CameraSystem.MODE_SPRINT);
        cam.SprintSpeed = 10.0;
        for (int i = 0; i < 120; i++) cam.Update(1.0 / 60.0, anchor, true);
        Check("sprint pulls camera back", cam.Distance > 7.5,
              string.Format("dist={0:F2}", cam.Distance));

        cam.SwitchMode(CameraSystem.MODE_CARRIER);
        cam.Update(1.0 / 60.0, anchor);
        Check("mode rows switch params",
              cam.Row.B("ForbidStrafe", false) && cam.Row.B("TurnCameraYawToObjectYaw", false));

        cam.SetMaxDistance(12.0);
        Check("script hook set_max_distance",
              Math.Abs(cam.Rows[CameraSystem.MODE_CHARACTER].F("TargetDistance", 0) - 12.0) < 1e-9);
        cam.SetFollowMode(CameraSystem.MODE_NPC_DIALOG);
        Check("follow mode event log",
              cam.Events[cam.Events.Count - 1] == "enter:" + CameraSystem.MODE_NPC_DIALOG,
              cam.Events[cam.Events.Count - 1]);

        cam.SetFollowAction(new double[] { 0, 1, 0 });
        cam.Update(1.0 / 60.0, anchor);
        Check("lock/follow-action aims at target",
              cam.Look[0] == 0 && cam.Look[1] == 1 && cam.Look[2] == 0);

        var track = new TrackCamera();
        double[] tgt = { 10, 5, -10 };
        for (int i = 0; i < 600; i++) track.Update(1.0 / 120.0, tgt);
        Check("track camera spring converges",
              Math.Abs(track.Pos[0] - tgt[0]) < 0.01 && Math.Abs(track.Pos[1] - tgt[1]) < 0.01 &&
              Math.Abs(track.Pos[2] - tgt[2]) < 0.01,
              string.Format("pos=({0:F2},{1:F2},{2:F2})", track.Pos[0], track.Pos[1], track.Pos[2]));

        var shake = new CameraShake();
        shake.Start(2.0, 0.5, 0.8, 3);
        double maxoff = 0;
        for (int i = 0; i < 10; i++)
        {
            shake.Update(1.0 / 60.0);
            double d = Math.Sqrt(shake.Offset[0] * shake.Offset[0] + shake.Offset[1] * shake.Offset[1] +
                                 shake.Offset[2] * shake.Offset[2]);
            if (d > maxoff) maxoff = d;
        }
        Check("camera shake offsets bounded by amplitude", maxoff <= 1.6,
              string.Format("maxoff={0:F2}", maxoff));
        for (int i = 0; i < 200; i++) shake.Update(1.0 / 60.0);
        Check("camera shake ends after max cycles",
              shake.Mode == 0 && shake.Amp == 0.0 && shake.Offset[0] == 0 && shake.Offset[1] == 0 &&
              shake.Offset[2] == 0);

        var cam2 = new CameraSystem();
        cam2.UnitsPerMeter = 1.0;
        cam2.SwitchMode(CameraSystem.MODE_CHARACTER);
        cam2.Pitch = cam2.Row.F("InitCameraPitch", -20.0 * DEG);
        cam2.Distance = cam2.Row.F("InitCameraDistance", 6.0);
        for (int i = 0; i < 60; i++) cam2.Update(1.0 / 60.0, anchor);
        Func<double[], double[], double?> obst = delegate(double[] a, double[] pos)
        {
            double dx = pos[0] - a[0], dy = pos[1] - a[1], dz = pos[2] - a[2];
            double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (d > 2.0) return (double?)2.0;
            return null;
        };
        cam2.Update(1.0 / 60.0, anchor, false, 0, 0, obst);
        cam2.Update(1.0 / 60.0, anchor, false, 0, 0, obst);
        double dd = Math.Sqrt(cam2.Pos[0] * cam2.Pos[0] + cam2.Pos[1] * cam2.Pos[1] + cam2.Pos[2] * cam2.Pos[2]);
        Check("obstruction pulls camera in", dd >= 1.6 && dd <= 1.9, string.Format("dist={0:F2}", dd));
        cam2.Update(1.0 / 60.0, anchor);
        dd = Math.Sqrt(cam2.Pos[0] * cam2.Pos[0] + cam2.Pos[1] * cam2.Pos[1] + cam2.Pos[2] * cam2.Pos[2]);
        Check("camera recovers when clear", dd > 3.0, string.Format("dist={0:F2}", dd));

        // native wall obstruction state machine (18 u clearance, 50/100
        // hysteresis, spring return) - docs/CAMERA_WALL_OBSTRUCTION.md
        Check("shared distance clamp helper (S5)",
              Math.Abs(cam.ClampDistanceUnits(50.0) - 100.0) < 1e-9 &&
              Math.Abs(cam.ClampDistanceUnits(5000.0) - 2000.0) < 1e-9 &&
              Math.Abs(cam.ClampDistanceUnits(600.0) - 600.0) < 1e-9,
              string.Format("{0:F0}/{1:F0}/{2:F0}", cam.ClampDistanceUnits(50.0),
                  cam.ClampDistanceUnits(5000.0), cam.ClampDistanceUnits(600.0)));

        var obstA = new CameraObstruction();
        double od = obstA.Update(1.0 / 60.0, 600.0, 500.0);
        Check("obstruction pulls in to hit-18u", obstA.Obstructed && Math.Abs(od - 482.0) < 1e-6,
              string.Format("dist={0:F1}", od));
        var obstB = new CameraObstruction();
        double od2 = obstB.Update(1.0 / 60.0, 600.0, 570.0);
        Check("shallow hit ignored (50 u hysteresis)", !obstB.Obstructed && Math.Abs(od2 - 600.0) < 1e-6,
              string.Format("dist={0:F1}", od2));
        double minReturn = obstA.Distance;
        for (int i = 0; i < 900; i++)
        {
            double before = obstA.Distance;
            od = obstA.Update(1.0 / 60.0, 600.0, -1.0);
            if (obstA.Distance < before - 0.5) minReturn = Math.Min(minReturn, obstA.Distance);
        }
        Check("camera springs back when clear", !obstA.Obstructed && Math.Abs(od - 600.0) < 1.0,
              string.Format("dist={0:F2}", od));
        Check("spring return is monotonic (no runaway zoom-in)",
              minReturn >= 482.0 - 0.5, string.Format("min={0:F1}", minReturn));
        var obstC = new CameraObstruction();
        double od3 = obstC.Update(1.0 / 60.0, 600.0, 10.0);
        Check("obstruction respects min distance", Math.Abs(od3 - 5.0) < 1e-6,
              string.Format("dist={0:F1}", od3));
        var obstH = new CameraObstruction();
        obstH.Update(1.0 / 60.0, 600.0, 500.0);           // obstructed, pulled to 482
        for (int i = 0; i < 900; i++) obstH.Update(1.0 / 60.0, 600.0, 620.0);
        Check("obstructed-side 100 u hysteresis keeps the state",
              obstH.Obstructed && Math.Abs(obstH.Distance - 600.0) < 1.0,
              string.Format("dist={0:F1} obst={1}", obstH.Distance, obstH.Obstructed));
        for (int i = 0; i < 600; i++) obstH.Update(1.0 / 60.0, 600.0, -1.0);
        Check("releases once the ray is fully clear",
              !obstH.Obstructed && Math.Abs(obstH.Distance - 600.0) < 1.0,
              string.Format("dist={0:F1}", obstH.Distance));

        var obstD = new CameraObstruction();
        obstD.Update(1.0 / 60.0, 600.0, 50.0);            // pulled to 32 u
        double minOut = obstD.Distance;
        for (int i = 0; i < 900; i++)
        {
            double before = obstD.Distance;
            obstD.Update(1.0 / 60.0, 600.0, 500.0);       // wall recedes
            if (obstD.Distance < before - 0.5) minOut = Math.Min(minOut, obstD.Distance);
        }
        Check("camera springs out along a receding wall (stays in front)",
              obstD.Obstructed && Math.Abs(obstD.Distance - 482.0) < 1.0 && minOut >= 32.0 - 0.5,
              string.Format("dist={0:F1} min={1:F1}", obstD.Distance, minOut));

        Console.WriteLine(_fail == 0 ? "ALL PASS" : (_fail + " FAILED"));
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
