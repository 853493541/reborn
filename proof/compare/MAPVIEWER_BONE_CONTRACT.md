# Map-viewer .ani → FBX bone contract

For Banner (MIN2→FBX shreds 花萝 on 走路/跳跃; stick FK OK).
Sources: `_port_from_mapviewer/` + live `jx3-web-map-viewer` on Andy (`SeasunDownloaderV2.4`).
Mid-frame dump: `proof/compare/hualuo_walk_mid_local_trs.json` (花萝 行走, frame 21/43).

## 0) Hard fact: Animation Player does **not** drive skeletal bones from player `.ani`

Live `public/js/actor-animation-player.js`:

| Step | What map-viewer does |
|------|----------------------|
| Character | `ensurePlayerAnchorRig` → FBXLoader actor preset (花萝) + tex; **bind pose** |
| Presentation | `applyPlayerRigPresentation`: upright quat from pelvis→head, then floor-center placement |
| Selection play | `.tani` / `.ani` → `tani-parse` → **PSS VFX** timed by `ani-header` duration |
| Bones | No `bone.position` / `bone.quaternion` writes from MIN2 tracks. `AnimationMixer` only on PSS mesh GLBs |

So map-viewer GT for “looks like 花萝” is **textured FBX bind + sockets**, not a reference implementation of MIN2→bone. Stick FK OK on our side is expected; the missing piece is our FBX apply matching the contract below.

Server `parseMin2AniHeader` only understands **vertex-morph / multi-subrecord** MIN2 (PSS / plot). It does **not** decode player-action skel tag `0xd17d22b0` (i16 quats). That decode lives in our `min2.py`.

## 1) Contract table (what Banner must feed FBX)

| Topic | Map-viewer / engine contract | Our stick path (`min2.py`) | FBX apply (`fbx_actor` / `fbx_viewport`) |
|-------|------------------------------|----------------------------|----------------------------------------|
| Space | Three.js bone TRS = **parent-local** | `local_quats_at` / `local_matrices_at` = parent-local; `quats_at` / `matrices_at` = **world** (LBS only) | Must use **local** only. World → `decompose` = shred |
| Bind × anim | FBX skeleton bind stays; sockets overlay authored Dummy matrix | Bind TRS in skel payload (type=2 or dense 15f); anim quats **parent-local**; FK `world_q = parent_q ⊗ local_q` | Prefer: local R from ani; **t**: root from ani, children = rest (or keep FBX bind t if rotation-only) |
| Quat layout | Three `Quaternion` = **(x,y,z,w)** | i16 tracks `/32767` → **xyzw**; stored same | `decompose` → `bone.quaternion` xyzw |
| Matrix layout | Socket: MovieEditor `Matrix` 16 CSV → `Matrix4.set(m0..m15)` = **row-major** n11…n44, then `decompose` | `local_matrices_at`: **3×4 row-major R|t**; `fbx_actor` expands to col-major 16 for `fromArray` | `Matrix4.fromArray` (col-major) → `decompose` → pos/quat/scale. **No transpose** |
| Translation `t` | Socket t from Dummy matrix; character locomotion **not** applied from `.ani` in this player | Root: animated/bind track pos. Children: **constant** parent-space rest (from bind), not per-frame track pos | Do **not** write world positions into `bone.position`. Child animated world t will explode |
| Scale | Socket keeps decomposed scale | Ani scale ≈ identity | Keep **FBX bind scale** (viewport already rotation-focused) |
| Bone names | `normalizeBoneKey`: lower + strip non `[a-z0-9]` | MIN2 names e.g. `bip01 pelvis` | Match case-insensitive / normalized; 182/182 map already OK |
| Upright | pelvis→head world up; if `up.y < 0.6`, `setFromUnitVectors(up, +Y)` on **orientationRoot** (not per-bone) | N/A | Apply once on placement/orientation group, not by rewriting every bone |

## 2) Socket-only matrix path (map-viewer code)

```text
DummyN.Matrix "a,b,..." (16 floats)
  → matrix.set(row-major)
  → decompose(pos, quat, scale)
  → socketNode under parentBone  (parent-local)
```

This is the **only** ani-adjacent TRS apply in Animation Player. It is not the player-action skel driver.

## 3) Player-action MIN2 (what stick FK uses — Banner must mirror for FBX)

From `min2.decode_skel_stick` / `local_matrices_at`:

- Tracks: bone-major; quat = `i16[4]/32767` (**xyzw**); flag `2` adds `f32[3]` pos on that bone.
- Local rotation = track quat (parent-local).
- Local translation = root animated; children = bind rest offset.
- World mats = FK for mesh LBS only.

## 4) Mid-frame dump — 花萝 走路

- Clip: `samples/player/moves/from_mapviewer/f1/动作/f1b02yd行走.ani`
- Mid frame: **21** / 43 @ 28 fps
- JSON: `proof/compare/hualuo_walk_mid_local_trs.json`
- Spot check (local):

| Bone | local t | local q (xyzw) |
|------|---------|----------------|
| bip01 | (-0.85, 1.14, 57.98) | (0, 0.707, -0.002, 0.707) |
| bip01 pelvis | (-2.44, 2.15, 0.29) | (0.509, 0.476, 0.530, 0.483) |
| bip01 l thigh | (4.01, 0, 0) | (0.585, -0.795, -0.157, 0.041) |

`max |t| local` ≈ 60 (root); `max |t| world` ≈ 83. World t into FBX bones = shred.

## 5) Banner checklist

1. Feed `local_matrices_at` / local TRS only — never world `matrices_at`.
2. Quat **xyzw**; matrix path: 3×4 row-major → col-major 16 → `fromArray` → `decompose` (no transpose).
3. Children: do not use world positions; use rest / FBX bind t.
4. Root may take ani local t for locomotion; upright fix on orientation group only.
5. Do not treat map-viewer Animation Player as a skeletal-drive reference — it never applies player MIN2 to bones.

Jump primary clip (when testing): `f1b02yd小跳b.ani` (same contract).
