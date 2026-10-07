// Mount core — character 3.x workstream W4, re-derived against docs/character/SPEC_MOUNT.md
// (the spec supersedes the assumed behaviors; 3_6_MOUNTS_GLIDER.md is reference only).
//
// Implemented per spec:
//   §1.2  state fields (bOnHorse/bHoldHorse model, item/attr id, bSprintFlag, parachute/hang)
//   §1.4  RideHorse guards (host models: bIgnoreGravity=!grounded, [+0x160]=0 always,
//         horse-item precondition via the host item model, already-mounted reject)
//   §1.5  DownHorse (no airborne guard; unequip loop modeled + logged)
//   §2.1  ride speeds 8/40 (尺/s -> host 120/600 u/s at its documented bridge) and the
//         ride yaw rate 0.003465 (x1000 = host ms convention) / reset 0.0023
//   §3.1  Space routing: idle/backward -> skill 13618 (no jump); moving -> skill 44565
//         + jump control; the host logs the mapped skill ids
//   §3.2  Jump branches: bSprintFlag != 0 = sprint branch (power gate, horse triple
//         60/180/11, jumpCount==1 && deliberate sprint -> DownHorse first); flag == 0 =
//         branch B (no horse triple; host reject on jumpCount>=1, generic arc on 0)
//   §3.3  the host is the server: bSprintFlag = mounted && moving-forward && (grounded ||
//         deliberate sprint intent); sprint power pool is a host-server rule
//   §4    facing/seat skeleton-derived (b_hs seat, head-vs-travel verified); CameraAdjust
//         and ModelScale/SocketScale read from rides.txt semantics
//   §5    steady gait from the adjust-table mapping; ride_rush fade values logged
//
// Registered provisionals / deviations (AGENTS §6):
//   P1 sprint power pool values (max/cost/regen) are host-server rules (RC_RIDE_POWER*).
//   P2 CameraAdjust application unit unresolved: logged + a pitch offset at the
//      provisional scale 0.01 deg/unit (RC_CAM_RIDE_ADJ_SCALE); re-open with a
//      represent consumer probe.
//   P3 deliberate sprint intent input is the host's rule (RC_MOUNT_SPRINT); the client
//      receives bSprintFlag from the server record.
//   D1 rider seated on the horse b_hs bone (s_hs socket uninitialized on the dummy
//      path) - verified 174 u; re-open with a socket bind.
//   D2 horse inventory is the host item model (equip/un-equip steps logged as modeled).
//   D3 the fade-in clip H加速奔跑01.tani AVs the MovieEditor host (runs 193436/193525);
//      fade values are logged, the steady gait is played. Re-open when the fade phases
//      are implemented with a safe clip.
//   D4 remote sync is applied through ApplySyncRecord (the host has no remote actors);
//      the criterion-5 proof drives that function directly.

using System;
using MovieEngineCLR;

internal sealed class MountState
{
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

    // §1.2 state (host model)
    public bool Mounted;
    public bool ItemEquipped = true;      // host item model (RC_HORSE_ITEM=0 -> criterion 1)
    public int HorseAttrId = 10000;       // host item's horse attribute id (criterion 2)
    public bool SprintFlag;               // §3.3 host-server rule (set per frame)
    public bool SprintIntent;             // deliberate sprint (P3, RC_MOUNT_SPRINT)
    public bool Parachute, Hang;          // §3.2 guards 3 (RC_MOUNT_PARACHUTE / RC_MOUNT_HANG)
    public float PowerMax = 100f, Power = 100f, PowerCost = 25f;   // P1
    public float RegenPerSec = 25f;
    public float TurnRate = 3.465f;       // §2.1 0.003465 * 1000 (host ms convention)
    public float TurnRateReset = 2.3f;    // §2.1 0.0023 * 1000 (logged; reset path unused)

    // §4 ride row facts
    public int CameraAdjust;              // 0 for ride 0; 80 for rides 1-6
    public float ModelScale = 1f, SocketScale = 1f;
    public bool AdjustTableAbsent;        // criterion 20 fallback

    public int RideType;
    public string Model, ClipIdle, ClipRun, ClipJump, RiderClip, RiderJump;

    public long Handle;
    public long AttachedHandle = -999;
    public bool Dbg;
    public IntPtr SeatActor = IntPtr.Zero;
    public int SeatIdx = -1;
    public IntPtr HeadActor = IntPtr.Zero;
    public int HeadIdx = -1;
    public float FacingOffset;
    private long lastSeatDbg;
    private long lastPowerRegen;
    public KGModelCLR Model_;
    public string CurHorseClip;
    private float lastX = float.MaxValue, lastY = float.MaxValue, lastZ = float.MaxValue, lastYaw = float.MaxValue;

