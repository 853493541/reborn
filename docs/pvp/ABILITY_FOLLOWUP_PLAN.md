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
| W2 be-hit animation **DONE (2026-10-09)** | kind map (`BeHittedByNpc` @ `0x1804cfb50`: kind 1→`[rsi+0xa4]`, 2→`[rsi+0x94]`, 6→`[rsi+0x8c]`) + default NPC rule (`BeHittedByPlayer` @ `0x1804d0220`: literal `bat01.ani`; sibling `0x1804d8700` = `data/source/npc_source/%s/动作/%s_%s`) | — | implemented: target's `_bat01` sibling of its idle anim, played + restored; `npc_animation.txt` is runtime-generated (not shipped) |
| W3 hit sound | ids are **authored, not hashed** (adjacent index events get consecutive ids; FLWS name→FNV ≠ id); client `ProcessSkillEffectSound` @`0x18059fdc0` hands `pcHitTargetSoundModel->szEvent` (name) to the Wwise manager; `sound_probe` already hooks the `PostEvent(const char*)` overload | the authored hit-target names (`TianCe_Body_L01`, `l_LongYaBeiJi`, `f2sgb11BangFaGongJi07`) are absent from all 230 indexed banks + `resource_sfx`/`custom_sfx`/`WwiseSound`; which bank carries them is unknown | find/load the bank with those exact strings, then by-name `PostEvent` (see `ENGINE_RE_BREAKTHROUGH_PLAN.md` W3 for detail); do NOT guess an id |
| W4 landing `.Sfx` | engine `.Sfx` spawn faults (`rc=7`, `0xC0000005`); `.pss` works | the correct engine SFX-factory owner chain | disasm the engine tag-spawn caller (shim comment cites `0x76E51A`) |
| W5 be-hit shake | `behit_shake.txt` (RepresentID→count/offset/duration/type) exists | which column/global selects a skill's RepresentID | grep `skill_caster_f1`/`skills.tab` for a shake column; disasm the shake trigger |

## B. Ability mechanics still imprecise

- **Multi-hit / channel hit schedule. CHANNEL DONE (2026-10-09).** Channel skills now apply the
  mechanic every `channelInterval` during the channel (27906 -> 10 applies over 4 s, was 1).
  Still open: non-channel multi-hit skills' authored hit count/timing. Next: parse the skill
  script hit count + `skill_chain.txt`.
- **Missiles / projectiles (LOW).** `skill_missile.txt`, `missile.txt`, `skill_bullet.krl.txt`
  exist; PointArea/projectile skills likely spawn a missile that travels and triggers on landing.
  We resolve instantly at the point. Unknown: flight + on-hit trigger + the missile model. Next:
  model missiles from `missile.txt`/`skill_missile.txt`.
- **Buff durations — SOURCE LOCATED (2026-10-09); unit unconfirmed.** The duration is the **5th
  positional arg of `AddBuff`** in the skill script (`AddBuff(ownerID, level, buffID, stack,
  nBuffTime)`; scripts name it `nBuffTime`, e.g. `AddBuff(npc.dwID, npc.nLevel, 20346, 1,
  nBuffTime)`). Literal values across the extracted scripts: {0,1,2,3,4,5,6,7,8,10,12,13,14,15,
  18,20,21,25,30,40,49,60,100,120,600,7200}; 600 (×22) / 120 / 60 look like round seconds if the
  unit is 1/60 s (10 s / 2 s / 1 s) but the CC `Intensity` column only reads as seconds at 1/16 s
  (445 silence 48→3 s), so the two units conflict. Disasm (`JX3ClientX64.exe`, read-only):
  `KBuffList::AddBuff` @`0x140309690` only copies the buff struct (no time scaling);
  `KScriptFuncList::LuaGetBuffTime` @`0x1401c0730` reads the buff's fields `[+0x3c]` and `[+0x40]`
  and returns **(count×interval, count, interval)** — so a buff stores a count and an interval
  (frames), and the duration is their product; no unit conversion appears at add/get time. The
  global `[..+0xe80]` pushed as a number by the sibling fn `0x1401c0860` is a candidate frame/time
  base. **Next probe:** identify `[+0x3c]`/`[+0x40]` (which AddBuff arg feeds each) and the
  `+0xe80` global value (FPS/time-base) to fix the unit, then emit `buffDur` in `apply_f1.tsv` and
  expire buffs. Do not apply a guessed unit.
- **Per-skill cooldown / GCD values. DONE (opt-in, 2026-10-09).** `RC_AUTHORED_CD=1` applies the
  authored per-skill cooldown (`cooldowns_f1` via the mechanics `normalCd` row) + authored GCD
  (`gcdRow`); default stays the locked uniform 3 s / 1.19 s. Verified: 65029 -> gcdMs=1500,
  cdMs=10000 (authored) vs 1190/3000 (default).
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
5. **W4** — deeper engine RE (`.Sfx` factory owner chain); W2 done.
6. **C: Lua VM** — for the 46 script-only abilities.
