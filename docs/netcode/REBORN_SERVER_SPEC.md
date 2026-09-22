# Reborn server + client spec (modeled on JX3)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Input:** `docs/netcode/JX3_PROTOCOL_SPEC.md` (static reverse-engineering findings)
**Goal:** a complete, implementable contract for our own server and client that
recreates the JX3 **model** — reliable framed session, serial/ack, ping/resume,
server-authoritative world with client prediction and interpolation.

This is our own protocol: IDs, byte layouts, and constants below are ours.
No JX3 opcodes, keys, or wire bytes are reused. The model is what we copy
(and JX3 itself is the evidence that this model works at MMO scale).

---

## 1. Topology

```
client ──TLS/TCP──▶ gateway  (auth, role list, session ticket, server assignment)
client ──TCP────── ▶ game server (authoritative world, per-map instance)
                       │
                       ├─ session/serial layer (this spec)
                       ├─ world sim (movement, combat, AI)
                       ├─ interest management (AOI grid)
                       └─ persistence (DB, async)
```

- **Transport: TCP** for the reliable session (JX3 uses TCP + app-level acks);
  optional UDP channel later for high-rate movement if profiling demands it.
- One **network thread** per game-server process; world ticks on the logic thread.
- Gateway may be the same process at first; split when needed. A plain Linux VM
  with Docker is enough for both (no GPU; all rendering is client-side).

## 2. Frame

All game-server messages use one frame:

```c
struct Frame {          // 15-byte common prefix
    u16 opcode;         // our ID space (see §7)
    u8  flags;          // bit0 RETRANSMIT, bit1 HAS_ACK, bit2 COMPRESSED
    u16 seq;            // sender's serial for this message
    u16 ack;            // last serial received from peer
    u32 sid;            // session id low 32 bits (handshake) or 0
    u32 param;          // opcode-specific scalar
    // u16 size;        // when opcode uses size-prefixed payload (routine sync)
    // u8  payload[size];
};
```

- Max frame payload 32,768 bytes (`MAX_EXTERNAL_PACKAGE_SIZE` in JX3 evidence).
- `size` field present on variable-length opcodes only; fixed-size opcodes use
  the compiled size table exactly like JX3's `m_nProtocolSize` check.
- Multi-byte integers little-endian (matches `CoreNet::KNetBuffer`).

## 3. Reliability

Application-level, exactly like the JX3 model:

| Rule | Value |
|---|---|
| serial space | u16, wraps |
| outstanding window | 2048 messages |
| ack | `ack` field = highest serial received (cumulative) |
| retransmit | unacked messages resent after 250 ms RTO, `flags |= RETRANSMIT`, max 8 tries |
| ack packet | when only acking, send `OP_ACK` with `HAS_ACK` (no payload) |
| queue full | if 2048 outstanding, backpressure senders/flush (JX3: "Unconfirm send buffer full!") |

Reliability is per-connection and independent of TCP retransmission: it tells us
the peer **processed** a message (used for handshake resume and business acks).

## 4. Session lifecycle

1. **Gateway**: login → role select → gateway issues a 16-byte `session_key`
   and game-server endpoint + a `ticket`.
2. **Handshake** `C→S OP_HANDSHAKE`: `role_id` (`param`), `session_key` (16 B),
   `resume_seq` (last server serial the client processed), `resume` bool.
3. **`S→C OP_HANDSHAKE_RESULT`**: `recovered` bool, `server_name`, `reconnect_timeout_ms`,
   `start_seq`, `server_tick`.
4. **Ping** `C→S OP_PING` every **3000 ms** with `param = client_tick`.
   Server replies `OP_PONG` (echo) for RTT. Dead timeout **12000 ms** without any
   received frame ⇒ disconnect; client keeps a **reconnect window** of 60 s
   using `resume_seq` to resume the same session.
5. **Resume**: server keeps session state ≥ reconnect window; `recovered=true`
   means the client continues without re-login; otherwise full re-spawn.

## 5. World model

### 5.1 Rates

| Stream | Rate | Notes |
|---|---|---|
| client input | 30 Hz | movement intent + facing |
| server snapshot (self) | 10 Hz | `OP_MOVE_STATE`, includes server tick + ack of last processed input |
| server snapshot (others, near AOI) | 10 Hz | `OP_ENTITY_SNAPSHOT`, batched |
| far AOI | 2 Hz | reduced fields |
| routine sync | 1 Hz | `OP_ROUTINE_SYNC` (JX3 id 0x6E analog) |

### 5.2 Movement (client prediction + server reconciliation)

