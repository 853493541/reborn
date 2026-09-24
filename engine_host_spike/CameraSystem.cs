// JX3-modeled camera system for the engine host.
//
// Port of the verified model in docs/netcode/REBORN_CAMERA_SPEC.md and the
// Python reference tools/netcode/reference/camera_model.py:
//  - per-mode parameter rows (character/sprint/carrier/air_combat/npc_dialog/god)
//  - yaw/pitch mouse camera with per-frame delta clamps
//  - anchor + rotated offset (JX3-exact axis convention, SetCharacterCameraPosition)
//  - exponential smoothing with dead-zone snap (current += delta*dt/SmoothTime)
//  - movement-reactive pitch (rate-limited pi/3000 rad/ms = 60 deg/s) and
//    yaw-follow while turning
//  - sprint pull-back, follow-action (lock target), obstruction pull-in
//  - camera shake (cos/decay burst + rand jitter) and cinematic track spring
//
// Distances in the JX3 rows are meters; the host world uses units, so all
// distance/height params are scaled by UnitsPerMeter (default 192).

using System;
using System.Collections.Generic;

public sealed class CameraParams
{
    readonly Dictionary<string, object> _v = new Dictionary<string, object>();

    public CameraParams() { }
    public CameraParams(CameraParams other)
    {
        foreach (var kv in other._v) _v[kv.Key] = kv.Value;
    }

    public void Set(string key, double v) { _v[key] = v; }
    public void Set(string key, bool v) { _v[key] = v; }

    public double F(string key, double dflt)
    {
        object o;
        if (_v.TryGetValue(key, out o))
        {
            if (o is double) return (double)o;
            if (o is bool) return ((bool)o) ? 1.0 : 0.0;
        }
        return dflt;
    }

    public bool B(string key, bool dflt)
    {
        object o;
        if (_v.TryGetValue(key, out o))
        {
            if (o is bool) return (bool)o;
            if (o is double) return (double)o != 0.0;
        }
        return dflt;
    }

    public bool Has(string key) { return _v.ContainsKey(key); }

    public IEnumerable<KeyValuePair<string, object>> All { get { return _v; } }
}

public sealed class CameraSystem
{
    public const double DEG = Math.PI / 180.0;
    const double EPS = 1e-6;
    // verified: pitch state machine moves pi/3000 rad per ms = 60 deg/s
    public const double PitchRate = 0.0010471976 * 1000.0;

    public const string MODE_CHARACTER = "character";
    public const string MODE_SPRINT = "sprint";
    public const string MODE_CARRIER = "carrier";
    public const string MODE_AIR_COMBAT = "air_combat";
    public const string MODE_NPC_DIALOG = "npc_dialog";
    public const string MODE_GOD = "god";

    public readonly Dictionary<string, CameraParams> Rows = new Dictionary<string, CameraParams>();
    public readonly List<string> Events = new List<string>();

    public string Mode = MODE_CHARACTER;
    public double Yaw, Pitch, Distance;
    public double[] Offset = new double[3];
    public double[] Pos = new double[3];
    public double[] Look = new double[3];
    public double SprintSpeed;
    public bool MovePitchApplied;
    public double[] FollowActionTarget;

    double _t, _lastT;

    // meters -> host units. 1 m = 100 u: mesh-verified (adult male
    // 181.64 u = 1.816 m, docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md).
    public double UnitsPerMeter = 100.0;

    public CameraSystem()
    {
        Rows[MODE_CHARACTER] = DefaultRow(MODE_CHARACTER);
        Rows[MODE_SPRINT] = DefaultRow(MODE_SPRINT);
        Rows[MODE_CARRIER] = DefaultRow(MODE_CARRIER);
        Rows[MODE_AIR_COMBAT] = DefaultRow(MODE_AIR_COMBAT);
        Rows[MODE_NPC_DIALOG] = DefaultRow(MODE_NPC_DIALOG);
        Rows[MODE_GOD] = DefaultRow(MODE_GOD);
        Pitch = Rows[MODE_CHARACTER].F("InitCameraPitch", -20.0 * DEG);
        Distance = Rows[MODE_CHARACTER].F("InitCameraDistance", 6.0) * UnitsPerMeter;
    }

