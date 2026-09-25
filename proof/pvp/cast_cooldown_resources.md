# JX3 PvP — Casting, Cooldowns/GCD, Resources, Talents & Gating

**Branch:** `research/jx3-pvp-battle`
**Date:** 2026-09-24
**Method:** read-only static extraction. All tables GB18030 TSV. No game/install writes.

**Primary evidence paths**

| What | Path |
|---|---|
| Skill master (41142 rows × 117 cols) | `PROBE\logic-skill-prefixed-out\settings\skill\skills.tab` |
| Cooldown master (3510 rows) | `reborn-netcode\proof\netcode\mode_juejing\pak_out2\CoolDownList.tab` |
| Map master (677 rows) | `...\pak_out2\MapList.tab` |
| SkillMove (918×757) / CharacterAction | `...\pak_out2\SkillMove.tab` |
| Scripts | `PROBE\ability-matcher\extracted\scripts\skill\**` (877 plaintext + 296 Lua 5.1 bytecode) |
| Include API | `PROBE\ability-matcher\extracted\scripts\Include\{Skill.lh,Player.lh,NewSkill.lh,LogicConst.lh,ClearCoolID.lh}` (bytecode) |
| Talent tables | `PROBE\logic-skill-out\settings\skill\{TenExtraPoint,ProxySkill,SkillCollection,SkillLearning,BitOpSchoolMountMask}.tab`, `...\logic-skill-prefixed-out\settings\skill\{recipeSkill,DynamicSkillGroup,PlatformKungfu,SurplusSkill,WeaponMapSkill,MainKungfuInfo,KungFuExp}.tab` |
| Tooltips | `PROBE\ad-desc-probe-out\ui\Scheme\Case\Skill.txt`, `Buff.txt` |
| Attributes | `Attribute.txt` dump → `proof\pvp\attributes\attribute_raw.tsv`; Buff attrs → `proof\pvp\attributes\buff_attr_names.tsv` |
| Engine strings | `proof\netcode\JX3ClientX64_exe_net_strings.txt` (Logic edit op copy also present) |

