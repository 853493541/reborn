# Path C Handoff — Real-game animation display

**Updated:** 2026-09-20 ~19:42 PT (Tony)  
**Product:** `C:\Users\Zhibin Ren\jx3-ani-player`  
**Owner:** Tony (lead) → Steve / Clint / Banner / Natasha / Thor  

## Goal

Reproduce the **real game way** of showing 花萝 + 风来吴山 (and keep loco working):

`Pakv4 MIN2 .Ani → game bip skeleton + game skin → engine-like display`

Not the destination: MovieEditor “无动作” FBX + MIN2 retarget (current 蓄力 PASS is a **fallback**, not Path C).

---

## What already works (do not regress)

| Feature | Path | Status |
|---------|------|--------|
| Textured 花萝 | ME/map-viewer FBX skin | PASS |
| 走路 / 小跳 | Path A: FBX embedded clips + AnimationMixer | Thor UI-click PASS |
| Walk/jump GIF | `proof/compare/hualuo_walk_jump.gif` | Done |
| 风来吴山·蓄力 (looks-like) | MIN2 → helper retarget → Mixer on ME FBX | Thor PASS; ribbon stretch OK for now |
| 释放 list wiring | `clip_flws_cast.json` | Listed; not full client-parity smoke |

Original-path write-up: `proof/compare/REPORT_ORIGINAL_ANIM_DISPLAY.md` (also Desktop).

---

## Three original pipelines (context)

| ID | Stack | Body drive |
|----|-------|------------|
| **A** | Map-viewer Actor Viewer | FBX `root.animations` + `AnimationMixer` (walk/jump GT) |
| **B** | Map-viewer Animation Player | Bind pose + `.ani`/`.tani` → **PSS/VFX**, not body bones |
| **C** | Live client | Pakv4 MIN2 on **game** skeleton + skin ← **target** |

---

## Reuse first (skip reinventing)

Reuse from Andy’s install:

1. **Pakv4 + anim catalogs** (`Ani.rt` / tables) — official clip identity  
2. **Extracted MIN2** 蓄力/释放 `.ani` already on disk  
3. **KG / PakV4 VFS DLLs** (MovieEditor `bin64` / client) — read by logical path  
4. **In-game F1 mesh + bip** — true bind (replaces ME FBX for Path C)  
5. **Seasun preview hosts** (MovieEditorHD via launcher, QModelEditor, any ani preview) — proven skinning/shaders if we can drive them  
6. **Cache LZHAM + GBK** recipe — when official extract crashes  

Keep Path A assets for loco only (`samples/.../mapviewer_clips/hualuo_walk.fbx` etc.).

---

## Strategy (default decision)

Widget choice was **skipped** → default:

**Phase 1 = engine-host spike first (Track 1).**  
If host can play game ani on game body → wrap it (Track 1).  
If not → Track 2: game-skel + our viewer (no ME-FBX retarget as GT).

Track 3: Path A remains for 走路/跳跃 only.

---

## Tracks

### Track 1 — Host real player (preferred if spike passes)

- Our app = catalog + Play → launch/IPC into MovieEditorHD or other Seasun preview that already skins.  
- **Skips:** Three.js skinning, retarget, shader parity.  
- **Risks:** launcher guards, PakV4/V5 init, automating load/play.

### Track 2 — Same game data, our viewer

- VFS-load F1 **game** mesh+skel; apply MIN2 parent-local on that skel; thin desktop shell.  
- **Skips:** retarget. **Doesn’t skip:** mesh/material + a skinning renderer.

### Fallback (current)

- ME FBX + retarget Mixer for FLWS — labeled fallback in UI/docs until Path C ships.

---

## Phases

### Phase 0 — Freeze (done in this handoff)

- GT = client / Seasun preview on **game** assets.  
- Default execution order = host spike → then Track 1 or 2.

### Phase 1 — Engine-host spike (Steve lead, Clint assist) — **NEXT**

**Exit:** one smoke video/PNG: Seasun tool shows F1 body + 蓄力 `.ani` (or equivalent) looking like client.

Checklist:

1. List binaries that can play player `.ani` on body mesh.  
2. Launch recipe (cwd/args/launcher) — reuse `R5c` / launch notes.  
3. Load F1 + play 蓄力 path; capture mid frame.  
4. Go/No-Go: Track 1 vs Track 2.

### Phase 2 — Data plane (Clint, Banner support)

1. VFS read spike for logical ani/mesh paths.  
2. Stage F1 game skel bone list; diff vs MIN2 182-bone set.  
3. Keep using extracted 蓄力/释放; grow library via VFS as needed.

### Phase 3 — Display plane

- **Track 1:** Natasha UI → host play; Thor vs **client** mid.  
- **Track 2:** Banner MIN2 local TRS on **game** skel + skin; Thor vs client mid.

### Phase 4 — Product parity

- Body → Character → 动作: loco = Path A; FLWS = Path C.  
- Smoke 释放; sockets/PSS later; more skills.  
- Optional Pak browse without full dump.

### Phase 5 — Hardening

- Stick ≠ pass; client mid = GT.  
- Docs: real path vs FBX fallback.

---

## Team split

| Who | Owns |
|-----|------|
| **Tony** | Plan / handoff / go-no-go after Phase 1 |
| **Steve** | Phase 1 host spike (MovieEditorHD / preview tools) |
| **Clint** | VFS / Pakv4 / F1 game mesh+skel staging |
| **Banner** | Path C apply on game skel **or** host IPC; freeze FBX-retarget as fallback-only |
| **Natasha** | UI: Path C vs fallback labeling |
| **Thor** | Smokes vs **client** mid frames |

---

## Success criteria

1. Mid-蓄力 matches **client** silhouette (not only “humanoid after retarget”).  
2. Same `.ani` bytes the game uses.  
3. 走路/跳跃 still PASS (Path A).  
4. Handoff states clearly: reused Seasun pieces vs still-faked pieces.

---

## Key paths (Andy)

```
C:\Users\Zhibin Ren\jx3-ani-player\
  HANDOFF_PATH_C.md          ← this file
  PLAN_PATH_C.md             ← same content, plan title
  proof/compare/REPORT_ORIGINAL_ANIM_DISPLAY.md
  proof/compare/hualuo_walk_jump.gif
  proof/compare/thor_flws_charge_mid.png
  samples/.../mapviewer_clips/   ← Path A loco only
  samples/player/moves/...蓄力...ani  ← Path C source bytes
```

Desktop copy: `C:\Users\Zhibin Ren\Desktop\HANDOFF_PATH_C.md`

---

## Explicit non-goals (for now)

- Replacing Path A loco with Path C  
- Full Pakv4 dump / redistribution  
- Treating map-viewer Animation Player VFX as skeletal GT  
- More FBX-retarget R&D as the main line (fallback only)

---

*Tony — 2026-09-20. Default after skipped widget: Phase 1 engine-host spike.*


---
## Update 2026-09-20 ~20:16 PT
Andy priority shift: **SFX/PSS animation** (not body moves). See **HANDOFF_SFX.md**. Path C body-host may continue only if it doesn't block SFX Phase S1.

