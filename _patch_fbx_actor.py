from pathlib import Path
import re
p = Path(r"C:\Users\Zhibin Ren\jx3-ani-player\fbx_actor.py")
t = p.read_text(encoding="utf-8")

old = '''_POSE_JSON = WEB_DIR / "runtime" / "pose.json"
_POSE_BY_NAME = ROOT / "viewport_fbx" / "runtime" / "pose_by_name.json"'''
new = '''_POSE_JSON = WEB_DIR / "runtime" / "pose.json"
_POSE_BY_NAME = ROOT / "viewport_fbx" / "runtime" / "pose_by_name.json"
# web/fbx_viewport.html polls ./runtime/ under /web/ — twin required.
_POSE_BY_NAME_WEB = WEB_DIR / "runtime" / "pose_by_name.json"'''
if old not in t:
    raise SystemExit("POSE defs missing")
t = t.replace(old, new, 1)

old2 = '''    _POSE_BY_NAME.parent.mkdir(parents=True, exist_ok=True)
    payload = {"v": _pose_ver, "frame": int(frame), "bones": by_name}
    tmp2 = _POSE_BY_NAME.with_suffix(".tmp")
    tmp2.write_text(json.dumps(payload) + "\\n", encoding="utf-8")
    tmp2.replace(_POSE_BY_NAME)
    actor.meta["pose_by_name_path"] = str(_POSE_BY_NAME)
    actor.meta["pose_bone_entries"] = len(by_name)
    return actor'''
# file uses real newline not escaped
old2 = """    _POSE_BY_NAME.parent.mkdir(parents=True, exist_ok=True)
    payload = {\"v\": _pose_ver, \"frame\": int(frame), \"bones\": by_name}
    tmp2 = _POSE_BY_NAME.with_suffix(\".tmp\")
    tmp2.write_text(json.dumps(payload) + \"\\n\", encoding=\"utf-8\")
    tmp2.replace(_POSE_BY_NAME)
    actor.meta[\"pose_by_name_path\"] = str(_POSE_BY_NAME)
    actor.meta[\"pose_bone_entries\"] = len(by_name)
    return actor"""
new2 = """    payload = {\"v\": _pose_ver, \"frame\": int(frame), \"bones\": by_name}
    text = json.dumps(payload) + \"\\n\"
    for dest in (_POSE_BY_NAME, _POSE_BY_NAME_WEB):
        dest.parent.mkdir(parents=True, exist_ok=True)
        tmp = dest.with_suffix(\".tmp\")
        tmp.write_text(text, encoding=\"utf-8\")
        tmp.replace(dest)
    actor.meta[\"pose_by_name_path\"] = str(_POSE_BY_NAME_WEB)
    actor.meta[\"pose_bone_entries\"] = len(by_name)
    return actor"""
if old2 not in t:
    # show nearby
    i = t.find("_POSE_BY_NAME.parent.mkdir")
    print("NEAR", repr(t[i:i+350]))
    raise SystemExit("apply_pose write block missing")
t = t.replace(old2, new2, 1)

old3 = '''    chrome = shutil.which("google-chrome") or shutil.which("chromium") or "/usr/bin/google-chrome"'''
new3 = '''    chrome = (
        shutil.which("google-chrome")
        or shutil.which("chromium")
        or shutil.which("chrome")
        or shutil.which("msedge")
    )
    if not chrome:
        for cand in (
            r"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
            r"C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
            r"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
            r"C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
            "/usr/bin/google-chrome",
        ):
            if Path(cand).is_file():
                chrome = cand
                break
    if not chrome:
        chrome = "/usr/bin/google-chrome"'''
if old3 not in t:
    raise SystemExit("chrome line missing")
t = t.replace(old3, new3, 1)
p.write_text(t, encoding="utf-8")
print("patched fbx_actor.py OK")
