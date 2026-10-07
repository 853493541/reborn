# D — MOTION / SKILL-DISPLACEMENT system spec (JX3 "Reborn")

**Area:** character · **Branch:** agent/3x-integration · **Index:** `docs/character/README.md` · **Status:** redefined spec 2026-10-06 (client-truth, 1-5-0 + 1-6-0 deltas) — supersedes the assumed behaviors in the older docs.
**Correction 2026-10-07 (`agent/3x-motion`):** §3.1's group-type mapping was off by one — the file's **type 2 = MotionTag** (type 1 = the FMOD sound group); the group walk is now proven and tooled. Everything else was implemented as written and passed §5.

**Purpose:** settle how the JX3 client displaces a character during a skill (太阴指 / any dash), so a
future host implementation agent can wire it exactly. Supersedes the assumptions behind the current
host dash (camera spin on a standing cast).

**Truth sources (read-only):**

| Tag | Install | Build | Identity |
|---|---|---|---|
| HD (release, primary) | `C:\SeasunGame\Game\JX3\bin\zhcn_hd` | 1-5-0-9975, binaries 2026-09-27 | `JX3ClientX64.exe` 11,349,432 B sha256 `95A651D8B1DC7984D64F410BFA04A06A71F450EF7E3577F2FFFD1BA746D8F153` |
| EXP (delta) | `C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp` | 1-6-0-9536, binaries 2026-10-03 | `JX3ClientX64.exe` 11,823,024 B |
| Engine (shared) | HD `KG3DEngineX64.dll` | byte-identical HD↔EXP | sha256 `891A625A4053FF5214386F0DDE7424696716C5C577E608EDF092EDC45F94B049` |
| Tag container | HD/EXP `KG3D_AnimationTagX64.dll` | 3 builds (ME/HD/EXP) | EXP sha256 `4F34349DBD13B665C1930E7C086652FD51A0B287DF2E25779337C623A9304762` |

**Method:** one-pass xref/disasm on the installed binaries (`xref_string.py`, `find_calls.py`,
`fn_at.py`, `dump2.py`, scratch `scan_disp.py`), official `PakV4SfxExtract.exe` in a scratch mirror
for tables. Installs read-only. Everything under
`C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x\`. No repo writes.

**Confidence:** HIGH = exact string/assert xref + .pdata boundary + instruction-level field check;
MED = pattern/boundary inference; LOW = not probed. Claims carry `path + symbol/RVA`.

---

## 0. TL;DR (what to implement)

1. **The displacement is not the `.tani` vector and not root motion.** It is a per-tick velocity curve
   from `settings/SkillMove.tab`, applied by `KCharacter::OnSkillMove`, integrated along the character's
   **motion heading** `[+0x26C]`.
2. **Direction:** heading is **initialized to the facing** `[+0x44]` at move start
   (`KCharacter::SkillMove`) and only changes by the row's per-frame `DirectionXY` deltas
   (byte angle, π/128/unit, wrap 0..255). **The body facing is never auto-turned toward the travel
   direction during the move** — no `atan2(velocity)` steering exists in the client.
3. **The 太阴指 authored float `(15.0, -156.32)` is an in-place lunge/pose vector**, not a dash:
   the measured root arc goes out 156.7 u and **returns** (net horizontal 10.37 u over 46 frames).
   The client does not read that block for movement at all (F1 clips are not on the
   `player_animation_use_originroot_trans.txt` whitelist; the `.tani` is a tag container).
4. **A host without a server authors the move itself**, exactly like the server move command does:
   pick the `SkillMoveID` row, set facing/heading/counters, then run the per-tick applier. Live, the
   authoritative path is the server's `KPlayerClient::OnAdjustPlayerMove` packet (SkillMoveID at
   `pkt+0x42`, frame counter at `pkt+0x19`, blend weight at `pkt+0x46`, facing at `pkt+0x17`,
   heading byte + velocity/height/gravity packed word at `pkt+0x23`) plus a separate 0x20-byte
   per-frame move-record queue.
5. **Camera:** the client camera never chases the skill heading. Its only move-turn coupling is
   `CameraAdjustYawWhenMoveTurn` (dead zone 0.26 rad) driven by **input movement** (moving only).
   A host that sets `yaw = atan2(travel)` per frame during the move and feeds it to the camera turns a
   standing cast into a spin (positive feedback / `atan2(0,0)` garbage). Fix = don't.

---

## 1. How the client ACTUALLY displaces a character during a skill

### 1.1 The chain (release build RVAs; EXP deltas in §7)

```
skill cast resolves (client action / server packet)
        │
        ├─(A) client-data path: KObjSlotInfoList::GetSlotInfo(settings/ObjSlotInfo.tab, SlotID)
        │      → OutSkillMoveID, OutOffsetFaceDirection
        │      KCharacter::ClearSlotInfo 0x14030F6B0: [char+0x44] += faceOffset (wrap 0xff),
        │      then KCharacter::SkillMove(0x14031C4A0)(char, id, 0)
        │      [1-6-0 data: all OutSkillMoveID == 0 → path is data-dead; see §6]
        │
        └─(B) server path: KPlayerClient::OnAdjustPlayerMove 0x14013AAA9 (fn 0x14013AAA9)
               → writes [+0x44] facing, [+0x48] turn rate, [+0xC08] frame counter,
                 packed velocity/heading/height/gravity word, 7 flag bits,
                 destination + MoveTo, and the skill-move fields:
                 [+0x2AC]=SkillMoveID (pkt+0x42>>1), [+0x2B0]=mode (bit0),
                 [+0x2B4]=blend weight (pkt+0x46),
                 then runs master-update catch-up frames

