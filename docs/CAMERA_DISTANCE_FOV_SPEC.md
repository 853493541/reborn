# Camera settings spec — 镜头最大距离 (max distance) + 广角 (FOV)

**Status:** implementation spec, no code in this branch.
**Decision (user):** mirror JX3 scopes, **read-only for now**.
**Base code:** `camara-fix` @ `00f1237`.

---

## 0. Recovered facts

### 镜头最大距离 (`fMaxCameraDistance`)
- Scope: **per role** — `userdata/<account>/<region>/<server>/<role>/custom.dat`
  → `VideoSettingPanel.tCameraStatic.fMaxCameraDistance`.
- Default **2000 u = 20 m**; user-modified roles 760 / 1125 / 1245.
- Applied via `SetCameraMaxDistance` → camera node classic `+0x74` / joystick
  `+0x8C`, clamp **[1, 2000]**.
- Effects: caps wheel zoom-out; scales the non-wheel zoom step
  `clamp(current/(0.2·max)·120, 10, 120)` u; does not set the current distance.
- Not the row `number.krl CameraMaxDistance = 1245`, not sprint pull-back, not
  `config.ini CammeraDistance = 80000` (engine far plane).

### 广角 (`fCameraAngle`)
- Scope: **per install** — `config.ini` `[KG3DENGINE] CammeraAngle = 0.837757`
  rad ≈ 48° (key misspelled in the ini), written by the video panel
  (`video_base.lua` → `[UIVideoSetting]` + `KG3DEngine.Set3DEngineOption`).
- Projection only: must not move the camera or change `Distance`.
- Caps: engine `fMinCameraAngle`/`fMaxCameraAngle` (compiled per graphics level).
- Nameplates compensate (`TitleAdjustFovMin/Max/Value = 0.7/1.4/40`);
  skill/mode cameras override FOV temporarily (`skill_move_camera.txt`).
- Engine `[Camera] fFovy` ini is not shipped — host stand-in needed until then.

### Current code
- `CameraSettings.cs` reads 3 `custom.dat` keys; `MinCameraDistance = 100` is a
  guess; no `config.ini` read; `ZoomBy` is the only distance clamp;
  `SetMaxDistance` sets `TargetDistance` in metres (misnamed).

## 1. Phase 0 — recon (read-only)
1. Capture the editor's default `scene.GetViewAngleFactor()` (already logged).
2. Check whether `JX3RepresentX64.dll`/`JX3UIX64.dll` are loaded in the client
   process (both exist in `MovieEditor\bin64`) — decides the caps probe path.
3. Check what `MovieEngineCLR` exposes for camera FOV (`IKG3D_Camera`,
   `KG3D_CAMERA_OPTION_PROXY`, `Suspend/RestoreCameraPerspective`).

## 2. Phase 1 — distance cap unification
- Add one enforcement point in `CameraSystem`: `ClampDistance(u)` over
  `[MinCameraDistance, MaxCameraDistance]`, and `SetDistance(u)` that clamps
  and writes both `Distance` and the row `TargetDistance` (`u / UnitsPerMeter`).
- Route **all** writers through it: `ZoomBy`, init, F11
  (`SetMaxDistance` → rename `SetTargetDistance`), `UpdateDistance` base and
  sprint targets (`base + 60 u`).
- `RebornClient.cs` call sites: init (`:202`), wheel (`:512`), F11 (`:558`),
  loop (`:1042`).
- Acceptance: user max 760 → 50 zoom-outs stop at 760 u; F11 returns to the row
  distance and is clamped; sprint cannot exceed the max (or is documented as a
  temporary pull-back).

## 3. Phase 2 — 广角 load + apply
- New `client/VideoSettings.cs`:
  - `Parse(string)` pure function + `Load(workingDir)`:
    `config.ini [KG3DENGINE] CammeraAngle` (default 0.837757; also
    `CammeraDistance` parsed but ignored), `[UIVideoSetting]` dictionary;
    fallback `config/config.default.ini`.
- Apply through a thin abstraction `ICameraLens`:
  - interim: `factor = CammeraAngle / 0.837757` → `scene.SetViewAngleFactor(factor)`;
  - later: real FOV via the engine camera (Phase 3 / `CAMERA_COMPLETION_PLAN.md`
    Phase 0), then delete the factor mapping.
- `RC_VIEW_ANGLE` stays a test override. Log
  `fov source=config.ini angle=.. factor=..`.
- Hard rule: changing FOV never moves the camera or changes `Distance`.
- `build_client.cmd`: add `client\VideoSettings.cs` to both csc lines.

## 4. Phase 3 — engine caps probe (optional spike)
- `[[JX3RepresentX64.dll+0x180f06a50]+0xB0] → vt+0x238` → caps object;
  `+0x50` min distance, `+0x58` max distance, `+0x60` min angle, `+0x68` max angle.
- Gate on Phase 0.2. Fallbacks: distance [100, 2000], angles unbounded.
- If unreachable: record as **BLOCKED**, do not invent values.

## 5. Phase 4 — defaults/docs
- Keep `MinCameraDistance`/`MaxCameraDistance` in the character row (user
  override at load). Note the mixed units (`TargetDistance` metres vs
  min/max world units) in the row docs.
- Update `CAMERA_CLIENT_AUDIT.md`, `CAMERA_COMPLETION_PLAN.md` Phase 4 and the
  gap register (S5, S8).

## 6. Phase 5 — verification
Smoke (pure):
1. `ClampDistance` with max 760; `SetDistance(900)` → 760; F11 obeys.
2. `UpdateDistance` base target clamp; sprint documented.
3. `VideoSettings.Parse`: `0.837757` → factor 1.0; `0.35` → ≈0.4178;
   malformed → default; no file writes.

Live:
- `RC_LOAD_CUSTOM_DAT=1` on a role with 760/1125/1245 → log caps, wheel obeys.
- FOV default vs modified: apparent size changes, camera position identical
  (screenshots + camdbg).

## 7. Risks / open
- Editor `ViewAngleFactor` ↔ real `fCameraAngle` relation is unverified
  (documented host stand-in).
- Caps probe may be unreachable in the editor host.
- Read-only decision means the settings panels later need a write path design.
