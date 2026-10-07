# V3 plan — real JX3 client on the frozen isolated install (JX3ZHENCHUAN)

**V3** = the V2 stack (real `JX3ClientX64.exe` + launcher emulator + our local gateway/game
server) running against a **frozen, isolated copy** of the stock client instead of the live
`C:\SeasunGame` install. Same protocol work, pinned client build, clean log attribution,
and no dependency on the player's updating install.

## Track lineage

| Track | Client | Install root | Status |
|---|---|---|---|
| V1 | reborn engine-host client (C#) + our server | `C:\SeasunGame\MovieEditor` engine root | separate track |
| V2 | real `JX3ClientX64.exe` + our stubs | live install `C:\SeasunGame\Game\JX3\bin\zhcn_hd` | research line (live) |
| **V3** | same real client + same stubs | **frozen `C:\JX3ZHENCHUAN`** | this doc |

## Frozen install (JX3ZHENCHUAN)

```
C:\JX3ZHENCHUAN\
  ZHENCHUAN_BUILD.json                     build fingerprint + provenance + exclusions
  Game\JX3\bin\zhcn_hd\...                 client tree (stock names kept verbatim)
  Game\JX3\Pakv4\...                       pak store (clientconfig.ini PakDir=../../PakV4)
```

- Source: live install `C:\SeasunGame\Game\JX3` (selective copy; **no** install writes).
- Client: `1-5-0-9975` (`version.cfg`), exe TimeDateStamp `1790430791`.
- Verified copy: `Pakv4` = 2,264 files / 192.88 GiB (**byte-size exact match with the
  source store**); client tree = source minus the intended exclusions (research trees,
  logs/runtime caches, `interface\*#DATA` addon data, root `*.memory`).
- Kept because they are load-bearing: `clientconfig.ini` relative `PakDir`, the
  `zhcn_hd`/`Pakv4` directory names, `version.cfg`, `GameDoctor*`, `PakV4Manager`.
- Excluded: `SeasunDownloaderV2.4*` (research/downloader trees), `logs`, runtime caches
  (`CachedShaders`, `zsCache`, `minidump`, WebView2 profile, dxvk cache), addon/user data
  (`interface\JX#DATA`, `MY#DATA`, `SG#data`), root `*.memory`/runtime ini files.

## Running V3

Every runtime tool resolves the client root through `tools/netcode/client_root.py`:

```
RC_CLIENT_ROOT=C:\JX3ZHENCHUAN\Game\JX3\bin\zhcn_hd     (client root directly)
RC_V3_ROOT=C:\JX3ZHENCHUAN                              (outer root; suffix appended)
unset                                                   -> live install (V2 behavior)
```

Wrappers: `C:\jx3tmp\run_gateway_zhenchuan.cmd`, `run_gamestub_zhenchuan.cmd`,
`run_emul_zhenchuan.cmd`, `run_incident_watch_zhenchuan.cmd` (all export `RC_CLIENT_ROOT`).

Hard rules:
- **One raw client at a time** — the engine namespace (`MovieEditor.memory`) is fixed;
  V3 and the live/V2 client can never run concurrently.
- The stub cipher table is read from the **frozen** exe via `client_root.exe()` — the
  server constants always match the exact build being driven, even after the live
  install updates.

## Update / bump policy

The frozen copy never updates itself (the launcher/updater only knows `C:\SeasunGame`).
To move to a newer build deliberately: delta-copy the new `bin64` (+ changed paks) into
the frozen tree, refresh `ZHENCHUAN_BUILD.json`, re-run the offline gates, and re-derive
any RVAs/offsets that changed (see the V2 re-derivation plan). Keep the old frozen tree
until the new one passes validation.

## Status (2026-10-07)

- DONE: frozen copy + verification + marker; V3 worktree/branch `agent/v3`; `client_root`
  resolver + parameterized runtime tools (commit `e529861`); wrappers.
- DONE — **validation GREEN (2026-10-07 16:49)**: launching the frozen client with
  `RC_V3_ROOT=C:\JX3ZHENCHUAN` + `run_emul_zhenchuan.cmd` produced the full login → game
  connect → `id 4` → ApplyEnterScene → `id 188` bind flow (stub decrypts correctly with the
  cipher table read from the frozen exe), and a one-shot read shows `map=296 reg=0 id=1001
  state=7`, `player+0x60` bound. The frozen client wrote its own logs into
  `C:\JX3ZHENCHUAN\Game\JX3\bin\zhcn_hd\logs\` (DumpReport/Dumper/GameDoctorSDK/KGPK4 at
  16:49:24) — isolation and clean attribution confirmed. Client released after the run.
- OPEN: update/CDN endpoint handling (`preupdater.ini` hosts, `jx3v4*-miniupdate` CDN),
  optional stale-pak trim via `Pakv4\Trunk.dir`/`versionmap.cfg`/`garbage_v2.txt`, and carry
  the V2 movement/crash work onto V3.

## Reproduce

```powershell
# 1) frozen copy (from the live install; read-only on the source)
cmd /c C:\jx3tmp\run_zhenchuan_copy.cmd          # robocopy client + Pakv4 -> C:\JX3ZHENCHUAN
# 2) services against the frozen root
cmd /c C:\jx3tmp\run_serverlist_wd.cmd           # port 80
cmd /c C:\jx3tmp\run_gateway_zhenchuan.cmd       # port 3724
cmd /c C:\jx3tmp\run_gamestub_zhenchuan.cmd      # port 3725
cmd /c C:\jx3tmp\run_emul_zhenchuan.cmd 1800 emul_zhenchuan.txt
# 3) state read (one-shot, frozen-aware)
$env:RC_V3_ROOT="C:\JX3ZHENCHUAN"; .venv\Scripts\python.exe tools\netcode\read_bind_state.py
```
