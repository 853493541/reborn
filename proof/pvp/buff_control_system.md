# JX3 buff / control system — consolidated report

**Worktree:** `C:\Users\Zhibin Ren\Desktop\reborn-pvp` (branch `research/jx3-pvp-battle`)
**Date:** 2026-09-24
**Status:** completes the interrupted "buff/control" workstream; all raw evidence files under
`proof/pvp/buffs/` retained. Confidence labels: **HIGH** (data + engine/script cross-check),
**MED** (data + inference from names/counts), **LOW** (hypothesis, needs engine disassembly).

> **Encoding warning.** Several files written by the interrupted agent contain double-encoded
> (GBK↔UTF‑8) Chinese names: `control_buff_rows.tsv`, `_key_buff_rows.tsv`, `_key_buff_summary.txt`,
> `_skill_movestate.txt`, `_suozu_summary.txt`, `_immunity_sets.tsv`, `_immunity_names.txt`,
> `_useage_groups.txt`, `_pvp_map_masks.txt`, `_dispel_api_calls.txt`, `_jichu_comments.txt`,
> `_decay_*`. Do **not** quote names from them. All names in this report come from clean
> regenerations in this workstream (listed in §10).

## 0. TL;DR

| Finding | Value | Confidence |
|---|---|---|
| Frame unit | 1/16 s everywhere in buff timing (160=10 s, 320=20 s, 96=6 s, 48=3 s) | HIGH |
| Buff.tab size | 61 555 data rows × 115 cols (`proof/pvp/attributes/buff_header.txt:1`) | HIGH |
| CC families | 眩晕/击倒/定身(Halt)/锁足(Charm)/减速/沉默(含封内)/缴械/恐惧/嘲讽/眠蛊/冰环/封轻功/混乱/击退-拉 | HIGH |
| DR table | `DecayType.tab` 10 rows (0…9, -1=no DR); **DecayFrame == ImmunityFrame in this build** (160/160, 320/320, 96/96) | HIGH |
| CC category ids (`DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE` / `atImmunity`) | 2=减速 3=恐惧 4=定身 6=雷霆 7=锁足 8=眩晕 (+5=沉默 9=嘲讽 11=击倒 from immunity sets) | HIGH/MED |
| MoveState bit names | 37-name `MOVE_STATE` enum run in `JX3RepresentX64_all_strings.txt:752293-752385`; bit index = 1-based ordinal (decoded in `movestate_decode.txt`) | names HIGH, bit-base MED |
| DetachType | dispel group; 3/5/7/11 = 阳性/混元/阴性/毒性 **增益**, 4/6/8/10/12 = same-class **减益** (odd/even pairs) | MED-HIGH |
| Stacking | `IsStackable`/`MaxStackNum`(≤255 assert)/`Count`/`Interval`/`MinInterval`/`MaxInterval` frames/`IsIntensityStackable` | HIGH |
| Mode masks | `MapList.InvalidBuffMask` (bit3=竞技+绝境, bit8=战场) ∩ `Buff.MapInvalidMask`; `MapList.BanSkillMask` ∩ `skills.MapBanMask` (bit9/512=绝境) | MED-HIGH |
| Previous evidence corrections | 6 hand-picked DR examples were wrong (424/453/464/749/2523/20346) — see `decay_types_verified.tsv:17-58` | HIGH |

---

## 1. Buff.tab field semantics

### 1.1 Table shape

`Buff.tab` = `PROBE\logic-skill-prefixed-out\settings\skill\Buff.tab`
(61 555 data rows, 115 columns; header dumped to `proof/pvp/attributes/buff_header.txt:2-116`).
Key columns used in this report:

`0 ID | 1 Name | 2 Useage | 3 FunctionType | 4 RepresentPos | 5 RepresentID | 6 AppendType | 7 DetachType | 8 BuffType | 9 Level | 10 Intensity | 11 IsStackable | 12 MaxStackNum | 13 Count | 14 Interval | 15 Hide | 16 Exclude | 17 Save | 18 OnManaShield | 19 OnFight | 20 OnHorse | 21 OnTerrain | 22 FormationLeader | 23 ScriptFile | 24 GlobalExclude | 25 UniqueTarget | 26 CanCancel | 27-71 BeginAttrib/Values | 72-77 ActiveAttrib | 78-83 EndTimeAttrib | 84 IsCountable | 85 DecayType | 86 MoveStateMask | 87 MapBanMask | 88 IsOffLineClock | 89 CanAccumulate | 90 SrcDistance | 91 OnKungfuID | 92 InSprint | 93 InFollow | 94 CancelByUseItem | 95 MapInvalidMask | 96 IsCombatBuff | 97 MinInterval | 98 ClearUnderWater | 99 MoveStateMask2 | 100 ShapeShiftInvalid | 101 MaxInterval | 102 ActiveCoefficient | 103 CanBeSteal | 104 NeedSync | 105 InBirdMove | 106 CanTransfer | 107 InParkour | 108 CostSingleStack | 109 InSlideSprint | 110 MorphInvalid | 111 ValidByLevel | 112 RLActions | 113 Coexist | 114 IsIntensityStackable`

Loader evidence: `JX3LogicEditOperation_net_strings.txt:0x007843F0` (`skill/Buff.tab`),
`[Buff] Get *` getter chain in `proof/pvp/buffs/_logicedit_functiontype_run.txt:47-89`,
machine-field names in `proof/pvp/netcode/struct_member_runs_logic.txt:77-85`.

### 1.2 FunctionType (col 3) — 18 distinct values

Source: data dump `proof/pvp/buffs/field_semantics.txt:324-342`.

| Value | Rows | Meaning (verified examples) |
|---|---|---|
| `''` | 55 110 | no special behaviour (passive/attribute only) — 103 打坐, 106 加速 |
| `Damage` | 2 982 | damage/periodic pulse — 418 绷带Debuff, 427 新手特殊技能-棍-减速效果 |
| `Normal` | 1 154 | generic — 101 策划默认项, 104 浮身 |
| `Stun` | 477 | 眩晕/击晕 — 424 冲锋眩晕, 462 地震、巨龟压制眩晕 |
| `Slow` | 342 | 减速 — 450 玄一无相_debuff, 509 减速, 523 剑主天地-迟缓 |
| `ResistDamage` | 275 | 减伤 — **122 春泥护花**, 367 天策-守如山, **399 无相诀**, 684 天地低昂 |
| `Hot` | 272 | 治疗持续 — 631 握针, 680 翔舞, 681 上元点鬟 |
| `Halt` | 257 | 定身/冰冻 — **554 大道无术**, 556 七星拱瑞, 675 芙蓉并蒂-定身 |
| `Shield` | 179 | 吸收盾 — 134 坐忘无我, 4244 渡厄力_吸收盾 |
| `Silence` | 150 | 沉默/封内 — **445 少林-沉默**, 534 止息, 557 八卦洞玄 |
| `Charm` | 131 | 锁足/冰环/禁锢（“无法移动”）— **541 断魂刺-无法移动**, **558 三才化生** |
| `Blooding` | 92 | 流血 — 655 流血 |
| `Enmity` | 86 | 嘲讽/仇恨 — 512 定军, 761 嘲讽仇恨置顶 |
| `Daze` | 32 | 眩晕/倒地/击飞 — 453 九转归一_眩晕, 467 击飞, 994 倒地1秒 |
| `Fear` | 10 | 恐惧 — 422 恐惧DEBUFF |
| `Fly` | 3 | 封轻功/限制 — 15 650, 29 108, 32 597 |
| `Disarm` | 2 | 缴械 — 8067 缴械Buff, 8332 缴械 |
| `Chaos` | 1 | 混乱 — 11605 燃烛 |

- Engine side: `nRetCode && "[Buff] Get FunctionType"` + enum `KSKILL_FUNCTION_TYPE`
  (`_logicedit_functiontype_run.txt:52-53`; `struct_member_runs_logic.txt:79`). **HIGH** that
  this column is the primary mechanical classification; the strings above are the exact data values.
- Note: this string column is **not** the numeric category used by
  `DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE` (see §4.3) — a common source of confusion.

### 1.3 BuffType (col 8) — 5 distinct values

Source: `field_semantics.txt:344-349`.

