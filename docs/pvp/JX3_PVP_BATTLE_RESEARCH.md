# JX3 PvP battle system — full gameplay research (from client data)

**Branch:** `research/jx3-pvp-battle` · **Worktree:** `reborn-pvp` · **Date:** 2026-09-24
**Method:** read-only static extraction from the local JX3 client install and the
previously extracted PakV4 assets, plus targeted disassembly of client binaries.
No live capture, no client modification, no server access.

This is the synthesis document. Per-workstream detail, raw values and citations live in:

| Workstream report | Content |
|---|---|
| `proof/pvp/attributes_and_damage.md` | attribute taxonomy, unit conventions, damage/mitigation pipeline, rating coefficients, 化劲/御劲 |
| `proof/pvp/buff_control_system.md` | Buff.tab semantics, control families, MoveState, DR/immunity, dispel, stacking, mode masks |
| `proof/pvp/cast_cooldown_resources.md` | cast/channel model, GCD, cooldown/charge/overdraft, resources, talents, validation checklist |
| `proof/pvp/pvp_modes_rules.md` | arena / battleground / 绝境战场 / camp rules, maps, bans, currencies |
| `proof/pvp/combat_netcode.md` | C2S/S2C combat opcodes, wire shapes, server-authority gates, reborn proposal |
| `proof/pvp/attr_catalog.tsv`, `proof/pvp/combat_opcodes.tsv`, `proof/pvp/cooldown_usage_catalog.tsv`, `proof/pvp/decay_and_controls.tsv` | machine-readable catalogs |

Confidence labels used throughout: **HIGH** (literal value + cross-check/citation),
**MED** (interpretation strongly implied by names/data), **LOW** (hypothesis).

---

## 0. Canonical constants

| Fact | Value | Evidence |
|---|---|---|
| gameplay frame | **1/16 s** (GAME_FPS=16) | `Include/Skill.lh`; script comments “16帧等于1秒” |
| 1 尺 | 64 engine units = 0.64 m | prior netcode research (`SKILL_DATA_RESEARCH.md` §6) |
| percent base | **1024 = 100 %** (`PERCENT_BASE`) | Skill.lh; Attribute.txt formulas |
| rate base | **100 = 1 %** (for `*BaseRate` attributes) | Buff.tab↔Buff.txt matches |
| consume base | 100 (`CONSUME_BASE`) | Skill.lh |
| engine unit | 1 cm | prior mesh calibration |

---

## 1. Attribute system

Three attribute universes exist (details: `proof/pvp/attributes_and_damage.md` §1):

| Set | Count | Where |
|---|---|---|
| UI character panel | 138 | `ui/Scheme/Case/Attribute.txt` |
| Buff.tab referenced `at*` names | 461 distinct | 19 attribute slots per buff |
| Engine enum (`SO3EnumConvertorX64.dll`) | 694 | full engine attribute list |

### 1.1 Unit conventions (verified)

| Kind | On-disk unit | Example |
|---|---|---|
| `*Base` (等级/rating) | raw rating | `atToughnessBase=162` → “御劲等级提高162点” |
| `*Percent` (修正值) | 1024 = 100 % | `atDecriticalDamagePowerPercent=-154` → 化劲 −15 % |
| `*BaseRate` (率) | 100 = 1 % | `atToughnessBaseRate=3000` → 被会心几率 −30 % |
| `*BaseKiloNumRate` | 1024 = 100 % (MED; one outlier) | `-358` → 化劲 −35 % |
| `*Cof`/`*Coefficient` | ratio | `atBeTherapyCoefficient=-512` → 被治疗 −50 % |

### 1.2 Attribute families relevant to PvP

- **Offence:** per-element attack power / 破防 (`*Overcome`) / 会心 (`*CriticalStrike`) /
  会心效果 (`*CriticalDamagePower`) / 命中 (`*HitValue`) / weapon damage;
  elements = Physics, Solar(阳性), Neutral(混元), Lunar(阴性), Poison(毒性) + `Magic`(内功) + `AllType`.
- **Defence:** `*Shield` (防御等级), `*MagicShield`, per-element `*MagicResistPercent`,
  `atGlobalResistPercent`, `atDecayAllTypeResistPercent` (无视减伤), block/mirror,
  absorb (`atGlobalDamageAbsorb`), mana-shield, reflection.
