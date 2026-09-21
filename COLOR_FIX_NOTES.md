# FBX albedo color fix (lime/orange → normal skin/cloth)

## Symptom
Ani Player FBX viewport showed psychedelic lime/orange albedo on 花萝 even when the
HUD said `textured (map-viewer preset)`. Embedded FBX texture refs include
`*_MRE.png` / `*_TangentSpace_Normal.png` / Specular; with `FBXLoader.setResourcePath(tex/)`,
those files bind into `material.map` and look like neon lime/orange.

## Map-viewer lesson (source of truth)
From `prepareAnchorRigMaterials` / actor-viewer `prepareMaterials`:

1. `renderer.outputColorSpace = THREE.SRGBColorSpace`
2. Per mesh material: zero `emissive` when there is no `emissiveMap`; `DoubleSide`;
   raise `alphaTest` when transparent / zero.
3. **Override `material.map` only** with `{materialName}_Diffuse.png` (or `.tga`)
   from the actor `tex/` folder via `TextureLoader`, with
   `tex.colorSpace = THREE.SRGBColorSpace` (and `flipY = false` for FBX UVs).
4. After override: `material.color.setHex(0xffffff)` so tint does not multiply albedo.
5. Also set `material.map.colorSpace` / `emissiveMap.colorSpace` to
   `THREE.SRGBColorSpace` on any leftover embedded FBX textures.
6. **Never** bind Normal / MRE / Specular into `material.map`.

Lookup matches map-viewer `findPresetTextureFile`: lowercase
`{materialName}_Diffuse.png|.tga` against a tex-directory file list
(`/api/f1_hualuo_textures` or `diffuse_manifest.json`).

## Extra Ani Player gotcha
Do **not** call `loader.setResourcePath(texBase)` before load for this asset.
FBX lists MRE/Normal next to Diffuse; resourcePath makes the loader attach the wrong
files to `material.map`. Skip resourcePath and only load `*_Diffuse` in the prepare
pass (404s for MRE/Normal during FBX parse are expected and harmless).

Also clear emissiveMap / metalnessMap / roughnessMap when they point at `*_MRE.png`.

## Files touched
- `web/fbx_viewport.js` — full prepare pass after FBX load; no setResourcePath.
- `viewport_fbx/index.html` — same Diffuse-only + sRGB + white albedo rules.
- `web/diffuse_manifest.json` — Diffuse filename list for standalone http.server.
- `COLOR_FIX_NOTES.md` — this note.

## Follow-up 2 — lime/orange with Diffuse correctly bound

Even when `material.map` URL is the right `*_Diffuse.png`, FBXLoader can leave a
wrong image in `material.normalMap`. MeshPhong then lights that into neon green/orange
albedo. MeshBasic (no normals) looked correct.

Fix: clear `normalMap` after load, then optionally rebind `*_TangentSpace_Normal.png`
with `THREE.NoColorSpace` (linear), matching map-viewer.
