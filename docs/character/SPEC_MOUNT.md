# C — MOUNT system spec (JX3 1-5-0 zhcn_hd primary + 1-6-0 zhcn_exp delta)

**Area:** character · **Branch:** agent/3x-integration · **Index:** `docs/character/README.md` · **Status:** redefined spec 2026-10-06 (client-truth, 1-5-0 + 1-6-0 deltas) — supersedes the assumed behaviors in the older docs.

**Purpose.** Complete, evidence-based specification of the mount (horse) system for a host
implementation. Supersedes the claim list in `docs/character/3_6_MOUNTS_GLIDER.md` where
noted (§8). Vehicles / glider / parachute are summarized in §6 (they share the mount code
paths).

**Truth sources (read-only, no writes under `C:\SeasunGame`):**

| Tag | Install | Binary | Build |
|---|---|---|---|
| HD (primary) | `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64` | `JX3ClientX64.exe` (image base 0x140000000), `JX3RepresentX64.dll` (0x180000000) | 1-5-0 `.9975` |
| EXP (delta) | `C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64` | same names | 1-6-0-9536 (2026-10-03) |

**Method / scratch.** Disassembly with scratch `exp16\dump2.py` / `xall.py` / `fn_at.py` /
`find_calls.py` (capstone + `.pdata`), attribute-table decode `spec3x\find_attr_table.py`,
EXP PakV4 extraction with the official `PakV4SfxExtract.exe` (scratch mirror, junction to
`C:\SeasunGame\Game\JX3_EXP\Pakv4`), Lua bytecode disassembly `luac32 -l -l` on the shipped
UI chunk, all outputs under `C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x\`.
Reused earlier evidence: `exp16\1_movement.md` (1-6-0 RVAs, KGJumpList, field moves),
`exp16\4_hostbase.md`, `char3x\*` (HD-era mount recon + tables), `proof/gravity/*`.

**Confidence legend.** HIGH = assert-string xref + `.pdata` boundary + instruction check
(or byte-level data); MED = one leg inferred (boundary/pattern); LOW = name/context only;
UNPROVEN = stated negative or missing probe.

**Move-state naming.** `KCharacter+0x1F4` `m_eMoveState` (unchanged both builds): 4 = JUMP,
6/7 = swim, 8 = (skill-move special), 0x17 = sprint dash, 0x1F/0x20/0x21 = fly-jump,
0x23/0x24/0x25 = bird fly. UI-side names used by the shipped Lua (`MOVE_STATE`):
`ON_STAND`, `ON_WALK`, `ON_RUN`, `ON_JUMP`, `ON_FLOAT`, `ON_ENTRAP`, `ON_DEATH`.

---

## 1. Mount lifecycle

### 1.1 Symbols / RVAs (both builds)

| Symbol | HD 1-5-0 | EXP 1-6-0 | Evidence |
|---|---|---|---|
| `KCharacter::Jump` | **0x140313680** | **0x14033EAC0** | `xall_hd.txt`/`xall_exp.txt`; both fully disassembled |
| `KPlayer::RideHorse` | **0x14036C210** | **0x1403A2F20** | assert string xref; disasm `hd_ride_fns.txt`, `exp_ride_fns.txt` |
| `KPlayer::DownHorse` | **0x140365C60** | **0x14039C270** | same |
| `ProcessRideHorse` | **0x1403AFB80** | **0x1403EA300** | same |
| `KPlayer::ApplyAttribHorse` | **0x140363AF0** | **0x140399FA0** | assert xref; `hd_ride_pre.txt`, `exp_applyattrib.txt` |
| `KPlayer::ApplyAttribHorseEquip` | **0x140363C40** | **0x14039A0F0** | assert xref; disasm |
| ride command handler (packet) | **0x140156BD0** | **0x140151FC0** | disasm; body directly next to `OnSyncHorseFlag` |
| pull/hold-horse handler (unnamed) | 0x140367E50 | 0x14039E460 | `RideHorse`/`DownHorse` branch targets; name MED |
| `KPlayerClient::OnSyncHorseFlag` | 0x140156B70 | 0x140151F60 | assert xref |
| `KPlayerClient::OnSyncHorseExteriorData` | 0x140156AC0 | 0x140151EB0 | assert xref |
| `KPlayerClient::OnBroadcastPlayerChangeHorseRunType` | 0x14013CDF0 | 0x140136D80 | assert xref |
| `KPlayerClient::OnSyncMoveParam` (carries `bSprintFlag` bit) | **0x140158A30** | not re-derived | HD disasm `hd_158a30.txt` |
| `KCharacter::HostJump` (script/command face) | **0x1403DBB70** | **0x140416970** | assert xref; disasm |
| `KGSO3WorldClientInterface::Jump` (input/Lua face) | **0x14018BA30** | **0x140189440** | assert xref; disasm |
| `KPlayerClient::DoCharacterJump` (request packet) | **0x14012B370** | **0x140124180** | assert xref; disasm |
| run-speed compute (`nRunSpeed`) | fn 0x140182470, write @**0x14018297F** | not re-derived (same shape) | `dis_runspeed.txt` |

`g_pSO3World`: HD **0x140A755B8**, EXP **0x140AE5B68**. `KGJumpList` = `g+0x23810` (HD) /
`g+0x25F38` (EXP); horse triple array = `KGJumpList+0x2740` (= `g+0x25F50` / `g+0x28678`),
words `{Vxy, Vz, Gravity}` 16-bit each (HIGH; `new_jump2.txt`, `hd_jump_full.txt`).

### 1.2 State fields (both builds)

| Field | HD | EXP | Source |
|---|---|---|---|
| `bOnHorse` | **+0x1E774** | **+0x11A14** | attr getters 0x1403E1B70 / 0x140453030 (`attr_hd.txt`, `attr_exp.txt`) |
| `bHoldHorse` | **+0x1E77C** | **+0x11A1C** | getters 0x1403E1B90 / 0x1404527C0 |
| horse attribute ID (dword) | +0x1E778 | +0x11A18 | set by `ApplyAttribHorse`; packet handler |
| horse item container | +0xB980 | +0x13E0 | `DownHorse`/`ApplyAttribHorse` `lea`; `piHorse` assert |
| `bSprintFlag` | +0x1F8 | +0x1F8 | getters 0x140412BB0 / 0x140453730 |
| `bIgnoreGravity` | +0x208 | +0x208 | jump/drop guards |
| `bHangFlag` / `bOnTowerFlag` / `bOnParachuteFlag` | +0x20C / +0x210 / +0x214 | same | `dis_jump_para.txt`, getters (EXP 0x140453040 for parachute) |
| `nFlyFlag` | +0x1FC (dword) | +0x1FC (byte) | getter 0x140412BC0 / 0x1404525F0 |
| `nJumpCount` / `nMaxJumpCount` / `nJumpSpeed` | +0x330 / +0x334 / +0x340 | +0x33C / +0x340 / +0x34C | attr table (`nJumpCount`, `nMaxJumpCount`, `nJumpSpeed`) + Jump disasm |
| horse Vz bonus (`HORSE_JUMP_SPEED_ADDITIONAL`) | +0x34C | +0x358 | `add r14d,[...+0x34C]` / `add r12d,[rbx+0x358]` |
| horse sprint power (pool/max/cost/revive) | +0x20198 / +0x201A0 / +0x201A8 / +0x201B0 | +0x134F4 / +0x134FC / +0x13504 / +0x1350C | attr getters |
| `bFightState` (×5 jump-power cost gate) | +0x540 | +0x55C (MED) | HD getter 0x140413840; EXP Jump `cmp [rbx+0x55C]` |

### 1.3 Mount command paths

**Server/script face — `ProcessRideHorse(pDstCharacter, bRide)`** (HIGH, both builds):
rejects NULL and NPCs (`id & 0x40000000`) → `bRide ? RideHorse() : DownHorse()`, returns 1 on
success. `hd_ride_fns.txt` / `exp_ride_fns.txt`.

**Packet face — ride command handler** (HIGH, both builds; HD 0x140156BD0 / EXP 0x140151FC0).
Packet layout (byte offsets from packet base): `+7` character id (u32); `+0xB` horse
attribute id (u32); `+0x13` flags byte, **bit0 = bHoldHorse, bit1 = bOnHorse**.
- If the id is the local client player: `bit1` set → `bit0` set ? pull/hold handler : `RideHorse()`;
  `bit1` clear → `DownHorse()`.
- If remote: writes the flags/attribute directly into the character
  (`[+0x1E774]=bit1; [+0x1E778]=packet+0xB; [+0x1E77C]=bit0`). This is why remote mounts appear
  without any local guard: **the server decides, the client applies**.

### 1.4 `KPlayer::RideHorse` guards (HIGH, byte-for-byte parity both builds)

HD 0x14036C210 / EXP 0x1403A2F20:

1. fail if `bIgnoreGravity [+0x208] != 0`;
2. fail if `[+0x160] != 0` (unnamed internal gate; MED for meaning, HIGH for the check);
3. if `bHoldHorse != 0` → require `bOnHorse != 0`, else fail;
   if `bHoldHorse == 0` → require `bOnHorse == 0`, else fail (**already mounted → reject**);
4. `ApplyAttribHorse` (0x140363AF0 HD / 0x140399FA0 EXP) must succeed:
   - `GetEquippedHorse()` on the container (+0xB980 / +0x13E0) must return an item (`piHorse` assert);
   - if item `vt[0x108]` says no property: item `vt[8]` must return a horse property
     (`pHorseProperty` assert);
   - if `bSprintFlag != 0` and the property exists: property id `[prop+0x14]` must be set in the
     global allowed-horse mask (`g+0xD34` HD / `g+0xD3C` EXP; `bt ecx,edx; jae fail`) — MED;
   - equips the horse (`0x14032B170` HD); writes horse attribute ID to `[+0x1E778]` / `[+0x11A18]`;
5. on success sets `bOnHorse := 1`, `bHoldHorse := 0` **before** calling
   `ApplyAttribHorseEquip` (0x140363C40 HD / 0x14039A0F0 EXP), which loops the four horse-equip
   item boxes **0x18..0x1B**, `CanEquip` probe then equip each; returns 1. (If equip fails the
   flags stay set — replicate exactly.)

**What can block mounting:** NPC; `bIgnoreGravity`; `[+0x160]`; already mounted without hold;
no equipped horse item / invalid horse item; sprint-mode mask mismatch; equip failure.
These are the complete guard set of the function (no other branches exist).

**T action**: the client-side T/骑马 hotkey resolves to a skill (`骑马技能ID`, e.g. 4098 in
`ride_rush.txt`); the local host can map T → `ProcessRideHorse(char, !bOnHorse)` directly.

### 1.5 `KPlayer::DownHorse` (HIGH)

HD 0x140365C60 / EXP 0x14039C270:

1. fail if `bOnHorse == 0`;
2. `GetEquippedHorse()` must return the horse item; `vt[0x108]` must pass;
3. for boxes **0x18..0x1B**: get item; if valid, unequip (fails → error, return 0);
4. unequip the horse item itself (box 0 HD);
5. on success clear `bOnHorse=0`, `bHoldHorse=0`, return 1. **No airborne/state guard exists** —
   dismount works mid-air if called (only callers decide).

---

## 2. Mounted movement

### 2.1 Values and storage (HIGH)

`Represent/common/number.krl.txt` (4,459 B EXP, sha16 `95d6de8c7e058cea`; HD proof copy
4,311 B — the only delta is 6 appended `Ragdoll*` keys, `exp16/1_movement.md` A23):
`CharacterRideWalkSpeed=8`, `CharacterRideRunSpeed=40`, `CharacterRideYawTurnSpeed=0.003465`,
`CharacterRideYawResetSpeed=0.0023`, `CharacterVehicleYawTurnSpeed=0.006283`,
`CameraRideYawOffset=0`, `RideFootContactGroundDis=5`, `RideFootprintLife=5000`,
`RideFootprintApplyTimeInterval=300`.

CommonNumber field offsets re-derived from the EXP loader (`JX3RepresentX64.dll` fn
**0x18063B470**, key-string xrefs per store — `exp_numbers_loader.txt`; HD loader old doc
0x180857DF0): `CharacterYawTurnSpeed +0x58`, `CharacterYawTurnResetSpeed +0x5C`,
**`CharacterRideWalkSpeed +0x60`**, **`CharacterRideRunSpeed +0x64`**,
`CharacterRideYawTurnSpeed +0x68`, `CharacterRideYawResetSpeed +0x6C`,
`CharacterVehicleYawTurnSpeed +0x70`, `CameraSpeedRatio +0x74`, `CameraRideYawOffset +0x78`,
`RideFootContactGroundDis +0x1B4`, `RideFootprintApplyTimeInterval +0x1B8`,
`RideFootprintLife +0x1BC`, `NavFlyVehicleRideID +0x260` (u32).

### 2.2 Logic side — what actually moves the character

- HD character fields (HIGH from `dis_runspeed.txt` and attr table): `nRunSpeed +0x2FC`,
  run-speed base `+0x300` (`nRunSpeedBase`), percent `+0x304` (`nRunSpeedPercent`),
  min `+0x294`; compute at 0x14018297F = `((percent + 0x400) * base) >> 10`, clamp ≤ 0x7F and
  ≥ min. EXP moved this block +0xC: `nRunSpeed +0x308` (the `exp16/1_movement.md` label
  "height" for +0x308 is a misnomer — `char3x/3_6` correction #3 identifies this function as
  the speed function; `DoCharacterJump` EXP packets `[rbx+0x308]` in the run-speed slot).
- The value applied to `nRunSpeed` while mounted comes from the **server** (move params ops
  `RUN_SPEED_BASE`=1 / `MOVE_SPEED_PERCENT`=2 and `HORSE_RUN_TYPE`=11, op table evidence
  `char3x/3_6` A9), not from the `CharacterRide*` constants directly. The `CharacterRide*`
  numbers are the represent-side horse gait/footprint basis (loader above).
- Heading: `+0x26C` byte heading (`DirectionXY` = byte×π/128) written by `Jump` and by move
  records; turn-in-place while mounted is represent-driven (see §4).
- **Consumer sites of CommonNumber +0x60..+0x70 are still not located in either build**
  (register-indirect accessor, old open item stands; re-verified as still open). Host guidance:
  use the table values directly for the mounted gait (walk 8 / run 40 尺/s; if 1 尺 = 64 u then
  512 / 2560 u/s — unit convention warning: repo docs carry a 1 m = 192 u vs 1 u = 1 cm
  inconsistency, `1_movement.md` A25; keep the host's existing unit bridge and document it).

### 2.3 What changes vs unmounted

1. Walk/run speed source switches to the ride constants (8/40 vs 6/20) and yaw smoothing to the
   ride yaw rates (0.003465 / reset 0.0023 vs 0.007465 / 0.0023).
2. The Space input is re-routed through horse-specific logic (§3.1) — idle presses do not jump.
3. The jump takeoff selects the horse triple when the character is in the sprint/jump path
   (`bSprintFlag`), and the horse consumes sprint power (§3.2).
4. The actor swaps to the ride model + mounted animation set (§4, §5).
5. Camera gains the per-ride `CameraAdjust` offset from `rides.txt` (§4).

---

## 3. Mounted jump — COMPLETE rule

### 3.1 Input layer: the shipped Space handler (HIGH)

`proof/controls/ui_lua/hotkeys_script.lua` (shipped Lua 5.1 bytecode) function `hotkeys.lua
0/84`, lines 942-1044. Full listing: `spec3x\hotkeys_luac.txt` / `hotkeys_luac_ll.txt`
(constants verified). Decoded behavior of Space with `player.bOnHorse == true`:

```
horse = player:GetEquippedHorse(); idx = horse.dwIndex
if not (idx==38699 and bSprintFlag) then FireEvent("ON_ONHORSE_JUMP") end
if Table_GetSpecialHorseOnJump()[idx] exists:            -- special-horse table
    if nFlyFlag==0 and nMoveState==ON_RUN  -> OnUseSkill(13951, 2)
    if nFlyFlag==0 and nMoveState==ON_STAND-> OnUseSkill(13618, 9)
    if nFlyFlag==1 -> return
    if idx==38699 and bSprintFlag -> return
    if nMoveState==ON_JUMP -> return
    Camera_EnableControl(CONTROL_JUMP, true)             -- allow the engine jump
else if idx==13784:  (same shape: ON_RUN -> 13951, then jump-control enable)
else if idx==13148:  (nFlyFlag!=0 -> return; ON_STAND/backward -> OnUseSkill(13618,9); return)
else:                                                    -- ordinary horse
    if nFlyFlag!=0:
        OnUseSkill(44565, 6); Camera_EnableControl(CONTROL_JUMP, true)
    else if nMoveState in {ON_STAND, ON_FLOAT} or IsCharacterMoving(CONTROL_BACKWARD):
        OnUseSkill(13618, 9)                             -- ground action, NO jump
    else:
        OnUseSkill(44565, 6); Camera_EnableControl(CONTROL_JUMP, true)
```

`CONTROL_JUMP` is control id 9 (`docs/controls/JX3_MOVEMENT_CONTROLS.md`). Facts:

- **The mounted branch never consults `IsKeyDoubleDown`** (only the bird-dash branch does, at
  pc62; the unmounted fallthrough passes `false` to `ResponseDisplacementHotkey`).
- **Idle press (ON_STAND / ON_FLOAT / moving backward) casts skill 13618 and never enables the
  jump control** → no jump.
- **Moving press (ON_RUN/ON_WALK etc.) casts skill 44565 and enables the jump control**.
- Skill ids 13951 / 13618 / 44565 have **no player-visible row** in
  `ui/Scheme/Case/Skill.txt` (lookup run, `lookup_skill.py`; only 4098=骑乘, 9007=后撤,
  13148=隐刀 resolved) — their effects are server/hidden skill content; the host must reproduce
  the motion locally (below).

Unmounted contrast (same function): off-horse Space fires `ON_PLAYER_JUMP`, runs
`ResponseDisplacementHotkey("SPACE", true, IsKeyDoubleDown())` (dash targets), then either
`OnUseSkill(9007, 8)` + `Camera_LockControl(8)` (后撤 back-dash) or enables `CONTROL_JUMP`
(plus a `bSlideSprintFlag` gate).

### 3.2 Engine layer: `KCharacter::Jump` (HIGH, both builds disassembled)

Sources: `hd_jump_full.txt` (HD 0x140313680..0x140313D8B), `exp16\new_jump2.txt`
(EXP 0x14033EAC0..0x14033F4CE). Signature: `Jump(this, p1 /*esi*/, direction /*r8d*/, hostFlag)`;
`p1 != 0` means "use the current position as the takeoff base" (skips the client steering
computation). `HostJump` calls `Jump(char, p1, dir, 1)` for local prediction, then
`DoCharacterJump` sends `{direction, p1!=0 flag, gravity, nJumpSpeed, nRunSpeed, x,y,z, state}`
to the server (`hd_jumpcaller.txt`, `hd_docharjump_ridecmd.txt`; EXP same at 0x140416970 /
0x140124180).

Acceptance gates (both builds, addresses per build):

1. If `bSprintFlag != 0` (`[+0x1F8]`) then `[+0x29C]` must be **zero**, else reject
   (HD 0x140313701, EXP 0x14033EB3E).
2. Move state must be in {1..7, 0x1C(28)}; states 0x1A/0x1B additionally require the skill-move
   setting and `[+0x2CC]==0 && byte[+0x2F8]==0`; all other states reject.
3. Reject if `bHangFlag [+0x20C]` or `bOnParachuteFlag [+0x214]` set.
4. If `bIgnoreGravity [+0x208]` set, only allow inside a ±0x55 angle window around
   `[+0x25C]`/current heading.
5. NPC (`flags & 0x40000000`): skips the mounted/sprint branch (goes to wall/main selection).
6. Mounted + `nFlyFlag==0` + `p1 != 0` → reject (mounted flight/jump-command combination).
7. If `psize dwJumpType [+0x138] >= 0x20` → reject (jump-type table bound).

Then two mutually exclusive branches:

**A. `bSprintFlag != 0` — sprint / 轻功 / horse-sprint branch** (this is the ONLY branch that
selects table triples):
- sprint-power gate (MED): index `[+0x134]`; pool `nHorseSprintPower` must be > cost; cost is
  ×5 when `bFightState`; cost is not deducted when the shield flag `[+0x55C]`/`[+0x540]`(HD)
  is set; insufficient pool → reject;
- if mounted (`bOnHorse != 0`) and `nJumpCount == 1` → `DownHorse()` is called first
  (HD 0x140313A30, EXP 0x14033EE7C); if it succeeded the mount is dropped and the normal jump
  continues; if it failed, the mounted reject below applies;
- if mounted (`bOnHorse != 0`): require `nJumpCount < 1` else **reject**; row index =
  `dwJumpType + nJumpCount`; triple = `{word0 Vxy, word1 Vz, word2 Gravity}` at
  `g+0x25F50`/`g+0x28678 + 6*index`; `Vz += horseVzBonus` (`+0x34C`/`+0x358`);
- if not mounted: `[+0x200]` wall flag → wall triple (`+0x2440`), else main triple (`+0x0`),
  with jump-count caps `0x18` and per-jump-type cap from `g+0x25C10`/`g+0x28338`;
- fallthrough common stores (B below applies): state := 4.

`JumpParam.tab` row values (HIGH; `proof/gravity/JumpParam.tab`, `jumpparam_horse_vals.txt`;
EXP table byte-identical per `1_movement.md`): columns 159-161
`HorseJumpSpeedXY0=60`, `HorseVelocityZ0=180`, `HorseGravity0=11` for **every** shipped jump
type (school 0..22). So the reachable horse triple is always **Vxy 60, Vz 180 + bonus, g 11**
(u/frame @15 Hz; conversion note kept from the old doc).

**B. `bSprintFlag == 0` — ordinary/velocity branch** (HD 0x140313C22 / EXP 0x14033F201):
- if water depth ≥ speed threshold → state 5 (water/jump variant; state name MED); else
- `nJumpCount < nMaxJumpCount` and `< 0xF` → `nJumpCount++`, then state 4 with
  **Vxy = run-speed-derived** (`nRunSpeed`, halved for |turn delta| ≤ 0x50) and
  **Vz = nJumpSpeed** (`+0x340` HD / `+0x34C` EXP). No horse triple here.

**Common completion (both branches), HD addresses:** gravity clamp to `[0,0x1F]`
(0x140313BD5); apply move info to `[+0x2A0/+0x2A4/+0x2A8]`, `[+0x338]=0`, `[+0xC08]=jump-type
script`; `[+0x174] = (oldState==0x18)`; `call 0x140315E40` if `[+0x2CC]!=0`; **state := 4**;
`[+0x210]=0`, `[+0xC04]=0`; `[+0x204]=1`; `call 0x14036BCB0` (non-NPC); clamps
`Vz ∈ [-0x800,0x7FF]`, `Vxy ∈ [0,0x7F]`, `Vxy` stored `<<4` at `[+0x268]`, `[+0x2F8]`;
`[+0x26C] = heading`; `[+0x270] = Vz`; returns 1. EXP: same at 0x14033F425-0x14033F4C6
(gravity `[+0x32C]`, Vxy `[+0x300]`, fixed `[+0x268]`).

**Landing:** `ProcessVerticalMove` ground contact resets `nJumpCount` (`[+0x33C]=0` EXP
0x140347DF0 / HD `[+0x330]`) and the landing move state; **it does not touch `bOnHorse`** —
a mounted character stays mounted through jump/land cycles. HIGH (`1_movement.md` A9).

### 3.3 What is server/move-record driven

- `OnSyncMoveParam` (HD 0x140158A30) unpacks a periodic move record; **byte `+0x27` bit0 →
  `bSprintFlag`**, bit1 `bIgnoreGravity`, bit3 wall flag, and position/`[+0x2a0..]` fields
  (`hd_158a30.txt`). So which of the two `Jump` branches runs is **decided by the server bit**,
  and the host is the server.
- `DoCharacterJump` sends the client's jump request; the server echoes a jump move record which
  the host applies through `HostJump`→`Jump` (local) — for remote characters the host must run
  the same `Jump` locally from the record's fields.
- Therefore the host must: (a) gate the input per §3.1; (b) set the sprint bit per its own
  movement rules; (c) call `Jump` with the record's `p1`/direction; (d) never invent a horse
  triple outside the sprint branch.

**Verdicts on the user's statements** (all code-verified):

| Statement | Verdict | Evidence |
|---|---|---|
| idle press must NOT jump | **TRUE** (input layer): mounted ON_STAND/ON_FLOAT/backward → skill 13618, jump control not enabled | hotkeys Lua 942-1044 |
| moving jump is fine | **TRUE**: mounted moving → skill 44565 + `Camera_EnableControl(CONTROL_JUMP,true)` | same |
| a double press must NOT kill the mount state | **TRUE as stated**: the mounted branch ignores `IsKeyDoubleDown`; the only mid-air dismount is `Jump` sprint branch (`bSprintFlag!=0 && nJumpCount==1`), which ordinary play does not enter | same + `Jump` disasm |

The old doc's "second jump press dismounts" (item 6) is **not wrong code** but is **scoped to
the sprint branch**; do not implement it for a normal mounted double press.

---

## 4. Facing / seat

- **Ride actor table — `Represent/rides/rides.txt`** (EXP: 90,444 B / 628 lines, sha16
  `cbf01a51eea10225`; HD-era copy 88,234 B / 605 lines). Columns (header, HIGH):
  `RepresentID, RidesDetailType, CameraAdjust, TerrainSlopeType, 是否贴水, 水面时高度修正,
  禁用地表效果(水面水花), MainModelFile, ModelScale, SocketScale, SelectionScale, IdleAniID,
  GlobalColorChannel, ColorChannel, BodyMesh, BodyMaterial, …, ManeMesh, …, ChangeRepresent,
  HeadTopHorseAdjust, KeepCharacterScale, Faker, AdjustCharacterBalloonPos, FootprintSFX×4+scales`.
  Horse rows: `RideType 0` = `Horse_01_01a_00.mdl` (CameraAdjust=0, IdleAniID=0),
  `RideType 1-6` = `Horse_0x_*.mdl` (**CameraAdjust=80**, IdleAniID=10000, ModelScale=1,
  SocketScale=1, `KeepCharacterScale` set); riding-carriage 29/30 use `Horse.mdl` with body/head
  mesh+mtl parts; kite 54 CameraAdjust=0 IdleAniID=1316; nav-fly basket 1152 IdleAniID=31157.
  **EXP added ride ids 401-420 and 1321-1323; none removed** (diff, `states_dump.txt`).
- **Orientation.** The logic character owns position (`+0x10/+0x14/+0x18`), heading (`+0x26C`)
  and fixed XY (`+0x268`); the ride is a represent actor bound to the character
  (`KRLRide::BindCharacter`, `KRLCharacter::MountRide` / `ChangeRideState`). The horse model
  faces the travel direction; turn-in-place is animated (adjust states `TurnLeft/TurnRight`,
  `rides_animation.txt` column `锁定朝向` "lock facing" flags clips that must not rotate the
  actor). `CameraAdjust` (80 for ordinary horses) is the camera pitch/offset applied while
  mounted; `CameraRideYawOffset=0`.
- **Represent symbols** (EXP re-derived — the EXP represent DLL is a different build):
  `KRLCharacter::MountRide` 0x18026ACB0, `ChangeRideState` 0x180258050 (also inlined at
  0x18026ADB4..), `LoadRide` 0x180268510 / 0x180268640, `KRLRide::BindCharacter` 0x1802D8110 /
  0x1802D84E0, `KRLRideRush::Jump` 0x180230D80, `RLState::GetRideStateAnimationID` 0x180222F40 /
  0x1802233F0, `KRLRushState::UpdateRideJumpAnimation` 0x1802443D0 / 0x180245FD0
  (`xall_exp_rep.txt`). HD anchors (old doc, not re-derived): `pRide [+0x39F8]`,
  `pRideRush [+0x3A00]`, mount/camera socket `[+0x3A08]`, `MountRide` ref 0x1804E4FC6,
  `BindCharacter` 0x180CB6C20, `LuaGetTurnInputYaw` fn 0x180469630. **EXP pRide offsets were not
  re-derived** (open); the EXP `LuaGetTurnInputYaw` symbol string is **absent** (renamed/inlined
  — `xall_exp_rep.txt` MISSING).
- **Seat.** The character is attached by `KRLRide::BindCharacter`; the seat transform comes from
  the ride model/skeleton (`SocketScale` in `rides.txt`), not from a shipped socket-name column;
  no `s_ride`/`s_seat` string exists in the represent string table (negative scan). Double-ride
  offsets/angles are data in `ride_link.txt` (738,024 B, byte-identical HD→EXP). Seat-socket name
  = UNPROVEN; probe `KRLRide::BindCharacter` EXP 0x1802D84E0.

---

## 5. Animations

### 5.1 Tables and schemas

| Table | Size (EXP) | Role / columns |
|---|---|---|
| `Represent/rides/rides.txt` | 90,444 B | ride actor (model, scale, IdleAniID, CameraAdjust, footprint SFX) |
| `Represent/rides/rides_animation.txt` | 9,937,409 B | per RideType clip list: `RepresentID, AnimationID, IsLooping, IsPullRideAni, AnimationSpeed, SFXType, SFXSpeed, SFXScale, BoneName, SFXFile, AnimationFile, 锁定朝向, IsVehicleCanPlay` (HIGH) |
| `Represent/player/player_animation_adjust_rides_type_state.txt` | 88,875 B, 3,758 lines, sha16 `864f18217060f79e` | `RideType, Hash, AniState, AdjustAniID` — re-extracted from EXP (the old doc's mapviewer copy is gone); 135 RideTypes × exactly **64 AniState names** (list in `states_dump.txt`) |
| `Represent/rides/ride_rush.txt` | 224,631 B | mount motion/rush: 46 columns (header decoded, `mount_tables_dump.txt`): 骑马技能ID, 淡入距离/时间/到达不透明时间/淡入动作, 淡出距离/时间/角度/动作, 淡出停止一次动作, 淡出停止维持动作, 跑步动作, 跳跃动作, 待机, 人物准备/上马/骑马动作, 双人同骑骑马动作, 人物骑马一段跳, 停滑动作(人/马), 下马动作×3, 马下落动作/速度/加速度, 进入轻功动作(人/马), 淡出变速率, 淡出停止比例, 第二阶段进入/维持动作 |
| `ride_link.txt`, `mannedspace.krl.txt`, `vehicle*.krl.txt`, `pull_ride_config.krl.txt`, `rides_freaky.krl.txt`, `special_buff_rides.txt` | — | **byte-identical HD-era → EXP** (sha256 compare, `spec3x` vs `char3x`) |
| `rides.txt`, `rides_animation.txt`, `rides_road.txt` | — | changed in EXP (rides: +23 ride types; animation: +356 B; road: diff) |

`number.krl.txt` changed only by 6 appended `Ragdoll*` keys (ride values untouched).

### 5.2 State → clip mapping (HIGH)

- The represent animates from per-character state blocks; `RLState::GetRideStateAnimationID`
  (EXP 0x180222F40) is the ride override: when the state block's type `[rcx+0x18]==2`, it looks
  up the state on the unit holder and returns `AdjustAniID` from `[entry+0x38]` if non-zero,
  else the passed-in animation id (`exp_ridestateanim.txt`; assert lines 0x7D/0x80/0x86). HD
  equivalent old fn ~0x18049F6D0.
- Per-RideType adjust table: e.g. RideType 0 `Idle 10030, RunForward 10016, JumpingOnce 10205,
  BeginJumpOnce 10204, EndJumpIdleOnce 10206, Halt 10080`; RideType 1 uses 2xxxx family
  (`Idle 20030, WalkForward 20009, RunForward 20016, JumpingOnce 20205, TurnLeft 20202`);
  RideType 29 (flying carriage) Fly* = 50800-50804 (`states_dump.txt`).
- The shared locomotion lookup is overridden for mounts: EXP mount-override wrapper
  **0x180640FC0** forces school index **0x3E7 (999)** when the config gate
  `[0x180C114C8+0x2F0D6]` is set (`exp16/3_character.md`; HD wrapper 0x18085CE60) — this is the
  "999 sentinel" row in `player_rush.txt` (school 999 rows exist).
- Horse clips: `rides_animation.txt` RideType 0 lists idle `H普通待机01.tani`, plus per-ride
  clips; rider clips come from `ride_rush.txt` player columns (`M2bqg_horse_run.ani`,
  `M2H_m2_f2_jump_a.ani`, `M2bqg_horse_down*.tani`, `M2bqg_horse_jump_to_horse*.tani`, …).

### 5.3 Fade / steady-gait rules (data, HIGH)

`ride_rush.txt` horse row (RideType 0, role types 1/2/5/6): 骑马技能ID=4098; fade-in distance
1000, time 3000 ms, time-to-opaque 1100, fade-in action `H加速奔跑01.tani`; fade-out distance
15000, time 10000 ms, angle 15°, action `H加速奔跑01.tani`; fade-out stop once
`H加速奔跑停止01.tani`; fade-out stop steady `H普通待机01.ani`; run `H加速奔跑01.tani`;
jump `H小跳a.ani`; idle `H普通待机01a.ani`; 停滑(人) `M2bqg_horse_run_end.ani`; 停滑(马)
`H加速奔跑停止01.tani`; 下马 `M2bqg_horse_down.tani/02/03`; 马下落 `H加速奔跑01.tani`,
下落速度 **350**, 下落加速度 **10**; 进入轻功 `M2bqg_horse_run_start.ani` (人) /
`H加速奔跑开始01.ani` (马); 淡出变速率 **0.3**, 淡出停止比例 **0.1**; 第二阶段进入 `H小跳a.ani`,
维持 `H普通待机01a.ani`(马) / `M2H_m2_f2_jump_a.ani`(人). RideType 29/30 share 骑马技能ID=4098
and the glide/ascend clips (`WJ_飞机马车001_滑翔_01.tani`, `_上升_01.tani`) with the same
350/10 descent pair. Interpretation of 马下落速度/加速度: horse drop parameters (the old doc's
"flying-mount descent law" reading is plausible but not proven to be used by the vertical
integrator — MED).

---

## 6. Vehicles / glider / parachute (concise; they share the mount paths)

- **Parachute** (HIGH, unchanged in EXP): `bOnParachuteFlag KCharacter+0x214` (EXP getter
  0x140453040 returns `[rcx+0x214]`), script op `ON_PARACHUTE_FLAG`=9. While set: `Jump` rejects
  (guard #3), `ProcessDropSpeed` keeps current `Vz` (skips slope projection/air-stop), `BirdFlyTo`
  entry rejected, landing/roll branches gated (`char3x/3_6` D35-37; EXP Jump guard verified).
  Host: implement the flag + those three gates; clear on landing.
- **Glider / nav auto-fly** (MED-HIGH): no dedicated glider move state. Entry is move state
  **AUTOFLY 0xF** + path data; `ProcessNavAutoFly` (HD 0x1403175B0 / EXP 0x140345770) and
  `ProcessAutoFly` (HD 0x140316990 / EXP 0x140344B20) follow the server/path track and use the
  shared vertical integrator; represent attaches nav-fly vehicle `NavFlyVehicleRideID=1152`
  (`WJ_吊篮002_HD.mdl`, IdleAniID 31157) and `nVehicleTrack/nVehicleNode` state attrs; camera is
  the GliderCamera set (asset not in base paks). Mode skill name is `天原绝境_滑翔翼` (29021).
  Host: implement AUTOFLY path following + ride 1152 + camera mode; do not invent a glide
  equation (client has none).
- **Vehicles / manned spaces** (HIGH names, delta re-derived): `KRLCharacter::UpdateMannedSpace`
  EXP 0x18026AB20 (+SkillBuff 0x180292A50); `mannedspace.krl.txt` (134,067 B, identical) is
  `载具ID → Name/MDL per faction/Socket="s_hs"/Transform state→anim` for 神机车/摧城车 (ids
  1-16, 37-51); war elephant/airship have no row (skill/item driven, UNPROVEN). Host: generic
  `MountMannedSpace(id)` binding to `s_hs` + Transform table + dynamic action bar.
- **Fly/bird** states: `FlyTo`/`EndFlyJump`/`BirdFlyTo` EXP 0x14033B6A0 / 0x14033AFE0 /
  0x140333ED0 (`exp16/1_movement.md`), states 0x1F/0x20/0x21 and 0x23/0x24/0x25; parachute off
  required for bird fly.

---

## 7. ACCEPTANCE CRITERIA (input → expected, testable)

Host-level behaviors; each can be checked in a scripted run. "Mounted" = host character model
with `bOnHorse` equivalent set and a horse item equipped.

1. T with no horse item equipped → **mounted stays false**, no mount actor, log a reject.
2. T with horse item + not mounted → mounted becomes true; flags `bOnHorse=1, bHoldHorse=0`;
   horse actor loaded from `rides.txt` row for the item's ride type; horse attribute id recorded.
3. T while mounted → dismount; horse item + the four equip items (boxes 0x18..0x1B) unequipped;
   `bOnHorse=0, bHoldHorse=0`; mounted actor removed.
4. Mount while `bIgnoreGravity` (falling/pull) or `[+0x160]` state set → reject (criterion 1
   shape). Re-press after clearing succeeds.
5. Remote mount sync: applying a record with flags bit1 set on a remote character → flags +
   horse attribute id applied directly, no local guard (server-authoritative).
6. Mounted idle (ON_STAND) Space → character **does not leave the ground**; no jump state 4;
   equals the client's skill-13618 branch.
7. Mounted moving forward (ON_RUN/ON_WALK) Space → jump: state 4, Vz ≈ 180 + horse bonus,
   Vxy ≈ 60 (host sprint-branch mapping) or the host's authored skill arc; lands, still mounted.
8. Mounted moving backward Space → same as idle (no jump).
9. Mounted double press: press 1 (moving) jumps; press 2 while airborne → **still mounted**;
   jumpCount≥1 press is rejected (no second arc) unless the host has deliberately entered the
   sprint state.
10. Sprint state (host sets `bSprintFlag`) + mounted + jumpCount==1 + Space → dismount first
    (flags cleared, horse unequipped) then a normal jump; no horse triple for the post-dismount
    jump.
11. Mounted sprint jump when `nHorseSprintPower <= cost` → rejected (no state-4 entry).
12. Landing after any mounted jump → `nJumpCount` resets to 0; next jump accepted; mount flags
    untouched.
13. Mounted + parachute flag → Space rejected; while falling, `Vz` is preserved (no air-stop).
14. Mounted + hang flag → Space rejected.
15. Mounted at `nJumpCount == 0` in sprint state → horse triple exactly `(60, 180+bonus, 11)`
    (assert values in the host trace; values from JumpParam columns 159-161).
16. Turn in place while mounted → heading changes with `CharacterRideYawTurnSpeed` 0.003465 /
    reset 0.0023, not the unmounted 0.007465.
17. Mounted walk/run → gait speeds selected from 8 / 40 (尺/s table) and the mounted animation
    set (idle/walk/run clips resolved through the ride adjust table + 999 override).
18. Mounting applies `CameraAdjust` (80 for ordinary horses) to the camera; `CameraRideYawOffset`
    0.
19. Dismount mid-air via T → flags cleared immediately; fall continues with unmounted animation.
20. A ride type absent from the adjust table → locomotion falls back to the base (AdjustAniID 0
    → passed-in clip id).
21. Horse actor scales from `rides.txt` (`ModelScale`, `SocketScale`); footprint SFX fields
    drive footprints; `RideFootContactGroundDis=5`.
22. `ride_rush.txt` fade values observed for the mount transitions: fade-in 1000/3000 ms,
    time-to-opaque 1100 ms, fade-out 15000/10000 ms/15°, 淡出变速率 0.3, 淡出停止比例 0.1.

---

## 8. ASSUMED-vs-VERIFIED (old doc `docs/character/3_6_MOUNTS_GLIDER.md`)

| # | Old claim | Verdict | Correction / evidence |
|---|---|---|---|
| A1 | `bOnHorse [+0x1E774]`, `bHoldHorse [+0x1E77C]` | **VERIFIED (HD)** | confirmed by getters 0x1403E1B70/0x1403E1B90; EXP moved to **+0x11A14/+0x11A1C** (`attr_exp.txt`) |
| A2 | `RideHorse 0x14036C210` guards/sets | **VERIFIED** | EXP 0x1403A2F20; flags are set *before* the equip-apply call; precondition = `ApplyAttribHorse` (horse item required) |
| A3 | `DownHorse 0x140365C60` unequips boxes 0x18..0x1B | **VERIFIED** | EXP 0x14039C270; container +0xB980→**+0x13E0**; **no airborne guard** (mid-air dismount possible) |
| A4 | `ProcessRideHorse 0x1403AFB80` | **VERIFIED** | EXP 0x1403EA300; reject NPCs |
| A5 | "mounted && jumpCount<1 selects horse triple" | **INCOMPLETE** | triple selection is inside the **`bSprintFlag != 0`** branch only; `JumpParam` values 60/180/11 confirmed for all jump types (cols 159-161) |
| A6 | "second jump press (`jumpCount==1`) dismounts" | **SCOPED** | true only in the sprint branch; ordinary double press does **not** dismount (input layer never reads `IsKeyDoubleDown` when mounted). Implement per §3 |
| A7 | `[+0x34C]` horse Vz bonus | **VERIFIED, moved** | EXP **+0x358**; read at 0x14033EECD |
| A8 | ride speeds 8/40, yaw 0.003465/0.0023/0.006283 | **VERIFIED values + offsets** | EXP loader 0x18063B470 maps them to CommonNumber +0x60/+0x64/+0x68/+0x6C/+0x70; **consumer sites still unlocated** (register-indirect, open item) |
| A9 | mount op vocabulary (`HORSE_ATTRIBUTE`=10, `HORSE_RUN_TYPE`=11, `HORSE_JUMP_SPEED_ADDITIONAL`=0x12 …) | not re-probed | treated as name-level unchanged |
| A10 | "jump triple 60/180/11 + `[+0x34C]`" host note | **INCOMPLETE** | add the `bSprintFlag` gate + input-layer routing (§3); otherwise the host jumps from idle and/or dismounts on double press (both wrong) |
| A11 | represent `pRide [+0x39F8]`, `pRideRush [+0x3A00]`, socket `[+0x3A08]` | **HD only, UNVERIFIED for EXP** | EXP represent is a rebuilt DLL; new anchors listed §4; EXP pRide offsets need a probe |
| A12 | 999 sentinel override | **VERIFIED** | EXP wrapper 0x180640FC0, gate `[0x180C114C8]+0x2F0D6` (`exp16/3_character.md`) |
| A13 | `rides.txt` = actor table | **VERIFIED + UPDATED** | EXP 90,444 B / 627 ids (old copy 605); CameraAdjust=80 for horse rows 1-6; +401-420, 1321-1323 added |
| A14 | `player_animation_adjust_rides_type_state.txt` 64 states | **VERIFIED** | re-extracted from EXP (88,875 B; exactly 64 state names; 135 RideTypes) |
| A15 | `rides_animation.txt` clip list | **VERIFIED** | 13 columns decoded incl. `锁定朝向`, `IsVehicleCanPlay`; EXP +356 B |
| A16 | `ride_rush` fade/mount/jump/fall columns | **VERIFIED** | 46-column header decoded; horse row values §5.3; ride_rush byte-identical to HD-era copy |
| A17 | parachute `[+0x214]`, op 9, no-jump/keep-Vz | **VERIFIED (EXP unchanged)** | getter 0x140453040; jump guard present in EXP `Jump` |
| A18 | glider = AUTOFLY + nav-fly ride 1152 + camera | **VERIFIED (path-driven)** | `NavFlyVehicleRideID` loader offset EXP +0x260; RVAs in §6 |
| A19 | skill 29021 named 滑翔翼 | **VERIFIED** | (doc already corrected) |
| A20 | host note "T as ProcessRideHorse(char,!mounted)" | **OK with caveat** | replicate the exact guard set of §1.4, including `[+0x160]` and the horse-item precondition; do not set flags on failed precondition |

---

## Reproduce (scratch)

```powershell
$py  = "C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
$S   = "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x"
$exp = "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\exp16"
$HD  = "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"
$EX  = "C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64\JX3ClientX64.exe"

