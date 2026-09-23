# Reborn Online — plan (engine-host client + our netcode)

**Branch:** `feature/engine-host-online` (worktree `Desktop\reborn-online`)
**Goal:** recreate the 绝境战场 mode online, using the real JX3 engine (MovieEditor DLLs)
for rendering/assets and our own client + server. No real-client hijack, no packet capture,
no protocol RE.

## Locked decisions

| Topic | Decision | Why |
|---|---|---|
| Client | C# net48 x64, WinForms + `MovieEngineCLR` | `MovieEngineCLR.dll` is mixed-mode C++/CLI; only .NET Framework x64 can host it |
| Server | C# (.NET 8) + shared rules library | one movement-rules source for client and server |
| Protocol | `docs/netcode/REBORN_SERVER_SPEC.md` (ours) | already designed + reference model (`tools/netcode/reference/jx3_model.py`) |
| Mode | 绝境战场, first map 龙门寻宝 | all BR maps have baked collision; 龙门寻宝 is the most complete |
| First milestone | **one player, fully animated, on the real map** | prove the process before networking |
| Scale | decided later | M1 is single player |

## Foundation (proven, reuse)

| Area | Artifact |
|---|---|
| Engine host init + actor + tani + SFX | `engine_host_spike/SpikeHost.cs` (Spike A PASS) |
| Map load + camera + player walk/jump + collision | `engine_host_spike/MapSpike.cs` (Spike B PASS) |
| Baked collision (5 maps) | `engine_host_spike/collision_data/`, `tools/bake_map_collision.py` |
| Jump/gravity/fall exact model | `docs/REBORN_JUMP_FALL_SPEC.md` + `tools/gravity/verify_model.py` |
| Movement + camera rules | `docs/JX3_CHARACTER_MOVEMENT_RESEARCH.md`, `docs/netcode/REBORN_CAMERA_SPEC.md` |
| Netcode model + server spec | `docs/netcode/REBORN_SERVER_SPEC.md`, `tools/netcode/reference/` |
| Skill data (182 绝境 skills) | `docs/netcode/JX3_MODE_JUEJING_LOGIC.md`, `SkillMove.tab`, ranges, cooldowns |
| Loot/doodad layouts + tables | `docs/netcode/JX3_LOOT_PROTOCOL_LAYOUTS.md`, `JX3_MODE_LOOT_SYSTEM.md` |
| Map catalog + BR rows | `MapList.tab`, `JX3_MODE_LOAD_FLOW.md` |
| Launcher shell | `app/JX3RebornLauncher.cs` |

## Milestone 1 — one player (current)

Steps:

1. **M1.2 actor-on-map spike** (current): animated actor on a loaded map, moved per frame.
   Leads: `KGSceneCLR.AddDummyModel` / `AddStateMachineModel`, `KGModelCLR.AttachModel+PlayAnimation`,
   `KGMovieEditorCLR.SetObjectProperty`, `KGEngineCLR.SetMainPlayerType`.
2. **M1.1 client scaffold**: product client app from the proven spike code.
3. **M1.3 shared movement model**: port the exact integer jump/gravity/fall model to C#,
   unit-tested against `tools/gravity/verify_model.py` outputs.
4. **M1.4 animation state machine**: move state → clip (idle/walk/run/jump/fall) from the Ani catalog.
5. **M1.5 input + follow camera**.
6. **M1.6 one skill cast** (tani + PSS + sound).
7. **M1.7 HUD stub + 5-min solo run proof**.

**M1 exit:** one 花萝, real 龙门寻宝 map, animated, walks/runs/jumps/falls with real game values,
one skill casts with animation + SFX, follow camera, stable 5 minutes.

## Later milestones

- **M2 networking**: port reference model to C#; server skeleton; connect → spawn; input → snapshots;
  prediction/reconciliation; remote players rendered + interpolated.
- **M3 combat**: data-driven skill runtime (cast/channel/effect, cooldowns, ranges, dashes),
  damage/death, feedback; one school first, then all 182.
- **M4 BR loop**: rooms/queue, spawn points per map, loot doodads + loot window + inventory,
  zone phases/shrink, revive, scoring, win.
- **M5 bots + polish**: server-side bots, lobby, teams, scoreboard.
- **M6 hardening**: actor performance at scale, reconnect/resume, version pin, packaging.

## Rules

- Do not modify the real client or touch Seasun servers.
- Prefer engine APIs the editor itself uses (recon before inventing).
- Spikes stay as reference; product code lives in `client/`.
- Server-only values (spawns, drop rates, phases, scoring) are invented until evidence exists;
  label them in data files.
