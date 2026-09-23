# REBORN — JX3 jump & fall reproduction spec

**Status:** reproduce-ready for the single jump, gravity, fall, landing and
death. The 轻功 multi-jump chain is data-complete but its phase timing still
needs one update function decoded (no live capture).
**Sources:** `docs/JX3_GRAVITY_RESEARCH.md`, tables in `proof/gravity/`,
disasm in `proof/gravity/disasm/`.

## 1. Constants (verified)

| Constant | Value | Source |
|---|---|---|
| Logic tick | **66.7 ms** (15/s) | 11-frame jump animation = 0.733 s FBX; `JumpFrameParam.TotalFrame` |
| Length unit | **1 m = 192 units** (1 尺 = 64) | dash 800 u = 12.5 尺 cross-check (`SKILL_DATA_RESEARCH.md` §6) |
| Velocity unit | **units per logic frame** | `JumpTo`/flash/sprint-end all compute `distance / frames` |
| Gravity unit | units per frame² | `KCharacter+0x320`; `vz = Δz/t + g·t/2` in `JumpTo` |
| XY velocity clamp | **0 … 127 u/f** (0…9.9 m/s) | `0x140313F88` (`0x7F`), flash `0x1403DC0C3` |
| Z velocity clamp | **-2048 … 2047 u/f** (-160…160 m/s) | `0x140313F95` (`-0x800`/`0x7FF`) |
| Gravity clamp | **[0, 31] u/f²** | `KCharacter::Jump` `0x140313BD5` (≤0x1F) / `0x140313BEA` (<0 → 0) |
| Fixed-point XY | `+0x268` = XY×16 | `Jump`/`JumpTo`/sprint-end all `shl …, 4` |

## 2. Data tables

| Table | Content | Extracted |
|---|---|---|
| `settings/JumpParam.tab` | per school: `MaxJumpCount`, `WeaponMask`, per jump `JumpSpeedXY/VelocityZ/Gravity` + `…End`, `Wall*`, `Horse*`, `Dash*`, `KnockedBack*`, `KickRange`, `Flash*`, fly costs | `proof/gravity/JumpParam.tab` |
| `settings/JumpFrameParam.tab` | per (school, jump count, double player): `TotalFrame` + 128 `(Frame, VelocityXY, VelocityZ, DirectionXY)` (DirectionXY = byte × π/128) | `proof/gravity/JumpFrameParam.tab` |
| `settings/Sprint.tab` | per school: `MinVelocityXY/MaxVelocityXY/MinVelocityZ/MaxVelocityZ`, `AniFrame`, `SearchDirection` | `proof/gravity/Sprint.tab` |
| `Represent/player/player_suspend.krl.txt` | per 轻功 skill × body type: 滞空 animations, **`FallDownHeightFloor/AdjustFloor`**, `FallDownHeightWater/AdjustWater`, `FallFloor/FallWater*Animation` | `proof/gravity/player_suspend.krl.txt` |
| `Represent/common/number.krl.txt` | walk/run/swim/ride speeds, camera params | `proof/gravity/number.krl.txt` |
| `settings/SkillMove.tab` | per skill move: `IngoreGravity`, `SkillMoveDeath`, per-frame velocity keyframes | `proof/gravity/SkillMove.tab` |

Parser/replay: `tools/gravity/parse_jump_tables.py` (`--summary`, `--json`).
Numeric verification: `tools/gravity/verify_model.py` → `proof/gravity/verification.txt`.

## 3. Core simulation (per logic frame, exact integer model)

```python
# state: pos, vx, vy, vz (integers, units/frame); jump_count; grounded
# Client functions (disasm proof/gravity/disasm/):
#   ProcessAcceleration 0x1403165D0 - per-frame vz += accel, clamps
#   ProcessVerticalMove 0x140318C50 - y += vz, ground clamp, landing reset
#   ProcessDropSpeed    0x140316BE0 - slope projection / air-stop
def frame():
    global vz
    vz += accel                 # ProcessAcceleration 0x140316912
    vz = clamp(vz, -2048, 2047) # 0x14031694A..65
    pos.y += vz                 # ProcessVerticalMove 0x140318E70
    if pos.y > ground: pos.y = ground          # 0x140318E73
    if within(64, of_cell_top): jump_count = 0 # landing reset 0x14031A25E
    vz = clamp(vz, 0, Sprint.tab.MaxVelocityZ[school] or 2047)  # dive cap

def on_jump_pressed():
    if grounded:  jump_count = 0
    jump_count += 1
    (xy, z, g) = JumpParam[school][jump_count]      # takeoff triple
    vx, vz, gravity[jump_count] = xy, z, clamp(g, 0, 31)
    vxy_fixed = xy << 4                             # char+0x268
    if jump_count > MaxJumpCount[school]: reject

def on_jump_segment_end():                          # ModifySprintEndSpeed 0x1403140A0
    # guard: (move_state == 4 or 0x1A) and [char+0x1F8]==0 and jump_count >= 1
    (xy, z, g) = JumpParam[school][jump_count].End     # End triple of current jump
    vx, vz, gravity = clamp(xy,0,127), clamp(z,-2048,2047), clamp(g,0,31)
    air_frame = 0
    jump_count = 1                                     # reset to 1, not 0
```

