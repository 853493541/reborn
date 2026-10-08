// SkillCast.cs — v6 full cast-chain state (client truth).
//
// Models what the shipped client does for a targeted melee skill like
// JueJing LongYa (docs/netcode/JX3_SKILL_CAST_FLOW.md):
//   * the caster TURNS to face the selected target,
//   * plays the skill's authored animation (skill_caster_f1.txt
//     CastSkillAnimationID0 -> player_animation_f1.txt -> .tani),
//   * DASHES toward the target WHILE that animation plays (the skill's child
//     DASH attribute; the value is a speed in engine units per frame — the
//     scripts name it `nDashSpeed`, unit `点/帧` = points/frame; e.g. 65030
//     DASH 120 -> 120 u/frame = 1920 u/s at GAME_FPS 16), and
//   * plays the skill's effect (skill_effect, PhysicsDamageEffectResultID) once.
//
// The dash is a fast, authored-speed move to the target (not a slow lerp over
// the whole cast): it covers the gap at dashSpeedPerFrame and then holds while
// the animation finishes.
//
// Pure state/maths (no engine types) so RebornClient owns the engine calls.
using System;

internal sealed class SkillCast
{
    public const int GameFps = 16;   // GAME_FPS (docs/netcode/README.md)

    public bool Active;
    public string Name = "";
    public string AnimPath = "";
    public string EffectPath = "";

    long startMs;
    long animMs;
    long dashMs;
    long commitMs;                 // effect/commit time = max(prepare, effect frame)
    long totalMs;                  // cast ends here = max(anim, commit)
    bool commitPending;

    float fromX, fromZ;
    float endX, endZ;          // dash end (target pos pulled back by StopDistance)
    float fxX, fxY, fxZ;       // effect anchor (the target)
    float faceYaw;

    // dashSpeedPerFrame = engine units per frame (the child skill's DASH value).
    // prepareMs = the skill's cast time (nPrepareFrames / GAME_FPS); 0 = instant.
    public void Begin(long now, string name, string anim, string effect,
                      float selfX, float selfZ,
                      float tgtX, float tgtY, float tgtZ,
                      long animMs, long effectAtMs, long prepareMs, long channelMs,
                      float stopDistance, float dashSpeedPerFrame)
    {
        Active = true;
        Name = name;
        AnimPath = anim;
        EffectPath = effect;
        startMs = now;
        this.animMs = animMs > 1 ? animMs : 1;
        long eff = effectAtMs < 0 ? 0 : effectAtMs;
        // commit = the cast completes: instant skills commit at the effect frame,
        // prepared skills at the end of the prepare (nPrepareFrames) phase.
        commitMs = prepareMs > eff ? prepareMs : eff;
        totalMs = (this.animMs > commitMs ? this.animMs : commitMs) + (channelMs > 0 ? channelMs : 0);
        commitPending = true;   // commit fires once at commitMs (effect optional)

        fromX = selfX;
        fromZ = selfZ;

        float dx = tgtX - selfX;
        float dz = tgtZ - selfZ;
        float d = (float)Math.Sqrt(dx * dx + dz * dz);
        // dashSpeedPerFrame <= 0 => the skill has no dash: stay in place.
        if (dashSpeedPerFrame <= 0f || d <= stopDistance + 1f)
        {
            endX = selfX;
            endZ = selfZ;
        }
        else
        {
            endX = tgtX - dx / d * stopDistance;
            endZ = tgtZ - dz / d * stopDistance;
        }

        fxX = tgtX;
        fxY = tgtY;
        fxZ = tgtZ;

        // dash timing from the authored speed (u/frame -> u/s) over the gap
        float speedUpS = dashSpeedPerFrame > 1f ? dashSpeedPerFrame * GameFps : 1f;
        float travel = (float)Math.Sqrt((endX - fromX) * (endX - fromX) +
                                        (endZ - fromZ) * (endZ - fromZ));
        long dash = travel > 0.5f ? (long)(travel / speedUpS * 1000f) : 0;
        if (dash < 80) dash = 80;             // never instant
        if (dash > this.animMs) dash = this.animMs;
        dashMs = dash;

        // the client's forward is (-cos(yaw), -sin(yaw)); face the target.
        if (d > 0.01f)
            faceYaw = (float)Math.Atan2(-(tgtZ - selfZ), -(tgtX - selfX));
        else
            faceYaw = 0f;
    }

    public float FaceYaw() { return faceYaw; }
    public long DashMs() { return dashMs; }

    // Advance; returns true while the cast is running and sets the render pos.
    public bool Tick(long now, out float x, out float z)
    {
        x = fromX;
        z = fromZ;
        if (!Active) return false;
        long el = now - startMs;
        float f;
        if (dashMs > 0)
        {
            f = (float)el / dashMs;
            if (f > 1f) f = 1f;
            if (f < 0f) f = 0f;
        }
        else f = 1f;
        x = fromX + (endX - fromX) * f;
        z = fromZ + (endZ - fromZ) * f;
        if (el >= totalMs) Active = false;
        return true;
    }

    // true exactly once, at the commit frame (damage/buff; effect is separate).
    public bool TakeEffect(long now)
    {
        if (!commitPending) return false;
        if (now - startMs >= commitMs) { commitPending = false; return true; }
        return false;
    }

    public long TotalMs() { return totalMs; }
    public long CommitMs() { return commitMs; }

    public void EffectPoint(out float x, out float y, out float z)
    {
        x = fxX; y = fxY; z = fxZ;
    }
}

// SkillDamage — resolve an ability's authored damage program (P3 v1).
//
// Uses the mechanic program (mechanics_f1.tsv col 11) written by
// ability_picker/tools/build_mechanics.py: the SKILL_<type>_DAMAGE /
// SKILL_<type>_DAMAGE_RAND ops carry the level-table value times an authored
// multiplier (e.g. "tSkillData[dwSkillLevel].nDamage * 1.1"). v1 returns the flat
// base + half the random spread (a deterministic mid-roll); weapon%/attack-power
// scaling and mitigation are not applied yet (open).
internal static class SkillDamage
{
    static float Num(string[] row, int i)
    {
        float v;
        return (i >= 0 && i < row.Length && float.TryParse(row[i], out v)) ? v : 0f;
    }

    static float Mult(string expr)
    {
        int star = expr.LastIndexOf('*');
        if (star >= 0)
        {
            float m;
            if (float.TryParse(expr.Substring(star + 1).Trim(), out m) && m > 0f) return m;
        }
        return 1f;
    }

    public static float Base(string[] row)
    {
        float dmg = Num(row, 3), rand = Num(row, 4);
        string ops = row.Length > 11 ? row[11] : "";
        float baseMult = 0f, randMult = 0f;
        string[] parts = ops.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            string[] f = parts[i].Split('|');
            if (f.Length < 2) continue;
            string t = f[1];
            string arg = f.Length > 2 ? f[2] : "";
            if (t.StartsWith("SKILL_") && t.EndsWith("_DAMAGE_RAND")) randMult += Mult(arg);
            else if (t.StartsWith("SKILL_") && t.EndsWith("_DAMAGE")) baseMult += Mult(arg);
        }
        if (baseMult <= 0f && randMult <= 0f) return 0f;
        return dmg * baseMult + rand * randMult * 0.5f;
    }
}
