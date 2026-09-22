# Reference server + client (reborn netcode model)

Runnable, stdlib-only reference implementation of `docs/netcode/REBORN_SERVER_SPEC.md`,
which is itself modeled on the JX3 findings in `docs/netcode/JX3_PROTOCOL_SPEC.md`.

## Run

```powershell
.\.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py
```

Expected output: 10 x `PASS`, exit code 0. Covers:

1. handshake spawns a new session
2. join world
3. client prediction converges with server state
4. forward movement reaches the server
5. AOI entity add both ways (2 clients)
6. skill lifecycle prepare → cast → effect → cooldown
7. casting a move-forbidden skill while moving is rejected (`move_state`)
8. a dropped cast frame is recovered by serial retransmit
9. reconnect with `resume_seq` recovers the session
10. resumed entity keeps its position

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

## Deliberate simplifications (documented, not hidden)

- payloads are JSON; production should use the fixed-size table + typed primitives
  (`CoreNet::KNetBuffer`-style) from the spec
- movement is a flat-plane kinematic model; production swaps in the real
  capsule/terrain rules shared by client and server
- no compression/crypto layer yet (spec §5 covers the hooks)
- one connection per client; gateway is folded into the game server
