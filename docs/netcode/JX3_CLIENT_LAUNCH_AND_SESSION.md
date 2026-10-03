# JX3 client launch & session handoff — reality-check findings

**Date:** 2026-10-01 · **Method:** static disasm + read-only observation of a normal
launch (no client modification, no packet capture, no hooking).
**Question:** can we take the real client, cut its server connections, and run it
against our own server? (feasibility probe before any pivot)

## 1. Launch chain (observed, normal launch)

```
explorer.exe
  -> SeasunGame.exe            (launcher; pid observed 30612)
       -> JX3ClientX64.exe     (game; pid observed 3464; spawns cefrender.exe)
```

- **No command-line handoff**: the client's Dumper log for the run
  (`logs\Dumper\2026_10_01\Dumper_2026_10_01_18_09_47.log`) has **no `-c` field**
  (compare: probe runs with args show `-c "/u:probe /t:probe "`). WMI `CommandLine`
  is empty for both `JX3ClientX64.exe` and `SeasunGame.exe` (PEB scrubbed by the
  protection).
- The `/u:<account> /t:<token>` contract recovered from
  `KJX3Streaming::ParseCmdLine` (`0x14009baa0`, prefixes at `0x14009bacb`/`0x14009bac3`)
  plus operators `/c` (streaming), `/wg` (WeGame) belongs to the **streaming/WeGame
  operator paths**, not the normal launcher handoff.
- Direct launches (bare, `/u:/t:`, `/c /u:/t:`) **exit in ~2 s, code 0, before any
  network activity** (no DNS for JX3/XGSDK hosts, no TCP attempts) — the client is
  gated on launcher/session presence.

## 2. Live endpoints during a normal run

| Direction | Endpoint | Note |
|---|---|---|
| game session | `109.244.61.154:3724` Established | gateway/game port |
| web/sdk | `120.92.180.92:80`, `114.230.213.79:80`, `114.237.67.202:443` | HTTP/HTTPS |
| DNS | `jx3comm.xoyocdn.com`, `xgdata.xgsdk.com`, `infoc.xoyo.com`, `static-support.xoyocdn.com`, `jx3xlauncher.xoyocdn.com` | launcher + SDK + game comms |
| IPC | loopback TCP pairs (client ↔ launcher/cef) | session handoff candidate |

## 3. Login / session model (static)

- `login.lua` flow: remote server list → `LoginServerList` → `Login_SetGatewayAddress(ip,
  port)` → `Login_ConnectGateway`; account channels: XGSDK (HTTPS), WeGame, Streaming,
  TW/direct account+password (`Login_SetAccountPassword`).
- Server list is remote: `RequestRemoteServerList`, `GetServerListUrl`
  (`KJX3ConfigModule::RegisterLua` getter).
- `login.ini` per account stores `[LastLogin] ServerIP/ServerPort` (display-name server
  list in `recent_server.dat`; no IPs).
- Full S2C protocol table already extracted: `proof/netcode/protocol_table_s2c.tsv`
  (814 SIDs, handler VAs, packet sizes) + `protocol_names_s2c.tsv`.

## 4. Protection

`Dumper64.dll` (loads, "protection Init success"), `VMProtectSDK64.dll`,
`TP3Helper.exe` (TenProtect 3). No TP driver/service observed; TP did not block direct
launches.

## 5. Launcher handoff mechanism (second pass, 2026-10-01)

The launcher→client session handoff is a **PID-keyed named shared memory + mutex**, not a
command line (the real launch had no `-c`; WMI cmdlines empty).

- **Mapping**: `400BBBA7-F29F-4357-9B07-%04X-D62109852BD6` (PID hex), size **0x275C**
  (10076) bytes. Client side (`0x140101dc0`): `OpenFileMappingA(FILE_MAP_ALL_ACCESS,
  FALSE, name)`; if absent it **creates** it itself
  (`CreateFileMappingA(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, 0x275C, name)`),
  zeroes it, then `UnmapViewOfFile`.
- **Mutex**: `56992E93-3828-415E-AB04-%04X-33107B63107D` — client `CreateMutexA` +
  `WaitForSingleObject(INFINITE)` around the block copy.
- **Block crypto**: XTEA decrypt in place (`0x140101450`, 32 rounds, delta `0x61C88647`,
  key words `a0b1c2d3 e4f5a6b7 c8d9eafb 0c1d2e3f`); caller `0x140104b71` reads fields
  at +8/+0xc. Launcher must XTEA-**encrypt** the same layout.
- **Launcher side**: `XCommonX64.dll` `[XCommon] DetachProgram` → `OSUtil::_LaunchProgram`
  (`0x1800a82f0`): plain `CreateProcessW(appPath, args, NULL, NULL, inherit=FALSE,
  env=NULL, workDir, ...)` — no args in the observed run; the session is filled into the
  block afterwards (the launcher receives the PID via the out-param).
- `KGatewayClient::OnSyncLoginKey` carries `pcszGameServerIP` — the server tells the
  client which game-server IP to use; a private gateway controls this.

**Implication:** a launcher emulator (own program: start client → open PID-keyed mapping →
write XTEA block) is reproducible **without modifying the client**. Remaining unknowns: the
0x275C plaintext layout and the gateway protocol implementation.

## 7. Block format (static decode, 2026-10-01 third pass)

Block = **0x275C bytes = 16-byte header + 20 slots × 0x1F7 bytes**.

