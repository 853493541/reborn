// WW (double-tap W) qinggong rules -- client-data derived.
//
// Sources (extracted 2026-09-29 with bin64\PakV4SfxExtract.exe; see the
// agent/daqinggong research doc docs/movement/JX3_DAQINGGONG_RESEARCH.md):
//   skill 37891 "双w进入轻功技能": SelfMoveStateMask=0xFFFFFFFF, sprint state=Any
//   scripts/skill/轻功/轻功通用/天策轻功急坠.lua:
//       Apply -> player.SetPassiveVelocityZ(-2000)   (engine units per logic frame)
//   engine Z clamp -2048..2047 units/frame (KCharacter vertical move)
//   logic tick 15/s (docs/movement/JX3_GRAVITY_RESEARCH.md)
//   ground sprint 8.8 尺/s x 64 units/尺 (existing client contract)
using System;

internal static class WwRules
{
    public const float LogicTicksPerSecond = 15f;
    public const float PlungeVzFrame = -2000f;
    public const float PlungeVzPerSecond = PlungeVzFrame * LogicTicksPerSecond;
    public const float VzClampMinPerSecond = -2048f * LogicTicksPerSecond;
    public const float VzClampMaxPerSecond = 2047f * LogicTicksPerSecond;
    public const float SprintSpeedPerSecond = 8.8f * 64f;

    public enum WwAction
    {
        None,
        Sprint,
        Plunge
    }

    public static WwAction Evaluate(bool grounded, bool doubleTapW, bool schoolWeaponEquipped)
    {
        if (!doubleTapW) return WwAction.None;
        if (grounded) return WwAction.Sprint;
        if (!schoolWeaponEquipped) return WwAction.None;
        return WwAction.Plunge;
    }

    public static int SelfTest()
    {
        int fail = 0;
        fail += Check("GROUND_WW_SPRINT", Evaluate(true, true, true), WwAction.Sprint);
        fail += Check("GROUND_WW_NO_WEAPON_SPRINT", Evaluate(true, true, false), WwAction.Sprint);
        fail += Check("AIR_WW_PLUNGE", Evaluate(false, true, true), WwAction.Plunge);
        fail += Check("AIR_WW_NO_WEAPON_NONE", Evaluate(false, true, false), WwAction.None);
        fail += Check("NO_DOUBLE_TAP_NONE", Evaluate(true, false, true), WwAction.None);
        fail += CheckRange("PLUNGE_VZ_RANGE", PlungeVzPerSecond, VzClampMinPerSecond, VzClampMaxPerSecond);
        Console.WriteLine("RESULT " + (fail == 0 ? "PASS" : "FAIL") + " failures=" + fail);
        return fail;
    }

    private static int CheckRange(string name, float vz, float lo, float hi)
    {
        if (vz >= lo && vz <= hi)
        {
            Console.WriteLine(name + ": PASS vz=" + PlungeVzFrame + " u/f = " + vz +
                " u/s within Z clamp [" + lo + ", " + hi + "]");
            return 0;
        }
        Console.WriteLine(name + ": FAIL vz=" + vz + " outside [" + lo + ", " + hi + "]");
        return 1;
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
}
