# reborn PvP battle system — implementation spec

**Branch:** `research/jx3-pvp-battle` · **Date:** 2026-09-24
**Basis:** `docs/pvp/JX3_PVP_BATTLE_RESEARCH.md` + the five evidence reports under
`proof/pvp/`. Every constraint marked **[JX3]** is verified from client data;
**[OURS]** marks design decisions; **[DEFAULT]** marks a chosen value where the
client data does not ship the answer (tracked in §10).

This spec is PvP-first but the combat core is shared with PvE.

---

## 1. Scope & goals

- Server-authoritative combat: cast arbitration, cooldowns, resources, buffs/CC,
  damage/heal resolution, death/revive, match scoring.
- Client predicts **animation and UI only** (JX3 has no rollback netcode [JX3]).
- Modes: arena (名剑大会-class), instanced battleground (战场-class),
  battle royale (绝境战场-class), open-world camp PvP (阵营-class).
- Data-driven: skills/buffs/cooldowns/map masks come from tables, not code.

Non-goals (this spec): matchmaking/rating algorithms, economy, PvE AI,
anti-cheat beyond server-side validation.

---

## 2. Authority model

| Concern | Owner | Notes |
|---|---|---|
| cast legality | server | client sends intent only [JX3] |
| cooldowns/charges/overdraft | server | client renders estimate, corrects on `OP_COOLDOWN` [JX3] |
| damage/heal numbers | server | only `OP_SKILL_EFFECT` changes HP [JX3] |
| buffs/stacks/ticks | server | client receives full + incremental sync [JX3] |
| CC + DR state | server | client ships only the DR/immunity windows [JX3] |
| movement/move-state | server | client move-state masks are per-skill data [JX3] |
| death/revive | server | revive request is a C→S op [JX3] |
| competitor/observer overlay | server | pull-on-demand, gated by `NeedSyncOB` [JX3] |

Client prediction allowed: cast bar, animation, SFX, cooldown sweep, hit prediction
visuals. Client **must not** apply damage or buff state locally.

---

## 3. Data model

### 3.1 Units & conventions [JX3]

| Unit | Value |
|---|---|
| frame | 1/16 s (`GAME_FPS=16`) |
| length | 1 尺 = 64 units = 0.64 m; 1 m = 100 units |
| percent | 1024 = 100 % (`PERCENT_BASE`) |
| rate | 100 = 1 % (for `*BaseRate`-style attrs) |
| angle | 256 = 360° (`nAngleRange × 1.40625 = degrees`) |
| cooldown table | seconds on disk, frames at runtime |

### 3.2 Required attributes

Implement the attribute families of the research doc §1.2 with the exact JX3 names
(they are the table vocabulary). Minimum PvP set:

- offence per element: `Physics`, `Solar`, `Neutral`, `Lunar`, `Poison`, `Magic`, `AllType`
  → attack power, `Overcome` (破防), `CriticalStrike`, `CriticalDamagePower`, `HitValue`
- defence per element: `Shield`/`MagicShield`, `MagicResistPercent`; global
  `GlobalResistPercent`, `DecayAllTypeResistPercent`
- avoidance: `Dodge`/`DodgeBaseRate`, `ParryBase`/`ParryValueBase`/`ParryPercent`
- PvP: `ToughnessBase`/`ToughnessPercent`/`ToughnessBaseRate` (御劲),
  `DecriticalDamagePowerBase`/`Percent`/`BaseKiloNumRate` (化劲),
  `PVXAllRound`, `AllShieldIgnoreAdd`/`Percent`, `GlobalDamageFixedAdd`
- therapy: `TherapyPowerBase`, `TherapyCoefficient`, `BeTherapyCoefficient`
- utility: `HasteBase`, `MoveSpeedPercent`

Rating→percent conversion coefficients are known (`GlobalParam.lua`, research §1.3).
**The equation itself is not shipped** — see §10.1 for the default formula.

### 3.3 Damage types [JX3]