Single-jump numbers (school 0, jump 0): `v0 = 90 u/f = 7.03 m/s`,
`g = 11 u/f² = 12.89 m/s²`, apex **1.9–2.2 m** (2.16 m with the frame order
above, 1.92 m continuous), air ≈ 1.1–1.2 s.

## 4. State machine

```
GROUND --(space)--> JUMP (jump_count 1)
JUMP/AIR --(space, jump_count < MaxJumpCount)--> JUMP+1   (轻功 chain)
JUMP/AIR --(walk off ledge / jump ends)--> FALL
FALL --(ground contact)--> LANDING
  landed height diff > FallDownHeightFloor (500 u = 2.6 m) -> roll animation
  landed in water          -> FallWater*Animation
  fall > death threshold (server)                         -> FALL_DEATH move
AIR --(shift/dive in 轻功)--> SPRINT DIVE (vz capped by Sprint.tab)
AIR --(轻功 skill)--> SUSPEND (滞空: Stay/Front/Left/Right/… per player_suspend.krl.txt)
```

## 5. Landing rules (`player_suspend.krl.txt`, per 轻功 skill × body type)

| Key | Typical value | Meaning |
|---|---|---|
| `FallDownHeightFloor` | **500 u (2.6 m)** | min height difference for the roll/landing branch |
| `FallDownAdjustFloor` | 50 u | adjust threshold (landing blend) |
| `FallDownHeightWater` | 500 u (some skills 200) | water variant |
| `FallDownAdjustWater` | 50 u | water adjust |
| `FallFloorAnimation` / `FallFloorGoForwardAnimation` | e.g. `M2b02yd握拳小跳c.ani` | standing / moving landing animation |
| `FallWaterAnimation` / `FallWaterGoForwardAnimation` | e.g. `M2bqg加速跑02c_水_刹车.tani` | water landing |
| `StayAnimaiton` / `FrontAnimaiton` / directional variants | `M2bqg<school>滞空*.ani` | 滞空 loop set |

## 6. Animations

| Situation | Animation | Source |
|---|---|---|
| jump 1/2/3 (跳跃) | `f1b02yd小跳a/b/c.ani`, `跳跃1/2/3.fbx` (0.733/0.667/0.800 s = 11/10/12 ticks) | `proof/gt_mapviewer_locomotion/`, FBX export |
| fall loop | `Fall{Floor|Water}{Idle|GoForward}Animation` from `tabCGAni` (values in `player_suspend.krl.txt`) | §8/§9 of `JX3_GRAVITY_RESEARCH.md` |
| landing roll | `FallFloor*Animation` when height > 500 u | `player_suspend.krl.txt` |
| 二段跳 / 轻功 | `f1b02yd二段跳a.tani`, `player_flyjump.krl.txt` (Enter/Crash/Turn/InAir per skill × body) | extracted |
| fall death | `SkillMove.tab` `SkillMoveDeath=1` moves (25 rows; first = id 2, 50 frames), state `FALL_DEATH` | `proof/gravity/SkillMove.tab` |

## 7. What is already implemented

`engine_host_spike/MapSpike.cs` player mode: continuous gravity/jump
(`-1289 cm/s²`, `703 cm/s`), walk/run 200/667 cm/s, terrain-height collision,
ledge-fall detection, follow camera. It does **not** yet use: the jump chain,
the 500 u roll rule, Sprint dive cap, or the real animation selection.

## 8. Gaps before an exact 轻功 chain