Client:
1. samples input, applies immediately to the local actor (prediction);
2. stores `(input_seq, position, velocity)` in a history ring (1 s);
3. sends `OP_MOVE_INPUT` with `input_seq`, keys, facing, client_tick.

Server:
1. queues inputs, simulates with the same movement rules (capsule + terrain);
2. sends `OP_MOVE_STATE` with `last_input_seq`, authoritative position/velocity,
   `move_flags` (control-locked, rooted, jumping…);
3. sends `OP_MOVE_CORRECTION` when deviation > 0.5 m (position) or > 15° (facing).

Client on `OP_MOVE_STATE`:
- drop history ≤ `last_input_seq`;
- if authoritative state ≈ predicted → keep predicting from it;
- else snap and replay remaining inputs (reconciliation).

Other actors:
- interpolation buffer 150 ms (`disable remote character interpolate` in JX3 is
  the runtime toggle for the same mechanism);
- extrapolation capped at 250 ms;
- teleports (gap > 30 m) snap.

### 5.3 Combat (server-authoritative)

```
C→S OP_CAST_SKILL { skill_id, target_id, aim_pos, client_tick }
S→C OP_SKILL_PREPARE  { caster, skill_id, cast_time_ms, channel_id }
S→C OP_SKILL_CAST     { caster, skill_id, channel_id }        // animation start
S→C OP_SKILL_EFFECT   { caster, skill_id, targets[...], damage[...], result }
S→C OP_SKILL_REJECT   { reason }   // move_state, cooldown, range, silence, resource
S→C OP_COOLDOWN       { skill_id, ready_at, paused|reset|accelerated }
```

Rules:
- Server owns cooldowns/charges/resources; client only predicts animation/UI.
- Server validates: alive, control-lock flags, distance/range, facing arc,
  cooldown, resource, line-of-sight if required.
- `MOVE_STATE` semantics: while `control_locked` or `casting` with move-forbidden,
  a cast reject uses reason `move_state` (JX3 exposes `YOU_MOVE_STATE_WRONG`,
  `MOVE_STATE_INVALID`, `TARGET_MOVE_STATE_WRONG`).
- Buffs: `OP_BUFF_SYNC` full list on join/resume; incremental add/remove after.

## 6. Interest management

- world divided into tiles (e.g. 128 m); player subscribes to 3×3 tiles;
- enter/leave emits `OP_ENTITY_ADD` / `OP_ENTITY_REMOVE`;
- snapshot batches are per-subscriber, capped (e.g. 200 entities);
- per-entity LOD: full fields near, `id/pos/facing/anim` far.

## 7. Our opcode plan (MVP)

| ID | Name | Dir | Payload |
|---|---|---|---|
| 0x0001 | `OP_HANDSHAKE` | C→S | 16 B key + u16 resume_seq + u8 resume |
| 0x0002 | `OP_HANDSHAKE_RESULT` | S→C | u8 recovered + u32 reconnect_ms + u32 start_seq |
| 0x0006 | `OP_PING` | C→S | u32 client_tick |
| 0x0007 | `OP_PONG` | S→C | u32 client_tick + u32 server_tick |
| 0x0008 | `OP_ACK` | both | (flags HAS_ACK only) |
| 0x0010 | `OP_JOIN_WORLD` | S→C | map_id, spawn pos, self entity |
| 0x0020 | `OP_MOVE_INPUT` | C→S | u32 input_seq, u32 keys, u16 facing, u32 client_tick |
| 0x0021 | `OP_MOVE_STATE` | S→C | u32 last_input_seq, pos3, vel3, u32 move_flags |
| 0x0022 | `OP_MOVE_CORRECTION` | S→C | pos3, vel3, u16 facing, u32 reason |
| 0x0023 | `OP_MOVE_CTRL` | S→C | u8 locked (counter semantics), u16 duration_ms |
| 0x0030 | `OP_ENTITY_ADD` | S→C | entity record |
| 0x0031 | `OP_ENTITY_REMOVE` | S→C | entity_id |
| 0x0032 | `OP_ENTITY_SNAPSHOT` | S→C | count + records |
| 0x0040 | `OP_CAST_SKILL` | C→S | u32 skill_id, u64 target_id, pos3 aim |
| 0x0041 | `OP_SKILL_PREPARE` | S→C | caster, skill_id, u32 cast_ms |
| 0x0042 | `OP_SKILL_CAST` | S→C | caster, skill_id |
| 0x0043 | `OP_SKILL_EFFECT` | S→C | caster, skill_id, targets + damage |
| 0x0044 | `OP_SKILL_REJECT` | S→C | u8 reason |
| 0x0045 | `OP_COOLDOWN` | S→C | skill_id, ready_at, u8 kind |
| 0x0046 | `OP_BUFF_SYNC` | S→C | full/incremental |
| 0x0050 | `OP_DOODAD_ADD` | S→C | id, template, name, tier, pos (container spawn) |
| 0x0051 | `OP_DOODAD_REMOVE` | S→C | id |
| 0x0052 | `OP_DOODAD_STATE` | S→C | id, state |
| 0x0053 | `OP_LOOT_OPEN` | C→S | id |
| 0x0054 | `OP_LOOT_LIST` | S→C | id, template, name, items[{slot,name,rarity,count}] |
| 0x0055 | `OP_LOOT_TAKE` | C→S | id, slot |
| 0x0056 | `OP_LOOT_RESULT` | S→C | id, ok, slot, item, reason |
| 0x0057 | `OP_ITEM_ADD` | S→C | name, rarity, count, inventory_size |
| 0x006E | `OP_ROUTINE_SYNC` | C→S | u32 param + size-prefixed payload (JX3-shape analog) |

