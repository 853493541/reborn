# Camera tracks / skill-FOV / settings — workstream B plan (item 1.5)

**Date:** 2026-10-05 · **Branch:** `agent/camera-tracks` · **Worktree:** `Desktop\reborn-iso-camera-tracks`
**Scope:** close the closeable 1.5 gaps. Three active tracks:
**(B1)** `.mani`/KRLCameraAni camera tracks — decode + host playback infrastructure;
**(B2)** skill-move camera FOV — table-driven infrastructure (gameplay hook awaits the skill
runtime); **(B3)** minimal camera UI. **Explicitly not in scope** (boundaries, see §6):
automatic mode switching, glider/dynamic-follow modes, edge/saturation post FX.

Companion: `CLIENT_AUDIT.md` (missing list), `CONFIG_FILES.md`, `MODE_ANIM_STATUS.md`
(lives on `agent/item1-completion`, arrives at merge).

## 1. Current state (main @ `8e0352a`)

- Camera core done: drag orbit (real drag model), follow/camera rows
  (character/sprint/carrier/air_combat/npc_dialog/god) with `SwitchMode`, wall
  obstruction, shake, TrackCamera spring, FOV (广角) via `SetViewAngleFactor`,
  real settings read from `config.ini`/`custom.dat` (read-only).
- No automatic mode triggers (WW trigger removed by user decision 2026-09-30); rows reachable
  via `RC_CAM_MODE` only.
- Skill-move camera: **none**. `.mani`/KRLCameraAni: **none** (class `TrackCamera` is a spring,
  not a file track).

## 2. Evidence corpus (grounded 2026-10-05)

| Item | Where | Fact |
|---|---|---|
| `.mani` sample | `zhcn_hd\SeasunDownloaderV2.4\seasun\editortool\movieeditor\source\action\turningeye.mani` (+ `f1/f2/m1/m2_turningeye.mani`) | 768–1008 B; **magic `ACON`**; u32@+4 = **classId** (42 for these editor files), not a count — see `MANI_FORMAT.md` |
| Rush-camera table | viewer tmp `nieyun-rush-out\represent\player\player_rush_camera.txt` (present) | `CameraID / File / Speed`, 4 rows → `\data\movie\camera\16.mani`, `17.mani`, speed 1 |
| Skill-move camera table | `proof/netcode/camera_files/skill_move_camera.txt` (in the original checkout; copied to `proof/camera_tracks/` in P0) | GB18030, 9 lines; columns: `SkillID, bAniTag, 过渡时间(切入)ms, 过渡时间(切出)ms, 广角增幅(弧度 0~2PI)/固定广角(角度 >=30°), 持续时间ms, 屏幕特效开关, 边缘色差(0~10), 色差饱和度(0~1)` |
| Scene/dialog tables | `Represent/camera/SceneCameraAni.tab`, `npc_dialog_scene_camera_ani.txt`, `animation_camera_model.txt` | referenced by `CONFIG_FILES.md`; extract in P0 |
| Managed API | metadata dump | only `KGMovieEditorCLR.ExportCameraTrack/SetCameraTrackPlaySpeedPerMS/SetCameraTrackPlayMethod` (editor playback; **no load-by-name**) — host playback must be our reader |
| Engine key | `NpcDialogCamera` row | `PlaySceneCameraAni` (table key; scene camera animations) |

## 3. Phases

### P0 — corpus + format (static, ~0.5 day) — **DONE 2026-10-06**
1. Corpus: `skill_move_camera.txt` copied to `proof/camera_tracks/` (tracked); 16/17.mani,
   `SceneCameraAni.tab`, `npc_dialog_scene_camera_ani.txt`, `animation_camera_model.txt` and
   10 cameradata samples extracted to the ignored temp dir via `run_pakv4` (provenance in
   `MANI_FORMAT.md`).
2. `tools/camera/mani_probe.py`: full cameradata grammar decoder + sampler, `--selftest`
   (14/14 PASS), `--verify` (10/10 shipped samples, exact payload consumption), `--tsv`
   proof table (`proof/camera_tracks/mani_keys.tsv`).
3. Format spec `docs/camera/MANI_FORMAT.md` + reader citation (KG3DMovieX64.dll:
   validator `0x1238d0`, factory `0x1ba7a0`, header writer `0x123d20`, loader `0x1bb170`;
   `OnPlaySceneCameraAni` @ `0x18031ed00` in JX3RepresentX64.dll).
   **Result:** ACON = 40-byte section headers {magic, classId, 32 zeros}; cameradata =
   class 25 (meta) + class 10 (track A `{x,frame,z,y}` + track B `{x,a,b,frame}`, sparse
   keys, duration = last frame + 1). Rush variant deferred (different key grammar, §6.4).
   Format: `MANI_FORMAT.md` (HIGH).

### P1 — host `.mani` playback (~1 day) — **DONE 2026-10-06**
1. `client/CameraTrack.cs`: ACON decoder + keyframe sampler (`Sample(frame) -> cam/aim`),
   cameradata grammar per `MANI_FORMAT.md` (world space, cm, Y up — resolved in P0;
   track A = position, track B = look-at target, semantics MED). Rush variant rejected with
   a clear log (deferred).
2. `RC_CAM_ANI=<path>[,loop]` (+ `RC_CAM_ANI_FPS`, default 30) drives camera + look-at
   through the existing engine set path; obstruction/shake/terrain-clamp/snapguard are
   bypassed for authored tracks; per-second `camani` log records sampled vs applied.