- **Avoidance:** `atDodge`/`atDodgeBaseRate`, `atParryBase`/`atParryValueBase`/`atParryPercent`.
- **PvP block:** 御劲 `atToughnessBase`/`atToughnessPercent`/`atToughnessBaseRate`;
  化劲 `atDecriticalDamagePowerBase`/`Percent`/`BaseKiloNumRate`; plus
  `atPVXAllRound` (全能), `atAssistedPowerExtAdd` (凝神), `atAllShieldIgnoreAdd` (无视防御),
  `atGlobalDamageFixedAdd` (技能伤害提高).
- **Utility:** `atHasteBase` (加速), `atMoveSpeedPercent`, `atTherapyPowerBase`,
  `atTherapyCoefficient` (治疗成效), `atBeTherapyCoefficient` (被治疗效果 → 减疗).

### 1.3 Rating → percent coefficients (values HIGH, formula not shipped)

`scripts/skill/GlobalParam.lua` (bytecode) sets the client's conversion coefficients
(`proof/pvp/attributes/GlobalParam_coefficients.tsv`):

```
fCriticalStrikeParam 9.985      fCriticalStrikePowerParam 3.679
fDefCriticalStrikeParam 9.985   fDecriticalStrikePowerParam 1.521
fHitValueParam 7.644            fDodgeParam 4.628
fParryParam 5.432               fInsightParam 6.734
fPhysicsShieldParam 6.364       fMagicShieldParam 6.364
fOvercomeParam 11.412           fHasteRate 10.61
fSurplusParam 8.534             fAssistedPowerCof 9.985
fToughnessDecirDamageCof 2.784  fPlayerCriticalCof 0.75
fDecriticalDamagePVPCof 0.9
fDecriticalDamagePowerToPhysicsAttackPowerCof 0.525
fDecriticalDamagePowerToMagicAttackPowerCof 0.584
fDecriticalDamagePowerToMaxLifeBaseCof 10.0
nMaxDecriticalDamagePowerEableConvert 58800
nPhysicsAPToMagicAPCof 1138     nWeaponToMagicAPCof 757
```

**Open (important):** the actual rating→% equation and its level term were NOT found in
any client table/Lua (0 hits for `Level2Percent`-style symbols). The likely site is
`KPlayer::GetAttributeValue` (`JX3ClientX64.exe` file offset `0x00839248`) and requires
decompilation. Treat the coefficient list as inputs, not a formula.

---

## 2. Damage & mitigation pipeline

### 2.1 Damage types

Engine enum (`JX3ClientX64.exe` `0x0080CFB0..0x0080D050`):
`Physics, SolarMagic, NeutralMagic, LunarMagic, Poison, Leap, None, Adaptive, Any`.
Adaptive ordinal (Buff 26578): **1=Physics, 2=Solar, 3=Neutral, 4=Lunar, 5=Poison**.

### 2.2 Skill damage primitives (scripts)

| Primitive | Meaning |
|---|---|
| `SKILL_<TYPE>_DAMAGE` / `_RAND` | flat base + random range of that element |
| `CALL_<TYPE>_DAMAGE` | secondary damage event (DOT tick, extra hit, reflect) |
| `SKILL_ADAPTIVE_DAMAGE(_RAND)` + `CALL_ADAPTIVE_DAMAGE` | adaptive element (ordinal 1..5) |
| `nDamageBase` / `nDamageRand` | per-level flat table (`tSkillData`) |
| `nWeaponDamagePercent` | weapon scaling, 1024 = 100 % |
| `nAttackAttenuationCof` | AOE falloff (1024 = 100 %) |
| `PHYSICS_ATTACK_POWER_PERCENT(v, 0)` | attack-power coefficient, 1024 = 100 % |
| skills.tab `SkillCoefficient`/`DotCoefficient`/`SurplusCoefficient` | table-side coefficients |

Damage = flat per-level base/rand + weapon-damage % + attack-power/adaptive coefficient;
the exact aggregation is server-side.

### 2.3 Layered mitigation model (fields HIGH, ordering inferred)

1. hit check (`*HitValue`/`*HitBaseRate` vs dodge/parry/`atSetHitReduceRate`)
2. dodge / parry
3. shield (防御等级; countered by 破防 `*Overcome` and 无视防御)
4. flat damage reduction (`atGlobalResistPercent`, per-element `*MagicResistPercent`,
   reduced by `atDecayAllTypeResistPercent`)
