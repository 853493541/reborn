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

## 5. Verdict (2026-10-01)

Taking the real client offline is **not a cheap pivot**. It requires, in order:

1. **Launcher emulation** — reproduce the `SeasunGame.exe` → client session handoff
   (IPC/loopback; new RE on the launcher).
2. **Auth stub** — satisfy XGSDK account verification offline (`xgdata.xgsdk.com`).
3. **Server-list interception** — replace the remote list source.
4. **Protocol implementation** — 814 messages; sizes known, field layouts unknown.

All four are needed before one map loads, plus protection layers, and it would require
changing the locked rules (launcher emulation, protocol work). The current engine-host
plan stays the cheaper and cleaner path.

## Reproduce

```powershell
# static: launch contract
.\.venv\Scripts\python.exe tools\netcode\xref_string.py `
  "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe" `
  "KJX3Streaming::ParseCmdLine" --out proof\netcode\disasm\streaming_parsecmdline.txt
# observed: normal launch (user launches the game); read the run's Dumper log
# logs\Dumper\<date>\Dumper_<date>_<HH_MM_SS>.log -> no -c field; WMI cmdline empty
```
