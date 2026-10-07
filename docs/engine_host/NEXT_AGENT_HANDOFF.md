# Next-agent handoff — Gate 1 (real `CreateRLScene`) / RL table list

Last updated: 2026-10-06 (end of a very long session). Read this top to bottom
before touching anything. `docs/EXPERIENCES.md` (2026-10-06 entries) has the
blow-by-blow; this file is the working map.

## 0. TL;DR — what to do next

The RL table task chain now runs through the game's own code (builder +
runTasks runner) and dies inside the **Engine_Lua5X64 file open**. The single
next job:

1. Probe the lua file-system root/prefix globals in-host:
   - `lua+0x1709C0` (the pak-path prefix string used by the pak path builder
     0xB3710) — log its value AND its string content.
   - Check what the host's `SetRoot` calls (`lua+0xB5400` / `lua+0xB5220`) and
     `InitPak` (`lua+0xCC2D0`) actually set; the lua path formatter expects the
     base path to END WITH A BACKSLASH (`%sbin64\%s`), and the host passes
     `rootA` without one.
2. Fix the root/prefix (or the pak flag path), re-run, and watch for
   `[host] registerTasks` / `runTasks` / `tableLoad` hooks + the tables probe
   `[main+0x210]` becoming non-null.
3. When `[SO3Represent+0x210]` (m_tabCommon) is set, KRLScene::Init line 212
   (weather) passes and Gate 1 can proceed to its checkpoint: the real
   `CreateRLScene` completes + `GetRLScene(2)` non-null through the game's path.

If the lua route dead-ends, the fallback is documented at the end (fab the
tables / the offline editor-stack map compile) — but exhaust the lua file
layer first: the chain is one step away.

## 1. Where the work lives — worktree + branch (READ FIRST)

**Main checkout (do not do feature work here):**
`C:\Users\Zhibin Ren\Desktop\reborn` (branch `main`, shared by other agents).

**This task's worktree (do ALL work here):**
`C:\Users\Zhibin Ren\Desktop\reborn-iso-skillv2-sandbox`

**This task's branch:** `agent/skillv2-sandbox` (local only).

- Setup pattern (already done; for reference / if the worktree is ever missing):
  from the main checkout run
  `git worktree add "../reborn-iso-skillv2-sandbox" -b "agent/skillv2-sandbox" main`.
- Verify you are in the right place before editing (all four must match):
  `git worktree list` (shows the worktree + branch),
  `git branch --show-current` -> `agent/skillv2-sandbox`,
  `git log -1 --oneline` -> a `Client:`/`Docs:` commit from the 2026-10-06 session,
  `git status --short` -> clean.
