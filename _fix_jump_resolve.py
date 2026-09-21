from pathlib import Path
ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")
fa_path = ROOT / "fbx_actor.py"
fa = fa_path.read_text(encoding="utf-8")
start = fa.find("def clip_fbx_for_locomotion")
end = fa.find("\ndef web_viewport_url", start)
if start < 0 or end < 0:
    raise SystemExit(f"markers {start} {end}")
new_fn = '''def clip_fbx_for_locomotion(path_or_label: str | Path | None) -> Path | None:
    """Resolve 走路/跳跃 catalog rows / .ani names → embedded-clip FBX for Mixer.

    Returns None for non-locomotion clips (keep MIN2/debug paths elsewhere).
    """
    if path_or_label is None:
        return None
    s = str(path_or_label).replace("\\\\", "/").lower()
    # jump first (小跳 / 跳跃 / jump) — explicit 2/3 only (avoid f1b02yd false match)
    if any(k in s for k in ("跳跃", "小跳", "jump", "二段跳")):
        if any(k in s for k in ("跳跃2", "jump2", "小跳b", "小跳2")):
            if CLIP_FBX_JUMP2.is_file():
                return CLIP_FBX_JUMP2
        if any(k in s for k in ("跳跃3", "jump3", "小跳c", "小跳3", "二段")):
            if CLIP_FBX_JUMP3.is_file():
                return CLIP_FBX_JUMP3
        return CLIP_FBX_JUMP1 if CLIP_FBX_JUMP1.is_file() else None
    if any(k in s for k in ("行走", "走路", "walk")):
        if "攻击" in s:
            return None
        return CLIP_FBX_WALK if CLIP_FBX_WALK.is_file() else None
    return None


'''
# fix the double-escape - use real backslash in source
new_fn = new_fn.replace("\\\\", "\\")
fa = fa[:start] + new_fn + fa[end:]
fa_path.write_text(fa, encoding="utf-8")
import sys
sys.path.insert(0, str(ROOT))
# reimport fresh
import importlib
import fbx_actor
importlib.reload(fbx_actor)
from fbx_actor import clip_fbx_for_locomotion
print("小跳a", clip_fbx_for_locomotion("f1b02yd小跳a.ani").name)
print("行走", clip_fbx_for_locomotion("F1_A081_行走.ani").name)
print("跳跃2", clip_fbx_for_locomotion("跳跃2__x.ani").name)
print("ok")