`Physics, SolarMagic, NeutralMagic, LunarMagic, Poison, Leap, None, Adaptive, Any`;
adaptive resolves to ordinal 1..5 = Physics/Solar/Neutral/Lunar/Poison.
Per-element defence fields are in `proof/pvp/attributes/damage_type_map.tsv`.

### 3.4 Damage pipeline (order is ours; layers are JX3)

```
1 hit check        : attacker HitValue/BaseRate vs defender dodge/parry/set-hit-reduce
2 avoidance        : dodge -> parry roll
3 shield           : shield rating -> % (coefficients 6.364), countered by Overcome (11.412)
                     and AllShieldIgnoreAdd/Percent
4 flat reduction   : GlobalResistPercent + per-element MagicResistPercent,
                     reduced by DecayAllTypeResistPercent
5 PvP layer        : 化劲 DecriticalDamagePower (PvP coef 0.9), 御劲 Toughness
                     (crit-chance reduction; crit-damage coef 2.784)
6 block/mirror/mana-shield
7 absorb shields   : damage absorb, therapy absorb, rechargeable absorb
8 reflection
9 final modifiers  : GamePlayDamageFinalCof, GlobalDamageFixedAdd, caps
```

Damage composition [JX3]: `flat per-level base/rand` + `weapon%` (`nWeaponDamagePercent`,
1024=100 %) + `attack-power%` / adaptive coefficient. Skill tables may override via
`SkillCoefficient`/`DotCoefficient`/`SurplusCoefficient`.

### 3.5 Buff model [JX3]

| Concept | Implementation |
|---|---|
| slot/merge | `AppendType` (default = own id); same slot merges/replaces unless `Coexist` |
| dispel groups | `DetachType`: 3/5/7/11 = elemental buffs, 4/6/8/10/12 = elemental debuffs, 10 = control debuffs |
| dispel ops | strip by group (`DETACH_MULTI_GROUP_BUFF`) or by control category (`DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE`) |
| stacking | `IsStackable`, `MaxStackNum` (≤255), `IsIntensityStackable` |
| ticking | `Count` × `Interval` frames (DOT/HOT), `MinInterval`/`MaxInterval` |
| steal/transfer | `CanBeSteal`, `CanTransfer` |
| persistence | `Save`, `IsCombatBuff`, `NeedSync` |
| mode gating | `MapInvalidMask` ∩ map `InvalidBuffMask` |

Buff application must be scriptable (JX3 uses Lua skill scripts); reborn can keep the
same event hooks: on-begin attributes, on-tick, on-end attributes, `atImmunity(n)`,
`atKnockedBackRate/RepulsedRate/PullRate`, `atFreeze`, `atGlobalBlock`,
`atNegativeShield`, `atBeTherapyCoefficient`.

### 3.6 Control & DR [JX3]

Control categories: `2 slow, 3 fear, 4 root(定身), 5 silence, 6 雷霆, 7 锁足,
8 stun, 9 taunt, 11 knockdown`. Displacement (knockback/pull) is rate-based, not a category.

`DecayType` table (windows in frames, 1/16 s; Decay == Immunity in this build):

| id | 0 击晕 | 1 冰环 | 2 定身 | 3 七星拱瑞 | 4 雷霆震怒 | 5 沉默 | 6 眠蛊 | 7 迷神钉 | 8 短暂锁足 | 9 恐惧 |
|---|---|---|---|---|---|---|---|---|---|---|
| frames | 160 | 160 | 160 | 320 | 320 | 160 | 160 | 160 | 96 | 160 |
| seconds | 10 | 10 | 10 | 20 | 20 | 10 | 10 | 10 | 6 | 10 |

Server keeps per-entity DR state `{decay_type -> {stacks, immunity_until}}`.
Ladder and reset are **[DEFAULT]** (§10.2).

### 3.7 Casting / cooldowns / resources [JX3]

- Cast fields: `nPrepareFrames`, `nMinPrepareFrames`, `nChannelFrame`, `nChannelInterval`,
  `nMinChannelFrame`, `nMinChannelInterval`, `bInstantChannel`, `bIgnorePrepareState`.
