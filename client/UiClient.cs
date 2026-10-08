// UiClient.cs — render the client's own target HUD (TargetTarget.ini + real atlases).
//
// Every pixel comes from the game client; nothing is hand-drawn:
//   layout : ui/Config/Default/TargetTarget.ini (the selected-target window,
//            ScriptFile=UI\Config\Default\Target.lua): section tree, Left/Top/
//            Width/Height, PosType, Image/Frame/ImageType/Alpha, FontScheme
//   art    : the .UITex atlases + their .Tga/.dds textures, extracted from the
//            client PakV4 by tools/netcode/ui/extract_target_frame.py
//   fonts  : ui/Font/*.ttf loaded read-only from the game client install, sized
//            and coloured through ui/Scheme/Elem/{font.ini,fontlist.ini,color.txt}
// Missing assets are skipped and logged; no substitute art is drawn.
//
// Runtime state mirrors Target.lua:
//   * Image_Health width = authored width x HP percent (SetPercentage)
//   * Text_Target / Text_Level / Text_Health carry the live values
//   * mana row + school icon show for player targets only (UpdateEnergy/UpdateHead)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text;

// .UITex: 92-byte header ('UI', texW@4, texH@8, frameCount@12, groupCount@16,
// 64-byte texture name @24), then 20-byte frame records (x,y,w,h,flag).
internal sealed class UiTexAtlas
{
    readonly int[] fx, fy, fw, fh;
    readonly string textureFile;
    readonly Dictionary<int, Bitmap> crops = new Dictionary<int, Bitmap>();
    Bitmap texture;

    UiTexAtlas(byte[] b, string textureFile)
    {
        this.textureFile = textureFile;
        int count = Math.Max(0, BitConverter.ToInt32(b, 12));
        fx = new int[count]; fy = new int[count]; fw = new int[count]; fh = new int[count];
        int kept = count;
        for (int i = 0; i < count; i++)
        {
            int o = 92 + i * 20;
            if (o + 20 > b.Length) { kept = i; break; }
            fx[i] = BitConverter.ToInt32(b, o);
            fy[i] = BitConverter.ToInt32(b, o + 4);
            fw[i] = BitConverter.ToInt32(b, o + 8);
            fh[i] = BitConverter.ToInt32(b, o + 12);
        }
        if (kept != count) { Array.Resize(ref fx, kept); Array.Resize(ref fy, kept); Array.Resize(ref fw, kept); Array.Resize(ref fh, kept); }
    }

    public static UiTexAtlas Load(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        if (b.Length < 92 || b[0] != (byte)'U' || b[1] != (byte)'I')
            throw new InvalidDataException("not a UITex: " + path);
        string name = Encoding.ASCII.GetString(b, 24, 64).TrimEnd('\0');
        string dir = Path.GetDirectoryName(path);
        string texPath = Path.Combine(dir, name);
        if (!File.Exists(texPath))
        {
            string stem = Path.GetFileNameWithoutExtension(name);
            string tga = Path.Combine(dir, stem + ".tga");
            string dds = Path.Combine(dir, stem + ".dds");
            if (File.Exists(tga)) texPath = tga;
            else if (File.Exists(dds)) texPath = dds;
        }
        return new UiTexAtlas(b, texPath);
    }

    Bitmap Texture()
    {
        if (texture == null) texture = TextureDecode.Load(textureFile);
        return texture;
    }

    public Bitmap GetFrame(int index)
    {
        if (index < 0 || index >= fw.Length || fw[index] <= 0 || fh[index] <= 0) return null;
        Bitmap hit;
        if (crops.TryGetValue(index, out hit)) return hit;
        Bitmap tex = Texture();
        var rect = new Rectangle(fx[index], fy[index], fw[index], fh[index]);
        if (rect.X < 0 || rect.Y < 0 || rect.Right > tex.Width || rect.Bottom > tex.Height) return null;
        hit = tex.Clone(rect, PixelFormat.Format32bppArgb);
        crops[index] = hit;
        return hit;
    }
}

