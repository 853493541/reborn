# Next-agent handoff — Gate 1 (real `CreateRLScene`) / RL table list

**You are reading `docs/engine_host/NEXT_AGENT_HANDOFF.md`** — this file is the
handoff guide. Find it at:

- Worktree: `C:\Users\Zhibin Ren\Desktop\reborn-iso-skillv2-sandbox`
- Path: `docs\engine_host\NEXT_AGENT_HANDOFF.md`
- Branch: `agent/skillv2-sandbox`
- Registered in the area index: `docs\engine_host\README.md` (row
  `NEXT_AGENT_HANDOFF.md`).

If a prompt points you here, read in this order:

1. **This file** (the working map: state, chain, blocker, next probes).
2. `docs\engine_host\CLIENT_CHARACTER_PLAN.md` — the Gate 1..5 plan
   (Gate 1 = the real `CreateRLScene` completing).
3. `docs\EXPERIENCES.md` — the 2026-10-06 entries and the 2026-10-07 entries
   (the lua file-layer fix + RL table chain completion).
4. `AGENTS.md` §2 (isolation/worktree rules) and §15 (response protocol:
   Verified line + game-design check + EXPERIENCES entry).
5. Session logs: `%TEMP%\opencode\skillv2\host_exe*.out` — the 2026-10-07 runs
   are `host_exe179-193.out` (the current chain); 146-178 is the previous
   session.

Last updated: 2026-10-07 (mid-session, after the lua file layer was fixed and
the RL table chain now completes).

## 0. TL;DR — what changed and what to do next

**Major progress (2026-10-07):**

1. **The lua file layer works.** The old "wild call" was the host's OWN
   instrumentation bug: `installTableLoadHook` used a 5-byte `jmp rel32` to a
   `VirtualAlloc(NULL,..)` stub that could land >2 GB away — the truncated
   displacement jumped wild (nvcuda64 / module-unknown addresses). Fixed with
   `allocNear()` (near allocation) + a stub that forces 16-byte stack
   alignment and saves rsp in a callee-saved register (the lambda at
   `rep+0x80B9C3` is reached by a **non-call jump** — a plain `call` from a
   misaligned stack crashed inside the CRT `movdqa`).
2. **The lua "pak flag" was a misread.** `lua+0x1729C0` is not a bool — it is
   the **prior-root string** (export `g_SetPriorRootPath` = lua+0xB5380).
   The real globals:
   - root `0x170060` (`g_SetRootPath` 0xB5400, strips a trailing separator),
   - file path `0x170170` (`g_SetFilePath` 0xB5220),
   - prior root `0x1729C0` (`g_SetPriorRootPath` 0xB5380; empty = loose),
   - pak manager `0x1730B8` + type vector `0x1730A0..0x1730A8`
     (`KG_InitPakV4FileSystem` = lua+0xCC2D0).
   `0x1709C0` does not exist (0 refs) — drop that lead.
3. **Pak mode matters:** `KG_InitPakV4FileSystem(..., arg4)` select the file
   priority: 0 = pak-first, 1 = loose-first. The host now runs with `arg4=1`
   (`RC_HOST_PAK=1`, matching `config.ini` `PakFirst=0`) — the sandbox's loose
   files win; the pak supplies only what is missing loose (e.g.
   `GI_DetailTracingCommon.hlsli`, `Represent/skill/skill_caster_model.ini`).
   Without the pak the engine's shader compile fails; pak-first shadows the
   sandbox map (scene environment NULL -> frame-0 paint fault).
4. **SemanticX64 file IO must be installed before the RL table task.** The
   represent Init installs it in the game; in-host it ran later (MapConverter).
   The host now calls `SemanticX64!SetFileIOFunctions(rep+0x788D, rep+0x1EB0,
   rep+0x10DC, rep+0x18926)` — see
   `frame60: Semantic SetFileIOFunctions installed (pre-RL-task)`.
   Before this, `sLoadNumberFromFile` got a NULL `Table` (CreateRLFile failed).
