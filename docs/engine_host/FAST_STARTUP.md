# Fast startup — the 22 s shader-DB stall and the D7 override (verified)

Date: 2026-10-04 · Branch: `agent/predraw` (`#iso`) · Status: **VERIFIED**
Build: `reborn_client_predraw.exe` `git=eab4277` · Proof:
`proof/engine_host/startup_nodb_2026_10_04/`

## TL;DR

Engine init blocked ~22 s in a **blocking TCP connect** to the editor shader-build
database `10.11.10.102:1433` (Seasun LAN, unreachable), from
`KG3D_MaterialShaderManager::_initShaderUpload()` in `KG3D_MaterialSystemX64.dll`.
`RC_STARTUP=nodb` rewrites that IP literal to `0.0.0.0` in memory at DLL load, so
`connect()` fails immediately (`WSAEADDRNOTAVAIL`) and the engine takes its own
existing "server not ready" path:

| cell | `Init3DEngine` (client log) | engine `const time` |
|---|---:|---:|
| baseline (shipped) | **24 187 / 24 563 ms** | **24.375 s** |
| `RC_STARTUP=nodb` (sandbox 1×1) | **3 203 ms** | **2.844 s** |
| `RC_STARTUP=nodb` (full 8×8 map) | **3 094 ms** (+`LoadMap` 1 563 ms) | 2.8 s |

Spawn `(18991,962,33853)`, terrain, and per-region RGB fingerprints are unchanged;
`hitch` clean max 39 ms baseline vs 61 ms nodb (p95 27 both) — no gameplay stutter.

## Correction to the earlier diagnosis

`MINI_SANDBOX_CLIENT.md` §Startup breakdown attributed the ~22 s to the
single-threaded pre-draw (`PreDrawSetting.ini PreDrawThreadMaxNum=1`). **That was
wrong** — the master-switch log line simply sits immediately before the stall:

- Forcing `StartPreDrawShader` to 4 threads (`mov ebp,N` at RVAs `0x8CFF3F/47`)
  or to the engine's no-thread path (NOP at `0x8CFF64`) left init at
  24.2–24.6 s — the stall is not the predraw thread wait.
- CPU during the gap was ~45 % **of one core**, disk read avg ~0.14 MB/s, GPU
  mostly idle → a network wait, not shader compilation.
- `Get-NetTCPConnection` during the gap: `10.11.10.102:1433` in `SynSent`.

## Root cause evidence

| Evidence | Source |
|---|---|
| `10.11.10.102:1433 SynSent` during the init gap | `Get-NetTCPConnection -OwningProcess`, 2026-10-04 |
| `_isSqlServerReady`, `10.11.10.102`, `ShaderListUpload\KG3D_ShaderToDB.dll`, `_initShaderUpload`, `_AppendShaderList` strings | `KG3D_MaterialSystemX64.dll` (.rdata, GBK/ASCII) |
| connect sequence: `WSAStartup(0x202)` → `socket(AF_INET,SOCK_STREAM)` → `inet_addr("10.11.10.102")` → `htons(0x599)` → blocking `connect` → `select` → failure sets state 0 | disasm of the function at `KG3D_MaterialSystemX64.dll` RVA `0xAAA980` |
| IP literal at RVA `0x3D4518` = `"10.11.10.102\0\0\0\0"` | PE map + byte match |
| module guard | TimeDateStamp `0x6AA7BCE6`, SizeOfImage `0xAE7000` |

This is the editor's "append shader list to the compile DB" workflow (the same
routine ships in the game client DLL, but it belongs to the pak/shader build
pipeline; a player host must not depend on a Seasun LAN database).

## The override (registered deviation D7)

`native/startup_shim.cpp` → `bin64\startup_shim.dll` (built by
`native\build_startup.cmd`):

1. Client calls `RC_Startup_EarlyInit()` before `new KGEngineCLR()` **only when
   `RC_STARTUP` is set**; otherwise the DLL is never loaded and behaviour is
   byte-identical to shipped.
2. `LdrRegisterDllNotification` on `KG3D_MaterialSystemX64.dll` (or immediate
   apply if already loaded).
3. The shim **scans the module's readable sections for the exact standalone
   literal** `10.11.10.102` (byte-equal, followed by NUL) and overwrites it
   with `0.0.0.0`. No hardcoded RVA and no PE-stamp gate: an engine update that
   keeps the same server address keeps working unchanged. The PE
   stamp/size are only reported as `build=ok|mismatch` for diagnosis; if the
   literal is gone it refuses (`applied=0 site-not-found`), never guesses.
4. `VirtualProtect` → write → restore. **Data-only** (no instruction patch, no
   trampoline, no executable allocation).
5. Client logs `Startup: mode=nodb applied=1 sites=1 build=ok` after
   `Init3DEngine` (evidence: `reborn_20261004_235236.log`).

Why this is a deviation and not a game change: the engine already handles an
unreachable server — it skips the upload exactly the same way after the timeout.
The override only changes *when the failure happens* (instant vs ~21 s SYN
timeout). No gameplay, rendering, data, or network behaviour is altered; the
host simply stops waiting on an editor LAN service.

Kill switch: unset `RC_STARTUP` (or `RC_STARTUP=engine`).

Maintenance model (development reality): our own client rebuilds never affect
this — the target is the installed engine DLL, not our code. A game/MovieEditor
update that keeps the same DB address needs **no action** (content scan finds
the literal). Only if Seasun changes/removes the address does the shim log
`site-not-found` and stay on the shipped path; then re-run the diagnosis
(strings of `KG3D_MaterialSystemX64.dll`, `Get-NetTCPConnection`). Re-open
criteria: literal gone/changed, or a supported config that disables the
shader-list upload (prefer that over the shim).

## Reproduce

```powershell
# build
native\build_startup.cmd
set RC_CLIENT_EXE=reborn_client_predraw.exe
client\build_client.cmd

# run (sandbox), then read the log in bin64\reborn_out\
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
set RC_AUTORUN=120000
set RC_DEMO=1
set RC_SHOTS=8000,15000
set RC_STARTUP=nodb
C:\SeasunGame\MovieEditor\bin64\reborn_client_predraw.exe
# expect: Startup: mode=nodb applied=1 site=0x3d4518 ip=0.0.0.0
#         Init3DEngine=1 err=1 ms≈3100-3300
# engine log (zhcn_hd\logs\KG3D_Engine\<date>\): const time ≈ 2.8 s
```

## Rollout

- **Sandbox default:** `tools/sandbox/run_sandbox.cmd` sets `RC_STARTUP=nodb`
  (user decision 2026-10-04). After the merge, rebuild `reborn_client_mini.exe`
  from main so the exe carries the `RC_STARTUP` wiring; an older exe ignores the
  variable and stays on the shipped path.
- **Canonical `reborn_client.exe`:** ships with the wiring but no default
  (shipped 24 s path); opt in per run with `set RC_STARTUP=nodb`.
- **Rollback:** unset `RC_STARTUP` (or `RC_STARTUP=engine`) — no rebuild needed.

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| 22 s stall = blocking connect to 10.11.10.102:1433 | HIGH | SynSent sampling + disasm + CPU/IO profile |
| Fix removes ~21 s (24.2→3.1 s init) | HIGH | A/B logs + engine `const time` |
| Scene/visuals unchanged | HIGH | fingerprint sets in proof dir; spawn/terrain lines |
| predraw threads were not the cause | HIGH | forced 4-thread and skip A/B runs (no effect) |
| supported config to disable the upload exists | LOW (not found) | shader-manager strings / PreDrawSetting.ini |