    public MountState(int rideType, string model, string clipIdle, string clipRun,
        string clipJump, string riderClip, string riderJump)
    {
        if (rideType < 0 || rideType >= HorseModels.Length)
        {
            // criterion 20: the ride is absent from the host's adjust-table set
            AdjustTableAbsent = true;
            rideType = 0;
        }
        RideType = rideType;
        Model = model != null && model.Length > 0 ? model : HorseModels[rideType];
        ClipIdle = clipIdle != null && clipIdle.Length > 0 ? clipIdle : DefaultClipIdle;
        ClipRun = clipRun != null && clipRun.Length > 0 ? clipRun : DefaultClipRun;
        ClipJump = clipJump != null && clipJump.Length > 0 ? clipJump : DefaultClipJump;
        RiderClip = riderClip != null && riderClip.Length > 0 ? riderClip : DefaultRiderClip;
        RiderJump = riderJump != null && riderJump.Length > 0 ? riderJump : DefaultRiderJump;
        CameraAdjust = rideType == 0 ? 0 : 80;   // rides.txt rides 1-6
    }

    // §1.4 RideHorse (guards checked by the caller; this is the apply side) - the
    // host server face. Fades (criterion 22) and per-row facts are logged here.
    public bool Mount(KGSceneCLR scene, float x, float y, float z, float yaw, Action<string> log)
    {
        if (Mounted) return true;
        try
        {
            var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
            float half = yaw * 0.5f;
            var rot = new CLRfloat4();
            rot.x = 0f; rot.y = (float)Math.Sin(half); rot.z = 0f; rot.w = (float)Math.Cos(half);
            var scl = new CLRfloat3(); scl.x = ModelScale; scl.y = ModelScale; scl.z = ModelScale;
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
            Power = PowerMax;
            log(string.Format("mount: on ride={0} item={1} attrId={2} model='{3}' scale={4:F2}/{5:F2} handle={6} at ({7:F0},{8:F0},{9:F0})",
                RideType, ItemEquipped ? 1 : 0, HorseAttrId, Model, ModelScale, SocketScale, Handle, x, y, z));
            // §5.3 fade data (criterion 22) - logged; the fade-in clip AVs the host (D3)
            log("mount fades (ride_rush): in d=1000 t=3000ms opaque=1100 clip=H加速奔跑01.tani (D3: not played); out d=15000 t=10000ms ang=15deg stopOnce=H加速奔跑停止01.tani stopSteady=H普通待机01.ani speed=0.3 ratio=0.1");
            // §4 CameraAdjust (P2)
            log(string.Format("mount camera: CameraAdjust={0} (ride {1}) + CameraRideYawOffset=0; application scale 0.01 deg/unit = {2:F2} deg (P2 provisional)",
                CameraAdjust, RideType, CameraAdjust * 0.01f));
            // §2.1 turn rates (criterion 16)
            log(string.Format("mount turn: CharacterRideYawTurnSpeed 0.003465 (*1000={0:F3} rad/s) / reset 0.0023 (*1000={1:F1}); unmounted table 0.007465",
                TurnRate, TurnRateReset));
            if (AdjustTableAbsent)
                log("mount adjust table: ride absent -> AdjustAniID 0 fallback, base clips used (criterion 20)");
            return true;
        }
        catch (Exception e) { log("mount ex: " + e.Message); return false; }
    }

    // §1.5 DownHorse: no airborne/state guard; unequip loop modeled + logged (D2).
    public void Dismount(KGSceneCLR scene, Action<string> log)
    {
        if (!Mounted) return;
        try { scene.RemoveDummyModel("mount_horse"); }
        catch (Exception e) { log("dismount ex: " + e.Message); }
        log(string.Format("mount: DownHorse unequip model - boxes 0x18..0x1B (4 horse-equip items) + horse item attr={0} (D2)", HorseAttrId));
        Mounted = false;
        Handle = 0;
        AttachedHandle = -999;
        CurHorseClip = null;
        SprintFlag = false;
        log("mount: off (bOnHorse=0, bHoldHorse=0)");
    }

    // §1.3 packet face (criterion 5): remote character sync applies flags + attr id
    // directly, no local guard (server-authoritative). The host has no remote actors -
    // the criterion proof drives this function.
    public void ApplySyncRecord(bool onHorse, bool holdHorse, int attrId, Action<string> log)
    {
        Mounted = onHorse;
        HorseAttrId = attrId;
        log(string.Format("mount sync record (remote semantics, no guard): bOnHorse={0} bHoldHorse={1} attrId={2}",
            onHorse ? 1 : 0, holdHorse ? 1 : 0, attrId));
    }