1. **Phase timing**: the phase rule and call sites are now decoded (§3,
   `on_jump_segment_end`): takeoff triple on press, `…End` triple of the
   current jump count applied when `move_state ∈ {4, 0x1A}` & `[+0x1F8]==0` &
   `jump_count ≥ 1`, then `jump_count := 1`. Remaining: the frame-level
   condition that reaches the two call sites (`0x140182874`, `0x14036317A`).
2. **Chain apex anomaly**: raw `VelocityZ` for jump ≥ 1 (300/400/780 u/f)
   yields implausible ballistic apexes (11–78 m) if treated as a full-segment
   parabola; with the decoded phase model the takeoff segment is short and the
   `…End` triple governs the rest, which qualitatively explains the data
   (exact segment lengths still to be measured from the frame condition).
3. Per-character buff modifiers (`GRAVITY_BASE/PERCENT`, `JUMP_SPEED_BASE/PERCENT`)
   — Lua-driven; defaults reproduce the base game.
4. Server death threshold for fall damage (client only plays the death move).

## 9. Completeness audit — what is / isn't reproduced

Legend: **[OK]** decoded + reproducible · **[DATA]** table extracted, behaviour
not decoded · **[SERVER]** authoritative, not in the client · **[NA]**
different subsystem (not jump/fall physics).

