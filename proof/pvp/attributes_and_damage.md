# JX3 (剑网3) — Attributes + Damage/Mitigation pipeline (PvP focus)

Workstream: ATTRIBUTES + DAMAGE/MITIGATION PIPELINE.
Branch: `research/jx3-pvp-battle`. All work read-only on the game install.

## Method / sources

| Source | Path | What it gives |
|---|---|---|
| Attribute UI table | `...\pakv4-probe\ad-desc-probe-out\ui\Scheme\Case\Attribute.txt` (138 data rows, 10 cols) | `at*` names + Chinese tooltip formulas for the character panel |
| Buff UI table | `...\ad-desc-probe-out\ui\Scheme\Case\Buff.txt` (26453 rows) | Player-facing buff/debuff descriptions (Chinese) |
| Skill UI table | `...\ad-desc-probe-out\ui\Scheme\Case\Skill.txt` (34582 rows) | Player-facing skill descriptions/talents |
| Buff logic table | `...\logic-skill-prefixed-out\settings\skill\Buff.tab` (61555 rows, 115 cols) | `at*` attribute names + numeric values on buffs |
| Skill logic table | `...\logic-skill-prefixed-out\settings\skill\skills.tab` (41141 rows, 117 cols) | skill flags/coefficients |
| Skill scripts | `...\ability-matcher\extracted\scripts\skill\**` (881 plaintext + 312 Lua5.1 bytecode) | damage primitives, per-skill values |
| Lua includes | `...\scripts\Include\{Skill,Player,NewSkill,LogicConst,ClearCoolID}.lh` | engine constants exposed to scripts |
| Engine name table | `JX3ClientX64.exe` file offsets `0x0084B280..0x0084D380` | player combat/attribute member-name table (names + order) |
| Engine global params | `...\logic-skill-out\scripts\skill\GlobalParam.lua` (Lua 5.1 bytecode) | KSkillGlobalParam rating conversion coefficients |
| Engine enum table | `...\bin64\SO3EnumConvertorX64.dll` | full `at*` enum names (694), in enum order |
| Engine strings | `reborn-pvp\proof\netcode\*.txt` (previously dumped) | function names, asserts |

Encoding: all tables are GB18030/GBK TSV. Numeric values are stored as strings on disk (no type suffixes).

Confidence labels: **HIGH** = directly quoted and cross-checked in ≥2 independent extracted artifacts or exact ID↔tooltip match; **MED** = single artifact or interpretation of a name; **LOW** = weak/inferred.

---

## 1. Attribute taxonomy

### 1.1 Three attribute universes

| Set | Count | Evidence |
|---|---|---|
| `Attribute.txt` (UI character panel) | **138** | `ad-desc-probe-out\ui\Scheme\Case\Attribute.txt` rows 1..138 |
| `Buff.tab` referenced `at*` names (19 attribute slots: BeginAttrib1..15, ActiveAttrib1..2, EndTimeAttrib1..2) | **461** distinct | `proof\pvp\attributes\buff_attr_names.tsv`, `attr_set_diff_allcols.txt` |
| Engine enum names (`SO3EnumConvertorX64.dll`) | **694** | `proof\pvp\attributes\so3enum_at_names.txt` (offset-ordered, monotonic) |

HIGH: the 694-name list in `SO3EnumConvertorX64.dll` (first offset `0x0000EAF0 atInvalid`, contiguous `at*` strings) is the engine's attribute enum/name table; the ordering matches the panel ordering (atInvalid first, then movement, then primary stats, then combat).

### 1.2 Grouping (names verified in Attribute.txt unless noted)

**Offence**
* attack power per element: `atPhysicsAttackPowerBase`, `atSolarAttackPowerBase`, `atNeutralAttackPowerBase`, `atLunarAttackPowerBase`, `atPoisonAttackPowerBase`, `atMagicAttackPowerBase` (内功), `atSolarAndLunarAttackPowerBase` (阴阳), `atAllTypeAttackPowerBase` (全攻击); percent variants `at*AttackPowerPercent` exist in Buff.tab/engine enum but **not** in Attribute.txt.
* 破防 Overcome: `atPhysicsOvercomeBase`, `atSolarOvercomeBase`, `atNeutralOvercomeBase`, `atLunarOvercomeBase`, `atPoisonOvercomeBase`, `atSolarAndLunarOvercomeBase`, `atAllTypeOvercomeBase`; `atNeutralOvercomePercent` (and per-type `at*OvercomePercent` in Buff.tab).
* 会心 CriticalStrike: `atPhysicsCriticalStrike`, `atSolarCriticalStrike`, `atNeutralCriticalStrike`, `atLunarCriticalStrike`, `atPoisonCriticalStrike`, `atAllTypeCriticalStrike`, `atSolarAndLunarCriticalStrike`; raw-rate variants `at*CriticalStrikeBaseRate` (only `atLunar/Neutral/Poison/SolarCriticalStrikeBaseRate` in Attribute.txt, `atPhysicsCriticalStrikeBaseRate` exists in Buff.tab/enum).
* 会心效果 CriticalDamagePower: `at*CriticalDamagePowerBase` (rating), `at*CriticalDamagePowerPercent` (修正值), `atAllTypeCriticalDamagePowerBase`, `atSolarAndLunarCriticalDamagePowerBase`, plus `KiloNumRate` variants (`at*CriticalDamagePowerBaseKiloNumRate`) and `atUnlimitCriticalDamagePowerKiloNumRate` that exist only in Buff.tab/engine enum.
* 命中 Hit: `atPhysicsHitValue`, `atSolarHitValue`, `atNeutralHitValue`, `atLunarHitValue`, `atPoisonHitValue`, `atAllTypeHitValue`, `atSolarAndLunarHitValue`; raw-rate variants `at*HitBaseRate` (Buff.tab/enum only; `atPhysicsHitBaseRate` etc.).
* weapon: `atMeleeWeaponDamageBase` (武器伤害), `atRangeWeaponDamageBase` (暗器伤害); percent variants `atMeleeWeaponDamagePercent`, `atRangeWeaponDamagePercent`, `atMeleeWeaponDamageRandPercent`, `atRangeWeaponDamageRandPercent` (Buff.tab/enum only).
* skill damage: `atSkillPhysicsDamage/Percent`, `atSkillSolarDamage/Percent`, `atSkillNeutralDamage/Percent`, `atSkillLunarDamage/Percent`, `atSkillPoisonDamage/Percent`, `atGlobalDamageFixedAdd` (技能伤害提高, rating), `atGlobalDamageFactor`.

