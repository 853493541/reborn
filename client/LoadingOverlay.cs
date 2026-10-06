// Loading overlay (workstream D4, item 1.2).
//
// The engine renders into a child window of the host form, so normal WinForms
// controls sit behind the 3D output (same reason the HUD is a separate top-level
// layered window). This overlay is a small borderless top-most window shown during
// engine init / map load; it never takes focus (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW)
// and is closed as soon as the client reaches its first frame.
//
// Env: RC_NOLOADING=1 disables it (headless/scripted runs).
using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class LoadingOverlay : Form
{
    readonly Label _label;

    public LoadingOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 84);
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        _label = new Label();
        _label.Dock = DockStyle.Fill;
        _label.ForeColor = Color.White;
        _label.TextAlign = ContentAlignment.MiddleCenter;
        _label.Font = new Font("Microsoft YaHei", 12f);
        _label.Text = "";
        Controls.Add(_label);
        Show();
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return cp;
        }
    }

    // Update the phase text and pump the message queue once, so the text is painted
    // even though the engine calls around it block the UI thread.
    public void Phase(string text)
    {
        try
        {
            _label.Text = text;
            _label.Refresh();
            Application.DoEvents();
        }
        catch { }
    }

    public void Done()
    {
        try { Close(); Dispose(); }
        catch { }
    }
}
