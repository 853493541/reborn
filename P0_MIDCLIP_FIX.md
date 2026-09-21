# P0 Mid-clip explode fix — 2026-09-20

## What was wrong
`applyPoseByName` / `bone.decompose` expects **parent-local** TRS.
`transport.Sample` only filled **world** `matrices` from `clip.matrices_at`.
`pose_drive.tick_pose` and (when local missing) chrome `_sync_fbx_pose` fed those
world mats into FBX → star spikes / explode mid-clip (风来吴山·蓄力).

## What changed
1. **`min2.py`** — `Min2SkelClip.local_matrices_at(frame)` returns 3×4 row-major (R|t)
   in parent space. Stores `_local_positions` during `decode_skel_stick`:
   - root: animated/bind position per frame
   - children: constant parent-space `rest[i]` (same as FK)
   Rotation from existing `_local_quats` (same quat→matrix math as `matrices_at`).
2. **`transport.py`** — `Sample.local_matrices`; `sample(with_pose=True)` fills it via
   `local_matrices_at` when present. World `matrices` kept for Mesh RQ LBS.
3. **`pose_drive.py`** — prefers `sample.local_matrices` for `apply_pose`; **does not**
   apply world mats to FBX.
4. **`player.py` `_sync_fbx_pose`** — local only (no world fallback).
5. **`web/fbx_viewport.js` ≡ `viewport_fbx/fbx_viewport.js`** — keep
   `fromArray` + `decompose` (no transpose). Skip |t|>5000 as world-looking safety net.

## Proof
Run on Andy:
```
cd "C:\Users\Zhibin Ren\jx3-ani-player"
.venv\Scripts\python.exe proof_midclip_local.py
```
Expected: `proof/compare/aniplayer_flws_midclip_human_YYYYMMDD.png` (matplotlib stick,
humanoid charge) and `max |t| local` human-scale (≪ 5000).

Ignore explode junk: `*_flws_f0*.png`, `*_local_f*.png`, `*_retarget_*.png`,
`aniplayer_flws_pose_f*.png`.

## Sync package
Copy everything under `sync_to_andy/` onto Andy's tree root (and `web/` +
`viewport_fbx/` for the JS twin).
