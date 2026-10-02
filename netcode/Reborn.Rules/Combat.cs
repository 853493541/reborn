using System.Collections.Generic;

namespace Reborn.Rules
{
    public sealed class SkillSpec
    {
        public int Id;
        public int CastMs;
        public int CdMs;
        public bool MoveForbidden;
        public double Range;

        public SkillSpec(int id, int castMs, int cdMs, bool moveForbidden, double range)
        {
            Id = id;
            CastMs = castMs;
            CdMs = cdMs;
            MoveForbidden = moveForbidden;
            Range = range;
        }
    }

    /// <summary>
    /// Server-authoritative cast rules - the C# twin of <c>Peer._cast</c> in
    /// tools/netcode/reference/jx3_model.py. Decisions (ok / reject code) are parity-vectored;
    /// damage numbers stay placeholder until the skill-data slice lands
    /// (docs/netcode/SKILL_DATA_RESEARCH.md).
    /// </summary>
    public static class Combat
    {
        public const int RejectMoveState = 1;
        public const int RejectCooldown = 2;
        public const int RejectRange = 3;

        public const int Ok = 0;

        public static readonly Dictionary<int, SkillSpec> Skills = new Dictionary<int, SkillSpec>
        {
            { 1, new SkillSpec(1, 400, 2000, true, 6.0) },
            { 2, new SkillSpec(2, 200, 800, false, 6.0) },
        };

        /// <summary>
        /// Mirror of the reference validation order: unknown -> range-reject (the reference's
        /// odd label), move_forbidden while moving -> move_state, cooldown, target out of range.
        /// </summary>
        public static int ValidateCast(int skillId, bool moving, double nowS, double readyAtS, bool hasTarget, double targetDist)
        {
            SkillSpec spec;
            if (!Skills.TryGetValue(skillId, out spec)) return RejectRange;
            if (spec.MoveForbidden && moving) return RejectMoveState;
            if (nowS < readyAtS) return RejectCooldown;
            if (hasTarget && targetDist > spec.Range) return RejectRange;
            return Ok;
        }
    }
}
