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
| P2 | Login + gateway stub | client passes login against our gateway | **DONE (live-verified 2026-10-05)** |
| P3 | Game server stub (enter world) | client loads the world and holds the session | **BIND MET LIVE 2026-10-06 (id 188 -> state 7); world-data set + session hold next** |
| P4 | Playable loop | walk around 5 min, no desync/disconnect | pending (movement ops identified) |
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

**Status (checkpoint 2026-10-05):** the whole entry chain is decoded and live-verified up to
the loading screen.

### Done (live-verified)
- Gateway login chain (P2): handshake op2 -> verify op3 -> verify respond + role list op9 ->
  role select -> op10 -> login key op14 -> client moves to the game server.
- Game transport: 42-byte hello, stream cipher (state 0xC9FFFFFF, LCG `state*0x1F+0x8088405`),
  frame `[u16 id][u8 flags][u16 serial][u16 ack][u32 field] + payload`; ack flag bit1 must be 0.
- Handshake respond: S2C id **0x2FE** (71 B; ServerName @+0x17, ReconnectTimeout @+0x37 ->
  `mgr+0xE404`, flags @+0x3B/+0x3F/+0x43). Live proof: `mgr+0xE404 = 30`.
- `id 4` OnSyncPlayerBaseInfo (343 B): name @+0xB (32 B -> `player+0x88`), map @+0x2C and
  region @+0x30 (-> `client+0x14/0x18`), position X/Y/Z @+0x34/+0x38/+0x3C ->
  `player+0x10/0x14/0x18`; sets `player+0xFDC = 4`. Live proof: `client+0x14 = 1` (sandbox
  map), player inserted, state 4.
- Scene registry (`client+0x5673D8`): the reborn sandbox map registers as key **(1, 0)**
  (dims 32..64, 4 loaded cells). The engine loads `C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s`
  at the loading screen.
- Loading/confirm chain (static + LoadingPanel.lua): the loading panel polls the engine scene
  load; at 100% it runs EndLoading -> **ConfirmClientReady** (= DLL state machine 0x1803525F0)
  -> `DoClientConfirmReady` sends the C2S 11-byte confirm (observed `proto=5 len=11`) ->
  state 4 -> **state 7** (0x180352700) -> LoadingComplete (event 7) -> LOADING_END -> world UI.
- Post-entry request: `KPlayerClient::DoApplyEnterScene` = C2S id 3, 15 B (`[client+0x788]`
  at +0xB); S2C id 3 = time-sync reply (server timestamp at +7 -> `client+0x28EF0`).
- `id 10` is for OTHER entities (local id is rejected by design; state 7 for the local player
  comes from the state machine).

### Corrected (2026-10-06): the world-bind is **S2C id 188 (0xBC) OnSyncRoleDataOver**, not 189
The previous "id 189" was one dispatch slot off (registration sizes 8/7/19/56/59 = ids
187/188/189/190/191; handler 0x14015FA70 = id 188, log "Sync role data over !"). Stub now sends
188; live validation pending. See EXPERIENCES 2026-10-06 (root cause) + proof/netcode/disasm/.

### Remaining (next static targets)
1. **S2C id 5** (per-player world data after the confirm; min 15 B): sub0 -> 256-dword
   attribute array -> `player+0x1020` (parser 0x140327680; type 0 = 256 dwords, types 1..3 =
   [count][dwords]); sub1 -> other players (local-id compare -> notify); sub2 -> vtable notify.
   Stub already replies with a zero array (frame 1039 B).
2. **World data set**: ids 10/11/12/13 (players/npcs/doodads/moves), id 8 (map switch),
   id 5 attrs — decode the minimal spawn set the HUD/scene needs.
3. **Movement (P4 preview)**: C2S `DoCharacterJump` (0x14/0x4E/0x0E/0x12),
   `DoMoveExteriorRequest` (0x1C), `DoMoveViewPointRequest` (0xBF); S2C id 13 OnMoveCharacter.
4. **Live test window**: requires the user's real client closed (raw client has no namespace
   isolation — EXPERIENCES 2026-10-05 hazard entry).

- **P3.1** Per-SID handler/size/layout RE for the enter-world path (static; sizes already
  extracted) — DONE for ids 1..54 (`game_protocol_layouts_live.tsv`).
- **P3.2** Minimal S2C: enter-world accept, map/terrain load, self entity spawn, tick/heartbeat
  — IN PROGRESS (stub: hello, handshake 0x2FE, id 4, id 5 reply; pending: the world data set).
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
