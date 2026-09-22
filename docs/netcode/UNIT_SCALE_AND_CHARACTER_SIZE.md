# Engine unit scale and character size vs map

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Question:** how big is a character in engine units, and how do those units relate
to the map and to the player-visible 尺?

**Answer: 1 engine unit = 1 cm.** Character (adult male) = 181.6 units = 1.82 m.
Combined with the skill-script factor, **1 尺 = 64 units = 0.64 m**.

---

## 1. Character size (measured from shipped HD meshes)

Method: `tools/netcode/character_mesh_census.py` parses bind-pose vertex data from
HD `.mesh` files and reports the Y extent. Evidence: `proof/netcode/character_size/mesh_census.json`.

| Mesh | height (units) | ymin | ymax | verts |
|---|---|---|---|---|
| `m2_1018_body_hd.mesh` (adult male, full body) | **181.64** | -0.09 | 181.56 | 5,098 |
| `f1_1008_body_hd.mesh` (child, full body) | **150.24** | -0.01 | 150.23 | 30,050 |
| `f1_1006_body_hd.mesh` | 141.88 | -0.01 | 141.87 | 15,400 |
| `f1_1009_body_hd.mesh` | 120.70 | 0.15 | 120.85 | 16,184 |
| `f1_1007_body_hd.mesh` | 117.51 | -0.09 | 117.42 | 13,307 |
| `f1_1004_body_hd.mesh` | 114.59 | -0.05 | 114.55 | 8,372 |
| `f1_2227_body_hd.mesh` (dress-only piece, feet missing) | 92.64 | 6.82 | 99.46 | 11,738 |

Calibration: an adult male game model is ~1.8 m. `181.64 units → 1.816 m` gives
**1 unit = 1.00 cm**. Any other scale is absurd for a human (e.g. treating a unit
as 1/192 m would make the adult 0.95 m). All 5,000+ mesh vertices share the same
coordinate space as the skeleton and the world, so this scale applies to the map.

Child (`f1`) full-body meshes span 1.15–1.50 m depending on outfit/hair; the
dress-only pieces (like 2227) omit the legs, which is why they measure ~0.93 m.

## 2. Unit conversions

| From | To | Factor | Evidence |
|---|---|---|---|
| engine unit | cm | ×1 | adult mesh 181.64 units = 1.816 m |
| engine unit | m | /100 | same |
| 尺 (player-visible) | engine units | ×64 | 4 script sites (`/64`, `4*64*4*64`, `15*15*64*64`) |
| 尺 | m | ×0.64 | 64 cm; so `LENGTH_BASE = 64` = 0.64 m |
| m | 尺 | ×1.5625 | 1 m = 1.5625 尺 |
| gameplay frame | s | /16 | script comment “16帧等于1秒” (GAME_FPS = 16) |

This is why the tooltips are consistent: 风来吴山 tooltip says 10尺 and the script
uses `nAreaRadius = 10 * LENGTH_BASE`; 蹑云逐月 max level = 16 frames × 80 units =
1280 units = **20尺 = 12.8 m**.

## 3. Character vs map

| Quantity | Value in units | In meters |
|---|---|---|
| adult male height | 181.6 | 1.82 |
| child (花萝) height | 120–150 | 1.20–1.50 |
| terrain tile (from `_Setting.ini`) | 512 texels × scale 100 = 51,200 | 512 |
| terrain region grid | 8×8 tiles = 409,600 | 4,096 (4.1 km zone) |
| map world coordinate example (systemCamera0 X) | 147,463 | 1,474.6 |
| camera height example (龙门寻宝) | 5,231 | 52.3 |
| melee range (4尺) | 256 | 2.56 |
| 风来吴山 radius (10尺) | 640 | 6.40 |
| 太阴指 backdash (15尺) | 960 | 9.60 |
| 蹑云逐月 dash (20尺, max) | 1,280 | 12.80 |
| 夺命蛊 range (30尺) | 1,920 | 19.20 |
| 破绽触发 (50尺) | 3,200 | 32.00 |

So on the map a character is ~1.8 m against a 512 m terrain tile — about
0.35 % of a tile, exactly the human/land scale you would expect from the
terrain data. `LogicalScene 2048x2048` in the map setting is therefore metres
(2.05 km logical extent; MED).

## 4. Reproduction

```powershell
# character mesh census (needs .venv for numpy)
.\.venv\Scripts\python.exe tools\netcode\character_mesh_census.py samples\mesh --json proof\netcode\character_size\mesh_census.json
# single mesh detail with bone binds
.\.venv\Scripts\python.exe tools\netcode\measure_character_size.py samples\mesh\m2_1018_body_hd.mesh --json proof\netcode\character_size\character_meshes.json
```

## 5. Open items

1. GAME_FPS is inferred from script comments (`16帧等于1秒`); confirm against a
   movement-speed table if one is extracted (`player.nRunSpeed` is runtime-only).
2. `LogicalScene 2048x2048` units (m vs 尺) — currently treated as metres (MED).
3. Bone bind matrices in HD meshes carry zero translations on the last matrix
   (layout quirk); height measurement uses vertices, which is sufficient.
