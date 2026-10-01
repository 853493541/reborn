# Collision build — full check + test list (2026-09-30)

Branch `agent/collision-improvement`. Everything below is the current
verification state; run the automated gates first, then walk the manual list.

## 1. Automated gates (all green at this tip)

| Command | Expected |
|---|---|
| `.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py` | all 10 checks PASS |
| `.venv\Scripts\python.exe tools\gravity\verify_model.py` | model self-consistent |
| `.venv\Scripts\python.exe tools\netcode\loot\capture.py selftest` | SELFTEST PASS |
| `bin64\collision_selftest_reborn_client_collision.exe` | 25/25 PASS |
| `native\build_shim.cmd` | `RC_Shim_*` exports linked (run with NO reborn client alive) |
| `ui-process-app` `--selftest` | needs a desktop session (headless exits 1; app untouched by this branch) |

Automated in-engine routes (env-driven; logs in `bin64\reborn_out\`):

```powershell
# rug crossing (must stand ON the rug at y=924, hits=0, no shake)
RC_DEMO_COLLIDE=1 RC_SPAWN=19240,0,36435 RC_DEMO_DIR=1,0  reborn_client_collision.exe
# cabinet press (must hold at z=36704 with small propfix pushes, never inside)
RC_DEMO_COLLIDE=1 RC_SPAWN=19200,0,36650 RC_DEMO_DIR=0,1  reborn_client_collision.exe
# jump/walk demo (walk 90 u/s, run 300 u/s, jump ~1 s air)
RC_DEMO=1                                                 reborn_client_collision.exe
# other maps load smoke
RC_MAP=data\source\maps\白龙绝境\白龙绝境.jsonmap          reborn_client_collision.exe
```

Latest results: gates green; rug `19840,924` / `hits=0`; cabinet held at
`z=36704` (`propfix push oz<=20`); demo walk/run 90/300; all five maps load
their re-baked bins (`reborn_20260930_171559/171657/171754.log` and the map
smokes `170531/170643`).

## 2. Manual test list (feel checks)

For every "fail", press **COPY LOG** (top-right) and **F9**; the log names the
exact blocker (`blocked by inst=… mesh=… top=… feet=…`) or the prop contact
(`propfix … push …`).

1. **House rug (龙门寻宝, x 19290-20089 / z 35871-36670)**
   - Walk onto the rug from any side: you stand on it (HUD y≈924), no jolt, no
     invisible wall at the edge.
   - Pace back and forth over the rug-to-floor step: the camera must stay
     steady (no vertical shake).
2. **Cabinet (x 18728-19351 / z 36721-36821)**
   - Walk into it: you stop at its face (~z 36704); you cannot end up inside.
   - Jump against it: you cannot jump in; landing on top stays possible from
     high ground.
   - Pressing into it must not bounce the camera (wall-like pushes only).
3. **Jump**
   - Space on flat ground: ~1 s air, clean landing, no double-grounding.
   - Jump while running; jump against a wall: you cannot climb it.
4. **Steps/ledges**
   - The ~51 u house floor edge is walkable (climb); taller ledges block and
     require a jump.
   - Thin rails/planks at walls do not stop you (step budget 64 u).
5. **Props**
   - Wooden boxes can be jumped onto (standing on top works).
   - You can never end up inside a cabinet/box/barrel; interiors push you out.
6. **Flags/lanterns/racks/straw mats** (bAutoProduceObstacle=0): walk-through is
   correct — the shipped game data says those have no physics.
7. **Other maps** (optional feel): launch with `RC_MAP=...` for 白龙绝境 /
   天原绝境 / 海岛绝境 / 龙门寻宝_夜晚; movement and ground should behave the
   same (same solver + per-map baked bins).

## 3. Known boundaries (do not report as bugs)

- Door/doodad open state, movable obstacles and the runtime static-object set
  are server-owned (not in the client install); the host uses the baked client
  geometry.
- Solid-prop push is a registered host proxy (name taxonomy + AABB), re-open
  with the server `bUnitWalkable` values.
- `ProcessDropSpeed` slope *slides* need engine-scene cell data (not shipped).
- Sprint hold (double-tap W) is a host test convenience; the game has no plain
  hold-to-sprint constant.

## 4. Useful switches

`RC_STEP_HEIGHT` (default 64) · `RC_PROP_SOLID` (1/0) · `RC_OBST_FLAGS` (1/0) ·
`RC_CAM_YFOLLOW` (1/0) · `RC_CAM_YDBG` (raw vs smoothed camera Y) ·
`RC_CAM_OBSTHZ` (20) · `RC_CAM_HITWIN` (0.4) · `RC_SPAWN=x,y,z` ·
`RC_DEMO_COLLIDE=1 RC_DEMO_DIR=dx,dz` · `RC_MAP=<vfs jsonmap>` ·
`RC_PHYS_PROBE=1` (P5 engine-stack probe).
