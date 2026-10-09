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

- **W1 — FWD1 per-tani AV (39 abilities). DUMP CAPTURED (faulting site known).**
  WER LocalDumps works for our exe (`%LOCALAPPDATA%\CrashDumps\reborn_client_skillv6.exe.<pid>.dmp`).
  Reproduced 65068 (blacklist emptied) -> crash; `tools/camera/minidump_exc.py` on the dump:
  `exc code=0xC0000005 addr=ntdll.dll+0xFA7D`, `rdx=rsi=0xFFFFFFFA` (a bogus size / bad source).
  Stack walk (faulting thread, from rsp): `ntdll+0xFA7D` -> `ntdll+0x26844` -> **`jemallocX64.dll+0x11001`**
  (allocator memcpy/realloc) -> **`KG3DEngineDX11EX64.dll+0xE2E842`** -> `+0xE2DADA` -> `+0xE2D706`
  -> `+0x10D0A6` -> `+0xE2768F`. So the AV is a **bad-size `memcpy` through jemalloc, called from
  the engine's animation code** (`+0xE2E842` is a std::map/tree lookup region) with a bad node/size.
  Root: the tani drives the engine anim code into a corrupt/absent map entry (bad size 0xFFFFFFFA),
  most likely a bone/skeleton/animation id the host model lacks.
  **Status:** heuristic stack walk (rbp-chain broken) + return-address validation both fail
  because the dump does not contain the caller frames' **code pages** (`d.read` returns None at
  `ntdll+0x26844`, `jemallocX64.dll+0x11001`, `KG3DEngineDX11EX64.dll+0xE2E842` even with
  DumpType=2 / 118 MB). Only the faulting pages are captured. So the exact engine caller can't be
  recovered from these dumps. Unblock = capture with a debugger (x64dbg/`cdb`), or force full
  memory capture, then unwind via `.pdata`. Faulting site + module + bad value (0xFFFFFFFA) are
  known; the fix (supply the anim/bone binding) is a large engine-RE sub-task.
