# JX3 轻功 behavior — full analysis (client-side)

**Status:** consolidated client-side behavior analysis. Sources are the shipped
tables/scripts and the decoded engine functions; live claims carry confidence.
**Last verified:** 2026-09-30 (sandbox client `reborn_client_mini.exe`, cropped map).
**Related:** `docs/movement/JX3_GRAVITY_RESEARCH.md` (per-frame law),
`docs/movement/REBORN_JUMP_FALL_SPEC.md` (reproduction spec),
`docs/controls/OPERATION_MODES_PLAN.md` (input modes). The earlier
`JX3_DAQINGGONG_RESEARCH.md` (agent/daqinggong) is folded in here.

## 1. Terms and systems

| Term | Meaning | Evidence |
|---|---|---|
| 新轻功状态 / 萍踪侠影 | the flight state WW enters; hold W keeps, release ends, Space does the air moves | `string.txt` `STR_SPRINT_POWER`; buffs 17327/24073 |
| 气力值 (SprintPower) | the flight resource (max/cost/revive per level/body; server-synced) | `KLevelUpList` columns; `OnSyncSprintPower` |
| 纵跃 / 滑翔 / 俯冲·急坠 / 登顶 | the stage names (leap / glide / dive / summit) | `Sprint\Condition.tab` desc, `Z_纵跃UI.pss`, `<style>·坠` skills |
| 大轻功 styles | per-school names: 游龙步(天策) 逍遥游(纯阳) 百转千回(藏剑) 金虹击殿(明教) 点墨江山(万花) 一苇渡江(少林) 暗香掠影(七秀) 云体风身(五毒) 九州踏歌(长歌) 堕天沉渊(苍云) 飞鸢泛月(唐门) 四方行(丐帮) 雷影风踪(霸刀) 御空翔波(蓬莱) 吴钩碎雪(凌雪阁) 月渡星汉(衍天) 凌叶飞霄(药宗) | `Skill.txt` 21204–21217/23478/25873/29679 |

**Official key map** (`\UI\Scheme\Case\Sprint\Action.tab`, verbatim comments):

| Input | Action |
|---|---|
| 双击 W | 【WW上冲】 (双击S 【SS下冲】, 双击A/D 【AA左冲/DD右冲】) |
| w | 【w纵跃】 |
| Space | 【space一段】…【space八段】, 【space急坠】, 【space前冲】, 【space中跃】 |
| Shift | 【SHIFT滑翔】/【SHIFT高跃】/【shift急降】/【shift前冲】 |
| 按住 W | 【快W滑翔(H)】/【W上爬】/【W按住】 |
| 松开 W | 【松开W登顶】 |

## 2. Stage table (`Sprint\Condition.tab`, 1868 rows)

Per-school rows keyed by `BitOPSchoolID` + conditions (`JumpCount`, `Jumping`,
`Fighting`, `RunOnWater`, `OnHorse`, `IgnoreGravity`, `HangFlag`, `OnTowerFlag`,
`CanTowerFlag`, `InProgress`, `nParkourFlag`, `bSlideSprintFlag`, `InStickCamera`,
`KeyStateGroup`, `MoveStateGroup`, `HangVelocityAndDirection`, `ActionGroup`, …):

- `<style>·纵跃段` = **JumpCount 1** "一段·前跃第一段跳"; then 一段/二段(上跃)/三段(鹰)/
  四段(**斜降**)/五段/六段(**俯冲**), with the 莲台/旌旗/弈韵 alternate branches (jump
  6–11, `SpecialSprint.tab` `szJumpCounts`).
- Common (school 0) rows 35–46: **萍踪侠影·一段~四段** (every-class beginner chain).
- Mounted: 纵马疾驰·纵跃段 (JumpCount 1, Jumping/RunOnWater).
- 飞檐走壁·攀爬 (wall climb, A/D/W held), 凌霄登顶·登顶段/冲刺段, 蹬踏借力·冲刺段
  (踩人冲刺 → auto-switch to 【腾空纵跃段】), 双人冲刺/双人俯冲, 凌波击水·冲刺段 (water).

