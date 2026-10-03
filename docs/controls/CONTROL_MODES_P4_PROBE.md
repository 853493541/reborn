# Control modes P4 — field map, host-build diff, probe plan

## 1. Host build diff (MovieEditor `JX3RepresentX64.dll`, 2026-09-14)

Probe targets must use the **host** build's RVAs; same-layout verification:

| Anchor (string assert) | Game client (1.5.0.9975) | Host (09-14) |
|---|---|---|
| `EnableControlCameraOnly` | ~`0x1805E36F0` region | ref `0x1802EE4FA` |
| `CameraAdjustYawWhenMoveTurn` row loader | `0x180338C10` region | ref `0x1803420EC` |
| `UpdateMoveAnimation` | `0x1804C0700` | ref `0x1804D04D3` |
| `GetMoveInfo` | `0x1805DFE90` | refs `0x1805F2A49/A80/AB7` |
| `ApplyRotation` | `0x180B1FA30` | refs `0x180B373B5/FB` |

All anchors exist in both builds (shift ≈ 0xF000–0x1B000) — the probe must read
the host module base from the process and apply the host RVAs, never the game
RVAs. Evidence: `proof/controls/p4/host_build_diff.txt`.

## 2. Camera-manager field writers (game client, static)

Scan of `[reg+0x1AC/0x1B0/0x1A8/0x90..0x9C]` in the camera-manager region
(`0x180B0xxxx–0x180B4xxxx`), evidence `proof/controls/p4/cam_mgr_refs_gc.txt`,
contexts `ac_writers_ctx.txt`:

- **`+0x1B0` is written only as zero**, inside `ResetCharacterCamera`
  (`0x180B09132 [rcx+0x1B0]=edi`, part of the bulk reset at `0x180B09094…`) —
  confirms the earlier "zero-only in normal play" finding.
- **`+0x1AC` writers** (dynamic-follow state machine, not the mouse path):
  - `0x180B1A6ED [rbx+0x1AC]=1` — state transition setter (also writes
    `[rbx+0x1E0/1E4/1E8]` floats and vtable `+0x5D0`; looks like
    enter-force-look/carry state).
  - `0x180B1B3CB [rsi+0x1AC]=r12d` — per-frame applier (writes `[rbx+0x3C]`,
    `[rsi+0x1DC]`, touches `[rsi+0x20]+0xA8`).
  - `0x180B1D31F [rdi+0x1AC]=0` — transition validator (state-mismatch check
    against `[[rsp+0x68]+0x50]`).
  - `0x180B1D3EB [rdi+0x1AC]=…` — flag-gated applier (`and al,0x60` return
    flags; args from `+0x50/+0x54`).
- **Readers** are the mouse pipeline: `ApplyMouse 0x180B1F520` (`0x180B1F6CD`
  `+0x1AC`, `0x180B1F6FF` `+0x1B0`), `UpdateRotation` region
  (`0x180B21A7F/AF2`, `0x180B21DB8`, `0x180B22112`, `0x180B2238D`,
  `0x180B226BA`).
- `+0x1A8` ("camera moved" flag) written at `0x180B1F911 (=1)` and
  `0x180B22162/8F`; accumulators `+0x90..0x9C` reset by `0x180B21587…` (per
  MouseMove frame) and written by the controller resolvers.

**G2/G3 static conclusion:** the LMB/RMB control properties do not write the
manager flags directly; they drive the dynamic-follow **state machine**
functions (`0x180B1A…0x180B1D`), whose writers toggle `+0x1AC`; `+0x1B0` stays
zero in normal play; the mouse pipeline only reads both. Behavioural
per-state mapping needs the probe (below).

## 2b. Probe run result (2026-10-02) — host has no game-world layer

`RC_PROBE_CONTROL=1` implemented and run
(`reborn_20261002_155820.log`): the probe found the loaded module
`C:\SeasunGame\MovieEditor\bin64\JX3RepresentX64.dll` (base `0x7FF8E4BC0000`,
verified via `Get-Process … .Modules`), but the game-world singleton global
`[base+0xF06A50]` is **null for the whole run** (checked every 2 s for 45 s).

Conclusion: the MovieEditor host links the Represent DLL but **does not
instantiate the JX3 game-world layer** (the KTableList singleton at
`0x180F06A50`, the character controller, the animation param table). That
layer is game-client-only.

Consequences:
- Runtime probing of the animation param table / controller intents in the
  host is **not possible**; P5's entry enumeration must come from the shipped
  BinText tables themselves (plus static decode), and P3's per-frame loop
  stays at the static/behavioural level.
- The probe code stays in the client (env-gated, read-only) so the same
  capture can run if a host ever loads the game world; it logs the singleton
  status each sample.
- The F1 catalog (`samples/player/catalog/player_animation_f1.txt`) is the
  per-kind animation table (columns AnimationID, KindID, SheathType,
  AnimationRatio, AnimationSpeed, IsLoop, AnimationFile, ShadowFile,
  是否禁止自动转头, IsLookAtCamera, PoseState, 锁定朝向); the 84-byte
  locomotion param table (two thresholds + three clip pairs) is a **separate
  BinText table** whose filename is still to be identified (next P5 step:
  locate it in the client paks by its column shape).

## 2c. Spring/smoothing integrator candidate (static, 2026-10-02)

`0x180B11E40` (camera-region float math; dump
`proof/controls/p4/spring_fn_180B11E40.txt`) is a spring/interpolation step
that consumes a per-component state:

```
+0x74 active flag;  +0x78 time/speed param;  +0x7C remaining time
+0x80/+0x84/+0x88 velocity components;  +0x8C/+0x90/+0x94 target components
+0x38/+0x3C/+0x40 current components;  +0x5C/+0x60 state
step: v = (target - current) * k / remaining ... ; remaining -= dt
```