5. **PvP layer 化劲/御劲**
6. block / mirror shield / mana shield
7. absorb shields (damage + therapy)
8. reflection
9. final modifiers (`atGamePlayDamageFinalCof`, `atGlobalDamageFixedAdd`)

Per-element field names are in `proof/pvp/attributes/damage_type_map.tsv`.

### 2.4 化劲 / 御劲 semantics (player-facing text)

- **御劲 (Toughness)** → 御劲率, reduces the chance to be critically hit:
  `疾如风` “被会心几率降低30%” (`atToughnessBaseRate=3000`); `亘绝`
  “御劲提高100%” (=10000, no crit damage at all). `fToughnessDecirDamageCof=2.784`
  additionally reduces incoming crit damage (MED).
- **化劲 (DecriticalDamagePower)** → damage reduction stat with a dedicated PvP
  coefficient `fDecriticalDamagePVPCof=0.9`; overflow converts to attack power / max life
  (cap 58800). PvP gear carries it (`治疗PVP护手加化劲`, `输出PVP护手减化劲`),
  and many PvP skills debuff it (`追命箭/兰摧/翔极碧落/灭影追风` etc., −6 %…−40 %).
- `28807 萌新助威` is an explicit PvP-only catch-up 化劲 buff, disabled in
  arena / 绝境 / 列星 (tooltip).

---

## 3. Buff system

`Buff.tab` = 61,555 rows × 115 cols. Semantics (full report:
`proof/pvp/buff_control_system.md`):

| Field | Meaning | Confidence |
|---|---|---|
| `FunctionType` (col 3) | mechanical class: Damage, Stun, Slow, ResistDamage, Hot, Halt, Shield, Silence, Charm, Blooding, Enmity, Daze, Fear, Fly, Disarm, Chaos… (18 values) | HIGH |
| `AppendType` (col 6) | merge/slot group; default = own ID; same slot = replace/merge | MED-HIGH |
| `DetachType` (col 7) | **dispel group**; 3/5/7/11 = 阳性/混元/阴性/毒性 增益, 4/6/8/10/12 = same-class 减益 | MED-HIGH |
| `Useage` (col 2) | design-time tag (318 values), not a runtime mechanic | MED-HIGH |
| `IsStackable`/`MaxStackNum`/`IsIntensityStackable` | stacking rules (cap ≤255) | HIGH |
| `Count`/`Interval`/`MinInterval`/`MaxInterval` | tick count / period in frames (e.g. 握针 6×48f = 6 ticks @3 s) | HIGH |
| `CanBeSteal`/`CanTransfer`/`Exclude`/`GlobalExclude`/`UniqueTarget`/`Coexist` | steal/transfer/merge arbitration | MED |
| `MapBanMask`/`MapInvalidMask` | per-map-category mode restriction (see §6.4) | MED-HIGH |

**Dispel mechanics** are script-driven: `DETACH_MULTI_GROUP_BUFF(group…)` and
`DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE(category…)`; e.g. 镇山河 strips
{2,4,6,8,10,12}, 剑转流云 strips {3,5,7,11} (offensive buffs).

---

## 4. Control system & diminishing returns

### 4.1 Control taxonomy

Control is expressed by `FunctionType` strings + a numeric category used by
immunity/dispel scripts (`proof/pvp/decay_and_controls.tsv`):

| id | Category | Typical FunctionType |
|---|---|---|
| 2 | 减速 Slow | Slow |
| 3 | 恐惧 Fear | Fear |
| 4 | 定身 Root | Halt |
| 5 | 沉默/封内 Silence | Silence |
| 6 | 雷霆震怒 (special) | — |
| 7 | 锁足 Root | Charm |
| 8 | 眩晕 Stun | Stun/Daze |
| 9 | 嘲讽 Taunt | Enmity |
| 11 | 击倒 Knockdown | Daze/Stun |

Knockback/pull/displacement are not in this enum; they use rate attributes
(`atKnockedBackRate`/`atRepulsedRate`/`atPullRate` = −1024 → immune).

### 4.2 MoveState

