# JX3 skill scaling & event data — research notes

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Method:** read-only extraction from the local install + previously extracted
PakV4/PakV5 assets (`jx3-web-map-viewer` cache); no live capture, no client mods.

**Question answered:** can we get the actual numbers for an ability (e.g. dash
distance, attack-power scaling)? **Yes — from skill tables + skill scripts.**

---

## 1. Where the data lives

| Data | Path (extracted copy) | Format |
|---|---|---|
| Skill master table (117 cols) | `...\cache-extraction\pakv4-probe\ad-desc-probe-out\settings\skill\skills.tab` (15.8 MB) | GBK TSV |
| Buff/attribute modifiers | `...\settings\skill\Buff.tab` (14 MB) | GBK TSV |
| Skill realization | `...\settings\skill\SkillRealization.tab` (177 KB) | GBK TSV |
| Skill scripts (formulas) | `...\ability-matcher\extracted\scripts\skill\**` (1173 files) | 874 plaintext + 299 Lua 5.1 bytecode |
| Global skill coefficients | `...\extracted\scripts\Include\Skill.lh` (`tSkillCoefficient[skillID]`) | Lua 5.1 bytecode (160 KB, now parseable) |
| Dash → animation map | `...\skill-tables-out\Represent\skill\skill_dash.txt` | GBK TSV |
| Skill-move → animation map | `...\skill-tables-out\Represent\player\player_skill_move_animation.txt` | GBK TSV |
| Skill-move camera | `...\skill-tables-out\Represent\camera\skill_move_camera.txt` | GBK TSV |

Original source inside the client VFS: `settings/skill/*.tab`, `scripts/skill/**`,
`Represent/skill/*.txt`. Tables/scripts can be re-extracted with
`PakV12345-Extract.exe <pathlist.txt> <outdir>` (GBK pathlist) once paths are known.

---

## 2. Damage scaling with 攻击力 / weapon damage

Skill scripts define formulas in `GetSkillLevelData(skill)` using `skill.*` fields
and `AddAttribute(ATTRIBUTE_TYPE.X, v1, v2)` calls. Verified primitives:

| Primitive | Meaning | Real example (script) |
|---|---|---|
| `skill.nWeaponDamagePercent` | weapon-damage scaling, **1024 = 100%** | 龙牙冲刺 `1024`; 韦陀献杵段一 `2048`; 风来吴山(道具) `2048`; NPC 远程 `1024*1.5` |
| `PHYSICS_ATTACK_POWER_PERCENT(v, 0)` | **attack-power coefficient (外功攻击力)**, 1024 = 100%, negative subtracts | 普渡四方 `(1024,0)`; 横扫千军 `(-1024,0)`; 横扫六合 `(-512,0)`; 韦陀献杵 `(-1024,0)` |
| `SKILL_ADAPTIVE_DAMAGE(mode, coef)` | adaptive damage multiplier (skill coefficient) | 神机台扫射 `(1, 0.8)`; 蚀心蛊 `(1,1)`; 绛唇珠袖 `(1,1)` |
| `SKILL_ADAPTIVE_DAMAGE_RAND(mode, coef)` | random part of adaptive damage | 神机台扫射 `(1, 0.4)` |
| damage-type pairs | `SKILL_PHYSICS_DAMAGE`, `SKILL_SOLAR_DAMAGE`, `SKILL_NEUTRAL_DAMAGE` (+ `_RAND`) then `CALL_*` | 普渡四方 `SKILL_SOLAR_DAMAGE(0)` + `CALL_ADAPTIVE_DAMAGE(1,0)` + `PHYSICS_ATTACK_POWER_PERCENT(1024,0)` |
| `tSkillData[level].nDamageBase` / `.nDamageRand` | flat per-level damage | 风来吴山 levels: `108,124,140,156,172,188,204,220,236,252` (compiled bytecode constants) |
| `tSkillCoefficient[skillID]` | global per-skill, per-level coefficient table (channels, intervals…) | `tSkillCoefficient[180].skillnChannelInterval(level)` (商阳指) |

Model (MED): damage is composed of a flat per-level base/rand, a weapon-damage
component (`nWeaponDamagePercent`), and an attack-power component
(`PHYSICS_ATTACK_POWER_PERCENT` / adaptive-damage coefficient). Exact server-side
aggregation formula is not shipped (server-authoritative).