    public CameraParams Row { get { return Rows[Mode]; } }

    static CameraParams DefaultRow(string mode)
    {
        var p = new CameraParams();
        // verified DLL defaults (proof/netcode/camera_defaults_verified.txt)
        p.Set("CameraMaxDeltaYaw", 2.0 * Math.PI);
        p.Set("CameraMaxDeltaPitch", 1.56);
        p.Set("TargetDistance", 1.0);
        p.Set("SmoothTime", 1.0);
        p.Set("CameraAdjustYawWhenMoveTurnDisableAngle", 0.26);
        p.Set("InitCameraPitch", Math.PI);          // sentinel
        p.Set("InitCameraAngle", 0.0);
        p.Set("ForbidStrafe", false);
        p.Set("ForbidRotation", false);
        p.Set("TurnCameraYawToObjectYaw", false);
        p.Set("CameraMovePitchApplyAngle", 0.0);
        p.Set("CameraMovePitchAdjustPitch", 0.0);
        p.Set("CameraMovePitchApplyMaxPitch", 0.0);
        p.Set("CameraMovePitchApplyTimeInterval", 0.0);
        p.Set("CameraAdjustYawWhenMoveTurn", 0.0);
        switch (mode)
        {
            case MODE_CHARACTER:
                p.Set("CameraHeight", 2.0);
                p.Set("TargetDistance", 6.0);
                // real zoom limits, world units (NOT meters). The client's
                // VideoSettingPanel.tCameraStatic default fMaxCameraDistance is
                // 2000 (userdata/<account>/<role>/custom.dat, 68/92 roles;
                // DLL const blob 0x180d2af90). fMinCameraDistance (engine cap)
                // is not present in this install (camera row tables missing),
                // so the floor is 100 u = 1 m.
                p.Set("MaxCameraDistance", 2000.0);
                p.Set("MinCameraDistance", 100.0);
                p.Set("SmoothTime", 0.08);
                p.Set("MaxDragSpeed", 60.0);
                p.Set("RotationSpeed", 60.0);
                p.Set("InitCameraPitch", -0.35);        // real client default (custom.dat)
                p.Set("InitCameraDistance", 6.0);
                p.Set("CameraMovePitchApplyAngle", -12.0 * DEG);
                p.Set("CameraMovePitchSmoothTime", 0.25);
                p.Set("CameraMovePitchAdjustPitch", -20.0 * DEG);
                p.Set("CameraMovePitchAdjustMaxPitch", -45.0 * DEG);
                p.Set("CameraMovePitchApplyMaxPitch", -30.0 * DEG);
                p.Set("CameraMovePitchApplyTimeInterval", 0.4);
                p.Set("CameraAdjustYawWhenMoveTurn", 1.0);
                break;
            case MODE_SPRINT:
                p.Set("CameraHeight", 2.2);
                p.Set("TargetDistance", 8.0);
                p.Set("SmoothTime", 0.15);
                p.Set("MaxDragSpeed", 70.0);
                p.Set("RotationSpeed", 70.0);
                p.Set("InitCameraPitch", -20.0 * DEG);
                p.Set("InitCameraDistance", 8.0);
                p.Set("SprintCameraMinTrackBackSpeed", 4.0);
                p.Set("SprintCameraMaxTrackBackSpeed", 14.0);
                p.Set("SprintCameraTrackBackSpeedSlope", 0.5);
                p.Set("SprintCameraMaxDistance", 9.0);
                p.Set("SprintCameraSmoothTime", 0.3);
                break;
            case MODE_CARRIER:
                p.Set("CameraHeight", 3.0);
                p.Set("TargetDistance", 7.0);
                p.Set("SmoothTime", 0.12);
                p.Set("InitCameraPitch", -20.0 * DEG);
                p.Set("InitCameraDistance", 7.0);
                p.Set("CarrierCameraMaxDistance", 8.5);
                p.Set("CarrierCameraDeltaHeight", 1.0);
                p.Set("ForbidStrafe", true);
                p.Set("TurnCameraYawToObjectYaw", true);
                break;
            case MODE_AIR_COMBAT:
                p.Set("CameraHeight", 1.0);
                p.Set("TargetDistance", 9.0);
                p.Set("SmoothTime", 0.2);
                p.Set("InitCameraPitch", -20.0 * DEG);
                p.Set("InitCameraDistance", 9.0);
                break;
            case MODE_NPC_DIALOG:
                p.Set("CameraHeight", 1.6);
                p.Set("TargetDistance", 2.5);
                p.Set("SmoothTime", 0.35);
                p.Set("ForbidRotation", true);
                p.Set("InitCameraPitch", -10.0 * DEG);
                p.Set("InitCameraDistance", 2.5);
                break;
            case MODE_GOD:
                p.Set("CameraHeight", 5.0);
                p.Set("TargetDistance", 10.0);
                p.Set("SmoothTime", 0.05);
                p.Set("MaxDragSpeed", 200.0);
                p.Set("RotationSpeed", 200.0);
                p.Set("InitCameraPitch", -25.0 * DEG);
                p.Set("InitCameraDistance", 10.0);
                break;
        }
        return p;
    }

