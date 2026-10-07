# 3.5 Ragdoll / physics bodies + 3.7 Swim / fly / 轻功 — deep verification (2026-10-06)

**Area:** character · **Branch:** research/character-animation · **Index:** `docs/character/README.md`

Scope: verify the existing doc claims against `JX3ClientX64.exe`, `JX3RepresentX64.dll`,
`PhysicsEngineX64.dll`, `SIMWorldX64.dll` (zhcn_hd bin64, 2026-09-27 build) and the extracted
tables in `proof/gravity/`; settle the ragdoll "blend update" question; list true remaining gaps
and host wiring work.

Method: read-only. New disassembly done on copies under
`C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\` (tool: `tools/collision/disasm_range.py`,
`tools/netcode/xref_string.py`, plus scratch scanners `find_calls.py`, `scan_state_stores.py`,
`fn_strings.py`). Game installs untouched.

---

## Verified facts

### 3.5a Ragdoll (KPhysicsRagdoll)

**F1. The four KPhysicsRagdoll methods exist exactly as G-16 says — but at different RVAs than
`recon_physics_funcs.txt` claims.** In `PhysicsEngineX64.dll` (both zhcn_hd and MovieEditor copies,
identical RVAs), assert-name strings and their code xrefs:

| Method | string VA | code xref | enclosing fn (gap heuristic) |
|---|---|---|---|
| `PhysicsEngine::KPhysicsRagdoll::Init` | 0x1800F7398 | 0x180011760 | 0x180011640 |
| `PhysicsEngine::KPhysicsRagdoll::AddPhysicsBone` | 0x1800F73D8 | 0x18000325D | 0x180003070 |
| `PhysicsEngine::KPhysicsRagdoll::AddPhysicsJoint` | 0x1800F7488 | 0x180003527 | 0x1800032E0 |
| `PhysicsEngine::KPhysicsRagdoll::SetPhysicsBoneFilterGroup` | 0x1800F74D0 | 0x1800037DA | 0x180003770 |

Confidence HIGH (raw string scan + xref + disasm prologues for Init and AddPhysicsBone).
`engine_host_spike/recon_physics_funcs.txt:7` lists AddPhysicsBone at `0x180002AE0`; that address
is a destructor-like routine (sets a vtable at [rcx], frees three vectors; disasm
`disasm_pe_2A00.txt`) and is **not** AddPhysicsBone. Correction, not a build difference (both
DLL copies agree).

**F2. Ragdoll = PhysX articulations, as G-16 claims.** `PhysicsEngineX64.dll` contains the
PhysX articulation surface: `PxArticulation`, `PxArticulationLink`, `PxArticulationJoint`,
`PxCreateBasePhysics`, `PxRegisterArticulations`, plus RTTI names
`?AVKPhysicsRagdoll@PhysicsEngine@@`, `?AUIPhysicsRagdoll@PhysicsEngine@@`,
`?AVPhysicsActorProxy@PhysicsEngine@@`. `KPhysicsRagdoll::Init` (0x180011640) allocates a 0xA0-byte
object, installs a vtable, sets up a bone-pointer vector (+0x58) and a name→bone hash (+0x18), and
calls a 16-slot reserve. `AddPhysicsBone` (0x180003070) builds a body-creation option struct, calls
`[proxy+0x98]` on `[this+0x10]` (the `PhysicsActorProxy` layer), asserts `option.IsValid()`,
`nIndex >= 0 && nIndex < m_vecPhysicsBoneBodyInstance.size()`, sets density 0.5
(`movss xmm1, =0.5; call [rax+0xE0]`), stores the body in the bone vector, and registers the bone
index (`[body+0x30] = index`). Confidence HIGH for the symbol set/articulation registration;
MED for the per-instruction reading (no symbols).

**F3. No `KPhysicsRagdoll::Update` symbol; lifecycle is a deadline + manager sweep, not an update
method.** Only four KPhysicsRagdoll names exist in the module. The Represent-side activation sets a
deadline and starts the ragdoll; a separate predicate checks expiry and a manager destroys finished
ragdolls:
- Activation `JX3RepresentX64.dll 0x1802F9540` (CONFIRMED, disasm `disasm_rep_2F9400.txt`):
  config via thunk 0x18001129D → 0x180338BF0; gate `bt ebx,0x1E`; entity via 0x18001C869; component
  flag `test [entity+0x30], 0x2000000`; current time `[timer+0x80]` → `[entity+0x74B8]`;
  `call 0x18000FDE9` (thunk → 0x1803556C0) with `(entity+0x38, 0x40, 1)`;
  `movss xmm1,[cfg+0x22C]` (RagdollTime) → `call 0x1800236DC` (thunk → **0x180542440**).
- `0x180542440` (called with `entity-0x70`, RagdollTime, 0): `deadline = now + RagdollTime`
  stored at `[this+0x8308]`, then `call 0x18001F942(this+0xA8, 0)` (start).
- Predicate `0x180540BD0`: reads `[this+0x8308]`, returns "expired" when `now > deadline` (also
  checks a second deadline at +0x60). Called from a manager loop at 0x180547130 that removes and
  frees the entry when the predicate is true (disasm `disasm_rep_5470a0.txt`).
- Config loader `proof/gravity/disasm/ragdoll.txt:995-1013`: `+0x228` SkillTurningTime, `+0x22C`
  RagdollTime, `+0x230` RagdollBlendWeight; values `RagdollTime=5000`, `RagdollBlendWeight=1.0`
  (`proof/gravity/number.krl.txt:145-146`). CONFIRMED.
- **Blend update loop: still not traced.** `RagdollBlendWeight` is only seen at the config
  load/store sites. A one-line getter `movss xmm0,[rcx+0x230]; ret` exists at 0x180611050 (thunk
  0x18000DB25) with 4 call sites in large animation-update functions (0x180501FBA, 0x180532555,
  0x180534E82, 0x180544B4E) — consistent with a per-frame blend consumer but not proven (MED).
  The physics solve itself is the PhysX scene step (articulations registered in the module).

**F4. Body presets CONFIRMED.** `proof/gravity/physic_character_param.krl.txt`: 4 body groups
(`1`, `2`, `5`, `6` = stander male/female, little boy/girl), each `count = "11"`; sockets
`bip01 l/r foretwist`, `upperarm`, `calf`, `thigh`, `foot` (+pelvis as the 11th); radii 5–8 u,
lengths 10–12 u; header flags `EnableCharacterStatic=1`, `DestructibleRadius=20`,
`DestructibleDamage=100`. Confidence HIGH.

**F5. SIMWorld has NO ragdoll/articulation API.** `proof/gravity/SIMWorldX64_exports.txt` (66
exports, `PxWorld`): actors/rays/floor height/force/velocity/conveyors/summit only; raw scan finds
no `Ragdoll`/`Articulation` string in `SIMWorldX64.dll`. Ragdoll lives one layer lower, in
`PhysicsEngineX64.dll`. Confidence HIGH.

**F6. The two config switches are real engine-adapter keys.** `KG3DEngineAdapterX64.dll`:
`bEnableRagdoll` @ 0x18029F228, `bAddPlayerPhysicsActor` @ 0x18029F238, both read by
`KG3D_LoadJX3Config_From_DX9` (fn 0x18005F9F0) from `config.ini` `[KG3DENGINE]` with default 1
(`disasm_adapter_656b0.txt`). `C:\SeasunGame\MovieEditor\config.ini` has `[KG3DENGINE]` but does
not set either key (no override; default 1 applies). Confidence HIGH for the keys/default; the
"host = 0" wording in the docs is about the host never creating a player physics actor, not about a
config value (see Claim table).

### 3.5b / 3.7 Swim

**S1. `SwimTo 0x14031D770` CONFIRMED** (proof `swim_to.txt`): state gate `state-6 <= 1` (states 6/7
only); shared path check 0x1403105A0; clears `+0x174/+0x210/+0xC04`; sets `+0x204=1`; sets
`+0x1F4=7` at 0x14031D895 (same-position branch) **or `+0x1F4=6` at 0x14031DA81** (moving branch).
Doc "sets state 7" is half-right (CORRECTED below). Tail-jumps into 0x14036BCB0 (display notify).
Confidence HIGH.

**S2. `GetWaterline 0x140312400` CONFIRMED** (proof `get_waterline.txt`): height `[+0x2FC]`,
`6h/20` = 0.30h for non-players (magic 0x66666667), `6h*11/112` ≈ 0.589h for players (bit 30 set;
magic 0x92492493). Submersion helper `0x140312440` CONFIRMED: requires `[char+0x50]` cell, cell
flag bit0, `cellTop = word[cell+6]<<6`, `ground = word[cell+4]<<6`, returns
`cellTop - max(ground, y)` if `y <= cellTop` else 0. Confidence HIGH.

**S3. Water height source: the logic waterline comes from the terrain cell, not scene blocks.**
`0x140312440` reads `word[cell+6]<<6` as the water surface and `word[cell+4]<<6` as the base, gated
by cell flag bit0. The scene-block/`_Water.mesh` claim (G-2/G-24) is about the water
render/collision data; the two are consistent but no doc currently states the cell source.
Confidence HIGH for the cell read; the cell flag semantics beyond bit0 are not decoded (MED).

**S4. The real per-frame swim/water handler is `KCharacter::ProcessVerticalMove` (fn 0x140318C50),
NOT 0x14031B640.** `ProcessVerticalMove` is the shared per-frame vertical/state integrator: it
samples the submersion helper (0x1403197CE, 0x140319894), dispatches on move state (jump table at
0x140319941), and performs the water transitions:
- state 6 (swim) writes at 0x140319A71; state 7 writes at 0x140319B60, 0x140319C7B, 0x140319E13;
  state 4 on exiting water at 0x140319D61/0x140319FA5; fly re-entry 0x20/0x1F at 0x14031A027/
  0x14031A052 (altitude > 0x100 above the surface while airborne);
- fly/bird transitions at 0x140318F91 (0x24), 0x140319029 (0x20), 0x140319063 (0x23),
  0x1403190DF (0x1F), selected by the vertical delta vs the cell top and the current state.
`ProcessVerticalMove` has exactly **one** direct call site: 0x140317FA1, inside the master update
function that starts at 0x1403175B0 (assert name `KCharacter::ProcessNavAutoFly`; no 4-byte CC gap
before the call, first gap at 0x140317FCC). Submersion depth is also consumed by
`ProcessAcceleration` (0x1403165E4) and `ProcessDropSpeed` (0x140316FF9/0x140317044) — i.e.
"swim/dive states flow through the same integrator" is CONFIRMED. Confidence HIGH.
`0x140318C50` is the true entry (docs right); the proof file's gap heuristic
(`process_vertical_move.txt:4` "fn start 0x140318b80") is wrong (0x140318B80 is a small separate
function).

**S5. G-15's "per-frame state handler at 0x14031B640" is a misidentification.**
`0x14031B640` is inside `KCharacter::RunTo` (fn start 0x14031B540): the branch for move state 4
with zero fixed XY velocity samples `GetWaterline` twice (0x14031B69D, 0x14031B6AD), scales the
first by `<<4`, clamps to 0x7FF/0x7F, and computes the packed cell slope `(cell>>1)&7` in the
ground branch (0x14031B8F0). That is the shared airborne/ground move-to default velocity, not a
swim step. The same two-sample pattern also exists in `WalkTo` (0x1403210A6/0x1403210C9) and once
in `Jump` (0x140313810). Confidence HIGH.

**S6. The "per-frame swim step 0x140327A80" of REBORN_JUMP_FALL_SPEC row 11 is wrong.**
`0x140327A80` asserts `KQuestList::UpdateNpcQuestMark` / `nQuestID >= -1 && nQuestID <
MAX_QUEST_COUNT` (strings at 0x1408306F8/0x140830718) and is called from 28 sites across character,
server and UI code — it is the quest-mark update helper, exactly as the later correction in
`JX3_GRAVITY_RESEARCH.md:466-469` says. Confidence HIGH.

**S7. Swim jump.** Logic state **8** is set by a small function at 0x14031C400 (0x14031C45C);
`JX3_CHARACTER_MOVEMENT_RESEARCH.md:248-249` already treats state 8 as swim. Represent owns the
`SWIM` / `SWIM_JUMP` / `SWIM_DOUBLE_JUMP` names (JX3RepresentX64 strings 0x00CBCF94/A0/B0) and
`pcmsOnSwim`/`pcmsOnSwimJump` (0x00CFE3A0/0x00CFE388); the logic exe only has the script ops
`ON_SWIM`/`ON_SWIM_JUMP` (0x007DE3B0/0x007DE3A0) and `KPlayerClient::OnChangeCharacterSwimLineCof`
(0x007C6600). No `SWIM_DOUBLE_JUMP` logic state exists. Confidence MED-HIGH.

**S8. Swim speed.** `CharacterSwimSpeed=20` exists only in the Represent config
`proof/gravity/number.krl.txt:22`; no `CharacterSwimSpeed` string in the logic exe. The logic moves
the character via MoveTo/RunTo (destination + server speed); the 20 figure is the config value to
use for a host. Confidence MED.

### 3.7a 轻功 chain

**C1. Data CONFIRMED.** `tools/gravity/parse_jump_tables.py --chain`: school 0 `MaxJumpCount=4`,
J0 `40,90,11`, J1 `30,300,20`, J2 `50,400,20`, J3 `100,-250,8`; End triples school 0 `60,90,11`,
schools 1–22 `125,-140,12`. School row select via WeaponMask CONFIRMED at `0x14030BA9A`:
`mask = [g+0x26110 + [char+0x1EB2C]*4]` (weapon index), `bt mask, [weapon+0x14]`; bit clear →
`[char+0x138] = 0`. Confidence HIGH.

**C2. `ModifySprintEndSpeed 0x1403140A0` has exactly two call sites (0x140182874, 0x14036317A)
and both guards are now decoded.** At both sites:
```
state ([+0x1F4]) == 4 or 0x1A
[+0x1F8] == 0
[+0x330] (jump count) >= 1
→ call ModifySprintEndSpeed (End triple of the current row)
→ [+0xC08] = 0 ; [+0x330] = 1
```
(`disasm_182700.txt:0x14018284F-0x140182880`, `disasm_363080.txt:0x140363156-0x140363185`.)
Immediately before, the same functions set `[+0x1F8] = (recordByte[+0x1A] == 1)` from a move
record, and call the sprint finisher 0x14031C800 when `[+0x2F4] != 0`. The enclosing functions
(fn starts 0x140182470 and ~0x140362960) are large move-record appliers (hash-map record access,
per-record fields applied to +0x300/+0x304/+0x328/+0x32C). **There is no local frame counter that
advances the chain** — the End phase is applied when the record's flag byte says the segment ended.
Confidence HIGH for the guard/apply; MED-HIGH for the "record byte is the driver" reading (the
record producer was not traced this session).

**C3. `JumpFrameParam` per-frame curve application** (0x14031AFD6 per docs §3.4) not re-verified
here; the storage/stride claims remain doc-level (MED).

### 3.7b Fly / suspend

**FL1. `FlyTo 0x140310B70` CONFIRMED** (proof `fly_to.txt`): valid states `state-0x1F <= 1` (0x1F
and 0x20); pathing check 0x1403105A0; one branch sets `0x20` (0x140310C9A), the other sets `0x1F`
(0x140310E0A). Confidence HIGH.

**FL2. `EndFlyJump 0x140310960` CONFIRMED** (proof `end_fly_jump.txt`): requires state 0x21
(assert `m_eMoveState == cmsOnFlyJump`); sets state 4 (0x1403109D8) or 0xE (0x140310A75). The
"back to 0x21 at 0x140310B06" in the docs belongs to a **separate function starting 0x140310AB0**
(begin-fly-jump; zeroes +0x268/+0x2F8/+0x270 and sets 0x21) — CORRECTED. Confidence HIGH.

**FL3. `player_suspend.krl.txt` CONFIRMED.** 48 body entries; keys `StayAnimaiton`,
`FrontAnimaiton`, `LeftFront/Left/LeftRear`, `Rear`, `RightRear/Right/RightFront`,
`FallDownHeightFloor/AdjustFloor`, `FallDownHeightWater/AdjustWater`,
`FallFloorAnimation/FallFloorGoForwardAnimation`, `FallWaterAnimation/FallWaterGoForwardAnimation`.
Values: 44 entries 500/50 (floor and water); 4 entries `FallDownHeightFloor=10`,
`AdjustFloor=0`, `FallDownHeightWater=200`. Doc "500/50, some skills 200" CONFIRMED.
`player_flyjump.krl.txt` has per-skill `ChongCiQingGong`/`ZuoQiQingGong` Enter/Crash/Turn/InAir
animation sets. Confidence HIGH.

### 3.7c Bird / auto-fly / parachute

**B1. `BirdFlyTo 0x14030C940` CONFIRMED** (proof `bird_fly_to.txt`): requires cell; valid states
`0x23..0x25`; sets 0x24 (0x14030CA6C) in one branch, 0x23 (0x14030CB8C) in the other; if state is
0x25 it keeps `[+0x270]` (velocity) and skips the reset. Confidence HIGH.

**B2. Auto-fly CONFIRMED** (proof `process_auto_fly.txt`): `ProcessAutoFly 0x140316990` uses track
nodes (`pTrack`/`pCurrentNode`/`pEndNode` asserts), advances via `KCharacter::MoveTo 0x140314230`,
increments `[+0xC08]` per frame (0x140316BAC), resets on missing track and calls 0x14031D650. Its
caller is the master update 0x1403175B0 (call at 0x140317719). Confidence HIGH.

**B3. Parachute flag CONFIRMED as a data-only flag**: two scans for `bOnParachuteFlag` code xrefs
are empty (`proof/gravity/disasm/parachute.txt`, `parachute2.txt`). The string exists in the exe
dump at 0x0084B508. Behaviour is Represent/script-side. Confidence HIGH for "no code xref".

### 3.7d Parkour / sprint dive / wall jump

**P1. `OnParkour 0x140314510` CONFIRMED with a nuance.** 0x140314510 is a small switch on
`[char+0xC70]` (values 1..9, turn/direction adjust). The parkour body is the function at
0x1403145C0: asserts `m_dwSkillMoveID <= 0x80`, fetches settings via 0x1403A2380
(`KGJumpList::GetParkourMoveSetting`), applies per-frame `VelocityXY` (word at row+2, blend weight
`[char+r10*8+0x2D8]`), `VelocityZ` (dword at row+0x144), heading delta (word at row+0x3C4),
clamps 0x7FF/0x7F/±0x800, decrements `[+0xC08]` (0x1403147A4), and sets state 4 (0x140314687).
Confidence HIGH.

**P2. `SprintDash 0x14031CC00` CONFIRMED** (proof `sprint_dash.txt`): gates `[+0x1F8] != 0`,
velocity > 0, rejects states 9..0x16, computes `distance = sqrt(dx²+dy²+dz²)`, per-axis
`velocity = distance/frames` style math, sets `[+0xC08]`, `[+0x270]`, `[+0x268]`, `[+0x26c]`,
destination, and sets state 0x17 (0x14031CEF2). `KCharacter::SprintFlash` (fn 0x14031CF60) sets
state 0x19 (0x14031D143). Confidence HIGH.

**P3. Wall jump** trigger `[char+0x200] != 0` and Wall triple index `4*school+jumpCount`
(§3.5) not re-verified this session (doc-level, MED). Wall hang/drag is Represent-side per docs.

---

## Claim verification

| # | Claim (source) | Verdict | Evidence |
|---|---|---|---|
| 1 | G-16 `KPhysicsRagdoll::{Init,AddPhysicsBone,AddPhysicsJoint,SetPhysicsBoneFilterGroup}` | **CONFIRMED** | 4 assert strings + xrefs in PhysicsEngineX64.dll (F1) |
| 2 | `PhysicsEngine::KPhysicsRagdoll::AddPhysicsBone 0x180002AE0` (JX3_COLLISION_SYSTEM §14.1, recon file) | **CORRECTED** | 0x180002AE0 is a destructor-like fn; AddPhysicsBone assert ref is at 0x18000325D in fn 0x180003070 (F1) |
| 3 | G-16 "solving is PhysX articulation, no separate update symbol" | **CONFIRMED (qualified)** | articulation registration/types present (F2); no Update symbol; deadline+manager lifecycle exists (F3) |
| 4 | G-16 "blend update documented" / spec "blend update not traced" | **UNVERIFIABLE (still open)** | RagdollBlendWeight loaded at cfg+0x230; candidate getter 0x180611050 with 4 animation-update callers, not proven (F3) |
| 5 | Activation `0x1802F9540`, flag `[entity+0x30]&0x2000000`, time `[+0x74B8]`, RagdollTime `cfg+0x22C` | **CONFIRMED** | disasm_rep_2F9400.txt; chain → 0x180542440 → 0x18001F942 (F3) |
| 6 | Config `+0x22C/+0x230`, values 5000/1.0 | **CONFIRMED** | ragdoll.txt:995-1013; number.krl.txt:145-146 (F3) |
| 7 | 11-body presets, 4 groups, radii 5–8, lengths 10–12 | **CONFIRMED** | physic_character_param.krl.txt (F4) |
| 8 | Host `bAddPlayerPhysicsActor=0` (COLLISION_SYSTEM_STATUS §4/§5) | **CORRECTED (wording)** | key exists in KG3DEngineAdapterX64.dll, config default 1, MovieEditor config.ini has no override; "no player actor" is a host wiring fact, not a config value (F6) |
| 9 | `SwimTo 0x14031D770`, valid states 6/7, sets state 7 | **CONFIRMED (corrected detail)** | valid 6/7; sets 7 in the in-place branch, **6** in the moving branch (S1) |
| 10 | `GetWaterline 0x140312400` magic divisors | **CONFIRMED** | 0.30h NPC / 0.589h player (S2) |
| 11 | Submersion helper `0x140312440` cellTop/ground math | **CONFIRMED** | (S2) |
| 12 | Water height from scene blocks / `.WaterData` editor-only | **CLARIFIED** | logic waterline reads terrain cell (flag bit0, word+6/word+4); scene-block claim covers render/collision data (S3) |
| 13 | G-15 "per-frame state handler `0x14031B640`" | **CORRECTED** | 0x14031B640 is inside RunTo 0x14031B540; real handler = ProcessVerticalMove 0x140318C50 (S4, S5) |
| 14 | G-15 "swim/dive states flow through the same integrator" | **CONFIRMED** | submersion calls in ProcessAcceleration/DropSpeed/VerticalMove; state 6/7 writes in ProcessVerticalMove (S4) |
| 15 | G-15 "waterline sampled twice per frame" | **CORRECTED** | twice only in RunTo's airborne branch (0x14031B69D/B6AD) and WalkTo; not per-frame swim (S5) |
| 16 | REBORN spec row 11 "per-frame swim step `0x140327A80` open" | **CORRECTED** | 0x140327A80 = `KQuestList::UpdateNpcQuestMark` (S6) |
| 17 | `SWIM_JUMP`/`SWIM_DOUBLE_JUMP` | **PARTIAL** | Represent state names; logic state 8 set at 0x14031C45C; no logic SWIM_DOUBLE_JUMP (S7) |
| 18 | Swim speed 20 尺/s | **CONFIRMED as config** | number.krl.txt:22, Represent-only (S8) |
| 19 | 轻功 data complete (`JumpParam` rows, WeaponMask 0x14030BA9A, End triples) | **CONFIRMED** | parse_jump_tables --chain; dump_va 0x14030BA9A (C1) |
| 20 | `ModifySprintEndSpeed 0x1403140A0`, guard + `jumpCount := 1` | **CONFIRMED** | both call sites, guard decoded (C2) |
| 21 | "frame-level condition that reaches the two call sites" still open (spec §8.1, DOUBLE_JUMP §5.1) | **CORRECTED** | the condition is decoded and is a move-record flag byte (`[+0x1F8] = record[+0x1A]==1`), not a frame counter; no local driver exists (C2) |
| 22 | `FlyTo 0x140310B70` states 0x20→0x1F | **CONFIRMED** | valid 0x1F/0x20, two branches (FL1) |
| 23 | `EndFlyJump 0x140310960` requires 0x21, sets 4/0xE, "back to 0x21" | **CONFIRMED (corrected address)** | 0x140310B06 belongs to separate fn 0x140310AB0 (FL2) |
| 24 | `player_suspend.krl` Stay/Front/Left/Right, 500/50, some 200 | **CONFIRMED** | 44×500/50; 4× (10/0, water 200) (FL3) |
| 25 | `BirdFlyTo 0x14030C940` states 0x24/0x23 | **CONFIRMED (plus 0x25)** | valid 0x23–0x25; 0x25 keeps velocity (B1) |
| 26 | auto-fly path update `0x140316BAC` | **CONFIRMED** | inc [+0xC08]; MoveTo per node (B2) |
| 27 | `bOnParachuteFlag` has no code xref | **CONFIRMED** | empty scans (B3) |
| 28 | `OnParkour 0x140314510`, state 4, counter, per-frame Vz | **CONFIRMED (body at 0x1403145C0)** | (P1) |
| 29 | `SprintDash 0x14031CC00` state 0x17 + Vz; full state machine open | **CONFIRMED** | (P2); states 0x18/0x19/0x14/0x15 identified but not decoded |
| 30 | Canonical unit 1 u = 1 cm (G-0) vs "1 m = 192 u" (REBORN spec §1:14) | **INCONSISTENT DOCS** | G-0 says 100 u/m (mesh census); DOUBLE_JUMP §3 says the 192 label is a metric artifact; spec §1 line 14 is stale |

---

## True remaining gaps (ranked, next probe each)

1. **轻功 offline phase driver.** The client never self-advances the chain: the End triple is
   applied only when a move record's flag byte (+0x1A → `[+0x1F8]`) says the segment ended (C2).
   Next probe: find the record producer (the functions that fill `record+0x1A..+0x1C`; candidates
   are the callers of 0x140182470 / 0x140362960: 0x1401744D3, 0x14017866A, 0x1403D500B) and decide
   whether a client-side timer exists anywhere; otherwise the host must define the segment length
   (e.g. `JumpFrameParam.TotalFrame`) as a registered provisional rule.
2. **Swim motion integration specifics.** How the logic applies `CharacterSwimSpeed` (input path
   speed arg / server sync) and the full swim-jump (state 8, 0x14031C400) transition + velocity.
   Next probe: disasm 0x14031C400..0x14031C4A0 and the input → `MoveTo` call chain for swim states
   (0x14031D650 is called on auto-fly reset; find the swim MoveTo caller).
3. **Cell water semantics.** Which terrain-cell flag/word is water vs terrain ceiling; how the
   engine builds it (`KG3DSceneBlockData::UnCompressWaterData` / `_FillWaterData`). Next probe:
   trace writers of `word[cell+4/6]` and flag bit0 in the terrain loader (KG3DEngineX64) and
   compare with `_Water.mesh` instances on 龙门寻宝.
4. **Ragdoll blend application.** Whether the 4 getter call sites (0x180501FBA, 0x180532555,
   0x180534E82, 0x180544B4E) consume `cfg+0x230` as the animation→physics blend and what the
   0x18001F942 start does (sets articulation poses? rigid bodies?). Next probe: disasm 0x18001F942
   and one caller block end-to-end; check `IPhysicsRagdoll` vtable in PhysicsEngineX64.
5. **Fly/suspend semantics.** `FlyTo` args (destination vs frame count), what separates 0x1F
   (float) from 0x20 (rise?), and the 4 vs 0xE branches of `EndFlyJump`. Next probe: disasm
   0x140310AB0..0x140310B70 (begin fly jump) and the Lua wrappers `LuaFlyJump` (0x1403E0F60
   region) / `LuaEndFlyJump`.
6. **Auto-fly / bird path data.** Track/node record layout and per-node timing; what enables
   auto-fly (nav data is server-side per G-25). Next probe: ProcessAutoFly call-site context
   0x140317719 and the track structures at `[char+0x3B4/0x3B8/0x3BC]`.
7. **Parkour `[+0xC70]` switch (1..9) + ParkourMove row layout.** Next probe: switch targets of
   0x140314510 and a parse of `ParkourMove.tab` (already extracted, 84 KB).
8. **Sprint state machine tail.** States 0x18 (0x14031582D), 0x14/0x15 (0x14031AEB9,
   0x14031B2ED, 0x14031D72F), and end conditions of 0x17/0x19. Next probe: disasm the functions
   containing those writes and `KCharacter::HostSprintDash` (Lua-facing).
9. **Wall hang.** `bHangFlag` (0x0084CF40) consumers and Represent `KRLRushState::BeginStrollOnSlope`
   (doc-level, not re-verified).

---

## Host wiring notes (what to build in the Reborn client)

**Ragdoll (currently MISSING in host).**
- There is no SIMWorld path: `SIMWorldX64.dll` exports no ragdoll/articulation API (F5). The
  native path is `PhysicsEngineX64.dll` (`KPhysicsRagdoll::Init/AddPhysicsBone/AddPhysicsJoint/
  SetPhysicsBoneFilterGroup` + PhysX articulation registration), driven by the Represent activation
  (0x1802F9540 → 0x180542440 deadline → 0x18001F942 start) and swept by the deadline predicate
  0x180540BD0.
- Minimum viable wiring order: (1) set `bEnableRagdoll=1` / `bAddPlayerPhysicsActor=1` in the
  engine adapter config or create the player physics actor; (2) on death/ragdoll trigger, call the
  Represent activation with RagdollTime; (3) build the 11 bodies per body type from
  `physic_character_param.krl.txt` (sockets → bone transforms, radius/length, density 0.5);
  (4) add joints and a filter group; (5) sweep with the deadline and blend out with
  RagdollBlendWeight (value available; mechanism untraced).
- If direct PhysicsEngine calls are out of reach, the only honest fallback is a **registered
  provisional host ragdoll proxy** (no native path recovered), re-open when the PhysicsEngine
  proxy layer (`PhysicsActorProxy`) is usable. Do not present a proxy as the game's ragdoll.

**Swim.**
- Water volumes: use the terrain cell data (flag bit0; surface `word[cell+6]<<6`, base
  `word[cell+4]<<6`) — this is what the client logic samples (S2/S3); scene-block `_Water.mesh`
  data is for rendering/collision visuals.
- Waterline: `0.589 * height` for players (magic math), `0.30 * height` for NPCs; submersion
  depth = `cellTop - max(ground, y)`.
- States: 6/7 entry via `SwimTo` semantics (6 while moving, 7 in place; both clear the standard
  flags), state 8 for swim jump (0x14031C400, MED); speed `CharacterSwimSpeed=20` (number.krl) —
  apply it in the host's 15 Hz integrator when the destination speed arg is absent.
- Landing: water branch thresholds 500/50 (some skills 10/0, water 200) with
  `FallWater*Animation` names from `player_suspend.krl.txt`; swim clips
  `SwimStand/Forward/Backward/Left/Right` (Represent strings 0x00CD8A40+).

**轻功 chain.**
- Port `JumpParam.tab` rows (takeoff + `…End`, Wall/Horse/Dash) and `JumpFrameParam.tab` curves
  per the spec; on each press apply the takeoff triple of the current jump count (guards: state ∈
  {4,0x1A}, `[+0x1F8]==0`, `jumpCount < MaxJumpCount`).
- End phase: apply the `…End` triple when the segment ends and set `jumpCount := 1` (C2). Because
  the client's trigger is a server move-record flag, the host must define a segment length — use
  `JumpFrameParam.TotalFrame` (data) or a tuned value; label it **provisional** with re-open
  criteria (server record producer traced) per AGENTS §6. Do not present a made-up arc as the
  game's.
- `jumpCount := 1` (not 0) after the End phase is essential to match the chain semantics.

**Fly / bird / auto-fly.**
- `FlyTo` (0x1F/0x20) + `EndFlyJump` (0x21 → 4/0xE) state machine; begin-fly-jump at 0x140310AB0;
  `BirdFlyTo` (0x23–0x25, 0x25 keeps Vz); auto-fly = per-node `MoveTo` with `[+0xC08]` node
  counter; suspend animation sets from `player_suspend.krl.txt` (Stay/Front/Left/Right/Rear).
- All of these are state/data complete; the host needs the 15 Hz integrator port (G-14) first so
  the states actually behave like the game.

---

## Corrections to existing docs

1. `JX3_COLLISION_SYSTEM.md` G-15 row: replace `0x14031B640` with
   `KCharacter::ProcessVerticalMove 0x140318C50` (state dispatch + submersion + state 6/7 writes);
   note the `<<4`/127 clamps and `(cell>>1)&7` evidence came from `RunTo` (0x14031B540), not swim.
2. `JX3_COLLISION_SYSTEM.md` §13.3 last bullet ("per-frame swim step not decoded") is stale — the
   water handling is in ProcessVerticalMove; also fix `process_vertical_move.txt` fn-start citation
   (0x140318C50, not 0x140318B80).
3. `REBORN_JUMP_FALL_SPEC.md` row 11 / §8 line 150: delete "per-frame swim step 0x140327A80 open";
   that address is `KQuestList::UpdateNpcQuestMark`. Swim status can move from PARTIAL to
   "decoded at rule level, host not wired".
4. `REBORN_JUMP_FALL_SPEC.md` §8.1 + `JX3_DOUBLE_JUMP_RESEARCH.md` §5.1: the phase trigger is no
   longer "undecoded frame condition" — it is a server move-record flag (`[+0x1F8]` from record
   byte +0x1A) applied in the record appliers at 0x140182874 / 0x14036317A. The remaining work is
   the offline segment-length policy, not a missing function.
5. `JX3_COLLISION_SYSTEM.md` §14.1: `KPhysicsRagdoll::AddPhysicsBone` is at fn `0x180003070`
   (string xref 0x18000325D) in the shipped `PhysicsEngineX64.dll`; `0x180002AE0` (recon file) is a
   different routine. Same correction applies to `engine_host_spike/recon_physics_funcs.txt:7`.
6. `JX3_COLLISION_SYSTEM.md` G-16: keep SOLVED for symbols/articulation, but state explicitly that
   the Represent deadline sweep (0x180540BD0 predicate, manager 0x180547130) is the lifecycle
   logic, and the `RagdollBlendWeight` consumer is still untraced (matches spec row 29).
7. `COLLISION_SYSTEM_STATUS.md` §4/§5 "`bAddPlayerPhysicsActor=0`": the key is an adapter config
   default **1** (KG3DEngineAdapterX64 `config.ini [KG3DENGINE]`); the host's lack of a player
   physics actor is a wiring fact. Reword to avoid implying a config value.
8. `JX3_GRAVITY_RESEARCH.md` §3.8: "`EndFlyJump` … then back to 0x21 (0x140310B06)" — 0x140310B06
   is in the separate function 0x140310AB0. `FlyTo` valid states are 0x1F/0x20 (not a single path).
9. `REBORN_JUMP_FALL_SPEC.md` §1: "1 m = 192 units" conflicts with G-0 ("1 u = 1 cm"); mark the
   192 label stale and keep integer physics unit-agnostic (affects only metric annotations).
10. `JX3_COLLISION_SYSTEM.md` §13.3 "SwimTo … sets state 7": also sets state 6 (moving branch,
    0x14031DA81).

---

## Reproduce (scratch, read-only)

```powershell
# copies (scratch)
$S="C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x"
# KPhysicsRagdoll symbols + xrefs
.venv\Scripts\python.exe tools\netcode\xref_string.py "$S\PhysicsEngineX64.dll" "KPhysicsRagdoll" --after 0 --all
# activation / deadline / manager
.venv\Scripts\python.exe tools\collision\disasm_range.py "$S\JX3RepresentX64.dll" 2f9400 400 "$S\rep_2F9400.txt"
.venv\Scripts\python.exe tools\collision\disasm_range.py "$S\JX3RepresentX64.dll" 540b80 200 "$S\rep_540b80.txt"
# swim handler (RunTo) and real integrator
.venv\Scripts\python.exe tools\collision\disasm_range.py "$S\JX3ClientX64.exe" 31b540 900 "$S\runto.txt"
.venv\Scripts\python.exe tools\collision\disasm_range.py "$S\JX3ClientX64.exe" 318c50 400 "$S\pvm.txt"
# chain guards
.venv\Scripts\python.exe tools\collision\disasm_range.py "$S\JX3ClientX64.exe" 182700 400 "$S\sync1.txt"
.venv\Scripts\python.exe tools\collision\disasm_range.py "$S\JX3ClientX64.exe" 363080 300 "$S\sync2.txt"
# data
.venv\Scripts\python.exe tools\gravity\parse_jump_tables.py --chain
```

All new artifacts referenced above live in the scratch dir; nothing in the repo or the game
installs was modified.

## Host implementation (W6, 2026-10-06, branch `agent/3x-states`)

Implemented in the client on top of the decoded rules above:

- **Swim** (`client/WaterField.cs` + `RebornClient.cs`): states 6 (moving) / 7 (in
  place) / 8 (swim jump); float height = `max(base, surface - 0.589*h)` with the
  player factor `6h*11/112`; `CharacterSwimSpeed` applied directly at 20 u/frame
  -> 300 u/s (the logic moves via MoveTo/server speed); water landing thresholds
  (500/50) stay in the existing landing branch; swim clips `F1b02yd游泳...`
  (9 F1 rows in `player_animation_use_originroot_trans.txt`).
- **轻功 chain End phase**: `JumpTable.EndTriples` + `TotalFrame` generated from
  `JumpParam.tab`/`JumpFrameParam.tab`; on segment end the `...End` triple of the
  current segment is applied and `jumpCount := 1` (ModifySprintEndSpeed semantics).
- **Fly/suspend harness** (`RC_SUSPEND_DEMO=1`): logs the decoded state codes
  (`FlyTo` 0x1F, `EndFlyJump` 0x21 -> 4) with a hover harness; the real triggers are
  skill/script driven (named host gap).
- **Test harness**: `RC_DEMO_STATES=1` scripted chain presses; `RC_CHAIN_SEG`,
  `RC_SWIM_LOG`, `RC_CHAIN_LOG`; proof runs in `proof/character/3x_states/`.

REGISTERED PROVISIONALS (AGENTS §6, re-open criteria):
1. **Water source**: the real surface is the logic `m_pCell` stream (GetWaterline
   0x140312440: flag bit0, `word[+6]<<6` surface, `word[+4]<<6` base) - not
   host-reachable (no managed query; `water/regiondata/RegionInfo.json` is a MISS
   in the paks). Host maps `RC_WATER` boxes. Re-open: trace the `m_pCell` writer /
   KMiniScene cell stream, or an engine water-node query.
2. **Swim jump impulse**: state 8 setter 0x14031C400 sets the state but no velocity
   site was found; the host reuses the school J0 takeoff triple. Re-open: trace the
   state-8 velocity writer.
3. **Swim buoyancy**: the game's below-waterline push is not decoded; the host holds
   the float height (clamp up+down). Re-open: decode the PVM below-float branch.
4. **Chain segment length**: client trigger is a server move-record flag; host uses
   `JumpFrameParam.TotalFrame` when shipped (schools 10/11), else `RC_CHAIN_SEG`
   (default 51 ticks, the shipped 51-81 range). Re-open: server record producer.
5. **Suspend hover**: harness only (1.2 s hold); the real 0x1F/0x20/0x21 vertical law
   is not decoded. Re-open: fly-state physics decode.
