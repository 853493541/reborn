# Ability follow-up plan — what is still imprecise (and the next probe for each)

**Status:** active. Area: pvp / netcode / client. Created: 2026-10-09.
**Purpose:** an honest inventory of every ability-system item that is NOT yet precise, with its
current confidence, the exact unknown, and the concrete next probe/tool to close it.
Complements `ABILITY_CORRECTNESS_PLAN.md` (FWD1–FWD8) and `ENGINE_RE_BREAKTHROUGH_PLAN.md` (W1–W5).

Confidence: **HIGH** = verified from data/code; **MED** = partial evidence; **LOW** = assumption.

## A. Engine-RE items (root cause located, exact detail open)

| Item | Precise now (HIGH/MED) | Still imprecise | Next probe |
|---|---|---|---|
| W1 per-tani AV (39 abilities) | fault = `memcpy` size `0xFFFFFFFA` via `jemallocX64.dll` from engine anim code (`KG3DEngineDX11EX64.dll+0xE2E842` region); dump captured | exact engine caller fn; the corrupt map key/binding (which anim/bone id) | debugger (x64dbg/`cdb`) attach, or forced full-memory dump → `.pdata` unwind; then disasm the caller |
| W2 be-hit animation | rule = kind-selected from the target anim set (`[rsi+0x8c/0x94/0xa4]`, kinds 1/2/6) or model-sibling template `data/source/npc_source/%s/动作/%s_%s` | which table fills the target's be-hit anims; the kind↔field mapping; the `<suffix>` | disasm the target anim loader / the code that fills `[rsi+0x8c..0xa4]`; find the npc anim table |
| W3 hit sound | `Behit.bnk` found; `hit_target_sound` gives the SoundEvent name; `PostEvent(uint)` only | the SoundEvent-name → Wwise-id mapping (NOT plain FNV-1/-1a) | write a `.bnk` HIRC event-id reader, or disasm the client's PostEvent name→id wrapper |
| W4 landing `.Sfx` | engine `.Sfx` spawn faults (`rc=7`, `0xC0000005`); `.pss` works | the correct engine SFX-factory owner chain | disasm the engine tag-spawn caller (shim comment cites `0x76E51A`) |
| W5 be-hit shake | `behit_shake.txt` (RepresentID→count/offset/duration/type) exists | which column/global selects a skill's RepresentID | grep `skill_caster_f1`/`skills.tab` for a shake column; disasm the shake trigger |

## B. Ability mechanics still imprecise

- **Multi-hit / channel hit schedule (MED→LOW).** We apply damage/buffs once at commit. Channel
  skills (`nChannelFrame`/`nChannelInterval`) and multi-hit skills may apply N times on an
  authored schedule. Unknown: the hit count + per-hit timing. Next: parse the skill script
  (`nChannelInterval`, hit count) + `skill_chain.txt`.
- **Missiles / projectiles (LOW).** `skill_missile.txt`, `missile.txt`, `skill_bullet.krl.txt`
  exist; PointArea/projectile skills likely spawn a missile that travels and triggers on landing.
  We resolve instantly at the point. Unknown: flight + on-hit trigger + the missile model. Next:
  model missiles from `missile.txt`/`skill_missile.txt`.
- **Buff durations (LOW).** `Buff.tab` has no duration column; how a buff's duration is authored
  is unknown (skill script `AddBuff` args? a Buff level table?). Next: find the duration source.
- **Per-skill cooldown / GCD values (MED).** We currently override uniformly (3 s cd, 1.19 s GCD).
  The authored per-skill cooldown (row = 2nd arg of `SetNormalCoolDown`) + GCD rows exist but are
  not applied per ability. Unknown: which rows apply to which skill. Next: apply `cooldowns_f1`.
- **Skill marks/tags/chain/shadow (LOW).** `skill_tag.txt`, `skill_chain.txt`, `skill_shadow.txt`
  are unmodeled. Next: read each + wire the ones that affect combat (chain, tag).
- **Hit-stiff (LOW).** `skills.tab` cols 112–116 (`HitStiffDelayFrame`, `HitStiffSkillMoveID`,
  `HitStiffVelocityXY`, `HitStiffAccelerateXY`) unmodeled. Next: apply the target's hit-stiff move.
- **Damage scaling (blocked).** `nWeaponDamagePercent` + adaptive coefficient not applied — no
  player weapon/attack-power stats. Unknown: whether a base-attribute table exists locally. Next:
  search the client settings for a character base-attribute / attack-power table.
- **AoE shape (MED).** We use a radius (`nAreaRadius`×64); some skills use sector/rect/line.
  Unknown: the non-circular AoE shapes. Next: read the AoE geometry fields fully.

## C. Scripts

- **46 `EXECUTE_SCRIPT`-only abilities (LOW).** Logic not statically extractable → the Lua 5.1 VM
  residual. Next: build the VM (`tools/pvp/lua51_disasm.py` already parses bytecode) + engine-API shim.
- **32 `-` abilities (HIGH).** Confirmed no cast-time function; mostly template passives.
- **Complex scripts (loops/local helpers) (LOW).** The static parser may miss them. Next: the VM.

## D. Tooling gaps

- **No debugger** (`x64dbg`/`cdb`) in the environment → W1 blocked.
- **`minidump_exc.py` has no `.pdata` unwind**, and the WER dumps omit caller code pages.
- **No `.bnk` (Wwise bank) reader** → W3 blocked.
- **No missile/particle model loader** beyond `AddDummyModel` (`.pss` only).

## E. Verification / review

- 115 castables individually verified; **"correct" is ultimately the user's judgement** via the
  panel review (F1 已完成 / F2 测试中 / F3 需要修复). The fastest closing loop is the user's
  `需要修复` tags → I fix each. Currently no unaddressed `需要修复` tags beyond the defaults.

## Suggested order (highest leverage first)

1. **W1** (39 abilities) — needs a debugger; highest count.
2. **B: per-skill cooldown/GCD + multi-hit schedule** — code-only, no engine RE; affects many skills.
3. **W3 hit sound** — a bounded `.bnk` reader.
4. **B: missiles** — model projectile skills.
5. **W2/W4** — deeper engine RE.
6. **C: Lua VM** — for the 46 script-only abilities.