| # | Subsystem | Status | Notes |
|---|---|---|---|
| 1 | Single jump ballistics (takeoff triple, gravity, arc) | **[OK]** | §3; verified by `verify_model.py` |
| 2 | Gravity, units, tick, velocity clamps | **[OK]** | §1 + §3 (127 / ±2047 / gravity [0,31]) |
| 3 | Fall integration + dive caps | **[OK]** | `Sprint.tab` + same integrator |
| 4 | Landing roll/water thresholds + animation names | **[OK]** | `player_suspend.krl.txt` 500/50, 200/50 |
| 5 | Fall death move (`FALL_DEATH`) | **[OK]** | `SkillMove.tab` death rows; trigger is [SERVER] |
| 6 | Walk/run/swim/ride speeds | **[OK]** | `number.krl.txt` |
| 7 | 轻功 chain — data (takeoff/End triples, costs, wall/horse/dash, flash) | **[OK]** | `JumpParam.tab`; school row select via `WeaponMask` decoded (`0x14030BA9A`) |
| 8 | 轻功 chain — phase-switch timing | **[OK]** | `ModifySprintEndSpeed` 0x1403140A0 + guards + `jump_count := 1` decoded (§3) |
| 9 | `JumpFrameParam.tab` per-frame curves application | **[OK]** | storage + consumer `0x14031AFD6` decoded: fields, stride 160, DirectionXY as heading delta |
| 10 | Wall jump trigger / wall hang / wall costs | **[PARTIAL]** | trigger `[char+0x200] != 0`, Wall triple idx `4*school+jumpCount` decoded (§3.5); hang/drag logic still open (`bHangFlag`) |
| 11 | Swim physics (`SwimTo`, water line, `GetWaterHeight`, `ValidWaterHeightDiff`, `EnableNewWaterHeight`, `SWIM_JUMP`/`SWIM_DOUBLE_JUMP`) | **[PARTIAL]** | `SwimTo` 0x14031D770 + `GetWaterline` 0x140312400 + submersion 0x140312440 decoded (§3.7); per-frame swim step 0x140327A80 open |
| 12 | Knockback chain (`KNOCK_DOWN/BACK/OFF`, `KnockedBackFrame/Speed`, `KickRange`) | **[SERVER]** | `KnockedBackFrame/Speed` have no client readers (server-driven); `KickRange` read at `0x1403157C8` |
| 13 | Parkour / wall run (`ParkourMove.tab`) | **[PARTIAL]** | `OnParkour` 0x140314510 decoded: state 4, counter `[+0xC08]`, per-frame Vz from settings, clamps |
| 14 | Skill moves / dashes (`SkillMove.tab`, 918 rows) | **[OK]** | start `0x14031C4A0` (state 26/27, counter, fields) + per-frame update `0x140315390` (XY/Z/Direction keyframes, clamps) decoded (§3.10); same pattern as jump curves |
| 15 | Flash / blink movement math (`Flash*`, `SprintFlash*`) | **[OK]** | `distance/frames` + clamps, `0x1403DBDB0` |
| 16 | Sprint dive / slide (`SPRINT_DASH/KICK/BREAK`, `SKID`) | **[PARTIAL]** | `SprintDash` 0x14031CC00 (state 0x17 + Vz) decoded; finisher `0x14031C800`; full state machine open |
| 17 | Suspend / 滞空 (`SUSPEND`, `player_suspend.krl.txt`) | **[PARTIAL]** | fly states decoded: `FlyTo` 0x20→0x1F, `EndFlyJump` 0x21→4/0xE + Vz (0x140310960); animations + fall keys extracted |
| 18 | Float / `RISE` / `FLOAT` / `FLY_FLOAT` | **[PARTIAL]** | fly states decoded (`FlyTo` 0x20→0x1F, `EndFlyJump` 0x21→4/0xE); bird `BirdFlyTo` states 0x24→0x23 (§3.11) |
| 19 | AutoFly (`AUTOFLY`, `ProcessAutoFly`, `PauseAutoFly`) | **[PARTIAL]** | `ProcessAutoFly` 0x140316990: nav path/track (`pTrack`/`pCurrentNode`/`pEndNode`), counter per node, then shared drop processing (§3.11) |
| 20 | Bird (`BIRD_FLY/FLOAT/JUMP`), Summit, MannedSpace (载体), Pull/Repulsed | **[PARTIAL]** | bird states 0x24/0x23 decoded (§3.11); Summit = Represent/script; Pull/Repulsed = script ops `CALL_REPULSED`/`REPULSED_RATE` + states + animation sets, moving via shared velocity fields |
| 21 | Parachute (`bOnParachuteFlag`, `ON_PARACHUTE_FLAG`) | **[PARTIAL]** | `bOnParachuteFlag` has no code xref → data/script/server-driven; Represent glider/animation side (`GliderCamera` config extracted) |
| 22 | Mounted / horse movement + conveyor belts | **[PARTIAL]** | horse jump triple + `Vz += [char+0x34C]`, `jumpCount < 1` decoded (§3.5) |
| 23 | Buff/skill speed & gravity modifiers (`GRAVITY_PERCENT`, `JUMP_SPEED_PERCENT`, …) | **[OK]** | source decoded (§3.12): server character data bytes → `[+0x138]`/`[+0x16C]`/`[+0x170]`; applied as `[+0x16C]/[+0x170] × [+0x40]/100` in `ProcessVerticalMove` |
| 24 | Character collision capsule (slope limit / step offset / skin) | **[PARTIAL]** | scene gravity `(0,−9.81,0)` + controller manager + scale-derived controller params + 20 ms world step decoded (§3.9); exact per-field CCT values still not individually labelled |
| 25 | Terrain slope handling | **[PARTIAL]** | decoded: `ProcessDropSpeed` slope projection + air-stop (`0x140316BE0`), `ProcessVerticalMove` ground clamp/snap (`0x140318C50`); remaining: foot-align rendering keys (`footAlignToSurfaceMaxSlopeAngle`) |
| 26 | Server move sync / reconciliation (`OnSyncMoveParam/State/Ctrl`) | **[SERVER]** | needed only for netcode; prediction model is complete |
| 27 | Fall damage / revive thresholds | **[SERVER]** | client plays death move only |
| 28 | Camera coupling, SFX/VFX, animation blending details | **[NA]** | camera in its own spec; blend keys known (`AnimationBlendTime=190`, `KeepTurningFrame=30`) |
| 29 | Ragdoll (`physic_character_param.krl.txt`, RagdollTime/BlendWeight) | **[PARTIAL]** | config offsets + activation `0x1802F9540` (component flag, time record, `RagdollTime`) decoded (§3.11); blend update not traced |

### Gravity by situation (coverage snapshot)

