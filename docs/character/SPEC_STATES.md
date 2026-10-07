# E — STATES spec: swim / fly / suspend / 轻功 / water (JX3 "Reborn")

**Area:** character · **Branch:** agent/3x-integration · **Index:** `docs/character/README.md` · **Status:** redefined spec 2026-10-06 (client-truth, 1-5-0 + 1-6-0 deltas) — supersedes the assumed behaviors in the older docs.

**Deliverable of the STATES research pass.** This document is written for the future
implementation agent; it replaces the "raw 轻功 chain" model currently in
`client/RebornClient.cs` (`RC_DJUMP=chain`) with the client's own truth.

**Truth sources (read-only, nothing written under `C:\SeasunGame`):**

| Source | Build | Notes |
|---|---|---|
| `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe` | **1-5-0-9975** (2026-09-27, 11,349,432 B) | primary (release); all RVAs below unless marked `EXP` |
| `C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64\JX3ClientX64.exe` | **1-6-0-9536** (2026-10-03, 11,823,024 B) | deltas; new RVAs from `%TEMP%\opencode\exp16\1_movement.md` |
| `...\zhcn_hd\...\cache-extraction\pakv4-probe\logic-skill-prefixed-out\settings\skill\{skills.tab,Buff.tab}` | 1-6-era store | GBK TSV, read-only (41,142 skill rows / 61,555 buff rows) |
| Release PakV4 store (scripts), extracted to scratch with the official `PakV4SfxExtract.exe` | 1-5-0 store | `spec3x\ext\out*` — provenance in §Reproduce |
| Repo proofs | — | `proof/gravity/disasm/*`, `docs/movement/*`, `docs/character/*` |
| Sibling research branch (local, unmerged) `agent/3x-states` | — | water-entry AV root cause + W6 host wiring; its findings are cited, and re-derivable from the binaries |

**Method:** capstone disasm via `tools/collision/disasm_range.py` / `tools/netcode/xref_va.py`,
string/table dumps on the exe copies, GBK extraction of the release PakV4 scripts, Buff.tab
parsing. **No engine runs.** Confidence legend: **HIGH** = exact instruction/table read +
consumer; **MED** = one link inferred; **LOW** = unprobed.

---

## 1. WATER / SWIM

### 1.1 Where water actually exists (per-map inventory) — HIGH (data), maps-bounded

Water is **authored per map** in `water\surface\watersurfacelist.json` +
`water\waterinfo.json` / `<map>_waterinfo.json` (plus `wave.json`). All five shipped
maps have it; the release PakV4 store extraction is in
`spec3x\ext\out3\data\source\maps\<map>\water\...` (paths are logical store paths).

| Map | Water bodies (WaterID: type, plane y, x, z, size) |
|---|---|
| **海岛绝境** | **id0 type0 OCEAN: y=0, global (Base 1e9, depth 1e6)**; id1 y=-5382.7 (x54397,z99809, scale 15.36); id3 y=402.4 (x53848,z50362, scale 7.67); id2 y=-1506.0 (x34453,z114423, scale 8.73). Material `gm_sea_ocean1` / `hdjj_bwater1` |
| **龙门寻宝** | 6 × type1: y=150 (x139424,z31167); y=150 (x67040,z58549); y=4738.9 (x178609,z100072); y=-457.7 (x150700,z53040); y=-758.7 (x134208,z47339); y=-463.4 (x180413,z55823). Material `lxxxx_ow_bwater` (the AV repro spot `(64293,-125,55238)` is water in-game; do not assume it is exactly one listed entry — the render surface and the logic cell are different layers) |
| **龙门寻宝_夜晚** | identical to 龙门寻宝 (6 bodies, same coordinates) |
| **白龙绝境** | **id3 type0 OCEAN y=2622.9**; 9 × type1 at y=10223.6/11939/7584.9/6571.9/5343.8/4182.8/3213.2 (lakes x~101-113k,z~74-93k) + 2 spring-shaped (scale 9×18, 6.2×6.8) |
| **天原绝境** | 7 × type1 only (no ocean): y=-3534/-3146/-3434/-2937/16556/6674/18457 at x 22k-449k |

Format fields (`watersurfacelist.json`): `WaterID, WaterType (0=ocean plane, 1=bwater mesh),
Postion[x,y,z], Scale[x,z], RotY, Depth, BaseWidth/Lenght, ClipType, IsLocal, WaterMaterial`.
The surface mesh comes from `water\surface\*.jsonins` (e.g. `waterdefault_bwater1.jsonins`,
`waterdefault_ocean1.jsonins`, `lxxxx_ow_bwater.jsonins`, `hdjj_bwater1.jsonins`).

**The gameplay (logic) water, however, is the terrain cell** (§1.2), not these render
surfaces. The cell water is populated from the per-map logic/mini-scene stream
(`<map>.Map.Logical` / `KMiniScene`), whose writer is **not yet traced** — see §1.6/§6.

### 1.2 Cell semantics, waterline, submersion math — HIGH

`KCharacter` carries a logic cell pointer at `[char+0x50]` (`m_pCell`).
Submersion helper `0x140312440` (release; `0x14033d4a0` EXP):

```
cell   = [char+0x50]                  ; assert on null
if (!(byte[cell] & 1)) return 0       ; cell flag bit0 = "water"
cellTop = word[cell+6] << 6           ; water surface, cell value is in 尺 (×64)
ground  = word[cell+4] << 6           ; cell floor
if (char.y > cellTop) return 0        ; above water -> no submersion
return cellTop - max(ground, char.y)  ; submersion depth
```

