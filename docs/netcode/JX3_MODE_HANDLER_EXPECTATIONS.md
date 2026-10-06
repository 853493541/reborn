# JX3 绝境战场 — what the CLIENT expects per mode message (entry checks)

**Date:** 2026-10-05 · **Method:** static disasm of the registered handlers
(`proof/netcode/protocol_table_s2c_annotated.tsv` gives id/size/handler) — the early
validation each handler performs before accepting a server message.

Legend: `EXACT` = the handler compares the packet size for equality (wrong size -> error log +
drop); `LOOKUP` = an entity/object lookup that must succeed; `SCENE` = requires the entity's
scene bind (`entity+0x60 != 0`).

| Id | Handler | Size | What the client requires |
|---|---|---|---|
| 170 (0xAA) | `OnSetBattleFieldSide` | 15 | `[+7]` entity id (bit30 -> global-id lookup) must resolve (`LOOKUP`, err 0x53F5); `[+0xB]` side dword -> setter `0x1802B46E0` must return non-zero |
| 581 (0x245) | `OnSyncBFRoleData` | 112 | `[+0xF]` player id must resolve (`LOOKUP`, err 0x7DB3); array at `[+0x17]`, count byte at `[+0x6F]` -> player `+0xEC8` via `client+0x2831F8` |
| 607 | `OnSyncBaseInfoFromBattlefieldCompetitorList` | var | `[+9]`/`[+0xD]` dwords -> lookup `0x18012B750` must succeed (err 0x7363); then the competitor record |
| 609 | `OnSyncBattlefieldCompetitorCDState` | 27 | `[+7]` player id must resolve (err 0x7FA3); **entity+0x60 must be non-null (`SCENE`, err 0x7FA4)** |
| 610 | `OnSyncBattlefieldCompetitorBuffList` | var | competitor record path (same family) |
| 803 | `OnCreateBattlefieldRoomRespond` | **EXACT 27** | size must equal 0x1B (else err 0x8029); UI object non-null; `[+7]` qword, `[+0xF]` qword, `[+0x17]` dword -> UI vtable `[+0x1550]` |
| 804 | `OnForceStartBattleFieldChaosFightRespond` | **EXACT 19** | size must equal 0x13 (else err 0x8038); UI object non-null; `[+7]` qword, `[+0xF]` dword -> UI vtable `[+0x1558]` |

## Movement & combat handlers (same extraction method)

| Id | Handler | Size | What the client requires |
|---|---|---|---|
| 25 (0x19) | `OnSyncMoveState` | 52 | `[+7]` entity id (bit30 -> global-id lookup `0x18012B5B0`, else `0x18012B610`) **must resolve** (null -> exit `0x18019D358`); then `[entity+8]` etc. |
| 22 (0x16) | `OnSyncMoveCtrl` | 47 | entity id `[+7]` lookup (same pattern) |
| 23 (0x17) | `OnSyncMoveParam` | 49 | entity id `[+7]` lookup (same pattern) |
| 24 (0x18) | `OnAdjustPlayerMove` | 71 | entity id lookup + position adjust |
| 39 (0x27) | `OnSkillPrepare` | 33 | entity id `[+7]` lookup (same pattern) + a 0x1801DBA30 scope init |
| 41 (0x29) | `OnSkillCast` | 39 | entity id `[+7]` lookup + 0x1802F48C0 / 0x1801DBA30 |
| 47 (0x2F) | `OnSkillEffectResult` | var | big handler (0x1B0 stack) - effect application |
| 595 (0x253) | `OnSkillChannel` | 37 | channel sync (same family) |
| 30 (0x1E) | `OnCharacterDeath` | 31 | death notify (entity id lookup family) |
| 185 (0xB9) | `OnCharacterDeath` | 15 | short variant |
| 210 (0xD2) | `OnSyncSlayKillCount` | 9 | kill counter |

**Universal rule (movement + combat):** every message is keyed by the entity id dword at `+7`
(bit 30 selects the global-id lookup); an unknown id makes the handler silently drop the
message. The same scene-bind prerequisite applies to entity state handlers.

## AOI / "how far do we receive information"

There is **no client-side range limit** in the entity handlers: id 10/11/12 validate only that
the position is inside the scene (dims + grid cell), not a radius. **The sync range (AOI) is the
server's choice** - the client accepts and renders whatever entities it is sent. Practical
defaults from the shipped data: `MaxLootRange=5` (MapList) for loot interaction; entity AOI is
a server-side constant to define (typical MMO values 30-100 m; our server picks it).

## Endgame (mode end)

No dedicated "mode end" S2C handler exists in the registration table under the obvious names
(`BattleFieldEnd`/`Result`/`Settle` not present). The end-of-match surface is built from:
`OnSyncBattlefieldStatistics` (id 281, 264 B), the stat flags (0x330), the competitor/rank
syncs, and the UI events (`LOADING_END` family + the mode UI). The endgame sequence is
therefore server-driven: stop the phase clock, send the final statistics/rank records, and the
client UI shows the result.

## Takeaways

1. **Exact sizes** for the UI responds (27/19) — the client rejects any other size outright.
2. **Valid entity ids** in every sync message — the lookups are by the dword at `+7`/`+0xF`
   (bit 30 selects the global-id lookup path).
3. **The scene bind (`entity+0x60`) is a prerequisite of the mode messages too** (id 609) —
   the same gate as the world entry (`JX3_MODE_MATCH_LIFECYCLE.md` staging + the id-10 guard).
4. The UI vtable slots (`+0x1550`/`+0x1558`) are the room/force-start UI entry points — the
   global UI object must exist (it does once the client UI is up).

## Reproduce

```
.venv\Scripts\python.exe - <<'PY'
import pefile, capstone
fp = r'C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3LogicEditOperationX64.dll'
pe = pefile.PE(fp, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
for va in (0x18018DC50, 0x180193AC0, 0x180193BB0, 0x180194490, 0x180194390, 0x180184480, 0x180186C90):
    for ins in md.disasm(pe.get_data(va - base, 0x70), va):
        print(hex(ins.address), ins.mnemonic, ins.op_str)
PY
```
