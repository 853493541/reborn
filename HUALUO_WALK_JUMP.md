# HUALUO walk/jump — live player

**2026-09-20 PT** · Banner

## Live path (Thor smoke)
`Start JX3 Ani Player.bat` → `--character-mode fbx` → pick 花萝 → 走路/跳跃:

- Resolves to **skin** `hualuo_no_anim.fbx` + **clipFbx** `hualuo_walk.fbx` / `hualuo_jump1.fbx`
- `AnimationMixer` (546 tracks) — **not** MIN2 `apply_pose` locals onto FBX bones
- Catalog `playable_f1_hualuo.json` marks walk/jump with `driver: clipFbx`

## Proofs
- `proof/compare/hualuo_walk_mid.png`
- `proof/compare/hualuo_jump_mid.png`
- `proof/compare/thor_livepath_walk_mid.png` (F1_A081_行走 name resolve)

## Note
Other .ani still use MIN2 pose feed (debug). Locomotion names (行走/走路/跳跃/小跳/walk/jump) use Mixer.