# xrefs + RVAs (both builds)
& $py "$exp\xall.py" $HD  KPlayer::RideHorse KPlayer::DownHorse ProcessRideHorse > "$S\xall_hd.txt" 2>&1
& $py "$exp\xall.py" $EX  KPlayer::RideHorse KPlayer::DownHorse ProcessRideHorse > "$S\xall_exp.txt" 2>&1
# disasm
& $py "$exp\dump2.py" $HD 0x140313680 0x140313d90 > "$S\hd_jump_full.txt"       # Jump (HD)
& $py "$exp\dump2.py" $EX 0x14033eac0 0x14033f4ce > "$exp\new_jump2.txt"        # Jump (EXP)
& $py "$exp\dump2.py" $HD 0x14036c210 0x14036c340 0x140365c60 0x140365e10 0x1403afb80 0x1403afca0 > "$S\hd_ride_fns.txt"
& $py "$exp\dump2.py" $EX 0x1403a2f20 0x1403a3050 0x14039c270 0x14039c430 0x1403ea300 0x1403ea420 > "$S\exp_ride_fns.txt"
# attribute table offsets (both builds)
& $py "$S\find_attr_table.py" $HD bOnHorse bHoldHorse bSprintFlag bOnParachuteFlag   > "$S\attr_hd.txt"
& $py "$S\find_attr_table.py" $EX bOnHorse bHoldHorse bSprintFlag bOnParachuteFlag   > "$S\attr_exp.txt"
# Lua Space handler (shipped bytecode -> full listing)
& "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\lua-5.1.5\lua-5.1.5\build32\luac32.exe" -l -l `
  "C:\Users\Zhibin Ren\Desktop\reborn\proof\controls\ui_lua\hotkeys_script.lua" > "$S\hotkeys_luac_ll.txt" 2>&1
# EXP table extraction (15 files; scratch mirror + junction as in exp16)
& "$exp\redir\x\bin64\PakV4SfxExtract.exe" "$S\pathlist_mount.txt" "$S\mount_out"
& $py -X utf8 "$S\dump_mount_tables.py"  > "$S\mount_tables_dump.txt"
& $py -X utf8 "$S\dump_states.py"        > "$S\states_dump.txt"
# EXP CommonNumber loader offsets + GetRideStateAnimationID
& $py "$exp\dump2.py" "C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64\JX3RepresentX64.dll" `
  0x18063b740 0x18063b860 0x18063c1c0 0x18063c260 0x18063c4c0 0x18063c530 > "$S\exp_numbers_loader.txt"
