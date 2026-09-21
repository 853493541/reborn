# JX3 Ani Player — HANDOFF

**Audience:** Cold start for the next team.  
**Written:** 2026-09-20 ~03:57 PT from box disk only (post Dev Lead stop).  
**Stop evidence:** `STOP_SUMMARY.md` + `proof/compare/STOP_SUMMARY.md` (identical, stamped ~03:56 PT).  
**Evidence roots:** `/workspace/jx3-ani-player` (this tree), `/workspace/jx3-movie-editor-research`, `/workspace/map-viewer-port`.  
**Lead stopped:** Dev Lead stopped for usage/context; pick up from STOP_SUMMARY priority order.  
**Andy:** `C:\Users\Zhibin Ren\jx3-ani-player` (machineId `3bd32f7d-8a1e-43b8-b5fd-2e413ab6ffde`). This write is **box-only**; parent CopyFromBox to Andy.

Confidence labels used below: **HIGH** = STOP_SUMMARY + file on disk; **MED** = code + docs, not re-run this turn; **LOW** = historical doc only.

---

## 1. Goals

From `README.md` + current proofs (`REAL_ASSETS.md`, `UI_REFERENCE_MAP_VIEWER.md`, `proof/compare/SOURCES.txt`):

| Priority | Goal | Source |
|----------|------|--------|
| Core | Free player for Clint’s FbxCmd **MINA** `.mesh.ani` samples (not PakV4 packs) with Natasha R4b companion chrome (body chips, catalog, transport) | `README.md` |
| Current bar | Play **textured F1 花萝 FBX** (map-viewer materials) with skills such as **风来吴山·蓄力 / 释放** | `REAL_ASSETS.md` pivot; `proof/compare/SOURCES.txt` |
| **P0 gate** | Mid-clip 花萝 pose must match **map-viewer Animation Player** (humanoid charge, no explode) | `STOP_SUMMARY.md` |
| UI | Chrome lookalike vs map-viewer Player Animation Browser (no maps / no Wwise) | `UI_REFERENCE_MAP_VIEWER.md` |
| Not success | Blue `mesh.py` RQ LBS as character parity; maps; terrain; Wwise; hashing explode PNGs into SOURCES | `SOURCES.txt` OUT_OF_SCOPE; STOP_SUMMARY Do not |

Older research goal gap (`jx3-movie-editor-research/STATUS_CONTINUE.md`): play animations while actors execute moves; real player `.ani` often PakV4-only; plot MIN2 tree missing on Andy.

---

## 2. Machines / paths (verified)

| Role | Path | Confidence | Notes |
|------|------|------------|-------|
| **Box repo (canonical for this handoff)** | `/workspace/jx3-ani-player` | **HIGH** | Present; STOP_SUMMARY + proofs checked here |
| **Andy (Windows)** | `C:\Users\Zhibin Ren\jx3-ani-player` | **HIGH** | machineId `3bd32f7d-8a1e-43b8-b5fd-2e413ab6ffde` — path from STOP_SUMMARY; chrome `_sync_fbx_pose` local prefer landed on Andy. Re-diff before treating Andy as canonical. |
| **Research sibling** | `/workspace/jx3-movie-editor-research` | **HIGH** | `STATUS_CONTINUE.md`, `legacy-map-viewer/`, `samples/actors` |
| **Map-viewer UI port** | `/workspace/map-viewer-port/` | **HIGH** | `actor-animation-player.js`, `player-anim-loaders.js` — **GT for P0 pose** |
| **MovieEditor (Andy)** | `C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor` | **LOW–MED** | From `STATUS_CONTINUE.md` only |
| **Staging (Andy)** | `%USERPROFILE%\jx3-staging` | **MED** | SeasunGame blocked from direct CopyToBox — stage first |

**Sync note:** Box has newer local/retarget pose proofs (~03:55–03:57 PT) after local_matrices land. Treat box as ahead until Andy is re-diffed. App start fix on Andy: sync `resolve_playable.py` + `pose_drive.py` if missing (STOP_SUMMARY).

**Samples alternate:** `player.py` falls back to `/workspace/jx3-movie-editor-research/samples` if `--samples` dir missing (`player.py` `main()`).

---

## 3. How to run

### 3.1 Venv + Tk player

