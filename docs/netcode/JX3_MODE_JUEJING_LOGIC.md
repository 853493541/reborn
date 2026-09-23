# 绝境战场 — mode logic visible from the client

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Sources:** extracted PakV4 settings tables (`skills.tab`, `Buff.tab`, `CoolDownList.tab`,
`MapList.tab`, `Doodad*`), client binaries (symbols), editor prop tables.
**Evidence:** `proof/netcode/mode_juejing/mode_kit_report.txt`, `mode_skills.txt`,
`mode_strict_search.txt`, `buff_zone_search.txt`, `scripts_extract_log.txt`.

This is what the **client** knows about the mode's logic. The rule scripts themselves
run server-side (verified: skill `ScriptFile` Lua files are not shipped in the client —
748 candidate paths probed, 0 hits; `Activity.tab` labels them `ServerScript`).

---

## 1. Mode skill kit (`settings\skill\skills.tab`, 182 rows contain 绝境)

| Group | Skills (SkillID) |
|---|---|
| Kungfu roots | `绝境战场技能` (64888), `绝境战场子技能` (64902), `沙漠风暴` (10516) |
| Movement / qinggong | 扶摇直上 (37681), 蹑云逐月 (37682), 凌霄揽胜 (37683), 瑶台枕鹤 (37684), 迎风回浪 (37685), 后撤 (37686), 骑乘 (37746); mobile 38381/38382; 林海 variants 41281-41284 |
| Starter weapon attacks (ground loot) | 四象轮回 64889 · 穿云 64890 · 普渡四方 64891 · 夺魄箭 64892 · 夕照雷锋 64893 · 阳明指 64894 · 蝎心 64895 · 赤日轮 64896 · 江海凝光 64897 · 横扫六合 64898 · 捕风 64899 · 抱残 64900 · 守缺 64901 (+ 自动赤日轮 64986, 守缺一段 64989, 伤害子技能 64903) |
| Mode abilities | 回风扫叶 (37687), 渊 (37717), 雾暗迷云 (39483), 截阳 (39492), 引窍 (39493), 风流云散 (39494), 雷霆震怒 (65156); sub/damage: 截阳伤害 (39526), 引窍伤害 (39527), 绝脉爆炸 (39528) |
| Class skills adapted to mode | 春泥护花_绝境战场 (33285), 林海绝境进战/脱战 (41291/41292), 吃鸡专用霸刀切大刀无武器 (41293) |
| Map mechanics — 天原绝境 | 滑翔伞 (29021), 照明火把释放/实际效果 (29043/29044), 燧石单体 (29051), 冰雹aoe (29055), 剑宗深寒 (29056), 武家炽血 (29061), 橙色下装被击降攻击 (29068), 极寒掉血 (29342), 战象践踏 (29146), 战象冲锋 (29264), 战象下车 (29267), 战象践踏母 (29283), 战象推人母 (29476), 天玄冰 (29637), 冰封复活 (31507) |
| Map mechanics — 洱海绝境 | 脱战附近aoe (35149); 飞爪 exists as cooldown id 2617 |
| Items | 道具_楚河汉界 (27902) |

`KindType` values seen: `Leap` (dash/轻功), `Adaptive`, `Physics` — the mode's kit is
school-agnostic (all belong to kungfu 64888/64902, `BelongSchool=13` = 江湖/无门派).

## 2. Cooldowns (`settings\CoolDownList.tab`, note column)

| ID | Duration | Effect (note) |
|---|---|---|
| 2173 | 90 s | 天原绝境_火把 |
| 2174 | 12 s | 天原绝境战象冲锋 (accelerable) |
| 2175 | 3 s | 天原绝境战象践踏 (accelerable) |
| 2192 | 10 s | 天原绝境天玄冰 (accelerable) |
| 2375 | 60 s | **吃鸡**天原绝境破冰 |
| 2617 | 60 s ×3 | 洱海绝境_飞爪 |
| 2630 | 1 s | 洱海绝境_飞爪保护 |
| 2856 | 3 s | 战场排队内置CD |
| 2875 | 45 s | 绝境·无相 |
| 2897 | 15 s | 绝境·连环? |
| 2898 | 40 s | 绝境·风来吴山 |
| 2989 | 45 s ×3 | 绝境楚河汉界40秒调息 |
| 2990 | 60 s | 绝境雷霆震怒 |

`NeedSyncOB`/`CanAccelerate`/`MaxCount` show which mode cooldowns are synced to the
observer cache and which can be accelerated — matching the `OnSyncBattlefieldCompetitorCDState`
protocol family.