The offsets overlap the camera-node per-mode block but reuse fields as
time/velocity, so this is a **spring interpolator helper**, not proof that the
per-mode `springResetSpeed` feeds the camera directly. The link between the
four per-mode setters (`+0x78/+0x90` spring, `+0x7C/+0x94` camera reset) and
this helper is **not established statically** — the remaining consumer decode
needs runtime tracing, which the host cannot do for the JX3 game-world layer
(§2b). Recorded as the best candidate; do not wire a rate from it.

## 2d. Reset-speed pipe + UseFullAngle (static, 2026-10-02)

**Camera reset/spring speed (J6):** the Lua globals are plaintext bindings in
`JX3UIX64.dll` (`Camera_SetResetSpeed`, `Camera_SetSpringResetSpeed`,
`KRepresentScriptTable::LuaCamera_SetResetSpeed`); the engine side is
`KGameWorldHandler::SetCameraResetSpeed` (game client `0x1805FA810`):

```
build action event {0x1E playerId, value, 0x17 (property), float speed}
  -> call [world vtbl +0x7D0]   (the HandleRLAction path, P3 static)
```

So the reset speed is an **action/property event (property id 0x17)** applied
through the same property store as the control ids - the per-frame consumer is
the action handler for that event kind (next decode; bounded).

**UseFullAngle (J6):** not a Represent camera feature - it lives in the logic
layer (`JX3ClientX64.exe` `ProcessFullAnglePlayer` `0x1403AC990`,
`KPlayerClient::DoUseFullAngle`, `bUseFullAngle`, `bFullAngleInAir`):
`ProcessFullAnglePlayer` sets player fields `+0x20038/+0x2003C` (two values)
or clears `+0x20038` when disabled (plus a `+0x3B0` bool variant). It is the
air/轻功 full-angle camera flag, outside the ground joystick scope.

## 2e. Reset-speed consumer chain (static, 2026-10-02) — G5/J6 closed to one hop

```
Camera_SetResetSpeed / SetSpringResetSpeed  (Lua, JX3UIX64 bindings)
 -> KGameWorldHandler::SetCameraResetSpeed  0x1805FA810
      builds a 4-entry field array {playerId 0x1E, selector, prop 0x17, float}
      posts via world vtbl +0x7D0
 -> HandleRLAction 0x1802F54B0
      key = event[0]; handler = lookup table 0x180E96C00 (0..0x2E, 47 entries)
      lookup impl 0x1805D9A70; handler(handler(evt+0x10, count-1))
 -> action table[0x1E] thunk 0x180026A58 -> 0x1802EA9B0
      resolves the player (0x18001C404), dispatches on the selector field
      through the per-type table 0x180E92360
 -> sel[0] = 0x1802EE050
      normalizes the event (rewrites {playerId, selector}) and calls the
      applier thunk 0x18001DB3D -> 0x180AE75C0
 -> applier 0x180AE75C0:
      walks [entity+0x20] to the type-0xD camera node (rbx) and a type-0xE
      companion (rdi); resolves two dwords via 0x18001697D;
      **node[+0x30] = the float value; node[+0x2C] = the property id;
       node[+0x24] = 1 (command slot active)**; companion[+0x2C] = 1;
      optional node[+0x64/+0x68] when [singleton+0x24F2C] is set; then calls
      0x180019A60(entity, ...) for the follow-up.
    sel[1] = 0x1802EE250  (6-field variant)
```

So the per-mode/joystick reset speed is queued in the camera node's **command
slot** (`+0x24` active / `+0x2C` prop / `+0x30` value) and consumed by the
camera update. The only remaining hunt is the reader of that slot (narrow,
well-defined) - then the A11 joystick rate can be ported exactly.

Evidence: `proof/controls/p4/handle_rl_action.txt`,
`rl_handler_lookup.txt`, `rl_lookup_impl.txt`, `reset_speed_handler.txt`,
`reset_speed_sel0_tail.txt`, `reset_speed_applier.txt`.

## 3. Probe plan (superseded in part by 2b; kept for completeness)

Feature build `#iso`? No: probe code is host instrumentation and can live in
the current worktree behind an env gate.

1. **Shim additions** (`native/camera_shim.cpp`): `RC_Probe_ModuleBase(name)`
   (loaded-module base/size) and a guarded `RC_Probe_ReadU32/F32(addr)`; keep
   the shim minimal (read-only, no patches).
2. **Client probe mode** (`client/RebornClient.cs`, `RC_PROBE_CONTROL=1`):
   per-frame log (throttled) of
   - the camera manager: locate via the existing scene/camera chain; dump
     `+0x5C`, `+0x90..0x9C`, `+0x1A8`, `+0x1AC`, `+0x1B0`;
   - the animation vector `[singleton+0x1E2B8]` count `[+0x1E2C0]` and the
     first N 0x54-byte entries (decoded with the P5 field offsets) while
     scripted `RC_DEMO_MOVE` inputs run in both `RC_MODE`s;
   - the character intents (`GetMoveInfo` fields `+0x3C/+0x4C/+0x50`) via the
     existing `EngineRay`/scene proxies if reachable; otherwise via the
     controller pointer discovered from the world handler.
3. **Scripted runs**: `RC_MODE=classical|joystick` + `RC_DEMO_MOVE`; extract
   per-input state tables; verify against the Lua/P3 model; freeze into the
   per-mode specs (P8 proof).

Gate: the probe must change no behavior when enabled (`RC_PROBE_CONTROL` reads
only), smoke ALL PASS, and the run must log `probe=1`.

## Reproduce

```
.venv\Scripts\python.exe <inline scanners>   # proofs under proof/controls/p4
```

Last verified: 2026-10-02.
