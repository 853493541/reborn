# Phase S1 — Map-viewer SFX smoke (Steve)

**Date:** 2026-09-20 ~20:20 PT  
**Exit:** FX visible mid-frame — **PASS**

## Boot

Map-viewer already listening on `http://127.0.0.1:3015` (`node tools/start-local.mjs` from):

`C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer\`

Animation Player: `http://127.0.0.1:3015/actor-animation-player.html`

## Smoke picks

| Item | Value |
|------|-------|
| Body | F1 |
| Tani | `data\source\player\f1\动作\F1s07cj重剑技能15_风来吴山_悟.tani` |
| PSS | `data\source\other\hd特效\技能\pss\发招\c_藏剑_风来吴山_刀光01_悟.pss` |
| Timing | PSS `effectiveStartTimeMs=2000`, play ~5000ms; capture @ **5.51s** |
| API | `GET /api/player-anim/tani-parse?detail=1&path=...` → `pssPaths` / `pssDetails` |

## Proof

- `proof/compare/sfx_s1_flws_mid_fx.png` (full UI)
- `proof/compare/sfx_s1_flws_mid_fx_canvas.png` (viewport — 刀光 shards visible)
- Capture script: map-viewer `capture_sfx_s1_flws.mjs`

HUD on shot: `c_藏剑_风来吴山_刀光01_悟.pss` · `S:16 M:11` · `5.51s`

## Notes for Clint / S2

Param for parse is **`path=`** (not sourcePath). Catalog search: `/api/player-anim/tani-catalog?search=f1s07cj`.