```bash
cd /workspace/jx3-ani-player
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
# requirements.txt: matplotlib>=3.8, tkinterweb>=4.0
.venv/bin/python player.py
# optional:
.venv/bin/python player.py --samples /workspace/jx3-movie-editor-research/samples
.venv/bin/python player.py --character-mode fbx    # default: Three.js 花萝
.venv/bin/python player.py --character-mode mesh   # RQ LBS matplotlib fallback
.venv/bin/python player.py --all-clips             # full catalog (default: 风来吴山 focus)
```

Andy: `Start JX3 Ani Player.bat` or `.venv\Scripts\python.exe player.py` (STOP_SUMMARY). Box may need `.venv` created before first run.

Playback timing: clip header fps via `transport.PlaybackClock` (wall-clock).

### 3.2 Locked FBX viewport `:8765`

```bash
cd /workspace/jx3-ani-player/viewport_fbx
# once: npm install   # three@^0.160 (package.json)
python3 -m http.server 8765
# open e.g.:
# http://127.0.0.1:8765/index.html?fbx=./samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx&tex=./samples/actor_presets/f1_hualuo/tex/
```

`viewport_fbx/samples` → symlink to `/workspace/jx3-ani-player/samples`.  
`player.py` `_init_fbx_viewport` prefers this locked URL; else `fbx_actor.paint_to(..., "webview")` (ephemeral port).

### 3.3 `web/` / helper serve + proof PNG

```bash
cd /workspace/jx3-ani-player
.venv/bin/python player_fbx_viewport.py --serve
.venv/bin/python player_fbx_viewport.py --proof proof/compare/aniplayer_f1_hualuo_closeup.png
```

### 3.4 Smokes

```bash
.venv/bin/python smoke_transport.py   # Dev3 PlaybackClock
.venv/bin/python smoke_pose_drive.py  # FLWS 蓄力 → mid-frame → apply_pose → pose JSON
# MINA quick:
.venv/bin/python -c "from mina import load_mina; print(load_mina('samples/f1_2227_body_hd.mesh.ani').bone_count, load_mina('samples/f1_2227_hand_hd.mesh.ani').bone_count)"
# expected after B1b: 33 57
```

### 3.5 Ports

| Port | What | Confidence |
|------|------|------------|
| **8765** | Locked `viewport_fbx/` | **HIGH** |
| **ephemeral** | `fbx_actor.ensure_server` | **HIGH** |
| **~3015** | Map-viewer ref (SeasunDownloader `jx3-web-map-viewer` Animation Player) | **MED** — STOP_SUMMARY |

---

## 4. Architecture

```
catalog row (playable_path / .tani)
        → resolve_playable.resolve_playable_path  → skeletal .ani (MIN2)
        → min2.load_min2_stick  → Min2SkelClip
        → transport.PlaybackClock
        → pose_drive.tick_pose  → sample.local_matrices (+ bone_names)
                                  sample.matrices only for Mesh RQ LBS
        → fbx_actor.apply_pose  → web/runtime/pose.json
                                 + web|viewport_fbx/runtime/pose_by_name.json
        → chrome _sync_fbx_pose prefers local_matrices
        → Three.js poller (fbx_viewport.js) → __applyPose
```

| Module | Path | Role |
|--------|------|------|
| Player UI | `player.py` | Tk R4b chrome, chips, scrub, FBX embed / mesh fallback; `_sync_fbx_pose` prefers `local_matrices` |
| Transport | `transport.py` | `PlaybackClock` wall-clock play/pause/seek/tick |
| Catalog | `catalog.py`, `catalog_resolve_patch.py` | Anim tables → `samples/player/catalog/` |
| MINA | `mina.py` | FbxCmd `.mesh.ani` (B1 body/hand) |
| MIN2 | `min2.py` | `local_matrices_at` (FBX) + world `matrices_at` (LBS) |
| Tani | `tani.py` | GATA `.tani` → referenced `.ani` paths |
| Mesh LBS | `mesh.py` | HD `.mesh` + RQ matrix skinning (fallback viz) |
| Resolve | `resolve_playable.py` | Row/tani → playable MIN2 `.ani` |
| Pose glue | `pose_drive.py` | `load_clock_for_row`, `tick_pose` — FBX path uses **local** matrices |
| FBX actor | `fbx_actor.py`, `actor_fbx.py` | 花萝 preset, HTTP, `apply_pose` |
| Web viewport | `web/fbx_viewport.{html,js}`, `web/runtime/` | Primary Three.js page + pose JSON |
| Locked viewport | `viewport_fbx/{index.html,fbx_viewport.js,README.md}` | `:8765` host; **JS must stay ≡ `web/`** |
| Samples | `samples/` | MINA roots; `mesh/`; `player/{moves,catalog,actors}`; `actor_presets/f1_hualuo/` |
| Proofs | `proof/`, `proof/compare/` | Stick/skinned/FBX/UI compare artifacts + SOURCES + STOP_SUMMARY |

