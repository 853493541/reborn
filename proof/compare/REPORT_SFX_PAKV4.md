# REPORT — PakV4SfxExtract probe (Phase X3)

**Date:** 2026-09-20 ~20:35 PT  
**Author:** Clint  
**Status:** **PASS** for dumping skill `.pss` bytes; **partial** for full dependency closure (textures)

## Tool

```text
C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe
Usage: PakV4SfxExtract.exe <pathlist.txt> <output_dir>
  pathlist.txt: GBK-encoded, one logical path per line
```

**Recipe that works:**

1. Write GBK pathlist (code page 936).
2. Run with **cwd** = `...\zhcn_hd\bin64` (tool itself cds to parent `zhcn_hd` and inits PakV4).
3. Paths must be exact `data\source\...` strings as in `.tani` / plot binds.

Do **not** wait on MovieEditor UI for this track.

## 风来吴山 — tani → PSS map (for Thor / X2)

| Role | Logical path |
|------|----------------|
| Cast (悟) knife trail | `data\source\other\hd特效\技能\pss\发招\c_藏剑_风来吴山_刀光01_悟.pss` |
| Cast (悟) knife trail 02 | `...\发招\c_藏剑_风来吴山_刀光02_悟.pss` |
| Charge / 蓄力 | `data\source\other\hd特效\技能\pss\状态\c_藏剑风车蓄力.pss` |
| Range ring | `...\状态\c_藏剑风车范围.pss` |
| Body ani (cast) | `data\source\player\f1\动作\f1s07cj重剑技能15.ani` (+ `_悟` / `_奇穴` variants) |
| Body ani (蓄力) | `...\f1s07cj重剑技能15蓄力_奇穴.ani` |
| Sample tani | `F1s07cj重剑技能15_风来吴山_悟.tani` (and HD / 皮肤 / 火 variants under `samples/player/moves/` + map-viewer tani-extract) |

Note: some `.tani` embeds a truncated twin path (`her\hd特效\...`) — ignore; use full `data\source\other\hd特效\...` only.

## Extract result (PASS)

Pathlist: `C:\Users\Zhibin Ren\jx3-staging\flws-sfx-pull\pakv4-flws.txt`  
Output: `...\flws-sfx-pull\extract\`  
**Extracted: 8 / Not found: 0**

| File | Bytes | Magic |
|------|------:|-------|
| `c_藏剑_风来吴山_刀光01_悟.pss` | 156769 | `PAR\0` (particle) |
| `c_藏剑_风来吴山_刀光02_悟.pss` | 155387 | `PAR\0` |
| `c_藏剑风车蓄力.pss` | 107714 | `PAR\0` |
| `c_藏剑风车范围.pss` | 35285 | `PAR\0` |
| + 4 related 刀光 baselines | 60–222 KB | `PAR\0` |

Staged copies + manifest:

- `proof/compare/sfx_pakv4_flws/*.pss`
- `proof/compare/sfx_pakv4_flws/manifest.json`

## Dependency closure (partial)

PSS embeds Seasun material + texture paths. Second pass (`pakv4-deps.txt`, 57 paths):

| Kind | Via PakV4SfxExtract |
|------|---------------------|
| `hd特效\材质\*.jsondef` | **9 found** (materials) |
| `特效\贴图\**\*.tga` | **0 / 48 NOT FOUND** |
| mesh/ani under `hd特效\技能\mesh\` (seen in related 刀光 PSS) | not in this FLWS focus list |

Tried `PakV12345-Extract.exe` on a 5-TGA sample → **access violation** (exit `-1073741819`); empty logs. No TGA on disk under Game/JX3 or Downloader trees from a quick name search.

**Implication:** `.pss` dumps are real engine particle packs, but they are **not** a standalone playable FX bundle yet — textures (and likely shaders/meshes) live in packs this SFX extractor does not resolve. Replay still needs ME/client or a broader extract story (X4 after GT).

## What this is / is not

| Is | Is not |
|----|--------|
| Confirmed FLWS PSS logical paths + on-disk bytes | GT for how FX *look* (Thor client / ME still) |
| Proof PakV4SfxExtract works for skill PSS | A map-viewer-ready asset pack |
| Material jsondefs extractable | Full texture closure via this tool alone |

## Next (Clint idle unless asked)

1. Hold further ME UI automation (Steve parked).  
2. Support Thor X2 with skill name **风来吴山** + PSS ids above.  
3. If Andy wants texture closure: try alternate pak roots / official texture extract, or pull from a live ME/client cache after one human load — not blocking X2.

Ping @Tony: **X3 PSS extract PASS** (bytes staged); texture deps still open.