---
name: re-binary-analysis
description: Use when reverse-engineering a binary or engine DLL (PE/ELF), interpreting x86-64/ARM64 disassembly or decompiler output, reconstructing a function/protocol, or identifying anti-debug/packing. Adapted from Masriyan's "Reverse Engineering & Binary Analysis" skill; this repo's AGENTS.md rules (copy & analyze, cite symbol/RVA, local-first) win on any conflict.
license: MIT
metadata:
  source: https://github.com/Masriyan/Claude-Code-CyberSecurity-Skill (skills/04-reverse-engineering)
  author: Riyan Pratama
  copyright: Copyright 2026 Riyan Pratama
  modified: trimmed adaptation — the upstream bundled scripts/binary_analyzer.py is not included; project rules and local tools take precedence
---

# Reverse Engineering & Binary Analysis (adapted)

Applies to the JX3 client / MovieEditor binaries and any engine DLL we analyze **on copies**
(root `AGENTS.md` §4: copy & analyze, never affect the installs). Every claim cites
symbol/RVA + confidence (HIGH/MED/LOW) per AGENTS §4.

## 1. Triage checklist (do this first)

```
[ ] Format/magic: PE / ELF / Mach-O / raw (file, or tools/ scanners)
[ ] Architecture: x86 / x64 / ARM32 / ARM64; endianness
[ ] Linking: static / dynamic
[ ] Security features: PIE/ASLR, NX/DEP, canary, RELRO
[ ] Packing: UPX/Themida/custom (high-entropy sections)
[ ] Compiler: MSVC / GCC / Clang / Rust / Go (JX3 is MSVC x64)
[ ] Strings: paths, URLs, keys, table names (use gbk_grep.py for GBK text)
[ ] Imports/exports: which engine APIs are called
[ ] Entry points + section mapping (record image base!)
```

**Precision rule:** record load address/base, architecture, calling convention, and
compiler/toolchain in every analysis so offsets are reproducible. Cite `module+RVA`,
never a bare address.

## 2. Interpreting disassembly / decompiler output

1. Identify the architecture from instruction syntax.
2. Trace flow from the entry point; name call targets.
3. Reconstruct the high-level logic; annotate blocks.
4. Flag security-relevant or engine-relevant patterns (vtables, RTTI, string refs).
5. Treat decompiler output as a hypothesis; confirm with the disassembly + runtime
   evidence. AI naming is a hypothesis to verify, not ground truth.

Common x86-64 patterns:

| Pattern | Instructions | Meaning |
|---|---|---|
| Prologue/epilogue | `push rbp; mov rbp,rsp; sub rsp,N` / `leave; ret` | stack frame |
| Local var | `mov [rbp-N], rax` | stack store |
| Loop | `cmp rax,N; jl loop_top` | counted loop |
| Memcpy/memset | `rep movsb` / `rep stosb` | copy / fill |
| Switch | `jmp [rax*8 + table]` | jump table |
| vtable call | `mov rax,[rcx]; call [rax+off]` | virtual dispatch |
| String ref | `lea rdi,[rip+str]` | literal reference |

Common ARM64 patterns: `stp x29,x30,[sp,#-N]!` (prologue) · `ldp/stp` (pair
load/store) · `bl func` (call) · `b.eq/b.ne/b.lt` (conditional) · `svc #0` (syscall).

## 3. Anti-analysis techniques

| Technique | Indicators | Handling (on copies only) |
|---|---|---|
| UPX | `UPX!`, high entropy | `upx -d copy` |
| IsDebuggerPresent / ptrace | import + branch | note it; static path first |
| Timing checks | RDTSC/GetTickCount loops | identify, don't patch installs |
| String encryption | no readable strings, XOR loops | find decrypt routine, breakpoint after |
| Control-flow flattening | switch dispatcher state machine | trace execution, map real CFG |
| Self-modifying code | VirtualProtect/WriteProcessMemory | breakpoint at write target |

## 4. Protocol / data-format reconstruction

Look for: magic/sync bytes · length fields (2/4 bytes, often at offset 2-4) · type
byte · checksum/CRC (last 1-4 bytes) · padding. Classify fields by evidence (uniform
random 16 bytes → UUID/key; NUL-terminated → string). Build request→response pairs,
then a state machine, then a parser — and validate the parser against real captures.
JX3 formats: prefer the local extractors/parsers in `tools/` (PakV4SfxExtract,
`tools/netcode/`, `tools/collision/`) over writing new decoders.

## 5. Output standards

- File summary: format, arch, base, security features, compiler.
- Key functions + purpose, with `module+RVA` citations.
- Annotated disassembly for the interesting blocks.
- Reconstructed pseudocode (hypothesis; label it as such).
- Findings carry confidence + evidence path; unresolved fields are marked
  `unresolved` — never invented (AGENTS §6).

## Local tooling

- `.venv` already has `capstone` + `pefile` (netcode tools use them); no install needed.
- `tools/proof/image_stats.py` for visual artifacts; `tools/camera/minidump_exc.py`
  for dumps; `tools/netcode/` scanners for strings/imports/tables.
- Ghidra is **not** installed on this machine — if a job needs it, ask first (new
  dependency); see the `ghidra-iterative-re` skill option.