- GCD = a cooldown row (`SetPublicCoolDown(id)`); default row 16 = **1.5 s**,
  alternate classes per school/vehicle (1.0 s 明教, 2.0 s 雕系, 0.5 s 隐刀…).
  Skills without `SetPublicCoolDown` have no GCD.
- Cooldown rows: `Duration`, `MinDuration`, `MaxDuration` (haste clamp), `MaxCount`
  (charges), `MaxOverDraftCount` (透支), `CanBackup`, `CanAccelerate`, `NeedSyncOB`.
- Resources: mana + school resource (剑气/墨意/战意/禅那/怒气/刀魂/星运/破绽/神机值/
  日灵/月魂/气点…), stamina, sprint power.
- Talents/recipes modify base skills via additive deltas (cast frames, CD adds, radius,
  cost, damage %) and runtime buff hooks (`atSetTalentRecipe`, `atClearCoolDown`).

### 3.8 Cast validation pipeline (server, ordered)

```
0 alive, not in a blocking state (death/horse/vehicle/move-state masks)
1 mode gating      : skill.MapBanMask & map.BanSkillMask == 0
2 cooldown/GCD     : public CD + normal CD + charges/overdraft
3 resource         : mana/school resource/stamina costs
4 caster state     : silence/control unless IgnoreSilence/IgnoreControl;
                     channel/prepare interruptibility
5 target legality  : TargetTypePlayer/Npc, relation mask, IgnoreCamp, stealth check
6 geometry         : range [min,max], angle (sector), LOS (Use3DObstacle),
                     path (CheckReachable), IgnoreRangeBlock for charges
7 immunity         : target control immunity, IgnoreImmunityCast,
                     shield bypass flags
```

Rejected casts return a reason code (`OP_CAST_RESULT.reason`, §5.2) and never consume
resources/cooldowns.

---

## 4. PvP mode configuration

All modes are config rows over the same combat core [JX3]:

| Mode | Category bit | Key flags | Bans |
|---|---|---|---|
| arena | 8 | `ReviveInSitu=0`, `RevieCycle=0`, `BanUseItemMask=1`, `OperationMask=7` | skills bit 8 (PvE procs, PvP gear passives), food/flask/feast removal, talent lock during countdown, kungfu-switch lock |
| battleground | 256 | `ReviveInSitu=0`, `RevieCycle=160` (≈10 s), `CampType=5` | per-map `BanSkillMask` |
| battle royale | 512 | `BanChangeTalent=1`, `ReviveInSitu=0`, `RevieCycle=160`, item bans | skills bit 512 (revive/meditate), BR-specific buff variants |
| camp/open world | 16 (city/siege) | `ReviveInSitu=1`, PK/duel flags, camp relations | siege vehicle skills bit 16 |
| 帮会 war/league | — | `IsTongWarMap`, `IsTongLeagueMap`, up to 210 players | per-map |

Rules to implement:

- **mask intersection** for both skills and buffs (research §6.4);
- arena healing dampening: ramping final-healing coefficient over match time
  (`名剑大会·对局火热` analogue) [JX3 mechanic, curve **[DEFAULT]** §10.4];
- revive: arena has no field revive cycle; BG/BR use a 10 s cycle; open-world
  revive-in-situ [JX3];
- anti-idle/leave penalties and reconnect flags (arena 3-strike analogue) [JX3 names];
- competitor sync: only `NeedSyncOB` cooldowns + buff list + base/variable stats,
  pull-on-demand (research §7) [JX3];
- camp relations: hostile (−1) vs neutral (0), kill/prestige rules [JX3].

---

## 5. Wire protocol (ours)

Frame layout/transport: `docs/netcode/REBORN_SERVER_SPEC.md`. IDs are ours; payloads
little-endian. `[PROPOSAL]` from `proof/pvp/combat_netcode.md` §7, refined here.

### 5.1 C→S

