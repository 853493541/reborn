// Generated from the game client (settings/SkillMove.tab, proof/gravity/SkillMove.tab).
// 万花 大轻功 (skills \u8f7b\u529f/\u516b\u5927\u6d3e/\u4e07\u82b1): each stage casts a SkillMove + SET_JUMP_COUNT.
// Per-frame VelocityXY / VelocityZ in units per 15 Hz logic frame (IgnoreGravity=1,
// SkillMoveEndButKeepVelocity=1). Do not hand-edit; regenerate from the table.
internal static class QinggongData
{
    public static readonly string[] MoveNames = { "纵跃段", "一段", "二段", "三段", "四段", "五段", "切入" };
    public static readonly int[] MoveSkillIds = { 15835, 15577, 15579, 15581, 15688, 15690, 15554 };
    public static readonly int[] MoveJumpCount = { 1, 7, 8, 9, 10, 11, 6 };
    public static readonly int[] MoveSkillMoveIds = { 175, 127, 128, 129, 163, 162, 126 };
    // the 棋弈 (Shift at 一段): 万花切入 consumes 50*100 sprint power (no gate)
    public const float ChessEntryCost = 5000f;
    public const int MoveCount = 7;

    public static readonly float[][] MoveXY = new float[][] {
        // 纵跃段 (skill 15835, SkillMove 175, 8 frames)
        new float[] { 0f, 77f, 72f, 66f, 59f, 51f, 41f, 28f },
        // 一段 (skill 15577, SkillMove 127, 37 frames)
        new float[] { 0f, 55f, 58f, 61f, 63f, 65f, 67f, 68f, 70f, 71f, 72f, 73f, 73f, 74f, 74f, 75f, 75f, 75f, 74f, 74f, 73f, 73f, 72f, 71f, 70f, 68f, 67f, 65f, 63f, 73f, 90f, 102f, 110f, 118f, 124f, 130f, 136f },
        // 二段 (skill 15579, SkillMove 128, 44 frames)
        new float[] { 0f, 202f, 201f, 194f, 181f, 171f, 170f, 168f, 166f, 165f, 163f, 161f, 159f, 157f, 156f, 155f, 156f, 156f, 155f, 154f, 152f, 150f, 146f, 140f, 132f, 115f, 96f, 86f, 79f, 73f, 68f, 65f, 63f, 62f, 63f, 65f, 69f, 76f, 87f, 100f, 80f, 77f, 71f, 61f },
        // 三段 (skill 15581, SkillMove 129, 37 frames)
        new float[] { 0f, 221f, 238f, 204f, 207f, 209f, 208f, 206f, 203f, 197f, 190f, 179f, 163f, 145f, 128f, 114f, 101f, 91f, 86f, 82f, 80f, 78f, 76f, 75f, 75f, 74f, 74f, 75f, 75f, 76f, 69f, 70f, 71f, 70f, 68f, 67f, 65f },
        // 四段 (skill 15688, SkillMove 163, 81 frames)
        new float[] { 0f, 78f, 98f, 104f, 102f, 92f, 83f, 82f, 81f, 80f, 79f, 78f, 77f, 76f, 75f, 74f, 73f, 71f, 69f, 67f, 65f, 65f, 64f, 64f, 65f, 66f, 68f, 70f, 72f, 76f, 76f, 76f, 77f, 80f, 85f, 91f, 100f, 111f, 123f, 135f, 145f, 154f, 160f, 165f, 168f, 170f, 169f, 167f, 164f, 158f, 151f, 117f, 92f, 89f, 87f, 88f, 90f, 94f, 110f, 121f, 122f, 124f, 125f, 125f, 125f, 125f, 124f, 124f, 123f, 118f, 111f, 91f, 82f, 81f, 79f, 80f, 82f, 81f, 77f, 72f, 67f },
        // 五段 (skill 15690, SkillMove 162, 7 frames)
        new float[] { 0f, 199f, 315f, 289f, 297f, 300f, 298f },
        // 切入 (skill 15554, SkillMove 126, 38 frames)
        new float[] { 0f, 54f, 56f, 56f, 56f, 57f, 57f, 57f, 56f, 55f, 55f, 57f, 59f, 60f, 62f, 62f, 63f, 63f, 64f, 64f, 62f, 60f, 57f, 55f, 55f, 59f, 64f, 69f, 75f, 70f, 61f, 56f, 54f, 49f, 45f, 47f, 63f, 60f },
    };

    public static readonly float[][] MoveZ = new float[][] {
        // 纵跃段 (skill 15835, SkillMove 175, 8 frames)
        new float[] { 0f, 287f, 267f, 227f, 176f, 119f, 59f, -1f },
        // 一段 (skill 15577, SkillMove 127, 37 frames)
        new float[] { 0f, 263f, 239f, 218f, 201f, 186f, 173f, 162f, 151f, 142f, 133f, 125f, 118f, 111f, 104f, 98f, 92f, 86f, 81f, 76f, 70f, 65f, 59f, 54f, 48f, 42f, 35f, 27f, 17f, 11f, 11f, 11f, 10f, 8f, 6f, 4f, 2f },
        // 二段 (skill 15579, SkillMove 128, 44 frames)
        new float[] { 0f, 769f, 751f, 727f, 669f, 569f, 523f, 484f, 450f, 419f, 389f, 359f, 328f, 294f, 257f, 220f, 194f, 174f, 157f, 140f, 123f, 105f, 85f, 62f, 35f, 0f, -41f, -77f, -109f, -137f, -163f, -188f, -212f, -236f, -262f, -292f, -333f, -426f, 371f, 748f, 639f, 573f, 388f, 174f },
        // 三段 (skill 15581, SkillMove 129, 37 frames)
        new float[] { 0f, 700f, 766f, 648f, 608f, 580f, 556f, 534f, 512f, 489f, 461f, 423f, 357f, 249f, 148f, 65f, 22f, -4f, -39f, -69f, -95f, -118f, -137f, -153f, -165f, -174f, -178f, -173f, -151f, -69f, 274f, 494f, 523f, 468f, 366f, 236f, 88f },
        // 四段 (skill 15688, SkillMove 163, 81 frames)
        new float[] { 0f, 1908f, 1602f, 1430f, 1247f, 990f, 765f, 686f, 614f, 549f, 491f, 440f, 396f, 359f, 329f, 306f, 290f, 269f, 240f, 211f, 185f, 160f, 136f, 113f, 92f, 72f, 54f, 37f, 21f, 7f, -1f, -5f, -13f, -26f, -43f, -64f, -89f, -118f, -152f, -184f, -209f, -231f, -251f, -270f, -286f, -302f, -317f, -331f, -344f, -357f, -370f, 91f, 95f, 108f, 108f, 94f, 66f, 25f, -83f, -83f, 181f, 181f, 205f, 215f, 209f, 189f, 153f, 103f, 38f, -54f, -54f, 115f, 438f, 585f, 509f, 472f, 519f, 455f, 336f, 199f, 64f },
        // 五段 (skill 15690, SkillMove 162, 7 frames)
        new float[] { 0f, -148f, -609f, -889f, -1004f, -1157f, -1295f },
        // 切入 (skill 15554, SkillMove 126, 38 frames)
        new float[] { 0f, 1266f, 1024f, 907f, 834f, 783f, 744f, 713f, 688f, 667f, 649f, 634f, 620f, 608f, 596f, 586f, 577f, 568f, 560f, 552f, 545f, 538f, 531f, 524f, 518f, 511f, 505f, 498f, 490f, 482f, 472f, 458f, 426f, 335f, 267f, 254f, 272f, 209f },
    };
}
