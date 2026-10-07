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
| `reborn_20261006_203258.log` | swim jump | `RC_WATER=... RC_DEMO_STATES=1 RC_SWIM_LOG=1 RC_AUTORUN=8000` | `swim: jump state=8 (prov J0 impulse)`; re-entry `state=7` |
| `reborn_20261006_202956.log` | first chain run (polluted) | as 203122 without `RC_SHOTS` override | shows the default 8000 ms screenshot blocking the loop (~5.4 s) — kept as the stall record |

Registered provisionals (AGENTS §6; details in `docs/character/3_5_3_7_RAGDOLL_SWIM_FLY.md`):
water source (`RC_WATER` boxes), swim-jump impulse (J0 triple), swim buoyancy hold
(float height), chain segment length (`RC_CHAIN_SEG`, default 51 ticks), suspend hover.
