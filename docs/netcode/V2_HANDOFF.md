# V2 handoff — real JX3 client through our gateway + game server

**For the next agent. Read this first, then `docs/EXPERIENCES.md`'s last 3 entries and
`docs/netcode/V2_PLAN.md`. Everything below is live-verified unless marked STATIC/DECODED.**

## 0. Where you are

- Worktree: `C:\Users\Zhibin Ren\Desktop\reborn-iso-v2`, branch **`agent/v2`** (main checkout
  untouched; never push, never commit to main).
- Goal: the REAL unmodified `JX3ClientX64.exe` → launcher emulator → our gateway → our game
  server → playable in-world. Personal use only.
- The client install is read-only; the only documented writes are MovieEditor\bin64 build
  outputs + userdata configs (login.ini, scene_init_param.txt) + the user-approved UGC copy.

## 1. The user's working rules (they were explicit)

- **Work in LONG autonomous blocks.** Chain the full loop (launch → flow → read state → kill a
  frozen client → fix → retest) without stopping for a "next step" report. The user was angry
  about 1–2 minute bursts.
- **If the client is stuck/frozen** (not merely busy at loading), **kill it immediately** and
  continue. (`Stop-Process -Id <pid> -Force`.)
- **Before EVERY client launch, verify ports 80 / 3724 / 3725 are listening** — a dead
  serverlist host is the real-server leak vector (see §7).
- Static-first; live runs only to validate a milestone.

## 2. The chain status (what is proven live)

1. **Gateway** (live ✓): client hello → op2 handshake → op3 account verify (stub echoes the
   account, accepts anything) → op4 verify respond + op9 role list → op10 (role select) →
   op14 login key (`GAME_PORT=3725`, `127.0.0.1`).
   Gateway cipher: state0 `0xC9FFFFFF`, table `exe+0xA34530`, DIVISOR `0x162F`, add
   `0x2E6D23C1`, **state NOT persisted** (gateway variant).
2. **Game connect** (live ✓): 42-byte plaintext hello → client op1 → **S2C id `0x2FE`** respond
   (71 B; ServerName @+0x17, ReconnectTimeout @+0x37 → `mgr+0xE404`, three flags @+0x3B/0x3F/0x43
   all NON-zero). Game cipher `exe+0x7A26D0`: table lookup once, ks += `0x2E6D23C1`, **state
   persists: `state = state*0x1F + 0x8088405`**, both directions start `0xC9FFFFFF`.
3. **Keepalive**: any S2C packet within 12 s; frame ack-flag bit1 must be **0**; id-5
   keepalives work (sessions held 600+ s).
4. **S2C id 4** (343 B): name @+0xB (32 B → `player+0x88`), map @+0x2C → `client+0x14`,
   region @+0x30 → `client+0x18`, position X/Y/Z @+0x34/+0x38/+0x3C → `player+0x10/0x14/0x18`;
   sets `player+0xFDC = 4`. Live ✓ (map/pos land).
   **A repeated identical id 4 makes the client RST — send it ONCE.**
5. **Loading**: with map=1 the client runs its loading screen; the engine loads the sandbox
   scene (registry key **(1,0)**); the client then sends **C2S ApplyEnterScene (proto=3, 15 B)**
   ~5 s after the connect.
6. **Our answer** (stub `tools/netcode/game_server_stub.py`): S2C id 3 (time sync) → then
   **S2C id 189 (0xBD, min size 7) = THE WORLD-BIND MESSAGE**:
   handler `0x14015FA70`, registered at `0x14011E973` (slot → **id 189**, size 7).
   Chain: local player lookup (`0x140174D10` by `[client+4]`) → scene lookup
   `0x140174E50(client, [client+0x14], [client+0x18])` → **cell check `0x1401830B0(scene,
   posX>>11, posY>>11, 1)`** → vtable `+0x8D8` notify → **guard `0x140173D90`** (writes
   `player+0x60` via setter `0x14017BDD0`) → state machine.
7. **Confirm**: the DLL state machine `ConfirmClientReady = 0x1803525F0` requires
   **`player+0x60 != 0`**, then sends **C2S proto=5 (11 B)**; we answer **S2C id 5** (sub0 =
   256-dword attribute array → `player+0x1020`) → **state 7** → LoadingComplete → world UI.

## 3. THE CURRENT BLOCKER — the last gate

`player+0x60` stays 0: the id-189 chain exits **before** the guard.

