# JX3 gravity system — static research notes

**Branch:** `feature/real-map-collision`
**Date:** 2026-09-22
**Primary binaries:** `JX3ClientX64.exe` (logic), `JX3RepresentX64.dll` (display),
`SIMWorldX64.dll` (PhysX wrapper)
**Method:** full string extraction + targeted disassembly + client-table
extraction via `PakV4SfxExtract` (same pipeline as the camera/skill research).

Question driving this: *what is the real gravity/jump/fall physics of the JX3
character, and can we replay it exactly?*

**Answer:** yes — the client predicts movement locally, so the whole rule set is
in the binary. **Velocities are integer units per logic frame** (66.7 ms tick),
velocities are clamped to 127 u/f (XY) and ±2047 u/f (Z), gravity is a
per-frame integer from `JumpParam.tab` (`Gravity[jumpCount]`), and the fall /
sprint limits come from `settings/Sprint.tab`. Single jump: **g = 12.89 m/s²,
v0 = 7.03 m/s, apex 1.92 m**. No runtime capture is needed.

---

## 1. Where gravity lives (architecture)

| Layer | Component | Evidence |
|---|---|---|
| Physics wrapper | `SIMWorldX64.dll` `PxWorld::SetGravity(float)` (RVA `0x33660`, export `?SetGravity@PxWorld@@QEAAXM@Z`) | PE exports; imported by `JX3RepresentX64.dll` (IAT `0x18109a928`) |
| World | `CreateSIMWorld` / `DestroySIMWorld`; `PxWorld::GetFloorHeight`, `RayCast`, `AddForce`, `SetVelocity` | exports of `SIMWorldX64.dll` (image base `0x180000000`) |
| Represent display | `JX3RepresentX64.dll` — `KRLRushState::UpdateJumpAnimation` (`0x00CAA110`), `KRLCharacterFrameData::UpdateJumpOffsetY` (`0x00CCC860`), `GetSingleJumpEndOffset` / `GetDoubleJumpEndOffset` (`0x00CCC898`/`0x00CCC928`) | full string dump `proof/gravity/JX3RepresentX64_strings.txt` |
| Logic (jump authoring) | `JX3ClientX64.exe` — `KGJumpList::LoadJumpPrarm` (`0x1403A25F0`), `KCharacter::Jump` (`0x140313680`), `KCharacter::JumpTo` (`0x140313D90`), `KPlayerClient::DoCharacterJump` (`0x14012B370`), `KCharacter::ModifySprintEndSpeed` (`0x1403114C2`) | string dump + disasm |
| Tables | `settings/JumpParam.tab`, `settings/JumpFrameParam.tab`, `settings/Sprint.tab`, `settings/SkillMove.tab`, `settings/ParkourMove.tab` (extracted, `proof/gravity/`) | loaders `KGJumpList::Load*` |
| Script control | `GRAVITY_BASE`, `GRAVITY_PERCENT`, `JUMP_SPEED_BASE`, `JUMP_SPEED_PERCENT` (`0x007D5308`…), `IGNORE_GRAVITY` (`0x007DA660`), `LuaReloadJumpParam` (`0x007F8148`), `LuaEnableIgnoreGravity` (`0x007FD9A0`), `bIgnoreGravity` (`0x0084CF30`) | strings in `JX3ClientX64.exe` |

Gravity is **not one constant**: the world has `PxWorld::SetGravity` for rigid
bodies, while the player's airborne motion is authored per jump-count in the
tables and can be modified per-character at runtime by Lua scripts
(`nGravityBase` / `nGravityPercent`, `nJumpSpeedBase` / `nJumpSpeedPercent`
struct fields at `0x0084B428`…`0x0084B468`) — this is how 轻功 buffs/skills
change gravity.

## 2. Client → server jump flow (verified)

`KPlayerClient::DoCharacterJump` (disasm `proof/gravity/disasm/do_character_jump.txt`)
builds a small packet (word type `0xA`, direction byte, face direction,
`nJumpSpeed`, jump count, position) and sends it via `0x140169840`. Movement is
server-authoritative: the server replies with move syncs
(`KPlayerClient::OnSyncMoveParam` / `OnSyncMoveState` / `OnSyncMoveCtrl`), and
the client predicts the arc locally with `PrefetchJumpEndCharacterPosition` /
`GetJumpEndGameLoop` (landing prediction used by other players/NPC placement).

`KCharacter::Jump` (`0x140313680`) is the state-machine gate: checks move state
(`m_eMoveState`), weapon/school (jump-count chain up to `MaxJumpCount`),
direction deltas, then calls into `KGSO3WorldClientInterface::Jump`.

## 3. Verified client prediction model (read from the binary)

The client predicts locally, so the exact rules are in the code — read from
`KCharacter::JumpTo` (`0x140313D90`, disasm
`proof/gravity/disasm/kcharacter_jump.txt`), the flash/位移 move
(`0x1403DBDB0`, `proof/gravity/disasm/so3world_jump.txt`) and
`KCharacter::ModifySprintEndSpeed` (`0x1403114C2`, disasm
`proof/gravity/disasm/modify_sprint_end_speed.txt`).

**Velocities are integer units per logic frame** (not per second):

| Character field | Meaning | Clamp in code |
|---|---|---|
| `+0x2F8` | VelocityXY, units/frame | `[0, 0x7F]` = **0..127 u/f** |
| `+0x270` | VelocityZ, units/frame | `[-0x800, 0x7FF]` = **-2048..2047 u/f** |
| `+0x268` | VelocityXY ×16 (1/16 fixed point) | `[0, 0x7FF]` |
| `+0x320` | **Gravity**, units/frame² | used in the arc compensation |
| `+0x330` | current jump count (1…24) | |
| `+0xC08` | jump/air frame counter | |

Evidence:

- `JumpTo` computes per-frame velocity as `v = distance / frames` (`idiv`),
  then solves the discrete arc `Δz = vz·t + ½·g·t²` for the initial velocity:
  `vz = (targetZ - z)/frames + (gravity*frames)/2`
  (`0x140313F4C`–`0x140313F60`), and finally clamps XY to 127 and Z to
  `[-2048, 2047]` (`0x140313F88`–`0x140313FDB`).
- The flash move does the same `distance/frames` with the same
  `[0,127]` / `[-2048,2047]` clamps (`0x1403DC0C3`–`0x1403DC0EB`).
