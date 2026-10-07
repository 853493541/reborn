# 3.6 Mounts / vehicles / glider / parachute — deep research (scratch deliverable)

**Area:** character · **Branch:** research/character-animation · **Index:** `docs/character/README.md`

**Date:** 2026-10-06
**Client:** `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64` (1.5.0.9975): `JX3ClientX64.exe`
(image base `0x140000000`), `JX3RepresentX64.dll` (image base `0x180000000`).
**Scratch work dir:** `C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\`
(no repo files were modified).
**Method:** string-table decode + targeted disasm of the live binaries (read-only), plus
`tools/netcode/extract_pak_paths.py` extraction of the ride/vehicle data tables into scratch.

> **Encoding note for all string addresses below.** The committed string dumps
> (`proof/gravity/JX3*_strings.txt`) print **file offsets**, not VAs. VA = image base + RVA,
> where RVA = file offset + section delta (`.rdata` delta is **+0x1200** in the exe and
> **+0x1e00** in the represent DLL; `.data` delta is **+0x1c00** in the exe). Example:
> `bOnParachuteFlag` printed as `0x0084B508` is file offset 0x84B508 → VA
> `0x14084C708`. This invalidates any doc that treats those numbers as addresses.

---

## Verified facts

### A. Horse / mount (logic + represent)

1. **Mount flags on `KCharacter`** (HIGH). `bOnHorse` = `[char+0x1E774]`,
   `bHoldHorse` = `[char+0x1E77C]`. Both are registered script attributes whose
   descriptor getters read those offsets: getters `0x1403E1B70` / `0x1403E1B90` in the
   exe character-attribute table (table base file `0xa10990`, 32-byte entries
   `{name*, 0, getter, type}`; names `bOnHorse`/`bHoldHorse`). The server data apply path
   already documented in `docs/movement/JX3_GRAVITY_RESEARCH.md` §3.12 sets both from
   `qword[display+0x7A]` bits 61/62 — confirmed consistent.
2. **`KPlayer::RideHorse` = `0x14036C210`** (HIGH). Guards `[+0x208]==0`,
   `[+0x160]==0`, then flag logic: `bHoldHorse` requires `bOnHorse`; plain mount requires
   `bOnHorse==0` (already mounted → reject). Calls `0x140363AF0` (mount precondition) and
   `0x140363C40` (mount apply); sets `[+0x1E774]=1`, `[+0x1E77C]=0`, returns 1.
   Assert string `KPlayer::RideHorse` (file 0x839718 → VA 0x14083A918).
3. **`KPlayer::DownHorse` = `0x140365C60`** (HIGH). Requires `[+0x1E774]!=0`; fetches the
   equipped horse item through the item container at `[char+0xB980]` (`piHorse` assert);
   checks its vtable `+0x108`; loops item boxes **0x18..0x1B** (the four `HORSE_EQUIP`
   positions) unequipping via `0x140333570`/`0x140335C10`; on success clears
   `[+0x1E774]=0` and `[+0x1E77C]=0` (0x140365D98), returns 1.
4. **`ProcessRideHorse` = `0x1403AFB80`** (HIGH) is the ride/dismount command entry:
   `ProcessRideHorse(pDstCharacter, bRide)`: rejects NPCs (`id & 0x40000000`), then
   `bRide ? KPlayer::RideHorse : KPlayer::DownHorse`. This is the server/script face of the
   T action.
5. **Horse jump triple** (HIGH, verified in `KCharacter::Jump` 0x140313680 disasm):
   mounted (`[+0x1E774]!=0`) and `jumpCount<1` selects the horse row
   `g_pSO3World + 0x25F50 + 6*(school + jumpCount)`: word0 = `HorseJumpSpeedXY`,
   word1 = `HorseVelocityZ` (`r14d += [char+0x34C]`), word2 = `HorseGravity`
   (0x140313A48–0x140313A88). `jumpCount>=1` while mounted is **rejected** (0x140313A52).
   `JumpParam.tab` columns 159–161 (`proof/gravity/JumpParam.tab`): school 0 (and every
   shipped school row) `HorseJumpSpeedXY0=60`, `HorseVelocityZ0=180`, `HorseGravity0=11`.
   With 1 u/frame = 15 Hz, that is Vz 180 u/f ≈ 14.1 m/s, g = 11 u/f² (same as a normal
   jump), Vxy 60 u/f ≈ 4.7 m/s.
6. **Mid-air dismount** (HIGH): in `KCharacter::Jump`, when mounted and
   `jumpCount == 1`, it calls `KPlayer::DownHorse` (0x140365C60) at 0x140313A30 *before*
   the source selection; the flag is cleared, so the second jump press becomes a normal
   jump. This is the "leap off the horse" path.
7. **`[char+0x34C]` = horse jump Vz bonus** (HIGH). Only read at 0x140313A81
   (`add r14d, [rbx+0x34C]`); no direct writer in the exe — it is zeroed in character
   init (0x1403128F8) and set by the `HORSE_JUMP_SPEED_ADDITIONAL` script op (see op table
   below) or server sync. It is adjacent to `nDivingCount` (+0x350).
8. **Ride speeds / turn rates** (HIGH, `proof/gravity/number.krl.txt:25-29,113-115`):
   `CharacterRideWalkSpeed=8`, `CharacterRideRunSpeed=40` (尺/s), `CharacterRideYawTurnSpeed=0.003465`,
   `CharacterRideYawResetSpeed=0.0023`, `CharacterVehicleYawTurnSpeed=0.006283`,
   `CameraRideYawOffset=0`, `RideFootContactGroundDis=5`, `RideFootprintLife=5000`,
   `RideFootprintApplyTimeInterval=300`. CommonNumber field offsets verified from the
   loader `sLoadNumberFromFile` (`JX3RepresentX64.dll 0x180857DF0`): walk `+0x4C`,
   run `+0x50`, swim `+0x54`, yaw `+0x58`, yaw reset `+0x5C`, ride walk `+0x60`,
   ride run `+0x64`, ride yaw `+0x68`, ride yaw reset `+0x6C`, vehicle yaw `+0x70`.
   Consumer sites for the ride fields were **not** located statically (the struct is read
   through the accessor `0x180338BF0` into a register, so RIP-relative scans miss it) —
   open item.
9. **Horse-adjacent script-op vocabulary** (HIGH, exe op-name table at file `0x9f4d40`,
   16-byte `{name*, id}` entries; this is the "set character movement attribute" op enum):
   `RUN_SPEED_BASE`=1, `MOVE_SPEED_PERCENT`=2, `MIN_RUN_SPEED`=3, `MAX_RUN_SPEED`=4,
   `MAX_JUMP_COUNT`=5, `SET_JUMP_COUNT`=6, `FLY_FLAG`=7, `ON_TOWER_FLAG`=8,
   `ON_PARACHUTE_FLAG`=9, `HORSE_ATTRIBUTE`=10, `HORSE_RUN_TYPE`=11,
   `HORSE_JUMP_SPEED_ADDITIONAL`=0x12, `GRAVITY_BASE`=0xE, `GRAVITY_PERCENT`=0xF,
   `JUMP_SPEED_BASE`=0x10, `JUMP_SPEED_PERCENT`=0x11, `DROP_DEFENCE`=0x13,
   `DIVING_FRAME_*`=0x14/0x15, `MODIFY_DIVING_COUNT`=0x26B, `WATER_FLY_ABILITY`=0x16.
   (Distinct from the neighbouring horse-detail enum table ending at file 0x9f4d30 with
   `HORSE`=0x15, `RARE_HORSE1..5`, `TOTAL`.)
10. **Horse item / exterior system** (HIGH names): exe `KItemList::{EquipHorse 0x831318,
    CanUnEquipHorse 0x8312F8, SetEquippedHorsePos, CanEquipHorseEquip, UnEquipHorseEquip}`,
    `KPlayerClient::{OnSyncHorseFlag 0x7C2510, OnSyncHorseExteriorData 0x7C54A0,
    OnBroadcastPlayerChangeHorseRunType 0x7C5958}`, `KCharacter::{SetHorseAttributeID
    0x82D200, GetHorseAttributeID 0x82D250}`, `HORSE_RUN_TYPE`, `HORSE_CAN_SWIM`,
    `SprintAbleHorseMask`, `HorseSprintPowerMax/Cost/Revive` and
    `ADD_HORSE_SPRINT_POWER_MAX/COST/REVIVE` (sprint-power pool for mounted sprint),
    `SelfHorseStateRequest`/`TargetHorseStateRequest` (skill-condition queries),
    `SKILL_HORSE_STATE_CONDITION` with tokens `Horse`/`NotHorse`/`EquipSpecialHorse`/
    `NotHoldHorse`. All names are string-dump file offsets; treat as symbol names, not VAs.
11. **Represent ride objects and animation state** (HIGH):
    `pRide` = `[pRLCharacter+0x39F8]`, `pRideRush` = `[+0x3A00]`, mount/camera socket =
    `[+0x3A08]` (camera research already used 0x3A08). `KRLCharacter::MountRide`
    (string VA 0x180CAE078, ref @0x1804E4FC6) / `KRLCharacter::ChangeRideState`
    (string VA 0x180CAE0C0) live in the block `0x1804E4F60` that dispatches mount/ride
    bind/unbind with flag bits 1/2/4/8 and binds `[+0x39F8]` via `0x18001909C`.
    `KRLCharacter::OfflineMountRide` (0x180CAE098), `LoadRide`/`UnloadRide` (0x180CACBB0),
    `RLState::GetRideStateAnimationID` (0x180CA9B58, fn ~0x18049F6D0, keys on
    `[state+0x18]==2`), `KRLRideRush::{Jump 0x180CAA490, BeginJump 0x180CAA4A8,
    EndJump 0x180CAA4C8, UpdateFlyJumpAnimation 0x180CAA598, UpdateSkillMoveAnimation
    0x180CAA680}`, `KRLRushState::UpdateRideJumpAnimation` (0x180CAB3E8, uses
    `[rush+0x5C]` mode / `[rush+0xAC]` index / `[rush+0x64]` selector to play the
    transition), `KRLRide::{BindCharacter 0x180CB6C20, UnbindCharacter 0x180CB6C40,
    IsBindCharacter 0x180CB6228, LuaGetTurnInputYaw 0x180469630}`.
    `KRLRide::LuaGetTurnInputYaw` body: `call 0x18001E5B0` (ride turn-input yaw getter) →
    double → Lua push; this is the `GetTurnInputYaw` referenced by
    `docs/controls/CONTROL_MODES_TRACEABILITY.md` A18.
12. **Ride animation selection** (HIGH): the mount/vehicle override in the locomotion
    lookup is real and data-driven: `sub_18085CE60` takes `[rushstate+0x74]!=0` and
    `[singleton+0x26466]!=0` and then uses **index 0x3E7 (999)** into the same 84-byte
    BinText locomotion rows (`docs/controls/CONTROL_MODES_P5_ANIM.md` §2). The
    `[singleton+0x26466]` flag is a config-option bool read at `0x1803185E0`. On top of
    that, rides have their **own** per-state animation adjust table (below).
13. **`rides.txt` is the mount actor table** (HIGH, extracted from the game paks; see
    "Data tables"): `RepresentID` (= RideType) → `MainModelFile`, `ModelScale`,
    `SocketScale`, `IdleAniID`, body/mane mesh+material+color channels, `CameraAdjust`,
    `TerrainSlopeType`, footprint SFX, water flags, etc. Examples: RideType 0/1/2 =
    `data\source\NPC_source\Horse\模型\Horse_0x_*.mdl` (the horse actors); RideType 29/30 =
    `WJ_飞机马车001` (flying carriage); 51/60/61/1152 = `data\source\doodad\万花谷\WJ_吊篮00x_HD.mdl`
    (hanging basket); 54/63/1218/1219 = `WJ_风筝00x` (kites); 137 = `WJ_热气球01` (hot-air
    balloon); 97 = `ZQ_熊猫拉车` (panda cart).
14. **`player_animation_adjust_rides_type_state.txt`** (HIGH, extracted): per `RideType`
    (0…148, ~600 rows in total with 3592 lines) × `AniState` name → `AdjustAniID`. The 64
    state names are the mounted animation vocabulary: `Idle, WalkForward, RunForward,
    RunBackward, WalkFightForward/Backward, RunFightForward/Backward, JumpingOnce,
    JumpingTwice, BeginJumpOnce/Twice, EndJumpIdleOnce/Twice, TurnLeft/Right, DashForward/
    Left/Right(+Idle), Halt, Entrap, SitDown, KnockOff, Slip*, SkillEffect{Hit,Dodge,Parry,
    PhysicsDamage}, Sheath/Unsheath variants, Swim* (Stand/Forward/Backward/Left/Right,
    SwimJumping, Begin/EndSwimJump), FlyForward/Backward/Left/Right/Up/Down/Float/FlyJump`.
    This is the per-RideType correction layer over the shared locomotion table.
15. **`ride_rush.txt`** (HIGH): `坐骑类型ID, 角色类型, 骑马技能ID, 淡入距离/时间, 淡入动作,
    淡出距离/时间/角度/动作, 跑步动作, 跳跃动作, 待机, 人物准备/上马/骑马动作,
    双人同骑下人物骑马动作, 人物骑马一段跳, 停滑动作, 下马动作, 马下落动作/速度/加速度,
    人在马上进入轻功动作（人/马）, 淡出变速率/停止比例, 第二阶段进入/维持动作`. RideType 29/30
    carry `骑马技能ID=4098`, fade 1000/3000 ms, and the flying-carriage glide/ascend set;
    RideType 30 `马下落速度=350`, `马下落加速度=10` (flying-mount descent law).
16. **`ride_link.txt`** (HIGH): the group/double-ride (共骑) table — host/follower roles,
    mount offset/angles, approach/mount/invite animations per body type, link sockets.
    RideTypes with double-ride support: 7 (boat), 9/13 (hot-air balloon), 20 (land eagle),
    29 (flying carriage), 30, plus 8/11/12/14/16-19/22/24/27/28/36/38-40/43-46/48/49/58/59.
17. **`pull_ride_config.krl.txt`** (HIGH): the "pull horse on a rope" (牵马) config —
    `HorseRopeMesh`, `HorseRopePosOffset=120`, `MaxHorseRopeLength=250`, forward/backward
    start distances, stop distance, idle-action limits. `bHoldHorse` (`[+0x1E77C]`) is the
    logic flag for this state; `KRLGroupRideLink`/`KRLRideLink`/`KRLHandLink`/`KRLSlotLink`
    are the represent links.
18. **`rides_animation.txt`** (HIGH): `RideType, AnimationID, flags, file` — the ride's own
    clip list (e.g. RideType 54/63 `WJ_风筝001_滑翔.tani`; 134/135 `WJ_飞机马车001_*`;
    137 `WJ_热气球01_*`; 51/60 `WJ_吊篮001_wlk01.tani`). 9.9 MB / ~125k rows.
19. **`rides_road.txt` / `vehicle.krl.txt` / `vehicle_model.krl.txt`** (HIGH): road-route
    carriages. `vehicle.krl.txt` is `RideType:VehicleType = --<route name>` → list of
    `{vehicleModelID, skillID, doodadID?}` (e.g. `2:3 = --稻香村驿站`). `vehicle_model.krl.txt`
    carries dev comments documenting the API: `/gm player.AutoFly(1233,1238)`,
    `/gm player.Stop()`.
20. **`special_buff_rides.txt`** (HIGH): buff IDs that force a ride (220, 221, 256, 252, 1288,
    …). **`rides_freaky.krl.txt`** (HIGH): "freaky"/transforming rides (hot-air balloon,
    flying carriage) with `BornAnimation`, `RideAnimation`, `FlyAwayAnimation`, model swaps.

### B. Vehicles / manned spaces (载体)

21. **Represent manned-space system** (HIGH names, `JX3RepresentX64.dll` string VAs; all
    string VAs = file offset + 0xE00, cross-checked by the multi-needle scanner):
    `KRLCharacter::{LoadMannedSpace 0x180CAFEA0, MountMannedSpace 0x180CAFFB8,
    UpdateMannedSpace 0x180CAE808}` (strings; `KRLCharacter::UnloadMannedSpace`,
    `CreateMannedSpace`, `PrepareEndMannedSpace`, `UpdateMannedSpaceSkillBuff` at
    +0x70/+0xC8/+0x230/+0x278 from those); `KRLLocalCharacter::MountMannedSpace`
    (string 0x180CB4040), `KRLRemoteCharacter::MountMannedSpace` (string 0x180CB5CC8);
    `KRLMannedSpace::{Init 0x180CB4640, LoadModel 0x180CB4660, UnloadModel 0x180CB4710,
    BindCharacter 0x180CB4738, UnbindCharacter 0x180CB4788, PlayAnimation 0x180CB47B0,
    RegisterModel 0x180CB47F0, UnregisterModel 0x180CB4818, SetSelectable 0x180CB4840,
    SetVisible 0x180CB4868, LoadMDL 0x180CB4890, ShowFrontSight 0x180CB48C0,
    UpdateFrontSight 0x180CB48E8, InitFrontSight 0x180CB4960, LoadSkillBuffPart 0x180CB49C0,
    ShowParabola 0x180CB4A88, HideParabola 0x180CB4B40, UpdateParabola 0x180CB4B68,
    UpdateParabolaTrajectory 0x180CB4BC8, SetPrabolaMDL 0x180CB4C50,
    InitParabolaFS 0x180CB4CA8}`. `KRLCharacter::UpdateMannedSpace` (fn ~0x1804E4DD0,
    string 0x180CAE808)
    keeps `pMannedSpace` at `[char+0x3A98]`, takes the manned-space ID as its argument, and
    re-creates the object when the ID changes; the disasm shows the ID check against
    `[char+0x4838]` (loaded-space manager) and pointer store `[+0x3A98]`.
22. **`mannedspace.krl.txt`** (HIGH, extracted): `载具ID` → `Name`, `MDL` per faction
    (0 中立 / 1 浩气 / 2 恶人), `Socket = "s_hs"` (character bind socket), `PrimaryBone`,
    `TerrainSlopeType`, `CameraAdjust`, `LocalPlayerAlpha`, `EnablePitch`, `OffsetY`,
    `SkillBuffParts` (buff → mesh+socket, e.g. buff 20268 → `D021001.Mesh` @ `s_rc`),
    and a `Transform` state→animation table (death 45021, walk 45022, run 45023/45072,
    backward 45024, …) with SFX type/speed/scale. IDs 1–16 and 37–51 are **神机车**
    (crossbow/cannon carts, e.g. id 1 MDL `WJ_js弩车001` per faction) and **摧城车**
    (siege ram) — the 攻防/campaign siege vehicles, not the BR war elephant. 3118 lines.
23. **War elephant 战象 is skill-driven, not a mannedspace row** (MED): mode skills
    `天原绝境_战象践踏 29146`, `_战象冲锋 29264`, `_战象下车 29267`, `战象践踏母 29283`,
    `战象推人母 29476` in `proof/netcode/mode_juejing/mode_kit_report.txt` (the report is
    **UTF-8**), scripts `沙漠风暴/天原绝境战象*.lua` are server-side and not shipped
    (`docs/netcode/JX3_MODE_JUEJING_LOGIC.md:25`). No `战象` entry exists in
    `mannedspace.krl.txt` (negative scan). The engine hook for a skill-created vehicle is
    `KRLCharacter::MountMannedSpace` + represent state `dwMannedSpaceID` (id 0xF4), but the
    binding is UNPROVEN.
24. **Airship 飞艇** (LOW/MED): only appears as a mode loot special
    (`docs/netcode/JX3_MODE_LOOT_SYSTEM.md:96` "飞艇 (airship, feiting)", next to
    驼铃 = mount summon). No client vehicle/mannedspace/ride row contains `飞艇` (negative
    scan of the extracted tables). Likely an item-summoned vehicle/mount — open.
25. **Conveyor belts (G-11)** (HIGH, unchanged): `KRLPxWorld::SetConveyorBeltParam` /
    `KRLSIMWorld::SetConveyorBeltParam` / `PhysxComponent::{SetConveyorBeltParam,
    LuaSetConveyorBeltParam}` + `represent/physic/physic_conveyor_belt_param.krl.txt`
    (`docs/movement/JX3_COLLISION_SYSTEM.md:873-875,2239`). No new mount coupling found.
26. **Vehicle action bars** (HIGH link): the dynamic-bar contexts are already decoded —
    `ExtentDynamicAction` (vehicles/pets/loot), `NpcAssistedDynamicAction`,
    `RougeDynamicAction`, `DynamicBattleRoyale`, `MobileSkillActionBar`
    (`docs/controls/JX3_COMBAT_CONTROLS.md:36`, `docs/controls/JX3_HOTKEY_SYSTEM.md:54,57`);
    up to 32 dynamic slots, BR bar 14 slots. A manned space / war elephant swaps the player
    into one of these contexts; the binding call is `KRLMannedSpace::LoadSkillBuffPart`
    (skill/buff-driven extra meshes) and the `Transform` animation table.

### C. Glider / 滑翔翼

27. **The mode skill is `天原绝境_滑翔翼` (SkillID 29021)** (HIGH): `KindType=Physics`,
    `FunctionType=Normal`, `CastMode=CasterSingle`, `ScriptFile=沙漠风暴/天原绝境_滑翔翼.lua`
    (`proof/netcode/mode_juejing/mode_kit_report.txt`, UTF-8; the file's Chinese is UTF-8,
    not GBK). The mode doc's "滑翔伞 (29021)" is a naming error.
28. **No dedicated glider move state exists** (HIGH, negative): the logic move-state
    vocabulary and `REBORN_JUMP_FALL_SPEC.md` §"Move-state vocabulary" contain no GLIDER;
    the only airborne states are JUMP 4, AUTOFLY 0xF, fly 0x1F/0x20/0x21, bird 0x23/0x24/0x25.
29. **Nav auto-fly is a path-driven move state** (HIGH): `KCharacter::ProcessNavAutoFly`
    (`0x1403175B0`) requires `[+0x1F4]==0xF` (AUTOFLY), auto-fly data `[+0xC28]`, path
    counter `[+0xC08]`, `[+0x3C0]` start flag, and ends with the shared vertical integrator
    `ProcessVerticalMove` (`0x140318C50`). `KCharacter::ProcessAutoFly` (`0x140316990`) is
    the other path follower. So altitude during auto-fly is the shared gravity integrator,
    not a bespoke glide equation.
30. **Nav-fly uses a vehicle ride** (HIGH): `CommonNumber` field `NavFlyVehicleRideID`
    = **1152** (`proof/gravity/number.krl.txt:133`; field offset `+0x248` verified from the
    loader at `0x180858E74`, RL singleton `0x180EDDFE0 + 0x24C14 + 0x248`). RideType 1152 in
    `rides.txt` = `data\source\doodad\万花谷\WJ_吊篮002_HD.mdl`, `IdleAniID=31157` (a hanging
    basket). Represent mirrors the vehicle-track state: state attrs `nVehicleTrack` (id
    0x9C) and `nVehicleNode` (id 0xA0); tables `m_pVehicleNodeFile` / `m_tabVehicleNode`
    (string VAs 0x180CD28A0 / 0x180CD28B8).
31. **Glider camera is camera-only** (HIGH): `GliderCamera` file mapping
    (`Represent/filepath.ini:245`), keys `LoadGliderParams` (string VA 0x180D0CA68, called
    at 0x180AF99CE inside `EnterGlider`), `EnterGlider` (string VA 0x180D0CC30, refs inside
    the fn at 0x180AF95A0 and in the controller at 0x180B1CAA4), `SmoothToGliderCamera`
    (0x180D0CD08; refs 0x180AFA7E2-0x180AFA84E and 0x180B1D741),
    `UpdateGliderCameraYaw` (0x180D0CDF0, ref 0x180AFB2BF), `ClampMouseForGliderCamera`
    (0x180D0EB28, ref 0x180B20235 in the sprint/controller update),
    `pGliderCameraController` (refs 0x180B1C4BC/0x180B1C4D3 in skill-move/carrier code).
    The controller's enter path (0x180B1C9E9–0x180B1CB17) looks up a camera row from
    `[singleton+0x1A0]` by `[row+0x20]` and calls the enter core `0x18000BA0F`, then
    `SmoothToGliderCamera`. `EnterGlider` (0x180AF95A0) has **no direct callers** — it is
    invoked through the camera-controller table/vtable, i.e. from a logic-side camera-mode
    switch, not from a local move-state check.
32. **GliderCamera value file is not local** (HIGH negative): `represent/camera/GliderCamera.krl.txt`
    is not in the base paks (all 12 probe roots missed; `docs/camera/CONFIG_FILES.md:78-83`);
    the key vocabulary is recovered from the loader (`docs/camera/CONFIG_FILES.md:116-129`):
    floats `MaxDragSpeed, PitchSpeed, BeginTurnAngle, PitchMaxSpeed, RollMaxSpeed,
    RotationSpeed, CameraPitchSpeed, MaxObjectCamarePitchDifference, RollTurnOverAcceleration,
    RollTurnBackAcceleration, PitchAcceleration, InitPitch`; ranges `YawRange, PitchRange,
    ADRollRange, QERollRange, TrackBlurSampleStrength, TrackBlurSampleDist`; plus
    `SmoothTime, SprintCameraAngle`; `EnterGlider` row keys `TurnCameraYawToObjectYaw,
    InitCameraAngleEnable/Angle, InitCameraDistanceEnable/Distance, FallingRollRange,
    FallingRollTime, ControlType ∈ {Aircraft, Aircraft2, Falling}`.
33. **Settled descent law** (MED-HIGH): the BR/mode glider is best modelled as
    **AUTOFLY (move state 0xF) following a server/path track + nav-fly vehicle model 1152
    + GliderCamera**. It is *not* a physics law in the client: the client has no glide
    equation; it plays the path and the shared integrator, and the camera/mode is cosmetic.
    Whether skill 29021 uses ride 1152 (吊篮) or a kite (RideType 54/63 has
    `WJ_风筝001_滑翔.tani`) is UNPROVEN; both exist.
34. **Flying-mount glide (separate)** (HIGH): RideType 30 (`飞机马车`) has glide/ascend
    clips and `马下落速度=350`, `马下落加速度=10` in `ride_rush.txt` — a mount-specific
    descent law for flying mounts, unrelated to the BR parachute.

### D. Parachute

35. **`bOnParachuteFlag` is `KCharacter +0x214`** (HIGH). Evidence chain:
    - exe character-attribute descriptor table (base file `0xa10990`), entry at file
      `0xa11390`: name pointer VA `0x14084C708` (`bOnParachuteFlag` string), getter
      `0x140412DE0`; the getter disassembles to `mov eax,[rcx+0x214]; ret`. Type 7.
    - The old "no code xref" claim came from treating the string's **file offset**
      `0x0084B508` as an address; the pointer to the string exists in the data table and
      the code reads the field, not the name.
36. **`ON_PARACHUTE_FLAG` is script-op id 9** (HIGH): op-name table entry at file `0x9f4dc0`
    `{name VA 0x1407D64B8, id 9}` (see §A.9 for the neighbouring ids).
37. **Consumers of `[+0x214]`** (HIGH, full exe scan of `[reg+0x214]` accesses):
    - `KCharacter::Jump` `0x14031377D`: parachute set → **jump rejected** (same guard block
      as hang/on-tower: `[+0x20C]`/`[+0x210]`).
    - `KCharacter::ProcessDropSpeed` `0x140316E0D`: if **none** of
      `bIgnoreGravity [+0x208]`, `bHangFlag [+0x20C]`, `bOnTowerFlag [+0x210]`,
      `bOnParachuteFlag [+0x214]` is set → `Vz = r12d` (slope-projected value);
      if any is set → Vz is **left as-is** (no slope projection, no air-stop). This is the
      parachute/glide vertical law: *keep current Vz*.
    - `BirdFlyTo`-region `0x14030CCB9`: parachute must be **off** to enter bird fly
      (`[+0x20C]==0 && [+0x228]==0 && [+0x214]==0`), states 0x23/0x24/0x25 only.
    - Landing/roll check regions `0x14031B66B` and `0x140321074`: parachute/hang/tower
      gate the landing branch (with `GetWaterline`-named speed function 0x140312400 and
      move state 4/0x1C handling).
    - `0x14031C428` (skill-move start region `0x14031C4A0`), `0x14031D3C0` (SwimTo
      `0x14031D770`).
38. **Represent side** (HIGH): `bOnParachuteFlag` is registered as a represent character
    state attribute **id 0x25** (represent descriptor table file `0xe97190`, 24-byte
    `{id, name*, type, flags}`; neighbours `bBirdMove` 0x24, `bFlyFlag` 0x26, `bOnRide`
    0x27, `bPullRide` 0x28). `bOnSummit` id 0xD8, `dwMannedSpaceID` id 0xF4,
    `dwRideShape` 0xF8, `dwHorseRunType` 0xFC, `nVehicleTrack` 0x9C, `nVehicleNode` 0xA0.
    These ids double as the state-block offsets the represent code reads.
39. **`nFlyFlag` = `[char+0x1FC]`** (HIGH, same table, getter `0x140412BC0`), adjacent to
    the parachute flag; `bWaterFlyAbility` = `[+0x360]`; `nCurrentTrack` = `[+0x3B4]`.
40. **No parachute model/ride row found** (MED negative): the extracted ride/vehicle/
    mannedspace tables contain no `降落伞`/`伞`/`滑翔伞` model row; the parachute visual is
    presumably a skill VFX / item model. Open probe.

### E. Auto-fly / bird cross-links (already decoded, not redone)

41. `KCharacter::FlyTo` 0x140310B70 (state 0x20→0x1F), `EndFlyJump` 0x140310960
    (state 0x21→4/0xE), `BirdFlyTo` 0x14030C940 (states 0x24→0x23, and 0x25 in the
    region above), `ProcessAutoFly` 0x140316990, `ProcessNavAutoFly` 0x1403175B0,
    `PauseAutoFly` 0x14030?? (string `KCharacter::PauseAutoFly` file 0x82D270,
    `m_eMoveState == cmsOnAutoFly` assert), `OnPauseAutoFlyNotify` 0x7C6238.
    Script names: `nFlyFlag`, `FlyJump`, `EndFlyJump`, `BirdFlyTo` are all entries in the
    exe script function table (type 8): `FlyJump` getter 0x1403DD5C0, `EndFlyJump`
    0x1403DD450, `BirdFlyTo` 0x1403DC910.

---

## State machines

### 1. Mount (horse)

```
                 T / ProcessRideHorse(char,1)                T / ProcessRideHorse(char,0)