## 3. Loot / gear progression (battle-royale core)

- `Doodad\沙漠风暴.tab` (256 rows): world loot props **一阶/二阶/三阶装备 + 武器**
  (tier 1-3 armour and weapons) — the ground-loot ladder.
- Weapon tiers grant the starter attack kits in §1 (the `（武器初始）` skill set is per
  weapon type), so picking up a weapon replaces your attack kit.
- Gear passives (equip skills): 伤害吸收 (18879), 被动反击 (18880), 降低技能命中率
  (18881), 附加dot (18882), 被动减速 (18878).
- `Doodad\沙漠风暴_寻宝模式.tab`: 伪装蘑菇/药罐/食人花/遗骸 (disguises), 匿踪宝盒.
- `Doodad\寻宝系统.tab`: 九州祈福灯, 宝箱.

## 4. Buffs / class interactions

| Buff | Note |
|---|---|
| `移动端_衍天_绝境战场_起卦回血` (71717) | 衍天 起卦 heals in the mode |
| TopBuff 4052 rows | 明教_暗尘弥散_绝境月大使用 / 日月晦使用 — class ability variants |
| `春泥护花_绝境战场` (33285) | 万花 skill replaced by mode variant |

## 5. Entry / rules (recap from `JX3_MODE_LOAD_FLOW.md`)

- `MapList.tab` flags per 绝境 map: `IsBattlefield=1`, `CampType=5`, `MaxPlayerCount=118`
  (108 some copies), `MaxCopyCount=512/1024`, `BanSkillMask=512`, `BanUseItemMask=4/32`,
  `InvalidBuffMask=8`, `ReviveInSitu=0`, `RevieCycle=160`, `QueueForSwitchWhenFull=1`,
  `BanChangeTalent=1`, `CanSprint=1`/0 (林海), `OperationMask=7`, no `ScriptFile`.
- Queue: solo + guild (`JoinBattleFieldQueue` / `JoinTongBattleFieldQueue`), countdown
  buff `BF_QUEUE_COUNTDOWN_BUFF`, room create + `ChaosFight` force-start.
- Teams/sides: BF team group ids, `SetBattleFieldSide`.

## 6. Runtime state the client maintains (the mode HUD)

`KBattlefieldCache` (periodic `AddNextSyncTime`) + `m_BattlefieldCompetitorInfoMap`:
competitor list/base+variable info, **skill cooldown state**, buff lists, statistics,
positions (`GetBattlefieldPlayersPosInfo`), rank data (`GetBFRank*`, `ApplyBattleFieldStatistics`),
objectives (`GetBattleFieldObjective`), head-top class/param (`GET_ALLBFPLAYER_KUNGFUID/_PARAM`),
income penalty (`BattleFieldIncomePunish`).

## 7. What is not in the client

| Missing | Evidence / reason |
|---|---|
| mode rule scripts (zone phases, shrink, scoring, respawn timers) | skill `ScriptFile` Lua files absent from PakV4 (748-path probe, 0 hits); `Activity.tab` marks scripts `ServerScript`/`CenterScript` |
| revive tables per map | `map\<map>\NpcRelive\MapReviveList.tab` referenced by logic but not extractable from the client pak |
| zone/shrink constants | no 缩圈/沙暴/安全区 terms in any extracted client table |
| exact damage/heal numbers | server-side simulation + sync |

To obtain the runtime logic without server files:
1. capture the S2C families above (`OnSyncBattlefieldStatistics`,
   `OnSyncBattlefieldCompetitorBuffList`, `...CDState`, plus map-scoped `OnSync*`) and
   reconstruct the state machine; or
2. read `KBattlefieldCache` / `m_BattlefieldCompetitorInfoMap` in a running client; or
3. observe the 倒计时 UI PSS (`UI_黑山绝境_倒计时通用底框.pss`) and mode HUD for phase timing.

## 8. Reproduce

```powershell
python tools\netcode\extract_pak_paths.py --list proof\netcode\mode_juejing\pak_candidates4.txt --out-dir proof\netcode\mode_juejing\pak_out4   # Buff/TopBuff/Activity/CoolDown...
python tools\netcode\extract_mode_kit.py                                                       # mode kit report
```

Evidence: `mode_kit_report.txt` (418 lines), `mode_skills.txt` (182 绝境 skills),
`mode_strict_search.txt` (cooldowns), `scripts_extract_log.txt` (scripts absent),
`juejing_maplist_rows.txt`, `exe_mode_keywords.txt`, `battlefield_api_symbols.txt`.