internal static class TextureDecode
{
    public static Bitmap Load(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        if (b.Length >= 4 && b[0] == (byte)'D' && b[1] == (byte)'D' && b[2] == (byte)'S' && b[3] == (byte)' ')
        {
            int h = BitConverter.ToInt32(b, 12), w = BitConverter.ToInt32(b, 16);
            return FromBgra(DecodeDds(b, w, h), w, h);
        }
        int tw = b[12] | (b[13] << 8), th = b[14] | (b[15] << 8);
        return FromBgra(DecodeTga(b, tw, th), tw, th);
    }

    static Bitmap FromBgra(byte[] pixels, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < h; y++)
                System.Runtime.InteropServices.Marshal.Copy(pixels, y * w * 4,
                    System.IntPtr.Add(data.Scan0, y * data.Stride), w * 4);
        }
        finally { bmp.UnlockBits(data); }
        return bmp;
    }

    static void Put(byte[] px, int w, int h, bool topDown, int index, byte[] src, int si, int bppx, bool gray)
    {
        int row = index / w, col = index % w;
        int y = topDown ? row : h - 1 - row;
        int di = (y * w + col) * 4;
        if (gray) { byte g = src[si]; px[di] = g; px[di + 1] = g; px[di + 2] = g; px[di + 3] = 255; }
        else
        {
            px[di] = src[si]; px[di + 1] = src[si + 1]; px[di + 2] = src[si + 2];
            px[di + 3] = bppx >= 4 ? src[si + 3] : (byte)255;
        }
    }

    static byte[] DecodeTga(byte[] b, int w, int h)
    {
        if (b.Length < 18) throw new InvalidDataException("TGA too small");
        int idLength = b[0], cmap = b[1], type = b[2];
        int bpp = b[16], desc = b[17];
        if (cmap != 0 || (type != 2 && type != 3 && type != 10 && type != 11))
            throw new InvalidDataException("TGA type " + type + " unsupported");
        bool topDown = (desc & 0x20) != 0;
        int bppx = Math.Max(1, bpp / 8);
        int off = 18 + idLength, n = w * h;
        var px = new byte[n * 4];
        int written = 0, p = off;
        bool rle = type == 10 || type == 11, gray = type == 3 || type == 11;
        var pxl = new byte[bppx];
        while (written < n)
        {
            if (rle)
            {
                if (p >= b.Length) break;
                int header = b[p++], count = (header & 0x7F) + 1;
                if ((header & 0x80) != 0) { Array.Copy(b, p, pxl, 0, bppx); p += bppx; }
                for (int i = 0; i < count && written < n; i++)
                {
                    if ((header & 0x80) != 0) Put(px, w, h, topDown, written, pxl, 0, bppx, gray);
                    else { Put(px, w, h, topDown, written, b, p, bppx, gray); p += bppx; }
                    written++;
                }
            }
            else
            {
                int step = gray ? 1 : bppx;
                if (p + step > b.Length) break;
                Put(px, w, h, topDown, written, b, p, bppx, gray);
                p += step; written++;
            }
        }
        return px;
    }

    static void Set(byte[] p, int w, int h, int x, int y, byte r, byte g, byte bl, byte a)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = (y * w + x) * 4;
        p[i] = bl; p[i + 1] = g; p[i + 2] = r; p[i + 3] = a;
    }

    static void Endpoints(byte[] s, int o, out byte r0, out byte g0, out byte b0, out byte r1, out byte g1, out byte b1)
    {
        int c0 = s[o] | (s[o + 1] << 8), c1 = s[o + 2] | (s[o + 3] << 8);
        b0 = (byte)((c0 & 0x1F) * 255 / 31); g0 = (byte)(((c0 >> 5) & 0x3F) * 255 / 63); r0 = (byte)(((c0 >> 11) & 0x1F) * 255 / 31);
        b1 = (byte)((c1 & 0x1F) * 255 / 31); g1 = (byte)(((c1 >> 5) & 0x3F) * 255 / 63); r1 = (byte)(((c1 >> 11) & 0x1F) * 255 / 31);
    }

    static void Dxt1(byte[] s, int o, byte[] p, int w, int h, int x0, int y0)
    {
        byte r0, g0, b0, r1, g1, b1;
        Endpoints(s, o, out r0, out g0, out b0, out r1, out g1, out b1);
        uint bits = BitConverter.ToUInt32(s, o + 4);
        for (int j = 0; j < 4; j++)
            for (int i = 0; i < 4; i++)
            {
                int code = (int)((bits >> (2 * (j * 4 + i))) & 0x3);
                byte r, g, bb, a = 255;
                if (code == 0) { r = r0; g = g0; bb = b0; }
                else if (code == 1) { r = r1; g = g1; bb = b1; }
                else if (code == 2) { r = (byte)((2 * r0 + r1) / 3); g = (byte)((2 * g0 + g1) / 3); bb = (byte)((2 * b0 + b1) / 3); }
                else { if (r0 <= r1) { r = 0; g = 0; bb = 0; a = 0; } else { r = (byte)((r0 + 2 * r1) / 3); g = (byte)((g0 + 2 * g1) / 3); bb = (byte)((b0 + 2 * b1) / 3); } }
                Set(p, w, h, x0 + i, y0 + j, r, g, bb, a);
            }
    }

    static void Dxt5(byte[] s, int o, byte[] p, int w, int h, int x0, int y0)
    {
        int a0 = s[o], a1 = s[o + 1];
        ulong abits = 0;
        for (int k = 0; k < 6; k++) abits |= (ulong)s[o + 2 + k] << (8 * k);
        byte r0, g0, b0, r1, g1, b1;
        Endpoints(s, o + 8, out r0, out g0, out b0, out r1, out g1, out b1);
        uint cbits = BitConverter.ToUInt32(s, o + 12);
        var alpha = new int[8];
        alpha[0] = a0; alpha[1] = a1;
        if (a0 > a1) { for (int i = 1; i <= 6; i++) alpha[i + 1] = ((7 - i) * a0 + i * a1) / 7; }
        else { for (int i = 1; i <= 4; i++) alpha[i + 1] = ((5 - i) * a0 + i * a1) / 5; alpha[6] = 0; alpha[7] = 255; }
        for (int j = 0; j < 4; j++)
            for (int i = 0; i < 4; i++)
            {
                int idx = j * 4 + i;
                int ac = (int)((abits >> (3 * idx)) & 0x7), cc = (int)((cbits >> (2 * idx)) & 0x3);
                byte r, g, bb;
                if (cc == 0) { r = r0; g = g0; bb = b0; }
                else if (cc == 1) { r = r1; g = g1; bb = b1; }
                else if (cc == 2) { r = (byte)((2 * r0 + r1) / 3); g = (byte)((2 * g0 + g1) / 3); bb = (byte)((2 * b0 + b1) / 3); }
                else { r = (byte)((r0 + 2 * r1) / 3); g = (byte)((g0 + 2 * g1) / 3); bb = (byte)((b0 + 2 * b1) / 3); }
                Set(p, w, h, x0 + i, y0 + j, r, g, bb, (byte)alpha[ac]);
            }
    }

    static byte[] DecodeDds(byte[] b, int w, int h)
    {
        if (b.Length < 128) throw new InvalidDataException("DDS too small");
        string fourCc = Encoding.ASCII.GetString(b, 84, 4);
        bool dxt1 = fourCc == "DXT1", dxt5 = fourCc == "DXT5";
        if (!dxt1 && !dxt5) throw new NotSupportedException("DDS " + fourCc);
        var px = new byte[w * h * 4];
        int off = 128, bw = Math.Max(1, (w + 3) / 4), bh = Math.Max(1, (h + 3) / 4);
        int block = dxt1 ? 8 : 16;
        for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                if (off + block > b.Length) break;
                if (dxt5) Dxt5(b, off, px, w, h, bx * 4, by * 4);
                else Dxt1(b, off, px, w, h, bx * 4, by * 4);
                off += block;
            }
        return px;
    }
}

