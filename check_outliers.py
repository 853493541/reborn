from pathlib import Path
from min2 import load_min2_stick
import statistics as stats
p = list(Path("samples/player/moves/fenglaiwushan").glob("f1*.ani"))[0]
c = load_min2_stick(p)
mid = max(0, int(c.frame_count * 0.45))
pos = c.positions_at(mid)
bip = [(n, pos[i]) for i, n in enumerate(c.bone_names) if n.lower().startswith("bip")]
# median center
xs = [p[0] for _, p in bip]; ys=[p[1] for _,p in bip]; zs=[p[2] for _,p in bip]
cx, cy, cz = stats.median(xs), stats.median(ys), stats.median(zs)
print("center", cx, cy, cz)
out = []
for n, p in bip:
    d = ((p[0]-cx)**2+(p[1]-cy)**2+(p[2]-cz)**2)**0.5
    out.append((d, n, p))
out.sort(reverse=True)
print("farthest bip from median:")
for row in out[:20]:
    print(f"  {row[0]:7.2f} {row[1]:30s} {row[2]}")
# core only
core = [k for k in ["bip01","bip01 pelvis","bip01 spine","bip01 spine1","bip01 spine2","bip01 neck","bip01 neck1","bip01 head",
 "bip01 l clavicle","bip01 r clavicle","bip01 l upperarm","bip01 r upperarm","bip01 l foretwist","bip01 r foretwist",
 "bip01 l hand","bip01 r hand","bip01 l thigh","bip01 r thigh","bip01 l calf","bip01 r calf","bip01 l foot","bip01 r foot"]]
print("\ncore positions:")
idx = {n.lower(): (n,pos[i]) for i,n in enumerate(c.bone_names)}
for k in core:
    print(k, idx.get(k))
