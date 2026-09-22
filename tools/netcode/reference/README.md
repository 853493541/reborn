# Reference server + client (reborn netcode model)

Runnable, stdlib-only reference implementation of `docs/netcode/REBORN_SERVER_SPEC.md`,
which is itself modeled on the JX3 findings in `docs/netcode/JX3_PROTOCOL_SPEC.md`.

## Run

```powershell
.\.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py
```

Expected output: 15 x `PASS`, exit code 0. Covers:

1. spawn rules deterministic per seed
2. handshake spawns a new session
3. join world
4. client prediction converges with server state
5. forward movement reaches the server
6. AOI entity add both ways (2 clients)
7. skill lifecycle prepare → cast → effect → cooldown
8. casting a move-forbidden skill while moving is rejected (`move_state`)
9. a dropped cast frame is recovered by serial retransmit
10. container spawns in AOI (doodad add)
11. loot list arrives after the wind-up
12. taking grants an item to the inventory
13. emptied container despawns
14. container respawns on the timer
15. reconnect with `resume_seq` recovers the session + position

## Map to the spec

| Spec area | Code |
|---|---|
| frame codec (15-byte prefix + size + payload) | `HEADER`, `encode`, `decode` |
| serial/ack + retransmit + dedupe | `ReliableChannel` |
| session key + serial resume + reconnect window | `OP_HANDSHAKE`, `SessionState`, `GameServer._handle` |
| ping / dead timeout | `GameServer._tick` timeout, `GameClient.ping` |
| 30 Hz sim, 10 Hz state/snapshot | `GameServer._tick`, `SNAPSHOT_EVERY` |
| prediction + reconciliation | `GameClient.move`, `OP_MOVE_STATE` handler, `apply_input` |
| server-authoritative skills + cooldown + move-state reject | `Peer._cast`, `SKILLS` |
| interest management | `Peer.push_state` (`AOI_RANGE`) |
| loot spawner + containers + pickup | `Spawner`, `LOOT_TABLES`, `Peer._loot_open/_loot_take` |

## Loot spawner — JX3-like rules (our own data)

Mirrors the rule *shape* observed statically in JX3 (`JX3_MODE_LOOT_SYSTEM.md`),
with reborn's own anchors, items and numbers:

| JX3 rule (observed) | Reborn rule (implemented) |
|---|---|
| fixed anchor set per map; a subset active per match (semi-random) | `build_anchors()` = 6 hotspot clusters + scattered points, deterministic per `(seed, map)`; `spawn_match()` activates each anchor with `LOOT_ACTIVE_RATIO` (hotspots ×1.25) |
| rich centre / poor rim zones | `zone_of()` by distance: <35% = zone 3 (rich), <70% = zone 2, else zone 1; `ZONE_WEIGHTS` picks container type per zone |
| hotspot clusters | hotspot anchors multiply high-tier weights (`HOTSPOT_BIAS`) |
| tiered containers (1st/2nd/3rd gear, weapon, meds, supply, secret) | `CONTAINERS` 1..7 with tier + drop table |
| per-container weighted drop table rolled on open | `LOOT_TABLES` + `roll_contents()` (weight, count range) |
| wind-up before the loot list (`OpenPrepareFrame`) | server holds `prepare_ms` before `OP_LOOT_LIST` |
| loot range + once per player | `LOOT_RANGE = 5.0`, `Peer.opened` |
| container removed when emptied, respawn on timer | `Spawner.deplete()` + `tick()` (`respawn_scale`) |
| phase escalation (later zones richer) | `Spawner.phase` boosts tier 2/3 weights in `roll_template` |

Protocols (our opcode space, shapes consistent with the recovered JX3 layouts):

```
0x0050 DOODAD_ADD     {id, template, name, tier, pos}
0x0051 DOODAD_REMOVE  {id}
0x0052 DOODAD_STATE   {id, state}
0x0053 LOOT_OPEN      {id}          (C->S)
0x0054 LOOT_LIST      {id, template, name, items:[{slot,name,rarity,count}]}
0x0055 LOOT_TAKE      {id, slot}    (C->S)
0x0056 LOOT_RESULT    {id, ok, slot, item, reason}
0x0057 ITEM_ADD       {name, rarity, count, inventory_size}
```

## Deliberate simplifications (documented, not hidden)

- payloads are JSON; production should use the fixed-size table + typed primitives
  (`CoreNet::KNetBuffer`-style) from the spec
- movement is a flat-plane kinematic model; production swaps in the real
  capsule/terrain rules shared by client and server
- loot contents are rolled once per container (global); JX3 can roll per opener —
  the per-player variant is a small change to `Peer._loot_open`
- anchors live in one plane (`y=0`) until terrain sampling is wired in
- no compression/crypto layer yet (spec §5 covers the hooks)
- one connection per client; gateway is folded into the game server
