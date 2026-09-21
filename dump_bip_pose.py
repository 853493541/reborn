from pathlib import Path
from min2 import load_min2_stick
try:
    from mina import stick_edges, _PARENT_RULES
except Exception as e:
    print("mina import", e)
    stick_edges = None
    _PARENT_RULES = []

p = list(Path("samples/player/moves/fenglaiwushan").glob("f1*.ani"))[0]
c = load_min2_stick(p)
mid = max(0, int(c.frame_count * 0.45))
pos = c.positions_at(mid)
print("clip", p.name, "mid", mid)
bip = [(n, pos[i]) for i, n in enumerate(c.bone_names) if n.lower().startswith("bip")]
# print key bones
keys = ["bip01", "bip01 pelvis", "bip01 spine", "bip01 spine1", "bip01 spine2",
        "bip01 neck", "bip01 head", "bip01 l thigh", "bip01 r thigh",
        "bip01 l calf", "bip01 r calf", "bip01 l foot", "bip01 r foot",
        "bip01 l upperarm", "bip01 r upperarm", "bip01 l forearm", "bip01 r forearm",
        "bip01 l hand", "bip01 r hand"]
index = {n.lower(): i for i, n in enumerate(c.bone_names)}
for k in keys:
    i = index.get(k)
    if i is None:
        # fuzzy
        hits = [n for n in c.bone_names if k.replace(" ","") in n.lower().replace(" ","")]
        print(k, "MISSING", hits[:3])
    else:
        print(f"{c.bone_names[i]:30s} {pos[i]}")

if stick_edges:
    edges = stick_edges(c.bone_names)
    print("stick edges", len(edges))
    # lengths of edges
    lens = []
    for a,b in edges:
        pa, pb = pos[a], pos[b]
        d = ((pa[0]-pb[0])**2+(pa[1]-pb[1])**2+(pa[2]-pb[2])**2)**0.5
        lens.append((d, c.bone_names[a], c.bone_names[b]))
    lens.sort(reverse=True)
    print("longest edges:")
    for row in lens[:10]:
        print(" ", row)
print("parent rules", len(_PARENT_RULES))