The runtime dispatcher is `ui\script\hotkeys.lua` `ResponseDisplacementHotkey`
(proto 44), fed by `ui\script\sprintbase.lua` (`Sprint_GetAllAction`,
`GetFitAction`, `Sprint_IsGlideEnable`, `Sprint_SetClimbOver`, …); the table
registry (`g_tTableFile`) lives in `ui\script\common\table_defs.lua`.
`Action.tab`/`Condition.tab`/`Display.tab`/`SpecialSprint.tab` are extracted in
`proof/controls/sprint/out/`.

### 2.1 Ground: press and hold W = 疾跑段 (accelerated run)

Every school has `<MOVEFORWARD;1>` (hold W) rows with `JumpCount=0`,
`Jumping=0`, `WeaponCheck=-1`, `BuffID=12085`, `OTAction=12085;150`:

| School | desc | comment |
|---|---|---|
| 0 (common) | 萍踪侠影·疾跑段 | 入门轻功·疾跑段 |
| 3 | 一苇渡江·疾跑段 | 进入轻功地面上加速跑 / 水面上加速跑 |
| 4 | 点墨江山·疾跑段 | 同上 (RunOnWater 0/1 variants) |
| 1/2/5/… | 游龙步/逍遥游/暗香掠影·疾跑段 | 同上 |

So holding W on the ground enters the **轻功疾跑段** — buff **12085 通用疾速跑**
(`Buff.tab`) plus **12190 通用疾跑按住** (the while-held buff; the 12085 script
`疾跑通用BUFF魔法.lua` removes it on exit) — an accelerated run above the normal
run speed (20 尺/s), with a separate water-surface variant (`RunOnWater=1`).

**Speed**: the sprint velocity is capped by `Sprint.tab` **MaxVelocityXY =
120 u/frame** (schools 0–16) / **127 u/frame** (17+):
- at the 15 Hz logic tick: **1800 u/s = 28.1 尺/s ≈ 9.4 m/s** (192 u/m);
  newer schools 1905 u/s ≈ 29.8 尺/s;
- at the client's 16 fps convention (`GAME_FPS=16`): 1920 / 2032 u/s.
- Ramp: staged speed-percent buffs `疾速第零~三段` (11343/11338/11339/11340,
  `atMoveSpeedPercent` 256 = +25%, 768 = +75%, …) while `InSprint`.
- Compare: normal run = `CharacterRunSpeed` 20 u/frame = 320 u/s (5 尺/s) — the
  sprint is ≈5.6× the run.

Release W → `MoveForwardStop` → `HoldW=0` + `CheckEndSprint()` (ends the sprint if
no other key is held). Double-tap W is NOT the sprint — it is 【WW上冲】 (§1); the
earlier "double-tap = sprint" note in `docs/controls/` is superseded.

Sandbox status: the 疾跑段 staged ramp (25/75/100% of the Sprint.tab cap, 1800 u/s
for school 4) is modelled in `client/RebornClient.cs` (`jipaoActive`), verified
2026-09-30 (`jipao: enter` -> 450/1350/1800 u/s -> `jipao: end` on release).

## 3. Movement law (verified, `JX3_GRAVITY_RESEARCH.md` §3)

- **Logic tick 15/s** (66.7 ms); velocities are **integer units per frame**.
- **Horizontal velocity = speed + heading**: `+0x2F8`/`+0x268` (speed, ×16 fixed),
  `+0x26C` heading byte; `ProcessAcceleration` rebuilds the vector each frame from
  the current velocity + input acceleration + heading steering and recomputes
  `speed = |v|`, `heading = atan2(v)` (`0x1403168DB`).
- **Gravity** `+0x320` (per jump row `Gravity`, clamped [0,31] u/f²); applied in
  `ProcessAcceleration` (`Vz += accel`, `0x140316912`), slope-projected through the
  terrain cell.
- **Vertical** `ProcessVerticalMove` (`y += Vz`, ground/landing rules); landing
  resets the chain (`jumpCount := 0` when within 1 尺 of the cell top).
- **Drop** `ProcessDropSpeed`: rotates `(Vxy, Vz)` by the slope; **air-stop**
  (`Vz = 0`) when the resulting speed < `0x18<<4` and `[+0x260]==0`.
