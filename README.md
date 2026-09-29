# JX3 Reborn

Research plus desktop prototypes that recreate JX3 runtime systems — engine host,
netcode, controls, camera, collision, mode UI — using the **real game engine DLLs**
(MovieEditor) and original game data. This is a desktop client project, not a website.

Game assets are private and are **not** in this repo. The local JX3 installs are the
source of truth for code and data.

## Start here

| Document | What it gives you |
|---|---|
| `PLAN_REBORN_ONLINE.md` | Goal, milestones, locked decisions |
| `docs/GAME_SYSTEMS_RESEARCH_MAP.md` | Primary research index: 17 systems, ~110 areas, priority backlog |
| `docs/netcode/README.md`, `docs/pvp/README.md`, `docs/controls/README.md` | Area indexes |
| `AGENTS.md` | Rules for AI agents working in this repo |
| `docs/EXPERIENCES.md` | Development log: what was tried, what worked, dead ends |
| `proof/` | Evidence artifacts (captures, disasm, dumps) |

## Layout

| Path | Content |
|---|---|
| `client/`, `app/` | Desktop client (`reborn_client.exe`) and launcher (`JX3Reborn.exe`) |
| `ability_picker/`, `ability_sandbox/`, `asset_sandbox/` | Engine-host tools for skills, doodads, scene experiments |
| `ui-process-app/` | WPF mode-UI explorer rendered from real KGUI layouts |
| `native/` | C++ camera shim (`camera_shim.dll`) |
| `tools/` | Research/extraction tooling (netcode, gravity, movement, collision, controls, pvp) |
| `docs/` | Research docs, per-area indexes |
| `engine_host_spike/` | Recon notes and maps from the original engine-host spikes |
| `proof/` | Evidence and verification artifacts |

## Requirements

- 64-bit Windows with the local JX3 installs at
  `C:\SeasunGame\Game\JX3\bin\zhcn_hd` (game client) and
  `C:\SeasunGame\MovieEditor` (engine host).
- Python 3.12 with `.venv` (research tooling; stdlib-first).
- .NET SDK for `ui-process-app`; `.NET Framework 4.8` `csc` for the C# 5 projects.
- MSVC (VS2022 BuildTools) for `native/`.

## Build / run / verify

```powershell
# green gates
.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py     # 10x PASS
.venv\Scripts\python.exe tools\gravity\verify_model.py
.venv\Scripts\python.exe tools\netcode\loot\capture.py selftest
native\build_shim.cmd

# builds
client\build_client.cmd        # bin64\reborn_client.exe
app\build_launcher.cmd         # app\JX3Reborn.exe
dotnet run --project ui-process-app
```

Full command list and hard rules: `AGENTS.md`.
