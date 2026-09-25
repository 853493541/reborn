# Camera fix suggestions — after the JX3 drag model (commit `733b131`)

**Branch:** `camara-fix` · **Date:** 2026-09-24
**Basis:** `client/RebornClient.cs` + `client/CameraSystem.cs` at `733b131`
(commits `a8471b4` "JX3-exact drag model + host aim closed loop", `733b131`
"spread aim corrections over frames"), plus the uncommitted local tuning in the
worktree (aim sync 60 → **200 ms** while dragging, correction spread
60 ms → **0.2 s**), the run captures `rc_00..rc_03` from the 22:10 build, and
the earlier 21:34 debug log.
**Verdict:** the drag *feel* is now correct — constant-radius sphere orbit, yaw
follow, pitch feed-forward. What is still wrong is **aim (centring) after
anything that changes the distance** (wheel zoom, sprint, `EyeScale`),
**obstruction/ground-clamp edge behaviour**, and the still-missing control
features (max distance cap, RMB turn model, LMB select, 广角/FOV, caps).

---

## 1. Evidence reviewed

| Evidence | Result |
|---|---|
| `rc_00` 3 s, `rc_01` 8 s, `rc_02` 15 s | **identical** — no input during the first 15 s; the camera is stable at rest (no drift/jitter), framing centred, sky visible |
| `rc_03` 30 s (after the player moved) | camera low/close near a building/slope, horizon absent; character only slightly left of centre. Consistent with terrain clamp + a stale aim, but no telemetry to prove it — see §5 |
| 21:34 debug log (pre-`a8471b4`) | `pitch=-0.566` vs measured `vpitch=+0.515`, anchor aim ≈ +0.23 rad → the aim mismatch the two new commits address |
| 22:12 run log | **overwritten, 2 lines only** — the 22:10 run has no camera telemetry. Fix this first (§5) |

## 2. What is working (do not touch)

- `CameraSystem.DesiredOffset` is the proven JX3 sphere offset
  (`docs/CAMERA_DRAG_MODEL.md`); the radius-invariant smoke checks pass.
- Drag mapping: mouse X → yaw, Y → pitch; pitch `+= dy`; clamps, engine orbit as
  the actuator; smoothed yaw correction (`yawCorr`) instead of a snap.
  **Uncommitted tuning:** sync every 200 ms while dragging, correction spread
  over 0.2 s — gentler, but it tracks the engine aim more slowly; keep an eye
  on S1/S4 with this timing.
- Pitch closed loop: `pitchAimErrPx` residual combined into the next orbit
  (`RebornClient.cs:691`, `:741`).
- No engine override of our placed position (`postdbg moved=0.0`).

---

## 3. Remaining defects, with suggested fixes

### S1 [P0] Aim goes stale after wheel zoom / sprint — character drifts off-centre

**Symptom:** after zooming or sprinting (or when a role uses
`fCameraToObjectEyeScale ≠ 1`) the character sits off-centre until the next
drag re-syncs.
**Mechanism:** `pitchAimErrPx` is refreshed only inside the sync block
(`RebornClient.cs:675-692`), which runs while dragging or right after an orbit
(`orbitApplied`). Wheel zoom (`:512`) and the sprint pull-back
(`CameraSystem.cs:446-453`) change `Distance` without triggering a sync, and
the 1 Hz idle block (`:830-844`) refreshes **only** `yawCorr`, not the pitch
residual. `aimPitchOf` is a function of the distance, so the required aim
moves while nothing corrects it.
**Fix:** track the effective distance used for the last aim sync
(`lastAimDist = camSys.Distance * EyeScale`); whenever
`|current − lastAimDist| > ~5 u` (zoom step, sprint, mode switch), force one
pitch sync even when idle. Also set `pitchAimErrPx` in the 1 Hz idle block.
**Acceptance:** wheel 10 steps while idle → character stays centred
(residual < 0.01 rad, no mouse input); same for a 5 s sprint; run a role with
`fCameraToObjectEyeScale=0.9`.

### S2 [P0] `EyeScale` is missing from the aim math

**Mechanism:** placement uses `dist = UpdateDistance(...) * cameraSettings.EyeScale`
(`RebornClient.cs:1033`) but `geometricAimPitch`/`aimPitchOf` use
`camSys.Distance` (`:441`, `:462`). For the roles that ship 0.9 the loop drives
the engine aim to a target computed for the wrong distance.
**Fix:** add one helper `eyeDistance()` (`camSys.Distance * EyeScale`) and use
it in both aim helpers; `DesiredOffset` calls should share the same value.
**Acceptance:** smoke unit test: `aimPitchOf(p)` with EyeScale 0.9 equals the
geometric angle of the placed offset.

### S3 [P1] Ground clamp breaks the orbit line → the closed loop aims at the wrong point

**Mechanism:** the obstruction retract scales the offset (ray preserved), but
the terrain clamp `if (camY < camGround) camY = camGround` (`:1035-1036`) moves
the camera off the ray. `aimPitchOf` still assumes the unclamped geometry, so
the residual controller aims the engine at a point that is not the anchor —
visible as an off-centre character near slopes (likely what `rc_03` shows).
**Fix:** when the ground clamp fires, compute the residual target from the
**effective** camera: `aimEff = atan2(anchor.y − camY, horizontal(anchor→cam))`
and feed *that* to the loop; or set a `terrainClamped` flag and skip pitch
correction while it is active (stable but off-centre beats fighting the clamp).
**Acceptance:** stand against a steep slope: no oscillation/hunting, camera
stable, character within ~0.03 rad of centre.

