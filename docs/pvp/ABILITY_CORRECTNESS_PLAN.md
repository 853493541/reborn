# Ability correctness — forward plan (make every roster ability behave as authored)

**Status:** active plan. Area: pvp / netcode / client. Created: 2026-10-08.
**Goal:** every one of the 154 roster abilities (F1/绝境 body) does what the client authored:
cast → correct target/AoE, damage, CC, buffs, child casts, movement, resources/CD/GCD, cast
animation, cast effect, hit effect, be-hit animation, hit sound.

This supersedes nothing; it is the umbrella forward plan. Detail lives in
`ABILITY_MECHANICS_PLAN.md` (mechanics P0–P7) and `ABILITY_FX_AND_HIT_REACTION_PLAN.md` (FX1–FX6).

## Current coverage (2026-10-08)

| Layer | State |
|---|---|
| Cast chain (anim → prepare/channel → dash → effect) | done; 115 castables verified |
| Mechanics (damage/CC/buffs/child/pull/knock/self-move) | done (P2–P3) |
| Resources / GCD / cooldowns | done (P4); uniform 20 尺 range, 3 s cd, 1.19 s GCD |
| Ability panel + F1/F2/F3 review tagging | done; persists |
| FX1 extract (hit_fx_f1.tsv) | done |
| FX3 hit effect (.pss) | done; one-shot (unique instance + 2 s removal) |
| **Castability** | **39 abilities AV-blacklisted — cannot cast** |
| FX2 landing-zone SFX (.Sfx) | **blocked** (engine .Sfx spawn faults) |
| FX4 be-hit animation | **done** (2026-10-09): target `_bat01` sibling of its idle anim, played + restored (`proof/netcode/skillv6_fx4_behit_20261009.txt`) |
| FX5 hit sound | **blocked** (name→Wwise-id mapping) |
| FX6 be-hit shake | optional (RepresentID selector unknown) |
| Scripts beyond static patterns (`-` abilities) | open (Lua 5.1 VM) |
| Damage scaling (weapon%/AP) | **blocked** (no player stats) |

## Forward phases

- **FWD1 — Castability: un-blacklist the 39 AV abilities. IN PROGRESS — root cause is per-tani
  (engine).** Reproduced 65068 (百足) with the blacklist emptied: `cast chain 65068` +
  `cast chain started`, then `System.AccessViolationException at RebornClient.Main` (crash);
  the AV is in an engine call in the cast path (D6 is seeded, so not D6; no summon/doodad in
  its apply; the child 65069 is not executed). Matches the known open item
  (`docs/EXPERIENCES.md` ~7957: "the per-tani AV root cause (39 tanis) is an open engine/host
  item"). Unblock = find why the per-tani anim AVs the host (engine RE) — likely the tani's
  skeleton/shadow/weapon binding. Until then these 39 stay blacklisted (do NOT substitute a
  fallback anim: that is a band-aid).
- **FWD2 — FX4 default be-hit animation. DONE (2026-10-09).** Disassembled
  `KRLCharacter::BeHittedByNpc` @ `0x1804cfb50` (kind map 1/2/6 -> `[rsi+0xa4/0x94/0x8c]`),
  `KRLCharacter::BeHittedByPlayer` @ `0x1804d0220` (npc_source target -> literal `bat01.ani` @
  `0x180caf980`) and the sibling builder `0x1804d8700` (template
  `data/source/npc_source/%s/动作/%s_%s` @ `0x180cac990`). The client plays the target's `_bat01`
  sibling of its idle anim when `isPlayBehit` and restores idle after 800 ms. Verified A/B.
  Proof `proof/netcode/skillv6_fx4_behit_20261009.txt`.
- **FWD3 — FX5 hit sound.** Map the `hit_target_sound` SoundEvent name → Wwise event id (parse
  the `.bnk` HIRC, or expose a by-name PostEvent in `sound_probe`). Material-aware.
- **FWD4 — FX2 landing-zone SFX.** Native-RE the engine `.Sfx` factory owner so `.Sfx` spawns
  (or find a scene-level spawn). Then play `AOESelectionSFXFile` at the AoE centre.
- **FWD5 — FX6 be-hit shake** (optional): find the `behit_shake` RepresentID selector; apply.
- **FWD6 — Scripts / `-` abilities.** Lua 5.1 VM + engine-API shim for the scripts whose logic
  is not a static pattern (residual list in `proof/netcode/skillv6_residual_*`).
- **FWD7 — Damage scaling.** Only if a character attribute source is found (currently blocked).
- **FWD8 — Per-ability review loop.** The user tags each ability via the panel (F1 已完成 /
  F2 测试中 / F3 需要修复); fix every 需要修复 ability until 已完成 covers the roster.

## Verification protocol (every fix)

1. Reproduce deterministically (scripted `RC_*` probe; capture the log + a numeric fingerprint).
2. Fix against the reproduction.
3. Re-run the same reproduction and show before/after; run the must-stay-green gates
   (`jx3_model.py`, `gravity/verify_model.py`, `loot/capture.py selftest`).
4. Proof under `proof/netcode/skillv6_*`; register docs; append `docs/EXPERIENCES.md`.

## Reproduce / run

```
# build a feature client
set RC_CLIENT_EXE=reborn_client_skillv6.exe && client\build_client.cmd
# drive one ability (isolated namespace so the user's session is untouched)
set RC_MEM_NS=reborn_skillv6_probe.memory && set RC_SLOTS=<id> && set RC_CAST_AT=38000:1
```