| Value | Rows | Examples | Reading (MED) |
|---|---|---|---|
| `''` | 60 330 | most buffs | no override |
| `Invalid` | 1 189 | 101 策划默认项, 104 浮身(lvl2), 2295 五毒_五仙图录_蟾啸, 10530 长歌门特殊武器_终结技 | non-standard/internal row marker |
| `Daze` | 31 | 453 九转归一_眩晕, 467 击飞, 852 七星拱瑞_player | UI/state class = daze |
| `Fear` | 3 | 422, 481, 486 恐惧 | UI/state class = fear |
| `Halt` | 2 | 455 拂穴手, 33093 1号_鲁念雪_扑倒 | UI/state class = halt |

Engine getter `[Buff] Get BuffType`, enum `KBUFF_TYPE` (`struct_member_runs_logic.txt:78-80`).
The three named values duplicate FunctionType names for a subset of rows only, so BuffType is best
treated as a UI/classification override; exact runtime effect: **MED/LOW**.

### 1.4 AppendType (col 6) — 29 245 distinct values = “merge/slot group”

- Default value = the buff's own ID: 101→101, 103→103, 104→104 (`stacking_rows.tsv:2-4`). **HIGH**
- Buffs meant to share one slot use a common constant, e.g. group `8` = 眩晕 family
  (464/505/533/548/567), group `661` = 破风 family (661 + 7969 破风叠加 + 12717 破甲增强),
  group `8050` = 交互测试 family (`field_semantics.txt:352-393`). **MED**
- Engine: `KBuffList::GetBuffByAppendType` (`JX3LogicEditOperation_net_strings.txt:0x007CC570`) and
  the assert `[Buff] Same 'ID' but different 'AppendType' : (%u, %d) --> (%u, %d).`
  (`JX3ClientX64_exe_net_strings.txt:0x0083DD50`, in `_enum_engine_hits.txt:8`). **HIGH**
- Reading: when a new buff is added, the list looks up the existing buff in the same AppendType
  slot and merges/replaces (subject to `Coexist`, §5.4).

### 1.5 DetachType (col 7) — 32 distinct values = “dispel group”

Script evidence (all in `PROBE\ability-matcher\extracted\scripts\...`):

| Skill | Groups passed to `DETACH_MULTI_GROUP_BUFF` / `DETACH_SINGLE_BUFF` | Meaning |
|---|---|---|
| `百战异闻录_玩家\通用_驱散混元.lua` | 5 (lvl2), 6 (lvl3) | 5=混元增益, 6=混元减益 — **HIGH** (script name says 驱散混元) |
| `纯阳\子技能_镇山河驱散自己.lua` | 2,4,6,8,10,12 | all six debuff classes |
| `明教\明教_圣明佑_月.lua` | 4,6,8,12 | 阳性/混元/阴性/毒性 减益 |
| `五毒\五毒_枯残蛊_对友方.lua` | 4,6,8,10,12 | +控制类 |
| `沙漠风暴\道具_剑转流云.lua` (plaintext GBK) | 7,3,5,11 | offensive: strip 阴性/阳性/混元/毒性 **增益** |
| `藏剑\…\霞流宝石远距离.lua` | 7,3,5,11 | same offensive set |
| `绝境战场\绝境_净世破魔击.lua` | 3,5,7,11 | same offensive set |
| `七秀\七秀_七诀剑气.lua` | 52 (comment 命门BUFF) | special group |

Data grouping (`detach_groups.txt:5-97`) confirms the pattern:

| Group | Rows | Membership | Confidence |
|---|---|---|---|
| 1 | 587 | 藏剑/天策 外功 buffs (扶摇, 泉凝月, 云栖松, 战意) | MED |
| 2 | 1 020 | 外功 debuff (天策 定军/致伤/破风/流血, NPC DOT_外功) | MED |
| 3 | 334 | **阳性 buff** (少林 般若诀/不动明王/无相诀/舍身诀) | HIGH |
| 4 | 466 | **阳性 debuff** (少林 摩诃无量/龙爪功/立地成佛/沉默) | HIGH |
| 5 | 575 | **混元 buff** (万花 清心静气/春泥护花/毫针, 纯阳气场) | HIGH |
| 6 | 420 | **混元 debuff** (钟林毓秀/兰摧玉折 DOT, 剑飞惊天沉默, 紫霞定身) | HIGH |
| 7 | 697 | **阴性 buff** (七秀 名动四方/翔舞/上元点鬟/天地低昂) | HIGH |
| 8 | 965 | **阴性 debuff** (绛唇珠袖/止水冰环/太阴指) | HIGH |
| 9 | 24 | 清风望月阵/万花 NPC copies — special | LOW |
| 10 | 145 | control-class debuffs (八卦洞玄/三才化生/暗藏杀机锁足/七星拱瑞) | MED |
| 11 | 105 | **毒性 buff** (五毒 仙王蛊鼎/镇派 蛇会心) | HIGH |
| 12 | 821 | **毒性 debuff** (置毒/花笺奇毒/DOT_毒性) | HIGH |
| 13 / 14 | 32 / 119 | 五毒 蛊系 buff / 蛊系控制减益 (万蛊真解/眠蛊/献祭) | MED-HIGH |
| 15-24 | ~1 400 | non-combat: 药物/食物/易容/阵法施放/宴会 | MED |
| 52,101,103,105,107,111,112 | — | per-skill/NPC special groups (剑舞, 蜜蜂蜇咬, 天地火…) | MED |

Loader: `[Buff] Get DetachType` (`_logicedit_functiontype_run.txt:58`). Group semantics come from
the scripts above (game-logic), so treat the parity table as inferred mechanics: **MED-HIGH**.

### 1.6 Useage (col 2) — 318 distinct values, design-time tag

- `_useage_dist.txt:1-3`: 318 distinct, min 0 max 470; most common values 289(5480), 30(2556),
  110(2573), 39(2380), 51(2358), 120(1310).
- No `[Buff] Get Useage` in the `KBuffManager::LoadBuffInfo` getter chain
  (`_logicedit_functiontype_run.txt:47-90`) → **not a runtime mechanic** (MED-HIGH).
- Sample semantics (from `field_semantics.txt` Useage section; grouping inferred, **MED**):

| Useage | Sample buffs | Reading |
|---|---|---|
| 0 | 101 策划默认项(非程序默认行), 104 浮身, 107 力量提升 | default/misc |
| 1 | 196 天策-憾如雷, 198 徐如林, 200 疾如风 | 天策 school |
| 2 | 112 清心静气, 113 碧水滔天, 122 春泥护花, 126 毫针 | 万花 school |
| 3 | 131 剑冲阴阳, 134 坐忘无我, 136 吐故纳新 | 纯阳/气场 |
| 4 | 213 迅影, 318 水上漂, 320 名动四方 | 七秀 |
| 5 | 382 般若诀, 385 不动明王, 399 无相诀 | 少林 |
| 6 | 1676 玉泉鱼跃, 1686 梦泉虎跑 | 藏剑 |
| 7 | 2237 化血镖, 3195 穿心弩, 3202 逐星箭 | 唐门 |
| 8 | 4019 幽月轮, 4025 赤日轮 | 明教 |
| 9 | 2295 五仙图录_蟾啸, 2307 万蛊真解 | 五毒 |
| 11 | **103 打坐**, 208 扶摇直上, 244 骑乘 | 移动/坐骑/轻功 |
| 12 | 142 嗜血, 168 杀红眼, 169 顽抗, 170 心法·善… | 战斗增益/减益 |
| 13 | 106 加速, 127 生活技能_Doodad需求, 341 摆摊, 342 交通 | 系统/交互 |
| 14 | 211 油炸肉条, 211 酱爆腰花 | 烹饪食物 |
| 15 | 414 山贼营地投毒, 418 绷带Debuff, 482 疲劳 | 任务/环境 |
| 24 | 285 易容药膏, 286 空山新雨, 289 缩身丹 | 药品/易容 |
| 30 | 717 三才阵1号DOT减蓝, 770 无盐岛2号燃烧 | NPC/BOSS buffs |
| 39 | 967 外防, 968 力量, 969 治疗, 971 外功AP及会心 | 属性/套装 |
| 42 | 1019 神力, 1020 鬼御, 1021 神行 | **竞技场/名剑** |
| 51 | 1591 领悟, 1594 如意饺, 1595 冬至食品 | 活动/宴席 |
| 110 | 7208 湿漉漉的鞋子, 7358 切换动作模式标记BUFF | 挂件/表现/杂项 |
| 187 | 12643 出圈伤害, 12651 藏匿 | likely 浪客行 (its maps use InvalidBuffMask 171) |
| 289 / 290 | 百战异闻录通用沉默 / 百战异闻录占用 | 百战异闻录 |

