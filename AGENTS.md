# JX3 Reborn — agent rules

Read this first. Keep it short; depth lives in the linked docs.

## 1. What this repo is

JX3 "Reborn": research plus desktop prototypes that recreate JX3 runtime systems
(engine host, netcode, controls, camera, collision, mode UI) using the real game
engine DLLs and original game data. The product is a desktop client — not a website.
Game assets are private and are not in the repo.

## 2. Workflow modes

### Default mode

Work directly in the current checkout on the current branch, as normal.

- Never commit or push to `main` unless the user explicitly asks.
- Never commit generated/binary artifacts (`*.bin`, `*.pss`, `*.t2`, `samples/`, `bin64/`, `.venv/`).
  Leave them untracked.
- Keep commits small and focused.

### Isolation mode: `#iso`

If the user's message contains the keyword `#iso`, enter isolation mode BEFORE doing
anything else (including reading or editing files):

1. Derive a short kebab-case slug from the task (e.g. `walk-jump-fix`).
2. Create a dedicated worktree and branch off `main`:
   `git worktree add "../reborn-iso-<slug>" -b "agent/<slug>" main`
3. For the rest of the task, operate ONLY inside that worktree. Use absolute paths
   for every read, edit, build, and commit. Never modify files in the original checkout
   and never `cd` out of the worktree permanently.
4. In your first status update, list the files/directories you own for this task. Do not
   edit anything outside that scope. If the task requires a file outside your scope, stop
   and report it instead of editing.
5. Commit to your branch and push after every commit. Never merge to `main`, never rebase
   `main`, never delete the worktree, never touch another agent's worktree.
6. When done, report: worktree path, branch name, commit hashes, files touched, and anything
   left uncommitted or blocked.

Shared resources (ports, `bin64/`, `.venv/`) are exclusive: state which ones you use in your
first update and never run two agents against the same shared resource. Engine runs are
namespace-exclusive, not machine-exclusive: concurrent isolated client builds are the
point (see "Parallel feature work on the client" below).

To change the trigger, edit the keyword in this section; agents follow whatever keyword is here.

### Parallel feature work on the client (isolated builds)

Client features are developed in parallel and merged when the subject is done.
Different client builds must not affect each other:

1. **One subject = one worktree + branch off `main`** (`#iso` gives the mechanics);
   merge back only when the subject is complete.
2. **Unique build name**: feature clients build as `reborn_client_<slug>.exe` via
   `set RC_CLIENT_EXE=reborn_client_<slug>.exe` before `client\build_client.cmd`
   (`RC_SMOKE_EXE=camera_smoke_<slug>.exe` for the smoke exe; feature builds skip the
   smoke build unless it is set). Never overwrite the canonical `reborn_client.exe`
   while a feature is in flight; the canonical name is restored at merge.