internal sealed class UiSection
{
    public string Name;
    public readonly Dictionary<string, string> Kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Get(string k) { string v; return Kv.TryGetValue(k, out v) ? v : ""; }
    public int I(string k, int def) { int v; return int.TryParse(Get(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def; }
}

// Renders the client's TargetTarget.ini target HUD with its real atlases + fonts.
internal sealed class UiTargetFrameRenderer
{
    const string LAYOUT_REL = "ui/Config/Default/TargetTarget.ini";

    readonly List<UiSection> sections = new List<UiSection>();
    readonly Dictionary<string, UiSection> byName = new Dictionary<string, UiSection>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, UiTexAtlas> atlases = new Dictionary<string, UiTexAtlas>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Color> colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Dictionary<string, string>> schemeBlocks = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Dictionary<string, string>> fontList = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    readonly PrivateFontCollection fontFiles = new PrivateFontCollection();
    readonly List<string> loadedFontFiles = new List<string>();
    public readonly List<string> Warnings = new List<string>();

    readonly string uiRoot, fontDir;
    public int Width = 325, Height = 115;
    public int OriginX = 500, OriginY = 124;   // authored default window position
    public int DebugSections { get { return sections.Count; } }
    public int DebugImages;

    public UiTargetFrameRenderer(string uiRoot, string fontDir, string schemeDir)
    {
        this.uiRoot = uiRoot;
        this.fontDir = fontDir;
        LoadLayout(Path.Combine(uiRoot, LAYOUT_REL.Replace('/', Path.DirectorySeparatorChar)));
        LoadColors(Path.Combine(schemeDir, "color.txt"));
        LoadBlockIni(Path.Combine(schemeDir, "font.ini"), schemeBlocks);
        LoadBlockIni(Path.Combine(schemeDir, "fontlist.ini"), fontList);
    }

    void LoadLayout(string ini)
    {
        if (!File.Exists(ini)) { Warnings.Add("missing layout " + ini); return; }
        UiSection cur = null;
        foreach (string raw in File.ReadAllLines(ini, Encoding.GetEncoding(936)))
        {
            string s = raw.Trim();
            if (s.Length == 0 || s[0] == ';') continue;
            if (s[0] == '[' && s[s.Length - 1] == ']')
            {
                cur = new UiSection();
                cur.Name = s.Substring(1, s.Length - 2);
                sections.Add(cur);
                byName[cur.Name] = cur;
                continue;
            }
            if (cur == null) continue;
            int eq = s.IndexOf('=');
            if (eq <= 0) continue;
            cur.Kv[s.Substring(0, eq).Trim()] = s.Substring(eq + 1).Trim();
        }
        UiSection root;
        if (byName.TryGetValue("TargetTarget", out root))
        {
            Width = root.I("Width", Width);
            Height = root.I("Height", Height);
            OriginX = root.I("Left", OriginX);
            OriginY = root.I("Top", OriginY);
        }
    }

    void LoadColors(string path)
    {
        if (!File.Exists(path)) return;
        string[] lines = File.ReadAllLines(path, Encoding.GetEncoding(936));
        for (int i = 0; i < lines.Length; i++)
        {
            string[] c = lines[i].Split('\t');
            if (c.Length < 4) continue;
            int r, g, b;
            if (int.TryParse(c[1].Trim(), out r) && int.TryParse(c[2].Trim(), out g) && int.TryParse(c[3].Trim(), out b))
                colors[c[0].Trim()] = Color.FromArgb(r, g, b);
        }
    }

    static void LoadBlockIni(string path, Dictionary<string, Dictionary<string, string>> into)
    {
        if (!File.Exists(path)) return;
        string cur = null;
        var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in File.ReadAllLines(path, Encoding.GetEncoding(936)))
        {
            string s = raw.Trim();
            if (s.Length == 0 || s[0] == ';') continue;
            if (s[0] == '[' && s[s.Length - 1] == ']')
            {
                if (cur != null) into[cur] = new Dictionary<string, string>(kv, StringComparer.OrdinalIgnoreCase);
                kv.Clear();
                cur = s.Substring(1, s.Length - 2);
                continue;
            }
            int eq = s.IndexOf('=');
            if (eq > 0) kv[s.Substring(0, eq).Trim()] = s.Substring(eq + 1).Trim();
        }
        if (cur != null) into[cur] = new Dictionary<string, string>(kv, StringComparer.OrdinalIgnoreCase);
    }

    Color Named(string name, Color def)
    {
        Color c;
        if (!string.IsNullOrEmpty(name) && colors.TryGetValue(name, out c)) return c;
        return def;
    }

    UiTexAtlas Atlas(string logical)
    {
        UiTexAtlas a;
        if (atlases.TryGetValue(logical, out a)) return a;
        try
        {
            string p = Path.Combine(uiRoot, logical.Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));
            a = File.Exists(p) ? UiTexAtlas.Load(p) : null;
            if (a == null) Warnings.Add("atlas missing: " + logical);
        }
        catch (Exception e) { Warnings.Add("atlas " + logical + ": " + e.Message); a = null; }
        atlases[logical] = a;
        return a;
    }