5. **The RL table chain now COMPLETES:** `runTasks enter` -> `tableLoad lambda
   enter` (the rep+0x80B9C3 hook now fires!) -> **`runTasks exit -> 1`** with
   `KTableList::Init` (rep+0x836510) succeeding. The loaded tables land in the
   KTableList at **`g_repSingleton + 0x1A0`** (`kt`), whose members
   `+0x11FF8`, `+0x12000`, `+0x1DE40`, `+0x23BB8` are non-null after the run.

**The remaining blocker (one logical link):** `[g_repSingleton + 0x210]`
(m_tabCommon, read by `KRLWeatherController::Init` line 27 as
`g_pRL->m_TableList.m_tabCommon`) is still NULL. **RESOLVED 2026-10-07**
(runs 205-208): the writer is the game's own **`KTableList::LoadConfigureFile`
(rep+0x833260)** — it opens `"CommonKRL"`, `SemanticX64!CreateRLFile()` ->
`[kt+0x23A68]` = m_pCommon, then `m_pCommon->vt[2](file,1,1)` ->
`[kt+0x70]` = m_tabCommon. The runTasks chain only runs the *misc* loader
(0x836510); the host now calls LoadConfigureFile(kt) at frame60 before
CreateRLScene. Result: `[main+0x210]` becomes non-null and **KRLScene::Init
line 27/212 now PASS** — CreateRLScene proceeds far deeper (loads the represent
lua scripts, sets up entities).

**STATUS (2026-10-07, run 239): Gate 1, Gate 2, Gate 3 done; Gate 4 blocked.**
- Gate 1: `CreateRLScene` -> `GetRLScene(2)` non-null (non-null 3DScene);
  `m_tabCommon` via `KTableList::LoadConfigureFile`. The "wild call" was a host
  hook-length bug (entityFactory len 16->12), fixed at the root.
- Gate 2: char chain runs fault-free on the real scene (`0x924B(2)` non-null).
- Gate 3 **checkpoint met**: running the engine's own initializers in order -
  named-object manager (`rep+0x920D10`), ECS root (`rep+0x924B20`), and the
  **global scene-system init `rep+0xADEFD0`** (no args, registers `"scene[main]"`)
  - makes `InitializeScene` (`rep+0xADFFE0`) return 1 and set
  `[KRLScene+0xF29E8]` (non-null). Note: the `0x58CE20` chain is the **camera**
  lookup (0x1B9D7 -> 0x304650 searches the scene node for `"camera"`), not the
  local player.
- Gate 4 **blocked (root cause found)**: the represent's actor/dummy managers
  (`RLActorMgrNT`, `KRLDummyMgr`) are created by the **logic-driven scene-enter**,
  not by represent-scene creation. Run 239 has `SO3Represent::Init -> 1`, the real
  `CreateRLScene` (GBK name, arg9=1), `InitializeScene -> 1`, yet **no** manager
  event registration fires. The manager API is fully located (see below) but its
  construction has no static caller (runtime dispatch) and no debugger is installed.
  Same root as Gate 3's local player (`KSO3World::AddPlayer`, logic-side).
- Gate 4 API (all located, `rep+`): `RLActorMgrNT::CreateRLActorNT` 0x36D3E5,
  `RLActorMgrNT::Init` 0x36DA50, `RLActorNT::Init` 0x35C0A9, `LoadModel` 0x35C370,
  `LoadPart` 0x35CC22, `KRLDummyMgr::Create` 0x40D770, `KRLDummy::CreateDummyEntity`
  0x409500, `KRLDummyMgr::Init` 0x40F050. Host has an event-register hook on
  `rep+0x39D560` (len **23** - its `sub rsp,0x80` is 7 bytes; len 20 = illegal
  instruction) that captures `{vtable,this}`; the RLActorMgrNT handler vtable is
  `rep+0xC90848`.
- Gate 4 MILESTONE (2026-10-07): the manager is an embedded singleton member at
  **singleton+0x25150** (vt `rep+0xC8FF98`). Host wires the engine's own Init caller
  (`rep+0x3E44C1`): `RLActorMgrNT::Init(mgr, [singleton+0xC0], [singleton+0xB0]->vt[8]())`,
  after `InitAsyncTask(rep_main+0x26090, NULL, 0)` (delegate registry at rep_main+0x262C0,
  `Delegate::Initialize` count 0x56). **`Init -> 1` (SUCCESS).** Root cause of the
  line-86 failure: the host's `void` hooks clobbered the engine's `Register` return
  (audit all `void` hooks - 3 instances of this bug class).
