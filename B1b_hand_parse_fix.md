# B1b — Hand MINA parse fix

**Owner:** Clint  
**Issue:** `f1_2227_hand_hd.mesh.ani` misread bone/frame (MemoryError) after shorter tex hint `MRE.png` (no leading `_`).

## Fix
After mesh/tex cstrings, **scan** delta 0..7 for `(bone_count, frame_count)` + marker skip 0..7 such that:
- counts are plausible
- 30-byte name table scores (nonempty + `bip01` bonus)
- track payload `bones×frames×15` f32 fits EOF (±16, NaN trailer)

Body still resolves to 33×46; hand to **57×46**.

## Verify
```bash
cd /workspace/jx3-ani-player
.venv/bin/python -c "from mina import load_mina; 
print(load_mina('samples/f1_2227_body_hd.mesh.ani').bone_count,
      load_mina('samples/f1_2227_hand_hd.mesh.ani').bone_count)"
# → 33 57
```