- **Waterline = `word[cell+6]<<6`; there is no height-based waterline.**
- `0x140312400` is **NOT** a waterline: it reads `[char+0x2FC]` and returns
  `6x/20 = 0.30x` (non-players, magic `0x66666667`) or `6x*11/112 ≈ 0.589x` (players,
  magic `0x92492493`). `[+0x2FC]` is the character's **nRunSpeed** (attr getter
  `0x140412CA0`; computed at `0x14018297F` as `(base·(percent+0x400))>>10`, clamped to
  `[nMinRunSpeed +0x294, nMaxRunSpeed +0x298]`; kungfu-row apply at `0x14015A6D3` writes
  `[+0x2FC]=row[+0x28]`, `[+0x340]=row[+0x2C]`). The function is a **speed factor** used
  as the swim/water XY velocity source and threshold (called in `Jump` state 2 at
  `0x140313810`, in `RunTo` `0x14031B69D/B6AD`, `WalkTo`). Correction of
  `JX3_GRAVITY_RESEARCH.md` §3.7 (0.589 is *not* a waterline/submersion factor and
  `[+0x2FC]` is *not* height). EXP moved the field to `[+0x308]` (`0x14033d460`).
- Water/landing animations come from `Represent/player/player_suspend.krl.txt`
  (`FallWaterAnimation`, `FallDownHeightWater/AdjustWater`, 500/50; some skills 10/0+200)
  — data only, unchanged in 1-6-0.

### 1.3 Swim states 6 / 7 / 8 and the per-frame transitions — HIGH (6/7), MED (8)

Move states are `m_eMoveState = [char+0x1F4]` (EXP unchanged). `ProcessVerticalMove`
(PVM) = `0x140318C50` (EXP `0x140346f20` body) is the only per-frame water/vertical
integrator (single direct call site, in the master update). Water behavior inside PVM
(release addresses):

| Behavior | Site | Semantics |
|---|---|---|
| state → **6** (SWIM, moving) | `0x319A71` | reached after an impulse/velocity is computed (states 2/3/`0x1F`/`0x20` descending into water); sets `[+0x270]` (Vz, computed from distance/frames over `[+0xC08]`), `[+0x174]`, `[+0x210]`, `[+0xC04]`, `[+0x204]=1` |
| state → **7** (SWIM, in place) | `0x319B60`, `0x319C7B`, `0x319E13` | zero-velocity float: clears `[+0x2F8]`, `[+0x260]`, `[+0x268]`, `[+0x270]`, `[+0x174]`, flags |
| **float height clamp** (6/7) | `0x31A285` | `if ([+0x170] > 0 && state ∈ {6,7}) { ecx = cellTop - scaled[+0x170]; if (y < ecx) y = ecx; }` — the character floats at the surface minus the scaled gravity-modifier (normally the surface) |
| exit water → **4** (JUMP/FALL) | `0x319D61`, `0x319FA5`; state 0x1F/0x20→4 at `0x319F85` | when the water branch no longer holds |
| fly re-entry (`nFlyFlag`) | `0x31A027` (state 1→0x20), `0x31A052` (other→0x1F) | `if ([+0x1FC] != 0 && [+0x364] == 0 && y - cellTop > 0x100)`: flying 256+ units above the water surface resumes fly/suspend |
| jump-chain landing reset | `0x31A24E..0x31A27E` | for states {1,2,3,6,7,27,28} (bitmask `0x18000000ce`) and for state 4 over water: `[+0x330]=0`, `[+0x338]=0` |

`KCharacter::SwimTo` (`0x14031D770`; EXP `0x14034d7d0`) is the **scripted/server
destination move**: valid only while already in state 6/7 (`state-6 <= 1`), clears
`+0x174/+0x210/+0xC04`, sets `+0x204=1`, then sets **7** on the in-place branch and
**6** on the moving branch. It is not the water-entry path.

**State 8 (SWIM_JUMP)** — setter `0x14031C400` (caller `0x1403E0783`, Lua-facing):
requires state **1 (stand)**, `[+0x260]==0`, `[+0x210]==0`, `[+0x214]==0` (parachute);
clears flags, sets **`[+0x1F4]=8`**, `[+0x204]=1`, calls the display notify
`0x14036BCB0`. It sets **no velocity**; the impulse is not in this function
(REGISTERED PROVISIONAL — see §6 P2).

### 1.4 Swim speed — MED

- Logic: water-surface movement uses the `0.589 × nRunSpeed` factor of §1.2 via the
  shared integrator/MoveTo (player); NPCs `0.30 × nRunSpeed`.
- Represent: `CharacterSwimSpeed=20` (`proof/gravity/number.krl.txt:22`) is the animation
  config value, **not** the logic speed. Do not present 20 u/frame as the game rule.
- Swim clips: `SwimStand/Forward/Backward/Left/Right` (Represent strings
  `0x00CD8A40+`); F1 swim clips are the `player_animation_use_originroot_trans.txt`
  9-row set (3x-states).

### 1.5 Entry / exit summary

- **Enter**: state 6/7 is entered by PVM from the state dispatch when the character is
  at/under the water surface while moving through the water branch (states 2/3 or fly
  states descending). There is no input "swim key"; you swim by moving into water.
- **Exit**: PVM resets to state 4 when the water branch drops; `SwimTo` never leaves 6/7.
- **Swim jump**: state 8 (script/server, from standing in water); on re-entry PVM puts
  you back into 6/7.

### 1.6 The engine AV on real water — root cause and correct host behavior — HIGH

- **Symptom:** client AV (`0xC0000005`) at `KG3DEngineDX11EX64.dll+0x12282B3`
  (`mov rbx,[rdi]` with `rdi=0`) whenever the actor is below a map water surface
  (stand/walk/spawn in a basin; 海岛 y=0 ocean floor above all). Reproduced on
  `龙门寻宝` NE basin `(64293,-125,55238)` and 海岛; option matrix (water quality/tier)
  ruled out.