`skills.tab` also carries coefficient switches: `UseSkillCoefficient` (col 96),
`SkillCoefficient` (101), `DotCoefficient` (102), `SurplusCoefficient` (104) —
in this dataset those cells are usually empty, which is why the scripts above are
the useful source.

---

## 3. Dash / displacement data

Dash is authored as an attribute in the skill script:

| Pattern | Meaning | Real example | Distance |
|---|---|---|---|
| `DASH_BACKWARD(frames, speed)` | backward dash over `frames` at `speed` per frame | 绝境·太阴指 `(16, nSpeed)` with `tSkillData.nSpeed = 60` | 960 units |
| `DASH_FORWARD(frames, speed)` | forward dash | 蹑云逐月 `(8, 70)`; 斗转星移·前 `(6, 128)` | 560 / 768 units |
| `DASH(distance, ?)` | direct displacement form | 龙牙冲刺 `(120, 0)`; 净世破魔击 `(90, 0)` | 120 / 90 units |
| `DASH_LEFT/RIGHT(frames, speed)` | lateral | 斗转星移·左/右 `(6, 128)` | 768 units |

Supporting maps:
- `skill_dash.txt` — `SkillID → AnimationID` for dash animations.
- `player_skill_move_animation.txt` — `SkillMoveID → AnimationID` (rush/move anims).
- Animation root motion is measurable too (`tools/netcode/measure_skill_motion.py`);
  太阴指 root arc peaked at **156.7 units** while the skill script says 960 units —
  confirming the animation is mostly in-place and the script/server drives the
  real displacement.

Units: distances are engine units; `LENGTH_BASE`/`GAME_FPS` are defined in
`scripts/Include/Skill.lh` (constants present: `GAME_FPS`, `LENGTH_BASE`,
`HEIGHT_BASE`, `PERCENT_BASE`, `CONSUME_BASE`, `MELEE_ATTACK_DISTANCE`).
Mapping to meters needs one calibration reference (e.g. a known in-game range) —
**open item**, not guessed here.

---

## 4. Tooling added

| Tool | Purpose |
|---|---|
| `tools/netcode/scan_skill_scripts.py` | decode all plaintext skill scripts → `proof/netcode/skill_scaling_report.tsv` (fields, attributes, per-level data) |
| `tools/netcode/lua51_constants.py` | parse JX3 Lua 5.1 bytecode (32-bit header: size_t=4) and dump every function's numeric/string constants |
| `tools/netcode/measure_skill_motion.py` | extract a tani+ani from PakV4 and measure root-bone displacement per frame |
| `tools/netcode/dump_motion_floats.py` | find authored motion vectors in `.tani` |

Evidence artifacts: `proof/netcode/skill_scaling_report.tsv`,
`proof/netcode/skill_scaling_examples.md`, `proof/netcode/skill_data/*.json`
(`flws_constants.json`, `skill_lh_constants.json`).

Reproduce:

```powershell
python tools\netcode\scan_skill_scripts.py --root "<...>\scripts\skill" --out proof\netcode\skill_scaling_report.tsv
python tools\netcode\lua51_constants.py "<...>\scripts\skill\藏剑\藏剑_灵峰剑式_风来吴山.lua" --json proof\netcode\skill_data\flws_constants.json
```

## 5. Open items

1. Pair `tSkillCoefficient[skillID]` values to skills (instruction-level Lua decode; constants already extracted).
2. ~~Calibrate `LENGTH_BASE`/`GAME_FPS`~~ solved for length: see §6.
3. Attribute meanings for `SKILL_ADAPTIVE_DAMAGE` mode arg (0 vs 1).
4. `Buff.tab` (14 MB, 413 KB `skill_buff.txt`) modifiers — attack buffs/debuffs per class.

---

## 6. Player-facing text and the 尺 unit translation

### 6.1 Where the strings live

