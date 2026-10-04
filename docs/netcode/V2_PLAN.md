# V2 plan — run the real JX3 client (phased)

**V2** = host the real `JX3ClientX64.exe` for personal use, without modifying the client,
without anti-cheat bypass. V1 (the reborn engine-host client + our own server, `main`) is a
separate track and must not be mixed into V2 work.

## Goal / end state

One command starts: launcher emulator -> real client -> our gateway -> our game server ->
playable in-world session. Personal use only (AGENTS §1: never commercial, never distributed).

## Locked constraints (do not cross)

- Client install stays read-only; no client modification, no injection into the real client's
  process, no packet capture, no anti-cheat bypass. Research = copy/analyze + observe; the
  only writable process is our own probe child (transient, in-memory, no disk writes).
- Everything needed exists locally (block cipher, 814-SID protocol table, login model, module
  map). A server-side claim needs a client-side counterpart.
- Every phase: reproduce command + evidence artifact + V2-labeled commit.

## Phase overview

| Phase | What | Exit criterion | Status |
|---|---|---|---|
| P0 | Recon + probe harness | reproducible direct launch; single blocker identified | **DONE** |
| P1 | Startup gate (critical path) | client survives past the ~2.3 s WinMain timeout; module Initialize runs; `state_sub+0x18 != NULL` | **DONE (provisional: `config+0xe10=0`)** |
| P2 | Login + gateway stub | client passes login against our gateway | **IN PROGRESS (static-first)** |
| P3 | Game server stub (enter world) | client loads the world and holds the session | pending |
| P4 | Playable loop | walk around 5 min, no desync/disconnect | pending |
| P5 | Packaging / ops | one-command cold start to in-world | pending |

## P0 — Recon + harness (done)

- Launch chain, launcher handoff (PID-keyed shared memory + mutex + custom TEA; block is a
  client->launcher heartbeat), exit mechanism (WinMain startup-task timeout), gate located
  (`state_sub+0x18` platform object never created), protection map, 52 modules, event registry.
- Probe tools: `unwind.py`, `probe_unwind.py`, `probe_state_timeline.py`, `probe_queue.py`,
  `probe_wait_trace.py`, `probe_logpatch.py`.
- Evidence: `docs/netcode/JX3_CLIENT_LAUNCH_AND_SESSION.md` §1–§27.

## P1 — Startup gate (critical path)

Everything downstream (logs, window, CEF, login) waits on the same missing platform object:
the 52 modules' Initialize handlers are only *registered*, never called (§27).

- **P1.1 Static**: find the writer of `state_sub+0x18` — which step/handler creates the
  platform object and what condition fails without the launcher. Trace from the pump's
  success test (`state_sub[0x18]->vt[8](0)`) and the platform steps.
- **P1.2 Launcher-provided input inventory**: what the real launcher supplies that our probe
  lacks. Candidates: the launcher IPC (loopback TCP pairs client<->launcher/CEF, §2), the
  security/report handshake (block heartbeat + response, §10), inherited environment/handles.
  Observe a real launch read-only (connection table by PID, process tree, window timing).
- **P1.3 Launcher emulator v1**: our own program — start the client suspended, create the
  PID-keyed mapping + mutex, write and keep updating the block heartbeat, host the expected
  IPC endpoint; hold the session.
- **Exit**: client survives >10 s (past the 2 s timeout), module Initialize runs (window
  appears; the console/xlogv log path becomes usable now that it is ungated), timeline probe
  shows `state_sub+0x18 != NULL`.

## P2 — Login + gateway

**Status (checkpoint):** routing is live and proven. The client fetches its server list from
our local host (`.crc` version probe -> `serverlist.ini` download -> crc32 verification ->
cache `%TEMP%\Jx3\serverlist\zhcn.hd.<crc>.tab`); `乾坤一掷` is patched to `127.0.0.1:3724`.
Every login attempt now reaches our gateway stub, which proves the whole address path.
Observed: the client connects and **sends nothing for exactly 20.0 s** (then drops) — the
gateway protocol is server-speaks-first and the client waits for a valid framed server packet.

### P2 execution plan — static-first (locked)

Live capture is only used for the final validation, never to discover one step at a time.

1. **Framing (static)** — **DONE** (launch doc §45): wire frame = `[u16 LE total][payload]`,
   payload = total-2, `0xFFFF` extended form for large frames; send writes the prefix at
   `data-2`; max normal payload `0xFFDC`; optional payload transform flag `transport+0xC`.
   Packet dispatch: proto id = `payload[0]`, handler `[gwClient+0x250+proto*8]`, min size
   `[gwClient+0xa50+proto*8]`.
