# Bone-apply contract: map-viewer vs ani-player

## Sources
| Side | File | Role |
|------|------|------|
| GT character play | `SeasunDownloader…/jx3-web-map-viewer/public/js/actor-viewer.js` (staged `_port_from_mapviewer/live_mv/`) | Drives 花萝 body |
| GT Animation Player | `actor-animation-player.js` | PSS/effects on bind-pose anchor — **not** skeletal .ani driver |
| Ours | `web/fbx_viewport.js` `__applyPose` / `applyPoseByName` | MIN2 → matrices → bone.decompose |

## Contract table

| Line | Map-viewer actor-viewer | Ours fbx_viewport |
|------|-------------------------|-------------------|
| Mechanism | `THREE.AnimationMixer(root)` + `mixer.clipAction(clip)` | Custom `__applyPose` polling JSON |
| Clip source | **FBX-embedded** `root.animations` (FBXLoader), track names normalized | **MIN2 `.ani`** decoded in Python (`min2.local_matrices_at`) |
| Parent-local vs model | Mixer applies tracks as authored on FBX bone hierarchy | We write parent-local 3x4 / quat onto bones **by name** |
| bind × anim | Inside FBX clip + Three mixer (bind rest lives in FBX) | We **replace** `bone.position/quaternion/scale` with anim local (or rotation-only attempt) |
| Quat order | Whatever FBXLoader embeds in tracks | MIN2 i16/32767 as **xyzw** |
| Translation | From FBX animation tracks | From MIN2 rest / root `anim_p` |
| `.ani` bytes | **Not** fed into bone.decompose in actor-viewer | Entire pose path |

## One line that still differs (blocker)
**Map-viewer never applies MIN2 locals via `bone.decompose`. It plays FBX AnimationClips through AnimationMixer.** Our `__applyPose` path has no GT equivalent for skeletal walk/jump.

## Artifacts
- Ours mid-walk local TRS: `proof/compare/ours_walk_mid_local_trs.json`
- Map-viewer mid-frame dump: **needs Clint** (sample mixer/FBX clip locals at same frame) → `proof/compare/mapviewer_walk_mid_local_trs.json`

## Next single hypothesis (not sprayed)
Convert MIN2 → `AnimationClip` (quat/pos tracks) and drive with `AnimationMixer` like actor-viewer — **or** obtain an FBX that already embeds 走路/跳跃 clips and play those.
