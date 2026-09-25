# reborn PvP battle research index

Static, read-only research on the local JX3 client used to recreate the battle
system for reborn, with a PvP focus. Branch: `research/jx3-pvp-battle`.

| Doc | Content |
|---|---|
| `JX3_PVP_BATTLE_RESEARCH.md` | **synthesis**: attributes/units, damage & mitigation, buffs/CC/DR, casting/GCD/resources, PvP modes, combat netcode, open items |
| `REBORN_PVP_BATTLE_SPEC.md` | implementable server/client contract: data model, damage pipeline, validation gates, our opcode set, mode config, test plan |

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
`dump_fn_disasm.py`, …) and the reused `tools/netcode/` Lua/binary helpers.

Sources: local JX3 install (`...\zhcn_hd`), extracted PakV4 assets under
`SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\pakv4-probe`, and the
committed binary string dumps in `proof/netcode/`.
