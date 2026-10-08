// MechanicProgram.cs — resolve an ability's authored mechanic program (P3 v2).
//
// Parses the ordered AddAttribute ops (mechanics_f1.tsv col 11) into a plan the
// client applies at cast commit: damage, CC (from functionType), buffs add/remove,
// self/target movement, child-skill casts. Ops we cannot interpret
// (EXECUTE_SCRIPT / *_WITH_PARAM) are flagged, not guessed.
using System;
using System.Collections.Generic;

internal sealed class MechanicPlan
{
    public float Damage;
    public string CcType = "";                       // Stun/Silence/Charm/Halt/Daze
    public List<string> BuffsAdd = new List<string>();
    public int BuffsRemove;
    public float SelfMovePerFrame;                   // SKILL_MOVE / DASH_* (u/frame)
    public float TargetPullPerFrame;                 // PULL (u/frame; arg commented 速度)
    public float TargetKnockPerFrame;                // REPULSED / KNOCKED_BACK_RATE (rate)
    public bool Knockdown;                           // CALL_KNOCKED_DOWN
    public List<string> ChildCasts = new List<string>();
    public bool HasScript;                           // EXECUTE_SCRIPT (not executed)

    public string Summary()
    {
        string s = "dmg=" + Damage.ToString("F1");
        if (CcType.Length > 0) s += " cc=" + CcType;
        if (BuffsAdd.Count > 0) s += " buff+=" + string.Join(",", BuffsAdd.ToArray());
        if (BuffsRemove > 0) s += " buff-=" + BuffsRemove;
        if (SelfMovePerFrame > 0f) s += " selfMove=" + SelfMovePerFrame.ToString("F0");
        if (TargetPullPerFrame > 0f) s += " pull=" + TargetPullPerFrame.ToString("F0");
        if (TargetKnockPerFrame > 0f) s += " knock=" + TargetKnockPerFrame.ToString("F0");
        if (Knockdown) s += " knockdown";
        if (ChildCasts.Count > 0) s += " child=" + string.Join(",", ChildCasts.ToArray());
        if (HasScript) s += " [script]";
        return s;
    }
}

internal static class MechanicProgram
{
    static float Num(string s)
    {
        float v;
        return float.TryParse(s, out v) ? v : 0f;
    }

    static bool IsCc(string ft)
    {
        return ft == "Stun" || ft == "Silence" || ft == "Charm" || ft == "Halt" || ft == "Daze";
    }

    public static MechanicPlan Resolve(string[] mechRow, string funcType)
    {
        var p = new MechanicPlan();
        if (mechRow == null) return p;
        p.Damage = SkillDamage.Base(mechRow);
        if (IsCc(funcType)) p.CcType = funcType;
        string ops = mechRow.Length > 11 ? mechRow[11] : "";
        string[] parts = ops.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            string[] f = parts[i].Split('|');
            if (f.Length < 2) continue;
            string t = f[1];
            string arg = f.Length > 2 ? f[2] : "";
            if (t == "CALL_BUFF") p.BuffsAdd.Add(arg);
            else if (t.StartsWith("DETACH") || t.StartsWith("DEL_") || t == "CONSUME_BUFF") p.BuffsRemove++;
            else if (t == "SKILL_MOVE" || t == "DASH" || t == "DASH_FORWARD" || t == "DASH_TO_POINT")
                p.SelfMovePerFrame = Math.Max(p.SelfMovePerFrame, Num(arg));
            else if (t == "PULL") p.TargetPullPerFrame = Math.Max(p.TargetPullPerFrame, Num(arg));
            else if (t == "CALL_REPULSED" || t == "KNOCKED_BACK_RATE")
                p.TargetKnockPerFrame = Math.Max(p.TargetKnockPerFrame, Num(arg));
            else if (t == "CALL_KNOCKED_DOWN") p.Knockdown = true;
            else if (t == "CAST_SKILL" || t == "CAST_SKILL_TARGET_DST" ||
                     t == "CAST_SKILL_TARGET_SRC" || t == "CAST_SUB_SKILL") p.ChildCasts.Add(arg);
            else if (t == "EXECUTE_SCRIPT" || t == "EXECUTE_SCRIPT_WITH_PARAM") p.HasScript = true;
        }
        return p;
    }
}
