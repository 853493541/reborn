using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using MovieEngineCLR;

internal static class FileListSpike
{
    [DllImport("KGPK4_FileSystemX64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int KG_PAKFS_CollectAllFileNames(int nIndex, out IntPtr ppszNames, out int pnListSize);

    private static readonly string[] Keywords =
    {
        "skill", "talent", "buff", "action", "motion", "rush", "move",
        "skilldata", "tab", "table", "config", "wiki", "ui",
    };

    private static string ReadAnsi(IntPtr p)
    {
        if (p == IntPtr.Zero)
        {
            return null;
        }
        var bytes = new List<byte>(128);
        for (int i = 0; i < 4096; i++)
        {
            byte b = Marshal.ReadByte(p, i);
            if (b == 0)
            {
                break;
            }
            bytes.Add(b);
        }
        return Encoding.GetEncoding(936).GetString(bytes.ToArray());
    }

    private static void Main()
    {
        string editorRoot = @"C:\SeasunGame\MovieEditor";
        string startupPath = Path.Combine(editorRoot, "bin64");
        string workingDir = @"C:\SeasunGame\Game\JX3\bin\zhcn_hd";
        string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "filelist_out");
        Directory.CreateDirectory(outDir);

        var log = new StringBuilder();
        Action<string> Log = delegate(string s)
        {
            Console.WriteLine(s);
            log.AppendLine(s);
        };

        Environment.CurrentDirectory = editorRoot;

        var form = new Form();
        form.ClientSize = new System.Drawing.Size(64, 64);
        form.Show();
        Application.DoEvents();

        var baselib = new KGBaseCLR();
        var engine = new KGEngineCLR();
        var editor = new KGMovieEditorCLR();

        engine.SetRootPath(workingDir);
        try { baselib.InitConsoleLog(); } catch (Exception e) { Log("InitConsoleLog: " + e.Message); }
        int r1 = baselib.InitPath(workingDir, false);
        int r2 = baselib.InitMemory("MovieEditor.memory");
        int r3 = baselib.InitPak(false);
        Log(string.Format("InitPath={0} InitMemory={1} InitPak={2}", r1, r2, r3));

        int err = 1;
        int ok = engine.Init3DEngine(startupPath, startupPath, workingDir, 0, "./configHttpFile.ini", ref err);
        Log(string.Format("Init3DEngine={0} err={1}", ok, err));
        if (ok == 0)
        {
            File.WriteAllText(Path.Combine(outDir, "filelist_host.log"), log.ToString(), Encoding.UTF8);
            return;
        }

        try
        {
            int editorResult = editor.Init(editorRoot, err, form.Handle.ToInt64());
            Log("editor.Init result=" + editorResult);
        }
        catch (Exception e)
        {
            Log("editor.Init ex: " + e.Message);
        }

        for (int index = 0; index < 10; index++)
        {
            IntPtr names;
            int count;
            int okCollect;
            try
            {
                okCollect = KG_PAKFS_CollectAllFileNames(index, out names, out count);
            }
            catch (Exception e)
            {
                Log(string.Format("index {0}: exception {1}", index, e.Message));
                continue;
            }
            Log(string.Format("index {0}: ok={1} count={2}", index, okCollect, count));
            if (okCollect == 0 || names == IntPtr.Zero || count <= 0)
            {
                continue;
            }

            var all = new List<string>(count);
            var interesting = new List<string>();
            for (int i = 0; i < count; i++)
            {
                IntPtr s = Marshal.ReadIntPtr(names, i * IntPtr.Size);
                string name = ReadAnsi(s);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                all.Add(name);
                string low = name.ToLowerInvariant();
                foreach (string kw in Keywords)
                {
                    if (low.Contains(kw))
                    {
                        interesting.Add(name);
                        break;
                    }
                }
            }
            File.WriteAllLines(Path.Combine(outDir, "filelist_" + index + ".txt"), all, Encoding.UTF8);
            File.WriteAllLines(Path.Combine(outDir, "interesting_" + index + ".txt"), interesting, Encoding.UTF8);
            Log(string.Format("index {0}: wrote {1} names, {2} interesting", index, all.Count, interesting.Count));
        }

        File.WriteAllText(Path.Combine(outDir, "filelist_host.log"), log.ToString(), Encoding.UTF8);
        try { form.Close(); } catch { }
    }
}