**Tools written** (all under `tools\pvp\`, read-only w.r.t. the game):
`lua51_disasm.py` (Lua 5.1 bytecode → globals/calls), `dump_func.py`, `find_funcs.py`,
`pvp_probe.py`, `filter_skills.py`, `scan_cast_resources.py`, `analyze_cast.py`,
`scan_bytecode_cd.py`, `analyze_hitstiff.py`, `write_evidence.py`, `build_usage_catalog.py`,
`report_resources.py`, `resource_phrases.py`, `find_talent_buffs.py`, `agg_*.py`.

---

## 1. Casting model — instant / cast-time / channel

### 1.1 Constants (HIGH)

Disassembled from `Include\Skill.lh` main chunk (`proof\pvp\cast\skill_lh_globals.txt`):

| Constant | Value | Evidence |
|---|---|---|
| `GAME_FPS` | 16 | `GAME_FPS = GLOBAL.GAME_FPS` (engine global). Script comments: `--吟唱帧数,16帧等于1秒` (e.g. `skill\天策\天策_虎牙令_徐如林.lua`); `ModifyCoolDown(2279, -16 * 40)` in `skill\沙漠风暴\绝境·临时飞爪.lua` = "−40 秒" |
| `LENGTH_BASE` | 64.0 | Skill.lh pc=12–13 |
| `HEIGHT_BASE` | 512.0 | Skill.lh pc=14–15 |
| `PERCENT_BASE` | 1024.0 | Skill.lh pc=16–17 |
| `CONSUME_BASE` | 100.0 | Skill.lh pc=18–19 |
| `PRE_FRAMES` | 80.0 | Skill.lh pc=20–21 |
| `MELEE_ATTACK_DISTANCE` | `ZZBS * 64` | Skill.lh pc=22–24 |
| `DISTANCE_DIS` | `XWJD_1 * 64` | Skill.lh pc=25–27 |

### 1.2 Script fields (HIGH, from `skill\Default.lua` API doc + code)

| Field | Meaning (verbatim comment) |
|---|---|
| `nPrepareFrames` | 吟唱帧数 (cast time, frames) |
| `nMinPrepareFrames` | 急速效果最小吟唱帧数；默认 -1 = 不受急速影响 |
| `nChannelFrame` | 通道技持续时间，单位帧数 |
| `nMinChannelFrame` | 配合急速最小间隔；-1 = 不整体加速 |
| `nChannelInterval` | 通道技间隔时间 (tick period, frames) |
| `nMinChannelInterval` | 急速效果最小通道技间隔；-1 = 不受急速影响 |
| `bInstantChannel` | 通道技能是否立刻造成一次伤害 |
| `bIgnorePrepareState` | 技能是否可在吟唱中施放（吟唱/通道/蓄力技不能填 true） |

### 1.3 Table-side flags (skills.tab)

- `CastMode` (col 10) enum strings: `TargetSingle` 14028, `CasterSingle` 13231, `CasterArea` 6420,
  `PointArea` 2057, `Sector` 2027, `Rectangle` 1508, `TargetChain` 640, `TargetArea` 339,
  `PartyArea` 186, `CasterAreaOfDepth` 116, `PointRectangle` 83, `CasterSpreadCircle` 80,
  `TargetRay` 72, `TargetLeader` 59.
- `IsChannelSkill` (col 20) = 1 → 698 rows; `IsPassiveSkill` (19) = 1 → 3473;
  `IsAutoTurn` (24) = 1 → 369; `UseCastScript` (32) = 1 → 7094.
- Engine enum names seen in strings: `scmItem`, `scmTargetSingle`, `scmTargetArea`, `scmTargetTeamArea`
  (`JX3ClientX64_exe_net_strings.txt` line 5989).

### 1.4 Verified examples

| Skill (ID) | Script | Values | Derivation |
|---|---|---|---|
| 绝境_四象轮回（武器初始） (64889) | `绝境战场\绝境_四象轮回.lua` | `nPrepareFrames = 1.5 * GAME_FPS` (=24 f = **1.5 s**), `nMinPrepareFrames=1`, `nMaxRadius=25*LENGTH_BASE`, `SetPublicCoolDown(16)`, `SetCheckCoolDown(1,444)` | tooltip 读条 1.5 s |
| 绝境_阳明指（武器初始） (64894) | `绝境战场\绝境_阳明指.lua` | `nPrepareFrames=24` (1.5 s), `nMinPrepareFrames=1`, `nChannelInterval=48` (DoT tick) | cast-time |
| 绝境·兰摧玉折 (64995) | `绝境战场\绝境_兰摧玉折.lua` | `nPrepareFrames=16` (**1.0 s**), `nMinPrepareFrames=1`, `nChannelInterval = 80*HDJueJingSkillCoe` | cast-time + DoT interval |
| 绝境_笑醉狂 (65672) | `绝境战场\绝境_笑醉狂.lua` | `nChannelFrame=144` (**9 s**), `nChannelInterval=16` (**1 tick/s**), `nMinChannelFrame=1`, `nMinChannelInterval=1` | true channel |
| 沙漠风暴 道具_笑醉狂 | `沙漠风暴\道具_笑醉狂.lua` | `nChannelFrame=153`, `nChannelInterval=17` | channel |
| 沙漠风暴 道具_风来吴山 | `沙漠风暴\道具_风来吴山.lua` | `nChannelFrame=84`, `nChannelInterval=10`, `nMinChannelFrame=1` | channel |
| 回雪飘摇 (565) | script not in client extract | tooltip: 2.25 s / 3 ticks → 0.75 s tick = 12 f | tooltip-derived (MED) |

**Pipeline (MED — fields HIGH, ordering inferred):** cast request → validate (§8) → check public CD
(`SetPublicCoolDown` CD id) + normal/check cooldowns → if `nPrepareFrames>0` enter PREPARE for the
haste-scaled frame count (floor `nMinPrepareFrames`) → apply effects → if `nChannelFrame>0` enter
CHANNEL for `nChannelFrame` frames, ticking every `nChannelInterval` (floors `nMinChannelInterval` /
`nMinChannelFrame`) → GCD applied. Server-authoritative (protocol `OnSkillPrepare/Cast/Channel/
EffectResult`, `REBORN_SERVER_SPEC.md:130`).

**Open:** exact state machine (when GCD is applied relative to prepare), `PRE_FRAMES=80` semantics,
`HDJueJingSkillCoe` scaling base.

---

## 2. GCD / 公共调息 (HIGH)

### 2.1 The GCD is a CoolDownList row, not a hardcoded timer

`skill.SetPublicCoolDown(N)` takes a **CoolDownList.tab ID**. Verified row:

```
ID  Duration MinDuration note                Usage MaxCount MaxDuration CanBackup MaxOverDraftCount CanAccelerate NeedSyncOB
16  1.5       0           江湖_技能公共CD     11    1        3                      0                 0             1
```

`skill\Default.lua` template line: `--skill.SetPublicCoolDown(16); -- 公共CD 1.5秒`.
`Duration` is **seconds** (1.5), so the game GCD = **1.5 s**; in frames that is 24 f at 16 fps.

### 2.2 Counts (comment-aware scan; `proof\pvp\cast\skill_cast_fields.tsv` + `scan_bytecode_cd.py`)

| Call | plaintext active | bytecode | examples |
|---|---|---|---|
| `SetPublicCoolDown(16)` | 159 | 97 | 绝境_四象轮回, 风来吴山, 商阳指 |
| `SetPublicCoolDown(503)` | 1 | 7 | 明教_圣明佑, 明教_净世破魔击, 幽月轮 |
| `SetPublicCoolDown(590)` | 2 | 8 | 丐帮_亢龙有悔, 龙战于野, 棒打狗头 |
| `SetPublicCoolDown(1403)` | 2 | 1 | 蓬莱_翔极碧落, 持雕浮空 |
| `SetPublicCoolDown(938)` | 10 | 2 | 挂件表现交互 (5 s) |
| `SetPublicCoolDown(1559/2437/2502/836/862)` | 1 each | — | 神机车 1 s / 擒拿 1.5 s / 百战 1.5 s / 隐刀 0.5 s / 面具 180 s |

### 2.3 Separate GCD classes (alternate CoolDownList rows)

| CD ID | note | Duration | MaxDuration | CanAccelerate |
|---|---|---|---|---|
| 16 | 江湖_技能公共CD | 1.5 | 3 | 0 |
| 503 | 明教_技能公共CD | 1.0 | 2 | 1 |
| 590 | 丐帮按键GCD | 1.5 | 1.5 | 1 |
| 1403 | 雕系技能公共CD | 2.0 | 2 | 1 |
| 1559 | 神机车公共CD | 1.0 | 1 | 1 |
| 2437 | 擒拿GCD | 1.5 | 3 | 0 |
| 2502 | 百战通用gcd | 1.5 | 1.5 | 0 |
| 938 | 表现交互公共CD5秒 | 5 | 5 | 0 |
| 836 | 隐刀0.5 | 0.5 | 0.5 | 1 |
| 3086 | 烟雨行保护CD | 1.25 | 1.25 | 1 |

### 2.4 Opt-out / GCD modification

- **Opt-out = never call `SetPublicCoolDown`** in the skill's `GetSkillLevelData`. Skills whose script
  omits it have no public CD.
- Tooltip-confirmed per-skill behaviour:
  - skill 442 御, `<TALENT 18239 1 可以在公共调息时间中施展…>` → castable during GCD.
  - skill 565 回雪飘摇, `<TALENT 24980 1 …施展后会进入公共调息时间>` → talent adds a GCD to a channel that otherwise has none.
- Recipes explicitly named "不占GCD / 不占用GCD" exist (`recipeSkill.tab` col 28):
  RecipeID 2828 巨门去掉公共调息 → SkillID 25142; RecipeID 2934–2939 (五毒 灵蛇引/天蛛引/碧蝶引/玉蟾引/风蜈引/圣蝎引);
  RecipeID 2995 青川濯莲; RecipeID 2560 断泽惊鸿掠水. **The recipe Lua files are server-shipped
  (not in the client extract)**, so the exact API call used to remove the GCD is **not verified** (LOW).
- GCD reduction:
  - 经脉·燕口 (skill 1571, `经脉\基础系\基础系_减少轻功的公共CD.lua`): reduces 江湖轻功 public CD by 0.5–2.5 s (tooltip).
  - 五毒 凄切 (talent 2940): reduces 夺命蛊/迷心蛊/枯残蛊 public CD by 1.5–3 s + adds interrupt.
  - Generic mechanism: Buff.tab `atModifyCoolDown` value = `(CoolDownListID, deltaFrames)`; e.g.
    buff 5798 纯阳_90级奇穴_减化三清1秒 = `239, -16` (−1 s); buff 6100 八荒归元减调息 = `438, -48` (−3 s).

### 2.5 Engine bindings (HIGH)

`JX3ClientX64_exe_net_strings.txt` lines 8471–8511, 8504–8511, 7758, 8132:
`KSkill::LuaSetPublicCoolDown`, `LuaGetPublicCoolDown`, `LuaSetNormalCoolDown`,
`LuaGetNormalCooldownCount`, `LuaGetNormalCooldownID`, `LuaSetCheckCoolDown`,
`LuaGetCheckCoolDownCount`, `LuaGetCheckCoolDownID`, `KPlayer::LuaGetCDMaxOverDraftCount`,
`KPlayer::LuaGetOverDraftCoolDown`; `KCoolDownList::Init/LoadCoolDownList` +
`GetCoolDownValue/GetMinCoolDownValue/GetMaxCoolDownValue/GetMaxCoolDownStackCount/`
`GetMaxCoolDownOverDraftCount/GetCanAccelerateCooldownFlag/GetCoolDownInfo` (lines 8599–8616).

---

## 3. Cooldown model (CoolDownList.tab)

### 3.1 Column → engine getter mapping (HIGH for first six)

| Col | Name | Engine symbol |
|---|---|---|
| 1 | Duration | `GetCoolDownValue` |
| 2 | MinDuration | `GetMinCoolDownValue` |
| 3 | note | — |
| 4 | Usage | — (see `cooldown_usage_catalog.tsv`) |
| 5 | MaxCount | `GetMaxCoolDownStackCount` |
| 6 | MaxDuration | `GetMaxCoolDownValue` |
| 7 | CanBackup | — (no symbol found) |
| 8 | MaxOverDraftCount | `GetMaxCoolDownOverDraftCount` |
| 9 | CanAccelerate | `GetCanAccelerateCooldownFlag` |
| 10 | NeedSyncOB | — (observer sync, see §3.6) |

**Units:** table values are **seconds** (floats: 1.5, 0.25, 0.0625). Runtime APIs are **frames**
(`ModifyCoolDown(cd, -16*40)`, `atModifyCoolDown(cd, -16)`; GAME_FPS=16). Confidence HIGH.

### 3.2 Charges / 充能 (`MaxCount > 1`)

MaxCount distribution: 1×3223, 2×142, 3×62, 4×3, 5×5, 8×2.

| ID | Duration | MaxCount | note |
|---|---|---|---|
| 437 | 20 | 2 | 天策_霹雳2层充能 |
| 347 | 12 | 2 | 藏剑_君子风_啸日 |
| 433 | 57 | 2 | 纯阳-紫气东来 |
| 955 | 65 | 3 | 紫气东来3层充能 |
| 918 | 16 | 2 | 剑·羽16秒充能 |
| 2617 | 60 | 3 | 洱海绝境_飞爪 |

### 3.3 Overdraft / 透支 (`MaxOverDraftCount > 0`, 35 rows, mostly Usage=71 霸刀)

```
1074  10  10  冰凝姿态调息时间   71  1  10  1  4  1  0
1111  20  20  割据秦宫调息时间   71  1  20  1  3  1  1
1114  45  45  散流霞调息时间     71  1  45  1  3  1  1
600   8   8   龙跃于渊         7   1  8   1  2  1  1
```

Script mechanics (`skill\霸刀\套路及子技能\霸刀_大刀_鸣震九霄.lua`, plaintext):
`local nMaxOverDraftCount, nDraftCount = player.GetOverDraftCoolDown(2113)`;
`player.AddCDTime(2113, 560)`; `player.SetOverDraftCoolDown(2113, nDraftCount + 1)`; `CanCast`
refuses when `nDraftCount == 2`. Skill.lh helper `AddOverDraftCoolDown(player, cdID, skillID)`
(line 4801) does: if `0 < nDraftCount < nMax` → `AddCDTime(cdID, GetSkillCDInterval(skillID, level))`
then `SetOverDraftCoolDown(cdID, nDraftCount+1)`. **Each overdraft use extends the running CD by one
interval and increments the draft counter; further overdrafts are blocked at max.** (HIGH for API,
MED for recovery timing.)

### 3.4 Backup / 保存调息 (`CanBackup`)

`CanBackup=1` 2328 rows, `0` 560, empty 622. No engine symbol / doc comment found. Observed: every
player school row is `1`; `0` appears on some 少林 达摩武诀 rows (e.g. 37 轮回诀) and on NPC CDs.
Semantics **unverified** (likely "cooldown is preserved across stance/kungfu switch or defeat") — LOW.

### 3.5 Haste / 加速 (`CanAccelerate`)

`CanAccelerate=1` 2041 rows. Pattern: player skills that are haste-scalable have
`MinDuration=0` and `MaxDuration=2×Duration` (e.g. 天策-龙牙 `1.5/0/3`, 天策-穿云 `2/0/4`,
明教 GCD `1/0/2`); fixed CDs have `Min=Max=Duration`. Interpretation: `Duration` is the base,
`[MinDuration, MaxDuration]` clamps the accelerated result, `CanAccelerate` gates whether 急速
applies at all. **Confidence MED** (no engine formula recovered).

### 3.6 Observer sync (`NeedSyncOB=1`, 837 rows)

Used for PvP HUD/competitor tracking — `OnSyncBattlefieldCompetitorCDStateRequest` family
(`docs\netcode\JX3_MODE_LOAD_FLOW.md:203`). The GCD row 16 itself has `NeedSyncOB=1`.
`docs\netcode\JX3_MODE_JUEJING_LOGIC.md:50` already noted `NeedSyncOB/CanAccelerate/MaxCount` for
绝境 CDs.

### 3.7 Five representative rows (verbatim)

```
ID   Duration MinDuration note                      Usage MaxCount MaxDuration CanBackup MaxOverDraftCount CanAccelerate NeedSyncOB
16   1.5      0           江湖_技能公共CD           11    1        3                      0                 0             1
45   1.5      1           少林_龙爪手_拿云式        5     1        1.5       1            0                 1             0
437  20       20          天策_霹雳2层充能           1     2        20        1            0                 1             1
1111 20       20          割据秦宫调息时间           71    1        20        1            3                 1             1
2989 45       45          绝境楚河汉界40秒调息       46    3        45        1            0                 1             1
```

---

## 4. Resources (school / kungfu)

Attribute names come from `Attribute.txt` (`proof\pvp\attributes\attribute_raw.tsv`) and Buff.tab
usage (`buff_attr_names.tsv`); skill fields from `Default.lua` + comment-aware script scan
(`proof\pvp\cast\skill_cast_fields.tsv`).

| Resource | Attribute(s) | Skill fields | Example |
|---|---|---|---|
| 内力 Mana | `atCurrentMana`, `atMaxManaBase/Additional/PercentAdd`, `atManaReplenish/Ext/Percent`, `atModifyCostManaPercent` | `nCostMana`, `nCostManaBasePercent` | 绝境_四象轮回 `tSkillData.nCostMana = 29…146` (per level) |
| 职业资源 "Rage" (剑气/墨意/战意/禅那/气点/怒气/刀魂/星运…) | `atMaxRage`, `atCurrentRage`, `atRageReplenish` (tooltip label 剑气) | `nCostRage`, `nAddRage` | 藏剑 夕照雷锋 `nCostRage=20`; 丐帮 落水打狗 `nAddRage=35`; 万花 长针 uses `player.nCurrentRage>=20` (墨意) |
| 能量/神机值 Energy | `atMaxEnergy`, `atEnergyReplenish`, `atModifyCostEnergyPercent` | `nCostEnergy` | 唐门 孔雀翎 `nCostEnergy=10`; 化血镖 `20`; tooltip "消耗10点神机值" |
| 日灵/月魂 Sun/Moon | `atCurrentSunEnergy`, `atCurrentMoonEnergy`, `atSunEnergyAddPercent`, `atMoonEnergyAddPercent`, `atModifyCostSunEnergyPercent` | `nCostSunEnergy`, `nNeedSunEnergy`, `nCostMoonEnergy`, `nNeedMoonEnergy` | 明教 scripts (e.g. `明教_净世破魔击`) |
| 聚气/气点 Accumulate | `atAccumulate`, `atMaxAccumulateValue` | `bIsAccumulate`, `nNeedAccumulateCount` | buff 613 韬光养晦 `atAccumulate=2`; buff 1157 洗髓获得禅拿 `=1`; 少林 五蕴皆空 `bIsAccumulate=true` |
| 体力 Stamina | `atCurrentStamina` | `nCostStamina` | 江湖/轻功 scripts |
| 气力值 SprintPower | `atAddSprintPowerMax/Cost/Revive` | `nCostSprintPower = X * CONSUME_BASE` | 丐帮 烟雨行 `20*CONSUME_BASE` (=2000) |
| 剑舞 (七秀) | tracked as buff stacks (tooltip 剑舞点数) | none found in scripts | 回雪飘摇 consumes 1 剑舞 (tooltip) |

Per-school resource names from tooltip phrase census (`resource_phrases.py`):
内力 154 skills; 破绽 61 (刀宗); 剑气 41 (藏剑); 神机值 39 (唐门); 战意 25 (天策);
墨意 24 (万花); 剑舞 23 (七秀); 怒气 23 (苍云); 禅那 21 (少林); 刀魂/狂意 (霸刀, tooltip 16026
"积累10点刀魂值"); 星运 12 (衍天); 日灵/月魂 10/9 (明教); 气点 9 (纯阳); 药性 8 (北天药宗);
魂灯 5; 醉意/连击 1 (丐帮); 符印 1.

`MainKungfuInfo.tab` (33 rows): `KungfuID, KungfuIndex, ForceID, TalentGroup, KungfuType (T/DPS/THERAPY),
name, AdaptiveType, NonadaptiveType, NoneSchoolKungfu` — e.g. `10002,1,1,1,T,洗髓经,SolarMagic,Physics`.
`KungFuExp.tab` (4 rows): `KungfuID, KungFuLevel, ExpAdd, CostQiPoint`.

**Confidence:** attribute names/fields HIGH; per-school internal unit scales (e.g. 纯阳 sub-skills
`子技能_三才化生_N点气` all carry `nCostRage=10` → 1 气 = 10 units) MED; max values per resource
(藏剑 buff 1728 `atMaxRage=100`) HIGH.

---

## 5. Talents (奇穴) & recipes (秘籍)

### 5.1 Schemas (headers + samples)

| Table | Rows | Columns | Sample |
|---|---|---|---|
| `TenExtraPoint.tab` | 288 | `ForceID, KungFuID, PointID, Type, CostTrain, PointRequireLevel, [SkillID, SkillLevel, RequireLevel, RequireQuestID, SkillColor]×12` | `3,6,1,1,300,108,24896,1,108,0,0,30654,1,108,0,1,15158,…` (少林 易筋经 tier 1: 12 alternatives) |
| `recipeSkill.tab` | 6354 | 35 cols; key: `Type (0=秘籍,3=奇穴/镇派), SkillRecipeType(=base SkillID), SkillRecipeTagMask, SkillID, SkillLevel, PrepareFramesAdd, PrepareFramesPercent, CoolDownAdd1/2/3, MinRadiusAdd, MaxRadiusAdd, CostManaAddPercent, CostRageAdd, DamageAddPercent, ScriptFile, …` | 583 《灵峰剑式·风来吴山》参悟残页 → SkillID 1645, DamageAddPercent=20 (2%) |
| `SkillRecipeMirror.tab` | 0 (header only in HD) | `SkillID, MirrorSkillID` | mobile variant has 98 B |
| `SkillCollection.tab` | 152 | `ID, SkillID, StarCount, Deprecated` | `1,30535,1,0` |
| `DynamicSkillGroup.tab` | 1362 | `ID, CanUserChange, CanCastSkill, MoveStateMask, ActiveSkillID1-32, PassiveSkillID1-8, SubActiveSkillID1-16, MoveStateMask2, TargetAutoSearchID` | ID 2: active 4133/4138/4135/4136, sub 4134 (唐门特殊武器) |
| `PlatformKungfu.tab` | 33 | `HDKungfuID, MobileKungfuID, Force, Desc` | `10003,100053,少林,易筋经` |
| `SurplusSkill.tab` | 30 | `KungfuID, SkillID` | `10002,24788` (破招阳性伤害子技能(母)) |
| `WeaponMapSkill.tab` | 22 | `WeaponType, CommonSkillID, BaseKungfuID` | `wdtSpear,12,41` |
| `BitOpSchoolMountMask.tab` | 21 | `BitOpSchoolID, AucMountMask` | `1,48` |
| `ProxySkill.tab` | 33 | `KungfuID, NpcTemplateID, [SkillID, SkillLevel]×10, [HideSkillID, HideSkillLevel]×10` | kungfu 10002: 232,233,235,258,257,252,240,18604,9007,9003,21693 |

### 5.2 How a talent/recipe modifies a base skill

1. **奇穴 choice:** `TenExtraPoint` row = one talent tier; picking a `SkillColor` slot learns
   `(SkillID, SkillLevel)`. Tooltips embed `<TALENT id level>` so the client can show each choice.
2. **秘籍 deltas:** `recipeSkill` rows are applied by the engine as additive/multiplicative deltas on
   the base skill — cast time (`PrepareFramesAdd` frames, `PrepareFramesPercent` 1024=100%),
   cooldowns (`CoolDownAdd1/2/3` in frames), radius, costs, damage. E.g. 少林练武任务 普渡四方
   `CoolDownAdd1=-48` (−3 s).
3. **Buff-driven talents:** Buff.tab attribute `atSetTalentRecipe` (1426 rows) value = `(SkillID, level)`;
   e.g. buff 1728 藏剑_西子情_莺鸣柳 `atSetTalentRecipe 2910 1` (adds skill 2910 天策_盘蛇_御加2层),
   and `atClearCoolDown` value = CD id (buff 1913 → 176; buff 1915 → 438). Applied on buff begin/end.
4. **Recipe gating:** `SkillRecipeTagMask` + `Skill.lh GetPlayerCDListBySkillRecipeTagMask`
   (line 4879) = iterate `player:GetSkillListByRecipeTagMask(mask)` and collect each skill's
   `GetNormalCooldownID(i)` → the set of cooldowns affected by active recipes.
5. **Dynamic bars:** `DynamicSkillGroup` swaps the active skill bar based on `MoveStateMask`
   (e.g. ID 2 唐门特殊武器 天罗/惊羽, ID 1 赛马). `skills.tab BelongDynamicGroupID` (col 99) marks
   membership (10001/10002/10003 groups exist but are empty client-side).
6. **Surplus/Proxy/WeaponMap:** `SurplusSkill` maps a kungfu to its "surplus" attack skill;
   `ProxySkill` gives the transform/变身 skill set per kungfu; `WeaponMapSkill` maps a weapon type to
   the common attack + base kungfu (e.g. `wdtSpear → 12 / 41`).

**Open / LOW:** the exact client-side selection algorithm for the active variant (which `SkillID` is
displayed/cast when multiple recipes/talents are learned) is engine/server-side; only the data
contracts are verified.

---

## 6. PvP gating — MapBanMask / PlatformType

### 6.1 Semantics

- `skills.tab` col 67 `MapBanMask` is a **32-bit mask**. `MapList.tab` col 23 `BanSkillMask` is the
  map's mask. A skill is banned on a map when `(skill.MapBanMask & map.BanSkillMask) != 0`.
  `4294967167 = 0xFFFFFF7F` (= all bits except bit 7) appears on 风雪稻香村 quest skills.
- `MapList.tab` also has `IsArenaMap` (42), `CanJoinArena` (41), `IsBattlefield` (66), `Type` (8).

### 6.2 Observed bit meanings (MED; inferred from skill families per bit)

| Bit | Value | Skills | Meaning | Example SkillIDs |
|---|---|---|---|---|
| 0 | 1 | 195 | mount / 骑乘骑御 | 53 骑乘, 605 骑御, 4097 骑乘_普通 |
| 1 | 2 | 54 | 传功/经脉 | 67 任脉·神阙, 1029 传功 |
| 2 | 4 | 113 | 烟花/活动道具 | 5228 烟花筒_爆竹, 7254 缝纫_蹴鞠 |
| 3 | 8 | 883 | **竞技场 arena** | 551 心鼓弦(520), 1830 傲血战意_重置疾如风, 1850 傲血战意_加buff |
| 4 | 16 | 116 | **城市/安全区** (siege vehicles) | 14376–14483 攻防神机车/神机台 |
| 5 | 32 | 257 | 经脉/轻功 | 114 督脉·风府, 117 督脉·百会 |
| 7 | 128 | dungeons | 副本 | (map side) |
| 8 | 256 | 云湖天池(40级) | 特殊武器/天灯/食物 | 5210 明教特殊武器, 8971 骑乘_天灯, 9553 金钱花糕 |
| 9 | 512 | **绝境/龙门寻宝** | 复活/打坐/战复 | 17 打坐, 20379 打坐, 259 轮回诀, 3003 妙舞神扬, 2229 涅槃重生, 139 锋针, 27634 灵素还生 |

### 6.3 Map-side examples

- Arena maps (`BanSkillMask=8`, `IsArenaMap=1`): 127 天山碎冰谷, 128 乐山大佛窟, 129 华山之巅,
  137 大漠楼兰, 238 青竹书院, 277/362/529/530/624/696/790.
- Battlefields: 39 四十级云湖天池 = 256; 322 洛道_李渡城 = 2; 415 野狸岛 = 3; 296/297 龙门寻宝 = 512.
- 绝境 maps = 512: 410 海岛绝境, 512 白龙绝境, 532 天原绝境, 645 洱海绝境, 709/715 林海绝境.
- Cities = 16: 6 扬州, 8 洛阳, 15 长安, 108 成都, 172 长安内城, 194 太原, 276/278–281 拭剑园.
- Dungeons = 128: 14 灵霄峡, 17 天工坊, 18 无盐岛, 20 天地三才阵, 28 日轮山城, 32 战宝迦兰.

### 6.4 PlatformType (col 100) / enums

Values: empty 29324, `1` 9713, `0` 2004, `2` 100. `PlatformKungfu.tab` maps
`HDKungfuID → MobileKungfuID` (e.g. 10003 → 100053). The exact enum meaning (PC/mobile/both) is
**not proven** — MED/OPEN. Engine CastMode enum names are `scm*` (§1.3).

`KindType` (col 5): `Physics` 22867, `None` 8544, `LunarMagic` 2952, `SolarMagic` 2174,
`NeutralMagic` 2102, `Poison` 1806, `Adaptive` 437, `Leap` 259.
`FunctionType` (col 6): `Normal` 33060, `Damage` 7447, `Stun` 120, `Daze` 94, `Fly` 93, `Slow` 87,
`Halt` 71, `Enmity` 68, `Silence` 36, `Charm` 34, `Blooding` 19, `Heal` 7, `Fear` 3, `Blow` 1.
`IsPassiveSkill=1` 3473 rows (proc/passive skills) — e.g. 心法 "加buff" skills 1850–1882.

---

## 7. Hit-stiff / beat-back / beat-break

### 7.1 Column stats (skills.tab)

| Col | Name | Non-zero | Values |
|---|---|---|---|
| 26 | CauseBeatBreak | 551 (`1`) | interrupt flag |
| 27 | CauseBeatBack | 3325 (`1`) | pushback flag |
| 28 | HasCriticalStrike | 16347 (`1`) | can crit |
| 112 | HitStiffDelayFrame | 134 | 1–15 (8×24, 2×18, 4×15, 9×14) |
| 113 | HitStiffSkillMoveID | 162 | 881×103, 893×18, 891×7, 888/887×7/5, 906, 901… |
| 114 | HitStiffVelocityXY | 28 | 80, 120, 140, 160, 200, 400 |
| 115 | HitStiffAccelerateXY | 28 | 1000, 2000, 3000, 4000, 6000, 7000 |

### 7.2 Model (MED-HIGH)

- `CauseBeatBreak=1` skills are exactly the **interrupt/silence/stun** family: 183 厥阴指 (万花打断),
  240 抢珠式 (少林), 310 剑飞惊天 (纯阳), 426 天策-破坚阵 (Stun), 466–475 八卦洞玄, 482 天策-崩,
  547 剑心通明 (七秀 Silence). → flag = "can interrupt target's channel/cast".
- `CauseBeatBack=1` on 3325 damage skills incl. all 普通攻击 (7–16), 商阳指 180, 万剑归宗 311 →
  damage pushback of the target's cast bar.
- `HitStiffDelayFrame` = frames after impact before the victim flinch starts.
- `HitStiffSkillMoveID` → `SkillMove.tab` row played on the victim. Rows 881/882/887/891/893/906 have
  `TotalFrame` 13/14/19/23/25/38 with all-zero velocity tracks → pure flinch animation lengths.
- `HitStiffVelocityXY` + `HitStiffAccelerateXY` = knockback displacement for the victim:
  钟林毓秀 189 (120, 2000), 四象轮回_普通 896 (140, 2000), 蝎心 2209 (160, 3000), 迷神钉 3090
  (120, 3000), 流火连星 25132 (200, 7000), 段氏 断脉 38006 / 截阳 38007 / 引窍 38009 (400, 6000).
- Client-side feedback: `Represent\skill\behit_shake.txt` (RepresentID, OscillationCount, MaxOffset,
  Duration **ms**, OscillationType) rows `1: 2/10/100ms`, `2: 5/50/100ms`; `skill_result.txt`
  maps `EffectResultID → (EffectType, EffectID)` hit-result visuals.

---

## 8. Targeting / range / cast-validation checklist

| Col | Name | =1 rows | Examples / meaning |
|---|---|---|---|
| 33 | Use3DObstacle | 17075 | LOS via 3-D obstacles |
| 34 | CheckReachable | 201 | path check (天策-穿云 400, 断魂刺 428, 冲锋 47) |
| 35/36 | IgnorePositiveShield / IgnoreNegativeShield | 25118 / 15810 | shield bypass |
| 40/41 | TargetTypePlayer / TargetTypeNpc | 36996 / 31795 | allowed target kinds |
| 42–49 | TargetRelation None/Self/Ally/Enemy/Dialog/Neutrality/Party/Raid | — | relation mask |
| 50 | TargetHorseStateRequest | — | target mounted state |
| 52 | AutoSelectTarget | — | auto-pick |
| 62 | CastMask | 2983 non-zero, 47 distinct | bitfield, **semantics OPEN** |
| 63/64 | SelfMoveStateMask / TargetMoveStateMask | — | move-state gating |
| 79 | IgnoreCamp | 223 | 烟花/赛马/传功 (4104 双人同骑, 7606 赛马) |
| 81 | IsCheckStealth | 12282 | stealth check/break |
| 92 | IgnoreSilence | 4458 | castable while silenced |
| 98 | IgnoreSelectCheck | — | skip target-select rules |
| 107 | IgnoreRangeBlock | 729 | charges ignore blocking (243 拿云式, 418 突, 426 破坚阵, 428 断魂刺) |
| 110 | IgnoreImmunityCast | 18 | 长歌 DoTs 14287–14294, 五毒 蛊虫狂暴 3459 |
| 111 | IgnoreControl | 88 | 解控 skills: 100 星楼月影, 228 太阴指, 257 锻骨诀, 355 凭虚御风, 371 镇山河, 412 疾如风, 550 鹊踏枝, 1656 啸日 |
| 37 | NeedOutOfFight | — | out-of-combat only |
| 38/51 | SelfOnFear / TargetOnFear | — | fear-state requirements |

**Validation checklist for a cast (synthesised, MED):**
1. public CD (§2) + skill cooldown/charges/overdraft (§3) ready;
2. resources sufficient (`nCostMana/nCostRage/nCostEnergy/nNeed…`, §4);
3. caster not silenced/controlled unless `IgnoreSilence`/`IgnoreControl`;
4. target type (`TargetTypePlayer/Npc`) + relation mask (42–49) + `IgnoreCamp`;
5. range `[nMinRadius, nMaxRadius]`, `Use3DObstacle` LOS, `CheckReachable` path;
6. move-state masks (63/64/82–84) and `IsAutoTurn`/`IsCheckStealth`;
7. map/mode gating `MapBanMask` × `BanSkillMask` (§6), `PlatformType`;
8. shields/immunity flags (35/36/110) resolved by the damage pipeline.
Server is authoritative (`docs\netcode\REBORN_SERVER_SPEC.md:130-137`).

---

## 9. Files written

| File | Content |
|---|---|
| `proof\pvp\cast_cooldown_resources.md` | this report |
| `proof\pvp\cooldown_usage_catalog.tsv` | 55 Usage values → inferred meaning + confidence + sample rows |
| `proof\pvp\cast\skill_cast_fields.tsv` | 1173 scripts × {active fields, tSkillData levels, CD calls, ATTRIBUTE_TYPE names} |
| `proof\pvp\cast\cdlist_gcd_rows.txt` | GCD-relevant CoolDownList rows (verbatim) |
| `proof\pvp\cast\mapban_bits.txt` | MapBanMask bit census + examples + MapList BanSkillMask maps |
| `proof\pvp\cast\hitstiff_examples.txt` | all skills with HitStiff*/CauseBeat* flags |
| `proof\pvp\cast\targeting_flags.txt` | per-flag distributions + examples |
| `proof\pvp\cast\skill_lh_globals.txt` | Skill.lh global assignments (pc + name) |
| `proof\pvp\cast\skill_lh_calls.json` | Skill.lh cooldown/overdraft function call sites |

## 10. Open items

1. Recipe scripts ("不占GCD", "风来吴山透支") are **not shipped client-side** → exact GCD-removal API unverified.
2. `CanBackup` (保存调息) and `CastMask` bit semantics not recovered from symbols.
3. Exact haste formula (`Duration/MinDuration/MaxDuration` clamp) and the prepare/channel state ordering.
4. `PlatformType` 0/1/2 enum meaning.
5. Client-side active-variant selection across `TenExtraPoint` + `recipeSkill` + `DynamicSkillGroup`.
6. `PRE_FRAMES=80`, `ZZBS`, `XWJD_1`, `HDJueJingSkillCoe*` base values.