    // Per-frame: place the horse at the rider (D1) and select the gait clip (§5.2).
    // Sprint power regen is a host-server rule (P1).
    public void Update(KGSceneCLR scene, float x, float y, float z, float yaw,
        bool grounded, bool moving, Action<string> log)
    {
        if (!Mounted) return;
        try
        {
            if (grounded && Power < PowerMax &&
                Environment.TickCount - lastPowerRegen >= 200)
            {
                lastPowerRegen = Environment.TickCount;
                Power += RegenPerSec * 0.2f;
                if (Power > PowerMax) Power = PowerMax;
            }
            float hyaw = yaw + FacingOffset;
            if (Math.Abs(x - lastX) > 0.5f || Math.Abs(y - lastY) > 0.5f ||
                Math.Abs(z - lastZ) > 0.5f || Math.Abs(hyaw - lastYaw) > 0.01f)
            {
                var pos = new CLRfloat3(); pos.x = x; pos.y = y; pos.z = z;
                float half = hyaw * 0.5f;
                var rot = new CLRfloat4();
                rot.x = 0f; rot.y = (float)Math.Sin(half); rot.z = 0f; rot.w = (float)Math.Cos(half);
                var scl = new CLRfloat3(); scl.x = ModelScale; scl.y = ModelScale; scl.z = ModelScale;
                long prev = Handle;
                Handle = scene.AddDummyModel("mount_horse", Model, pos, rot, scl);
                if (Dbg && Handle != prev)
                    log("mount dbg: re-add handle " + prev + " -> " + Handle + " at (" + (int)x + "," + (int)y + "," + (int)z + ")");
                lastX = x; lastY = y; lastZ = z; lastYaw = hyaw;
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
                ResolveFacing(log);
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

    // Horse facing from the skeleton (head/tail), runtime-verified by the caller.
    private void ResolveFacing(Action<string> log)
    {
        FacingOffset = 0f;
        try
        {
            int hi = -1, ti = -1;
            IntPtr ha = CameraShim.FindBoneActor(new IntPtr(Handle), "bip01_horse head", out hi);
            IntPtr ta = CameraShim.FindBoneActor(new IntPtr(Handle), "bip01_horse tail", out ti);
            HeadActor = ha; HeadIdx = hi;
            float[] mh = new float[16], mt = new float[16];
            if (ha != IntPtr.Zero && hi != -1 && ta != IntPtr.Zero && ti != -1 &&
                CameraShim.ActorBoneMatrix(ha, hi, mh) == 0 &&
                CameraShim.ActorBoneMatrix(ta, ti, mt) == 0)
            {
                double fx = mh[12] - mt[12], fz = mh[14] - mt[14];
                double len = Math.Sqrt(fx * fx + fz * fz);
                if (len > 1e-3)
                {
                    fx /= len; fz /= len;
                    double alpha = Math.Atan2(fz, fx);
                    FacingOffset = (float)(alpha - Math.PI / 2.0);
                    log(string.Format(
                        "mount facing: tail->head f=({0:F2},{1:F2}) alpha={2:F0}deg offset={3:F0}deg (model-Z dot {4:F2})",
                        fx, fz, alpha * 180.0 / Math.PI, FacingOffset * 180.0 / Math.PI, -fz));
                }
                else log("mount facing: head/tail coincide (offset 0)");
            }
            else log("mount facing: horse bones unresolved (offset 0)");
        }
        catch (Exception e) { log("mount facing ex: " + e.Message); }
    }

    // Horse head world position (runtime facing proof).
    public bool HeadWorld(float hx, float hy, float hz, float riderYaw,
        out float x, out float y, out float z)
    {
        x = hx; y = hy; z = hz;
        try
        {
            if (HeadActor == IntPtr.Zero || HeadIdx == -1) return false;
            float[] m = new float[16];
            if (CameraShim.ActorBoneMatrix(HeadActor, HeadIdx, m) != 0) return false;
            double yaw = riderYaw + FacingOffset;
            double ca = Math.Cos(yaw), sa = Math.Sin(yaw);
            double tx = m[12], ty = m[13], tz = m[14];
            x = (float)(hx + tx * ca + tz * sa);
            y = (float)(hy + ty);
            z = (float)(hz - tx * sa + tz * ca);
            return true;
        }
        catch { return false; }
    }

    // §4 seat: the horse skeleton's b_hs bone (s_hs socket parent; D1).
    private void ResolveSeat(Action<string> log)
    {
        try
        {
            SeatActor = CameraShim.FindBoneActor(new IntPtr(Handle), "b_hs", out SeatIdx);
            log("mount seat bone: actor=0x" + SeatActor.ToInt64().ToString("X") + " idx=" + SeatIdx);
        }
        catch (Exception e) { log("mount seat ex: " + e.Message); }
    }

    public bool SeatWorld(float hx, float hy, float hz, float riderYaw, Action<string> log,
        out float x, out float y, out float z)
    {
        x = hx; y = hy; z = hz;
        try
        {
            if (SeatActor == IntPtr.Zero || SeatIdx == -1) return false;
            float[] m = new float[16];
            if (CameraShim.ActorBoneMatrix(SeatActor, SeatIdx, m) != 0) return false;
            double yaw = riderYaw + FacingOffset;
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
