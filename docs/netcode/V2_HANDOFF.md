# V2 handoff — real JX3 client through our gateway + game server

**For the next agent. Read this first, then `docs/EXPERIENCES.md`'s last 3 entries and
`docs/netcode/V2_PLAN.md`. Everything below is live-verified unless marked STATIC/DECODED.**

## CURRENT STATE (corrected 2026-10-07) — read this first

**The goal: the user wants to MOVE in the world.** The chain reaches state 7 and (after a long
game-logic loading phase) the C2S proto=5 confirm; the client then crashes at world entry.

- **Crash root cause PROVEN from the caught dump (2026-10-07):** every run that reaches the
  C2S proto=5 confirm dies `0xC0000374` at the world-entry teardown — the represent singleton
  (`JX3RepresentX64+0xF51298`) record vector holds the local avatar record **twice** (dump
  `C:\jx3tmp\crashes\1791356323_*.dmp`: `heap_failure_lfh_bitmap_corruption`, block
  0x1E3E9D06E00 = vector indices 8 and 9 share pointer 0x1E3E9D06E10; teardown chain
  `~SO3Represent -> ~KGameWorldHandler -> 0x920B00 -> free loop` -> double free). The earlier
  id-5 keepalive removal only removed the EARLY 26-91 s crashes; the confirm crash is unchanged
  (same DumpKey `BD677BCD...`). The duplicate is intermittent; `tools/netcode/watch_represent_array.py`
  polls the vector live (elems/dups on change).
- **Gate A (crash):** find what registers the local avatar record twice. Candidates: our
  remaining S2C data (id 4 / 187 / repeated 188 / id 3 keepalive) vs the client's own
  world-entry path. One variable per run, watcher armed: `GAME_BIND_REPEATS=0`, `GAME_SEND187=0`.
- **Gate B (loading, 5-28 min):** the panel polls the game-logic scene load
  (`GetSceneLoadingProcess(dwId)`, LoadingPanel.lua) and the client does this phase with **NO
  KG3D engine session**. The UGC map copy `data\UGC\binkp1\龙门寻宝` (4117 files, 975 MB) misses
  `foliage/blendmap/clusterinfo.json`, `bd/volumetricCloud/volumetricCloud.json`,
  `龙门寻宝_PFX_Runtime.json` (PakV4SfxExtract: NOT FOUND — authoring-only) and has 200/248
  zero-byte `.foliage` cell files. Authoring anything under `C:\SeasunGame` needs the user's OK.
- **KG3D log attribution hazard (2026-10-07):** `zhcn_hd\logs\KG3D_Engine` is SHARED with the
  user's `reborn_client_*` builds (their `reborn_out` logs show `asset_root=zhcn_hd`); every
  23:33+ KG3D session matched a `reborn_<date>_<time>.log` start. The sandbox/RayIntersection/
  missing-file logs there are the reborn clients' — do NOT attribute them to the V2 client.
  Verify by matching the KG3D log timestamp against `MovieEditor\bin64\reborn_out\reborn_*.log`.
- **id 4 map:** `GAME_ID4_MAP=296` (龙门寻宝); spawn: in-map coords (23334,24224,761). The old
  editor-camera spawn (54991,42845,2930) only matters for the reborn clients' sandbox crop.