    public void LoadConfig(string path)
    {
        string text = System.IO.File.ReadAllText(path);
        var data = MiniJson.ParseObject(text);
        foreach (var modeKv in data)
        {
            CameraParams row;
            if (!Rows.TryGetValue(modeKv.Key, out row))
            {
                row = new CameraParams();
                Rows[modeKv.Key] = row;
            }
            foreach (var kv in modeKv.Value)
            {
                if (kv.Value is double) row.Set(kv.Key, (double)kv.Value);
                else if (kv.Value is bool) row.Set(kv.Key, (bool)kv.Value);
            }
        }
    }

    public void SaveTemplate(string path)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        int mi = 0;
        foreach (var mode in Rows)
        {
            sb.Append("  \"").Append(mode.Key).Append("\": {\n");
            int ki = 0;
            foreach (var kv in mode.Value.All)
            {
                sb.Append("    \"").Append(kv.Key).Append("\": ");
                if (kv.Value is bool) sb.Append(((bool)kv.Value) ? "true" : "false");
                else sb.Append(((double)kv.Value).ToString("0.######",
                    System.Globalization.CultureInfo.InvariantCulture));
                if (++ki < CountParams(mode.Value)) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("  }");
            if (++mi < Rows.Count) sb.Append(',');
            sb.Append('\n');
        }
        sb.Append("}\n");
        System.IO.File.WriteAllText(path, sb.ToString());
    }

    static int CountParams(CameraParams p)
    {
        int n = 0;
        foreach (var kv in p.All) n++;
        return n;
    }

    public void Mouse(double dx, double dy, double sens = 1.0)
    {
        var row = Row;
        if (!row.B("ForbidRotation", false)) Yaw -= dx * sens;
        Yaw = (Yaw + Math.PI) % (2 * Math.PI) - Math.PI;
        double maxPitch = row.F("CameraMaxDeltaPitch", 0.35);
        Pitch = Math.Max(-Math.PI / 2 + 0.05, Math.Min(Math.PI / 2 - 0.05, Pitch - dy * sens));
        Pitch = Math.Max(Pitch - maxPitch, Math.Min(Pitch + maxPitch, Pitch));
    }

    public void SwitchMode(string mode) { SwitchMode(mode, true); }

    public void SwitchMode(string mode, bool applyInit)
    {
        if (!Rows.ContainsKey(mode)) throw new ArgumentException("unknown camera mode: " + mode);
        Events.Add("leave:" + Mode);
        Mode = mode;
        Events.Add("enter:" + mode);
        if (!applyInit) return;
        var row = Row;
        if (row.Has("InitCameraPitch"))
        {
            double ip = row.F("InitCameraPitch", 0);
            if (Math.Abs(ip - Math.PI) > 1e-9) Pitch = ip;   // pi is the "unset" sentinel
        }
        if (row.Has("InitCameraDistance")) Distance = row.F("InitCameraDistance", 6.0) * UnitsPerMeter;
    }

