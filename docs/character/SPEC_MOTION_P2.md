# D2 — Per-skill displacement + camera-follow (太阴指 / 风来吴山), client truth

**Area:** character · **Branch:** agent/3x-integration · **Index:** `docs/character/README.md` · **Status:** client-truth addendum 2026-10-07 - corrects SPEC_MOTION.md / SPEC_STATES.md where marked; supersedes any host assumption it contradicts.

**Scope:** correct `docs/character/SPEC_MOTION.md` (branch `agent/3x-motion`) where it assumes
SkillMove.tab is *the only* displacement source and camera-follow is one global rule.
**Truth sources (read-only):** HD `JX3ClientX64.exe` 1-5-0-9975 (sha256 `95A651D8…`, 11,349,432 B)
and EXP 1-6-0-9536 (11,823,024 B); HD `JX3RepresentX64.dll`; HD/EXP pak tables extracted with the
official `PakV4SfxExtract.exe`; shipped skill scripts (LuaQ bytecode, constants decoded with
`tools/netcode/lua51_constants.py`).
**Scratch (everything this pass):** `C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x\`
(`d2_*` files, `hd_out\`, `d2_ex\`, `d2_cam\`, `d2_exp_JX3ClientX64.exe` copy). Installs untouched,
no engine runs, no repo writes.
**Confidence:** HIGH = string/assert xref + instruction-level check; MED = inference; LOW = unprobed.

Skill IDs (verified in `settings/skill/skills.tab`, HD store, 41,436 rows, 118 cols):

| Skill | SkillID | Kind | School | ScriptFile (shipped) | Related |
|---|---|---|---|---|---|
| 太阴指 | **228** | Leap (轻功) | 2 万花 | `万花\万花_轻功_震地拔冲.lua` (2,535 B) | dmg sub 497; buffs 6261 (+30% speed), 6262 (−35/40% dmg) |
| 风来吴山 | **1645** | Physics, IsChannelSkill=1 | 6 藏剑 | `藏剑\藏剑_灵峰剑式_风来吴山.lua` (3,265 B) | dmg sub 1905; buffs 1856 (不工), 2151 (+10% speed), 1947 (target −20%) |

Cast animations (`player_animation_f1.txt`): 228 → `skill_dash.txt` 347 =
`F1s01wh点穴19_太阴HD.tani`; 1645 → `skill_tag.txt` 719 =
`F1s07cj重剑技能15_风来吴山HD.tani`.

---

## Skill->move mapping

**Result: there is no shipped skill→SkillMoveID mapping, and neither skill uses a SkillMove.tab row.**

1. `settings/ObjSlotInfo.tab` — the only client-side slot→move table
   (`SlotID/OffsetDistance/OffsetDirection/OutSkillMoveID/OutOffsetFaceDirection/OwnerMask/
   SubordinateMask`): **all OutSkillMoveID = 0** in both stores. HD/EXP extracted copies are
   byte-identical (2,626 B, 138 data rows; decoded-text sha `90898f56…`); `spec3x\hd_out\settings\
   ObjSlotInfo.tab` == `spec3x\ex_slot\settings\ObjSlotInfo.tab`. HIGH.
2. `skills.tab` has **no move column** (118 cols checked; only `HitStiff*` for hit reactions).
   HIGH.
3. The skill scripts (shipped in the client pak, `scripts/skill/**`, LuaQ bytecode with readable
   string table) carry the **movement primitive** as a server-side skill attribute:
   - 228 adds `ATTRIBUTE_TYPE.DASH_BACKWARD` (enum id **307** = `0x133`, dumped from the client
     exe: `attribute_type_table.txt` line 320) with frames `16` and `tSkillData.nSpeed`.
   - 1645 adds `ATTRIBUTE_TYPE.CAST_SKILL_TARGET_DST` (id 348) — a **channel re-cast**, not
     movement. No dash/move token in any FLWS script (base + `_伤害`).
4. Live, the move arrives from the server in `KPlayerClient::OnAdjustPlayerMove` (SkillMoveID at
   `pkt+0x42`, mode/blend/facing/heading/VXY/VZ in the packed word) and the 0x20-byte per-frame
   move-record queue (SPEC_MOTION §2.1–2.2). The client's `KCharacter::SkillMove` path only
   replays a row the server names. **No client data chooses the row.**
5. `Represent/skill/skill_dash.txt` (SkillID→cast AnimationID) and `Represent/player/
   player_skill_move_animation.txt` (SkillMoveID→rush AnimationID) are **visual** maps only. The
   small-ID coincidences (skill_dash 228→347 vs SkillMove 347→85130, 719→75381) are different ID
   namespaces (85130 = `F1sqg04sl…`, not 太阴) — do not cross-map them. HIGH.

**Consequence for the host:** for `DASH_*` skills the host authors the primitive from the script
constants; for FLWS there is nothing to author (see next section). Fabricating a SkillMove row or
scoring one by similarity violates AGENTS §6 — none exists.

---

## Per-skill displacement (太阴指 / 风来吴山 verified)

### 太阴指 (228) — backward dash, 16 ticks × nSpeed

- Script constants (`tools/netcode/lua51_constants.py` on
  `spec3x\hd_out\scripts\skill\万花\万花_轻功_震地拔冲.lua`, raw const dump:
  `spec3x\d2_taiyin_constants.json`): `tSkillData.nSpeed` per level =
  `[60, 125, 226, 314, 401, 488, 576]`; the `Apply` proto contains `DASH_BACKWARD` and the
  constant **16** (frames). Level 1: **16 × 60 = 960 u = 15 尺** backward (1 u = 1 cm,
  1 尺 = 64 u). Cross-check: `docs/netcode/SKILL_DATA_RESEARCH.md` §3/§6.3 (绝境·太阴指
  16×nSpeed, nSpeed=60 → 960 u = 15 尺). Tooltip (Skill.txt, verbatim): “解除自身减速、锁足、被击僵直
  效果并**向后疾退**…”. HIGH (attribute + params); MED-HIGH (exact frames/nSpeed pairing).
- Client primitive `KCharacter::Dash` — **the worker `KCharacter::LuaDash` calls** (HD
  `0x14030F7C0`; called from `LuaDash` `0x1403DD230` @0x1403DD2E9 and `ProcessCatchDash`
  `0x1403AB0AC` @0x1403AB25D). Disasm semantics (HIGH): reject while state∈9..0x16; clamp
  `frames=arg1` 1..255 → `[+0xC08]`; `dir=arg2 & 0xFF` → `[+0x26C]` (**absolute heading byte**,
  not a delta); `speed=arg3` clamp 1..127 → `[+0x2F8]` (+`<<4` copy `[+0x268]`); `z=arg4`
  clamp −2048..2047 → `[+0x270]`; state `[+0x1F4]=0x11`; no SkillMove.tab read, no facing write.
  EXP twin: `LuaDash` `0x140418050` → Dash `0x1403399F0` (same clamp/store pattern; field
  offsets shift +8: VXY `[+0x300]`, remaining `[+0xC28]`; extra tail code — MED).
  `ProcessCatchDash` passes `dir = [char+0x44]` (facing byte) → dir is absolute. Therefore
  DASH_BACKWARD = play the same primitive with `dir = facing + 0x80` (name + tooltip HIGH;
  the +0x80 computation itself is MED — server-side applier not shipped).
- **There is no SkillMove.tab row for this.** A full scan of the 935 rows found no
  16-frame/constant-60/backward row; rows with Total=16 (117–121, 594–615) are jump/ramp/airborne
  profiles with different DirectionXY. State the dash as the primitive, not as a row.
- The `.tani` motion block (`F1s01wh点穴19_太阴指_悟.tani` group2 type2 @0x1EE4, key time=6) is a
  generic `type=0` tag, payload `00000000 0000803f` (f32 **1.0**), identical in FLWS — authoring
  metadata, not displacement (`d2_mtag_payload.py`).

### 风来吴山 (1645) — no scripted displacement; walk-during-channel

- Script (`d2_ex\scripts\skill\藏剑\藏剑_灵峰剑式_风来吴山.lua`; constants
  `proof/netcode/skill_data/flws_constants.json`): `AddAttribute(EFFECT_TO_SELF_NOT_ROLLBACK,
  CAST_SKILL_TARGET_DST, EXECUTE_SCRIPT, …)` + channel params; `OnTimer` (`fn2`) does
  `AddBuff(1856…)`, `XiaohaoJianQi`, `CastSkillXYZ(nX,nY,nZ)` (re-cast the damage sub-skill at the
  current position). **No DASH/SKILL_MOVE/velocity attribute anywhere.** HIGH.
- Animation root motion: `proof/netcode/skill_motion/flws_control.json` = **net 0.0, no motion
  runs** (`SKILL_MOTION_METHOD.md` §measurements); the spin is in the clip's bones. The `.tani`
  motion block is the same benign float-1.0 tag. HIGH.
- Movement during the channel is **player input**: buff **1856 “不工”** = `atImmunity`(control) +
  `atImmuneSkillMove=1`; UI Buff.txt: “无法跳跃，不受控制招式影响” — it blocks **jump**, not walk.
  Buff **2151 “随风”** = `atMoveSpeedPercent +102` (+10%); 1947 “惊风” is the −20% on targets.
  Talent texts confirm the distinction (“站立/非站立” damage, “无需自身旋转” variant). HIGH (buffs/
  tooltips).
- **Does it displace? No scripted displacement; how much: 0 units.** The character visibly spins
  (clip) and may walk (input) at ×1.10 while channeling. If a live server ever adds a positional
  drift it is not in shipped client data (open probe P1 below).

---

## Camera-follow discriminator (exact)

**The client has no per-skill camera-follow flag for these skills; camera-follow is global and
positional, and the user-visible difference is displacement vs no displacement.**

1. **Position follow (always).** `SetCharacterCameraPosition` (`JX3RepresentX64.dll` 0x180B0E820,
   docs/camera) anchors the camera on the character position (head-bone/custom-offset chain) and
   springs toward the desired offset. A skill that changes the logic position (Dash/SkillMove)
   translates the anchor → the camera follows; a skill that does not (FLWS) leaves the camera still
   unless the player moves. HIGH (mechanism) / MED (per-frame mapping).
2. **Yaw follow is input-only.** The only move-turn coupling is `CameraAdjustYawWhenMoveTurn`:
   shipped values in this build — `Represent/camera/camera_common.krl.txt` per zoom row:
   `CameraAdjustYawWhenMoveTurn = 0.013` rad/frame, `…DisableAngle = 0.45` rad (the 0.26 rad in
   docs is the code default only). Comment (verbatim): “角色移动的方向与镜头方向夹角小于该值，则
   不触发镜头水平旋转” — it compares the **movement direction**, never the skill heading/facing/
   animation. Nothing in the dash/skill-move path writes the camera. HIGH.
3. **Per-skill camera systems exist but do not cover these skills** (all checked):
   - `Represent/camera/skill_move_camera.txt` (FOV/post-FX) rows: 0, 3119, 20788, 21000, 25252,
     100182, 124841, 200415 — no 228/1645.
   - `Represent/camera/camera_follow_animation.txt` = `PlayerAnimationEnableCameraFollow`; consumer
     `KRLCharacter::UpdateAnimationEnableCameraFollow` 0x180500820 → lookup in the table
     (KTableList) by id → `SetCameraFollowCharacterAction` thunk 0x180ACE370 (face look-at mode).
     All 68 IDs are emote/pose/fashion/stall anims (`F1b01ty…千`, `pst01..08`, 时装, 摆摊,
     `jy摩天轮`); neither 347 nor 719 is present. HIGH.
   - `Represent/camera/DynamicFollowCamera.krl.txt` (HoldCamera + rotate/translate smoothing,
     teleport/blink), configs 0–4 named for 段氏风流云散 / 明教流光囚影 / 傀儡. Invoked by server
     scripts via RemoteCallToClient; neither skill script references it. HIGH (absence).
4. **So the discriminator is the movement primitive**, not a camera record:
   - 太阴指: `Dash(16, facing+0x80, nSpeed, 0)` → logic position moves backward → follow camera
     translates with it; **no yaw change** (heading only, facing untouched).
   - 风来吴山: no Dash/SkillMove → logic position static → camera static (spin is clip-only; input
     walking still moves it via rule 2). The host must never feed clip/facing/heading yaw to the
     camera during it.

Ruled out with evidence: `skill_move_camera.txt` rows (no 228/1645), `DynamicFollowCamera`
(script-invoked only), `camera_follow_animation.txt` (emote/fashion IDs only), per-skill camera
script calls (both scripts' full string tables contain no camera token).

---

## Corrections to SPEC_MOTION.md

| § | Current text (branch `agent/3x-motion`) | Correction | Evidence |
|---|---|---|---|
| §0.1, §1.2, §2.3 | “The displacement is … `SkillMove.tab`, applied by `OnSkillMove`” / “`settings/SkillMove.tab` — the only per-frame displacement source” | **Not the only mover.** A second primitive exists: `KCharacter::Dash` (HD 0x14030F7C0 / EXP 0x1403399F0), driven by the script `DASH_*` attributes (DASH_BACKWARD 307 …). It writes `[+0x2F8]`(VXY)/`[+0x270]`(VZ)/`[+0xC08]`(frames)/`[+0x26C]`(**absolute** heading byte), state 0x11, and never reads SkillMove.tab. SkillMove.tab is read only by `KCharacter::SkillMove` (SKILL_MOVE id 311 / SPECIAL_SKILL_MOVE 616). | `d2_xref_luadash.txt`, `d2_xref_luadash_exp.txt`, Dash disasm HD 0x14030F7C0; attribute enum table |
| §2.3 step 1 | “Store, per skill: `SkillMoveID` (row in SkillMove.tab)” | For dash skills there is **no row**: store the primitive (`type=DASH_BACKWARD\|DASH_FORWARD\|…`, `frames`, `speed`, `z`; dir = facing ± offset). SkillMove.tab rows remain valid only for SKILL_MOVE-type skills. Do not invent a skill→row map (ObjSlotInfo is all-zero in both stores; skills.tab has no move column). | ObjSlotInfo both stores; script constants; Dash disasm |
| §0.3/§1.4 (太阴指) | in-place lunge / no dash | 太阴指 **is a real 960 u (15 尺) backward dash** — the tani is in-place, but the script applies DASH_BACKWARD(16, nSpeed=60 at lv1). Measured tani net 10.37 u is the visual sway, expected. | `d2_taiyin_constants.json`; SKILL_DATA_RESEARCH §3/§6.3; tooltip |
| §2.3 (FLWS) / new | absent | FLWS has **no scripted displacement**: channel attribute `CAST_SKILL_TARGET_DST`, root motion 0.0. During the channel **movement input stays live** (only jump and control are blocked — buff 1856) with +10% move speed (buff 2151). The spin is the clip. | flws script/constants; flws_control.json; Buff.tab 1856/2151; tooltips |
| §1.5/§4.1 camera | yaw dead zone 0.26 rad; “camera never chases the skill heading” | Keep the “never chases heading”, but use the **shipped** values in this build: `CameraAdjustYawWhenMoveTurn=0.013`, `…DisableAngle=0.45` (`camera_common.krl.txt`); 0.26 is only the code default. Add: per-skill camera overrides exist only as `skill_move_camera.txt` (FOV), `DynamicFollowCamera` (script-invoked hold/smoothing), `camera_follow_animation.txt` (face look-at for emote/fashion anims) — none applies to 228/1645; the observed follow difference is displacement vs in-place spin. | `d2_cam_dump.txt`; `d2_xref_updatecam.txt`; skill_move_camera rows |
| §5 acceptance | generic rows 300/700 | Add the two skill cases (below) and the walk-during-channel rule. | — |

**Where the client is server-driven (exact record):** live, the server decides and sends
`OnAdjustPlayerMove` — facing `pkt+0x17`, turn rate `pkt+0x18`, remaining frames `pkt+0x19`,
packed heading/VXY/VZ/height/gravity `pkt+0x23`, 7 flags `pkt+0x33`, destination `pkt+0x35`,
height/gravity accumulators `pkt+0x3D/0x3E/0x40`, **SkillMoveID/mode `pkt+0x42`**, blend
`pkt+0x46` — plus the 0x20-byte per-frame move-record stream (producer 0x140140AA0; record layout
in SPEC_MOTION §2.2). The DASH attributes themselves are applied by the (not shipped) server code;
the shipped script parameter block is the client-side counterpart. For FLWS the server record is a
channel/state record; the only position change comes from the player's input.

---

## Host acceptance criteria

Data model (replaces “skill→moveID map” for these skills):

1. **太阴指 cast (level 1).** On cast at facing θ: run 16 logic ticks (15 Hz), per tick moving
   `60` u along heading byte `h = θ_byte + 0x80` (mod 256); total ≈ **960 u (15 尺)** ±1 tick
   before collision. Facing/model yaw stays θ (animation sway only). `z` velocity 0, gravity normal.
   No blend (blend is a server packet field).
2. **太阴指 camera.** Camera yaw/pitch unchanged by the dash (numeric fingerprint over the move,
   e.g. `RC_CAM_YAW` log stable ±1e-6); the camera anchor/position follows the translated
   character through the normal spring (no teleport, no snap beyond SmoothTime). FOV unchanged
   (no `skill_move_camera` row for 228).
3. **风来吴山.** Casting + channeling adds **0 units of scripted translation** (position delta with
   no input = 0 ±1 u) in every direction; the spin is the clip. WASD input remains active during
   the channel and moves the character normally (×1.10 move speed while the damage buff is up);
   **jump must be rejected** during the channel; control-immune (no pull/knock).
4. **风来吴山 camera.** Camera yaw does **not** change because of the spin/facing/animation (log
   yaw before/during/after the spin; only input turn per `CameraAdjustYawWhenMoveTurn` dead zone
   0.45 rad may move it, and only when the player actually moves). With no input the camera
   position does not move. FOV unchanged (no row for 1645).
5. **No invented data.** If a SkillMove row is used anyway for authoring, it must be built from the
   script constants above and labelled host-authored; the shipped map is empty and must stay so.
6. **Evidence/logging.** Log per cast: primitive kind (dash/none), frames, speed, heading byte,
   moved units, and camera yaw start/end; FLWS: channel active + move-allowed/jump-rejected lines.
   These are the numeric fingerprints for the regression gate.

### Open probes (only if the user observes drift on FLWS)
- P1: a live client cannot be inspected without hijack; the shipped data says “no displacement”.
  If a real positional drift is observed while channeling FLWS with no input, the server move
  record for it is the missing piece — watch `OnAdjustPlayerMove`/queue via a legal local
  replay only. Do not invent a row before that.
- P2: exact server applier for `DASH_BACKWARD` (dir = facing+0x80) is not shipped; if the host
  wants byte-exact facing offsets for the family (DASH_FORWARD/LEFT/RIGHT/TO_DST_BACK), the same
  Dash semantics apply with the direction computed server-side.

## Implementation status (2026-10-07, `agent/3x-motion` @ 94c9efa + WIP)

Implemented as written (host: `client/SkillMotionMap` + `RebornClient.cs`):
228 = the Dash primitive (16 × nSpeed(60) along heading byte facing+0x80; facing
untouched; state 0x11 modelled as a 16-tick move state); 1645 = channel (no
displacement; walk live ×1.10; jump rejected; `RC_SKILL_MOVEID` still authors
SkillMove rows for the generic SPEC_MOTION §5 tests). The host's camera yaw-follow
dead zone is now the shipped **0.45** (`CameraSystem.DefaultRow`; 0.26 was only the
code default). The cast camera shake (a host-invented default) is removed
(`RC_CAM_SHAKE=1` re-enables for A/B).

Acceptance evidence: `proof/character/3x_motion/README.md` (P2 table) — dash Δz
−960 exact + camera follows positionally; FLWS pos delta 0, walk 330 u/s, jump
rejected, camera yaw/pitch constant through the spin.

**One criterion measured with a named boundary (evidence-first):** criterion 4's
"with no input the camera position does not move" holds exactly in the no-cast idle
baseline (camPos constant ×15 samples), but a standing FLWS cast shows a ±26 u slow
camPos sway during the clip (settling exactly at the clip end; camYaw/camPitch
unaffected). Cause: the host's C1 camera anchor uses the animated head-bone matrix
(`client/RebornClient.cs` ~4777), so the spin clip's head-bone sway feeds the camera
position. Re-open (camera workstream): anchor on the logic position + fixed offset
per `SetCharacterCameraPosition`, or verify the real chain's head-bone use during
spins.

### Reproduce
```powershell
$py = "C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
$s  = "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\spec3x"
# script constants
& $py "C:\Users\Zhibin Ren\Desktop\reborn\tools\netcode\lua51_constants.py" "$s\hd_out\scripts\skill\万花\万花_轻功_震地拔冲.lua" --json "$s\d2_taiyin_constants.json"
# dash primitive xref/disasm (HD copy = sha 95A651D8…)
& $py "C:\Users\Zhibin Ren\Desktop\reborn\tools\netcode\xref_string.py" "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\JX3ClientX64.exe" "KCharacter::LuaDash" --out "$s\d2_xref_luadash.txt" --after 2500
& $py "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\exp16\dump2.py" "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\JX3ClientX64.exe" 0x14030f7c0 0x14030f920
# camera follow table consumer
& $py "C:\Users\Zhibin Ren\Desktop\reborn\tools\netcode\xref_string.py" "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\JX3RepresentX64.dll" "UpdateAnimationEnableCameraFollow" --out "$s\d2_xref_updatecam.txt" --after 4000
# tables (mirror extractor, ref pathlist in $s\pl_d2*.txt)
& "C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\exp16\run\client\bin64\PakV4SfxExtract.exe" "$s\pl_d2cam.txt" "$s\d2_cam"
& $py "$s\d2_camfollow_ids.py"; & $py "$s\d2_mtag_payload.py"
```
