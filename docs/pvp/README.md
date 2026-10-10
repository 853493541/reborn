# reborn PvP battle research index

Static, read-only research on the local JX3 client used to recreate the battle
system for reborn, with a PvP focus. Branch: `research/jx3-pvp-battle`.

| Doc | Content |
|---|---|
| `JX3_PVP_BATTLE_RESEARCH.md` | **synthesis**: attributes/units, damage & mitigation, buffs/CC/DR, casting/GCD/resources, PvP modes, combat netcode, open items |
| `REBORN_PVP_BATTLE_SPEC.md` | implementable server/client contract: data model, damage pipeline, validation gates, our opcode set, mode config, test plan |
| `TARGET_DUMMY_RESEARCH.md` | 木桩 target dummies: full inventory + live spawn stats (主城木桩 zone + generic), model resolution, PvP damage-test framing, sandbox verification |
| `ABILITY_MECHANICS_PLAN.md` | **plan**: make every ability do what it was designed to do (targets/AoE, damage/heal, buffs/CC, movement, child skills, costs/GCD); phases P0–P7, grounded in `mechanics_f1.tsv` |
| `ABILITY_FX_AND_HIT_REACTION_PLAN.md` | **plan**: ability FX beyond the cast — landing-zone SFX (`AOESelectionSFXFile`), hit effect (`HitEffectResultID`→.pss), be-hit animation (`BeHittedBy*`) + hit sound (`HitSoundID`); phases FX1–FX6, grounded in `skill_caster_f1.txt` (五方行尽 65149 worked example) |
| `ABILITY_CORRECTNESS_PLAN.md` | **umbrella forward plan**: make every roster ability behave as authored; phases FWD1 (un-blacklist 39 AV abilities) → FWD8 (per-ability review loop); coverage table + verification protocol |
| `ENGINE_RE_BREAKTHROUGH_PLAN.md` | **plan**: break the binary-RE / packed-data walls (W1 per-tani AV, W2 default be-hit anim, W3 Wwise name→id, W4 engine `.Sfx`, W5 Lua VM) using `tools/pvp/dump_fn_disasm.py`, `minidump_exc.py`, `PakV4SfxExtract.exe` |
| `ABILITY_FOLLOWUP_PLAN.md` | **inventory**: everything still imprecise (engine RE, mechanics, scripts, tooling) with confidence + the exact next probe per item |
| `SESSION_HANDOFF_20261009.md` | **handoff**: branch/state, build/run/verify, what's done, what's missing (+ next probe each), key files, RC knobs, gotchas — for the next session |
| `SESSION_HANDOFF_20261009b.md` | **handoff 2** (same day feature work): FX4 be-hit anim, buff durations, AoE fix, sweep regression green; W4/W3/W1 blockers + next probes; closed items (CC, chain/tag/shake, missiles, nMinRadius) |
| `HANDOFF_W1_SKILLV6_20261009.md` | **handoff**: worktree/branch/tip, what's done, the W1 open question, repro/gates, gotchas — for another agent to pick up this worktree |
| `W1_CRITICAL_SECTION_ROOT_20261009.md` | **W1 root**: the 39-ability AV is a null-deref in `RtlEnterCriticalSection` on the uninitialized static cs `KG3DEngine+0x2D3EEE8` (from `KG3D_TimeLine<float>::CreateCache`); the engine's lazy `RtlInitializeCriticalSection` is gated by a global-vs-TLS once-check that our run fails. Corrected chain + next probes |

Per-workstream evidence reports (raw values + citations + confidence):

| Report | Content |
|---|---|
| `../proof/pvp/attributes_and_damage.md` | attribute taxonomy (138 UI / 461 buff / 694 engine), unit conventions, mitigation field runs, GlobalParam coefficients, 化劲/御劲 |
| `../proof/pvp/buff_control_system.md` | Buff.tab field semantics, CC families, MoveState enum, DecayType DR, 解控 sets, dispel groups, stacking, mode masks |
| `../proof/pvp/cast_cooldown_resources.md` | prepare/channel model, GCD rows, cooldown charges/overdraft/haste, resources, talents, validation checklist |
| `../proof/pvp/pvp_modes_rules.md` | arena/battleground/BR/camp maps & flags, bans, currencies, UI Lua status |
| `../proof/pvp/combat_netcode.md` | C2S/S2C combat opcodes, wire shapes, server gates, reborn proposal |

Catalogs: `../proof/pvp/attr_catalog.tsv`, `../proof/pvp/combat_opcodes.tsv`,
`../proof/pvp/cooldown_usage_catalog.tsv`, `../proof/pvp/decay_and_controls.tsv`.

Tools: `tools/pvp/` (`tab.py` table reader, `field_semantics.py`, `verify_pvp_evidence.py`,
`dump_fn_disasm.py`, `disasm_fn.py`, `mini_debugger.py` / `multi_trace.py` — read-only **hardware-BP
live debugger** (AV/fault catch, stack tagging, full-memory dump) for our own host process, ported
from `agent/v2`; `minidump_full.py` — streaming (mmap) reader for a multi-GB minidump (modules/
threads/contexts/memory); `verify_skillv6_data.py` — offline invariant checks for the v6 datasets
(FX4/buff/AoE); the W1/W4 unblock),
`tools/netcode/mode/extract_target_dummies.py` (dummy
NPC-template extraction; in-client spawn: `client/RebornClient.cs` `RC_DUMMY`)
and the reused `tools/netcode/` Lua/binary helpers.

Sources: local JX3 install (`...\zhcn_hd`), extracted PakV4 assets under
`SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\pakv4-probe`, and the
committed binary string dumps in `proof/netcode/`.