- `KCharacter::Jump` (`0x140313B20`) reads the per-jump triple from
  `g_pSO3World + 0x23810 + 6*(24*school+jump)` (takeoff: `JumpSpeedXY`,
  `VelocityZ`), stores `+0x268 = XY<<4`, `+0x2F8 = XY`, `+0x270 = Z`,
  `+0x320 = Gravity`, and **clamps gravity to [0, 0x1F]**
  (`0x140313BD5` >0x1F→0x1F, `0x140313BEA` <0→0). For wall jumps it reads the
  `Wall*` triple instead (`g_pSO3World + 0x25C50 + 6*(4*school+jump)`).
- `KCharacter::ModifySprintEndSpeed` (`0x1403140A0`) and its frame-update twin
  `0x1403114C2` apply the `…End` triple of the jump row that was just used
  (address `g_pSO3World + 0x24A0A + 6*(24*school+jumpCount)` = `…End` array
  row `jumpCount−1`, base `+0x24A10`) with the same clamps (plus gravity
  `[0, 0x1F]` in `0x1403140A0`). See §3.3 for the full phase model.

Together: `KGJumpList = g_pSO3World + 0x23810`; main triples at `+0`,
`…End` triples at `+0x1200`, Wall at `+0x2440`, MaxJumpCount at `+0x2400`,
Flash* arrays at `+0x2A40…+0x2B00` (all confirmed by `KGJumpList::LoadJumpPrarm`
store offsets and the consumer reads).


**Fall integration**: each logic frame `Vz -= Gravity; y += Vz` with
`Gravity = JumpParam.Gravity[jumpCount]` (11/20/30/…), velocities clamped as
above. See §6 for the fall state and landing rules.

### 3.1 Single-jump calibration (all schools)

| Quantity | Table value | Derived |
|---|---|---|
| Logic tick | 11-frame jump animation = **0.733 s** (FBX `跳跃1`, see `proof/compare/MAPVIEWER_FBX_CLIPS.md`) | **66.7 ms/tick (15 ticks/s)** |
| Length unit | skill dash 蹑云逐月 = 10 frames × 80 units = **800 units = 12.5 尺** (cross-check with `docs/netcode/SKILL_DATA_RESEARCH.md` §6) | **1 m = 192 units** (1 尺 = 64) |
| Jump speed | `VelocityZ0 = 90` u/frame | **7.03 m/s** |
| Gravity | `Gravity0 = 11` u/frame² | **12.89 m/s²** |
| Apex | v²/2g | **1.92 m** (discrete integration: 2.17 m) |
| Air time | 2v/g | **1.09 s**; rise 0.55 s |

### 3.2 Per-frame update functions (verified from disassembly)

The movement update is split into three functions; all are integer math.

**`KCharacter::ProcessAcceleration` (`0x1403165D0`)** — per-frame velocity
integration (disasm `proof/gravity/disasm/process_acceleration.txt`):

- reads gravity `[char+0x320]` (`0x1403166D1`, `0x1403167F3`) and projects it
  through the current terrain cell's packed slope angle (sin/cos lookups via
  `0x14020EE10` from `(cell>>1)&7`, `(cell>>4)&7`);
- adds the input acceleration (`r12d`) and applies **`Vz += accel`**
  (`0x140316912`);
- clamps `Vz` to `[-2048, 2047]` (`0x14031694A`–`0x140316965`);
- recomputes heading `[char+0x26C]` from `(Vxy_fixed, Vz)` via
  `atan2` (`0x14020ED10`, `0x1403168DB`);
- the `Vz += accel` step is skipped when `[char+0x208] != 0`
  (`0x1403168E0`).

**`KCharacter::ProcessVerticalMove` (`0x140318B80` wrapper / body `0x140318C50`)**
— position integration (disasm `proof/gravity/disasm/process_vertical_move.txt`):

- **`y += Vz`** — `add dword ptr [rbx+0x18], edi` with `edi = [rbx+0x270]`
  (`0x140318E70`);
- y clamp: `if y > r15d then y = r15d` (`0x140318E73`–`0x140318E7D`), where
  `r15d = max(word[cell+4]<<6, (word[cell+6]<<6) − scaled[+0x16C])`
  (`0x140318DDA`–`0x140318E00`) — i.e. the ground height or the cell top minus
  the scaled jump modifier; cell heights are stored in 尺 (×64 to units);
- **landing resets the jump chain**: when move state == 4 and vertical motion,
  if `y - cellTop <= 64 units (1 尺)` then `[+0x330] = 0` and `[+0x338] = 0`
  (`0x14031A25E`–`0x14031A27E`, `r15d` zeroed at `0x140319F79`);
- ground snap for move states 6/7: `y = max(y, cellTop - scaled[+0x170])`
  (`0x14031A285`–`0x14031A2AA`), where `[+0x170]` is scaled by `[+0x40]/100`
  at entry (`0x140318C8E`–`0x140318CB9`, same `base*percent/100` pattern as
  `[+0x16C]`).

**`KCharacter::ProcessDropSpeed` (`0x140316BE0`)** — slope-projected drop
(disasm `proof/gravity/disasm/process_drop_speed.txt`):

- returns immediately when `Vz == 0` (`0x140316BEE`);
- reads the cell's packed slope; if the slope angle ≤ 8 or the move state is
  26–29 → **`Vz = 0`** (`0x140316C84`–`0x140316E8F`);
- otherwise rotates `(Vxy_fixed, Vz)` by the slope, updates
  `[+0x26C] = atan2(...)` and recomputes the speed vector;
- if the resulting speed < `0x18<<4` and `[char+0x260] == 0` → **`Vz = 0`**
  (air-stop) (`0x140316DCA`–`0x140316DED`);
- clamps `[+0x268]` to `[0, 0x7FF]` and `[+0x270]` to `[-2048, 2047]`
  (`0x140316E23`–`0x140316E65`).

These are the only per-frame vertical-integration sites; together with §3 they
complete the airborne law: takeoff/End triples set `(Vxy, Vz, Gravity)`, each
frame `ProcessAcceleration` applies gravity (slope-projected) and clamps,
`ProcessVerticalMove` applies `y += Vz` and the ground/landing rules,
`ProcessDropSpeed` handles sloped/air-stop drop projection.

### 3.3 Jump phase model (verified)

`KCharacter::ModifySprintEndSpeed` (`0x1403140A0`, disasm
`proof/gravity/disasm/modify_sprint_end_speed_callers.txt`) is the **segment-end
phase**: it applies the `…End` triple of the jump row that was just used

