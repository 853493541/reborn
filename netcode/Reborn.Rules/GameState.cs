using System.Collections.Generic;

namespace Reborn.Rules
{
    public sealed class Entity
    {
        public int Eid;
        public string Name;
        public double[] Pos = new double[] { 0.0, 0.0, 0.0 };
        public double[] Vel = new double[] { 0.0, 0.0, 0.0 };
        public int Keys;
        public int Facing;
        public int LastInputSeq;
        public double CtrlLockUntil;
        public double CastingUntil;
        public readonly Dictionary<int, double> Cooldowns = new Dictionary<int, double>();
        public readonly List<int> Buffs = new List<int>();
        public int Hp = 100;

        public bool Moving { get { return Keys != 0; } }

        public bool Locked(double nowS) { return nowS < CtrlLockUntil; }
    }

    /// <summary>
    /// Authoritative world state - the C# twin of <c>GameServer</c>'s entity model and tick in
    /// tools/netcode/reference/jx3_model.py (30 Hz, locks freeze velocity, y clamped at 0,
    /// distance-based AOI). Session/transport stays in the server app.
    /// </summary>
    public sealed class GameState
    {
        public readonly Dictionary<int, Entity> Entities = new Dictionary<int, Entity>();
        public int NextEid = 1;
        public int ServerTick;

        /// <summary>Ground speed in u/s (reference default 5.0 m; the reborn server uses GameMovement.RunSpeed).</summary>
        public double MoveSpeed = Movement.MoveSpeed;

        /// <summary>Where new entities spawn (the reborn server sets the map spawn).</summary>
        public double[] SpawnPos = new double[] { 0.0, 0.0, 0.0 };

        public Entity Spawn(string name)
        {
            Entity ent = new Entity();
            ent.Eid = NextEid++;
            ent.Name = name;
            ent.Pos = new double[] { SpawnPos[0], SpawnPos[1], SpawnPos[2] };
            Entities[ent.Eid] = ent;
            return ent;
        }

        /// <summary>One authoritative tick (dt = 1/TickHz at call sites).</summary>
        public void Step(double nowS, double dt)
        {
            ServerTick++;
            foreach (Entity ent in Entities.Values)
            {
                if (ent.Locked(nowS) || nowS < ent.CastingUntil)
                {
                    ent.Vel = new double[] { 0.0, 0.0, 0.0 };
                    continue;
                }
                double[] old = ent.Pos;
                ent.Pos = Movement.ApplyInput(ent.Pos, ent.Keys, dt, MoveSpeed);
                ent.Vel = new double[] { (ent.Pos[0] - old[0]) / dt, 0.0, (ent.Pos[2] - old[2]) / dt };
                if (ent.Pos[1] < 0.0)
                {
                    ent.Pos = new double[] { ent.Pos[0], 0.0, ent.Pos[2] };
                }
            }
        }

        /// <summary>Distance-based interest management (AOI_RANGE), excluding the observer.</summary>
        public List<Entity> Visible(Entity observer)
        {
            List<Entity> outList = new List<Entity>();
            foreach (Entity e in Entities.Values)
            {
                if (e.Eid == observer.Eid) continue;
                if (Movement.Dist(e.Pos, observer.Pos) <= Protocol.AoiRange) outList.Add(e);
            }
            return outList;
        }
    }
}
