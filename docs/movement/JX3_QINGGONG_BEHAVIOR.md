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

## 8. Open items

1. `CONSUME_BASE` numeric (engine/server script VM) — not in client scripts.
2. Air-steering gain/turn-rate in `ProcessAcceleration` (exact input→heading math).
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