Full value→count→example dump: `proof/pvp/buffs/field_semantics.txt:4-323`.

---

## 2. Control (CC) taxonomy

Family summary: `cc_families.tsv:2-15` (name-keyword scan over all 61k rows; counts are keyword hits).
Clean representative rows (all levels): `cc_examples.tsv` (35 cols incl. decoded masks).
DR data: `decay_types_verified.tsv:5-14` (data-driven membership, corrects hand-picked lists).

### 2.1 Family table

| Family | FunctionType(s) (top) | DecayType | Representative buff (ID, name, DecayType, MoveStateMask/MS2) | Confidence |
|---|---|---|---|---|
| 眩晕 Stun/Daze | Stun (477), Daze (32) | 0 击晕; **-1/empty = no DR** | **533 天霜粉** DT=0; **719 天策-破坚阵-击晕** DT=0, MS=0x53C01000, MS2=60; 424 冲锋眩晕 DT=**empty**, MS=0x53C01000 | HIGH |
| 击倒 Knockdown | Daze/Stun/Damage | -1 (165 rows), no DR entry | 994 倒地1秒 DT=empty MS=0x53D00200(1406140928) = {9 ON_KNOCKED_DOWN, 20 ON_HALT, 22-25,28,30}; 359 抗击飞 MS=0xFFFFFFFE | MED |
| 定身 Root (Halt) | Halt (257), Charm | **2 定身 (160f)** | **554 大道无术** DT=2, MS=0x53C02000, MS2=60; **675 芙蓉并蒂-定身** DT=2, same MS | HIGH |
| 锁足 Root (Charm) | Charm, Halt | **1 冰环** / **8 短暂锁足 (96f)** | **541 天策-断魂刺-无法移动** FT=Charm, DT=empty, MS=0x53C04000, MS2=60; **558 三才化生** DT=1; 3466 唐门_暗藏杀机_锁足 DT=1 | HIGH |
| 减速 Slow | Slow (342), Charm, Fly | -1 mostly | 509 减速 DT=empty, Detach=8; 549 天策-穿-减速 (CanTransfer=1) | HIGH |
| 沉默/封内 Silence | Silence (150) | **5 沉默 (160f)** | **445 少林-沉默** DT=5, Detach=4, MS=0xDFFEFFFE; **557 八卦洞玄** DT=5, Detach=10; 534 止息 DT=5 | HIGH |
| 缴械 Disarm | Disarm (2) | -1 | **8067 缴械Buff** FT=Disarm, DT=empty, MS=0xDFFEFFFE | HIGH |
| 恐惧 Fear | Fear (10) | **9 恐惧 (160f)** | **422 恐惧DEBUFF** FT=Fear, BT=Fear, DT=empty, MS=0xFFFFFFFE; 27038 万灵山庄_恐惧 DT=9 | HIGH |
| 嘲讽 Taunt | Enmity (86) | -1 | 761 嘲讽仇恨置顶 FT=Enmity; 4344 NPC通用_免疫嘲讽 (atImmunity=9) | HIGH |
| 眠蛊 Sleep | Stun | **6 眠蛊 (160f)** | **2522 五毒_万蛊真解_眠蛊(对Player)** FT=Stun, DT=6, Detach=14, MS=0x53C01000 (2523 对NPC is DT=0 — see corrections) | HIGH |
| 冰环 Freeze | Charm, Halt | **1 冰环 (160f)** | **706 云裳心经_止水_冰环** FT=Charm, DT=1, Detach=8, MS=0x53C04000 | HIGH |
| 封轻功 Fly-ban | `''` (65), Slow (1) | -1 | **562 吞日月** family (72 levels: 苍云盾压封轻功, 极乐引, 长歌_冲秋冥, 破重围…), MS=0xDFFEFFFE | HIGH |
| 混乱 Chaos | `''` (25) | -1 | 1261 经脉混乱, 4331 南诏皇宫_引导者_混乱, 11605 燃烛 FT=Chaos | MED |
| 击退/拉 Knockback/Pull | Stun(7), Normal/Halt | handled via rates, not DecayType | 359 抗击飞; 364 抗击退; immunity via `atKnockedBackRate/atRepulsedRate/atPullRate=-1024` (see §4.4) | HIGH |
| 致盲 (blind) | **no dedicated FunctionType** | — | 533/812 致盲 = Stun (眩晕); 3595 致盲 = hit-rate debuff (`bufftxt_zhmang_lines.tsv:2-5`) | HIGH |
| 麻痹 (paralysis) | no dedicated FunctionType | — | 6735 麻痹 = Slow+DOT; 23348 行动迟缓 (`bufftxt_mabi_lines.tsv:2-19`) | HIGH |
| 封内 (inner-power lock) | Silence | — | Tooltip-only category: 29241 流梨 "免疫封内、缴械", 31698 歇拍 "免疫封内、打断"; 转乾坤/锻骨诀 tooltips (`bufftxt_fengnei_lines.tsv:2-11`) | MED |

### 2.2 CC-category ids used by scripts (numeric enum, separate from FunctionType strings)

Evidence: `长歌\套路及子程序\长歌_影_孤影化双.lua` bytecode comments dump
(`_guying_functiontype.txt:32-79`) + `DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE` arg scan
(`_delmulti_functiontype_map.txt`):

| id | Meaning | Evidence | Confidence |
|---|---|---|---|
| 2 | 减速 Slow | 孤影化双 comment `--解除减速` → arg 2 | HIGH |
| 3 | 恐惧 Fear | arg 3 (no comment), 无惧(8247) tooltip + sets {2,3,4,7,8,11} | HIGH |
| 4 | 定身 Root | `--解除定身` → arg 4; 冰心诀-冻逝-免疫定身(700) `atImmunity=4` | HIGH |
| 5 | 沉默 Silence | 免疫沉默(763) `atImmunity=5`; 水月免控 lvl1 adds 5 (`immunity_clean_sets.tsv:3,18`); 转乾坤/锻骨诀 免疫封内 buffs | HIGH |
| 6 | 雷霆震怒 (special) | `--解除雷霆` → arg 6 | HIGH (name) |
| 7 | 锁足 Root | `--解除锁足` → arg 7 | HIGH |
| 8 | 眩晕 Stun | `--解除眩晕` → arg 8 | HIGH |
| 9 | 嘲讽 Taunt | 4344/8832 `atImmunity=9` | HIGH |
| 10 | unknown | used by 圣明佑_月 after detach 4/6/8/12 | LOW |
| 11 | 击倒 Knockdown | 摧蕊免疫击倒效果(20704) `atImmunity=11 + atKnockedDownRate=-1024`; 风止韧性及免疫击倒(27102) `atKnockedDownRate=-1024 + atImmunity=11` (`immunity_clean_sets.tsv:30,41`) | HIGH |
| 12 | unknown | used by 圣明佑_月 | LOW |
| 14-17 | newer categories | 月影护盾/寂灭蛊/孔雀引 {5,14}; 衍天宗荧惑守心 {15,16,17} | LOW |
| - | 击退/被拉 displacement | not an atImmunity id; uses the *Rate attributes | HIGH |

---

## 3. MoveStateMask / MoveStateMask2 decode

### 3.1 Bit names (HIGH)