```
jumpCount = [char+0x330]                     ; must be 1..24
row       = jumpCount - 1                    ; same row as the takeoff read
addr      = g + 0x24A0A + 6*(24*school + jumpCount)
          = End-array row `row`              ; End array base = g + 0x24A10
xy   = word[addr]      -> [+0x268] = xy<<4   clamp [0, 0x7FF]
xy   = same word       -> [+0x2F8] = xy      clamp [0, 0x7F]
z    = word[addr+2]    -> [+0x270] = z       clamp [-0x800, 0x7FF]
g    = word[addr+4]    -> [+0x320] = g       clamp [0, 0x1F]
```

Both call sites (`0x140182874`, `0x14036317A`) guard it identically:

```
if (move_state == 4 (cmsOnJump) || move_state == 0x1A) && [char+0x1F8] == 0
   && [char+0x330] >= 1:
       ModifySprintEndSpeed(char)
       [char+0xC08] = 0        ; jump/air frame counter
       [char+0x330] = 1        ; jump count reset to 1
```

So the full phase model is: **press → takeoff triple of the current row;
segment end → that row's `…End` triple + `jumpCount := 1`; ground contact →
`jumpCount := 0`** (§3.2). A twin application site inside the frame update
(`0x1403114C2`, same reads, no gravity clamp) handles the second update path.
The two `ModifySprintEndSpeed` call sites sit in the per-frame player update
paths right after the skill-move/sprint-break finisher `0x14031C800`
(disasm `proof/gravity/disasm/modify_sprint_end_speed_callers.txt`).

### 3.4 Jump curve driver and `JumpFrameParam` application (verified)

Storage (from `KGJumpList::LoadJumpPrarmFrame`, disasm
`proof/gravity/disasm/load_jump_frame_param.txt`): four parallel word arrays in
`KGJumpList`, row = `doublePlayer + 2*(24*school + jumpCount)`, stride
**160 words (0x140 bytes) per keyframe row** (the assert bounds frames by
`MAX_TOTAL_FRAME`; 160 is the stride observed in code), plus a
`TotalFrame` word per row:

| Array | KGJumpList offset | g_pSO3World offset |
|---|---|---|
| `TotalFrame[row]` | `+0x2BC0` | `+0x263D0` |
| `VelocityXY[row][frame]` | `+0x37C0` | `+0x26FD0` |
| `VelocityZ[row][frame]` | `+0x7B7C0` | `+0x9EFD0` |
| `DirectionXY[row][frame]` | `+0xF37C0` | `+0x116FD0` |
| `Frame[row][frame]` | `+0x16B7C0` | `+0x18EFD0` |

`KGJumpList::LoadJumpPrarmFrame` asserts `nFrame < m_nTotalFrame[...]`,
`nJumpCount <= MAX_JUMP_COUNT`, `nSchoolID < MAX_PLAYER_SCHOOLID`,
`nDoublePlayer ∈ {0,1}`.

Two verified consumers:

- **Jump start driver** (`0x1403159F0` region): applies the takeoff triple
  (`VelocityXY<<4 → +0x268`, `VelocityXY → +0x2F8`, `VelocityZ → +0x270`), sets
  **`[char+0xC08] = TotalFrame[row]`** (`0x140315A62`), and adds
  `word[jumpList+0x2B80 + 2*school]` (the `SprintFlashEndRevive` column) to
  `[char+0x20194]`, clamped to `[0, char+0x2019C]` (`0x140315A81`–`0x140315AB5`).
- **Curve applier** (`0x14031AFD6`): with `row = jumpCount + 24*school`,
  `row_curve = row - 1` and keyframe index `idx = TotalFrame[row-1] - r8d`
  (`r8d` = current air frame), it returns when `idx < 0` or
  `Frame[row-1][idx] == bx`, then writes
  `VelocityXY[idx]<<4 → +0x268` (clamp `[0,0x7FF]`),
  `VelocityXY[idx] → +0x2F8` (clamp `[0,0x7F]`),
  `VelocityZ[idx] → +0x270` (clamp `[-0x800,0x7FF]`), and
  **adds `DirectionXY[idx]` to the heading** `[char+0x26C]` with byte wrap
  `[0,255]` (`0x14031B084`–`0x14031B0A5`).

So the per-frame authored curve (`JumpFrameParam.tab`) is applied as a
**velocity override plus a per-frame heading delta**, indexed by elapsed air
frames, with the same clamps as the ballistic path. `DirectionXY` is an angle
increment (byte, π/128 unit), not an absolute direction.

### 3.5 Jump variant selection (verified)

`KCharacter::Jump` (`0x140313680`, disasm `proof/gravity/disasm/kcharacter_jump.txt`)
selects the velocity source by character state:

| Condition (verified instructions) | Source used |
|---|---|
| `[char+0x1E774] != 0` (riding/mount flag) and `jumpCount < 1` | **Horse** triple, `g+0x25F50 + 6*(school+jumpCount)`, `Vz += [char+0x34C]` (`0x140313A48`–`0x140313A88`); `jumpCount >= 1` → reject |
| `[char+0x200] != 0` (wall-contact flag) and `jumpCount < 4` | **Wall** triple, `g+0x25C50 + 6*(4*school+jumpCount)` (`0x140313B7A`–`0x140313BBF`) |
| otherwise | main takeoff triple, `g+0x23810 + 6*(24*school+jumpCount)` (`0x140313B20`–`0x140313B38`) |

All three store the same fields (`+0x268 = XY<<4`, `+0x2F8 = XY`, `+0x270 = Z`,
`+0x320 = Gravity` clamped `[0,0x1F]`) and increment `[char+0x330]`
(capped at 0x10 in the normal path, `0x140313C3D`).

### 3.6 Jump-type (school) selection and other located consumers (verified)

- **Jump type / school selection** (`0x14030BA9A`): the character's jump-type
  `[char+0x138]` is validated with a weapon check —
  `mask = dword[g+0x26110 + [char+0x1EB2C]*4]` (the `WeaponMask` array,
  per weapon index), then `bt mask, [weapon+0x14]` (weapon type bit); if the
  bit is clear the jump type is reset to 0 (`0x14030BAB6`). So `SchoolID` in
  `JumpParam.tab` = the character's 门派 jump type, kept only for weapons the
  school uses, otherwise 0 (default row).
- **Skill-move application**: `KGJumpList::GetSkillMoveSetting` (`0x1403A2490`)
  has 8 call sites in the character update/skill-move paths
  (`0x140313756`, `0x140314E6C`, `0x1403153E7`, `0x140317AA3`, `0x14031919B`,
  `0x14031C4E6`, `0x14031C853`, `0x1403DEAB6`; disasm
  `proof/gravity/disasm/get_skill_move_setting_callers.txt`) — these consume
  the 918-row `SkillMove.tab` settings (per-frame velocity keyframes).
