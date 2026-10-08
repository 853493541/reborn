// AbilityPanel.cs 鈥?v6 ability panel (our own; reuses the v5 icon assets only).
//
// A top-level borderless window listing every ability from the roster
// (ability_picker/tools/build_roster.py -> roster_f1.tsv) as an icon grid.
// Click an icon = make it the active ability; key 1 casts it. P toggles.
//
// It is NOT a child of the render host: adding controls to the render panel
// before engine init hung the host (measured 2026-10-08). A separate top-level
// window (WS_EX_NOACTIVATE, owned by the main form) renders above the engine
// just like HudOverlay/AbilityBar and leaves the engine surface untouched.
// Icons come from the shipped icon assets extracted to
// bin64\ability_picker\icons\<id>.png (v5 supplied only the extracted PNGs).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class AbilityPanel : Form
{
    public string SelectedId = "";
    public string SelectedName = "";
    public Action OnSelect;

    readonly Dictionary<string, string[]> rows;
    readonly Dictionary<string, PictureBox> byId = new Dictionary<string, PictureBox>();
    PictureBox selectedPb;
    readonly Action<string> log;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080    // WS_EX_TOOLWINDOW
                        | 0x08000000;   // WS_EX_NOACTIVATE
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
        ClientSize = new Size(340, 640);

        var head = new Label();
        head.Dock = DockStyle.Top;
        head.Height = 22;
        head.Text = "  ABILITIES (P)  -  click = active,  1 = cast";
        head.ForeColor = Color.Gainsboro;
        head.BackColor = Color.FromArgb(24, 24, 28);
        head.Font = new Font("Consolas", 9f, FontStyle.Bold);
        Controls.Add(head);

        var grid = new FlowLayoutPanel();
        grid.Dock = DockStyle.Fill;
        grid.AutoScroll = true;
        grid.WrapContents = true;
        grid.FlowDirection = FlowDirection.LeftToRight;
        grid.BackColor = Color.FromArgb(14, 14, 16);
        grid.Padding = new Padding(2);
        var tip = new ToolTip();
        tip.InitialDelay = 150;
        tip.AutoPopDelay = 20000;

        int got = 0;
        for (int i = 0; i < order.Count; i++)
        {
            string id = order[i];
            string[] r;
            if (!rows.TryGetValue(id, out r)) continue;
            var pb = new PictureBox();
            pb.Size = new Size(28, 28);          // 30% smaller than 40
            pb.Margin = new Padding(1);
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.Cursor = Cursors.Hand;
            pb.BackColor = Color.FromArgb(32, 32, 36);
            pb.Paint += delegate(object s, PaintEventArgs e)
            {
                if (s == selectedPb)
                {
                    using (Pen pen = new Pen(Color.FromArgb(80, 220, 80), 2f))
                        e.Graphics.DrawRectangle(pen, 1, 1, pb.Width - 3, pb.Height - 3);
                }
            };
            string png = r.Length > 8 ? r[8] : "";
            if (png.Length > 0)
            {
                try
                {
                    string p = Path.Combine(iconDir, png);
                    if (File.Exists(p))
                    {
                        using (FileStream fs = File.OpenRead(p))
                        using (Image img = Image.FromStream(fs))
                            pb.Image = new Bitmap(img);
                        got++;
                    }
                }
                catch { }
            }
            tip.SetToolTip(pb, r[1] + "  [" + id + "]");
            string cid = id;
            pb.Click += delegate { Select(cid); };
            grid.Controls.Add(pb);
            byId[id] = pb;
        }
        Controls.Add(grid);
        grid.BringToFront();
        head.BringToFront();
        log("ability panel: " + order.Count + " entries, " + got + " icons");
    }

    public void Attach(Form owner)
    {
        Point o = owner.PointToScreen(Point.Empty);
        int w = ClientSize.Width > 1 ? ClientSize.Width : 300;
        Location = new Point(o.X + owner.ClientSize.Width - w - 8, o.Y + 40);
        if (Owner == null) Owner = owner;
    }

    public void Select(string id)
    {
        string[] r;
        if (!rows.TryGetValue(id, out r)) return;
        SelectedId = id;
        SelectedName = r[1];
        selectedPb = byId.ContainsKey(id) ? byId[id] : null;
        foreach (KeyValuePair<string, PictureBox> kv in byId) kv.Value.Invalidate();
        if (OnSelect != null) OnSelect();
    }

    public void Toggle() { SetVisible(!Visible); }

    public void SetVisible(bool v)
    {
        Visible = v;
        if (v) { BringToFront(); if (Owner != null) Attach(Owner); }
    }
}
