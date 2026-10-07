# V2 — world visibility + movement chain (research map)

**Goal chain:** enter world (**DONE live**: id 188 → state 7) → character visible / world rendered →
movable (walk around). This doc is the static-first map for the remaining chain; each question gets
a decode target + evidence path. One live validation per conclusion (V2_PLAN rules).

## UPDATE 2026-10-06 (late session, live) — world renders; movement still gated

### Root cause of the "stuck at state 7" stall (FIXED)
`KPlayerClient::ProcessPackage` (0x140168740) walks a receive buffer packet by packet:
- fixed-size protocols: advances by the **registration-table size** (`[mgr+id*4+0x17F28]`);
- var-size protocols (table = -1): advances by the **u16 packet length at +7**
  (`movzx edi, word ptr [r14+7]`).
Our frames were malformed: id 3 sent 19 B (table 11), id 188 sent 11 B (table 7), id-5 keepalives
had length word 0 → the loop never advanced → infinite reprocessing + KGLogPrintf spam → the main
thread never pumped messages ("ghost window", input dead, variable lifetime, exit 0xCFFFFFFF).
**Fix (stub): exact frame sizes + the +7 length word for var-size ids.** After the fix the client
is responsive in state 7 and the **world renders** (proof: `proof/netcode/v2_state7_fixed_sizes.png`,
fingerprint mean #71847D, terrain regions).

### World registration fix
id-4 packet `+0xDF` (qword) → `player+0xEC8` = the global role id. Zero makes
`KSO3World::AddPlayer`'s 0x140178730 log "Player GlobalID Error" and skip the world-map
registration. Stub now sends `GAME_GLOBAL_ID` (default 1001 = the gateway role global id);
live: `player+0xEC8 = 0x3E9`, world gid map present.

### Input gate pinned (2026-10-06 late): the world UI never gets SetMainPlayer

The UI object (`client+0xA755C0`, JX3UIX64.dll) vtable slots (found via .rdata):
**+0x2E0 SetMainPlayer, +0x2E8 SyncRoleDataBegin, +0x2F0 SyncRoleDataEnd**.
- id 4 fires SyncRoleDataBegin (EXE 0x14015C7ED, in the reset+bind block); id 188 fires
  SyncRoleDataEnd (handler tail). Both fire — so the role-data events are not the gate.
- **SetMainPlayer (+0x2E0) is called by the scene setter 0x14017BDD0 (0x14017BEE8) and by
  0x140178730 (0x1401786D1) — but only on the setter's SUCCESS path, which requires the
  position validator 0x1403D5220(cell, player) to return non-zero.**
- 0x1403D5220 walks the cell's terrain sub-regions: `cell+0x20` = sub-region pointer array,
  `cell+0x28` = index table; index = `minorY*64 + minorX`; the sub-region holds the terrain
  height words ([+4]/[+6] << 6) validated against the player Z ([player+0x18]).
- **Live: the player's cell (11,11) sub-region entry for minor (25,53) is uninitialized garbage**
  (`0x3FDA000000010112`) — the cells were created empty by `KScene::ValidateRegions`; the bind
  only *requests* async cell loads (0x1403d8a00) and they never complete for this scene.
  So the validator fails -> the setter returns 0 (bRetCode 0x78c) -> SetMainPlayer never fires
  -> the world UI/input layer never activates -> WASD/ESC/click produce nothing.
- Repeat-bind experiment (stub repeats id 188 x4, 8-10 s apart): cell data stays uninitialized,
  input still dead. The fix must make the scene cells actually load (map load pipeline / the
  scene's region streamer), not repeat the bind.

### Movement still gated (open)
With the world rendering and the client responsive: SendInput W/A/D/S (game foreground, verified
fg == hwnd), PostMessage WM_KEYDOWN, ESC/C keys, mouse right-drag — **no C2S 0x1E8, no position
change, no UI reaction**. The gate is inside the client's gameplay/UI initialization, not the input
transport. Next suspects (in order): (1) the world-entry data exchange (the burst) must be answered
before the HUD/gameplay layer activates; (2) a specific S2C world-entry message still missing
(candidates: id 8 OnSwitchMap well-formed, id 10/5 data); (3) the local character/represent not
created (no avatar to control).

## 0. Confirmed facts (2026-10-06)

### Wire / protocol
- World bind = **S2C id 188 `OnSyncRoleDataOver`** (handler 0x14015FA70); sends → state 7.
  Proof: `proof/netcode/disasm/id_dispatch_registrations_14011E900.txt`, live
  `proof/netcode/bind_state7_run4.txt`.
- Post-bind client burst (observed, `C:\jx3tmp\game_stub.log`): C2S 0x1C0 (DoNavStop?), 0x0002,
  0x00FE x2, 0x00E5 (DoApplyAllGlobalCounter), 0x003C (DoAddFellowshipRequest), 0x005A, 0x00B8
  (`DoRemoteLuaCall`, carries `On_QiYu_*` / `On_Recharge_*` / `OnClientAddAchievement*` strings),
  0x006D x11 (632 B role-data), 0x0077, 0x008F, 0x00D8, 0x00F4, 0x0065, 0x00D3, 0x00E2.
  Names from `proof/netcode/c2s_protocol_catalog.tsv` (call-site → protocol id).
- Movement wire ids (from sender disasm):
  - C2S **0x1E8 `DoMoveExteriorRequest`** (DLL 0x1801756F0): `[u16 0x1E8][word seq @+0xB]`
    `[byte mode @+0xD][payload from +0xE, filled by 0x1807417F0]`, send 0x1801ACDA0.
    Stub logs it as `proto=232`.
  - C2S jump family `DoCharacterJump` (DLL 0x1801708A0 area): carries `[player+0x2FC]`
    `[player+0x340]` + self pos X/Y/Z.
  - C2S **0xBF `DoMoveViewPointRequest`** (0x1801759E1).
  - S2C **id 13 `OnMoveCharacter`** (33 B, entity @+7) = other-entity broadcast.
- Post-bind world registration: guard `0x140173D90` = `KSO3World::AddPlayer` calls
  `0x140178730` (world global-id map `client+0x5673C8`, "Duplicate GlobalRoleID" guard).
  Proof: `proof/netcode/disasm/postbind_addplayer_scene.txt`.

### Input path
- The 3D world input is **async key state**: `GetAsyncKeyState`/`GetKeyState` imports in
  `JX3RepresentX64.dll`, `KG3DEngineX64.dll`, `KG3DEngineDX11EX64.dll`,
  `KG3DEngineAdapterX64.dll`, `KGUICocosX64.dll`. **Posted WM_KEYDOWN does not move the
  character; scripted `SendInput` is required** (tool `drive_move.py`).
- Represent-DLL key consumers found: 0x1803DDDC8 (modifier keys 0xA0-0xA3 + a timer compare),
  0x180B17C80 (keys 0x45/0x51 → flag vector `[rbx+0x25D90..]`). The W/A/S/D move handler was
  not among them — it goes through the key-binding layer.
- Movement sender callers are **not statically reachable** (no direct calls, no VA table refs) —
  they are invoked through runtime dispatch (vtable/Lua/command layer). Do not chase one hop at
  a time; decode the dispatch layer instead (see Q2).

### Live-run lessons (hard-won)
- The client **may or may not auto-login** (saved account `binkp1`); check the gateway log before
  posting. Never post input once the game window is up (killed a session in 26 s).
- Focus for SendInput needs the ALT+AttachThreadInput sequence + verification; if focus fails,
  do not send (input would go to another window).
- The session lifetime is **variable** (26 s .. 476 s) — not a fixed watchdog; find the exit
  cause (Q4). `watch_world_bind.py`'s background polling can hang on torn tree reads during the
  id-4 mutation; one-shot reads (module import + `player_lookup`) are reliable.

## 1. Research questions (decode order)

### Q1 — How does the local character load (self avatar)?
- Which id-4 fields feed the self appearance (field map: +0xD6/+0xDA/+0xDF/+0xAE →
  `proof/netcode/game_field_maps.tsv`), and where is the self represent created?
- Does the bind (id 188 → `KSO3World::AddPlayer`) create the self character entity, or does the
  client need an id-10-like spawn for self? Compare the id-10 handler (0x14015A120, other
  players) with the self path (`0x140178730`, `player+0xEC8`).
- Represent-side entry points exist in JX3RepresentX64.dll: `HandleSyncCharacterData`,
  `HandleSyncCharacterRepresentBaseInfo`, `OnSyncRepresentCharacterList` (names from
  `proof/netcode/symbols_JX3RepresentX64_net.txt`). Locate their RVAs (net symbol map / string
  xrefs) and trace who feeds the LOCAL player.
- Evidence target: `proof/netcode/disasm/self_character_*.txt` + a live read of the self entity
  pointer (`player+0xEC8`?) and its represent state.

### Q2 — What does the client expect in order to move?
- Find the movement command dispatcher: the C2S message ids 0x1E8/0xBF/jump are produced by the
  DLL's sender functions; find the **command/action layer** that calls them (JX3 uses a command
  table / Lua bindings: `interface` key bindings → command ids → KPlayerClient methods). Search
  the client + represent DLL for the command ids feeding 0x1E8 (the `mov` command path) rather
  than the sender callers.
- Preconditions in code: player state (7), scene bound (`player+0x60`), self entity present,
  camera present, UI/loading-panel state. Decode the guards around the move-intent update.
- Server expectations: does the client require an ack/reconciliation for its own move
  (S2C 13 for self, id 25 `OnSyncMoveState`, id 22/23/24)? Decode the move reply handling.
- Evidence target: `proof/netcode/disasm/move_command_*.txt`; live: keys → `proto=232` in the
  stub log + `player+0x10/14` change.

### Q3 — Why is the world render black (scene activation)?
- The bind created scene cells (`KScene::ValidateRegions`); what turns the scene into the active
  rendered world (camera creation/attachment, scene render module, cell `[+0x44]` loaded flags)?
- Check what the client's render path needs after state 7 (KG3D scene/camera selection), and
  whether a server message (id 8 `OnSwitchMap`, or scene/appearance data) is required.
- Evidence target: `proof/netcode/disasm/scene_render_activation.txt`; live: screenshot
  fingerprints after each candidate.

### Q4 — Session exit cause (26 s .. 476 s)
- The client exits `0xCFFFFFFF`; stub sees the socket close. Find the exit path (engine fatal /
  game-transport state machine / Lua error auto-exit). Search for the exit-code producers and the
  game-transport watchdog constants; check whether unanswered burst replies accumulate into a
  fatal state.
- Evidence target: `proof/netcode/disasm/session_exit_*.txt`; live: correlate stub log silence
  windows with the exit.

### Q5 — Minimal world-data set (UI/HUD unblock)
- Decode the `DoRemoteLuaCall` (0xB8) **reply** format from the client's Lua remote-call handler;
  identify which of the burst requests gate the HUD/session (vs. cosmetic data).
- Evidence target: `proof/netcode/disasm/lua_remotecall_reply.txt`.

## 2. Method

1. Static decode per question (dump_va/xref_va + data scans), claims carry RVA + confidence,
   dumps into `proof/netcode/disasm/`.
2. Only after a question is answered: one live validation run (ports verified, WMI launches,
   scripted input only, kill frozen on sight), evidence = stub log + one-shot state reads +
   screenshot fingerprint.
3. Update this doc + `V2_PLAN.md` + `docs/EXPERIENCES.md` per result.

## Reproduce (current chain, unchanged)

```powershell
# services (verify 80/3724/3725 listening first)
cmd /c C:\jx3tmp\run_serverlist_wd.cmd ; run_stub_p2.cmd ; run_gamestub_1.cmd   # via WMI
# client + login (auto-login may happen; check gw log before post_login)
cmd /c C:\jx3tmp\run_emul.cmd 900 emul_next.txt                                  # via WMI
tools\netcode\post_login.py
# bind happens on the client's ApplyEnterScene; stub sends id 188
# state read (one-shot): python -c "import sys; sys.path.insert(0, r'...\tools\netcode'); ..."
```