Header (after the client's in-place decrypt of the first 8 bytes):
- `+2` dword — `GetTickCount()/1000` at fill time; the parser fails unless
  `(GetTickCount()/1000 - value) <= 10` (freshness gate; a zero block always fails).
- `+8` dword — copied to `obj+0x108`; a caller checks it is **non-zero**.
- `+0xc` dword — entry count, clamped to ≤ 0x14; the client **zeroes it in the shared
  view after reading** (consume marker).
- `+0x10 + i*0x1F7` — slot payload (0x1E7 bytes) + 16-byte key at `+0x1E7`; parsed
  into a map keyed by the 16-byte pair (values copied into 0x208-byte nodes).

Crypto: the first 8 bytes are decrypted by a **custom TEA variant** (`0x140101450`)
whose exact 64-step update table was recovered by emulating the disassembly
(`proof/netcode/launch_block_cipher_table.txt`):

```
mx = (((x>>5) ^ (x<<4)) + x) ^ (key[e] + sum)     # x = the other word
v[target] -= mx                                    # decrypt; encrypt = reverse with +=
key = { a0b1c2d3, e4f5a6b7, c8d9eafb, 0c1d2e3f }; sum per step in the table
```

The table model reproduces the emulated function exactly on all test vectors and the
inverse round-trips. **Still only the decrypt direction exists** in
`JX3ClientX64.exe` / `JX3LogicEditOperationX64.dll`; the writer (which must encrypt)
remains unidentified.

Client behaviour: the parser is **polled** (caller `0x140100530`, timeout-guarded) and
the client gives up ~2.2–2.4 s after start. Launcher-emulator mechanics work
(suspended start → create PID-keyed mapping → write → resume), but a synthesized block
with a **cipher-verified fresh timestamp + `+8` set** still exits at 2.4 s — the exit is
therefore gated by something else (network/launcher presence), not the block header.

## 8. Verdict (2026-10-01, updated)

The earlier "blocked on launcher emulation" verdict is **superseded**: the handoff is a
reproducible shared-memory protocol with a hardcoded key (§5), so a launcher emulator can
produce a session **without modifying the client**.

What still stands between here and a playable private server:

1. **Block layout** — decode the 0x275C plaintext (fields at +8/+0xc and the string
   fields). Ground truth: capture one real block and XTEA-decrypt it (key known).
2. **Token semantics** — whether the client verifies the account/token locally or simply
   forwards it to the gateway (if server-side, any token works).
3. **Gateway + game protocol** — implement the real messages (814 IDs, sizes extracted;
   field layouts unknown). `OnSyncLoginKey.pcszGameServerIP` lets our gateway point the
   client at our game server.
4. **Legal posture** — private-server territory; no Seasun contact, no client
   modification, no anti-cheat bypass (stop if TP blocks).

Still a large project — but the feasibility answer is now **yes, without touching the
client**, not "impossible".

## Reproduce

```powershell
# static: launch contract
.\.venv\Scripts\python.exe tools\netcode\xref_string.py `
  "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe" `
  "KJX3Streaming::ParseCmdLine" --out proof\netcode\disasm\streaming_parsecmdline.txt
# observed: normal launch (user launches the game); read the run's Dumper log
# logs\Dumper\<date>\Dumper_<date>_<HH_MM_SS>.log -> no -c field; WMI cmdline empty
```

## 9. Gate hunt #5 (2026-10-01, fourth pass)

Facts from the probe matrix (all with the launch block pre-created by us):

| Probe | Result |
|---|---|
| zero block | client writes 8 random-looking bytes into the block at ~2.0 s, exits ~2.4 s |
| cipher-valid block (fresh ts, +8=1, count=0 or 1) | block left **untouched** (count not zeroed), exits ~2.2 s |
| fresh KGPK4 downloader running | no change (~2.2 s) |
| cwd = `C:\SeasunGame`, `Game\JX3`, version dir | no change (~2.4 s) |
| emulator process renamed `SeasunGame.exe` (parent name) | no change (~2.3 s) |
| live namespace during run | client creates `KG3D_Memory_Buffer_*`, connects `KGPK4-IPC-MQ-OUT-1`, spawns CEF (`CefView-Job-*`), then exits |

Ruled out: block content/cipher, downloader freshness, cwd, parent process name, the
`JDYinput` watchdog, network availability, self-relaunch. The exit is a deliberate early
termination at ~2.2–2.4 s that is independent of the block; the block is read (zero-block
marker) but a filled block is not consumed. Next probes: compare zero-vs-filled object
timelines, decode the launch-info entry map consumers, or capture one real filled block
(the writer is still unidentified).

## 10. Real block capture (2026-10-01, user-approved)

Captured during a real launcher launch (client reached in-game):

- The real block contains **only 8 nonzero bytes**, changing ~2x/s; everything else is zero
  (`+8=0`, `count=0`, no entries, no strings).
- Conclusion: the block is an **output/heartbeat channel** (client -> launcher), not a
  session container. Our synthesized header/entries model was over-built; the client
  neither needs nor consumes launcher entries.
- Feeding a changing 8-byte heartbeat does not keep the client alive in our probe
  (still exits ~2.8 s), so the exit gate is elsewhere.

**Security/report module discovered** (strings obfuscated with XOR 0xAD, built from dword
immediates): `settings\BlackProcess.tab`, `settings\WhiteDLL.tab`, module MD5/SHA256
hashing, machine UUID/MAC fingerprints, `SYSTEM\CurrentControlSet\Services\DD63330`
driver check, and report formats `AlwaysReportLogInV1;%s`, `;;MultiProcess counts:%d`,
`SharedMemoryError;k`, `;IntervalTime:k`. The client runs a periodic (16-iteration)
security check loop at startup.

**Open**: why the client exits ~2.3 s after start when launched outside the launcher.
Leads: the security/report handshake (block heartbeat + launcher response), the DD63330
service check path, and the launcher's inherited environment/session.

## 11. Exit mechanism located (2026-10-01, fifth pass)

WinMain = `0x1400e11f0` (called from the CRT at `0x14079ba05` with
`hInstance=image base, hPrevInstance=0, cmdLine, nCmdShow`).

- WinMain waits on a **startup task queue** via `0x14009d9a0(state, timeout_ms)`: it
  polls a virtual check method (queue object vtable `0x140953e50`, processor
  `0x14009d120`, task execute via `[task+8]->[rax+8]`) until the queue reaches a
  terminal state or the deadline passes.
- First wait: **10000 ms** (failure logs `PlatformBeforeLoad`). Second wait:
  **2000 ms**; on timeout it logs `WinMain` + `false` and leaves `r15d = 0`, so
  **WinMain returns 0** -> clean exit. Startup (~0.3 s) + 2 s timeout = the observed
  ~2.3 s exit.
- On success (`r15d = 1`) WinMain proceeds to the real UI/startup path.

So the ~2.3 s exit is a **startup-task timeout**, not a crash and not the launch block.
Next: identify which task stalls in the queue (the tasks enqueued between
`0x1400e1250` and `0x1400e13ac`, via `0x14009d860` / `0x14009c890` / `0x14009f6d0`) and
what completes it (likely the launcher/security handshake).

## 12. Startup-task gate — probe findings (2026-10-01, sixth pass)

- The startup queue is a **thread pool** (`0x14009d860` = `_beginthreadex` pool; worker
  loop `0x14009ce40`; task execute via `[task+8]->[rax+8]`; class tag `KStep_Async`).
- The probe client opens **no TCP connections** during its life — the stalled step is not
  a socket handshake.
- **New technique: stdout capture.** Launching the probe client with
  `STARTF_USESTDHANDLES` redirected to a file surfaces client/GameDoctor output. Captured
  for a failing run: `[%commonstartup%:174] Hash conflict!`, `Use jemalloc.`, and
  `KG3DEngineManager::UnInit ... (FALSE)` — the tag/strings live in GameDoctor
  (`GameDoctor.exe`/`GameDoctorSDK.dll`), i.e. this is a shutdown diagnostic, not proven
  causal. The engine DLL's own `string hash confliction, consider to change name` error
  exists but is unrelated to this line (exact `hash conflict!` is not in the engine).
- Still open: which startup step never completes. Next probes: trace the stage functions
  (`PlatformLoad` `0x14009f480` enqueues steps and waits itself), or read the probe
  client's pending task at ~1.9 s (memory read of our own child, needs user OK).

## 13. Step framework + CEF finding (2026-10-01, seventh pass)

- The startup step framework is `step_internal`: classes `Step`/`StepGroup`/
  `StepGroupFull`/`Async`/`Watchdog` plus anonymous `StepImpl<lambda_...>` instances,
  dispatched through an RTTI->handler registry at `0x140a8c1f0` (`0x14009c890` looks up
  `typeid(step)` and calls the handler). The steps are lambdas with no descriptive names
  — naming the stalled step statically is impractical.
- `PlatformLoad` (`0x14009f480`) enqueues a step and runs its own 10 s wait.
- **The probe client never spawns `cefrender.exe`** (10 ms poll over a full run), while
  the real client does (child of JX3ClientX64). A CEF job object appears but no browser
  process. So the startup stalls **before/at the login/browser-UI stage** — consistent
  with a launcher-provided session/URL being required.
- Next: runtime read of the pending step (memory read of our own child, needs user OK),
  or test whether a launcher-provided URL/session is the missing input.

## 14. WinMain wait-loop decode + CEF/job negatives (2026-10-01, eighth pass)

- WinMain (`0x1400e1383..0x1400e1428`): starts the state-A pool, builds the
  `game.startup` step group (`0x14009f6d0`; parent step vtable `0x140953e70`, 4 sub-steps
  ids 2..5 vtable `0x140953e00`, plus two marker nodes `0x140953dc0`/`0x140953de0` and a
  profiler node vtable `0x140953da0` tagged `"game.startup"`), then **loops**
  `wait(stateA, 2000)`: ret 1 = startup done -> stop pool -> global->vtable[0x38]() (enter
  game); ret 2 = call `0x1400ad460` and continue; ret 3 = keep waiting; **ret 0 =
  state idle for 2 s -> log `WinMain`/`false` -> return 0 (exit)**. In our probe the
  state stays idle - the group never becomes active.
- DBWIN OutputDebugString capture works (clean, read-only) and is a new runtime channel
  for the client.
- CEF: the launcher UI is `CefViewWing.exe` (5 procs under SeasunGame); the real game
  client spawned `cefrender.exe` as its child. Our probe spawns neither.
- Job-object hypothesis tested and ruled out: our shell is inside a Windows job
  (`in_job=True`) and CEF logged `Failed to assign current process to windows job
  object: 5`, but a WMI-launched probe outside the job (`in_job=False`) still exited at
  2.2 s.
- Dumper/DumpReport logs from probe runs are routine (no fresh minidump; newest crash
  XMLs are from 2025-12).

## 15. Runtime read (user-approved) + hash-conflict identified (2026-10-01, ninth pass)

- Runtime reads of our own probe child (suspend main thread + GetThreadContext + ReadProcessMemory;
  no injection/writes) show: the main thread is in **ntdll waits** for the whole boot, and the
  process thread count explodes (16 -> 44+) as the KGPK4 pool starts. At ~1.47 s the main thread
  is inside **`KG_InitPakV4FileSystem`** (Engine_Lua5X64.dll) called from exe `0x1400B3DE2`
  ("InitPackage"), executing in a module loaded after 0.72 s (KGPK4_FileSystemX64.dll range).
  The WinMain wait frame (`0x1400E13DF`) was never found on any thread at 0.5/1.0/1.4/1.8/2.05 s.
- `[%commonstartup%:174] Hash conflict!` (stdout) is **not** Lua: it is the engine's string-intern
  warning `ERROR: there is string hash confliction, consider to change name.` in
  KG3DEngineDX11EX64.dll (source tag `commonstartup.cpp:174`) - benign. `Use jemalloc.` and
  `KG3DEngineManager::UnInit ... (FALSE)` are shutdown diagnostics of an engine that never
  completed init.
- Real-block decrypt (our cipher): probe clients write a heartbeat counter
  `(uptime_seconds << 16) | ticks` (e.g. `C09D0000 -> C09F000B`, ~6 ticks/s); the real game's
  block held random-looking 8-byte values. `+8`/`+0xc` stayed 0 in every captured sample.
- Net: the missing input is consumed early (before/at PakV4 FS init); the exit is a clean
  teardown. The wait/pump loop appears to spend the ~2 s inside a step that waits (ntdll) with a
  timeout, then reports idle -> WinMain exits.

## 16. Wait trace (2026-10-01, tenth pass)

- Tool: `tools/netcode/probe_wait_trace.py` - spawns the client suspended, writes the block,
  resumes, then samples the main thread every 10 ms; when RIP is inside an ntdll wait stub it
  reads registers (R10 = handle), the timeout LARGE_INTEGER, the handle object (dup +
  NtQueryObject, done while the thread is suspended), and the stack (module-mapped).
- Observed: early boot is a **Sleep loop** (`NtDelayExecution`, same timeout pointer repeated,
  0.02-0.13 s); then short `NtWaitForSingleObject` catches; at ~1.33-1.37 s a stable
  **infinite wait** (`timeout = 0`) whose stack is
  `ucrtbase -> kernelbase!WaitForSingleObject -> engine_lua5x64.dll+0x12DDA8 -> ...` - i.e. the
  wait happens inside the **Engine_Lua5X64** async file-system layer, not the exe.
- Handle naming: duplicate-from-child works with `OpenProcess(PROCESS_DUP_HANDLE)` for some
  handles (e.g. 0x1B70) but the handles caught in-wait fail to duplicate (`ERROR_INVALID_HANDLE`),
  so the exact waited-on object is still unnamed; the wait-stub offset match may also be
  ambiguous between adjacent ntdll exports. Follow-up: use .pdata unwind walking to attribute
  the wait to a concrete frame before trusting the handle.
- No WinMain frame (`0x1400E13DF`) was found on any thread at 0.5/1.0/1.3/1.4/1.8/2.05 s; the
  exe never appears in the sampled wait stacks. Exit stays clean at ~2.2 s.

## 17. .pdata unwinder + full startup chain (2026-10-01, eleventh pass)

- Tools: `tools/netcode/unwind.py` (PE .pdata parser + x64 unwinder incl. UNW_FLAG_CHAININFO
  fragments) and `tools/netcode/probe_unwind.py` (suspends our own probe child, walks the main
  thread's stack with real unwind info, reads the WinMain frame's state object).
- Full chain observed (all live, no injection):
  `WinMain -> PlatformLoad (0x14009F480) -> wait (0x14009D9A0) -> pump (0x14009D120) ->
   game.startup step group -> KJX3BaseModule::Initialize step (0xA37D0..) -> InitPackage
   (0xB3DE2/0xB5D60) -> device/driver enumeration (SetupAPI/SPINF/windows.storage/drvstore)`.
  At ~1.9 s the chain is still live (WinMain at the wait-loop return 0x1400E13DF, step running).
- Exit path: the wait returns 0 -> WinMain logs "WinMain"/"false" -> shutdown: stop state
  (0x14009D7C0), module notifications (ids 0x12/0x13), a 6-iteration cleanup loop (0x1400E14D1),
  module vector teardown, a **2 s worker wait** (0x14010041E, `WaitForSingleObject(thread,2000)`
  with TerminateThread fallback), Dumper64 shutdown -> clean exit at ~2.2 s.
- **The gate**: the pump's success test is `state_sub[0x18]->vtable[8](0)` (return 1 = start the
  game). In our probe **state_sub[0x18] is NULL** and the done flag (sub+0x61) is 0 while the
  current step (sub+0x70) is still non-null; when the chain finishes with that pointer NULL the
  pump returns 0 -> WinMain exits. So the missing piece is the **client/game instance object**
  at state_sub+0x18, which the startup chain is supposed to create.
