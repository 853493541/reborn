// Runtime hotkey table (controls C1/C2): the game's own binding data
// (ui/hotkey/default.txt + ui/hotkey/bindings.ini) decoded at startup.
//
// Key encoding (docs/controls/JX3_HOTKEY_SYSTEM.md §3): value = low 16 bits
// virtual-key code, high 16 bits modifier word (Ctrl 0x1, Shift 0x2, Alt 0x4).
// Mouse specials: 1 LMB, 2 RMB, 256 wheel up, 257 wheel down.
//
// Sources: the extracted game files are compiled into the exe as embedded
// resources (same bytes as proof/movement/extracted/, which the parser tool
// decodes offline). RC_HOTKEY_DIR overrides with live files - the future path
// for user rebinding / per-role overrides (C3/C4); never writes anything.
//
// Matching: VK + Ctrl/Alt must match exactly. Shift is ignored for the
// movement command set only, because Shift is the registered host debug speed
// (x10) - a held Shift must not block W/A/S/D. Documented in
// docs/controls/CONTROLS_GAP_REGISTER.md (C1 note).

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

internal sealed class HotkeyRow
{
    public string Name = "";
    public string Context = "";
    public int Key1;
    public int Key2;
    public string Down = "";
    public string Up = "";
}

internal sealed class HotkeyTable
{
    public const string DefaultResource = "ui_hotkey_default.txt";
    public const string BindingsResource = "ui_hotkey_bindings.ini";

    // Commands whose bindings ignore the Shift modifier (host debug speed).
    private static readonly string[] ShiftIgnored = new string[]
    {
        "MOVEFORWARD", "MOVEBACKWARD", "STRAFELEFT", "STRAFERIGHT",
        "TURNLEFT", "TURNRIGHT", "JUMP", "TOGGLERUN", "TOGGLEAUTORUN"
    };

    private readonly List<HotkeyRow> rows = new List<HotkeyRow>();
    private readonly Dictionary<string, HotkeyRow> byName =
        new Dictionary<string, HotkeyRow>(StringComparer.OrdinalIgnoreCase);

    public int Count { get { return rows.Count; } }

    // Active binding context ("" = normal play). Rows in another context must
    // not fire in normal play (e.g. MINIGAME_JUMP on W would alias
    // MOVEFORWARD). Contexts come from the shipped table and custom.dat-era
    // runtime events (morph/summon/minigame/BR).
    public string Context = "";
    public int Overrides;

    public HotkeyRow Get(string name)
    {
        HotkeyRow r;
        return byName.TryGetValue(name, out r) ? r : null;
    }

