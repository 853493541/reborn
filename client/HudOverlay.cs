// Information panel for the reborn client (M1.7).
//
// The engine renders into a child window of the host form, so WinForms child
// controls (the old Label) sit *behind* the 3D output. This panel is a
// separate top-level layered window owned by the host form: per-pixel alpha
// (UpdateLayeredWindow) draws above the engine viewport. It is hidden by
// default (no on-screen hints) and toggled with Esc; while open it shows the
// run info, the control mode and a clickable COPY LOG row. WS_EX_NOACTIVATE
// keeps focus with the game; the window is only visible while open, so it
// never blocks input when closed.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class HudOverlay : Form
{
    const int Pad = 8;         // panel padding
    const int Gap = 8;         // space between blocks
    const int CopyH = 26;      // COPY LOG row height

    string text = "loading...";
    string modeText = "";
    bool showInfo;
    bool dirty = true;
    Bitmap buffer;
    Rectangle copyRect;
    Rectangle copyPosRect;
    readonly Font font;
    readonly Font modeFont;
    readonly Font copyFont;
    readonly SolidBrush boxBrush = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
    readonly SolidBrush copyBoxBrush = new SolidBrush(Color.FromArgb(210, 40, 60, 90));
    readonly SolidBrush textBrush = new SolidBrush(Color.White);
    readonly SolidBrush modeBrush = new SolidBrush(Color.FromArgb(255, 220, 120));
    readonly SolidBrush copyBrush = new SolidBrush(Color.FromArgb(220, 235, 255));

    // Copy actions, wired by the client (recent run log / current position).
    public Action OnCopyLog;
    public Action OnCopyPos;

    public HudOverlay()
    {
        font = new Font("Consolas", 10f);
        modeFont = new Font("Consolas", 12f, FontStyle.Bold);
        copyFont = new Font("Consolas", 10f, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(1, 1);
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
                        | 0x00080000;   // WS_EX_LAYERED
            return cp;
        }
    }

    // Position over the engine viewport (host form client origin + 10,10).
    // Visibility is owned by UpdateLayered (only shown while the panel is open).
    public void PlaceOver(Form owner)
    {
        if (owner == null || owner.IsDisposed || !owner.IsHandleCreated) return;
        Point origin = owner.PointToScreen(Point.Empty);
        Point want = new Point(origin.X + 10, origin.Y + 10);
        if (Location != want) Location = want;
        if (Owner == null) Owner = owner;
    }

    public void SetText(string value)
    {
        if (value != text) { text = value; dirty = true; }
    }

    // Control-mode name (shown inside the panel).
    public void SetModeText(string value)
    {
        if (value != modeText) { modeText = value; dirty = true; }
    }

    // Re-render the layered buffer; hidden while the panel is closed.
    public void UpdateLayered()
    {
        if (!showInfo)
        {
            if (Visible) Hide();
            dirty = false;
            return;
        }
        if (!dirty)
        {
            if (!Visible) Show();
            return;
        }
        dirty = false;

        Size modeSize = Size.Empty, infoSize = Size.Empty, copySize = Size.Empty;
        using (Bitmap probe = new Bitmap(1, 1))
        using (Graphics g = Graphics.FromImage(probe))
        {
            if (modeText.Length > 0) modeSize = Size.Ceiling(g.MeasureString(modeText, modeFont));
            if (text.Length > 0) infoSize = Size.Ceiling(g.MeasureString(text, font));
            copySize = Size.Ceiling(g.MeasureString("COPY POS", copyFont));
        }
        int modeH = modeText.Length > 0 ? modeSize.Height : 0;
        int copyW = Math.Max(140, copySize.Width + Pad * 3);
        int w = Math.Max(Math.Max(modeSize.Width, infoSize.Width), copyW * 2 + Gap) + Pad * 2;
        int h = Pad + modeH + Gap + infoSize.Height + Gap + CopyH + Pad;
        if (w < 1) w = 1;
        if (h < 1) h = 1;

        if (buffer == null || buffer.Width != w || buffer.Height != h)
        {
            if (buffer != null) buffer.Dispose();
            buffer = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        }
        copyRect = new Rectangle(Pad, h - Pad - CopyH, copyW, CopyH);
        copyPosRect = new Rectangle(Pad + copyW + Gap, h - Pad - CopyH, copyW, CopyH);
        using (Graphics g = Graphics.FromImage(buffer))
        {
            g.Clear(Color.Transparent);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.FillRectangle(boxBrush, new Rectangle(0, 0, w, h));
            if (modeText.Length > 0)
                g.DrawString(modeText, modeFont, modeBrush, new PointF(Pad, Pad));
            g.DrawString(text, font, textBrush, new PointF(Pad, Pad + modeH + Gap));
            g.FillRectangle(copyBoxBrush, copyRect);
            g.FillRectangle(copyBoxBrush, copyPosRect);
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("COPY LOG", copyFont, copyBrush, copyRect, sf);
                g.DrawString("COPY POS", copyFont, copyBrush, copyPosRect, sf);
            }
        }

        if (ClientSize.Width != w || ClientSize.Height != h)
            ClientSize = new Size(w, h);

        // RC_HUD_DUMP=<png path>: save the rendered buffer (test/diagnostic;
        // layered windows cannot be captured with PrintWindow or screen grabs).
        string dump = Environment.GetEnvironmentVariable("RC_HUD_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            try { buffer.Save(dump, ImageFormat.Png); }
            catch { }
        }

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
        if (!Visible) Show();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!showInfo) return;
        if (copyRect.Contains(e.Location)) { if (OnCopyLog != null) OnCopyLog(); }
        else if (copyPosRect.Contains(e.Location)) { if (OnCopyPos != null) OnCopyPos(); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor want = showInfo && (copyRect.Contains(e.Location) || copyPosRect.Contains(e.Location))
            ? Cursors.Hand : Cursors.Default;
        if (Cursor != want) Cursor = want;
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