**JS identity (verified ~03:57 PT):** `web/fbx_viewport.js` md5 `ad1b0741ab5e52303f32e70087617349` ≡ `viewport_fbx/fbx_viewport.js`. Edit both together.

**Code contract (STOP_SUMMARY, smoke green):** FBX uses **local** `sample.local_matrices` / `local_matrices_at`; world `sample.matrices` only for Mesh RQ LBS.

---

## 5. Done vs in-flight

*Source of truth for this section: `STOP_SUMMARY.md` (~03:56 PT). Older HANDOFF claims that mid-clip FLWS PNGs were success proofs are **superseded** — those frames are junk/explode.*

### Done (keep) — HIGH

| Area | Evidence |
|------|----------|
| Map-viewer material parity on 花萝 FBX (Diffuse/sRGB/normals/Specular) — peach cloth/skin | STOP_SUMMARY; `COLOR_FIX_NOTES.md` |
| Face: hide dark `head_hd`/`hat` veils; peach face | `proof/compare/aniplayer_face_v3.png` |
| Sleeves: hide cyan hand/body FX cards + glove cutout | `proof/compare/aniplayer_sleeves_v1.png` |
| Catalog: F1 花萝 / 风来吴山 rows → playable `.ani` | `samples/player/catalog/…`, `resolve_playable.py` |
| Transport: Play/Pause/Seek + `pose_drive` / `PlaybackClock` | `transport.py`, `pose_drive.py`, smokes |
| Bone **name** map 182/182 | `proof/fbx_ani_bone_map.json` (**keep**) |
| Code contract: FBX **local** matrices; world matrices for Mesh RQ LBS only | `min2.py`, `pose_drive.py` — smoke green |
| Chrome `_sync_fbx_pose` prefers `local_matrices` | Dev3, landed on Andy (STOP_SUMMARY) |
| HANDOFF + Handoff agent for cold pickup | this file; `proof/compare/HANDOFF_STATUS.md` |
| App start fix on Andy | sync `resolve_playable.py` + `pose_drive.py` if missing |
| Earlier foundations (still valid) | B1/B1b MINA, MIN2 sticks, Dev3 clock, RQ LBS FLWS mesh path, lookalike chrome **proofs** (live merge still open) |

### NOT done — do next (priority order) — from STOP_SUMMARY

| Pri | Item | Status / notes |
|-----|------|----------------|
| **P0** | **Moving 花萝 pose still wrong (BLOCKS action)** | Mid-clip 风来吴山·蓄力 still **explodes** even after local-matrix switch. Ignore junk: `aniplayer_f1_hualuo_flws_f0{00,25,50}.png`. Also still bad: `*_local_f*.png`, `*_retarget_f*.png`, `aniplayer_flws_pose_f000.png` (head float / shatter). **Map-viewer Animation Player shows this action correctly — match that path.** Hypothesis residual: rest/bind multiply, parent-space vs bone.local, scale, or viewport `transpose`/`decompose`. **Exit:** mid-clip PNG of a recognizable charge pose (humanoid, no spikes); then Dev5 SOURCES vs map-viewer. |
| **P1** | Bind-pose hands float | Sleeves/face OK but wrists/hands detached in bind — likely same skinning/bind issue; fix with P0 or separately |
| **P1** | Merge lookalike chrome into live Tk window | Lookalike proofs exist; live window merge + fresh both-app screenshots still open (`todo 6`) |
| **P2** | Dev5 SOURCES | Hold until P0 good mid-clip lands; then hash into `proof/compare/` + side-by-side map-viewer |
| **P2** | Keep JS copies synced | `web/fbx_viewport.js` ≡ `viewport_fbx/fbx_viewport.js` |

### Recent notable mtimes (box, PT, excl. node_modules)