GROUND/ANY ── bOnHorse==0 ──► KPlayer::RideHorse(0x14036C210) ──► MOUNTED
MOUNTED   ── bOnHorse==1 ──► KPlayer::DownHorse(0x140365C60) ──► GROUND
   RideHorse: guard [+0x208]==0, [+0x160]==0; if bHoldHorse then require bOnHorse
              (pull-ride); else require !bOnHorse; precondition 0x140363AF0;
              apply 0x140363C40; bOnHorse:=1, bHoldHorse:=0
   DownHorse: require bOnHorse; piHorse from item container [+0xB980] (assert);
              vtable+0x108 check; unequip horse boxes 0x18..0x1B;
              bOnHorse:=0, bHoldHorse:=0
SPACE (Jump 0x140313680):
   if bOnHorse && jumpCount==0 → HORSE TRIPLE g+0x25F50+6*(school+jumpCount)
        Vxy=word0, Vz=word1 + [+0x34C], g=word2   (60/180(+add)/11 for school 0)
   if bOnHorse && jumpCount>=1 → reject
   if bOnHorse && jumpCount==1 → DownHorse() first (mid-air dismount), then normal jump
MOVEMENT (logic): run speed ← CharacterRideWalkSpeed 8 / CharacterRideRunSpeed 40 (尺/s);
   yaw turn ← CharacterRideYawTurnSpeed 0.003465 / reset 0.0023;
   vehicle-class yaw ← CharacterVehicleYawTurnSpeed 0.006283