**Defence**
* shield/defence rating: `atPhysicsShieldBase`, `atPhysicsShieldPercent`, `atPhysicsShieldAdditional`, `atPhysicsDefenceAdd` (外功防御等级提高; note duplicate naming), `atMagicShield` (内功防御等级), per-type `atSolar/Neutral/Lunar/PoisonMagicShieldBase` (+ `Percent` only in Buff.tab/enum).
* resist: `atPhysicsResistPercent`, `atSolarMagicResistPercent`, `atNeutralMagicResistPercent`, `atLunarMagicResistPercent`, `atPoisonMagicResistPercent`, `atGlobalResistPercent` (受到的所有伤害降低{D0/1024*100}%).
* mitigation shred/resist decay: `atDecayAllTypeResistPercent`.
* shields/absorb: `atPositiveShield`, `atNegativeShield`, `atMirrorShield`, `atGlobalBlock`, `atPhysicsBlock`, `atSolar/Neutral/Lunar/PoisonMagicBlock`, `atGlobalDamageAbsorb`, `atGlobalDamageAbsorbBySelfMaxLife/Spirit/Agility/Vitality`, `atDamageAbsorbShieldCoefficient`, `atShieldTransfer`, `atRecoveryShield`, `atRangeAngleShield`, `atDashBlock`, `atBlockLongRange`.
* reflection: `atPhysicsReflection`, `atPhysicsReflectionPercent`, `atSolar/Neutral/Lunar/PoisonMagicReflection`, `at*MagicReflectionPercent`, `atModifyReflectionPercent`.
* mana-shield: `atGlobalDamageManaShield`, `atPhysicsDamageManaShield`, `atSolar/Neutral/Lunar/PoisonDamageManaShield`.
* avoidance: `atDodge` (闪避等级), `atDodgeBaseRate` (闪避几率), `atParryBase` (招架等级), `atParryValueBase` (拆招值), `atParryPercent`, `atParryValuePercent`, `atParryBaseRate`, `atDeflect`, `atSetHitReduceRate`.
* conversion-matters (legacy stat conversions): `atVitalityToParryValueCof`, `atVitalityToPhysicsAttackPowerCof`, `atAgilityToParryCof`, `atStrainRate`, `atSurplusValueBase` (破招值).

**PvP stats (the 化劲/御劲 block)** — engine enum order `0x0000F3B8..0x0000F508`:
```
atDodgeBaseRate ... atParryBaseRate ... atStrainBase ... atStrainPercent ... atStrainRate
atToughnessBaseRate   (0xF480)
atToughnessBase       (0xF498)   御劲等级
atToughnessPercent    (0xF4A8)   御劲等级修正值
atDecriticalDamagePowerBaseKiloNumRate (0xF4C0)
atDecriticalDamagePowerBase            (0xF4E8)   化劲等级
atDecriticalDamagePowerPercent         (0xF508)   化劲等级修正值
```
* 御劲 Toughness: `atToughnessBase` (等级, Attribute.txt "御劲等级提高{D0}"), `atToughnessPercent` ("御劲等级修正值提高{D0}"), `atToughnessBaseRate` (御劲率; **not in Attribute.txt**).
* 化劲 DecriticalDamagePower: `atDecriticalDamagePowerBase` ("化劲等级提高{D0}"), `atDecriticalDamagePowerPercent` ("化劲等级修正值提高{D0}"), `atDecriticalDamagePowerBaseKiloNumRate` (**not in Attribute.txt**), plus `atEnableDecriticalDamagePowerConvert` and the conversion coefficients (see §4).
* Other PvP/PVX util: `atPVXAllRound` (全能等级), `atTherapyPVXAllRound`, `atAssistedPowerExtAdd` (凝神等级), `atAllShieldIgnoreAdd` (无视防御等级), `atAllShieldIgnorePercent` (无视防御%), `atGlobalDamageFixedAdd`, `atGlobalResistPercent`, `atDecayAllTypeResistPercent`, `atResistCriticalStrikeRate`, `atFixedDamageReduceValue`, `atReduceDamageWhenLifeLow/Changed`, `atReduceAOEDamagePercent`, `atGamePlayDamageFinalCof`, `atActivityDamageFinalCof`.

**Utility**: `atHasteBase` (加速等级), `atHasteBasePercentAdd`, `atUnlimitHasteBasePercentAdd`, `atMoveSpeedPercent`, `atRunSpeedBase`, `atJumpSpeedBase/Percent`, `atGravityBase/Percent`, `atMaxLifeBase/PercentAdd/Additional`, `atTherapyPowerBase` (治疗成效提高), `atTherapyCoefficient`, `atBeTherapyCoefficient` (被治疗效果提高{D0/1024*100}%), `atTherapyPowerPercent`, `atSkillTherapyPercent`.

### 1.3 Unit conventions (verified, HIGH)

From Attribute.txt tooltip arithmetic and Buff.tab↔Buff.txt ID matches:

| Kind of attribute | On-disk unit | Verified example |
|---|---|---|
| plain `*Base` (等级/rating) | raw rating | `atToughnessBase=162` ↔ "御劲等级提高162点" |
| `*Percent` (修正值) | **1024 = 100 %** | `atDecriticalDamagePowerPercent=-154` ↔ "化劲等级降低15%" (154/1024=15.04 %) — Buff 11873; `atGlobalResistPercent=102` ↔ "受到的伤害减免10%" (Buff 193 L2); `atAllShieldIgnorePercent=615` ↔ 破苍穹 60 % (614/1024=59.96 %); `atPhysicsReflectionPercent=136` ↔ "伤害反弹13.3 %" (136/1024=13.28 %) — Buff 388 |
| `*BaseRate` (率, e.g. 御劲率/闪避几率/被命中降低) | **100 = 1 %** | `atToughnessBaseRate=800` ↔ "御劲率提高8 %" (Buff 6309); `=900/1800` ↔ 9 %/18 % (Buff 9341); `=1000` ↔ 10 % (Buff 33175); `=500` ↔ 5 % (Buff 14980); `=3000` ↔ "被会心几率降低30 %" (Buff 9864); `atDodgeBaseRate=3000` ↔ "闪避几率提高30 %" (Buff 677); `atSetHitReduceRate=10000` ↔ 完全不被命中 (Buff 4052) |
| `*BaseKiloNumRate` | **1024 = 100 %** (same numeric scale as `*Percent`), name is misleading | `atDecriticalDamagePowerBaseKiloNumRate=-358` ↔ "化劲等级降低35 %" (Buff 24168 L5–8, 358/1024=34.96 %); `=-154` ↔ 15 % (Buff 25351); `=10` ↔ "每层提高1 %化劲" (Buff 28807, 0.98 %) |
| `*Cof`/`*Coefficient` | ratio/coefficient | `atBeTherapyCoefficient=1024` ↔ +100 %; `atDamageAbsorbShieldCoefficient=-512` ↔ -50 % |

Counter-example (unresolved): Buff 7505 春点 `atDecriticalDamagePowerBaseKiloNumRate=-1000`, tooltip "化劲降低10 %" — -1000/1024 = -97.7 %, not -10 %. See Open items. All other `KiloNumRate` samples fit /1024, so the **scale is reported as MED**, and this row is flagged.

### 1.4 Attributes that exist ONLY in Buff.tab / scripts / engine enum, not in Attribute.txt

`Attribute.txt` has 138 rows; **348** Buff.tab attrs and **556** engine-enum names are not in it. Explicitly requested ones:

| Attribute | Attribute.txt | Buff.tab | engine enum | UI text (from Buff.txt placeholder) |
|---|---|---|---|---|
| `atToughnessBaseRate` | – | Y (35 rows) | Y | 御劲率 |
| `atDecriticalDamagePowerBaseKiloNumRate` | – | Y (11 rows) | Y | 化劲 |
| `atSolarMagicResistPercent` | – | Y (136) | Y | (伤害减免, per-type) |
| `atFreeze` | – | Y (695) | Y | – |
| `atSkillEventHandler` | – | Y (2100) | Y | – |
| `atExecuteScript` | – | Y (13451) | Y | – |
| `atPhysicsAttackPowerPercent` | – | Y (714) | Y | 外功攻击 |
| `atMagicAttackPowerPercent` | – | Y (499) | Y | 内功攻击 |
| `atDecayAllTypeResistPercent` | – | Y (4) | Y | 无视减伤 |
| `atGlobalResistPercent` | Y | Y | Y | 受到的所有伤害降低 |
| `atAllShieldIgnoreAdd` | Y | Y (6) | Y | 无视防御 |
| `atGlobalDamageFixedAdd` | Y | Y (6) | Y | 技能伤害提高 |
| `atAssistedPowerExtAdd` | Y | – | Y | 凝神 |
| `atPVXAllRound` | Y | Y (1) | Y | 全能 |
| `atGlobalBlock`, `atGlobalDamageManaShield` | – | Y | Y | 化解/内力护盾 |
| `atBeTherapyCoefficient` | Y | Y (717) | Y | 被治疗效果 |