- History note: this worktree/branch was once accidentally merged into
  `agent/item1-completion` (commit `ec9e3a1`) and then reverted (`a8b37bc`);
  the worktree was deleted and **restored** at commit `c1869a2` (with the
  session's uncommitted edits preserved). If you see a stale merge or a missing
  worktree, that is the story; do not re-merge.
- Rules (AGENTS §2): commit small and often with `Area: summary` messages;
  **never push to origin**; **never merge to main**; never delete the worktree;
  never touch another agent's worktree. Do not commit generated/binary
  artifacts (`bin64/`, `samples/`, `*.bin`, `*.pss`, `*.t2`, `.venv/`).
- Shared resources: `bin64\camera_shim.dll` is shared by all clients (rebuild it
  only from a worktree current with main; a stale-branch shim reintroduces the
  D6 crash for everyone). Engine runs are namespace-exclusive: this host uses
  its own namespace; do not run two clients in the same namespace.

**Build/run (from the worktree root):**
- Build: `native\client_host\build_client_host.cmd` -> `native\client_host\out\client_host.exe`
- Run (PowerShell; the host reads these env vars):

```powershell
$env:RC_HOST_ROOT = "$env:TEMP\opencode\skillv2\client_root"
$env:RC_HOST_MAP  = "data\source\maps\龙门寻宝_s\龙门寻宝_s.jsonmap"
$env:RC_HOST_SHOT = "$env:TEMP\opencode\skillv2\host_exeNNN.png"
$env:RC_HOST_RLLOADER = "1"; $env:RC_HOST_LOGIC = "1"
$env:RC_HOST_EXE = "1";     $env:RC_HOST_EXE_NOINIT = "1"
& "...\native\client_host\out\client_host.exe" *> "$env:TEMP\opencode\skillv2\host_exeNNN.out"
```

- The window is OFF-SCREEN by default (visible to the engine, invisible to the
  user). `RC_HOST_SHOW=1` shows it. A watchdog force-exits after
  `RC_HOST_MAXSEC` (default 240 s). Long KEEP runs hit the game protection.
- Read the log with e.g.
  `Select-String host_exeNNN.out -Pattern "frame60|runTasks|new task|fault|line 212"`.

## 2. The current Gate and where the chain stands

Gate 1 = the real `CreateRLScene` (rep+0xB0B5C0) completes and
`GetRLScene(2)` (rep+0x924B) is non-null via the game's own path.

Chain status (all in `native/client_host/client_host.cpp`, frame60):

1. Phase A/B done long ago (logic boot, `SO3Represent::Init -> 1`, map load
   725 objects, resource manager, shadow descriptor fix).
2. **The exe's own `KJX3RepresentModule::Initialize` (exe+0xBC150) COMPLETES**
   (`exit -> 0x00000001`) through the game's dispatcher (exe+0xBC6A0, state 3).
   To get there the host:
   - calls exe+0xAF4A0 -> creates the KJX3LogicModule (stored at exe+0xA8C208);
   - creates the four subsystem modules 0xB2910/0xB72F0/0xBF440/0xC54D0 with
     caller storage + calls their OnInitialize = **vtable slot 5**
     (0xB2CE0/0xB7D20/0xBFA00/0xC5DB0);
   - fabricates exe globals: 0xA8C1C8 (zeroed config whose +0x18 = a stub whose
     vt[0x80]() returns g_ifUI), 0xA8C1E8 (+0x18 = g_ifXLogic), 0xA8C1E0 (UI
     shell module with +0x60 = the JX3UIX64.dll handle);
   - patches the Initialize's post-init block away: 0xBC4DA -> 0xBC5F7.
3. The host's own Param + `SO3Represent::Init(Param) -> 1` queues **5 tasks**
   into the fabricated stepCtrl's inner list (`stepA`; list fields
   +0x70/+0x78/+0x80). The table-loader task = vtable `rep+0xC99D30`, its
   invoke = slot 1 (`rep+0x3E59C0`).
