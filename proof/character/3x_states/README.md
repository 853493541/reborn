# proof/character/3x_states — W6 swim / fly / 轻功 runs (2026-10-06)

Branch `agent/3x-states`, build `reborn_client_3x_states.exe` (git 3010c88 / 42a6af0).
Engine root `C:\SeasunGame\MovieEditor`, `RC_STARTUP=nodb`, screenshots disabled
(`RC_SHOTS=999999`) so the default 3 s/8 s proof screenshot stall does not pollute
the chain phase timing (the 202956 log is the polluted first run, kept for the record).

| log (captured) | run | env | what it shows |
|---|---|---|---|
| `reborn_20261006_202918.log` | swim idle | `RC_WATER=18700,33600,19300,34100,1062,762 RC_SWIM_LOG=1 RC_AUTORUN=6000` | `state=7`, `float=994` (= surface 1062 − 0.589×116), `depth=68` |
| `reborn_20261006_203122.log` | 轻功 chain + suspend harness | `RC_DEMO_STATES=1 RC_DJUMP=chain RC_CHAIN_LOG=1 RC_SUSPEND_DEMO=1 RC_AUTORUN=10000` | J0/J1/J2 presses; `fly: FlyTo state=0x1F (harness)` / `EndFlyJump state=0x21 -> 4`; landing |
| `reborn_20261006_203200.log` | chain End phase | `RC_DEMO_STATES=1 RC_DJUMP=chain RC_CHAIN_SEG=20 RC_CHAIN_LOG=1 RC_AUTORUN=9000` | `chain: end seg=3 end=60,90,11 vy=702` then `jumpCount := 1` (land n=1) |
| `reborn_20261006_203228.log` | swim moving + exit | `RC_WATER=... RC_DEMO=1 RC_SWIM_LOG=1 RC_AUTORUN=9000` | `state=7` → `state=6` at `300u/s`, float held; exit at the box edge → grounded walk |
| `run_20261006_210340.txt` | suspend float law | `RC_DEMO_STATES=1 RC_DJUMP=chain RC_SUSPEND_DEMO=1 RC_SHOTS=999999 RC_AUTORUN=10000` | `fly: float floor y=1218 (terrain+0x100, PVM 0x31913C state 0x1F)`, EndFlyJump 0x21 -> 4 |
| `reborn_20261006_203258.log` | swim jump | `RC_WATER=... RC_DEMO_STATES=1 RC_SWIM_LOG=1 RC_AUTORUN=8000` | `swim: jump state=8 (prov J0 impulse)`; re-entry `state=7` |
| `reborn_20261006_202956.log` | first chain run (polluted) | as 203122 without `RC_SHOTS` override | shows the default 8000 ms screenshot blocking the loop (~5.4 s) — kept as the stall record |

Registered provisionals (AGENTS §6; details in `docs/character/3_5_3_7_RAGDOLL_SWIM_FLY.md`):
water source (`RC_WATER` boxes), swim-jump impulse (J0 triple), swim buoyancy hold
(float height), chain segment length (`RC_CHAIN_SEG`, default 51 ticks), suspend float **law now decoded**
(floor = terrain + 256 u) with the entry trigger still a harness.

## Water-entry AV — root cause + seed fix A/B (2026-10-06)

| log (captured) | run | env | what it shows |
|---|---|---|---|
| `crash_before_215647.txt` / `crash_before_220045.txt` | pre-fix drive/spawn into the NE basin | — | log ends abruptly, WER `KG3DEngineDX11EX64+0x12282B3` |
| `crash_seed_off_221659.txt` | seed off, stand at (64293,-125,55238) | `RC_SEED_RCPISCENE=0 RC_SPAWN=64293,-125,55238` | crash ~2 s after spawn (WER 22:17:12) |
| `seed_on_stand_221934.txt` | seed on, same spot | `RC_SPAWN=64293,-125,55238` | `lazyseed ... seeded=121`, 15 s standing, `DONE` |
| `seed_on_drive_222125.txt` | seed on, drive (0.99,0.17) from (63098,242,55063) | `RC_DEMO_COLLIDE=1 RC_DEMO_DIR=0.99,0.17` | through the basin to y=-551, `DONE` |
| `seed_off_race_clean_222616.txt` | seed off, same spot (race variance) | `RC_SEED_RCPISCENE=0` | clean `DONE` — the guard race is timing-dependent (3 of 4 seed-off attempts crashed) |
| `seed_on_final_222651.txt` | final build, seed default on | `RC_SPAWN=64293,-125,55238` | `lazyseed ... seeded=121`, `DONE` |
| `seed_rcpiscene_slots.txt` | evidence | — | crash site disasm, FNV validation, all 129 slot RVAs, A/B summary |

## P3 water region (real RegionInfo) — driven proof (2026-10-07, `agent/3x-integration`)

Build `reborn_client_3x.exe` (git f4c59cb+, `RC_STARTUP=nodb`, map 龙门寻宝). The water
source is now the shipped RegionInfo region tree (`client/WaterRegions.cs`, one region at
world (0,0)..(102400), surface 150) instead of the 4096\*Scale P1 heuristic.

| log (captured) | env | what it shows |
|---|---|---|
| `p3_walkin.txt` | `RC_SPAWN=67040,936,65000 RC_DEMO_COLLIDE=1 RC_DEMO_DIR=0,-1 RC_SWIM_LOG=1` | grounded run from the shore (ground 969 -> -471): `swim: enter state=6 surface=150 depth=634 T=627`, then `state=6 ... y=150` float + swim clip; depth grows to 1061 as it swims into the basin |
| `p3_idle_jump.txt` | `RC_SPAWN=67040,150,60000 RC_DEMO_JUMP=1` | spawn in the lake -> `swim: enter state=7 ... depth=962` (idle float, y=150, swim-idle clip); `swim: jump state=5 ... vy=702`; re-entry `enter state=7` (fall-in float) |
| `p3_exit.txt` | `RC_SPAWN=67040,150,60000 RC_DEMO_COLLIDE=1 RC_DEMO_DIR=0,1` | swim north, depth falls 960 -> 650: `swim: exit state=6->0 depth=625 T=627 y=150 ground=-475`, then grounded land run (y 150 -> 719 -> 959) |

Screenshots + numeric fingerprint (`tools/proof/image_stats.py --grid 4x4`): the in-water
frames are dark low-luma (walkin 14 s mean `#333035`, 20 s `#313544`; exit 3 s `#353640`,
8 s `#322D2E`) vs the brown land frames (walkin 3 s `#765E41`, exit 12 s `#775E3C`,
16 s `#7B6342`) - land vs in-water is separable numerically.

Water **rendering** is still the known asset boundary (`FluxWaterDefault_BWater.JsonIns`
absent from the MovieEditor host -> `EnableFluxWaterSimulation` E_FAIL); these runs prove
the interaction region/state, not the flux render. The engine's region hole/normal masks
(which narrow the shoreline) are not decoded, so the host floods the region AABB wherever
`ground < surface` - registered below.
