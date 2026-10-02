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

## 3. Probe plan (next executable step)

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
