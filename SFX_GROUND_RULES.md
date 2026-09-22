# SFX ground rules — project-wide, do not violate

Recorded 2026-09-21 by request of Andy.

## 1. Map-viewer is NOT a reference

The JX3 web map-viewer and anything it produced is **banned as a reference or
ground truth** for this companion player:

- Do not use map-viewer screenshots, parsed JSON (`/api/pss/analyze`), or its
  PSS/mesh/texture decoding output as the correctness bar.
- Do not copy map-viewer converted artifacts into this repo:
  `_mesh-glb/*.glb`, `_assets` conversions, placeholder textures, cached JSON.
- Do not port map-viewer heuristics as the decoder. That heuristic layer is
  where the FLWS work went wrong.

## 2. Allowed inputs

- **Original raw game resources only**: `.pss`, `.mesh`, `.mesh.ani`, `.ani`,
  `.tani`, `.jsondef`, `.jsoninspack`, `.tga`/`.dds` exactly as shipped.
- Official Seasun tools may be used to extract them (`PakV4SfxExtract.exe`
  etc.), with outputs staged under our own tree and documented in SOURCES.
- Engine binaries (`KG3D_*.dll`, MovieEditor `bin64`) are valid **evidence** for
  format semantics; cite symbol/RVA.

## 3. Ground truth for visuals

- A visual only counts as GT when it comes from the **game client** or
  **MovieEditor** playing the same asset, captured with a documented path.
- Until that exists, label companion FX output as "native decode, not yet
  visually verified" — never "parity".

## 4. Consequence for the current code

- `sfx_runtime.py` / `sfx_layer.js` currently depend on the optional map-viewer
  service (`http://127.0.0.1:3015`, `analysis_url`, `rawUrl`). This dependency
  must be removed and replaced by our own decoder over raw bytes.

## 5. Decoder rules

- Parse from the actual bytes; every field must be justified by structure or
  engine evidence.
- No guess-scoring, no "closest texture by name", no procedural stand-ins
  presented as authored data. If a field cannot be decoded yet, mark it
  `unresolved` and leave it out of the render rather than inventing it.
