// AbilityBar.cs — always-visible numbered ability bar (top-right).
//
// A per-pixel-alpha layered window (same technique as HudOverlay) pinned to the
// top-right of the host form, listing the active hotkey slots: key number +
// ability name, with the selected slot highlighted. Focus stays with the game
// (WS_EX_NOACTIVATE), and the window never blocks input.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class AbilityBar : Form
{
    const int RowH = 26;
    const int Pad = 6;
    const int Gap = 4;
    const int KeyW = 22;

    string[] names = new string[0];
    int selected = -1;
    long cdRemain = 0, cdTotal = 0;   // cooldown of the selected slot (ms)
    bool dirty = true;
    Bitmap buffer;

    readonly Font font = new Font("Consolas", 11f, FontStyle.Bold);
    readonly SolidBrush boxBrush = new SolidBrush(Color.FromArgb(175, 0, 0, 0));
    readonly SolidBrush selBrush = new SolidBrush(Color.FromArgb(215, 40, 95, 165));
    readonly SolidBrush textBrush = new SolidBrush(Color.White);
    readonly SolidBrush keyBrush = new SolidBrush(Color.FromArgb(255, 222, 120));

    public AbilityBar()
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
            cp.ExStyle |= 0x00000080    // WS_EX_TOOLWINDOW
                        | 0x08000000    // WS_EX_NOACTIVATE
                        | 0x00080000;   // WS_EX_LAYERED
            return cp;
        }
    }

    public void PlaceTopRight(Form owner)
    {
        Point o = owner.PointToScreen(Point.Empty);
        int w = ClientSize.Width > 1 ? ClientSize.Width : 140;
        int h = ClientSize.Height > 1 ? ClientSize.Height : 1;
        Point want = new Point(o.X + owner.ClientSize.Width - w - 12, o.Y + 12);
        if (Location != want) Location = want;
        if (Owner == null) Owner = owner;
        if (Height > 1 && Bounds.Bottom > o.Y + owner.ClientSize.Height - 12) { /* keep */ }
    }

    public void SetSlots(string[] s)
    {
        names = s != null ? s : new string[0];
        dirty = true;
    }

    public void SetSlot(int i, string name)
    {
        if (i < 0 || i >= names.Length || names[i] == name) return;
        names[i] = name;
        dirty = true;
    }

    public void SetSelected(int i)
    {
        if (selected != i) { selected = i; dirty = true; }
    }

    // cooldown of the selected slot (remaining/total ms); 0 = ready.
    public void SetCooldown(long remainMs, long totalMs)
    {
        if (remainMs != cdRemain || totalMs != cdTotal)
        {
            cdRemain = remainMs < 0 ? 0 : remainMs;
            cdTotal = totalMs;
            dirty = true;
        }
    }

    public void UpdateLayered()
    {
        if (names.Length == 0) { if (Visible) Hide(); return; }
        if (!dirty) { if (!Visible) Show(); if (Owner != null) PlaceTopRight(Owner); return; }
        dirty = false;

        int rowW = 120;
        using (Bitmap probe = new Bitmap(1, 1))
        using (Graphics g = Graphics.FromImage(probe))
        {
            foreach (string n in names)
            {
                int tw = (int)Math.Ceiling(g.MeasureString(n, font).Width) + KeyW + Gap + Pad * 2;
                if (tw > rowW) rowW = tw;
            }
        }
        int w = rowW;
        int h = Pad * 2 + names.Length * RowH + (names.Length - 1) * Gap;
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
            for (int i = 0; i < names.Length; i++)
            {
                int y = Pad + i * (RowH + Gap);
                Rectangle row = new Rectangle(Pad, y, w - Pad * 2, RowH);
                if (i == selected) g.FillRectangle(selBrush, row);
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString((i + 1).ToString(), font, keyBrush,
                        new Rectangle(row.X, row.Y, KeyW, RowH), sf);
                }
                using (StringFormat sf = new StringFormat())
                {
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(names[i], font, textBrush,
                        new Rectangle(row.X + KeyW + Gap, row.Y, row.Width - KeyW - Gap, RowH), sf);
                }
                if (i == selected && cdRemain > 0 && cdTotal > 0)
                {
                    float frac = (float)cdRemain / cdTotal;
                    if (frac > 1f) frac = 1f;
                    using (SolidBrush cd = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                        g.FillRectangle(cd, row.X, row.Y, (int)(row.Width * frac), row.Height);
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(((cdRemain + 999) / 1000) + "s", font, textBrush, row, sf);
                    }
                }
            }
        }
        if (ClientSize.Width != w || ClientSize.Height != h) ClientSize = new Size(w, h);

        string dump = Environment.GetEnvironmentVariable("RC_BAR_DUMP");
        if (!string.IsNullOrEmpty(dump) &&
            (cdRemain > 0 || Environment.GetEnvironmentVariable("RC_BAR_DUMP_ALL") == "1"))
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
            blend.BlendOp = 0;
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = 1;
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2);
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) { SelectObject(memDc, oldBitmap); DeleteObject(hBitmap); }
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
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
