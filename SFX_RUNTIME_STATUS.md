# SFX runtime status

Updated: 2026-09-21 PT

> **HARD RULE (2026-09-21):** Map-viewer is banned as reference and as a data
> source for converted artifacts. See `SFX_GROUND_RULES.md` and `PSS_FORMAT.md`.
> The native path below replaces the old map-viewer `analysis_url` dependency.

## Native data path (current)

```text
GATA .tani ──tani.parse_tani──▶ PSS logical paths
  ──find staged raw .pss──▶ pss.parse_pss
       ├─ binary header/TOC (PAR)
       └─ PACK zip → PSEF DataStorage XML (MovieEditor spec)
            ├─ emitters: Name, ID, DurationTime, DelayTime, BlendType,
            │            FaceType, MotionType, ParticleCreateMode, ...
            ├─ modules: Emitter Shape, Emitter Spawn, Particle LifeTime,
            │            Particle Size, Particle Material, Particle Type,
            │            Particle Color LifeTime, ... with typed keyframes
            └─ resources: material .def, .tga texture slots (labeled), .Mesh
  ──pss_assets: official PakV4SfxExtract──▶ assets/sfx/extract (raw originals)
```

- `pss.py` — parser; `python pss.py <file> --json out.json` (add `--summary`
  to skip module keyframe trees).
- `pss_assets.py` — staging with provenance per effect under
  `assets/sfx/extract/manifests/<name>.json`; `.tga` logical paths resolve to
  the engine's stored `.dds` bytes via the official tool.
- `sfx_runtime.py` — builds `/api/sfx/flws` payload from the above only.
- `fbx_actor.py` — serves `/api/sfx/asset?path=` and
  `/api/sfx/mesh?path=` (HSEM geometry via `mesh.py`).

## Verified

```text
python smoke_sfx_runtime.py
native PSS smoke PASS: events=2 blade_emitters=20 range_emitters=5
staged_assets=14 timing_source=pss-emitter-authored
```

- Blade `c_藏剑刀光01b红色.pss`: 41 blocks, 20 emitters.
  - 7 emitters carry authored `.Mesh` (大剑转动 / 转刀光a / 转刀光b / 龙卷风圈01).
  - 24 textures, 7 materials; 35/35 assets staged (32 DDS, 9 jsondef, 4 HSEM).
- Range `c_藏剑风车范围_红色.pss`: 11 blocks, 5 emitters, no authored mesh,
  11 textures, 3 materials; 14/14 assets staged.

## Known unresolved (no invented data)

- `Particle Type Mesh` emitters whose authored mesh string is empty: no mesh is
  rendered until the spec for that case is decoded (not faked with rings).
- Emitter size/color curve → renderer mapping is only partially consumed; the
  full module trees are parsed and available in `pss.py` JSON.
- Mesh animation (.ani) for PSS meshes is not yet decoded/played.
- Visual ground truth remains MovieEditor/game client; companion output is
  labeled native-decode, not parity.
