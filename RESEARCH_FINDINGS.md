# Research Findings — JX3 client reverse-engineering (communication summary)

**Location:** worktree `Desktop\reborn-netcode`, branch `research/jx3-netcode`
**Status:** this record file is committed on the branch. Sibling artifacts
(`tools/netcode/`, `proof/netcode/`, other `docs/netcode/` files) may still be
uncommitted — check `git status` there.
**Purpose of this file:** one place that communicates everything found, with an
honest confidence label on every claim.

Confidence labels:

| Label | Meaning |
|---|---|
| **VERIFIED** | bytes/disassembly/table on disk, cited by file offset or address |
| **INFERRED** | derived from names/structure; behavior not fully decoded |
| **INVENTED** | our own placeholder/design, not from JX3 |
| **UNKNOWN** | not yet obtainable from this install |

---

## 1. Netcode / session model

**VERIFIED**
- Two-stage session: login gateway (`KGatewayClient`: account/role/login-key) →
  game server (`KPlayerClient`, 923 methods; `Do*` = client→server, `On*` = server→client).
- Frame prefix (15 B): `u16 protocol id; u8 flags; u16 send_seq; u16 ack; u32 field7; u32 param`; protocol tail from `+0xF`.
- Reliability layer: 2048-slot unconfirmed ring, cumulative ack (frame `+5`),
  retransmit with `flags |= 3`, ack-control protocol `0x2FE`, "Unconfirm send buffer full!".
- Ping = protocol **6** every **3000 ms**; dead timeout **12000 ms** (4×ping).
- Handshake = protocol **1**: RoleID (`+0xB`), 16-byte session blob (`+0xF`),
  resume serial (`+5`), resume bool (`+0x1F`); response has `bRecover` + `ReconnectTimeout`.
- Routine sync = protocol **0x6E**: `u32 param; u16 size; payload`; max frame `0x8000`.
- Transport stack lives in `KBaseX64.dll` (exported): `CoreNet::KNetDelegate/KNetSocket/
  KNetBuffer` typed serialization (Int8..UInt64, float, string); `KAESCryptor` +
  custom `KSimpleCryptor`.
- Fixed-size protocol table (`uDataLen == m_nProtocolSize[id]`) + protocol recorder/replayer.
- Server-authoritative gameplay: `OnSyncMoveState/MoveCtrl/MoveParam`,
  `OnAdjustPlayerMove`, `Self/TargetMoveStateMask`, `MOVE_STATE_*` action rejects,
  skill lifecycle `OnSkillPrepare/Cast/Channel/EffectResult/BeatBack/RayEffect`,
  cooldowns server-pushed (`reset/pause/accelerate`).
- Separate local vs remote character classes with independent `Interpolate`.

**INFERRED**
- 20-bit sequence field packing (bits 60/61) — read, semantics inferred.
- "Ack = peer *processed* the message" rationale (app-level ack on top of TCP).
- Server tier topology from message prefixes (`R2C/L2C/G2C`).

**UNKNOWN**
- Field layouts of the ~900 remaining opcodes (only 1, 6, 0x6E, 0x2FE decoded).
- Encryption/compression order on the wire.

**Reference implementation exists** (`tools/netcode/reference/jx3_model.py`, 10/10
smoke): it implements the *model* with our own opcodes — it does **not** speak
JX3's bytes and is not a client replacement.

## 2. Skill / ability data

**VERIFIED**
- Extraction pipeline: `Tani.rt` (GBK table) → `PakV4SfxExtract.exe` → `.tani`
  (GATA) → base `.ani` (MIN2) → per-frame root-bone (`bip01`) motion.
- Authored motion float-blocks inside `.tani` (pattern `0.00 1.00 0.00 <dx> <dz> ...`):
  - 太阴指 (万花): `(130.8, 284.5)` and `(15.0, -156.3)` — magnitude 157.0
    matches measured root arc peak 156.7.
  - 玉泉鱼跃 15a (藏剑): `(76.9, -61.8)`.
  - Control 风来吴山 (spin): **no** motion blocks, root net 0.0.
- Real tables (Aug-2026 build raw files, parsed):
  - `Represent/skill/skill_dash.txt` — SkillID → AnimationID for dashes.
  - `Represent/player/player_skill_move_animation.txt` — SkillMoveID → AnimationID (+ bAllowSkillMoveDst).
  - `Represent/camera/skill_move_camera.txt` — per-skill camera FX with real
    values (enter/exit ms, FOV rad or fixed deg, duration ms, screen-effect id,
    edge color temp 0–10, saturation 0–1).

