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

## Client integration + two-client smoke (slice 3, 2026-10-02)

- `client/NetClient.cs` (net48/C#5, on the shared rules) + `MiniJson` extended in
  `CameraSystem.cs` (arrays/nested objects for netcode payloads). Env-gated:
  `RC_NET=host:port` (off by default, canonical behaviour unchanged), `RC_NET_AUTH=1`
  enables local reconciliation snap (drift > 96 u) once the server shares terrain.
- Client wiring: input upstream at 10 Hz (`OP_MOVE_INPUT`, keys from WASD), server state
  downstream (`OP_MOVE_STATE`), AOI entities -> engine actors (`AddDummyModel("net_<eid>")`),
  removal on `OP_ENTITY_REMOVE`. Server `--spawn x,y,z` places new entities at the map spawn.
- **Two-client in-engine smoke** (server + `reborn_client_m2-a.exe` + `reborn_client_m2-b.exe`,
  RC_DEMO scripted movement, ~130 s each, no disconnects, no errors):

```
a: net: connect ok / handshake recovered=0 / join eid=1 / net entity add eid=2 name=82482b0f
   net actor add eid=2 handle=5943650872
b: net: connect ok / handshake recovered=0 / join eid=2 / net entity add eid=1 name=fac51f96
   net actor add eid=1 handle=1833106360
```

Both clients joined, saw each other through AOI and spawned the remote actor in the engine
scene; sessions stayed up for the full run (t=129 s each, movement logged).

**Known gap (next M2 slice):** the server still simulates on a flat plane with the reference
speed; authoritative movement needs the server-side terrain/collision model (the client keeps
its own position unless `RC_NET_AUTH=1`). Remote interpolation is snapshot-stepped (10 Hz).

## Authoritative-movement slice (2026-10-02)

- **Heightfield bake**: client tool mode `RC_BAKE_HF=<out.tsv>` (+ `RC_BAKE_HF_AREA`,
  `RC_BAKE_HF_STEP`) samples the game's own terrain loader into a text grid -
  `netcode/data/龙门寻宝_hf.tsv` (381x321 @ 10 u over x 20400-24200 / z 22000-25200,
  0.86 MB; re-baked 2026-10-02 so the grid covers the full held-W run path, not only
  the spawn +/- 1024 u). The headless server cannot init the game VFS, so the grid is
  baked by the client once.
- **Server**: `--heightmap <tsv>` (ground follow in the tick), `--speed <u/s>` (game run
  320), `--aoi <u>` (interest radius; the reference's 100 is meters - the game uses 5000 u =
  50 m, otherwise entities vanish after a few steps).
- **Client remotes**: 120 ms interpolation buffer (10 Hz snapshots -> smooth placement) and
  run/idle clips driven by interpolated speed (`KGModelCLR` per remote).
- **Reconciliation** (`RC_NET_AUTH=1`): rate-limited safety net (drift > 600 u, max 1 per
  500 ms). Exact convergence needs the server to run the client's full movement (turn model
  + object collision + per-frame integration) - that is the remaining M2 slice.

## Movement port complete (2026-10-02, this slice)

The server now runs the client's own movement block (`GameMovement.StepEntity`), so
prediction and simulation converge instead of fighting:

- **Wire input frame**: `MoveInput` carries `fx`,`fz` - the *exact camera-forward vector*
  the client's input block used that frame (`CameraSystem.Forward`: `(-cos yaw, -sin yaw)`).
  The server rotates the key bits with `Movement.WorldDir` (W = forward, A/D = right =
  `(fz, -fx)`), so no yaw-sign convention is guessed on either side. Resent on key change,
  camera turn > 0.001 or every 100 ms. `run` + a one-shot `jump` flag ride the same op.
- **Server movement**: `GameMovement.StepEntity` (turn model: heading = travel dir, turn
  rate pi, > 112.5 deg hard turn halves speed + turn step; slope block 70 u; ledge drop
  150 u; object/foliage collision via `ICollision`, which `client/FoliageCollision.cs` now
  implements; jump triple 90/11; gravity). Server links the client's FoliageCollision.cs
  and loads `--collision <dir> --mapname <map>` (`<map>_foliage_collision.bin` +
  `<map>_structure_collision.bin`; the client's `bin64\collision_data` has both).
- **Real-time tick**: the tick loop integrates the *measured* wall dt (5 ms poll, 0.25 s
  clamp) instead of a nominal 1/30 s. With the full movement the nominal loop actually ran
  at ~21 Hz, making the server ~29 % slower than the client in real time; snapshots stay
  time-based at 10 Hz.
- **Heightmap header fix**: `Heightmap.Load` required 8 header tokens and read `head[8-1]`;
  the bake writes 9 (`origin x z step s nx n nz m`) - the server had silently run without
  ground follow. Fixed (`head[8]`, 9 tokens).
- **Spawn alignment**: the smoke's server `--spawn` z was 24424 while the client spawns at
  24224, giving a constant ~200 u perpendicular offset. Aligned.

Evidence (`tools/proof/run_m2_auth_smoke.py`, 2026-10-02, 11 s held-W run ~3000 u):

```
server a final (20720,842,22251)   client a final (20722,842,22252)   -> 2.4 u apart
server b final (20724,842,22253)   (same run from the same spawn)
corrections: none on either client (drift never reached 600 u)
gates: Reborn.Server --selftest 8 PASS / Rules.Selftest 51 PASS
```

Remaining M2 follow-ups (not blockers): RMB turn while idle is not on the wire (server
yaw only changes while keys != 0), and jump input is a one-shot flag (no air-control
inputs yet).

Smoke (`tools/proof/run_m2_auth_smoke.py`, server + 2 clients, held-W runs):

```
a: connect ok auth=1 / join eid=1 / entity add eid=2 / actor add eid=2 / corrections ~1.3 s apart at 600 u
b: connect ok auth=1 / join eid=2 / entity add eid=1 / actor add eid=1 / same
```

No entity churn with AOI 5000, no correction storm, sessions stable.