- **Clamps**: XY ≤ 127 u/f, Z ∈ [−2048, 2047] u/f; gravity [0, 31] u/f².
- **Chain phases**: press → takeoff triple; segment end → that row's `…End` triple
  + `jumpCount := 1` (`ModifySprintEndSpeed` `0x1403140A0`); ground → 0.
- **Curves**: `JumpFrameParam.tab` overrides the velocity per frame and adds a
  per-frame **heading delta** (`DirectionXY`, byte × π/128).

## 4. Falling and turning the facing (the specific question)

**Answer: turning steers the horizontal fall; the vertical fall is unchanged.**

1. The horizontal state is a **vector (speed + heading)**, not a world-space
   velocity that persists independently. `ProcessAcceleration` steers that vector
   toward the current heading/input each frame (sin/cos rotation by the heading
   difference, `0x140316830`–`0x1403168A7`), then recomputes magnitude + heading.
   So when you turn the face (input/camera) mid-fall, the fall **curves toward the
   new facing at the current horizontal speed** — no drift in the old direction.
2. The **vertical** speed keeps integrating with gravity (`Vz += g`, clamps) — the
   fall rate is unaffected by turning; only the horizontal trajectory rotates.
3. On slopes, `ProcessDropSpeed` additionally rotates `(Vxy, Vz)` by the terrain
   slope; at very low speed with no input flag it **air-stops** (`Vz = 0`).
4. The **fly/大轻功** states layer on top: `FlyTo` (0x20→0x1F), `EndFlyJump`
   (0x21→4/0xE), `BirdFlyTo` (0x24→0x23), `AutoFly` (nav path), `SUSPEND`/`SUMMIT`;
   the glide/hover uses the `player_suspend.krl.txt` sets (per-school 滞空/滑翔
   animations, `DragLimit`, `SuspendCameraMaxDragSpeed`, `IsSuspendCameraRoll`).
5. **Confidence**: HIGH for the speed+heading vector and gravity/drop law (decoded
   RVAs); MED for the exact steering gain/turn-rate in air (not fully traced).

## 5. Data tables that drive 轻功

| Table | Content | Key values (school 4 / 万花) |
|---|---|---|
| `settings/JumpParam.tab` | per-school chain triples + fly costs | J0 40/90/11, J1 50/160/8, J2 70/240/7, J3 100/700/36, J4/J5 100/−250/8; End 125/−140/12; MaxJumpCount 5; OnFly 75, Float 35, Jump 300, Stand 21, InSprint 200, Dash 3000, Bird 206, BirdDash 375 |
| `settings/JumpFrameParam.tab` | per-frame arcs (schools 10/11 only) | XY 150→205→273 peak, Z dive −949 |
| `settings/Sprint.tab` | fall/dive caps | XY 10–120, Z 8–**900** u/f (newer schools 127/1000) |
| `settings/SkillMove.tab` | scripted moves (918 rows, 64 fly-only) | 311 launch (Z +590), 312 school launch (+769), 517–528 新大侠 chain (XY 33–92; 528 Z −2337) |
| `Sprint\Condition/Action/Display/SpecialSprint` | stage/action/key tables | see §2 |

## 6. Costs and limits

- **气力值**: entry needs `nSprintPower >= 10000`; school trigger costs
  `100*CONSUME_BASE`; shared cost handler `通用轻功气力值扣除` (1×CONSUME_BASE /
  25 / 5×CONSUME_BASE per level). Attribute mods: max `+D0/100`, drain
  `−D0/19*3`/s, regen `+D0/20*3`/s.
- Bans: 禁轻功 buff family (562), 禁止大轻功 (9138 addon label; 5665/11990/13277/
  18487 tooltips), 双人轻功 (31003), 飞行机甲 (21438), altitude gates (mobile 扶摇
  `nMaxAltitude = 0.5*HEIGHT_BASE`), `MapBanMask` per skill.
- Server authority: 气力值 values, fall-death threshold, move sync.

## 7. The WW (double-tap W) behavior

- UI: `MoveForwardStart` sends `ResponseWASDKey('Forward', down, IsKeyDoubleDown())`
  → `ResponseDisplacementHotkey` (sprint dispatcher) → `OnUseSkill`/`BreatheCall`.
