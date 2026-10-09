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
  rows, prepare/channel frames, AoE geometry, knockback) → `mechanics_f1.tsv`.
- **P2** target/AoE resolution (castMode + `nAreaRadius`×64) with `RC_DUMMY_N` multi-target.
- **P3** effect runtime: damage (flat base+rand), CC (functionType), buffs add/remove, child
  casts, self-move (DASH family), **PULL** (to caster) and **knockback** (`nKnockedBackDis`).
  `SkillDamage` + `MechanicProgram`.
- **P4** resources/GCD/cooldowns: `build_cooldowns.py` (`CoolDownList.tab`), GCD row 16 = 1.5 s,
  per-skill cooldown (row = 2nd arg of `SetNormalCoolDown`), mana. Uniform **20 尺 cast range**
  (`RC_CAST_RANGE`) enforced with out-of-range feedback.
- **P5** feedback: layered ability panel (right, catalogs `测试`/`全部`, 29 px icons, green selected
  border, default open), combat-text overlay (status + events), cast bar, ability-bar/panel
  cooldown, target CC/buff.
- **Prepared / channel cast**: prepared skills play the **prepare anim** for `nPrepareFrames` then
  the **release anim** at commit (65076 1.5 s, 27874 3 s, …); channel skills hold for `nChannelFrame`.
- **P6/P6b** coverage: full 154 sweep (115 ok + 39 av); **all 115 castable abilities individually
  verified** (`proof/netcode/skillv6_batch*`); the 39 av are per-tani blacklisted.
- **Apply() path**: `build_passives.py` extracts each `Apply()` child casts + AddBuff/DelBuff ids
  + heal + summon + doodad → `apply_f1.tsv` (137 abilities); the client runs them **at cast**.

Open:
- `EXECUTE_SCRIPT` bodies beyond the static patterns (88 self + 5 external; the 32 `-` abilities
  are mostly **no-op/template passives**) → a **Lua 5.1 VM + engine-API shim** would only help the
  subset with real runtime logic (`proof/netcode/skillv6_residual_20261008.txt`).
- weapon% / attack-power damage scaling + mitigation (needs a character attribute model — the host
  player has no stats).
- world-projected floating numbers (no world→screen API); buff durations (`Buff.tab` has no
  duration column).
- **P7** server-spec reconciliation.

## P7 — server-spec reconciliation (2026-10-08)

`REBORN_PVP_BATTLE_SPEC.md` §7 is server-authoritative: the client sends cast intents with
predicted cooldown gating, predicts the cast bar/animation, and **never spawns damage numbers
without `OP_SKILL_EFFECT`** (no local damage/buff/CC application).

The v6 client ability runtime is a **local stand-in for the absent server**: it applies damage/
buff/CC/cooldown locally (a documented host deviation — no server exists). Alignment points for
when a server is built:
- keep: target/AoE resolution, cast bar prediction, cooldown gating (already client-side);
- move server-side: damage/buff/CC application and HP (replace the local `MechanicProgram`
  application with `OP_SKILL_EFFECT` handling; the same `mechanics_f1` program can drive the
  server's resolution so client and server agree);
- the local `selfBuffs`/target `Buffs`/`CcUntil` become prediction mirrors of `OP_CONTROL`/
  `OP_SKILL_EFFECT`.

Last verified: 2026-10-08.