    // --- layout: port of the repo's UiLayout placement rules for the cases this
    // window uses (PosType 0 default, 8 right-aligned in window) ---
    bool AbsPos(UiSection s, out double x, out double y)
    {
        var chain = new List<UiSection>();
        UiSection cur = s;
        for (int i = 0; i < 16; i++)
        {
            chain.Insert(0, cur);
            string p;
            if (!cur.Kv.TryGetValue("._Parent", out p) || p.Length == 0 || p == "Normal") break;
            UiSection parent;
            if (!byName.TryGetValue(p, out parent)) break;
            cur = parent;
        }
        double ax = 0, ay = 0;
        for (int i = 0; i < chain.Count; i++)
        {
            UiSection e = chain[i];
            double lx = e.I("Left", 0), ly = e.I("Top", 0);
            double ew = e.I("Width", 0), eh = e.I("Height", 0);
            // The root section's Left/Top is the window position (OriginX/OriginY),
            // not a content offset; the control already sits at the origin.
            if (i > 0)
            {
                if (e.I("PosType", 0) == 8 && lx == 0)
                    lx = Width - ew - ax;   // right-aligned in window (UiLayout rule)
            }
            else
            {
                lx = 0; ly = 0;
            }
            ax += lx; ay += ly;
        }
        x = ax; y = ay;
        return chain.Count > 0;
    }