37-name engine enum (`JX3RepresentX64_all_strings.txt:752293`):
`ON_STAND, ON_WALK, ON_RUN, ON_JUMP, ON_SWIM…, ON_SIT, ON_KNOCKED_DOWN,
ON_KNOCKED_BACK, ON_KNOCKED_OFF, ON_SPRINT_*, ON_SKILL_MOVE_*, ON_HALT, ON_FREEZE,
ON_ENTRAP, ON_DEATH, ON_DASH, ON_PULL, ON_REPULSED, ON_RISE, ON_SKID, ON_FLY*, ON_BIRD_*`.
Buff `MoveStateMask` values decode consistently with 1-based bit = ordinal
(打坐 mask 256 = bit 8 = ON_SIT). Skills carry `SelfMoveStateMask`/`TargetMoveStateMask`
(per-skill cast gating).

### 4.3 Diminishing returns (递减) & immunity

`DecayType.tab` (10 rows; frame = 1/16 s; **DecayFrame == ImmunityFrame in this build**):

| id | Name | Decay/Immunity |
|---|---|---|
| 0 | 击晕 | 160 f = 10 s |
| 1 | 冰环 | 160 f = 10 s |
| 2 | 定身 | 160 f = 10 s |
| 3 | 七星拱瑞 | 320 f = 20 s |
| 4 | 雷霆震怒 | 320 f = 20 s |
| 5 | 沉默 | 160 f = 10 s |
| 6 | 眠蛊 | 160 f = 10 s |
| 7 | 迷神钉 | 160 f = 10 s |
| 8 | 短暂锁足 | 96 f = 6 s |
| 9 | 恐惧 | 160 f = 10 s |

Client tables only expose the windows; the per-application DR ladder
(100 %→50 %→25 %→immune) and reset rules are engine/server-side (**open**).

### 4.4 解控 / 免控

Verified per-school immunity sets (attribute `atImmunity=n` + rate −1024):

| Skill | Categories | Displacement |
|---|---|---|
| 生太极 (纯阳) | {2,3,4,7,8,11} | knockdown |
| 星楼月影 (万花) | {2,4,7,8} | knockdown |
| 鹊踏枝 (七秀) | {2,4,7,8} | knockdown |
| 疾如风 (天策) | {2,3,4,6,7,8,11} | knockdown |
| 笑醉狂 (丐帮) | {2,3,4,7,8,11} | all displacement |
| 镇山河 (纯阳) | {2,3,4,5,7,8,11} + 无敌/block | all displacement |
| 任驰骋 (天策) | {2,3,4,6,7,8,11} | knockback/repulse (+pull L2) |
| 转乾坤 (纯阳) | {2,4,7,8} (L2 +repulse/pull) | knockdown |

Pattern: most 解控 skills cover {2,4,7,8} and explicitly **exclude knockback/pull**
(tooltips “…(击退和被拉除外)”). `IgnoreControl=1` skills (73) are castable while controlled.

---

## 5. Casting, cooldowns, resources

### 5.1 Cast model

`nPrepareFrames` (吟唱, frames), `nChannelFrame` + `nChannelInterval` (channel duration/tick),
`nMin*` variants for haste floors, `bInstantChannel`, `bIgnorePrepareState`.
CastMode (col 10) is the shape: `TargetSingle`, `CasterArea`, `PointArea`, `Sector`,
`Rectangle`, `TargetChain`, `TargetRay` … (14 values).
Verified examples: 四象轮回 24 f = 1.5 s cast; 笑醉狂 144 f channel, 16 f tick.

### 5.2 GCD (公共调息)

The GCD is a **CoolDownList row**, not hardcoded: `SetPublicCoolDown(16)`.
Row 16 = `江湖_技能公共CD`, **Duration 1.5 s** (`MaxDuration 3`, `CanAccelerate 0`,
`NeedSyncOB 1`). Alternate GCD classes exist per school/vehicle: 明教 503 (1.0 s),
丐帮 590 (1.5 s), 雕系 1403 (2.0 s), 神机车 1559 (1.0 s), 百战 2502, 隐刀 836 (0.5 s).
Skills opt out by never calling `SetPublicCoolDown`.

### 5.3 Cooldown model

`CoolDownList.tab` (3,510 rows; on-disk **seconds**, runtime **frames**):

