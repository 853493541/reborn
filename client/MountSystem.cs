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
//   1. The rider is seated on the horse's b_hs BONE, not socket-bound: the rider
//      model is placed at the b_hs matrix composed to world (the s_hs socket stays
//      uninitialized on the dummy path; b_hs is the socket's parent - verified
//      idx/height 174 u on Horse_01, 2026-10-06). The real client binds the rider
//      to s_hs (BindTo); re-open for the socket bind when dummy sockets initialize
//      or the Represent bind becomes reachable.
//   2. Horse inventory (equip boxes 0x18-0x1B, horse-item exterior) does not exist in
//      the host: T toggles the flag directly (KPlayer::RideHorse precondition/apply
//      fns 0x140363AF0/0x140363C40 are inventory-side and are not modeled).
//      Re-open when an item/inventory layer exists.
//   3. The school-999 PlayerRush rows examined (2026-10-06) are the acceleration/
//      afterimage set (bqg加速跑 clips + horse dismount columns 48/49), NOT a
//      mounted locomotion override; the mounted rider pose is ride_rush col 23.
//      The runtime mount override key stays open. The horse gait itself uses the
//      engine's adjust-table mapping (RideType 0: Idle->10030, RunForward->10016,
//      BeginJumpOnce->10204).
//   4. Ride yaw values (0.003465 / 0.0023 1/ms) have no located consumer; turn rate
//      stays the camera-row value. Re-open when the represent consumer is decoded.

using System;
using MovieEngineCLR;

internal sealed class MountState
{
    // rides.txt horse family (RideType -> model). Steady-state gait clips come from
    // the engine's own mapping: player_animation_adjust_rides_type_state.txt
    //   RideType 0: Idle -> 10030, RunForward -> 10016, BeginJumpOnce -> 10204
    // resolved through rides_animation.txt (RepresentID 0) to the files below.
    // NOTE: ride_rush.txt columns [13]-[15] are fade-in/stop hints (H加速奔跑01.tani
    // etc.), NOT the steady gait; H加速奔跑01.tani AVs the MovieEditor host when
    // played on the horse dummy (evidence: scratch runs 3x_mount 193436/193525) and
    // is not used until the fade phases exist.
    public static readonly string[] HorseModels = new string[]
    {
        @"data\source\NPC_source\Horse\模型\Horse_01_01a_00.mdl",
        @"data\source\NPC_source\Horse\模型\Horse_01_01a_01.mdl",
        @"data\source\NPC_source\Horse\模型\Horse_02_01b_01.mdl",
        @"data\source\NPC_source\Horse\模型\Horse_03_01c_01.mdl"
    };
    public const string DefaultClipIdle = @"data\source\NPC_source\Horse\动作\H普通待机01.tani";
    public const string DefaultClipRun = @"data\source\NPC_source\Horse\动作\H奔跑01.tani";
    public const string DefaultClipJump = @"data\source\NPC_source\Horse\动作\H小跳a.ani";
    public const string DefaultRiderClip = @"data\source\player\f1\动作\f1bqg_horse_run.ani";
    public const string DefaultRiderJump = @"data\source\player\f1\动作\f1H小跳a.ani";

    public bool Mounted;
    public int RideType;
    public string Model, ClipIdle, ClipRun, ClipJump, RiderClip, RiderJump;

    public long Handle;
    public long AttachedHandle = -999;
    public bool Dbg;
    public IntPtr SeatActor = IntPtr.Zero;
    public int SeatIdx = -1;
    private long lastSeatDbg;
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
                long prev = Handle;
                Handle = scene.AddDummyModel("mount_horse", Model, pos, rot, scl);
                if (Dbg && Handle != prev)
                    log("mount dbg: re-add handle " + prev + " -> " + Handle + " at (" + (int)x + "," + (int)y + "," + (int)z + ")");
                lastX = x; lastY = y; lastZ = z; lastYaw = yaw;
            }
            if (Handle != AttachedHandle)
            {
                Model_ = new KGModelCLR();
                Model_.AttachModel(Handle);
                AttachedHandle = Handle;
                CurHorseClip = null;
                SeatActor = IntPtr.Zero;
                SeatIdx = -1;
                ResolveSeat(log);
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

    // Seat bone: the horse skeleton carries `b_hs` (idx 10 in Horse_01, parsed
    // from the shipped H普通待机01a.ani) - the horse-back bone the s_hs socket
    // parents to (docs/character/3_1_RIG_SOCKETS.md; the rider bind in the real
    // client is LoadRide -> s_hs). Bones resolve for dummy actors (the proven
    // head-bone anchor route); sockets stay uninitialized on the dummy path.
    private void ResolveSeat(Action<string> log)
    {
        try
        {
            SeatActor = CameraShim.FindBoneActor(new IntPtr(Handle), "b_hs", out SeatIdx);
            log("mount seat bone: actor=0x" + SeatActor.ToInt64().ToString("X") + " idx=" + SeatIdx);
        }
        catch (Exception e) { log("mount seat ex: " + e.Message); }
    }

    // Rider seat world position: horse placement x bone local (the same
    // composition the client's head-bone camera anchor uses). The rider clip
    // (`f1bqg_horse_run.ani`) is authored relative to this bind point, so the
    // rider model must be PLACED here - the game binds the rider actor to the
    // horse's s_hs; this is the same transform without the socket matrix.
    public bool SeatWorld(float hx, float hy, float hz, float yaw, Action<string> log,
        out float x, out float y, out float z)
    {
        x = hx; y = hy; z = hz;
        try
        {
            if (SeatActor == IntPtr.Zero || SeatIdx == -1) return false;
            float[] m = new float[16];
            if (CameraShim.ActorBoneMatrix(SeatActor, SeatIdx, m) != 0) return false;
            double ca = Math.Cos(yaw), sa = Math.Sin(yaw);
            double tx = m[12], ty = m[13], tz = m[14];
            x = (float)(hx + tx * ca + tz * sa);
            y = (float)(hy + ty);
            z = (float)(hz - tx * sa + tz * ca);
            if (Dbg && Environment.TickCount - lastSeatDbg > 2000)
            {
                lastSeatDbg = Environment.TickCount;
                log(string.Format("mount seat dbg local=({0:F1},{1:F1},{2:F1}) world=({3:F1},{4:F1},{5:F1})",
                    tx, ty, tz, x, y, z));
            }
            return true;
        }
        catch { return false; }
    }
}
