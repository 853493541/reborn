// SkillCast.cs — v6 full cast-chain state (client truth).
//
// Models what the shipped client does for a targeted melee skill like
// JueJing LongYa (docs/netcode/JX3_SKILL_CAST_FLOW.md):
//   * the caster TURNS to face the selected target,
//   * plays the skill's authored animation (skill_caster_f1.txt
//     CastSkillAnimationID0 -> player_animation_f1.txt -> .tani),
//   * DASHES toward the target WHILE that animation plays (the skill's child
//     DASH attribute; the value is a speed in engine units per frame — e.g.
//     65030 DASH 120 -> 120 u/frame = 1920 u/s at GAME_FPS 16), and
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
    long effectAtMs;
    bool effectPending;

    float fromX, fromZ;
    float endX, endZ;          // dash end (target pos pulled back by StopDistance)
    float fxX, fxY, fxZ;       // effect anchor (the target)
    float faceYaw;

    // dashSpeedPerFrame = engine units per frame (the child skill's DASH value).
    public void Begin(long now, string name, string anim, string effect,
                      float selfX, float selfZ,
                      float tgtX, float tgtY, float tgtZ,
                      long animMs, long effectAtMs, float stopDistance,
                      float dashSpeedPerFrame)
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