| Column | Meaning |
|---|---|
| Duration / MinDuration / MaxDuration | base and haste clamp range |
| MaxCount | charges (142 rows ×2, 62 ×3 …) |
| MaxOverDraftCount | 透支 charges (35 rows, mostly 霸刀) — each use extends CD + increments draft |
| CanBackup | CD preserved (likely stance/defeat) — LOW |
| CanAccelerate | haste may accelerate; server sends `OnAccelerateCDTimer` |
| NeedSyncOB | exposed to competitor/observer sync (837 rows) |

### 5.4 Resources

| Resource | Attributes | Skills |
|---|---|---|
| 内力 Mana | `atCurrentMana`, `atModifyCostManaPercent` | `nCostMana` |
| school resource (剑气/墨意/战意/禅那/怒气/刀魂/星运…) | `atCurrentRage`/`atMaxRage`/`atRageReplenish` | `nCostRage`/`nAddRage` |
| 神机值 Energy (唐门) | `atMaxEnergy` | `nCostEnergy` |
| 日灵/月魂 (明教) | `atCurrentSunEnergy`/`atCurrentMoonEnergy` | `nCostSunEnergy`/`nCostMoonEnergy` |
| 气点/聚气 (纯阳/少林) | `atAccumulate`/`atMaxAccumulateValue` | `bIsAccumulate`/`nNeedAccumulateCount` |
| 体力/气力值 | `atCurrentStamina`, `atAddSprintPower*` | `nCostStamina`, `nCostSprintPower` |

Per-school names/counts: 内力 154, 破绽 61 (刀宗), 剑气 41 (藏剑), 神机值 39 (唐门),
战意 25 (天策), 墨意 24 (万花), 剑舞 23 (七秀), 怒气 23 (苍云), 禅那 21 (少林), 星运 12 (衍天),
日灵/月魂 10/9 (明教), 气点 9 (纯阳), 药性 8 (北天药宗)…

### 5.5 Talents (奇穴) / recipes (秘籍)

`TenExtraPoint.tab` (288 rows) = talent tiers with up to 12 alternatives;
`recipeSkill.tab` (6,354 rows) = additive deltas on a base skill
(cast time frames/%, cooldown adds, radius, cost, damage %);
`DynamicSkillGroup.tab` swaps bars by move state; `ProxySkill`/`SurplusSkill`/
`WeaponMapSkill` map kungfu/weapon → skill sets. Buff attributes
`atSetTalentRecipe` and `atClearCoolDown` apply/clear talents at runtime.

### 5.6 Cast validation checklist (synthesised)

1. public CD + skill CD/charges/overdraft ready
2. resources sufficient
3. not silenced/controlled unless `IgnoreSilence`/`IgnoreControl`
4. target type + relation mask + `IgnoreCamp`
5. range `[nMinRadius,nMaxRadius]`, LOS (`Use3DObstacle`), path (`CheckReachable`)
6. move-state masks (self/target), stealth (`IsCheckStealth`), auto-turn
7. map/mode gating `MapBanMask × BanSkillMask`, `PlatformType`
8. immunity/shield flags (`IgnoreImmunityCast`, `IgnorePositive/NegativeShield`)

---

## 6. PvP modes & rules

### 6.1 Arena — 名剑大会 (11 map instances, `IsArenaMap=1`)

天山碎冰谷 127 · 乐山大佛窟 128 · 华山之巅 129 · 大漠楼兰 137 · 青竹书院 238 ·
拭剑台 277 · 墟海之眼 362 · 藏剑武库 529 · 墟海之眼_演武场 530 · 沃石火狱 624 ·
南诏段氏_红叶泽 696.
Flags: `BanSkillMask=8`, `BanUseItemMask=1`, `InvalidBuffMask=8`, `CampType=4`,
`ReviveInSitu=0`, `RevieCycle=0`, `OperationMask=7`, `QueueForSwitchWhenFull=1`.
- 529 arena-banned skills (bit 8): all PvE legendary-weapon procs, PvE set bonuses,
  and the **PVP gear passives** themselves (输出/治疗PVP护手, PVP项链, PVP附魔).
- Food/flask/feast and class raid buffs removed by system buffs 3369-3373, 3387-3392.
- **Healing dampening:** `30633 名剑大会·对局火热` — final healing decreases over the match
  (`atGamePlayTherapyFinalCof=-71` per row); 5v5 robot variants 16654/17644.
- Prep/talent rules: 竞技场准备时间_可奇穴切换 (11839), 倒计时_无法切换奇穴 (11840),
  竞技场禁止换心法 (11837), 准备阶段清cd (33311).
