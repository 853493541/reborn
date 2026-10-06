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
