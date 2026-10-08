// SkillCast.cs — v6 full cast-chain state (client truth).
//
// Models what the shipped client does for a targeted melee skill like
// JueJing LongYa (docs/netcode/JX3_SKILL_CAST_FLOW.md):
//   * the caster TURNS to face the selected target,
//   * plays the skill's authored animation (skill_caster_f1.txt
//     CastSkillAnimationID0 -> player_animation_f1.txt -> .tani),
//   * DASHES toward the target WHILE that animation plays (the skill's child
//     DASH attribute; e.g. LongYa dash child 65030 = DASH 120), and
//   * plays the skill's effect (skill_effect, PhysicsDamageEffectResultID) once.
//
// This class is pure state/maths (no engine types) so RebornClient owns the
// engine calls (setClip / AddDummyModel). The goal is a faithful chain, not a
// per-frame stand-in: the renderer is driven from the client's own cast data.
using System;

internal sealed class SkillCast
{
    public bool Active;
    public string Name = "";
    public string AnimPath = "";
    public string EffectPath = "";

    long startMs;
    long animMs;
    long effectAtMs;
    bool effectPending;

    float fromX, fromZ;
    float endX, endZ;          // dash end (target pos pulled back by StopDistance)
    float fxX, fxY, fxZ;       // effect anchor (the target)
    float faceYaw;

    // Begin a cast. selfX/selfZ = caster ground pos; tgtX/tgtZ/tgtY = target;
    // stopDistance = how close the dash stops to the target (u, engine cm).
    public void Begin(long now, string name, string anim, string effect,
                      float selfX, float selfZ,
                      float tgtX, float tgtY, float tgtZ,
                      long animMs, long effectAtMs, float stopDistance)
    {
        Active = true;
        Name = name;
        AnimPath = anim;
        EffectPath = effect;
        startMs = now;
        this.animMs = animMs > 1 ? animMs : 1;
        this.effectAtMs = effectAtMs < 0 ? 0 : effectAtMs;
        effectPending = effect != null && effect.Length > 0;

        fromX = selfX;
        fromZ = selfZ;

        float dx = tgtX - selfX;
        float dz = tgtZ - selfZ;
        float d = (float)Math.Sqrt(dx * dx + dz * dz);
        if (d <= stopDistance + 1f)
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

        // the client's forward is (-cos(yaw), -sin(yaw)); face the target.
        if (d > 0.01f)
            faceYaw = (float)Math.Atan2(-(tgtZ - selfZ), -(tgtX - selfX));
        else
            faceYaw = 0f;
    }

    public float FaceYaw() { return faceYaw; }

    // Advance; returns true while the cast is running and sets the render pos.
    public bool Tick(long now, out float x, out float z)
    {
        x = fromX;
        z = fromZ;
        if (!Active) return false;
        long el = now - startMs;
        float f = (float)el / animMs;
        if (f > 1f) f = 1f;
        if (f < 0f) f = 0f;
        x = fromX + (endX - fromX) * f;
        z = fromZ + (endZ - fromZ) * f;
        if (el >= animMs) Active = false;
        return true;
    }

    // true exactly once, at the effect frame.
    public bool TakeEffect(long now)
    {
        if (!effectPending) return false;
        if (now - startMs >= effectAtMs) { effectPending = false; return true; }
        return false;
    }

    public void EffectPoint(out float x, out float y, out float z)
    {
        x = fxX; y = fxY; z = fxZ;
    }
}
