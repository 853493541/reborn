// Targeting.cs — client-side target selection + target frame HUD (v1).
//
// Mechanics from the shipped client (docs/controls/JX3_TARGET_SELECTION.md):
//   * Tab (SEARCH_ENEMY): engine `KPlayer::LuaSearchForEnemy(nRadius, nAngle)`
//     is called once per zone of `ui/script/target.lua`'s tArea; hits are
//     filtered (selectable/relation) and sorted by zone SelLevel then the
//     axis-offset weight `dist * sin(angle from facing)` (nAxisDim), then
//     `SelectTarget(TARGET.NPC|PLAYER, id)`.
//   * Zones (radius u / half-angle deg / priority): MidAxis 2560/15/3,
//     Inner 512/85/2, Outer 1280/114/1.
//   * Selection is client-local (no opcode); the target id travels with cast
//     intents (future). Click uses a cursor ray (host approximation; the real
//     client uses the engine pick — JX3_COLLISION_SYSTEM.md §10).
//
// HUD: the client's own TargetTarget.ini window rendered with real .UITex
// atlases + game fonts (UiClient.cs). Selection values come from the shipped
// sNpcTemplate row (docs/pvp/TARGET_DUMMY_RESEARCH.md).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class TargetEntity
{
    public long Handle;
    public string Name = "";
    public int Level;
    public long Hp;
    public long MaxHp;
    public float X, Y, Z;      // world position (X/Z ground, Y up)
    public bool IsPlayer;      // relation class: player vs npc
    public bool IsEnemy;
    public System.Collections.Generic.List<string> Buffs = new System.Collections.Generic.List<string>();
    public long CcUntil;       // CC (stun/silence/...) active until this ms
    public string CcType = "";
    public string ModelName = "";   // engine dummy name (to move the model)
    public string ModelPath = "";   // engine model path
    public override string ToString() { return Name + "(" + Level + ")"; }
}

internal sealed class TargetZone
{
    public string Name;
    public float Radius;       // world units (cm)
    public float HalfAngleDeg; // per-zone angular limit from the facing axis
    public int SelLevel;       // priority (higher wins), from target.lua
    public TargetZone(string name, float radius, float halfAngleDeg, int selLevel)
    {
        Name = name; Radius = radius; HalfAngleDeg = halfAngleDeg; SelLevel = selLevel;
    }
}

internal sealed class TargetHit
{
    public TargetEntity Entity;
    public int ZoneLevel;
    public double AxisDistance;  // dist * sin(angle from facing)
    public double Distance;
}

internal sealed class TargetSelector
{
    // target.lua ENMEY tArea rows (MidAxis / Inner / Outer)
    public static readonly TargetZone[] Zones = new TargetZone[]
    {
        new TargetZone("MidAxis", 2560f, 15f, 3),
        new TargetZone("Inner", 512f, 85f, 2),
        new TargetZone("Outer", 1280f, 114f, 1),
    };

    public readonly List<TargetEntity> Entities = new List<TargetEntity>();
    public TargetEntity Current;
    public string LastZone = "";
    public double LastAxisDistance;
    int cycleIndex;

    public void Add(TargetEntity e)
    {
        if (e == null) return;
        Entities.Add(e);
    }

    public void Clear() { Entities.Clear(); Current = null; cycleIndex = 0; }

    // One zone test: |angle from facing| <= half angle and dist <= radius.
    // Returns the hit or null. Facing = (sin(yaw), cos(yaw)) in the X/Z plane.
    public TargetHit TestZone(TargetEntity e, float px, float pz, double yaw, TargetZone zone)
    {
        if (e == null || e.MaxHp <= 0 && e.Hp <= 0) return null;
        double dx = e.X - px;
        double dz = e.Z - pz;
        double dist = Math.Sqrt(dx * dx + dz * dz);
        if (dist > zone.Radius) return null;
        double facingX = Math.Sin(yaw), facingZ = Math.Cos(yaw);
        double fx = dx, fz = dz;
        double fl = Math.Sqrt(fx * fx + fz * fz);
        if (fl < 1e-6) return null;
        fx /= fl; fz /= fl;
        double dot = fx * facingX + fz * facingZ;
        if (dot > 1.0) dot = 1.0;
        if (dot < -1.0) dot = -1.0;
        double angleDeg = Math.Acos(dot) * 180.0 / Math.PI;   // 0 = dead ahead
        if (angleDeg > zone.HalfAngleDeg) return null;
        double sin = Math.Sin(angleDeg * Math.PI / 180.0);
        var hit = new TargetHit();
        hit.Entity = e;
        hit.ZoneLevel = zone.SelLevel;
        hit.Distance = dist;
        hit.AxisDistance = dist * sin;   // target.lua nAxisDim weight
        return hit;
    }