- **Angle encoding**: `DirectionXY` in `SkillMove.tab` / `JumpFrameParam.tab`
  is a **byte angle / delta**, π/128 per unit (verified decoder at
  `0x14037F7D0` × `0.024543693` = π/128; applied as a heading increment in
  §3.4). `SearchDirection = 32` in `Sprint.tab` = 32/128 π = 45°.
- **Knockback**: `KnockedBackFrame` / `KnockedBackSpeed` arrays
  (`g+0x26190` / `g+0x261D0`) have **no client-side readers** in any client
  DLL (scan of `JX3ClientX64.exe`, `JX3ClientX64Base.dll`, `KBaseX64.dll`,
  `JX3RepresentX64.dll`, `JX3LogicEditOperationX64.dll`) — the motion is
  server-driven. `KickRange` (`g+0x26210`) is read at `0x1403157C8`.
- **Character controller**: `PhysicsEngineX64.dll` contains
  `PhysicsController` / `PxCapsuleControllerDesc` / `PxCreateControllerManager`
  and links `PhysX3CharacterKinematic_x64.dll`; config key
  `bEnableCapsuleCollision` (adapter), semantic keys `capsules radius`,
  `capsules length` (SIMWorld), JSON collider keys `capsuleRadius`,
  `capsuleHalfHeight` (SceneManager), IK key `footAlignToSurfaceMaxSlopeAngle`
  (SIMWorld). Concrete `slopeLimit` / `stepOffset` values are not literals —
  they come from config/semantic at runtime (not yet extracted).

### 3.7 Water, sprint and world gravity (verified)

- **Waterline** (`KCharacter::GetWaterline` `0x140312400`): base = character
  height `[char+0x2FC]`; the function returns `(6·h)/20` = **0.30·h** for NPCs
  (ID bit 30 set; magic `0x66666667` + `sar edx,3` = /20 at `0x140312416`) and
  `(6·h)·11/112` ≈ **0.586·h** for players (magic `0x92492493`, add, `sar 4`
  at `0x140312429`). Verified by integer emulation of the exact code sequences.
  The companion function `0x140312440` returns the submersion
  against the cell water top: `cellTop = word[cell+6]<<6`,
  `ground = word[cell+4]<<6`; depth `= cellTop - max(ground, y)` when
  `y <= cellTop`, else 0 (above water).
- **Swim entry** (`KCharacter::SwimTo` `0x14031D770`): only valid from move
  states 6/7 (`0x14031D786`); validates the destination via the shared
  `0x1403105A0` pathing check; sets `[+0x174]=0`, `[+0x210]=0`, `[+0xC04]=0`,
  **`[+0x1F4] = 7` (SWIM)**, `[+0x204]=1`, then runs the swim step
  (`0x140327A80`). Swim speed = `CharacterSwimSpeed` (`number.krl.txt` = 20 尺/s).
- **Sprint actions**: `KCharacter::SprintDash` (`0x14031CC00`) writes Vz
  `[+0x270]` and sets **`[+0x1F4] = 0x17` (23, SPRINT_DASH)**; the finish path
  `0x14031C800` (asserts `SpecialSkillMove`/`SprintBreak`) is the one that
  precedes the `ModifySprintEndSpeed` phase call (§3.3).
- **World gravity** (`SIMWorldX64.dll`): `PxWorld::SetGravity(float)`
  (`0x180033660`) is a **forwarder** to the inner physics object
  (`[PxWorld+0xA8]`, vtable slot `0x60`). No caller exists inside SIMWorld, and
  no gravity literal exists in `PhysicsEngineX64.dll` (only
  `eDISABLE_GRAVITY`/vehicle strings); shipped map files
  (`*_landscapeinfo.json`, `entities/*.json`) contain no gravity value either.
  So the client-side PhysX world gravity is **not set from static client
  data** — it is runtime (server/editor) or PhysX default; the character's
  gravity is independent of it (table-driven, §3). For our host we may choose
  the world gravity freely without contradicting the client.

### 3.8 Scripted-move counter, fly states, parkour (verified)

- **`[char+0xC08]` = scripted-move frame counter (countdown).**
  Set from the move's frame count at start (`= TotalFrame[row]` on jump,
  §3.4; `= settings frame` on parkour, below), decremented each update
  (`dec [rbx+0xC08]` in `KCharacter::OnParkour` `0x1403147A4` and at
  `0x140314958`, `0x14031496E`, `0x140315150`, `0x14031770E`), incremented in
  the nav auto-fly path update (`0x140316BAC`), and reset to 0 on
  landing/state reset (`0x140316A16`, `0x14031A22D`, `0x140182879`,
  `0x14031161A`, `0x14036317F`). So "elapsed air frame" used by the curve
  applier (§3.4) counts down from the move's frame count.
- **Fly / 滞空 states**: `KCharacter::FlyTo` (`0x140310B70`) sets move state
  `[+0x1F4]` to **0x20** (`0x140310C9A`) then **0x1F** (`0x140310E0A`);
  `KCharacter::EndFlyJump` (`0x140310960`) requires state **0x21**, writes Vz
  `[+0x270]` (`0x140310AD6`) and transitions to **4 (JUMP)** or **0xE**
  (`0x1403109D8` / `0x140310A75`), then back to 0x21 (`0x140310B06`). Swim =
  6/7, sprint dash = 0x17, jump = 4.
- **Parkour (`ParkourMove.tab`)**: `KCharacter::OnParkour` (`0x140314510`)
  asserts `m_dwSkillMoveID <= MAX_PARKOUR_MOVE_ID`, fetches the settings via
  `GetParkourMoveSetting`, sets **move state 4 (JUMP)** (`0x140314687`) and the
  frame counter, applies the per-frame Vz from the setting
  (`[+0x270]`, clamped `0x7FF`, `0x14031475C`/`0x1403148FF`) and decrements the
  counter (`0x1403147A4`).
- **Wall hang / wall run**: no client-logic hang function exists; the wall
  stroll/hang is Represent-side (`KRLRushState::BeginStrollOnSlope`,
  `HasDragPoint`, `IsHangTurnDirection`, `UpdateFaceFootDistance`) driven by
  the script flag `bHangFlag` (`0x0084CF40`) and parkour/skill moves; the wall
  jump itself is §3.5.

### 3.9 Physics scene, world gravity and controller (verified)

`PhysicsEngineX64.dll` scene/controller creation (region `0x180018990`,
disasm `proof/gravity/disasm/physics_scene_setup.txt`):

