# client — agent notes

Desktop game client (`reborn_client.exe`) hosting the MovieEditor engine DLLs.

- Build from the repo root: `client\build_client.cmd`. It writes `reborn_client.exe`
  and `camera_smoke.exe` into `C:\SeasunGame\MovieEditor\bin64` and copies
  `camera.json` + `scene_init_param.txt`. That is the only allowed write location.
- Do not write other files into the `bin64` root — it is shared with other apps
  (`ability_*`, `asset_sandbox`, engine hosts). Use an isolated output subdir.
- **C# 5 only** (built with `Framework64\v4.0.30319\csc.exe`): no `$"..."`, `?.`,
  `nameof`, expression-bodied members.
- All engine-hosting executables must run with working dir `C:\SeasunGame\MovieEditor`.
- Camera settings read the player's `userdata\...\custom.dat` read-only; never write it.
- Verify: run `camera_smoke.exe`; visual checks need a numeric fingerprint, not one
  screenshot. Root `AGENTS.md` rules apply.