- **Root cause (proven):** the faulting render-worker path does an rbtree lookup keyed by
  the engine's **lazily computed FNV-1 hash of the named context `RCPI_Scene`**; the
  guard race lets the worker read an uncomputed slot (value 0) → lookup miss → NULL
  deref without a check. `KG3DEngineDX11EX64.dll` has **129 such per-call-site value
  slots**; the existing D6 fix seeded only one (`RVA 0x2D5BBD0`).
  `FNV-1("RCPI_Scene") = 0x392E0BFA0428F080`.
- **Correct host behavior (no code patch):** seed **all 129 value slots** with
  `0x392E0BFA0428F080` **before `LoadMap`** (only when the slot is 0), gated on the
  engine PE timestamp (`0x6AA7C1F5` for the shipped build; slot RVAs are
  build-specific), opt-out `RC_SEED_RCPISCENE=0` for A/B. A/B proof (3x-states):
  seed-off crashes 3/4 at the basin; seed-on stands 15 s and drives through to y=-551,
  `DONE`.
- **Not part of the fix:** blocking movement into water (a guard was prototyped and
  removed as superseded), and the engine water *physics/render* layer (compressed scene
  blocks `Terrain_Block_Water_Compress` type `0xb`, `KG3DSceneBlockData::UnCompressWaterData`
  `KG3DEngineX64.dll 0x18035cc10` — same bytes in hd/exp — plus `%s%s%s.WaterData` /
  `%s_Water.mesh`) which remains an M2 host feature. The logic water itself (§1.2) is
  unaffected by this crash once the seed is in place.

---

## 2. 轻功 — THE GRANT SYSTEM

### 2.1 The cap field and the air-press gate — HIGH (behavioral), MED (field name)

`KCharacter::Jump` release `0x140313680` / EXP `0x14033eac0`:

- With **no active fly move record** — `[char+0x1F8] == 0` → branch at release
  `0x140313C22` / EXP `0x14033F201`:

  ```
  cap   = [char+0x334]   ; EXP: [char+0x340]   (nMaxJumpCount)
  count = [char+0x330]   ; EXP: [char+0x33C]   (nJumpCount)
  if (waterDepth >= scaledJumpSpeed) -> swim-jump branch (state 5/other)
  if (count >= cap)  reject          ; <-- the grant gate
  if (count >= 0xF)  reject
  count++ ; [char+0x330] = count
  ```

  The branch falls through to the common velocity stores
  (`[+0x268]=XY<<4`, `[+0x2F8]=XY`, `[+0x270]=Vz`, `[+0x26C]=heading`, `[+0xC08]=0`)
  with XY from the character's run-speed-derived value (`[+0x2FC]`, factor 8/16 with
  clamping to 127) and Vz from `[+0x340]` (release) = the character's jump-speed
  attribute (kungfu row, §1.2; Init `0x1403128B9` zeroes `[+0x330]` and writes
  `[+0x334]=1`). **No JumpParam row is read on this path.**

- With an active fly/move record — `[char+0x1F8] != 0` → the wall/mount/main paths
  **read `JumpParam.tab`**: row `24*school + count` at `g+0x23810` (takeoff
  `XY, Vz, Gravity`; `g+0x25C10` = `MaxJumpCount`; Wall `g+0x25C50`; Horse `g+0x25F50`),
  stores `[+0xC08]=TotalFrame`, and **spends the 气力值 pool**
  `[char+0x20194+idx*4]` against `[char+0x201A4+idx*4]` (skip if `[+0x201C8]`).
  This is the *powered* path only.

**Default grant = 1** (Init writes `[+0x334]=1`). Therefore: on a character with no
grant, an airborne jump press is **rejected — nothing happens**.

### 2.2 What raises the cap (the actual grant) — HIGH

1. **Passive skill 18 「踏云」 (江湖轻功, `skills.tab`: KindType=Leap, `IsPassiveSkill=1`,
   MaxLevel=1, Design_Belong=主角通用技能)**. Its script
   `scripts/skill/江湖/江湖轻功_踏云.lua` (extracted from the **release** PakV4 store;
   header comment "技能效果: 被动技能:二段跳"):

   ```lua
   skill.AddAttribute(
       ATTRIBUTE_EFFECT_MODE.EFFECT_TO_SELF_AND_ROLLBACK,
       ATTRIBUTE_TYPE.MAX_JUMP_COUNT,   -- enum id 5
       1, 0);
   ```

   → **+1 max air jump = the 二段跳 grant.** `ATTRIBUTE_TYPE.MAX_JUMP_COUNT = 5`
   (exe `ATTRIBUTE_TYPE` enum table: `{name*, id}` array at VA `0x1409F6940`,
   `MAX_JUMP_COUNT`→5, `SET_JUMP_COUNT`→6, `FLY_FLAG`→7). The attribute pipeline's
   character applier is the `add [char+0x334], r8d` function `0x1403ADE70`.
   The newbie trainer table `SkillLearning/新手村.tab` teaches **「江湖轻功」(skill
   10082, free, level 2)** — i.e. every player has the basic 轻功 package; 踏云 is its
   passive double-jump (baseline grant = 2). MED for "auto-learned by all", HIGH for
   the +1 effect.

