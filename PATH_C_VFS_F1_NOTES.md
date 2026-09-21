# Path C Phase 2 prep — VFS / Pak F1 mesh+skel notes (Clint)

Parallel to Phase 1 host spike. **Do not block Steve.**

## Goal if Track 2

Read F1 **game** mesh + bip skel by logical path (not ME 花萝无动作 FBX).

## Known working extract (Andy)

| Tool | Result |
|------|--------|
| PakV4SfxExtract.exe (bin64) | Single-path OK — used for 小跳 a/b/c + 行走 |
| PakV12345-Extract.exe | Previously AV / Engine_Lua crash — avoid broad pulls |
| Usage | cwd ≈ C:\SeasunGame\Game\JX3\bin\zhcn_hd ; pathlist **GBK**; PakV4SfxExtract.exe <list.txt> <outDir> |

## Logical paths to stage next (Track 2)

`
data\source\player\f1\动作\f1s07cj重剑技能15蓄力_奇穴.ani   # already staged
data\source\player\f1\动作\f1s07cj重剑技能15.ani
data\source\player\f1\部件\mdl\f1.mdl                      # confirm exact
data\source\player\f1\部件\f1_*_body_hd.mesh                 # actor DependModel
`

Prior body mesh on disk (from FLWS mesh proofs): samples\mesh\f1_1008_body_hd.mesh (verify still present).

## Cache fallback

SeasunDownloader map-viewer cache-extraction\actor-assets\... — GBK+lower paths; LZHAM skip 20B (legacy DIGEST). Prefer PakV4SfxExtract single-path first.

## Bone set

MIN2 player sticks: **182** bones (ip01 …). Diff vs F1 game skel when host/Track2 needs bind parity.

## Status

Notes only — no Track 2 coding until Phase 1 Go/No-Go.
