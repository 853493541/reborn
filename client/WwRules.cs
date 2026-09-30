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

    // settings/JumpFrameParam.tab school 10 jump 1 (the shipped 大轻功 flight
    // curve; per-frame VelocityXY/VelocityZ in u/frame at the 15/s logic tick).
    // Profile: entry (150,150) -> dive (Z -949) -> launch (XY 205) -> climb
    // (Z +1280) -> hover -> landing. This is the authored forward/down profile.
    public const float FlyCurveTickSeconds = 1f / 15f;
    // Forward-dominant charge used by the WW air tech. 150 u/frame is the
    // flight curve's frame-0 entry velocity (FlyCurveXY[0] = 150); the curve's
    // peak (205) felt too strong, the End triple (125) too weak. Tunable via
    // RC_WW_FWD.
    public const float ChargeForwardFrame = 150f;
    public const float ChargeDownFrame = 0f;
    // Fall/dive terminal speed: settings/Sprint.tab school 4 MaxVelocityZ
    // (900 u/frame = 13500 u/s); the engine hard clamp is -2048 u/f.
    public const float FallCapFrame = -900f;
    public static readonly float[] FlyCurveXY = { 150f, 2f, 9f, 16f, 22f, 28f, 34f, 39f, 45f, 51f, 57f, 64f, 70f, 77f, 84f, 92f, 100f, 108f, 118f, 128f, 140f, 153f, 168f, 185f, 205f, 202f, 178f, 158f, 142f, 128f, 116f, 106f, 97f, 90f, 83f, 77f, 72f, 68f, 64f, 60f, 58f, 55f, 54f, 52f, 51f, 51f, 51f, 51f, 52f, 54f, 56f, 58f, 47f, 17f, 3f, 2f, 6f, 10f, 15f, 20f, 24f, 29f, 34f, 40f, 45f, 50f, 56f, 62f, 68f, 74f, 80f, 87f, 94f, 100f, 108f, 115f, 123f, 131f, 139f, 147f, 156f };
    public static readonly float[] FlyCurveZ = { 150f, -81f, -232f, -369f, -491f, -599f, -693f, -771f, -836f, -886f, -921f, -942f, -949f, -941f, -918f, -881f, -830f, -763f, -683f, -588f, -478f, -354f, -216f, -63f, 105f, 298f, 493f, 666f, 818f, 949f, 1058f, 1145f, 1211f, 1256f, 1279f, 1280f, 1260f, 1219f, 1156f, 1072f, 966f, 838f, 689f, 519f, 327f, 114f, 0f, 0f, 0f, 0f, 0f, 693f, 693f, 315f, -14f, -41f, -65f, -88f, -108f, -127f, -143f, -156f, -168f, -178f, -185f, -191f, -194f, -195f, -194f, -191f, -185f, -178f, -168f, -156f, -143f, -127f, -108f, -88f, -65f, -41f, -14f };

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
        fail += CheckValue("FLY_CURVE_FRAMES", (float)FlyCurveXY.Length, 81f);
        float peak = 0f;
        for (int i = 0; i < FlyCurveXY.Length; i++) if (FlyCurveXY[i] > peak) peak = FlyCurveXY[i];
        fail += CheckValue("FLY_CURVE_PEAK_XY", peak, 205f);
        float dive = 0f;
        for (int i = 0; i < FlyCurveZ.Length; i++) if (FlyCurveZ[i] < dive) dive = FlyCurveZ[i];
        fail += CheckValue("FLY_CURVE_DIVE_Z", dive, -949f);
        fail += CheckValue("CHARGE_FORWARD", ChargeForwardFrame, FlyCurveXY[0]);
        fail += CheckValue("FALL_CAP", FallCapFrame, -900f);        fail += CheckRange("LEAP_J3_APEX_M", ApexMeters(3), 34.0f, 37.0f);
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