The `MOVE_STATE` enum name run is literal in the engine string dump:
`JX3RepresentX64_all_strings.txt:752293-752385`, consecutive:
`ON_STAND, ON_WALK, ON_RUN, ON_JUMP, ON_SWIM_JUMP, ON_SWIM, ON_FLOAT, ON_SIT,
ON_KNOCKED_DOWN, ON_KNOCKED_BACK, ON_KNOCKED_OFF, ON_SPRINT_BREAK, ON_SPRINT_DASH,
ON_SPRINT_KICK, ON_SPRINT_FLASH, ON_SKILL_MOVE_SRC, ON_SKILL_MOVE_DST, ON_SKILL_MOVE_TAIL,
ON_SKILL_MOVE_DEATH, ON_HALT, ON_FREEZE, ON_ENTRAP, ON_AUTO_FLY, ON_DEATH, ON_DASH,
ON_PULL, ON_REPULSED, ON_RISE, ON_SKID, ON_START_AUTO_FLY, ON_FLY, ON_FLY_FLOAT,
ON_FLY_JUMP, ON_DASH_TO_POSITION, ON_BIRD_FLY, ON_BIRD_FLOAT, ON_BIRD_JUMP`
followed by `UNKNOWN`, `NONE` and an action-state run `CAST_SKILL, SKILL_PREPARE,
SKILL_CHANNEL, RECIPE_PREPARE, PICK_PREPARE, OPEN_DOODAD, ...` (lines 752387-752400).
Script-visible names match: `Skill.lh`/`NewSkill.lh` reference `MOVE_STATE.ON_KNOCKED_OFF /
ON_PULL / ON_KNOCKED_DOWN / ON_KNOCKED_BACK / ON_REPULSED / ON_SKILL_MOVE_DEATH /
ON_SKILL_MOVE_DST / ON_SKILL_MOVE_SRC / ON_SKILL_MOVE_TAIL / ON_FREEZE`
(`_lh_movestate_context.txt:48-60`; `proof/pvp/attributes/Skill.lh.const.json` func 77).
Engine fields: `ullSelfMoveStateMask` (64-bit), `dwSelfMoveStateMaskLow/High`, `SelfMoveStateMask2`
(`JX3ClientX64_exe_net_strings.txt:0x008100D8-0x00810128`, `_movestate_enumreflect.txt:21-30`);
note the 64-bit storage means the newer states 32+ (ON_FLY_FLOAT…) need either high bits or the
separate `MoveStateMask2`.

### 3.2 Bit-base (1-based ordinal) — MED

`tools/pvp/decode_movestate.py:14-28` defines two hypotheses. Supporting data:

- **Buff 103 打坐** has `MoveStateMask = 256` (`stacking_rows.tsv:2`). 256 = bit 8. Under the
  1-based reading (bit index = ordinal, ON_STAND=1) bit 8 = **ON_SIT** — exactly the sitting
  meditation buff. Under 0-based (ON_STAND=0) bit 8 = ON_KNOCKED_DOWN — contradicted by the buff name.
- The “all movement states” constants observed are `0xFFFFFFFE`/`0xFFFFFFFF`
  (`movestate_decode.txt:4-9`); bit 0 is the only bit with no name in the run, so a 1-based
  ordinal scheme naturally leaves bit 0 unused and produces `0xFFFFFFFE` for “all defined states”.
- `movestate_decode.txt` gives both decodings for every common mask; **use the “1-based” line**.

Representative decoded masks (1-based, from `movestate_decode.txt:1-69`):

| Mask (dec) | Hex | Bits (1-based names) | Seen on |
|---|---|---|---|
| 3758030846 | 0xDFFEFFFE | 1-15, 17-28, 30, 31 (**no ON_SKILL_MOVE_SRC**, no ON_SKID) | default passive buffs (445, 509, 523, 557, 761, 4036, 8067, 203, 655, 631) |
| 3757998078 | 0xDFFE7FFE | 1-14, 17-28, 30, 31 (also no ON_SPRINT_FLASH) | 122 春泥护花, 549, 562(51) |
| 4294967294 | 0xFFFFFFFE | all defined states (bit0 unused) | 359 抗击飞, 422 恐惧, 465 击倒, 104 浮身 |
| 1405095936 | 0x53C01000 | 12 ON_SPRINT_BREAK, 22 ON_ENTRAP, 23 ON_AUTO_FLY, 24 ON_DEATH, 25 ON_DASH, 28 ON_RISE, 30 ON_START_AUTO_FLY | 424/453/464/517/682/2522/719 (眩晕 family) |
| 1405100032 | 0x53C02000 | 13 + 22,23,24,25,28,30 | 554/556/675/1857 (定身 Halt) |
| 1405108224 | 0x53C04000 | 14 + 22,23,24,25,28,30 | 541/558/706/749/1512 (锁足/冰环 Charm) |
| 1406140928 | 0x53D00200 | 9 ON_KNOCKED_DOWN, 20 ON_HALT, 22-25, 28, 30 | 994 倒地 family |
| 3758096382 | 0xDFFFFFFE | all defined states except 29 ON_SKID (bit0 unused) | 32545, 33463 (禁疗/减速 NPC) |
| 1610547150 | 0x5FFEFFCE | 1-15,17-28,30 (no 16,29,31) | 139 扶摇直上 |

Semantics of the mask on a buff are **not** fully proven: both a “whitelist: buff lives only in
these states” reading (fits 打坐) and a “state-suppression” reading (fits stun masks) are
compatible with the data. Mark semantics **LOW**, bit names **HIGH**.

### 3.3 MoveStateMask2 — unresolved (LOW)

- Observed values (buff space, `_skill_movestate.txt:27-52`, re-verified in `cc_examples.tsv`):
  `63` (default, 29 375 rows), `60`, `56`, `7`, `0`, `20`, `16`, `24`, `23`, `59`, `61`, `57`, `29`.
- Control/CC buffs consistently use **60** (424/453/541/554/556/558/682/706/749/2522…), while
  normal buffs use **63**; 打坐 (103) uses **56**; skill 433 天策-任驰骋 has `SelfMS2=4`
  (`_skill_movestate.txt:56`, numeric fields trustworthy).
- Hypothesis (unverified): M2 is a 6-bit mask for the 6 post-31 MOVE_STATE names
  (ON_FLY_FLOAT…ON_BIRD_JUMP), so 63 = all six, 60 = 4 of 6, 56 = 3 of 6. The engine does carry a
  separate `SelfMoveStateMask2` field, but no numeric enum dump was recovered. **Open item.**

---

## 4. Diminishing returns (DR) & 解控

### 4.1 DecayType.tab semantics (HIGH)

`PROBE\logic-skill-prefixed-out\settings\skill\DecayType.tab` = 10 rows × 4 cols
`DecayType | DecayFrame | ImmunityFrame | Name` (header via `tools/pvp/tab.py HEADER`).

- Frame unit = 1/16 s: `160 = 10 s`, `320 = 20 s`, `96 = 6 s`.
- **DecayFrame == ImmunityFrame for every row in this build** — DR window and immunity window are
  the same length (data-driven check in `decay_types_verified.tsv:5-14`). HIGH.
- Categories (verified membership): `0 击晕(140 rows)`, `1 冰环(51)`, `2 定身(102)`, `3 七星拱瑞(27)`,
  `4 雷霆震怒(5)`, `5 沉默(62)`, `6 眠蛊(6)`, `7 迷神钉(7)`, `8 短暂锁足(12)`, `9 恐惧(1)`,
  `-1 (none) 1 316 rows no DR`.
- Loader: `KBuffManager::LoadBuffDecayInfo`, `m_BuffDecayTable`, `MAX_BUFF_DECAY_TYPE`,
  `[Buff] Get DecayType` (`JX3ClientX64_exe_net_strings.txt:0x0083DA90/0x0083D1E8/0x0083DDA8`).
- **Corrections to previous evidence:** the hand-picked example lists in
  `proof/pvp/decay_and_controls.tsv` and `proof/pvp/buffs/decay_types.tsv` contain 6 wrong IDs
  (424/453/464 are `empty` not 0; 749 is 8 not 2; 2523 is 0 not 6; 20346 is -1 not 9).
  See the audit in `proof/pvp/buffs/decay_types_verified.tsv:17-58`. Use the verified file.

### 4.2 How DR is applied (MED, engine-side)

- The tab only stores the window lengths; the in-fight “递减 → 免疫” ladder is engine logic
  (`KBuffManager::LoadBuffDecayInfo`), not exposed in any UI table. Player tooltips do not document
  control DR at all — grep of the whole UI Skill.txt for 递减 returns only *damage-split* diminishing
  text (e.g. 3108 天绝地灭 "同时命中多名侠士目标时伤害递减至多50%",
  `skilltxt_decay_lines.tsv:2-15`). **Open item:** exact per-application sequence (e.g.
  100%→50%→25%→immune) is not recovered from the client tables.
