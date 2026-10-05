# tools/netcode — agent notes

Static, read-only research against the JX3 client install. Nothing here may write to
`C:\SeasunGame` — extraction outputs go to ignored `proof/netcode/...` dirs with a
`SOURCES.txt` (source path + tool + command).

- **Copy & analyze only** (root `AGENTS.md` §4): never write, patch, or inject into the
  installed client/engine or its running processes (on disk or in memory). Work on copies
  under `proof/`.
- **Local-first** (root `AGENTS.md` §4): whatever the client needs exists in the client
  install (prediction, tables, configs, UI data). "Server-only / we cannot find it" is not
  an acceptable answer — keep digging and cite the client-side counterpart.

- Python stdlib-first, run with `.venv\Scripts\python.exe`; no new dependencies.
- `reference/jx3_model.py` is the runnable spec model — **10x PASS must stay green**.
  The contract lives in `docs/netcode/REBORN_SERVER_SPEC.md`.
- `loot/capture.py selftest` = 9 checks; keep green.
- Prefer official Seasun extractors (`bin64\PakV4SfxExtract.exe`, `extract_hpkg_member.py`).
- Evidence only from code: binaries/IL/tables (root `AGENTS.md` §5). `interface\`
  userdata is player perspective — never mechanism evidence.
- New docs go to `docs/netcode/` and must be registered in `docs/netcode/README.md`
  (documents + tools tables). Zip extractions are not committed.
