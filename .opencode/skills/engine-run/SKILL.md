---
name: engine-run
description: Use when starting, restarting, or testing the reborn game client or any feature client build (reborn_client*.exe), or when a client start is blocked by the single-instance guard. Covers build naming, engine memory namespaces, window titles, the shared camera_shim (D6 seed), launch env, the ~24 s engine init, and reading reborn_out logs.
---

# Engine run loop (reborn client)

Repo: `C:\Users\Zhibin Ren\Desktop\reborn`. Client sources in `client/`; builds write to
`C:\SeasunGame\MovieEditor\bin64` (the only allowed write location under `C:\SeasunGame`).
Root `AGENTS.md` rules apply; engine runs are **namespace-exclusive, not machine-exclusive**.

## 1. Build

- Canonical: `client\build_client.cmd` (from the repo root) → `bin64\reborn_client.exe` +
  `bin64\camera_smoke.exe`; copies shared `camera.json` / `scene_init_param.txt`.
- Feature build (parallel work, AGENTS §2): `set RC_CLIENT_EXE=reborn_client_<slug>.exe`
  **before** the script. It skips the shared config copies, writes
  `build_info_<exe>.txt`, and skips the smoke build unless `RC_SMOKE_EXE` is set.
- Never overwrite the canonical `reborn_client.exe` while a feature is in flight.

## 2. Pre-flight (shared shim)

The bin64 shim is shared by every client. Every run must load one with the D6 fix:

- Log line `CameraShim: loaded ... d6=seed slot=0x...` = good; `d6=n/a` = stale shim.
- Or `dumpbin /exports bin64\camera_shim.dll | findstr RC_D6Seed`.
- If stale: close all clients (the DLL locks), then `native\build_shim.cmd` from a
  worktree current with main (the script refuses sources without `RC_D6Seed`).

## 3. Namespace + guard (AGENTS §2.4)

- Canonical `reborn_client.exe` → `MovieEditor.memory`; feature
  `reborn_client_<slug>.exe` → `reborn_client_<slug>.memory`; `RC_MEM_NS` overrides.
- The guard blocks only namespace peers. If a start is blocked, the response **must end
  by naming the conflicting session** — process name, PID, start time.
- Never run two clients in the same namespace; different namespaces are the point.

## 4. Launch

- Working dir is always `C:\SeasunGame\MovieEditor` (required for engine config/VFS).
- Title: feature builds auto-derive `sandbox-<slug>`; `RC_TITLE=<feature>` overrides.
  Never leave a feature build with a generic title (AGENTS §2.7).
- Useful env: `RC_MAP=<vfs path or absolute sandbox path>`, `RC_AUTORUN=<ms>` (auto-exit),
  `RC_SHOTS=8000,15000` (screenshots), `RC_CAM_*` knobs, `RC_D6SEED=0` (A/B the seed).

## 5. Wait + read the log

- Engine init ~24 s; a run is ~2 min. Wait for the `spawn=` line before judging.
- Logs: `bin64\reborn_out\reborn_<ts>.log`. Identify the right one by the first-line
  fingerprint `build=<exe> <mtime> git=<hash> dirty=<n>` and the `ns=` init line.
- Key lines: `InitPath/InitMemory/InitPak ... ns=...`, `Init3DEngine=1`, `LoadMap=`,
  `TerrainSampler: size=... regions=...`, `spawn=`, `CameraShim: loaded ... d6=`.
- Sandbox runs: `regions=1x1 origin=(0,0)` (see `sandbox-map` skill).

## 6. Verify + stop

- Screenshots: `tools\proof\image_stats.py <img> [--grid 8x8]` (size/hash/region RGB).
  **Never Read image files** — the API caps images per request and long sessions die.
- Stop with `Stop-Process -Name <exe>`; kill stale hosts before shim rebuilds.

## 7. Report

Build id (git hash), exe + title, namespace, log path, and the key log lines
(`spawn=`, `d6=`, terrain). A `Verified:` line per AGENTS §15.