4. The host invokes that task through `g_taskInvokeStub`:
   `mov rax,[rcx]; mov rax,[rax+8]; jmp rax` with **rcx = the task, rdx = the
   stepCtrl** (the wrapper reads the stepCtrl from its arg2 -> its rdx-save ->
   `[rbp+0x38]` -> the builder's arg4).
5. `param+0xA8` = `[exe+0xA8C208 + 0x18]` (the KJX3LogicModule **sub-object**,
   vtable exe+0x952B20; its slot 3 = exe+0x98A20 = the table-source getter).
6. The timed wrapper (rep+0x3E3D90) runs -> calls the game's own task-list
   builder (rep+0x8261F0) -> **completes without fault**. The builder pushes
   its register/run tasks into the **stepBuf's own list** (sc+0x70), NOT the
   stepA list.
7. The host re-walks `sc+0x70` and invokes the new tasks with the same stub ->
   **the game's own runTasks runner (0x80E360 -> 0x80B8C0) runs**.
8. The runner: opens `SkillCasterModel` via the rep fs (works) -> lua
   `g_OpenIniFile` (Engine_Lua5X64 export 0xBBA30; the rep IAT 0x109A020
   resolves to it correctly) -> `g_OpenFile` (0xB2F50) -> `KG_OpenPakV4File`
   (0xCC670) -> branches on the pak flag `lua+0x1729C0`:
   - flag 0 -> the loose path (`g_OpenAloneFile` 0xB2EA0 -> the open 0xB1C70);
   - flag 1 -> the pak path (`0xB4570` -> the path builder `0xB3710`).
   Both paths currently end in a **wild call/AV** (a 64KB-aligned address
   outside every loaded module; the VEH prints `(module?)`; in one run it
   happened to land in nvcuda64's range). The lua fs callbacks
   0x170030/0x170040/0x170048 are all set and valid.
9. **Current blocker**: that wild call inside the lua file layer. The pak path
   builder 0xB3710 copies `prefix (r9 = [lua+0x1709C0]) + name` into a buffer;
   a wild prefix/root is the prime suspect (the host's SetRoot passes `rootA`
   with no trailing backslash while the lua formats `%sbin64\%s`).

## 3. The next job — concrete probes (in order)

1. Extend the existing lua probe block in frame60 (search for
   `[host] frame60: lua=` in `client_host.cpp`; it already logs the base, the
   fs callbacks and the pak flag) to also log:
   - `*(void**)(lua+0x1709C0)` and the string it points to (`%.200s`);
   - the same for any nearby root strings (dump 0x170000-0x170060 as qwords);
   - the values after the host's SetRoot/InitPak calls (they run earlier in
     main — find `SetRootFn` / `InitPakFn`).
2. Hook the lua's `0xB3710` entry (use the host's `installInlineHook` pattern;
   the first 15 bytes: `push rbx; mov rbx,rdx; mov r10,r8; movzx edx,[r8+1]`
   = 1+3+3+4 = 11 bytes — pick len 15) and log `rcx/rdx/r8/r9` (the buf, size,
   name, prefix) to see which pointer is wild.
3. If the prefix is empty/wild, fix the root the lua way: re-check the host's
   `SetRoot` calls — the base path likely needs a trailing backslash and/or a
   different SetRoot variant; the game's own exe calls these from its startup.
   Grep the lua for what writes `lua+0x1709C0` / `lua+0x1729C0` (stores to the
   globals; the pak flag was 0 in-host until we set it).
4. After a fix, the success markers are:
   - `[host] registerTasks enter` (hook on 0x80B6A0) and/or `tableLoad lambda`
     (hook on 0x80B9C3) firing;
   - `[main+0x210]` (m_tabCommon) becoming non-null after the task run
     (`[host] frame60: after task run [main+0x210]=...`);
   - no `KGLOG_PROCESS_ERROR(g_pRL->m_TableList.m_tabCommon) at line 27` and no
     `line 212 in KRLScene::Init` in the log.
5. Then re-check the CreateRLScene result (`real CreateRLScene -> ...`,
   `GetRLScene(2) -> ...`) — that is Gate 1's checkpoint.

## 4. Instrumentation already in the host (do not re-add)

