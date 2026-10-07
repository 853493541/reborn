// Mount core — character 3.x workstream W4 (horse phase 1).
//
// Decoded sources (game client, all read-only; docs/character/3_6_MOUNTS_GLIDER.md):
//   - Represent/rides/rides.txt        RideType -> MainModelFile / ModelScale / IdleAniID
//   - Represent/rides/ride_rush.txt    RideType x body type rider + horse gait clips
//       [13] 跑步动作 (horse run)      [14] 跳跃动作 (horse jump)     [15] 待机 (horse idle)
//       [23] 人物骑马动作 (rider pose) [26] 人物骑马一段跳 (rider jump)
//       [32] 下马动作 (rider dismount, not yet wired)
//   - KPlayer::RideHorse 0x14036C210 / DownHorse 0x140365C60 (flag/guard semantics)
//   - Horse jump triple 60/180/11 u/frame (KCharacter::Jump 0x140313A48); the
//     [+0x34C] script bonus (HORSE_JUMP_SPEED_ADDITIONAL) is server/script — 0 here.
//   - CommonNumber CharacterRideWalkSpeed=8 / CharacterRideRunSpeed=40 u/logic-frame
//     at the verified 15 Hz tick -> 120 / 600 u/s (proof/gravity/number.krl.txt).
//
// Registered deviations (AGENTS §6; re-open criteria below):
//   1. The rider is NOT socket-bound. The real client binds the rider to the horse's
//      s_hs socket (KRLLocalCharacter::MountMannedSpace / slot_link); the CLR exposes
//      no bone/socket transform (docs/character/3_1_RIG_SOCKETS.md). Both models are
//      placed at the player position; the authored riding clip carries the seated pose.
//      Re-open when Agent A's socket shim (RC_ActorSocketMatrix) lands.
//   2. Horse inventory (equip boxes 0x18-0x1B, horse-item exterior) does not exist in
//      the host: T toggles the flag directly (KPlayer::RideHorse precondition/apply
//      fns 0x140363AF0/0x140363C40 are inventory-side and are not modeled).
//      Re-open when an item/inventory layer exists.
//   3. The ride animation adjust table (player_animation_adjust_rides_type_state) and
//      the 999-sentinel PlayerRush row are NOT consumed yet; phase 1 selects the
//      explicit ride_rush gait columns (idle/run/jump). Re-open with the locomotion
//      pass (W2 wiring).
//   4. Ride yaw values (0.003465 / 0.0023 1/ms) have no located consumer; turn rate
//      stays the camera-row value. Re-open when the represent consumer is decoded.

using System;
using MovieEngineCLR;

internal sealed class MountState
{
    // rides.txt horse family (RideType -> model); clips are the ride_rush RideType-0 set
    // (ride_rush carries no rows for 1/2; the horsle family shares the authored set).
    public static readonly string[] HorseModels = new string[]
    {
        @"data\source\NPC_source\Horse\模型\Horse_01_01a_00.mdl",
        @"data\source\NPC_source\Horse\模型\Horse_01_01a_01.mdl",
        @"data\source\NPC_source\Horse\模型\Horse_02_01b_01.mdl",
        @"data\source\NPC_source\Horse\模型\Horse_03_01c_01.mdl"
    };
    public const string DefaultClipIdle = @"data\source\NPC_source\Horse\动作\H普通待机01.ani";
    public const string DefaultClipRun = @"data\source\NPC_source\Horse\动作\H加速奔跑01.tani";
    public const string DefaultClipJump = @"data\source\NPC_source\Horse\动作\H小跳a.ani";
    public const string DefaultRiderClip = @"data\source\player\f1\动作\f1bqg_horse_run.ani";
    public const string DefaultRiderJump = @"data\source\player\f1\动作\f1H小跳a.ani";

    public bool Mounted;
    public int RideType;
    public string Model, ClipIdle, ClipRun, ClipJump, RiderClip, RiderJump;

    public long Handle;
    public long AttachedHandle = -999;
    public KGModelCLR Model_;
    public string CurHorseClip;
    private float lastX = float.MaxValue, lastY = float.MaxValue, lastZ = float.MaxValue, lastYaw = float.MaxValue;

