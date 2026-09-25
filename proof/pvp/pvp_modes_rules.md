# JX3 (剑网3) PvP modes & battle rules — consolidated report

**Worktree:** `reborn-pvp` · **Branch:** `research/jx3-pvp-battle` · **Date:** 2026-09-24
**Status:** consolidates the interrupted agent's raw evidence (`proof/pvp/**`), re-verified against the
source tables; all numbers below were re-checked with `tools/pvp/verify_pvp_evidence.py` and
`tools/pvp/dump_modegroups.py`.

**Confidence legend (per claim):** **HIGH** = literal value read from a table/string with a citation;
**MED** = interpretation strongly implied by names/enums but not proven; **LOW** = working hypothesis.

## 0. Sources & verification performed

| Source | Use |
|---|---|
| `C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing\pak_out2\MapList.tab` | map/mode flags (677 data rows × 77 cols) |
| `...\pak_out4\RelationCamp.tab`, `CampLevelParam.tab`, `Buff.tab` | camp relations / camp-level params / buffs |
| `PROBE\logic-skill-prefixed-out\settings\skill\skills.tab`, `Buff.tab`, `DecayType.tab` | skill/buff/control tables |
| `PROBE\ad-desc-probe-out\ui\Scheme\Case\{Skill,Buff,Attribute}.txt` | UI tooltip strings |
| `...\ability-matcher\extracted\scripts\skill\绝境战场\|沙漠风暴\*.lua` | BR skill scripts |
| `proof\netcode\engine_strings.txt`, `JX3ClientX64_exe_net_strings.txt`, `JX3UIX64_net_strings.txt` | engine API/protocol surface |
| Game install `interface\`, `ui\` | shipped (encrypted) UI Lua |

**Verification run (new artifact):** `proof/pvp/modes/verification_2026-09-24.txt`
- `maplist_mode_flags.tsv`: 85 unique IDs, **0 mismatches** vs `MapList.tab` (all 35 compared fields).
- `skills_mapban_arena_bit8.tsv` (529 rows), `skills_mapban_bit2.tsv` (4), `skills_mapban_bit504.tsv` (69),
  `skills_mapban_bit520_8_512.tsv` (4), `skills_mapban_juejing_bit512.tsv` (14):
  **0 mismatches** vs `skills.tab`; every row really has the claimed `MapBanMask` bits.
- New verified dump: `proof/pvp/modes/maplist_modegroups_verified.tsv` (groups by flag; line ranges cited below).

Map flags are read by **column name** from the 77-col header in `proof/pvp/maplist_header.txt`:
`ReviveInSitu`=col 20, `BanSkillMask`=23, `BattleRelationMask`=24, `RevieCycle`=25, `CampType`=34,
`FightList`=35, `bCanTongWar`=38, `CanJoinBattleField`=39, `BanUseItemMask`=40, `CanJoinArena`=41,
`IsArenaMap`=42, `NewCampFight`=43, `CanJoinTongBattlefield`=44, `InvalidBuffMask`=46,
`BanChangeTalent`=48, `CanSprint`=49, `QueueForSwitchWhenFull`=52, `IsTongWarMap`=59,
`IsBattlefield`=66, `IsTongLeagueMap`=70, `OperationMask`=71, `bCanPK`=6, `bCanDuel`=7.

> Caveat: the old category files are broader than the flags. `maplist_summary.txt` counts
> `campfight`=677 (nearly every map has non-zero `CampType`/`FightList`) and `battlefield`=112
> (includes `CanJoinTongBattlefield`). Use the **flag groups** below for exact mode instances.

---

## 1. Complete PvP mode enumeration (MapList.tab)

Flag distributions re-verified on the source (HIGH), source `MapList.tab` + `maplist_summary.txt:1846-1858`:

```
IsArenaMap=1: 11   IsBattlefield=1: 26   IsTongWarMap=1: 1   IsTongLeagueMap=1: 1
NewCampFight=1: 21 bCanTongWar=1: 45     CanJoinArena=1: 84  CanJoinBattleField=1: 84
CampType: {1:562, 4:63, 5:52}            Type: {0:219,1:404,2:42,3:2,4:2,5:8}
BanSkillMask: {0:230,128:340,129:1,130:5,131:25,16:14,2:1,256:4,3:1,32:5,512:31,6:2,64:1,8:17}
BanUseItemMask: {1:15,16:622,2:7,32:2,4:29,8:2}   InvalidBuffMask: {0:200,128:118,131:260,16:27,171:20,256:17,32:7,6:2,64:1,8:25}
```

### 1.1 Arena instances — `IsArenaMap=1` (HIGH) — `maplist_modegroups_verified.tsv:2-12`

| MapID | Name / DisplayName |
|---|---|
| 127 | 天山碎冰谷 · 128 乐山大佛窟 · 129 华山之巅 · 137 大漠楼兰 (`大漠楼兰.map`) |
| 238 | 青竹书院 · 277 拭剑台 · 362 墟海之眼 |
| 529 | 藏剑武库 · 530 墟海之眼_演武场 · 624 沃石火狱 · 696 南诏段氏_红叶泽 |

Common flags (all 11, verified): `Type=2, MaxCopyCount=2048, MinPlayerCount=1, MaxPlayerCount=1024,
KeepTime=2, ReviveInSitu=0, RevieCycle=0, BanSkillMask=8, BattleRelationMask=2, CampType=4,
BanUseItemMask=1, CanJoinArena=0, IsArenaMap=1, CanJoinTongBattlefield=0, InvalidBuffMask=8,
BanChangeTalent=0, CanSprint=0, QueueForSwitchWhenFull=1, OperationMask=7, bCanPK=0, bCanDuel=0`.
(137 大漠楼兰 also has `FightList=1`; nothing else differs.)

### 1.2 Battlefield instances — `IsBattlefield=1` (26 rows) — `maplist_modegroups_verified.tsv:13-38`

**绝境战场 (BR), 10 rows (HIGH):** 296 龙门寻宝/龙门绝境, 297 龙门寻宝_夜晚, 410 海岛绝境/沧溟绝境,
512 白龙绝境, 532 天原绝境, 645 洱海绝境, 676/677 龙门寻宝 (108-player alt copies), 709 林海绝境,
715 林海绝境·纷争.

**Instanced 战场 (BG), 16 rows (HIGH):** 38 神农洇(34人), 39 四十级云湖天池(24), 48 珍珑棋谷(54),
50 丝绸之路(54), 52 七十级云湖天池(24), 135 三国古战场(34), 186 冷香丘/浮香丘(34),
322 洛道_李渡城/李渡鬼域(100), 412 列星岛(14), 415 野狸岛(24), 589 羊村/羊村大作战(5),
689 帮会联赛_2024/九素云峰(210; also `IsTongLeagueMap=1`), 712 雪域关城(34),
790 扬刀大会_擎霄山(10), 801 ACT_大富翁(4), 807 欢乐谷/鹅鸭大游园(10).
`ScriptFile` is empty for every one of these **except** 807 (`scripts/Map/ACT_三方杀/ACT_三方杀.lua`) —
mode logic is server-side (`pvpfield_and_scriptfile.txt`; `JX3_MODE_LOAD_FLOW.md` §1/§6).

### 1.3 帮会 systems (HIGH)
- `IsTongWarMap=1`: **149 帮会约战 / 雪原争锋** (`maplist_modegroups_verified.tsv:39`):
  `MaxPlayerCount=54, RevieCycle=160, BanSkillMask=256, BattleRelationMask=216, CampType=5,
  bCanTongWar=0, CanJoinTongBattlefield=0, IsTongWarMap=1, CampQueue=1, QueueForSwitchWhenFull=1`.
- `IsTongLeagueMap=1`: **689 帮会联赛_2024** (same row as BF above; `KeepTime=120`, `IsTongLeagueMap=1`).
- `bCanTongWar=1`: 45 open-world maps allow 帮战/阵营战 (`maplist_modegroups_verified.tsv:62-106`),
  including 洛道, 寇岛, 枫华谷, 金水镇, 昆仑, 瞿塘峡, 巴陵, 南屏山, 龙门荒漠, 白龙口, 无量山, 融天岭,
  黑龙沼, 苍山洱海, 万花/七秀/少林/藏剑·乱世, 五台山, 千岛湖, 阴山大草原, 黑戈壁, 龙泉府, 经首道源岛,
  蔷薇列岛, 百溪, 烂柯山, 楚州, 晟江, 黑山林海, 银霜口, 河西瀚漠, 洛阳城北, 洞天福地岛, 长安·战乱.
- `bCanPK=1` + `bCanDuel=1` on all 45 (+ several others); e.g. 洛道 row 41.

### 1.4 Open-world camp-fight maps — `NewCampFight=1` (21 rows) — `maplist_modegroups_verified.tsv:41-61`
9 洛道, 13 金水镇, 21 巴陵县, 22 南屏山, 23 龙门荒漠, 25 浩气盟, 27 恶人谷, 30 昆仑, 35 瞿塘峡,
100 白龙口, 101 无量山, 103 融天岭, 104 黑龙沼, 105 苍山洱海, 139 枫华谷·战乱, 153 马嵬驿,
216 阴山大草原, 259 竞技大师_华山之巅, 325 洞天福地岛, 330 龙泉府, 656 阴山大草原_攻防分线.
Shared flags (distinct-value scan of all 21 rows): `Type=0, KeepTime=10, ReviveInSitu=1, RevieCycle=0,
BanSkillMask=0, BattleRelationMask=0, CampType=4 (15 rows) / 5 (4) / 1 (2), FightList=0,
bCanTongWar=1 (19), CanJoinBattleField=1, BanUseItemMask=16, CanJoinArena=1 (20), CanJoinTongBattlefield=0 (18),
InvalidBuffMask=0, BanChangeTalent=0, CanSprint=1 (20), QueueForSwitchWhenFull=1, CampQueue=1 (18),
bCanPK=1 (18), bCanDuel=1`. (25 浩气盟 / 27 恶人谷 have `MaxCopyCount=10, NeedCampBuff=1`;
259 竞技大师_华山之巅 and 656 攻防分线 are the `CanJoinArena=0` / `CanSprint=0` outliers.)

### 1.5 Queueability flags (HIGH)
- `CanJoinArena=1` on 84 maps (`maplist_modegroups_verified.tsv:107-190`) — open-world/arena-entry maps
  (万花, 少林, 扬州, 洛阳, 成都, 侠客岛, 拭剑园 276/278-281, …). The 11 real arena instances have
  `CanJoinArena=0` (entered via match, not direct queue).
- `CanJoinBattleField=1` on the same 84-row set (`:191-274`), plus BFs are joined through
  `JoinBattleFieldQueue`/`JoinTongBattleFieldQueue` (`JX3_MODE_LOAD_FLOW.md` §2).
- 名剑大会 staging: 276/278/279/280/281 拭剑园 (`CanJoinArena=1, IsArenaMap=0, BanSkillMask=16,
  BanUseItemMask=16`), 293 拭剑园战场 (`IsBattlefield` empty; `BanSkillMask=16`). (HIGH flags; MED purpose.)

### 1.6 Mode enum hints (MED)
`JX.dec.lua` (bytecode, game install) exposes `IsArenaMap`, `IsBattleFieldMap`, `IsHomelandMap`,
`IsMobaBFMap`, `IsTreasureBFMap`, `IsTongWar` (`proof/pvp/modes/interface_lua_pvp_ascii.txt:2,7,12`);
matching `Table_Is*BattleFieldMap` functions at `:8-11` confirm **MOBA / Treasure(寻宝) / Zombie**
battlefield sub-kinds. `CampType` values: 5 = battlefield/camp maps, 4 = camp-flagged open world,
1 = normal/open (`maplist_summary.txt:1847`; enum names not recovered — MED).

---

## 2. 名剑大会 / arena (竞技场)

**Maps, capacity, revive (HIGH).** 11 instances, see §1.1. Each copy allows 1024 players (queue/room
capacity, not team size), 2048 copies; `ReviveInSitu=0`, `RevieCycle=0` — no revive-cycle value
(unlike BG/BR, which use 160) ⇒ in-arena death does not use the field revive cycle (MED).
`KeepTime=2`, `OperationMask=7`, `QueueForSwitchWhenFull=1`.

**Bans.** `BanSkillMask=8` (bit 3). The verified set `skills_mapban_arena_bit8.tsv` holds **529 skills**
(lines 2-530); it contains all PvE legendary-weapon procs (橙武_*), dungeon weapon gimmicks
(荻花圣殿/烛龙殿/唐门 special weapons), PvE set bonuses, MOBA装备, 家园/挂件 (e.g. 龙门飞剑), and —
directly PvP-relevant — the **PVP gear passives** `输出PVP护手减化劲 (39829)`, `治疗PVP护手加化劲 (39830)`,
`PVP项链化劲加御劲 (39857)`, `PVP附魔1腰带_被击加御劲 (40728)`, `PVP附魔2腰带_化劲转御劲 (40729)`
(`skills_mapban_arena_bit8.tsv:394,395,401,429,430`). Bit 3 is also used by a few non-arena
"no-gimmick" maps (173 齐物阁, 181 狼影殿, 344 浮丘岛, 445 江南府邸, 467 幻灵境, 790 扬刀大会) —
so bit 3 is an arena/duel-class ban set (HIGH: values; MED: semantic).
`BanUseItemMask=1` on arena maps (HIGH).

**Food/flask/feast removal and class raid-buff removal (HIGH names).** `buff_arena_group.tsv`
(Buff.tab rows with 竞技场 in the name):
`竞技场_删辅助类食品(3369)`, `删增强类食品(3370)`, `删宴席(3371)`, `删辅助类药品(3372)`,
`删增强类药品(3373)` (`:16-20`); `删撼如雷(3387)`, `删清心静气(3388)`, `删秀气(3389)`,
`删雨霖铃(3390)`, `删般若诀(3391)`, `删药石气劲(3392)`, `删玄水(3913)` (`:35-40,52`);
`回蓝回豆(3849)`, `反隐形BUFF(5015)`, `清内功标记(3602)`, `标记战阶(3449)`.
`竞技场_战旗A(3381)` / `战旗B(3386)` (MapBanMask 57600) are flag/objective props.

**Healing decay ("对局火热").** Buff `30633 名剑大会·对局火热`: tooltip *"随着对局的进行，造成的最终治疗会逐渐降低"*
(`Case\Buff.txt`, offset 0x0024CFD1), attribute `atGamePlayTherapyFinalCof = -71` per row
(`Buff.tab` ID 30633). This is the arena healing-dampening mechanic (HIGH name/tooltip/attr; MED exact curve).
Robot/修正 variants: `竞技场能力修正 16654` (`atBeTherapyCoefficient -41/+31`, script
`Map/天山碎冰谷/skill/机器人减疗效果.lua`) and `竞技场55机器人减疗修正 17644` (`atBeTherapyCoefficient -51`)
— **5v5 confirmed** (HIGH for the buff name); no 2v2/3v3 string found (OPEN).

**Rules/flow (HIGH names, MED semantics).**
- Prep & talents: `竞技场准备时间_可奇穴切换(11839)`, `竞技场倒计时_无法切换奇穴(11840)`,
  `竞技场禁止换心法(11837)`, `竞技场准备阶段清cd(33311)`, `清除CD和BUFF` / `出场清CD` scripts
  (`skills_pvp_terms.tsv:16,17`), `进入竞技场标记(13479)`, `竞技场排队标记(11878)`,
  `记录竞技场房间号(15011)`, `记录竞技场房间类型(15088)`, `竞技场重连(32798)`,
  `竞技场重连3次惩罚(32799)` (`buff_pvp_unique.tsv`).
- Scoring: `竞技场_赛点标记(13120)`, `极速追击竞技场计分(28421)`, `竞技场最高分记录(23239/23240)`;
  rating cutoffs from legacy tooltips: *"名剑大会2200分以下结算分数翻倍"*, *"每场竞技结算时获得双倍积分（2000分以下）"*
  (`buff_tooltip_juejing_terms.txt:12,18,42`), weekly: `名剑大会每周五场胜利标记(17488)`,
  `参与名剑大会每周获胜的前10场…额外名剑币` (`:43`), `竞技场2胜拭剑宝箱` (Activity 754,
  `activity_pvp_rows.tsv:125`).
- Fair-play: `竞技场三层(11881)` is an inactivity counter, not rounds — tooltip *"当此效果叠加三层后…半时辰内无法参加名剑大会"*
  with `竞技场惩罚(11880)` (*"因名剑大会期间消极参战…无法排队"*) (`Case\Buff.txt` 0x0011262E).
- Arena is **recorded/replayable**: `KVideoReplayer::OnSyncArenaCompetitorList / OnSyncArenaCompetitorBaseData /
  OnSyncArenaCompetitorTalentData / OnSyncArenaCompetitorEquipBoxInfo / OnArenaVideoStart / OnMatchPoint`
  (`JX3ClientX64_exe_net_strings.txt` @0x008206D0+), `s2c_sync_arena_competitior_cd_state`; OB buffs
  `竞技场OB防被打(17678)`, `竞技场状态机无敌buff(13597)/减伤buff(13598)`.
- Dynamic arena environment: `Quest/竞技场障碍破坏/` scripts — 青竹书院房子倒塌击飞 (20069),
  乐山大佛窟落石 (20965/21016), 乐山柱子倒塌矩形AOE (21072), 拭剑台鼓撞击 (20971), plus
  `竞技场延时释放冲击波技能(14326)`, `竞技场延时设置血量10%(14390)`, `玩家被击溅射状态机(20181)`
  (`skills_pvp_terms.tsv:147,162,214-219`; `buff_pvp_unique.tsv:266`).
- Engine surface: `DoGetArenaRankRequest/OnGetArenaRankRespond`, `OnSyncArenaLevel`, `nDeltaArenaLevel`
  (`JX3ClientX64_exe_net_strings.txt`); Lua `GetArenaPlayerCount`, `GetArenaPlayer`, `bIsArenaMap`
  (`interface_lua_pvp_ascii.txt:4-6,2`).
- Cross-check `buff_arena_group.tsv` and `skills_mapban_arena_bit8.tsv`: **consistent** (both are cuts of the
  same Buff.tab/skills.tab sources; verification 0 mismatches).

---

## 3. 战场 (instanced battleground)

**Maps (HIGH):** see §1.2 (16 non-BR BF rows). Player counts are per-copy: 神农洇 34, 云湖天池 24,
珍珑棋谷 54, 丝绸之路 54, 三国古战场 34, 浮香丘 34, 李渡鬼域 100, 列星岛 14, 野狸岛 24, 羊村 5,
雪域关城 34, 扬刀大会 10, 大富翁 4, 鹅鸭大游园 10, 帮会联赛 210.
**Per-map flag deltas (HIGH, verified):**
- 39 四十级云湖天池: `BanSkillMask=256`; 52 七十级云湖天池: `BanSkillMask=0` (both `BanUseItemMask=16`,
  `InvalidBuffMask=256`); 38 神农洇/48/50/135/186: `BanSkillMask=0`, `BanUseItemMask=16`,
  `InvalidBuffMask=256`.
- 322 李渡鬼域: `BanSkillMask=2`, `MaxCopyCount=1024`, 100 players, zombie-BF (API `Table_IsZombieBattleFieldMap`).
- 412 列星岛: `BanSkillMask=512, BanUseItemMask=1, InvalidBuffMask=8`, 14 players — MOBA-style
  (`Table_IsMobaBattleFieldMap`, `JX_Moba.dec.lua`).
- 415 野狸岛: `BanSkillMask=3, RevieCycle=0, BattleRelationMask=1, BanUseItemMask=4`,
  `InvalidBuffMask=256`, `CanSprint=0`.
- 296/297/676/677: `BanSkillMask=512`, `BanUseItemMask=4`, `InvalidBuffMask=8` (treasure-BF API).
- 712 雪域关城: 34 players, `BanSkillMask=0, BanUseItemMask=16, InvalidBuffMask=256`.
- 790 扬刀大会: 10 players, `BanSkillMask=8, BanUseItemMask=1, InvalidBuffMask=8`.
- 689 帮会联赛: see §1.3.
Most non-BR BFs: `ReviveInSitu=0`, `RevieCycle=160` (415 野狸岛 and 790 扬刀大会 use 0),
`CampType=5` (807 欢乐谷 uses 1), `BattleRelationMask=2` (415 uses 1),
`BanChangeTalent=0`, `QueueForSwitchWhenFull=1`, `OperationMask=7`.

**Objectives & scoring — not in client tables (HIGH for absence).** No BF map carries a `ScriptFile`
(only 807 欢乐谷 does); the client exposes only the data model: `GetBattleFieldObjective`,
`GetBattleFieldPQInfo`, `GetBFRank*`, `ApplyBattleFieldStatistics`, `BattleFieldIncomePunish`
(`JX3_MODE_LOAD_FLOW.md:145`, `JX3_MODE_JUEJING_LOGIC.md:§6`). Objective-object buffs exist by name
(Buff.tab, `buff_pvp_unique.tsv`): `战场开旗苍云专用计时buff(31140)`, `战场站旗时长统计(19652)`,
`战场开旗背箱时间计时(25880)`, `战场开旗通用扣盾buff(25786)`, `战场站旗范围提示buff特效浩气蓝/恶人红/中立白(26193/26200/26201)`,
`战场守卫减伤防止刷伤害(30613)`, `战场位移处理(30634)`, `战场衰减Skillmove位移距离处理(30668)`,
`战场积极行为积分(25668)`, `战场结算标记(26077)`, `战场首胜buff(11962)`, `战场为帮会加分(7551)`,
`禁行·阵营战场(19584)`, `战场机器人能力修正(26041)`.
**Revive:** `ReviveInSitu=0` + `RevieCycle=160` (HIGH flags). Using the engine's frame unit (1/16 s,
`proof/pvp/decay_and_controls.tsv:5`), 160 ≈ **10 s** revive cycle (MED).
**Vehicles/mounts:** no BG vehicle skill set located in the client; the vehicle skill family
(`攻防神机车/神机台`, `MapBanMask=504`, 69 skills — `mapban_bits.txt:54-57`, `skills_mapban_bit504.tsv:2-70`)
belongs to **阵营攻防战** open-world maps (黑戈壁/蔷薇列岛/河西瀚漠), not instanced BG (HIGH values; MED scope).
**Rotation (HIGH):** Activity.tab schedules 战场 days (神农洇/九宫棋谷/云湖天池/西风古道/三国古战场/浮香丘/雪域关城)
from 12:00, duration 46 740 s (`activity_pvp_rows.tsv:2-8,12-17,21-23,37,60-61,165-167`).

---

## 4. 绝境战场 (BR / 沙漠风暴 / 吃鸡)

**Established (cite prior docs, don't redo):** maps & flags (`JX3_MODE_LOAD_FLOW.md` §1, `juejing_maplist_rows.txt`);
mode skill kit, cooldowns, loot/tier progression (`JX3_MODE_JUEJING_LOGIC.md` §1-§3);
queue/room/side/competitor sync and loot protocol (`JX3_MODE_JUEJING.md` §4, `JX3_MODE_LOAD_FLOW.md` §4/§8);
spawn rules absent locally (`JX3_MODE_SPAWN_RULES_SEARCH.md`).

**New combat/scoring/revive findings from the raw evidence (this workstream):**
- **绝脉 (execute/DP debuff).** `绝境_绝脉buff (29897)`: *"经脉受到损伤，侠士目标招式调息速度降低1%"*
  (`Case\Buff.txt` 0x0024252F). BR skill `绝境·截阳` applies it **3×** (`target.AddBuff(...,29897,1)` ×3,
  `scripts\skill\绝境战场\绝境·截阳.lua`, lines with `AddBuff … 29897`). `绝境绝脉爆炸伤害` sub-skill:
  40-unit radius, 256 angle, `nChannelInterval = 60 * dwSkillLevel`, base damage 20, calls
  `CALL_ADAPTIVE_DAMAGE;SKILL_NEUTRAL_DAMAGE;SKILL_NEUTRAL_DAMAGE_RAND`
  (`proof/pvp/cast/skill_cast_fields.tsv:1046`; script header + `tSkillData` verified).
  Related 段氏 (class) buff `29865` also exists (*"被引窍命中时引爆"*, 0x00242211).
- **狂怒 (low-HP/execute-adjacent damage).** `战意·狂怒 (27419)`, `绝境狂怒 (27421)`:
  *"所有伤害提升10%"*, with sibling text *"上限的90%以上时，额外提升10%所有伤害；不可叠加"*
  (`Case\Buff.txt` 0x0021F76D/0x0021F777).
- **Revive.** Two mechanisms:
  (a) 天原绝境 active revive — skill `天原绝境_冰封复活 (31507)`, script `沙漠风暴/天原主动复活.lua`
  (`skills.tab`; `mode_skills.txt`/prior doc);
  (b) 洱海绝境 camp revive — `洱海绝境可复活标记 (26073)`: *"拥有临时疗伤营地，重伤时不会失败结算，而是回到营地疗伤"*,
  `洱海绝境复活计时 (26074)` (*"疗伤中"*), scripts `沙漠风暴/洱海绝境复活.lua`,
  `洱海绝境复活buff消失.lua` (`buff_pvp_unique.tsv:521-522`, `Case\Buff.txt` 0x0020B6C3).
  BR maps keep `ReviveInSitu=0` + `RevieCycle=160` (10 s class value, MED).
- **Zone / endgame.** 洱海 南诏决斗: `洱海绝境死斗结束前等待 (26178)` (*"不受伤害，无法攻击"*),
  `洱海绝境死斗结束后不吃毒 (26179)` (*"胜者…不受洱海毒雾影响"*) (`Case\Buff.txt` 0x0020E0CA).
  `洱海绝境击杀叠伤害 (26378)` (`buff_pvp_unique.tsv:530`).
  乱武 (乱斗) mode: skills `竞技场_毒圈 (25104)` = `沙漠风暴/乱斗_毒圈.lua`,
  `竞技场_AOE拉进战斗 (25103)` = `乱斗_全场拉进战斗.lua`, `竞技场用轰炸/母技能 (25102/25388)` =
  `乱斗_轰炸圈*.lua` (`skills.tab` rows; grep 40). **These are the closest client-side evidence of
  BR shrinking-zone/forced-combat mechanics** (HIGH files/names; MED they are 乱武-only).
- **乱武 player buff.** `绝境战场·乱武 (31671)`: *"额外增加220000点气血上限，施展招式不受内力限制，
  部分轻功招式调息时间减少"* (`Case\Buff.txt` 0x0025E7AB).
- **Mode variants (Activity, HIGH):** `绝境战场` daily 12:00 windows (446/450), `绝境战场·乱武开关 (937)`
  + `绝境战场·乱武 (1028)`, `绝境战场·单人 (947)`, `绝境战场·普通吃鸡开关 (1039)`,
  `绝境战场·镖人 (839)` (`activity_pvp_rows.tsv:69-70,147,152,173,176,128`).
- **Mode control buffs:** `绝境战场禁用技能镇派 (51377)`, `绝境战场自动轮Buff (51275)`,
  `黑山绝境已经被揭示规则的个人记录buff (30141)` (`buff_pvp_unique.tsv:793,747,691`).
- **Map mechanics** (already catalogued in `JX3_MODE_JUEJING_LOGIC.md` §1): 天原 cold/warm zones
  (`天原绝境_寒冷(21181)/寒冷降低血上限(21182)/温暖区(21458)/谷风降温(21454)`), 滑翔伞/滑索,
  战象, 剑宗饮冰/武家饮血, `天原绝境_人数显示 (21428)`; 沧溟 etc.
- **Loot/scoring currency:** `飞沙令` (see §7).

---

## 5. 阵营 system (camps / 浩气盟 / 恶人谷)

**Camp relation matrix (HIGH, raw table).** `RelationCamp.tab` (3 data rows × 4 cols, header `ID 0 1 2`):

```
ID  0  1  2
0   1  0  0     (neutral/same)
1   0  1  -1    (camp 1 vs camp 2 = hostile)
2   0 -1  1     (camp 2 vs camp 1 = hostile)
```
Source: `...\pak_out4\RelationCamp.tab`. Camp identities: game text pairs `草上飞_浩气盟 (1160)` /
`草上飞_恶人谷 (1161)` and map IDs 25 浩气盟 / 27 恶人谷 (`NeedCampBuff=1`) give 浩气盟/恶人谷 (HIGH names;
MED mapping to ids 1/2 — `CampLevelParam.tab` calls them `GoodCamp*`/`EvilCamp*`).
`RelationForce.tab` exists in the source file list (`tab_paths.txt`) but **was not extracted** (OPEN).
Engine internals call camps **ForceID**: `MAX_PLAYER_FORCEID`, `LuaPlayerForceToPresentIndex`,
`GetCampPlayerCountPerMap` with `nGoodPlayerCount`/`nEvilPlayerCount`
(`JX3ClientX64_exe_net_strings.txt`).

**Camp level parameters (HIGH).** `CampLevelParam.tab` (13 data rows × 12 cols):
`Parameter/Level0..Level10`; `Score = 1500,1700,1900,2100,2300,2500,3000,3500,4500,6000,8000`;
`GoodCampReducePrestigeOnDeath = 0,0,0,512,1024,1024,1024,1024,1024,1024,1024`;
`EvilCampReducePrestigeOnDeath = 1024,1024,1024,1024,1024,512,0,0,0,0,0`;
all `*MoneyPercent/ReputePercent/PrestigePercent/DamagePercent/ShieldPercent = 1024` (=100%).
Interpretation: camp level L0-L10 by score; high-level players of each camp lose 威望 on death
(Good from ~L3/L4, Evil until L5) (MED).

**杀气 / 红名 / wanted (HIGH names).** `大唐监狱` map 152 (`BanSkillMask=64`), buffs
`大唐监狱_杀气 (5315)`, `大唐监狱_杀气01 (5427)`, `大唐监狱_屠戮/绝世/灭界 (5384/5386/5388)`,
`破魂杀气 (14895)`, `国际服监狱减杀气buff (22743)`, `国际服杀气太重走火入魔 (25429)`
(`buff_pvp_unique.tsv:72,73,74,75,76,286,477,508`). Engine: `KPlayerClient::OnSetPlayerRedName` (红名),
`DoWantedInfoQueryRequest`, `DoWantedPlayerPositionQueryRequest`, `OnSyncWantedPlayerInfo`,
`QueryWantedPlayerPosition`, `GetWantedPlayerPositions` (`JX3ClientX64_exe_net_strings.txt`).
`复活无威名 (1351)`: *"此时若被击败，对方无法获得阵营声望"* (`Case\Buff.txt` 0x000272EE) — anti-feed rule.

**帮战 / 攻防 (HIGH names, MED numbers).** `FirstAttackOnTongWar`, `DoApplyTongWarKillStatRequest`,
`OnApplyTongWarKillStatRespond`, `IsTongCastleWar` + `KTongDiplomacyCache` (`JX3ClientX64_exe_net_strings.txt`);
map 149 雪原争锋 `IsTongWarMap=1`; 45 `bCanTongWar` maps (see §1.3). 攻防 schedule: Activity
`阵营攻防战（周六/周日）706/707` 13:00 & 19:00 ×7200 s, 大攻防 queue locks 490-493, 逐鹿中原 225 (Tue/Thu 20:00),
拍卖 485/554/555 (`activity_pvp_rows.tsv:118-119,79-82,25,78,90-91`). Camp vehicles:
`攻防神机车/神机台` skills with `MapBanMask=504` (`skills_mapban_bit504.tsv:2-70`) and buffs
`攻防载具禁止回血 (9467)`, `黑戈壁禁止载具回血 (9785)`, `攻防神机雷监控 (9632)`,
`攻防空投/结算` (`buff_pvp_unique.tsv`). `战鼓 (2238)`: *"获得威名提高50%"*.
`阵营_刷战阶惩罚_伪英雄 (3313)`: *"该状态下您无法获得威名和战阶"* (`Case\Buff.txt` 0x00068EF5).

**IgnoreCamp (skills.tab col 79).** Distribution `{'0':30077, '':10841, '1':223}` — **223 skills**.
Cross-check of all `IgnoreCamp=1` rows against PvP names/`MapBanMask` finds only toys/cosmetics:
双人同骑, 烟花筒, 礼品道具, 赛马, 艺人表演/舞蹈, 雪球, 腰挂/背挂 effects, 帮会坐骑邀请
(`targeting_flags.txt:38-46`; full query in §0 tooling). **No arena/BG/BR combat skill uses
`IgnoreCamp=1`** — it means "target ignores camp" for social items (HIGH values; MED semantic).

---

## 6. PvP-specific combat rules

**减疗 / 被治疗效果 (healing-received reduction).** Attribute `atBeTherapyCoefficient`
("被治疗效果提高", 717 buff refs; negative = 减疗) and `atTherapyCoefficient` ("治疗效果提高")
(`proof/pvp/attr_catalog.tsv` rows `atBeTherapyCoefficient`, `atTherapyCoefficient`).
Examples: `百会_爆绝脉减疗 (29244)` — *"每层使受到治疗效果降低5%"*, `atBeTherapyCoefficient(-51)`
(`proof/pvp/attributes/buff_pvp_names.txt:175`; `buff_pvp_unique.tsv:676`); `楚济_减疗40% (9514)` —
tooltip *"受到疗伤效果降低45%"* (`Case\Buff.txt` 0x000E9387); `减疗免疫 (18038)` —
*"自身免疫疗伤成效降低效果"* (0x0018480B); generic `通用_减疗 (15661)`, `持续减疗效果 (18004)`,
`减疗减盾吸收 (24223)`, `盾击减疗实际效果 (10243)`, `剑纯实际减疗效果 (16664)`,
`延迟处理减疗添加 (16665)` (`buff_pvp_unique.tsv` IDs; tooltips in `ui_decay_terms.txt`).

**治疗衰减 (arena dampening).** §2: `名剑大会·对局火热 (30633)` — final healing decreases over the match.
(Also `JX3_MODE_JUEJING_LOGIC.md` notes there are no 缩圈/沙暴 constants in client tables.)

**化劲 / 御劲.** Attribute enums (HIGH): `atDecriticalDamagePowerBase` = 化劲等级提高 (161 buff refs),
`atDecriticalDamagePowerPercent` = 化劲等级修正值提高 (88); `atToughnessBase` = 御劲等级提高 (124),
`atToughnessPercent` = 御劲等级修正值提高 (20) (`proof/pvp/attr_catalog.tsv`). PvP gear/enchant skills
carry both and are arena-banned in PvE contexts: `输出PVP护手减化劲 (39829)`,
`治疗PVP护手加化劲 (39830)`, `PVP项链化劲加御劲 (39857)`, `PVP附魔1腰带_被击加御劲 (40728)`,
`PVP附魔2腰带_化劲转御劲 (40729)` (`skills_mapban_arena_bit8.tsv:394-430`). Buff-side PvP enchants:
`PVP附魔1护手战阶_*`, `PVP附魔1/2腰带战阶_*`, `PVP附魔1上衣/鞋子战阶_*` (buff IDs 30702-30720).

**免控 (CC immunity).** BR example: `绝境_笑醉狂.lua` removes every control category in one call —
`DelMultiGroupBuffByFunctionType(2,3,4,7,8,11)` then `AddBuff(51849)` (免控+减伤)
(`scripts\skill\绝境战场\绝境_笑醉狂.lua`). Category ids decoded (HIGH): 2 减速, 3 恐惧, 4 定身,
5 沉默, 7 锁足, 8 眩晕, 9 嘲讽, 11 击倒 (`proof/pvp/decay_and_controls.tsv:20-33`).
CC families and their function types are catalogued in `proof/pvp/buffs/cc_families.tsv`
(眩晕/击倒/定身/锁足/减速/沉默/缴械/恐惧/嘲讽/昏睡/冰环/击退/封轻功/混乱, with example buffs).

**递减 (diminishing returns / immunity).** `DecayType.tab` (10 rows, HIGH): 击晕 10 s, 冰环 10 s,
定身 10 s, 七星拱瑞 20 s, 雷霆震怒 20 s, 沉默 10 s, 眠蛊 10 s, 迷神钉 10 s, 短暂锁足 6 s, 恐惧 10 s;
`DecayFrame == ImmunityFrame` for every row (engine frame = 1/16 s; 160 = 10 s, 320 = 20 s, 96 = 6 s)
(`proof/pvp/decay_and_controls.tsv:9-18`, `proof/pvp/buffs/decay_types.tsv`). Interpretation: each
control application imposes a same-length immunity window of its DecayType (MED).

**战斗复活 / 复活次数.** Map-level flags (HIGH): arena `ReviveInSitu=0, RevieCycle=0`; BG/BR
`ReviveInSitu=0, RevieCycle=160`; open-world camp/攻防 maps `ReviveInSitu=1, RevieCycle=0`
(`maplist_modegroups_verified.tsv`). Player revive is a request/response family
(`DoPlayerReviveRequest`, `OnClientOtherPlayerRevive`/`OTHER_PLAYER_REVIVE`/`SYNC_PLAYER_REVIVE`,
`NpcReviveTimeReduce/IncreaseThreshold/Rate` in `JX3UIX64_net_strings.txt` / `JX3ClientX64_exe_net_strings.txt`).
BR specifics in §4. Per-map revive tables (`MapReviveList.tab`/`DoodadReviveList.tab`) are absent locally
(`JX3_MODE_GAP_REGISTER.md` §4; `JX3_MODE_SPAWN_RULES_SEARCH.md`).

---

## 7. PvP currencies / progression (brief)

Activity.tab carries **schedules and server-script hooks only** — no reward columns
(`activity_pvp_rows.tsv` header; e.g. `名剑大会 (570)`, `战场首胜 (265)`, `群雄竞技逐名剑 (266)`,
`名剑大会精英赛 (279/280/283)`, `武林争霸赛 (662)`, `攻防战利品拍卖 (485/554/555)`).
Currency facts come from Buff tooltips/attributes (HIGH strings, MED semantics):

| Currency | Evidence |
|---|---|
| **名剑币** (arena) | `buff 6472 偿还名剑币`, `6476 扣除名剑币道具`, `11924 JJC扣名剑币` (`atExecuteScript Map/天山碎冰谷/skill/扣名剑币.lua`); `7096 加速buff-名剑币10`; on-site buff *"名剑大会结算名剑币+10%"*; weekly *"参与名剑大会每周获胜的前10场…额外名剑币"*; *"每场竞技结算…双倍名剑币"* (`buff_tooltip_juejing_terms.txt:11,43,42`) |
| **威望/威名 (Prestige)** | `7094 加速buff-威望名剑币5` with `atAddPrestigePercentForAll=51`; *"战场结算威名+20%"*; `复活无威名 1351`; `战场领10000威望触发黑白路标记 19582`; engine `KPlayer::getCurrentPrestige`, `LuaGetPrestige*`, `LuaGetCurrency*` (`buff_pvp_unique.tsv`; exe strings) |
| **战阶** (campaign rank) | `2538/2539/2540 战阶防刷标记`, `7288 战阶_上周战阶积分`, `9629-9631 个人/其他/实时战阶`, `22601 战阶_上周战阶积分排名`, `3313 伪英雄`; *"战场结算团队额外获得200威名点50战阶"*; `7813 本帮据点周期性自动增长战阶` (Buff.tab names; `buff_tooltip_juejing_terms.txt:52`) |
| **竞技分** (arena rating) | `23239 竞技场最高分记录`, `28421 极速追击竞技场计分`, `OnSyncArenaLevel`/`nDeltaArenaLevel`, `DoGetArenaRankRequest`; cutoffs 2000/2200 in legacy tooltips (`buff_pvp_unique.tsv`; exe strings; `buff_tooltip_juejing_terms.txt:12,42`) |
| **飞沙令** (BR/鬼域/列星 weekly) | *"龙门绝境结算飞沙令碎片+10%"*, *"龙门绝境进入前十名获得飞沙令增加10%"*, *"绝境战场、李渡鬼域和列星虚境的周常奖励飞沙令碎片翻倍"*, `逍遥剑歌吃鸡展示buff` +5%/帮会成员 (`buff_tooltip_juejing_terms.txt:11,16,18,20,22`) |

No item/coin table (`Item.tab`/currency enum) was extracted from any local store — see OPEN.

---

## 8. Interface / UI Lua accessibility

**Answer: UI Lua is shipped but encrypted; only 6 files have decrypted bytecode copies. Not plaintext.**
- Game install `interface\` contains **309 `.lua` files**; plaintext read shows all start with
  `40 c9 6d 5a 57 b4 14 1f…` (encrypted container), e.g. `interface\JX\JX_0Base\JX.lua`,
  `JX\JX_Battle\JX_TongWar.lua`, `JX\JX_Buff\JX_Buff.lua` (HIGH, verified by magic bytes).
- **6 decrypted bytecode** files exist (magic `1b 4c 75 61 51` = Lua 5.1): `JX\JX_0Base\JX.dec.lua`,
  `JX.UI.dec.lua`, `JX.XGUI.dec.lua`, `JX\JX_Activity\JX_LootPlus.dec.lua`,
  `JX\JX_Activity\JX_Moba.dec.lua`, `JX\JX_ToolBox\JX_MiddleMapMark.dec.lua` (all under `interface\JX`).
- The previous agent mined these `.dec.lua` bytecode copies (ASCII strings only; no source recovered):
  `proof/pvp/modes/interface_lua_pvp_ascii.txt` shows `IsArenaMap`, `IsBattleFieldMap`,
  `IsMobaBFMap`, `IsTreasureBFMap`, `IsZombieBattleFieldMap`, `GetArenaPlayerCount`, `GetArenaPlayer`,
  `IsTongWar`, `Table_Is*BattleFieldMap`; `proof/pvp/modes/interface_lua_terms.txt` shows PvP UI text in
  `.ini`/`.dec.lua` (战场 type list in `JX.dec.lua` @0xAB06, 团队/战场 shout UI, JX_Activity info
  "绝境战场、列星虚境等辅助功能", JX_Battle "帮战辅助", JX_ToolBox "自动进入JJC/战场",
  "【剑心插件】将在10秒后进入名剑大会").
- The other plugin trees (`interface\LM`, `MY`, `SG#data`) keep **encrypted** `.lua` sources
  (e.g. `LM\LM_!Base\src.<timestamp>.lua`, ~930 KB each); `LM_Tip\src.*.lua` matches earlier evidence
  were only partial decodes (`interface_lua_terms.txt:11`).
