"""Headless verification of the Tk app's FBX viewport server + assets."""
from __future__ import annotations

import urllib.request
from pathlib import Path

from fbx_actor import (
    clip_fbx_for_locomotion,
    clip_json_for_flws,
    ensure_server,
    load_fbx_actor,
    web_viewport_url,
)

actor = load_fbx_actor()
port = ensure_server(actor)
url = web_viewport_url(actor, closeup=True)
print("server port", port)
print("bind url   ", url)

walk = clip_fbx_for_locomotion("f1b02yd行走")
charge = clip_json_for_flws("风来吴山·蓄力")
cast = clip_json_for_flws("风来吴山·释放")
print("walk fbx   ", walk, walk.is_file() if walk else None)
print("charge json", charge, charge.is_file() if charge else None)
print("cast json  ", cast, cast.is_file() if cast else None)

urls = {
    "viewport html": f"http://127.0.0.1:{port}/web/fbx_viewport.html",
    "three module": f"http://127.0.0.1:{port}/web/vendor/three/build/three.module.js",
    "fbx": f"http://127.0.0.1:{port}/samples/actor_presets/f1_hualuo/mapviewer_clips/hualuo_no_anim.fbx",
    "walk fbx": f"http://127.0.0.1:{port}/samples/actor_presets/f1_hualuo/mapviewer_clips/hualuo_walk.fbx",
    "charge json": f"http://127.0.0.1:{port}/web/runtime/clip_flws_charge.json",
}
for label, target in urls.items():
    try:
        with urllib.request.urlopen(target, timeout=10) as resp:
            print(f"  {label:14s} HTTP {resp.status} {resp.headers.get('Content-Type')} {resp.headers.get('Content-Length')}")
    except Exception as exc:
        print(f"  {label:14s} FAIL {exc}")

if charge:
    charge_url = web_viewport_url(actor, closeup=True, clip_json=charge)
    print("charge viewport url:", charge_url)