KCharacter::SkillMove(id, src|dst)  0x14031C4A0
  assert 1..0x800; pSettings = KGJumpList::GetSkillMoveSetting(g+0x23810, id)
  reject while [+0x2CC]!=0 or (state∈9..0x16 and mode) …
  [+0x2AC]=id ; [+0xC08]=word[pSettings+0] (TotalFrame) ; [+0x26C]=[+0x44] (heading:=facing)
  [+0x174]=0; [+0x2C8]=0; [+0x210]=0; [+0xC04]=0; [+0x204]=1
  state [+0x1F4] = 0x1A (src) or 0x1B (dst); [+0x2B0] = mode
        │
        ▼  every logic tick (15 Hz) in the character update:
KCharacter::OnSkillMove  0x140314DE0   (assert @0x140314e1b)
  if [+0x2CC]!=0 → OnParkour-style body 0x1403145C0
  remaining=[+0xC08]; if <=0 → finisher 0x14031C6D0 (zero V, state 4, or turn-in-place)
  idx = TotalFrame - remaining
  if idx == word[row+0x5A4] (the TSV "Column" field) → end phase: state 0x1C
  if [+0x2B0]!=0 → zero [+0x268]/[+0x2F8]
  elif byte[row+0x504+idx] != 0:
     w = clamp([+0x2B4],0,255); k = 255-w
     [+0x2F8] = VXY[idx] * k / 255          (VXY = word[row+2+2*idx], signed)
     [+0x268] = VXY[idx] * k * 16 / 255     (fixed <<4 copy)
     [+0x270] = VZ[idx]  (dword[row+0x144+4*idx], or  relative: +dword[row+0x140+4*idx])
     [+0x26C] += DirectionXY[idx]           (word[row+0x3C4+2*idx]) wrapped 0..255
     clamps: [+0x268]∈[0,0x7FF], [+0x2F8]∈[0,0x7F], [+0x270]∈[-0x800,0x7FF]
  dec [+0xC08]

world integration (client): X/Y velocity along heading, Z by the vertical integrator
  heading h ∈ 0..255 byte angle, unit π/128 (decoder 0x14037F7D0 × 0.024543693)
  0x14020ED10(dx,dy) = heading  (0 = +X, 64 = +Y, 128 = -X, 192 = -Y; quadrant code + arctan table @0x140A0DEA0)
  0x14020EE10(h)      = sin(h·π/128) Q12 (table entries: [0]=0, [32]=2896, [64]=4096)
  Px += cos(h·π/128)·VXY ; Py += sin(h·π/128)·VXY ; Pz += VZ (gravity/floor rules as jump)