- Live scene fields (scene map at `client+0x5673D8`, key (1,0)): `[scene+0x64]=1`,
  **`[scene+0x20DCC]=[scene+0x20DD0]=-1` (the scene's stored cell = UNSET)**,
  dims `[scene+0x790/0x794]=32`.
- Cell check `0x1401830B0`: `eax=[scene+0x64]=1` → `+2` → range loop at `0x14018313B+`;
  compares the requested cell with `[scene+0x20DCC/0x20DD0]` (-1,-1) → mismatch flag → scan.
- Both tested positions fail: `(16,16)` → cell (0,0); the sandbox spawn
  `(23334,24224,761)` → cell (11,11) — **the position lands correctly** (live-verified).
- **NEXT STEP: decode the check's loop result (`0x14018313B+`) and find what SETS
  `[scene+0x20DCC]`** (the scene cell activation — likely the engine's cell streaming / map
  data being live). Then the guard runs → `player+0x60` → confirm → state 7.

## 4. Decoded — do NOT re-chase (dead ends)

- The id-4's own "reset+bind" block (`0x14015C733..`) runs every id-4, but its bind call
  `0x140174970 → 0x1401780D0` with **edx=1 takes the early-return** (`0x1401783EC`) — it does
  NOT set `player+0x60`. The edx=0 setter path = **`KHomelandMgr::ChangeSkin`** (homeland).
- The "map manager" `client+0x1280` (lookup `0x140212A40`, key = dword at `node+0x1C`) is a
  per-session appointment-like set (observed `0,2,3,995` / `2,3,589,995`) — NOT the gate.
- id 10 (`OnSyncNewPlayer`) **exits for the local id** (equal-check → log → epilogue
  `0x14015AEEC`); it is for OTHER entities.
- The "sandbox redirect" scare was a misread: the KG3D log dir is shared; our client loads the
  real maps through the normal path (e.g. `data\source\maps\龙门寻宝\龙门寻宝.jsonmap` success).

## 5. Key addresses / structures (x64, EXE base + RVA)

- Client global `[base+0xA755B8]`; manager `base+0xA4C4F0`.
- Registry container `client+0x5673D8`: player map head @+0x20 (node key@+0x20, val@+0x28),
  scene map head @+8 (key pair @+0x20/+0x24, val@+0x28).
- `player+0xFDC` state; `player+0x60` scene; `player+0x10/0x14/0x18` pos; `client+0x14/0x18`
  map/region; `client+0x1B108` id-4 skip flag (= packet qword `+0xFA`).
- Player map lookup `0x140177EE0` (get-or-create), scene lookup `0x140174E50`,
  id-189 handler `0x14015FA70`, cell check `0x1401830B0`, guard `0x140173D90`,
  setter `0x14017BDD0`, state machine `0x1803525F0` (DLL).

## 6. Live environment state (as of this handoff)

- Client PID **36056** at the loading (Responding=False = busy-normal), session up.
- Services: **80** (serverlist watchdog), **3724** (gateway), **3725** (game stub).
- Game-stub wrapper `C:\jx3tmp\run_gamestub_1.cmd` = `GAME_ID4_MAP=1`,
  `GAME_POS_X=23334`, `GAME_POS_Y=24224`, `GAME_POS_Z=761`; id 189 wired after the
  enter-scene answer.
- hosts: `jx3comm.xoyocdn.com`, `infoc.xoyo.com`, `dumpinfo.xoyo.com` → `127.0.0.1`;
  the serverlist **cache** (`%TEMP%\Jx3\serverlist\zhcn.hd.2909632076.tab`) overwritten
  all-local.
- UGC data: `zhcn_hd\data\UGC\{binkp1,binkp4,admin}\龙门寻宝` (975 MB, user-approved).
- `scene_init_param.txt` (MovieEditor\bin64) has a 296 row added.

## 7. Hazards (hard-won — obey)

- **Real-server leaks (2×)**: a dead serverlist host → the client fell back to the real list
  (and once to the old cached list). Both are closed (all-entries patch + cache overwrite),
  but **check the 3 ports before every launch**; if a leak ever happens, KILL the client first.
- **Shared resources**: the KG3D logs and `MovieEditor\bin64` are shared with a concurrent
  agent (`reborn_client_cameratracks`) — sandbox loads in the logs may be THEIRS; avoid editing
  shared bin64 files while they build.
- **No namespace isolation** in the raw client: never run two clients at once; the user's real
  game must be closed.
- **Never Start-Process long-lived processes** from the tool shell (pipe-stall rule): launch
  via WMI: `Invoke-CimMethod Win32_Process Create -Arguments @{CommandLine='cmd /c
  C:\jx3tmp\run_emul.cmd 900 emul_x.txt'}`.

## 8. Reproduce the current run

```powershell
# 1) services (verify ports 80/3724/3725)
Invoke-CimMethod Win32_Process Create -Arguments @{CommandLine='cmd /c C:\jx3tmp\run_serverlist_wd.cmd'}
Invoke-CimMethod Win32_Process Create -Arguments @{CommandLine='cmd /c C:\jx3tmp\run_stub_p2.cmd'}
Invoke-CimMethod Win32_Process Create -Arguments @{CommandLine='cmd /c C:\jx3tmp\run_gamestub_1.cmd'}
# 2) client
Invoke-CimMethod Win32_Process Create -Arguments @{CommandLine='cmd /c C:\jx3tmp\run_emul.cmd 900 emul_next.txt'}
# 3) after ~55 s: login + role (scripted, no user mouse)
& C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe tools\netcode\post_login.py
& C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe tools\netcode\role_enter.py
# 4) watch C:\jx3tmp\game_stub_out.txt for: id4 -> enter-scene -> id=189 sent
# 5) read state: [base+0xA755B8] -> client; player via container+0x20; scene via container+8
```

## 9. Definition of done for the immediate milestone

`player+0x60 != 0` live → the client sends `proto=5` (seen in `game_stub_out.txt`) → our id-5
reply → `player+0xFDC == 7` → the loading screen ends → the world UI. Then commit + an
EXPERIENCES entry (`docs/EXPERIENCES.md`, append at the bottom).
