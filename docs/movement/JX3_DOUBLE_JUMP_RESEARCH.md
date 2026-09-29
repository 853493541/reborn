# JX3 二段跳 (double jump / jump chain) — research notes

**Status:** press semantics + per-school data verified; reproduced in the Reborn client
(`client/JumpTable.cs`, `client/RebornClient.cs`), feature build
`reborn_client_double_jump.exe`. The segment-end phase (End triples) and the
per-frame curves stay **open** (trigger frame still undecoded); the reproduced
model is the ballistics of the verified takeoff triples.
**Sources:** `JX3_GRAVITY_RESEARCH.md`, `REBORN_JUMP_FALL_SPEC.md`,
`proof/gravity/JumpParam.tab`, `proof/gravity/disasm/kcharacter_jump.txt`,
`proof/gravity/JX3RepresentX64_strings.txt`.
**Last verified:** 2026-09-29 (table extraction + disasm re-read).

## 1. What the game does

JX3 has a real mid-air jump: pressing jump again while airborne does **not** get
ignored (unlike most MMOs of its era) — it advances a per-character **jump chain**:

| Press | Row | Meaning | Conf |
|---|---|---|---|
| 1 (grounded) | `J0` | 跳跃 | HIGH |
| 2 (airborne) | `J1` | **二段跳** — the big launch, per 门派 | HIGH |
| 3+ (airborne) | `J2`, `J3`, … | further 轻功 chain presses up to `MaxJumpCount[school]` | HIGH |
| beyond `MaxJumpCount` | — | rejected (no jump) | HIGH |

- Represent has a dedicated `DOUBLE_JUMP` state (`JX3RepresentX64.dll` string
  `0x00CBD028`, next to `JUMP` `0x00CBD020`) and a dedicated
  `KRLCharacterFrameData::GetDoubleJumpEndOffset` (`0x00CCC928`) — the animation
  side treats 二段跳 as its own state, not as 跳跃 again. (HIGH)
- Shipped animation `f1b02yd二段跳a.tani` (extracted; see
  `REBORN_JUMP_FALL_SPEC.md` §6). It resolves through the VFS and loads with
  `PlayAnimation` rc=0, but the host AVs (0xC0000005) within ~1 s of playing it
  in both the default and opt-in runs — so the client's default reuses the jump
  clip and the tani stays opt-in (`RC_CLIP_DJUMP=<path>`). (HIGH, evidence
  `proof/gravity/double_jump_reborn_run.txt` runs B/D; re-open when the anim
  path is fixed)
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
- Convert with the verified units: `1 m = 192 u`, tick = 1/15 s →
  `v[m/s] = vz × 15/192`, `g[m/s²] = g × 225/192`. (HIGH, `REBORN_JUMP_FALL_SPEC.md` §1)
- Clamps apply: gravity `[0,31]` (matters: school 10/11 `g=80` → 31), `|vz| ≤ 2047`,
  `xy ≤ 127`. (HIGH)
- Schools 10/11 additionally ship authored `JumpFrameParam.tab` curves (81 frames)
  that override velocity per air frame — not reproduced. (HIGH data / OPEN trigger)

Worked example, school 0 二段跳 (`J1 = 30,300,20`):
`v0 = 300×15/192 = 23.44 m/s`, `g = 20×225/192 = 23.44 m/s²`, ballistic apex
`v0²/2g = 11.7 m`, air time `2·v0/g = 2.0 s` (continuous; the exact per-frame
integer integration is in `verification.txt` §6). Compare first jump: `v0 = 7.03 m/s`,
apex 1.9–2.2 m. The 二段跳 is the “real jump” of the game — exactly why it exists.

## 4. Reborn client reproduction

`client/JumpTable.cs` (generated: `python tools/gravity/parse_jump_tables.py
--csharp-out client/JumpTable.cs`) + the jump block in `client/RebornClient.cs`:

- first press from ground → `J0` triple (identical to the old constants
  `vy = 1350 u/s`, `g = 2475 u/s²`; single-jump behavior unchanged);
- air press while `jumpCount < MaxJumpCount` → next triple, `vy := vz×15`,
  `g := clamp(g,0,31)×225`, `grounded = false`;
- landing → `jumpCount = 0`; count-limit press → rejected (logged);
- `RC_JUMP_SCHOOL` selects the row (default 0 = weaponless/default character);
  `RC_DJUMP=0` disables the mid-air chain for A/B; `RC_CLIP_DJUMP` plays the real
  `f1b02yd二段跳a.tani` when set (default: reuse `RC_CLIP_JUMP`; see §1);
- `RC_DJUMP_LOG=1` emits `djb press/land/reject` lines for a numeric fingerprint.

Verified in-engine (2026-09-29, logs curated in
`proof/gravity/double_jump_reborn_run.txt`): run A/C (clean, exit 0)
`djb press n=1 triple=40,90,11 vy=1350 g=2475` → `djb press n=2
triple=30,300,20 vy=4500 g=4500` (mid-air, y 1009 ← 646) → `djb land n=2
vy=-4833` → `DONE`; run B/D (tani opt-in) reproduce the 0xC0000005 AV right
after `clip -> ...f1b02yd二段跳a.tani (0)`. Isolation verified in every run:
`ns=reborn_client_double_jump.memory`.

Not reproduced (documented, not invented): segment-end End triple, `JumpFrameParam`
curves, wall/horse variants, fly/suspend states, horizontal `JumpSpeedXY`
(the demo moves by WASD run/walk speed, not jump XY).

## 5. Open items

1. Trigger frame of `ModifySprintEndSpeed` (the End phase) — needs the call-site
   guard traced through `0x140182874` / `0x14036317A`.
2. `JumpFrameParam` curve application start index (`TotalFrame[row-1] − airFrame`)
   phase alignment for schools 10/11.
3. Whether fly states `0x1A/0x1B` re-press (the `[+0x1F8]==0` path at
   `0x140313C22`) is a separate abort/advance, distinct from the normal chain.
4. Wall-jump (`jumpCount < 4`) and horse-jump (`< 1`) triples — data present.
5. `f1b02yd二段跳a.tani` engine AV (crash before the clip's first update);
   re-open when the animation asset pipeline is fixed — the chain itself is
   clip-independent (runs A/C pass with the jump clip).

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
# expect exit 0 and: djb press n=1 (40,90,11) -> djb press n=2 (30,300,20)
# -> djb land n=2 -> DONE; ns=reborn_client_double_jump.memory in the init line
```
