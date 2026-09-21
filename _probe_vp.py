from pathlib import Path
import re
t = Path("web/fbx_viewport.js").read_text(encoding="utf-8")
for m in re.finditer(r"function\s+(\w+)", t):
    name = m.group(1)
    if any(k in name.lower() for k in ("clip", "mixer", "pose", "track", "anim")):
        print("fn", name, m.start())
for key in ("clipFbx", "get(\"clip\")", "get('clip')", "QuaternionKeyframeTrack", "VectorKeyframeTrack", "AnimationClip.parse", "clipAction"):
    print(key, t.find(key))
# dump from first QuaternionKeyframeTrack
i = t.find("QuaternionKeyframeTrack")
print("--- around Quaternion ---")
print(t[max(0, i - 400) : i + 2000])
i = t.find("clipFbx")
print("--- around clipFbx ---")
print(t[max(0, i - 200) : i + 2200])