```

**Provenance for the position integration:** the per-tick XY add itself is inside the shared
character tick (the same path that consumes `[+0x2F8]`/`[+0x26C]` for RunTo/Jump/Parkour). The exact
integrator function was **not pinned this pass** (see §9 probe 1); the velocity fields, their units,
the heading convention and the clamp/application code are all HIGH. `KCharacter::MoveTo 0x140314230`
is a teleport (cell-validated, sets `[+0x10]/[+0x14]/[+0x18]`, floor `word[cell+6]<<6`), not the
per-tick mover.

**Companion state** `KCharacter::SpecialSkillMove 0x14031C820` + `KCharacter::OnSpecialSkillMove`
(fn 0x140314DE0 chunk @0x1403153A0, assert @0x1403156E7) is the same mechanism on counters
`[+0xC1C]` (id) / `[+0xC18]` (remaining) / `[+0xC20]` (state 0x1A/0x1B/1=done). Used by scripts/modes;
same row format. `KCharacter::OnParkour` (0x140314510 / body 0x1403145C0) is the `ParkourMove.tab`
twin.

### 1.2 `settings/SkillMove.tab` — the only per-frame displacement source (HIGH)

Extracted read-only from the EXP store with the official extractor
(`spec3x\ex_slot\settings\SkillMove.tab`, 952,312 B, 935 rows + header, GBK TSV, up to 757 columns;
1-5-0: 934,596 B / 918 rows, sha `5fce86a3` — rows 1..952 byte-identical, +17 appended, exp16 §tables).

Columns: `SkillMoveID, IngoreGravity, TotalFrame, Column, SkillMoveOnlyFly, SkillMoveDeath,
SkillMoveEndButKeepVelocity, CanJump, CanBanMask`, then up to 187 quadruples
`FrameN, VelocityXYN, VelocityZN, DirectionXYN` (FrameN is the frame index 0,1,2,…; empty tail).

Runtime mapping (proven by disasm, both builds same semantics):
- `word[row+0]` = TotalFrame; `word[row+0x5A4]` = the TSV `Column` (end/transition frame);
  `byte[row+0x504+idx]` = per-frame enable; `byte[row+0x5A9]` = Vz absolute-vs-relative select;
  `byte[row+0x5AA]` = keep-velocity-at-end; `byte[row+0x5AB]` = restriction flag.
- VXY ∈ 0..127 effective (word, clamped); VZ ∈ ±2048; DirectionXY signed byte-angle delta.
- Sampled rows (EXP store, verbatim): ID 2 death 50f; ID 200 flying 33f VZ 1500-1700;
  ID 300 51f VXY up to 333 (clamped to 127 at runtime); ID 700 25f DIR -128 at F3.

### 1.3 The server move command + move-record queue

Decoded in full in §2. Live, a skill's displacement arrives in `OnAdjustPlayerMove`; the queue
records (0x20 B) carry per-frame height/gravity modifiers and follow-motion.

### 1.4 What is NOT the displacement source (the old assumption, refuted)

| Candidate | Verdict | Evidence |
|---|---|---|
| `.tani` "motion vector" floats `(15.0,-156.32)` etc. | **NOT displacement.** The `.tani` is the ANI_TAG container (tag groups); the floats sit inside a tag record (hash `0x4A8C8588`, block `0x230/1/0/71/…`). No client code feeds them to movement. 太阴指's ANIM root arc lunges and returns (net 10.37 u / 46 f) → an in-place attack, not a dash. | `.tani` loader §3.1; old proof `taiyin_motion.json` (net 10.3698, peak 156.72) |
| ANIM root motion | **Not applied** for F1 skill/locomotion clips: `player_animation_use_originroot_trans.txt` whitelists only 9 F1 rows, all `游泳`. | `docs/character/3_2_3_3_LOCOMOTION_MOTION.md` V19/V20 (HIGH) |
| ANIM v4 clip data | Rendering only (SKEL/TRKS/TRCK… chunks); no displacement consumer found. | exp16 3_character §format (HIGH) |
| `player_rush_skill.txt` / `skill_rush_state.txt` | Represent-side *visuals* (tani/animation per 位移技能ID), not the mover. | proof/gravity tables; `KRLRushState::UpdateMoveAnimation` |
| `ObjSlotInfo.tab` OutSkillMoveID | Client-side slot→move map; **all zero in the 1-6-0 store** (138 data rows). | `spec3x\ex_slot\settings\ObjSlotInfo.tab` |

### 1.5 Units and arithmetic (HIGH unless noted)

- Logic tick 15 Hz (66.7 ms). VXY/VZ are **units per tick**; 1 u = 1 cm (G-0); docs' "1 m = 192 u"
  label is stale.
- Blend weight `[+0x2B4]` (server byte): effective V = V × (255−w)/255. w=0 → full, w=255 → zero
  (verified by emulating the exact `magic 0x8080808080808081, sar 7` sequence — signed divide by 255:
  VXY=1,k=255 → 1; VXY=255,k=255 → 255).
- Heading wrap: add delta, if <0 +0x100, if ≥0x100 −0x100 (byte wheel).
- Facing has a **turn-rate byte** `[+0x48]` and global rates
  `CharacterYawTurnSpeed=0.007465`, `CharacterYawTurnResetSpeed=0.0023`,
  `SkillTurningToTargetSpeed=0.01`, `SkillTurningToLogicSpeed=0.01`, `SkillTurningTime=1500`
  (`number.krl.txt`; the per-char `[+0x48]` is the live one, set by the server packets).

---

## 2. Server/record-driven displacement — exact contents + host simulation

### 2.1 `KPlayerClient::OnAdjustPlayerMove` (the authoritative move command)

HD fn **0x14013AAA9** (chunk of 0x14013AA40; assert `KPlayerClient::OnAdjustPlayerMove` @0x14013AC0C);
EXP **0x1401346B0**. Payload decoded (packet = `rdi`; character = `rbx`):

| packet | → character | meaning |
|---|---|---|
| +0x07 dword | resolved char | id (bit30 = player) |
| +0x0B | sequence | vs `[clientPlayer+0xF9C]` |
| +0x0F dword | `[+0x1FFE0]` catch-up | server frame number (replays missing ticks) |
| +0x13 dword | `[+0x264]` | move/action id aux |
| +0x17 byte | `[+0x44]` | **facing** (byte angle) |
| +0x18 byte | `[+0x48]` | **turn rate** (byte) |
| +0x19 byte | `[+0xC08]` | **remaining frames** of the move |
| +0x1A (via 0x14016AD70) | `[+0x368]`, `[+0x36C]`, `[+0xC28]` | 3 flag bits |
| +0x23 (via **0x14016ABE0**) | packed move word | bits0-10 `[+0x268]`; 11-22 `[+0x270]` Vz (signed); 23-30 `[+0x26C]` heading; 31-37 `[+0x2F8]` VXY; 38-44 `[+0x2FC]` height; 45-49 `[+0x320]` gravity; +flags |
| +0x33 (via **0x14016AEF0**) | 7 flags | bit0 `[+0x1F8]`, bit1 `[+0x208]`, bit2 `[+0x230]`, bit3 `[+0x200]`, bit4 `[+0x174]`, bit5 `[+0x2CC]`, bit6 `[+0x2F4]`, bits7-9 `[+0x22C]` |
| +0x35..qword | destination | 18/18/22-bit x/y/z → `[+0x2A0/2A4/2A8]` then `MoveTo` |
| +0x3D byte&0x7F | `[+0x300]` | height-scale accumulator (0..127) |
| +0x3E i16 | `[+0x304]` | height-bias accumulator |
| +0x40 i16 | `[+0x32C]` | gravity-bias accumulator |
| +0x42 dword | `[+0x2AC]` = >>1, `[+0x2B0]` = bit0 | **SkillMoveID + mode** |
| +0x46 byte | `[+0x2B4]` | **blend weight** 0..255 |

`[+0x300]/[+0x304]` and `[+0x328]/[+0x32C]` are per-frame *accumulators*; the frame update derives
`[+0x2FC] height = ((acc+0x400)*scale)>>10`, `[+0x324] gravity = …` clamped 0..0x1F
(appliers §2.2 and master 0x140310F50 @0x140311722-0x1403117F0).

`OnSyncMoveParam` (0x140158A30) / `OnSyncMoveCtrl` (0x140158870) / `OnSyncMoveState` (0x140158C50)
are the same vocabulary: facing+turndelta+`[+0xC08]`, a packed move word, a 7-bit flag word
(→ `[+0x1F8]`,`[+0x208]`,`[+0x230]`,`[+0x200]`,`[+0x174]`,`[+0x2CC]`,`[+0x2F4]`), 3-bit `[+0x22C]`,
18/18/22-bit destination, then `MoveTo` (0x140314230).

### 2.2 The 0x20-byte move-record queue (per-frame replay stream)

- **Producer:** fn **0x140140AA0** (HD; packet handler, no direct callers → dispatch-registered)
  builds a record on the stack and pushes it into the character ring `[char+0x20010]`
  (ring fields are capacity/ptr/head/count; growth via 0x14016B960).
- **Consumer:** the character frame update. HD: applier fn **0x140182470** (called from
  0x14017441B / 0x1401785E0) and the near-identical **0x140362DA8** (exact-frame match variant,
  called from 0x14036298D); the master update fn **0x140310F50** (0x14031103E-0x140311A26) has the
  same logic inline and is the one `OnAdjustPlayerMove` calls for catch-up. Drain protocol: ring1
  `[+0x20010]` → ring2 `[+0x1FFE8]` → process records matching frame `[+0x1FFE0]`, then clear.
- **Record layout (proven by producer push order + applier reads):**

| rec | size | meaning | applied to |
|---|---|---|---|
| +0x00 | u32 | frame/time (== `[+0x1FFE0]` tick) | scheduling |
| +0x04 | i32 | height-scale delta | `[+0x300] +=` |
| +0x08 | i32 | height-bias delta | `[+0x304] +=` |
| +0x0C | i32 | gravity-scale delta | `[+0x328] +=` |
| +0x10 | i32 | gravity-bias delta | `[+0x32C] +=` |
| +0x14 | i32 | follow velocity (Q4) | kind1: `[+0x280]=v`, `[+0x2F8]=v>>4` |
| +0x18 | u8 | kind: 1 = set follow (vel+angle), 2 = interpolate angle | `[+0x280]/[+0x284]` |
| +0x19 | u8 | target angle byte (dir of follow motion) | `[+0x284]` |
| +0x1A | u8 | 1 = segment start, 2 = segment end | `[+0x1F8]` (+ cancels `[+0x2CC]`, 轻功 End) |
| +0x1B | u8 | 1/2 → set/clear `[+0x2CC]` (attach/pull) | `[+0x2CC]` |
| +0x1C | u8 | 1/2 → set/clear `[+0x2F4]` (sprint end) | `[+0x2F4]` |
| +0x1D | 3 B | zero pad | — |

- **`[+0x1F8]`** is the "segment end" flag: written either directly by the sync-flag bit0 or by
  record+0x1A==1 (→1) / ==2 (→0). The 轻功 chain End triple (`ModifySprintEndSpeed 0x1403140A0`) is
  applied by the applier when state∈{4,0x1A} && `[+0x1F8]==0` && jumpCount≥1 — i.e. the client
  never self-timers the chain; the server record ends the segment.

### 2.3 Host without a server — the simulation recipe (normative)

1. **Author the move per skill.** Store, per skill: `SkillMoveID` (row in `SkillMove.tab`) and an
   optional facing offset (the client's `OutOffsetFaceDirection`, currently data-dead). Apply the
   face offset once at start (wrap 0..255): `facing = (facing + offset) & 0xFF`.
2. **Start:** set `heading = facing`; `remaining = TotalFrame`; `Vz = raw row Vz at idx 0` (or 0);
   `state = SKILL_MOVE`; clear ground-glue/pull flags per the row (`SkillMoveOnlyFly`,
   `SkillMoveDeath`, `SkillMoveEndButKeepVelocity`, `CanJump`, `CanBanMask`, `IngoreGravity`).
3. **Tick (15 Hz), for idx = 0..TotalFrame-1:**
   - If the per-frame slot is empty → no velocity update that tick (client byte `+0x504+idx`).
   - `V = min(VelocityXY[idx], 127)`; position += `(cos(heading·π/128), sin(heading·π/128)) * V`
     (heading 0 = +X axis per the client's own atan2 0x14020ED10).
   - `Vz = VelocityZ[idx]` (clamp ±2048); feed the normal gravity integrator (`IngoreGravity` skips
     gravity; else same rules as jump/fall, ground clamp on landing).
   - `heading = (heading + DirectionXY[idx]) & 0xFF`.
   - **Do not modify facing. Do not turn toward the travel direction.**
   - At `idx == Column` (TSV field) the client enters the end phase (state 0x1C). The host may
     simply continue to the end (or trigger the end effect/camera if it wants parity).
4. **End** (idx == TotalFrame): zero `Vxy`/`Vz` unless `SkillMoveEndButKeepVelocity`; the client's
   finisher then sets state 4 (JUMP) when airborne else turn-in-place (0x14031BFE0). Host: re-enter
   idle/ground state, preserve the input velocity model.
5. **Special-case rows:** `SkillMoveDeath=1` → death move (state remains until respawn);
   `SkillMoveOnlyFly=1` → no ground snap while active.
6. **Server-shaped variant (if the host ever grows netcode):** emit/consume the exact
   `OnAdjustPlayerMove` fields above instead of step 1-2; the per-tick applier is the same code.

Note: `SkillMove.tab` is a **per-move** table, not per-skill; the skill→move association was a
client slot table (`ObjSlotInfo.tab`) that ships all-zero in 1-6-0 and, live, is server-driven.
The host must therefore own a small skill→moveID map (data gap to fill per skill).

---

## 3. MotionTag — container settled (both builds)

### 3.1 Container (HIGH, EXP `KG3D_AnimationTagX64.dll`; format shared HD/EXP)

- `.tani` = **ANI_TAG_FILE**: `KG3D_AnimationTani_Data::LoadFromFile` fn **0x18001D260**
  (assert @0x18001D2C9; `pcszFileName`); `tani mask error: %s` @0x18001D39A; version assert
  `dwVersion == ANI_TAG_FILE_HEADER::s_dwVersion0 || …1` @0x18001D3B7; magic compare
  `0x41544147` ("GATA") @0x18001D38F; header `ReadObject(0x130)` @0x18001D33D.
- **File layout (proven):**
  `+0x00` char[4] `GATA`; `+0x04` u32 version (0/1; ≥2 rejected); `+0x08` NUL base `.ani` path;
  `+0x10C` u32 groupCount; then per group a **12-byte header `{i32 type, i32 version, i32 count}`**
  followed by the payload read by that type's class (the outer loop delegates; group[type] stored at
  `[this+0x28+8*type]`).
- **Group type 2 = `KG3D_AnimationMotionTag_Group_Data`** — proven by the
  `KG3D_AnimationTani_Data::_NewTagData` factory **0x18001DB60** (switch `edx<=5`, jump table
  @RVA 0x1DEA4): case **2** allocates 0x160 and calls ctor 0x18000D160 which sets vtable
  **0x180048AC0**, and that vtable's slot 3 is
  `KG3D_AnimationMotionTag_Group_Data::LoadFromFile` **0x180003000** (vtable contains the fn ptr at
  VA 0x180048AD8). Case 0 = SFXTag_Group_Data (0x298, vtable 0x180048CC8, LoadFromFile 0x180004AB0);
  case 1 = the sound group (0x508, vtable 0x1800490B0 — its payload carries the `FMOD` magic and
  the Wwise event names); cases 3/4/5 = 0x130/0x58D8/0x1B0 (classes not decoded).
  **Correction 2026-10-07 (evidence-first):** the earlier pass read the factory case order off by
  one and claimed "type 1 = MotionTag"; the jump table and the file payloads both say type 2 (the
  type-1 payload in `太阴指_悟.tani` is `u32 0` + `"FMOD"` + the event string, not a 0x188 stream).
- **MotionTag payload** (both loaders agree, see §3.2): `LoadFromFile(reader, dwVersion, numKeyFrames)`
  with v0 = `numKeyFrames × ReadObject(0x970)`; v1 = `u32, u32` then `numKeyFrames × ReadObject(0x188)`;
  v2 = `u32 n2` + `n2 × 0x130` then v1.

**Group walk (proven 2026-10-07).** The records are the payload of the type-2 group; reaching it
requires the preceding groups' payload sizes. Type 0 (SFX) is exact:
`u32 n1 + n1*0x130 + count*(0x164 + 8*u32 + 0x64 + (v==3 ? 4 : 0))` (loader 0x180004AB0; the
0x1EC-per-record stride at v3 is byte-validated). Type 2 is exact per §3.2. The other types are
opaque (their loaders are not decoded), so the walker scans forward for the next plausible group
header such that the remaining groups walk exactly to EOF (structural scan, no raw signature).
Validated: `太阴指_悟.tani` walks group0 SFX @0x130 → group1 (type 1, opaque) → **group2 type=2
@0x1EE4**, whose 0x188 stream parses byte-exact to EOF 0x2088 (`key 0 time=6 hash='User Define
Tag' tags=[(0,8)]`). 8/11 sampled F1 tanis walk cleanly this way. Tool:
`tools/character/motion_tag.py` (selftest 15/15); client probe `client/SkillMotion.cs`.
**Which shipped tanis carry a MotionTag group is now enumerable with the tool.**