2. **Connect state machine (static)**: **DONE** (launch doc §46): `Login_ConnectGateway`
   -> `Connect 0x140185ED0` (state 0 -> 1, queue `RealConnectGateway` task; repeated call
   while busy sets cancel flag `+0x1268`); worker sets state 2; pump state machine
   `0x189630` (state 2 + flag clear -> `ProcessConnectState` -> 229-byte handshake ->
   `Send`). `Login_SetGatewayAddress` stores host at `gwClient+0x0`, port at `+0x20`.
   Probe `probe_gw_bp.py` verified the chain and same-instance connect; remaining: valid
   server opcode from `[gwClient+0x250]` + one clean UI login for the capture.
3. **Message set (static)**: recover the gateway protocol table (opcodes, sizes, handlers)
   from the registration writes to `gwClient+0x250`/`+0xa50`; decode
   `OnHandShakeRespond`, `OnSyncLoginKey`, account verify, role list, login-game layouts.
4. **Implement the stub conversation** offline: first server packet -> handshake respond ->
   account verify accept (admin/admin) -> role list -> login game -> `pcszGameServerIP`
   pointing at our game server. No live runs during implementation.
5. **One live validation**: single client run, one user login click, capture + compare
   against the implemented conversation. Exit: client reaches the role-list / enter-world
   stage against the stub.

Evidence so far: `proof/netcode/disasm/gw_*.txt` (gateway surface),
`docs/netcode/JX3_PROTOCOL_SPEC.md` §4.1, `JX3_CLIENT_LAUNCH_AND_SESSION.md` §44.
Token semantics (user decision #2) stays open until step 3 shows whether the client
verifies locally or forwards.

## P3 — Game server stub (enter world)

- **P3.1** Per-SID handler/size/layout RE for the enter-world path (static; sizes already
  extracted).
- **P3.2** Minimal S2C: enter-world accept, map/terrain load, self entity spawn, tick/heartbeat.
- **Exit**: client loads the world and holds the session (no disconnect) against our server.

## P4 — Playable loop

- **P4.1** Movement: client input -> server state -> echo/authority (mirror the M2 shared
  rules where the real client's behaviour matches).
- **P4.2** Minimal interaction (target/chat) and UI sanity.
- **Exit**: user walks around in the real client for 5 min with no desync/disconnect.

## P5 — Packaging / ops

- **P5.1** One-command launcher (starts gateway + game server + client through the emulator).
- **P5.2** Config, logs, docs, reproduce + regression smoke.
- **Exit**: cold start -> in-world with no manual steps; documented reproduce.

## Critical path and risks

- Critical path: **P1 -> P2 -> P3 -> P4**. P1 is the wall; nothing else can be validated
  before it.
- Main unknown: what the launcher actually provides (IPC/handshake) — if it is a signed or
  driver-backed handshake, the emulator may not be able to reproduce it (then: stop and
  report; no bypass).
- The security module (BlackProcess.tab / WhiteDLL.tab / DD63330 driver check) may reject a
  non-launcher process set — the emulator must match documented behaviour, never bypass.
- TP3 may block once the client proceeds further (then: stop; no bypass).
- Protocol field layouts (814 SIDs) are unknown but bounded by the extracted handler VAs and
  sizes; P3/P4 effort scales with the scope decision below.
- Protection denies external memory reads of the real client -> comparisons are external
  (process/network tables, timing) or use our own probe child.

## Open decisions (user)

1. P3/P4 scope: minimal walk-around vs fuller (combat/NPCs/interactions).
2. P2 token semantics: accept whatever the client forwards (if server-side) vs model an
   account flow (login.lua account channels).

## Reproduce (current phase)

```powershell
# server-list host: patched 乾坤一掷 -> 127.0.0.1:3724, serves .crc + .ini (template C:\jx3tmp\patched_serverlist.ini)
.\.venv\Scripts\python.exe tools\netcode\serverlist_host.py --port 80
# gateway stub: threaded, sends hello on connect, logs every byte
.\.venv\Scripts\python.exe tools\netcode\gateway_stub.py --port 3724
# client: production launch shape + config+0xe10=0 + console log flags
.\.venv\Scripts\python.exe tools\netcode\launcher_emulator.py --observe 3600 --cfg-e10 0 --log-flags
# direct-launch probe (P0/P1 evidence)
.\.venv\Scripts\python.exe tools\netcode\probe_state_timeline.py
```

Hosts entry required once (admin): `127.0.0.1 jx3comm.xoyocdn.com`.
Login drivers (`drive_login_click.py`, `watch_client_conns.py`) are test-only aids; the
static-first plan uses one user-driven login for final validation.
