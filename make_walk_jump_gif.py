"""Capture walk+jump Mixer frames → GIF."""
from __future__ import annotations

import sys
import time
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))

from fbx_actor import (  # noqa: E402
    FbxActor,
    _capture_with_playwright,
    ensure_server,
    web_viewport_url,
)

OUT_DIR = ROOT / "proof" / "compare" / "gif_frames"
OUT_GIF = ROOT / "proof" / "compare" / "hualuo_walk_jump.gif"
CLIP_DIR = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "mapviewer_clips"

WALK_TS = [0.05, 0.22, 0.40, 0.55, 0.70, 0.90, 1.10, 1.30]
JUMP_TS = [0.05, 0.15, 0.25, 0.35, 0.45, 0.55, 0.68]


def main():
    walk = CLIP_DIR / "hualuo_walk.fbx"
    jump = CLIP_DIR / "hualuo_jump1.fbx"
    skin = CLIP_DIR / "hualuo_no_anim.fbx"
    if not walk.is_file() or not jump.is_file():
        raise SystemExit(f"missing clips in {CLIP_DIR}")
    tex = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "tex"
    actor = FbxActor(fbx_path=skin if skin.is_file() else (ROOT / "samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx"), tex_dir=tex)
    ensure_server(actor)
    time.sleep(0.3)
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    frames = []
    for i, t in enumerate(WALK_TS):
        out = OUT_DIR / f"w{i:02d}.png"
        url = web_viewport_url(actor, closeup=True, clip_fbx=walk, t=t, playing=False)
        _capture_with_playwright(url, out, timeout_ms=90000)
        print("walk", t, out.stat().st_size, flush=True)
        frames.append(out)
    for i, t in enumerate(JUMP_TS):
        out = OUT_DIR / f"j{i:02d}.png"
        url = web_viewport_url(actor, closeup=True, clip_fbx=jump, t=t, playing=False)
        _capture_with_playwright(url, out, timeout_ms=90000)
        print("jump", t, out.stat().st_size, flush=True)
        frames.append(out)

    imgs = []
    for p in frames:
        im = Image.open(p).convert("RGB")
        im.thumbnail((640, 480), Image.Resampling.LANCZOS)
        imgs.append(im)
    imgs[0].save(OUT_GIF, save_all=True, append_images=imgs[1:], duration=110, loop=0, optimize=True)
    print("GIF", OUT_GIF, OUT_GIF.stat().st_size, flush=True)


if __name__ == "__main__":
    main()
