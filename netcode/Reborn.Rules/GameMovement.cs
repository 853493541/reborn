using System;

namespace Reborn.Rules
{
    /// <summary>Object/foliage collision contract (the client's FoliageCollision implements it).</summary>
    public interface ICollision
    {
        float SupportHeight(float x, float z, float yLow, float yHigh);
        bool Resolve(ref float px, ref float py, ref float pz, float radius, float height,
                     ref float ground, ref bool grounded);
    }

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

        public const double JumpScale = 0.52;        // client RC_JUMP_SCALE default (provisional tuning)
        public const double JumpVyBase = 15.0;       // trip[1] * 15 * scale -> u/s
        public const double JumpGravityBase = 225.0; // trip[2] * 225 * scale -> u/s^2
        public const double TurnRate = Math.PI;      // charTurnRate fallback (camera row default)
        public const double HardTurnAngle = 2.0071;  // > 112.5 deg halves speed + turn step
        public const double SlopeBlock = 70.0;       // terrain step limit (u)
        public const double LedgeDrop = 150.0;       // grounded -> airborne drop (u)
        public const double PlayerRadius = 25.0;
        public const double PlayerHeight = 170.0;
        public const int MaxJumpCount = 2;           // ground jump + one flip/double jump

        /// <summary>
        /// The client's per-frame movement block, ported for server simulation
        /// (RebornClient main loop: turn model, slope block, object collision,
        /// grounded/ledge, jump triple (school 0 J0 = 90/11), gravity).
        /// With Ground=null and Collision=null this reduces to the reference move
        /// (flat plane, horizontal only), so parity vectors stay valid.
        /// </summary>
        public static void StepEntity(Entity e, Func<double, double, double> groundAt, ICollision col, double dt, double turnRate)
        {
            double ground = groundAt != null ? groundAt(e.Pos[0], e.Pos[2]) : e.Pos[1];
            double oldX = e.Pos[0], oldZ = e.Pos[2];
            if (e.Keys != 0)
            {
                double sp = e.Run ? RunSpeed : WalkSpeed;
                double ux, uz;
                Movement.WorldDir(e.Keys, e.CamFx, e.CamFz, out ux, out uz);
                double heading = Math.Atan2(ux, uz);
                double dYaw = heading - e.Yaw;
                while (dYaw > Math.PI) dYaw -= 2.0 * Math.PI;
                while (dYaw < -Math.PI) dYaw += 2.0 * Math.PI;
                bool hard = Math.Abs(dYaw) > HardTurnAngle;
                if (hard) sp *= 0.5;
                double turnStep = turnRate * dt * (hard ? 0.5 : 1.0);
                if (Math.Abs(dYaw) <= turnStep) e.Yaw = heading;
                else e.Yaw += Math.Sign(dYaw) * turnStep;
                double step = sp * dt;
                double tryX = e.Pos[0] + ux * step, tryZ = e.Pos[2] + uz * step;
                double gh = groundAt != null ? groundAt(tryX, tryZ) : ground;
                if (gh - ground > SlopeBlock)
                {
                    double gx2 = groundAt != null ? groundAt(tryX, e.Pos[2]) : ground;
                    double gz2 = groundAt != null ? groundAt(e.Pos[0], tryZ) : ground;
                    if (gx2 - ground <= SlopeBlock) e.Pos[0] = tryX;
                    else if (gz2 - ground <= SlopeBlock) e.Pos[2] = tryZ;
                }
                else { e.Pos[0] = tryX; e.Pos[2] = tryZ; }
            }
            if (col != null)
            {
                float px = (float)e.Pos[0], py = (float)e.Pos[1], pz = (float)e.Pos[2];
                float g = (float)ground;
                bool gr = e.Grounded;
                float stepGround = col.SupportHeight(px, pz, py - 20f, py + 70f);
                if (e.Keys != 0)
                {
                    double ux, uz;
                    Movement.WorldDir(e.Keys, e.CamFx, e.CamFz, out ux, out uz);
                    for (int si = 1; si <= 3; si++)
                    {
                        float sd = (float)PlayerRadius + si * 25f;
                        float sh2 = col.SupportHeight(px + (float)ux * sd, pz + (float)uz * sd, py - 20f, py + 70f);
                        if (sh2 > stepGround) stepGround = sh2;
                    }
                }
                if (stepGround > g) g = stepGround;
                else
                {
                    col.Resolve(ref px, ref py, ref pz, (float)PlayerRadius, (float)PlayerHeight, ref g, ref gr);
                    if (gr)
                    {
                        float sh = col.SupportHeight(px, pz, py - 150f, py + 60f);
                        if (sh > g) g = sh;
                    }
                }
                e.Pos[0] = px; e.Pos[1] = py; e.Pos[2] = pz;
                ground = g;
                e.Grounded = gr;
            }
            if (e.Grounded)
            {
                if (e.Pos[1] - ground > LedgeDrop) { e.Grounded = false; e.Vy = 0.0; }
                else if (e.Pos[1] > ground) e.Pos[1] = ground;
                else if (ground - e.Pos[1] <= SlopeBlock) e.Pos[1] = ground;
            }
            if (e.JumpRequest)
            {
                e.JumpRequest = false;
                if (e.Grounded) e.JumpCount = 0;
                int next = e.JumpCount + 1;
                if (next <= MaxJumpCount)
                {
                    e.JumpCount = next;
                    e.Vy = 90.0 * JumpVyBase * JumpScale;
                    e.Gravity = 11.0 * JumpGravityBase * JumpScale;
                    e.Grounded = false;
                }
            }
            if (!e.Grounded)
            {
                e.Vy -= e.Gravity * dt;
                e.Pos[1] += e.Vy * dt;
                if (e.Pos[1] <= ground)
                {
                    e.Pos[1] = ground;
                    if (e.Vy < 0.0) e.Vy = 0.0;
                    e.Grounded = true;
                    e.JumpCount = 0;
                }
            }
            else e.JumpCount = 0;
            e.Vel = new double[] { (e.Pos[0] - oldX) / dt, e.Vy, (e.Pos[2] - oldZ) / dt };
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