- **Reset behavior:** no client table column, script API or engine string for “reset decay” was
  found (`proof/pvp/buffs/_dispel_api_calls.txt` scan, `engine_symbol` greps). Because
  `DecayFrame == ImmunityFrame`, the visible contract is: after a DR-class effect ends, the same
  class is immune for the same duration; whether the counter resets on immunity expiry, on death,
  or on out-of-combat is server-side and remains **unknown** (Open item 3).

### 4.3 Per-school 解控 examples (HIGH for attribute sets)

Clean dumps: `proof/pvp/buffs/immunity_clean_*.tsv` (regenerated from Buff.tab with
`tools/pvp/immunity_sets.py`). Attribute meanings: `atImmunity=n` = immune to control category n
(§2.2); `atKnockedDownRate=-1024` = −100% chance to be knocked down; `atKnockedBackRate/
atRepulsedRate/atPullRate=-1024` = immune to knockback/repulse/pull; `atGlobalBlock=1` = block (免伤);
`atNegativeShield=1` = 无敌 (negative shield).

| Skill (school) | Buff ID / level | Immunity attributes | Notes |
|---|---|---|---|
| 生太极_不受控制 (纯阳气场) | 374 lvl1 | {2,3,4,7,8,11} + atKnockedDownRate −1024 | `immunity_clean_shengtaiji.tsv:2` |
| 星楼月影 (万花) | 411 lvl1; 20247 item; 51358 绝境 | {2,4,7,8} + knockedDown −1024 | `immunity_clean_xinglou.tsv:2-4` |
| 鹊踏枝 (七秀) | 677 lvl1/8; 2847 素衿; 8742 加强 | {2,4,7,8} + knockedDown −1024; also atDodgeBaseRate 3000 | `immunity_clean_queta.tsv:2-8`; `buff_mitigation_rows.txt:114` |
| 疾如风 (天策) | 20201 item; 51362 绝境 (base skill 412) | {2,3,4,6,7,8,11} + knockedDown −1024 | `immunity_clean_jirufeng.tsv:2-3` |
| 任驰骋免控 (天策) | 2756 lvl1/2 | {2,3,4,6,7,8,11} + knockedDown; lvl1 + knockback/repulse; lvl2 + pull −1024 | `immunity_clean_sets.tsv:6-7` |
| 啸如虎 (天策) | 203 (减伤/回血, no immunity); 51468 绝境_啸如虎 | 51468: {2,4,7,8} + knockedDown/repulse/pull −1024 | `immunity_clean_xiaoru.tsv:2-3` |
| 笑醉狂 (丐帮) | 5995 lvl1; 20194 item; 51849 绝境 | **{2,3,4,7,8,11}** + knockedDown/knockBack/repulse/pull −1024 | `immunity_clean_xiaozui.tsv:2-5` |
| 无相诀 (少林) | 6213 少林_无相诀定身无敌 | atGlobalBlock=1 + atFreeze (self-root) ; 5703 adds atLunarMagicReflectionPercent 205 | `immunity_clean_wuxiang.tsv:2`; `pvp_keyword_view4.txt:30` |
| 镇山河 (纯阳) | 377 lvl1; 13144 镇山河免疫 | atNegativeShield=1 + atGlobalBlock=1 + {2,3,4,5,7,8,11} | `immunity_clean_zhenshanhe.tsv:2-4` |
| 转乾坤 (纯阳) | 384 (atImmunity=5 only); 2781 免控 | 2781: {2,4,7,8} + knockedDown; lvl2 + repulse/pull −1024 | `immunity_clean_zhuanqiankun.tsv:2-5` |
| 天地低昂 (七秀) | 684 | **no immunity attributes** in `immunity_sets.py` scan → it is a 减伤 buff (FT ResistDamage) not a 解控 | `immunity_clean_tiandi.tsv` empty; `field_semantics.txt:330` |

**Restriction pattern (HIGH):** most 解控 buffs list only `{2,4,7,8}` (+ knockedDown) and **omit the
pull/knockback rates** — matching tooltips like 鹊踏枝 “不受招式控制效果影响(击退和被拉除外)”
(`buff_mitigation_rows.txt:114`) and 暗尘弥散 “…(击退和被拉除外)” (`buff_mitigation_rows.txt:120`).
Full displacement immunity exists only when `atKnockedBackRate/atRepulsedRate/atPullRate=-1024`
(e.g. 5995, 2756 lvl2, 377 镇山河 line `immunity_clean_zhenshanhe.tsv:4`).

Additionally, 解控 skill scripts *delete* the existing effect with
`DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE` (categories {2,4,7,8} = 减速/定身/锁足/眩晕 per
`_guying_functiontype.txt:32-79`) and/or `DETACH_MULTI_GROUP_BUFF` (§1.5), e.g.
`纯阳\子技能_镇山河驱散自己.lua` uses detach {2,4,6,8,10,12} then delete-by-FunctionType {4,8,7,2}.

`IgnoreControl=1` skills (skills that can be cast while controlled) were also catalogued:
73 player skills including 100 星楼月影, 412 疾如风, 371 镇山河, 372 转乾坤, 550 鹊踏枝,
13042 无惧, 14081/14162 孤影化双 (`_ignorecontrol_skills.txt:1-74`; names decoded from source in
`cc_examples` / skill dumps). Confidence MED (name/ID join).

---

## 5. Dispel / steal / transfer

### 5.1 Flag distribution (HIGH)

`proof/pvp/buffs/flags_catalog.txt:1-29` (counts over 61 555 rows):

| Column | Values (count) | Reading |
|---|---|---|
| `CanBeSteal` | 0 (60 092), **1 (1 434)**, '' (29) | can be stolen by 琴音共鸣-class effects |
| `CanTransfer` | '' (59 918), **1 (1 326)**, 0 (311) | can be transferred/spread to another target |
| `Exclude` | 1 (57 618), 0 (3 937) | default-on; 排斥/替换 behaviour |
| `GlobalExclude` | 1 (56 013), 0 (5 542) | global variant of above |
| `UniqueTarget` | 0 (61 317), **1 (237)**, '' (1) | one instance per caster across targets |
| `Coexist` | '' (61 475), 0 (51), **1 (29)** | same AppendType may coexist |
| `CanAccumulate` | 0 (48 540), '' (12 548), 1 (467) | accumulate value across applications |
| `CostSingleStack` | '' (61 473), 1 (57), 0 (25) | a use consumes one stack |
| `Save` | 1 (47 471), 0 (14 084) | persists through logout/phase |
| `OnFight` | '' (55 400), Fight (3 778), Any (1 248), NotFight (1 129) | combat-state validity |

### 5.2 Steal (HIGH)

- `STEAL_BUFF` attribute exists in scripts: `长歌\套路及子技能\长歌_气_琴音共鸣.lua` (and
  凌雪阁/沙漠风暴 copies) (`grep STEAL` over `PROBE\...\scripts`, hit in
  `_dispel_api_calls.txt` and source).
- Stealable examples: 112 清心静气, 113 碧水滔天, 122 春泥护花, 124 花语酥心, 126 毫针,
  134 坐忘无我 (`flags_catalog.txt:31-71`). These are exactly the “good buff” category, consistent
  with 琴音共鸣 (steal one buff). MED-HIGH.

### 5.3 Transfer (MED)

- `CanTransfer=1` examples: 549 天策-穿-减速, 566 立地成佛3, 576 恒河劫沙, 661 天策-破风
  (`flags_catalog.txt:73-113`). Game logic: DOT/减速 effects that certain skills (e.g.
  吞日月/驱散-转发 mechanics) can re-apply on another target. The exact transfer API was not
  located in client scripts (`TRANSFER` string absent from the script scan). **MED/open.**

### 5.4 Exclude / GlobalExclude / UniqueTarget / Coexist (MED, inferred)

- Engine has `KBuffList::CanAddBuff`, `KBuffList::GetBuffByAppendType`,
  `KBuffList::UpdateBuffData`, `KBuffList::CleanBuffByPersist`
  (`proof/pvp/buffs/_cms_context.txt:8-23`) — i.e. add-time slot/merge arbitration exists in the
  client buff list.
- Data pattern: `Exclude`/`GlobalExclude` default to **1** for almost all rows; the 0-valued rows are
  a small curated set (打坐/浮身/加速/碧水滔天/…, `globalexclude0_rows.tsv:2-13`) → these flags
  likely mean “excluded from the same-group replacement / global-purge pass when 1”.