| ID | Name | Payload |
|---|---|---|
| `0x0040` | `OP_CAST_INTENT` | `u32 seq, u32 skill_id, u64 target_id, f32 aim[3], u8 flags, u32 client_tick` |
| `0x0041` | `OP_CD_QUERY` | `u32 cd_id` |
| `0x0042` | `OP_COMPETITOR_SUBSCRIBE` | `u64 player_id, u8 want (0 cancel, 1 buffs, 2 cd, 3 stats)` |
| `0x0043` | `OP_REVIVE_REQUEST` | `u8 kind (in-situ/point/npc), u32 point_id` |
| `0x0044` | `OP_TARGET_INTENT` | `u64 target_id` (ground-target aim) |
| `0x0045` | `OP_INTERACT_REQUEST` | `u64 doodad_id, u32 option` (objective/flags) |

### 5.2 S→C

| ID | Name | Payload |
|---|---|---|
| `0x0050` | `OP_CAST_RESULT` | `u32 seq, u8 accepted, u8 reason` (0 ok, 1 dead, 2 move_state, 3 range, 4 angle, 5 los, 6 resource, 7 cooldown, 8 silent, 9 control, 10 immune, 11 relation, 12 stealth, 13 busy, 14 mode_banned) |
| `0x0051` | `OP_SKILL_PREPARE` | `u64 caster, u32 skill, u8 level, u32 cast_ms, u8 kind, u32 seq` |
| `0x0052` | `OP_SKILL_CAST` | `u64 caster, u32 skill, u8 level, u32 seq, u8 has_aim, f32 aim[3]?` |
| `0x0053` | `OP_SKILL_CHANNEL` | `u64 caster, u32 skill, u8 level, u16 tick_index, u16 tick_ms, u32 total_ms` |
| `0x0054` | `OP_SKILL_EFFECT` | `u64 caster, u32 skill, u8 count, then per result: u64 target, u8 hit_type, u8 crit, u8 result, i32 amount, u32 absorbed, u32 shield_left, u8 flags` |
| `0x0055` | `OP_SKILL_MOTION` | `u64 caster, u32 skill, u8 motion (1 beat_back, 2 ray, 3 chain, 4 point_chain), u16 count, u32 nodes[count]` |
| `0x0056` | `OP_SKILL_HOARD` | `u64 caster, u32 skill, u8 phase (start/tick/cast/end), u32 value, u32 max` |
| `0x0057` | `OP_SKILL_INTERRUPT` | `u64 caster, u32 skill, u8 reason (beat_back, control, death, range_lost, moved)` |
| `0x0058` | `OP_COOLDOWN` | `u32 cd_id, u32 remaining_ms, u8 kind (set, reset_all, pause, resume, accelerate, overdraft, charge), u32 value` |
| `0x0059` | `OP_BUFF_SYNC` | `u8 mode (full/add/remove/stack), u64 entity, u16 count, records: u32 buff_id, u16 level, u32 remainder_ms, u32 caster, u32 stacks, u32 flags` |
| `0x005A` | `OP_CONTROL` | `u64 entity, u32 control_id, u8 kind (apply/immune/break/refresh), u8 category, u32 duration_ms` |
| `0x005B` | `OP_DEATH` | `u64 victim, u64 killer, u8 death_kind, u32 respawn_ms` |
| `0x005C` | `OP_REVIVE` | `u64 player, f32 pos[3], u32 hp, u8 kind` |
| `0x005D` | `OP_COMPETITOR_SYNC` | `u8 kind (base/variable/buffs/cd/stats), u64 player, u16 count, records…` |
| `0x005E` | `OP_MATCH_STATE` | `u8 phase (queue/prep/countdown/live/end), u32 ends_in_ms, u8 score[2]` (mode overlay) |

Rules:

- `OP_SKILL_EFFECT` is the only HP-changing event.
- `seq` correlates `OP_CAST_RESULT` with the resulting skill events (dedupe on reconnect).
- Channels end via `OP_SKILL_INTERRUPT`; server re-validates range/LOS each tick.
- Competitor sync is rate-limited; never applied to the local player's own cooldowns.
- Full buff sync on join/resume; incremental afterwards.

---

## 6. Server loop rules

