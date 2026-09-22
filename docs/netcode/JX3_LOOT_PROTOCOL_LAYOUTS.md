# JX3 loot/doodad protocol layouts (static recovery)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Method:** string-xref disassembly of `JX3LogicEditOperationX64.dll` (image base
`0x180000000`) + Lua 5.1 bytecode analysis. Evidence: `proof/netcode/disasm/`.
**Confidence:** HIGH = offsets read directly from disassembly; MED = semantics inferred.

These layouts close the client half of the spawn→pickup chain
(`JX3_MODE_LOOT_SYSTEM.md` §8 items B1, C1, E1, F1).

---

## 1. S→C `OnSyncNewDoodad` — container/object spawn (HIGH)

Handler `0x18019D510`. Packet pointer `p` (assert `pDoodadData`).

| Off | Size | Meaning | Where it goes |
|---|---|---|---|
| `+0x07` | u32 | doodad **template id** | looked up in doodad list |
| `+0x0B` | u32 | doodad **global id** | passed to scene `AddDoodad` path |
| `+0x0F` | u8 | flag/camp byte | doodad+0x44 |
| `+0x10` | u8 | flag byte | doodad+0x48 |
| `+0x11` | u8 | flag byte | doodad+0x118 |
| `+0x12` | u8 | **kind/type** (e.g. 0x0E checked later) | doodad+0xAC |
| `+0x13` | u8 | state byte (passed to `0x1802C7190`) | state |
| `+0x14` | u32 | NPC template id (0 = none; copies 0x20-byte name into doodad+0x8C) | doodad+0xC0 |
| `+0x18` | u32 | secondary npc/type id | doodad+0xC4 |
| `+0x1C` | u32 | value | doodad+0xC8 |
| `+0x20/+0x24` | u32 ×2 | optional range/param pair (if +0x20 > 0) | `0x1802C7580` |
| `+0x28` | u8 | flag | doodad+0x108 |
| `+0x29` | u32 | value | doodad+0xFC |
| `+0x2D` | u32 | count/owner-related (if > 0, team check) | doodad+0x11C |
| `+0x31` | u32 | value | doodad+0x120 |
| `+0x35` | u32 | **link/owner id** (-1 = none) | doodad+0xD0 |
| `+0x39` | u64 | **packed transform**: bits0-17 = X (18-bit fixed), bits18-35 = Z, bits36-57 = Y (22-bit); bit58 = flag→doodad+0xCC, bit59/bit60 = two bools to scene calls | position/flags |
| `+0x43` | u8 | spawn broadcast flag | gate for scene list insert |

X/Z pack in 18 bits with a 7-bit region at bit11 (`(x>>11)&0x7F`) — i.e. region + 11-bit
sub-cell fixed point. Y is 22-bit.

## 2. S→C `OnSyncDoodadState` — container state update (HIGH)

Handler `0x1801975F0`. Packet pointer `p`.

| Off | Size | Meaning |
|---|---|---|
| `+0x07` | u32 | doodad global id (lookup) |
| `+0x0B` | s8 | state (passed to doodad state setter `0x1802C7190`) |
| `+0x0C` | u8 | flag A → `0x180158E80(scene, linkId)` |
| `+0x0D` | u8 | flag B → `0x180158EE0(scene, linkId)` |

## 3. S→C `OnSyncLootList` — **the rolled contents** (HIGH)

Handler `0x18019BA60`. Packet pointer `p`; packet is size-prefixed (`u16 @+7`).

| Off | Size | Meaning |
|---|---|---|
| `+0x07` | u16 | total packet size (loop bound) |
| `+0x09` | u32 | doodad/entity global id (lookup; existing window released) |
| `+0x0D` | u32 | loot-list id |
| `+0x11` | u8 | `0` = create new loot window (alloc 0xA8) |
| `+0x12` | u32 | window field (+0x68) |
| `+0x16` | u8 | **looter count N** |
| `+0x17` | u32 | window field (+0x6C) |
| `+0x1B` | u32 | window field (+0x70) |
| `+0x1F` | u32 × N | **looter player ids** (client checks its own id here) |
| after looters | records | **loot items**, variable length |

Loot item record (cursor `q`):

| Off | Size | Meaning |
|---|---|---|
| `+0` | u8 | item type/tab (`edx` to item factory `0x1802E22C0`) |
| `+1` | u32 | item index/global id (`edx`) |
| `+5` | u8 | count (→ item+8) |
| `+6` | u8 | flag/bound bool (→ item+0xC) |
| `+7` | u8 | extra data length L |
| `+8` | u8 × L | extra data (passed as string to the item vtable `+0x20`) |

Loop condition: `cursor - p < size(u16 @+7)`; after each record `cursor += 8 + L`.
So a parsed loot window yields: container id + list of `(type, index, count, flag, extra)`.

## 4. C→S `DoApplyLootList` — take item(s) (HIGH)

Builder inside the doodad handler block (`0x18019DA0F`), assert folded at `0x18019DA37`.

```
protocol id = 0x4D          (77)
frame size  = 0x0F          (15 bytes: 15-byte header only)
param       = u32 @+0xB ← [request+7]  (doodad/entity global id)
```

## 5. C→S `DoLootMoney` (HIGH)

Clean builder `0x180175020`.

```
protocol id = 0x51          (81)
frame size  = 0x0F          (15 bytes)
param       = u32 @+0xB    (money/loot index)     [semantics MED]
```

## 6. Pick / prepare (client-side only) (HIGH)

No `DoPickPrepare` message exists in either binary — the "prepare" phase is purely the
client-side interaction timer from `DoodadTemplate` (`OpenPrepareFrame` frames), after
which the client sends `DoApplyLootList` (0x4D). `PickUpItem`/`CanLoot` are Lua-facing
functions (`KPlayer::LuaCanLootBoxItem`) that gate the UI, not wire messages.

## 7. What this enables

A capture/replay tool can now:
1. decode every container spawn (`OnSyncNewDoodad`) → template id + exact position;
2. follow state changes (`OnSyncDoodadState`);
3. read the **rolled loot contents** (`OnSyncLootList`) per container;
4. see the take requests (`DoApplyLootList` 0x4D) and money takes (`DoLootMoney` 0x51).

Combined with `DoodadTemplate` (template → drop-table name), logging messages 1+3 during
play reconstructs the empirical spawn distribution and roll table per container — the
remaining server-only pieces (chain items A1/A3/D2/G2).

## 8. Evidence

`proof/netcode/disasm/OnSyncNewDoodad.txt`, `OnSyncDoodadState.txt`,
`OnSyncLootList.txt`, `OnOpenLootList.txt`, `DoApplyLootList.txt`,
`apply_loot_build.txt`, `DoLootMoney.txt`.
