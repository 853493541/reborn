# Map-viewer GT — 花萝 / 风来吴山·蓄力

Staged 2026-09-20 for Banner P0 (pose vs map-viewer Animation Player).

## Driving .ani (confirmed)

| Field | Value |
|-------|-------|
| UI label | 风来吴山·蓄力 (first_bar / charge) |
| Character | F1 花萝 |
| Playable skeletal | `samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani` |
| Format | **MIN2** (not MINA / FbxCmd) |
| Size / sha256 | 104451 / `8206455ec82840d1a5904c0d1a0b046ebf14f7e5a04f15064938e14615f97440` |
| Stick | 182 bones × 51 frames @ 33 fps (bone0 = bip01) |
| Byte-identical twin | `samples/player/moves/from_mapviewer/f1/动作/f1s07cj重剑技能15蓄力_奇穴.ani` |
| Also under | `samples/player/moves/fenglaiwushan/f1s07cj重剑技能15蓄力_奇穴.ani` |

Catalog source: `samples/player/catalog/playable_f1_hualuo.json` → `first_bar` / `default_clips` entry `kind=charge`.

Related cast clip (not P0 gate): `moves/fenglaiwushan/f1s07cj重剑技能15.ani` (风来吴山·释放).

`.tani` rows for this skill resolve to the skeletal charge .ani above via `resolve_playable` — do **not** feed .tani into MIN2.

## Map-viewer GT visuals (this folder)

These are the reference captures from map-viewer Animation Player / compare pack. Use for mid-clip humanoid check — **not** the explode aniplayer PNGs.

- `COMPARE_mapviewer_flws.png` / `COMPARE_mapviewer_flws_viewport.png`
- `SIDE_mapviewer.png`
- `mv_05_playing.png` / `mv_06_canvas.png` / `mv_04_selected.png`
- `mapviewer_f1_loaded.png` / `mapviewer_actor_page.png`
- `MV_ZOOM_character.png` / `MV_CHARACTER_CLOSE.png`

## Explicit non-GT (do not SOURCES / do not treat as pass)

- `proof/compare/aniplayer_f1_hualuo_flws_f0{00,25,50}.png` (if present)
- `*_local_f*.png`, `*_retarget_f*.png`, bad `aniplayer_flws_pose_*`
- Any mid-clip with spikes / shatter / floating head

## GT code path (for Banner)

Map-viewer port: `/workspace/map-viewer-port/` — `actor-animation-player.js`, `player-anim-loaders.js`.

Andy player contract: `min2.local_matrices_at` for FBX; world matrices only for mesh LBS.