- Next: find the writer of state_sub+0x18 (the step/condition that creates the client object)
  and why it is skipped/fails without the launcher session.

## 18. State timeline + PlatformInitialize steps (2026-10-01, twelfth pass)

- Tool `tools/netcode/probe_state_timeline.py`: finds the WinMain frame once, then samples the
  state sub-object every 40 ms. Observed sequence (reproducible):
  - 0.3-1.86 s: queue empty (`+0x70`=0) - platform/protection/module-init phases run inside
    PlatformLoad's own state.
  - **1.90 s**: first step appears in WinMain's state A (`+0x70`=heap ptr) - the `game.startup`
    group starts.
  - 1.90-2.05 s: group runs; **2.05-2.08 s**: done flag `+0x61`=1, queue drained.
  - `sub+0x18` (status object) stays **0 the whole time** -> pump returns 0 -> WinMain exits at
    ~2.2-2.26 s.
- The `game.startup` sub-steps are **PlatformInitialize lambdas**
  (`PlatformInitialize::<lambda_ecfea5fd20b359a80114fb9a53b7b3e5>::operator()`, failure log at
  line 512) dispatched by id (2..5) through the type registry; runner `0x1400A0870` passes a
  descriptor `{0x140953EC0 ("KStep_Async" type desc), id, state-sub}` to the dispatcher
  `0x14009C890`.