    public void SetMaxDistance(double meters) { Rows[MODE_CHARACTER].Set("TargetDistance", meters); }

    // JX3 wheel zoom, mirrored from ZoomCharacterCamera_Step
    // (JX3RepresentX64.dll 0x180b3ce40):
    //   step = clamp(current / (0.2 * fMaxCameraDistance) * 120, 10, 120)
    // current/step in world units; limits from the real per-user setting.
    public double ZoomStep(double current)
    {
        double step = current / (0.2 * Row.F("MaxCameraDistance", 2000.0)) * 120.0;
        if (step > 120.0) step = 120.0;
        if (step < 10.0) step = 10.0;
        return step;
    }

    // direction: +1 = zoom out, -1 = zoom in (one wheel notch)
    public void ZoomBy(double direction)
    {
        double td = Row.F("TargetDistance", 6.0) * UnitsPerMeter;
        td += direction * ZoomStep(td);
        double min = Row.F("MinCameraDistance", 100.0);
        double max = Row.F("MaxCameraDistance", 2000.0);
        if (td < min) td = min;
        if (td > max) td = max;
        SetMaxDistance(td / UnitsPerMeter);
    }
    public void SetDragSpeed(double v) { Rows[MODE_CHARACTER].Set("MaxDragSpeed", v); }
    public void SetFollowMode(string mode) { SwitchMode(mode); }
    public void SetPitch(double deg) { Pitch = deg * DEG; }
    public void SetFollowAction(double[] target) { FollowActionTarget = target; }

    // anchor: (x, y=height, z) in host units
    public void Update(double dt, double[] anchor, bool moving = false, double turnAngle = 0.0,
                       double charYaw = 0.0, Func<double[], double[], double?> obstruction = null)
    {
        _t += dt;
        var row = Row;
        if (_lastT == 0.0) { _lastT = _t; dt = 0.0; }
        else dt = Math.Min(dt, 0.1);

        double meters = UnitsPerMeter;
        double distance = row.F("TargetDistance", 6.0) * meters;
        double height = row.F("CameraHeight", 2.0) * meters;

        if (Mode == MODE_SPRINT && SprintSpeed > 0.0)
        {
            double smin = row.F("SprintCameraMinTrackBackSpeed", 4.0);
            double smax = row.F("SprintCameraMaxTrackBackSpeed", 14.0);
            double slope = row.F("SprintCameraTrackBackSpeedSlope", 0.5);
            double target = row.F("SprintCameraMaxDistance", 9.0) * meters;
            double st = Math.Max(row.F("SprintCameraSmoothTime", 0.3), 1e-3);
            Distance += Math.Min(1.0, dt / st) * (target - Distance);
            distance = Distance;
        }
        else
        {
            double st = Math.Max(row.F("SmoothTime", 0.1), 1e-3);
            Distance += Math.Min(1.0, dt / st) * (distance - Distance);
            distance = Distance;
        }

        if (moving)
        {
            double applyAngle = row.F("CameraMovePitchApplyAngle", -12.0 * DEG);
            double step = PitchRate * dt;
            if (Math.Abs(applyAngle - Pitch) <= step) Pitch = applyAngle;
            else Pitch += Math.Sign(applyAngle - Pitch) * step;
            MovePitchApplied = true;
        }
        else
        {
            double adjust = row.F("CameraMovePitchAdjustPitch", -20.0 * DEG);
            double step = PitchRate * dt;
            if (Math.Abs(adjust - Pitch) <= step) Pitch = adjust;
            else Pitch += Math.Sign(adjust - Pitch) * step;
            MovePitchApplied = false;
        }

        if (moving && Math.Abs(turnAngle) > row.F("CameraAdjustYawWhenMoveTurnDisableAngle", 15.0 * DEG))
            Yaw += row.F("CameraAdjustYawWhenMoveTurn", 1.0) * turnAngle;

        // JX3-exact convention: offset = (cos(yaw)*cos(pitch)*d, sin(pitch)*d + h,
        // sin(yaw)*cos(pitch)*d)  (yaw=0 -> +X, yaw+ -> +Z; pitch from horizontal)
        double yaw = Yaw, pitch = Pitch;
        double[] desired = new double[3];
        desired[0] = Math.Cos(yaw) * Math.Cos(pitch) * distance;
        desired[1] = Math.Sin(pitch) * distance + height;
        desired[2] = Math.Sin(yaw) * Math.Cos(pitch) * distance;

        double smoothTime = Math.Max(row.F("SmoothTime", 0.1), 1e-3);
        for (int i = 0; i < 3; i++)
        {
            double delta = desired[i] - Offset[i];
            if (Math.Abs(delta) > EPS && Math.Abs(delta) > Math.Abs(delta) * dt / smoothTime)
                Offset[i] += delta * dt / smoothTime;
            else
                Offset[i] = desired[i];
        }

        for (int i = 0; i < 3; i++) Pos[i] = anchor[i] + Offset[i];

        if (obstruction != null)
        {
            double? hit = obstruction(anchor, Pos);
            if (hit.HasValue)
            {
                double dx = Pos[0] - anchor[0], dy = Pos[1] - anchor[1], dz = Pos[2] - anchor[2];
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (len < 1e-6) len = 1e-6;
                double pull = Math.Max(0.2 * UnitsPerMeter, hit.Value - 0.2 * UnitsPerMeter);
                if (len > pull)
                {
                    double s = pull / len;
                    Pos[0] = anchor[0] + dx * s;
                    Pos[1] = anchor[1] + dy * s;
                    Pos[2] = anchor[2] + dz * s;
                }
            }
        }

        Look[0] = anchor[0];
        Look[1] = anchor[1] + height * 0.35;
        Look[2] = anchor[2];
        if (FollowActionTarget != null)
        {
            Look[0] = FollowActionTarget[0];
            Look[1] = FollowActionTarget[1];
            Look[2] = FollowActionTarget[2];
        }
        _lastT = _t;
    }

