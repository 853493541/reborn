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

    // settings/JumpParam.tab school 4 (万花/点墨江山, the sandbox actor's school;
    // fresh extraction 2026-09-29), Vz u/frame, G u/frame^2, MaxJumpCount 5:
    //   J1 50/160/8  J2 70/240/7  J3 100/700/36  J4/J5 100/-250/8
    //   End triple (all stages): xy 125, vz -140, g 12  -> forward-down glide
    public static readonly float[] ChainVzFrame = { 90f, 160f, 240f, 700f, -250f, -250f };
    public static readonly float[] ChainGravityFrame = { 11f, 8f, 7f, 36f, 8f, 8f };
    public static readonly float[] ChainSpeedXYFrame = { 40f, 50f, 70f, 100f, 100f, 100f };
    public const float EndSpeedXYFrame = 125f;
    public const float EndVzFrame = -140f;
    public const float EndGravityFrame = 12f;

    public enum WwAction
    {
        None,
        Sprint,
        Charge
    }

    public static WwAction Evaluate(bool grounded, bool doubleTapW, bool schoolWeaponEquipped)
    {
        if (!doubleTapW) return WwAction.None;
        if (grounded) return WwAction.Sprint;
        if (!schoolWeaponEquipped) return WwAction.None;
        return WwAction.Charge;
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
        fail += Check("GROUND_WW_SPRINT", Evaluate(true, true, true), WwAction.Sprint);
        fail += Check("GROUND_WW_NO_WEAPON_SPRINT", Evaluate(true, true, false), WwAction.Sprint);
        fail += Check("AIR_WW_CHARGE", Evaluate(false, true, true), WwAction.Charge);
        fail += Check("AIR_WW_NO_WEAPON_NONE", Evaluate(false, true, false), WwAction.None);
        fail += Check("NO_DOUBLE_TAP_NONE", Evaluate(true, false, true), WwAction.None);
        fail += CheckValue("CHAIN_J1_VZ", ChainVzFrame[1], 160f);
        fail += CheckValue("CHAIN_J1_G", ChainGravityFrame[1], 8f);
        fail += CheckValue("CHAIN_J1_XY", ChainSpeedXYFrame[1], 50f);
        fail += CheckValue("CHAIN_J2_VZ", ChainVzFrame[2], 240f);
        fail += CheckValue("CHAIN_J2_XY", ChainSpeedXYFrame[2], 70f);
        fail += CheckValue("CHAIN_J3_VZ", ChainVzFrame[3], 700f);
        fail += CheckValue("CHAIN_J4_VZ", ChainVzFrame[4], -250f);
        fail += CheckValue("CHAIN_END_XY", EndSpeedXYFrame, 125f);
        fail += CheckValue("CHAIN_END_VZ", EndVzFrame, -140f);
        fail += CheckValue("CHAIN_END_G", EndGravityFrame, 12f);
        fail += CheckRange("LEAP_J3_APEX_M", ApexMeters(3), 34.0f, 37.0f);
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
