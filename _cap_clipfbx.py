from pathlib import Path
from urllib.parse import urlencode
import json
import fbx_actor as fa

ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")
actor = fa.load_fbx_actor()
fa.ensure_server(actor)
port = actor._port
print("port", port)

def capture(name, clip_rel, t, out_name):
    q = urlencode({
        "fbx": "/samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx",
        "tex": "/samples/actor_presets/f1_hualuo/tex/",
        "closeup": "1",
        "clipFbx": clip_rel,
        "t": str(t),
    })
    url = f"http://127.0.0.1:{port}/web/fbx_viewport.html?{q}"
    out = ROOT / "proof" / "compare" / out_name
    print("CAPTURING", name, "t=", t)
    print("URL", url)
    meta = fa._capture_with_playwright(url, out, timeout_ms=120000)
    print("META", json.dumps(meta, indent=2, ensure_ascii=False)[:2000])
    return meta

m1 = capture(
    "walk",
    "/samples/actor_presets/f1_hualuo/mapviewer_clips_ascii/walk.fbx",
    0.63,
    "hualuo_walk_clipFbx_mid.png",
)
m2 = capture(
    "jump1",
    "/samples/actor_presets/f1_hualuo/mapviewer_clips_ascii/jump1.fbx",
    0.33,
    "hualuo_jump_clipFbx_mid.png",
)
print("DONE walk mixer=", m1.get("mixer") or (m1.get("ready") if False else m1))
