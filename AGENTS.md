# JX3 Reborn — agent rules

Read this first. Keep it short; depth lives in the linked docs.

## 1. What this repo is

JX3 "Reborn": research plus desktop prototypes that recreate JX3 runtime systems
(engine host, netcode, controls, camera, collision, mode UI) using the real game
engine DLLs and original game data. The product is a desktop client — not a website.
Game assets are private and are not in the repo.

**Purpose & legal (locked, user statement):** this project is **purely for personal
interest — it will NEVER earn any money; it is purely for fun.** No commercial use, no
monetization, no distribution, no sale of any kind. Game assets stay private and are
never redistributed. Any proposal that would commercialize the project is out of scope
by this rule.

## 2. Workflow modes

### ACTIVE RULE — NO CLIENT STARTS (user, 2026-10-05, until the user removes it)

**Static research ONLY.** The user is playing the real JX3 on this machine.

- Never launch JX3ClientX64, reborn_client*.exe, the launcher emulator (`launcher_emulator.py`,
  `run_emul.cmd`), or any run that starts a game client.
- Never run the gateway/game/serverlist stubs against a live client; no client drives, no
  window automation, no memory reads of a running game client.
- Work only on binaries, IL, paks, tables, and docs (copy & analyze). This rule stays until
  the user explicitly lifts it.

### Default mode

Work directly in the current checkout on the current branch, as normal.

- **V2 protocol work is mostly static; only milestone boundaries run the client.** Extract
  layouts/tables from the binaries in batch; do not discover protocol steps with
  step-by-step live runs.
- **Real captures only via script** (`tools/netcode/login_driver.py`, `drive_verify.py`,
  probes): never the user's mouse, never ask the user to log in or click for a test.
- **Never push to `origin` (any branch) unless the user explicitly asks** — pushing is
  always an explicit request; local commits are fine.
- Never commit to `main` unless the user explicitly asks.
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
5. Commit to your branch after every change; never push unless the user explicitly asks.
   Never merge to `main`, never rebase `main`, never delete the worktree, never touch
   another agent's worktree.
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
   **If a client start is blocked by this guard, the response must end by naming the
   conflicting session** — process name, PID and start time holding the namespace (from
   the guard message or a process check) — so the user knows which window/session to
   close.
5. **Attribute logs by fingerprint**: every run logs
   `build=<exe> <mtime> git=<hash> dirty=<n> ...` and the init line records
   `ns=<memory namespace>`; use both to separate concurrent runs.
6. **Known shared-write caveat**: all clients share the engine root
   `C:\SeasunGame\MovieEditor` (ShaderListUpload/dxvk caches) - treat concurrent-run
   flakiness as a shared-write suspect first, not as a namespace collision.
7. **Feature title (mandatory on every client update)**: whenever a client is updated
   for a feature, its top-left window title must name that feature — never leave it as
   the plain canonical name. Feature builds derive it from the exe name
   (`reborn_client_<slug>.exe` -> `sandbox-<slug>`; sandboxes: `sandbox-ability`,
   `sandbox-asset`); `RC_TITLE=<feature>` overrides for one-off runs of the canonical
   exe. If several client windows show the same title you cannot tell which feature is
   which — that is a rule violation.
8. **Shared shim**: `bin64\camera_shim.dll` is shared by all clients — rebuild it only
   from a worktree current with main (the build script refuses sources without
   `RC_D6Seed`); every run log records the loaded shim's `d6=` seed status. A stale-branch
   shim build reintroduces the D6 crash for every client (2026-09-30 incident).

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
- **Research = copy & analyze, never affect**: the client and MovieEditor installs may be
  copied out (into ignored dirs) and analyzed offline (parsers, scanners, disassemblers on
  the copies), and observed while running. Never write, rename, delete, patch, or inject
  into the installs or their running processes — on disk or in memory. The only writes
  under `C:\SeasunGame` are our documented build outputs into `MovieEditor\bin64` (§8);
  those are our binaries, not the client's.
- **Game client is primary; MovieEditor is a visual resource.** For any mechanism,
  behavior, format, or value question, the game client (`...\zhcn_hd` binaries/IL/paks)
  is the source of truth — do not answer a game-client question by digging into
  MovieEditor. MovieEditor is used for rendering/preview (visual resources) and as
  supporting host-behavior evidence only. Client-bundled `...\zhcn_hd\MovieEditor` is an
  older build (2026-04-28).
- Evidence hierarchy: repo docs → game client code/IL → raw extracted caches; MovieEditor
  behavior/IL supports visuals and host behavior only. Never answer from assumption.