**INFERRED**
- Motion float-block field layout (the vector interpretation is supported by the
  magnitude match; full GATA MotionTag structure not decoded).
- Engine units → meters calibration (1 unit ≈ cm suggested by terrain scale=100;
  not confirmed).

**UNKNOWN**
- Server-side numbers (damage, authoritative dash distance) — runtime-synced, not on disk.

## 3. Camera system

**VERIFIED**
- ECS architecture: `camera` component + named `"camera controller"` components;
  controller lookup order (sprint → carrier → move/pitch → focus-face →
  dynamic-follow) read from `strncmp` sequence in `SetCharacterCameraPosition`.
- Mode names/events: `SwitchCameraMode`, `OnSetLocal/Remote/God/HomelandCameraMode`,
  `OnEnableAirCombatCamera`, `EnterCarrier`, `LeaveSkillMoveCamera`, `SmoothTo*`.
- Follow math: anchor (mount socket → custom offsets → head → raw pos priority)
  + offset rotated by yaw/pitch via sin/cos, then exponential smoothing
  `current += delta·dt/SmoothTime` with dead-zone snap.
- Machine-verified default constants (current build's effective values,
  `proof/netcode/camera_defaults_verified.txt`):
  `CameraMaxDeltaYaw = 2π`, `CameraMaxDeltaPitch = 1.56`,
  `TargetDistance = 1.0` (clamped), `SmoothTime = 1.0`,
  `InitCameraPitch = π` (sentinel), `InitCameraAngle = 0`,
  `CameraAdjustYawWhenMoveTurnDisableAngle = 0.26 rad (15°)`,
  10-row move-pitch table defaults all 0, bool flags false.
- 10-row loader: 10 iterations, stride 0x24, key set `ZoomLength` +
  `CameraMovePitch*` + `CameraAdjustYawWhenMoveTurn*`.
- Camera shake updater (`0x180B10A70`): cos curve over period, amplitude × decay
  per cycle, position jitter + rotation, self-disables after max cycles; plus a
  `rand()`-based jitter branch.
- Cinematic track camera: spring system (`RunSpringSystem`), `KRLCameraAni::SyncCamera`.
- Config plumbing: `KFilePath` loads `Represent\filepath.ini`;
  `CameraConfig = Represent/camera/config.ini`, `CameraLockTargetConfig`,
  per-mode `*.krl.txt`, `CameraCommon = camera_common.krl.txt`.
- krl loader (`sLoadNumberFromFile` @ `0x180857DF0`): key-value float file,
  full key vocabulary extracted (Camera*, Sprint*, Carrier*, TitleAdjust*, etc.).
- The `represent` camera tree is **absent from the current (Sep-2026) client**:
  official downloader synced against Seasun's manifest and reported 0 missing
  files; local paks contain no `represent` entries (exhaustive index scan).

**Later passes (verified additions) + remaining mixed-status items**

**Fifth pass — new machine-verified data (see `proof/netcode/disasm/`):**
- **Air-combat params — VERIFIED defaults** (`LoadAirCombatParams`, loader @
  `0x180AC9C00`; machine-paired by `verify_camera_defaults.py`):
  `InitCameraDistance = 2000`, `AdjustEyeScale = 0.2`, `YawRange = 70`,
  `EnterYawAngleSpeed = 500`, `EnterPitchAngleSpeed = 500`, `FinalYawAngle = 30`,
  `ScreenWidthLimit = 0.2`, `ScreenHeightLimit = 0.25`.
- **Camera shake model — VERIFIED**: logic triggers `OnSetCameraShake(int)`
  (event adaptor), config rows from `CameraShake` table
  (`KTableList::GetCameraShakeConfig`) with keys `ShakeType`, `ShakeIntensity`,
  `ShakeTotalTime`, `ShakeCycleCount`, `ShakeOffsetX`, `ShakeOffsetY`,
  `ShakeDecayRate`, `ShakePeriodTime`; updater `0x180B10A70` applies the
  cos/decay/jitter curve. Table values remain a data gap.
- **Follow-action — mechanism VERIFIED**: enum stored at camera object `+0x27C`
  (`SetCameraFollowCharacterAction` @ `0x180ACE370` writes it; nonzero enables
  the mode). `SetCharacterCameraPosition` then resolves the `s_face` named
  target as the camera look-at (`0x180B0F152–0x180B0F187`). Min/MaxDis/Speed
  keys are loaded (krl) — the distance blend math in the caller is still open.
- **Skill-move camera — partial VERIFIED**: config field `+0x22C` = **duration**;
  applied as an expiry timestamp `now + duration` at `[obj+0x8308]`
  (`0x180542440`) and gated by character flag bit 30 (`bt esi, 0x1E`); FOV
  interpolation curve still open.