- Anti-idle: 11881 三层 → cannot queue; 11880 消极参战惩罚.
- Arena is recorded/observer-synced (`KVideoReplayer::OnSyncArena*`, `bOBFlag`).

### 6.2 Battleground — 战场 (16 instanced maps + 10 BR maps)

神农洇 38 · 云湖天池 39/52 · 珍珑棋谷 48 · 丝绸之路 50 · 三国古战场 135 · 浮香丘 186 ·
李渡鬼域 322 · 列星岛 412 (MOBA) · 野狸岛 415 · 羊村 589 · 帮会联赛 689 (210 players) ·
雪域关城 712 · 扬刀大会 790 · 大富翁 801 · 鹅鸭大游园 807.
Typical flags: `ReviveInSitu=0`, `RevieCycle=160` (≈10 s, MED), `CampType=5`,
per-map `BanSkillMask` (256/512/2/3…), `InvalidBuffMask=256`.
Objectives/scoring are **server-only** (no client `ScriptFile` except 807); the client has
`GetBattleFieldObjective` and objective buffs (开旗/站旗/结算 markers). Rotation is
scheduled in `Activity.tab` from 12:00 (46,740 s).

### 6.3 绝境战场 — battle royale (10 maps)

龙门寻宝/龙门绝境 296/297/676/677 · 海岛/沧溟绝境 410 · 白龙绝境 512 · 天原绝境 532 ·
洱海绝境 645 · 林海绝境 709/715.
Flags: `BanSkillMask=512`, `BanUseItemMask=4/32`, `InvalidBuffMask=8`, `CampType=5`,
`BanChangeTalent=1`, `ReviveInSitu=0`, `RevieCycle=160`.
Combat-relevant findings: 绝脉 debuff 29897 (−1 % cooldown recovery/stack; 截阳 applies ×3),
绝脉爆炸 (40-unit radius), 狂怒 +10 % damage, 乱武 poison-zone/bombing scripts,
洱海 camp revive 26073/26074, 天原 active revive 31507.
Loading/loot/queue flows are documented in `docs/netcode/JX3_MODE_JUEJING*.md`.

### 6.4 Mode restriction encoding (two parallel mask systems)

```
skill banned on map  ⇔ (skills.MapBanMask   & MapList.BanSkillMask)   != 0
buff invalid on map  ⇔ (Buff.MapInvalidMask & MapList.InvalidBuffMask) != 0
```

Map-category bits: 1=骑乘, 2=传功, 4=烟花, **8=竞技场(+绝境)**, 16=城市/攻防载具,
32=经脉/轻功, 128=副本, **256=战场**, **512=绝境**. Client engine reads
`MapInvalidMask` at buff load; `MapBanMask` enforcement is server-side (MED).

### 6.5 Camps / open-world PvP

`RelationCamp.tab`: camp 1 vs camp 2 = hostile (−1), neutral 0. 浩气盟 25 / 恶人谷 27
(`NeedCampBuff=1`). `CampLevelParam.tab`: score 1500→8000 for L0–L10, death 威望 loss
scales with camp level. 杀气/红名/大唐监狱 (5315/5427, `OnSetPlayerRedName`, wanted APIs),
帮战 45 maps + 149 雪原争锋, 攻防 vehicles (神机车/神机台, `MapBanMask=504`).
`IgnoreCamp=1` on 223 skills — all social/cosmetic, never combat.

### 6.6 PvP currencies (client-visible evidence only)

名剑币 (arena; cutoffs 2000/2200), 威名/威望 (`atAddPrestigePercentForAll`),
战阶 (2538-2540, 9629-9631), 竞技分 (`OnSyncArenaLevel`), 飞沙令 (BR/鬼域/列星 weekly).

---

## 7. Combat netcode (server-authoritative)

Full detail + disassembly: `proof/pvp/combat_netcode.md`. Headlines:

- **C2S intents (IDs HIGH, from builder disasm):**
  `DoCastProfessionSkill 0x49/32 B`, `DoCharacterSkill 0x1B/29 B`,
  `DoStartHoardSkill 0x103/33 B`, `DoCastHoardSkill 0x104/31 B`,
  `DoPlayerReviveRequest 0xB9/15 B`, `DoApplyCharacterBuffList 0x20/15 B`,
  competitor sync requests `0x161/0x162/0x164`, OB skill list `0x71`.
  There is **no target-select opcode** — target choice is client-side only.
