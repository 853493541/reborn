// AbilityPanel.cs — v6 ability panel (layered overlay, categorized).
//
// A per-pixel-alpha layered window (WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW),
// like HudOverlay/CombatText, so it renders ABOVE the engine child window (a plain Form's
// top was occluded by the engine surface). Icons are drawn into the layered buffer and clicks
// are hit-tested; the panel is grouped into catalogs (e.g. 测试 at the top, then 全部).
// Reuses only the shipped icon assets (bin64\ability_picker\icons\<id>.png).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class AbilityPanel : Form
{
    sealed class Cat { public string Name; public List<string> Ids; public Cat(string n, List<string> i) { Name = n; Ids = i; } }
    sealed class Item { public bool Header; public string Text = ""; public string Id = ""; public Rectangle R; }

    public string SelectedId = "";
    public string SelectedName = "";
    public Action OnSelect;

    readonly Dictionary<string, string[]> rows;
    readonly Dictionary<string, Image> icons = new Dictionary<string, Image>();
    readonly List<Cat> cats = new List<Cat>();
    readonly List<Item> items = new List<Item>();
    readonly Action<string> log;

    const int Pad = 6;
    const int Cell = 34;
    const int IconPx = 29;   // 36 * 0.8
    const int HeaderH = 22;

    string selectedId = "";
    int contentH = 0;
    int scrollY = 0;
    Bitmap buffer;
    readonly Font headFont = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
    readonly SolidBrush boxBrush = new SolidBrush(Color.FromArgb(235, 14, 14, 16));
    readonly SolidBrush headBrush = new SolidBrush(Color.FromArgb(255, 230, 190));
    readonly SolidBrush selBrush = new SolidBrush(Color.FromArgb(120, 30, 90, 40));
    readonly Pen selPen = new Pen(Color.FromArgb(80, 220, 80), 2f);
    readonly SolidBrush barBrush = new SolidBrush(Color.FromArgb(120, 200, 200, 200));

    // Test catalog: the 5 abilities verified individually.
    static readonly string[] TestIds = { "65029", "65076", "65116", "30081", "27844" };

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080 | 0x08000000 | 0x00080000;   // TOOLWINDOW|NOACTIVATE|LAYERED
            return cp;
        }
    }

    public AbilityPanel(Dictionary<string, string[]> rows, List<string> order,
                        string iconDir, Action<string> logIn)
    {
        this.rows = rows;
        log = logIn;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(14, 14, 16);
        ClientSize = new Size(320, 600);

        // catalog: 测试 (the 5 verified) on top, then 全部 (everything not in 测试).
        var test = new List<string>();
        foreach (string t in TestIds) if (rows.ContainsKey(t)) test.Add(t);
        var rest = new List<string>();
        foreach (string id in order) if (!test.Contains(id)) rest.Add(id);
        if (test.Count > 0) cats.Add(new Cat("测试", test));
        cats.Add(new Cat("全部", rest));

        int got = 0;
        foreach (string id in order)
        {
            string[] r;
            if (!rows.TryGetValue(id, out r)) continue;
            string png = r.Length > 8 ? r[8] : "";
            if (png.Length == 0) continue;
            try
            {
                string p = Path.Combine(iconDir, png);
                if (File.Exists(p))
                {
                    using (FileStream fs = File.OpenRead(p))
                    using (Image img = Image.FromStream(fs))
                        icons[id] = new Bitmap(img);
                    got++;
                }
            }
            catch { }
        }
        Layout();
        log("ability panel: " + order.Count + " entries, " + got + " icons, " + cats.Count + " catalogs");
    }

    void Layout()
    {
        items.Clear();
        int w = ClientSize.Width;
        int y = Pad;
        foreach (Cat c in cats)
        {
            var h = new Item(); h.Header = true; h.Text = c.Name; h.R = new Rectangle(Pad, y, w - Pad * 2, HeaderH);
            items.Add(h);
            y += HeaderH;
            int x = Pad;
            foreach (string id in c.Ids)
            {
                if (x + IconPx > w - Pad) { x = Pad; y += Cell; }
                var it = new Item(); it.Id = id; it.R = new Rectangle(x, y, IconPx, IconPx);
                items.Add(it);
                x += Cell;
            }
            y += Cell;
        }
        contentH = y + Pad;
    }

    public void Attach(Form owner)
    {
        Point o = owner.PointToScreen(Point.Empty);
        int w = 320;
        int h = owner.ClientSize.Height - 80;
        if (h < 200) h = 200;
        Location = new Point(o.X + owner.ClientSize.Width - w - 8, o.Y + 40);
        ClientSize = new Size(w, h);
        if (Owner == null) Owner = owner;
        Layout();
    }

    public void Select(string id)
    {
        string[] r;
        if (!rows.TryGetValue(id, out r)) return;
        selectedId = id;
        SelectedId = id;
        SelectedName = r[1];
        if (OnSelect != null) OnSelect();
    }

    public void Toggle() { SetVisible(!Visible); }

    public void SetVisible(bool v)
    {
        Visible = v;
        if (v)
        {
            if (Owner != null) Attach(Owner);
            SetWindowPos(Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            UpdateLayered();
        }
    }

    public void UpdateLayered()
    {
        int w = ClientSize.Width, h = ClientSize.Height;
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
            foreach (Item it in items)
            {
                Rectangle r = it.R; r.Offset(0, -scrollY);
                if (r.Bottom < 0 || r.Top > h) continue;
                if (it.Header)
                {
                    g.DrawString(it.Text, headFont, headBrush, r.X, r.Y);
                }
                else
                {
                    Image img;
                    if (icons.TryGetValue(it.Id, out img))
                        g.DrawImage(img, r);
                    if (it.Id == selectedId)
                    {
                        g.FillRectangle(selBrush, r);
                        g.DrawRectangle(selPen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                    }
                }
            }
            // scrollbar
            int maxScroll = contentH > h ? contentH - h : 0;
            if (maxScroll > 0)
            {
                int barH = Math.Max(20, h * h / contentH);
                int barY = (h - barH) * scrollY / maxScroll;
                g.FillRectangle(barBrush, w - 4, barY, 3, barH);
            }
        }

        string dump = Environment.GetEnvironmentVariable("RC_PANEL_DUMP");
        if (!string.IsNullOrEmpty(dump)) { try { buffer.Save(dump, ImageFormat.Png); } catch { } }

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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        foreach (Item it in items)
        {
            if (it.Header) continue;
            Rectangle r = it.R; r.Offset(0, -scrollY);
            if (r.Contains(e.Location)) { Select(it.Id); UpdateLayered(); break; }
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int maxScroll = contentH > ClientSize.Height ? contentH - ClientSize.Height : 0;
        scrollY -= e.Delta / 4;
        if (scrollY < 0) scrollY = 0;
        if (scrollY > maxScroll) scrollY = maxScroll;
        UpdateLayered();
    }

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    static readonly IntPtr HWND_TOP = IntPtr.Zero;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOACTIVATE = 0x0010;
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