### 5.4 Loot spawn rules (JX3-shaped, our data)

Mirrors the observed JX3 rule structure (anchors + semi-random subset + tier zones +
weighted tables + wind-up/timers):

1. **Anchors**: fixed set per map (hotspot clusters + scattered points), deterministic
   from `(seed, map_id)`.
2. **Match spawn**: each anchor activates with probability `active_ratio` (hotspots
   favored); position = anchor + gaussian jitter.
3. **Tier zones**: distance from center → zone 3 (rich) / 2 / 1 (poor); zone weights pick
   the container template (1st/2nd/3rd gear, weapon, meds, supply, secret).
4. **Phase escalation**: later phases shift weights toward higher tiers.
5. **Open roll**: contents rolled on first open from the container's weighted table
   (`LOOT_TABLES`), then delivered after `prepare_ms` (wind-up).
6. **Pickup**: range ≤ 5 m, once per player; inventory grant via `OP_ITEM_ADD`.
7. **Lifecycle**: container despawns when emptied; respawns on its template timer
   (`respawn_s`); state changes broadcast as `OP_DOODAD_STATE` / `OP_DOODAD_REMOVE`.

Reference implementation: `tools/netcode/reference/jx3_model.py` (`Spawner`,
`CONTAINERS`, `LOOT_TABLES`, `ZONE_WEIGHTS`, `Peer._loot_open/_loot_take`) with
15/15 smoke checks passing.

IDs ≥ 0x0100 reserved for content (inventory, quests, social, arena echoes).

## 8. Module layout (implementation)

```
server/
  net/frame.py        encode/decode, size table, validation
  net/session.py      serial/ack window, retransmit, resume store
  net/loop.py         net thread (selectors), publish to logic queue
  world/tick.py       30 Hz logic tick, 10 Hz snapshot fan-out
  world/move.py       movement rules shared with client (single source)
  world/combat.py     cast validation, cooldowns, buffs
  world/aoi.py        tile grid + subscriptions
  world/entities.py   entity records, spawn/despawn
  gateway/auth.py     login, session_key, ticket
client/
  net/session.py      same frame/reliability + prediction history
  predict/move.py     shared movement rules
  interp/remote.py    interpolation buffer
  combat/client.py    cast intent, animation predict, cooldown UI
```

Shared deterministic movement code is compiled/imported by both sides so
reconciliation drift stays small.

## 9. Hosting (answers the original VM question)

- gateway + game servers: **plain Linux VM / Docker**, no GPU.
- assets/patches: object storage + CDN, not the VM.
- one game-server process per map instance; ~200-500 players typical per process
  (JX3-scale would shard further, but a VM is the right unit either way).
- only the cloud-rendering variant would need GPU hosts; not this design.

## 10. Acceptance tests

1. **rtt:** inject 200 ms; own movement stays responsive (prediction), remote
   actors smooth (interpolation), corrections < 1 per 10 s while walking.
2. **loss:** drop 10% acks for 10 s; unacked messages retransmit, no duplicate
   effects (dedupe by `seq`).
3. **reconnect:** kill socket; client resumes with `resume_seq` inside 60 s and
   receives only missed state (no duplicate spawns).
4. **combat:** cast while moving with a move-forbidden skill → `SKILL_REJECT`
   `move_state`, no client-side damage; cooldown echoed by server.
5. **aoi:** 2 clients 300 m apart → no entity traffic; walk into range → add +
   snapshots begin.