- **Camera animation (cinematic) — VERIFIED**: `KRLCameraAni::SyncCamera`
  (`0x1805AE2xx`) writes track position into camera `[+0x44/0x48/0x4C]` and
  look-target into `[+0x50/0x54/0x58]` each frame, then engine setters via
  vtable (`+0x30`, `+0xA0`, `+0x50`, `+0xA8`).

**Remaining items (mixed status — read each label):**

1. **Mouse input path** — corrected by evidence: `MouseMoveCamera` (`0x180B240D0`)
   is the **homeland housing camera** (screen-edge drag via X3DEngine `IView` +
   auto-rotate flags), not the follow camera. The follow/sprint camera mouse
   path is `ClampMouse` (`0x180B1FE70`, sprint + glider branches). Raw
   sensitivity handling (multiplier source) still open.
2. Follow formula axis convention — **VERIFIED** (closed):
   `offset = ( cos(yaw)·sin(pol)·A + sin(yaw)·B,  cos(pol)·A + C,  sin(yaw)·sin(pol)·A − cos(yaw)·B )`
   where yaw = param0 (yaw=0 → +X, yaw+ → +Z), pol = param1 (angle from
   vertical-down; pitch-from-horizontal = π/2 − pol), A/B/C = the
   `CameraPositionOffset` fields `[c8/cc/d0]` = distance / lateral / height.
   Evidence: π/2 const @ `0x180C8DAC8` (`0x180B0F1D5`), sin/cos sequence
   `0x180B0F1FA..0x180B0F28B`, per-component smoothing `0x180B0F2BA`. The
   reference implementation now uses exactly this form.
3. **Sprint camera pull-back — structure VERIFIED** (`ClampMouse`,
   `0x180B1FE70` sprint branch `0x180B20140–0x180B20200`):
   - int speeds read from sprint controller fields `[+0x1D8]`/`[+0x1DC]`
     (getters `0x180611BB0` / `0x180611B90`), converted to radians by
     **π/128 = 0.024543693** (const @ `0x180D0CF64`);
   - slew-rate limiter `fn(controller, angle)` @ `0x180B13B60`:
     `return clamp(angle, -dt·[rcx+0x5C], +dt·[rcx+0x5C])`;
   - final mouse pitch clamp = `clamp(speed_angle, max(min_angle, base + result)) − base`.
   The per-mode numeric values remain in the missing krl data (data gap).
4. Move-pitch consuming code — **closed by the seventh pass** (superseded):
   rate-limited pitch motion (π/3000 rad/ms ≈ 60°/s) + slope refresh at
   `>1000 ms`; the adjusted angles go through `AdjustCharacterCameraPosition`
   (`0x180AC8F10`, single caller `0x180B1006E`) into
   `[r14+0x38/0x3C/0x40]` and the height `C` at `[r14+0x8C]`. Mapping of these
   constants to the krl keys `CameraMovePitchSmoothTime` /
   `CameraMovePitchApplyTimeInterval` is inferred, not proven.
5. `MODE_ROWS` numeric values in `tools/netcode/reference/camera_model.py`
   (height 2.0, distance 6.0, smooth 0.08, speeds 60…) — **INVENTED tunables**,
   except the annotated verified ones.
6. Obstruction: engine flag `bObstructdAvert` is real; the pull-in algorithm
   (hit−0.2, smooth return) is our own — INVENTED.
7. Track-camera spring constants (k=60, c=12) — INVENTED.
8. "10 rows = zoom levels" — INFERRED from `ZoomLength` being the first key.
9. Shake: burst/jitter curve VERIFIED and the config schema is VERIFIED
   (`OnSetCameraShake(int)` → `CameraShake` table keys); only the table's
   numeric rows remain a data gap.
10. Skill-move camera — **duration/apply verified (seventh pass), FOV curve open**:
    tag application `KRLAnimationFeedBackTag::ApplySkillMoveCameraTag`
    (`0x1802F8D20`), controller `0x180B1B1E0` (`pcSkillMoveCameraParam`),
    config reader `0x18082C3C0` (`SkillMoveCamera` key), disable/leave
    (`OnDisableSkillMoveCamera` `0x1803174B0`, `LeaveSkillMoveCamera`
    `0x180ACC8E0`), apply fn `0x1804FBAE0`. The real enter/exit/FOV table
    values are already extracted; only the FOV interpolation curve itself
    remains to decode.