- The scene descriptor fallback gravity vector is **(0, −9.81, 0)**
  (`movss xmm1, [0x180101888]` = `-9.81` stored into the vector at
  `[rsp+0x40..0x48]`, `0x180018B02`–`0x180018B26`) — i.e. the client physics
  world uses the **PhysX default gravity**; the game does not override it
  (consistent with §3.7: no caller/literal anywhere).
- Character-controller parameters are derived from the character scale
  `[rax]`: multipliers **0.01, 0.025, 0.2, 0.04** and constants **0.4**,
  plus flag words (`0x180018A11`–`0x180018A8F`); controller manager created via
  `PxCreateControllerManager(scene, 0)` (`0x180018C40`, import
  `PhysX3CharacterKinematic_x64.dll!PxCreateControllerManager`), stored at
  `[this+0x10]`.
- The **world is stepped at a fixed 20 ms (50 Hz)**: the tick function
  (`0x180018C60` region) accumulates `dt_ms / 1000.0` into `[this+0x34]` and
  when it exceeds the threshold `[this+0x30]` subtracts it and runs
  `vtable[0x318]` / `vtable[0x1B0]` with the 20 ms step (`0.02` constant,
  `0x180018CBE`). (Character movement itself runs on the 15 Hz logic tick,
  §3.)

### 3.10 Skill moves (`SkillMove.tab`) — start and per-frame application (verified)

- **Start** (`0x14031C4A0`): validates `SkillMoveID ∈ [1, 0x800]`
  (`0x14031C4CA`), fetches the row via `GetSkillMoveSetting` (`0x14031C4E6`),
  then `[char+0xC08] = TotalFrame(row[0])`, `[char+0x26C] = [char+0x44]`
  (heading := facing), `[+0x174]=0`, `[+0x2C8]=0`, `[+0x2AC] = SkillMoveID`,
  `[+0x2B0] = flag`, `[+0x210]=0`, `[+0xC04]=0`, `[+0x204]=1`, and sets move
  state **0x1A (SKILL_MOVE_SRC)** or **0x1B (SKILL_MOVE_DST)**; one branch
  sets `[+0xC04] = word[row+0x5A4] + now` (timer).
- **Per-frame update** (`0x140315390` region): `active = [char+0xC1C]`,
  `remaining = [char+0xC18]`, `idx = TotalFrame(row[0]) - remaining`; returns
  when `remaining <= 0` or the per-frame flag `byte[row + 0x504 + idx] == 0`.
  It then writes
  `VelocityXY = word[row + 2 + 2*idx]` scaled by the blend weight
  `[char+0x2B4]` (clamped 0..255, magic `/128` division) →
  `[+0x268] = XY<<4`, `[+0x2F8] = XY`;
  `VelocityZ = dword[row + 0x144 + 4*idx]` (absolute) or
  `dword[row+0x144] - dword[row+0x140] + Vz` (relative blend branch selected by
  `byte[row+0x5A9]`) → `[+0x270]`;
  **`[+0x26C] += word[row + 0x3C4 + 2*idx]`** (heading delta, byte wrap); then
  the standard clamps (`0x140315596`–`0x140315600`).
- So `SkillMove.tab` rows hold, past `TotalFrame`: `VelocityXY` words at
  `+2`, `VelocityZ` dwords at `+0x140/+0x144`, `DirectionXY` words at `+0x3C4`,
  a per-frame byte at `+0x504`, and flags at `+0x5A9/+0x5AB` — the same
  "override velocity + heading delta + clamps" pattern as the jump curves
  (§3.4), driven by the countdown counters `[+0xC18]` / `[+0xC08]`.

### 3.11 Bird and ragdoll (verified pointers)

- **Auto-fly**: `KCharacter::ProcessAutoFly` (`0x140316990`) follows a nav
  **path/track** (`pTrack`, `pCurrentNode`, `pEndNode` asserts): it resets
  `[char+0xC08] = 0` (`0x140316A16`) and increments it per node/frame
  (`inc [rbx+0xC08]`, `0x140316BAC`), then falls through to the normal
  drop/vertical processing (`ProcessDropSpeed`). So auto-flight is
  path-node-driven with the shared vertical integrator for altitude.
- **Bird fly**: `KCharacter::BirdFlyTo` (`0x14030C940`) requires a cell
  (`m_pCell` assert), reads move state, uses Vz `[char+0x270]` and sets move
  state **0x24 (36)** then **0x23 (35)** (`0x14030CA6C`, `0x14030CB8C`).
- **Ragdoll**: parameters come from `CommonNumber` (`number.krl.txt`):
  `RagdollTime = 5000` ms, `RagdollBlendWeight = 1.0`, read by
  `sLoadNumberFromFile` (`JX3RepresentX64.dll 0x180857DF0`) into the global
  config struct (`+0x22C` time, `+0x230` blend; neighbors `SkillTurningTime`
  `+0x228`, `0x1808591AB`–`0x1808591E3`). **Activation** is
  `0x1802F9540`: it gates on a component flag (`[entity+0x30] & 0x2000000`),
  records the current time at `[entity+0x74B8]`, and starts the ragdoll with
  `RagdollTime` (`movss xmm1, [cfg+0x22C]` → `call 0x1800236DC`,
  `0x1802F9586`–`0x1802F95C0`). The bodies are
  `represent/physic/physic_character_param.krl.txt` (11 rigid bodies per body
  type with sockets/radius/length, extracted). Simulation is the PhysX world
  (gravity `(0,−9.81,0)`, fixed 20 ms step, §3.9).
- **Pull / Repulsed** are script/state-driven: script ops `CALL_REPULSED`,
  `REPULSED_RATE` (field `nRepulsedRate`), client move states
  `pcmsOnPull` / `pcmsOnRepulsed`, Represent animation sets
  `RepulsedForward/Backward/Left/Right` (`0x00CD8918`–`0x00CD8958`); they move
  the character through the shared velocity fields (no separate gravity law).
- **Parachute / summit / manned space are Represent/script-side**:
  `bOnParachuteFlag` (`0x0084B508`) has **no code xref** in the client exe
  (data-descriptor flag, server/script-driven), same for the other
  `b*Flag` state names; the behaviour lives in the Represent layer
  (`KRLSummit::Update`/`UpdateTurning` `0x00CA88C8`/`0x00CA88E0`,
  `STKREPRESENT_EVENT_QINGGONG_SUMMIT`, `KRLMannedSpace::UpdateParabola`) with
  data in `player_summit.txt` (extracted) and camera keys
  (`GliderCamera`, `AirCombatCamera` in `filepath.ini`); `SummitDistance`,
  `SummitFadeTime`, `SummitAdjustY`, `SummitCamera*` in `number.krl.txt`.