| Time (PT) | Files |
|-----------|-------|
| ~03:55–03:57 | `*_local_f*.png`, `*_retarget_f*.png`, `aniplayer_flws_pose_f*.png`, STOP_SUMMARY |
| ~03:00 | Earlier `aniplayer_f1_hualuo_flws_f000/025/050.png` (junk — do not treat as pass) |
| ~02:58–02:59 | sleeve proofs, bone map JSON, closeup |
| ~02:54–02:55 | `player.py`, `pose_drive.py`, `resolve_playable.py` |

---

## 6. Code hotspots

| Concern | Files |
|---------|-------|
| Playback UI / scrub / chips / local pose sync | `player.py` (`AniPlayer`, `_sync_fbx_pose`, `_init_fbx_viewport`) |
| Clock API | `transport.py` |
| Catalog lists / playable_path | `catalog.py`, `catalog_resolve_patch.py`, `samples/player/catalog/*` |
| Skinning (LBS fallback) | `mesh.py` (`skin_positions`, bind matrices) |
| MINA / MIN2 / tani bytes | `mina.py`, `min2.py` (`local_matrices_at`), `tani.py` |
| Resolve + pose publish | `resolve_playable.py`, `pose_drive.py` |
| FBX load / pose / HTTP | `fbx_actor.py`, `actor_fbx.py`, `player_fbx_viewport.py` |
| Materials / cutout / cyan kill | `web/fbx_viewport.js` ≡ `viewport_fbx/fbx_viewport.js` (`prepareAnchorRigMaterials`, `disableIfDarkCutout`, `disableIfCyanFxCard`) |
| **P0 residual math** | rest/bind multiply, parent-space vs `bone.local`, scale, viewport `transpose`/`decompose` — verify vs map-viewer `actor-animation-player` |
| Bone orthography | `proof/fbx_ani_bone_map.json` (rule: lowercase + spaces/hyphens → underscores) |

---

## 7. Do / Don’t

### Do

- Keep `web/fbx_viewport.js` and `viewport_fbx/fbx_viewport.js` byte-identical after edits
- Follow `COLOR_FIX_NOTES.md` (Diffuse-only albedo, sRGB, white tint, no wrong maps in `material.map`)
- Resolve `.tani` → skeletal `.ani` before MIN2 (`resolve_playable`)
- Prefer textured FBX proofs over blue LBS for character gates
- Drive FBX with **local** matrices; use world matrices only for Mesh RQ LBS
- Match mid-clip pose to **map-viewer Animation Player** (GT)
- Stage Seasun assets via `%USERPROFILE%\jx3-staging` on Andy
- Rebuild catalog with `python catalog.py && python catalog_resolve_patch.py` when lists change
- Update `proof/compare/SOURCES.txt` only after a **good** mid-clip (humanoid, no spikes)
- Refresh this HANDOFF + `proof/compare/HANDOFF_STATUS.md` on stop

### Don’t

- Don’t hash explode PNGs into SOURCES (`aniplayer_f1_hualuo_flws_f0{00,25,50}.png`, `*_local_f*`, `*_retarget_f*`, bad `aniplayer_flws_pose_*`)
- Don’t feed `.tani` into MIN2 — resolve to skeletal `.ani` first
- Don’t re-enable dark hat/head or cyan hand FX cards without alpha-aware path
- Don’t bind Normal / MRE / Specular into `material.map`
- Don’t call `loader.setResourcePath(texBase)` for 花萝 (wrong albedo attach)
- Don’t claim `mesh.py` RQ LBS as map-viewer character parity
- Don’t treat `mv_f1_zoom_char.png` as textured GT close-up
- Don’t invent Andy paths beyond what’s documented; verify with machineId Shell before claiming sync

---

## 8. Verification checklist