- Official: 双击W = 【WW上冲】; 松开W = 【松开W登顶】; 按住W = 【快W滑翔】.
- **Player tech (emergent, not a shipped action row):** in air, WW applies the fly
  velocity (authored curve/End values), releasing W ends the state but the
  velocity persists → a fast forward+down charge. The sandbox models this:
  forward = curve entry 150 u/f (2250 u/s), level while held; release → gravity
  builds the down; terminal fall cap = Sprint.tab 900 u/f (13500 u/s); charge clip
  one-shot (`PlayAnimation` playType 1; 0 loops).

### 7.1 万花大轻功「点墨江山」 in the sandbox (implemented 2026-09-30)

Sources: `proof/controls/sprint/out/scripts/skill/轻功/轻功通用/万花轻功触发.lua`,
`万花轻功急坠.lua`; `settings/JumpParam.tab` school 4; `Sprint/Condition.tab`
school 4 JC1..JC5; `Sprint/Action.tab` actions 5/8/9/10/11.

| Step | Real source (HIGH) | Sandbox model |
|---|---|---|
| Trigger | skill 20628: CanCast `nSprintPower >= 10000` (all school triggers share the gate), cost `100*CONSUME_BASE`; cast applies `SkillMove 336` (the school launch); `Apply` -> `SetTimer(30)` -> `OnTimer`: `BirdFlyTo` + `LockBirdMoveZ`, buffs 13422(lv3)/14626/13836 (binds 13889/16516) | WW (ground or air; ground WW = 【WW上冲】 per the scenario table, not a sprint): 气力值 gate 10000, cost 2500 (CONSUME_BASE=25 hypothesis, MED); the cast runs the **SkillMove 336 launch** (31 frames, XY=0, IgnoreGravity; the per-frame VelocityZ curve), then `BirdFlyTo` + `LockBirdMoveZ` starts the Z-locked fly. Stage presses are ignored during the launch (SkillMove `CanJump=0`) |
| Stages | Condition.tab school 4: JC1..JC5 = 点墨江山·纵跃段/一段/二段/三段/四段, `ActionGroup` space一段..四段 (Action.tab 8/9/10/11) | Space: takeoff triple J1..J5 (JumpParam rows 1..5), apex -> End triple 125/−140/12 (`ModifySprintEndSpeed`) |
| 急坠 | skill 20630 `万花轻功急坠.lua`: `SetPassiveVelocityZ(-2000)` | key 3: vy = −2000 u/f, bypasses the 900 u/f fall cap (engine clamp −2048 u/f) |
| Fly costs | JumpParam school 4: OnFlyCost 75, OnFlyFloatCost 35, OnFlyJumpCost 300, OnFlyBirdMoveCost 206 | 气力值: hover 35/s, bird move (W) 206/s, per stage 300; ground regen 2000/s (provisional, D0 open) |
| Exit | Action.tab 5 【松开W登顶】 (`<MOVEFORWARD;1>`) | release -> StopBirdFly + UnlockBirdMoveZ: velocity persists, gravity integrates Vz, 900 u/f fall cap, 气力值 drain stops (no dash; §7.2) |
| Animation | `player_suspend.krl.txt` ZhiKongQingGong:2 (【万花】) body 6 `StayAnimaiton`; `Tani.rt` 万花加强段跳/俯冲 series | hover/fly = `F1bqg万花加强滞空_01.tani`; stages = 加强一段跳b / 加强二段跳a / 加强三段跳a / 加强二段跳b / 加强俯冲b; 急坠 = 加强俯冲b |

**Engine-AV clips (avoid)**: `F1bqg万花四段跳a_空.tani` and `F1bqg万花俯冲a01.tani`
crash the engine host with 0xC0000005 when played airborne (isolated 2026-09-30;
both load fine as idle clips — same class as the documented `f1b02yd二段跳a.tani`
AV). Stage 4/5 therefore substitute 加强二段跳b / 加强俯冲b; both survive a 5 s
airborne window.

Not modelled yet: the 弈韵一~六段 branch (Condition.tab JC6..JC11), the 双人/踩人
rows, the exact per-stage action→animation mapping (Action.tab labels vs
Condition.tab comments), the exact `CONSUME_BASE` value, the 气力值 regen D0, and
whether `SetTimer(30)` counts 15 Hz logic frames (2 s) or ms-scale frames.