REPRESENT: LoadRide(RideType) → pRide [+0x39F8]; ChangeRideState(...) flags;
   mount socket [+0x3A08]; KRLRideRush (run/jump/fly) → pRideRush [+0x3A00];
   animation: player_animation_adjust_rides_type_state[RideType][AniState] →
   AdjustAniID, clip from rides_animation.txt; 999 sentinel override when
   [singleton+0x26466] (config option 0x1803185E0)
```

### 2. Vehicle / manned space (载体)

```
logic/script (skill, mode script, campaign) ── dwMannedSpaceID (represent state id 0xF4)
   ──► KRLCharacter::UpdateMannedSpace (0x1804E4DD0, param = manned-space ID)
        ── if changed: create/bind via [char+0x4838] manager
        ── pMannedSpace := [char+0x3A98]
        ── KRLCharacter::MountMannedSpace (string 0x180CAF1B8)
             KRLLocalCharacter / KRLRemoteCharacter variants
   ──► KRLMannedSpace: LoadModel → BindCharacter (Socket "s_hs") → PlayAnimation
        state table from mannedspace.krl.txt (death/walk/run/back/... → anim IDs)
        LoadSkillBuffPart: buff → extra mesh + socket
        ShowParabola/UpdateParabola: aim/throw arc for siege vehicles
   ──► action bar context switch (ExtentDynamicAction / NpcAssistedDynamicAction /
        DynamicBattleRoyale); ExitAnimationID = 下载具人动画ID on dismount