### 3.12 Character attribute source — jump type, jump-speed & gravity modifiers (verified)

The character data apply path (`0x14015A11A` region, string refs include
`KPlayerClient::OnSyncNewPlayer` / body-bone display data; the values arrive in
the character display/attribute block `rdi`) writes:

```
[rbx+0x138] = byte [rdi+0x40]     ; jump type / school (0x14015A549)
[rbx+0x16C] = byte [rdi+0x41]     ; jump-speed modifier  (0x14015A553)
[rbx+0x170] = byte [rdi+0x42]     ; gravity modifier     (0x14015A55D)
[rbx+0x1E774] = (qword[rdi+0x7A] >> 61) & 1   ; riding/mount flag
[rbx+0x1E77C] = (qword[rdi+0x7A] >> 62) & 1
[rbx+0x684]   =  qword[rdi+0x7A] >> 63
[rbx+0x1E778] = byte [rdi+0x3F]
```

So the per-character jump/gravity modifiers (§3.6, `[+0x16C]`/`[+0x170]`
scaled by `[+0x40]/100` in `ProcessVerticalMove`) and the jump-type/school
(`[+0x138]`, later validated against `WeaponMask` at `0x14030BA9A`) are **set
from the server-synced character data block**, together with the mount flag —
this closes the buff/modifier source question (no local setter needed).

Correction on swim: `0x140327A80` (called by `SwimTo`) is a quest-mark update
on another object (`KQuestList` asserts), not the swim step; the swim motion
runs through the shared integrator with `CharacterSwimSpeed` (20 尺/s) and the
waterline helpers (§3.7).

## 4. The jump parameter table — `settings/JumpParam.tab`

Extracted: `proof/gravity/JumpParam.tab` (23 school rows × 183 cols, GBK TSV).
Loader: `KGJumpList::LoadJumpPrarm` reads `SchoolID, MaxJumpCount, WeaponMask`,
then per-jump `JumpSpeedXY%d, VelocityZ%d, Gravity%d` (+`End` variants), then
`Wall%d`, `Horse%d`, `Dash%d` and knockback/flash/fly-cost columns (disasm
`proof/gravity/disasm/load_jump_param.txt`; values are read as 16-bit words,
asserted `< 0xFFFF`).

Every school row (jump 0 = the plain 跳跃):

| School | MaxJump | J0 Vz | J0 G | J1 Vz | J1 G | J2 Vz | J2 G | J3 Vz | J3 G |
|---|---|---|---|---|---|---|---|---|---|
| 0 (default) | 4 | 90 | 11 | 300 | 20 | 400 | 20 | −250 | 8 |
| 1 | 5 | 90 | 11 | 180 | 12 | 15 | 3 | 780 | 30 |
| 9 | 6 | 120 | 11 | 1000 | 25 | 0 | 0 | 600 | 16 |
| 10 | 4 | 90 | 11 | 80 | 80 | 80 | 60 | −200 | 20 |

(`J0` = first jump, `J1` = 二段跳 press, `J2`/`J3` = further presses; negative
Vz = the downward dive segment.) School 0 tail columns: `KnockedBackFrame=16`,
`KnockedBackSpeed=800`, `KickRange=256`, `FlashJumpCount=−1`, `FlashFrame=16`,
`MaxFlashDistance=1600`, `MinFlashDistance=512`, `SprintFlashStartCost=960`,
fly costs 21–3000.

### Movement speeds — `Represent/common/number.krl.txt` (`CommonNumber`)

Extracted: `proof/gravity/number.krl.txt`. Loaded by `sLoadNumberFromFile`
(`0x180857DF0`) from the table logical name `CommonNumber`
(`Represent/common/number.krl.txt`, mapped in `Represent\filepath.ini` —
extracted `proof/gravity/filepath.ini`).

| Key | Value | Interpretation |
|---|---|---|
| `CharacterWalkSpeed` | 6 | 6 尺/s ≈ **2.0 m/s** |
| `CharacterRunSpeed` | 20 | **6.67 m/s** |
| `CharacterSwimSpeed` | 20 | 6.67 m/s |
| `CharacterRideWalkSpeed` | 8 | 2.67 m/s |
| `CharacterRideRunSpeed` | 40 | **13.3 m/s** |
| `CameraMaxDistance` | 1245 | 1245 units ≈ 6.5 m |

Units are 尺/s here (6 尺/s walk, 20 尺/s run are the known in-game speeds).

## 5. The per-frame jump arcs — `settings/JumpFrameParam.tab`

Extracted: `proof/gravity/JumpFrameParam.tab` (520 cols: `SchoolID, JumpCount,
TotalFrame, DoublePlayer` + 128× `(Frame, VelocityXY, VelocityZ, DirectionXY)`
keyframes; only schools 10/11 ship rows in this build).

`DirectionXY` is a direction **byte**: decoded as `radians = byte × π/128`
(verified: `JX3ClientX64.exe` `0x14037F7D0` multiplies the byte by the float
`0.024543693` = π/128 at `0x14083CC78`).

The `TotalFrame` values exactly match the shipped jump animations at 15
ticks/s: `跳跃1/2/3` = 0.733/0.667/0.800 s = 11/10/12 ticks (school 10 row
`JumpCount 0` has 11 frames). Curve shapes (school 10, see
`tools/gravity/parse_jump_tables.py --summary`):

- JumpCount 0 (single 跳跃): 11 frames, ballistic rise + fall.
- JumpCount 1–3 (轻功 chain): 51–81 frames (3.4–5.4 s) with the classic JX3
  shape — initial ballistic segment, **dive**, **launch**, a **hover plateau**
  (constant-height segment: `…4383 4383 4383…` = zero vertical velocity), and a
  landing hop.

These curves are the authored 轻功 arcs; their exact vertical scale relative to
the ballistic model is the only remaining table question (see §9 open items 1).

## 6. Falling — what happens when you fall

### 6.1 The fall is a move state, not free-fall physics

Airborne motion is one of the client move states
(`m_eMoveState == cmsOnJump` / `cmsOnFlyJump` / `cmsOnAutoFly`,
`m_eMoveState == cmsOnFlyJump` assert at `0x0082D7F0`). The client runs the
same per-frame gravity integration as the jump (above) until ground/water
contact, then plays the landing transition.

The Represent layer picks the **fall loop animation** in
`KRLRushState::UpdateRollAnimation` (`JX3RepresentX64.dll` `0x1804C19C0`,
disasm `proof/gravity/disasm/fall_animation.txt`) by building the name

