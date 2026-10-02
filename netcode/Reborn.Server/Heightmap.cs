using System;
using System.Globalization;
using System.IO;

namespace Reborn.Server
{
    /// <summary>
    /// Baked terrain heightfield (text grid) sampled by the headless server. The grid is
    /// produced by the reborn client itself (RC_BAKE_HF) through the game's own terrain
    /// data loader - the server cannot init the game VFS on its own.
    /// Format: header "origin &lt;x0&gt; &lt;z0&gt; step &lt;s&gt; nx &lt;n&gt; nz &lt;m&gt;" then nz rows of nx floats.
    /// </summary>
    internal sealed class Heightmap
    {
        float x0, z0, step;
        int nx, nz;
        float[] h;

        public static Heightmap Load(string path, out string err)
        {
            err = null;
            try
            {
                Heightmap m = new Heightmap();
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 2) { err = "empty heightmap"; return null; }
                string[] head = lines[0].Split(' ');
                if (head.Length != 8 || head[0] != "origin")
                {
                    err = "bad heightmap header";
                    return null;
                }
                m.x0 = float.Parse(head[1], CultureInfo.InvariantCulture);
                m.z0 = float.Parse(head[2], CultureInfo.InvariantCulture);
                m.step = float.Parse(head[4], CultureInfo.InvariantCulture);
                m.nx = int.Parse(head[6], CultureInfo.InvariantCulture);
                m.nz = int.Parse(head[8 - 1], CultureInfo.InvariantCulture);
                if (lines.Length < 1 + m.nz) { err = "truncated heightmap"; return null; }
                m.h = new float[m.nx * m.nz];
                for (int j = 0; j < m.nz; j++)
                {
                    string[] row = lines[1 + j].Split('\t');
                    for (int i = 0; i < m.nx && i < row.Length; i++)
                        m.h[j * m.nx + i] = float.Parse(row[i], CultureInfo.InvariantCulture);
                }
                return m;
            }
            catch (Exception e)
            {
                err = e.Message;
                return null;
            }
        }

        public int Nx { get { return nx; } }
        public int Nz { get { return nz; } }
        public double Step { get { return step; } }

        public double Sample(double x, double z)
        {
            double gx = (x - x0) / step;
            double gz = (z - z0) / step;
            if (gx < 0) gx = 0;
            if (gz < 0) gz = 0;
            if (gx > nx - 1) gx = nx - 1;
            if (gz > nz - 1) gz = nz - 1;
            int i0 = (int)Math.Floor(gx), j0 = (int)Math.Floor(gz);
            int i1 = i0 + 1; if (i1 > nx - 1) i1 = nx - 1;
            int j1 = j0 + 1; if (j1 > nz - 1) j1 = nz - 1;
            double fx = gx - i0, fz = gz - j0;
            double h00 = h[j0 * nx + i0], h10 = h[j0 * nx + i1];
            double h01 = h[j1 * nx + i0], h11 = h[j1 * nx + i1];
            double a = h00 + (h10 - h00) * fx;
            double b = h01 + (h11 - h01) * fx;
            return a + (b - a) * fz;
        }
    }
}
