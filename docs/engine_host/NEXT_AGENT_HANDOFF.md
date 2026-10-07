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

**New blocker (a wild call inside CreateRLScene):** after LoadConfigureFile,
CreateRLScene faults with `exc 0xC0000005 at 0x...16001D (module?)`. The VEH now
also scans the raw stack (`[VEH] stk[i] ...`) and logs the AV registers,
including `regs rip/rsp/rbp/rax..rdi`. Observed (run 209):
`rip=0x207DB86001D rax=0x35B1DFE0 rcx=rsp+0x9F rdx=7 rsi=rsp+0x108 rdi=0
rbx=rep/eng ptr`; raw-stack chain `rep+0xAEE2DD`, `rep+0xAE000F`,
`rep+0x3DB9B7` (return after `call [rax+0xD0]` on `[scene+0xF1978]`),
`CreateRLScene (rep+0xB0BB74)`. The 32-bit-looking `rax=0x35B1DFE0` and the
stack-pointing `rcx` suggest an indirect call through a bad/truncated pointer
(or an object whose vtable was never set). Same class as the earlier wild call.
Next probes:

1. Use the AV registers + stack scan to pick the immediate caller; disassemble
   around `rep+0xAEE2DD` (`call 0x15BF4` -> 0xAEDFD0) and `rep+0x3DB9B1`
   (`call [rax+0xD0]`); log `Rip`, `Rax`, `Rcx` on the next AV.
2. Check which table/config the newly reached path expects (a table loader the
   host still has not called, as was the case for `LoadConfigureFile`).
3. If it is another missing game loader, call it (do not fabricate).
4. Then re-check the CreateRLScene return and `GetRLScene(2)`.

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
