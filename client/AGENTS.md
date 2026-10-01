# client — agent notes

Desktop game client (`reborn_client.exe`) hosting the MovieEditor engine DLLs.

- Build from the repo root: `client\build_client.cmd`. It writes `reborn_client.exe`
  and `camera_smoke.exe` into `C:\SeasunGame\MovieEditor\bin64` and copies
  `camera.json` + `scene_init_param.txt`. That is the only allowed write location.
- Parallel feature builds: `set RC_CLIENT_EXE=reborn_client_<slug>.exe` before
  `client\build_client.cmd` (`RC_SMOKE_EXE` for the smoke exe; skipped otherwise).
  With it set, the script skips the shared `camera.json`/`scene_init_param.txt` copies
  and writes `build_info_<exe>.txt`; the canonical `reborn_client.exe` stays untouched
  until the feature merges.
- Concurrent runs are supported: the client derives its engine memory namespace from
  its exe name (`reborn_client_<slug>.exe` -> `reborn_client_<slug>.memory`; canonical
  keeps `MovieEditor.memory`; `RC_MEM_NS` overrides). The instance guard blocks only
  namespace peers, so different feature builds run side by side. The engine root stays
  shared (root isolation broke init — `d8268d2`); ShaderListUpload/dxvk writes are the
  known shared-write caveat.
- Feature builds title their window `sandbox-<slug>` (derived from
  `reborn_client_<slug>.exe`); the canonical build keeps `JX3`. **Any client update for
  a feature must set a matching title** (`RC_TITLE=<feature>` for one-off runs) — never
  run a feature build under a generic title; identical titles across client windows are
  a rule violation (root `AGENTS.md` §2.7).
- If a start is blocked by the instance guard (namespace conflict), the response must
  end by naming the conflicting session: process name, PID, start time and namespace —
  so the user knows which window/session to close (root `AGENTS.md` §2.4). Blocked
  starts are also recorded in `bin64\reborn_out\guard_block.txt` (timestamp,
  blocker pid/start/window) and the dialog title names the build
  (`<exe>: start blocked`), so a missed dialog is diagnosable.
- A blocked start is NOT a startup failure: single instance per build namespace is by
  design; press the guard dialog away and close the named window.
- Do not write other files into the `bin64` root — it is shared with other apps
  (`ability_*`, `asset_sandbox`, engine hosts). Use an isolated output subdir.
- **C# 5 only** (built with `Framework64\v4.0.30319\csc.exe`): no `$"..."`, `?.`,
  `nameof`, expression-bodied members.
- All engine-hosting executables must run with working dir `C:\SeasunGame\MovieEditor`.
- Camera settings read the player's `userdata\...\custom.dat` read-only; never write it.
- Verify: run `camera_smoke.exe`; visual checks need a numeric fingerprint, not one
  screenshot. Root `AGENTS.md` rules apply.