### 7.2 Exit: release-W (登顶) and the automatic fly ends (traced 2026-09-30)

Release W during the 大轻功 → `MoveForwardStop` → UI action 5 **【松开W登顶】**
(`Sprint/Action.tab`, `<MOVEFORWARD;1>`). Two outcomes:

1. **Near a summit point → 登顶 (summit).** The stage rows have 登顶 variants
   (`Sprint/Condition.tab` school 4, `CanTowerFlag=1`, ActionGroup contains 5).
   - detection cone: `Represent/doodad/doodad_summit.krl.txt` — `angle=45`,
     `horizontal=[0,6000]`, `vertical=[0,5600]`, `v = angle*x + horizontal*y + vertical*z`;
     `number.krl.txt`: `SummitDistance=20000`, `SummitFadeTime=2000` ms, `SummitAdjustY=30`.
   - the character plays the 踩点/踩尖 performance
     (`Represent/player/player_summit.txt`: per 体型×门派, A=起跳(原地) /
     B=过程(飞行) / C=到达(终点) animations, 总持续时间 ~1846–2000 ms, 水平夹角;
     万花 = `*bqg万花踩尖_上a01/b01.tani`), then stands
     (`KRLCharacter::{QinggongSummit, StandSummit, ForceStandSummit,
     TerminateQinggongSummit}`, `KRLSummit::{Enter, Stand}`, event
     `STKREPRESENT_EVENT_QINGGONG_SUMMIT`); UI "当前处在轻功登顶状态".