    public MountState(int rideType, string model, string clipIdle, string clipRun,
        string clipJump, string riderClip, string riderJump)
    {
        if (rideType < 0 || rideType >= HorseModels.Length) rideType = 0;
        RideType = rideType;
        Model = model != null && model.Length > 0 ? model : HorseModels[rideType];
        ClipIdle = clipIdle != null && clipIdle.Length > 0 ? clipIdle : DefaultClipIdle;
        ClipRun = clipRun != null && clipRun.Length > 0 ? clipRun : DefaultClipRun;
        ClipJump = clipJump != null && clipJump.Length > 0 ? clipJump : DefaultClipJump;
        RiderClip = riderClip != null && riderClip.Length > 0 ? riderClip : DefaultRiderClip;
        RiderJump = riderJump != null && riderJump.Length > 0 ? riderJump : DefaultRiderJump;
    }

    // KPlayer::RideHorse apply: mount flag set, hold-horse cleared. Returns false and
    // logs the decoded guard when the mount is rejected.
    public bool Mount(KGSceneCLR scene, float x, float y, float z, float yaw, Action<string> log)
    {
        if (Mounted) return true;
        try
        {
            var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
            float half = yaw * 0.5f;
            var rot = new CLRfloat4();
            rot.x = 0f; rot.y = (float)Math.Sin(half); rot.z = 0f; rot.w = (float)Math.Cos(half);
            var scl = new CLRfloat3(); scl.x = 1f; scl.y = 1f; scl.z = 1f;
            Handle = scene.AddDummyModel("mount_horse", Model, pos, rot, scl);
            if (Handle == 0 || Handle == -1)
            {
                scene.RemoveDummyModel("mount_horse");
                Handle = scene.AddDummyModel("mount_horse", Model, pos, rot, scl);
            }
            CurHorseClip = null;
            AttachedHandle = -999;
            lastX = lastY = lastZ = lastYaw = float.MaxValue;
            Mounted = true;
            log(string.Format("mount: on ride={0} model='{1}' handle={2} at ({3:F0},{4:F0},{5:F0})",
                RideType, Model, Handle, x, y, z));
            return true;
        }
        catch (Exception e) { log("mount ex: " + e.Message); return false; }
    }

    // KPlayer::DownHorse: release the ride actor, clear both flags.
    public void Dismount(KGSceneCLR scene, Action<string> log)
    {
        if (!Mounted) return;
        try { scene.RemoveDummyModel("mount_horse"); }
        catch (Exception e) { log("dismount ex: " + e.Message); }
        Mounted = false;
        Handle = 0;
        AttachedHandle = -999;
        CurHorseClip = null;
        log("mount: off (DownHorse)");
    }

    // Per-frame: place the horse at the rider (deviation 1) and select the gait clip.
    public void Update(KGSceneCLR scene, float x, float y, float z, float yaw,
        bool grounded, bool moving, Action<string> log)
    {
        if (!Mounted) return;
        try
        {
            if (Math.Abs(x - lastX) > 0.5f || Math.Abs(y - lastY) > 0.5f ||
                Math.Abs(z - lastZ) > 0.5f || Math.Abs(yaw - lastYaw) > 0.01f)
            {
                // same-name AddDummyModel keeps the handle and the running clip
                // (proven player pattern, client/RebornClient.cs placePlayer).
                var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
                float half = yaw * 0.5f;
                var rot = new CLRfloat4();
                rot.x = 0f; rot.y = (float)Math.Sin(half); rot.z = 0f; rot.w = (float)Math.Cos(half);
                var scl = new CLRfloat3(); scl.x = 1f; scl.y = 1f; scl.z = 1f;
                Handle = scene.AddDummyModel("mount_horse", Model, pos, rot, scl);
                lastX = x; lastY = y; lastZ = z; lastYaw = yaw;
            }
            if (Handle != AttachedHandle)
            {
                Model_ = new KGModelCLR();
                Model_.AttachModel(Handle);
                AttachedHandle = Handle;
                CurHorseClip = null;
            }
            string want = !grounded ? ClipJump : moving ? ClipRun : ClipIdle;
            if (want != CurHorseClip)
            {
                int rc = Model_.PlayAnimation(want, 0, 1.0f, 0);
                log("mount horse clip -> " + want + " (" + rc + ")");
                CurHorseClip = want;
            }
        }
        catch (Exception e) { log("mount update ex: " + e.Message); }
    }
}
