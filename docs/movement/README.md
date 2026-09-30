# docs/movement - movement / gravity / collision index

Terrain, gravity, jump/fall and collision research. Bake output: `tools/bake_map_collision.py` -> `engine_host_spike/collision_data/`.

| Doc | Title |
|---|---|
| `FULL_MAP_COLLISION.md` | Full map collision from the real client data - how it was achieved |
| `JX3_CHARACTER_MOVEMENT_RESEARCH.md` | JX3 character movement & turning — research |
| `JX3_COLLISION_SYSTEM.md` | JX3 Collision & Physics — Full-System Analysis and Reproduction Reference |
| `JX3_DOUBLE_JUMP_RESEARCH.md` | JX3 二段跳 (double jump / jump chain) — research + client reproduction |
| `JX3_GRAVITY_RESEARCH.md` | JX3 gravity system — static research notes |
| `JX3_QINGGONG_BEHAVIOR.md` | JX3 轻功 behavior — full client-side analysis (stages, WW, fall/turn) |
| `REAL_CLIENT_MAP_COLLISION.md` | Real client map collision — recon + working host probe |
| `REBORN_JUMP_FALL_SPEC.md` | REBORN — JX3 jump & fall reproduction spec |
| `STRUCTURE_COLLISION_RESEARCH.md` | Structure collision research — how the game handles houses/walls |

## Tools

| Tool | What |
|---|---|
| `tools/gravity/parse_jump_tables.py` | parse `JumpParam/JumpFrameParam/Sprint/SkillMove.tab`; `--summary`, `--chain`, `--json`, `--csharp-out` |
| `tools/gravity/verify_model.py` | numeric gates for the jump/fall model (incl. the 二段跳 chain) -> `proof/gravity/verification.txt` |
| `tools/movement/find_xrefs.py` | PE call/field xref and window disassembly helper |
