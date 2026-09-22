# Experience — Spike B: hosting MovieEditor map display (2026-09-21)

**Verdict: this one is a keeper.** A full reverse-engineering + hosting spike
that went from "how does MovieEditor show a map?" to a working standalone app
rendering the real 龙门寻宝 scene in one session. Same recipe as the winning
Spike A, applied to scene loading instead of actor playback.

## What we set out to do

Host the MovieEditor engine DLLs in our own C# process and display a real game
map (龙门寻宝) exactly the way the editor does — no reimplementation, no
retargeting. The actor workstream owns `spike_host.exe`, so this was built as a
**separate app** (`map_spike_host.exe`) on its own branch.

## The shape of the win

- **IL recon first.** Cheap, read-only, decisive. A 200-line F# metadata dumper
  (`tools/dump_map_recon.fsx`, built from the existing `dump_managed_api.fsx`
  helpers) pulled `MainForm::LoadMap` / `SceneForm::LoadScene` IL and revealed
  the ENTIRE load recipe before a single line of host code was written:
  `new KGSceneCLR()` -> `LoadMap(path, async)` -> `SetActiveEnvironment()` ->
  `AddOutputWindow` — no `AttachScene`, no `NewScene`, and the **order is
  mandatory**. That recon is now checked in (`recon_map.txt`) so it never has
  to be redone.
- **Map catalog decoded.** `MapList.tab` (GBK TSV) -> `maplist.py`; the map
  bundle (`.jsonmap` / `.SRScene` / `.rcidx` / `_Setting.ini` /
  `systemCamera.json` / `environment.json`) extracted from the client PakV4
  with the official `PakV4SfxExtract.exe` (`_probe_map_files.py`).
- **First run rendered the map.** 龙门寻宝 loaded in ~1.6 s, loading progress
  1.0 at ~2 s, real terrain + sun sky + 城墙 props from every camera position.
- **Camera system adopted, not invented.** Second recon pass
  (`tools/dump_camera_recon.fsx`, `RECON_CAMERA.md`) dumped the full
  `EXEACTION` + `CAMERA_MOVE_STATE` enums and the ViewWindow handlers, so the
  host now uses the editor's exact scheme: right-drag pan, Alt+right orbit,
  WASD fly via `SetCamareMoveState`, `MOUSE_MOVE(30)` reference, wheel zoom.
  `MAP_MOVETEST=1` proved every action moves the camera in the host.

## The two bugs (and what they taught)

1. **`AddOutputWindow` before `LoadMap` -> winId -1, assert spam,
   AccessViolation on screenshot.** The native scene only exists after LoadMap
   creates it. The editor's own order (views added in `OnAfterLoadMap`) was the
   answer, visible right in the IL. Lesson: when the crash and the fix are both
   in the IL, trust the IL.
2. **"The map didn't render" was a camera problem.** First screenshots were a
   dark-blue/gray blur — the map was fine, the default camera was in a bad
   spot. The `MAP_TOUR` position sweep + pixel-grid analysis of the PNGs
   (can't eyeball images in this environment) found the map's world-coordinate
   content. Lesson: verify visually with a cheap numeric fingerprint
   (per-region RGB grid), don't trust one screenshot.

## What made it fast

- Reused the proven Spike A init chain verbatim (editor root not bin64,
  ref-int returns, GBK paths as C# strings, exe-in-bin64 + cwd-at-editor-root).
- Kept every run automated: env-switch driven (`MAP_AUTORUN`, `MAP_TOUR`,
  `MAP_CAMPOS`, `MAP_MOVETEST`), log file + timestamped PNGs, no guessing.
- All recon artifacts committed (`recon_map.txt`, `recon_camera.txt`) so the
  next spike starts from knowledge, not from re-dumping.

## Hand-off notes for the actor-on-map workstream

- Map load recipe: `engine_host_spike/SPIKE_B_MAP_NOTES.md`.
- Camera/input spec: `engine_host_spike/RECON_CAMERA.md`.
- Actor goes in after `LoadMap` via the Spike A path (`ActorEditorCommandHelper
  .LoadFromFile` -> `scene.AppendModel(handle)` -> `KGModelCLR.PlayAnimation`).
- Spawn point data for 龙门寻宝 lives in the client pak (player spawn /
  systemCamera0 at world coords ~(147463, 5231, 49912)).
- `ROTATE_VIEW(4)` is still a no-op in the host (editor edit-state only).
  `MapList.tab` has 3 rows named 龙门寻宝 (IDs 296/676/677) — verify which is
  the playable one before wiring a picker.

## Costs worth remembering

- The engine init is ~24 s and the editor session is single-user; each test run
  costs ~2 min. Env-switch automation kept that affordable.
- Both host exes must not run concurrently with conflicting engine resources;
  kill stale hosts before rebuilds (`Stop-Process -Name map_spike_host`).