Data classes: mannedspace = 神机车/摧城车 (campaign siege) — IDs 1-16, 37-51;
战象/飞艇 have no mannedspace row (skill/item driven; UNPROVEN binding)
```

### 3. Glider (滑翔翼 29021 / nav-fly)

```
server mode script (沙漠风暴/天原绝境_滑翔翼.lua, server-side) or auto-fly command
   ──► logic move state AUTOFLY 0xF
        KCharacter::ProcessNavAutoFly (0x1403175B0): path data [+0xC28],
        frame counter [+0xC08], start flag [+0x3C0]; else ProcessAutoFly (0x140316990)
        altitude via shared ProcessVerticalMove (0x140318C50) — no glide equation
   ──► represent: vehicle track/node (nVehicleTrack 0x9C / nVehicleNode 0xA0) and
        NavFlyVehicleRideID = 1152 → rides.txt RideType 1152
        (WJ_吊篮002_HD.mdl, IdleAniID 31157) — model follows the track
   ──► camera: EnterGlider (0x180AF95A0) via camera-controller table →
        LoadGliderParams (0x180AFA3B0) row from GliderCamera.krl.txt (CDN, not local)
        → SmoothToGliderCamera / UpdateGliderCameraYaw / ClampMouseForGliderCamera
        ControlType Aircraft | Aircraft2 | Falling
