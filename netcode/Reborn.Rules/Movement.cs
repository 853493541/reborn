using System;

namespace Reborn.Rules
{
    /// <summary>
    /// Movement rules shared by client prediction and server simulation - the C# twin of
    /// <c>apply_input</c> in tools/netcode/reference/jx3_model.py (8-direction, normalized).
    /// The M1 client's table-driven speeds (walk 96 / run 320 u/s, gravity model) plug in
    /// here as the next slice; parity vectors pin this contract first.
    /// </summary>
    public static class Movement
    {
        public const double MoveSpeed = 5.0;
        public const int KFwd = 1;
        public const int KBack = 2;
        public const int KLeft = 4;
        public const int KRight = 8;

        public static double[] ApplyInput(double[] pos, int keys, double dt)
        {
            return ApplyInput(pos, keys, dt, MoveSpeed);
        }

        /// <summary>Speed-explicit variant: the reborn game uses its table values (u/s).</summary>
        public static double[] ApplyInput(double[] pos, int keys, double dt, double speed)
        {
            int dx = ((keys & KRight) != 0 ? 1 : 0) - ((keys & KLeft) != 0 ? 1 : 0);
            int dz = ((keys & KFwd) != 0 ? 1 : 0) - ((keys & KBack) != 0 ? 1 : 0);
            double n = Math.Sqrt((double)dx * dx + (double)dz * dz);
            if (n == 0.0) return new double[] { pos[0], pos[1], pos[2] };
            return new double[]
            {
                pos[0] + dx / n * speed * dt,
                pos[1],
                pos[2] + dz / n * speed * dt
            };
        }

        /// <summary>Normalized input direction from the key bits (0,0 when idle).</summary>
        public static void InputDir(int keys, out double ux, out double uz)
        {
            int dx = ((keys & KRight) != 0 ? 1 : 0) - ((keys & KLeft) != 0 ? 1 : 0);
            int dz = ((keys & KFwd) != 0 ? 1 : 0) - ((keys & KBack) != 0 ? 1 : 0);
            double n = Math.Sqrt((double)dx * dx + (double)dz * dz);
            if (n == 0.0) { ux = 0.0; uz = 0.0; }
            else { ux = dx / n; uz = dz / n; }
        }

        /// <summary>
        /// World-space input direction: the camera-relative combo the client uses
        /// (RebornClient input block: W = camera forward, A/D = camera right).
        /// <paramref name="fx"/>/<paramref name="fz"/> is the camera forward vector
        /// as sent on the wire; right = (fz, -fx).
        /// </summary>
        public static void WorldDir(int keys, double fx, double fz, out double ux, out double uz)
        {
            int dx = ((keys & KRight) != 0 ? 1 : 0) - ((keys & KLeft) != 0 ? 1 : 0);
            int dz = ((keys & KFwd) != 0 ? 1 : 0) - ((keys & KBack) != 0 ? 1 : 0);
            double x = dx * fz + dz * fx;
            double z = dx * (-fx) + dz * fz;
            double n = Math.Sqrt(x * x + z * z);
            if (n == 0.0) { ux = 0.0; uz = 0.0; }
            else { ux = x / n; uz = z / n; }
        }

        public static double Dist(double[] a, double[] b)
        {
            double dx = a[0] - b[0];
            double dy = a[1] - b[1];
            double dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