    // Merged search over the three zones, sorted like target.lua's comparator:
    // zone SelLevel desc, then axis-offset asc, then distance asc.
    public List<TargetHit> Search(float px, float pz, double yaw)
    {
        var hits = new List<TargetHit>();
        foreach (TargetEntity e in Entities)
        {
            TargetHit best = null;
            foreach (TargetZone z in Zones)
            {
                TargetHit h = TestZone(e, px, pz, yaw, z);
                if (h == null) continue;
                if (best == null || z.SelLevel > best.ZoneLevel) best = h;
                if (z.SelLevel == 3) break;   // MidAxis is the top priority
            }
            if (best != null) hits.Add(best);
        }
        hits.Sort(delegate(TargetHit a, TargetHit b)
        {
            if (a.ZoneLevel != b.ZoneLevel) return b.ZoneLevel - a.ZoneLevel;
            if (Math.Abs(a.AxisDistance - b.AxisDistance) > 1e-3)
                return a.AxisDistance < b.AxisDistance ? -1 : 1;
            return a.Distance < b.Distance ? -1 : a.Distance > b.Distance ? 1 : 0;
        });
        return hits;
    }

    // Tab / Ctrl+Tab cycle (JX3: g_nSearchEnemyIndex over the merged list).
    // Returns true when a target is selected; false when the zones are empty.
    public bool Cycle(float px, float pz, double yaw, bool prev, Action<string> log)
    {
        List<TargetHit> hits = Search(px, pz, yaw);
        if (hits.Count == 0)
        {
            Current = null;
            cycleIndex = 0;
            if (log != null) log("target: none in cone zones (MidAxis/Inner/Outer)");
            return false;
        }
        if (prev) cycleIndex = (cycleIndex - 2 + hits.Count * 2) % hits.Count;
        else cycleIndex = cycleIndex % hits.Count;
        TargetHit pick = hits[cycleIndex];
        cycleIndex = (cycleIndex + 1) % hits.Count;
        Current = pick.Entity;
        LastZone = ZoneOf(pick.ZoneLevel);
        LastAxisDistance = pick.AxisDistance;
        if (log != null)
            log(string.Format("target={0} lv{1} zone={2} dist={3:F0}u axis={4:F0}u",
                pick.Entity.Name, pick.Entity.Level, LastZone, pick.Distance, pick.AxisDistance));
        return true;
    }

    static string ZoneOf(int selLevel)
    {
        if (selLevel >= 3) return "MidAxis";
        if (selLevel == 2) return "Inner";
        return "Outer";
    }

    // Cursor-ray pick: ray vs each entity's vertical body cylinder, nearest hit
    // wins. The real client picks the character model (KCharacter::OnPickPrepare
    // / represent PickDoodad); the host exposes no world->screen or model pick,
    // so the hit volume is the entity's body cylinder. Any click whose ray
    // misses every body is an empty pick -> deselect (CAMERAORSELECTORMOVE:
    // select under cursor; NO_TARGET clear).
    public const double PickRadius = 90.0;   // body radius (u)
    public const double PickHeight = 220.0;  // body height (u)

