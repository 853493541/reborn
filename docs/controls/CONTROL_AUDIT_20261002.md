# Control-system audit — 2026-10-02 (movement + camera + joystick)

**Build under audit:** `reborn_client_control_modes.exe` @ `e5da30a` (+ lint
fixes in the audit commit). Worktree `reborn-iso-move-controls`, branch
`agent/move-controls`. Scope: camera + movement only (per user); all decode
evidence from the game client (`JX3 1.5.0.9975`, hash in
`proof/controls/PROVENANCE.txt`).

## 1. Gates (must-stay-green)

| Gate | Command | Result |
|---|---|---|
| Netcode model | `tools\netcode\reference\jx3_model.py` | exit 0, **10 PASS** |
| Gravity model | `tools\gravity\verify_model.py` | exit 0, self-consistent |
| Loot selftest | `tools\netcode\loot\capture.py selftest` | **SELFTEST PASS** |
| Camera smoke | `camera_smoke_control_modes.exe` | **ALL PASS** (incl. hotkey context/override checks) |
| Client build | `client\build_client.cmd` | **exit 0**, warning-free |
| Parity / rules / server gates | `gen_parity_vectors.py`, `Reborn.Rules.Selftest`, `Reborn.Server` | **N/A in this worktree** (no `netcode/` tree) |

## 2. Scripted engine runs (audited build)

| Feature | Run | Result |
|---|---|---|
| Default mode | `181057.log` | `operationMode=joystick`, `opmode applied: op=joystick` |
| Joystick instant turn | `180750.log` | A-alone yaw snap `-1.57`, `dist=320`; W+A `383 dyaw=-2.36`; W+D `383 dyaw=1.57`; camera kept (fm=0) |
| Classical regression | `180923.log` | A strafe `-96/96 d=0`; arrows turn `camd=-1.88`; W+A/D `384/385 dcam=0.00` |
| Sprint input | `181358.log` | 150 ms double-tap → `StartSprint`; release → `EndSprint` (engine state open) |
| Follow ALWAYS (joystick) | `173504.log` | A `dcam≈1.06`, TURNRIGHT `camd=1.57`, W+D `dcam=-1.05` |
| Mode persistence | `175629.log` | `userprefs CurrentOperationMode=0 (classical)` read from the real role file |

## 3. Feature matrix vs client truth

- **Movement + camera + base actions: 18/18 handled** (coverage join,
  `hotkey_coverage.txt`); all 286 shipped bindings are loaded/matched.
- **Classic**: A/D strafe (default.txt + decoded `0/76` classical branch);
  arrows turn; S back-pedal; camera mouse-owned except the host turn-key
  coupling (documented host behavior).
- **Joystick**: vector model (`FreeMoveControl 0/16`, A9), auto-face with
  **instant target heading** (`KCharacter::TurnTo 0x14031E7C0` writes
  `[char+0x44]`), mouse-only camera (J2), sprint input (J3, 250 ms), math
  constants decoded (J4, A11), follow-mode gating (J5, A12), reset-speed pipe
  decoded to the node command slot (J6), persistence via
  `userpreferences.jx3dat` (J8), morph contexts N/A in the host (J7).
- **HUD/switch**: top-left `CONTROL: JOYSTICK|CLASSICAL [/] switch`; `/` and
  F7 switch; default joystick.

## 4. Residuals (explicit, no approximations)

1. **A11 rate integration**: the camera-node command-slot reader
   (`+0x24/+0x2C/+0x30`) is the last undecoded hop; the host keeps the
   mode-gated follow (J5) instead of a guessed rate.
2. **Engine `Sprint(true)` state + skill 6754** (J3 engine side): input side
   ported and verified; movement effect open.
3. **Locomotion BinText table**: filename still unidentified (P5 §2b); the
   `0x54`-byte entry table is decoded but not enumerated.
4. **Property/binding registry (G11)**: hashed Lua binding names; not needed
   for the current scope but blocks a full automatic binding audit.
5. **Classical turn-key camera coupling**: host behavior requested by the user
   (turn keys rotate the view); the game client's keyboard never writes the
   camera — documented deviation.
6. **Camera-drift flake watch**: run `165931` showed a non-reproducible drift
   (dcam up to 1.98); all later runs clean.

## 5. Hygiene

- Client build warning-free (unused `mrx/mry/mrz` removed); stale HUD/comment
  text about `/` corrected (`Num/` is the run toggle).
- All controls docs registered in `docs/controls/README.md`; all referenced
  proof files exist; `proof/controls/PROVENANCE.txt` pins the decode source.
- Tree clean apart from the audit commit itself.

**Verdict:** PASS for the camera + movement scope; residuals are listed above
and tracked in `CONTROL_MODES_TRACEABILITY.md` §8 and
`OPERATION_MODES_PLAN.md` §7h.

Last verified: 2026-10-02.