3. **No shared-state writes**: with `RC_CLIENT_EXE` set the build script skips the
   shared `bin64` config copies (`camera.json`, `scene_init_param.txt`) and writes
   `build_info_<exe>.txt` instead of the shared `build_info.txt`. Never write other
   files into the shared `bin64` root (`client/AGENTS.md`); logs are per-run files in
   `bin64\reborn_out\`, attributed by the fingerprint + `ns=` lines.
4. **Concurrent runs are the point - isolation, not exclusion**: 2+ feature clients run
   at the same time, each with its own engine memory namespace. The client derives it
   from its exe name (`reborn_client_<slug>.exe` -> `reborn_client_<slug>.memory`; the
   canonical `reborn_client.exe` keeps `MovieEditor.memory`; `RC_MEM_NS` overrides).
   The single-instance guard blocks only processes that share the namespace: the
   canonical build excludes `asset_sandbox`/`ability_picker` (they still hardcode
   `MovieEditor.memory`), a feature build excludes only a second instance of itself.
   `RC_ALLOW_MULTI=1` overrides. This follows the proven `ability_sandbox` recipe
   (own namespace + own runtime dir); the root-isolation attempt broke engine init
   (commit `d8268d2`), so the engine root stays shared.
5. **Attribute logs by fingerprint**: every run logs
   `build=<exe> <mtime> git=<hash> dirty=<n> ...` and the init line records
   `ns=<memory namespace>`; use both to separate concurrent runs.
6. **Known shared-write caveat**: all clients share the engine root
   `C:\SeasunGame\MovieEditor` (ShaderListUpload/dxvk caches) - treat concurrent-run
   flakiness as a shared-write suspect first, not as a namespace collision.

## 3. Session start (do this first)

1. `PLAN_REBORN_ONLINE.md` — goal, milestones, locked decisions.
2. `docs/GAME_SYSTEMS_RESEARCH_MAP.md` — primary research index (17 systems, ~110 areas, backlog §18).
3. Area index for the task: `docs/README.md`, then the area's `docs/<area>/README.md`
   (`netcode/`, `controls/`, `pvp/`, `camera/`, `movement/`, `engine_host/`, `ui/`, `audio/`).
4. Then task code. Load docs lazily — never preemptively read the whole docs tree.

State check before work: `git status`, `git log -5 --oneline`, confirm the branch.

## 4. Truth & lookup protocol

- Game client `C:\SeasunGame\Game\JX3\bin\zhcn_hd` is the true resource. For any problem or
  question, look here first (`bin64` DLLs, IL, shipped tables/paks). Read-only, always.
- MovieEditor `C:\SeasunGame\MovieEditor` is the canonical engine/resource host.
  Client-bundled `...\zhcn_hd\MovieEditor` is an older build (2026-04-28).
- Evidence hierarchy: repo docs → game client code/IL → MovieEditor behavior/IL → raw
  extracted caches. Never answer from assumption.
- Engine claims cite symbol/RVA. Every claim carries HIGH/MED/LOW + a source path.
- No web apps: not for the product and not for test/prototype tooling. New tooling is
  native (C#, C++, Python CLI). `map-ui-explorer`/`web/` were removed in cleanup — do not
  revive web paths.

## 5. `interface\` is not mechanism evidence

`C:\SeasunGame\Game\JX3\bin\zhcn_hd\interface` mixes shipped addon Lua with user/addon
data (`JX#DATA`, `MY#DATA`, `SG#data`, `userdata\...`). Mechanism questions must be
answered from code: engine/client binaries (`bin64\*.dll`, IL, symbols) and shipped
data tables. `interface\` may be used only for UI labels/strings or observations, and
any claim sourced there is marked `interface-addon` + LOW unless independently confirmed
by code. `#DATA`/`userdata` contents are player/addon perspective — never ground truth.

## 6. No invented fixes

Adopt from IL/engine before writing anything (`engine_host_spike/EXPERIENCE_MAP_SPIKE.md`:
"when the crash and the fix are both in the IL, trust the IL"). If a workaround is
unavoidable: label it *provisional*, state why no native path exists, and log it in
`docs/EXPERIENCES.md` with re-open criteria. No guess-scoring, no closest-match-by-name,
no procedural stand-ins presented as authored data.

## 7. Map-viewer disposition

The JX3 web map-viewer is a failed project. Its **raw extracted game resources** are
usable as input data (e.g. `SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\...`),
with provenance. Its **theory is banned**: no decoders/heuristics/parsed JSON/converted
artifacts (`.glb`, `_assets`), no porting its code or PSS heuristics. Repo copies were
removed in cleanup; `proof/gt_mapviewer_*` are player-era captures, not current GT.
This supersedes the stricter wording in `SFX_GROUND_RULES.md` for resources only.

## 8. Locked constraints

- Game installs are read-only. The only writes allowed under `C:\SeasunGame` are the
  documented build outputs into `C:\SeasunGame\MovieEditor\bin64` (see the `.cmd` scripts).
- No real-client hijack, no packet capture, no protocol RE (`PLAN_REBORN_ONLINE.md`).
- Never redistribute game assets; keep them in ignored dirs.

## 9. Legacy / stale — under assessment, do not extend

Keep for reference; do not edit, fix, import, or cite as current without checking.

