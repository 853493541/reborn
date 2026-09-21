# B1b SMOKE — hand MINA load (57×46)

**Owner:** Thor  
**Date:** 2026-09-19 PT  
**Overall:** **PASS**

## Accept

| # | Check | Result |
|---|--------|--------|
| 1 | Body `f1_2227_body_hd.mesh.ani` still 33×46 | **PASS** |
| 2 | Hand `f1_2227_hand_hd.mesh.ani` loads 57 bones × 46 frames | **PASS** |
| 3 | Hand scrub endpoints (0 / 10 / 45) return 57 positions | **PASS** |

## Note

Clint’s B1b parse fix (scan after shorter `MRE.png` tex hint) unblocks hand. Prior MemoryError / garbage counts cleared.

## Verify

```bash
cd /workspace/jx3-ani-player
.venv/bin/python -c "from mina import load_mina; b=load_mina('samples/f1_2227_body_hd.mesh.ani'); h=load_mina('samples/f1_2227_hand_hd.mesh.ani'); print(b.bone_count,b.frame_count,h.bone_count,h.frame_count)"
# → 33 46 57 46
```
