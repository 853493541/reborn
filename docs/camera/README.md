# docs/camera - camera research index

Camera model and host work. Canonical gameplay spec in `docs/netcode/REBORN_CAMERA_SPEC.md`; divergences from the native rule in `HOST_DEVIATIONS.md`.

| Doc | Title |
|---|---|
| `ADOPTION_FOR_ONLINE_CLIENT.md` | Camera: what the online client (`reborn-online`) should take from the camera work |
| `CLIENT_AUDIT.md` | Main client camera audit (`reborn-merge`, branch `merge`) |
| `CLOSE_RANGE_RESEARCH.md` | Close-range camera + obstruction return - engine research (2026-09-24) |
| `COMPLETION_PLAN.md` | Camera plan #2 — first fix, then full completion |
| `CONFIG_FILES.md` | JX3 camera config files — inventory and where the values live |
| `CONFORMANCE_CHECKS.md` | Camera conformance checks — notes vs code |
| `DISTANCE_FOV_SPEC.md` | Camera settings spec — 镜头最大距离 (max distance) + 广角 (FOV) |
| `DRAG_MODEL.md` | JX3 camera drag model - proven from the client binaries (2026-09-24) |
| `FIX_SPEC.md` | Camera drag/placement fix — exact spec (2026-09-24) |
| `FIX_SUGGESTIONS.md` | Camera fix suggestions — after the JX3 drag model (`00f1237`) |
| `HANDOFF.md` | Camera work - handoff report (for a fresh session) |
| `HOST_DEVIATIONS.md` | Host deviations register (camera) |
| `INPUT_CONTROLS.md` | JX3 camera input controls — real client data (2026-09-23) |
| `PENETRATION_PLAN.md` | Camera penetration plan (accurate, 2026-09-28) |
| `REAL_VALUES.md` | JX3 camera — real values found (2026-09-23) |
| `RECONCILIATION_STATUS.md` | Camera reconciliation status (2026-09-24) |
| `STATUS.md` | Camera status — JX3 follow model (port of the camara-imp branch) |
| `WALL_OBSTRUCTION.md` | Native JX3 camera wall obstruction |

## Tools

| Tool | Purpose |
|---|---|
| `tools/camera/minidump_exc.py` | stdlib minidump reader: exception record, registers, module-resolved stack candidates, register-pointer strings. Used for the D6 crash analysis (no debugger installed; dumps in `%LOCALAPPDATA%\CrashDumps`). |
| `tools/camera/drive_client.ps1` | Synthetic player input driver (camera drags + WASD via `mouse_event`/`keybd_event`) to reproduce interactive-only engine crashes (D6) on a running client. |