| Item | Why |
|---|---|
| Root player-era docs: `HANDOFF*.md`, `STATUS_REPORT.md`, `STOP_SUMMARY.md`, `PLAN_PATH_C.md`, `PATH_C_*.md`, `B1_*.md`, `HUALUO_*.md`, `COLOR_FIX_NOTES.md`, `P0_MIDCLIP_FIX.md`, `DESKTOP_VIEWPORT.md`, `SFX_RUNTIME_STATUS.md`, `REPORT_SFX_MOVIEEDITOR_PATH.md`, `S1_MAPVIEWER_SFX_SMOKE.md` | Player-era (2026-09-21); several reference files no longer in the tree |
| Root player-era Python: `mina.py`, `min2.py`, `tani.py`, `mesh.py`, `pss*.py`, `catalog*.py`, `transport.py`, `resolve_playable.py`, `sfx_runtime.py`, `transport.py`, `smoke_sfx_runtime.py`, etc. | Frozen; new code must not import them — port needed logic into `tools/<area>/` |
| Root scratch: tracked `_*` PSS/probe files (27, e.g. `_pss_*.py`, `_pss_check.txt`, `_alpha_probe.py`) | One-off probes; frozen (see §13) |
| `ui-process-app/README.md` | "shared UiLayout renderer from map-ui-app" phrasing — map-ui-app retired |
| `proof/gt_mapviewer_*` | Player-era map-viewer captures; GT is client/MovieEditor only |
| Local runtime state: `perf_config.ini`, `log/`, `launch_out.txt`, `launch_err.txt` | Untracked local state; do not commit, delete, or overwrite |

## 10. Area map

| Area | Code | Docs index | Proof |
|---|---|---|---|
| Engine host / game client | `client/`, `app/`, `native/` | `docs/engine_host/ENGINE_HOST_PLAN.md`, `docs/engine_host/M1_*.md`, `engine_host_spike/` (recon only) | `proof/engine_host*`, `proof/map_spike` |
| Mode UI | `ui-process-app/` | `docs/ui/README.md`, `docs/netcode/JX3_MODE_UI_INVENTORY.md`, `docs/netcode/JX3_MODE_UI_FLOW.md` | `proof/ui` |
| Abilities / skills | `ability_picker/`, `ability_sandbox/`, `asset_sandbox/` | `docs/netcode/SKILL_DATA_RESEARCH.md` | `proof/netcode` |
| Netcode / protocol / server | `tools/netcode/`, `tools/netcode/reference/` | `docs/netcode/README.md`, `REBORN_SERVER_SPEC.md` | `proof/netcode` |
| Controls | `client/` (input), `tools/controls/` | `docs/controls/README.md` | `proof/controls` |
| Camera | `client/CameraSystem.cs`, `native/camera_shim.cpp` | `docs/camera/README.md` | `proof/*` camera sets |
| Movement / gravity / collision | `client/TerrainSampler.cs`, `client/FoliageCollision.cs`, `tools/gravity/`, `tools/movement/`, `tools/collision/` | `docs/movement/REBORN_JUMP_FALL_SPEC.md`, `docs/movement/JX3_GRAVITY_RESEARCH.md`, `docs/movement/FULL_MAP_COLLISION.md` | `proof/gravity`, `proof/collision` |
| PVP / combat | — | `docs/pvp/README.md` | `proof/pvp` |

## 11. Stack & toolchain

| Layer | Tool | Notes |
|---|---|---|
| C# net48 x64 | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` | **C# 5 only** — no `$"..."`, `?.`, `nameof`, expression-bodied members. `client/`, `app/`, `ability_*`, `asset_sandbox/` |
| C# net5.0-windows | `dotnet` SDK | `ui-process-app/` (WPF) |
| C++ | MSVC (VS2022 BuildTools `vcvars64`) | `native/` |
| Python 3.12 | `.venv\Scripts\python.exe` | stdlib-first; root modules are legacy |
| Shell | PowerShell 5.1 | no `&&`; quote paths with spaces; prefer full cmdlet names |
| Node/JS | none | removed in cleanup; do not reintroduce |

## 12. Build / run / verify

```powershell
# must-stay-green gates
.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py     # 10x PASS
.venv\Scripts\python.exe tools\gravity\verify_model.py            # jump/fall model
.venv\Scripts\python.exe tools\netcode\loot\capture.py selftest   # 8 checks
native\build_shim.cmd                                             # bin64\camera_shim.dll, RC_Shim exports
# UI gate (after dotnet build ui-process-app -c Release):
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 15/15 windows

# builds
client\build_client.cmd        # bin64\reborn_client.exe + camera_smoke.exe
app\build_launcher.cmd         # app\JX3Reborn.exe (launches reborn_client.exe)
ability_picker\build.cmd       # bin64\ability_picker.exe (run: ability_picker\run.cmd)
ability_sandbox\build.cmd      # bin64\ability_sandbox.exe (isolated out dir)
asset_sandbox\build_asset_sandbox.cmd
dotnet run --project ui-process-app
```

