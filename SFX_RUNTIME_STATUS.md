# SFX runtime status

Updated: 2026-09-21 PT

## What MovieEditor does

The observed MovieEditor data path is:

```text
GATA .tani
  -> player .ani plus PSS/SFX references
  -> MovieEditor/KG PakV4 resolution
  -> PAR/PSS emitters, materials, textures, and mesh effects
  -> actor/socket attachment
  -> timeline playback
```

The exact red-HD FLWS sample from the MovieEditor screenshot is:

- TANI: `samples/player/moves/f1_f1s07cj重剑技能15_风来吴山红色hd.tani`
- body ANI: `data\source\player\f1\动作\f1s07cj重剑技能15.ani`
- PSS: `data\source\other\HD特效\技能\Pss\发招\c_藏剑刀光01b红色.pss`
- PSS: `data\source\other\HD特效\技能\Pss\状态\c_藏剑风车范围_红色.pss`
- measured PSS start: `2000ms`
- measured PSS play duration: `5000ms`
- measured timeline duration: `8000ms`

The exact red PSS bytes were decoded from CDN HPKG packages `bps6l6gzkjw6r` and `jzwoyc7yazqta`; both have `PAR\0` magic. The PSS analysis service reports 20 sprite/20 mesh emitters plus 22 textures for the blade effect, and 5 sprite/5 mesh emitters plus 6 textures for the range effect.

## Implemented vertical slice

`sfx_runtime.py` now discovers the TANI event, resolves the staged real PSS file, audits its referenced assets, and serves `/api/sfx/flws` from the existing FBX viewport server.

The browser viewport now:

1. Loads the real GATA/PSS timeline metadata.
2. Queries the optional local map-viewer cache service for decoded emitter metadata and texture URLs.
3. Keeps the `发招/刀光` event on `bip01_r_hand` while placing the `状态/风车范围` event at actor-root ground space.
4. Uses the measured `2000ms → 7000ms` event window.
5. Renders the range event as one authored cirque edge ring, three animated center layers using the authored aura/light/noise textures, one sphere-based ground glow layer, and five procedural ribbon rings.
6. Applies decoded color curves/material tints and the red-HD fallback tint to the separated blade event.
7. Keeps the body animation and SFX timeline separate, because the retargeted body clip is shorter than the 8-second PSS timeline.

Both `web/fbx_viewport.js` and `viewport_fbx/fbx_viewport.js` remain byte-identical, with matching `sfx_layer.js` copies.

## Verification

The local viewport was opened with:

```text
http://127.0.0.1:<port>/web/fbx_viewport.html?model=box&sfx=flws
```

Observed browser state:

```text
SFX ready · blade 20 sprites · range 1 ring + 3 center + 1 glow + 5 ribbons
anchors: range actor-root · blade bip01_r_hand
timeline: start 2000ms, duration 5000ms, total 8000ms
```

In SFX box mode, the player uses an independent looping clock over the measured
event duration so the effect starts immediately. The FBX path remains available
with `model=fbx`, but is not needed for SFX-only investigation.

## Known limitation

MovieEditorHD itself still cannot be driven unattended: its custom DX UI exposes no usable UIA tree, and the prior cursor/synthetic-input attempt failed. Therefore the current browser screenshot proves the real PSS paths, timing, decoded emitter metadata, separated placement conventions, and cache textures are wired into the companion, but it is not yet a MovieEditor visual-parity proof.

The range mesh emitters use procedural ribbon rings because their authored track/mesh geometry is not yet decoded. The blade mesh/track emitters and exact MovieEditor socket binding remain open parity work.
