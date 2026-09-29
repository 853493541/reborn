# mode/capture.py — 绝境战场 S2C capture decoder

Decodes raw client packet buffers using the opcode IDs recovered from the
KPlayerClient protocol registration table (`proof/netcode/protocol_table_s2c_annotated.tsv`).

## Input

JSONL, one record per packet:

```json
{"dir":"S2C","opcode":282,"buf":"<hex bytes>"}
{"dir":"S2C","opcode":283,"buf":"<hex bytes>"}
```

`buf` starts at the frame header (protocol id at `+0x00`, payload from `+0x07`).

## Opcodes handled

| opcode | handler | decoded |
|---|---|---|
| `0x08` | `OnSwitchMap` | map id + placement x/y/z (cm) + scene |
| `0xAA` | `OnSetBattleFieldSide` | character id → side |
| `0x11A` | `OnSyncBFObjectiveIncrement` | objective index + two values |
| `0x11B` | `OnSyncBFPQInfo` | phase state A/B, start offset, duration (seconds) |
| `0x119` | `OnSyncBattlefieldStatistics` | id/force/side/name/global id + 25 stats |
| `0x330` | `OnSyncBattleStatFlag` | two u32 |
| `0x245` | `OnSyncBFRoleData` | player id + raw array |
| `0x25B/0x25F/0x260/0x261/0x262/0x323/0x324` | rank / competitor / rooms | raw hex |

Loot packets (`0x0C/0x10/0x82`, takes `0x4D/0x51`) are decoded by
`tools/netcode/loot/capture.py`; feed the same JSONL to both tools.

## Usage

```powershell
python tools\netcode\mode\capture.py selftest
python tools\netcode\mode\capture.py decode capture.jsonl --out dir
```

Outputs: `switch_maps.csv`, `sides.csv`, `objective.csv`, `pq_clock.csv`,
`statistics.csv`, `stat_flags.csv`, `bf_roles.csv`, `raw_known.csv`, `summary.txt`.