- `UniqueTarget=1` examples: 406 水凝为冰, 410 狂暴 (`uniquetarget_rows.tsv:2-11`) → single instance
  per caster.
- `Coexist=1` examples: 28433 事件奖励CD variants, 28736/28737 治疗全能转治疗量计数
  (`coexist1_rows.tsv:2-13`); `Coexist=0` examples: 12850 明教_驱夜前置 variants, 28313 剑气护盾
  (`coexist0_rows.tsv:2-13`). Reading: with `Coexist=0` a new buff in the same AppendType replaces
  the old one; `Coexist=1` lets them coexist. **MED** (engine field `m_nBuffType`/AppendType assert
  support the slot model; the exact flag name→behaviour mapping is inferred).

### 5.5 Dispel skills found in scripts (HIGH for existence)

`DETACH_SINGLE_BUFF` / `DETACH_MULTI_GROUP_BUFF` used by (17 files; `_dispel_api_calls.txt`):
剑转流云 (七秀, args 7/3/5/11), 少明指 (万花), 枯残蛊_对友方 (五毒, 4/6/8/10/12), 霞流宝石
(藏剑, 7/3/5/11), 净世破魔击 (明教, 3/5/7/11), 圣明佑_月 (明教, 4/6/8/12), 云栖松 (纯阳,
4/6/8/12), 镇山河驱散自己 (2/4/6/8/10/12), 通用_驱散混元 (百战, 5/6), 剑转流云(北极), 天策
龙城铁壁_驱散, 轮回诀/锻骨诀 (少林), 方宇谦_归潮长生驱散, 十方玄机清自身buff. Group ids = §1.5.

---

## 6. Stacking / periodic tick model

Field semantics verified from data + engine asserts (`struct_member_runs_logic.txt:81-85`:
`MaxStackNum > 0 && <= UCHAR_MAX`, `Get Count` with `nActiveCount > 0`, `Get Interval/MinInterval/
MaxInterval` with `nIntervalFrame >= 0`, `IsIntensityStackable`, `IsStackable`).

| Field | Meaning | Evidence |
|---|---|---|
| `IsStackable` | stacks allowed | 122=1, 655=0, 203=0 |
| `MaxStackNum` | cap (0 = not stackable; 1..255) | 122 春泥护花 = **8**; 655 流血 = **3** |
| `Intensity` | primary magnitude; for CC buffs = duration in frames | 445 silence 48/56/64/72 (3–4.5 s); 556 root 400…720 (25–45 s) |
| `Count` | number of ticks/hits/applications | 631 握针 = 6; 655 流血 = 5; 203 啸如虎 = 5 |
| `Interval` | tick interval (frames) | 631 = 48 (3 s); 655 = 48; 203 = 32 (2 s) |
| `MinInterval` / `MaxInterval` | interval bounds (randomisation / accumulation) | 631 Min=1 Max=48; 655 Min=48 Max=48 |
| `IsIntensityStackable` | intensity accumulates per stack | 122 (8 stacks × 2 %), `_key_buff_summary` |
| `CanAccumulate` | value accumulation across applications | 0/1/'' distribution §5.1 |
| `CostSingleStack` | using the buff consumes 1 stack | 57 rows = 1 |
| `ActiveCoefficient` | coefficient applied on active tick | all examples = 1 |

### 6.1 Verified numeric examples

1. **122 春泥护花** (`stacking_rows.tsv:5-11`): levels 1-7, `Intensity` 28→30→32→…→55,
   `IsStackable=1`, `MaxStackNum=8`, `Count=1`, `Interval=320` (=20 s),
   `MinInterval=MaxInterval=320`, `CanBeSteal=1`, `CanCancel=1`, script
   `万花/非弹梦春泥消失.lua`. → 8-stack damage reduction, ~2 % per stack.
2. **631 握针** (`stacking_rows.tsv:24-32`): `IsStackable=0`, `MaxStackNum=1`, `Count=6`,
   `Interval=48` (=3 s), `MinInterval=1`, `MaxInterval=48`, `CanBeSteal=1`, script
   `万花/握针完整跳后恢复墨意.lua`. → HoT with 6 ticks at 3 s.
3. **203 天策-啸如虎-傲血战意** (`stacking_rows.tsv:12-17`): `Intensity` 2..6,
   `IsStackable=0`, `MaxStackNum=1`, `Count=5`, `Interval=32` (2 s), script
   `天策\虎加护盾.lua`. Level 6 has `Count=1, Interval=160`.
4. **655 流血** (`stacking_rows.tsv:53-67`): `MaxStackNum=3`, `Count=5`, `Interval=48`,
   `Exclude=1`, `GlobalExclude=1`; levels 1-15 encode `Intensity=1..15`.
5. **556 七星拱瑞** (`stacking_rows.tsv:19-23`): `Intensity=400/480/560/640/720` frames
   (25/30/35/40/45 s), `DecayType=3`, `MS=0x53C02000`, `MS2=60`, `BeginAttrib1=atFreeze:0`.

---

## 7. PvP-relevant buffs

Keyword catalog: `proof/pvp/buffs/pvp_keyword_buffs.tsv` (clean UTF-8, 860 rows;
columns ID/Name/FunctionType/DecayType/MS/MS2/MapBanMask/MapInvalidMask/BeginAttrib1/Count/Interval/
MaxStackNum/ScriptFile) and pretty printer output `pvp_keyword_view3/4/5.txt`.
Richer attribute sets: `proof/pvp/attributes/buff_mitigation_rows.txt`, `buff_pvp_names.txt`,
`buff_pvp_rows.txt`.

### 7.1 Debuff families (ID, name, attribute pair)

| Family | Examples (ID, name, key attribute) | Evidence |
|---|---|---|
| 减疗 (healing reduction) | 9514 楚济_减疗40% `atBeTherapyCoefficient=-461`; 15033 MOBA减疗 `-512`; 30427 丐帮减疗 `-563`; 24324 苍云_分山劲_决死减疗 `-307`; 51331 穿心弩减疗 `-512, Count=6, Interval=48` | `pvp_keyword_view1.txt` (减疗 section); `pvp_keyword_buffs.tsv` |
| 禁疗 (heal absorb) | 10332 藏剑_禁疗 `atGlobalTherapyAbsorb=9999999`; 9175 生息禁疗buff `=9000000, Count=4`; 11488 禁疗 `atBeTherapyCoefficient=-1014, MapBan=920, MapInv=408`; 19138 帮会联赛禁疗 `-4096` | `pvp_keyword_buffs.tsv` (禁疗) |
| 破防 (armor/overcome) | 661 天策-破风 `atPhysicsShieldBase=-7` (stacks 5, 480 f); 703 剑法·破防 `atPhysicsShieldPercent=-205`; 2806 七秀_碎冰 `atSolarOvercomePercent=51`; 14941 MOBA光环破防 `atAllTypeOvercomeBase=102` | `pvp_keyword_view4.txt:69-110` |
| 减速/控场 | 509 减速; 562 封轻功 family; 523 剑主天地-迟缓 | §2 |

### 7.2 Defensive buffs