- Game install `ui\` contains **no Lua** — only `Font/ H5Host/ Loading/ PublishVideo/ Video/`
  (+ `ui\Loading\Loading.ini` per prior docs); actual mode HUD data ships inside **PakV4**, not on disk
  (`JX3_MODE_GAP_REGISTER.md` §7).

---

## 9. Open items

1. **Arena team brackets/round format** — only "竞技场55机器人" proves 5v5; 2v2/3v3, round count and
   win conditions are not in any client table/string. (Rounds are NOT `竞技场三层`; that is an inactivity penalty.)
2. **Arena ban-mask semantic** — bit 3 (`BanSkillMask=8`) is shared by arena maps and a few duel/no-gimmick
   maps; per-map-class bit table is not shipped. (MED today.)
3. **BG objective/scoring/revive numbers** — server-only; client has `GetBattleFieldObjective` and
   objective buff names but no values.
4. **BR zone phase timings/shrink schedule** — only 乱武 poison/bombing skill scripts found; no
   shrink constants (consistent with `JX3_MODE_GAP_REGISTER.md` §4/§10).
5. **Camp enum table** — `RelationForce.tab` is in `tab_paths.txt` but not extracted; camp id↔浩气盟/恶人谷
   mapping is inferred from `GoodCamp*`/`EvilCamp*` + 草上飞 buffs.
6. **Item/currency tables** — no `Item.tab`/currency enum in PROBE or extracted paks; currency evidence is
   tooltip/attribute-level only.
7. **RevieCycle unit** — 160 mapped to ~10 s using the engine 1/16 s frame convention (from control tables);
   the revive path itself is server-authoritative.
8. **UI text assets** — mode HUD labels live in PakV4 `ui/`; enumeration attempted but not completed
   (`JX3_MODE_GAP_REGISTER.md` §7, recommend `KG_PAKFS_CollectAllFileNames`).

---

### New/changed files in this workstream
- **Written:** `proof/pvp/pvp_modes_rules.md` (this report).
- **Added evidence:** `proof/pvp/modes/verification_2026-09-24.txt` (field-level verification, 0 mismatches),
  `proof/pvp/modes/maplist_modegroups_verified.tsv` (exact flag groups by MapID).
- **Added tools:** `tools/pvp/verify_pvp_evidence.py`, `tools/pvp/dump_modegroups.py`.
- No existing raw evidence was modified or deleted; game install and `reborn-netcode` untouched.
