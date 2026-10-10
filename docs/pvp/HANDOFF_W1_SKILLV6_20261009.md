# HANDOFF — skillv6 sandbox / W1 (per-tani AV) — 2026-10-09

Hand this to another agent (or a fresh session). Everything needed to resume is here;
the authoritative mechanism write-up is `docs/pvp/W1_CRITICAL_SECTION_ROOT_20261009.md`.

## Location / git

- **Worktree**: `C:\Users\Zhibin Ren\Desktop\reborn-iso-skillv6-sandbox`
- **Branch**: `agent/skillv6-sandbox`  (off `main`)
- **Tip**: `e00a881b` — clean, level with `origin` (0 unpushed)
- **Remote**: `https://github.com/853493541/reborn.git`
- **Venv to use** (this worktree has none): `C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe`
- Isolation rules: operate ONLY in this worktree; never push unless asked; never merge to `main`.

## What this branch already delivered (done + verified)

- FX4 target be-hit animation; buff durations (`build_buff_times.py` -> `buff_times_f1.tsv`, Buff.tab
  `Count x Interval` @16 fps); AoE radius fallback fix (`nMaxRadius` when `nAreaRadius<=0`).
- Full 154-ability coverage sweep green (115 casts + 39 AV-skips, 0 AV); offline gate
  `tools/pvp/verify_skillv6_data.py`.
- Extensive W1 RE (read-only): tools `tools/pvp/mini_debugger.py`, `multi_trace.py` (cold HW BP, AV
  catch, `--arm-after`/`--bp-log`/`--dump`), `minidump_full.py` (streaming dump reader + `.pdata`
  unwinder), plus `tools/netcode/xref_va.py`, `tools/pvp/dump_fn_disasm.py`.

## The open task (W1): unblock the 39 AV-blacklisted abilities

39 roster abilities AV when their SFX plays. Root chain (proven):

- AV = null-deref in `RtlEnterCriticalSection` (`ntdll+0xFA7D` `inc [rax+0x24]`, `rax=[cs]=0`) on the
  static cs `KG3DEngineDX11EX64.dll+0x2D3EEE8`, called from `KG3D_TimeLine<float>::CreateCache`
  (`0xE2E83C`).
- That cs has exactly one initializer: a lazy `RtlInitializeCriticalSection(&cs)` (`0xE2E8D7`) inside
  `CreateCache`, gated by `cmp [KG3DEngine+0x2D3EF10], [TLSblock + 0x89b0]; jg <detour>`.
- **Proven** (cold BP, resolved target): the init site is hit **0 times** on the repro while the AV
  fires -> cs stays 0 -> AV. Non-crashing ability `27847` (has `.Sfx`) never enters the lock path
  (routing is data-driven).
- `KG3D_Engine::_InitSFXModuleEx` (export `0x1800E5420`) is a **no-op stub** (`xor eax,eax; ret`).
- `TLS+0x89b0` is `__declspec(thread)` (`.tls` template at offset `0x89b0` = `00 00 00 80` =
  `0x80000000`; verified). Crash/engine thread = `0`, 110 workers = `0x80000000`.
- Generic engine lock/generation wrapper `0x1818C7CA8`+`0x1818C7C48` (hundreds of call sites) does
  `G += 1` (G @ RVA `0x257F2D0`, currently `0x8000025A`), `*guard = G`, `[TLS+0x89b0] = G`. So the
  engine thread's `[0x89b0] = 0` was NOT written by this wrapper.

### The exact open question + the definitive next probe

**Definitive next probe (ready to run):** set a **hardware data WRITE breakpoint** on
`engine_TLSblock(index=29) + 0x89B0` for the engine thread during the W1 repro, log the writer's RIP.
The two wrapper helpers both write `G=0x8000025A` here, never `0`; no immediate/disp write of `0`
exists, so the zeroing writer uses a **stored field pointer** — only a data BP can catch it.
`tools/pvp/mini_debugger.py` already supports HW data BPs (`set_hw_bp`, `Dr0/Dr1/Dr7`, `len=`); check
`multi_trace.py` wiring. Resolve `engine_TLSblock(29)` at runtime via `TEB+0x58 -> [..+29*8]` (see the
TEB-walk snippet used in `minidump_full.py`), and confirm the resolved address equals `worker? <no>` /
the crash thread's block before arming.

**Tooling now supports it (this session):** `tools/pvp/multi_trace.py` gained `--data-write` (arms the
Dr slots as data WRITE breakpoints, len=4: Dr7 `rw=01,len=11`) and accepts a **bare absolute address**
target (no `Module+off`). On a data hit it prints
`DATAWRITE tid=.. addr=0x.. writer_rip=<resolved> dr6=.. dr7=.. rcx/rdx/rsi/rax=..`.
Remaining wiring for the run: the target must be the runtime `engine_TLSblock(29)+0x89B0` absolute VA,
which is ASLR-dependent — either add a tiny self-resolver (compute `[gs:0x58]`->`[..+29*8]`+0x89B0 in the
target per thread) or set it just-in-time. Example once the VA is known:
```
.venv\Scripts\python.exe tools\pvp\multi_trace.py <pid> 0x<TLSblock+0x89b0> --data-write --arm-after 62 --bp-log
```
The writer RIP it reports is the answer (likely a `mov dword ptr [reg], 0` or a region memset on the
engine thread).