2. **Buffs (`Buff.tab`, 61,555 rows; 1,215 rows use `atMaxJumpCount`)** modify the cap
   *additively* (values are deltas):
   - Negative (penalties): `139/1900/4808 扶摇直上` `-1` (during the rise you lose an air
     jump), `244 骑乘`/mount buffs `-1/-2` (mounted air jumps restricted),
     `195 千斤坠` `-2`, silence/entangle-like debuffs `-10` (jump forbidden),
     `32769 唐简禁止跳跃` etc.
   - Positive (extra jumps; special systems): `773 增加一次跳跃 +1`,
     `13239 通用_腾跃 +2`, `13340 猎魔人_跳跃 +2`, `3298 唐门密室_叶凡_二段跳 +2`,
     `51464 绝境_鸟翔碧空 +4`, `8164 踏燕南飞 +3`, `9518 飞行 +1`.
   - `atSetJumpCount` = 1 buff (`30085 重置跳跃` = set current count), `atFlyFlag`
     = 17 rows (see §3).

3. **Meridian passives** (not jump count, but 轻功 budget): skill **1196 任脉·龈交**
   script `经脉\基础系\基础系_新轻功上限提升.lua` applies
   `ATTRIBUTE_TYPE.ADD_SPRINT_POWER_MAX (0x21A) +14000` — the **气力值 (sprint/轻功
   power) pool ceiling**; skill **1571 任脉·燕口** reduces the 轻功 public CD
   (`EXECUTE_SCRIPT`). This is how longer 轻功 flights are unlocked.

### 2.3 What the player experiences — HIGH

| State | Air press behavior |
|---|---|
| no grant (cap=1) | rejected — no air jump at all |
| standard player (踏云, cap=2) | **exactly one extra air jump (二段跳)**; the third press is rejected. The extra jump is the same jump profile as the first (plain path §2.1), with the authored `f1b02yd二段跳a.ani` flip clip |
| grant N>2 (special buffs/encounters) | N-1 extra presses, each the same plain profile |
| during a 扶摇直上 rise / mounted | cap temporarily reduced by the buff (-1/-2), pressing jump gives fewer/no extra jumps |
| while a 轻功 flight record is active (`[+0x1F8]!=0`) | presses advance the **powered chain** (J-rows, §2.4) and spend 气力值 — this is skill-driven flight, not repeated space-tapping |

**The raw `JumpParam.tab` J1..JN chain launched ballistically on plain air presses never
happens in the game.** It is currently shipped only as the labeled research harness
`RC_DJUMP=chain` (and must stay labeled, or be deleted).

### 2.4 Chain rows and activation context — HIGH

`JumpParam.tab` (23 school rows; per school `MaxJumpCount`, `WeaponMask`, takeoff
`J0..Jn = (JumpSpeedXY, VelocityZ, Gravity)`, `...End`, `Wall*`, `Horse*`, `Dash*`,
`OnFlyCost/OnFlyFloatCost/OnFlyJumpCost/OnFlyStandCost/OnFlyJumpInSprintCost/
OnSprintDashCost/OnFlyBirdMoveCost/OnFlyBirdMoveDashCost`) is read **only** in the
`[+0x1F8]!=0` powered path, where:
- the row index is the current jump count; Wall (`[+0x200]`, count<4) / Horse
  (`[+0x1E774]`, count<1, `Vz += [horse bonus]`) / main variants are selected;
- the cost is deducted from the 气力值 pool (`[+0x20194+idx*4]`, idx = `[+0x134]`);
- `[+0xC08]` is set to `TotalFrame[row]` (jump-curve countdown; `JumpFrameParam.tab`
  applies per-frame XY/Vz/heading for schools 10/11).

`JumpFrameParam.tab` curves and `player_flyjump.krl.txt` (冲冲刺/坐骑轻功 Enter/Crash/
Turn/InAir sets) belong to this powered/skill flight.

### 2.5 Segment / phase timing — HIGH (source), no local timer

- **There is no client-side frame timer that advances the chain.** The record appliers
  (`0x140182470` / `0x140362960`) set `[char+0x1F8] = (recordByte[+0x1A] == 1)` from the
  server **move record**, and when the segment ends:
  `if (state ∈ {4,0x1A}) && [+0x1F8]==0 && [+0x330]>=1 → ModifySprintEndSpeed
  (0x1403140A0) applies the current row's `...End` triple; then [+0xC08]=0 and
  [+0x330]=1` (`0x140182874`/`0x14036317A`).
- Ground contact resets `[+0x330]=0` (PVM, §1.3). So: takeoff row on press → End triple
  at segment end → `jumpCount := 1` → next press continues the chain from there.
- The End triple is school 0 `60,90,11`; schools 1–22 `125,-140,12`.

### 2.6 What a host without a server must simulate — normative

1. **Do the grant, not the table.** Default `nMaxJumpCount=1`; model the 踏云 grant
   (cap=2) as the standard player. Air press:
   - `count >= cap` → reject (log);
   - else `count++`, apply the **plain jump profile** (the existing calibrated J0
     profile is the accepted approximation of the plain path), play the 二段跳 flip clip
     on the second jump, reset count to 0 on landing.
2. **Never read `J1..Jn` on plain air presses.** `RC_DJUMP=chain` may remain only as a
   harness explicitly labeled `HARNESS - NOT GAME BEHAVIOR` (or be deleted); the startup
   warning of the 3x-states branch is the model.
3. If/when the host implements 轻功 skills: only then add the powered path, with its own
   record/segment state, `[+0x1F8]`-equivalent, 气力值 pool (base/max from character
   data; 任脉·龈交 +14000), and the End phase + `jumpCount := 1`. Since the segment
   length is server record data, use the shipped `JumpFrameParam.TotalFrame` where it
   exists (schools 10/11) or register a provisional segment length (§6 P4).
4. Buff modifiers (§2.2) are additive deltas on the cap (扶摇 -1, mount -1/-2, special
   +N); model as buff effects when the host gets a buff system.

---

## 3. FLY / SUSPEND / BIRD / AUTO-FLY