- Registry map `[exe+0xA8C1F0]` is populated at runtime (count 0xB9 = 185 handlers), so handlers
  are registered; the object at `sub+0x18` is still never created.
- Conclusion: the missing input is consumed by the **PlatformInitialize steps** - they complete
  but do not produce the platform/game object the pump's success test requires.

## 19. Platform-init functor + registry (2026-10-01, thirteenth pass)

- The `game.startup` sub-steps are functor instances of type
  `.?AUInitialize@Module@KSO3ClientEvents@@` (`KSO3ClientEvents::Module::Initialize`);
  descriptor vtable `0x140953EC0` (entry `0x14009CC90`, data name string "KStep_Async"),
  RTTI COL `0x14096C180`, type descriptor `0x140A42300` (name at +0x10).
- The runner `0x1400A0870` builds descriptor `{vt=0x140953EC0, id=2..5, ctx=&state_sub}` and calls
  the dispatcher `0x14009C890`, which looks up the type in the registry at `[exe+0xA8C1F0]`
  (a std::map: head at `[map]`, size at `[map+8]`; nodes {left, parent, right, color/isnil,
  key@+0x20, value@+0x60}) and calls `[value]->vtable[0x10]`.
- Runtime: registry map size grows 44 entries (0.46 s) -> 185 entries (1.96 s); the map is
  populated throughout boot, so the handler registration is not the gate by itself.