```
Fall%s%sAnimation      ; format string 0x180CAAED0
  %s #1 = 'Floor' | 'Water'     (by the surface flag [rdi+0xBC])
  %s #2 = 'GoForward' | <other> (by the moving check 0x18001E097)
```

i.e. the four falling loops `FallFloorIdleAnimation`,
`FallFloorGoForwardAnimation`, `FallWaterIdleAnimation`,
`FallWaterGoForwardAnimation` looked up in `tabCGAni`.

### 6.2 Fall velocity caps — `settings/Sprint.tab`

`KGJumpList::LoadSprintTab` (`0x1403A4880`, disasm
`proof/gravity/disasm/load_sprint_tab.txt`) loads `settings/Sprint.tab` into
**16-bit word arrays** in `KGJumpList` (offsets `+0x1E39C0` MinVelocityXY,
`+0x1E3A00` MaxVelocityXY, `+0x1E3A40` MinVelocityZ, `+0x1E3A80` MaxVelocityZ)
and asserts:

| Constant | Value | Meaning |
|---|---|---|
| `MAX_VELOCITY_XY` | `0x7F` = 127 | hard clamp on XY velocity (u/frame) |
| `MAX_VELOCITY_Z` | `0x7FF` = 2047 | hard clamp on Z velocity (u/frame) |
| `nMinVelocityXY/Z >= 1` | | both mins are non-zero |

Extracted values (`proof/gravity/Sprint.tab`):

| School (spot) | MinVelXY | MaxVelXY | MinVelZ | **MaxVelZ** | AniFrame | SearchDir |
|---|---|---|---|---|---|---|
| 0 | 10 | 120 | 8 | **900** | 40 | 32 |
| 10 (丐帮) | 10 | 120 | 8 | 900 | 50 | 32 |
| 17 | 1 | 127 | 1 | **1000** | 105 | 32 |

In the verified units/frame: XY 0.78–9.38 m/s, Z 0.63–**70.3 m/s** (the 轻功
dive is deliberately extreme), under the hard code clamps of 127 / 2047 u/f.

### 6.3 Landing: roll, water, death

`KRLRushState::IsRoll` (`0x1804B65E0` region, disasm
`proof/gravity/disasm/fall_down_height.txt`) reads per-state fall thresholds
from `tabCGAni` (values live in
`Represent/player/player_suspend.krl.txt`, extracted
`proof/gravity/player_suspend.krl.txt`) and compares *height fallen*:

```
(height_now - ground_y)  > FallDownHeightFloor   AND
FallDownAdjustFloor      <= (start_y - ground_y)
    -> play roll landing
```

| Key | Value (typical) | Meaning |
|---|---|---|
| `FallDownHeightFloor` | **500 units (2.6 m)** | roll-on-landing min fall |
| `FallDownAdjustFloor` | 50 units | adjust threshold |
| `FallDownHeightWater` / `FallDownAdjustWater` | 500 (200) / 50 | water variant |

The same file also holds `FallFloorAnimation` / `FallFloorGoForwardAnimation` /
`FallWaterAnimation` / `FallWaterGoForwardAnimation` and the 滞空
`StayAnimaiton` / directional animation set, per 轻功 skill × body type.


Death from falling is a **move**, not damage math on the client:
`settings/SkillMove.tab` has `SkillMoveDeath=1` rows (25 of 918), the first
being SkillMoveID 2 (50 frames, `IgnoreGravity=0`) — the 摔死 crash move,
together with the `FALL_DEATH` entry in the Represent state vocabulary
(`0x00CBD050`). Which fall causes death is server-decided; the client just
plays the `FALL_DEATH`/death move.

### 6.4 Skills can change gravity and velocity per move

`settings/SkillMove.tab` (918 rows) gives every skill move
`IngoreGravity` (565 rows = 1, 353 = 0), `TotalFrame`, `SkillMoveOnlyFly`,
`SkillMoveDeath`, `SkillMoveEndButKeepVelocity`, `CanJump`, `CanBanMask`
and 250 per-frame `(VelocityXY, VelocityZ, DirectionXY)` keyframes.
`settings/ParkourMove.tab` (100 rows) does the same for wall-run/parkour moves
with `PointFrame1..4` anchors.

Runtime gravity overrides are exposed to scripts (`JX3ClientX64.exe` strings):
`GRAVITY_BASE`, `GRAVITY_PERCENT`, `JUMP_SPEED_BASE`, `JUMP_SPEED_PERCENT`,
`HORSE_JUMP_SPEED_ADDITIONAL`, `IGNORE_GRAVITY`,
`KScriptFuncList::LuaReloadJumpParam`, `LuaEnableIgnoreGravity`, and the
per-character fields `nGravity`, `nGravityBase`, `nGravityPercent`,
`nJumpSpeedBase`, `nJumpSpeedPercent`, `bIgnoreGravity` (`0x0084B428`…).
`KCharacter::ProcessAutoFly` / `ProcessNavAutoFly` / `PauseAutoFly` and the
parachute flag (`bOnParachuteFlag` `0x0084B508`, `ON_PARACHUTE_FLAG`
`0x007D52B8`) drive the 轻功 glide/auto-fly fall behaviour.

## 7. Evidence files

- `proof/gravity/filepath.ini` — full `Represent\filepath.ini` mapping (extracted)
- `proof/gravity/JumpParam.tab`, `JumpFrameParam.tab`, `Sprint.tab`,
  `SkillMove.tab`, `ParkourMove.tab` — extracted game tables
- `proof/gravity/number.krl.txt`, `physic_character_param.krl.txt`,
  `scene_init_param.txt`, `player_rush_skill.txt`, `skill_rush_state.txt`,
  `player_flyjump.krl.txt`, `player_suspend.krl.txt` — extracted Represent data
- `proof/gravity/JX3ClientX64_exe_strings.txt`,
  `JX3RepresentX64_strings.txt`, `SIMWorldX64_strings.txt`,
  `JX3ClientX64Base_strings.txt` — full string dumps
- `proof/gravity/disasm/` — `load_jump_param.txt`, `kcharacter_jump.txt`,
  `do_character_jump.txt`, `prefetch_jump_end.txt`, `get_jump_end_gameloop.txt`,
  `setgravity_callers.txt`, `update_jump_offset_y.txt`, `update_rush_jump.txt`,
  `load_sprint_tab.txt`, `fall_down_height.txt`, `fall_animation.txt`,
  `process_max_follow_velocity.txt`, `so3world_jump.txt`,
  `modify_sprint_end_speed.txt`, `jumplist_init.txt`,
  `process_acceleration.txt`, `process_vertical_move.txt`,
  `process_drop_speed.txt`, `load_jump_frame_param.txt`,
  `modify_sprint_end_speed_callers.txt`, `get_skill_move_setting_callers.txt`,
  `swim_to.txt`, `get_waterline.txt`, `sprint_dash.txt`, `on_parkour.txt`,
  `fly_to.txt`, `end_fly_jump.txt`, `physics_scene_setup.txt`, `bird_fly_to.txt`,
  `ragdoll.txt`