### 3.1 State vocabulary (release; `m_eMoveState = [char+0x1F4]`)

| Code | Name | Evidence |
|---|---|---|
| 4 | JUMP/FALL (airborne) | everywhere |
| 6 / 7 | SWIM moving / in place | §1.3 |
| 8 | SWIM_JUMP | `0x14031C400` |
| 0x17 / 0x19 | SPRINT_DASH / SPRINT_FLASH | old §3.7 (SprintDash/SprintFlash) |
| 0x1A / 0x1B | SKILL_MOVE_SRC / DST | old §3.10 |
| 0x1F | FLY_FLOAT (float/hover; "滞空") | `FlyTo 0x140310E0A`; PVM float law §3.2 |
| 0x20 | FLY (rise / fly-to) | `FlyTo 0x140310C9A`; PVM fly re-entry |
| 0x21 | FLY_JUMP (轻功 jump) | begin `0x140310AB0`; `EndFlyJump 0x140310960` (assert) |
| 0x23 | BIRD_FLY | `BirdFlyTo` branch `0x14030CB8C` |
| 0x24 | BIRD_FLOAT | `BirdFlyTo` branch `0x14030CA6C` |
| 0x25 | BIRD_JUMP (keeps `[+0x270]`) | `BirdFlyTo` 0x25-preserve branch |

Represent-side names (`SWIM/SWIM_JUMP/SWIM_DOUBLE_JUMP/FLOAT/FLY_FLOAT/FLY_JUMP/
BIRD_FLY/BIRD_FLOAT/BIRD_JUMP/AUTOFLY/SUSPEND/…`) are display vocabulary only —
JX3RepresentX64 strings `0x00CBCF08..0x00CBD3F8`; there is no logic `SWIM_DOUBLE_JUMP`.

### 3.2 `FlyTo` and the 0x1F float law — HIGH (FlyTo), HIGH (float law)

`KCharacter::FlyTo(this, destX, destZ, mode4th)` `0x140310B70` (EXP `0x14033b6a0`):
- valid only from states **0x1F/0x20** (`state-0x1F <= 1`) + shared path check;
- speed source = `[char+0x2FC]` (nRunSpeed, §1.2); computes velocity via sin/cos of the
  heading/atan2 for a destination, or (same-position + mode) a forward impulse;
- sets state **0x20** (one branch, `0x140310C9A`) or **0x1F** (`0x140310E0A`), clears
  `+0x174/+0x210/+0xC04`, `+0x204=1`.

**Float law (state 0x1F)** — PVM `0x31913C`:

```
if (state == 0x1F
    && !(cell.flag & 1)                 ; not the water flag
    && y + Vz <= cellTop + 0x100
    && !((cell.flag & 2) && moving))
{ y = cellTop + 0x100; Vz = 0; }        ; hold at surface/cell top + 256 u
```

i.e. in fly-float the character descends under gravity until the floor = **cell top +
0x100 (256 u)** and then holds (the 3x-states W6 proof measured exactly this:
`fly: float floor y=1218 (terrain+0x100, PVM 0x31913C state 0x1F)`). If the water flag
(bit0) is set the clamp does not apply (you can descend into water).

### 3.3 Fly jump (0x21) — HIGH

- begin: separate fn `0x140310AB0` (zeroes `[+0x268]/[+0x2F8]/[+0x270]`, flags, sets
  state **0x21**) — the doc's old "back to 0x21 in EndFlyJump" was this function.
- `KCharacter::EndFlyJump` `0x140310960`: asserts `state == 0x21`; sets state **4**
  (`0x1403109D8`) or, if `[+0x58]`/variant gate, state **0xE** (`0x140310A75`);
  `[+0x204]=1`; display notify.
- Entered from script/Lua (`EndFlyJump` caller `0x1403DD4A0`; begin caller `0x1403DD610`),
  i.e. 轻功 skill/server, never raw input.

### 3.4 Bird fly — HIGH

`KCharacter::BirdFlyTo` `0x14030C940` (EXP `0x140333ed0`): requires a cell (`m_pCell`);
valid states **0x23..0x25**; sets **0x24** or **0x23**; if already 0x25 keeps `[+0x270]`
(Vz) and skips the reset. Represent plays `BIRD_FLY/BIRD_FLOAT/BIRD_JUMP` clips.
Callers: `0x14036B204`, `0x1403740B0`, `0x1403DCA5B` (script/Lua cluster).

### 3.5 Auto-fly (AUTOFLY / glide paths) — HIGH (mechanism), MED (data source)

`KCharacter::ProcessAutoFly` `0x140316990` (EXP `0x140344b20`) follows a nav **track**
(`pTrack/pCurrentNode/pEndNode` asserts), advancing via `KCharacter::MoveTo` per node and
incrementing `[+0xC08]` per node (`0x140316BAC`); reset on missing track calls
`0x14031D650`. Caller = PVM master update (`0x140317719`). `ON_START_AUTO_FLY` is a
script event. The track/node data is server/nav-driven — **the host cannot invent it**;
treat auto-fly as unavailable without authored tracks (registered boundary).

### 3.6 Parachute / water-fly ability — HIGH (consumers), data-driven

- `bOnParachuteFlag` = `[char+0x214]` (old "no xref" was a string-offset mix-up), set by
  script op `ON_PARACHUTE_FLAG` (ATTRIBUTE_TYPE id 9); consumers: jump reject
  (`0x14031377D`), drop-speed Vz keep, bird-fly gate, landing checks, skill-move/swim.
- `bWaterFlyAbility` / `ATTRIBUTE_TYPE.WATER_FLY_ABILITY (22)` and `CAN_NOT_JUMP (23)`
  are attribute flags for surface flight / jump lockout.