    public static HotkeyTable Load(string dir, Action<string> log)
    {
        string defaultText;
        string bindingsText = null;
        string source;
        string dfile = string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, DefaultResource);
        if (dfile != null && File.Exists(dfile))
        {
            defaultText = Decode(File.ReadAllBytes(dfile));
            string bfile = Path.Combine(dir, BindingsResource);
            if (File.Exists(bfile)) bindingsText = Decode(File.ReadAllBytes(bfile));
            source = "dir=" + dir;
        }
        else
        {
            defaultText = ReadResource(DefaultResource);
            bindingsText = ReadResource(BindingsResource);
            source = "embedded (proof/movement/extracted snapshot)";
        }
        HotkeyTable t = new HotkeyTable();
        if (defaultText != null) t.ParseDefault(defaultText);
        if (bindingsText != null) t.ParseBindings(bindingsText);
        // per-role user overrides: hotkey_newlast.txt (name \t context \t
        // index \t key; index 1/2; empty key = unbound). Decoded in
        // docs/controls/RESEARCH_RESOLVED_GAPS.md §1 - never written here.
        // The dir may contain ONLY the override file (real role dirs do not
        // ship the base tables): apply it over whichever base source loaded.
        string ufile = string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "hotkey_newlast.txt");
        if (ufile != null && File.Exists(ufile))
            t.Overrides = t.ParseOverrides(Decode(File.ReadAllBytes(ufile)));
        if (log != null)
            log("hotkeys: source=" + source + " rows=" + t.rows.Count +
                " commands=" + t.byName.Count + " overrides=" + t.Overrides);
        return t;
    }

    private int ParseOverrides(string text)
    {
        int n = 0;
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0 || line[0] == ';') continue;
            string[] f = line.Split('\t');
            if (f.Length < 4) continue;
            string name = f[0].Trim();
            int index;
            if (!int.TryParse(f[2].Trim(), out index)) continue;
            if (index != 1 && index != 2) continue;
            HotkeyRow r;
            if (!byName.TryGetValue(name, out r)) continue;
            int key = ParseKey(f[3]);
            if (index == 1) r.Key1 = key; else r.Key2 = key;
            n++;
        }
        return n;
    }

    // Commands bound to this key + modifier state (may be several rows; the
    // real table is a flat list, e.g. MOVEFORWARD on W and Up). Only rows in
    // the active context fire ("" = normal play).
    public List<string> Match(int vk, bool ctrl, bool shift, bool alt)
    {
        List<string> res = new List<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            HotkeyRow r = rows[i];
            if (!string.Equals(r.Context, Context, StringComparison.OrdinalIgnoreCase))
                continue;
            if (KeyMatches(r.Name, r.Key1, vk, ctrl, shift, alt) ||
                KeyMatches(r.Name, r.Key2, vk, ctrl, shift, alt))
                res.Add(r.Name);
        }
        return res;
    }

    private static bool KeyMatches(string name, int code, int vk, bool ctrl, bool shift, bool alt)
    {
        if (code == 0) return false;
        if ((code & 0xFFFF) != vk) return false;
        int mods = (code >> 16) & 0xFFFF;
        int want = (ctrl ? 1 : 0) | (shift ? 2 : 0) | (alt ? 4 : 0);
        if (IgnoresShift(name)) { mods &= ~2; want &= ~2; }
        return mods == want;
    }

    private static bool IgnoresShift(string name)
    {
        for (int i = 0; i < ShiftIgnored.Length; i++)
            if (string.Equals(ShiftIgnored[i], name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void ParseDefault(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            string[] f = line.Split('\t');
            string name = f[0].Trim();
            if (name.Length == 0 || name == "name") continue;
            HotkeyRow r = new HotkeyRow();
            r.Name = name;
            if (f.Length > 1) r.Context = f[1].Trim();
            if (f.Length > 2) r.Key1 = ParseKey(f[2]);
            if (f.Length > 3) r.Key2 = ParseKey(f[3]);
            rows.Add(r);
            if (!byName.ContainsKey(name)) byName.Add(name, r);
        }
    }

    private void ParseBindings(string text)
    {
        HotkeyRow cur = null;
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == ';') continue;
            if (line[0] == '[' && line[line.Length - 1] == ']')
            {
                string name = line.Substring(1, line.Length - 2).Trim();
                cur = Get(name);
                if (cur == null) { cur = new HotkeyRow(); cur.Name = name; byName.Add(name, cur); }
                continue;
            }
            int eq = line.IndexOf('=');
            if (cur == null || eq <= 0) continue;
            string k = line.Substring(0, eq).Trim();
            string v = line.Substring(eq + 1).Trim();
            if (k == "down") cur.Down = v;
            else if (k == "up") cur.Up = v;
        }
    }

    private static int ParseKey(string s)
    {
        int v;
        if (int.TryParse(s.Trim(), out v)) return v;
        return 0;
    }

    private static string ReadResource(string name)
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream st = asm.GetManifestResourceStream(name))
            {
                if (st == null) return null;
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = st.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                    return Decode(ms.ToArray());
                }
            }
        }
        catch { return null; }
    }

    // Game text is GBK; the tables may also be pure ASCII/UTF-8.
    private static string Decode(byte[] bytes)
    {
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.GetEncoding(936).GetString(bytes); }
    }

    public static string Describe(HotkeyRow r)
    {
        if (r == null) return "(none)";
        string s = KeyName(r.Key1);
        if (r.Key2 != 0) s += " / " + KeyName(r.Key2);
        return s;
    }

    // Decoded display name for logs (same rule as tools/controls/hotkey_parse.py).
    public static string KeyName(int code)
    {
        if (code == 0) return "";
        int vk = code & 0xFFFF;
        int mods = (code >> 16) & 0xFFFF;
        string prefix = "";
        if ((mods & 0x4) != 0) prefix += "Alt+";
        if ((mods & 0x2) != 0) prefix += "Shift+";
        if ((mods & 0x1) != 0) prefix += "Ctrl+";
        string name;
        if (vk == 1) name = "LMB";
        else if (vk == 2) name = "RMB";
        else if (vk == 256) name = "WheelUp";
        else if (vk == 257) name = "WheelDown";
        else if (vk >= 65 && vk <= 90) name = ((char)vk).ToString();
        else if (vk >= 48 && vk <= 57) name = ((char)vk).ToString();
        else if (vk >= 112 && vk <= 123) name = "F" + (vk - 111);
        else if (vk == 32) name = "Space";
        else if (vk == 37) name = "Left";
        else if (vk == 38) name = "Up";
        else if (vk == 39) name = "Right";
        else if (vk == 40) name = "Down";
        else if (vk == 111) name = "Num/";
        else if (vk == 106) name = "Num*";
        else if (vk == 107) name = "Num+";
        else if (vk == 109) name = "Num-";
        else if (vk == 110) name = "Num.";
        else if (vk == 144) name = "NumLock";
        else if (vk == 191) name = "/";
        else if (vk == 186) name = ";";
        else if (vk == 187) name = "=";
        else if (vk == 188) name = ",";
        else if (vk == 189) name = "-";
        else if (vk == 190) name = ".";
        else if (vk == 192) name = "`";
        else if (vk == 219) name = "[";
        else if (vk == 220) name = "\\";
        else if (vk == 221) name = "]";
        else if (vk == 222) name = "'";
        else name = "VK" + vk;
        return prefix + name;
    }
}