### 3.2 The 0x188 record stream (in-memory, both loaders; HIGH)

`KG3D_AnimationMotionTag_Group_Data::LoadFromFile` 0x180003000 + engine twin
`KG3DMotionTagData::LoadFromFile` 0x180299AC0 (engine DLL byte-identical both builds):
per keyframe one 0x188-byte record: `+0x100 u32 time/key index` (runtime inserts `time<<16|0xFFFE`),
`+0x104 u32 tagCount n`, `+0x108+4i u32 payloadSize[i]`; then `n` payloads, each beginning with
`u32 type` (0..11), deserialized with `min(sizeTable[type], payloadSize[i])` (size table @0x180047C60:
8, 0x118, 0x19C, 0x14, 0x10, 0xC, 0x2C, 0x30, 0x34, 0x68, 0x4C, 0x58). Per-keyframe 0x40-byte entry
= {hash string, vector<tag*>}.

### 3.3 Application + the 12 types (partially proven)

`KG3DAnimationMotionTag::Helper_Apply` **0x180287320** (engine; assert @0x18028738A): resolves the
scene, binary-searches keyframe times (0x18028F320), and for each not-yet-applied keyframe fetches
its tag pointers (0x1802994E0) and dispatches **per tag type** through a table at **0x1808A27D0**
(entry = {handler, this-relative slot}, 16-byte stride; handler calls confirmed at 0x180287582).
Event-handler class names present in the engine (RTTI): `KG3DAnimationMotionTag`,
`KG3DAnimationMotionTagTranisitionLifeRangeEventHandeler`, `KG3DAnimationSFXTag(+LifeRange/
SceneLifeRange handlers)`, `KG3DAnimationSoundTag`, `KG3DAnimationTagSFXMotionSwithEventHandeler`;
function strings `Helper_Apply`, `Helper_ApplySFX`, `OnApplyWeaponMotion`,
`_CreateWorldPositionRepeatSFXAndPutInWorld`, `OnMotionChange`, `PlaySameTag`,
`CreateRunTimeData`, `GetPatternModel`.