    public TargetEntity Pick(float camX, float camY, float camZ,
                             float aimX, float aimY, float aimZ,
                             float nx, float ny, double fovDeg)
    {
        double fx = aimX - camX, fy = aimY - camY, fz = aimZ - camZ;
        double fl = Math.Sqrt(fx * fx + fy * fy + fz * fz);
        if (fl < 1e-6) return null;
        fx /= fl; fy /= fl; fz /= fl;
        // right = normalize(forward x up), up' = right x forward (y up)
        double rx = fz, ry = 0.0, rz = -fx;
        double rl = Math.Sqrt(rx * rx + rz * rz);
        if (rl < 1e-6) { rx = 1.0; rz = 0.0; rl = 1.0; }
        rx /= rl; rz /= rl;
        double ux = fy * rz - fz * ry;
        double uy = fz * rx - fx * rz;
        double uz = fx * ry - fy * rx;
        double halfTan = Math.Tan(fovDeg * Math.PI / 360.0);
        double dx = fx + rx * (nx * halfTan) + ux * (ny * halfTan);
        double dy = fy + ry * (nx * halfTan) + uy * (ny * halfTan);
        double dz = fz + rz * (nx * halfTan) + uz * (ny * halfTan);
        double dl = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (dl < 1e-6) return null;
        dx /= dl; dy /= dl; dz /= dl;
        // ray vs vertical cylinder (XZ circle + Y band); nearest positive hit
        TargetEntity best = null;
        double bestT = double.MaxValue;
        foreach (TargetEntity e in Entities)
        {
            double ox = camX - e.X, oz = camZ - e.Z;
            double a = dx * dx + dz * dz;
            if (a < 1e-9) continue;
            double b = 2.0 * (ox * dx + oz * dz);
            double c = ox * ox + oz * oz - PickRadius * PickRadius;
            double disc = b * b - 4.0 * a * c;
            if (disc < 0) continue;
            double sq = Math.Sqrt(disc);
            double t = (-b - sq) / (2.0 * a);
            if (t < 0) t = (-b + sq) / (2.0 * a);   // origin inside the footprint
            if (t < 0) continue;
            double hy = camY + t * dy;
            if (hy < e.Y || hy > e.Y + PickHeight) continue;
            if (t < bestT) { bestT = t; best = e; }
        }
        return best;
    }
}

// Target HUD: the client's own TargetTarget.ini window rendered with real
// .UITex atlases + game fonts (UiClient.cs), composited over the engine
// viewport by a per-pixel-alpha layered window (a WinForms child control
// cannot blend over the engine's child HWND).
internal sealed class TargetFrameControl : Form
{
    public TargetEntity Target;
    public double Distance;
    readonly UiTargetFrameRenderer renderer;
    Bitmap buffer;
    bool paintErrorLogged;

    public TargetFrameControl(UiTargetFrameRenderer renderer)
    {
        this.renderer = renderer;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = renderer != null ? new Size(renderer.Width, renderer.Height) : new Size(325, 115);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080    // WS_EX_TOOLWINDOW
                        | 0x08000000    // WS_EX_NOACTIVATE
                        | 0x00000020    // WS_EX_TRANSPARENT (click-through)
                        | 0x00080000;   // WS_EX_LAYERED
            return cp;
        }
    }

    // Position over the engine viewport at the window's authored anchor.
    public void PlaceOver(Form owner)
    {
        if (renderer == null) return;
        Point origin = owner.PointToScreen(Point.Empty);
        Location = new Point(origin.X + renderer.OriginX, origin.Y + renderer.OriginY);
        if (!Visible) { Owner = owner; Show(); }
    }

    public void HideFrame() { if (Visible) Visible = false; }

    // Re-render the real UI into the layered buffer.
    public void UpdateLayered()
    {
        if (renderer == null || Target == null) { HideFrame(); return; }
        if (!Visible) Show();
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
        if (buffer == null || buffer.Width != w || buffer.Height != h)
        {
            if (buffer != null) buffer.Dispose();
            buffer = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }
        using (Graphics g = Graphics.FromImage(buffer))
        {
            g.Clear(Color.Transparent);
            try { renderer.Render(g, Target.Name, Target.Level, Target.Hp, Target.MaxHp, Target.IsPlayer); }
            catch (Exception e)
            {
                if (!paintErrorLogged)
                {
                    paintErrorLogged = true;
                    try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "reborn_out", "target_ui_paint_error.txt"), e.ToString()); } catch { }
                }
            }
        }
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero;
        try
        {
            hBitmap = buffer.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memDc, hBitmap);
            var size = new SIZE(buffer.Width, buffer.Height);
            var src = new POINT(0, 0);
            var dst = new POINT(Left, Top);
            var blend = new BLENDFUNCTION();
            blend.BlendOp = 0;
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = 1;   // AC_SRC_ALPHA
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2);
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) { SelectObject(memDc, oldBitmap); DeleteObject(hBitmap); }
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hDC);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
        ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; public SIZE(int w, int h) { cx = w; cy = h; } }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
}
