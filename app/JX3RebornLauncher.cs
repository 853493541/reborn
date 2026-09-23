using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// JX3 Reborn launcher - two options:
//   1. Character action  (spike_host.exe    -> FLWS tani + SFX + sound)
//   2. Map display       (map_spike_host.exe -> MovieEditor map render)
// Both hosts live in C:\SeasunGame\MovieEditor\bin64 and must run with
// working dir = C:\SeasunGame\MovieEditor.
internal static class JX3RebornLauncher
{
    private const string EditorRoot = @"C:\SeasunGame\MovieEditor";
    private static readonly string Bin64 = Path.Combine(EditorRoot, "bin64");

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var form = new Form();
        form.Text = "JX3 Reborn";
        form.ClientSize = new Size(460, 300);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.BackColor = Color.FromArgb(24, 28, 36);
        form.ForeColor = Color.White;

        var title = new Label();
        title.Text = "JX3 Reborn";
        title.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        title.ForeColor = Color.White;
        title.SetBounds(24, 16, 400, 34);
        form.Controls.Add(title);

        var subtitle = new Label();
        subtitle.Text = "MovieEditor \u5F15\u64CE\u4E3B\u673A  /  engine host"; // 引擎主机
        subtitle.Font = new Font("Segoe UI", 9F);
        subtitle.ForeColor = Color.FromArgb(170, 180, 195);
        subtitle.SetBounds(26, 50, 400, 20);
        form.Controls.Add(subtitle);

        var b1 = MakeButton(
            "\u89D2\u8272\u52A8\u4F5C   Character Action\n(\u98CE\u6765\u5434\u5C71)", // 角色动作 / 风来吴山
            78);
        b1.Click += delegate { Launch("spike_host.exe"); };
        form.Controls.Add(b1);

        var b2 = MakeButton(
            "\u5730\u56FE\u663E\u793A   Map Display\n(\u9009\u62E9\u5730\u56FE / pick a map)", // 地图显示 / 选择地图
            148);
        b2.Click += delegate
        {
            string mapPath = PickMap();
            if (mapPath != null) Launch("map_spike_host.exe", "MAP_PATH", mapPath);
        };
        form.Controls.Add(b2);

        var b3 = MakeButton("\u9000\u51FA   Exit", 222); // 退出
        b3.Font = new Font("Segoe UI", 9F);
        b3.Click += delegate { form.Close(); };
        form.Controls.Add(b3);

        Application.Run(form);
    }

    private static Button MakeButton(string text, int top)
    {
        var b = new Button();
        b.Text = text;
        b.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        b.TextAlign = ContentAlignment.MiddleCenter;
        b.SetBounds(24, top, 412, 58);
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Color.FromArgb(40, 48, 62);
        b.ForeColor = Color.White;
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 105, 130);
        return b;
    }

    private static string PickMap()
    {
        // Rendered maps (data\source\maps\<name>\<name>.jsonmap)
        string[] names = {
            "\u9F99\u95E8\u5BFB\u5B9D",             // 龙门寻宝
            "\u9F99\u95E8\u5BFB\u5B9D_\u591C\u665A", // 龙门寻宝_夜晚
            "\u6D77\u5C9B\u7EDD\u5883",             // 海岛绝境
            "\u767D\u9F99\u7EDD\u5883",             // 白龙绝境
            "\u5929\u539F\u7EDD\u5883"              // 天原绝境
        };

        var dlg = new Form();
        dlg.Text = "\u5730\u56FE\u663E\u793A  Map Display"; // 地图显示
        dlg.ClientSize = new Size(360, 292);
        dlg.StartPosition = FormStartPosition.CenterScreen;
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = false;
        dlg.MinimizeBox = false;
        dlg.BackColor = Color.FromArgb(24, 28, 36);
        dlg.ForeColor = Color.White;

        var list = new ListBox();
        list.SetBounds(20, 18, 320, 200);
        list.BackColor = Color.FromArgb(34, 40, 52);
        list.ForeColor = Color.White;
        list.Font = new Font("Microsoft YaHei UI", 11F);
        list.BorderStyle = BorderStyle.FixedSingle;
        foreach (string n in names) list.Items.Add(n);
        list.SelectedIndex = 0;
        dlg.Controls.Add(list);

        var ok = new Button();
        ok.Text = "OK";
        ok.DialogResult = DialogResult.OK;
        ok.SetBounds(170, 236, 80, 30);
        dlg.Controls.Add(ok);

        var cancel = new Button();
        cancel.Text = "\u53D6\u6D88 Cancel"; // 取消
        cancel.DialogResult = DialogResult.Cancel;
        cancel.SetBounds(260, 236, 80, 30);
        dlg.Controls.Add(cancel);

        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog() != DialogResult.OK || list.SelectedIndex < 0) return null;
        string name = names[list.SelectedIndex];
        return "data\\source\\maps\\" + name + "\\" + name + ".jsonmap";
    }

    private static void Launch(string exeName)
    {
        Launch(exeName, null, null);
    }

    private static void Launch(string exeName, string envName, string envValue)
    {
        string path = Path.Combine(Bin64, exeName);
        if (!File.Exists(path))
        {
            MessageBox.Show("Host not found:\n" + path, "JX3 Reborn",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            var psi = new ProcessStartInfo(path);
            psi.WorkingDirectory = EditorRoot;
            psi.UseShellExecute = false;
            if (envName != null) psi.EnvironmentVariables[envName] = envValue;
            Process.Start(psi);
        }
        catch (Exception e)
        {
            MessageBox.Show("Failed to start " + exeName + ":\n" + e.Message, "JX3 Reborn",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