& $py "$exp\dump2.py" "C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64\JX3RepresentX64.dll" `
  0x180222f40 0x180223100 > "$S\exp_ridestateanim.txt"
```

Read-only guarantee: nothing under `C:\SeasunGame` was written; extraction used a scratch
mirror + NTFS junction for the PakV4 store, outputs under `%LOCALAPPDATA%\Temp\opencode\spec3x\`.

**Open probes (do not treat as settled):**
1. EXP `pRide/pRideRush/socket` represent offsets (HD +0x39F8/+0x3A00/+0x3A08) and the seat
   socket name (`KRLRide::BindCharacter` EXP 0x1802D84E0).
2. CommonNumber ride-speed consumer sites (both builds; register-indirect).
3. Skill 13618 / 13951 / 44565 effects (hidden skills; not in player Skill.txt, not in
   `skills.tab` mode extraction) — needed only if the host wants skill-accurate motion.
4. Whether the server's jump move record for an ordinary (non-sprint) mounted jump uses
   `KCharacter::Jump` with the velocity branch or a skill-move track.
5. `OnSyncMoveParam` EXP RVA (bSprint bit packet unchanged assumed).

---

## Acceptance verification (2026-10-07, agent/3x-mount re-implementation)

Implemented in `client/MountSystem.cs` + `client/RebornClient.cs` (feature build
`reborn_client_3x_mount.exe`). Driven runs (logs under `proof/character/mount/`,
shots + `spec_image_stats.txt`), full map + 1x1 mini sandbox, all `DONE`:

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | T no horse item -> stays false + reject | **PASS** (`RC_HORSE_ITEM=0`: `mount rejected: no equipped horse item`) | mode-2 run `002025` |
| 2 | T with item -> mounted, flags, ride actor, attr id | **PASS** (`mount: on ride=0 item=1 attrId=10000 ...`) | all runs |
| 3 | T while mounted -> dismount + unequip steps, flags cleared | **PASS** (`C3 T dismount mounted=0`) | mode-1 `001917` |
| 4 | Mount while bIgnoreGravity / [+0x160] -> reject | **PASS** (airborne T: `bIgnoreGravity (RideHorse guard 1)`; grounded T mounts) | mode-2 `002025` |
| 5 | Remote sync applies flags/attr directly, no guard | **PASS** (`ApplySyncRecord` log, both directions) | mode-3 `002025` |
| 6 | Mounted idle Space -> no jump (skill 13618) | **PASS** (`Space -> skill 13618 ... no jump`, grounded stays 1) | mode-1 `001917` |
| 7 | Mounted moving forward Space -> jump (sprint branch) | **PASS** (skill 44565 + `horse triple=(60,180+bonus,11)`, lands mounted) | mode-1 `001917` |
| 8 | Mounted backward Space -> no jump | **PASS** (13618, grounded stays 1) | mode-1 `001917` |
| 9 | Double press airborne -> reject, still mounted | **PASS** (`branch B reject`, `mount intact after landing`, landed mounted=1) | mode-1 `001917` |
| 10 | Sprint state + jumpCount==1 -> DownHorse first, then normal jump | **PASS** (`mount: off` on press 2 under sprint intent) | mode-1 `001917` |
| 11 | Sprint jump with power <= cost -> reject | **PASS** (`nHorseSprintPower 0 <= cost 25`) | mode-1 `001917` |
| 12 | Landing resets jumpCount, mount untouched | **PASS** (`mount intact after landing n=1`; landed mounted=1) | mode-1 `001917` |
| 13 | Parachute flag -> Space rejected (+ landing clears) | **PASS** (`bOnParachuteFlag (guard 3)`) | mode-4 `002130` |
| 14 | Hang flag -> Space rejected | **PASS** (`bHangFlag (guard 3)`) | mode-4 `002043` |
| 15 | Sprint jumpCount==0 -> exact triple | **PASS** (`(60,180+bonus,11)`, power 75/100) | mode-1 `001917` |
| 16 | Mounted turn rate 0.003465 (*1000=3.465) not unmounted | **PASS** (applied + logged `sprint intent ON turnRate=3.465`) | mode-1 `001917` |
| 17 | Mounted walk/run 8/40 + mounted anim set | **PASS** (120/600 u/s bridge + adjust-table clips, existing proof) | earlier runs |
| 18 | CameraAdjust applied on mount | **PARTIAL/PROVISIONAL (P2)**: value read per ride (80 for 1-6) + logged, scale 0.01 deg/unit pending the unit probe | mode-1 log |
| 19 | Midair dismount via T -> flags cleared, unmounted fall | **PASS** (`C19 midair T mounted=0`, fall continues unmounted) | mode-1 `001917` |
| 20 | Ride absent from adjust table -> AdjustAniID 0 fallback | **PASS** (`RC_MOUNT_RIDE=1152`: `ride absent -> AdjustAniID 0 fallback`) | mode-3 `002025` |
| 21 | Scales from rides.txt; footprint fields | **PASS (scale) / logged (footprints)**: ModelScale/SocketScale 1.0 applied; footprint SFX fields logged | mode-1 |
| 22 | ride_rush fade values observed | **PASS (logged)**: in 1000/3000/1100, out 15000/10000/15deg, speed 0.3, ratio 0.1; the fade-in clip AVs the host (D3) | mode-1 log |

**Host-server rules (spec 3.3 recipe), registered provisional:**
- P1 sprint power pool: max 100 / cost 25 / regen 25/s grounded (`RC_RIDE_POWER_*`);
  criterion 11 proven by draining the pool.
- P3 deliberate sprint intent: `RC_MOUNT_SPRINT` (the client receives `bSprintFlag`
  from the server record; the host defines the rule). `bSprintFlag = mounted &&
  forward-moving && (grounded || sprint intent)` — this makes the moving press the
  sprint branch (criteria 7/15) while the ordinary airborne double press stays branch B
  (criterion 9) and the deliberate sprint press-2 dismounts (criterion 10).
- P2 CameraAdjust unit unresolved -> logged + 0.01 deg/unit scale (criterion 18 partial).
- D3 the `H加速奔跑01.tani` fade clip remains an AV boundary (not played).

---

## Real-input verification (2026-10-07, driven OS input)

The scripted harness bypassed nothing in logic, but it never walked a 180-degree
turn - the real-input rig (`tools/character/drive_mount.ps1`, fallback-verified
`drive_mount2.ps1`: verified foreground activation, PostMessage fallback) exposed a
real defect:

**Defect (before, run `proof/character/mount/realinput_before_flip.txt`):** the
runtime facing check compared the horse head to the instantaneous TRAVEL vector.
Walking backward (joystick mode turns the character 180 deg) made `head dot
travel=-1.00` during the turn and the check **mutated the placement (auto-flip
+pi)** - twice (`17:34:24.381/.395`), visibly flipping the horse. The horse must
follow the character heading (`+0x44`); travel differs from facing during turns and
backward movement.

**Fix:** the check now compares the horse head against the RIDER'S FACING
(`sin r, cos r`), logs `head dot rider`, and NEVER mutates the placement (a
persistent mismatch logs an anomaly after 5 samples instead).

**After (run `proof/character/mount/realinput_after_fix.txt`):** full real-input
scenario - T mount; idle Space -> skill 13618 (no jump); W + Space -> skill 44565 +
horse triple 60/180/11; airborne double -> branch-B reject with the mount kept;
landing intact; S + Space -> 13618 (no jump); T dismount; `DONE`; **zero auto-flip
lines**, `head dot rider=1.00` through the 180-degree turn (riderYaw 0 -> 3.14).

**Scope note (spec 6, named not faked):** vehicles/manned spaces (神机车/摧城车),
glider/nav-fly (AUTOFLY + ride 1152 + GliderCamera), full parachute behavior
(flag + jump reject are in; Vz-preserve/bird gates are not), double-ride
(`ride_link.txt`), footprint SFX, fade phases (fade-in clip is an AV boundary D3),
and the hidden skill motions for 13618/44565 remain separate unimplemented features.
