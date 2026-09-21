# Mixer path status — 2026-09-20 PT

## Done
- Stopped decompose retarget for clip mode.
- Viewport: `AnimationMixer` + `?clip=` JSON (both `web/` and `viewport_fbx/`).
- Exported MIN2 → clip JSON (quat-only, bip without fingers):
  - `web/runtime/clip_walk_quat.json` (f1b02yd行走, 43f)
  - `web/runtime/clip_jump_quat.json` (f1b02yd小跳a, 23f)
- Mixer HUD confirms: `mixer f1b02yd行走 t=0.679s tracks=152` / `小跳a t=0.300s`.

## FAIL proofs (still shred)
- `proof/compare/hualuo_walk_mid_20260920.png`
- `proof/compare/hualuo_jump_mid_20260920.png`

## Hard blocker
All staged 花萝 FBX are **无动作 / no_anim** (no `root.animations`).
Map-viewer GT plays **FBX-embedded** clips via Mixer — it does not retarget MIN2 onto a no-anim preset.
MIN2→AnimationClip (quat-only) + Mixer still shreds skinning.

## Unblock
Clint stages map-viewer export FBX that **embeds** 走路 + 跳跃 (or any F1 clip FBX with those takes) under `samples/actor_presets/f1_hualuo/`. Then Mixer plays `root.animations` with zero MIN2 retarget.