**STATUS (latest, 2026-10-09):** the tooling is implemented and smoke-tested —
`multi_trace.py <pid> "tls:29:0x89b0" --data-write --arm-after <s> --bp-log` resolves the per-thread
engine TLS field, arms a HW data WRITE BP (Dr7 rw=01,len=11), and prints
`DATAWRITE tid=.. addr=.. writer_rip=.. dr6/dr7/rcx/rdx/rsi/rax` on each write.
Runs done: `--arm-after 60` -> **0 DATAWRITE hits** (so the field was already zeroed before t=60s) while
the AV still fired; an `--arm-after 1` run was **aborted by the user** mid-flight (unverified). Next:
re-run with a small `--arm-after` (e.g. 1-5s, so `arm_all` catches existing threads early) and grep the
`DATAWRITE` lines for the one that writes `0` (its `writer_rip` is the answer). NOTE: the engine thread's
field is written on every engine-lock (wrapper) call, so expect many hits/some slowdown; the `0`-writer
stands out by `rax/rdx/rcx = 0`. If 0 hits even with an early arm, the resolved address may be wrong —
compare the printed `addr` for the engine thread against the dump's `[TLSblock(29)+0x89B0]`, or print a
one-shot read of the field at arm time to confirm.



**Why is the engine thread's `[TLS+0x89b0]` already `0` when `CreateCache` first runs** — instead of
the fresh-thread template `0x80000000` (which would make `0 > 0x80000000` true and run the init
detour)? Find the writer of `0`, or the startup step that should have set guard/state so the detour
runs. (Or: does our host force a *rebuild* path the retail client avoids? — the lock path is only hit
by these 39 abilities' SFX.)

## Repro / verification

```
# build (stop any running reborn_client_skillv6 first!)
$env:RC_CLIENT_EXE="reborn_client_skillv6.exe"; cmd /c client\build_client.cmd   # want exit=0
# runtime data (build does NOT copy): copy ability_picker\data\*_f1.tsv to
#   C:\SeasunGame\MovieEditor\bin64\ability_picker\

# gates (must stay green; use the OTHER worktree's venv)
C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe tools\pvp\verify_skillv6_data.py   # 0 failed
C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py # 10 PASS
C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe tools\gravity\verify_model.py
C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe tools\netcode\loot\capture.py selftest

# W1 repro (cwd = C:\SeasunGame\MovieEditor): remove 65068 from runtime av_blacklist_f1.txt
#   (BACK IT UP first: %TEMP%\opencode\behit_re\av_blacklist_backup.txt)
$env:RC_MEM_NS="reborn_skillv6_probe.memory"; $env:RC_ABILITY="65068"; $env:RC_CAST_AT="38000:1"
$env:RC_TAB_AT="10000"; $env:RC_DUMMY_N="1"; $env:RC_NOLOADING="1"; $env:RC_AUTORUN="70000"
# launch C:\SeasunGame\MovieEditor\bin64\reborn_client_skillv6.exe,
# then: .venv\Scripts\python.exe tools\pvp\multi_trace.py <pid> "KG3DEngineDX11EX64.dll+0xE2E83C" --arm-after 62 --bp-log
# RESTORE the blacklist as a SEPARATE command afterwards (39 ids, 65068 present).
```

Expected: BPHIT at `0xE2E83C` then AV (tid = same). Init site `0xE2E8D7` -> 0 hits.

## Draft data
- Repository `ability_picker\data\*.tsv`; runtime `C:\SeasunGame\MovieEditor\bin64\ability_picker\`.
- Builders `ability_picker\tools\build_*.py`; blacklist `ability_picker\data\av_blacklist_f1.txt`
  (39 ids).

## Gotchas (learned the hard way)

- **rip-relative arithmetic**: a `[rip+disp]` target = `next_instruction_VA + disp32`, NOT
  `ImageBase+disp`. Recomputing by hand caused several wrong reads this session. Use capstone to
  resolve targets. The `0x89b0` offset is an **immediate** loaded into a register
  (`mov ecx,0x89b0; [rdx+rcx]`), so it never appears as a memory displacement.
- **`multi_trace.py` BP target may print without a base** (module-enumeration race ~2 s after launch):
  the line `target ... = 0xE2E8D7` (no `0x7FFE...`) means it set a bogus BP -> 0 hits is a false
  negative. Retry / confirm the base before trusting a 0-hit result.
- **Do the blacklist restore as its own command**: a trailing `try/finally` did not run when the
  debugger command errored, leaving `65068` un-blacklisted.
- Never set a HW exec BP on a hot function (flashing/stall); use `--arm-after` cold BPs or AV catch.
- Known `-6` red herring: `[KG3DEngine+0x2D3EEF0]=0xFFFFFFFA`; it is not a size.

## Pointers
- Mechanism doc + handoff: `docs/pvp/W1_CRITICAL_SECTION_ROOT_20261009.md`
- Plans: `docs/pvp/ENGINE_RE_BREAKTHROUGH_PLAN.md`, `docs/pvp/ABILITY_FOLLOWUP_PLAN.md`
- Proofs: `proof/netcode/skillv6_w1_*_20261009.txt`
- Experience log: `docs/EXPERIENCES.md` (append-only; the W1 entries are at the bottom)

Reproduce section: see "Repro / verification" above. Last verified: 2026-10-09.