- `FLY_FLAG` (id 7) sets the fly flag behaviorally read as `[char+0x1FC]` (PVM fly
  re-entry §1.3; Jump mounted logic). Buff 13422 「全门派战斗轻功状态监控」 carries
  `atFlyFlag` and the script `轻功\轻功状态结束处理.lua`, whose `OnRemove` calls
  `player.StopBirdFly(); player.UnlockBirdMoveZ();` + disables the air-combat camera —
  i.e. the combat-轻功 state is cancelled by (re)applying this monitor buff.
- Suspend/滞空 animation sets: `player_suspend.krl.txt` (48 entries, Stay/Front/Left/
  Right/Rear + fall thresholds); `player_flyjump.krl.txt` (per-skill
  `ChongCiQingGong`/`ZuoQiQingGong` Enter/Crash/Turn/InAir).

### 3.7 Triggers and host requirements — normative

- **All state transitions above are script/skill/server driven.** Verifiable callers:
  `FlyTo` ← `0x14036B22D`, `0x1403740D8`; `BirdFlyTo` ← `0x14036B204`,
  `0x1403740B0`, `0x1403DCA5B`; `SwimTo` ← `0x14036B23D`, `0x14036B41B`,
  `0x1403740E8`, `0x1403E0F3E`; state-8 setter ← `0x1403E0783`; begin/end fly jump ←
  `0x1403DD610`/`0x1403DD4A0`. The `0x14036B2xx` cluster is the KPlayer command
  surface; `0x140374xxx`/`0x1403DCxxx`/`0x1403E0xxx` are Lua bindings. Script event
  names: `ON_FLY`, `ON_FLY_FLOAT`, `ON_FLY_JUMP`, `ON_BIRD_FLY`, `ON_BIRD_FLOAT`,
  `ON_BIRD_JUMP`, `ON_START_AUTO_FLY`, `ON_SWIM`, `ON_SWIM_JUMP` (exe `0x007DE3A0..`).
- **Host minimum**: an integrator port (15 Hz) that applies the PVM vertical law
  (§1.3/§3.2), a state field, and script hooks. Without a script/data trigger the host
  must not fabricate fly entry; the 3x-states `RC_SUSPEND_DEMO` harness (pressing a key
  to enter 0x1F) is a **harness, not game behavior** and must stay labeled.

---

## 4. ACCEPTANCE CRITERIA (numbered, testable)

**Water / swim**
1. Given a terrain cell with `flag bit0` set and `y <= word[+6]<<6`, the host reports
   `depth = (word[+6]<<6) - max(word[+4]<<6, y)` and treats the cell as water; with the
   bit clear, no water.
2. Standing (no input) below the surface → state 7; holding a move key → state 6; the
   character is clamped to `y >= surface - scaledGravityModifier` (surface when the
   modifier is 0). Never sink below the cell floor.
3. Moving to a cell without water (or rising above the surface) → state 4 (airborne),
   with the landing rules unchanged.
4. Player water-surface speed = `0.589 × nRunSpeed`; NPC = `0.30 × nRunSpeed`
   (0.589 = 6h·11/112; h = nRunSpeed, **not** character height).
5. `SwimTo`-style destination moves are only accepted in state 6/7 and set state 6 when
   moving / 7 when in place (no water entry through it).
6. State 8 (swim jump) can only start from standing (state 1) with no parachute and no
   other active flags; whatever vertical impulse it uses must be a registered
   provisional until decoded.
7. With all `RCPI_Scene` hash slots seeded, an actor can stand/walk at `龙门寻宝`
   `(64293,-125,55238)` and 海岛 ocean-floor positions for ≥15 s without the
   `+0x12282B3` AV, and can drive through the basin (3x-states A/B proof). With seeding
   off, the AV reproduces (race-dependent).
8. Per-map water surfaces are loaded from `water\surface\watersurfacelist.json`
   (5 maps listed in §1.1), not hand-authored boxes (provisional until §6 P1 is closed).

**轻功**
9. With cap 1 (no grant), an airborne jump press is rejected and the log says so.
10. With the standard grant (踏云, cap 2), air press #2 executes exactly one extra jump
    with the plain profile; air press #3 is rejected. Landing resets the count to 0.
11. No matter the cap, plain air presses never apply `JumpParam` J1..Jn, never launch the
    11–78 m ballistic arcs, and never consume 气力值.
12. When (and only when) the host models a powered 轻功 flight record: the first press
    applies the J-row by jump count, the pool `[+0x20194]` is checked/deducted, and at
    the server-flagged segment end the `...End` triple is applied with `jumpCount := 1`.
13. The `RC_DJUMP=chain` harness, if kept, is labeled `HARNESS - NOT GAME BEHAVIOR` in
    every log line and in the window title; it is never the default.
14. Buff modifiers on the cap are additive and removable (扶摇 -1, mount -1/-2,
    silence -10, special +N).

**Fly / suspend / bird**
15. `FlyTo` is accepted only from states 0x1F/0x20 and results in 0x20 or 0x1F according
    to its mode; it never originates from a raw key press in the host.
16. In state 0x1F, with a non-water cell, the character descends under gravity and then
    holds at `cellTop + 0x100` with `Vz = 0`.
17. `EndFlyJump` requires state 0x21 and transitions to 4 (or 0xE); begin-fly-jump zeroes
    the velocities and sets 0x21.
18. With the fly flag set, crossing `y - cellTop > 0x100` upward re-enters 0x20 (from
    stand) / 0x1F (otherwise) with `[+0x364]==0`.
19. `BirdFlyTo` is valid only in 0x23..0x25; 0x25 preserves `[+0x270]`.
20. Auto-fly requires an authored nav track; with no track the host does not move.
21. Parachute flag blocks air jumps; removing it restores the grant cap.

