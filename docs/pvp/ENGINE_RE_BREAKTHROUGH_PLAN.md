# Engine RE breakthrough plan — how to break the blocked walls

**Status:** active plan. Area: netcode / pvp / engine-host. Created: 2026-10-08.
**Purpose:** turn the "blocked (binary RE / packed data)" items in
`ABILITY_CORRECTNESS_PLAN.md` into concrete, tooled tasks. All game installs stay read-only; we
copy binaries out and analyze the copies (root `AGENTS.md` §4/§8).

## What "binary RE" and "packed data" mean here

- **Binary RE** = disassembling/decompiling the game client's native DLLs (`JX3RepresentX64.dll`,
  `KG3DEngineDX11EX64.dll`, `JX3ClientX64.exe`, …) to recover *how* the client does something
  (e.g. which default be-hit animation it plays). Same method we already used for camera/collision.
- **Packed data** = `.ani/.tani/.Sfx/.pss/.mdl/.wem` live inside packed archives; we extract the
  needed files with the official `bin64\PakV4SfxExtract.exe` (already our standard tool).

## Tooling we already have (verified 2026-10-08)

| Tool | Use |
|---|---|
| `tools/pvp/dump_fn_disasm.py` | linear disasm of every function that references a method-name string (capstone+pefile) |
| `tools/pvp/disasm_fn.py` | disassemble a function by RVA/range |
| `tools/netcode/xref_string.py`, `xref_va.py` | find code/data xrefs to a string or VA |
| `tools/movement/find_xrefs.py`, `tools/collision/disasm_range.py` | xref/range helpers |
| `tools/camera/minidump_exc.py` | read a `.dmp` (registers, faulting module+RVA) |
| `tools/netcode/pe_imports.py` | imports/exports |
| `tools/pvp/lua51_disasm.py` | Lua 5.1 bytecode (already used) |
| libs: `capstone`, `pefile` | present in `.venv` (lief absent) |
| `bin64\PakV4SfxExtract.exe` | extract packed files (read-only install) |

Proven: `dump_fn_disasm.py JX3RepresentX64.dll --names behit_names.txt` returned
`KRLCharacter::PlayBeHittedAnimation` (3 xrefs) and `KRLCharacter::BeHitted` (6 xrefs).

## Break-the-wall tasks

- **W1 — FWD1 per-tani AV (39 abilities).** Get the faulting site:
  1. Enable a local crash dump for *our* exe (WER `LocalDumps`, HKCU — a local dev setting) OR
     wrap the suspect engine call in an SEH probe (the `sfx_shim`/`CameraShim` pattern) that logs
     a status instead of dying.
  2. `minidump_exc.py <dmp>` -> faulting module + RVA.
  3. `disasm_fn.py <module> --rva <RVA>` -> read the faulting access; compare the bad pointer
     chain to what we pass (the tani/model/bone we set).
  4. Fix the host wiring (likely a missing weapon/skeleton/shadow binding for those tanis).
- **W2 — FWD2 default be-hit animation. IN PROGRESS (good progress).** Dumped `KRLCharacter::
  PlayBeHittedAnimation` (virtual; caller passes `pcszAni`) and located `BeHittedByPlayer`
  (`0x1804d0220`) / `BeHittedByNpc` (`0x1804cfdxx`) / `BeHitted` (`0x1804cfbxx`). `BeHittedByPlayer`
  resolves the be-hit anim by **string lookups on the target model path**: it tests
  `strstr(path,"npc_source/a")` / `strstr(path,"NPC_source\\A")` and builds the anim ending
  `...bat01.ani` (default speed at `[rip+0x7b1f30]`), else falls back to a global table
  (`[rip+0xa0dcae]+0x1a0`). So the default be-hit anim is **derived from the target's model path**
  (NPC sibling `...bat01.ani`), not a fixed id. Next: read the branch past `0x1804d032b` (the
  player/default table) and the builder `0x1804d8700` to get the exact filename rule; then
  reproduce it in the host for our target's model path.
- **W3 — FWD3 Wwise name→id.** Either (a) write a small `.bnk` HIRC reader (parse event ids) and
  match against the game's name table, or (b) disassemble the client's PostEvent wrapper to see
  the name→id hash it uses. Then `PostEvent(id)` the hit sound.
- **W4 — FWD4 engine `.Sfx` spawn.** `dump_fn_disasm.py` on the engine DLL for the SFX factory /
  the tag-spawn caller; recover the correct owner chain so `RC_Shim_SfxPlay` stops faulting, then
  play `AOESelectionSFXFile` at the AoE centre.
- **W5 — FWD6 scripts.** Build the Lua 5.1 VM (`lua51_disasm.py` already parses bytecode) + an
  engine-API shim; run the residual scripts.

## Method / rules

1. Copy the DLL/bank out of the install into an ignored dir; analyze the copy (never write to the
   install). Use `tools/` scanners, never `Read` on binaries.
2. Every claim cites module + RVA + confidence + evidence path; no assumption-as-fact.
3. Verify each fix with a driven repro + the must-stay-green gates; proof under `proof/netcode/`.
4. Order: **W2 first** (single-function dump, already done), then **W1** (minidump), then W3/W4/W5.

## Reproduce

```
.venv\Scripts\python.exe tools\pvp\dump_fn_disasm.py ^
  "<client>\bin64\JX3RepresentX64.dll" --names behit_names.txt --out-dir <out>
.venv\Scripts\python.exe tools\camera\minidump_exc.py <crash.dmp>
```
