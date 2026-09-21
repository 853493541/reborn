from pathlib import Path
from min2 import load_min2_stick
p = list(Path("samples/player/moves/fenglaiwushan").glob("f1*.ani"))[0]
c = load_min2_stick(p)
for n in c.bone_names:
    low = n.lower()
    if any(k in low for k in ("arm", "hand", "fore", "clav", "shoulder", "wrist", "finger", "thigh", "calf", "foot", "spine", "neck", "head", "pelvis", "bip01")):
        if low.startswith("bip") or "arm" in low or "hand" in low or "fore" in low:
            print(repr(n))