UNPROVEN: whether 29021 selects ride 1152 or a kite ride (54/63 have 滑翔 clips)
```

### 4. Parachute

```
server/script: ON_PARACHUTE_FLAG op id 9 → KCharacter::bOnParachuteFlag [+0x214] := 1
   (and represent state id 0x25)
WHILE SET:
   - KCharacter::Jump rejects (0x14031377D)
   - ProcessDropSpeed skips slope projection / air-stop: Vz kept (0x140316E0D)
   - BirdFlyTo entry rejected (0x14030CCB9)
   - landing/roll branches gated (0x14031B66B, 0x140321074)
   - skill-move start / SwimTo interactions (0x14031C428 / 0x14031D3C0)
CLEAR (flag := 0): normal fall/landing resumes; exact setter path is script op /
   server sync (no direct C++ setter found)
```

---

## Data tables & actors

All extracted into scratch via `tools/netcode/extract_pak_paths.py` (official PakV4
extractor); **15/15 candidates hit** — no repo files written:

| File (logical name) | Scratch path | Content |
|---|---|---|
| `Represent/rides/rides.txt` | `.../char3x/rides_out/rides.txt` (88 KB, 605 lines) | RideType → model/socket/scale/IdleAniID/body+mane mesh/material/color/footprint SFX/water flags. Horse actors: `NPC_source\Horse\模型\Horse_0x_*.mdl` |
| `Represent/rides/rides_animation.txt` | `.../rides_out/rides_animation.txt` (9.9 MB) | RideType → AnimationID → clip file (glide/ascend/hot-air/basket/kite clips) |
| `Represent/rides/ride_rush.txt` | `.../rides_out/ride_rush.txt` (224 KB) | Mount skill (骑马技能ID, 4098 for flying carriage), fade/mount/jump/fall/phase-2 animations, `马下落速度/加速度` |
| `Represent/rides/ride_link.txt` | `.../rides_out/ride_link.txt` (738 KB) | Double/group ride (共骑): roles, offsets, mount/invite animations, link sockets |
| `Represent/rides/rides_road.txt` | `.../rides_out/rides_road.txt` | Road route data |
| `Represent/rides/mannedspace.krl.txt` | `.../rides_out/mannedspace.krl.txt` (134 KB, 3118 lines) | 载具 ID → Name/MDL per faction/socket `s_hs`/SkillBuffParts/Transform state→anim |
| `Represent/rides/vehicle.krl.txt` | `.../rides_out/vehicle.krl.txt` (43 KB) | Road vehicle routes `RideType:VehicleType` → `{modelID, skillID, doodadID}` |
| `Represent/rides/vehicle_model.krl.txt` | `.../rides_out/vehicle_model.krl.txt` (48 KB) | Vehicle models + `/gm player.AutoFly(...)` dev comments |
| `Represent/rides/rides_animation_adjust.krl.txt` | `.../rides_out/rides_animation_adjust.krl.txt` | Per-ride animation adjust (mostly commented) |
| `Represent/rides/pull_ride_config.krl.txt` | `.../rides_out/pull_ride_config.krl.txt` | Horse-rope (牵马) parameters |
| `Represent/rides/special_buff_rides.txt` | `.../rides_out/special_buff_rides.txt` | Buffs that force rides (220/221/256/252/1288…) |
| `Represent/rides/rides_freaky.krl.txt` | `.../rides_out/rides_freaky.krl.txt` | Transforming rides (balloon/carriage) model+anim swaps |
| `Represent/player/player_summit.txt` | `.../rides_out/player_summit.txt` (46 KB) | Summit (踩尖) per body type, kite weapon `WJ_风筝003`, 踩尖 animations |
| `Represent/player/player_ride_link_animation.txt` | `.../rides_out/player_ride_link_animation.txt` | Player link animations |
| `Represent/doodad/doodad_summit.krl.txt` | `.../rides_out/doodad_summit.krl.txt` | Doodad summit params |

Already extracted elsewhere and reused:
- `Represent/player/player_animation_adjust_rides_type_state.txt` —
  `.../SeasunDownloaderV2.4/jx3-web-map-viewer/cache-extraction/pakv4-probe/player-animation-out/Represent/player/`
  (RideType × 64 AniState → AdjustAniID; 3592 lines).
- `player_ride_animation_force_reshape.txt` (same dir) — RideType hash → `.ani` paths
  (`M2Horse...`, `M2马...`).
- `proof/gravity/number.krl.txt` (CommonNumber; ride/vehicle/summit/nav-fly values),
  `proof/gravity/JumpParam.tab` (horse triple), `proof/gravity/filepath.ini` (logical names).

Actors / clip families:
- Horses: `data\source\NPC_source\Horse\模型\Horse_*.mdl`, clips via
  `rides_animation.txt` / `player_animation_adjust_rides_type_state.txt`.
- Mounted player: `player_ride_link_animation.txt`, `ride_rush.txt` player columns
  (`人物骑马动作`, `人物骑马一段跳`, `下马动作`), `M2飞机马车主控*`, `M2热气球*`.
- Vehicles: `WJ_js弩车001` (神机车), 摧城车 MDLs, `s_hs` bind socket.
- Nav-fly: `data\source\doodad\万花谷\WJ_吊篮002_HD.mdl` (RideType 1152).
- Kites: `WJ_风筝001/002/006/007` (RideType 54/63/1218/1219) with `_滑翔.tani`.

---

## Symbols & RVAs

All VAs are image-base-relative for the listed binary. String VAs were computed from the
committed dumps (file offset + section delta) and verified by pointer-scan; function
addresses are the enclosing function of the assert string.

### JX3ClientX64.exe (logic)

| Symbol | VA / ref | Evidence |
|---|---|---|
| `KPlayer::RideHorse` | fn `0x14036C210` | assert VA 0x14083A918, disasm |
| `KPlayer::DownHorse` | fn `0x140365C60` | assert VA 0x14083A930, disasm |
| `ProcessRideHorse` | fn `0x1403AFB80` | assert VA 0x140842E18, disasm |
| `KCharacter::Jump` (horse/dismount/parachute) | `0x140313680` | `proof/gravity/disasm/kcharacter_jump.txt` + new disasm |
| `KCharacter::ProcessDropSpeed` (parachute gate) | `0x140316BE0` | `[+0x214]` at 0x140316E0D |
| `BirdFlyTo` region (parachute gate) | `0x14030C940` | `[+0x214]` at 0x14030CCB9 |
| `KCharacter::ProcessNavAutoFly` | `0x1403175B0` | assert VA 0x14082F238 |
| `KCharacter::ProcessAutoFly` | `0x140316990` | docs + callers |
| `KCharacter::FlyTo` / `EndFlyJump` | `0x140310B70` / `0x140310960` | gravity research §3.8 |
| `KCharacter::GetWaterline` (assert owner) | `0x140312440` | `proof/gravity/disasm/get_waterline.txt` |
| run-speed compute `nRunSpeed = f(base,percent)` | `0x14018297F` | writes `[+0x2FC]`, clamps 0x7F / `[+0x294]` |
| `bOnParachuteFlag` getter | `0x140412DE0` (`[+0x214]`) | attr table entry file 0xa11390 |
| `bOnHorse` / `bHoldHorse` getters | `0x1403E1B70` / `0x1403E1B90` | `[+0x1E774]` / `[+0x1E77C]` |
| `nHorseSprintPower*` getters | `0x1403E2440/80/C0/500` | `[+0x20198/+0x201A8/+0x201B0/+0x201A0]` |
| `nRunSpeed`/`Base`/`Percent`, `nMinRunSpeed` | `0x140412CA0/CB0/CC0`, `0x1404076E0` | `[+0x2FC/+0x300/+0x304/+0x294]` |
| attr descriptor table base | file `0xa10990` (VA `0x140A12590`) | 1350 entries decoded |
| script-op name table (movement attrs) | file `0x9f4d40` (VA `0x1409F6940`) | `ON_PARACHUTE_FLAG`=9, `HORSE_JUMP_SPEED_ADDITIONAL`=0x12 |
| `HORSE_JUMP_SPEED_ADDITIONAL` name | VA `0x1407D65E0` | file 0x7d53e0 |
| `ON_PARACHUTE_FLAG` name | VA `0x1407D64B8` | file 0x7d52b8 |
| `bOnParachuteFlag` name | VA `0x14084C708` | file 0x84b508 |
| `KCharacter::PauseAutoFly` / `ProcessPauseAutoFly` | names file 0x82D270/0x82D2B0 | asserts `m_eMoveState == cmsOnAutoFly` |

### JX3RepresentX64.dll

| Symbol | VA / ref |
|---|---|
| `KRLCharacter::UpdateMannedSpace` | fn ~`0x1804E4DD0` (assert VA 0x180CAE808, ref 0x1804E4E01) |
| `KRLCharacter::MountRide` / `ChangeRideState` | refs 0x1804E4FC6 / 0x1804E5064 (block 0x1804E4F60) |
| `KRLCharacter::LoadMannedSpace` / `MountMannedSpace` | string VAs 0x180CAFEA0 / 0x180CAFFB8 |
| `KRLLocalCharacter::MountMannedSpace` | string VA 0x180CB4040 |
| `KRLMannedSpace::*` | string VAs 0x180CB4640…0x180CB4CA8 (`BindCharacter` 0x180CB4738, `UpdateParabola` 0x180CB4B68) |
| `KRLCharacter::LoadRide` / `MountRide` / `OfflineMountRide` | 0x180CACBB0 / 0x180CAE078 / 0x180CAE098 |
| `KRLRideRush::{Jump,BeginJump,EndJump,UpdateFlyJumpAnimation,UpdateSkillMoveAnimation}` | 0x180CAA490/4A8/4C8/598/680 |
| `KRLRushState::UpdateRideJumpAnimation` | 0x180CAB3E8 |
| `RLState::GetRideStateAnimationID` | 0x180CA9B58 (fn ~0x18049F6D0) |
| `KRLRide::LuaGetTurnInputYaw` | fn `0x180469630` (yaw getter 0x18001E5B0) |
| `KRLRide::{BindCharacter,UnbindCharacter,IsBindCharacter}` | 0x180CB6C20 / 0x180CB6C40 / 0x180CB6228 |
| `LoadGliderParams` | called at 0x180AF99CE; string VA 0x180D0CA68 |
| `EnterGlider` | fn `0x180AF95A0` (no direct callers — table/vtable) |
| `SmoothToGliderCamera` / `UpdateGliderCameraYaw` / `ClampMouseForGliderCamera` | refs 0x180AFA7E2 / 0x180AFB2BF / 0x180B20235 |
| glider controller enter core | `0x18000BA0F`, table `[singleton+0x1A0]`, refs 0x180B1C9E9–0x180B1CB17 |
| represent state attr table | file `0xe97190` (24-byte entries; `bOnParachuteFlag` id 0x25 at file 0xe97208) |
| `sLoadNumberFromFile` / CommonNumber accessor | `0x180857DF0` / `0x180338BF0` (NavFlyVehicleRideID `+0x248`) |

---

## Corrections to existing docs

1. **`JX3_GRAVITY_RESEARCH.md` §3.11 / `REBORN_JUMP_FALL_SPEC.md` item 21**
   ("`bOnParachuteFlag` has no code xref") — **wrong**. It is `KCharacter+0x214`, set by
   script-op `ON_PARACHUTE_FLAG` id 9, and read by at least six code sites (Jump, drop
   speed, bird-fly gate, two landing checks, skill-move/swim). The string-address mix-up
   is the root cause (dumps print file offsets).
2. **String-dump columns are file offsets, not VAs.** VA = image base + RVA; deltas:
   exe `.rdata` +0x1200, exe `.data` +0x1c00, represent `.rdata` +0x1e00. Any doc quoting
   e.g. `0x0084B508` as an address should be recomputed.
3. **`JX3_GRAVITY_RESEARCH.md` §3.7 waterline attribution** — `[char+0x2FC]` is proven
   `nRunSpeed` (attr getter `0x140412CA0`; computed at `0x14018297F` as
   `(percent+0x400)*base >> 10`, clamped to `0x7F` and `nMinRunSpeed [+0x294]`). The
   function at `0x140312400` (6·x/20 / 6·x·11/112) is therefore a **speed** function (its
   callers use the result ×16 as an XY velocity threshold at 0x14031B69D / 0x1403210A6),
   and the `KCharacter::GetWaterline` assert actually belongs to `0x140312440` (the
   cell-top/submersion helper). Re-verify the waterline numbers; do not cite +0x2FC as
   height.
4. **`JX3_MODE_JUEJING_LOGIC.md:25`** calls skill 29021 `滑翔伞`; the client table
   (`mode_kit_report.txt`) says `天原绝境_滑翔翼` (`沙漠风暴/天原绝境_滑翔翼.lua`).
5. **`REBORN_JUMP_FALL_SPEC.md` item 22 / gravity §3.5** — mounted jump now fully
   specified: triple `60/180/11`, `+[0x34C]` bonus, `jumpCount>=1` reject, and the
   `jumpCount==1` → `DownHorse` mid-air dismount call.
6. **`CONTROL_MODES_P5_ANIM.md` §2 (999 sentinel)** — grounded: the override is the ride
   animation system; the per-RideType adjust table is extracted
   (`player_animation_adjust_rides_type_state.txt`) and `rides_animation.txt` supplies the
   clips. The `[singleton+0x26466]` gate is a config-option bool (`0x1803185E0`).
7. **`CONTROL_MODES_TRACEABILITY.md` A18 / `JX3_CHARACTER_MOVEMENT_RESEARCH.md` open item
   6** — `GetTurnInputYaw` = `KRLRide::LuaGetTurnInputYaw` (`0x180469630`, yaw getter
   `0x18001E5B0`); `CharacterVehicleYawTurnSpeed` is `CommonNumber+0x70` (value
   `0.006283`), still no located consumer (register-indirect read via the accessor).
8. **`JX3_COLLISION_SYSTEM.md` G-11** stays [SOLVED]; no new coupling to mounts found.
   `MannedSpace=Represent/rides/mannedspace.krl.txt` is now extracted (神机车/摧城车).
9. **`JX3_MODE_MATCH_LIFECYCLE.md` stage 4** — the descent is not a bespoke glider law:
   it is AUTOFLY (0xF) path-following + nav-fly vehicle ride 1152 + GliderCamera; the
   "descent law UNPROVEN" can be downgraded to "path-driven (no client glide equation)".

---

## Open questions + next probes

1. **`HORSE_JUMP_SPEED_ADDITIONAL` setter** (`[+0x34C]`). The op table has no code xref
   (`0x1409F6940` scan = 0). Probe: scan `.data`/`.rdata` for an 8-byte pointer to the op
   table (registration descriptor), or find a parallel id→offset table; alternatively look
   for a generic `mov [reg+off], edx` executor with `off` loaded from data.
2. **Ride speed / yaw consumers.** `CommonNumber+0x60/0x64/0x68/0x6C/0x70` are read
   through the singleton accessor `0x180338BF0`; trace its callers and register-flow (not
   RIP-relative). Until then the host should use the table values directly.
3. **War elephant binding.** No `战象` row in mannedspace. Probe represent state
   `dwMannedSpaceID` (id 0xF4) writers and the buffs granted by skills 29146/29264/29267;
   check `KRLMannedSpace::LoadSkillBuffPart` callers for a mode-specific ID.
4. **飞艇 (airship).** Only a loot special so far. Probe the item/doodad template of
   飞艇 and any skill that spawns it; check `rides.txt`/`vehicle.krl.txt` for its model.
5. **Parachute visual/model.** No 伞 row in the extracted tables. Probe skill 29021's
   animation/SFX and `player_suspend.krl.txt` 滞空 animation sets; check whether the
   parachute uses an item model (like 风筝) attached to a weapon socket.
6. **Glider camera values.** `GliderCamera.krl.txt` is not in the base paks; fetch via the
   CDN mini-update path (`bin64\KGPK4_StreamDownloader.zhcn_hd.conf`) or capture at
   runtime. Key list is already decoded.
7. **EnterGlider trigger.** `EnterGlider` has no direct callers; find the camera-controller
   table slot that holds it and the logic `SwitchCameraMode` value that selects it
   (`KGameWorldHandler::SwitchCameraMode` is Lua-facing).
8. **Mounted animation mapping.** `player_animation_adjust_rides_type_state.txt` keys on
   `AniState` names; find the represent enum that produces those names
   (`RLState::GetRideStateAnimationID`) and the RideType source (`dwRideShape` id 0xF8 /
   `dwHorseRunType` id 0xFC).
9. **`KRLRideRush` per-frame update.** Disassemble the full function set
   (0x1804AB7B0…0x1804AD000) to decode the mount jump/glide phase timing
   (`ride_rush` fade times 1000/3000 ms are the data side).

---

## Host implementation notes (dependency order)

1. **Mount state core.** Add `bOnHorse` / `bHoldHorse` (and the four horse equip boxes
   0x18–0x1B) to the character model; implement T as
   `ProcessRideHorse(char, !mounted)`, with the same guards (no mount while
   `[+0x208]`/`[+0x160]`). Mounted flag drives speed selection and the horse jump branch.
2. **Mounted movement.** Use `CharacterRideWalkSpeed=8` / `CharacterRideRunSpeed=40`
   尺/s (÷64 units) for walk/run; `CharacterRideYawTurnSpeed=0.003465`,
   `CharacterRideYawResetSpeed=0.0023`, `CharacterVehicleYawTurnSpeed=0.006283` for yaw.
3. **Horse jump.** On jump while mounted and `jumpCount==0`: `Vxy=60`, `Vz=180+[+0x34C]`,
   `g=11`; reject `jumpCount>=1`; second jump press (`jumpCount==1`) dismounts then jumps.
4. **Mounted animation.** Load `rides.txt` (RideType→model/socket/IdleAniID) and
   `rides_animation.txt` (clip list); apply
   `player_animation_adjust_rides_type_state.txt[RideType][state]` for per-state clip
   overrides; expose `dwRideShape`/`dwHorseRunType`; honor the 999 sentinel as
   "mounted locomotion row".
5. **Ride links (double ride).** `ride_link.txt` gives roles/offsets/animations per
   RideType; bind the follower to the host's link socket (`KRLRideLink`/`KRLGroupRideLink`
   semantics). `pull_ride_config.krl.txt` for the rope (牵马) state.
6. **Manned spaces.** Load `mannedspace.krl.txt`; bind the player to `s_hs`; per-faction
   model selection; play the `Transform` state→anim table; `SkillBuffParts` for
   skill/buff attachments; `ShowParabola` for aim arcs. War elephant/airship are skill/item
   driven — implement the generic `MountMannedSpace(id)` hook first.
7. **Glider.** Implement AUTOFLY (move state 0xF) with a path/track follower (use the
   shared vertical integrator for altitude), attach nav-fly vehicle ride 1152
   (`WJ_吊篮002_HD.mdl`) or a script-specified ride, and a GliderCamera mode using the
   decoded key list (ControlType Aircraft/Aircraft2/Falling). Skill 29021 is the entry
   trigger (server-side script; the host must script it locally).
8. **Parachute.** Add `bOnParachuteFlag` (+0x214): while set, reject jump and skip the
   drop-speed projection/air-stop so Vz is preserved (controlled descent); clear on
   landing; mirror to the represent state (id 0x25) for fall animation.
9. **Conveyors / carriers.** SIMWorld `SetConveyorBeltParam` with
   `physic_conveyor_belt_param.krl.txt` values (already decoded, G-11) for moving
   platforms; character binding inherits platform motion.

---

## Evidence index (scratch)

- `.../char3x/xref_attr_table.txt` — attr table xrefs/registration
- `.../char3x/exe_attr_offsets_full.txt` — 1350 attr descriptors → field offsets
- `.../char3x/exe_field_hits.txt` — every `[reg+0x34C/0x214/0x1FC/0x1E774/0x1E77C]` access
- `.../char3x/exe_field_hits2.txt` — every `[reg+0x2FC/0x300/0x304]` access
- `.../char3x/xref_ridehorse.txt`, `xref_downhorse.txt`, `xref_processride.txt`
- `.../char3x/xref_navautofly.txt`, `dis_dropspeed_para.txt`, `dis_jump_para.txt`,
  `dis_navfly_vehicle.txt`, `dis_para_31b6.txt`, `dis_para_3210.txt`
- `.../char3x/xrefs_rep.txt` — represent ride/mannedspace/glider string→function refs
- `.../char3x/dis_mannedspace.txt`, `dis_ridestate.txt`, `dis_riderush.txt`,
  `dis_ridestateanim.txt`, `dis_ridejumpanim.txt`, `dis_turnyaw2.txt`,
  `dis_glider_entry.txt`, `callers_loadglider.txt`
- `.../char3x/rides_out/` (15 extracted tables), `rides_utf8/`, `ride_tables_summary*.txt`,
  `gbk_term_hits.txt`, `skill29021.txt`, `jumpparam_horse_vals.txt`
- Scanners: `scan_ptrs.py`, `hexdump_off.py`, `decode_exe_attrs.py`,
  `decode_rep_table.py`, `multi_xref.py`, `scan_field_access.py`

---

## Host implementation - phase 1 horse (2026-10-06, `agent/3x-mount`)

Wired in `client/MountSystem.cs` + `client/RebornClient.cs` (feature build
`reborn_client_3x_mount.exe`, title `sandbox-3x_mount`):

- **State**: `T` (`RIDEHORSE`, default hotkey 84) toggles mount/dismount with the
  decoded guards mapped to host state (grounded + not sitting; inventory preconditions
  are not modeled - deviation 2).
- **Movement**: mounted walk/run = `CharacterRideWalkSpeed` 8 / `CharacterRideRunSpeed`
  40 u/logic-frame -> **120 / 600 u/s** (`proof/gravity/number.krl.txt`).
- **Horse jump**: the decoded triple **60/180/11** (uniform `jumpScale` with the normal
  jump; `[+0x34C]` script bonus = 0), reject while mounted airborne with `jumpCount>=1`,
  and the **midair second press dismounts first** (`DownHorse`) then applies the normal
  jump rules - all observed in the proof run.
- **Actors**: horse dummy (RideType 0 `Horse_01_01a_00.mdl`, `rides.txt`) follows the
  player (same-name `AddDummyModel` re-place per frame; handle stays stable, logged);
  rider plays `ride_rush` 人物骑马动作 `f1bqg_horse_run.ani` (+ `f1H小跳a.ani` airborne).
- **Horse gait - engine's own mapping** (correction to the first pass):
  `player_animation_adjust_rides_type_state.txt` RideType 0 -> `Idle 10030`,
  `RunForward 10016`, `BeginJumpOnce 10204`, resolved via `rides_animation.txt` to
  `H普通待机01.tani` / `H奔跑01.tani` / `H小跳a.ani`. The `ride_rush` columns [13]-[15]
  are **fade-in/stop hints**, not the steady gait.
- **AV boundary (host)**: `H加速奔跑01.tani` (the ride_rush fade class) **AVs the
  MovieEditor host** when played on the horse dummy - reproduced stationary (runs
  `reborn_20261006_193436/193525`); the adjust-table steady gait does not. Re-open when
  the fade phases are implemented (crash is in the engine render path; a dump exists
  under `%LOCALAPPDATA%\CrashDumps\`).
- **Deviations** (registered, re-open criteria in the `MountSystem.cs` header):
  rider not socket-bound (s_hs; re-open with Agent A's socket shim); no horse
  inventory; 999-sentinel PlayerRush row not consumed; ride-yaw consumer unlocated.
- **Proof**: `proof/character/mount/run_20261006_193655.txt` (mount -> run -> horse
  jump -> midair dismount -> remount -> dismount, exit DONE) + `mounted_run.png` /
  `mount_jump.png` / `mounted_idle.png` + `image_stats_20261006.txt` (1280x720,
  per-region RGB; region means `#AA9178` / `#AA9076` / `#91755C`).
