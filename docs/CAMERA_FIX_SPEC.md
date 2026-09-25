# Camera drag/placement fix — exact spec (2026-09-24)

Companion to `docs/CAMERA_COMPLETION_PLAN.md` Part 1. This file records what
is **proven** from the recorded disassembly, what is **inferred**, and the
exact code change plus the short probe that settles the remaining
conventions.

## 1. What is proven (disassembly evidence)

From `JX3RepresentX64.dll SetCharacterCameraPosition` (`0x180B0E820`,
`proof/netcode/disasm/camera_set.txt`):

- The trig wrappers: `0x180BB83B5` = **cosf**, `0x180BB83BB` = **sinf**
  (both `jmp` to `api-ms-win-crt-math-l1-1-0`).
- The desired offset block (`0x180B0F1EE`..`0x180B0F2A8`) builds a rotated
  offset from the controller's `A/B/C` (`+0xC8`/`+0xCC`/`+0xD0`) and the
  caller's angle params (`rbp+0x20`, `rbp+0x24`):
  - X = `A * cosf(p1) * sinf(p2) + B * sinf(p2 - k)`
  - Y = `A * sinf(p1) + C`
  - Z = `A * cosf(p1) * cosf(p2) ± B * (...)`
  i.e. **the pitch multiplies both the horizontal plane (`cos`) and the
  vertical (`sin`) of the same distance `A`, and `C` is added to Y only.**
  The `A` term is a rotation of a constant-length vector; there is **no
  `tan`, and `C` is not scaled by distance**.
- `C` = `[r14+0xD0]` is a separate constant term (the row height).
- After the rotation, the offset is smoothed per axis with the dead-zone
  rule (`0x180B0F2BA`..`0x180B0F3A6`, state `+0x1B8/0x1BC/0x1C0`), and the
  **final camera = anchor + smoothed offset** (`0x180B0F3AF`..`0x180B0F435`).
- The mouse path in the represent layer only updates controller yaw/pitch
  (`ApplyMouse`/`ClampMouse`, `camera_input_controls`); nothing writes a
  camera position from the mouse.

## 2. What is inferred (needs one probe)

- Which of the caller params (`+0x20` vs `+0x24`) is yaw vs pitch, and the
  sign relation to our `camSys.Yaw/camSys.Pitch` (our yaw is derived from the
  engine's measured view direction, so it should already be in the engine's
  convention, but this is not proven byte-for-byte).
- The exact `B` lateral term and the `k` constant (irrelevant in this fix:
  `B` is 0 in the host rows).

Neither affects the fix structure: our placement already uses the same
horizontal yaw convention as the engine (`Forward()` = view direction), and
the fix only adds the `cos(pitch)` factor and corrects the vertical term.

## 3. The defect in our code

`client/RebornClient.cs` (placement block, ~914-955; also
`engine_host_spike/MapSpike.cs:1162-1190`):

```csharp
double pitchOffset = Math.Tan(camSys.Pitch);
double camX = ax2 - vx * dist;              // horizontal radius = dist
double camY = ay2 - pitchOffset * dist;     // vertical = tan(pitch) * dist
double camZ = az2 - vz * dist;
```

The offset length is `dist / cos(pitch)` — it **grows as the camera
pitches**, so vertical drag changes the camera's distance from the anchor
and the horizontal offset never shortens with pitch. The startup pitch
`-atan2(CameraHeight, dist)` hid this by folding `C` into `tan`.

## 4. The fix (exact change)

Replace the placement (and the obstruction retract) with the constant-length
rotation, keeping the existing smoothing and obstruction ordering:

```csharp
double camHeight = camSys.Row.F("CameraHeight", 2.0) * camSys.UnitsPerMeter;
double cp = Math.Cos(camSys.Pitch);
double sp = Math.Sin(camSys.Pitch);

// desired offset from the anchor (A rotation + C on Y only)
double[] desired = { -vx * cp * dist,  sp * dist + camHeight,  -vz * cp * dist };

// existing per-axis dead-zone smoothing on camOffset (unchanged)
...

// obstruction: move along the anchor->camera ray by scaling the offset
if (hit) {
    double s = d2 / Math.Max(1e-6, dist);
    camX = ax2 + camOffset[0] * s;
    camY = ay2 + camOffset[1] * s;
    camZ = az2 + camOffset[2] * s;
}
scene.SetCameraPos((float)camX, (float)camY, (float)camZ, false);
```

Also:

1. **Delete the height-in-pitch calibration** (`RebornClient.cs:588-604`):
   do not set `alignPitch = -atan2(CameraHeight, distance)`. Keep the
   saved/row pitch; `CameraHeight` stays an additive term.
2. **Remove the yaw-from-position feedback** in the normal loop
   (`RebornClient.cs:722-730`): never assign `camSys.Yaw` from
   `measureView()` while running; keep it as a debug-only diagnostic. Drag
   integrates yaw/pitch from the deltas (with the row clamps).
3. Same change in `engine_host_spike/MapSpike.cs` so both hosts agree.

### Expected look change (one-time retune)

At the defaults (`dist = 600 u`, `pitch = -0.35`, `CameraHeight = 200 u`) the
camera Y becomes `anchor.y + sin(-0.35)*600 + 200 = anchor.y - 6 u` instead of
today's `anchor.y + 219 u`. That is the real geometry; the anchor is the head
in the real client, while ours is chest+90 u — Phase 1 of the plan switches
the anchor and re-tunes `CameraHeight` from the (missing) row data. Until
then expect the camera to sit lower/closer to the real game.

## 5. Acceptance tests

1. **Radius invariant** (new `CameraSmoke` case): with fixed `dist`, sweep
   `pitch` over `{-1.2, -0.8, -0.35, 0, +0.8}` and assert
   `|camera - anchor - (0, CameraHeight, 0)| == dist` within `1e-3` u.
2. **Drag invariant**: apply delta pairs; assert `dist` and `camHeight`
   unchanged and only yaw/pitch changed; the camera-to-anchor distance is
   constant.
3. `camera_smoke.exe` stays 13/13.
4. One `RC_SHOTS` capture: camera behind the character at every tested pitch,
   no slide or fly-away after a drag.

## 6. The probe that settles section 2 (2 minutes, no live game needed)

With `RC_CAM_DEBUG=1`:

1. Start at rest; log `camSys.Yaw/Pitch` and one `measureView()` result.
2. Drag **up** by a fixed amount (`oy < 0`) and log both again.
3. Expected per the engine: pitch moves toward positive/negative depending on
   the sign convention; the *measured* view pitch must move the same way as
   `camSys.Pitch` (the placement keeps the aim on the anchor). If it moves
   the other way, negate the `sp` term (and the pitch integration sign) in
   one place.
4. Drag **right** (`ox > 0`) and verify the camera orbits around the anchor
   with the measured yaw following `camSys.Yaw`; if it mirrors, negate the
   `cp` horizontal terms.

Record the two outcomes in this file when done.

## 7. Not covered here

Native obstruction (walls/models, 18 u clearance, hysteresis, flex), mode
behaviour, settings and extras are the plan's Phases 0-6 — see
`docs/CAMERA_COMPLETION_PLAN.md`.