---

## 5. ASSUMED-vs-VERIFIED (corrections; each claim → evidence)

| # | Old claim (source) | Verdict | Evidence |
|---|---|---|---|
| 1 | `GetWaterline 0x140312400` = waterline 0.30h/0.589h from character height (`JX3_GRAVITY_RESEARCH.md` §3.7, `REBORN_JUMP_FALL_SPEC.md`) | **WRONG** | reads `[+0x2FC]` = nRunSpeed (attr getter `0x140412CA0`; computed `0x14018297F`; kungfu apply `0x14015A6D3`); the `0.30/0.589` are **speed factors**, not a height waterline. Real waterline = cell `word[+6]<<6` (`0x140312440`) |
| 2 | The air press reads `J1` from `JumpParam` (`JX3_DOUBLE_JUMP_RESEARCH.md` §2, old doc) | **WRONG for plain presses** | `KCharacter::Jump` `0x140313975 je 0x140313C22`: no-record path caps at `[+0x334]` and never reads the J table; J rows only at `0x140313B20+` under `[+0x1F8]!=0` |
| 3 | The 轻功 grant = generic script op `MAX_JUMP_COUNT` with setter `0x140431180` (3x-states note) | **PARTIAL**: enum id 5 confirmed; concrete grant proven = passive skill 18 踏云 `MAX_JUMP_COUNT +1`; the char applier is `add [char+0x334], r8d` `0x1403ADE70`; the tiny setter attribution is MED | `spec3x\ext\out\...\江湖轻功_踏云.lua`; `attribute_type_table.txt`; `skills.tab` row 18 |
| 4 | "二段跳 = one extra jump" but modeled with the J1 triple / raw chain (client `RC_DJUMP=flip/chain`) | **flip≈correct profile; chain wrong** | plain path uses the character speed/jump attributes (c22), not J1; `chain` is the powered-path table used out of context |
| 5 | State-0x1F float floor = "terrain cell top + 0x100" (3x-states) | **CONFIRMED with conditions** | PVM `0x31913C`: only when `!(cellflag&1)` and `y+Vz <= cellTop+0x100` and not `(cellflag&2 && moving)`; holds `y=cellTop+0x100, Vz=0` |
| 6 | Swim float = `surface − 0.589×height` (3x-states `WaterField.cs`, registered provisional) | **WRONG basis** | 0.589 is a run-speed factor (§1.2); PVM float clamp is `y = max(y, cellTop − scaled[+0x170])` (`0x31A285`) |
| 7 | `CharacterSwimSpeed=20` = swim speed (spec row 6/[OK]) | **PARTIAL** | Represent config only; logic water-surface movement is the `0.589×nRunSpeed` factor; use 20 only if a data-driven animation speed is needed |
| 8 | Horse Vz add `[+0x34C]` "unchanged" in 1-6-0 (`exp16/1_movement.md` field table) | **WRONG label** | release: base Vz `[+0x340]`, horse add `[+0x34C]`; EXP: base Vz `[+0x34C]`, horse add `[+0x358]` (`new_jump2.txt` 0x14033ed06/0x14033eecd) |
| 9 | Water = scene blocks + `_Water.mesh` only; no authored per-map water (G-24 / VOID_SPAWN §2.3) | **PARTIAL** | per-map authored surfaces exist (`watersurfacelist.json`, all 5 maps, §1.1); engine compressed type `0xb` and `%s_Water.mesh` are the render side; logic cells from `.Map.Logical`/KMiniScene (writer open) |
| 10 | Water-entry AV = "missing water layer" only (`VOID_SPAWN_CRASH_TRIAGE.md` working narrative) | **ROOT CAUSE FOUND** | lazy `RCPI_Scene` hash slots (129) in `KG3DEngineDX11EX64.dll`; seed all slots before `LoadMap` (`0x392E0BFA0428F080`); A/B proof in 3x-states |
| 11 | `0x140327A80` = per-frame swim step (`REBORN_JUMP_FALL_SPEC.md` row 11) | **WRONG (already corrected)** | `KQuestList::UpdateNpcQuestMark`; PVM is the integrator |
| 12 | `SwimTo` "sets state 7" (old §3.7) | **PARTIAL** | 7 in-place branch, **6** moving branch (`0x14031DA81`) |
| 13 | `bOnParachuteFlag` has no code xref (`JX3_GRAVITY_RESEARCH.md` §3.11) | **WRONG (already corrected)** | `[char+0x214]`, six consumers (Jump `0x14031377D`, drop-speed, bird gate, landings, skill-move/swim) |
| 14 | State 8 swim jump has "no velocity site found" (3x-states provisional) | **CONFIRMED absence in the setter** | `0x14031C400` only sets state/flags; impulse source open (§6 P2) |
| 15 | "no local frame counter advances the chain" (3x-states / C2) | **CONFIRMED** | the End triple is record-flag driven (`[+0x1F8]=record[+0x1A]==1`), no timer |
| 16 | old new-build field map: jump-speed mod `[+0x16C]/[+0x170]` × `[+0x40]/100` | **CONFIRMED** | Init `[+0x16C]=0x46`; PVM `[+0x170]` scaled (`0x31A285`), Jump prelude `[+0x16C]·[+0x40]/100` |

### New-build (1-6-0) offset moves relevant here (EXP, from `exp16/1_movement.md` + fresh reads)

