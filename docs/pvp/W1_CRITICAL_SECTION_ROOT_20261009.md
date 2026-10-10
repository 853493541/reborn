# W1 root — uninitialized critical section in `KG3D_TimeLine<float>::CreateCache`

Status: **root localized (MED-HIGH)**; fix not yet applied. Read-only analysis of
`C:\SeasunGame\MovieEditor\bin64\KG3DEngineDX11EX64.dll` (ImageBase `0x180000000`) + live dump of
our own host process. Supersedes the earlier "memmove size -6" and "RtlAllocateHeap" notes (both were
register-decode errors).

## Symptom

39 roster abilities AV when their SFX is played. The crash is a **first-chance AV** at
`ntdll.dll+0xFA7D`; the same thread had just entered `KG3D_TimeLine<float>::CreateCache`.

## Corrected fault chain

- `ntdll+0xFA7D` is `inc dword ptr [rax+0x24]` with `rax = [rdi] = 0` and
  `rdi = KG3DEngine+0x2D3EEE8` -> writes address `0x24` -> **AV (null-deref)**.
  (`rdx/rsi = 0xFFFFFFFA` is a leftover register, NOT a size.)
- `ntdll+0xFA7D` lies inside **`RtlEnterCriticalSection`** — confirmed by ntdll's export table
  (`RtlInitializeCriticalSection=0xD65C0`, `RtlEnterCriticalSection=0x127F0`) and the code at
  `0xFA6D`: `mov rax,[rdi]; cmp rax,-1; je; inc [rax+0x24]`.
- The caller is `KG3D_TimeLine<float>::CreateCache` (`0xE2E690`): after the per-frame curve-eval loop
  fills the buffer, it does `lea rcx,[0x2D3EEE8]` (`0xE2E835`) then
  `call RtlEnterCriticalSection` (`0xE2E83C`) to commit the cache, then writes the timeline object
  (`[r14+0x18]=buffer`, `[r14+0x20]=minKey`, `[r14+0x24]=maxKey`).
- The IAT slot at `0xE2E83C` resolves (loaded image) to `ntdll+0x127F0` = **`RtlEnterCriticalSection`**.

So: `KG3DEngine+0x2D3EEE8` is a static **CRITICAL_SECTION** whose DebugInfo (first qword) is `0` ->
`RtlEnterCriticalSection` faults. The only RIP-relative references to it are inside `CreateCache`
itself (enter/leave).

## Why it is uninitialized (the gate)

`CreateCache` *has* a lazy init: `0xE2E8D7: call [de608b]` -> `ntdll+0xD65C0` =
**`RtlInitializeCriticalSection(&0x2D3EEE8)`**. But the init block (`0xE2E8B7..0xE2E8F5`) is only
entered via:

```
0xE2E6CA  cmp  dword ptr [KG3DEngine+0x2D3EF10], eax   ; eax = [TLSblock + 0x89b0]
0xE2E6D0  jg   0xE2E8B7                                 ; if guard > tls -> init block
...
0xE2E8B7  lea  rcx,[KG3DEngine+0x2D3EF10]
0xE2E8BE  call 0x1818C7CA8                              ; once-helper: if([guard]==0)[guard]=-1
0xE2E8C3  cmp  dword ptr [KG3DEngine+0x2D3EF10], -1
0xE2E8CA  jne  0xE2E6D6                                 ; (not -1) -> skip init -> main -> Enter -> AV
0xE2E8D0  lea  rcx,[0x2D3EEE8]
0xE2E8D7  call RtlInitializeCriticalSection
...
0xE2E8F5  jmp  0xE2E6D6
```

`eax` is a per-thread value: `mov rax, gs:[0x58]` (TLS array) -> `[rax + idx*8]` where `idx` is read
from the engine's data at RVA `0x2B46C4` (disp `0x1826014` from `0x180E2E6B0`) -> `[TLSblock + 0x89b0]`.

Live dump values (our host run): `[0x2D3EF10] = 0`, `[0x2D3EEE8] = 0` (cs uninitialized).
Since the guard is still `0`, the `jg` at `0xE2E6D0` was **not taken** -> init skipped -> AV.

`0x1818C7CA8(&guard)` is a once-helper that locks another cs and writes the sentinel `-1` into the
guard when it is `0` — i.e. it marks "initialized". It only runs inside the (skipped) init block.

## Next probes

1. Resolve the TLS index at RVA `0x2B46C4` and read the faulting thread's `[TLS+0x89b0]` from the dump
   (walk TEB -> ThreadLocalStoragePointer) to learn `eax` and confirm the compare.
2. Find the engine startup step that seeds the guard/TLS (who sets `0x2D3EF10` or the TLS slot) and
   whether our host omits it (likely a host init call), vs the path being a latent engine bug only
   these 39 abilities reach.
3. ~~Compare a non-crashing ability.~~ **DONE (2026-10-09):** casting non-blacklisted ability `27847`
   (which has an `.Sfx` effect) hits the `0xE2E83C` lock **0 times** (and no AV). So the Enter-cs path
   in `CreateCache` is **unique to the 39 abilities** — the routing is **data-driven**, not a global
   init gap. The 39 abilities' sfx/timeline data drives `CreateCache` into its rebuild/lock branch,
   whose cs is uninitialized. So the fix direction is: (a) find why these 39 sfx trigger the
   rebuild/lock branch (vs the prebuilt fast path), and (b) seed the cs (the gate `0x2D3EF10`/TLS).
   Repro of this test: `RC_ABILITY=27847 RC_CAST_AT=38000:1` + the same `0xE2E83C` BP.

## Reproduce

- Repro: remove `65068` from the runtime `...\bin64\ability_picker\av_blacklist_f1.txt`, run the client
  with `RC_ABILITY=65068 RC_CAST_AT=38000:1 RC_DUMMY_N=1 RC_NOLOADING=1 RC_AUTORUN=70000`, and attach
  `tools/pvp/multi_trace.py <pid> KG3DEngineDX11EX64.dll+0xE2E83C --arm-after 62 --bp-log` (cold BP).
- Evidence: `proof/netcode/skillv6_w1_live_fault_20261009.txt`, `proof/netcode/skillv6_w1_cs_xref_20261009.txt`.
- Tools: `tools/pvp/multi_trace.py`, `tools/pvp/minidump_full.py`, `tools/netcode/xref_va.py`,
  `tools/pvp/dump_fn_disasm.py`.

Confidence: HIGH (fault site / chain), MED-HIGH (the gate values in our run), MED (that a missing
host init is the cause vs a latent engine bug). Last verified: 2026-10-09.