    bool IsDrawn(string name)
    {
        // Runtime-state elements not simulated yet (shop preview, danger/party
        // marks, damage flash, cast bar, avatar face which Target.lua sets at
        // runtime via .area frames).
        string[] skip = {
            "Image_BuyBG1", "Image_BuyBG2", "Image_BuyBG3", "Animate_Whole",
            "Image_Avatar", "Animate_Avatar", "Image_Target", "Image_NewTarget",
            "Image_Danger", "Image_NPCMark", "Image_Invincible",
            "Image_HPmark", "Image_BarShieldBg", "Image_Bg", "Image_Progress",
            "Image_FlashS", "Image_FlashF", "Text_Name",
            "Image_CMNormal", "Image_CMOver", "Text_CMName",
            "Image_Cloud",
        };
        foreach (string s in skip) if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public void Render(Graphics g, string name, int level, long hp, long maxHp, bool playerTarget)
    {
        g.SmoothingMode = SmoothingMode.None;
        DebugImages = 0;
        foreach (UiSection s in sections)
        {
            string type = s.Get("._WndType");
            if (type != "Image" && type != "Text") continue;
            if (type == "Image" && !IsDrawn(s.Name)) continue;
            if (type == "Text" && s.Name != "Text_Target" && s.Name != "Text_Level" &&
                s.Name != "Text_Health" && s.Name != "Text_Mana") continue;
            if (s.Name == "Image_Mana" || s.Name == "Text_Mana") continue;   // player-only row
            if (s.Name == "Image_School" && !playerTarget) continue;         // school icon: players
            if (s.Name == "Image_SubHealth" && hp >= maxHp) continue;        // shield overlay

            double px, py;
            if (!AbsPos(s, out px, out py)) continue;
            double w = s.I("Width", 0), h = s.I("Height", 0);

            if (type == "Image")
            {
                string image = s.Get("Image");
                if (image.Length == 0) continue;   // .area-driven / runtime-set
                UiTexAtlas a = Atlas(image);
                if (a == null) continue;
                Bitmap bmp = a.GetFrame(Math.Max(0, s.I("Frame", 0)));
                if (bmp == null) { Warnings.Add("frame missing: " + s.Name + " " + image + " f" + s.I("Frame", 0)); continue; }
                double dw = w > 0 ? w : bmp.Width, dh = h > 0 ? h : bmp.Height;
                if (s.Name == "Image_Health")
                    dw = (w > 0 ? w : bmp.Width) * (maxHp > 0 ? Math.Max(0.0, Math.Min(1.0, (double)hp / maxHp)) : 0.0);
                DrawImage(g, bmp, (float)px, (float)py, (float)dw, (float)dh, s.I("Alpha", 255));
                DebugImages++;
            }
            else
            {
                float size; string file; Color fill, border;
                ResolveFont(s.Get("FontScheme"), out size, out file, out fill, out border);
                string text = null;
                if (s.Name == "Text_Target") text = name;
                else if (s.Name == "Text_Level") text = level > 0 ? level.ToString() : "";
                else if (s.Name == "Text_Health") text = hp + "/" + maxHp;
                if (string.IsNullOrEmpty(text)) continue;
                DrawText(g, text, (float)px, (float)py, (float)(w > 0 ? w : 100), (float)(h > 0 ? h : 16),
                    size, file, fill, border, s.I("HAlign", 0), s.I("VAlign", 0), s.I("BorderSize", 0));
            }
        }
    }

    void ResolveFont(string scheme, out float size, out string file, out Color fill, out Color border)
    {
        size = 14f; file = null; fill = Color.White; border = Color.Black;
        if (scheme.Length == 0) return;
        Dictionary<string, string> fb;
        if (!schemeBlocks.TryGetValue(scheme, out fb)) return;
        string v;
        if (fb.TryGetValue("Color", out v)) fill = Named(v, fill);
        if (fb.TryGetValue("BorderColor", out v)) border = Named(v, border);
        string fontId;
        if (!fb.TryGetValue("FontID", out fontId)) return;
        Dictionary<string, string> ff;
        if (!fontList.TryGetValue(fontId, out ff)) return;
        if (ff.TryGetValue("Size", out v)) { float f; if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) size = f; }
        if (ff.TryGetValue("File", out v) && v.Length > 0)
            file = Path.Combine(fontDir, Path.GetFileName(v.Replace('/', Path.DirectorySeparatorChar)));
    }

