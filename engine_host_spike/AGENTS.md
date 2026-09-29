# engine_host_spike — agent notes

Recon/reference area only. The buildable hosts and run scripts were removed in the
repo cleanup; the product hosts live in `client/`, `app/`, and the sandbox apps.

- Read `EXPERIENCE_MAP_SPIKE.md`, `RECON_CAMERA.md`, and the `recon_*.txt` dumps
  before re-dumping anything. Recon artifacts are committed so they are never redone.
- `collision_data/` holds baked per-map collision; `*.bin` are generated and ignored
  (regenerate with `tools/bake_map_collision.py`; runtime copies live in
  `C:\SeasunGame\MovieEditor\bin64\collision_data`).
- `sound_extract/` is extracted sound evidence — keep, do not edit.
- Engine context: init ~24 s, a full test run ~2 min. Never run two engine hosts
  concurrently. Kill stale hosts before rebuild/run.
- Root `AGENTS.md` rules apply.