- **W2 — FWD2 default be-hit animation. DONE (2026-10-09; implemented + verified A/B).**
  - **Default NPC rule:** `KRLCharacter::BeHittedByPlayer` @ `0x1804d0220` uses the literal
    `bat01.ani` (string @0x180caf980) for npc_source targets via the sibling builder
    `0x1804d8700` (template `data/source/npc_source/%s/动作/%s_%s` @0x180cac990). Kind map in
    `BeHittedByNpc` @ `0x1804cfb50`: kind 1→`[rsi+0xa4]`, 2→`[rsi+0x94]`, 6→`[rsi+0x8c]`
    (only for non-empty skill-caster cols, none in the roster). `npc_animation.txt` is
    runtime-generated (not shipped in the readable paks). Client plays the target's `_bat01`
    sibling of its idle anim; proof `proof/netcode/skillv6_fx4_behit_20261009.txt`.
  - (historical detail below)
  - `KRLCharacter::PlayBeHittedAnimation` is virtual; the caller passes `pcszAni`.
  - `BeHittedByNpc` @ `0x1804cfb50`: gets the target's anim model (`pSkillCasterModel`,
    `[rip+0xa0e2e5]+0x1a0`), reads a **kind** from the frame data (`0x180003a6c`) and selects one of
    the target's loaded be-hit anims: kind 1 -> `[rsi+0xa4]`, 2 -> `[rsi+0x94]`, 6 -> `[rsi+0x8c]`;
    else builds a sibling path via `0x1804d8700` with the template
    **`data/source/npc_source/%s/动作/%s_%s`** (string @ `0x180cac990`). Then `PlayBeHittedAnimation`.
  - `BeHittedByPlayer` @ `0x1804d0220`: tests the target model path for `npc_source/a` /
    `NPC_source\A` -> template path; else the player/global branch (`[rip+0xa0dcae]+0x1a0`).
  - So the be-hit anim is a **kind-selected animation from the target's set** (or a model-sibling
    path), not a fixed id. To implement FX4 we must load the target's be-hit anims + pick by kind
    (our dummy's set) — non-trivial; keep documented until we have the target anim set.
  - Remaining: the target's be-hit anim set source (which table fills `[rsi+0x8c/0x94/0xa4]`).
- **W3 — FWD3 Wwise name→id. INVESTIGATED (2026-10-09) — BLOCKED; exact next probe named.**
  - Wwise event ids here are **authored, not name-hashed**: in `wwise-soundbank-index.json`
    (`...\jx3-web-map-viewer\log`, 230 banks / 23,579 events) adjacent names get consecutive ids
    (`Play_AiLi_Skill02`=48696777, `Skill01`=48696778), and neither FNV-1/FNV-1a/djb2 of the
    FLWS name matches its id (3378728138). So a hash is not derivable.
  - The `hit_target_sound.txt` SoundEvent names (`SoundID 0 -> f2sgb11BangFaGongJi07`,
    `TianCe_Body_L01`, `l_LongYaBeiJi`, …) are **not present** in the index (any bank, exact or
    prefix/lower/substring), nor in `resource_sfx`/`custom_sfx`/`WwiseSound`/`WwiseMIDISound`.
    The game client's `Behit.bnk` holds `Play_BeHit_*` (22), the `TianCe` bank holds
    `TianCe_TianCe_Behit_Behit_*` — neither matches the authored hit-target names.
  - Client path (`JX3RepresentX64.dll`, read-only disasm): `ProcessSkillEffectSound` @
    `0x18059fdc0` looks up `pcHitTargetSoundModel` by (SoundID, TargetType), asserts
    `pcHitTargetSoundModel->szEvent`, then hands the **name string** to the Wwise manager vtable
    `[r8+0x3f8]` (`HitTargetSound` = `represent/skill/hit_target_sound.txt`, filepath.ini).
  - `native/sound_probe.cpp` already hooks `AK::SoundEngine::PostEvent` incl. the `const char*`
    overload (`g_tPostEventStr`), so a **by-name PostEvent** is available in the host.
  - **Next probe:** (a) load the bank that carries these names — find it by scanning the game
    client's Wwise banks for the exact strings `TianCe_Body_L01`/`l_LongYaBeiJi` (the index's
    parser did not surface them); then post by name into the loaded bank; OR (b) expose
    `RC_SoundProbe_PostEventName(const char*)` and try the names against the loaded bank set and
    read Wwise's return (invalid id vs played). Do not guess a numeric id.
- **W4 — FWD4 engine `.Sfx` spawn. INVESTIGATED (2026-10-09) — the direct factory call cannot be
  made correct from the host.** `KG3D_CreateSFXFromFile` @`0xBE4000` (ME build) is called by
  `KG3D_SFXModel::BindData` @`0x180e3412a` with tag-context args: a1 owner, a2 path, a3=`[tagobj+0x38]`,
  a4=a built string, a5=0, a6=`tagdata+0x288` (matrix), a7=`[tagdata+0x58]` flag, **a8=&out slot**.
  `native/sfx_shim.cpp RC_Shim_SfxPlay` passes a3=NULL, a4=&outParam, a7=0, a8=owner — all wrong —
  and the tag-context object/path simply don't exist in the host (it plays a raw dummy model's tani
  via `PlayAnimation`, not a `KG3D_SFXModel` tag), so no arg fix can supply them. **Route:** spawn
  the landing `.Sfx` through the engine's own animation/SFX **tag** path (so the tag args exist), or
  a scene-level standalone-effect API. Evidence: `proof/netcode/skillv6_sfx_argmapping_20261009.txt`.
  **Refined:** the fault is a bad-size `memcpy` via `jemallocX64.dll` (ntdll/jemalloc frames; the
  printed `fault_rva` is meaningless outside the engine) reached from `KG3D_CreateModelFromFile` —
  the **same class as W1**, so the arg fix alone would not clear it. `KG3D_CreateSFXFromFile`'s a8 is
  a creation-params struct (`[a8+0x15]`), not an out slot. Evidence
  `proof/netcode/skillv6_sfx_faultsite_20261009.txt`.
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