**Sixth pass — curve consumers narrowed (exact addresses for pickup):**
- The per-frame camera update is one large region
  `SetCharacterCameraPosition ~0x180B0E820 .. ~0x180B10350` (follow placement,
  inline follow-action `s_face` step, `AdjustCharacterCameraPosition` call at
  `0x180B1006E`, shake apply at `0x180B0F452`/`0x180B10A70`). The move-pitch and
  follow-action blend math lives inside it:
  - move-pitch angles struct: `[rbp-0x38]/[rbp-0x34]/[rbp-0x30]` → r8 of
    `AdjustCharacterCameraPosition`; results to `[r14+0x8C]` (height `C`) and
    `[r14+0x38]`.
  - follow-action config fields: `MinDis = [config+0x278]`,
    `MaxDis = [config+0x27C]`, `Speed = [config+0x280]` (exact offsets from the
    loader at `0x180858FD0–0x180859028`); the blend reads run inside the same
    per-frame update (`[rsi+0x54]` etc. in the `0x180B0FED0–0x180B10350` block).
  - camera interpolation state: `[r14+0x22C..0x24C]`, `[r14+0x220/0x238]`,
    applied via `0x180B0E6E0` + `0x180B0E370`.
- Skill-move FOV curve: trigger/duration verified; curve in helpers called from
  `KRLAnimationFeedBackTag::ApplySkillMoveCameraTag` (`0x1802F8D20`) /
  controller `0x180B1B1E0`.
- Carrier smoothing (`EnterCarrier`) was decoded in the seventh pass:
  `SmoothToCarrierCamera` (`0x180AD0230`) per-axis exponential step, ratio
  clamped `[0,1]`.
- Caveat: the `+0x278/0x27C/0x280` offsets also exist in unrelated structs
  (e.g. `RLSkillShadow`), so only reads against the camera config object count.

**Seventh pass — move-pitch / slope, carrier, skill-move apply (decoded):**
- **Move-pitch path (`AdjustCameraForSlope`, asserted @ `0x180B0FEFA`; blend
  `0x180B0FF45–0x180B1001E`):**
  - `[rsi+0x54]` = the row's apply-angle; if > ε:
    `angle[i] += slopeOffset[i] · weight(xmm11) · [rsi+0x54]`;
  - slope offsets stored `[r14+0x14C/0x150/0x154]`, refreshed only when
    `now − [r14+0x158] > ` **1000.0 ms** (double const `0x180C8E3D0`);
  - `AdjustCharacterCameraPosition` called at `0x180B1006E` with the adjusted
    angles; outputs to `[r14+0x38/0x3C/0x40]`.
- **Height state machine (`0x180B100F0–0x180B10283`, state `[r14+0xC0]`):**
  - states 1/2/3/4; 3→1 and 4→2 resets; transitions gated by
    `[r14+0xB4]`, `[r14+0xB8]` vs `[r14+0xBC]`, `[r14+0xE4]`;
  - state 2 = instant height `[r14+0x8C] = target`;
  - state 1 = smooth: `target = [rdi+0x24] − dt·(π/3000)` (const
    `0x180D0E180` = `0.0010471976` rad/ms ≈ 60°/s), clamped so height never
    drops below current, switches to state 2 on reach;
  - final apply always: `[r14+0x38/0x3C/0x40] = adjusted angles`.
- **Carrier smoothing (`SmoothToCarrierCamera` @ `0x180AD0230`):** per-axis
  exponential step — delta divided by a time factor from `[rcx+0x44]`, ratio
  clamped to `[0,1]` (consts `1.0` @ `0x180C82240`, abs-mask @ `0x180C8D240`),
  per-axis states `[rdi+0x60..0x6C]`, outputs yaw/pitch/angle
  (`pfRetYaw`/`pfRetPitch`/`pfRetAngle`).
- **Skill-move apply (`0x1804FBAE0`, called from the tag path):** stores params
  to `[obj+0x7458/0x7460/0x7464]`, rotates them via `0x180024A78`, writes
  `[obj+0x746C/0x7470/0x7474]` and `[obj+0x7448]`, then invokes the engine
  camera vtable `+0xA48`.

