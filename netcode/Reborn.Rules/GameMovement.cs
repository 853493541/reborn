using System;

namespace Reborn.Rules
{
    /// <summary>
    /// Game-unit movement constants and the exact integer jump/gravity/fall model
    /// (docs/movement/REBORN_JUMP_FALL_SPEC.md §3, verified by tools/gravity/verify_model.py).
    /// Units: 1 u = 1 cm, 1 m = 192 u, 1 尺 = 64 u; logic tick 1/15 s; velocities in u/frame.
    /// </summary>
    public static class GameMovement
    {
        public const double TickS = 1.0 / 15.0;
        public const double UnitsPerM = 192.0;
        public const int MaxChainGravity = 31;

        /// <summary>Ground speeds from the client tables (number.krl.txt): walk 96 / run 320 u/s.</summary>
        public const double WalkSpeed = 96.0;
        public const double RunSpeed = 320.0;

        public struct JumpResult
        {
            public double ApexUnits;
            public int ApexFrame;
            public int AirFrames;
        }

        /// <summary>
        /// Order B integration (y += v; v -= g) matching KCharacter::JumpTo's compensation
        /// vz = dz/t + g*t/2 - the C# twin of verify_model.simulate_jump.
        /// </summary>
        public static JumpResult SimulateJump(int vz0, int g, int maxFrames)
        {
            double vz = vz0;
            double y = 0.0;
            double apex = 0.0;
            int apexFrame = 0;
            int airFrames = 0;
            for (int f = 1; f <= maxFrames; f++)
            {
                y += vz;
                vz -= g;
                if (y > apex) { apex = y; apexFrame = f; }
                if (y <= 0.0 && vz < 0.0) { airFrames = f; break; }
            }
            JumpResult r;
            r.ApexUnits = apex;
            r.ApexFrame = apexFrame;
            r.AirFrames = airFrames;
            return r;
        }

        public static double UnitsPerFrameToMetersPerSecond(double v)
        {
            return v / TickS / UnitsPerM;
        }

        public static double UnitsPerFrame2ToMetersPerSecond2(double g)
        {
            return g / (TickS * TickS) / UnitsPerM;
        }
    }
}
