# JX3 Ani Player — Status Report (handoff)

**Updated:** 2026-09-20 ~16:09 PT  
**Audience:** Next team picking up cold  
**Canonical docs:** `HANDOFF.md` (full), `STOP_SUMMARY.md` (crisp next steps), this file (status snapshot)

---

## One-liner

Desktop **Python/Tk** companion player with an embedded **Three.js** 花萝 FBX viewport. Look/materials are largely good; **action pose on FBX is still broken** (mesh explode). Map-viewer Animation Player is the GT for action.

---

## Architecture (not a full web rewrite)

| Layer | What |
|-------|------|
| Shell | `Start JX3 Ani Player.bat` → `player.py` (Tk catalog, Play/Pause/Seek) |
| Character view | Web: `viewport_fbx/` Three.js on `http://127.0.0.1:8765` (embedded or opened) |
| Pose pipe | catalog `playable_path` → MIN2 → `PlaybackClock` → `sample.local_matrices` → `apply_pose` → viewport poll |
| Reference | map-viewer Player Animation Browser (materials + correct action) |

**Not** a standalone website. Only the 3D viewport is web.

---

## Machines

| Role | Path |
|------|------|
| Andy | `C:\Users\Zhibin Ren\jx3-ani-player` (machineId `3bd32f7d-8a1e-43b8-b5fd-2e413ab6ffde`) |
| Box mirror | `/workspace/jx3-ani-player` |
| Map-viewer (verify on Andy) | SeasunDownloader `jx3-web-map-viewer` (~:3015 historically) |

---

## Done ✅

| Area | Proof / artifact |
|------|------------------|
| Diffuse/sRGB/normals/Specular vs map-viewer | `COLOR_FIX_NOTES.md`, textured HUD Diffuse 28 |
| Peach face (dark `head_hd`/`hat` veils) | `proof/compare/aniplayer_face_v3.png` |
| No cyan sleeve FX cards | `proof/compare/aniplayer_sleeves_v1.png` (+ closeup) |
| Catalog → playable `.ani` (风来吴山·蓄力) | `resolve_playable.py`, `samples/player/catalog/` |
| Clock + pose_drive + chrome local prefer | `pose_drive.py`, `player.py` `_sync_fbx_pose` |
| Bone **names** 182/182 | `proof/fbx_ani_bone_map.json` (**keep**) |
| Local vs world contract | FBX: `sample.local_matrices` / `local_matrices_at`; world only for Mesh RQ LBS |
| Startup: missing Dev2 modules on Andy | ensure `resolve_playable.py` + `pose_drive.py` present |
| Handoff scaffolding | `HANDOFF.md`, `STOP_SUMMARY.md`, Handoff agent |

---

## Open — do next (priority)

### P0 — FBX action pose (BLOCKER)
- 风来吴山·蓄力 on 花萝 FBX still **explodes** after local matrices + retarget attempts
- Tried / **do not SOURCES**: `*_flws_f0*.png`, `*_local_f*.png`, `*_retarget_*.png`, `aniplayer_flws_pose_f*.png`
- Bone **names** OK — blocker is **matrix / rest-pose space vs map-viewer** (Dev4)
- **Exit criteria:** mid-clip PNG of a recognizable humanoid charge pose (no spikes), then match map-viewer framing

### P1 — Bind-pose floating hands
- Likely related to bind/skin; fix with or after P0

### P1 — Merge lookalike chrome into live Tk window
- Lookalike proofs exist; live merge + fresh both-app screenshots still open

### P2 — Dev5 SOURCES + map-viewer side-by-side
- Only after P0 mid-clip is human

### Hygiene
- Keep `web/fbx_viewport.js` ≡ `viewport_fbx/fbx_viewport.js`
- Never feed `.tani` into MIN2 — resolve skeletal `.ani` first
- Don’t re-enable dark hat/head or cyan hand FX without alpha-aware path

---

## How to run (Andy)

```text
cd C:\Users\Zhibin Ren\jx3-ani-player
Start JX3 Ani Player.bat
  or  .venv\Scripts\python.exe player.py
```

FBX viewport: ensure `:8765` serves `viewport_fbx` with `samples/` reachable.

---

## Team notes at handoff

| Role | Status |
|------|--------|
| Dev1 | Catalog playable rows — done |
| Dev2 | local_matrices API — landed; may still need rest-pose math with Dev4 |
| Dev3 | chrome prefers local_matrices — landed on Andy |
| Dev4 | P0 owner — rest-pose / map-viewer matrix space |
| Dev5 | Holding SOURCES until P0 good mid-clip |
| Handoff agent | Refresh these docs when Lead says stop |

---

## First task for next team

1. Diff map-viewer actor-animation-player bone/matrix application vs `fbx_actor.apply_pose` + `fbx_viewport.js` `decompose`.
2. Produce one clean mid-clip 风来吴山·蓄力 screenshot on 花萝 FBX.
3. Then SOURCES + chrome merge.

---

## SFX status addendum — 2026-09-21

The current active work is the red-HD FLWS SFX path documented in
`HANDOFF_SFX.md` and `SFX_RUNTIME_STATUS.md`. Use:

```text
/web/fbx_viewport.html?model=box&sfx=flws
```

The SFX-only viewport skips the FBX payload, places the range effect
horizontally on the ground, renders the animated center layers, and advances
the measured event window automatically. The FBX character remains available
with `model=fbx`. Mesh/ribbon rendering is still an approximation pending
MovieEditor parity.
