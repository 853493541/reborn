// CombatText.cs — always-on combat feedback overlay (P5).
//
// A per-pixel-alpha layered window (like HudOverlay/AbilityBar) pinned near the
// top-centre: a status line (target HP / self mana / GCD) plus a short stack of
// recent combat events (damage/heal/CC), each fading out after ~2.5 s.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class CombatText : Form
{
    sealed class Line { public string Text; public long T; public Color C; }

    readonly List<Line> lines = new List<Line>();
    string status = "";
    string castName = "";
    float castPct = 0f;
    bool dirty = true;
    Bitmap buffer;
    readonly Font font = new Font("Consolas", 12f, FontStyle.Bold);
    readonly SolidBrush textBrush = new SolidBrush(Color.White);
    readonly SolidBrush boxBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
    const long Life = 2500;

    public CombatText()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(1, 1);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080 | 0x08000000 | 0x00080000;   // TOOLWINDOW|NOACTIVATE|LAYERED
            return cp;
        }
    }

    public void Push(string text, Color c)
    {
        lines.Add(new Line { Text = text, T = Environment.TickCount, C = c });
        if (lines.Count > 8) lines.RemoveAt(0);
        dirty = true;
    }

    public void SetStatus(string s) { if (s != status) { status = s; dirty = true; } }

    // cast bar (prepare/channel): name + progress 0..1; empty name hides it.
    public void SetCast(string name, float pct)
    {
        if (name != castName || Math.Abs(pct - castPct) > 0.02f)
        {
            castName = name;
            castPct = pct < 0f ? 0f : (pct > 1f ? 1f : pct);
            dirty = true;
        }
    }

    public void PlaceTop(Form owner)
    {
        Point o = owner.PointToScreen(Point.Empty);
        int w = ClientSize.Width > 1 ? ClientSize.Width : 260;
        Point want = new Point(o.X + (owner.ClientSize.Width - w) / 2, o.Y + 12);
        if (Location != want) Location = want;
        if (Owner == null) Owner = owner;
    }

    public void UpdateLayered()
    {
        long now = Environment.TickCount;
        for (int i = lines.Count - 1; i >= 0; i--)
            if (now - lines[i].T > Life) { lines.RemoveAt(i); dirty = true; }
        if (!dirty) { if (!Visible) Show(); return; }
        dirty = false;

        int w = 260, lh = 22;
        using (Bitmap probe = new Bitmap(1, 1))
        using (Graphics g = Graphics.FromImage(probe))
        {
            foreach (Line l in lines)
            {
                int tw = (int)Math.Ceiling(g.MeasureString(l.Text, font).Width) + 20;
                if (tw > w) w = tw;
            }
            if (status.Length > 0)
            {
                int tw = (int)Math.Ceiling(g.MeasureString(status, font).Width) + 20;
                if (tw > w) w = tw;
            }
        }
        int barH = castName.Length > 0 ? 16 : 0;
        int h = 8 + (status.Length > 0 ? lh : 0) + barH + lines.Count * lh;
        if (w < 1) w = 1;
        if (h < 1) h = 1;
        if (buffer == null || buffer.Width != w || buffer.Height != h)
        {
            if (buffer != null) buffer.Dispose();
            buffer = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        }
        using (Graphics g = Graphics.FromImage(buffer))
        {
            g.Clear(Color.Transparent);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.FillRectangle(boxBrush, new Rectangle(0, 0, w, h));
            int y = 4;
            if (status.Length > 0) { g.DrawString(status, font, textBrush, 8, y); y += lh; }
            if (castName.Length > 0)
            {
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(90, 90, 90)))
                    g.FillRectangle(bg, 8, y + 3, w - 16, 10);
                using (SolidBrush fg = new SolidBrush(Color.FromArgb(120, 200, 120)))
                    g.FillRectangle(fg, 8, y + 3, (int)((w - 16) * castPct), 10);
                g.DrawString(castName + "  " + (int)(castPct * 100) + "%", font, textBrush, 8, y);
                y += barH;
            }
            foreach (Line l in lines)
            {
                using (SolidBrush b = new SolidBrush(l.C)) g.DrawString(l.Text, font, b, 8, y);
                y += lh;
            }
        }
        if (ClientSize.Width != w || ClientSize.Height != h) ClientSize = new Size(w, h);

        string dump = Environment.GetEnvironmentVariable("RC_CT_DUMP");
        if (!string.IsNullOrEmpty(dump) &&
            (castName.Length > 0 || Environment.GetEnvironmentVariable("RC_CT_DUMP_ALL") == "1"))
        { try { buffer.Save(dump, ImageFormat.Png); } catch { } }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero;
        try
        {
            hBitmap = buffer.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memDc, hBitmap);
            SIZE size = new SIZE(buffer.Width, buffer.Height);
            POINT src = new POINT(0, 0);
            POINT dst = new POINT(Left, Top);
            BLENDFUNCTION blend = new BLENDFUNCTION();
            blend.BlendOp = 0; blend.SourceConstantAlpha = 255; blend.AlphaFormat = 1;
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2);
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) { SelectObject(memDc, oldBitmap); DeleteObject(hBitmap); }
            DeleteDC(memDc); ReleaseDC(IntPtr.Zero, screenDc);
        }
        if (!Visible) Show();
    }

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
        ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; public SIZE(int w, int h) { cx = w; cy = h; } }
    [StructLayout(LayoutKind.Sequential)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
}
