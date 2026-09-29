# JX3 camera drag model - proven from the client binaries (2026-09-24)

Answers "what does left/right drag do vs up/down drag, exactly?" with the real
client code, not with assumptions. Build: `JX3RepresentX64.dll` from the host
(`C:\SeasunGame\MovieEditor\bin64`, image base `0x180000000`). Constants are
from this build; addresses of the original research build can differ.

**Result (corrects the earlier assumption):**

| Input | Real JX3 behaviour |
|---|---|
| drag left/right | **yaw**: the camera position **orbits around the character**; the character stays in the centre (the look target is the anchor) |
| drag up/down | **pitch**: the camera rises/lowers and the view tilts up/down; the character still stays in the centre |

So the camera **is** a satellite: its position changes on a sphere; it is never
"rotate in place". The offset length is constant; only the angles change.

## 1. Input chain: mouse delta -> yaw/pitch (disasm proof)

`ApplyMouse` @ `0x180B36E20` (function containing the `ApplyMouse` assert at
`0x180B36F11`; raw dump `proof/netcode/disasm/mouse_drag_update.txt`):

1. `0x180B36FDD` / `0x180B36FE5`: both mouse deltas are scaled by
   `[0x180CB9F34]` = **6.2831855 (2*pi)**:
   `dx *= 2pi`, `dy *= 2pi` (stored at `[rsp+0x54]`, `[rsp+0x50]`).
2. `0x180B36FFF`..`0x180B37047`: normal character path picks the live camera
   controller (`r15`) and calls `ClampMouse` @ `0x180B37770` with the current
   angles `xmm3 = [r15+0x20]`, `xmm6 = [r15+0x24]` plus the scaled deltas
   (per-mode clamps: sprite pace `0x180B37133` branch, glider/npc-dialog
   variants asserted in `ClampMouse`).
3. `0x180B3707F`: `xmm6 = [r15+0x24] + dy*2pi`; `0x180B37085`..`0x180B37092`
   clamps it into `[obj+0xA4] .. [obj+0xA8]`; `0x180B370AD` turns that into a
   clamped pitch delta.
4. `0x180B370B3`..`0x180B370BF`: applies
   `setter(controller, xmm1 = dx*2pi, xmm2 = clampedPitchDelta, 0)`.

The setter `0x180AE29D0` (jump-table entry `0x18000327E` ->
`0x180AE29D0`, dump `proof/netcode/disasm/camera_set_tail.txt`):

- `0x180AE2A4C`..`0x180AE2A79`: yaw delta wrapped by `2pi`
  (`[0x180CC8864]` = -6.2831855).
- `0x180AE2AF6`..`0x180AE2B26`: pitch clamped by
  `[0x180CAD584]` = **-1.5550884** and `[0x180CAD538]` = **+1.5550884**
  (= `+/-(pi/2 - 0.0157)`, i.e. `|pitch| < pi/2`) before the rate/spring
  `0x180AE3000`.
- The stored fields are `[controller+0x20]` (yaw) and `[controller+0x24]`
  (pitch); see also `0x180B3700C` / `0x180B37017`.

Conclusion: **mouse X -> yaw (`+0x20`), mouse Y -> pitch (`+0x24`)**. Both are
the angles of the *camera offset around the character*; nothing in the mouse
path writes a camera position, distance or look direction.

Direction: the pitch is `pitch += dy` (target then clamped), and the cursor
delta comes from `ComputeMouseMoveDelta` @ `0x180B381B0` (cursor-lock path
stores previous minus current). Dragging **down** therefore raises the camera
offset (`offset.y = sin(pitch)*d + height` grows) - the camera looks further
**down** at the character. Dragging **up** lowers it and looks up. This is the
same direction as the host engine's native orbit, confirmed in the captures
(`reborn_out/rc_01_8000ms.png` top-down vs `rc_02_15000ms.png` level).

## 2. Placement chain: anchor + rotated offset, look at the anchor

`SetCharacterCameraPosition` @ `0x180B0E820`
(`proof/netcode/disasm/camera_set.txt`):

1. **Anchor** (`0x180B0EE17`..`0x180B0EFDC`): mount socket (`+0x3A08`),
   custom offsets (`+0xCF8` / `+0x3CA8`), character head/entity
   (`+0x3B9C`, `+0x38`), else the raw character position. The height helper
   `AdjustCharacterCameraObjectY` @ `0x180AC8980` resolves **`Bip01 Head`**
   (`0x180AC8C3D`) - the anchor is the character, normally the head/socket.
