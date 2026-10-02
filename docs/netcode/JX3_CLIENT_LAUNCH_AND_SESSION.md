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
