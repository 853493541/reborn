# Ability Mechanics Runtime — implementation plan (v6 client)

Goal: the client executes each ability's **authored mechanics**, not just its
animation — correct targets/AoE, damage/heal, buffs/debuffs, CC, movement, child
skills, costs, GCD/cooldowns — driven only by the client's own tables and scripts.
`绝境·龙牙` (65029) is the reference: target → dash → authored anim → effect → damage.

Evidence base: `docs/pvp/JX3_PVP_BATTLE_RESEARCH.md` (synthesis),
`docs/pvp/REBORN_PVP_BATTLE_SPEC.md` §3.7 (cast/GCD), `proof/pvp/attributes_and_damage.md`
(damage/mitigation), `proof/pvp/buff_control_system.md` (buffs/CC/DR),
`proof/pvp/cast_cooldown_resources.md` (prepare/channel/GCD/resources),
`docs/pvp/TARGET_DUMMY_RESEARCH.md`, `docs/netcode/SKILL_DATA_RESEARCH.md`.

## What the roster is (154 abilities, `ability_picker/data/roster_f1.tsv`)
- `functionType`: Normal 81, Damage 61, Fly 4, Stun 4, Silence 1, Charm 1, Halt 1, Daze 1.
- `kind`: Adaptive 71, Physics 28, None 22, Leap 11, NeutralMagic 7, SolarMagic 6, LunarMagic 5, Poison 4.
- `castMode`: TargetSingle 70, CasterSingle 67, PointArea 8, CasterArea 7, TargetArea 2.
- Script attribute taxonomy (over 1173 scripts): `EXECUTE_SCRIPT` 437,
  `SKILL_*_DAMAGE(_RAND)` (Physics/Neutral/Solar/Lunar/Poison/Adaptive), `CALL_*_DAMAGE`,
  `CALL_BUFF`/`DETACH_*`/`DEL_*`, `CAST_SKILL(+_TARGET_DST/SRC)`,
  `SKILL_MOVE`/`DASH`/`PULL`/`CALL_KNOCKED_OFF_PARABOLA`/`CALL_REPULSED`, `ACCUMULATE`,
  `SET_TALENT_RECIPE`, `CLEAR_COOL_DOWN`, `PHYSICS_ATTACK_POWER_PERCENT`.

## Model (Phase 0)
- **Ability record**: castMode, functionType, kind, range, AoE shape, cost, cooldown/GCD
  rows, coefficients (skills.tab), and a **mechanic program** = ordered effect ops.
- **Op** = `{mode, ATTRIBUTE_TYPE, arg0..}`; the runtime interprets each type
  (damage / buff / detach / move / child-cast / accumulate / execute-script).
- **Combat state**: self + target(s) with attributes, HP, resource, buff list (with
  stacks/duration/DR), CC state (MoveState).

## Phases
- **P0 Model/taxonomy freeze** — this doc + `client/` data structures.
- **P1 Mechanics extraction (DONE — `build_mechanics.py`)** — per-ability level table
  (cost/damage/rand), `AddAttribute` program, weapon %, cooldown rows, prepare/channel
  frames → `ability_picker/data/mechanics_f1.tsv` (146 text + 0 bytecode + 8 missing of 154).
- **P2 Target/AoE resolution** — castMode targeting (TargetSingle/CasterSingle/PointArea/
  TargetArea/CasterArea, Alt-directional), range/LOS, AoE gather → affected-target list at commit.
- **P3 Effect execution runtime (core)** — damage pipeline (`attributes_and_damage.md`:
  coefficient×level, attack/weapon scaling, kind, crit, mitigation vs dummy defense → HP);
  buffs/debuffs (Buff.tab; apply/refresh/detach; HoT/DoT ticks); CC (functionType + buff
  MoveState); movement (leap/pull/knockback/repel/parabola); child-skill scheduling; ACCUMULATE.
- **P4 Resources/GCD/cooldowns/charges** — cost at commit, cooldown rows + charges, GCD row 16
  (1.5 s), interrupt-on-move, channel ticks (`nChannelInterval`).
- **P5 Feedback/UI** — cast bar (prepare/channel), cooldown sweep on panel icons, floating
  combat text (damage/heal), hit reactions, buff/debuff/CC icons on the target frame.
- **P6 Verification** — deterministic harness casts each of the 154 vs the dummy; compare
  observed (HP delta, buff set, CC state, movement vector, child casts) to the authored data;
  coverage matrix by functionType; golden tests (龙牙 damage, 云飞玉皇 prepare, a channel, a CC, a leap).
- **P7 Server-spec reconciliation** — align with `REBORN_PVP_BATTLE_SPEC.md` opcodes so a future
  server agrees (no packet capture).

## Dependencies / risks
- P0→P1→P2→P3→P4→P5→P6; P7 parallel.
- Risks: 39 per-tani AVing tanis (`ability_picker/data/av_blacklist_f1.txt`); 8 roster ids with
  no script; damage-formula unknowns; no server (client-authoritative prediction only).

## Reproduce
```
.venv\Scripts\python.exe ability_picker\tools\build_mechanics.py
# 65029 绝境·龙牙 -> cost=36 dmg=113.42 weaponPct=1024 gcd=16 prepare=0
#   ops: CAST_SKILL_TARGET_DST(65030); SKILL_PHYSICS_DAMAGE; SKILL_PHYSICS_DAMAGE_RAND; CALL_ADAPTIVE_DAMAGE
```

## Status (2026-10-08)

Done (v6 client, branch `agent/skillv6-sandbox`):
- **P0/P1** model + `build_mechanics.py` (level table, AddAttribute program, weapon%, cooldown
  rows, prepare/channel frames, AoE geometry) → `mechanics_f1.tsv`.
- **P2** target/AoE resolution (castMode + `nAreaRadius`×64) with `RC_DUMMY_N` multi-target.
- **P3** effect runtime: damage (flat base+rand), CC (functionType), buffs add/remove, child
  casts, self-move (DASH family). `SkillDamage` + `MechanicProgram`.
- **P4** resources/GCD/cooldowns: `build_cooldowns.py` (`CoolDownList.tab`), GCD row 16 = 1.5 s,
  per-skill cooldown, mana.
- **P5** feedback: combat-text overlay (status + events), cast bar (prepare/channel), ability-bar
  cooldown sweep, target CC/buff in the status line.
- **P6/P6b** coverage: full 154 sweep (115 ok + 39 av) → `coverage_mechanics_f1.tsv`; the 39 are
  passives (`function Apply`) → `script_only_f1.tsv`.
- **Passive path**: `build_passives.py` extracts each `Apply()` child casts + AddBuff/DelBuff ids
  → `passives_f1.tsv`; the client applies self-buffs and child casts.

Open:
- `EXECUTE_SCRIPT` bodies beyond the static patterns (69 abilities touch them; 39 are passives
  whose Apply is partly parsed) → a **Lua 5.1 VM + engine-API shim** is the remaining path.
- weapon% / attack-power scaling + mitigation (needs a character attribute model).
- target-displacement units (PULL/KNOCKED_BACK_RATE); other Apply effects (heal/summon/doodad).
- world-projected floating numbers; buff durations (Buff.tab has no duration column).
- **P7** server-spec reconciliation.

Last verified: 2026-10-08.
