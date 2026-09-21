from pathlib import Path
from min2 import load_min2_stick
import math
import sys
sys.path.insert(0, r"C:\Users\Zhibin Ren\jx3-ani-player")
# when run on Andy, ROOT is cwd
ROOT = Path(".").resolve()
p = ROOT / "samples" / "player" / "moves" / "fenglaiwushan"
anis = list(p.glob("*.ani"))
print("anis", [a.name for a in anis])
c = load_min2_stick(anis[0])
mid = c.frame_count // 2
print("bones", c.bone_count, "frames", c.frame_count, "mid", mid)
lp = c._local_positions
bad = []
ok = []
for bi, name in enumerate(c.bone_names):
    t = lp[bi][mid]
    mag = math.sqrt(t[0] * t[0] + t[1] * t[1] + t[2] * t[2])
    if mag > 200 or mag != mag or abs(t[0]) > 1e6:
        bad.append((mag, name, t))
    else:
        ok.append((mag, name))
bad.sort(reverse=True)
print("bad local pos", len(bad), "ok", len(ok))
print("worst bad:")
for row in bad[:12]:
    print(" ", row[0], row[1], row[2])
print("sample ok:", ok[:8])
try:
    from mina import _PARENT_RULES
except ImportError:
    _PARENT_RULES = []
index = {n.lower().strip(): i for i, n in enumerate(c.bone_names)}
parented = sum(1 for child, par in _PARENT_RULES if child in index and par in index)
print("parent rules matched", parented, "of", len(_PARENT_RULES))
# roots = bones with huge local that are actually unparented using bind
print("has local_matrices_at", hasattr(c, "local_matrices_at"))
lm = c.local_matrices_at(mid)
wm = c.matrices_at(mid)

def tmax(mats):
    mags = []
    for i, m in enumerate(mats):
        t = (m[3], m[7], m[11])
        mag = math.sqrt(sum(x * x for x in t))
        mags.append((mag, c.bone_names[i]))
    mags.sort(reverse=True)
    return mags[:8]

print("top local t", tmax(lm))
print("top world t", tmax(wm))
# check bind sanity via rest-like: world positions look ok
wp = c.positions_at(mid)
print("world pos range", min(min(p) for p in wp), max(max(p) for p in wp))
lq = c.local_quats_at(mid)
badq = 0
for q in lq:
    n = math.sqrt(sum(x * x for x in q))
    if abs(n - 1) > 0.1:
        badq += 1
print("nonunit local quats", badq)
