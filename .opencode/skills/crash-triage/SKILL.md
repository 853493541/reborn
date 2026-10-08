---
name: crash-triage
description: Use when the reborn client crashes (log ends abruptly while running, AV) — the D6 signature, stale shared camera_shim detection, driven reproduction with drive_client.ps1, minidump reading, and before/after proof.
---

# Client crash triage

## 1. Confirm it crashed

- Newest log for that exe in `C:\SeasunGame\MovieEditor\bin64\reborn_out\reborn_<ts>.log`.
- A crash = log **ends abruptly while running** (last line is a normal `t=Ns` line, no
  `DONE`). `DONE` = clean exit (often `RC_AUTORUN`).
- Identify the build by the first-line fingerprint `build=<exe> <mtime> git=<hash>`.

## 2. D6 first (the known interactive AV)

Symptom: AV while dragging the camera below the character / looking up; fault at
`KG3DEngineDX11EX64+0x11D03B6` (null `RCPI_Scene` registry entry). Root cause: the
engine's lazy FNV-1 hash was never computed (guard race). Fix: the shim seeds the exact
hash at init (`RC_D6Seed`, default-on, `RC_D6SEED=0` opt-out; `HOST_DEVIATIONS.md` D6).

- Check the run log's `CameraShim: loaded ... d6=`:
  - `d6=seed slot=0x...` → the fix is active; look elsewhere (new crash site).
  - `d6=n/a` → **stale shared shim**: close all clients, `native\build_shim.cmd` from
    main (the script refuses sources without `RC_D6Seed`), relaunch.
- A stale shim is the first suspect after merges: other worktrees may have rebuilt the
  shared `bin64\camera_shim.dll` from an old branch (2026-09-30 incident).

## 3. Reproduce (never hand testing back to the user)

- Driven interactive repro: `tools\camera\drive_client.ps1 -ProcessId <pid> -Seconds 90`
  (synthetic drag-down / look-up + WASD; takes over the mouse/keyboard for the duration).
- Internal, no OS input: launch with `RC_CAM_DEMO=1` (yaw sweep + pitch sweep).
- Verify: client alive at the end, log continuous (fps lines), clean exit — no AV.

## 4. Dumps

- `tools\camera\minidump_exc.py <dump>` (stdlib reader; prints registers incl. r12-r15
  and the fault module+RVA). Dumps live under `zhcn_hd\bin64\minidump` and the engine's
  dump dirs.
- For the D6 site, an `RC_D6DBG=1` run writes a crash-context snapshot
  (`RC_D6DBG_FILE=<path>`): registers, the r12 chain, the registry map/root node.

## 5. Log it

Append an EXPERIENCES entry (full template for crashes): problem, tried, outcome, why
(root cause + evidence), re-open criteria, links. Before/after evidence is required —
"should be fixed" is not an outcome.