2. **Else the fly ends.** Buff **13422 全门派战斗轻功状态监控** (added by the
   school trigger's OnTimer) ends; its `ScriptFile = skill/轻功/轻功状态结束处理.lua`
   `OnRemove` (extracted script, HIGH) runs:
   - `player.StopBirdFly()` — leave the BirdFly state,
   - `player.UnlockBirdMoveZ()` — release the trigger's Z lock,
   - `enable aircombat camera 0` — camera back to normal.
   The horizontal speed persists (speed+heading vector, §4) and gravity now
   integrates Vz → the forward-down glide; the Sprint.tab fall cap (900 u/f) and
   the landing rules apply; the 气力值 drain (buff 13422 `ActiveAttrib1 =
   气力值持续消耗.lua`) stops.

**Automatic fly ends** (`气力值持续消耗.lua` `Apply`, HIGH): `nSprintPower == 0` →
`StopBirdFly + UnlockBirdMoveZ`; altitude `Flyheight < 6*8*64 = 3072 u`
(≈16 m at 192 u/m; ≈30.7 m at 100 u/m) → same. The same script applies the
control-state 气力值 penalties (锁足 −1000, 定身 −2000, 眩晕 −3000, 击倒 −4000 per
tick; /4 with the mount 10447) and the GF-map regen (+500/+2000 per s).

Engine states (RVAs): `KCharacter::FlyTo` `0x140310B70` (0x20→0x1F),
`BirdFlyTo` 0x24→0x23, `EndFlyJump` `0x140310960` (requires 0x21, writes Vz
`[+0x270]`, → 4/0xE); landing resets the scripted-move counter `[+0xC08]`.

Sandbox: implemented 2026-09-30 (`wwEndState` in `client/RebornClient.cs`) —
release = the real StopBirdFly + UnlockBirdMoveZ semantics: the fly ends, the
horizontal speed persists (the forward glide), gravity integrates Vz from the
row-0 base gravity (`WwRules.FallGravityPerSecond2` 2475 u/s²), the 900 u/f fall
cap applies, the 气力值 drain stops; the 45° constant-angle dash was removed.
The auto-ends are wired too: 气力值 0 and altitude < `WwRules.FlyMinAltitudeUnits`
(3072 u, checked while flying, not during the launch). `RC_WH_DEMO_RELEASE_MS`
exercises the exit in the demo. Not modelled: the summit state (no summit points
in the cropped sandbox map).

Recast (2026-09-30): WW works on the ground too (`WwRules.Evaluate` — the ground
double-tap is 【WW上冲】, the ground sprint is HOLD W/疾跑段), so after landing you
can cast again once the bar is back to the 10000 gate. The ground regen default
is 4000/s (`RC_WH_REGEN`; D0 open — provisional); the top-right overlay shows
段数 N/5 + the state (上冲/段名/急坠/滑翔) + 气力值 while flying, and
`气力未满 需 10000` while the gate is unmet. `RC_WH_DEMO_NOJUMP` /
`RC_WH_DEMO_RECAST_MS` exercise the ground cast in the demo. The 气力值 max is
not in the client tables (server/attribute-side) — the sandbox uses 10000
(provisional); note the gate is the *bar at 10000*, not "enough for the cost"
(all school triggers use the same check).

### 7.3 Full-system audit (2026-09-30) — the generators of 大轻功

Re-derived from the client after user feedback that the sandbox does not match
the live game. **The system is a composition of several generators**, and the
earlier sandbox implemented only part of the solo base chain:

| Layer | Client source | State |
|---|---|---|
| WW entry | skill `37891 双w进入轻功技能` (`轻功/轻功通用/双w进入轻功技能.lua`, compiled): the mount checks (special mounts → red warning; `tSpecialHorse`, `GetEquippedHorse`), the 龙门寻宝 map check, the dispatch to the school trigger | not modelled (mount checks) |
| School trigger | `20628 万花轻功触发`: CanCast 气力值 ≥ 10000, cost `100*CONSUME_BASE`, `SkillMove 336` launch, `SetTimer(30)` → `BirdFlyTo` + `LockBirdMoveZ`, buffs 13422(lv3)/14626/13836 | modelled |
| Fly monitor | buff `13422`: `atFlyFlag`, `atMoveSpeedPercent 1024`, the stage skill icons (21124/21130/21132/21133), `ActiveAttrib1 = 气力值持续消耗.lua` | partly (drain) |
| Stage chain (solo) | Condition.tab JC1..JC5 (纵跃段/一段/二段/三段/四段) → JumpParam J1..J5 + End triple | modelled |
| **Air dash** | skill `20788 通用空中冲刺` → `20789` (learned by the trigger): `DashToPitchDirection(480, nFaceDirection, 160)`, buff `14561` (免控/CC immunity), buff `14129` (空中持续冲刺换二段; `ScriptFile = 轻功/通用持续冲刺结束.lua` → `Stop()` + skill-move camera + SFX hide; `atDriftFlag`; icon 20788→20789), `Fly_Skill` fullscreen SFX, `nDashFrame` 100/12/14/16 | **modelled 2026-09-30**: the fly double-tap W/S/A/D = 上冲/下冲/右冲/左冲 (`DashToPitchDirection` along the camera pitch; 480 u/f = 7200 u/s x `WhDashFrames` 100; `WhDashCostPerSecond` 375; the end `Stop()`); the CC immunity/drift are logged, not simulated |
| 急坠 | `20630` → `SetPassiveVelocityZ(-2000)` | modelled |
| 登顶/exit | buff 13422 end → `轻功状态结束处理.lua` (StopBirdFly + UnlockBirdMoveZ); summit = `KRLSummit` + doodad_summit cone + `SummitDistance=20000` | exit modelled; summit not (no summit points in the cropped map) |
| 弈韵 branch | Condition JC6..JC11, entered via the 踩人/双人 actions (`6;7;`), the 踩尖/踩点 performance (player_summit.txt), `SpecialSprint` school 4 = 6\|7\|8\|9\|10\|11 | not modelled |
| 双人 | the 双人一段..六段 rows (带人/被带 animations) | not modelled |
| Curve schools | `JumpFrameParam.tab` authored per-frame flight curves exist only for JumpParam 10/11 = **丐帮 (weapon 13) / 苍云 (weapon 14)** | the 万花 has no curve |

**School mapping (verified, closes old open item 5)**: `JumpParam.WeaponMask` =
`1 << (WeaponRequest-1)`; the trigger skills give the weapon per school
(少林 1, 天策 2, 纯阳 3, 七秀 5, **万花 6 → mask 32 → JumpParam school 4**,
唐门 11, 明教 12, 丐帮 13, 苍云 14, 长歌 15, 霸刀 16, 蓬莱 17, 凌雪阁 18,
衍天 19, 药宗 20). The double-jump doc's "10/11 (万花/…)" label was wrong and is
corrected there.

### 7.4 The 万花 大轻功 structure (live-game verified 2026-09-30)

User-described live-game flow, now mapped to the Condition.tab school-4 rows
(JC1..JC11) and implemented in the sandbox:

| Input | Live behaviour | Data |
|---|---|---|
| WW (ground) | 点墨江山·疾跑段 — the fast run (NOT the 大轻功 trigger) | the `<MOVEFORWARD;1>` rows + buffs 12085/12190; `WwRules.Evaluate` ground -> Sprint |
| 疾跑段 + Space | 点墨江山·纵跃段 (the chain entry / takeoff) | JC1; casts the trigger 20628 (gate + `SkillMove 336` launch) |
| Space | 纵跃段 -> 一段 -> 二段 -> 三段 -> 四段 | JC2..JC5 (JumpParam J2..J5); at 四段 the Space has no effect, the player falls |
| Shift at 一段/二段/三段 | enters 点墨江山·棋弈 (弈韵) | JC6; action 4 【shift切入】 on the JC2..JC4 rows |
| Space in 棋弈 | cycles 弈韵一段..弈韵五段; at 五段 Space returns to 弈韵一段 | JC6..JC10 (the SpecialSprint counts 6\|7\|8\|9\|10\|11); the cycle |
| Shift in 棋弈 | 弈韵六段 (俯冲) — the fall-out | JC11; the fly ends (StopBirdFly -> fall) |

The 弈韵 (棋弈) stages are modelled as a **float** (Z stays locked, the poses
cycle) — the JumpParam J6..J10 rows are all `100/-250/8` dives, which descend
into the altitude auto-end within a cycle, contradicting the live "cycle"
behaviour; flagged MED/open (the 弈韵's authored motion is not in the extracted
tables). The 四段 (JC5, the J5 dive) descends into the auto-end/fall as the live
"start to fall" describes.

**Corrections from live-game feedback (2026-09-30)**: (a) the
上冲/下冲/左冲/右冲 double-tap actions are the **长歌 御空** system (Condition
school 13 rows with actions 13/14) — the 万花 fly has no such action; the
sandbox's fly double-tap W is now a no-op and the 20788 dash is bound to key 4.
(b) The 气力值 **UI scale**: the live bar shows ~700-1000 and a full bar flies
~1 min; the scripts' `nSprintPower` is 10x the display (10000 = full = displayed
1000), and 10000 / OnFlyBirdMoveCost 206/s ≈ 48 s of bird-move flight. The HUD
now displays the UI scale (`WwRules.WhPowerUiScale` 10) and the gate reads
"需 1000 (满)".

