# proof/character/3x_motion — SPEC_MOTION §5 acceptance run (2026-10-07)

Branch `agent/3x-motion`, build `reborn_client_3x_motion.exe` (git 70fbf88 + WIP),
engine root `C:\SeasunGame\MovieEditor`, `RC_STARTUP=nodb`, driven runs with
`RC_SKILL_AT` + `RC_SKILL_MOVEID` (row from `settings/SkillMove.tab`). Logs are the
verbatim run logs; the per-second `t=` lines now carry `camYaw=`/`camPitch=` (the
camera fingerprint).

| criterion (SPEC_MOTION §5) | env | expected | measured | log |
|---|---|---|---|---|
| 1 standing cast, zero row | `RC_SKILL_MOVEID=876` | 0 translation, 0 rotation, camera unchanged over 30 s | `moved=0`, pos/heading unchanged; `camYaw=-1.173 camPitch=0.219` identical t=2s→t=20s | `c1_standing_zero_003320.txt` |
| 2 displacement row (300) | `RC_SKILL_MOVEID=300` | Σ min(VXY,127) along the cast facing | `moved=6289`, end (18991,40142): Δz=+6289 exact (Σ=6289) | `c2_dash_sum_003711.txt` |
| 3 camera-yawed cast | `RC_ORBIT_AT=2000,873` + row 300 | travel along the cast facing, camera does not converge | camYaw −1.173→−2.745 (90°) before cast; travel Δz=+6289 along +Z (facing); camYaw constant during the move | `c3_cam_yawed_003811.txt` |
| 4 moving cast | `RC_HOLD_W=1000,12000` + row 876 | input gated during the move, resumes after; no teleport | RUN → `spd=0` at cast → RUN after `end`; moved=0 (first step 0 ≤ 127) | `c4_moving_cast_003935.txt` |
| 5 turning cast | `RC_MODE=classical RC_RMB=3000,10000 RC_ORBIT_AT=…` + row 700 | facing turns rate-limited; displacement unaffected | body yaw 0→0.47 (= the RMB target 0.462); end pos/heading **identical** to the no-turn control (`moved=77`, heading=192) | `c5_turn_rmb_004300.txt` vs `c5_turn_control_004335.txt` |
| 6 delta row (700 F3 DIR=−128) | `RC_SKILL_MOVEID=700 RC_SMOVE_TICK=1` | heading rotates −π at F3; no auto-face | `smvtick idx=2 heading=192` → `idx=3 heading=64` (Δ−128); body yaw stays 0 | `c6_delta_row_062501.txt` |
| 7a ignore-gravity (200) | `RC_SKILL_MOVEID=200` | VZ held without gravity | y 962→24169 rising with `vy=0` during the move; gravity resumes after | `c7_ignore_gravity_062529.txt` |
| 7b gravity + VZ (55) | `RC_SKILL_MOVEID=55` | gravity integrates, ground snap on landing | y 962→5137 with `vy=-2310` rising; back to `grounded=True` y=952 | `c7_gravity_vz_062557.txt` |
| 8a keep velocity (300) | `RC_SKILL_MOVEID=300` | keep=1 carries the velocity | after `end`: Δz=+3000 in 1.65 s ≈ 1818 u/s (last VXY 120×15=1800) | `c8_keep1_062957.txt` |
| 8b zero at end (300) | `RC_SKILL_KEEP=0` | keep=0 zeroes it | after `end`: z stays 40142 (no drift) | `c8_keep0_062740.txt` |
| 8c death move (2) | `RC_SKILL_MOVEID=2` | death move runs | `end id=2 … death=1 ticks=50 moved=668`; host has no death state (registered boundary) | `c8_death_062806.txt` |
| 9a blend w=128 (300) | `RC_SKILL_BLEND=128` | V×(255−w)/255 | `moved=3132` = 6289×127/255 exact | `c9_blend128_062832.txt` |
| 9b blend w=255 (300) | `RC_SKILL_BLEND=255` | no translation | z stays 33853 through t=8s (VXY zeroed; VZ still applies) | `c9_blend255_062858.txt` |
| 10 gates | — | build 0 / smoke ALL PASS / collision 36/36 | all green; SkillMoveCamera FOV path untouched | — |
| MotionTag container | `RC_SKILL_TANI=<tani>` | type-2 group → 0x188 stream | group0 SFX 0x130 → type-1 opaque scanned to 0x1EE4 → group2 **type=2 MotionTag**, `key 0 time=6 hash='User Define Tag' tags=[(0,8)]`, walk_to_eof | `probe_tani_063259.txt` |

Note (honest): one blend=255 run hit a 5 s engine hitch at cast (concurrent
agents); the z=33853 constant through t=8 s is still exact. The camera fingerprint
is the per-second `camYaw`/`camPitch` pair (numeric), not an image.