| DB | Path | Content |
|---|---|---|
| Skill tooltips | `...\ad-desc-probe-out\ui\Scheme\Case\Skill.txt` (34,583 rows, 32 cols) | per skill+level: `Name, Desc, ShortDesc, SpecialDesc, KungfuDesc, HelpDesc` |
| Buff tooltips | `...\ui\Scheme\Case\Buff.txt` | buff descriptions |
| Attribute labels | `interface\**\lang\zhcn.jx3dat` (e.g. `LM_Tip`) | key → label: `['physics attack'] = "外功攻击"`, `['critical damage base'] = "会心伤害"` … |
| Skill realization | `settings\skill\SkillRealization.tab` | SkillID, SkillLevel, LevelUpExp, LevelUpPlayerLevel, ExpAddOdds, Name, School |

Tooltip text uses markup resolved by the client at runtime:

```
<SUB id level>            embed sub-skill description
<BUFF id level>           embed buff description
<SKILL PhysicsDamage>     computed damage line
<SKILLEx {D0} {SkillPhysicsAP}>   formula placeholder (D0 = coefficient, AP = attack power)
<KUNGFU id level> / <TALENT id level>
```

Examples (level 1 rows, verbatim):

- 风来吴山 (1645): “消耗10点剑气 … 8次 `<SUB 1905 0>` **外功伤害附加200%武器伤害** …” — matches the script constant `nWeaponDamagePercent = 2048`.
- 风来吴山 damage line (1905): `<SKILL PhysicsDamage>（<SKILLEx {D0} {SkillPhysicsAP}>）` — the visible damage number is **attack-power scaled**.
- 太阴指 (228): “门派轻功 … 解除自身控制效果并**向后疾退** … 对周围**8尺**内的目标造成 `<SUB 497 0>` 混元性内功伤害 …”.
- 蹑云逐月 (9003): “江湖轻功，解除被击僵直，**向前冲刺一段距离** …”.

Tool: `tools/netcode/query_skill_tooltip.py --db <Skill.txt> 蹑云逐月`.

### 6.2 The unit conversion: 1 尺 = 64 engine units

Four independent script sites prove the factor:

| Script | Code | Meaning |
|---|---|---|
| `刀宗\套路及子技能\破绽产生.lua` | `local Distance = GetCharacterDistance(...) / 64` | converts units → 尺 |
| `绝境战场\绝境_棒打狗头.lua` | `if nDistance >= 4 * 64 * 4 * 64 then --超过4尺才冲刺` | squared distance, 4尺 = 4×64 |
| `霸刀\套路及子技能\霸刀_大刀_鸣震九霄.lua` | `GetDistanceSq(...) > 15 * 15 * 64 * 64` | 15尺 |
| `少林\判定距离重置捉影.lua`, `绝境_云飞玉皇.lua` | `Distance >= 8 * 64`, `Distance <= 4 * 64` | 8尺 / 4尺 |

So `LENGTH_BASE = 64` (the constant used as `nMaxRadius = 25 * LENGTH_BASE` = 25尺).
If JX3 follows the modern 尺 = 1/3 m, then **1 m = 192 units** (MED; calibrate in
game if exact meters matter).

### 6.3 Worked example — 蹑云逐月 = 20尺

Compiled `江湖轻功_蹑云逐月.lua` (`proof/netcode/skill_data/nieyun_constants.json`):

- `tSkillData` levels → `nDashFrame` = **10 / 12 / 14 / 16** frames
- `GetSkillLevelData` builds `ATTRIBUTE_TYPE.DASH_FORWARD` with speed constant **80** units/frame

| Level | units | 尺 |
|---|---|---|
| 1 | 10 × 80 = 800 | 12.5 |
| 2 | 12 × 80 = 960 | 15 |
| 3 | 14 × 80 = 1120 | 17.5 |
| 4 | 16 × 80 = 1280 | **20** |

The max-level dash is exactly **20尺**, matching the player-visible distance.
Cross-checks: 太阴指 16 × 60 = 960 units = **15尺**; 斗转星移 6 × 128 = 768 = **12尺**;
风来吴山 radius `10 * LENGTH_BASE` = 10尺 (tooltip says 10尺); 龙牙冲刺
`nMaxRadius = 25 * LENGTH_BASE` = 25尺.

Conversion summary: **units = 尺 × 64** (and our measured animation root motion of
157 units for 太阴指 was the in-place animation sway, not the 960-unit script dash).