| Family | Examples | Evidence |
|---|---|---|
| 减伤 | 2716 霸皇诀_减伤80% `atGlobalResistPercent=512` (512/1024 = 50 %…); 2251 春泥护花秘籍 `=51`; 122 春泥护花 8 stacks; 5722 少林_万佛_加减伤 `=102, Stack=5`; 4227 狂暴免伤 `atGlobalBlock=1` | `pvp_keyword_view3.txt` (减伤/免伤); `buff_mitigation_rows.txt:126-131` |
| 无敌 (invulnerable) | 160 无敌 `atNegativeShield=1`; 205 无敌BUFF `=1, Int=9600`; 856 锻骨诀内功无敌 `atLunarMagicBlock=1`; 2946 凤凰蛊 `atGlobalResistPercent=1024`; 377 镇山河 `atNegativeShield=1 + atGlobalBlock=1` | `pvp_keyword_view1.txt` (无敌); `buff_mitigation_rows.txt:3-13` |
| 免伤 (block/absorb) | 4227 狂暴免伤 `atGlobalBlock=1`; 778/32878/33493 `atGlobalBlock=1`; 7879 挟持人质免伤 `atGlobalDamageAbsorb=200000000`; 342 交通/682 雷霆 `atPositiveShield=1` | `buff_mitigation_rows.txt:2-6,17-20`; `pvp_keyword_view3.txt` (免伤) |
| 免控 (immunity) | 677 鹊踏枝 `atImmunity 2/4/7/8 + atDodgeBaseRate 3000`; 2756 任驰骋; 5995 笑醉狂; 6219/5950 五毒献祭; **all first-attribute rows show `atImmunity:2`** | `pvp_keyword_view3.txt` (免控, 60 rows); `immunity_clean_*.tsv` |
| 反伤/反弹 (reflect/mirror) | 371 反弹 `atPhysicsReflection=100`; 388 罗汉金身段1 `at{Physics,Solar,Lunar,Neutral,Poison}MagicReflectionPercent=136` (=13.3 %); 1078 反弹 `atPhysicsReflection=50 + 4 magic types`; 8303/8438 盾立 `atMirrorShield()`; 27119 上岩反弹 `atDamageFeedbackPercent=81` | `pvp_keyword_view4.txt:2-67`; `buff_mitigation_rows.txt:23-28,42-53` |
| 化劲 (crit-damage reduction) | 1455 增加化劲 `atDecriticalDamagePowerBase=54`; 1854 香梅 `atToughnessBase/atStrainBase/atDecriticalDamagePowerBase`; tooltip 3874 “化劲等级提高6%,御劲等级提高2%” | `buff_pvp_names.txt` (化劲 section) |
| 御劲 (anti-crit) | 1072 御劲 `atToughnessBase=162`; 1458 增加御劲 `=50`; 2198 铁壁 `atToughnessPercent=205`; 14980 御劲提高 `atToughnessBaseRate=500`; 9864 疾如风 “被会心几率降低10%” `atToughnessBaseRate=3000` | `buff_pvp_names.txt` (御劲) |

Note attribute scale: 1024 = 100 % (`PERCENT_BASE`), `-512` = −50 %; coefficients are
`KiloNum`/BASE based (see `proof/pvp/attributes/GlobalParam_coefficients.tsv`).
Cross-check with the attribute workstream: `proof/pvp/proof/pvp/attributes_and_damage.md` and
`proof/pvp/attr_catalog.tsv`.

### 7.3 Arena / battlefield / 绝境 (BR) specific buffs

- **Arena (竞技场)**: 3369-3373 竞技场_删食品/宴席/药品 (system purge buffs), 3387-3392
  竞技场_删撼如雷/清心静气/秀气/雨霖铃/般若诀/药石气劲, **11837 竞技场禁止换心法
  `MapBanMask=520`**, 13597 竞技场状态机无敌buff `atNegativeShield=1, Count=400`,
  13598 减伤 `atRepulsedRate=-1024`, 16654 竞技场能力修正 `atBeTherapyCoefficient=-41, Stack=33`
  (`pvp_keyword_view5.txt:17-77`; `buff_arena_group.tsv:1-54`).
- **Battlefield**: **12009 战场禁止切心法 `MapBanMask=520`**, 14726 战场禁止用奇穴提示,
  27914 体验战场机器人减伤 `atGlobalResistPercent=51`, 26041 机器人能力修正 (`view5:78-138`).
- **BR 绝境**: 16586 绝境的馈赠_少林 `atFinalMaxLifeAddPercent=61, MapBanMask=255`;
  27421 绝境狂怒 `atAllDamageAddPercent=103, MapBanMask=472`; 28508 绝境_圣明佑
  `atSetHitReduceRate=2000`; 28541 绝境_大道定身 `FT=Halt DT=2 MS=0x53C02000`;
  21124 天原绝境_滑翔维持 `MS=16`; 21181/21182 寒冷 (haste −300 / max-life −30);
  26073/26074 洱海绝境复活; 28542-28567 绝境·X skill variants; class-replacement buffs like
  51358 绝境_星楼月影, 51468 绝境_啸如虎, 51849 绝境_笑醉狂, 51362 绝境·疾如风
  (`pvp_keyword_view5.txt:139-199`; `docs/netcode/JX3_MODE_JUEJING_LOGIC.md:65-78`).

---

## 8. MapBanMask / MapInvalidMask & mode restriction encoding

### 8.1 Two parallel mask systems sharing one map-category bit encoder (MED-HIGH)

- **Skills**: `skills.tab` col **67 = MapBanMask** (`tools/pvp/tab.py HEADER`, cols 63/64 =
  Self/TargetMoveStateMask). `MapList.tab` col 23 = `BanSkillMask` (header dump
  `proof/pvp/buffs/_maplist_head.txt:2`; machine names `JX3ClientX64_exe_net_strings.txt:0x00801E98`).
- **Buffs**: `Buff.tab` col **87 = MapBanMask**, col **95 = MapInvalidMask**. `MapList.tab` col
  **46 = InvalidBuffMask** (`_maplist_head.txt:2`, `JX3ClientX64_exe_net_strings.txt:0x00801EA8`;
  col 23 = BanSkillMask, col 25 = RevieCycle).
- Engine reads `MapInvalidMask` at buff load: `[Buff] Get MapInvalidMask`
  (`JX3ClientX64_exe_net_strings.txt:0x0083D740`). **No client-engine string for `MapBanMask`** was
  found (not in KSkillManager/KBuffManager getter runs); ban enforcement is likely
  server-authoritative / logic-side. **MED** (absence of evidence).
- Reading: bit *n* is a map **category**; a map is in the category if that bit is set in the map's
  `BanSkillMask`/`InvalidBuffMask`. A skill/buff is banned/invalidated when
  `(objectMask & mapMask) != 0`. Evidence: 绝境 maps have `BanSkillMask=512` and skills banned in
  绝境 (打坐 17, 锋针 139, 轮回诀 259, 涅槃重生 2229, 妙舞神扬 3003 …) all have
  `MapBanMask=512` (`proof/pvp/modes/skills_mapban_juejing_bit512.tsv:2-15`,
  `docs/netcode/JX3_MODE_LOAD_FLOW.md:49`). Arena maps have `BanSkillMask=8`
  (`maplist_mode_flags.tsv:23-25`) and arena-banned gathering/food buffs carry bit 3
  (`mapban520_rows.tsv:2-15`; 520 = 8 + 512 = arena + BR).

### 8.2 Observed category bits

From `MapList.tab` (`proof/pvp/buffs/mapmask_correlation.txt:1-478`, `_pvp_map_masks.txt:1-27`):

| Bit | Value | Map category | Example maps |
|---|---|---|---|
| 0 | 1 | PvE dungeon group A | 灵霄峡/天工坊/无盐岛… (mask 131 = bits 0,1,7) |
| 1 | 2 | PvE dungeon group B | same dungeon masks |
| 3 | 8 | **competitive PvP: arena + 绝境** | 127 天山碎冰谷, 128 乐山大佛窟, 129 华山之巅, 137 大漠楼兰, 173 齐物阁, 181 狼影殿, 238/277/362/529/530/624/696 (arena); 410 海岛绝境, 512 白龙绝境, 532 天原绝境, 645 洱海绝境, 709/715 林海绝境, 296/297/676/677 龙门寻宝 |
| 4 | 16 | 拭剑园 / duel gardens | 6 扬州, 8 洛阳, 15 长安, 108 成都, 172 长安内城, 276/278-281 拭剑园, 293 拭剑园战场, 321 思过园, 332 侠客岛, 413/414 望扬镇, 455/462/463 家园 |
| 5 | 32 | 试炼秘境 | 143-147, 247 梦回稻香, 446 试炼之地 |
| 6 | 64 | 大唐监狱 | 152 大唐监狱 |
| 7 | 128 | raid/PvE dungeon group C | 133/134 烛龙殿, 164/165 大明宫, 175/176 血战天策, 177/178 风雪稻香村, 182/183 秦皇陵, 191-206, 220-241 上阳宫/永王行宫, 263-273 风雷刀谷, 283-289 狼牙堡, 298-301, 341-361 冰火岛, 364-370, 426-428 敖龙岛, 452-454 范阳夜变, 482-484 达摩洞, 518-520 白帝江关, 559-562, 573-575, 586-588, 620, 636-638, 648-650, 668-670, 686-688, 706-711, 720, 722-724, 793-795, 804, 807 |
| 8 | 256 | **战场 (battlefield)** | 29/39 云湖天池, 38 神农洇, 48 珍珑棋谷, 50 丝绸之路, 52, 121, 135 三国古战场, 149 帮会约战, 186 冷香丘, 322 洛道_李渡城, 415 野狸岛, 515 帮会联赛, 589 羊村, 689 帮会联赛_2024, 712 雪域关城, 801 大富翁 |
| 3+5+7 etc. | 171 | 浪客行 | 421-425, 433-443, 461, 527, 528, 631 |
| 9 | 512 | (skill-ban category only) BR skills | skills `MapBanMask=512`; BR maps `BanSkillMask=512` |

