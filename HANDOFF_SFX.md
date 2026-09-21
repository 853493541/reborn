# Handoff — SFX / skill “animation” (UPDATED)

**Updated:** 2026-09-21 ~11:33 PT  
**Andy decision:** **Map-viewer is NO LONGER SFX reference** — FX there was wrong / not playable. Need a **new** path to how **game** or **Movie Editor** plays SFX.

Vocabulary:
- **Moves** = body loco/pose (already working in companion)
- **SFX / animation** = skill visuals (PSS, particles, trails, tags) with a move — **current target**

---

## Dead end (do not use as GT)

| Source | Why dropped |
|--------|-------------|
| Map-viewer Animation Player / `pss-renderer.js` | Andy: SFX wrong / unable to play correctly — **not GT** |

Keep map-viewer only for Path A body loco FBX clips if needed — **not** for skill FX.

---

## New SFX strategy — game / Movie Editor first

### Track X1 — Movie Editor as SFX host (primary spike)

**Why:** Same Seasun stack as cinematic/skill preview; binaries present on Andy:
- `MovieEditorLauncher.exe` → `MovieEditorHD.exe`
- `KG3D_AnimationTagX64.dll` (animation tags / FX hooks)
- `AnimationTimeShaft*.dll` (timeline)
- `KG3DEditorAdapterX64.dll`, `KG_EngineEditorX64.dll`
- `FbxTool\particle.dll`

**Spike exit:** In MovieEditorHD, load F1/花萝 (or actor), play a skill/timeline that shows **real SFX**, capture mid-frame. Document exact click/path/args.

**Owner:** Steve lead, Clint on asset/timeline paths.

### Track X2 — Game client as SFX GT (parallel observe)

Capture **client** mid-skill (风来吴山 or any clear SFX skill) as visual GT. Note: observe-only; don’t automate online play.

**Owner:** Thor (screenshots), Clint (which skill / which `.tani`/sfx ids from tables if visible).

### Track X3 — Official SFX extract tools

`PakV4SfxExtract.exe` under client `bin64` — probe whether it dumps playable SFX packs (observe logs/UI; no mass redistribute).

**Owner:** Clint.

### Track X4 — Companion integration (after X1 or X3 works)

Either:
- **Host** MovieEditor/preview for FX while companion plays body moves, or  
- **Replay** extracted FX assets with a minimal viewer once format is known from ME/game — **not** via map-viewer PSS port.

**Owner:** Banner + Natasha after spike PASS.

---

## What we are *not* doing next

1. Porting map-viewer `pss-renderer.js` as GT  
2. More map-viewer tani→PSS “fix” loops  
3. Blocking on Path C body-host unless it directly helps ME SFX spike  

---

## Phases

### Phase X0 — Freeze (this doc)
Map-viewer SFX = dead. GT = MovieEditor and/or live client.

### Phase X1 — MovieEditor SFX smoke (NEXT, 2–3 days)
1. Launch ME HD via known launcher recipe.  
2. Find any skill/effect timeline that shows particles/trails.  
3. Capture PNG + note resource names (pss/sfx/tag).  
4. Write `proof/compare/REPORT_SFX_MOVIEEDITOR_PATH.md`.

### Phase X2 — Client GT frames
Mid-skill client captures for comparison.

### Phase X3 — Extract / reuse
`PakV4SfxExtract` + ME resource paths → stage sample FX bytes next to companion.

### Phase X4 — Product
Companion: body move + SFX layer (host or native), Thor smoke vs client/ME.

---

## Team (reassigned)

| Who | Now |
|-----|-----|
| **Steve** | MovieEditorHD SFX spike lead |
| **Clint** | ME resource paths + PakV4SfxExtract probe + skill id notes |
| **Thor** | Client mid-SFX GT shots when Steve has a skill name |
| **Banner** | Hold map-viewer PSS port; prep host/IPC once X1 PASS |
| **Natasha** | Hold FX UI until we know host vs in-app |

---

## Success (first milestone)

1. One **MovieEditor or client** PNG where skill SFX is clearly correct.  
2. Written path: which exe, which asset, which timeline/tag.  
3. Decision: host ME vs extract+replay.

---

## Key paths

```
C:\SeasunGame\MovieEditor\MovieEditorLauncher.exe
C:\SeasunGame\MovieEditor\bin64\MovieEditorHD.exe
C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe
C:\Users\Zhibin Ren\jx3-ani-player\HANDOFF_SFX.md  ← this file
```

---

*Tony — Map-viewer SFX abandoned; Movie Editor + game are the new crack surface.*

---

## Implementation update — companion SFX vertical slice

The first in-app SFX slice is now wired:

- `sfx_runtime.py` resolves the exact red-HD FLWS GATA/TANI event to the real staged PAR/PSS bytes.
- `fbx_actor.py` serves `/api/sfx/flws`.
- `web/fbx_viewport.js` and `viewport_fbx/fbx_viewport.js` attach an SFX layer to `bip01_r_hand` and use the measured `2000ms` start / `5000ms` play window.
- `web/sfx_layer.js` and `viewport_fbx/sfx_layer.js` consume emitter metadata and cache textures from the optional local map-viewer service.
- Full implementation notes: `SFX_RUNTIME_STATUS.md`.

The body mixer and SFX timeline are intentionally separate: the current retargeted body clip is about 0.5 seconds while the PSS timeline is 8 seconds.

This is a real-data integration smoke, not yet MovieEditor visual parity. The current slice renders the 25 decoded red sprite emitters; 25 red mesh emitters still need a renderer. MovieEditorHD still requires a human UI action to open the plot and provide the final mid-effect comparison frame.

---

## Latest companion SFX update — 2026-09-21

The current SFX-only viewport is now the active path:

- `?sfx=flws` defaults to `model=box`, avoiding FBX and character-texture loading. Use `model=fbx` to restore the character.
- The FLWS range effect is placed on the horizontal ground plane, not as a camera-facing vertical ring.
- The range PSS uses the decoded cirque emitters: one edge ring, three animated center texture layers, one sphere glow, and five procedural ribbon layers.
- Box mode uses an independent looping clock and starts inside the measured FLWS event window immediately. It no longer remains frozen at pose time `0` or waits through the original `2000ms` lead-in.
- Current browser status: `SFX ready · blade 20 sprites · range 1 ring + 3 center + 1 glow + 5 ribbons`.

Verification completed:

```text
python smoke_sfx_runtime.py
SFX smoke PASS: events=2 pss_bytes=224930 textures=22 meshes=3 window=2000..7000ms
```

The browser proof confirmed that box mode advances automatically and that center-layer rotation changes over time. `web/` and `viewport_fbx/` JavaScript copies remain byte-identical.

Known limitation: the five range mesh/ribbon emitters are still procedural approximations. The sprite center/edge layers now use the real red-HD PSS texture and launcher metadata, but this is not yet pixel-identical to the MovieEditor reference image.