**Proven:** container+record stream; dispatch = per-type handler table; families Motion/SFX/Sound.
**Candidate (MED, from the old research + handler names, not yet 1:1):** 0 ForceMove/force-field,
1 IK, 2 SFX/particle, 3 weapon-motion, 4 sound, 5 camera/ani tag, 6-11 sub-variants (transition
life-range, texture, follow-position sound, …). Exact type-id→handler mapping = next probe (§9.3).

### 3.4 ANIM v4

`ANIM` v4 (f1 clips, 1-6-0) = SKEL (182 bones), TRKS, per-bone TRCK + ATRC/BTRC/…/VTRC chunks.
No displacement consumer; root transform only for whitelisted swimming paths. (exp16 3_character
format table, HIGH.)

---

## 4. The camera question — why a naive dash spins, and the correct behavior

### 4.1 Decoded client turn model (HIGH)

- Two separate angles: **facing** `[+0x44]` (body/model yaw, byte) and **motion heading** `[+0x26C]`
  (travel direction, byte). `KCharacter::SkillMove` sets `heading := facing` once, at start.
- During the move `OnSkillMove` only advances `heading` by the row's `DirectionXY` deltas
  (frame-authored; typically 0 or ±64/±128 snaps). It never writes facing. Nothing in the logic
  computes heading from velocity (`atan2(Vxy)` appears nowhere in the move path).
