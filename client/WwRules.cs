// WW (double-tap W) qinggong rules -- client-data derived.
//
// Sources (extracted 2026-09-29; see agent/daqinggong commits e0c7be3/32450a9):
//   skill 37891 "双w进入轻功技能": SelfMoveStateMask=0xFFFFFFFF, sprint state=Any
//   settings/JumpParam.tab school 0 (proof/gravity/JumpParam.tab) -- the 轻功
//     chain takeoff triples, Vz u/frame, G u/frame^2, MaxJumpCount 4:
//       J0  90/11  ground jump (Space)
//       J1 300/20  纵跃段 1  (apex ~11.7 m)
//       J2 400/20  纵跃段 2  (apex ~20.8 m)
//       J3 -250/8  downward dash stage
//   ui effect Z_纵跃UI.pss (editor resource index) -- 纵跃 is a named chain state
//   logic tick 15/s (docs/movement/JX3_GRAVITY_RESEARCH.md); engine Z clamp
//   -2048..2047 u/f; ground sprint 8.8 尺/s x 64 units/尺 (client contract).
using System;

internal static class WwRules
{
    public const float LogicTicksPerSecond = 15f;
    public const float VzClampMinPerSecond = -2048f * LogicTicksPerSecond;
    public const float VzClampMaxPerSecond = 2047f * LogicTicksPerSecond;
    public const float SprintSpeedPerSecond = 8.8f * 64f;

    public static readonly float[] ChainVzFrame = { 90f, 300f, 400f, -250f };
    public static readonly float[] ChainGravityFrame = { 11f, 20f, 20f, 8f };

    public enum WwAction
    {
        None,
        Sprint,
        Leap
    }

    public static WwAction Evaluate(bool grounded, bool doubleTapW, bool schoolWeaponEquipped, int stage)
    {
        if (!doubleTapW) return WwAction.None;
        if (grounded) return WwAction.Sprint;
        if (!schoolWeaponEquipped) return WwAction.None;
        if (stage < 1 || stage >= ChainVzFrame.Length) return WwAction.None;
        return WwAction.Leap;
    }

    public static float ApexMeters(int stage)
    {
        if (stage < 0 || stage >= ChainVzFrame.Length) return 0f;
        float v = ChainVzFrame[stage] * LogicTicksPerSecond;
        float g = ChainGravityFrame[stage] * LogicTicksPerSecond * LogicTicksPerSecond;
        if (g <= 0f || v <= 0f) return 0f;
        return v * v / (2f * g) / 192f;
    }

    public static int SelfTest()
    {
        int fail = 0;
        fail += Check("GROUND_WW_SPRINT", Evaluate(true, true, true, 0), WwAction.Sprint);
        fail += Check("GROUND_WW_NO_WEAPON_SPRINT", Evaluate(true, true, false, 0), WwAction.Sprint);
        fail += Check("AIR_WW_LEAP", Evaluate(false, true, true, 1), WwAction.Leap);
        fail += Check("AIR_WW_NO_WEAPON_NONE", Evaluate(false, true, false, 1), WwAction.None);
        fail += Check("AIR_WW_STAGE0_NONE", Evaluate(false, true, true, 0), WwAction.None);
        fail += Check("AIR_WW_EXHAUSTED_NONE", Evaluate(false, true, true, 4), WwAction.None);
        fail += Check("NO_DOUBLE_TAP_NONE", Evaluate(true, false, true, 1), WwAction.None);
        fail += CheckValue("CHAIN_J1_VZ", ChainVzFrame[1], 300f);
        fail += CheckValue("CHAIN_J1_G", ChainGravityFrame[1], 20f);
        fail += CheckValue("CHAIN_J2_VZ", ChainVzFrame[2], 400f);
        fail += CheckValue("CHAIN_J3_VZ", ChainVzFrame[3], -250f);
        fail += CheckRange("LEAP_J1_APEX_M", ApexMeters(1), 11.0f, 12.5f);
        fail += CheckRange("LEAP_J2_APEX_M", ApexMeters(2), 19.5f, 22.0f);
        Console.WriteLine("RESULT " + (fail == 0 ? "PASS" : "FAIL") + " failures=" + fail);
        return fail;
    }

    private static int Check(string name, WwAction got, WwAction want)
    {
        if (got == want)
        {
            Console.WriteLine(name + ": PASS (" + got + ")");
            return 0;
        }
        Console.WriteLine(name + ": FAIL got=" + got + " want=" + want);
        return 1;
    }

    private static int CheckValue(string name, float got, float want)
    {
        if (got == want)
        {
            Console.WriteLine(name + ": PASS (" + got + ")");
            return 0;
        }
        Console.WriteLine(name + ": FAIL got=" + got + " want=" + want);
        return 1;
    }

    private static int CheckRange(string name, float v, float lo, float hi)
    {
        if (v >= lo && v <= hi)
        {
            Console.WriteLine(name + ": PASS " + v + " within [" + lo + ", " + hi + "]");
            return 0;
        }
        Console.WriteLine(name + ": FAIL " + v + " outside [" + lo + ", " + hi + "]");
        return 1;
    }
}