1. **Tick 16 Hz** for combat timers (frames), higher for movement as per netcode spec.
2. On `OP_CAST_INTENT`: run §3.8; on success start prepare/channel timer, consume
   resource at cast commit, set cooldown/GCD at commit (not intent).
3. DoT/HoT ticks at `Interval` frames for `Count` ticks, snapshotting caster stats at
   apply time (JX3 buffs store values at begin/active/end — mirror that).
4. DR: on control apply, consult per-entity DR state; apply duration multiplier and
   start/refresh immunity for the DecayType window.
5. Damage events: resolve pipeline §3.4, clamp, emit one `OP_SKILL_EFFECT` per skill
   result batch (mirrors JX3's single result message).
6. Death: stop timers, clear combat state per mode, emit `OP_DEATH`; revive per mode.
7. Persistence: only `Save=1` buffs survive disconnect; reconnect restores full
   buff/cooldown snapshot (JX3 competitor/self sync shapes).

---

## 7. Client responsibilities

- Send cast intents with predicted cooldown gating (soft); correct on `OP_CAST_RESULT`.
- Predict cast bar/animation; never spawn damage numbers without `OP_SKILL_EFFECT`.
- Render cooldowns from `OP_COOLDOWN` server clocks (pause/accelerate/overdraft).
- Render DR/immunity from `OP_CONTROL`; keep a local DR display per DecayType.
- Interpolate remote entities; no rollback.

---

## 8. Conformance checklist (against JX3 evidence)

- [ ] 1.5 s default GCD with alternate GCD classes; skills can opt out
- [ ] 1024=100 % / 100=1 % unit conventions in all attribute math
- [ ] 5 damage elements + adaptive ordinal mapping
- [ ] 化劲/御劲 present in the PvP mitigation layer, with PvP coefficient 0.9
- [ ] control categories 2/3/4/5/7/8/9/11 + DecayType windows (10/20/6 s)
- [ ] 解控/免控 sets match per-skill tables (击退/被拉 exceptions)
- [ ] cooldown charges, overdraft, haste clamp, NeedSyncOB gating
- [ ] mask intersection for skill/buff mode bans
- [ ] arena dampening, BG 10 s revive cycle, BR no-revive-cycle rules
- [ ] competitor sync pull-on-demand + observer gating

---

## 9. Test plan

| Test | Method |
|---|---|
| unit conventions | property tests: buff value round-trip vs UI formula (research §1.1) |
| cast validation | table-driven: for each gate, a case with expected reason code |
| DR | scripted CC chain: verify duration multipliers + immunity window (10/20/6 s) |
| cooldowns | charge/overdraft sequences vs CoolDownList samples |
| masks | load MapList + skills/buffs, assert banned sets match JX3 tables |
| damage pipeline | golden tests from skill scripts (base/weapon%/AP%) with fixed stats |
| mode rules | arena/BG/BR flag fixtures from `maplist_modegroups_verified.tsv` |

---

## 10. Open decisions & defaults

| # | Missing JX3 fact | Default to ship | Revisit |
|---|---|---|---|
| 1 | rating→% equation | `pct = rating / (coef × (level_term))`, level_term=1, calibrate per stat to hit known tooltips (e.g. 3000 rate = 30 % crit reduction) | after `GetAttributeValue` disasm |
| 2 | DR ladder/reset | `100/50/25 %` duration, immune after 3rd; reset 30 s out of combat or on death | capture/reverse |
| 3 | MoveStateMask semantics | treat as whitelist (buff valid only in listed states) | disasm `KBuffList::CheckValidity` |
| 4 | arena dampening curve | linear 100 %→50 % healing over 10 min | capture |
| 5 | BG objective/scoring | score per objective tick, 10 s revive | server design |
| 6 | BR zone schedule | config-driven phases, damage ramp per phase | server design |
| 7 | `CanBackup`/`CastMask`/`Usage` | CanBackup = persist across stance/defeat; CastMask unused | disasm |
| 8 | S2C IDs / KSKILL_RESULT fields | use our opcode set §5 | capture |
| 9 | camp id mapping | 1=浩气盟, 2=恶人谷, 0=neutral | extract `RelationForce.tab` |