- Trace hooks (installed in main, near the exe module setup):
  - `hookRegisterTasks` on rep+0x80B6A0; `hookRunTasks` on rep+0x80B8C0;
  - `installTableLoadHook` on rep+0x80B9C3 (the load lambda);
  - `hookTableWrapper` on rep+0x3E3D90; `hookTableBuilder` on rep+0x8261F0;
  - `hookExeInit` on exe+0xBC150 (logs the Initialize's args);
  - `hookTableBuilder`'s a4 probe logs `[a4]` and `[a4]+0x10`.
- frame60 probes (search `frame60:` in the log):
  - `tables [main+0x1B0]/[main+0x210]`, `taskList`, `stepCtrl`, `stepA list`,
    `stepA task[i]` (vtable names), `invoke RL table task`, `new task[i]`,
    `after task run`, `holder[0]`, `FetchResult`, `lua=...`, `lua fs cb ...`,
    `lua pakFlag(0x1729C0)=...`, `exe sys globals`, `exe Create(...)`,
    `exe OnInitialize(...)`, `exe Represent Initialize enter/exit`,
    `exe dispatcher(state 3)`.
- `g_taskInvokeStub` (asm, VirtualAlloc) — the task invoke with the stepCtrl.
- The VEH prints backtraces for faults in rep/exe/CRT/ntdll modules.

## 5. Pitfalls learned the hard way (read before editing)

- **Order bugs**: patches/hooks must be applied BEFORE the code that needs them
  runs. Two such bugs cost hours: the 0x9FA70 guard stub and the fabrication
  block were applied after the code that needed them. Check the frame60
  sequence order whenever a "should work" fix doesn't.
- **The CRT guards**: the exe's magic-static guards 0x79B6E0/0x79B680/0x79B3F0
  must be stubbed for the OnInitialize blocks, but NOT before the module
  Creates' ctors (they need their statics to initialize). 0x9FA70 is a
  different guard flavor (stub with `exeGuardNoop`).
- **The stepCtrl structure**: the rep Init reads the pool allocator via
  `[[stepCtrl]+0x10]`; the builder reads it via `[stepCtrl+0x10]`. The host's
  fake provides it at BOTH (`stepA+0x10` and `stepBuf+0x10`).
- **The invoke convention**: the V tasks and the builder's functors both use
  `vt[1]` (from the object's vtable pointer) as the invoke; the wrapper's arg2
  is the stepCtrl (rdx), and the game's task runner passes it. Do not call
  `tvt[1](val)` directly — use the stub with (val, stepCtrl).
- **Exe addresses**: the arithmetic in the old notes had several off-by-0x1000/
  0x1000000 errors (e.g. movie singleton = KG_MovieEngine+0x19F8E8, not
  0x1D6EE8). Verify every address by computing it in the script, not mentally.
- **NEVER call the singleton vt[1] (activate)** — it hangs.
- **Window**: default off-screen; the on-screen present is still unfixed (the
  frame loop calls beginPaint/beginView/endView/endPaint but no present).
- **No shell edits of `client_host.cpp`** — use the edit tool or a Python
  script (the file has CRLF; the shell mangles quotes). Python helpers from
  this session are in `%TEMP%\opencode\skillv2\*.py`.

## 6. Evidence and commits

- Logs: `%TEMP%\opencode\skillv2\host_exe146-178.out` (the current chain),
  host_exe119-145 (window fix + earlier Gate 1 steps).
- Key commits (branch `agent/skillv2-sandbox`, all local):
  - `78e32e5` destination args + no-dot shadow name;
  - `3a55132` window fix + vt[6] setter (line 198 cleared);
  - `cd6cb3f` the exe's own Initialize completes;
  - `478090a` param+0xA8 = KJX3LogicModule sub-object; builder completes;
  - `5dd1b33` runTasks runner reached;
  - `f7dce23` lua pak flag set (pak path).
- The full narrative: `docs/EXPERIENCES.md` 2026-10-06 entries (8 of them).

## 7. Fallbacks (only if the lua file layer dead-ends)

- Fabricate the tables directly: the weather check only needs
  `[SO3Represent+0x210]` non-null; the table pointers live at
  `SO3Represent+0x1B0..0x248`. This is a band-aid — register it as a
  provisional deviation in `docs/EXPERIENCES.md` with re-open criteria.
- The compiled-map fork for the destination scene: an offline compile with the
  client-bundled editor stack (`zhcn_hd\MovieEditor\bin64`, needs
  SO3StatsSystemX64.dll) — the user approved "do both a and b" for this.

## 8. Response protocol (mandatory, AGENTS §15)

- End every response with a `Verified:` line (command -> result) and the
  game-design check sentence.
- Append a compact entry to `docs/EXPERIENCES.md` after every work-bearing
  response; register new docs in the area README index.
- Commit after every change (`Area: summary` style); never push.
