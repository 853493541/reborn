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

## 4. Authored character-size table (`player.txt`, 2026-10-05)

Extract `Represent/player/player.txt` (index key `PlayerTable` in `Represent/filepath.ini`),
GBK TSV, 7 role rows (`RoleType`, `Desc`, `ModelHeight`, `ModelScale`, `MDLFilePath`):

| RoleType | Desc | ModelHeight | ModelScale | Model |
|---|---|---|---|---|
| 0 | rtInvalid | 190 | 1 | M2_player.mdl |
| 1 | rtStandardMale | 185 | 1 | M2_player.mdl |
| 2 | rtStandardFemale | 173 | 1.04 | F2_player.mdl |
| 3 | rtStrongMale | 190 | 1 | M3_临时套装.mdl |
| 4 | rtSexyFemale | 190 | 1 | F2_player.mdl |
| 5 | rtLittleBoy | 125 | 1.15 | M1_player.mdl |
| 6 | rtLittleGirl (花萝 F1) | 125 | 1.1 | F1.mdl |

- `ModelHeight` is authored in engine units (= cm). The mesh bind-pose census gives
  181.6 for the adult male **body mesh** (table: 185 — logical height incl. gear/pose).
- Consumer: `JX3RepresentX64.dll` parses `player.txt` (`PlayerTable` + `ModelHeight`
  strings); the Lua helper `GetMasterModelHeight` (fn `0x1804171f0`) exposes it at runtime.
- Host: per-role height/scale should come from this table, not hardcoded values.

Host capsule proportion (for reference, registered proxy): 花萝 17/116 at ModelHeight
125 and adult 25/170 at ~185 are both ≈ (0.136, 0.93) × ModelHeight — a host-chosen
proportion, not a game-derived rule.

## 5. Gameplay capsule (G-1) — dig result (2026-10-05): not in the client

- The Semantic K/V schema names `capsules radius` / `capsules length` exist in **exactly
  two modules** in both installs (`SIMWorldX64.dll`, `JX3RepresentX64.dll`) plus the
  downloader copies; a full byte scan of the game client `bin64`, the MovieEditor install,
  the `SeasunDownloaderV2.4` extraction tree and `C:\jx3tmp` found **no data file**
  carrying the keys.
- SIMWorld reader: interner `0x180001bc0` → global `0x18005E3B8`; the consumer around
  `0x180032400` reads it through the K/V vtable (`call [rax+0x40]`) and feeds
  `PhysicScene::_AddCapsules` (`0x1800223b0`); the only embedded constants are `0.01f`
  epsilons — **no defaults**.
- JX3Represent interner `0x180036f60` → global `0x180EC6EA0`; no RIP-relative reader in
  module, no `rel32` callers, no pointer-table references; `GetMasterModelHeight`
  (`0x1804171f0`) is the Lua height helper, not the capsule writer.
- `physic_shape_param.krl.txt` capsule id 6 (`capsule r50/l50`) is a named dynamic-actor
  shape (`PxWorld::GetRigidParam`/ShapeData), not the movement capsule;
  `physic_character_param.krl.txt` is the 11-body ragdoll (`radius 4..13`, `length 6..18`)
  plus `EnableCharacterCapsule=1`.
- Conclusion (HIGH): the gameplay capsule value arrives through the Semantic K/V
  scene-response stream — no client-shipped value exists to extract. Registered boundary;
  the host keeps its capsule as a registered proxy, scaled per role from the authored
  `player.txt` ModelHeight (§4) where a per-role value is needed.

## 6. Reproduction

```powershell
# character mesh census (needs .venv for numpy)
.\.venv\Scripts\python.exe tools\netcode\character_mesh_census.py samples\mesh --json proof\netcode\character_size\mesh_census.json
# single mesh detail with bone binds
.\.venv\Scripts\python.exe tools\netcode\measure_character_size.py samples\mesh\m2_1018_body_hd.mesh --json proof\netcode\character_size\character_meshes.json
```

## 7. Open items

1. GAME_FPS is inferred from script comments (`16帧等于1秒`); confirm against a
   movement-speed table if one is extracted (`player.nRunSpeed` is runtime-only).
2. `LogicalScene 2048x2048` units (m vs 尺) — currently treated as metres (MED).
3. Bone bind matrices in HD meshes carry zero translations on the last matrix
   (layout quirk); height measurement uses vertices, which is sufficient.
4. Gameplay capsule (G-1): the values are not in the client install (§5) — a runtime /
   server K/V boundary. The host capsule stays a registered proxy (optionally scaled
   per role from the authored `ModelHeight`, §4); re-open only with a runtime source.
