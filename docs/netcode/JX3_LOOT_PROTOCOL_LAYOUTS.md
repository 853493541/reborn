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
| `+0x07` | u32 | doodad **template id** (used as list lookup key) | looked up in doodad list |
| `+0x0B` | u32 | doodad id (second key; role vs +0x07 not fully proven) | passed to scene `AddDoodad` path |
| `+0x0F` | u8 | byte stored as doodad field | doodad+0x44 |
| `+0x10` | u8 | byte stored as doodad field | doodad+0x48 |
| `+0x11` | u8 | byte stored as doodad field | doodad+0x118 |
| `+0x12` | u8 | kind/type byte; `0x0E` compared later against doodad+0xAC | doodad+0xAC |
| `+0x13` | u8 | state byte (same setter as `OnSyncDoodadState` uses) | state |
| `+0x14` | u32 | NPC template id (0 = none; proven by lookup + 0x20-byte name copy) | doodad+0xC0 |
| `+0x18` | u32 | u32 stored (role unknown) | doodad+0xC4 |
| `+0x1C` | u32 | u32 stored (role unknown) | doodad+0xC8 |
| `+0x20/+0x24` | u32 ×2 | u32 pair, used only when `[+0x20] > 0` | `0x1802C7580` |
| `+0x28` | u8 | byte stored (role unknown) | doodad+0x108 |
| `+0x29` | u32 | u32 stored (role unknown) | doodad+0xFC |
| `+0x2D` | s32 | if > 0 and player+0x20960 set, passed to a check | doodad+0x11C |
| `+0x31` | u32 | u32 stored (role unknown) | doodad+0x120 |
| `+0x35` | s32 | id used with two scene calls when `!= -1`; `-1` = none | doodad+0xD0 |
| `+0x39` | u64 | **packed transform** (bit ranges proven by masks): bits0-17 = X, bits18-35 = Z, bits36-57 = Y; bit58 → doodad+0xCC; bit59/bit60 passed as bools to scene calls | position/flags |
| `+0x43` | u8 | gate byte: when non-zero the doodad is inserted into a scene list | scene list insert |

Field **offsets/sizes** are read directly from disassembly (HIGH). The **semantics labels**
in the right column that say "role unknown" are deliberately not guessed. Bit ranges inside
`+0x39` are proven by the AND/shift masks; that X/Z carry a 7-bit region at bit11
(`(v>>11)&0x7F` is passed to a scene call) is proven; calling it a "terrain region" is an
inference (MED).

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
frame size  = 0x0F          (11-byte base header + u32 param @+0xB)
param       = u32 @+0xB ← [request+7]  (doodad/entity global id)
```

## 5. C→S `DoLootMoney` (HIGH)

Clean builder `0x180175020`.

```
protocol id = 0x51          (81)
frame size  = 0x0F          (11-byte base header + u32 param @+0xB)
param       = u32 @+0xB    (money/loot index)     [semantics MED, not proven]
```

## 6. Pick / prepare (scope-limited finding)

`DoPickPrepare` / `OnBreakPickPrepare` / `PickUpItem` were **not found as strings** in the two
binaries scanned (`JX3ClientX64.exe`, `JX3LogicEditOperationX64.dll`). What is verified: the
take request is `DoApplyLootList` (0x4D), and the UI gates use Lua-facing
`KPlayer::LuaCanLootBoxItem` / `CanLoot`. Whether a prepare-phase message exists in another
module is **unverified**; `OpenPrepareFrame` in `DoodadTemplate` is at least the client-side
wind-up timer (frames).

## 7. What this enables

A capture/replay tool can now:
1. decode every container spawn (`OnSyncNewDoodad`) → template id + exact position;
2. follow state changes (`OnSyncDoodadState`);
3. read the **rolled loot contents** (`OnSyncLootList`) per container;
4. see the take requests (`DoApplyLootList` 0x4D) and money takes (`DoLootMoney` 0x51).

Combined with `DoodadTemplate` (template → drop-table name), logging messages 1+3 during
play reconstructs the empirical spawn distribution and roll table per container — the
pieces that are not available locally (chain items A1/A3/D2/G2).

## 8. Evidence

`proof/netcode/disasm/OnSyncNewDoodad.txt`, `OnSyncDoodadState.txt`,
`OnSyncLootList.txt`, `OnOpenLootList.txt`, `DoApplyLootList.txt`,
`apply_loot_build.txt`, `DoLootMoney.txt`.

## 9. Assumptions and scope limits (audit, 2026-09-21)

| Statement | Status |
|---|---|
| offsets/sizes of all fields listed in §1–§5 | PROVEN (read from disassembly) |
| `+0x14` = NPC template id | PROVEN (lookup + 0x20-byte name copy into doodad) |
| `+0x39` bit ranges, bit58→+0xCC, bits59/60→scene bools | PROVEN (masks/shifts) |
| X/Z contain a 7-bit sub-field at bit11 | PROVEN (mask+shift+call); "terrain region" is INFERRED |
| §1 "role unknown" fields (`+0x18,+0x1C,+0x29,+0x31,+0x0F,+0x10,+0x11,+0x28`) | semantics NOT claimed |
| `+0x0B` vs `+0x07` split of template/global id | PARTIAL — both are lookup/registration keys; which is authoritative is inferred |
| `+0x2D` "team check" | INFERRED (a non-null manager slot at player+0x20960 gates a call) |
| `+0x35` "link/owner id" | INFERRED from paired scene calls; only `-1 = none` is proven |
| `+0x43` "broadcast/spawn gate" | INFERRED (byte gates a list insert) |
| `DoLootMoney` param semantics | NOT proven |
| "no prepare message" | SCOPE-LIMITED to the two binaries scanned |
| `OnSyncLootList` window fields (`+0x12,+0x17,+0x1B`, dest +0x68/0x6C/0x70) | offsets PROVEN; meanings not claimed |
| drop-table contents/rates, spawn anchors | NOT in this doc (see `JX3_MODE_SPAWN_RULES_SEARCH.md`) |