- Gate 4 remaining: `CreateRLActorNT(mgr, representID, type)` enters the engine's actor
  path (loads `represent/scripts/dummy/behavior_base.lua`, `OnSceneActorLoaded` fires)
  but the run dies with fatal `STATUS_HEAP_CORRUPTION` (0xC0000374), nondeterministic.
  Suspect: `RLActorMgrNT::Init` creates an async worker thread (`mgr+0xB8`,
  `RLAsyncTaskMgr::Init` rep+0x375F50) and the host passes `InitAsyncTask` an EMPTY task
  list (`r8d=0`); the engine's normal call supplies the represent's async-task array.
  Next: supply that array (find InitAsyncTask's caller, no static xref yet), or run
  actor creation on the engine's task thread; then `RLActorNT::LoadModel` F1 +
  `tools/proof/image_stats.py`.
- `RC_HOST_ACTOR=<rid>,<type>` drives the actor call; `RC_HOST_ACTOR=init` = init-only;
  `RC_HOST_NOHANGPET`/`RC_HOST_NODIAG`/`RC_HOST_NOSHADOWDESC` gate the other blocks.
- Next: drive the logic scene-enter (or construct `RLActorMgrNT`), then
  `CreateRLActorNT` + `LoadModel` F1 + `tools/proof/image_stats.py` proof.
- `m_tabCommon` is set by the game's own `KTableList::LoadConfigureFile`
  (`"CommonKRL"`).
- The long-running "wild call" was a **host inline-hook bug**: `hookEntityFactory`
  was installed at `rep+0xAEDFD0` with `len=16`, so its trampoline replayed a
  RIP-relative `je` and a truncated instruction -> garbage execution when the
  engine created the scene entity. Fixed to `len=12`. (Audit note: the two
  diagnostic lua hooks `g_GetFullPath`/`g_GetPriorFullPath` replay a
  RIP-relative `lea r9`; benign here but latent - see EXPERIENCES 2026-10-07.)
- The provisional VEH recovery added during diagnosis has been removed.

Historical diagnosis (kept for reference) — the since-fixed wild call:
CreateRLScene faults with `exc 0xC0000005 at 0x...001D (module?)`. The VEH now
logs AV registers (with `tid=`) and dumps the first 24 raw stack qwords (any
module) plus a filtered game-stack scan. Observed (runs 209-212):

- The fault is on the **main thread** inside CreateRLScene (`regs tid` == the
  `[host] main tid` line); the process still dies (the host `__try` may catch
  the first one but the engine retries the same fault and dies; the last log
  lines are lost to stdio buffering — the VEH `fflush`es).
- `regs rip=0x...001D rax=0x35B1DFE0 rcx=rsp+0x9F rdx=7 rsi=rsp+0x108
  rdi=rsp+0xB0 rbx=rep+? `; `rax=0x35B1DFE0` is **constant across runs** (a
  32-bit-looking value, not a pointer).
- raw stack: `raw[13]=JX3RepresentX64.dll+0xAEE2DD` is the top game return
  (raw[0..12] are the host VEH/exception frames); the same frame was seen in
  runs 206/208/210/211. `raw[20..21]` spell `scene[000002]`; the rep assert
  format `"scene[%.6u]"` lives at rep+0xD0A2B0 (referenced by 0xAD38B0, a
  scene-id wrapper `obj=0x14E2F(id)` -> `0x2CB6(obj,id)`).
- The wild call has **no pushed return address** at [rsp] (raw[0]=raw[1]=0), so
  it is an indirect `jmp [reg]`/tail dispatch, not a plain `call [reg]`.

Next probes:

1. **The wild transfer is located** (runs 213-217) with a hardware
   execute-breakpoint + single-step tracer (`armExecTrace`, Dr1 + trap flag;
   opt-in via `RC_HOST_DEBUGTRACE`) and a probe hook on the jmp target:
   armed at `rep+0xAEE2D8`, the trace is `HIT rep+0xAEE2D8` ->
   `step[0] rip=rep+0x15BF4` -> `0x15BF4` jmps to `rep+0xAEDFD0` -> the
   `0xAEDFD0` hook FIRES with `a1=<name ptr> a2=7` -> then the wild AV
   (`at 0x...001D`, `rax=0x35B1DFE0` constant).
   `rep+0xAEDFD0` = a "create named object" helper: it gets a singleton via
   `rep+0xDF8A` -> `rep+0x920C40` (global `rep+0xF51298`) -> `rep+0x1687E` ->
   `rep+0x923400` (an allocator that calls `malloc`), then `strncpy`s the name.
   All calls in that body resolve to valid imports; the wild call is in that
   creation path (or the caller's continuation `rep+0xAEE311 call 0xE5D4`).
   Live bytes at frame60 are unmodified: `0xAEE2D8 = E8 17 79 52 FF`,
   `0x15BF4 = E9 D7 83 AD 00`.
   **New (run 221):** the fault's `rax` is exactly the **low 32 bits of a code
   pointer**: with `rep` base `0x7FF854E60000`, `rep+0xAEDFE0` low-32 =
   `0x5594DFE0` == the observed `rax` (it was `0x35B1DFE0` in run 209 with the
   corresponding base). So some code holds a **32-bit-truncated pointer near
   `rep+0xAEDFE0`** and calls through it. The singleton `[rep+0xF51298]` is
   **NULL** at frame60, so the factory should early-return 0 (no wild) - yet the
   wild happens, so either `[rep+0xF51298]` becomes a bad non-null value during
   CreateRLScene, or the truncated pointer is the real cause. Next: hook
   `0x1687E`/`0x923400`, log `rcx`, and check `[rep+0xF51298]` right before the
   fault; search for a 32-bit store of a `rep+0xAEDFE0`-like pointer (the
   truncation site).
2. **Critical observation (run 216):** when the fault is caught (that run had
   the debug arms active, which changed exception dispatch), the host's own
   `__try` logged `real CreateRLScene fault` and **`GetRLScene(2)` returned
   non-null with a non-null 3DScene** (`0x...6040`, 3DScene `0x...ED8`), and the
   run completed. So **CreateRLScene does create and attach the scene before the
   wild call**; the fault is late in the setup. In the clean build (run 217) the
   same wild AV is not caught and the process terminates (likely the game
   protection module reacts to the AV, or the SEH cannot unwind). Next:
   identify the unset registration behind the wild call (the singleton
   `rep+0xF51298` / its factory), then either fix it or ensure the host catches
   the late fault so Gate 1's checkpoint (scene created + attached) holds.
3. Watch out: arming Dr0/Dr1/TF changes the outcome — keep diagnostics behind
   `RC_HOST_DEBUGTRACE` and judge Gate 1 from a clean run.

**Superseded leads (kept for context):** the register-step / async-queue theory
for m_tabCommon was a red herring — `LoadConfigureFile` sets it directly. The
register functor capture/invoke machinery remains in the host but is gated
behind `RC_HOST_REGINVOKE=1` (the fabricated stepCtrl lacks the async queue's
`[+8]` sync object, so the invoke AVs; it is not needed for m_tabCommon).

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
  `git log -1 --oneline` -> a `Client:`/`Docs:` commit,
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
- Run (PowerShell; the host reads these env vars — **the map name is
  `龙门寻宝_s` (U+9F99 U+95E8 U+5BFB U+5B9D + `_s`); an ASCII transliteration
  silently loads no scene and the frame-0 paint faults**):

```powershell
$env:RC_HOST_ROOT = "$env:TEMP\opencode\skillv2\client_root"
$mapDir = [char]0x9F99 + [char]0x95E8 + [char]0x5BFB + [char]0x5B9D + "_s"
$env:RC_HOST_MAP  = "data\source\maps\$mapDir\$mapDir.jsonmap"
$env:RC_HOST_SHOT = "$env:TEMP\opencode\skillv2\host_exeNNN.png"
$env:RC_HOST_RLLOADER = "1"; $env:RC_HOST_LOGIC = "1"
$env:RC_HOST_EXE = "1";     $env:RC_HOST_EXE_NOINIT = "1"
$env:RC_HOST_PAK = "1"      # InitPak(...,mode=1 loose-first; required: pak has shader includes + the RL ini
$env:RC_HOST_MAXSEC = "500" # the shader map may recompile once (~165 s); after that init is ~5 s
& "...\native\client_host\out\client_host.exe" *> "$env:TEMP\opencode\skillv2\host_exeNNN.out"
```

- The window is OFF-SCREEN by default. `RC_HOST_SHOW=1` shows it. A watchdog
  force-exits after `RC_HOST_MAXSEC` (default 240 s). Long KEEP runs hit the
  game protection.
- Read the log with e.g.
  `Select-String host_exeNNN.out -Pattern "runTasks|tableLoad|main\+0x210|line 212|fault"`.
- Note: the run opens many thousands of files while logging — the log is
  ~20 MB. All opens go through the lua hooks; the noise is expected.

## 2. The current Gate and where the chain stands

Gate 1 = the real `CreateRLScene` (rep+0xB0B5C0) completes and
`GetRLScene(2)` (rep+0x924B) is non-null via the game's own path.

Chain status (all in `native/client_host/client_host.cpp`, frame60):

1. Phase A/B done long ago (logic boot, `SO3Represent::Init -> 1`, map load,
   resource manager, shadow descriptor fix).
2. **The exe's own `KJX3RepresentModule::Initialize` (exe+0xBC150) COMPLETES**
   (`exit -> 1`) through the game's dispatcher (exe+0xBC6A0, state 3).
3. The host's Param + `SO3Represent::Init(Param) -> 1` queues 5 tasks into the
   fabricated stepCtrl's stepA list; the table-loader task = vtable
   `rep+0xC99D30`, invoke = slot 1 (`rep+0x3E59C0`).
4. The builder (rep+0x8261F0) builds register/run tasks into the stepBuf list
   (sc+0x70). The host walks it and invokes the tasks; **runTasks
   (rep+0x80B8C0) now completes with 1** (2026-10-07).
5. runTasks opens `Represent/skill/skill_caster_model.ini` (from the pak),
   reads `Count` == 7, then `KTableList::Init` (rep+0x836510, reached by the
   `jmp` thunk at rep+0x8003) loads the sub-tables via SemanticX64
   (`CreateRLFile`, `g_OpenIniFile`) and stores them into the KTableList at
   `singleton+0x1A0` (+0x11FF8/+0x12000/+0x1DE40/+0x23BB8 non-null).
6. **Blocker:** `[singleton+0x210]` (m_tabCommon) never gets set; the weather
   check then fails (line 27) and `KRLScene::Init` line 212 fails. The write
   watch shows the task run does not write it (see §0 for the register-task
   lead).

## 3. Instrumentation already in the host (do not re-add)

- Trace hooks (installed in main):
  - `hookRegisterTasks` on rep+0x80B6A0 (never fired so far);
    `hookRunTasks` on rep+0x80B8C0 (fires; logs kt fields on exit);
  - `installTableLoadHook` on rep+0x80B9C3 (fires; logs the 7 module-side
    table names at rep+0xF16AF0 — empty in-host, they are runtime-filled);
  - `hookTableWrapper` on rep+0x3E3D90; `hookTableBuilder` on rep+0x8261F0;
  - `hookExeInit` on exe+0xBC150.
- lua file-layer hooks (installed in main with the lua load):
  - `hookGetFullPath` (0xB4390), `hookGetPriorFullPath` (0xB4570),
    `hookOpenFileLua` (0xB2F50), `hookIsFileExist` (0xB5060),
    `hookOpenPakV4` (0xCC670, logs the pak mgr vtable slot 2),
    `hookLooseOpen` (0xB1C70). All log and forward.
- `allocNear()` — allocate within ±1 GB for rel32 hooks.
- `armWriteWatch(addr)` — Dr0/Dr7 4-byte write watch + VEH logs the writer RIP
  (used on `singleton+0x210`; zero hits).
- VEH now captures a backtrace for up to 24 AVs (not only rep/CRT/exe/ntdll).
- frame60 probes: lua root/filepath/priorRoot strings, pak manager + type
  vector, `tableSite` patch bytes/protection, `Semantic SetFileIOFunctions`,
  `runTasks`/`tableLoad`/`registerTasks`, `[main+0x1B0..0x248]` dump after the
  task run, `[param+0xA8]` member dump.
- `g_taskInvokeStub` (asm, VirtualAlloc) — the task invoke (vt[1], rdx=stepCtrl).

## 4. Pitfalls learned the hard way (read before editing)

- **rel32 hooks**: never allocate a stub with plain `VirtualAlloc(NULL,...)` for
  a 5-byte `jmp rel32` patch — the stub can be >2 GB away and the truncated
  displacement jumps wild. Use `allocNear(site, ...)`.
- **Non-call entry points**: rep+0x80B9C3 is reached by a `jmp`, not a `call` —
  the ABI's 16-byte stack alignment is not guaranteed. The stub must save rsp
  in a **callee-saved** register (`rbp`), align, call the logger, restore.
  (A volatile register gets clobbered by the hook call.)
- **Order bugs**: patches/hooks must be applied BEFORE the code that needs them
  runs. The entire 178-era "wild call" was this stub bug.
- **The CRT guards**: the exe's magic-static guards 0x79B6E0/0x79B680/0x79B3F0
  must be stubbed for the OnInitialize blocks, but NOT before the module
  Creates' ctors. 0x9FA70 uses `exeGuardNoop`.
- **The stepCtrl structure**: the rep Init reads the pool allocator via
  `[[stepCtrl]+0x10]`; the builder reads it via `[stepCtrl+0x10]`. The host's
  fake provides it at BOTH.
- **The invoke convention**: the V tasks and the builder's functors both use
  `vt[1]` as the invoke; the wrapper's arg2 is the stepCtrl (rdx). Do not call
  `tvt[1](val)` directly — use the stub with (val, stepCtrl). NOTE: the
  builder's register/run step objects (vtables 0xCD80C8 / 0xCD8000) have their
  work in **vt[0]**; this may be the reason register never runs — verify.
- **Exe addresses**: verify every address by computing it in a script.
- **NEVER call the singleton vt[1] (activate)** — it hangs.
- **Window**: default off-screen; the on-screen present is still unfixed.
- **No shell edits of `client_host.cpp`** — use the edit tool or a Python
  script (the file has CRLF). Python helpers from the sessions are in
  `%TEMP%\opencode\skillv2\*.py`.
- **Run env**: `RC_HOST_PAK=1` (loose-first) is required for a full init; the
  map name must be the real `龙门寻宝_s` (see §1).

## 5. Evidence and commits

- Logs: `%TEMP%\opencode\skillv2\host_exe179-193.out` (2026-10-07 chain);
  `host_exe146-178.out` (the 2026-10-06 chain).
- Key commits (branch `agent/skillv2-sandbox`, all local):
  - 2026-10-07: `6b552db` lua file-layer trace hooks + real globals + frame60
    latch + VEH traces; `4796241` table-load hook stub fix (near stub + stack
    alignment); `70b9154` Semantic file IO before the RL task; `a53d874`,
    `c947771` write watch + KTableList probes.
  - 2026-10-06: `78e32e5`, `3a55132`, `cd6cb3f`, `478090a`, `5dd1b33`,
    `f7dce23` (see git log; the narrative is in `docs/EXPERIENCES.md`).
- The full narrative: `docs/EXPERIENCES.md` 2026-10-06/07 entries.

## 6. Fallbacks (only if the RL-table publish dead-ends)

- Fabricate the tables directly: the weather check only needs
  `[SO3Represent+0x210]` non-null; the table pointers live at
  `SO3Represent+0x1B0..0x248`. This is a band-aid — register it as a
  provisional deviation in `docs/EXPERIENCES.md` with re-open criteria.
- The compiled-map fork for the destination scene: an offline compile with the
  client-bundled editor stack (`zhcn_hd\MovieEditor\bin64`, needs
  SO3StatsSystemX64.dll) — the user approved "do both a and b" for this.

## 7. Response protocol (mandatory, AGENTS §15)

- End every response with a `Verified:` line (command -> result) and the
  game-design check sentence.
- Append a compact entry to `docs/EXPERIENCES.md` after every work-bearing
  response; register new docs in the area README index.
- Commit after every change (`Area: summary` style); never push.
- **If a client start is blocked by the single-instance guard**, the response
  must end by naming the conflicting session (process name, PID, start time).
