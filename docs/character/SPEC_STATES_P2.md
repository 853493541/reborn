# E2 — WATER ENTRY: client semantics, host diagnosis, surface data, corrections

**Area:** character · **Branch:** agent/3x-integration · **Index:** `docs/character/README.md` · **Status:** client-truth addendum 2026-10-07 - corrects SPEC_MOTION.md / SPEC_STATES.md where marked; supersedes any host assumption it contradicts.

**Area:** movement/character · **Task:** why the player cannot enter water in the Reborn host, and
what the client's own control semantics are. **Date:** 2026-10-07.
**Scope:** static decode only (no engine runs). **Installs read-only.** Scratch:
`C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x\`.

**Truth sources**

| Source | Build | Use |
|---|---|---|
| `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe` | 1-5-0 (release) | all RVAs below unless marked HIP/EXP |
| `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3D_LoaderNoRenderX64.dll` | 1-5-0 | `watersurfacelist.json` loader |
| `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineDX11EX64.dll` | 1-5-0 | water JSON field-name table |
| `C:\SeasunGame\Game\JX3_EXP\bin\zhcn_exp\bin64\KG3D_LoaderNoRenderX64.dll` | 1-6-0 | delta: same loader family (`RangBox`, `watersurfacelist`) |
| Host worktree `C:\Users\Zhibin Ren\Desktop\reborn-iso-3x-integration\client` | branch agent/3x-integration | diagnosis only |
| Reused specs | — | `docs/character/SPEC_STATES.md`, `docs/movement/JX3_GRAVITY_RESEARCH.md`, `docs/movement/REBORN_JUMP_FALL_SPEC.md`, `proof/gravity/disasm/*` |

Confidence: **HIGH** = instruction/table read directly + consumer; **MED** = one inferred link;
**LOW** = unprobed. Every claim below carries its evidence address.

---

## Client entry semantics (inputs/states/conditions)

### 1. The water model is the terrain logic cell (not the render surface)

`KCharacter` holds `m_pCell = [char+0x50]`. Submersion helper `KCharacter::GetWaterline`
`0x140312440` (release; EXP `0x14033d4a0`) — HIGH (`proof/gravity/disasm/get_waterline.txt:26-55`):

```
cell = [char+0x50]                     ; assert on null
if (!(byte[cell] & 1)) return 0        ; cell flag bit0 = water
cellTop = word[cell+6] << 6            ; water surface (cell values in 尺, ×64)
ground  = word[cell+4] << 6            ; cell floor
if (y > cellTop) return 0              ; above the surface -> not submerged
return cellTop - max(ground, y)        ; submersion depth
```

- Walking on the bottom (`y == ground`): depth = the local water depth.
- Falling/rising (`y > ground`): depth = how far the root is *below* the surface.
- **Waterline = `word[cell+6]<<6`, cell floor = `word[cell+4]<<6`, gate = cell byte bit0.**
  There is no height-derived waterline (`0x140312400` is a run-speed factor, already corrected in
  SPEC_STATES §5 #1).

### 2. The depth threshold that decides "wade vs swim" — HIGH

Both `ProcessVerticalMove` (`0x140318C50`) and `ProcessAcceleration` (`0x1403165D0`) and
`KCharacter::Jump` (`0x140313680`) compute the same threshold at entry:

```
T = max(0, [char+0x16C] * [char+0x40] / 100)      ; magic /100 (0x51EB851F, sar 5)
```

- PVM: `0x140318C66-0x140318C8B` (stored `[rsp+0xd8]`).
- Jump: `0x140313691-0x1403136C5` (stored `r13d`).
- Accel: `0x140316FFE-0x140317023`.
- `[+0x16C]` = jump-speed modifier (Init default `0x46` = 70; overwritten from server character
  block byte `[rdi+0x41]`, `0x14015A553`). `[+0x40]` = scale, `KCharacter::Init` writes `0x380`
  (896) at `0x1403126CA`. Defaults ⇒ **T ≈ 627 u** (MED on the live values: the server byte
  overwrites the Init default; formula is HIGH).
- **Shallow water is wadeable**: below T no swim state is entered at all — the player keeps
  walking on the cell floor. This is the client's "depth that blocks entry".

### 3. The per-frame state machine that performs the transition — HIGH

`KCharacter::ProcessVerticalMove 0x140318C50` (single call site in the master update) is the only
water/vertical integrator. It dispatches on `m_eMoveState = [char+0x1F4]` via a byte+dword jump
table at RVA `0x31A350`/`0x31A364` (dumped for this task — `pvm_dispatch.py` output):

| Move state | Case target | Meaning |
|---|---|---|
| 1,2,3,8,0x1F,0x20,0x21,0x23..0x25 | `0x31995E` | land stand/walk/run, swim-jump(script), fly/bird states |
| 4 | `0x319C15` | airborne (jump/fall) |
| 5 | `0x319D18` | in-water jump (takeoff) |
| 6,7 | `0x319EB0` | swimming |
| other | `0x319F24` | no water logic |

**Case 0 — `0x31995E` (enter water from land / fly / idle).** Requires `depth >= T`
(`cmp esi,[rsp+0xd8]; jl exit` at `0x31995E`). Then (`pvm_full.txt:868-1036`):

```
if (!player) -> state 7                         ; 0x31996B test r13 (player bit)
if (state in {2,3} or {0x1F,0x20}) && [char+0x268]>0:
    speed   = [char+0x268] >> 4                 ; current XY per-frame speed
    dist    = |char.xz - destination[+0x2A0/+0x2A4]|
    frames  = ceil(dist / speed)
    Vz      = old_Vz * old_frames / frames      ; keep the vertical rate
    [+0xC08] = frames ; [+0x270] = clamp(Vz)    ; 0x319988-0x319A2C
    [+0x174]=[+0x210]=[+0xC04]=0 ; [+0x204]=1
    state = 6                                   ; SWIM moving       (0x319A71)
else
    clear 0x2F8/0x260/0x268/0x270/0x174
    state = 7                                   ; SWIM in place     (0x319B60)
```

`[+0x2A0/+0x2A4]` = the **active RunTo/WalkTo/SwimTo destination** (RunTo writes them:
`new_runto2.txt 0x14034AD09/AD13/AD19`; SwimTo `swim_to.txt 0x14031DB94/DBAA`). So the state-6
transition is the *destination move* entering water: the remaining ground move is converted into a
swim move with the same destination and a recomputed frame count. **The player does not press a
swim key; the movement input (WASD / click-to-move) that already holds a destination is what
carries them in.**

**Case 1 — `0x319C15` (fall in).** `state==4 && Vz<0 && depth >= T` → clear the velocities and
`state = 7` (`0x319C7B`). Falling into water therefore stops you in place at the entry depth (the
surface float is handled by the generic snap, §4).

**Case 3 — `0x319EB0` (stay/leave water).** `depth >= T` → keep 6/7. When the shallow edge is
reached (`depth < T`): player + `[+0x270]<=0` or no move (`[+0xC08]==0`) → `state = 1` (stand);
rising with fly-mode bit (`[+0xee0]&4`) → `state = 0x1F`; otherwise `state = 3` (run) with
`[+0x268] = [+0x2FC]<<4`. **Walking out of water is automatic at the shallow edge.**

**Input jump from water — state 5.** `KCharacter::Jump 0x140313680` computes `depth` at
`0x1403136C9` and `T` at entry; its state gate (`0x14031370D-0x140313728`) accepts move states
**1..7** (plus 0x1A/0x1B). With `depth >= T` it branches at `0x313C2D` to `0x313C50`:
`state = 5` (`0x313C7B`) and falls through the **common velocity store** (plain jump profile:
`[+0x268]/[+0x2F8]/[+0x270]` from the character's run/jump attributes, `0x313C85-0x313D58`).
PVM case 2 (`0x319D18`): shallow (`T > depth`) → `state 4` + `[+0x330]++` (a normal jump out);
deep + descending → `state 7` (`0x319E13`). **So: in water, Space = splash/jump; deep water pulls
you back to float when you come down.** (State 8 `SWIM_JUMP`, setter `0x14031C400`, is the
script/Lua path only — not raw input.)

### 4. Float at the surface — HIGH (instruction), MED (mode byte)

At the end of PVM (`0x31A0C2-0x31A10A`):

```
if (player && state in {1,2,3,6} && [+0x20C]==0 && [+0x210]==0) {
    esi = max(floor, cellTop)
    if (!(cell flags & 1))            y = esi   ; normal ground snap
    else if ([+0xee0] == 1)           y = esi   ; water cell, normal mode -> y = cellTop (surface)
    else                              /* skip */ ; a water-mode move controls y
}
```

plus the states 6/7 clamp `if ([+0x170] > 0) y = max(y, cellTop - scaled[+0x170])`
(`0x31A285-0x31A2AA`). `[+0xee0]` is a synced per-character movement-mode byte: writer
`0x14015C589` (`[+0xee0] = byte[character_data+0xD0]` from the same server block that writes
`[+0x16C]/[+0x170]` at `0x14015A553/55D`); default `1` written by the character init at
`0x140373D10`; PVM consumers at `0x318D05` (==8), `0x319EBE` (==2), `0x319F68` (&4),
`0x31A100` (==1). Reading (MED): `1` = normal mode (surface snap), `2/4/8` = special water move
modes (in-place, fly-recovery, destination water move).

**Net client behavior:** in a water-flagged cell with the default mode, the player's root is
placed at `cellTop` — the character floats on the surface and can swim; the swim state (6/7) adds
the movement/exit gates above. There is no "sink to the bottom" for a normal player.

### 5. `SwimTo` — the script/server destination move (not entry)

`KCharacter::SwimTo 0x14031D770` (HIGH, `proof/gravity/disasm/swim_to.txt`): valid **only from
states 6/7** (`state-6 <= 1`, `0x14031D78E`); writes destination `[+0x2A0/+0x2A4]`, XY velocity
`[+0x2F8]/[+0x268]`, heading `[+0x26C]`, Vz `[+0x270]`; sets **7** (same position, `0x14031D895`)
or **6** (moving, `0x14031DA81`). Callers are the KPlayer command surface / Lua cluster
(`0x14036B23D`, `0x14036B41B`, `0x1403740E8`, `0x1403E0F3E`; `ON_SWIM` event) — i.e. the server's
move order follows the same water states but is not how a player *enters*.

### 6. What the player actually does (answer to the report)

1. **Walk/run toward the water** (hold the movement key / click-move). Shallow water (depth < T)
   is waded on the bottom. Once the water is deep enough at the feet (`depth >= T`), the client
   converts the active move into state 6 and the character floats/swims at the surface.
2. **Jump into water** from the bank: airborne descent (state 4, Vz<0) with submersion >= T →
   state 7, floats.
3. **Space while swimming**: deep → state 5 rise (plain jump profile) then back to float; shallow
   → normal jump (state 4) out of the water.
4. **Walk out**: at the shallow edge (depth < T) the state returns to run/stand automatically.
5. No dedicated swim key exists anywhere in the client (`ON_SWIM` / `SwimTo` are script/server).

---

## Why the host blocks entry (diagnosis)

Files: worktree `C:\Users\Zhibin Ren\Desktop\reborn-iso-3x-integration\client`
(`RebornClient.cs`, `WaterField.cs`, `WaterSurfaces.cs`). **Diagnosis only — no patch here.**

| # | Host code | What it does | Client rule it violates |
|---|---|---|---|
| 1 | `RebornClient.cs:4041-4055` ground snap | while `grounded`, `py = ground` — walking down a shore follows the **terrain floor under the water** | client raises the player to `cellTop` in a water cell (PVM `0x31A0C2-0x31A10A`); the water state never sinks the player to the bottom |
| 2 | `RebornClient.cs:4257` `if (!grounded && inWater && vy <= 0f)` | swim state 6/7 + float only when **airborne** | client enters state 6 directly from states 2/3 (RunTo/WalkTo) while still on the ground (PVM case 0 `0x31995E→0x319A71`) |
| 3 | `RebornClient.cs:3754` `inWater = py <= wSurf && vy <= 0 && wFloat > ground + 1` | any submersion counts; `vy<=0` excludes nothing on the ground, but there is **no depth threshold and no swim state while grounded** | client requires `submersion >= T` (`0x16C·0x40/100`) and then **always** sets 6/7 regardless of ground contact |
| 4 | `RebornClient.cs:4073-4096` jump in water | only state 8, only when **not moving**, wrong state (script path) | client's input jump is **state 5** (plain profile), accepted from states 1..7 while deep; state 8 is script only |
| 5 | `WaterField.cs:76-107` (`:35` radius) | region = circle `radius = 4096 * max(ScaleX,ScaleZ)` around the body center | the shipped data has no radius; the render entry rectangle is `BaseWidth×BaseLenght × Scale`, RotY-rotated (`RangBox` in newer data); the **gameplay** region is the cell water flag |
| 6 | `RebornClient.cs:2588-2590`, `:4413-4421` | `swimState` is only ever assigned, **never reset**; clips gated on `swimState != 0 && swimmingLast`; `swimmingLast` sampled pre-move | leaving water must clear the swim mode; the client's exit path returns to state 1/3 and the normal animation set |
| 7 | `RebornClient.cs:3751-3755` samples water once, before the move substeps | after moving, `inWater`/`depth` are stale for the current position | the client evaluates the water branch every frame at the post-move position |

**Root cause (one sentence):** the host's water entry is modeled as an *airborne event*
(`!grounded` gate + ground snap), so a player who walks into a lake is never released from the
terrain-floor snap and never transitions to state 6/7; the client transitions from *grounded
movement* (states 2/3) at a depth threshold and floats the player at the surface.

Note: the swim **animation** itself works because `swimState` gets set when the player is
airborne over water (jump/fall) and then sticks (bug #6), which is exactly why only the entry
feels broken.

---

## Water surface data semantics

### File and loader — HIGH

- `data\source\maps\<map>\water\surface\watersurfacelist.json` (UTF-8 JSON), plus
  `water\waterinfo.json` (= `{ "Version": 6 }` only) and `water\surface\*.jsonins` materials.
- Loader: **`KG3D_LoaderNoRenderX64.dll` fn `0x180023220`** (release), which builds
  `%s\surface\watersurfacelist.json` (format string at `0x18006A630`), reads `WaterCount`,
  `MaxWaterID` (errors `WaterMaxID Error->%d`), then loops `water%d` objects into an array of
  **0xB0-byte** entries (`0x180023436` `imul r8, rbx, 0xB0`).
- The renderer also parses the fields itself: `KG3D_WaterFileOperate::LoadWaterConfigFromJSON`
  in `KG3DEngineDX11EX64.dll` — the field-name table sits at file offset `0x02184FF8`
  (`WaterID, WaterType, RotY, Postion, IsLocal, BaseLenght, WaterQuality, UwUnique,
  WaterRenderVersion, PostMaterial, BaseWidth, Guid, ...`).
- EXP 1-6 `KG3D_LoaderNoRenderX64.dll` contains the same loader strings plus `RangBox`
  (file offsets `RangBox=432896`, `watersurfacelist=432667`) — the 1-6 data schema adds the
  range box.

### Field map (loader stores; key resolver output in `water_loader_keys.py`) — HIGH

| JSON key | Struct off | Semantics |
|---|---|---|
| `WaterID` | +0x00 | body id (index; `MaxWaterID` must be ≥ 0) |
| `WaterType` | +0x04 | 0 = ocean/global plane, 1 = bwater body |
| `WaterRenderVersion` | +0x08 | schema/render version (shipped maps = 0) |
| `water_%u` (built from ID) | +0x10 | hashed engine water-surface registry key (`KG3D_ConvertToStandardHashString`) |
| `WaterMaterial` | +0x18 | hashed `%mappath%\water\surface\*.jsonins` (over-water material) |
| `UWaterMaterial` | +0x20 | hashed under-water material |
| `Postion` [x,y,z] | x→+0x28, **y→+0x5C**, z→+0x30 | plane position; `y` = the water plane height |
| `Scale` [sx,sz] | +0x34, +0x38 | mesh scale multipliers |
| `RotY` | +0x3C | yaw of the body |
| `Depth` | +0x40 | volume depth; bottom = `Postion.y - Depth` stored at +0x50 (1000000 = unbounded) |
| `BaseWidth` / `BaseLenght` | +0x44 / +0x48 | base rectangle extents; the engine DX11 field table names them `BaseWidth`/`BaseLenght` (shipped files), while the LoaderNoRender code reads `RangBox: [minX,maxX,minZ,maxZ]` and optional `Width`/`Lenght` overrides with defaults `v1-v0` / `v3-v2` (1-6 schema) |
| `RangBox` [4] | +0x4C..+0x60 | authored X/Z extents of the base mesh |
| `ClipType` | +0x64 | water clipping mode |
| Fog/Caustic/FFT/Reflect fields | +0x6C.. | render parameters (not gameplay) |
| `UseLogic` (shipped = 1) | engine-side | **the surface shape follows the logic (terrain-cell) water** — key read by `KG3DEngineDX11EX64.dll` (string at file off `0x02185778`, beside `FFTDensity/UseFFT`) |

The gamified `water_surfaces.txt` summary from the earlier pass printed only a subset; the real
files carry the whole set (e.g. 龙门 `water1` has `Scale=[2,2]`, `BaseWidth=40`, `BaseLenght=40`,
`Depth=1e6`, `UseLogic=1`, `WaterMaterial=…lxxxx_ow_bwater.jsonins`).

### How the client turns it into a region — HIGH (structure), MED (exact render clipping)

- These entries are the **render/wave placement and material parameters**; they are *not* the
  collision/entry region. With `UseLogic=1` the rendered surface is shaped by the logic-cell water.
- Gameplay (`m_pCell`, flag bit0, `word+4/+6`) is the region used by every entry/exit test in
  §"Client entry semantics". `SPEC_STATES` P1 called the cell-stream writer untraced; this pass
  checked the only loose candidate: `data\UGC\**\*.Map.Logical` (e.g. the 683 KB 龙门 file) is an
  **INI-style entity/nav logic scene** (`[MAIN] NumNPC=1365…NumLogicalPoly=4`, NPC/doodad/traffic
  waypoints) — **it does not contain the terrain water cells**. So P1 stays open; the writer is
  still to be traced.
- Consequence for the host: `radius = 4096 * Scale` (`WaterField.cs:35`) is **not in the data and
  not the client rule**. Until the cell stream is traced, the honest replacement is the authored
  rectangle from the entry itself (center `Postion`, half-extents `0.5*BaseWidth*Scale.x` ×
  `0.5*BaseLenght*Scale.z`, yaw `RotY`; type 0 = global plane), still gated on
  `ground < Postion.y` — plus the depth threshold T from the client. This must stay labeled a
  provisional mapping (P1) because the true region is the cell layer.

---

## Corrections to SPEC_STATES.md

1. **§1.3 entry (rows state→6 / state→7)** — add the two missing gates: (a) `submersion >= T`,
   `T = [+0x16C]*[+0x40]/100` (defaults 70·896/100 ≈ 627 u); (b) state 6 needs the player bit
   (`[+char+8]&0x40000000`) **and** an active destination move (`state ∈ {2,3,0x1F,0x20}`,
   `[+0x268]>0`); everything else (state 1, 8, 0x21, 0x23–0x25, NPCs) → 7. Full jump-table mapping
   (states → `0x31995E / 0x319C15 / 0x319D18 / 0x319EB0`) is in §"Client entry semantics" §3.
2. **§1.3 state 6 semantics** — the transition recomputes `Vz`/`[+0xC08]` from the *remaining
   distance to `[+0x2A0/+0x2A4]`* (the RunTo/WalkTo/SwimTo destination) and the current XY speed;
   it is a conversion of the active ground move into a swim move. "states 2/3 descending into
   water" understates the destination condition.
3. **§1.3/§1.5 float clamp** — the surface float is primarily the **generic player snap**
   `y = max(cell ground, cellTop)` for `state ∈ {1,2,3,6}` when the cell is water and the synced
   mode byte `[+0xee0] == 1` (`0x31A0C2-0x31A10A`); the `cellTop - scaled[+0x170]` clamp
   (`0x31A285`) only applies when `[+0x170] > 0`. The spec presents the latter as *the* float law.
4. **New field to register: `[+0xee0]`** — synced per-character water/movement-mode byte
   (server block byte +0xD0 → `[+0xee0]`, `0x14015C589`; default 1 at `0x140373D10`; PVM consumers
   at `0x318D05`, `0x319EBE`, `0x319F68`, `0x31A100`). Without it the state-6/7 and snap
   conditions cannot be read correctly.
5. **§1.3/§1.5 "Swim jump"** — split the two paths: **input** Space in water = state **5**
   (plain jump velocity, accepted from states 1..7, `Jump 0x140313680` → `0x313C50`), shallow →
   state 4; **script** `SwimTo`-era `SWIM_JUMP` = state **8** (`0x14031C400`, no velocity). The
   host §8 note "state 8 starts from standing in water" is only the script path and must not be
   used for the key.
6. **§1.5 exit** — add that exit is threshold-driven: `state 6/7 && depth < T` → state 3 (run, if
   a move is active), state 1 (stand), or 0x1F (fly-mode recovery); i.e. shallow edges
   automatically switch back to land movement (PVM case 3 `0x319EB0`).
7. **§4 acceptance criterion 2** ("Standing below the surface → state 7") — restate as: entering
   deep water sets 6 (moving) / 7 (in place), and a normal-mode player is snapped to the surface;
   the sustained state may be 1/7 depending on the move flags (MED), but **y = cellTop** is the
   testable invariant.
8. **§6 P1 water source** — new evidence: `watersurfacelist.json` is the render/wave list
   (loader `KG3D_LoaderNoRenderX64.dll 0x180023220`, field map above; `UseLogic=1`); the
   `data\UGC\*.Map.Logical` files are entity/nav logic, **not** the water cells, so the cell-stream
   writer remains the re-open. The `radius = 4096*Scale` heuristic has no basis in the data and
   should be replaced by the authored rectangle (+`ground<surface`) as the interim mapping.
9. **REBORN_JUMP_FALL_SPEC.md rows 11/196** — "per-frame swim step 0x140327A80 open" and
   "GetWaterline 0x140312400 (0.6h/6h/7)" are stale (already corrected elsewhere): the integrator
   is PVM `0x140318C50`, and `0x140312440` is the submersion helper; the entry gate is `T` above.

---

## Host acceptance criteria

For the implementation agent (worktree `reborn-iso-3x-integration`, no spec writes here):

1. **Region**: replace the `WaterField.cs:35` radius rule with the authored rectangle from
   `WaterSurfaces`/the JSON: center `Postion`, half-extents `0.5*BaseWidth*ScaleX` and
   `0.5*BaseLenght*ScaleZ`, yaw `RotY`; type 0 = global plane; keep `ground < surface`; label the
   mapping P1-provisional (the real gameplay region is the cell layer).
2. **Depth + threshold**: per tick (after the move substeps, `RebornClient.cs:3833+`), compute
   `depth = surface - max(ground, py)` when inside a region and `py <= surface`;
   `T = RC_SWIM_DEPTH` default **627 u** (= client default `70*896/100`; formula
   `[+0x16C]*[+0x40]/100`), documented as client-derived, live value server-supplied.
3. **Entry from ground movement (the fix)**: if `depth >= T` and a move input is active → set
   `swimState = 6`; idle → `swimState = 7`. Do **not** require `!grounded`; while `swimState != 0`
   suspend the 4048-4055 ground snap and apply `py = max(py, surface)` (surface float), with
   horizontal speed `pSwim` (`0.589*nRunSpeed`, unchanged). A walk-in must float the player at the
   surface within the first water tick.
4. **Entry from air**: keep the existing airborne path, but gate it on `depth >= T` too
   (client case 1: state 4, Vz<0, depth >= T → 7/float).
5. **Exit**: when `depth < T` or the region is left → `swimState = 0` (this also fixes the sticky
   clip bug), restore the land ground snap; run/stand selection per input (`swimState==0`,
   `swimmingLast==false`).
6. **Space in water**: replace the state-8-only branch (`4073-4096`): deep → plain jump profile
   (existing calibrated profile; decoded = state 5 + common velocity store), shallow → normal
   jump; accepted while moving; state 8 stays the script move only.
7. **Re-evaluate water after movement**: `inWater`/`surface`/`depth` must be sampled at the
   post-move (x,z) used for collision and the transform.
8. **Verification (for the implementer)**: scripted drive at 龙门 water1 (center ≈ `67040,58549`,
   `Scale=2`) — walk in from the shore: expect log `swim: state=6` with `y → surface`, then idle
   `state=7`; walk out: `state=0` and normal run; jump deep: rising then float, shallow: normal
   jump. Proof: `RC_SWIM_LOG=1` numeric lines (pos/depth/y) before/after + `proof/` capture.

## Open items (registered)

- **P1 cell stream** (blocking exactness): the writer of `m_pCell` water (`flag bit0`,
  `word+4/+6`) is still untraced; `.Map.Logical` checked and ruled out this pass.
- **`[+0xee0]` mode names** (1/2/4/8): consumers decoded, names not; MED.
- **`[+0x170]` float-modifier live value** and the player swim vertical between threshold and
  surface (P3) remain as in SPEC_STATES.

## Reproduce (read-only)

```powershell
$py="C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
$hd="C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"
$S="C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x"
& $py tools\collision\disasm_range.py $hd 0x318C50 0x1700 $S\pvm_full.txt      # PVM water branches
& $py $S\pvm_dispatch.py                                                        # state->case jump table
& $py $S\water_loader_keys.py                                                   # loader field->offset map
& $py tools\netcode\xref_string.py "C:\SeasunGame\...\KG3D_LoaderNoRenderX64.dll" "watersurfacelist" --out $S\water_loader_xref.txt
& $py tools\collision\find_field_refs.py $hd ee0 > $S\field_ee0.txt              # [char+0xee0] consumers/writer
& $py $S\logical_scan.py                                                        # .Map.Logical content check (entity logic, no water)
```

**Evidence files (scratch):** `pvm_full.txt`, `pvm_dispatch.py` output, `water_loader_xref.txt`,
`water_loader_2.txt`, `water_loader_keys.py`, `map_logical_寻宝.bin`+`logical_scan.py`,
`field_ee0.txt`, `charattr_apply.txt`, `ee0_writer1/2.txt`, `terrain_blocks.txt`,
water JSON extraction under `ext\out3\...\water\surface\`; repo:
`proof/gravity/disasm/{get_waterline,kcharacter_jump,swim_to,process_vertical_move,process_acceleration}.txt`.

Last verified: 2026-10-07 (release 1-5-0 binaries static; exp 1-6 loader delta string check only).

---

## Implementation note (2026-10-07, `agent/3x-states`)

Implemented per the acceptance criteria above; proofs in
`proof/character/3x_states/p2_*`.

- Region = authored rectangle (center `Postion`, half-extents
  `0.5*BaseWidth*ScaleX` x `0.5*BaseLenght*ScaleZ`, yaw `RotY`, `ground < surface`;
  type 0 global). P1-provisional, labeled in the startup line.
- Entry from ground movement: post-move evaluation (no `!grounded` gate); state 6
  moving / 7 idle at `depth >= T` (default 627, `RC_SWIM_DEPTH`); the float
  `y = max(ground, surface - RC_FLOATMOD)`; exit at `depth < T` or leaving the
  region, `swimState` reset. Input jump in water = state 5 (plain profile),
  shallow = normal jump; the old key->state-8 path is removed (script only).
- **Correction to criterion 2 (evidence-first):** the gate quantity is the LOCAL
  water depth `surface - ground`, not `surface - max(ground, y)`. Once the player
  floats at the surface the submersion form yields 0, so a sustained 6/7 state
  would exit on the next tick - observed in the first implementation
  (`swim: exit state=6->0 depth=0` immediately after entering). The local-depth
  form matches section 1 ("walking on the bottom: depth = the local water depth")
  and the case-3 sustained keep; entry from air also uses it (state 4 + `Vz<0` +
  local depth >= T).
- Proofs: `p2_walkthrough.txt` (walk in: `enter state=6 surface=150 depth=1002
  y=-852` -> `exit state=6->0 ... y=150` at the far edge), `p2_idle.txt`
  (`enter state=7` -> `y=150`), `p2_shallow_wade.txt` (T=1500: no swim, walks the
  floor), `p2_jump_deep.txt` (`swim: jump state=5` -> re-entry state 7),
  `p2_jump_shallow.txt` (normal jump). Gates: build 0, smoke ALL PASS, collision
  36/36.

## Water visibility - engine lead (2026-10-07)

The user's "walk into water like no water exists" is literal: the host renders no water.
`scene.EnableFluxWaterSimulation(1)` returns E_FAIL (0x80004005) and there is no render
path for the shipped surfaces.

Engine evidence (MovieEditor `KG3DEngineDX11EX64.dll`, image base 0x180000000):
- The flux-water subsystem ships: strings `FluxWater.Enable`, water material
  `data\material\Shader\Mtl_Water\FluxWaterDefault_BWater.JsonIns`, components
  `KG3D_FluxWaterVolumeComponent` / `KG3D_FluxWaterMeshBakeComponent` /
  `KG3D_FluxWaterMaskExtractComponent`, solver `KG3D_FluxWaterDataSolver`.
- Entry points: `KG3D_CreateFluxModel` @0x180c04b50 and `KG3D_FluxDomainModel::Init`
  @0x180c05250 (string xrefs at 0x180c04f37 / 0x180c05431).

Next probe (owner: E/states or camera/engine host): disassemble 0x180c04b50 /
0x180c05250 to find which precondition returns E_FAIL from the CLR
`EnableFluxWaterSimulation` path (likely a missing flux asset/world that the editor
loads with the map), then drive the same init the MovieEditor editor does. Do NOT fake
water with a procedural quad; if the engine path stays unreachable, register it as a
named boundary with the exact failing precondition.
