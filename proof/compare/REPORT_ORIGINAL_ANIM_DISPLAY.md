# Report: How the original stack displays character animation

Date: 2026-09-20 PT  
Scope: JX3 map-viewer + MovieEditor + game Pakv4 (observe-only on Andy’s install)  
Goal: Trace the *original* display path for 花萝 actions (走路 / 跳跃 / 风来吴山), not our companion workarounds.

---

## Executive summary

There is **not one** “engine path.” On your PC we see **three different original pipelines**:

| # | Tool | What you see | What actually drives the body |
|---|------|--------------|-------------------------------|
| A | **Map-viewer Actor Viewer** | Textured 花萝 walking/jumping | **FBX embedded clips** → Three.js `AnimationMixer` |
| B | **Map-viewer Animation Player** | Character + skill VFX | Character stays mostly **bind pose**; `.ani`/`.tani` drive **PSS effects**, not bones |
| C | **Live game / Pakv4** | In-game 风来吴山 | Official `.Ani` in **Pakv4 VFS** on the **game skeleton** (not MovieEditor FBX) |

Walk/jump worked for us because we copied **path A**.  
风来吴山 is hard because it lives as **MIN2 `.ani` (path C catalog)** with **no MovieEditor FBX clip export** like 走路 — so path A has nothing to load, and shoving MIN2 onto the FBX (path A’s mesh) needs retarget.

---

## Path A — Map-viewer Actor Viewer (skeletal GT for walk/jump)

**Code:** `jx3-web-map-viewer/public/js/actor-viewer.js`

1. `FBXLoader` loads an actor export (e.g. 花萝 skin FBX).
2. If that FBX (or a *clip-source* FBX) has `root.animations`, they are stored as a clip library.
3. `THREE.AnimationMixer(root)` + `mixer.clipAction(clip)` plays the take (often named `seasun animation`).
4. Map-viewer **switches clip source**: keep 花萝 skin, load 走路.fbx / 跳跃N.fbx only for their embedded animations.

**Evidence on disk:**
- `MovieEditor/source/fbx/走路/花萝走路.fbx`, `跳跃1–3/*.fbx`, `花萝/花萝无动作.fbx`
- Staged copies: `jx3-ani-player/samples/actor_presets/f1_hualuo/mapviewer_clips/hualuo_walk.fbx` etc.

**This is the original way walk/jump look correct on the pretty mesh.**

---

## Path B — Map-viewer Animation Player (NOT skeletal driver)

**Code:** `public/js/actor-animation-player.js`

1. `ensurePlayerAnchorRig` loads player FBX → **bind pose** + upright/floor presentation.
2. Selecting a skill/action parses `.tani` / `.ani` via `/api/player-anim/tani-parse`.
3. Timing comes from ani headers; playback drives **PSS / effect meshes** (GLBs) with their own `AnimationMixer`.
4. **No** writes of player MIN2 tracks into `bone.position` / `bone.quaternion` for the body.

**Implication:** Do not treat Animation Player as the reference for “how `.ani` moves 花萝 bones.” It mostly does VFX on a posed anchor.

---

## Path C — Game engine / Pakv4 (official 风来吴山 data)

1. Catalogs (`Ani.rt`, player anim tables) list logical paths like `data\source\player\f1\…\*.Ani`.
2. Those files are **not loose on disk**; they live in **`C:\SeasunGame\Game\JX3\Pakv4\`** and are opened through engine VFS (`KG_FileSys` / PakV4).
3. Runtime applies skeletal MIN2 (player-action tag) onto the **in-game bip skeleton** that matches the client mesh — same family as our stick FK decode (`min2.py`), **not** automatically the MovieEditor multi-mesh FBX graph.

**What we already extracted (usable bytes):**
- 蓄力: `samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani` (MIN2, 182 bones × 51 @ ~33fps)
- 释放: `samples/player/moves/fenglaiwushan/f1s07cj重剑技能15.ani`
- Related `.tani` wrappers (must resolve to skeletal `.ani`, not fed raw as skel)

**What does *not* exist:** any MovieEditor/map-viewer **FBX with embedded 风来吴山** (Clint searched ~102 Seasun FBXs).

---

## Why our companion had two different outcomes

```
走路/跳跃  → Path A FBX clips → Mixer on 花萝 FBX     → PASS (same as map-viewer Actor Viewer)
风来吴山  → only Path C MIN2 .ani → retarget onto FBX → needs helper/axis (no Path A asset)
```

Original engine never had to “MIN2 → MovieEditor FBX” for FLWS in the tools you use for walk; the game uses its own skel, and MovieEditor never exported an FLWS clip FBX on your machine.

---

## Contract (if applying MIN2 onto FBX yourself)

From map-viewer port analysis (`MAPVIEWER_BONE_CONTRACT.md`):

1. Use **parent-local** TRS only (world matrices into bones = shred).
2. Quats **xyzw**; children usually keep rest/bind translation.
3. Bone name normalize (spaces vs underscores); FBX has **duplicate bone names** across meshes — name-only maps are unsafe.
4. Upright correction on an orientation root, not every bone.

---

## Bottom line for Andy

| Question | Answer |
|----------|--------|
| How does original stack show walk/jump on pretty 花萝? | **Actor Viewer: FBX + embedded clips + AnimationMixer** |
| How does Animation Player use `.ani`? | Mostly **PSS/VFX timing**, body stays bind |
| How does the game show 风来吴山? | **Pakv4 `.Ani` on game skeleton** |
| Do we have FLWS animation files? | **Yes — MIN2 `.ani`** |
| Do we have FLWS the *same way* as walk FBX? | **No — no clip FBX export found** |

Current companion: walk/jump = Path A; FLWS = Path C data retargeted onto Path A mesh (Thor smoked 蓄力 PASS with known ribbon stretch).

---

*Sources: live `actor-viewer.js` / `actor-animation-player.js`, `MAPVIEWER_BONE_CONTRACT.md`, `MAPVIEWER_FLWS_FBX_CLIPS.md`, R6 Pakv4 locate notes, staged `mapviewer_clips/`.*
