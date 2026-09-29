# JX3 二段跳 (double jump / jump chain) — research notes

**Status:** press semantics + per-school data verified; reproduced in the Reborn client
(`client/JumpTable.cs`, `client/RebornClient.cs`), feature build
`reborn_client_double_jump.exe`. The plain 二段跳 ships as a **provisional**
one-extra-jump model (`RC_DJUMP=flip`) because the game's `J1 takeoff burst +
End-triple flight` phase trigger is still undecoded (§5.1); `RC_DJUMP=chain`
keeps the literal table chain for research.
**Sources:** `JX3_GRAVITY_RESEARCH.md`, `REBORN_JUMP_FALL_SPEC.md`,
`proof/gravity/JumpParam.tab`, `proof/gravity/disasm/kcharacter_jump.txt`,
`proof/gravity/JX3RepresentX64_strings.txt`.
**Last verified:** 2026-09-29 (table extraction + disasm re-read + flip-mode run).

## 1. What the game does

JX3 has a real mid-air jump: pressing jump again while airborne does **not** get
ignored (unlike most MMOs of its era) — it advances a per-character **jump chain**:

| Press | Row | Meaning | Conf |
|---|---|---|---|
| 1 (grounded) | `J0` | 跳跃 | HIGH |
| 2 (airborne) | `J1` | **二段跳** — flip + extra jump; the raw row is the 轻功 launch, phase-shaped | HIGH |
| 3+ (airborne) | `J2`, `J3`, … | further 轻功 chain presses up to `MaxJumpCount[school]` | HIGH |
| beyond `MaxJumpCount` | — | rejected (no jump) | HIGH |

- Represent has a dedicated `DOUBLE_JUMP` state (`JX3RepresentX64.dll` string
  `0x00CBD028`, next to `JUMP` `0x00CBD020`) and a dedicated
  `KRLCharacterFrameData::GetDoubleJumpEndOffset` (`0x00CCC928`) — the animation
  side treats 二段跳 as its own state, not as 跳跃 again. (HIGH)
- Shipped animation `f1b02yd二段跳a.tani` (GATA wrapper, extracted): the wrapper
  resolves and loads (`rc=0`) but **AVs the host** (0xC0000005) within ~1 s; the
  underlying `f1b02yd二段跳a.ani` plays cleanly and is what the client uses.
  (HIGH, `proof/gravity/double_jump_reborn_run.txt` runs B/D vs E)
- The jump clips are **in-place** (no root motion): `f1b02yd小跳b.ani` bip01 Y = 0
  on every frame — the arc is pure movement physics. (HIGH, MIN2 read of the
  staged clip, 2026-09-29)
- Landing resets the chain: `ProcessVerticalMove` zeroes `[char+0x330]`/`[+0x338]`
  when within 64 u of the cell top (`0x14031A25E`). (HIGH)

## 2. Press model (client-side prediction)

`KCharacter::Jump` (`JX3ClientX64.exe` `0x140313680`, disasm
`proof/gravity/disasm/kcharacter_jump.txt`):

- count lives in `[char+0x330]`; allowed move states are 1–7 and `0x1C`
  (state 4 = `cmsOnJump`), with extra checks for fly states `0x1A`/`0x1B`
  (`0x14031370D`–`0x140313728`). (HIGH)
- **chain gate:** `if (jumpCount >= MaxJumpCount[school]) reject` —
  `0x140313B19` reading `word[KGJumpList + 0x25C10 + 2*school]`. (HIGH)
- **row select:** takeoff triple at `KGJumpList + 0x23810 + 6*(24*school + jumpCount)`
  → `JumpSpeedXY`, `VelocityZ` (`0x140313B20`–`0x140313B41`); the row index is the
  *current* count, so the second press reads `J1`. (HIGH)
- count is stored as `jumpCount+1` (`0x140313BC1`), capped at `0x10` in the normal
  path (`0x140313C3D`); move state is set to 4 (`0x140313C16`). (HIGH)
- velocities stored as `+0x268 = XY<<4`, `+0x2F8 = XY`, `+0x270 = Z`, gravity
  `+0x320` clamped `[0, 0x1F]` (`0x140313BD5`/`0x140313BEA`). (HIGH)
- Horse jumps are limited to `jumpCount < 1`, wall jumps to `< 4`
  (`0x140313A48`/`0x140313B7D`) — the normal chain is the `MaxJumpCount` one. (HIGH)

**Phase model (verified, trigger open):** at segment end the *current* row's
`…End` triple replaces the flight (`ModifySprintEndSpeed` `0x1403140A0`, guard
`move_state ∈ {4, 0x1A} && [+0x1F8] == 0 && jumpCount ≥ 1`) and `jumpCount := 1`.
The frame-level condition that reaches the two call sites
(`0x140182874`, `0x14036317A`) is still not decoded — **the client applies the
takeoff triples only** and does not fake a segment length. (HIGH model / OPEN trigger)

## 3. The data — `proof/gravity/JumpParam.tab`

Per school: `MaxJumpCount` + per-jump takeoff (`JumpSpeedXY, VelocityZ, Gravity`)
and `…End` triples. `J0` is almost invariant; `J1` (二段跳) is the school signature.
Full chain: `python tools/gravity/parse_jump_tables.py --chain`.

| School | Max | J0 (xy,vz,g) | J1 二段跳 (xy,vz,g) | J2 | J3 |
|---|---|---|---|---|---|
| 0 default | 4 | 40,90,11 | **30,300,20** | 50,400,20 | 100,−250,8 |
| 1 | 5 | 40,90,11 | 40,180,12 | 65,15,3 | 100,780,30 |
| 2 | 5 | 40,90,11 | 60,450,30 | 80,600,24 | 80,600,24 |
| 3 | 5 | 40,90,11 | 50,400,25 | 70,300,13 | 100,700,36 |
| 4 | 5 | 40,90,11 | 50,160,8 | 70,240,7 | 100,700,36 |
| 5 | 5 | 40,90,11 | 40,160,6 | 50,250,12 | 80,600,20 |
| 6/7 | 5 | 40,90,11 | 65,36,4 | 60,150,7 | 100,800,36 |
| 8 | 5 | 40,90,11 | 40,400,10 | 160,400,25 | 60,600,12 |
| 9 | 6 | 40,**120**,11 | 25,**1000**,25 | 0,0,0 | 75,600,16 |
| 10/11 (万花/…) | 4 | 40,90,11 | 40,80,**80** | 40,80,60 | 120,−200,20 |
| 12–16 | 8–11 | 40,90,11 | 50,400,25 | 70,300,13 | 100,700,36 |
| 17–21 | 13–15 | 40,90,11 | 50,400,25 | 70,300,13 | 100,700,36 |
| 22 | 6 | 40,90,11 | 50,400,25 | 70,300,13 | 100,700,36 |

- `…End` triples: schools 0 → `60,90,11`; schools 1–22 → `125,−140,12`
  (a fast forward dive) — these are the post-segment flight, not reproduced. (HIGH)
- Convert with the canonical unit research: **1 u = 1 cm**
  (`UNIT_SCALE_AND_CHARACTER_SIZE.md`; `JX3_COLLISION_SYSTEM.md` G-0) — the older
  `1 m = 192 u` label in the gravity docs is a metric artifact and does not change
  the integer physics. Raw `J0` apex = 368 u (3.68 m); the spec's in-game jump is
  192 u (1.92 m) / 1.09 s air (`REBORN_JUMP_FALL_SPEC.md`; MapSpike `703 u/s`,
  `1289 u/s²`), realised by the client's `RC_JUMP_SCALE=0.52` (below). (HIGH)
- Clamps apply: gravity `[0,31]` (matters: school 10/11 `g=80` → 31), `|vz| ≤ 2047`,
  `xy ≤ 127`. (HIGH)
- Schools 10/11 additionally ship authored `JumpFrameParam.tab` curves (81 frames)
  that override velocity per air frame — not reproduced. (HIGH data / OPEN trigger)

Worked example, school 0 chain-mode 二段跳 (`J1 = 30,300,20`):
raw apex `v²/2g = 300²/40 = 2250 u` (22.5 m) — with the client's 0.52 calibration
11.7 m, air time 2.0 s. **Do not ship this as the plain 二段跳** — it is the 轻功
chain row applied ballistically, i.e. the takeoff burst stretched over the whole
arc (user feedback 2026-09-29: “way too high”; the game caps it via the segment
phase, whose trigger is still open).

## 4. Reborn client reproduction

`client/JumpTable.cs` (generated: `python tools/gravity/parse_jump_tables.py
--csharp-out client/JumpTable.cs`) + the jump block in `client/RebornClient.cs`.

Default `RC_DJUMP=flip` — the plain 二段跳:
- press 1 from ground → `J0` triple (raw `vy = 1350 u/s`, `g = 2475 u/s²`, scaled
  by `RC_JUMP_SCALE`);
- `RC_JUMP_SCALE=0.52` (default, 100/192): scales **takeoff and gravity together**
  so `J0` realises the spec's 1.92 m apex / 1.09 s air (raw triple = 3.68 m, ~2×
  the game; user feedback). `1.0` = raw table;
- air press 2 → the **J0 takeoff triple again** (one extra normal-strength jump,
  max 2), and the clip switches to the authored `f1b02yd二段跳a.ani` flip;
- third press → rejected (logged); landing → `jumpCount = 0`;
- **provisional** (root AGENTS §6): the exact game arc is `J1 takeoff burst +
  End-triple flight`; the burst/phase trigger is undecoded (§5.1), so re-using
  the J0 triple is the closest data-anchored, non-ballistic behavior. Re-open
  when the trigger is decoded.

`RC_DJUMP=chain` keeps the literal table chain for research: air press n reads
`J1..` (`vy := vz×15`, `g := clamp(g,0,31)×225`) up to `MaxJumpCount` — the
11.7 m / 104 m launches are expected here, that is the 轻功 flight entry.
`RC_DJUMP=0` disables the mid-air jump. `RC_JUMP_SCHOOL` selects the row
(default 0). `RC_CLIP_DJUMP` overrides the flip clip (default the real
`f1b02yd二段跳a.ani`; `0` reuses `RC_CLIP_JUMP`). `RC_DJUMP_LOG=1` emits
`djb press/land/reject` lines.

Verified in-engine (2026-09-29, curated in
`proof/gravity/double_jump_reborn_run.txt`):
- flip mode, unit-calibrated (run F, exit 0): `jump: mode=flip school=0 scale=0.520
  (apex 191u)` → `djb press n=1` at y 646 → `djb press n=2` at y 835 (first apex,
  1.91 m) → `clip -> ...f1b02yd二段跳a.ani (0)` → `djb land n=2` at y 646.
  First jump ≈ 1.9 m; double-jump apex ≈ 3.8 m.
- flip mode pre-calibration (run E, exit 0): same structure with the raw 3.7 m
  apex (the "way too high" run).
- chain mode (runs A/C): `triple=30,300,20` ballistic (documented above).
- `.tani` wrapper (runs B/D) reproduces the 0xC0000005 AV; the underlying
  `.ani` plays cleanly (runs E/F) — the flip action is available without the
  composite wrapper.
Isolation verified in every run: `ns=reborn_client_double_jump.memory`.

Not reproduced (documented, not invented): segment-end End triple, `JumpFrameParam`
curves, wall/horse variants, fly/suspend states, horizontal `JumpSpeedXY`
(the demo moves by WASD run/walk speed, not jump XY).

## 5. Open items

1. Trigger frame of `ModifySprintEndSpeed` (the End phase) — needs the call-site
   guard traced through `0x140182874` / `0x14036317A`; blocks replacing the
   provisional `flip` model with the real `J1 burst + End` arc.
2. `JumpFrameParam` curve application start index (`TotalFrame[row-1] − airFrame`)
   phase alignment for schools 10/11.
3. Whether fly states `0x1A/0x1B` re-press (the `[+0x1F8]==0` path at
   `0x140313C22`) is a separate abort/advance, distinct from the normal chain.
4. Wall-jump (`jumpCount < 4`) and horse-jump (`< 1`) triples — data present.
5. `f1b02yd二段跳a.tani` wrapper AV (the underlying `.ani` is fine); re-open when
   the composite tani path is fixed.

## Reproduce

```powershell
.venv\Scripts\python.exe tools\gravity\parse_jump_tables.py --chain
.venv\Scripts\python.exe tools\gravity\parse_jump_tables.py --csharp-out client\JumpTable.cs
.venv\Scripts\python.exe tools\gravity\verify_model.py
# feature client (isolated name/namespace):
set RC_CLIENT_EXE=reborn_client_double_jump.exe
client\build_client.cmd
# automated fingerprint run (engine, ~1 min; cwd must be MovieEditor):
$env:RC_DEMO="1"; $env:RC_DJUMP_LOG="1"; $env:RC_AUTORUN="25000"
$p = Start-Process "C:\SeasunGame\MovieEditor\bin64\reborn_client_double_jump.exe" -WorkingDirectory "C:\SeasunGame\MovieEditor" -PassThru
$p.WaitForExit(); $p.ExitCode
# expect exit 0 and: jump: mode=flip school=0 scale=0.520 (apex 191u) ->
# djb press n=1 -> djb press n=2 at ~191u above n=1 + clip -> ...二段跳a.ani
# -> djb land n=2 -> DONE; ns=reborn_client_double_jump.memory in the init line
# research chain mode: add $env:RC_DJUMP="chain"
```
