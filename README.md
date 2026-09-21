# Reborn — JX3 Ani Player

Free player for Clint’s FbxCmd **MINA** `.mesh.ani` samples (not PakV4 packs). The UI is polished to Natasha’s R4b companion-player wireframes: body chips, inferred character/clip catalog, viewport, and Fluent dark transport chrome.

## Features
- Load `samples/*.mesh.ani`
- R4b dark companion chrome with F1 / F2 / M1 / M2 body chips
- Inferred character and 动作 Clip lists from sample filenames
- Bone list
- Scrub + Play / Pause / Stop + Loop
- 3D stick / scatter viz (biped edges when names match)

## Run
```bash
cd /workspace/jx3-ani-player
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
.venv/bin/python player.py
# or
.venv/bin/python player.py --samples /workspace/jx3-movie-editor-research/samples
```

Playback timing follows FbxCmd’s 33 ms/frame (~30.3 fps).

## Format
See research notes:
- `jx3-movie-editor-research/R2e_mina_ani_header.md`
- `jx3-movie-editor-research/R2f_mina_track_layout.md` — bone-major `pos3+scale3+quat4+1+extra4`