- Facing changes only via: server sync bytes (`OnAdjustPlayerMove` pkt+0x17 / `OnSyncMoveParam`
  pkt+0x0B), the turn command (`[+0x48]` turn rate; `SetRotation`/TurnTo), and the turn-in-place
  helper 0x14031BFE0 at movement stops. Global rates in `number.krl.txt`
  (`CharacterYawTurnSpeed=0.007465` … `SkillTurningTo*Speed=0.01`, `SkillTurningTime=1500`).
- Camera: `CameraSystem.FollowYaw` implements `CameraAdjustYawWhenMoveTurn` — drag camera yaw toward
  the **movement** yaw only **while moving**, and only after the dead zone
  `CameraAdjustYawWhenMoveTurnDisableAngle = 0.26 rad`. It is never driven by the skill heading.

### 4.2 Why a standing cast spins the camera (root cause class)

The current host dash makes the camera chase a yaw that it also feeds, or rotates the model to the
travel direction:

1. **`yaw = atan2(travel)` per frame.** This rule does not exist in the client. During a standing
   cast `travel ≈ 0` → `atan2(0,0)` is undefined (0 in C#); any nonzero residual (animation nudge,
   collision slide, heading deltas) flips the direction frame-to-frame.
2. **Feedback loop.** If travel is camera-relative ("dash forward = camera forward") and the camera
   follows the model/travel yaw, then each frame's dash direction rotates with the camera that is
   chasing it → constant rotation with **zero translation** (exactly "standing cast spins the
   camera"). The same loop exists in `ability_sandbox\rb\RebornClient.cs:1986`
   (`curYaw = atan2(ux3, uz3)` during the pull) and is why the client's own rule matters.
3. **Delta vs absolute.** `DirectionXY` is a per-frame **delta** added once per tick to the heading
   (client code adds word to `[+0x26C]` with byte wrap). Interpreting the authored value as an
   absolute heading, or adding it every render frame (not every 15 Hz tick), accumulates rotation.

### 4.3 Correct host behavior (normative)

- Motion: implement §2.3 — move along `heading` (starts at facing); apply `DirectionXY` deltas once
  per logic tick; **never** steer to the travel direction; never touch camera yaw for it.
- Facing: only the input/turn model may rotate the body (rate-limited). A skill's authored face
  offset is applied once at start.
- Camera: keep following the **input** movement direction with the 0.26 rad dead zone and only while
  `moving` (canonical behavior already present in `client/CameraSystem.cs:407-408` and
  `FollowYaw`): `if (moving && |turn| > dead) yaw += k * turn`.
- During the move the camera may still be user-orbited (drag) — the client does not lock it; FOV
  changes belong to the separate `SkillMoveCamera` system (`skill_move_camera.txt`, already wired).

---

## 5. ACCEPTANCE CRITERIA (numbered, testable)

1. **Standing cast, zero-velocity row (or any row at idx where VXY=0 for all frames):** the character
   translates 0 units and rotates 0 rad; the camera yaw/pitch are unchanged (numeric fingerprint
   over 30 s). The historical bug (camera spin on standing cast) must not recur.
2. **Standing cast, displacement row (e.g. SkillMove ID 300):** displacement along the cast facing
   equals `Σ min(VelocityXY[idx],127)` units ±1 tick (≈2 cm), measured by the position log; facing
   unchanged at DIR=0 frames.
3. **Camera-yawed cast:** cast the same move with the camera rotated 90°; travel is along the cast
   facing, not the camera view; camera yaw does not converge to the travel direction during the move.
4. **Moving cast:** with W held, the character keeps its input motion after the move; no teleport at
   start (first-frame step ≤ 127 u); camera keeps the input-follow dead zone behavior.
5. **Turning cast:** RMB/steer during the move changes facing at the client's rate (no snap);
   displacement stays heading-based (unaffected by the turn).
6. **Delta rows (e.g. ID 700 F3 DIR=-128):** heading rotates exactly −π at frame 3; the model does
   not auto-face the heading.
7. **`IngoreGravity=1` rows:** VZ held without gravity; `=0` rows integrate gravity and clamp
   VZ ∈ [−2048, 2047]; ground snap on landing.
8. **End conditions:** at `idx == TotalFrame` the move ends; `SkillMoveEndButKeepVelocity=1` keeps the
   velocity into the next state, `=0` zeroes it; `SkillMoveDeath=1` runs the death behavior.
9. **Blend weight:** with the server-style 0x2B4 byte w, the measured per-tick displacement scales by
   `(255−w)/255` (±1 unit); w=255 → no translation.
10. **Regression gates stay green** (§8 commands) and the camera FOV path (`SkillMoveCamera`) is
    unchanged unless the move's skill has a `skill_move_camera.txt` row.

---

## 6. ASSUMED vs VERIFIED — corrections of the old docs

| # | Old doc claim | Verdict | Evidence |
|---|---|---|---|
| 1 | `SKILL_MOTION_METHOD.md` / `SKILL_DATA_EXTRACTION.md`: "`.tani` motion vectors are the dash displacement; root arc encodes the same" | **ASSUMED → WRONG for movement.** The vectors are tag-record floats; no move-path consumer. 太阴指's root arc is an in-place lunge (net 10.37 u). Displacement = `SkillMove.tab` applied along heading. | §1.4, §3.1; `proof/netcode/skill_motion/taiyin_motion.json` |
| 2 | `3_2_3_3_LOCOMOTION_MOTION.md` V17/V18: "authored `.tani` vector == displacement intent consumed by the move layer" | **PARTIAL.** The correlation is an authoring artifact; the client consumes `SkillMove.tab`, not the tani block. | §1.1-1.2 |
| 3 | "MotionTag loaders only reachable through vtables; on-disk container not proven" | **CLOSED (container):** `.tani` GATA v0/v1 typed groups; **type 2 = MotionTag** (corrected 2026-10-07 — the earlier pass read the factory case order off by one; type 1 is the FMOD sound group); payload is the 0x188 stream; the group walk + tool enumerate which tanis carry it. | §3.1 |
| 4 | "`[+0x1F8]` is a server move-record flag driving the 轻功 End" | **VERIFIED (HD+EXP):** written by sync flag bit0 and by queue record byte+0x1A; consumed by the applier's End-phase guard. | §2.1-2.2 |
| 5 | "SkillMove start `0x14031C4A0`, per-frame `0x140315390`" | **VERIFIED + REFINED:** per-frame applier is `KCharacter::OnSkillMove 0x140314DE0`; `0x1403153A0` is `OnSpecialSkillMove`. RVAs move in EXP (§7). | §1.1, §7 |
| 6 | "SkillMove.tab rows hold per-frame keyframes" | **VERIFIED** incl. the blend `(255−w)/255`, `Column` end frame, clamps. | §1.2, §1.5 |
| 7 | "Client self-drives the 轻功 chain / no local driver" | **VERIFIED:** the chain's End phase is record-driven (`[+0x1F8]==0` from server records); a host must author it. | §2.2-2.3 |
| 8 | `JX3_GRAVITY_RESEARCH.md` §3.10 "heading is a byte angle delta" | **VERIFIED + convention added:** π/128; 0 = +X, 64 = +Y by the client atan2 0x14020ED10 + sin table 0x140A0DEA0. | §1.1, §1.5 |
| 9 | Root-motion whitelist claim (V19/V20) | **VERIFIED (carried):** F1 skill/jump clips not whitelisted → no actor root translation. | 3_2_3_3 |
| 10 | ObjSlotInfo/SkillID→move mapping | **NEW:** `settings/ObjSlotInfo.tab` cols `SlotID/OffsetDistance/OffsetDirection/OutSkillMoveID/OutOffsetFaceDirection/OwnerMask/SubordinateMask`; 1-6-0 store: all OutSkillMoveID=0. The host needs its own map. | §1.1, §2.3 |
| 11 | `.tani` "5 pss paths" etc. | **VERIFIED:** group0 = SFX group (type 0, count 5). | §3.1 |

---

## 7. Evidence index — RVAs per module/build

**HD `JX3ClientX64.exe` (1-5-0-9975, base 0x140000000):**

| Symbol | HD RVA | EXP RVA (1-6-0) | Conf |
|---|---|---|---|
| `KCharacter::SkillMove` | 0x14031C4A0 | 0x14034BC40 | HIGH |
| `KCharacter::OnSkillMove` | 0x140314DE0 | 0x140342360 | HIGH |
| `KCharacter::SpecialSkillMove` | 0x14031C820 | 0x14034C3E0 | HIGH |
| `KCharacter::OnSpecialSkillMove` (chunk) | 0x1403153A0 | 0x1403428C0 (assert fn) | HIGH |
| `KPlayerClient::OnAdjustPlayerMove` | 0x14013AAA9 | 0x1401346B0 | HIGH |
| `KGJumpList::GetSkillMoveSetting` | 0x1403A2490 | 0x1403DC430 | HIGH |
| Move-record push handler | 0x140140AA0 | not pinned | MED |
| Record applier (player) | 0x140182470 | not pinned | HIGH |
| Record applier (alt, exact-frame) | 0x140362DA8 | not pinned | HIGH |
| Master update (in-frame applier) | 0x140310F50 | 0x14033BDA0 (twin frame-update, 1_movement) | HIGH (RVA) / MED (queue block inside) |
| Packed move-word appliers | 0x14016ABE0 / 0x14016AEF0 / 0x14016AD70 | not pinned | HIGH |
| Arc trig helpers | 0x14020ED10 (atan2), 0x14020EE10 (sin Q12), table 0x140A0DEA0 | 0x140222C50 / 0x140222D50 (atan2/sin per 1_movement) | HIGH/MED |
| `MoveTo` (teleport) | 0x140314230 | not pinned | HIGH |
| Turn-in-place | 0x14031BFE0 | not pinned | MED |
| SkillMove finisher | 0x14031C6D0 | not pinned | HIGH |
| Skill executor (slot path) | 0x14030F6B0 (`KCharacter::ClearSlotInfo`) | not pinned | HIGH |

**Engine `KG3DEngineX64.dll` (identical HD/EXP, base 0x180000000):**
`KG3DMotionTagData::LoadFromFile` 0x180299AC0; `KG3DAnimationMotionTag::Helper_Apply` 0x180287320;
per-type handler table 0x1808A27D0; tag time search 0x18028F320; tag fetch 0x1802994E0.

**`KG3D_AnimationTagX64.dll` (EXP 4F3434…; container decode):**
`KG3D_AnimationTani_Data::LoadFromFile` 0x18001D260; `_NewTagData` factory 0x18001DB60;
MotionTag group ctor 0x18000D160 (vtable 0x180048AC0); `KG3D_AnimationMotionTag_Group_Data::
LoadFromFile` 0x180003000; SFX group ctor 0x18001DB9B (vtable 0x180048CC8);
`KG3D_AnimationSFXTag_Group_Data::LoadFromFile` 0x180004AB0; factory type sizes 0x298/0x160/0x508/
0x130/0x58D8/0x1B0 (types 0..5).

**Tables (EXP store, official extractor, sha256):**
`settings/SkillMove.tab` `e319ac1e…` (952,312 B, 935 rows; 1-5-0 `5fce86a3…`, rows 1..952 byte-same);
`settings/ObjSlotInfo.tab` `af4a6ef0…` (2,626 B, all move IDs 0);
`Represent/player/player_skill_move_animation.txt` (SkillMoveID→AnimationID, 435 lines);
`proof/gravity/{player_rush_skill,skill_rush_state}.txt` (represent visuals).

**Host-source anchors (for the fix):** `client/CameraSystem.cs:407` (dead-zone drag),
`:516-531` (`FollowYaw`); `client/RebornClient.cs:3550/3568` (follow feeds);
`ability_sandbox/rb/RebornClient.cs:1986` (naive `curYaw = atan2(travel)` during a pull).

---

## 8. Reproduce

```powershell
$py  = "C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
$s   = "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\exp16"      # helper scripts
$o   = "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x"     # this spec's evidence
$hd  = "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"
$exp = "C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64\JX3ClientX64.exe"

# skill-move chain (HD dumps already in $o)
& $py "$s\fn_at.py"   $hd 0x14031C4A0 0x140314DE0 0x14013AAA9 0x140140AA0 0x140182470 0x140362DA8
& $py "$s\dump2.py"   $hd 0x14031C4A0 0x14031C6CB     # KCharacter::SkillMove
& $py "$s\dump2.py"   $hd 0x140314DE0 0x140315200     # KCharacter::OnSkillMove
& $py "$s\dump2.py"   $hd 0x14013AAA9 0x14013AD10     # OnAdjustPlayerMove
& $py "$s\dump2.py"   $hd 0x14016ABE0 0x14016AC60 0x14016AEF0 0x14016AF60
& $py "$s\find_calls.py" $hd 0x14031C4A0 0x140140AA0
# EXP RVAs
& $py "$s\xall.py"    $exp "KCharacter::SkillMove" "KCharacter::OnSkillMove" "KPlayerClient::OnAdjustPlayerMove"
# tables (official PakV4SfxExtract in the scratch mirror; pathlist is GBK, cwd=bin64)
& "$s\run\client\bin64\PakV4SfxExtract.exe" "$o\pl_slot.txt" "$o\ex_slot"   # ObjSlotInfo + SkillMove
# .tani container
& $py "$s\dump2.py" "C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64\KG3D_AnimationTagX64.dll" 0x18001D27C 0x18001DB00
& $py "$o\parse_tani_groups.py" "C:\Users\Zhibin Ren\Desktop\reborn\proof\netcode\skill_motion\*.tani"
& $py "$o\scan_motiongroup.py"  "C:\Users\Zhibin Ren\Desktop\reborn\proof\netcode\skill_motion\*.tani"
# MotionTag runtime (engine)
& $py "C:\Users\Zhibin Ren\Desktop\reborn\tools\netcode\xref_string.py" "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineX64.dll" "KG3DAnimationMotionTag::Helper_Apply"
```

Read-only: no writes under `C:\SeasunGame`; scratch outputs only; no engine runs.

---

## 9. Open questions + next probes

1. **Exact XY integrator.** Pin the tick function that consumes `[+0x2F8]`/`[+0x26C]` and adds to
   `[+0x10]`/`[+0x14]` (candidates: the master update's per-frame body or the world wrapper
   `KGSO3WorldClientInterface::MoveCharacter` 0x14018BBC0/MoveCtrl 0x14018BE00). Probe: watch-memory
   on a driven dash (RC build) or trace `[+0x2F8]` reads with a value >0 in an emulator.
2. **Skill→SkillMoveID map.** `ObjSlotInfo.tab` is all-zero in the 1-6-0 store; find the live
   producer (server skill config) or the 1-5-0 store's copy (extract from HD PakV4 with a complete
   runtime set). Until then the host owns a hand-built map; register it as host data.
3. **MotionTag type→handler table.** Decode the 12 entries of 0x1808A27D0 (handler pointers +
   this-slots) and dump each handler's first block; map type ids to
   ForceMove/IK/SFX/WeaponMotion/Sound/Camera. (The tanis-with-a-motion-group enumeration is
   DONE via the group walk: `tools/character/motion_tag.py` walks type-2 groups; 8/11 sampled
   F1 tanis clean.)
4. **The float block in `.tani`.** Decode the record around `hash 0x4A8C8588, 0x230, 1, 0, 71, 1,
   floats` (likely a typed tag record; identify type id and consumer).
5. **`skill_mobile/SkillMove.tab`.** A second table loaded by `KGJumpList::GetMaxSkillMoveID`; check
   whether it differs and whether any skill uses it.
6. **Record queue packet name.** Identify the dispatch registration for push handler 0x140140AA0
   (packet id/type) to know which server message carries the 0x20-byte records.