**Eighth pass — engine chase camera + real engine configs found:**
- **Engine chase camera** (`KG3DEngineX64.dll` loader `0x1804C1660`): opens an
  ini via `g_OpenIniFile` and reads **section `[Camera]`** with keys
  `nChaseType`, **`bObstructdAvert`**, `fChaseRate`, `fMaxDistance`,
  `fMinDistance`, `fMaxAngelVel`, `fMinAngelVel`, `fAngelRateHor`,
  `fAngelRateVel`, `fDisZoomRate`, `fFlexCoefficient`, `fDampCoefficient`,
  `bUseFlexibilitySys`, `fFlexRate`, `fDistance`, `fAngleHor`, `fAngleVel`,
  `fLootAtOffsetY`, `bLockedCamera`, `fFovy`.
  Struct field offsets mapped: `+0x10` bObstructdAvert, `+0x18` nChaseType,
  `+0x1C` fChaseRate, `+0x20/24` max/min distance, `+0x28/2C` max/min angle vel,
  `+0x30/34` angle rates, `+0x38` fDisZoomRate, `+0x3C/40` flex/damp,
  `+0x44` bUseFlexibilitySys, `+0x48` fFlexRate, `+0x4C` fDistance,
  `+0x50/54` fAngleHor/Vel, `+0x58` fLootAtOffsetY, `+0x5C` bLockedCamera,
  `+0x60` fFovy. Interpretation (MED): `fFlexCoefficient` / `fDampCoefficient` /
  `fFlexRate` are the engine chase camera's spring/damping parameters (values
  live in the ini) — this is the engine-side third-person camera, separate from
  the client's SO3Represent follow camera.
- **Engine `data/public` configs are NOT in the client paks** — they stream
  separately. The downloader's debug cache
  (`SeasunDownloaderV2.4\seasun\client\_HttpFileForDebug_\local\data\public\`)
  contains real copies, including:
  - `lookatconfig.ini` — head/neck look-at bone segments with REAL values
    (`BoneSegment0`: Spine1→Spine2, ThresholdAngleDifference 30,
    BendingMultiplier 0.4, MaxAngleDiffenece 90, MaxBendingAngle 20,
    Responsiveness 2.5; `BoneSegment1`: Neck→Head, 20 / 0.9 / 30 / 75 / 4, …),
    `FaceSocket=s_face`, `OverrideAnimation=0`.
  - `flexconfig.ini` — `fDamping = 0.675` / `0.2` (flexibility system).
  - `3denginesettings.ini`, `environmentdefault.ini`, `physics.ini`, etc.
- The `[Camera]` chase ini itself was **not** in the cache (also not in local
  paks) — see missing list.

**UNKNOWN (definitive missing list)**
1. `represent/camera/camera_common.krl.txt` per-mode row values — absent from
   the current client build (Aug build only).
2. `CameraShake` table rows (values for ShakeType/Intensity/TotalTime/…).
3. Engine chase camera `[Camera]` ini values (the file is neither in the local
   paks nor the downloader cache; likely streamed on demand).
4. `CameraLockTargetConfig.txt` row values (loader known, file absent).
5. `CameraSegmentConfig.json` values.
6. Mouse sensitivity defaults (`userdata\custom.dat`, binary).
7. Engine obstruction algorithm internals (only key + struct offset known:
   `bObstructdAvert` at `+0x10`).



## 4. Where everything lives

```
docs/netcode/
  JX3_NETCODE_RESEARCH.md      netcode findings + evidence
  JX3_PROTOCOL_SPEC.md         wire/session spec (JX3 side)
  REBORN_SERVER_SPEC.md        our server+client contract
  JX3_CAMERA_RESEARCH.md       camera findings (all passes)
  REBORN_CAMERA_SPEC.md        our camera contract
  SKILL_DATA_EXTRACTION.md     skill-data pipeline method note
proof/netcode/
  *_net_strings.txt, *_all_strings.txt, imports/exports, disasm/*.txt,
  c2s_protocol_catalog.tsv, camera_defaults_verified.txt, skill_motion/*.json
tools/netcode/
  extract_strings / scan_net_strings / xref_string / xref_va / dump_floats /
  verify_camera_defaults / measure_skill_motion / dump_motion_floats / ...
tools/netcode/reference/
  jx3_model.py   (netcode model, 10/10 smoke)
  camera_model.py (camera model, 13/13 smoke)
```

## 5. Open items / possible next steps

- **Remaining decode work:** skill-move FOV interpolation curve
  (`ApplySkillMoveCameraTag` helpers) and engine obstruction internals
  (`bObstructdAvert` at struct `+0x10`); mouse-input sensitivity multiplier.
- **Remaining data:** the seven items in the missing list above — per-mode krl
  rows, shake rows, engine `[Camera]` ini, lock-target/segment configs,
  `custom.dat` sensitivity. Sources: an Aug-2026 client install or a runtime
  read; the reference now accepts them via `camera.json` with no code changes.
- **Protect sibling artifacts:** only this file is committed; the tools, proof
  dumps, and specs in `docs/netcode/` are still uncommitted in this worktree.
- Product building (C# engine-host camera, server+client) — **not started**,
  intentionally; this file is the handoff.
