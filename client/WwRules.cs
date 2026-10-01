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
    // rows 0..10 (school 4): J0..J5 + the 弈韵 rows J6..J10 (all 100/-250/8,
    // Condition.tab JC6..JC11 = 点墨江山·弈韵一段..六段; SpecialSprint school 4 =
    // 6|7|8|9|10|11)
    public static readonly float[] ChainVzFrame = { 90f, 160f, 240f, 700f, -250f, -250f, -250f, -250f, -250f, -250f, -250f };
    public static readonly float[] ChainGravityFrame = { 11f, 8f, 7f, 36f, 8f, 8f, 8f, 8f, 8f, 8f, 8f };
    public static readonly float[] ChainSpeedXYFrame = { 40f, 50f, 70f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f };
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
    // Base fall gravity after a fly exit (StopBirdFly -> UnlockBirdMoveZ):
    // JumpParam row 0 gravity 11 u/frame^2 -> 2475 u/s^2 (the non-chain fall).
    public const float FallGravityFrame = 11f;
    public const float FallGravityPerSecond2 = FallGravityFrame * 225f;

    // Ground hold-W 疾跑段 (buff 12085 通用疾速跑 + 12190 通用疾跑按住):
    // Sprint.tab school 4 MaxVelocityXY = 120 u/frame -> 1800 u/s; staged ramp
    // per 疾速第零~三段 (atMoveSpeedPercent 256/768/1024 = 25/75/100%), 16-frame
    // buff intervals. RC_JUMP_SCALE does not apply here.
    public const float JipaoCapFrame = 120f;
    public const float JipaoCapPerSecond = JipaoCapFrame * LogicTicksPerSecond;
    public const int JipaoStageFrames = 16;

    // 万花大轻功「点墨江山」 (school 4; skills 20628 trigger / 20630 急坠;
    // extracted 2026-09-30, proof/controls/sprint/out/scripts/skill/轻功/轻功通用/):
    //  - CanCast: nSprintPower >= 10000 (bar max provisional); cost 100*CONSUME_BASE
    //    (CONSUME_BASE open; 通用轻功气力值扣除 level 2 uses a literal 25 ->
    //    the 25 hypothesis, MED)
    //  - Apply: SetTimer(30) -> OnTimer: BirdFlyTo + LockBirdMoveZ (Z-locked fly),
    //    buffs 13422 (lv3) / 14626 / 13836 (binds 13889 + 16516)
    //  - 急坠 (20630): SetPassiveVelocityZ(-2000) u/frame
    //  - JumpParam school 4 fly costs (气力值, per second unless noted): OnFlyCost 75,
    //    OnFlyFloatCost 35, OnFlyJumpCost 300 (per stage press), OnFlyStandCost 21,
    //    OnFlyJumpInSprintCost 200, OnSprintDashCost 3000, OnFlyBirdMoveCost 206,
    //    OnFlyBirdMoveDashCost 375
    //  - stages JC1..JC5 = 纵跃段/一段/二段/三段/四段 use JumpParam rows J1..J5
    //    (Condition.tab school 4, JumpCount 1..5; MaxJumpCount 5)
    public const float WhPowerMax = 10000f;
    // UI scale: the live bar shows 气力值 ~700-1000 (user observation), so the
    // scripts' nSprintPower is 10x the displayed value (10000 = the full bar
    // shown as 1000). A full bar lasts ~1 min of bird-move flight:
    // 10000 / OnFlyBirdMoveCost 206/s ~= 48 s.
    public const float WhPowerUiScale = 10f;
    public const float WhTriggerCost = 2500f;
    public const float WhFlyCostPerSecond = 75f;
    public const float WhFloatCostPerSecond = 35f;
    public const float WhJumpCost = 300f;
    public const float WhBirdMoveCostPerSecond = 206f;
    public const float WhPlungeFrame = -2000f;
    public const int WhTimerFrames = 30;
    // 11 phases: 1..5 = 纵跃段/一段/二段/三段/四段 (Condition JC1..JC5),
    // 6..11 = 弈韵一段..六段 (JC6..JC11; the 棋弈 branch: Shift from 一段/二段/
    // 三段 enters 6; Space cycles 6..10 then 10 -> 6; Shift -> 11 = the
    // 俯冲 fall-out)
    public static readonly string[] WhStageNames = { "纵跃段", "一段", "二段", "三段", "四段" };
    public static readonly string[] WhYiyunNames = { "弈韵一段", "弈韵二段", "弈韵三段", "弈韵四段", "弈韵五段", "弈韵六段" };
    public const int WhBaseStages = 5;      // 1..5
    public const int WhYiyunFirst = 6;      // 6..11
    public const int WhYiyunCycleLast = 10; // Space at 10 -> 6 (the cycle)
    public const int WhYiyunDive = 11;      // Shift at 6..10 -> 11 (fall out)
    // Trigger cast SkillMove 336 (settings/SkillMove.tab; the school launch):
    // IgnoreGravity=1, TotalFrame=31, XY=0, per-frame VelocityZ 0/471/537/574/
    // 590/589/578/560/536/509/481/452/422/393/364/335/308/281/255/230/206/183/
    // 161/139/118/98/79/60/42/25/8 u/frame. The WW上冲 vertical takeoff; the
    // fly (BirdFlyTo + LockBirdMoveZ) starts after SetTimer(30) ~= the launch.
    public const int WhLaunchFrames = 31;
    public static readonly float[] WhLaunchVzFrame = { 0f, 471f, 537f, 574f, 590f, 589f, 578f, 560f,
        536f, 509f, 481f, 452f, 422f, 393f, 364f, 335f, 308f, 281f, 255f, 230f, 206f, 183f, 161f,
        139f, 118f, 98f, 79f, 60f, 42f, 25f, 8f };
    // 大轻功 auto-end altitude (气力值持续消耗.lua): Flyheight < 6*8*64 u
    // (3072 u) -> StopBirdFly + UnlockBirdMoveZ (checked while flying, not
    // during the launch).
    public const float FlyMinAltitudeUnits = 6f * 8f * 64f;
    // 空中冲刺 / 空中持续冲刺 (skill 20788 通用空中冲刺 -> 20789, learned by every
    // school trigger; the dash is usable only with the flight buff 13422):
    // Apply = DashToPitchDirection(480, nFaceDirection, 160) + buff 14561
    // (江湖轻功通用免控) + buff 14129 (空中持续冲刺换二段, ScriptFile =
    // 轻功/通用持续冲刺结束.lua -> Stop() + skill-move camera + HideFullScreenSFX;
    // atDriftFlag; icon 20788 -> 20789) + the Fly_Skill SFX.
    // tSkillData nDashFrame per level: 100 / 12 / 14 / 16.
    public const float WhDashFrame = 480f;        // DashToPitchDirection velocity, u/frame
    public const int WhDashFrames = 100;          // nDashFrame level 1 (100/12/14/16)
    public const float WhDashCostPerSecond = 375f; // OnFlyBirdMoveDashCost
    public static readonly float[] FlyCurveXY = { 150f, 2f, 9f, 16f, 22f, 28f, 34f, 39f, 45f, 51f, 57f, 64f, 70f, 77f, 84f, 92f, 100f, 108f, 118f, 128f, 140f, 153f, 168f, 185f, 205f, 202f, 178f, 158f, 142f, 128f, 116f, 106f, 97f, 90f, 83f, 77f, 72f, 68f, 64f, 60f, 58f, 55f, 54f, 52f, 51f, 51f, 51f, 51f, 52f, 54f, 56f, 58f, 47f, 17f, 3f, 2f, 6f, 10f, 15f, 20f, 24f, 29f, 34f, 40f, 45f, 50f, 56f, 62f, 68f, 74f, 80f, 87f, 94f, 100f, 108f, 115f, 123f, 131f, 139f, 147f, 156f };
    public static readonly float[] FlyCurveZ = { 150f, -81f, -232f, -369f, -491f, -599f, -693f, -771f, -836f, -886f, -921f, -942f, -949f, -941f, -918f, -881f, -830f, -763f, -683f, -588f, -478f, -354f, -216f, -63f, 105f, 298f, 493f, 666f, 818f, 949f, 1058f, 1145f, 1211f, 1256f, 1279f, 1280f, 1260f, 1219f, 1156f, 1072f, 966f, 838f, 689f, 519f, 327f, 114f, 0f, 0f, 0f, 0f, 0f, 693f, 693f, 315f, -14f, -41f, -65f, -88f, -108f, -127f, -143f, -156f, -168f, -178f, -185f, -191f, -194f, -195f, -194f, -191f, -185f, -178f, -168f, -156f, -143f, -127f, -108f, -88f, -65f, -41f, -14f };

    public enum WwAction
    {
        None,
        Sprint,
        Charge
    }

    // WW (double-tap W): GROUND = 点墨江山·疾跑段 (the fast run — live-game
    // behaviour: "ww = run fast 点墨江山 疾跑段"; the chain is entered from the
    // 疾跑段 with Space = 纵跃段); AIR (not flying) = the school trigger.
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
        fail += CheckValue("FALL_CAP", FallCapFrame, -900f);
        fail += CheckValue("FALL_GRAVITY", FallGravityPerSecond2, 2475f);
        fail += CheckValue("JIPAO_CAP", JipaoCapPerSecond, 1800f);        fail += CheckRange("LEAP_J3_APEX_M", ApexMeters(3), 34.0f, 37.0f);
        fail += CheckValue("WH_TRIGGER_COST", WhTriggerCost, 2500f);
        fail += CheckValue("WH_PLUNGE_FRAME", WhPlungeFrame, -2000f);
        fail += CheckValue("WH_TIMER_FRAMES", (float)WhTimerFrames, 30f);
        fail += CheckValue("WH_STAGES", (float)WhStageNames.Length, 5f);
        fail += CheckValue("WH_LAUNCH_FRAMES", (float)WhLaunchVzFrame.Length, 31f);
        fail += CheckValue("WH_LAUNCH_PEAK", WhLaunchVzFrame[4], 590f);
        fail += CheckValue("WH_MIN_ALTITUDE", FlyMinAltitudeUnits, 3072f);
        fail += CheckValue("WH_DASH_FRAME", WhDashFrame, 480f);
        fail += CheckValue("WH_DASH_FRAMES", (float)WhDashFrames, 100f);
        fail += CheckValue("WH_POWER_UI_SCALE", WhPowerUiScale, 10f);
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