- **Damage/heal arrives in one message:** `OnSkillEffectResult` = `0x23 + 9·n` bytes,
  `cResultCount` @ `+0x22`, `KSKILL_RESULT` records (9 B) @ `+0x23` (assert-proven).
- **Event sequence:** prepare → cast → channel ticks → effect result → derived motion
  (beat-back/ray/chain/point-chain). Only `OnSkillEffectResult` changes HP.
- **Cooldowns are server clocks:** `OnResetCooldown` (server base + delta),
  `OnPauseCDTimer`, `OnAccelerateCDTimer`, `OnCoolDownOverDraftNotify`.
- **Competitor sync is pull-on-demand:** list 0x164, CD state 0x161/0x162,
  pushes `OnSync*Competitor*` (buff list shares the buff-list wire shape;
  arena CD state = fixed 4×u32; `NeedSyncOB` gates exposure).
- **No rollback/resim netcode** — prediction is animation/UI only.
- **Server gates** (client-visible enums): move-state, cooldown, silence/immunity,
  camp/side, stealth, channel interruption, death/revive. Range/angle/LOS have **no**
  client validation — server-only.

---

## 8. What this means for reborn

The implementable contract is in `docs/pvp/REBORN_PVP_BATTLE_SPEC.md`. Summary:

1. **Server owns combat truth**: cast validation, cooldown clocks, DR state,
   damage/heal results, death/revive; the client predicts animation only.
2. **Data model mirrors JX3**: attributes with the unit conventions of §1.1,
   5-element damage + adaptive, buff slots with dispel groups/stacking/ticks,
   control categories {2,3,4,5,7,8,9,11} + DecayType windows, CoolDownList-style
   cooldowns with charges/overdraft/haste, per-skill move-state masks.
3. **Mode config is mask-based**: `MapBanMask`/`MapInvalidMask` intersect with map
   category masks; arena/BG/BR differ only in flags + ban sets + revive rules.
4. **PvP stats are first-class**: 化劲/御劲 + per-element defence layers must exist in
   the damage pipeline from day one, or PvP balance cannot be reproduced.
5. **Unknowns to prototype with defaults** (marked in the spec): rating→% equations,
   DR ladder/reset, arena brackets/scoring, BG objective numbers, BR zone schedule.

---

## 9. Open items (consolidated)

| # | Item | Status |
|---|---|---|
| 1 | rating→% equations (会心/破防/化劲/御劲/加速/命中/闪避/招架) | coefficients known; formula needs disassembly of `KPlayer::GetAttributeValue` |
| 2 | DR per-application ladder + immunity reset rules | client only ships windows |
| 3 | `MoveStateMask` whitelist-vs-suppression semantics; Mask2 bit order | MED/LOW |
| 4 | arena brackets (2v2/3v3), round format, ban-bit semantics | only 5v5 string found |
| 5 | BG objective/scoring/revive numbers | server-only |
| 6 | BR zone shrink schedule | only 乱武 scripts found |
| 7 | `CanBackup`, `CastMask`, `Usage` enums | not decoded |
| 8 | S2C protocol IDs, `KSKILL_RESULT` 9-byte field split | needs capture/deeper RE |
| 9 | camp id ↔ 浩气盟/恶人谷 mapping (`RelationForce.tab` not extracted) | inferred |
| 10 | `atDecriticalDamagePowerBaseKiloNumRate` outlier (buff 7505) | MED |

## 10. Reproduce

Each workstream report ends with its own reproduce commands. Entry points:

```powershell
# tables (GB18030)
python tools\pvp\tab.py HEADER "<PROBE>\logic-skill-prefixed-out\settings\skill\Buff.tab"
python tools\pvp\tab.py ROWS   "<PROBE>\logic-skill-prefixed-out\settings\skill\skills.tab" --cols SkillName,SkillID,MapBanMask --match SkillName=星楼月影
# Lua bytecode constants
python tools\netcode\lua51_constants.py "<PROBE>\...\scripts\skill\GlobalParam.lua" --json proof\pvp\attributes\GlobalParam.lua.const.json
# evidence verification
python tools\pvp\verify_pvp_evidence.py
python tools\pvp\field_semantics.py
```

`PROBE` = `C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\pakv4-probe`.