- Client KGLog writes no files in the probe (`logs/JX3Client_2052-zhcn/<date>` dirs stay empty;
  `config/log.ini` has no file path), so the "[Initialize] ... failed" line is not observable
  through files or OutputDebugString.
- Open: the platform object (`state_sub+0x18`) is still never created - next is to instrument the
  dispatcher's return values / compare registry contents against a real launch.

## 20. Real-launch capture + module event system (2026-10-01, fourteenth pass)

- Real launch (user clicked): client pid 19552, parent launcher 13724. The real client is
  **protected**: `PROCESS_VM_READ`/`PROCESS_DUP_HANDLE` denied (err=5), toolhelp module snapshot
  returns 0 modules; only `PROCESS_QUERY_LIMITED_INFORMATION` succeeds. Live state comparison of
  the real client is therefore not possible without bypassing protection (not done).
- Launch block captured at spawn: launcher payload decrypts to `0x4D914010DD4DE3EB` (random
  64-bit session key), rest zeros - a key, not a boot gate (payload variants had no effect).
- Registry decoded completely: it is the **KSO3ClientEvents module event system**, 185 handler
  entries over 15 event types: Initialize 52, Finalize 52, RegisterLua 21, Update 14, Start 9,
  Message 8, Update@Background 7, Stop 6, Loaded 4, Start@Background 3, Reset 3, Stop@Background 2,
  Unloading 2, Unloaded 1, Loading 1. Each entry: handler object `{vt=exe+0x954C48,
  fn=exe+0xA3750, ctx}`, keyed by the RTTI name string (e.g. `.?AUInitialize@Module@KSO3ClientEvents@@`).
- Dispatch: vtable[0x10] = `0x1400A2830` -> `jmp [handler+8]` (thunk `0x1400A3750`), which allocates
  a 0x20 node `{vt=0x140954C80, handler_ctx, event_id, state}` and appends it (via `0x14009CF70`)
  to a container derived from the descriptor's state - i.e. dispatching "Initialize" **queues a
  per-module task**; those queued tasks are the work the state's pump then runs.
- Next: read the handler ctx objects to name the 52 modules, find which module's task creates the
  platform object (`state_sub+0x18`), and check why it does not in the probe.

## 21. The 52 client modules + queue snapshot (2026-10-01, fifteenth pass)

- All 52 Initialize handler owners resolved by RTTI (ctx vtable -> COL -> type descriptor name):
KJX3LocaleModule KJX3MemoryModule KJX3PathModule KJX3LogModule KX3DEngineModule KX3PakV5Module
KJX3ConfigModule KJX3ConsoleModule KJX3CoreDumpModule KJX3DllModule KJX3EcsModule KJX3StreamingModule
KJX3MultiInstanceModule KJX3WeGameModule KJX3OleModule KJX3WindowsApplicationModule KHotPointReader
KJX3WindowsViewModule KJX3WindowsMouseModule KJX3WindowsClipboardModule KJX3WindowsEmbededWebPageModule
KJX3WindowsDpiModule KJX3WindowsMultimediaModule KJX3WindowsCompositionModule KJX3WindowsLfhModule
KJX3PackageModule KJX3LoadingModule KJX3VideoCardScoreModule KJX3ZZQModule KJX3LaptopModule
KJX3LaunchUpdaterModule KJX3LuaModule KJX3ConvertResourceModule KJX3CpuUsageModule KJX3ProcessMemoryModule
KJX3FileRecordModule KJX3ReportModule KJX3SoundModule KJX3VoiceModule KJX3RenderModule KJX3AsyncTaskModule
KJX3FileModule KJX3VideoModule KJX3RepresentEventModule KJX3LogicModule KJX3RepresentModule KJX3UICoreModule
KJX3UIShellModule KJX3CommonEventModule KJX3LogicEventModule KJX3ImageModule KJX3MessageModule
- These are the client's own engine modules (KSO3ClientEvents event system: Initialize/Finalize/
  RegisterLua/Update/Start/Stop/...). Their Initialize callbacks are std::function-wrapped bound
  member functions (RTTI ?_Binder@...P8<Module>@@EAAHAEAUKSO3ClientBaseEvent@@@Z).
