// HUD overlay for the reborn client (M1.7).
//
// The engine renders into a child window of the host form, so WinForms child
// controls (the old Label) sit *behind* the 3D output. This overlay is a
// separate top-level layered window owned by the host form: per-pixel alpha
// (UpdateLayeredWindow) draws above the engine viewport, WS_EX_TRANSPARENT
// keeps it click-through so input still reaches the game. The old clickable
// "I" toggle becomes a keyboard toggle handled by the client.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class HudOverlay : Form
{
    const int HintSize = 22;   // "I" indicator box
    const int Gap = 6;         // space between hint and info box
    const int Pad = 6;         // info box padding

    string text = "loading...";
    bool showInfo;
    bool dirty = true;
    Bitmap buffer;
    readonly Font font;
    readonly Font hintFont;
    readonly SolidBrush boxBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
    readonly SolidBrush textBrush = new SolidBrush(Color.White);

    public HudOverlay()
    {
        font = new Font("Consolas", 10f);
        hintFont = new Font("Consolas", 10f, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(HintSize, HintSize);
    }

    public bool ShowInfo
    {
        get { return showInfo; }
        set { if (showInfo != value) { showInfo = value; dirty = true; } }
    }

    public bool ToggleInfo()
    {
        ShowInfo = !showInfo;
        return showInfo;
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

    // Position over the engine viewport (host form client origin + 10,10).
    public void PlaceOver(Form owner)
    {
        Point origin = owner.PointToScreen(Point.Empty);
        Point want = new Point(origin.X + 10, origin.Y + 10);
        if (Location != want) Location = want;
        if (!Visible) { Owner = owner; Show(); }
    }

    public void SetText(string value)
    {
        if (value != text) { text = value; dirty = true; }
    }

    // Re-render the layered buffer; no-op when nothing changed.
    public void UpdateLayered()
    {
        if (!dirty) return;
        dirty = false;

        Size infoSize = Size.Empty;
        if (showInfo && text.Length > 0)
        {
            using (Bitmap probe = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
                infoSize = Size.Ceiling(g.MeasureString(text, font));
        }
        int w = HintSize + (showInfo ? Gap + infoSize.Width + Pad * 2 : 0);
        int h = Math.Max(HintSize, showInfo ? infoSize.Height + Pad * 2 : 0);
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
            g.FillRectangle(boxBrush, new Rectangle(0, 0, HintSize, HintSize));
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("I", hintFont, textBrush, new RectangleF(0, 0, HintSize, HintSize), sf);
            }
            if (showInfo)
            {
                g.FillRectangle(boxBrush, new Rectangle(HintSize + Gap, 0, w - HintSize - Gap, h));
                g.DrawString(text, font, textBrush, new PointF(HintSize + Gap + Pad, Pad));
            }
        }

        if (ClientSize.Width != w || ClientSize.Height != h)
            ClientSize = new Size(w, h);

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

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
        ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; public SIZE(int w, int h) { cx = w; cy = h; } }
    [StructLayout(LayoutKind.Sequential)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
}

// Clickable COPY LOG widget (top-right, owned by the host form): a small
// top-level form - NOT click-through - so the button receives mouse input over
// the engine viewport; WS_EX_NOACTIVATE keeps focus with the game. The old
// panel Label cannot be used: WinForms children sit behind the engine child
// window (see the HudOverlay header).
internal sealed class CopyLogOverlay : Form
{
    public Action OnClick;

    public CopyLogOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(90, 24);
        BackColor = Color.Black;
        var b = new Label();
        b.Dock = DockStyle.Fill;
        b.Text = "COPY LOG";
        b.TextAlign = ContentAlignment.MiddleCenter;
        b.ForeColor = Color.White;
        b.BackColor = Color.Black;
        b.Font = new Font("Consolas", 9f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Click += delegate { if (OnClick != null) OnClick(); };
        Controls.Add(b);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080    // WS_EX_TOOLWINDOW
                        | 0x08000000    // WS_EX_NOACTIVATE
                        | 0x00000008;   // WS_EX_TOPMOST
            return cp;
        }
    }

    public void PlaceOver(Form owner)
    {
        Point origin = owner.PointToScreen(Point.Empty);
        Point want = new Point(origin.X + owner.ClientSize.Width - 100, origin.Y + 10);
        if (Location != want) Location = want;
        if (!Visible) { Owner = owner; Show(); }
    }
}