| Situation | Gravity/vertical law known? | Where |
|---|---|---|
| Ground contact / walking | **Yes** | ground clamp `ProcessVerticalMove` `0x140318E73` |
| Single jump | **Yes** | §3 (takeoff triple, clamps, phase) |
| Multi-jump / 轻功 chain | **Yes** | §3.3 phase + §3.4 curves (counter decrement detail open) |
| Walk off a ledge / free fall | **Yes** | `ProcessAcceleration` + `ProcessDropSpeed` |
| Landing (floor / water) | **Yes** (thresholds/animations) | `player_suspend.krl.txt` 500/50, 200/50 |
| Sprint dive / slide | **Partial** | `SprintDash` 0x14031CC00 sets state 0x17 + Vz; finisher `0x14031C800`; full state machine open |
| Wall jump | **Partial** | trigger/triple §3.5; hang is Represent-side (`bHangFlag`, `KRLRushState::BeginStrollOnSlope`) driven by parkour/skill moves |
| Mounted jump | **Yes** | Horse triple + `Vz += [char+0x34C]`, §3.5 |
| Buff-modified gravity / jump speed | **Application yes, setter open** | `[+0x16C]`/`[+0x170]` × `[+0x40]/100` §3.6 |
| Slope-projected motion | **Partial** | `ProcessDropSpeed`; foot-align config only |
| Knockback / knocked-off parabola | **Server** | client arrays unused; `CALL_KNOCKED_OFF_PARABOLA` script op |
| Skill moves / dashes | **Yes** (pattern decoded) | start `0x14031C4A0` + per-frame `0x140315390`: XY/Z/Direction keyframes + clamps (§3.10) |
| Parkour / wall run | **Partial** | `OnParkour` 0x140314510 decoded (§3.8): state 4, counter, per-frame Vz |
| Suspend / float / rise (滞空) | **Partial** | fly states decoded (`FlyTo` 0x20→0x1F, `EndFlyJump` 0x21→4/0xE) §3.8; animations/keys extracted |
| Auto-fly / bird / parachute | **Partial** | auto-fly path update (counter inc 0x140316BAC); bird states 0x24/0x23 (`BirdFlyTo` 0x14030C940); parachute flag only |
| Flash / blink | **Yes** | `0x1403DBDB0` |
| Swim entry / waterline | **Partial** | `SwimTo` 0x14031D770 (state 7), `GetWaterline` 0x140312400 (0.6·h NPC / 6h/7 player), submersion helper 0x140312440; swim motion uses the shared integrator + `CharacterSwimSpeed` |
| World physics gravity (props, ragdoll) | **Known (−9.81)** | Physics scene created with `(0, −9.81, 0)` default (§3.9); world stepped at fixed 20 ms (50 Hz); characters use their own 15 Hz table gravity |
| Missiles / trajectories | **Different system** | `KParabolaMissileProcessor` (parabola data) |
| CCT collision (slope/step/skin) | **Partial** | scene gravity `(0,−9.81,0)`, controller manager + scale-derived params, 20 ms world step (§3.9); per-field labels open |
| Ragdoll (death physics) | **Partial** | params (`RagdollTime` 5000, `BlendWeight` 1.0 at config `+0x22C/+0x230`) + activation `0x1802F9540` + bodies `physic_character_param.krl.txt` + PhysX world (§3.11) |
| Vehicles / conveyor belts | **Data only** | `SetConveyorBeltParam`, MANNED_SPACE |



### Move-state vocabulary (Represent, `0x00CBCF08`–`0x00CBD3F8`)

`STAND, TURN_LEFT/RIGHT, WALK, SWIM, SWIM_JUMP, SWIM_DOUBLE_JUMP, FLOAT,
FLY_FLOAT, FLY_JUMP(+turns), JUMP, DOUBLE_JUMP, DASH, KNOCK_DOWN, KNOCK_BACK,
KNOCK_OFF, SPRINT_BREAK, HALT, FREEZE, ENTRAP, AUTOFLY, PULL, REPULSED, RISE,
SKID, WALL_JUMP, SPRINT_DASH, SPRINT_KICK, SPRINT_FLASH, SKILL_MOVE_SRC/DST/DEATH,
SUSPEND(+turns), SUMMIT, BIRD_FLY, BIRD_FLOAT, BIRD_JUMP, RUSH, POSE_STATE,
MANNED_SPACE, HAND_LINK, SLOT_LINK, TERRAIN, WATER, SIT_DOWN, DEATH, FALL_DEATH`.

## 10. Verdict

- **Fully reproducible today:** items 1–9, 15, 25 (single jump, gravity, fall,
  per-frame integrator incl. slope projection/ground clamp/landing reset,
  landing incl. roll/water, fall death, speeds, flash math, **the whole 轻功
  chain phase model and the per-frame `JumpFrameParam` curves**) — tables +
  model + animation names, validated numerically.
- **Decoded at rule/data level, remaining wiring:** items 10–11, 13–14,
  16–24 — the rest of the movement kit (wall hang, swim, parkour, individual
  skill moves, sprint/suspend/auto-fly/bird/parachute transitions, buff
  setters, CCT config values).
- **Not reproducible offline (by design):** items 12, 26–27 (server
  authority: knockback motion, move sync, fall damage).
- **Different subsystem:** item 28.

So: **the jump + fall + land loop is complete and the 轻功 chain is now
specified end-to-end**; what remains is the wider movement kit, all of it
located in data/functions, none requiring live capture.