3. Hook point for the gameplay trigger: `CameraTrack.Play(loop)` / `Update(dt)` — the rush
   trigger still needs the rush/skill-move state (dependency registered, §6.4).
   **Verify:** run `13_0.mani` (log `proof/camera_tracks/p1_run_20261006.txt`): sampled ==
   applied on every logged frame, C# sampler ≡ Python reference (≤5 u at 0.1-frame rows),
   4 distinct screenshot fingerprints, no crash; `camera_smoke` ALL PASS; collision 36/36.

### P2 — skill-move camera FOV (~0.5 day) — **DONE 2026-10-06**
1. `client/SkillMoveCamera.cs`: parses the table (embedded resource copy + `RC_SKILL_MOVE_TABLE`
   override; GB18030 header re-decoded 2026-10-06: value <30 = **FOV increase in rad**,
   ≥30 = fixed FOV in deg), exposes `Get(skillId)` + the effect state machine.
2. `RC_SKILL_MOVE_CAM=<skillId>,<ms>` scripted trigger: FOV angle interpolated over the row's
   过渡时间(切入/切出) with the row's 持续时间 hold, applied through `SetViewAngleFactor`
   (factor = angle / 0.837757). Screen-FX/edge/saturation fields **logged only** (post
   pipeline absent → boundary). The ramp is LINEAR and provisional (client curve open;
   deviation registered in `HOST_DEVIATIONS.md` B16).
3. FLWS has no row — gameplay wiring waits for the skill runtime (dependency registered).
   **Verify:** skill 124841 run (`proof/camera_tracks/p2_run_20261006.txt`): ramp
   60.0 → 64.8 → 69.6 → 75.0 → base exactly matches the linear expectation, effect ends at
   enter+exit, 3 distinct screenshot fingerprints, no crash; gates green.

### P3 — minimal camera UI (~0.5 day) — **DONE 2026-10-06**
`HudOverlay` camera line now shows mode / yaw / distance / FOV deg / obstruction state+len
plus `ani f<frame>/<dur>` and `skillmove s<stage>` while those effects are active; the top
mode line also names the camera row. Host test keys: **F5** row cycle, **F6/F8** base FOV
±5°, **PgUp/PgDn** distance ±100 u; `RC_HUD_LOG=1` logs the HUD text (test). **Read-only**
real settings (no `custom.dat` write); persistence deferred to the settings-UI (system 4),
registered. **Verify:** `proof/camera_tracks/p3_run_20261006.txt` - baseline vs
track+skillmove HUD text (`fov 60deg obst ON` -> `fov 100deg obst off ... ani f174/175
skillmove s1`), layered-buffer dumps 633x237 vs 678x237 distinct hashes.

### P4 — closures (~0.25 day) — **DONE 2026-10-06**
`CLIENT_AUDIT.md` missing-items 4 (track camera → done) and 7 (skill-move FOV → done,
FX logged only) updated with a workstream status note; item 8 annotated (P3 read-only HUD,
write path still deferred). Boundary register updated (§6.4); EXPERIENCES entries for
P0/P1/P2/P3; README index rows (`MANI_FORMAT.md`, `mani_probe.py`) landed in P0.

## 4. Deliverables

- Code: `client/CameraTrack.cs`, `client/SkillMoveCamera.cs`, HUD additions, RC knobs;
  feature build `reborn_client_cameratracks.exe` (title `sandbox-cameratracks`).
- Docs: this plan, `MANI_FORMAT.md` (P0), table decode, status updates + README index.
- Proof: `proof/camera_tracks/` (keys TSV, fingerprints, run logs), copied skill table.

## 5. Verification & gates

`camera_smoke` ALL PASS (feature build), collision selftest 36/36, gravity/`jx3_model`/loot
python gates. Numeric fingerprints for every visual claim. No shared-state writes; the shared
`camera_shim.dll` is **not** touched (if a shim change becomes necessary, stop and report).

## 6. Boundaries (registered, not closable here)

1. **Auto mode switching** — the WW sprint trigger was removed by user decision (2026-09-30);
   real triggers need engine states (sprint/mount/glider/dialog/spectate) that don't exist.
2. **Glider / dynamic-follow modes** — rows absent; need the state systems.
3. **Edge color / saturation screen FX** — needs the post-render pipeline (quality-gated in
   the game); table fields documented, not faked.
4. **Rush/dialog gameplay triggers** — need the SkillMove/Represent state. What landed:
   cameradata `.mani` playback (P1) and the skill-FOV effect (P2) both work through scripted
   triggers (`RC_CAM_ANI` / `RC_SKILL_MOVE_CAM`); the rush `.mani` grammar and the real
   gameplay hooks stay open.
5. **Camera `[Camera]` ini / engine caps** — file absent from the install (compiled defaults).

## 7. Effort

≈2–3.5 agent-days. P0 is pure static; P1 is the main uncertain piece (ACON layout/track
space); P2/P3 are small. No blockers for P0–P3.

## Reproduce

```powershell
$env:RC_CLIENT_EXE='reborn_client_cameratracks.exe'; client\build_client.cmd
.venv\Scripts\python.exe tools\camera\mani_probe.py --selftest                  # 14/14 PASS
.venv\Scripts\python.exe tools\camera\mani_probe.py --tsv proof\camera_tracks\mani_keys.tsv <cameradata .mani...>
# run: cwd = C:\SeasunGame\MovieEditor
$env:RC_CAM_ANI='represent\camera\cameradata\13_0.mani'; $env:RC_AUTORUN='12000'; & bin64\reborn_client_cameratracks.exe
```
