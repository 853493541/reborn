# docs/movement - movement / gravity / collision index

Terrain, gravity, jump/fall and collision research. Bake output: `tools/bake_map_collision.py` -> `engine_host_spike/collision_data/`.

| Doc | Title |
|---|---|
| `CLIENT_COLLISION_IMPROVEMENT_PLAN.md` | Main client collision — gap audit (holes, capsule, slope) + fix plan |
| `COLLISION_SYSTEM_COMPARISON.md` | Host vs game collision system — full subsystem/flag/data inventory incl. what we do NOT have |
| `COLLISION_TEST_LIST.md` | Full check results + manual in-game test list (current build) |
| `COLLISION_SYSTEM_STATUS.md` | Collision system status audit — implemented / missing / wrong per subsystem |
| `FULL_MAP_COLLISION.md` | Full map collision from the real client data - how it was achieved |
| `JX3_CHARACTER_MOVEMENT_RESEARCH.md` | JX3 character movement & turning — research |
| `JX3_COLLISION_SYSTEM.md` | JX3 Collision & Physics — Full-System Analysis and Reproduction Reference |
| `JX3_DOUBLE_JUMP_RESEARCH.md` | JX3 二段跳 (double jump / jump chain) — research + client reproduction |
| `JX3_GRAVITY_RESEARCH.md` | JX3 gravity system — static research notes |
| `JX3_STEP_FORGIVENESS_RESEARCH.md` | Step forgiveness — recovered engine CCT stepOffset/slope defaults, what is and is not knowable |
| `REAL_CLIENT_MAP_COLLISION.md` | Real client map collision — recon + working host probe |
| `REBORN_JUMP_FALL_SPEC.md` | REBORN — JX3 jump & fall reproduction spec |
| `STRUCTURE_COLLISION_RESEARCH.md` | Structure collision research — how the game handles houses/walls |

## Tools

| Tool | What |
|---|---|
| `tools/gravity/parse_jump_tables.py` | parse `JumpParam/JumpFrameParam/Sprint/SkillMove.tab`; `--summary`, `--chain`, `--json`, `--csharp-out` |
| `tools/gravity/verify_model.py` | numeric gates for the jump/fall model (incl. the 二段跳 chain) -> `proof/gravity/verification.txt` |
| `tools/movement/find_xrefs.py` | PE call/field xref and window disassembly helper |
| `tools/collision/check_hole_mask.py` | Convert an extracted `.hlb` hole mask (flip rule applied) and A/B it against a client `RC_HOLE_DUMP` engine dump |
| `tools/collision/spot_ab.py` | Solver-vs-engine A/B at field spots: capsule scan on our baked bin vs the engine PxMeshQuery scan recorded in `proof/movement/phys_engine_vtables.txt` (2026-10-02 c) |
| `client/collision_selftest.cs` | Offline FoliageCollision gate (36 checks, no engine/assets); built as `bin64\collision_selftest_<exe>.exe` by `client\build_client.cmd`; `audit` mode = map-wide wall-face capsule sweep (walk-through detector) |
| `tools/export_camera_flags.py` | Per-mesh `bObscatleCamera` extraction -> `camera_mesh_flags.json` -> `.cflags` sidecar |
| `tools/bake_map_collision.py` | Bake per-map foliage/structure collision bins from the pak |
| `tools/collision/pxdiff.py` | Phase-1 grid A/B diff: parse engine `PX_GRID` lines vs solver `GRID` lines, per-y state compare + mismatch report |
