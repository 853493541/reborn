from pathlib import Path
md = """# Map-viewer 风来吴山 FBX clips — HARD BLOCKER

Staged search 2026-09-20. Goal was Mixer product path for 花萝 风来吴山·蓄力/释放 (same pattern as `hualuo_walk.fbx`).

## Result

**No MovieEditor / map-viewer / repo-clips FBX embeds 风来吴山 (蓄力 or 释放).**

Nothing to stage under `samples/actor_presets/f1_hualuo/mapviewer_clips/` as `hualuo_flws_charge.fbx` / `hualuo_flws_cast.fbx`.

We already have **MIN2** skeletal sources (not Mixer GT):

| Role | Path |
|------|------|
| 蓄力 | `samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani` |
| 释放 | `samples/player/moves/fenglaiwushan/f1s07cj重剑技能15.ani` |

Those are the PakV4 / catalog playable paths — **not** FBX `root.animations`.

## Where searched

| Location | Finding |
|----------|---------|
| `C:\\SeasunGame\\MovieEditor\\source\\fbx\\*` | Only: 花萝(无动作), 走路, 跳跃1–3, 龙牙 — **no 风来吴山 folder** |
| `jx3-web-map-viewer\\public\\repo-clips\\*` | Same mirror set |
| `C:\\SeasunGame\\public\\repo-clips\\*` | Same |
| All `*.fbx` under `C:\\SeasunGame` (~102 unique paths) | Name filter 风来/吴山/蓄力/释放/flws/s07cj/技能15 — **0 hits** |
| AnimCurve ranking | Clip-bearing actor exports = walk/jump/龙牙 (+ skeleton import tests). No FLWS-named export |
| `MovieEditor\\source\\player\\F1\\F1动作导入测试.FBX` | 1 clip: `F1b02ty排箫行走01` (not FLWS) |
| `F1\\f1.fbx` / `F1-标准骨骼.FBX` | Generic Take 001 / Bip01 — not FLWS |
| `MovieEditor\\source\\fbx\\998\\998.fbx` | `seasun animation` 1.5s — unrelated |
| Text refs in map-viewer | 风来吴山 only as **.tani** / ability-sound paths — no `.fbx` |

## Map-viewer Mixer pattern (for context)

Walk/jump work because separate export folders ship FBX with embedded `seasun animation`:

- `source/fbx/走路/花萝走路.fbx` → staged `mapviewer_clips/hualuo_walk.fbx`
- `source/fbx/跳跃N/跳跃N.fbx` → `hualuo_jumpN.fbx`

Actor-viewer: load 花萝 skin → switch clip source to that export → `AnimationMixer`.

**风来吴山 was never exported that way on this machine.**

## Unblock options (for Tony / Banner — Clint not inventing FBX)

1. **MovieEditor export**: someone exports 花萝 + 蓄力/释放 takes to `source/fbx/风来吴山*/` (or two folders), then Clint stages like walk.
2. **Banner Mixer from MIN2**: convert `f1s07cj…蓄力_奇穴.ani` / cast `.ani` → `THREE.AnimationClip` (Banner already exploring) — only path without a new FBX.
3. Re-check another Andy disk / Seasun install if one exists outside `C:\\SeasunGame`.

## Verify

No file to run `FBXLoader.parse` on for FLWS. Walk/jump aliases remain the only confirmed non-empty `root.animations` 花萝 clip FBXs.
"""
path = Path(r"C:\Users\Zhibin Ren\jx3-ani-player\proof\compare\MAPVIEWER_FLWS_FBX_CLIPS.md")
path.write_text(md, encoding="utf-8")
Path(r"C:\Users\Zhibin Ren\jx3-staging\MAPVIEWER_FLWS_FBX_CLIPS.md").write_text(md, encoding="utf-8")
print("wrote", path, path.stat().st_size)