- **Local-first: everything needed is in the client/MovieEditor installs.** Whatever the
  game needs to run (prediction, UI data, tables, configs, formats) exists locally in the
  client binaries/IL/paks or the MovieEditor engine — "it comes from the server / we
  cannot find it" is **not an acceptable answer**. Keep digging locally; a server-side
  claim needs a cited client-side counterpart before it is used.
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

**Explore the whole system; never patch the reported spot.** When part of a system's
behaviour is incorrect, the system was wired up wrong — not only the place that shows it.
Explore the full system first (inputs → state → outputs, the data it reads, the engine
calls it makes) before changing anything. When the user points at a specific wrong spot,
that is a symptom location, not the fix target: fixing only there produces a band-aid.
Trace the full chain to the root cause and fix the wiring.

## 7. Map-viewer disposition

The JX3 web map-viewer is a failed project. Its **raw extracted game resources** are
usable as input data (e.g. `SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\...`),
with provenance. Its **theory is banned**: no decoders/heuristics/parsed JSON/converted
artifacts (`.glb`, `_assets`), no porting its code or PSS heuristics. Repo copies were
removed in cleanup; `proof/gt_mapviewer_*` are player-era captures, not current GT.
This supersedes the stricter wording in `SFX_GROUND_RULES.md` for resources only.

## 8. Locked constraints

- Game installs are read-only. Research may copy files out and analyze them, but must
  never affect the install itself (no writes, renames, patches, or injection — on disk or
  in memory). The only writes allowed under `C:\SeasunGame` are the documented build
  outputs into `C:\SeasunGame\MovieEditor\bin64` (see the `.cmd` scripts).
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
- **Images**: the model API caps images per request (30), and the cap counts the whole
  conversation — never Read image files for analysis in a session that already has
  several; use `tools/proof/image_stats.py` (size/hash/per-region RGB) instead. If an
  image must be attached, do it in a fresh session and keep the total well under 30.
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
  focused commits; never commit to `main` and **never push to `origin` unless the user
  explicitly asks**.
- **Engine ops**: engine init ~24 s, each test run ~2 min — automate with env switches and
  log/timestamp outputs; kill stale hosts before rebuilds; never run two engine clients in
  the same memory namespace (concurrent isolated feature builds are allowed — §2);
  commit recon dumps so they are never redone.
- **Background processes**: never launch long-lived processes (emulator, stub, watchers)
  with `Start-Process` from the tool shell — `.venv\Scripts\python.exe` is a venv redirector
  that spawns the real interpreter as a child; on a tool timeout-kill the redirector dies but
  the orphaned real python survives holding the shell's stdout/stderr pipe, and every
  subsequent tool command is blocked until it exits (2026-10-04: a 3600 s `--observe`
  emulator stalled the session 58 min; normally-completing calls do not block). Launch them
  via WMI wrappers instead: `Invoke-CimMethod Win32_Process Create -Arguments @{CommandLine =
  'cmd /c C:\jx3tmp\run_<x>.cmd'}` (WMI children inherit no tool handles; proven recipe:
  `run_stub.cmd`, `run_vspam.cmd`, `run_emul.cmd`).
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

**Full-chain ownership (the agent is the tester).** When a problem repeats, or when a fix
is requested, the agent owns the entire chain — never hand testing back to the user:

1. **Reproduce** it deterministically first (scripted repro + env switches; capture logs
   and a numeric fingerprint — `tools/proof/image_stats.py`; proof files under `proof/`).
2. **Fix** against that reproduction.
3. **Prove it is solved**: re-run the same reproduction and show before/after evidence,
   plus the regression gates (§12). "Should be fixed" is not an outcome.
4. If it cannot be reproduced, report exactly what evidence/conditions are missing and
   what the next probe would be — do not ask the user to retry and report back.

**Mandatory closing game-design check.** End every response with:

> **Game-design check:** Does this follow the client's own truth — no invented fixes or
> band-aids? **Yes** — we connect the real game engines and reproduce the original game
> design; nothing was invented or band-aided around.

If a provisional deviation is involved, the check must name it explicitly (the registered
deviation + re-open criteria, §6) — a "Yes" must never hide it.

Definition of done: verify/gate command run; area README index updated; `docs/EXPERIENCES.md`
entry appended.

Ask before: new dependencies, deleting files, touching anything under `C:\SeasunGame`
beyond documented build outputs, changing locked decisions, crossing milestones.

## 16. Experience log

`docs/EXPERIENCES.md` — append-only, newest at the bottom. Append a compact entry after
every work-bearing response; full template for notable lessons, dead ends, or decisions.
Legacy records: `engine_host_spike/EXPERIENCE_MAP_SPIKE.md` (the removed
`_port_from_mapviewer/EXPERIENCES.md` is recoverable from git history, commit `f92139d`).