    // forward direction on the ground plane (camera -> anchor), for movement input
    public void Forward(out double fx, out double fz)
    {
        fx = -Math.Cos(Yaw);
        fz = -Math.Sin(Yaw);
    }

    // Distance-only update for hosts whose orientation comes from the engine
    // (the engine camera owns the look direction; we advance the JX3 distance
    // dynamics: SmoothTime smoothing + sprint pull-back). Returns the smoothed
    // distance in host units.
    public double UpdateDistance(double dt, bool sprinting = false, double sprintSpeed = 0.0)
    {
        var row = Row;
        double meters = UnitsPerMeter;
        if (Mode == MODE_SPRINT && sprinting && sprintSpeed > 0.0)
        {
            double target = row.F("SprintCameraMaxDistance", 9.0) * meters;
            double st = Math.Max(row.F("SprintCameraSmoothTime", 0.3), 1e-3);
            Distance += Math.Min(1.0, dt / st) * (target - Distance);
        }
        else
        {
            double target = row.F("TargetDistance", 6.0) * meters;
            double st = Math.Max(row.F("SmoothTime", 0.1), 1e-3);
            Distance += Math.Min(1.0, dt / st) * (target - Distance);
        }
        return Distance;
    }

    // JX3 "CameraAdjustYawWhenMoveTurn": while moving, drag the camera yaw
    // toward the character's movement yaw once it leaves the dead zone.
    // Rate-limited pull (converges; no per-frame delta feedback).
    public void FollowYaw(double moveYaw, double dt)
    {
        var row = Row;
        double k = row.F("CameraAdjustYawWhenMoveTurn", 0.0);
        if (k <= 0.0) return;
        double dead = row.F("CameraAdjustYawWhenMoveTurnDisableAngle", 15.0 * DEG);
        double diff = moveYaw - Yaw;
        while (diff > Math.PI) diff -= 2.0 * Math.PI;
        while (diff < -Math.PI) diff += 2.0 * Math.PI;
        if (Math.Abs(diff) <= dead) return;
        double step = k * Math.Min(1.0, dt / 0.2) * diff;
        Yaw = (Yaw + step + Math.PI) % (2.0 * Math.PI) - Math.PI;
    }
}