| Field | 1-5-0 | 1-6-0 |
|---|---|---|
| jump count | `+0x330` | `+0x33C` |
| max jump count (grant cap) | `+0x334` | **`+0x340`** (`new_jump2.txt` 0x14033f201/0x14033f227) |
| next-jump | `+0x338` | `+0x344` |
| base jump Vz | `+0x340` | `+0x34C` |
| horse Vz bonus | `+0x34C` | `+0x358` |
| nRunSpeed | `+0x2FC` | `+0x308` |
| gravity | `+0x320` | `+0x32C` |
| VelocityXY | `+0x2F8` | `+0x300` |
| air/script counter | `+0xC08` | `+0xC28` |
| state / fly flag / record flag / parachute | `+0x1F4` / `+0x1FC` / `+0x1F8` / `+0x214` | unchanged |
| `Jump` | `0x140313680` | `0x14033eac0` |
| `SwimTo` | `0x14031D770` | `0x14034d7d0` |
| `FlyTo` / `EndFlyJump` / `BirdFlyTo` | `0x140310B70` / `0x140310960` / `0x14030C940` | `0x14033b6a0` / `0x14033afe0` / `0x140333ed0` |

`JumpParam/JumpFrameParam/Sprint/SkillMove(rows 1–952)/player_suspend/player_flyjump`
are byte-identical in 1-6-0 (tables unchanged).

---

## 6. REGISTERED PROVISIONALS (host policy, AGENTS §6) + re-open criteria

- **P1 — Water source.** Host models water from `watersurfacelist.json` (per map) mapped
  to cells, or `RC_WATER` boxes for tests. *Re-open:* trace the `m_pCell` writer /
  `KMiniScene` cell stream / `<map>.Map.Logical` loader in `JX3ClientX64.exe` (the logic
  cell words), then load water directly from game data.
- **P2 — Swim-jump impulse (state 8).** Setter `0x14031C400` carries no velocity; host
  reuses the calibrated J0 takeoff. *Re-open:* find the state-8 per-frame handler (PVM
  dispatch case for 8) / the script-server record that supplies the impulse.
- **P3 — Swim buoyancy.** Decoded clamp is `y = max(y, cellTop − scaled[+0x170])`
  (normally surface). If a below-surface push is needed, keep the decoded clamp only.
  *Re-open:* decode the PVM underwater push branch (offset `[+0x364]` gate).
- **P4 — Chain segment length (powered 轻功 only).** No client timer; use
  `JumpFrameParam.TotalFrame` where shipped (schools 10/11) or a fixed value for
  research, always labeled provisional. *Re-open:* server move-record producer traced.
- **P5 — Fly/suspend entry trigger.** The law and states are decoded, but the entry is a
  轻功 skill/script transition; any host key that enters 0x1F is a harness. *Re-open:*
  drive the real skill scripts (扶摇直上/梯云纵 etc.) once the host has skills.
- **P6 — Auto-fly tracks.** No local track data; do not fabricate. *Re-open:* authored
  nav/track source (server).

**Explicitly rejected as game behavior (must not ship unlabeled):** the raw
`JumpParam` J1..Jn ballistic press chain; "swim speed 20 u/f" as the logic rule;
`surface − 0.589×height` as the float/waterline law; blocking water movement as an
AV workaround.

---

## 7. Reproduce (all read-only; scratch = `C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x\`)

```powershell
$py="C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
$hd="C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"
$S="C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x"

# 1) grant gate + PVM water/fly (release)
& $py tools\collision\disasm_range.py $hd 0x313C00 0x180 S\jump_gate.txt
& $py tools\collision\disasm_range.py $hd 0x318C50 0x1700 S\pvm_full.txt
& $py tools\collision\disasm_range.py $hd 0x31C400 0x70   S\swimjump.txt
& $py tools\collision\disasm_range.py $hd 0x310B70 0x2a0  S\flyto.txt
# 2) ATTRIBUTE_TYPE enum + Buff.tab survey -> attribute_type_table.txt / buff_*.txt
& $py S\dump_attr_enum.py $hd 0x1409F6940 0x1409F9380 | Out-File -Encoding utf8 S\attribute_type_table.txt
& $py S\buf_survey.py "<store>\...\logic-skill-prefixed-out\settings\skill\Buff.tab" > S\buff_move_attrs.txt
# 3) release PakV4 scripts (official extractor, scratch mirror; junction bin\PakV4 -> release store)
& S\ext\bin\zhcn_hd\bin64\PakV4SfxExtract.exe S\ext\pl.txt S\ext\out  # 江湖轻功_踏云.lua + 经脉 scripts
# 4) per-map water inventory
& S\ext\bin\zhcn_hd\bin64\PakV4SfxExtract.exe S\ext\pl3.txt S\ext\out3 ; & $py S\water_summary.py S\ext\out3
```

**Evidence files (scratch):** `attribute_type_table.txt`, `buff_jump_summary.txt`,
`maxjump_dist.txt`, `buff_move_attrs_decoded.txt`, `ext\out\scripts\skill\江湖\江湖轻功_踏云.lua`,
`ext\out2\scripts\skill\经脉\基础系\基础系_新轻功上限提升.lua`, `ext\out3\...\watersurfacelist.json`,
`pvm_full.txt`, `swimjump.txt`, `speed440.txt`, `attr_getters.txt`, `charapply.txt`,
`runspeed_calc.txt`, `water_maps_summary.txt`.
**Repo evidence:** `proof/gravity/disasm/{kcharacter_jump,fly_to,end_fly_jump,swim_to,
bird_fly_to,process_vertical_move,get_waterline}.txt`; `docs/movement/*`;
`sibling worktree reborn-iso-3x-states: docs/character/3_5_3_7_RAGDOLL_SWIM_FLY.md
(\"Host implementation (W6)\" + water-entry crash section), proof/character/3x_states/
seed_rcpiscene_slots.txt`.

Last verified: 2026-10-06 (release 1-5-0-9975 + EXP 1-6-0-9536 binaries, static only).
