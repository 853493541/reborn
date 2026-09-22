# JX3 / reborn research index

All static, read-only research on the local JX3 client (client build on this
machine), used to recreate the runtime model for reborn. Branch:
`research/jx3-netcode`.

## Canonical constants (calibrated)

| Fact | Value | Evidence |
|---|---|---|
| engine unit | **1 cm** | adult male HD mesh 181.64 units = 1.82 m |
| 1 尺 (player-visible) | **64 units = 0.64 m** | 4 skill-script conversion sites + mesh calibration |
| 1 m | 100 units = 1.5625 尺 | derived |
| gameplay frame | **1/16 s** (GAME_FPS = 16) | script comments “16帧等于1秒” |
| angle unit | `nAngleRange × 1.40625 = degrees` (256 = 360°) | `--施放角度,全角256`, 120° file matches 85 raw |
| frame max payload | 0x8000 (32,768 B) | `DoRoutineSync` guard |
| ping / dead timeout | 3000 ms / 12000 ms | net thread disasm |
| adult male height | 181.6 units = 1.82 m | mesh census |
| 花萝 (F1) height | 120–150 units = 1.20–1.50 m | full-body meshes only |
| terrain tile | 51,200 units = 512 m | map `_Setting.ini` tileSize × scale |
| zone grid 8×8 tiles | 409,600 units = 4.1 km | terrain descriptor |

## Documents

| Doc | Content |
|---|---|
| `UNIT_SCALE_AND_CHARACTER_SIZE.md` | **canonical unit system**: 1 u = 1 cm, 尺 = 0.64 m, character vs map sizes |
| `JX3_NETCODE_RESEARCH.md` | static map of client/server netcode (CoreNet, KPlayerClient, sync/reconnect) |
| `JX3_PROTOCOL_SPEC.md` | frame layout, serial/ack reliability, handshake, ping, protocol IDs |
| `REBORN_SERVER_SPEC.md` | implementable server+client contract (our own opcodes, rates, AOI, combat) |
| `SKILL_DATA_RESEARCH.md` | skill scaling (attack %/weapon %/adaptive), dash attributes, tooltip DB, 尺 conversion |
| `SKILL_DATA_EXTRACTION.md`, `SKILL_MOTION_METHOD.md` | skill/asset extraction methods |
| `JX3_CAMERA_RESEARCH.md`, `REBORN_CAMERA_SPEC.md` | camera behaviour research + spec |
| `JX3_MODE_*.md`, `JX3_LOOT_PROTOCOL_LAYOUTS.md` | 绝境 mode loading and loot protocol layouts |
| `JX3_DROPS_RESEARCH.md` | **drops session record (audited)**: containers, tables, wire formats, dead ends, confidence |
| `JX3_MODE_LOOT_SYSTEM.md`, `JX3_MODE_SPAWN_RULES_SEARCH.md` | loot container schema / spawn-rule hunt + dead-end log |

## Tools (`tools/netcode/`)

| Tool | Purpose |
|---|---|
| `scan_net_strings.py`, `extract_strings.py`, `pe_imports.py`, `find_net_modules.py` | binary string/import recon |
| `xref_string.py`, `dump_protocol_ids.py` | string→code xref disassembly, C2S protocol catalog |
| `scan_text_assets.py` | CJK text asset scanning |
| `scan_skill_scripts.py`, `scan_skill_ranges.py`, `make_range_examples.py`, `query_skill_tooltip.py` | skill formula/range/tooltip extraction |
| `lua51_constants.py` | JX3 Lua 5.1 bytecode constant dumper (32-bit header, size_t=4) |
| `measure_skill_motion.py`, `dump_motion_floats.py` | tani/ani motion extraction |
| `character_mesh_census.py`, `measure_character_size.py` | mesh height census / unit calibration |
| `loot/capture.py` | loot/doodad capture decoder (spawn positions, rolled contents, takes) + selftest |
| `extract_pak_paths.py`, `mine_item_scripts.py`, `dump_mode_inventory.py` | PakV4 path extraction, item-pool catalog, container inventory |
| `lua51_dump.py`, `gbk_grep.py`, `search_tree.py` | Lua 5.1 bytecode proto/const dump, GBK/UTF-16 binary grep, tree token search |
| `reference/jx3_model.py` | runnable reference server+client (10/10 smoke) |

## Evidence (`proof/netcode/`)

`*_net_strings.txt` (binary string dumps), `exports_*.txt`, `imports_*.txt`,
`disasm/` (annotated transcripts), `c2s_protocol_catalog.tsv`,
`skill_data/` (scaling/ranges/tooltips/bytecode constants),
`character_size/` (mesh census), `skill_motion/` (tani motion curves).

Reproduce commands live at the bottom of each doc.