public sealed class CameraShake
{
    public int Mode;                 // 0 idle, 1 burst
    public double Amp, Period = 0.5, Decay = 0.9, MaxCycles = 6, Cycles, T;
    public double RotScale = 0.02;
    public double[] Offset = new double[3];
    public double Rotation;
    Random _rng;

    public CameraShake(int seed = 1234) { _rng = new Random(seed); }

    public void Start(double amp, double period = 0.5, double decay = 0.9, double cycles = 6)
    {
        Mode = 1; Amp = amp; Period = period; Decay = decay; MaxCycles = cycles; Cycles = 0; T = 0;
    }

    public void Update(double dt)
    {
        if (Mode == 1)
        {
            T += dt;
            double w = Math.Cos((T % Period) / Period * 2.0 * Math.PI) * Amp;
            Rotation = w * RotScale;
            Offset[0] = w * 0.5; Offset[1] = w * 0.25; Offset[2] = w * 0.5;
            if (T >= Period * (Cycles + 1))
            {
                Amp *= Decay;
                Cycles += 1;
                if (Cycles >= MaxCycles)
                {
                    Mode = 0; Amp = 0;
                    Offset[0] = Offset[1] = Offset[2] = 0; Rotation = 0;
                }
            }
        }
        else if (Amp > 0.0)
        {
            for (int i = 0; i < 3; i++) Offset[i] = (_rng.NextDouble() * 2.0 - 1.0) * Amp;
        }
        else
        {
            Offset[0] = Offset[1] = Offset[2] = 0;
        }
    }
}

public sealed class TrackCamera
{
    public double[] Pos = new double[3];
    public double[] Vel = new double[3];

    public double[] Update(double dt, double[] target, double k = 60.0, double c = 12.0)
    {
        for (int i = 0; i < 3; i++)
        {
            double accel = k * (target[i] - Pos[i]) - c * Vel[i];
            Vel[i] += accel * dt;
            Pos[i] += Vel[i] * dt;
        }
        return Pos;
    }
}

// tiny JSON reader for the flat camera config (objects of numbers/bools/strings)
internal static class MiniJson
{
    public static Dictionary<string, Dictionary<string, object>> ParseObject(string text)
    {
        int i = 0;
        var raw = ParseValue(text, ref i, true) as Dictionary<string, object>;
        var outd = new Dictionary<string, Dictionary<string, object>>();
        if (raw == null) return outd;
        foreach (var kv in raw)
        {
            var inner = kv.Value as Dictionary<string, object>;
            if (inner != null) outd[kv.Key] = inner;
        }
        return outd;
    }

    static object ParseValue(string s, ref int i, bool expectObject)
    {
        SkipWs(s, ref i);
        if (i >= s.Length) return null;
        char c = s[i];
        if (c == '{')
        {
            i++;
            var obj = new Dictionary<string, object>();
            while (true)
            {
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; break; }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '{') obj[key] = ParseValue(s, ref i, true);
                else obj[key] = ParseScalar(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
            }
            return obj;
        }
        return null;
    }

    static object ParseScalar(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i >= s.Length) return null;
        if (s[i] == '"') return ParseString(s, ref i);
        int start = i;
        while (i < s.Length && s[i] != ',' && s[i] != '}' && !char.IsWhiteSpace(s[i])) i++;
        string tok = s.Substring(start, i - start).Trim();
        if (tok == "true") return true;
        if (tok == "false") return false;
        double d;
        if (double.TryParse(tok, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
        return tok;
    }

    static string ParseString(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i < s.Length && s[i] == '"')
        {
            i++;
            int start = i;
            while (i < s.Length && s[i] != '"') i++;
            string v = s.Substring(start, i - start);
            if (i < s.Length) i++;
            return v;
        }
        int st = i;
        while (i < s.Length && s[i] != ':' && s[i] != ',' && s[i] != '}') i++;
        return s.Substring(st, i - st).Trim();
    }

    static void SkipWs(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }
}