The full 694-row catalog is in `proof\pvp\attr_catalog.tsv`; the raw diffs in `proof\pvp\attributes\attr_set_diff_allcols.txt`.

Note: `scripts_attr_names.tsv` shows the plaintext skill scripts almost never spell out `at*` names; they use `ATTRIBUTE_TYPE.<NAME>` (85 distinct members, `proof\pvp\attributes\script_attribute_type_refs.tsv`), e.g. `EXECUTE_SCRIPT` (437), `SKILL_PHYSICS_DAMAGE` (113), `CALL_PHYSICS_DAMAGE` (97), `SKILL_ADAPTIVE_DAMAGE` (14), `SKILL_SOLAR_DAMAGE` (31). The engine enum for these is at JX3ClientX64.exe 0x7DAF18..0x7DB170 (uppercase names, e.g. `SKILL`, `BUFF`, `EFFECT_TO_SELF_AND_ROLLBACK`, `ADAPT_ATTRIBUTE_TYPE`, `NO_ADAPT_PHYSICS_ATTACK_POWER_BASE`).

---

## 2. Damage-type taxonomy and pipeline primitives

### 2.1 Elemental types (HIGH)

Engine enum (string table in `JX3ClientX64.exe` @ `0x0080CFB0..0x0080D050`, used by `KSkillManager::LoadSkillData`):

```
0x0080CFD0 Physics
0x0080CFD8 SolarMagic
0x0080CFE8 NeutralMagic
0x0080CFF8 LunarMagic
0x0080D004 Poison
0x0080D00C Leap
0x0080D014 None
0x0080D020 Adaptive
0x0080D02C Any
```