- `proof/gravity/verification.txt` — numeric verification of the model
  (`tools/gravity/verify_model.py`)
- `docs/REBORN_JUMP_FALL_SPEC.md` — implementation-ready jump/fall spec
- `tools/gravity/parse_jump_tables.py` — parser + calibrated replays
  (`--summary` covers jump, fall caps, death moves)

## 8. Open items

1. **轻功 chain frame driver** (§3.3/§3.4 solved the rules): the remaining
   detail is the exact counter semantics — `[char+0xC08]` is initialized to
   `TotalFrame[row]` at jump start and used as `r8d` in the curve applier
   (`idx = TotalFrame[row-1] - r8d`); its per-frame decrement site has not been
   traced. No live capture needed.
2. `PxWorld::SetGravity` callers: only IAT-thunk indirection found so far
   (thunk `0x180A9C6C0`, table slot `0x180016450`); the actual gravity value is
   likely scene-semantic driven (`PxWorld::Initialize(UKVTable@Semantic@)`
   reads keys into `.data` slots at `0x18005E410`+). Resolve by reading the
   scene semantic table in the map host.
3. Per-character runtime modifiers (`GRAVITY_BASE/PERCENT`, `JUMP_SPEED_BASE/PERCENT`,
   `HORSE_JUMP_SPEED_ADDITIONAL`): the **application** is decoded
   (`[+0x16C]`/`[+0x170]` × `[+0x40]/100` in `ProcessVerticalMove`); the
   setter was not located — the field-name strings have no code xref
   (data descriptor), consistent with server attribute sync. Defaults (100%)
   reproduce the base game.
4. Server-side fall-damage/death threshold (client only plays the
   `FALL_DEATH` move — see `REBORN_JUMP_FALL_SPEC.md` §9).
5. Per-field character-controller mapping (which of the scale-derived values
   0.01/0.025/0.2/0.04/0.4 is radius/height/contactOffset/slopeLimit/stepOffset)
   — the creating code and constants are decoded (§3.9); only the struct-field
   names are not individually labelled.
6. Not decoded subsystems (data + function pointers located, §3.6/§9 audit):
   swim integration (`SwimTo` 0x14031D770), parkour application, sprint
   dive/slide state machine, suspend/float/auto-fly/bird/parachute
   transitions, individual `SkillMove.tab` applications (8 call sites of
   `GetSkillMoveSetting` located).

For the implementation-ready summary see `docs/REBORN_JUMP_FALL_SPEC.md`;
numeric validation is `proof/gravity/verification.txt`
(`tools/gravity/verify_model.py`).


## 9. Applied to the map-host player sim

The MapSpike player (`engine_host_spike/MapSpike.cs`) uses the calibrated
real values (defaults, still overridable via `MAP_PLAYER_*`):

```
gravity = -1289 cm/s^2   (12.89 m/s^2, = 11 u/frame^2 at 15 Hz)
jumpV   = 703 cm/s       (7.03 m/s, apex 1.92 m)
walk    = 200 cm/s       (CharacterWalkSpeed = 6 尺/s)
run     = 667 cm/s       (CharacterRunSpeed  = 20 尺/s)
```

## 10. Findings index (status per subsystem)

**[OK]** decoded + reproducible · **[PARTIAL]** rule/data decoded, wiring open ·
**[SERVER]** server-authoritative · **[NA]** other subsystem.

| Subsystem | Status | Where |
|---|---|---|
| Single jump ballistics (takeoff triple, gravity, arc) | **[OK]** | §3, verified by `tools/gravity/verify_model.py` |
| Gravity/units/tick/velocity clamps (127 / ±2047 / gravity [0,31]) | **[OK]** | §3 |
| Per-frame integrator (`ProcessAcceleration`/`VerticalMove`/`DropSpeed`, slope, air-stop, landing reset) | **[OK]** | §3.2 |
| 轻功 chain phases (takeoff→End, `jumpCount := 1`, ground `:= 0`) | **[OK]** | §3.3 |
| `JumpFrameParam` curves (storage, consumer, heading delta) | **[OK]** | §3.4 |
| Jump variants: wall (`[+0x200]`), mounted (`[+0x1E774]`, `Vz += [+0x34C]`) | **[OK]** | §3.5 |
| School/row selection via `WeaponMask` | **[OK]** | §3.6 |
| Modifier source (server data → `[+0x138]`/`[+0x16C]`/`[+0x170]`) | **[OK]** | §3.12 |
| Waterline (0.30·h NPC / ≈0.586·h player) + submersion | **[OK]** | §3.7 |
| World physics gravity `(0,−9.81,0)` + 20 ms step + controller mgr | **[OK]** | §3.9 |
| Flash / blink math | **[OK]** | §3.1 |
| Skill moves (start + per-frame keyframes) | **[OK]** | §3.10 |
| Landing roll/water thresholds + animations | **[OK]** | §6.3 |
| Fall death move (`FALL_DEATH`), death threshold [SERVER] | **[OK]/[SERVER]** | §6.3 |
| Walk/run/swim/ride speeds | **[OK]** | §4 |
| Air/scripted counter `[+0xC08]` semantics | **[OK]** | §3.8 |
| Fly / 滞空 / bird state codes | **[PARTIAL]** | §3.8, §3.11 |
| Auto-fly (nav path/track) | **[PARTIAL]** | §3.11 |
| Parkour (`ParkourMove.tab`) | **[PARTIAL]** | §3.8 |
| Sprint dive/slide state machine | **[PARTIAL]** | §3.7 |
| Swim motion details (shared integrator + `CharacterSwimSpeed`) | **[PARTIAL]** | §3.7 |
| Ragdoll (config + activation + bodies + world) | **[PARTIAL]** | §3.11 |
| Parachute / Summit / MannedSpace / Pull-Repulsed | **[PARTIAL]** (Represent/script) | §3.11 |
| CCT per-field labels (constants + scene decoded) | **[PARTIAL]** | §3.9 |
| Knockback motion, move sync, fall damage | **[SERVER]** | §3.6, §6.3 |
| Missiles/trajectories (`KParabolaMissileProcessor`) | **[NA]** | §3.11 |
| Camera/SFX/blending | **[NA]** | separate specs |

