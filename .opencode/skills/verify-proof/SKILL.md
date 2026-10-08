---
name: verify-proof
description: Use when verifying a change in this repo — the must-stay-green gates, numeric fingerprint workflow for visuals (image_stats.py; never attach images), proof placement rules, and the definition of done (gate run, index update, EXPERIENCES entry, Verified line, game-design check).
---

# Verify + proof (reborn)

## Must-stay-green gates

```powershell
.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py     # 10x PASS
.venv\Scripts\python.exe tools\gravity\verify_model.py            # jump/fall model
.venv\Scripts\python.exe tools\netcode\loot\capture.py selftest   # 8 checks
native\build_shim.cmd                                             # RC_Shim + RC_D6Seed exports
C:\SeasunGame\MovieEditor\bin64\camera_smoke.exe                  # ALL PASS
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 15/15 windows
```

Client feature branches add their own gate (e.g. `collision_selftest.exe` on the
collision branch; a rebuilt `camera_smoke` for camera work).

## Visual proof = numeric fingerprint

- `tools\proof\image_stats.py <img> [<img>...] [--grid 8x8]` → size, SHA256, per-region
  mean RGB (hex). Compare before/after numerically.
- **Never Read image files for analysis**: the model API caps images per request (30) and
  the cap counts the whole conversation — a session that exceeds it dies. Use the tool.

## Proof integrity

- Files live under `proof/<area>/` and count only when a doc cites them with capture
  context + date. Never overwrite proof files; never cite known-broken outputs.
- Logs are per-run files in `bin64\reborn_out\`; attribute them by the fingerprint
  (`build=<exe> <mtime> git=<hash> dirty=<n>`) + `ns=` lines.

## Definition of done

1. The verify/gate command was run (show the result).
2. The area README index is updated (new docs/tools registered, `AGENTS.md` §13).
3. A `docs/EXPERIENCES.md` entry is appended (compact; full template for lessons/dead
   ends).
4. The response carries a `Verified:` line (command → result) and ends with the
   mandatory game-design check — answer **Yes** ("follows the client's own truth"); if a
   provisional deviation is involved, name it explicitly (registered + re-open criteria).

## When a problem repeats

Own the full chain (AGENTS §15): reproduce deterministically first (scripted repro, env
switches, logs, numeric fingerprint), fix against the repro, re-run and show before/after
plus the gates — never ask the user to retry and report back.
