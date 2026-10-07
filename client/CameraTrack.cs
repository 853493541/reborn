// CameraTrack.cs - .mani (ACON) camera-track decoder + sampler for the host
// client (workstream B, P1). Format spec: docs/camera/MANI_FORMAT.md
// (verified on 10/10 shipped cameradata samples; the rush variant is a
// different grammar and is rejected with a clear message - deferred).
//
// Read-only: File.ReadAllBytes on the given path (no engine, no writes).
// C# 5 (csc Framework64 v4.0.30319): no interpolation/nameof/expression bodies.
using System;
using System.Collections.Generic;
using System.IO;

class CameraTrack
{
    public class Key
    {
        public int Frame;
        public float X, Y, Z;

        public Key(int frame, float x, float y, float z)
        {
            Frame = frame; X = x; Y = y; Z = z;
        }
    }

    // Track A: camera position. The frame-0 key is implicit (x0, z0, y0).
    public readonly List<Key> Cam = new List<Key>();
    public float CamX0, CamY0, CamZ0;
    // Track B: secondary track (host treats it as the look-at target; the
    // SceneCameraAni.tab rotation-type column is the real consumer - MED).
    public readonly List<Key> Aim = new List<Key>();

    public float Duration;   // frames (last frame + 1)
    public int Fps = 30;     // SceneCameraAni.tab: duration/enter-ms = 30.0 on 4 samples
    public string Source = "";

    bool active;
    bool loop;
    double frame;

    public bool Active { get { return active; } }
    public double Frame { get { return frame; } }

    public string Describe()
    {
        return string.Format("source={0} duration={1:F0}f camKeys={2} aimKeys={3} fps={4}",
            Source, Duration, Cam.Count, Aim.Count, Fps);
    }

    public void Play(bool loopPlayback)
    {
        active = true;
        loop = loopPlayback;
        frame = 0.0;
    }

    public void Update(double dtMs)
    {
        if (!active) return;
        frame += dtMs * 0.001 * Fps;
        double last = Duration - 1.0;
        if (last < 0.0) last = 0.0;
        if (loop)
        {
            double span = Duration > 0f ? Duration : 1.0;
            while (frame >= span) frame -= span;
        }
        else if (frame > last)
        {
            frame = last;   // hold the last authored pose until stopped
        }
    }

    public void Sample(double atFrame,
                       out double camX, out double camY, out double camZ,
                       out double aimX, out double aimY, out double aimZ)
    {
        SampleList(Cam, CamX0, CamY0, CamZ0, atFrame, out camX, out camY, out camZ);
        if (Aim.Count > 0)
        {
            SampleList(Aim, 0f, 0f, 0f, atFrame, out aimX, out aimY, out aimZ);
        }
        else
        {
            aimX = camX; aimY = camY; aimZ = camZ - 100.0;
        }
    }

    static void SampleList(List<Key> keys, float x0, float y0, float z0, double atFrame,
                           out double x, out double y, out double z)
    {
        // keys are frame-increasing (validated at load); the implicit frame-0
        // key is the header default (x0, y0, z0).
        if (keys.Count == 0 || atFrame <= 0.0)
        {
            x = x0; y = y0; z = z0;
            return;
        }
        int prevF = 0;
        float px = x0, py = y0, pz = z0;
        for (int i = 0; i < keys.Count; i++)
        {
            Key k = keys[i];
            if (atFrame <= k.Frame)
            {
                double t = k.Frame == prevF ? 0.0 : (atFrame - prevF) / (double)(k.Frame - prevF);
                x = px + (k.X - px) * t;
                y = py + (k.Y - py) * t;
                z = pz + (k.Z - pz) * t;
                return;
            }
            prevF = k.Frame; px = k.X; py = k.Y; pz = k.Z;
        }
        x = px; y = py; z = pz;
    }

    // ------------------------------------------------------------- parsing

    static uint U32(byte[] b, int o)
    {
        return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    }

    static float F32(byte[] b, int o)
    {
        return BitConverter.ToSingle(b, o);
    }

    static void Need(byte[] b, int off, int n)
    {
        if (off < 0 || off + n > b.Length) throw new Exception("truncated payload at " + off);
    }

    public static CameraTrack Load(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        CameraTrack t = new CameraTrack();
        t.Source = path;
        Need(b, 0, 40);
        if (U32(b, 0) != 0x4E4F4341) throw new Exception("not an ACON container");
        uint cls0 = U32(b, 4);
        int off;
        if (cls0 == 25)
        {
            Need(b, 64, 40);
            if (U32(b, 64) != 0x4E4F4341) throw new Exception("missing class-10 section at 64");
            if (U32(b, 68) != 10) throw new Exception("expected class 10, got " + U32(b, 68));
            off = 104;
        }
        else if (cls0 == 10)
        {
            off = 40;
        }
        else
        {
            throw new Exception("unsupported ACON class " + cls0);
        }
        Need(b, off, 8);
        for (int i = 0; i < 8; i++)
            if (b[off + i] != 0) throw new Exception("payload prefix not zero");
        off += 8;
        Need(b, off, 16);
        uint kind = U32(b, off);
        float dur = F32(b, off + 4);
        uint m3 = U32(b, off + 8);
        uint m4 = U32(b, off + 12);
        if (kind != 1) throw new Exception("meta kind " + kind);
        if (cls0 == 25)
        {
            if (m3 != 1 || m4 != 0) throw new Exception("cameradata meta marker mismatch");
        }
        else
        {
            if (m3 != 0 || m4 != 1)
                throw new Exception("rush-variant .mani grammar not decoded (deferred)");
        }
        t.Duration = dur;
        off += 16;
        Need(b, off, 16);
        uint nA = U32(b, off);
        t.CamX0 = F32(b, off + 4);
        t.CamZ0 = F32(b, off + 8);
        t.CamY0 = F32(b, off + 12);
        if (nA < 1) throw new Exception("nA=" + nA);
        off += 16;
        int prev = -1;
        for (uint k = 0; k + 1 < nA; k++)
        {
            Need(b, off, 16);
            float x = F32(b, off);
            uint f = U32(b, off + 4);
            float z = F32(b, off + 8);
            float y = F32(b, off + 12);
            if ((int)f <= prev) throw new Exception("A frames not increasing at " + f);
            prev = (int)f;
            t.Cam.Add(new Key((int)f, x, y, z));
            off += 16;
        }
        Need(b, off, 16);
        uint nB = U32(b, off + 8);
        off += 16;
        prev = -1;
        for (uint k = 0; k < nB; k++)
        {
            Need(b, off, 16);
            float x = F32(b, off);
            float a = F32(b, off + 4);
            float bb = F32(b, off + 8);
            uint f = U32(b, off + 12);
            if ((int)f <= prev)
            {
                // loop-closure key at frame 1 (all 10 shipped samples)
                if (!(k == nB - 1 && f == 1)) throw new Exception("B frames not increasing at " + f);
            }
            prev = (int)f;
            t.Aim.Add(new Key((int)f, x, a, bb));
            off += 16;
        }
        if (off != b.Length)
            throw new Exception("payload not fully consumed: " + (b.Length - off) + " trailing bytes");
        return t;
    }
}
