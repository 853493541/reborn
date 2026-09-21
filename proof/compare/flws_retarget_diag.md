# FLWS retarget diagnosis (helper-skel + SkeletonUtils.retargetClip)

## MIN2 space
- `positions_at` / `quats_at`: **WORLD** (see min2.Min2SkelClip docstring).
- `local_quats_at` / `_local_positions`: **parent-local** (used by this export).
- worldUuid shred root cause hypothesis: applying one world pose (name-keyed) onto
  **all duplicate-named bones** across multi-mesh (weapon/hair) with FBX parent graph ≠ MINA parents,
  plus Z-up→Y-up baking into wrong locals → Mixer still plays but mesh shreds.

## Approach (Tony authorized)
MIN2 parent-local clip → helper Object3D/Bone hierarchy matching MINA parents →
`THREE.SkeletonUtils.retargetClip` onto **primary body SkinnedMesh** → `AnimationMixer` on that mesh.
No bone.decompose / apply_pose spray.

## MIN2 charge parent graph (core)
```
{
  "bip01": null,
  "bip01 pelvis": "bip01",
  "bip01 spine": "bip01 pelvis",
  "bip01 spine1": "bip01 spine",
  "bip01 spine2": "bip01 spine1",
  "bip01 neck": "bip01 spine2",
  "bip01 head": "bip01 neck1",
  "bip01 l thigh": "bip01 pelvis",
  "bip01 r thigh": "bip01 pelvis",
  "bip01 l upperarm": "bip01 l clavicle",
  "bip01 r upperarm": "bip01 r clavicle",
  "bone_spine": null,
  "bone_l_thigh": null,
  "bone_r_thigh": null
}
```

- export bones=182 tracks=364 frames=51

## FBX note
Name-only Map is wrong across duplicate Bone names; retargetClip iterates only
`primary.skeleton.bones` (largest / hualuo-body biased SkinnedMesh).