## 13. Hard rules

- **Notes placement**: new research lands in `docs/<area>/` and must be registered in
  that area's README index table. No new root-level `.md`.
- **Doc lifecycle**: stale docs get `SUPERSEDED by <path>` instead of silent rot; every
  research doc ends with a Reproduce section; add `Last verified: YYYY-MM-DD` when
  re-verified; claims carry confidence + evidence path.
- **Proof integrity**: an artifact counts only when a doc cites it with capture context
  and date; never overwrite proof files; never cite outputs known to be broken.
- **Tools**: extend existing tools before adding; no `recon_physics2/3/4`-style variants;
  list tools in the area README tools table; delete one-off probes after use.
- **Testing**: no fix without a reproduce/verify command; prefer offline deterministic
  checks; visual passes need a numeric fingerprint (per-region RGB), not one screenshot.
- **Scope**: align to the M1 exit criteria; one milestone/area per session; no M2+ work
  before the M1 gate passes.
- **Constants**: canonical values live in `docs/netcode/README.md`; do not redefine magic
  numbers; client and server share one movement/combat rules source.
- **Privacy**: no committed absolute user paths or account data (`%USERPROFILE%` placeholders);
  never commit `userdata`/`custom.dat`; add `.gitignore` entries in the same change as new
  extraction dirs.
- **Dependencies**: Python stdlib-first; no new dependencies without asking; `.venv` only,
  no global installs; no new Node deps.
- **Encoding**: decode game text as GB18030/GBK; repo files are UTF-8.
- **Git**: commit style `Area: summary` (e.g. `Client:`, `Camera:`, `Cleanup:`, `Docs:`);
  branches `research/<topic>`, `feature/<name>`, `cleanup/<scope>`, `<area>-fix`; small
  focused commits; never commit to `main` unless asked.
- **Engine ops**: engine init ~24 s, each test run ~2 min — automate with env switches and
  log/timestamp outputs; kill stale hosts before rebuilds; never run two engine clients in
  the same memory namespace (concurrent isolated feature builds are allowed — §2);
  commit recon dumps so they are never redone.
- **Binaries**: never open game assets/binaries with Read; use `tools/` scanners
  (`gbk_grep.py`, `extract_*`, `scan_*`); prefer indexes (`filepath.ini`, `tani.rt`).

## 14. Pending source audit

Claims currently sourced from `interface\` addon/user data; annotate on touch:

- `docs/netcode/JX3_MODE_MATCH_LIFECYCLE.md` (~line 204) — match schedule from `MY#DATA` userdata.
- `proof/pvp/pvp_modes_rules.md` — mode predicates from decrypted addon Lua, uncorroborated.
- `docs/netcode/SKILL_DATA_RESEARCH.md` — attribute labels from `interface\**\lang\zhcn.jx3dat` (labels only).
- `docs/netcode/JX3_MODE_JUEJING.md` — interface scan command; output reused as evidence.
- `proof/pvp/attributes_and_damage.md` — already flags an addon contradiction (LOW); generalize.
- `proof/gt_mapviewer_*` — relabel as player-era, not GT.

## 15. Response guidelines

Loose format. Required: a `Verified:` line (command → result) for any change or finding,
and evidence paths/confidence for factual claims. Do not present hypotheses as facts.

Definition of done: verify/gate command run; area README index updated; `docs/EXPERIENCES.md`
entry appended.

Ask before: new dependencies, deleting files, touching anything under `C:\SeasunGame`
beyond documented build outputs, changing locked decisions, crossing milestones.

## 16. Experience log

`docs/EXPERIENCES.md` — append-only, newest at the bottom. Append a compact entry after
every work-bearing response; full template for notable lessons, dead ends, or decisions.
Legacy records: `engine_host_spike/EXPERIENCE_MAP_SPIKE.md` (the removed
`_port_from_mapviewer/EXPERIENCES.md` is recoverable from git history, commit `f92139d`).