### 8.3 Buff value samples (verified)

- `MapInvalidMask=8` (413 rows): 1171 基础_增加生命最大值, 1172 基础_增加内力最大值,
  1624 元旦活动_玩家标志_首领撑抱了 (`mapinvalid8_rows.tsv:2-19`).
- `MapInvalidMask=264` (=8+256; 64 rows): 772 回神 `atNegativeShield=1`, 水枪/水龙滚 quest
  items, 10100 炼狱/百炼水煮鱼 food (`mapinvalid264_rows.tsv:2-13`).
- `MapBanMask=520` (=8+512; 363 rows): 1175-1179 生活技能_采集BUFF, 1287 心领神会,
  1594 如意饺, 11837 竞技场禁止换心法, 12009 战场禁止切心法 (`mapban520_rows.tsv:2-15`).
- `MapBanMask=8`: 1575 云屏红烛 furniture buffs, 14420 江湖行记 attribute buffs
  (`mapban8_rows.tsv:2-15`).
- Top values: `MapBanMask` '' 52 200, 0 8 012, 520 363, 8 325, 1 161, 255 155, 16 39, 264 39;
  `MapInvalidMask` 0 41 717, '' 19 167, 8 413, 264 64, 296 60, 503 29 …
  (`mapmask_correlation.txt:480-567`).

### 8.4 Mode-load cross-check

`docs/netcode/JX3_MODE_LOAD_FLOW.md:49-57`: BR maps set `BanSkillMask=512`, `InvalidBuffMask=8`,
`BanChangeTalent=1`, no `ScriptFile`; `docs/netcode/JX3_MODE_JUEJING_LOGIC.md:75-78` recaps the same
and lists BR buff variants (§7.3). This matches the data in `maplist_mode_flags.tsv:42-82`
(龙门寻宝/沧溟/白龙/天原/洱海/林海 rows). BanSkillMask values per map family:
BF=256, ARENA=8, BR=512, 拭剑园=16, 试炼=32, 浪客行=171 (`maplist_mode_flags.tsv`).
**HIGH** for the values; **MED** for the “bit category ∩ mask” enforcement model (client tables +
skill/buff value correlation, no disassembly proof).

---

## 9. Open items

1. **MoveStateMask semantics** — whitelist vs suppression; confirm from client disassembly
   (likely near `KBuffList::CheckValidity`/`UpdateBuffValidity`, `_cms_context.txt:19-23`).
2. **MoveStateMask2 bit order/enum** — 6-bit mask hypothesis (63=all, 60/56 for CC) unproven;
   need the engine enum values for ON_FLY_FLOAT…ON_BIRD_JUMP.
3. **DR ladder** — per-application decrement table and immunity-reset rules are engine-side; the
   client tables only expose `DecayFrame`/`ImmunityFrame` (identical in this build).
4. **Buff.tab `MapBanMask` enforcement** — no client getter string found; verify server-side or in
   logic-obfuscated table loaders.
5. **Exclude / GlobalExclude / Coexist exact semantics** — inferred from data distribution and
   `KBuffList::CanAddBuff`/`GetBuffByAppendType`; needs targeted script experiments or disassembly.
6. **DetachType groups 9, 10, 13, 14, 15-24, 52+** — names/inference only.
7. **CC category ids 10 / 12 / 14-17** — used by 圣明佑_月 and newer buffs; meanings unknown.
8. **CanTransfer mechanics** — flag verified, transfer API not found in client scripts.
9. **Encoded legacy evidence** — many `_*` files are double-encoded; future edits should use the
   clean regenerated files (below) or re-extract with `tools/pvp/tab.py` (GB18030).

---

## 10. Evidence index (files written/updated in this workstream)

**Report:** `proof/pvp/buff_control_system.md` (this file).

**New/regenerated evidence (all UTF-8):**

| File | Content |
|---|---|
| `proof/pvp/buffs/field_semantics.txt` | Useage/FunctionType/BuffType/AppendType/DetachType value→count→examples |
| `proof/pvp/buffs/detach_groups.txt` | per-DetachType membership listing |
| `proof/pvp/buffs/decay_types_verified.tsv` | data-driven DecayType membership + audit of previous mismatches |
| `proof/pvp/buffs/cc_examples.tsv` | 339 clean control-buff rows (35 cols + decoded masks) |
| `proof/pvp/buffs/stacking_rows.tsv` | clean stacking/periodic rows (103/104/122/203/418/556/631/655/8067/11605/3466) |
| `proof/pvp/buffs/immunity_clean_*.tsv`, `immunity_clean_sets.tsv` | per-skill/category immunity attribute sets (生太极/星楼月影/天地低昂/疾如风/鹊踏枝/啸如虎/笑醉狂/无相诀/镇山河/转乾坤 + atImmunity=2/4/5/8/9/11 anchors: 7173/700/763/18246/4344/20704/27102 + newer 14/15/16/17 rows) |
| `proof/pvp/buffs/pvp_keyword_view1.txt`–`view5.txt` | pretty-printed PvP keyword sections (减疗/禁疗/无敌/免伤/免控/反伤/反弹/破防/化劲/御劲/名剑/竞技/战场/绝境) |
| `proof/pvp/buffs/mapinvalid8_rows.tsv`, `mapinvalid264_rows.tsv`, `mapban520_rows.tsv`, `mapban8_rows.tsv` | mask-value samples with names |
| `proof/pvp/buffs/uniquetarget_rows.tsv`, `coexist0_rows.tsv`, `coexist1_rows.tsv`, `globalexclude0_rows.tsv` | flag samples |
| `proof/pvp/buffs/bufftxt_decay_lines.tsv`, `skilltxt_decay_lines.tsv`, `skilltxt_jiekong_lines.tsv`, `skilltxt_miankong_lines.tsv`, `bufftxt_fengnei_lines.tsv`, `bufftxt_zhmang_lines.tsv`, `bufftxt_mabi_lines.tsv` | tooltip scans |
| `tools/pvp/field_semantics.py`, `detach_groups.py`, `decay_members.py`, `pvp_buff_view.py`, `mask_rows.py` | generators for the above |

**Pre-existing evidence reused (unchanged):** `proof/pvp/buffs/cc_families.tsv`,
`movestate_decode.txt`, `_client_movestate_run.txt`, `_movestate_enumreflect.txt`,
`flags_catalog.txt`, `mapmask_correlation.txt`, `_pvp_map_masks.txt`, `_invalidbuffmask_context.txt`,
`_maplist_head.txt`, `_logicedit_functiontype_run.txt`, `_guying_functiontype.txt`,
`_delmulti_functiontype_map.txt`, `_detachmulti_ctx.txt`, `_dispel_api_calls.txt`,
`_lh_movestate_context.txt`, `_cms_context.txt`, `_bytecode_control_consts.txt`,
`_ignorecontrol_skills.txt`, `_immunity_names.txt`, `_suozu_summary.txt`, `_qixing_script.txt`,
`_zhenshanhe_script.txt`, `_youfeng_script.txt`, `_shengmingyou_yue.txt`;
`proof/pvp/decay_and_controls.tsv` (see §4.1 corrections);
`proof/pvp/attributes/{buff_mitigation_rows,buff_pvp_names,buff_pvp_rows,buff_header}.txt`;
`proof/pvp/modes/{maplist_mode_flags,maplist_pvp_rows,buff_arena_group,skills_mapban_juejing_bit512}.tsv`;
`proof/pvp/cast/mapban_bits.txt`; `proof/netcode/JX3RepresentX64_all_strings.txt`,
`JX3ClientX64_exe_net_strings.txt`, `JX3LogicEditOperation_net_strings.txt`;
`docs/netcode/JX3_MODE_LOAD_FLOW.md`, `JX3_MODE_JUEJING_LOGIC.md`.