- **Loop machinery (user rule: freeze/crash -> log reason -> kill -> fix -> retry):**
  - `tools\netcode\incident_report.py` (wrapper `C:\jx3tmp\run_incident_watch.cmd`): watches
    the client; on freeze (hung + static screen 90 s) or process exit it collects the reason
    (exit code, DumpReport summary, caught crash XML, client KG3D log tail, stub tail, state,
    screenshot) into `C:\jx3tmp\incidents\incident_<stamp>\reason.txt`, kills the client, exits.
  - `tools\netcode\crash_catcher.py` (`C:\jx3tmp\run_crash_catcher.cmd`): copies
    `bin64\minidump\*.xml|*.dmp` to `C:\jx3tmp\crashes\` before DumpReport deletes them
    (retries the locked .dmp; the 2026-10-07 dump proves it works).
  - `tools\netcode\watch_freeze.py`: standalone freeze watchdog (FROZEN/STUCK -> logs -> kill).
  - `tools\netcode\watch_represent_array.py` (wrapper `C:\jx3tmp\run_rep_watch.cmd`): live
    duplicate watcher for the crash vector.
  - `tools\netcode\game_server_stub.py` (`--rawlog` = full plaintext of every C2S packet).
  - **Launch hidden always**: `tools\netcode\launch_hidden.ps1 "<cmd>"` (plain WMI launches pop
    console windows - user complaint).
- **The V2 client's own logs**: `logs\DumpReport\`, `logs\Dumper\`, the crash XML/dmp; its KG3D
  engine logs land in the SHARED `zhcn_hd\logs\KG3D_Engine\<date>\` (see attribution hazard).
- **Live env (2026-10-07 00:3x):** services 80/3724/3725 up (stub wrapper
  `C:\jx3tmp\run_gamestub_v221b.cmd`, map=296, in-map spawn); V2 client cycling the loading
  phase; incident/crash/rep-array watchers armed. The user's reborn clients run concurrently
  (shared asset_root + disk contention) — expect variable V2 loading times.

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
   **S2C id 188 (0xBC, min size 7) = THE WORLD-BIND MESSAGE `OnSyncRoleDataOver`**:
   handler `0x14015FA70`, registered at `0x14011E973` (slot at +0x16A40 = **id 188**, size 7;
   corrected 2026-10-06 — the earlier "189" was one slot off; id 189 = OnSyncItemListInfo).
   Chain: local player lookup (`0x140174D10` by `[client+4]`) → scene lookup
   `0x140174E50(client, [client+0x14], [client+0x18])` → **`KScene::ValidateRegions`
   `0x1401830B0(scene, posX>>11, posY>>11, 1)`** (bootstraps the 3x3 cells, writes
   `[scene+0x20DCC/DD0]`, returns 1 unless cell alloc/init fails) → vtable `+0x8D8` notify →
   **guard `0x140173D90` = `KSO3World::AddPlayer`** → setter `0x14017BDD0` (stores
   `player+0x60` at entry) → state machine.
7. **Confirm**: the DLL state machine `ConfirmClientReady = 0x1803525F0` requires
   **`player+0x60 != 0`**, then sends **C2S proto=5 (11 B)**; we answer **S2C id 5** (sub0 =
   256-dword attribute array → `player+0x1020`) → **state 7** → LoadingComplete → world UI.

## 3. THE BLOCKER — RESOLVED (2026-10-06): the bind id was 188, not 189

Root cause: a dispatch **off-by-one**. `0x14015FA70` is registered in slot +0x16A40, which is
**id 188 `OnSyncRoleDataOver`** (handler log: "Sync role data over !"); its neighbours' min
sizes (8/7/19/56/59) match name-table ids 187/188/189/190/191 exactly. Our id-189 packet went
to `OnSyncItemListInfo` (min size 19) and never touched the bind.

- `0x1401830B0` = `KScene::ValidateRegions`: with arg4=1 it touches/requests the 3x3 cells,
  **writes** `[scene+0x20DCC/DD0]` (0x14018355A/0x140183561), ensures grid cell objects
  (`scene+0x7a8 + (y*128+x)*8`), and returns 1 unless a cell alloc/init fails. So the old live
  `-1` simply meant the handler never ran the call.
- Guard `0x140173D90` = `KSO3World::AddPlayer`; the setter `0x14017BDD0` stores
  `player+0x60 = scene` at function entry (0x14017bdec) — reaching the guard binds the player.
- Fix: the stub sends **id 188** after the enter-scene answer. Next: live validation (stub log
  must show the client's `proto=5` confirm → id-5 reply → state 7; reader
  `tools/netcode/watch_world_bind.py` shows `p60 != 0` / state 7).
- Historical live scene data (still valid): scene map at `client+0x5673D8` key (1,0),
  `[scene+0x64]=1`, dims 32; position lands correctly (live-verified).
- **Live proof (2026-10-06 run4)**: id 188 after ApplyEnterScene -> external read shows
  `player+0x60` = scene ptr, `player+0xFDC` = 7, pos (23334,24224) —
  `proof/netcode/bind_state7_run4.txt`, screenshot `proof/netcode/v2_world_state7_run4.png`
  (render still black: world data not served). Next: answer the world-data burst (C2S 0x1C0, 2,
  254x2, 229, 60, 90, 184, 109, ...) within the client's fixed ~220 s watchdog (exit code
  0xCFFFFFFF).
- **Driver lesson**: do NOT post_login/role_enter once the game window is up (keystrokes into
  the world window killed run3 in 26.6 s); check the gateway log for the login first.

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

- Client: none running (run4 exited at 18:11:41 via the ~220 s watchdog). Services still up:
  **80** (serverlist watchdog), **3724** (gateway), **3725** (game stub).
- Game-stub wrapper `C:\jx3tmp\run_gamestub_1.cmd` = `GAME_ID4_MAP=1`,
  `GAME_POS_X=23334`, `GAME_POS_Y=24224`, `GAME_POS_Z=761`; **id 188** wired after the
  enter-scene answer. Bind watcher: `C:\jx3tmp\run_bindwatch.cmd` +
  `tools/netcode/watch_world_bind.py` (external reader, hardened vs torn tree reads).
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

**MET (2026-10-06):** `player+0x60 != 0` and `player+0xFDC == 7` live (run4). Note: the
observed post-bind client packet is C2S id 2 (11 B), not "proto=5"; the stub's id-5 reply was
never required for state 7. Next milestone: serve the world-data burst (C2S 0x1C0, 2, 254x2,
229, 60, 90, 184, 109, ...) so the world renders and the session holds past the ~220 s
watchdog. Then commit + an EXPERIENCES entry (`docs/EXPERIENCES.md`, append at the bottom).
