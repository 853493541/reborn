# Loot capture decoder (JX3 BR modes)

Decodes the container-spawn and loot-roll messages recovered in
`docs/netcode/JX3_LOOT_PROTOCOL_LAYOUTS.md`, and turns a packet log into:

- `spawns.csv` — every container spawn: template id, global id, position (x/y/z),
  region, kind, link/owner, flags
- `heatmap.txt` — ASCII spawn-density map
- `loot.csv` — **the rolled contents** per container (type/index/count/flag/extra)
- `takes.csv` — take requests (`DoApplyLootList` 0x4D, `DoLootMoney` 0x51)
- `summary.txt` — counts, template histogram, per-container loot histogram

## Input format (JSONL)

One record per raw packet buffer as the client handler sees it:

```json
{"dir":"S2C","opcode":900,"buf":"<hex bytes>"}
{"dir":"C2S","opcode":77, "buf":"<hex bytes>"}
```

`buf` is the **handler buffer** (offset 0 = first byte the handler reads). The capture
agent should log, per invocation, `nSize` bytes from the pointer argument
(`rdx` for `On*` handlers, `rcx`/`this` excluded; `rdx`+`r8` for `KPlayerClient` methods).

## Usage

```powershell
python tools\netcode\loot\capture.py selftest
python tools\netcode\loot\capture.py decode capture.jsonl --out out --detect
# or with a known opcode map:
#   {"neww":900,"state":901,"loot":902}
python tools\netcode\loot\capture.py decode capture.jsonl --out out --opcode-map map.json
```

`--detect` classifies records by payload shape when opcodes are unknown (the S2C opcode
IDs for these handlers have not been recovered statically; a capture reveals them once
and they can be pinned in `map.json`).

Join the outputs with `settings\DoodadTemplate.tab` (extracted via
`extract_pak_paths.py`, `MapName=沙漠风暴`) to get human-readable container names and the
drop-table each container uses — that is how per-container roll distributions become
readable.

## Status

- Decoders implemented offline-first; `selftest` (8 checks) and a synthetic 170-record
  end-to-end run pass (`proof/netcode/loot_demo_out/`).
- Remaining to make it live: a capture agent that logs the handler buffers (same pattern
  as any API hook / debugger breakpoint logger). No client modification is required for
  offline replay of captures produced this way.