### S4 [P1] Two pitch controllers can hunt

**Mechanism:** the drag feed-forward `oyFF = −aimDelta/0.00121 − oy`
(`:739-740`) and the measured residual `pitchAimErrPx` (`:741`) both command the
engine pitch; the residual has no deadband and triggers another orbit every
frame until it is zero.
**Fix:** keep the feed-forward for the drag's own aim change, but apply the
residual only when `|pitchAimErrPx| > 2 px`, with a gain of ~0.7, and clear it
after use; log the residual while `RC_CAM_DEBUG=1`.
**Acceptance:** fast drag probe: no ringing; residual converges and stays inside
±2 px at rest.

### S5 [P2] User max distance is enforced only at wheel time

**Mechanism:** `ZoomBy` clamps (`CameraSystem.cs:304-313`), but F11
(`RebornClient.cs:558`), the initial distance, and the sprint target
(`CameraSystem.cs:450`, `base + 60 u`) bypass the cap. `SetMaxDistance` is also
misnamed — it sets `TargetDistance` in metres (`CameraSystem.cs:297`).
**Fix:** add `ClampDistance()` to `CameraSystem` and route every writer
(`ZoomBy`, `SetTargetDistance`, `UpdateDistance` base + sprint targets) through
it; rename `SetMaxDistance` → `SetTargetDistance`.
**Acceptance:** with `fMaxCameraDistance=760`, 50 zoom-outs stop at 760 u, F11
returns to the row distance, sprint cannot exceed the cap (or is documented as
temporary).

### S6 [P2] RMB character turn is an instant snap

**Mechanism:** `curYaw = atan2(−cos(Yaw), −sin(Yaw))` (`:919`) sets the body yaw
in one frame.
**Fix:** rate-limit the turn with the documented yaw turn speed
(`number.krl CharacterYawTurnSpeed=0.007465`; `CharacterYawTurnResetSpeed`
when idle) and add the >112.5° movement-speed penalty when this lands in the
movement model.
**Acceptance:** RMB drag turns the body smoothly, no snap; camera-relative move
direction unchanged.

### S7 [P2] LMB click vs drag not separated

**Mechanism:** any LMB press locks/hides the cursor (`:484-495`); a click
without movement does nothing and again locks the cursor. The real client
selects on click.
**Fix:** track press position/time; on release, if movement < 4 px and
duration < 200 ms treat it as a click (future select/raycast under cursor) and
do not lock; lock only once a drag threshold is crossed.
**Acceptance:** click selects (or is a no-op until targeting exists) without
cursor capture; drag still rotates.

### S8 [P3] 广角 / FOV and engine caps still missing

- Read `config.ini [KG3DENGINE] CammeraAngle` (default 0.837757 rad) read-only,
  map to `SetViewAngleFactor(angle / 0.837757)` until the engine FOV interface
  is exposed (`docs/CAMERA_COMPLETION_PLAN.md` Phase 0); FOV must not move the
  camera.
- Probe the engine caps (`+0x50/+0x58/+0x60/+0x68`) to replace the 100 u
  `MinCameraDistance` guess (`CameraSettings.cs:12`).
- Full spec: `docs/CAMERA_DISTANCE_FOV_SPEC.md` (this worktree).

---

## 4. Suggested order

| Priority | Item | Why |
|---|---|---|
| 1 | S1 + S2 | most visible: off-centre after zoom/sprint; two-line helper change |
| 2 | S3 + S4 | stability near terrain and no controller hunting |
| 3 | S5 | correctness of the user setting (镜头最大距离) |
| 4 | S6 + S7 | control fidelity (turn model, click-select) |
| 5 | S8 | 广角 + caps |

## 5. Verification hygiene (do this with S1)

- The 22:12 run overwrote `reborn.log` with 2 lines, so the 22:10 run's camera
  telemetry is lost. Write per-run logs (e.g. `reborn_<HHmmss>.log` or a
  `RC_LOG` env) and keep `RC_CAM_DEBUG=1` for camera runs.
- Add `camdbg`-style one-line telemetry to the screenshots runs:
  `dist / effDist(EyeScale) / model pitch / measured aim / residual / clamped`.
- New smoke cases (pure math, no engine): `ClampDistance`; `aimPitchOf` with
  `EyeScale`; ground-clamp residual target.
- Keep the `rc_00..rc_03` capture set as the visual regression baseline; add a
  zoom/sprint scripted variant so S1 is covered by a run, not only by a manual
  test.

## 6. Cross-references

| File | Content |
|---|---|
| `docs/CAMERA_DRAG_MODEL.md` | proven mouse → yaw/pitch mapping |
| `docs/CAMERA_FIX_SPEC.md` | Part 1 (drag fix) proof and acceptance |
| `docs/CAMERA_CLIENT_AUDIT.md` | implemented / partial / missing inventory |
| `docs/CAMERA_COMPLETION_PLAN.md` | Phase 0–6 (native interface, obstruction, modes, settings) |
| `docs/CAMERA_DISTANCE_FOV_SPEC.md` | 镜头最大距离 + 广角 implementation spec |
| `docs/PLAYER_CONTROLS_FINDINGS.md` | the full control surface this camera serves |
