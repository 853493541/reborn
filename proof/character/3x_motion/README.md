# proof/character/3x_motion — SPEC_MOTION §5 acceptance run (2026-10-07)

Branch `agent/3x-motion`, build `reborn_client_3x_motion.exe` (git 70fbf88 + WIP),
engine root `C:\SeasunGame\MovieEditor`, `RC_STARTUP=nodb`, driven runs with
`RC_SKILL_AT` + `RC_SKILL_MOVEID` (row from `settings/SkillMove.tab`). Logs are the
verbatim run logs; the per-second `t=` lines now carry `camYaw=`/`camPitch=` (the
camera fingerprint) and `camPos=` (the engine camera position read-back).

## SPEC_MOTION_P2 (per-skill rebuild, 2026-10-07)

Branch `agent/3x-motion` @ 94c9efa + WIP. `RC_SKILL_ID` selects the host skill
(228 = 太阴指 dash, 1645 = 风来吴山 channel; default 1645 = the demo clip).

| P2 criterion | env | expected | measured | log |
|---|---|---|---|---|
| 1 太阴指 dash (228) | `RC_SKILL_ID=228 RC_SKILL_AT=3000` | 16 x nSpeed(60) = 960 u along heading facing+0x80, facing unchanged | `skilldash: start/end frames=16 speed=60 heading=192`; Δz = **−960 exact**; facingYaw 0.000 unchanged; moved=960 | `p2_dash_228_082017.txt` |
| 2 太阴指 camera | same | yaw/pitch unchanged; position follows the dash | `camYaw=-1.173 camPitch=0.219` before/at/after the dash; camPos z 33113 → 32128 (≈ −960 + spring) | `p2_dash_228_082017.txt` |
| 3 FLWS no displacement | `RC_SKILL_ID=1645 RC_SKILL_AT=3000` | 0 scripted translation; pos delta 0 | pos (18991,962,33853) constant through the channel; `skillchannel: start ... (no displacement; spin is the clip)` | `p2_flws_stand_081504.txt` |
| 3 FLWS walk live ×1.10 | `+ RC_HOLD_W=3500,9000` | WASD moves at ×1.10 | `spd=330u/s(RUN)`; z 34007→34667 in 2 s = 330 u/s | `p2_flws_walk_082047.txt` |
| 3 FLWS jump rejected | `+ RC_DEMO_JUMP=1` | jump blocked (buff 1856) | `skillchannel: jump rejected (buff 1856 - no jump during the channel)` at t≈5.5 s; character stays grounded | `p2_flws_walk_082047.txt` |
| 4 FLWS camera yaw | standing/walk | no yaw change from spin/facing/animation | `camYaw=-1.173 camPitch=0.219` constant through the channel in both runs | `p2_flws_stand_081504.txt`, `p2_flws_walk_082047.txt` |
| 4 camera position, no input | idle baseline vs standing cast | no input -> position does not move | **idle no-cast run: camPos exactly constant (19299,1437,33113) x15 samples.** Standing cast: camPos ±26 u slow sway during the clip, settling exactly at the clip end — the C1 **head-bone anchor** follows the spin clip's head-bone sway. Registered camera-workstream boundary (re-open: anchor on the logic position + fixed offset; yaw/pitch are unaffected) | `p2_idle_baseline_081757.txt`, `p2_flws_stand_081504.txt` |
| 5 no invented data | — | host map = primitives, not rows | `SkillMotionMap`: 228 = dash(16,60,+0x80), 1645 = channel(mul 1.10, jump blocked); SkillMove rows only via `RC_SKILL_MOVEID` | code |
| 6 logging | — | primitive kind/frames/speed/heading/moved/camera | `skilldash: start/end ... camYaw= camPitch=`; `skillchannel: start/end/jump rejected` | all P2 logs |
| regression §5 rows | `RC_SKILL_MOVEID=876` | standing/moving row behavior preserved | standing: moved=0, camera yaw/pitch constant; moving (W held): input gated 8 ticks then RUN resumes | `p2_reg_stand_row_082143.txt`, `p2_reg_move_row_082211.txt` |
| gates | — | build 0 / smoke ALL PASS / collision 36/36 | all green | — |

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

## Camera anchor fix (2026-10-07, `08a9bbc`) — FLWS spin killed

Rule decoded from both builds (`JX3RepresentX64.dll`): `AdjustCharacterCameraObjectY`
(HD 0x180AC8980 / EXP 0x1808DCBxx, byte-identical logic) reads the animated
`Bip01 Head` only when `0.3 > ratio` AND (`[ctrl+0x5C]==8 || lookup`) AND
`0.5 > |[ctrl+0xD0]|`; third-person distances never take the branch, so the anchor
is the character LOGIC position + a constant height offset. Host: cache the head
bone's REST local translation once (idle clip) and compose it as a constant offset
(`RC_CAM_TRACE=1` logs the per-frame anchor + engine camera).

| run | env | expected | measured | log |
|---|---|---|---|---|
| FLWS standing cast, whole clip | `RC_SKILL_ID=1645 RC_SKILL_AT=3000 RC_CAM_TRACE=1` | camPos constant across the clip (was ±26 u sway) | `ctrace` 1963 frames: anchor `(18989.4,1031.5,33853.5)` + campos `(18993.0,1582.0,33111.1)` **identical** t=642→t=15774; camYaw/camPitch constant | `p2_cam_fix_flws_const_173958.txt` (user's shorter copy: `p2_flws_camconst_fixed.txt`) |
| 太阴指 dash follow | `RC_SKILL_ID=228 RC_SKILL_AT=3000 RC_CAM_TRACE=1` | positional follow unchanged | anchor/campos translate with the −960 u dash (campos z 33112→32152), camYaw/camPitch constant | `p2_cam_fix_dash_follow_174028.txt` (user's copy: `p2_dash228_follow_fixed.txt`) |
| idle no-cast baseline | `RC_CAM_TRACE=1` (no cast) | constant | campos single value `(18993.0,1582.0,33111.1)` for the whole run (only the pre-settle init sample differs) | `p2_cam_fix_idle_baseline_174214.txt` |
| gates | — | build 0 / smoke ALL PASS / collision 36/36 | green (canonical `reborn_client_3x_motion.exe`, git 08a9bbc) | — |
