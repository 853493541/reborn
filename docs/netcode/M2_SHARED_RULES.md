# M2 — shared rules library (C#) + parity gate

**Date:** 2026-10-02
**Worktree:** `Desktop\reborn-iso-m2-model` (branch `agent/m2-model`)
**Contract source:** `tools/netcode/reference/jx3_model.py` (10x PASS) +
`docs/netcode/REBORN_SERVER_SPEC.md`

## What this is

`netcode/Reborn.Rules/` — the single movement/combat/protocol rules source for the
reborn client and server (plan: "one movement-rules source for client and server").
Target `netstandard2.0` so both the net48 client and the .NET server can reference it.

| File | Contract mirrored from the reference |
|---|---|
| `Protocol.cs` | opcodes/flags, 15-byte header `<HBHHII` + size + payload, 30 Hz tick, snapshot cadence, ping/dead/reconnect/RTO constants, AOI range |
| `ReliableChannel.cs` | serial/ack window, retransmit at RTO (250 ms, max 8 tries, `FLAG_RETRANSMIT`), duplicate suppression, one-shot drop hook; injectable clock for deterministic tests |
| `Movement.cs` | `apply_input` — 8-direction normalized, `MOVE_SPEED = 5.0`; key flags `K_FWD/K_BACK/K_LEFT/K_RIGHT` |
| `Combat.cs` | skill table (cast/cd/move-forbidden/range), validation order and reject codes (`move_state=1`, `cooldown=2`, `range=3`, incl. the reference's unknown-skill -> range label) |
| `GameState.cs` | entity model (pos/vel/keys/locks/cooldowns), authoritative tick (locks freeze velocity, y clamped at 0), distance-based AOI |

Transport (asyncio-equivalent sockets, JSON payload codecs, session store) stays in the
server app — the next slice.

## Parity gate

`tools/netcode/reference/gen_parity_vectors.py` runs the **Python reference itself**
(with a fake clock for the channel) and writes
`proof/netcode/parity/rules_vectors.txt`; `netcode/Reborn.Rules.Selftest` replays every
vector against the C# rules and exits non-zero on any mismatch.

Coverage: movement 12, framing + roundtrip 8, reliable channel 11 (retransmits,
dup suppression, state), cast validation 8, authoritative tick 3 — **41 PASS, 0 FAIL**
(2026-10-02).

```powershell
.venv\Scripts\python.exe tools\netcode\reference\gen_parity_vectors.py
dotnet run --project netcode\Reborn.Rules.Selftest -- proof\netcode\parity\rules_vectors.txt
```

## Toolchain note (decision pending)

The plan's locked decision says the server is **.NET 8**; this machine has the
**5.0.408** SDK. Slice 1 uses it (library `netstandard2.0`, selftest `net5.0`, no new
dependencies). Installing .NET 8 is a new dependency — ask before the server slice
needs it; nothing here depends on the version.

## Next (slice 2)

1. Server app (sockets + session store + the tick loop) using `Reborn.Rules`.
2. JSON payload codecs for the messages in the reference (join/move/skill/AOI).
3. Client integration: connect the M1 client to the server (input -> `OP_MOVE_INPUT`,
   `OP_MOVE_STATE` -> reconciliation), then the two-client smoke.

## Reproduce (parity)

```powershell
# from the worktree root
.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py          # reference 10x PASS
.venv\Scripts\python.exe tools\netcode\reference\gen_parity_vectors.py
dotnet run --project netcode\Reborn.Rules.Selftest -- proof\netcode\parity\rules_vectors.txt
```

## Server app (slice 2)

`netcode/Reborn.Server/` — TCP listener + per-peer framed IO on `Reborn.Rules`:
handshake (16-byte key + resume_seq + resume flag), session store with 60 s resume,
30 Hz tick stepping `GameState`, 10 Hz `OP_MOVE_STATE` + distance AOI
(add/remove/snapshot), ping/pong, JSON payloads with the reference's keys.
`--selftest` runs an in-process two-client smoke.

```powershell
dotnet run --project netcode\Reborn.Server -- --selftest
# reborn server listening on 127.0.0.1:<port>
# 8 PASS, 0 FAIL (2026-10-02)
```

Smoke checks: handshake new session, join spawns entity, prediction converges
(drift 0.000), moved forward (z=5.00), AOI entity add both ways, ping/pong,
reconnect recovers session, resumed entity keeps position.

## Next (slice 3)

1. Client integration: connect the M1 client (`client/`) to the server — input ->
   `OP_MOVE_INPUT`, `OP_MOVE_STATE` -> reconciliation of the local prediction, remote
   entities -> scene actors.
2. Two-client in-engine smoke (two reborn clients, one server, AOI visible).
3. Move the client's table-driven movement (walk 96 / run 320 u/s, gravity) into
   `Reborn.Rules` so prediction and simulation share the exact model (M1.3).