- **Reproduce**: `RC_STARTUP=nodb`, `RC_MOUNT_TEST=1`, `RC_AUTORUN=12500`
  (+ `RC_SHOTS=4200,5200,7000,9200,10400`), cwd `C:\SeasunGame\MovieEditor`.

### Seat fix + camera collapse (2026-10-06, bug report on `reborn_client_3x.exe`)

Report: after `T`, the camera zoomed in and the rider rendered UNDER the horse.
Root cause (reproduced on the integration build): the rider model was placed at the
player's physics position while the riding clips (`f1bqg_horse_run.ani`) are authored
relative to the horse's saddle bind point - with the model at ground the head-bone
camera anchor sat at ~991 (inside the horse; terrain/scene probes collapsed the camera
to ~63 u). Fix: place the rider model at the horse's own **`b_hs` bone** (the s_hs
socket parent; resolved via the Agent A shim `FindBoneActor`/`ActorBoneMatrix`, idx 0
in the child part, saddle 174 u above the horse root) and compose the camera anchor
from the MODEL placement instead of the physics position. Verified in
`proof/character/mount/run_20261006_220215_seatfix.txt`: anchor 991 -> 1169 while
mounted, no obstruction pulls (`obst=0` throughout, camera r ~1200 stable), full
mount -> run -> jump -> midair dismount -> remount -> dismount green; screenshots
`seated_run.png` / `seated_jump_dismount.png` / `seated_idle_remount.png`
(`image_stats_seatfix_20261006.txt`).