    Font GetFont(string file, float size)
    {
        if (!string.IsNullOrEmpty(file) && File.Exists(file) && loadedFontFiles.IndexOf(file) < 0)
        {
            try { fontFiles.AddFontFile(file); loadedFontFiles.Add(file); }
            catch (Exception e) { Warnings.Add("font " + file + ": " + e.Message); }
        }
        try
        {
            if (fontFiles.Families.Length > 0) return new Font(fontFiles.Families[0], size, FontStyle.Regular, GraphicsUnit.Pixel);
        }
        catch { }
        return new Font("Microsoft YaHei", size, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    void DrawImage(Graphics g, Bitmap bmp, float x, float y, float w, float h, int alpha)
    {
        if (w <= 0 || h <= 0) return;
        var old = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.Bilinear;
        if (alpha >= 255) g.DrawImage(bmp, x, y, w, h);
        else
        {
            var cm = new ColorMatrix();
            cm.Matrix33 = alpha / 255f;
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(bmp, new Rectangle((int)x, (int)y, (int)w, (int)h),
                    0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
            }
        }
        g.InterpolationMode = old;
    }

    void DrawText(Graphics g, string text, float x, float y, float w, float h,
                  float size, string file, Color fill, Color border, int hAlign, int vAlign, int borderSize)
    {
        using (Font f = GetFont(file, size))
        using (var sf = new StringFormat())
        {
            sf.Alignment = hAlign == 1 ? StringAlignment.Center : hAlign == 2 ? StringAlignment.Far : StringAlignment.Near;
            sf.LineAlignment = vAlign == 2 ? StringAlignment.Far : vAlign == 1 ? StringAlignment.Center : StringAlignment.Near;
            var rect = new RectangleF(x, y, w, h);
            if (borderSize > 0)
            {
                using (var b = new SolidBrush(border))
                {
                    for (int dx = -borderSize; dx <= borderSize; dx++)
                        for (int dy = -borderSize; dy <= borderSize; dy++)
                            if (dx != 0 || dy != 0)
                                g.DrawString(text, f, b, new RectangleF(x + dx, y + dy, w, h), sf);
                }
            }
            using (var t = new SolidBrush(fill))
                g.DrawString(text, f, t, rect, sf);
        }
    }
}
