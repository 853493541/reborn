---
name: merge-finalize
description: Use when merging an agent/feature branch into main for this repo — preflight, the proven conflict-resolution policy (client code, build script, EXPERIENCES, indexes), dual rebuild, must-stay-green gates, relaunch, and the local-only commit rule (never push unless asked).
---

# Merge a branch into main (finalize)

Branches are parallel feature work (`AGENTS.md` §2). Merge only when the subject is done
and the branch worktree is clean. **If the branch choice is ambiguous, ask — never infer.**

## 1. Preflight

```powershell
git merge-tree --write-tree --name-only main <branch>   # exit 0 = conflict-free
git -C "C:\Users\Zhibin Ren\Desktop\reborn-iso-<slug>" status --short   # must be clean
```

Optionally verify the branch's own gate first (e.g. rebuild its smoke in a detached temp
worktree; never modify another agent's worktree).

## 2. Conflict resolution policy (the proven way)

- `client/RebornClient.cs` → `git merge-file --ours`: export the three stages to temp
  (`git show :1:/:2:/:3:` via cmd redirection, byte-safe), run `git merge-file --ours
  ours base theirs`, copy back. Then check for dangling refs the auto-merged branch code
  uses (e.g. `selfSlug`) and restore them in main's version.
- `client/build_client.cmd` → keep **main's** feature-build contract
  (`RC_CLIENT_EXE`/`RC_SMOKE_EXE`/`BINFO`).
- `docs/EXPERIENCES.md` → keep main's file, then append the branch's entry blocks whose
  `###` headers are not present in main (parse blocks with `^### `).
- `docs/*/README.md` → union rows/tables.
- New files (tools, proof, docs) merge normally.

Commit with `--no-ff` and a message naming the branch's features.

## 3. Rebuild + gates

```powershell
client\build_client.cmd                                              # canonical
set RC_CLIENT_EXE=reborn_client_mini.exe && client\build_client.cmd  # sandbox
C:\SeasunGame\MovieEditor\bin64\camera_smoke.exe                     # ALL PASS
.venv\Scripts\python.exe tools\gravity\verify_model.py
.venv\Scripts\python.exe tools\netcode\loot\capture.py selftest
.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py
```

If the branch touched `native/camera_shim.cpp`, rebuild the shim
(`native\build_shim.cmd`) with all clients closed.

## 4. Relaunch + finish

- Relaunch with proper titles: canonical `RC_TITLE=Main-Full-Client` (or `JX3`), sandbox
  auto `sandbox-mini` (see `engine-run` skill).
- Append the EXPERIENCES entry; keep commits **local** — never push to origin unless the
  user explicitly asks (AGENTS §2/§13).
- Report: merge hash, conflicts resolved, build ids, gate results, running PIDs/titles.

## 5. Post-merge notes

- `agent/double-jump`-style branches that already contain a previously merged branch
  (e.g. camera-wall-clip) need a rebase afterwards.
- A shared `camera_shim.dll` built from a stale branch reintroduces the D6 crash — the
  build script now refuses stale sources and every log shows `d6=` (see `crash-triage`).