2. **Offset** = constant-length rotation of `(distance A, lateral B, height C)`
   by `(yaw, pitch)` (`CameraSystem.DesiredOffset` matches this; verified form
   in `docs/netcode/JX3_CAMERA_RESEARCH.md` section 6):
   `off = (cos(yaw)cos(pitch)A, sin(pitch)A + C, sin(yaw)cos(pitch)A)`.
3. Smoothed per axis (dead zone + `SmoothTime`), final
   **camera = anchor + off**.
4. The engine camera is set with **look-at params** (research section 6.3; the
   follow-action branch resolves the `s_face` target as the look-at,
   `0x180B0F152`..`0x180B0F187`).

So the camera always looks at the anchor. That is why the character stays in
the centre during both drags, and why "left/right drag" visibly moves the
camera position (it orbits) instead of rotating in place.

## 3. What our probe showed

`bin64\reborn_out\reborn.log`, `RC_CAM_DEBUG=1`:

- Before the closed loop, our tracked yaw diverged from the engine's measured
  yaw by up to **4.36 rad** during drags (`yaw=-1.57` vs `vyaw=-2.69`): the
  camera was placed at a different orbit angle than where the engine was
  aiming - that is the "drag changes the camera position wrongly" symptom.
- With the closed loop, tracked yaw equals the engine yaw to 3 decimals
  (`yaw=1.344 vyaw=1.344`) and the measured radius matches the sphere formula
  (`r=593.8` measured vs `593.7` computed at `pitch=-0.199`, `dist=600`,
  `CameraHeight=200`).
- Pitch aim: `pitch=-0.199` model vs `vpitch=-0.165` measured. The exact
  geometric aim would be `-atan2(sin(p)*600+200, cos(p)*600) = -0.138`; the
  remaining 1.5 deg is the anchor convention (our anchor is chest+90 u, the
  real anchor is the head/socket).

## 4. Consequences for our client (implemented, final)

- `client/CameraSystem.cs CameraSystem.DesiredOffset` matches step 2 - done.
- Left/right drag = yaw orbit (`Yaw -= ox * 0.0018` while dragging).
- Up/down drag = pitch (`Pitch += oy * 0.00121`) - same direction as JX3.
- Speed independence: in JX3 centring cannot lag the drag - one state
  `(yaw, pitch)` is written by the mouse and **both** the position and the
  look-at are derived from it in the same frame (`SetCharacterCameraPosition`
  computes `anchor + offset` and sets the look-at to the anchor). There is no
  separate aim tracker. Our host splits the two (the engine owns the look), so
  `RebornClient.cs` closes the loop **every frame while dragging**:
  - `measureView()` reads the engine view back; the yaw difference is stored
    in `yawCorr` and applied through a 10 ms low-pass
    (`1 - exp(-dt/0.01)`, i.e. lag ~= omega * 0.01 s) - fast enough that the
    character stays centred at any drag speed, smooth enough to filter the
    nudge noise.
  - the pitch error `measured - aimPitchOf(P)` is drained with the same
    low-pass into `oyFF`; `oyFF` also compensates the raw-orbit aim difference
    of the drag itself.
  - a single `ROTATE_CAMERA` per frame sends `ox` plus the pitch part (two
    `ExecAction(30,...)` starts in one frame would drop the first delta).
  - `alignAim()` seeds the aim at startup and on F11/Home/End.
- Probe (fast yaw sweep ~1.5 rad/s, `RC_CAM_DEBUG=1`):
  `max |wrap(yaw-vyaw)| = 0.002 rad` (mean 0.0001); pitch aim `max 0.013`,
  avg 0.001; radius matches `|anchor+offset|`. Captures show the character
  centred in both sweeps.
- The real anchor is the head/socket (`Bip01 Head`), not chest+90 u; plan
  Phase 1 switches the anchor and takes `CameraHeight` from the real row data.

## 5. Evidence files

| File | Content |
|---|---|
| `proof/netcode/disasm/mouse_drag_update.txt` | `ApplyMouse` (deltas * 2pi) + `ClampMouse` prologue/controller dispatch |
| `proof/netcode/disasm/mouse_drag_clamp.txt` | `ClampMouse` per-mode clamps + `0x180AE29D0` setter jump |
| `proof/netcode/disasm/camera_set_tail.txt` | anchor/params reads + the yaw/pitch setter `0x180AE29D0` (pitch clamps) |
| `proof/netcode/disasm/camera_adjust.txt`, `camera_set.txt` | slope adjustment + placement function |
| `docs/netcode/JX3_CAMERA_RESEARCH.md` sections 2, 6 | offset formula, smoothing, look-at, row vocabulary |
| `docs/camera/INPUT_CONTROLS.md` | LMB/RMB/wheel bindings + UI layer |