```bash
cd /workspace/jx3-ani-player
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt

# A. Transport
.venv/bin/python smoke_transport.py

# B. Pose drive → runtime JSON (local_matrices path)
.venv/bin/python smoke_pose_drive.py
test -f web/runtime/pose.json && test -f viewport_fbx/runtime/pose_by_name.json

# C. Textured close-up (materials / face / sleeves — already landed)
.venv/bin/python player_fbx_viewport.py --proof proof/compare/aniplayer_f1_hualuo_closeup.png
# Check: peach face, no cyan sleeve cards, no neon lime/orange
# Refs: proof/compare/aniplayer_face_v3.png, aniplayer_sleeves_v1.png

# D. Locked viewport
(cd viewport_fbx && python3 -m http.server 8765) &
# open index.html with fbx=&tex= query; confirm textured 花萝

# E. Full player
.venv/bin/python player.py
# F1 → 风来吴山·蓄力 (first_bar) → Play; watch FBX move if :8765 up

# F. P0 gate (OPEN) — mid-clip must be recognizable charge pose, humanoid, no spikes
# Compare against map-viewer Animation Player (~3015 / actor-animation-player).
# Do NOT treat as pass:
#   proof/compare/aniplayer_f1_hualuo_flws_f000.png
#   proof/compare/aniplayer_f1_hualuo_flws_f025.png
#   proof/compare/aniplayer_f1_hualuo_flws_f050.png
#   proof/compare/*_local_f*.png
#   proof/compare/*_retarget_f*.png
#   proof/compare/aniplayer_flws_pose_f000.png  (and peer pose dumps — head float/shatter)

# G. Keep bone map
test -f proof/fbx_ani_bone_map.json   # 182/182 — keep

# H. MINA regression
.venv/bin/python -c "from mina import load_mina; b=load_mina('samples/f1_2227_body_hd.mesh.ani'); h=load_mina('samples/f1_2227_hand_hd.mesh.ani'); print(b.bone_count,h.bone_count)"
```

**P0 exit (STOP_SUMMARY):** mid-clip PNG of a recognizable charge pose (humanoid, no spikes). Then Dev5 SOURCES vs map-viewer.

---

## 9. Team roles (STOP_SUMMARY ownership at stop)

| Who | Owns |
|-----|------|
| Dev2 | local_matrices / pose_drive (landed; may still need rest-pose math) |
| Dev3 | chrome `_sync_fbx_pose` (local preferred — landed on Andy) |
| Dev4 | re-dump real mid-clip after pose math fixed; bone map JSON keep |
| Dev5 | SOURCES after good mid-clip |
| Handoff agent | refresh `HANDOFF.md` on stop |

Historical / thinner roster (still useful): Dev Lead (PakV4 gating), Dev1 (catalog), Thor/Clint/Natasha/Tony/Wenjing — see prior smoke/README notes.

---

## Appendix — Key paths quick index

```
STOP_SUMMARY.md  proof/compare/STOP_SUMMARY.md  proof/compare/HANDOFF_STATUS.md
README.md  B1_SMOKE.md  B1b_hand_parse_fix.md  REAL_ASSETS.md
COLOR_FIX_NOTES.md  UI_REFERENCE_MAP_VIEWER.md  requirements.txt
player.py  transport.py  catalog.py  catalog_resolve_patch.py
mina.py  min2.py  tani.py  mesh.py
resolve_playable.py  pose_drive.py  fbx_actor.py  actor_fbx.py
smoke_transport.py  smoke_pose_drive.py  player_fbx_viewport.py
web/  viewport_fbx/  samples/  proof/  proof/compare/
proof/fbx_ani_bone_map.json
```

Related: `/workspace/jx3-movie-editor-research/STATUS_CONTINUE.md`, `/workspace/map-viewer-port/` (map-viewer GT for P0).

---

*Prefer updating this file over chat when status changes. Mirror to Andy when machineId Shell / CopyFromBox is available. Lead stopped ~03:56 PT for usage — next team starts at P0.*

---

## Path C update (2026-09-20 ~19:42 PT)

Real-game display plan + handoff: see **HANDOFF_PATH_C.md** / **PLAN_PATH_C.md**.
Default next: Phase 1 engine-host spike (Steve + Clint). FBX-retarget FLWS = fallback only.

---

## Current SFX addendum — 2026-09-21

For the active FLWS SFX task, use `HANDOFF_SFX.md` and
`SFX_RUNTIME_STATUS.md` as the source of truth. The browser viewport now
supports an SFX-only box mode:

```text
/web/fbx_viewport.html?model=box&sfx=flws
```

This mode skips the FBX payload, renders the range effect horizontally on the
ground plane, includes the animated center layers, and loops the measured SFX
window immediately. Use `model=fbx` only when character rendering is needed.
The remaining visual-parity gap is the procedural approximation of the range
mesh/ribbon emitters.