- Queue snapshot during the game.startup group (freeze-all-threads walk of state+0x70 chain):
  1.88 s and 1.96 s show exactly **6 pending nodes** (the group's 4 sub-steps + 2 markers,
  tasks' ctx = the state sub-object); 2.04 s done=1 and queue empty. The 52 module tasks are not
  in the pump queue - the Initialize dispatch executes them within the step (or via another list).
- Next (step 3): find which module's Initialize creates the platform object (state_sub+0x18);
  leading hypothesis: KJX3WindowsApplicationModule (window/app object) or KJX3LoadingModule.

## 22. Module handler contexts (2026-10-01, sixteenth pass)

- Each Initialize handler ctx = `std::_Func_impl_no_alloc<_Binder<...>>` whose vtable RTTI names
  the module (all 52 resolved). Fields: an inline small string at +8, heap pointers at +0x10+;
  related objects found in ctx fields: `KJX3ClientMessageHandler` (CoreDump ctx+0x50),
  `KJX3ClientLoadProgressImpl` (Loading ctx+0x28), a `type_info` (Render ctx+0x68).
- No raw code pointer appears in the scanned ctx range: the bound Initialize methods are
  member-function pointers that are likely **virtual** (stored as vtable indices), so resolving
  the concrete Initialize method requires reading the module object's vtable + index.
- Next for step 3: resolve the bound method (vtable index) for KJX3WindowsApplicationModule /
  KJX3LoadingModule / KJX3RenderModule, dump those Initialize methods, and find which one writes
  the platform object (`state_sub+0x18`) and under what condition it fails.

## 23. Module event dispatch + window path (2026-10-01, seventeenth pass)

- Event dispatch: each module's handler thunk (e.g. `KJX3BaseModule::Finalize` at `0x1400A3A00`)
  calls **`module->vtable[0x30](event_id)`**; the module vtable[0x30] is the per-module event
  handler. Observed: `KJX3WindowsApplicationModule` vt[0x30] = `0x1400DFD40` handles only
  `event_id == 3` (CloseHandle on module+0x70) and otherwise returns 1 - so the app object is not
  created by this event handler.
- The Initialize path: registry dispatch -> task `{vt=0x954C80, handler_ctx, ...}` -> run
  `0x1400A3FD0` -> `KJX3BaseModule::Initialize` lambda `0x1400A37D0(ctx)` -> builds a per-module
  step group and runs it via `ctx->vtable[0x20]` (`0x1400A3AA0` = run-group for most modules).
- Window creation: `CreateWindowExA` callers are in the app module region - `0x140089BD3`,
  `0x1400DB786`; message pump `PeekMessageW`/`DispatchMessageW` at `0x1400E023F`/`0x1400E0334`;
  `ShowWindow` callers include `0x1400DA54D`, `0x1400DB9E4`, `0x1400DC816`, `0x1400DF1F5`.
  Our probe never creates a window, so this path is never reached (or fails earlier).
- Still open: the writer of `state_sub+0x18` (the pump's success gate) is not yet identified;
  candidate paths: the app module's Initialize step group (window creation) or the platform code.

## 24. game.startup group structure + module globals (2026-10-01, eighteenth pass)

- WinMain flow re-read: wait(state B, 10 s) "PlatformBeforeLoad" -> destroy B -> PlatformLoad
  (`0x14009F480`) -> pool for state A -> build `game.startup` group into `[rbp+0x60]` -> wait(state A,
  2 s) loop. WinMain never writes state A `+0x18` - a step must.
- Module globals (runtime, all resolved): `0xA8C1B0`=KJX3WindowsApplicationModule,
  `0xA8C1C0`=KJX3CommonEventModule, `0xA8C1C8`=KJX3ConfigModule, `0xA8C210`=KJX3LogicEventModule,
  `0xA8C218`=KJX3LogModule, `0xA8C220`=KJX3MessageModule, `0xA8C260`=KJX3RepresentEventModule,
  `0xA8C1F0`=registry map. WinMain's success path calls `appModule->vt[0x38]` (`0x1400E0780`, app Run).
- Module vtables: `vt[+8]` = `0x1400A3730` (destroy: calls `[obj]->vt[0](obj,1)` and returns 0),
  `vt[0x20]` = `0x1400A3AA0` (run step group), `vt[0x30]` = per-module event handler
  (e.g. WindowsApplication `0x1400DFD40` handles only id 3 = CloseHandle).
- Queue walk during the group (freeze-all): the `game.startup` group is **3 pump-type steps
  (vt=0x953E70, task fields {vt, state, state+0x170}) + 3 markers (vt 0x953DC0/0x953DE0/0x953DA0)**;
  the queue does not advance for >0.1 s (first step running device enumeration), then done=1 ~2.05 s.
- ZZQ window: `CreateWindowExA` in `0x1400DB62D` registers class "ZZQ_WINDOW_CLASS" (KJX3ZZQModule);
  reached from Lua (`Lua_ZZQStart` binding at 0x1400DA1E0) - not from the event handler.
- Open: which step/handler should set `state+0x18`; candidate = the platform stage steps in the
  group (PlatformStartup/BeforeLoad/Load) or a module Initialize handler.

## 25. KGLog internals - why the client's logs are invisible (2026-10-01, nineteenth pass)

- `Engine_Lua5X64.dll!KGLogPrintf` (RVA 0xE52D0) decoded:
  - `priority & 7` indexes a **global level bitmask** at `Engine+0x16C440`; unset bit -> message
    dropped. Runtime value: `0x16E62E` (bits 1,2,3,5 set - error priority 3 IS enabled).
  - If the **sink global** (`Engine+0x177598`) is non-null, the sink object's vt[0] handles the
    message; if null, the message is formatted (0x800 buffer) and the **console path** requires a
    global **flags** word (`Engine+0x174020`) with bit 1 or 2 set; then GetStdHandle(STD_OUTPUT)
    + GetFileType==FILE_TYPE_CHAR -> WriteFile.
- Runtime checks: sink = NULL, flags = 0x0, mask = 0x16E62E. Our probe runs with piped stdout, so
  the console branch never fires; setting flags (VirtualProtectEx + write 0x6) with an inherited
  console still produced no output - the client likely detaches/has no valid stdout, or another
  TLS/rate-limit gate in the null path applies.
- Client log files: `logs/JX3Client_2052-zhcn/<date>` dirs exist (old) but no new files are
  written during the probe; the file logger is not active before the gate.
- Next: decode the null-path gates (`0x180127B52`, TEB/TLS check) or set the sink global to a
  target; alternatively instrument the module Initialize steps directly (vt[0x30]/group results).

## 26. Console/log mechanism decoded + lv.exe experiment (2026-10-01, twentieth pass)

- `KJX3ConsoleModule::OnInitialize` (`0x1400A5950`, event id 1): requires config flag
  `[configModule+0x224] != 0`, then calls `OpenXLogV` (`0x1400A5AA0`):
  - builds `<root>bin64\xlogv.exe` (literal at `exe+0x955229` = "bin64\xlogv.exe", ANSI,
    appended to the root which ends with a backslash), checks existence;
  - creates two pipes, `_spawnl(P_NOWAIT, <xlogv>, <xlogv>, "-o", <fd1_read>, "-i", <fd2_write>)`,
    then `freopen(%TEMP%\JX3Client.io)` and `_dup2` stdout/stderr -> pipe (client writes logs to
    the spawned viewer) and stdin <- pipe;
  - if the spawn succeeds: `[module+0x18]=1`, `freopen("CONOUT$","w+t",stdout)` + `KGLogAddOption(2)`.
- `bin64\xlogv.exe` is **not shipped** in this install -> the whole console/log mechanism is
  skipped; that is why no client logs are ever visible (files, console, debug output).
- Experiment: wrote a replacement log viewer (`C:\jx3tmp\lv.c` -> `lv.exe`, reads fd from `-o`,
  appends to `C:\jx3tmp\client_log.txt`), patched the in-memory literal to `C:\jx3t\lv.exe`
  (write succeeded) and set the config flag + engine log flags - the viewer was **not spawned**
  because the literal is appended to the root (relative path); the root is stored as **UTF-16**
  (`C:\SeasunGame\Game\JX3\bin\zhcn_hd\...` at e.g. 0xE6C89FF1FE) and `g_GetRootPath` converts it.
- Next: patch the UTF-16 root (or find g_GetRootPath's ANSI source) so `<root>bin64\xlogv.exe`
  resolves to our viewer; then the client's full log stream lands in `C:\jx3tmp\client_log.txt`.

## 27. Log-visibility probe: the log path is gated by the startup gate (2026-10-02, twenty-first pass)

Tool: `tools/netcode/probe_logpatch.py` (extends `probe_state_timeline`'s spawn/block/read
machinery; patches our own probe child only, transient, no disk writes).

- **Engine root is ANSI, not UTF-16** (corrects sec.26): `g_GetRootPath` (Engine_Lua5X64.dll,
  called through exe IAT `0x1407B84B8`) is a 9-instruction copy helper that copies the
  **ANSI** string at **`engine+0x170060`** (already the real root by 0.66 s) into the
  caller's buffer. The UTF-16 root strings found in the client heap are the client's own
  copies, not the engine's.
- Probe patches (all verified applied and held): engine ANSI root -> `C:\jx3t\`;
  `KJX3ConfigModule` (+0x224 = 1) so `KJX3ConsoleModule::OnInitialize` may run; engine log
  flags `[Engine+0x174020] |= 0x6`; and the fixed 16-byte viewer literal at
  `exe+0x955228` -> `\bin64\lv.exe` (viewer at `C:\jx3t\bin64\lv.exe`) as a reachability
  test that does not depend on the root.
- **Result: no viewer spawn, no `client_log.txt`, exit unchanged (~2.2 s).** The patch
  window is not the issue: the patches were applied from 0.66 s and re-applied through the
  group, and the config flag/root were read back as patched.
- **Root cause of the negative result**: the 52 modules' Initialize handlers are only
  *registered* (registry 44 -> 185 entries); their Initialize calls never run in the probe -
  no window (sec.23), no new log dir (sec.25), no CEF (sec.13), no console module. The
  `game.startup` group runs and completes, but the per-module Initialize dispatch (and with
  it `KJX3ConsoleModule::OnInitialize` -> `OpenXLogV`) waits on the same missing platform
  object (`state_sub+0x18`) as the pump's success test. Module Initialize is a consequence
  of the gate, not a step before it.
- **Consequence**: log visibility cannot be obtained before the startup gate. The gate is
  the single blocker for logs, window, CEF and login; the next work is to find what creates
  `state_sub+0x18` (candidates: the launcher IPC - loopback TCP pairs in sec.2 - or the
  security/report handshake in sec.10).

## 28. Log channel works + module events run (2026-10-02, twenty-second pass)

- **Viewer spawn contract corrected**: the client spawns `xlogv -i <fd1_read> -o <fd2_write>`
  (from the `_spawnl` arg setup at `0x1400A5C19`). `-i` is the input stream (the client's
  stdout, where logs are written); `-o` is the output stream (client stdin). The first lv.c
  had `-i`/`-o` swapped, so it read the write end and got `_read == -1` (EBADF) instantly.
  Fixed viewer source: `tools/netcode/xlogv_lv.c`; binary at `C:\jx3t\bin64\lv.exe`.
- **Channel works**: with the event-id and config-flag checks NOPed (probe patches), the
  viewer spawns and `C:\jx3tmp\client_log.txt` receives the client's stdout stream (proven
  with Dumper64/protection/GameDoctor startup+shutdown lines, ~2.3 KB per run).
- **OnInitialize success path decoded** (see sec.26): `OpenXLogV` returns nonzero on success
  -> `OnInitialize` calls `KGLogAddOption(2)`, which is exactly
  `or dword [Engine+0x174020], 2` (sets the console-output flag bit). The alternate path
  (`OpenXLogV` returns 0 and `module+0x18 == 0`) calls `AllocConsole` and continues only if
  it succeeds - a process that already has a console skips the whole success block.
- **Engine KGLog still not in the stream**: the console path needs a CHAR stdout; after the
  console module, stdout is the pipe (`FILE_TYPE_PIPE`) and the engine's early boot logs
  (0.3-1.5 s) are lost when `freopen` discards the CRT stdout buffer. Capturing engine KGLog
  needs a console-typed stdout (CREATE_NEW_CONSOLE + read the console buffer) or a later
  flush point.
- **Module events DO run** (corrects sec.27): an event-id sweep (patch only the immediate in
  `cmp edx, 1`; spawn happens iff the dispatched id matches) shows the console module's
  handler is called with **edx = 0 and edx = 1** - i.e. **Initialize IS dispatched** at
  ~1.2 s, inside PlatformLoad's module phases (two calls, matching the two viewer spawns).
- The sec.27 "Initialize never runs" conclusion was an artifact: the config flag
  `[configModule+0x224]` reads 0 at call time because the config module re-initializes the
  field when it loads config, overwriting our early write; the flag check is a policy gate,
  not a dispatch gate. With the flag check NOPed and the event check intact (EV_ID=1) the
  viewer spawns -> the whole OnInitialize path is reachable.
- **Corrected picture**: the startup chain does dispatch module Initialize (~1.2 s). The
  remaining startup blocker is still the pump's `state_sub+0x18` (platform object); the
  question shifts from "why is Initialize never dispatched" to "which module Initialize
  fails/what it needs" (no window per sec.23, no CEF per sec.13 - some module's init must be
  failing or skipping).
- Next: instrument the module Initialize results (which modules run, which return failure)
  and find the writer of `state_sub+0x18`.

## 29. The gate, exactly (2026-10-02, twenty-third pass)

Static decode of the wait/pump chain:

- `wait` (`0x14009D9A0`) calls `sub->vt[8]` with `rcx = &sub` (`state+0xE8`); `sub->vt[8]`
  (`0x14009D120`) is the **pump**: it walks the step chain at `sub+0x70`, calling each step's
  `[step+8]->vt[8]`; step return 0 = all done (`sub+0x61 = 1`), 1 = advance to the next step
  (`sub+0x70 = next`), 2 = keep waiting; the terminal handler is `0x14009D2E0`.
- **The gate** is `0x14009D2E0`: `if [sub+0x64] > 0 -> return 3`; else `[sub+0x60]=1`;
  if done (`sub+0x61`): `state->vt[0x10]()`, optional `[sub+0x58]->vt[0x10]()`, then
  `mov rcx, [sub+0x18]; test rcx; je -> return 0`; `call [rcx]->vt[8](0)`; nonzero ->
  **return 1 (startup done -> WinMain enters the game)**; zero -> return 0 (WinMain exits).
  So `sub+0x18` is a platform/client object whose `vt[8]` answers "ready".
- **Timeline (5 ms polling of the real fields)**: `sub+0x18` stays 0 from 0.56 s through
  2.05 s; `sub+0x70` (step) is non-null only 1.88-2.05 s (the `game.startup` group);
  `sub+0x61` (done) = 1 at 2.05 s; after that the object is torn down (the field then shows
  garbage code addresses at 2.21 s). So the group runs and completes, but nothing ever
  creates `sub+0x18` - the writer is skipped or fails silently.
- Next: find the writer of `sub+0x18` - candidates: the group's step processors
  (`0x1400A0870` runner / dispatcher `0x14009C890`) or a module Initialize result handler;
  a hardware write-watchpoint on `sub+0x18` during 1.8-2.1 s would identify it exactly.