**Consequence for the sandbox**: the 万花 solo chain data (J1..J5 + End), the
launch and the **空中冲刺 dash** are now modelled; the 弈韵 (needs a 踩人 target
player), the 双人 (needs two players) and the 登顶 summit (needs summit doodads
in the map) remain unmodelled — none can be entered in a single-player sandbox.
The tuned hold-W fly-forward value (150 u/f, `RC_WW_FWD`) came from the 丐帮
curve entry — a cross-school borrow; the 万花's own bird-move speed is not in the
extracted tables (open). In-flight double-tap W no longer re-casts the trigger
(that was the wrong "气力未足" on a 7k bar) — it is the 上冲 dash; the 10000 gate
applies only to the initial cast (all school triggers, HIGH).

## 8. Open items

1. `CONSUME_BASE` numeric (engine/server script VM) — not in client scripts.2. Air-steering gain/turn-rate in `ProcessAcceleration` (exact input→heading math).
3. `门派轻功触发区分.lua` referenced by skill 10548 but absent from the paks.
4. Fly curves ship only for schools 10/11 (`JumpFrameParam.tab`).
5. `JumpParam` SchoolID ↔ playable school mapping (weapon-mask keyed).

## 9. Reproduce

```powershell
# tables (read-only extraction)
& "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe" proof\controls\sprint\pathlist.txt proof\controls\sprint\out
# per-frame law + fall model
.venv\Scripts\python.exe tools\gravity\verify_model.py
# sandbox run (cropped map)
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
set RC_CLIENT_EXE=reborn_client_mini.exe  &  client\build_client.cmd
C:\SeasunGame\MovieEditor\bin64\reborn_client_mini.exe
```