Chinese names come from the UI (skills.tab `Design_Effect`, Skill.txt) and Attribute.txt:
Physics = 外功; SolarMagic = 阳性内功; NeutralMagic = 混元/中性内功; LunarMagic = 阴性内功; Poison = 毒性内功; `Any`/`Adaptive` = 自适应 (uses the caster's school element).

**Adaptive ordinal mapping (HIGH)** — Buff 26578 `八荒衡鉴切心法屏蔽被动_{外功,阳性,中性,阴性,毒性}自适应` carries `atSetAdaptiveSkillType(1..5)`:
`1=Physics(外功)`, `2=Solar(阳性)`, `3=Neutral(混元)`, `4=Lunar(阴性)`, `5=Poison(毒性)`.
(Attributes `atNoAdaptPhysicsAttackPowerBase`, `atNoAdaptPhysicsCriticalStrikeBase`, `atNoAdaptPhysicsCriticalDamagePowerBase`, `atNoAdaptPhysicsOvercomeBase` exist in the engine enum @0x7DAFD0..0x7DB050.)

### 2.2 Damage primitives in skill scripts (HIGH for names/usage; MED for arg semantics)

Plaintext player skill scripts build damage through `skill.AddAttribute(effectMode, ATTRIBUTE_TYPE, value1, value2)` (Default.lua documents the signature) plus `tSkillData` level tables (`nDamageBase`, `nDamageRand`). Examples (exact code):

```lua
-- ...\scripts\skill\Carrier\神机台_扫射低伤.lua L36-56
skill.AddAttribute(ATTRIBUTE_EFFECT_MODE.EFFECT_TO_SELF_AND_ROLLBACK,
                   ATTRIBUTE_TYPE.SKILL_ADAPTIVE_DAMAGE, 1, nDamageBase*0.8);
skill.AddAttribute(ATTRIBUTE_EFFECT_MODE.EFFECT_TO_SELF_AND_ROLLBACK,
                   ATTRIBUTE_TYPE.SKILL_ADAPTIVE_DAMAGE_RAND, 1, nDamageBase*0.4);
skill.AddAttribute(ATTRIBUTE_EFFECT_MODE.EFFECT_TO_DEST_NOT_ROLLBACK,
                   ATTRIBUTE_TYPE.CALL_ADAPTIVE_DAMAGE, 1, 0);
-- ...\scripts\skill\npc\副本BOSS\一之窟\1号_骆耀阳\1号_骆耀阳_破风.lua L33
skill.AddAttribute(ATTRIBUTE_EFFECT_MODE.EFFECT_TO_SELF_AND_ROLLBACK,
                   ATTRIBUTE_TYPE.SKILL_PHYSICS_DAMAGE,
                   GetSkillDamageBaseByVersion(skill.dwSkillID, skill.dwLevel), 0);
```

| Primitive | Meaning | Evidence |
|---|---|---|
| `SKILL_<TYPE>_DAMAGE` / `_RAND` | flat base + random range of that element, applied to the skill | 113 refs SKILL_PHYSICS_DAMAGE, 109 SKILL_PHYSICS_DAMAGE_RAND (`script_attribute_type_refs.tsv`) |
| `CALL_<TYPE>_DAMAGE` | secondary/attributed damage event (DOT, reflect, extra hit) | 97 refs; script `绝境_棒打狗头伤害.lua` etc. |
| `SKILL_ADAPTIVE_DAMAGE(_RAND)` + `CALL_ADAPTIVE_DAMAGE` | adaptive-element damage; value1 = adaptive ordinal (1..5), value2 = scaled flat | Carrier example above; Buff 26578 ordinal mapping |
| `nDamageBase` / `nDamageRand` | per-level flat damage table in `tSkillData` | Default.lua L14-24; 635/706 scripts |
| `nWeaponDamagePercent` | weapon-damage scaling (1024 = 100 %); "对外功伤害有用，填0表示不计算武器伤害" | Default.lua L185 comment |
| `nAttackAttenuationCof` | AOE damage falloff (1024=100 %) | Default.lua L58 |
| `nDamageToLifeForParty` | x % damage-to-life for party | Default.lua L55 |
| `nSkillDamageLevelCoefficient` | skill-level damage scaling | `Include\Player.lh` function containing strings `nSkillPlatformType`, `SKILL_PLATFORM_TYPE`, `MOBILE`, `math.min`, `math.max`, `nLevel`, `nSkillDamageLevelCoefficient`, numbers `[120, 100, 0.02, 0.6, 1024]` (`Player.lh.const.json`) |
| Buff-side amplifier attributes | `at<Type>DamageCoefficient`, `atAllDamageAddPercent`, `atAllPhysicsDamageAddPercent`, `atAllMagicDamageAddPercent`, `atGlobalDamageFactor`, `atGamePlayDamageFinalCof`, `atGlobalDamageFixedAdd`, `atDamagePercentForSrc`, `atDamageToLifeCof`, `atDamageFeedbackPercent` | Buff.tab |
| Debuff-side reduction | `atPhysicsDamageAbsorb`, `atSolar/Neutral/Lunar/PoisonDamageAbsorb`, `atMagicDamageAbsorb`, `atReduceAOEDamagePercent` | Buff.tab |
| `SuperCustomDamage`, `Skill_SuperCustomDamageToPlayer`, `Skill_ElementsDamageToPlayer`, `CallSkill_ElementsDamage`, `BangHuiYueZhanDamageRate`, `GbAddDamage`/`KyAddDamage` | special PvP/guild-war damage hooks | `Include\Skill.lh`, `Include\NewSkill.lh` string tables |
| skill table coefficients | `SkillCoefficient`, `DotCoefficient`, `SurplusCoefficient`, `UseSkillCoefficient` (skills.tab cols 96,101,102,104) | skills.tab header |

### 2.3 Per-element defence/shield/resist/reflection fields (HIGH)

From the player combat member table in `JX3ClientX64.exe` (see §3 for the raw quote) — full table in `proof\pvp\attributes\damage_type_map.tsv`:

| Element | shield | resist % | reflection | block | mana-shield |
|---|---|---|---|---|---|
| Physics | `nPhysicsShield` (+ `nPhysicsShieldBase`/`Additional`/`Percent` attrs) | `nPhysicsResistPercent` | `nPhysicsReflection` + `nPhysicsReflectionPercent` | *(no nPhysicsBlock field; attr `atPhysicsBlock` exists)* | `nPhysicsDamageManaShield` |
| Solar | `nSolarMagicShield` (+Base) | `nSolarMagicResistPercent` | `nSolarMagicReflection` + `Percent` | `nSolarMagicBlock` | `nSolarDamageManaShield` |
| Neutral | `nNeutralMagicShield` (+Base) | `nNeutralMagicResistPercent` | `nNeutralMagicReflection` + `Percent` | `nNeutralMagicBlock` | `nNeutralDamageManaShield` |
| Lunar | `nLunarMagicShield` (+Base) | `nLunarMagicResistPercent` | `nLunarMagicReflection` + `Percent` | `nLunarMagicBlock` | `nLunarDamageManaShield` |
| Poison | `nPoisonMagicShield` (+Base) | `nPoisonMagicResistPercent` | `nPoisonMagicReflection` + `Percent` | `nPoisonMagicBlock` | `nPoisonDamageManaShield` |
| Generic | `nGlobalBlock`, `bPositiveShield`, `nNegativeShield`, `bMirrorShield` | `nGlobalResistPercent`* | (none) | `nGlobalBlock` | `nGlobalDamageManaShield` |

\* `nGlobalResistPercent` does **not** exist as a literal field in the client player table; the attribute name is `atGlobalResistPercent` (0 corpus hits for `nGlobalResistPercent` in JX3ClientX64.exe). Likewise `nBeOvercome` has 0 hits. Per-element attack/crit/overcome fields are in `damage_type_map.tsv`.

---

## 3. Mitigation pipeline (field runs + layered model)

### 3.1 The name-table run (HIGH — raw bytes)

`JX3ClientX64.exe` file offsets `0x0084B280..0x0084D380` contain a contiguous `n*`/`b*` member-name table. Quoted run (offset ↦ name; strings are null-terminated ASCII literals, 8-byte aligned):

```
0x0084BA80 nSurplusValue
0x0084BAC0 nPoseState
0x0084BAD0 nDodgeBaseRate
0x0084BAE0 nParryBaseRate
0x0084BAF0 nStrain
0x0084BAF8 nToughnessBaseRate
0x0084BB10 nToughness
0x0084BB20 nDecriticalDamagePowerBaseKiloNumRate
0x0084BB48 nDecriticalDamagePower
0x0084BB60 bFightState
0x0084BB70 bSystemShield
0x0084BB80 bSheathFlag
0x0084BB90 bPositiveShield
0x0084BBA0 nNegativeShield
0x0084BBB0 bMirrorShield
0x0084BBC0 nGlobalBlock
0x0084BBD0 nGlobalDamageManaShield
0x0084BBE8 nDamageToLifeForSelf
0x0084BC00 nDamageToManaForSelf
0x0084BC90 nAllDamageAddPercent
0x0084BCA8 nUnlimitCriticalDamagePowerKiloNumRate
...
0x0084BD00 nPhysicsAttackPower
0x0084BD18 nSkillPhysicsDamageRand
0x0084BD30 nPhysicsHitValue
0x0084BD48 nPhysicsHitBaseRate
0x0084BD60 nPhysicsCriticalStrikeBaseRate
0x0084BD80 nPhysicsCriticalDamagePowerBaseKiloNumRate
0x0084BDB0 nPhysicsCriticalDamagePower
0x0084BDD0 nPhysicsOvercome
0x0084BDE8 nPhysicsResistPercent
0x0084BE00 nPhysicsShield
0x0084BE10 nPhysicsReflection
0x0084BE28 nPhysicsReflectionPercent
0x0084BE48 nPhysicsDamageManaShield
0x0084BE68 nSolarAttackPower
   ... (Solar, Neutral, Lunar, Poison blocks follow the same 12/13-field pattern) ...
0x0084C400 nTherapyPower
0x0084C410 nSkillTherapy
0x0084C420 nBeTherapyCoefficient
0x0084C5B0 nDamageAbsorbValue
0x0084C5C8 nTherapyAbsorbValue
0x0084C5E0 nRechargeableAbsorbShieldValue
0x0084C600 nMaxRechargeableAbsorbShieldValue
0x0084C628 nCurrentHasteRate
0x0084C640 nHasteBase
0x0084C650 nHasteBasePercentAdd
...
0x0084D0D0 nDecriticalDamagePowerBase
0x0084D0F0 nParryBase
0x0084D100 nParryValueBase
0x0084D110 nStrainBase
0x0084D120 nPhysicsOvercomeBase
0x0084D138 nSolarOvercomeBase
0x0084D150 nNeutralOvercomeBase
0x0084D168 nLunarOvercomeBase
0x0084D180 nPoisonOvercomeBase
0x0084D198 nDecayAllTypeResistPercent
```

Full listing: `proof\pvp\attributes\jx3client_attr_table.txt` (380 strings). Primary stats (`nStrengthBase`…), life/mana/energy, and movement precede the block; loot/social/account fields follow it. The run is a name-registration table (Luna/Lua binding), so **names and relative order are HIGH confidence; exact struct byte offsets are not established** (a companion Lua-member list `nMoveState…nDecriticalDamagePowerBase` is at offsets 0x84B300..0x84D0D0).

Supporting runs:
* `proof\pvp\attributes\jx3client_region_83c000.txt` — `KSkillGlobalParam` members + Buff loader:
  `fCriticalStrikeParam, fCriticalStrikePowerParam, fDefCriticalStrikeParam, fDecriticalStrikePowerParam, fHitValueParam, fDodgeParam, fParryParam, fInsightParam, fPhysicsShieldParam, fMagicShieldParam, fHasteRate, fPlayerCriticalCof, fToughnessDecirDamageCof, fOvercomeParam, fSurplusParam, fAssistedPowerCof, fDecriticalDamagePowerToPhysicsAttackPowerCof, fDecriticalDamagePowerToMagicAttackPowerCof, fDecriticalDamagePowerToMaxLifeBaseCof, nMaxDecriticalDamagePowerEableConvert, fDecriticalDamagePVPCof, nPhysicsAPToMagicAPCof, nWeaponToMagicAPCof`.
* `proof\pvp\attributes\jx3client_region_7fe000.txt` — NPC-assisted attribute struct: `nStrainRate, nAttackPowerBase, nTherapyPowerBase, nCriticalDamageBaseRate, nPhysicsShieldBase, nMagicShield, nToughnessBase, nAllTypeOvercomeBase, nGlobalDamageFixedAdd, nAssistedPowerExtAdd, nAllShieldIgnoreAdd`.
* `proof\pvp\attributes\jx3client_region_803400.txt` — NPC template: `nParry, nParryValue, nSense, nPhysics/Solar/Neutral/Lunar/PoisonCriticalStrike, nPhysicsDefenceMax, nSolar/Neutral/Lunar/PoisonMagicDefence`.

### 3.2 Layered model (verified parts marked ✅, inferred ❌)

1. **Hit check** — attacker `n<Type>HitValue`/`n<Type>HitBaseRate` vs defender avoidance (`nDodgeBaseRate`, `nParryBaseRate`, `nSetHitReduceRate`-derived). ✅ attribute names & `atSetHitReduceRate=10000 → 完全不被命中` (Buff 4052); ❌ exact hit formula/order.
2. **Dodge / Parry (avoidance)** — `atDodgeBaseRate=3000 → 闪避几率+30 %` (Buff 677), `atParryBase`(招架等级)/`atParryValueBase`(拆招值) (Attribute.txt; Buff 220), `nParryBaseRate`. ✅ values; ❌ roll order.
3. **Shield (防御等级)** — `n<Type>Shield` / `n<X>MagicShield` converted to % (`fPhysicsShieldParam=fMagicShieldParam=6.364`), attacker `n<Type>Overcome` counteracts (`fOvercomeParam=11.412`, `atAllShieldIgnorePercent`/`atAllShieldIgnoreAdd`). ✅ fields/coefs; ❌ formula.
4. **Damage reduction layer** — `atGlobalResistPercent` (1024-based, Attribute.txt formula `{D0/1024*100}%`), `atDecayAllTypeResistPercent` ("无视目标10%减伤效果", Buff 24582 = -102), per-type `at*ResistPercent`. ✅ values; ❌ stacking order.
5. **PvP layer 化劲/御劲** — 化劲 (`nDecriticalDamagePowerBase` → `nDecriticalDamagePower`) reduces incoming player damage; 御劲 (`nToughnessBaseRate`/`nToughness`) cuts incoming crit chance/crit damage (`fToughnessDecirDamageCof=2.784`). ✅ fields/coefs + tooltip quotes §6; ❌ exact formulas.
6. **Block / mirror / mana-shield** — `nGlobalBlock` / per-type `n<X>MagicBlock` ("每层化解一次内外功伤害", Buff 360 天策-御), `bMirrorShield` ("反弹招式伤害和效果", Buff 8438), `n<X>DamageManaShield`/`nGlobalDamageManaShield` ("受到的伤害由内力承担", Buff 31942). ✅ semantics from tooltips.
7. **Absorb / rechargeable shields** — `nDamageAbsorbValue`, `nTherapyAbsorbValue`, `nRechargeableAbsorbShieldValue`, `atGlobalDamageAbsorb`, `atDamageAbsorbShieldCoefficient`, `atAddGlobalAbsorbShieldRecord*`. ✅ fields.
8. **Reflection** — `n<X>Reflection` (flat) + `n<X>ReflectionPercent` (1024-based; Buff 388 136→13.3 %). ✅.
9. **Final damage modifiers / caps** — `nGamePlayDamageFinalCof`, `nActivityDamageFinalCof`, `atGlobalDamageFixedAdd`, `atGamePlayTherapyFinalCof` (Buff 30633 名剑大会·对局火热 `-71`). ✅ names.

Ordering of layers 1→9 is **inferred (LOW/MED)** from naming and tooltip semantics; no client/server call-graph or decompiled function was recovered.

---

## 4. Rating → percent conversion

### 4.1 What WAS found (HIGH)

`KPlayer::GetAttributeValue` / `KPlayer::GetAttributeString` exist in JX3ClientX64.exe (file offsets `0x00839228`, `0x00839248`), and `KSkillManager::LoadSkillGlobalParam` loads `scripts/skill/GlobalParam.lua` (`JX3ClientX64.exe` file offsets `0x0080DA66`, `0x0080DA90`). That Lua file sets the conversion coefficients on `GetSkillGlobalParam()`:

`proof\pvp\attributes\GlobalParam_pairs.txt` (disassembled Lua 5.1 bytecode; key↦value exact):

| KSkillGlobalParam field | value | role |
|---|---|---|
| `fPlayerCriticalCof` | 0.75 | player crit coefficient (PvP) |
| `fCriticalStrikeParam` | 9.985 | 会心 rating→% coefficient |
| `fCriticalStrikePowerParam` | 3.679 | 会心效果 coefficient |
| `fDefCriticalStrikeParam` | 9.985 | (defender crit) |
| `fDecriticalStrikePowerParam` | 1.521 | decrit damage power coefficient |
| `fHitValueParam` | 7.644 | 命中 coefficient |
| `fDodgeParam` | 4.628 | 闪避 coefficient |
| `fParryParam` | 5.432 | 招架 coefficient |
| `fInsightParam` | 6.734 | 洞察 coefficient |
| `fPhysicsShieldParam` | 6.364 | 外功防御 coefficient |
| `fMagicShieldParam` | 6.364 | 内功防御 coefficient |
| `fOvercomeParam` | 11.412 | 破防 coefficient |
| `fHasteRate` | 10.61 | 加速 coefficient |
| `fToughnessDecirDamageCof` | 2.784 | 御劲→会心伤害降低 coefficient |
| `fSurplusParam` | 8.534 | 破招 coefficient |
| `fAssistedPowerCof` | 9.985 | 凝神 coefficient |
| `fDecriticalDamagePowerToPhysicsAttackPowerCof` | 0.525 | 溢出化劲→外功攻击 |
| `fDecriticalDamagePowerToMagicAttackPowerCof` | 0.584 | 溢出化劲→内功攻击 |
| `fDecriticalDamagePowerToMaxLifeBaseCof` | 10.0 | 溢出化劲→气血 |
| `nMaxDecriticalDamagePowerEableConvert` | 58800 | 化劲转属性上限 |
| `fDecriticalDamagePVPCof` | 0.9 | 化劲 PvP 系数 |
| `nPhysicsAPToMagicAPCof` | 1138 | 外功→内功攻击换算 |
| `nWeaponToMagicAPCof` | 757 | 武器→内功攻击换算 |

These are the constants that any rating→percent conversion must use; the script only *sets* them (`SETTABLE R0['fCriticalStrikeParam']=9.985` …), it contains no formula.

### 4.2 What was NOT found (explicit)

* No `Level2Percent` / `RatingToPercent` / `GetPercentByLevel` / `PercentByLevel` / `LevelToPercent` / `ConvertLevel` / `GetRateByLevel` string exists in any `bin64` DLL/EXE (searched all 30 binaries, 0 hits).
* No Lua function in `Include\Skill.lh`, `Player.lh`, `NewSkill.lh`, `LogicConst.lh`, `ClearCoolID.lh` performs a rating→percent conversion. `Player.lh` only contains skill-damage level scaling (`nSkillDamageLevelCoefficient`: constants `[120,100,0.02,0.6,1024]`) and `UpdatePlayerSkillDamageCoefficient`.
* The C++ formula is inside `JX3ClientX64.exe` (and on the unavailable server); no disassembly of the attribute-calculation function was produced. `KBaseX64.dll` contains **zero** attribute strings — it is not where this lives.
* Therefore: **the actual rating→% equations (and level term) remain unknown.** Do not treat the coefficient table as a formula.
* Conversely the *inverse* direction is verified for buff application: the on-disk units of Percent/Rate/KiloNumRate attributes are known (§1.3), and `atGlobalResistPercent`, `atDecayAllTypeResistPercent`, `atAllShieldIgnorePercent`, `atPhysicsReflectionPercent`, `atDodgeBaseRate`, `atToughnessBaseRate` all follow `1024=100 %` or `100=1 %`.

---

## 5. PvP-specific modifiers (Buff.tab / scripts)

(All buff rows below are quoted from `proof\pvp\attributes\buff_pvp_names.txt`, `buff_pvp_rows.txt`, `buff_unit_check.txt`, `buff_mitigation_rows.txt`.)

### 5.1 化劲 (DecriticalDamagePower) modifiers

| ID | Name | Useage | attrs |
|---|---|---|---|
| 30060 | 治疗PVP护手加化劲 | 39 | `atDecriticalDamagePowerBase(2756)` |
| 30059 | 输出PVP护手减化劲 | 39 | `atDecriticalDamagePowerBase(-2756)` |
| 1455 | 增加化劲 | 14 | `atDecriticalDamagePowerBase(54)` |
| 11873 | 追命箭降低目标化劲 | 7 | L1 `atDecriticalDamagePowerPercent(-51)`, L2 `-154` |
| 12597 | 兰摧降低目标化劲 | 2 | `atDecriticalDamagePowerPercent(-154)` (x7 stacks) |
| 13924 | 翔极碧落降低目标化劲等级 | 201 | `atDecriticalDamagePowerPercent(-205)` |
| 18031 | 百步穿杨减化劲 | 7 | `atDecriticalDamagePowerPercent(-103)` |
| 23071 | 霸刀七刀无视化劲 | 163 | `atDecriticalDamagePowerPercent(-102)` |
| 24168 | 灭影追风 | 315 | L5-8 `atDecriticalDamagePowerBaseKiloNumRate(-358)` + `atPhysicsCriticalStrikeBaseRate(500..2000)` + `atPhysicsOvercomePercent(51..205)` |
| 24761 | 灭影追风减化劲等级 | 315 | `atDecriticalDamagePowerPercent(-61)` |
| 25272 | 丐帮_温酒_无视化劲 | 82 | `atDecriticalDamagePowerPercent(-359)` |
| 27064 | 明教_净恶瑕诛无视化劲 | 8 | `atDecriticalDamagePowerPercent(-410)` |
| 30306/30307 | 奇穴_雾灭_降低/提高化劲 | 315 | `-51` / `+51` |
| 7505 | 春点减化劲 | 116 | `atDecriticalDamagePowerBaseKiloNumRate(-1000)` (tooltip 化劲降低10 % — see open item) |
| 28807 | 萌新助威 | 13 | `atDecriticalDamagePowerBaseKiloNumRate(10)` (每层+1 %化劲, MaxStack 15) |
| 28496 | 木桩心法属性 | 13 | `atDecriticalDamagePowerBase(49183)` + `...+KiloNumRate(102)` |
| 51856 | 绝境_丐帮棒打降低化劲 | 187 | `atDecriticalDamagePowerPercent(-62)` |

Tooltips (Buff.txt): 11873 穿杨 "化劲等级降低15 %" (L2); 13924 遥思 "化劲等级降低20 %"; 18031 灵机卸甲 "化劲等级降低10 %"; 24168 灭影追风 "化劲等级降低35 %" (L5-8); 7505 春点 "化劲降低10 %"; 28807 萌新助威 "每层提高1 %化劲（该效果不会在竞技场、绝境战场、列星虚境中生效）".

### 5.2 御劲 (Toughness) modifiers

| ID | Name | Useage | attrs | tooltip |
|---|---|---|---|---|
| 1072 | 御劲 | 42 | `atToughnessBase(162 / 124)` | 御劲等级提高{X}点 |
| 30074 | PVP项链化劲加御劲 | 39 | `atToughnessBase(160)` | – |
| 30707 | PVP附魔1腰带…被击加御劲 | 39 | `atToughnessBase(10800 / 4320 / 12609 / 5043 / 14760 / 5904)` (3 s) | 提高自身N点御劲等级 |
| 30708 | PVP附魔2腰带…化劲转御劲 | 39 | `atToughnessBase(120/48/140/56/164/65)` (10 stacks) | 每层提高自身N点御劲等级 |
| 2198 / 2199 | 铁壁 / 溃甲 | 39 | `atToughnessPercent(+205 / -205)` | 御劲等级提高/降低20 % |
| 14980 | 归清 | 8 | `atToughnessBaseRate(500)` | 御劲提高5 % |
| 20812 | 慑服 | 139 | `atToughnessBaseRate(512)` | 御劲率提高5 % |
| 6309 | 旗渊 | 1 | `atToughnessBaseRate(800)` | 御劲率提高8 % |
| 9341 | 石间意 | 120 | `atToughnessBaseRate(900 / 1800)` | 御劲率提高9 %/18 % |
| 33175 | 积势 | 2 | `atToughnessBaseRate(1000)` | 御劲率提高10 % |
| 9864 | 疾如风 (风20 %会心换20 %御劲) | 1 | `atToughnessBaseRate(3000)` | **被会心几率降低30 %** |
| 24185 | 亘绝 (驰风八步结束不受会心伤害) | 315 | `atToughnessBaseRate(10000)` | 御劲提高100 % |
| 22999 | 履冰 | 6 | `atDecriticalDamagePowerPercent(-10)` + `atToughnessBaseRate(-100)` | 每层降低1 %的御劲率和化劲等级 |
| 21647 | 履冰(Lv2) | 6 | `atDecriticalDamagePowerPercent(-10)` + `atToughnessBaseRate(-50)` | 每层降低1 %的御劲和化劲等级 |
| 30515 | JJC插旗大王-御劲 | 42 | `atToughnessBase(±100)` | – |
| 7669 | 势如破竹 (据点大旗) | – | 化劲×1.2, 御劲×1.5, 受到疗伤-15 % | 据点 PvP objective buff |
| 7049 | 天选者 (铁血宝箱) | – | 化劲×0.9, 御劲×0.8, 免疫控制 | 逐鹿中原 |
| 13209/13218/23147 | 阵营小斗士 / 铁血大将军 | – | 化劲+350, 御劲+200, 伤害+50 % | 逐鹿中原/阵营攻防 |
| 30516 | JJC插旗大王-化劲 | 42 | `atDecriticalDamagePowerBase(±1000)` | – |

### 5.3 减疗 (healing reduction) — `atBeTherapyCoefficient` in 1024 units

| ID | Name | value | reading |
|---|---|---|---|
| 2662 | 帮会_擂台_减疗伤成效 | -512 / -1536 | 50 % / 150 % |
| 9514 | 清角 (buff name 楚济_减疗40 %) | -461 | 45 % (461/1024=45.0 %; Buff.txt tooltip "受到疗伤效果降低45 %") |
| 10243 | 盾击减疗实际效果 | -307 / -614 | 30 % / 60 % |
| 15033 | MOBA减疗 | -512 | 50 % |
| 15531 | 血覆黄泉减疗效果BUFF(50) | -307 / -512 | 30 % / 50 % |
| 30427 | 丐帮减疗 | -563 | ~55 % |
| 17644 | 竞技场55机器人减疗修正 | -51 | ~5 % |
| 16967 | 新苍云特殊武器_减疗 | -5 | ~0.5 % |
| 24223/24477 | 减疗+减盾吸收 | `atBeTherapyCoefficient(-512)` + `atDamageAbsorbShieldCoefficient(-512)` | 50 % both |
| 21977/26477 | 系统_精神对应减疗 | `atBeTherapyCoefficient(-10)` + `atDamageAbsorbShieldCoefficient(-10)` + `atDamageToLifeCof(-10)` | ~1 % |

### 5.4 减伤 (damage reduction)

* `atGlobalResistPercent` — Buff 176 坚硬 (102,153,…,679 → 10 %..66 %), 1986 西风古道_减伤 `1024` (=100 %), 1014 明教_朝圣言_自身减伤免控 `614` (=60 %), 13598 竞技场状态机减伤buff `1004` (=98 %).
* per-element `at*ResistPercent` — Buff 2717/2718 (万花长针/提针减伤), 5626-5630 大明宫傀儡 (306/612 → 30 %/60 %).
* `atDecayAllTypeResistPercent` — 24582 横云破锋阵 "无视目标10 %减伤效果" (-102), 24986/25345/30814 (-31/-102), 30814 孤锋破浪 (无视减伤).
* shield absorb + therapy absorb — `atDamageAbsorbShieldCoefficient`, `atAddGlobalAbsorbShieldRecordOnly*`, `atGlobalTherapyAbsorb`.

### 5.5 名剑/竞技/战场/绝境

* **竞技场 (arena)**: Buff 16654/26041 "竞技场能力修正/战场机器人能力修正" (`atBeTherapyCoefficient(±31)`, `atAllDamageAddPercent(±31)`, `at<Element>DamageCoefficient(∓31)`); 17644 robot healing reduction; 13597/13598 state-machine invulnerability (`atNegativeShield(1)`,`atGlobalBlock(1)` / `atGlobalResistPercent(1004)`); 30633 名剑大会·对局火热 `atGamePlayTherapyFinalCof(-71)`. Note Buff 28807 explicitly excludes arena/绝境 (see §6).
* **战场 (battlefield)**: Buff 12456 体验战场狂暴 `at<AllType>CriticalStrikeBaseRate(6000)` (=60 % crit); 21181 天原绝境_寒冷/极寒 `atHasteBasePercentAdd(-300/-500)` (1024-based, ≈ -29 %/-49 % haste); 16654 battlefield robot correction.
* **名剑大会**: no stat-changing buffs beyond match bookkeeping (6472/11924 名剑币, 13211 复查, 17488 每周五场胜利) + 30633 对局火热.
* **逐鹿中原 / 阵营攻防**: 13209/13218 阵营小斗士, 23147 铁血大将军, 7049 天选者, 7669 势如破竹 — all directly modify 化劲/御劲.
* `PvpField.tab` exists in the previous-session extraction (`reborn-netcode\proof\netcode\mode_juejing\pak_out2\PVPField.tab`) — a PvP-scene field table, not yet analysed here.

### 5.6 Skills that modify 化劲/御劲 (from scripts / buff names)

* Plaintext comment evidence: `...\skill\蓬莱\套路及子技能\蓬莱_子技能_翔极碧落_实际伤害.lua` L247 "翔极碧落降低化劲" + `player.GetSkillLevel(20343)==1` gate; `绝境_亢龙有悔/绝境_棒打狗头伤害/绝境_龙战于野` contain **commented-out** `--skill.BindBuff(2, 51228, 1); -- 减化劲` lines; `...\skill\沙漠风暴\道具_疾如风.lua` mentions 免控 (comment).
* Buff 11873 名称 "追命箭降低目标化劲" ⇒ 唐门 追命箭; 12597 "兰摧降低目标化劲" ⇒ 万花 兰摧玉折 (skills.tab SkillID 190, 3037); 18031 "百步穿杨减化劲" ⇒ 唐门 百步穿杨; 24168 "灭影追风" ⇒ 刀宗 灭影追风; 13924 ⇒ 蓬莱 翔极碧落; 33175 "离经_新积势_炸黑御劲" ⇒ 万花 离经易道 积势. Skill IDs by name from skills.tab: 190 兰摧玉折, 3096/3291/3535 追命箭, 359 破苍穹, 362 碎星辰, 550 鹊踏枝, 355 凭虚御风, 245 罗汉金身, 3973 贪魔体, 3985 朝圣言, 258 舍身诀, 442 天策-御.
* 递减/免控/解控 are not attributes in Buff.tab names seen in the PvP subset; control-immunity is `atImmunity(n)`, `atImmuneSkillMove`, `atKnockedDownRate(-1024)` etc. (e.g. Buff 11808 星楼免击退, 4245 朝圣言, 4439 贪魔体). 递减 (diminishing returns) appears only in comments (绝境_七星拱瑞).

---

## 6. 化劲 / 御劲 semantics from player-facing text

Quoted exactly (Buff.txt, ID + level + name + desc):

* `1072 御劲` L0/L1 — **“御劲等级提高<BUFF atToughnessBase>点”** (rating, no %)
* `2198 铁壁` L1 — **“御劲等级提高20%”** and `2199 溃甲` L1 — **“御劲等级降低20%”** (Percent attribute, 205/1024)
* `9864 疾如风` — **“被会心几率降低30%”** (atToughnessBaseRate 3000 ⇒ 御劲率)
* `24185 亘绝` — **“御劲提高100%”**; the same Buff.tab row 24185 is named “驰风八步结束不受会心伤害” (atToughnessBaseRate 10000)
* `20812 慑服` — **“御劲率提高5%”**; `6309 旗渊` — **“御劲率提高8%”**; `14980 归清` — **“御劲提高5%”**; `33175 积势` — **“御劲率提高10%”**
* `22999 履冰` — **“每层降低1%的御劲率和化劲等级”** (atToughnessBaseRate -100 + atDecriticalDamagePowerPercent -10)
* `7505 春点` — **“化劲降低10%”** (atDecriticalDamagePowerBaseKiloNumRate -1000)
* `11873 穿杨` L2 — **“化劲等级降低15%”**; `13924 遥思` — **“化劲等级降低20%”**; `18031 灵机卸甲` — **“化劲等级降低10%”**; `24168 灭影追风` L5-8 — **“化劲等级降低35%”**
* `28807 萌新助威` — **“隐元会为防止萌新阵营侠士化劲过低，当侠士装分低于本服对抗类平均装分时且自身化劲低于70%时，为侠士增加额外化劲，该效果每层提高1%化劲（该效果不会在竞技场、绝境战场、列星虚境中生效）”**
* `7049 天选者` — **“血量增加至100%，化劲加成0.9倍，御劲加成0.8倍，免疫一切控制…侠士死亡、离线、隐身或使用神行时效果消失”**
* `7669 势如破竹` — **“气血最大值提高16倍，化劲等级提高1.2倍，御劲等级提高1.5倍，受到的疗伤效果降低15%”**
* `13209 阵营小斗士` — **“气血上限+100%，伤害和治疗结果+50%，化劲+350，御劲+200，仅逐鹿中原和阵营攻防活动期间，参与战斗的场景生效”**
* Skill.txt 342 融金 (school attribute skill) L10 — **“外功防御等级提高2850点\n内功防御等级提高2850点\n化劲率提高10%…”**, L11 “化劲率提高10%”
* Asset-localisation maps in the player installation (community UI addons, not game logic):
  - `interface\LM\LM_Retrieval\lang\zhcn.jx3dat`: `['toughness']="御劲"`, `['damage reduction']="化劲"` — matches the engine naming.
  - `interface\LM\LM_Tip\lang\zhcn.jx3dat`: `['toughness']="化劲"`, `['decritical damage base']="御劲"` — **inverted** vs. the engine; treat as an addon bug (LOW value).

### Conclusions (as far as the text supports)

* **御劲 (Toughness)** is a rating → 御劲率. The only in-game numeric statements tie 御劲率 to **reducing the chance the player is critically hit** (“被会心几率降低30 %” with atToughnessBaseRate=3000; “不受会心伤害” at 10000=100 %). A separate coefficient `fToughnessDecirDamageCof=2.784` indicates 御劲 additionally reduces incoming critical damage. **MED** (tooltip semantics HIGH for crit-chance; crit-damage link MED, name-based).
* **化劲 (DecriticalDamagePower)** is a rating → 化劲率, described by the addon localisation as "damage reduction". Its conversion has a dedicated PvP coefficient `fDecriticalDamagePVPCof=0.9`, and the stat can overflow-convert into attack power / max life (parameters in §4.1), capped at 58800. The buff `28807 萌新助威` is explicitly a **PvP-only catch-up damage-reduction stat for 阵营侠士** and is disabled in arena/绝境. **MED** for "reduces damage taken from players"; the exact % formula is unknown.
* Uncertain: (a) whether 化劲 reduces *all* damage or only 侠士 damage — the localization name "damage reduction" and `fDecriticalDamagePVPCof` suggest broad damage reduction with a PvP-specific coefficient, but no sentence "化劲降低受到的侠士伤害" was found in the extracted UI tables; (b) interplay between 御劲 crit-chance reduction and attacker `atResistCriticalStrikeRate`/`atUnlimitCriticalDamagePowerKiloNumRate`; (c) the exact layer order vs. global resist.

---

## 7. Open items

1. **`atDecriticalDamagePowerBaseKiloNumRate` unit conflict** — 5 buffs fit `1024=100 %` (24168 -358→35 %, 25351 -154→15 %, 28807 10→1 %, 8402 31→3 %, 28496/30121 102→10 %), but 7505 (-1000) tooltip says 10 %. Either the tooltip is stale, the buff clamps, or a second interpretation applies. Marked MED.
2. **Rating→% formulas not found** (`会心/破防/化劲/御劲/加速/命中/闪避/招架`). Only the coefficient table (`GlobalParam.lua`) and the on-disk units are verified. `GetAttributeValue` at `JX3ClientX64.exe:0x839248` is the likely implementation site — needs IDA/Ghidra disassembly.
3. **Struct byte offsets/order unknown** — `jx3client_attr_table.txt` is a name-registration table; exact C++ struct layout and which names alias the same storage are not established.
4. **Layer order** of dodge/parry → shield → resist → 化劲 → block/reflect/mana-shield is inferred; no call-graph evidence.
5. `nGlobalResistPercent`, `nBeOvercome`, `nMagicBlock`, `nMagicShield` (generic), `nMagicResistPercent` (generic), `nMagicReflection` (generic), `nDamageManaShield` (generic) are **not** literal fields in the player name table; only `at*` attribute names or per-element variants exist. `nMagicShield` appears only in the NPC-assisted struct (`m_piScript` region 0x7FE078).
6. **减疗 scaling** — most rows round-trip at 1024=100 % (9514 -461↔45 %, 30427 -563↔55 %, 15531 -512↔50 %), but Buff 10243 (盾击减疗) has tooltips "减疗33 %/66 %" against values -307/-614 (30 %/60 %); its Buff.tab Name says "盾击减疗实际效果" and one level's tooltip is 60 %, so the 33/66 texts may be stale. MED.
7. `atBeTherapyCoefficient` and `atDecriticalDamagePowerPercent` are both 1024-based but their interaction with the target's base value (multiplicative vs additive) is unverified.
8. Server-side (`KBaseX64.dll` has zero combat attribute strings) was not available; `fDecriticalDamagePVPCof`, `fPlayerCriticalCof` semantics are name-based only.

---

## 8. Evidence files written

Report + catalog:
* `proof\pvp\attributes_and_damage.md` — this report
* `proof\pvp\attr_catalog.tsv` — 694 rows: engine-enum attr, Chinese label, presence flags, 6 Attribute.txt formula strings, Buff.txt context, Buff.tab ref count

Raw dumps (`proof\pvp\attributes\`):
* `attribute_raw.tsv` (Attribute.txt verbatim), `buff_attr_names.tsv`, `buff_attr_names_console.txt`, `attr_set_diff.txt`, `attr_set_diff_allcols.txt`
* `buff_header.txt`, `skills_header.txt`, `buff_ui_header.txt`, `skill_ui_header.txt`, `skills_effecttype_dist.txt`, `represent_headers.txt`, `skill_result_header.txt`
* `buff_pvp_rows.txt`, `buff_pvp_names.txt`, `buff_unit_check.txt`, `buff_tooltips_kilonum.txt`, `raw_rows_focus.txt`, `buff_mitigation_rows.txt`, `buff_adaptive_rows.txt`, `buff_ui_hua_yu.txt`, `buff_ui_semantics.txt`
* `so3enum_at_names.txt`, `so3enum_all_strings.txt`, `so3enum_pvp_hits.txt`, `so3enum_kw_hits.txt`
* `jx3client_attr_table.txt`, `jx3client_attr_table_preview.txt`, `jx3client_attr_window.txt`, `jx3client_region_83c000.txt`, `jx3client_region_7fe000.txt`, `jx3client_region_803400.txt`, `jx3client_region_80cf00.txt`, `jx3client_region_7fc800.txt`, `jx3client_region_873000.txt`, `jx3client_region_874300.txt`, `jx3client_adapt_and_attrname.txt`, `jx3client_rate_fn_strings.txt`, `kbase_attr_strings.txt`, `engine_fieldname_search.txt`, `engine_conversion_hits.txt`, `engine_cjk_pvp_hits.txt`, `poison_revive_dump_read.txt`
* `Skill.lh.strings.txt`, `Player.lh.strings.txt`, `NewSkill.lh.strings.txt`, `*.const.json`, `Skill_lh_damage_strings.txt`, `NewSkill_damage_strings.txt`, `include_str_hits.txt`, `script_attribute_type_refs.tsv`, `scripts_attr_names.tsv`, `scripts_pvp_terms.txt`, `damage_pipeline_examples.txt`
* `GlobalParam.lua.const.json`, `GlobalParam_pairs.txt`, `GlobalParam_disasm.txt`, `GlobalParam_coefficients.tsv`, `GlobalParam_decompiled.lua` (unluac failed — invalid jar; pairing done via own disassembler)
* `damage_type_map.tsv`, `skills_by_name.tsv`, `therapy_coef_check.txt`, `absorb_coef_check.txt`, `skill342_full.txt`
* `probe_cjk_pvp_scan.txt`, `probe_cjk_defs.txt`, `probe_cjk_defs2.txt`, `lm_tip_hua_yu.txt`, `lm_retrieval_hua_yu.txt`, `wiki_pvp_hits.txt`

External references (read-only, not modified):
* `C:\SeasunGame\...\SO3EnumConvertorX64.dll` (enum names), `...\bin64\JX3ClientX64.exe` (member table), `...\bin64\KBaseX64.dll` (0 combat attr strings)
